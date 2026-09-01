using System.Diagnostics.CodeAnalysis;
using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.SimpleDragon.Batch;
using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;

namespace Dragons.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Runs a SimpleDragon research matrix while keeping all simulation setup paths module-owned.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; removal cancels work and completion disposes the token source.")]
public sealed class ManagedRunSimpleDragonBatchComponent : SimpleDragonComponent
{
    private const string ExampleActionVariable = "DRAGONS_EXAMPLE_ACTION";
    private const string GateStatusVariable = "DRAGONS_ENERGYPLUS_GATE_STATUS";
    private const string ExampleOutputVariable = "DRAGONS_EXAMPLES_OUTPUT";
    private const string RuntimeRootVariable = "DRAGONS_ENERGYPLUS_ROOT";
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
            "Typed batch-case tree. Branches are preserved in Case IDs and Statuses; execution identity and weather are resolved within SimpleDragon.",
            GH_ParamAccess.tree);
        pManager.AddIntegerParameter(
            "Parallel Limit",
            "N",
            "Maximum simultaneous EnergyPlus cases.",
            GH_ParamAccess.item,
            Math.Max(1, Math.Min(Environment.ProcessorCount, 4)));
        pManager.AddBooleanParameter(
            "Run",
            "R",
            "Connect a momentary Grasshopper Button and press it to explicitly start one batch.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Cancel",
            "C",
            "Connect a momentary Grasshopper Button and press it to cancel the active batch while preserving completed cases.",
            GH_ParamAccess.item,
            false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("State", "S", "Current managed batch state and progress.", GH_ParamAccess.item);
        pManager.AddTextParameter("Case IDs", "IDs", "Case identities in the original input paths.", GH_ParamAccess.tree);
        pManager.AddTextParameter("Statuses", "Status", "Case statuses in the original input paths.", GH_ParamAccess.tree);
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
        GH_Structure<SimpleDragonBatchCaseGoo>? caseTree = null;
        int parallelLimit = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
        bool run = false;
        bool cancel = false;
        if (!DA.GetDataTree(0, out caseTree)
            || !DA.GetData(1, ref parallelLimit)
            || !DA.GetData(2, ref run)
            || !DA.GetData(3, ref cancel))
        {
            return;
        }

        var caseGoos = new List<SimpleDragonBatchCaseGoo>();
        var casePaths = new List<GH_Path>();
        for (int branchIndex = 0; branchIndex < caseTree.PathCount; branchIndex++)
        {
            GH_Path path = caseTree.Paths[branchIndex];
            foreach (SimpleDragonBatchCaseGoo goo in caseTree.Branches[branchIndex])
            {
                caseGoos.Add(goo);
                casePaths.Add(path);
            }
        }

        ExplicitManagedBatchTriggerObservation triggers = _triggerGate.Observe(run, cancel);
        if (triggers.Cancel)
        {
            CancelActiveBatch();
        }

        if (triggers.Start && !triggers.Cancel)
        {
            ManagedBatchInputs? inputs = TryCreateInputs(caseGoos, casePaths, parallelLimit);
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
            var caseIds = new GH_Structure<GH_String>();
            var statuses = new GH_Structure<GH_String>();
            for (int index = 0; index < outcome.Result.Cases.Count; index++)
            {
                BatchCaseResult item = outcome.Result.Cases[index];
                GH_Path path = outcome.CasePaths[index];
                caseIds.Append(new GH_String(item.CaseId), path);
                statuses.Append(new GH_String(item.Status.ToString()), path);
            }

            DA.SetDataTree(1, caseIds);
            DA.SetDataTree(2, statuses);
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
        IReadOnlyList<GH_Path> casePaths,
        int parallelLimit)
    {
        if (casePaths.Count != caseGoos.Count)
        {
            throw new ArgumentException("Each managed batch case requires one Grasshopper path.", nameof(casePaths));
        }

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
            string tempDirectory = Path.GetTempPath();
            BatchCaseDefinition[] definitions = cases
                .Select(item => new BatchCaseDefinition(item.Model, item.CaseId))
                .ToArray();
            return new ManagedBatchInputs(
                definitions,
                casePaths.ToArray(),
                ManagedBatchPaths.Create(
                    tempDirectory,
                    CaptureAutomationRuntimeRoot(),
                    CaptureAutomationOutputRoot(tempDirectory)),
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
            return ManagedBatchOutcome.FromResult(result, weather.Diagnostics, inputs.CasePaths);
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

        if (!paths.CanBootstrapRuntime)
        {
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

    private static string? CaptureAutomationRuntimeRoot(Func<string, string?>? readVariable = null)
    {
        readVariable ??= Environment.GetEnvironmentVariable;
        string? action = OptionalEnvironmentValue(readVariable(ExampleActionVariable));
        string? gateStatus = OptionalEnvironmentValue(readVariable(GateStatusVariable));
        if ((!string.Equals(action, "Generate", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(action, "Validate", StringComparison.OrdinalIgnoreCase))
            || !string.Equals(gateStatus, "ready", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return OptionalEnvironmentValue(readVariable(RuntimeRootVariable));
    }

    private static string? CaptureAutomationOutputRoot(
        string tempDirectory,
        Func<string, string?>? readVariable = null)
    {
        readVariable ??= Environment.GetEnvironmentVariable;
        string? action = OptionalEnvironmentValue(readVariable(ExampleActionVariable));
        string? gateStatus = OptionalEnvironmentValue(readVariable(GateStatusVariable));
        if ((!string.Equals(action, "Generate", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(action, "Validate", StringComparison.OrdinalIgnoreCase))
            || !string.Equals(gateStatus, "ready", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? outputDirectory = OptionalEnvironmentValue(readVariable(ExampleOutputVariable));
        if (outputDirectory is null)
        {
            return null;
        }

        string fullOutputDirectory = Path.GetFullPath(outputDirectory);
        return IsSameOrDescendant(fullOutputDirectory, tempDirectory)
            ? Path.Combine(fullOutputDirectory, "b")
            : null;
    }

    private static bool IsSameOrDescendant(string rootPath, string candidatePath)
    {
        string root = Path.GetFullPath(rootPath);
        string candidate = Path.GetFullPath(candidatePath);
        string comparableRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string comparableCandidate = candidate.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (string.Equals(comparableRoot, comparableCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string? OptionalEnvironmentValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

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
            string outputRoot,
            bool canBootstrapRuntime)
        {
            Root = root;
            RuntimeRoot = runtimeRoot;
            WeatherCacheRoot = weatherCacheRoot;
            OutputRoot = outputRoot;
            CanBootstrapRuntime = canBootstrapRuntime;
        }

        internal string Root { get; }

        internal string RuntimeRoot { get; }

        internal string WeatherCacheRoot { get; }

        internal string OutputRoot { get; }

        internal bool CanBootstrapRuntime { get; }

        internal static ManagedBatchPaths Create(
            string tempDirectory,
            string? runtimeRootOverride,
            string? outputRootOverride)
        {
            if (string.IsNullOrWhiteSpace(tempDirectory))
            {
                throw new ArgumentException("The operating-system temp directory is unavailable.", nameof(tempDirectory));
            }

            string root = Path.GetFullPath(Path.Combine(
                tempDirectory.Trim(),
                "Dragons"));
            EnergyPlusRuntimeManifest runtime = EnergyPlusRuntimeManifest.Supported;
            string managedRuntimeRoot = Path.Combine(
                root,
                "runtime",
                "EnergyPlus",
                runtime.EnergyPlusVersion + "-" + runtime.EnergyPlusBuild);
            string runtimeRoot = string.IsNullOrWhiteSpace(runtimeRootOverride)
                ? managedRuntimeRoot
                : Path.GetFullPath(runtimeRootOverride!.Trim());
            string outputRoot = string.IsNullOrWhiteSpace(outputRootOverride)
                ? Path.Combine(root, "temp", "simpledragon-managed-batch")
                : Path.GetFullPath(outputRootOverride!.Trim());
            return new ManagedBatchPaths(
                root,
                runtimeRoot,
                Path.Combine(
                    root,
                    "weather",
                    "SimpleDragon",
                    SimpleDragonWeatherPackManifest.Supported.PackId),
                outputRoot,
                string.IsNullOrWhiteSpace(runtimeRootOverride));
        }
    }

    private sealed class ManagedBatchInputs
    {
        internal ManagedBatchInputs(
            IReadOnlyList<BatchCaseDefinition> cases,
            IReadOnlyList<GH_Path> casePaths,
            ManagedBatchPaths paths,
            int maxDegreeOfParallelism)
        {
            Cases = cases;
            CasePaths = casePaths;
            Paths = paths;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        internal IReadOnlyList<BatchCaseDefinition> Cases { get; }

        internal IReadOnlyList<GH_Path> CasePaths { get; }

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
            IReadOnlyList<Diagnostic> diagnostics,
            IReadOnlyList<GH_Path>? casePaths = null)
        {
            State = state;
            Result = result;
            Diagnostics = diagnostics;
            CasePaths = casePaths ?? Array.Empty<GH_Path>();
        }

        internal string State { get; }

        internal BatchRunResult? Result { get; }

        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal IReadOnlyList<GH_Path> CasePaths { get; }

        internal static ManagedBatchOutcome FromResult(
            BatchRunResult result,
            IReadOnlyList<Diagnostic> preparationDiagnostics,
            IReadOnlyList<GH_Path> casePaths)
        {
            if (casePaths.Count != result.Cases.Count)
            {
                throw new ArgumentException("Batch result paths must match the result case count.", nameof(casePaths));
            }

            Diagnostic[] diagnostics = preparationDiagnostics
                .Concat(result.Cases.SelectMany(item => item.Diagnostics).Select(PublicDiagnostic))
                .ToArray();
            string state = result.CompletedWithoutFailures
                ? "Succeeded"
                : result.CancelledCount > 0 && result.FailureCount == 0
                    ? "Cancelled"
                    : "Completed With Failures";
            return new ManagedBatchOutcome(state, result, diagnostics, casePaths);
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
