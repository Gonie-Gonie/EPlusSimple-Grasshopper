using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Construction;

/// <summary>
/// A massless opaque construction defined directly by U-value.
/// </summary>
public sealed record NoMassConstruction : ISurfaceConstruction
{
    public NoMassConstruction(string name, double uValueWattsPerSquareMetreKelvin)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        UValueWattsPerSquareMetreKelvin = DomainGuard.Positive(
            uValueWattsPerSquareMetreKelvin,
            nameof(uValueWattsPerSquareMetreKelvin));
    }

    public string Name { get; }

    public double UValueWattsPerSquareMetreKelvin { get; }

    public double ThermalResistance => 1 / UValueWattsPerSquareMetreKelvin;
}

/// <summary>
/// A simple glazing system defined by U-value and solar heat-gain coefficient.
/// </summary>
public sealed record Glazing
{
    public Glazing(
        string name,
        double uValueWattsPerSquareMetreKelvin,
        double solarHeatGainCoefficient)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        UValueWattsPerSquareMetreKelvin = DomainGuard.Positive(
            uValueWattsPerSquareMetreKelvin,
            nameof(uValueWattsPerSquareMetreKelvin));
        SolarHeatGainCoefficient = DomainGuard.InRange(
            solarHeatGainCoefficient,
            0,
            1,
            nameof(solarHeatGainCoefficient));
    }

    public string Name { get; }

    public double UValueWattsPerSquareMetreKelvin { get; }

    public double SolarHeatGainCoefficient { get; }
}

/// <summary>
/// A zone-mixing air boundary construction.
/// </summary>
public sealed record AirBoundary : ISurfaceConstruction
{
    public AirBoundary(string name, double airChangesPerHour = 0.5)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        AirChangesPerHour = DomainGuard.NonNegative(airChangesPerHour, nameof(airChangesPerHour));
    }

    public string Name { get; }

    public double AirChangesPerHour { get; }
}
