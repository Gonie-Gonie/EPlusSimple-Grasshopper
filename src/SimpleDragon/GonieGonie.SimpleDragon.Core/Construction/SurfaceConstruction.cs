using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

public enum SurfaceType
{
    Wall,
    Ceiling,
    Floor,
}

public enum SurfaceBoundaryCondition
{
    Outdoors,
    Ground,
    AdjacentSpace,
    Adiabatic,
    Zone,
}

/// <summary>
/// A single material layer, with thickness in metres.
/// </summary>
public sealed class SurfaceConstructionLayer
{
    public SurfaceConstructionLayer(Material material, double thickness)
    {
        Material = DomainSupport.NotNull(material, nameof(material));
        Thickness = DomainSupport.FinitePositive(thickness, nameof(thickness));
    }

    public Material Material { get; }

    public double Thickness { get; }
}

/// <summary>
/// An ordered opaque construction whose first layer follows the upstream database order.
/// </summary>
public sealed class SurfaceConstruction
{
    public SurfaceConstruction(
        string name,
        IEnumerable<SurfaceConstructionLayer> layers,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        DomainSupport.NotNull(layers, nameof(layers));

        SurfaceConstructionLayer[] materialLayers = layers.ToArray();
        if (materialLayers.Length == 0)
        {
            throw new ArgumentException("At least one material layer is required.", nameof(layers));
        }

        if (materialLayers.Any(layer => layer is null))
        {
            throw new ArgumentException("A material layer cannot be null.", nameof(layers));
        }

        Layers = Array.AsReadOnly(materialLayers);
        Id = id ?? DeterministicDomainId.Create(
            "CTSF",
            new object?[] { Name }
                .Concat(materialLayers.SelectMany(layer => new object?[] { layer.Material.Id.Value, layer.Thickness }))
                .ToArray());
    }

    public EntityId Id { get; }

    public string Name { get; }

    public IReadOnlyList<SurfaceConstructionLayer> Layers { get; }

    public double InternalUValue => 1d / Layers.Sum(layer => layer.Thickness / layer.Material.Conductivity);

    public double Depth => Layers.Sum(layer => layer.Thickness);

    public double HeatCapacity => Layers.Sum(
        layer => layer.Material.Density * layer.Material.SpecificHeat * layer.Thickness);

    public double GetUValue(
        double interiorConvection = ConvectionHeatTransfer.Interior,
        double exteriorConvection = ConvectionHeatTransfer.Exterior)
    {
        DomainSupport.FinitePositive(interiorConvection, nameof(interiorConvection));
        DomainSupport.FinitePositive(exteriorConvection, nameof(exteriorConvection));
        return 1d / ((1d / interiorConvection) + (1d / exteriorConvection) + (1d / InternalUValue));
    }

    public SurfaceConstruction Reverse()
    {
        return new SurfaceConstruction(
            Name + "_reversed",
            Layers.Reverse(),
            DeterministicDomainId.Create("CTSF-REVERSED", Id.Value));
    }

    public static SurfaceConstruction CreateSimple(
        string name,
        double uValue,
        Material insulation,
        Material concrete,
        double interiorConvection = ConvectionHeatTransfer.Interior,
        double exteriorConvection = ConvectionHeatTransfer.Exterior,
        double concreteThickness = 0.19d,
        EntityId? id = null)
    {
        DomainSupport.RequiredText(name, nameof(name));
        DomainSupport.FinitePositive(uValue, nameof(uValue));
        DomainSupport.NotNull(insulation, nameof(insulation));
        DomainSupport.NotNull(concrete, nameof(concrete));

        DomainSupport.FinitePositive(interiorConvection, nameof(interiorConvection));
        DomainSupport.FinitePositive(exteriorConvection, nameof(exteriorConvection));
        DomainSupport.FinitePositive(concreteThickness, nameof(concreteThickness));

        double maximumUValue = 1d / ((1d / interiorConvection) + (1d / exteriorConvection));
        if (uValue >= maximumUValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(uValue),
                uValue,
                "U-value must be below the zero-thickness surface limit.");
        }

        double insulationThickness = insulation.Conductivity
            * ((1d / uValue)
               - (1d / interiorConvection)
               - (1d / exteriorConvection)
               - (concreteThickness / concrete.Conductivity));

        if (insulationThickness < 0d)
        {
            insulationThickness = 0d;
            concreteThickness = concrete.Conductivity
                * ((1d / uValue) - (1d / interiorConvection) - (1d / exteriorConvection));
        }

        var layers = new List<SurfaceConstructionLayer>(2);
        if (insulationThickness > 0d)
        {
            layers.Add(new SurfaceConstructionLayer(insulation, insulationThickness));
        }

        layers.Add(new SurfaceConstructionLayer(concrete, concreteThickness));
        return new SurfaceConstruction(name, layers, id);
    }
}

/// <summary>
/// Exact five-column key used by the Korean opaque-construction regulation table.
/// </summary>
public sealed class SurfaceRegulationKey : IEquatable<SurfaceRegulationKey>
{
    public SurfaceRegulationKey(
        DateTime effectiveDate,
        string part,
        string outsideAirCondition,
        string use,
        string climateRegion)
    {
        EffectiveDate = effectiveDate.Date;
        Part = DomainSupport.RequiredText(part, nameof(part));
        OutsideAirCondition = DomainSupport.RequiredText(outsideAirCondition, nameof(outsideAirCondition));
        Use = DomainSupport.RequiredText(use, nameof(use));
        ClimateRegion = DomainSupport.RequiredText(climateRegion, nameof(climateRegion));
    }

    public DateTime EffectiveDate { get; }

    public string Part { get; }

    public string OutsideAirCondition { get; }

    public string Use { get; }

    public string ClimateRegion { get; }

    public bool Equals(SurfaceRegulationKey? other)
    {
        return other is not null
            && EffectiveDate == other.EffectiveDate
            && StringComparer.Ordinal.Equals(Part, other.Part)
            && StringComparer.Ordinal.Equals(OutsideAirCondition, other.OutsideAirCondition)
            && StringComparer.Ordinal.Equals(Use, other.Use)
            && StringComparer.Ordinal.Equals(ClimateRegion, other.ClimateRegion);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SurfaceRegulationKey);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = EffectiveDate.GetHashCode();
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Part);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(OutsideAirCondition);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Use);
            return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ClimateRegion);
        }
    }

    public override string ToString()
    {
        return EffectiveDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)
            + "&" + Part
            + "&" + OutsideAirCondition
            + "&" + Use
            + "&" + ClimateRegion;
    }
}
