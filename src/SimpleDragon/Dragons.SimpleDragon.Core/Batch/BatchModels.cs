using System.Collections.ObjectModel;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon.Batch;

public enum BatchCaseStatus
{
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>
/// One ordered model alternative and its deterministic per-case inputs.
/// </summary>
public sealed class BatchCaseDefinition
{
    public BatchCaseDefinition(
        GreenRetrofitModel model,
        string? caseId = null,
        string? weatherFilePath = null,
        IReadOnlyDictionary<string, string>? options = null)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        CaseId = NormalizeCaseId(caseId);
        WeatherFilePath = string.IsNullOrWhiteSpace(weatherFilePath)
            ? null
            : Path.GetFullPath(weatherFilePath!.Trim());
        Options = CopyOptions(options);
    }

    public GreenRetrofitModel Model { get; }

    /// <summary>
    /// Gets the caller-supplied stable ID, or null when the runner should derive one.
    /// </summary>
    public string? CaseId { get; }

    public string? WeatherFilePath { get; }

    public IReadOnlyDictionary<string, string> Options { get; }

    private static string? NormalizeCaseId(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length is < 1 or > 80)
        {
            throw new ArgumentException("A case ID must contain 1 to 80 characters.", nameof(value));
        }

        if (normalized.Any(character => !IsCaseIdCharacter(character)))
        {
            throw new ArgumentException(
                "A case ID may contain only letters, digits, period, underscore, and hyphen.",
                nameof(value));
        }

        return normalized;
    }

    private static bool IsCaseIdCharacter(char value)
    {
        return value is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '.' or '_' or '-';
    }

    private static ReadOnlyDictionary<string, string> CopyOptions(
        IReadOnlyDictionary<string, string>? options)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (options is not null)
        {
            foreach (KeyValuePair<string, string> item in options)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Value is null)
                {
                    throw new ArgumentException("Case options require non-empty keys and non-null values.", nameof(options));
                }

                result.Add(item.Key.Trim(), item.Value);
            }
        }

        return new ReadOnlyDictionary<string, string>(result);
    }
}

/// <summary>
/// File-system and scheduling controls for one batch invocation.
/// </summary>
public sealed class BatchRunOptions
{
    public int MaxDegreeOfParallelism { get; set; } = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));

    public bool UseCache { get; set; } = true;

    public bool WriteOutputs { get; set; } = true;

    public string OutputRootPath { get; set; } = Path.Combine(
        Path.GetTempPath(),
        "Dragons",
        "temp",
        "simpledragon-batch");

    public string? CacheRootPath { get; set; }
}

/// <summary>
/// A deterministic executor contract. Identity and option strings must change whenever execution semantics change.
/// </summary>
public interface IBatchCaseExecutor
{
    string ExecutorIdentity { get; }

    BatchRuntimeIdentity RuntimeIdentity { get; }

    string CanonicalExecutionOptions { get; }

    string CanonicalOutputOptions { get; }

    Task<BatchCaseExecution> ExecuteAsync(
        BatchCaseContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Immutable executor input for one prepared case.
/// </summary>
public sealed class BatchCaseContext
{
    internal BatchCaseContext(
        int index,
        string caseId,
        GreenRetrofitModel model,
        string? weatherFilePath,
        IReadOnlyDictionary<string, string> options,
        BatchDeterministicInput deterministicInput,
        string workRootPath)
    {
        Index = index;
        CaseId = caseId;
        Model = model;
        WeatherFilePath = weatherFilePath;
        Options = options;
        DeterministicInput = deterministicInput;
        WorkRootPath = workRootPath;
    }

    public int Index { get; }

    public string CaseId { get; }

    public GreenRetrofitModel Model { get; }

    public string? WeatherFilePath { get; }

    public IReadOnlyDictionary<string, string> Options { get; }

    public BatchDeterministicInput DeterministicInput { get; }

    public string WorkRootPath { get; }
}

/// <summary>
/// Executor-owned result payload. The runner catches thrown exceptions and preserves other cases.
/// </summary>
public sealed class BatchCaseExecution
{
    private BatchCaseExecution(
        bool succeeded,
        IReadOnlyDictionary<string, double> metrics,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Succeeded = succeeded;
        Metrics = CopyMetrics(metrics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        if (succeeded && Diagnostics.Any(item => item.IsFailure))
        {
            throw new ArgumentException("A successful execution cannot contain failure diagnostics.", nameof(diagnostics));
        }
    }

    public bool Succeeded { get; }

    public IReadOnlyDictionary<string, double> Metrics { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public static BatchCaseExecution Success(
        IReadOnlyDictionary<string, double>? metrics = null,
        IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        return new BatchCaseExecution(
            true,
            metrics ?? new Dictionary<string, double>(),
            diagnostics ?? Array.Empty<Diagnostic>());
    }

    public static BatchCaseExecution Failure(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, double>? partialMetrics = null)
    {
        DomainSupport.NotNull(diagnostics, nameof(diagnostics));

        if (!diagnostics.Any(item => item.IsFailure))
        {
            throw new ArgumentException("A failed execution requires a failure diagnostic.", nameof(diagnostics));
        }

        return new BatchCaseExecution(
            false,
            partialMetrics ?? new Dictionary<string, double>(),
            diagnostics);
    }

    private static ReadOnlyDictionary<string, double> CopyMetrics(
        IReadOnlyDictionary<string, double> metrics)
    {
        var copy = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, double> item in metrics)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                throw new ArgumentException("Metric names cannot be empty.", nameof(metrics));
            }

            if (double.IsNaN(item.Value) || double.IsInfinity(item.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(metrics), "Metric values must be finite.");
            }

            copy.Add(item.Key.Trim(), item.Value);
        }

        return new ReadOnlyDictionary<string, double>(copy);
    }
}

public sealed class BatchCaseResult
{
    internal BatchCaseResult(
        int index,
        string caseId,
        string cacheKey,
        string modelSha256,
        string? weatherFileSha256,
        BatchCaseStatus status,
        bool cacheHit,
        IReadOnlyDictionary<string, double> metrics,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Index = index;
        CaseId = caseId;
        CacheKey = cacheKey;
        ModelSha256 = modelSha256;
        WeatherFileSha256 = weatherFileSha256;
        Status = status;
        CacheHit = cacheHit;
        var sortedMetrics = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, double> metric in metrics)
        {
            sortedMetrics.Add(metric.Key, metric.Value);
        }

        Metrics = new ReadOnlyDictionary<string, double>(sortedMetrics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public int Index { get; }

    public string CaseId { get; }

    public string CacheKey { get; }

    public string ModelSha256 { get; }

    public string? WeatherFileSha256 { get; }

    public BatchCaseStatus Status { get; }

    public bool CacheHit { get; }

    public IReadOnlyDictionary<string, double> Metrics { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}

public sealed class BatchProgressSnapshot
{
    public BatchProgressSnapshot(
        long sequence,
        int total,
        int started,
        int completed,
        int succeeded,
        int failed,
        int cancelled,
        int cacheHits,
        int active,
        string? lastCaseId)
    {
        Sequence = sequence;
        Total = total;
        Started = started;
        Completed = completed;
        Succeeded = succeeded;
        Failed = failed;
        Cancelled = cancelled;
        CacheHits = cacheHits;
        Active = active;
        LastCaseId = lastCaseId;
    }

    public long Sequence { get; }

    public int Total { get; }

    public int Started { get; }

    public int Completed { get; }

    public int Succeeded { get; }

    public int Failed { get; }

    public int Cancelled { get; }

    public int CacheHits { get; }

    public int Active { get; }

    public string? LastCaseId { get; }
}

public sealed class BatchRunResult
{
    internal BatchRunResult(
        string runFingerprint,
        IReadOnlyList<BatchCaseResult> cases,
        IReadOnlyList<BatchProgressSnapshot> progressSnapshots,
        string combinedCsv,
        string reproducibilityManifest,
        string? outputDirectory,
        string? combinedCsvPath,
        string? manifestPath)
    {
        RunFingerprint = runFingerprint;
        Cases = Array.AsReadOnly(cases.ToArray());
        ProgressSnapshots = Array.AsReadOnly(progressSnapshots.ToArray());
        CombinedCsv = combinedCsv;
        ReproducibilityManifest = reproducibilityManifest;
        OutputDirectory = outputDirectory;
        CombinedCsvPath = combinedCsvPath;
        ManifestPath = manifestPath;
    }

    public string RunFingerprint { get; }

    public IReadOnlyList<BatchCaseResult> Cases { get; }

    public IReadOnlyList<BatchProgressSnapshot> ProgressSnapshots { get; }

    public string CombinedCsv { get; }

    public string ReproducibilityManifest { get; }

    public string? OutputDirectory { get; }

    public string? CombinedCsvPath { get; }

    public string? ManifestPath { get; }

    public int SuccessCount => Cases.Count(item => item.Status == BatchCaseStatus.Succeeded);

    public int FailureCount => Cases.Count(item => item.Status == BatchCaseStatus.Failed);

    public int CancelledCount => Cases.Count(item => item.Status == BatchCaseStatus.Cancelled);

    public bool CompletedWithoutFailures => FailureCount == 0 && CancelledCount == 0;
}
