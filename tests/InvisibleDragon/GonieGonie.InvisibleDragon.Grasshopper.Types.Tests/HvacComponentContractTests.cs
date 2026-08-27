using System.Collections;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class HvacComponentContractTests
{
    private const string ComponentsNamespace = "GonieGonie.InvisibleDragon.Grasshopper.Components.";

    private static readonly IReadOnlyDictionary<string, (Guid Guid, string Panel)> ExpectedComponents =
        new Dictionary<string, (Guid, string)>(StringComparer.Ordinal)
        {
            ["HeatPumpComponent"] = (new("e8751fda-24b9-4727-ad66-f81de722f64f"), "HVAC"),
            ["GeothermalHeatPumpComponent"] = (new("ccfa3a94-c7ea-4011-8b0f-b3364f4c023a"), "HVAC"),
            ["CoolingTowerComponent"] = (new("68084dee-fa5c-4669-b3c0-d64e9aca182b"), "HVAC"),
            ["ChillerComponent"] = (new("a4254427-84f7-4ba3-9c8a-2aea8862fde6"), "HVAC"),
            ["AbsorptionChillerComponent"] = (new("5719d04d-3093-4293-87d9-17f5bd9d9a7e"), "HVAC"),
            ["BoilerComponent"] = (new("e732f5f9-db94-405b-9221-f4449b4baad7"), "HVAC"),
            ["DistrictHeatingComponent"] = (new("e768769e-3a89-425d-9f99-3610e8e43bb9"), "HVAC"),
            ["PackagedAirConditionerComponent"] = (new("c78b3a6c-5517-4c56-ad1d-b0da8bfc37c3"), "HVAC"),
            ["AirHandlingUnitComponent"] = (new("a3a4afd8-17e1-4d9f-8da5-5883331c360f"), "HVAC"),
            ["FanCoilUnitComponent"] = (new("b24068e1-bd66-4d79-a1c6-aa6a79f50edc"), "HVAC"),
            ["RadiatorComponent"] = (new("1aed82ba-f96f-453b-b2b0-7d30498659cb"), "HVAC"),
            ["ElectricRadiatorComponent"] = (new("f18b4488-39e9-406c-b632-5e635c9972bb"), "HVAC"),
            ["RadiantFloorComponent"] = (new("e3bd88b6-54b6-43ec-9c94-ee0e36218618"), "HVAC"),
            ["ElectricRadiantFloorComponent"] = (new("b59c6585-0c85-4c68-bb43-1f37e4aade22"), "HVAC"),
            ["DomesticHotWaterComponent"] = (new("6f59e771-5dc0-44aa-9b7d-a84c3d0c7d74"), "HVAC"),
            ["EnergyRecoveryVentilatorComponent"] = (new("3d5f630e-66c3-43da-b73c-50d5be1792c3"), "HVAC"),
            ["PhotovoltaicPanelComponent"] = (new("237bc85d-769a-468b-a048-70e3b5c382ee"), "Systems"),
            ["SupplyGroupAssignmentComponent"] = (new("1c78fc6e-952f-4513-a39f-b107daba9677"), "HVAC"),
        };

    private static readonly string[] SourceComponentNames =
    {
        "HeatPumpComponent",
        "GeothermalHeatPumpComponent",
        "ChillerComponent",
        "AbsorptionChillerComponent",
        "BoilerComponent",
        "DistrictHeatingComponent",
    };

    private static readonly string[] SupplyComponentNames =
    {
        "PackagedAirConditionerComponent",
        "AirHandlingUnitComponent",
        "FanCoilUnitComponent",
        "RadiatorComponent",
        "ElectricRadiatorComponent",
        "RadiantFloorComponent",
        "ElectricRadiantFloorComponent",
    };

    private static readonly string[] LegacyModelInputNames =
    {
        "Name",
        "Zones",
        "North Axis",
        "Terrain",
    };

    private static readonly string[] AppendedModelInputNames =
    {
        "Sources",
        "Supply Systems",
        "Supply Zone Indices",
        "HVAC Assignments",
        "Ventilators",
        "Ventilator Zone Indices",
        "PV Panels",
    };

    private static readonly string[] AssignmentOutputParamTypeNames =
    {
        "Param_GenericObject",
        "Param_GenericObject",
        "DragonSourceSystemParam",
    };

    private static readonly string[] AppendedModelInputTypeNames =
    {
        "DragonSourceSystemParam",
        "DragonSupplySystemParam",
        "Param_Integer",
        "Param_GenericObject",
        "DragonEnergyRecoveryVentilatorParam",
        "Param_Integer",
        "DragonPhotovoltaicPanelParam",
    };

    private static readonly string[] ModelOutputNames =
    {
        "Model",
        "Valid",
        "Diagnostics",
    };

    private static readonly int[] ThreeZeroZoneIndices = { 0, 0, 0 };

    private static readonly int[] SingleZeroZoneIndex = { 0 };

    private static readonly int[] InvalidZoneIndex = { 2 };

    [Fact]
    public void HvacAuthoringCatalogIsCompleteTypedAndGuidStable()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = ExpectedComponents.Keys
            .Select(name => Component(assembly, name))
            .ToArray();

        Assert.Equal(18, components.Length);
        Assert.All(components, component =>
        {
            (Guid guid, string panel) = ExpectedComponents[component.GetType().Name];
            Assert.Equal("InvisibleDragon", component.Category);
            Assert.Equal(panel, component.SubCategory);
            Assert.Equal(guid, component.ComponentGuid);
        });
        Assert.Equal(components.Length, components.Select(item => item.ComponentGuid).Distinct().Count());
        Assert.All(SourceComponentNames, name => Assert.Equal(
            "DragonSourceSystemParam",
            Component(assembly, name).Params.Output[0].GetType().Name));
        Assert.All(SupplyComponentNames, name => Assert.Equal(
            "DragonSupplySystemParam",
            Component(assembly, name).Params.Output[0].GetType().Name));
        Assert.Equal("Param_GenericObject", Component(assembly, "CoolingTowerComponent").Params.Output[0].GetType().Name);
        Assert.Equal(
            "DragonEnergyRecoveryVentilatorParam",
            Component(assembly, "EnergyRecoveryVentilatorComponent").Params.Output[0].GetType().Name);
        Assert.Equal(
            "DragonDomesticHotWaterParam",
            Component(assembly, "DomesticHotWaterComponent").Params.Output[0].GetType().Name);
        Assert.Equal(
            "DragonPhotovoltaicPanelParam",
            Component(assembly, "PhotovoltaicPanelComponent").Params.Output[0].GetType().Name);

        GH_Component assignment = Component(assembly, "SupplyGroupAssignmentComponent");
        Assert.Equal("DragonZoneParam", assignment.Params.Input[0].GetType().Name);
        Assert.Equal("DragonSupplySystemParam", assignment.Params.Input[1].GetType().Name);
        Assert.Equal("DragonScheduleParam", assignment.Params.Input[2].GetType().Name);
        Assert.True(assignment.Params.Input[2].Optional);
        Assert.Equal(AssignmentOutputParamTypeNames, assignment.Params.Output.Select(item => item.GetType().Name));
    }

    [Fact]
    public void EngineeringPortsExposeUnitsDefaultsAndNamedEnumChoices()
    {
        Assembly assembly = LoadPlugin();
        GH_Component heatPump = Component(assembly, "HeatPumpComponent");
        Assert.Equal("Param_Integer", heatPump.Params.Input[1].GetType().Name);
        Assert.Equal("Heat Pump", PersistentDefault(heatPump.Params.Input[0]));
        Assert.Equal(3.5d, PersistentDefault(heatPump.Params.Input[2]));
        Assert.Contains("COP", heatPump.Params.Input[2].Name, StringComparison.Ordinal);
        Assert.Contains("W", heatPump.Params.Input[4].Description, StringComparison.Ordinal);
        Assert.Contains("0 means autosize", heatPump.Params.Input[4].Description, StringComparison.OrdinalIgnoreCase);

        GH_Component tower = Component(assembly, "CoolingTowerComponent");
        Assert.Equal("Param_Integer", tower.Params.Input[1].GetType().Name);
        Assert.Equal("Param_Integer", tower.Params.Input[2].GetType().Name);
        Assert.Contains("Open", tower.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("Closed", tower.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("Single", tower.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("Two", tower.Params.Input[2].Description, StringComparison.Ordinal);

        GH_Component chiller = Component(assembly, "ChillerComponent");
        Assert.Equal("Param_Integer", chiller.Params.Input[2].GetType().Name);
        Assert.Contains("Turbo", chiller.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("Screw", chiller.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("degrees C", chiller.Params.Input[6].Description, StringComparison.Ordinal);

        GH_Component ventilator = Component(assembly, "EnergyRecoveryVentilatorComponent");
        Assert.Contains("m", ventilator.Params.Input[3].Description, StringComparison.Ordinal);
        Assert.Contains("/s", ventilator.Params.Input[3].Description, StringComparison.Ordinal);
        Assert.Contains("Pa", ventilator.Params.Input[5].Description, StringComparison.Ordinal);
        GH_Component photovoltaic = Component(assembly, "PhotovoltaicPanelComponent");
        Assert.Contains("m", photovoltaic.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("degrees", photovoltaic.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("0 to 1", photovoltaic.Params.Input[4].Description, StringComparison.Ordinal);

        GH_Component domesticHotWater = Component(assembly, "DomesticHotWaterComponent");
        Assert.Equal("Param_Integer", domesticHotWater.Params.Input[1].GetType().Name);
        Assert.Equal("Domestic Hot Water", PersistentDefault(domesticHotWater.Params.Input[0]));
        Assert.Equal((int)Fuel.NaturalGas, PersistentDefault(domesticHotWater.Params.Input[1]));
        Assert.Equal(0.85d, PersistentDefault(domesticHotWater.Params.Input[2]));
        Assert.Contains("greater than 0", domesticHotWater.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("no greater than 1", domesticHotWater.Params.Input[2].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void EnergyModelPreservesLegacyPortsAndAppendsOptionalTypedHvacPorts()
    {
        GH_Component component = Component(LoadPlugin(), "EnergyModelComponent");

        Assert.Equal(LegacyModelInputNames, component.Params.Input.Take(4).Select(item => item.Name));
        Assert.Equal(AppendedModelInputNames, component.Params.Input.Skip(4).Select(item => item.Name));
        Assert.Equal(AppendedModelInputTypeNames, component.Params.Input.Skip(4).Select(item => item.GetType().Name));
        Assert.All(component.Params.Input.Skip(4), item => Assert.True(item.Optional));
        Assert.Equal(ModelOutputNames, component.Params.Output.Select(item => item.Name));
    }

    [Fact]
    public void SourceComponentsCreateEveryDomainFamilyAndAllTowerCombinations()
    {
        Assembly assembly = LoadPlugin();
        DragonSourceSystemGoo heatPumpGoo = Source(assembly, "HeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "Authored HP", [1] = (int)Fuel.Electricity, [2] = 3.7d, [3] = 4.8d,
            [4] = 20_000d, [5] = 21_000d, [6] = "source-authored-hp",
        });
        var heatPump = Assert.IsType<HeatPump>(heatPumpGoo.Value);
        Assert.Equal(new EntityId("source-authored-hp"), heatPump.Id);
        Assert.Equal(3.7d, heatPump.HeatingCoefficientOfPerformance);
        Assert.Equal(21_000d, heatPump.CoolingCapacityWatts);

        var geothermal = Assert.IsType<GeothermalHeatPump>(Source(
            assembly,
            "GeothermalHeatPumpComponent",
            new Dictionary<int, object?>
            {
                [0] = "Authored Geo", [1] = (int)Fuel.Electricity, [2] = 4.2d, [3] = 5.3d,
                [4] = 22_000d, [5] = 23_000d, [6] = "source-authored-geo",
            }).Value);
        Assert.Equal(new EntityId("source-authored-geo"), geothermal.Id);

        CoolingTower[] towers =
        {
            Tower(assembly, 0, 0, "tower-open-single"),
            Tower(assembly, 0, 1, "tower-open-two"),
            Tower(assembly, 1, 0, "tower-closed-single"),
            Tower(assembly, 1, 1, "tower-closed-two"),
        };
        Assert.IsType<OpenSingleSpeedCoolingTower>(towers[0]);
        Assert.IsType<OpenTwoSpeedCoolingTower>(towers[1]);
        Assert.IsType<ClosedSingleSpeedCoolingTower>(towers[2]);
        Assert.IsType<ClosedTwoSpeedCoolingTower>(towers[3]);

        DragonSourceSystemGoo boilerGoo = Source(assembly, "BoilerComponent", new Dictionary<int, object?>
        {
            [0] = "Authored Boiler", [1] = (int)Fuel.NaturalGas, [2] = 0.92d,
            [3] = 31_000d, [4] = 0.88d, [5] = 62d, [6] = "source-authored-boiler",
        });
        var boiler = Assert.IsType<Boiler>(boilerGoo.Value);
        Assert.Equal(0.92d, boiler.NominalThermalEfficiency);
        var district = Assert.IsType<DistrictHeating>(Source(
            assembly,
            "DistrictHeatingComponent",
            new Dictionary<int, object?>
            {
                [0] = "Authored District", [1] = 32_000d, [2] = 0.87d,
                [3] = 58d, [4] = "source-authored-district",
            }).Value);
        Assert.Equal(58d, district.SetpointTemperatureCelsius);

        var chiller = Assert.IsType<Chiller>(Source(assembly, "ChillerComponent", new Dictionary<int, object?>
        {
            [0] = "Authored Chiller", [1] = 5.4d, [2] = (int)CompressorType.Screw,
            [3] = towers[3], [4] = 41_000d, [5] = 0.86d, [6] = 5.8d,
            [7] = "source-authored-chiller",
        }).Value);
        Assert.Same(towers[3], chiller.CoolingTower);
        Assert.Equal(CompressorType.Screw, chiller.Compressor);

        var absorption = Assert.IsType<AbsorptionChiller>(Source(
            assembly,
            "AbsorptionChillerComponent",
            new Dictionary<int, object?>
            {
                [0] = "Authored Absorption", [1] = 1.15d, [2] = boilerGoo,
                [3] = towers[1], [4] = 42_000d, [5] = 0.85d, [6] = 5.9d,
                [7] = "source-authored-absorption",
            }).Value);
        Assert.Same(boiler, absorption.HeatSource);
        Assert.Same(towers[1], absorption.CoolingTower);

        DragonSourceSystemGoo repeat = Source(assembly, "HeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "Deterministic", [1] = (int)Fuel.Electricity, [2] = 3.5d, [3] = 4d,
            [4] = 0d, [5] = 0d, [6] = string.Empty,
        });
        DragonSourceSystemGoo repeatAgain = Source(assembly, "HeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "Deterministic", [1] = (int)Fuel.Electricity, [2] = 3.5d, [3] = 4d,
            [4] = 0d, [5] = 0d, [6] = string.Empty,
        });
        Assert.Equal(repeat.Value.Id, repeatAgain.Value.Id);
    }

    [Fact]
    public void SupplyVentilationPvAndAssignmentComponentsCreateRepresentativeValues()
    {
        Assembly assembly = LoadPlugin();
        var heatPump = new HeatPump(
            new EntityId("source-for-supplies"),
            "Supply HP",
            Fuel.Electricity,
            3.6,
            4.7,
            51_000,
            52_000);
        var boiler = new Boiler(
            new EntityId("boiler-for-supplies"),
            "Supply Boiler",
            Fuel.NaturalGas,
            0.91,
            53_000,
            0.89,
            60);
        var district = new DistrictHeating(
            new EntityId("district-for-supplies"),
            "Supply District",
            54_000,
            0.88,
            59);
        var chiller = new Chiller(
            new EntityId("chiller-for-supplies"),
            "Supply Chiller",
            5.1,
            CompressorType.Turbo,
            new OpenSingleSpeedCoolingTower(new EntityId("tower-for-supplies"), "Tower", 55_000, 0.87),
            56_000,
            0.86,
            6);
        var heatPumpGoo = new DragonSourceSystemGoo(heatPump);
        var boilerGoo = new DragonSourceSystemGoo(boiler);
        var districtGoo = new DragonSourceSystemGoo(district);
        var chillerGoo = new DragonSourceSystemGoo(chiller);

        SupplySystem[] supplies =
        {
            Supply(assembly, "PackagedAirConditionerComponent", new Dictionary<int, object?>
            {
                [0] = "Packaged", [1] = heatPumpGoo, [2] = "supply-packaged",
            }),
            Supply(assembly, "AirHandlingUnitComponent", new Dictionary<int, object?>
            {
                [0] = "AHU", [1] = heatPumpGoo, [2] = 0.74d, [3] = 135d,
                [4] = 0.93d, [5] = "supply-ahu",
            }),
            Supply(assembly, "FanCoilUnitComponent", new Dictionary<int, object?>
            {
                [0] = "Cooling FCU", [1] = chillerGoo, [2] = 0.75d, [3] = 136d,
                [4] = 0.92d, [5] = "supply-fcu",
            }),
            Supply(assembly, "RadiatorComponent", new Dictionary<int, object?>
            {
                [0] = "Radiator", [1] = boilerGoo, [2] = 8_000d, [3] = 0.3d,
                [4] = "supply-radiator",
            }),
            Supply(assembly, "ElectricRadiatorComponent", new Dictionary<int, object?>
            {
                [0] = "Electric Radiator", [1] = 8_100d, [2] = 0.98d,
                [3] = 0.31d, [4] = "supply-electric-radiator",
            }),
            Supply(assembly, "RadiantFloorComponent", new Dictionary<int, object?>
            {
                [0] = "Radiant Floor", [1] = districtGoo, [2] = 2.8d,
                [3] = "supply-radiant-floor",
            }),
            Supply(assembly, "ElectricRadiantFloorComponent", new Dictionary<int, object?>
            {
                [0] = "Electric Floor", [1] = 2.9d, [2] = "supply-electric-floor",
            }),
        };
        Assert.Collection(
            supplies,
            item => Assert.IsType<PackagedAirConditioner>(item),
            item => Assert.IsType<AirHandlingUnit>(item),
            item => Assert.IsType<FanCoilUnit>(item),
            item => Assert.IsType<Radiator>(item),
            item => Assert.IsType<ElectricRadiator>(item),
            item => Assert.IsType<RadiantFloor>(item),
            item => Assert.IsType<ElectricRadiantFloor>(item));
        Assert.Same(heatPump, supplies[0].Source);
        Assert.Same(heatPump, supplies[1].Source);
        Assert.Same(chiller, supplies[2].Source);
        Assert.Same(boiler, supplies[3].Source);
        Assert.Same(district, supplies[5].Source);

        var ventilatorAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored ERV", [1] = 0.8d, [2] = 0.67d, [3] = 0.4d,
            [4] = 0.72d, [5] = 120d, [6] = "erv-authored",
        });
        InvokeSolve(Component(assembly, "EnergyRecoveryVentilatorComponent"), ventilatorAccess);
        var ventilatorGoo = Assert.IsType<DragonEnergyRecoveryVentilatorGoo>(ventilatorAccess.Outputs[0]);
        Assert.Equal(0.4d, ventilatorGoo.Value.SupplyAirFlowCubicMetresPerSecond);

        var photovoltaicAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored PV", [1] = 18d, [2] = 25d, [3] = 185d,
            [4] = 0.21d, [5] = 0.75d, [6] = "pv-authored",
        });
        InvokeSolve(Component(assembly, "PhotovoltaicPanelComponent"), photovoltaicAccess);
        var photovoltaicGoo = Assert.IsType<DragonPhotovoltaicPanelGoo>(photovoltaicAccess.Outputs[0]);
        Assert.Equal(18d, photovoltaicGoo.Value.AreaSquareMetres);

        var domesticHotWaterAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored Hot Water", [1] = (int)Fuel.Propane, [2] = 0.91d,
            [3] = "dhw-authored",
        });
        InvokeSolve(Component(assembly, "DomesticHotWaterComponent"), domesticHotWaterAccess);
        var domesticHotWaterGoo = Assert.IsType<DragonDomesticHotWaterGoo>(domesticHotWaterAccess.Outputs[0]);
        Assert.Equal(new EntityId("dhw-authored"), domesticHotWaterGoo.Value.Id);
        Assert.Equal("Authored Hot Water", domesticHotWaterGoo.Value.Name);
        Assert.Equal(Fuel.Propane, domesticHotWaterGoo.Value.Fuel);
        Assert.Equal(0.91d, domesticHotWaterGoo.Value.Efficiency);

        GonieGonie.InvisibleDragon.Shape.Zone zone = HvacDragonGooTests.FullHvacModel().Zones[0];
        var assignmentAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = new DragonZoneGoo(zone),
            [1] = supplies.Take(4).Select(item => new DragonSupplySystemGoo(item)).ToArray(),
        });
        InvokeSolve(Component(assembly, "SupplyGroupAssignmentComponent"), assignmentAccess);
        var groupWrapper = Assert.IsType<GH_ObjectWrapper>(assignmentAccess.Outputs[0]);
        var group = Assert.IsType<SupplyGroup>(groupWrapper.Value);
        Assert.Equal(4, group.Systems.Count);
        var assignmentWrapper = Assert.IsType<GH_ObjectWrapper>(assignmentAccess.Outputs[1]);
        var assignment = Assert.IsType<ZoneHvacAssignment>(assignmentWrapper.Value);
        Assert.Equal(zone.Id, assignment.ZoneId);
        Assert.Equal(3, assignmentAccess.OutputList(2).Count);
    }

    [Fact]
    public void DomesticHotWaterComponentUsesDeterministicIdsAndReportsInvalidEngineeringInputs()
    {
        Assembly assembly = LoadPlugin();
        IReadOnlyDictionary<int, object?> inputs = new Dictionary<int, object?>
        {
            [0] = "Deterministic Hot Water", [1] = (int)Fuel.Electricity,
            [2] = 0.95d, [3] = string.Empty,
        };

        var firstAccess = new TestDataAccess(inputs);
        var secondAccess = new TestDataAccess(inputs);
        GH_Component firstComponent = Component(assembly, "DomesticHotWaterComponent");
        GH_Component secondComponent = Component(assembly, "DomesticHotWaterComponent");
        InvokeSolve(firstComponent, firstAccess);
        InvokeSolve(secondComponent, secondAccess);
        Assert.Empty(firstComponent.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        Assert.Empty(secondComponent.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        Assert.Equal(
            Assert.IsType<DragonDomesticHotWaterGoo>(firstAccess.Outputs[0]).Value.Id,
            Assert.IsType<DragonDomesticHotWaterGoo>(secondAccess.Outputs[0]).Value.Id);

        var invalidFuelAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid Fuel", [1] = int.MaxValue, [2] = 0.9d, [3] = string.Empty,
        });
        GH_Component invalidFuel = Component(assembly, "DomesticHotWaterComponent");
        InvokeSolve(invalidFuel, invalidFuelAccess);
        Assert.False(invalidFuelAccess.Outputs.ContainsKey(0));
        Assert.Contains(
            invalidFuel.RuntimeMessages(GH_RuntimeMessageLevel.Error),
            message => message.Contains("Fuel value", StringComparison.Ordinal));

        var invalidEfficiencyAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid Efficiency", [1] = (int)Fuel.NaturalGas,
            [2] = 0d, [3] = string.Empty,
        });
        GH_Component invalidEfficiency = Component(assembly, "DomesticHotWaterComponent");
        InvokeSolve(invalidEfficiency, invalidEfficiencyAccess);
        Assert.False(invalidEfficiencyAccess.Outputs.ContainsKey(0));
        Assert.Contains(
            invalidEfficiency.RuntimeMessages(GH_RuntimeMessageLevel.Error),
            message => message.Contains("greater than zero", StringComparison.Ordinal));
    }

    [Fact]
    public void CompleteEnergyModelAssemblyPreservesSharedSourcesAndAllExplicitAssignments()
    {
        Assembly assembly = LoadPlugin();
        EnergyModel fixture = HvacDragonGooTests.FullHvacModel();
        GonieGonie.InvisibleDragon.Shape.Zone zone = fixture.Zones[0];
        SupplySystem[] systems = fixture.HvacAssignments[0].Supply.Systems
            .Where(item => item is AirHandlingUnit or FanCoilUnit or Radiator)
            .Take(3)
            .ToArray();
        SourceSystem[] sources = systems
            .Select(item => item.Source)
            .Where(item => item is not null)
            .Cast<SourceSystem>()
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .ToArray();
        EnergyRecoveryVentilator ventilator = fixture.VentilationAssignments[0].Ventilator;
        PhotovoltaicPanel panel = fixture.PhotovoltaicPanels[0];
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored Complete Model",
            [1] = new[] { new DragonZoneGoo(zone) },
            [2] = 17d,
            [3] = "City",
            [4] = sources.Select(item => new DragonSourceSystemGoo(item)).ToArray(),
            [5] = systems.Select(item => new DragonSupplySystemGoo(item)).ToArray(),
            [6] = ThreeZeroZoneIndices,
            [7] = Array.Empty<object>(),
            [8] = new[] { new DragonEnergyRecoveryVentilatorGoo(ventilator) },
            [9] = SingleZeroZoneIndex,
            [10] = new[] { new DragonPhotovoltaicPanelGoo(panel) },
        });

        InvokeSolve(Component(assembly, "EnergyModelComponent"), access);

        var modelGoo = Assert.IsType<DragonEnergyModelGoo>(access.Outputs[0]);
        EnergyModel model = modelGoo.Value;
        Assert.Equal("Authored Complete Model", model.Name);
        Assert.Equal(17d, model.NorthAxisDegrees);
        Assert.Equal(Terrain.City, model.Terrain);
        ZoneHvacAssignment assignment = Assert.Single(model.HvacAssignments);
        Assert.Equal(3, assignment.Supply.Systems.Count);
        Assert.Same(systems[0].Source, assignment.Supply.Systems[0].Source);
        Assert.Same(assignment.Supply.Systems[1].Source, assignment.Supply.Systems[2].Source);
        Assert.Single(model.VentilationAssignments);
        Assert.Same(ventilator, model.VentilationAssignments[0].Ventilator);
        Assert.Single(model.PhotovoltaicPanels);
        Assert.Same(panel, model.PhotovoltaicPanels[0]);
        Assert.True(Assert.IsType<bool>(access.Outputs[1]));

        var legacyAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Legacy Model",
            [1] = new[] { new DragonZoneGoo(zone) },
            [2] = 0d,
            [3] = "Suburbs",
        });
        InvokeSolve(Component(assembly, "EnergyModelComponent"), legacyAccess);
        EnergyModel legacy = Assert.IsType<DragonEnergyModelGoo>(legacyAccess.Outputs[0]).Value;
        Assert.Empty(legacy.HvacAssignments);
        Assert.Empty(legacy.VentilationAssignments);
        Assert.Empty(legacy.PhotovoltaicPanels);
    }

    [Fact]
    public void InvalidEnumsSourceCombinationsAndMappingsReportActionableRuntimeErrors()
    {
        Assembly assembly = LoadPlugin();
        var invalidTowerAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid Tower", [1] = 9, [2] = 0, [3] = 0d, [4] = 0.9d, [5] = string.Empty,
        });
        GH_Component invalidTower = Component(assembly, "CoolingTowerComponent");
        InvokeSolve(invalidTower, invalidTowerAccess);
        Assert.False(invalidTowerAccess.Outputs.ContainsKey(0));
        Assert.Contains(
            invalidTower.RuntimeMessages(GH_RuntimeMessageLevel.Error),
            message => message.Contains("Circuit must be Open (0) or Closed (1)", StringComparison.Ordinal));

        var boiler = new Boiler(
            new EntityId("wrong-ahu-source"),
            "Wrong AHU Source",
            Fuel.NaturalGas,
            0.9,
            10_000,
            0.9,
            60);
        var invalidAhuAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid AHU", [1] = new DragonSourceSystemGoo(boiler),
            [2] = 0.7d, [3] = 100d, [4] = 0.9d, [5] = string.Empty,
        });
        GH_Component invalidAhu = Component(assembly, "AirHandlingUnitComponent");
        InvokeSolve(invalidAhu, invalidAhuAccess);
        Assert.False(invalidAhuAccess.Outputs.ContainsKey(0));
        string ahuError = Assert.Single(invalidAhu.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        Assert.Contains("Heat Pump requires HeatPump", ahuError, StringComparison.Ordinal);
        Assert.Contains("Boiler", ahuError, StringComparison.Ordinal);

        GonieGonie.InvisibleDragon.Shape.Zone zone = HvacDragonGooTests.FullHvacModel().Zones[0];
        var electric = new ElectricRadiator(new EntityId("bad-map-supply"), "Electric", null, 1, 0);
        var invalidMappingAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid Mapping", [1] = new[] { new DragonZoneGoo(zone) },
            [2] = 0d, [3] = "Suburbs", [4] = Array.Empty<DragonSourceSystemGoo>(),
            [5] = new[] { new DragonSupplySystemGoo(electric) }, [6] = InvalidZoneIndex,
        });
        GH_Component invalidMapping = Component(assembly, "EnergyModelComponent");
        InvokeSolve(invalidMapping, invalidMappingAccess);
        Assert.False(invalidMappingAccess.Outputs.ContainsKey(0));
        Assert.Contains(
            invalidMapping.RuntimeMessages(GH_RuntimeMessageLevel.Error),
            message => message.Contains("zero-based zone indices", StringComparison.Ordinal));
    }

    private static DragonSourceSystemGoo Source(
        Assembly assembly,
        string componentName,
        IReadOnlyDictionary<int, object?> inputs)
    {
        var access = new TestDataAccess(inputs);
        GH_Component component = Component(assembly, componentName);
        InvokeSolve(component, access);
        Assert.Empty(component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        return Assert.IsType<DragonSourceSystemGoo>(access.Outputs[0]);
    }

    private static SupplySystem Supply(
        Assembly assembly,
        string componentName,
        IReadOnlyDictionary<int, object?> inputs)
    {
        var access = new TestDataAccess(inputs);
        GH_Component component = Component(assembly, componentName);
        InvokeSolve(component, access);
        Assert.Empty(component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        return Assert.IsType<DragonSupplySystemGoo>(access.Outputs[0]).Value;
    }

    private static CoolingTower Tower(Assembly assembly, int circuit, int speeds, string id)
    {
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = id, [1] = circuit, [2] = speeds, [3] = 30_000d, [4] = 0.9d, [5] = id,
        });
        GH_Component component = Component(assembly, "CoolingTowerComponent");
        InvokeSolve(component, access);
        Assert.Empty(component.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        return Assert.IsAssignableFrom<CoolingTower>(Assert.IsType<GH_ObjectWrapper>(access.Outputs[0]).Value);
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
        Type type = assembly.GetType(ComponentsNamespace + typeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type));
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
        private readonly IReadOnlyDictionary<int, object?> inputs;

        public TestDataAccess(IReadOnlyDictionary<int, object?> inputs)
        {
            this.inputs = inputs;
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
            if (!inputs.TryGetValue(index, out object? candidate) || candidate is not T typed)
            {
                return false;
            }

            value = typed;
            return true;
        }

        public bool GetData<T>(string name, ref T value) => false;

        public bool GetDataList<T>(int index, List<T> values)
        {
            if (!inputs.TryGetValue(index, out object? candidate)
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
