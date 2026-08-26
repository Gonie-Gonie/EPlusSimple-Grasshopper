using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelConditioningTests
{
    [Fact]
    public void PartitionsZonesByAssignmentAndProfileAvailabilityInInputOrder()
    {
        Zone assignedWithoutAvailability = CreateZone(
            "ZONE-ASSIGNED-NO-AVAILABILITY",
            "Assigned without availability");
        Zone availabilityWithoutAssignment = CreateZone(
            "ZONE-AVAILABLE-NO-ASSIGNMENT",
            "Available without assignment",
            Schedule.Constant("Unused profile availability", 1, ScheduleType.OnOff));
        Zone assignedWithZeroAvailability = CreateZone(
            "ZONE-ASSIGNED-ZERO",
            "Assigned with zero availability",
            Schedule.Constant("Zero profile availability", 0, ScheduleType.OnOff));
        Zone assignedWithAvailability = CreateZone(
            "ZONE-ASSIGNED-AVAILABLE",
            "Assigned with availability",
            Schedule.Constant("Active profile availability", 1, ScheduleType.OnOff));
        Schedule customOnly = Schedule.Constant(
            "Custom assignment availability",
            1,
            ScheduleType.OnOff);
        var model = new EnergyModel(
            "Conditioning truth table",
            new[]
            {
                assignedWithoutAvailability,
                availabilityWithoutAssignment,
                assignedWithZeroAvailability,
                assignedWithAvailability,
            },
            new[]
            {
                Assignment(assignedWithoutAvailability, "UNAVAILABLE", customOnly),
                Assignment(assignedWithZeroAvailability, "ZERO"),
                Assignment(assignedWithAvailability, "AVAILABLE"),
            });

        IReadOnlyList<Zone> conditioned = model.ConditionedZones;
        IReadOnlyList<Zone> unconditioned = model.UnconditionedZones;

        Assert.Equal(2, conditioned.Count);
        Assert.Same(assignedWithZeroAvailability, conditioned[0]);
        Assert.Same(assignedWithAvailability, conditioned[1]);
        Assert.Equal(2, unconditioned.Count);
        Assert.Same(assignedWithoutAvailability, unconditioned[0]);
        Assert.Same(availabilityWithoutAssignment, unconditioned[1]);
        Assert.Equal(model.Zones.Count, conditioned.Count + unconditioned.Count);
    }

    [Fact]
    public void SetpointValidationAppliesOnlyToConditionedAssignments()
    {
        Zone unconditioned = CreateZone(
            "ZONE-UNCONDITIONED",
            "Unconditioned",
            includeSetpoints: false);
        Zone conditioned = CreateZone(
            "ZONE-CONDITIONED",
            "Conditioned",
            Schedule.Constant("Conditioned availability", 1, ScheduleType.OnOff),
            includeSetpoints: false);
        var model = new EnergyModel(
            "Setpoint validation",
            new[] { unconditioned, conditioned },
            new[]
            {
                Assignment(unconditioned, "UNCONDITIONED"),
                Assignment(conditioned, "CONDITIONED"),
            });

        ValidationResult result = model.Validate();

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.MODEL.MISSING_HEATING_SETPOINT");
        Assert.Equal(conditioned.Id, diagnostic.ObjectId);
        Assert.DoesNotContain(
            result.Diagnostics,
            item => item.ObjectId == unconditioned.Id
                && item.Code.StartsWith("INVISIBLEDRAGON.MODEL.MISSING_", StringComparison.Ordinal));
    }

    [Fact]
    public void AssignedZoneWithoutProfileAvailabilityUsesIdealLoadsAndOmitsExplicitHvac()
    {
        Zone zone = CreateZone("ZONE-UNCONDITIONED", "Unconditioned zone");
        Schedule customAvailability = Schedule.Constant(
            "Custom-only availability",
            1,
            ScheduleType.OnOff);
        var source = new HeatPump(
            new EntityId("SOURCE-UNCONDITIONED"),
            "Unconditioned source",
            Fuel.Electricity,
            3,
            3);
        var terminal = new AirHandlingUnit(
            new EntityId("SUPPLY-UNCONDITIONED"),
            "Unconditioned terminal",
            source);
        var model = new EnergyModel(
            "Unconditioned assigned zone",
            new[] { zone },
            new[]
            {
                new ZoneHvacAssignment(
                    zone.Id,
                    new SupplyGroup(new[] { terminal }, new[] { customAvailability })),
            });

        IdfDocument document = model.ToIdfDocument();

        Assert.Empty(model.ConditionedZones);
        Assert.Same(zone, Assert.Single(model.UnconditionedZones));
        Assert.Empty(document["AirConditioner:VariableRefrigerantFlow"]);
        Assert.Empty(document["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"]);
        Assert.Empty(document["Sizing:Zone"]);
        Assert.Empty(document["ZoneControl:Thermostat"]);
        Assert.DoesNotContain(
            document["Schedule:Compact"],
            item => item.Name == customAvailability.Name);
        IdfObject idealLoads = Assert.Single(document["HVACTemplate:Zone:IdealLoadsAirSystem"]);
        Assert.Equal(zone.Name, idealLoads.Name);
    }

    [Fact]
    public void ZeroValuedProfileAvailabilityEmitsExplicitHvacAndProfileSchedule()
    {
        Schedule profileAvailability = Schedule.Constant(
            "Zero profile availability",
            0,
            ScheduleType.OnOff);
        Zone zone = CreateZone(
            "ZONE-CONDITIONED-ZERO",
            "Zero availability zone",
            profileAvailability);
        var source = new HeatPump(
            new EntityId("SOURCE-CONDITIONED-ZERO"),
            "Conditioned source",
            Fuel.Electricity,
            3,
            3);
        var terminal = new AirHandlingUnit(
            new EntityId("SUPPLY-CONDITIONED-ZERO"),
            "Conditioned terminal",
            source);
        var model = new EnergyModel(
            "Zero availability conditioned zone",
            new[] { zone },
            new[]
            {
                new ZoneHvacAssignment(zone.Id, new SupplyGroup(new[] { terminal })),
            });

        IdfDocument document = model.ToIdfDocument();

        Assert.Same(zone, Assert.Single(model.ConditionedZones));
        Assert.Empty(model.UnconditionedZones);
        Assert.Single(document["AirConditioner:VariableRefrigerantFlow"]);
        IdfObject emittedTerminal = Assert.Single(
            document["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"]);
        Assert.Equal(profileAvailability.Name, emittedTerminal[1]);
        Assert.Single(document["Sizing:Zone"]);
        Assert.Single(document["ZoneControl:Thermostat"]);
        Assert.Empty(document["HVACTemplate:Zone:IdealLoadsAirSystem"]);
        IdfObject emittedAvailability = Assert.Single(
            document["Schedule:Compact"],
            item => item.Name == profileAvailability.Name);
        Assert.Contains(emittedAvailability.Fields, field => field.Value == "0");
    }

    private static ZoneHvacAssignment Assignment(
        Zone zone,
        string suffix,
        Schedule? availability = null)
    {
        var radiator = new ElectricRadiator(
            new EntityId($"SUPPLY-{suffix}"),
            $"Radiator {suffix}");
        return new ZoneHvacAssignment(
            zone.Id,
            new SupplyGroup(new[] { radiator }, new[] { availability }));
    }

    private static Zone CreateZone(
        string id,
        string name,
        Schedule? hvacAvailability = null,
        bool includeSetpoints = true)
    {
        Schedule? heating = includeSetpoints
            ? Schedule.Constant($"{name} heating", 20, ScheduleType.Temperature)
            : null;
        Schedule? cooling = includeSetpoints
            ? Schedule.Constant($"{name} cooling", 26, ScheduleType.Temperature)
            : null;
        var profile = new ZoneProfile(
            new EntityId($"{id}-PROFILE"),
            $"{name} profile",
            heating,
            cooling,
            hvacAvailability);
        Surface floor = TestDomainFactory.Surface(
            $"{id}-FLOOR",
            $"{name} floor",
            TestDomainFactory.Square(),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        return new Zone(new EntityId(id), name, new[] { floor }, profile);
    }
}
