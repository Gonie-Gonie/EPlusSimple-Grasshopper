using System.Diagnostics;
using System.Text;

namespace Dragons.EnergyPlus.Runtime;

public enum EnergyPlusProcessStage
{
    ExpandObjects,
    EnergyPlus
}

public enum ProcessTerminationReason
{
    Exited,
    Cancelled
}

/// <summary>
/// Captured output and termination data for one child process stage.
/// </summary>
public sealed record ProcessExecutionResult(
    EnergyPlusProcessStage Stage,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    int? ProcessId,
    int? ExitCode,
    ProcessTerminationReason TerminationReason,
    bool ProcessTreeKillRequested,
    string StandardOutput,
    string StandardError,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc)
{
    public TimeSpan Elapsed => FinishedAtUtc - StartedAtUtc;

    public bool Succeeded => TerminationReason == ProcessTerminationReason.Exited && ExitCode == 0;
}

internal sealed record ProcessExecutionRequest(
    EnergyPlusProcessStage Stage,
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments);

internal interface IProcessExecutor
{
    Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken);
}

internal sealed class ProcessExecutor : IProcessExecutor
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public async Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var copiedArguments = request.Arguments.ToArray();
        if (cancellationToken.IsCancellationRequested)
        {
            return new ProcessExecutionResult(
                request.Stage,
                request.ExecutablePath,
                copiedArguments,
                null,
                null,
                ProcessTerminationReason.Cancelled,
                ProcessTreeKillRequested: false,
                string.Empty,
                string.Empty,
                startedAtUtc,
                DateTimeOffset.UtcNow);
        }

        using var process = new Process();
        process.StartInfo = CreateStartInfo(request);
        process.EnableRaisingEvents = true;

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        var outputCompleted = NewCompletionSource();
        var errorCompleted = NewCompletionSource();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                outputCompleted.TrySetResult(null);
                return;
            }

            lock (standardOutput)
            {
                standardOutput.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                errorCompleted.TrySetResult(null);
                return;
            }

            lock (standardError)
            {
                standardError.AppendLine(eventArgs.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"The {request.Stage} process did not start.");
        }

        var processId = process.Id;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exitTask = WaitForExitSignalAsync(process);
        var cancellationCompletion = NewCompletionSource();
        CancellationTokenRegistration registration = default;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(
                state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
                cancellationCompletion);
        }

        var killRequested = false;
        var terminationReason = ProcessTerminationReason.Exited;
        try
        {
            var completed = await Task.WhenAny(exitTask, cancellationCompletion.Task).ConfigureAwait(false);
            if (completed == cancellationCompletion.Task && !exitTask.IsCompleted)
            {
                terminationReason = ProcessTerminationReason.Cancelled;
                killRequested = TryKillProcessTree(process);
                await exitTask.ConfigureAwait(false);
            }

            // This overload also drains the asynchronous output events after the Exited event fires.
            process.WaitForExit();
            await Task.WhenAll(outputCompleted.Task, errorCompleted.Task).ConfigureAwait(false);
        }
        finally
        {
            registration.Dispose();
        }

        int? exitCode;
        try
        {
            exitCode = process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            exitCode = null;
        }

        string capturedOutput;
        string capturedError;
        lock (standardOutput)
        {
            capturedOutput = standardOutput.ToString();
        }

        lock (standardError)
        {
            capturedError = standardError.ToString();
        }

        return new ProcessExecutionResult(
            request.Stage,
            request.ExecutablePath,
            copiedArguments,
            processId,
            exitCode,
            terminationReason,
            killRequested,
            capturedOutput,
            capturedError,
            startedAtUtc,
            DateTimeOffset.UtcNow);
    }

    private static ProcessStartInfo CreateStartInfo(ProcessExecutionRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom
        };

#if NET8_0_OR_GREATER
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["LANG"] = "C";
        startInfo.Environment["LC_ALL"] = "C";
#else
        startInfo.Arguments = WindowsCommandLine.Join(request.Arguments);
        startInfo.EnvironmentVariables["LANG"] = "C";
        startInfo.EnvironmentVariables["LC_ALL"] = "C";
#endif
        return startInfo;
    }

    private static Task WaitForExitSignalAsync(Process process)
    {
        if (process.HasExited)
        {
            return Task.CompletedTask;
        }

        var completion = NewCompletionSource();
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            process.Exited -= handler;
            completion.TrySetResult(null);
        };
        process.Exited += handler;

        if (process.HasExited)
        {
            process.Exited -= handler;
            completion.TrySetResult(null);
        }

        return completion.Task;
    }

    private static bool TryKillProcessTree(Process process)
    {
        if (process.HasExited)
        {
            return false;
        }

#if NET8_0_OR_GREATER
        try
        {
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return TryKillSingleProcess(process);
        }
#else
        try
        {
            using var taskKill = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/PID " + process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + " /T /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (taskKill is not null && taskKill.WaitForExit(10000) && taskKill.ExitCode == 0)
            {
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Fall back to killing the direct child when taskkill is unavailable.
        }

        return TryKillSingleProcess(process);
#endif
    }

    private static bool TryKillSingleProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }

            process.Kill();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static TaskCompletionSource<object?> NewCompletionSource()
    {
        return new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal static class WindowsCommandLine
{
    internal static string Join(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(Quote));
    }

    internal static string Quote(string argument)
    {
        if (argument.Length != 0
            && argument.All(character => !char.IsWhiteSpace(character) && character != '"'))
        {
            return argument;
        }

        var result = new StringBuilder();
        result.Append('"');
        var backslashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                result.Append('\\', (backslashCount * 2) + 1);
                result.Append('"');
                backslashCount = 0;
                continue;
            }

            result.Append('\\', backslashCount);
            backslashCount = 0;
            result.Append(character);
        }

        result.Append('\\', backslashCount * 2);
        result.Append('"');
        return result.ToString();
    }
}
