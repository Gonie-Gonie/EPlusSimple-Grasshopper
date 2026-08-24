using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelValidationTests
{
    [Fact]
    public void ReportsDuplicateIdsNamesAndUnknownAssignmentReferences()
    {
        Zone first = Zone("ZONE-DUP", "Repeated", "SURFACE-A");
        Zone second = Zone("ZONE-DUP", "Repeated", "SURFACE-B");
        var heatPump = new HeatPump(new EntityId("SOURCE"), "Source", Fuel.Electricity, 3, 3);
        var air = new AirHandlingUnit(new EntityId("SUPPLY"), "Supply", heatPump);
        var assignment = new ZoneHvacAssignment(
            new EntityId("ZONE-MISSING"),
            new SupplyGroup(new[] { air }));
        var model = new EnergyModel("Invalid", new[] { first, second }, new[] { assignment });

        ValidationResult result = model.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.DUPLICATE_ZONE_ID");
        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.DUPLICATE_ZONE_NAME");
        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.UNKNOWN_HVAC_ZONE");
        Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument());
    }

    [Fact]
    public void PromotesMissingCrossZoneAdjacencyToModelError()
    {
        var surface = TestDomainFactory.Surface(
            "SURFACE-ADJ",
            "Adjacent",
            TestDomainFactory.Square(),
            boundary: SurfaceBoundary.AdjacentTo(new EntityId("SURFACE-MISSING")));
        var zone = new Zone(
            new EntityId("ZONE-ADJ"),
            "Zone",
            new[] { surface },
            TestDomainFactory.EmptyProfile());
        var model = new EnergyModel("Adjacency", new[] { zone });

        ValidationResult result = model.Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.ADJACENT_SURFACE_MISSING");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatesGeometryForReciprocalCrossZoneAdjacency()
    {
        var firstId = new EntityId("SURFACE-FIRST");
        var secondId = new EntityId("SURFACE-SECOND");
        Surface first = TestDomainFactory.Surface(
            firstId.Value,
            "First adjacent surface",
            TestDomainFactory.Square(2),
            boundary: SurfaceBoundary.AdjacentTo(secondId));
        Surface second = TestDomainFactory.Surface(
            secondId.Value,
            "Second adjacent surface",
            TestDomainFactory.Square(2, x: 0.25),
            boundary: SurfaceBoundary.AdjacentTo(firstId));
        var firstZone = new Zone(
            new EntityId("ZONE-FIRST"),
            "First zone",
            new[] { first },
            TestDomainFactory.EmptyProfile("PROFILE-FIRST"));
        var secondZone = new Zone(
            new EntityId("ZONE-SECOND"),
            "Second zone",
            new[] { second },
            TestDomainFactory.EmptyProfile("PROFILE-SECOND"));
        var model = new EnergyModel("Invalid geometry", new[] { firstZone, secondZone });

        ValidationResult result = model.Validate();

        Assert.False(result.IsValid);
        Assert.Single(
            result.Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.ADJACENCY.GEOMETRY_MISMATCH");
    }

    [Fact]
    public void ReportsConflictingMaterialAndConstructionNames()
    {
        var material = new Material("Concrete", 1.4, 2200, 880);
        var firstConstruction = new OpaqueConstruction(
            "Shared construction",
            new[] { new Layer("Shared material", material, 0.1) });
        var secondConstruction = new OpaqueConstruction(
            "Shared construction",
            new[] { new Layer("Shared material", material, 0.2) });
        Surface first = new(
            new EntityId("SURFACE-FIRST"),
            "First floor",
            SurfaceType.Floor,
            firstConstruction,
            SurfaceBoundary.Ground,
            TestDomainFactory.Square(2));
        Surface second = new(
            new EntityId("SURFACE-SECOND"),
            "Second floor",
            SurfaceType.Floor,
            secondConstruction,
            SurfaceBoundary.Ground,
            TestDomainFactory.Square(2, x: 3));
        var model = new EnergyModel(
            "Conflicting constructions",
            new[]
            {
                new Zone(new EntityId("ZONE-FIRST"), "First", new[] { first }, TestDomainFactory.EmptyProfile("PROFILE-FIRST")),
                new Zone(new EntityId("ZONE-SECOND"), "Second", new[] { second }, TestDomainFactory.EmptyProfile("PROFILE-SECOND")),
            });

        ValidationResult result = model.Validate();

        Assert.Contains(
            result.Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_MATERIAL_NAME");
        Assert.Contains(
            result.Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_CONSTRUCTION_NAME");
        Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument(
            options: new EnergyModelIdfOptions { ThrowOnValidationErrors = false }));
    }

    [Fact]
    public void ReportsConflictingGlazingNames()
    {
        var firstWindow = new Window(
            new EntityId("WINDOW-FIRST"),
            "First window",
            new Glazing("Shared glazing", 1.2, 0.4),
            TestDomainFactory.Square(1, x: 0.5, y: 0.5));
        var secondWindow = new Window(
            new EntityId("WINDOW-SECOND"),
            "Second window",
            new Glazing("Shared glazing", 2.4, 0.4),
            TestDomainFactory.Square(1, x: 3.5, y: 0.5));
        Surface first = TestDomainFactory.Surface(
            "SURFACE-FIRST",
            "First wall",
            TestDomainFactory.Square(2),
            openings: new[] { firstWindow });
        Surface second = TestDomainFactory.Surface(
            "SURFACE-SECOND",
            "Second wall",
            TestDomainFactory.Square(2, x: 3),
            openings: new[] { secondWindow });
        var model = new EnergyModel(
            "Conflicting glazing",
            new[]
            {
                new Zone(new EntityId("ZONE-FIRST"), "First", new[] { first }, TestDomainFactory.EmptyProfile("PROFILE-FIRST")),
                new Zone(new EntityId("ZONE-SECOND"), "Second", new[] { second }, TestDomainFactory.EmptyProfile("PROFILE-SECOND")),
            });

        ValidationResult result = model.Validate();

        Assert.Contains(
            result.Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_GLAZING_NAME");
        Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument(
            options: new EnergyModelIdfOptions { ThrowOnValidationErrors = false }));
    }

    [Fact]
    public void ReportsConflictingSharedSourceIdentifier()
    {
        Zone first = Zone("ZONE-1", "First", "SURFACE-1");
        Zone second = Zone("ZONE-2", "Second", "SURFACE-2");
        var sourceA = new HeatPump(new EntityId("SOURCE-SAME"), "Source A", Fuel.Electricity, 3, 3);
        var sourceB = new HeatPump(new EntityId("SOURCE-SAME"), "Source B", Fuel.Electricity, 3, 3);
        var assignments = new[]
        {
            new ZoneHvacAssignment(first.Id, new SupplyGroup(new[] { new AirHandlingUnit(new EntityId("AHU-A"), "AHU A", sourceA) })),
            new ZoneHvacAssignment(second.Id, new SupplyGroup(new[] { new AirHandlingUnit(new EntityId("AHU-B"), "AHU B", sourceB) })),
        };
        var model = new EnergyModel("Sources", new[] { first, second }, assignments);

        ValidationResult result = model.Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
    }

    [Fact]
    public void ReportsSharedSourceIdentifierWhenParametersDifferDespiteSameName()
    {
        Zone first = Zone("ZONE-1", "First", "SURFACE-1");
        Zone second = Zone("ZONE-2", "Second", "SURFACE-2");
        var sourceA = new HeatPump(new EntityId("SOURCE-SAME"), "Shared source", Fuel.Electricity, 3, 3);
        var sourceB = new HeatPump(new EntityId("SOURCE-SAME"), "Shared source", Fuel.Electricity, 4, 3);
        var assignments = new[]
        {
            new ZoneHvacAssignment(first.Id, new SupplyGroup(new[] { new AirHandlingUnit(new EntityId("AHU-A"), "AHU A", sourceA) })),
            new ZoneHvacAssignment(second.Id, new SupplyGroup(new[] { new AirHandlingUnit(new EntityId("AHU-B"), "AHU B", sourceB) })),
        };
        var model = new EnergyModel("Sources", new[] { first, second }, assignments);

        ValidationResult result = model.Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
        Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument(
            options: new EnergyModelIdfOptions { ThrowOnValidationErrors = false }));
    }

    [Fact]
    public void ReportsSharedChillerIdentifierWhenCoolingTowerDefinitionDiffers()
    {
        Zone first = Zone("ZONE-1", "First", "SURFACE-1");
        Zone second = Zone("ZONE-2", "Second", "SURFACE-2");
        var sourceA = new Chiller(
            new EntityId("SOURCE-SAME"),
            "Shared chiller",
            3.2,
            CompressorType.Turbo,
            new OpenSingleSpeedCoolingTower(
                new EntityId("TOWER-SAME"),
                "Shared tower",
                nominalCapacityWatts: 100_000));
        var sourceB = new Chiller(
            new EntityId("SOURCE-SAME"),
            "Shared chiller",
            3.2,
            CompressorType.Turbo,
            new OpenSingleSpeedCoolingTower(
                new EntityId("TOWER-SAME"),
                "Shared tower",
                nominalCapacityWatts: 120_000));
        var assignments = new[]
        {
            new ZoneHvacAssignment(first.Id, new SupplyGroup(new[] { new FanCoilUnit(new EntityId("FCU-A"), "FCU A", sourceA) })),
            new ZoneHvacAssignment(second.Id, new SupplyGroup(new[] { new FanCoilUnit(new EntityId("FCU-B"), "FCU B", sourceB) })),
        };
        var model = new EnergyModel("Cold sources", new[] { first, second }, assignments);

        ValidationResult result = model.Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
    }

    [Fact]
    public void ReportsSharedAbsorptionChillerIdentifierWhenGeneratorDefinitionDiffers()
    {
        Zone first = Zone("ZONE-1", "First", "SURFACE-1");
        Zone second = Zone("ZONE-2", "Second", "SURFACE-2");
        var sourceA = new AbsorptionChiller(
            new EntityId("SOURCE-SAME"),
            "Shared absorption chiller",
            0.72,
            new Boiler(new EntityId("GENERATOR-SAME"), "Shared generator", Fuel.NaturalGas, 0.85),
            new ClosedSingleSpeedCoolingTower(new EntityId("TOWER-SAME"), "Shared tower"));
        var sourceB = new AbsorptionChiller(
            new EntityId("SOURCE-SAME"),
            "Shared absorption chiller",
            0.72,
            new Boiler(new EntityId("GENERATOR-SAME"), "Shared generator", Fuel.NaturalGas, 0.9),
            new ClosedSingleSpeedCoolingTower(new EntityId("TOWER-SAME"), "Shared tower"));
        var assignments = new[]
        {
            new ZoneHvacAssignment(first.Id, new SupplyGroup(new[] { new FanCoilUnit(new EntityId("FCU-A"), "FCU A", sourceA) })),
            new ZoneHvacAssignment(second.Id, new SupplyGroup(new[] { new FanCoilUnit(new EntityId("FCU-B"), "FCU B", sourceB) })),
        };
        var model = new EnergyModel("Absorption sources", new[] { first, second }, assignments);

        ValidationResult result = model.Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
    }

    [Fact]
    public void ReportsSharedPhotovoltaicIdentifierWhenParametersDifferDespiteSameName()
    {
        Zone zone = Zone("ZONE-1", "First", "SURFACE-1");
        var panels = new[]
        {
            new PhotovoltaicPanel(new EntityId("PV-SAME"), "Shared PV", 10, 30, 180, 0.2),
            new PhotovoltaicPanel(new EntityId("PV-SAME"), "Shared PV", 12, 30, 180, 0.2),
        };
        var model = new EnergyModel("PV", new[] { zone }, photovoltaicPanels: panels);

        ValidationResult result = model.Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
    }

    private static Zone Zone(string id, string name, string surfaceId)
    {
        Schedule heating = Schedule.Constant($"{name} heat", 20, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant($"{name} cool", 26, ScheduleType.Temperature);
        var profile = new ZoneProfile(new EntityId($"{id}-PROFILE"), $"{name} profile", heating, cooling);
        Surface floor = TestDomainFactory.Surface(
            surfaceId,
            $"{name} floor",
            TestDomainFactory.Square(),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        return new Zone(new EntityId(id), name, new[] { floor }, profile);
    }
}
