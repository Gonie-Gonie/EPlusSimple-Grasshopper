using System.Security.Cryptography;
using System.Text.Json.Nodes;
using GH_IO.Serialization;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Dragons.InvisibleDragon.Grasshopper.Types;

namespace Dragons.InvisibleDragon.Grasshopper.Tests;

public sealed class PreparedWeatherFileGooTests
{
    private const string Provider = "Climate.OneBuilding.Org";
    private const string WeatherIdentity = "KOR_Seoul.471080_IWEC.epw";

    [Fact]
    public void ConstructorNormalizesMetadataWithoutRequiringArtifactToExist()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".EPW");

        var weather = new PreparedWeatherFile(
            path,
            $"  {Provider}  ",
            $"  {WeatherIdentity}  ",
            new string('a', 64));

        Assert.Equal(Path.GetFullPath(path), weather.ArtifactPath);
        Assert.Equal(Provider, weather.Provider);
        Assert.Equal(WeatherIdentity, weather.WeatherIdentity);
        Assert.Equal(new string('A', 64), weather.Sha256);
        Assert.False(weather.VerifyArtifact());
    }

    [Fact]
    public void ConstructorRejectsRelativeNonEpwAndMalformedMetadata()
    {
        string absoluteEpw = Path.Combine(Path.GetTempPath(), "prepared-weather.epw");
        string absoluteText = Path.ChangeExtension(absoluteEpw, ".txt");
        string validHash = new('A', 64);

        Assert.Throws<ArgumentException>(
            () => new PreparedWeatherFile("relative.epw", Provider, WeatherIdentity, validHash));
        Assert.Throws<ArgumentException>(
            () => new PreparedWeatherFile(absoluteText, Provider, WeatherIdentity, validHash));
        Assert.Throws<ArgumentException>(
            () => new PreparedWeatherFile(absoluteEpw, string.Empty, WeatherIdentity, validHash));
        Assert.Throws<ArgumentException>(
            () => new PreparedWeatherFile(absoluteEpw, Provider, " ", validHash));
        Assert.Throws<ArgumentException>(
            () => new PreparedWeatherFile(absoluteEpw, Provider, WeatherIdentity, new string('A', 63)));
        Assert.Throws<ArgumentException>(
            () => new PreparedWeatherFile(absoluteEpw, Provider, WeatherIdentity, new string('Z', 64)));
    }

    [Fact]
    public void FactoryHashesArtifactAndVerificationDetectsMutationAndDeletion()
    {
        string artifactPath = CreateArtifact("LOCATION,Seoul\nDATA,initial");
        try
        {
            PreparedWeatherFile weather = PreparedWeatherFile.FromVerifiedArtifact(
                artifactPath,
                Provider,
                WeatherIdentity);

            Assert.Equal(ExpectedSha256(artifactPath), weather.Sha256);
            Assert.True(weather.VerifyArtifact());

            File.AppendAllText(artifactPath, "\nchanged");
            Assert.False(weather.VerifyArtifact());

            File.Delete(artifactPath);
            Assert.False(weather.VerifyArtifact());
            Assert.Throws<FileNotFoundException>(
                () => PreparedWeatherFile.FromVerifiedArtifact(artifactPath, Provider, WeatherIdentity));
        }
        finally
        {
            DeleteArtifact(artifactPath);
        }
    }

    [Fact]
    public void FactoryRejectsArtifactWithoutLocationHeader()
    {
        string artifactPath = CreateArtifact("NOT-AN-EPW\nDATA,invalid");
        try
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => PreparedWeatherFile.FromVerifiedArtifact(
                    artifactPath,
                    Provider,
                    WeatherIdentity));

            Assert.Contains("LOCATION", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(artifactPath, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteArtifact(artifactPath);
        }
    }

    [Fact]
    public void DomainAndGooDisplayExposeIdentityButNeverLocalPath()
    {
        string artifactPath = CreateArtifact("LOCATION,Seoul\nDATA,display");
        try
        {
            PreparedWeatherFile weather = PreparedWeatherFile.FromVerifiedArtifact(
                artifactPath,
                Provider,
                WeatherIdentity);
            var goo = new PreparedWeatherFileGoo(weather);
            string domainDisplay = weather.ToString();
            string gooDisplay = goo.ToString();
            string? castDisplay = null;

            Assert.True(goo.CastTo(ref castDisplay));
            AssertPathFreeDisplay(domainDisplay, weather);
            AssertPathFreeDisplay(gooDisplay, weather);
            AssertPathFreeDisplay(Assert.IsType<string>(castDisplay), weather);
        }
        finally
        {
            DeleteArtifact(artifactPath);
        }
    }

    [Fact]
    public void V1SnapshotAndArchivePersistOnlyLogicalMetadata()
    {
        string artifactPath = CreateArtifact("LOCATION,Seoul\nDATA,persistence");
        try
        {
            PreparedWeatherFile weather = PreparedWeatherFile.FromVerifiedArtifact(
                artifactPath,
                Provider,
                WeatherIdentity);
            var source = new PreparedWeatherFileGoo(weather);

            string snapshot = DragonGooSnapshot.Serialize(weather);
            JsonObject envelope = Assert.IsType<JsonObject>(JsonNode.Parse(snapshot));
            Assert.Equal("dragons.invisible-dragon.grasshopper-goo.v1", envelope["schema"]?.GetValue<string>());
            Assert.Equal("prepared-weather-file", envelope["kind"]?.GetValue<string>());
            Assert.DoesNotContain(artifactPath, snapshot, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("artifactPath", snapshot, StringComparison.OrdinalIgnoreCase);

            PreparedWeatherFileGoo duplicate = Assert.IsType<PreparedWeatherFileGoo>(source.Duplicate());
            (PreparedWeatherFileGoo restored, byte[] archive) = ArchiveRoundTrip(
                source,
                new PreparedWeatherFileGoo());

            Assert.NotSame(weather, duplicate.Value);
            AssertEquivalent(weather, duplicate.Value);
            Assert.True(duplicate.Value.VerifyArtifact());
            AssertLogicalMetadata(weather, restored.Value);
            Assert.False(restored.Value.IsBound);
            Assert.False(restored.Value.VerifyArtifact());
            Assert.Throws<InvalidOperationException>(() => restored.Value.ArtifactPath);
            Assert.False(ContainsBytes(archive, System.Text.Encoding.UTF8.GetBytes(artifactPath)));
            Assert.False(ContainsBytes(archive, System.Text.Encoding.Unicode.GetBytes(artifactPath)));
        }
        finally
        {
            DeleteArtifact(artifactPath);
        }
    }

    [Fact]
    public void LegacyV1PayloadWithArtifactPathStillReadsAsUnboundMetadata()
    {
        string legacyPath = Path.Combine(
            Path.GetTempPath(),
            "private-user-cache",
            WeatherIdentity);
        string payload = new JsonObject
        {
            ["artifactPath"] = legacyPath,
            ["provider"] = Provider,
            ["weatherIdentity"] = WeatherIdentity,
            ["sha256"] = new string('B', 64),
        }.ToJsonString();
        string snapshot = new JsonObject
        {
            ["schema"] = "dragons.invisible-dragon.grasshopper-goo.v1",
            ["kind"] = "prepared-weather-file",
            ["payload"] = payload,
        }.ToJsonString();

        PreparedWeatherFile restored = DragonGooSnapshot.Deserialize<PreparedWeatherFile>(snapshot);

        Assert.Equal(Provider, restored.Provider);
        Assert.Equal(WeatherIdentity, restored.WeatherIdentity);
        Assert.Equal(new string('B', 64), restored.Sha256);
        Assert.False(restored.IsBound);
        Assert.False(restored.TryGetArtifactPath(out string? artifactPath));
        Assert.Null(artifactPath);
        Assert.False(restored.VerifyArtifact());
        Assert.DoesNotContain(legacyPath, restored.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreparedWeatherParameterUsesReservedStableGuid()
    {
        var parameter = new PreparedWeatherFileParam();

        Assert.Equal(new Guid("9571341c-3795-417d-9908-5833d234d815"), parameter.ComponentGuid);
        Assert.Equal("InvisibleDragon", parameter.Category);
        Assert.Equal("Parameters", parameter.SubCategory);
    }

    private static void AssertPathFreeDisplay(string display, PreparedWeatherFile weather)
    {
        string directory = Path.GetDirectoryName(weather.ArtifactPath)
            ?? throw new Xunit.Sdk.XunitException("The test artifact path has no directory.");
        Assert.Contains(weather.Provider, display, StringComparison.Ordinal);
        Assert.Contains(weather.WeatherIdentity, display, StringComparison.Ordinal);
        Assert.Contains(weather.Sha256, display, StringComparison.Ordinal);
        Assert.DoesNotContain(weather.ArtifactPath, display, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(directory, display, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertEquivalent(PreparedWeatherFile expected, PreparedWeatherFile actual)
    {
        Assert.Equal(expected.ArtifactPath, actual.ArtifactPath);
        Assert.True(actual.IsBound);
        AssertLogicalMetadata(expected, actual);
    }

    private static void AssertLogicalMetadata(PreparedWeatherFile expected, PreparedWeatherFile actual)
    {
        Assert.Equal(expected.Provider, actual.Provider);
        Assert.Equal(expected.WeatherIdentity, actual.WeatherIdentity);
        Assert.Equal(expected.Sha256, actual.Sha256);
    }

    private static (PreparedWeatherFileGoo Goo, byte[] Archive) ArchiveRoundTrip(
        PreparedWeatherFileGoo source,
        PreparedWeatherFileGoo target)
    {
        var writeArchive = new GH_Archive();
        Assert.True(writeArchive.AppendObject(source, "Value"));
        byte[] bytes = writeArchive.Serialize_Binary();
        var readArchive = new GH_Archive();
        Assert.True(readArchive.Deserialize_Binary(bytes));
        Assert.True(readArchive.ExtractObject(target, "Value"));
        return (target, bytes);
    }

    private static string CreateArtifact(string content)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "dragons-prepared-weather-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, WeatherIdentity);
        File.WriteAllText(path, content);
        return path;
    }

    private static void DeleteArtifact(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        string? directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory);
        }
    }

    private static string ExpectedSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool ContainsBytes(byte[] source, byte[] candidate)
    {
        for (int index = 0; index <= source.Length - candidate.Length; index++)
        {
            if (source.AsSpan(index, candidate.Length).SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }
}
