using Grasshopper.Kernel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

public sealed class ZoneComponent : DragonComponent
{
    public ZoneComponent()
        : base(
            "Thermal Zone",
            "Zone",
            "Creates an InvisibleDragon thermal zone from polygon surfaces and a profile.",
            DragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("e5627899-dcdb-4154-98fc-f7c547d50d2e");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Zone name.", GH_ParamAccess.item, "Zone");
        pManager.AddParameter(new DragonSurfaceParam(), "Surfaces", "S", "Closed boundary surfaces.", GH_ParamAccess.list);
        pManager.AddParameter(new DragonProfileParam(), "Profile", "P", "Zone usage profile.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Infiltration", "ACH", "Infiltration in air changes per hour.", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Lighting Power Density", "LPD", "Lighting power density in W/m².", GH_ParamAccess.item, 0);
        pManager.AddNumberParameter("Outdoor Air Flow", "OA", "Outdoor air flow in m³/s.", GH_ParamAccess.item, 0);
        pManager.AddTextParameter("ID", "ID", "Optional stable zone identifier.", GH_ParamAccess.item, string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonZoneParam(), "Zone", "Z", "InvisibleDragon thermal zone.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Valid", "V", "True when the zone has no error diagnostics.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Zone validation diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "Zone";
        var surfaceGoos = new List<DragonSurfaceGoo>();
        DragonProfileGoo? profileGoo = null;
        double infiltration = 0;
        double lightingPowerDensity = 0;
        double outdoorAirFlow = 0;
        string id = string.Empty;
        if (!DA.GetData(0, ref name) ||
            !DA.GetDataList(1, surfaceGoos) ||
            !DA.GetData(2, ref profileGoo) ||
            !DA.GetData(3, ref infiltration) ||
            !DA.GetData(4, ref lightingPowerDensity) ||
            !DA.GetData(5, ref outdoorAirFlow))
        {
            return;
        }

        DA.GetData(6, ref id);
        if (profileGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Profile is required.");
            return;
        }

        Surface[] surfaces = surfaceGoos.Select((goo, index) =>
        {
            if (goo?.Value is null)
            {
                throw new ArgumentException($"Surface at index {index} is empty.");
            }

            return goo.Value;
        }).ToArray();
        var zone = new Zone(
            StableIds.Resolve(id, "zone", name, string.Join("|", surfaces.Select(item => item.Id.Value))),
            name,
            surfaces,
            profileGoo.Value,
            infiltration,
            lightingPowerDensity,
            outdoorAirFlow);
        ValidationResult validation = zone.Validate();
        Report(validation.Diagnostics);
        DA.SetData(0, new DragonZoneGoo(zone));
        DA.SetData(1, validation.IsValid);
        DA.SetDataList(2, validation.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class EnergyModelComponent : DragonComponent
{
    public EnergyModelComponent()
        : base(
            "Energy Model",
            "Model",
            "Assembles thermal zones into a complete InvisibleDragon energy model.",
            DragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("fee2629c-94d8-4eed-8be2-14ba108ce825");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Model name.", GH_ParamAccess.item, "InvisibleDragon Model");
        pManager.AddParameter(new DragonZoneParam(), "Zones", "Z", "Thermal zones.", GH_ParamAccess.list);
        pManager.AddNumberParameter("North Axis", "N°", "North-axis rotation in degrees.", GH_ParamAccess.item, 0);
        pManager.AddTextParameter("Terrain", "T", "Country, Suburbs, City, Ocean, or Urban.", GH_ParamAccess.item, "Suburbs");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonEnergyModelParam(), "Model", "M", "InvisibleDragon energy model.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Valid", "V", "True when the model has no error diagnostics.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Model validation diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        string name = "InvisibleDragon Model";
        var zoneGoos = new List<DragonZoneGoo>();
        double northAxis = 0;
        string terrainText = "Suburbs";
        if (!DA.GetData(0, ref name) ||
            !DA.GetDataList(1, zoneGoos) ||
            !DA.GetData(2, ref northAxis) ||
            !DA.GetData(3, ref terrainText))
        {
            return;
        }

        if (!Enum.TryParse(terrainText.Trim(), true, out Terrain terrain))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown terrain '{terrainText}'.");
            return;
        }

        Zone[] zones = zoneGoos.Select((goo, index) =>
        {
            if (goo?.Value is null)
            {
                throw new ArgumentException($"Zone at index {index} is empty.");
            }

            return goo.Value;
        }).ToArray();
        var model = new EnergyModel(name, zones, northAxisDegrees: northAxis, terrain: terrain);
        ValidationResult validation = model.Validate();
        Report(validation.Diagnostics);
        DA.SetData(0, new DragonEnergyModelGoo(model));
        DA.SetData(1, validation.IsValid);
        DA.SetDataList(2, validation.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class CompileIdfComponent : DragonComponent
{
    public CompileIdfComponent()
        : base(
            "Compile IDF",
            "IDF",
            "Compiles an InvisibleDragon model into deterministic EnergyPlus IDF text.",
            DragonPanels.Model)
    {
    }

    public override Guid ComponentGuid => new("2743be88-ef3a-4f0d-abf8-cf062d93aafe");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new DragonEnergyModelParam(), "Model", "M", "InvisibleDragon energy model.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "IDD Path",
            "IDD",
            "Optional Energy+.idd file or EnergyPlus root. Empty uses configured/default EnergyPlus 24.2.",
            GH_ParamAccess.item,
            string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new DragonIdfParam(), "IDF", "IDF", "Compiled IDF document.", GH_ParamAccess.item);
        pManager.AddTextParameter("Text", "T", "Deterministic IDF text.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Valid", "V", "True when model and IDF validation pass.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Compilation diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        DragonEnergyModelGoo? modelGoo = null;
        string iddPath = string.Empty;
        if (!DA.GetData(0, ref modelGoo))
        {
            return;
        }

        DA.GetData(1, ref iddPath);
        if (modelGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Model is required.");
            return;
        }

        Idd.IddSchema? schema = IddSchemaProvider.Resolve(iddPath);
        IdfDocument document = modelGoo.Value.ToIdfDocument(
            schema,
            new EnergyModelIdfOptions { ThrowOnValidationErrors = false });
        var diagnostics = modelGoo.Value.Validate().Diagnostics.ToList();
        if (schema is null)
        {
            diagnostics.Add(new Diagnostic(
                "INVISIBLEDRAGON.GH.IDD_NOT_RESOLVED",
                DiagnosticSeverity.Warning,
                "Energy+.idd was not resolved; the IDF was compiled without schema validation.",
                suggestedAction: "Supply IDD Path or configure EnergyPlus 24.2."));
        }
        else
        {
            diagnostics.AddRange(IdfValidator.Validate(document).Diagnostics);
        }

        bool valid = !diagnostics.Any(item => item.IsFailure);
        Report(diagnostics);
        DA.SetData(0, new DragonIdfGoo(document));
        DA.SetData(1, IdfWriter.Write(document));
        DA.SetData(2, valid);
        DA.SetDataList(3, diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}

public sealed class ValidateIdfComponent : DragonComponent
{
    public ValidateIdfComponent()
        : base(
            "Validate IDF",
            "Valid",
            "Validates an IDF document against the EnergyPlus 24.2 IDD.",
            DragonPanels.Core)
    {
    }

    public override Guid ComponentGuid => new("fa664eeb-5503-4366-831d-e3478c8a1832");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new DragonIdfParam(), "IDF", "IDF", "IDF document to validate.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "IDD Path",
            "IDD",
            "Optional Energy+.idd file or EnergyPlus root. Empty uses configured/default EnergyPlus 24.2.",
            GH_ParamAccess.item,
            string.Empty);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddBooleanParameter("Valid", "V", "True when no IDF error is present.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "IDD-backed validation diagnostics.", GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        DragonIdfGoo? idfGoo = null;
        string iddPath = string.Empty;
        if (!DA.GetData(0, ref idfGoo))
        {
            return;
        }

        DA.GetData(1, ref iddPath);
        if (idfGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "IDF is required.");
            return;
        }

        Idd.IddSchema? schema = IddSchemaProvider.Resolve(iddPath);
        IdfDocument document = schema is null
            ? idfGoo.Value
            : IdfParser.Parse(IdfWriter.Write(idfGoo.Value), schema);
        ValidationResult validation = IdfValidator.Validate(document);
        Report(validation.Diagnostics);
        DA.SetData(0, validation.IsValid);
        DA.SetDataList(1, validation.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}
