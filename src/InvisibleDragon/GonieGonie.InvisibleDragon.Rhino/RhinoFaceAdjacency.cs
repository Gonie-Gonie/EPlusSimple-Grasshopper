using System.Collections.ObjectModel;
using Rhino.Geometry;

namespace GonieGonie.InvisibleDragon.Rhino;

public sealed class RhinoFaceAdjacencyMatch
{
    public RhinoFaceAdjacencyMatch(int firstFaceIndex, int secondFaceIndex)
    {
        FirstFaceIndex = firstFaceIndex;
        SecondFaceIndex = secondFaceIndex;
    }

    public int FirstFaceIndex { get; }

    public int SecondFaceIndex { get; }
}

/// <summary>
/// Finds coincident planar polygon faces without retaining Rhino geometry in the Core model.
/// </summary>
public static class RhinoFaceAdjacency
{
    public static IReadOnlyList<RhinoFaceAdjacencyMatch> FindCoincidentFaces(
        IReadOnlyList<BrepFace> faces,
        RhinoGeometryContext context)
    {
        if (faces is null)
        {
            throw new ArgumentNullException(nameof(faces));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        RhinoPolygonExtraction[] polygons = faces
            .Select(face => RhinoPolygonConverter.FromBrepFace(
                face ?? throw new ArgumentException("A face collection cannot contain null.", nameof(faces)),
                context))
            .ToArray();
        var matches = new List<RhinoFaceAdjacencyMatch>();
        for (int first = 0; first < polygons.Length; first++)
        {
            for (int second = first + 1; second < polygons.Length; second++)
            {
                if (polygons[first].OuterLoop.IsGeometricallyEquivalentTo(
                    polygons[second].OuterLoop,
                    allowReversedWinding: true,
                    tolerance: context.ModelToleranceMetres))
                {
                    matches.Add(new RhinoFaceAdjacencyMatch(first, second));
                }
            }
        }

        return new ReadOnlyCollection<RhinoFaceAdjacencyMatch>(matches);
    }
}
