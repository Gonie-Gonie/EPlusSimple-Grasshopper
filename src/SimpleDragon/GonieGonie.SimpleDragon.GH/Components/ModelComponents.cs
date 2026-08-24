using System.Globalization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

public sealed class AssembleGreenRetrofitModelComponent : SimpleDragonComponent
{
    public AssembleGreenRetrofitModelComponent()
        : base(
            "Assemble SimpleDragon GRM",
            "Assemble GRM",
            "Assembles extracted zones and referenced resources into a GRM 0.7 model. North Axis is applied here only; extracted azimuths remain world-north values.",
            SimpleDragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("f0a131e0-7cfe-45fc-945a-7e52237535ee");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Building/model name.", GH_ParamAccess.item, "SimpleDragon Model");
        pManager.AddParameter(new SimpleDragonZoneParam(), "Zones", "Z", "SimpleDragon zones.", GH_ParamAccess.list);
        pManager.AddNumberParameter(
            "North Axis",
            "North",
            "Clockwise building north-axis rotation in degrees. Do not pre-apply this to extracted azimuths.",
            GH_ParamAccess.item,
            0);
        pManager.AddTextParameter("Address", "A", "Korean address used to resolve weather and climate metadata.", GH_ParamAccess.item, "서울특별시 종로구");
        pManager.AddTextParameter("Vintage", "V", "Building vintage as yyyy-MM-dd.", GH_ParamAccess.item, "2020-01-01");
        pManager.AddBooleanParameter("Multifamily Housing", "MF", "True for multifamily housing.", GH_ParamAccess.item, false);
        pManager.AddParameter(new SimpleDragonMaterialParam(), "Materials", "M", "Additional model materials.", GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonSurfaceConstructionParam(),
            "Surface Constructions",
            "SC",
            "Additional surface constructions.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonFenestrationConstructionParam(),
            "Fenestration Constructions",
            "FC",
            "Additional fenestration constructions.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonSourceSystemParam(),
            "Source Systems",
            "Sources",
            "Optional explicit HVAC source-system catalog. Sources nested in supplied/assigned supply systems are also included.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonSupplySystemParam(),
            "Supply Systems",
            "Supplies",
            "Optional explicit HVAC supply-system catalog. Zone-assigned supply systems are also included.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonEnergyRecoveryVentilatorParam(),
            "ERV Systems",
            "ERVs",
            "Optional explicit ventilation/ERV catalog. Zone-assigned ERVs are also included.",
            GH_ParamAccess.list);
        pManager.AddParameter(
            new SimpleDragonPhotovoltaicPanelParam(),
            "Photovoltaic Panels",
            "PV",
            "Optional photovoltaic panels included in the assembled GRM.",
            GH_ParamAccess.list);
        pManager[6].Optional = true;
        pManager[7].Optional = true;
        pManager[8].Optional = true;
        pManager[9].Optional = true;
        pManager[10].Optional = true;
        pManager[11].Optional = true;
        pManager[12].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitModelParam(), "GRM", "GRM", "Assembled GRM 0.7 model.", GH_ParamAccess.item);
        pManager.AddTextParameter("JSON", "J", "Deterministic GRM 0.7 JSON.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Floor Area", "A", "Total model floor area in m\u00B2.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Weather and assembly diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "SimpleDragon Model";
        var zoneGoos = new List<SimpleDragonZoneGoo>();
        double northAxis = 0;
        string address = "서울특별시 종로구";
        string vintageText = "2020-01-01";
        bool multifamily = false;
        var materialGoos = new List<SimpleDragonMaterialGoo>();
        var surfaceConstructionGoos = new List<SimpleDragonSurfaceConstructionGoo>();
        var fenestrationConstructionGoos = new List<SimpleDragonFenestrationConstructionGoo>();
        var sourceGoos = new List<SimpleDragonSourceSystemGoo>();
        var supplyGoos = new List<SimpleDragonSupplySystemGoo>();
        var ventilatorGoos = new List<SimpleDragonEnergyRecoveryVentilatorGoo>();
        var photovoltaicGoos = new List<SimpleDragonPhotovoltaicPanelGoo>();
        if (!DA.GetData(0, ref name)
            || !DA.GetDataList(1, zoneGoos)
            || !DA.GetData(2, ref northAxis)
            || !DA.GetData(3, ref address)
            || !DA.GetData(4, ref vintageText)
            || !DA.GetData(5, ref multifamily))
        {
            return;
        }

        DA.GetDataList(6, materialGoos);
        DA.GetDataList(7, surfaceConstructionGoos);
        DA.GetDataList(8, fenestrationConstructionGoos);
        DA.GetDataList(9, sourceGoos);
        DA.GetDataList(10, supplyGoos);
        DA.GetDataList(11, ventilatorGoos);
        DA.GetDataList(12, photovoltaicGoos);
        if (!DateTime.TryParseExact(
                vintageText.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime vintage))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Vintage must use yyyy-MM-dd.");
            return;
        }

        Zone[] zones = Values<SimpleDragonZoneGoo, Zone>(zoneGoos, "Zone");
        Material[] suppliedMaterials = Values<SimpleDragonMaterialGoo, Material>(materialGoos, "Material");
        SurfaceConstruction[] suppliedSurfaceConstructions = Values<SimpleDragonSurfaceConstructionGoo, SurfaceConstruction>(
            surfaceConstructionGoos,
            "Surface Construction");
        FenestrationConstruction[] suppliedFenestrationConstructions = Values<
            SimpleDragonFenestrationConstructionGoo,
            FenestrationConstruction>(
            fenestrationConstructionGoos,
            "Fenestration Construction");
        SourceSystem[] suppliedSources = Values<SimpleDragonSourceSystemGoo, SourceSystem>(
            sourceGoos,
            "Source System");
        SupplySystem[] suppliedSupplies = Values<SimpleDragonSupplySystemGoo, SupplySystem>(
            supplyGoos,
            "Supply System");
        VentilationSystem[] suppliedVentilators = Values<
            SimpleDragonEnergyRecoveryVentilatorGoo,
            VentilationSystem>(
            ventilatorGoos,
            "ERV System");
        PhotovoltaicSystem[] photovoltaicSystems = DistinctById(Values<
            SimpleDragonPhotovoltaicPanelGoo,
            PhotovoltaicSystem>(
            photovoltaicGoos,
            "Photovoltaic Panel"));
        SurfaceConstruction[] surfaceConstructions = DistinctById(
            suppliedSurfaceConstructions.Concat(
                zones.SelectMany(zone => zone.Surfaces)
                    .Select(surface => surface.Construction)
                    .OfType<SurfaceConstruction>()));
        Material[] materials = DistinctById(
            suppliedMaterials.Concat(surfaceConstructions.SelectMany(item => item.Layers).Select(item => item.Material)));
        FenestrationConstruction[] fenestrationConstructions = DistinctById(
            suppliedFenestrationConstructions.Concat(
                zones.SelectMany(zone => zone.Surfaces)
                    .SelectMany(surface => surface.Fenestrations)
                    .Select(item => item.Construction)
                    .OfType<FenestrationConstruction>()));
        SupplySystem[] supplySystems = DistinctById(
            suppliedSupplies.Concat(zones.SelectMany(zone => zone.SupplySystems)));
        SourceSystem[] sourceSystems = DistinctById(
            suppliedSources.Concat(
                supplySystems.Select(item => item.SourceSystem).OfType<SourceSystem>()));
        VentilationSystem[] ventilationSystems = DistinctById(
            suppliedVentilators.Concat(
                zones.SelectMany(zone => zone.VentilationAssignments)
                    .Select(item => item.VentilationSystem)
                    .OfType<VentilationSystem>()));
        BuildingFloor[] floors = zones
            .GroupBy(zone => zone.FloorNumber)
            .OrderBy(group => group.Key)
            .Select(group => new BuildingFloor(group.Key, group))
            .ToArray();
        LookupResult<WeatherSelection> weather = SimpleDragonDatabase.Default.Weather.FindByAddress(address, vintage);
        var model = new GreenRetrofitModel(
            name,
            northAxis,
            address,
            vintage,
            multifamily,
            floors,
            materials,
            surfaceConstructions,
            fenestrationConstructions,
            sourceSystems,
            supplySystems,
            ventilationSystems,
            photovoltaicSystems,
            weather: weather.Value);
        Report(weather.Diagnostics);
        DA.SetData(0, new GreenRetrofitModelGoo(model));
        DA.SetData(1, GrmWriter.Serialize(model));
        DA.SetData(2, model.Area);
        DA.SetDataList(3, weather.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }

    private static T[] Values<TGoo, T>(IEnumerable<TGoo> goos, string description)
        where TGoo : SimpleDragonGoo<T>
        where T : class
    {
        return goos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException(description + " at index " + index + " is empty."))
            .ToArray();
    }

    private static T[] DistinctById<T>(IEnumerable<T> values)
        where T : class
    {
        return values
            .GroupBy(value => Id(value), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static string Id<T>(T value)
        where T : class
    {
        return value switch
        {
            Material item => item.Id.Value,
            SurfaceConstruction item => item.Id.Value,
            FenestrationConstruction item => item.Id.Value,
            SourceSystem item => item.Id.Value,
            SupplySystem item => item.Id.Value,
            VentilationSystem item => item.Id.Value,
            PhotovoltaicSystem item => item.Id.Value,
            _ => throw new NotSupportedException("Cannot read ID from " + value.GetType().FullName + "."),
        };
    }
}

public sealed class ReadGreenRetrofitModelComponent : SimpleDragonComponent
{
    public ReadGreenRetrofitModelComponent()
        : base(
            "Read SimpleDragon GRM",
            "Read GRM",
            "Reads a UTF-8 GRM 0.7 file and reports reference-resolution diagnostics.",
            SimpleDragonPanels.Model)
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
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "GRM read diagnostics.", GH_ParamAccess.list);
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
        DA.SetDataList(4, read.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class WriteGreenRetrofitModelComponent : SimpleDragonComponent
{
    public WriteGreenRetrofitModelComponent()
        : base(
            "Write SimpleDragon GRM",
            "Write GRM",
            "Writes deterministic UTF-8 GRM 0.7 JSON when Write is true.",
            SimpleDragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("5d3c5ff1-03e3-4b2e-85a5-43b36f856d92");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitModelParam(), "GRM", "GRM", "GRM model to serialize.", GH_ParamAccess.item);
        pManager.AddTextParameter("Path", "P", "Destination .grm or JSON path.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Write", "W", "Explicit write trigger.", GH_ParamAccess.item, false);
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

        string fullPath = ResolveDocumentPath(path);
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

public sealed class ConvertGreenRetrofitModelComponent : SimpleDragonComponent
{
    public ConvertGreenRetrofitModelComponent()
        : base(
            "Convert SimpleDragon GRM",
            "GRM to IDF",
            "Converts a GRM into an InvisibleDragon energy model and deterministic EnergyPlus IDF.",
            SimpleDragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("b38f2e41-f63b-42a8-b549-65cd60c7a994");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new GreenRetrofitModelParam(), "GRM", "GRM", "SimpleDragon model to convert.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "IDD Path",
            "IDD",
            "Optional Energy+.idd path or EnergyPlus root. Empty uses configured/default EnergyPlus 24.2 when available.",
            GH_ParamAccess.item,
            string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonEnergyModelParam(), "Energy Model", "M", "Converted InvisibleDragon model.", GH_ParamAccess.item);
        pManager.AddParameter(new DragonIdfParam(), "IDF", "IDF", "Compiled EnergyPlus IDF document.", GH_ParamAccess.item);
        pManager.AddTextParameter("IDF Text", "T", "Deterministic IDF text.", GH_ParamAccess.item);
        pManager.AddTextParameter("EPW File", "EPW", "Resolved weather EPW filename, when available.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Success", "OK", "True when conversion and validation have no errors.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Conversion and IDF diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        GreenRetrofitModelGoo? modelGoo = null;
        string iddPath = string.Empty;
        if (!DA.GetData(0, ref modelGoo) || modelGoo?.Value is null)
        {
            return;
        }

        DA.GetData(1, ref iddPath);
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(modelGoo.Value);
        var diagnostics = conversion.Diagnostics.ToList();
        IddSchema? schema = ResolveIdd(iddPath);
        if (schema is null)
        {
            diagnostics.Add(new Diagnostic(
                "SD.GH.IDD_NOT_RESOLVED",
                DiagnosticSeverity.Warning,
                "Energy+.idd was not resolved; IDF compilation continues without schema validation.",
                suggestedAction: "Supply IDD Path or configure EnergyPlus 24.2."));
        }

        if (conversion.EnergyModel is not null)
        {
            IdfDocument document = conversion.EnergyModel.ToIdfDocument(
                schema,
                new EnergyModelIdfOptions { ThrowOnValidationErrors = false });
            if (schema is not null)
            {
                diagnostics.AddRange(IdfValidator.Validate(document).Diagnostics);
            }

            DA.SetData(0, new DragonEnergyModelGoo(conversion.EnergyModel));
            DA.SetData(1, new DragonIdfGoo(document));
            DA.SetData(2, IdfWriter.Write(document));
        }

        Report(diagnostics);
        DA.SetData(3, conversion.Weather?.EpwFileName ?? string.Empty);
        DA.SetData(4, conversion.EnergyModel is not null && diagnostics.All(item => !item.IsFailure));
        DA.SetDataList(5, diagnostics.Select(item => new DiagnosticGoo(item)));
    }

    private static IddSchema? ResolveIdd(string suppliedPath)
    {
        string? path = ResolveIddPath(suppliedPath);
        return path is null ? null : IddParser.ParseFile(path);
    }

    private static string? ResolveIddPath(string suppliedPath)
    {
        if (!string.IsNullOrWhiteSpace(suppliedPath))
        {
            string full = Path.GetFullPath(suppliedPath.Trim());
            return Directory.Exists(full) ? Path.Combine(full, "Energy+.idd") : full;
        }

        foreach (string variable in new[]
        {
            "GONIEGONIE_ENERGYPLUS_ROOT",
            "ENERGYPLUS_24_2_ROOT",
            "ENERGYPLUS_ROOT",
        })
        {
            string? root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root))
            {
                string candidate = Path.Combine(root, "Energy+.idd");
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        string conventional = Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
            "EnergyPlusV24-2-0",
            "Energy+.idd");
        return File.Exists(conventional) ? conventional : null;
    }
}
