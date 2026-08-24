using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Batch;

namespace GonieGonie.SimpleDragon.Tests.Batch;

public sealed class BatchPartialFailureTests
{
    [Fact]
    public async Task FailedCasesDoNotDiscardSuccessfulCases()
    {
        using var directory = new TemporaryBatchDirectory();
        var executor = new TestBatchExecutor((context, _) => context.Index switch
        {
            1 => throw new InvalidOperationException("synthetic failure"),
            3 => Task.FromResult(BatchCaseExecution.Failure(new[]
            {
                new Diagnostic("SD.TEST.EXPLICIT_FAILURE", DiagnosticSeverity.Error, "Explicit failure."),
            })),
            _ => Task.FromResult(BatchCaseExecution.Success(new Dictionary<string, double>
            {
                ["value"] = context.Index + 0.5,
            })),
        });

        BatchRunResult result = await BatchRunner.RunAsync(
            BatchTestSupport.Cases(5),
            executor,
            BatchTestSupport.Options(directory, parallelism: 3));

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.Equal(
            new[]
            {
                BatchCaseStatus.Succeeded,
                BatchCaseStatus.Failed,
                BatchCaseStatus.Succeeded,
                BatchCaseStatus.Failed,
                BatchCaseStatus.Succeeded,
            },
            result.Cases.Select(item => item.Status));
        Assert.Contains("SD.BATCH.CASE_FAILED", result.Cases[1].Diagnostics.Select(item => item.Code));
        Assert.Contains("SD.TEST.EXPLICIT_FAILURE", result.Cases[3].Diagnostics.Select(item => item.Code));
        Assert.Contains(",Succeeded,", result.CombinedCsv, StringComparison.Ordinal);
        Assert.Contains(",Failed,", result.CombinedCsv, StringComparison.Ordinal);
    }
}
