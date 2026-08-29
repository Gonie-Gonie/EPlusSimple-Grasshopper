using System.Reflection;
using System.Security.Cryptography;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

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

    private static readonly string[] ConvertOutputNames =
    {
        "Energy Model",
        "IDF",
        "IDF Text",
        "EPW File",
        "Success",
        "Diagnostics",
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
        Assert.Contains(new Guid("49b71334-f6f0-4964-b1ed-c80e03a3a574"),
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

    [Fact]
    public void EveryParameterHasItsOwnEmbeddedTwentyFourPixelIcon()
    {
        const string prefix =
            "GonieGonie.SimpleDragon.Grasshopper.Resources.Parameters.";
        Assembly assembly = typeof(SimpleDragonMaterialParam).Assembly;
        Type[] parameterTypes = ParameterTypes(assembly);
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(15, parameterTypes.Length);
        Assert.Equal(parameterTypes.Length, resources.Length);
        var resourceHashes = new HashSet<string>(StringComparer.Ordinal);
        var runtimeHashes = new HashSet<string>(StringComparer.Ordinal);
        Bitmap? defaultIcon = new NullIconStringParam().Icon_24x24;
        Assert.NotNull(defaultIcon);
        string defaultHash = PixelHash(defaultIcon);

        foreach (Type type in parameterTypes)
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
            Assert.True(
                resourceHashes.Add(Convert.ToHexString(sha.ComputeHash(stream))),
                resourceName);

            GH_DocumentObject parameter = Assert.IsAssignableFrom<GH_DocumentObject>(
                Activator.CreateInstance(type));
            Bitmap? icon = parameter.Icon_24x24;
            Assert.NotNull(icon);
            Assert.Equal(24, icon.Width);
            Assert.Equal(24, icon.Height);
            string runtimeHash = PixelHash(icon);
            Assert.NotEqual(defaultHash, runtimeHash);
            Assert.True(runtimeHashes.Add(runtimeHash), type.FullName);
        }
    }

    [Fact]
    public void ParameterIconResourcesAreByteUniqueAcrossBothProducts()
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        foreach ((Assembly Assembly, string Prefix) source in new[]
        {
            (
                typeof(DragonMaterialParam).Assembly,
                "GonieGonie.InvisibleDragon.Grasshopper.Resources.Parameters."),
            (
                typeof(SimpleDragonMaterialParam).Assembly,
                "GonieGonie.SimpleDragon.Grasshopper.Resources.Parameters."),
        })
        {
            foreach (string resourceName in source.Assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(source.Prefix, StringComparison.Ordinal)))
            {
                using Stream stream = Assert.IsAssignableFrom<Stream>(
                    source.Assembly.GetManifestResourceStream(resourceName));
                using SHA256 sha = SHA256.Create();
                Assert.True(
                    hashes.Add(Convert.ToHexString(sha.ComputeHash(stream))),
                    resourceName);
                count++;
            }
        }

        Assert.Equal(31, count);
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
    public void WeatherAutomationPreservesPortsAndAdvertisesExecutableReadiness()
    {
        Assembly assembly = LoadPlugin("GonieGonie.SimpleDragon.GH");
        GH_Component convert = Component(assembly, "ConvertGreenRetrofitModelComponent");
        GH_Component batch = Component(assembly, "RunSimpleDragonBatchComponent");

        Assert.Equal(ConvertOutputNames, convert.Params.Output.Select(parameter => parameter.Name));
        Assert.Contains(
            "automatically selected",
            convert.Params.Output[3].Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "EPW is ready",
            convert.Params.Output[4].Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "packaged SimpleDragon weather archive",
            batch.Params.Input[2].Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "saved True value does not run",
            batch.Params.Input[6].Description,
            StringComparison.OrdinalIgnoreCase);
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
            new SimpleDragonOpeningDefinitionParam().ComponentGuid,
            new SimpleDragonZoneDefinitionParam().ComponentGuid,
            new SimpleDragonSourceSystemParam().ComponentGuid,
            new SimpleDragonSupplySystemParam().ComponentGuid,
            new SimpleDragonEnergyRecoveryVentilatorParam().ComponentGuid,
            new SimpleDragonVentilationAssignmentParam().ComponentGuid,
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
            new PreparedWeatherFileParam().ComponentGuid,
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

    private static Type[] ParameterTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => type.IsPublic
                && !type.IsAbstract
                && typeof(IGH_Param).IsAssignableFrom(type)
                && type.Namespace == "GonieGonie.SimpleDragon.Grasshopper.Parameters")
            .ToArray();
    }

    private static string PixelHash(Bitmap bitmap)
    {
        byte[] bytes = new byte[bitmap.Width * bitmap.Height * sizeof(int)];
        int offset = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                BitConverter.GetBytes(bitmap.GetPixel(x, y).ToArgb()).CopyTo(bytes, offset);
                offset += sizeof(int);
            }
        }

        return Convert.ToHexString(SHA256.HashData(bytes));
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

    private sealed class NullIconStringParam : GH_PersistentParam<GH_String>
    {
        internal NullIconStringParam()
            : base("Default Icon Probe", "Probe", "Default icon probe.", "Tests", "Tests")
        {
        }

        public override Guid ComponentGuid => new("0e346f03-ce9a-48dd-898e-ea2540902304");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap? Icon => null;

        protected override GH_GetterResult Prompt_Singular(ref GH_String value)
        {
            return GH_GetterResult.cancel;
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_String> values)
        {
            return GH_GetterResult.cancel;
        }
    }
}
