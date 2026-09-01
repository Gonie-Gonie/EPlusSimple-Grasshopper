using System.Globalization;
using System.Numerics;
using Dragons.InvisibleDragon.Profile;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class RuleSetScalarCompatibilityTests
{
    private const long FirstInexactInteger = 9_007_199_254_740_993L;

    [Fact]
    public void ScalarKindsUsePythonNamesForRuleSetsAndDaysIndependentOfCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            RuleSet source = CreateSource();

            RuleSet boolean = source.Add(true);
            RuleSet integer = source.Add(3);
            RuleSet floating = source.Add(3D);
            RuleSet compared = source.GreaterThanOrEqual(3L);
            RuleSet between = source.IsBetween(3L, 4D);

            AssertNames(boolean, "Source:ADD:True", "Weekday:ADD:True", "Monday:ADD:True");
            AssertNames(integer, "Source:ADD:3", "Weekday:ADD:3", "Monday:ADD:3");
            AssertNames(floating, "Source:ADD:3.0", "Weekday:ADD:3.0", "Monday:ADD:3.0");
            AssertNames(compared, "Source:GE:3", "Weekday:GE:3", "Monday:GE:3");
            AssertNames(
                between,
                "Source:GE:3:AND:Source:LE:4.0",
                "Weekday:GE:3:AND:Weekday:LE:4.0",
                "Monday:GE:3:AND:Monday:LE:4.0");
            Assert.Equal("Source:MUL:3.0", source.Multiply(3D).Name);
            Assert.Equal("3.0:SUB:Source", source.ReverseSubtract(3D).Name);
            Assert.Equal("3.0:DIV:Source", source.ReverseDivide(3D).Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void TypedOperatorsPreserveScalarKindsReverseNamesAndOverrideTopology()
    {
        RuleSet source = CreateSource();

        RuleSet addedBoolean = source + true;
        RuleSet reverseAddedBoolean = true + source;
        RuleSet subtractedInteger = source - 2;
        RuleSet reverseSubtractedInteger = 10L - source;
        RuleSet multipliedInteger = source * 2;
        RuleSet reverseMultipliedInteger = 2 * source;
        RuleSet dividedFloat = source / 2D;
        RuleSet reverseDividedInteger = 12 / source;
        RuleSet addedBigInteger = source + new BigInteger(2);

        Assert.Equal("Source:ADD:True", addedBoolean.Name);
        Assert.Equal("True:ADD:Source", reverseAddedBoolean.Name);
        Assert.Equal("Source:SUB:2", subtractedInteger.Name);
        Assert.Equal("10:SUB:Source", reverseSubtractedInteger.Name);
        Assert.Equal("Source:MUL:2", multipliedInteger.Name);
        Assert.Equal("Source:MUL:2", reverseMultipliedInteger.Name);
        Assert.Equal("Source:DIV:2.0", dividedFloat.Name);
        Assert.Equal("12:DIV:Source", reverseDividedInteger.Name);
        Assert.Equal("Source:ADD:2", addedBigInteger.Name);

        Assert.Equal(3, Value(addedBoolean, DayOfWeek.Tuesday));
        Assert.Equal(4, Value(addedBoolean, DayOfWeek.Monday));
        Assert.Equal(4, Value(multipliedInteger, DayOfWeek.Tuesday));
        Assert.Equal(6, Value(reverseMultipliedInteger, DayOfWeek.Monday));
        Assert.Equal(1, Value(dividedFloat, DayOfWeek.Tuesday));
        Assert.Equal(4, Value(reverseDividedInteger, DayOfWeek.Monday));
        Assert.Equal(5, Value(addedBigInteger, DayOfWeek.Monday));
        Assert.NotNull(addedBoolean.Monday);
        Assert.Null(addedBoolean.Tuesday);
        Assert.Null(addedBoolean.Holiday);
        Assert.Equal(2, Value(source, DayOfWeek.Tuesday));
    }

    [Fact]
    public void IntegerComparisonsAndBetweenKeepExactPythonSemanticsBeyondBinary64Precision()
    {
        RuleSet ordinary = RuleSet.Constant("Ordinary", 2);
        RuleSet boundary = RuleSet.Constant(
            "Boundary",
            9_007_199_254_740_992D);
        BigInteger bigIntegerBoundary = new(FirstInexactInteger);

        Assert.All(ordinary.ElementEqual(2).Weekdays, value => Assert.Equal(1, value));
        Assert.All(boundary.ElementEqual(FirstInexactInteger).Weekdays, value => Assert.Equal(0, value));
        Assert.All(boundary.ElementNotEqual(FirstInexactInteger).Weekdays, value => Assert.Equal(1, value));
        Assert.All(boundary.LessThan(FirstInexactInteger).Weekdays, value => Assert.Equal(1, value));
        Assert.All(boundary.LessThanOrEqual(FirstInexactInteger).Weekdays, value => Assert.Equal(1, value));
        Assert.All(boundary.GreaterThan(FirstInexactInteger).Weekdays, value => Assert.Equal(0, value));
        Assert.All(boundary.GreaterThanOrEqual(FirstInexactInteger).Weekdays, value => Assert.Equal(0, value));
        Assert.All(boundary.ElementEqual(bigIntegerBoundary).Weekdays, value => Assert.Equal(0, value));
        Assert.All(
            boundary.IsBetween(FirstInexactInteger, FirstInexactInteger).Weekdays,
            value => Assert.Equal(0, value));
        Assert.All(
            boundary.IsBetween(
                new BigInteger(9_007_199_254_740_992L),
                bigIntegerBoundary).Weekdays,
            value => Assert.Equal(1, value));

        Assert.Equal(
            "Boundary:EQ:9007199254740993",
            boundary.ElementEqual(FirstInexactInteger).Name);
        Assert.Equal(
            "Boundary:GE:9007199254740993:AND:Boundary:LE:9007199254740993",
            boundary.IsBetween(bigIntegerBoundary, bigIntegerBoundary).Name);
    }

    [Fact]
    public void CharacterAndDecimalOperandsAreRejectedInsteadOfBecomingNumbers()
    {
        RuleSet source = CreateSource();
        RuleSet condition = RuleSet.Constant("Condition", 1, ScheduleType.OnOff);

        Assert.Throws<ScheduleOperationException>(() => _ = source + 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' + source);
        Assert.Throws<ScheduleOperationException>(() => _ = source - 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' - source);
        Assert.Throws<ScheduleOperationException>(() => _ = source * 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' * source);
        Assert.Throws<ScheduleOperationException>(() => _ = source / 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' / source);
        Assert.Throws<ScheduleOperationException>(() => source.Add(1M));
        Assert.Throws<ScheduleOperationException>(() => source.ElementEqual('A'));
        Assert.Throws<ScheduleOperationException>(() => source.ElementMaximum(1M));
        Assert.Throws<ScheduleOperationException>(() => source.IsBetween('A', 3));
        Assert.Throws<ScheduleOperationException>(() => source.IsBetween(1, 3M));
        Assert.Throws<ArgumentException>(() => RuleSet.Where(condition, 'A', 0));
        Assert.Throws<ArgumentException>(() => RuleSet.Where(condition, 1, 0M));
    }

    [Fact]
    public void ScalarMinimumAndMaximumImplementTheDeclaredUpstreamRepair()
    {
        RuleSet source = CreateSource();
        RuleSet minimum = source.ElementMinimum(true);
        RuleSet maximum = source.ElementMaximum(3L);
        RuleSet boundary = RuleSet.Constant("Boundary", 9_007_199_254_740_992D);
        RuleSet high = RuleSet.Constant("High", 9_007_199_254_740_994D);
        BigInteger firstInexact = new(FirstInexactInteger);

        AssertNames(minimum, "Source:MIN:True", "Weekday:MIN:True", "Monday:MIN:True");
        AssertNames(maximum, "Source:MAX:3", "Weekday:MAX:3", "Monday:MAX:3");
        Assert.Equal(1, Value(minimum, DayOfWeek.Tuesday));
        Assert.Equal(1, Value(minimum, DayOfWeek.Monday));
        Assert.Equal(3, Value(maximum, DayOfWeek.Tuesday));
        Assert.Equal(3, Value(maximum, DayOfWeek.Monday));

        RuleSet roundedMaximum = boundary.ElementMaximum(firstInexact);
        RuleSet roundedMinimum = high.ElementMinimum(firstInexact);
        Assert.All(
            roundedMaximum.Weekdays,
            value => Assert.Equal(9_007_199_254_740_992D, value));
        Assert.All(
            roundedMinimum.Weekdays,
            value => Assert.Equal(9_007_199_254_740_992D, value));
        Assert.Equal("Boundary:MAX:9007199254740993", roundedMaximum.Name);
        Assert.Equal("High:MIN:9007199254740993", roundedMinimum.Name);

        BigInteger overflowingInteger = BigInteger.Pow(10, 400);
        Assert.Throws<OverflowException>(() => boundary.ElementMaximum(overflowingInteger));
        Assert.Throws<ScheduleOperationException>(() =>
            RuleSet.Constant("OnOff", 1, ScheduleType.OnOff).ElementMinimum(0));
    }

    [Fact]
    public void WhereAcceptsBooleanAndLargeIntegerBranchesAndPreservesOverrideTopology()
    {
        var condition = new RuleSet(
            "Condition",
            DaySchedule.Constant("Condition weekday", 1, ScheduleType.OnOff),
            DaySchedule.Constant("Condition weekend", 0, ScheduleType.OnOff),
            monday: DaySchedule.Constant("Condition Monday", 0, ScheduleType.OnOff),
            type: ScheduleType.OnOff);
        var falseValues = new RuleSet(
            "False",
            DaySchedule.Constant("False weekday", 4),
            DaySchedule.Constant("False weekend", 5),
            tuesday: DaySchedule.Constant("False Tuesday", 6));

        RuleSet selected = RuleSet.Where(
            condition,
            FirstInexactInteger,
            falseValues,
            "Selected");
        RuleSet boolean = RuleSet.Where(
            condition,
            true,
            false,
            "Boolean",
            ScheduleType.OnOff);

        Assert.Equal(ScheduleType.Real, selected.Type);
        Assert.Equal("Selected:weekdays", selected.Weekdays.Name);
        Assert.Equal("Selected:weekends", selected.Weekends.Name);
        Assert.Equal("Selected:monday", selected.Monday?.Name);
        Assert.Equal("Selected:tuesday", selected.Tuesday?.Name);
        Assert.Equal(9_007_199_254_740_992D, Value(selected, DayOfWeek.Wednesday));
        Assert.Equal(5, Value(selected, DayOfWeek.Saturday));
        Assert.Equal(4, Value(selected, DayOfWeek.Monday));
        Assert.Equal(9_007_199_254_740_992D, Value(selected, DayOfWeek.Tuesday));
        Assert.NotNull(selected.Monday);
        Assert.NotNull(selected.Tuesday);
        Assert.Null(selected.Wednesday);
        Assert.Null(selected.Holiday);

        Assert.Equal(ScheduleType.OnOff, boolean.Type);
        Assert.Equal(1, Value(boolean, DayOfWeek.Wednesday));
        Assert.Equal(0, Value(boolean, DayOfWeek.Monday));
        Assert.Equal(0, Value(boolean, DayOfWeek.Saturday));
        Assert.NotNull(boolean.Monday);
        Assert.Null(boolean.Tuesday);
    }

    [Fact]
    public void WhereCoercionIsEagerForHugeIntegersAndRejectsNonfiniteValues()
    {
        RuleSet allFalse = RuleSet.Constant("All false", 0, ScheduleType.OnOff);
        RuleSet allTrue = RuleSet.Constant("All true", 1, ScheduleType.OnOff);
        BigInteger overflowingInteger = BigInteger.Pow(10, 400);

        Assert.Throws<OverflowException>(() =>
            RuleSet.Where(allFalse, overflowingInteger, 0, "Unselected huge"));
        Assert.Throws<OverflowException>(() =>
            RuleSet.Where(allTrue, 0, overflowingInteger, "Selected huge"));

        foreach (double nonfinite in new[]
        {
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.NaN,
        })
        {
            RuleSet unselectedTrue = RuleSet.Where(
                allFalse,
                nonfinite,
                0,
                "Unselected true nonfinite");
            RuleSet unselectedFalse = RuleSet.Where(
                allTrue,
                0,
                nonfinite,
                "Unselected false nonfinite");
            Assert.Equal(0, Value(unselectedTrue, DayOfWeek.Monday));
            Assert.Equal(0, Value(unselectedFalse, DayOfWeek.Monday));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RuleSet.Where(allTrue, nonfinite, 0, "Selected true nonfinite"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RuleSet.Where(allFalse, 0, nonfinite, "Selected false nonfinite"));
        }

        Assert.Equal(0, Value(allFalse, DayOfWeek.Monday));
        Assert.Equal(1, Value(allTrue, DayOfWeek.Monday));
    }

    [Fact]
    public void WhereEagerlyValidatesUnselectedScalarsForExplicitBoundedTypes()
    {
        RuleSet allFalse = RuleSet.Constant("All false", 0, ScheduleType.OnOff);
        RuleSet allTrue = RuleSet.Constant("All true", 1, ScheduleType.OnOff);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuleSet.Where(allTrue, 0.5, 1.1, "Invalid false fraction", ScheduleType.Fraction));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuleSet.Where(allFalse, -0.1, 0.5, "Invalid true fraction", ScheduleType.Fraction));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuleSet.Where(allTrue, 1, 0.5, "Invalid false onoff", ScheduleType.OnOff));

        foreach (double nonfinite in new[]
        {
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.NaN,
        })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RuleSet.Where(
                    allFalse,
                    nonfinite,
                    0.5,
                    "Unselected nonfinite fraction",
                    ScheduleType.Fraction));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RuleSet.Where(
                    allTrue,
                    20,
                    nonfinite,
                    "Unselected nonfinite temperature",
                    ScheduleType.Temperature));
        }
    }

    [Fact]
    public void WhereNormalizesOnOffNegativeZeroAndPythonEmptyDefaultName()
    {
        RuleSet allTrue = RuleSet.Constant("All true", 1, ScheduleType.OnOff);

        RuleSet result = RuleSet.Where(
            allTrue,
            -0d,
            1d,
            string.Empty,
            ScheduleType.OnOff);

        Assert.Equal("WHERE", result.Name);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(Value(result, DayOfWeek.Monday)));
    }

    [Fact]
    public void ResultNamesRejectWhitespaceUnderTheNativeEnergyPlusSafetyPolicy()
    {
        RuleSet allTrue = RuleSet.Constant("All true", 1, ScheduleType.OnOff);
        RuleSet source = CreateSource();

        Assert.Throws<ArgumentException>(() =>
            RuleSet.Where(allTrue, 1, 0, "   ", ScheduleType.OnOff));
        Assert.Throws<ArgumentException>(() => source.NormalizeByMaximum(string.Empty));
        Assert.Throws<ArgumentException>(() => source.NormalizeByMaximum("   "));
    }

    [Fact]
    public void ResultNamesNormalizeSurroundingWhitespaceBeforeBuildingChildNames()
    {
        RuleSet allTrue = RuleSet.Constant("All true", 1, ScheduleType.OnOff);
        RuleSet source = CreateSource();

        RuleSet selected = RuleSet.Where(
            allTrue,
            1,
            0,
            "  Selected  ",
            ScheduleType.OnOff);
        RuleSet normalized = source.NormalizeByMaximum("  Normalized  ");

        Assert.Equal("Selected", selected.Name);
        Assert.Equal("Selected:weekdays", selected.Weekdays.Name);
        Assert.Equal("Normalized", normalized.Name);
    }

    private static RuleSet CreateSource()
    {
        return new RuleSet(
            "Source",
            DaySchedule.Constant("Weekday", 2),
            DaySchedule.Constant("Weekend", 4),
            monday: DaySchedule.Constant("Monday", 3));
    }

    private static void AssertNames(
        RuleSet ruleSet,
        string expectedRuleSet,
        string expectedWeekday,
        string expectedMonday)
    {
        Assert.Equal(expectedRuleSet, ruleSet.Name);
        Assert.Equal(expectedWeekday, ruleSet.Weekdays.Name);
        Assert.Equal(expectedMonday, ruleSet.Monday?.Name);
    }

    private static double Value(RuleSet ruleSet, DayOfWeek dayOfWeek)
    {
        return ruleSet.GetDaySchedule(dayOfWeek)[0];
    }
}
