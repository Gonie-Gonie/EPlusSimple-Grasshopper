using System.Collections.ObjectModel;
using Rhino.Geometry;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Shape;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;

namespace GonieGonie.InvisibleDragon.Rhino;

/// <summary>
/// Conversion helpers for complete InvisibleDragon surfaces and zones.
/// </summary>
public static class RhinoSurfaceAdapter
{
    public static Brep ToBrep(DragonSurface surface, RhinoGeometryContext context)
    {
        if (surface is null)
        {
            throw new ArgumentNullException(nameof(surface));
        }

        return RhinoPolygonConverter.ToBrep(
            surface.Polygon,
            surface.Openings.Select(opening => opening.Polygon),
            context);
    }

    public static IReadOnlyList<Brep> ToBreps(Zone zone, RhinoGeometryContext context)
    {
        if (zone is null)
        {
            throw new ArgumentNullException(nameof(zone));
        }

        var converted = new List<Brep>(zone.Surfaces.Count);
        bool completed = false;
        try
        {
            foreach (DragonSurface surface in zone.Surfaces)
            {
                converted.Add(ToBrep(surface, context));
            }

            var result = new ReadOnlyCollection<Brep>(converted);
            completed = true;
            return result;
        }
        finally
        {
            if (!completed)
            {
                foreach (Brep brep in converted)
                {
                    brep.Dispose();
                }
            }
        }
    }

    public static GeometryProvenance CreateProvenance(
        RhinoPolygonExtraction extraction,
        Guid? rhinoObjectId = null,
        int? brepFaceIndex = null,
        string? grasshopperPath = null,
        int? grasshopperIndex = null)
    {
        if (extraction is null)
        {
            throw new ArgumentNullException(nameof(extraction));
        }

        return new GeometryProvenance(
            rhinoObjectId,
            brepFaceIndex,
            extraction.GeometryFingerprint,
            grasshopperPath,
            grasshopperIndex);
    }
}
