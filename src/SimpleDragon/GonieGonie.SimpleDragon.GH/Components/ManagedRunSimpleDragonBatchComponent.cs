using System.Diagnostics.CodeAnalysis;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.SimpleDragon.Batch;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Rhino;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Runs a SimpleDragon research matrix while keeping all simulation setup paths module-owned.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; removal cancels work and completion disposes the token source.")]
public sealed class ManagedRunSimpleDragonBatchComponent : SimpleDragonComponent
{
    private readonly object _syncRoot = new();
    private readonly ExplicitManagedBatchTriggerGate _triggerGate = new();
    private CancellationTokenSource? _activeCancellation;
    private Task<ManagedBatchOutcome>? _activeTask;
    private ManagedBatchOutcome? _lastOutcome;
    private BatchProgressSnapshot? _latestProgress;
    private bool _removed;
    private int _solutionScheduled;
    private string _state = "Idle";

    public ManagedRunSimpleDragonBatchComponent()
        : base(
            "Managed Run SimpleDragon Batch",
            "Managed Batch",
            "Runs address-selected SimpleDragon models with module-managed EnergyPlus, weather, and temporary storage. No setup path input is required.",
            SimpleDragonPanels.Results)
    {
    }

    public override Guid ComponentGuid => new("e0a54494-3d69-4681-8756-cc3cd86df4e1");

    public override GH_Exposure Exposure => GH_Exposure.primary;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new SimpleDragonBatchCaseParam(),
            "Cases",
            "Cases",
            "Ordered typed batch cases. Each case owns its GRM; execution identity and weather are resolved within SimpleDragon.",
            GH_ParamAccess.list);
        pManager.AddIntegerParameter(
            "Parallel Limit",
            "N",
            "Maximum simultaneous EnergyPlus cases.",
            GH_ParamAccess.item,
            Math.Max(1, Math.Min(Environment.ProcessorCount, 4)));
        pManager.AddBooleanParameter(
            "Run",
            "R",
            "Toggle false then true to explicitly start a batch. A saved True value does not run when a document opens.",
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
        pManager.AddTextParameter("State", "S", "Current managed batch state and progress.", GH_ParamAccess.item);
        pManager.AddTextParameter("Case IDs", "IDs", "Cases in original input order.", GH_ParamAccess.list);
        pManager.AddTextParameter("Statuses", "Status", "Case statuses in original input order.", GH_ParamAccess.list);
        pManager.AddTextParameter("Combined CSV", "CSV", "Deterministic combined CSV result path.", GH_ParamAccess.item);
        pManager.AddTextParameter("Manifest", "Manifest", "Deterministic reproducibility manifest result path.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Complete", "OK", "True when every case succeeded.", GH_ParamAccess.item);
        pManager.AddParameter(
            new SimpleDragonDiagnosticParam(),
            "Diagnostics",
            "D",
            "Path-free preparation and per-case diagnostics.",
            GH_ParamAccess.list);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        var caseGoos = new List<SimpleDragonBatchCaseGoo>();
        int parallelLimit = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
        bool run = false;
        bool cancel = false;
        if (!DA.GetDataList(0, caseGoos)
            || !DA.GetData(1, ref parallelLimit)
            || !DA.GetData(2, ref run)
            || !DA.GetData(3, ref cancel))
        {
            return;
        }

        ExplicitManagedBatchTriggerObservation triggers = _triggerGate.Observe(run, cancel);
        if (triggers.Cancel)
        {
            CancelActiveBatch();
        }

        if (triggers.Start && !triggers.Cancel)
        {
            ManagedBatchInputs? inputs = TryCreateInputs(caseGoos, parallelLimit);
            if (inputs is not null)
            {
                StartBatch(inputs);
            }
        }

        ManagedBatchOutcome? outcome;
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

        Message = state;
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
        DA.SetDataList(6, diagnostics.Select(item => new SimpleDragonDiagnosticGoo(item)));
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        lock (_syncRoot)
        {
            _removed = false;
            _triggerGate.Reset();
        }
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

    private ManagedBatchInputs? TryCreateInputs(
        IReadOnlyList<SimpleDragonBatchCaseGoo> caseGoos,
        int parallelLimit)
    {
        SimpleDragonBatchCase[] cases = caseGoos
            .Where(item => item?.Value is not null)
            .Select(item => item.Value!)
            .ToArray();
        if (cases.Length != caseGoos.Count || cases.Length == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one valid SimpleDragon Batch Case is required.");
            return null;
        }

        if (parallelLimit is < 1 or > 1024)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Parallel Limit must be between 1 and 1024.");
            return null;
        }

        try
        {
            BatchCaseDefinition[] definitions = cases
                .Select(item => new BatchCaseDefinition(item.Model, item.CaseId))
                .ToArray();
            return new ManagedBatchInputs(
                definitions,
                ManagedBatchPaths.Create(Path.GetTempPath()),
                parallelLimit);
        }
        catch (ArgumentException exception)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "The managed batch inputs are invalid: " + exception.Message);
            return null;
        }
    }

    private void StartBatch(ManagedBatchInputs inputs)
    {
        lock (_syncRoot)
        {
            if (_activeTask is not null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "A managed batch is already running. Cancel it before starting another.");
                return;
            }

            _activeCancellation = new CancellationTokenSource();
            CancellationToken token = _activeCancellation.Token;
            _latestProgress = null;
            _state = "Resolving Runtime";
            Task<ManagedBatchOutcome> task = Task.Run(() => ExecuteAsync(inputs, token), token);
            _activeTask = task;
            _ = task.ContinueWith(
                CompleteBatch,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task<ManagedBatchOutcome> ExecuteAsync(
        ManagedBatchInputs inputs,
        CancellationToken cancellationToken)
    {
        try
        {
            EnergyPlusRuntimeLayout? runtime = await ResolveRuntimeAsync(inputs.Paths, cancellationToken)
                .ConfigureAwait(false);
            if (runtime is null)
            {
                return ManagedBatchOutcome.Failed(new Diagnostic(
                    "SD.GH.MANAGED_BATCH_RUNTIME_NOT_READY",
                    DiagnosticSeverity.Error,
                    "The module-managed EnergyPlus runtime could not be prepared.",
                    suggestedAction: "Retry after confirming network access or run dev.cmd setup."));
            }

            UpdateState("Preparing Weather");
            ManagedWeatherPreparation weather = PrepareWeather(
                inputs.Cases,
                inputs.Paths.WeatherCacheRoot,
                cancellationToken);
            if (weather.Cancelled)
            {
                return ManagedBatchOutcome.Cancelled();
            }

            if (!weather.Success)
            {
                return ManagedBatchOutcome.Failed(weather.Diagnostics);
            }

            BatchRunOptions options = CreateBatchRunOptions(inputs);
            var progress = new InlineProgress<BatchProgressSnapshot>(ReportProgress);
            UpdateState("Running");
            BatchRunResult result = await BatchRunner.RunAsync(
                weather.Cases,
                new EnergyPlusBatchCaseExecutor(runtime),
                options,
                progress,
                cancellationToken).ConfigureAwait(false);
            return ManagedBatchOutcome.FromResult(result, weather.Diagnostics);
        }
        catch (OperationCanceledException)
        {
            return ManagedBatchOutcome.Cancelled();
        }
        catch (Exception exception)
        {
            return ManagedBatchOutcome.InternalFailure(exception);
        }
    }

    private async Task<EnergyPlusRuntimeLayout?> ResolveRuntimeAsync(
        ManagedBatchPaths paths,
        CancellationToken cancellationToken)
    {
        EnergyPlusRuntimeResolveOptions resolveOptions = CreateRuntimeResolveOptions(paths);
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver().ResolveAsync(
            resolveOptions,
            cancellationToken).ConfigureAwait(false);
        if (resolution.IsSuccess)
        {
            return resolution.Runtime;
        }

        if (resolution.Failure?.Category == EnergyPlusFailureCategory.Cancelled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        UpdateState("Preparing Runtime");
        EnergyPlusRuntimeBootstrapOptions bootstrapOptions = CreateRuntimeBootstrapOptions(paths);
        var progress = new InlineProgress<EnergyPlusRuntimeBootstrapProgress>(
            update => UpdateState("Preparing Runtime: " + update.Stage));
        EnergyPlusRuntimeBootstrapResult bootstrap = await new EnergyPlusRuntimeBootstrapper()
            .EnsureInstalledAsync(bootstrapOptions, progress, cancellationToken)
            .ConfigureAwait(false);
        if (!bootstrap.IsSuccess)
        {
            if (bootstrap.Failure?.Category == EnergyPlusFailureCategory.Cancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return null;
        }

        return bootstrap.Runtime;
    }

    private static EnergyPlusRuntimeResolveOptions CreateRuntimeResolveOptions(ManagedBatchPaths paths) => new()
    {
        RuntimeRoot = paths.RuntimeRoot,
        SearchEnvironmentVariables = false,
        SearchDefaultCacheLocation = false,
        SearchDefaultInstallLocation = false,
    };

    private static EnergyPlusRuntimeBootstrapOptions CreateRuntimeBootstrapOptions(ManagedBatchPaths paths) => new()
    {
        TargetRoot = paths.RuntimeRoot,
        ReplaceInvalidExistingTarget = true,
    };

    private static BatchRunOptions CreateBatchRunOptions(ManagedBatchInputs inputs) => new()
    {
        MaxDegreeOfParallelism = inputs.MaxDegreeOfParallelism,
        OutputRootPath = inputs.Paths.OutputRoot,
        UseCache = true,
        WriteOutputs = true,
    };

    private static ManagedWeatherPreparation PrepareWeather(
        IReadOnlyList<BatchCaseDefinition> cases,
        string weatherCacheRoot,
        CancellationToken cancellationToken)
    {
        var selectionByFileName = new Dictionary<string, WeatherSelection>(StringComparer.Ordinal);
        var effectiveSelections = new WeatherSelection[cases.Count];
        var diagnostics = new List<Diagnostic>();
        for (int index = 0; index < cases.Count; index++)
        {
            BatchCaseDefinition item = cases[index];
            WeatherSelection? selection = item.Model.Weather;
            if (selection is null)
            {
                LookupResult<WeatherSelection> lookup = SimpleDragonDatabase.Default.Weather.FindByAddress(
                    item.Model.Address,
                    item.Model.Vintage);
                if (!lookup.Found || lookup.Value is null)
                {
                    return ManagedWeatherPreparation.Failed(lookup.Diagnostics.Count == 0
                        ? new Diagnostic(
                            "SD.GH.MANAGED_BATCH_WEATHER_REQUIRED",
                            DiagnosticSeverity.Error,
                            "A managed batch model has no resolvable address-selected weather identity.")
                        : PublicDiagnostic(lookup.Diagnostics[0]));
                }

                selection = lookup.Value;
                diagnostics.AddRange(lookup.Diagnostics
                    .Where(diagnostic => !diagnostic.IsFailure)
                    .Select(PublicDiagnostic));
            }

            effectiveSelections[index] = selection;
#if NET7_0_OR_GREATER
            selectionByFileName.TryAdd(selection.EpwFileName, selection);
#else
            if (!selectionByFileName.ContainsKey(selection.EpwFileName))
            {
                selectionByFileName.Add(selection.EpwFileName, selection);
            }
#endif
        }

        WeatherSelection[] selections = selectionByFileName.Values.ToArray();
        IReadOnlyList<SimpleDragonWeatherFileResolution> resolutions =
            new SimpleDragonWeatherPackResolver().ResolveMany(
                selections,
                CreateWeatherPackOptions(weatherCacheRoot),
                cancellationToken);
        if (cancellationToken.IsCancellationRequested
            || resolutions.Any(IsCancelledWeatherResolution))
        {
            return ManagedWeatherPreparation.CancelledOutcome();
        }

        var preparedByFileName = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < selections.Length; index++)
        {
            WeatherSelection selection = selections[index];
            SimpleDragonWeatherFileResolution resolution = resolutions[index];
            if (!resolution.IsSuccess
                || resolution.FilePath is null
                || !File.Exists(resolution.FilePath))
            {
                return ManagedWeatherPreparation.Failed(new Diagnostic(
                    "SD.GH.MANAGED_BATCH_WEATHER_NOT_READY",
                    DiagnosticSeverity.Error,
                    "The packaged weather artifact for '" + selection.EpwFileName + "' could not be prepared.",
                    suggestedAction: "Reinstall the SimpleDragon weather package with dev.cmd install."));
            }

            preparedByFileName.Add(selection.EpwFileName, resolution.FilePath);
            diagnostics.AddRange(resolution.Diagnostics
                .Where(item => !item.IsFailure)
                .Select(PublicDiagnostic));
        }

        var resolvedCases = new BatchCaseDefinition[cases.Count];
        for (int index = 0; index < cases.Count; index++)
        {
            BatchCaseDefinition item = cases[index];
            resolvedCases[index] = new BatchCaseDefinition(
                item.Model,
                item.CaseId,
                preparedByFileName[effectiveSelections[index].EpwFileName],
                item.Options);
        }

        return ManagedWeatherPreparation.Succeeded(resolvedCases, diagnostics);
    }

    private static SimpleDragonWeatherPackOptions CreateWeatherPackOptions(string weatherCacheRoot) => new()
    {
        CacheRoot = weatherCacheRoot,
    };

    private static bool IsCancelledWeatherResolution(SimpleDragonWeatherFileResolution resolution)
    {
        return resolution.Diagnostics.Any(diagnostic => string.Equals(
            diagnostic.Code,
            "SD.WEATHER.EXTRACTION_CANCELLED",
            StringComparison.Ordinal));
    }

    private static Diagnostic PublicDiagnostic(Diagnostic diagnostic)
    {
        return new Diagnostic(
            diagnostic.Code,
            diagnostic.Severity,
            diagnostic.Message,
            diagnostic.ObjectId,
            diagnostic.Geometry);
    }

    private void UpdateState(string state)
    {
        lock (_syncRoot)
        {
            _state = state;
        }

        ScheduleSolution();
    }

    private void ReportProgress(BatchProgressSnapshot progress)
    {
        lock (_syncRoot)
        {
            _latestProgress = progress;
        }

        ScheduleSolution();
    }

    private void CompleteBatch(Task<ManagedBatchOutcome> completed)
    {
        ManagedBatchOutcome outcome;
        if (completed.Status == TaskStatus.RanToCompletion)
        {
            outcome = completed.Result;
        }
        else if (completed.IsCanceled)
        {
            outcome = ManagedBatchOutcome.Cancelled();
        }
        else
        {
            Exception exception = completed.Exception?.GetBaseException()
                ?? new InvalidOperationException("The managed batch task faulted without an exception.");
            outcome = ManagedBatchOutcome.InternalFailure(exception);
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

    private sealed class ManagedBatchPaths
    {
        private ManagedBatchPaths(
            string root,
            string runtimeRoot,
            string weatherCacheRoot,
            string outputRoot)
        {
            Root = root;
            RuntimeRoot = runtimeRoot;
            WeatherCacheRoot = weatherCacheRoot;
            OutputRoot = outputRoot;
        }

        internal string Root { get; }

        internal string RuntimeRoot { get; }

        internal string WeatherCacheRoot { get; }

        internal string OutputRoot { get; }

        internal static ManagedBatchPaths Create(string tempDirectory)
        {
            if (string.IsNullOrWhiteSpace(tempDirectory))
            {
                throw new ArgumentException("The operating-system temp directory is unavailable.", nameof(tempDirectory));
            }

            string root = Path.GetFullPath(Path.Combine(
                tempDirectory.Trim(),
                "GonieGonie",
                "Dragons"));
            EnergyPlusRuntimeManifest runtime = EnergyPlusRuntimeManifest.Supported;
            return new ManagedBatchPaths(
                root,
                Path.Combine(
                    root,
                    "runtime",
                    "EnergyPlus",
                    runtime.EnergyPlusVersion + "-" + runtime.EnergyPlusBuild),
                Path.Combine(
                    root,
                    "weather",
                    "SimpleDragon",
                    SimpleDragonWeatherPackManifest.Supported.PackId),
                Path.Combine(root, "temp", "simpledragon-managed-batch"));
        }
    }

    private sealed class ManagedBatchInputs
    {
        internal ManagedBatchInputs(
            IReadOnlyList<BatchCaseDefinition> cases,
            ManagedBatchPaths paths,
            int maxDegreeOfParallelism)
        {
            Cases = cases;
            Paths = paths;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        internal IReadOnlyList<BatchCaseDefinition> Cases { get; }

        internal ManagedBatchPaths Paths { get; }

        internal int MaxDegreeOfParallelism { get; }
    }

    private sealed class ManagedWeatherPreparation
    {
        private ManagedWeatherPreparation(
            IReadOnlyList<BatchCaseDefinition> cases,
            IReadOnlyList<Diagnostic> diagnostics,
            bool success,
            bool cancelled)
        {
            Cases = cases;
            Diagnostics = diagnostics;
            Success = success;
            Cancelled = cancelled;
        }

        internal IReadOnlyList<BatchCaseDefinition> Cases { get; }

        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal bool Success { get; }

        internal bool Cancelled { get; }

        internal static ManagedWeatherPreparation Succeeded(
            IReadOnlyList<BatchCaseDefinition> cases,
            IReadOnlyList<Diagnostic> diagnostics) =>
            new(cases, diagnostics, success: true, cancelled: false);

        internal static ManagedWeatherPreparation Failed(params Diagnostic[] diagnostics) =>
            new(Array.Empty<BatchCaseDefinition>(), diagnostics, success: false, cancelled: false);

        internal static ManagedWeatherPreparation Failed(IReadOnlyList<Diagnostic> diagnostics) =>
            new(Array.Empty<BatchCaseDefinition>(), diagnostics, success: false, cancelled: false);

        internal static ManagedWeatherPreparation CancelledOutcome() =>
            new(
                Array.Empty<BatchCaseDefinition>(),
                Array.Empty<Diagnostic>(),
                success: false,
                cancelled: true);
    }

    private sealed class ExplicitManagedBatchTriggerGate
    {
        private bool _observed;
        private bool _previousStart;
        private bool _previousCancel;

        internal ExplicitManagedBatchTriggerObservation Observe(bool start, bool cancel)
        {
            if (!_observed)
            {
                _observed = true;
                _previousStart = start;
                _previousCancel = cancel;
                return default;
            }

            var observation = new ExplicitManagedBatchTriggerObservation(
                start && !_previousStart,
                cancel && !_previousCancel);
            _previousStart = start;
            _previousCancel = cancel;
            return observation;
        }

        internal void Reset()
        {
            _observed = false;
            _previousStart = false;
            _previousCancel = false;
        }
    }

    private readonly struct ExplicitManagedBatchTriggerObservation
    {
        internal ExplicitManagedBatchTriggerObservation(bool start, bool cancel)
        {
            Start = start;
            Cancel = cancel;
        }

        internal bool Start { get; }

        internal bool Cancel { get; }
    }

    private sealed class ManagedBatchOutcome
    {
        private ManagedBatchOutcome(
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

        internal static ManagedBatchOutcome FromResult(
            BatchRunResult result,
            IReadOnlyList<Diagnostic> preparationDiagnostics)
        {
            Diagnostic[] diagnostics = preparationDiagnostics
                .Concat(result.Cases.SelectMany(item => item.Diagnostics).Select(PublicDiagnostic))
                .ToArray();
            string state = result.CompletedWithoutFailures
                ? "Succeeded"
                : result.CancelledCount > 0 && result.FailureCount == 0
                    ? "Cancelled"
                    : "Completed With Failures";
            return new ManagedBatchOutcome(state, result, diagnostics);
        }

        internal static ManagedBatchOutcome Failed(params Diagnostic[] diagnostics) =>
            new("Failed", null, diagnostics);

        internal static ManagedBatchOutcome Failed(IReadOnlyList<Diagnostic> diagnostics) =>
            new("Failed", null, diagnostics);

        internal static ManagedBatchOutcome Cancelled() =>
            new(
                "Cancelled",
                null,
                new[]
                {
                    new Diagnostic(
                        "SD.GH.MANAGED_BATCH_CANCELLED",
                        DiagnosticSeverity.Info,
                        "The managed batch was cancelled."),
                });

        internal static ManagedBatchOutcome InternalFailure(Exception exception) =>
            new(
                "Failed",
                null,
                new[]
                {
                    new Diagnostic(
                        "SD.GH.MANAGED_BATCH_INTERNAL_ERROR",
                        DiagnosticSeverity.Error,
                        "Managed batch execution failed internally (" + exception.GetType().Name + ")."),
                });
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
}
