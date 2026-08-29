using System.Globalization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using GonieGonie.SimpleDragon.Rhino;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public sealed class ExtractSimpleDragonZonesComponent : SimpleDragonComponent
{
    public ExtractSimpleDragonZonesComponent()
        : base(
            "Extract SimpleDragon Zones",
            "SD Zones",
            "Extracts area-based SimpleDragon zones from closed polygonal Breps. Azimuths use world north; model North Axis is supplied later when assembling the GRM.",
            SimpleDragonPanels.Geometry)
    {
    }

    public override Guid ComponentGuid => new("668591e2-458a-42a2-a924-6c3862f1b2c6");

    public override GH_Exposure Exposure => GH_Exposure.hidden;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddBrepParameter("Zone Breps", "B", "Closed polygonal Breps, one per thermal zone.", GH_ParamAccess.list);
        pManager.AddTextParameter("Names", "N", "One name per Brep, one base name, or empty for generated names.", GH_ParamAccess.list);
        pManager.AddIntegerParameter("Floor Numbers", "F", "One floor number per Brep, one shared number, or empty for floor zero.", GH_ParamAccess.list);
        pManager.AddParameter(new SimpleDragonUsageProfileParam(), "Profile", "P", "Usage profile shared by the extracted zones.", GH_ParamAccess.item);
        pManager.AddParameter(
            new SimpleDragonSurfaceConstructionParam(),
            "Surface Construction",
            "SC",
            "Optional default construction for every extracted face.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new SimpleDragonFenestrationConstructionParam(),
            "Fenestration Construction",
            "FC",
            "Optional default construction for Brep inner loops and separate opening curves.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Unmatched Floor Boundary",
            "Floor BC",
            "Ground, Outdoors, or Adiabatic for unmatched floor faces.",
            GH_ParamAccess.item,
            "Ground");
        pManager.AddNumberParameter("Lighting Power Density", "LPD", "Shared lighting power density in W/m\u00B2.", GH_ParamAccess.item, 10);
        pManager.AddCurveParameter("Opening Curves", "O", "Optional separate closed planar opening curves.", GH_ParamAccess.list);
        pManager.AddIntegerParameter("Opening Zone Indices", "OZ", "Zero-based zone index for each separate opening.", GH_ParamAccess.list);
        pManager.AddIntegerParameter("Opening Face Indices", "OF", "Zero-based Brep face index for each separate opening.", GH_ParamAccess.list);
        pManager[1].Optional = true;
        pManager[2].Optional = true;
        pManager[4].Optional = true;
        pManager[5].Optional = true;
        pManager[8].Optional = true;
        pManager[9].Optional = true;
        pManager[10].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new SimpleDragonZoneParam(), "Zones", "Z", "Extracted SimpleDragon zones.", GH_ParamAccess.list);
        pManager.AddParameter(new SimpleDragonSurfaceParam(), "Surfaces", "S", "Extracted area-based surfaces.", GH_ParamAccess.list);
        pManager.AddTextParameter("Geometry Map", "Map", "Domain ID to Rhino source/face/loop mapping.", GH_ParamAccess.list);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Geometry abstraction and validation diagnostics.", GH_ParamAccess.list);
        pManager.AddGenericParameter(
            "Geometry Map Data",
            "Map Data",
            "Structured Rhino-independent geometry mappings for CSV export and downstream data workflows.",
            GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        var breps = new List<Brep>();
        var names = new List<string>();
        var floorNumbers = new List<int>();
        SimpleDragonUsageProfileGoo? profileGoo = null;
        SimpleDragonSurfaceConstructionGoo? surfaceConstructionGoo = null;
        SimpleDragonFenestrationConstructionGoo? fenestrationConstructionGoo = null;
        string floorBoundaryText = "Ground";
        double lightDensity = 10;
        var openings = new List<Curve>();
        var openingZoneIndices = new List<int>();
        var openingFaceIndices = new List<int>();
        if (!DA.GetDataList(0, breps)
            || !DA.GetData(3, ref profileGoo)
            || !DA.GetData(6, ref floorBoundaryText)
            || !DA.GetData(7, ref lightDensity))
        {
            return;
        }

        DA.GetDataList(1, names);
        DA.GetDataList(2, floorNumbers);
        DA.GetData(4, ref surfaceConstructionGoo);
        DA.GetData(5, ref fenestrationConstructionGoo);
        DA.GetDataList(8, openings);
        DA.GetDataList(9, openingZoneIndices);
        DA.GetDataList(10, openingFaceIndices);
        if (profileGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Profile is required.");
            return;
        }

        if (!Enum.TryParse(floorBoundaryText.Trim(), true, out SurfaceBoundaryCondition floorBoundary))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unknown unmatched floor boundary '" + floorBoundaryText + "'.");
            return;
        }

        ValidateParallelValues(names.Count, breps.Count, "Names");
        ValidateParallelValues(floorNumbers.Count, breps.Count, "Floor Numbers");
        if (openings.Count != openingZoneIndices.Count || openings.Count != openingFaceIndices.Count)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "Opening Curves, Opening Zone Indices, and Opening Face Indices must have equal lengths.");
            return;
        }

        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "An active Rhino document is required for units and tolerances.");
            return;
        }

        var openingByZone = Enumerable.Range(0, breps.Count)
            .Select(_ => new List<RhinoFenestrationSource>())
            .ToArray();
        string openingConstructionId = fenestrationConstructionGoo?.Value?.Id.Value
            ?? "RHINO-UNRESOLVED-FENESTRATION";
        for (int index = 0; index < openings.Count; index++)
        {
            int zoneIndex = openingZoneIndices[index];
            if (zoneIndex < 0 || zoneIndex >= breps.Count)
            {
                throw new ArgumentException(
                    "Opening Zone Index at position " + index + " is outside the Brep list.");
            }

            openingByZone[zoneIndex].Add(new RhinoFenestrationSource(
                openings[index],
                openingFaceIndices[index],
                "Opening " + (index + 1).ToString(CultureInfo.InvariantCulture),
                FenestrationType.Window,
                openingConstructionId,
                fenestrationConstructionGoo?.Value,
                grasshopperIndex: index));
        }

        RhinoZoneSource[] sources = breps.Select((brep, index) => new RhinoZoneSource(
            brep,
            ResolveName(names, index),
            ResolveFloor(floorNumbers, index),
            profileGoo.Value.Name,
            profileGoo.Value,
            lightDensity,
            grasshopperIndex: index,
            fenestrations: openingByZone[index])).ToArray();
        var options = new RhinoZoneExtractionOptions
        {
            UnmatchedFloorBoundary = floorBoundary,
            DefaultSurfaceConstruction = surfaceConstructionGoo?.Value,
            DefaultFenestrationConstruction = fenestrationConstructionGoo?.Value,
            UnresolvedFenestrationConstructionId = openingConstructionId,
        };
        RhinoZoneExtractionResult extraction = RhinoZoneExtractor.Extract(
            sources,
            RhinoGeometryContext.FromDocument(document),
            options);
        var diagnostics = extraction.Diagnostics.ToList();
        diagnostics.Add(new Diagnostic(
            "SD.GH.AZIMUTH_USES_WORLD_NORTH",
            DiagnosticSeverity.Info,
            "Extracted wall azimuths use Rhino world north. GRM North Axis remains a separate model value and is not applied during extraction.",
            suggestedAction: "Set North Axis only in Assemble SimpleDragon GRM; do not pre-rotate extracted azimuths."));
        Report(diagnostics);
        DA.SetDataList(0, extraction.Zones.Select(item => new SimpleDragonZoneGoo(item)));
        DA.SetDataList(1, extraction.Zones.SelectMany(item => item.Surfaces).Select(item => new SimpleDragonSurfaceGoo(item)));
        DA.SetDataList(2, extraction.GeometryMap.Select(FormatMap));
        DA.SetDataList(3, diagnostics.Select(item => new DiagnosticGoo(item)));
        DA.SetDataList(4, extraction.GeometryMap.Select(ToCoreGeometryMapEntry));
    }

    private static void ValidateParallelValues(int valueCount, int sourceCount, string description)
    {
        if (valueCount != 0 && valueCount != 1 && valueCount != sourceCount)
        {
            throw new ArgumentException(
                description + " must be empty, contain one shared value, or contain one value per Brep.");
        }
    }

    private static string ResolveName(IReadOnlyList<string> names, int index)
    {
        if (names.Count == 0)
        {
            return "Zone " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        if (names.Count == 1 && index > 0)
        {
            return names[0] + " " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        return names.Count == 1 ? names[0] : names[index];
    }

    private static int ResolveFloor(IReadOnlyList<int> floors, int index)
    {
        return floors.Count == 0 ? 0 : floors.Count == 1 ? floors[0] : floors[index];
    }

    private static string FormatMap(RhinoDomainGeometryMapEntry entry)
    {
        string face = entry.FaceIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
        string loop = entry.LoopIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
        return entry.EntityId.Value
            + " | " + entry.Kind
            + " | source " + entry.SourceIndex.ToString(CultureInfo.InvariantCulture)
            + " | face " + face
            + " | loop " + loop
            + " | " + entry.Provenance.GeometryFingerprint;
    }

    private static GreenRetrofitGeometryMapEntry ToCoreGeometryMapEntry(
        RhinoDomainGeometryMapEntry entry)
    {
        GreenRetrofitGeometryKind kind = entry.Kind switch
        {
            RhinoMappedGeometryKind.Zone => GreenRetrofitGeometryKind.Zone,
            RhinoMappedGeometryKind.Surface => GreenRetrofitGeometryKind.Surface,
            RhinoMappedGeometryKind.Fenestration => GreenRetrofitGeometryKind.Fenestration,
            _ => throw new ArgumentOutOfRangeException(nameof(entry)),
        };
        return new GreenRetrofitGeometryMapEntry(
            entry.EntityId,
            kind,
            entry.SourceIndex,
            entry.FaceIndex,
            entry.BrepLoopIndex,
            entry.FenestrationSourceIndex,
            entry.Provenance);
    }
}
