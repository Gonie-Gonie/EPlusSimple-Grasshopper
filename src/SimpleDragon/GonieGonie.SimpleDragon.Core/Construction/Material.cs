using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// An opaque material with thermal properties matching the SimpleDragon input model.
/// </summary>
public sealed class Material : IEquatable<Material>
{
    public Material(
        string name,
        double conductivity,
        double density,
        double specificHeat,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        Conductivity = DomainSupport.FinitePositive(conductivity, nameof(conductivity));
        Density = DomainSupport.FinitePositive(density, nameof(density));
        SpecificHeat = DomainSupport.FinitePositive(specificHeat, nameof(specificHeat));
        if (SpecificHeat < 100d)
        {
            throw new ArgumentOutOfRangeException(nameof(specificHeat), specificHeat, "Specific heat must be at least 100 J/kgK.");
        }

        Id = id ?? DeterministicDomainId.Create(
            "MTRL",
            Name,
            Conductivity,
            Density,
            SpecificHeat);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public double Conductivity { get; }

    public double Density { get; }

    public double SpecificHeat { get; }

    public bool Equals(Material? other)
    {
        return other is not null
            && Conductivity.Equals(other.Conductivity)
            && Density.Equals(other.Density)
            && SpecificHeat.Equals(other.SpecificHeat);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Material);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Conductivity.GetHashCode();
            hash = (hash * 397) ^ Density.GetHashCode();
            return (hash * 397) ^ SpecificHeat.GetHashCode();
        }
    }

    public override string ToString()
    {
        return Name + " (" + Id.Value + ")";
    }
}
