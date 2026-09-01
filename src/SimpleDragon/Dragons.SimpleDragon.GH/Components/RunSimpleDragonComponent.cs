using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Rhino;

namespace Dragons.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Runs one complete SimpleDragon model while keeping simulation preparation,
/// weather, EnergyPlus, and temporary paths behind the SimpleDragon boundary.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; removal cancels work and completion disposes the token source.")]
public sealed class RunSimpleDragonComponent : SimpleDragonComponent
{
    private static readonly char[] FailureCodeSeparators = { '_' };
    private static readonly Regex InvisibleDragonTermPattern = new(
        @"\bInvisibleDragon\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex IdfTermPattern = new(
        @"\bIDF\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex WindowsAbsolutePathPattern = new(
        @"(?<![A-Za-z0-9])[A-Za-z]:[\\/]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UncPathPattern = new(
        @"\\\\[^\\/\s]+[\\/][^\\/\s]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PosixAbsolutePathPattern = new(
        @"(?<![:A-Za-z0-9])/(?:tmp|var|home|users|private|opt|etc|mnt|run|srv|usr)(?:/|\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex FileUriPattern = new(
        @"\bfile:/+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private readonly object _sync = new();
    private readonly ExplicitRunTriggerGate _triggerGate = new();
    private readonly SimpleDragonSimulationExecutor _simulationExecutor = new();
    private CancellationTokenSource? _activeCancellation;
    private Task<RunOutcome>? _activeTask;
    private RunOutcome? _lastOutcome;
    private string? _lastRunKey;
    private string _state = "Idle";
    private bool _removed;
    private bool _rejectMultipleInputItems;
    private readonly SolutionScheduleGate _solutionScheduleGate = new();

    public RunSimpleDragonComponent()
        : base(
            "Run SimpleDragon",
            "SD Run",
            "Runs a complete GRM with address-selected packaged weather and module-managed EnergyPlus. Simulation preparation, weather, runtime, and work paths remain internal.",
            SimpleDragonPanels.Core)
    {
    }

    public override Guid ComponentGuid => new("6e242e51-77ce-4f77-8445-a17d636c7310");

    public override GH_Exposure Exposure => GH_Exposure.primary;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new GreenRetrofitModelParam(),
            "GRM",
            "GRM",
            "Complete SimpleDragon model. Its Address and Vintage select the packaged weather internally.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter(
            "Run",
            "Run",
            "Connect a momentary Grasshopper Button and press it to start one run; do not use a Toggle for this action.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Cancel",
            "Cancel",
            "Connect a momentary Grasshopper Button and press it to cancel the active run.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Force Rerun",
            "Force",
            "Ignore the last result for an identical GRM and timeout.",
            GH_ParamAccess.item,
            false);
        pManager.AddNumberParameter(
            "Timeout",
            "Min",
            "Positive EnergyPlus timeout in minutes.",
            GH_ParamAccess.item,
            30d);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new GreenRetrofitResultParam(),
            "GRR",
            "GRR",
            "Last complete SimpleDragon result.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "State",
            "State",
            "Idle, preparation/execution progress, Cached, or a terminal state.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter(
            "Success",
            "OK",
            "True when the last run produced a complete GRR.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new SimpleDragonDiagnosticParam(),
            "Diagnostics",
            "D",
            "SimpleDragon conversion, weather, runtime, simulation, and result diagnostics.",
            GH_ParamAccess.list);
    }

    protected override void BeforeSolveInstance()
    {
        base.BeforeSolveInstance();
        _rejectMultipleInputItems = Params.Input.Any(
            parameter => parameter.VolatileData.DataCount > 1);
        if (_rejectMultipleInputItems)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "Run SimpleDragon accepts one data-matched input set per component. "
                + "Use Batch Case and Managed Run SimpleDragon Batch for model lists or trees.");
        }
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        lock (_sync)
        {
            _removed = false;
            _triggerGate.Reset();
        }
    }

    public override void RemovedFromDocument(GH_Document document)
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            _removed = true;
            cancellation = _activeCancellation;
        }

        TryCancel(cancellation);
        base.RemovedFromDocument(document);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        bool run = false;
        bool cancel = false;
        bool force = false;
        double timeoutMinutes = 30d;
        DA.GetData(1, ref run);
        DA.GetData(2, ref cancel);
        ExplicitRunTriggerObservation triggers = _triggerGate.Observe(run, cancel);
        if (triggers.Cancel)
        {
            CancelActiveRun();
        }

        if (_rejectMultipleInputItems)
        {
            return;
        }

        GreenRetrofitModelGoo? modelGoo = null;
        if (!DA.GetData(0, ref modelGoo)
            || !DA.GetData(3, ref force)
            || !DA.GetData(4, ref timeoutMinutes))
        {
            return;
        }

        GreenRetrofitModel? model = modelGoo?.Value;
        if (model is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A complete SimpleDragon GRM is required.");
            return;
        }

        if (double.IsNaN(timeoutMinutes)
            || double.IsInfinity(timeoutMinutes)
            || timeoutMinutes <= 0d
            || timeoutMinutes > TimeSpan.MaxValue.TotalMinutes)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Timeout must be a finite positive number of minutes.");
            return;
        }

        var inputs = new RunInputs(
            model,
            TimeSpan.FromMinutes(timeoutMinutes),
            AutomationOverrides.Capture());
        string runKey = ComputeRunKey(inputs);
        if (triggers.Start && !triggers.Cancel)
        {
            StartRun(inputs, runKey, force);
        }

        RunOutcome? outcome;
        bool running;
        string state;
        string? outcomeRunKey;
        lock (_sync)
        {
            outcome = _lastOutcome;
            running = _activeTask is not null;
            state = _state;
            outcomeRunKey = _lastRunKey;
        }

        OutcomeVisibility visibility = ClassifyOutcome(
            running,
            outcome,
            outcomeRunKey,
            runKey);
        state = VisibleState(state, visibility);
        string? hiddenOutcomeWarning = HiddenOutcomeWarning(visibility);
        if (hiddenOutcomeWarning is not null)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                hiddenOutcomeWarning);
        }

        RunOutcome? visibleOutcome = visibility == OutcomeVisibility.Current
            ? outcome
            : null;
        if (visibleOutcome is not null)
        {
            Report(visibleOutcome.Diagnostics);
        }

        Message = state;
        if (visibleOutcome?.Result is not null)
        {
            DA.SetData(0, new GreenRetrofitResultGoo(visibleOutcome.Result));
        }

        DA.SetData(1, state);
        DA.SetData(2, visibleOutcome?.Success ?? false);
        DA.SetDataList(
            3,
            visibleOutcome?.Diagnostics.Select(item => new SimpleDragonDiagnosticGoo(item))
                ?? Enumerable.Empty<SimpleDragonDiagnosticGoo>());
    }

    private void StartRun(RunInputs inputs, string runKey, bool force)
    {
        lock (_sync)
        {
            if (_activeTask is not null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "A SimpleDragon run is already active; cancel it before starting another.");
                return;
            }

            if (CanReuseLastOutcome(force, _lastOutcome, _lastRunKey, runKey))
            {
                _state = "Cached";
                return;
            }

            _activeCancellation = new CancellationTokenSource();
            CancellationToken token = _activeCancellation.Token;
            _state = "Validating Model";
            Task<RunOutcome> task = Task.Run(() => ExecuteAsync(inputs, UpdateState, token), token);
            _activeTask = task;
            _ = task.ContinueWith(
                completed => CompleteRun(completed, runKey),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private void CancelActiveRun()
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            if (_activeCancellation is null || _activeTask is null)
            {
                cancellation = null;
            }
            else
            {
                _state = "Cancelling";
                cancellation = _activeCancellation;
            }
        }

        if (cancellation is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "There is no active SimpleDragon run to cancel.");
            return;
        }

        TryCancel(cancellation);
    }

    private void UpdateState(string state)
    {
        lock (_sync)
        {
            if (string.Equals(_state, state, StringComparison.Ordinal))
            {
                return;
            }

            _state = state;
        }

        ScheduleSolution();
    }

    private static void TryCancel(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CompleteRun(Task<RunOutcome> completed, string runKey)
    {
        RunOutcome outcome;
        if (completed.Status == TaskStatus.RanToCompletion)
        {
            outcome = completed.Result;
        }
        else if (completed.IsCanceled)
        {
            outcome = RunOutcome.Cancelled();
        }
        else
        {
            outcome = RunOutcome.InternalFailure(
                completed.Exception?.GetBaseException()
                    ?? new InvalidOperationException("The SimpleDragon task faulted without an exception."));
        }

        lock (_sync)
        {
            _activeTask = null;
            _activeCancellation?.Dispose();
            _activeCancellation = null;
            _lastOutcome = outcome;
            _lastRunKey = runKey;
            _state = outcome.State;
            if (_removed)
            {
                return;
            }
        }

        ScheduleSolution();
    }

    private void ScheduleSolution()
    {
        if (!_solutionScheduleGate.TryRequest())
        {
            return;
        }

        bool submitted = false;
        try
        {
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                bool handedOff = false;
                try
                {
                    lock (_sync)
                    {
                        if (_removed)
                        {
                            return;
                        }
                    }

                    GH_Document? document = OnPingDocument();
                    if (document is null)
                    {
                        return;
                    }

                    document.ScheduleSolution(5, _ =>
                    {
                        _solutionScheduleGate.Release();
                        lock (_sync)
                        {
                            if (_removed)
                            {
                                return;
                            }
                        }

                        ExpireSolution(false);
                    });
                    handedOff = true;
                }
                finally
                {
                    if (!handedOff)
                    {
                        _solutionScheduleGate.Release();
                    }
                }
            }));
            submitted = true;
        }
        finally
        {
            if (!submitted)
            {
                _solutionScheduleGate.Release();
            }
        }
    }

    private async Task<RunOutcome> ExecuteAsync(
        RunInputs inputs,
        Action<string> stateChanged,
        CancellationToken cancellationToken)
    {
        try
        {
            stateChanged("Validating Model");
            GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(inputs.Model);
            cancellationToken.ThrowIfCancellationRequested();
            var preparationDiagnostics = conversion.Diagnostics.ToList();
            if (!conversion.Success || conversion.Weather is null)
            {
                if (conversion.Weather is null && !preparationDiagnostics.Any(item => item.IsFailure))
                {
                    preparationDiagnostics.Add(new Diagnostic(
                        "SD.GH.RUN_WEATHER_REQUIRED",
                        DiagnosticSeverity.Error,
                        "The GRM Address and Vintage did not resolve to packaged weather."));
                }

                return RunOutcome.Failed(preparationDiagnostics);
            }

            cancellationToken.ThrowIfCancellationRequested();
            stateChanged("Preparing Weather");
            SimpleDragonWeatherFileResolution weather = ResolveWeather(
                conversion.Weather,
                inputs.Automation,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            preparationDiagnostics.AddRange(weather.Diagnostics);
            if (!weather.IsSuccess || weather.FilePath is null)
            {
                return RunOutcome.Failed(EnsureFailure(
                    preparationDiagnostics,
                    "SD.GH.RUN_WEATHER_NOT_READY",
                    "The address-selected packaged weather could not be prepared."));
            }

            cancellationToken.ThrowIfCancellationRequested();
            stateChanged("Resolving Runtime");
            RuntimePreparationResult runtimePreparation = await ResolveRuntimeAsync(
                    inputs.Automation,
                    stateChanged,
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!runtimePreparation.IsSuccess)
            {
                return RunOutcome.Failed(new[]
                {
                    CreateRuntimeFailureDiagnostic(
                        runtimePreparation.RequireFailure(),
                        inputs.Automation.HasRuntimeOverride),
                });
            }

            string runRoot = ResolveRunRoot();
            Directory.CreateDirectory(runRoot);
            var request = new SimpleDragonSimulationRequest(
                inputs.Model,
                runtimePreparation.RequireRuntime(),
                weather.FilePath,
                runRoot)
            {
                Timeout = inputs.Timeout,
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess,
            };
            var progress = new InlineProgress<SimpleDragonSimulationTransition>(
                transition => stateChanged(UserFacingState(transition.State)));
            SimpleDragonSimulationResult simulation = await _simulationExecutor
                .ExecuteAsync(request, progress, cancellationToken)
                .ConfigureAwait(false);
            Diagnostic[] diagnostics = preparationDiagnostics
                .Concat(simulation.Diagnostics)
                .Distinct()
                .ToArray();
            return RunOutcome.FromSimulation(simulation, diagnostics);
        }
        catch (OperationCanceledException)
        {
            return RunOutcome.Cancelled();
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is IOException
            || exception is InvalidOperationException
            || exception is UnauthorizedAccessException)
        {
            return RunOutcome.InternalFailure(exception);
        }
    }

    private static SimpleDragonWeatherFileResolution ResolveWeather(
        WeatherSelection selection,
        AutomationOverrides automation,
        CancellationToken cancellationToken)
    {
        if (automation.WeatherPath is null)
        {
            return new SimpleDragonWeatherPackResolver().Resolve(
                selection,
                cancellationToken: cancellationToken);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(automation.WeatherPath);
            if (!automation.MatchesWeatherFile(path, cancellationToken))
            {
                return InvalidAutomationWeather();
            }

            return new SimpleDragonWeatherFileResolution(
                path,
                null,
                false,
                Array.Empty<Diagnostic>());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is IOException
            || exception is NotSupportedException
            || exception is UnauthorizedAccessException)
        {
            return InvalidAutomationWeather();
        }
    }

    private static SimpleDragonWeatherFileResolution InvalidAutomationWeather() =>
        new(
            null,
            null,
            false,
            new[]
            {
                new Diagnostic(
                    "SD.GH.RUN_AUTOMATION_WEATHER_INVALID",
                    DiagnosticSeverity.Error,
                    "The internal automation weather override is not a readable EPW LOCATION document.",
                    suggestedAction: "Re-run the Rhino example gate with an existing EPW file."),
            });

    private static async Task<RuntimePreparationResult> ResolveRuntimeAsync(
        AutomationOverrides automation,
        Action<string> stateChanged,
        CancellationToken cancellationToken)
    {
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver().ResolveAsync(
            automation.CreateRuntimeOptions(),
            cancellationToken).ConfigureAwait(false);
        if (resolution.IsSuccess)
        {
            if (resolution.Runtime is not null
                && automation.MatchesIddPath(resolution.Runtime.IddPath))
            {
                return RuntimePreparationResult.Succeeded(resolution.Runtime);
            }

            return RuntimePreparationResult.Failed(new EnergyPlusFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "AUTOMATION_IDD_MISMATCH",
                "The verified example runtime IDD does not match the automation contract."));
        }

        if (resolution.Failure?.Category == EnergyPlusFailureCategory.Cancelled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (automation.HasRuntimeOverride)
        {
            return RuntimePreparationResult.Failed(
                resolution.Failure ?? MissingRuntimeFailure("RUNTIME_RESOLUTION_EMPTY"));
        }

        stateChanged("Preparing Runtime");
        var progress = new InlineProgress<EnergyPlusRuntimeBootstrapProgress>(
            update => stateChanged("Preparing Runtime: " + update.Stage));
        EnergyPlusRuntimeBootstrapResult bootstrap = await new EnergyPlusRuntimeBootstrapper()
            .EnsureInstalledAsync(progress: progress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!bootstrap.IsSuccess)
        {
            if (bootstrap.Failure?.Category == EnergyPlusFailureCategory.Cancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return RuntimePreparationResult.Failed(
                bootstrap.Failure ?? MissingRuntimeFailure("RUNTIME_BOOTSTRAP_EMPTY"));
        }

        return bootstrap.Runtime is not null
            ? RuntimePreparationResult.Succeeded(bootstrap.Runtime)
            : RuntimePreparationResult.Failed(MissingRuntimeFailure("RUNTIME_BOOTSTRAP_EMPTY"));
    }

    private static EnergyPlusFailure MissingRuntimeFailure(string code) =>
        new(
            EnergyPlusFailureCategory.Internal,
            code,
            "EnergyPlus runtime preparation ended without a verified runtime or a structured failure.");

    private static Diagnostic CreateRuntimeFailureDiagnostic(
        EnergyPlusFailure failure,
        bool automationOverride)
    {
        string scope = automationOverride ? "AUTOMATION_RUNTIME" : "RUNTIME";
        string category = failure.Category.ToString().ToUpperInvariant();
        string code = string.IsNullOrWhiteSpace(failure.Code)
            ? "UNKNOWN"
            : failure.Code.Trim().ToUpperInvariant();
        return new Diagnostic(
            "SD.GH.RUN." + scope + "." + category + "." + code,
            failure.Category == EnergyPlusFailureCategory.Cancelled
                ? DiagnosticSeverity.Info
                : DiagnosticSeverity.Error,
            string.IsNullOrWhiteSpace(failure.Message)
                ? "The module-managed EnergyPlus runtime could not be prepared."
                : failure.Message.Trim(),
            suggestedAction: RuntimeFailureAction(failure, automationOverride));
    }

    private static string? RuntimeFailureAction(
        EnergyPlusFailure failure,
        bool automationOverride)
    {
        if (failure.Category == EnergyPlusFailureCategory.Cancelled)
        {
            return null;
        }

        if (automationOverride)
        {
            return "Re-run the Rhino example gate after its verified EnergyPlus runtime is ready.";
        }

        if ((failure.Code ?? string.Empty)
            .Split(FailureCodeSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, "DOWNLOAD", StringComparison.OrdinalIgnoreCase)))
        {
            return "Check network access and LocalApplicationData write permission, then run dev install again.";
        }

        return failure.Category switch
        {
            EnergyPlusFailureCategory.RuntimeEnvironment or EnergyPlusFailureCategory.UserInput =>
                "Confirm LocalApplicationData is writable, then run dev install or reinstall SimpleDragon.",
            EnergyPlusFailureCategory.RuntimeNotFound or EnergyPlusFailureCategory.RuntimeIntegrity =>
                "Run dev install or reinstall SimpleDragon to restore the pinned module-managed EnergyPlus runtime.",
            _ => "Run dev install or reinstall SimpleDragon, then retry.",
        };
    }

    private static string ResolveRunRoot()
    {
        string temp = Path.GetTempPath();
        if (string.IsNullOrWhiteSpace(temp))
        {
            throw new InvalidOperationException("The operating-system temporary directory is unavailable.");
        }

        return Path.GetFullPath(Path.Combine(temp, "Dragons", "simpledragon-runs"));
    }

    private static bool CanReuseLastOutcome(
        bool force,
        RunOutcome? outcome,
        string? outcomeRunKey,
        string requestedRunKey)
    {
        return !force
            && outcome?.Success == true
            && outcome.Result is not null
            && string.Equals(outcomeRunKey, requestedRunKey, StringComparison.Ordinal);
    }

    private static OutcomeVisibility ClassifyOutcome(
        bool running,
        RunOutcome? outcome,
        string? outcomeRunKey,
        string requestedRunKey)
    {
        if (outcome is null)
        {
            return OutcomeVisibility.None;
        }

        if (running)
        {
            return OutcomeVisibility.HiddenWhileRunning;
        }

        return string.Equals(outcomeRunKey, requestedRunKey, StringComparison.Ordinal)
            ? OutcomeVisibility.Current
            : OutcomeVisibility.HiddenForDifferentInputs;
    }

    private static string? HiddenOutcomeWarning(OutcomeVisibility visibility) => visibility switch
    {
        OutcomeVisibility.HiddenWhileRunning =>
            "The previous GRR is hidden while a SimpleDragon run is active. Wait for the current run to finish.",
        OutcomeVisibility.HiddenForDifferentInputs =>
            "The previous GRR is hidden because it belongs to different inputs. Press the Run Button to evaluate the current GRM.",
        _ => null,
    };

    private static string VisibleState(string activeState, OutcomeVisibility visibility) =>
        visibility == OutcomeVisibility.HiddenForDifferentInputs
            ? "Inputs Changed"
            : activeState;

    private static string UserFacingState(SimpleDragonSimulationState state) => state switch
    {
        SimpleDragonSimulationState.Pending => "Preparing Simulation",
        SimpleDragonSimulationState.ConvertingModel => "Preparing Model",
        SimpleDragonSimulationState.CompilingIdf => "Preparing Simulation",
        SimpleDragonSimulationState.RunningEnergyPlus => "Running Simulation",
        SimpleDragonSimulationState.ParsingResults => "Processing Results",
        SimpleDragonSimulationState.BuildingResult => "Building Result",
        SimpleDragonSimulationState.Succeeded => "Succeeded",
        SimpleDragonSimulationState.Failed => "Failed",
        SimpleDragonSimulationState.Cancelled => "Cancelled",
        SimpleDragonSimulationState.TimedOut => "Timed Out",
        _ => "Preparing Simulation",
    };

    private static Diagnostic SanitizeDiagnostic(Diagnostic diagnostic)
    {
        string message = ReplaceImplementationTerms(diagnostic.Message);
        string? action = diagnostic.SuggestedAction is null
            ? null
            : ReplaceImplementationTerms(diagnostic.SuggestedAction);

        if (LooksLikeFileSystemPath(message))
        {
            message = SafeDiagnosticMessage(diagnostic.Code);
        }

        if (IsWeatherPreparationDiagnostic(diagnostic.Code))
        {
            action = WeatherPreparationAction(diagnostic);
        }
        else if (IsRuntimeExecutionDiagnostic(diagnostic.Code))
        {
            action = diagnostic.IsFailure
                ? "Review the SimpleDragon model and retry. If the problem persists, run dev install or reinstall SimpleDragon."
                : null;
        }
        else if (action is not null && LooksLikeFileSystemPath(action))
        {
            action = SafeDiagnosticAction(diagnostic.Code, diagnostic.IsFailure);
        }

        return new Diagnostic(
            diagnostic.Code,
            diagnostic.Severity,
            message,
            diagnostic.ObjectId,
            diagnostic.Geometry,
            action);
    }

    private static string ReplaceImplementationTerms(string value)
    {
        string withoutLayerName = InvisibleDragonTermPattern.Replace(value, "internal simulation");
        return IdfTermPattern.Replace(withoutLayerName, "simulation input");
    }

    private static bool LooksLikeFileSystemPath(string value) =>
        WindowsAbsolutePathPattern.IsMatch(value)
        || UncPathPattern.IsMatch(value)
        || PosixAbsolutePathPattern.IsMatch(value)
        || FileUriPattern.IsMatch(value);

    private static bool IsWeatherPreparationDiagnostic(string code) =>
        code.StartsWith("SD.WEATHER.", StringComparison.OrdinalIgnoreCase);

    private static bool IsRuntimeExecutionDiagnostic(string code) =>
        code.StartsWith("ENERGYPLUS.RUNTIME.", StringComparison.OrdinalIgnoreCase);

    private static string SafeDiagnosticMessage(string code)
    {
        if (IsWeatherPreparationDiagnostic(code))
        {
            return "The address-selected packaged weather could not be prepared.";
        }

        if (IsRuntimeExecutionDiagnostic(code)
            || code.StartsWith("SD.GH.RUN.RUNTIME.", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("SD.GH.RUN.AUTOMATION_RUNTIME.", StringComparison.OrdinalIgnoreCase))
        {
            return "The managed simulation runtime reported a failure during this run.";
        }

        if (code.StartsWith("SD.CONVERSION.", StringComparison.OrdinalIgnoreCase))
        {
            return "The GRM could not be prepared for simulation.";
        }

        if (code.StartsWith("SD.GRR.", StringComparison.OrdinalIgnoreCase))
        {
            return "The simulation outputs could not be converted into a complete GRR.";
        }

        return "The SimpleDragon run reported an internal preparation or execution error.";
    }

    private static string? WeatherPreparationAction(Diagnostic diagnostic)
    {
        if (!diagnostic.IsFailure)
        {
            return null;
        }

        if (diagnostic.Code.EndsWith("LOCK_TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            return "Wait for any other SimpleDragon weather preparation to finish, then retry.";
        }

        if (diagnostic.Code.EndsWith("EXTRACTION_FAILED", StringComparison.OrdinalIgnoreCase))
        {
            return "Confirm LocalApplicationData is writable, then run dev install or reinstall SimpleDragon.";
        }

        return "Run dev install or reinstall SimpleDragon to restore the packaged weather, then retry.";
    }

    private static string? SafeDiagnosticAction(string code, bool isFailure)
    {
        if (!isFailure)
        {
            return null;
        }

        if (code.StartsWith("SD.CONVERSION.", StringComparison.OrdinalIgnoreCase))
        {
            return "Review the reported GRM object or relationship, then retry.";
        }

        if (code.StartsWith("SD.GRR.", StringComparison.OrdinalIgnoreCase))
        {
            return "Review the SimpleDragon model and result diagnostics, then retry.";
        }

        return "Review the SimpleDragon diagnostics and retry.";
    }

    private static IReadOnlyList<Diagnostic> EnsureFailure(
        IReadOnlyList<Diagnostic> diagnostics,
        string code,
        string message)
    {
        return diagnostics.Any(item => item.IsFailure)
            ? diagnostics
            : diagnostics.Concat(new[]
            {
                new Diagnostic(code, DiagnosticSeverity.Error, message),
            }).ToArray();
    }

    private static string ComputeRunKey(RunInputs inputs)
    {
        EnergyPlusRuntimeManifest runtime = EnergyPlusRuntimeManifest.Supported;
        SimpleDragonWeatherPackManifest weather = SimpleDragonWeatherPackManifest.Supported;
        WeatherSelection? selectedWeather = inputs.Model.Weather;
        string source = string.Join(
            "\n",
            GrmWriter.Serialize(inputs.Model, indented: false),
            selectedWeather is null ? "weather:none" : "weather:explicit",
            selectedWeather?.EpwFileName ?? string.Empty,
            selectedWeather?.ClimateRegion ?? string.Empty,
            selectedWeather?.ClimateEffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                ?? string.Empty,
            runtime.EnergyPlusVersion,
            runtime.EnergyPlusBuild,
            runtime.EnergyPlusExecutableSha256,
            runtime.EnergyPlusIddSha256,
            runtime.ExpandObjectsSha256,
            SimpleDragonSimulationExecutor.ExecutionProfileIdentity,
            weather.PackId,
            weather.ArchiveSha256,
            inputs.Automation.CacheIdentity,
            inputs.Timeout.TotalSeconds.ToString("R", CultureInfo.InvariantCulture));
        byte[] bytes = Encoding.UTF8.GetBytes(source);
#if NET6_0_OR_GREATER
        byte[] hash = SHA256.HashData(bytes);
#else
        byte[] hash;
        using (SHA256 sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(bytes);
        }
#endif
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private sealed class RunInputs
    {
        internal RunInputs(GreenRetrofitModel model, TimeSpan timeout)
            : this(model, timeout, AutomationOverrides.None)
        {
        }

        internal RunInputs(
            GreenRetrofitModel model,
            TimeSpan timeout,
            AutomationOverrides automation)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            }

            Timeout = timeout;
            Automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        internal GreenRetrofitModel Model { get; }

        internal TimeSpan Timeout { get; }

        internal AutomationOverrides Automation { get; }
    }

    private enum OutcomeVisibility
    {
        None,
        Current,
        HiddenWhileRunning,
        HiddenForDifferentInputs,
    }

    private sealed class RuntimePreparationResult
    {
        private RuntimePreparationResult(
            EnergyPlusRuntimeLayout? runtime,
            EnergyPlusFailure? failure)
        {
            Runtime = runtime;
            Failure = failure;
        }

        internal EnergyPlusRuntimeLayout? Runtime { get; }

        internal EnergyPlusFailure? Failure { get; }

        internal bool IsSuccess => Runtime is not null && Failure is null;

        internal static RuntimePreparationResult Succeeded(EnergyPlusRuntimeLayout runtime) =>
            new(runtime ?? throw new ArgumentNullException(nameof(runtime)), null);

        internal static RuntimePreparationResult Failed(EnergyPlusFailure failure) =>
            new(null, failure ?? throw new ArgumentNullException(nameof(failure)));

        internal EnergyPlusRuntimeLayout RequireRuntime() => Runtime
            ?? throw new InvalidOperationException("A successful runtime preparation requires a runtime.");

        internal EnergyPlusFailure RequireFailure() => Failure
            ?? throw new InvalidOperationException("A failed runtime preparation requires a structured failure.");
    }

    private sealed class AutomationOverrides
    {
        private const string ExampleActionVariable = "DRAGONS_EXAMPLE_ACTION";
        private const string GateStatusVariable = "DRAGONS_ENERGYPLUS_GATE_STATUS";
        private const string RuntimeRootVariable = "DRAGONS_ENERGYPLUS_ROOT";
        private const string IddPathVariable = "DRAGONS_ENERGYPLUS_IDD";
        private const string WeatherPathVariable = "DRAGONS_ENERGYPLUS_WEATHER";

        internal AutomationOverrides(
            string? runtimeRoot,
            string? iddPath,
            string? weatherPath)
        {
            RuntimeRoot = OptionalValue(runtimeRoot);
            IddPath = OptionalValue(iddPath);
            WeatherPath = OptionalValue(weatherPath);
            WeatherContentSha256 = ComputeValidatedWeatherSha256(
                WeatherPath,
                CancellationToken.None);
        }

        internal static AutomationOverrides None { get; } = new(null, null, null);

        internal string? RuntimeRoot { get; }

        internal string? IddPath { get; }

        internal string? WeatherPath { get; }

        private string? WeatherContentSha256 { get; }

        internal bool HasRuntimeOverride => RuntimeRoot is not null || IddPath is not null;

        internal string CacheIdentity => string.Join(
            "\n",
            RuntimeRoot is null ? "runtime:managed" : "runtime:override:" + RuntimeRoot,
            IddPath is null ? "idd:managed" : "idd:override:" + IddPath,
            WeatherPath is null ? "weather:packaged" : "weather:override:" + WeatherPath,
            WeatherPath is null
                ? "weather-content:packaged"
                : "weather-sha256:" + (WeatherContentSha256 ?? "invalid"));

        internal static AutomationOverrides Capture(
            Func<string, string?>? readVariable = null)
        {
            readVariable ??= Environment.GetEnvironmentVariable;
            string? action = OptionalValue(readVariable(ExampleActionVariable));
            string? gateStatus = OptionalValue(readVariable(GateStatusVariable));
            if ((!string.Equals(action, "Generate", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(action, "Validate", StringComparison.OrdinalIgnoreCase))
                || !string.Equals(gateStatus, "ready", StringComparison.OrdinalIgnoreCase))
            {
                return None;
            }

            return new AutomationOverrides(
                readVariable(RuntimeRootVariable),
                readVariable(IddPathVariable),
                readVariable(WeatherPathVariable));
        }

        internal EnergyPlusRuntimeResolveOptions CreateRuntimeOptions()
        {
            string? effectiveRoot = RuntimeRoot;
            if (effectiveRoot is null && IddPath is not null)
            {
                effectiveRoot = Path.GetDirectoryName(Path.GetFullPath(IddPath))
                    ?? throw new InvalidOperationException("The automation IDD path has no parent directory.");
            }

            return new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = effectiveRoot,
                SearchEnvironmentVariables = false,
                SearchDefaultCacheLocation = !HasRuntimeOverride,
                SearchDefaultInstallLocation = false,
            };
        }

        internal bool MatchesIddPath(string resolvedIddPath)
        {
            if (IddPath is null)
            {
                return true;
            }

            return string.Equals(
                Path.GetFullPath(IddPath),
                Path.GetFullPath(resolvedIddPath),
                StringComparison.OrdinalIgnoreCase);
        }

        internal bool MatchesWeatherFile(
            string resolvedWeatherPath,
            CancellationToken cancellationToken)
        {
            if (WeatherPath is null || WeatherContentSha256 is null)
            {
                return false;
            }

            string fullPath = Path.GetFullPath(resolvedWeatherPath);
            if (!string.Equals(
                    Path.GetFullPath(WeatherPath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string? currentHash = ComputeValidatedWeatherSha256(fullPath, cancellationToken);
            return string.Equals(
                WeatherContentSha256,
                currentHash,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string? ComputeValidatedWeatherSha256(
            string? weatherPath,
            CancellationToken cancellationToken)
        {
            if (weatherPath is null)
            {
                return null;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.GetFullPath(weatherPath);
                if (!string.Equals(Path.GetExtension(path), ".epw", StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(path))
                {
                    return null;
                }

                using var content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using (var reader = new StreamReader(
                    content,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true))
                {
                    string? header = reader.ReadLine();
                    if (header is null
                        || !header.StartsWith("LOCATION,", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                content.Position = 0;
                using SHA256 sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(content);
                cancellationToken.ThrowIfCancellationRequested();
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is IOException
                || exception is NotSupportedException
                || exception is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string? OptionalValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
    }

    private sealed class ExplicitRunTriggerGate
    {
        private bool _observed;
        private bool _previousRun;
        private bool _previousCancel;

        internal ExplicitRunTriggerObservation Observe(bool run, bool cancel)
        {
            if (!_observed)
            {
                _observed = true;
                _previousRun = run;
                _previousCancel = cancel;
                return default;
            }

            var result = new ExplicitRunTriggerObservation(
                run && !_previousRun,
                cancel && !_previousCancel);
            _previousRun = run;
            _previousCancel = cancel;
            return result;
        }

        internal void Reset()
        {
            _observed = false;
            _previousRun = false;
            _previousCancel = false;
        }
    }

    private sealed class SolutionScheduleGate
    {
        private int _requested;

        internal bool TryRequest() => Interlocked.CompareExchange(ref _requested, 1, 0) == 0;

        internal void Release()
        {
            Interlocked.Exchange(ref _requested, 0);
        }
    }

    private readonly struct ExplicitRunTriggerObservation
    {
        internal ExplicitRunTriggerObservation(bool start, bool cancel)
        {
            Start = start;
            Cancel = cancel;
        }

        internal bool Start { get; }

        internal bool Cancel { get; }
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

    private sealed class RunOutcome
    {
        private RunOutcome(
            string state,
            bool success,
            GreenRetrofitResult? result,
            IEnumerable<Diagnostic> diagnostics)
        {
            State = state;
            Success = success;
            Result = result;
            Diagnostics = diagnostics.Select(SanitizeDiagnostic).Distinct().ToArray();
        }

        internal string State { get; }

        internal bool Success { get; }

        internal GreenRetrofitResult? Result { get; }

        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal static RunOutcome FromSimulation(
            SimpleDragonSimulationResult simulation,
            IReadOnlyList<Diagnostic> diagnostics)
        {
            return new RunOutcome(
                UserFacingState(simulation.State),
                simulation.IsSuccess,
                simulation.Result,
                diagnostics);
        }

        internal static RunOutcome Failed(IEnumerable<Diagnostic> diagnostics) =>
            new("Failed", false, null, diagnostics);

        internal static RunOutcome Cancelled() =>
            new(
                "Cancelled",
                false,
                null,
                new[]
                {
                    new Diagnostic(
                        "SD.GH.RUN_CANCELLED",
                        DiagnosticSeverity.Info,
                        "The SimpleDragon run was cancelled."),
                });

        internal static RunOutcome InternalFailure(Exception exception) =>
            new(
                "Failed",
                false,
                null,
                new[]
                {
                    new Diagnostic(
                        "SD.GH.RUN_INTERNAL_ERROR",
                        DiagnosticSeverity.Error,
                        "SimpleDragon execution failed internally (" + exception.GetType().Name + ")."),
                });
    }
}
