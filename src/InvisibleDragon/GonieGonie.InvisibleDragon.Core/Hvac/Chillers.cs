using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Internal;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Hvac;

/// <summary>
/// Compressor performance families retained from the pinned InvisibleDragon source.
/// </summary>
public enum CompressorType
{
    Turbo,
    Screw,
    Reciprocating,
}

/// <summary>
/// A water-cooled electric EIR chiller with complete chilled- and condenser-water loops.
/// </summary>
public sealed class Chiller : SourceSystem
{
    private string _availabilityScheduleName = "ALLON";

    public Chiller(
        EntityId id,
        string name,
        double referenceCoefficientOfPerformance,
        CompressorType compressor,
        CoolingTower coolingTower,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9,
        double setpointTemperatureCelsius = 6)
        : base(id, name)
    {
        if (!Enum.IsDefined(typeof(CompressorType), compressor))
        {
            throw new ArgumentOutOfRangeException(nameof(compressor));
        }

        CoolingTower = coolingTower ?? throw new ArgumentNullException(nameof(coolingTower));
        if (CoolingTower.Id.Equals(id))
        {
            throw new ArgumentException(
                "A chiller and its cooling tower require distinct identifiers.",
                nameof(coolingTower));
        }

        ReferenceCoefficientOfPerformance = DomainGuard.Positive(
            referenceCoefficientOfPerformance,
            nameof(referenceCoefficientOfPerformance));
        Compressor = compressor;
        NominalCapacityWatts = nominalCapacityWatts is null
            ? null
            : DomainGuard.Positive(nominalCapacityWatts.Value, nameof(nominalCapacityWatts));
        PumpMotorEfficiency = DomainGuard.InRange(
            pumpMotorEfficiency,
            0.000001,
            1,
            nameof(pumpMotorEfficiency));
        SetpointTemperatureCelsius = DomainGuard.InRange(
            setpointTemperatureCelsius,
            0.1,
            30,
            nameof(setpointTemperatureCelsius));
    }

    public double ReferenceCoefficientOfPerformance { get; }

    public CompressorType Compressor { get; }

    public CoolingTower CoolingTower { get; }

    public double? NominalCapacityWatts { get; }

    public double PumpMotorEfficiency { get; }

    public double SetpointTemperatureCelsius { get; }

    public override string IdfObjectType => Compressor == CompressorType.Screw
        ? "Chiller:Electric:ReformulatedEIR"
        : "Chiller:Electric:EIR";

    public override string IdfObjectName => $"Chiller_named_{Name}";

    internal static Chiller CreateLegacyDisabled(
        EntityId id,
        string name,
        CoolingTower coolingTower)
    {
        var chiller = new Chiller(
            id,
            name,
            1E-10,
            CompressorType.Turbo,
            coolingTower,
            1E-10);
        chiller._availabilityScheduleName = "ALLOFF";
        return chiller;
    }

    public override IReadOnlyList<IdfObject> ToIdfObjects(
        IdfGenerationContext context,
        IReadOnlyList<PlantDemandConnection>? demandConnections = null,
        IReadOnlyList<string>? terminalUnitNames = null)
    {
        DomainGuard.NotNull(context, nameof(context));
        var objects = new List<IdfObject>();
        objects.AddRange(CreatePerformanceCurves(context));
        objects.AddRange(CoolingTower.ToIdfObjects(context, this));

        IdfObject component = CreateMainComponent(context);
        objects.AddRange(CoolingPlantLoopAssembler.CreateCoolingLoop(
            context,
            this,
            component,
            ChilledWaterInletNodeName,
            ChilledWaterOutletNodeName,
            PumpMotorEfficiency,
            SetpointTemperatureCelsius,
            demandConnections ?? Array.Empty<PlantDemandConnection>(),
            _availabilityScheduleName));
        return objects;
    }

    internal string IdfObjectTypeFor(IdfGenerationContext context)
    {
        return context.Options.UseLegacySimpleDragonHvacTopology
            ? "Chiller:Electric:EIR"
            : IdfObjectType;
    }

    internal string ChilledWaterInletNodeName => $"{IdfObjectName} ChilledWater InletNode";

    internal string ChilledWaterOutletNodeName => $"{IdfObjectName} ChilledWater OutletNode";

    internal string CondenserInletNodeName => $"{IdfObjectName} Condenser InletNode";

    internal string CondenserOutletNodeName => $"{IdfObjectName} Condenser OutletNode";

    private IdfObject CreateMainComponent(IdfGenerationContext context)
    {
        if (Compressor == CompressorType.Screw
            && !context.Options.UseLegacySimpleDragonHvacTopology)
        {
            // EnergyPlus 24.2 exposes the pinned temperature-by-PLR surface through
            // the reformulated model; the standard Electric:EIR PLR field is univariate.
            return context.Create(
                IdfObjectType,
                IdfGenerationContext.Field(0, "Name", IdfObjectName),
                IdfGenerationContext.Field(1, "Reference Capacity", NominalCapacityWatts ?? (object)"autosize"),
                IdfGenerationContext.Field(2, "Reference COP", ReferenceCoefficientOfPerformance),
                IdfGenerationContext.Field(3, "Reference Leaving Chilled Water Temperature", SetpointTemperatureCelsius),
                IdfGenerationContext.Field(4, "Reference Leaving Condenser Water Temperature", 29),
                IdfGenerationContext.Field(5, "Reference Chilled Water Flow Rate", "autosize"),
                IdfGenerationContext.Field(6, "Reference Condenser Water Flow Rate", "autosize"),
                IdfGenerationContext.Field(7, "Cooling Capacity Function of Temperature Curve Name", Curve("CoolingCapaTemp")),
                IdfGenerationContext.Field(8, "Electric Input to Cooling Output Ratio Function of Temperature Curve Name", Curve("CoolingCOPTemp")),
                IdfGenerationContext.Field(9, "Electric Input to Cooling Output Ratio Function of Part Load Ratio Curve Type", "LeavingCondenserWaterTemperature"),
                IdfGenerationContext.Field(10, "Electric Input to Cooling Output Ratio Function of Part Load Ratio Curve Name", Curve("CoolingCOPPLR")),
                IdfGenerationContext.Field(11, "Minimum Part Load Ratio", 0.1),
                IdfGenerationContext.Field(12, "Maximum Part Load Ratio", 1),
                IdfGenerationContext.Field(13, "Optimum Part Load Ratio", 1),
                IdfGenerationContext.Field(14, "Minimum Unloading Ratio", 0.2),
                IdfGenerationContext.Field(15, "Chilled Water Inlet Node Name", ChilledWaterInletNodeName),
                IdfGenerationContext.Field(16, "Chilled Water Outlet Node Name", ChilledWaterOutletNodeName),
                IdfGenerationContext.Field(17, "Condenser Inlet Node Name", CondenserInletNodeName),
                IdfGenerationContext.Field(18, "Condenser Outlet Node Name", CondenserOutletNodeName),
                IdfGenerationContext.Field(19, "Fraction of Compressor Electric Consumption Rejected by Condenser", 1),
                IdfGenerationContext.Field(20, "Leaving Chilled Water Lower Temperature Limit", 2),
                IdfGenerationContext.Field(21, "Chiller Flow Mode Type", "NotModulated"));
        }

        string objectType = IdfObjectTypeFor(context);
        object referenceChilledWaterTemperature = context.Options.UseLegacySimpleDragonHvacTopology
            ? 6.67
            : SetpointTemperatureCelsius;
        object referenceCondenserTemperature = context.Options.UseLegacySimpleDragonHvacTopology
            ? 29.4
            : 29;
        return context.Create(
            objectType,
            IdfGenerationContext.Field(0, "Name", IdfObjectName),
            IdfGenerationContext.Field(1, "Reference Capacity", NominalCapacityWatts ?? (object)"autosize"),
            IdfGenerationContext.Field(2, "Reference COP", ReferenceCoefficientOfPerformance),
            IdfGenerationContext.Field(3, "Reference Leaving Chilled Water Temperature", referenceChilledWaterTemperature),
            IdfGenerationContext.Field(4, "Reference Entering Condenser Fluid Temperature", referenceCondenserTemperature),
            IdfGenerationContext.Field(5, "Reference Chilled Water Flow Rate", "autosize"),
            IdfGenerationContext.Field(6, "Reference Condenser Fluid Flow Rate", "autosize"),
            IdfGenerationContext.Field(7, "Cooling Capacity Function of Temperature Curve Name", Curve("CoolingCapaTemp")),
            IdfGenerationContext.Field(8, "Electric Input to Cooling Output Ratio Function of Temperature Curve Name", Curve("CoolingCOPTemp")),
            IdfGenerationContext.Field(9, "Electric Input to Cooling Output Ratio Function of Part Load Ratio Curve Name", Curve("CoolingCOPPLR")),
            IdfGenerationContext.Field(10, "Minimum Part Load Ratio", 0.1),
            IdfGenerationContext.Field(11, "Maximum Part Load Ratio", 1),
            IdfGenerationContext.Field(12, "Optimum Part Load Ratio", 1),
            IdfGenerationContext.Field(13, "Minimum Unloading Ratio", 0.2),
            IdfGenerationContext.Field(14, "Chilled Water Inlet Node Name", ChilledWaterInletNodeName),
            IdfGenerationContext.Field(15, "Chilled Water Outlet Node Name", ChilledWaterOutletNodeName),
            IdfGenerationContext.Field(16, "Condenser Inlet Node Name", CondenserInletNodeName),
            IdfGenerationContext.Field(17, "Condenser Outlet Node Name", CondenserOutletNodeName),
            IdfGenerationContext.Field(18, "Condenser Type", "WaterCooled"),
            IdfGenerationContext.Field(20, "Fraction of Compressor Electric Consumption Rejected by Condenser", 1),
            IdfGenerationContext.Field(21, "Leaving Chilled Water Lower Temperature Limit", 2),
            IdfGenerationContext.Field(22, "Chiller Flow Mode", "NotModulated"));
    }

    private IEnumerable<IdfObject> CreatePerformanceCurves(IdfGenerationContext context)
    {
        switch (Compressor)
        {
            case CompressorType.Turbo:
                yield return Biquadratic(
                    context,
                    "CoolingCapaTemp",
                    0.257183345,
                    0.038794102,
                    -0.00021648,
                    0.046738887,
                    -0.000940235,
                    -0.000342491,
                    5,
                    10,
                    24,
                    35);
                yield return Biquadratic(
                    context,
                    "CoolingCOPTemp",
                    0.933678591,
                    -0.058199196,
                    0.00449937,
                    0.002429466,
                    0.000485893,
                    -0.001214733,
                    5,
                    10,
                    24,
                    35);
                yield return Quadratic(
                    context,
                    "CoolingCOPPLR",
                    0.222903,
                    0.313387,
                    0.46371,
                    0,
                    1);
                break;

            case CompressorType.Screw:
                yield return Biquadratic(
                    context,
                    "CoolingCapaTemp",
                    0.907133913,
                    0.029260566,
                    -0.00036511,
                    -0.000971992,
                    -0.0000906018,
                    0.000252984,
                    0,
                    10,
                    0,
                    50);
                yield return Biquadratic(
                    context,
                    "CoolingCOPTemp",
                    0.392011698,
                    -0.024908656,
                    -0.001031643,
                    0.01429376,
                    0.000406631,
                    -0.000765035,
                    0,
                    20,
                    0,
                    50);
                yield return Bicubic(
                    context,
                    "CoolingCOPPLR",
                    0.044612112,
                    0.023594163,
                    0.0000619872,
                    -0.353684198,
                    1.797965254,
                    -0.0272333223,
                    0,
                    -0.467387755,
                    0,
                    0,
                    14.56,
                    34.97,
                    0.18,
                    1.03);
                break;

            case CompressorType.Reciprocating:
                yield return Biquadratic(
                    context,
                    "CoolingCapaTemp",
                    0.9441897,
                    0.03371079,
                    0.00009756685,
                    -0.003220573,
                    -0.00004917369,
                    -0.0001775717,
                    5.56,
                    10,
                    23.89,
                    35);
                yield return Biquadratic(
                    context,
                    "CoolingCOPTemp",
                    0.727387,
                    -0.01189276,
                    0.0005411677,
                    0.001879294,
                    0.0004734664,
                    -0.000711485,
                    5.56,
                    10,
                    23.89,
                    35);
                yield return Quadratic(
                    context,
                    "CoolingCOPPLR",
                    0.04146742,
                    0.6543795,
                    0.3044125,
                    0.25,
                    1.01);
                break;

            default:
                throw new InvalidOperationException($"Unsupported compressor family '{Compressor}'.");
        }
    }

    private IdfObject Biquadratic(
        IdfGenerationContext context,
        string suffix,
        params object?[] values) => context.CreateRaw(
            "Curve:Biquadratic",
            new object?[] { Curve(suffix) }.Concat(values).ToArray());

    private IdfObject Bicubic(
        IdfGenerationContext context,
        string suffix,
        params object?[] values) => context.CreateRaw(
            "Curve:Bicubic",
            new object?[] { Curve(suffix) }.Concat(values).ToArray());

    private IdfObject Quadratic(
        IdfGenerationContext context,
        string suffix,
        params object?[] values) => context.CreateRaw(
            "Curve:Quadratic",
            new object?[] { Curve(suffix) }.Concat(values).ToArray());

    private string Curve(string suffix) => $"Curve_for_{IdfObjectName}:{suffix}";
}

/// <summary>
/// A hot-water-fired absorption chiller with chilled-, condenser-, and generator-water loops.
/// </summary>
public sealed class AbsorptionChiller : SourceSystem
{
    public AbsorptionChiller(
        EntityId id,
        string name,
        double thermalCoefficientOfPerformance,
        Boiler heatSource,
        CoolingTower coolingTower,
        double? nominalCapacityWatts = null,
        double pumpMotorEfficiency = 0.9,
        double setpointTemperatureCelsius = 6)
        : base(id, name)
    {
        HeatSource = heatSource ?? throw new ArgumentNullException(nameof(heatSource));
        CoolingTower = coolingTower ?? throw new ArgumentNullException(nameof(coolingTower));
        if (HeatSource.Id.Equals(id) || CoolingTower.Id.Equals(id) || HeatSource.Id.Equals(CoolingTower.Id))
        {
            throw new ArgumentException(
                "An absorption chiller, heat source, and cooling tower require distinct identifiers.");
        }

        ThermalCoefficientOfPerformance = DomainGuard.Positive(
            thermalCoefficientOfPerformance,
            nameof(thermalCoefficientOfPerformance));
        NominalCapacityWatts = nominalCapacityWatts is null
            ? null
            : DomainGuard.Positive(nominalCapacityWatts.Value, nameof(nominalCapacityWatts));
        PumpMotorEfficiency = DomainGuard.InRange(
            pumpMotorEfficiency,
            0.000001,
            1,
            nameof(pumpMotorEfficiency));
        SetpointTemperatureCelsius = DomainGuard.InRange(
            setpointTemperatureCelsius,
            0.1,
            30,
            nameof(setpointTemperatureCelsius));
    }

    public double ThermalCoefficientOfPerformance { get; }

    public Boiler HeatSource { get; }

    public Fuel GeneratorFuel => HeatSource.Fuel;

    public CoolingTower CoolingTower { get; }

    public double? NominalCapacityWatts { get; }

    public double PumpMotorEfficiency { get; }

    public double SetpointTemperatureCelsius { get; }

    public override string IdfObjectType => "Chiller:Absorption";

    public override string IdfObjectName => $"AbsorptionChiller_named_{Name}";

    public override IReadOnlyList<IdfObject> ToIdfObjects(
        IdfGenerationContext context,
        IReadOnlyList<PlantDemandConnection>? demandConnections = null,
        IReadOnlyList<string>? terminalUnitNames = null)
    {
        DomainGuard.NotNull(context, nameof(context));
        IdfObject component = context.Create(
            IdfObjectType,
            IdfGenerationContext.Field(0, "Name", IdfObjectName),
            IdfGenerationContext.Field(1, "Nominal Capacity", NominalCapacityWatts ?? (object)"autosize"),
            IdfGenerationContext.Field(2, "Nominal Pumping Power", "autosize"),
            IdfGenerationContext.Field(3, "Chilled Water Inlet Node Name", ChilledWaterInletNodeName),
            IdfGenerationContext.Field(4, "Chilled Water Outlet Node Name", ChilledWaterOutletNodeName),
            IdfGenerationContext.Field(5, "Condenser Inlet Node Name", CondenserInletNodeName),
            IdfGenerationContext.Field(6, "Condenser Outlet Node Name", CondenserOutletNodeName),
            IdfGenerationContext.Field(7, "Minimum Part Load Ratio", 0.15),
            IdfGenerationContext.Field(8, "Maximum Part Load Ratio", 1),
            IdfGenerationContext.Field(9, "Optimum Part Load Ratio", 0.65),
            IdfGenerationContext.Field(10, "Design Condenser Inlet Temperature", 35),
            IdfGenerationContext.Field(11, "Design Chilled Water Flow Rate", "autosize"),
            IdfGenerationContext.Field(12, "Design Condenser Water Flow Rate", "autosize"),
            IdfGenerationContext.Field(13, "Coefficient 1 of the Hot Water or Steam Use Part Load Ratio Curve", 0.03303 / ThermalCoefficientOfPerformance),
            IdfGenerationContext.Field(14, "Coefficient 2 of the Hot Water or Steam Use Part Load Ratio Curve", 0.6852 / ThermalCoefficientOfPerformance),
            IdfGenerationContext.Field(15, "Coefficient 3 of the Hot Water or Steam Use Part Load Ratio Curve", 0.2818 / ThermalCoefficientOfPerformance),
            IdfGenerationContext.Field(16, "Coefficient 1 of the Pump Electric Use Part Load Ratio Curve", 1),
            IdfGenerationContext.Field(17, "Coefficient 2 of the Pump Electric Use Part Load Ratio Curve", 0),
            IdfGenerationContext.Field(18, "Coefficient 3 of the Pump Electric Use Part Load Ratio Curve", 0),
            IdfGenerationContext.Field(19, "Chilled Water Outlet Temperature Lower Limit", 5),
            IdfGenerationContext.Field(20, "Generator Inlet Node Name", GeneratorInletNodeName),
            IdfGenerationContext.Field(21, "Generator Outlet Node Name", GeneratorOutletNodeName),
            IdfGenerationContext.Field(22, "Chiller Flow Mode", "NotModulated"),
            IdfGenerationContext.Field(23, "Generator Heat Source Type", "HotWater"),
            IdfGenerationContext.Field(24, "Design Generator Fluid Flow Rate", "autosize"));

        var objects = new List<IdfObject>();
        objects.AddRange(CoolingPlantLoopAssembler.CreateCoolingLoop(
            context,
            this,
            component,
            ChilledWaterInletNodeName,
            ChilledWaterOutletNodeName,
            PumpMotorEfficiency,
            SetpointTemperatureCelsius,
            demandConnections ?? Array.Empty<PlantDemandConnection>()));
        objects.AddRange(CoolingTower.ToIdfObjects(context, this));
        string generatorBranchName = context.Options.UseLegacySimpleDragonHvacTopology
            ? $"{HeatSource.LoopName} Demand MainGenerator"
            : $"{HeatSource.LoopName} Demand MainGenerator_for_{IdfObjectName}";
        var generatorConnection = new PlantDemandConnection(
            generatorBranchName,
            IdfObjectType,
            IdfObjectName,
            GeneratorInletNodeName,
            GeneratorOutletNodeName);
        objects.AddRange(HeatSource.ToIdfObjects(context, new[] { generatorConnection }));
        return objects;
    }

    internal string ChilledWaterInletNodeName => $"{IdfObjectName} ChilledWater InletNode";

    internal string ChilledWaterOutletNodeName => $"{IdfObjectName} ChilledWater OutletNode";

    internal string CondenserInletNodeName => $"{IdfObjectName} Condenser InletNode";

    internal string CondenserOutletNodeName => $"{IdfObjectName} Condenser OutletNode";

    internal string GeneratorInletNodeName => $"{IdfObjectName} Generator InletNode";

    internal string GeneratorOutletNodeName => $"{IdfObjectName} Generator OutletNode";
}
