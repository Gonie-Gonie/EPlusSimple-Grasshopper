using System.Reflection;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class GrasshopperAssemblyTests
{
    [Fact]
    public void PluginAssemblyUsesGhaExtensionAndLoadsFourteenComponents()
    {
        Assembly assembly = LoadPlugin("GonieGonie.SimpleDragon.GH");
        GH_Component[] components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();

        Assert.EndsWith(".gha", assembly.Location, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(14, components.Length);
        Assert.All(components, component => Assert.Equal("SimpleDragon", component.Category));
        Assert.Equal(components.Length, components.Select(component => component.ComponentGuid).Distinct().Count());
        Assert.Contains(new Guid("b38f2e41-f63b-42a8-b549-65cd60c7a994"),
            components.Select(component => component.ComponentGuid));
    }

    [Fact]
    public void PublicGuidsDoNotOverlapInvisibleDragon()
    {
        Guid[] simpleParameters =
        {
            new SimpleDragonMaterialParam().ComponentGuid,
            new SimpleDragonSurfaceConstructionParam().ComponentGuid,
            new SimpleDragonFenestrationConstructionParam().ComponentGuid,
            new SimpleDragonUsageProfileParam().ComponentGuid,
            new SimpleDragonSurfaceParam().ComponentGuid,
            new SimpleDragonZoneParam().ComponentGuid,
            new GreenRetrofitModelParam().ComponentGuid,
            new GreenRetrofitResultParam().ComponentGuid,
        };
        Guid[] invisibleParameters =
        {
            new DragonMaterialParam().ComponentGuid,
            new DragonConstructionParam().ComponentGuid,
            new DragonScheduleParam().ComponentGuid,
            new DragonProfileParam().ComponentGuid,
            new DragonSurfaceParam().ComponentGuid,
            new DragonZoneParam().ComponentGuid,
            new DragonEnergyModelParam().ComponentGuid,
            new DragonIdfParam().ComponentGuid,
            new EnergyPlusResultParam().ComponentGuid,
            new DiagnosticParam().ComponentGuid,
        };
        Guid[] simpleComponents = ComponentTypes(LoadPlugin("GonieGonie.SimpleDragon.GH"))
            .Select(type => ((GH_Component)Activator.CreateInstance(type)!).ComponentGuid)
            .ToArray();
        Guid[] invisibleComponents = ComponentTypes(LoadPlugin("GonieGonie.InvisibleDragon.GH"))
            .Select(type => ((GH_Component)Activator.CreateInstance(type)!).ComponentGuid)
            .ToArray();
        Guid[] all = simpleParameters
            .Concat(invisibleParameters)
            .Concat(simpleComponents)
            .Concat(invisibleComponents)
            .ToArray();

        Assert.Equal(all.Length, all.Distinct().Count());
    }

    private static Type[] ComponentTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(GH_Component).IsAssignableFrom(type))
            .ToArray();
    }

    private static Assembly LoadPlugin(string assemblyName)
    {
        string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string path = Path.Combine(
            repositoryRoot,
            "temp",
            "build",
            "bin",
            assemblyName,
            "Release",
            "net8.0-windows",
            assemblyName + ".gha");
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

        throw new DirectoryNotFoundException("Could not locate the Dragons.Grasshopper.sln repository root.");
    }
}
