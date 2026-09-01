namespace Dragons.EnergyPlus.Runtime;

internal interface IEnergyPlusRuntimeBundledArchiveLocator
{
    string? FindArchivePath();
}

internal sealed class AssemblyAdjacentEnergyPlusRuntimeArchiveLocator
    : IEnergyPlusRuntimeBundledArchiveLocator
{
    private const string SimpleDragonPackageDirectoryName = "simple-dragon";
    private const string InvisibleDragonPackageDirectoryName = "invisible-dragon";

    // A directly bundled Rhino archive requires at most one parent hop; a
    // SimpleDragon-loaded shared assembly reaches its matching InvisibleDragon Yak
    // sibling within three. Six also reaches the repository root from the standard
    // temp/build/bin/<project>/<config>/<tfm> developer layout, without enumerating
    // package directories or turning this into an unbounded filesystem search.
    internal const int MaximumAncestorLevels = 6;

    private readonly string startingDirectory;

    internal AssemblyAdjacentEnergyPlusRuntimeArchiveLocator()
        : this(GetExecutingAssemblyDirectory())
    {
    }

    internal AssemblyAdjacentEnergyPlusRuntimeArchiveLocator(string startingDirectory)
    {
        if (string.IsNullOrWhiteSpace(startingDirectory))
        {
            throw new ArgumentException("An assembly directory is required.", nameof(startingDirectory));
        }

        this.startingDirectory = Path.GetFullPath(startingDirectory);
    }

    public string? FindArchivePath()
    {
        DirectoryInfo? current = new(startingDirectory);
        for (var ancestorLevel = 0;
            current is not null && ancestorLevel <= MaximumAncestorLevels;
            ancestorLevel++)
        {
            var directArchive = FindArchiveAtRoot(current.FullName);
            if (directArchive is not null)
            {
                return directArchive;
            }

            var siblingArchive = FindMatchingInvisibleDragonYakSibling(current);
            if (siblingArchive is not null)
            {
                return siblingArchive;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? FindMatchingInvisibleDragonYakSibling(DirectoryInfo current)
    {
        // Yak installs matching package versions as
        // <host>/simple-dragon/<version>/... and
        // <host>/invisible-dragon/<version>/.... The shared runtime DLL may be
        // loaded from the former, so derive the one exact sibling root from the
        // version directory instead of scanning arbitrary packages or versions.
        var simpleDragonRoot = current.Parent;
        if (simpleDragonRoot is null
            || !simpleDragonRoot.Name.Equals(
                SimpleDragonPackageDirectoryName,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hostPackageRoot = simpleDragonRoot.Parent;
        if (hostPackageRoot is null)
        {
            return null;
        }

        var invisibleDragonVersionRoot = Path.Combine(
            hostPackageRoot.FullName,
            InvisibleDragonPackageDirectoryName,
            current.Name);
        return FindArchiveAtRoot(invisibleDragonVersionRoot);
    }

    private static string? FindArchiveAtRoot(string root)
    {
        foreach (var relativePath in new[]
        {
            Path.Combine(
                "runtime",
                "energyplus",
                EnergyPlusRuntimeDistribution.SupportedArchiveFileName),
            Path.Combine(
                ".tools",
                "distributions",
                "energyplus",
                EnergyPlusRuntimeDistribution.SupportedArchiveFileName),
        })
        {
            var candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static string GetExecutingAssemblyDirectory()
    {
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
            ? null
            : Path.GetDirectoryName(assemblyLocation);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            return assemblyDirectory!;
        }

        return AppContext.BaseDirectory;
    }
}
