using System.Diagnostics.CodeAnalysis;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Internal;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Hvac;

/// <summary>
/// Describes a domestic-hot-water system whose pinned upstream representation
/// currently emits no EnergyPlus objects.
/// </summary>
public sealed class DomesticHotWater : HvacSystem
{
    public DomesticHotWater(EntityId id, string name, Fuel fuel, double efficiency)
        : base(id, name)
    {
        if (!Enum.IsDefined(typeof(Fuel), fuel))
        {
            throw new ArgumentOutOfRangeException(nameof(fuel), fuel, "A defined fuel is required.");
        }

        double validatedEfficiency = DomainGuard.Positive(efficiency, nameof(efficiency));
        if (validatedEfficiency > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(efficiency),
                efficiency,
                "Efficiency must be greater than zero and no greater than one.");
        }

        Fuel = fuel;
        Efficiency = validatedEfficiency;
    }

    public Fuel Fuel { get; }

    public double Efficiency { get; }

    /// <summary>
    /// Returns the pinned upstream emission, which is currently an empty list.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The public instance route preserves the pinned upstream DomesticHotWater.to_idf_object contract.")]
    public IReadOnlyList<IdfObject> ToIdfObjects(IdfGenerationContext context)
    {
        DomainGuard.NotNull(context, nameof(context));
        return new List<IdfObject>().AsReadOnly();
    }

    public override string ToString()
    {
        return FormattableString.Invariant(
            $"Domestic Hot Water {Name} (fuel: {Fuel}, efficiency: {Efficiency * 100:0.0}%)");
    }
}
