using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Shape;

/// <summary>
/// A Rhino-independent three-dimensional vector in a right-handed coordinate system.
/// </summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public Vector3(double x, double y, double z)
    {
        X = DomainGuard.Finite(x, nameof(x));
        Y = DomainGuard.Finite(y, nameof(y));
        Z = DomainGuard.Finite(z, nameof(z));
    }

    public static Vector3 Zero { get; } = new(0, 0, 0);

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    public double Length => Math.Sqrt(LengthSquared);

    public bool IsZero(double tolerance = GeometryTolerance.Distance)
    {
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        return Length <= tolerance;
    }

    public Vector3 Normalize(double tolerance = GeometryTolerance.Distance)
    {
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        double length = Length;
        if (length <= tolerance)
        {
            throw new InvalidOperationException("A zero-length vector cannot be normalized.");
        }

        return this / length;
    }

    public double Dot(Vector3 other)
    {
        return (X * other.X) + (Y * other.Y) + (Z * other.Z);
    }

    public Vector3 Cross(Vector3 other)
    {
        return new Vector3(
            (Y * other.Z) - (Z * other.Y),
            (Z * other.X) - (X * other.Z),
            (X * other.Y) - (Y * other.X));
    }

    public bool AlmostEquals(Vector3 other, double tolerance = GeometryTolerance.Distance)
    {
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        return (this - other).Length <= tolerance;
    }

    public static Vector3 operator +(Vector3 left, Vector3 right)
    {
        return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    public static Vector3 operator -(Vector3 left, Vector3 right)
    {
        return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    public static Vector3 operator -(Vector3 value)
    {
        return new Vector3(-value.X, -value.Y, -value.Z);
    }

    public static Vector3 operator *(Vector3 value, double factor)
    {
        DomainGuard.Finite(factor, nameof(factor));
        return new Vector3(value.X * factor, value.Y * factor, value.Z * factor);
    }

    public static Vector3 operator *(double factor, Vector3 value)
    {
        return value * factor;
    }

    public static Vector3 operator /(Vector3 value, double divisor)
    {
        DomainGuard.Finite(divisor, nameof(divisor));
        if (divisor == 0)
        {
            throw new DivideByZeroException("A vector cannot be divided by zero.");
        }

        return new Vector3(value.X / divisor, value.Y / divisor, value.Z / divisor);
    }

    public bool Equals(Vector3 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector3 other && Equals(other);
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
        return $"<{X:G17}, {Y:G17}, {Z:G17}>";
    }

    public static bool operator ==(Vector3 left, Vector3 right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Vector3 left, Vector3 right)
    {
        return !left.Equals(right);
    }
}
