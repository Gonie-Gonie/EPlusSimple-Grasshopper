using System.Reflection;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class ConvertIddPathResolutionTests
{
    [Fact]
    public void ExplicitRelativeIddFileUsesOwningDocumentDirectory()
    {
        string root = TestRoot();
        string documentPath = Path.Combine(root, "examples", "workflow.gh");

        string resolved = ResolveIddPath(
            Path.Combine("..", "runtime", "Energy+.idd"),
            documentPath,
            Path.Combine(root, "rhino-system"));

        Assert.Equal(Path.Combine(root, "runtime", "Energy+.idd"), resolved);
    }

    [Fact]
    public void ExplicitRelativeEnergyPlusDirectoryUsesOwningDocumentDirectory()
    {
        string root = TestRoot();
        string documentPath = Path.Combine(root, "examples", "workflow.gh");
        string runtimeDirectory = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtimeDirectory);

        try
        {
            string resolved = ResolveIddPath(
                Path.Combine("..", "runtime"),
                documentPath,
                Path.Combine(root, "rhino-system"));

            Assert.Equal(Path.Combine(runtimeDirectory, "Energy+.idd"), resolved);
        }
        finally
        {
            Directory.Delete(runtimeDirectory, recursive: true);
        }
    }

    private static string ResolveIddPath(
        string path,
        string documentFilePath,
        string currentDirectory)
    {
        Assembly assembly = LoadPlugin();
        Type? componentType = assembly.GetType(
            "GonieGonie.SimpleDragon.Grasshopper.Components.ConvertGreenRetrofitModelComponent");
        Assert.NotNull(componentType);

        MethodInfo? method = componentType.GetMethod(
            "ResolveExplicitIddPath",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(string) },
            modifiers: null);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(
            obj: null,
            parameters: new object?[] { path, documentFilePath, currentDirectory }));
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
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "simple-dragon-convert-idd-path-tests"));
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
