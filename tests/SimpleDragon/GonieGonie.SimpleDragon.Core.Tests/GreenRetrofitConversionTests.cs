using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class GreenRetrofitConversionTests
{
    [Fact]
    public void AshraeFixtureConvertsToValidEnergyModelAndEnergyPlusReadyIdf()
    {
        GreenRetrofitModel source = GrmReader.ReadFile(Fixture("grm")).RequireModel();

        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(source);

        Assert.True(result.Success, Describe(result));
        EnergyModel model = result.RequireEnergyModel();
        Assert.Equal("ASHRAE 140 modified", model.Name);
        Assert.Equal(Terrain.Suburbs, model.Terrain);
        Assert.Single(model.Zones);
        Assert.Equal(6, model.Surfaces.Count);
        Assert.Equal(2, model.Surfaces.Sum(surface => surface.Openings.Count));
        Assert.Equal(48d, model.Zones[0].FloorArea, 8);
        Assert.Equal(0.105d, model.Zones[0].InfiltrationAirChangesPerHour, 8);
        Assert.Equal(4d * 48d / 3600d, model.Zones[0].OutdoorAirFlowCubicMetresPerSecond, 8);

        Assert.Single(model.HvacAssignments);
        SupplyGroup group = model.HvacAssignments[0].Supply;
        Assert.Equal(2, group.Systems.Count);
        Assert.Contains(group.Systems, item => item is AirHandlingUnit);
        Assert.Contains(group.Systems, item => item is RadiantFloor);
        Assert.Empty(model.VentilationAssignments);
        Assert.Empty(model.PhotovoltaicPanels);
        Assert.True(model.Validate().IsValid, Describe(model.Validate().Diagnostics));

        IdfDocument idf = result.ToIdfDocument();
        Assert.Equal("24.2", idf.EnergyPlusVersion);
        Assert.Single(idf["Building"]);
        Assert.Single(idf["Zone"]);
        Assert.Equal(6, idf["BuildingSurface:Detailed"].Count);
        Assert.Equal(2, idf["FenestrationSurface:Detailed"].Count);
        Assert.Single(idf["AirConditioner:VariableRefrigerantFlow"]);
        Assert.Single(idf["Boiler:HotWater"]);
        Assert.Single(idf["ZoneHVAC:LowTemperatureRadiant:VariableFlow"]);

        IdfDocument oracle = IdfParser.ParseFile(ReferenceIdf());
        Assert.Equal(oracle["Building"][0][0], idf["Building"][0][0]);
        Assert.Equal(oracle["Building"][0][2], idf["Building"][0][2]);
        Assert.Equal(
            oracle["Zone"].Select(item => item.Name),
            idf["Zone"].Select(item => item.Name));
        Assert.Equal(
            oracle["BuildingSurface:Detailed"].Select(item => (item.Name, item[1])),
            idf["BuildingSurface:Detailed"].Select(item => (item.Name, item[1])));
        Assert.Equal(
            oracle["Window"].Select(item => item.Name),
            idf["FenestrationSurface:Detailed"].Select(item => item.Name));
        Assert.Equal(oracle["Boiler:HotWater"][0][0], idf["Boiler:HotWater"][0][0]);
        Assert.Equal(oracle["Boiler:HotWater"][0][1], idf["Boiler:HotWater"][0][1]);

        string first = IdfWriter.Write(idf);
        string second = IdfWriter.Write(GreenRetrofitConverter.ToIdfDocument(source));
        Assert.Equal(first, second);
        Assert.Contains("SURF-0x000003", first, StringComparison.Ordinal);
        Assert.Contains("FNST-0x000000", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertedOpeningsRetainAreaAndDoNotOverlap()
    {
        GreenRetrofitModel source = GrmReader.ReadFile(Fixture("grm")).RequireModel();
        EnergyModel converted = GreenRetrofitConverter.Convert(source).RequireEnergyModel();
        DragonSurface wall = Assert.Single(converted.Surfaces, surface => surface.Openings.Count == 2);

        Assert.Equal(21.6d, wall.GrossArea, 8);
        Assert.All(wall.Openings, opening => Assert.Equal(6d, opening.Polygon.Area, 8));
        Assert.False(wall.Openings[0].Polygon.IntersectsInterior(wall.Openings[1].Polygon));
        Assert.True(wall.Validate().IsValid, Describe(wall.Validate().Diagnostics));
    }

    private static string Fixture(string extension)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                extension,
                "ASHRAE 140 modified." + extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SimpleDragon fixture.");
    }

    private static string ReferenceIdf()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "reference",
                "python-0.7.0",
                "ashrae-140-modified.idf");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the pinned Python IDF oracle.");
    }

    private static string Describe(GreenRetrofitConversionResult result)
    {
        return Describe(result.Diagnostics);
    }

    private static string Describe(IEnumerable<GonieGonie.BuildingEnergy.Contracts.Diagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine, diagnostics.Select(item => item.Code + ": " + item.Message));
    }
}
