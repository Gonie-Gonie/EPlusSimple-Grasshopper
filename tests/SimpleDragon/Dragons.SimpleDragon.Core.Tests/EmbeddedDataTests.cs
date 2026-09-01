using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Dragons.SimpleDragon.Tests;

public sealed class EmbeddedDataTests
{
    [Fact]
    public void AllEightCsvFilesAreEmbeddedByteForByteAndPinnedByUpstreamManifest()
    {
        DirectoryInfo repository = FindRepositoryRoot();
        string manifestText = File.ReadAllText(
            Path.Combine(repository.FullName, "upstream", "data-hashes.json"));
        var manifestHashes = new HashSet<string>(
            Regex.Matches(manifestText, "[0-9a-f]{64}", RegexOptions.CultureInvariant)
                .Select(match => match.Value),
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(8, SimpleDragonEmbeddedData.Files.Count);
        foreach (string path in SimpleDragonEmbeddedData.Files)
        {
            byte[] embedded = SimpleDragonEmbeddedData.ReadAllBytes(path);
            byte[] copied = File.ReadAllBytes(
                Path.Combine(repository.FullName, "data", "simple-dragon", path.Replace('/', Path.DirectorySeparatorChar)));
            string hash = Convert.ToHexString(SHA256.HashData(embedded));

            Assert.Equal(copied, embedded);
            Assert.Contains(hash, manifestHashes);
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "upstream", "data-hashes.json")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
