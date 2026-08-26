using System.Text;

namespace GonieGonie.EnergyPlus.Runtime.Tests;

public sealed class EnergyPlusRunnerTests
{
    [Fact]
    public async Task SuccessfulRunUsesUniqueSafeDirectoryAndCollectsStandardOutputs()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var input = directory.WriteFile("inputs/model.idf", "Version,24.2;");
        var weather = directory.WriteFile("inputs/weather.epw", "fake-weather");
        var tempRoot = System.IO.Path.Combine(directory.Path, "runs");
        var executor = CreateSuccessfulExecutor();
        var progress = new List<EnergyPlusRunTransition>();
        var runner = new EnergyPlusRunner(executor);
        var externalIddBytes = File.ReadAllBytes(runtime.IddPath);

        var result = await runner.RunAsync(
            new EnergyPlusRunRequest(runtime, input, weather, tempRoot),
            new InlineProgress(progress.Add));

        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.True(result.WorkDirectoryRetained);
        Assert.NotNull(result.WorkDirectory);
        Assert.True(RuntimeFileSystem.IsDescendantOf(tempRoot, result.WorkDirectory!));
        Assert.Equal(2, executor.Requests.Count);
        Assert.NotNull(result.ExpandObjectsProcess);
        Assert.NotNull(result.EnergyPlusProcess);
        Assert.Equal("warning text", result.Outputs.Error?.TextContent);
        Assert.Equal("audit text", result.Outputs.Audit?.TextContent);
        Assert.Equal("boundary text", result.Outputs.Boundary?.TextContent);
        Assert.Equal("header,value\nrow,1", NormalizeNewlines(result.Outputs.TableCsv?.TextContent));
        Assert.All(result.Outputs.Available, artifact =>
            Assert.True(RuntimeFileSystem.IsDescendantOf(result.WorkDirectory!, artifact.FullPath)));
        Assert.Equal(
            new[]
            {
                EnergyPlusRunState.Pending,
                EnergyPlusRunState.Validating,
                EnergyPlusRunState.Preparing,
                EnergyPlusRunState.ExpandingObjects,
                EnergyPlusRunState.RunningEnergyPlus,
                EnergyPlusRunState.CollectingResults,
                EnergyPlusRunState.Succeeded
            },
            progress.Select(item => item.State));

        var energyRequest = executor.Requests.Single(item => item.Stage == EnergyPlusProcessStage.EnergyPlus);
        var workDirectory = result.WorkDirectory!;
        var localIddPath = System.IO.Path.Combine(workDirectory, "Energy+.idd");
        Assert.True(File.Exists(System.IO.Path.Combine(workDirectory, ".goniegonie-energyplus-run")));
        Assert.True(RuntimeFileSystem.IsDescendantOf(workDirectory, localIddPath));
        Assert.NotEqual(runtime.IddPath, localIddPath);
        Assert.Equal(externalIddBytes, File.ReadAllBytes(localIddPath));
        Assert.Equal(externalIddBytes, File.ReadAllBytes(runtime.IddPath));
        Assert.Equal(workDirectory, energyRequest.WorkingDirectory);
        Assert.Equal(
            new[]
            {
                "-i",
                localIddPath,
                "-d",
                System.IO.Path.Combine(workDirectory, "output"),
                "-p",
                "eplus",
                "-s",
                "L",
                "-w",
                System.IO.Path.Combine(workDirectory, "in.epw"),
                System.IO.Path.Combine(workDirectory, "expanded.idf")
            },
            energyRequest.Arguments);
    }

    [Fact]
    public async Task DeleteOnSuccessRemovesOnlyRunDirectoryButKeepsCapturedResults()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var input = directory.WriteFile("model.idf", "Version,24.2;");
        var runner = new EnergyPlusRunner(CreateSuccessfulExecutor());

        var result = await runner.RunAsync(new EnergyPlusRunRequest(
            runtime,
            input,
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "runs"))
        {
            CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess
        });

        Assert.True(result.IsSuccess);
        Assert.False(result.WorkDirectoryRetained);
        Assert.False(Directory.Exists(result.WorkDirectory));
        Assert.Equal("audit text", result.Outputs.Audit?.TextContent);
        Assert.Null(result.CleanupError);
    }

    [Fact]
    public async Task DeleteOnSuccessRetainsFailedRunForDiagnosis()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var input = directory.WriteFile("model.idf", "Version,24.2;");
        var executor = new DelegateProcessExecutor((request, _) =>
            Task.FromResult(DelegateProcessExecutor.Exited(request, exitCode: 9)));

        var result = await new EnergyPlusRunner(executor).RunAsync(new EnergyPlusRunRequest(
            runtime,
            input,
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "runs"))
        {
            CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess
        });

        Assert.Equal(EnergyPlusRunState.Failed, result.State);
        Assert.Equal(EnergyPlusFailureCategory.ProcessFailure, result.Failure?.Category);
        Assert.Equal("EXPANDOBJECTS_FAILED", result.Failure?.Code);
        Assert.True(result.WorkDirectoryRetained);
        Assert.True(Directory.Exists(result.WorkDirectory));
    }

    [Fact]
    public async Task MissingInputIsReportedAsCallerErrorWithoutStartingAProcess()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var executor = CreateSuccessfulExecutor();

        var result = await new EnergyPlusRunner(executor).RunAsync(new EnergyPlusRunRequest(
            runtime,
            System.IO.Path.Combine(directory.Path, "missing.idf"),
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "runs")));

        Assert.Equal(EnergyPlusRunState.Failed, result.State);
        Assert.Equal(EnergyPlusFailureCategory.UserInput, result.Failure?.Category);
        Assert.Equal("INPUT_IDF_NOT_FOUND", result.Failure?.Code);
        Assert.Empty(executor.Requests);
        Assert.Null(result.WorkDirectory);
    }

    [Fact]
    public async Task InternalExecutorExceptionIsNotMisreportedAsCallerError()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var input = directory.WriteFile("model.idf", "Version,24.2;");
        var executor = new DelegateProcessExecutor((_, _) =>
            throw new InvalidOperationException("synthetic library fault"));

        var result = await new EnergyPlusRunner(executor).RunAsync(new EnergyPlusRunRequest(
            runtime,
            input,
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "runs")));

        Assert.Equal(EnergyPlusFailureCategory.Internal, result.Failure?.Category);
        Assert.Equal("RUN_INTERNAL_ERROR", result.Failure?.Code);
        Assert.Contains("InvalidOperationException", result.Failure?.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimeoutAndCallerCancellationHaveDistinctStructuredStates()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var input = directory.WriteFile("model.idf", "Version,24.2;");

        var timeoutExecutor = CreateCancellableExecutor();
        var timedOut = await new EnergyPlusRunner(timeoutExecutor).RunAsync(new EnergyPlusRunRequest(
            runtime,
            input,
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "timeout-runs"))
        {
            Timeout = TimeSpan.FromMilliseconds(100),
            CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways
        });

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var cancelled = await new EnergyPlusRunner(CreateCancellableExecutor()).RunAsync(new EnergyPlusRunRequest(
            runtime,
            input,
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "cancel-runs"))
        {
            Timeout = TimeSpan.FromMinutes(1),
            CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways
        }, cancellationSource.Token);

        Assert.Equal(EnergyPlusRunState.TimedOut, timedOut.State);
        Assert.Equal(EnergyPlusFailureCategory.Timeout, timedOut.Failure?.Category);
        Assert.False(timedOut.WorkDirectoryRetained);
        Assert.Equal(EnergyPlusRunState.Cancelled, cancelled.State);
        Assert.Equal(EnergyPlusFailureCategory.Cancelled, cancelled.Failure?.Category);
        Assert.False(cancelled.WorkDirectoryRetained);
    }

    [Fact]
    public async Task OversizedArtifactIsHashedWithoutBeingLoadedIntoMemory()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var input = directory.WriteFile("model.idf", "Version,24.2;");

        var result = await new EnergyPlusRunner(CreateSuccessfulExecutor()).RunAsync(new EnergyPlusRunRequest(
            runtime,
            input,
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "runs"))
        {
            MaximumCapturedArtifactBytes = 5
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Outputs.TableCsv);
        Assert.False(result.Outputs.TableCsv!.ContentCaptured);
        Assert.Null(result.Outputs.TableCsv.TextContent);
        Assert.Equal(64, result.Outputs.TableCsv.Sha256.Length);
    }

    [Fact]
    public async Task ExpandObjectsMaySucceedWithoutProducingExpandedIdf()
    {
        using var directory = new TestDirectory();
        var (runtime, _) = await TestRuntimeFactory.CreateAsync(directory);
        var input = directory.WriteFile("model.idf", "Version,24.2;");
        var executor = CreateSuccessfulExecutor(createExpandedIdf: false);

        var result = await new EnergyPlusRunner(executor).RunAsync(new EnergyPlusRunRequest(
            runtime,
            input,
            WeatherFilePath: null,
            System.IO.Path.Combine(directory.Path, "runs")));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var energyRequest = executor.Requests.Single(item => item.Stage == EnergyPlusProcessStage.EnergyPlus);
        Assert.Equal(System.IO.Path.Combine(result.WorkDirectory!, "in.idf"), energyRequest.Arguments[^1]);
    }

    private static DelegateProcessExecutor CreateSuccessfulExecutor(bool createExpandedIdf = true)
    {
        return new DelegateProcessExecutor((request, _) =>
        {
            if (request.Stage == EnergyPlusProcessStage.ExpandObjects)
            {
                if (createExpandedIdf)
                {
                    File.Copy(
                        System.IO.Path.Combine(request.WorkingDirectory, "in.idf"),
                        System.IO.Path.Combine(request.WorkingDirectory, "expanded.idf"));
                }
            }
            else
            {
                var arguments = request.Arguments.ToList();
                var outputDirectory = arguments[arguments.IndexOf("-d") + 1];
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(System.IO.Path.Combine(outputDirectory, "eplusout.err"), "warning text");
                File.WriteAllText(System.IO.Path.Combine(outputDirectory, "eplusout.audit"), "audit text");
                File.WriteAllText(System.IO.Path.Combine(outputDirectory, "eplusout.bnd"), "boundary text");
                File.WriteAllText(
                    System.IO.Path.Combine(outputDirectory, "eplustbl.csv"),
                    "header,value" + Environment.NewLine + "row,1",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            return Task.FromResult(DelegateProcessExecutor.Exited(request));
        });
    }

    private static DelegateProcessExecutor CreateCancellableExecutor()
    {
        return new DelegateProcessExecutor(async (request, cancellationToken) =>
        {
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return DelegateProcessExecutor.Cancelled(request);
            }

            throw new InvalidOperationException("The infinite delay unexpectedly completed.");
        });
    }

    private static string? NormalizeNewlines(string? value)
    {
        return value?.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class InlineProgress : IProgress<EnergyPlusRunTransition>
    {
        private readonly Action<EnergyPlusRunTransition> report;

        internal InlineProgress(Action<EnergyPlusRunTransition> report)
        {
            this.report = report;
        }

        public void Report(EnergyPlusRunTransition value)
        {
            report(value);
        }
    }
}
