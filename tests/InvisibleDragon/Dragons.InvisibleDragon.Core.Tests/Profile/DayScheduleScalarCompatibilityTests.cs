using System.Globalization;
using System.Numerics;
using Dragons.InvisibleDragon.Profile;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class DayScheduleScalarCompatibilityTests
{
    [Fact]
    public void ScalarKindsUsePythonNamesIndependentOfCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            DaySchedule schedule = Constant("Real", 2);

            Assert.Equal("Real:ADD:3", (schedule + 3).Name);
            Assert.Equal("Real:ADD:3", (schedule + 3U).Name);
            Assert.Equal("Real:ADD:3", (schedule + 3L).Name);
            Assert.Equal("Real:ADD:3", (schedule + 3UL).Name);
            Assert.Equal("Real:ADD:3.0", (schedule + 3F).Name);
            Assert.Equal("Real:ADD:3.0", (schedule + 3D).Name);
            Assert.Equal("Real:ADD:True", (schedule + true).Name);
            Assert.Equal("Real:GE:3:AND:Real:LE:4.0", schedule.IsBetween(3L, 4D).Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData(5908597336708864211L, "9.808016357692319e+86")]
    [InlineData(4696062752294938894L, "909848.6278965787")]
    [InlineData(-4376334117423966200L, "-1.1336640015052816e+16")]
    [InlineData(0x3e70000000000000L, "5.960464477539063e-08")]
    [InlineData(0x0060000000000000L, "7.120236347223045e-307")]
    public void ScalarNamesUseCpythonShortestRoundTripFormatting(long bits, string expected)
    {
        DaySchedule schedule = Constant("Real", 0);
        double value = BitConverter.Int64BitsToDouble(bits);

        Assert.Equal($"Real:EQ:{expected}", schedule.ElementEqual(value).Name);
    }

    [Fact]
    public void CharacterOperandsAreRejectedAcrossScalarOperations()
    {
        DaySchedule real = Constant("Real", 2);
        DaySchedule condition = Constant("Condition", 1, ScheduleType.OnOff);

        Assert.Throws<ScheduleOperationException>(() => _ = real + 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' + real);
        Assert.Throws<ScheduleOperationException>(() => _ = real - 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' - real);
        Assert.Throws<ScheduleOperationException>(() => _ = real * 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' * real);
        Assert.Throws<ScheduleOperationException>(() => _ = real / 'A');
        Assert.Throws<ScheduleOperationException>(() => _ = 'A' / real);
        Assert.Throws<ScheduleOperationException>(() => real.ElementEqual('A'));
        Assert.Throws<ScheduleOperationException>(() => real.ElementNotEqual('A'));
        Assert.Throws<ScheduleOperationException>(() => real.LessThan('A'));
        Assert.Throws<ScheduleOperationException>(() => real.LessThanOrEqual('A'));
        Assert.Throws<ScheduleOperationException>(() => real.GreaterThan('A'));
        Assert.Throws<ScheduleOperationException>(() => real.GreaterThanOrEqual('A'));
        Assert.Throws<ScheduleOperationException>(() => real.ElementMinimum('A'));
        Assert.Throws<ScheduleOperationException>(() => real.ElementMaximum('A'));
        Assert.Throws<ScheduleOperationException>(() => real.IsBetween('A', 3));
        Assert.Throws<ScheduleOperationException>(() => real.IsBetween(1, 'A'));
        Assert.Throws<ScheduleOperationException>(() =>
            DaySchedule.Where(condition, real, 'A', "Where"));
        Assert.Throws<ScheduleOperationException>(() =>
            DaySchedule.Where(condition, 'A', real, "Where"));
        Assert.Throws<ScheduleOperationException>(() =>
            DaySchedule.Where(condition, 'A', 'B', "Where"));
    }

    [Fact]
    public void LargeIntegerComparisonsKeepPythonExactIntegerSemantics()
    {
        DaySchedule schedule = Constant("Boundary", 9_007_199_254_740_992D);
        const long nextInteger = 9_007_199_254_740_993L;

        Assert.All(schedule.ElementEqual(nextInteger), value => Assert.Equal(0, value));
        Assert.All(schedule.ElementNotEqual(nextInteger), value => Assert.Equal(1, value));
        Assert.All(schedule.LessThan(nextInteger), value => Assert.Equal(1, value));
        Assert.All(schedule.LessThanOrEqual(nextInteger), value => Assert.Equal(1, value));
        Assert.All(schedule.GreaterThan(nextInteger), value => Assert.Equal(0, value));
        Assert.All(schedule.GreaterThanOrEqual(nextInteger), value => Assert.Equal(0, value));
        Assert.All(schedule.IsBetween(nextInteger, nextInteger), value => Assert.Equal(0, value));
        Assert.Equal("Boundary:EQ:9007199254740993", schedule.ElementEqual(nextInteger).Name);
    }

    [Fact]
    public void IntegerMaterializationUsesPythonFloatCoercionOnlyWhenSelected()
    {
        const long inexactInteger = 9_007_199_254_740_993L;
        DaySchedule low = Constant("Low", 9_007_199_254_740_992D);
        DaySchedule high = Constant("High", 9_007_199_254_740_994D);
        DaySchedule allTrue = Constant("True", 1, ScheduleType.OnOff);
        DaySchedule allFalse = Constant("False", 0, ScheduleType.OnOff);

        DaySchedule maximum = low.ElementMaximum(inexactInteger);
        DaySchedule minimum = high.ElementMinimum(inexactInteger);
        DaySchedule selectedTrue = DaySchedule.Where(
            allTrue,
            inexactInteger,
            0L,
            "SelectedTrue");
        DaySchedule selectedFalse = DaySchedule.Where(
            allFalse,
            0L,
            inexactInteger,
            "SelectedFalse");
        Assert.All(maximum, value => Assert.Equal(9_007_199_254_740_992D, value));
        Assert.All(minimum, value => Assert.Equal(9_007_199_254_740_992D, value));
        Assert.All(selectedTrue, value => Assert.Equal(9_007_199_254_740_992D, value));
        Assert.All(selectedFalse, value => Assert.Equal(9_007_199_254_740_992D, value));

        DaySchedule unselectedTrue = DaySchedule.Where(
            allFalse,
            inexactInteger,
            0L,
            "UnselectedTrue");
        DaySchedule unselectedFalse = DaySchedule.Where(
            allTrue,
            0L,
            inexactInteger,
            "UnselectedFalse");
        Assert.All(unselectedTrue, value => Assert.Equal(0, value));
        Assert.All(unselectedFalse, value => Assert.Equal(0, value));

        BigInteger overflowingInteger = BigInteger.Pow(10, 400);
        Assert.Throws<OverflowException>(() => low.ElementMaximum(overflowingInteger));
        Assert.Throws<OverflowException>(() =>
            DaySchedule.Where(allTrue, overflowingInteger, 0, "Overflow"));
        DaySchedule unselectedOverflow = DaySchedule.Where(
            allFalse,
            overflowingInteger,
            0,
            "UnselectedOverflow");
        Assert.All(unselectedOverflow, value => Assert.Equal(0, value));
    }

    [Fact]
    public void SelectedHugeIntegersValidateExactNonRealDomainsBeforeFloatCoercion()
    {
        BigInteger positive = BigInteger.Pow(10, 400);
        BigInteger negative = -positive;
        DaySchedule fractionLow = Constant("FractionLow", 0.25, ScheduleType.Fraction);
        DaySchedule fractionHigh = Constant("FractionHigh", 0.75, ScheduleType.Fraction);
        DaySchedule allTrue = Constant("True", 1, ScheduleType.OnOff);
        DaySchedule allFalse = Constant("False", 0, ScheduleType.OnOff);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fractionLow.ElementMaximum(positive));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fractionHigh.ElementMinimum(negative));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DaySchedule.Where(
                allTrue,
                positive,
                0,
                "FractionOverflow",
                ScheduleType.Fraction));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DaySchedule.Where(
                allTrue,
                positive,
                0,
                "TemperatureOverflow",
                ScheduleType.Temperature));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DaySchedule.Where(
                allTrue,
                positive,
                0,
                "OnOffOverflow",
                ScheduleType.OnOff));

        DaySchedule unselected = DaySchedule.Where(
            allFalse,
            positive,
            0,
            "UnselectedFractionOverflow",
            ScheduleType.Fraction);
        Assert.All(unselected, value => Assert.Equal(0, value));
    }

    [Fact]
    public void BigIntegerMaterializationUsesCpythonRoundingAndOverflowThreshold()
    {
        DaySchedule zero = Constant("Zero", 0);
        BigInteger roundingCounterexample = BigInteger.Parse(
            "317683130351994687160902159115792094947415741811",
            CultureInfo.InvariantCulture);
        DaySchedule positive = zero.ElementMaximum(roundingCounterexample);
        DaySchedule negative = zero.ElementMinimum(-roundingCounterexample);
        Assert.All(
            positive,
            value => Assert.Equal(
                unchecked((long)0x49cbd2b3be59b302UL),
                BitConverter.DoubleToInt64Bits(value)));
        Assert.All(
            negative,
            value => Assert.Equal(
                unchecked((long)0xc9cbd2b3be59b302UL),
                BitConverter.DoubleToInt64Bits(value)));

        BigInteger overflowMidpoint =
            (BigInteger.One << 1024) - (BigInteger.One << 970);
        DaySchedule roundedMaximum = zero.ElementMaximum(overflowMidpoint - 1);
        Assert.All(roundedMaximum, value => Assert.Equal(double.MaxValue, value));
        Assert.Throws<OverflowException>(() =>
            zero.ElementMaximum(overflowMidpoint));
        Assert.Throws<OverflowException>(() =>
            zero.ElementMinimum(-overflowMidpoint));
    }

    private static DaySchedule Constant(
        string name,
        double value,
        ScheduleType type = ScheduleType.Real)
    {
        return new DaySchedule(
            name,
            Enumerable.Repeat(value, DaySchedule.FixedLength),
            type);
    }
}
