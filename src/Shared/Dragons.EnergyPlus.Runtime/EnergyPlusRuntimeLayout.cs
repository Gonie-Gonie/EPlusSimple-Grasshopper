namespace Dragons.EnergyPlus.Runtime;

/// <summary>
/// A fully hash-verified EnergyPlus installation. Instances are produced by <see cref="RuntimeResolver"/>.
/// </summary>
public sealed class EnergyPlusRuntimeLayout
{
    internal EnergyPlusRuntimeLayout(
        string rootPath,
        string energyPlusExecutablePath,
        string expandObjectsExecutablePath,
        string iddPath,
        string schemaPath,
        EnergyPlusRuntimeManifest manifest,
        DateTimeOffset verifiedAtUtc)
    {
        RootPath = rootPath;
        EnergyPlusExecutablePath = energyPlusExecutablePath;
        ExpandObjectsExecutablePath = expandObjectsExecutablePath;
        IddPath = iddPath;
        SchemaPath = schemaPath;
        Manifest = manifest;
        VerifiedAtUtc = verifiedAtUtc;
    }

    public string RootPath { get; }

    public string EnergyPlusExecutablePath { get; }

    public string ExpandObjectsExecutablePath { get; }

    public string IddPath { get; }

    public string SchemaPath { get; }

    public EnergyPlusRuntimeManifest Manifest { get; }

    public DateTimeOffset VerifiedAtUtc { get; }
}

/// <summary>
/// Controls deterministic runtime discovery. An explicit root is never silently replaced by a fallback.
/// </summary>
public sealed record EnergyPlusRuntimeResolveOptions
{
    public string? RuntimeRoot { get; init; }

    public string? ManifestPath { get; init; }

    public IReadOnlyList<string> AdditionalSearchRoots { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Overrides the stable Dragons per-user cache location used by discovery.
    /// This is primarily useful for portable hosts and isolated tests.
    /// </summary>
    public string? CachedRuntimeRoot { get; init; }

    public bool SearchEnvironmentVariables { get; init; } = true;

    public bool SearchDefaultCacheLocation { get; init; } = true;

    public bool SearchDefaultInstallLocation { get; init; } = true;
}

/// <summary>
/// The structured result of runtime discovery and integrity verification.
/// </summary>
public sealed record EnergyPlusRuntimeResolution(
    EnergyPlusRuntimeLayout? Runtime,
    EnergyPlusFailure? Failure,
    IReadOnlyList<string> AttemptedRoots)
{
    public bool IsSuccess => Runtime is not null && Failure is null;
}
