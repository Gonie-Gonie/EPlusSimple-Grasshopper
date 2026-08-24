using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Construction;

/// <summary>
/// Immutable thermophysical properties of a homogeneous opaque material.
/// </summary>
public sealed record Material
{
    public Material(
        string name,
        double conductivityWattsPerMetreKelvin,
        double densityKilogramsPerCubicMetre,
        double specificHeatJoulesPerKilogramKelvin,
        double thermalAbsorptance = 0.9,
        double solarAbsorptance = 0.7,
        double visibleAbsorptance = 0.7,
        MaterialRoughness roughness = MaterialRoughness.Rough)
    {
        if (!Enum.IsDefined(typeof(MaterialRoughness), roughness))
        {
            throw new ArgumentOutOfRangeException(nameof(roughness), roughness, "Unknown material roughness.");
        }

        Name = DomainGuard.RequiredText(name, nameof(name));
        ConductivityWattsPerMetreKelvin = DomainGuard.Positive(
            conductivityWattsPerMetreKelvin,
            nameof(conductivityWattsPerMetreKelvin));
        DensityKilogramsPerCubicMetre = DomainGuard.Positive(
            densityKilogramsPerCubicMetre,
            nameof(densityKilogramsPerCubicMetre));
        if (DomainGuard.Finite(specificHeatJoulesPerKilogramKelvin, nameof(specificHeatJoulesPerKilogramKelvin)) < 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(specificHeatJoulesPerKilogramKelvin),
                specificHeatJoulesPerKilogramKelvin,
                "Specific heat must be at least 100 J/(kg K).");
        }

        SpecificHeatJoulesPerKilogramKelvin = specificHeatJoulesPerKilogramKelvin;
        ThermalAbsorptance = DomainGuard.InRange(thermalAbsorptance, 0, 1, nameof(thermalAbsorptance));
        SolarAbsorptance = DomainGuard.InRange(solarAbsorptance, 0, 1, nameof(solarAbsorptance));
        VisibleAbsorptance = DomainGuard.InRange(visibleAbsorptance, 0, 1, nameof(visibleAbsorptance));
        Roughness = roughness;
    }

    public string Name { get; }

    public double ConductivityWattsPerMetreKelvin { get; }

    public double DensityKilogramsPerCubicMetre { get; }

    public double SpecificHeatJoulesPerKilogramKelvin { get; }

    public double ThermalAbsorptance { get; }

    public double SolarAbsorptance { get; }

    public double VisibleAbsorptance { get; }

    public MaterialRoughness Roughness { get; }
}
