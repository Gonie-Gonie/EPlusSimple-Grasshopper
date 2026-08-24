using System.Reflection;
using Grasshopper.Kernel;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class GrasshopperAssemblyTests
{
    [Fact]
    public void PublicComponentsConstructWithUniqueStableGuids()
    {
        Assembly assembly = LoadPlugin();
        List<GH_Component> components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToList();

        Assert.All(components, component => Assert.Equal("InvisibleDragon", component.Category));
        Assert.Equal(components.Count, components.Select(component => component.ComponentGuid).Distinct().Count());
        Assert.Contains(new Guid("5f1a9663-6f81-4635-b54d-607b48c9fd47"), components.Select(component => component.ComponentGuid));
    }

    [Fact]
    public void PluginAssemblyUsesGrasshopperExtensionAndAllComponentsLoad()
    {
        Assembly assembly = LoadPlugin();
        Type[] componentTypes = ComponentTypes(assembly);

        Assert.EndsWith(".gha", assembly.Location, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(14, componentTypes.Length);
        Assert.All(componentTypes, type => Assert.NotNull(Activator.CreateInstance(type)));
    }

    private static Type[] ComponentTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(GH_Component).IsAssignableFrom(type))
            .ToArray();
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
        Assert.True(File.Exists(path), $"Expected built Grasshopper assembly at '{path}'.");
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

        throw new DirectoryNotFoundException("Could not locate the Dragons.Grasshopper.sln repository root.");
    }
}
