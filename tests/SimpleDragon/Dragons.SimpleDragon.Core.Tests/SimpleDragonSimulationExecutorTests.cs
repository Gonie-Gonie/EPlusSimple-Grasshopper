using System.Globalization;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Results;
using Dragons.SimpleDragon.Batch;

namespace Dragons.SimpleDragon.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep the synthetic EnergyPlus result fixture readable.")]
public sealed class SimpleDragonSimulationExecutorTests
{
    private static readonly string[] Months =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    private static readonly string[] ElectricityHeadings =
    {
        "HEATING [kWh]",
        "COOLING [kWh]",
        "INTERIORLIGHTS [kWh]",
        "EXTERIORLIGHTS [kWh]",
        "INTERIOREQUIPMENT [kWh]",
        "FANS [kWh]",
        "PUMPS [kWh]",
        "HEATRECOVERY [kWh]",
        "WATERSYSTEMS [kWh]",
    };

    [Fact]
    public void ExecutionOptionsPreserveCompatibilityExceptForNativeHvacTopology()
    {
        EnergyModelIdfOptions options = SimpleDragonExecutionIdf.CreateOptions();

        Assert.False(options.ThrowOnValidationErrors);
        Assert.True(options.UseLegacyRectangularFenestration);
        Assert.True(options.UseLegacySimpleDragonScheduleMetadata);
        Assert.True(options.UseLegacySimpleDragonDefaultObjectFields);
        Assert.True(options.UseLegacySimpleDragonUsedProfileScheduleSelection);
        Assert.False(options.UseLegacySimpleDragonHvacTopology);
        Assert.True(options.UseLegacySimpleDragonVentilation);
        Assert.Equal(
            "energyplus-24.2-simpledragon-execution-v1",
            SimpleDragonSimulationExecutor.ExecutionProfileIdentity);
    }

    [Fact]
    public async Task SuccessfulExecutionUsesRuntimeIddAndReturnsOnlySimpleDragonResultData()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        var calls = new List<EnergyPlusRunRequest>();
        var runtimeRunner = new FakeEnergyPlusRunner((request, progress, _) =>
        {
            calls.Add(request);
            Assert.True(File.Exists(request.InputIdfPath));
            Assert.NotEmpty(File.ReadAllText(request.InputIdfPath));
            progress?.Report(new EnergyPlusRunTransition(
                EnergyPlusRunState.RunningEnergyPlus,
                DateTimeOffset.UtcNow,
                "Fake EnergyPlus is running."));
            return Task.FromResult(RuntimeResult(runtime, EnergyPlusRunState.Succeeded));
        });
        string? loadedIddPath = null;
        int loadCount = 0;
        var executor = new SimpleDragonSimulationExecutor(
            runtimeRunner,
            path =>
            {
                loadedIddPath = path;
                loadCount++;
                return Schema();
            },
            _ => SuccessfulSimulation());
        var transitions = new List<SimpleDragonSimulationTransition>();
        var progress = new InlineProgress<SimpleDragonSimulationTransition>(transitions.Add);

        SimpleDragonSimulationResult first = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path)
            {
                Timeout = TimeSpan.FromMinutes(7),
                MaximumCapturedArtifactBytes = 123456,
            },
            progress);
        SimpleDragonSimulationResult second = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path),
            progress);

        Assert.True(first.IsSuccess, Describe(first));
        Assert.NotNull(first.Result);
        Assert.True(second.IsSuccess, Describe(second));
        Assert.Equal(runtime.IddPath, loadedIddPath);
        Assert.Equal(1, loadCount);
        Assert.Equal(2, calls.Count);
        Assert.Equal(weatherPath, calls[0].WeatherFilePath);
        Assert.Equal(TimeSpan.FromMinutes(7), calls[0].Timeout);
        Assert.Equal(123456, calls[0].MaximumCapturedArtifactBytes);
        Assert.Equal(EnergyPlusCleanupPolicy.DeleteOnSuccess, calls[0].CleanupPolicy);
        Assert.Empty(Directory.GetFiles(directory.Path, "simpledragon-input-*.idf"));
        Assert.Contains(transitions, item => item.State == SimpleDragonSimulationState.ConvertingModel);
        Assert.Contains(transitions, item => item.State == SimpleDragonSimulationState.CompilingIdf);
        Assert.Contains(transitions, item => item.State == SimpleDragonSimulationState.RunningEnergyPlus);
        Assert.Contains(transitions, item => item.State == SimpleDragonSimulationState.ParsingResults);
        Assert.Contains(transitions, item => item.State == SimpleDragonSimulationState.BuildingResult);
        Assert.Equal(SimpleDragonSimulationState.Succeeded, transitions[^1].State);

        Type[] exposedTypes = typeof(SimpleDragonSimulationResult)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(property => Flatten(property.PropertyType))
            .ToArray();
        Assert.DoesNotContain(
            exposedTypes,
            type => type.Namespace?.StartsWith("Dragons.InvisibleDragon", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task CancelledRuntimeKeepsCancellationStateAndBatchTranslatesItToCancellation()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        EnergyPlusRunResult cancelledRuntime = RuntimeResult(
            runtime,
            EnergyPlusRunState.Cancelled,
            new EnergyPlusFailure(
                EnergyPlusFailureCategory.Cancelled,
                "RUN_CANCELLED",
                "The EnergyPlus run was cancelled."));
        var executor = new SimpleDragonSimulationExecutor(
            new FakeEnergyPlusRunner((_, _, _) => Task.FromResult(cancelledRuntime)),
            _ => Schema(),
            _ => throw new InvalidOperationException("A cancelled run must not be parsed."));

        SimpleDragonSimulationResult result = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path));

        Assert.False(result.IsSuccess);
        Assert.Equal(SimpleDragonSimulationState.Cancelled, result.State);
        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == "ENERGYPLUS.RUNTIME.RUN_CANCELLED");
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);

        var batchExecutor = new EnergyPlusBatchCaseExecutor(
            runtime,
            new FixedSimulationExecutor(result));
        BatchCaseContext context = Context(model, weatherPath, directory.Path);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => batchExecutor.ExecuteAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task RuntimeFailureRetainsStableFailureCodeWithoutExposingInternalPaths()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        string retained = Path.Combine(directory.Path, "retained-run");
        EnergyPlusRunResult failedRuntime = RuntimeResult(
            runtime,
            EnergyPlusRunState.Failed,
            new EnergyPlusFailure(
                EnergyPlusFailureCategory.RuntimeEnvironment,
                "RUN_PATH_UNREADABLE",
                "The temporary path is not accessible.",
                "Check per-user permissions."),
            retained);
        var executor = new SimpleDragonSimulationExecutor(
            new FakeEnergyPlusRunner((_, _, _) => Task.FromResult(failedRuntime)),
            _ => Schema(),
            _ => throw new InvalidOperationException("A failed run must not be parsed."));

        SimpleDragonSimulationResult result = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path));

        Assert.Equal(SimpleDragonSimulationState.Failed, result.State);
        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == "ENERGYPLUS.RUNTIME.RUN_PATH_UNREADABLE");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("The temporary path is not accessible.", diagnostic.Message);
        Assert.Contains("module-managed EnergyPlus runtime", diagnostic.SuggestedAction, StringComparison.Ordinal);
        Assert.DoesNotContain("Check per-user permissions.", diagnostic.SuggestedAction, StringComparison.Ordinal);
        Assert.DoesNotContain(retained, diagnostic.SuggestedAction, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchUsesSharedResultAndExecutionProfileForDeterministicMetrics()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        GreenRetrofitResult result = GreenRetrofitResultBuilder.Build(
            model,
            SuccessfulSimulation()).RequireResult();
        var simulation = new SimpleDragonSimulationResult(
            result,
            SimpleDragonSimulationState.Succeeded,
            Array.Empty<Diagnostic>());
        var executor = new EnergyPlusBatchCaseExecutor(
            runtime,
            new FixedSimulationExecutor(simulation),
            TimeSpan.FromMinutes(4),
            654321);

        BatchCaseExecution execution = await executor.ExecuteAsync(
            Context(model, weatherPath, directory.Path),
            CancellationToken.None);

        Assert.True(execution.Succeeded);
        Assert.Equal(9, execution.Metrics.Count);
        Assert.Equal(result.TotalArea, execution.Metrics["total_area_m2"]);
        Assert.Equal(
            result.GrossSummaries[GreenRetrofitMetric.SiteUses].AnnualTotal,
            execution.Metrics["site_energy_gross"]);
        Assert.Equal("Dragons.SimpleDragon.EnergyPlusBatchCaseExecutor/v2", executor.ExecutorIdentity);
        Assert.Contains(SimpleDragonSimulationExecutor.ExecutionProfileIdentity, executor.CanonicalExecutionOptions, StringComparison.Ordinal);
        Assert.Contains(SimpleDragonSimulationExecutor.ExecutionProfileIdentity, executor.CanonicalOutputOptions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreCancelledExecutionDoesNotCompileOrStartEnergyPlus()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        var runner = new FakeEnergyPlusRunner(
            (_, _, _) => throw new InvalidOperationException("EnergyPlus must not start."));
        var executor = new SimpleDragonSimulationExecutor(
            runner,
            _ => throw new InvalidOperationException("IDD must not load."),
            _ => throw new InvalidOperationException("Results must not parse."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        SimpleDragonSimulationResult result = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path),
            cancellationToken: cancellation.Token);

        Assert.Equal(SimpleDragonSimulationState.Cancelled, result.State);
        Assert.Equal("SD.SIMULATION.CANCELLED", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task CancellationRequestedDuringSchemaLoadStopsBeforeIdfCompilationAndEnergyPlus()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        using var cancellation = new CancellationTokenSource();
        var runner = new FakeEnergyPlusRunner(
            (_, _, _) => throw new InvalidOperationException("EnergyPlus must not start."));
        var executor = new SimpleDragonSimulationExecutor(
            runner,
            _ =>
            {
                cancellation.Cancel();
                return Schema();
            },
            _ => throw new InvalidOperationException("Results must not parse."));

        SimpleDragonSimulationResult result = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path),
            cancellationToken: cancellation.Token);

        Assert.Equal(SimpleDragonSimulationState.Cancelled, result.State);
        Assert.Equal("SD.SIMULATION.CANCELLED", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, runner.CallCount);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "simpledragon-input-*.idf"));
    }

    [Fact]
    public async Task CancellationRequestedByResultParserWinsBeforeResultBuilding()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        using var cancellation = new CancellationTokenSource();
        var executor = new SimpleDragonSimulationExecutor(
            new FakeEnergyPlusRunner(
                (_, _, _) => Task.FromResult(
                    RuntimeResult(runtime, EnergyPlusRunState.Succeeded))),
            _ => Schema(),
            _ =>
            {
                cancellation.Cancel();
                return SuccessfulSimulation();
            });
        var transitions = new List<SimpleDragonSimulationTransition>();

        SimpleDragonSimulationResult result = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path),
            new InlineProgress<SimpleDragonSimulationTransition>(transitions.Add),
            cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Result);
        Assert.Equal(SimpleDragonSimulationState.Cancelled, result.State);
        Assert.Equal("SD.SIMULATION.CANCELLED", Assert.Single(result.Diagnostics).Code);
        Assert.Contains(transitions, item => item.State == SimpleDragonSimulationState.ParsingResults);
        Assert.DoesNotContain(transitions, item => item.State == SimpleDragonSimulationState.BuildingResult);
        Assert.Equal(SimpleDragonSimulationState.Cancelled, transitions[^1].State);
    }

    [Fact]
    public async Task CancellationRequestedAtResultBuildingWinsBeforeSuccess()
    {
        using var directory = new TestDirectory();
        GreenRetrofitModel model = LoadModel();
        EnergyPlusRuntimeLayout runtime = Runtime(directory.Path);
        string weatherPath = directory.Write("weather.epw", "LOCATION,test");
        using var cancellation = new CancellationTokenSource();
        var executor = new SimpleDragonSimulationExecutor(
            new FakeEnergyPlusRunner(
                (_, _, _) => Task.FromResult(
                    RuntimeResult(runtime, EnergyPlusRunState.Succeeded))),
            _ => Schema(),
            _ => SuccessfulSimulation());
        var transitions = new List<SimpleDragonSimulationTransition>();
        var progress = new InlineProgress<SimpleDragonSimulationTransition>(transition =>
        {
            transitions.Add(transition);
            if (transition.State == SimpleDragonSimulationState.BuildingResult)
            {
                cancellation.Cancel();
            }
        });

        SimpleDragonSimulationResult result = await executor.ExecuteAsync(
            new SimpleDragonSimulationRequest(model, runtime, weatherPath, directory.Path),
            progress,
            cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Result);
        Assert.Equal(SimpleDragonSimulationState.Cancelled, result.State);
        Assert.Equal("SD.SIMULATION.CANCELLED", Assert.Single(result.Diagnostics).Code);
        Assert.Contains(transitions, item => item.State == SimpleDragonSimulationState.BuildingResult);
        Assert.DoesNotContain(transitions, item => item.State == SimpleDragonSimulationState.Succeeded);
        Assert.Equal(SimpleDragonSimulationState.Cancelled, transitions[^1].State);
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }

    private static BatchCaseContext Context(
        GreenRetrofitModel model,
        string weatherPath,
        string workRootPath) =>
        new(
            0,
            "case",
            model,
            weatherPath,
            new Dictionary<string, string>(),
            null!,
            workRootPath);

    private static EnergyPlusRuntimeLayout Runtime(string root)
    {
        EnergyPlusRuntimeManifest manifest = EnergyPlusRuntimeManifest.Supported;
        return (EnergyPlusRuntimeLayout)(Activator.CreateInstance(
            typeof(EnergyPlusRuntimeLayout),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[]
            {
                root,
                Path.Combine(root, "energyplus.exe"),
                Path.Combine(root, "ExpandObjects.exe"),
                Path.Combine(root, "Energy+.idd"),
                Path.Combine(root, "Energy+.schema.epJSON"),
                manifest,
                DateTimeOffset.UtcNow,
            },
            culture: CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Could not create the test runtime layout."));
    }

    private static IddSchema Schema() =>
        new(
            EnergyPlusRuntimeManifest.Supported.EnergyPlusVersion,
            EnergyPlusRuntimeManifest.Supported.EnergyPlusBuild,
            EnergyPlusRuntimeManifest.Supported.EnergyPlusIddSha256,
            Array.Empty<IddObjectDefinition>());

    private static EnergyPlusRunResult RuntimeResult(
        EnergyPlusRuntimeLayout runtime,
        EnergyPlusRunState state,
        EnergyPlusFailure? failure = null,
        string? workDirectory = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new EnergyPlusRunResult(
            "test-run",
            state,
            failure,
            runtime,
            workDirectory,
            workDirectory is not null,
            null,
            null,
            EnergyPlusOutputFiles.Empty,
            now,
            now,
            Array.Empty<EnergyPlusRunTransition>(),
            null);
    }

    private static EnergyPlusSimulationResult SuccessfulSimulation()
    {
        EnergyPlusTabularCell[] header = new[] { Text("Month") }
            .Concat(ElectricityHeadings.Select(Text))
            .ToArray();
        EnergyPlusTabularRow[] rows = Months
            .Select(month => new EnergyPlusTabularRow(
                new[] { Text(month) }
                    .Concat(ElectricityHeadings.Select(_ => Number(0d)))
                    .ToArray()))
            .ToArray();
        var table = new EnergyPlusTabularTable(
            "eplustbl.csv",
            "EndUseEnergyConsumptionElectricityMonthly",
            "Entire Facility",
            new[] { "EndUseEnergyConsumptionElectricityMonthly" },
            new EnergyPlusTabularRow(header),
            rows,
            isMonthly: true);
        return new EnergyPlusSimulationResult(
            EnergyPlusSimulationResult.CurrentSchema,
            new EnergyPlusResultMetadata(
                "test-run",
                EnergyPlusRunState.Succeeded,
                true,
                startedAtUtc: null,
                finishedAtUtc: null,
                runtimeElapsedSeconds: 1d,
                energyPlusProcessElapsedSeconds: null,
                workDirectory: null,
                workDirectoryRetained: false),
            new EnergyPlusErrorLog(
                null,
                null,
                null,
                Array.Empty<EnergyPlusDiagnostic>(),
                new EnergyPlusDiagnosticSummary(0, 0, 0, true, 1d)),
            new EnergyPlusAuditLog(null, null),
            new EnergyPlusBoundaryData(null, null),
            new[] { table },
            Array.Empty<EnergyPlusResultSource>());
    }

    private static EnergyPlusTabularCell Text(string value) => new(value, null);

    private static EnergyPlusTabularCell Number(double value) =>
        new(value.ToString(CultureInfo.InvariantCulture), value);

    private static GreenRetrofitModel LoadModel()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                "grm",
                "ASHRAE 140 modified.grm");
            if (File.Exists(candidate))
            {
                return GrmReader.ReadFile(candidate).RequireModel();
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GRM fixture.");
    }

    private static string Describe(SimpleDragonSimulationResult result) =>
        string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(item => item.Code + ": " + item.Message));

    private sealed class TestDirectory : IDisposable
    {
        internal TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Dragons",
                "Dragons",
                "temp",
                "simpledragon-simulation-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal string Write(string fileName, string content)
        {
            string path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FakeEnergyPlusRunner : ISimpleDragonEnergyPlusRunner
    {
        private readonly Func<
            EnergyPlusRunRequest,
            IProgress<EnergyPlusRunTransition>?,
            CancellationToken,
            Task<EnergyPlusRunResult>> _run;

        internal FakeEnergyPlusRunner(
            Func<
                EnergyPlusRunRequest,
                IProgress<EnergyPlusRunTransition>?,
                CancellationToken,
                Task<EnergyPlusRunResult>> run)
        {
            _run = run;
        }

        internal int CallCount { get; private set; }

        public Task<EnergyPlusRunResult> RunAsync(
            EnergyPlusRunRequest request,
            IProgress<EnergyPlusRunTransition>? progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _run(request, progress, cancellationToken);
        }
    }

    private sealed class FixedSimulationExecutor : ISimpleDragonSimulationExecutor
    {
        private readonly SimpleDragonSimulationResult _result;

        internal FixedSimulationExecutor(SimpleDragonSimulationResult result)
        {
            _result = result;
        }

        public Task<SimpleDragonSimulationResult> ExecuteAsync(
            SimpleDragonSimulationRequest request,
            IProgress<SimpleDragonSimulationTransition>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        internal InlineProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value)
        {
            _report(value);
        }
    }
}
