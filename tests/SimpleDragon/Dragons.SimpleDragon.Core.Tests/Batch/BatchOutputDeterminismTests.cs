using Dragons.SimpleDragon.Batch;
using System.Text;

namespace Dragons.SimpleDragon.Tests.Batch;

public sealed class BatchOutputDeterminismTests
{
    [Fact]
    public async Task CaseIdsManifestAndCsvAreStableAcrossCompletionOrderAndCacheState()
    {
        using var directory = new TemporaryBatchDirectory();
        IReadOnlyList<BatchCaseDefinition> cases = BatchTestSupport.Cases(4);
        BatchRunOptions options = BatchTestSupport.Options(directory, parallelism: 3);
        var firstExecutor = Executor(reverseDelay: true);
        BatchRunResult first = await BatchRunner.RunAsync(cases, firstExecutor, options);
        var cachedExecutor = Executor(reverseDelay: false);
        BatchRunResult cached = await BatchRunner.RunAsync(cases, cachedExecutor, options);

        Assert.Equal(0, cachedExecutor.InvocationCount);
        Assert.Equal(first.RunFingerprint, cached.RunFingerprint);
        Assert.Equal(first.Cases.Select(item => item.CaseId), cached.Cases.Select(item => item.CaseId));
        Assert.Equal(first.CombinedCsv, cached.CombinedCsv);
        Assert.Equal(first.ReproducibilityManifest, cached.ReproducibilityManifest);
        Assert.DoesNotContain("cache_hit", first.CombinedCsv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("timestamp", first.ReproducibilityManifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(",succeeded,", first.CombinedCsv, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"succeeded\"", first.ReproducibilityManifest, StringComparison.Ordinal);
        Assert.Equal(first.CombinedCsv, await File.ReadAllTextAsync(first.CombinedCsvPath!));
        Assert.Equal(first.ReproducibilityManifest, await File.ReadAllTextAsync(first.ManifestPath!));
        byte[] csvBytes = await File.ReadAllBytesAsync(first.CombinedCsvPath!);
        byte[] manifestBytes = await File.ReadAllBytesAsync(first.ManifestPath!);
        Assert.True(csvBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.False(manifestBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }

    [Fact]
    public void PublicIdentityHelperIncludesEveryFullContentInput()
    {
        BatchRuntimeIdentity runtime = TestBatchExecutor.Runtime("runtime");
        string hash = BatchDeterminism.Sha256Text("weather");
        var baseline = new BatchDeterministicInput(
            "case-a",
            "{\"model\":1}",
            "{\"case\":1}",
            "executor/v1",
            "{\"execution\":1}",
            "{\"output\":1}",
            "simple-core",
            "invisible-core",
            "upstream-repository",
            "upstream-commit",
            "upstream-version",
            runtime,
            hash);
        var changedOutput = new BatchDeterministicInput(
            "case-a",
            "{\"model\":1}",
            "{\"case\":1}",
            "executor/v1",
            "{\"execution\":1}",
            "{\"output\":2}",
            "simple-core",
            "invisible-core",
            "upstream-repository",
            "upstream-commit",
            "upstream-version",
            runtime,
            hash);

        Assert.NotEqual(baseline.CacheKey, changedOutput.CacheKey);
        Assert.Equal(baseline.CacheKey, BatchDeterminism.Sha256Text(baseline.ToCanonicalJson()));
    }

    [Fact]
    public void CanonicalModelIncludesExplicitWeatherSelectionOverride()
    {
        GreenRetrofitModel template = BatchTestSupport.Model;
        var firstWeather = new WeatherSelection(
            new WeatherMetadata(
                "District A",
                "1111111111",
                "Suburbs",
                37.5,
                127.0,
                "Station A",
                "TMY",
                37.4,
                126.9,
                "station-a.epw"),
            "Climate A",
            new DateTime(2020, 1, 1));
        var secondWeather = new WeatherSelection(
            new WeatherMetadata(
                "District B",
                "2222222222",
                "City",
                35.1,
                129.0,
                "Station B",
                "TMY",
                35.2,
                129.1,
                "station-b.epw"),
            "Climate B",
            new DateTime(2021, 1, 1));
        GreenRetrofitModel first = WithWeather(template, firstWeather);
        GreenRetrofitModel second = WithWeather(template, secondWeather);

        string firstIdentity = BatchDeterminism.CanonicalizeModel(first);
        string secondIdentity = BatchDeterminism.CanonicalizeModel(second);

        Assert.NotEqual(firstIdentity, secondIdentity);
        Assert.Contains("station-a.epw", firstIdentity, StringComparison.Ordinal);
        Assert.Contains("station-b.epw", secondIdentity, StringComparison.Ordinal);
    }

    private static TestBatchExecutor Executor(bool reverseDelay)
    {
        return new TestBatchExecutor(async (context, cancellationToken) =>
        {
            int delay = reverseDelay ? 5 - context.Index : context.Index + 1;
            await Task.Delay(TimeSpan.FromMilliseconds(delay * 4), cancellationToken);
            return BatchCaseExecution.Success(new Dictionary<string, double>
            {
                ["area_m2"] = context.Model.Area,
                ["index"] = context.Index,
            });
        });
    }

    private static GreenRetrofitModel WithWeather(
        GreenRetrofitModel source,
        WeatherSelection weather)
    {
        return new GreenRetrofitModel(
            source.Name,
            source.NorthAxis,
            source.Address,
            source.Vintage,
            source.IsMultifamilyHousing,
            source.Floors,
            source.Materials,
            source.SurfaceConstructions,
            source.FenestrationConstructions,
            source.SourceSystems,
            source.SupplySystems,
            source.VentilationSystems,
            source.PhotovoltaicSystems,
            weather);
    }
}
