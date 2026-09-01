using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Rhino.Tests;

public sealed class RhinoGeometryFingerprintTests
{
    [Fact]
    public void FingerprintIgnoresCyclicStartAndReversedWinding()
    {
        var first = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(2, 0, 0),
            new Vertex(2, 1, 0),
            new Vertex(0, 1, 0),
        });
        var rotated = new PlanarPolygon(new[]
        {
            new Vertex(2, 1, 0),
            new Vertex(2, 0, 0),
            new Vertex(0, 0, 0),
            new Vertex(0, 1, 0),
        });

        string firstHash = RhinoGeometryFingerprint.ForPolygon(first);
        string secondHash = RhinoGeometryFingerprint.ForPolygon(rotated);

        Assert.Equal(firstHash, secondHash);
        Assert.Matches("^[0-9a-f]{64}$", firstHash);
    }

    [Fact]
    public void FingerprintChangesOutsideQuantizationTolerance()
    {
        var first = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0), new Vertex(0, 1, 0),
        });
        var changed = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0), new Vertex(1.001, 0, 0), new Vertex(1, 1, 0), new Vertex(0, 1, 0),
        });

        Assert.NotEqual(
            RhinoGeometryFingerprint.ForPolygon(first, 1e-5),
            RhinoGeometryFingerprint.ForPolygon(changed, 1e-5));
    }
}
