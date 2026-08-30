using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Results;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Stable, SimpleDragon-owned states for one model simulation.
/// </summary>
public enum SimpleDragonSimulationState
{
    Pending,
    ConvertingModel,
    CompilingIdf,
    RunningEnergyPlus,
    ParsingResults,
    BuildingResult,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
}

/// <summary>
/// One progress observation from the Rhino-independent simulation pipeline.
/// </summary>
public sealed record SimpleDragonSimulationTransition(
    SimpleDragonSimulationState State,
    string Message);

/// <summary>
/// Inputs for one simulation after the module has resolved its managed runtime
/// and address-selected weather artifact.
/// </summary>
public sealed class SimpleDragonSimulationRequest
{
    private TimeSpan _timeout = TimeSpan.FromMinutes(30);
    private EnergyPlusCleanupPolicy _cleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess;
    private long _maximumCapturedArtifactBytes = 64L * 1024L * 1024L;

    public SimpleDragonSimulationRequest(
        GreenRetrofitModel model,
        EnergyPlusRuntimeLayout runtime,
        string weatherFilePath,
        string tempRootPath)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        WeatherFilePath = RequiredPath(weatherFilePath, nameof(weatherFilePath));
        TempRootPath = RequiredPath(tempRootPath, nameof(tempRootPath));
    }

    public GreenRetrofitModel Model { get; }

    public EnergyPlusRuntimeLayout Runtime { get; }

    public string WeatherFilePath { get; }

    public string TempRootPath { get; }

    public TimeSpan Timeout
    {
        get => _timeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The EnergyPlus timeout must be positive.");
            }

            _timeout = value;
        }
    }

    public EnergyPlusCleanupPolicy CleanupPolicy
    {
        get => _cleanupPolicy;
        init
        {
            if (!Enum.IsDefined(typeof(EnergyPlusCleanupPolicy), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown cleanup policy.");
            }

            _cleanupPolicy = value;
        }
    }

    public long MaximumCapturedArtifactBytes
    {
        get => _maximumCapturedArtifactBytes;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "At least one captured artifact byte is required.");
            }

            _maximumCapturedArtifactBytes = value;
        }
    }

    private static string RequiredPath(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty path is required.", parameterName);
        }

        return Path.GetFullPath(value.Trim());
    }
}

/// <summary>
/// Terminal SimpleDragon simulation result. InvisibleDragon and raw EnergyPlus
/// implementation values are intentionally absent from this public surface.
/// </summary>
public sealed class SimpleDragonSimulationResult
{
    internal SimpleDragonSimulationResult(
        GreenRetrofitResult? result,
        SimpleDragonSimulationState state,
        IEnumerable<Diagnostic> diagnostics)
    {
        if (!IsTerminal(state))
        {
            throw new ArgumentException("A simulation result requires a terminal state.", nameof(state));
        }

        Diagnostic[] copied = diagnostics?.Distinct().ToArray()
            ?? throw new ArgumentNullException(nameof(diagnostics));
        if (state == SimpleDragonSimulationState.Succeeded
            && (result is null || copied.Any(item => item.IsFailure)))
        {
            throw new ArgumentException(
                "A successful simulation requires a GRR without failure diagnostics.",
                nameof(result));
        }

        if (state != SimpleDragonSimulationState.Succeeded && result is not null)
        {
            throw new ArgumentException(
                "Only a successful simulation may expose a GRR.",
                nameof(result));
        }

        Result = result;
        State = state;
        Diagnostics = new ReadOnlyCollection<Diagnostic>(copied);
    }

    public GreenRetrofitResult? Result { get; }

    public SimpleDragonSimulationState State { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool IsSuccess => State == SimpleDragonSimulationState.Succeeded
        && Result is not null
        && Diagnostics.All(item => !item.IsFailure);

    private static bool IsTerminal(SimpleDragonSimulationState state) =>
        state is SimpleDragonSimulationState.Succeeded
            or SimpleDragonSimulationState.Failed
            or SimpleDragonSimulationState.Cancelled
            or SimpleDragonSimulationState.TimedOut;
}

internal interface ISimpleDragonSimulationExecutor
{
    Task<SimpleDragonSimulationResult> ExecuteAsync(
        SimpleDragonSimulationRequest request,
        IProgress<SimpleDragonSimulationTransition>? progress = null,
        CancellationToken cancellationToken = default);
}

internal interface ISimpleDragonEnergyPlusRunner
{
    Task<EnergyPlusRunResult> RunAsync(
        EnergyPlusRunRequest request,
        IProgress<EnergyPlusRunTransition>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// Converts a GRM, compiles it against the resolved runtime IDD, executes
/// EnergyPlus asynchronously, and reduces the structured output to a GRR.
/// </summary>
public sealed class SimpleDragonSimulationExecutor : ISimpleDragonSimulationExecutor
{
    /// <summary>
    /// Stable primitive identity for cache keys tied to the executable IDF semantics.
    /// </summary>
    public const string ExecutionProfileIdentity = SimpleDragonExecutionIdf.ProfileIdentity;

    private readonly ISimpleDragonEnergyPlusRunner _runner;
    private readonly Func<string, IddSchema> _iddLoader;
    private readonly Func<EnergyPlusRunResult, EnergyPlusSimulationResult> _resultParser;
    private readonly ConcurrentDictionary<string, Lazy<IddSchema>> _schemas =
        new(StringComparer.OrdinalIgnoreCase);

    public SimpleDragonSimulationExecutor()
        : this(
            new DefaultEnergyPlusRunner(),
            IddParser.ParseFile,
            EnergyPlusResultParser.Parse)
    {
    }

    internal SimpleDragonSimulationExecutor(
        ISimpleDragonEnergyPlusRunner runner,
        Func<string, IddSchema> iddLoader,
        Func<EnergyPlusRunResult, EnergyPlusSimulationResult> resultParser)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _iddLoader = iddLoader ?? throw new ArgumentNullException(nameof(iddLoader));
        _resultParser = resultParser ?? throw new ArgumentNullException(nameof(resultParser));
    }

    public async Task<SimpleDragonSimulationResult> ExecuteAsync(
        SimpleDragonSimulationRequest request,
        IProgress<SimpleDragonSimulationTransition>? progress = null,
        CancellationToken cancellationToken = default)
    {
#if NETFRAMEWORK
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
#else
        ArgumentNullException.ThrowIfNull(request);
#endif

        var diagnostics = new List<Diagnostic>();
        string? stagedIdfPath = null;
        try
        {
            Report(progress, SimpleDragonSimulationState.ConvertingModel, "Converting the SimpleDragon model.");
            cancellationToken.ThrowIfCancellationRequested();
            GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(request.Model);
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics.AddRange(conversion.Diagnostics);
            if (!conversion.Success)
            {
                return FinishFailure(
                    progress,
                    diagnostics,
                    "SD.SIMULATION.CONVERSION_FAILED",
                    "The GRM could not be converted to an EnergyPlus model.");
            }

            Report(progress, SimpleDragonSimulationState.CompilingIdf, "Compiling against the verified runtime IDD.");
            cancellationToken.ThrowIfCancellationRequested();
            IddSchema schema = ResolveSchema(request.Runtime);
            cancellationToken.ThrowIfCancellationRequested();
            IdfDocument document = conversion.ToIdfDocument(
                schema,
                SimpleDragonExecutionIdf.CreateOptions());
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(request.TempRootPath);
            stagedIdfPath = Path.Combine(
                request.TempRootPath,
                "simpledragon-input-" + Guid.NewGuid().ToString("N") + ".idf");
            IdfWriter.WriteFile(
                stagedIdfPath,
                document,
                new IdfWriterOptions
                {
                    NewLine = "\n",
                    IncludeSchemaFieldComments = true,
                });

            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, SimpleDragonSimulationState.RunningEnergyPlus, "Running EnergyPlus.");
            var runtimeProgress = new ForwardRuntimeProgress(progress);
            EnergyPlusRunResult runtimeResult = await _runner.RunAsync(
                new EnergyPlusRunRequest(
                    request.Runtime,
                    stagedIdfPath,
                    request.WeatherFilePath,
                    request.TempRootPath)
                {
                    Timeout = request.Timeout,
                    CleanupPolicy = request.CleanupPolicy,
                    MaximumCapturedArtifactBytes = request.MaximumCapturedArtifactBytes,
                },
                runtimeProgress,
                cancellationToken).ConfigureAwait(false);
            if (!runtimeResult.IsSuccess)
            {
                return FromRuntimeFailure(runtimeResult, diagnostics, progress);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, SimpleDragonSimulationState.ParsingResults, "Parsing EnergyPlus outputs.");
            EnergyPlusSimulationResult simulation = _resultParser(runtimeResult);

            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, SimpleDragonSimulationState.BuildingResult, "Building the SimpleDragon GRR.");
            GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
                request.Model,
                simulation);
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics.AddRange(build.Diagnostics);
            diagnostics.AddRange(simulation.Diagnostics.Diagnostics);
            if (!build.Success || diagnostics.Any(item => item.IsFailure))
            {
                return FinishFailure(
                    progress,
                    diagnostics,
                    "SD.SIMULATION.RESULT_FAILED",
                    "The EnergyPlus outputs could not be converted into a complete GRR result.");
            }

            Report(progress, SimpleDragonSimulationState.Succeeded, "SimpleDragon simulation succeeded.");
            return new SimpleDragonSimulationResult(
                build.RequireResult(),
                SimpleDragonSimulationState.Succeeded,
                diagnostics);
        }
        catch (OperationCanceledException)
        {
            return Cancelled(diagnostics, progress);
        }
        catch (Exception exception) when (IsExpectedExecutionException(exception))
        {
            diagnostics.Add(new Diagnostic(
                "SD.SIMULATION.INTERNAL_ERROR",
                DiagnosticSeverity.Error,
                "SimpleDragon simulation failed internally (" + exception.GetType().Name + ").",
                suggestedAction: "Review the model diagnostics and reinstall the managed runtime if the failure persists."));
            Report(progress, SimpleDragonSimulationState.Failed, "SimpleDragon simulation failed.");
            return new SimpleDragonSimulationResult(
                null,
                SimpleDragonSimulationState.Failed,
                diagnostics);
        }
        finally
        {
            TryDeleteStagedIdf(stagedIdfPath);
        }
    }

    private IddSchema ResolveSchema(EnergyPlusRuntimeLayout runtime)
    {
        string key = runtime.IddPath + "|" + runtime.Manifest.EnergyPlusIddSha256;
        IddSchema schema = _schemas.GetOrAdd(
            key,
            _ => new Lazy<IddSchema>(
                () => _iddLoader(runtime.IddPath),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if (!string.Equals(
                schema.SourceSha256,
                runtime.Manifest.EnergyPlusIddSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The parsed IDD does not match the verified runtime manifest.");
        }

        return schema;
    }

    private static SimpleDragonSimulationResult FromRuntimeFailure(
        EnergyPlusRunResult runtime,
        List<Diagnostic> diagnostics,
        IProgress<SimpleDragonSimulationTransition>? progress)
    {
        SimpleDragonSimulationState state = runtime.State switch
        {
            EnergyPlusRunState.Cancelled => SimpleDragonSimulationState.Cancelled,
            EnergyPlusRunState.TimedOut => SimpleDragonSimulationState.TimedOut,
            _ when runtime.Failure?.Category == EnergyPlusFailureCategory.Cancelled =>
                SimpleDragonSimulationState.Cancelled,
            _ when runtime.Failure?.Category == EnergyPlusFailureCategory.Timeout =>
                SimpleDragonSimulationState.TimedOut,
            _ => SimpleDragonSimulationState.Failed,
        };
        diagnostics.Add(RuntimeFailureDiagnostic(runtime.Failure, state));
        Report(progress, state, state switch
        {
            SimpleDragonSimulationState.Cancelled => "SimpleDragon simulation was cancelled.",
            SimpleDragonSimulationState.TimedOut => "SimpleDragon simulation timed out.",
            _ => "EnergyPlus execution failed.",
        });
        return new SimpleDragonSimulationResult(null, state, diagnostics);
    }

    private static Diagnostic RuntimeFailureDiagnostic(
        EnergyPlusFailure? failure,
        SimpleDragonSimulationState state)
    {
        if (failure is null)
        {
            return new Diagnostic(
                "SD.SIMULATION.ENERGYPLUS_FAILED",
                DiagnosticSeverity.Error,
                "EnergyPlus ended without a successful result or a structured failure.");
        }

        return new Diagnostic(
            "ENERGYPLUS.RUNTIME." + failure.Code,
            state == SimpleDragonSimulationState.Cancelled
                ? DiagnosticSeverity.Info
                : DiagnosticSeverity.Error,
            string.IsNullOrWhiteSpace(failure.Message) ? failure.Code : failure.Message.Trim(),
            suggestedAction: RuntimeFailureAction(state));
    }

    private static string? RuntimeFailureAction(SimpleDragonSimulationState state) => state switch
    {
        SimpleDragonSimulationState.Cancelled => null,
        SimpleDragonSimulationState.TimedOut =>
            "Increase the timeout or simplify the model, then retry.",
        _ => "Review the model diagnostics and verify the module-managed EnergyPlus runtime, then retry.",
    };

    private static SimpleDragonSimulationResult FinishFailure(
        IProgress<SimpleDragonSimulationTransition>? progress,
        List<Diagnostic> diagnostics,
        string code,
        string message)
    {
        if (!diagnostics.Any(item => item.IsFailure))
        {
            diagnostics.Add(new Diagnostic(code, DiagnosticSeverity.Error, message));
        }

        Report(progress, SimpleDragonSimulationState.Failed, message);
        return new SimpleDragonSimulationResult(
            null,
            SimpleDragonSimulationState.Failed,
            diagnostics);
    }

    private static SimpleDragonSimulationResult Cancelled(
        List<Diagnostic> diagnostics,
        IProgress<SimpleDragonSimulationTransition>? progress)
    {
        diagnostics.Add(new Diagnostic(
            "SD.SIMULATION.CANCELLED",
            DiagnosticSeverity.Info,
            "The SimpleDragon simulation was cancelled."));
        Report(progress, SimpleDragonSimulationState.Cancelled, "SimpleDragon simulation was cancelled.");
        return new SimpleDragonSimulationResult(
            null,
            SimpleDragonSimulationState.Cancelled,
            diagnostics);
    }

    private static void Report(
        IProgress<SimpleDragonSimulationTransition>? progress,
        SimpleDragonSimulationState state,
        string message)
    {
        if (progress is null)
        {
            return;
        }

        try
        {
            progress.Report(new SimpleDragonSimulationTransition(state, message));
        }
        catch (Exception)
        {
            // Progress observers cannot alter the deterministic simulation outcome.
        }
    }

    private static bool IsExpectedExecutionException(Exception exception) =>
        exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException;

    private static void TryDeleteStagedIdf(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class DefaultEnergyPlusRunner : ISimpleDragonEnergyPlusRunner
    {
        public Task<EnergyPlusRunResult> RunAsync(
            EnergyPlusRunRequest request,
            IProgress<EnergyPlusRunTransition>? progress,
            CancellationToken cancellationToken)
        {
            return new EnergyPlusRunner().RunAsync(request, progress, cancellationToken);
        }
    }

    private sealed class ForwardRuntimeProgress : IProgress<EnergyPlusRunTransition>
    {
        private readonly IProgress<SimpleDragonSimulationTransition>? _target;

        internal ForwardRuntimeProgress(IProgress<SimpleDragonSimulationTransition>? target)
        {
            _target = target;
        }

        public void Report(EnergyPlusRunTransition value)
        {
            SimpleDragonSimulationExecutor.Report(
                _target,
                SimpleDragonSimulationState.RunningEnergyPlus,
                value.Message);
        }
    }
}
