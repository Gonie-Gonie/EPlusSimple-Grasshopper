using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelFixtureMatrixTests
{
    [Fact]
    public void Ashrae140RepresentativePathExportsExpectedHvacObjectMatrix()
    {
        EnergyModel model = CreateRepresentativeModel();

        IdfDocument document = model.ToIdfDocument();

        Assert.Equal(8, document["Curve:Biquadratic"].Count);
        Assert.Equal(8, document["Curve:Cubic"].Count);
        Assert.Equal(7, document["Curve:Linear"].Count);
        Assert.Single(document["Curve:Quadratic"]);
        Assert.Single(document["AirConditioner:VariableRefrigerantFlow"]);
        Assert.Single(document["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"]);
        Assert.Single(document["Boiler:HotWater"]);
        Assert.Single(document["ZoneHVAC:LowTemperatureRadiant:VariableFlow"]);
        Assert.Single(document["PlantLoop"]);
        Assert.Equal(5, document["Pipe:Adiabatic"].Count);
        Assert.Equal(8, document["Branch"].Count);
        Assert.Equal(2, document["BranchList"].Count);
        Assert.Equal(2, document["Connector:Splitter"].Count);
        Assert.Equal(2, document["Connector:Mixer"].Count);
        Assert.Single(document["OutputControl:Table:Style"]);
        Assert.Single(document["Output:Table:SummaryReports"]);
        Assert.Single(document["Output:Table:Monthly"]);
    }

    [Fact]
    public void RepresentativeSubsystemCountsMatchPinnedPythonFixtureSummary()
    {
        string fixture = Path.Combine(
            FindRepositoryRoot(),
            "fixtures",
            "reference",
            "python-0.7.0",
            "ashrae-140-modified.idf-summary.json");
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(fixture));
        JsonElement expected = summary.RootElement.GetProperty("object_counts");
        IdfDocument actual = CreateRepresentativeModel().ToIdfDocument();
        string[] subsystemTypes =
        {
            "ConstructionProperty:InternalHeatSource",
            "DesignSpecification:OutdoorAir",
            "DesignSpecification:ZoneAirDistribution",
            "Sizing:Zone",
            "Sizing:Plant",
            "ZoneControl:Thermostat",
            "ThermostatSetpoint:DualSetpoint",
            "ZoneHVAC:TerminalUnit:VariableRefrigerantFlow",
            "ZoneHVAC:LowTemperatureRadiant:VariableFlow",
            "ZoneHVAC:LowTemperatureRadiant:VariableFlow:Design",
            "ZoneHVAC:LowTemperatureRadiant:SurfaceGroup",
            "ZoneHVAC:EquipmentList",
            "ZoneHVAC:EquipmentConnections",
            "Fan:ConstantVolume",
            "Coil:Cooling:DX:VariableRefrigerantFlow",
            "Coil:Heating:DX:VariableRefrigerantFlow",
            "AirConditioner:VariableRefrigerantFlow",
            "ZoneTerminalUnitList",
            "Branch",
            "BranchList",
            "Connector:Splitter",
            "Connector:Mixer",
            "ConnectorList",
            "NodeList",
            "Pipe:Adiabatic",
            "Pump:VariableSpeed",
            "Boiler:HotWater",
            "PlantLoop",
            "PlantEquipmentList",
            "PlantEquipmentOperation:HeatingLoad",
            "PlantEquipmentOperationSchemes",
            "AvailabilityManager:Scheduled",
            "AvailabilityManagerAssignmentList",
            "SetpointManager:Scheduled",
            "Curve:Linear",
            "Curve:Quadratic",
            "Curve:Cubic",
            "Curve:Biquadratic",
            "Output:Table:SummaryReports",
            "Output:Table:Monthly",
            "OutputControl:Table:Style",
        };

        foreach (string objectType in subsystemTypes)
        {
            Assert.Equal(expected.GetProperty(objectType).GetInt32(), actual[objectType].Count);
        }
    }

    [Fact]
    public void AssemblyIsDeterministicAndRetainsEveryPolygonVertex()
    {
        EnergyModel model = CreateRepresentativeModel();

        IdfDocument first = model.ToIdfDocument();
        IdfDocument second = model.ToIdfDocument();

        Assert.Equal(IdfWriter.Write(first), IdfWriter.Write(second));
        IdfObject floor = first["BuildingSurface:Detailed"]["Floor"];
        Assert.Equal("autocalculate", floor[10]);
        Assert.Equal(23, floor.Count);
        Assert.Equal(
            new[] { "0.0", "0.0", "0.0", "8.0", "0.0", "0.0", "8.0", "6.0", "0.0", "0.0", "6.0", "0.0" },
            floor.Fields.Skip(11).Select(field => field.Value));
    }

    [Fact]
    public void ElectricRadiantAndDistrictHeatingCoverAlternativeFamilies()
    {
        Zone zone = CreateZone("ZONE-ALT", "Alternative");
        var district = new DistrictHeating(new EntityId("HVAC-DISTRICT"), "District");
        var hydronic = new RadiantFloor(new EntityId("HVAC-HYDRONIC"), "Hydronic", district);
        var electric = new ElectricRadiantFloor(new EntityId("HVAC-ELECTRIC"), "Electric");
        var model = new EnergyModel(
            "Alternatives",
            new[] { zone },
            new[] { new ZoneHvacAssignment(zone.Id, new SupplyGroup(new SupplySystem[] { hydronic, electric })) });

        IdfDocument document = model.ToIdfDocument();

        Assert.Single(document["DistrictHeating:Water"]);
        Assert.Single(document["ZoneHVAC:LowTemperatureRadiant:VariableFlow"]);
        Assert.Single(document["ZoneHVAC:LowTemperatureRadiant:Electric"]);
        Assert.Equal(2, document["ZoneHVAC:LowTemperatureRadiant:SurfaceGroup"].Count);
    }

    [Fact]
    public void LegacyElectricRadiantFloorUsesPinnedGroupAndZeroFlowControl()
    {
        Zone zone = CreateZone("ZONE-ELECTRIC-RADIANT-LEGACY", "Legacy Electric Radiant");
        var electric = new ElectricRadiantFloor(
            new EntityId("HVAC-ELECTRIC-RADIANT-LEGACY"),
            "Legacy Electric Radiant");
        var model = new EnergyModel(
            "Legacy electric radiant",
            new[] { zone },
            new[]
            {
                new ZoneHvacAssignment(
                    zone.Id,
                    new SupplyGroup(new SupplySystem[] { electric })),
            });
        var legacyOptions = new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonHvacTopology = true,
        };

        IdfDocument native = model.ToIdfDocument();
        IdfDocument legacy = model.ToIdfDocument(options: legacyOptions);

        IdfObject nativeTerminal = Assert.Single(native["ZoneHVAC:LowTemperatureRadiant:Electric"]);
        IdfObject legacyTerminal = Assert.Single(legacy["ZoneHVAC:LowTemperatureRadiant:Electric"]);
        Assert.Equal(
            $"ElectricRadiantFloorSurfaceGroup_for_{zone.Name}",
            nativeTerminal[3]);
        Assert.Equal("HalfFlowPower", nativeTerminal[9]);
        Assert.Equal($"RadiantFloorSurfaceGroup_for_{zone.Name}", legacyTerminal[3]);
        Assert.Equal("ZeroFlowPower", legacyTerminal[9]);
        Assert.Single(
            legacy["ZoneHVAC:LowTemperatureRadiant:SurfaceGroup"],
            item => item.Name == $"RadiantFloorSurfaceGroup_for_{zone.Name}");
    }

    [Fact]
    public void LegacySimpleDragonRetainsPinnedZeroDensityLightsObject()
    {
        Zone template = CreateZone("ZONE-ZERO-LIGHTS", "Zero Lights");
        var profile = new ZoneProfile(
            template.Profile.Id,
            template.Profile.Name,
            template.Profile.HeatingSetpoint,
            template.Profile.CoolingSetpoint,
            template.Profile.HvacAvailability,
            lighting: Schedule.Constant(
                "Zero Lights Schedule",
                1,
                ScheduleType.Fraction));
        var zone = new Zone(
            template.Id,
            template.Name,
            template.Surfaces,
            profile,
            lightingPowerDensityWattsPerSquareMetre: 0);
        var model = new EnergyModel("Zero lights", new[] { zone });

        IdfDocument native = model.ToIdfDocument();
        IdfDocument legacy = model.ToIdfDocument(
            options: new EnergyModelIdfOptions
            {
                UseLegacySimpleDragonScheduleMetadata = true,
            });

        Assert.Empty(native["Lights"]);
        IdfObject lights = Assert.Single(legacy["Lights"]);
        Assert.Equal("0.0", lights[5]);
    }

    [Fact]
    public void LegacySimpleDragonThermostatAlwaysUsesDualSetpointWithoutChangingNativeDefault()
    {
        Zone zone = CreateZone("ZONE-THERMOSTAT", "Thermostat Zone");
        var heatingOnly = new ElectricRadiator(
            new EntityId("HVAC-THERMOSTAT-HEAT"),
            "Heating only");
        var model = new EnergyModel(
            "Thermostat modes",
            new[] { zone },
            new[]
            {
                new ZoneHvacAssignment(
                    zone.Id,
                    new SupplyGroup(new SupplySystem[] { heatingOnly })),
            });

        IdfDocument native = model.ToIdfDocument();
        IdfObject nativeControl = Assert.Single(
            native["Schedule:Constant"],
            item => item.Name == "ScheduleTypeForThermostat_for_Thermostat Zone");
        Assert.Equal("1", nativeControl[2]);
        Assert.Single(native["ThermostatSetpoint:SingleHeating"]);
        Assert.Empty(native["ThermostatSetpoint:DualSetpoint"]);

        var options = new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonHvacTopology = true,
        };
        IdfDocument legacy = model.ToIdfDocument(options: options);
        IdfObject legacyControl = Assert.Single(
            legacy["Schedule:Constant"],
            item => item.Name == "ScheduleTypeForThermostat_for_Thermostat Zone");
        Assert.Equal("4", legacyControl[2]);
        Assert.Single(legacy["ThermostatSetpoint:DualSetpoint"]);
        Assert.Empty(legacy["ThermostatSetpoint:SingleHeating"]);

        var context = new IdfGenerationContext(options: options);
        Assert.Same(options, context.Options);
        Assert.False(new IdfGenerationContext().Options.UseLegacySimpleDragonHvacTopology);
    }

    [Fact]
    public void EnergyRecoveryVentilatorAndPhotovoltaicFamiliesJoinZoneAssembly()
    {
        Zone zone = CreateZone("ZONE-ERV", "Ventilated");
        var erv = new EnergyRecoveryVentilator(new EntityId("HVAC-ERV"), "ERV", 0.75, 0.65, 0.2);
        var pv = new PhotovoltaicPanel(new EntityId("PV-ERV"), "PV", 12, 30, 180, 0.2);
        var model = new EnergyModel(
            "Ventilation and PV",
            new[] { zone },
            ventilationAssignments: new[] { new ZoneVentilationAssignment(zone.Id, erv) },
            photovoltaicPanels: new[] { pv });

        IdfDocument document = model.ToIdfDocument();

        Assert.Single(document["ZoneHVAC:EnergyRecoveryVentilator"]);
        Assert.Single(document["HeatExchanger:AirToAir:SensibleAndLatent"]);
        Assert.Equal(2, document["Fan:OnOff"].Count);
        Assert.Single(document["Generator:Photovoltaic"]);
        Assert.Single(document["ElectricLoadCenter:Distribution"]);
        Assert.Contains(document["ZoneHVAC:EquipmentList"], item => item[2] == "ZoneHVAC:EnergyRecoveryVentilator");
    }

    [Fact]
    public void LegacySimpleDragonVentilationReducesZoneLoadAndPreservesNativeDefault()
    {
        Zone template = CreateZone("ZONE-LEGACY-ERV", "Legacy Ventilated");
        var profile = new ZoneProfile(
            template.Profile.Id,
            template.Profile.Name,
            template.Profile.HeatingSetpoint,
            template.Profile.CoolingSetpoint,
            template.Profile.HvacAvailability,
            Schedule.Constant("Legacy Occupancy", 0.1d));
        var zone = new Zone(
            template.Id,
            template.Name,
            template.Surfaces,
            profile);
        var ventilator = new EnergyRecoveryVentilator(
            new EntityId("HVAC-LEGACY-ERV"),
            "Legacy ERV",
            0.75d,
            0.65d,
            0.2d);
        var model = new EnergyModel(
            "Legacy ventilation",
            new[] { zone },
            ventilationAssignments: new[]
            {
                new ZoneVentilationAssignment(zone.Id, ventilator),
            });

        IdfDocument native = model.ToIdfDocument();

        Assert.Single(native["ZoneHVAC:EnergyRecoveryVentilator"]);
        Assert.Single(native["HeatExchanger:AirToAir:SensibleAndLatent"]);
        Assert.Equal(2, native["Fan:OnOff"].Count);
        IdfObject activity = Assert.Single(
            native["Schedule:Constant"],
            item => item.Name == "$DEFAULT$PEOPLEACTIVITY");
        Assert.Equal("107.0", activity[2]);
        Assert.Equal("0.0083", Assert.Single(native["ZoneVentilation:DesignFlowRate"])[6]);

        IdfDocument legacy = model.ToIdfDocument(options: new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonVentilation = true,
        });

        IdfObject reduced = Assert.Single(legacy["ZoneVentilation:DesignFlowRate"]);
        Assert.Equal(string.Empty, reduced[2]);
        Assert.Equal("Flow/Person", reduced[3]);
        Assert.Equal("0.0024900000000000005", reduced[6]);
        Assert.Equal("Exhaust", reduced[8]);
        Assert.Equal("166.66666666666663", reduced[9]);
        Assert.Equal("0.85", reduced[10]);
        Assert.Empty(legacy["OutdoorAir:Node"]);
        Assert.Empty(legacy["HeatExchanger:AirToAir:SensibleAndLatent"]);
        Assert.Empty(legacy["Fan:OnOff"]);
        Assert.Empty(legacy["ZoneHVAC:EnergyRecoveryVentilator:Controller"]);
        Assert.Empty(legacy["ZoneHVAC:EnergyRecoveryVentilator"]);
        Assert.Empty(legacy["ZoneHVAC:EquipmentList"]);
        Assert.Empty(legacy["HVACTemplate:Zone:IdealLoadsAirSystem"]);
        Assert.False(new EnergyModelIdfOptions().UseLegacySimpleDragonVentilation);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(999d)]
    [InlineData(1000d)]
    public void OpaqueConstructionLayerAlwaysEmitsUpstreamMaterial(
        double arealHeatCapacity)
    {
        var material = new Material("Low volumetric capacity", 0.04, 0.01, 100);
        double thickness = arealHeatCapacity
            / (material.DensityKilogramsPerCubicMetre * material.SpecificHeatJoulesPerKilogramKelvin);
        var construction = new OpaqueConstruction(
            "Low capacity construction",
            new[] { new Layer("Low capacity layer", material, thickness) });
        var surface = new Surface(
            new EntityId("LOW-CAPACITY-FLOOR"),
            "Low capacity floor",
            SurfaceType.Floor,
            construction,
            SurfaceBoundary.Ground,
            TestDomainFactory.Square(reverse: true));
        var zone = new Zone(
            new EntityId("LOW-CAPACITY-ZONE"),
            "Low capacity zone",
            new[] { surface },
            TestDomainFactory.EmptyProfile("LOW-CAPACITY-PROFILE"));

        IdfDocument document = new EnergyModel("Low capacity model", new[] { zone }).ToIdfDocument();

        IdfObject emitted = Assert.Single(document["Material"]);
        Assert.Equal("Low capacity layer", emitted.Name);
        Assert.Equal(
            InvariantText.FormatPythonFloat(thickness),
            emitted[2]);
        Assert.Empty(document["Material:NoMass"]);
    }

    internal static EnergyModel CreateRepresentativeModel()
    {
        Zone zone = CreateZone("ZONE-140", "ASHRAE 140 Zone");
        var heatPump = new HeatPump(new EntityId("HVAC-HP"), "Heat pump", Fuel.Electricity, 3.2, 3.0);
        var air = new AirHandlingUnit(new EntityId("HVAC-AHU"), "AHU", heatPump);
        var boiler = new Boiler(new EntityId("HVAC-BOILER"), "Boiler", Fuel.NaturalGas);
        var radiant = new RadiantFloor(new EntityId("HVAC-RADIANT"), "Radiant", boiler);
        var supply = new SupplyGroup(new SupplySystem[] { air, radiant });
        return new EnergyModel(
            "ASHRAE 140 modified",
            new[] { zone },
            new[] { new ZoneHvacAssignment(zone.Id, supply) });
    }

    internal static Zone CreateZone(string id, string name)
    {
        var material = new Material("Concrete", 1.4, 2200, 880);
        var construction = new OpaqueConstruction(
            "Floor assembly",
            new[] { new Layer("Concrete layer", material, 0.2) });
        var floor = new Surface(
            new EntityId($"{id}-FLOOR"),
            "Floor",
            SurfaceType.Floor,
            construction,
            SurfaceBoundary.Ground,
            new PlanarPolygon(new[]
            {
                new Vertex(0, 0, 0),
                new Vertex(8, 0, 0),
                new Vertex(8, 6, 0),
                new Vertex(0, 6, 0),
            }));
        Schedule heating = Schedule.Constant($"{name} Heating", 20, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant($"{name} Cooling", 27, ScheduleType.Temperature);
        Schedule availability = Schedule.Constant($"{name} HVAC Availability", 1, ScheduleType.OnOff);
        var profile = new ZoneProfile(
            new EntityId($"{id}-PROFILE"),
            $"{name} Profile",
            heating,
            cooling,
            availability);
        return new Zone(new EntityId(id), name, new[] { floor }, profile);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
