using Grasshopper.Kernel;

namespace Dragons.SimpleDragon.Grasshopper.Components;

internal static class GrasshopperDocumentPathResolver
{
    internal static string Resolve(string path, GH_Document? document)
    {
        return Resolve(path, document, Directory.GetCurrentDirectory());
    }

    internal static string Resolve(
        string path,
        GH_Document? document,
        string unsavedDocumentDirectory)
    {
        string? documentFilePath = document is not null && document.IsFilePathDefined
            ? document.FilePath
            : null;
        return Resolve(path, documentFilePath, unsavedDocumentDirectory);
    }

    internal static string Resolve(
        string path,
        string? documentFilePath,
        string currentDirectory)
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

        if (string.IsNullOrWhiteSpace(currentDirectory))
        {
            throw new ArgumentException("A current directory is required.", nameof(currentDirectory));
        }

        if (Path.IsPathRooted(trimmedPath))
        {
            return Path.GetFullPath(trimmedPath);
        }

        string fallbackDirectory = Path.GetFullPath(currentDirectory.Trim());
        string baseDirectory = fallbackDirectory;
        if (!string.IsNullOrWhiteSpace(documentFilePath))
        {
            string trimmedDocumentPath = documentFilePath!.Trim();
            string fullDocumentPath = Path.IsPathRooted(trimmedDocumentPath)
                ? Path.GetFullPath(trimmedDocumentPath)
                : Path.GetFullPath(Path.Combine(fallbackDirectory, trimmedDocumentPath));
            string? documentDirectory = Path.GetDirectoryName(fullDocumentPath);
            if (!string.IsNullOrEmpty(documentDirectory))
            {
                baseDirectory = documentDirectory;
            }
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, trimmedPath));
    }
}
