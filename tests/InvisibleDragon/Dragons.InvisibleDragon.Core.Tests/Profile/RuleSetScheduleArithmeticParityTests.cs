using Dragons.InvisibleDragon.Profile;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class RuleSetScheduleArithmeticParityTests
{
    [Fact]
    public void RuleSetScalarAndReverseOperatorsPreserveMissingOverrides()
    {
        var source = new RuleSet(
            "Source",
            DaySchedule.Constant("Weekday", 2),
            DaySchedule.Constant("Weekend", 4),
            monday: DaySchedule.Constant("Monday", 3));

        RuleSet multiplied = source * 2;
        RuleSet reverseMultiplied = 2 * source;
        RuleSet directAdded = source + 1;
        RuleSet added = 1 + source;
        RuleSet directSubtracted = source - 1;
        RuleSet subtracted = 10 - source;
        RuleSet directDivided = source / 2;
        RuleSet divided = 12 / source;

        Assert.Equal(6, Value(multiplied, DayOfWeek.Monday));
        Assert.Equal(6, Value(reverseMultiplied, DayOfWeek.Monday));
        Assert.Equal(4, Value(multiplied, DayOfWeek.Tuesday));
        Assert.Equal(3, Value(directAdded, DayOfWeek.Tuesday));
        Assert.Equal(5, Value(added, DayOfWeek.Saturday));
        Assert.Equal(1, Value(directSubtracted, DayOfWeek.Tuesday));
        Assert.Equal(7, Value(subtracted, DayOfWeek.Monday));
        Assert.Equal(1, Value(directDivided, DayOfWeek.Tuesday));
        Assert.Equal(6, Value(divided, DayOfWeek.Tuesday));
        Assert.Equal(3, Value(divided, DayOfWeek.Saturday));
        Assert.NotNull(multiplied.Monday);
        Assert.Null(multiplied.Tuesday);
        Assert.Null(divided.Holiday);
        Assert.Equal("1:ADD:Source", added.Name);
        Assert.Equal("10:SUB:Source", subtracted.Name);
        Assert.Equal("12:DIV:Source", divided.Name);
        Assert.Equal(2, Value(source, DayOfWeek.Tuesday));
    }

    [Fact]
    public void RuleSetBinaryOperatorsResolveFallbackOnlyWhenEitherSideHasOverride()
    {
        var left = new RuleSet(
            "Left",
            DaySchedule.Constant("Left weekday", 2),
            DaySchedule.Constant("Left weekend", 8),
            monday: DaySchedule.Constant("Left Monday", 4));
        var right = new RuleSet(
            "Right",
            DaySchedule.Constant("Right weekday", 3),
            DaySchedule.Constant("Right weekend", 2),
            tuesday: DaySchedule.Constant("Right Tuesday", 5));

        RuleSet product = left * right;
        RuleSet sum = left + right;
        RuleSet difference = left - right;
        RuleSet quotient = left / right;

        Assert.Equal(12, Value(product, DayOfWeek.Monday));
        Assert.Equal(10, Value(product, DayOfWeek.Tuesday));
        Assert.Equal(6, Value(product, DayOfWeek.Wednesday));
        Assert.Equal(7, Value(sum, DayOfWeek.Monday));
        Assert.Equal(-3, Value(difference, DayOfWeek.Tuesday));
        Assert.Equal(4d / 3d, Value(quotient, DayOfWeek.Monday));
        Assert.NotNull(product.Monday);
        Assert.NotNull(product.Tuesday);
        Assert.Null(product.Wednesday);
    }

    [Fact]
    public void RuleSetComparisonElementAndPredicateMethodsPropagateDayScheduleRules()
    {
        var source = new RuleSet(
            "Source",
            DaySchedule.Constant("Weekday", 2),
            DaySchedule.Constant("Weekend", -2),
            monday: DaySchedule.Constant("Monday", 3));
        RuleSet threshold = RuleSet.Constant("Threshold", 2);

        Assert.Equal(1, Value(source.ElementEqual(threshold), DayOfWeek.Tuesday));
        Assert.Equal(1, Value(source.ElementNotEqual(threshold), DayOfWeek.Monday));
        Assert.Equal(1, Value(source.LessThan(threshold), DayOfWeek.Saturday));
        Assert.Equal(1, Value(source.LessThanOrEqual(2), DayOfWeek.Tuesday));
        Assert.Equal(1, Value(source.GreaterThan(threshold), DayOfWeek.Monday));
        Assert.Equal(1, Value(source.GreaterThanOrEqual(3), DayOfWeek.Monday));
        Assert.Equal(2, Value(source.ElementMinimum(2), DayOfWeek.Monday));
        Assert.Equal(3, Value(source.ElementMaximum(threshold), DayOfWeek.Monday));
        Assert.Equal(1, Value(source.IsPositive(), DayOfWeek.Monday));
        Assert.Equal(1, Value(source.IsNegative(), DayOfWeek.Saturday));
        Assert.Equal(1, Value(source.IsNonzero(), DayOfWeek.Saturday));
        Assert.Equal(0, Value(source.IsZero(), DayOfWeek.Tuesday));
        Assert.Equal(1, Value(source.IsBetween(2, 3), DayOfWeek.Monday));
        Assert.Equal(0, Value(source.IsBetween(2, 3, includeMaximum: false), DayOfWeek.Monday));
    }

    [Fact]
    public void RuleSetLogicalAndWherePreserveOverrideTopologyAndInferRequestedType()
    {
        var condition = new RuleSet(
            "Condition",
            DaySchedule.Constant("Weekday on", 1, ScheduleType.OnOff),
            DaySchedule.Constant("Weekend off", 0, ScheduleType.OnOff),
            monday: DaySchedule.Constant("Monday off", 0, ScheduleType.OnOff),
            type: ScheduleType.OnOff);
        var trueValues = new RuleSet(
            "True",
            DaySchedule.Constant("True weekday", 0.8, ScheduleType.Fraction),
            DaySchedule.Constant("True weekend", 0.7, ScheduleType.Fraction),
            tuesday: DaySchedule.Constant("True Tuesday", 0.6, ScheduleType.Fraction),
            type: ScheduleType.Fraction);

        RuleSet selected = RuleSet.Where(
            condition,
            trueValues,
            0.2,
            "Selected",
            ScheduleType.Fraction);
        RuleSet inverted = !condition;
        RuleSet and = condition & inverted;
        RuleSet or = condition | inverted;
        RuleSet selectedDaySchedule = RuleSet.Where(
            condition,
            DaySchedule.Constant("Day branch", 0.4, ScheduleType.Fraction),
            0.1,
            type: ScheduleType.Fraction);

        Assert.Equal(ScheduleType.Fraction, selected.Type);
        Assert.Equal("Selected:weekdays", selected.Weekdays.Name);
        Assert.Equal("Selected:monday", selected.Monday?.Name);
        Assert.Equal(0.2, Value(selected, DayOfWeek.Monday));
        Assert.Equal(0.6, Value(selected, DayOfWeek.Tuesday));
        Assert.Equal(0.8, Value(selected, DayOfWeek.Wednesday));
        Assert.Equal(0.2, Value(selected, DayOfWeek.Saturday));
        Assert.NotNull(selected.Monday);
        Assert.NotNull(selected.Tuesday);
        Assert.Null(selected.Wednesday);
        Assert.Equal(1, Value(inverted, DayOfWeek.Monday));
        Assert.Equal(0, Value(inverted, DayOfWeek.Tuesday));
        Assert.Equal(1, Value(condition.IsOn(), DayOfWeek.Tuesday));
        Assert.Equal(1, Value(condition.IsOff(), DayOfWeek.Monday));
        Assert.Equal(0, Value(and, DayOfWeek.Tuesday));
        Assert.Equal(1, Value(or, DayOfWeek.Tuesday));
        Assert.Equal(0.4, Value(selectedDaySchedule, DayOfWeek.Tuesday));
        Assert.Equal(0.1, Value(selectedDaySchedule, DayOfWeek.Saturday));
        Assert.Throws<ScheduleOperationException>(() => RuleSet.Where(trueValues, 1, 0));
    }

    [Fact]
    public void ScheduleScalarAndReverseOperatorsMatchCorrespondingDateValues()
    {
        Schedule source = Schedule.Constant("Source", 2).Apply(
            RuleSet.Constant("Special", 4),
            January(10),
            January(20));

        Schedule multiplied = source * 3;
        Schedule reverseMultiplied = 3 * source;
        Schedule directAdded = source + 1;
        Schedule added = 1 + source;
        Schedule directSubtracted = source - 1;
        Schedule subtracted = 10 - source;
        Schedule directDivided = source / 2;
        Schedule divided = 12 / source;

        Assert.Equal(6, Value(multiplied, January(1)));
        Assert.Equal(6, Value(reverseMultiplied, January(1)));
        Assert.Equal(12, Value(multiplied, January(10)));
        Assert.Equal(3, Value(directAdded, January(1)));
        Assert.Equal(5, Value(added, January(10)));
        Assert.Equal(1, Value(directSubtracted, January(1)));
        Assert.Equal(6, Value(subtracted, January(10)));
        Assert.Equal(1, Value(directDivided, January(1)));
        Assert.Equal(6, Value(divided, January(1)));
        Assert.Equal(3, Value(divided, January(10)));
        Assert.Equal(3, multiplied.Compactize().Count);
        Assert.Equal("1:ADD:Source", added.Name);
        Assert.Equal("10:SUB:Source", subtracted.Name);
        Assert.Equal("12:DIV:Source", divided.Name);
        Assert.Equal(2, Value(source, January(1)));
    }

    [Fact]
    public void ScheduleBinaryOperatorsUseUnifiedCompactBoundaries()
    {
        Schedule left = Schedule.Constant("Left", 2).Apply(
            RuleSet.Constant("Left special", 4),
            January(10),
            January(20));
        Schedule right = Schedule.Constant("Right", 10).Apply(
            RuleSet.Constant("Right special", 5),
            January(15),
            February(1));

        Schedule product = left * right;
        Schedule sum = left + right;
        Schedule difference = left - right;
        Schedule quotient = left / right;
        Schedule minimum = left.ElementMinimum(right);
        Schedule maximum = left.ElementMaximum(right);
        Schedule equal = left.ElementEqual(right);

        Assert.Equal(20, Value(product, January(1)));
        Assert.Equal(40, Value(product, January(10)));
        Assert.Equal(20, Value(product, January(15)));
        Assert.Equal(10, Value(product, January(21)));
        Assert.Equal(20, Value(product, February(2)));
        Assert.Equal(5, product.Compactize().Count);
        Assert.Equal(9, Value(sum, January(15)));
        Assert.Equal(-1, Value(difference, January(15)));
        Assert.Equal(0.8, Value(quotient, January(15)));
        Assert.Equal(4, Value(minimum, January(10)));
        Assert.Equal(10, Value(maximum, January(1)));
        Assert.Equal(0, Value(equal, January(1)));
        Assert.Equal(ScheduleType.OnOff, equal.Type);
    }

    [Fact]
    public void ScheduleComparisonsLogicalOperationsAndPredicatesMatchUpstreamComposition()
    {
        Schedule values = Schedule.Constant("Values", -1).Apply(
            RuleSet.Constant("Positive", 2),
            January(2),
            January(3));
        Schedule threshold = Schedule.Constant("Threshold", 1);

        Assert.Equal(1, Value(values.LessThan(threshold), January(1)));
        Assert.Equal(1, Value(values.LessThanOrEqual(2), January(2)));
        Assert.Equal(1, Value(values.GreaterThan(threshold), January(2)));
        Assert.Equal(1, Value(values.GreaterThanOrEqual(2), January(2)));
        Assert.Equal(1, Value(values.ElementNotEqual(threshold), January(1)));
        Assert.Equal(1, Value(values.IsPositive(), January(2)));
        Assert.Equal(1, Value(values.IsNegative(), January(1)));
        Assert.Equal(1, Value(values.IsNonzero(), January(1)));
        Assert.Equal(0, Value(values.IsZero(), January(1)));
        Assert.Equal(1, Value(values.IsBetween(-1, 2), January(1)));

        Schedule positive = values.IsPositive();
        Schedule inverted = !positive;
        Assert.Equal(1, Value(positive.IsOn(), January(2)));
        Assert.Equal(1, Value(positive.IsOff(), January(1)));
        Assert.Equal(0, Value(positive & inverted, January(1)));
        Assert.Equal(1, Value(positive | inverted, January(1)));
        Assert.Equal(1, Value(inverted, January(1)));
    }

    [Fact]
    public void ScheduleWhereUnifiesConditionAndBranchPeriods()
    {
        Schedule condition = Schedule.Constant("Condition", 0, ScheduleType.OnOff).Apply(
            RuleSet.Constant("On", 1, ScheduleType.OnOff),
            January(10),
            January(20));
        Schedule trueValues = Schedule.Constant("True", 20, ScheduleType.Temperature).Apply(
            RuleSet.Constant("True setback", 18, ScheduleType.Temperature),
            January(15),
            January(25));

        Schedule selected = Schedule.Where(
            condition,
            trueValues,
            16,
            "Selected",
            ScheduleType.Temperature);
        Schedule selectedLowerLevelBranches = Schedule.Where(
            condition,
            RuleSet.Constant("Rule branch", 22, ScheduleType.Temperature),
            DaySchedule.Constant("Day branch", 15, ScheduleType.Temperature),
            type: ScheduleType.Temperature);

        Assert.Equal(ScheduleType.Temperature, selected.Type);
        Assert.Equal(16, Value(selected, January(1)));
        Assert.Equal(20, Value(selected, January(10)));
        Assert.Equal(18, Value(selected, January(15)));
        Assert.Equal(16, Value(selected, January(21)));
        Assert.Equal(5, selected.Compactize().Count);
        Assert.Equal(22, Value(selectedLowerLevelBranches, January(10)));
        Assert.Equal(15, Value(selectedLowerLevelBranches, January(1)));
        Assert.Throws<ScheduleOperationException>(() => Schedule.Where(trueValues, 1, 0));
    }

    [Fact]
    public void PositiveAverageUsesOnlyPositiveIntervalValuesAcrossEffectiveCalendarDays()
    {
        double[] mixedValues = Enumerable.Repeat(2d, DaySchedule.FixedLength / 2)
            .Concat(Enumerable.Repeat(-4d, DaySchedule.FixedLength / 2))
            .ToArray();
        var mixedDay = new DaySchedule("Mixed", mixedValues);
        RuleSet mixedRuleSet = RuleSet.FromDaySchedule("Mixed rules", mixedDay);
        Schedule schedule = Schedule.Constant("Annual negative", -1).Apply(
            mixedRuleSet,
            January(1),
            January(1));

        Assert.Equal(2, schedule.PositiveAverage);
        Assert.Equal(0, Schedule.Constant("No positives", 0).PositiveAverage);
    }

    [Fact]
    public void RuleSetMinMaxIncludeUnusedFallbackSlotsAsPinnedPythonOracleDoes()
    {
        var ruleSet = new RuleSet(
            "Fully overridden",
            DaySchedule.Constant("Unused weekday extreme", -10),
            DaySchedule.Constant("Unused weekend extreme", 10),
            monday: DaySchedule.Constant("Monday", 1),
            tuesday: DaySchedule.Constant("Tuesday", 2),
            wednesday: DaySchedule.Constant("Wednesday", 3),
            thursday: DaySchedule.Constant("Thursday", 4),
            friday: DaySchedule.Constant("Friday", 5),
            saturday: DaySchedule.Constant("Saturday", 6),
            sunday: DaySchedule.Constant("Sunday", 7),
            holiday: DaySchedule.Constant("Holiday", 8));

        Assert.Equal(1, Value(ruleSet, DayOfWeek.Monday));
        Assert.Equal(7, Value(ruleSet, DayOfWeek.Sunday));
        Assert.Equal(-10, ruleSet.Minimum);
        Assert.Equal(10, ruleSet.Maximum);
        Schedule annual = new(
            "Annual",
            Enumerable.Repeat(ruleSet, Schedule.FixedLength));
        Assert.Equal(-10, annual.Minimum);
        Assert.Equal(10, annual.Maximum);
    }

    [Fact]
    public void ScheduleIntegralAndLegacyAverageMatchPinnedPythonOracle()
    {
        var ruleSet = new RuleSet(
            "Week pattern",
            DaySchedule.Constant("Weekday", 2),
            DaySchedule.Constant("Weekend", 4));
        Schedule schedule = new(
            "Annual",
            Enumerable.Repeat(ruleSet, Schedule.FixedLength));

        // Pinned Python 0.7.0 oracle: 261 weekdays and 104 weekend days in 2026.
        Assert.Equal(22512, schedule.IntegralHours);
        Assert.Equal(22512d / 365d, schedule.Average);
        Assert.Equal(2, schedule.Minimum);
        Assert.Equal(4, schedule.Maximum);
        Assert.Equal(8760, Schedule.Constant("One", 1).IntegralHours);
        Assert.Equal(24, Schedule.Constant("One", 1).Average);
    }

    [Fact]
    public void CompactAndWindowOverlapsUsePinnedPythonLaterEntryPrecedence()
    {
        RuleSet zero = RuleSet.Constant("Zero", 0);
        RuleSet early = RuleSet.Constant("Early", 1);
        RuleSet late = RuleSet.Constant("Late", 2);
        Schedule compact = Schedule.FromCompact(
            "Compact overlap",
            new[]
            {
                new SchedulePeriod(January(1), January(10), early),
                new SchedulePeriod(January(5), January(15), late),
            });
        Schedule windows = Schedule.FromWindows(
            "Window overlap",
            zero,
            new[]
            {
                new ScheduleWindow(January(1), January(10), early),
                new ScheduleWindow(January(5), January(15), late),
            });

        foreach (Schedule schedule in new[] { compact, windows })
        {
            Assert.Equal(1, Value(schedule, January(4)));
            Assert.Equal(2, Value(schedule, January(5)));
            Assert.Equal(2, Value(schedule, January(15)));
            Assert.Equal(0, Value(schedule, January(16)));
        }
    }

    [Fact]
    public void CompactScheduleUnificationSplitsAllInputsAtEveryBoundary()
    {
        RuleSet one = RuleSet.Constant("One", 1);
        RuleSet two = RuleSet.Constant("Two", 2);
        RuleSet three = RuleSet.Constant("Three", 3);
        Schedule first = Schedule.Constant("First", 1).Apply(two, January(10), January(20));
        Schedule second = Schedule.Constant("Second", 2).Apply(three, January(15), February(1));
        Schedule third = Schedule.Constant("Third", 3).Apply(one, January(12), January(12));

        (IReadOnlyList<SchedulePeriod> left, IReadOnlyList<SchedulePeriod> right) =
            Schedule.UnifyCompactizedSchedules(first.Compactize(), second.Compactize());
        IReadOnlyList<IReadOnlyList<SchedulePeriod>> many =
            Schedule.UnifyCompactizedSchedulesMany(
                first.Compactize(),
                second.Compactize(),
                third.Compactize());

        Assert.Equal(5, left.Count);
        Assert.Equal(left.Select(period => (period.Start, period.End)), right.Select(period => (period.Start, period.End)));
        Assert.Equal(3, many.Count);
        Assert.All(many, periods => Assert.Equal(7, periods.Count));
        Assert.Equal(many[0].Select(period => (period.Start, period.End)), many[2].Select(period => (period.Start, period.End)));
        Assert.Empty(Schedule.UnifyCompactizedSchedulesMany());

        IReadOnlyList<IReadOnlyList<SchedulePeriod>> unifiedOne =
            Schedule.UnifyCompactizedSchedulesMany(first.Compactize());
        Assert.Single(unifiedOne);
        Assert.Equal(first.Compactize(), unifiedOne[0]);
        IReadOnlyList<IReadOnlyList<SchedulePeriod>> oneEmpty =
            Schedule.UnifyCompactizedSchedulesMany(Array.Empty<SchedulePeriod>());
        Assert.Single(oneEmpty);
        Assert.Empty(oneEmpty[0]);
    }

    [Fact]
    public void CompactScheduleUnificationUsesFirstMatchForOverlapsAndRejectsGaps()
    {
        RuleSet first = RuleSet.Constant("First", 1);
        RuleSet second = RuleSet.Constant("Second", 2);
        var overlapping = new List<SchedulePeriod>
        {
            new(January(1), January(10), first),
            new(January(5), January(15), second),
        };

        IReadOnlyList<SchedulePeriod> unified =
            Assert.Single(Schedule.UnifyCompactizedSchedulesMany(overlapping));

        Assert.Equal(3, unified.Count);
        Assert.Equal((January(1), January(4), first), (unified[0].Start, unified[0].End, unified[0].RuleSet));
        Assert.Equal((January(5), January(10), first), (unified[1].Start, unified[1].End, unified[1].RuleSet));
        Assert.Equal((January(11), January(15), second), (unified[2].Start, unified[2].End, unified[2].RuleSet));
        Assert.Same(first, unified[1].RuleSet);

        var gapped = new List<SchedulePeriod>
        {
            new(January(1), January(5), first),
            new(January(10), January(15), second),
        };

        Assert.Throws<ArgumentException>(() => Schedule.UnifyCompactizedSchedulesMany(gapped));
        Assert.Throws<ArgumentException>(() => Schedule.UnifyCompactizedSchedules(gapped, gapped));
    }

    [Fact]
    public void InvalidTypesAndZeroDivisorsAreRejectedAtRuleSetAndScheduleLayers()
    {
        RuleSet real = RuleSet.Constant("Real", 2);
        RuleSet zero = RuleSet.Constant("Zero", 0);
        RuleSet fraction = RuleSet.Constant("Fraction", 0.5, ScheduleType.Fraction);
        RuleSet onOff = RuleSet.Constant("OnOff", 1, ScheduleType.OnOff);
        RuleSet dividedOnOff = onOff / 2;

        Assert.Equal(ScheduleType.Real, dividedOnOff.Type);
        Assert.Equal(0.5, Value(dividedOnOff, DayOfWeek.Monday));
        Assert.Throws<DivideByZeroException>(() => real / 0);
        Assert.Throws<DivideByZeroException>(() => onOff / 0);
        Assert.Throws<DivideByZeroException>(() => real / zero);
        Assert.Throws<DivideByZeroException>(() => 1 / zero);
        Assert.Throws<ScheduleOperationException>(() => onOff + 1);
        Assert.Throws<ScheduleOperationException>(() => real & onOff);
        Assert.Throws<ScheduleOperationException>(() => real.ElementMinimum(fraction));

        Schedule realSchedule = Schedule.Constant("Real annual", 2);
        Schedule zeroSchedule = Schedule.Constant("Zero annual", 0);
        Schedule fractionSchedule = Schedule.Constant("Fraction annual", 0.5, ScheduleType.Fraction);
        Schedule onOffSchedule = Schedule.Constant("OnOff annual", 1, ScheduleType.OnOff);
        Schedule dividedOnOffSchedule = onOffSchedule / 2;

        Assert.Equal(ScheduleType.Real, dividedOnOffSchedule.Type);
        Assert.Equal(0.5, Value(dividedOnOffSchedule, January(1)));
        Assert.Throws<DivideByZeroException>(() => realSchedule / 0);
        Assert.Throws<DivideByZeroException>(() => onOffSchedule / 0);
        Assert.Throws<DivideByZeroException>(() => realSchedule / zeroSchedule);
        Assert.Throws<DivideByZeroException>(() => 1 / zeroSchedule);
        Assert.Throws<ScheduleOperationException>(() => onOffSchedule + 1);
        Assert.Throws<ScheduleOperationException>(() => realSchedule & onOffSchedule);
        Assert.Throws<ScheduleOperationException>(() => realSchedule.ElementMinimum(fractionSchedule));
    }

    private static DateTime January(int day)
    {
        return new DateTime(Schedule.DefaultYear, 1, day);
    }

    private static DateTime February(int day)
    {
        return new DateTime(Schedule.DefaultYear, 2, day);
    }

    private static double Value(RuleSet ruleSet, DayOfWeek dayOfWeek)
    {
        return ruleSet.GetDaySchedule(dayOfWeek)[0];
    }

    private static double Value(Schedule schedule, DateTime date)
    {
        return schedule[date].GetDaySchedule(date.DayOfWeek)[0];
    }
}
