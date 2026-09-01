using System.Reflection;
using System.Security.Cryptography;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

public sealed class GrasshopperAssemblyTests
{
    private static readonly string[] CsvInputNames =
    {
        "GRR",
        "GRM",
        "Directory",
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
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
        GH_Component[] components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();

        Assert.EndsWith(".gha", assembly.Location, StringComparison.OrdinalIgnoreCase);
        Assert.True(components.Length >= 35);
        Assert.All(components, component => Assert.Equal("SimpleDragon", component.Category));
        Assert.Equal(components.Length, components.Select(component => component.ComponentGuid).Distinct().Count());
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
        Assert.Contains(new Guid("e0a54494-3d69-4681-8756-cc3cd86df4e1"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("11336c6a-5bd4-4d6b-80a1-89bd168f8d54"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("6e242e51-77ce-4f77-8445-a17d636c7310"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("e15d7475-e5cf-4e37-81a4-e656c69ee250"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("39e2ad8c-8fbb-40bd-84cc-218de37bb720"),
            components.Select(component => component.ComponentGuid));
        Assert.Contains(new Guid("2c0bc0e2-df1d-4e42-9b97-d841e8c83214"),
            components.Select(component => component.ComponentGuid));
        Assert.DoesNotContain(new Guid("039bf7bb-da65-49e2-80fe-86d636cf0a48"),
            components.Select(component => component.ComponentGuid));
    }

    [Fact]
    public void EveryComponentHasItsOwnEmbeddedTwentyFourPixelIcon()
    {
        const string prefix =
            "Dragons.SimpleDragon.Grasshopper.Resources.Components.";
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
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
            "Dragons.SimpleDragon.Grasshopper.Resources.Parameters.";
        Assembly assembly = typeof(SimpleDragonMaterialParam).Assembly;
        Type[] parameterTypes = ParameterTypes(assembly);
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(18, parameterTypes.Length);
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
                "Dragons.InvisibleDragon.Grasshopper.Resources.Parameters."),
            (
                typeof(SimpleDragonMaterialParam).Assembly,
                "Dragons.SimpleDragon.Grasshopper.Resources.Parameters."),
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

        Assert.Equal(37, count);
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
    public void CsvAndPlotPortsExposeStructuredData()
    {
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
        GH_Component export = Component(assembly, "ExportGreenRetrofitCsvComponent");
        GH_Component dataTree = Component(assembly, "GreenRetrofitDataTreeComponent");
        GH_Component line = Component(assembly, "GreenRetrofitMonthlyLinePlotComponent");
        GH_Component bars = Component(assembly, "GreenRetrofitMonthlyBarPlotComponent");

        Assert.Equal(CsvInputNames, export.Params.Input.Select(parameter => parameter.Name));
        Assert.Equal(CsvOutputNames, export.Params.Output.Select(parameter => parameter.Name));
        Assert.Equal(GH_ParamAccess.tree, dataTree.Params.Output[2].Access);
        Assert.Equal(GH_ParamAccess.tree, dataTree.Params.Output[3].Access);
        Assert.Equal("Lines", line.Params.Output[0].Name);
        Assert.Equal(GH_ParamAccess.tree, line.Params.Output[5].Access);
        Assert.Equal("Bars", bars.Params.Output[0].Name);
        Assert.Equal(GH_ParamAccess.tree, bars.Params.Output[0].Access);
        Assert.Equal(GH_ParamAccess.tree, bars.Params.Output[5].Access);
        foreach (GH_Component component in new[] { dataTree, line, bars })
        {
            Assert.Equal("ChoiceStringParam", component.Params.Input[1].GetType().Name);
            Assert.Equal("ChoiceStringParam", component.Params.Input[3].GetType().Name);
        }
    }

    [Fact]
    public void MonthlyConsumersKeepDirectGrrDefaultsAndWaitQuietlyForAResult()
    {
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
        GH_Component dataTree = Component(assembly, "GreenRetrofitDataTreeComponent");
        GH_Component line = Component(assembly, "GreenRetrofitMonthlyLinePlotComponent");
        GH_Component bars = Component(assembly, "GreenRetrofitMonthlyBarPlotComponent");

        foreach (GH_Component component in new[] { dataTree, line, bars })
        {
            Assert.Equal("SiteUses", StringDefault(component, 1));
            Assert.False(BooleanDefault(component, 2));
            Assert.Equal("Fuel", StringDefault(component, 3));
        }

        foreach (GH_Component component in new[] { line, bars })
        {
            Assert.Equal(Plane.WorldXY, PlaneDefault(component, 4));
            Assert.Equal(12d, NumberDefault(component, 5));
            Assert.Equal(6d, NumberDefault(component, 6));
        }

        Assert.False(BooleanDefault(bars, 7));

        foreach (GH_Component component in new[] { dataTree, line, bars })
        {
            IGH_DataAccess access = DispatchProxy.Create<IGH_DataAccess, MissingResultDataAccess>();
            var probe = Assert.IsAssignableFrom<MissingResultDataAccess>(access);

            InvokeSolve(component, access);

            Assert.Empty(component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
            Assert.Equal(0, probe.OutputSetCount);
        }
    }

    [Fact]
    public void MonthlyPlotStillRejectsInvalidConnectedAdvancedInputs()
    {
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
        GH_Component line = Component(assembly, "GreenRetrofitMonthlyLinePlotComponent");
        string fixture = Path.Combine(
            FindRepositoryRoot(AppContext.BaseDirectory),
            "fixtures",
            "simple-dragon",
            "grr",
            "ASHRAE 140 modified.grr");
        IGH_DataAccess access = DispatchProxy.Create<IGH_DataAccess, MissingResultDataAccess>();
        var probe = Assert.IsAssignableFrom<MissingResultDataAccess>(access);
        probe.Inputs.Add(0, new GreenRetrofitResultGoo(GrrReader.ReadFile(fixture).RequireResult()));
        probe.Inputs.Add(1, "not-a-metric");
        probe.Inputs.Add(2, false);
        probe.Inputs.Add(3, "Fuel");
        probe.Inputs.Add(4, Plane.WorldXY);
        probe.Inputs.Add(5, 12d);
        probe.Inputs.Add(6, 6d);

        InvokeSolve(line, access);

        string error = Assert.Single(line.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        Assert.Contains("Metric must be", error, StringComparison.Ordinal);
        Assert.Equal(0, probe.OutputSetCount);
    }

    [Fact]
    public void MonthlyDataTreesAppendSeriesToTheCurrentResultPath()
    {
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
        string fixture = Path.Combine(
            FindRepositoryRoot(AppContext.BaseDirectory),
            "fixtures",
            "simple-dragon",
            "grr",
            "ASHRAE 140 modified.grr");
        var result = new GreenRetrofitResultGoo(GrrReader.ReadFile(fixture).RequireResult());
        var targetPath = new GH_Path(4, 2);

        GH_Component dataTree = Component(assembly, "GreenRetrofitDataTreeComponent");
        MissingResultDataAccess dataTreeProbe = Probe(
            targetPath,
            result,
            includePlotInputs: false);
        InvokeSolve(dataTree, dataTreeProbe.Access);

        AssertSeriesPaths<GH_Number>(dataTreeProbe.OutputTrees[2], targetPath);
        AssertSeriesPaths<GH_Number>(dataTreeProbe.OutputTrees[3], targetPath);
    }

    [Fact]
    public void CanonicalComponentsHaveNoHiddenOrRelationshipStageResidue()
    {
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
        GH_Component[] components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();
        string[] forbiddenStageWords = { "Assign", "Assemble", "Extract" };

        Assert.All(components, component =>
        {
            Assert.NotEqual(GH_Exposure.hidden, component.Exposure);
            Assert.All(forbiddenStageWords, word =>
            {
                Assert.DoesNotContain(word, component.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(word, component.NickName, StringComparison.OrdinalIgnoreCase);
            });
            Assert.All(component.Params.Input, input =>
            {
                Assert.DoesNotContain("Index", input.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Indices", input.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Index", input.Description, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Indices", input.Description, StringComparison.OrdinalIgnoreCase);
            });
        });
    }

    [Fact]
    public void PublicComponentsDoNotExposeEntityOrCaseIdentifierInputs()
    {
        Assembly assembly = LoadPlugin("Dragons.SimpleDragon.GH");
        GH_Component[] components = ComponentTypes(assembly)
            .Select(type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)))
            .ToArray();

        Assert.DoesNotContain(
            components.SelectMany(component => component.Params.Input),
            parameter => string.Equals(parameter.Name, "ID", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.EndsWith(" ID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parameter.NickName, "ID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicGuidsDoNotOverlapInvisibleDragon()
    {
        Guid[] simpleParameters =
        {
            new SimpleDragonDiagnosticParam().ComponentGuid,
            new SimpleDragonMaterialParam().ComponentGuid,
            new SimpleDragonSurfaceConstructionLayerParam().ComponentGuid,
            new SimpleDragonSurfaceConstructionParam().ComponentGuid,
            new SimpleDragonFenestrationConstructionParam().ComponentGuid,
            new SimpleDragonUsageProfileParam().ComponentGuid,
            new SimpleDragonSurfaceParam().ComponentGuid,
            new SimpleDragonZoneParam().ComponentGuid,
            new SimpleDragonOpeningDefinitionParam().ComponentGuid,
            new SimpleDragonSurfaceDefinitionParam().ComponentGuid,
            new SimpleDragonZoneDefinitionParam().ComponentGuid,
            new SimpleDragonSourceSystemParam().ComponentGuid,
            new SimpleDragonSupplySystemParam().ComponentGuid,
            new SimpleDragonZoneErvParam().ComponentGuid,
            new SimpleDragonPhotovoltaicPanelParam().ComponentGuid,
            new GreenRetrofitModelParam().ComponentGuid,
            new SimpleDragonBatchCaseParam().ComponentGuid,
            new GreenRetrofitResultParam().ComponentGuid,
        };
        Guid[] invisibleParameters =
        {
            new DragonMaterialParam().ComponentGuid,
            new DragonLayerParam().ComponentGuid,
            new DragonConstructionParam().ComponentGuid,
            new DragonGlazingParam().ComponentGuid,
            new DragonScheduleParam().ComponentGuid,
            new DragonProfileParam().ComponentGuid,
            new DragonSurfaceParam().ComponentGuid,
            new DragonOpeningParam().ComponentGuid,
            new DragonZoneDefinitionParam().ComponentGuid,
            new DragonEnergyModelParam().ComponentGuid,
            new DragonSourceSystemParam().ComponentGuid,
            new DragonSupplySystemParam().ComponentGuid,
            new DragonDomesticHotWaterParam().ComponentGuid,
            new DragonEnergyRecoveryVentilatorParam().ComponentGuid,
            new DragonPhotovoltaicPanelParam().ComponentGuid,
            new DragonIdfParam().ComponentGuid,
            new EnergyPlusResultParam().ComponentGuid,
            new PreparedWeatherFileParam().ComponentGuid,
            new DiagnosticParam().ComponentGuid,
        };
        Guid[] simpleComponents = ComponentTypes(LoadPlugin("Dragons.SimpleDragon.GH"))
            .Select(type => ((GH_Component)Activator.CreateInstance(type)!).ComponentGuid)
            .ToArray();
        Guid[] invisibleComponents = ComponentTypes(LoadPlugin("Dragons.InvisibleDragon.GH"))
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
                && type.Namespace == "Dragons.SimpleDragon.Grasshopper.Parameters")
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

    private static string StringDefault(GH_Component component, int index) =>
        Assert.IsType<GH_String>(Assert.Single(
            Assert.IsAssignableFrom<Param_String>(component.Params.Input[index]).PersistentData.AllData(true))).Value;

    private static bool BooleanDefault(GH_Component component, int index) =>
        Assert.IsType<GH_Boolean>(Assert.Single(
            Assert.IsType<Param_Boolean>(component.Params.Input[index]).PersistentData.AllData(true))).Value;

    private static double NumberDefault(GH_Component component, int index) =>
        Assert.IsType<GH_Number>(Assert.Single(
            Assert.IsType<Param_Number>(component.Params.Input[index]).PersistentData.AllData(true))).Value;

    private static Plane PlaneDefault(GH_Component component, int index) =>
        Assert.IsType<GH_Plane>(Assert.Single(
            Assert.IsType<Param_Plane>(component.Params.Input[index]).PersistentData.AllData(true))).Value;

    private static void InvokeSolve(GH_Component component, IGH_DataAccess access)
    {
        MethodInfo solve = Assert.IsAssignableFrom<MethodInfo>(component.GetType().BaseType?.GetMethod(
            "SolveInstance",
            BindingFlags.Instance | BindingFlags.NonPublic));
        solve.Invoke(component, new object[] { access });
    }

    private static MissingResultDataAccess Probe(
        GH_Path targetPath,
        GreenRetrofitResultGoo result,
        bool includePlotInputs)
    {
        IGH_DataAccess access = DispatchProxy.Create<IGH_DataAccess, MissingResultDataAccess>();
        var probe = Assert.IsAssignableFrom<MissingResultDataAccess>(access);
        probe.Access = access;
        probe.TargetPath = targetPath;
        probe.Inputs.Add(0, result);
        probe.Inputs.Add(1, "SiteUses");
        probe.Inputs.Add(2, false);
        probe.Inputs.Add(3, "Fuel");
        if (includePlotInputs)
        {
            probe.Inputs.Add(4, Plane.WorldXY);
            probe.Inputs.Add(5, 12d);
            probe.Inputs.Add(6, 6d);
        }

        return probe;
    }

    private static void AssertSeriesPaths<T>(object value, GH_Path targetPath)
        where T : IGH_Goo
    {
        GH_Structure<T> tree = Assert.IsType<GH_Structure<T>>(value);
        Assert.NotEmpty(tree.Paths);
        for (int index = 0; index < tree.PathCount; index++)
        {
            Assert.Equal(targetPath.AppendElement(index), tree.Paths[index]);
        }
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass of this probe.")]
    private class MissingResultDataAccess : DispatchProxy
    {
        internal IGH_DataAccess Access { get; set; } = null!;

        internal Dictionary<int, object?> Inputs { get; } = new();

        internal Dictionary<int, object> OutputTrees { get; } = new();

        internal GH_Path TargetPath { get; set; } = new(0);

        internal int OutputSetCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            if (string.Equals(targetMethod!.Name, "GetData", StringComparison.Ordinal)
                && args is { Length: 2 }
                && args[0] is int inputIndex
                && Inputs.TryGetValue(inputIndex, out object? inputValue))
            {
                args[1] = inputValue;
                return true;
            }

            if (string.Equals(targetMethod.Name, "ParameterTargetPath", StringComparison.Ordinal))
            {
                return TargetPath;
            }

            if (string.Equals(targetMethod.Name, "SetDataTree", StringComparison.Ordinal)
                && args is { Length: 2 }
                && args[0] is int outputIndex
                && args[1] is not null)
            {
                OutputTrees[outputIndex] = args[1]!;
                OutputSetCount++;
                return true;
            }

            if (targetMethod!.Name.StartsWith("SetData", StringComparison.Ordinal))
            {
                OutputSetCount++;
            }

            if (targetMethod.ReturnType == typeof(bool))
            {
                return false;
            }

            if (targetMethod.ReturnType == typeof(int))
            {
                return 0;
            }

            return null;
        }
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
