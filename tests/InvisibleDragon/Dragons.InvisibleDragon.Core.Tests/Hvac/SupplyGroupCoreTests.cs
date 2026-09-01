using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Profile;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class SupplyGroupCoreTests
{
    [Fact]
    public void CapabilityProjectionsPreserveOrderIdentityAndFreshResults()
    {
        var source = HeatPump("CAPABILITY-SOURCE", "Capability source");
        var coolingOnly = new PackagedAirConditioner(
            new EntityId("CAPABILITY-COOLING"),
            "Cooling only",
            source);
        var heatingOnly = new ElectricRadiator(
            new EntityId("CAPABILITY-HEATING"),
            "Heating only");
        var dualMode = new AirHandlingUnit(
            new EntityId("CAPABILITY-DUAL"),
            "Dual mode",
            source);
        var systems = new List<SupplySystem> { coolingOnly, heatingOnly, dualMode };
        Schedule availability = Schedule.Constant(
            "Capability availability",
            1,
            ScheduleType.OnOff);
        var availabilities = new List<Schedule?> { null, availability, null };

        var group = new SupplyGroup(systems, availabilities);
        systems.Clear();
        availabilities[1] = null;

        Assert.Equal(3, group.Systems.Count);
        Assert.Same(coolingOnly, group.Systems[0]);
        Assert.Same(heatingOnly, group.Systems[1]);
        Assert.Same(dualMode, group.Systems[2]);
        Assert.Null(group.Availabilities[0]);
        Assert.Same(availability, group.Availabilities[1]);
        Assert.Null(group.Availabilities[2]);

        IReadOnlyList<SupplySystem> firstHeating = group.HeatingSystems;
        IReadOnlyList<SupplySystem> secondHeating = group.HeatingSystems;
        Assert.NotSame(firstHeating, secondHeating);
        Assert.Equal(2, firstHeating.Count);
        Assert.Same(heatingOnly, firstHeating[0]);
        Assert.Same(dualMode, firstHeating[1]);

        IReadOnlyList<SupplySystem> firstCooling = group.CoolingSystems;
        IReadOnlyList<SupplySystem> secondCooling = group.CoolingSystems;
        Assert.NotSame(firstCooling, secondCooling);
        Assert.Equal(2, firstCooling.Count);
        Assert.Same(coolingOnly, firstCooling[0]);
        Assert.Same(dualMode, firstCooling[1]);

        Assert.True(group.CanHeat);
        Assert.True(group.CanCool);

        var heatingGroup = new SupplyGroup(new[] { heatingOnly });
        Assert.True(heatingGroup.CanHeat);
        Assert.False(heatingGroup.CanCool);

        var coolingGroup = new SupplyGroup(new[] { coolingOnly });
        Assert.False(coolingGroup.CanHeat);
        Assert.True(coolingGroup.CanCool);
    }

    [Fact]
    public void ConstructorEnforcesNativeIdentityAndAvailabilityInvariants()
    {
        var first = new ElectricRadiator(
            new EntityId("STRICT-SUPPLY"),
            "Strict supply");
        var duplicateIdentifier = new ElectricRadiator(
            new EntityId("STRICT-SUPPLY"),
            "Duplicate logical supply");
        Schedule temperature = Schedule.Constant(
            "Not an availability",
            20,
            ScheduleType.Temperature);

        Assert.Throws<ArgumentNullException>(() => new SupplyGroup(null!));
        Assert.Throws<ArgumentException>(
            () => new SupplyGroup(Array.Empty<SupplySystem>()));
        Assert.Throws<ArgumentException>(
            () => new SupplyGroup(new SupplySystem[] { first, null! }));
        Assert.Throws<ArgumentException>(
            () => new SupplyGroup(new SupplySystem[] { first, duplicateIdentifier }));
        Assert.Throws<ArgumentException>(
            () => new SupplyGroup(new[] { first }, Array.Empty<Schedule?>()));
        Assert.Throws<ArgumentException>(
            () => new SupplyGroup(new[] { first }, new Schedule?[] { temperature }));
    }

    [Fact]
    public void SourcesUseFirstEncounteredStableIdentifierAndReturnFreshResults()
    {
        HeatPump firstLogicalSource = HeatPump("LOGICAL-SOURCE", "Logical source");
        HeatPump equivalentSourceCopy = HeatPump("LOGICAL-SOURCE", "Logical source");
        HeatPump otherSource = HeatPump("OTHER-SOURCE", "Other source");
        var first = new AirHandlingUnit(
            new EntityId("SOURCE-SUPPLY-FIRST"),
            "First terminal",
            firstLogicalSource);
        var sourceFree = new ElectricRadiator(
            new EntityId("SOURCE-SUPPLY-FREE"),
            "Source-free terminal");
        var equivalentCopyTerminal = new AirHandlingUnit(
            new EntityId("SOURCE-SUPPLY-COPY"),
            "Equivalent-copy terminal",
            equivalentSourceCopy);
        var sharedReferenceTerminal = new AirHandlingUnit(
            new EntityId("SOURCE-SUPPLY-SHARED"),
            "Shared-reference terminal",
            firstLogicalSource);
        var other = new AirHandlingUnit(
            new EntityId("SOURCE-SUPPLY-OTHER"),
            "Other terminal",
            otherSource);
        var group = new SupplyGroup(new SupplySystem[]
        {
            first,
            sourceFree,
            equivalentCopyTerminal,
            sharedReferenceTerminal,
            other,
        });

        IReadOnlyList<SourceSystem> firstRead = group.Sources;
        IReadOnlyList<SourceSystem> secondRead = group.Sources;

        Assert.NotSame(firstRead, secondRead);
        Assert.Equal(2, firstRead.Count);
        Assert.Same(firstLogicalSource, firstRead[0]);
        Assert.Same(otherSource, firstRead[1]);
        Assert.DoesNotContain(equivalentSourceCopy, firstRead);
        Assert.Same(firstLogicalSource, secondRead[0]);
        Assert.Same(otherSource, secondRead[1]);
    }

    private static HeatPump HeatPump(string id, string name) => new(
        new EntityId(id),
        name,
        Fuel.Electricity,
        3,
        3);
}
