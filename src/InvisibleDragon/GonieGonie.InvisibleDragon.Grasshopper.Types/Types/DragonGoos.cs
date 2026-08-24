using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.InvisibleDragon.Shape;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Grasshopper.Types;

public sealed class DragonMaterialGoo : DragonGoo<Material>
{
    public DragonMaterialGoo() { }
    public DragonMaterialGoo(Material value) : base(value) { }
    public override string TypeName => "InvisibleDragon Material";
    public override string TypeDescription => "An immutable opaque material definition.";
    protected override DragonGoo<Material> Create(Material value) => new DragonMaterialGoo(value);
    protected override DragonGoo<Material> CreateEmpty() => new DragonMaterialGoo();
    protected override string DisplayText(Material value) => $"Material {value.Name}";
}

public sealed class DragonConstructionGoo : DragonGoo<ISurfaceConstruction>
{
    public DragonConstructionGoo() { }
    public DragonConstructionGoo(ISurfaceConstruction value) : base(value) { }
    public override string TypeName => "InvisibleDragon Construction";
    public override string TypeDescription => "An opaque or air-boundary surface construction.";
    protected override DragonGoo<ISurfaceConstruction> Create(ISurfaceConstruction value) => new DragonConstructionGoo(value);
    protected override DragonGoo<ISurfaceConstruction> CreateEmpty() => new DragonConstructionGoo();
    protected override string DisplayText(ISurfaceConstruction value) => $"Construction {value.Name}";
}

public sealed class DragonScheduleGoo : DragonGoo<Schedule>
{
    public DragonScheduleGoo() { }
    public DragonScheduleGoo(Schedule value) : base(value) { }
    public override string TypeName => "InvisibleDragon Schedule";
    public override string TypeDescription => "A non-leap-year InvisibleDragon schedule.";
    protected override DragonGoo<Schedule> Create(Schedule value) => new DragonScheduleGoo(value);
    protected override DragonGoo<Schedule> CreateEmpty() => new DragonScheduleGoo();
    protected override string DisplayText(Schedule value) => $"Schedule {value.Name} ({value.Type})";
}

public sealed class DragonProfileGoo : DragonGoo<ZoneProfile>
{
    public DragonProfileGoo() { }
    public DragonProfileGoo(ZoneProfile value) : base(value) { }
    public override string TypeName => "InvisibleDragon Profile";
    public override string TypeDescription => "A collection of annual schedules used by a thermal zone.";
    protected override DragonGoo<ZoneProfile> Create(ZoneProfile value) => new DragonProfileGoo(value);
    protected override DragonGoo<ZoneProfile> CreateEmpty() => new DragonProfileGoo();
    protected override string DisplayText(ZoneProfile value) => $"Profile {value.Name}";
    protected override string? InvalidReason(ZoneProfile value) => FailureText(value.Validate());

    private static string? FailureText(ValidationResult validation) => validation.IsValid
        ? null
        : string.Join(" ", validation.Diagnostics.Where(item => item.IsFailure).Select(item => item.Message));
}

public sealed class DragonSurfaceGoo : DragonGoo<Surface>
{
    public DragonSurfaceGoo() { }
    public DragonSurfaceGoo(Surface value) : base(value) { }
    public override string TypeName => "InvisibleDragon Surface";
    public override string TypeDescription => "A vertex-preserving building surface.";
    protected override DragonGoo<Surface> Create(Surface value) => new DragonSurfaceGoo(value);
    protected override DragonGoo<Surface> CreateEmpty() => new DragonSurfaceGoo();
    protected override string DisplayText(Surface value) => $"{value.Type} {value.Name} ({value.GrossArea:0.###} m²)";
    protected override string? InvalidReason(Surface value) => FailureText(value.Validate());

    private static string? FailureText(ValidationResult validation) => validation.IsValid
        ? null
        : string.Join(" ", validation.Diagnostics.Where(item => item.IsFailure).Select(item => item.Message));
}

public sealed class DragonZoneGoo : DragonGoo<Zone>
{
    public DragonZoneGoo() { }
    public DragonZoneGoo(Zone value) : base(value) { }
    public override string TypeName => "InvisibleDragon Zone";
    public override string TypeDescription => "A thermal zone with explicit polygon surfaces.";
    protected override DragonGoo<Zone> Create(Zone value) => new DragonZoneGoo(value);
    protected override DragonGoo<Zone> CreateEmpty() => new DragonZoneGoo();
    protected override string DisplayText(Zone value) => $"Zone {value.Name} ({value.Surfaces.Count} surfaces)";
    protected override string? InvalidReason(Zone value) => FailureText(value.Validate());

    private static string? FailureText(ValidationResult validation) => validation.IsValid
        ? null
        : string.Join(" ", validation.Diagnostics.Where(item => item.IsFailure).Select(item => item.Message));
}

public sealed class DragonEnergyModelGoo : DragonGoo<EnergyModel>
{
    public DragonEnergyModelGoo() { }
    public DragonEnergyModelGoo(EnergyModel value) : base(value) { }
    public override string TypeName => "InvisibleDragon Energy Model";
    public override string TypeDescription => "A complete Rhino-independent EnergyPlus input model.";
    protected override DragonGoo<EnergyModel> Create(EnergyModel value) => new DragonEnergyModelGoo(value);
    protected override DragonGoo<EnergyModel> CreateEmpty() => new DragonEnergyModelGoo();
    protected override string DisplayText(EnergyModel value) => $"Energy Model {value.Name} ({value.Zones.Count} zones)";
    protected override string? InvalidReason(EnergyModel value) => FailureText(value.Validate());

    private static string? FailureText(ValidationResult validation) => validation.IsValid
        ? null
        : string.Join(" ", validation.Diagnostics.Where(item => item.IsFailure).Select(item => item.Message));
}

public sealed class DragonSourceSystemGoo : DragonGoo<SourceSystem>
{
    public DragonSourceSystemGoo() { }
    public DragonSourceSystemGoo(SourceSystem value) : base(value) { }
    public override string TypeName => "InvisibleDragon Source System";
    public override string TypeDescription => "An InvisibleDragon HVAC source-system definition.";
    protected override DragonGoo<SourceSystem> Create(SourceSystem value) => new DragonSourceSystemGoo(value);
    protected override DragonGoo<SourceSystem> CreateEmpty() => new DragonSourceSystemGoo();
    protected override string DisplayText(SourceSystem value) => $"Source System {value.Name} ({value.GetType().Name})";
}

public sealed class DragonSupplySystemGoo : DragonGoo<SupplySystem>
{
    public DragonSupplySystemGoo() { }
    public DragonSupplySystemGoo(SupplySystem value) : base(value) { }
    public override string TypeName => "InvisibleDragon Supply System";
    public override string TypeDescription => "An InvisibleDragon zone HVAC supply-system definition.";
    protected override DragonGoo<SupplySystem> Create(SupplySystem value) => new DragonSupplySystemGoo(value);
    protected override DragonGoo<SupplySystem> CreateEmpty() => new DragonSupplySystemGoo();
    protected override string DisplayText(SupplySystem value) => $"Supply System {value.Name} ({value.GetType().Name})";
}

public sealed class DragonEnergyRecoveryVentilatorGoo : DragonGoo<EnergyRecoveryVentilator>
{
    public DragonEnergyRecoveryVentilatorGoo() { }
    public DragonEnergyRecoveryVentilatorGoo(EnergyRecoveryVentilator value) : base(value) { }
    public override string TypeName => "InvisibleDragon Energy Recovery Ventilator";
    public override string TypeDescription => "An InvisibleDragon sensible and latent heat-recovery ventilator.";
    protected override DragonGoo<EnergyRecoveryVentilator> Create(EnergyRecoveryVentilator value) =>
        new DragonEnergyRecoveryVentilatorGoo(value);
    protected override DragonGoo<EnergyRecoveryVentilator> CreateEmpty() => new DragonEnergyRecoveryVentilatorGoo();
    protected override string DisplayText(EnergyRecoveryVentilator value) => $"Energy Recovery Ventilator {value.Name}";
}

public sealed class DragonPhotovoltaicPanelGoo : DragonGoo<PhotovoltaicPanel>
{
    public DragonPhotovoltaicPanelGoo() { }
    public DragonPhotovoltaicPanelGoo(PhotovoltaicPanel value) : base(value) { }
    public override string TypeName => "InvisibleDragon Photovoltaic Panel";
    public override string TypeDescription => "An InvisibleDragon fixed-geometry photovoltaic panel.";
    protected override DragonGoo<PhotovoltaicPanel> Create(PhotovoltaicPanel value) => new DragonPhotovoltaicPanelGoo(value);
    protected override DragonGoo<PhotovoltaicPanel> CreateEmpty() => new DragonPhotovoltaicPanelGoo();
    protected override string DisplayText(PhotovoltaicPanel value) => $"Photovoltaic Panel {value.Name} ({value.AreaSquareMetres:0.###} m²)";
}

public sealed class DragonIdfGoo : DragonGoo<IdfDocument>
{
    public DragonIdfGoo() { }
    public DragonIdfGoo(IdfDocument value) : base(value) { }
    public override string TypeName => "InvisibleDragon IDF";
    public override string TypeDescription => "An ordered EnergyPlus IDF document.";
    protected override DragonGoo<IdfDocument> Create(IdfDocument value) => new DragonIdfGoo(value);
    protected override DragonGoo<IdfDocument> CreateEmpty() => new DragonIdfGoo();
    protected override string DisplayText(IdfDocument value) => $"IDF {value.EnergyPlusVersion ?? "unversioned"} ({value.Count} objects)";
}

public sealed class EnergyPlusResultGoo : DragonGoo<EnergyPlusSimulationResult>
{
    public EnergyPlusResultGoo() { }
    public EnergyPlusResultGoo(EnergyPlusSimulationResult value) : base(value) { }
    public override string TypeName => "InvisibleDragon EnergyPlus Result";
    public override string TypeDescription => "A structured EnergyPlus simulation result.";
    protected override DragonGoo<EnergyPlusSimulationResult> Create(EnergyPlusSimulationResult value) => new EnergyPlusResultGoo(value);
    protected override DragonGoo<EnergyPlusSimulationResult> CreateEmpty() => new EnergyPlusResultGoo();
    protected override string DisplayText(EnergyPlusSimulationResult value) =>
        $"EnergyPlus Result ({value.ErrorLog.Summary.WarningCount} warnings, {value.ErrorLog.Summary.SevereCount} severe)";
}

public sealed class DiagnosticGoo : DragonGoo<Diagnostic>
{
    public DiagnosticGoo() { }
    public DiagnosticGoo(Diagnostic value) : base(value) { }
    public override string TypeName => "InvisibleDragon Diagnostic";
    public override string TypeDescription => "A stable validation or execution diagnostic.";
    protected override DragonGoo<Diagnostic> Create(Diagnostic value) => new DiagnosticGoo(value);
    protected override DragonGoo<Diagnostic> CreateEmpty() => new DiagnosticGoo();
    protected override string DisplayText(Diagnostic value) => $"{value.Severity}: {value.Code} - {value.Message}";
}
