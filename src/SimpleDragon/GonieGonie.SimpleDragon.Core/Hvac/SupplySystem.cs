using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

public enum SupplySystemType
{
    PackagedAirConditioner,
    AirHandlingUnit,
    FanCoilUnit,
    Radiator,
    ElectricRadiator,
    RadiantFloor,
    ElectricRadiantFloor,
}

/// <summary>
/// Simplified zone-side HVAC system covering every GRM 0.7 supply type.
/// </summary>
public sealed class SupplySystem
{
    private readonly HashSet<string> _grmFields;

    public SupplySystem(
        string name,
        SupplySystemType type,
        string? sourceSystemId = null,
        SourceSystem? sourceSystem = null,
        double? coolingCop = null,
        double? coolingCapacity = null,
        double? heatingCapacity = null,
        EntityId? id = null,
        IEnumerable<string>? grmFields = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        if (!Enum.IsDefined(typeof(SupplySystemType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown supply-system type.");
        }

        Type = type;
        SourceSystemId = sourceSystemId is null || string.IsNullOrWhiteSpace(sourceSystemId)
            ? null
            : sourceSystemId.Trim();
        SourceSystem = sourceSystem;
        if (sourceSystem is not null
            && !StringComparer.Ordinal.Equals(SourceSystemId, sourceSystem!.Id.Value))
        {
            throw new ArgumentException("Source-system ID does not match the resolved source.", nameof(sourceSystemId));
        }

        bool requiresSource = type == SupplySystemType.AirHandlingUnit
            || type == SupplySystemType.FanCoilUnit
            || type == SupplySystemType.Radiator
            || type == SupplySystemType.RadiantFloor;
        if (requiresSource && SourceSystemId is null)
        {
            throw new ArgumentException("The selected supply-system type requires a source-system ID.", nameof(sourceSystemId));
        }

        if (!requiresSource && SourceSystemId is not null)
        {
            throw new ArgumentException("The selected supply-system type does not use a source system.", nameof(sourceSystemId));
        }

        if (sourceSystem is not null && !AcceptsSource(type, sourceSystem.Type))
        {
            throw new ArgumentException(
                "The selected supply-system type is incompatible with the resolved source-system type.",
                nameof(sourceSystem));
        }

        CoolingCop = PositiveOrNull(coolingCop, nameof(coolingCop));
        CoolingCapacity = PositiveOrNull(coolingCapacity, nameof(coolingCapacity));
        HeatingCapacity = PositiveOrNull(heatingCapacity, nameof(heatingCapacity));
        Id = id ?? DeterministicDomainId.Create(
            "SUPL",
            Name,
            Type,
            SourceSystemId,
            CoolingCop,
            CoolingCapacity,
            HeatingCapacity);
        _grmFields = grmFields is null
            ? CreateDefaultFields()
            : new HashSet<string>(grmFields, StringComparer.Ordinal);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public SupplySystemType Type { get; }

    public string? SourceSystemId { get; }

    public SourceSystem? SourceSystem { get; }

    public double? CoolingCop { get; }

    public double? CoolingCapacity { get; }

    public double? HeatingCapacity { get; }

    public bool Heatable => Type switch
    {
        SupplySystemType.ElectricRadiator => true,
        SupplySystemType.ElectricRadiantFloor => true,
        SupplySystemType.AirHandlingUnit => IsSource(SourceSystemType.HeatPump, SourceSystemType.GeothermalHeatPump),
        SupplySystemType.FanCoilUnit => IsSource(SourceSystemType.Boiler, SourceSystemType.DistrictHeating),
        SupplySystemType.Radiator => IsSource(SourceSystemType.Boiler, SourceSystemType.DistrictHeating),
        SupplySystemType.RadiantFloor => IsSource(SourceSystemType.Boiler, SourceSystemType.DistrictHeating),
        _ => false,
    };

    public bool Coolable => Type switch
    {
        SupplySystemType.PackagedAirConditioner => true,
        SupplySystemType.AirHandlingUnit => IsSource(SourceSystemType.HeatPump, SourceSystemType.GeothermalHeatPump),
        SupplySystemType.FanCoilUnit => IsSource(SourceSystemType.Chiller, SourceSystemType.AbsorptionChiller),
        _ => false,
    };

    internal bool HasGrmField(string name)
    {
        return _grmFields.Contains(name);
    }

    private bool IsSource(SourceSystemType first, SourceSystemType second)
    {
        return SourceSystem is not null
            && (SourceSystem.Type == first || SourceSystem.Type == second);
    }

    private static bool AcceptsSource(SupplySystemType supplyType, SourceSystemType sourceType)
    {
        return supplyType switch
        {
            SupplySystemType.AirHandlingUnit => sourceType == SourceSystemType.HeatPump
                || sourceType == SourceSystemType.GeothermalHeatPump,
            SupplySystemType.FanCoilUnit => sourceType == SourceSystemType.Boiler
                || sourceType == SourceSystemType.DistrictHeating
                || sourceType == SourceSystemType.Chiller
                || sourceType == SourceSystemType.AbsorptionChiller,
            SupplySystemType.Radiator => sourceType == SourceSystemType.Boiler
                || sourceType == SourceSystemType.DistrictHeating,
            SupplySystemType.RadiantFloor => sourceType == SourceSystemType.Boiler
                || sourceType == SourceSystemType.DistrictHeating,
            _ => false,
        };
    }

    private HashSet<string> CreateDefaultFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        if (SourceSystemId is not null)
        {
            fields.Add("source_system_id");
        }

        if (CoolingCop.HasValue)
        {
            fields.Add("cop_cooling");
        }

        if (CoolingCapacity.HasValue)
        {
            fields.Add("capacity_cooling");
        }

        if (HeatingCapacity.HasValue)
        {
            fields.Add("capacity_heating");
        }

        return fields;
    }

    private static double? PositiveOrNull(double? value, string parameterName)
    {
        return value.HasValue
            ? DomainSupport.FinitePositive(value.Value, parameterName)
            : null;
    }
}
