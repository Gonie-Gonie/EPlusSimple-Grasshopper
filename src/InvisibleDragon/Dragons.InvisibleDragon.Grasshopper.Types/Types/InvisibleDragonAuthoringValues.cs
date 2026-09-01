using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Grasshopper.Types;

/// <summary>
/// Immutable Grasshopper authoring value that keeps one thermal zone together with
/// the HVAC and ventilation systems owned by that zone.
/// </summary>
public sealed class InvisibleDragonZoneDefinition
{
    public InvisibleDragonZoneDefinition(
        Zone zone,
        IEnumerable<SupplySystem>? supplySystems = null,
        IEnumerable<EnergyRecoveryVentilator>? ventilators = null)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(zone);
#else
        if (zone is null)
        {
            throw new ArgumentNullException(nameof(zone));
        }
#endif

        SupplySystem[] supplyArray = CopyRequired(supplySystems, nameof(supplySystems));
        EnergyRecoveryVentilator[] ventilatorArray = CopyRequired(ventilators, nameof(ventilators));

        EnsureUnique(
            supplyArray.Select(item => item.Id.Value),
            "supply-system",
            nameof(supplySystems));
        EnsureUnique(
            ventilatorArray.Select(item => item.Id.Value),
            "ventilator",
            nameof(ventilators));

        Zone = Copy(zone);
        SupplySystems = Array.AsReadOnly(supplyArray);
        Ventilators = Array.AsReadOnly(ventilatorArray);
    }

    public Zone Zone { get; }

    public IReadOnlyList<SupplySystem> SupplySystems { get; }

    public IReadOnlyList<EnergyRecoveryVentilator> Ventilators { get; }

    internal InvisibleDragonZoneDefinition Duplicate()
    {
        return DragonGooSnapshot.Deserialize<InvisibleDragonZoneDefinition>(
            DragonGooSnapshot.Serialize(this));
    }

    private static T Copy<T>(T value)
        where T : class
    {
        return DragonGooSnapshot.Deserialize<T>(DragonGooSnapshot.Serialize(value));
    }

    private static T[] CopyRequired<T>(IEnumerable<T>? values, string parameterName)
        where T : class
    {
        if (values is null)
        {
            return Array.Empty<T>();
        }

        T[] array = values.ToArray();
        if (array.Any(item => item is null))
        {
            throw new ArgumentException("An owned item cannot be null.", parameterName);
        }

        return array.Select(Copy).ToArray();
    }

    private static void EnsureUnique(
        IEnumerable<string> identifiers,
        string description,
        string parameterName)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (string identifier in identifiers)
        {
            if (!known.Add(identifier))
            {
                throw new ArgumentException(
                    "Duplicate " + description + " ID '" + identifier + "'.",
                    parameterName);
            }
        }
    }
}
