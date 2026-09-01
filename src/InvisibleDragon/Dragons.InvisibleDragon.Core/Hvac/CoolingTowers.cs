using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Internal;
using Dragons.InvisibleDragon.Model;

namespace Dragons.InvisibleDragon.Hvac;

/// <summary>
/// Base class for the open- and closed-circuit heat-rejection equipment used by a chiller.
/// </summary>
public abstract class CoolingTower
{
    protected CoolingTower(
        EntityId id,
        string name,
        double? nominalCapacityWatts,
        double pumpMotorEfficiency)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = DomainGuard.RequiredText(name, nameof(name));
        NominalCapacityWatts = nominalCapacityWatts is null
            ? null
            : DomainGuard.Positive(nominalCapacityWatts.Value, nameof(nominalCapacityWatts));
        PumpMotorEfficiency = DomainGuard.InRange(
            pumpMotorEfficiency,
            0.000001,
            1,
            nameof(pumpMotorEfficiency));
    }

    public EntityId Id { get; }

    public string Name { get; }

    public double? NominalCapacityWatts { get; }

    public double PumpMotorEfficiency { get; }

    public abstract string IdfObjectType { get; }

    public static string ObjectNameFor(SourceSystem source)
    {
        DomainGuard.NotNull(source, nameof(source));
        return $"CT_for_{source.IdfObjectName}";
    }

    public static string LoopNameFor(SourceSystem source)
    {
        return $"Loop_for_{ObjectNameFor(source)}";
    }

    public IReadOnlyList<IdfObject> ToIdfObjects(
        IdfGenerationContext context,
        SourceSystem source)
    {
        DomainGuard.NotNull(context, nameof(context));
        DomainGuard.NotNull(source, nameof(source));
        if (source is not Chiller && source is not AbsorptionChiller)
        {
            throw new ArgumentException(
                "Cooling-tower condenser loops require a chiller or absorption chiller.",
                nameof(source));
        }

        string sourceName = source.IdfObjectName;
        IdfObject tower = CreateMainObject(context, source);
        return CoolingPlantLoopAssembler.CreateCondenserLoop(
            context,
            this,
            source,
            tower,
            $"{sourceName} Condenser InletNode",
            $"{sourceName} Condenser OutletNode");
    }

    protected double CapacityFor(SourceSystem source)
    {
        DomainGuard.NotNull(source, nameof(source));
        return NominalCapacityWatts
            ?? (source switch
            {
                Chiller chiller => chiller.NominalCapacityWatts,
                AbsorptionChiller absorption => absorption.NominalCapacityWatts,
                _ => null,
            })
            ?? 1E6;
    }

    protected abstract IdfObject CreateMainObject(
        IdfGenerationContext context,
        SourceSystem source);
}

/// <summary>
/// Open-circuit cooling tower with a single-speed fan.
/// </summary>
public sealed class OpenSingleSpeedCoolingTower : CoolingTower
{
    public OpenSingleSpeedCoolingTower(
        EntityId id,
        string name,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9)
        : base(id, name, nominalCapacityWatts, pumpMotorEfficiency)
    {
    }

    public override string IdfObjectType => "CoolingTower:SingleSpeed";

    protected override IdfObject CreateMainObject(
        IdfGenerationContext context,
        SourceSystem source)
    {
        string name = ObjectNameFor(source);
        if (context.Options.UseLegacySimpleDragonHvacTopology)
        {
            return context.Create(
                IdfObjectType,
                IdfGenerationContext.Field(0, "Name", name),
                IdfGenerationContext.Field(1, "Water Inlet Node Name", $"{name} Water InletNode"),
                IdfGenerationContext.Field(2, "Water Outlet Node Name", $"{name} Water OutletNode"),
                IdfGenerationContext.Field(3, "Design Water Flow Rate", "autosize"),
                IdfGenerationContext.Field(4, "Design Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(5, "Design Fan Power", "autosize"),
                IdfGenerationContext.Field(6, "Design U-Factor Times Area Value", "autosize"),
                IdfGenerationContext.Field(7, "Free Convection Regime Air Flow Rate", "autocalculate"),
                IdfGenerationContext.Field(8, "Free Convection Regime Air Flow Rate Sizing Factor", 0.1),
                IdfGenerationContext.Field(9, "Free Convection Regime U-Factor Times Area Value", "autocalculate"),
                IdfGenerationContext.Field(10, "Free Convection U-Factor Times Area Value Sizing Factor", 0.1),
                IdfGenerationContext.Field(11, "Performance Input Method", "UFactorTimesAreaAndDesignWaterFlowRate"),
                IdfGenerationContext.Field(12, "Heat Rejection Capacity and Nominal Capacity Sizing Ratio", 1.25),
                IdfGenerationContext.Field(13, "Nominal Capacity", CapacityFor(source)),
                IdfGenerationContext.Field(14, "Free Convection Capacity", "autocalculate"),
                IdfGenerationContext.Field(15, "Free Convection Nominal Capacity Sizing Factor", 0.1),
                IdfGenerationContext.Field(16, "Design Inlet Air Dry-Bulb Temperature", 35),
                IdfGenerationContext.Field(17, "Design Inlet Air Wet-Bulb Temperature", 25.6),
                IdfGenerationContext.Field(18, "Design Approach Temperature", "autosize"),
                IdfGenerationContext.Field(19, "Design Range Temperature", "autosize"),
                IdfGenerationContext.Field(20, "Basin Heater Capacity", 0),
                IdfGenerationContext.Field(21, "Basin Heater Setpoint Temperature", 2),
                IdfGenerationContext.Field(22, "Basin Heater Operating Schedule Name", null),
                IdfGenerationContext.Field(23, "Evaporation Loss Mode", "SaturatedExit"),
                IdfGenerationContext.Field(24, "Evaporation Loss Factor", 0.2),
                IdfGenerationContext.Field(25, "Drift Loss Percent", 0.008),
                IdfGenerationContext.Field(26, "Blowdown Calculation Mode", "ConcentrationRatio"),
                IdfGenerationContext.Field(27, "Blowdown Concentration Ratio", 3),
                IdfGenerationContext.Field(28, "Blowdown Makeup Water Usage Schedule Name", "ALLON"),
                IdfGenerationContext.Field(29, "Supply Water Storage Tank Name", null),
                IdfGenerationContext.Field(30, "Outdoor Air Inlet Node Name", null),
                IdfGenerationContext.Field(31, "Capacity Control", "FanCycling"),
                IdfGenerationContext.Field(32, "Number of Cells", 1),
                IdfGenerationContext.Field(33, "Cell Control", "MaximalCell"),
                IdfGenerationContext.Field(34, "Cell Minimum  Water Flow Rate Fraction", 0.33),
                IdfGenerationContext.Field(35, "Cell Maximum Water Flow Rate Fraction", 2.5),
                IdfGenerationContext.Field(36, "Sizing Factor", 1),
                IdfGenerationContext.Field(37, "End-Use Subcategory", "General"));
        }

        return context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Water Inlet Node Name", $"{name} Water InletNode"),
            IdfGenerationContext.Field(2, "Water Outlet Node Name", $"{name} Water OutletNode"),
            IdfGenerationContext.Field(3, "Design Water Flow Rate", null),
            IdfGenerationContext.Field(4, "Design Air Flow Rate", "autosize"),
            IdfGenerationContext.Field(5, "Design Fan Power", "autosize"),
            IdfGenerationContext.Field(6, "Design U-Factor Times Area Value", null),
            IdfGenerationContext.Field(7, "Free Convection Regime Air Flow Rate", "autocalculate"),
            IdfGenerationContext.Field(9, "Free Convection Regime U-Factor Times Area Value", null),
            IdfGenerationContext.Field(11, "Performance Input Method", "NominalCapacity"),
            IdfGenerationContext.Field(13, "Nominal Capacity", CapacityFor(source)),
            IdfGenerationContext.Field(14, "Free Convection Capacity", "autocalculate"),
            IdfGenerationContext.Field(28, "Blowdown Makeup Water Usage Schedule Name", "ALLON"));
    }
}

/// <summary>
/// Open-circuit cooling tower with high-, low-, and free-convection fan stages.
/// </summary>
public sealed class OpenTwoSpeedCoolingTower : CoolingTower
{
    public OpenTwoSpeedCoolingTower(
        EntityId id,
        string name,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9)
        : base(id, name, nominalCapacityWatts, pumpMotorEfficiency)
    {
    }

    public override string IdfObjectType => "CoolingTower:TwoSpeed";

    protected override IdfObject CreateMainObject(
        IdfGenerationContext context,
        SourceSystem source)
    {
        string name = ObjectNameFor(source);
        if (context.Options.UseLegacySimpleDragonHvacTopology)
        {
            return context.Create(
                IdfObjectType,
                IdfGenerationContext.Field(0, "Name", name),
                IdfGenerationContext.Field(1, "Water Inlet Node Name", $"{name} Water InletNode"),
                IdfGenerationContext.Field(2, "Water Outlet Node Name", $"{name} Water OutletNode"),
                IdfGenerationContext.Field(3, "Design Water Flow Rate", "autosize"),
                IdfGenerationContext.Field(4, "High Fan Speed Air Flow Rate", "autosize"),
                IdfGenerationContext.Field(5, "High Fan Speed Fan Power", "autosize"),
                IdfGenerationContext.Field(6, "High Fan Speed U-Factor Times Area Value", "autosize"),
                IdfGenerationContext.Field(7, "Low Fan Speed Air Flow Rate", "autocalculate"),
                IdfGenerationContext.Field(8, "Low Fan Speed Air Flow Rate Sizing Factor", 0.5),
                IdfGenerationContext.Field(9, "Low Fan Speed Fan Power", "autocalculate"),
                IdfGenerationContext.Field(10, "Low Fan Speed Fan Power Sizing Factor", 0.16),
                IdfGenerationContext.Field(11, "Low Fan Speed U-Factor Times Area Value", "autocalculate"),
                IdfGenerationContext.Field(12, "Low Fan Speed U-Factor Times Area Sizing Factor", 0.6),
                IdfGenerationContext.Field(13, "Free Convection Regime Air Flow Rate", "autocalculate"),
                IdfGenerationContext.Field(14, "Free Convection Regime Air Flow Rate Sizing Factor", 0.1),
                IdfGenerationContext.Field(15, "Free Convection Regime U-Factor Times Area Value", "autocalculate"),
                IdfGenerationContext.Field(16, "Free Convection U-Factor Times Area Value Sizing Factor", 0.1),
                IdfGenerationContext.Field(17, "Performance Input Method", "UFactorTimesAreaAndDesignWaterFlowRate"),
                IdfGenerationContext.Field(18, "Heat Rejection Capacity and Nominal Capacity Sizing Ratio", 1.25),
                IdfGenerationContext.Field(19, "High Speed Nominal Capacity", CapacityFor(source)),
                IdfGenerationContext.Field(20, "Low Speed Nominal Capacity", "autocalculate"),
                IdfGenerationContext.Field(21, "Low Speed Nominal Capacity Sizing Factor", 0.5),
                IdfGenerationContext.Field(22, "Free Convection Nominal Capacity", "autocalculate"),
                IdfGenerationContext.Field(23, "Free Convection Nominal Capacity Sizing Factor", 0.1),
                IdfGenerationContext.Field(24, "Design Inlet Air Dry-Bulb Temperature", 35),
                IdfGenerationContext.Field(25, "Design Inlet Air Wet-Bulb Temperature", 25.6),
                IdfGenerationContext.Field(26, "Design Approach Temperature", "autosize"),
                IdfGenerationContext.Field(27, "Design Range Temperature", "autosize"),
                IdfGenerationContext.Field(28, "Basin Heater Capacity", 0),
                IdfGenerationContext.Field(29, "Basin Heater Setpoint Temperature", 2),
                IdfGenerationContext.Field(30, "Basin Heater Operating Schedule Name", null),
                IdfGenerationContext.Field(31, "Evaporation Loss Mode", "SaturatedExit"),
                IdfGenerationContext.Field(32, "Evaporation Loss Factor", 0.2),
                IdfGenerationContext.Field(33, "Drift Loss Percent", 0.008),
                IdfGenerationContext.Field(34, "Blowdown Calculation Mode", "ConcentrationRatio"),
                IdfGenerationContext.Field(35, "Blowdown Concentration Ratio", 3),
                IdfGenerationContext.Field(36, "Blowdown Makeup Water Usage Schedule Name", "ALLON"),
                IdfGenerationContext.Field(37, "Supply Water Storage Tank Name", null),
                IdfGenerationContext.Field(38, "Outdoor Air Inlet Node Name", null),
                IdfGenerationContext.Field(39, "Number of Cells", 1),
                IdfGenerationContext.Field(40, "Cell Control", "MaximalCell"),
                IdfGenerationContext.Field(41, "Cell Minimum  Water Flow Rate Fraction", 0.33),
                IdfGenerationContext.Field(42, "Cell Maximum Water Flow Rate Fraction", 2.5),
                IdfGenerationContext.Field(43, "Sizing Factor", 1),
                IdfGenerationContext.Field(44, "End-Use Subcategory", "General"));
        }

        return context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Water Inlet Node Name", $"{name} Water InletNode"),
            IdfGenerationContext.Field(2, "Water Outlet Node Name", $"{name} Water OutletNode"),
            IdfGenerationContext.Field(3, "Design Water Flow Rate", null),
            IdfGenerationContext.Field(4, "High Fan Speed Air Flow Rate", "autosize"),
            IdfGenerationContext.Field(5, "High Fan Speed Fan Power", "autosize"),
            IdfGenerationContext.Field(6, "High Fan Speed U-Factor Times Area Value", null),
            IdfGenerationContext.Field(7, "Low Fan Speed Air Flow Rate", "autocalculate"),
            IdfGenerationContext.Field(9, "Low Fan Speed Fan Power", "autocalculate"),
            IdfGenerationContext.Field(11, "Low Fan Speed U-Factor Times Area Value", null),
            IdfGenerationContext.Field(13, "Free Convection Regime Air Flow Rate", "autocalculate"),
            IdfGenerationContext.Field(15, "Free Convection Regime U-Factor Times Area Value", null),
            IdfGenerationContext.Field(17, "Performance Input Method", "NominalCapacity"),
            IdfGenerationContext.Field(19, "High Speed Nominal Capacity", CapacityFor(source)),
            IdfGenerationContext.Field(20, "Low Speed Nominal Capacity", "autocalculate"),
            IdfGenerationContext.Field(22, "Free Convection Nominal Capacity", "autocalculate"),
            IdfGenerationContext.Field(36, "Blowdown Makeup Water Usage Schedule Name", "ALLON"));
    }
}

/// <summary>
/// Closed-circuit dry fluid cooler with a single-speed fan.
/// </summary>
public sealed class ClosedSingleSpeedCoolingTower : CoolingTower
{
    public ClosedSingleSpeedCoolingTower(
        EntityId id,
        string name,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9)
        : base(id, name, nominalCapacityWatts, pumpMotorEfficiency)
    {
    }

    public override string IdfObjectType => "FluidCooler:SingleSpeed";

    protected override IdfObject CreateMainObject(
        IdfGenerationContext context,
        SourceSystem source)
    {
        string name = ObjectNameFor(source);
        return context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Water Inlet Node Name", $"{name} Water InletNode"),
            IdfGenerationContext.Field(2, "Water Outlet Node Name", $"{name} Water OutletNode"),
            IdfGenerationContext.Field(3, "Performance Input Method", "NominalCapacity"),
            IdfGenerationContext.Field(
                4,
                "Design Air Flow Rate U-factor Times Area Value",
                context.Options.UseLegacySimpleDragonHvacTopology ? "autosize" : null),
            IdfGenerationContext.Field(5, "Nominal Capacity", CapacityFor(source)),
            IdfGenerationContext.Field(6, "Design Entering Water Temperature", 35),
            IdfGenerationContext.Field(7, "Design Entering Air Temperature", 28),
            IdfGenerationContext.Field(8, "Design Entering Air Wetbulb Temperature", 25.56),
            IdfGenerationContext.Field(9, "Design Water Flow Rate", "autosize"),
            IdfGenerationContext.Field(10, "Design Air Flow Rate", "autosize"),
            IdfGenerationContext.Field(11, "Design Air Flow Rate Fan Power", "autosize"));
    }
}

/// <summary>
/// Closed-circuit dry fluid cooler with high and low fan stages.
/// </summary>
public sealed class ClosedTwoSpeedCoolingTower : CoolingTower
{
    public ClosedTwoSpeedCoolingTower(
        EntityId id,
        string name,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9)
        : base(id, name, nominalCapacityWatts, pumpMotorEfficiency)
    {
    }

    public override string IdfObjectType => "FluidCooler:TwoSpeed";

    protected override IdfObject CreateMainObject(
        IdfGenerationContext context,
        SourceSystem source)
    {
        string name = ObjectNameFor(source);
        bool legacy = context.Options.UseLegacySimpleDragonHvacTopology;
        return context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Name", name),
            IdfGenerationContext.Field(1, "Water Inlet Node Name", $"{name} Water InletNode"),
            IdfGenerationContext.Field(2, "Water Outlet Node Name", $"{name} Water OutletNode"),
            IdfGenerationContext.Field(
                3,
                "Performance Input Method",
                legacy ? "UFactorTimesAreaAndDesignWaterFlowRate" : "NominalCapacity"),
            IdfGenerationContext.Field(
                4,
                "High Fan Speed U-factor Times Area Value",
                legacy ? "autosize" : null),
            IdfGenerationContext.Field(
                5,
                "Low Fan Speed U-factor Times Area Value",
                legacy ? "autocalculate" : null),
            IdfGenerationContext.Field(
                7,
                "High Speed Nominal Capacity",
                legacy ? null : CapacityFor(source)),
            IdfGenerationContext.Field(
                8,
                "Low Speed Nominal Capacity",
                legacy ? null : "autocalculate"),
            IdfGenerationContext.Field(10, "Design Entering Water Temperature", 35),
            IdfGenerationContext.Field(11, "Design Entering Air Temperature", 28),
            IdfGenerationContext.Field(12, "Design Entering Air Wet-bulb Temperature", 25.56),
            IdfGenerationContext.Field(13, "Design Water Flow Rate", "autosize"),
            IdfGenerationContext.Field(14, "High Fan Speed Air Flow Rate", "autosize"),
            IdfGenerationContext.Field(15, "High Fan Speed Fan Power", "autosize"),
            IdfGenerationContext.Field(16, "Low Fan Speed Air Flow Rate", "autocalculate"),
            IdfGenerationContext.Field(17, "Low Fan Speed Air Flow Rate Sizing Factor", 0.5),
            IdfGenerationContext.Field(18, "Low Fan Speed Fan Power", "autocalculate"),
            IdfGenerationContext.Field(19, "Low Fan Speed Fan Power Sizing Factor", 0.16));
    }
}
