using System.Globalization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon.Batch;

/// <summary>
/// Production batch executor that converts GRM alternatives, runs the verified runtime, and emits GRR summaries.
/// </summary>
public sealed class EnergyPlusBatchCaseExecutor : IBatchCaseExecutor
{
    private readonly EnergyPlusRuntimeLayout _runtime;
    private readonly Lazy<IddSchema> _schema;
    private readonly TimeSpan _timeout;
    private readonly long _maximumCapturedArtifactBytes;

    public EnergyPlusBatchCaseExecutor(
        EnergyPlusRuntimeLayout runtime,
        TimeSpan? timeout = null,
        long maximumCapturedArtifactBytes = 64L * 1024L * 1024L)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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
        _schema = new Lazy<IddSchema>(
            () => IddParser.ParseFile(_runtime.IddPath),
            LazyThreadSafetyMode.ExecutionAndPublication);
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
                ["maximum_captured_artifact_bytes"] = maximumCapturedArtifactBytes.ToString(CultureInfo.InvariantCulture),
                ["timeout_seconds"] = _timeout.TotalSeconds.ToString("R", CultureInfo.InvariantCulture),
            });
        CanonicalOutputOptions = BatchDeterminism.CanonicalizeOptions(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["combined_metrics"] = "GRR annual gross and per-area summaries",
                ["idf_new_line"] = "LF",
                ["idf_schema_comments"] = "true",
            });
    }

    public string ExecutorIdentity => "GonieGonie.SimpleDragon.EnergyPlusBatchCaseExecutor/v1";

    public BatchRuntimeIdentity RuntimeIdentity { get; }

    public string CanonicalExecutionOptions { get; }

    public string CanonicalOutputOptions { get; }

    public async Task<BatchCaseExecution> ExecuteAsync(
        BatchCaseContext context,
        CancellationToken cancellationToken)
    {
        DomainSupport.NotNull(context, nameof(context));

        cancellationToken.ThrowIfCancellationRequested();
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(context.Model);
        if (!conversion.Success)
        {
            return BatchCaseExecution.Failure(EnsureFailure(
                conversion.Diagnostics,
                "SD.BATCH.CONVERSION_FAILED",
                "The GRM case could not be converted to an EnergyPlus model."));
        }

        Directory.CreateDirectory(context.WorkRootPath);
        string inputPath = Path.Combine(context.WorkRootPath, "input.idf");
        try
        {
            IdfDocument document = conversion.ToIdfDocument(_schema.Value);
            IdfWriter.WriteFile(
                inputPath,
                document,
                new IdfWriterOptions
                {
                    NewLine = "\n",
                    IncludeSchemaFieldComments = true,
                });
            var request = new EnergyPlusRunRequest(
                _runtime,
                inputPath,
                context.WeatherFilePath,
                context.WorkRootPath)
            {
                Timeout = _timeout,
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess,
                MaximumCapturedArtifactBytes = _maximumCapturedArtifactBytes,
            };
            EnergyPlusRunResult runtimeResult = await new EnergyPlusRunner()
                .RunAsync(request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!runtimeResult.IsSuccess)
            {
                if (runtimeResult.State == EnergyPlusRunState.Cancelled
                    || runtimeResult.Failure?.Category == EnergyPlusFailureCategory.Cancelled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                Diagnostic runtimeDiagnostic = RuntimeFailure(
                    runtimeResult.Failure,
                    runtimeResult.WorkDirectory);
                return BatchCaseExecution.Failure(
                    conversion.Diagnostics.Concat(new[] { runtimeDiagnostic }).ToArray());
            }

            EnergyPlusSimulationResult simulation = EnergyPlusResultParser.Parse(runtimeResult);
            GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(context.Model, simulation);
            Diagnostic[] diagnostics = conversion.Diagnostics
                .Concat(build.Diagnostics)
                .Concat(simulation.Diagnostics.Diagnostics)
                .Distinct()
                .ToArray();
            if (!build.Success || diagnostics.Any(item => item.IsFailure))
            {
                return BatchCaseExecution.Failure(EnsureFailure(
                    diagnostics,
                    "SD.BATCH.RESULT_FAILED",
                    "The EnergyPlus outputs could not be converted into a complete GRR result."));
            }

            GreenRetrofitResult result = build.RequireResult();
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
            return BatchCaseExecution.Success(metrics, diagnostics);
        }
        finally
        {
            try
            {
                File.Delete(inputPath);
            }
            catch (IOException)
            {
                // The isolated runner already copied the input; a locked staging file is harmless.
            }
            catch (UnauthorizedAccessException)
            {
                // The completed result remains valid when a staging file cannot be removed.
            }
        }
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

    private static Diagnostic RuntimeFailure(EnergyPlusFailure? failure, string? workDirectory)
    {
        if (failure is null)
        {
            return new Diagnostic(
                "SD.BATCH.ENERGYPLUS_FAILED",
                DiagnosticSeverity.Error,
                "EnergyPlus ended without a successful result or a structured failure.");
        }

        return new Diagnostic(
            "ENERGYPLUS.RUNTIME." + failure.Code,
            DiagnosticSeverity.Error,
            failure.Message,
            suggestedAction: FailureDetail(failure, workDirectory));
    }

    private static string? FailureDetail(EnergyPlusFailure failure, string? workDirectory)
    {
        string? detail = OptionalTrim(failure.Detail);
        string? retained = OptionalTrim(workDirectory);
        if (retained is null)
        {
            return detail;
        }

        string location = "Retained work directory: " + retained;
        return detail is null ? location : detail + Environment.NewLine + location;
    }

    private static string? OptionalTrim(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
