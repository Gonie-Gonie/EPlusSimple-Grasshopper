using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Tests;

public sealed class PlanarPolygonTests
{
    [Fact]
    public void PolygonCalculatesAreaNormalCentroidAndHeightFromVertices()
    {
        var polygon = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(4, 0, 0),
            new Vertex(4, 3, 0),
            new Vertex(0, 3, 0),
        });

        Assert.Equal(12, polygon.Area, 12);
        Assert.True(polygon.Normal.AlmostEquals(new Vector3(0, 0, 1)));
        Assert.Equal(new Vertex(2, 1.5, 0), polygon.Centroid);
        Assert.Equal(0, polygon.Height);
        Assert.Equal(PolygonWinding.CounterClockwise, polygon.WindingRelativeTo(new Vector3(0, 0, 1)));
    }

    [Fact]
    public void ReverseFlipsNormalAndPreservesAreaAndVertices()
    {
        PlanarPolygon original = TestDomainFactory.Square(2);
        PlanarPolygon reversed = original.Reverse();

        Assert.Equal(original.Area, reversed.Area, 12);
        Assert.True(original.Normal.AlmostEquals(-reversed.Normal));
        Assert.True(original.IsGeometricallyEquivalentTo(reversed));
        Assert.False(original.IsGeometricallyEquivalentTo(reversed, allowReversedWinding: false));
    }

    [Fact]
    public void ScaleAboutCentroidUsesLinearScaleAndPredictableAreaScale()
    {
        PlanarPolygon original = TestDomainFactory.Square(4);
        PlanarPolygon scaled = original.ScaleAboutCentroid(0.5);

        Assert.Equal(4, scaled.Area, 12);
        Assert.Equal(original.Centroid, scaled.Centroid);
    }

    [Fact]
    public void ContainsWorksForConcavePolygonAndBoundaryChoice()
    {
        var concave = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(3, 0, 0),
            new Vertex(3, 1, 0),
            new Vertex(1, 1, 0),
            new Vertex(1, 3, 0),
            new Vertex(0, 3, 0),
        });

        Assert.True(concave.Contains(new Vertex(0.5, 2, 0)));
        Assert.False(concave.Contains(new Vertex(2, 2, 0)));
        Assert.True(concave.Contains(new Vertex(0, 1, 0), includeBoundary: true));
        Assert.False(concave.Contains(new Vertex(0, 1, 0), includeBoundary: false));
        Assert.False(concave.Contains(new Vertex(0.5, 2, 0.1)));
    }

    [Fact]
    public void PolygonContainmentChecksEdgesAsWellAsVertices()
    {
        var concave = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(4, 0, 0),
            new Vertex(4, 1, 0),
            new Vertex(1, 1, 0),
            new Vertex(1, 4, 0),
            new Vertex(0, 4, 0),
        });
        var crossingCandidate = new PlanarPolygon(new[]
        {
            new Vertex(0.5, 3, 0),
            new Vertex(3, 0.5, 0),
            new Vertex(0.5, 0.5, 0),
        });

        Assert.All(crossingCandidate.Vertices, vertex => Assert.True(concave.Contains(vertex)));
        Assert.False(concave.Contains(crossingCandidate));
    }

    [Fact]
    public void ValidationReportsMultipleGeometryProblemsWithProvenance()
    {
        var provenance = new GeometryProvenance(
            Guid.NewGuid(),
            0,
            "sha256:face",
            "{0;0}",
            2);
        var id = new EntityId("SURF-000001");
        Vertex[] invalid =
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(1, 1, 0),
            new(0, 1, 0.1),
            new(0, 0, 0),
        };

        var result = PlanarPolygon.Validate(invalid, id, provenance);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "INVISIBLEDRAGON.GEOMETRY.NON_PLANAR");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "INVISIBLEDRAGON.GEOMETRY.DUPLICATE_CLOSING_VERTEX");
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(id, diagnostic.ObjectId));
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(provenance, diagnostic.Geometry));
    }

    [Fact]
    public void ConstructorRejectsDegenerateAndSelfIntersectingLoops()
    {
        Assert.Throws<ArgumentException>(() => new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 1, 0),
            new Vertex(2, 2, 0),
        }));

        Assert.Throws<ArgumentException>(() => new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(2, 0, 0),
            new Vertex(0, 2, 0),
            new Vertex(2, 2, 0),
            new Vertex(1, -1, 0),
        }));
    }

    [Fact]
    public void PolygonDefensivelyCopiesVertexInput()
    {
        Vertex[] source =
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(1, 1, 0),
            new(0, 1, 0),
        };
        var polygon = new PlanarPolygon(source);
        source[0] = new Vertex(100, 100, 100);

        Assert.Equal(Vertex.Origin, polygon.Vertices[0]);
    }
}
