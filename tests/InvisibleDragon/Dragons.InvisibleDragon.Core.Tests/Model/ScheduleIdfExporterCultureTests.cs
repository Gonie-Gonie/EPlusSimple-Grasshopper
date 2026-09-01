using System.Globalization;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class ScheduleIdfExporterCultureTests
{
    [Fact]
    public void AggregateScheduleDatesUseEnergyPlusSlashesAcrossCultures()
    {
        string[] korean = ExportThroughFields("ko-KR");
        string[] english = ExportThroughFields("en-US");
        string[] arabic = ExportThroughFields("ar-SA");

        Assert.NotEmpty(korean);
        Assert.Equal(english, korean);
        Assert.Equal(english, arabic);
        Assert.All(korean, value => Assert.Equal("Through: 12/31", value));
    }

    private static string[] ExportThroughFields(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            Zone zone = EnergyModelFixtureMatrixTests.CreateZone(
                "SCHEDULE-CULTURE-ZONE",
                "Schedule Culture Zone");
            IdfDocument document = new EnergyModel(
                "Schedule culture model",
                new[] { zone }).ToIdfDocument();

            return document["Schedule:Compact"]
                .SelectMany(item => item.Fields)
                .Select(field => field.Value)
                .Where(value => value.StartsWith("Through: ", StringComparison.Ordinal))
                .ToArray();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
