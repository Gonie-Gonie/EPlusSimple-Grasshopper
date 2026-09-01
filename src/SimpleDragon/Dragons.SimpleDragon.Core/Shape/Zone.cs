using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

public sealed class SupplySystemAssignment
{
    public SupplySystemAssignment(string supplySystemId, SupplySystem? supplySystem = null)
    {
        SupplySystemId = DomainSupport.RequiredText(supplySystemId, nameof(supplySystemId));
        SupplySystem = supplySystem;
        if (supplySystem is not null
            && !StringComparer.Ordinal.Equals(SupplySystemId, supplySystem.Id.Value))
        {
            throw new ArgumentException(
                "Supply-system ID does not match the resolved system.",
                nameof(supplySystemId));
        }
    }

    public string SupplySystemId { get; }

    public SupplySystem? SupplySystem { get; }
}

/// <summary>
/// Rhino-free thermal zone containing area-based surfaces and system references.
/// </summary>
public sealed class Zone
{
    public Zone(
        string name,
        int floorNumber,
        double height,
        IEnumerable<Surface> surfaces,
        string profileName,
        UsageProfile? profile,
        double? lightDensity,
        IEnumerable<SupplySystemAssignment>? supplySystems = null,
        IEnumerable<VentilationAssignment>? ventilationSystems = null,
        EntityId? id = null)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        FloorNumber = floorNumber;
        Height = DomainSupport.FinitePositive(height, nameof(height));
        Surface[] surfaceArray = DomainSupport.NotNull(surfaces, nameof(surfaces)).ToArray();
        if (surfaceArray.Any(item => item is null))
        {
            throw new ArgumentException("A zone surface cannot be null.", nameof(surfaces));
        }

        EnsureUnique(surfaceArray.Select(item => item.Id.Value), "surface", nameof(surfaces));
        Surfaces = Array.AsReadOnly(surfaceArray);
        ProfileName = DomainSupport.RequiredText(profileName, nameof(profileName));
        Profile = profile;
        if (profile is not null && !StringComparer.Ordinal.Equals(ProfileName, profile.Name))
        {
            throw new ArgumentException("Profile name does not match the resolved profile.", nameof(profileName));
        }

        if (lightDensity.HasValue)
        {
            LightDensity = DomainSupport.FiniteNonNegative(lightDensity.Value, nameof(lightDensity));
        }

        SupplySystemAssignment[] supplyArray = supplySystems?.ToArray()
            ?? Array.Empty<SupplySystemAssignment>();
        if (supplyArray.Any(item => item is null))
        {
            throw new ArgumentException("A supply-system assignment cannot be null.", nameof(supplySystems));
        }

        EnsureUnique(
            supplyArray.Select(item => item.SupplySystemId),
            "supply-system",
            nameof(supplySystems));
        int radiantCount = supplyArray.Count(item => item.SupplySystem?.Type == SupplySystemType.RadiantFloor
            || item.SupplySystem?.Type == SupplySystemType.ElectricRadiantFloor);
        if (radiantCount > 1)
        {
            throw new ArgumentException("A zone cannot use more than one radiant-floor system.", nameof(supplySystems));
        }

        SupplySystemAssignments = Array.AsReadOnly(supplyArray);
        VentilationAssignment[] ventilationArray = ventilationSystems?.ToArray()
            ?? Array.Empty<VentilationAssignment>();
        if (ventilationArray.Any(item => item is null))
        {
            throw new ArgumentException("A ventilation assignment cannot be null.", nameof(ventilationSystems));
        }

        EnsureUnique(
            ventilationArray.Select(item => item.VentilationSystemId),
            "ventilation-system",
            nameof(ventilationSystems));
        VentilationAssignments = Array.AsReadOnly(ventilationArray);
        Id = id ?? DeterministicDomainId.Create(
            "ZONE",
            Name,
            FloorNumber,
            Height,
            ProfileName,
            LightDensity);
    }

    public EntityId Id { get; }

    public string Name { get; }

    public int FloorNumber { get; }

    public double Height { get; }

    public IReadOnlyList<Surface> Surfaces { get; }

    public string ProfileName { get; }

    public UsageProfile? Profile { get; }

    public double? LightDensity { get; }

    public IReadOnlyList<SupplySystemAssignment> SupplySystemAssignments { get; }

    public IReadOnlyList<VentilationAssignment> VentilationAssignments { get; }

    public IReadOnlyList<SupplySystem> SupplySystems => SupplySystemAssignments
        .Where(item => item.SupplySystem is not null)
        .Select(item => item.SupplySystem!)
        .ToArray();

    public IReadOnlyList<SupplySystem> HeatingSupplySystems => SupplySystems
        .Where(system => system.Heatable)
        .ToArray();

    public IReadOnlyList<SupplySystem> CoolingSupplySystems => SupplySystems
        .Where(system => system.Coolable)
        .ToArray();

    public double Area => Surfaces
        .Where(surface => surface.Type == SurfaceType.Floor)
        .Sum(surface => surface.Area);

    public double Infiltration => Surfaces.Any(surface =>
        surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors
        && surface.Fenestrations.Any(opening =>
            opening.Type == FenestrationType.Window
            || opening.Type == FenestrationType.GlassDoor))
        ? 1.5d
        : 0d;

    private static void EnsureUnique(
        IEnumerable<string> ids,
        string itemDescription,
        string parameterName)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!known.Add(id))
            {
                throw new ArgumentException(
                    "Duplicate " + itemDescription + " ID '" + id + "'.",
                    parameterName);
            }
        }
    }
}

public sealed class BuildingFloor
{
    public BuildingFloor(int floorNumber, IEnumerable<Zone> zones)
    {
        FloorNumber = floorNumber;
        DomainSupport.NotNull(zones, nameof(zones));
        Zone[] zoneArray = zones.ToArray();
        if (zoneArray.Any(item => item is null))
        {
            throw new ArgumentException("A floor zone cannot be null.", nameof(zones));
        }

        if (zoneArray.Any(zone => zone.FloorNumber != floorNumber))
        {
            throw new ArgumentException("Every zone floor number must match its containing floor.", nameof(zones));
        }

        Zones = Array.AsReadOnly(zoneArray);
    }

    public int FloorNumber { get; }

    public IReadOnlyList<Zone> Zones { get; }
}
