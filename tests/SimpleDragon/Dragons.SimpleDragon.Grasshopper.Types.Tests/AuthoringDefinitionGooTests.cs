using GH_IO.Serialization;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Grasshopper.Parameters;
using Dragons.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Dragons.SimpleDragon.Grasshopper.Tests;

public sealed class AuthoringDefinitionGooTests
{
    [Fact]
    public void OpeningSurfaceAndZoneDefinitionsDefensivelyOwnTheirGeometryAndItems()
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

        SurfaceConstruction surfaceConstruction = OpaqueConstruction();
        using Brep surfaceSource = SurfaceFace();
        var surface = new SurfaceDefinition(
            surfaceSource,
            "South Wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            surfaceConstruction,
            new[] { opening },
            id: new EntityId("SURFACE-AUTHORING"));
        BoundingBox expectedSurfaceBounds = Bounds(surface.Geometry);

        Assert.True(surfaceSource.Transform(Transform.Translation(200d, 0d, 0d)));
        using (Brep firstRead = surface.Geometry)
        {
            Assert.Equal(expectedSurfaceBounds, firstRead.GetBoundingBox(true));
            Assert.True(firstRead.Transform(Transform.Translation(0d, 200d, 0d)));
        }

        using (Brep secondRead = surface.Geometry)
        {
            Assert.Equal(expectedSurfaceBounds, secondRead.GetBoundingBox(true));
        }

        Assert.NotSame(surfaceConstruction, surface.Construction);
        Assert.NotSame(opening, surface.Openings[0]);

        UsageProfile profile = Profile();
        SupplySystem supply = Supply();
        VentilationSystem ventilator = Ventilator();
        var assignment = new VentilationAssignment(ventilator.Id.Value, 2, ventilator);
        var zone = new ZoneDefinition(
            "Ground Floor",
            1,
            3d,
            new[] { surface },
            profile,
            8.5d,
            new[] { supply },
            new[] { assignment },
            new EntityId("ZONE-AUTHORING"));

        Assert.NotSame(profile, zone.Profile);
        Assert.NotSame(surface, zone.Surfaces[0]);
        Assert.NotSame(surface.Construction, zone.Surfaces[0].Construction);
        Assert.NotSame(surface.Openings[0], zone.Surfaces[0].Openings[0]);
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
        SurfaceDefinition surface = Surface(opening);
        ZoneDefinition zone = Zone(opening);

        var openingGoo = new SimpleDragonOpeningDefinitionGoo(opening);
        var openingDuplicate = Assert.IsType<SimpleDragonOpeningDefinitionGoo>(openingGoo.Duplicate());
        SimpleDragonOpeningDefinitionGoo openingArchived = ArchiveRoundTrip(
            openingGoo,
            new SimpleDragonOpeningDefinitionGoo());
        AssertOpeningEquivalent(opening, openingDuplicate.Value);
        AssertOpeningEquivalent(opening, openingArchived.Value);

        var surfaceGoo = new SimpleDragonSurfaceDefinitionGoo(surface);
        var surfaceDuplicate = Assert.IsType<SimpleDragonSurfaceDefinitionGoo>(surfaceGoo.Duplicate());
        SimpleDragonSurfaceDefinitionGoo surfaceArchived = ArchiveRoundTrip(
            surfaceGoo,
            new SimpleDragonSurfaceDefinitionGoo());
        AssertSurfaceEquivalent(surface, surfaceDuplicate.Value);
        AssertSurfaceEquivalent(surface, surfaceArchived.Value);

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
    public void ZoneErvGooPromotesRawErvInputsToOneUnit()
    {
        VentilationSystem ventilator = Ventilator();

        var direct = new SimpleDragonZoneErvGoo();
        Assert.True(direct.CastFrom(ventilator));
        AssertSingleUnitCopy(ventilator, direct.Value);

        var fromWrapper = new SimpleDragonZoneErvGoo();
        Assert.True(fromWrapper.CastFrom(new GH_ObjectWrapper(ventilator)));
        AssertSingleUnitCopy(ventilator, fromWrapper.Value);
    }

    [Fact]
    public void ZoneErvGooPreservesExplicitCountsAndNestedErv()
    {
        VentilationSystem ventilator = Ventilator();
        var assignment = new VentilationAssignment(ventilator.Id.Value, 4, ventilator);
        var goo = new SimpleDragonZoneErvGoo(assignment);

        var duplicate = Assert.IsType<SimpleDragonZoneErvGoo>(goo.Duplicate());
        SimpleDragonZoneErvGoo archived = ArchiveRoundTrip(
            goo,
            new SimpleDragonZoneErvGoo());

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
            FenestrationType.Window,
            TransparentConstruction()));

        using Brep multiFaceBrep = ZoneBrep();
        Assert.Throws<ArgumentException>(() => new ZoneDefinition(
            "Empty Zone",
            1,
            3d,
            Array.Empty<SurfaceDefinition>(),
            Profile()));
        Assert.Throws<ArgumentException>(() => new SurfaceDefinition(
            multiFaceBrep,
            "Multiple Faces",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors));

        FenestrationConstruction opaque = new(
            "Opaque Door",
            2.2d,
            id: new EntityId("OPAQUE-DOOR"));
        using Curve closedCurve = OpeningCurve();
        Assert.Throws<ArgumentNullException>(() => new OpeningDefinition(
            closedCurve,
            "Missing Construction",
            FenestrationType.Window,
            null!));
        Assert.Throws<ArgumentException>(() => new OpeningDefinition(
            closedCurve,
            "Invalid Window",
            FenestrationType.Window,
            opaque));

        using Brep face = SurfaceFace();
        var surface = new SurfaceDefinition(
            face,
            "South Wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            OpaqueConstruction(),
            new[] { new OpeningDefinition(
                closedCurve,
                "Window",
                FenestrationType.Window,
                TransparentConstruction()) },
            id: new EntityId("SURFACE-VALID"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SurfaceDefinition(
            face,
            "Invalid Boundary",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.AdjacentSpace));
        Assert.Throws<ArgumentException>(() => new SurfaceDefinition(
            face,
            "Ground With Opening",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            openings: surface.Openings));
        Assert.Throws<ArgumentException>(() => new SurfaceDefinition(
            face,
            "Wall Reflectance",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            coolRoofReflectance: 0.7d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SurfaceDefinition(
            face,
            "Invalid Reflectance",
            SurfaceType.Ceiling,
            SurfaceBoundaryCondition.Outdoors,
            coolRoofReflectance: 1.1d));

        SupplySystem first = Supply("DUPLICATE-SUPPLY");
        SupplySystem second = Supply("DUPLICATE-SUPPLY");
        Assert.Throws<ArgumentException>(() => new ZoneDefinition(
            "Duplicate HVAC",
            1,
            3d,
            new[] { surface },
            Profile(),
            supplySystems: new[] { first, second }));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneDefinition(
            "Invalid Height",
            1,
            0d,
            new[] { surface },
            Profile(),
            lightDensity: 10d));
        });
    }

    [Fact]
    public void AuthoringGooAndParameterTypesArePubliclyDiscoverable()
    {
        Type[] goos =
        {
            typeof(SimpleDragonOpeningDefinitionGoo),
            typeof(SimpleDragonSurfaceDefinitionGoo),
            typeof(SimpleDragonZoneDefinitionGoo),
            typeof(SimpleDragonZoneErvGoo),
        };
        (Type Type, Guid Guid)[] parameters =
        {
            (typeof(SimpleDragonOpeningDefinitionParam), new Guid("51610fe9-ecf1-43b4-9157-7260b3ba89ad")),
            (typeof(SimpleDragonSurfaceDefinitionParam), new Guid("14feee1f-498c-478c-92ac-4bd0e9d256da")),
            (typeof(SimpleDragonZoneDefinitionParam), new Guid("df2c89ba-56a7-48ea-83f2-ba58ac15f17f")),
            (typeof(SimpleDragonZoneErvParam), new Guid("14f1683e-4b0a-4754-aac5-6b85331c2126")),
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

    private static void AssertSurfaceEquivalent(SurfaceDefinition expected, SurfaceDefinition actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.BoundaryCondition, actual.BoundaryCondition);
        Assert.Equal(expected.CoolRoofReflectance, actual.CoolRoofReflectance);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Construction?.Id, actual.Construction?.Id);
        if (expected.Construction is not null)
        {
            Assert.NotSame(expected.Construction, actual.Construction);
        }

        Assert.Equal(expected.Openings.Count, actual.Openings.Count);
        for (int index = 0; index < expected.Openings.Count; index++)
        {
            AssertOpeningEquivalent(expected.Openings[index], actual.Openings[index]);
        }

        using Brep expectedGeometry = expected.Geometry;
        using Brep actualGeometry = actual.Geometry;
        Assert.Equal(expectedGeometry.GetBoundingBox(true), actualGeometry.GetBoundingBox(true));
        Assert.Equal(expectedGeometry.GetArea(), actualGeometry.GetArea(), 10);
    }

    private static void AssertZoneEquivalent(ZoneDefinition expected, ZoneDefinition actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.FloorNumber, actual.FloorNumber);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Profile.Id, actual.Profile.Id);
        Assert.Equal(expected.LightDensity, actual.LightDensity);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Single(actual.Surfaces);
        Assert.Single(actual.SupplySystems);
        Assert.Single(actual.VentilationAssignments);
        AssertSurfaceEquivalent(expected.Surfaces[0], actual.Surfaces[0]);
        Assert.Equal(expected.SupplySystems[0].Id, actual.SupplySystems[0].Id);
        Assert.NotSame(expected.SupplySystems[0], actual.SupplySystems[0]);
        AssertAssignmentEquivalent(
            expected.VentilationAssignments[0],
            actual.VentilationAssignments[0]);
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
        return new ZoneDefinition(
            "Round-trip Zone",
            2,
            3.25d,
            new[] { Surface(opening) },
            Profile(),
            9.25d,
            new[] { Supply() },
            new[] { new VentilationAssignment(ventilation.Id.Value, 3, ventilation) },
            new EntityId("ZONE-ROUNDTRIP"));
    }

    private static SurfaceDefinition Surface(OpeningDefinition opening)
    {
        using Brep geometry = SurfaceFace();
        return new SurfaceDefinition(
            geometry,
            "South Wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            OpaqueConstruction(),
            new[] { opening },
            id: new EntityId("SURFACE-ROUNDTRIP"));
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

    private static Brep SurfaceFace()
    {
        using Brep zone = ZoneBrep();
        BrepFace face = zone.Faces
            .First(item => item.TryGetPlane(out Plane plane)
                && Math.Abs(plane.Normal.Y) > 0.99d);
        return face.DuplicateFace(false);
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
