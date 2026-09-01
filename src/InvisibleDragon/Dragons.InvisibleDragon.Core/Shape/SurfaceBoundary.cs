using Dragons.BuildingEnergy.Contracts;

namespace Dragons.InvisibleDragon.Shape;

public enum SurfaceType
{
    Wall,
    Ceiling,
    Floor,
}

public enum SurfaceBoundaryCondition
{
    Outdoors,
    Ground,
    Adiabatic,
    Zone,
}

/// <summary>
/// An outside boundary condition and, for interzone surfaces, its adjacent surface identifier.
/// </summary>
public sealed record SurfaceBoundary
{
    public SurfaceBoundary(SurfaceBoundaryCondition condition, EntityId? adjacentSurfaceId = null)
    {
        if (!Enum.IsDefined(typeof(SurfaceBoundaryCondition), condition))
        {
            throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown surface boundary condition.");
        }

        if (condition == SurfaceBoundaryCondition.Zone && adjacentSurfaceId is null)
        {
            throw new ArgumentException("A zone boundary requires an adjacent surface identifier.", nameof(adjacentSurfaceId));
        }

        if (condition != SurfaceBoundaryCondition.Zone && adjacentSurfaceId is not null)
        {
            throw new ArgumentException("Only a zone boundary can reference an adjacent surface.", nameof(adjacentSurfaceId));
        }

        Condition = condition;
        AdjacentSurfaceId = adjacentSurfaceId;
    }

    public SurfaceBoundaryCondition Condition { get; }

    public EntityId? AdjacentSurfaceId { get; }

    public static SurfaceBoundary Outdoors { get; } = new(SurfaceBoundaryCondition.Outdoors);

    public static SurfaceBoundary Ground { get; } = new(SurfaceBoundaryCondition.Ground);

    public static SurfaceBoundary Adiabatic { get; } = new(SurfaceBoundaryCondition.Adiabatic);

    public static SurfaceBoundary AdjacentTo(EntityId surfaceId)
    {
        return new SurfaceBoundary(
            SurfaceBoundaryCondition.Zone,
            surfaceId ?? throw new ArgumentNullException(nameof(surfaceId)));
    }
}
