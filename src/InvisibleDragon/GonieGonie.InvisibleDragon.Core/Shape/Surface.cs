using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Shape;

/// <summary>
/// A Rhino-independent building surface with its exact polygon and polygonal openings.
/// </summary>
public sealed class Surface : IEquatable<Surface>
{
    public Surface(
        EntityId id,
        string name,
        SurfaceType type,
        ISurfaceConstruction construction,
        SurfaceBoundary boundary,
        PlanarPolygon polygon,
        IEnumerable<IOpening>? openings = null,
        GeometryProvenance? provenance = null)
    {
        if (!Enum.IsDefined(typeof(SurfaceType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown surface type.");
        }

        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = DomainGuard.RequiredText(name, nameof(name));
        Type = type;
        Construction = construction ?? throw new ArgumentNullException(nameof(construction));
        Boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
        IOpening[] openingCopy = openings is null
            ? Array.Empty<IOpening>()
            : DomainGuard.CopyRequired(openings, nameof(openings));
        Openings = new ReadOnlyCollection<IOpening>(openingCopy);
        Provenance = provenance;
    }

    public EntityId Id { get; }

    public string Name { get; }

    public SurfaceType Type { get; }

    public ISurfaceConstruction Construction { get; }

    public SurfaceBoundary Boundary { get; }

    public PlanarPolygon Polygon { get; }

    public IReadOnlyList<IOpening> Openings { get; }

    public GeometryProvenance? Provenance { get; }

    public double GrossArea => Polygon.Area;

    public double OpeningArea => Openings.Sum(opening => opening.Polygon.Area);

    public double NetArea => GrossArea - OpeningArea;

    public Vector3 Normal => Polygon.Normal;

    public Vertex Center => Polygon.Centroid;

    public double Height => Polygon.Height;

    public IReadOnlyList<Window> Windows => new ReadOnlyCollection<Window>(Openings.OfType<Window>().ToArray());

    public IReadOnlyList<Door> Doors => new ReadOnlyCollection<Door>(Openings.OfType<Door>().ToArray());

    public PlanarPolygon CreateCenteredSubsurface(double targetArea)
    {
        if (double.IsNaN(targetArea) || double.IsInfinity(targetArea) || targetArea <= 0 || targetArea >= GrossArea)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetArea),
                targetArea,
                "A centered subsurface area must be positive and smaller than its host surface.");
        }

        return Polygon.ScaleAboutCentroid(Math.Sqrt(targetArea / GrossArea));
    }

    public Surface WithBoundary(SurfaceBoundary boundary)
    {
        return new Surface(Id, Name, Type, Construction, boundary, Polygon, Openings, Provenance);
    }

    public Surface WithPolygon(PlanarPolygon polygon)
    {
        return new Surface(Id, Name, Type, Construction, Boundary, polygon, Openings, Provenance);
    }

    public Surface WithOpenings(IEnumerable<IOpening> openings)
    {
        return new Surface(Id, Name, Type, Construction, Boundary, Polygon, openings, Provenance);
    }

    public ValidationResult Validate(double tolerance = GeometryTolerance.Distance)
    {
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        List<Diagnostic> diagnostics = new();
        HashSet<EntityId> ids = new();

        foreach (IOpening opening in Openings)
        {
            if (!ids.Add(opening.Id))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.SURFACE.DUPLICATE_OPENING_ID",
                    $"Surface '{Name}' contains duplicate opening identifier '{opening.Id}'.",
                    opening.Id,
                    opening.Provenance,
                    "Assign a stable unique identifier to every opening."));
            }

            if (!Polygon.Contains(opening.Polygon, tolerance))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.SURFACE.OPENING_OUTSIDE_HOST",
                    $"Opening '{opening.Name}' is not fully contained in surface '{Name}'.",
                    opening.Id,
                    opening.Provenance,
                    "Move or trim the opening so its polygon lies inside the host polygon."));
            }

            if (opening.Polygon.Area >= GrossArea - GeometryTolerance.Area)
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.SURFACE.OPENING_TOO_LARGE",
                    $"Opening '{opening.Name}' consumes all or more of its host surface area.",
                    opening.Id,
                    opening.Provenance,
                    "Reduce the opening polygon area."));
            }
        }

        for (int first = 0; first < Openings.Count; first++)
        {
            for (int second = first + 1; second < Openings.Count; second++)
            {
                if (Openings[first].Polygon.IntersectsInterior(Openings[second].Polygon, tolerance))
                {
                    diagnostics.Add(Error(
                        "INVISIBLEDRAGON.SURFACE.OPENINGS_OVERLAP",
                        $"Openings '{Openings[first].Name}' and '{Openings[second].Name}' overlap.",
                        Id,
                        Provenance,
                        "Separate or merge the opening polygons."));
                }
            }
        }

        if (NetArea <= GeometryTolerance.Area)
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.SURFACE.NON_POSITIVE_NET_AREA",
                $"Surface '{Name}' has no positive opaque net area.",
                Id,
                Provenance,
                "Reduce or remove openings."));
        }

        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    public bool Equals(Surface? other)
    {
        return other is not null
            && Equals(Id, other.Id)
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && Type == other.Type
            && Equals(Construction, other.Construction)
            && Equals(Boundary, other.Boundary)
            && Equals(Polygon, other.Polygon)
            && Openings.SequenceEqual(other.Openings)
            && Equals(Provenance, other.Provenance);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Surface);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Id.GetHashCode();
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 397) ^ Type.GetHashCode();
            hash = (hash * 397) ^ Construction.GetHashCode();
            hash = (hash * 397) ^ Boundary.GetHashCode();
            hash = (hash * 397) ^ Polygon.GetHashCode();
            foreach (IOpening opening in Openings)
            {
                hash = (hash * 397) ^ opening.GetHashCode();
            }

            return hash;
        }
    }

    private static Diagnostic Error(
        string code,
        string message,
        EntityId objectId,
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
}
