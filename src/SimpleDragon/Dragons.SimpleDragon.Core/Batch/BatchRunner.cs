using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon.Batch;

/// <summary>
/// Executes an ordered model matrix with bounded concurrency and deterministic persistence.
/// </summary>
public static class BatchRunner
{
    public static async Task<BatchRunResult> RunAsync(
        IReadOnlyList<BatchCaseDefinition> cases,
        IBatchCaseExecutor executor,
        BatchRunOptions? options = null,
        IProgress<BatchProgressSnapshot>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DomainSupport.NotNull(cases, nameof(cases));
        DomainSupport.NotNull(executor, nameof(executor));

        options ??= new BatchRunOptions();
        RunConfiguration configuration = Validate(options, executor);
        PreparedCase[] prepared = Prepare(cases, executor);
        string runFingerprint = ComputeRunFingerprint(prepared, configuration.MaxDegreeOfParallelism);
        string outputDirectory = Path.Combine(configuration.OutputRootPath, "runs", runFingerprint);
        string workRoot = Path.Combine(outputDirectory, "work");
        string cacheRoot = configuration.CacheRootPath;

        var results = new BatchCaseResult?[prepared.Length];
        var tracker = new ProgressTracker(prepared.Length, progress);
        tracker.ReportInitial();
        int nextCase = -1;
        int workerCount = Math.Min(configuration.MaxDegreeOfParallelism, Math.Max(1, prepared.Length));
        var workers = new Task[workerCount];
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            workers[workerIndex] = RunWorkerAsync();
        }

        await Task.WhenAll(workers).ConfigureAwait(false);

        for (int index = 0; index < results.Length; index++)
        {
            if (results[index] is null)
            {
                results[index] = Cancelled(prepared[index]);
                tracker.Completed(results[index]!);
            }
        }

        BatchCaseResult[] ordered = results.Select(item => item!).ToArray();
        string combinedCsv = BatchOutputWriter.CreateCombinedCsv(ordered);
        string manifest = BatchOutputWriter.CreateManifest(
            runFingerprint,
            configuration.MaxDegreeOfParallelism,
            executor,
            ordered);
        string? writtenDirectory = null;
        string? csvPath = null;
        string? manifestPath = null;
        if (configuration.WriteOutputs)
        {
            Directory.CreateDirectory(outputDirectory);
            csvPath = Path.Combine(outputDirectory, "combined.csv");
            manifestPath = Path.Combine(outputDirectory, "reproducibility-manifest.json");
            AtomicFile.WriteAllText(
                csvPath,
                combinedCsv,
                emitUtf8Bom: true,
                cancellationToken: CancellationToken.None);
            AtomicFile.WriteAllText(
                manifestPath,
                manifest,
                cancellationToken: CancellationToken.None);
            writtenDirectory = outputDirectory;
        }

        return new BatchRunResult(
            runFingerprint,
            ordered,
            tracker.Snapshots,
            combinedCsv,
            manifest,
            writtenDirectory,
            csvPath,
            manifestPath);

        async Task RunWorkerAsync()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int index = Interlocked.Increment(ref nextCase);
                if (index >= prepared.Length)
                {
                    return;
                }

                PreparedCase item = prepared[index];
                tracker.Started(item.CaseId);
                BatchCaseResult result = await RunCaseAsync(
                    item,
                    executor,
                    configuration.UseCache,
                    cacheRoot,
                    workRoot,
                    cancellationToken).ConfigureAwait(false);
                results[index] = result;
                tracker.Completed(result);
            }
        }
    }

    private static async Task<BatchCaseResult> RunCaseAsync(
        PreparedCase item,
        IBatchCaseExecutor executor,
        bool useCache,
        string cacheRoot,
        string workRoot,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled(item);
        }

        string cachePath = Path.Combine(cacheRoot, item.DeterministicInput.CacheKey + ".json");
        if (useCache && BatchCache.TryRead(cachePath, item.DeterministicInput.CacheKey, out CachedExecution? cached))
        {
            return CreateResult(item, BatchCaseStatus.Succeeded, true, cached!.Metrics, cached.Diagnostics);
        }

        try
        {
            var context = new BatchCaseContext(
                item.Index,
                item.CaseId,
                item.Definition.Model,
                item.Definition.WeatherFilePath,
                item.Definition.Options,
                item.DeterministicInput,
                Path.Combine(workRoot, item.CaseId));
            BatchCaseExecution execution = await executor.ExecuteAsync(context, cancellationToken)
                .ConfigureAwait(false);
            if (execution is null)
            {
                throw new InvalidOperationException("The batch executor returned no result.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Cancelled(item);
            }

            if (!execution.Succeeded)
            {
                return CreateResult(
                    item,
                    BatchCaseStatus.Failed,
                    false,
                    execution.Metrics,
                    execution.Diagnostics);
            }

            IReadOnlyList<Diagnostic> diagnostics = execution.Diagnostics;
            if (useCache)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    BatchCache.Write(
                        cachePath,
                        item.DeterministicInput.CacheKey,
                        execution.Metrics,
                        execution.Diagnostics,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return Cancelled(item);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    diagnostics = execution.Diagnostics.Concat(new[]
                    {
                        new Diagnostic(
                            "SD.BATCH.CACHE_WRITE_FAILED",
                            DiagnosticSeverity.Warning,
                            "The completed case could not be written to the batch cache.",
                            suggestedAction: exception.Message),
                    }).ToArray();
                }
            }

            return CreateResult(
                item,
                BatchCaseStatus.Succeeded,
                false,
                execution.Metrics,
                diagnostics);
        }
        catch (OperationCanceledException)
        {
            return Cancelled(item);
        }
        catch (Exception exception)
        {
            return CreateResult(
                item,
                BatchCaseStatus.Failed,
                false,
                new Dictionary<string, double>(),
                new[]
                {
                    new Diagnostic(
                        "SD.BATCH.CASE_FAILED",
                        DiagnosticSeverity.Error,
                        exception.GetType().Name + ": " + exception.Message,
                        suggestedAction: "Inspect this case's deterministic input and executor diagnostics."),
                });
        }
    }

    private static PreparedCase[] Prepare(
        IReadOnlyList<BatchCaseDefinition> cases,
        IBatchCaseExecutor executor)
    {
        var prepared = new PreparedCase[cases.Count];
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < cases.Count; index++)
        {
            BatchCaseDefinition definition = cases[index]
                ?? throw new ArgumentException("A batch case cannot be null.", nameof(cases));
            string canonicalModel = BatchDeterminism.CanonicalizeModel(definition.Model);
            string canonicalCaseOptions = BatchDeterminism.CanonicalizeOptions(definition.Options);
            string? weatherHash = definition.WeatherFilePath is null
                ? null
                : BatchDeterminism.Sha256File(definition.WeatherFilePath);
            string seed = canonicalModel + "\n" + canonicalCaseOptions + "\n" + (weatherHash ?? "<no-weather>");
            string caseId = definition.CaseId ?? BatchDeterminism.CreateCaseId(index, seed);
            if (!identifiers.Add(caseId))
            {
                throw new ArgumentException("Duplicate batch case ID '" + caseId + "'.", nameof(cases));
            }

            var deterministicInput = new BatchDeterministicInput(
                caseId,
                canonicalModel,
                canonicalCaseOptions,
                executor.ExecutorIdentity,
                executor.CanonicalExecutionOptions,
                executor.CanonicalOutputOptions,
                PackageInfo.Version,
                Dragons.InvisibleDragon.PackageInfo.Version,
                PackageInfo.Compatibility.UpstreamRepository,
                PackageInfo.Compatibility.UpstreamCommit,
                PackageInfo.Compatibility.UpstreamVersion,
                executor.RuntimeIdentity,
                weatherHash);
            prepared[index] = new PreparedCase(
                index,
                caseId,
                definition,
                deterministicInput,
                BatchDeterminism.Sha256Text(canonicalModel),
                weatherHash);
        }

        return prepared;
    }

    private static RunConfiguration Validate(BatchRunOptions options, IBatchCaseExecutor executor)
    {
        if (options.MaxDegreeOfParallelism is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxDegreeOfParallelism must be between 1 and 1024.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputRootPath))
        {
            throw new ArgumentException("An output root is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(executor.ExecutorIdentity)
            || string.IsNullOrWhiteSpace(executor.CanonicalExecutionOptions)
            || string.IsNullOrWhiteSpace(executor.CanonicalOutputOptions)
            || executor.RuntimeIdentity is null)
        {
            throw new ArgumentException("The executor must expose complete deterministic identity data.", nameof(executor));
        }

        string outputRoot = Path.GetFullPath(options.OutputRootPath.Trim());
        string cacheRoot = string.IsNullOrWhiteSpace(options.CacheRootPath)
            ? Path.Combine(outputRoot, "cache")
            : Path.GetFullPath(options.CacheRootPath!.Trim());
        return new RunConfiguration(
            options.MaxDegreeOfParallelism,
            options.UseCache,
            options.WriteOutputs,
            outputRoot,
            cacheRoot);
    }

    private static string ComputeRunFingerprint(PreparedCase[] cases, int maxDegreeOfParallelism)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "dragons.simple-dragon.batch-run.v1");
            writer.WriteNumber("max_degree_of_parallelism", maxDegreeOfParallelism);
            writer.WriteStartArray("ordered_case_keys");
            foreach (PreparedCase item in cases)
            {
                writer.WriteStringValue(item.DeterministicInput.CacheKey);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return BatchDeterminism.Sha256Text(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static BatchCaseResult Cancelled(PreparedCase item)
    {
        return CreateResult(
            item,
            BatchCaseStatus.Cancelled,
            false,
            new Dictionary<string, double>(),
            new[]
            {
                new Diagnostic(
                    "SD.BATCH.CASE_CANCELLED",
                    DiagnosticSeverity.Info,
                    "The batch case was cancelled before a complete result was available."),
            });
    }

    private static BatchCaseResult CreateResult(
        PreparedCase item,
        BatchCaseStatus status,
        bool cacheHit,
        IReadOnlyDictionary<string, double> metrics,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        return new BatchCaseResult(
            item.Index,
            item.CaseId,
            item.DeterministicInput.CacheKey,
            item.ModelSha256,
            item.WeatherFileSha256,
            status,
            cacheHit,
            metrics,
            diagnostics);
    }

    private sealed class RunConfiguration
    {
        internal RunConfiguration(
            int maxDegreeOfParallelism,
            bool useCache,
            bool writeOutputs,
            string outputRootPath,
            string cacheRootPath)
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
            UseCache = useCache;
            WriteOutputs = writeOutputs;
            OutputRootPath = outputRootPath;
            CacheRootPath = cacheRootPath;
        }

        internal int MaxDegreeOfParallelism { get; }
        internal bool UseCache { get; }
        internal bool WriteOutputs { get; }
        internal string OutputRootPath { get; }
        internal string CacheRootPath { get; }
    }

    private sealed class PreparedCase
    {
        internal PreparedCase(
            int index,
            string caseId,
            BatchCaseDefinition definition,
            BatchDeterministicInput deterministicInput,
            string modelSha256,
            string? weatherFileSha256)
        {
            Index = index;
            CaseId = caseId;
            Definition = definition;
            DeterministicInput = deterministicInput;
            ModelSha256 = modelSha256;
            WeatherFileSha256 = weatherFileSha256;
        }

        internal int Index { get; }
        internal string CaseId { get; }
        internal BatchCaseDefinition Definition { get; }
        internal BatchDeterministicInput DeterministicInput { get; }
        internal string ModelSha256 { get; }
        internal string? WeatherFileSha256 { get; }
    }

    private sealed class ProgressTracker
    {
        private readonly object _syncRoot = new();
        private readonly int _total;
        private readonly IProgress<BatchProgressSnapshot>? _progress;
        private readonly List<BatchProgressSnapshot> _snapshots = new();
        private long _sequence;
        private int _started;
        private int _completed;
        private int _succeeded;
        private int _failed;
        private int _cancelled;
        private int _cacheHits;
        private int _active;

        internal ProgressTracker(int total, IProgress<BatchProgressSnapshot>? progress)
        {
            _total = total;
            _progress = progress;
        }

        internal IReadOnlyList<BatchProgressSnapshot> Snapshots
        {
            get
            {
                lock (_syncRoot)
                {
                    return Array.AsReadOnly(_snapshots.ToArray());
                }
            }
        }

        internal void ReportInitial()
        {
            Record(null);
        }

        internal void Started(string caseId)
        {
            lock (_syncRoot)
            {
                _started++;
                _active++;
                RecordLocked(caseId);
            }
        }

        internal void Completed(BatchCaseResult result)
        {
            lock (_syncRoot)
            {
                _completed++;
                if (_active > 0)
                {
                    _active--;
                }

                switch (result.Status)
                {
                    case BatchCaseStatus.Succeeded:
                        _succeeded++;
                        break;
                    case BatchCaseStatus.Failed:
                        _failed++;
                        break;
                    case BatchCaseStatus.Cancelled:
                        _cancelled++;
                        break;
                    default:
                        throw new InvalidOperationException("Unknown batch case status.");
                }

                if (result.CacheHit)
                {
                    _cacheHits++;
                }

                RecordLocked(result.CaseId);
            }
        }

        private void Record(string? caseId)
        {
            lock (_syncRoot)
            {
                RecordLocked(caseId);
            }
        }

        private void RecordLocked(string? caseId)
        {
            var snapshot = new BatchProgressSnapshot(
                _sequence++,
                _total,
                _started,
                _completed,
                _succeeded,
                _failed,
                _cancelled,
                _cacheHits,
                _active,
                caseId);
            _snapshots.Add(snapshot);
            try
            {
                _progress?.Report(snapshot);
            }
            catch (Exception)
            {
                // Observers are isolated from simulation control flow.
            }
        }
    }
}

internal sealed class CachedExecution
{
    internal CachedExecution(
        IReadOnlyDictionary<string, double> metrics,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Metrics = metrics;
        Diagnostics = diagnostics;
    }

    internal IReadOnlyDictionary<string, double> Metrics { get; }

    internal IReadOnlyList<Diagnostic> Diagnostics { get; }
}

internal static class BatchCache
{
    internal static bool TryRead(
        string path,
        string expectedCacheKey,
        out CachedExecution? execution)
    {
        execution = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (!string.Equals(
                    root.GetProperty("schema").GetString(),
                    "dragons.simple-dragon.batch-cache.v1",
                    StringComparison.Ordinal)
                || !string.Equals(
                    root.GetProperty("cache_key").GetString(),
                    expectedCacheKey,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var metrics = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.GetProperty("metrics").EnumerateObject())
            {
                metrics.Add(property.Name, property.Value.GetDouble());
            }

            var diagnostics = new List<Diagnostic>();
            foreach (JsonElement element in root.GetProperty("diagnostics").EnumerateArray())
            {
                diagnostics.Add(new Diagnostic(
                    element.GetProperty("code").GetString() ?? string.Empty,
                    (DiagnosticSeverity)element.GetProperty("severity").GetInt32(),
                    element.GetProperty("message").GetString() ?? string.Empty,
                    suggestedAction: element.GetProperty("suggested_action").ValueKind == JsonValueKind.Null
                        ? null
                        : element.GetProperty("suggested_action").GetString()));
            }

            BatchCaseExecution validated = BatchCaseExecution.Success(metrics, diagnostics);
            execution = new CachedExecution(
                validated.Metrics,
                validated.Diagnostics);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException
            or KeyNotFoundException
            or ArgumentException)
        {
            return false;
        }
    }

    internal static void Write(
        string path,
        string cacheKey,
        IReadOnlyDictionary<string, double> metrics,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
            }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "dragons.simple-dragon.batch-cache.v1");
            writer.WriteString("cache_key", cacheKey);
            writer.WriteStartObject("metrics");
            foreach (KeyValuePair<string, double> metric in metrics.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                CanonicalDouble.Write(writer, metric.Key, metric.Value);
            }

            writer.WriteEndObject();
            writer.WriteStartArray("diagnostics");
            foreach (Diagnostic diagnostic in diagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("code", diagnostic.Code);
                writer.WriteNumber("severity", (int)diagnostic.Severity);
                writer.WriteString("message", diagnostic.Message);
                if (diagnostic.SuggestedAction is null)
                {
                    writer.WriteNull("suggested_action");
                }
                else
                {
                    writer.WriteString("suggested_action", diagnostic.SuggestedAction);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        cancellationToken.ThrowIfCancellationRequested();
        AtomicFile.WriteAllText(
            path,
            Encoding.UTF8.GetString(stream.ToArray()),
            cancellationToken: cancellationToken);
    }
}

internal static class BatchOutputWriter
{
    internal static string CreateCombinedCsv(IReadOnlyList<BatchCaseResult> cases)
    {
        string[] metricNames = cases
            .SelectMany(item => item.Metrics.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var output = new StringBuilder();
        var headings = new List<string>(metricNames.Length + 4) { "index", "case_id", "status" };
        headings.AddRange(metricNames);
        headings.Add("diagnostic_codes");
        AppendCsvRow(output, headings);
        foreach (BatchCaseResult item in cases)
        {
            var values = new List<string>(metricNames.Length + 4)
            {
                item.Index.ToString(CultureInfo.InvariantCulture),
                item.CaseId,
                StatusName(item.Status),
            };
            foreach (string metricName in metricNames)
            {
                values.Add(item.Metrics.TryGetValue(metricName, out double value)
                    ? CanonicalDouble.Format(value)
                    : string.Empty);
            }

            values.Add(string.Join(
                "|",
                item.Diagnostics.Select(diagnostic => diagnostic.Code).OrderBy(code => code, StringComparer.Ordinal)));
            AppendCsvRow(output, values);
        }

        return output.ToString();
    }

    internal static string CreateManifest(
        string runFingerprint,
        int maxDegreeOfParallelism,
        IBatchCaseExecutor executor,
        IReadOnlyList<BatchCaseResult> cases)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
            }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "dragons.simple-dragon.batch-manifest.v1");
            writer.WriteString("run_fingerprint", runFingerprint);
            writer.WriteNumber("max_degree_of_parallelism", maxDegreeOfParallelism);
            writer.WriteString("executor_identity", executor.ExecutorIdentity);
            writer.WriteString("canonical_execution_options", executor.CanonicalExecutionOptions);
            writer.WriteString("canonical_output_options", executor.CanonicalOutputOptions);
            writer.WriteString("simple_dragon_core_version", PackageInfo.Version);
            writer.WriteString("invisible_dragon_core_version", Dragons.InvisibleDragon.PackageInfo.Version);
            writer.WriteString("upstream_repository", PackageInfo.Compatibility.UpstreamRepository);
            writer.WriteString("upstream_commit", PackageInfo.Compatibility.UpstreamCommit);
            writer.WriteString("upstream_version", PackageInfo.Compatibility.UpstreamVersion);
            writer.WriteStartObject("energyplus_runtime");
            writer.WriteString("version", executor.RuntimeIdentity.EnergyPlusVersion);
            writer.WriteString("build", executor.RuntimeIdentity.EnergyPlusBuild);
            writer.WriteString("executable_sha256", executor.RuntimeIdentity.EnergyPlusExecutableSha256);
            writer.WriteString("idd_sha256", executor.RuntimeIdentity.EnergyPlusIddSha256);
            writer.WriteString("expandobjects_sha256", executor.RuntimeIdentity.ExpandObjectsSha256);
            writer.WriteEndObject();
            writer.WriteStartArray("cases");
            foreach (BatchCaseResult item in cases)
            {
                writer.WriteStartObject();
                writer.WriteNumber("index", item.Index);
                writer.WriteString("case_id", item.CaseId);
                writer.WriteString("cache_key", item.CacheKey);
                writer.WriteString("model_sha256", item.ModelSha256);
                if (item.WeatherFileSha256 is null)
                {
                    writer.WriteNull("weather_file_sha256");
                }
                else
                {
                    writer.WriteString("weather_file_sha256", item.WeatherFileSha256);
                }

                writer.WriteString("status", StatusName(item.Status));
                writer.WriteStartObject("metrics");
                foreach (KeyValuePair<string, double> metric in item.Metrics)
                {
                    CanonicalDouble.Write(writer, metric.Key, metric.Value);
                }

                writer.WriteEndObject();
                writer.WriteStartArray("diagnostic_codes");
                foreach (string code in item.Diagnostics
                             .Select(diagnostic => diagnostic.Code)
                             .OrderBy(code => code, StringComparer.Ordinal))
                {
                    writer.WriteStringValue(code);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void AppendCsvRow(StringBuilder output, IEnumerable<string> fields)
    {
        bool first = true;
        foreach (string field in fields)
        {
            if (!first)
            {
                output.Append(',');
            }

            first = false;
            AppendCsvField(output, field);
        }

        output.Append('\n');
    }

    private static string StatusName(BatchCaseStatus status) =>
        SnakeCaseLowerNamingPolicy.Instance.ConvertName(status.ToString());

    private static void AppendCsvField(StringBuilder output, string value)
    {
        if (!RequiresCsvQuoting(value))
        {
            output.Append(value);
            return;
        }

        output.Append('"');
        output.Append(value.Replace("\"", "\"\""));
        output.Append('"');
    }

    private static bool RequiresCsvQuoting(string value)
    {
        foreach (char character in value)
        {
            if (character is ',' or '"' or '\r' or '\n')
            {
                return true;
            }
        }

        return false;
    }
}

internal static class AtomicFile
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

    internal static void WriteAllText(
        string path,
        string contents,
        bool emitUtf8Bom = false,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("The target must have a parent directory.", nameof(path));
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (var writer = new StreamWriter(
                stream,
                emitUtf8Bom ? Utf8WithBom : Utf8WithoutBom))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, null);
            }
            else
            {
                try
                {
                    File.Move(temporaryPath, fullPath);
                }
                catch (IOException) when (File.Exists(fullPath))
                {
                    // Another process atomically published the same deterministic cache entry.
                }
            }
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Best effort cleanup of a private temporary file.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort cleanup of a private temporary file.
            }
        }
    }
}
