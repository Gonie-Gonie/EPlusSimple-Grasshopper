using System.IO.Compression;
using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using Dragons.BuildingEnergy.Contracts;

namespace Dragons.SimpleDragon;

/// <summary>
/// Pins the immutable weather archive shipped beside SimpleDragon packages.
/// </summary>
public sealed record SimpleDragonWeatherPackManifest
{
    public const string SupportedPackId = "korean-tmy-v1";
    public const string SupportedArchiveFileName = "KoreanTMY-v1.zip";
    public const long SupportedArchiveSize = 128349513;
    public const string SupportedArchiveSha256 =
        "fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0";

    [SuppressMessage(
        "Maintainability",
        "CA1512:Use ArgumentOutOfRangeException throw helper",
        Justification = "The shared net48/net7 API surface cannot call the net8-only throw helper.")]
    public SimpleDragonWeatherPackManifest(
        string packId,
        string archiveFileName,
        long archiveSize,
        string archiveSha256)
    {
        PackId = RequiredSafeSegment(packId, nameof(packId));
        ArchiveFileName = RequiredSafeFileName(archiveFileName, nameof(archiveFileName));
        if (archiveSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(archiveSize));
        }

        string normalizedHash = archiveSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedHash.Length != 64 || normalizedHash.Any(character => !IsHex(character)))
        {
            throw new ArgumentException("The weather archive SHA-256 must contain 64 hexadecimal characters.", nameof(archiveSha256));
        }

        ArchiveSize = archiveSize;
        ArchiveSha256 = normalizedHash;
    }

    public static SimpleDragonWeatherPackManifest Supported { get; } = new(
        SupportedPackId,
        SupportedArchiveFileName,
        SupportedArchiveSize,
        SupportedArchiveSha256);

    public string PackId { get; }

    public string ArchiveFileName { get; }

    public long ArchiveSize { get; }

    public string ArchiveSha256 { get; }

    private static string RequiredSafeSegment(string? value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0
            || normalized is "." or ".."
            || Path.IsPathRooted(normalized)
            || normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || normalized.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
        {
            throw new ArgumentException("A single non-empty path segment is required.", parameterName);
        }

        return normalized;
    }

    private static string RequiredSafeFileName(string? value, string parameterName)
    {
        string normalized = RequiredSafeSegment(value, parameterName);
        if (!string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException("A single archive filename is required.", parameterName);
        }

        return normalized;
    }

    private static bool IsHex(char value) =>
        value is >= '0' and <= '9'
        || value is >= 'a' and <= 'f';
}

/// <summary>
/// Overrides package discovery or the per-user extraction cache.
/// </summary>
public sealed record SimpleDragonWeatherPackOptions
{
    public string? ArchivePath { get; init; }

    public string? CacheRoot { get; init; }

    public TimeSpan LockWaitTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan LockRetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
}

/// <summary>
/// Non-throwing result of resolving one address-selected EPW from the packaged archive.
/// </summary>
public sealed record SimpleDragonWeatherFileResolution(
    string? FilePath,
    string? ArchivePath,
    bool Extracted,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsSuccess => FilePath is not null && Diagnostics.All(item => !item.IsFailure);
}

/// <summary>
/// Verifies the pinned SimpleDragon weather pack and atomically extracts only the selected EPW
/// into a per-user cache. No Rhino installation directory is ever used as a write target.
/// </summary>
public sealed class SimpleDragonWeatherPackResolver
{
    private readonly SimpleDragonWeatherPackManifest _manifest;

    public SimpleDragonWeatherPackResolver()
        : this(SimpleDragonWeatherPackManifest.Supported)
    {
    }

    public SimpleDragonWeatherPackResolver(SimpleDragonWeatherPackManifest manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    [SuppressMessage(
        "Maintainability",
        "CA1510:Use ArgumentNullException throw helper",
        Justification = "The shared net48/net7 API surface cannot call the net6-only throw helper.")]
    public SimpleDragonWeatherFileResolution Resolve(
        WeatherSelection selection,
        SimpleDragonWeatherPackOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (selection is null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        return ResolveMany(new[] { selection }, options, cancellationToken)[0];
    }

    /// <summary>
    /// Resolves several address-selected EPWs while verifying the immutable archive only once.
    /// Results preserve input order, and repeated EPW filenames reuse the same verified result.
    /// </summary>
    [SuppressMessage(
        "Maintainability",
        "CA1510:Use ArgumentNullException throw helper",
        Justification = "The shared net48/net7 API surface cannot call the net6-only throw helper.")]
    public IReadOnlyList<SimpleDragonWeatherFileResolution> ResolveMany(
        IReadOnlyList<WeatherSelection> selections,
        SimpleDragonWeatherPackOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (selections is null)
        {
            throw new ArgumentNullException(nameof(selections));
        }

        if (selections.Count == 0)
        {
            return Array.Empty<SimpleDragonWeatherFileResolution>();
        }

        for (int index = 0; index < selections.Count; index++)
        {
            if (selections[index] is null)
            {
                throw new ArgumentException(
                    "Weather selections must not contain null items.",
                    nameof(selections));
            }
        }

        options ??= new SimpleDragonWeatherPackOptions();
        try
        {
            ValidateOptions(options);
            cancellationToken.ThrowIfCancellationRequested();
            string? archivePath = ResolveArchivePath(options.ArchivePath);
            if (archivePath is null)
            {
                return RepeatFailure(selections.Count, Failed(
                    "SD.WEATHER.PACK_NOT_FOUND",
                    "The packaged SimpleDragon Korean weather archive was not found.",
                    "Run 'dev.cmd setup' and reinstall the SimpleDragon package, or supply an explicit archive path."));
            }

            string cacheRoot = ResolveCacheRoot(options.CacheRoot);
            EnsureSafeDirectory(cacheRoot);
            string lockPath = CombineUnder(cacheRoot, ".weather-pack.lock");

            using FileStream installLock = AcquireLock(lockPath, options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            using var archiveStream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.SequentialScan);
            VerifyArchive(archiveStream, archivePath, cancellationToken);
            archiveStream.Position = 0;
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            var byFileName = new Dictionary<string, SimpleDragonWeatherFileResolution>(
                StringComparer.Ordinal);
            var results = new SimpleDragonWeatherFileResolution[selections.Count];
            for (int index = 0; index < selections.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string epwFileName = RequireSafeEpwFileName(selections[index].EpwFileName);
                if (!byFileName.TryGetValue(epwFileName, out SimpleDragonWeatherFileResolution? result))
                {
                    result = ResolveVerifiedEntry(
                        archive,
                        archivePath,
                        cacheRoot,
                        epwFileName,
                        cancellationToken);
                    byFileName.Add(epwFileName, result);
                }

                results[index] = result;
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            return RepeatFailure(selections.Count, Failed(
                "SD.WEATHER.EXTRACTION_CANCELLED",
                "SimpleDragon weather extraction was cancelled.",
                null,
                DiagnosticSeverity.Info));
        }
        catch (TimeoutException exception)
        {
            return RepeatFailure(selections.Count, Failed(
                "SD.WEATHER.LOCK_TIMEOUT",
                "Timed out waiting for another SimpleDragon weather extraction.",
                exception.Message));
        }
        catch (InvalidDataException exception)
        {
            return RepeatFailure(selections.Count, Failed(
                "SD.WEATHER.PACK_INTEGRITY_FAILED",
                "The packaged SimpleDragon weather archive failed integrity validation.",
                exception.Message));
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException)
        {
            return RepeatFailure(selections.Count, Failed(
                "SD.WEATHER.EXTRACTION_FAILED",
                "The selected SimpleDragon weather file could not be prepared in the per-user cache.",
                exception.Message));
        }
    }

    private static SimpleDragonWeatherFileResolution ResolveVerifiedEntry(
        ZipArchive archive,
        string archivePath,
        string cacheRoot,
        string epwFileName,
        CancellationToken cancellationToken)
    {
        string targetPath = CombineUnder(cacheRoot, epwFileName);
        EnsureNotReparsePoint(targetPath, "weather cache target");
        ZipArchiveEntry entry = ResolveEntry(archive, epwFileName);
        byte[] entryHash = ComputeHash(entry, cancellationToken);

        if (File.Exists(targetPath)
            && ExistingFileMatches(targetPath, entry.Length, entryHash, cancellationToken))
        {
            ValidateEpwHeader(targetPath);
            return Succeeded(targetPath, archivePath, extracted: false);
        }

        string partialPath = CombineUnder(
            cacheRoot,
            "." + epwFileName + "." + Guid.NewGuid().ToString("N") + ".partial");
        string backupPath = CombineUnder(
            cacheRoot,
            "." + epwFileName + "." + Guid.NewGuid().ToString("N") + ".backup");
        try
        {
            EnsureNotReparsePoint(partialPath, "weather extraction partial file");
            using (Stream source = entry.Open())
            using (var destination = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.SequentialScan))
            {
                CopyWithCancellation(source, destination, cancellationToken);
                destination.Flush(true);
            }

            if (!ExistingFileMatches(partialPath, entry.Length, entryHash, cancellationToken))
            {
                throw new InvalidDataException("The extracted EPW did not match its verified archive entry.");
            }

            ValidateEpwHeader(partialPath);
            if (File.Exists(targetPath))
            {
                File.Replace(partialPath, targetPath, backupPath, ignoreMetadataErrors: true);
                TryDeleteTemporary(cacheRoot, backupPath);
            }
            else
            {
                File.Move(partialPath, targetPath);
            }

            return Succeeded(targetPath, archivePath, extracted: true);
        }
        finally
        {
            TryDeleteTemporary(cacheRoot, partialPath);
            TryDeleteTemporary(cacheRoot, backupPath);
        }
    }

    internal string? ResolveArchivePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            string fullPath = Path.GetFullPath(explicitPath!.Trim());
            return File.Exists(fullPath) ? fullPath : null;
        }

        string assemblyLocation = typeof(SimpleDragonWeatherPackResolver).Assembly.Location;
        string? startDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(assemblyLocation));
        for (int depth = 0; startDirectory is not null && depth <= 8; depth++)
        {
            foreach (string relativePath in new[]
            {
                Path.Combine("runtime", "weather", _manifest.ArchiveFileName),
                Path.Combine(".tools", "distributions", "weather", _manifest.ArchiveFileName),
            })
            {
                string candidate = Path.Combine(startDirectory, relativePath);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            startDirectory = Directory.GetParent(startDirectory)?.FullName;
        }

        return null;
    }

    private void VerifyArchive(FileStream stream, string path, CancellationToken cancellationToken)
    {
        if (stream.Length != _manifest.ArchiveSize)
        {
            throw new InvalidDataException(
                "Weather archive size mismatch. Expected " + _manifest.ArchiveSize
                + "; actual " + stream.Length + "; path " + path + ".");
        }

        string actualHash = ComputeHashHex(stream, cancellationToken);
        if (!string.Equals(actualHash, _manifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Weather archive SHA-256 mismatch. Expected " + _manifest.ArchiveSha256
                + "; actual " + actualHash + ".");
        }

    }

    private static ZipArchiveEntry ResolveEntry(ZipArchive archive, string epwFileName)
    {
        ZipArchiveEntry[] matches = archive.Entries
            .Where(item => !string.IsNullOrEmpty(item.Name)
                && string.Equals(item.FullName.Replace('\\', '/'), epwFileName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                matches.Length == 0
                    ? "The address-selected EPW is absent from the verified weather archive: " + epwFileName + "."
                    : "The address-selected EPW occurs more than once in the verified weather archive: " + epwFileName + ".");
        }

        ZipArchiveEntry entry = matches[0];
        if (entry.Length <= 0 || entry.Length > 16L * 1024L * 1024L)
        {
            throw new InvalidDataException("The selected EPW entry has an invalid uncompressed size: " + entry.Length + ".");
        }

        return entry;
    }

    private static byte[] ComputeHash(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        using Stream stream = entry.Open();
        return ComputeHash(stream, cancellationToken);
    }

    private static string ComputeHashHex(Stream stream, CancellationToken cancellationToken)
    {
        byte[] hash = ComputeHash(stream, cancellationToken);
        var text = new char[hash.Length * 2];
        const string Hex = "0123456789abcdef";
        for (int index = 0; index < hash.Length; index++)
        {
            text[index * 2] = Hex[hash[index] >> 4];
            text[(index * 2) + 1] = Hex[hash[index] & 0x0f];
        }

        return new string(text);
    }

    private static byte[] ComputeHash(Stream stream, CancellationToken cancellationToken)
    {
        using SHA256 sha = SHA256.Create();
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return sha.Hash ?? throw new InvalidOperationException("SHA-256 did not produce a hash.");
    }

    private static bool ExistingFileMatches(
        string path,
        long expectedLength,
        byte[] expectedHash,
        CancellationToken cancellationToken)
    {
        var information = new FileInfo(path);
        if (information.Length != expectedLength)
        {
            return false;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.SequentialScan);
        return ComputeHash(stream, cancellationToken).SequenceEqual(expectedHash);
    }

    private static void ValidateEpwHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        string? header = reader.ReadLine();
        if (header is null || !header.StartsWith("LOCATION,", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The selected weather payload is not an EPW LOCATION document.");
        }
    }

    private static FileStream AcquireLock(
        string lockPath,
        SimpleDragonWeatherPackOptions options,
        CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + options.LockWaitTimeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                if (cancellationToken.WaitHandle.WaitOne(options.LockRetryDelay))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (IOException exception)
            {
                throw new TimeoutException("Lock path: " + lockPath, exception);
            }
        }
    }

    private string ResolveCacheRoot(string? suppliedRoot)
    {
        string root;
        if (!string.IsNullOrWhiteSpace(suppliedRoot))
        {
            root = suppliedRoot!.Trim();
        }
        else
        {
            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationData))
            {
                localApplicationData = Path.GetTempPath();
            }

            root = Path.Combine(
                localApplicationData,
                "Dragons",
                "BuildingEnergyWeather",
                "SimpleDragon",
                _manifest.PackId);
        }

        return Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void EnsureSafeDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? ancestor = fullPath;
        while (ancestor is not null)
        {
            if (Directory.Exists(ancestor) || File.Exists(ancestor))
            {
                EnsureNotReparsePoint(ancestor, "weather cache ancestor");
            }

            ancestor = Path.GetDirectoryName(ancestor);
        }

        Directory.CreateDirectory(fullPath);
        EnsureNotReparsePoint(fullPath, "weather cache root");
    }

    private static void EnsureNotReparsePoint(string path, string description)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The " + description + " must not be a reparse point: " + path + ".");
        }
    }

    private static string CombineUnder(string root, string fileName)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string combined = Path.GetFullPath(Path.Combine(fullRoot, fileName));
        string prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The weather cache path escaped its root.");
        }

        return combined;
    }

    private static string RequireSafeEpwFileName(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0
            || !normalized.EndsWith(".epw", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(normalized)
            || normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal)
            || normalized.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
        {
            throw new InvalidDataException("The address-selected EPW filename is unsafe: " + normalized + ".");
        }

        return normalized;
    }

    private static void CopyWithCancellation(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);
        }
    }

    private static void ValidateOptions(SimpleDragonWeatherPackOptions options)
    {
        if (options.LockWaitTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "LockWaitTimeout must be positive.");
        }

        if (options.LockRetryDelay <= TimeSpan.Zero || options.LockRetryDelay > options.LockWaitTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "LockRetryDelay must be positive and no greater than LockWaitTimeout.");
        }
    }

    private static void TryDeleteTemporary(string cacheRoot, string temporaryPath)
    {
        try
        {
            string safePath = CombineUnder(cacheRoot, Path.GetFileName(temporaryPath));
            if (File.Exists(safePath))
            {
                File.Delete(safePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static SimpleDragonWeatherFileResolution Succeeded(
        string filePath,
        string archivePath,
        bool extracted) =>
        new(filePath, archivePath, extracted, Array.Empty<Diagnostic>());

    private static SimpleDragonWeatherFileResolution Failed(
        string code,
        string message,
        string? action,
        DiagnosticSeverity severity = DiagnosticSeverity.Error) =>
        new(
            null,
            null,
            false,
            new[]
            {
                new Diagnostic(
                    code,
                    severity,
                    message,
                    suggestedAction: action),
            });

    private static SimpleDragonWeatherFileResolution[] RepeatFailure(
        int count,
        SimpleDragonWeatherFileResolution failure)
    {
        var results = new SimpleDragonWeatherFileResolution[count];
        for (int index = 0; index < results.Length; index++)
        {
            results[index] = failure;
        }

        return results;
    }

}
