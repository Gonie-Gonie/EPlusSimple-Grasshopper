using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Profile;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class ScheduleCoreParityTests
{
    [Fact]
    public void ConstantsTimeTupleAndDefaultConstructorMatchAnnualTopology()
    {
        Schedule schedule = new(null, type: ScheduleType.Fraction);

        Assert.Equal(Schedule.FixedLength, schedule.Count);
        Assert.Equal(365, Schedule.TimeTuple.Count);
        Assert.Equal(new DateTime(2026, 1, 1), Schedule.TimeTuple[0]);
        Assert.Equal(new DateTime(2026, 12, 31), Schedule.TimeTuple[364]);
        Assert.Equal("anonymous", schedule.Name);
        Assert.Equal(ScheduleType.Fraction, schedule.Type);
        Assert.Equal(365, UniqueReferenceCount(schedule.RuleSets));
        Assert.All(schedule.RuleSets, ruleSet =>
        {
            Assert.NotSame(ruleSet.Weekdays, ruleSet.Weekends);
            Assert.Equal(0d, ruleSet.Weekdays[0]);
            Assert.Equal(0d, ruleSet.Weekends[0]);
        });

        Assert.Throws<NotSupportedException>(() =>
            ((IList<DateTime>)Schedule.TimeTuple).Add(new DateTime(2027, 1, 1)));
        Assert.Throws<ArgumentException>(() => new Schedule(" ", type: ScheduleType.Real));

        RuleSet supplied = RuleSet.Constant("supplied", 2);
        Schedule aliased = new("aliased", Enumerable.Repeat(supplied, Schedule.FixedLength));
        Assert.All(aliased.RuleSets, value => Assert.Same(supplied, value));

        Schedule trimmed = new("  named  ", Enumerable.Repeat(supplied, Schedule.FixedLength));
        Assert.Equal("named", trimmed.Name);
    }

    [Fact]
    public void FromConstantPreservesScalarDayAndRuleSetAliasContracts()
    {
        Schedule scalar = Schedule.FromConstant("scalar", 0.25, ScheduleType.Fraction);
        Assert.Equal(1, UniqueReferenceCount(scalar.RuleSets));
        Assert.NotSame(scalar[0].Weekdays, scalar[0].Weekends);
        Assert.Equal(0.25, scalar[0].Weekdays[0]);
        Assert.Same(scalar[0], scalar[364]);

        DaySchedule day = DaySchedule.Constant("day", 0.25, ScheduleType.Fraction);
        Schedule fromDay = Schedule.FromConstant("from-day", day, ScheduleType.Real);
        Assert.Equal(ScheduleType.Fraction, fromDay.Type);
        Assert.Same(fromDay[0], fromDay[364]);
        Assert.Same(day, fromDay[0].Weekdays);
        Assert.Same(day, fromDay[0].Weekends);

        RuleSet ruleSet = new("rule", day, day, monday: day, holiday: day);
        Schedule fromRule = Schedule.FromConstant("from-rule", ruleSet, ScheduleType.Real);
        Assert.Equal(ScheduleType.Fraction, fromRule.Type);
        Assert.All(fromRule.RuleSets, value => Assert.Same(ruleSet, value));

        object boxedDay = day;
        Schedule fromBoxedDay = Schedule.FromConstant("boxed-day", boxedDay);
        Assert.Equal(ScheduleType.Fraction, fromBoxedDay.Type);
        Assert.Same(day, fromBoxedDay[0].Weekdays);
        Assert.Same(day, fromBoxedDay[0].Weekends);

        object boxedRule = ruleSet;
        Schedule fromBoxedRule = Schedule.FromConstant("boxed-rule", boxedRule);
        Assert.All(fromBoxedRule.RuleSets, value => Assert.Same(ruleSet, value));

        Schedule trimmed = Schedule.FromConstant("  scalar  ", 0.25, ScheduleType.Fraction);
        Assert.Equal("scalar", trimmed.Name);
        Assert.Equal("scalar:ruleset", trimmed[0].Name);
    }

    [Fact]
    public void FromCompactUsesDistinctDefaultsPerUncoveredDateAndLastPeriodWins()
    {
        RuleSet source = RuleSet.Constant("source", 0.25, ScheduleType.Fraction);
        Schedule schedule = Schedule.FromCompact(
            "compact",
            new[]
            {
                new SchedulePeriod("0102", "0103", source),
            });

        Assert.Equal(364, UniqueReferenceCount(schedule.RuleSets));
        Assert.Equal(364, schedule.Compactize().Count);
        Assert.Same(source, schedule[January(2)]);
        Assert.Same(source, schedule[January(3)]);
        Assert.NotSame(schedule[January(1)], schedule[January(4)]);
        Assert.Equal(363, UniqueReferenceCount(
            schedule.RuleSets.Where(ruleSet => !ReferenceEquals(ruleSet, source))));
        Assert.All(
            schedule.RuleSets.Where(ruleSet => !ReferenceEquals(ruleSet, source)),
            ruleSet => Assert.NotSame(ruleSet.Weekdays, ruleSet.Weekends));

        Schedule trimmed = Schedule.FromCompact(
            "  compact-name  ",
            new[] { new SchedulePeriod("0102", "0103", source) });
        Assert.Equal("compact-name", trimmed.Name);
        Assert.StartsWith("compact-name:default:", trimmed[January(1)].Name, StringComparison.Ordinal);

        RuleSet late = RuleSet.Constant("late", 0.5, ScheduleType.Fraction);
        Schedule overlap = Schedule.FromCompact(
            "overlap",
            new[]
            {
                new SchedulePeriod("0101", "0103", source),
                new SchedulePeriod("0103", "0104", late),
            });
        Assert.Same(source, overlap[January(2)]);
        Assert.Same(late, overlap[January(3)]);
        Assert.Same(late, overlap[January(4)]);

        RuleSet real = RuleSet.Constant("real", 1, ScheduleType.Real);
        Assert.Throws<ArgumentException>(() => Schedule.FromCompact(
            "mixed",
            new[]
            {
                new SchedulePeriod("0101", "0101", source),
                new SchedulePeriod("0102", "0102", real),
            }));
    }

    [Fact]
    public void FromWindowsSupportsScalarDayAndRuleSetValuesWithOrderedOverwrite()
    {
        DaySchedule day = DaySchedule.Constant("day", 0.25, ScheduleType.Fraction);
        RuleSet ruleSet = new("rule", day, day);
        Schedule schedule = Schedule.FromWindows(
            "  window  ",
            0.1,
            new[]
            {
                new ScheduleValueWindow("0102", "0103", day),
                new ScheduleValueWindow("0103", "0104", ruleSet),
                new ScheduleValueWindow("0104", "0105", 0.5),
            },
            ScheduleType.Fraction);

        Assert.Equal(
            new[] { 0.1, 0.25, 0.25, 0.5, 0.5, 0.1 },
            Enumerable.Range(1, 6)
                .Select(dayNumber => schedule[January(dayNumber)].Weekdays[0])
                .ToArray());
        Assert.Equal("window", schedule.Name);
        Assert.Same(schedule[January(1)], schedule[January(6)]);
        Assert.Same(day, schedule[January(2)].Weekdays);
        Assert.Same(day, schedule[January(2)].Weekends);
        Assert.Same(ruleSet, schedule[January(3)]);
        Assert.Same(schedule[January(4)], schedule[January(5)]);
        Assert.Equal("window:window:003", schedule[January(4)].Name);
        Assert.Equal(4, UniqueReferenceCount(
            Enumerable.Range(1, 6).Select(dayNumber => schedule[January(dayNumber)])));

        DaySchedule fraction = DaySchedule.Constant("fraction", 0.5, ScheduleType.Fraction);
        Assert.Throws<ArgumentException>(() => Schedule.FromWindows(
            "mismatch",
            1d,
            new[] { new ScheduleValueWindow("0101", "0101", fraction) },
            ScheduleType.Real));
        Assert.Throws<ArgumentException>(() => Schedule.FromWindows(
            "unsupported",
            1d,
            new[] { new ScheduleValueWindow("0101", "0101", new object()) },
            ScheduleType.Real));
    }

    [Fact]
    public void ApplyReturnsImmutableCopyAndPreservesUnchangedAliases()
    {
        Schedule source = Schedule.FromConstant("source", 0d);
        RuleSet applied = RuleSet.Constant("applied", 1d);

        Schedule result = source.Apply(applied, "0701", "0731");

        Assert.Equal("source", result.Name);
        Assert.Same(source[January(1)], result[January(1)]);
        Assert.Same(applied, result[new DateTime(2026, 7, 1)]);
        Assert.Same(applied, result[new DateTime(2026, 7, 31)]);
        Assert.NotSame(applied, result[new DateTime(2026, 8, 1)]);
        Assert.Equal(0d, source[new DateTime(2026, 7, 1)].Weekdays[0]);

        Schedule normalizedYear = source.Apply(
            applied,
            new DateTime(2025, 7, 1),
            new DateTime(2030, 7, 1));
        Assert.Same(applied, normalizedYear[new DateTime(2026, 7, 1)]);

        Assert.Throws<ArgumentException>(() =>
            source.Apply(applied, new DateTime(2026, 2, 1), new DateTime(2026, 1, 1)));
        RuleSet fraction = RuleSet.Constant("fraction", 0.5, ScheduleType.Fraction);
        Assert.Throws<ArgumentException>(() =>
            source.Apply(fraction, January(1), January(1)));
    }

    [Fact]
    public void DeepCopyAndAsTypeCopyEveryExplicitChildAndPreservePeriodAliases()
    {
        DaySchedule day = DaySchedule.Constant("day", 0.25, ScheduleType.Fraction);
        RuleSet ruleSet = new(
            "rule",
            day,
            day,
            monday: day,
            holiday: day);
        Schedule source = Schedule.FromConstant("source", ruleSet);

        Schedule deepCopy = source.DeepCopy();
        Assert.Equal("source:COPY", deepCopy.Name);
        Assert.Equal(1, UniqueReferenceCount(deepCopy.RuleSets));
        Assert.NotSame(ruleSet, deepCopy[0]);
        Assert.Equal("rule:COPY", deepCopy[0].Name);
        AssertCopiedSlotsAreDistinct(day, deepCopy[0]);

        Schedule real = source.AsType(ScheduleType.Real);
        Assert.Equal("source", real.Name);
        Assert.Equal(ScheduleType.Real, real.Type);
        Assert.Equal(1, UniqueReferenceCount(real.RuleSets));
        Assert.Equal("rule:COPY", real[0].Name);
        Assert.Equal("day:COPY", real[0].Weekdays.Name);
        Assert.Equal("day:COPY", real[0].Weekends.Name);
        AssertCopiedSlotsAreDistinct(day, real[0]);

        Assert.Equal(ScheduleType.Fraction, source.Type);
        Assert.Same(ruleSet, source[0]);
        Assert.Same(day, source[0].Weekdays);
    }

    [Fact]
    public void MetricsUsePython312CompensatedOuterSumsAndPreserveBoundaries()
    {
        RuleSet[] cancellationRules = new[] { 1e16, 1d, -1e16 }
            .Select((value, index) => RuleSet.Constant($"cancel-{index}", value))
            .Concat(Enumerable.Repeat(RuleSet.Constant("zero", 0d), 362))
            .ToArray();
        Schedule cancellation = new("cancellation", cancellationRules);
        AssertBits(24d, cancellation.Integral);
        AssertBits(0.06575342465753424d, cancellation.Average);

        RuleSet zero = RuleSet.Constant("zero", 0d);
        Schedule positives = new(
            "positives",
            new[]
            {
                RuleSet.Constant("huge", 1e16),
                RuleSet.Constant("one", 1d),
            }.Concat(Enumerable.Repeat(zero, 363)));
        AssertBits(5000000000000001d, positives.PositiveAverage);

        double minimumSubnormal = BitConverter.Int64BitsToDouble(1);
        Schedule subnormal = Schedule.FromConstant("subnormal", minimumSubnormal);
        Assert.Equal(0x2238L, BitConverter.DoubleToInt64Bits(subnormal.Integral));
        AssertBits(minimumSubnormal, subnormal.PositiveAverage);
        AssertBits(minimumSubnormal, subnormal.Minimum);
        AssertBits(minimumSubnormal, subnormal.Maximum);

        Schedule negativeZero = Schedule.FromConstant("negative-zero", -0d);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(negativeZero.Integral));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(negativeZero.Average));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(negativeZero.PositiveAverage));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(negativeZero.Minimum));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(negativeZero.Maximum));

        Schedule overflow = Schedule.FromConstant("overflow", double.MaxValue);
        Assert.Equal(double.PositiveInfinity, overflow.Integral);
        Assert.Equal(double.PositiveInfinity, overflow.Average);
        Assert.Equal(double.PositiveInfinity, overflow.PositiveAverage);
    }

    [Fact]
    public void DaySchedulesResolveCalendarFallbacksAndReturnReadOnlySnapshot()
    {
        DaySchedule weekday = DaySchedule.Constant("weekday", 1d);
        DaySchedule weekend = DaySchedule.Constant("weekend", 2d);
        DaySchedule monday = DaySchedule.Constant("monday", 3d);
        RuleSet ruleSet = new("rules", weekday, weekend, monday: monday);
        Schedule schedule = Schedule.FromConstant("annual", ruleSet);

        IReadOnlyList<DaySchedule> days = schedule.DaySchedules;

        Assert.Equal(365, days.Count);
        Assert.Same(weekday, days[0]);
        Assert.Same(weekend, days[2]);
        Assert.Same(monday, days[4]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<DaySchedule>)days).Add(weekday));
    }

    [Fact]
    public void ClipIsImmutableUsesPythonDefaultNameAndRejectsReversedBounds()
    {
        Schedule source = Schedule.FromConstant("source", 2d);

        Schedule clipped = source.Clip(0d, 1d, string.Empty);

        Assert.Equal("source:CLIP", clipped.Name);
        Assert.Equal(1d, clipped[0].Weekdays[0]);
        Assert.Equal(2d, source[0].Weekdays[0]);
        Assert.NotSame(source[0], clipped[0]);
        Assert.Single(clipped.Compactize());

        Schedule copy = source.Clip(name: "copy");
        Assert.Equal("copy", copy.Name);
        Assert.Equal(2d, copy[0].Weekdays[0]);
        Assert.NotSame(source[0], copy[0]);

        Assert.Throws<ArgumentException>(() => source.Clip(2d, 1d));
    }

    [Fact]
    public void CompactUnificationUsesAllBoundariesAndRejectsMissingCoverage()
    {
        RuleSet a = RuleSet.Constant("a", 1d);
        RuleSet b = RuleSet.Constant("b", 2d);
        RuleSet c = RuleSet.Constant("c", 3d);
        RuleSet d = RuleSet.Constant("d", 4d);
        var left = new[]
        {
            new SchedulePeriod("0101", "0630", a),
            new SchedulePeriod("0701", "1231", b),
        };
        var right = new[]
        {
            new SchedulePeriod("0101", "0331", c),
            new SchedulePeriod("0401", "1231", d),
        };

        (IReadOnlyList<SchedulePeriod> unifiedLeft, IReadOnlyList<SchedulePeriod> unifiedRight) =
            Schedule.UnifyCompactizedSchedules(left, right);

        Assert.Equal(3, unifiedLeft.Count);
        Assert.Equal(3, unifiedRight.Count);
        Assert.Equal(new DateTime(2026, 3, 31), unifiedLeft[0].End);
        Assert.Equal(new DateTime(2026, 6, 30), unifiedLeft[1].End);
        Assert.Same(a, unifiedLeft[0].RuleSet);
        Assert.Same(a, unifiedLeft[1].RuleSet);
        Assert.Same(b, unifiedLeft[2].RuleSet);
        Assert.Same(c, unifiedRight[0].RuleSet);
        Assert.Same(d, unifiedRight[1].RuleSet);
        Assert.Same(d, unifiedRight[2].RuleSet);

        IReadOnlyList<IReadOnlyList<SchedulePeriod>> many =
            Schedule.UnifyCompactizedSchedulesMany(left, right, left);
        Assert.Equal(3, many.Count);
        Assert.All(many, result => Assert.Equal(3, result.Count));
        Assert.Empty(Schedule.UnifyCompactizedSchedulesMany());

        var missingJanuaryFirst = new[]
        {
            new SchedulePeriod("0102", "1231", a),
        };
        Assert.Throws<ArgumentException>(() =>
            Schedule.UnifyCompactizedSchedules(missingJanuaryFirst, right));
    }

    [Fact]
    public void SummaryMatchesPinnedFormattingIncludingSliceBoundaries()
    {
        DaySchedule day = DaySchedule.Constant("day", 0.25, ScheduleType.Fraction);
        DaySchedule sameValueOverride = DaySchedule.Constant(
            "different-name",
            0.25,
            ScheduleType.Fraction);
        RuleSet ruleSet = new(
            "idf-rule",
            day,
            day,
            monday: sameValueOverride,
            sunday: sameValueOverride,
            holiday: sameValueOverride);
        Schedule schedule = Schedule.FromConstant("idf", ruleSet);

        const string expected =
            "Schedule 'idf' [type=fraction, days=365]\n"
            + "  range: min=0.25, max=0.25, periods=1, unique_rulesets=1\n"
            + "  01/01 ~ 12/31: 'idf-rule' (min=0.25, max=0.25)";
        Assert.Equal(expected, schedule.Summary());
        Assert.Equal(expected, schedule.ToString());
        Assert.Equal(
            "Schedule 'idf' [type=fraction, days=365]\n"
            + "  range: min=0.25, max=0.25, periods=1, unique_rulesets=1\n"
            + "  ... (1 more periods)",
            schedule.Summary(0));
        Assert.Equal(
            "Schedule 'idf' [type=fraction, days=365]\n"
            + "  range: min=0.25, max=0.25, periods=1, unique_rulesets=1\n"
            + "  ... (2 more periods)",
            schedule.Summary(-1));
    }

    [Fact]
    public void SummaryUsesPythonFourSignificantDigitRoundingAndStringRepr()
    {
        (double Value, string Text)[] cases =
        {
            (9.9995d, "9.999"),
            (10_000d, "1e+04"),
            (0.0001d, "0.0001"),
            (0.00001d, "1e-05"),
            (BitConverter.Int64BitsToDouble(1), "4.941e-324"),
            (double.MaxValue, "1.798e+308"),
            (123456789d, "1.235e+08"),
            (-12.3456d, "-12.35"),
            (-0d, "-0"),
        };
        foreach ((double value, string text) in cases)
        {
            string summary = Schedule.FromConstant("format", value).Summary(0);
            Assert.Contains($"range: min={text}, max={text},", summary, StringComparison.Ordinal);
        }

        Schedule quoted = Schedule.FromConstant("a'b\n\u2028😀", 1d);
        Assert.StartsWith(
            "Schedule \"a'b\\n\\u2028😀\" [type=real, days=365]",
            quoted.Summary(0),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ToIdfObjectMatchesPinnedRuleSetFieldSequenceAndAnnualIdentityPeriods()
    {
        DaySchedule day = DaySchedule.Constant("day", 0.25, ScheduleType.Fraction);
        DaySchedule sameValueOverride = DaySchedule.Constant(
            "different-name",
            0.25,
            ScheduleType.Fraction);
        RuleSet ruleSet = new(
            "idf-rule",
            day,
            day,
            monday: sameValueOverride,
            sunday: sameValueOverride,
            holiday: sameValueOverride);
        Schedule schedule = Schedule.FromConstant("idf", ruleSet);

        IdfObject idf = schedule.ToIdfObject();

        Assert.Equal("Schedule:Compact", idf.ObjectType);
        Assert.Equal(
            new[]
            {
                "idf",
                "ScheduleTypeLimits:Fraction",
                "Through: 12/31",
                "For: Monday", "Until: 24:00", "0.25",
                "For: Tuesday", "Until: 24:00", "0.25",
                "For: Wednesday", "Until: 24:00", "0.25",
                "For: Thursday", "Until: 24:00", "0.25",
                "For: Friday", "Until: 24:00", "0.25",
                "For: Saturday", "Until: 24:00", "0.25",
                "For: Sunday", "Until: 24:00", "0.25",
                "For: Holiday", "Until: 24:00", "0.25",
                "For: AllOtherDays", "Until: 24:00", "0.25",
            },
            idf.Fields.Select(field => field.Value).ToArray());

        IdfObject defaultIdf = new Schedule("default").ToIdfObject();
        Assert.Equal(3652, defaultIdf.Count);
        Assert.Equal("Through: 1/1", defaultIdf[2]);
        Assert.Equal("Through: 1/2", defaultIdf[12]);
        Assert.Equal("Through: 12/31", defaultIdf[3642]);
    }

    private static void AssertCopiedSlotsAreDistinct(DaySchedule source, RuleSet copy)
    {
        Assert.NotSame(source, copy.Weekdays);
        Assert.NotSame(source, copy.Weekends);
        Assert.NotSame(source, copy.Monday);
        Assert.NotSame(source, copy.Holiday);
        Assert.NotSame(copy.Weekdays, copy.Weekends);
        Assert.NotSame(copy.Weekdays, copy.Monday);
        Assert.NotSame(copy.Monday, copy.Holiday);
        Assert.Equal("day:COPY", copy.Weekdays.Name);
        Assert.Equal("day:COPY", copy.Weekends.Name);
        Assert.Equal("day:COPY", copy.Monday!.Name);
        Assert.Equal("day:COPY", copy.Holiday!.Name);
    }

    private static int UniqueReferenceCount(IEnumerable<RuleSet> values)
    {
        var unique = new List<RuleSet>();
        foreach (RuleSet value in values)
        {
            if (!unique.Any(candidate => ReferenceEquals(candidate, value)))
            {
                unique.Add(value);
            }
        }

        return unique.Count;
    }

    private static void AssertBits(double expected, double actual)
    {
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(expected),
            BitConverter.DoubleToInt64Bits(actual));
    }

    private static DateTime January(int day)
    {
        return new DateTime(Schedule.DefaultYear, 1, day);
    }
}
