using Dragons.SimpleDragon.Batch;

namespace Dragons.SimpleDragon.Tests.Batch;

public sealed class BatchCacheTests
{
    [Fact]
    public async Task ReusesSuccessfulEntriesButFullWeatherContentAndRuntimeIdentityInvalidateThem()
    {
        using var directory = new TemporaryBatchDirectory();
        string weather = Path.Combine(directory.Path, "weather.epw");
        await File.WriteAllTextAsync(weather, "weather-A");
        IReadOnlyList<BatchCaseDefinition> cases = BatchTestSupport.Cases(1, weather);
        BatchRunOptions options = BatchTestSupport.Options(directory, parallelism: 1);

        var firstExecutor = SuccessExecutor("runtime-a");
        BatchRunResult first = await BatchRunner.RunAsync(cases, firstExecutor, options);
        var cachedExecutor = SuccessExecutor("runtime-a");
        BatchRunResult cached = await BatchRunner.RunAsync(cases, cachedExecutor, options);

        Assert.Equal(1, firstExecutor.InvocationCount);
        Assert.Equal(0, cachedExecutor.InvocationCount);
        Assert.True(cached.Cases[0].CacheHit);
        Assert.Equal(first.Cases[0].CacheKey, cached.Cases[0].CacheKey);

        await File.WriteAllTextAsync(weather, "weather-B");
        var changedWeatherExecutor = SuccessExecutor("runtime-a");
        BatchRunResult changedWeather = await BatchRunner.RunAsync(cases, changedWeatherExecutor, options);
        Assert.Equal(1, changedWeatherExecutor.InvocationCount);
        Assert.NotEqual(first.Cases[0].CacheKey, changedWeather.Cases[0].CacheKey);

        var changedRuntimeExecutor = SuccessExecutor("runtime-b");
        BatchRunResult changedRuntime = await BatchRunner.RunAsync(cases, changedRuntimeExecutor, options);
        Assert.Equal(1, changedRuntimeExecutor.InvocationCount);
        Assert.NotEqual(changedWeather.Cases[0].CacheKey, changedRuntime.Cases[0].CacheKey);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.AllDirectories));
    }

    private static TestBatchExecutor SuccessExecutor(string runtimeSeed)
    {
        return new TestBatchExecutor(
            (context, _) => Task.FromResult(BatchCaseExecution.Success(new Dictionary<string, double>
            {
                ["area"] = context.Model.Area,
            })),
            runtimeIdentity: TestBatchExecutor.Runtime(runtimeSeed));
    }
}
