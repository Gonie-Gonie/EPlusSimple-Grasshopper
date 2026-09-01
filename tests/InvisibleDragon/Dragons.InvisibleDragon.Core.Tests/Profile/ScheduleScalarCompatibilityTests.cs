using System.Globalization;
using System.Numerics;
using Dragons.InvisibleDragon.Profile;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class ScheduleScalarCompatibilityTests
{
    private static readonly DateTime January1 = new(Schedule.DefaultYear, 1, 1);

    [Fact]
    public void ScalarNamesArePythonExactUnderFrenchCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            Schedule source = Schedule.Constant("Source", 2);

            Assert.Equal("Source:ADD:2", source.Add(2).Name);
            Assert.Equal("Source:ADD:2.0", source.Add(2D).Name);
            Assert.Equal("Source:ADD:True", source.Add(true).Name);
            Assert.Equal("Source:ADD:-0.0", source.Add(-0D).Name);
            Assert.Equal(4, Value(source.Add(2), January1));
            Assert.Equal(3, Value(source.Add(true), January1));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void BigIntegerOperatorsKeepExactValuesNamesAndOperandDirection()
    {
        Schedule source = Schedule.Constant("Source", 2);
        BigInteger two = new(2);
        BigInteger three = new(3);
        BigInteger eight = new(8);

        AssertResult(source + three, "Source:ADD:3", 5);
        AssertResult(three + source, "3:ADD:Source", 5);
        AssertResult(source - three, "Source:SUB:3", -1);
        AssertResult(three - source, "3:SUB:Source", 1);
        AssertResult(source * three, "Source:MUL:3", 6);
        AssertResult(three * source, "Source:MUL:3", 6);
        AssertResult(source / two, "Source:DIV:2", 1);
        AssertResult(eight / source, "8:DIV:Source", 4);

        BigInteger firstInexact = BigInteger.Parse("9007199254740993", CultureInfo.InvariantCulture);
        Schedule boundary = Schedule.Constant("Boundary", 9_007_199_254_740_992D);
        Assert.Equal(0, Value(boundary.ElementEqual(firstInexact), January1));
        Assert.Equal(1, Value(boundary.LessThan(firstInexact), January1));
        Assert.Equal("Boundary:EQ:9007199254740993", boundary.ElementEqual(firstInexact).Name);
        Assert.Equal(2, Value(source, January1));
    }

    [Fact]
    public void UnsupportedCharacterAndDecimalOperandsAreRejectedByMethodsAndWhere()
    {
        Schedule source = Schedule.Constant("Source", 2);
        Schedule condition = Schedule.Constant("Condition", 1, ScheduleType.OnOff);

        Assert.Throws<ScheduleOperationException>(() => source.Add('A'));
        Assert.Throws<ScheduleOperationException>(() => source.Multiply(1M));
        Assert.Throws<ScheduleOperationException>(() => source.ElementEqual('A'));
        Assert.Throws<ScheduleOperationException>(() => source.IsBetween(1, 3M));
        Assert.Throws<ArgumentException>(() => Schedule.Where(condition, 'A', 0));
        Assert.Throws<ArgumentException>(() => Schedule.Where(condition, 1M, 0));
    }

    [Fact]
    public void ReverseMethodsAndGenericBetweenUsePythonNamesAndExactIntegerComparison()
    {
        Schedule source = Schedule.Constant("Source", 2);

        AssertResult(source.ReverseAdd(3L), "3:ADD:Source", 5);
        AssertResult(source.ReverseSubtract(5L), "5:SUB:Source", 3);
        AssertResult(source.ReverseDivide(8L), "8:DIV:Source", 4);
        AssertResult(3L * source, "Source:MUL:3", 6);

        BigInteger minimum = new(9_007_199_254_740_992L);
        BigInteger maximum = BigInteger.Parse("9007199254740993", CultureInfo.InvariantCulture);
        Schedule boundary = Schedule.Constant("Boundary", 9_007_199_254_740_992D);
        Schedule between = boundary.IsBetween(minimum, maximum);

        Assert.Equal(1, Value(between, January1));
        Assert.Equal(
            "Boundary:GE:9007199254740992:AND:Boundary:LE:9007199254740993",
            between.Name);
    }

    [Fact]
    public void WhereUsesPythonDefaultsAndNativeTrimmedNameSafety()
    {
        Schedule allTrue = Schedule.Constant("All true", 1, ScheduleType.OnOff);

        Assert.Equal("WHERE", Schedule.Where(allTrue, 1, 0).Name);
        Assert.Equal("WHERE", Schedule.Where(allTrue, 1, 0, string.Empty).Name);
        Assert.Equal("Selected", Schedule.Where(allTrue, 1, 0, "  Selected  ").Name);
        Assert.Throws<ArgumentException>(() => Schedule.Where(allTrue, 1, 0, "   "));

        Schedule negativeZero = Schedule.Where(
            allTrue,
            -0D,
            1D,
            string.Empty,
            ScheduleType.OnOff);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(Value(negativeZero, January1)));
    }

    [Fact]
    public void WhereEagerlyRejectsHugeIntegersButOnlyRejectsSelectedNonfiniteValues()
    {
        Schedule allFalse = Schedule.Constant("All false", 0, ScheduleType.OnOff);
        Schedule allTrue = Schedule.Constant("All true", 1, ScheduleType.OnOff);
        BigInteger huge = BigInteger.Pow(10, 400);

        Assert.Throws<OverflowException>(() => Schedule.Where(allFalse, huge, 0));
        Assert.Throws<OverflowException>(() => Schedule.Where(allTrue, 0, huge));

        foreach (double nonfinite in new[]
        {
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.NaN,
        })
        {
            Assert.Equal(0, Value(Schedule.Where(allFalse, nonfinite, 0), January1));
            Assert.Equal(0, Value(Schedule.Where(allTrue, 0, nonfinite), January1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Schedule.Where(allTrue, nonfinite, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Schedule.Where(allFalse, 0, nonfinite));
        }
    }

    [Fact]
    public void WhereUnifiesAsymmetricPeriodBoundariesWithoutMutatingSources()
    {
        RuleSet on = RuleSet.Constant("On", 1, ScheduleType.OnOff);
        RuleSet trueWindow = RuleSet.Constant("True window", 3);
        RuleSet falseWindow = RuleSet.Constant("False window", 20);
        Schedule condition = Schedule.Constant("Condition", 0, ScheduleType.OnOff)
            .Apply(on, January(10), January(20));
        Schedule whenTrue = Schedule.Constant("True", 2)
            .Apply(trueWindow, January(15), January(25));
        Schedule whenFalse = Schedule.Constant("False", 10)
            .Apply(falseWindow, January(5), January(12));
        RuleSet originalCondition = condition[January(10)];
        RuleSet originalTrue = whenTrue[January(15)];
        RuleSet originalFalse = whenFalse[January(5)];

        Schedule result = Schedule.Where(condition, whenTrue, whenFalse, "Mixed");

        Assert.Equal(new[] { 10D, 20D, 2D, 2D, 3D, 10D, 10D }, new[]
        {
            Value(result, January(1)),
            Value(result, January(5)),
            Value(result, January(10)),
            Value(result, January(13)),
            Value(result, January(15)),
            Value(result, January(21)),
            Value(result, January(26)),
        });
        Assert.Equal(
            new[] { 1, 5, 10, 13, 15, 21, 26 },
            result.Compactize().Select(period => period.Start.Day).ToArray());

        Assert.Equal("Condition", condition.Name);
        Assert.Equal("True", whenTrue.Name);
        Assert.Equal("False", whenFalse.Name);
        Assert.Same(originalCondition, condition[January(10)]);
        Assert.Same(originalTrue, whenTrue[January(15)]);
        Assert.Same(originalFalse, whenFalse[January(5)]);
        Assert.Equal(0, Value(condition, January(1)));
        Assert.Equal(2, Value(whenTrue, January(1)));
        Assert.Equal(10, Value(whenFalse, January(1)));
    }

    private static DateTime January(int day) => new(Schedule.DefaultYear, 1, day);

    private static double Value(Schedule schedule, DateTime date)
    {
        return schedule[date].GetDaySchedule(date.DayOfWeek)[0];
    }

    private static void AssertResult(Schedule schedule, string expectedName, double expectedValue)
    {
        Assert.Equal(expectedName, schedule.Name);
        Assert.Equal(expectedValue, Value(schedule, January1));
    }
}
