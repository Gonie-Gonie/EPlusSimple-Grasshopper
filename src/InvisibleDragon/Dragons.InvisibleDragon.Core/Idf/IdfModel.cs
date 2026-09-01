using System.Collections;
using System.Collections.ObjectModel;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Idf;

/// <summary>
/// One IDF field value together with comments retained by the parser.
/// </summary>
public sealed class IdfField
{
    private readonly List<string> leadingComments;

    public IdfField(
        string? value,
        IEnumerable<string>? leadingComments = null,
        string? inlineComment = null)
    {
        Value = value?.Trim() ?? string.Empty;
        this.leadingComments = (leadingComments ?? Array.Empty<string>()).Select(NormalizeComment).ToList();
        LeadingComments = new ReadOnlyCollection<string>(this.leadingComments);
        InlineComment = NormalizeOptionalComment(inlineComment);
    }

    public string Value { get; set; }

    public IReadOnlyList<string> LeadingComments { get; }

    public string? InlineComment { get; internal set; }

    internal static string NormalizeComment(string? value)
    {
        string result = value?.Trim() ?? string.Empty;
        return result.Length > 0 && result[0] == '!' ? result.Substring(1).TrimStart() : result;
    }

    internal static string? NormalizeOptionalComment(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NormalizeComment(value);
    }
}

/// <summary>
/// A mutable IDF object whose field positions are optionally bound to an immutable IDD definition.
/// </summary>
public sealed class IdfObject
{
    private readonly List<IdfField> fields;
    private readonly List<string> leadingComments;

    public IdfObject(
        string objectType,
        IEnumerable<string?>? values = null,
        IddObjectDefinition? definition = null,
        IEnumerable<string>? leadingComments = null,
        string? headerComment = null)
        : this(
            objectType,
            (values ?? Array.Empty<string?>()).Select(value => new IdfField(value)),
            definition,
            leadingComments,
            headerComment)
    {
    }

    public IdfObject(
        string objectType,
        IEnumerable<IdfField> fields,
        IddObjectDefinition? definition = null,
        IEnumerable<string>? leadingComments = null,
        string? headerComment = null)
    {
        if (string.IsNullOrWhiteSpace(objectType))
        {
            throw new ArgumentException("An IDF object type is required.", nameof(objectType));
        }

        ObjectType = objectType.Trim();
        this.fields = Guard.NotNull(fields, nameof(fields)).ToList();
        if (this.fields.Any(field => field is null))
        {
            throw new ArgumentException("An IDF object cannot contain a null field.", nameof(fields));
        }

        Fields = new ReadOnlyCollection<IdfField>(this.fields);
        this.leadingComments = (leadingComments ?? Array.Empty<string>()).Select(IdfField.NormalizeComment).ToList();
        LeadingComments = new ReadOnlyCollection<string>(this.leadingComments);
        HeaderComment = IdfField.NormalizeOptionalComment(headerComment);
        BindDefinition(definition);
    }

    public string ObjectType { get; }

    public IReadOnlyList<IdfField> Fields { get; }

    public IReadOnlyList<string> LeadingComments { get; }

    public string? HeaderComment { get; internal set; }

    public IddObjectDefinition? Definition { get; private set; }

    public int Count => fields.Count;

    public string? Name
    {
        get
        {
            if (Definition is not null && Definition.TryGetField("Name", out IddFieldDefinition? nameField))
            {
                return nameField!.Position < fields.Count ? NormalizeName(fields[nameField.Position].Value) : null;
            }

            return fields.Count == 0 ? null : NormalizeName(fields[0].Value);
        }
    }

    public string this[int index]
    {
        get => fields[index].Value;
        set
        {
            EnsureField(index);
            fields[index].Value = value?.Trim() ?? string.Empty;
        }
    }

    public string this[string fieldName]
    {
        get
        {
            IddFieldDefinition definition = ResolveNamedField(fieldName);
            return definition.Position < fields.Count ? fields[definition.Position].Value : string.Empty;
        }
        set
        {
            IddFieldDefinition definition = ResolveNamedField(fieldName);
            this[definition.Position] = value;
        }
    }

    public void Add(string? value)
    {
        fields.Add(new IdfField(value));
    }

    public void Insert(int index, string? value)
    {
        fields.Insert(index, new IdfField(value));
    }

    public void ApplyDefaults()
    {
        if (Definition is null)
        {
            return;
        }

        int targetCount = Math.Max(fields.Count, Definition.MinimumFields);
        while (fields.Count < targetCount)
        {
            fields.Add(new IdfField(string.Empty));
        }

        for (int index = 0; index < fields.Count; index++)
        {
            IddFieldDefinition? fieldDefinition = Definition.ResolveField(index);
            if (fieldDefinition?.DefaultValue is not null && string.IsNullOrWhiteSpace(fields[index].Value))
            {
                fields[index].Value = fieldDefinition.DefaultValue;
            }
        }
    }

    internal void BindDefinition(IddObjectDefinition? definition)
    {
        if (definition is not null && !string.Equals(ObjectType, definition.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"IDD definition '{definition.Name}' does not describe IDF object '{ObjectType}'.",
                nameof(definition));
        }

        Definition = definition;
    }

    private IddFieldDefinition ResolveNamedField(string fieldName)
    {
        if (Definition is null)
        {
            throw new InvalidOperationException($"IDF object '{ObjectType}' is not bound to an IDD definition.");
        }

        if (!Definition.TryGetField(fieldName, out IddFieldDefinition? field))
        {
            throw new KeyNotFoundException($"Field '{fieldName}' is not defined for '{ObjectType}'.");
        }

        return field!;
    }

    private void EnsureField(int index)
    {
        Guard.NonNegative(index, nameof(index));
        while (fields.Count <= index)
        {
            fields.Add(new IdfField(string.Empty));
        }
    }

    private static string NormalizeName(string value)
    {
        string normalized = value.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[normalized.Length - 1] == '"')
        {
            return normalized.Substring(1, normalized.Length - 2).Replace("\"\"", "\"");
        }

        return normalized;
    }
}

/// <summary>
/// A live, type-filtered view over an <see cref="IdfDocument"/>.
/// </summary>
public sealed class IdfObjectCollection : IReadOnlyList<IdfObject>
{
    private readonly IdfDocument document;
    private readonly string objectType;

    internal IdfObjectCollection(IdfDocument document, string objectType)
    {
        this.document = document;
        this.objectType = objectType;
    }

    public int Count => Items.Count;

    public IdfObject this[int index] => Items[index];

    public IdfObject this[string name]
    {
        get
        {
            if (TryGetByName(name, out IdfObject? value))
            {
                return value!;
            }

            throw new KeyNotFoundException($"No '{objectType}' object named '{name}' exists.");
        }
    }

    public bool TryGetByName(string name, out IdfObject? value)
    {
        value = Items.FirstOrDefault(
            item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return value is not null;
    }

    public void Append(IdfObject value)
    {
        EnsureType(value);
        document.Append(value);
    }

    public void Insert(int index, IdfObject value)
    {
        EnsureType(value);
        IReadOnlyList<IdfObject> items = Items;
        if (index < 0 || index > items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        int documentIndex = index == items.Count
            ? document.FindInsertionIndexAfterType(objectType)
            : document.IndexOf(items[index]);
        document.Insert(documentIndex, value);
    }

    public IEnumerator<IdfObject> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private IReadOnlyList<IdfObject> Items => document.GetObjects(objectType);

    private void EnsureType(IdfObject value)
    {
        Guard.NotNull(value, nameof(value));
        if (!string.Equals(value.ObjectType, objectType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Object type '{value.ObjectType}' cannot be added to the '{objectType}' collection.",
                nameof(value));
        }
    }
}

/// <summary>
/// An ordered IDF document with fast case-insensitive object-class indexing.
/// </summary>
public sealed class IdfDocument : IReadOnlyList<IdfObject>
{
    private readonly List<IdfObject> objects = new();
    private readonly Dictionary<string, List<IdfObject>> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> preambleComments;
    private readonly List<string> trailingComments;

    public IdfDocument(
        IddSchema? schema = null,
        IEnumerable<IdfObject>? objects = null,
        IEnumerable<string>? preambleComments = null,
        IEnumerable<string>? trailingComments = null)
    {
        Schema = schema;
        this.preambleComments = (preambleComments ?? Array.Empty<string>()).Select(IdfField.NormalizeComment).ToList();
        this.trailingComments = (trailingComments ?? Array.Empty<string>()).Select(IdfField.NormalizeComment).ToList();
        PreambleComments = new ReadOnlyCollection<string>(this.preambleComments);
        TrailingComments = new ReadOnlyCollection<string>(this.trailingComments);
        foreach (IdfObject item in objects ?? Array.Empty<IdfObject>())
        {
            Append(item);
        }
    }

    public IddSchema? Schema { get; }

    public IReadOnlyList<string> PreambleComments { get; }

    public IReadOnlyList<string> TrailingComments { get; }

    public int Count => objects.Count;

    public IdfObject this[int index] => objects[index];

    public IdfObjectCollection this[string objectType] => new(this, objectType);

    public string? EnergyPlusVersion
    {
        get
        {
            IdfObjectCollection versions = this["Version"];
            return versions.Count == 0 || versions[0].Count == 0 ? null : versions[0][0];
        }
    }

    public void Append(IdfObject value)
    {
        Insert(objects.Count, value);
    }

    public void Insert(int index, IdfObject value)
    {
        Guard.NotNull(value, nameof(value));
        if (index < 0 || index > objects.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        Bind(value);
        objects.Insert(index, value);
        RebuildIndex();
    }

    public bool Remove(IdfObject value)
    {
        Guard.NotNull(value, nameof(value));
        bool removed = objects.Remove(value);
        if (removed)
        {
            RebuildIndex();
        }

        return removed;
    }

    public void ApplyDefaults()
    {
        foreach (IdfObject item in objects)
        {
            item.ApplyDefaults();
        }
    }

    public IEnumerator<IdfObject> GetEnumerator()
    {
        return objects.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal IReadOnlyList<IdfObject> GetObjects(string objectType)
    {
        return index.TryGetValue(objectType, out List<IdfObject>? items)
            ? items
            : Array.AsReadOnly(Array.Empty<IdfObject>());
    }

    internal int FindInsertionIndexAfterType(string objectType)
    {
        for (int position = objects.Count - 1; position >= 0; position--)
        {
            if (string.Equals(objects[position].ObjectType, objectType, StringComparison.OrdinalIgnoreCase))
            {
                return position + 1;
            }
        }

        return objects.Count;
    }

    internal int IndexOf(IdfObject item)
    {
        return objects.IndexOf(item);
    }

    private void Bind(IdfObject value)
    {
        if (Schema is not null && Schema.TryGetObject(value.ObjectType, out IddObjectDefinition? definition))
        {
            value.BindDefinition(definition);
        }
    }

    private void RebuildIndex()
    {
        index.Clear();
        foreach (IdfObject item in objects)
        {
            if (!index.TryGetValue(item.ObjectType, out List<IdfObject>? items))
            {
                items = new List<IdfObject>();
                index.Add(item.ObjectType, items);
            }

            items.Add(item);
        }
    }
}
