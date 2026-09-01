using Dragons.InvisibleDragon.Shape;
using Dragons.InvisibleDragon.Rhino;
using Rhino;
using Rhino.Geometry;
using RhinoApp = global::Rhino.RhinoApp;
using RhinoCore = global::Rhino.Runtime.InProcess.RhinoCore;

namespace Dragons.InvisibleDragon.RhinoSmoke;

internal static class RhinoSmokeChecks
{
    public static int Run(string[] coreArguments)
    {
        using var core = new RhinoCore(coreArguments);

        int checks = 0;
        Check(RhinoApp.Version.Major == 8, "Rhino 8 runtime was not loaded.");
        checks++;

        var context = new RhinoGeometryContext(UnitSystem.Millimeters, 0.01);
        var sourcePoint = new Point3d(1250, -250, 3000);
        Vertex vertex = context.ToDragon(sourcePoint);
        Check(AlmostEqual(vertex.X, 1.25) && AlmostEqual(vertex.Y, -0.25) && AlmostEqual(vertex.Z, 3),
            "Millimetre-to-SI point conversion changed.");
        Check(sourcePoint.EpsilonEquals(context.ToRhino(vertex), 1e-9), "SI point round-trip changed.");
        checks += 2;

        Polyline sourcePolyline = Closed(
            new Point3d(0, 0, 0),
            new Point3d(2000, 0, 0),
            new Point3d(2500, 1000, 0),
            new Point3d(1000, 2000, 0),
            new Point3d(0, 1000, 0));
        PlanarPolygon polygon = RhinoPolygonConverter.FromPolyline(sourcePolyline, context);
        Polyline restored = RhinoPolygonConverter.ToPolyline(polygon, context);
        Check(polygon.Vertices.Count == 5 && restored.Count == 6 && restored.IsClosed,
            "Closed polyline topology changed during round-trip.");
        Check(AlmostEqual(polygon.Area, 3.5), "Closed polyline area changed during SI conversion.");
        checks += 2;

        PlanarPolygon outer = Rectangle(0, 0, 4, 3);
        PlanarPolygon hole = Rectangle(1, 1, 2, 2);
        using Brep brep = RhinoPolygonConverter.ToBrep(outer, new[] { hole }, context);
        RhinoPolygonExtraction extraction = RhinoPolygonConverter.FromBrepFace(brep.Faces[0], context);
        Check(AlmostEqual(extraction.OuterLoop.Area, 12) &&
              extraction.InnerLoops.Count == 1 &&
              AlmostEqual(extraction.InnerLoops[0].Area, 1),
            "Planar Brep outer/inner loop extraction changed.");
        Check(extraction.OuterLoop.Normal.Dot(outer.Normal) > 0.999999,
            "Planar Brep normal orientation changed.");
        checks += 2;

        using var opening = new PolylineCurve(Closed(
            new Point3d(1000, 1000, 100),
            new Point3d(2000, 1000, 100),
            new Point3d(2000, 2000, 100),
            new Point3d(1000, 2000, 100)));
        using Curve projected = RhinoPolygonConverter.ProjectOpeningToFacePlane(opening, brep.Faces[0], context);
        PlanarPolygon projectedPolygon = RhinoPolygonConverter.FromClosedCurve(projected, context);
        Check(projected.IsClosed && projectedPolygon.Vertices.All(point => AlmostEqual(point.Z, 0)),
            "Opening projection did not preserve a closed host-plane loop.");
        checks++;

        using Brep first = RhinoPolygonConverter.ToBrep(outer, context);
        using Brep second = RhinoPolygonConverter.ToBrep(outer.Reverse(), context);
        IReadOnlyList<RhinoFaceAdjacencyMatch> matches = RhinoFaceAdjacency.FindCoincidentFaces(
            new[] { first.Faces[0], second.Faces[0] },
            context);
        Check(matches.Count == 1 && matches[0].FirstFaceIndex == 0 && matches[0].SecondFaceIndex == 1,
            "Coincident face matching changed.");
        checks++;

        string forwardHash = RhinoGeometryFingerprint.ForPolygon(outer);
        string reverseHash = RhinoGeometryFingerprint.ForPolygon(outer.Reverse());
        Check(string.Equals(forwardHash, reverseHash, StringComparison.Ordinal),
            "Geometry fingerprint is not winding-invariant.");
        checks++;

        Console.WriteLine(
            $"InvisibleDragon Rhino smoke checks passed: {checks} checks on Rhino {RhinoApp.Version}.");
        return 0;
    }

    private static PlanarPolygon Rectangle(double x0, double y0, double x1, double y1)
    {
        return new PlanarPolygon(new[]
        {
            new Vertex(x0, y0, 0),
            new Vertex(x1, y0, 0),
            new Vertex(x1, y1, 0),
            new Vertex(x0, y1, 0),
        });
    }

    private static Polyline Closed(params Point3d[] points)
    {
        var polyline = new Polyline(points);
        polyline.Add(points[0]);
        return polyline;
    }

    private static bool AlmostEqual(double first, double second)
    {
        return Math.Abs(first - second) <= 1e-10;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
