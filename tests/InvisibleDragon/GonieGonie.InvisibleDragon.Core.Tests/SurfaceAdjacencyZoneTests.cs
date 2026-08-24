using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Tests;

public sealed class SurfaceAdjacencyZoneTests
{
    [Fact]
    public void MatchCreatesReciprocalBoundariesAndOppositeNormals()
    {
        Surface first = TestDomainFactory.Surface(
            "SURF-FIRST",
            "First",
            TestDomainFactory.Square(2));
        Surface second = TestDomainFactory.Surface(
            "SURF-SECOND",
            "Second",
            TestDomainFactory.Square(2));

        SurfaceAdjacencyPair pair = SurfaceAdjacency.Match(first, second);

        Assert.Equal(second.Id, pair.First.Boundary.AdjacentSurfaceId);
        Assert.Equal(first.Id, pair.Second.Boundary.AdjacentSurfaceId);
        Assert.Equal(SurfaceBoundaryCondition.Zone, pair.First.Boundary.Condition);
        Assert.True(pair.First.Normal.Dot(pair.Second.Normal) < -0.999999);
        Assert.Equal(SurfaceBoundaryCondition.Outdoors, first.Boundary.Condition);
    }

    [Fact]
    public void AdjacencyValidationRejectsDifferentPolygonLoops()
    {
        Surface first = TestDomainFactory.Surface(
            "SURF-FIRST",
            "First",
            TestDomainFactory.Square(2));
        Surface second = TestDomainFactory.Surface(
            "SURF-SECOND",
            "Second",
            TestDomainFactory.Square(2, x: 0.1));

        var validation = SurfaceAdjacency.ValidateMatch(first, second);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, item => item.Code == "INVISIBLEDRAGON.ADJACENCY.GEOMETRY_MISMATCH");
        Assert.Throws<ArgumentException>(() => SurfaceAdjacency.Match(first, second));
    }

    [Fact]
    public void AdjacencyRequiresMirroredOpeningGeometryAndType()
    {
        var opening = new Window(
            new EntityId("FNST-FIRST"),
            "First window",
            new GonieGonie.InvisibleDragon.Construction.Glazing("Glass", 1.2, 0.4),
            TestDomainFactory.Square(0.5, x: 0.5, y: 0.5));
        Surface first = TestDomainFactory.Surface(
            "SURF-FIRST",
            "First",
            TestDomainFactory.Square(2),
            openings: new[] { opening });
        Surface second = TestDomainFactory.Surface(
            "SURF-SECOND",
            "Second",
            TestDomainFactory.Square(2));

        var validation = SurfaceAdjacency.ValidateMatch(first, second);

        Assert.Contains(validation.Diagnostics, item => item.Code == "INVISIBLEDRAGON.ADJACENCY.OPENING_COUNT_MISMATCH");
    }

    [Fact]
    public void ZoneCalculatesFloorNetAreaAndValidatesSurfaceDiagnostics()
    {
        Surface floor = TestDomainFactory.Surface(
            "SURF-FLOOR",
            "Floor",
            TestDomainFactory.Square(5),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        var zone = new Zone(
            new EntityId("ZONE-000001"),
            "Zone",
            new[] { floor },
            TestDomainFactory.EmptyProfile(),
            infiltrationAirChangesPerHour: 0.4,
            lightingPowerDensityWattsPerSquareMetre: 8,
            outdoorAirFlowCubicMetresPerSecond: 0.1);

        Assert.Equal(25, zone.FloorArea, 12);
        Assert.True(zone.Validate().IsValid);
        Assert.Equal(0.4, zone.InfiltrationAirChangesPerHour);
    }

    [Fact]
    public void ZoneValidationDefersCrossZoneAdjacencyAsWarning()
    {
        Surface missing = TestDomainFactory.Surface(
            "SURF-FIRST",
            "Missing",
            boundary: SurfaceBoundary.AdjacentTo(new EntityId("SURF-NOT-THERE")));
        var zone = new Zone(
            new EntityId("ZONE-000001"),
            "Zone",
            new[] { missing },
            TestDomainFactory.EmptyProfile());

        var validation = zone.Validate();

        Assert.Contains(validation.Diagnostics, item => item.Code == "INVISIBLEDRAGON.ZONE.ADJACENT_SURFACE_OUTSIDE_SCOPE");
        Assert.True(validation.IsValid);
        Assert.True(validation.HasWarnings);
    }

    [Fact]
    public void ZoneValidationAcceptsReciprocalMatchedSurfaces()
    {
        Surface first = TestDomainFactory.Surface(
            "SURF-FIRST",
            "First",
            TestDomainFactory.Square(2));
        Surface second = TestDomainFactory.Surface(
            "SURF-SECOND",
            "Second",
            TestDomainFactory.Square(2));
        SurfaceAdjacencyPair pair = SurfaceAdjacency.Match(first, second);
        Surface floor = TestDomainFactory.Surface(
            "SURF-FLOOR",
            "Floor",
            TestDomainFactory.Square(2, z: -1),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        var zone = new Zone(
            new EntityId("ZONE-000001"),
            "Zone",
            new[] { pair.First, pair.Second, floor },
            TestDomainFactory.EmptyProfile());

        var validation = zone.Validate();

        Assert.True(validation.IsValid);
    }

    [Fact]
    public void ZoneRejectsNegativeLoadInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zone(
            new EntityId("ZONE-000001"),
            "Zone",
            Array.Empty<Surface>(),
            TestDomainFactory.EmptyProfile(),
            infiltrationAirChangesPerHour: -0.1));
    }
}
