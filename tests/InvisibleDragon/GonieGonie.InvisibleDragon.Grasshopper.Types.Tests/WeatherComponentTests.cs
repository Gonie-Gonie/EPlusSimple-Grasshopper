using System.Reflection;
using System.Security.Cryptography;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Types;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class WeatherComponentTests
{
    private const string ComponentTypeName =
        "GonieGonie.InvisibleDragon.Grasshopper.Components.VerifyInvisibleDragonWeatherComponent";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrWhitespaceInputIsANoOp(string? input)
    {
        object result = Verify(input, documentFilePath: null);

        Assert.False(Success(result));
        Assert.Null(Weather(result));
        Assert.Empty(Diagnostics(result));
    }

    [Fact]
    public void SavedDocumentResolvesRelativeEpwAndCreatesContentAddressedHandle()
    {
        string root = CreateTestDirectory();
        try
        {
            string definitionDirectory = Path.Combine(root, "definition");
            string weatherDirectory = Path.Combine(definitionDirectory, "weather");
            Directory.CreateDirectory(weatherDirectory);
            string documentPath = Path.Combine(definitionDirectory, "standalone.gh");
            string artifactPath = Path.Combine(weatherDirectory, "seoul.epw");
            File.WriteAllText(artifactPath, "LOCATION,Seoul\nDATA,verified");

            object result = Verify(Path.Combine("weather", "seoul.epw"), documentPath);
            PreparedWeatherFile weather = Assert.IsType<PreparedWeatherFile>(Weather(result));

            Assert.True(Success(result));
            Assert.Empty(Diagnostics(result));
            Assert.Equal(Path.GetFullPath(artifactPath), weather.ArtifactPath);
            Assert.Equal(ExpectedSha256(artifactPath), weather.Sha256);
            Assert.True(weather.VerifyArtifact());
            Assert.DoesNotContain(root, weather.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(artifactPath, weather.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RelativeInputWithoutSavedDocumentReturnsPathFreeDiagnostic()
    {
        const string input = @"private\weather\secret.epw";

        object result = Verify(input, documentFilePath: null);
        Diagnostic diagnostic = Assert.Single(Diagnostics(result));

        Assert.False(Success(result));
        Assert.Null(Weather(result));
        Assert.Equal(
            "INVISIBLEDRAGON.GH.WEATHER.RELATIVE_PATH_REQUIRES_SAVED_DOCUMENT",
            diagnostic.Code);
        AssertPathFree(diagnostic, input, "secret.epw");
    }

    [Fact]
    public void InvalidNonEmptyInputsReturnSpecificPathFreeDiagnostics()
    {
        string root = CreateTestDirectory();
        try
        {
            string wrongExtension = Path.Combine(root, "private-weather.txt");
            string missing = Path.Combine(root, "missing-private.epw");
            string invalidHeader = Path.Combine(root, "invalid-private.epw");
            File.WriteAllText(wrongExtension, "LOCATION,Seoul\nDATA,wrong-extension");
            File.WriteAllText(invalidHeader, "NOT-AN-EPW\nDATA,invalid");

            AssertFailure(
                Verify(wrongExtension, documentFilePath: null),
                "INVISIBLEDRAGON.GH.WEATHER.EXTENSION_INVALID",
                root,
                wrongExtension,
                "private-weather.txt");
            AssertFailure(
                Verify(missing, documentFilePath: null),
                "INVISIBLEDRAGON.GH.WEATHER.FILE_NOT_FOUND",
                root,
                missing,
                "missing-private.epw");
            AssertFailure(
                Verify(invalidHeader, documentFilePath: null),
                "INVISIBLEDRAGON.GH.WEATHER.HEADER_INVALID",
                root,
                invalidHeader,
                "invalid-private.epw");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertFailure(object result, string code, params string[] privateText)
    {
        Assert.False(Success(result));
        Assert.Null(Weather(result));
        Diagnostic diagnostic = Assert.Single(Diagnostics(result));
        Assert.Equal(code, diagnostic.Code);
        AssertPathFree(diagnostic, privateText);
    }

    private static void AssertPathFree(Diagnostic diagnostic, params string[] privateText)
    {
        string diagnosticText = diagnostic.Message + "\n" + diagnostic.SuggestedAction;
        foreach (string text in privateText)
        {
            Assert.DoesNotContain(text, diagnosticText, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static object Verify(string? input, string? documentFilePath)
    {
        Assembly assembly = LoadPlugin();
        Type componentType = assembly.GetType(ComponentTypeName, throwOnError: true)!;
        MethodInfo verify = Assert.IsAssignableFrom<MethodInfo>(componentType.GetMethod(
            "VerifyInput",
            BindingFlags.Static | BindingFlags.NonPublic));
        object? result = verify.Invoke(null, new object?[] { input, documentFilePath });
        return Assert.IsAssignableFrom<object>(result);
    }

    private static bool Success(object result)
    {
        return Assert.IsType<bool>(result.GetType().GetProperty("Success")!.GetValue(result));
    }

    private static PreparedWeatherFile? Weather(object result)
    {
        return result.GetType().GetProperty("Weather")!.GetValue(result) as PreparedWeatherFile;
    }

    private static IReadOnlyList<Diagnostic> Diagnostics(object result)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<Diagnostic>>(
            result.GetType().GetProperty("Diagnostics")!.GetValue(result));
    }

    private static string CreateTestDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "goniegonie-id-weather-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ExpectedSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static Assembly LoadPlugin()
    {
        string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string path = Path.Combine(
            repositoryRoot,
            "temp",
            "build",
            "bin",
            "GonieGonie.InvisibleDragon.GH",
            "Release",
            "net8.0-windows",
            "GonieGonie.InvisibleDragon.GH.gha");
        Assert.True(File.Exists(path), "Expected built Grasshopper assembly at '" + path + "'.");
        return Assembly.LoadFrom(path);
    }

    private static string FindRepositoryRoot(string start)
    {
        DirectoryInfo? current = new(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Dragons.Grasshopper.sln repository root.");
    }
}
