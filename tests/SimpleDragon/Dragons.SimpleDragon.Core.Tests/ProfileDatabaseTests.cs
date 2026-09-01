namespace Dragons.SimpleDragon.Tests;

public sealed class ProfileDatabaseTests
{
    [Fact]
    public void StandardAndExtendedProfilesAreOrderedAndMatchPythonValues()
    {
        UsageProfileDatabase database = SimpleDragonDatabase.Default.UsageProfiles;

        Assert.Equal(24, database.Items.Count);
        Assert.Equal("주거공간", database.Items[0].Name);
        Assert.Equal("교실(어린이집)", database.Items[23].Name);
        Assert.Equal(UsageProfileSource.Extended, database.Items[23].Source);

        UsageProfile residential = database.Find("주거공간").Require();
        Assert.Equal(0, residential.OccupantStart);
        Assert.Equal(24, residential.OccupantEnd);
        Assert.Equal(24d, residential.OccupiedHours);
        Assert.Equal(1.1d, residential.Ventilation);
        Assert.Equal(84d, residential.DomesticHotWater);
        Assert.Equal(53d, residential.Occupancy);
        Assert.Equal(52d, residential.Equipment);
        Assert.Equal(Enum.GetValues<UsageDay>(), residential.OperatingDays);
    }

    [Fact]
    public void VacationRangesAndWeekdayFlagsMatchPythonParsing()
    {
        UsageProfile classroom = SimpleDragonDatabase.Default.UsageProfiles.Find("교실(초중고)").Require();

        Assert.Equal(3, classroom.Vacations.Count);
        Assert.Equal("01/01", classroom.Vacations[0].Start.ToString());
        Assert.Equal("02/14", classroom.Vacations[0].End.ToString());
        Assert.True(classroom.OperatesOn(UsageDay.Monday));
        Assert.False(classroom.OperatesOn(UsageDay.Saturday));
        Assert.False(classroom.OperatesOn(UsageDay.Holiday));
    }

    [Fact]
    public void HolidayCalendarPreservesKoreanNamesAndDates()
    {
        KoreanHolidayDatabase holidays = SimpleDragonDatabase.Default.Holidays;

        Assert.Equal(21, holidays.Items.Count);
        Assert.Equal(21, holidays.InYear(2026).Count);
        KoreanHoliday marchFirst = Assert.Single(holidays.On(new DateTime(2026, 3, 1)));
        Assert.Equal("삼일절", marchFirst.Name);
        Assert.Equal("삼일절(대체공휴일)", Assert.Single(holidays.On(new DateTime(2026, 3, 2))).Name);
    }

    [Fact]
    public void UnknownProfileReturnsDiagnosticInsteadOfOpaqueFailure()
    {
        LookupResult<UsageProfile> result = SimpleDragonDatabase.Default.UsageProfiles.Find("없는 용도");

        Assert.False(result.Found);
        Assert.Equal("SD.DB.PROFILE_NOT_FOUND", Assert.Single(result.Diagnostics).Code);
        Assert.Throws<KeyNotFoundException>(() => result.Require());
    }
}
