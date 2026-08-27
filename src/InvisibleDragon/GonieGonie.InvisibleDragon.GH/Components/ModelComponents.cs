using Grasshopper.Kernel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;
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
        int sources = pManager.AddParameter(
            new DragonSourceSystemParam(),
            "Sources",
            "Src",
            "Optional source registry. Referenced sources are persisted through their supply systems.",
            GH_ParamAccess.list);
        pManager[sources].Optional = true;
        int supplies = pManager.AddParameter(
            new DragonSupplySystemParam(),
            "Supply Systems",
            "Sys",
            "Optional supply systems assigned using Supply Zone Indices. Omit when using HVAC Assignments.",
            GH_ParamAccess.list);
        pManager[supplies].Optional = true;
        int supplyZoneIndices = pManager.AddIntegerParameter(
            "Supply Zone Indices",
            "Zi",
            "Optional zero-based zone index per Supply System; one index broadcasts. Empty auto-maps only unambiguous cases.",
            GH_ParamAccess.list);
        pManager[supplyZoneIndices].Optional = true;
        int assignments = pManager.AddGenericParameter(
            "HVAC Assignments",
            "HVAC",
            "Optional ZoneHvacAssignment values from Supply Group Assignment components.",
            GH_ParamAccess.list);
        pManager[assignments].Optional = true;
        int ventilators = pManager.AddParameter(
            new DragonEnergyRecoveryVentilatorParam(),
            "Ventilators",
            "ERV",
            "Optional energy recovery ventilators assigned using Ventilator Zone Indices.",
            GH_ParamAccess.list);
        pManager[ventilators].Optional = true;
        int ventilatorZoneIndices = pManager.AddIntegerParameter(
            "Ventilator Zone Indices",
            "VZi",
            "Optional zero-based zone index per Ventilator; one index broadcasts. A single ERV with no indices broadcasts to all zones.",
            GH_ParamAccess.list);
        pManager[ventilatorZoneIndices].Optional = true;
        int photovoltaicPanels = pManager.AddParameter(
            new DragonPhotovoltaicPanelParam(),
            "PV Panels",
            "PV",
            "Optional photovoltaic panels included in the model.",
            GH_ParamAccess.list);
        pManager[photovoltaicPanels].Optional = true;
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

        var sourceGoos = new List<DragonSourceSystemGoo>();
        var supplyGoos = new List<DragonSupplySystemGoo>();
        var supplyZoneIndices = new List<int>();
        var assignmentObjects = new List<object>();
        var ventilatorGoos = new List<DragonEnergyRecoveryVentilatorGoo>();
        var ventilatorZoneIndices = new List<int>();
        var photovoltaicGoos = new List<DragonPhotovoltaicPanelGoo>();
        DA.GetDataList(4, sourceGoos);
        DA.GetDataList(5, supplyGoos);
        DA.GetDataList(6, supplyZoneIndices);
        DA.GetDataList(7, assignmentObjects);
        DA.GetDataList(8, ventilatorGoos);
        DA.GetDataList(9, ventilatorZoneIndices);
        DA.GetDataList(10, photovoltaicGoos);

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
        SourceSystem[] explicitSources = sourceGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException($"Sources contains an empty value at index {index}."))
            .ToArray();
        ValidateSourceRegistry(explicitSources);
        SupplySystem[] directSupplies = supplyGoos
            .Select((goo, index) => HvacComponentSupport.Supply(goo, "Supply Systems", index))
            .ToArray();
        int[] directSupplyIndices = HvacComponentSupport.AssignmentIndices(
            supplyZoneIndices,
            directSupplies.Length,
            zones.Length,
            "Supply Zone Indices",
            broadcastSingleItemToAllZones: false);
        var hvacAssignments = assignmentObjects
            .Select((value, index) => HvacComponentSupport.RequireObject<ZoneHvacAssignment>(
                value,
                $"HVAC Assignments[{index}]"))
            .ToList();
        hvacAssignments.AddRange(directSupplies
            .Select((system, index) => new { System = system, ZoneIndex = directSupplyIndices[index] })
            .GroupBy(item => item.ZoneIndex)
            .Select(group => new ZoneHvacAssignment(
                zones[group.Key].Id,
                new SupplyGroup(group.Select(item => item.System)))));

        EnergyRecoveryVentilator[] ventilators = ventilatorGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException($"Ventilators contains an empty value at index {index}."))
            .ToArray();
        int[] ventilationIndices = HvacComponentSupport.AssignmentIndices(
            ventilatorZoneIndices,
            ventilators.Length,
            zones.Length,
            "Ventilator Zone Indices",
            broadcastSingleItemToAllZones: true);
        ZoneVentilationAssignment[] ventilationAssignments = ventilationIndices
            .Select((zoneIndex, index) => new ZoneVentilationAssignment(
                zones[zoneIndex].Id,
                ventilators.Length == 1 ? ventilators[0] : ventilators[index]))
            .ToArray();
        PhotovoltaicPanel[] photovoltaicPanels = photovoltaicGoos.Select((goo, index) => goo?.Value
            ?? throw new ArgumentException($"PV Panels contains an empty value at index {index}."))
            .ToArray();

        var componentDiagnostics = SourceRegistryDiagnostics(explicitSources, hvacAssignments);
        var model = new EnergyModel(
            name,
            zones,
            hvacAssignments: hvacAssignments,
            ventilationAssignments: ventilationAssignments,
            photovoltaicPanels: photovoltaicPanels,
            northAxisDegrees: northAxis,
            terrain: terrain);
        ValidationResult validation = model.Validate();
        Diagnostic[] diagnostics = componentDiagnostics.Concat(validation.Diagnostics).ToArray();
        Report(diagnostics);
        DA.SetData(0, new DragonEnergyModelGoo(model));
        DA.SetData(1, diagnostics.All(item => !item.IsFailure));
        DA.SetDataList(2, diagnostics.Select(item => new DiagnosticGoo(item)));
    }

    private static void ValidateSourceRegistry(IReadOnlyList<SourceSystem> sources)
    {
        foreach (IGrouping<EntityId, SourceSystem> group in sources.GroupBy(source => source.Id))
        {
            SourceSystem first = group.First();
            if (group.Skip(1).Any(source => !HvacComponentSupport.SourceDefinitionsEqual(first, source)))
            {
                throw new ArgumentException(
                    $"Sources contains conflicting definitions for identifier '{group.Key}'.");
            }
        }
    }

    private static List<Diagnostic> SourceRegistryDiagnostics(
        IReadOnlyList<SourceSystem> explicitSources,
        IReadOnlyList<ZoneHvacAssignment> assignments)
    {
        SourceSystem[] referencedSources = assignments
            .SelectMany(assignment => assignment.Supply.Systems)
            .SelectMany(system => RelatedSources(system.Source))
            .ToArray();
        var diagnostics = new List<Diagnostic>();
        foreach (SourceSystem source in explicitSources.GroupBy(item => item.Id).Select(group => group.First()))
        {
            SourceSystem[] matches = referencedSources.Where(item => item.Id.Equals(source.Id)).ToArray();
            if (matches.Any(match => !HvacComponentSupport.SourceDefinitionsEqual(source, match)))
            {
                throw new ArgumentException(
                    $"Source registry definition '{source.Name}' conflicts with a supply-system source using ID '{source.Id}'.");
            }

            if (matches.Length == 0)
            {
                diagnostics.Add(new Diagnostic(
                    "INVISIBLEDRAGON.GH.UNREFERENCED_SOURCE",
                    DiagnosticSeverity.Warning,
                    $"Source '{source.Name}' is not referenced by an HVAC assignment and is not stored in EnergyModel.",
                    source.Id,
                    suggestedAction: "Connect the source to a Supply System and assign that system to a zone."));
            }
        }

        return diagnostics;
    }

    private static IEnumerable<SourceSystem> RelatedSources(SourceSystem? source)
    {
        if (source is null)
        {
            yield break;
        }

        yield return source;
        if (source is AbsorptionChiller absorptionChiller)
        {
            yield return absorptionChiller.HeatSource;
        }
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
            "Optional Energy+.idd file or EnergyPlus root. Relative paths use the saved Grasshopper document. Empty uses configured/default EnergyPlus 24.2.",
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

        string? resolvedIddPath = string.IsNullOrWhiteSpace(iddPath)
            ? null
            : ResolveDocumentPath(iddPath);
        Idd.IddSchema? schema = IddSchemaProvider.Resolve(resolvedIddPath);
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
            "Optional Energy+.idd file or EnergyPlus root. Relative paths use the saved Grasshopper document. Empty uses configured/default EnergyPlus 24.2.",
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

        string? resolvedIddPath = string.IsNullOrWhiteSpace(iddPath)
            ? null
            : ResolveDocumentPath(iddPath);
        Idd.IddSchema? schema = IddSchemaProvider.Resolve(resolvedIddPath);
        IdfDocument document = schema is null
            ? idfGoo.Value
            : IdfParser.Parse(IdfWriter.Write(idfGoo.Value), schema);
        ValidationResult validation = IdfValidator.Validate(document);
        Report(validation.Diagnostics);
        DA.SetData(0, validation.IsValid);
        DA.SetDataList(1, validation.Diagnostics.Select(item => new DiagnosticGoo(item)));
    }
}
