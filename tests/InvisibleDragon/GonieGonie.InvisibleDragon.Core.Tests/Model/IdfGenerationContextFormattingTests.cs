using System.Globalization;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class IdfGenerationContextFormattingTests
{
    public static TheoryData<double, string> PythonDoubleCases => new()
    {
        { 0d, "0.0" },
        { BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL)), "-0.0" },
        { 12d, "12.0" },
        { 0.0001d, "0.0001" },
        { 0.00001d, "1e-05" },
        { 1_000_000_000_000_000d, "1000000000000000.0" },
        { 10_000_000_000_000_000d, "1e+16" },
        { 5.9604644775390625e-8d, "5.960464477539063e-08" },
        { 5506.7000000000007d, "5506.700000000001" },
        { 9.2300000000000022d, "9.230000000000002" },
        { 0.84000000000000019d, "0.8400000000000002" },
        { 986.33000000000015d, "986.3300000000002" },
        { double.Epsilon, "5e-324" },
        { 2.2250738585072014e-308d, "2.2250738585072014e-308" },
        { double.MaxValue, "1.7976931348623157e+308" },
        { double.MinValue, "-1.7976931348623157e+308" },
    };

    [Theory]
    [MemberData(nameof(PythonDoubleCases))]
    public void FormatUsesPinnedCpythonBinary64Text(double value, string expected)
    {
        Assert.Equal(expected, IdfGenerationContext.Format(value));
    }

    [Fact]
    public void FormatCanonicalizesPythonNonFiniteTokens()
    {
        double negativeNaN = BitConverter.Int64BitsToDouble(
            unchecked((long)0xfff8000000000001UL));

        Assert.Equal("nan", IdfGenerationContext.Format(double.NaN));
        Assert.Equal("nan", IdfGenerationContext.Format(negativeNaN));
        Assert.Equal("inf", IdfGenerationContext.Format(double.PositiveInfinity));
        Assert.Equal("-inf", IdfGenerationContext.Format(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(0f, "0.0")]
    [InlineData(-0f, "-0.0")]
    [InlineData(12f, "12.0")]
    [InlineData(0.1f, "0.10000000149011612")]
    public void FormatWidensBinary32BeforeUsingPythonBinary64Text(float value, string expected)
    {
        Assert.Equal(expected, IdfGenerationContext.Format(value));
    }

    [Fact]
    public void FormatPreservesExistingNonFloatingDispatch()
    {
        Assert.Equal(string.Empty, IdfGenerationContext.Format(null));
        Assert.Equal("literal", IdfGenerationContext.Format("literal"));
        Assert.Equal("Yes", IdfGenerationContext.Format(true));
        Assert.Equal("No", IdfGenerationContext.Format(false));
        Assert.Equal("Heating", IdfGenerationContext.Format(TestMode.Heating));
        Assert.Equal("12", IdfGenerationContext.Format(12));
        Assert.Equal("1.2300", IdfGenerationContext.Format(1.2300m));
        Assert.Equal("format=<null>;culture=", IdfGenerationContext.Format(new ProbeFormattable()));
    }

    [Fact]
    public void FormatAndBothCreationRoutesIgnoreCurrentCultureAndAgree()
    {
        object?[] values =
        {
            12d,
            0.1f,
            1.2300m,
            true,
            TestMode.Heating,
            "literal",
            null,
        };
        string[] expected =
        {
            "12.0",
            "0.10000000149011612",
            "1.2300",
            "Yes",
            "Heating",
            "literal",
            string.Empty,
        };

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo french = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = french;
            CultureInfo.CurrentUICulture = french;

            var context = new IdfGenerationContext();
            var fields = values
                .Select((value, index) => IdfGenerationContext.Field(index, "Field " + index, value))
                .ToArray();
            string[] named = context.Create("Test:Object", fields).Fields
                .Select(field => field.Value)
                .ToArray();
            string[] raw = context.CreateRaw("Test:Object", values).Fields
                .Select(field => field.Value)
                .ToArray();

            Assert.Equal(expected, named);
            Assert.Equal(expected, raw);
            Assert.Equal(named, raw);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private enum TestMode
    {
        Heating,
    }

    private sealed class ProbeFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            CultureInfo? culture = formatProvider as CultureInfo;
            return "format=" + (format ?? "<null>") + ";culture=" + (culture?.Name ?? "<none>");
        }
    }
}
