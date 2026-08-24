using GonieGonie.SimpleDragon.Batch;

namespace GonieGonie.SimpleDragon.Tests.Batch;

public sealed class BatchCancellationTests
{
    [Fact]
    public async Task CancellationReturnsCompletedSuccessesAndNeverCachesCancelledCases()
    {
        using var directory = new TemporaryBatchDirectory();
        using var cancellation = new CancellationTokenSource();
        var executor = new TestBatchExecutor(async (context, cancellationToken) =>
        {
            TimeSpan delay = context.Index == 0
                ? TimeSpan.FromMilliseconds(20)
                : TimeSpan.FromSeconds(5);
            await Task.Delay(delay, cancellationToken);
            return BatchCaseExecution.Success(new Dictionary<string, double> { ["index"] = context.Index });
        });
        var progress = new InlineTestProgress<BatchProgressSnapshot>(snapshot =>
        {
            if (snapshot.Succeeded == 1)
            {
                cancellation.Cancel();
            }
        });

        BatchRunResult result = await BatchRunner.RunAsync(
            BatchTestSupport.Cases(6),
            executor,
            BatchTestSupport.Options(directory, parallelism: 2),
            progress,
            cancellation.Token);

        Assert.Equal(6, result.Cases.Count);
        Assert.Equal(BatchCaseStatus.Succeeded, result.Cases[0].Status);
        Assert.Contains(result.Cases, item => item.Status == BatchCaseStatus.Cancelled);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(5, result.CancelledCount);
        Assert.Equal(0, result.FailureCount);
        Assert.All(
            result.Cases.Skip(1),
            item => Assert.Equal(BatchCaseStatus.Cancelled, item.Status));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(directory.Path, "cache"), "*.json"));
        Assert.Equal(6, result.ProgressSnapshots[^1].Completed);
    }
}
