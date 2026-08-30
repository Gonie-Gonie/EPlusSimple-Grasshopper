using System.Collections;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;

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

    private static readonly string[] GeometryModelAndProfileComponentNames =
    {
        "WindowFromPolylineComponent",
        "DoorFromPolylineComponent",
        "FloorComponent",
        "CeilingComponent",
        "WallComponent",
        "ZoneComponent",
        "ConstantProfileComponent",
    };

    private static readonly string[] ModelOutputNames =
    {
        "Model",
        "Valid",
        "Diagnostics",
    };

    private static readonly string[] ZoneInputNames =
    {
        "Name", "Surfaces", "Profile", "Infiltration", "Lighting Power Density",
        "Outdoor Air Flow", "HVAC", "ERVs",
    };

    private static readonly string[] ZoneInputTypes =
    {
        "Param_String", "DragonSurfaceParam", "DragonProfileParam", "Param_Number",
        "Param_Number", "Param_Number", "DragonSupplySystemParam",
        "DragonEnergyRecoveryVentilatorParam",
    };

    private static readonly string[] EnergyModelInputNames =
    {
        "Name", "Zones", "North Axis", "Terrain", "PV Panels",
    };

    private static readonly string[] EnergyModelInputTypes =
    {
        "Param_String", "DragonZoneDefinitionParam", "Param_Number", "ChoiceStringParam",
        "DragonPhotovoltaicPanelParam",
    };

    [Fact]
    public void HvacAuthoringCatalogIsCompleteTypedAndGuidStable()
    {
        Assembly assembly = LoadPlugin();
        GH_Component[] components = ExpectedComponents.Keys
            .Select(name => Component(assembly, name))
            .ToArray();

        Assert.Equal(17, components.Length);
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

    }

    [Fact]
    public void PublicAuthoringComponentsDoNotExposeEntityIdentifierInputs()
    {
        Assembly assembly = LoadPlugin();
        string[] componentNames = ExpectedComponents.Keys
            .Concat(GeometryModelAndProfileComponentNames)
            .ToArray();

        Assert.Equal(24, componentNames.Length);
        Assert.All(componentNames, name => Assert.DoesNotContain(
            Component(assembly, name).Params.Input,
            parameter => string.Equals(parameter.Name, "ID", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void EngineeringPortsExposeUnitsDefaultsAndSelectableTextChoices()
    {
        Assembly assembly = LoadPlugin();
        GH_Component heatPump = Component(assembly, "HeatPumpComponent");
        Assert.Equal("ChoiceStringParam", heatPump.Params.Input[1].GetType().Name);
        Assert.Equal(nameof(Fuel.Electricity), PersistentDefault(heatPump.Params.Input[1]));
        Assert.Equal("Heat Pump", PersistentDefault(heatPump.Params.Input[0]));
        Assert.Equal(3.5d, PersistentDefault(heatPump.Params.Input[2]));
        Assert.Contains("COP", heatPump.Params.Input[2].Name, StringComparison.Ordinal);
        Assert.Contains("W", heatPump.Params.Input[4].Description, StringComparison.Ordinal);
        Assert.Contains("0 means autosize", heatPump.Params.Input[4].Description, StringComparison.OrdinalIgnoreCase);

        GH_Component tower = Component(assembly, "CoolingTowerComponent");
        Assert.Equal("ChoiceStringParam", tower.Params.Input[1].GetType().Name);
        Assert.Equal("ChoiceStringParam", tower.Params.Input[2].GetType().Name);
        Assert.Equal("Open", PersistentDefault(tower.Params.Input[1]));
        Assert.Equal("Single", PersistentDefault(tower.Params.Input[2]));
        Assert.Contains("Open", tower.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("Closed", tower.Params.Input[1].Description, StringComparison.Ordinal);
        Assert.Contains("Single", tower.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("Two", tower.Params.Input[2].Description, StringComparison.Ordinal);

        GH_Component chiller = Component(assembly, "ChillerComponent");
        Assert.Equal("ChoiceStringParam", chiller.Params.Input[2].GetType().Name);
        Assert.Equal(nameof(CompressorType.Turbo), PersistentDefault(chiller.Params.Input[2]));
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
        Assert.Equal("ChoiceStringParam", domesticHotWater.Params.Input[1].GetType().Name);
        Assert.Equal("Domestic Hot Water", PersistentDefault(domesticHotWater.Params.Input[0]));
        Assert.Equal(nameof(Fuel.NaturalGas), PersistentDefault(domesticHotWater.Params.Input[1]));
        Assert.Equal(0.85d, PersistentDefault(domesticHotWater.Params.Input[2]));
        Assert.Contains("greater than 0", domesticHotWater.Params.Input[2].Description, StringComparison.Ordinal);
        Assert.Contains("no greater than 1", domesticHotWater.Params.Input[2].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ZoneOwnsSystemsAndEnergyModelHasOnlyDirectObjectPorts()
    {
        Assembly assembly = LoadPlugin();
        GH_Component zone = Component(assembly, "ZoneComponent");
        GH_Component model = Component(assembly, "EnergyModelComponent");

        Assert.Equal(ZoneInputNames, zone.Params.Input.Select(item => item.Name));
        Assert.Equal(ZoneInputTypes, zone.Params.Input.Select(item => item.GetType().Name));
        Assert.Equal("DragonZoneDefinitionParam", zone.Params.Output[0].GetType().Name);
        Assert.True(zone.Params.Input[6].Optional);
        Assert.True(zone.Params.Input[7].Optional);

        Assert.Equal(EnergyModelInputNames, model.Params.Input.Select(item => item.Name));
        Assert.Equal(EnergyModelInputTypes, model.Params.Input.Select(item => item.GetType().Name));
        Assert.True(model.Params.Input[4].Optional);
        Assert.Equal(ModelOutputNames, model.Params.Output.Select(item => item.Name));
        Assert.DoesNotContain(
            zone.Params.Input.Concat(model.Params.Input),
            item => item.Name.Contains("Index", StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains("Assignment", StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains("Adjacent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ZoneComponentStoresDirectlyConnectedHvacAndErvsInItsDefinition()
    {
        Assembly assembly = LoadPlugin();
        EnergyModel fixture = HvacDragonGooTests.FullHvacModel();
        GonieGonie.InvisibleDragon.Shape.Zone source = fixture.Zones[0];
        SupplySystem[] systems = fixture.HvacAssignments[0].Supply.Systems.Take(2).ToArray();
        EnergyRecoveryVentilator ventilator = fixture.VentilationAssignments[0].Ventilator;
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Direct Zone",
            [1] = source.Surfaces.Select(item => new DragonSurfaceGoo(item)).ToArray(),
            [2] = new DragonProfileGoo(source.Profile),
            [3] = source.InfiltrationAirChangesPerHour,
            [4] = source.LightingPowerDensityWattsPerSquareMetre,
            [5] = source.OutdoorAirFlowCubicMetresPerSecond,
            [6] = systems.Select(item => new DragonSupplySystemGoo(item)).ToArray(),
            [7] = new[] { new DragonEnergyRecoveryVentilatorGoo(ventilator) },
        });

        InvokeSolve(Component(assembly, "ZoneComponent"), access);

        InvisibleDragonZoneDefinition definition = Assert.IsType<DragonZoneDefinitionGoo>(access.Outputs[0]).Value;
        Assert.False(string.IsNullOrWhiteSpace(definition.Zone.Id.Value));
        Assert.Equal(2, definition.SupplySystems.Count);
        Assert.Single(definition.Ventilators);
        Assert.Equal(systems[0].Id, definition.SupplySystems[0].Id);
        Assert.Equal(ventilator.Id, definition.Ventilators[0].Id);
    }

    [Fact]
    public void EnergyModelAutomaticallyPairsExactlyTwoCoincidentZoneSurfaces()
    {
        Assembly assembly = LoadPlugin();
        GonieGonie.InvisibleDragon.Shape.Zone fixture = HvacDragonGooTests.FullHvacModel().Zones[0];
        Surface firstSurface = CoincidentSurface("surface-a", "Shared A", reversed: false);
        Surface secondSurface = CoincidentSurface("surface-b", "Shared B", reversed: true);
        var first = new GonieGonie.InvisibleDragon.Shape.Zone(
            new EntityId("zone-a"), "Zone A", new[] { firstSurface }, fixture.Profile);
        var second = new GonieGonie.InvisibleDragon.Shape.Zone(
            new EntityId("zone-b"), "Zone B", new[] { secondSurface }, fixture.Profile);
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Auto Adjacency",
            [1] = new[]
            {
                new DragonZoneDefinitionGoo(new InvisibleDragonZoneDefinition(first)),
                new DragonZoneDefinitionGoo(new InvisibleDragonZoneDefinition(second)),
            },
            [2] = 0d,
            [3] = "Suburbs",
        });

        InvokeSolve(Component(assembly, "EnergyModelComponent"), access);

        EnergyModel model = Assert.IsType<DragonEnergyModelGoo>(access.Outputs[0]).Value;
        Surface resolvedFirst = Assert.Single(model.Zones[0].Surfaces);
        Surface resolvedSecond = Assert.Single(model.Zones[1].Surfaces);
        Assert.Equal(SurfaceBoundaryCondition.Zone, resolvedFirst.Boundary.Condition);
        Assert.Equal(SurfaceBoundaryCondition.Zone, resolvedSecond.Boundary.Condition);
        Assert.Equal(resolvedSecond.Id, resolvedFirst.Boundary.AdjacentSurfaceId);
        Assert.Equal(resolvedFirst.Id, resolvedSecond.Boundary.AdjacentSurfaceId);
        Assert.DoesNotContain(
            access.OutputList(2),
            item => item is DiagnosticGoo diagnostic
                && diagnostic.Value.Code == "INVISIBLEDRAGON.GH.ADJACENCY_AMBIGUOUS");
    }

    [Fact]
    public void EnergyModelReportsAmbiguousThreeWayCoincidenceWithoutChoosingIndices()
    {
        Assembly assembly = LoadPlugin();
        GonieGonie.InvisibleDragon.Shape.Zone fixture = HvacDragonGooTests.FullHvacModel().Zones[0];
        DragonZoneDefinitionGoo[] definitions = Enumerable.Range(0, 3)
            .Select(index => new GonieGonie.InvisibleDragon.Shape.Zone(
                new EntityId("zone-ambiguous-" + index),
                "Ambiguous " + index,
                new[] { CoincidentSurface("surface-ambiguous-" + index, "Shared " + index, index % 2 == 1) },
                fixture.Profile))
            .Select(zone => new DragonZoneDefinitionGoo(new InvisibleDragonZoneDefinition(zone)))
            .ToArray();
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Ambiguous Adjacency",
            [1] = definitions,
            [2] = 0d,
            [3] = "Suburbs",
        });

        InvokeSolve(Component(assembly, "EnergyModelComponent"), access);

        Assert.False(Assert.IsType<bool>(access.Outputs[1]));
        Assert.Contains(
            access.OutputList(2),
            item => item is DiagnosticGoo diagnostic
                && diagnostic.Value.Code == "INVISIBLEDRAGON.GH.ADJACENCY_AMBIGUOUS"
                && diagnostic.Value.Message.Contains("multiple coincident candidates", StringComparison.Ordinal));
    }

    [Fact]
    public void GeometryCatalogUsesDirectOpenings()
    {
        Assembly assembly = LoadPlugin();
        GH_Component glazing = Component(assembly, "GlazingComponent");
        GH_Component window = Component(assembly, "WindowFromPolylineComponent");
        GH_Component door = Component(assembly, "DoorFromPolylineComponent");
        GH_Component floor = Component(assembly, "FloorComponent");
        GH_Component ceiling = Component(assembly, "CeilingComponent");
        GH_Component wall = Component(assembly, "WallComponent");
        GH_Component[] surfaces = { floor, ceiling, wall };

        Assert.Equal(new Guid("ecfd5cdd-3e4c-4261-8ddd-ecea8eaf5599"), glazing.ComponentGuid);
        Assert.Equal(new Guid("54bb0065-1b10-420c-a90e-0ce75e746781"), window.ComponentGuid);
        Assert.Equal(new Guid("b2e1e805-a126-44fe-bf6c-4dbf16a76aae"), door.ComponentGuid);
        Assert.Equal(new Guid("1938b273-3a60-459b-beb2-92e7c4905053"), floor.ComponentGuid);
        Assert.Equal(new Guid("d1930bb6-4398-46b9-a661-451370f09103"), ceiling.ComponentGuid);
        Assert.Equal(new Guid("20a8a2f5-845e-4a46-aa03-fb8849f592e2"), wall.ComponentGuid);
        Assert.Equal("Name|U-Value|SHGC", string.Join("|", glazing.Params.Input.Select(item => item.Name)));
        Assert.Equal("Curve|Name|Glazing", string.Join("|", window.Params.Input.Select(item => item.Name)));
        Assert.Equal("Curve|Name|Construction", string.Join("|", door.Params.Input.Select(item => item.Name)));
        Assert.All(surfaces, surface => Assert.Equal(
            "Curve|Name|Construction|Boundary Condition|Openings",
            string.Join("|", surface.Params.Input.Select(item => item.Name))));
        Assert.Equal("DragonGlazingParam", glazing.Params.Output[0].GetType().Name);
        Assert.Equal("DragonOpeningParam", window.Params.Output[0].GetType().Name);
        Assert.Equal("DragonOpeningParam", door.Params.Output[0].GetType().Name);
        Assert.All(surfaces, surface =>
        {
            Assert.Equal("ChoiceStringParam", surface.Params.Input[3].GetType().Name);
            Assert.Equal("DragonOpeningParam", surface.Params.Input[4].GetType().Name);
            Assert.DoesNotContain(
                surface.Params.Input,
                item => item.Name.Contains("Adjacent", StringComparison.OrdinalIgnoreCase)
                    || item.Name.Contains("Index", StringComparison.OrdinalIgnoreCase));
        });
        Assert.Equal(nameof(SurfaceBoundaryCondition.Ground), PersistentDefault(floor.Params.Input[3]));
        Assert.Equal(nameof(SurfaceBoundaryCondition.Outdoors), PersistentDefault(ceiling.Params.Input[3]));
        Assert.Equal(nameof(SurfaceBoundaryCondition.Outdoors), PersistentDefault(wall.Params.Input[3]));
        Assert.Null(assembly.GetType(ComponentsNamespace + "SurfaceComponent", throwOnError: false));
        Assert.DoesNotContain(
            assembly.GetTypes().Where(type => !type.IsAbstract && typeof(GH_Component).IsAssignableFrom(type)),
            type => Assert.IsAssignableFrom<GH_Component>(Activator.CreateInstance(type)).ComponentGuid
                == new Guid("c25eb6d8-9500-44e5-9909-58d41de0a320"));
        var host = new Surface(
            new EntityId("host"),
            "Host",
            SurfaceType.Wall,
            new NoMassConstruction("Wall", 0.4),
            SurfaceBoundary.Outdoors,
            Rectangle(0, 0, 4, 4),
            new IOpening[]
            {
                new Window(
                    new EntityId("outside-window"),
                    "Outside Window",
                    new Glazing("Glass", 1.8, 0.5),
                    Rectangle(3, 1, 5, 3)),
            });
        Assert.Contains(
            host.Validate().Diagnostics,
            diagnostic => diagnostic.Code == "INVISIBLEDRAGON.SURFACE.OPENING_OUTSIDE_HOST");
    }

    [Fact]
    public void SourceComponentsCreateEveryDomainFamilyAndAllTowerCombinations()
    {
        Assembly assembly = LoadPlugin();
        DragonSourceSystemGoo heatPumpGoo = Source(assembly, "HeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "Authored HP", [1] = nameof(Fuel.Electricity), [2] = 3.7d, [3] = 4.8d,
            [4] = 20_000d, [5] = 21_000d,
        });
        var heatPump = Assert.IsType<HeatPump>(heatPumpGoo.Value);
        Assert.False(string.IsNullOrWhiteSpace(heatPump.Id.Value));
        Assert.Equal(3.7d, heatPump.HeatingCoefficientOfPerformance);
        Assert.Equal(21_000d, heatPump.CoolingCapacityWatts);

        var geothermal = Assert.IsType<GeothermalHeatPump>(Source(
            assembly,
            "GeothermalHeatPumpComponent",
            new Dictionary<int, object?>
            {
                [0] = "Authored Geo", [1] = nameof(Fuel.Electricity), [2] = 4.2d, [3] = 5.3d,
                [4] = 22_000d, [5] = 23_000d,
            }).Value);
        Assert.False(string.IsNullOrWhiteSpace(geothermal.Id.Value));

        CoolingTower[] towers =
        {
            Tower(assembly, "Open", "Single", "tower-open-single"),
            Tower(assembly, "Open", "Two", "tower-open-two"),
            Tower(assembly, "Closed", "Single", "tower-closed-single"),
            Tower(assembly, "Closed", "Two", "tower-closed-two"),
        };
        Assert.IsType<OpenSingleSpeedCoolingTower>(towers[0]);
        Assert.IsType<OpenTwoSpeedCoolingTower>(towers[1]);
        Assert.IsType<ClosedSingleSpeedCoolingTower>(towers[2]);
        Assert.IsType<ClosedTwoSpeedCoolingTower>(towers[3]);

        DragonSourceSystemGoo boilerGoo = Source(assembly, "BoilerComponent", new Dictionary<int, object?>
        {
            [0] = "Authored Boiler", [1] = nameof(Fuel.NaturalGas), [2] = 0.92d,
            [3] = 31_000d, [4] = 0.88d, [5] = 62d,
        });
        var boiler = Assert.IsType<Boiler>(boilerGoo.Value);
        Assert.Equal(0.92d, boiler.NominalThermalEfficiency);
        var district = Assert.IsType<DistrictHeating>(Source(
            assembly,
            "DistrictHeatingComponent",
            new Dictionary<int, object?>
            {
                [0] = "Authored District", [1] = 32_000d, [2] = 0.87d,
                [3] = 58d,
            }).Value);
        Assert.Equal(58d, district.SetpointTemperatureCelsius);

        var chiller = Assert.IsType<Chiller>(Source(assembly, "ChillerComponent", new Dictionary<int, object?>
        {
            [0] = "Authored Chiller", [1] = 5.4d, [2] = nameof(CompressorType.Screw),
            [3] = towers[3], [4] = 41_000d, [5] = 0.86d, [6] = 5.8d,
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
            }).Value);
        Assert.Same(boiler, absorption.HeatSource);
        Assert.Same(towers[1], absorption.CoolingTower);

        DragonSourceSystemGoo repeat = Source(assembly, "HeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "Deterministic", [1] = nameof(Fuel.Electricity), [2] = 3.5d, [3] = 4d,
            [4] = 0d, [5] = 0d,
        });
        DragonSourceSystemGoo repeatAgain = Source(assembly, "HeatPumpComponent", new Dictionary<int, object?>
        {
            [0] = "Deterministic", [1] = nameof(Fuel.Electricity), [2] = 3.5d, [3] = 4d,
            [4] = 0d, [5] = 0d,
        });
        Assert.Equal(repeat.Value.Id, repeatAgain.Value.Id);
    }

    [Fact]
    public void SupplyVentilationAndPvComponentsCreateRepresentativeValues()
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
                [0] = "Packaged", [1] = heatPumpGoo,
            }),
            Supply(assembly, "AirHandlingUnitComponent", new Dictionary<int, object?>
            {
                [0] = "AHU", [1] = heatPumpGoo, [2] = 0.74d, [3] = 135d,
                [4] = 0.93d,
            }),
            Supply(assembly, "FanCoilUnitComponent", new Dictionary<int, object?>
            {
                [0] = "Cooling FCU", [1] = chillerGoo, [2] = 0.75d, [3] = 136d,
                [4] = 0.92d,
            }),
            Supply(assembly, "RadiatorComponent", new Dictionary<int, object?>
            {
                [0] = "Radiator", [1] = boilerGoo, [2] = 8_000d, [3] = 0.3d,
            }),
            Supply(assembly, "ElectricRadiatorComponent", new Dictionary<int, object?>
            {
                [0] = "Electric Radiator", [1] = 8_100d, [2] = 0.98d,
                [3] = 0.31d,
            }),
            Supply(assembly, "RadiantFloorComponent", new Dictionary<int, object?>
            {
                [0] = "Radiant Floor", [1] = districtGoo, [2] = 2.8d,
            }),
            Supply(assembly, "ElectricRadiantFloorComponent", new Dictionary<int, object?>
            {
                [0] = "Electric Floor", [1] = 2.9d,
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
            [4] = 0.72d, [5] = 120d,
        });
        InvokeSolve(Component(assembly, "EnergyRecoveryVentilatorComponent"), ventilatorAccess);
        var ventilatorGoo = Assert.IsType<DragonEnergyRecoveryVentilatorGoo>(ventilatorAccess.Outputs[0]);
        Assert.Equal(0.4d, ventilatorGoo.Value.SupplyAirFlowCubicMetresPerSecond);

        var photovoltaicAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored PV", [1] = 18d, [2] = 25d, [3] = 185d,
            [4] = 0.21d, [5] = 0.75d,
        });
        InvokeSolve(Component(assembly, "PhotovoltaicPanelComponent"), photovoltaicAccess);
        var photovoltaicGoo = Assert.IsType<DragonPhotovoltaicPanelGoo>(photovoltaicAccess.Outputs[0]);
        Assert.Equal(18d, photovoltaicGoo.Value.AreaSquareMetres);

        var domesticHotWaterAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored Hot Water", [1] = nameof(Fuel.Propane), [2] = 0.91d,
        });
        InvokeSolve(Component(assembly, "DomesticHotWaterComponent"), domesticHotWaterAccess);
        var domesticHotWaterGoo = Assert.IsType<DragonDomesticHotWaterGoo>(domesticHotWaterAccess.Outputs[0]);
        Assert.False(string.IsNullOrWhiteSpace(domesticHotWaterGoo.Value.Id.Value));
        Assert.Equal("Authored Hot Water", domesticHotWaterGoo.Value.Name);
        Assert.Equal(Fuel.Propane, domesticHotWaterGoo.Value.Fuel);
        Assert.Equal(0.91d, domesticHotWaterGoo.Value.Efficiency);

    }

    [Fact]
    public void DomesticHotWaterComponentUsesDeterministicIdsAndReportsInvalidEngineeringInputs()
    {
        Assembly assembly = LoadPlugin();
        IReadOnlyDictionary<int, object?> inputs = new Dictionary<int, object?>
        {
            [0] = "Deterministic Hot Water", [1] = nameof(Fuel.Electricity),
            [2] = 0.95d,
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
            [0] = "Invalid Fuel", [1] = "Uranium", [2] = 0.9d,
        });
        GH_Component invalidFuel = Component(assembly, "DomesticHotWaterComponent");
        InvokeSolve(invalidFuel, invalidFuelAccess);
        Assert.False(invalidFuelAccess.Outputs.ContainsKey(0));
        Assert.Contains(
            invalidFuel.RuntimeMessages(GH_RuntimeMessageLevel.Error),
            message => message.Contains("Fuel must be", StringComparison.Ordinal));

        var invalidEfficiencyAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid Efficiency", [1] = nameof(Fuel.NaturalGas),
            [2] = 0d,
        });
        GH_Component invalidEfficiency = Component(assembly, "DomesticHotWaterComponent");
        InvokeSolve(invalidEfficiency, invalidEfficiencyAccess);
        Assert.False(invalidEfficiencyAccess.Outputs.ContainsKey(0));
        Assert.Contains(
            invalidEfficiency.RuntimeMessages(GH_RuntimeMessageLevel.Error),
            message => message.Contains("greater than zero", StringComparison.Ordinal));
    }

    [Fact]
    public void CompleteEnergyModelAssemblyUsesZoneOwnedSystemsWithoutMappingPorts()
    {
        Assembly assembly = LoadPlugin();
        EnergyModel fixture = HvacDragonGooTests.FullHvacModel();
        GonieGonie.InvisibleDragon.Shape.Zone zone = fixture.Zones[0];
        SupplySystem[] systems = fixture.HvacAssignments[0].Supply.Systems
            .Where(item => item is AirHandlingUnit or FanCoilUnit or Radiator)
            .Take(3)
            .ToArray();
        EnergyRecoveryVentilator ventilator = fixture.VentilationAssignments[0].Ventilator;
        PhotovoltaicPanel panel = fixture.PhotovoltaicPanels[0];
        var definition = new InvisibleDragonZoneDefinition(zone, systems, new[] { ventilator });
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Authored Complete Model",
            [1] = new[] { new DragonZoneDefinitionGoo(definition) },
            [2] = 17d,
            [3] = "City",
            [4] = new[] { new DragonPhotovoltaicPanelGoo(panel) },
        });

        InvokeSolve(Component(assembly, "EnergyModelComponent"), access);

        var modelGoo = Assert.IsType<DragonEnergyModelGoo>(access.Outputs[0]);
        EnergyModel model = modelGoo.Value;
        Assert.Equal("Authored Complete Model", model.Name);
        Assert.Equal(17d, model.NorthAxisDegrees);
        Assert.Equal(Terrain.City, model.Terrain);
        ZoneHvacAssignment assignment = Assert.Single(model.HvacAssignments);
        Assert.Equal(3, assignment.Supply.Systems.Count);
        Assert.Equal(systems[0].Source?.Id, assignment.Supply.Systems[0].Source?.Id);
        Assert.Equal(assignment.Supply.Systems[1].Source?.Id, assignment.Supply.Systems[2].Source?.Id);
        Assert.Single(model.VentilationAssignments);
        Assert.Equal(ventilator.Id, model.VentilationAssignments[0].Ventilator.Id);
        Assert.Single(model.PhotovoltaicPanels);
        Assert.Equal(panel.Id, model.PhotovoltaicPanels[0].Id);
        Assert.True(Assert.IsType<bool>(access.Outputs[1]));

        var unconditionedAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Unconditioned Model",
            [1] = new[] { new DragonZoneDefinitionGoo(new InvisibleDragonZoneDefinition(zone)) },
            [2] = 0d,
            [3] = "Suburbs",
        });
        InvokeSolve(Component(assembly, "EnergyModelComponent"), unconditionedAccess);
        EnergyModel unconditioned = Assert.IsType<DragonEnergyModelGoo>(unconditionedAccess.Outputs[0]).Value;
        Assert.Empty(unconditioned.HvacAssignments);
        Assert.Empty(unconditioned.VentilationAssignments);
        Assert.Empty(unconditioned.PhotovoltaicPanels);
    }

    [Fact]
    public void InvalidEnumsAndSourceCombinationsReportActionableRuntimeErrors()
    {
        Assembly assembly = LoadPlugin();
        var invalidTowerAccess = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = "Invalid Tower", [1] = "Loop", [2] = "Single", [3] = 0d, [4] = 0.9d,
        });
        GH_Component invalidTower = Component(assembly, "CoolingTowerComponent");
        InvokeSolve(invalidTower, invalidTowerAccess);
        Assert.False(invalidTowerAccess.Outputs.ContainsKey(0));
        Assert.Contains(
            invalidTower.RuntimeMessages(GH_RuntimeMessageLevel.Error),
            message => message.Contains("Circuit must be Open, Closed", StringComparison.Ordinal));

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
            [2] = 0.7d, [3] = 100d, [4] = 0.9d,
        });
        GH_Component invalidAhu = Component(assembly, "AirHandlingUnitComponent");
        InvokeSolve(invalidAhu, invalidAhuAccess);
        Assert.False(invalidAhuAccess.Outputs.ContainsKey(0));
        string ahuError = Assert.Single(invalidAhu.RuntimeMessages(GH_RuntimeMessageLevel.Error));
        Assert.Contains("Heat Pump requires HeatPump", ahuError, StringComparison.Ordinal);
        Assert.Contains("Boiler", ahuError, StringComparison.Ordinal);

    }

    private static Surface CoincidentSurface(string id, string name, bool reversed)
    {
        List<Vertex> vertices = new()
        {
            new Vertex(0, 0, 0),
            new Vertex(4, 0, 0),
            new Vertex(4, 0, 3),
            new Vertex(0, 0, 3),
        };
        if (reversed)
        {
            vertices.Reverse();
        }

        return new Surface(
            new EntityId(id),
            name,
            SurfaceType.Wall,
            new NoMassConstruction("Shared Wall", 0.4),
            SurfaceBoundary.Outdoors,
            new PlanarPolygon(vertices));
    }

    private static PlanarPolygon Rectangle(double minimumX, double minimumY, double maximumX, double maximumY)
    {
        return new PlanarPolygon(new List<Vertex>
        {
            new Vertex(minimumX, minimumY, 0),
            new Vertex(maximumX, minimumY, 0),
            new Vertex(maximumX, maximumY, 0),
            new Vertex(minimumX, maximumY, 0),
        });
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

    private static CoolingTower Tower(Assembly assembly, string circuit, string speeds, string name)
    {
        var access = new TestDataAccess(new Dictionary<int, object?>
        {
            [0] = name, [1] = circuit, [2] = speeds, [3] = 30_000d, [4] = 0.9d,
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
