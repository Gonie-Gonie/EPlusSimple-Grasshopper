using GH_IO.Serialization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class DirectAuthoringGooTests
{
    [Fact]
    public void GlazingGooDuplicatesAndArchivesIndependentValue()
    {
        var source = new Glazing("Direct Glazing", 1.37d, 0.41d);
        var goo = new DragonGlazingGoo(source);

        var duplicate = Assert.IsType<DragonGlazingGoo>(goo.Duplicate());
        DragonGlazingGoo archived = ArchiveRoundTrip(goo, new DragonGlazingGoo());

        AssertGlazing(source, duplicate.Value);
        AssertGlazing(source, archived.Value);
        Assert.NotSame(source, duplicate.Value);
        Assert.NotSame(source, archived.Value);
    }

    [Fact]
    public void OpeningGooDuplicatesAndArchivesWindowsAndDoors()
    {
        IOpening[] openings =
        {
            new Window(
                new EntityId("DIRECT-WINDOW"),
                "Direct Window",
                new Glazing("Window Glazing", 1.42d, 0.39d),
                Rectangle(0d, 0d, 1.8d, 1.2d),
                new Shade("Window Shade", 0.2d, 0.5d)),
            new Door(
                new EntityId("DIRECT-DOOR"),
                "Direct Door",
                new NoMassConstruction("Door Construction", 0.58d),
                Rectangle(2d, 0d, 1d, 2.1d)),
        };

        foreach (IOpening source in openings)
        {
            var goo = new DragonOpeningGoo(source);
            var duplicate = Assert.IsType<DragonOpeningGoo>(goo.Duplicate());
            DragonOpeningGoo archived = ArchiveRoundTrip(goo, new DragonOpeningGoo());

            AssertOpening(source, duplicate.Value);
            AssertOpening(source, archived.Value);
            Assert.NotSame(source, duplicate.Value);
            Assert.NotSame(source, archived.Value);
        }
    }

    [Fact]
    public void ZoneDefinitionGooDuplicatesAndArchivesOwnedSystems()
    {
        Zone zone = ValidZone();
        SupplySystem supply = Supply("DIRECT-SUPPLY", "Direct Radiator");
        EnergyRecoveryVentilator ventilator = Ventilator(
            "DIRECT-ERV",
            "Direct ERV");
        var source = new InvisibleDragonZoneDefinition(
            zone,
            new[] { supply },
            new[] { ventilator });
        var goo = new DragonZoneDefinitionGoo(source);

        var duplicate = Assert.IsType<DragonZoneDefinitionGoo>(goo.Duplicate());
        DragonZoneDefinitionGoo archived = ArchiveRoundTrip(
            goo,
            new DragonZoneDefinitionGoo());

        AssertDefinition(source, duplicate.Value);
        AssertDefinition(source, archived.Value);
        Assert.NotSame(source, duplicate.Value);
        Assert.NotSame(source, archived.Value);
    }

    [Fact]
    public void ZoneDefinitionCopiesInputsAndExposesReadOnlyOwnedCollections()
    {
        Zone zone = ValidZone();
        SupplySystem supply = Supply("COPY-SUPPLY", "Copy Supply");
        EnergyRecoveryVentilator ventilator = Ventilator("COPY-ERV", "Copy ERV");
        var supplies = new List<SupplySystem> { supply };
        var ventilators = new List<EnergyRecoveryVentilator> { ventilator };

        var definition = new InvisibleDragonZoneDefinition(
            zone,
            supplies,
            ventilators);
        supplies.Clear();
        ventilators.Clear();

        Assert.NotSame(zone, definition.Zone);
        Assert.Single(definition.SupplySystems);
        Assert.Single(definition.Ventilators);
        Assert.NotSame(supply, definition.SupplySystems[0]);
        Assert.NotSame(ventilator, definition.Ventilators[0]);

        var writableSupplies = Assert.IsAssignableFrom<IList<SupplySystem>>(
            definition.SupplySystems);
        var writableVentilators = Assert.IsAssignableFrom<IList<EnergyRecoveryVentilator>>(
            definition.Ventilators);
        Assert.Throws<NotSupportedException>(() => writableSupplies.Add(supply));
        Assert.Throws<NotSupportedException>(() => writableVentilators.Add(ventilator));
    }

    [Fact]
    public void ZoneDefinitionRejectsNullItemsAndDuplicateIdentifierErrors()
    {
        Zone zone = ValidZone();
        SupplySystem supply = Supply("VALIDATION-SUPPLY", "Validation Supply");
        EnergyRecoveryVentilator ventilator = Ventilator(
            "VALIDATION-ERV",
            "Validation ERV");

        Assert.Equal(
            "zone",
            Assert.Throws<ArgumentNullException>(() =>
                new InvisibleDragonZoneDefinition(null!)).ParamName);
        Assert.Equal(
            "supplySystems",
            Assert.Throws<ArgumentException>(() =>
                new InvisibleDragonZoneDefinition(
                    zone,
                    new SupplySystem[] { null! })).ParamName);
        Assert.Equal(
            "ventilators",
            Assert.Throws<ArgumentException>(() =>
                new InvisibleDragonZoneDefinition(
                    zone,
                    ventilators: new EnergyRecoveryVentilator[] { null! })).ParamName);
        Assert.Equal(
            "supplySystems",
            Assert.Throws<ArgumentException>(() =>
                new InvisibleDragonZoneDefinition(
                    zone,
                    new[]
                    {
                        supply,
                        Supply(supply.Id.Value, "Duplicate Supply"),
                    })).ParamName);
        Assert.Equal(
            "ventilators",
            Assert.Throws<ArgumentException>(() =>
                new InvisibleDragonZoneDefinition(
                    zone,
                    ventilators: new[]
                    {
                        ventilator,
                        Ventilator(ventilator.Id.Value, "Duplicate ERV"),
                    })).ParamName);
    }

    [Fact]
    public void DirectAuthoringParamsHaveStableGuids()
    {
        Assert.Equal(
            new Guid("a22adfb7-d7cb-4b8a-bda5-13d07626f405"),
            new DragonGlazingParam().ComponentGuid);
        Assert.Equal(
            new Guid("d12ac46e-0e15-4d41-8643-5771940991f7"),
            new DragonOpeningParam().ComponentGuid);
        Assert.Equal(
            new Guid("999ffa44-88d8-4652-a767-c662f877b709"),
            new DragonZoneDefinitionParam().ComponentGuid);
    }

    private static Zone ValidZone()
    {
        var profile = new ZoneProfile(
            new EntityId("DIRECT-PROFILE"),
            "Direct Profile",
            Schedule.Constant("Heating", 20d, ScheduleType.Temperature),
            Schedule.Constant("Cooling", 26d, ScheduleType.Temperature));
        var floor = new DragonSurface(
            new EntityId("DIRECT-FLOOR"),
            "Direct Floor",
            SurfaceType.Floor,
            new NoMassConstruction("Floor Construction", 0.31d),
            SurfaceBoundary.Ground,
            Rectangle(0d, 0d, 5d, 4d));
        return new Zone(
            new EntityId("DIRECT-ZONE"),
            "Direct Zone",
            new[] { floor },
            profile,
            0.25d,
            8.5d,
            0.1d);
    }

    private static ElectricRadiator Supply(string id, string name)
    {
        return new ElectricRadiator(new EntityId(id), name, 6_500d, 0.98d, 0.15d);
    }

    private static EnergyRecoveryVentilator Ventilator(string id, string name)
    {
        return new EnergyRecoveryVentilator(
            new EntityId(id),
            name,
            0.72d,
            0.63d,
            0.24d,
            0.76d,
            112d);
    }

    private static PlanarPolygon Rectangle(
        double x,
        double y,
        double width,
        double height)
    {
        return new PlanarPolygon(new[]
        {
            new Vertex(x, y, 0d),
            new Vertex(x + width, y, 0d),
            new Vertex(x + width, y + height, 0d),
            new Vertex(x, y + height, 0d),
        });
    }

    private static void AssertGlazing(Glazing expected, Glazing actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(
            expected.UValueWattsPerSquareMetreKelvin,
            actual.UValueWattsPerSquareMetreKelvin);
        Assert.Equal(
            expected.SolarHeatGainCoefficient,
            actual.SolarHeatGainCoefficient);
    }

    private static void AssertOpening(IOpening expected, IOpening actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Polygon.Vertices, actual.Polygon.Vertices);
        Assert.Equal(expected.Provenance, actual.Provenance);
        if (expected is Window expectedWindow)
        {
            Window actualWindow = Assert.IsType<Window>(actual);
            AssertGlazing(expectedWindow.Glazing, actualWindow.Glazing);
            Assert.Equal(expectedWindow.Shading, actualWindow.Shading);
        }
        else
        {
            Door expectedDoor = Assert.IsType<Door>(expected);
            Door actualDoor = Assert.IsType<Door>(actual);
            Assert.Equal(expectedDoor.Construction, actualDoor.Construction);
        }
    }

    private static void AssertDefinition(
        InvisibleDragonZoneDefinition expected,
        InvisibleDragonZoneDefinition actual)
    {
        Assert.NotSame(expected.Zone, actual.Zone);
        Assert.Equal(expected.Zone, actual.Zone);
        Assert.Equal(expected.SupplySystems.Count, actual.SupplySystems.Count);
        Assert.Equal(expected.Ventilators.Count, actual.Ventilators.Count);
        Assert.NotSame(expected.SupplySystems[0], actual.SupplySystems[0]);
        Assert.Equal(expected.SupplySystems[0].Id, actual.SupplySystems[0].Id);
        Assert.NotSame(expected.Ventilators[0], actual.Ventilators[0]);
        Assert.Equal(expected.Ventilators[0].Id, actual.Ventilators[0].Id);
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
