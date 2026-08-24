using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

public enum FenestrationType
{
    Window,
    Door,
    GlassDoor,
}

public enum BlindType
{
    Shade,
    Venetian,
}

/// <summary>
/// Rhino-free opening geometry represented by area and a construction reference.
/// </summary>
public sealed class Fenestration
{
    public Fenestration(
        string name,
        FenestrationType type,
        double area,
        string constructionId,
        FenestrationConstruction? construction = null,
        BlindType? blind = null,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        if (!Enum.IsDefined(typeof(FenestrationType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown fenestration type.");
        }

        Type = type;
        Area = DomainSupport.FinitePositive(area, nameof(area));
        ConstructionId = DomainSupport.RequiredText(constructionId, nameof(constructionId));
        Construction = construction;
        if (construction is not null
            && type == FenestrationType.Door
            && construction.IsTransparent)
        {
            throw new ArgumentException("A door requires an opaque fenestration construction.", nameof(construction));
        }

        if (construction is not null
            && type != FenestrationType.Door
            && !construction.IsTransparent)
        {
            throw new ArgumentException("A window or glass door requires a transparent construction.", nameof(construction));
        }

        if (blind.HasValue && type == FenestrationType.Door)
        {
            throw new ArgumentException("An opaque door cannot have a window blind.", nameof(blind));
        }

        Blind = blind;
        Id = id ?? DeterministicDomainId.Create(
            "FNST",
            Name,
            Type,
            Area,
            ConstructionId,
            Blind);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public FenestrationType Type { get; }

    public double Area { get; }

    public string ConstructionId { get; }

    public FenestrationConstruction? Construction { get; }

    public BlindType? Blind { get; }
}
