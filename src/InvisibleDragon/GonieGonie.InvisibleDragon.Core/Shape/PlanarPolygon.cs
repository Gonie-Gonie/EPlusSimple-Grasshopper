using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Shape;

/// <summary>
/// An immutable planar, non-self-intersecting polygon that preserves its full vertex loop.
/// </summary>
public sealed class PlanarPolygon : IEquatable<PlanarPolygon>
{
    private readonly ProjectionAxis projectionAxis;

    public PlanarPolygon(IEnumerable<Vertex> vertices, double tolerance = GeometryTolerance.Planarity)
    {
        DomainGuard.NotNull(vertices, nameof(vertices));
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        Vertex[] copy = vertices.ToArray();
        ValidationResult validation = Validate(copy, tolerance: tolerance);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Diagnostics[0].Message, nameof(vertices));
        }

        Vertices = new ReadOnlyCollection<Vertex>(copy);
        AreaVector = CalculateAreaVector(copy);
        Area = AreaVector.Length / 2;
        Normal = AreaVector.Normalize(GeometryTolerance.Area);
        Centroid = new Vertex(
            copy.Average(vertex => vertex.X),
            copy.Average(vertex => vertex.Y),
            copy.Average(vertex => vertex.Z));
        projectionAxis = ProjectionAxis.FromNormal(Normal);
    }

    public IReadOnlyList<Vertex> Vertices { get; }

    public Vector3 Normal { get; }

    public Vector3 AreaVector { get; }

    public double Area { get; }

    public Vertex Centroid { get; }

    public double Height => Vertices.Max(vertex => vertex.Z) - Vertices.Min(vertex => vertex.Z);

    public static ValidationResult Validate(
        IEnumerable<Vertex> vertices,
        EntityId? objectId = null,
        GeometryProvenance? provenance = null,
        double tolerance = GeometryTolerance.Planarity)
    {
        DomainGuard.NotNull(vertices, nameof(vertices));
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        Vertex[] points = vertices.ToArray();
        List<Diagnostic> diagnostics = new();

        if (points.Length < 3)
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.GEOMETRY.TOO_FEW_VERTICES",
                "A polygon requires at least three vertices.",
                objectId,
                provenance,
                "Supply an open vertex loop with at least three distinct points."));
            return ValidationResult.From(diagnostics);
        }

        if (points[0].AlmostEquals(points[points.Length - 1], tolerance))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.GEOMETRY.DUPLICATE_CLOSING_VERTEX",
                "The first vertex must not be repeated at the end of the polygon loop.",
                objectId,
                provenance,
                "Remove the final duplicate vertex; polygon closure is implicit."));
        }

        for (int index = 0; index < points.Length; index++)
        {
            Vertex current = points[index];
            Vertex next = points[(index + 1) % points.Length];
            if (current.AlmostEquals(next, tolerance))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.GEOMETRY.ZERO_LENGTH_EDGE",
                    $"Polygon edge {index} has zero length within tolerance.",
                    objectId,
                    provenance,
                    "Remove duplicate consecutive vertices."));
            }
        }

        if (!Vertex.AreCoplanar(points, tolerance))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.GEOMETRY.NON_PLANAR",
                "Polygon vertices are not coplanar within the model tolerance.",
                objectId,
                provenance,
                "Planarize or triangulate the source geometry before conversion."));
        }

        Vector3 areaVector = CalculateAreaVector(points);
        if (areaVector.Length / 2 <= GeometryTolerance.Area)
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.GEOMETRY.ZERO_AREA",
                "Polygon area is zero or numerically degenerate.",
                objectId,
                provenance,
                "Supply a non-collinear polygon loop."));
        }
        else
        {
            ProjectionAxis axis = ProjectionAxis.FromNormal(areaVector.Normalize(GeometryTolerance.Area));
            if (HasSelfIntersection(points, axis, tolerance))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.GEOMETRY.SELF_INTERSECTION",
                    "Polygon edges intersect each other.",
                    objectId,
                    provenance,
                    "Supply a simple outer loop without crossings."));
            }
        }

        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    public PlanarPolygon Reverse()
    {
        return new PlanarPolygon(Vertices.Reverse());
    }

    public PlanarPolygon ScaleAboutCentroid(double linearScale)
    {
        if (linearScale <= 0 || double.IsNaN(linearScale) || double.IsInfinity(linearScale))
        {
            throw new ArgumentOutOfRangeException(nameof(linearScale), linearScale, "A finite positive scale is required.");
        }

        return new PlanarPolygon(
            Vertices.Select(vertex => Centroid + ((vertex - Centroid) * linearScale)));
    }

    public double SignedAreaRelativeTo(Vector3 referenceNormal)
    {
        return AreaVector.Dot(referenceNormal.Normalize()) / 2;
    }

    public PolygonWinding WindingRelativeTo(Vector3 referenceNormal)
    {
        return SignedAreaRelativeTo(referenceNormal) >= 0
            ? PolygonWinding.CounterClockwise
            : PolygonWinding.Clockwise;
    }

    public bool IsCoplanarWith(PlanarPolygon other, double tolerance = GeometryTolerance.Planarity)
    {
        DomainGuard.NotNull(other, nameof(other));
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        double alignment = Math.Abs(Normal.Dot(other.Normal));
        if (1 - alignment > tolerance)
        {
            return false;
        }

        Vertex origin = Vertices[0];
        return other.Vertices.All(vertex => Math.Abs((vertex - origin).Dot(Normal)) <= tolerance);
    }

    public bool Contains(Vertex point, bool includeBoundary = true, double tolerance = GeometryTolerance.Distance)
    {
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        if (Math.Abs((point - Vertices[0]).Dot(Normal)) > tolerance)
        {
            return false;
        }

        Point2 projectedPoint = projectionAxis.Project(point);
        Point2[] projected = Vertices.Select(projectionAxis.Project).ToArray();
        bool inside = false;

        for (int current = 0, previous = projected.Length - 1; current < projected.Length; previous = current++)
        {
            Point2 a = projected[previous];
            Point2 b = projected[current];
            if (PointOnSegment(projectedPoint, a, b, tolerance))
            {
                return includeBoundary;
            }

            bool crosses = (a.Y > projectedPoint.Y) != (b.Y > projectedPoint.Y);
            if (crosses)
            {
                double crossingX = ((b.X - a.X) * (projectedPoint.Y - a.Y) / (b.Y - a.Y)) + a.X;
                if (projectedPoint.X < crossingX)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    public bool Contains(PlanarPolygon other, double tolerance = GeometryTolerance.Distance)
    {
        DomainGuard.NotNull(other, nameof(other));
        if (!IsCoplanarWith(other, tolerance))
        {
            return false;
        }

        for (int index = 0; index < other.Vertices.Count; index++)
        {
            Vertex start = other.Vertices[index];
            Vertex end = other.Vertices[(index + 1) % other.Vertices.Count];
            if (!Contains(start, true, tolerance)
                || !Contains(start + ((end - start) * 0.5), true, tolerance))
            {
                return false;
            }

            Point2 candidateStart = projectionAxis.Project(start);
            Point2 candidateEnd = projectionAxis.Project(end);
            Point2[] host = Vertices.Select(projectionAxis.Project).ToArray();
            for (int hostIndex = 0; hostIndex < host.Length; hostIndex++)
            {
                if (SegmentsProperlyIntersect(
                    candidateStart,
                    candidateEnd,
                    host[hostIndex],
                    host[(hostIndex + 1) % host.Length],
                    tolerance))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool IntersectsInterior(PlanarPolygon other, double tolerance = GeometryTolerance.Distance)
    {
        DomainGuard.NotNull(other, nameof(other));
        if (!IsCoplanarWith(other, tolerance))
        {
            return false;
        }

        if (IsGeometricallyEquivalentTo(other, true, tolerance))
        {
            return true;
        }

        if (Vertices.Any(vertex => other.Contains(vertex, false, tolerance))
            || other.Vertices.Any(vertex => Contains(vertex, false, tolerance)))
        {
            return true;
        }

        Point2[] left = Vertices.Select(projectionAxis.Project).ToArray();
        Point2[] right = other.Vertices.Select(projectionAxis.Project).ToArray();
        for (int first = 0; first < left.Length; first++)
        {
            for (int second = 0; second < right.Length; second++)
            {
                if (SegmentsProperlyIntersect(
                    left[first],
                    left[(first + 1) % left.Length],
                    right[second],
                    right[(second + 1) % right.Length],
                    tolerance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsGeometricallyEquivalentTo(
        PlanarPolygon other,
        bool allowReversedWinding = true,
        double tolerance = GeometryTolerance.Distance)
    {
        if (other is null || Vertices.Count != other.Vertices.Count)
        {
            return false;
        }

        for (int start = 0; start < other.Vertices.Count; start++)
        {
            if (!Vertices[0].AlmostEquals(other.Vertices[start], tolerance))
            {
                continue;
            }

            bool same = true;
            bool reverse = true;
            for (int offset = 0; offset < Vertices.Count; offset++)
            {
                same &= Vertices[offset].AlmostEquals(
                    other.Vertices[(start + offset) % other.Vertices.Count],
                    tolerance);
                int reverseIndex = (start - offset + other.Vertices.Count) % other.Vertices.Count;
                reverse &= Vertices[offset].AlmostEquals(other.Vertices[reverseIndex], tolerance);
            }

            if (same || (allowReversedWinding && reverse))
            {
                return true;
            }
        }

        return false;
    }

    public bool Equals(PlanarPolygon? other)
    {
        return other is not null && Vertices.SequenceEqual(other.Vertices);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PlanarPolygon);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (Vertex vertex in Vertices)
            {
                hash = (hash * 397) ^ vertex.GetHashCode();
            }

            return hash;
        }
    }

    private static Diagnostic Error(
        string code,
        string message,
        EntityId? objectId,
        GeometryProvenance? provenance,
        string suggestedAction)
    {
        return new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            message,
            objectId,
            provenance,
            suggestedAction);
    }

    private static Vector3 CalculateAreaVector(IReadOnlyList<Vertex> vertices)
    {
        Vector3 sum = Vector3.Zero;
        for (int index = 0; index < vertices.Count; index++)
        {
            sum += vertices[index].ToVector().Cross(vertices[(index + 1) % vertices.Count].ToVector());
        }

        return sum;
    }

    private static bool HasSelfIntersection(
        IReadOnlyList<Vertex> vertices,
        ProjectionAxis axis,
        double tolerance)
    {
        Point2[] projected = vertices.Select(axis.Project).ToArray();
        for (int first = 0; first < projected.Length; first++)
        {
            int firstNext = (first + 1) % projected.Length;
            for (int second = first + 1; second < projected.Length; second++)
            {
                int secondNext = (second + 1) % projected.Length;
                if (first == second || firstNext == second || secondNext == first)
                {
                    continue;
                }

                if (SegmentsIntersect(
                    projected[first],
                    projected[firstNext],
                    projected[second],
                    projected[secondNext],
                    tolerance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Point2 a, Point2 b, Point2 c, Point2 d, double tolerance)
    {
        double abC = Cross(a, b, c);
        double abD = Cross(a, b, d);
        double cdA = Cross(c, d, a);
        double cdB = Cross(c, d, b);

        if (((abC > tolerance && abD < -tolerance) || (abC < -tolerance && abD > tolerance))
            && ((cdA > tolerance && cdB < -tolerance) || (cdA < -tolerance && cdB > tolerance)))
        {
            return true;
        }

        return (Math.Abs(abC) <= tolerance && PointOnSegment(c, a, b, tolerance))
            || (Math.Abs(abD) <= tolerance && PointOnSegment(d, a, b, tolerance))
            || (Math.Abs(cdA) <= tolerance && PointOnSegment(a, c, d, tolerance))
            || (Math.Abs(cdB) <= tolerance && PointOnSegment(b, c, d, tolerance));
    }

    private static bool SegmentsProperlyIntersect(Point2 a, Point2 b, Point2 c, Point2 d, double tolerance)
    {
        double abC = Cross(a, b, c);
        double abD = Cross(a, b, d);
        double cdA = Cross(c, d, a);
        double cdB = Cross(c, d, b);
        return ((abC > tolerance && abD < -tolerance) || (abC < -tolerance && abD > tolerance))
            && ((cdA > tolerance && cdB < -tolerance) || (cdA < -tolerance && cdB > tolerance));
    }

    private static double Cross(Point2 a, Point2 b, Point2 point)
    {
        return ((b.X - a.X) * (point.Y - a.Y)) - ((b.Y - a.Y) * (point.X - a.X));
    }

    private static bool PointOnSegment(Point2 point, Point2 start, Point2 end, double tolerance)
    {
        if (Math.Abs(Cross(start, end, point)) > tolerance)
        {
            return false;
        }

        return point.X >= Math.Min(start.X, end.X) - tolerance
            && point.X <= Math.Max(start.X, end.X) + tolerance
            && point.Y >= Math.Min(start.Y, end.Y) - tolerance
            && point.Y <= Math.Max(start.Y, end.Y) + tolerance;
    }

    private readonly struct Point2
    {
        public Point2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }

    private readonly struct ProjectionAxis
    {
        private ProjectionAxis(int droppedCoordinate)
        {
            DroppedCoordinate = droppedCoordinate;
        }

        private int DroppedCoordinate { get; }

        public static ProjectionAxis FromNormal(Vector3 normal)
        {
            double x = Math.Abs(normal.X);
            double y = Math.Abs(normal.Y);
            double z = Math.Abs(normal.Z);
            return new ProjectionAxis(x >= y && x >= z ? 0 : y >= z ? 1 : 2);
        }

        public Point2 Project(Vertex vertex)
        {
            return DroppedCoordinate switch
            {
                0 => new Point2(vertex.Y, vertex.Z),
                1 => new Point2(vertex.X, vertex.Z),
                _ => new Point2(vertex.X, vertex.Y),
            };
        }
    }
}
