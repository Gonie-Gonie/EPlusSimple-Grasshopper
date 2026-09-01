using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Dragons.EnergyPlus.Runtime;

internal static class RuntimeFileSystem
{
    private const int BufferSize = 81920;

    internal static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A directory path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    internal static bool IsDescendantOf(string rootPath, string candidatePath)
    {
        var root = NormalizeDirectory(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    internal static string CombineUnder(string rootPath, params string[] relativeSegments)
    {
        if (relativeSegments is null || relativeSegments.Length == 0)
        {
            throw new ArgumentException("At least one relative path segment is required.", nameof(relativeSegments));
        }

        var combined = relativeSegments.Aggregate(rootPath, Path.Combine);
        var fullPath = Path.GetFullPath(combined);
        if (!IsDescendantOf(rootPath, fullPath))
        {
            throw new SecurityException("The resolved path escapes its permitted root.");
        }

        return fullPath;
    }

    internal static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        using var algorithm = SHA256.Create();
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            useAsync: true);

        var buffer = new byte[BufferSize];
        while (true)
        {
#if NET7_0_OR_GREATER
            var count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
            var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
            if (count == 0)
            {
                break;
            }

            algorithm.TransformBlock(buffer, 0, count, buffer, 0);
        }

        algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return ToLowerHex(algorithm.Hash ?? Array.Empty<byte>());
    }

    internal static async Task<string> ReadAllTextAsync(
        string path,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
#if NET7_0_OR_GREATER
        return await File.ReadAllTextAsync(path, encoding, cancellationToken).ConfigureAwait(false);
#else
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            BufferSize,
            useAsync: true);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return text;
#endif
    }

    private static string ToLowerHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}

internal sealed class SafeRunDirectory
{
    private const string MarkerFileName = ".dragons-energyplus-run";
    private const string RunPrefix = "run-";

    private SafeRunDirectory(string rootPath, string runId, string path)
    {
        RootPath = rootPath;
        RunId = runId;
        Path = path;
    }

    internal string RootPath { get; }

    internal string RunId { get; }

    internal string Path { get; }

    internal static SafeRunDirectory Create(string requestedRoot)
    {
        var root = RuntimeFileSystem.NormalizeDirectory(requestedRoot);
        if (File.Exists(root))
        {
            throw new IOException("The temporary root names an existing file.");
        }

        Directory.CreateDirectory(root);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var runId = Guid.NewGuid().ToString("N");
            var path = RuntimeFileSystem.CombineUnder(root, RunPrefix + runId);
            if (Directory.Exists(path) || File.Exists(path))
            {
                continue;
            }

            Directory.CreateDirectory(path);
            var markerPath = RuntimeFileSystem.CombineUnder(path, MarkerFileName);
            try
            {
                using var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var markerBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(runId);
                marker.Write(markerBytes, 0, markerBytes.Length);
                marker.Flush();
                return new SafeRunDirectory(root, runId, path);
            }
            catch (IOException)
            {
                // A collision or concurrent writer cannot turn an existing directory into our cleanup target.
            }
        }

        throw new IOException("A unique EnergyPlus work directory could not be created.");
    }

    internal void Delete()
    {
        var markerPath = RuntimeFileSystem.CombineUnder(Path, MarkerFileName);
        var directoryName = System.IO.Path.GetFileName(Path);
        if (!RuntimeFileSystem.IsDescendantOf(RootPath, Path)
            || !directoryName.StartsWith(RunPrefix, StringComparison.Ordinal)
            || !File.Exists(markerPath))
        {
            throw new SecurityException("Refusing to delete a directory that is not a marked EnergyPlus run directory.");
        }

        Directory.Delete(Path, recursive: true);
    }
}
