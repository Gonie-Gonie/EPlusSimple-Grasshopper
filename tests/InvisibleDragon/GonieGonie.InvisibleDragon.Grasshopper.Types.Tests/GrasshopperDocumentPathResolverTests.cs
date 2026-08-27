using System.Reflection;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class GrasshopperDocumentPathResolverTests
{
    [Fact]
    public void RelativePathUsesOwningDocumentDirectory()
    {
        string root = TestRoot();
        string fallback = Path.Combine(root, "working");
        string documentPath = Path.Combine(root, "definitions", "study.gh");

        string resolved = Resolve(
            Path.Combine("runtime", "Energy+.idd"),
            documentPath,
            fallback);

        Assert.Equal(
            Path.Combine(root, "definitions", "runtime", "Energy+.idd"),
            resolved);
    }

    [Fact]
    public void UnsavedDocumentUsesExplicitFallbackDirectory()
    {
        string fallback = Path.Combine(TestRoot(), "fallback");

        string resolved = Resolve(
            Path.Combine("runtime", "EnergyPlus"),
            documentFilePath: null,
            fallback);

        Assert.Equal(Path.Combine(fallback, "runtime", "EnergyPlus"), resolved);
    }

    [Fact]
    public void AbsolutePathIsIndependentOfDocumentAndFallbackDirectories()
    {
        string root = TestRoot();
        string absolutePath = Path.Combine(root, "runtime", "EnergyPlus");

        string resolved = Resolve(
            "  " + absolutePath + "  ",
            Path.Combine(root, "definitions", "study.gh"),
            Path.Combine(root, "working"));

        Assert.Equal(absolutePath, resolved);
    }

    private static string Resolve(
        string path,
        string? documentFilePath,
        string fallbackDirectory)
    {
        Assembly assembly = LoadPlugin();
        Type? resolver = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.GrasshopperDocumentPathResolver");
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
            parameters: new object?[] { path, documentFilePath, fallbackDirectory }));
    }

    private static Assembly LoadPlugin()
    {
        string path = Path.Combine(
            RepositoryRoot(),
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

    private static string TestRoot()
    {
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "invisible-dragon-path-tests"));
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
