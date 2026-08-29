using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.InvisibleDragon.Shape;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

internal sealed class OpeningHostResolution
{
    internal OpeningHostResolution(int? faceIndex, IReadOnlyList<Diagnostic> diagnostics)
    {
        FaceIndex = faceIndex;
        Diagnostics = diagnostics;
    }

    internal int? FaceIndex { get; }

    internal IReadOnlyList<Diagnostic> Diagnostics { get; }

    internal bool IsSuccess => FaceIndex.HasValue
        && Diagnostics.All(item => !item.IsFailure);
}

/// <summary>
/// Resolves an opening to exactly one coplanar containing face. This deliberately
/// rejects arbitrary projection onto a merely parallel face: a wire into a Zone
/// expresses ownership, while geometry still has to identify an unambiguous host.
/// </summary>
internal static class OpeningHostResolver
{
    internal static OpeningHostResolution Resolve(
        Brep zone,
        Curve opening,
        RhinoGeometryContext context,
        EntityId? openingId = null)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(context);
#else
        if (zone is null)
        {
            throw new ArgumentNullException(nameof(zone));
        }

        if (opening is null)
        {
            throw new ArgumentNullException(nameof(opening));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }
#endif

        if (!opening.IsValid || !opening.IsClosed || !opening.TryGetPolyline(out Polyline polyline))
        {
            return Failure(
                "SD.GH.OPENING_BOUNDARY_UNSUPPORTED",
                "An opening must be a valid closed polygonal curve.",
                openingId,
                "Convert the opening boundary to a closed polyline before connecting it to SD Opening.");
        }

        if (!opening.TryGetPlane(out Plane openingPlane, context.SourceAbsoluteTolerance))
        {
            return Failure(
                "SD.GH.OPENING_BOUNDARY_NON_PLANAR",
                "An opening boundary is not planar within the Rhino document tolerance.",
                openingId,
                "Planarize the opening curve on its intended host surface.");
        }

        var candidates = new List<int>();
        double normalThreshold = Math.Cos(context.AngleToleranceRadians);
        for (int faceIndex = 0; faceIndex < zone.Faces.Count; faceIndex++)
        {
            BrepFace face = zone.Faces[faceIndex];
            if (!face.IsValid
                || !face.TryGetPlane(out Plane facePlane, context.SourceAbsoluteTolerance))
            {
                continue;
            }

            double alignment = Math.Abs(openingPlane.Normal * facePlane.Normal);
            if (alignment < normalThreshold
                || polyline.Any(point => Math.Abs(facePlane.DistanceTo(point)) > context.SourceAbsoluteTolerance))
            {
                continue;
            }

            try
            {
                using Curve projected = RhinoPolygonConverter.ProjectOpeningToFacePlane(
                    opening,
                    face,
                    context);
                PlanarPolygon openingPolygon = RhinoPolygonConverter.FromClosedCurve(projected, context);
                RhinoPolygonExtraction facePolygon = RhinoPolygonConverter.FromBrepFace(face, context);
                if (facePolygon.OuterLoop.Contains(openingPolygon, context.ModelToleranceMetres))
                {
                    candidates.Add(faceIndex);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is NotSupportedException)
            {
                // Unsupported faces are not candidates. The collective zone extractor
                // will report its own face-level diagnostics after host assignment.
            }
        }

        if (candidates.Count == 1)
        {
            return new OpeningHostResolution(candidates[0], Array.Empty<Diagnostic>());
        }

        return candidates.Count == 0
            ? Failure(
                "SD.GH.OPENING_HOST_NOT_FOUND",
                "The opening is not coplanar with and contained by a supported face of its connected Zone Brep.",
                openingId,
                "Move the opening onto exactly one planar Zone face and keep it inside that face boundary.")
            : Failure(
                "SD.GH.OPENING_HOST_AMBIGUOUS",
                "The opening matches more than one face of its connected Zone Brep.",
                openingId,
                "Remove coincident duplicate faces or move the opening so exactly one host face contains it.");
    }

    private static OpeningHostResolution Failure(
        string code,
        string message,
        EntityId? openingId,
        string suggestedAction)
    {
        return new OpeningHostResolution(
            null,
            new[]
            {
                new Diagnostic(
                    code,
                    DiagnosticSeverity.Error,
                    message,
                    openingId,
                    suggestedAction: suggestedAction),
            });
    }
}
