using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Tests;

public sealed class SurfaceOpeningTests
{
    [Fact]
    public void SurfacePreservesExactHostAndOpeningPolygons()
    {
        PlanarPolygon host = TestDomainFactory.Square(4);
        PlanarPolygon openingPolygon = TestDomainFactory.Square(1, x: 1, y: 1);
        var window = new Window(
            new EntityId("FNST-000001"),
            "Window",
            new Glazing("Glass", 1.2, 0.4),
            openingPolygon);
        Surface surface = TestDomainFactory.Surface(
            "SURF-000001",
            "Wall",
            host,
            openings: new[] { window });

        Assert.Same(host, surface.Polygon);
        Assert.Same(openingPolygon, surface.Windows.Single().Polygon);
        Assert.Equal(16, surface.GrossArea, 12);
        Assert.Equal(1, surface.OpeningArea, 12);
        Assert.Equal(15, surface.NetArea, 12);
        Assert.True(surface.Validate().IsValid);
    }

    [Fact]
    public void CenteredSubsurfaceUsesSquareRootAreaScale()
    {
        Surface surface = TestDomainFactory.Surface(
            "SURF-000001",
            "Wall",
            TestDomainFactory.Square(4));

        PlanarPolygon subsurface = surface.CreateCenteredSubsurface(4);

        Assert.Equal(4, subsurface.Area, 12);
        Assert.Equal(surface.Center, subsurface.Centroid);
        Assert.True(surface.Polygon.Contains(subsurface));
    }

    [Fact]
    public void SurfaceValidationReportsOpeningOutsideHostWithOpeningProvenance()
    {
        var provenance = new GeometryProvenance(
            Guid.NewGuid(),
            2,
            "sha256:opening",
            "{0}",
            1);
        var window = new Window(
            new EntityId("FNST-OUTSIDE"),
            "Outside",
            new Glazing("Glass", 1.2, 0.4),
            TestDomainFactory.Square(1, x: 3.5, y: 3.5),
            provenance: provenance);
        Surface surface = TestDomainFactory.Surface(
            "SURF-000001",
            "Wall",
            TestDomainFactory.Square(4),
            openings: new[] { window });

        var validation = surface.Validate();

        Assert.False(validation.IsValid);
        var diagnostic = Assert.Single(
            validation.Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.SURFACE.OPENING_OUTSIDE_HOST");
        Assert.Equal(window.Id, diagnostic.ObjectId);
        Assert.Equal(provenance, diagnostic.Geometry);
    }

    [Fact]
    public void SurfaceValidationAccumulatesDuplicateAndOverlappingOpeningErrors()
    {
        var glazing = new Glazing("Glass", 1.2, 0.4);
        var sharedId = new EntityId("FNST-DUPLICATE");
        var first = new Window(
            sharedId,
            "First",
            glazing,
            TestDomainFactory.Square(2, x: 0.5, y: 0.5));
        var second = new Window(
            sharedId,
            "Second",
            glazing,
            TestDomainFactory.Square(2, x: 1.5, y: 1.5));
        Surface surface = TestDomainFactory.Surface(
            "SURF-000001",
            "Wall",
            TestDomainFactory.Square(5),
            openings: new[] { first, second });

        var validation = surface.Validate();

        Assert.Contains(validation.Diagnostics, item => item.Code == "INVISIBLEDRAGON.SURFACE.DUPLICATE_OPENING_ID");
        Assert.Contains(validation.Diagnostics, item => item.Code == "INVISIBLEDRAGON.SURFACE.OPENINGS_OVERLAP");
    }

    [Fact]
    public void WindowAndDoorRetainTheirDistinctConstructionKinds()
    {
        var window = new Window(
            new EntityId("FNST-WINDOW"),
            "Window",
            new Glazing("Glass", 1.2, 0.4),
            TestDomainFactory.Square());
        var doorConstruction = new NoMassConstruction("Door", 2.2);
        var door = new Door(
            new EntityId("FNST-DOOR"),
            "Door",
            doorConstruction,
            TestDomainFactory.Square());

        Assert.Equal(OpeningType.Window, window.Type);
        Assert.Equal(OpeningType.Door, door.Type);
        Assert.Same(doorConstruction, door.Construction);
    }

    [Fact]
    public void ShadingPropertiesArePhysicallyBounded()
    {
        var shade = new Shade("Fabric", 0.2, 0.3);

        Assert.Equal(0.5, shade.Emissivity, 12);
        Assert.Throws<ArgumentException>(() => new Shade("Bad", 0.8, 0.4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Blind("Bad", 0.01, 0.02, 0, -0.1, 0.5));
    }

    [Fact]
    public void SurfaceBoundaryEnforcesAdjacentIdentifierInvariant()
    {
        Assert.Throws<ArgumentException>(() => new SurfaceBoundary(SurfaceBoundaryCondition.Zone));
        Assert.Throws<ArgumentException>(() => new SurfaceBoundary(
            SurfaceBoundaryCondition.Outdoors,
            new EntityId("SURF-OTHER")));
        Assert.Equal(
            new EntityId("SURF-OTHER"),
            SurfaceBoundary.AdjacentTo(new EntityId("SURF-OTHER")).AdjacentSurfaceId);
    }
}
