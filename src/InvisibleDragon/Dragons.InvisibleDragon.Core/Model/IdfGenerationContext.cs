using System.Globalization;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Model;

/// <summary>
/// Describes one IDF value by its EnergyPlus field name and stable 24.2 fallback position.
/// </summary>
public readonly struct IdfFieldValue
{
    public IdfFieldValue(int fallbackPosition, string fieldName, object? value)
    {
        DomainGuard.NonNegative(fallbackPosition, nameof(fallbackPosition));
        FallbackPosition = fallbackPosition;
        FieldName = DomainGuard.RequiredText(fieldName, nameof(fieldName));
        Value = value;
    }

    public int FallbackPosition { get; }

    public string FieldName { get; }

    public object? Value { get; }
}

/// <summary>
/// Creates IDF objects using IDD field names when available and deterministic 24.2 positions otherwise.
/// </summary>
public sealed class IdfGenerationContext
{
    public IdfGenerationContext(
        IddSchema? schema = null,
        EnergyModelIdfOptions? options = null)
    {
        Schema = schema;
        Options = options ?? new EnergyModelIdfOptions();
    }

    public IddSchema? Schema { get; }

    /// <summary>
    /// Gets the IDF generation behavior shared by all nested exporters.
    /// </summary>
    public EnergyModelIdfOptions Options { get; }

    public IdfObject Create(string objectType, params IdfFieldValue[] fields)
    {
        DomainGuard.RequiredText(objectType, nameof(objectType));
        DomainGuard.NotNull(fields, nameof(fields));

        IddObjectDefinition? definition = null;
        Schema?.TryGetObject(objectType, out definition);
        var resolved = new List<(int Position, string Value)>();
        foreach (IdfFieldValue field in fields)
        {
            int position = definition is not null
                && definition.TryGetField(field.FieldName, out IddFieldDefinition? fieldDefinition)
                    ? fieldDefinition!.Position
                    : field.FallbackPosition;
            resolved.Add((position, Format(field.Value)));
        }

        int count = resolved.Count == 0 ? 0 : resolved.Max(field => field.Position) + 1;
        string?[] values = new string?[count];
        foreach ((int position, string value) in resolved)
        {
            if (!string.IsNullOrEmpty(values[position]))
            {
                throw new ArgumentException(
                    $"More than one value targets field {position + 1} of '{objectType}'.",
                    nameof(fields));
            }

            values[position] = value;
        }

        return new IdfObject(objectType, values, definition);
    }

    public IdfObject CreateRaw(string objectType, params object?[] values)
    {
        DomainGuard.NotNull(values, nameof(values));
        IddObjectDefinition? definition = null;
        Schema?.TryGetObject(objectType, out definition);
        return new IdfObject(objectType, values.Select(Format), definition);
    }

    public static IdfFieldValue Field(int position, string name, object? value)
    {
        return new IdfFieldValue(position, name, value);
    }

    public static string Format(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool flag => flag ? "Yes" : "No",
            Enum enumeration => enumeration.ToString(),
            double number => InvariantText.FormatPythonFloat(number),
            float number => InvariantText.FormatPythonFloat(number),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }
}
