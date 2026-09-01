using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;

namespace Dragons.SimpleDragon.Internal;

/// <summary>
/// Provides SimpleDragon's canonical binary64 and JSON-writer facade.
/// </summary>
internal static class CanonicalDouble
{
    internal static string Format(double value) => InvariantText.FormatCanonicalDouble(value);

    internal static string FormatPythonFloat(double value) => InvariantText.FormatPythonFloat(value);

    internal static void Write(Utf8JsonWriter writer, string propertyName, double value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteRawValue(Format(value), skipInputValidation: false);
    }

    internal static void WriteValue(Utf8JsonWriter writer, double value)
    {
        writer.WriteRawValue(Format(value), skipInputValidation: false);
    }
}
