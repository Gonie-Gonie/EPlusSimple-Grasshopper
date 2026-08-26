using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelLegacyAssemblyTests
{
    [Fact]
    public void LegacyDefaultObjectFieldsAreExplicitAndIndependentFromScheduleMetadataOnlyMode()
    {
        var model = new EnergyModel("Default fields", Array.Empty<Zone>());

        IdfDocument safe = model.ToIdfDocument();
        IdfDocument metadataOnly = model.ToIdfDocument(
            options: new EnergyModelIdfOptions
            {
                UseLegacySimpleDragonScheduleMetadata = true,
            });
        IdfDocument exact = model.ToIdfDocument(
            options: new EnergyModelIdfOptions
            {
                UseLegacySimpleDragonDefaultObjectFields = true,
            });

        Assert.False(new EnergyModelIdfOptions().UseLegacySimpleDragonDefaultObjectFields);
        AssertFields(
            Assert.Single(safe["GlobalGeometryRules"]),
            "UpperLeftCorner", "CounterClockwise", "World");
        AssertFields(
            Assert.Single(safe["Schedule:Constant"]),
            "$DEFAULT$PEOPLEACTIVITY", "ScheduleTypeLimits:Real", "107.0");

        AssertFields(
            Assert.Single(metadataOnly["GlobalGeometryRules"]),
            "UpperLeftCorner", "CounterClockwise", "World");
        AssertFields(
            Assert.Single(metadataOnly["Schedule:Constant"]),
            "$DEFAULT$PEOPLEACTIVITY", "Real", "107.0");
        Assert.Equal(string.Empty, metadataOnly["Schedule:Compact"]["ALLON"][1]);
        Assert.Equal(string.Empty, metadataOnly["Schedule:Compact"]["ALLOFF"][1]);

        AssertFields(
            Assert.Single(exact["GlobalGeometryRules"]),
            "UpperLeftCorner", "Counterclockwise", "World", "Relative", "Relative");
        AssertFields(
            Assert.Single(exact["Schedule:Constant"]),
            "$DEFAULT$PEOPLEACTIVITY", "real", "107.0");
        Assert.Equal(string.Empty, exact["Schedule:Compact"]["ALLON"][1]);
        Assert.Equal(string.Empty, exact["Schedule:Compact"]["ALLOFF"][1]);
    }

    [Fact]
    public void LegacyUsedProfileSelectionUsesLastExactNameAndKeepsEarlierZoneReference()
    {
        Zone first = CreateZone(
            "DUPLICATE-FIRST",
            new ZoneProfile(
                new EntityId("PROFILE-DUPLICATE-FIRST"),
                "DuplicateProfile",
                lighting: Schedule.Constant("Light-A", 1, ScheduleType.OnOff)),
            0);
        Zone second = CreateZone(
            "DUPLICATE-SECOND",
            new ZoneProfile(
                new EntityId("PROFILE-DUPLICATE-SECOND"),
                "DuplicateProfile",
                lighting: Schedule.Constant("Light-B", 0, ScheduleType.OnOff)),
            2);
        var model = new EnergyModel("Duplicate profiles", new[] { first, second });

        IdfDocument safe = model.ToIdfDocument();
        IdfDocument legacy = model.ToIdfDocument(
            options: LegacyOptions(usedProfileSelection: true));

        Assert.False(new EnergyModelIdfOptions().UseLegacySimpleDragonUsedProfileScheduleSelection);
        AssertScheduleNames(safe, "Light-A", "Light-B");
        AssertScheduleNames(legacy, "Light-B");
        Assert.Equal(
            new[] { "Light-A", "Light-B" },
            legacy["Lights"].Select(item => item[2]));
    }

    [Fact]
    public void LegacyUsedProfileSelectionKeepsCaseDistinctProfilesInInputOrder()
    {
        Zone upper = CreateZone(
            "CASE-UPPER",
            new ZoneProfile(
                new EntityId("PROFILE-CASE-UPPER"),
                "CaseProfile",
                lighting: Schedule.Constant("CaseLight", 1, ScheduleType.OnOff)),
            0);
        Zone lower = CreateZone(
            "CASE-LOWER",
            new ZoneProfile(
                new EntityId("PROFILE-CASE-LOWER"),
                "caseprofile",
                lighting: Schedule.Constant("caselight", 0, ScheduleType.OnOff)),
            2);
        var model = new EnergyModel("Case-sensitive profiles", new[] { upper, lower });

        Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument());
        IdfDocument document = model.ToIdfDocument(
            options: LegacyOptions(usedProfileSelection: true));

        AssertScheduleNames(document, "CaseLight", "caselight");
    }

    [Fact]
    public void LegacyTopologySharesUnconditionedThermostatAndDropsUnavailableSupply()
    {
        Zone assigned = CreateZone(
            "UNCONDITIONED-ASSIGNED",
            new ZoneProfile(
                new EntityId("PROFILE-UNCONDITIONED-ASSIGNED"),
                "Assigned without HVAC availability"),
            0);
        Zone unassigned = CreateZone(
            "UNCONDITIONED-UNASSIGNED",
            new ZoneProfile(
                new EntityId("PROFILE-UNCONDITIONED-UNASSIGNED"),
                "Unassigned"),
            2);
        var radiator = new ElectricRadiator(
            new EntityId("RADIATOR-UNAVAILABLE"),
            "Unavailable radiator");
        var model = new EnergyModel(
            "Shared unconditioned fallback",
            new[] { assigned, unassigned },
            new[]
            {
                new ZoneHvacAssignment(
                    assigned.Id,
                    new SupplyGroup(new SupplySystem[] { radiator })),
            });

        IdfDocument safe = model.ToIdfDocument();
        IdfDocument legacy = model.ToIdfDocument(
            options: LegacyOptions(hvacTopology: true));

        Assert.Equal(2, safe["HVACTemplate:Thermostat"].Count);
        Assert.Equal(
            new[]
            {
                $"IdealThermostat_for_{assigned.Name}",
                $"IdealThermostat_for_{unassigned.Name}",
            },
            safe["HVACTemplate:Thermostat"].Select(item => item.Name));

        IdfObject thermostat = Assert.Single(legacy["HVACTemplate:Thermostat"]);
        AssertFields(
            thermostat,
            "UNCONDITIONED_THERMOSTAT", string.Empty, "-30", string.Empty, "50");
        Assert.Equal(2, legacy["HVACTemplate:Zone:IdealLoadsAirSystem"].Count);
        AssertFields(
            legacy["HVACTemplate:Zone:IdealLoadsAirSystem"][0],
            assigned.Name, "UNCONDITIONED_THERMOSTAT", "ALLON");
        AssertFields(
            legacy["HVACTemplate:Zone:IdealLoadsAirSystem"][1],
            unassigned.Name, "UNCONDITIONED_THERMOSTAT", "ALLON");
        Assert.Empty(legacy["ZoneHVAC:Baseboard:RadiantConvective:Electric"]);
        Assert.Empty(legacy["Sizing:Zone"]);
        Assert.Empty(legacy["ZoneControl:Thermostat"]);
    }

    [Fact]
    public void LegacyVentilationRetainsSharedIdealFallbackOnlyWithTopologyEnabled()
    {
        Schedule occupant = Schedule.Constant(
            "ERV occupancy",
            0.1,
            ScheduleType.Real);
        Zone zone = CreateZone(
            "ERV-ZONE",
            new ZoneProfile(
                new EntityId("PROFILE-ERV-ZONE"),
                "ERV profile",
                occupant: occupant),
            0);
        var ventilator = new EnergyRecoveryVentilator(
            new EntityId("LEGACY-ERV"),
            "Legacy ERV",
            0.7,
            0.5,
            0.2);
        var model = new EnergyModel(
            "Legacy ERV fallback",
            new[] { zone },
            ventilationAssignments: new[]
            {
                new ZoneVentilationAssignment(zone.Id, ventilator),
            });

        IdfDocument legacy = model.ToIdfDocument(
            options: LegacyOptions(hvacTopology: true, ventilation: true));

        IdfObject reduced = Assert.Single(legacy["ZoneVentilation:DesignFlowRate"]);
        Assert.Equal("Flow/Person", reduced[3]);
        Assert.Equal("0.00332", reduced[6]);
        Assert.Equal("Exhaust", reduced[8]);
        Assert.Equal("125.0", reduced[9]);
        Assert.Equal("0.85", reduced[10]);
        Assert.Empty(legacy["OutdoorAir:Node"]);
        Assert.Empty(legacy["HeatExchanger:AirToAir:SensibleAndLatent"]);
        Assert.Empty(legacy["Fan:OnOff"]);
        Assert.Empty(legacy["ZoneHVAC:EnergyRecoveryVentilator:Controller"]);
        Assert.Empty(legacy["ZoneHVAC:EnergyRecoveryVentilator"]);
        AssertFields(
            Assert.Single(legacy["HVACTemplate:Thermostat"]),
            "UNCONDITIONED_THERMOSTAT", string.Empty, "-30", string.Empty, "50");
        AssertFields(
            Assert.Single(legacy["HVACTemplate:Zone:IdealLoadsAirSystem"]),
            zone.Name, "UNCONDITIONED_THERMOSTAT", "ALLON");

        EnergyModelIdfOptions disabledOptions = LegacyOptions(
            hvacTopology: true,
            ventilation: true);
        disabledOptions.AddIdealLoadsForUnassignedZones = false;
        IdfDocument disabled = model.ToIdfDocument(options: disabledOptions);
        Assert.Single(disabled["ZoneVentilation:DesignFlowRate"]);
        Assert.Empty(disabled["HVACTemplate:Thermostat"]);
        Assert.Empty(disabled["HVACTemplate:Zone:IdealLoadsAirSystem"]);
    }

    private static EnergyModelIdfOptions LegacyOptions(
        bool usedProfileSelection = false,
        bool hvacTopology = false,
        bool ventilation = false)
    {
        return new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonScheduleMetadata = true,
            UseLegacySimpleDragonUsedProfileScheduleSelection = usedProfileSelection,
            UseLegacySimpleDragonHvacTopology = hvacTopology,
            UseLegacySimpleDragonVentilation = ventilation,
        };
    }

    private static Zone CreateZone(
        string label,
        ZoneProfile profile,
        double x)
    {
        Surface floor = TestDomainFactory.Surface(
            $"SURFACE-{label}",
            $"Floor {label}",
            TestDomainFactory.Square(x: x),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        return new Zone(
            new EntityId($"ZONE-{label}"),
            label,
            new[] { floor },
            profile,
            lightingPowerDensityWattsPerSquareMetre: 5);
    }

    private static void AssertScheduleNames(
        IdfDocument document,
        params string[] expected)
    {
        HashSet<string> names = new(expected, StringComparer.Ordinal);
        Assert.Equal(
            expected,
            document["Schedule:Compact"]
                .Where(item => item.Name is not null && names.Contains(item.Name))
                .Select(item => item.Name));
    }

    private static void AssertFields(IdfObject item, params string[] expected)
    {
        Assert.Equal(expected, item.Fields.Select(field => field.Value));
    }
}
