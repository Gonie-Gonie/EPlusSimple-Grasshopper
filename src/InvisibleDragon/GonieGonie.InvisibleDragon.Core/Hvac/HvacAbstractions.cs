using System.Collections.ObjectModel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Internal;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;

namespace GonieGonie.InvisibleDragon.Hvac;

public enum Fuel
{
    Electricity,
    NaturalGas,
    Propane,
    FuelOilNo1,
    FuelOilNo2,
    Coal,
    Diesel,
    Gasoline,
    OtherFuel1,
    OtherFuel2,
}

public abstract class HvacSystem
{
    protected HvacSystem(EntityId id, string name)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = DomainGuard.RequiredText(name, nameof(name));
    }

    public EntityId Id { get; }

    public string Name { get; }
}

public abstract class SourceSystem : HvacSystem
{
    protected SourceSystem(EntityId id, string name)
        : base(id, name)
    {
    }

    public abstract string IdfObjectType { get; }

    public abstract string IdfObjectName { get; }

    public virtual string LoopName => $"Loop_for_{Name}";

    public abstract IReadOnlyList<IdfObject> ToIdfObjects(
        IdfGenerationContext context,
        IReadOnlyList<PlantDemandConnection>? demandConnections = null,
        IReadOnlyList<string>? terminalUnitNames = null);
}

public abstract class SupplySystem : HvacSystem
{
    protected SupplySystem(EntityId id, string name, SourceSystem? source)
        : base(id, name)
    {
        Source = source;
    }

    public SourceSystem? Source { get; }

    public abstract bool CanHeat { get; }

    public abstract bool CanCool { get; }

    internal abstract SupplyIdfFragment Generate(
        IdfGenerationContext context,
        Zone zone,
        string availabilityScheduleName);

    public string ObjectNameFor(Zone zone)
    {
        return $"{GetType().Name}_named_{Name}_for_{zone.Name}";
    }
}

public sealed record ZoneEquipmentDescriptor
{
    public ZoneEquipmentDescriptor(
        string objectType,
        string name,
        int coolingSequence,
        int heatingSequence,
        string? inletNodeName = null,
        string? exhaustNodeName = null)
    {
        ObjectType = DomainGuard.RequiredText(objectType, nameof(objectType));
        Name = DomainGuard.RequiredText(name, nameof(name));
        if (coolingSequence < 0 || heatingSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coolingSequence));
        }

        CoolingSequence = coolingSequence;
        HeatingSequence = heatingSequence;
        InletNodeName = string.IsNullOrWhiteSpace(inletNodeName) ? null : inletNodeName!.Trim();
        ExhaustNodeName = string.IsNullOrWhiteSpace(exhaustNodeName) ? null : exhaustNodeName!.Trim();
    }

    public string ObjectType { get; }

    public string Name { get; }

    public int CoolingSequence { get; }

    public int HeatingSequence { get; }

    public string? InletNodeName { get; }

    public string? ExhaustNodeName { get; }
}

public sealed record PlantDemandConnection
{
    public PlantDemandConnection(
        string branchName,
        string componentObjectType,
        string componentName,
        string inletNodeName,
        string outletNodeName)
    {
        BranchName = DomainGuard.RequiredText(branchName, nameof(branchName));
        ComponentObjectType = DomainGuard.RequiredText(componentObjectType, nameof(componentObjectType));
        ComponentName = DomainGuard.RequiredText(componentName, nameof(componentName));
        InletNodeName = DomainGuard.RequiredText(inletNodeName, nameof(inletNodeName));
        OutletNodeName = DomainGuard.RequiredText(outletNodeName, nameof(outletNodeName));
    }

    public string BranchName { get; }

    public string ComponentObjectType { get; }

    public string ComponentName { get; }

    public string InletNodeName { get; }

    public string OutletNodeName { get; }
}

internal sealed class SupplyIdfFragment
{
    public SupplyIdfFragment(
        IEnumerable<IdfObject> objects,
        ZoneEquipmentDescriptor equipment,
        PlantDemandConnection? plantConnection = null,
        string? terminalUnitName = null)
    {
        Objects = new ReadOnlyCollection<IdfObject>(objects.ToArray());
        Equipment = equipment;
        PlantConnection = plantConnection;
        TerminalUnitName = terminalUnitName;
    }

    public IReadOnlyList<IdfObject> Objects { get; }

    public ZoneEquipmentDescriptor Equipment { get; }

    public PlantDemandConnection? PlantConnection { get; }

    public string? TerminalUnitName { get; }
}

public sealed record ZoneHvacAssignment
{
    public ZoneHvacAssignment(EntityId zoneId, SupplyGroup supply)
    {
        ZoneId = zoneId ?? throw new ArgumentNullException(nameof(zoneId));
        Supply = supply ?? throw new ArgumentNullException(nameof(supply));
    }

    public EntityId ZoneId { get; }

    public SupplyGroup Supply { get; }
}

public sealed class SupplyGroup
{
    public SupplyGroup(
        IEnumerable<SupplySystem> systems,
        IEnumerable<Schedule?>? availabilities = null)
    {
        SupplySystem[] systemCopy = DomainGuard.CopyRequired(systems, nameof(systems));
        if (systemCopy.Length == 0)
        {
            throw new ArgumentException("A supply group requires at least one system.", nameof(systems));
        }

        if (systemCopy.Any(system => !system.CanHeat && !system.CanCool))
        {
            throw new ArgumentException("Every supply system must support heating or cooling.", nameof(systems));
        }

        if (systemCopy.Select(system => system.Id).Distinct().Count() != systemCopy.Length)
        {
            throw new ArgumentException("Supply-system identifiers must be unique within a group.", nameof(systems));
        }

        Schedule?[] availabilityCopy = availabilities?.ToArray() ?? new Schedule?[systemCopy.Length];
        if (availabilityCopy.Length != systemCopy.Length)
        {
            throw new ArgumentException("Availability count must match supply-system count.", nameof(availabilities));
        }

        if (availabilityCopy.Any(schedule => schedule is not null && schedule.Type != ScheduleType.OnOff))
        {
            throw new ArgumentException("Supply availability schedules must have OnOff type.", nameof(availabilities));
        }

        Systems = new ReadOnlyCollection<SupplySystem>(systemCopy);
        Availabilities = new ReadOnlyCollection<Schedule?>(availabilityCopy);
    }

    public IReadOnlyList<SupplySystem> Systems { get; }

    public IReadOnlyList<Schedule?> Availabilities { get; }

    public IReadOnlyList<SupplySystem> HeatingSystems =>
        new ReadOnlyCollection<SupplySystem>(Systems.Where(system => system.CanHeat).ToArray());

    public IReadOnlyList<SupplySystem> CoolingSystems =>
        new ReadOnlyCollection<SupplySystem>(Systems.Where(system => system.CanCool).ToArray());

    public bool CanHeat => HeatingSystems.Count > 0;

    public bool CanCool => CoolingSystems.Count > 0;

    public IReadOnlyList<SourceSystem> Sources => new ReadOnlyCollection<SourceSystem>(
        Systems
            .Select(system => system.Source)
            .Where(source => source is not null)
            .Cast<SourceSystem>()
            .GroupBy(source => source.Id)
            .Select(group => group.First())
            .ToArray());

    internal IReadOnlyList<Schedule> CustomAvailabilitySchedules => new ReadOnlyCollection<Schedule>(
        Availabilities.Where(schedule => schedule is not null).Cast<Schedule>().ToArray());
}
