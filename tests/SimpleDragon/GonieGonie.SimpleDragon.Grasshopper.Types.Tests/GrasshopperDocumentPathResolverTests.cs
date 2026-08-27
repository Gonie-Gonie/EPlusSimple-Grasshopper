using System.Reflection;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class GrasshopperDocumentPathResolverTests
{
    [Fact]
    public void RelativePathUsesOwningDocumentDirectory()
    {
        string root = TestRoot();
        string currentDirectory = Path.Combine(root, "working");
        string documentPath = Path.Combine(root, "definitions", "study.gh");

        string resolved = Resolve(
            Path.Combine("data", "baseline.grm"),
            documentPath,
            currentDirectory);

        Assert.Equal(
            Path.Combine(root, "definitions", "data", "baseline.grm"),
            resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsavedDocumentUsesCurrentDirectory(string? documentPath)
    {
        string currentDirectory = Path.Combine(TestRoot(), "working");

        string resolved = Resolve(
            Path.Combine("results", "case.grr"),
            documentPath,
            currentDirectory);

        Assert.Equal(
            Path.Combine(currentDirectory, "results", "case.grr"),
            resolved);
    }

    [Fact]
    public void AbsolutePathIsIndependentOfDocumentAndCurrentDirectories()
    {
        string root = TestRoot();
        string absolutePath = Path.Combine(root, "exports", "csv");

        string resolved = Resolve(
            "  " + absolutePath + "  ",
            Path.Combine(root, "definitions", "study.gh"),
            Path.Combine(root, "working"));

        Assert.Equal(absolutePath, resolved);
    }

    [Fact]
    public void UnsavedDocumentCanUseSystemTempAsItsExplicitFallback()
    {
        string tempDirectory = Path.GetFullPath(Path.GetTempPath());

        string resolved = ResolveForUnsavedDocument(
            Path.Combine("simpledragon-batch", "results"),
            tempDirectory);

        Assert.Equal(
            Path.Combine(tempDirectory, "simpledragon-batch", "results"),
            resolved);
    }

    [Fact]
    public void SavedDocumentOverridesAnExplicitSystemTempFallback()
    {
        string root = TestRoot();
        string documentPath = Path.Combine(root, "definitions", "batch-study.gh");

        string resolved = Resolve(
            Path.Combine("..", "temp", "batch-results"),
            documentPath,
            Path.GetTempPath());

        Assert.Equal(
            Path.Combine(root, "temp", "batch-results"),
            resolved);
    }

    private static string Resolve(
        string path,
        string? documentFilePath,
        string currentDirectory)
    {
        Assembly assembly = LoadPlugin();
        Type? resolver = assembly.GetType(
            "GonieGonie.SimpleDragon.Grasshopper.Components.GrasshopperDocumentPathResolver");
        Assert.NotNull(resolver);
        MethodInfo? method = resolver.GetMethod(
            "Resolve",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(string) },
            modifiers: null);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(
            obj: null,
            parameters: new object?[] { path, documentFilePath, currentDirectory }));
    }

    private static string ResolveForUnsavedDocument(
        string path,
        string fallbackDirectory)
    {
        Assembly assembly = LoadPlugin();
        Type? resolver = assembly.GetType(
            "GonieGonie.SimpleDragon.Grasshopper.Components.GrasshopperDocumentPathResolver");
        Assert.NotNull(resolver);
        Type? documentType = assembly.GetReferencedAssemblies()
            .Where(item => item.Name == "Grasshopper")
            .Select(item => Assembly.Load(item).GetType("Grasshopper.Kernel.GH_Document"))
            .Single();
        Assert.NotNull(documentType);
        MethodInfo? method = resolver.GetMethod(
            "Resolve",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), documentType!, typeof(string) },
            modifiers: null);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(
            obj: null,
            parameters: new object?[] { path, null, fallbackDirectory }));
    }

    private static Assembly LoadPlugin()
    {
        string path = Path.Combine(
            RepositoryRoot(),
            "temp",
            "build",
            "bin",
            "GonieGonie.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "GonieGonie.SimpleDragon.GH.gha");
        Assert.True(File.Exists(path), "Expected built Grasshopper assembly at '" + path + "'.");
        return Assembly.LoadFrom(path);
    }

    private static string TestRoot()
    {
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "simple-dragon-path-tests"));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
