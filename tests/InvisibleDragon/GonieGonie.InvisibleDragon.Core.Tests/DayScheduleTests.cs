using GonieGonie.InvisibleDragon.Profile;

namespace GonieGonie.InvisibleDragon.Tests;

public sealed class DayScheduleTests
{
    [Fact]
    public void ConstantScheduleHasFixedTenMinuteResolution()
    {
        DaySchedule schedule = DaySchedule.Constant("Always on", 1, ScheduleType.OnOff);

        Assert.Equal(144, schedule.Count);
        Assert.All(schedule, value => Assert.Equal(1, value));
        Assert.Equal(24, schedule.IntegralHours);
        Assert.True(schedule.IsConstant);
    }

    [Theory]
    [InlineData(ScheduleType.OnOff, 0.5)]
    [InlineData(ScheduleType.Fraction, -0.1)]
    [InlineData(ScheduleType.Fraction, 1.1)]
    [InlineData(ScheduleType.Temperature, 201)]
    public void ScheduleTypesRejectOutOfDomainValues(ScheduleType type, double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DaySchedule.Constant("Bad", value, type));
    }

    [Fact]
    public void CompactScheduleUsesEnergyPlusUntilSemantics()
    {
        DaySchedule schedule = DaySchedule.FromCompact(
            "Office",
            new[]
            {
                new DayScheduleSegment(TimeSpan.FromHours(9), 0),
                new DayScheduleSegment(TimeSpan.FromHours(18), 1),
                new DayScheduleSegment(TimeSpan.FromHours(24), 0),
            },
            ScheduleType.OnOff);

        Assert.Equal(0, schedule[53]); // 08:50-09:00
        Assert.Equal(1, schedule[54]); // 09:00-09:10
        Assert.Equal(1, schedule[107]); // 17:50-18:00
        Assert.Equal(0, schedule[108]); // 18:00-18:10
        Assert.Equal(9, schedule.IntegralHours);
    }

    [Fact]
    public void WindowScheduleUsesStartInclusiveEndExclusiveIntervals()
    {
        DaySchedule schedule = DaySchedule.FromWindows(
            "Office",
            0,
            new[]
            {
                new DayScheduleWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(18), 1),
            },
            ScheduleType.OnOff);

        Assert.Equal(0, schedule[53]);
        Assert.Equal(1, schedule[54]);
        Assert.Equal(1, schedule[107]);
        Assert.Equal(0, schedule[108]);
    }

    [Fact]
    public void CompactizeRoundTripsValues()
    {
        DaySchedule source = DaySchedule.FromWindows(
            "Fraction",
            0.2,
            new[]
            {
                new DayScheduleWindow(TimeSpan.FromHours(8), TimeSpan.FromHours(12), 0.8),
            },
            ScheduleType.Fraction);

        DaySchedule roundTrip = DaySchedule.FromCompact(
            "Fraction",
            source.Compactize(),
            ScheduleType.Fraction);

        Assert.Equal(source, roundTrip);
    }

    [Fact]
    public void InputAndUpdatesAreImmutable()
    {
        double[] values = Enumerable.Repeat(0d, DaySchedule.FixedLength).ToArray();
        var original = new DaySchedule("Original", values);
        values[0] = 99;

        DaySchedule changed = original.WithValue(0, 2);

        Assert.Equal(0, original[0]);
        Assert.Equal(2, changed[0]);
    }

    [Fact]
    public void ArithmeticPropagatesScheduleTypes()
    {
        DaySchedule availability = DaySchedule.Constant("Available", 1, ScheduleType.OnOff);
        DaySchedule fraction = DaySchedule.Constant("Fraction", 0.5, ScheduleType.Fraction);
        DaySchedule temperature = DaySchedule.Constant("Temperature", 20, ScheduleType.Temperature);
        DaySchedule real = DaySchedule.Constant("Real", 2, ScheduleType.Real);

        Assert.Equal(ScheduleType.Fraction, (availability * fraction).Type);
        Assert.Equal(ScheduleType.Temperature, (real * temperature).Type);
        Assert.Equal(ScheduleType.Temperature, (temperature + real).Type);
        Assert.Equal(10, (fraction * temperature)[0]);
    }

    [Fact]
    public void InvalidArithmeticReportsOperationError()
    {
        DaySchedule on = DaySchedule.Constant("On", 1, ScheduleType.OnOff);
        DaySchedule fraction = DaySchedule.Constant("Fraction", 0.5, ScheduleType.Fraction);

        Assert.Throws<ScheduleOperationException>(() => on + on);
        Assert.Throws<ScheduleOperationException>(() => fraction / fraction);
    }

    [Fact]
    public void ComparisonsLogicalOperationsAndWhereAreElementWise()
    {
        DaySchedule values = DaySchedule.FromWindows(
            "Values",
            0,
            new[] { new DayScheduleWindow(TimeSpan.FromHours(12), TimeSpan.FromHours(24), 2) });
        DaySchedule positive = values.GreaterThan(0);
        DaySchedule notPositive = !positive;
        DaySchedule selected = DaySchedule.Where(
            positive,
            DaySchedule.Constant("High", 10),
            DaySchedule.Constant("Low", 1));

        Assert.Equal(1, notPositive[0]);
        Assert.Equal(0, notPositive[72]);
        Assert.Equal(1, selected[0]);
        Assert.Equal(10, selected[72]);
        DaySchedule conjunction = positive & !notPositive;
        Assert.Equal(positive.Values, conjunction.Values);
    }

    [Fact]
    public void ClipAndStatisticsPreserveFixedLength()
    {
        DaySchedule values = DaySchedule.FromWindows(
            "Values",
            -2,
            new[] { new DayScheduleWindow(TimeSpan.FromHours(6), TimeSpan.FromHours(18), 4) });

        DaySchedule clipped = values.Clip(0, 2);

        Assert.Equal(0, clipped.Minimum);
        Assert.Equal(2, clipped.Maximum);
        Assert.Equal(12, clipped.PositiveHours);
        Assert.Equal(1, clipped.Average);
    }

    [Fact]
    public void ValidationAccumulatesLengthAndValueDiagnostics()
    {
        var result = DaySchedule.Validate(new[] { -1d, 2d }, ScheduleType.Fraction);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Diagnostics.Count);
        Assert.Equal("INVISIBLEDRAGON.SCHEDULE.INVALID_DAY_LENGTH", result.Diagnostics[0].Code);
    }
}
