using System.Reflection;

namespace Dragons.ExampleDefinitions;

/// <summary>
/// Publishes generated binary examples without replacing tracked containers that
/// already satisfy the complete semantic and technical-identity contract.
/// </summary>
internal static class CanonicalExamplePublisher
{
    internal static void ValidateGrasshopperIdentity(string path, string product)
    {
        string assemblyName = product switch
        {
            "InvisibleDragon" => "Dragons.InvisibleDragon.GH",
            "SimpleDragon" => "Dragons.SimpleDragon.GH",
            _ => throw new InvalidOperationException("Unknown example product '" + product + "'."),
        };
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .SingleOrDefault(candidate => string.Equals(
                candidate.GetName().Name,
                assemblyName,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The current example host has not loaded '" + assemblyName + "'.");
        string expectedIdentity = assembly.GetName().FullName
            ?? throw new InvalidOperationException("The loaded Dragon assembly has no full identity.");
        string expectedVersion = assembly.GetName().Version?.ToString()
            ?? throw new InvalidOperationException("The loaded Dragon assembly has no version identity.");

        var archive = new GH_IO.Serialization.GH_Archive();
        if (!archive.ReadFromFile(path))
        {
            throw new InvalidOperationException("Grasshopper could not inspect example metadata in '" + path + "'.");
        }

        string xml = archive.Serialize_Xml();
        string[] dragonIdentities = System.Text.RegularExpressions.Regex.Matches(
                xml,
                "<item name=\"AssemblyFullName\"[^>]*>(?<value>[^<]+)</item>",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(match => System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value))
            .Where(value => value.StartsWith("Dragons.", StringComparison.Ordinal)
                || value.StartsWith("GonieGonie.", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (dragonIdentities.Length != 1
            || !string.Equals(dragonIdentities[0], expectedIdentity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Example '" + Path.GetFileName(path) + "' must reference only '" + expectedIdentity
                    + "'; found '" + string.Join("', '", dragonIdentities) + "'.");
        }

        string[] assemblyVersions = System.Text.RegularExpressions.Regex.Matches(
                xml,
                "<item name=\"AssemblyVersion\"[^>]*>(?<value>[^<]+)</item>",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(match => System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (assemblyVersions.Length != 1
            || !string.Equals(assemblyVersions[0], expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Example '" + Path.GetFileName(path) + "' must store assembly version '"
                    + expectedVersion + "'; found '" + string.Join("', '", assemblyVersions) + "'.");
        }

        bool exposesLocalIdentity = System.Text.RegularExpressions.Regex.IsMatch(
                xml,
                "GonieGonie",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            || System.Text.RegularExpressions.Regex.IsMatch(
                xml,
                @"(?:[A-Za-z]:[\\/]|\\\\)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (exposesLocalIdentity)
        {
            throw new InvalidOperationException(
                "Example '" + Path.GetFileName(path)
                    + "' exposes a local user, repository, or absolute-path identity.");
        }
    }

    internal static bool Publish(
        string candidatePath,
        string canonicalPath,
        string outputDirectory,
        Action<string> validateSemantic)
    {
        if (!File.Exists(candidatePath))
        {
            throw new FileNotFoundException("Generated example candidate is absent.", candidatePath);
        }

        validateSemantic(candidatePath);
        if (File.Exists(canonicalPath))
        {
            try
            {
                validateSemantic(canonicalPath);
                return false;
            }
            catch (InvalidOperationException)
            {
                // The tracked binary no longer satisfies the current semantic
                // contract. The already validated candidate may replace it.
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(canonicalPath)
            ?? throw new InvalidOperationException("Canonical example has no parent directory."));
        string rollbackDirectory = Path.Combine(outputDirectory, "publish-rollback");
        Directory.CreateDirectory(rollbackDirectory);
        string rollbackPath = Path.Combine(rollbackDirectory, Path.GetFileName(canonicalPath) + ".previous");
        if (File.Exists(rollbackPath)) File.Delete(rollbackPath);

        bool replaced = File.Exists(canonicalPath);
        try
        {
            if (replaced) File.Replace(candidatePath, canonicalPath, rollbackPath, ignoreMetadataErrors: true);
            else File.Move(candidatePath, canonicalPath);
            validateSemantic(canonicalPath);
            if (File.Exists(rollbackPath)) File.Delete(rollbackPath);
            return true;
        }
        catch
        {
            if (replaced && File.Exists(rollbackPath))
            {
                File.Replace(rollbackPath, canonicalPath, null, ignoreMetadataErrors: true);
            }
            else if (!replaced && File.Exists(canonicalPath))
            {
                File.Delete(canonicalPath);
            }
            throw;
        }
    }
}
