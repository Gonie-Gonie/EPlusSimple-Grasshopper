using System.Collections.ObjectModel;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Construction;

/// <summary>
/// An ordered outside-to-inside collection of opaque material layers.
/// </summary>
public sealed class Construction : ISurfaceConstruction, IEquatable<Construction>
{
    public Construction(string name, IEnumerable<Layer> layers)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        Layer[] copy = DomainGuard.CopyRequired(layers, nameof(layers));
        if (copy.Length == 0)
        {
            throw new ArgumentException("A construction requires at least one layer.", nameof(layers));
        }

        Layers = new ReadOnlyCollection<Layer>(copy);
    }

    public string Name { get; }

    public IReadOnlyList<Layer> Layers { get; }

    public double ThicknessMetres => Layers.Sum(layer => layer.ThicknessMetres);

    public double ThermalResistance => Layers.Sum(layer => layer.ThermalResistance);

    public double UValue => 1 / Layers.Sum(layer => 1 / layer.UValue);

    public double HeatCapacityJoulesPerSquareMetreKelvin =>
        Layers.Sum(layer => layer.HeatCapacityJoulesPerSquareMetreKelvin);

    public Construction Reverse(string? name = null)
    {
        return new Construction(name ?? $"{Name}_reversed", Layers.Reverse());
    }

    public bool Equals(Construction? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && Layers.SequenceEqual(other.Layers);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Construction);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Name);
    }
}
