using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public sealed class BuildGreenRetrofitResultComponent : SimpleDragonComponent
{
    public BuildGreenRetrofitResultComponent()
        : base(
            "Build SimpleDragon GRR",
            "Build GRR",
            "Builds a GRR 0.7 result from a GRM and InvisibleDragon EnergyPlus monthly tables.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("2a9f3a4e-56f2-4227-8725-e8befe43cf53");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitModelParam(), "GRM", "GRM", "The simulated SimpleDragon model.", GH_ParamAccess.item);
        pManager.AddParameter(
            new EnergyPlusResultParam(),
            "EnergyPlus Result",
            "E+",
            "Parsed InvisibleDragon EnergyPlus result with monthly tables.",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitResultParam(), "GRR", "GRR", "Built GRR result.", GH_ParamAccess.item);
        pManager.AddTextParameter("JSON", "J", "Deterministic GRR JSON.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Success", "OK", "True when all required monthly data was converted.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "GRR build diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitModelGoo? modelGoo = null;
        EnergyPlusResultGoo? simulationGoo = null;
        if (!DA.GetData(0, ref modelGoo)
            || !DA.GetData(1, ref simulationGoo)
            || modelGoo?.Value is null
            || simulationGoo?.Value is null)
        {
            return;
        }

        GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
            modelGoo.Value,
            simulationGoo.Value);
        Report(build.Diagnostics);
        if (build.Result is not null)
        {
            DA.SetData(0, new GreenRetrofitResultGoo(build.Result));
            DA.SetData(1, GrrWriter.Serialize(build.Result));
        }

        DA.SetData(2, build.Success);
        DA.SetDataList(3, build.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class ReadGreenRetrofitResultComponent : SimpleDragonComponent
{
    public ReadGreenRetrofitResultComponent()
        : base(
            "Read SimpleDragon GRR",
            "Read GRR",
            "Reads a strict UTF-8 GRR 0.7 result file.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("a03fb1d7-7ae2-4e2c-ab31-0e626af50163");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Path", "P", "Path to a GRR JSON file.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitResultParam(), "GRR", "GRR", "Parsed GRR result.", GH_ParamAccess.item);
        pManager.AddTextParameter("Canonical JSON", "J", "Deterministic canonical GRR JSON.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Success", "OK", "True when the GRR is complete and valid.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "GRR read diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string path = string.Empty;
        if (!DA.GetData(0, ref path))
        {
            return;
        }

        string fullPath = ResolveDocumentPath(path);
        GrrReadResult read = GrrReader.ReadFile(fullPath);
        Report(read.Diagnostics);
        if (read.Result is not null)
        {
            DA.SetData(0, new GreenRetrofitResultGoo(read.Result));
            DA.SetData(1, GrrWriter.Serialize(read.Result));
        }

        DA.SetData(2, read.Success);
        DA.SetDataList(3, read.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class WriteGreenRetrofitResultComponent : SimpleDragonComponent
{
    public WriteGreenRetrofitResultComponent()
        : base(
            "Write SimpleDragon GRR",
            "Write GRR",
            "Writes deterministic UTF-8 GRR 0.7 JSON when Write is true.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("38a9036e-813c-435a-b573-022660b2fbb9");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitResultParam(), "GRR", "GRR", "GRR result to serialize.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Path",
            "P",
            "Destination .grr or JSON path. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter("Write", "W", "Explicit write trigger.", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("JSON", "J", "Deterministic GRR JSON.", GH_ParamAccess.item);
        pManager.AddTextParameter("Full Path", "P", "Resolved destination path.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Written", "OK", "True when the file was written during this solution.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitResultGoo? resultGoo = null;
        string path = string.Empty;
        bool write = false;
        if (!DA.GetData(0, ref resultGoo)
            || !DA.GetData(1, ref path)
            || !DA.GetData(2, ref write)
            || resultGoo?.Value is null)
        {
            return;
        }

        string fullPath = ResolveDocumentPath(path, Path.GetTempPath());
        string json = GrrWriter.Serialize(resultGoo.Value);
        if (write)
        {
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            GrrWriter.WriteFile(fullPath, resultGoo.Value);
        }

        DA.SetData(0, json);
        DA.SetData(1, fullPath);
        DA.SetData(2, write);
    }
}

public sealed class GreenRetrofitResultSummaryComponent : SimpleDragonComponent
{
    public GreenRetrofitResultSummaryComponent()
        : base(
            "SimpleDragon GRR Summary",
            "GRR Summary",
            "Extracts annual, monthly, carrier, and end-use totals for one GRR metric.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("577809aa-2d1c-40ea-aa50-f71d73f19f83");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitResultParam(), "GRR", "GRR", "SimpleDragon result.", GH_ParamAccess.item);
        pManager.AddTextParameter("Metric", "M", "SiteUses, SourceUses, Carbon, or Cost.", GH_ParamAccess.item, "SiteUses");
        pManager.AddBooleanParameter("Gross", "G", "False for per-area values; true for gross building values.", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddNumberParameter("Total Area", "A", "Building floor area in m\u00B2.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Annual Total", "Annual", "Net annual total for the selected metric.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Monthly Totals", "Monthly", "January through December net totals.", GH_ParamAccess.list);
        pManager.AddTextParameter("Carriers", "C", "Energy carrier names.", GH_ParamAccess.list);
        pManager.AddNumberParameter("Carrier Totals", "CV", "Totals corresponding to Carriers.", GH_ParamAccess.list);
        pManager.AddTextParameter("End Uses", "E", "Energy end-use names.", GH_ParamAccess.list);
        pManager.AddNumberParameter("End-Use Totals", "EV", "Totals corresponding to End Uses.", GH_ParamAccess.list);
        pManager.AddTextParameter("Basis", "B", "Selected metric and gross/per-area basis.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitResultGoo? resultGoo = null;
        string metricText = "SiteUses";
        bool gross = false;
        if (!DA.GetData(0, ref resultGoo)
            || !DA.GetData(1, ref metricText)
            || !DA.GetData(2, ref gross)
            || resultGoo?.Value is null)
        {
            return;
        }

        if (!Enum.TryParse(metricText.Trim(), true, out GreenRetrofitMetric metric))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unknown GRR metric '" + metricText + "'.");
            return;
        }

        GreenRetrofitSummary summary = gross
            ? resultGoo.Value.GrossSummaries[metric]
            : resultGoo.Value.PerAreaSummaries[metric];
        EnergyCarrier[] carriers = (EnergyCarrier[])Enum.GetValues(typeof(EnergyCarrier));
        EnergyEndUse[] endUses = (EnergyEndUse[])Enum.GetValues(typeof(EnergyEndUse));
        DA.SetData(0, resultGoo.Value.TotalArea);
        DA.SetData(1, summary.AnnualTotal);
        DA.SetDataList(2, summary.MonthlyTotal);
        DA.SetDataList(3, carriers.Select(item => item.ToString()));
        DA.SetDataList(4, carriers.Select(item => summary.CarrierTotals[item]));
        DA.SetDataList(5, endUses.Select(item => item.ToString()));
        DA.SetDataList(6, endUses.Select(item => summary.EndUseTotals[item]));
        DA.SetData(7, metric + (gross ? " gross" : " per area"));
    }
}
