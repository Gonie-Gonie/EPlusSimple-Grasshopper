using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Results;
using Rhino;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; RemovedFromDocument cancels and the task continuation disposes the token source.")]
public sealed class RunEnergyPlusComponent : DragonComponent
{
    private readonly object syncRoot = new();
    private readonly ExplicitTriggerGate triggerGate = new();
    private CancellationTokenSource? activeCancellation;
    private Task<RunOutcome>? activeTask;
    private RunOutcome? lastOutcome;
    private string? lastRunKey;
    private string stateText = "Idle";
    private bool removed;

    public RunEnergyPlusComponent()
        : base(
            "Run EnergyPlus",
            "Run",
            "Runs EnergyPlus 24.2 off the Rhino UI thread on an explicit Boolean rising edge.",
            DragonPanels.Core)
    {
    }

    public override Guid ComponentGuid => new("5f1a9663-6f81-4635-b54d-607b48c9fd47");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddParameter(new DragonIdfParam(), "IDF", "IDF", "Compiled EnergyPlus IDF document.", GH_ParamAccess.item);
        pManager.AddTextParameter(
            "Weather",
            "EPW",
            "Optional user-supplied EPW weather-file path. Relative paths use the saved Grasshopper document. InvisibleDragon never downloads weather files.",
            GH_ParamAccess.item,
            string.Empty);
        pManager.AddTextParameter(
            "Runtime Root",
            "E+",
            "Optional EnergyPlus 24.2 root. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory so Prepare Missing Runtime remains writable. Empty checks the verified per-user cache, environment hints, and default install.",
            GH_ParamAccess.item,
            string.Empty);
        pManager.AddTextParameter(
            "Temp Root",
            "Temp",
            "Optional caller-owned temporary root for isolated runs. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory.",
            GH_ParamAccess.item,
            string.Empty);
        pManager.AddBooleanParameter(
            "Run",
            "Run",
            "Toggle from False to True to start one run. A saved True value does not run when a document opens.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Cancel",
            "Cancel",
            "Toggle from False to True to cancel the active run.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter("Force Rerun", "Force", "Ignore the last matching run-key result.", GH_ParamAccess.item, false);
        pManager.AddBooleanParameter("Keep Work Directory", "Keep", "Retain successful EnergyPlus work files.", GH_ParamAccess.item, true);
        pManager.AddNumberParameter("Timeout", "Min", "Positive timeout in minutes.", GH_ParamAccess.item, 30);
        pManager.AddBooleanParameter(
            "Prepare Missing Runtime",
            "Prepare",
            "When Run rises and no verified runtime is available, securely prepare the pinned per-user runtime before running. "
            + "This may download EnergyPlus, never weather, and cannot occur during an ordinary recompute.",
            GH_ParamAccess.item,
            false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new EnergyPlusResultParam(), "Result", "R", "Last structured EnergyPlus result.", GH_ParamAccess.item);
        pManager.AddTextParameter("State", "S", "Idle, active EnergyPlus state, Cached, or terminal state.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Success", "OK", "True when the last run succeeded.", GH_ParamAccess.item);
        pManager.AddTextParameter("Work Directory", "Dir", "Retained EnergyPlus work directory, when available.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Runtime and EnergyPlus diagnostics.", GH_ParamAccess.list);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        lock (syncRoot)
        {
            removed = false;
            triggerGate.Reset();
        }
    }

    public override void RemovedFromDocument(GH_Document document)
    {
        CancellationTokenSource? cancellation;
        lock (syncRoot)
        {
            removed = true;
            cancellation = activeCancellation;
        }

        TryCancel(cancellation);
        base.RemovedFromDocument(document);
    }

    protected override void Solve(IGH_DataAccess DA)
    {
        DragonIdfGoo? idfGoo = null;
        string weatherPath = string.Empty;
        string runtimeRoot = string.Empty;
        string tempRoot = string.Empty;
        bool runTrigger = false;
        bool cancelTrigger = false;
        bool forceRerun = false;
        bool keepWorkDirectory = true;
        bool prepareMissingRuntime = false;
        double timeoutMinutes = 30;
        if (!DA.GetData(0, ref idfGoo))
        {
            return;
        }

        DA.GetData(1, ref weatherPath);
        DA.GetData(2, ref runtimeRoot);
        DA.GetData(3, ref tempRoot);
        DA.GetData(4, ref runTrigger);
        DA.GetData(5, ref cancelTrigger);
        DA.GetData(6, ref forceRerun);
        DA.GetData(7, ref keepWorkDirectory);
        DA.GetData(8, ref timeoutMinutes);
        DA.GetData(9, ref prepareMissingRuntime);

        if (idfGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "IDF is required.");
            return;
        }

        GH_Document? document = OnPingDocument();
        string? documentFilePath = document is not null && document.IsFilePathDefined
            ? document.FilePath
            : null;
        string idfText = IdfWriter.Write(idfGoo.Value);
        var inputs = new RunInputs(
            idfText,
            OptionalFullPath(weatherPath, documentFilePath, Directory.GetCurrentDirectory()),
            OptionalFullPath(runtimeRoot, documentFilePath, Path.GetTempPath()),
            ResolveTempRoot(tempRoot, documentFilePath),
            TimeSpan.FromMinutes(timeoutMinutes),
            keepWorkDirectory,
            prepareMissingRuntime);
        string runKey = ComputeRunKey(inputs);

        ExplicitTriggerObservation triggers = triggerGate.Observe(runTrigger, cancelTrigger);
        if (triggers.Cancel)
        {
            CancelActiveRun();
        }

        if (triggers.Start)
        {
            StartRun(inputs, runKey, forceRerun);
        }

        RunOutcome? outcome;
        bool running;
        string currentState;
        string? outcomeRunKey;
        lock (syncRoot)
        {
            outcome = lastOutcome;
            running = activeTask is not null;
            currentState = stateText;
            outcomeRunKey = lastRunKey;
        }

        if (!running && outcome is not null && !string.Equals(outcomeRunKey, runKey, StringComparison.Ordinal))
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "The displayed result belongs to previous inputs. Toggle Run False, then True, to evaluate the current IDF.");
        }

        if (outcome is not null)
        {
            Report(outcome.Diagnostics);
        }

        Message = currentState;
        DA.SetData(0, outcome?.Result is null ? null : new EnergyPlusResultGoo(outcome.Result));
        DA.SetData(1, currentState);
        DA.SetData(2, outcome?.Success ?? false);
        DA.SetData(3, outcome?.WorkDirectory ?? string.Empty);
        DA.SetDataList(4, outcome?.Diagnostics.Select(item => new DiagnosticGoo(item)) ?? Enumerable.Empty<DiagnosticGoo>());
    }

    private void StartRun(RunInputs inputs, string runKey, bool forceRerun)
    {
        lock (syncRoot)
        {
            if (activeTask is not null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An EnergyPlus run is already active; cancel it before starting another.");
                return;
            }

            if (!forceRerun && lastOutcome is not null && string.Equals(lastRunKey, runKey, StringComparison.Ordinal))
            {
                stateText = "Cached";
                return;
            }

            activeCancellation = new CancellationTokenSource();
            CancellationToken token = activeCancellation.Token;
            stateText = "Validating";
            Task<RunOutcome> task = Task.Run(() => ExecuteAsync(inputs, UpdateState, token), token);
            activeTask = task;
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
        lock (syncRoot)
        {
            if (activeCancellation is null || activeTask is null)
            {
                cancellation = null;
            }
            else
            {
                stateText = "Cancelling";
                cancellation = activeCancellation;
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
        lock (syncRoot)
        {
            stateText = state;
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
            // A just-completed run may dispose its source between the state check and this request.
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
            Exception exception = completed.Exception?.GetBaseException()
                ?? new InvalidOperationException("The EnergyPlus task faulted without an exception.");
            outcome = RunOutcome.InternalFailure(exception);
        }

        lock (syncRoot)
        {
            activeTask = null;
            activeCancellation?.Dispose();
            activeCancellation = null;
            lastOutcome = outcome;
            lastRunKey = runKey;
            stateText = outcome.State;
            if (removed)
            {
                return;
            }
        }

        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            lock (syncRoot)
            {
                if (removed)
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
            var resolver = new RuntimeResolver();
            var resolveOptions = new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = inputs.RuntimeRoot,
            };
            EnergyPlusRuntimeResolution resolution = await resolver.ResolveAsync(resolveOptions, cancellationToken)
                .ConfigureAwait(false);
            EnergyPlusRuntimeLayout? runtime = resolution.Runtime;
            if (runtime is null && inputs.PrepareMissingRuntime)
            {
                stateChanged("Preparing Runtime");
                var bootstrapOptions = new EnergyPlusRuntimeBootstrapOptions
                {
                    TargetRoot = inputs.RuntimeRoot,
                };
                var bootstrapProgress = new InlineProgress<EnergyPlusRuntimeBootstrapProgress>(
                    update => stateChanged("Preparing: " + update.Stage));
                EnergyPlusRuntimeBootstrapResult bootstrap = await new EnergyPlusRuntimeBootstrapper()
                    .EnsureInstalledAsync(bootstrapOptions, bootstrapProgress, cancellationToken)
                    .ConfigureAwait(false);
                if (!bootstrap.IsSuccess)
                {
                    return RunOutcome.FromResolutionFailure(bootstrap.Failure!);
                }

                runtime = bootstrap.Runtime;
            }

            if (runtime is null)
            {
                return RunOutcome.FromResolutionFailure(resolution.Failure!);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(inputs.TempRoot);
            stagedIdf = Path.Combine(inputs.TempRoot, $"grasshopper-input-{Guid.NewGuid():N}.idf");
            File.WriteAllText(stagedIdf, inputs.IdfText, new UTF8Encoding(false));
            var request = new EnergyPlusRunRequest(
                runtime,
                stagedIdf,
                inputs.WeatherPath,
                inputs.TempRoot)
            {
                Timeout = inputs.Timeout,
                CleanupPolicy = inputs.KeepWorkDirectory
                    ? EnergyPlusCleanupPolicy.KeepAlways
                    : EnergyPlusCleanupPolicy.DeleteOnSuccess,
            };
            var progress = new InlineProgress<EnergyPlusRunTransition>(transition => stateChanged(transition.State.ToString()));
            EnergyPlusRunResult runtimeResult = await new EnergyPlusRunner()
                .RunAsync(request, progress, cancellationToken)
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
                    // The isolated runner has already copied the input; a locked staging file is harmless.
                }
                catch (UnauthorizedAccessException)
                {
                    // Reported run results remain valid even when the staging file cannot be removed.
                }
            }
        }
    }

    private static string ResolveTempRoot(string supplied, string? documentFilePath)
    {
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return ResolvePath(supplied, documentFilePath, Path.GetTempPath());
        }

        string? configured = Environment.GetEnvironmentVariable("GONIEGONIE_TEMP_ROOT");
        return !string.IsNullOrWhiteSpace(configured)
            ? ResolvePath(configured, null, Path.GetTempPath())
            : Path.Combine(Path.GetTempPath(), "GonieGonie", "Dragons", "temp");
    }

    private static string? OptionalFullPath(
        string value,
        string? documentFilePath,
        string fallbackDirectory)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : ResolvePath(value, documentFilePath, fallbackDirectory);
    }

    private static string ResolvePath(
        string path,
        string? documentFilePath,
        string fallbackDirectory)
    {
        return GrasshopperDocumentPathResolver.Resolve(
            path,
            documentFilePath,
            fallbackDirectory);
    }

    private static string ComputeRunKey(RunInputs inputs)
    {
        string weatherIdentity = inputs.WeatherPath ?? "<none>";
        if (inputs.WeatherPath is not null && File.Exists(inputs.WeatherPath))
        {
            var weather = new FileInfo(inputs.WeatherPath);
            weatherIdentity += $"|{weather.Length.ToString(CultureInfo.InvariantCulture)}|{weather.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)}";
        }

        string source = string.Join(
            "\n",
            inputs.IdfText,
            weatherIdentity,
            inputs.RuntimeRoot ?? "<auto>",
            inputs.Timeout.TotalSeconds.ToString("R", CultureInfo.InvariantCulture),
            inputs.KeepWorkDirectory.ToString(CultureInfo.InvariantCulture),
            inputs.PrepareMissingRuntime.ToString(CultureInfo.InvariantCulture));
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
            string? weatherPath,
            string? runtimeRoot,
            string tempRoot,
            TimeSpan timeout,
            bool keepWorkDirectory,
            bool prepareMissingRuntime)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            }

            IdfText = idfText;
            WeatherPath = weatherPath;
            RuntimeRoot = runtimeRoot;
            TempRoot = tempRoot;
            Timeout = timeout;
            KeepWorkDirectory = keepWorkDirectory;
            PrepareMissingRuntime = prepareMissingRuntime;
        }

        internal string IdfText { get; }
        internal string? WeatherPath { get; }
        internal string? RuntimeRoot { get; }
        internal string TempRoot { get; }
        internal TimeSpan Timeout { get; }
        internal bool KeepWorkDirectory { get; }
        internal bool PrepareMissingRuntime { get; }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> report;

        internal InlineProgress(Action<T> report)
        {
            this.report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public void Report(T value)
        {
            report(value);
        }
    }

    private sealed class RunOutcome
    {
        private RunOutcome(
            string state,
            bool success,
            EnergyPlusSimulationResult? result,
            string? workDirectory,
            IEnumerable<Diagnostic> diagnostics)
        {
            State = state;
            Success = success;
            Result = result;
            WorkDirectory = workDirectory;
            Diagnostics = diagnostics.ToArray();
        }

        internal string State { get; }
        internal bool Success { get; }
        internal EnergyPlusSimulationResult? Result { get; }
        internal string? WorkDirectory { get; }
        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal static RunOutcome FromRuntime(EnergyPlusRunResult runtime, EnergyPlusSimulationResult result)
        {
            var diagnostics = result.Diagnostics.Diagnostics.ToList();
            if (runtime.Failure is not null)
            {
                diagnostics.Insert(0, FailureDiagnostic(runtime.Failure));
            }

            return new RunOutcome(
                runtime.State.ToString(),
                runtime.IsSuccess,
                result,
                runtime.WorkDirectory,
                diagnostics);
        }

        internal static RunOutcome FromResolutionFailure(EnergyPlusFailure failure)
        {
            string state = failure.Category == EnergyPlusFailureCategory.Cancelled ? "Cancelled" : "Failed";
            return new RunOutcome(state, false, null, null, new[] { FailureDiagnostic(failure) });
        }

        internal static RunOutcome Cancelled()
        {
            return FromResolutionFailure(new EnergyPlusFailure(
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
                null,
                new[]
                {
                    new Diagnostic(
                        "INVISIBLEDRAGON.GH.RUN_INTERNAL_ERROR",
                        DiagnosticSeverity.Error,
                        $"{exception.GetType().Name}: {exception.Message}"),
                });
        }

        private static Diagnostic FailureDiagnostic(EnergyPlusFailure failure)
        {
            DiagnosticSeverity severity = failure.Category == EnergyPlusFailureCategory.Cancelled
                ? DiagnosticSeverity.Info
                : DiagnosticSeverity.Error;
            return new Diagnostic(
                $"ENERGYPLUS.RUNTIME.{failure.Code}",
                severity,
                failure.Message,
                suggestedAction: failure.Detail);
        }
    }
}
