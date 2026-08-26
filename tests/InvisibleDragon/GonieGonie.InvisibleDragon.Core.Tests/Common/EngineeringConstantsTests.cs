using System.Globalization;
using System.Reflection;

namespace GonieGonie.InvisibleDragon.Tests.Common;

public sealed class EngineeringConstantsTests
{
    [Fact]
    public void NamedConstantsExposePinnedPublicBinary64Values()
    {
        AssertConstant(
            typeof(UnitConversions),
            nameof(UnitConversions.LitresToCubicMetres),
            0.001d,
            0x3F50_624D_D2F1_A9FCL);
        AssertConstant(
            typeof(UnitConversions),
            nameof(UnitConversions.MillimetresToMetres),
            0.001d,
            0x3F50_624D_D2F1_A9FCL);
        AssertConstant(
            typeof(UnitConversions),
            nameof(UnitConversions.FractionToPercent),
            100d,
            0x4059_0000_0000_0000L);
        AssertConstant(
            typeof(UnitConversions),
            nameof(UnitConversions.PercentToFraction),
            0.01d,
            0x3F84_7AE1_47AE_147BL);
        AssertConstant(
            typeof(UnitConversions),
            nameof(UnitConversions.WattsToKilowatts),
            0.001d,
            0x3F50_624D_D2F1_A9FCL);
        AssertConstant(
            typeof(ThermalDefaults),
            nameof(ThermalDefaults.PeopleActivityLevelWattsPerPerson),
            107d,
            0x405A_C000_0000_0000L);

        Assert.Equal(
            "0.0083",
            (8.3d * UnitConversions.LitresToCubicMetres)
                .ToString("R", CultureInfo.InvariantCulture));
    }

    private static void AssertConstant(
        Type declaringType,
        string name,
        double expectedValue,
        long expectedBits)
    {
        FieldInfo field = Assert.IsAssignableFrom<FieldInfo>(declaringType.GetField(
            name,
            BindingFlags.Public | BindingFlags.Static));
        Assert.True(field.IsLiteral);
        Assert.False(field.IsInitOnly);
        Assert.Equal(typeof(double), field.FieldType);
        double actual = Assert.IsType<double>(field.GetRawConstantValue());
        Assert.Equal(expectedValue, actual);
        Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(actual));
    }
}
