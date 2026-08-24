using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class GeothermalHeatPumpTests
{
    [Fact]
    public void GeothermalIdentityUsesPinnedRegularHeatPumpCompatibilityPath()
    {
        var geothermal = new GeothermalHeatPump(
            new EntityId("GEOTHERMAL-IDENTITY"),
            "Ground source",
            Fuel.Electricity,
            4.2,
            4.8,
            12_000,
            10_000);
        var regular = new HeatPump(
            geothermal.Id,
            geothermal.Name,
            geothermal.Fuel,
            geothermal.HeatingCoefficientOfPerformance,
            geothermal.CoolingCoefficientOfPerformance,
            geothermal.HeatingCapacityWatts,
            geothermal.CoolingCapacityWatts);

        Assert.IsType<GeothermalHeatPump>(geothermal);
        Assert.IsAssignableFrom<HeatPump>(geothermal);
        Assert.Equal("AirConditioner:VariableRefrigerantFlow", geothermal.IdfObjectType);
        Assert.Equal("HeatPump_named_Ground source", geothermal.IdfObjectName);
        Assert.Equal(
            Serialize(regular.ToIdfObjects(new IdfGenerationContext())),
            Serialize(geothermal.ToIdfObjects(new IdfGenerationContext())));
    }

    private static string Serialize(IEnumerable<IdfObject> objects)
    {
        return IdfWriter.Write(new IdfDocument(objects: objects));
    }
}
