using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

public enum FuelType
{
    Electricity,
    NaturalGas,
    LiquefiedPetroleumGas,
    Oil,
    DistrictHeating,
}

public enum SourceSystemType
{
    HeatPump,
    GeothermalHeatPump,
    Chiller,
    AbsorptionChiller,
    Boiler,
    DistrictHeating,
}

public enum CompressorType
{
    Turbo,
    Screw,
    Reciprocating,
}

public enum CoolingTowerType
{
    Closed,
    Open,
}

public enum CoolingTowerControl
{
    SingleSpeed,
    TwoSpeed,
}

/// <summary>
/// Simplified source-side HVAC system covering every GRM 0.7 source type.
/// </summary>
public sealed class SourceSystem
{
    private readonly HashSet<string> _grmFields;

    public SourceSystem(
        string name,
        SourceSystemType type,
        FuelType? fuelType = null,
        double? heatingCop = null,
        double? coolingCop = null,
        double? heatingCapacity = null,
        double? coolingCapacity = null,
        double? efficiency = null,
        bool? hotWaterSupply = null,
        CompressorType? compressorType = null,
        CoolingTowerType? coolingTowerType = null,
        double? coolingTowerCapacity = null,
        CoolingTowerControl? coolingTowerControl = null,
        double? boilerEfficiency = null,
        EntityId? id = null,
        IEnumerable<string>? grmFields = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        if (!Enum.IsDefined(typeof(SourceSystemType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown source-system type.");
        }

        Type = type;
        FuelType = DomainSupport.DefinedEnumOrNull(fuelType, nameof(fuelType));
        CompressorType = DomainSupport.DefinedEnumOrNull(compressorType, nameof(compressorType));
        CoolingTowerType = DomainSupport.DefinedEnumOrNull(coolingTowerType, nameof(coolingTowerType));
        CoolingTowerControl = DomainSupport.DefinedEnumOrNull(
            coolingTowerControl,
            nameof(coolingTowerControl));
        HeatingCop = PositiveOrNull(heatingCop, nameof(heatingCop));
        CoolingCop = PositiveOrNull(coolingCop, nameof(coolingCop));
        HeatingCapacity = PositiveOrNull(heatingCapacity, nameof(heatingCapacity));
        CoolingCapacity = PositiveOrNull(coolingCapacity, nameof(coolingCapacity));
        Efficiency = FractionOrNull(efficiency, nameof(efficiency));
        HotWaterSupply = hotWaterSupply;
        CoolingTowerCapacity = PositiveOrNull(coolingTowerCapacity, nameof(coolingTowerCapacity));
        BoilerEfficiency = FractionOrNull(boilerEfficiency, nameof(boilerEfficiency));
        ValidateRequiredValues();
        Id = id ?? DeterministicDomainId.Create(
            "SRCE",
            Name,
            Type,
            FuelType,
            HeatingCop,
            CoolingCop,
            HeatingCapacity,
            CoolingCapacity,
            Efficiency,
            HotWaterSupply,
            CompressorType,
            CoolingTowerType,
            CoolingTowerCapacity,
            CoolingTowerControl,
            BoilerEfficiency);
        _grmFields = grmFields is null
            ? CreateDefaultFields()
            : new HashSet<string>(grmFields, StringComparer.Ordinal);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public SourceSystemType Type { get; }

    public FuelType? FuelType { get; }

    public double? HeatingCop { get; }

    public double? CoolingCop { get; }

    public double? HeatingCapacity { get; }

    public double? CoolingCapacity { get; }

    public double? Efficiency { get; }

    public bool? HotWaterSupply { get; }

    public CompressorType? CompressorType { get; }

    public CoolingTowerType? CoolingTowerType { get; }

    public double? CoolingTowerCapacity { get; }

    public CoolingTowerControl? CoolingTowerControl { get; }

    public double? BoilerEfficiency { get; }

    internal IReadOnlyCollection<string> GrmFields => _grmFields;

    internal bool HasGrmField(string name)
    {
        return _grmFields.Contains(name);
    }

    private void ValidateRequiredValues()
    {
        if ((Type == SourceSystemType.HeatPump
             || Type == SourceSystemType.GeothermalHeatPump
             || Type == SourceSystemType.AbsorptionChiller
             || Type == SourceSystemType.Boiler)
            && !FuelType.HasValue)
        {
            throw new ArgumentException("The selected source-system type requires a fuel type.", nameof(FuelType));
        }

        if (Type == SourceSystemType.Chiller
            && (!CompressorType.HasValue || !CoolingTowerType.HasValue || !CoolingTowerControl.HasValue))
        {
            throw new ArgumentException("A chiller requires compressor and cooling-tower types and control.");
        }

        if ((Type == SourceSystemType.Boiler || Type == SourceSystemType.DistrictHeating)
            && !HotWaterSupply.HasValue)
        {
            throw new ArgumentException("A boiler or district-heating source requires hot-water-supply status.");
        }
    }

    private HashSet<string> CreateDefaultFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        AddIfValue(fields, "fuel_type", FuelType);
        AddIfValue(fields, "cop_heating", HeatingCop);
        AddIfValue(fields, "cop_cooling", CoolingCop);
        AddIfValue(fields, "capacity_heating", HeatingCapacity);
        AddIfValue(fields, "capacity_cooling", CoolingCapacity);
        AddIfValue(fields, "efficiency", Efficiency);
        AddIfValue(fields, "hotwater_supply", HotWaterSupply);
        AddIfValue(fields, "compressor_type", CompressorType);
        AddIfValue(fields, "coolingtower_type", CoolingTowerType);
        AddIfValue(fields, "coolingtower_capacity", CoolingTowerCapacity);
        AddIfValue(fields, "coolingtower_control", CoolingTowerControl);
        AddIfValue(fields, "boiler_efficiency", BoilerEfficiency);
        return fields;
    }

    private static void AddIfValue(HashSet<string> fields, string name, object? value)
    {
        if (value is not null)
        {
            fields.Add(name);
        }
    }

    private static double? PositiveOrNull(double? value, string parameterName)
    {
        return value.HasValue
            ? DomainSupport.FinitePositive(value.Value, parameterName)
            : null;
    }

    private static double? FractionOrNull(double? value, string parameterName)
    {
        if (value.HasValue
            && (double.IsNaN(value.Value)
                || double.IsInfinity(value.Value)
                || value.Value <= 0d
                || value.Value > 1d))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A fraction must be in (0, 1].");
        }

        return value;
    }
}
