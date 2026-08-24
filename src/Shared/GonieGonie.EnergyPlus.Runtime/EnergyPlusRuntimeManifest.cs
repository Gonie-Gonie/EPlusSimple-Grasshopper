using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GonieGonie.EnergyPlus.Runtime;

/// <summary>
/// The pinned identity of an EnergyPlus payload and its distributable metadata.
/// </summary>
public sealed record EnergyPlusRuntimeManifest(
    string RuntimeSchema,
    string EnergyPlusVersion,
    string EnergyPlusBuild,
    string EnergyPlusArchiveSha256,
    long EnergyPlusArchiveSize,
    string EnergyPlusExecutableSha256,
    string EnergyPlusIddSha256,
    string ExpandObjectsSha256,
    string WeatherPackVersion,
    string WeatherPackSha256,
    string CreatedBy)
{
    public const string SupportedSchema = "goniegonie.energyplus-runtime.v1";

    public static EnergyPlusRuntimeManifest Supported { get; } = new(
        SupportedSchema,
        "24.2.0",
        "94a887817b",
        "26c7c22b731f54031626750284c8b613fb8f03c3aa56b6bc7ec65b6bf8668df1",
        179248139,
        "95f6047e26b9144fcff7771a85afb1e09da1f2434b748b24c092a0be5ac94728",
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2",
        "a15bd8e10f6a004e270fa4761527cabc95f776c089b92c11603faba19ed541ae",
        "weather/v1",
        "fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0",
        "GonieGonie-Dragons");

    /// <summary>
    /// Loads and structurally validates a UTF-8 runtime manifest.
    /// </summary>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    /// <exception cref="InvalidDataException">The JSON or required manifest data is invalid.</exception>
    public static EnergyPlusRuntimeManifest Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A manifest path is required.", nameof(path));
        }

        RuntimeManifestDto? dto;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var serializer = new DataContractJsonSerializer(typeof(RuntimeManifestDto));
            dto = serializer.ReadObject(stream) as RuntimeManifestDto;
        }
        catch (SerializationException exception)
        {
            throw new InvalidDataException("The runtime manifest is not valid JSON.", exception);
        }

        if (dto is null)
        {
            throw new InvalidDataException("The runtime manifest is empty.");
        }

        var manifest = new EnergyPlusRuntimeManifest(
            dto.RuntimeSchema ?? string.Empty,
            dto.EnergyPlusVersion ?? string.Empty,
            dto.EnergyPlusBuild ?? string.Empty,
            dto.EnergyPlusArchiveSha256 ?? string.Empty,
            dto.EnergyPlusArchiveSize,
            dto.EnergyPlusExecutableSha256 ?? string.Empty,
            dto.EnergyPlusIddSha256 ?? string.Empty,
            dto.ExpandObjectsSha256 ?? string.Empty,
            dto.WeatherPackVersion ?? string.Empty,
            dto.WeatherPackSha256 ?? string.Empty,
            dto.CreatedBy ?? string.Empty);

        var errors = manifest.Validate();
        if (errors.Count != 0)
        {
            throw new InvalidDataException(string.Join(" ", errors));
        }

        return manifest;
    }

    /// <summary>
    /// Serializes this manifest with stable field names and UTF-8 encoding.
    /// </summary>
    public string ToJson()
    {
        var dto = new RuntimeManifestDto
        {
            RuntimeSchema = RuntimeSchema,
            EnergyPlusVersion = EnergyPlusVersion,
            EnergyPlusBuild = EnergyPlusBuild,
            EnergyPlusArchiveSha256 = EnergyPlusArchiveSha256,
            EnergyPlusArchiveSize = EnergyPlusArchiveSize,
            EnergyPlusExecutableSha256 = EnergyPlusExecutableSha256,
            EnergyPlusIddSha256 = EnergyPlusIddSha256,
            ExpandObjectsSha256 = ExpandObjectsSha256,
            WeatherPackVersion = WeatherPackVersion,
            WeatherPackSha256 = WeatherPackSha256,
            CreatedBy = CreatedBy
        };

        using var stream = new MemoryStream();
        var serializer = new DataContractJsonSerializer(typeof(RuntimeManifestDto));
        serializer.WriteObject(stream, dto);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Returns all structural validation errors in deterministic field order.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        RequireText(RuntimeSchema, "runtime_schema", errors);
        RequireText(EnergyPlusVersion, "energyplus_version", errors);
        RequireText(EnergyPlusBuild, "energyplus_build", errors);
        RequireSha256(EnergyPlusArchiveSha256, "energyplus_archive_sha256", errors);
        if (EnergyPlusArchiveSize <= 0)
        {
            errors.Add("energyplus_archive_size must be greater than zero.");
        }

        RequireSha256(EnergyPlusExecutableSha256, "energyplus_exe_sha256", errors);
        RequireSha256(EnergyPlusIddSha256, "energyplus_idd_sha256", errors);
        RequireSha256(ExpandObjectsSha256, "expandobjects_sha256", errors);
        RequireText(WeatherPackVersion, "weather_pack_version", errors);
        RequireSha256(WeatherPackSha256, "weather_pack_sha256", errors);
        RequireText(CreatedBy, "created_by", errors);
        return errors;
    }

    internal IReadOnlyList<string> CompareWith(EnergyPlusRuntimeManifest expected)
    {
        var differences = new List<string>();
        Compare(RuntimeSchema, expected.RuntimeSchema, "runtime_schema", differences);
        Compare(EnergyPlusVersion, expected.EnergyPlusVersion, "energyplus_version", differences);
        Compare(EnergyPlusBuild, expected.EnergyPlusBuild, "energyplus_build", differences);
        CompareHash(EnergyPlusArchiveSha256, expected.EnergyPlusArchiveSha256, "energyplus_archive_sha256", differences);
        if (EnergyPlusArchiveSize != expected.EnergyPlusArchiveSize)
        {
            differences.Add("energyplus_archive_size does not match the pinned manifest.");
        }

        CompareHash(EnergyPlusExecutableSha256, expected.EnergyPlusExecutableSha256, "energyplus_exe_sha256", differences);
        CompareHash(EnergyPlusIddSha256, expected.EnergyPlusIddSha256, "energyplus_idd_sha256", differences);
        CompareHash(ExpandObjectsSha256, expected.ExpandObjectsSha256, "expandobjects_sha256", differences);
        Compare(WeatherPackVersion, expected.WeatherPackVersion, "weather_pack_version", differences);
        CompareHash(WeatherPackSha256, expected.WeatherPackSha256, "weather_pack_sha256", differences);
        Compare(CreatedBy, expected.CreatedBy, "created_by", differences);
        return differences;
    }

    private static void RequireText(string value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }

    private static void RequireSha256(string value, string fieldName, List<string> errors)
    {
        if (value.Length != 64 || value.Any(character => !IsHex(character)))
        {
            errors.Add($"{fieldName} must be a 64-character SHA-256 value.");
        }
    }

    private static bool IsHex(char value)
    {
        return (value >= '0' && value <= '9')
            || (value >= 'a' && value <= 'f')
            || (value >= 'A' && value <= 'F');
    }

    private static void Compare(string actual, string expected, string fieldName, List<string> differences)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            differences.Add($"{fieldName} does not match the pinned manifest.");
        }
    }

    private static void CompareHash(string actual, string expected, string fieldName, List<string> differences)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add($"{fieldName} does not match the pinned manifest.");
        }
    }

    [DataContract]
    private sealed class RuntimeManifestDto
    {
        [DataMember(Name = "runtime_schema", IsRequired = true)]
        public string? RuntimeSchema { get; set; }

        [DataMember(Name = "energyplus_version", IsRequired = true)]
        public string? EnergyPlusVersion { get; set; }

        [DataMember(Name = "energyplus_build", IsRequired = true)]
        public string? EnergyPlusBuild { get; set; }

        [DataMember(Name = "energyplus_archive_sha256", IsRequired = true)]
        public string? EnergyPlusArchiveSha256 { get; set; }

        [DataMember(Name = "energyplus_archive_size", IsRequired = true)]
        public long EnergyPlusArchiveSize { get; set; }

        [DataMember(Name = "energyplus_exe_sha256", IsRequired = true)]
        public string? EnergyPlusExecutableSha256 { get; set; }

        [DataMember(Name = "energyplus_idd_sha256", IsRequired = true)]
        public string? EnergyPlusIddSha256 { get; set; }

        [DataMember(Name = "expandobjects_sha256", IsRequired = true)]
        public string? ExpandObjectsSha256 { get; set; }

        [DataMember(Name = "weather_pack_version", IsRequired = true)]
        public string? WeatherPackVersion { get; set; }

        [DataMember(Name = "weather_pack_sha256", IsRequired = true)]
        public string? WeatherPackSha256 { get; set; }

        [DataMember(Name = "created_by", IsRequired = true)]
        public string? CreatedBy { get; set; }
    }
}
