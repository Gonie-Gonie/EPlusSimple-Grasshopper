namespace Dragons.EnergyPlus.Runtime.Tests;

public sealed class BundledRuntimeBootstrapperTests
{
    [Fact]
    public void LocatorFindsOnlyTheExactBundlePathWithinTheBoundedAncestorSearch()
    {
        using var directory = new TestDirectory();
        var packageRoot = Path.Combine(directory.Path, "package");
        var assemblyDirectory = Path.Combine(packageRoot, "frameworks", "net8.0");
        Directory.CreateDirectory(assemblyDirectory);
        directory.WriteFile(
            Path.Combine("package", "frameworks", "net8.0", "runtime", "energyplus", "wrong.zip"),
            "decoy");
        var expected = directory.WriteFile(
            Path.Combine(
                "package",
                "runtime",
                "energyplus",
                EnergyPlusRuntimeDistribution.SupportedArchiveFileName),
            "bundle");

        var locator = new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory);

        Assert.Equal(expected, locator.FindArchivePath());
    }

    [Fact]
    public void LocatorDoesNotSearchBeyondItsMaximumAncestorDepth()
    {
        using var directory = new TestDirectory();
        var bundleRoot = Path.Combine(directory.Path, "package");
        var assemblyDirectory = bundleRoot;
        for (var index = 0;
            index <= AssemblyAdjacentEnergyPlusRuntimeArchiveLocator.MaximumAncestorLevels;
            index++)
        {
            assemblyDirectory = Path.Combine(assemblyDirectory, "level-" + index);
        }

        Directory.CreateDirectory(assemblyDirectory);
        directory.WriteFile(
            Path.Combine(
                "package",
                "runtime",
                "energyplus",
                EnergyPlusRuntimeDistribution.SupportedArchiveFileName),
            "out-of-range-bundle");

        var locator = new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory);

        Assert.Null(locator.FindArchivePath());
    }

    [Fact]
    public void LocatorFindsTheSetupPreparedDeveloperDistribution()
    {
        using var directory = new TestDirectory();
        var repositoryRoot = Path.Combine(directory.Path, "repository");
        var assemblyDirectory = Path.Combine(
            repositoryRoot,
            "temp",
            "build",
            "bin",
            "Runtime",
            "Release",
            "net8.0");
        Directory.CreateDirectory(assemblyDirectory);
        var expected = directory.WriteFile(
            Path.Combine(
                "repository",
                ".tools",
                "distributions",
                "energyplus",
                EnergyPlusRuntimeDistribution.SupportedArchiveFileName),
            "setup-prepared-bundle");

        var locator = new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory);

        Assert.Equal(expected, locator.FindArchivePath());
    }

    [Fact]
    public void LocatorFindsTheMatchingVersionInvisibleDragonYakSibling()
    {
        using var directory = new TestDirectory();
        var hostPackageRoot = Path.Combine(directory.Path, "packages", "8.0");
        var assemblyDirectory = Path.Combine(
            hostPackageRoot,
            "simple-dragon",
            "0.1.0",
            "net8.0");
        Directory.CreateDirectory(assemblyDirectory);
        directory.WriteFile(
            Path.Combine(
                "packages",
                "8.0",
                "invisible-dragon",
                "0.2.0",
                "runtime",
                "energyplus",
                EnergyPlusRuntimeDistribution.SupportedArchiveFileName),
            "wrong-version-decoy");
        var expected = directory.WriteFile(
            Path.Combine(
                "packages",
                "8.0",
                "invisible-dragon",
                "0.1.0",
                "runtime",
                "energyplus",
                EnergyPlusRuntimeDistribution.SupportedArchiveFileName),
            "matching-version-bundle");

        var locator = new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory);

        Assert.Equal(expected, locator.FindArchivePath());
    }

    [Fact]
    public async Task BundledArchiveIsCopiedAndVerifiedBeforeTheFallbackDownloader()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var assemblyDirectory = CreatePackageLayout(directory, fixture.ArchivePath);
        var fallback = new DelegateRuntimeArchiveDownloader((_, _) =>
            throw new InvalidOperationException("HTTPS fallback must not run when the bundle exists."));
        var progress = new CollectingProgress<EnergyPlusRuntimeBootstrapProgress>();
        var target = Path.Combine(directory.Path, "cache", "bundled-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            fixture.Distribution,
            fallback,
            new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target },
            progress);

        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.Equal(EnergyPlusRuntimeBootstrapDisposition.Installed, result.Disposition);
        Assert.Equal(0, fallback.CallCount);
        Assert.Contains(
            progress.Updates,
            update => update.Stage == EnergyPlusRuntimeBootstrapStage.DownloadingArchive
                && update.Message.Contains("bundled", StringComparison.OrdinalIgnoreCase));
        long[] byteProgress = progress.Updates
            .Where(update => update.Stage == EnergyPlusRuntimeBootstrapStage.DownloadingArchive)
            .Select(update => update.CompletedBytes)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
        Assert.NotEmpty(byteProgress);
        Assert.Equal(byteProgress.OrderBy(value => value), byteProgress);
        Assert.Equal(fixture.Manifest.EnergyPlusArchiveSize, byteProgress[^1]);
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task SiblingInvisibleDragonYakBundleIsCopiedBeforeTheFallbackDownloader()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var assemblyDirectory = CreateSiblingYakPackageLayout(directory, fixture.ArchivePath);
        var fallback = new DelegateRuntimeArchiveDownloader((_, _) =>
            throw new InvalidOperationException("HTTPS fallback must not run when the sibling bundle exists."));
        var target = Path.Combine(directory.Path, "cache", "sibling-bundled-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            fixture.Distribution,
            fallback,
            new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target });

        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.Equal(EnergyPlusRuntimeBootstrapDisposition.Installed, result.Disposition);
        Assert.Equal(0, fallback.CallCount);
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task InvalidBundledArchiveFailsIntegrityWithoutNetworkFallback()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var assemblyDirectory = CreatePackageLayout(directory, fixture.ArchivePath);
        var bundlePath = BundlePath(Path.GetDirectoryName(assemblyDirectory)!);
        using (var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var firstByte = stream.ReadByte();
            Assert.NotEqual(-1, firstByte);
            stream.Position = 0;
            stream.WriteByte((byte)(firstByte ^ 0xff));
        }

        var fallback = DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath);
        var target = Path.Combine(directory.Path, "cache", "invalid-bundle-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            fixture.Distribution,
            fallback,
            new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target });

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, result.Failure?.Category);
        Assert.Equal("RUNTIME_ARCHIVE_HASH_MISMATCH", result.Failure?.Code);
        Assert.Equal(0, fallback.CallCount);
        Assert.False(Directory.Exists(target));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task MissingBundleUsesTheInjectedHttpsFallback()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var assemblyDirectory = Path.Combine(directory.Path, "isolated-package");
        for (var index = 0;
            index <= AssemblyAdjacentEnergyPlusRuntimeArchiveLocator.MaximumAncestorLevels;
            index++)
        {
            assemblyDirectory = Path.Combine(assemblyDirectory, "level-" + index);
        }

        Directory.CreateDirectory(assemblyDirectory);
        var fallback = DelegateRuntimeArchiveDownloader.Copying(fixture.ArchivePath);
        var progress = new CollectingProgress<EnergyPlusRuntimeBootstrapProgress>();
        var target = Path.Combine(directory.Path, "cache", "fallback-target");
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            fixture.Distribution,
            fallback,
            new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target },
            progress);

        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.Equal(1, fallback.CallCount);
        Assert.Contains(
            progress.Updates,
            update => update.Stage == EnergyPlusRuntimeBootstrapStage.DownloadingArchive
                && update.Message.Contains("HTTPS", StringComparison.OrdinalIgnoreCase));
        AssertNoOperationResidue(target);
    }

    [Fact]
    public async Task CancellationStopsBundledCopyAndCleansOperationFiles()
    {
        using var directory = new TestDirectory();
        var fixture = TestRuntimeArchiveFactory.Create(directory);
        var assemblyDirectory = CreatePackageLayout(directory, fixture.ArchivePath);
        var fallback = new DelegateRuntimeArchiveDownloader((_, _) =>
            throw new InvalidOperationException("HTTPS fallback must not run when the bundle exists."));
        var target = Path.Combine(directory.Path, "cache", "cancelled-bundle-target");
        using var cancellation = new CancellationTokenSource();
        var progress = new ActionProgress<EnergyPlusRuntimeBootstrapProgress>(update =>
        {
            if (update.Stage == EnergyPlusRuntimeBootstrapStage.DownloadingArchive
                && update.CompletedBytes > 0)
            {
                cancellation.Cancel();
            }
        });
        var bootstrapper = new EnergyPlusRuntimeBootstrapper(
            fixture.Distribution,
            fallback,
            new AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(assemblyDirectory));

        var result = await bootstrapper.EnsureInstalledAsync(
            new EnergyPlusRuntimeBootstrapOptions { TargetRoot = target },
            progress,
            cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.Cancelled, result.Failure?.Category);
        Assert.Equal("RUNTIME_BOOTSTRAP_CANCELLED", result.Failure?.Code);
        Assert.Equal(0, fallback.CallCount);
        Assert.False(Directory.Exists(target));
        AssertNoOperationResidue(target);
    }

    private static string CreatePackageLayout(TestDirectory directory, string sourceArchive)
    {
        var packageRoot = Path.Combine(directory.Path, "package");
        var assemblyDirectory = Path.Combine(packageRoot, "net8.0");
        var bundlePath = BundlePath(packageRoot);
        Directory.CreateDirectory(assemblyDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(bundlePath)!);
        File.Copy(sourceArchive, bundlePath);
        return assemblyDirectory;
    }

    private static string CreateSiblingYakPackageLayout(
        TestDirectory directory,
        string sourceArchive)
    {
        var hostPackageRoot = Path.Combine(directory.Path, "packages", "8.0");
        var assemblyDirectory = Path.Combine(
            hostPackageRoot,
            "simple-dragon",
            "0.1.0",
            "net8.0");
        var bundlePath = BundlePath(Path.Combine(
            hostPackageRoot,
            "invisible-dragon",
            "0.1.0"));
        Directory.CreateDirectory(assemblyDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(bundlePath)!);
        File.Copy(sourceArchive, bundlePath);
        return assemblyDirectory;
    }

    private static string BundlePath(string packageRoot)
    {
        return Path.Combine(
            packageRoot,
            "runtime",
            "energyplus",
            EnergyPlusRuntimeDistribution.SupportedArchiveFileName);
    }

    private static void AssertNoOperationResidue(string targetRoot)
    {
        var parent = Path.GetDirectoryName(targetRoot)!;
        if (!Directory.Exists(parent))
        {
            return;
        }

        var residue = Directory.EnumerateFileSystemEntries(parent)
            .Select(Path.GetFileName)
            .Where(name => name is not null
                && (name.EndsWith(".download.partial", StringComparison.Ordinal)
                    || name.EndsWith(".staging", StringComparison.Ordinal)
                    || name.EndsWith(".displaced", StringComparison.Ordinal)))
            .ToArray();
        Assert.Empty(residue);
    }

    private sealed class ActionProgress<T> : IProgress<T>
    {
        private readonly Action<T> action;

        internal ActionProgress(Action<T> action)
        {
            this.action = action;
        }

        public void Report(T value)
        {
            action(value);
        }
    }
}
