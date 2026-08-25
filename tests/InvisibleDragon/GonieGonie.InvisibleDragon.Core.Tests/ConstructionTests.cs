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
    public void MaterialEqualityMatchesPinnedInvisibleDragonProperties()
    {
        var first = new Material(
            "Brick",
            0.72,
            1920,
            840,
            thermalAbsorptance: 0.1,
            solarAbsorptance: 0.2,
            visibleAbsorptance: 0.3,
            roughness: MaterialRoughness.VeryRough);
        var samePinnedProperties = new Material(
            "Brick",
            0.72,
            1920,
            840,
            thermalAbsorptance: 0.9,
            solarAbsorptance: 0.8,
            visibleAbsorptance: 0.7,
            roughness: MaterialRoughness.Smooth);
        var renamed = new Material("Other", 0.72, 1920, 840);
        var changedConductivity = new Material("Brick", 0.73, 1920, 840);
        var changedDensity = new Material("Brick", 0.72, 1919, 840);
        var changedSpecificHeat = new Material("Brick", 0.72, 1920, 841);

        Assert.Equal(first, samePinnedProperties);
        Assert.True(first == samePinnedProperties);
        Assert.Equal(first.GetHashCode(), samePinnedProperties.GetHashCode());
        Assert.NotEqual(first, renamed);
        Assert.NotEqual(first, changedConductivity);
        Assert.NotEqual(first, changedDensity);
        Assert.NotEqual(first, changedSpecificHeat);
        Assert.False(first.Equals(null));
        Assert.False(first.Equals(new object()));
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
    public void LayerEqualityAndHashMatchPinnedInvisibleDragonBehavior()
    {
        var first = new Layer(
            "Exterior concrete",
            new Material("Concrete", 1.4, 2300, 880, thermalAbsorptance: 0.1),
            0.2);
        var renamedWithIgnoredMaterialProperties = new Layer(
            "Interior concrete",
            new Material("Concrete", 1.4, 2300, 880, thermalAbsorptance: 0.9),
            0.2);
        var changedThickness = new Layer("Exterior concrete", first.Material, 0.21);
        var changedMaterial = new Layer(
            "Exterior concrete",
            new Material("Concrete", 1.5, 2300, 880),
            0.2);

        Assert.Equal(first, renamedWithIgnoredMaterialProperties);
        Assert.True(first == renamedWithIgnoredMaterialProperties);
        Assert.NotEqual(first, changedThickness);
        Assert.NotEqual(first, changedMaterial);
        Assert.Equal(StringComparer.Ordinal.GetHashCode(first.Name), first.GetHashCode());
        Assert.Equal(first.GetHashCode(), changedThickness.GetHashCode());
        Assert.Equal(
            StringComparer.Ordinal.GetHashCode(renamedWithIgnoredMaterialProperties.Name),
            renamedWithIgnoredMaterialProperties.GetHashCode());
        Assert.False(first.Equals(null));
        Assert.False(first.Equals(new object()));
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
    public void ConstructionEqualityAndHashMatchPinnedInvisibleDragonBehavior()
    {
        var concrete = new Material("Concrete", 1.4, 2300, 880);
        var insulation = new Material("Insulation", 0.04, 30, 1400);
        var first = new OpaqueConstruction(
            "Wall",
            new[]
            {
                new Layer("Concrete outside", concrete, 0.2),
                new Layer("Insulation inside", insulation, 0.1),
            });
        var samePinnedProperties = new OpaqueConstruction(
            "Wall",
            new[]
            {
                new Layer("Renamed concrete", concrete, 0.2),
                new Layer("Renamed insulation", insulation, 0.1),
            });
        var renamed = new OpaqueConstruction("Other", samePinnedProperties.Layers);
        var reversed = new OpaqueConstruction("Wall", samePinnedProperties.Layers.Reverse());
        var fewerLayers = new OpaqueConstruction("Wall", samePinnedProperties.Layers.Take(1));

        Assert.Equal(first, samePinnedProperties);
        Assert.NotEqual(first, renamed);
        Assert.NotEqual(first, reversed);
        Assert.NotEqual(first, fewerLayers);
        Assert.Equal(StringComparer.Ordinal.GetHashCode(first.Name), first.GetHashCode());
        Assert.Equal(first.GetHashCode(), reversed.GetHashCode());
        Assert.Equal(StringComparer.Ordinal.GetHashCode(renamed.Name), renamed.GetHashCode());
        Assert.False(first.Equals(null));
        Assert.False(first.Equals(new object()));
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
