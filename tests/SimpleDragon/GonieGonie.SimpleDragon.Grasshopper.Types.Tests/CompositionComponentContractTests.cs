using System.Collections;
using System.Reflection;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class CompositionComponentContractTests
{
    private static readonly ComponentContract[] CanonicalComponents =
    {
        new(
            "CreateSimpleDragonOpeningComponent",
            new Guid("7d41fd2c-b93f-4fc8-88ea-db1f3abeb2f1"),
            "SimpleDragon Opening",
            "SD Opening",
            "Geometry",
            new[]
            {
                Input("Boundary", "Param_Curve", GH_ParamAccess.item),
                Input("Name", "Param_String", GH_ParamAccess.item),
                Input("Type", "Param_Integer", GH_ParamAccess.item),
                Input("Construction", "SimpleDragonFenestrationConstructionParam", GH_ParamAccess.item, optional: true),
                Input("Blind", "Param_String", GH_ParamAccess.item),
                Input("ID", "Param_String", GH_ParamAccess.item),
            },
            new[]
            {
                Output("Opening", "SimpleDragonOpeningDefinitionParam", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
        new(
            "CreateSimpleDragonZoneComponent",
            new Guid("f7389ac4-51dd-44dc-803a-e8e0989e7638"),
            "SimpleDragon Zone",
            "SD Zone",
            "Geometry",
            new[]
            {
                Input("Zone Brep", "Param_Brep", GH_ParamAccess.item),
                Input("Name", "Param_String", GH_ParamAccess.item),
                Input("Floor Number", "Param_Integer", GH_ParamAccess.item),
                Input("Profile", "SimpleDragonUsageProfileParam", GH_ParamAccess.item),
                Input("Surface Construction", "SimpleDragonSurfaceConstructionParam", GH_ParamAccess.item, optional: true),
                Input("Opening Construction", "SimpleDragonFenestrationConstructionParam", GH_ParamAccess.item, optional: true),
                Input("Openings", "SimpleDragonOpeningDefinitionParam", GH_ParamAccess.list, optional: true),
                Input("HVAC", "SimpleDragonSupplySystemParam", GH_ParamAccess.list, optional: true),
                Input("Ventilation", "SimpleDragonVentilationAssignmentParam", GH_ParamAccess.list, optional: true),
                Input("Floor Boundary", "Param_String", GH_ParamAccess.item),
                Input("Lighting Power Density", "Param_Number", GH_ParamAccess.item),
            },
            new[]
            {
                Output("Zone", "SimpleDragonZoneDefinitionParam", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
        new(
            "CreateSimpleDragonVentilationAssignmentComponent",
            new Guid("596158ca-aaa8-42e5-b22a-b4cfbead4a36"),
            "SimpleDragon ERV Assignment",
            "SD ERV Count",
            "Model",
            new[]
            {
                Input("ERV", "SimpleDragonEnergyRecoveryVentilatorParam", GH_ParamAccess.item),
                Input("Count", "Param_Integer", GH_ParamAccess.item),
            },
            new[]
            {
                Output("Ventilation", "SimpleDragonVentilationAssignmentParam", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
        new(
            "CreateSimpleDragonModelComponent",
            new Guid("ce38124b-f99b-4d09-be3b-e5e5717db707"),
            "SimpleDragon Model",
            "SD Model",
            "Model",
            new[]
            {
                Input("Name", "Param_String", GH_ParamAccess.item),
                Input("Zones", "SimpleDragonZoneDefinitionParam", GH_ParamAccess.list),
                Input("North Axis", "Param_Number", GH_ParamAccess.item),
                Input("Address", "Param_String", GH_ParamAccess.item),
                Input("Vintage", "Param_String", GH_ParamAccess.item),
                Input("Multifamily Housing", "Param_Boolean", GH_ParamAccess.item),
                Input("Photovoltaic Panels", "SimpleDragonPhotovoltaicPanelParam", GH_ParamAccess.list, optional: true),
            },
            new[]
            {
                Output("GRM", "GreenRetrofitModelParam", GH_ParamAccess.item),
                Output("Zones", "SimpleDragonZoneParam", GH_ParamAccess.list),
                Output("Surfaces", "SimpleDragonSurfaceParam", GH_ParamAccess.list),
                Output("Geometry Map", "Param_String", GH_ParamAccess.list),
                Output("Geometry Map Data", "Param_GenericObject", GH_ParamAccess.list),
                Output("JSON", "Param_String", GH_ParamAccess.item),
                Output("Floor Area", "Param_Number", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
        new(
            "PrepareSimpleDragonSimulationComponent",
            new Guid("ca666fd7-788c-4682-8b04-fad8c7252fe0"),
            "Prepare SimpleDragon Simulation",
            "SD to IDF",
            "Model",
            new[]
            {
                Input("GRM", "GreenRetrofitModelParam", GH_ParamAccess.item),
            },
            new[]
            {
                Output("Energy Model", "DragonEnergyModelParam", GH_ParamAccess.item),
                Output("IDF", "DragonIdfParam", GH_ParamAccess.item),
                Output("Weather", "PreparedWeatherFileParam", GH_ParamAccess.item),
                Output("Success", "Param_Boolean", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
    };

    private static readonly ComponentContract[] LegacyComponents =
    {
        new(
            "ExtractSimpleDragonZonesComponent",
            new Guid("668591e2-458a-42a2-a924-6c3862f1b2c6"),
            null,
            null,
            null,
            new[]
            {
                Input("Zone Breps", "Param_Brep", GH_ParamAccess.list),
                Input("Names", "Param_String", GH_ParamAccess.list, optional: true),
                Input("Floor Numbers", "Param_Integer", GH_ParamAccess.list, optional: true),
                Input("Profile", "SimpleDragonUsageProfileParam", GH_ParamAccess.item),
                Input("Surface Construction", "SimpleDragonSurfaceConstructionParam", GH_ParamAccess.item, optional: true),
                Input("Fenestration Construction", "SimpleDragonFenestrationConstructionParam", GH_ParamAccess.item, optional: true),
                Input("Unmatched Floor Boundary", "Param_String", GH_ParamAccess.item),
                Input("Lighting Power Density", "Param_Number", GH_ParamAccess.item),
                Input("Opening Curves", "Param_Curve", GH_ParamAccess.list, optional: true),
                Input("Opening Zone Indices", "Param_Integer", GH_ParamAccess.list, optional: true),
                Input("Opening Face Indices", "Param_Integer", GH_ParamAccess.list, optional: true),
            },
            new[]
            {
                Output("Zones", "SimpleDragonZoneParam", GH_ParamAccess.list),
                Output("Surfaces", "SimpleDragonSurfaceParam", GH_ParamAccess.list),
                Output("Geometry Map", "Param_String", GH_ParamAccess.list),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
                Output("Geometry Map Data", "Param_GenericObject", GH_ParamAccess.list),
            }),
        new(
            "AssignSimpleDragonSupplySystemsComponent",
            new Guid("82b8b48c-5930-4649-bc5f-6c17b05daa52"),
            null,
            null,
            null,
            new[]
            {
                Input("Zone", "SimpleDragonZoneParam", GH_ParamAccess.item),
                Input("Supplies", "SimpleDragonSupplySystemParam", GH_ParamAccess.list),
                Input("Replace Existing", "Param_Boolean", GH_ParamAccess.item),
            },
            new[]
            {
                Output("Zone", "SimpleDragonZoneParam", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
        new(
            "AssignSimpleDragonVentilationSystemsComponent",
            new Guid("5f66b3fd-e69c-4c33-92db-839c07dcbda5"),
            null,
            null,
            null,
            new[]
            {
                Input("Zone", "SimpleDragonZoneParam", GH_ParamAccess.item),
                Input("ERVs", "SimpleDragonEnergyRecoveryVentilatorParam", GH_ParamAccess.list),
                Input("Counts", "Param_Integer", GH_ParamAccess.list, optional: true),
                Input("Replace Existing", "Param_Boolean", GH_ParamAccess.item),
            },
            new[]
            {
                Output("Zone", "SimpleDragonZoneParam", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
        new(
            "AssembleGreenRetrofitModelComponent",
            new Guid("f0a131e0-7cfe-45fc-945a-7e52237535ee"),
            null,
            null,
            null,
            new[]
            {
                Input("Name", "Param_String", GH_ParamAccess.item),
                Input("Zones", "SimpleDragonZoneParam", GH_ParamAccess.list),
                Input("North Axis", "Param_Number", GH_ParamAccess.item),
                Input("Address", "Param_String", GH_ParamAccess.item),
                Input("Vintage", "Param_String", GH_ParamAccess.item),
                Input("Multifamily Housing", "Param_Boolean", GH_ParamAccess.item),
                Input("Materials", "SimpleDragonMaterialParam", GH_ParamAccess.list, optional: true),
                Input("Surface Constructions", "SimpleDragonSurfaceConstructionParam", GH_ParamAccess.list, optional: true),
                Input("Fenestration Constructions", "SimpleDragonFenestrationConstructionParam", GH_ParamAccess.list, optional: true),
                Input("Source Systems", "SimpleDragonSourceSystemParam", GH_ParamAccess.list, optional: true),
                Input("Supply Systems", "SimpleDragonSupplySystemParam", GH_ParamAccess.list, optional: true),
                Input("ERV Systems", "SimpleDragonEnergyRecoveryVentilatorParam", GH_ParamAccess.list, optional: true),
                Input("Photovoltaic Panels", "SimpleDragonPhotovoltaicPanelParam", GH_ParamAccess.list, optional: true),
            },
            new[]
            {
                Output("GRM", "GreenRetrofitModelParam", GH_ParamAccess.item),
                Output("JSON", "Param_String", GH_ParamAccess.item),
                Output("Floor Area", "Param_Number", GH_ParamAccess.item),
                Output("Diagnostics", "DiagnosticParam", GH_ParamAccess.list),
            }),
    };

    [Fact]
    public void CanonicalCompositionComponentsExposeStableTypedContracts()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = CanonicalComponents
            .Select(contract => AssertComponent(assembly, contract, assertPresentation: true))
            .ToArray();

        Assert.Equal(components.Length, components.Select(component => component.ComponentGuid).Distinct().Count());
        Assert.All(components, component => Assert.Equal("SimpleDragon", component.Category));
    }

    [Fact]
    public void CanonicalPrepareUsesExecutableEnergyPlus242HvacLayout()
    {
        Assembly assembly = LoadPlugin();
        Type componentType = assembly.GetType(
            "GonieGonie.SimpleDragon.Grasshopper.Components.PrepareSimpleDragonSimulationComponent",
            throwOnError: true)!;
        MethodInfo createOptions = Assert.IsAssignableFrom<MethodInfo>(componentType.GetMethod(
            "CreateExecutionIdfOptions",
            BindingFlags.Static | BindingFlags.NonPublic));

        EnergyModelIdfOptions options = Assert.IsType<EnergyModelIdfOptions>(createOptions.Invoke(null, null));

        Assert.False(options.UseLegacySimpleDragonHvacTopology);
        Assert.True(options.UseLegacySimpleDragonScheduleMetadata);
        Assert.True(options.UseLegacySimpleDragonVentilation);
        Assert.False(options.ThrowOnValidationErrors);

        Type positioningType = assembly.GetType(
            "GonieGonie.SimpleDragon.Grasshopper.Components.EnergyPlus242ExecutionIdf",
            throwOnError: true)!;
        PropertyInfo schemaProperty = Assert.IsAssignableFrom<PropertyInfo>(positioningType.GetProperty(
            "Schema",
            BindingFlags.Static | BindingFlags.NonPublic));
        IddSchema schema = Assert.IsType<IddSchema>(schemaProperty.GetValue(null));

        Assert.Equal("24.2.0", schema.Version);
        Assert.Equal(6, schema["AirConditioner:VariableRefrigerantFlow"]
            ["Cooling Capacity Ratio Modifier Function of Low Temperature Curve Name"].Position);
        Assert.Equal(66, schema["AirConditioner:VariableRefrigerantFlow"]["Fuel Type"].Position);
        Assert.Equal(22, schema["Sizing:Zone"]
            ["Design Specification Zone Air Distribution Object Name"].Position);

        IdfGenerationContext context = new(schema, options);
        IdfObject terminal = context.Create(
            "ZoneHVAC:TerminalUnit:VariableRefrigerantFlow",
            IdfGenerationContext.Field(14, "Supply Air Fan Operating Mode Schedule Name", "ALLON"),
            IdfGenerationContext.Field(15, "Supply Air Fan Placement", "DrawThrough"),
            IdfGenerationContext.Field(17, "Supply Air Fan Object Name", "Test Fan"),
            IdfGenerationContext.Field(25, "Zone Terminal Unit Off Parasitic Electric Energy Use", 20));
        var document = new IdfDocument(schema, new[] { terminal });
        document.ApplyDefaults();

        Assert.Equal("ALLON", terminal[11]);
        Assert.Equal("DrawThrough", terminal[12]);
        Assert.Equal("Fan:ConstantVolume", terminal[13]);
        Assert.Equal("Test Fan", terminal[14]);
        Assert.Equal("20", terminal[22]);
    }

    [Fact]
    public void LegacyCompositionComponentsRetainTheirGuidAndPortSchemas()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = LegacyComponents
            .Select(contract => AssertComponent(assembly, contract, assertPresentation: false))
            .ToArray();

        Assert.Equal(components.Length, components.Select(component => component.ComponentGuid).Distinct().Count());
        Assert.All(components, component => Assert.Equal(GH_Exposure.hidden, component.Exposure));
        Assert.Empty(
            components.Select(component => component.ComponentGuid)
                .Intersect(CanonicalComponents.Select(component => component.Guid)));
    }

    [Fact]
    public void VentilationAssignmentComponentPreservesCountGreaterThanOne()
    {
        Assembly assembly = LoadPlugin();
        var ventilator = new VentilationSystem(
            "Contract ERV",
            0.35d,
            0.8d,
            0.65d,
            new EntityId("ERV-COMPOSITION-CONTRACT"));
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = new SimpleDragonEnergyRecoveryVentilatorGoo(ventilator),
            [1] = 3,
        });

        InvokeSolve(Component(assembly, "CreateSimpleDragonVentilationAssignmentComponent"), access);

        Assert.Empty(access.OutputList(1));
        IGH_Goo goo = Assert.IsAssignableFrom<IGH_Goo>(access.Outputs[0]);
        VentilationAssignment assignment = Assert.IsType<VentilationAssignment>(goo.ScriptVariable());
        Assert.Equal(3, assignment.Count);
        Assert.Equal(ventilator.Id.Value, assignment.VentilationSystemId);
        Assert.NotNull(assignment.VentilationSystem);
        Assert.Equal(ventilator.Id, assignment.VentilationSystem!.Id);
    }

    private static GH_Component AssertComponent(
        Assembly assembly,
        ComponentContract contract,
        bool assertPresentation)
    {
        GH_Component component = Component(assembly, contract.TypeName);
        Assert.Equal(contract.Guid, component.ComponentGuid);
        if (assertPresentation)
        {
            Assert.Equal(contract.Name, component.Name);
            Assert.Equal(contract.NickName, component.NickName);
            Assert.Equal(contract.SubCategory, component.SubCategory);
        }

        Assert.Equal(contract.Inputs.Length, component.Params.Input.Count);
        for (int index = 0; index < contract.Inputs.Length; index++)
        {
            ParameterContract expected = contract.Inputs[index];
            IGH_Param actual = component.Params.Input[index];
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.TypeName, actual.GetType().Name);
            Assert.Equal(expected.Access, actual.Access);
            Assert.Equal(expected.Optional, actual.Optional);
        }

        Assert.Equal(contract.Outputs.Length, component.Params.Output.Count);
        for (int index = 0; index < contract.Outputs.Length; index++)
        {
            ParameterContract expected = contract.Outputs[index];
            IGH_Param actual = component.Params.Output[index];
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.TypeName, actual.GetType().Name);
            Assert.Equal(expected.Access, actual.Access);
        }

        return component;
    }

    private static ParameterContract Input(
        string name,
        string typeName,
        GH_ParamAccess access,
        bool optional = false)
    {
        return new ParameterContract(name, typeName, access, optional);
    }

    private static ParameterContract Output(string name, string typeName, GH_ParamAccess access)
    {
        return new ParameterContract(name, typeName, access, Optional: false);
    }

    private static GH_Component Component(Assembly assembly, string typeName)
    {
        Type type = Assert.Single(
            assembly.GetTypes(),
            candidate => !candidate.IsAbstract
                && typeof(GH_Component).IsAssignableFrom(candidate)
                && string.Equals(candidate.Name, typeName, StringComparison.Ordinal));
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static void InvokeSolve(GH_Component component, IGH_DataAccess access)
    {
        Type? current = component.GetType();
        MethodInfo? solve = null;
        while (current is not null && solve is null)
        {
            solve = current.GetMethod(
                "SolveInstance",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            current = current.BaseType;
        }

        Assert.NotNull(solve);
        solve.Invoke(component, new object[] { access });
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

    private sealed record ComponentContract(
        string TypeName,
        Guid Guid,
        string? Name,
        string? NickName,
        string? SubCategory,
        ParameterContract[] Inputs,
        ParameterContract[] Outputs);

    private sealed record ParameterContract(
        string Name,
        string TypeName,
        GH_ParamAccess Access,
        bool Optional);

    private sealed class TestDataAccess : IGH_DataAccess
    {
        private readonly IReadOnlyDictionary<int, object?> _inputs;

        internal TestDataAccess(IReadOnlyDictionary<int, object?> inputs)
        {
            _inputs = inputs;
        }

        internal Dictionary<int, object?> Outputs { get; } = new();

        public int Iteration { get; private set; }

        internal IReadOnlyList<object?> OutputList(int index)
        {
            return Outputs.TryGetValue(index, out object? value) && value is IReadOnlyList<object?> list
                ? list
                : Array.Empty<object?>();
        }

        public void IncrementIteration() => Iteration++;

        public void DisableGapLogic() { }

        public void DisableGapLogic(int parameterIndex) { }

        public GH_Path ParameterTargetPath(int parameterIndex) => new(0);

        public int ParameterTargetIndex(int parameterIndex) => parameterIndex;

        public void AbortComponentSolution() { }

        public List<T> Util_RemoveNullRefs<T>(List<T> list) => list.Where(item => item is not null).ToList();

        public int Util_CountNullRefs<T>(List<T> list) => list.Count(item => item is null);

        public int Util_CountNonNullRefs<T>(List<T> list) => list.Count(item => item is not null);

        public bool Util_EnsureNonNullCount<T>(List<T> list, int count) => Util_CountNonNullRefs(list) >= count;

        public int Util_FirstNonNullItem<T>(List<T> list) => list.FindIndex(item => item is not null);

        public bool SetData(int index, object value)
        {
            Outputs[index] = value;
            return true;
        }

        public bool SetData(int index, object value, int subIndex) => SetData(index, value);

        public bool SetData(string name, object value) => false;

        public bool SetDataList(int index, IEnumerable values)
        {
            Outputs[index] = values.Cast<object?>().ToArray();
            return true;
        }

        public bool SetDataList(int index, IEnumerable values, int subIndex) => SetDataList(index, values);

        public bool SetDataList(string name, IEnumerable values) => false;

        public bool SetDataTree(int index, IGH_DataTree tree) => false;

        public bool SetDataTree(int index, IGH_Structure tree) => false;

        public bool BlitData<Q>(int sourceIndex, GH_Structure<Q> target, bool dataMapping)
            where Q : IGH_Goo => false;

        public bool GetData<T>(int index, ref T value)
        {
            if (!_inputs.TryGetValue(index, out object? candidate) || candidate is not T typed)
            {
                return false;
            }

            value = typed;
            return true;
        }

        public bool GetData<T>(string name, ref T value) => false;

        public bool GetDataList<T>(int index, List<T> values)
        {
            if (!_inputs.TryGetValue(index, out object? candidate)
                || candidate is string
                || candidate is not IEnumerable enumerable)
            {
                return false;
            }

            foreach (object? item in enumerable)
            {
                if (item is not T typed)
                {
                    return false;
                }

                values.Add(typed);
            }

            return true;
        }

        public bool GetDataList<T>(string name, List<T> values) => false;

        public bool GetDataTree<T>(int index, out GH_Structure<T> tree)
            where T : IGH_Goo
        {
            tree = new GH_Structure<T>();
            return false;
        }

        public bool GetDataTree<T>(string name, out GH_Structure<T> tree)
            where T : IGH_Goo
        {
            tree = new GH_Structure<T>();
            return false;
        }
    }
}
