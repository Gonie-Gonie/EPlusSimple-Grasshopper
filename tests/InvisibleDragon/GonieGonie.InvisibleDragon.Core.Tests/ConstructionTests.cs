using GonieGonie.InvisibleDragon.Construction;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;

namespace GonieGonie.InvisibleDragon.Tests;

public sealed class ConstructionTests
{
    [Fact]
    public void MaterialPreservesValidatedThermophysicalProperties()
    {
        var material = new Material(
            "Brick",
            0.72,
            1920,
            840,
            0.88,
            0.65,
            0.61,
            MaterialRoughness.MediumRough);

        Assert.Equal("Brick", material.Name);
        Assert.Equal(0.72, material.ConductivityWattsPerMetreKelvin);
        Assert.Equal(1920, material.DensityKilogramsPerCubicMetre);
        Assert.Equal(840, material.SpecificHeatJoulesPerKilogramKelvin);
        Assert.Equal(MaterialRoughness.MediumRough, material.Roughness);
    }

    [Theory]
    [InlineData(0, 1000, 1000)]
    [InlineData(1, 0, 1000)]
    [InlineData(1, 1000, 99)]
    [InlineData(double.NaN, 1000, 1000)]
    public void MaterialRejectsInvalidPhysicalValues(double conductivity, double density, double specificHeat)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Material("Bad", conductivity, density, specificHeat));
    }

    [Fact]
    public void LayerCalculatesConductanceResistanceAndArealHeatCapacity()
    {
        var layer = new Layer("Concrete 200 mm", TestDomainFactory.Concrete(), 0.2);

        Assert.Equal(7, layer.UValue, 12);
        Assert.Equal(1d / 7, layer.ThermalResistance, 12);
        Assert.Equal(387200, layer.HeatCapacityJoulesPerSquareMetreKelvin, 8);
    }

    [Fact]
    public void ConstructionAggregatesLayersAndDefensivelyCopiesInput()
    {
        var concrete = new Layer("Concrete", TestDomainFactory.Concrete(), 0.2);
        var insulation = new Layer(
            "Insulation",
            new Material("Insulation", 0.04, 30, 1400),
            0.1);
        var source = new List<Layer> { concrete, insulation };

        var construction = new OpaqueConstruction("Exterior Wall", source);
        source.Clear();

        Assert.Equal(2, construction.Layers.Count);
        Assert.Equal(0.3, construction.ThicknessMetres, 12);
        Assert.Equal(1d / ((0.2 / 1.4) + (0.1 / 0.04)), construction.UValue, 12);
        Assert.Equal(
            concrete.HeatCapacityJoulesPerSquareMetreKelvin
                + insulation.HeatCapacityJoulesPerSquareMetreKelvin,
            construction.HeatCapacityJoulesPerSquareMetreKelvin,
            8);
    }

    [Fact]
    public void ReversedConstructionPreservesPropertiesAndReversesLayerOrder()
    {
        var first = new Layer("First", TestDomainFactory.Concrete(), 0.1);
        var second = new Layer("Second", TestDomainFactory.Concrete("Other"), 0.2);
        var construction = new OpaqueConstruction("Wall", new[] { first, second });

        OpaqueConstruction reversed = construction.Reverse();

        Assert.Equal("Wall_reversed", reversed.Name);
        Assert.Equal(new[] { second, first }, reversed.Layers);
        Assert.Equal(construction.UValue, reversed.UValue, 12);
        Assert.Equal(construction.HeatCapacityJoulesPerSquareMetreKelvin, reversed.HeatCapacityJoulesPerSquareMetreKelvin, 8);
    }

    [Fact]
    public void ConstructionRequiresAtLeastOneLayer()
    {
        Assert.Throws<ArgumentException>(() => new OpaqueConstruction("Empty", Array.Empty<Layer>()));
    }

    [Fact]
    public void SimpleConstructionsExposeDerivedValues()
    {
        var noMass = new NoMassConstruction("Partition", 2.5);
        var glazing = new Glazing("Double glazing", 1.2, 0.42);
        var air = new AirBoundary("Air wall", 0.5);

        Assert.Equal(0.4, noMass.ThermalResistance, 12);
        Assert.Equal(0.42, glazing.SolarHeatGainCoefficient);
        Assert.Equal(0.5, air.AirChangesPerHour);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void GlazingRejectsInvalidSolarHeatGainCoefficient(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Glazing("Bad", 1, value));
    }
}
