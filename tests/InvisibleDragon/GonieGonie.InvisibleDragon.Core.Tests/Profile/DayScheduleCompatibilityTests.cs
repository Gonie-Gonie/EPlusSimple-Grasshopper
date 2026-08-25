using GonieGonie.InvisibleDragon.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Profile;

public sealed class DayScheduleCompatibilityTests
{
    [Fact]
    public void ReverseArithmeticPreservesUpstreamOperandOrderNamesAndTypes()
    {
        DaySchedule real = Pattern("Real", ScheduleType.Real, "kW", 2, 4);
        DaySchedule fraction = Pattern("Fraction", ScheduleType.Fraction, null, 0.25, 0.75);
        DaySchedule temperature = Pattern("Temperature", ScheduleType.Temperature, null, 20, 22);

        DaySchedule reverseDifference = 10 - real;
        DaySchedule reverseQuotient = 12 / real;

        AssertPattern(reverseDifference, 8, 6);
        Assert.Equal("Real:SUB:10", reverseDifference.Name);
        Assert.Equal(ScheduleType.Real, reverseDifference.Type);
        Assert.Null(reverseDifference.Unit);
        AssertPattern(reverseQuotient, 6, 3);
        Assert.Equal("Real", reverseQuotient.Name);
        Assert.Equal(ScheduleType.Real, reverseQuotient.Type);
        Assert.Null(reverseQuotient.Unit);
        AssertPattern(1 - fraction, 0.75, 0.25);
        Assert.Equal(ScheduleType.Fraction, (1 - fraction).Type);
        AssertPattern(30 - temperature, 10, 8);
        Assert.Equal(ScheduleType.Temperature, (30 - temperature).Type);
    }

    [Fact]
    public void ReverseArithmeticEnforcesUpstreamTypeAndZeroDivisorRules()
    {
        DaySchedule onOff = Pattern("OnOff", ScheduleType.OnOff, null, 0, 1);
        DaySchedule fraction = DaySchedule.Constant("Fraction", 0.5, ScheduleType.Fraction);
        DaySchedule realWithZero = Pattern("Real zero", ScheduleType.Real, null, 0, 2);

        Assert.Throws<ScheduleOperationException>(() => 1 - onOff);
        Assert.Throws<ScheduleOperationException>(() => 1 / onOff);
        Assert.Throws<ScheduleOperationException>(() => 1 / fraction);
        Assert.Throws<DivideByZeroException>(() => 1 / realWithZero);
    }

    [Fact]
    public void ForwardScalarArithmeticAndInversionMatchUpstreamTypesAndNames()
    {
        DaySchedule real = Pattern("Real", ScheduleType.Real, null, 2, 4);
        DaySchedule onOff = Pattern("OnOff", ScheduleType.OnOff, null, 0, 1);

        DaySchedule added = real + 3;
        DaySchedule subtracted = real - 1;
        DaySchedule dividedOnOff = onOff / 2;
        DaySchedule inverted = !onOff;

        AssertPattern(added, 5, 7);
        Assert.Equal("Real:ADD:3", added.Name);
        AssertPattern(subtracted, 1, 3);
        Assert.Equal("Real:SUB:1", subtracted.Name);
        AssertPattern(dividedOnOff, 0, 0.5);
        Assert.Equal(ScheduleType.Real, dividedOnOff.Type);
        Assert.Equal("OnOff", dividedOnOff.Name);
        AssertPattern(inverted, 1, 0);
        Assert.Equal("OnOff:INVERTED", inverted.Name);
        Assert.Throws<DivideByZeroException>(() => onOff / 0);
    }

    [Fact]
    public void AlgebraResultsDropUnitsWithoutMutatingTheirOperands()
    {
        DaySchedule left = Pattern("Left", ScheduleType.Real, "kW", 2, 4);
        DaySchedule right = Pattern("Right", ScheduleType.Real, "kW", 1, 2);
        DaySchedule onOffLeft = Pattern("OnOff left", ScheduleType.OnOff, "flag", 0, 1);
        DaySchedule onOffRight = Pattern("OnOff right", ScheduleType.OnOff, "flag", 1, 0);

        DaySchedule[] results =
        {
            left * 2,
            left / 2,
            left + 1,
            left - 1,
            left * right,
            left / right,
            left + right,
            left - right,
            onOffLeft & onOffRight,
            onOffLeft | onOffRight,
            !onOffLeft,
        };

        Assert.All(results, result => Assert.Null(result.Unit));
        Assert.Equal("kW", left.Unit);
        Assert.Equal("kW", right.Unit);
        Assert.Equal("flag", onOffLeft.Unit);
        Assert.Equal("flag", onOffRight.Unit);
        AssertPattern(left, 2, 4);
        AssertPattern(right, 1, 2);
        AssertPattern(onOffLeft, 0, 1);
        AssertPattern(onOffRight, 1, 0);
    }

    [Fact]
    public void NormalizeByMaximumUsesScalarDivisionTypeAndKeepsSourceImmutable()
    {
        DaySchedule onOff = Pattern("OnOff", ScheduleType.OnOff, "flag", 0, 1);
        DaySchedule real = Pattern("Real", ScheduleType.Real, "kW", 2, 4);

        DaySchedule normalizedOnOff = onOff.NormalizeByMaximum();
        DaySchedule normalizedReal = real.NormalizeByMaximum("Normalized real");

        AssertPattern(normalizedOnOff, 0, 1);
        Assert.Equal("OnOff_normalized", normalizedOnOff.Name);
        Assert.Equal(ScheduleType.Real, normalizedOnOff.Type);
        Assert.Null(normalizedOnOff.Unit);
        AssertPattern(normalizedReal, 0.5, 1);
        Assert.Equal("Normalized real", normalizedReal.Name);
        Assert.Equal(ScheduleType.Real, normalizedReal.Type);
        Assert.Null(normalizedReal.Unit);
        AssertPattern(onOff, 0, 1);
        Assert.Equal(ScheduleType.OnOff, onOff.Type);
        Assert.Equal("flag", onOff.Unit);
        AssertPattern(real, 2, 4);
        Assert.Equal("kW", real.Unit);
    }

    [Theory]
    [InlineData("eq", new double[] { 0, 1, 0, 0 })]
    [InlineData("ne", new double[] { 1, 0, 1, 1 })]
    [InlineData("lt", new double[] { 1, 0, 0, 1 })]
    [InlineData("le", new double[] { 1, 1, 0, 1 })]
    [InlineData("gt", new double[] { 0, 0, 1, 0 })]
    [InlineData("ge", new double[] { 0, 1, 1, 0 })]
    public void ScheduleComparisonsAreElementWiseAcrossScheduleTypes(
        string operation,
        double[] expected)
    {
        DaySchedule left = Pattern("Left", ScheduleType.Real, "ignored", 1, 2, 3, 2);
        DaySchedule right = Pattern("Right", ScheduleType.Temperature, "ignored", 2, 2, 2, 3);

        DaySchedule result = operation switch
        {
            "eq" => left.ElementEqual(right),
            "ne" => left.ElementNotEqual(right),
            "lt" => left.LessThan(right),
            "le" => left.LessThanOrEqual(right),
            "gt" => left.GreaterThan(right),
            "ge" => left.GreaterThanOrEqual(right),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        AssertPattern(result, expected);
        Assert.Equal(ScheduleType.OnOff, result.Type);
        Assert.Null(result.Unit);
        Assert.Equal($"Left:{operation.ToUpperInvariant()}:Right", result.Name);
    }

    [Fact]
    public void ScalarComparisonsRetainPythonNanSemantics()
    {
        DaySchedule value = DaySchedule.Constant("Value", 1);

        Assert.All(value.ElementEqual(double.NaN), item => Assert.Equal(0, item));
        Assert.All(value.ElementNotEqual(double.NaN), item => Assert.Equal(1, item));
        Assert.All(value.LessThan(double.NaN), item => Assert.Equal(0, item));
        Assert.All(value.LessThanOrEqual(double.NaN), item => Assert.Equal(0, item));
        Assert.All(value.GreaterThan(double.NaN), item => Assert.Equal(0, item));
        Assert.All(value.GreaterThanOrEqual(double.NaN), item => Assert.Equal(0, item));
    }

    [Fact]
    public void ElementExtremaSupportScalarAndMatchingScheduleOperands()
    {
        DaySchedule left = Pattern("Left", ScheduleType.Real, "kW", 1, 3);
        DaySchedule right = Pattern("Right", ScheduleType.Real, "kW", 2, 2);

        DaySchedule scheduleMinimum = left.ElementMinimum(right);
        DaySchedule scheduleMaximum = left.ElementMaximum(right);
        DaySchedule scalarMinimum = left.ElementMinimum(2);
        DaySchedule scalarMaximum = left.ElementMaximum(2);

        AssertPattern(scheduleMinimum, 1, 2);
        AssertPattern(scheduleMaximum, 2, 3);
        AssertPattern(scalarMinimum, 1, 2);
        AssertPattern(scalarMaximum, 2, 3);
        Assert.Equal("Left:MIN:Right", scheduleMinimum.Name);
        Assert.Equal("Left:MAX:2", scalarMaximum.Name);
        Assert.All(
            new[] { scheduleMinimum, scheduleMaximum, scalarMinimum, scalarMaximum },
            result => Assert.Null(result.Unit));
    }

    [Fact]
    public void ElementExtremaEnforceSameNonOnOffTypeAndResultDomain()
    {
        DaySchedule real = DaySchedule.Constant("Real", 1, ScheduleType.Real);
        DaySchedule temperature = DaySchedule.Constant("Temperature", 20, ScheduleType.Temperature);
        DaySchedule onOff = DaySchedule.Constant("OnOff", 1, ScheduleType.OnOff);
        DaySchedule fraction = DaySchedule.Constant("Fraction", 0.5, ScheduleType.Fraction);

        Assert.Throws<ScheduleOperationException>(() => real.ElementMinimum(temperature));
        Assert.Throws<ScheduleOperationException>(() => real.ElementMaximum(temperature));
        Assert.Throws<ScheduleOperationException>(() => onOff.ElementMinimum(0));
        Assert.Throws<ScheduleOperationException>(() => onOff.ElementMaximum(onOff));
        Assert.Throws<ArgumentOutOfRangeException>(() => fraction.ElementMinimum(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fraction.ElementMaximum(1.1));
    }

    [Fact]
    public void WhereSupportsEveryScheduleAndScalarBranchPair()
    {
        DaySchedule condition = Pattern("Condition", ScheduleType.OnOff, null, 0, 1);
        DaySchedule whenTrue = Pattern("True", ScheduleType.Fraction, "fraction", 0.2, 0.4);
        DaySchedule whenFalse = Pattern("False", ScheduleType.Fraction, "fraction", 0.8, 0.6);

        DaySchedule scheduleSchedule = DaySchedule.Where(
            condition,
            whenTrue,
            whenFalse,
            "Schedule schedule",
            ScheduleType.Fraction);
        DaySchedule scheduleScalar = DaySchedule.Where(condition, whenTrue, 0.1);
        DaySchedule scalarSchedule = DaySchedule.Where(condition, 0.3, whenFalse);
        DaySchedule scalarScalar = DaySchedule.Where(condition, 3, 8);

        AssertPattern(scheduleSchedule, 0.8, 0.4);
        AssertPattern(scheduleScalar, 0.1, 0.4);
        AssertPattern(scalarSchedule, 0.8, 0.3);
        AssertPattern(scalarScalar, 8, 3);
        Assert.Equal("Schedule schedule", scheduleSchedule.Name);
        Assert.Equal(ScheduleType.Fraction, scheduleSchedule.Type);
        Assert.Equal(ScheduleType.Fraction, scheduleScalar.Type);
        Assert.Equal(ScheduleType.Fraction, scalarSchedule.Type);
        Assert.Equal(ScheduleType.Real, scalarScalar.Type);
        Assert.Null(scheduleSchedule.Unit);
    }

    [Fact]
    public void WhereEnforcesConditionInferredTypeExplicitTypeAndScalarDomain()
    {
        DaySchedule condition = Pattern("Condition", ScheduleType.OnOff, null, 0, 1);
        DaySchedule alwaysTrue = DaySchedule.Constant("Always true", 1, ScheduleType.OnOff);
        DaySchedule badCondition = DaySchedule.Constant("Bad condition", 1, ScheduleType.Real);
        DaySchedule fraction = DaySchedule.Constant("Fraction", 0.5, ScheduleType.Fraction);
        DaySchedule real = DaySchedule.Constant("Real", 2, ScheduleType.Real);

        Assert.Throws<ScheduleOperationException>(
            () => DaySchedule.Where(badCondition, 1, 0));
        Assert.Throws<ScheduleOperationException>(
            () => DaySchedule.Where(condition, fraction, real));
        Assert.Throws<ScheduleOperationException>(
            () => DaySchedule.Where(condition, fraction, 0.1, type: ScheduleType.Real));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DaySchedule.Where(condition, 0.5, 1.1, type: ScheduleType.Fraction));

        DaySchedule unusedInvalidBranch = DaySchedule.Where(
            alwaysTrue,
            0.5,
            1.1,
            type: ScheduleType.Fraction);
        Assert.All(unusedInvalidBranch, value => Assert.Equal(0.5, value));
    }

    [Fact]
    public void PredicatesMatchPythonMasksForNegativeZeroOneAndPositiveValues()
    {
        DaySchedule values = Pattern("Values", ScheduleType.Real, null, -2, 0, 1, 2);

        AssertPattern(values.IsOn(), 0, 0, 1, 0);
        AssertPattern(values.IsOff(), 0, 1, 0, 0);
        AssertPattern(values.IsPositive(), 0, 0, 1, 1);
        AssertPattern(values.IsNegative(), 1, 0, 0, 0);
        AssertPattern(values.IsZero(), 0, 1, 0, 0);
        AssertPattern(values.IsNonzero(), 1, 0, 1, 1);
    }

    [Theory]
    [InlineData(true, true, new double[] { 0, 1, 1, 0 })]
    [InlineData(false, true, new double[] { 0, 0, 1, 0 })]
    [InlineData(true, false, new double[] { 0, 1, 0, 0 })]
    [InlineData(false, false, new double[] { 0, 0, 0, 0 })]
    public void IsBetweenHonorsIndependentBoundaryModes(
        bool includeMinimum,
        bool includeMaximum,
        double[] expected)
    {
        DaySchedule values = Pattern("Values", ScheduleType.Real, null, -1, 0, 1, 2);

        DaySchedule result = values.IsBetween(0, 1, includeMinimum, includeMaximum);

        AssertPattern(result, expected);
        Assert.Equal(ScheduleType.OnOff, result.Type);
        Assert.All(values.IsBetween(2, 0), item => Assert.Equal(0, item));
    }

    [Fact]
    public void PositiveStatisticsIgnoreZeroAndNegativeValues()
    {
        DaySchedule mixed = Pattern("Mixed", ScheduleType.Real, null, -2, 0, 1, 2);
        DaySchedule nonPositive = Pattern("Non-positive", ScheduleType.Real, null, -2, 0);
        DaySchedule zero = DaySchedule.Constant("Zero", 0);

        Assert.True(mixed.HasPositive);
        Assert.True(mixed.HasNonzero);
        Assert.Equal(1.5, mixed.PositiveAverage);
        Assert.False(nonPositive.HasPositive);
        Assert.True(nonPositive.HasNonzero);
        Assert.Equal(0, nonPositive.PositiveAverage);
        Assert.False(zero.HasPositive);
        Assert.False(zero.HasNonzero);
        Assert.Equal(0, zero.PositiveAverage);
    }

    private static DaySchedule Pattern(
        string name,
        ScheduleType type,
        string? unit,
        params double[] values)
    {
        return new DaySchedule(
            name,
            Enumerable.Range(0, DaySchedule.FixedLength)
                .Select(index => values[index % values.Length]),
            type,
            unit);
    }

    private static void AssertPattern(DaySchedule schedule, params double[] expected)
    {
        for (int index = 0; index < schedule.Count; index++)
        {
            Assert.Equal(expected[index % expected.Length], schedule[index]);
        }
    }
}
