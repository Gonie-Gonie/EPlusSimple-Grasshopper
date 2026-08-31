using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Results;
using Grasshopper.Kernel;
using Rhino;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

/// <summary>
/// Canonical path-free EnergyPlus runner. InvisibleDragon consumes a verified EPW handle
/// created by its dedicated standalone weather-verification component.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; RemovedFromDocument cancels and the completion path disposes the source.")]
public sealed class ManagedRunEnergyPlusComponent : DragonComponent
{
    private readonly object _sync = new();
    private readonly ExplicitTriggerGate _triggerGate = new();
    private CancellationTokenSource? _activeCancellation;
    private Task<RunOutcome>? _activeTask;
    private RunOutcome? _lastOutcome;
    private string? _lastRunKey;
    private string _state = "Idle";
    private bool _removed;
    private bool _rejectMultipleInputItems;

    public ManagedRunEnergyPlusComponent()
        : base(
            "Run InvisibleDragon",
            "Run",
            "Runs EnergyPlus off the Rhino UI thread. EnergyPlus, IDD, and temporary paths are managed internally; connect an EPW handle verified by ID Weather.",
            DragonPanels.Core)
    {
    }

    public override Guid ComponentGuid => new("50e4f5bf-f174-458f-bfaa-aaf4e25ce5b5");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(
            new DragonIdfParam(),
            "IDF",
            "IDF",
            "Compiled EnergyPlus IDF document.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new PreparedWeatherFileParam(),
            "Weather",
            "EPW",
            "Verified EPW handle from ID Weather.",
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
            "Ignore the last result for identical IDF, weather, and timeout inputs.",
            GH_ParamAccess.item,
            false);
        pManager.AddNumberParameter(
            "Timeout",
            "Min",
            "Positive timeout in minutes.",
            GH_ParamAccess.item,
            30d);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(
            new EnergyPlusResultParam(),
            "Result",
            "R",
            "Last structured EnergyPlus result.",
            GH_ParamAccess.item);
        pManager.AddTextParameter(
            "State",
            "S",
            "Idle, active EnergyPlus state, Cached, or terminal state.",
            GH_ParamAccess.item);
        pManager.AddBooleanParameter(
            "Success",
            "OK",
            "True when the last run succeeded.",
            GH_ParamAccess.item);
        pManager.AddParameter(
            new DiagnosticParam(),
            "Diagnostics",
            "D",
            "Runtime and EnergyPlus diagnostics.",
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
                "Run InvisibleDragon accepts one data-matched input set per component. "
                + "Use one Run InvisibleDragon component per simulation for model lists or trees.");
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
        DragonIdfGoo? idfGoo = null;
        PreparedWeatherFileGoo? weatherGoo = null;
        bool run = false;
        bool cancel = false;
        bool force = false;
        double timeoutMinutes = 30d;
        DA.GetData(2, ref run);
        DA.GetData(3, ref cancel);
        ExplicitTriggerObservation triggers = _triggerGate.Observe(run, cancel);
        if (triggers.Cancel)
        {
            CancelActiveRun();
        }

        if (_rejectMultipleInputItems)
        {
            return;
        }

        if (!DA.GetData(0, ref idfGoo)
            || !DA.GetData(1, ref weatherGoo)
            || !DA.GetData(4, ref force)
            || !DA.GetData(5, ref timeoutMinutes))
        {
            return;
        }

        if (idfGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "IDF is required.");
            return;
        }

        PreparedWeatherFile? weather = weatherGoo?.Value;
        if (weather is null)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "Weather is required. Connect the Weather output from ID Weather.");
            return;
        }

        if (!weather.IsBound)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "The restored Weather metadata is not bound to a local EPW artifact. Recompute ID Weather and reconnect its Weather output.");
            return;
        }

        if (!weather.VerifyArtifact()
            || !weather.TryGetArtifactPath(out string? weatherArtifactPath))
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "The EPW artifact is missing or no longer matches its verified SHA-256. Recompute ID Weather before running InvisibleDragon again.");
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
            IdfWriter.Write(idfGoo.Value),
            weatherArtifactPath!,
            weather.Sha256,
            TimeSpan.FromMinutes(timeoutMinutes));
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

        if (!running && outcome is not null && !string.Equals(outcomeRunKey, runKey, StringComparison.Ordinal))
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "The displayed result belongs to previous inputs. Press the Run Button to evaluate the current simulation.");
        }

        if (outcome is not null)
        {
            Report(outcome.Diagnostics);
        }

        Message = state;
        DA.SetData(0, outcome?.Result is null ? null : new EnergyPlusResultGoo(outcome.Result));
        DA.SetData(1, state);
        DA.SetData(2, outcome?.Success ?? false);
        DA.SetDataList(
            3,
            outcome?.Diagnostics.Select(item => new DiagnosticGoo(item))
                ?? Enumerable.Empty<DiagnosticGoo>());
    }

    private void StartRun(RunInputs inputs, string runKey, bool force)
    {
        lock (_sync)
        {
            if (_activeTask is not null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "An EnergyPlus run is already active; cancel it before starting another.");
                return;
            }

            if (!force
                && _lastOutcome is not null
                && string.Equals(_lastRunKey, runKey, StringComparison.Ordinal))
            {
                _state = "Cached";
                return;
            }

            _activeCancellation = new CancellationTokenSource();
            CancellationToken token = _activeCancellation.Token;
            _state = "Validating";
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
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "There is no active EnergyPlus run to cancel.");
            return;
        }

        TryCancel(cancellation);
    }

    private void UpdateState(string state)
    {
        lock (_sync)
        {
            _state = state;
        }
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
                    ?? new InvalidOperationException("The EnergyPlus task faulted without an exception."));
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

        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            lock (_sync)
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

    private static async Task<RunOutcome> ExecuteAsync(
        RunInputs inputs,
        Action<string> stateChanged,
        CancellationToken cancellationToken)
    {
        string? stagedIdf = null;
        try
        {
            stateChanged("Resolving Runtime");
            EnergyPlusRuntimeResolution resolution = await new RuntimeResolver().ResolveAsync(
                new EnergyPlusRuntimeResolveOptions(),
                cancellationToken).ConfigureAwait(false);
            EnergyPlusRuntimeLayout? runtime = resolution.Runtime;
            if (runtime is null)
            {
                stateChanged("Preparing Runtime");
                var progress = new InlineProgress<EnergyPlusRuntimeBootstrapProgress>(
                    update => stateChanged("Preparing: " + update.Stage));
                EnergyPlusRuntimeBootstrapResult bootstrap = await new EnergyPlusRuntimeBootstrapper()
                    .EnsureInstalledAsync(progress: progress, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!bootstrap.IsSuccess)
                {
                    return RunOutcome.FromFailure(bootstrap.Failure!);
                }

                runtime = bootstrap.Runtime;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "GonieGonie",
                "Dragons",
                "energyplus-runs");
            Directory.CreateDirectory(tempRoot);
            stagedIdf = Path.Combine(tempRoot, "managed-input-" + Guid.NewGuid().ToString("N") + ".idf");
            File.WriteAllText(stagedIdf, inputs.IdfText, new UTF8Encoding(false));
            var request = new EnergyPlusRunRequest(
                runtime!,
                stagedIdf,
                inputs.WeatherPath,
                tempRoot)
            {
                Timeout = inputs.Timeout,
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess,
            };
            var runProgress = new InlineProgress<EnergyPlusRunTransition>(
                transition => stateChanged(transition.State.ToString()));
            EnergyPlusRunResult runtimeResult = await new EnergyPlusRunner()
                .RunAsync(request, runProgress, cancellationToken)
                .ConfigureAwait(false);
            EnergyPlusSimulationResult result = EnergyPlusResultParser.Parse(runtimeResult);
            return RunOutcome.FromRuntime(runtimeResult, result);
        }
        catch (OperationCanceledException)
        {
            return RunOutcome.Cancelled();
        }
        catch (Exception exception)
        {
            return RunOutcome.InternalFailure(exception);
        }
        finally
        {
            if (stagedIdf is not null)
            {
                try
                {
                    File.Delete(stagedIdf);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static string ComputeRunKey(RunInputs inputs)
    {
        string source = string.Join(
            "\n",
            inputs.IdfText,
            inputs.WeatherSha256,
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
        internal RunInputs(
            string idfText,
            string weatherPath,
            string weatherSha256,
            TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            }

            IdfText = idfText;
            WeatherPath = weatherPath;
            WeatherSha256 = weatherSha256;
            Timeout = timeout;
        }

        internal string IdfText { get; }

        internal string WeatherPath { get; }

        internal string WeatherSha256 { get; }

        internal TimeSpan Timeout { get; }
    }

    private sealed class ExplicitTriggerGate
    {
        private bool _observed;
        private bool _previousRun;
        private bool _previousCancel;

        internal ExplicitTriggerObservation Observe(bool run, bool cancel)
        {
            if (!_observed)
            {
                _observed = true;
                _previousRun = run;
                _previousCancel = cancel;
                return default;
            }

            var result = new ExplicitTriggerObservation(
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

    private readonly struct ExplicitTriggerObservation
    {
        internal ExplicitTriggerObservation(bool start, bool cancel)
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
            EnergyPlusSimulationResult? result,
            IEnumerable<Diagnostic> diagnostics)
        {
            State = state;
            Success = success;
            Result = result;
            Diagnostics = diagnostics.ToArray();
        }

        internal string State { get; }

        internal bool Success { get; }

        internal EnergyPlusSimulationResult? Result { get; }

        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal static RunOutcome FromRuntime(
            EnergyPlusRunResult runtime,
            EnergyPlusSimulationResult result)
        {
            var diagnostics = result.Diagnostics.Diagnostics.ToList();
            if (runtime.Failure is not null)
            {
                diagnostics.Insert(0, FailureDiagnostic(runtime.Failure));
            }

            return new RunOutcome(runtime.State.ToString(), runtime.IsSuccess, result, diagnostics);
        }

        internal static RunOutcome FromFailure(EnergyPlusFailure failure)
        {
            return new RunOutcome(
                failure.Category == EnergyPlusFailureCategory.Cancelled ? "Cancelled" : "Failed",
                false,
                null,
                new[] { FailureDiagnostic(failure) });
        }

        internal static RunOutcome Cancelled()
        {
            return FromFailure(new EnergyPlusFailure(
                EnergyPlusFailureCategory.Cancelled,
                "RUN_CANCELLED",
                "The EnergyPlus run was cancelled."));
        }

        internal static RunOutcome InternalFailure(Exception exception)
        {
            return new RunOutcome(
                "Failed",
                false,
                null,
                new[]
                {
                    new Diagnostic(
                        "INVISIBLEDRAGON.GH.MANAGED_RUN_INTERNAL_ERROR",
                        DiagnosticSeverity.Error,
                        "Managed EnergyPlus execution failed internally ("
                            + exception.GetType().Name
                            + ")."),
                });
        }

        private static Diagnostic FailureDiagnostic(EnergyPlusFailure failure)
        {
            string message = string.IsNullOrWhiteSpace(failure.Message)
                ? failure.Code
                : failure.Message.Trim();
            string? suggestedAction = string.IsNullOrWhiteSpace(failure.Detail)
                ? null
                : failure.Detail!.Trim();
            return new Diagnostic(
                "ENERGYPLUS.RUNTIME." + failure.Code,
                failure.Category == EnergyPlusFailureCategory.Cancelled
                    ? DiagnosticSeverity.Info
                    : DiagnosticSeverity.Error,
                message,
                suggestedAction: suggestedAction);
        }
    }
}
