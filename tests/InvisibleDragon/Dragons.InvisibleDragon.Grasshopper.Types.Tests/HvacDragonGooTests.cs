using GH_IO.Serialization;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Grasshopper.Parameters;
using Dragons.InvisibleDragon.Grasshopper.Types;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Grasshopper.Kernel;
using DragonSurface = Dragons.InvisibleDragon.Shape.Surface;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Grasshopper.Tests;

public sealed class HvacDragonGooTests
{
    private static readonly string[] CustomSummaryReports =
    {
        "AnnualBuildingUtilityPerformanceSummary",
        "DemandEndUseComponentsSummary",
    };

    [Fact]
    public void EverySupportedSourceAndCoolingTowerRoundTripsLosslessly()
    {
        Boiler generator = BoilerSource();
        SourceSystem[] sources =
        {
            HeatPumpSource(),
            GeothermalSource(),
            generator,
            new DistrictHeating(new EntityId("source-district"), "District", 54_321, 0.87, 57.5),
            new Chiller(
                new EntityId("source-chiller-open-single"),
                "Open Single Chiller",
                5.15,
                CompressorType.Turbo,
                new OpenSingleSpeedCoolingTower(
                    new EntityId("tower-open-single"),
                    "Open Single",
                    91_001,
                    0.81),
                72_001,
                0.82,
                5.1),
            new Chiller(
                new EntityId("source-chiller-open-two"),
                "Open Two Chiller",
                4.65,
                CompressorType.Screw,
                new OpenTwoSpeedCoolingTower(
                    new EntityId("tower-open-two"),
                    "Open Two",
                    92_002,
                    0.82),
                72_002,
                0.83,
                5.2),
            new Chiller(
                new EntityId("source-chiller-closed-single"),
                "Closed Single Chiller",
                4.25,
                CompressorType.Reciprocating,
                new ClosedSingleSpeedCoolingTower(
                    new EntityId("tower-closed-single"),
                    "Closed Single",
                    93_003,
                    0.83),
                72_003,
                0.84,
                5.3),
            new Chiller(
                new EntityId("source-chiller-closed-two"),
                "Closed Two Chiller",
                4.05,
                CompressorType.Turbo,
                new ClosedTwoSpeedCoolingTower(
                    new EntityId("tower-closed-two"),
                    "Closed Two",
                    94_004,
                    0.84),
                72_004,
                0.85,
                5.4),
            new AbsorptionChiller(
                new EntityId("source-absorption"),
                "Absorption",
                1.17,
                generator,
                new OpenTwoSpeedCoolingTower(
                    new EntityId("tower-absorption"),
                    "Absorption Tower",
                    95_005,
                    0.85),
                73_005,
                0.86,
                5.5),
        };

        foreach (SourceSystem source in sources)
        {
            var goo = new DragonSourceSystemGoo(source);
            var duplicate = Assert.IsType<DragonSourceSystemGoo>(goo.Duplicate());
            DragonSourceSystemGoo archived = ArchiveRoundTrip(goo, new DragonSourceSystemGoo());

            Assert.NotSame(source, duplicate.Value);
            AssertSourceEquivalent(source, duplicate.Value);
            AssertSourceEquivalent(source, archived.Value);

            var castTarget = new DragonSourceSystemGoo();
            Assert.True(castTarget.CastFrom(source));
            SourceSystem? cast = null;
            Assert.True(castTarget.CastTo(ref cast));
            Assert.Same(source, cast);
        }
    }

    [Fact]
    public void EverySupportedSupplyRoundTripsLosslessly()
    {
        HeatPump heatPump = HeatPumpSource();
        GeothermalHeatPump geothermal = GeothermalSource();
        Boiler boiler = BoilerSource();
        DistrictHeating district = new(
            new EntityId("source-supply-district"),
            "Supply District",
            44_500,
            0.89,
            58.2);
        Chiller chiller = new(
            new EntityId("source-supply-chiller"),
            "Supply Chiller",
            4.8,
            CompressorType.Screw,
            new ClosedTwoSpeedCoolingTower(
                new EntityId("tower-supply-chiller"),
                "Supply Chiller Tower",
                67_800,
                0.88),
            58_900,
            0.86,
            6.4);

        SupplySystem[] supplies =
        {
            new AirHandlingUnit(new EntityId("supply-ahu"), "AHU", heatPump, 0.73, 121, 0.91),
            new VariableRefrigerantFlowTerminal(new EntityId("supply-vrf"), "VRF", geothermal),
            new PackagedAirConditioner(new EntityId("supply-packaged"), "Packaged", heatPump),
            new FanCoilUnit(new EntityId("supply-fan-coil-heating"), "Heating FCU", boiler, 0.74, 132, 0.92),
            new FanCoilUnit(new EntityId("supply-fan-coil-cooling"), "Cooling FCU", chiller, 0.75, 133, 0.93),
            new RadiantFloor(new EntityId("supply-radiant-floor"), "Radiant Floor", district, 2.7),
            new ElectricRadiantFloor(new EntityId("supply-electric-floor"), "Electric Floor", 2.8),
            new Radiator(new EntityId("supply-radiator"), "Radiator", boiler, 7_400, 0.27),
            new ElectricRadiator(new EntityId("supply-electric-radiator"), "Electric Radiator", 7_500, 0.96, 0.28),
        };

        foreach (SupplySystem supply in supplies)
        {
            var goo = new DragonSupplySystemGoo(supply);
            var duplicate = Assert.IsType<DragonSupplySystemGoo>(goo.Duplicate());
            DragonSupplySystemGoo archived = ArchiveRoundTrip(goo, new DragonSupplySystemGoo());

            Assert.NotSame(supply, duplicate.Value);
            AssertSupplyEquivalent(supply, duplicate.Value);
            AssertSupplyEquivalent(supply, archived.Value);

            var castTarget = new DragonSupplySystemGoo();
            Assert.True(castTarget.CastFrom(supply));
            SupplySystem? cast = null;
            Assert.True(castTarget.CastTo(ref cast));
            Assert.Same(supply, cast);
        }
    }

    [Fact]
    public void VentilatorAndPhotovoltaicGoosDuplicateCastAndArchiveRoundTrip()
    {
        EnergyRecoveryVentilator ventilator = Ventilator();
        PhotovoltaicPanel panel = Panel();

        var ventilatorGoo = new DragonEnergyRecoveryVentilatorGoo(ventilator);
        var ventilatorDuplicate = Assert.IsType<DragonEnergyRecoveryVentilatorGoo>(ventilatorGoo.Duplicate());
        DragonEnergyRecoveryVentilatorGoo ventilatorArchived = ArchiveRoundTrip(
            ventilatorGoo,
            new DragonEnergyRecoveryVentilatorGoo());
        AssertVentilatorEquivalent(ventilator, ventilatorDuplicate.Value);
        AssertVentilatorEquivalent(ventilator, ventilatorArchived.Value);
        var emptyVentilator = new DragonEnergyRecoveryVentilatorGoo();
        Assert.True(emptyVentilator.CastFrom(ventilator));
        EnergyRecoveryVentilator? ventilatorCast = null;
        Assert.True(emptyVentilator.CastTo(ref ventilatorCast));
        Assert.Same(ventilator, ventilatorCast);

        var panelGoo = new DragonPhotovoltaicPanelGoo(panel);
        var panelDuplicate = Assert.IsType<DragonPhotovoltaicPanelGoo>(panelGoo.Duplicate());
        DragonPhotovoltaicPanelGoo panelArchived = ArchiveRoundTrip(
            panelGoo,
            new DragonPhotovoltaicPanelGoo());
        AssertPanelEquivalent(panel, panelDuplicate.Value);
        AssertPanelEquivalent(panel, panelArchived.Value);
        var emptyPanel = new DragonPhotovoltaicPanelGoo();
        Assert.True(emptyPanel.CastFrom(panel));
        PhotovoltaicPanel? panelCast = null;
        Assert.True(emptyPanel.CastTo(ref panelCast));
        Assert.Same(panel, panelCast);
    }

    [Fact]
    public void DomesticHotWaterGooDuplicatesCastsAndArchiveRoundTripsLosslessly()
    {
        var system = new DomesticHotWater(
            new EntityId("dhw-goo"),
            "Goo Hot Water",
            Fuel.NaturalGas,
            0.88);
        var goo = new DragonDomesticHotWaterGoo(system);

        var duplicate = Assert.IsType<DragonDomesticHotWaterGoo>(goo.Duplicate());
        DragonDomesticHotWaterGoo archived = ArchiveRoundTrip(
            goo,
            new DragonDomesticHotWaterGoo());
        string snapshot = DragonGooSnapshot.Serialize(system);
        DomesticHotWater restored = DragonGooSnapshot.Deserialize<DomesticHotWater>(snapshot);

        AssertDomesticHotWaterEquivalent(system, duplicate.Value);
        AssertDomesticHotWaterEquivalent(system, archived.Value);
        AssertDomesticHotWaterEquivalent(system, restored);
        Assert.Equal(snapshot, DragonGooSnapshot.Serialize(restored));
        Assert.Contains("\"kind\":\"domestic-hot-water\"", snapshot, StringComparison.Ordinal);

        var empty = new DragonDomesticHotWaterGoo();
        Assert.True(empty.CastFrom(system));
        DomesticHotWater? cast = null;
        Assert.True(empty.CastTo(ref cast));
        Assert.Same(system, cast);
    }

    [Fact]
    public void FullEnergyModelRoundTripPreservesHvacGraphsAndEngineeringProperties()
    {
        EnergyModel source = FullHvacModel();

        DragonEnergyModelGoo restoredGoo = ArchiveRoundTrip(
            new DragonEnergyModelGoo(source),
            new DragonEnergyModelGoo());
        EnergyModel restored = restoredGoo.Value;

        Assert.True(restoredGoo.IsValid, restoredGoo.IsValidWhyNot);
        Assert.NotSame(source, restored);
        Assert.Equal(source.Name, restored.Name);
        Assert.Equal(source.NorthAxisDegrees, restored.NorthAxisDegrees);
        Assert.Equal(source.Terrain, restored.Terrain);
        Assert.Equal(source.OutputTables.SummaryReports, restored.OutputTables.SummaryReports);
        Assert.Equal(
            source.OutputTables.IncludeElectricityBalanceMonthly,
            restored.OutputTables.IncludeElectricityBalanceMonthly);
        Assert.Equal(2, restored.Zones.Count);
        Assert.Single(restored.HvacAssignments);

        SupplyGroup supply = restored.HvacAssignments[0].Supply;
        Assert.Equal(9, supply.Systems.Count);
        Assert.Equal(9, supply.Availabilities.Count);
        Assert.Equal("Custom HVAC Availability", supply.Availabilities[0]!.Name);
        Assert.All(supply.Availabilities.Skip(1), item => Assert.Null(item));

        AirHandlingUnit airHandler = Assert.IsType<AirHandlingUnit>(supply.Systems[0]);
        var packaged = Assert.IsType<PackagedAirConditioner>(supply.Systems[1]);
        var vrf = Assert.IsType<VariableRefrigerantFlowTerminal>(supply.Systems[2]);
        Assert.Same(airHandler.Source, packaged.Source);
        Assert.Same(airHandler.Source, vrf.Source);
        var geothermal = Assert.IsType<GeothermalHeatPump>(airHandler.Source);
        Assert.Equal(3.91, geothermal.HeatingCoefficientOfPerformance);
        Assert.Equal(5.12, geothermal.CoolingCoefficientOfPerformance);
        Assert.Equal(0.76, airHandler.FanTotalEfficiency);
        Assert.Equal(141, airHandler.FanPressureRisePascals);
        Assert.Equal(0.94, airHandler.MotorEfficiency);

        var heatingFanCoil = Assert.IsType<FanCoilUnit>(supply.Systems[3]);
        var radiator = Assert.IsType<Radiator>(supply.Systems[4]);
        Assert.Same(heatingFanCoil.Source, radiator.Source);
        var boiler = Assert.IsType<Boiler>(radiator.Source);
        Assert.Equal(0.93, boiler.NominalThermalEfficiency);
        Assert.Equal(61.3, boiler.SetpointTemperatureCelsius);
        Assert.Equal(8_600, radiator.HeatingCapacityWatts);
        Assert.Equal(0.31, radiator.RadiantFraction);

        var coolingFanCoil = Assert.IsType<FanCoilUnit>(supply.Systems[5]);
        var absorption = Assert.IsType<AbsorptionChiller>(coolingFanCoil.Source);
        Assert.Same(boiler, absorption.HeatSource);
        Assert.Equal(1.21, absorption.ThermalCoefficientOfPerformance);
        Assert.Equal(79_200, absorption.NominalCapacityWatts);
        var tower = Assert.IsType<ClosedTwoSpeedCoolingTower>(absorption.CoolingTower);
        Assert.Equal(new EntityId("tower-model-absorption"), tower.Id);
        Assert.Equal(88_400, tower.NominalCapacityWatts);
        Assert.Equal(0.87, tower.PumpMotorEfficiency);

        var radiantFloor = Assert.IsType<RadiantFloor>(supply.Systems[6]);
        var district = Assert.IsType<DistrictHeating>(radiantFloor.Source);
        Assert.Equal(2.9, radiantFloor.ThrottlingRangeCelsius);
        Assert.Equal(59.4, district.SetpointTemperatureCelsius);
        Assert.IsType<ElectricRadiantFloor>(supply.Systems[7]);
        Assert.IsType<ElectricRadiator>(supply.Systems[8]);

        Assert.Equal(2, restored.VentilationAssignments.Count);
        Assert.Same(
            restored.VentilationAssignments[0].Ventilator,
            restored.VentilationAssignments[1].Ventilator);
        AssertVentilatorEquivalent(Ventilator(), restored.VentilationAssignments[0].Ventilator);
        Assert.Single(restored.PhotovoltaicPanels);
        AssertPanelEquivalent(Panel(), restored.PhotovoltaicPanels[0]);
    }

    [Fact]
    public void NewGooAndPersistentParameterTypesArePubliclyDiscoverable()
    {
        Type[] expectedGoos =
        {
            typeof(DragonSourceSystemGoo),
            typeof(DragonSupplySystemGoo),
            typeof(DragonDomesticHotWaterGoo),
            typeof(DragonEnergyRecoveryVentilatorGoo),
            typeof(DragonPhotovoltaicPanelGoo),
        };
        Type[] expectedParameters =
        {
            typeof(DragonSourceSystemParam),
            typeof(DragonSupplySystemParam),
            typeof(DragonDomesticHotWaterParam),
            typeof(DragonEnergyRecoveryVentilatorParam),
            typeof(DragonPhotovoltaicPanelParam),
        };
        Type[] exported = typeof(DragonSourceSystemGoo).Assembly.GetExportedTypes();

        Assert.All(expectedGoos, type =>
        {
            Assert.True(type.IsPublic);
            Assert.Contains(type, exported);
        });
        Assert.All(expectedParameters, type =>
        {
            Assert.True(type.IsPublic);
            Assert.Contains(type, exported);
            Assert.IsAssignableFrom<IGH_Param>(Activator.CreateInstance(type));
        });
    }

    internal static EnergyModel FullHvacModel()
    {
        Zone first = Zone("zone-model-first", "First Zone", 0);
        Zone second = Zone("zone-model-second", "Second Zone", 10);
        GeothermalHeatPump geothermal = new(
            new EntityId("source-model-geothermal"),
            "Model Geothermal",
            Fuel.Electricity,
            3.91,
            5.12,
            48_100,
            52_300);
        Boiler boiler = new(
            new EntityId("source-model-boiler"),
            "Model Boiler",
            Fuel.NaturalGas,
            0.93,
            82_500,
            0.91,
            61.3);
        DistrictHeating district = new(
            new EntityId("source-model-district"),
            "Model District",
            83_600,
            0.92,
            59.4);
        AbsorptionChiller absorption = new(
            new EntityId("source-model-absorption"),
            "Model Absorption",
            1.21,
            boiler,
            new ClosedTwoSpeedCoolingTower(
                new EntityId("tower-model-absorption"),
                "Model Closed Tower",
                88_400,
                0.87),
            79_200,
            0.88,
            5.7);
        SupplySystem[] systems =
        {
            new AirHandlingUnit(new EntityId("supply-model-ahu"), "Model AHU", geothermal, 0.76, 141, 0.94),
            new PackagedAirConditioner(new EntityId("supply-model-packaged"), "Model Packaged", geothermal),
            new VariableRefrigerantFlowTerminal(new EntityId("supply-model-vrf"), "Model VRF", geothermal),
            new FanCoilUnit(new EntityId("supply-model-heating-fcu"), "Model Heating FCU", boiler, 0.77, 142, 0.93),
            new Radiator(new EntityId("supply-model-radiator"), "Model Radiator", boiler, 8_600, 0.31),
            new FanCoilUnit(new EntityId("supply-model-cooling-fcu"), "Model Cooling FCU", absorption, 0.78, 143, 0.92),
            new RadiantFloor(new EntityId("supply-model-radiant-floor"), "Model Radiant Floor", district, 2.9),
            new ElectricRadiantFloor(new EntityId("supply-model-electric-floor"), "Model Electric Floor", 3.1),
            new ElectricRadiator(new EntityId("supply-model-electric-radiator"), "Model Electric Radiator", 8_700, 0.97, 0.32),
        };
        Schedule availability = Schedule.Constant(
            "Custom HVAC Availability",
            1,
            ScheduleType.OnOff);
        Schedule?[] availabilities = new Schedule?[systems.Length];
        availabilities[0] = availability;
        EnergyRecoveryVentilator ventilator = Ventilator();

        return new EnergyModel(
            "Full HVAC Model",
            new[] { first, second },
            new[]
            {
                new ZoneHvacAssignment(first.Id, new SupplyGroup(systems, availabilities)),
            },
            new[]
            {
                new ZoneVentilationAssignment(first.Id, ventilator),
                new ZoneVentilationAssignment(second.Id, ventilator),
            },
            new[] { Panel() },
            17.5,
            Terrain.City,
            new OutputTableSettings(CustomSummaryReports, false));
    }

    private static HeatPump HeatPumpSource() => new(
        new EntityId("source-heat-pump"),
        "Heat Pump",
        Fuel.Electricity,
        3.31,
        4.42,
        31_100,
        42_200);

    private static GeothermalHeatPump GeothermalSource() => new(
        new EntityId("source-geothermal"),
        "Geothermal",
        Fuel.Electricity,
        4.13,
        5.24,
        51_300,
        62_400);

    private static Boiler BoilerSource() => new(
        new EntityId("source-boiler"),
        "Generator Boiler",
        Fuel.NaturalGas,
        0.92,
        81_500,
        0.88,
        62.5);

    private static EnergyRecoveryVentilator Ventilator() => new(
        new EntityId("ventilator-model"),
        "Model ERV",
        0.82,
        0.71,
        0.43,
        0.74,
        137);

    private static PhotovoltaicPanel Panel() => new(
        new EntityId("pv-model"),
        "Model PV",
        42.5,
        31.2,
        181.4,
        0.213,
        0.83);

    private static Zone Zone(string id, string name, double xOffset)
    {
        var profile = new ZoneProfile(
            new EntityId($"profile-{id}"),
            $"Profile {name}",
            Schedule.Constant($"Heating {name}", 20.5, ScheduleType.Temperature),
            Schedule.Constant($"Cooling {name}", 25.5, ScheduleType.Temperature),
            Schedule.Constant($"HVAC Availability {name}", 1, ScheduleType.OnOff));
        var polygon = new PlanarPolygon(new[]
        {
            new Vertex(xOffset, 0, 0),
            new Vertex(xOffset + 5, 0, 0),
            new Vertex(xOffset + 5, 4, 0),
            new Vertex(xOffset, 4, 0),
        });
        var floor = new DragonSurface(
            new EntityId($"surface-{id}"),
            $"Floor {name}",
            SurfaceType.Floor,
            new NoMassConstruction($"Floor Construction {name}", 0.31),
            SurfaceBoundary.Ground,
            polygon);
        return new Zone(new EntityId(id), name, new[] { floor }, profile, 0.37, 8.4, 0.12);
    }

    private static void AssertSourceEquivalent(SourceSystem expected, SourceSystem actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        switch (expected)
        {
            case HeatPump expectedHeatPump:
                var actualHeatPump = Assert.IsAssignableFrom<HeatPump>(actual);
                Assert.Equal(expectedHeatPump.Fuel, actualHeatPump.Fuel);
                Assert.Equal(expectedHeatPump.HeatingCoefficientOfPerformance, actualHeatPump.HeatingCoefficientOfPerformance);
                Assert.Equal(expectedHeatPump.CoolingCoefficientOfPerformance, actualHeatPump.CoolingCoefficientOfPerformance);
                Assert.Equal(expectedHeatPump.HeatingCapacityWatts, actualHeatPump.HeatingCapacityWatts);
                Assert.Equal(expectedHeatPump.CoolingCapacityWatts, actualHeatPump.CoolingCapacityWatts);
                break;
            case Boiler expectedBoiler:
                var actualBoiler = Assert.IsType<Boiler>(actual);
                Assert.Equal(expectedBoiler.Fuel, actualBoiler.Fuel);
                Assert.Equal(expectedBoiler.NominalThermalEfficiency, actualBoiler.NominalThermalEfficiency);
                Assert.Equal(expectedBoiler.NominalCapacityWatts, actualBoiler.NominalCapacityWatts);
                Assert.Equal(expectedBoiler.PumpMotorEfficiency, actualBoiler.PumpMotorEfficiency);
                Assert.Equal(expectedBoiler.SetpointTemperatureCelsius, actualBoiler.SetpointTemperatureCelsius);
                break;
            case DistrictHeating expectedDistrict:
                var actualDistrict = Assert.IsType<DistrictHeating>(actual);
                Assert.Equal(expectedDistrict.NominalCapacityWatts, actualDistrict.NominalCapacityWatts);
                Assert.Equal(expectedDistrict.PumpMotorEfficiency, actualDistrict.PumpMotorEfficiency);
                Assert.Equal(expectedDistrict.SetpointTemperatureCelsius, actualDistrict.SetpointTemperatureCelsius);
                break;
            case Chiller expectedChiller:
                var actualChiller = Assert.IsType<Chiller>(actual);
                Assert.Equal(expectedChiller.ReferenceCoefficientOfPerformance, actualChiller.ReferenceCoefficientOfPerformance);
                Assert.Equal(expectedChiller.Compressor, actualChiller.Compressor);
                Assert.Equal(expectedChiller.NominalCapacityWatts, actualChiller.NominalCapacityWatts);
                Assert.Equal(expectedChiller.PumpMotorEfficiency, actualChiller.PumpMotorEfficiency);
                Assert.Equal(expectedChiller.SetpointTemperatureCelsius, actualChiller.SetpointTemperatureCelsius);
                AssertTowerEquivalent(expectedChiller.CoolingTower, actualChiller.CoolingTower);
                break;
            case AbsorptionChiller expectedAbsorption:
                var actualAbsorption = Assert.IsType<AbsorptionChiller>(actual);
                Assert.Equal(expectedAbsorption.ThermalCoefficientOfPerformance, actualAbsorption.ThermalCoefficientOfPerformance);
                Assert.Equal(expectedAbsorption.NominalCapacityWatts, actualAbsorption.NominalCapacityWatts);
                Assert.Equal(expectedAbsorption.PumpMotorEfficiency, actualAbsorption.PumpMotorEfficiency);
                Assert.Equal(expectedAbsorption.SetpointTemperatureCelsius, actualAbsorption.SetpointTemperatureCelsius);
                AssertSourceEquivalent(expectedAbsorption.HeatSource, actualAbsorption.HeatSource);
                AssertTowerEquivalent(expectedAbsorption.CoolingTower, actualAbsorption.CoolingTower);
                break;
            default:
                throw new Xunit.Sdk.XunitException($"Missing source assertion for {expected.GetType().FullName}.");
        }
    }

    private static void AssertTowerEquivalent(CoolingTower expected, CoolingTower actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.NominalCapacityWatts, actual.NominalCapacityWatts);
        Assert.Equal(expected.PumpMotorEfficiency, actual.PumpMotorEfficiency);
    }

    private static void AssertSupplyEquivalent(SupplySystem expected, SupplySystem actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.CanHeat, actual.CanHeat);
        Assert.Equal(expected.CanCool, actual.CanCool);
        if (expected.Source is null)
        {
            Assert.Null(actual.Source);
        }
        else
        {
            Assert.NotNull(actual.Source);
            AssertSourceEquivalent(expected.Source, actual.Source!);
        }

        switch (expected)
        {
            case AirHandlingUnit expectedAir:
                var actualAir = Assert.IsAssignableFrom<AirHandlingUnit>(actual);
                Assert.Equal(expectedAir.FanTotalEfficiency, actualAir.FanTotalEfficiency);
                Assert.Equal(expectedAir.FanPressureRisePascals, actualAir.FanPressureRisePascals);
                Assert.Equal(expectedAir.MotorEfficiency, actualAir.MotorEfficiency);
                break;
            case FanCoilUnit expectedFanCoil:
                var actualFanCoil = Assert.IsType<FanCoilUnit>(actual);
                Assert.Equal(expectedFanCoil.FanTotalEfficiency, actualFanCoil.FanTotalEfficiency);
                Assert.Equal(expectedFanCoil.FanPressureRisePascals, actualFanCoil.FanPressureRisePascals);
                Assert.Equal(expectedFanCoil.MotorEfficiency, actualFanCoil.MotorEfficiency);
                break;
            case RadiantFloor expectedFloor:
                Assert.Equal(expectedFloor.ThrottlingRangeCelsius, Assert.IsType<RadiantFloor>(actual).ThrottlingRangeCelsius);
                break;
            case ElectricRadiantFloor expectedFloor:
                Assert.Equal(expectedFloor.ThrottlingRangeCelsius, Assert.IsType<ElectricRadiantFloor>(actual).ThrottlingRangeCelsius);
                break;
            case Radiator expectedRadiator:
                var actualRadiator = Assert.IsType<Radiator>(actual);
                Assert.Equal(expectedRadiator.HeatingCapacityWatts, actualRadiator.HeatingCapacityWatts);
                Assert.Equal(expectedRadiator.RadiantFraction, actualRadiator.RadiantFraction);
                break;
            case ElectricRadiator expectedRadiator:
                var actualElectricRadiator = Assert.IsType<ElectricRadiator>(actual);
                Assert.Equal(expectedRadiator.HeatingCapacityWatts, actualElectricRadiator.HeatingCapacityWatts);
                Assert.Equal(expectedRadiator.Efficiency, actualElectricRadiator.Efficiency);
                Assert.Equal(expectedRadiator.RadiantFraction, actualElectricRadiator.RadiantFraction);
                break;
            default:
                throw new Xunit.Sdk.XunitException($"Missing supply assertion for {expected.GetType().FullName}.");
        }
    }

    private static void AssertVentilatorEquivalent(
        EnergyRecoveryVentilator expected,
        EnergyRecoveryVentilator actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.SensibleEffectiveness, actual.SensibleEffectiveness);
        Assert.Equal(expected.LatentEffectiveness, actual.LatentEffectiveness);
        Assert.Equal(expected.SupplyAirFlowCubicMetresPerSecond, actual.SupplyAirFlowCubicMetresPerSecond);
        Assert.Equal(expected.FanTotalEfficiency, actual.FanTotalEfficiency);
        Assert.Equal(expected.FanPressureRisePascals, actual.FanPressureRisePascals);
    }

    private static void AssertPanelEquivalent(PhotovoltaicPanel expected, PhotovoltaicPanel actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.AreaSquareMetres, actual.AreaSquareMetres);
        Assert.Equal(expected.TiltDegrees, actual.TiltDegrees);
        Assert.Equal(expected.AzimuthDegrees, actual.AzimuthDegrees);
        Assert.Equal(expected.Efficiency, actual.Efficiency);
        Assert.Equal(expected.ActiveCellAreaFraction, actual.ActiveCellAreaFraction);
    }

    private static void AssertDomesticHotWaterEquivalent(
        DomesticHotWater expected,
        DomesticHotWater actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Fuel, actual.Fuel);
        Assert.Equal(expected.Efficiency, actual.Efficiency);
    }

    private static TGoo ArchiveRoundTrip<TGoo>(TGoo source, TGoo target)
        where TGoo : GH_IO.GH_ISerializable
    {
        var writeArchive = new GH_Archive();
        Assert.True(writeArchive.AppendObject(source, "Value"));
        byte[] bytes = writeArchive.Serialize_Binary();
        var readArchive = new GH_Archive();
        Assert.True(readArchive.Deserialize_Binary(bytes));
        Assert.True(readArchive.ExtractObject(target, "Value"));
        return target;
    }
}
