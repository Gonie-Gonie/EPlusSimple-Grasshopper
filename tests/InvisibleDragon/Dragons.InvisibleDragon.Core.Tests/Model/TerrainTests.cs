using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class TerrainTests
{
    public static TheoryData<Terrain, string> Members => new()
    {
        { Terrain.Country, "Country" },
        { Terrain.Suburbs, "Suburbs" },
        { Terrain.City, "City" },
        { Terrain.Ocean, "Ocean" },
        { Terrain.Urban, "Urban" },
    };

    [Fact]
    public void DeclaresStableTypedMemberOrderAndValues()
    {
        Assert.True(typeof(Terrain).IsEnum);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Terrain)));
        Assert.Equal(
            new[]
            {
                Terrain.Country,
                Terrain.Suburbs,
                Terrain.City,
                Terrain.Ocean,
                Terrain.Urban,
            },
            Enum.GetValues<Terrain>());
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, Enum.GetValues<Terrain>().Select(item => (int)item));
    }

    [Fact]
    public void EnergyModelDefaultsToSuburbs()
    {
        var model = new EnergyModel("Default terrain", Array.Empty<Zone>());

        Assert.Equal(Terrain.Suburbs, model.Terrain);
        Assert.Equal("Suburbs", Assert.Single(model.ToIdfDocument()["Building"])[2]);
    }

    [Theory]
    [MemberData(nameof(Members))]
    public void EveryMemberEmitsItsEnergyPlusToken(Terrain terrain, string expectedToken)
    {
        var model = new EnergyModel(
            "Terrain token",
            Array.Empty<Zone>(),
            terrain: terrain);

        IdfObject building = Assert.Single(model.ToIdfDocument()["Building"]);

        Assert.Equal(terrain, model.Terrain);
        Assert.Equal(expectedToken, terrain.ToString());
        Assert.Equal(expectedToken, building[2]);
    }

    [Fact]
    public void RejectsUndefinedTerrainValue()
    {
        Terrain undefined = (Terrain)int.MaxValue;

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EnergyModel(
                "Invalid terrain",
                Array.Empty<Zone>(),
                terrain: undefined));

        Assert.Equal("terrain", exception.ParamName);
    }
}
