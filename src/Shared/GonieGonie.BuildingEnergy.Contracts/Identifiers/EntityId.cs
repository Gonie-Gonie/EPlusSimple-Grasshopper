using System.Text.Json;
using System.Text.Json.Serialization;
using GonieGonie.BuildingEnergy.Contracts.Internal;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// A stable identifier for a model entity.
/// </summary>
[JsonConverter(typeof(EntityIdJsonConverter))]
public sealed record EntityId : IComparable<EntityId>
{
    /// <summary>
    /// Creates an identifier from its stable textual representation.
    /// </summary>
    /// <param name="value">The non-empty, whitespace-free identifier.</param>
    [JsonConstructor]
    public EntityId(string value)
    {
        Value = Validate(value);
    }

    /// <summary>
    /// Gets the stable textual representation.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public int CompareTo(EntityId? other)
    {
        return other is null
            ? 1
            : StringComparer.Ordinal.Compare(Value, other.Value);
    }

    /// <summary>
    /// Determines whether one identifier sorts before another by ordinal value.
    /// </summary>
    public static bool operator <(EntityId? left, EntityId? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Determines whether one identifier sorts after another by ordinal value.
    /// </summary>
    public static bool operator >(EntityId? left, EntityId? right)
    {
        return right < left;
    }

    /// <summary>
    /// Determines whether one identifier sorts at or before another by ordinal value.
    /// </summary>
    public static bool operator <=(EntityId? left, EntityId? right)
    {
        return !(left > right);
    }

    /// <summary>
    /// Determines whether one identifier sorts at or after another by ordinal value.
    /// </summary>
    public static bool operator >=(EntityId? left, EntityId? right)
    {
        return !(left < right);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }

    private static string Validate(string value)
    {
        string validated = ContractGuard.RequiredText(value, nameof(value));

        for (int index = 0; index < validated.Length; index++)
        {
            char character = validated[index];
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                throw new ArgumentException(
                    "An entity identifier must not contain whitespace or control characters.",
                    nameof(value));
            }
        }

        return validated;
    }
}

internal sealed class EntityIdJsonConverter : JsonConverter<EntityId>
{
    public override EntityId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (value is null)
        {
            throw new JsonException("An entity identifier must be a non-null JSON string.");
        }

        try
        {
            return new EntityId(value);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("The entity identifier is invalid.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        EntityId value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
