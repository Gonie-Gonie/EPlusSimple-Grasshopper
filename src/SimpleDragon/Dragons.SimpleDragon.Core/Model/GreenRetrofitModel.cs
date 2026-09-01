using System.Collections.ObjectModel;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

/// <summary>
/// Rhino-free aggregate represented by the GRM 0.7 building-energy input format.
/// </summary>
public sealed class GreenRetrofitModel
{
    public GreenRetrofitModel(
        string name,
        double northAxis,
        string address,
        DateTime vintage,
        bool isMultifamilyHousing,
        IEnumerable<BuildingFloor> floors,
        IEnumerable<Material> materials,
        IEnumerable<SurfaceConstruction> surfaceConstructions,
        IEnumerable<FenestrationConstruction> fenestrationConstructions,
        IEnumerable<SourceSystem>? sourceSystems = null,
        IEnumerable<SupplySystem>? supplySystems = null,
        IEnumerable<VentilationSystem>? ventilationSystems = null,
        IEnumerable<PhotovoltaicSystem>? photovoltaicSystems = null,
        WeatherSelection? weather = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        if (double.IsNaN(northAxis) || double.IsInfinity(northAxis) || northAxis < 0d || northAxis >= 360d)
        {
            throw new ArgumentOutOfRangeException(nameof(northAxis), northAxis, "North axis must be in [0, 360). ");
        }

        NorthAxis = northAxis;
        Address = DomainSupport.RequiredText(address, nameof(address));
        Vintage = vintage.Date;
        IsMultifamilyHousing = isMultifamilyHousing;
        Floors = Copy(floors, nameof(floors));
        Materials = Copy(materials, nameof(materials));
        SurfaceConstructions = Copy(surfaceConstructions, nameof(surfaceConstructions));
        FenestrationConstructions = Copy(fenestrationConstructions, nameof(fenestrationConstructions));
        SourceSystems = Copy(sourceSystems ?? Array.Empty<SourceSystem>(), nameof(sourceSystems));
        SupplySystems = Copy(supplySystems ?? Array.Empty<SupplySystem>(), nameof(supplySystems));
        VentilationSystems = Copy(
            ventilationSystems ?? Array.Empty<VentilationSystem>(),
            nameof(ventilationSystems));
        PhotovoltaicSystems = Copy(
            photovoltaicSystems ?? Array.Empty<PhotovoltaicSystem>(),
            nameof(photovoltaicSystems));
        EnsureUnique(Floors.SelectMany(floor => floor.Zones).Select(zone => zone.Id.Value), "zone");
        EnsureUnique(Materials.Select(item => item.Id.Value), "material");
        EnsureUnique(SurfaceConstructions.Select(item => item.Id.Value), "surface construction");
        EnsureUnique(FenestrationConstructions.Select(item => item.Id.Value), "fenestration construction");
        EnsureUnique(SourceSystems.Select(item => item.Id.Value), "source system");
        EnsureUnique(SupplySystems.Select(item => item.Id.Value), "supply system");
        EnsureUnique(VentilationSystems.Select(item => item.Id.Value), "ventilation system");
        EnsureUnique(PhotovoltaicSystems.Select(item => item.Id.Value), "photovoltaic system");
        Weather = weather;
        Id = DeterministicDomainId.Create(
            "GRM",
            Name,
            Address,
            Vintage,
            NorthAxis,
            IsMultifamilyHousing);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public double NorthAxis { get; }

    public string Address { get; }

    public DateTime Vintage { get; }

    public bool IsMultifamilyHousing { get; }

    public IReadOnlyList<BuildingFloor> Floors { get; }

    public IReadOnlyList<Zone> Zones => Floors.SelectMany(floor => floor.Zones).ToArray();

    public IReadOnlyList<Material> Materials { get; }

    public IReadOnlyList<SurfaceConstruction> SurfaceConstructions { get; }

    public IReadOnlyList<FenestrationConstruction> FenestrationConstructions { get; }

    public IReadOnlyList<SourceSystem> SourceSystems { get; }

    public IReadOnlyList<SupplySystem> SupplySystems { get; }

    public IReadOnlyList<VentilationSystem> VentilationSystems { get; }

    public IReadOnlyList<PhotovoltaicSystem> PhotovoltaicSystems { get; }

    public WeatherSelection? Weather { get; }

    public double Area => Zones.Sum(zone => zone.Area);

    public IReadOnlyList<Surface> ExteriorWalls => Zones
        .SelectMany(zone => zone.Surfaces)
        .Where(surface => surface.Type == SurfaceType.Wall
            && surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors)
        .ToArray();

    public IReadOnlyList<Surface> ExteriorRoofs => Zones
        .SelectMany(zone => zone.Surfaces)
        .Where(surface => surface.Type == SurfaceType.Ceiling
            && surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors)
        .ToArray();

    public IReadOnlyList<Surface> ExteriorFloors => Zones
        .SelectMany(zone => zone.Surfaces)
        .Where(surface => surface.Type == SurfaceType.Floor
            && (surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors
                || surface.BoundaryCondition == SurfaceBoundaryCondition.Ground))
        .ToArray();

    public IReadOnlyList<Fenestration> ExteriorWindows => ExteriorWalls
        .SelectMany(surface => surface.Fenestrations)
        .Where(opening => opening.Type == FenestrationType.Window
            || opening.Type == FenestrationType.GlassDoor)
        .ToArray();

    public double AverageExteriorWallUValue => WeightedSurfaceUValue(ExteriorWalls);

    public double AverageExteriorRoofUValue => WeightedSurfaceUValue(ExteriorRoofs);

    public double AverageExteriorFloorUValue => WeightedSurfaceUValue(ExteriorFloors);

    public double AverageWindowUValue => WeightedAverage(
        ExteriorWindows.Where(item => item.Construction is not null),
        item => item.Area,
        item => item.Construction!.UValue);

    public double AverageLightDensity => WeightedAverage(
        Zones.Where(zone => zone.LightDensity.HasValue),
        zone => zone.Area,
        zone => zone.LightDensity!.Value);

    public double AverageInfiltration => WeightedAverage(
        Zones,
        zone => zone.Area * zone.Height,
        zone => zone.Infiltration);

    private static double WeightedSurfaceUValue(IEnumerable<Surface> surfaces)
    {
        return WeightedAverage(
            surfaces.Where(surface => surface.Construction is not null),
            surface => surface.Area,
            surface => surface.Construction!.GetUValue());
    }

    private static double WeightedAverage<T>(
        IEnumerable<T> items,
        Func<T, double> weight,
        Func<T, double> value)
    {
        double weightSum = 0d;
        double weightedValue = 0d;
        foreach (T item in items)
        {
            double itemWeight = weight(item);
            weightSum += itemWeight;
            weightedValue += itemWeight * value(item);
        }

        return weightSum > 0d ? weightedValue / weightSum : 0d;
    }

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> items, string parameterName)
        where T : class
    {
        DomainSupport.NotNull(items, parameterName);
        T[] copy = items.ToArray();
        if (copy.Any(item => item is null))
        {
            throw new ArgumentException("An item cannot be null.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    private static void EnsureUnique(IEnumerable<string> ids, string description)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!known.Add(id))
            {
                throw new ArgumentException("Duplicate " + description + " ID '" + id + "'.");
            }
        }
    }
}
