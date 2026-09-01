using System.Globalization;
using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon.Batch;

/// <summary>
/// Production batch executor that converts GRM alternatives, runs the verified runtime, and emits GRR summaries.
/// </summary>
public sealed class EnergyPlusBatchCaseExecutor : IBatchCaseExecutor
{
    private readonly EnergyPlusRuntimeLayout _runtime;
    private readonly ISimpleDragonSimulationExecutor _simulationExecutor;
    private readonly TimeSpan _timeout;
    private readonly long _maximumCapturedArtifactBytes;

    public EnergyPlusBatchCaseExecutor(
        EnergyPlusRuntimeLayout runtime,
        TimeSpan? timeout = null,
        long maximumCapturedArtifactBytes = 64L * 1024L * 1024L)
        : this(
            runtime,
            new SimpleDragonSimulationExecutor(),
            timeout,
            maximumCapturedArtifactBytes)
    {
    }

    internal EnergyPlusBatchCaseExecutor(
        EnergyPlusRuntimeLayout runtime,
        ISimpleDragonSimulationExecutor simulationExecutor,
        TimeSpan? timeout = null,
        long maximumCapturedArtifactBytes = 64L * 1024L * 1024L)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _simulationExecutor = simulationExecutor
            ?? throw new ArgumentNullException(nameof(simulationExecutor));
        _timeout = timeout ?? TimeSpan.FromMinutes(30);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The EnergyPlus timeout must be positive.");
        }

        if (maximumCapturedArtifactBytes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCapturedArtifactBytes),
                "At least one captured artifact byte is required.");
        }

        _maximumCapturedArtifactBytes = maximumCapturedArtifactBytes;
        EnergyPlusRuntimeManifest manifest = runtime.Manifest;
        RuntimeIdentity = new BatchRuntimeIdentity(
            manifest.EnergyPlusVersion,
            manifest.EnergyPlusBuild,
            manifest.EnergyPlusExecutableSha256,
            manifest.EnergyPlusIddSha256,
            manifest.ExpandObjectsSha256);
        CanonicalExecutionOptions = BatchDeterminism.CanonicalizeOptions(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["cleanup_policy"] = EnergyPlusCleanupPolicy.DeleteOnSuccess.ToString(),
                ["idf_execution_profile"] = SimpleDragonSimulationExecutor.ExecutionProfileIdentity,
                ["maximum_captured_artifact_bytes"] = maximumCapturedArtifactBytes.ToString(CultureInfo.InvariantCulture),
                ["timeout_seconds"] = CanonicalDouble.Format(_timeout.TotalSeconds),
            });
        CanonicalOutputOptions = BatchDeterminism.CanonicalizeOptions(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["combined_metrics"] = "GRR annual gross and per-area summaries",
                ["idf_execution_profile"] = SimpleDragonSimulationExecutor.ExecutionProfileIdentity,
                ["idf_new_line"] = "LF",
                ["idf_schema_comments"] = "true",
            });
    }

    public string ExecutorIdentity => "Dragons.SimpleDragon.EnergyPlusBatchCaseExecutor/v2";

    public BatchRuntimeIdentity RuntimeIdentity { get; }

    public string CanonicalExecutionOptions { get; }

    public string CanonicalOutputOptions { get; }

    public async Task<BatchCaseExecution> ExecuteAsync(
        BatchCaseContext context,
        CancellationToken cancellationToken)
    {
        DomainSupport.NotNull(context, nameof(context));

        cancellationToken.ThrowIfCancellationRequested();
        if (context.WeatherFilePath is null)
        {
            return BatchCaseExecution.Failure(new[]
            {
                new Diagnostic(
                    "SD.BATCH.WEATHER_REQUIRED",
                    DiagnosticSeverity.Error,
                    "The prepared SimpleDragon weather artifact is required for EnergyPlus execution."),
            });
        }

        SimpleDragonSimulationResult simulation = await _simulationExecutor.ExecuteAsync(
            new SimpleDragonSimulationRequest(
                context.Model,
                _runtime,
                context.WeatherFilePath,
                context.WorkRootPath)
            {
                Timeout = _timeout,
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess,
                MaximumCapturedArtifactBytes = _maximumCapturedArtifactBytes,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (simulation.State == SimpleDragonSimulationState.Cancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!simulation.IsSuccess)
        {
            return BatchCaseExecution.Failure(EnsureFailure(
                simulation.Diagnostics,
                "SD.BATCH.SIMULATION_FAILED",
                "The SimpleDragon simulation did not produce a complete GRR result."));
        }

        GreenRetrofitResult result = simulation.Result!;
        var metrics = new SortedDictionary<string, double>(StringComparer.Ordinal)
        {
            ["carbon_gross"] = result.GrossSummaries[GreenRetrofitMetric.Carbon].AnnualTotal,
            ["carbon_per_m2"] = result.PerAreaSummaries[GreenRetrofitMetric.Carbon].AnnualTotal,
            ["cost_gross"] = result.GrossSummaries[GreenRetrofitMetric.Cost].AnnualTotal,
            ["cost_per_m2"] = result.PerAreaSummaries[GreenRetrofitMetric.Cost].AnnualTotal,
            ["site_energy_gross"] = result.GrossSummaries[GreenRetrofitMetric.SiteUses].AnnualTotal,
            ["site_energy_per_m2"] = result.PerAreaSummaries[GreenRetrofitMetric.SiteUses].AnnualTotal,
            ["source_energy_gross"] = result.GrossSummaries[GreenRetrofitMetric.SourceUses].AnnualTotal,
            ["source_energy_per_m2"] = result.PerAreaSummaries[GreenRetrofitMetric.SourceUses].AnnualTotal,
            ["total_area_m2"] = result.TotalArea,
        };
        return BatchCaseExecution.Success(metrics, simulation.Diagnostics);
    }

    private static IReadOnlyList<Diagnostic> EnsureFailure(
        IReadOnlyList<Diagnostic> diagnostics,
        string code,
        string message)
    {
        if (diagnostics.Any(item => item.IsFailure))
        {
            return diagnostics;
        }

        return diagnostics.Concat(new[]
        {
            new Diagnostic(code, DiagnosticSeverity.Error, message),
        }).ToArray();
    }
}
