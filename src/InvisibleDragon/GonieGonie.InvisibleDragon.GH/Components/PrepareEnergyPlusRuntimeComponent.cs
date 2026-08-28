using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Grasshopper.Kernel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using Rhino;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Grasshopper owns component lifetime; RemovedFromDocument cancels and completion disposes the token source.")]
public sealed class PrepareEnergyPlusRuntimeComponent : DragonComponent
{
    private static readonly long RefreshIntervalTicks = Stopwatch.Frequency / 4;

    private readonly object syncRoot = new();
    private readonly ExplicitTriggerGate triggerGate = new();
    private CancellationTokenSource? activeCancellation;
    private Task<BootstrapOutcome>? activeTask;
    private BootstrapOutcome? lastOutcome;
    private string? lastRequestKey;
    private string stateText = "Idle";
    private string progressText = "Toggle Prepare from False to True to check or prepare EnergyPlus.";
    private double progressFraction;
    private long lastRefreshTimestamp;
    private bool removed;

    public PrepareEnergyPlusRuntimeComponent()
        : base(
            "Prepare EnergyPlus Runtime",
            "Prepare E+",
            "Checks or securely prepares the pinned per-user EnergyPlus 24.2 runtime on an explicit Boolean rising edge. "
            + "The bundled archive is used first and verified HTTPS is used only when the bundle is absent. "
            + "The managed default is stored safely in LocalAppData; weather files are never acquired.",
            DragonPanels.Core)
    {
    }

    public override Guid ComponentGuid => new("5199b03c-644b-4194-b38c-37f3c7a423aa");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter(
            "Target Root",
            "Root",
            "Optional per-user target directory. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory. Empty uses the pinned GonieGonie LocalAppData cache.",
            GH_ParamAccess.item,
            string.Empty);
        pManager.AddBooleanParameter(
            "Prepare",
            "Prepare",
            "Toggle from False to True to perform one check/install attempt. A saved True value does not run when a document opens.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Cancel",
            "Cancel",
            "Toggle from False to True to cancel the active attempt.",
            GH_ParamAccess.item,
            false);
        pManager.AddBooleanParameter(
            "Replace Invalid Custom Target",
            "Repair",
            "Explicitly allow an invalid existing custom target to be replaced transactionally. The managed default cache repairs itself safely.",
            GH_ParamAccess.item,
            false);
        pManager.AddNumberParameter(
            "Lock Timeout",
            "Min",
            "Positive minutes to wait when another process is preparing the same runtime.",
            GH_ParamAccess.item,
            2d);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Runtime Root", "Root", "Verified runtime root, when ready.", GH_ParamAccess.item);
        pManager.AddTextParameter("Executable", "E+", "Verified energyplus.exe path, when ready.", GH_ParamAccess.item);
        pManager.AddTextParameter("State", "State", "Idle, active bootstrap stage, Ready, Cancelled, or Failed.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Ready", "Ready", "True only when the pinned runtime was hash-verified.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Progress", "%", "Bundled-copy or HTTPS-download progress from 0 to 1 when byte totals are known.", GH_ParamAccess.item);
        pManager.AddTextParameter("Message", "Message", "Current bootstrap progress or terminal message.", GH_ParamAccess.item);
        pManager.AddParameter(new DiagnosticParam(), "Diagnostics", "D", "Structured bootstrap diagnostics.", GH_ParamAccess.list);
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
        string targetRoot = string.Empty;
        bool prepareTrigger = false;
        bool cancelTrigger = false;
        bool replaceInvalidCustomTarget = false;
        double lockTimeoutMinutes = 2d;
        DA.GetData(0, ref targetRoot);
        DA.GetData(1, ref prepareTrigger);
        DA.GetData(2, ref cancelTrigger);
        DA.GetData(3, ref replaceInvalidCustomTarget);
        DA.GetData(4, ref lockTimeoutMinutes);

        ExplicitTriggerObservation triggers = triggerGate.Observe(prepareTrigger, cancelTrigger);
        if (triggers.Cancel)
        {
            CancelActivePreparation();
        }

        GH_Document? document = OnPingDocument();
        string? documentFilePath = document is not null && document.IsFilePathDefined
            ? document.FilePath
            : null;
        string? resolvedTargetRoot = OptionalFullPath(targetRoot, documentFilePath);

        string requestKey = RequestKey(
            resolvedTargetRoot ?? string.Empty,
            replaceInvalidCustomTarget,
            lockTimeoutMinutes);
        if (triggers.Start)
        {
            StartPreparation(
                new BootstrapInputs(
                    resolvedTargetRoot,
                    replaceInvalidCustomTarget,
                    TimeSpan.FromMinutes(lockTimeoutMinutes)),
                requestKey);
        }

        BootstrapOutcome? outcome;
        bool running;
        string currentState;
        string currentProgressText;
        double currentProgressFraction;
        string? outcomeRequestKey;
        lock (syncRoot)
        {
            outcome = lastOutcome;
            running = activeTask is not null;
            currentState = stateText;
            currentProgressText = progressText;
            currentProgressFraction = progressFraction;
            outcomeRequestKey = lastRequestKey;
        }

        if (!running && outcome is not null && !string.Equals(outcomeRequestKey, requestKey, StringComparison.Ordinal))
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "The displayed runtime belongs to previous inputs. Toggle Prepare False, then True, to evaluate the current target.");
        }

        if (outcome is not null)
        {
            Report(outcome.Diagnostics);
        }

        Message = currentState;
        DA.SetData(0, outcome?.RuntimeRoot ?? string.Empty);
        DA.SetData(1, outcome?.ExecutablePath ?? string.Empty);
        DA.SetData(2, currentState);
        DA.SetData(3, outcome?.Ready ?? false);
        DA.SetData(4, currentProgressFraction);
        DA.SetData(5, currentProgressText);
        DA.SetDataList(6, outcome?.Diagnostics.Select(item => new DiagnosticGoo(item)) ?? Enumerable.Empty<DiagnosticGoo>());
    }

    private void StartPreparation(BootstrapInputs inputs, string requestKey)
    {
        lock (syncRoot)
        {
            if (activeTask is not null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "An EnergyPlus preparation is already active. Toggle Cancel to stop it before retrying.");
                return;
            }

            activeCancellation = new CancellationTokenSource();
            CancellationToken token = activeCancellation.Token;
            stateText = "Checking";
            progressText = "Checking the requested EnergyPlus runtime.";
            progressFraction = 0d;
            Task<BootstrapOutcome> task = Task.Run(() => ExecuteAsync(inputs, UpdateProgress, token), token);
            activeTask = task;
            _ = task.ContinueWith(
                completed => CompletePreparation(completed, requestKey),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private void CancelActivePreparation()
    {
        CancellationTokenSource? cancellation;
        lock (syncRoot)
        {
            cancellation = activeTask is null ? null : activeCancellation;
            if (cancellation is not null)
            {
                stateText = "Cancelling";
                progressText = "Cancelling the active EnergyPlus preparation.";
            }
        }

        if (cancellation is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "There is no active EnergyPlus preparation to cancel.");
            return;
        }

        TryCancel(cancellation);
    }

    private void UpdateProgress(EnergyPlusRuntimeBootstrapProgress update)
    {
        lock (syncRoot)
        {
            stateText = StageText(update.Stage);
            progressText = update.Message;
            if (update.TotalBytes is > 0 && update.CompletedBytes is >= 0)
            {
                progressFraction = Math.Max(
                    0d,
                    Math.Min(1d, (double)update.CompletedBytes.Value / update.TotalBytes.Value));
            }
            else if (update.Stage == EnergyPlusRuntimeBootstrapStage.Completed)
            {
                progressFraction = 1d;
            }
        }

        RequestSolutionRefresh(force: false);
    }

    private void CompletePreparation(Task<BootstrapOutcome> completed, string requestKey)
    {
        BootstrapOutcome outcome;
        if (completed.Status == TaskStatus.RanToCompletion)
        {
            outcome = completed.Result;
        }
        else if (completed.IsCanceled)
        {
            outcome = BootstrapOutcome.Cancelled();
        }
        else
        {
            Exception exception = completed.Exception?.GetBaseException()
                ?? new InvalidOperationException("The EnergyPlus preparation faulted without an exception.");
            outcome = BootstrapOutcome.InternalFailure(exception);
        }

        lock (syncRoot)
        {
            activeTask = null;
            activeCancellation?.Dispose();
            activeCancellation = null;
            lastOutcome = outcome;
            lastRequestKey = requestKey;
            stateText = outcome.State;
            progressText = outcome.Message;
            if (outcome.Ready)
            {
                progressFraction = 1d;
            }
        }

        RequestSolutionRefresh(force: true);
    }

    private void RequestSolutionRefresh(bool force)
    {
        if (!force)
        {
            long now = Stopwatch.GetTimestamp();
            long previous = Interlocked.Read(ref lastRefreshTimestamp);
            if (now - previous < RefreshIntervalTicks)
            {
                return;
            }

            Interlocked.Exchange(ref lastRefreshTimestamp, now);
        }

        lock (syncRoot)
        {
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

    private static async Task<BootstrapOutcome> ExecuteAsync(
        BootstrapInputs inputs,
        Action<EnergyPlusRuntimeBootstrapProgress> progressChanged,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new EnergyPlusRuntimeBootstrapOptions
            {
                TargetRoot = inputs.TargetRoot,
                ReplaceInvalidExistingTarget = inputs.ReplaceInvalidCustomTarget,
                LockWaitTimeout = inputs.LockWaitTimeout,
            };
            var progress = new InlineProgress<EnergyPlusRuntimeBootstrapProgress>(progressChanged);
            EnergyPlusRuntimeBootstrapResult result = await new EnergyPlusRuntimeBootstrapper()
                .EnsureInstalledAsync(options, progress, cancellationToken)
                .ConfigureAwait(false);
            return BootstrapOutcome.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            return BootstrapOutcome.Cancelled();
        }
        catch (Exception exception)
        {
            return BootstrapOutcome.InternalFailure(exception);
        }
    }

    private static string? OptionalFullPath(string value, string? documentFilePath)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : GrasshopperDocumentPathResolver.Resolve(
                value,
                documentFilePath,
                Path.GetTempPath());
    }

    private static string RequestKey(string targetRoot, bool replaceInvalidCustomTarget, double lockTimeoutMinutes)
    {
        return string.Join(
            "|",
            string.IsNullOrWhiteSpace(targetRoot) ? "<default>" : targetRoot.Trim(),
            replaceInvalidCustomTarget.ToString(),
            lockTimeoutMinutes.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string StageText(EnergyPlusRuntimeBootstrapStage stage)
    {
        return stage switch
        {
            EnergyPlusRuntimeBootstrapStage.CheckingExistingRuntime => "Checking",
            EnergyPlusRuntimeBootstrapStage.WaitingForInstallLock => "Waiting",
            EnergyPlusRuntimeBootstrapStage.DownloadingArchive => "Acquiring",
            EnergyPlusRuntimeBootstrapStage.VerifyingArchive => "Verifying Archive",
            EnergyPlusRuntimeBootstrapStage.ExtractingArchive => "Extracting",
            EnergyPlusRuntimeBootstrapStage.VerifyingExtractedRuntime => "Verifying Runtime",
            EnergyPlusRuntimeBootstrapStage.PromotingRuntime => "Installing",
            EnergyPlusRuntimeBootstrapStage.Completed => "Ready",
            _ => stage.ToString(),
        };
    }

    private static void TryCancel(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Completion may dispose the source between the state check and cancellation.
        }
    }

    private sealed class BootstrapInputs
    {
        internal BootstrapInputs(
            string? targetRoot,
            bool replaceInvalidCustomTarget,
            TimeSpan lockWaitTimeout)
        {
            if (lockWaitTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lockWaitTimeout),
                    "Lock timeout must be positive.");
            }

            TargetRoot = targetRoot;
            ReplaceInvalidCustomTarget = replaceInvalidCustomTarget;
            LockWaitTimeout = lockWaitTimeout;
        }

        internal string? TargetRoot { get; }

        internal bool ReplaceInvalidCustomTarget { get; }

        internal TimeSpan LockWaitTimeout { get; }
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

    private sealed class BootstrapOutcome
    {
        private BootstrapOutcome(
            string state,
            bool ready,
            string? runtimeRoot,
            string? executablePath,
            string message,
            IEnumerable<Diagnostic> diagnostics)
        {
            State = state;
            Ready = ready;
            RuntimeRoot = runtimeRoot;
            ExecutablePath = executablePath;
            Message = message;
            Diagnostics = diagnostics.ToArray();
        }

        internal string State { get; }

        internal bool Ready { get; }

        internal string? RuntimeRoot { get; }

        internal string? ExecutablePath { get; }

        internal string Message { get; }

        internal IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal static BootstrapOutcome FromResult(EnergyPlusRuntimeBootstrapResult result)
        {
            if (result.IsSuccess)
            {
                string verb = result.Disposition == EnergyPlusRuntimeBootstrapDisposition.Reused
                    ? "Reused"
                    : "Installed";
                string message = verb + " and hash-verified EnergyPlus 24.2.0 at " + result.Runtime!.RootPath + ".";
                return new BootstrapOutcome(
                    "Ready",
                    true,
                    result.Runtime.RootPath,
                    result.Runtime.EnergyPlusExecutablePath,
                    message,
                    new[]
                    {
                        new Diagnostic(
                            "ENERGYPLUS.RUNTIME.BOOTSTRAP_READY",
                            DiagnosticSeverity.Info,
                            message),
                    });
            }

            EnergyPlusFailure failure = result.Failure
                ?? new EnergyPlusFailure(
                    EnergyPlusFailureCategory.Internal,
                    "BOOTSTRAP_EMPTY_RESULT",
                    "EnergyPlus preparation returned neither a runtime nor a failure.");
            return FromFailure(failure, result.TargetRoot);
        }

        internal static BootstrapOutcome Cancelled()
        {
            return FromFailure(
                new EnergyPlusFailure(
                    EnergyPlusFailureCategory.Cancelled,
                    "BOOTSTRAP_CANCELLED",
                    "EnergyPlus preparation was cancelled."),
                null);
        }

        internal static BootstrapOutcome InternalFailure(Exception exception)
        {
            return new BootstrapOutcome(
                "Failed",
                false,
                null,
                null,
                exception.GetType().Name + ": " + exception.Message,
                new[]
                {
                    new Diagnostic(
                        "ENERGYPLUS.RUNTIME.BOOTSTRAP_UI_INTERNAL",
                        DiagnosticSeverity.Error,
                        exception.GetType().Name + ": " + exception.Message),
                });
        }

        private static BootstrapOutcome FromFailure(EnergyPlusFailure failure, string? targetRoot)
        {
            string state = failure.Category == EnergyPlusFailureCategory.Cancelled ? "Cancelled" : "Failed";
            DiagnosticSeverity severity = failure.Category == EnergyPlusFailureCategory.Cancelled
                ? DiagnosticSeverity.Info
                : DiagnosticSeverity.Error;
            return new BootstrapOutcome(
                state,
                false,
                targetRoot,
                null,
                failure.Message,
                new[]
                {
                    new Diagnostic(
                        "ENERGYPLUS.RUNTIME." + failure.Code,
                        severity,
                        failure.Message,
                        suggestedAction: failure.Detail),
                });
        }
    }
}
