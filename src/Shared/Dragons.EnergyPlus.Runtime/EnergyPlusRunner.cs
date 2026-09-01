using System.ComponentModel;
using System.Globalization;

namespace Dragons.EnergyPlus.Runtime;

/// <summary>
/// Runs ExpandObjects and EnergyPlus in an isolated, caller-owned temporary root.
/// </summary>
public sealed class EnergyPlusRunner
{
    private static readonly TimeSpan MaximumCompatibleTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
    private readonly IProcessExecutor processExecutor;

    public EnergyPlusRunner()
        : this(new ProcessExecutor())
    {
    }

    internal EnergyPlusRunner(IProcessExecutor processExecutor)
    {
        this.processExecutor = processExecutor
            ?? throw new ArgumentNullException(nameof(processExecutor));
    }

    public Task<EnergyPlusRunResult> RunAsync(
        EnergyPlusRunRequest request,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(request, progress: null, cancellationToken);
    }

    public async Task<EnergyPlusRunResult> RunAsync(
        EnergyPlusRunRequest request,
        IProgress<EnergyPlusRunTransition>? progress,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var provisionalRunId = Guid.NewGuid().ToString("N");
        var history = new List<EnergyPlusRunTransition>();
        ProcessExecutionResult? expandObjectsProcess = null;
        ProcessExecutionResult? energyPlusProcess = null;
        var outputs = EnergyPlusOutputFiles.Empty;
        SafeRunDirectory? runDirectory = null;

        void Transition(EnergyPlusRunState state, string message)
        {
            var transition = new EnergyPlusRunTransition(state, DateTimeOffset.UtcNow, message);
            history.Add(transition);
            if (progress is null)
            {
                return;
            }

            try
            {
                progress.Report(transition);
            }
            catch (Exception)
            {
                // Observers must not be able to corrupt or abort the simulation state machine.
            }
        }

        EnergyPlusRunResult Finish(EnergyPlusRunState state, EnergyPlusFailure? failure)
        {
            Transition(state, TerminalMessage(state));
            string? cleanupError = null;
            if (runDirectory is not null && ShouldDelete(request?.CleanupPolicy, state))
            {
                try
                {
                    runDirectory.Delete();
                }
                catch (Exception exception)
                {
                    cleanupError = $"{exception.GetType().FullName}: {exception.Message}";
                }
            }

            var retained = runDirectory is not null && Directory.Exists(runDirectory.Path);
            return new EnergyPlusRunResult(
                runDirectory?.RunId ?? provisionalRunId,
                state,
                failure,
                request?.Runtime,
                runDirectory?.Path,
                retained,
                expandObjectsProcess,
                energyPlusProcess,
                outputs,
                startedAtUtc,
                DateTimeOffset.UtcNow,
                history.ToArray(),
                cleanupError);
        }

        Transition(EnergyPlusRunState.Pending, "The EnergyPlus run was created.");
        Transition(EnergyPlusRunState.Validating, "Validating runtime and caller paths.");
        var validation = Validate(request);
        if (validation.Failure is not null)
        {
            return Finish(EnergyPlusRunState.Failed, validation.Failure);
        }

        var validated = validation.Request!;
        if (cancellationToken.IsCancellationRequested)
        {
            return Finish(EnergyPlusRunState.Cancelled, CancellationFailure());
        }

        using var timeoutSource = new CancellationTokenSource();
        if (request.Timeout is { } timeout)
        {
            timeoutSource.CancelAfter(timeout);
        }

        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        var executionToken = linkedSource.Token;

        EnergyPlusRunResult CancellationOrTimeoutResult()
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Finish(EnergyPlusRunState.Cancelled, CancellationFailure());
            }

            return Finish(
                EnergyPlusRunState.TimedOut,
                new EnergyPlusFailure(
                    EnergyPlusFailureCategory.Timeout,
                    "RUN_TIMED_OUT",
                    "The EnergyPlus run exceeded its configured timeout."));
        }

        try
        {
            Transition(EnergyPlusRunState.Preparing, "Creating an isolated work directory and copying inputs.");
            runDirectory = SafeRunDirectory.Create(validated.TempRootPath);
            executionToken.ThrowIfCancellationRequested();

            var localInputPath = RuntimeFileSystem.CombineUnder(runDirectory.Path, "in.idf");
            var localIddPath = RuntimeFileSystem.CombineUnder(runDirectory.Path, "Energy+.idd");
            var localWeatherPath = RuntimeFileSystem.CombineUnder(runDirectory.Path, "in.epw");
            var outputDirectory = RuntimeFileSystem.CombineUnder(runDirectory.Path, "output");
            Directory.CreateDirectory(outputDirectory);
            File.Copy(validated.InputIdfPath, localInputPath, overwrite: false);
            File.Copy(request.Runtime.IddPath, localIddPath, overwrite: false);
            if (validated.WeatherFilePath is not null)
            {
                File.Copy(validated.WeatherFilePath, localWeatherPath, overwrite: false);
            }

            executionToken.ThrowIfCancellationRequested();
            Transition(EnergyPlusRunState.ExpandingObjects, "Running ExpandObjects.");
            expandObjectsProcess = await processExecutor.RunAsync(
                new ProcessExecutionRequest(
                    EnergyPlusProcessStage.ExpandObjects,
                    request.Runtime.ExpandObjectsExecutablePath,
                    runDirectory.Path,
                    Array.Empty<string>()),
                executionToken).ConfigureAwait(false);

            if (expandObjectsProcess.TerminationReason == ProcessTerminationReason.Cancelled)
            {
                return CancellationOrTimeoutResult();
            }

            if (!expandObjectsProcess.Succeeded)
            {
                return Finish(
                    EnergyPlusRunState.Failed,
                    ProcessFailure("EXPANDOBJECTS_FAILED", "ExpandObjects", expandObjectsProcess));
            }

            var expandedInputPath = RuntimeFileSystem.CombineUnder(runDirectory.Path, "expanded.idf");
            if (!File.Exists(expandedInputPath))
            {
                // ExpandObjects intentionally emits no expanded.idf when the source contains no expandable objects.
                expandedInputPath = localInputPath;
            }

            executionToken.ThrowIfCancellationRequested();
            var arguments = BuildEnergyPlusArguments(
                localIddPath,
                validated.WeatherFilePath is null ? null : localWeatherPath,
                outputDirectory,
                expandedInputPath);
            Transition(EnergyPlusRunState.RunningEnergyPlus, "Running EnergyPlus.");
            energyPlusProcess = await processExecutor.RunAsync(
                new ProcessExecutionRequest(
                    EnergyPlusProcessStage.EnergyPlus,
                    request.Runtime.EnergyPlusExecutablePath,
                    runDirectory.Path,
                    arguments),
                executionToken).ConfigureAwait(false);

            Transition(EnergyPlusRunState.CollectingResults, "Collecting EnergyPlus result files.");
            outputs = await EnergyPlusOutputCollector.CollectAsync(
                outputDirectory,
                request.MaximumCapturedArtifactBytes,
                CancellationToken.None).ConfigureAwait(false);

            if (executionToken.IsCancellationRequested)
            {
                return CancellationOrTimeoutResult();
            }

            if (energyPlusProcess.TerminationReason == ProcessTerminationReason.Cancelled)
            {
                return CancellationOrTimeoutResult();
            }

            if (!energyPlusProcess.Succeeded)
            {
                return Finish(
                    EnergyPlusRunState.Failed,
                    ProcessFailure("ENERGYPLUS_FAILED", "EnergyPlus", energyPlusProcess));
            }

            return Finish(EnergyPlusRunState.Succeeded, failure: null);
        }
        catch (OperationCanceledException)
        {
            return CancellationOrTimeoutResult();
        }
        catch (Win32Exception exception)
        {
            return Finish(
                EnergyPlusRunState.Failed,
                new EnergyPlusFailure(
                    EnergyPlusFailureCategory.ProcessFailure,
                    "PROCESS_START_FAILED",
                    "An EnergyPlus child process could not be started.",
                    exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Finish(
                EnergyPlusRunState.Failed,
                new EnergyPlusFailure(
                    EnergyPlusFailureCategory.RuntimeEnvironment,
                    "RUN_PATH_UNREADABLE",
                    "An input or temporary path is not accessible.",
                    exception.Message));
        }
        catch (IOException exception)
        {
            return Finish(
                EnergyPlusRunState.Failed,
                new EnergyPlusFailure(
                    EnergyPlusFailureCategory.RuntimeEnvironment,
                    "RUN_PATH_IO_ERROR",
                    "An input or temporary path could not be prepared.",
                    exception.Message));
        }
        catch (Exception exception)
        {
            return Finish(
                EnergyPlusRunState.Failed,
                EnergyPlusFailure.Internal(
                    "RUN_INTERNAL_ERROR",
                    "An unexpected error occurred inside the EnergyPlus runner.",
                    exception));
        }
    }

    private static (ValidatedRunRequest? Request, EnergyPlusFailure? Failure) Validate(
        EnergyPlusRunRequest? request)
    {
        if (request is null)
        {
            return Invalid("RUN_REQUEST_REQUIRED", "An EnergyPlus run request is required.");
        }

        if (request.Runtime is null)
        {
            return Invalid("RUNTIME_REQUIRED", "A resolved EnergyPlus runtime is required.");
        }

        if (string.IsNullOrWhiteSpace(request.InputIdfPath))
        {
            return Invalid("INPUT_IDF_PATH_REQUIRED", "An input IDF path is required.");
        }

        if (request.WeatherFilePath is not null && string.IsNullOrWhiteSpace(request.WeatherFilePath))
        {
            return Invalid("WEATHER_PATH_INVALID", "A supplied weather path cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.TempRootPath))
        {
            return Invalid("TEMP_ROOT_REQUIRED", "A caller-owned temporary root is required.");
        }

        if (request.Timeout is { } timeout
            && (timeout <= TimeSpan.Zero || timeout > MaximumCompatibleTimeout))
        {
            return Invalid(
                "TIMEOUT_INVALID",
                $"The timeout must be greater than zero and no longer than {MaximumCompatibleTimeout.TotalDays.ToString("0.###", CultureInfo.InvariantCulture)} days.");
        }

        if (request.MaximumCapturedArtifactBytes <= 0)
        {
            return Invalid(
                "ARTIFACT_CAPTURE_LIMIT_INVALID",
                "The maximum captured artifact size must be greater than zero.");
        }

        if (!Enum.IsDefined(typeof(EnergyPlusCleanupPolicy), request.CleanupPolicy))
        {
            return Invalid("CLEANUP_POLICY_INVALID", "The cleanup policy is not supported.");
        }

        string inputPath;
        string? weatherPath;
        string tempRoot;
        try
        {
            inputPath = Path.GetFullPath(request.InputIdfPath);
            weatherPath = request.WeatherFilePath is null
                ? null
                : Path.GetFullPath(request.WeatherFilePath);
            tempRoot = RuntimeFileSystem.NormalizeDirectory(request.TempRootPath);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.UserInput,
                "RUN_PATH_INVALID",
                "An input or temporary path is invalid.",
                exception.Message));
        }

        if (!File.Exists(inputPath))
        {
            return Invalid("INPUT_IDF_NOT_FOUND", "The input IDF file does not exist.", inputPath);
        }

        if (weatherPath is not null && !File.Exists(weatherPath))
        {
            return Invalid("WEATHER_FILE_NOT_FOUND", "The weather file does not exist.", weatherPath);
        }

        if (File.Exists(tempRoot))
        {
            return Invalid("TEMP_ROOT_IS_FILE", "The temporary root names an existing file.", tempRoot);
        }

        var runtimeFiles = new[]
        {
            request.Runtime.EnergyPlusExecutablePath,
            request.Runtime.ExpandObjectsExecutablePath,
            request.Runtime.IddPath
        };
        if (runtimeFiles.Any(path => !File.Exists(path)))
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "VERIFIED_RUNTIME_CHANGED",
                "A required file disappeared after the EnergyPlus runtime was verified."));
        }

        if (runtimeFiles.Any(path => !RuntimeFileSystem.IsDescendantOf(request.Runtime.RootPath, path)))
        {
            return (null, new EnergyPlusFailure(
                EnergyPlusFailureCategory.RuntimeIntegrity,
                "RUNTIME_PATH_ESCAPE",
                "A runtime file path escapes the verified runtime root."));
        }

        return (new ValidatedRunRequest(inputPath, weatherPath, tempRoot), null);
    }

    private static List<string> BuildEnergyPlusArguments(
        string iddPath,
        string? weatherPath,
        string outputDirectory,
        string expandedInputPath)
    {
        var arguments = new List<string>
        {
            "-i",
            iddPath,
            "-d",
            outputDirectory,
            "-p",
            "eplus",
            "-s",
            "L"
        };
        if (weatherPath is not null)
        {
            arguments.Add("-w");
            arguments.Add(weatherPath);
        }

        arguments.Add(expandedInputPath);
        return arguments;
    }

    private static EnergyPlusFailure ProcessFailure(
        string code,
        string processName,
        ProcessExecutionResult result)
    {
        var detail = $"Exit code: {result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}.";
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            detail += Environment.NewLine + result.StandardError;
        }

        return new EnergyPlusFailure(
            EnergyPlusFailureCategory.ProcessFailure,
            code,
            $"{processName} did not complete successfully.",
            detail);
    }

    private static EnergyPlusFailure CancellationFailure()
    {
        return new EnergyPlusFailure(
            EnergyPlusFailureCategory.Cancelled,
            "RUN_CANCELLED",
            "The EnergyPlus run was cancelled.");
    }

    private static string TerminalMessage(EnergyPlusRunState state)
    {
        return state switch
        {
            EnergyPlusRunState.Succeeded => "The EnergyPlus run completed successfully.",
            EnergyPlusRunState.Cancelled => "The EnergyPlus run was cancelled.",
            EnergyPlusRunState.TimedOut => "The EnergyPlus run timed out.",
            _ => "The EnergyPlus run failed."
        };
    }

    private static bool ShouldDelete(EnergyPlusCleanupPolicy? policy, EnergyPlusRunState state)
    {
        return policy == EnergyPlusCleanupPolicy.DeleteAlways
            || (policy == EnergyPlusCleanupPolicy.DeleteOnSuccess && state == EnergyPlusRunState.Succeeded);
    }

    private static (ValidatedRunRequest? Request, EnergyPlusFailure Failure) Invalid(
        string code,
        string message,
        string? detail = null)
    {
        return (null, new EnergyPlusFailure(
            EnergyPlusFailureCategory.UserInput,
            code,
            message,
            detail));
    }

    private static bool IsPathException(Exception exception)
    {
        return exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException;
    }

    private sealed record ValidatedRunRequest(
        string InputIdfPath,
        string? WeatherFilePath,
        string TempRootPath);
}
