using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Components;

public sealed class ReadGreenRetrofitModelComponent : SimpleDragonComponent
{
    public ReadGreenRetrofitModelComponent()
        : base(
            "Read SimpleDragon GRM",
            "Read GRM",
            "Reads a UTF-8 GRM 0.7 file and reports reference-resolution diagnostics.",
            SimpleDragonPanels.Simulation)
    {
    }

    public override Guid ComponentGuid => new("3dae48ad-3c81-41e5-8207-580ff3e096db");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Path", "P", "Path to a GRM JSON file.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitModelParam(), "GRM", "GRM", "Parsed GRM model.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonZoneParam(), "Zones", "Z", "Zones contained in the model.", GH_ParamAccess.list);
        pManager.AddTextParameter("Canonical JSON", "J", "Deterministic canonical GRM JSON.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Success", "OK", "True when parsing and reference resolution succeeded.", GH_ParamAccess.item);
        pManager.AddParameter(new SimpleDragonDiagnosticParam(), "Diagnostics", "D", "GRM read diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string path = string.Empty;
        if (!DA.GetData(0, ref path))
        {
            return;
        }

        string fullPath = ResolveDocumentPath(path);
        GrmReadResult read = GrmReader.ReadFile(fullPath);
        Report(read.Diagnostics);
        if (read.Model is not null)
        {
            DA.SetData(0, new GreenRetrofitModelGoo(read.Model));
            DA.SetDataList(1, read.Model.Zones.Select(item => new SimpleDragonZoneGoo(item)));
            DA.SetData(2, GrmWriter.Serialize(read.Model));
        }

        DA.SetData(3, read.Success);
        DA.SetDataList(4, read.Diagnostics.Select(item => new SimpleDragonDiagnosticGoo(item)));
    }
}

public sealed class WriteGreenRetrofitModelComponent : SimpleDragonComponent
{
    public WriteGreenRetrofitModelComponent()
        : base(
            "Write SimpleDragon GRM",
            "Write GRM",
            "Writes deterministic UTF-8 GRM 0.7 JSON when the Write Button is pressed.",
            SimpleDragonPanels.Simulation)
    {
    }

    public override Guid ComponentGuid => new("5d3c5ff1-03e3-4b2e-85a5-43b36f856d92");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitModelParam(), "GRM", "GRM", "GRM model to serialize.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Path",
            "P",
            "Destination .grm or JSON path. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter("Write", "W", "Connect a momentary Grasshopper Button and press it once to write.", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("JSON", "J", "Deterministic GRM JSON.", GH_ParamAccess.item);
        pManager.AddTextParameter("Full Path", "P", "Resolved destination path.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Written", "OK", "True when the file was written during this solution.", GH_ParamAccess.item);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitModelGoo? modelGoo = null;
        string path = string.Empty;
        bool write = false;
        if (!DA.GetData(0, ref modelGoo)
            || !DA.GetData(1, ref path)
            || !DA.GetData(2, ref write)
            || modelGoo?.Value is null)
        {
            return;
        }

        string fullPath = ResolveDocumentPath(path, Path.GetTempPath());
        string json = GrmWriter.Serialize(modelGoo.Value);
        if (write)
        {
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            GrmWriter.WriteFile(fullPath, modelGoo.Value);
        }

        DA.SetData(0, json);
        DA.SetData(1, fullPath);
        DA.SetData(2, write);
    }
}
