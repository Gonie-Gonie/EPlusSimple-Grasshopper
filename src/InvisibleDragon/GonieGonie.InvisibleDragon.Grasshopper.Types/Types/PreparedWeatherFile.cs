using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace GonieGonie.InvisibleDragon.Grasshopper.Types;

/// <summary>
/// Identifies a content-addressed local EPW artifact prepared for InvisibleDragon execution.
/// </summary>
public sealed class PreparedWeatherFile
{
    private readonly string? _artifactPath;

    /// <summary>
    /// Creates a prepared-weather handle without reading the artifact.
    /// </summary>
    /// <param name="artifactPath">Absolute path to the local EPW artifact.</param>
    /// <param name="provider">Logical provider of the weather data.</param>
    /// <param name="weatherIdentity">Logical weather identity, normally the source EPW file name.</param>
    /// <param name="sha256">Expected SHA-256 digest as exactly 64 hexadecimal characters.</param>
    public PreparedWeatherFile(
        string artifactPath,
        string provider,
        string weatherIdentity,
        string sha256)
        : this(
            NormalizeArtifactPath(artifactPath),
            provider,
            weatherIdentity,
            sha256,
            artifactPathIsNormalized: true)
    {
    }

    private PreparedWeatherFile(
        string? artifactPath,
        string provider,
        string weatherIdentity,
        string sha256,
        bool artifactPathIsNormalized)
    {
        _artifactPath = artifactPathIsNormalized ? artifactPath : null;
        Provider = RequireText(provider, nameof(provider));
        WeatherIdentity = RequireText(weatherIdentity, nameof(weatherIdentity));
        Sha256 = NormalizeSha256(sha256);
    }

    /// <summary>
    /// Gets the absolute local EPW artifact path for internal execution consumers.
    /// </summary>
    /// <remarks>This property must not be used as Grasshopper display text.</remarks>
    public string ArtifactPath => _artifactPath
        ?? throw new InvalidOperationException(
            "This restored Weather handle is not bound to a local artifact. Recreate it from a verified EPW in the workflow that owns the Weather input.");

    /// <summary>Gets whether this handle is currently bound to a verified local-artifact location.</summary>
    public bool IsBound => _artifactPath is not null;

    /// <summary>Gets the logical weather-data provider.</summary>
    public string Provider { get; }

    /// <summary>Gets the logical weather identity, normally the source EPW file name.</summary>
    public string WeatherIdentity { get; }

    /// <summary>Gets the expected SHA-256 digest as uppercase hexadecimal text.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Creates a handle for an existing EPW artifact after verifying its LOCATION header,
    /// then records its current SHA-256 digest.
    /// </summary>
    public static PreparedWeatherFile FromVerifiedArtifact(
        string artifactPath,
        string provider,
        string weatherIdentity)
    {
        string normalizedPath = NormalizeArtifactPath(artifactPath);
        string normalizedProvider = RequireText(provider, nameof(provider));
        string normalizedIdentity = RequireText(weatherIdentity, nameof(weatherIdentity));
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("The prepared weather artifact does not exist.");
        }

        return new PreparedWeatherFile(
            normalizedPath,
            normalizedProvider,
            normalizedIdentity,
            ComputeVerifiedSha256(normalizedPath));
    }

    /// <summary>
    /// Verifies that the local artifact exists and still matches the recorded SHA-256 digest.
    /// </summary>
    public bool VerifyArtifact()
    {
        if (!TryGetArtifactPath(out string? artifactPath))
        {
            return false;
        }

        try
        {
            return File.Exists(artifactPath)
                && string.Equals(ComputeSha256(artifactPath!), Sha256, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to obtain the local artifact path for an immediate execution operation.
    /// Restored persisted handles intentionally remain unbound until the owning workflow recreates
    /// them from a verified EPW artifact.
    /// </summary>
    public bool TryGetArtifactPath(out string? artifactPath)
    {
        artifactPath = _artifactPath;
        return artifactPath is not null;
    }

    internal static PreparedWeatherFile FromPersistedMetadata(
        string provider,
        string weatherIdentity,
        string sha256) =>
        new(
            artifactPath: null,
            provider,
            weatherIdentity,
            sha256,
            artifactPathIsNormalized: false);

    /// <summary>Returns path-free Grasshopper display text.</summary>
    public override string ToString()
    {
        return $"Prepared weather {WeatherIdentity} from {Provider} (SHA-256 {Sha256})";
    }

    private static string NormalizeArtifactPath(string artifactPath)
    {
        string value = RequireText(artifactPath, nameof(artifactPath));
        if (!Path.IsPathRooted(value))
        {
            throw new ArgumentException("The EPW artifact path must be absolute.", nameof(artifactPath));
        }

        string fullPath = Path.GetFullPath(value);
        if (!string.Equals(
                Path.GetPathRoot(value),
                Path.GetPathRoot(fullPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The EPW artifact path must be fully qualified.", nameof(artifactPath));
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".epw", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The weather artifact must have an .epw extension.", nameof(artifactPath));
        }

        return fullPath;
    }

    private static string NormalizeSha256(string sha256)
    {
        string value = RequireText(sha256, nameof(sha256));
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "The weather artifact SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(sha256));
        }

        return value.ToUpperInvariant();
    }

    private static string RequireText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value!.Trim();
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ComputeSha256(stream);
    }

    private static string ComputeVerifiedSha256(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using (var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true))
        {
            string? header = reader.ReadLine();
            if (header is null
                || !header.StartsWith("LOCATION,", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The weather artifact must begin with an EnergyPlus LOCATION header.");
            }
        }

        stream.Position = 0;
        return ComputeSha256(stream);
    }

    private static string ComputeSha256(Stream stream)
    {
        using SHA256 algorithm = SHA256.Create();
        byte[] hash = algorithm.ComputeHash(stream);
        var text = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash)
        {
            text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }
}

/// <summary>Grasshopper wrapper for a prepared EPW artifact handle.</summary>
public sealed class PreparedWeatherFileGoo : DragonGoo<PreparedWeatherFile>
{
    public PreparedWeatherFileGoo()
    {
    }

    public PreparedWeatherFileGoo(PreparedWeatherFile value)
        : base(value)
    {
    }

    public override string TypeName => "InvisibleDragon Prepared Weather";

    public override string TypeDescription =>
        "A content-addressed EPW artifact prepared for InvisibleDragon execution.";

    protected override DragonGoo<PreparedWeatherFile> Create(PreparedWeatherFile value) =>
        new PreparedWeatherFileGoo(value);

    protected override DragonGoo<PreparedWeatherFile> CreateEmpty() => new PreparedWeatherFileGoo();

    public override global::Grasshopper.Kernel.Types.IGH_Goo Duplicate()
    {
        if (Value is null)
        {
            return new PreparedWeatherFileGoo();
        }

        PreparedWeatherFile duplicate = Value.TryGetArtifactPath(out string? artifactPath)
            ? new PreparedWeatherFile(artifactPath!, Value.Provider, Value.WeatherIdentity, Value.Sha256)
            : PreparedWeatherFile.FromPersistedMetadata(Value.Provider, Value.WeatherIdentity, Value.Sha256);
        return new PreparedWeatherFileGoo(duplicate);
    }

    protected override string DisplayText(PreparedWeatherFile value) => value.ToString();
}
