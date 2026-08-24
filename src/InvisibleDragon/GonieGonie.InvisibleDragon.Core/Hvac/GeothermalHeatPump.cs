using GonieGonie.BuildingEnergy.Contracts;

namespace GonieGonie.InvisibleDragon.Hvac;

/// <summary>
/// Preserves the geothermal heat-pump domain identity exposed by the pinned 0.7 source.
/// </summary>
/// <remarks>
/// The pinned implementation does not define a separate EnergyPlus object model for this
/// identity, and SimpleDragon historically routes it through the regular heat-pump path.
/// This type therefore reuses <see cref="HeatPump"/> IDF behavior without duplicating it.
/// </remarks>
public sealed class GeothermalHeatPump : HeatPump
{
    public GeothermalHeatPump(
        EntityId id,
        string name,
        Fuel fuel,
        double heatingCoefficientOfPerformance,
        double coolingCoefficientOfPerformance,
        double? heatingCapacityWatts = null,
        double? coolingCapacityWatts = null)
        : base(
            id,
            name,
            fuel,
            heatingCoefficientOfPerformance,
            coolingCoefficientOfPerformance,
            heatingCapacityWatts,
            coolingCapacityWatts)
    {
    }
}
