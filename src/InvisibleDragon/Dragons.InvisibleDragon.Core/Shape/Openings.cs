using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Shape;

public enum OpeningType
{
    Window,
    Door,
}

public interface IOpening
{
    EntityId Id { get; }

    string Name { get; }

    OpeningType Type { get; }

    PlanarPolygon Polygon { get; }

    GeometryProvenance? Provenance { get; }
}

/// <summary>
/// A transparent polygonal opening with an optional interior shading device.
/// </summary>
public sealed record Window : IOpening
{
    public Window(
        EntityId id,
        string name,
        Glazing glazing,
        PlanarPolygon polygon,
        IShadingDevice? shading = null,
        GeometryProvenance? provenance = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = DomainGuard.RequiredText(name, nameof(name));
        Glazing = glazing ?? throw new ArgumentNullException(nameof(glazing));
        Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
        Shading = shading;
        Provenance = provenance;
    }

    public EntityId Id { get; }

    public string Name { get; }

    public OpeningType Type => OpeningType.Window;

    public Glazing Glazing { get; }

    public PlanarPolygon Polygon { get; }

    public IShadingDevice? Shading { get; }

    public GeometryProvenance? Provenance { get; }

    public double Area => Polygon.Area;
}

/// <summary>
/// An opaque polygonal door opening.
/// </summary>
public sealed record Door : IOpening
{
    public Door(
        EntityId id,
        string name,
        ISurfaceConstruction construction,
        PlanarPolygon polygon,
        GeometryProvenance? provenance = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = DomainGuard.RequiredText(name, nameof(name));
        Construction = construction ?? throw new ArgumentNullException(nameof(construction));
        Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
        Provenance = provenance;
    }

    public EntityId Id { get; }

    public string Name { get; }

    public OpeningType Type => OpeningType.Door;

    public ISurfaceConstruction Construction { get; }

    public PlanarPolygon Polygon { get; }

    public GeometryProvenance? Provenance { get; }

    public double Area => Polygon.Area;
}
