using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Shape;

/// <summary>
/// A three-dimensional model vertex whose coordinates are expressed in metres.
/// </summary>
public readonly struct Vertex : IEquatable<Vertex>
{
    public Vertex(double x, double y, double z)
    {
        X = DomainGuard.Finite(x, nameof(x));
        Y = DomainGuard.Finite(y, nameof(y));
        Z = DomainGuard.Finite(z, nameof(z));
    }

    public static Vertex Origin { get; } = new(0, 0, 0);

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public double DistanceTo(Vertex other)
    {
        return (this - other).Length;
    }

    public Vector3 ToVector()
    {
        return new Vector3(X, Y, Z);
    }

    public bool AlmostEquals(Vertex other, double tolerance = GeometryTolerance.Distance)
    {
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        return DistanceTo(other) <= tolerance;
    }

    public static bool AreCoplanar(IEnumerable<Vertex> vertices, double tolerance = GeometryTolerance.Planarity)
    {
        DomainGuard.NotNull(vertices, nameof(vertices));
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        Vertex[] points = vertices.ToArray();
        if (points.Length <= 3)
        {
            return true;
        }

        if (!TryFindPlane(points, tolerance, out Vertex origin, out Vector3 normal))
        {
            return true;
        }

        return points.All(point => Math.Abs((point - origin).Dot(normal)) <= tolerance);
    }

    internal static bool TryFindPlane(
        IReadOnlyList<Vertex> vertices,
        double tolerance,
        out Vertex origin,
        out Vector3 normal)
    {
        origin = vertices.Count == 0 ? Origin : vertices[0];
        normal = Vector3.Zero;

        for (int first = 1; first < vertices.Count - 1; first++)
        {
            Vector3 a = vertices[first] - origin;
            if (a.IsZero(tolerance))
            {
                continue;
            }

            for (int second = first + 1; second < vertices.Count; second++)
            {
                Vector3 candidate = a.Cross(vertices[second] - origin);
                if (!candidate.IsZero(tolerance * tolerance))
                {
                    normal = candidate.Normalize(tolerance * tolerance);
                    return true;
                }
            }
        }

        return false;
    }

    public static Vertex operator +(Vertex point, Vector3 vector)
    {
        return new Vertex(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);
    }

    public static Vertex operator -(Vertex point, Vector3 vector)
    {
        return new Vertex(point.X - vector.X, point.Y - vector.Y, point.Z - vector.Z);
    }

    public static Vector3 operator -(Vertex left, Vertex right)
    {
        return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    public bool Equals(Vertex other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vertex other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = X.GetHashCode();
            hash = (hash * 397) ^ Y.GetHashCode();
            hash = (hash * 397) ^ Z.GetHashCode();
            return hash;
        }
    }

    public override string ToString()
    {
        return $"({X:G17}, {Y:G17}, {Z:G17})";
    }

    public static bool operator ==(Vertex left, Vertex right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Vertex left, Vertex right)
    {
        return !left.Equals(right);
    }
}
