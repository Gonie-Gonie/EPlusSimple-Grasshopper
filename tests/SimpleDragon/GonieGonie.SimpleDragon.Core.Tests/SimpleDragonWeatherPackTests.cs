using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class SimpleDragonWeatherPackTests
{
    [Fact]
    public void SupportedManifestPinsTheUpstreamCompatibilityWeatherArchive()
    {
        SimpleDragonWeatherPackManifest manifest = SimpleDragonWeatherPackManifest.Supported;

        Assert.Equal("korean-tmy-v1", manifest.PackId);
        Assert.Equal("KoreanTMY-v1.zip", manifest.ArchiveFileName);
        Assert.Equal(128349513, manifest.ArchiveSize);
        Assert.Equal(
            "fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0",
            manifest.ArchiveSha256);
    }

    [Fact]
    public void ExtractsOnlyAddressSelectedEpwAndThenReusesVerifiedCache()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\nDATA PERIODS,1,1,Data,Sunday,1/1,12/31\n"),
            ("other.epw", "LOCATION,Other,Fixture\n"));
        var resolver = new SimpleDragonWeatherPackResolver(fixture.Manifest);
        WeatherSelection selection = Selection("selected.epw");

        SimpleDragonWeatherFileResolution first = resolver.Resolve(selection, fixture.Options);
        SimpleDragonWeatherFileResolution second = resolver.Resolve(selection, fixture.Options);

        Assert.True(first.IsSuccess, string.Join(" | ", first.Diagnostics.Select(item => item.Message)));
        Assert.True(first.Extracted);
        Assert.Equal(Path.Combine(fixture.CacheRoot, "selected.epw"), first.FilePath);
        Assert.True(File.Exists(first.FilePath));
        Assert.False(File.Exists(Path.Combine(fixture.CacheRoot, "other.epw")));
        Assert.True(second.IsSuccess, string.Join(" | ", second.Diagnostics.Select(item => item.Message)));
        Assert.False(second.Extracted);
        Assert.Equal(first.FilePath, second.FilePath);
    }

    [Fact]
    public void ResolveManyExtractsDistinctSelectionsFromOneVerifiedArchiveAndDeduplicatesNames()
    {
        using var fixture = new WeatherArchiveFixture(
            ("first.epw", "LOCATION,First,Fixture\nfirst\n"),
            ("second.epw", "LOCATION,Second,Fixture\nsecond\n"),
            ("unused.epw", "LOCATION,Unused,Fixture\nunused\n"));
        var resolver = new SimpleDragonWeatherPackResolver(fixture.Manifest);

        IReadOnlyList<SimpleDragonWeatherFileResolution> results = resolver.ResolveMany(
            new[]
            {
                Selection("first.epw"),
                Selection("second.epw"),
                Selection("first.epw"),
            },
            fixture.Options);

        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.True(
            result.IsSuccess,
            string.Join(" | ", result.Diagnostics.Select(item => item.Message))));
        Assert.Same(results[0], results[2]);
        Assert.Equal(Path.Combine(fixture.CacheRoot, "first.epw"), results[0].FilePath);
        Assert.Equal(Path.Combine(fixture.CacheRoot, "second.epw"), results[1].FilePath);
        Assert.False(File.Exists(Path.Combine(fixture.CacheRoot, "unused.epw")));
        Assert.Equal(2, Directory.GetFiles(fixture.CacheRoot, "*.epw").Length);
        Assert.Empty(Directory.GetFiles(fixture.CacheRoot, "*.partial"));
    }

    [Fact]
    public void ResolveManyWithNoSelectionsDoesNotRequireAnArchive()
    {
        IReadOnlyList<SimpleDragonWeatherFileResolution> results =
            new SimpleDragonWeatherPackResolver().ResolveMany(
                Array.Empty<WeatherSelection>());

        Assert.Empty(results);
    }

    [Fact]
    public void ReplacesAStaleCachedEpwWithVerifiedArchiveBytes()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\nverified\n"));
        Directory.CreateDirectory(fixture.CacheRoot);
        File.WriteAllText(Path.Combine(fixture.CacheRoot, "selected.epw"), "LOCATION,Stale\nstale\n");

        SimpleDragonWeatherFileResolution result = new SimpleDragonWeatherPackResolver(fixture.Manifest)
            .Resolve(Selection("selected.epw"), fixture.Options);

        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.True(result.Extracted);
        Assert.Contains("verified", File.ReadAllText(result.FilePath!));
    }

    [Fact]
    public void PreservesTheLastKnownGoodEpwWhenAtomicReplacementCannotComplete()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\nverified replacement\n"));
        Directory.CreateDirectory(fixture.CacheRoot);
        string targetPath = Path.Combine(fixture.CacheRoot, "selected.epw");
        const string StaleContent = "LOCATION,Stale,Fixture\nlast known good\n";
        File.WriteAllText(targetPath, StaleContent);

        using var replacementBlocker = new FileStream(
            targetPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        SimpleDragonWeatherFileResolution result = new SimpleDragonWeatherPackResolver(fixture.Manifest)
            .Resolve(Selection("selected.epw"), fixture.Options);

        Assert.False(result.IsSuccess);
        Assert.Equal("SD.WEATHER.EXTRACTION_FAILED", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(StaleContent, File.ReadAllText(targetPath));
        Assert.Empty(Directory.GetFiles(fixture.CacheRoot, "*.partial"));
        Assert.Empty(Directory.GetFiles(fixture.CacheRoot, "*.backup"));
    }

    [Fact]
    public void RevalidatesArchiveContentEvenWhenSizeAndTimestampAreUnchanged()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\nverified\n"));
        var resolver = new SimpleDragonWeatherPackResolver(fixture.Manifest);
        WeatherSelection selection = Selection("selected.epw");
        SimpleDragonWeatherFileResolution first = resolver.Resolve(selection, fixture.Options);
        Assert.True(first.IsSuccess, string.Join(" | ", first.Diagnostics.Select(item => item.Message)));

        DateTime timestamp = File.GetLastWriteTimeUtc(fixture.ArchivePath);
        byte[] bytes = File.ReadAllBytes(fixture.ArchivePath);
        bytes[bytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(fixture.ArchivePath, bytes);
        File.SetLastWriteTimeUtc(fixture.ArchivePath, timestamp);

        SimpleDragonWeatherFileResolution second = resolver.Resolve(selection, fixture.Options);

        Assert.False(second.IsSuccess);
        Assert.Equal("SD.WEATHER.PACK_INTEGRITY_FAILED", Assert.Single(second.Diagnostics).Code);
    }

    [Fact]
    public void RejectsArchiveHashMismatchWithoutLeavingAnEpwOrPartialFile()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\n"));
        var invalidManifest = new SimpleDragonWeatherPackManifest(
            fixture.Manifest.PackId,
            fixture.Manifest.ArchiveFileName,
            fixture.Manifest.ArchiveSize,
            new string('0', 64));

        SimpleDragonWeatherFileResolution result = new SimpleDragonWeatherPackResolver(invalidManifest)
            .Resolve(Selection("selected.epw"), fixture.Options);

        Assert.False(result.IsSuccess);
        Assert.Equal("SD.WEATHER.PACK_INTEGRITY_FAILED", Assert.Single(result.Diagnostics).Code);
        Assert.False(File.Exists(Path.Combine(fixture.CacheRoot, "selected.epw")));
        Assert.Empty(Directory.Exists(fixture.CacheRoot)
            ? Directory.GetFiles(fixture.CacheRoot, "*.partial")
            : Array.Empty<string>());
    }

    [Fact]
    public void RejectsMissingSelectedEntryAfterArchiveVerification()
    {
        using var fixture = new WeatherArchiveFixture(
            ("other.epw", "LOCATION,Other,Fixture\n"));

        SimpleDragonWeatherFileResolution result = new SimpleDragonWeatherPackResolver(fixture.Manifest)
            .Resolve(Selection("selected.epw"), fixture.Options);

        Assert.False(result.IsSuccess);
        Assert.Equal("SD.WEATHER.PACK_INTEGRITY_FAILED", Assert.Single(result.Diagnostics).Code);
        Assert.Contains("absent", result.Diagnostics[0].SuggestedAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTraversalLikeSelectedFilenameBeforeWritingOutsideCache()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\n"));
        string escapedPath = Path.Combine(fixture.Root, "escaped.epw");

        SimpleDragonWeatherFileResolution result = new SimpleDragonWeatherPackResolver(fixture.Manifest)
            .Resolve(Selection(".." + Path.DirectorySeparatorChar + "escaped.epw"), fixture.Options);

        Assert.False(result.IsSuccess);
        Assert.Equal("SD.WEATHER.PACK_INTEGRITY_FAILED", Assert.Single(result.Diagnostics).Code);
        Assert.False(File.Exists(escapedPath));
    }

    [Fact]
    public void RejectsASelectedEntryThatIsNotAnEpwDocument()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "not an EPW document\n"));

        SimpleDragonWeatherFileResolution result = new SimpleDragonWeatherPackResolver(fixture.Manifest)
            .Resolve(Selection("selected.epw"), fixture.Options);

        Assert.False(result.IsSuccess);
        Assert.Equal("SD.WEATHER.PACK_INTEGRITY_FAILED", Assert.Single(result.Diagnostics).Code);
        Assert.False(File.Exists(Path.Combine(fixture.CacheRoot, "selected.epw")));
    }

    [Fact]
    public async Task ConcurrentResolversConvergeOnOneVerifiedCachedFile()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\nconcurrent\n"));
        var resolver = new SimpleDragonWeatherPackResolver(fixture.Manifest);
        WeatherSelection selection = Selection("selected.epw");

        SimpleDragonWeatherFileResolution[] results = await Task.WhenAll(
            Task.Run(() => resolver.Resolve(selection, fixture.Options)),
            Task.Run(() => resolver.Resolve(selection, fixture.Options)));

        Assert.All(results, result => Assert.True(
            result.IsSuccess,
            string.Join(" | ", result.Diagnostics.Select(item => item.Message))));
        Assert.Single(results, result => result.Extracted);
        Assert.Single(results, result => !result.Extracted);
        Assert.Single(Directory.GetFiles(fixture.CacheRoot, "*.epw"));
        Assert.Empty(Directory.GetFiles(fixture.CacheRoot, "*.partial"));
    }

    [Fact]
    public async Task CancellationInterruptsWeatherPackLockWaiting()
    {
        using var fixture = new WeatherArchiveFixture(
            ("selected.epw", "LOCATION,Selected,Fixture\n"));
        Directory.CreateDirectory(fixture.CacheRoot);
        string lockPath = Path.Combine(fixture.CacheRoot, ".weather-pack.lock");
        using var heldLock = new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        SimpleDragonWeatherFileResolution result = await Task.Run(() =>
            new SimpleDragonWeatherPackResolver(fixture.Manifest).Resolve(
                Selection("selected.epw"),
                fixture.Options,
                cancellation.Token));

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SD.WEATHER.EXTRACTION_CANCELLED", diagnostic.Code);
        Assert.False(diagnostic.IsFailure);
    }

    private static WeatherSelection Selection(string epwFileName)
    {
        var metadata = new WeatherMetadata(
            "Fixture District",
            "0000000000",
            "City",
            37,
            127,
            "Fixture Station",
            "Station",
            37,
            127,
            epwFileName);
        return new WeatherSelection(metadata, "Fixture Climate", new DateTime(2020, 1, 1));
    }

    private sealed class WeatherArchiveFixture : IDisposable
    {
        public WeatherArchiveFixture(params (string Name, string Content)[] entries)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "GonieGonie.SimpleDragon.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            ArchivePath = Path.Combine(Root, "fixture-weather.zip");
            using (ZipArchive archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Create))
            {
                foreach ((string name, string content) in entries)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                    using Stream stream = entry.Open();
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                    writer.Write(content);
                }
            }

            var information = new FileInfo(ArchivePath);
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ArchivePath)))
                .ToLowerInvariant();
            Manifest = new SimpleDragonWeatherPackManifest(
                "fixture-weather",
                Path.GetFileName(ArchivePath),
                information.Length,
                hash);
            CacheRoot = Path.Combine(Root, "cache");
            Options = new SimpleDragonWeatherPackOptions
            {
                ArchivePath = ArchivePath,
                CacheRoot = CacheRoot,
                LockWaitTimeout = TimeSpan.FromSeconds(5),
                LockRetryDelay = TimeSpan.FromMilliseconds(10),
            };
        }

        public string Root { get; }

        public string ArchivePath { get; }

        public string CacheRoot { get; }

        public SimpleDragonWeatherPackManifest Manifest { get; }

        public SimpleDragonWeatherPackOptions Options { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
