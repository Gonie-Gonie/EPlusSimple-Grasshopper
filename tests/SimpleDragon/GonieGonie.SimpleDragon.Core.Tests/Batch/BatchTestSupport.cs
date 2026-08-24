using GonieGonie.SimpleDragon.Batch;

namespace GonieGonie.SimpleDragon.Tests.Batch;

internal static class BatchTestSupport
{
    internal static GreenRetrofitModel Model => GrmReader.ReadFile(FixturePath()).RequireModel();

    internal static IReadOnlyList<BatchCaseDefinition> Cases(int count, string? weatherFilePath = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => new BatchCaseDefinition(Model, weatherFilePath: weatherFilePath))
            .ToArray();
    }

    internal static BatchRunOptions Options(TemporaryBatchDirectory directory, int parallelism = 2)
    {
        return new BatchRunOptions
        {
            MaxDegreeOfParallelism = parallelism,
            OutputRootPath = directory.Path,
            UseCache = true,
            WriteOutputs = true,
        };
    }

    private static string FixturePath()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                "grm",
                "ASHRAE 140 modified.grm");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SimpleDragon GRM fixture.");
    }
}

internal sealed class TestBatchExecutor : IBatchCaseExecutor
{
    private readonly Func<BatchCaseContext, CancellationToken, Task<BatchCaseExecution>> _execute;
    private int _active;
    private int _invocations;
    private int _maximumActive;

    internal TestBatchExecutor(
        Func<BatchCaseContext, CancellationToken, Task<BatchCaseExecution>> execute,
        string executorIdentity = "GonieGonie.SimpleDragon.Tests.BatchExecutor/v1",
        BatchRuntimeIdentity? runtimeIdentity = null,
        string canonicalExecutionOptions = "{}",
        string canonicalOutputOptions = "{}")
    {
        _execute = execute;
        ExecutorIdentity = executorIdentity;
        RuntimeIdentity = runtimeIdentity ?? Runtime("runtime-a");
        CanonicalExecutionOptions = canonicalExecutionOptions;
        CanonicalOutputOptions = canonicalOutputOptions;
    }

    public string ExecutorIdentity { get; }

    public BatchRuntimeIdentity RuntimeIdentity { get; }

    public string CanonicalExecutionOptions { get; }

    public string CanonicalOutputOptions { get; }

    internal int InvocationCount => Volatile.Read(ref _invocations);

    internal int MaximumActive => Volatile.Read(ref _maximumActive);

    public async Task<BatchCaseExecution> ExecuteAsync(
        BatchCaseContext context,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocations);
        int active = Interlocked.Increment(ref _active);
        UpdateMaximum(active);
        try
        {
            return await _execute(context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    internal static BatchRuntimeIdentity Runtime(string seed)
    {
        return new BatchRuntimeIdentity(
            "24.2.0",
            seed,
            BatchDeterminism.Sha256Text(seed + ":exe"),
            BatchDeterminism.Sha256Text(seed + ":idd"),
            BatchDeterminism.Sha256Text(seed + ":expandobjects"));
    }

    private void UpdateMaximum(int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref _maximumActive);
            if (candidate <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _maximumActive, candidate, current) != current);
    }
}

internal sealed class InlineTestProgress<T> : IProgress<T>
{
    private readonly Action<T> _report;

    internal InlineTestProgress(Action<T> report)
    {
        _report = report;
    }

    public void Report(T value)
    {
        _report(value);
    }
}

internal sealed class TemporaryBatchDirectory : IDisposable
{
    internal TemporaryBatchDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GonieGonie",
            "Dragons",
            "temp",
            "batch-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
