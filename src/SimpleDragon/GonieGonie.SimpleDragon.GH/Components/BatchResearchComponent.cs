using System.Diagnostics.CodeAnalysis;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.SimpleDragon.Batch;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Rhino;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Explicit-trigger asynchronous research matrix. Ordinary Grasshopper recomputes never launch work.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; RemovedFromDocument cancels and the task continuation disposes the token source.")]
public sealed class RunSimpleDragonBatchComponent : SimpleDragonComponent
{
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _activeCancellation;
    private Task<BatchOutcome>? _activeTask;
    private BatchOutcome? _lastOutcome;
    private BatchProgressSnapshot? _latestProgress;
    private bool _previousRunSignal;
    private bool _removed;
    private int _solutionScheduled;
    private string _state = "Idle";

    public RunSimpleDragonBatchComponent()
        : base(
            "Run SimpleDragon Batch",
            "Run Batch",
            "Runs an ordered GRM research matrix only on an explicit Run rising edge. Supports bounded parallelism, cache, progress, cancellation, partial failures, CSV, and a reproducibility manifest.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("c0af86b6-5f6e-478c-b069-a7892a31dadd");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new GreenRetrofitModelParam(),
            "Models",
            "M",
            "Ordered GRM alternatives.",
            GH_ParamAccess.list);
        pManager.AddTextParameter(
            "Case IDs",
            "IDs",
            "Optional stable case IDs. Supply none or exactly one per model.",
            GH_ParamAccess.list);
        pManager[1].Optional = true;
        pManager.AddTextParameter(
            "EPW Paths",
            "EPW",
            "Optional EPW paths. Relative paths use the saved Grasshopper document. Supply none, one shared path, or exactly one per model.",
            GH_ParamAccess.list);
        pManager[2].Optional = true;
        pManager.AddTextParameter(
            "Runtime Root",
            "E+",
            "Optional EnergyPlus 24.2 runtime root. Relative paths use the saved Grasshopper document.",
            GH_ParamAccess.item);
        pManager[3].Optional = true;
        pManager.AddTextParameter(
            "Output Root",
            "Out",
            "Optional batch root. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory. Empty uses the operating-system temp directory.",
            GH_ParamAccess.item);
        pManager[4].Optional = true;
        pManager.AddIntegerParameter(
            "Parallel Limit",
            "N",
            "Maximum simultaneous EnergyPlus cases.",
            GH_ParamAccess.item,
            Math.Max(1, Math.Min(Environment.ProcessorCount, 4)));
        pManager.AddBooleanParameter(
            "Run",
            "R",
            "Toggle false then true to explicitly start a new batch.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Cancel",
            "C",
            "Cancels the active batch while preserving completed cases.",
            GH_ParamAccess.item,
            false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("State", "S", "Current batch state and progress.", GH_ParamAccess.item);
        pManager.AddTextParameter("Case IDs", "IDs", "Cases in original input order.", GH_ParamAccess.list);
        pManager.AddTextParameter("Statuses", "Status", "Case statuses in original input order.", GH_ParamAccess.list);
        pManager.AddTextParameter("Combined CSV", "CSV", "Deterministic combined CSV path.", GH_ParamAccess.item);
        pManager.AddTextParameter("Manifest", "Manifest", "Deterministic reproducibility manifest path.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Complete", "OK", "True when every case succeeded.", GH_ParamAccess.item);
        pManager.AddParameter(
            new DiagnosticParam(),
            "Diagnostics",
            "D",
            "Per-case failures and warnings.",
            GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        var modelGoos = new List<GreenRetrofitModelGoo>();
        var caseIds = new List<string>();
        var weatherPaths = new List<string>();
        string runtimeRoot = string.Empty;
        string outputRoot = string.Empty;
        int parallelLimit = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
        bool run = false;
        bool cancel = false;
        if (!DA.GetDataList(0, modelGoos)
            || !DA.GetData(5, ref parallelLimit)
            || !DA.GetData(6, ref run)
            || !DA.GetData(7, ref cancel))
        {
            return;
        }

        DA.GetDataList(1, caseIds);
        DA.GetDataList(2, weatherPaths);
        DA.GetData(3, ref runtimeRoot);
        DA.GetData(4, ref outputRoot);

        if (cancel)
        {
            CancelActiveBatch();
        }

        bool start = run && !_previousRunSignal;
        _previousRunSignal = run;
        if (start)
        {
            BatchInputs? inputs = TryCreateInputs(
                modelGoos,
                caseIds,
                weatherPaths,
                runtimeRoot,
                outputRoot,
                parallelLimit);
            if (inputs is not null)
            {
                StartBatch(inputs);
            }
        }

        BatchOutcome? outcome;
        BatchProgressSnapshot? progress;
        string state;
        lock (_syncRoot)
        {
            outcome = _lastOutcome;
            progress = _latestProgress;
            state = _state;
        }

        if (progress is not null)
        {
            state += " (" + progress.Completed + "/" + progress.Total
                + ", active " + progress.Active + ")";
        }

        DA.SetData(0, state);
        if (outcome?.Result is not null)
        {
            DA.SetDataList(1, outcome.Result.Cases.Select(item => item.CaseId));
            DA.SetDataList(2, outcome.Result.Cases.Select(item => item.Status.ToString()));
            DA.SetData(3, outcome.Result.CombinedCsvPath);
            DA.SetData(4, outcome.Result.ManifestPath);
            DA.SetData(5, outcome.Result.CompletedWithoutFailures);
        }
        else
        {
            DA.SetData(5, false);
        }

        IReadOnlyList<Diagnostic> diagnostics = outcome?.Diagnostics ?? Array.Empty<Diagnostic>();
        Report(diagnostics);
        DA.SetDataList(6, diagnostics.Select(item => new DiagnosticGoo(item)));
    }

    public override void RemovedFromDocument(GH_Document document)
    {
        lock (_syncRoot)
        {
            _removed = true;
        }

        CancelActiveBatch();
        base.RemovedFromDocument(document);
    }

    private BatchInputs? TryCreateInputs(
        IReadOnlyList<GreenRetrofitModelGoo> modelGoos,
        IReadOnlyList<string> caseIds,
        IReadOnlyList<string> weatherPaths,
        string runtimeRoot,
        string outputRoot,
        int parallelLimit)
    {
        GreenRetrofitModel[] models = modelGoos
            .Where(item => item?.Value is not null)
            .Select(item => item.Value!)
            .ToArray();
        if (models.Length != modelGoos.Count || models.Length == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one valid GRM model is required.");
            return null;
        }

        if (caseIds.Count != 0 && caseIds.Count != models.Length)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Case IDs must be empty or match the model count.");
            return null;
        }

        if (weatherPaths.Count != 0 && weatherPaths.Count != 1 && weatherPaths.Count != models.Length)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "EPW paths must be empty, one shared path, or match the model count.");
            return null;
        }

        if (parallelLimit < 1)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Parallel Limit must be at least one.");
            return null;
        }

        GH_Document? document = OnPingDocument();
        string? documentFilePath = document is not null && document.IsFilePathDefined
            ? document.FilePath
            : null;
        string currentDirectory = Directory.GetCurrentDirectory();

        var definitions = new List<BatchCaseDefinition>(models.Length);
        for (int index = 0; index < models.Length; index++)
        {
            string? caseId = caseIds.Count == 0 ? null : caseIds[index];
            string? weatherPath = weatherPaths.Count switch
            {
                0 => null,
                1 => weatherPaths[0],
                _ => weatherPaths[index],
            };
            weatherPath = ResolveBatchReadPath(
                weatherPath,
                documentFilePath,
                currentDirectory);

            definitions.Add(new BatchCaseDefinition(models[index], caseId, weatherPath));
        }

        string effectiveOutputRoot = ResolveBatchOutputRoot(
            outputRoot,
            documentFilePath,
            Path.GetTempPath());
        string? effectiveRuntimeRoot = ResolveBatchReadPath(
            runtimeRoot,
            documentFilePath,
            currentDirectory);
        return new BatchInputs(definitions, effectiveRuntimeRoot, effectiveOutputRoot, parallelLimit);
    }

    private static string? ResolveBatchReadPath(
        string? suppliedPath,
        string? documentFilePath,
        string currentDirectory)
    {
        return string.IsNullOrWhiteSpace(suppliedPath)
            ? null
            : GrasshopperDocumentPathResolver.Resolve(
                suppliedPath!,
                documentFilePath,
                currentDirectory);
    }

    private static string ResolveBatchOutputRoot(
        string suppliedPath,
        string? documentFilePath,
        string tempDirectory)
    {
        return string.IsNullOrWhiteSpace(suppliedPath)
            ? new BatchRunOptions().OutputRootPath
            : GrasshopperDocumentPathResolver.Resolve(
                suppliedPath,
                documentFilePath,
                tempDirectory);
    }

    private void StartBatch(BatchInputs inputs)
    {
        lock (_syncRoot)
        {
            if (_activeTask is not null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "A batch is already running. Cancel it before starting another.");
                return;
            }

            _activeCancellation = new CancellationTokenSource();
            CancellationToken token = _activeCancellation.Token;
            _latestProgress = null;
            _state = "Resolving Runtime";
            Task<BatchOutcome> task = Task.Run(() => ExecuteAsync(inputs, token), token);
            _activeTask = task;
            _ = task.ContinueWith(
                CompleteBatch,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task<BatchOutcome> ExecuteAsync(BatchInputs inputs, CancellationToken cancellationToken)
    {
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver().ResolveAsync(
            new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = inputs.RuntimeRoot,
            },
            cancellationToken).ConfigureAwait(false);
        if (!resolution.IsSuccess)
        {
            EnergyPlusFailure failure = resolution.Failure!;
            if (failure.Category == EnergyPlusFailureCategory.Cancelled)
            {
                return BatchOutcome.Cancelled();
            }

            return BatchOutcome.Failed(new Diagnostic(
                "ENERGYPLUS.RUNTIME." + failure.Code,
                failure.Category == EnergyPlusFailureCategory.Cancelled
                    ? DiagnosticSeverity.Info
                    : DiagnosticSeverity.Error,
                failure.Message,
                suggestedAction: OptionalTrim(failure.Detail)));
        }

        lock (_syncRoot)
        {
            _state = "Running";
        }

        var options = new BatchRunOptions
        {
            MaxDegreeOfParallelism = inputs.MaxDegreeOfParallelism,
            OutputRootPath = inputs.OutputRoot,
            UseCache = true,
            WriteOutputs = true,
        };
        var progress = new InlineProgress<BatchProgressSnapshot>(ReportProgress);
        BatchCaseDefinition[] resolvedCases = ResolveDefaultWeatherPaths(
            inputs.Cases,
            resolution.Runtime!);
        BatchRunResult result = await BatchRunner.RunAsync(
            resolvedCases,
            new EnergyPlusBatchCaseExecutor(resolution.Runtime!),
            options,
            progress,
            cancellationToken).ConfigureAwait(false);
        return BatchOutcome.FromResult(result);
    }

    private static BatchCaseDefinition[] ResolveDefaultWeatherPaths(
        IReadOnlyList<BatchCaseDefinition> cases,
        EnergyPlusRuntimeLayout runtime)
    {
        var resolved = new BatchCaseDefinition[cases.Count];
        string weatherDirectory = Path.Combine(runtime.RootPath, "WeatherData");
        for (int index = 0; index < cases.Count; index++)
        {
            BatchCaseDefinition item = cases[index];
            string? weatherPath = item.WeatherFilePath;
            if (weatherPath is null && item.Model.Weather is not null)
            {
                string candidate = item.Model.Weather.ResolveEpwPath(weatherDirectory);
                if (File.Exists(candidate))
                {
                    weatherPath = candidate;
                }
            }

            resolved[index] = new BatchCaseDefinition(
                item.Model,
                item.CaseId,
                weatherPath,
                item.Options);
        }

        return resolved;
    }

    private static string? OptionalTrim(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private void ReportProgress(BatchProgressSnapshot progress)
    {
        lock (_syncRoot)
        {
            _latestProgress = progress;
        }

        ScheduleSolution();
    }

    private void CompleteBatch(Task<BatchOutcome> completed)
    {
        BatchOutcome outcome;
        if (completed.Status == TaskStatus.RanToCompletion)
        {
            outcome = completed.Result;
        }
        else if (completed.IsCanceled)
        {
            outcome = BatchOutcome.Cancelled();
        }
        else
        {
            Exception exception = completed.Exception?.GetBaseException()
                ?? new InvalidOperationException("The batch task faulted without an exception.");
            outcome = BatchOutcome.Failed(new Diagnostic(
                "SD.GH.BATCH_INTERNAL_ERROR",
                DiagnosticSeverity.Error,
                exception.GetType().Name + ": " + exception.Message));
        }

        lock (_syncRoot)
        {
            _activeTask = null;
            _activeCancellation?.Dispose();
            _activeCancellation = null;
            _lastOutcome = outcome;
            _state = outcome.State;
        }

        ScheduleSolution();
    }

    private void CancelActiveBatch()
    {
        CancellationTokenSource? cancellation;
        lock (_syncRoot)
        {
            cancellation = _activeCancellation;
            if (cancellation is not null)
            {
                _state = "Cancelling";
            }
        }

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A just-completed task may dispose its token between the state check and cancellation.
        }
    }

    private void ScheduleSolution()
    {
        if (Interlocked.Exchange(ref _solutionScheduled, 1) != 0)
        {
            return;
        }

        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            Interlocked.Exchange(ref _solutionScheduled, 0);
            lock (_syncRoot)
            {
                if (_removed)
                {
                    return;
                }
            }

            GH_Document? document = OnPingDocument();
            document?.ScheduleSolution(5, _ => ExpireSolution(false));
        }));
    }

    private sealed class BatchInputs
    {
        internal BatchInputs(
            IReadOnlyList<BatchCaseDefinition> cases,
            string? runtimeRoot,
            string outputRoot,
            int maxDegreeOfParallelism)
        {
            Cases = cases;
            RuntimeRoot = runtimeRoot;
            OutputRoot = outputRoot;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        internal IReadOnlyList<BatchCaseDefinition> Cases { get; }
        internal string? RuntimeRoot { get; }
        internal string OutputRoot { get; }
        internal int MaxDegreeOfParallelism { get; }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        internal InlineProgress(Action<T> report)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public void Report(T value)
        {
            _report(value);
        }
    }

    private sealed class BatchOutcome
    {
        private BatchOutcome(
            string state,
            BatchRunResult? result,
            IReadOnlyList<Diagnostic> diagnostics)
        {
            State = state;
            Result = result;
            Diagnostics = diagnostics;
        }

        internal string State { get; }

        internal BatchRunResult? Result { get; }

        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal static BatchOutcome FromResult(BatchRunResult result)
        {
            Diagnostic[] diagnostics = result.Cases.SelectMany(item => item.Diagnostics).ToArray();
            string state = result.CompletedWithoutFailures
                ? "Succeeded"
                : result.CancelledCount > 0 && result.FailureCount == 0
                    ? "Cancelled"
                    : "Completed With Failures";
            return new BatchOutcome(state, result, diagnostics);
        }

        internal static BatchOutcome Failed(Diagnostic diagnostic)
        {
            return new BatchOutcome("Failed", null, new[] { diagnostic });
        }

        internal static BatchOutcome Cancelled()
        {
            return Failed(new Diagnostic(
                "SD.GH.BATCH_CANCELLED",
                DiagnosticSeverity.Info,
                "The batch was cancelled."));
        }
    }
}
