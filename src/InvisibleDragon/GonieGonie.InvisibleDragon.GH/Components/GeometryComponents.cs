using Grasshopper.Kernel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.InvisibleDragon.Shape;
using Rhino;
using Rhino.Geometry;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class SurfaceFromPolylineComponent : DragonComponent
{
    public SurfaceFromPolylineComponent()
        : base(
            "Surface From Polyline",
            "Surf",
            "Converts a closed planar Rhino polyline curve into an SI InvisibleDragon surface.",
            DragonPanels.Geometry)
    {
    }

    public override Guid ComponentGuid => new("291150ba-bbb5-41c2-99ac-914a5183d3ed");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddCurveParameter("Boundary", "B", "Closed planar polygonal boundary curve.", GH_ParamAccess.item);
        pManager.AddTextParameter("Name", "N", "Surface name.", GH_ParamAccess.item, "Surface");
        pManager.AddTextParameter("Type", "T", "Wall, Ceiling, or Floor.", GH_ParamAccess.item, "Wall");
        pManager.AddParameter(
            new DragonConstructionParam(),
            "Construction",
            "C",
            "Opaque InvisibleDragon surface construction.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Boundary Condition",
            "BC",
            "Outdoors, Ground, Adiabatic, or Zone.",
            GH_ParamAccess.item,
            "Outdoors");
        pManager.AddTextParameter(
            "Adjacent Surface ID",
            "Adj",
            "Required only when Boundary Condition is Zone.",
            GH_ParamAccess.item,
            string.Empty);
        pManager.AddTextParameter("ID", "ID", "Optional stable surface identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonSurfaceParam(), "Surface", "S", "InvisibleDragon surface.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Gross Area", "A", "Surface gross area in m².", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Surface validation diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        Curve? curve = null;
        string name = "Surface";
        string typeText = "Wall";
        DragonConstructionGoo? construction = null;
        string boundaryText = "Outdoors";
        string adjacentId = string.Empty;
        string id = string.Empty;
        if (!DA.GetData(0, ref curve) ||
            !DA.GetData(1, ref name) ||
            !DA.GetData(2, ref typeText) ||
            !DA.GetData(3, ref construction) ||
            !DA.GetData(4, ref boundaryText))
        {
            return;
        }

        DA.GetData(5, ref adjacentId);
        DA.GetData(6, ref id);
        if (curve is null || construction?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Boundary and Construction are required.");
            return;
        }

        if (!Enum.TryParse(typeText.Trim(), true, out SurfaceType surfaceType))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown surface type '{typeText}'.");
            return;
        }

        if (!Enum.TryParse(boundaryText.Trim(), true, out SurfaceBoundaryCondition boundaryCondition))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown boundary condition '{boundaryText}'.");
            return;
        }

        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "An active Rhino document is required for unit and tolerance conversion.");
            return;
        }

        var context = RhinoGeometryContext.FromDocument(document);
        PlanarPolygon polygon = RhinoPolygonConverter.FromClosedCurve(curve, context);
        string fingerprint = RhinoGeometryFingerprint.ForPolygon(polygon);
        var provenance = new GeometryProvenance(null, null, fingerprint, null, null);
        SurfaceBoundary boundary = boundaryCondition == SurfaceBoundaryCondition.Zone
            ? SurfaceBoundary.AdjacentTo(new EntityId(adjacentId))
            : new SurfaceBoundary(boundaryCondition);
        var surface = new DragonSurface(
            StableIds.Resolve(id, "surface", name, fingerprint),
            name,
            surfaceType,
            construction.Value,
            boundary,
            polygon,
            provenance: provenance);
        var diagnostics = surface.Validate().Diagnostics;
        Report(diagnostics);
        DA.SetData(0, new DragonSurfaceGoo(surface));
        DA.SetData(1, surface.GrossArea);
        DA.SetDataList(2, diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}
