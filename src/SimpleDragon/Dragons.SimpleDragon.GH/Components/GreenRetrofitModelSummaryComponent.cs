using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Components;

public sealed class GreenRetrofitModelSummaryComponent : SimpleDragonComponent
{
    public GreenRetrofitModelSummaryComponent()
        : base(
            "SimpleDragon Model Summary",
            "SD Model Summary",
            "Extracts envelope, load, and weather summary values directly from a SimpleDragon GRM.",
            SimpleDragonPanels.Analysis)
    {
    }

    public override Guid ComponentGuid => new("f2e7bb6b-9cf9-4833-9069-b9be4089e1b3");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new GreenRetrofitModelParam(),
            "GRM",
            "GRM",
            "SimpleDragon model to summarize.",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddNumberParameter(
            "Floor Area",
            "Area",
            "Total conditioned zone floor area in m\u00B2.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new SimpleDragonSurfaceParam(),
            "Exterior Floors",
            "Floors",
            "Exterior or ground-contact floor surfaces.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonSurfaceParam(),
            "Exterior Roofs",
            "Roofs",
            "Outdoor ceiling surfaces used as exterior roofs.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonSurfaceParam(),
            "Exterior Walls",
            "Walls",
            "Outdoor wall surfaces.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonFenestrationParam(),
            "Exterior Windows",
            "Windows",
            "Windows and glass doors hosted by exterior walls.",
            GH_ParamAccess.list);
        pManager.AddNumberParameter(
            "Average Exterior Floor U-Value",
            "Floor U",
            "Area-weighted U-value of exterior and ground-contact floors in W/(m\u00B2\u00B7K).",
            GH_ParamAccess.item);
        pManager.AddNumberParameter(
            "Average Exterior Roof U-Value",
            "Roof U",
            "Area-weighted U-value of exterior roofs in W/(m\u00B2\u00B7K).",
            GH_ParamAccess.item);
        pManager.AddNumberParameter(
            "Average Exterior Wall U-Value",
            "Wall U",
            "Area-weighted U-value of exterior walls in W/(m\u00B2\u00B7K).",
            GH_ParamAccess.item);
        pManager.AddNumberParameter(
            "Average Window U-Value",
            "Window U",
            "Area-weighted U-value of exterior windows and glass doors in W/(m\u00B2\u00B7K).",
            GH_ParamAccess.item);
        pManager.AddNumberParameter(
            "Average Infiltration at 50 Pa",
            "ACH50",
            "Zone-volume-weighted average infiltration rate at 50 Pa in air changes per hour.",
            GH_ParamAccess.item);
        pManager.AddNumberParameter(
            "Average Lighting Power Density",
            "LPD",
            "Zone-area-weighted average lighting power density in W/m\u00B2 for zones with a defined value.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Climate Region",
            "Climate",
            "Resolved climate region embedded in the model.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Terrain",
            "Terrain",
            "Resolved terrain category embedded in the model.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Weather Location",
            "Weather",
            "Resolved weather-station location embedded in the model.",
            GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitModelGoo? modelGoo = null;
        if (!DA.GetData(0, ref modelGoo) || modelGoo?.Value is null)
        {
            return;
        }

        GreenRetrofitModel model = modelGoo.Value;
        DA.SetData(0, model.Area);
        DA.SetDataList(1, model.ExteriorFloors.Select(surface => new SimpleDragonSurfaceGoo(surface)));
        DA.SetDataList(2, model.ExteriorRoofs.Select(surface => new SimpleDragonSurfaceGoo(surface)));
        DA.SetDataList(3, model.ExteriorWalls.Select(surface => new SimpleDragonSurfaceGoo(surface)));
        DA.SetDataList(4, model.ExteriorWindows.Select(window => new SimpleDragonFenestrationGoo(window)));
        DA.SetData(5, model.AverageExteriorFloorUValue);
        DA.SetData(6, model.AverageExteriorRoofUValue);
        DA.SetData(7, model.AverageExteriorWallUValue);
        DA.SetData(8, model.AverageWindowUValue);
        DA.SetData(9, model.AverageInfiltration);
        DA.SetData(10, model.AverageLightDensity);

        if (model.Weather is null)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "The GRM has no resolved weather metadata; climate region, terrain, and weather location are unavailable.");
            return;
        }

        DA.SetData(11, model.Weather.ClimateRegion);
        DA.SetData(12, model.Weather.Terrain);
        DA.SetData(13, model.Weather.WeatherLocation);
    }
}
