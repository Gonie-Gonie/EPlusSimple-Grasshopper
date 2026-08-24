using System.Security.Cryptography;
using System.Text;

namespace GonieGonie.EnergyPlus.Runtime.Tests;

internal sealed class TestDirectory : IDisposable
{
    internal TestDirectory()
    {
        var root = FindRepositoryRoot();
        Path = System.IO.Path.Combine(
            root,
            "temp",
            "tests",
            "energyplus-runtime",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string WriteFile(string relativePath, string content)
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    internal static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(System.IO.Path.Combine(directory.FullName, "Directory.Build.props")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be found for test isolation.");
    }
}

internal static class TestRuntimeFactory
{
    internal static async Task<(EnergyPlusRuntimeLayout Runtime, EnergyPlusRuntimeManifest Manifest)> CreateAsync(
        TestDirectory directory)
    {
        var runtimeRoot = System.IO.Path.Combine(directory.Path, "runtime");
        Directory.CreateDirectory(runtimeRoot);
        var energyPlus = WriteRuntimeFile(runtimeRoot, "energyplus.exe", "fake-energyplus");
        var expandObjects = WriteRuntimeFile(runtimeRoot, "ExpandObjects.exe", "fake-expandobjects");
        var idd = WriteRuntimeFile(runtimeRoot, "Energy+.idd", "fake-idd");
        var manifest = EnergyPlusRuntimeManifest.Supported with
        {
            EnergyPlusExecutableSha256 = Hash(energyPlus),
            ExpandObjectsSha256 = Hash(expandObjects),
            EnergyPlusIddSha256 = Hash(idd)
        };
        var resolution = await new RuntimeResolver(manifest).ResolveAsync(
            new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = runtimeRoot,
                SearchDefaultInstallLocation = false,
                SearchEnvironmentVariables = false
            });
        Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);
        return (resolution.Runtime!, manifest);
    }

    internal static string Hash(string path)
    {
        using var algorithm = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string WriteRuntimeFile(string root, string name, string content)
    {
        var path = System.IO.Path.Combine(root, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}

internal sealed class DelegateProcessExecutor : IProcessExecutor
{
    private readonly Func<ProcessExecutionRequest, CancellationToken, Task<ProcessExecutionResult>> execute;

    internal DelegateProcessExecutor(
        Func<ProcessExecutionRequest, CancellationToken, Task<ProcessExecutionResult>> execute)
    {
        this.execute = execute;
    }

    internal List<ProcessExecutionRequest> Requests { get; } = new();

    public Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return execute(request, cancellationToken);
    }

    internal static ProcessExecutionResult Exited(ProcessExecutionRequest request, int exitCode = 0)
    {
        var started = DateTimeOffset.UtcNow;
        return new ProcessExecutionResult(
            request.Stage,
            request.ExecutablePath,
            request.Arguments,
            1234,
            exitCode,
            ProcessTerminationReason.Exited,
            ProcessTreeKillRequested: false,
            "captured stdout" + Environment.NewLine,
            exitCode == 0 ? string.Empty : "captured stderr" + Environment.NewLine,
            started,
            DateTimeOffset.UtcNow);
    }

    internal static ProcessExecutionResult Cancelled(ProcessExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow;
        return new ProcessExecutionResult(
            request.Stage,
            request.ExecutablePath,
            request.Arguments,
            1234,
            null,
            ProcessTerminationReason.Cancelled,
            ProcessTreeKillRequested: true,
            string.Empty,
            string.Empty,
            started,
            DateTimeOffset.UtcNow);
    }
}
