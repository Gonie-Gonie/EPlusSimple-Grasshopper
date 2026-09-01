using System.Collections.ObjectModel;

namespace Dragons.SimpleDragon;

/// <summary>
/// A stable SimpleDragon automatic-identifier prefix.
/// </summary>
/// <remarks>
/// The instances and their order mirror the public EPlusSimple 0.7.0
/// <c>AUTOID_PREFIX</c> values. The type is immutable and intentionally exposes
/// only the defined singleton instances.
/// </remarks>
public sealed class AutoIdPrefix : IEquatable<AutoIdPrefix>, IFormattable
{
    private AutoIdPrefix(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public static AutoIdPrefix Material { get; } = new("MATERIAL", "MTRL");

    public static AutoIdPrefix SurfaceConstruction { get; } = new("SURFACE_CONSTRUCTION", "CTSF");

    public static AutoIdPrefix FenestrationConstruction { get; } = new("FENESTRATION_CONSTRUCTION", "CTFN");

    public static AutoIdPrefix SourceSystem { get; } = new("SOURCE_SYSTEM", "SRCE");

    public static AutoIdPrefix SupplySystem { get; } = new("SUPPLY_SYSTEM", "SUPL");

    public static AutoIdPrefix HeatExchanger { get; } = new("HEAT_EXCHANGER", "ERVT");

    public static AutoIdPrefix PvPanel { get; } = new("PV_PANEL", "PVPN");

    public static AutoIdPrefix Surface { get; } = new("SURFACE", "SURF");

    public static AutoIdPrefix Fenestration { get; } = new("FENESTRATION", "FNST");

    public static AutoIdPrefix Zone { get; } = new("ZONE", "ZONE");

    public static AutoIdPrefix DaySchedule { get; } = new("DAY_SCHEDULE", "DYSC");

    public static AutoIdPrefix Ruleset { get; } = new("RULESET", "RLST");

    public static AutoIdPrefix Schedule { get; } = new("SCHEDULE", "SCHE");

    public static AutoIdPrefix Profile { get; } = new("PROFILE", "PRFL");

    private static readonly IReadOnlyList<AutoIdPrefix> OrderedValues = Array.AsReadOnly(
        new[]
        {
            Material,
            SurfaceConstruction,
            FenestrationConstruction,
            SourceSystem,
            SupplySystem,
            HeatExchanger,
            PvPanel,
            Surface,
            Fenestration,
            Zone,
            DaySchedule,
            Ruleset,
            Schedule,
            Profile,
        });

    private static readonly ReadOnlyDictionary<string, AutoIdPrefix> ValuesByToken =
        new ReadOnlyDictionary<string, AutoIdPrefix>(
            OrderedValues.ToDictionary(item => item.Value, StringComparer.Ordinal));

    /// <summary>
    /// Gets the members in their upstream declaration order.
    /// </summary>
    public static IReadOnlyList<AutoIdPrefix> Values => OrderedValues;

    /// <summary>
    /// Gets the exact upstream member name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the unformatted identifier token.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Resolves an exact, case-sensitive raw token to its singleton member.
    /// </summary>
    public static AutoIdPrefix FromValue(string value)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif

        if (TryFromValue(value, out AutoIdPrefix? result))
        {
            return result!;
        }

        throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            "A defined SimpleDragon automatic-identifier prefix is required.");
    }

    /// <summary>
    /// Attempts to resolve an exact, case-sensitive raw token.
    /// </summary>
    public static bool TryFromValue(string? value, out AutoIdPrefix? result)
    {
        if (value is not null && ValuesByToken.TryGetValue(value, out AutoIdPrefix? found))
        {
            result = found;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Formats the token as an automatic-identifier prefix.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats the token as <c>VALUE-</c>, or <c>VALUE:FORMAT-</c> when a
    /// non-empty format is supplied.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        _ = formatProvider;
        return string.IsNullOrEmpty(format)
            ? Value + "-"
            : Value + ":" + format + "-";
    }

    public bool Equals(AutoIdPrefix? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => Equals(obj as AutoIdPrefix);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(AutoIdPrefix? left, AutoIdPrefix? right) => Equals(left, right);

    public static bool operator !=(AutoIdPrefix? left, AutoIdPrefix? right) => !Equals(left, right);
}

/// <summary>
/// A stable SimpleDragon tag prepended to a specially derived identifier.
/// </summary>
/// <remarks>
/// The instances and their order mirror the public EPlusSimple 0.7.0
/// <c>SpecialTag</c> values. The type is immutable and intentionally exposes
/// only the defined singleton instances.
/// </remarks>
public sealed class SpecialTag : IEquatable<SpecialTag>, IFormattable
{
    private SpecialTag(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public static SpecialTag Special { get; } = new("SPECIAL", "SPECIAL");

    public static SpecialTag Database { get; } = new("DB", "FROM_DB");

    public static SpecialTag Clone { get; } = new("CLONE", "CLONE_OF");

    public static SpecialTag Flip { get; } = new("FLIP", "REVERSED");

    public static SpecialTag CoolRoof { get; } = new("COOLROOF", "FOR_COOLROOF");

    private static readonly IReadOnlyList<SpecialTag> OrderedValues = Array.AsReadOnly(
        new[]
        {
            Special,
            Database,
            Clone,
            Flip,
            CoolRoof,
        });

    private static readonly ReadOnlyDictionary<string, SpecialTag> ValuesByToken =
        new ReadOnlyDictionary<string, SpecialTag>(
            OrderedValues.ToDictionary(item => item.Value, StringComparer.Ordinal));

    /// <summary>
    /// Gets the members in their upstream declaration order.
    /// </summary>
    public static IReadOnlyList<SpecialTag> Values => OrderedValues;

    /// <summary>
    /// Gets the exact upstream member name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the unformatted tag token.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Resolves an exact, case-sensitive raw token to its singleton member.
    /// </summary>
    public static SpecialTag FromValue(string value)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif

        if (TryFromValue(value, out SpecialTag? result))
        {
            return result!;
        }

        throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            "A defined SimpleDragon special tag is required.");
    }

    /// <summary>
    /// Attempts to resolve an exact, case-sensitive raw token.
    /// </summary>
    public static bool TryFromValue(string? value, out SpecialTag? result)
    {
        if (value is not null && ValuesByToken.TryGetValue(value, out SpecialTag? found))
        {
            result = found;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Formats the token as a special-tag prefix.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats the token as <c>$VALUE$:</c>, or <c>$VALUE:FORMAT$:</c> when a
    /// non-empty format is supplied.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        _ = formatProvider;
        return string.IsNullOrEmpty(format)
            ? "$" + Value + "$:"
            : "$" + Value + ":" + format + "$:";
    }

    public bool Equals(SpecialTag? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => Equals(obj as SpecialTag);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(SpecialTag? left, SpecialTag? right) => Equals(left, right);

    public static bool operator !=(SpecialTag? left, SpecialTag? right) => !Equals(left, right);
}
