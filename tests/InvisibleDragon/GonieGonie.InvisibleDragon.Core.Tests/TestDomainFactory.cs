using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Shape;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests;

internal static class TestDomainFactory
{
    public static Material Concrete(string name = "Concrete")
    {
        return new Material(name, 1.4, 2200, 880);
    }

    public static OpaqueConstruction WallConstruction(string name = "Wall")
    {
        return new OpaqueConstruction(
            name,
            new[]
            {
                new Layer("Concrete 200 mm", Concrete(), 0.2),
            });
    }

    public static PlanarPolygon Square(
        double size = 1,
        double z = 0,
        bool reverse = false,
        double x = 0,
        double y = 0)
    {
        Vertex[] vertices =
        {
            new(x, y, z),
            new(x + size, y, z),
            new(x + size, y + size, z),
            new(x, y + size, z),
        };

        return new PlanarPolygon(reverse ? vertices.Reverse() : vertices);
    }

    public static Surface Surface(
        string id,
        string name,
        PlanarPolygon? polygon = null,
        SurfaceType type = SurfaceType.Wall,
        SurfaceBoundary? boundary = null,
        IEnumerable<IOpening>? openings = null)
    {
        return new Surface(
            new EntityId(id),
            name,
            type,
            WallConstruction(),
            boundary ?? SurfaceBoundary.Outdoors,
            polygon ?? Square(),
            openings);
    }

    public static ZoneProfile EmptyProfile(string id = "PRFL-000001")
    {
        return new ZoneProfile(new EntityId(id), "Empty");
    }
}
