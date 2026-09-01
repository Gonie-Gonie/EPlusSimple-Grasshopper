using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Profile;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Tests;

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
        Assert.Equal(24, schedule.Average);
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
    public void CompactAnnualScheduleAppliesOverlappingPeriodsInGivenOrder()
    {
        RuleSet early = RuleSet.Constant("Early", 1);
        RuleSet late = RuleSet.Constant("Late", 2);

        Schedule schedule = Schedule.FromCompact(
            "Overlap",
            new[]
            {
                new SchedulePeriod(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), early),
                new SchedulePeriod(new DateTime(2026, 2, 1), new DateTime(2026, 3, 1), late),
            });

        Assert.Equal(1, schedule[new DateTime(2026, 1, 31)].Weekdays[0]);
        Assert.Equal(2, schedule[new DateTime(2026, 2, 1)].Weekdays[0]);
        Assert.Equal(2, schedule[new DateTime(2026, 3, 1)].Weekdays[0]);
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
    public void ProfileAcceptsFractionLightingSchedule()
    {
        Schedule fraction = Schedule.Constant("Lighting fraction", 0.5, ScheduleType.Fraction);

        var profile = new ZoneProfile(
            new EntityId("PRFL-000001"),
            "Fraction lighting",
            lighting: fraction);

        Assert.Same(fraction, profile.Lighting);
    }

    [Fact]
    public void ProfileRejectsUnrelatedLightingScheduleType()
    {
        Schedule real = Schedule.Constant("Lighting real", 1, ScheduleType.Real);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new ZoneProfile(
            new EntityId("PRFL-000001"),
            "Bad lighting",
            lighting: real));

        Assert.Equal("lighting", exception.ParamName);
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

    [Fact]
    public void ProfileToIdfObjectsReturnsEmptyCollectionWhenEverySlotIsNull()
    {
        var profile = new ZoneProfile(new EntityId("PRFL-000001"), "Empty");

        IReadOnlyList<IdfObject> objects = profile.ToIdfObjects();

        Assert.Empty(objects);
    }

    [Fact]
    public void ProfileToIdfObjectsUsesUpstreamSlotOrder()
    {
        Schedule heating = Schedule.Constant("Heating", 20, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant("Cooling", 24, ScheduleType.Temperature);
        Schedule availability = Schedule.Constant("Availability", 1, ScheduleType.OnOff);
        Schedule occupant = Schedule.Constant("Occupant", 0.1, ScheduleType.Real);
        Schedule lighting = Schedule.Constant("Lighting", 0.2, ScheduleType.Fraction);
        Schedule equipment = Schedule.Constant("Equipment", 0.3, ScheduleType.Real);
        Schedule hotWater = Schedule.Constant("Hot water", 0.4, ScheduleType.Real);
        var profile = new ZoneProfile(
            new EntityId("PRFL-000001"),
            "Complete",
            heating,
            cooling,
            availability,
            occupant,
            lighting,
            equipment,
            hotWater);

        IReadOnlyList<IdfObject> objects = profile.ToIdfObjects();

        Assert.Equal(
            new[]
            {
                "Heating",
                "Cooling",
                "Availability",
                "Occupant",
                "Lighting",
                "Equipment",
                "Hot water",
            },
            objects.Select(value => value.Name));
        Assert.All(objects, value => Assert.Equal("Schedule:Compact", value.ObjectType));
    }

    [Fact]
    public void ProfileToIdfObjectsDoesNotDeduplicateRepeatedScheduleReferences()
    {
        Schedule repeated = Schedule.Constant("Repeated", 0.5, ScheduleType.Real);
        var profile = new ZoneProfile(
            new EntityId("PRFL-000001"),
            "Repeated references",
            occupant: repeated,
            equipment: repeated,
            hotWater: repeated);

        IReadOnlyList<IdfObject> first = profile.ToIdfObjects();
        IReadOnlyList<IdfObject> second = profile.ToIdfObjects();

        Assert.Equal(3, first.Count);
        Assert.All(first, value => Assert.Equal("Repeated", value.Name));
        Assert.NotSame(first[0], first[1]);
        Assert.NotSame(first[1], first[2]);
        Assert.NotSame(first[0].Fields[0], first[1].Fields[0]);
        Assert.NotSame(first[0], second[0]);

        first[0].Fields[0].Value = "Changed output";

        Assert.Equal("Repeated", first[1].Name);
        Assert.Equal("Repeated", second[0].Name);
    }

    [Fact]
    public void ProfileToIdfObjectsPreservesSourcesAndReturnsReadOnlyCollection()
    {
        Schedule heating = Schedule.Constant("Heating", 20, ScheduleType.Temperature);
        Schedule lighting = Schedule.Constant("Lighting", 1, ScheduleType.OnOff);
        var profile = new ZoneProfile(
            new EntityId("PRFL-000001"),
            "Sources",
            heatingSetpoint: heating,
            lighting: lighting);

        IReadOnlyList<IdfObject> objects = profile.ToIdfObjects();

        Assert.Same(heating, profile.HeatingSetpoint);
        Assert.Same(lighting, profile.Lighting);
        Assert.Equal(new[] { "Heating", "Lighting" }, objects.Select(value => value.Name));

        IList<IdfObject> mutableView = Assert.IsAssignableFrom<IList<IdfObject>>(objects);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = mutableView[0]);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(objects[0]));
        Assert.Throws<NotSupportedException>(() => mutableView.Remove(objects[0]));
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        Assert.Equal(new[] { "Heating", "Lighting" }, objects.Select(value => value.Name));
    }
}
