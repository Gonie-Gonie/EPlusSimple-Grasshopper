using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Shape;

/// <summary>
/// Default absolute tolerances for model geometry expressed in metres.
/// </summary>
public static class GeometryTolerance
{
    public const double Distance = 1e-7;

    public const double Planarity = 1e-7;

    public const double Area = 1e-10;

    internal static void RequirePositive(double tolerance, string parameterName)
    {
        DomainGuard.Positive(tolerance, parameterName);
    }
}
