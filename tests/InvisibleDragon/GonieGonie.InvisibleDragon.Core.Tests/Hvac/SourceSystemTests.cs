using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

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

        Assert.Single(objects, item => item.ObjectType == "Boiler:HotWater");
        Assert.Single(objects, item => item.ObjectType == "PlantLoop");
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
