using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Idd;

/// <summary>
/// Parses the canonical EnergyPlus IDD text format without EnergyPlus runtime dependencies.
/// </summary>
public static class IddParser
{
    private static readonly Regex FieldLine = new(
        @"^\s*(?<fields>(?:[AN]\d+\s*[,;]\s*)+)(?<directives>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FieldToken = new(
        @"(?<token>[AN]\d+)\s*(?<delimiter>[,;])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex VersionLine = new(
        @"^\s*!IDD_Version\s+(?<value>\S+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BuildLine = new(
        @"^\s*!IDD_BUILD\s+(?<value>\S+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IddSchema ParseFile(string path)
    {
        Guard.NotNull(path, nameof(path));
        byte[] bytes = File.ReadAllBytes(path);
        string hash = ComputeSha256(bytes);
        string text = DetectText(bytes);
        return Parse(text, hash);
    }

    public static IddSchema Parse(string text)
    {
        Guard.NotNull(text, nameof(text));
        return Parse(text, ComputeSha256(Encoding.UTF8.GetBytes(text)));
    }

    public static IddSchema Parse(string text, string sourceSha256)
    {
        Guard.NotNull(text, nameof(text));

        string version = string.Empty;
        string build = string.Empty;
        string group = string.Empty;
        var objects = new List<IddObjectDefinition>();
        ObjectBuilder? currentObject = null;
        FieldBuilder? currentField = null;

        using var reader = new StringReader(text);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            Match versionMatch = VersionLine.Match(line);
            if (versionMatch.Success)
            {
                version = versionMatch.Groups["value"].Value.Trim();
                continue;
            }

            Match buildMatch = BuildLine.Match(line);
            if (buildMatch.Success)
            {
                build = buildMatch.Groups["value"].Value.Trim();
                continue;
            }

            int inlineComment = line.IndexOf('!');
            if (inlineComment >= 0)
            {
                line = line.Substring(0, inlineComment);
            }

            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '!')
            {
                continue;
            }

            Match fieldMatch = FieldLine.Match(line);
            if (fieldMatch.Success)
            {
                if (currentObject is null)
                {
                    throw new FormatException($"IDD field found before an object at line {lineNumber}.");
                }

                foreach (Match tokenMatch in FieldToken.Matches(fieldMatch.Groups["fields"].Value))
                {
                    FinishField(currentObject, ref currentField);
                    string token = tokenMatch.Groups["token"].Value.ToUpperInvariant();
                    currentField = new FieldBuilder(token, currentObject.Fields.Count);
                }

                if (currentField is null)
                {
                    throw new FormatException($"IDD field declaration did not contain a token at line {lineNumber}.");
                }

                ApplyDirectiveText(fieldMatch.Groups["directives"].Value, currentObject, currentField, lineNumber);
                continue;
            }

            if (trimmed[0] == '\\')
            {
                Directive directive = ParseDirective(trimmed, lineNumber);
                if (string.Equals(directive.Name, "group", StringComparison.OrdinalIgnoreCase))
                {
                    FinishObject(objects, ref currentObject, ref currentField);
                    group = directive.Value;
                    continue;
                }

                if (currentObject is null)
                {
                    continue;
                }

                ApplyDirective(directive, currentObject, currentField);
                continue;
            }

            if (IsObjectHeader(trimmed))
            {
                FinishObject(objects, ref currentObject, ref currentField);
                currentObject = new ObjectBuilder(trimmed.Substring(0, trimmed.Length - 1).Trim(), group);
                continue;
            }

            throw new FormatException($"Unrecognized IDD syntax at line {lineNumber}: {trimmed}");
        }

        FinishObject(objects, ref currentObject, ref currentField);
        return new IddSchema(version, build, sourceSha256, objects);
    }

    public static string ComputeFileSha256(string path)
    {
        Guard.NotNull(path, nameof(path));
#if NET6_0_OR_GREATER
        using var stream = File.OpenRead(path);
        return ToHex(SHA256.HashData(stream));
#else
        using var stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return ToHex(sha.ComputeHash(stream));
#endif
    }

    private static string ComputeSha256(byte[] bytes)
    {
#if NET6_0_OR_GREATER
        return ToHex(SHA256.HashData(bytes));
#else
        using SHA256 sha = SHA256.Create();
        return ToHex(sha.ComputeHash(bytes));
#endif
    }

    private static string ToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string DetectText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true).GetString(bytes, 3, bytes.Length - 3);
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
    }

    private static bool IsObjectHeader(string value)
    {
        if (value.Length < 2 || value[value.Length - 1] != ',')
        {
            return false;
        }

        char first = value[0];
        return first != '!' && first != '\\';
    }

    private static void ApplyDirectiveText(
        string text,
        ObjectBuilder currentObject,
        FieldBuilder currentField,
        int lineNumber)
    {
        int slashIndex = text.IndexOf('\\');
        if (slashIndex >= 0)
        {
            ApplyDirective(ParseDirective(text.Substring(slashIndex).Trim(), lineNumber), currentObject, currentField);
        }
    }

    private static Directive ParseDirective(string text, int lineNumber)
    {
        if (text.Length < 2 || text[0] != '\\')
        {
            throw new FormatException($"Invalid IDD directive at line {lineNumber}.");
        }

        int index = 1;
        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '-' or '<' or '>'))
        {
            index++;
        }

        if (index == 1)
        {
            throw new FormatException($"Empty IDD directive at line {lineNumber}.");
        }

        string name = text.Substring(1, index - 1);
        string value;
        if (index < text.Length && text[index] == ':')
        {
            index++;
            int end = index;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            value = text.Substring(index, end - index);
        }
        else
        {
            value = text.Substring(index).Trim();
        }

        return new Directive(name, value);
    }

    private static void ApplyDirective(Directive directive, ObjectBuilder targetObject, FieldBuilder? targetField)
    {
        if (targetField is null)
        {
            targetObject.Apply(directive);
        }
        else
        {
            targetField.Apply(directive);
        }
    }

    private static void FinishField(ObjectBuilder currentObject, ref FieldBuilder? currentField)
    {
        if (currentField is not null)
        {
            currentObject.Fields.Add(currentField.Build());
            currentField = null;
        }
    }

    private static void FinishObject(
        List<IddObjectDefinition> objects,
        ref ObjectBuilder? currentObject,
        ref FieldBuilder? currentField)
    {
        if (currentObject is null)
        {
            return;
        }

        FinishField(currentObject, ref currentField);
        objects.Add(currentObject.Build());
        currentObject = null;
    }

    private sealed class ObjectBuilder
    {
        private readonly List<string> memo = new();
        private readonly Dictionary<string, List<string>> additional = new(StringComparer.OrdinalIgnoreCase);

        public ObjectBuilder(string name, string group)
        {
            Name = name;
            Group = group;
        }

        public string Name { get; }

        public string Group { get; }

        public List<IddFieldDefinition> Fields { get; } = new();

        public bool IsUnique { get; private set; }

        public bool IsRequired { get; private set; }

        public int MinimumFields { get; private set; }

        public int ExtensibleGroupSize { get; private set; }

        public string? Format { get; private set; }

        public string? ObsoleteMessage { get; private set; }

        public void Apply(Directive directive)
        {
            switch (directive.Name.ToLowerInvariant())
            {
                case "memo":
                case "note":
                    memo.Add(directive.Value);
                    break;
                case "unique-object":
                    IsUnique = true;
                    break;
                case "required-object":
                    IsRequired = true;
                    break;
                case "min-fields":
                    MinimumFields = ParseNonNegativeInt(directive);
                    break;
                case "extensible":
                    ExtensibleGroupSize = ParseNonNegativeInt(directive);
                    break;
                case "format":
                    Format = directive.Value;
                    break;
                case "obsolete":
                    ObsoleteMessage = directive.Value;
                    break;
                default:
                    Add(additional, directive);
                    break;
            }
        }

        public IddObjectDefinition Build()
        {
            IEnumerable<IddFieldDefinition> canonicalFields = Fields;
            int markedStart = Fields.FindIndex(field => field.BeginsExtensible);
            if (markedStart >= 0 &&
                ExtensibleGroupSize > 0 &&
                Fields.Count > markedStart + ExtensibleGroupSize)
            {
                canonicalFields = Fields.Take(markedStart + ExtensibleGroupSize).ToArray();
            }

            return new IddObjectDefinition(
                Name,
                Group,
                canonicalFields,
                memo,
                IsUnique,
                IsRequired,
                MinimumFields,
                ExtensibleGroupSize,
                Format,
                ObsoleteMessage,
                ToReadOnly(additional));
        }
    }

    private sealed class FieldBuilder
    {
        private readonly List<string> notes = new();
        private readonly List<string> choices = new();
        private readonly List<string> objectLists = new();
        private readonly List<string> references = new();
        private readonly List<string> referenceClassNames = new();
        private readonly Dictionary<string, List<string>> additional = new(StringComparer.OrdinalIgnoreCase);

        public FieldBuilder(string token, int position)
        {
            Token = token;
            Position = position;
        }

        public string Token { get; }

        public int Position { get; }

        public string Name { get; private set; } = string.Empty;

        public string? Units { get; private set; }

        public string? IpUnits { get; private set; }

        public string? UnitsBasedOnField { get; private set; }

        public bool IsRequired { get; private set; }

        public bool BeginsExtensible { get; private set; }

        public bool IsDeprecated { get; private set; }

        public bool IsAutosizable { get; private set; }

        public bool IsAutocalculatable { get; private set; }

        public bool RetainsCase { get; private set; }

        public string? DefaultValue { get; private set; }

        public IddDataType DataType { get; private set; }

        public string? ExternalList { get; private set; }

        public IddNumericBound? Minimum { get; private set; }

        public IddNumericBound? Maximum { get; private set; }

        public void Apply(Directive directive)
        {
            switch (directive.Name.ToLowerInvariant())
            {
                case "field":
                    Name = directive.Value;
                    break;
                case "note":
                case "memo":
                    notes.Add(directive.Value);
                    break;
                case "required-field":
                case "required":
                    IsRequired = true;
                    break;
                case "begin-extensible":
                    BeginsExtensible = true;
                    break;
                case "units":
                    Units = directive.Value;
                    break;
                case "ip-units":
                    IpUnits = directive.Value;
                    break;
                case "unitsbasedonfield":
                    UnitsBasedOnField = directive.Value;
                    break;
                case "minimum":
                    Minimum = new IddNumericBound(
                        ParseDouble(directive),
                        isInclusive: !HasLeadingOperator(directive.Value, '>'));
                    break;
                case "minimum>":
                    Minimum = new IddNumericBound(ParseDouble(directive), isInclusive: false);
                    break;
                case "maximum":
                    Maximum = new IddNumericBound(
                        ParseDouble(directive),
                        isInclusive: !HasLeadingOperator(directive.Value, '<'));
                    break;
                case "maximum<":
                    Maximum = new IddNumericBound(ParseDouble(directive), isInclusive: false);
                    break;
                case "default":
                    DefaultValue = directive.Value;
                    break;
                case "deprecated":
                    IsDeprecated = true;
                    break;
                case "autosizable":
                    IsAutosizable = true;
                    break;
                case "autocalculatable":
                    IsAutocalculatable = true;
                    break;
                case "type":
                    DataType = ParseType(directive.Value);
                    break;
                case "retaincase":
                    RetainsCase = true;
                    break;
                case "key":
                    choices.Add(directive.Value);
                    break;
                case "object-list":
                    objectLists.Add(directive.Value);
                    break;
                case "external-list":
                    ExternalList = directive.Value;
                    break;
                case "reference":
                    references.Add(directive.Value);
                    break;
                case "reference-class-name":
                    referenceClassNames.Add(directive.Value);
                    break;
                default:
                    Add(additional, directive);
                    break;
            }
        }

        public IddFieldDefinition Build()
        {
            IddFieldKind kind = Token[0] == 'N' ? IddFieldKind.Numeric : IddFieldKind.Alpha;
            IddDataType resolvedType = DataType == IddDataType.Unspecified
                ? kind == IddFieldKind.Numeric ? IddDataType.Real : IddDataType.Alpha
                : DataType;
            return new IddFieldDefinition(
                Token,
                Position,
                kind,
                Name,
                notes,
                Units,
                IpUnits,
                UnitsBasedOnField,
                IsRequired,
                BeginsExtensible,
                IsDeprecated,
                IsAutosizable,
                IsAutocalculatable,
                RetainsCase,
                DefaultValue,
                resolvedType,
                choices,
                objectLists,
                ExternalList,
                references,
                referenceClassNames,
                Minimum,
                Maximum,
                ToReadOnly(additional));
        }
    }

    private sealed class Directive
    {
        public Directive(string name, string value)
        {
            Name = name;
            Value = value.Trim();
        }

        public string Name { get; }

        public string Value { get; }
    }

    private static int ParseNonNegativeInt(Directive directive)
    {
        if (!int.TryParse(directive.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 0)
        {
            throw new FormatException($"Invalid value for \\{directive.Name}: '{directive.Value}'.");
        }

        return value;
    }

    private static double ParseDouble(Directive directive)
    {
        string text = directive.Value;
        if (HasLeadingOperator(text, '>') || HasLeadingOperator(text, '<'))
        {
            text = text.Substring(1).TrimStart();
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new FormatException($"Invalid value for \\{directive.Name}: '{directive.Value}'.");
        }

        return value;
    }

    private static bool HasLeadingOperator(string value, char expected)
    {
        string trimmed = value.TrimStart();
        return trimmed.Length > 0 && trimmed[0] == expected;
    }

    private static IddDataType ParseType(string value)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "alpha": return IddDataType.Alpha;
            case "choice": return IddDataType.Choice;
            case "object-list": return IddDataType.ObjectList;
            case "external-list": return IddDataType.ExternalList;
            case "node": return IddDataType.Node;
            case "integer": return IddDataType.IntegerNumber;
            case "real": return IddDataType.Real;
            default: return IddDataType.Unspecified;
        }
    }

    private static void Add(IDictionary<string, List<string>> target, Directive directive)
    {
        if (!target.TryGetValue(directive.Name, out List<string>? values))
        {
            values = new List<string>();
            target.Add(directive.Name, values);
        }

        values.Add(directive.Value);
    }

    private static Dictionary<string, IReadOnlyList<string>> ToReadOnly(
        IReadOnlyDictionary<string, List<string>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }
}
