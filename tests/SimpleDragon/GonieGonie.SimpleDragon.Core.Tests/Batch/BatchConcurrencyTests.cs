using GonieGonie.SimpleDragon.Batch;

namespace GonieGonie.SimpleDragon.Tests.Batch;

public sealed class BatchConcurrencyTests
{
    [Fact]
    public async Task PreservesInputOrderAndNeverExceedsConfiguredParallelism()
    {
        using var directory = new TemporaryBatchDirectory();
        var executor = new TestBatchExecutor(async (context, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20 + ((8 - context.Index) * 3)), cancellationToken);
            return BatchCaseExecution.Success(new Dictionary<string, double>
            {
                ["input_index"] = context.Index,
            });
        });
        var throwingObserver = new InlineTestProgress<BatchProgressSnapshot>(
            _ => throw new InvalidOperationException("Observers must not control the runner."));

        BatchRunResult result = await BatchRunner.RunAsync(
            BatchTestSupport.Cases(8),
            executor,
            BatchTestSupport.Options(directory, parallelism: 3),
            throwingObserver);

        Assert.Equal(3, executor.MaximumActive);
        Assert.InRange(executor.MaximumActive, 1, 3);
        Assert.Equal(Enumerable.Range(0, 8), result.Cases.Select(item => item.Index));
        Assert.Equal(Enumerable.Range(0, 8).Select(index => (double)index),
            result.Cases.Select(item => item.Metrics["input_index"]));
        Assert.Equal(8, result.ProgressSnapshots[^1].Completed);
        Assert.Equal(0, result.ProgressSnapshots[^1].Active);
    }
}
