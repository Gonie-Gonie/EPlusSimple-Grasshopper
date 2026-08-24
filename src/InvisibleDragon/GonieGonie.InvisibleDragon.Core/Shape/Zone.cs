using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Internal;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Shape;

/// <summary>
/// A thermal zone composed of explicit polygon surfaces; HVAC equipment is intentionally outside this type.
/// </summary>
public sealed class Zone : IEquatable<Zone>
{
    public Zone(
        EntityId id,
        string name,
        IEnumerable<Surface> surfaces,
        ZoneProfile profile,
        double infiltrationAirChangesPerHour = 0,
        double lightingPowerDensityWattsPerSquareMetre = 0,
        double outdoorAirFlowCubicMetresPerSecond = 0)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = DomainGuard.RequiredText(name, nameof(name));
        Surface[] copy = DomainGuard.CopyRequired(surfaces, nameof(surfaces));
        Surfaces = new ReadOnlyCollection<Surface>(copy);
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        InfiltrationAirChangesPerHour = DomainGuard.NonNegative(
            infiltrationAirChangesPerHour,
            nameof(infiltrationAirChangesPerHour));
        LightingPowerDensityWattsPerSquareMetre = DomainGuard.NonNegative(
            lightingPowerDensityWattsPerSquareMetre,
            nameof(lightingPowerDensityWattsPerSquareMetre));
        OutdoorAirFlowCubicMetresPerSecond = DomainGuard.NonNegative(
            outdoorAirFlowCubicMetresPerSecond,
            nameof(outdoorAirFlowCubicMetresPerSecond));
    }

    public EntityId Id { get; }

    public string Name { get; }

    public IReadOnlyList<Surface> Surfaces { get; }

    public ZoneProfile Profile { get; }

    public double InfiltrationAirChangesPerHour { get; }

    public double LightingPowerDensityWattsPerSquareMetre { get; }

    public double OutdoorAirFlowCubicMetresPerSecond { get; }

    public IReadOnlyList<Surface> FloorSurfaces =>
        new ReadOnlyCollection<Surface>(Surfaces.Where(surface => surface.Type == SurfaceType.Floor).ToArray());

    public double FloorArea => FloorSurfaces.Sum(surface => surface.GrossArea);

    public ValidationResult Validate(double tolerance = GeometryTolerance.Distance)
    {
        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        List<Diagnostic> diagnostics = new();
        if (Surfaces.Count == 0)
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.ZONE.NO_SURFACES",
                $"Zone '{Name}' has no building surfaces.",
                Id,
                "Supply the closed boundary surfaces of the zone."));
        }

        HashSet<EntityId> ids = new();
        foreach (Surface surface in Surfaces)
        {
            if (!ids.Add(surface.Id))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.ZONE.DUPLICATE_SURFACE_ID",
                    $"Zone '{Name}' contains duplicate surface identifier '{surface.Id}'.",
                    surface.Id,
                    "Assign a stable unique identifier to every surface."));
            }

            diagnostics.AddRange(surface.Validate(tolerance).Diagnostics);
        }

        Dictionary<EntityId, Surface> byId = Surfaces
            .GroupBy(surface => surface.Id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (Surface surface in Surfaces.Where(
            item => item.Boundary.Condition == SurfaceBoundaryCondition.Zone))
        {
            EntityId adjacentId = surface.Boundary.AdjacentSurfaceId!;
            if (!byId.TryGetValue(adjacentId, out Surface? adjacent))
            {
                diagnostics.Add(new Diagnostic(
                    "INVISIBLEDRAGON.ZONE.ADJACENT_SURFACE_OUTSIDE_SCOPE",
                    DiagnosticSeverity.Warning,
                    $"Surface '{surface.Name}' references adjacent surface '{adjacentId}' outside this zone.",
                    surface.Id,
                    surface.Provenance,
                    "Validate the reciprocal reference when the complete multi-zone model is assembled."));
                continue;
            }

            if (adjacent.Boundary.AdjacentSurfaceId is null
                || !adjacent.Boundary.AdjacentSurfaceId.Equals(surface.Id))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.ZONE.ADJACENCY_NOT_RECIPROCAL",
                    $"Adjacency between '{surface.Name}' and '{adjacent.Name}' is not reciprocal.",
                    surface.Id,
                    "Match the surfaces using SurfaceAdjacency.Match."));
            }

            diagnostics.AddRange(SurfaceAdjacency.ValidateMatch(surface, adjacent, tolerance).Diagnostics);
        }

        if (FloorSurfaces.Count == 0)
        {
            diagnostics.Add(new Diagnostic(
                "INVISIBLEDRAGON.ZONE.NO_FLOOR",
                DiagnosticSeverity.Warning,
                $"Zone '{Name}' has no floor surface.",
                Id,
                suggestedAction: "Classify at least one horizontal lower boundary as Floor when appropriate."));
        }

        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    public bool Equals(Zone? other)
    {
        return other is not null
            && Equals(Id, other.Id)
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && Surfaces.SequenceEqual(other.Surfaces)
            && Equals(Profile, other.Profile)
            && InfiltrationAirChangesPerHour.Equals(other.InfiltrationAirChangesPerHour)
            && LightingPowerDensityWattsPerSquareMetre.Equals(other.LightingPowerDensityWattsPerSquareMetre)
            && OutdoorAirFlowCubicMetresPerSecond.Equals(other.OutdoorAirFlowCubicMetresPerSecond);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Zone);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Id.GetHashCode();
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 397) ^ Profile.GetHashCode();
            foreach (Surface surface in Surfaces)
            {
                hash = (hash * 397) ^ surface.GetHashCode();
            }

            return hash;
        }
    }

    private static Diagnostic Error(string code, string message, EntityId objectId, string suggestedAction)
    {
        return new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            message,
            objectId,
            suggestedAction: suggestedAction);
    }
}
