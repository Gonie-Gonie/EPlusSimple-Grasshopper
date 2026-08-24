namespace GonieGonie.EnergyPlus.Runtime.Tests;

public sealed class RuntimeBootstrapperTests
{
    [Fact]
    public void SupportedDistributionPinsOfficialArchiveAndStablePerUserTarget()
    {
        var distribution = EnergyPlusRuntimeDistribution.Supported;
        var expectedTarget = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GonieGonie",
            "BuildingEnergyRuntime",
            "EnergyPlus",
            "24.2.0-94a887817b");

        Assert.Equal(
            "https://github.com/NREL/EnergyPlus/releases/download/v24.2.0a/"
                + EnergyPlusRuntimeDistribution.SupportedArchiveFileName,
            distribution.ArchiveUri.AbsoluteUri);
        Assert.Equal(179248139, distribution.Manifest.EnergyPlusArchiveSize);
        Assert.Equal(
            "26c7c22b731f54031626750284c8b613fb8f03c3aa56b6bc7ec65b6bf8668df1",
            distribution.Manifest.EnergyPlusArchiveSha256);
        Assert.Equal(expectedTarget, EnergyPlusRuntimePaths.DefaultRuntimeRoot);
    }

    [Fact]
    public async Task DistributionAndBuiltInDownloaderRejectNonHttpsSources()
    {
        var insecureUri = new Uri("http://example.invalid/energyplus.zip");
        Assert.Throws<ArgumentException>(() => new EnergyPlusRuntimeDistribution(
            insecureUri,
            EnergyPlusRuntimeManifest.Supported));

        using var directory = new TestDirectory();
        var destination = System.IO.Path.Combine(directory.Path, "must-not-download.partial");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new HttpEnergyPlusRuntimeArchiveDownloader().DownloadAsync(
                insecureUri,
                destination,
                progress: null,
                CancellationToken.None));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task InstallsNestedArchiveTransactionallyAndResolverFindsCache()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var downloader = DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath);
        var target = System.IO.Path.Combine(directory.Path, "cache", "24.2.0-test");
        var progress = new CollectingProgress<EnergyPlusRuntimeBootstrapProgress>();
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(fixture.Distribution, downloader);

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target },
            progress);

        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.Equal(EnergyPlusRuntimeBootstrapDisposition.Installed, result.Disposition);
        Assert.Equal(1, downloader.CallCount);
        Assert.Equal(target, result.Runtime?.RootPath);
        Assert.True(File.Exists(System.IO.Path.Combine(target, "ExampleFiles", "example.idf")));
        Assert.Contains(
            progress.Updates,
            update => update.Stage == EnergyPlusRuntimeBootstrapStage.DownloadingArchive);
        Assert.Contains(
            progress.Updates,
            update => update.Stage == EnergyPlusRuntimeBootstrapStage.PromotingRuntime);
        Assert.Equal(EnergyPlusRuntimeBootstrapStage.Completed, progress.Updates[^1].Stage);

        var resolution = await new RuntimeResolver(fixture.Manifest).ResolveAsync(
            new EnergyPlusRuntimeResolveOptions
            {
                CachedRuntimeRoot = target,
                SearchEnvironmentVariables = false,
                SearchDefaultInstallLocation = false
            });
        Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);
        Assert.Contains(target, resolution.AttemptedRoots);
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task ReusesExistingVerifiedRuntimeWithoutDownloading()
    {
        using var directory = new TestDirectory();
        var (runtime, manifest) = await TestRuntimeFactory.CreateAsync(directory);
        var downloader = new DelegateRuntimeArchiveDownloader((_, _) =>
            throw new InvalidOperationException("Download must not run for a verified runtime."));
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            new EnergyPlusRuntimeDistribution(
                new Uri("https://example.invalid/must-not-download.zip"),
                manifest),
            downloader);

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = runtime.RootPath });

        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.Equal(EnergyPlusRuntimeBootstrapDisposition.Reused, result.Disposition);
        Assert.Equal(0, downloader.CallCount);
        Assert.Equal(runtime.RootPath, result.Runtime?.RootPath);
    }

    [Fact]
    public async Task RejectsCorruptArchiveAndCleansPartialAndStagingFiles()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var corruptDistribution = new EnergyPlusRuntimeDistribution(
            fixture.Distribution.ArchiveUri,
            fixture.Manifest with
            {
                EnergyPlusArchiveSha256 = new string('0', 64)
            });
        var target = System.IO.Path.Combine(directory.Path, "cache", "corrupt-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            corruptDistribution,
            DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target });

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, result.Failure?.Category);
        Assert.Equal("RUNTIME_ARCHIVE_HASH_MISMATCH", result.Failure?.Code);
        Assert.False(Directory.Exists(target));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task CancellationStopsDownloadAndCleansOperationFiles()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloader = new DelegateRuntimeArchiveDownloader(async (destination, cancellationToken) =>
        {
            await File.WriteAllTextAsync(destination, "partial", cancellationToken);
            started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        var target = System.IO.Path.Combine(directory.Path, "cache", "cancelled-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(fixture.Distribution, downloader);
        using var cancellation = new CancellationTokenSource();

        var operation = bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target },
            cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var result = await operation;

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.Cancelled, result.Failure?.Category);
        Assert.Equal("RUNTIME_BOOTSTRAP_CANCELLED", result.Failure?.Code);
        Assert.False(Directory.Exists(target));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task ConcurrentPreparationsConvergeOnOneVerifiedRuntime()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var downloader = DelegateRuntimeArchiveDownloader.Copying(
            fixture.ArchivePath,
            TimeSpan.FromMilliseconds(200));
        var target = System.IO.Path.Combine(directory.Path, "cache", "concurrent-target");
        var options = new EnergyPlusRuntimeBootstrapOptions
        {
            TargetRoot = target,
            LockWaitTimeout = TimeSpan.FromSeconds(5),
            LockRetryDelay = TimeSpan.FromMilliseconds(20)
        };
        var firstBootstrapper = new EnergyPlusRuntimeBootstrapper(fixture.Distribution, downloader);
        var secondBootstrapper = new EnergyPlusRuntimeBootstrapper(fixture.Distribution, downloader);

        var results = await Task.WhenAll(
            firstBootstrapper.EnsureInstalledAsync(options),
            secondBootstrapper.EnsureInstalledAsync(options));

        Assert.All(results, result => Assert.True(
            result.IsSuccess,
            result.Failure?.Detail ?? result.Failure?.Message));
        Assert.Equal(1, downloader.CallCount);
        Assert.Single(results, result =>
            result.Disposition == EnergyPlusRuntimeBootstrapDisposition.Installed);
        Assert.Single(results, result =>
            result.Disposition == EnergyPlusRuntimeBootstrapDisposition.Reused);
        Assert.All(results, result => Assert.Equal(target, result.Runtime?.RootPath));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task LockWaitIsBoundedAndDoesNotStartDownload()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var downloader = DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath);
        var target = System.IO.Path.Combine(directory.Path, "cache", "locked-target");
        var parent = System.IO.Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        var lockPath = EnergyPlusRuntimeBootstrapper.GetInstallLockPath(target, parent);
        using var heldLock = new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(fixture.Distribution, downloader);

        var result = await bootstrapper.EnsureInstalledAsync(new EnergyPlusRuntimeBootstrapOptions
        {
            TargetRoot = target,
            LockWaitTimeout = TimeSpan.FromMilliseconds(100),
            LockRetryDelay = TimeSpan.FromMilliseconds(10)
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.Timeout, result.Failure?.Category);
        Assert.Equal("RUNTIME_INSTALL_LOCK_TIMEOUT", result.Failure?.Code);
        Assert.Equal(0, downloader.CallCount);
    }

    [Fact]
    public async Task UnsafeArchivePathIsRejectedWithoutEscapingOrLeavingResidue()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory, includeUnsafeTraversal: true);
        var target = System.IO.Path.Combine(directory.Path, "cache", "unsafe-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            fixture.Distribution,
            DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target });

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, result.Failure?.Category);
        Assert.Equal("ARCHIVE_PATH_UNSAFE", result.Failure?.Code);
        Assert.Empty(Directory.GetFiles(directory.Path, "escaped.txt", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(target));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task ExplicitInvalidTargetIsNotMutatedWithoutReplacementOptIn()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var target = System.IO.Path.Combine(directory.Path, "cache", "protected-target");
        Directory.CreateDirectory(target);
        var sentinel = System.IO.Path.Combine(target, "keep-me.txt");
        await File.WriteAllTextAsync(sentinel, "caller-owned");
        var downloader = DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath);
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(fixture.Distribution, downloader);

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target });

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.UserInput, result.Failure?.Category);
        Assert.Equal("RUNTIME_TARGET_REPLACEMENT_REQUIRED", result.Failure?.Code);
        Assert.Equal(0, downloader.CallCount);
        Assert.Equal("caller-owned", await File.ReadAllTextAsync(sentinel));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task InvalidExistingTargetIsAtomicallyReplacedByVerifiedRuntime()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var target = System.IO.Path.Combine(directory.Path, "cache", "repair-target");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(System.IO.Path.Combine(target, "untrusted.txt"), "invalid runtime");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            fixture.Distribution,
            DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions
            {
                TargetRoot = target,
                ReplaceInvalidExistingTarget = true
            });

        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.Equal(EnergyPlusRuntimeBootstrapDisposition.Installed, result.Disposition);
        Assert.False(File.Exists(System.IO.Path.Combine(target, "untrusted.txt")));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task OversizedDownloadStopsAtFirstByteBeyondPinnedSize()
    {
        using var directory = new TestDirectory();
        const long expectedSize = 10;
        var manifest = EnergyPlusRuntimeManifest.Supported with
        {
            EnergyPlusArchiveSize = expectedSize,
            EnergyPlusArchiveSha256 = new string('0', 64)
        };
        var distribution = new EnergyPlusRuntimeDistribution(
            new Uri("https://example.invalid/oversized.zip"),
            manifest);
        var downloader = new OversizedReportingDownloader(expectedSize + 100);
        var target = System.IO.Path.Combine(directory.Path, "cache", "oversized-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(distribution, downloader);

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target });

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, result.Failure?.Category);
        Assert.Equal("RUNTIME_ARCHIVE_SIZE_EXCEEDED", result.Failure?.Code);
        Assert.Equal(expectedSize + 1, downloader.BytesWritten);
        Assert.False(Directory.Exists(target));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task ExistingReparsePointTargetIsRejectedBeforeDownloadOrMutation()
    {
        using var directory = new TestDirectory();
        var realDirectory = System.IO.Path.Combine(directory.Path, "real-target");
        var link = System.IO.Path.Combine(directory.Path, "linked-target");
        Directory.CreateDirectory(realDirectory);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(realDirectory, "keep-me.txt"),
            "reparse target content");
        var createdLink = false;
        string? reparseTarget = null;
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, realDirectory);
                createdLink = true;
                reparseTarget = link;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is PlatformNotSupportedException)
            {
                reparseTarget = FindExistingReparsePoint();
            }

            Assert.False(string.IsNullOrWhiteSpace(reparseTarget));
            var fixture = TestRuntimeArchiveFactory.Create(directory);
            var downloader = DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath);
            var bootstrapper = new EnergyPlusRuntimeBootstrapper(fixture.Distribution, downloader);

            var result = await bootstrapper.EnsureInstalledAsync(new EnergyPlusRuntimeBootstrapOptions
            {
                TargetRoot = reparseTarget,
                ReplaceInvalidExistingTarget = true
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(EnergyPlusFailureCategory.RuntimeEnvironment, result.Failure?.Category);
            Assert.Equal("RUNTIME_REPARSE_POINT_REJECTED", result.Failure?.Code);
            Assert.Equal(0, downloader.CallCount);
            Assert.Equal(
                "reparse target content",
                await File.ReadAllTextAsync(System.IO.Path.Combine(realDirectory, "keep-me.txt")));
        }
        finally
        {
            if (createdLink && Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }
    }

    private static void AssertNoOperationResidue(string targetRoot)
    {
        var parent = System.IO.Path.GetDirectoryName(targetRoot)!;
        if (!Directory.Exists(parent))
        {
            return;
        }

        var residue = Directory.EnumerateFileSystemEntries(parent)
            .Select(System.IO.Path.GetFileName)
            .Where(name => name is not null
                && (name.EndsWith(".download.partial", StringComparison.Ordinal)
                    || name.EndsWith(".staging", StringComparison.Ordinal)
                    || name.EndsWith(".displaced", StringComparison.Ordinal)))
            .ToArray();
        Assert.Empty(residue);
    }

    private static string? FindExistingReparsePoint()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(userProfile))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(userProfile))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        return path;
                    }
                }
                catch (Exception exception) when (
                    exception is IOException || exception is UnauthorizedAccessException)
                {
                    // Continue to another compatibility junction in the profile.
                }
            }
        }

        const string legacyProfilesJunction = @"C:\Documents and Settings";
        return File.Exists(legacyProfilesJunction) || Directory.Exists(legacyProfilesJunction)
            ? legacyProfilesJunction
            : null;
    }

    private sealed class OversizedReportingDownloader : IEnergyPlusRuntimeArchiveDownloader
    {
        private readonly long requestedBytes;

        internal OversizedReportingDownloader(long requestedBytes)
        {
            this.requestedBytes = requestedBytes;
        }

        internal long BytesWritten { get; private set; }

        public Task DownloadAsync(
            Uri sourceUri,
            string destinationPartialPath,
            IProgress<EnergyPlusRuntimeDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            using var stream = new FileStream(
                destinationPartialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            for (long index = 0; index < requestedBytes; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stream.WriteByte(0x42);
                BytesWritten++;
                progress?.Report(new EnergyPlusRuntimeDownloadProgress(BytesWritten, TotalBytes: null));
            }

            return Task.CompletedTask;
        }
    }
}
