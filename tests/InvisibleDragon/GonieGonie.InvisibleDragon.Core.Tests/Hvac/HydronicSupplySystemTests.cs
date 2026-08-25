using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.InvisibleDragon.Tests.Model;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class HydronicSupplySystemTests
{
    [Fact]
    public void FanCoilAcceptsOnlyUpstreamHeatingSourcesAndValidFanInputs()
    {
        var boiler = new Boiler(new EntityId("BOILER-FCU"), "Boiler FCU", Fuel.NaturalGas);
        var district = new DistrictHeating(new EntityId("DISTRICT-FCU"), "District FCU");
        var heatPump = new HeatPump(new EntityId("HEATPUMP-FCU"), "Heat pump FCU", Fuel.Electricity, 3, 3);
        var coolingTower = new OpenSingleSpeedCoolingTower(new EntityId("TOWER-FCU"), "Tower FCU");
        var chiller = new Chiller(
            new EntityId("CHILLER-FCU"),
            "Chiller FCU",
            5,
            CompressorType.Turbo,
            coolingTower);
        var absorptionTower = new OpenSingleSpeedCoolingTower(
            new EntityId("TOWER-ABS-FCU"),
            "Absorption tower FCU");
        var absorption = new AbsorptionChiller(
            new EntityId("ABSORPTION-FCU"),
            "Absorption FCU",
            0.8,
            boiler,
            absorptionTower);

        var fromBoiler = new FanCoilUnit(new EntityId("FCU-BOILER"), "Boiler terminal", boiler);
        var fromDistrict = new FanCoilUnit(new EntityId("FCU-DISTRICT"), "District terminal", district);
        var fromChiller = new FanCoilUnit(new EntityId("FCU-CHILLER"), "Chiller terminal", chiller);
        var fromAbsorption = new FanCoilUnit(new EntityId("FCU-ABSORPTION"), "Absorption terminal", absorption);

        Assert.Same(boiler, fromBoiler.Source);
        Assert.Same(district, fromDistrict.Source);
        Assert.True(fromBoiler.CanHeat);
        Assert.False(fromBoiler.CanCool);
        Assert.False(fromChiller.CanHeat);
        Assert.True(fromChiller.CanCool);
        Assert.False(fromAbsorption.CanHeat);
        Assert.True(fromAbsorption.CanCool);
        Assert.Throws<ArgumentException>(
            () => new FanCoilUnit(new EntityId("FCU-HP"), "Invalid terminal", heatPump));
        Assert.Throws<ArgumentNullException>(
            () => new FanCoilUnit(new EntityId("FCU-NULL"), "Null terminal", null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FanCoilUnit(new EntityId("FCU-FAN"), "Invalid fan", boiler, fanTotalEfficiency: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FanCoilUnit(new EntityId("FCU-PRESSURE"), "Invalid pressure", boiler, fanPressureRisePascals: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FanCoilUnit(new EntityId("FCU-MOTOR"), "Invalid motor", boiler, motorEfficiency: 1.1));
    }

    [Fact]
    public void FanCoilConnectsCoolingCoilToChillerDemandLoop()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-FCU-COOL", "Cooling Fan Coil Zone");
        var tower = new OpenSingleSpeedCoolingTower(new EntityId("TOWER-FCU-COOL"), "Cooling FCU Tower");
        var chiller = new Chiller(
            new EntityId("CHILLER-FCU-COOL"),
            "Cooling FCU Chiller",
            5,
            CompressorType.Turbo,
            tower);
        var fanCoil = new FanCoilUnit(new EntityId("FCU-COOL"), "Cooling terminal", chiller);

        IdfDocument document = ModelWith(zone, fanCoil).ToIdfDocument();

        const string equipmentName = "FanCoilUnit_named_Cooling terminal_for_Cooling Fan Coil Zone";
        IdfObject terminal = document["ZoneHVAC:FourPipeFanCoil"][equipmentName];
        Assert.Equal(terminal[1], terminal[7]);
        Assert.Equal("autosize", terminal[16]);
        Assert.Equal("0", terminal[21]);
        string coolingCoilName = $"CoolingCoil_for_{equipmentName}";
        IdfObject coolingCoil = document["Coil:Cooling:Water"][coolingCoilName];
        Assert.Equal("autosize", coolingCoil[2]);
        IdfObject heatingCoil = document["Coil:Heating:Water"][$"HeatingCoil_for_{equipmentName}"];
        Assert.Equal("ALLOFF", heatingCoil[1]);
        Assert.Equal("0", heatingCoil[3]);
        IdfObject demand = document["Branch"][$"{chiller.LoopName} Demand {equipmentName}"];
        Assert.Equal("Coil:Cooling:Water", demand[2]);
        Assert.Equal(coolingCoilName, demand[3]);
        Assert.Equal(coolingCoil[9], demand[4]);
        Assert.Equal(coolingCoil[10], demand[5]);
        Assert.Single(document["DistrictHeating:Water"]);
        Assert.Empty(document["Boiler:HotWater"]);
        Assert.DoesNotContain(
            document["Branch"],
            item => item.Name?.StartsWith("NonUsed_", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void LegacyCoolingOnlyFanCoilExportsPinnedDisabledHeatingLoop()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone(
            "ZONE-FCU-LEGACY-COOL",
            "Legacy Cooling Fan Coil Zone");
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("TOWER-FCU-LEGACY-COOL"),
            "Legacy Cooling FCU Tower");
        var chiller = new Chiller(
            new EntityId("CHILLER-FCU-LEGACY-COOL"),
            "Legacy Cooling FCU Chiller",
            5,
            CompressorType.Turbo,
            tower);
        var fanCoil = new FanCoilUnit(
            new EntityId("FCU-LEGACY-COOL"),
            "Legacy Cooling terminal",
            chiller);

        IdfDocument document = ModelWith(zone, fanCoil).ToIdfDocument(
            options: new EnergyModelIdfOptions
            {
                UseLegacySimpleDragonHvacTopology = true,
            });

        const string equipmentName =
            "FanCoilUnit_named_Legacy Cooling terminal_for_Legacy Cooling Fan Coil Zone";
        IdfObject terminal = document["ZoneHVAC:FourPipeFanCoil"][equipmentName];
        Assert.NotEmpty(terminal[1]);
        Assert.Equal(string.Empty, terminal[7]);
        Assert.Equal("autosize", terminal[16]);
        Assert.Equal("0", terminal[21]);

        string coolingCoilName = $"CoolingCoil_for_{equipmentName}";
        IdfObject coolingCoil = document["Coil:Cooling:Water"][coolingCoilName];
        string heatingCoilName = $"HeatingCoil_for_{equipmentName}";
        IdfObject heatingCoil = document["Coil:Heating:Water"][heatingCoilName];
        Assert.Equal("ALLOFF", heatingCoil[1]);
        Assert.Equal("autosize", heatingCoil[3]);

        string mainBranchName =
            $"{chiller.LoopName} Demand Main_{nameof(FanCoilUnit)}_for_{zone.Name}";
        IdfObject coolingBranch = document["Branch"][mainBranchName];
        Assert.Equal("Coil:Cooling:Water", coolingBranch[2]);
        Assert.Equal(coolingCoilName, coolingBranch[3]);
        Assert.Equal(coolingCoil[9], coolingBranch[4]);
        Assert.Equal(coolingCoil[10], coolingBranch[5]);

        IdfObject heatingBranch = document["Branch"][$"NonUsed_{mainBranchName}"];
        Assert.Equal("Coil:Heating:Water", heatingBranch[2]);
        Assert.Equal(heatingCoilName, heatingBranch[3]);
        Assert.Equal(heatingCoil[4], heatingBranch[4]);
        Assert.Equal(heatingCoil[5], heatingBranch[5]);

        string boilerName = $"Boiler_named_NonUsedBoiler_for_{equipmentName}";
        IdfObject boiler = document["Boiler:HotWater"][boilerName];
        Assert.Equal("Coal", boiler[1]);
        Assert.Equal("1E-10", boiler[2]);
        Assert.Equal("1E-10", boiler[3]);
        Assert.Equal("LeavingBoiler", boiler[4]);
        Assert.Equal("autosize", boiler[6]);
        Assert.Equal("0", boiler[7]);
        Assert.Equal("1", boiler[8]);
        Assert.Equal("1", boiler[9]);
        Assert.Equal("99.9", boiler[12]);
        Assert.Equal("NotModulated", boiler[13]);
        Assert.Equal("0", boiler[14]);
        Assert.Equal("1", boiler[15]);
        Assert.Equal("General", boiler[16]);

        string dummyLoopName = $"Loop_for_NonUsedBoiler_for_{equipmentName}";
        IdfObject availability = document["AvailabilityManager:Scheduled"]
            [$"{dummyLoopName} AvailabilityManager"];
        Assert.Equal("ALLOFF", availability[1]);
        Assert.Empty(document["DistrictHeating:Water"]);
    }

    [Fact]
    public void FanCoilExportsDeterministicAirNodesAndHeatingPlantDemand()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-FCU", "Fan Coil Zone");
        var boiler = new Boiler(new EntityId("BOILER-FCU-GRAPH"), "Fan Coil Boiler", Fuel.NaturalGas);
        var fanCoil = new FanCoilUnit(new EntityId("FCU-GRAPH"), "Terminal", boiler);
        EnergyModel model = ModelWith(zone, fanCoil);

        IdfDocument first = model.ToIdfDocument();
        IdfDocument second = model.ToIdfDocument();

        Assert.Equal(IdfWriter.Write(first), IdfWriter.Write(second));
        Assert.Single(first["ZoneHVAC:FourPipeFanCoil"]);
        Assert.Single(first["Fan:OnOff"]);
        Assert.Single(first["OutdoorAir:Mixer"]);
        Assert.Single(first["OutdoorAir:NodeList"]);
        Assert.Single(first["Coil:Cooling:Water"]);
        Assert.Single(first["Coil:Heating:Water"]);
        Assert.Single(first["Curve:Exponent"]);
        Assert.Single(first["Curve:Cubic"]);

        const string equipmentName = "FanCoilUnit_named_Terminal_for_Fan Coil Zone";
        IdfObject terminal = first["ZoneHVAC:FourPipeFanCoil"][equipmentName];
        Assert.Equal($"{equipmentName} Air InletNode", terminal[8]);
        Assert.Equal($"{equipmentName} Air OutletNode", terminal[9]);
        Assert.Equal("0", terminal[16]);
        Assert.Equal("autosize", terminal[21]);

        string heatingCoilName = $"HeatingCoil_for_{equipmentName}";
        IdfObject heatingCoil = first["Coil:Heating:Water"][heatingCoilName];
        Assert.Equal($"{heatingCoilName} Water InletNode", heatingCoil[4]);
        Assert.Equal($"{heatingCoilName} Water OutletNode", heatingCoil[5]);
        IdfObject demand = first["Branch"][$"{boiler.LoopName} Demand {equipmentName}"];
        Assert.Equal("Coil:Heating:Water", demand[2]);
        Assert.Equal(heatingCoilName, demand[3]);
        Assert.Equal(heatingCoil[4], demand[4]);
        Assert.Equal(heatingCoil[5], demand[5]);

        IdfObject connection = Assert.Single(first["ZoneHVAC:EquipmentConnections"]);
        Assert.NotEmpty(connection[2]);
        Assert.NotEmpty(connection[3]);
    }

    [Theory]
    [InlineData(null, "autosize")]
    [InlineData(6500d, "6500")]
    public void HydronicRadiatorExportsAutosizedOrExplicitCapacity(
        double? heatingCapacityWatts,
        string expectedCapacity)
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-RADIATOR", "Radiator Zone");
        var district = new DistrictHeating(new EntityId("DISTRICT-RADIATOR"), "Radiator District");
        var radiator = new Radiator(
            new EntityId("RADIATOR"),
            "Perimeter",
            district,
            heatingCapacityWatts,
            radiantFraction: 0.2);

        IdfDocument document = ModelWith(zone, radiator).ToIdfDocument();

        const string equipmentName = "Radiator_named_Perimeter_for_Radiator Zone";
        IdfObject design = document["ZoneHVAC:Baseboard:RadiantConvective:Water:Design"][$"DesignOf_{equipmentName}"];
        IdfObject equipment = document["ZoneHVAC:Baseboard:RadiantConvective:Water"][equipmentName];
        Assert.Equal("0.2", design[5]);
        Assert.Equal(expectedCapacity, equipment[7]);
        Assert.Equal("autosize", equipment[8]);
        Assert.Equal($"{equipmentName} Water InletNode", equipment[3]);
        Assert.Equal($"{equipmentName} Water OutletNode", equipment[4]);

        IdfObject demand = document["Branch"][$"{district.LoopName} Demand {equipmentName}"];
        Assert.Equal(equipment.ObjectType, demand[2]);
        Assert.Equal(equipmentName, demand[3]);
        Assert.Equal(equipment[3], demand[4]);
        Assert.Equal(equipment[4], demand[5]);
    }

    [Fact]
    public void HydronicRadiatorRejectsInvalidSourceCapacityAndRadiantFraction()
    {
        var boiler = new Boiler(new EntityId("BOILER-RAD-VALIDATION"), "Validation boiler", Fuel.NaturalGas);
        var heatPump = new HeatPump(new EntityId("HP-RAD-VALIDATION"), "Validation heat pump", Fuel.Electricity, 3, 3);

        Assert.Throws<ArgumentException>(
            () => new Radiator(new EntityId("RAD-SOURCE"), "Invalid source", heatPump));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Radiator(new EntityId("RAD-CAPACITY"), "Invalid capacity", boiler, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Radiator(new EntityId("RAD-FRACTION"), "Invalid fraction", boiler, radiantFraction: 1.1));
    }

    [Theory]
    [InlineData(null, "autosize")]
    [InlineData(4200d, "4200")]
    public void ElectricRadiatorRetainsCapacityAndEnergyInputParameters(
        double? heatingCapacityWatts,
        string expectedCapacity)
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-ELECTRIC-RAD", "Electric Radiator Zone");
        var radiator = new ElectricRadiator(
            new EntityId("ELECTRIC-RAD"),
            "Electric perimeter",
            heatingCapacityWatts,
            efficiency: 0.95,
            radiantFraction: 0.25);

        IdfDocument document = ModelWith(zone, radiator).ToIdfDocument();

        IdfObject equipment = Assert.Single(document["ZoneHVAC:Baseboard:RadiantConvective:Electric"]);
        Assert.Equal(expectedCapacity, equipment[3]);
        Assert.Equal("0.95", equipment[6]);
        Assert.Equal("0.25", equipment[7]);
        Assert.Equal("0", equipment[8]);
        Assert.Empty(document["PlantLoop"]);
    }

    [Fact]
    public void ElectricRadiatorOmitsTrailingPeopleFractionOnlyForLegacySimpleDragon()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone(
            "ZONE-ELECTRIC-RAD-LEGACY",
            "Electric Radiator Legacy Zone");
        var radiator = new ElectricRadiator(
            new EntityId("ELECTRIC-RAD-LEGACY"),
            "Legacy electric perimeter",
            4200,
            radiantFraction: 0);
        EnergyModel model = ModelWith(zone, radiator);
        var legacyOptions = new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonHvacTopology = true,
        };

        IdfObject native = Assert.Single(
            model.ToIdfDocument()["ZoneHVAC:Baseboard:RadiantConvective:Electric"]);
        IdfObject legacy = Assert.Single(
            model.ToIdfDocument(options: legacyOptions)[
                "ZoneHVAC:Baseboard:RadiantConvective:Electric"]);

        Assert.Equal(9, native.Count);
        Assert.Equal("0", native[8]);
        Assert.Equal(8, legacy.Count);
        Assert.Equal("0", legacy[7]);
    }

    [Fact]
    public void ModelDefinitionEqualityIncludesEveryNewSupplyParameter()
    {
        var boiler = new Boiler(new EntityId("BOILER-EQUALITY"), "Equality boiler", Fuel.NaturalGas);
        AssertConflictingDefinitions(
            new FanCoilUnit(new EntityId("SUPPLY-EQUALITY"), "Equality supply", boiler, fanTotalEfficiency: 0.7),
            new FanCoilUnit(new EntityId("SUPPLY-EQUALITY"), "Equality supply", boiler, fanTotalEfficiency: 0.8));
        AssertConflictingDefinitions(
            new Radiator(new EntityId("SUPPLY-EQUALITY"), "Equality supply", boiler, 4000, 0.1),
            new Radiator(new EntityId("SUPPLY-EQUALITY"), "Equality supply", boiler, 5000, 0.1));
        AssertConflictingDefinitions(
            new ElectricRadiator(
                new EntityId("SUPPLY-EQUALITY"),
                "Equality supply",
                4000,
                efficiency: 0.9,
                radiantFraction: 0.1),
            new ElectricRadiator(
                new EntityId("SUPPLY-EQUALITY"),
                "Equality supply",
                4000,
                efficiency: 0.95,
                radiantFraction: 0.1));
    }

    [Fact]
    public void HydronicFamiliesPassInstalledEnergyPlus242IddValidation()
    {
        string? iddPath = FindInstalledIdd();
        if (iddPath is null)
        {
            return;
        }

        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-HYDRONIC-IDD", "Hydronic IDD Zone");
        Zone coolingZone = EnergyModelFixtureMatrixTests.CreateZone(
            "ZONE-HYDRONIC-COOL-IDD",
            "Hydronic Cooling IDD Zone");
        var boiler = new Boiler(new EntityId("BOILER-HYDRONIC-IDD"), "Hydronic IDD Boiler", Fuel.NaturalGas);
        var fanCoil = new FanCoilUnit(new EntityId("FCU-HYDRONIC-IDD"), "Hydronic IDD FCU", boiler);
        var radiator = new Radiator(
            new EntityId("RAD-HYDRONIC-IDD"),
            "Hydronic IDD Radiator",
            boiler,
            5000,
            0.15);
        var tower = new OpenSingleSpeedCoolingTower(
            new EntityId("TOWER-HYDRONIC-IDD"),
            "Hydronic IDD Tower");
        var chiller = new Chiller(
            new EntityId("CHILLER-HYDRONIC-IDD"),
            "Hydronic IDD Chiller",
            5,
            CompressorType.Turbo,
            tower);
        var coolingFanCoil = new FanCoilUnit(
            new EntityId("FCU-HYDRONIC-COOL-IDD"),
            "Hydronic Cooling IDD FCU",
            chiller);
        IddSchema schema = IddParser.ParseFile(iddPath);
        IdfDocument heatingDocument = new EnergyModel(
            "Hydronic IDD families",
            new[] { zone },
            new[]
            {
                new ZoneHvacAssignment(
                    zone.Id,
                    new SupplyGroup(new SupplySystem[] { fanCoil, radiator })),
            }).ToIdfDocument(schema);
        IdfDocument coolingDocument = new EnergyModel(
            "Hydronic cooling IDD family",
            new[] { coolingZone },
            new[]
            {
                new ZoneHvacAssignment(
                    coolingZone.Id,
                    new SupplyGroup(new SupplySystem[] { coolingFanCoil })),
            }).ToIdfDocument(schema);

        AssertIddValid(heatingDocument);
        AssertIddValid(coolingDocument);
    }

    private static EnergyModel ModelWith(Zone zone, SupplySystem system) => new(
        $"Model for {system.Name}",
        new[] { zone },
        new[] { new ZoneHvacAssignment(zone.Id, new SupplyGroup(new[] { system })) });

    private static void AssertConflictingDefinitions(SupplySystem first, SupplySystem second)
    {
        Zone firstZone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-EQUALITY-A", "Equality Zone A");
        Zone secondZone = EnergyModelFixtureMatrixTests.CreateZone("ZONE-EQUALITY-B", "Equality Zone B");
        var model = new EnergyModel(
            "Supply equality",
            new[] { firstZone, secondZone },
            new[]
            {
                new ZoneHvacAssignment(firstZone.Id, new SupplyGroup(new[] { first })),
                new ZoneHvacAssignment(secondZone.Id, new SupplyGroup(new[] { second })),
            });

        ValidationResult result = model.Validate();

        Assert.Contains(
            result.Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
    }

    private static void AssertIddValid(IdfDocument document)
    {
        ValidationResult result = IdfValidator.Validate(
            document,
            new IdfValidationOptions { ValidateSchemaDefaults = false });

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    private static string? FindInstalledIdd()
    {
        string? root = Environment.GetEnvironmentVariable("GONIEGONIE_ENERGYPLUS_ROOT")
            ?? Environment.GetEnvironmentVariable("DRAGONS_ENERGYPLUS_HOME")
            ?? Environment.GetEnvironmentVariable("ENERGYPLUS_HOME")
            ?? Environment.GetEnvironmentVariable("ENERGYPLUS_ROOT")
            ?? @"C:\EnergyPlusV24-2-0";
        string path = Path.Combine(root, "Energy+.idd");
        return File.Exists(path) ? path : null;
    }
}
