using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class SourceSystemTests
{
    [Fact]
    public void HeatPumpExportsUpstreamVrfPerformanceCurveMatrix()
    {
        var heatPump = new HeatPump(new EntityId("HVAC-HP-1"), "Main", Fuel.Electricity, 3.2, 3.0);

        IReadOnlyList<IdfObject> objects = heatPump.ToIdfObjects(
            new IdfGenerationContext(),
            terminalUnitNames: new[] { "Terminal A" });

        Assert.Equal(8, objects.Count(item => item.ObjectType == "Curve:Biquadratic"));
        Assert.Equal(6, objects.Count(item => item.ObjectType == "Curve:Cubic"));
        Assert.Equal(5, objects.Count(item => item.ObjectType == "Curve:Linear"));
        Assert.Single(objects, item => item.ObjectType == "Curve:Quadratic");
        Assert.Equal("HeatPump_named_Main", Assert.Single(objects, item => item.ObjectType == heatPump.IdfObjectType).Name);
        IdfObject terminals = Assert.Single(objects, item => item.ObjectType == "ZoneTerminalUnitList");
        Assert.Equal("Terminal A", terminals[1]);
        IdfObject coolingLowPartLoad = Assert.Single(
            objects,
            item => item.ObjectType == "Curve:Cubic"
                && item.Name == "Curve_for_HeatPump_named_Main:CoolingEIRMF_LowPLR");
        Assert.Equal("Capacity", coolingLowPartLoad[10]);
        IdfObject heatingBoundary = Assert.Single(
            objects,
            item => item.ObjectType == "Curve:Cubic"
                && item.Name == "Curve_for_HeatPump_named_Main:HeatingEIRBoundary");
        Assert.Equal("-20.0", heatingBoundary[7]);
        Assert.Equal("15.0", heatingBoundary[8]);
        IdfObject heatingLowPartLoad = Assert.Single(
            objects,
            item => item.ObjectType == "Curve:Cubic"
                && item.Name == "Curve_for_HeatPump_named_Main:HeatingEIRMF_LowPLR");
        Assert.Equal("Dimensionless", heatingLowPartLoad[9]);
    }

    [Fact]
    public void HeatPumpOmitsHeatingCopOnlyForLegacyCoolingOnlyPackagedTerminals()
    {
        var heatPump = new HeatPump(
            new EntityId("HVAC-HP-PACKAGED"),
            "Packaged source",
            Fuel.Electricity,
            3.2,
            3.6,
            heatingCapacityWatts: 0.001,
            coolingCapacityWatts: 12_000);
        string packagedTerminal =
            "PackagedAirConditioner_named_SUPPLY-PACKAGED_for_ZONE-PACKAGED";
        var legacyOptions = new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonHvacTopology = true,
        };

        IdfObject native = Assert.Single(
            heatPump.ToIdfObjects(
                new IdfGenerationContext(),
                terminalUnitNames: new[] { packagedTerminal }),
            item => item.ObjectType == heatPump.IdfObjectType);
        IdfObject legacyPackaged = Assert.Single(
            heatPump.ToIdfObjects(
                new IdfGenerationContext(options: legacyOptions),
                terminalUnitNames: new[] { packagedTerminal }),
            item => item.ObjectType == heatPump.IdfObjectType);
        IdfObject legacyMixed = Assert.Single(
            heatPump.ToIdfObjects(
                new IdfGenerationContext(options: legacyOptions),
                terminalUnitNames: new[]
                {
                    packagedTerminal,
                    "AirHandlingUnit_named_SUPPLY-AHU_for_ZONE-AHU",
                }),
            item => item.ObjectType == heatPump.IdfObjectType);

        Assert.Equal("3.2", native[20]);
        Assert.Equal(string.Empty, legacyPackaged[20]);
        Assert.Equal("3.2", legacyMixed[20]);
    }

    [Fact]
    public void BoilerExportsClosedPlantLoopAndDemandBranch()
    {
        var boiler = new Boiler(new EntityId("HVAC-BOILER-1"), "Boiler", Fuel.NaturalGas);
        var demand = new PlantDemandConnection(
            "Radiant demand",
            "ZoneHVAC:LowTemperatureRadiant:VariableFlow",
            "Radiant",
            "Radiant inlet",
            "Radiant outlet");

        IReadOnlyList<IdfObject> objects = boiler.ToIdfObjects(new IdfGenerationContext(), new[] { demand });

        IdfObject boilerObject = Assert.Single(objects, item => item.ObjectType == "Boiler:HotWater");
        Assert.Equal("LeavingBoiler", boilerObject[4]);
        Assert.Equal("autosize", boilerObject[6]);
        Assert.Equal("1", boilerObject[8]);
        Assert.Equal("99.9", boilerObject[12]);
        Assert.Equal("NotModulated", boilerObject[13]);
        Assert.Equal("General", boilerObject[16]);
        IdfObject plantLoop = Assert.Single(objects, item => item.ObjectType == "PlantLoop");
        Assert.Equal("99.9", plantLoop[5]);
        Assert.Equal("SequentialLoad", plantLoop[18]);
        Assert.Equal($"{boiler.LoopName} AvailabilityManagerAssignmentList", plantLoop[19]);
        Assert.Equal("2", plantLoop[23]);
        IdfObject sizing = Assert.Single(objects, item => item.ObjectType == "Sizing:Plant");
        Assert.Equal("80", sizing[2]);
        Assert.Equal("10", sizing[3]);
        Assert.Equal("NonCoincident", sizing[4]);
        Assert.Equal("1", sizing[5]);
        IdfObject pump = Assert.Single(objects, item => item.ObjectType == "Pump:VariableSpeed");
        Assert.Equal("179352", pump[4]);
        Assert.Equal("Continuous", pump[13]);
        Assert.Equal("PowerPerFlowPerPressure", pump[25]);
        Assert.Equal("348701.1", pump[26]);
        Assert.Equal("1.282051282", pump[27]);
        Assert.Equal("General", pump[29]);
        IdfObject setpoint = Assert.Single(
            objects,
            item => item.ObjectType == "Schedule:Constant"
                && item.Name == $"{boiler.LoopName} SetpointTemperature");
        Assert.Equal(string.Empty, setpoint[1]);
        Assert.Equal(5, objects.Count(item => item.ObjectType == "Pipe:Adiabatic"));
        Assert.Equal(8, objects.Count(item => item.ObjectType == "Branch"));
        Assert.Equal(2, objects.Count(item => item.ObjectType == "BranchList"));
        Assert.Equal(2, objects.Count(item => item.ObjectType == "Connector:Splitter"));
        Assert.Equal(2, objects.Count(item => item.ObjectType == "Connector:Mixer"));
        Assert.Contains(objects, item => item.ObjectType == "Branch" && item.Name == "Radiant demand");
    }

    [Fact]
    public void DistrictHeatingUsesSameExtensiblePlantDemandTopology()
    {
        var district = new DistrictHeating(new EntityId("HVAC-DISTRICT-1"), "District");

        IReadOnlyList<IdfObject> objects = district.ToIdfObjects(new IdfGenerationContext());

        Assert.Single(objects, item => item.ObjectType == "DistrictHeating:Water");
        Assert.Single(objects, item => item.ObjectType == "PlantLoop");
        Assert.Equal(7, objects.Count(item => item.ObjectType == "Branch"));
    }

    [Fact]
    public void PhotovoltaicPanelExportsPanelGeneratorInverterAndDistribution()
    {
        var panel = new PhotovoltaicPanel(new EntityId("PV-1"), "Roof array", 20, 30, 180, 0.2);

        IReadOnlyList<IdfObject> objects = panel.ToIdfObjects(new IdfGenerationContext());

        Assert.Equal(6, objects.Count);
        Assert.Contains(objects, item => item.ObjectType == "Shading:Site");
        Assert.Contains(objects, item => item.ObjectType == "Generator:Photovoltaic");
        Assert.Contains(objects, item => item.ObjectType == "ElectricLoadCenter:Distribution");
    }
}
