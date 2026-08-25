using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class OpeningAndPhotovoltaicParityTests
{
    [Fact]
    public void LegacyOpeningsExportGlassDoorOpaqueDoorAndPinnedInteriorShading()
    {
        var glazing = new Glazing("CTFN-TRANSPARENT", 1.5, 0.42);
        var shade = new Shade("default_shade", 0.5, 0.4);
        var blind = new Blind("default_blind", 0.05, 0.05, 90, 0.5, 0.5);
        IOpening[] openings =
        {
            new Window(
                new EntityId("FNST-SHADE"),
                "FNST-SHADE",
                glazing,
                VerticalRectangle(0.5, 0.75, 0.5, 1.5),
                shade),
            new Window(
                new EntityId("FNST-BLIND"),
                "FNST-BLIND",
                glazing,
                VerticalRectangle(1.75, 0.75, 0.5, 1.5),
                blind),
            new Window(
                new EntityId("FNST-GLASS-DOOR"),
                "FNST-GLASS-DOOR",
                glazing,
                VerticalRectangle(3, 0.75, 0.5, 1.5)),
            new Door(
                new EntityId("FNST-OPAQUE-DOOR"),
                "FNST-OPAQUE-DOOR",
                new NoMassConstruction("CTFN-OPAQUE", 1.2),
                VerticalRectangle(4.25, 0.75, 0.5, 1.5)),
        };
        var wall = new Surface(
            new EntityId("SURF-OPENINGS"),
            "SURF-OPENINGS",
            SurfaceType.Wall,
            TestDomainFactory.WallConstruction(),
            SurfaceBoundary.Outdoors,
            VerticalRectangle(0, 6, 0, 3),
            openings);
        var zone = new Zone(
            new EntityId("ZONE-OPENINGS"),
            "ZONE-OPENINGS",
            new[] { wall },
            TestDomainFactory.EmptyProfile());
        var model = new EnergyModel("Opening parity", new[] { zone });

        IdfDocument document = model.ToIdfDocument(options: new EnergyModelIdfOptions
        {
            AddIdealLoadsForUnassignedZones = false,
            UseLegacyRectangularFenestration = true,
        });

        Assert.Equal(3, document["Window"].Count);
        Assert.Equal("CTFN-TRANSPARENT", document["Window"]["FNST-GLASS-DOOR"][1]);
        Assert.Single(document["Door"]);
        Assert.Equal("CTFN-OPAQUE", document["Door"]["FNST-OPAQUE-DOOR"][1]);
        IdfObject noMass = document["Material:NoMass"]["$MaterialFor$_CTFN-OPAQUE"];
        Assert.Equal("0.8333333333333334", noMass[2]);
        Assert.Equal(
            "$MaterialFor$_CTFN-OPAQUE",
            document["Construction"]["CTFN-OPAQUE"][1]);

        IdfObject shadeMaterial = Assert.Single(document["WindowMaterial:Shade"]);
        Assert.Equal(
            new[]
            {
                "default_shade", "0.5", "0.4", "0.5", "0.4",
                "0.09999999999999998", "0.5", "0.01", "100", "0.05",
                "0.5", "0.5", "0.5", "0.5", "0",
            },
            shadeMaterial.Fields.Select(field => field.Value));

        IdfObject blindMaterial = Assert.Single(document["WindowMaterial:Blind"]);
        Assert.Equal("default_blind", blindMaterial[0]);
        Assert.Equal("Horizontal", blindMaterial[1]);
        Assert.Equal("0.05", blindMaterial[2]);
        Assert.Equal("0.05", blindMaterial[3]);
        Assert.Equal("0.00025", blindMaterial[4]);
        Assert.Equal("90", blindMaterial[5]);
        Assert.Equal("221", blindMaterial[6]);
        Assert.Equal("0.5", blindMaterial[8]);
        Assert.Equal("0.5", blindMaterial[9]);
        Assert.Equal("0.5", blindMaterial[11]);
        Assert.Equal("0.5", blindMaterial[12]);
        Assert.Equal(string.Empty, blindMaterial[14]);
        Assert.Equal(string.Empty, blindMaterial[15]);
        Assert.Equal("0.9", blindMaterial[20]);
        Assert.Equal("0.9", blindMaterial[21]);
        Assert.Equal("180", blindMaterial[28]);

        AssertShadingControl(
            document["WindowShadingControl"]["FNST-SHADE:ShadingControl"],
            "InteriorShade",
            "default_shade",
            "FNST-SHADE");
        AssertShadingControl(
            document["WindowShadingControl"]["FNST-BLIND:ShadingControl"],
            "InteriorBlind",
            "default_blind",
            "FNST-BLIND");
        Assert.Equal(2, document["WindowShadingControl"].Count);
    }

    [Theory]
    [InlineData("PVPN-0x300000", 18d, 0.205d, 180d, 25d, "4.242640687119285")]
    [InlineData("PVPN-0x300001", 9d, 0.18d, 225d, 10d, "3")]
    public void PhotovoltaicExportsPinnedExteriorShadeAndLoadCenterFields(
        string name,
        double area,
        double efficiency,
        double azimuth,
        double tilt,
        string expectedSide)
    {
        var panel = new PhotovoltaicPanel(
            new EntityId(name),
            name,
            area,
            tilt,
            azimuth,
            efficiency);

        IReadOnlyList<IdfObject> objects = panel.ToIdfObjects(new IdfGenerationContext());

        IdfObject shade = Object(objects, "Shading:Site");
        Assert.Equal($"Shading4PVpanel:{name}", shade[0]);
        Assert.Equal(azimuth.ToString("R", System.Globalization.CultureInfo.InvariantCulture), shade[1]);
        Assert.Equal(tilt.ToString("R", System.Globalization.CultureInfo.InvariantCulture), shade[2]);
        Assert.Equal(expectedSide, shade[6]);
        Assert.Equal(expectedSide, shade[7]);

        IdfObject performance = Object(objects, "PhotovoltaicPerformance:Simple");
        Assert.Equal($"Spec4PVpanel:{name}", performance[0]);
        Assert.Equal("0.7", performance[1]);
        Assert.Equal("Fixed", performance[2]);
        Assert.Equal(
            efficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            performance[3]);

        IdfObject generator = Object(objects, "Generator:Photovoltaic");
        Assert.Equal(new[]
        {
            $"PVpanel:{name}",
            $"Shading4PVpanel:{name}",
            "PhotovoltaicPerformance:Simple",
            $"Spec4PVpanel:{name}",
        }, generator.Fields.Select(field => field.Value));

        IdfObject generators = Object(objects, "ElectricLoadCenter:Generators");
        Assert.Equal(new[]
        {
            $"Generator4PVpanel:{name}",
            $"PVpanel:{name}",
            "Generator:Photovoltaic",
            "1000000",
        }, generators.Fields.Select(field => field.Value));
        Assert.Single(objects, item => item.ObjectType == "ElectricLoadCenter:Inverter:Simple");
        Assert.Single(objects, item => item.ObjectType == "ElectricLoadCenter:Distribution");
    }

    private static IdfObject Object(IEnumerable<IdfObject> objects, string objectType) =>
        Assert.Single(objects, item => item.ObjectType == objectType);

    private static PlanarPolygon VerticalRectangle(
        double x,
        double width,
        double z,
        double height) => new(new[]
        {
            new Vertex(x, 0, z),
            new Vertex(x + width, 0, z),
            new Vertex(x + width, 0, z + height),
            new Vertex(x, 0, z + height),
        });

    private static void AssertShadingControl(
        IdfObject control,
        string shadingType,
        string materialName,
        string windowName)
    {
        Assert.Equal("ZONE-OPENINGS", control[1]);
        Assert.Equal("1", control[2]);
        Assert.Equal(shadingType, control[3]);
        Assert.Equal("OffNightAndOnDayIfCoolingAndHighSolarOnWindow", control[5]);
        Assert.Equal("20", control[7]);
        Assert.Equal("No", control[8]);
        Assert.Equal("No", control[9]);
        Assert.Equal(materialName, control[10]);
        Assert.Equal("FixedSlatAngle", control[11]);
        Assert.Equal("Sequential", control[15]);
        Assert.Equal(windowName, control[16]);
    }
}
