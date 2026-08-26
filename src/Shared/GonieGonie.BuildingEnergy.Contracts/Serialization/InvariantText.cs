using System.Globalization;
using GonieGonie.BuildingEnergy.Contracts.Internal;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// Provides invariant text conversions for CSV, manifests, cache keys, and diagnostics.
/// </summary>
public static class InvariantText
{
    /// <summary>
    /// Formats a floating-point value in a round-trippable, culture-independent form.
    /// </summary>
    public static string FormatDouble(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a finite floating-point value as the shortest canonical JSON number without depending on the host CLR's dtoa implementation.
    /// </summary>
    public static string FormatCanonicalDouble(double value)
    {
        return CanonicalDouble.FormatCanonical(value);
    }

    /// <summary>
    /// Formats a floating-point value with CPython 3.12 binary64 representation semantics.
    /// </summary>
    public static string FormatPythonFloat(double value)
    {
        return CanonicalDouble.FormatPythonFloat(value);
    }

    /// <summary>
    /// Parses an invariant floating-point value without accepting locale-specific separators.
    /// </summary>
    public static double ParseDouble(string value)
    {
        string validated = ContractGuard.RequiredText(value, nameof(value));
        return double.Parse(validated, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats an instant as an ISO 8601 UTC timestamp.
    /// </summary>
    public static string FormatUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
}
