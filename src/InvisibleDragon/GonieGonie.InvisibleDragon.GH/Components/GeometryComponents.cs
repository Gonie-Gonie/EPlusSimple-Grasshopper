using Grasshopper.Kernel;
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
        pManager.AddTextParameter("ID", "ID", "Optional stable window identifier.", GH_ParamAccess.item, string.Empty);
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
        string id = string.Empty;
        if (!DA.GetData(0, ref curve)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref glazingGoo))
        {
            return;
        }

        DA.GetData(3, ref id);
        Glazing glazing = glazingGoo?.Value
            ?? throw new ArgumentException("Glazing requires a non-empty value.");
        OpeningGeometry geometry = OpeningGeometry.FromCurve(curve, "Window");
        var window = new Window(
            StableIds.Resolve(id, "window", name, geometry.Fingerprint, glazing.Name),
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
        pManager.AddTextParameter("ID", "ID", "Optional stable door identifier.", GH_ParamAccess.item, string.Empty);
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
        string id = string.Empty;
        if (!DA.GetData(0, ref curve)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref constructionGoo))
        {
            return;
        }

        DA.GetData(3, ref id);
        ISurfaceConstruction construction = constructionGoo?.Value
            ?? throw new ArgumentException("Construction requires a non-empty value.");
        OpeningGeometry geometry = OpeningGeometry.FromCurve(curve, "Door");
        var door = new Door(
            StableIds.Resolve(id, "door", name, geometry.Fingerprint, construction.Name),
            name,
            construction,
            geometry.Polygon,
            geometry.Provenance);
        DA.SetData(0, new DragonOpeningGoo(door));
    }
}

public sealed class SurfaceComponent : DragonComponent
{
    public SurfaceComponent()
        : base(
            "Surface",
            "Surface",
            "Creates an opaque surface with directly owned openings. Interzone adjacency is inferred by Energy Model.",
            DragonPanels.Geometry)
    {
    }

    public override Guid ComponentGuid => new("c25eb6d8-9500-44e5-9909-58d41de0a320");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddCurveParameter("Curve", "C", "Closed planar polygonal surface boundary.", GH_ParamAccess.item);
        pManager.AddTextParameter("Name", "N", "Surface name.", GH_ParamAccess.item, "Surface");
        pManager.AddTextParameter("Type", "T", "Wall, Ceiling, or Floor.", GH_ParamAccess.item, "Wall");
        pManager.AddParameter(
            new DragonConstructionParam(),
            "Construction",
            "C",
            "Opaque surface construction.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Boundary Intent",
            "BC",
            "Outdoors, Ground, or Adiabatic. Coincident surfaces in distinct Zones become reciprocal Zone boundaries automatically.",
            GH_ParamAccess.item,
            "Outdoors");
        int openings = pManager.AddParameter(
            new DragonOpeningParam(),
            "Openings",
            "O",
            "Windows and doors owned by this Surface.",
            GH_ParamAccess.list);
        pManager[openings].Optional = true;
        pManager.AddTextParameter("ID", "ID", "Optional stable surface identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSurfaceParam(), "Surface", "S", "InvisibleDragon surface.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Gross Area", "Gross", "Surface gross area in m².", GH_ParamAccess.item);
        pManager.AddNumberParameter("Net Area", "Net", "Opaque net area after openings in m².", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Valid", "V", "True when opening containment and overlap validation pass.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Surface and opening diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        Curve? curve = null;
        string name = "Surface";
        string typeText = "Wall";
        DragonConstructionGoo? constructionGoo = null;
        string boundaryText = "Outdoors";
        var openingGoos = new List<DragonOpeningGoo>();
        string id = string.Empty;
        if (!DA.GetData(0, ref curve)
            || !DA.GetData(1, ref name)
            || !DA.GetData(2, ref typeText)
            || !DA.GetData(3, ref constructionGoo)
            || !DA.GetData(4, ref boundaryText))
        {
            return;
        }

        DA.GetDataList(5, openingGoos);
        DA.GetData(6, ref id);
        ISurfaceConstruction construction = constructionGoo?.Value
            ?? throw new ArgumentException("Construction requires a non-empty value.");
        if (!Enum.TryParse(typeText.Trim(), true, out SurfaceType surfaceType)
            || !Enum.IsDefined(typeof(SurfaceType), surfaceType))
        {
            throw new ArgumentException("Type must be Wall, Ceiling, or Floor.");
        }

        if (!Enum.TryParse(boundaryText.Trim(), true, out SurfaceBoundaryCondition boundaryCondition)
            || (boundaryCondition != SurfaceBoundaryCondition.Outdoors
                && boundaryCondition != SurfaceBoundaryCondition.Ground
                && boundaryCondition != SurfaceBoundaryCondition.Adiabatic))
        {
            throw new ArgumentException(
                "Boundary Intent must be Outdoors, Ground, or Adiabatic. Interzone adjacency is inferred automatically.");
        }

        OpeningGeometry geometry = OpeningGeometry.FromCurve(curve, "Surface");
        IOpening[] openingValues = openingGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException("Openings contains an empty value at position " + index + "."))
            .ToArray();
        var surface = new DragonSurface(
            StableIds.Resolve(id, "surface", name, geometry.Fingerprint),
            name,
            surfaceType,
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

    internal static OpeningGeometry FromCurve(Curve? curve, string inputName)
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
            new GeometryProvenance(null, null, fingerprint, null, null));
    }
}
