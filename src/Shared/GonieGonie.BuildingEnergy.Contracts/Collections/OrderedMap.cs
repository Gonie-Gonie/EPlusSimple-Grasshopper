using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using GonieGonie.BuildingEnergy.Contracts.Internal;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// An immutable string-keyed map that preserves insertion order.
/// </summary>
/// <typeparam name="TValue">The mapped value type.</typeparam>
[JsonConverter(typeof(OrderedMapJsonConverterFactory))]
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "OrderedMap is the domain contract name and distinguishes deterministic ordering from mutable dictionaries.")]
public sealed class OrderedMap<TValue> : IReadOnlyDictionary<string, TValue>
{
    private readonly ReadOnlyCollection<KeyValuePair<string, TValue>> _entries;
    private readonly ReadOnlyCollection<string> _keys;
    private readonly ReadOnlyCollection<TValue> _values;
    private readonly Dictionary<string, int> _indices;

    /// <summary>
    /// Creates an empty map.
    /// </summary>
    public OrderedMap()
        : this(Array.Empty<KeyValuePair<string, TValue>>())
    {
    }

    /// <summary>
    /// Creates a map by preserving the exact order of the supplied entries.
    /// </summary>
    public OrderedMap(IEnumerable<KeyValuePair<string, TValue>> entries)
    {
        ContractGuard.NotNull(entries, nameof(entries));

        List<KeyValuePair<string, TValue>> entryCopy = new();
        List<string> keys = new();
        List<TValue> values = new();
        _indices = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, TValue> entry in entries)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException("An ordered map cannot contain a null key.", nameof(entries));
            }

            if (_indices.ContainsKey(entry.Key))
            {
                throw new ArgumentException(
                    "An ordered map cannot contain duplicate keys: '" + entry.Key + "'.",
                    nameof(entries));
            }

            _indices.Add(entry.Key, entryCopy.Count);

            entryCopy.Add(entry);
            keys.Add(entry.Key);
            values.Add(entry.Value);
        }

        _entries = new ReadOnlyCollection<KeyValuePair<string, TValue>>(entryCopy);
        _keys = new ReadOnlyCollection<string>(keys);
        _values = new ReadOnlyCollection<TValue>(values);
    }

    /// <inheritdoc />
    public int Count => _entries.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _keys;

    /// <inheritdoc />
    public IEnumerable<TValue> Values => _values;

    /// <inheritdoc />
    public TValue this[string key]
    {
        get
        {
            ContractGuard.NotNull(key, nameof(key));

            if (!_indices.TryGetValue(key, out int index))
            {
                throw new KeyNotFoundException("The ordered map does not contain key '" + key + "'.");
            }

            return _entries[index].Value;
        }
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        ContractGuard.NotNull(key, nameof(key));

        return _indices.ContainsKey(key);
    }

    /// <inheritdoc />
    public bool TryGetValue(string key, out TValue value)
    {
        ContractGuard.NotNull(key, nameof(key));

        if (_indices.TryGetValue(key, out int index))
        {
            value = _entries[index].Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Returns a new map with an entry appended.
    /// </summary>
    public OrderedMap<TValue> Add(string key, TValue value)
    {
        ContractGuard.NotNull(key, nameof(key));

        if (ContainsKey(key))
        {
            throw new ArgumentException("The ordered map already contains key '" + key + "'.", nameof(key));
        }

        List<KeyValuePair<string, TValue>> entries = new(_entries)
        {
            new KeyValuePair<string, TValue>(key, value),
        };
        return new OrderedMap<TValue>(entries);
    }

    /// <summary>
    /// Returns a new map with a value replaced in place, or appended when the key is new.
    /// </summary>
    public OrderedMap<TValue> SetItem(string key, TValue value)
    {
        ContractGuard.NotNull(key, nameof(key));

        if (!_indices.TryGetValue(key, out int replacementIndex))
        {
            return Add(key, value);
        }

        KeyValuePair<string, TValue>[] entries = _entries.ToArray();
        entries[replacementIndex] = new KeyValuePair<string, TValue>(key, value);
        return new OrderedMap<TValue>(entries);
    }

    /// <summary>
    /// Returns a new map without the specified entry.
    /// </summary>
    public OrderedMap<TValue> Remove(string key)
    {
        ContractGuard.NotNull(key, nameof(key));

        if (!ContainsKey(key))
        {
            return this;
        }

        return new OrderedMap<TValue>(_entries.Where(entry => !StringComparer.Ordinal.Equals(entry.Key, key)));
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
    {
        return _entries.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

internal sealed class OrderedMapJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(OrderedMap<>);
    }

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        Type valueType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(OrderedMapJsonConverter<>).MakeGenericType(valueType);
        object? converter = Activator.CreateInstance(converterType);
        if (converter is null)
        {
            throw new InvalidOperationException("Unable to create an ordered-map JSON converter.");
        }

        return (JsonConverter)converter;
    }
}

internal sealed class OrderedMapJsonConverter<TValue> : JsonConverter<OrderedMap<TValue>>
{
    public override OrderedMap<TValue> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An ordered map must be represented by a JSON object.");
        }

        List<KeyValuePair<string, TValue>> entries = new();
        HashSet<string> keys = new(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new OrderedMap<TValue>(entries);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected an ordered-map property name.");
            }

            string key = reader.GetString()!;
            if (!keys.Add(key))
            {
                throw new JsonException("An ordered map cannot contain duplicate key '" + key + "'.");
            }

            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of an ordered-map value.");
            }

            TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options)!;
            entries.Add(new KeyValuePair<string, TValue>(key, value));
        }

        throw new JsonException("Unexpected end of an ordered map.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        OrderedMap<TValue> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (KeyValuePair<string, TValue> entry in value)
        {
            writer.WritePropertyName(entry.Key);
            JsonSerializer.Serialize(writer, entry.Value, options);
        }

        writer.WriteEndObject();
    }
}
