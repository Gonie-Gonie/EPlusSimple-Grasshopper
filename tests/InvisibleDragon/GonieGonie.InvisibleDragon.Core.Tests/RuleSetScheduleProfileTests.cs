using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Profile;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests;

public sealed class RuleSetScheduleProfileTests
{
    [Fact]
    public void RuleSetUsesDefaultsOverridesAndWeekendHolidayFallback()
    {
        DaySchedule weekday = DaySchedule.Constant("Weekday", 1, ScheduleType.OnOff);
        DaySchedule weekend = DaySchedule.Constant("Weekend", 0, ScheduleType.OnOff);
        DaySchedule friday = DaySchedule.Constant("Friday", 0, ScheduleType.OnOff);
        var ruleSet = new RuleSet(
            "Work week",
            weekday,
            weekend,
            friday: friday,
            type: ScheduleType.OnOff);

        Assert.Same(weekday, ruleSet.GetDaySchedule(DayOfWeek.Monday));
        Assert.Same(friday, ruleSet.GetDaySchedule(DayOfWeek.Friday));
        Assert.Same(weekend, ruleSet.GetDaySchedule(DayOfWeek.Saturday));
        Assert.Same(weekend, ruleSet.GetDaySchedule(DayOfWeek.Monday, true));
    }

    [Fact]
    public void RuleSetRejectsMixedDayScheduleTypes()
    {
        DaySchedule fraction = DaySchedule.Constant("Fraction", 0.5, ScheduleType.Fraction);
        DaySchedule real = DaySchedule.Constant("Real", 1, ScheduleType.Real);

        Assert.Throws<ArgumentException>(() => new RuleSet("Mixed", fraction, real));
    }

    [Fact]
    public void RuleSetOperationsRespectResolvedOverrides()
    {
        DaySchedule one = DaySchedule.Constant("One", 1);
        DaySchedule two = DaySchedule.Constant("Two", 2);
        DaySchedule mondayTen = DaySchedule.Constant("Monday ten", 10);
        var left = new RuleSet("Left", one, one, monday: mondayTen);
        var right = new RuleSet("Right", two, two);

        RuleSet sum = left.Add(right);

        Assert.Equal(12, sum.GetDaySchedule(DayOfWeek.Monday)[0]);
        Assert.Equal(3, sum.GetDaySchedule(DayOfWeek.Tuesday)[0]);
    }

    [Fact]
    public void AnnualConstantHas365DaysAndCalendarIndexing()
    {
        Schedule schedule = Schedule.Constant("Annual", 1, ScheduleType.OnOff);

        Assert.Equal(365, schedule.Count);
        Assert.Equal(1, schedule[new DateTime(2030, 8, 24)].GetDaySchedule(DayOfWeek.Monday)[0]);
        Assert.Equal(8760, schedule.IntegralHours);
        Assert.Equal(1, schedule.Average);
    }

    [Fact]
    public void CompactAnnualScheduleUsesInclusivePeriodsAndZeroFillsGaps()
    {
        RuleSet winter = RuleSet.Constant("Winter", 20, ScheduleType.Temperature);
        RuleSet summer = RuleSet.Constant("Summer", 24, ScheduleType.Temperature);
        Schedule schedule = Schedule.FromCompact(
            "Setpoints",
            new[]
            {
                new SchedulePeriod(new DateTime(2026, 1, 1), new DateTime(2026, 4, 30), winter),
                new SchedulePeriod(new DateTime(2026, 10, 1), new DateTime(2026, 12, 31), summer),
            });

        Assert.Equal(20, schedule[new DateTime(2026, 4, 30)].Weekdays[0]);
        Assert.Equal(0, schedule[new DateTime(2026, 5, 1)].Weekdays[0]);
        Assert.Equal(24, schedule[new DateTime(2026, 10, 1)].Weekdays[0]);
    }

    [Fact]
    public void CompactAnnualScheduleRejectsOverlappingPeriods()
    {
        RuleSet value = RuleSet.Constant("Value", 1);

        Assert.Throws<ArgumentException>(() => Schedule.FromCompact(
            "Overlap",
            new[]
            {
                new SchedulePeriod(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), value),
                new SchedulePeriod(new DateTime(2026, 2, 1), new DateTime(2026, 3, 1), value),
            }));
    }

    [Fact]
    public void ApplyReturnsChangedCopyAndCompactizeRoundTripsPeriods()
    {
        Schedule original = Schedule.Constant("Annual", 0);
        RuleSet occupied = RuleSet.Constant("Occupied", 1);

        Schedule changed = original.Apply(
            occupied,
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 31),
            "Changed");

        Assert.Equal(0, original[new DateTime(2026, 7, 1)].Weekdays[0]);
        Assert.Equal(1, changed[new DateTime(2026, 7, 1)].Weekdays[0]);
        Assert.Equal(3, changed.Compactize().Count);
    }

    [Fact]
    public void AnnualScheduleRejectsLeapDayIndex()
    {
        Schedule schedule = Schedule.Constant("Annual", 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => schedule[new DateTime(2028, 2, 29)]);
    }

    [Fact]
    public void ProfileRequiresSemanticScheduleTypes()
    {
        Schedule real = Schedule.Constant("Real", 1, ScheduleType.Real);

        Assert.Throws<ArgumentException>(() => new ZoneProfile(
            new EntityId("PRFL-000001"),
            "Bad",
            heatingSetpoint: real));
    }

    [Fact]
    public void ProfileReportsOverlappingSetpointRangesAsWarning()
    {
        Schedule heating = Schedule.Constant("Heating", 22, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant("Cooling", 20, ScheduleType.Temperature);
        var profile = new ZoneProfile(
            new EntityId("PRFL-000001"),
            "Overlapping",
            heating,
            cooling);

        var validation = profile.Validate();

        Assert.True(validation.IsValid);
        Assert.True(validation.HasWarnings);
        Assert.Equal("INVISIBLEDRAGON.PROFILE.SETPOINT_OVERLAP", validation.Diagnostics.Single().Code);
    }
}
