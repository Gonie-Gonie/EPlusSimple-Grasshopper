using Grasshopper.Kernel.Types;

namespace GonieGonie.SimpleDragon.Grasshopper.Types;

/// <summary>
/// Grasshopper wrapper for one geometry-backed opening definition.
/// </summary>
public sealed class SimpleDragonOpeningDefinitionGoo : SimpleDragonGoo<OpeningDefinition>
{
    public SimpleDragonOpeningDefinitionGoo()
    {
    }

    public SimpleDragonOpeningDefinitionGoo(OpeningDefinition value)
        : base(value)
    {
    }

    public override string TypeName => "SimpleDragon Opening Definition";

    public override string TypeDescription =>
        "A geometry-backed opening that is connected directly to its owning SimpleDragon zone.";

    protected override SimpleDragonGoo<OpeningDefinition> Create(OpeningDefinition value) =>
        new SimpleDragonOpeningDefinitionGoo(value);

    protected override SimpleDragonGoo<OpeningDefinition> CreateEmpty() =>
        new SimpleDragonOpeningDefinitionGoo();

    protected override string DisplayText(OpeningDefinition value) =>
        $"Opening {value.Name} ({value.Type})";
}

/// <summary>
/// Grasshopper wrapper for one composable, geometry-backed zone definition.
/// </summary>
public sealed class SimpleDragonZoneDefinitionGoo : SimpleDragonGoo<ZoneDefinition>
{
    public SimpleDragonZoneDefinitionGoo()
    {
    }

    public SimpleDragonZoneDefinitionGoo(ZoneDefinition value)
        : base(value)
    {
    }

    public override string TypeName => "SimpleDragon Zone Definition";

    public override string TypeDescription =>
        "A geometry-backed SimpleDragon zone with its openings, usage, and HVAC inputs.";

    protected override SimpleDragonGoo<ZoneDefinition> Create(ZoneDefinition value) =>
        new SimpleDragonZoneDefinitionGoo(value);

    protected override SimpleDragonGoo<ZoneDefinition> CreateEmpty() =>
        new SimpleDragonZoneDefinitionGoo();

    protected override string DisplayText(ZoneDefinition value) =>
        $"Zone Definition {value.Name} ({value.Openings.Count} openings)";
}

/// <summary>
/// Grasshopper wrapper for an ERV assignment, including the number of identical units.
/// </summary>
public sealed class SimpleDragonVentilationAssignmentGoo
    : SimpleDragonGoo<VentilationAssignment>
{
    public SimpleDragonVentilationAssignmentGoo()
    {
    }

    public SimpleDragonVentilationAssignmentGoo(VentilationAssignment value)
        : base(value)
    {
    }

    public override string TypeName => "SimpleDragon Ventilation Assignment";

    public override string TypeDescription =>
        "A SimpleDragon energy-recovery ventilator assignment with a unit count.";

    public override bool CastFrom(object source)
    {
        switch (source)
        {
            case VentilationSystem system:
                Value = CreateSingleUnitAssignment(system);
                return true;
            case SimpleDragonEnergyRecoveryVentilatorGoo { Value: not null } goo:
                Value = CreateSingleUnitAssignment(goo.Value);
                return true;
            case GH_ObjectWrapper { Value: VentilationSystem wrapped }:
                Value = CreateSingleUnitAssignment(wrapped);
                return true;
            default:
                return base.CastFrom(source);
        }
    }

    protected override SimpleDragonGoo<VentilationAssignment> Create(VentilationAssignment value) =>
        new SimpleDragonVentilationAssignmentGoo(value);

    protected override SimpleDragonGoo<VentilationAssignment> CreateEmpty() =>
        new SimpleDragonVentilationAssignmentGoo();

    protected override string DisplayText(VentilationAssignment value) =>
        $"Ventilation {value.VentilationSystemId} (x{value.Count})";

    private static VentilationAssignment CreateSingleUnitAssignment(VentilationSystem system)
    {
        VentilationSystem copy = SimpleDragonGooSnapshot.Deserialize<VentilationSystem>(
            SimpleDragonGooSnapshot.Serialize(system));
        return new VentilationAssignment(copy.Id.Value, 1, copy);
    }
}
