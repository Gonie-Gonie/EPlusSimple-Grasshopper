using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Construction;

/// <summary>
/// A material layer with thickness in metres.
/// </summary>
public sealed record Layer
{
    public Layer(string name, Material material, double thicknessMetres)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        Material = material ?? throw new ArgumentNullException(nameof(material));
        ThicknessMetres = DomainGuard.Positive(thicknessMetres, nameof(thicknessMetres));
    }

    public string Name { get; }

    public Material Material { get; }

    public double ThicknessMetres { get; }

    /// <summary>
    /// Gets conductance in W/(m2 K), excluding surface films.
    /// </summary>
    public double UValue => Material.ConductivityWattsPerMetreKelvin / ThicknessMetres;

    /// <summary>
    /// Gets thermal resistance in (m2 K)/W.
    /// </summary>
    public double ThermalResistance => ThicknessMetres / Material.ConductivityWattsPerMetreKelvin;

    /// <summary>
    /// Gets areal heat capacity in J/(m2 K).
    /// </summary>
    public double HeatCapacityJoulesPerSquareMetreKelvin =>
        Material.SpecificHeatJoulesPerKilogramKelvin
        * Material.DensityKilogramsPerCubicMetre
        * ThicknessMetres;

    /// <summary>
    /// Compares the material and thickness used by InvisibleDragon 0.7.0.
    /// The descriptive layer name does not participate in equality.
    /// </summary>
    public bool Equals(Layer? other)
    {
        return other is not null
            && Material.Equals(other.Material)
            && ThicknessMetres.Equals(other.ThicknessMetres);
    }

    /// <summary>
    /// Returns the name-only hash used by InvisibleDragon 0.7.0.
    /// </summary>
    /// <remarks>
    /// The upstream behavior intentionally differs from the fields used by
    /// equality and is retained for public-behavior compatibility.
    /// </remarks>
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Name);
    }
}
