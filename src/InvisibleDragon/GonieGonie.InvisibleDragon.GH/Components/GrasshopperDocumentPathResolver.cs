using Grasshopper.Kernel;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

internal static class GrasshopperDocumentPathResolver
{
    internal static string Resolve(
        string path,
        GH_Document? document,
        string fallbackDirectory)
    {
        string? documentFilePath = document is not null && document.IsFilePathDefined
            ? document.FilePath
            : null;
        return Resolve(path, documentFilePath, fallbackDirectory);
    }

    internal static string Resolve(
        string path,
        string? documentFilePath,
        string fallbackDirectory)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull is unavailable on Rhino 7's net48 target.
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }
#pragma warning restore CA1510

        string trimmedPath = path.Trim();
        if (trimmedPath.Length == 0)
        {
            throw new ArgumentException("A file or directory path is required.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(fallbackDirectory))
        {
            throw new ArgumentException("A fallback directory is required.", nameof(fallbackDirectory));
        }

        if (Path.IsPathRooted(trimmedPath))
        {
            return Path.GetFullPath(trimmedPath);
        }

        string fullFallback = Path.GetFullPath(fallbackDirectory.Trim());
        string baseDirectory = fullFallback;
        if (!string.IsNullOrWhiteSpace(documentFilePath))
        {
            string trimmedDocumentPath = documentFilePath!.Trim();
            string fullDocumentPath = Path.IsPathRooted(trimmedDocumentPath)
                ? Path.GetFullPath(trimmedDocumentPath)
                : Path.GetFullPath(Path.Combine(fullFallback, trimmedDocumentPath));
            string? documentDirectory = Path.GetDirectoryName(fullDocumentPath);
            if (!string.IsNullOrEmpty(documentDirectory))
            {
                baseDirectory = documentDirectory;
            }
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, trimmedPath));
    }
}
