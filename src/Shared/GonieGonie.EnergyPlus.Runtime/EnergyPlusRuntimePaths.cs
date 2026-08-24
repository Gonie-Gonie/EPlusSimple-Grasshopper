namespace GonieGonie.EnergyPlus.Runtime;

/// <summary>
/// Defines stable per-user locations for GonieGonie-managed EnergyPlus runtimes.
/// </summary>
public static class EnergyPlusRuntimePaths
{
    private static readonly char[] DirectorySeparators =
    {
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar
    };

    private const string PublisherDirectoryName = "GonieGonie";
    private const string RuntimeDirectoryName = "BuildingEnergyRuntime";
    private const string EnergyPlusDirectoryName = "EnergyPlus";

    /// <summary>
    /// Gets the default cache root for the supported EnergyPlus runtime.
    /// </summary>
    public static string DefaultRuntimeRoot => GetDefaultRuntimeRoot(EnergyPlusRuntimeIdentity.Supported);

    /// <summary>
    /// Gets the stable per-user cache root for an EnergyPlus runtime identity.
    /// </summary>
    public static string GetDefaultRuntimeRoot(EnergyPlusRuntimeIdentity identity)
    {
        identity = identity ?? throw new ArgumentNullException(nameof(identity));

        ValidatePathSegment(identity.Version, nameof(identity));
        ValidatePathSegment(identity.Build, nameof(identity));

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "The current Windows profile does not expose a LocalApplicationData directory.");
        }

        return RuntimeFileSystem.NormalizeDirectory(Path.Combine(
            localApplicationData,
            PublisherDirectoryName,
            RuntimeDirectoryName,
            EnergyPlusDirectoryName,
            identity.Version + "-" + identity.Build));
    }

    internal static string GetDefaultRuntimeRoot(EnergyPlusRuntimeManifest manifest)
    {
        return GetDefaultRuntimeRoot(new EnergyPlusRuntimeIdentity(
            manifest.EnergyPlusVersion,
            manifest.EnergyPlusBuild,
            manifest.EnergyPlusExecutableSha256,
            manifest.EnergyPlusIddSha256,
            manifest.ExpandObjectsSha256));
    }

    private static void ValidatePathSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value == "."
            || value == ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.IndexOfAny(DirectorySeparators) >= 0)
        {
            throw new ArgumentException("The runtime identity contains an invalid path segment.", parameterName);
        }
    }
}
