using System.IO.Compression;
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

    internal static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string WriteRuntimeFile(string root, string name, string content)
    {
        var path = System.IO.Path.Combine(root, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}

internal sealed record TestRuntimeArchive(
    string ArchivePath,
    EnergyPlusRuntimeManifest Manifest,
    EnergyPlusRuntimeDistribution Distribution);

internal static class TestRuntimeArchiveFactory
{
    private static readonly byte[] EnergyPlusBytes = Encoding.UTF8.GetBytes("fake-energyplus-bootstrap");
    private static readonly byte[] ExpandObjectsBytes = Encoding.UTF8.GetBytes("fake-expandobjects-bootstrap");
    private static readonly byte[] IddBytes = Encoding.UTF8.GetBytes("fake-idd-bootstrap");

    internal static TestRuntimeArchive Create(
        TestDirectory directory,
        bool includeUnsafeTraversal = false)
    {
        var archivePath = System.IO.Path.Combine(directory.Path, "fake-energyplus.zip");
        using (var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            if (includeUnsafeTraversal)
            {
                WriteEntry(archive, "../escaped.txt", Encoding.UTF8.GetBytes("must-not-escape"));
            }

            const string archiveRoot = "EnergyPlus-24.2.0-94a887817b-Windows-x86_64/";
            WriteEntry(archive, archiveRoot + "energyplus.exe", EnergyPlusBytes);
            WriteEntry(archive, archiveRoot + "ExpandObjects.exe", ExpandObjectsBytes);
            WriteEntry(archive, archiveRoot + "Energy+.idd", IddBytes);
            WriteEntry(archive, archiveRoot + "ExampleFiles/example.idf", Encoding.UTF8.GetBytes("Version,24.2;"));
        }

        var manifest = EnergyPlusRuntimeManifest.Supported with
        {
            EnergyPlusArchiveSha256 = TestRuntimeFactory.Hash(archivePath),
            EnergyPlusArchiveSize = new FileInfo(archivePath).Length,
            EnergyPlusExecutableSha256 = TestRuntimeFactory.Hash(EnergyPlusBytes),
            ExpandObjectsSha256 = TestRuntimeFactory.Hash(ExpandObjectsBytes),
            EnergyPlusIddSha256 = TestRuntimeFactory.Hash(IddBytes)
        };
        var distribution = new EnergyPlusRuntimeDistribution(
            new Uri("https://example.invalid/fake-energyplus.zip", UriKind.Absolute),
            manifest);
        return new TestRuntimeArchive(archivePath, manifest, distribution);
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}

internal sealed class DelegateRuntimeArchiveDownloader : IEnergyPlusRuntimeArchiveDownloader
{
    private readonly Func<string, CancellationToken, Task> download;
    private int callCount;

    internal DelegateRuntimeArchiveDownloader(Func<string, CancellationToken, Task> download)
    {
        this.download = download;
    }

    internal int CallCount => Volatile.Read(ref callCount);

    public async Task DownloadAsync(
        Uri sourceUri,
        string destinationPartialPath,
        IProgress<EnergyPlusRuntimeDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref callCount);
        await download(destinationPartialPath, cancellationToken);
        if (File.Exists(destinationPartialPath))
        {
            var length = new FileInfo(destinationPartialPath).Length;
            progress?.Report(new EnergyPlusRuntimeDownloadProgress(length, length));
        }
    }

    internal static DelegateRuntimeArchiveDownloader Copying(
        string archivePath,
        TimeSpan? delay = null)
    {
        return new DelegateRuntimeArchiveDownloader(async (destination, cancellationToken) =>
        {
            if (delay is not null)
            {
                await Task.Delay(delay.Value, cancellationToken);
            }

            using var source = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous);
            using var target = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        });
    }
}

internal sealed class CollectingProgress<T> : IProgress<T>
{
    internal List<T> Updates { get; } = new();

    public void Report(T value)
    {
        Updates.Add(value);
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
