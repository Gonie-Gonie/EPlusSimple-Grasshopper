namespace GonieGonie.EnergyPlus.Runtime;

/// <summary>
/// Downloads, verifies, and transactionally prepares a pinned EnergyPlus runtime in a per-user cache.
/// </summary>
public sealed class EnergyPlusRuntimeBootstrapper
{
    private readonly EnergyPlusRuntimeDistribution distribution;
    private readonly IEnergyPlusRuntimeArchiveDownloader downloader;
    private readonly RuntimeResolver resolver;

    public EnergyPlusRuntimeBootstrapper()
        : this(
            EnergyPlusRuntimeDistribution.Supported,
            new HttpEnergyPlusRuntimeArchiveDownloader())
    {
    }

    public EnergyPlusRuntimeBootstrapper(
        EnergyPlusRuntimeDistribution distribution,
        IEnergyPlusRuntimeArchiveDownloader downloader)
    {
        this.distribution = distribution ?? throw new ArgumentNullException(nameof(distribution));
        this.downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));

        var archiveUri = distribution.ArchiveUri
            ?? throw new ArgumentException("The runtime distribution requires an archive URI.", nameof(distribution));
        if (!archiveUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The runtime distribution archive URI must be absolute.", nameof(distribution));
        }

        var manifest = distribution.Manifest
            ?? throw new ArgumentException("The runtime distribution requires a manifest.", nameof(distribution));
        var errors = manifest.Validate();
        if (errors.Count != 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(distribution));
        }

        resolver = new RuntimeResolver(manifest);
    }

    /// <summary>
    /// Reuses a valid cached runtime or prepares it from the immutable distribution archive.
    /// Expected acquisition, integrity, environment, timeout, and cancellation failures are returned
    /// as structured data instead of being thrown.
    /// </summary>
    public async Task<EnergyPlusRuntimeBootstrapResult> EnsureInstalledAsync(
        EnergyPlusRuntimeBootstrapOptions? options = null,
        IProgress<EnergyPlusRuntimeBootstrapProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new EnergyPlusRuntimeBootstrapOptions();
        var targetResolution = ResolveTargetRoot(options);
        if (targetResolution.Failure is not null)
        {
            return Failed(null, targetResolution.Failure);
        }

        var targetRoot = targetResolution.TargetRoot!;
        var allowInvalidTargetReplacement = options.TargetRoot is null
            || options.ReplaceInvalidExistingTarget;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parentRoot = Path.GetDirectoryName(targetRoot)!;
            EnsurePathIsNotReparsePoint(parentRoot, "runtime cache parent");
            EnsurePathIsNotReparsePoint(targetRoot, "runtime target");
            Report(
                progress,
                EnergyPlusRuntimeBootstrapStage.CheckingExistingRuntime,
                "Checking the cached EnergyPlus runtime.");
            var existing = await ResolveExactAsync(targetRoot, cancellationToken).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                Report(
                    progress,
                    EnergyPlusRuntimeBootstrapStage.Completed,
                    "The verified EnergyPlus runtime is already available.");
                return Succeeded(
                    existing.Runtime!,
                    EnergyPlusRuntimeBootstrapDisposition.Reused,
                    targetRoot);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(targetRoot))
            {
                return Failed(targetRoot, new EnergyPlusFailure(
                    EnergyPlusFailureCategory.RuntimeEnvironment,
                    "RUNTIME_TARGET_IS_FILE",
                    "The EnergyPlus runtime target is occupied by a file.",
                    targetRoot));
            }

            if (Directory.Exists(targetRoot) && !allowInvalidTargetReplacement)
            {
                return Failed(targetRoot, InvalidCustomTargetFailure(targetRoot));
            }

            Directory.CreateDirectory(parentRoot);
            EnsurePathIsNotReparsePoint(parentRoot, "runtime cache parent");
            using var installLock = await AcquireInstallLockAsync(
                targetRoot,
                parentRoot,
                options,
                progress,
                cancellationToken).ConfigureAwait(false);

            Report(
                progress,
                EnergyPlusRuntimeBootstrapStage.CheckingExistingRuntime,
                "Rechecking the runtime after acquiring the installation lock.");
            existing = await ResolveExactAsync(targetRoot, cancellationToken).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                Report(
                    progress,
                    EnergyPlusRuntimeBootstrapStage.Completed,
                    "Another process prepared the verified EnergyPlus runtime.");
                return Succeeded(
                    existing.Runtime!,
                    EnergyPlusRuntimeBootstrapDisposition.Reused,
                    targetRoot);
            }

            EnsurePathIsNotReparsePoint(targetRoot, "runtime target");
            if (Directory.Exists(targetRoot) && !allowInvalidTargetReplacement)
            {
                return Failed(targetRoot, InvalidCustomTargetFailure(targetRoot));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var installed = await InstallUnderLockAsync(
                targetRoot,
                parentRoot,
                allowInvalidTargetReplacement,
                progress,
                cancellationToken).ConfigureAwait(false);
            Report(
                progress,
                EnergyPlusRuntimeBootstrapStage.Completed,
                "The verified EnergyPlus runtime is ready.");
            return Succeeded(
                installed,
                EnergyPlusRuntimeBootstrapDisposition.Installed,
                targetRoot);
        }
        catch (RuntimeBootstrapException exception)
        {
            return Failed(targetRoot, exception.Failure);
        }
        catch (OperationCanceledException)
        {
            return Failed(targetRoot, new EnergyPlusFailure(
                EnergyPlusFailureCategory.Cancelled,
                "RUNTIME_BOOTSTRAP_CANCELLED",
                "EnergyPlus runtime preparation was cancelled."));
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            return Failed(targetRoot, new EnergyPlusFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_BOOTSTRAP_IO_FAILED",
                "The EnergyPlus runtime cache could not be prepared.",
                exception.Message));
        }
        catch (Exception exception)
        {
            return Failed(targetRoot, EnergyPlusFailure.Internal(
                "RUNTIME_BOOTSTRAP_INTERNAL",
                "An unexpected error occurred while preparing EnergyPlus.",
                exception));
        }
    }

    private async Task<EnergyPlusRuntimeLayout> InstallUnderLockAsync(
        string targetRoot,
        string parentRoot,
        bool allowInvalidTargetReplacement,
        IProgress<EnergyPlusRuntimeBootstrapProgress>? progress,
        CancellationToken cancellationToken)
    {
        var operationPrefix = "." + Path.GetFileName(targetRoot) + ".bootstrap-" + Guid.NewGuid().ToString("N");
        var partialArchivePath = RuntimeFileSystem.CombineUnder(
            parentRoot,
            operationPrefix + ".download.partial");
        var stagingRoot = RuntimeFileSystem.CombineUnder(
            parentRoot,
            operationPrefix + ".staging");
        var extractionRoot = RuntimeFileSystem.CombineUnder(stagingRoot, "extracted");

        try
        {
            EnsureOperationPathIsUnused(partialArchivePath);
            EnsureOperationPathIsUnused(stagingRoot);
            Report(
                progress,
                EnergyPlusRuntimeBootstrapStage.DownloadingArchive,
                "Downloading the pinned EnergyPlus archive.",
                0,
                distribution.Manifest.EnergyPlusArchiveSize);
            var downloadProgress = new CallbackProgress<EnergyPlusRuntimeDownloadProgress>(update =>
            {
                var expectedSize = distribution.Manifest.EnergyPlusArchiveSize;
                if (update.BytesReceived > expectedSize
                    || (update.TotalBytes is not null && update.TotalBytes.Value > expectedSize))
                {
                    var reportedTotal = update.TotalBytes?.ToString(
                        System.Globalization.CultureInfo.InvariantCulture) ?? "unknown";
                    throw BootstrapFailure(
                        EnergyPlusFailureCategory.RuntimeIntegrity,
                        "RUNTIME_ARCHIVE_SIZE_EXCEEDED",
                        "The EnergyPlus archive download exceeded the pinned size.",
                        $"Expected at most {expectedSize}; received {update.BytesReceived}; "
                            + $"reported total {reportedTotal}.");
                }

                Report(
                    progress,
                    EnergyPlusRuntimeBootstrapStage.DownloadingArchive,
                    "Downloading the pinned EnergyPlus archive.",
                    update.BytesReceived,
                    update.TotalBytes ?? expectedSize);
            });
            try
            {
                await downloader.DownloadAsync(
                    distribution.ArchiveUri,
                    partialArchivePath,
                    downloadProgress,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (RuntimeBootstrapException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw BootstrapFailure(
                    EnergyPlusFailureCategory.RuntimeEnvironment,
                    "RUNTIME_ARCHIVE_DOWNLOAD_FAILED",
                    "The pinned EnergyPlus archive could not be downloaded.",
                    distribution.ArchiveUri + " " + exception.Message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsurePathIsNotReparsePoint(partialArchivePath, "downloaded partial archive");
            await VerifyArchiveAsync(partialArchivePath, progress, cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(stagingRoot);
            EnsurePathIsNotReparsePoint(stagingRoot, "runtime staging directory");
            Report(
                progress,
                EnergyPlusRuntimeBootstrapStage.ExtractingArchive,
                "Extracting the verified EnergyPlus archive.");
            try
            {
                await RuntimeArchiveExtractor.ExtractAsync(
                    partialArchivePath,
                    extractionRoot,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (RuntimeArchiveExtractionException exception)
            {
                throw new RuntimeBootstrapException(exception.Failure);
            }
            catch (InvalidDataException exception)
            {
                throw BootstrapFailure(
                    EnergyPlusFailureCategory.RuntimeIntegrity,
                    "RUNTIME_ARCHIVE_INVALID",
                    "The pinned EnergyPlus archive is not a valid ZIP payload.",
                    exception.Message);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                throw BootstrapFailure(
                    EnergyPlusFailureCategory.RuntimeEnvironment,
                    "RUNTIME_ARCHIVE_EXTRACTION_FAILED",
                    "The verified EnergyPlus archive could not be extracted.",
                    exception.Message);
            }

            Report(
                progress,
                EnergyPlusRuntimeBootstrapStage.VerifyingExtractedRuntime,
                "Verifying the extracted EnergyPlus executables, IDD, and epJSON schema.");
            var candidate = await FindVerifiedRuntimeAsync(
                extractionRoot,
                cancellationToken).ConfigureAwait(false);
            EnsurePathIsNotReparsePoint(candidate.RootPath, "extracted runtime root");
            cancellationToken.ThrowIfCancellationRequested();

            return await PromoteAsync(
                candidate.RootPath,
                targetRoot,
                parentRoot,
                operationPrefix,
                allowInvalidTargetReplacement,
                progress).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteOwnedFile(parentRoot, partialArchivePath, operationPrefix);
            TryDeleteOwnedDirectory(parentRoot, stagingRoot, operationPrefix);
        }
    }

    private async Task VerifyArchiveAsync(
        string archivePath,
        IProgress<EnergyPlusRuntimeBootstrapProgress>? progress,
        CancellationToken cancellationToken)
    {
        Report(
            progress,
            EnergyPlusRuntimeBootstrapStage.VerifyingArchive,
            "Verifying the EnergyPlus archive size and SHA-256.");
        if (!File.Exists(archivePath))
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_ARCHIVE_NOT_CREATED",
                "The archive downloader did not create its partial destination.",
                archivePath);
        }

        long actualSize;
        try
        {
            actualSize = new FileInfo(archivePath).Length;
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_ARCHIVE_UNREADABLE",
                "The downloaded EnergyPlus archive cannot be read.",
                exception.Message);
        }

        if (actualSize != distribution.Manifest.EnergyPlusArchiveSize)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "RUNTIME_ARCHIVE_SIZE_MISMATCH",
                "The downloaded EnergyPlus archive size does not match the pinned manifest.",
                $"Expected {distribution.Manifest.EnergyPlusArchiveSize}; actual {actualSize}.");
        }

        string actualHash;
        try
        {
            actualHash = await RuntimeFileSystem.ComputeSha256Async(
                archivePath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_ARCHIVE_UNREADABLE",
                "The downloaded EnergyPlus archive cannot be hashed.",
                exception.Message);
        }

        if (!string.Equals(
                actualHash,
                distribution.Manifest.EnergyPlusArchiveSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "RUNTIME_ARCHIVE_HASH_MISMATCH",
                "The downloaded EnergyPlus archive SHA-256 does not match the pinned manifest.",
                $"Expected {distribution.Manifest.EnergyPlusArchiveSha256}; actual {actualHash}.");
        }
    }

    private async Task<EnergyPlusRuntimeLayout> FindVerifiedRuntimeAsync(
        string extractionRoot,
        CancellationToken cancellationToken)
    {
        string[] executables;
        try
        {
            executables = Directory.GetFiles(
                extractionRoot,
                "energyplus.exe",
                SearchOption.AllDirectories);
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_ARCHIVE_SCAN_FAILED",
                "The extracted EnergyPlus archive could not be inspected.",
                exception.Message);
        }

        if (executables.Length == 0)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "RUNTIME_ARCHIVE_PAYLOAD_MISSING",
                "The verified archive does not contain energyplus.exe.");
        }

        var matchingRuntimes = new List<EnergyPlusRuntimeLayout>();
        EnergyPlusFailure? lastFailure = null;
        foreach (var executable in executables.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidateRoot = Path.GetDirectoryName(executable)!;
            var resolution = await ResolveExactAsync(candidateRoot, cancellationToken).ConfigureAwait(false);
            if (resolution.IsSuccess)
            {
                matchingRuntimes.Add(resolution.Runtime!);
            }
            else
            {
                lastFailure = resolution.Failure;
            }
        }

        if (matchingRuntimes.Count == 0)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "RUNTIME_ARCHIVE_PAYLOAD_MISMATCH",
                "The verified archive does not contain the pinned EnergyPlus executable set.",
                lastFailure is null
                    ? null
                    : lastFailure.Code + ": " + lastFailure.Message + " " + lastFailure.Detail);
        }

        if (matchingRuntimes.Count != 1)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "RUNTIME_ARCHIVE_PAYLOAD_AMBIGUOUS",
                "The verified archive contains more than one matching EnergyPlus runtime root.");
        }

        return matchingRuntimes[0];
    }

    private async Task<EnergyPlusRuntimeLayout> PromoteAsync(
        string candidateRoot,
        string targetRoot,
        string parentRoot,
        string operationPrefix,
        bool allowInvalidTargetReplacement,
        IProgress<EnergyPlusRuntimeBootstrapProgress>? progress)
    {
        Report(
            progress,
            EnergyPlusRuntimeBootstrapStage.PromotingRuntime,
            "Promoting the verified EnergyPlus runtime into the stable cache.");
        var displacedRoot = RuntimeFileSystem.CombineUnder(
            parentRoot,
            operationPrefix + ".displaced");
        var displaced = false;
        try
        {
            EnsurePathIsNotReparsePoint(targetRoot, "runtime target");
            EnsureOperationPathIsUnused(displacedRoot);
            if (File.Exists(targetRoot))
            {
                throw BootstrapFailure(
                    EnergyPlusFailureCategory.RuntimeEnvironment,
                    "RUNTIME_TARGET_IS_FILE",
                    "The EnergyPlus runtime target is occupied by a file.",
                    targetRoot);
            }

            if (Directory.Exists(targetRoot))
            {
                if (!allowInvalidTargetReplacement)
                {
                    throw new RuntimeBootstrapException(InvalidCustomTargetFailure(targetRoot));
                }

                Directory.Move(targetRoot, displacedRoot);
                displaced = true;
            }

            try
            {
                Directory.Move(candidateRoot, targetRoot);
            }
            catch
            {
                RestoreDisplacedRuntime(targetRoot, displacedRoot, displaced);
                throw;
            }

            var promoted = await ResolveExactAsync(targetRoot, CancellationToken.None).ConfigureAwait(false);
            if (!promoted.IsSuccess)
            {
                TryDeletePromotedRuntime(targetRoot);
                RestoreDisplacedRuntime(targetRoot, displacedRoot, displaced);
                throw BootstrapFailure(
                    EnergyPlusFailureCategory.RuntimeIntegrity,
                    "RUNTIME_PROMOTION_VERIFICATION_FAILED",
                    "The promoted EnergyPlus runtime failed final integrity verification.",
                    promoted.Failure?.Code + ": " + promoted.Failure?.Message);
            }

            if (displaced)
            {
                TryDeleteOwnedDirectory(parentRoot, displacedRoot, operationPrefix);
            }

            return promoted.Runtime!;
        }
        catch (RuntimeBootstrapException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            RestoreDisplacedRuntime(targetRoot, displacedRoot, displaced);
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_PROMOTION_FAILED",
                "The verified EnergyPlus runtime could not be promoted into the stable cache.",
                exception.Message);
        }
    }

    private static async Task<FileStream> AcquireInstallLockAsync(
        string targetRoot,
        string parentRoot,
        EnergyPlusRuntimeBootstrapOptions options,
        IProgress<EnergyPlusRuntimeBootstrapProgress>? progress,
        CancellationToken cancellationToken)
    {
        var lockPath = GetInstallLockPath(targetRoot, parentRoot);
        EnsurePathIsNotReparsePoint(lockPath, "runtime installation lock");
        if (Directory.Exists(lockPath))
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_INSTALL_LOCK_INVALID",
                "The EnergyPlus runtime installation lock path is occupied by a directory.",
                lockPath);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.None);
            }
            catch (IOException exception)
            {
                if (stopwatch.Elapsed >= options.LockWaitTimeout)
                {
                    throw BootstrapFailure(
                        EnergyPlusFailureCategory.Timeout,
                        "RUNTIME_INSTALL_LOCK_TIMEOUT",
                        "Timed out waiting for another EnergyPlus runtime preparation process.",
                        lockPath + " " + exception.Message);
                }
            }

            Report(
                progress,
                EnergyPlusRuntimeBootstrapStage.WaitingForInstallLock,
                "Waiting for another process to finish preparing EnergyPlus.");
            var remaining = options.LockWaitTimeout - stopwatch.Elapsed;
            var delay = remaining < options.LockRetryDelay ? remaining : options.LockRetryDelay;
            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<EnergyPlusRuntimeResolution> ResolveExactAsync(
        string targetRoot,
        CancellationToken cancellationToken)
    {
        return resolver.ResolveAsync(new EnergyPlusRuntimeResolveOptions
        {
            RuntimeRoot = targetRoot,
            SearchDefaultCacheLocation = false,
            SearchDefaultInstallLocation = false,
            SearchEnvironmentVariables = false
        }, cancellationToken);
    }

    private (string? TargetRoot, EnergyPlusFailure? Failure) ResolveTargetRoot(
        EnergyPlusRuntimeBootstrapOptions options)
    {
        if (options.LockWaitTimeout <= TimeSpan.Zero
            || options.LockWaitTimeout.TotalMilliseconds > int.MaxValue)
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "RUNTIME_LOCK_TIMEOUT_INVALID",
                "The runtime lock wait timeout must be greater than zero and no more than 24.8 days."));
        }

        if (options.LockRetryDelay <= TimeSpan.Zero
            || options.LockRetryDelay.TotalMilliseconds > int.MaxValue)
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "RUNTIME_LOCK_RETRY_INVALID",
                "The runtime lock retry delay must be greater than zero and no more than 24.8 days."));
        }

        if (options.TargetRoot is not null && string.IsNullOrWhiteSpace(options.TargetRoot))
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "RUNTIME_TARGET_INVALID",
                "The EnergyPlus runtime target cannot be empty."));
        }

        try
        {
            var targetRoot = options.TargetRoot is null
                ? EnergyPlusRuntimePaths.GetDefaultRuntimeRoot(distribution.Manifest)
                : RuntimeFileSystem.NormalizeDirectory(options.TargetRoot);
            var volumeRoot = Path.GetPathRoot(targetRoot);
            var parentRoot = Path.GetDirectoryName(targetRoot);
            if (string.Equals(targetRoot, volumeRoot, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(parentRoot))
            {
                return (null, new EnergyPlusFailure(
                    EnergyPlusFailureCategory.UserInput,
                    "RUNTIME_TARGET_UNSAFE",
                    "A filesystem root cannot be used as the EnergyPlus runtime target.",
                    targetRoot));
            }

            return (targetRoot, null);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException
            || exception is InvalidOperationException)
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "RUNTIME_TARGET_INVALID",
                "The EnergyPlus runtime target path is invalid.",
                exception.Message));
        }
    }

    internal static string GetInstallLockPath(string targetRoot, string parentRoot)
    {
        return RuntimeFileSystem.CombineUnder(
            parentRoot,
            "." + Path.GetFileName(targetRoot) + ".bootstrap.lock");
    }

    private static EnergyPlusFailure InvalidCustomTargetFailure(string targetRoot)
    {
        return new EnergyPlusFailure(
            EnergyPlusFailureCategory.UserInput,
            "RUNTIME_TARGET_REPLACEMENT_REQUIRED",
            "The explicit EnergyPlus runtime target already contains an invalid runtime.",
            "No files were changed. Set ReplaceInvalidExistingTarget to true only if this exact "
                + "directory may be transactionally replaced: " + targetRoot);
    }

    private static void EnsureOperationPathIsUnused(string path)
    {
        EnsurePathIsNotReparsePoint(path, "runtime operation path");
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_OPERATION_PATH_COLLISION",
                "A unique EnergyPlus runtime operation path is already occupied.",
                path);
        }
    }

    private static void EnsurePathIsNotReparsePoint(string path, string description)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_PATH_INSPECTION_FAILED",
                "A critical EnergyPlus runtime path could not be inspected.",
                description + ": " + path + " " + exception.Message);
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw BootstrapFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUNTIME_REPARSE_POINT_REJECTED",
                "A critical EnergyPlus runtime path is a reparse point and was rejected.",
                description + ": " + path);
        }
    }

    private static void RestoreDisplacedRuntime(
        string targetRoot,
        string displacedRoot,
        bool displaced)
    {
        if (!displaced || !Directory.Exists(displacedRoot) || Directory.Exists(targetRoot))
        {
            return;
        }

        try
        {
            Directory.Move(displacedRoot, targetRoot);
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            // The original runtime remains in the operation-owned displaced directory.
            // The primary failure reports that promotion did not complete.
        }
    }

    private static void TryDeletePromotedRuntime(string targetRoot)
    {
        try
        {
            if (Directory.Exists(targetRoot))
            {
                Directory.Delete(targetRoot, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            // Best-effort rollback; final verification has already failed.
        }
    }

    private static void TryDeleteOwnedFile(
        string parentRoot,
        string path,
        string operationPrefix)
    {
        try
        {
            if (RuntimeFileSystem.IsDescendantOf(parentRoot, path)
                && Path.GetFileName(path).StartsWith(operationPrefix, StringComparison.Ordinal)
                && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            // Cleanup cannot obscure the structured operation failure or a verified result.
        }
    }

    private static void TryDeleteOwnedDirectory(
        string parentRoot,
        string path,
        string operationPrefix)
    {
        try
        {
            if (RuntimeFileSystem.IsDescendantOf(parentRoot, path)
                && Path.GetFileName(path).StartsWith(operationPrefix, StringComparison.Ordinal)
                && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            // Cleanup cannot obscure the structured operation failure or a verified result.
        }
    }

    private static void Report(
        IProgress<EnergyPlusRuntimeBootstrapProgress>? progress,
        EnergyPlusRuntimeBootstrapStage stage,
        string message,
        long? completedBytes = null,
        long? totalBytes = null)
    {
        progress?.Report(new EnergyPlusRuntimeBootstrapProgress(
            stage,
            message,
            completedBytes,
            totalBytes));
    }

    private static EnergyPlusRuntimeBootstrapResult Succeeded(
        EnergyPlusRuntimeLayout runtime,
        EnergyPlusRuntimeBootstrapDisposition disposition,
        string targetRoot)
    {
        return new EnergyPlusRuntimeBootstrapResult(runtime, null, disposition, targetRoot);
    }

    private static EnergyPlusRuntimeBootstrapResult Failed(
        string? targetRoot,
        EnergyPlusFailure failure)
    {
        return new EnergyPlusRuntimeBootstrapResult(
            null,
            failure,
            EnergyPlusRuntimeBootstrapDisposition.None,
            targetRoot);
    }

    private static RuntimeBootstrapException BootstrapFailure(
        EnergyPlusFailureCategory category,
        string code,
        string message,
        string? detail = null)
    {
        return new RuntimeBootstrapException(new EnergyPlusFailure(category, code, message, detail));
    }

    private sealed class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> callback;

        internal CallbackProgress(Action<T> callback)
        {
            this.callback = callback;
        }

        public void Report(T value)
        {
            callback(value);
        }
    }

    private sealed class RuntimeBootstrapException : Exception
    {
        internal RuntimeBootstrapException(EnergyPlusFailure failure)
            : base(failure.Message)
        {
            Failure = failure;
        }

        internal EnergyPlusFailure Failure { get; }
    }
}
