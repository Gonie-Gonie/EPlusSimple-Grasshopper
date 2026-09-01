using System.IO.Compression;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Idd;

/// <summary>
/// Reads and writes portable JSON/GZip IDD caches that are bound to the source file SHA-256.
/// </summary>
public static class IddSchemaCache
{
    public const string CacheSchema = "dragons.invisible-dragon.idd-cache.v1";

    public static void Write(string path, IddSchema schema)
    {
        Guard.NotNull(path, nameof(path));
        Guard.NotNull(schema, nameof(schema));

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(output, schema, leaveOpen: false);
    }

    public static void Write(Stream output, IddSchema schema, bool leaveOpen = false)
    {
        Guard.NotNull(output, nameof(output));
        Guard.NotNull(schema, nameof(schema));

        var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen);
        try
        {
            JsonSerializer.Serialize(gzip, CacheDocument.FromSchema(schema), BuildingEnergyJson.CreateOptions());
        }
        finally
        {
            gzip.Dispose();
        }
    }

    public static IddSchema Read(string path, string expectedSourceSha256)
    {
        Guard.NotNull(path, nameof(path));
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(input, expectedSourceSha256, leaveOpen: false);
    }

    public static IddSchema Read(Stream input, string expectedSourceSha256, bool leaveOpen = false)
    {
        Guard.NotNull(input, nameof(input));
        string expectedHash = IddSchema.NormalizeSha256(expectedSourceSha256);
        var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen);
        CacheDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<CacheDocument>(gzip, BuildingEnergyJson.CreateOptions());
        }
        finally
        {
            gzip.Dispose();
        }

        if (document is null)
        {
            throw new InvalidDataException("The IDD cache contains no document.");
        }

        if (!string.Equals(document.CacheSchema, CacheSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported IDD cache schema '{document.CacheSchema}'.");
        }

        string actualHash = IddSchema.NormalizeSha256(document.SourceSha256);
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"IDD cache source hash mismatch. Expected '{expectedHash}', found '{actualHash}'.");
        }

        return document.ToSchema();
    }

    public static bool TryRead(string path, string expectedSourceSha256, out IddSchema? schema)
    {
        try
        {
            schema = Read(path, expectedSourceSha256);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            schema = null;
            return false;
        }
    }

    private sealed class CacheDocument
    {
        public string CacheSchema { get; set; } = string.Empty;

        public string SourceSha256 { get; set; } = string.Empty;

        public string EnergyPlusVersion { get; set; } = string.Empty;

        public string EnergyPlusBuild { get; set; } = string.Empty;

        public List<ObjectDocument> Objects { get; set; } = new();

        public static CacheDocument FromSchema(IddSchema schema)
        {
            return new CacheDocument
            {
                CacheSchema = IddSchemaCache.CacheSchema,
                SourceSha256 = schema.SourceSha256,
                EnergyPlusVersion = schema.Version,
                EnergyPlusBuild = schema.Build,
                Objects = schema.Objects.Select(ObjectDocument.FromDefinition).ToList(),
            };
        }

        public IddSchema ToSchema()
        {
            return new IddSchema(
                EnergyPlusVersion,
                EnergyPlusBuild,
                SourceSha256,
                Objects.Select(item => item.ToDefinition()));
        }
    }

    private sealed class ObjectDocument
    {
        public string Name { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public List<string> Memo { get; set; } = new();

        public bool IsUnique { get; set; }

        public bool IsRequired { get; set; }

        public int MinimumFields { get; set; }

        public int ExtensibleGroupSize { get; set; }

        public string? Format { get; set; }

        public string? ObsoleteMessage { get; set; }

        public List<FieldDocument> Fields { get; set; } = new();

        public Dictionary<string, List<string>> AdditionalDirectives { get; set; } = new();

        public static ObjectDocument FromDefinition(IddObjectDefinition definition)
        {
            return new ObjectDocument
            {
                Name = definition.Name,
                Group = definition.Group,
                Memo = definition.Memo.ToList(),
                IsUnique = definition.IsUnique,
                IsRequired = definition.IsRequired,
                MinimumFields = definition.MinimumFields,
                ExtensibleGroupSize = definition.ExtensibleGroupSize,
                Format = definition.Format,
                ObsoleteMessage = definition.ObsoleteMessage,
                Fields = definition.Fields.Select(FieldDocument.FromDefinition).ToList(),
                AdditionalDirectives = ToMutable(definition.AdditionalDirectives),
            };
        }

        public IddObjectDefinition ToDefinition()
        {
            return new IddObjectDefinition(
                Name,
                Group,
                Fields.Select(item => item.ToDefinition()),
                Memo,
                IsUnique,
                IsRequired,
                MinimumFields,
                ExtensibleGroupSize,
                Format,
                ObsoleteMessage,
                ToReadOnly(AdditionalDirectives));
        }
    }

    private sealed class FieldDocument
    {
        public string Token { get; set; } = string.Empty;

        public int Position { get; set; }

        public IddFieldKind Kind { get; set; }

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

        public IddDataType DataType { get; set; }

        public List<string> Choices { get; set; } = new();

        public List<string> ObjectLists { get; set; } = new();

        public string? ExternalList { get; set; }

        public List<string> References { get; set; } = new();

        public List<string> ReferenceClassNames { get; set; } = new();

        public BoundDocument? Minimum { get; set; }

        public BoundDocument? Maximum { get; set; }

        public Dictionary<string, List<string>> AdditionalDirectives { get; set; } = new();

        public static FieldDocument FromDefinition(IddFieldDefinition definition)
        {
            return new FieldDocument
            {
                Token = definition.Token,
                Position = definition.Position,
                Kind = definition.Kind,
                Name = definition.Name,
                Notes = definition.Notes.ToList(),
                Units = definition.Units,
                IpUnits = definition.IpUnits,
                UnitsBasedOnField = definition.UnitsBasedOnField,
                IsRequired = definition.IsRequired,
                BeginsExtensible = definition.BeginsExtensible,
                IsDeprecated = definition.IsDeprecated,
                IsAutosizable = definition.IsAutosizable,
                IsAutocalculatable = definition.IsAutocalculatable,
                RetainsCase = definition.RetainsCase,
                DefaultValue = definition.DefaultValue,
                DataType = definition.DataType,
                Choices = definition.Choices.ToList(),
                ObjectLists = definition.ObjectLists.ToList(),
                ExternalList = definition.ExternalList,
                References = definition.References.ToList(),
                ReferenceClassNames = definition.ReferenceClassNames.ToList(),
                Minimum = BoundDocument.FromBound(definition.Minimum),
                Maximum = BoundDocument.FromBound(definition.Maximum),
                AdditionalDirectives = ToMutable(definition.AdditionalDirectives),
            };
        }

        public IddFieldDefinition ToDefinition()
        {
            return new IddFieldDefinition(
                Token,
                Position,
                Kind,
                Name,
                Notes,
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
                DataType,
                Choices,
                ObjectLists,
                ExternalList,
                References,
                ReferenceClassNames,
                Minimum?.ToBound(),
                Maximum?.ToBound(),
                ToReadOnly(AdditionalDirectives));
        }
    }

    private sealed class BoundDocument
    {
        public double Value { get; set; }

        public bool IsInclusive { get; set; }

        public static BoundDocument? FromBound(IddNumericBound? bound)
        {
            return bound is null ? null : new BoundDocument { Value = bound.Value, IsInclusive = bound.IsInclusive };
        }

        public IddNumericBound ToBound()
        {
            return new IddNumericBound(Value, IsInclusive);
        }
    }

    private static Dictionary<string, List<string>> ToMutable(
        IReadOnlyDictionary<string, IReadOnlyList<string>> source)
    {
        return source.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IReadOnlyList<string>> ToReadOnly(
        IReadOnlyDictionary<string, List<string>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }
}
