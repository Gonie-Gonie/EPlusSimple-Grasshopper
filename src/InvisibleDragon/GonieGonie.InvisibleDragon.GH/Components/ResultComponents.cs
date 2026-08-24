using Grasshopper.Kernel;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Results;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class ReadEnergyPlusResultsComponent : DragonComponent
{
    public ReadEnergyPlusResultsComponent()
        : base(
            "Read EnergyPlus Results",
            "ReadR",
            "Reads eplusout.err, audit, boundary, and tabular CSV files from an EnergyPlus output directory.",
            DragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("af9419cd-0d68-4ee2-870b-b2ac04c95a41");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Directory", "Dir", "EnergyPlus output directory.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new EnergyPlusResultParam(), "Result", "R", "Structured EnergyPlus result.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Parsed EnergyPlus diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string directory = string.Empty;
        if (!DA.GetData(0, ref directory))
        {
            return;
        }

        EnergyPlusSimulationResult result = EnergyPlusResultParser.ParseDirectory(directory);
        Report(result.Diagnostics.Diagnostics);
        DA.SetData(0, new EnergyPlusResultGoo(result));
        DA.SetDataList(1, result.Diagnostics.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class EnergyPlusResultSummaryComponent : DragonComponent
{
    public EnergyPlusResultSummaryComponent()
        : base(
            "EnergyPlus Result Summary",
            "Sum",
            "Reports run state, diagnostic counts, elapsed time, and available monthly tables.",
            DragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("31967aee-84ae-4536-b091-b301d1ab2c3d");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new EnergyPlusResultParam(), "Result", "R", "Structured EnergyPlus result.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Run ID", "ID", "Runtime run identifier, if known.", GH_ParamAccess.item);
        pManager.AddTextParameter("State", "S", "Runtime state, if known.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Success", "OK", "Runtime or EnergyPlus completion success.", GH_ParamAccess.item);
        pManager.AddIntegerParameter("Warnings", "W", "EnergyPlus warning count.", GH_ParamAccess.item);
        pManager.AddIntegerParameter("Severe", "E", "EnergyPlus severe error count.", GH_ParamAccess.item);
        pManager.AddIntegerParameter("Fatal", "F", "EnergyPlus fatal error count.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Elapsed", "Sec", "Elapsed seconds, if known.", GH_ParamAccess.item);
        pManager.AddTextParameter("Monthly Tables", "M", "Available monthly table titles.", GH_ParamAccess.list);
        pManager.AddTextParameter("Work Directory", "Dir", "EnergyPlus work directory, if known.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "EnergyPlus diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        EnergyPlusResultGoo? resultGoo = null;
        if (!DA.GetData(0, ref resultGoo) || resultGoo?.Value is null)
        {
            return;
        }

        EnergyPlusSimulationResult result = resultGoo.Value;
        EnergyPlusDiagnosticSummary summary = result.ErrorLog.Summary;
        bool success = result.Metadata.RuntimeSucceeded ?? summary.CompletedSuccessfully;
        double elapsed = result.Metadata.RuntimeElapsedSeconds
            ?? summary.ReportedElapsedSeconds
            ?? 0;
        Report(result.Diagnostics.Diagnostics);
        DA.SetData(0, result.Metadata.RunId ?? string.Empty);
        DA.SetData(1, result.Metadata.RuntimeState?.ToString() ?? string.Empty);
        DA.SetData(2, success);
        DA.SetData(3, summary.WarningCount);
        DA.SetData(4, summary.SevereCount);
        DA.SetData(5, summary.FatalCount);
        DA.SetData(6, elapsed);
        DA.SetDataList(7, result.MonthlyTables.Select(table => string.IsNullOrWhiteSpace(table.Title) ? table.ReportName : table.Title));
        DA.SetData(8, result.Metadata.WorkDirectory ?? string.Empty);
        DA.SetDataList(9, result.Diagnostics.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}
