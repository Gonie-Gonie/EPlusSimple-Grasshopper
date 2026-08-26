using System.Globalization;

namespace GonieGonie.BuildingEnergy.Contracts.Tests;

public sealed class InvariantTextDoubleFormattingTests
{
    [Theory]
    [InlineData(0.1d, "0.1")]
    [InlineData(79.34d, "79.34")]
    [InlineData(5506.7000000000007d, "5506.700000000001")]
    [InlineData(9.2300000000000022d, "9.230000000000002")]
    [InlineData(0.84000000000000019d, "0.8400000000000002")]
    [InlineData(986.33000000000015d, "986.3300000000002")]
    [InlineData(5.9604644775390625e-8d, "5.960464477539063e-8")]
    [InlineData(double.Epsilon, "5e-324")]
    [InlineData(double.MaxValue, "1.7976931348623157e+308")]
    [InlineData(double.MinValue, "-1.7976931348623157e+308")]
    public void CanonicalFormattingHasPinnedShortestRoundTripText(
        double value,
        string expected)
    {
        Assert.Equal(expected, InvariantText.FormatCanonicalDouble(value));
    }

    [Fact]
    public void CanonicalFormattingPreservesSignedZeroAndRejectsNonFiniteValues()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(
            unchecked((long)0x8000000000000000UL));

        Assert.Equal("0", InvariantText.FormatCanonicalDouble(0d));
        Assert.Equal("-0", InvariantText.FormatCanonicalDouble(negativeZero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvariantText.FormatCanonicalDouble(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvariantText.FormatCanonicalDouble(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvariantText.FormatCanonicalDouble(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(12d, "12.0")]
    [InlineData(0.0001d, "0.0001")]
    [InlineData(0.00001d, "1e-05")]
    [InlineData(1_000_000_000_000_000d, "1000000000000000.0")]
    [InlineData(10_000_000_000_000_000d, "1e+16")]
    [InlineData(5.9604644775390625e-8d, "5.960464477539063e-08")]
    [InlineData(double.Epsilon, "5e-324")]
    [InlineData(2.2250738585072014e-308d, "2.2250738585072014e-308")]
    [InlineData(double.MaxValue, "1.7976931348623157e+308")]
    public void PythonFormattingUsesPinnedCpythonDigitsAndNotation(
        double value,
        string expected)
    {
        Assert.Equal(expected, InvariantText.FormatPythonFloat(value));
    }

    [Fact]
    public void PythonFormattingPreservesSignedZeroAndCanonicalizesNonFiniteTokens()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(
            unchecked((long)0x8000000000000000UL));
        double negativeNaN = BitConverter.Int64BitsToDouble(
            unchecked((long)0xfff8000000000001UL));

        Assert.Equal("0.0", InvariantText.FormatPythonFloat(0d));
        Assert.Equal("-0.0", InvariantText.FormatPythonFloat(negativeZero));
        Assert.Equal("nan", InvariantText.FormatPythonFloat(double.NaN));
        Assert.Equal("nan", InvariantText.FormatPythonFloat(negativeNaN));
        Assert.Equal("inf", InvariantText.FormatPythonFloat(double.PositiveInfinity));
        Assert.Equal("-inf", InvariantText.FormatPythonFloat(double.NegativeInfinity));
    }

    [Fact]
    public void CanonicalAndPythonFormattingRoundTripSampledFiniteBitPatterns()
    {
        ulong state = 0x6a09e667f3bcc909UL;
        for (int index = 0; index < 4096; index++)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            long bits = unchecked((long)state);
            double value = BitConverter.Int64BitsToDouble(bits);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                continue;
            }

            AssertRoundTrips(bits, InvariantText.FormatCanonicalDouble(value));
            AssertRoundTrips(bits, InvariantText.FormatPythonFloat(value));
        }
    }

    [Fact]
    public void NewFormattersIgnoreCurrentCultureWithoutChangingLegacyFormatDouble()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Equal("1234.5", InvariantText.FormatCanonicalDouble(1234.5d));
            Assert.Equal("1234.5", InvariantText.FormatPythonFloat(1234.5d));
            Assert.Equal(
                1234.5d.ToString("R", CultureInfo.InvariantCulture),
                InvariantText.FormatDouble(1234.5d));
            Assert.Equal(
                double.PositiveInfinity.ToString("R", CultureInfo.InvariantCulture),
                InvariantText.FormatDouble(double.PositiveInfinity));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static void AssertRoundTrips(long expectedBits, string text)
    {
        double parsed = double.Parse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(parsed));
    }
}
