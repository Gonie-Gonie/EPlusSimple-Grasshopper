namespace Dragons.SimpleDragon.Tests;

public sealed class ShapeAndSystemsTests
{
    [Fact]
    public void AreaAzimuthAndIdsAreRhinoFreeAndDeterministic()
    {
        var glazing = new FenestrationConstruction("clear glazing", 2.1d, 0.7d);
        var opening = new Fenestration(
            "south window",
            FenestrationType.Window,
            8d,
            glazing.Id.Value,
            glazing,
            BlindType.Shade);
        var repeatedOpening = new Fenestration(
            "south window",
            FenestrationType.Window,
            8d,
            glazing.Id.Value,
            glazing,
            BlindType.Shade);
        var wall = new Surface(
            "south wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            24d,
            180d,
            null,
            null,
            new[] { opening });
        var repeatedWall = new Surface(
            "south wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            24d,
            180d,
            null,
            null,
            new[] { repeatedOpening });
        var floor = new Surface(
            "floor",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            40d,
            null,
            null,
            null);
        var zone = new Zone("zone", 1, 2.7d, new[] { floor, wall }, "office", null, 10d);

        Assert.Equal(opening.Id, repeatedOpening.Id);
        Assert.Equal(wall.Id, repeatedWall.Id);
        Assert.Equal(24d, wall.Area);
        Assert.Equal(180d, wall.Azimuth);
        Assert.Equal(40d, zone.Area);
        Assert.Equal(1.5d, zone.Infiltration);
        Assert.Equal(2, zone.Surfaces.Count);

        Surface flipped = wall.Flip();
        Assert.Equal(SurfaceType.Wall, flipped.Type);
        Assert.Equal(0d, flipped.Azimuth);
        Assert.Equal(wall.Area, flipped.Area);
        Assert.Single(flipped.Fenestrations);
    }

    [Fact]
    public void SurfaceEnforcesAreaAzimuthBoundaryAndOpeningInvariants()
    {
        var opaque = new FenestrationConstruction("opaque door", 2d);
        var door = new Fenestration("door", FenestrationType.Door, 2d, opaque.Id.Value, opaque);

        Assert.Throws<ArgumentException>(() => new Surface(
            "wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            10d,
            null,
            null,
            null));
        Assert.Throws<ArgumentException>(() => new Surface(
            "floor",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            10d,
            0d,
            null,
            null));
        Assert.Throws<ArgumentException>(() => new Surface(
            "ground",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            10d,
            null,
            null,
            null,
            new[] { door }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(
            "wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            10d,
            360d,
            null,
            null));
    }

    [Fact]
    public void EveryGrmSystemTypeHasAValidatedRhinoFreeRepresentation()
    {
        var heatPump = new SourceSystem(
            "heat pump",
            SourceSystemType.HeatPump,
            FuelType.Electricity,
            heatingCop: 3d,
            coolingCop: 3d);
        var geothermal = new SourceSystem(
            "ground heat pump",
            SourceSystemType.GeothermalHeatPump,
            FuelType.Electricity,
            heatingCop: 4d,
            coolingCop: 4.5d);
        var chiller = new SourceSystem(
            "chiller",
            SourceSystemType.Chiller,
            coolingCop: 3d,
            compressorType: CompressorType.Screw,
            coolingTowerType: CoolingTowerType.Open,
            coolingTowerControl: CoolingTowerControl.TwoSpeed);
        var absorption = new SourceSystem(
            "absorption",
            SourceSystemType.AbsorptionChiller,
            FuelType.NaturalGas,
            coolingCop: 0.9d,
            boilerEfficiency: 0.85d);
        var boiler = new SourceSystem(
            "boiler",
            SourceSystemType.Boiler,
            FuelType.NaturalGas,
            efficiency: 0.85d,
            hotWaterSupply: true);
        var district = new SourceSystem(
            "district",
            SourceSystemType.DistrictHeating,
            hotWaterSupply: false);

        SourceSystem[] sources = { heatPump, geothermal, chiller, absorption, boiler, district };
        Assert.Equal(Enum.GetValues<SourceSystemType>().Length, sources.Length);
        Assert.Equal(sources.Length, sources.Select(source => source.Id).Distinct().Count());

        SupplySystem[] supplies =
        {
            new("packaged", SupplySystemType.PackagedAirConditioner, coolingCop: 3d),
            new("ahu", SupplySystemType.AirHandlingUnit, heatPump.Id.Value, heatPump),
            new("fan coil", SupplySystemType.FanCoilUnit, chiller.Id.Value, chiller),
            new("radiator", SupplySystemType.Radiator, boiler.Id.Value, boiler),
            new("electric radiator", SupplySystemType.ElectricRadiator, heatingCapacity: 5000d),
            new("radiant floor", SupplySystemType.RadiantFloor, district.Id.Value, district),
            new("electric radiant floor", SupplySystemType.ElectricRadiantFloor),
        };
        Assert.Equal(Enum.GetValues<SupplySystemType>().Length, supplies.Length);
        Assert.True(supplies[0].Coolable);
        Assert.True(supplies[1].Heatable);
        Assert.True(supplies[1].Coolable);
        Assert.True(supplies[2].Coolable);
        Assert.False(supplies[2].Heatable);
        Assert.True(supplies[3].Heatable);
        Assert.False(supplies[3].Coolable);
        Assert.True(supplies[4].Heatable);
        Assert.True(supplies[5].Heatable);
        Assert.True(supplies[6].Heatable);
        Assert.Throws<ArgumentException>(() => new SupplySystem(
            "invalid ahu",
            SupplySystemType.AirHandlingUnit,
            boiler.Id.Value,
            boiler));

        var ventilation = new VentilationSystem("erv", 0.4d);
        var photovoltaic = new PhotovoltaicSystem("roof pv", 20d, 0.2d, 180d, 30d);
        Assert.Equal(0.7d, ventilation.HeatingEfficiency);
        Assert.Equal(0.45d, ventilation.CoolingEfficiency);
        Assert.Equal(4d, photovoltaic.Area * photovoltaic.Efficiency);
    }
}
