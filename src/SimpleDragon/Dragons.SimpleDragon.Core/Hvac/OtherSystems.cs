using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

public sealed class VentilationSystem
{
    public VentilationSystem(
        string name,
        double airflowRate,
        double heatingEfficiency = 0.7d,
        double coolingEfficiency = 0.45d,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        AirflowRate = DomainSupport.FinitePositive(airflowRate, nameof(airflowRate));
        HeatingEfficiency = ValidateEfficiency(heatingEfficiency, nameof(heatingEfficiency));
        CoolingEfficiency = ValidateEfficiency(coolingEfficiency, nameof(coolingEfficiency));
        Id = id ?? DeterministicDomainId.Create(
            "ERVT",
            Name,
            AirflowRate,
            HeatingEfficiency,
            CoolingEfficiency);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public double AirflowRate { get; }

    public double HeatingEfficiency { get; }

    public double CoolingEfficiency { get; }

    private static double ValidateEfficiency(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d || value >= 1d)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Efficiency must be in (0, 1). ");
        }

        return value;
    }
}

public sealed class VentilationAssignment
{
    public VentilationAssignment(
        string ventilationSystemId,
        int count,
        VentilationSystem? ventilationSystem = null)
    {
        VentilationSystemId = DomainSupport.RequiredText(
            ventilationSystemId,
            nameof(ventilationSystemId));
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Ventilation count must be positive.");
        }

        Count = count;
        VentilationSystem = ventilationSystem;
        if (ventilationSystem is not null
            && !StringComparer.Ordinal.Equals(VentilationSystemId, ventilationSystem.Id.Value))
        {
            throw new ArgumentException(
                "Ventilation-system ID does not match the resolved system.",
                nameof(ventilationSystemId));
        }
    }

    public string VentilationSystemId { get; }

    public int Count { get; }

    public VentilationSystem? VentilationSystem { get; }
}

public sealed class PhotovoltaicSystem
{
    public PhotovoltaicSystem(
        string name,
        double area,
        double efficiency,
        double azimuth,
        double tilt,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        Area = DomainSupport.FinitePositive(area, nameof(area));
        if (double.IsNaN(efficiency) || double.IsInfinity(efficiency) || efficiency <= 0d || efficiency > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(efficiency), efficiency, "Efficiency must be in (0, 1].");
        }

        if (double.IsNaN(azimuth) || double.IsInfinity(azimuth) || azimuth < 0d || azimuth >= 360d)
        {
            throw new ArgumentOutOfRangeException(nameof(azimuth), azimuth, "Azimuth must be in [0, 360). ");
        }

        if (double.IsNaN(tilt) || double.IsInfinity(tilt) || tilt < 0d || tilt > 90d)
        {
            throw new ArgumentOutOfRangeException(nameof(tilt), tilt, "Tilt must be in [0, 90].");
        }

        Efficiency = efficiency;
        Azimuth = azimuth;
        Tilt = tilt;
        Id = id ?? DeterministicDomainId.Create("PVPN", Name, Area, Efficiency, Azimuth, Tilt);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public double Area { get; }

    public double Efficiency { get; }

    public double Azimuth { get; }

    public double Tilt { get; }
}
