using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon.Batch;

/// <summary>
/// Exact EnergyPlus payload identity used by deterministic simulation keys.
/// </summary>
public sealed class BatchRuntimeIdentity
{
    public BatchRuntimeIdentity(
        string energyPlusVersion,
        string energyPlusBuild,
        string energyPlusExecutableSha256,
        string energyPlusIddSha256,
        string expandObjectsSha256)
    {
        EnergyPlusVersion = Required(energyPlusVersion, nameof(energyPlusVersion));
        EnergyPlusBuild = Required(energyPlusBuild, nameof(energyPlusBuild));
        EnergyPlusExecutableSha256 = NormalizeSha256(
            energyPlusExecutableSha256,
            nameof(energyPlusExecutableSha256));
        EnergyPlusIddSha256 = NormalizeSha256(energyPlusIddSha256, nameof(energyPlusIddSha256));
        ExpandObjectsSha256 = NormalizeSha256(expandObjectsSha256, nameof(expandObjectsSha256));
    }

    public string EnergyPlusVersion { get; }

    public string EnergyPlusBuild { get; }

    public string EnergyPlusExecutableSha256 { get; }

    public string EnergyPlusIddSha256 { get; }

    public string ExpandObjectsSha256 { get; }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeSha256(string value, string parameterName)
    {
        string normalized = Required(value, parameterName).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !IsHex(character)))
        {
            throw new ArgumentException("A 64-character SHA-256 value is required.", parameterName);
        }

        return normalized;
    }

    private static bool IsHex(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f';
    }
}

/// <summary>
/// Complete deterministic simulation identity. It intentionally excludes paths, timestamps, and cache state.
/// </summary>
public sealed class BatchDeterministicInput
{
    public BatchDeterministicInput(
        string caseId,
        string canonicalModel,
        string canonicalCaseOptions,
        string executorIdentity,
        string canonicalExecutionOptions,
        string canonicalOutputOptions,
        string simpleDragonCoreVersion,
        string invisibleDragonCoreVersion,
        string upstreamRepository,
        string upstreamCommit,
        string upstreamVersion,
        BatchRuntimeIdentity runtime,
        string? weatherFileSha256)
    {
        CaseId = Required(caseId, nameof(caseId));
        CanonicalModel = Required(canonicalModel, nameof(canonicalModel));
        CanonicalCaseOptions = Required(canonicalCaseOptions, nameof(canonicalCaseOptions));
        ExecutorIdentity = Required(executorIdentity, nameof(executorIdentity));
        CanonicalExecutionOptions = Required(canonicalExecutionOptions, nameof(canonicalExecutionOptions));
        CanonicalOutputOptions = Required(canonicalOutputOptions, nameof(canonicalOutputOptions));
        SimpleDragonCoreVersion = Required(simpleDragonCoreVersion, nameof(simpleDragonCoreVersion));
        InvisibleDragonCoreVersion = Required(invisibleDragonCoreVersion, nameof(invisibleDragonCoreVersion));
        UpstreamRepository = Required(upstreamRepository, nameof(upstreamRepository));
        UpstreamCommit = Required(upstreamCommit, nameof(upstreamCommit));
        UpstreamVersion = Required(upstreamVersion, nameof(upstreamVersion));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        WeatherFileSha256 = weatherFileSha256 is null
            ? null
            : NormalizeSha256(weatherFileSha256, nameof(weatherFileSha256));
    }

    public string CaseId { get; }

    public string CanonicalModel { get; }

    public string CanonicalCaseOptions { get; }

    public string ExecutorIdentity { get; }

    public string CanonicalExecutionOptions { get; }

    public string CanonicalOutputOptions { get; }

    public string SimpleDragonCoreVersion { get; }

    public string InvisibleDragonCoreVersion { get; }

    public string UpstreamRepository { get; }

    public string UpstreamCommit { get; }

    public string UpstreamVersion { get; }

    public BatchRuntimeIdentity Runtime { get; }

    public string? WeatherFileSha256 { get; }

    public string CacheKey => BatchDeterminism.Sha256Text(ToCanonicalJson());

    public string ToCanonicalJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false,
            }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "dragons.simple-dragon.batch-input.v1");
            writer.WriteString("case_id", CaseId);
            writer.WriteString("canonical_model", CanonicalModel);
            writer.WriteString("canonical_case_options", CanonicalCaseOptions);
            writer.WriteString("executor_identity", ExecutorIdentity);
            writer.WriteString("canonical_execution_options", CanonicalExecutionOptions);
            writer.WriteString("canonical_output_options", CanonicalOutputOptions);
            writer.WriteString("simple_dragon_core_version", SimpleDragonCoreVersion);
            writer.WriteString("invisible_dragon_core_version", InvisibleDragonCoreVersion);
            writer.WriteString("upstream_repository", UpstreamRepository);
            writer.WriteString("upstream_commit", UpstreamCommit);
            writer.WriteString("upstream_version", UpstreamVersion);
            writer.WriteStartObject("energyplus_runtime");
            writer.WriteString("version", Runtime.EnergyPlusVersion);
            writer.WriteString("build", Runtime.EnergyPlusBuild);
            writer.WriteString("executable_sha256", Runtime.EnergyPlusExecutableSha256);
            writer.WriteString("idd_sha256", Runtime.EnergyPlusIddSha256);
            writer.WriteString("expandobjects_sha256", Runtime.ExpandObjectsSha256);
            writer.WriteEndObject();
            if (WeatherFileSha256 is null)
            {
                writer.WriteNull("weather_file_sha256");
            }
            else
            {
                writer.WriteString("weather_file_sha256", WeatherFileSha256);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeSha256(string value, string parameterName)
    {
        string normalized = Required(value, parameterName).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A 64-character SHA-256 value is required.", parameterName);
        }

        return normalized;
    }
}

/// <summary>
/// Reusable full-content hashing helpers for batch and single EnergyPlus runs.
/// </summary>
public static class BatchDeterminism
{
    /// <summary>
    /// Canonicalizes every GRM value that can affect conversion, including an explicit weather override.
    /// </summary>
    public static string CanonicalizeModel(GreenRetrofitModel model)
    {
        GreenRetrofitModel present = DomainSupport.NotNull(model, nameof(model));
        string grm = GrmWriter.Serialize(present, indented: false);
        WeatherSelection? weather = present.Weather;
        if (weather is null)
        {
            return grm + "\n{\"effective_weather_override\":null}";
        }

        WeatherMetadata metadata = weather.Metadata;
        string weatherIdentity = CanonicalizeOptions(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["administrative_area"] = metadata.AdministrativeArea,
            ["administrative_latitude"] = CanonicalDouble.Format(metadata.AdministrativeLatitude),
            ["administrative_longitude"] = CanonicalDouble.Format(metadata.AdministrativeLongitude),
            ["climate_effective_date"] = weather.ClimateEffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["climate_region"] = weather.ClimateRegion,
            ["epw_file_name"] = metadata.EpwFileName,
            ["legal_district_code"] = metadata.LegalDistrictCode,
            ["terrain"] = metadata.Terrain,
            ["weather_latitude"] = CanonicalDouble.Format(metadata.WeatherLatitude),
            ["weather_location"] = metadata.WeatherLocation,
            ["weather_location_type"] = metadata.WeatherLocationType,
            ["weather_longitude"] = CanonicalDouble.Format(metadata.WeatherLongitude),
            ["weather_metadata_id"] = metadata.Id.Value,
        });
        return grm + "\n{\"effective_weather_override\":" + weatherIdentity + "}";
    }

    public static string Sha256Text(string value)
    {
        return Sha256Bytes(Encoding.UTF8.GetBytes(DomainSupport.NotNull(value, nameof(value))));
    }

    public static string Sha256File(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file path is required.", nameof(path));
        }

        using var stream = new FileStream(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using SHA256 algorithm = SHA256.Create();
        return ToHex(algorithm.ComputeHash(stream));
    }

    public static string CanonicalizeOptions(IReadOnlyDictionary<string, string>? options)
    {
        var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> item in options ?? new ReadOnlyDictionary<string, string>(
                     new Dictionary<string, string>()))
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                throw new ArgumentException("Option keys cannot be empty.", nameof(options));
            }

            if (item.Value is null)
            {
                throw new ArgumentException("Option values cannot be null.", nameof(options));
            }

            copy.Add(item.Key.Trim(), item.Value);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string> item in copy)
            {
                writer.WriteString(item.Key, item.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string CreateCaseId(int zeroBasedIndex, string deterministicSeed)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(zeroBasedIndex);
#else
        if (zeroBasedIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
        }
#endif

        string digest = Sha256Text(deterministicSeed ?? throw new ArgumentNullException(nameof(deterministicSeed)));
        return "case-" + zeroBasedIndex.ToString("D4", CultureInfo.InvariantCulture)
            + "-" + digest.Remove(12);
    }

    private static string Sha256Bytes(byte[] bytes)
    {
#if NET6_0_OR_GREATER
        return ToHex(SHA256.HashData(bytes));
#else
        using SHA256 algorithm = SHA256.Create();
        return ToHex(algorithm.ComputeHash(bytes));
#endif
    }

    private static string ToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
