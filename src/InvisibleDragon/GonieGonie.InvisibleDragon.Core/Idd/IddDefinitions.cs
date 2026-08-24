using System.Collections.ObjectModel;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Idd;

/// <summary>
/// Identifies the physical token family used by an EnergyPlus IDD field.
/// </summary>
public enum IddFieldKind
{
    Alpha,
    Numeric,
}

/// <summary>
/// Identifies the validation type declared by an EnergyPlus IDD field.
/// </summary>
public enum IddDataType
{
    Unspecified,
    Alpha,
    Choice,
    ObjectList,
    ExternalList,
    Node,
    IntegerNumber,
    Real,
}

/// <summary>
/// Defines one inclusive or exclusive numeric constraint.
/// </summary>
public sealed class IddNumericBound
{
    public IddNumericBound(double value, bool isInclusive)
    {
        Value = value;
        IsInclusive = isInclusive;
    }

    public double Value { get; }

    public bool IsInclusive { get; }
}

/// <summary>
/// Immutable definition of one field in an EnergyPlus object.
/// </summary>
public sealed class IddFieldDefinition
{
    public IddFieldDefinition(
        string token,
        int position,
        IddFieldKind kind,
        string name,
        IEnumerable<string>? notes = null,
        string? units = null,
        string? ipUnits = null,
        string? unitsBasedOnField = null,
        bool isRequired = false,
        bool beginsExtensible = false,
        bool isDeprecated = false,
        bool isAutosizable = false,
        bool isAutocalculatable = false,
        bool retainsCase = false,
        string? defaultValue = null,
        IddDataType dataType = IddDataType.Unspecified,
        IEnumerable<string>? choices = null,
        IEnumerable<string>? objectLists = null,
        string? externalList = null,
        IEnumerable<string>? references = null,
        IEnumerable<string>? referenceClassNames = null,
        IddNumericBound? minimum = null,
        IddNumericBound? maximum = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? additionalDirectives = null)
    {
        Token = Required(token, nameof(token));
        Guard.NonNegative(position, nameof(position));

        Position = position;
        Kind = kind;
        Name = string.IsNullOrWhiteSpace(name) ? token : name.Trim();
        Notes = Copy(notes);
        Units = Optional(units);
        IpUnits = Optional(ipUnits);
        UnitsBasedOnField = Optional(unitsBasedOnField);
        IsRequired = isRequired;
        BeginsExtensible = beginsExtensible;
        IsDeprecated = isDeprecated;
        IsAutosizable = isAutosizable;
        IsAutocalculatable = isAutocalculatable;
        RetainsCase = retainsCase;
        DefaultValue = Optional(defaultValue);
        DataType = dataType;
        Choices = Copy(choices);
        ObjectLists = Copy(objectLists);
        ExternalList = Optional(externalList);
        References = Copy(references);
        ReferenceClassNames = Copy(referenceClassNames);
        Minimum = minimum;
        Maximum = maximum;
        AdditionalDirectives = CopyDictionary(additionalDirectives);
    }

    public string Token { get; }

    public int Position { get; }

    public IddFieldKind Kind { get; }

    public string Name { get; }

    public IReadOnlyList<string> Notes { get; }

    public string? Units { get; }

    public string? IpUnits { get; }

    public string? UnitsBasedOnField { get; }

    public bool IsRequired { get; }

    public bool BeginsExtensible { get; }

    public bool IsDeprecated { get; }

    public bool IsAutosizable { get; }

    public bool IsAutocalculatable { get; }

    public bool RetainsCase { get; }

    public string? DefaultValue { get; }

    public IddDataType DataType { get; }

    public IReadOnlyList<string> Choices { get; }

    public IReadOnlyList<string> ObjectLists { get; }

    public string? ExternalList { get; }

    public IReadOnlyList<string> References { get; }

    public IReadOnlyList<string> ReferenceClassNames { get; }

    public IddNumericBound? Minimum { get; }

    public IddNumericBound? Maximum { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> AdditionalDirectives { get; }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? Optional(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static ReadOnlyCollection<string> Copy(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        return new ReadOnlyCollection<string>(values.Select(value => value.Trim()).ToArray());
    }

    private static ReadOnlyDictionary<string, IReadOnlyList<string>> CopyDictionary(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? values)
    {
        var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (values is not null)
        {
            foreach (KeyValuePair<string, IReadOnlyList<string>> item in values)
            {
                copy.Add(item.Key, Copy(item.Value));
            }
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }
}

/// <summary>
/// Immutable definition of one EnergyPlus IDD object class.
/// </summary>
public sealed class IddObjectDefinition
{
    private readonly ReadOnlyDictionary<string, IddFieldDefinition> fieldsByName;

    public IddObjectDefinition(
        string name,
        string group,
        IEnumerable<IddFieldDefinition> fields,
        IEnumerable<string>? memo = null,
        bool isUnique = false,
        bool isRequired = false,
        int minimumFields = 0,
        int extensibleGroupSize = 0,
        string? format = null,
        string? obsoleteMessage = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? additionalDirectives = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An IDD object name is required.", nameof(name));
        }

        Guard.NonNegative(minimumFields, nameof(minimumFields));
        Guard.NonNegative(extensibleGroupSize, nameof(extensibleGroupSize));

        Name = name.Trim();
        Group = group?.Trim() ?? string.Empty;
        var fieldArray = fields?.ToArray() ?? throw new ArgumentNullException(nameof(fields));
        Fields = new ReadOnlyCollection<IddFieldDefinition>(fieldArray);
        Memo = new ReadOnlyCollection<string>((memo ?? Array.Empty<string>()).Select(value => value.Trim()).ToArray());
        IsUnique = isUnique;
        IsRequired = isRequired;
        MinimumFields = minimumFields;
        ExtensibleGroupSize = extensibleGroupSize;
        Format = string.IsNullOrWhiteSpace(format?.Trim()) ? null : format!.Trim();
        ObsoleteMessage = string.IsNullOrWhiteSpace(obsoleteMessage?.Trim()) ? null : obsoleteMessage!.Trim();
        AdditionalDirectives = CopyDictionary(additionalDirectives);

        var byName = new Dictionary<string, IddFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < fieldArray.Length; index++)
        {
            IddFieldDefinition field = fieldArray[index];
            if (field.Position != index)
            {
                throw new ArgumentException("IDD field positions must be consecutive and ordered.", nameof(fields));
            }

#if NETFRAMEWORK
            if (!byName.ContainsKey(field.Name))
            {
                byName.Add(field.Name, field);
            }
#else
            byName.TryAdd(field.Name, field);
#endif
        }

        fieldsByName = new ReadOnlyDictionary<string, IddFieldDefinition>(byName);
        int markedStart = Array.FindIndex(fieldArray, field => field.BeginsExtensible);
        ExtensibleStartIndex = markedStart >= 0
            ? markedStart
            : extensibleGroupSize > 0 && fieldArray.Length >= extensibleGroupSize
                ? fieldArray.Length - extensibleGroupSize
                : (int?)null;
    }

    public string Name { get; }

    public string Group { get; }

    public IReadOnlyList<IddFieldDefinition> Fields { get; }

    public IReadOnlyList<string> Memo { get; }

    public bool IsUnique { get; }

    public bool IsRequired { get; }

    public int MinimumFields { get; }

    public int ExtensibleGroupSize { get; }

    public int? ExtensibleStartIndex { get; }

    public string? Format { get; }

    public string? ObsoleteMessage { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> AdditionalDirectives { get; }

    public IddFieldDefinition this[int index] => Fields[index];

    public IddFieldDefinition this[string fieldName] => fieldsByName[fieldName];

    public bool TryGetField(string fieldName, out IddFieldDefinition? field)
    {
        return fieldsByName.TryGetValue(fieldName, out field);
    }

    /// <summary>
    /// Resolves both explicitly declared and dynamically repeated extensible fields.
    /// </summary>
    public IddFieldDefinition? ResolveField(int zeroBasedIndex)
    {
        Guard.NonNegative(zeroBasedIndex, nameof(zeroBasedIndex));

        if (zeroBasedIndex < Fields.Count)
        {
            return Fields[zeroBasedIndex];
        }

        if (ExtensibleGroupSize == 0 || ExtensibleStartIndex is null)
        {
            return null;
        }

        int repeatedOffset = (zeroBasedIndex - ExtensibleStartIndex.Value) % ExtensibleGroupSize;
        return Fields[ExtensibleStartIndex.Value + repeatedOffset];
    }

    private static ReadOnlyDictionary<string, IReadOnlyList<string>> CopyDictionary(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? values)
    {
        var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (values is not null)
        {
            foreach (KeyValuePair<string, IReadOnlyList<string>> item in values)
            {
                copy.Add(item.Key, new ReadOnlyCollection<string>(item.Value.ToArray()));
            }
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }
}

/// <summary>
/// Immutable, ordered, case-insensitively queryable EnergyPlus dictionary.
/// </summary>
public sealed class IddSchema
{
    private readonly ReadOnlyDictionary<string, IddObjectDefinition> objectsByName;

    public IddSchema(
        string version,
        string build,
        string sourceSha256,
        IEnumerable<IddObjectDefinition> objects)
    {
        Version = version?.Trim() ?? string.Empty;
        Build = build?.Trim() ?? string.Empty;
        SourceSha256 = NormalizeSha256(sourceSha256);
        var objectArray = objects?.ToArray() ?? throw new ArgumentNullException(nameof(objects));
        Objects = new ReadOnlyCollection<IddObjectDefinition>(objectArray);

        var lookup = new Dictionary<string, IddObjectDefinition>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<string>();
        var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IddObjectDefinition definition in objectArray)
        {
            if (lookup.ContainsKey(definition.Name))
            {
                throw new ArgumentException($"Duplicate IDD object '{definition.Name}'.", nameof(objects));
            }

            lookup.Add(definition.Name, definition);
            if (seenGroups.Add(definition.Group))
            {
                groups.Add(definition.Group);
            }
        }

        objectsByName = new ReadOnlyDictionary<string, IddObjectDefinition>(lookup);
        Groups = new ReadOnlyCollection<string>(groups);
    }

    public string Version { get; }

    public string Build { get; }

    public string SourceSha256 { get; }

    public IReadOnlyList<IddObjectDefinition> Objects { get; }

    public IReadOnlyList<string> Groups { get; }

    public IddObjectDefinition this[int index] => Objects[index];

    public IddObjectDefinition this[string objectName] => objectsByName[objectName];

    public bool TryGetObject(string objectName, out IddObjectDefinition? definition)
    {
        return objectsByName.TryGetValue(objectName, out definition);
    }

    public IEnumerable<IddObjectDefinition> InGroup(string group)
    {
        return Objects.Where(item => string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase));
    }

    internal static string NormalizeSha256(string value)
    {
        string normalized = value?.Trim().Replace("-", string.Empty) ?? string.Empty;
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A 64-character SHA-256 hex digest is required.", nameof(value));
        }

        return normalized.ToLowerInvariant();
    }
}
