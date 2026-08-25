using GonieGonie.InvisibleDragon.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Profile;

public sealed class ScheduleArithmeticTests
{
    [Fact]
    public void SequentialFractionsMatchUpstreamEpsilonFormulaExactly()
    {
        const double epsilonValue = 1e-10;
        Schedule one = Schedule.Constant("One", 1);
        Schedule two = Schedule.Constant("Two", 2);
        Schedule epsilon = Schedule.Constant("Epsilon", epsilonValue);

        Schedule firstFraction = one.Divide(two.Add(epsilon));
        Schedule secondFraction = one.Divide(one.Add(epsilon));

        Assert.Equal(1 / (2 + epsilonValue), Value(firstFraction, January(1)));
        Assert.Equal(1 / (1 + epsilonValue), Value(secondFraction, January(1)));
        Assert.Equal(ScheduleType.Real, firstFraction.Type);
        Assert.Equal(ScheduleType.Real, secondFraction.Type);
    }

    [Fact]
    public void DivideRejectsZeroButDoesNotTreatEpsilonAsZero()
    {
        Schedule numerator = Schedule.Constant("Numerator", 1);
        Schedule zero = Schedule.Constant("Zero", 0);
        Schedule epsilon = Schedule.Constant("Epsilon", 1e-10);

        Assert.Throws<DivideByZeroException>(() => numerator.Divide(zero));

        Schedule result = numerator.Divide(epsilon);

        Assert.Equal(1 / 1e-10, Value(result, January(1)));
    }

    [Fact]
    public void AnnualOperationsCombineCorrespondingDatesWithoutMutatingInputs()
    {
        Schedule left = Schedule.Constant("Left", 8).Apply(
            RuleSet.Constant("Left special", 9),
            January(2),
            January(2));
        Schedule right = Schedule.Constant("Right", 2).Apply(
            RuleSet.Constant("Right special", 3),
            January(2),
            January(2));

        Schedule sum = left.Add(right, "Sum");
        Schedule difference = left.Subtract(right, "Difference");
        Schedule quotient = left.Divide(right, "Quotient");

        Assert.Equal(10, Value(sum, January(1)));
        Assert.Equal(12, Value(sum, January(2)));
        Assert.Equal(6, Value(difference, January(1)));
        Assert.Equal(6, Value(difference, January(2)));
        Assert.Equal(4, Value(quotient, January(1)));
        Assert.Equal(3, Value(quotient, January(2)));
        Assert.Equal("Sum", sum.Name);
        Assert.Equal("Difference", difference.Name);
        Assert.Equal("Quotient", quotient.Name);
        Assert.Equal(8, Value(left, January(1)));
        Assert.Equal(9, Value(left, January(2)));
        Assert.Equal(2, Value(right, January(1)));
        Assert.Equal(3, Value(right, January(2)));
    }

    [Fact]
    public void AnnualOperationsPreserveDayScheduleTypeRules()
    {
        Schedule fraction = Schedule.Constant("Fraction", 0.25, ScheduleType.Fraction);
        Schedule otherFraction = Schedule.Constant("Other fraction", 0.5, ScheduleType.Fraction);
        Schedule temperature = Schedule.Constant("Temperature", 20, ScheduleType.Temperature);
        Schedule real = Schedule.Constant("Real", 2, ScheduleType.Real);
        Schedule onOff = Schedule.Constant("OnOff", 1, ScheduleType.OnOff);

        Assert.Equal(ScheduleType.Fraction, fraction.Add(otherFraction).Type);
        Assert.Equal(ScheduleType.Temperature, temperature.Subtract(real).Type);
        Assert.Equal(ScheduleType.Temperature, temperature.Divide(real).Type);
        Assert.Throws<ScheduleOperationException>(() => fraction.Add(real));
        Assert.Throws<ScheduleOperationException>(() => real.Divide(fraction));
        Assert.Throws<ScheduleOperationException>(() => onOff.Divide(real));
    }

    [Fact]
    public void RuleSetDivideResolvesOverridesBeforeApplyingDayScheduleDivision()
    {
        DaySchedule numeratorDefault = DaySchedule.Constant("Numerator default", 4);
        DaySchedule numeratorMonday = DaySchedule.Constant("Numerator Monday", 9);
        DaySchedule denominatorDefault = DaySchedule.Constant("Denominator default", 2);
        DaySchedule denominatorMonday = DaySchedule.Constant("Denominator Monday", 3);
        var numerator = new RuleSet(
            "Numerator",
            numeratorDefault,
            numeratorDefault,
            monday: numeratorMonday);
        var denominator = new RuleSet(
            "Denominator",
            denominatorDefault,
            denominatorDefault,
            monday: denominatorMonday);

        RuleSet quotient = numerator.Divide(denominator);

        Assert.Equal(3, quotient.GetDaySchedule(DayOfWeek.Monday)[0]);
        Assert.Equal(2, quotient.GetDaySchedule(DayOfWeek.Tuesday)[0]);
        Assert.Equal(ScheduleType.Real, quotient.Type);
    }

    private static DateTime January(int day)
    {
        return new DateTime(Schedule.DefaultYear, 1, day);
    }

    private static double Value(Schedule schedule, DateTime date)
    {
        return schedule[date].GetDaySchedule(date.DayOfWeek)[0];
    }
}
