using System.IO.Compression;

namespace GonieGonie.EnergyPlus.Runtime;

internal static class RuntimeArchiveExtractor
{
    private const int BufferSize = 81920;
    private const int MaximumEntryCount = 100000;
    private const long MaximumExtractedBytes = 8L * 1024 * 1024 * 1024;
    private static readonly char[] UnsafeWindowsPathCharacters = { ':' };
    private static readonly char[] ArchiveSeparators = { '/' };

    internal static async Task ExtractAsync(
        string archivePath,
        string extractionRoot,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(extractionRoot);
        using var stream = new FileStream(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count > MaximumEntryCount)
        {
            throw UnsafeArchive(
                "ARCHIVE_ENTRY_LIMIT_EXCEEDED",
                "The EnergyPlus archive contains too many entries.",
                $"Maximum {MaximumEntryCount}; actual {archive.Entries.Count}.");
        }

        long extractedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSymbolicLink(entry))
            {
                throw UnsafeArchive(
                    "ARCHIVE_LINK_UNSAFE",
                    "The EnergyPlus archive contains a symbolic-link entry.",
                    entry.FullName);
            }

            try
            {
                extractedBytes = checked(extractedBytes + entry.Length);
            }
            catch (OverflowException exception)
            {
                throw UnsafeArchive(
                    "ARCHIVE_EXPANDED_SIZE_INVALID",
                    "The EnergyPlus archive declares an invalid expanded size.",
                    exception.Message);
            }

            if (extractedBytes > MaximumExtractedBytes)
            {
                throw UnsafeArchive(
                    "ARCHIVE_EXPANDED_SIZE_LIMIT_EXCEEDED",
                    "The EnergyPlus archive expands beyond the permitted safety limit.",
                    $"Maximum {MaximumExtractedBytes} bytes.");
            }

            var segments = GetSafeSegments(entry.FullName);
            if (segments.Length == 0)
            {
                continue;
            }

            var destination = RuntimeFileSystem.CombineUnder(extractionRoot, segments);
            var isDirectory = EndsWithCharacter(entry.FullName, '/')
                || EndsWithCharacter(entry.FullName, '\\');
            if (isDirectory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            var parent = Path.GetDirectoryName(destination)
                ?? throw new InvalidDataException("An archive destination has no parent directory.");
            Directory.CreateDirectory(parent);
            using var source = entry.Open();
            using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await CopyAsync(source, output, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string[] GetSafeSegments(string entryName)
    {
        if (string.IsNullOrEmpty(entryName))
        {
            return Array.Empty<string>();
        }

        var normalized = entryName.Replace('\\', '/');
        if (StartsWithCharacter(normalized, '/')
            || normalized.IndexOfAny(UnsafeWindowsPathCharacters) >= 0
            || Path.IsPathRooted(entryName))
        {
            throw UnsafeArchive(
                "ARCHIVE_PATH_UNSAFE",
                "The EnergyPlus archive contains an absolute or device path.",
                entryName);
        }

        var segments = normalized.Split(ArchiveSeparators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "."
                || segment == ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || EndsWithCharacter(segment, '.')
                || EndsWithCharacter(segment, ' '))
            {
                throw UnsafeArchive(
                    "ARCHIVE_PATH_UNSAFE",
                    "The EnergyPlus archive contains an unsafe relative path.",
                    entryName);
            }
        }

        return segments;
    }

    private static bool StartsWithCharacter(string value, char expected)
    {
        return value.Length != 0 && value[0] == expected;
    }

    private static bool EndsWithCharacter(string value, char expected)
    {
        return value.Length != 0 && value[value.Length - 1] == expected;
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int unixFileTypeMask = 0xF000;
        const int unixSymbolicLink = 0xA000;
        var unixMode = (entry.ExternalAttributes >> 16) & unixFileTypeMask;
        return unixMode == unixSymbolicLink;
    }

    private static async Task CopyAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        while (true)
        {
#if NET7_0_OR_GREATER
            var count = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
            var count = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
            if (count == 0)
            {
                break;
            }

#if NET7_0_OR_GREATER
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
#else
            await destination.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
#endif
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static RuntimeArchiveExtractionException UnsafeArchive(
        string code,
        string message,
        string? detail)
    {
        return new RuntimeArchiveExtractionException(new EnergyPlusFailure(
            EnergyPlusFailureCategory.RuntimeIntegrity,
            code,
            message,
            detail));
    }
}

internal sealed class RuntimeArchiveExtractionException : Exception
{
    internal RuntimeArchiveExtractionException(EnergyPlusFailure failure)
        : base(failure.Message)
    {
        Failure = failure;
    }

    internal EnergyPlusFailure Failure { get; }
}
