using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Grasshopper.Parameters;
using GonieGonie.InvisibleDragon.Grasshopper.Types;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Grasshopper.Tests;

public sealed class DragonGooTests
{
    [Fact]
    public void BasicEnergyModelDuplicateRoundTripsIndependentDomainGraph()
    {
        EnergyModel source = BasicModel();
        var goo = new DragonEnergyModelGoo(source);

        var duplicate = Assert.IsType<DragonEnergyModelGoo>(goo.Duplicate());

        Assert.True(duplicate.IsValid, duplicate.IsValidWhyNot);
        Assert.NotSame(source, duplicate.Value);
        Assert.Equal(source.Name, duplicate.Value.Name);
        Assert.Equal(source.Zones[0].Id, duplicate.Value.Zones[0].Id);
        Assert.Equal(source.Surfaces[0].Polygon.Vertices, duplicate.Value.Surfaces[0].Polygon.Vertices);
        Assert.Equal(source.Zones[0].Profile.HeatingSetpoint, duplicate.Value.Zones[0].Profile.HeatingSetpoint);
    }

    [Fact]
    public void IdfDuplicatePreservesCanonicalText()
    {
        var document = new IdfDocument(objects: new[]
        {
            new IdfObject("Version", new List<string?> { "24.2" }),
            new IdfObject("Building", new List<string?> { "Dragon Test" }),
        });

        var duplicate = Assert.IsType<DragonIdfGoo>(new DragonIdfGoo(document).Duplicate());

        Assert.NotSame(document, duplicate.Value);
        Assert.Equal(IdfWriter.Write(document), IdfWriter.Write(duplicate.Value));
    }

    [Fact]
    public void GooCastsExposeDomainAndScriptValues()
    {
        var material = new Material("Brick", 0.72, 1920, 840);
        var goo = new DragonMaterialGoo();

        Assert.True(goo.CastFrom(material));
        Material? cast = null;
        Assert.True(goo.CastTo(ref cast));
        Assert.Same(material, cast);
        Assert.Same(material, goo.ScriptVariable());
        Assert.Contains("Brick", goo.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PublicParameterGuidsAreUniqueAndStable()
    {
        Guid[] identifiers =
        {
            new DragonMaterialParam().ComponentGuid,
            new DragonLayerParam().ComponentGuid,
            new DragonConstructionParam().ComponentGuid,
            new DragonScheduleParam().ComponentGuid,
            new DragonProfileParam().ComponentGuid,
            new DragonSurfaceParam().ComponentGuid,
            new DragonGlazingParam().ComponentGuid,
            new DragonOpeningParam().ComponentGuid,
            new DragonZoneDefinitionParam().ComponentGuid,
            new DragonEnergyModelParam().ComponentGuid,
            new DragonSourceSystemParam().ComponentGuid,
            new DragonSupplySystemParam().ComponentGuid,
            new DragonEnergyRecoveryVentilatorParam().ComponentGuid,
            new DragonPhotovoltaicPanelParam().ComponentGuid,
            new DragonIdfParam().ComponentGuid,
            new EnergyPlusResultParam().ComponentGuid,
            new DiagnosticParam().ComponentGuid,
            new PreparedWeatherFileParam().ComponentGuid,
        };

        Assert.Equal(identifiers.Length, identifiers.Distinct().Count());
        Assert.Contains(new Guid("dbfba1b5-624a-4db4-8fec-d80eb9561467"), identifiers);
        Assert.Contains(new Guid("d7597f76-1486-45b7-bcc6-7e8f5fb23738"), identifiers);
        Assert.Contains(new Guid("c6afcc1f-f11e-4a54-a84a-0e845a828d5d"), identifiers);
        Assert.Contains(new Guid("bc8c67a8-e853-4eec-a576-acdeedbe371b"), identifiers);
        Assert.Contains(new Guid("26ef6130-77e3-4c6d-a802-9460bcc386ed"), identifiers);
        Assert.Contains(new Guid("9571341c-3795-417d-9908-5833d234d815"), identifiers);
    }

    [Fact]
    public void ErrorDiagnosticIsValidGooData()
    {
        var diagnostic = new Diagnostic("TEST.ERROR", DiagnosticSeverity.Error, "Expected validation error.");
        var goo = new DiagnosticGoo(diagnostic);

        Assert.True(goo.IsValid);
        Assert.Equal(string.Empty, goo.IsValidWhyNot);
    }

    private static EnergyModel BasicModel()
    {
        var profile = new ZoneProfile(
            new EntityId("profile-basic"),
            "Basic",
            Schedule.Constant("Heating", 20, ScheduleType.Temperature),
            Schedule.Constant("Cooling", 26, ScheduleType.Temperature));
        var polygon = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(5, 0, 0),
            new Vertex(5, 4, 0),
            new Vertex(0, 4, 0),
        });
        var surface = new DragonSurface(
            new EntityId("surface-floor"),
            "Floor",
            SurfaceType.Floor,
            new NoMassConstruction("Floor Construction", 0.3),
            SurfaceBoundary.Ground,
            polygon);
        var zone = new Zone(new EntityId("zone-basic"), "Basic Zone", new[] { surface }, profile);
        return new EnergyModel("Basic Model", new[] { zone });
    }
}
