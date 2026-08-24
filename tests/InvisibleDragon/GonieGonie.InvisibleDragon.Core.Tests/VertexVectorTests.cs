using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Tests;

public sealed class VertexVectorTests
{
    [Fact]
    public void VectorArithmeticMatchesRightHandedCoordinateSystem()
    {
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);

        Assert.Equal(new Vector3(0, 0, 1), x.Cross(y));
        Assert.Equal(0, x.Dot(y));
        Assert.Equal(Math.Sqrt(2), (x + y).Length, 12);
        Assert.Equal(new Vector3(0.5, 0, 0), x / 2);
    }

    [Fact]
    public void NormalizationProducesUnitVectorAndRejectsZero()
    {
        Vector3 unit = new Vector3(3, 4, 0).Normalize();

        Assert.Equal(1, unit.Length, 12);
        Assert.True(unit.AlmostEquals(new Vector3(0.6, 0.8, 0)));
        Assert.Throws<InvalidOperationException>(() => Vector3.Zero.Normalize());
    }

    [Fact]
    public void VertexAndVectorOperatorsPreservePointSemantics()
    {
        var point = new Vertex(1, 2, 3);
        var displacement = new Vector3(4, -2, 1);

        Vertex moved = point + displacement;

        Assert.Equal(new Vertex(5, 0, 4), moved);
        Assert.Equal(displacement, moved - point);
        Assert.Equal(Math.Sqrt(21), point.DistanceTo(moved), 12);
    }

    [Fact]
    public void CoplanarityHandlesArbitraryPlaneAndTolerance()
    {
        Vertex[] plane =
        {
            new(0, 0, 0),
            new(1, 0, 1),
            new(1, 1, 2),
            new(0, 1, 1 + 1e-9),
        };

        Assert.True(Vertex.AreCoplanar(plane));
        Assert.False(Vertex.AreCoplanar(plane, 1e-11));
    }

    [Fact]
    public void ThreeOrFewerVerticesAreAlwaysCoplanar()
    {
        Assert.True(Vertex.AreCoplanar(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 2, 3),
            new Vertex(-5, 4, 9),
        }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void GeometryRejectsNonFiniteCoordinates(double coordinate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vertex(coordinate, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3(0, coordinate, 0));
    }
}
