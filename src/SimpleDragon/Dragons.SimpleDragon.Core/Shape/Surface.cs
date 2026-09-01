using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

public enum SurfaceConstructionReferenceKind
{
    Defined,
    Unknown,
    Open,
    Unresolved,
}

/// <summary>
/// Rhino-free building surface represented by type, boundary, area, and optional azimuth.
/// </summary>
public sealed class Surface
{
    public Surface(
        string name,
        SurfaceType type,
        SurfaceBoundaryCondition boundaryCondition,
        double area,
        double? azimuth,
        string? constructionId,
        SurfaceConstruction? construction,
        IEnumerable<Fenestration>? fenestrations = null,
        double? coolRoofReflectance = null,
        string? adjacentZoneId = null,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        if (!Enum.IsDefined(typeof(SurfaceType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown surface type.");
        }

        if (!Enum.IsDefined(typeof(SurfaceBoundaryCondition), boundaryCondition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundaryCondition),
                boundaryCondition,
                "Unknown surface boundary condition.");
        }

        Type = type;
        BoundaryCondition = boundaryCondition;
        Area = DomainSupport.FinitePositive(area, nameof(area));
        bool needsAzimuth = type == SurfaceType.Wall
            && boundaryCondition == SurfaceBoundaryCondition.Outdoors;
        if (needsAzimuth != azimuth.HasValue)
        {
            throw new ArgumentException(
                needsAzimuth
                    ? "An outdoor wall requires an azimuth."
                    : "Azimuth is only valid for an outdoor wall.",
                nameof(azimuth));
        }

        if (azimuth.HasValue
            && (double.IsNaN(azimuth.Value)
                || double.IsInfinity(azimuth.Value)
                || azimuth.Value < 0d
                || azimuth.Value >= 360d))
        {
            throw new ArgumentOutOfRangeException(nameof(azimuth), azimuth, "Azimuth must be in [0, 360). ");
        }

        Azimuth = azimuth;
        ConstructionId = constructionId?.Trim();
        Construction = construction;
        ConstructionReferenceKind = ResolveConstructionKind(ConstructionId, construction);
        if (ConstructionReferenceKind == SurfaceConstructionReferenceKind.Defined
            && !StringComparer.Ordinal.Equals(ConstructionId, construction!.Id.Value))
        {
            throw new ArgumentException("Construction ID does not match the resolved construction.", nameof(constructionId));
        }

        Fenestration[] openings = fenestrations?.ToArray() ?? Array.Empty<Fenestration>();
        if (openings.Any(item => item is null))
        {
            throw new ArgumentException("A fenestration cannot be null.", nameof(fenestrations));
        }

        if ((boundaryCondition == SurfaceBoundaryCondition.Ground
             || boundaryCondition == SurfaceBoundaryCondition.Adiabatic)
            && openings.Length > 0)
        {
            throw new ArgumentException(
                "Ground and adiabatic surfaces cannot contain fenestrations.",
                nameof(fenestrations));
        }

        Fenestrations = Array.AsReadOnly(openings);
        if (coolRoofReflectance.HasValue
            && (double.IsNaN(coolRoofReflectance.Value)
                || double.IsInfinity(coolRoofReflectance.Value)
                || coolRoofReflectance.Value <= 0d
                || coolRoofReflectance.Value > 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(coolRoofReflectance),
                coolRoofReflectance,
                "Cool-roof reflectance must be in (0, 1].");
        }

        if (coolRoofReflectance.HasValue
            && (type != SurfaceType.Ceiling
                || boundaryCondition != SurfaceBoundaryCondition.Outdoors))
        {
            throw new ArgumentException(
                "Cool-roof reflectance is only valid on an outdoor ceiling.",
                nameof(coolRoofReflectance));
        }

        CoolRoofReflectance = coolRoofReflectance;
        if (boundaryCondition == SurfaceBoundaryCondition.Zone)
        {
            AdjacentZoneId = DomainSupport.RequiredText(adjacentZoneId, nameof(adjacentZoneId));
        }
        else if (!string.IsNullOrWhiteSpace(adjacentZoneId))
        {
            throw new ArgumentException(
                "Adjacent zone ID is only valid for a zone boundary.",
                nameof(adjacentZoneId));
        }

        Id = id ?? DeterministicDomainId.Create(
            "SURF",
            Name,
            Type,
            BoundaryCondition,
            Area,
            Azimuth,
            ConstructionId,
            AdjacentZoneId);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public SurfaceType Type { get; }

    public SurfaceBoundaryCondition BoundaryCondition { get; }

    public double Area { get; }

    public double? Azimuth { get; }

    public string? ConstructionId { get; }

    public SurfaceConstruction? Construction { get; }

    public SurfaceConstructionReferenceKind ConstructionReferenceKind { get; }

    public IReadOnlyList<Fenestration> Fenestrations { get; }

    public double? CoolRoofReflectance { get; }

    public string? AdjacentZoneId { get; }

    public int WindowCount => Fenestrations.Count(
        item => item.Type == FenestrationType.Window || item.Type == FenestrationType.GlassDoor);

    public int DoorCount => Fenestrations.Count(item => item.Type == FenestrationType.Door);

    public Surface Flip()
    {
        SurfaceType flippedType = Type switch
        {
            SurfaceType.Floor => SurfaceType.Ceiling,
            SurfaceType.Ceiling => SurfaceType.Floor,
            _ => Type,
        };
        double? flippedAzimuth = Azimuth.HasValue ? (Azimuth.Value + 180d) % 360d : null;
        return new Surface(
            Name + "_flipped",
            flippedType,
            BoundaryCondition,
            Area,
            flippedAzimuth,
            ConstructionId,
            Construction,
            Fenestrations,
            coolRoofReflectance: null,
            AdjacentZoneId,
            DeterministicDomainId.Create("SURF-FLIPPED", Id.Value));
    }

    private static SurfaceConstructionReferenceKind ResolveConstructionKind(
        string? constructionId,
        SurfaceConstruction? construction)
    {
        if (string.IsNullOrEmpty(constructionId))
        {
            return SurfaceConstructionReferenceKind.Unknown;
        }

        if (StringComparer.Ordinal.Equals(constructionId, "open"))
        {
            return SurfaceConstructionReferenceKind.Open;
        }

        return construction is null
            ? SurfaceConstructionReferenceKind.Unresolved
            : SurfaceConstructionReferenceKind.Defined;
    }
}
