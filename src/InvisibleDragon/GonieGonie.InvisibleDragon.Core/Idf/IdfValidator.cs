using System.Globalization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Idf;

public sealed class IdfValidationOptions
{
    public bool ValidateReferences { get; set; } = true;

    public bool ValidateSchemaDefaults { get; set; } = true;
}

/// <summary>
/// Performs deterministic IDD-backed IDF structural and field validation.
/// </summary>
public static class IdfValidator
{
    public static ValidationResult Validate(IdfDocument document, IdfValidationOptions? options = null)
    {
        Guard.NotNull(document, nameof(document));
        options ??= new IdfValidationOptions();
        var diagnostics = new List<Diagnostic>();
        IddSchema? schema = document.Schema;
        if (schema is null)
        {
            diagnostics.Add(Error(
                "IDF_SCHEMA_MISSING",
                "The IDF document is not bound to an IDD schema."));
            return ValidationResult.From(diagnostics);
        }

        ValidateObjectCardinality(document, schema, diagnostics);
        Dictionary<string, HashSet<string>> referenceValues = BuildReferenceValues(document);

        for (int objectIndex = 0; objectIndex < document.Count; objectIndex++)
        {
            IdfObject item = document[objectIndex];
            if (!schema.TryGetObject(item.ObjectType, out IddObjectDefinition? definition))
            {
                diagnostics.Add(Error(
                    "IDF_OBJECT_UNKNOWN",
                    $"Object #{objectIndex + 1} has unknown type '{item.ObjectType}'."));
                continue;
            }

            ValidateObject(item, definition!, objectIndex, referenceValues, options, diagnostics);
        }

        if (options.ValidateSchemaDefaults)
        {
            ValidateDefaults(schema, diagnostics);
        }

        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    private static void ValidateObjectCardinality(
        IdfDocument document,
        IddSchema schema,
        List<Diagnostic> diagnostics)
    {
        foreach (IddObjectDefinition definition in schema.Objects)
        {
            int count = document[definition.Name].Count;
            if (definition.IsRequired && count == 0)
            {
                diagnostics.Add(Error(
                    "IDF_REQUIRED_OBJECT_MISSING",
                    $"Required object '{definition.Name}' is missing."));
            }

            if (definition.IsUnique && count > 1)
            {
                diagnostics.Add(Error(
                    "IDF_UNIQUE_OBJECT_DUPLICATED",
                    $"Unique object '{definition.Name}' occurs {count} times."));
            }
        }
    }

    private static void ValidateObject(
        IdfObject item,
        IddObjectDefinition definition,
        int objectIndex,
        IReadOnlyDictionary<string, HashSet<string>> referenceValues,
        IdfValidationOptions options,
        List<Diagnostic> diagnostics)
    {
        string location = Describe(item, objectIndex);
        if (item.Count < definition.MinimumFields)
        {
            diagnostics.Add(Error(
                "IDF_MINIMUM_FIELDS",
                $"{location} has {item.Count} fields; at least {definition.MinimumFields} are required."));
        }

        if (item.Count > definition.Fields.Count && definition.ExtensibleGroupSize == 0)
        {
            diagnostics.Add(Error(
                "IDF_TOO_MANY_FIELDS",
                $"{location} has {item.Count} fields but '{definition.Name}' defines only {definition.Fields.Count}."));
        }

        int fieldsToValidate = Math.Max(item.Count, definition.Fields.Count);
        for (int fieldIndex = 0; fieldIndex < fieldsToValidate; fieldIndex++)
        {
            IddFieldDefinition? field = definition.ResolveField(fieldIndex);
            if (field is null)
            {
                break;
            }

            string actualValue = fieldIndex < item.Count ? item[fieldIndex].Trim() : string.Empty;
            if (actualValue.Length == 0)
            {
                if (field.IsRequired && field.DefaultValue is null)
                {
                    diagnostics.Add(Error(
                        "IDF_REQUIRED_FIELD_EMPTY",
                        $"{location}, field {fieldIndex + 1} '{field.Name}', is required."));
                }

                actualValue = field.DefaultValue ?? string.Empty;
            }

            if (actualValue.Length == 0)
            {
                continue;
            }

            ValidateValue(
                actualValue,
                field,
                $"{location}, field {fieldIndex + 1} '{field.Name}'",
                options.ValidateReferences ? referenceValues : null,
                diagnostics,
                isSchemaDefault: false);
        }
    }

    private static void ValidateValue(
        string rawValue,
        IddFieldDefinition field,
        string location,
        IReadOnlyDictionary<string, HashSet<string>>? referenceValues,
        List<Diagnostic> diagnostics,
        bool isSchemaDefault)
    {
        string value = NormalizeValue(rawValue);
        string codePrefix = isSchemaDefault ? "IDD_DEFAULT" : "IDF_FIELD";
        bool isAutosize = string.Equals(value, "Autosize", StringComparison.OrdinalIgnoreCase);
        bool isAutocalculate = string.Equals(value, "Autocalculate", StringComparison.OrdinalIgnoreCase);
        if (isAutosize || isAutocalculate)
        {
            if ((isAutosize && !field.IsAutosizable) || (isAutocalculate && !field.IsAutocalculatable))
            {
                diagnostics.Add(Error(
                    codePrefix + "_SPECIAL_VALUE",
                    $"{location} does not allow '{value}'."));
            }

            return;
        }

        bool numeric = field.Kind == IddFieldKind.Numeric ||
            field.DataType is IddDataType.IntegerNumber or IddDataType.Real;
        if (numeric)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
                double.IsNaN(number) || double.IsInfinity(number))
            {
                diagnostics.Add(Error(
                    codePrefix + "_NUMBER",
                    $"{location} must be a finite invariant number; found '{value}'."));
                return;
            }

            if (field.DataType == IddDataType.IntegerNumber && number != Math.Truncate(number))
            {
                diagnostics.Add(Error(
                    codePrefix + "_INTEGER",
                    $"{location} must be an integer; found '{value}'."));
            }

            ValidateBounds(number, field, location, codePrefix, diagnostics);
        }

        if (field.Choices.Count > 0 && !field.Choices.Any(
            choice => string.Equals(choice, value, StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(Error(
                codePrefix + "_CHOICE",
                $"{location} has '{value}', which is not an allowed choice."));
        }

        if (referenceValues is not null && field.ObjectLists.Count > 0)
        {
            bool found = field.ObjectLists.Any(
                listName => referenceValues.TryGetValue(listName, out HashSet<string>? values) && values.Contains(value));
            if (!found)
            {
                diagnostics.Add(Error(
                    codePrefix + "_REFERENCE",
                    $"{location} references '{value}', which is absent from [{string.Join(", ", field.ObjectLists)}]."));
            }
        }
    }

    private static void ValidateBounds(
        double value,
        IddFieldDefinition field,
        string location,
        string codePrefix,
        List<Diagnostic> diagnostics)
    {
        if (field.Minimum is not null)
        {
            bool invalid = field.Minimum.IsInclusive ? value < field.Minimum.Value : value <= field.Minimum.Value;
            if (invalid)
            {
                diagnostics.Add(Error(
                    codePrefix + "_MINIMUM",
                    $"{location} must be {(field.Minimum.IsInclusive ? ">=" : ">")} " +
                    $"{field.Minimum.Value.ToString("R", CultureInfo.InvariantCulture)}; found " +
                    $"{value.ToString("R", CultureInfo.InvariantCulture)}."));
            }
        }

        if (field.Maximum is not null)
        {
            bool invalid = field.Maximum.IsInclusive ? value > field.Maximum.Value : value >= field.Maximum.Value;
            if (invalid)
            {
                diagnostics.Add(Error(
                    codePrefix + "_MAXIMUM",
                    $"{location} must be {(field.Maximum.IsInclusive ? "<=" : "<")} " +
                    $"{field.Maximum.Value.ToString("R", CultureInfo.InvariantCulture)}; found " +
                    $"{value.ToString("R", CultureInfo.InvariantCulture)}."));
            }
        }
    }

    private static Dictionary<string, HashSet<string>> BuildReferenceValues(IdfDocument document)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (IdfObject item in document)
        {
            if (item.Definition is null)
            {
                continue;
            }

            for (int fieldIndex = 0; fieldIndex < item.Count; fieldIndex++)
            {
                IddFieldDefinition? field = item.Definition.ResolveField(fieldIndex);
                if (field is null)
                {
                    continue;
                }

                string value = NormalizeValue(item[fieldIndex]);
                if (value.Length > 0)
                {
                    foreach (string reference in field.References)
                    {
                        GetOrCreate(result, reference).Add(value);
                    }
                }

                foreach (string referenceClassName in field.ReferenceClassNames)
                {
                    GetOrCreate(result, referenceClassName).Add(item.ObjectType);
                }
            }
        }

        return result;
    }

    private static void ValidateDefaults(IddSchema schema, List<Diagnostic> diagnostics)
    {
        foreach (IddObjectDefinition item in schema.Objects)
        {
            foreach (IddFieldDefinition field in item.Fields)
            {
                if (field.DefaultValue is null)
                {
                    continue;
                }

                ValidateValue(
                    field.DefaultValue,
                    field,
                    $"IDD default for '{item.Name}.{field.Name}'",
                    referenceValues: null,
                    diagnostics,
                    isSchemaDefault: true);
            }
        }
    }

    private static HashSet<string> GetOrCreate(
        Dictionary<string, HashSet<string>> source,
        string name)
    {
        if (!source.TryGetValue(name, out HashSet<string>? values))
        {
            values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            source.Add(name, values);
        }

        return values;
    }

    private static string NormalizeValue(string value)
    {
        string normalized = value.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[normalized.Length - 1] == '"')
        {
            normalized = normalized.Substring(1, normalized.Length - 2).Replace("\"\"", "\"");
        }

        return normalized;
    }

    private static string Describe(IdfObject item, int objectIndex)
    {
        return string.IsNullOrWhiteSpace(item.Name)
            ? $"Object #{objectIndex + 1} '{item.ObjectType}'"
            : $"Object #{objectIndex + 1} '{item.ObjectType}' named '{item.Name}'";
    }

    private static Diagnostic Error(string code, string message)
    {
        return new Diagnostic(code, DiagnosticSeverity.Error, message);
    }
}
