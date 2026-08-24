namespace GonieGonie.SimpleDragon.Tests;

public sealed class WeatherDatabaseTests
{
    [Fact]
    public void KoreanStreetAddressResolvesWeatherAndCurrentClimateMetadata()
    {
        LookupResult<WeatherSelection> lookup = SimpleDragonDatabase.Default.Weather.FindByAddress(
            "  서울특별시   종로구 세종대로 1  ",
            new DateTime(2020, 1, 1));
        WeatherSelection result = lookup.Require();

        Assert.Equal(252, SimpleDragonDatabase.Default.Weather.Items.Count);
        Assert.Equal("서울특별시 종로구", result.Metadata.AdministrativeArea);
        Assert.Equal("1111000000", result.Metadata.LegalDistrictCode);
        Assert.Equal("Suburbs", result.Terrain);
        Assert.Equal("중부2", result.ClimateRegion);
        Assert.Equal(new DateTime(2018, 9, 1), result.ClimateEffectiveDate);
        Assert.Equal("국립중앙박물관", result.WeatherLocation);
        Assert.Equal("KOR_SO_Seoul.WS.471080_TMYx.2009-2023.epw", result.EpwFileName);
        Assert.Equal(37.59491848d, result.Metadata.AdministrativeLatitude, 8);
        Assert.Equal(126.9773205d, result.Metadata.AdministrativeLongitude, 7);
    }

    [Fact]
    public void ClimateLookupSelectsLatestRuleOnOrBeforeVintage()
    {
        WeatherSelection result = SimpleDragonDatabase.Default.Weather.FindByAddress(
            "서울특별시 종로구",
            new DateTime(2005, 5, 1)).Require();

        Assert.Equal("중부1", result.ClimateRegion);
        Assert.Equal(new DateTime(2001, 1, 17), result.ClimateEffectiveDate);
    }

    [Theory]
    [InlineData(null, "SD.WEATHER.ADDRESS_REQUIRED")]
    [InlineData("용왕국 투명시", "SD.WEATHER.ADDRESS_NOT_FOUND")]
    public void MissingOrUnknownAddressesReturnActionableDiagnostics(string? address, string expectedCode)
    {
        LookupResult<WeatherSelection> result = SimpleDragonDatabase.Default.Weather.FindByAddress(
            address,
            new DateTime(2020, 1, 1));

        Assert.False(result.Found);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
        Assert.NotEmpty(result.Diagnostics[0].SuggestedAction!);
    }
}
