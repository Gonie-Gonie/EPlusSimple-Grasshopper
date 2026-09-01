using System.Diagnostics;

namespace Dragons.EnergyPlus.Runtime.Tests;

public sealed class ProcessExecutorTests
{
    private static readonly string[] CaptureCommandArguments =
    {
        "/d",
        "/s",
        "/c",
        "echo stdout-line & echo stderr-line 1>&2 & exit /b 7"
    };

    private static readonly string[] LongRunningCommandArguments =
    {
        "/d",
        "/s",
        "/c",
        "ping 127.0.0.1 -n 30 >nul"
    };

    [Fact]
    public async Task CapturesStandardOutputAndErrorAsynchronously()
    {
        using var directory = new TestDirectory();
        var commandInterpreter = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var request = new ProcessExecutionRequest(
            EnergyPlusProcessStage.EnergyPlus,
            commandInterpreter,
            directory.Path,
            CaptureCommandArguments);

        var result = await new ProcessExecutor().RunAsync(request, CancellationToken.None);

        Assert.Equal(ProcessTerminationReason.Exited, result.TerminationReason);
        Assert.Equal(7, result.ExitCode);
        Assert.Contains("stdout-line", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stderr-line", result.StandardError, StringComparison.Ordinal);
        Assert.False(result.ProcessTreeKillRequested);
    }

    [Fact]
    public async Task CancellationKillsLongRunningProcessTree()
    {
        using var directory = new TestDirectory();
        var commandInterpreter = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var request = new ProcessExecutionRequest(
            EnergyPlusProcessStage.EnergyPlus,
            commandInterpreter,
            directory.Path,
            LongRunningCommandArguments);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var stopwatch = Stopwatch.StartNew();

        var result = await new ProcessExecutor().RunAsync(request, cancellationSource.Token);

        stopwatch.Stop();
        Assert.Equal(ProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.True(result.ProcessTreeKillRequested);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), stopwatch.Elapsed.ToString());
    }
}
