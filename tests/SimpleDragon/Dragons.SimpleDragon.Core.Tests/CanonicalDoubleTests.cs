using System.Globalization;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon.Tests;

public sealed class CanonicalDoubleTests
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
    public void KnownValuesHavePinnedShortestRoundTripText(double value, string expected)
    {
        Assert.Equal(expected, CanonicalDouble.Format(value));
    }

    [Fact]
    public void SignedZeroAndNonFiniteBoundariesAreExplicit()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));

        Assert.Equal("0", CanonicalDouble.Format(0d));
        Assert.Equal("-0", CanonicalDouble.Format(negativeZero));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalDouble.Format(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalDouble.Format(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalDouble.Format(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(12d, "12.0")]
    [InlineData(0.0001d, "0.0001")]
    [InlineData(0.00001d, "1e-05")]
    [InlineData(1_000_000_000_000_000d, "1000000000000000.0")]
    [InlineData(10_000_000_000_000_000d, "1e+16")]
    [InlineData(5.9604644775390625e-8d, "5.960464477539063e-08")]
    public void PythonLayoutUsesTheSameCanonicalDigitsAndPinnedNotation(
        double value,
        string expected)
    {
        Assert.Equal(expected, CanonicalDouble.FormatPythonFloat(value));
    }

    [Fact]
    public void PythonLayoutPreservesSignedIntegralZeroMarkers()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));

        Assert.Equal("0.0", CanonicalDouble.FormatPythonFloat(0d));
        Assert.Equal("-0.0", CanonicalDouble.FormatPythonFloat(negativeZero));
    }

    [Fact]
    public void ImplicitDomainIdsUseCanonicalDoubleAndSingleComponents()
    {
        double edge = BitConverter.Int64BitsToDouble(0x3e70000000000000L);
        float single = 0.1f;

        Assert.Equal(
            DeterministicDomainId.Create("EDGE", CanonicalDouble.Format(edge)).Value,
            DeterministicDomainId.Create("EDGE", edge).Value);
        Assert.Equal(
            DeterministicDomainId.Create("EDGE", CanonicalDouble.Format((double)single)).Value,
            DeterministicDomainId.Create("EDGE", single).Value);
        Assert.NotEqual(
            DeterministicDomainId.Create("EDGE", edge).Value,
            DeterministicDomainId.Create("EDGE", Math.BitIncrement(edge)).Value);
    }

    [Fact]
    public void SampledFiniteBitPatternsRoundTripUnderAnyCurrentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
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

                string text = CanonicalDouble.Format(value);
                double parsed = double.Parse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                Assert.Equal(bits, BitConverter.DoubleToInt64Bits(parsed));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
