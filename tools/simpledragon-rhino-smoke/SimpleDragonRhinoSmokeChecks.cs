using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.SimpleDragon.Rhino;
using Rhino;
using Rhino.Geometry;
using RhinoApp = global::Rhino.RhinoApp;
using RhinoCore = global::Rhino.Runtime.InProcess.RhinoCore;

namespace GonieGonie.SimpleDragon.RhinoSmoke;

internal static class SimpleDragonRhinoSmokeChecks
{
    internal static int Run(string[] coreArguments)
    {
        using var core = new RhinoCore(coreArguments);
        Check(RhinoApp.Version.Major == 8, "Rhino 8 runtime was not loaded.");
        int checks = 1;

        var context = new RhinoGeometryContext(UnitSystem.Meters, 1e-6);
        UsageProfile profile = Profile();
        var material = new Material("Smoke concrete", 1.4, 2200, 880);
        var construction = new SurfaceConstruction(
            "Smoke envelope",
            new[] { new SurfaceConstructionLayer(material, 0.2) });
        var glazing = new FenestrationConstruction("Smoke window", 1.5, 0.5);
        var doorConstruction = new FenestrationConstruction("Smoke opaque door", 2.5);
        var options = new RhinoZoneExtractionOptions();

        using Brep firstBox = Box(0, 0, 0, 4, 3, 2);
        int windowHost = FindWallFace(firstBox, new Vector3d(0, -1, 0));
        using var windowCurve = new PolylineCurve(Closed(
            new Point3d(1, 0, 0.5),
            new Point3d(2, 0, 0.5),
            new Point3d(2, 0, 1.5),
            new Point3d(1, 0, 1.5)));
        var opening = new RhinoFenestrationSource(
            windowCurve,
            windowHost,
            "South window",
            FenestrationType.Window,
            glazing);
        var firstSource = new RhinoZoneSource(
            firstBox,
            "First zone",
            1,
            profile,
            zoneId: new EntityId("ZONE-SMOKE-1"),
            fenestrations: new[] { opening },
            surfaceConstruction: construction);
        RhinoZoneExtractionResult single = RhinoZoneExtractor.Extract(
            new[] { firstSource },
            context,
            options);
        Check(single.Success, Describe(single.Diagnostics));
        Check(single.Zones.Count == 1 && single.Floors.Count == 1, "A single box did not create one zone and floor.");
        Check(single.Zones[0].Surfaces.Count == 6, "A box did not create six SimpleDragon surfaces.");
        Check(AlmostEqual(single.Zones[0].Area, 12) && AlmostEqual(single.Zones[0].Height, 2),
            "Box floor area or height changed during extraction.");
        Check(single.Zones[0].Surfaces.Count(surface => surface.Type == SurfaceType.Wall) == 4,
            "Box wall classification changed.");
        Check(single.Zones[0].Surfaces.Count(surface => surface.Type == SurfaceType.Floor) == 1
              && single.Zones[0].Surfaces.Count(surface => surface.Type == SurfaceType.Ceiling) == 1,
            "Box floor/ceiling classification changed.");
        Check(single.Zones[0].Surfaces.SelectMany(surface => surface.Fenestrations).Single().Area == 1,
            "Separate opening area changed during extraction.");
        Check(single.GeometryMap.Count == 8, "Zone, face, and opening provenance entries are incomplete.");
        checks += 7;

        using Brep flippedBox = Box(0, 0, 0, 4, 3, 2);
        flippedBox.Flip();
        Check(flippedBox.SolidOrientation == BrepSolidOrientation.Inward,
            "The flipped-solid regression fixture did not become inward-oriented.");
        RhinoZoneExtractionResult flipped = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    flippedBox,
                    "Flipped zone",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-FLIPPED"),
                    surfaceConstruction: construction),
            },
            context,
            options);
        Check(flipped.Success, Describe(flipped.Diagnostics));
        Check(flipped.Zones[0].Surfaces.Single(surface => surface.Type == SurfaceType.Floor)
                .BoundaryCondition == SurfaceBoundaryCondition.Ground
              && flipped.Zones[0].Surfaces.Single(surface => surface.Type == SurfaceType.Ceiling)
                .BoundaryCondition == SurfaceBoundaryCondition.Outdoors,
            "An inward solid reversed the extracted floor and ceiling boundaries.");
        Check(AzimuthsEqual(flipped.Zones[0], single.Zones[0]),
            "An inward solid reversed one or more exterior-wall azimuths.");
        checks += 4;

        var millimetreContext = new RhinoGeometryContext(UnitSystem.Millimeters, 0.001);
        using Brep millimetreBox = Box(0, 0, 0, 4000, 3000, 2000);
        RhinoZoneExtractionResult millimetres = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    millimetreBox,
                    "Millimetre zone",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-MM"),
                    surfaceConstruction: construction),
            },
            millimetreContext,
            options);
        Check(millimetres.Success, Describe(millimetres.Diagnostics));
        Check(AlmostEqual(millimetres.Zones[0].Area, 12d)
              && AlmostEqual(millimetres.Zones[0].Height, 2d),
            "Millimetre zone area or height was not normalized to SI units.");
        Check(AreasEqual(millimetres.Zones[0], single.Zones[0]),
            "Millimetre surface areas differ from the equivalent metre geometry.");
        checks += 3;

        using Brep innerLoopFace = PlanarFaceWithHole();
        int innerLoopHost = innerLoopFace.Faces[0].FaceIndex;
        int sourceBrepLoopIndex = innerLoopFace.Faces[0].Loops
            .Single(loop => loop.LoopType == BrepLoopType.Inner)
            .LoopIndex;
        Guid annotationRhinoId = new("aa94ddbf-3aad-4b54-8ce8-469fd9c3f312");
        using var innerLoopAnnotationCurve = new PolylineCurve(Closed(
            new Point3d(1, 0, 1),
            new Point3d(2, 0, 1),
            new Point3d(2, 0, 2),
            new Point3d(1, 0, 2)));
        var innerLoopAnnotation = new RhinoFenestrationSource(
            innerLoopAnnotationCurve,
            innerLoopHost,
            "Annotated inner-loop door",
            FenestrationType.Door,
            doorConstruction,
            id: new EntityId("FNST-SMOKE-INNER-OVERRIDE"),
            rhinoObjectId: annotationRhinoId,
            grasshopperPath: "{2;3}",
            grasshopperIndex: 7);
        var openFaceOptions = new RhinoZoneExtractionOptions
        {
            RequireClosedBreps = false,
        };
        RhinoZoneExtractionResult unannotatedInnerLoop = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    innerLoopFace,
                    "Unannotated inner-loop zone",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-INNER-UNANNOTATED"),
                    surfaceConstruction: construction),
            },
            context,
            openFaceOptions);
        Check(!unannotatedInnerLoop.Success
              && unannotatedInnerLoop.Diagnostics.Any(item =>
                  item.Code == "SD.RHINO.OPENING_METADATA_REQUIRED"),
            "An unannotated Brep inner loop did not require an explicit opening definition.");
        checks++;

        RhinoZoneExtractionResult innerLoop = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    innerLoopFace,
                    "Inner-loop zone",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-INNER"),
                    fenestrations: new[] { innerLoopAnnotation },
                    surfaceConstruction: construction),
            },
            context,
            openFaceOptions);
        Check(innerLoop.Success, Describe(innerLoop.Diagnostics));
        Fenestration annotated = innerLoop.Zones[0].Surfaces
            .SelectMany(surface => surface.Fenestrations)
            .Single();
        Check(annotated.Id == innerLoopAnnotation.Id
              && annotated.Name == innerLoopAnnotation.Name
              && annotated.Type == FenestrationType.Door
              && ReferenceEquals(annotated.Construction, doorConstruction),
            "An explicit source did not override matching Brep inner-loop metadata.");
        RhinoDomainGeometryMapEntry annotatedMap = innerLoop.GeometryMap.Single(entry =>
            entry.Kind == RhinoMappedGeometryKind.Fenestration);
        Check(annotatedMap.BrepLoopIndex == sourceBrepLoopIndex
              && annotatedMap.FenestrationSourceIndex == 0
              && annotatedMap.Provenance.RhinoObjectId == annotationRhinoId
              && annotatedMap.Provenance.GrasshopperPath == "{2;3}"
              && annotatedMap.Provenance.GrasshopperIndex == 7,
            "An inner-loop annotation did not retain both geometry-source indices and provenance.");
        Check(innerLoop.Diagnostics.Any(item => item.Code == "SD.RHINO.OPENING_INNER_LOOP_ANNOTATED")
              && innerLoop.Diagnostics.All(item => item.Code != "SD.RHINO.OPENING_DUPLICATE_IGNORED"),
            "The inner-loop metadata override diagnostic is missing or still reports an ignored duplicate.");
        checks += 4;

        using Brep slopedFace = SlopedPlanarFace(5d);
        RhinoZoneExtractionResult sloped = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    slopedFace,
                    "Sloped face zone",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-SLOPED"),
                    surfaceConstruction: construction),
            },
            context,
            openFaceOptions);
        Check(sloped.Success
              && sloped.Diagnostics.Any(item => item.Code == "SD.RHINO.SLOPED_SURFACE_ABSTRACTED"),
            "A five-degree horizontal tilt was not reported against the one-degree angle tolerance.");
        checks++;

        using Brep secondBox = Box(4, 0, 0, 8, 3, 2);
        var secondSource = new RhinoZoneSource(
            secondBox,
            "Second zone",
            1,
            profile,
            zoneId: new EntityId("ZONE-SMOKE-2"),
            surfaceConstruction: construction);
        RhinoZoneExtractionResult adjacent = RhinoZoneExtractor.Extract(
            new[] { firstSource, secondSource },
            context,
            options);
        Check(adjacent.Success, Describe(adjacent.Diagnostics));
        Check(adjacent.Zones.Sum(zone => zone.Surfaces.Count(surface =>
                  surface.BoundaryCondition == SurfaceBoundaryCondition.Zone)) == 2,
            "Coincident box faces did not create one reciprocal zone pair.");
        Check(adjacent.Zones.SelectMany(zone => zone.Surfaces)
                .Where(surface => surface.BoundaryCondition == SurfaceBoundaryCondition.Zone)
                .All(surface => surface.AdjacentZoneId is not null),
            "An adjacent SimpleDragon surface lost its zone identifier.");
        checks += 3;

        using Brep duplicateFirst = Box(0, 0, 0, 4, 3, 2);
        using Brep duplicateSecond = Box(0, 0, 0, 4, 3, 2);
        RhinoZoneExtractionResult duplicates = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    duplicateFirst,
                    "Duplicate first",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-DUPLICATE-1"),
                    surfaceConstruction: construction),
                new RhinoZoneSource(
                    duplicateSecond,
                    "Duplicate second",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-DUPLICATE-2"),
                    surfaceConstruction: construction),
            },
            context,
            options);
        Check(!duplicates.Success
              && duplicates.Diagnostics.Any(item => item.Code == "SD.RHINO.ADJACENCY_NORMALS_NOT_OPPOSED"),
            "Coincident overlapping volumes were not rejected by their same-direction normals.");
        Check(duplicates.Zones.SelectMany(zone => zone.Surfaces)
            .All(surface => surface.BoundaryCondition != SurfaceBoundaryCondition.Zone),
            "Same-direction duplicate faces were incorrectly assigned as adjacent zones.");
        checks += 2;

        using Brep mismatchFirst = Box(0, 0, 0, 4, 3, 2);
        using Brep mismatchSecond = Box(4, 0, 0, 8, 3, 2);
        int mismatchHost = FindWallFace(mismatchFirst, new Vector3d(1, 0, 0));
        using var mismatchOpeningCurve = new PolylineCurve(Closed(
            new Point3d(4, 0.75, 0.5),
            new Point3d(4, 1.75, 0.5),
            new Point3d(4, 1.75, 1.5),
            new Point3d(4, 0.75, 1.5)));
        var mismatchOpening = new RhinoFenestrationSource(
            mismatchOpeningCurve,
            mismatchHost,
            "Unpaired interior opening",
            FenestrationType.Window,
            glazing);
        RhinoZoneExtractionResult mismatchedOpenings = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    mismatchFirst,
                    "Mismatch first",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-MISMATCH-1"),
                    fenestrations: new[] { mismatchOpening },
                    surfaceConstruction: construction),
                new RhinoZoneSource(
                    mismatchSecond,
                    "Mismatch second",
                    1,
                    profile,
                    zoneId: new EntityId("ZONE-SMOKE-MISMATCH-2"),
                    surfaceConstruction: construction),
            },
            context,
            options);
        Check(!mismatchedOpenings.Success
              && mismatchedOpenings.Diagnostics.Any(item =>
                  item.Code == "SD.RHINO.ADJACENCY_OPENINGS_MISMATCH"),
            "Coincident faces with different opening topology did not report an adjacency error.");
        Check(mismatchedOpenings.Zones.SelectMany(zone => zone.Surfaces)
            .All(surface => surface.BoundaryCondition != SurfaceBoundaryCondition.Zone),
            "Faces with different opening topology were incorrectly assigned as adjacent zones.");
        checks += 2;

        var model = new GreenRetrofitModel(
            "Rhino smoke model",
            0,
            "서울특별시 종로구",
            new DateTime(2020, 1, 1),
            false,
            single.Floors,
            new[] { material },
            new[] { construction },
            new[] { glazing });
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
        Check(conversion.Success, Describe(conversion.Diagnostics));
        Check(conversion.RequireEnergyModel().Zones.Single().Profile.Id == profile.Id,
            "The converted profile did not retain its stable SimpleDragon entity ID.");
        using RhinoConversionPreview preview = RhinoConversionPreviewBuilder.Create(
            model,
            conversion,
            context,
            single.GeometryMap);
        Check(preview.Success, Describe(preview.Diagnostics));
        Check(preview.Surfaces.Count == 6
              && preview.Surfaces.All(item => item.Geometry.IsValid && item.ExplodedGeometry.IsValid),
            "Converted InvisibleDragon surfaces did not return as six valid exact and exploded Breps.");
        Check(preview.Surfaces.All(item => AlmostEqual(item.ConvertedNetArea, item.RhinoNetArea, 1e-8)),
            "A converted preview Brep changed net area.");
        Check(preview.Surfaces.All(item => !string.IsNullOrWhiteSpace(item.GeometryFingerprint)),
            "A converted preview surface lost its fingerprint.");
        Check(preview.Surfaces.All(item =>
                item.GrossAreaDifference.HasValue
                && item.OpeningAreaDifference.HasValue
                && AlmostEqual(item.GrossAreaDifference.Value, 0d, 1e-8)
                && AlmostEqual(item.OpeningAreaDifference.Value, 0d, 1e-8)),
            "Source-to-converted gross or opening area comparison changed.");
        Check(preview.Surfaces.All(item =>
                item.SourceGeometry is not null
                && !string.IsNullOrWhiteSpace(item.SourceGeometryFingerprint)),
            "A converted preview surface lost its Rhino source provenance.");
        Check(preview.Surfaces.All(LabelIsOnTrimmedFace),
            "A converted surface label anchor lies outside its trimmed Rhino face.");
        checks += 9;

        Console.WriteLine(
            $"SimpleDragon Rhino smoke checks passed: {checks} checks on Rhino {RhinoApp.Version}.");
        return 0;
    }

    private static Brep Box(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        return new Box(new BoundingBox(x0, y0, z0, x1, y1, z1)).ToBrep();
    }

    private static Brep PlanarFaceWithHole()
    {
        using var outer = new PolylineCurve(Closed(
            new Point3d(0, 0, 0),
            new Point3d(4, 0, 0),
            new Point3d(4, 0, 3),
            new Point3d(0, 0, 3)));
        using var inner = new PolylineCurve(Closed(
            new Point3d(1, 0, 1),
            new Point3d(2, 0, 1),
            new Point3d(2, 0, 2),
            new Point3d(1, 0, 2)));
        return CreateSinglePlanarBrep(new Curve[] { outer, inner });
    }

    private static Brep SlopedPlanarFace(double degrees)
    {
        double rise = 3d * Math.Tan(degrees * Math.PI / 180d);
        using var boundary = new PolylineCurve(Closed(
            new Point3d(0, 0, 0),
            new Point3d(4, 0, 0),
            new Point3d(4, 3, rise),
            new Point3d(0, 3, rise)));
        return CreateSinglePlanarBrep(new Curve[] { boundary });
    }

    private static Brep CreateSinglePlanarBrep(IEnumerable<Curve> boundaries)
    {
        Brep[] created = Brep.CreatePlanarBreps(boundaries, 1e-6);
        if (created.Length == 1)
        {
            return created[0];
        }

        foreach (Brep brep in created)
        {
            brep.Dispose();
        }

        throw new InvalidOperationException(
            $"Expected one planar Brep smoke fixture, but Rhino created {created.Length}.");
    }

    private static bool AzimuthsEqual(
        GonieGonie.SimpleDragon.Zone first,
        GonieGonie.SimpleDragon.Zone second)
    {
        double[] firstValues = first.Surfaces
            .Where(surface => surface.Type == SurfaceType.Wall
                && surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors)
            .Select(surface => surface.Azimuth!.Value)
            .OrderBy(value => value)
            .ToArray();
        double[] secondValues = second.Surfaces
            .Where(surface => surface.Type == SurfaceType.Wall
                && surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors)
            .Select(surface => surface.Azimuth!.Value)
            .OrderBy(value => value)
            .ToArray();
        return firstValues.Length == secondValues.Length
            && firstValues.Zip(secondValues, (left, right) => AlmostEqual(left, right)).All(value => value);
    }

    private static bool AreasEqual(
        GonieGonie.SimpleDragon.Zone first,
        GonieGonie.SimpleDragon.Zone second)
    {
        double[] firstValues = first.Surfaces.Select(surface => surface.Area).OrderBy(value => value).ToArray();
        double[] secondValues = second.Surfaces.Select(surface => surface.Area).OrderBy(value => value).ToArray();
        return firstValues.Length == secondValues.Length
            && firstValues.Zip(secondValues, (left, right) => AlmostEqual(left, right)).All(value => value);
    }

    private static bool LabelIsOnTrimmedFace(RhinoConvertedSurfacePreview preview)
    {
        if (preview.Geometry.Faces.Count != 1)
        {
            return false;
        }

        BrepFace face = preview.Geometry.Faces[0];
        return face.ClosestPoint(preview.LabelPoint, out double u, out double v)
            && face.IsPointOnFace(u, v) != PointFaceRelation.Exterior
            && face.PointAt(u, v).DistanceTo(preview.LabelPoint) <= 1e-8;
    }

    private static int FindWallFace(Brep brep, Vector3d targetNormal)
    {
        foreach (BrepFace face in brep.Faces)
        {
            Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
            if (face.OrientationIsReversed)
            {
                normal.Reverse();
            }

            normal.Unitize();
            if (normal * targetNormal > 0.999999)
            {
                return face.FaceIndex;
            }
        }

        throw new InvalidOperationException("The requested box wall was not found.");
    }

    private static UsageProfile Profile()
    {
        var operation = Enum.GetValues<UsageDay>().ToDictionary(day => day, _ => true);
        return new UsageProfile(
            "Smoke profile",
            8,
            18,
            7,
            19,
            4,
            0,
            10,
            0.1,
            5,
            20,
            26,
            operation);
    }

    private static Polyline Closed(params Point3d[] points)
    {
        var polyline = new Polyline(points);
        polyline.Add(points[0]);
        return polyline;
    }

    private static bool AlmostEqual(double first, double second, double tolerance = 1e-10)
    {
        return Math.Abs(first - second) <= tolerance;
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine, diagnostics.Select(item => item.Code + ": " + item.Message));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
