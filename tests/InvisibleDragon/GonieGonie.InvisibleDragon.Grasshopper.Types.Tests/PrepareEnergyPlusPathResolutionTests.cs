using System.Reflection;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class PrepareEnergyPlusPathResolutionTests
{
    [Fact]
    public void RelativeTargetRootUsesOwningDocumentDirectory()
    {
        string root = TestRoot();
        string documentPath = Path.Combine(root, "examples", "workflow.gh");

        string? resolved = ResolveTargetRoot(
            Path.Combine("..", "temp", "energyplus-runtime"),
            documentPath);

        Assert.Equal(Path.Combine(root, "temp", "energyplus-runtime"), resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsavedDocumentResolvesRelativeTargetFromSystemTemp(
        string? documentPath)
    {
        string? resolved = ResolveTargetRoot(
            Path.Combine("energyplus", "runtime"),
            documentPath);

        Assert.Equal(
            Path.Combine(Path.GetFullPath(Path.GetTempPath()), "energyplus", "runtime"),
            resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTargetRootKeepsManagedDefault(string supplied)
    {
        Assert.Null(ResolveTargetRoot(supplied, Path.Combine(TestRoot(), "workflow.gh")));
    }

    private static string? ResolveTargetRoot(string value, string? documentFilePath)
    {
        Assembly assembly = LoadPlugin();
        Type? component = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.PrepareEnergyPlusRuntimeComponent");
        Assert.NotNull(component);
        MethodInfo? method = component.GetMethod(
            "OptionalFullPath",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);
        Assert.NotNull(method);
        return (string?)method.Invoke(
            obj: null,
            parameters: new object?[] { value, documentFilePath });
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
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "invisible-dragon-prepare-path-tests"));
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
