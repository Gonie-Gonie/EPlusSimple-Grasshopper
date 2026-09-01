using System.IO.Compression;
using System.Text.Json;
using Dragons.InvisibleDragon.Idd;

namespace Dragons.InvisibleDragon.Tests.Idd;

public sealed class IddSchemaOracleTests
{
    private const string OracleSchema = "dragons.energyplus-idd-schema.v1";
    private const string EnergyPlusVersion = "24.2.0";
    private const string EnergyPlusBuild = "94a887817b";
    private const string EnergyPlusIddSha256 = "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const string EnergyPlusEpJsonSchemaSha256 = "aefb16d63495d170468ecab3c935f1aeb68eb07c6551403dd11cbba61cb136fa";
    private const long EnergyPlusEpJsonSchemaBytes = 10469751;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [EnergyPlusIddIntegrationFact]
    [Trait("Category", "Integration")]
    public void EnergyPlus242FullSchemaMatchesRawIddRegressionOracleWhenRuntimeReady()
    {
        string repository = FindRepositoryRoot();
        IddOracle expected = ReadOracle(FindOraclePath(repository));

        Assert.Equal(OracleSchema, expected.OracleSchema);
        Assert.Equal(EnergyPlusVersion, expected.EnergyplusVersion);
        Assert.Equal(EnergyPlusBuild, expected.EnergyplusBuild);
        Assert.Equal(EnergyPlusIddSha256, expected.SourceSha256);
        Assert.Equal(848, expected.ObjectCount);
        Assert.Equal(13702, expected.FieldCount);

        using (JsonDocument lockDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repository, "upstream", "upstream.lock.json"))))
        {
            Assert.Equal(
                lockDocument.RootElement.GetProperty("commit").GetString(),
                expected.UpstreamCommit);
        }

        string iddPath = FindEnergyPlusIdd()
            ?? throw new FileNotFoundException(
                "DRAGONS_RUN_ENERGYPLUS_INTEGRATION=1, but no installed EnergyPlus 24.2 Energy+.idd was found.");

        var source = new FileInfo(iddPath);
        IddSchema actual = IddParser.ParseFile(iddPath);
        Assert.Equal(expected.SourceBytes, source.Length);
        Assert.Equal(expected.SourceSha256, actual.SourceSha256);
        Assert.Equal(expected.EnergyplusVersion, actual.Version);
        Assert.Equal(expected.EnergyplusBuild, actual.Build);
        Assert.Equal(expected.ObjectCount, actual.Objects.Count);
        Assert.Equal(expected.Groups, actual.Groups);
        Assert.Equal(expected.Objects.Count, actual.Objects.Count);

        int comparedFields = 0;
        for (int objectIndex = 0; objectIndex < expected.Objects.Count; objectIndex++)
        {
            OracleObject expectedObject = expected.Objects[objectIndex];
            IddObjectDefinition actualObject = actual.Objects[objectIndex];
            CompareObject(expectedObject, actualObject, objectIndex);
            comparedFields += actualObject.Fields.Count;
        }

        Assert.Equal(expected.FieldCount, comparedFields);
    }

    [Fact]
    public void RawIddRegressionOracleRecordsOfficialEpJsonSemanticValidation()
    {
        string repository = FindRepositoryRoot();
        IddOracle expected = ReadOracle(FindOraclePath(repository));
        OfficialEpJsonSchema official = Assert.IsType<OfficialEpJsonSchema>(expected.OfficialEpjsonSchema);

        Assert.Equal("https://json-schema.org/draft-07/schema#", official.SchemaDraft);
        Assert.Equal("24.2", official.EnergyplusVersion);
        Assert.Equal(EnergyPlusBuild, official.PairedEnergyplusBuild);
        Assert.Equal(EnergyPlusEpJsonSchemaSha256, official.SourceSha256);
        Assert.Equal(EnergyPlusEpJsonSchemaBytes, official.SourceBytes);
        Assert.Equal(848, official.ObjectCount);
        Assert.Equal(13469, official.FieldDefinitionCount);
        Assert.Equal(13469, official.ValidatedFieldOccurrenceCount);
        Assert.Equal(120, official.ExtensibleObjectCount);
        Assert.Equal(256, official.ExtensiblePrototypeFieldCount);
        Assert.Equal(6, official.UnrepresentedFieldTopologyObjectCount);
        Assert.Equal(18, official.OfficialEnumSupersetFieldCount);
        Assert.True(official.UnrepresentedNodeTypeCount > 0);
        Assert.True(official.UnrepresentedRequiredFlagCount > 0);
        Assert.Equal(
            new[]
            {
                "object-name-order-group-unique-required-min-fields-format",
                "field-order-name-kind-required-default-enum",
                "field-type-units-bounds-autosize-autocalculate",
                "field-object-list-external-list-reference-reference-class",
                "extensible-start-size-prototype-kind-cycle",
            },
            official.ValidatedDimensions);
        Assert.Contains(
            official.NotComparedMetadata,
            value => value.Contains("node versus unconstrained alpha", StringComparison.Ordinal));
        Assert.Contains(
            official.NotComparedMetadata,
            value => value.Contains("ground-heat-transfer face objects", StringComparison.Ordinal));
    }

    private static void CompareObject(
        OracleObject expected,
        IddObjectDefinition actual,
        int position)
    {
        Assert.Equal(expected.Position, position);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Group, actual.Group);
        Assert.Equal(expected.Memo, actual.Memo);
        Assert.Equal(expected.IsUnique, actual.IsUnique);
        Assert.Equal(expected.IsRequired, actual.IsRequired);
        Assert.Equal(expected.MinimumFields, actual.MinimumFields);
        Assert.Equal(expected.ExtensibleGroupSize, actual.ExtensibleGroupSize);
        Assert.Equal(expected.ExtensibleStartIndex, actual.ExtensibleStartIndex);
        Assert.Equal(expected.Format, actual.Format);
        Assert.Equal(expected.ObsoleteMessage, actual.ObsoleteMessage);
        CompareDirectives(expected.AdditionalDirectives, actual.AdditionalDirectives);
        Assert.Equal(expected.Fields.Count, actual.Fields.Count);

        for (int fieldIndex = 0; fieldIndex < expected.Fields.Count; fieldIndex++)
        {
            CompareField(expected.Fields[fieldIndex], actual.Fields[fieldIndex], fieldIndex);
        }

        if (expected.ExtensibleStartIndex is int startIndex)
        {
            Assert.True(expected.ExtensibleGroupSize > 0);
            for (int offset = 0; offset < expected.ExtensibleGroupSize; offset++)
            {
                int resolvedIndex = startIndex +
                    ((actual.Fields.Count + offset - startIndex) % expected.ExtensibleGroupSize);
                Assert.Same(
                    actual.Fields[resolvedIndex],
                    actual.ResolveField(actual.Fields.Count + offset));
            }
        }
    }

    private static void CompareField(
        OracleField expected,
        IddFieldDefinition actual,
        int position)
    {
        Assert.Equal(expected.Position, position);
        Assert.Equal(expected.Token, actual.Token);
        Assert.Equal(expected.Kind, KindName(actual.Kind));
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.Units, actual.Units);
        Assert.Equal(expected.IpUnits, actual.IpUnits);
        Assert.Equal(expected.UnitsBasedOnField, actual.UnitsBasedOnField);
        Assert.Equal(expected.IsRequired, actual.IsRequired);
        Assert.Equal(expected.BeginsExtensible, actual.BeginsExtensible);
        Assert.Equal(expected.IsDeprecated, actual.IsDeprecated);
        Assert.Equal(expected.IsAutosizable, actual.IsAutosizable);
        Assert.Equal(expected.IsAutocalculatable, actual.IsAutocalculatable);
        Assert.Equal(expected.RetainsCase, actual.RetainsCase);
        Assert.Equal(expected.DefaultValue, actual.DefaultValue);
        Assert.Equal(expected.DataType, DataTypeName(actual.DataType));
        Assert.Equal(expected.Choices, actual.Choices);
        Assert.Equal(expected.ObjectLists, actual.ObjectLists);
        Assert.Equal(expected.ExternalList, actual.ExternalList);
        Assert.Equal(expected.References, actual.References);
        Assert.Equal(expected.ReferenceClassNames, actual.ReferenceClassNames);
        CompareBound(expected.Minimum, actual.Minimum);
        CompareBound(expected.Maximum, actual.Maximum);
        CompareDirectives(expected.AdditionalDirectives, actual.AdditionalDirectives);
    }

    private static void CompareBound(OracleBound? expected, IddNumericBound? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.Equal(expected.Value, actual!.Value);
        Assert.Equal(expected.IsInclusive, actual.IsInclusive);
    }

    private static void CompareDirectives(
        IReadOnlyDictionary<string, List<string>> expected,
        IReadOnlyDictionary<string, IReadOnlyList<string>> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (KeyValuePair<string, List<string>> directive in expected)
        {
            string actualName = Assert.Single(
                actual.Keys,
                name => string.Equals(name, directive.Key, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(directive.Key, actualName);
            Assert.Equal(directive.Value, actual[actualName]);
        }
    }

    private static string KindName(IddFieldKind value)
    {
        return value switch
        {
            IddFieldKind.Alpha => "alpha",
            IddFieldKind.Numeric => "numeric",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown IDD field kind."),
        };
    }

    private static string DataTypeName(IddDataType value)
    {
        return value switch
        {
            IddDataType.Unspecified => "unspecified",
            IddDataType.Alpha => "alpha",
            IddDataType.Choice => "choice",
            IddDataType.ObjectList => "object-list",
            IddDataType.ExternalList => "external-list",
            IddDataType.Node => "node",
            IddDataType.IntegerNumber => "integer-number",
            IddDataType.Real => "real",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown IDD data type."),
        };
    }

    private static IddOracle ReadOracle(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var compressed = new GZipStream(input, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<IddOracle>(compressed, JsonOptions)
            ?? throw new InvalidDataException($"The IDD oracle is empty: '{path}'.");
    }

    private static string? FindEnergyPlusIdd()
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("DRAGONS_ENERGYPLUS_ROOT"),
            Environment.GetEnvironmentVariable("DRAGONS_ENERGYPLUS_HOME"),
            Environment.GetEnvironmentVariable("ENERGYPLUS_HOME"),
            Environment.GetEnvironmentVariable("ENERGYPLUS_ROOT"),
            @"C:\EnergyPlusV24-2-0",
        };
        foreach (string? root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string candidate = Path.Combine(root, "Energy+.idd");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string FindOraclePath(string repository)
    {
        return Path.Combine(
            repository,
            "fixtures",
            "reference",
            "python-0.7.0",
            "idd-24.2.0.schema.json.gz");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class IddOracle
    {
        public string OracleSchema { get; set; } = string.Empty;

        public string UpstreamCommit { get; set; } = string.Empty;

        public string EnergyplusVersion { get; set; } = string.Empty;

        public string EnergyplusBuild { get; set; } = string.Empty;

        public string SourceSha256 { get; set; } = string.Empty;

        public long SourceBytes { get; set; }

        public int ObjectCount { get; set; }

        public int FieldCount { get; set; }

        public List<string> Groups { get; set; } = new();

        public List<OracleObject> Objects { get; set; } = new();

        public OfficialEpJsonSchema? OfficialEpjsonSchema { get; set; }
    }

    private sealed class OfficialEpJsonSchema
    {
        public string SchemaDraft { get; set; } = string.Empty;

        public string EnergyplusVersion { get; set; } = string.Empty;

        public string PairedEnergyplusBuild { get; set; } = string.Empty;

        public string SourceSha256 { get; set; } = string.Empty;

        public long SourceBytes { get; set; }

        public int ObjectCount { get; set; }

        public int FieldDefinitionCount { get; set; }

        public int ValidatedFieldOccurrenceCount { get; set; }

        public int ExtensibleObjectCount { get; set; }

        public int ExtensiblePrototypeFieldCount { get; set; }

        public int UnrepresentedFieldTopologyObjectCount { get; set; }

        public int OfficialEnumSupersetFieldCount { get; set; }

        public int UnrepresentedNodeTypeCount { get; set; }

        public int UnrepresentedRequiredFlagCount { get; set; }

        public List<string> ValidatedDimensions { get; set; } = new();

        public List<string> NotComparedMetadata { get; set; } = new();
    }

    private sealed class OracleObject
    {
        public int Position { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public List<string> Memo { get; set; } = new();

        public bool IsUnique { get; set; }

        public bool IsRequired { get; set; }

        public int MinimumFields { get; set; }

        public int ExtensibleGroupSize { get; set; }

        public int? ExtensibleStartIndex { get; set; }

        public string? Format { get; set; }

        public string? ObsoleteMessage { get; set; }

        public Dictionary<string, List<string>> AdditionalDirectives { get; set; } = new();

        public List<OracleField> Fields { get; set; } = new();
    }

    private sealed class OracleField
    {
        public string Token { get; set; } = string.Empty;

        public int Position { get; set; }

        public string Kind { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<string> Notes { get; set; } = new();

        public string? Units { get; set; }

        public string? IpUnits { get; set; }

        public string? UnitsBasedOnField { get; set; }

        public bool IsRequired { get; set; }

        public bool BeginsExtensible { get; set; }

        public bool IsDeprecated { get; set; }

        public bool IsAutosizable { get; set; }

        public bool IsAutocalculatable { get; set; }

        public bool RetainsCase { get; set; }

        public string? DefaultValue { get; set; }

        public string DataType { get; set; } = string.Empty;

        public List<string> Choices { get; set; } = new();

        public List<string> ObjectLists { get; set; } = new();

        public string? ExternalList { get; set; }

        public List<string> References { get; set; } = new();

        public List<string> ReferenceClassNames { get; set; } = new();

        public OracleBound? Minimum { get; set; }

        public OracleBound? Maximum { get; set; }

        public Dictionary<string, List<string>> AdditionalDirectives { get; set; } = new();
    }

    private sealed class OracleBound
    {
        public double Value { get; set; }

        public bool IsInclusive { get; set; }
    }

    public sealed class EnergyPlusIddIntegrationFactAttribute : FactAttribute
    {
        public EnergyPlusIddIntegrationFactAttribute()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("DRAGONS_RUN_ENERGYPLUS_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
            {
                Skip = "Set DRAGONS_RUN_ENERGYPLUS_INTEGRATION=1 to compare the installed EnergyPlus 24.2 IDD.";
            }
        }
    }
}
