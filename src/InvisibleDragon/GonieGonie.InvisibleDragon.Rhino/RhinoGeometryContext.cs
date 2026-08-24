using Rhino;
using Rhino.Geometry;
using DragonVertex = GonieGonie.InvisibleDragon.Shape.Vertex;

namespace GonieGonie.InvisibleDragon.Rhino;

/// <summary>
/// Captures the Rhino document unit and tolerance contract used at an adapter boundary.
/// </summary>
public sealed class RhinoGeometryContext
{
    public RhinoGeometryContext(
        UnitSystem sourceUnitSystem,
        double sourceAbsoluteTolerance,
        double angleToleranceRadians = RhinoMath.DefaultAngleTolerance)
    {
        if (sourceUnitSystem == UnitSystem.None || sourceUnitSystem == UnitSystem.Unset)
        {
            throw new ArgumentException("A concrete Rhino model unit system is required.", nameof(sourceUnitSystem));
        }

        if (!IsFinitePositive(sourceAbsoluteTolerance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceAbsoluteTolerance),
                sourceAbsoluteTolerance,
                "The Rhino absolute tolerance must be finite and positive.");
        }

        if (!IsFinitePositive(angleToleranceRadians))
        {
            throw new ArgumentOutOfRangeException(
                nameof(angleToleranceRadians),
                angleToleranceRadians,
                "The Rhino angle tolerance must be finite and positive.");
        }

        double scale = RhinoMath.UnitScale(sourceUnitSystem, UnitSystem.Meters);
        if (!IsFinitePositive(scale))
        {
            throw new ArgumentException(
                $"Rhino cannot convert {sourceUnitSystem} to metres.",
                nameof(sourceUnitSystem));
        }

        SourceUnitSystem = sourceUnitSystem;
        SourceAbsoluteTolerance = sourceAbsoluteTolerance;
        AngleToleranceRadians = angleToleranceRadians;
        SourceUnitsToMetres = scale;
        ModelToleranceMetres = sourceAbsoluteTolerance * scale;
    }

    public UnitSystem SourceUnitSystem { get; }

    public double SourceAbsoluteTolerance { get; }

    public double AngleToleranceRadians { get; }

    public double SourceUnitsToMetres { get; }

    public double ModelToleranceMetres { get; }

    public static RhinoGeometryContext FromDocument(RhinoDoc document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return new RhinoGeometryContext(
            document.ModelUnitSystem,
            document.ModelAbsoluteTolerance,
            document.ModelAngleToleranceRadians);
    }

    public double ToMetres(double sourceDistance)
    {
        RequireFinite(sourceDistance, nameof(sourceDistance));
        return sourceDistance * SourceUnitsToMetres;
    }

    public double FromMetres(double metres)
    {
        RequireFinite(metres, nameof(metres));
        return metres / SourceUnitsToMetres;
    }

    public DragonVertex ToDragon(Point3d point)
    {
        if (!point.IsValid)
        {
            throw new ArgumentException("The Rhino point must be valid.", nameof(point));
        }

        return new DragonVertex(
            ToMetres(point.X),
            ToMetres(point.Y),
            ToMetres(point.Z));
    }

    public Point3d ToRhino(DragonVertex vertex)
    {
        return new Point3d(
            FromMetres(vertex.X),
            FromMetres(vertex.Y),
            FromMetres(vertex.Z));
    }

    private static bool IsFinitePositive(double value)
    {
        return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static void RequireFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A finite distance is required.");
        }
    }
}
