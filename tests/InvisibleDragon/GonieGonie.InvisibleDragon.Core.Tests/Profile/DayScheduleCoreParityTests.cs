using GonieGonie.InvisibleDragon.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Profile;

public sealed class DayScheduleCoreParityTests
{
    [Fact]
    public void DefaultConstructorUsesDeterministicImmutableZeroSchedule()
    {
        DaySchedule schedule = new();

        Assert.Equal("anonymous", schedule.Name);
        Assert.Equal(ScheduleType.Real, schedule.Type);
        Assert.Null(schedule.Unit);
        Assert.Equal(DaySchedule.FixedLength, schedule.Count);
        Assert.All(schedule, value => Assert.Equal(0d, value));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<double>)schedule.Values).Add(1));
        Assert.Throws<ArgumentException>(() => new DaySchedule(" "));

        DaySchedule defaultFraction = new(null, type: ScheduleType.Fraction);
        Assert.Equal("anonymous", defaultFraction.Name);
        Assert.Equal(ScheduleType.Fraction, defaultFraction.Type);
        Assert.All(defaultFraction, value => Assert.Equal(0d, value));

        double[] source = Enumerable.Repeat(0.25, DaySchedule.FixedLength).ToArray();
        DaySchedule copied = new("  copied  ", source, ScheduleType.Fraction, " fraction ");
        source[0] = 1;

        Assert.Equal("copied", copied.Name);
        Assert.Equal("fraction", copied.Unit);
        Assert.Equal(0.25, copied[0]);
    }

    [Fact]
    public void TimeTupleReturnsFreshReadOnlyExactIntervalEnds()
    {
        IReadOnlyList<TimeSpan> first = DaySchedule.TimeTuple();
        IReadOnlyList<TimeSpan> second = DaySchedule.TimeTuple();

        Assert.NotSame(first, second);
        Assert.Equal(DaySchedule.FixedLength, first.Count);
        Assert.Equal(TimeSpan.FromMinutes(10), first[0]);
        Assert.Equal(TimeSpan.FromHours(1), first[5]);
        Assert.Equal(TimeSpan.FromHours(24), first[DaySchedule.FixedLength - 1]);
        Assert.Equal(first, second);
        Assert.All(
            Enumerable.Range(0, DaySchedule.FixedLength),
            index => Assert.Equal(TimeSpan.FromMinutes((index + 1) * 10), first[index]));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<TimeSpan>)first).Add(TimeSpan.FromHours(25)));
    }

    [Fact]
    public void FromConstantUsesPythonScalarDispatchAndStrictScheduleValidation()
    {
        DaySchedule on = DaySchedule.FromConstant(null, true, ScheduleType.OnOff);
        DaySchedule fraction = DaySchedule.FromConstant("fraction", 1u, ScheduleType.Fraction);
        DaySchedule real = DaySchedule.FromConstant("real", 4L);

        Assert.Equal("anonymous", on.Name);
        Assert.Equal(ScheduleType.OnOff, on.Type);
        Assert.All(on, value => Assert.Equal(1d, value));
        Assert.Equal(ScheduleType.Fraction, fraction.Type);
        Assert.All(fraction, value => Assert.Equal(1d, value));
        Assert.Equal(ScheduleType.Real, real.Type);
        Assert.All(real, value => Assert.Equal(4d, value));
        Assert.Throws<ScheduleOperationException>(() =>
            DaySchedule.FromConstant("bad", "1", ScheduleType.Real));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DaySchedule.FromConstant("non-finite", double.NaN, ScheduleType.Real));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DaySchedule.FromConstant("fraction", 2, ScheduleType.Fraction));
    }

    [Fact]
    public void FactoriesAcceptNullNamesThroughTheNullableNativeContract()
    {
        DaySchedule compact = DaySchedule.FromCompact(
            null,
            new[] { new DayScheduleSegment(TimeSpan.FromHours(24), 0) });
        DaySchedule windows = DaySchedule.FromWindows(
            null,
            0,
            Array.Empty<DayScheduleWindow>());

        Assert.Equal("anonymous", compact.Name);
        Assert.Equal("anonymous", windows.Name);
    }

    [Fact]
    public void DeepCopyRetainsDataAndMetadataWithCopyName()
    {
        DaySchedule original = DaySchedule.FromCompact(
            "source",
            new[]
            {
                new DayScheduleSegment(TimeSpan.FromHours(8), 0.2),
                new DayScheduleSegment(TimeSpan.FromHours(24), 0.8),
            },
            ScheduleType.Fraction,
            "ratio");

        DaySchedule copy = original.DeepCopy();

        Assert.NotSame(original, copy);
        Assert.NotSame(original.Values, copy.Values);
        Assert.Equal("source:COPY", copy.Name);
        Assert.Equal(original.Type, copy.Type);
        Assert.Equal(original.Unit, copy.Unit);
        Assert.Equal(original.Values, copy.Values);
        DaySchedule changedCopy = copy.WithValue(0, 0.5);
        Assert.Equal(0.5, changedCopy[0]);
        Assert.Equal(0.2, copy[0]);
        Assert.Equal(0.2, original[0]);
    }

    [Fact]
    public void ClipUsesUpstreamEmptyNameFallbackAndPreservesStrictNames()
    {
        DaySchedule source = DaySchedule.FromCompact(
            "source",
            new[]
            {
                new DayScheduleSegment(TimeSpan.FromHours(12), -2),
                new DayScheduleSegment(TimeSpan.FromHours(24), 2),
            },
            unit: "kW");

        DaySchedule clipped = source.Clip(-1, 1, string.Empty);

        Assert.Equal("source:CLIP", clipped.Name);
        Assert.Equal(ScheduleType.Real, clipped.Type);
        Assert.Equal("kW", clipped.Unit);
        Assert.Equal(-1, clipped[0]);
        Assert.Equal(1, clipped[DaySchedule.FixedLength - 1]);
        Assert.Equal(-2, source[0]);
        Assert.Throws<ArgumentException>(() => source.Clip(name: " "));
        Assert.Throws<ArgumentException>(() => source.Clip(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Clip(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Clip(double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Clip(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Clip(maximum: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.Clip(maximum: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.Clip(maximum: double.NegativeInfinity));
    }

    [Fact]
    public void ClipPreservesPythonFirstArgumentSignedZeroTies()
    {
        DaySchedule lower = DaySchedule
            .FromConstant("lower", 0d)
            .Clip(minimum: -0d);
        DaySchedule upper = DaySchedule
            .FromConstant("upper", -0d)
            .Clip(maximum: 0d);

        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(lower[0]));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(upper[0]));
        Assert.Equal("-0.0", lower.ToIdfCompactExpression()[1]);
        Assert.Equal("0.0", upper.ToIdfCompactExpression()[1]);
    }

    [Fact]
    public void SummaryAndToStringMatchPinnedPythonTextAndGeneralFormatting()
    {
        DaySchedule schedule = RichSchedule();
        const string expected =
            "DaySchedule 'workday' [type=real, unit=kW, steps=144, interval=10 min]\n"
            + "  range: min=-1.234e-05, max=1e+04, constant=False, segments=4\n"
            + "  Until 06:00 -> 0\n"
            + "  Until 08:00 -> 1.235\n"
            + "  Until 17:00 -> 1e+04\n"
            + "  Until 24:00 -> -1.234e-05";

        Assert.Equal(expected, schedule.Summary());
        Assert.Equal(expected, schedule.ToString());
        Assert.Equal(
            "DaySchedule 'workday' [type=real, unit=kW, steps=144, interval=10 min]\n"
                + "  range: min=-1.234e-05, max=1e+04, constant=False, segments=4\n"
                + "  Until 06:00 -> 0\n"
                + "  Until 08:00 -> 1.235\n"
                + "  ... (2 more segments)",
            schedule.Summary(2));
        Assert.Equal(
            "DaySchedule 'workday' [type=real, unit=kW, steps=144, interval=10 min]\n"
                + "  range: min=-1.234e-05, max=1e+04, constant=False, segments=4\n"
                + "  Until 06:00 -> 0\n"
                + "  Until 08:00 -> 1.235\n"
                + "  Until 17:00 -> 1e+04\n"
                + "  ... (5 more segments)",
            schedule.Summary(-1));
    }

    [Fact]
    public void ToIdfCompactExpressionUsesExactPythonScalarSpelling()
    {
        DaySchedule real = RichSchedule();
        DaySchedule onOff = DaySchedule.FromConstant("on", 1, ScheduleType.OnOff);

        IReadOnlyList<string> realFields = real.ToIdfCompactExpression();

        Assert.Equal(
            new[]
            {
                "Until: 06:00", "0.0",
                "Until: 08:00", "1.23456",
                "Until: 17:00", "10000.0",
                "Until: 24:00", "-1.2345e-05",
            },
            realFields);
        Assert.Equal(new[] { "Until: 24:00", "1" }, onOff.ToIdfCompactExpression());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)realFields).Add("mutable"));
    }

    private static DaySchedule RichSchedule()
    {
        return DaySchedule.FromCompact(
            "workday",
            new[]
            {
                new DayScheduleSegment(TimeSpan.FromHours(6), 0),
                new DayScheduleSegment(TimeSpan.FromHours(8), 1.23456),
                new DayScheduleSegment(TimeSpan.FromHours(17), 10000),
                new DayScheduleSegment(TimeSpan.FromHours(24), -0.000012345),
            },
            ScheduleType.Real,
            "kW");
    }
}
