using System.Reflection;
using System.Security.Cryptography;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using Grasshopper.Kernel;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class GrasshopperAssemblyTests
{
    private static readonly string[] GeometryOutputNames =
    {
        "Zones",
        "Surfaces",
        "Geometry Map",
        "Diagnostics",
        "Geometry Map Data",
    };

    private static readonly string[] CsvInputNames =
    {
        "GRR",
        "GRM",
        "Directory",
        "Case ID",
        "Diagnostics",
        "Geometry Map Data",
        "Export",
        "Overwrite",
    };

    private static readonly string[] CsvOutputNames =
    {
        "Full Directory",
        "File Names",
        "File Paths",
        "Content",
        "Written",
    };

    [Fact]
    public void PluginAssemblyUsesGhaExtensionAndLoadsRequiredComponents()
    {
        Assembly assembly = LoadPlugin("GonieGonie.SimpleDragon.GH");
        GH_Component[] components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();

        Assert.EndsWith(".gha", assembly.Location, StringComparison.OrdinalIgnoreCase);
        Assert.True(components.Length >= 35);
        Assert.All(components, component => Assert.Equal("SimpleDragon", component.Category));
        Assert.Equal(components.Length, components.Select(component => component.ComponentGuid).Distinct().Count());
        Assert.Contains(new Guid("b38f2e41-f63b-42a8-b549-65cd60c7a994"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("9fe8a410-ea95-4eb8-81ec-56c45cdd029c"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("cb5a98f8-4188-4323-b55d-795b4a7ba20e"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("76e0c1b6-68d6-4cdc-a418-eea18aa131c1"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("a73acba4-d98d-4fec-a846-dc982256d6b1"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("e6e14d7b-55b4-45a9-97f9-9b99715f5ebc"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("5f66b3fd-e69c-4c33-92db-839c07dcbda5"),
            components.Select(component => component.ComponentGuid));
    }

    [Fact]
    public void EveryComponentHasItsOwnEmbeddedTwentyFourPixelIcon()
    {
        const string prefix =
            "GonieGonie.SimpleDragon.Grasshopper.Resources.Components.";
        Assembly assembly = LoadPlugin("GonieGonie.SimpleDragon.GH");
        Type[] componentTypes = ComponentTypes(assembly);
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(componentTypes.Length, resources.Length);
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (Type type in componentTypes)
        {
            string resourceName = prefix + type.Name + ".png";
            Assert.Contains(resourceName, resources);
            using Stream stream = Assert.IsAssignableFrom<Stream>(
                assembly.GetManifestResourceStream(resourceName));
            using var bitmap = new Bitmap(stream);
            Assert.Equal(24, bitmap.Width);
            Assert.Equal(24, bitmap.Height);
            AssertTransparentBorder(bitmap);

            stream.Position = 0;
            using SHA256 sha = SHA256.Create();
            Assert.True(hashes.Add(Convert.ToHexString(sha.ComputeHash(stream))), resourceName);

            GH_Component component = Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
            Bitmap? icon = component.Icon_24x24;
            Assert.NotNull(icon);
            Assert.Equal(24, icon.Width);
            Assert.Equal(24, icon.Height);
        }
    }

    private static void AssertTransparentBorder(Bitmap bitmap)
    {
        for (int pixel = 0; pixel < 24; pixel++)
        {
            foreach (int edge in new[] { 0, 1, 22, 23 })
            {
                Assert.Equal(0, bitmap.GetPixel(edge, pixel).A);
                Assert.Equal(0, bitmap.GetPixel(pixel, edge).A);
            }
        }
    }

    [Fact]
    public void GeometryAndCsvPlotPortsPreserveOldOrderAndExposeStructuredData()
    {
        Assembly assembly = LoadPlugin("GonieGonie.SimpleDragon.GH");
        GH_Component geometry = Component(assembly, "ExtractSimpleDragonZonesComponent");
        GH_Component export = Component(assembly, "ExportGreenRetrofitCsvComponent");
        GH_Component dataTree = Component(assembly, "GreenRetrofitDataTreeComponent");
        GH_Component line = Component(assembly, "GreenRetrofitMonthlyLinePlotComponent");
        GH_Component bars = Component(assembly, "GreenRetrofitMonthlyBarPlotComponent");

        Assert.Equal(GeometryOutputNames, geometry.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal(CsvInputNames, export.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(CsvOutputNames, export.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal(GH_ParamAccess.tree, dataTree.Params.Output[2].Access);
        Assert.Equal(GH_ParamAccess.tree, dataTree.Params.Output[3].Access);
        Assert.Equal("Lines", line.Params.Output[0].Name);
        Assert.Equal(GH_ParamAccess.tree, line.Params.Output[5].Access);
        Assert.Equal("Bars", bars.Params.Output[0].Name);
        Assert.Equal(GH_ParamAccess.tree, bars.Params.Output[0].Access);
        Assert.Equal(GH_ParamAccess.tree, bars.Params.Output[5].Access);
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
            new SimpleDragonSourceSystemParam().ComponentGuid,
            new SimpleDragonSupplySystemParam().ComponentGuid,
            new SimpleDragonEnergyRecoveryVentilatorParam().ComponentGuid,
            new SimpleDragonPhotovoltaicPanelParam().ComponentGuid,
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
            new DragonSourceSystemParam().ComponentGuid,
            new DragonSupplySystemParam().ComponentGuid,
            new DragonEnergyRecoveryVentilatorParam().ComponentGuid,
            new DragonPhotovoltaicPanelParam().ComponentGuid,
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

    private static GH_Component Component(Assembly assembly, string typeName)
    {
        Type type = Assert.Single(ComponentTypes(assembly), type => type.Name == typeName);
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
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
