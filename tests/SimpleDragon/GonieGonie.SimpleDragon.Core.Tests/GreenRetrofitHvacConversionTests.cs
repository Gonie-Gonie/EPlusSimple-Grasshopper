using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using DragonAbsorptionChiller = GonieGonie.InvisibleDragon.Hvac.AbsorptionChiller;
using DragonBoiler = GonieGonie.InvisibleDragon.Hvac.Boiler;
using DragonChiller = GonieGonie.InvisibleDragon.Hvac.Chiller;
using DragonClosedSingleSpeedCoolingTower = GonieGonie.InvisibleDragon.Hvac.ClosedSingleSpeedCoolingTower;
using DragonClosedTwoSpeedCoolingTower = GonieGonie.InvisibleDragon.Hvac.ClosedTwoSpeedCoolingTower;
using DragonCompressorType = GonieGonie.InvisibleDragon.Hvac.CompressorType;
using DragonDistrictHeating = GonieGonie.InvisibleDragon.Hvac.DistrictHeating;
using DragonElectricRadiator = GonieGonie.InvisibleDragon.Hvac.ElectricRadiator;
using DragonFanCoilUnit = GonieGonie.InvisibleDragon.Hvac.FanCoilUnit;
using DragonFuel = GonieGonie.InvisibleDragon.Hvac.Fuel;
using DragonGeothermalHeatPump = GonieGonie.InvisibleDragon.Hvac.GeothermalHeatPump;
using DragonHeatPump = GonieGonie.InvisibleDragon.Hvac.HeatPump;
using DragonOpenSingleSpeedCoolingTower = GonieGonie.InvisibleDragon.Hvac.OpenSingleSpeedCoolingTower;
using DragonOpenTwoSpeedCoolingTower = GonieGonie.InvisibleDragon.Hvac.OpenTwoSpeedCoolingTower;
using DragonPackagedAirConditioner = GonieGonie.InvisibleDragon.Hvac.PackagedAirConditioner;
using DragonRadiator = GonieGonie.InvisibleDragon.Hvac.Radiator;
using DragonSupplySystem = GonieGonie.InvisibleDragon.Hvac.SupplySystem;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class GreenRetrofitHvacConversionTests
{
    public static IEnumerable<object[]> ChillerMatrix()
    {
        CompressorType[] compressors = Enum.GetValues<CompressorType>();
        foreach (CompressorType compressor in compressors)
        {
            yield return new object[]
            {
                compressor,
                CoolingTowerType.Open,
                CoolingTowerControl.SingleSpeed,
                typeof(DragonOpenSingleSpeedCoolingTower),
            };
            yield return new object[]
            {
                compressor,
                CoolingTowerType.Open,
                CoolingTowerControl.TwoSpeed,
                typeof(DragonOpenTwoSpeedCoolingTower),
            };
            yield return new object[]
            {
                compressor,
                CoolingTowerType.Closed,
                CoolingTowerControl.SingleSpeed,
                typeof(DragonClosedSingleSpeedCoolingTower),
            };
            yield return new object[]
            {
                compressor,
                CoolingTowerType.Closed,
                CoolingTowerControl.TwoSpeed,
                typeof(DragonClosedTwoSpeedCoolingTower),
            };
        }
    }

    [Theory]
    [MemberData(nameof(ChillerMatrix))]
    public void ChillerPreservesEveryCompressorAndCoolingTowerCombination(
        CompressorType compressor,
        CoolingTowerType towerType,
        CoolingTowerControl towerControl,
        Type expectedTowerType)
    {
        var source = new SourceSystem(
            "matrix chiller",
            SourceSystemType.Chiller,
            coolingCop: 5.25d,
            coolingCapacity: 12_345d,
            compressorType: compressor,
            coolingTowerType: towerType,
            coolingTowerCapacity: 23_456d,
            coolingTowerControl: towerControl,
            id: new EntityId("SOURCE-CHILLER"));
        SupplySystem supply = FanCoil("SUPPLY-FANCOIL", source);

        GreenRetrofitConversionResult result = Convert(supply, source);

        AssertSuccessfulAndSupported(result);
        DragonFanCoilUnit convertedSupply = Assert.IsType<DragonFanCoilUnit>(OnlySupply(result));
        DragonChiller converted = Assert.IsType<DragonChiller>(convertedSupply.Source);
        Assert.Equal(source.Id, converted.Id);
        Assert.Equal(source.Id.Value, converted.Name);
        Assert.Equal(5.25d, converted.ReferenceCoefficientOfPerformance, 12);
        Assert.Equal(12_345d, converted.NominalCapacityWatts);
        Assert.Equal((DragonCompressorType)compressor, converted.Compressor);
        Assert.Equal(expectedTowerType, converted.CoolingTower.GetType());
        Assert.Equal(new EntityId("CoolingTower_for_SOURCE-CHILLER"), converted.CoolingTower.Id);
        Assert.Equal("CoolingTower_for_SOURCE-CHILLER", converted.CoolingTower.Name);
        Assert.Equal(23_456d, converted.CoolingTower.NominalCapacityWatts);
        Assert.Equal(supply.Id, convertedSupply.Id);
        Assert.Equal(Idf(result), Idf(Convert(supply, source)));
    }

    [Fact]
    public void ChillerKeepsAutosizedSourceAndTowerCapacities()
    {
        var source = new SourceSystem(
            "autosized chiller",
            SourceSystemType.Chiller,
            compressorType: CompressorType.Turbo,
            coolingTowerType: CoolingTowerType.Open,
            coolingTowerControl: CoolingTowerControl.SingleSpeed,
            id: new EntityId("SOURCE-AUTOSIZE"));

        GreenRetrofitConversionResult result = Convert(FanCoil("SUPPLY-AUTOSIZE", source), source);

        AssertSuccessfulAndSupported(result);
        DragonChiller converted = Assert.IsType<DragonChiller>(OnlySupply(result).Source);
        Assert.Equal(3d, converted.ReferenceCoefficientOfPerformance, 12);
        Assert.Null(converted.NominalCapacityWatts);
        Assert.Null(converted.CoolingTower.NominalCapacityWatts);
    }

    [Fact]
    public void SimpleDragonDefaultIdfUsesLegacyDualSetpointForCoolingOnlyFanCoil()
    {
        SourceSystem source = CreateSource(
            SourceSystemType.Chiller,
            "SOURCE-LEGACY-THERMOSTAT");
        GreenRetrofitConversionResult result = Convert(
            FanCoil("SUPPLY-LEGACY-THERMOSTAT", source),
            source);

        IdfDocument idf = result.ToIdfDocument();

        IdfObject control = Assert.Single(
            idf["Schedule:Constant"],
            item => item.Name!.StartsWith(
                "ScheduleTypeForThermostat_for_",
                StringComparison.Ordinal));
        Assert.Equal("4", control[2]);
        Assert.Single(idf["ThermostatSetpoint:DualSetpoint"]);
        Assert.Empty(idf["ThermostatSetpoint:SingleCooling"]);
    }

    [Fact]
    public void AbsorptionChillerPreservesGeneratorFuelEfficiencyCapacityAndPinnedTower()
    {
        var source = new SourceSystem(
            "absorption",
            SourceSystemType.AbsorptionChiller,
            FuelType.NaturalGas,
            coolingCop: 0.82d,
            coolingCapacity: 15_000d,
            coolingTowerType: CoolingTowerType.Closed,
            coolingTowerCapacity: 99_000d,
            coolingTowerControl: CoolingTowerControl.TwoSpeed,
            boilerEfficiency: 0.88d,
            id: new EntityId("SOURCE-ABSORPTION"));

        GreenRetrofitConversionResult result = Convert(FanCoil("SUPPLY-ABSORPTION", source), source);

        AssertSuccessfulAndSupported(result);
        DragonFanCoilUnit fanCoil = Assert.IsType<DragonFanCoilUnit>(OnlySupply(result));
        DragonAbsorptionChiller converted = Assert.IsType<DragonAbsorptionChiller>(fanCoil.Source);
        Assert.Equal(source.Id, converted.Id);
        Assert.Equal(0.82d, converted.ThermalCoefficientOfPerformance, 12);
        Assert.Equal(15_000d, converted.NominalCapacityWatts);
        Assert.Equal(new EntityId("Boiler_for_SOURCE-ABSORPTION"), converted.HeatSource.Id);
        Assert.Equal("Boiler_for_SOURCE-ABSORPTION", converted.HeatSource.Name);
        Assert.Equal(DragonFuel.NaturalGas, converted.HeatSource.Fuel);
        Assert.Equal(0.88d, converted.HeatSource.NominalThermalEfficiency, 12);
        Assert.Null(converted.HeatSource.NominalCapacityWatts);
        DragonOpenSingleSpeedCoolingTower tower =
            Assert.IsType<DragonOpenSingleSpeedCoolingTower>(converted.CoolingTower);
        Assert.Equal(new EntityId("CoolingTower_for_SOURCE-ABSORPTION"), tower.Id);
        Assert.Equal(15_000d, tower.NominalCapacityWatts);
    }

    [Fact]
    public void AbsorptionChillerAppliesPinnedDefaults()
    {
        var source = new SourceSystem(
            "default absorption",
            SourceSystemType.AbsorptionChiller,
            FuelType.Oil,
            id: new EntityId("SOURCE-ABSORPTION-DEFAULT"));

        GreenRetrofitConversionResult result = Convert(FanCoil("SUPPLY-ABSORPTION-DEFAULT", source), source);

        AssertSuccessfulAndSupported(result);
        DragonAbsorptionChiller converted = Assert.IsType<DragonAbsorptionChiller>(OnlySupply(result).Source);
        Assert.Equal(0.9d, converted.ThermalCoefficientOfPerformance, 12);
        Assert.Null(converted.NominalCapacityWatts);
        Assert.Equal(DragonFuel.Diesel, converted.HeatSource.Fuel);
        Assert.Equal(0.85d, converted.HeatSource.NominalThermalEfficiency, 12);
        Assert.Null(converted.CoolingTower.NominalCapacityWatts);
    }

    [Theory]
    [InlineData(SourceSystemType.Boiler, typeof(DragonBoiler))]
    [InlineData(SourceSystemType.DistrictHeating, typeof(DragonDistrictHeating))]
    [InlineData(SourceSystemType.Chiller, typeof(DragonChiller))]
    [InlineData(SourceSystemType.AbsorptionChiller, typeof(DragonAbsorptionChiller))]
    public void FanCoilMapsEveryCompatibleHydronicSource(
        SourceSystemType sourceType,
        Type expectedSourceType)
    {
        SourceSystem source = CreateSource(sourceType, "SOURCE-FANCOIL");
        SupplySystem supply = FanCoil("SUPPLY-FANCOIL", source);

        GreenRetrofitConversionResult result = Convert(supply, source);

        AssertSuccessfulAndSupported(result);
        DragonFanCoilUnit converted = Assert.IsType<DragonFanCoilUnit>(OnlySupply(result));
        Assert.Equal(supply.Id, converted.Id);
        Assert.Equal(expectedSourceType, converted.Source!.GetType());
        Assert.Equal(source.Id, converted.Source.Id);
        Assert.Equal(sourceType is SourceSystemType.Boiler or SourceSystemType.DistrictHeating, converted.CanHeat);
        Assert.Equal(sourceType is SourceSystemType.Chiller or SourceSystemType.AbsorptionChiller, converted.CanCool);
    }

    [Theory]
    [InlineData(SourceSystemType.Boiler, typeof(DragonBoiler))]
    [InlineData(SourceSystemType.DistrictHeating, typeof(DragonDistrictHeating))]
    public void HydronicRadiatorPreservesCapacityAndHeatingSourceIdentity(
        SourceSystemType sourceType,
        Type expectedSourceType)
    {
        SourceSystem source = CreateSource(sourceType, "SOURCE-RADIATOR");
        var supply = new SupplySystem(
            "radiator",
            SupplySystemType.Radiator,
            source.Id.Value,
            source,
            heatingCapacity: 7_654d,
            id: new EntityId("SUPPLY-RADIATOR"));

        GreenRetrofitConversionResult result = Convert(supply, source);

        AssertSuccessfulAndSupported(result);
        DragonRadiator converted = Assert.IsType<DragonRadiator>(OnlySupply(result));
        Assert.Equal(7_654d, converted.HeatingCapacityWatts);
        Assert.Equal(expectedSourceType, converted.Source!.GetType());
        Assert.True(converted.CanHeat);
        Assert.False(converted.CanCool);
    }

    [Fact]
    public void ElectricRadiatorPreservesExplicitHeatingCapacity()
    {
        var supply = new SupplySystem(
            "electric radiator",
            SupplySystemType.ElectricRadiator,
            heatingCapacity: 4_321d,
            id: new EntityId("SUPPLY-ELECTRIC-RADIATOR"));

        GreenRetrofitConversionResult result = Convert(supply);

        AssertSuccessfulAndSupported(result);
        DragonElectricRadiator converted = Assert.IsType<DragonElectricRadiator>(OnlySupply(result));
        Assert.Equal(supply.Id, converted.Id);
        Assert.Equal(4_321d, converted.HeatingCapacityWatts);
    }

    [Theory]
    [InlineData(SourceSystemType.HeatPump, typeof(DragonHeatPump))]
    [InlineData(SourceSystemType.GeothermalHeatPump, typeof(DragonGeothermalHeatPump))]
    public void AirHandlerPreservesHeatPumpValuesAndPublicIdentity(
        SourceSystemType sourceType,
        Type expectedSourceType)
    {
        var source = new SourceSystem(
            "heat pump",
            sourceType,
            FuelType.LiquefiedPetroleumGas,
            heatingCop: 4.1d,
            coolingCop: 5.2d,
            heatingCapacity: 11_111d,
            coolingCapacity: 22_222d,
            id: new EntityId("SOURCE-HEATPUMP"));
        var supply = new SupplySystem(
            "air handler",
            SupplySystemType.AirHandlingUnit,
            source.Id.Value,
            source,
            id: new EntityId("SUPPLY-AHU"));

        GreenRetrofitConversionResult result = Convert(supply, source);

        AssertSuccessfulAndSupported(result);
        DragonHeatPump converted = Assert.IsAssignableFrom<DragonHeatPump>(OnlySupply(result).Source);
        Assert.Equal(expectedSourceType, converted.GetType());
        Assert.Equal(source.Id, converted.Id);
        Assert.Equal(DragonFuel.Propane, converted.Fuel);
        Assert.Equal(4.1d, converted.HeatingCoefficientOfPerformance, 12);
        Assert.Equal(5.2d, converted.CoolingCoefficientOfPerformance, 12);
        Assert.Equal(11_111d, converted.HeatingCapacityWatts);
        Assert.Equal(22_222d, converted.CoolingCapacityWatts);
    }

    [Fact]
    public void DistrictHeatingPreservesNativeExternalSourceAndPinnedLegacyBoiler()
    {
        var source = new SourceSystem(
            "district service",
            SourceSystemType.DistrictHeating,
            heatingCapacity: 50_000d,
            hotWaterSupply: false,
            id: new EntityId("SOURCE-DISTRICT"));
        SupplySystem supply = FanCoil("SUPPLY-DISTRICT", source);

        GreenRetrofitConversionResult result = Convert(supply, source);

        AssertSuccessfulAndSupported(result);
        DragonDistrictHeating converted = Assert.IsType<DragonDistrictHeating>(OnlySupply(result).Source);
        Assert.Equal(source.Id, converted.Id);
        Assert.Equal(50_000d, converted.NominalCapacityWatts);
        IdfDocument native = result.RequireEnergyModel().ToIdfDocument();
        Assert.Single(native["DistrictHeating:Water"]);
        Assert.Empty(native["Boiler:HotWater"]);

        IdfDocument legacy = result.ToIdfDocument();
        Assert.Empty(legacy["DistrictHeating:Water"]);
        IdfObject boiler = Assert.Single(legacy["Boiler:HotWater"]);
        Assert.Equal("OtherFuel1", boiler[1]);
        Assert.Equal("autosize", boiler[2]);
        Assert.Equal("1.0", boiler[3]);
    }

    [Fact]
    public void BoilerPreservesFuelEfficiencyAndCapacity()
    {
        var source = new SourceSystem(
            "boiler",
            SourceSystemType.Boiler,
            FuelType.LiquefiedPetroleumGas,
            heatingCapacity: 32_100d,
            efficiency: 0.93d,
            hotWaterSupply: true,
            id: new EntityId("SOURCE-BOILER"));

        GreenRetrofitConversionResult result = Convert(FanCoil("SUPPLY-BOILER", source), source);

        AssertSuccessfulAndSupported(result);
        DragonBoiler converted = Assert.IsType<DragonBoiler>(OnlySupply(result).Source);
        Assert.Equal(source.Id, converted.Id);
        Assert.Equal(DragonFuel.Propane, converted.Fuel);
        Assert.Equal(0.93d, converted.NominalThermalEfficiency, 12);
        Assert.Equal(32_100d, converted.NominalCapacityWatts);
    }

    [Fact]
    public void PackagedAirConditionerUsesDocumentedNeutralHeatingCopApproximation()
    {
        var supply = new SupplySystem(
            "packaged",
            SupplySystemType.PackagedAirConditioner,
            coolingCop: 4.75d,
            coolingCapacity: 18_000d,
            id: new EntityId("SUPPLY-PACKAGED"));

        GreenRetrofitConversionResult result = Convert(supply);

        Assert.True(result.Success, Describe(result));
        Diagnostic warning = Assert.Single(
            result.Diagnostics,
            item => item.Code == "SD.CONVERSION.PACKAGED_AC_HEATING_COP_APPROXIMATED");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(supply.Id, warning.ObjectId);
        DragonPackagedAirConditioner converted =
            Assert.IsType<DragonPackagedAirConditioner>(OnlySupply(result));
        DragonHeatPump source = Assert.IsType<DragonHeatPump>(converted.Source);
        Assert.Equal(new EntityId("DedicatedHeatPump_for_SUPPLY-PACKAGED"), source.Id);
        Assert.Equal("DedicatedHeatPump0xAUTO0000_for_SUPPLY-PACKAGED", source.Name);
        Assert.Equal(DragonFuel.Electricity, source.Fuel);
        Assert.Equal(1d, source.HeatingCoefficientOfPerformance, 12);
        Assert.Equal(4.75d, source.CoolingCoefficientOfPerformance, 12);
        Assert.Equal(0.001d, source.HeatingCapacityWatts);
        Assert.Equal(18_000d, source.CoolingCapacityWatts);
        Assert.False(converted.CanHeat);
        Assert.True(converted.CanCool);

        IdfDocument idf = result.ToIdfDocument();
        IdfObject outdoor = Assert.Single(idf[source.IdfObjectType]);
        Assert.Equal(source.IdfObjectName, outdoor.Name);
        Assert.Equal(string.Empty, outdoor[20]);
        IdfObject terminals = Assert.Single(idf["ZoneTerminalUnitList"]);
        Assert.Equal(source.TerminalUnitListName, terminals.Name);
        IdfObject terminal = Assert.Single(
            idf["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"]);
        Assert.Contains("HVACOperating:AND:0xAUTO0000:INVERTED", terminal[1]);
        IdfObject thermostatControl = Assert.Single(
            idf["Schedule:Constant"],
            item => item.Name!.StartsWith(
                "ScheduleTypeForThermostat_for_",
                StringComparison.Ordinal));
        Assert.Equal("4", thermostatControl[2]);
        Assert.Single(idf["ThermostatSetpoint:DualSetpoint"]);
        Assert.Empty(idf["ThermostatSetpoint:SingleCooling"]);
    }

    private static SourceSystem CreateSource(SourceSystemType type, string id)
    {
        var sourceId = new EntityId(id);
        return type switch
        {
            SourceSystemType.Boiler => new SourceSystem(
                "boiler",
                type,
                FuelType.NaturalGas,
                heatingCapacity: 40_000d,
                efficiency: 0.91d,
                hotWaterSupply: true,
                id: sourceId),
            SourceSystemType.DistrictHeating => new SourceSystem(
                "district",
                type,
                heatingCapacity: 40_000d,
                hotWaterSupply: true,
                id: sourceId),
            SourceSystemType.Chiller => new SourceSystem(
                "chiller",
                type,
                coolingCop: 4.2d,
                coolingCapacity: 40_000d,
                compressorType: CompressorType.Screw,
                coolingTowerType: CoolingTowerType.Closed,
                coolingTowerCapacity: 45_000d,
                coolingTowerControl: CoolingTowerControl.TwoSpeed,
                id: sourceId),
            SourceSystemType.AbsorptionChiller => new SourceSystem(
                "absorption",
                type,
                FuelType.NaturalGas,
                coolingCop: 0.8d,
                coolingCapacity: 40_000d,
                boilerEfficiency: 0.87d,
                id: sourceId),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    private static SupplySystem FanCoil(string id, SourceSystem source)
    {
        return new SupplySystem(
            "fan coil",
            SupplySystemType.FanCoilUnit,
            source.Id.Value,
            source,
            id: new EntityId(id));
    }

    private static GreenRetrofitConversionResult Convert(
        SupplySystem supply,
        params SourceSystem[] sources)
    {
        GreenRetrofitModel template = GrmReader.ReadFile(Fixture()).RequireModel();
        Zone original = Assert.Single(template.Zones);
        var zone = new Zone(
            original.Name,
            original.FloorNumber,
            original.Height,
            original.Surfaces,
            original.ProfileName,
            original.Profile,
            original.LightDensity,
            new[] { new SupplySystemAssignment(supply.Id.Value, supply) },
            id: original.Id);
        var model = new GreenRetrofitModel(
            template.Name,
            template.NorthAxis,
            template.Address,
            template.Vintage,
            template.IsMultifamilyHousing,
            new[] { new BuildingFloor(zone.FloorNumber, new[] { zone }) },
            template.Materials,
            template.SurfaceConstructions,
            template.FenestrationConstructions,
            sources,
            new[] { supply },
            weather: template.Weather);
        return GreenRetrofitConverter.Convert(model);
    }

    private static DragonSupplySystem OnlySupply(GreenRetrofitConversionResult result)
    {
        EnergyModel model = result.RequireEnergyModel();
        return Assert.Single(Assert.Single(model.HvacAssignments).Supply.Systems);
    }

    private static string Idf(GreenRetrofitConversionResult result)
    {
        return IdfWriter.Write(result.ToIdfDocument());
    }

    private static void AssertSuccessfulAndSupported(GreenRetrofitConversionResult result)
    {
        Assert.True(result.Success, Describe(result));
        Assert.DoesNotContain(
            result.Diagnostics,
            item => item.Code == "SD.CONVERSION.SUPPLY_TYPE_NOT_IMPLEMENTED"
                || item.Code == "SD.CONVERSION.SOURCE_TYPE_NOT_IMPLEMENTED");
    }

    private static string Fixture()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                "grm",
                "ASHRAE 140 modified.grm");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SimpleDragon fixture.");
    }

    private static string Describe(GreenRetrofitConversionResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(item => item.Code + ": " + item.Message));
    }
}
