using System.Collections.ObjectModel;
using Rhino.Geometry;
using GonieGonie.InvisibleDragon.Shape;
using DragonVector = GonieGonie.InvisibleDragon.Shape.Vector3;

namespace GonieGonie.InvisibleDragon.Rhino;

/// <summary>
/// A planar Brep face decomposed into one outer polygon and zero or more inner polygons.
/// </summary>
public sealed class RhinoPolygonExtraction
{
    public RhinoPolygonExtraction(
        PlanarPolygon outerLoop,
        IEnumerable<PlanarPolygon> innerLoops,
        Plane sourcePlane)
    {
        OuterLoop = outerLoop ?? throw new ArgumentNullException(nameof(outerLoop));
        InnerLoops = new ReadOnlyCollection<PlanarPolygon>(
            (innerLoops ?? throw new ArgumentNullException(nameof(innerLoops))).ToArray());
        SourcePlane = sourcePlane;
        GeometryFingerprint = RhinoGeometryFingerprint.ForFace(OuterLoop, InnerLoops);
    }

    public PlanarPolygon OuterLoop { get; }

    public IReadOnlyList<PlanarPolygon> InnerLoops { get; }

    public Plane SourcePlane { get; }

    public string GeometryFingerprint { get; }
}

/// <summary>
/// Converts polygonal Rhino geometry to and from the SI-only InvisibleDragon shape domain.
/// </summary>
public static class RhinoPolygonConverter
{
    public static PlanarPolygon FromPolyline(Polyline polyline, RhinoGeometryContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!polyline.IsValid || !polyline.IsClosed)
        {
            throw new ArgumentException("A valid closed Rhino polyline is required.", nameof(polyline));
        }

        List<Vertex> vertices = polyline.Select(context.ToDragon).ToList();
        RemoveClosingVertex(vertices, context.ModelToleranceMetres);
        return new PlanarPolygon(vertices, Math.Max(context.ModelToleranceMetres, GeometryTolerance.Planarity));
    }

    public static PlanarPolygon FromClosedCurve(Curve curve, RhinoGeometryContext context)
    {
        if (curve is null)
        {
            throw new ArgumentNullException(nameof(curve));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!curve.IsValid || !curve.IsClosed)
        {
            throw new ArgumentException("A valid closed Rhino curve is required.", nameof(curve));
        }

        if (!curve.TryGetPolyline(out Polyline polyline))
        {
            throw new NotSupportedException(
                "The first release supports polygonal loops only; convert curved boundaries to a closed polyline explicitly.");
        }

        return FromPolyline(polyline, context);
    }

    public static RhinoPolygonExtraction FromBrepFace(BrepFace face, RhinoGeometryContext context)
    {
        if (face is null)
        {
            throw new ArgumentNullException(nameof(face));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!face.IsValid || !face.TryGetPlane(out Plane plane, context.SourceAbsoluteTolerance))
        {
            throw new ArgumentException("The Brep face must be valid and planar within the Rhino document tolerance.", nameof(face));
        }

        BrepLoop? outer = face.OuterLoop;
        if (outer is null)
        {
            throw new ArgumentException("The Brep face does not have an outer boundary loop.", nameof(face));
        }

        Vector3d desiredNormal = plane.Normal;
        if (face.OrientationIsReversed)
        {
            desiredNormal.Reverse();
        }

        PlanarPolygon outerPolygon = OrientToNormal(
            FromLoop(outer, context),
            desiredNormal);
        PlanarPolygon[] holes = face.Loops
            .Where(loop => loop.LoopType == BrepLoopType.Inner)
            .Select(loop => OrientToNormal(FromLoop(loop, context), desiredNormal))
            .ToArray();
        return new RhinoPolygonExtraction(outerPolygon, holes, plane);
    }

    public static Polyline ToPolyline(PlanarPolygon polygon, RhinoGeometryContext context)
    {
        if (polygon is null)
        {
            throw new ArgumentNullException(nameof(polygon));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var result = new Polyline(polygon.Vertices.Select(context.ToRhino));
        result.Add(result[0]);
        return result;
    }

    public static PolylineCurve ToCurve(PlanarPolygon polygon, RhinoGeometryContext context)
    {
        return new PolylineCurve(ToPolyline(polygon, context));
    }

    public static Brep ToBrep(
        PlanarPolygon outerLoop,
        IEnumerable<PlanarPolygon>? innerLoops,
        RhinoGeometryContext context)
    {
        if (outerLoop is null)
        {
            throw new ArgumentNullException(nameof(outerLoop));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        PlanarPolygon[] holes = innerLoops?.ToArray() ?? Array.Empty<PlanarPolygon>();
        var curves = new List<Curve> { ToCurve(outerLoop, context) };
        curves.AddRange(holes.Select(loop => (Curve)ToCurve(loop, context)));

        Brep[] created;
        try
        {
            created = Brep.CreatePlanarBreps(curves, context.SourceAbsoluteTolerance);
        }
        finally
        {
            foreach (Curve curve in curves)
            {
                curve.Dispose();
            }
        }

        if (created.Length != 1)
        {
            foreach (Brep item in created)
            {
                item.Dispose();
            }

            throw new InvalidOperationException(
                $"Expected one planar Brep from the polygon loops, but Rhino created {created.Length}.");
        }

        Brep result = created[0];
        double expectedArea = outerLoop.Area - holes.Sum(loop => loop.Area);
        if (expectedArea <= GeometryTolerance.Area)
        {
            result.Dispose();
            throw new ArgumentException(
                "The inner loops must leave a positive planar face area.",
                nameof(innerLoops));
        }

        bool completed = false;
        try
        {
            AlignBrepNormal(result, outerLoop, expectedArea, context);
            completed = true;
            return result;
        }
        finally
        {
            if (!completed)
            {
                result.Dispose();
            }
        }
    }

    public static Brep ToBrep(PlanarPolygon polygon, RhinoGeometryContext context)
    {
        return ToBrep(polygon, Array.Empty<PlanarPolygon>(), context);
    }

    public static Curve ProjectOpeningToFacePlane(
        Curve opening,
        BrepFace hostFace,
        RhinoGeometryContext context)
    {
        if (opening is null)
        {
            throw new ArgumentNullException(nameof(opening));
        }

        if (hostFace is null)
        {
            throw new ArgumentNullException(nameof(hostFace));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!hostFace.TryGetPlane(out Plane plane, context.SourceAbsoluteTolerance))
        {
            throw new ArgumentException("The host face must be planar.", nameof(hostFace));
        }

        Curve projected = Curve.ProjectToPlane(opening, plane);
        if (projected is null || !projected.IsValid || !projected.IsClosed)
        {
            projected?.Dispose();
            throw new ArgumentException("The opening could not be projected to a valid closed loop.", nameof(opening));
        }

        return projected;
    }

    private static PlanarPolygon FromLoop(BrepLoop loop, RhinoGeometryContext context)
    {
        using Curve curve = loop.To3dCurve();
        if (curve.TryGetPolyline(out Polyline polyline))
        {
            return FromPolyline(polyline, context);
        }

        if (loop.Trims.Count < 3 || loop.Trims.Any(trim => !trim.IsLinear(context.SourceAbsoluteTolerance)))
        {
            throw new NotSupportedException(
                "The first release supports planar Brep faces whose boundary trims are straight line segments.");
        }

        var points = new Polyline(loop.Trims.Select(trim => trim.PointAtStart));
        points.Add(points[0]);
        return FromPolyline(points, context);
    }

    private static PlanarPolygon OrientToNormal(PlanarPolygon polygon, Vector3d desiredNormal)
    {
        var desired = new DragonVector(desiredNormal.X, desiredNormal.Y, desiredNormal.Z).Normalize();
        return polygon.Normal.Dot(desired) < 0 ? polygon.Reverse() : polygon;
    }

    private static void AlignBrepNormal(
        Brep brep,
        PlanarPolygon polygon,
        double expectedArea,
        RhinoGeometryContext context)
    {
        if (brep.Faces.Count == 0)
        {
            throw new InvalidOperationException("Rhino created a Brep without a face.");
        }

        BrepFace face = brep.Faces[0];
        Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
        if (face.OrientationIsReversed)
        {
            normal.Reverse();
        }

        DragonVector dragonNormal = new(normal.X, normal.Y, normal.Z);
        if (dragonNormal.Dot(polygon.Normal) < 0)
        {
            brep.Flip();
        }

        if (!brep.IsValid || Math.Abs(brep.GetArea() * context.SourceUnitsToMetres * context.SourceUnitsToMetres - expectedArea) >
            Math.Max(GeometryTolerance.Area, expectedArea * 1e-8))
        {
            throw new InvalidOperationException("Rhino did not preserve the source planar polygon area.");
        }
    }

    private static void RemoveClosingVertex(List<Vertex> vertices, double tolerance)
    {
        if (vertices.Count > 1 && vertices[0].AlmostEquals(vertices[vertices.Count - 1], tolerance))
        {
            vertices.RemoveAt(vertices.Count - 1);
        }
    }
}
