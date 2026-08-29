using GH_IO.Serialization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class AuthoringDefinitionGooTests
{
    [Fact]
    public void OpeningAndZoneDefinitionsDefensivelyOwnTheirGeometryAndItems()
    {
        RunWithNativeGeometry(() =>
        {
        FenestrationConstruction fenestration = TransparentConstruction();
        using Curve openingSource = OpeningCurve();
        var opening = new OpeningDefinition(
            openingSource,
            "South Window",
            FenestrationType.Window,
            fenestration,
            BlindType.Shade,
            new EntityId("OPENING-AUTHORING"));
        BoundingBox expectedOpeningBounds = Bounds(opening.Geometry);

        Assert.True(openingSource.Transform(Transform.Translation(100d, 0d, 0d)));
        using (Curve firstRead = opening.Geometry)
        {
            Assert.Equal(expectedOpeningBounds, firstRead.GetBoundingBox(true));
            Assert.True(firstRead.Transform(Transform.Translation(0d, 100d, 0d)));
        }

        using (Curve secondRead = opening.Geometry)
        {
            Assert.Equal(expectedOpeningBounds, secondRead.GetBoundingBox(true));
        }

        Assert.NotSame(fenestration, opening.Construction);

        UsageProfile profile = Profile();
        SurfaceConstruction surfaceConstruction = OpaqueConstruction();
        SupplySystem supply = Supply();
        VentilationSystem ventilator = Ventilator();
        var assignment = new VentilationAssignment(ventilator.Id.Value, 2, ventilator);
        using Brep zoneSource = ZoneBrep();
        var zone = new ZoneDefinition(
            zoneSource,
            "Ground Floor",
            1,
            profile,
            surfaceConstruction,
            fenestration,
            SurfaceBoundaryCondition.Ground,
            8.5d,
            new[] { opening },
            new[] { supply },
            new[] { assignment });
        BoundingBox expectedZoneBounds = Bounds(zone.Geometry);

        Assert.True(zoneSource.Transform(Transform.Translation(200d, 0d, 0d)));
        using (Brep firstRead = zone.Geometry)
        {
            Assert.Equal(expectedZoneBounds, firstRead.GetBoundingBox(true));
            Assert.True(firstRead.Transform(Transform.Translation(0d, 200d, 0d)));
        }

        using (Brep secondRead = zone.Geometry)
        {
            Assert.Equal(expectedZoneBounds, secondRead.GetBoundingBox(true));
        }

        Assert.NotSame(profile, zone.Profile);
        Assert.NotSame(surfaceConstruction, zone.SurfaceConstruction);
        Assert.NotSame(fenestration, zone.DefaultFenestrationConstruction);
        Assert.NotSame(opening, zone.Openings[0]);
        Assert.NotSame(supply, zone.SupplySystems[0]);
        Assert.NotSame(assignment, zone.VentilationAssignments[0]);
        Assert.NotSame(ventilator, zone.VentilationAssignments[0].VentilationSystem);
        });
    }

    [Fact]
    public void AuthoringGoosDuplicateAndGrasshopperArchiveRoundTripLosslessly()
    {
        RunWithNativeGeometry(() =>
        {
        OpeningDefinition opening = Opening();
        ZoneDefinition zone = Zone(opening);

        var openingGoo = new SimpleDragonOpeningDefinitionGoo(opening);
        var openingDuplicate = Assert.IsType<SimpleDragonOpeningDefinitionGoo>(openingGoo.Duplicate());
        SimpleDragonOpeningDefinitionGoo openingArchived = ArchiveRoundTrip(
            openingGoo,
            new SimpleDragonOpeningDefinitionGoo());
        AssertOpeningEquivalent(opening, openingDuplicate.Value);
        AssertOpeningEquivalent(opening, openingArchived.Value);

        var zoneGoo = new SimpleDragonZoneDefinitionGoo(zone);
        var zoneDuplicate = Assert.IsType<SimpleDragonZoneDefinitionGoo>(zoneGoo.Duplicate());
        SimpleDragonZoneDefinitionGoo zoneArchived = ArchiveRoundTrip(
            zoneGoo,
            new SimpleDragonZoneDefinitionGoo());
        AssertZoneEquivalent(zone, zoneDuplicate.Value);
        AssertZoneEquivalent(zone, zoneArchived.Value);
        });
    }

    [Fact]
    public void VentilationAssignmentGooPromotesRawErvInputsToOneUnit()
    {
        VentilationSystem ventilator = Ventilator();

        var direct = new SimpleDragonVentilationAssignmentGoo();
        Assert.True(direct.CastFrom(ventilator));
        AssertSingleUnitCopy(ventilator, direct.Value);

        var fromGoo = new SimpleDragonVentilationAssignmentGoo();
        Assert.True(fromGoo.CastFrom(new SimpleDragonEnergyRecoveryVentilatorGoo(ventilator)));
        AssertSingleUnitCopy(ventilator, fromGoo.Value);

        var fromWrapper = new SimpleDragonVentilationAssignmentGoo();
        Assert.True(fromWrapper.CastFrom(new GH_ObjectWrapper(ventilator)));
        AssertSingleUnitCopy(ventilator, fromWrapper.Value);
    }

    [Fact]
    public void VentilationAssignmentGooPreservesExplicitCountsAndNestedErv()
    {
        VentilationSystem ventilator = Ventilator();
        var assignment = new VentilationAssignment(ventilator.Id.Value, 4, ventilator);
        var goo = new SimpleDragonVentilationAssignmentGoo(assignment);

        var duplicate = Assert.IsType<SimpleDragonVentilationAssignmentGoo>(goo.Duplicate());
        SimpleDragonVentilationAssignmentGoo archived = ArchiveRoundTrip(
            goo,
            new SimpleDragonVentilationAssignmentGoo());

        AssertAssignmentEquivalent(assignment, duplicate.Value);
        AssertAssignmentEquivalent(assignment, archived.Value);
        Assert.Contains("x4", goo.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoringDefinitionsRejectInvalidOrAmbiguousInputs()
    {
        RunWithNativeGeometry(() =>
        {
        using var openCurve = new LineCurve(Point3d.Origin, new Point3d(1d, 0d, 0d));
        Assert.Throws<ArgumentException>(() => new OpeningDefinition(
            openCurve,
            "Open",
            FenestrationType.Window));

        using var plane = new PlaneSurface(
            Plane.WorldXY,
            new Interval(0d, 1d),
            new Interval(0d, 1d));
        using Brep openBrep = Brep.CreateFromSurface(plane);
        Assert.Throws<ArgumentException>(() => new ZoneDefinition(
            openBrep,
            "Open Zone",
            1,
            Profile()));

        FenestrationConstruction opaque = new(
            "Opaque Door",
            2.2d,
            id: new EntityId("OPAQUE-DOOR"));
        using Brep solid = ZoneBrep();
        Assert.Throws<ArgumentException>(() => new ZoneDefinition(
            solid,
            "Invalid Default",
            1,
            Profile(),
            defaultFenestrationConstruction: opaque));

        SupplySystem first = Supply("DUPLICATE-SUPPLY");
        SupplySystem second = Supply("DUPLICATE-SUPPLY");
        Assert.Throws<ArgumentException>(() => new ZoneDefinition(
            solid,
            "Duplicate HVAC",
            1,
            Profile(),
            supplySystems: new[] { first, second }));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneDefinition(
            solid,
            "Invalid Floor Boundary",
            1,
            Profile(),
            unmatchedFloorBoundary: SurfaceBoundaryCondition.AdjacentSpace));
        });
    }

    [Fact]
    public void AuthoringGooAndParameterTypesArePubliclyDiscoverable()
    {
        Type[] goos =
        {
            typeof(SimpleDragonOpeningDefinitionGoo),
            typeof(SimpleDragonZoneDefinitionGoo),
            typeof(SimpleDragonVentilationAssignmentGoo),
        };
        (Type Type, Guid Guid)[] parameters =
        {
            (typeof(SimpleDragonOpeningDefinitionParam), new Guid("51610fe9-ecf1-43b4-9157-7260b3ba89ad")),
            (typeof(SimpleDragonZoneDefinitionParam), new Guid("3fe45962-67fe-43d4-be95-ad81b91b19eb")),
            (typeof(SimpleDragonVentilationAssignmentParam), new Guid("14f1683e-4b0a-4754-aac5-6b85331c2126")),
        };
        Type[] exported = typeof(SimpleDragonOpeningDefinitionGoo).Assembly.GetExportedTypes();

        Assert.All(goos, type => Assert.Contains(type, exported));
        Assert.All(parameters, item =>
        {
            Assert.Contains(item.Type, exported);
            var parameter = Assert.IsAssignableFrom<IGH_Param>(Activator.CreateInstance(item.Type));
            Assert.Equal(item.Guid, parameter.ComponentGuid);
        });
    }

    private static void AssertOpeningEquivalent(OpeningDefinition expected, OpeningDefinition actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Blind, actual.Blind);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Construction!.Id, actual.Construction!.Id);
        Assert.NotSame(expected.Construction, actual.Construction);
        using Curve expectedGeometry = expected.Geometry;
        using Curve actualGeometry = actual.Geometry;
        Assert.Equal(expectedGeometry.GetBoundingBox(true), actualGeometry.GetBoundingBox(true));
        Assert.Equal(expectedGeometry.GetLength(), actualGeometry.GetLength(), 10);
    }

    private static void AssertZoneEquivalent(ZoneDefinition expected, ZoneDefinition actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.FloorNumber, actual.FloorNumber);
        Assert.Equal(expected.Profile.Id, actual.Profile.Id);
        Assert.Equal(expected.SurfaceConstruction!.Id, actual.SurfaceConstruction!.Id);
        Assert.Equal(
            expected.DefaultFenestrationConstruction!.Id,
            actual.DefaultFenestrationConstruction!.Id);
        Assert.Equal(expected.UnmatchedFloorBoundary, actual.UnmatchedFloorBoundary);
        Assert.Equal(expected.LightDensity, actual.LightDensity);
        Assert.Single(actual.Openings);
        Assert.Single(actual.SupplySystems);
        Assert.Single(actual.VentilationAssignments);
        AssertOpeningEquivalent(expected.Openings[0], actual.Openings[0]);
        Assert.Equal(expected.SupplySystems[0].Id, actual.SupplySystems[0].Id);
        Assert.NotSame(expected.SupplySystems[0], actual.SupplySystems[0]);
        AssertAssignmentEquivalent(
            expected.VentilationAssignments[0],
            actual.VentilationAssignments[0]);
        using Brep expectedGeometry = expected.Geometry;
        using Brep actualGeometry = actual.Geometry;
        Assert.Equal(expectedGeometry.GetBoundingBox(true), actualGeometry.GetBoundingBox(true));
        Assert.Equal(expectedGeometry.GetVolume(), actualGeometry.GetVolume(), 10);
    }

    private static void AssertSingleUnitCopy(
        VentilationSystem expected,
        VentilationAssignment actual)
    {
        Assert.Equal(1, actual.Count);
        Assert.Equal(expected.Id.Value, actual.VentilationSystemId);
        Assert.NotNull(actual.VentilationSystem);
        Assert.NotSame(expected, actual.VentilationSystem);
        Assert.Equal(expected.Id, actual.VentilationSystem!.Id);
        Assert.Equal(expected.AirflowRate, actual.VentilationSystem.AirflowRate);
    }

    private static void AssertAssignmentEquivalent(
        VentilationAssignment expected,
        VentilationAssignment actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.VentilationSystemId, actual.VentilationSystemId);
        Assert.Equal(expected.Count, actual.Count);
        Assert.NotNull(expected.VentilationSystem);
        Assert.NotNull(actual.VentilationSystem);
        Assert.NotSame(expected.VentilationSystem, actual.VentilationSystem);
        Assert.Equal(expected.VentilationSystem!.Id, actual.VentilationSystem!.Id);
        Assert.Equal(expected.VentilationSystem.AirflowRate, actual.VentilationSystem.AirflowRate);
        Assert.Equal(expected.VentilationSystem.HeatingEfficiency, actual.VentilationSystem.HeatingEfficiency);
        Assert.Equal(expected.VentilationSystem.CoolingEfficiency, actual.VentilationSystem.CoolingEfficiency);
    }

    private static BoundingBox Bounds(GeometryBase geometry)
    {
        using (geometry)
        {
            return geometry.GetBoundingBox(true);
        }
    }

    private static OpeningDefinition Opening()
    {
        using Curve geometry = OpeningCurve();
        return new OpeningDefinition(
            geometry,
            "South Window",
            FenestrationType.Window,
            TransparentConstruction(),
            BlindType.Venetian,
            new EntityId("OPENING-ROUNDTRIP"));
    }

    private static ZoneDefinition Zone(OpeningDefinition opening)
    {
        VentilationSystem ventilation = Ventilator();
        using Brep geometry = ZoneBrep();
        return new ZoneDefinition(
            geometry,
            "Round-trip Zone",
            2,
            Profile(),
            OpaqueConstruction(),
            TransparentConstruction(),
            SurfaceBoundaryCondition.Ground,
            9.25d,
            new[] { opening },
            new[] { Supply() },
            new[] { new VentilationAssignment(ventilation.Id.Value, 3, ventilation) });
    }

    private static PolylineCurve OpeningCurve()
    {
        return new PolylineCurve(new[]
        {
            new Point3d(2d, 0d, 0.8d),
            new Point3d(5d, 0d, 0.8d),
            new Point3d(5d, 0d, 2.2d),
            new Point3d(2d, 0d, 2.2d),
            new Point3d(2d, 0d, 0.8d),
        });
    }

    private static Brep ZoneBrep()
    {
        return new Box(
            Plane.WorldXY,
            new Interval(0d, 8d),
            new Interval(0d, 6d),
            new Interval(0d, 3d)).ToBrep();
    }

    private static FenestrationConstruction TransparentConstruction()
    {
        return new FenestrationConstruction(
            "Triple Glazing",
            1.1d,
            0.42d,
            new EntityId("WINDOW-CONSTRUCTION-AUTHORING"));
    }

    private static SurfaceConstruction OpaqueConstruction()
    {
        var material = new Material(
            "Authoring Insulation",
            0.035d,
            32d,
            1400d,
            new EntityId("MATERIAL-AUTHORING"));
        return new SurfaceConstruction(
            "Authoring Envelope",
            new[] { new SurfaceConstructionLayer(material, 0.18d) },
            new EntityId("SURFACE-CONSTRUCTION-AUTHORING"));
    }

    private static UsageProfile Profile()
    {
        var operation = ((UsageDay[])Enum.GetValues(typeof(UsageDay)))
            .ToDictionary(day => day, _ => true);
        return new UsageProfile(
            "Authoring Profile",
            8,
            18,
            7,
            19,
            4d,
            0.2d,
            10d,
            0.1d,
            8d,
            20d,
            26d,
            operation,
            source: UsageProfileSource.Custom,
            id: new EntityId("PROFILE-AUTHORING"));
    }

    private static SupplySystem Supply(string id = "SUPPLY-AUTHORING")
    {
        return new SupplySystem(
            "Packaged AC",
            SupplySystemType.PackagedAirConditioner,
            coolingCop: 3.8d,
            coolingCapacity: 12_000d,
            id: new EntityId(id));
    }

    private static VentilationSystem Ventilator()
    {
        return new VentilationSystem(
            "Authoring ERV",
            0.35d,
            0.82d,
            0.61d,
            new EntityId("ERV-AUTHORING"));
    }

    private static TGoo ArchiveRoundTrip<TGoo>(TGoo source, TGoo target)
        where TGoo : GH_IO.GH_ISerializable
    {
        var writeArchive = new GH_Archive();
        Assert.True(writeArchive.AppendObject(source, "Value"));
        byte[] bytes = writeArchive.Serialize_Binary();
        var readArchive = new GH_Archive();
        Assert.True(readArchive.Deserialize_Binary(bytes));
        Assert.True(readArchive.ExtractObject(target, "Value"));
        return target;
    }

    private static void RunWithNativeGeometry(Action assertion)
    {
        try
        {
            assertion();
        }
        catch (DllNotFoundException exception)
        {
            // Geometry-backed assertions execute when the test process is hosted by
            // Rhino. The repository's managed xUnit pass intentionally has no Rhino
            // native dependency; dedicated Rhino.Inside smoke tests cover that host.
            Assert.Contains("rhcommon_c", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
