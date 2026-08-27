using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class DomesticHotWaterTests
{
    [Fact]
    public void ConstructorPreservesImmutableIdentityFuelAndEfficiency()
    {
        var system = new DomesticHotWater(
            new EntityId("dhw-primary"),
            "  Primary Hot Water  ",
            Fuel.NaturalGas,
            0.825);

        Assert.Equal(new EntityId("dhw-primary"), system.Id);
        Assert.Equal("Primary Hot Water", system.Name);
        Assert.Equal(Fuel.NaturalGas, system.Fuel);
        Assert.Equal(0.825, system.Efficiency);
        Assert.True(typeof(DomesticHotWater).IsSealed);
        Assert.Contains("Primary Hot Water", system.ToString(), StringComparison.Ordinal);
        Assert.Contains("NaturalGas", system.ToString(), StringComparison.Ordinal);
        Assert.Contains("82.5%", system.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.001)]
    [InlineData(1.000001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void EfficiencyMustBeFiniteAndInTheHalfOpenUnitInterval(double efficiency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DomesticHotWater(
            new EntityId("dhw-invalid-efficiency"),
            "Invalid Efficiency",
            Fuel.Electricity,
            efficiency));
    }

    [Fact]
    public void ConstructorRejectsUndefinedFuel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DomesticHotWater(
            new EntityId("dhw-invalid-fuel"),
            "Invalid Fuel",
            (Fuel)int.MaxValue,
            0.9));
    }

    [Fact]
    public void ToIdfObjectsReturnsAFreshEmptyListForEveryCall()
    {
        var system = new DomesticHotWater(
            new EntityId("dhw-empty-emission"),
            "Empty Emission",
            Fuel.Propane,
            1);
        var context = new IdfGenerationContext();

        IReadOnlyList<IdfObject> first = system.ToIdfObjects(context);
        IReadOnlyList<IdfObject> second = system.ToIdfObjects(context);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.NotSame(first, second);
        Assert.Throws<ArgumentNullException>(() => system.ToIdfObjects(null!));
    }
}
