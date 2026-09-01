using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Shape;

public sealed record SurfaceAdjacencyPair(Surface First, Surface Second);

/// <summary>
/// Geometry-based interzone surface matching without RhinoCommon dependencies.
/// </summary>
public static class SurfaceAdjacency
{
    public static ValidationResult ValidateMatch(
        Surface first,
        Surface second,
        double tolerance = GeometryTolerance.Distance)
    {
        DomainGuard.NotNull(first, nameof(first));
        DomainGuard.NotNull(second, nameof(second));

        GeometryTolerance.RequirePositive(tolerance, nameof(tolerance));
        List<Diagnostic> diagnostics = new();
        if (first.Id.Equals(second.Id))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.ADJACENCY.SAME_SURFACE",
                "A surface cannot be adjacent to itself.",
                first.Id,
                first.Provenance,
                "Choose two distinct surfaces."));
        }

        if (!first.Polygon.IsGeometricallyEquivalentTo(second.Polygon, true, tolerance))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.ADJACENCY.GEOMETRY_MISMATCH",
                $"Surfaces '{first.Name}' and '{second.Name}' do not have coincident polygon loops.",
                first.Id,
                first.Provenance,
                "Use coincident polygons with the same vertices for an interzone boundary."));
        }

        if (first.Openings.Count != second.Openings.Count)
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.ADJACENCY.OPENING_COUNT_MISMATCH",
                "Adjacent surfaces must have matching opening counts.",
                first.Id,
                first.Provenance,
                "Mirror every interzone opening on the adjacent surface."));
        }
        else
        {
            for (int index = 0; index < first.Openings.Count; index++)
            {
                IOpening left = first.Openings[index];
                bool match = second.Openings.Any(
                    right => left.Type == right.Type
                        && left.Polygon.IsGeometricallyEquivalentTo(right.Polygon, true, tolerance));
                if (!match)
                {
                    diagnostics.Add(Error(
                        "INVISIBLEDRAGON.ADJACENCY.OPENING_GEOMETRY_MISMATCH",
                        $"Opening '{left.Name}' has no coincident peer on adjacent surface '{second.Name}'.",
                        left.Id,
                        left.Provenance,
                        "Mirror the opening polygon and opening type on both surfaces."));
                }
            }
        }

        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    public static SurfaceAdjacencyPair Match(
        Surface first,
        Surface second,
        double tolerance = GeometryTolerance.Distance)
    {
        ValidationResult validation = ValidateMatch(first, second, tolerance);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Diagnostics[0].Message);
        }

        Surface alignedSecond = first.Normal.Dot(second.Normal) > 0
            ? second.WithPolygon(second.Polygon.Reverse())
            : second;
        Surface matchedFirst = first.WithBoundary(SurfaceBoundary.AdjacentTo(alignedSecond.Id));
        Surface matchedSecond = alignedSecond.WithBoundary(SurfaceBoundary.AdjacentTo(first.Id));
        return new SurfaceAdjacencyPair(matchedFirst, matchedSecond);
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
