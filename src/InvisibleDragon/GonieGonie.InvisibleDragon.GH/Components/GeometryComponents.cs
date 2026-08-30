using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.InvisibleDragon.Shape;
using Rhino;
using Rhino.Geometry;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class GlazingComponent : DragonComponent
{
    public GlazingComponent()
        : base(
            "Glazing",
            "Glass",
            "Creates transparent glazing from its thermal and solar performance.",
            DragonPanels.Construction)
    {
    }

    public override Guid ComponentGuid => new("ecfd5cdd-3e4c-4261-8ddd-ecea8eaf5599");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Glazing name.", GH_ParamAccess.item, "Glazing");
        pManager.AddNumberParameter(
            "U-Value",
            "U",
            "Glazing U-value in W/(m² K).",
            GH_ParamAccess.item,
            1.5);
        pManager.AddNumberParameter(
            "SHGC",
            "g",
            "Solar heat-gain coefficient from 0 to 1.",
            GH_ParamAccess.item,
            0.5);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonGlazingParam(),
            "Glazing",
            "G",
            "InvisibleDragon glazing.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Glazing";
        double uValue = 1.5;
        double solarHeatGainCoefficient = 0.5;
        if (!DA.GetData(0, ref name)
            || !DA.GetData(1, ref uValue)
            || !DA.GetData(2, ref solarHeatGainCoefficient))
        {
            return;
        }

        DA.SetData(0, new DragonGlazingGoo(new Glazing(name, uValue, solarHeatGainCoefficient)));
    }
}

public sealed class WindowFromPolylineComponent : DragonComponent
{
    public WindowFromPolylineComponent()
        : base(
            "Window From Polyline",
            "Window",
            "Creates a polygonal window. Connect it directly to its owning Surface.",
            DragonPanels.Geometry)
    {
    }

    public override Guid ComponentGuid => new("54bb0065-1b10-420c-a90e-0ce75e746781");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddCurveParameter("Curve", "C", "Closed planar polygonal window boundary.", GH_ParamAccess.item);
        pManager.AddTextParameter("Name", "N", "Window name.", GH_ParamAccess.item, "Window");
        pManager.AddParameter(new DragonGlazingParam(), "Glazing", "G", "Window glazing.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonOpeningParam(),
            "Opening",
            "O",
            "InvisibleDragon window opening.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        Curve? curve = null;
        string name = "Window";
        DragonGlazingGoo? glazingGoo = null;
        if (!DA.GetData(0, ref curve)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref glazingGoo))
        {
            return;
        }

        Glazing glazing = glazingGoo?.Value
            ?? throw new ArgumentException("Glazing requires a non-empty value.");
        OpeningGeometry geometry = OpeningGeometry.FromCurve(
            curve,
            "Window",
            GrasshopperTarget.Path(DA, 0),
            GrasshopperTarget.Index(DA, 0));
        var window = new Window(
            StableIds.Create("window", name, geometry.Fingerprint, glazing.Name),
            name,
            glazing,
            geometry.Polygon,
            provenance: geometry.Provenance);
        DA.SetData(0, new DragonOpeningGoo(window));
    }
}

public sealed class DoorFromPolylineComponent : DragonComponent
{
    public DoorFromPolylineComponent()
        : base(
            "Door From Polyline",
            "Door",
            "Creates a polygonal opaque door. Connect it directly to its owning Surface.",
            DragonPanels.Geometry)
    {
    }

    public override Guid ComponentGuid => new("b2e1e805-a126-44fe-bf6c-4dbf16a76aae");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddCurveParameter("Curve", "C", "Closed planar polygonal door boundary.", GH_ParamAccess.item);
        pManager.AddTextParameter("Name", "N", "Door name.", GH_ParamAccess.item, "Door");
        pManager.AddParameter(
            new DragonConstructionParam(),
            "Construction",
            "C",
            "Opaque door construction.",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonOpeningParam(),
            "Opening",
            "O",
            "InvisibleDragon door opening.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        Curve? curve = null;
        string name = "Door";
        DragonConstructionGoo? constructionGoo = null;
        if (!DA.GetData(0, ref curve)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref constructionGoo))
        {
            return;
        }

        ISurfaceConstruction construction = constructionGoo?.Value
            ?? throw new ArgumentException("Construction requires a non-empty value.");
        OpeningGeometry geometry = OpeningGeometry.FromCurve(
            curve,
            "Door",
            GrasshopperTarget.Path(DA, 0),
            GrasshopperTarget.Index(DA, 0));
        var door = new Door(
            StableIds.Create("door", name, geometry.Fingerprint, construction.Name),
            name,
            construction,
            geometry.Polygon,
            geometry.Provenance);
        DA.SetData(0, new DragonOpeningGoo(door));
    }
}

public abstract class OpaqueSurfaceComponent : DragonComponent
{
    private static readonly SurfaceBoundaryCondition[] BoundaryChoices =
    {
        SurfaceBoundaryCondition.Outdoors,
        SurfaceBoundaryCondition.Ground,
        SurfaceBoundaryCondition.Adiabatic,
    };

    protected OpaqueSurfaceComponent(
        string name,
        string nickname,
        string description)
        : base(
            name,
            nickname,
            description,
            DragonPanels.Geometry)
    {
    }

    protected abstract SurfaceType FixedSurfaceType { get; }

    protected abstract string DefaultSurfaceName { get; }

    protected abstract SurfaceBoundaryCondition DefaultBoundaryCondition { get; }

    protected sealed override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddCurveParameter("Curve", "C", "Closed planar polygonal surface boundary.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Name",
            "N",
            FixedSurfaceType + " name.",
            GH_ParamAccess.item,
            DefaultSurfaceName);
        pManager.AddParameter(
            new DragonConstructionParam(),
            "Construction",
            "C",
            "Opaque " + FixedSurfaceType + " construction.",
            GH_ParamAccess.item);
        ChoiceInputs.AddEnum(
            pManager,
            "Boundary Condition",
            "BC",
            "Outdoors, Ground, or Adiabatic. Coincident surfaces in distinct Zones become reciprocal Zone boundaries automatically.",
            DefaultBoundaryCondition,
            BoundaryChoices);
        int openings = pManager.AddParameter(
            new DragonOpeningParam(),
            "Openings",
            "O",
            "Windows and doors owned by this " + FixedSurfaceType + ".",
            GH_ParamAccess.list);
        pManager[openings].Optional = true;
    }

    protected sealed override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSurfaceParam(), "Surface", "S", "InvisibleDragon surface.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Gross Area", "Gross", "Surface gross area in m².", GH_ParamAccess.item);
        pManager.AddNumberParameter("Net Area", "Net", "Opaque net area after openings in m².", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Valid", "V", "True when opening containment and overlap validation pass.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Surface and opening diagnostics.", GH_ParamAccess.list);
    }

    protected sealed override void Solve(IGH_DataAccess DA)
    {
        Curve? curve = null;
        string name = DefaultSurfaceName;
        DragonConstructionGoo? constructionGoo = null;
        string boundaryText = DefaultBoundaryCondition.ToString();
        var openingGoos = new List<DragonOpeningGoo>();
        if (!DA.GetData(0, ref curve)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref constructionGoo)
            || !DA.GetData(3, ref boundaryText))
        {
            return;
        }

        DA.GetDataList(4, openingGoos);
        ISurfaceConstruction construction = constructionGoo?.Value
            ?? throw new ArgumentException("Construction requires a non-empty value.");
        SurfaceBoundaryCondition boundaryCondition = ChoiceInputs.ParseEnum(
            boundaryText,
            "Boundary Condition",
            BoundaryChoices);
        OpeningGeometry geometry = OpeningGeometry.FromCurve(
            curve,
            FixedSurfaceType.ToString(),
            GrasshopperTarget.Path(DA, 0),
            GrasshopperTarget.Index(DA, 0));
        IOpening[] openingValues = openingGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException("Openings contains an empty value at position " + index + "."))
            .ToArray();
        var surface = new DragonSurface(
            StableIds.Create("surface", name, FixedSurfaceType.ToString(), geometry.Fingerprint),
            name,
            FixedSurfaceType,
            construction,
            new SurfaceBoundary(boundaryCondition),
            geometry.Polygon,
            openingValues,
            geometry.Provenance);
        ValidationResult validation = surface.Validate();
        Report(validation.Diagnostics);
        DA.SetData(0, new DragonSurfaceGoo(surface));
        DA.SetData(1, surface.GrossArea);
        DA.SetData(2, surface.NetArea);
        DA.SetData(3, validation.IsValid);
        DA.SetDataList(4, validation.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class FloorComponent : OpaqueSurfaceComponent
{
    public FloorComponent()
        : base(
            "Floor",
            "Floor",
            "Creates an opaque Floor with directly owned openings. Interzone adjacency is inferred by Energy Model.")
    {
    }

    public override Guid ComponentGuid => new("1938b273-3a60-459b-beb2-92e7c4905053");

    protected override SurfaceType FixedSurfaceType => SurfaceType.Floor;

    protected override string DefaultSurfaceName => "Floor";

    protected override SurfaceBoundaryCondition DefaultBoundaryCondition => SurfaceBoundaryCondition.Ground;
}

public sealed class CeilingComponent : OpaqueSurfaceComponent
{
    public CeilingComponent()
        : base(
            "Ceiling",
            "Ceiling",
            "Creates an opaque Ceiling with directly owned openings. Interzone adjacency is inferred by Energy Model.")
    {
    }

    public override Guid ComponentGuid => new("d1930bb6-4398-46b9-a661-451370f09103");

    protected override SurfaceType FixedSurfaceType => SurfaceType.Ceiling;

    protected override string DefaultSurfaceName => "Ceiling";

    protected override SurfaceBoundaryCondition DefaultBoundaryCondition => SurfaceBoundaryCondition.Outdoors;
}

public sealed class WallComponent : OpaqueSurfaceComponent
{
    public WallComponent()
        : base(
            "Wall",
            "Wall",
            "Creates an opaque Wall with directly owned openings. Interzone adjacency is inferred by Energy Model.")
    {
    }

    public override Guid ComponentGuid => new("20a8a2f5-845e-4a46-aa03-fb8849f592e2");

    protected override SurfaceType FixedSurfaceType => SurfaceType.Wall;

    protected override string DefaultSurfaceName => "Wall";

    protected override SurfaceBoundaryCondition DefaultBoundaryCondition => SurfaceBoundaryCondition.Outdoors;
}

internal sealed class OpeningGeometry
{
    private OpeningGeometry(PlanarPolygon polygon, string fingerprint, GeometryProvenance provenance)
    {
        Polygon = polygon;
        Fingerprint = fingerprint;
        Provenance = provenance;
    }

    internal PlanarPolygon Polygon { get; }

    internal string Fingerprint { get; }

    internal GeometryProvenance Provenance { get; }

    internal static OpeningGeometry FromCurve(
        Curve? curve,
        string inputName,
        string? grasshopperPath = null,
        int? grasshopperIndex = null)
    {
        if (curve is null)
        {
            throw new ArgumentException(inputName + " Curve requires a value.", nameof(curve));
        }

        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            throw new InvalidOperationException(
                "An active Rhino document is required for unit and tolerance conversion.");
        }

        var context = RhinoGeometryContext.FromDocument(document);
        PlanarPolygon polygon = RhinoPolygonConverter.FromClosedCurve(curve, context);
        string fingerprint = RhinoGeometryFingerprint.ForPolygon(polygon);
        return new OpeningGeometry(
            polygon,
            fingerprint,
            new GeometryProvenance(
                null,
                null,
                fingerprint,
                grasshopperPath,
                grasshopperIndex));
    }
}

internal static class GrasshopperTarget
{
    internal static string Path(IGH_DataAccess access, int parameterIndex)
    {
        GH_Path path = access.ParameterTargetPath(parameterIndex);
        return path.ToString();
    }

    internal static int? Index(IGH_DataAccess access, int parameterIndex)
    {
        int index = access.ParameterTargetIndex(parameterIndex);
        return index < 0 ? null : index;
    }
}
