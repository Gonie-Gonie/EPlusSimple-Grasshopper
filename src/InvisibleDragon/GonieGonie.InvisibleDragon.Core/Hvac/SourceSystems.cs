using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Internal;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Hvac;

/// <summary>
/// A variable-refrigerant-flow outdoor heat-pump unit.
/// </summary>
public class HeatPump : SourceSystem
{
    public HeatPump(
        EntityId id,
        string name,
        Fuel fuel,
        double heatingCoefficientOfPerformance,
        double coolingCoefficientOfPerformance,
        double? heatingCapacityWatts = null,
        double? coolingCapacityWatts = null)
        : base(id, name)
    {
        if (!Enum.IsDefined(typeof(Fuel), fuel))
        {
            throw new ArgumentOutOfRangeException(nameof(fuel));
        }

        Fuel = fuel;
        HeatingCoefficientOfPerformance = DomainGuard.Positive(
            heatingCoefficientOfPerformance,
            nameof(heatingCoefficientOfPerformance));
        CoolingCoefficientOfPerformance = DomainGuard.Positive(
            coolingCoefficientOfPerformance,
            nameof(coolingCoefficientOfPerformance));
        HeatingCapacityWatts = OptionalPositive(heatingCapacityWatts, nameof(heatingCapacityWatts));
        CoolingCapacityWatts = OptionalPositive(coolingCapacityWatts, nameof(coolingCapacityWatts));
    }

    public Fuel Fuel { get; }

    public double HeatingCoefficientOfPerformance { get; }

    public double CoolingCoefficientOfPerformance { get; }

    public double? HeatingCapacityWatts { get; }

    public double? CoolingCapacityWatts { get; }

    public override string IdfObjectType => "AirConditioner:VariableRefrigerantFlow";

    public override string IdfObjectName => $"HeatPump_named_{Name}";

    public string TerminalUnitListName => $"Terminal_Units_for_{IdfObjectName}";

    public override IReadOnlyList<IdfObject> ToIdfObjects(
        IdfGenerationContext context,
        IReadOnlyList<PlantDemandConnection>? demandConnections = null,
        IReadOnlyList<string>? terminalUnitNames = null)
    {
        DomainGuard.NotNull(context, nameof(context));
        List<IdfObject> objects = CreatePerformanceCurves(context).ToList();
        object?[] terminalFields = new object?[1 + (terminalUnitNames?.Count ?? 0)];
        terminalFields[0] = TerminalUnitListName;
        for (int index = 0; index < terminalFields.Length - 1; index++)
        {
            terminalFields[index + 1] = terminalUnitNames![index];
        }

        objects.Add(context.CreateRaw("ZoneTerminalUnitList", terminalFields));
        objects.Add(context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Heat Pump Name", IdfObjectName),
            IdfGenerationContext.Field(1, "Availability Schedule Name", "ALLON"),
            IdfGenerationContext.Field(2, "Gross Rated Total Cooling Capacity", CoolingCapacityWatts ?? (object)"autosize"),
            IdfGenerationContext.Field(3, "Gross Rated Cooling COP", CoolingCoefficientOfPerformance),
            IdfGenerationContext.Field(4, "Minimum Outdoor Temperature in Cooling Mode", -6),
            IdfGenerationContext.Field(5, "Maximum Outdoor Temperature in Cooling Mode", 43),
            IdfGenerationContext.Field(8, "Cooling Capacity Ratio Modifier Function of Low Temperature Curve Name", Curve("CoolingCapaMF_LowTemp")),
            IdfGenerationContext.Field(9, "Cooling Capacity Ratio Boundary Curve Name", Curve("CoolingCapaBoundary")),
            IdfGenerationContext.Field(10, "Cooling Capacity Ratio Modifier Function of High Temperature Curve Name", Curve("CoolingCapaMF_HighTemp")),
            IdfGenerationContext.Field(11, "Cooling Energy Input Ratio Modifier Function of Low Temperature Curve Name", Curve("CoolingEIRMF_LowTemp")),
            IdfGenerationContext.Field(12, "Cooling Energy Input Ratio Boundary Curve Name", Curve("CoolingEIRBoundary")),
            IdfGenerationContext.Field(13, "Cooling Energy Input Ratio Modifier Function of High Temperature Curve Name", Curve("CoolingEIRMF_HighTemp")),
            IdfGenerationContext.Field(14, "Cooling Energy Input Ratio Modifier Function of Low Part-Load Ratio Curve Name", Curve("CoolingEIRMF_LowPLR")),
            IdfGenerationContext.Field(15, "Cooling Energy Input Ratio Modifier Function of High Part-Load Ratio Curve Name", Curve("CoolingEIRMF_HighPLR")),
            IdfGenerationContext.Field(16, "Cooling Combination Ratio Correction Factor Curve Name", Curve("CoolingCombCorrection")),
            IdfGenerationContext.Field(17, "Cooling Part-Load Fraction Correlation Curve Name", Curve("CoolingPLRCorrelation")),
            IdfGenerationContext.Field(18, "Gross Rated Heating Capacity", HeatingCapacityWatts ?? (object)"autosize"),
            IdfGenerationContext.Field(19, "Rated Heating Capacity Sizing Ratio", 1),
            IdfGenerationContext.Field(20, "Gross Rated Heating COP", HeatingCoefficientOfPerformance),
            IdfGenerationContext.Field(24, "Heating Capacity Ratio Modifier Function of Low Temperature Curve Name", Curve("HeatingCapaMF_LowTemp")),
            IdfGenerationContext.Field(25, "Heating Capacity Ratio Boundary Curve Name", Curve("HeatingCapaBoundary")),
            IdfGenerationContext.Field(26, "Heating Capacity Ratio Modifier Function of High Temperature Curve Name", Curve("HeatingCapaMF_HighTemp")),
            IdfGenerationContext.Field(27, "Heating Energy Input Ratio Modifier Function of Low Temperature Curve Name", Curve("HeatingEIRMF_LowTemp")),
            IdfGenerationContext.Field(28, "Heating Energy Input Ratio Boundary Curve Name", Curve("HeatingEIRBoundary")),
            IdfGenerationContext.Field(29, "Heating Energy Input Ratio Modifier Function of High Temperature Curve Name", Curve("HeatingEIRMF_HighTemp")),
            IdfGenerationContext.Field(30, "Heating Energy Input Ratio Modifier Function of Low Part-Load Ratio Curve Name", Curve("HeatingEIRMF_LowPLR")),
            IdfGenerationContext.Field(31, "Heating Energy Input Ratio Modifier Function of High Part-Load Ratio Curve Name", Curve("HeatingEIRMF_HighPLR")),
            IdfGenerationContext.Field(32, "Heating Combination Ratio Correction Factor Curve Name", Curve("HeatingCombCorrection")),
            IdfGenerationContext.Field(33, "Heating Part-Load Fraction Correlation Curve Name", Curve("HeatingPLRCorrelation")),
            IdfGenerationContext.Field(38, "Master Thermostat Priority Control Type", "LoadPriority"),
            IdfGenerationContext.Field(44, "Zone Terminal Unit List Name", TerminalUnitListName),
            IdfGenerationContext.Field(49, "Fuel Type", Fuel)));
        return objects;
    }

    private IEnumerable<IdfObject> CreatePerformanceCurves(IdfGenerationContext context)
    {
        yield return Biquadratic(context, "CoolingCapaMF_LowTemp", 0.576882692, 0.017447952, 0.000583269, -1.76324E-06, -7.474E-09, -1.30413E-07, 15, 24, -5, 23);
        yield return Cubic(context, "CoolingCapaBoundary", 25.73473775, -0.03150043, -0.01416595, 0, 11, 30);
        yield return Biquadratic(context, "CoolingCapaMF_HighTemp", 0.6867358, 0.0207631, 0.0005447, -0.0016218, -4.259E-07, -0.0003392, 15, 24, 16, 43);
        yield return Biquadratic(context, "CoolingEIRMF_LowTemp", 0.989010541, -0.02347967, 0.000199711, 0.005968336, -1.0289E-07, -0.00015686, 15, 24, -5, 23);
        yield return Cubic(context, "CoolingEIRBoundary", 25.73473775, -0.03150043, -0.01416595, 0, 15, 24);
        yield return Biquadratic(context, "CoolingEIRMF_HighTemp", 0.1435147, 0.01860035, -0.0003954, 0.02485219, 0.00016329, -0.0006244, 15, 24, 16, 43);
        yield return Cubic(context, "CoolingEIRMF_LowPLR", 0.4628123, -1.0402406, 2.17490997, -0.5974817, 0, 1);
        yield return Linear(context, "CoolingEIRMF_HighPLR", 1, 0, 1, 1.5);
        yield return Linear(context, "CoolingCombCorrection", 0.618055, 0.381945, 1, 1.5);
        yield return Linear(context, "CoolingPLRCorrelation", 0.85, 0.15, 0, 1);
        yield return Biquadratic(context, "HeatingCapaMF_LowTemp", 1.014599599, -0.002506703, -0.000141599, 0.026931595, 1.83538E-06, -0.000358147, 15, 27, -20, 15);
        yield return Cubic(context, "HeatingCapaBoundary", -7.6000882, 3.05090016, -0.1162844, 0, 15, 27);
        yield return Biquadratic(context, "HeatingCapaMF_HighTemp", 1.161134821, 0.027478868, -0.00168795, 0.001783378, 2.03208E-06, -6.8969E-05, 15, 27, -10, 15);
        yield return Biquadratic(context, "HeatingEIRMF_LowTemp", 0.87465501, -0.01319754, 0.00110307, -0.0133118, 0.00089017, -0.00012766, 15, 27, -20, 12);
        yield return Cubic(context, "HeatingEIRBoundary", -7.6000882, 3.05090016, -0.1162844, 0, 15, 27);
        yield return Biquadratic(context, "HeatingEIRMF_HighTemp", 2.504005146, -0.05736767, 4.07336E-05, -0.12959669, 0.00135839, 0.00317047, 15, 27, -10, 15);
        yield return Cubic(context, "HeatingEIRMF_LowPLR", 0.1400093, 0.6415002, 0.1339047, 0.0845859, 0, 1);
        yield return context.CreateRaw("Curve:Quadratic", Curve("HeatingEIRMF_HighPLR"), 2.4294355, -2.235887, 0.8064516, 1, 1.5);
        yield return Linear(context, "HeatingCombCorrection", 0.96034, 0.03966, 1, 1.5);
        yield return Linear(context, "HeatingPLRCorrelation", 0.85, 0.15, 0, 1);
    }

    private IdfObject Biquadratic(IdfGenerationContext context, string suffix, params object?[] values) =>
        context.CreateRaw("Curve:Biquadratic", new object?[] { Curve(suffix) }.Concat(values).Concat(new object?[] { null, null, "Temperature", "Temperature", "Dimensionless" }).ToArray());

    private IdfObject Cubic(IdfGenerationContext context, string suffix, params object?[] values) =>
        context.CreateRaw("Curve:Cubic", new object?[] { Curve(suffix) }.Concat(values).Concat(new object?[] { null, null, "Temperature" }).ToArray());

    private IdfObject Linear(IdfGenerationContext context, string suffix, params object?[] values) =>
        context.CreateRaw("Curve:Linear", new object?[] { Curve(suffix) }.Concat(values).ToArray());

    private string Curve(string suffix) => $"Curve_for_{IdfObjectName}:{suffix}";

    private static double? OptionalPositive(double? value, string parameterName) =>
        value is null ? null : DomainGuard.Positive(value.Value, parameterName);
}

/// <summary>
/// A hot-water boiler and its closed heating plant loop.
/// </summary>
public sealed class Boiler : SourceSystem
{
    public Boiler(
        EntityId id,
        string name,
        Fuel fuel,
        double nominalThermalEfficiency = 0.9,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9,
        double setpointTemperatureCelsius = 60)
        : base(id, name)
    {
        if (!Enum.IsDefined(typeof(Fuel), fuel))
        {
            throw new ArgumentOutOfRangeException(nameof(fuel));
        }

        Fuel = fuel;
        NominalThermalEfficiency = DomainGuard.InRange(nominalThermalEfficiency, 0.000001, 1, nameof(nominalThermalEfficiency));
        NominalCapacityWatts = nominalCapacityWatts is null ? null : DomainGuard.Positive(nominalCapacityWatts.Value, nameof(nominalCapacityWatts));
        PumpMotorEfficiency = DomainGuard.InRange(pumpMotorEfficiency, 0.000001, 1, nameof(pumpMotorEfficiency));
        SetpointTemperatureCelsius = DomainGuard.Finite(setpointTemperatureCelsius, nameof(setpointTemperatureCelsius));
    }

    public Fuel Fuel { get; }

    public double NominalThermalEfficiency { get; }

    public double? NominalCapacityWatts { get; }

    public double PumpMotorEfficiency { get; }

    public double SetpointTemperatureCelsius { get; }

    public override string IdfObjectType => "Boiler:HotWater";

    public override string IdfObjectName => $"Boiler_named_{Name}";

    public override IReadOnlyList<IdfObject> ToIdfObjects(
        IdfGenerationContext context,
        IReadOnlyList<PlantDemandConnection>? demandConnections = null,
        IReadOnlyList<string>? terminalUnitNames = null)
    {
        DomainGuard.NotNull(context, nameof(context));
        IdfObject component = context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Name", IdfObjectName),
            IdfGenerationContext.Field(1, "Fuel Type", Fuel),
            IdfGenerationContext.Field(2, "Nominal Capacity", NominalCapacityWatts ?? (object)"autosize"),
            IdfGenerationContext.Field(3, "Nominal Thermal Efficiency", NominalThermalEfficiency),
            IdfGenerationContext.Field(5, "Design Water Flow Rate", "autosize"),
            IdfGenerationContext.Field(6, "Minimum Part Load Ratio", 0),
            IdfGenerationContext.Field(7, "Maximum Part Load Ratio", 1.2),
            IdfGenerationContext.Field(8, "Optimum Part Load Ratio", 1),
            IdfGenerationContext.Field(9, "Boiler Water Inlet Node Name", $"{IdfObjectName} Water InletNode"),
            IdfGenerationContext.Field(10, "Boiler Water Outlet Node Name", $"{IdfObjectName} Water OutletNode"),
            IdfGenerationContext.Field(13, "Efficiency Curve Temperature Evaluation Variable", "LeavingBoiler"));
        return PlantLoopAssembler.CreateHeatingLoop(
            context,
            this,
            component,
            PumpMotorEfficiency,
            SetpointTemperatureCelsius,
            demandConnections ?? Array.Empty<PlantDemandConnection>());
    }
}

/// <summary>
/// A district hot-water source represented on a complete plant loop.
/// </summary>
public sealed class DistrictHeating : SourceSystem
{
    public DistrictHeating(
        EntityId id,
        string name,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9,
        double setpointTemperatureCelsius = 60)
        : base(id, name)
    {
        NominalCapacityWatts = nominalCapacityWatts is null ? null : DomainGuard.Positive(nominalCapacityWatts.Value, nameof(nominalCapacityWatts));
        PumpMotorEfficiency = DomainGuard.InRange(pumpMotorEfficiency, 0.000001, 1, nameof(pumpMotorEfficiency));
        SetpointTemperatureCelsius = DomainGuard.Finite(setpointTemperatureCelsius, nameof(setpointTemperatureCelsius));
    }

    public double? NominalCapacityWatts { get; }

    public double PumpMotorEfficiency { get; }

    public double SetpointTemperatureCelsius { get; }

    public override string IdfObjectType => "DistrictHeating:Water";

    public override string IdfObjectName => $"DistrictHeating_named_{Name}";

    public override IReadOnlyList<IdfObject> ToIdfObjects(
        IdfGenerationContext context,
        IReadOnlyList<PlantDemandConnection>? demandConnections = null,
        IReadOnlyList<string>? terminalUnitNames = null)
    {
        DomainGuard.NotNull(context, nameof(context));
        IdfObject component = context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Name", IdfObjectName),
            IdfGenerationContext.Field(1, "Hot Water Inlet Node Name", $"{IdfObjectName} Water InletNode"),
            IdfGenerationContext.Field(2, "Hot Water Outlet Node Name", $"{IdfObjectName} Water OutletNode"),
            IdfGenerationContext.Field(3, "Nominal Capacity", NominalCapacityWatts ?? (object)"autosize"));
        return PlantLoopAssembler.CreateHeatingLoop(
            context,
            this,
            component,
            PumpMotorEfficiency,
            SetpointTemperatureCelsius,
            demandConnections ?? Array.Empty<PlantDemandConnection>());
    }
}
