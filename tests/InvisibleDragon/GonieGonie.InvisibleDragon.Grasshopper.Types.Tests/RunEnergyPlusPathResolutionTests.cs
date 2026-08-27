using System.Reflection;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class RunEnergyPlusPathResolutionTests
{
    [Fact]
    public void RelativeTempRootUsesOwningDocumentDirectory()
    {
        string root = TestRoot();
        string documentPath = Path.Combine(root, "examples", "workflow.gh");

        string resolved = ResolveTempRoot(
            Path.Combine("..", "temp", "energyplus-run"),
            documentPath);

        Assert.Equal(Path.Combine(root, "temp", "energyplus-run"), resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsavedDocumentKeepsRelativeTempRootOutOfRhinoInstallDirectory(
        string? documentPath)
    {
        string resolved = ResolveTempRoot(
            Path.Combine("energyplus", "runs"),
            documentPath);

        Assert.Equal(
            Path.Combine(Path.GetFullPath(Path.GetTempPath()), "energyplus", "runs"),
            resolved);
    }

    [Fact]
    public void AbsoluteTempRootIsIndependentOfDocumentLocation()
    {
        string absolute = Path.Combine(TestRoot(), "explicit", "runs");

        string resolved = ResolveTempRoot(
            "  " + absolute + "  ",
            Path.Combine(TestRoot(), "examples", "workflow.gh"));

        Assert.Equal(absolute, resolved);
    }

    [Theory]
    [InlineData("weather", "seoul.epw")]
    [InlineData("runtime", "EnergyPlusV24-2-0")]
    public void RelativeRunInputsUseOwningDocumentDirectory(
        string directory,
        string name)
    {
        string root = TestRoot();
        string documentPath = Path.Combine(root, "examples", "workflow.gh");

        string? resolved = ResolveOptionalPath(
            Path.Combine("..", directory, name),
            documentPath,
            Path.Combine(root, "rhino-system"));

        Assert.Equal(Path.Combine(root, directory, name), resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOptionalRunInputRemainsUnset(string supplied)
    {
        Assert.Null(ResolveOptionalPath(
            supplied,
            Path.Combine(TestRoot(), "examples", "workflow.gh"),
            Path.GetTempPath()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsavedRelativeRuntimeRootUsesSystemTemp(
        string? documentPath)
    {
        string? resolved = ResolveOptionalPath(
            Path.Combine("energyplus", "runtime"),
            documentPath,
            Path.GetTempPath());

        Assert.Equal(
            Path.Combine(Path.GetFullPath(Path.GetTempPath()), "energyplus", "runtime"),
            resolved);
    }

    private static string ResolveTempRoot(string path, string? documentFilePath)
    {
        Assembly assembly = LoadPlugin();
        Type? component = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.RunEnergyPlusComponent");
        Assert.NotNull(component);
        MethodInfo? method = component.GetMethod(
            "ResolveTempRoot",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(
            obj: null,
            parameters: new object?[] { path, documentFilePath }));
    }

    private static string? ResolveOptionalPath(
        string path,
        string? documentFilePath,
        string fallbackDirectory)
    {
        Assembly assembly = LoadPlugin();
        Type? component = assembly.GetType(
            "GonieGonie.InvisibleDragon.Grasshopper.Components.RunEnergyPlusComponent");
        Assert.NotNull(component);
        MethodInfo? method = component.GetMethod(
            "OptionalFullPath",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(string) },
            modifiers: null);
        Assert.NotNull(method);
        return (string?)method.Invoke(
            obj: null,
            parameters: new object?[] { path, documentFilePath, fallbackDirectory });
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
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "invisible-dragon-run-path-tests"));
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
