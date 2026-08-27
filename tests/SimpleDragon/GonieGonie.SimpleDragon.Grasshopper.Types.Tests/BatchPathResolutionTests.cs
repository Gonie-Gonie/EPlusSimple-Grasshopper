using System.Reflection;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class BatchPathResolutionTests
{
    [Theory]
    [InlineData("weather", "seoul.epw")]
    [InlineData("runtime", "EnergyPlusV24-2-0")]
    public void SavedBatchReadPathsUseOwningDocumentDirectory(
        string directory,
        string name)
    {
        string root = TestRoot();
        string documentPath = Path.Combine(root, "examples", "batch.gh");

        string? resolved = ResolveReadPath(
            Path.Combine("..", directory, name),
            documentPath,
            Path.Combine(root, "rhino-system"));

        Assert.Equal(Path.Combine(root, directory, name), resolved);
    }

    [Fact]
    public void SavedBatchOutputRootUsesOwningDocumentDirectory()
    {
        string root = TestRoot();

        string resolved = ResolveOutputRoot(
            Path.Combine("..", "temp", "batch"),
            Path.Combine(root, "examples", "batch.gh"),
            Path.GetTempPath());

        Assert.Equal(Path.Combine(root, "temp", "batch"), resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsavedBatchOutputRootUsesSystemTemp(string? documentFilePath)
    {
        string resolved = ResolveOutputRoot(
            Path.Combine("simpledragon", "batch"),
            documentFilePath,
            Path.GetTempPath());

        Assert.Equal(
            Path.Combine(Path.GetFullPath(Path.GetTempPath()), "simpledragon", "batch"),
            resolved);
    }

    private static string? ResolveReadPath(
        string path,
        string? documentFilePath,
        string currentDirectory)
    {
        MethodInfo method = GetMethod(
            "ResolveBatchReadPath",
            new[] { typeof(string), typeof(string), typeof(string) });
        return (string?)method.Invoke(
            obj: null,
            parameters: new object?[] { path, documentFilePath, currentDirectory });
    }

    private static string ResolveOutputRoot(
        string path,
        string? documentFilePath,
        string tempDirectory)
    {
        MethodInfo method = GetMethod(
            "ResolveBatchOutputRoot",
            new[] { typeof(string), typeof(string), typeof(string) });
        return Assert.IsType<string>(method.Invoke(
            obj: null,
            parameters: new object?[] { path, documentFilePath, tempDirectory }));
    }

    private static MethodInfo GetMethod(string name, Type[] parameterTypes)
    {
        Type? component = LoadPlugin().GetType(
            "GonieGonie.SimpleDragon.Grasshopper.Components.RunSimpleDragonBatchComponent");
        Assert.NotNull(component);
        MethodInfo? method = component.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.NotNull(method);
        return method;
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
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "simple-dragon-batch-path-tests"));
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
