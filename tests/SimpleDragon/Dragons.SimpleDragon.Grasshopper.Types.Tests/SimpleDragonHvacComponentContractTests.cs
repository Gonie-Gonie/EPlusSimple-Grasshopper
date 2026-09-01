using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

[SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline arrays keep each Grasshopper port contract local and readable.")]
public sealed class SimpleDragonHvacComponentContractTests
{
    private static readonly IReadOnlyDictionary<string, Guid> ExpectedComponents =
        new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["SimpleDragonHeatPumpComponent"] = new("e6e14d7b-55b4-45a9-97f9-9b99715f5ebc"),
            ["SimpleDragonGeothermalHeatPumpComponent"] = new("ebf437e1-425b-4cc5-a9db-c3e2276d2d8c"),
            ["SimpleDragonChillerComponent"] = new("d5cedc15-8b76-49e3-842b-5b0c498556fd"),
            ["SimpleDragonAbsorptionChillerComponent"] = new("c86733d7-2074-4688-8b49-e3da13de24b7"),
            ["SimpleDragonBoilerComponent"] = new("7b973e2c-7254-4730-9326-c320abedde5a"),
            ["SimpleDragonDistrictHeatingComponent"] = new("8216afdf-f5c1-4f3f-ae1f-0061813af720"),
            ["SimpleDragonPackagedAirConditionerComponent"] = new("8b4b8f93-cd03-4bd2-a7fa-da20bd802946"),
            ["SimpleDragonAirHandlingUnitComponent"] = new("8b0839fc-d03d-46af-8897-1ba4a41eab46"),
            ["SimpleDragonFanCoilUnitComponent"] = new("dd41df8f-9e3e-4663-8ce7-89025cfde30c"),
            ["SimpleDragonRadiatorComponent"] = new("2e77eee2-c354-40ba-abae-b501373046bc"),
            ["SimpleDragonElectricRadiatorComponent"] = new("3a3f5157-23bb-4094-83fd-e5cf4dc4d891"),
            ["SimpleDragonRadiantFloorComponent"] = new("c1315d1b-457b-444c-bda9-05aaa6a17749"),
            ["SimpleDragonElectricRadiantFloorComponent"] = new("e7d20017-8999-4cc1-bc12-f288f3f2ceb7"),
            ["SimpleDragonEnergyRecoveryVentilatorComponent"] = new("15afd6e6-1c05-4715-909b-b6e98ef91375"),
            ["SimpleDragonPhotovoltaicPanelComponent"] = new("7fcb5c47-3d49-4aa0-8fbc-bd765711401f"),
        };

    [Fact]
    public void HvacAuthoringCatalogIsCompleteTypedAndGuidStable()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = ExpectedComponents.Keys
            .Select(name => Component(assembly, name))
            .ToArray();

        Assert.Equal(15, components.Length);
        Assert.All(components, component =>
        {
            Assert.Equal("SimpleDragon", component.Category);
            Assert.Equal("Model", component.SubCategory);
            Assert.Equal(ExpectedComponents[component.GetType().Name], component.ComponentGuid);
            Assert.Equal(2, component.Params.Output.Count);
            Assert.Equal("Diagnostics", component.Params.Output[1].Name);
            Assert.Equal("SimpleDragonDiagnosticParam", component.Params.Output[1].GetType().Name);
        });
        Assert.Equal(components.Length, components.Select(item => item.ComponentGuid).Distinct().Count());

        string[] sourceNames = ExpectedComponents.Keys.Take(6).ToArray();
        string[] supplyNames = ExpectedComponents.Keys.Skip(6).Take(7).ToArray();
        Assert.All(sourceNames, name => Assert.Equal(
            "SimpleDragonSourceSystemParam",
            Component(assembly, name).Params.Output[0].GetType().Name));
        Assert.All(supplyNames, name => Assert.Equal(
            "SimpleDragonSupplySystemParam",
            Component(assembly, name).Params.Output[0].GetType().Name));
        Assert.Equal(
            "SimpleDragonZoneErvParam",
            Component(assembly, "SimpleDragonEnergyRecoveryVentilatorComponent").Params.Output[0].GetType().Name);
        Assert.Equal(
            "SimpleDragonPhotovoltaicPanelParam",
            Component(assembly, "SimpleDragonPhotovoltaicPanelComponent").Params.Output[0].GetType().Name);
    }

    [Fact]
    public void CapacityEnumsDefaultsAndOptionalInputsExposeExplicitEngineeringContracts()
    {
        Assembly assembly = LoadPlugin();
        (string Component, int Index)[] capacities =
        {
            ("SimpleDragonHeatPumpComponent", 4),
            ("SimpleDragonHeatPumpComponent", 5),
            ("SimpleDragonGeothermalHeatPumpComponent", 4),
            ("SimpleDragonGeothermalHeatPumpComponent", 5),
            ("SimpleDragonChillerComponent", 2),
            ("SimpleDragonChillerComponent", 6),
            ("SimpleDragonAbsorptionChillerComponent", 3),
            ("SimpleDragonBoilerComponent", 3),
            ("SimpleDragonDistrictHeatingComponent", 1),
            ("SimpleDragonPackagedAirConditionerComponent", 2),
            ("SimpleDragonRadiatorComponent", 2),
            ("SimpleDragonElectricRadiatorComponent", 1),
        };
        foreach ((string componentName, int index) in capacities)
        {
            IGH_Param parameter = Component(assembly, componentName).Params.Input[index];
            Assert.True(parameter.Optional);
            Assert.Contains("W", parameter.Description, StringComparison.Ordinal);
            Assert.Contains("autosize/unset", parameter.Description, StringComparison.OrdinalIgnoreCase);
        }

        GH_Component heatPump = Component(assembly, "SimpleDragonHeatPumpComponent");
        Assert.Equal("Heat Pump", PersistentDefault(heatPump.Params.Input[0]));
        Assert.Equal(nameof(FuelType.Electricity), PersistentDefault(heatPump.Params.Input[1]));
        Assert.Equal("ChoiceStringParam", heatPump.Params.Input[1].GetType().Name);
        Assert.Equal(3d, PersistentDefault(heatPump.Params.Input[2]));
        Assert.Equal(3d, PersistentDefault(heatPump.Params.Input[3]));
        GH_Component geothermal = Component(assembly, "SimpleDragonGeothermalHeatPumpComponent");
        Assert.Equal("Geothermal Heat Pump", PersistentDefault(geothermal.Params.Input[0]));

        GH_Component chiller = Component(assembly, "SimpleDragonChillerComponent");
        Assert.Equal("ChoiceStringParam", chiller.Params.Input[3].GetType().Name);
        Assert.Equal("ChoiceStringParam", chiller.Params.Input[4].GetType().Name);
        Assert.Equal("ChoiceStringParam", chiller.Params.Input[5].GetType().Name);
        Assert.Equal(nameof(CompressorType.Turbo), PersistentDefault(chiller.Params.Input[3]));
        Assert.Equal(nameof(CoolingTowerType.Open), PersistentDefault(chiller.Params.Input[4]));
        Assert.Equal(nameof(CoolingTowerControl.SingleSpeed), PersistentDefault(chiller.Params.Input[5]));
        Assert.Contains("Turbo", chiller.Params.Input[3].Description, StringComparison.Ordinal);
        Assert.Contains("Closed", chiller.Params.Input[4].Description, StringComparison.Ordinal);
        Assert.Contains("Single Speed", chiller.Params.Input[5].Description, StringComparison.Ordinal);

        GH_Component erv = Component(assembly, "SimpleDragonEnergyRecoveryVentilatorComponent");
        Assert.Contains("m³/s", erv.Params.Input[1].Description, StringComparison.Ordinal);
        GH_Component panel = Component(assembly, "SimpleDragonPhotovoltaicPanelComponent");
        Assert.Contains("m²", panel.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("[0, 360)", panel.Params.Input[3].Description, StringComparison.Ordinal);
        Assert.Contains("[0, 90]", panel.Params.Input[4].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySourceAndSupplyComponentSolvesToItsDeclaredDomainFamily()
    {
        Assembly assembly = LoadPlugin();
        SimpleDragonSourceSystemGoo heatPump = Source(assembly, "SimpleDragonHeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "HP", [1] = nameof(FuelType.Electricity), [2] = 3.2d, [3] = 4.3d,
            [4] = 10_000d, [5] = 11_000d,
        });
        SimpleDragonSourceSystemGoo geothermal = Source(assembly, "SimpleDragonGeothermalHeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "Geo", [1] = nameof(FuelType.Electricity), [2] = 4.2d, [3] = 5.3d,
            [4] = 12_000d, [5] = 13_000d,
        });
        SimpleDragonSourceSystemGoo chiller = Source(assembly, "SimpleDragonChillerComponent", new Dictionary<int, object?>
        {
            [0] = "Chiller", [1] = 5.1d, [2] = 14_000d, [3] = nameof(CompressorType.Screw),
            [4] = nameof(CoolingTowerType.Closed), [5] = nameof(CoolingTowerControl.TwoSpeed),
            [6] = 15_000d,
        });
        SimpleDragonSourceSystemGoo absorption = Source(assembly, "SimpleDragonAbsorptionChillerComponent", new Dictionary<int, object?>
        {
            [0] = "Absorption", [1] = nameof(FuelType.NaturalGas), [2] = 0.9d,
            [3] = 16_000d, [4] = 0.86d,
        });
        SimpleDragonSourceSystemGoo boiler = Source(assembly, "SimpleDragonBoilerComponent", new Dictionary<int, object?>
        {
            [0] = "Boiler", [1] = nameof(FuelType.NaturalGas), [2] = 0.91d,
            [3] = 17_000d, [4] = true,
        });
        SimpleDragonSourceSystemGoo district = Source(assembly, "SimpleDragonDistrictHeatingComponent", new Dictionary<int, object?>
        {
            [0] = "District", [1] = 18_000d, [2] = false,
        });
        Assert.Equal(
            Enum.GetValues<SourceSystemType>(),
            new[] { heatPump, geothermal, chiller, absorption, boiler, district }.Select(item => item.Value.Type));
        Assert.Equal(FuelType.Electricity, heatPump.Value.FuelType);
        Assert.Equal(3.2d, heatPump.Value.HeatingCop);
        Assert.Equal(4.3d, heatPump.Value.CoolingCop);
        Assert.Equal(10_000d, heatPump.Value.HeatingCapacity);
        Assert.Equal(11_000d, heatPump.Value.CoolingCapacity);
        Assert.Equal(CompressorType.Screw, chiller.Value.CompressorType);
        Assert.Equal(CoolingTowerType.Closed, chiller.Value.CoolingTowerType);
        Assert.Equal(CoolingTowerControl.TwoSpeed, chiller.Value.CoolingTowerControl);
        Assert.Equal(15_000d, chiller.Value.CoolingTowerCapacity);
        Assert.Equal(0.86d, absorption.Value.BoilerEfficiency);
        Assert.Equal(FuelType.NaturalGas, boiler.Value.FuelType);
        Assert.Equal(0.91d, boiler.Value.Efficiency);
        Assert.True(boiler.Value.HotWaterSupply);
        Assert.Null(district.Value.FuelType);
        Assert.False(district.Value.HotWaterSupply);

        SimpleDragonSupplySystemGoo packaged = Supply(assembly, "SimpleDragonPackagedAirConditionerComponent", new Dictionary<int, object?>
        {
            [0] = "Packaged", [1] = 4.4d, [2] = 19_000d,
        });
        SimpleDragonSupplySystemGoo ahu = Supply(assembly, "SimpleDragonAirHandlingUnitComponent", new Dictionary<int, object?>
        {
            [0] = "AHU", [1] = heatPump,
        });
        SimpleDragonSupplySystemGoo fanCoil = Supply(assembly, "SimpleDragonFanCoilUnitComponent", new Dictionary<int, object?>
        {
            [0] = "FCU", [1] = chiller,
        });
        SimpleDragonSupplySystemGoo radiator = Supply(assembly, "SimpleDragonRadiatorComponent", new Dictionary<int, object?>
        {
            [0] = "Radiator", [1] = boiler, [2] = 8_000d,
        });
        SimpleDragonSupplySystemGoo electricRadiator = Supply(assembly, "SimpleDragonElectricRadiatorComponent", new Dictionary<int, object?>
        {
            [0] = "Electric Radiator", [1] = 8_100d,
        });
        SimpleDragonSupplySystemGoo radiantFloor = Supply(assembly, "SimpleDragonRadiantFloorComponent", new Dictionary<int, object?>
        {
            [0] = "Radiant Floor", [1] = district,
        });
        SimpleDragonSupplySystemGoo electricFloor = Supply(assembly, "SimpleDragonElectricRadiantFloorComponent", new Dictionary<int, object?>
        {
            [0] = "Electric Floor",
        });
        Assert.Equal(
            Enum.GetValues<SupplySystemType>(),
            new[] { packaged, ahu, fanCoil, radiator, electricRadiator, radiantFloor, electricFloor }
                .Select(item => item.Value.Type));
        Assert.Same(heatPump.Value, ahu.Value.SourceSystem);
        Assert.Same(chiller.Value, fanCoil.Value.SourceSystem);
        Assert.Same(boiler.Value, radiator.Value.SourceSystem);
        Assert.Same(district.Value, radiantFloor.Value.SourceSystem);
        Assert.Equal(4.4d, packaged.Value.CoolingCop);
        Assert.Equal(19_000d, packaged.Value.CoolingCapacity);
        Assert.Equal(8_000d, radiator.Value.HeatingCapacity);
        Assert.Equal(8_100d, electricRadiator.Value.HeatingCapacity);
        Assert.Null(electricFloor.Value.SourceSystem);
    }

    [Fact]
    public void ComponentGeneratedIdsAreDeterministicAndTrackMeaningfulAuthoredContent()
    {
        Assembly assembly = LoadPlugin();
        var sourceInputs = new Dictionary<int, object?>
        {
            [0] = "Deterministic Heat Pump",
            [1] = nameof(FuelType.Electricity),
            [2] = 3.25d,
            [3] = 4.5d,
            [4] = 12_000d,
            [5] = 14_000d,
        };
        SourceSystem firstSource = Source(
            assembly,
            "SimpleDragonHeatPumpComponent",
            sourceInputs).Value;
        SourceSystem repeatedSource = Source(
            assembly,
            "SimpleDragonHeatPumpComponent",
            sourceInputs).Value;
        SourceSystem changedSource = Source(
            assembly,
            "SimpleDragonHeatPumpComponent",
            new Dictionary<int, object?>(sourceInputs) { [5] = 15_000d }).Value;

        Assert.Equal(firstSource.Id, repeatedSource.Id);
        Assert.NotEqual(firstSource.Id, changedSource.Id);

        var supplyInputs = new Dictionary<int, object?>
        {
            [0] = "Deterministic Packaged AC",
            [1] = 4.1d,
            [2] = 18_000d,
        };
        SupplySystem firstSupply = Supply(
            assembly,
            "SimpleDragonPackagedAirConditionerComponent",
            supplyInputs).Value;
        SupplySystem repeatedSupply = Supply(
            assembly,
            "SimpleDragonPackagedAirConditionerComponent",
            supplyInputs).Value;
        SupplySystem changedSupply = Supply(
            assembly,
            "SimpleDragonPackagedAirConditionerComponent",
            new Dictionary<int, object?>(supplyInputs) { [2] = 19_000d }).Value;

        Assert.Equal(firstSupply.Id, repeatedSupply.Id);
        Assert.NotEqual(firstSupply.Id, changedSupply.Id);
    }

    [Fact]
    public void InvalidSourceCombinationProducesActionableDiagnosticInsteadOfSupply()
    {
        Assembly assembly = LoadPlugin();
        var heatPump = new SourceSystem(
            "Heat Pump",
            SourceSystemType.HeatPump,
            FuelType.Electricity,
            id: new EntityId("SOURCE-WRONG"));
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid Fan Coil",
            [1] = new SimpleDragonSourceSystemGoo(heatPump),
        });

        InvokeSolve(Component(assembly, "SimpleDragonFanCoilUnitComponent"), access);

        Assert.False(access.Outputs.ContainsKey(0));
        SimpleDragonDiagnosticGoo diagnostic = Assert.IsType<SimpleDragonDiagnosticGoo>(
            Assert.Single(access.OutputList(1)));
        Assert.Equal("SD.GH.HVAC.SOURCE_INCOMPATIBLE", diagnostic.Value.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Value.Severity);
        Assert.Contains("Allowed source types", diagnostic.Value.Message, StringComparison.Ordinal);
        Assert.Contains("Connect one of the source types", diagnostic.Value.SuggestedAction, StringComparison.Ordinal);
    }

    [Fact]
    public void ErvComponentProducesOwnedZoneErvWithCountInOneStep()
    {
        Assembly assembly = LoadPlugin();
        GH_Component component = Component(assembly, "SimpleDragonEnergyRecoveryVentilatorComponent");
        Assert.Equal(
            new[] { "Name", "Airflow", "Heating Efficiency", "Cooling Efficiency", "Count" },
            component.Params.Input.Select(parameter => parameter.Name));
        Assert.True(component.Params.Input[4].Optional);
        Assert.Equal("Zone ERV", component.Params.Output[0].Name);
        Assert.Equal("SimpleDragonZoneErvParam", component.Params.Output[0].GetType().Name);

        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored ERV",
            [1] = 0.3d,
            [2] = 0.8d,
            [3] = 0.6d,
            [4] = 2,
        });
        InvokeSolve(component, access);

        SimpleDragonZoneErvGoo goo =
            Assert.IsType<SimpleDragonZoneErvGoo>(access.Outputs[0]);
        VentilationAssignment assignment = goo.Value;
        Assert.Equal(2, assignment.Count);
        Assert.False(string.IsNullOrWhiteSpace(assignment.VentilationSystemId));
        Assert.NotNull(assignment.VentilationSystem);
        Assert.Equal(assignment.VentilationSystem!.Id.Value, assignment.VentilationSystemId);
        Assert.Equal("Authored ERV", assignment.VentilationSystem!.Name);
    }

    private static SimpleDragonSourceSystemGoo Source(
        Assembly assembly,
        string componentName,
        IReadOnlyDictionary<int, object?> inputs)
    {
        var access = new TestDataAccess(inputs);
        InvokeSolve(Component(assembly, componentName), access);
        Assert.Empty(access.OutputList(1));
        return Assert.IsType<SimpleDragonSourceSystemGoo>(access.Outputs[0]);
    }

    private static SimpleDragonSupplySystemGoo Supply(
        Assembly assembly,
        string componentName,
        IReadOnlyDictionary<int, object?> inputs)
    {
        var access = new TestDataAccess(inputs);
        InvokeSolve(Component(assembly, componentName), access);
        Assert.Empty(access.OutputList(1));
        return Assert.IsType<SimpleDragonSupplySystemGoo>(access.Outputs[0]);
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

    private static object? PersistentDefault(IGH_Param parameter)
    {
        PropertyInfo? persistentData = parameter.GetType().GetProperty("PersistentData");
        object? structure = persistentData?.GetValue(parameter);
        MethodInfo? allData = structure?.GetType().GetMethod(
            "AllData",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(bool) },
            modifiers: null);
        IEnumerable? data = allData?.Invoke(structure, new object[] { true }) as IEnumerable;
        object? first = data?.Cast<object>().FirstOrDefault();
        return first is IGH_Goo goo ? goo.ScriptVariable() : first;
    }

    private static GH_Component Component(Assembly assembly, string typeName)
    {
        Type type = Assert.Single(
            assembly.GetTypes(),
            item => !item.IsAbstract
                && typeof(GH_Component).IsAssignableFrom(item)
                && item.Name == typeName);
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
    }

    private static Assembly LoadPlugin()
    {
        string repository = RepositoryRoot();
        string path = Path.Combine(
            repository,
            "temp",
            "build",
            "bin",
            "Dragons.SimpleDragon.GH",
            "Release",
            "net8.0-windows",
            "Dragons.SimpleDragon.GH.gha");
        Assert.True(File.Exists(path), "Expected built Grasshopper assembly at '" + path + "'.");
        return Assembly.LoadFrom(path);
    }

    private static string Fixture()
    {
        return Path.Combine(
            RepositoryRoot(),
            "fixtures",
            "simple-dragon",
            "grm",
            "ASHRAE 140 modified.grm");
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

    private sealed class TestDataAccess : IGH_DataAccess
    {
        private readonly IReadOnlyDictionary<int, object?> _inputs;

        public TestDataAccess(IReadOnlyDictionary<int, object?> inputs)
        {
            _inputs = inputs;
        }

        public Dictionary<int, object?> Outputs { get; } = new();

        public int Iteration { get; private set; }

        public IReadOnlyList<object?> OutputList(int index)
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
