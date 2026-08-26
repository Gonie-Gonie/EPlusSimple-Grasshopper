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
        Assert.Equal("90.0", blindMaterial[5]);
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

    [Fact]
    public void DetailedInterzoneOpeningsLinkGeometryMatchedCounterpartsAndRemainFresh()
    {
        var glazing = new Glazing("CTFN-INTERZONE-GLASS", 1.5, 0.42);
        var doorConstruction = new NoMassConstruction("CTFN-INTERZONE-DOOR", 1.2);
        var wallConstruction = TestDomainFactory.WallConstruction("CTFN-INTERZONE-WALL");
        PlanarPolygon host = VerticalRectangle(0, 8, 0, 3);
        PlanarPolygon windowLeft = VerticalRectangle(0.5, 1, 1, 1);
        PlanarPolygon windowRight = VerticalRectangle(2, 1.25, 1, 1);
        PlanarPolygon doorLeft = VerticalRectangle(4, 1, 0.2, 1.8);
        PlanarPolygon doorRight = VerticalRectangle(6, 1, 0.2, 1.8);

        IOpening[] firstOpenings =
        {
            new Window(new EntityId("FNST-A-WINDOW-LEFT"), "FNST-A-WINDOW-LEFT", glazing, windowLeft),
            new Window(new EntityId("FNST-A-WINDOW-RIGHT"), "FNST-A-WINDOW-RIGHT", glazing, windowRight),
            new Door(new EntityId("FNST-A-DOOR-LEFT"), "FNST-A-DOOR-LEFT", doorConstruction, doorLeft),
            new Door(new EntityId("FNST-A-DOOR-RIGHT"), "FNST-A-DOOR-RIGHT", doorConstruction, doorRight),
        };
        IOpening[] secondOpenings =
        {
            new Door(new EntityId("FNST-B-DOOR-RIGHT"), "FNST-B-DOOR-RIGHT", doorConstruction, doorRight.Reverse()),
            new Window(new EntityId("FNST-B-WINDOW-RIGHT"), "FNST-B-WINDOW-RIGHT", glazing, windowRight.Reverse()),
            new Door(new EntityId("FNST-B-DOOR-LEFT"), "FNST-B-DOOR-LEFT", doorConstruction, doorLeft.Reverse()),
            new Window(new EntityId("FNST-B-WINDOW-LEFT"), "FNST-B-WINDOW-LEFT", glazing, windowLeft.Reverse()),
        };
        var firstSurface = new Surface(
            new EntityId("SURF-INTERZONE-A"),
            "SURF-INTERZONE-A",
            SurfaceType.Wall,
            wallConstruction,
            SurfaceBoundary.Outdoors,
            host,
            firstOpenings);
        var secondSurface = new Surface(
            new EntityId("SURF-INTERZONE-B"),
            "SURF-INTERZONE-B",
            SurfaceType.Wall,
            wallConstruction,
            SurfaceBoundary.Outdoors,
            host,
            secondOpenings);
        SurfaceAdjacencyPair pair = SurfaceAdjacency.Match(firstSurface, secondSurface);
        var model = new EnergyModel(
            "Detailed interzone opening parity",
            new[]
            {
                new Zone(
                    new EntityId("ZONE-INTERZONE-A"),
                    "ZONE-INTERZONE-A",
                    new[] { pair.First },
                    TestDomainFactory.EmptyProfile("PROFILE-INTERZONE-A")),
                new Zone(
                    new EntityId("ZONE-INTERZONE-B"),
                    "ZONE-INTERZONE-B",
                    new[] { pair.Second },
                    TestDomainFactory.EmptyProfile("PROFILE-INTERZONE-B")),
            });
        var detailedOptions = new EnergyModelIdfOptions
        {
            AddIdealLoadsForUnassignedZones = false,
        };

        Assert.False(detailedOptions.UseLegacyRectangularFenestration);
        Assert.True(model.Validate().IsValid);
        IdfDocument first = model.ToIdfDocument(options: detailedOptions);
        IdfDocument second = model.ToIdfDocument(options: detailedOptions);

        Assert.NotSame(first, second);
        Assert.Equal(IdfWriter.Write(first), IdfWriter.Write(second));
        Assert.Equal(first.Count, second.Count);
        for (int objectIndex = 0; objectIndex < first.Count; objectIndex++)
        {
            Assert.NotSame(first[objectIndex], second[objectIndex]);
            Assert.Equal(first[objectIndex].Fields.Count, second[objectIndex].Fields.Count);
            for (int fieldIndex = 0; fieldIndex < first[objectIndex].Fields.Count; fieldIndex++)
            {
                Assert.NotSame(
                    first[objectIndex].Fields[fieldIndex],
                    second[objectIndex].Fields[fieldIndex]);
            }
        }

        var counterparts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FNST-A-WINDOW-LEFT"] = "FNST-B-WINDOW-LEFT",
            ["FNST-B-WINDOW-LEFT"] = "FNST-A-WINDOW-LEFT",
            ["FNST-A-WINDOW-RIGHT"] = "FNST-B-WINDOW-RIGHT",
            ["FNST-B-WINDOW-RIGHT"] = "FNST-A-WINDOW-RIGHT",
            ["FNST-A-DOOR-LEFT"] = "FNST-B-DOOR-LEFT",
            ["FNST-B-DOOR-LEFT"] = "FNST-A-DOOR-LEFT",
            ["FNST-A-DOOR-RIGHT"] = "FNST-B-DOOR-RIGHT",
            ["FNST-B-DOOR-RIGHT"] = "FNST-A-DOOR-RIGHT",
        };
        Assert.Equal(counterparts.Count, first["FenestrationSurface:Detailed"].Count);
        foreach ((string openingName, string counterpartName) in counterparts)
        {
            IdfObject firstOpening = first["FenestrationSurface:Detailed"][openingName];
            IdfObject secondOpening = second["FenestrationSurface:Detailed"][openingName];
            Assert.Equal(counterpartName, firstOpening[4]);
            Assert.Equal(counterpartName, secondOpening[4]);
            Assert.NotSame(firstOpening, secondOpening);
        }

        Assert.Equal(
            "SURF-INTERZONE-B",
            first["BuildingSurface:Detailed"]["SURF-INTERZONE-A"][6]);
        Assert.Equal(
            "SURF-INTERZONE-A",
            first["BuildingSurface:Detailed"]["SURF-INTERZONE-B"][6]);
        Assert.Empty(first["Window"]);
        Assert.Empty(first["Window:Interzone"]);
        Assert.Empty(first["Door"]);
        Assert.Empty(first["Door:Interzone"]);
        Assert.True(model.Validate().IsValid);

        IdfDocument legacy = model.ToIdfDocument(options: new EnergyModelIdfOptions
        {
            AddIdealLoadsForUnassignedZones = false,
            UseLegacyRectangularFenestration = true,
        });
        Assert.Empty(legacy["FenestrationSurface:Detailed"]);
        Assert.Equal(4, legacy["Window:Interzone"].Count);
        Assert.Equal(4, legacy["Door:Interzone"].Count);
        foreach ((string openingName, string counterpartName) in counterparts)
        {
            string objectType = openingName.Contains("WINDOW", StringComparison.Ordinal)
                ? "Window:Interzone"
                : "Door:Interzone";
            Assert.Equal(counterpartName, legacy[objectType][openingName][3]);
        }
    }

    [Fact]
    public void DetailedExteriorOpeningsKeepOutsideBoundaryObjectBlank()
    {
        var glazing = new Glazing("CTFN-DETAILED-EXTERIOR-GLASS", 1.5, 0.42);
        IOpening[] openings =
        {
            new Window(
                new EntityId("FNST-DETAILED-EXTERIOR-WINDOW"),
                "FNST-DETAILED-EXTERIOR-WINDOW",
                glazing,
                VerticalRectangle(0.5, 1, 1, 1)),
            new Door(
                new EntityId("FNST-DETAILED-EXTERIOR-DOOR"),
                "FNST-DETAILED-EXTERIOR-DOOR",
                new NoMassConstruction("CTFN-DETAILED-EXTERIOR-DOOR", 1.2),
                VerticalRectangle(2.5, 1, 0.2, 1.8)),
        };
        var surface = new Surface(
            new EntityId("SURF-DETAILED-EXTERIOR"),
            "SURF-DETAILED-EXTERIOR",
            SurfaceType.Wall,
            TestDomainFactory.WallConstruction("CTFN-DETAILED-EXTERIOR-WALL"),
            SurfaceBoundary.Outdoors,
            VerticalRectangle(0, 5, 0, 3),
            openings);
        var model = new EnergyModel(
            "Detailed exterior opening parity",
            new[]
            {
                new Zone(
                    new EntityId("ZONE-DETAILED-EXTERIOR"),
                    "ZONE-DETAILED-EXTERIOR",
                    new[] { surface },
                    TestDomainFactory.EmptyProfile("PROFILE-DETAILED-EXTERIOR")),
            });

        IdfDocument document = model.ToIdfDocument(options: new EnergyModelIdfOptions
        {
            AddIdealLoadsForUnassignedZones = false,
        });

        Assert.Equal(2, document["FenestrationSurface:Detailed"].Count);
        Assert.All(
            document["FenestrationSurface:Detailed"],
            opening => Assert.Equal(string.Empty, opening[4]));
        Assert.Empty(document["Window"]);
        Assert.Empty(document["Window:Interzone"]);
        Assert.Empty(document["Door"]);
        Assert.Empty(document["Door:Interzone"]);
    }

    [Theory]
    [InlineData("PVPN-0x300000", 18d, 0.205d, 180d, 25d, "4.242640687119285")]
    [InlineData("PVPN-0x300001", 9d, 0.18d, 225d, 10d, "3.0")]
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
        Assert.Equal(InvariantText.FormatPythonFloat(azimuth), shade[1]);
        Assert.Equal(InvariantText.FormatPythonFloat(tilt), shade[2]);
        Assert.Equal(expectedSide, shade[6]);
        Assert.Equal(expectedSide, shade[7]);

        IdfObject performance = Object(objects, "PhotovoltaicPerformance:Simple");
        Assert.Equal($"Spec4PVpanel:{name}", performance[0]);
        Assert.Equal("0.7", performance[1]);
        Assert.Equal("Fixed", performance[2]);
        Assert.Equal(
            InvariantText.FormatPythonFloat(efficiency),
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
