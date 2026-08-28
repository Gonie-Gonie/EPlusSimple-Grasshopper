namespace GonieGonie.EnergyPlus.Runtime;

/// <summary>
/// Identifies an immutable EnergyPlus archive and the runtime payload it must contain.
/// </summary>
public sealed record EnergyPlusRuntimeDistribution
{
    public const string SupportedArchiveFileName =
        "EnergyPlus-24.2.0-94a887817b-Windows-x86_64.zip";

    public EnergyPlusRuntimeDistribution(
        Uri archiveUri,
        EnergyPlusRuntimeManifest manifest)
    {
        archiveUri = archiveUri ?? throw new ArgumentNullException(nameof(archiveUri));
        manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        if (!archiveUri.IsAbsoluteUri || archiveUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "The EnergyPlus archive URI must be absolute and use HTTPS.",
                nameof(archiveUri));
        }

        var errors = manifest.Validate();
        if (errors.Count != 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(manifest));
        }

        ArchiveUri = archiveUri;
        Manifest = manifest;
    }

    public static EnergyPlusRuntimeDistribution Supported { get; } = new(
        new Uri(
            "https://github.com/NREL/EnergyPlus/releases/download/v24.2.0a/"
            + SupportedArchiveFileName,
            UriKind.Absolute),
        EnergyPlusRuntimeManifest.Supported);

    public Uri ArchiveUri { get; }

    public EnergyPlusRuntimeManifest Manifest { get; }
}

/// <summary>
/// Receives archive bytes from a source without deciding where the runtime is installed.
/// Implementations must create the requested partial destination file exclusively.
/// </summary>
public interface IEnergyPlusRuntimeArchiveDownloader
{
    /// <summary>
    /// Downloads an archive and reports monotonically increasing byte counts after each write.
    /// Throwing from the progress receiver must stop the download immediately.
    /// </summary>
    Task DownloadAsync(
        Uri sourceUri,
        string destinationPartialPath,
        IProgress<EnergyPlusRuntimeDownloadProgress>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// Reports byte-level archive acquisition progress for a bundled copy or HTTPS download.
/// </summary>
public sealed record EnergyPlusRuntimeDownloadProgress(
    long BytesReceived,
    long? TotalBytes);

/// <summary>
/// Identifies the current phase of an EnergyPlus runtime bootstrap operation.
/// </summary>
public enum EnergyPlusRuntimeBootstrapStage
{
    CheckingExistingRuntime,
    WaitingForInstallLock,
    DownloadingArchive,
    VerifyingArchive,
    ExtractingArchive,
    VerifyingExtractedRuntime,
    PromotingRuntime,
    Completed
}

/// <summary>
/// A stable progress update suitable for presentation by any host UI.
/// </summary>
public sealed record EnergyPlusRuntimeBootstrapProgress(
    EnergyPlusRuntimeBootstrapStage Stage,
    string Message,
    long? CompletedBytes = null,
    long? TotalBytes = null);

/// <summary>
/// Controls per-user runtime acquisition without performing a machine-global install.
/// </summary>
public sealed record EnergyPlusRuntimeBootstrapOptions
{
    /// <summary>
    /// Overrides the exact runtime target directory. When omitted, the stable GonieGonie
    /// LocalApplicationData cache is used.
    /// </summary>
    public string? TargetRoot { get; init; }

    /// <summary>
    /// Maximum time to wait for another process that is preparing the same runtime.
    /// </summary>
    public TimeSpan LockWaitTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Delay between attempts to acquire the cross-process installation lock.
    /// </summary>
    public TimeSpan LockRetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Allows an invalid directory already occupying an explicitly supplied <see cref="TargetRoot"/>
    /// to be transactionally replaced. This is unnecessary for the managed default cache and is
    /// disabled for custom roots unless the caller deliberately opts in.
    /// </summary>
    public bool ReplaceInvalidExistingTarget { get; init; }
}

/// <summary>
/// Describes whether the requested runtime was reused or installed by this operation.
/// </summary>
public enum EnergyPlusRuntimeBootstrapDisposition
{
    None,
    Reused,
    Installed
}

/// <summary>
/// The non-throwing result of preparing a pinned EnergyPlus runtime.
/// </summary>
public sealed record EnergyPlusRuntimeBootstrapResult(
    EnergyPlusRuntimeLayout? Runtime,
    EnergyPlusFailure? Failure,
    EnergyPlusRuntimeBootstrapDisposition Disposition,
    string? TargetRoot)
{
    public bool IsSuccess => Runtime is not null && Failure is null;
}
