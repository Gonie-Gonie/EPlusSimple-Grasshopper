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
        var otherGlazing = new FenestrationConstruction("Other smoke window", 1.8, 0.4);
        var doorConstruction = new FenestrationConstruction("Smoke opaque door", 2.5);
        using var owned = new OwnedBreps();

        using Brep firstBox = Box(0, 0, 0, 4, 3, 2);
        using var windowCurve = new PolylineCurve(Closed(
            new Point3d(1, 0, 0.5),
            new Point3d(2, 0, 0.5),
            new Point3d(2, 0, 1.5),
            new Point3d(1, 0, 1.5)));
        var window = new RhinoFenestrationSource(
            windowCurve,
            "South window",
            FenestrationType.Window,
            glazing,
            id: new EntityId("FNST-SMOKE-SOUTH"));
        IReadOnlyList<RhinoSurfaceSource> firstSurfaces = BoxSurfaces(
            firstBox,
            "First",
            construction,
            context,
            owned,
            openings: normal => normal * new Vector3d(0, -1, 0) > 0.999999
                ? new[] { window }
                : null);
        var firstSource = new RhinoZoneSource(
            "First zone",
            1,
            2,
            profile,
            firstSurfaces,
            lightDensity: 10,
            zoneId: new EntityId("ZONE-SMOKE-1"));
        RhinoZoneExtractionResult single = RhinoZoneExtractor.Extract(new[] { firstSource }, context);
        Check(single.Success, Describe(single.Diagnostics));
        Check(single.Zones.Count == 1 && single.Floors.Count == 1, "A surface collection did not create one zone and floor.");
        Check(single.Zones[0].Surfaces.Count == 6, "Six authored box surfaces did not survive extraction.");
        Check(AlmostEqual(single.Zones[0].Area, 12) && AlmostEqual(single.Zones[0].Height, 2),
            "Explicit box floor area or zone height changed during extraction.");
        Check(single.Zones[0].Surfaces.Count(surface => surface.Type == SurfaceType.Wall) == 4
              && single.Zones[0].Surfaces.Count(surface => surface.Type == SurfaceType.Floor) == 1
              && single.Zones[0].Surfaces.Count(surface => surface.Type == SurfaceType.Ceiling) == 1,
            "Explicit box surface types changed.");
        Check(AlmostEqual(single.Zones[0].Surfaces.SelectMany(surface => surface.Fenestrations).Single().Area, 1),
            "Surface-owned opening area changed.");
        Check(single.GeometryMap.Count == 8
              && single.GeometryMap.Count(entry => entry.Kind == RhinoMappedGeometryKind.Zone) == 1
              && single.GeometryMap.Count(entry => entry.Kind == RhinoMappedGeometryKind.Surface) == 6,
            "Zone, surface, and opening provenance entries are incomplete.");
        RhinoDomainGeometryMapEntry windowMap = single.GeometryMap.Single(entry =>
            entry.Kind == RhinoMappedGeometryKind.Fenestration);
        Check(windowMap.ZoneIndex == 0
              && windowMap.SurfaceIndex.HasValue
              && windowMap.OpeningIndex == 0
              && !windowMap.TrimLoopIndex.HasValue,
            "Explicit opening provenance must use Zone/Surface/Opening indices and no trim-loop index.");
        checks += 8;

        var millimetreContext = new RhinoGeometryContext(UnitSystem.Millimeters, 0.001);
        using Brep millimetreBox = Box(0, 0, 0, 4000, 3000, 2000);
        IReadOnlyList<RhinoSurfaceSource> millimetreSurfaces = BoxSurfaces(
            millimetreBox,
            "Millimetre",
            construction,
            millimetreContext,
            owned);
        RhinoZoneExtractionResult millimetres = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource(
                    "Millimetre zone",
                    1,
                    2,
                    profile,
                    millimetreSurfaces,
                    zoneId: new EntityId("ZONE-SMOKE-MM")),
            },
            millimetreContext);
        Check(millimetres.Success, Describe(millimetres.Diagnostics));
        Check(AlmostEqual(millimetres.Zones[0].Area, 12)
              && AlmostEqual(millimetres.Zones[0].Height, 2),
            "Millimetre surface geometry or explicit metre height was not normalized correctly.");
        Check(AreasEqual(millimetres.Zones[0], single.Zones[0]),
            "Millimetre surface areas differ from equivalent metre geometry.");
        checks += 3;

        using Brep innerLoopFace = PlanarFaceWithHole();
        int sourceTrimLoopIndex = innerLoopFace.Faces[0].Loops
            .Single(loop => loop.LoopType == BrepLoopType.Inner)
            .LoopIndex;
        var unannotatedSurface = new RhinoSurfaceSource(
            innerLoopFace,
            "Unannotated wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            construction);
        RhinoZoneExtractionResult unannotated = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource("Unannotated", 1, 3, profile, new[] { unannotatedSurface }),
            },
            context);
        Check(!unannotated.Success
              && unannotated.Diagnostics.Any(item => item.Code == "SD.RHINO.OPENING_METADATA_REQUIRED"),
            "An unannotated inner trim loop was accepted.");

        Guid annotationRhinoId = new("aa94ddbf-3aad-4b54-8ce8-469fd9c3f312");
        using var trimAnnotationCurve = new PolylineCurve(Closed(
            new Point3d(1, 0, 1),
            new Point3d(2, 0, 1),
            new Point3d(2, 0, 2),
            new Point3d(1, 0, 2)));
        var trimAnnotation = new RhinoFenestrationSource(
            trimAnnotationCurve,
            "Annotated trim door",
            FenestrationType.Door,
            doorConstruction,
            id: new EntityId("FNST-SMOKE-TRIM"),
            rhinoObjectId: annotationRhinoId,
            grasshopperPath: "{2;3}",
            grasshopperIndex: 7);
        var annotatedSurface = new RhinoSurfaceSource(
            innerLoopFace,
            "Annotated wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            construction,
            new[] { trimAnnotation });
        RhinoZoneExtractionResult annotated = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource("Annotated", 1, 3, profile, new[] { annotatedSurface }),
            },
            context);
        Check(annotated.Success, Describe(annotated.Diagnostics));
        RhinoDomainGeometryMapEntry annotatedMap = annotated.GeometryMap.Single(entry =>
            entry.Kind == RhinoMappedGeometryKind.Fenestration);
        Check(annotatedMap.ZoneIndex == 0
              && annotatedMap.SurfaceIndex == 0
              && annotatedMap.OpeningIndex == 0
              && annotatedMap.TrimLoopIndex == sourceTrimLoopIndex
              && annotatedMap.Provenance.RhinoObjectId == annotationRhinoId
              && !annotatedMap.Provenance.BrepFaceIndex.HasValue
              && annotatedMap.Provenance.GrasshopperPath == "{2;3}"
              && annotatedMap.Provenance.GrasshopperIndex == 7,
            "Inner-loop annotation did not retain the new source indices and provenance.");
        checks += 3;

        using var offPlaneCurve = new PolylineCurve(Closed(
            new Point3d(1, 0.1, 1),
            new Point3d(2, 0.1, 1),
            new Point3d(2, 0.1, 2),
            new Point3d(1, 0.1, 2)));
        var offPlaneOpening = new RhinoFenestrationSource(
            offPlaneCurve,
            "Off-plane door",
            FenestrationType.Door,
            doorConstruction);
        var offPlaneSurface = new RhinoSurfaceSource(
            innerLoopFace,
            "Off-plane wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            construction,
            new[] { offPlaneOpening });
        RhinoZoneExtractionResult offPlane = RhinoZoneExtractor.Extract(
            new[] { new RhinoZoneSource("Off plane", 1, 3, profile, new[] { offPlaneSurface }) },
            context);
        Check(!offPlane.Success
              && offPlane.Diagnostics.Any(item => item.Code == "SD.RHINO.OPENING_NOT_COPLANAR"),
            "A distant parallel opening curve was projected onto its surface implicitly.");
        checks++;

        var multiFaceSurface = new RhinoSurfaceSource(
            firstBox,
            "Invalid volume surface",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            construction);
        RhinoZoneExtractionResult multiFace = RhinoZoneExtractor.Extract(
            new[] { new RhinoZoneSource("Invalid volume", 1, 2, profile, new[] { multiFaceSurface }) },
            context);
        Check(!multiFace.Success
              && multiFace.Diagnostics.Any(item => item.Code == "SD.RHINO.SURFACE_REQUIRES_ONE_FACE"),
            "A multi-face zone Brep was accepted as an SD Surface.");
        checks++;

        using Brep adjacentFirstBox = Box(0, 0, 0, 4, 3, 2);
        using Brep adjacentSecondBox = Box(4, 0, 0, 8, 3, 2);
        IReadOnlyList<RhinoSurfaceSource> adjacentFirstSurfaces = BoxSurfaces(
            adjacentFirstBox,
            "Adjacent first",
            construction,
            context,
            owned);
        IReadOnlyList<RhinoSurfaceSource> adjacentSecondSurfaces = BoxSurfaces(
            adjacentSecondBox,
            "Adjacent second",
            construction,
            context,
            owned);
        RhinoZoneExtractionResult adjacent = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource("Adjacent first", 1, 2, profile, adjacentFirstSurfaces,
                    zoneId: new EntityId("ZONE-SMOKE-ADJ-1")),
                new RhinoZoneSource("Adjacent second", 1, 2, profile, adjacentSecondSurfaces,
                    zoneId: new EntityId("ZONE-SMOKE-ADJ-2")),
            },
            context);
        Check(adjacent.Success, Describe(adjacent.Diagnostics));
        Check(adjacent.Zones.Sum(zone => zone.Surfaces.Count(surface =>
                  surface.BoundaryCondition == SurfaceBoundaryCondition.Zone)) == 2,
            "Coincident opposite-normal outdoor surfaces did not create reciprocal Zone boundaries.");
        Check(adjacent.Zones.SelectMany(zone => zone.Surfaces)
            .Where(surface => surface.BoundaryCondition == SurfaceBoundaryCondition.Zone)
            .All(surface => surface.AdjacentZoneId is not null),
            "A reciprocal zone surface lost its adjacent zone ID.");
        checks += 3;

        using var interiorOpeningCurve = new PolylineCurve(Closed(
            new Point3d(4, 0.75, 0.5),
            new Point3d(4, 1.75, 0.5),
            new Point3d(4, 1.75, 1.5),
            new Point3d(4, 0.75, 1.5)));
        var interiorOpening = new RhinoFenestrationSource(
            interiorOpeningCurve,
            "Shared window",
            FenestrationType.Window,
            glazing,
            id: new EntityId("FNST-SMOKE-SHARED"));
        using Brep mirroredFirstBox = Box(0, 0, 0, 4, 3, 2);
        using Brep mirroredSecondBox = Box(4, 0, 0, 8, 3, 2);
        IReadOnlyList<RhinoSurfaceSource> mirroredFirstSurfaces = BoxSurfaces(
            mirroredFirstBox,
            "Mirrored first",
            construction,
            context,
            owned,
            openings: normal => normal * Vector3d.XAxis > 0.999999
                ? new[] { interiorOpening }
                : null);
        IReadOnlyList<RhinoSurfaceSource> mirroredSecondSurfaces = BoxSurfaces(
            mirroredSecondBox,
            "Mirrored second",
            construction,
            context,
            owned);
        RhinoZoneExtractionResult mirrored = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource("Mirrored first", 1, 2, profile, mirroredFirstSurfaces),
                new RhinoZoneSource("Mirrored second", 1, 2, profile, mirroredSecondSurfaces),
            },
            context);
        Check(mirrored.Success, Describe(mirrored.Diagnostics));
        Check(mirrored.Zones.SelectMany(zone => zone.Surfaces)
                .Where(surface => surface.BoundaryCondition == SurfaceBoundaryCondition.Zone)
                .All(surface => surface.Fenestrations.Count == 1)
              && mirrored.Diagnostics.Any(item => item.Code == "SD.RHINO.ADJACENCY_OPENINGS_MIRRORED"),
            "A one-sided inter-zone opening was not mirrored to the reciprocal surface.");
        Check(mirrored.GeometryMap.Count(entry => entry.Kind == RhinoMappedGeometryKind.Fenestration) == 2
              && mirrored.GeometryMap.Where(entry => entry.Kind == RhinoMappedGeometryKind.Fenestration)
                  .All(entry => entry.OpeningIndex == 0),
            "Mirrored opening provenance lost its explicit opening index.");
        var mirroredModel = new GreenRetrofitModel(
            "Mirrored Rhino smoke model",
            0,
            "서울특별시 종로구",
            new DateTime(2020, 1, 1),
            false,
            mirrored.Floors,
            new[] { material },
            new[] { construction },
            new[] { glazing });
        GreenRetrofitConversionResult mirroredConversion = GreenRetrofitConverter.Convert(mirroredModel);
        Check(mirroredConversion.Success, Describe(mirroredConversion.Diagnostics));
        checks += 4;

        using Brep mismatchFirstBox = Box(0, 0, 0, 4, 3, 2);
        using Brep mismatchSecondBox = Box(4, 0, 0, 8, 3, 2);
        var conflictingOpening = new RhinoFenestrationSource(
            interiorOpeningCurve,
            "Different shared window",
            FenestrationType.Window,
            otherGlazing);
        IReadOnlyList<RhinoSurfaceSource> mismatchFirstSurfaces = BoxSurfaces(
            mismatchFirstBox,
            "Mismatch first",
            construction,
            context,
            owned,
            openings: normal => normal * Vector3d.XAxis > 0.999999
                ? new[] { interiorOpening }
                : null);
        IReadOnlyList<RhinoSurfaceSource> mismatchSecondSurfaces = BoxSurfaces(
            mismatchSecondBox,
            "Mismatch second",
            construction,
            context,
            owned,
            openings: normal => normal * -Vector3d.XAxis > 0.999999
                ? new[] { conflictingOpening }
                : null);
        RhinoZoneExtractionResult mismatch = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource("Mismatch first", 1, 2, profile, mismatchFirstSurfaces),
                new RhinoZoneSource("Mismatch second", 1, 2, profile, mismatchSecondSurfaces),
            },
            context);
        Check(!mismatch.Success
              && mismatch.Diagnostics.Any(item =>
                  item.Code == "SD.RHINO.ADJACENCY_OPENING_METADATA_CONFLICT"),
            "Two-sided inter-zone openings with different semantics were accepted.");
        checks++;

        using Brep conflictFirstBox = Box(0, 0, 0, 4, 3, 2);
        using Brep conflictSecondBox = Box(4, 0, 0, 8, 3, 2);
        IReadOnlyList<RhinoSurfaceSource> conflictFirstSurfaces = BoxSurfaces(
            conflictFirstBox,
            "Conflict first",
            construction,
            context,
            owned,
            boundary: normal => normal * Vector3d.XAxis > 0.999999
                ? SurfaceBoundaryCondition.Adiabatic
                : DefaultBoundary(normal));
        IReadOnlyList<RhinoSurfaceSource> conflictSecondSurfaces = BoxSurfaces(
            conflictSecondBox,
            "Conflict second",
            construction,
            context,
            owned);
        RhinoZoneExtractionResult boundaryConflict = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource("Conflict first", 1, 2, profile, conflictFirstSurfaces),
                new RhinoZoneSource("Conflict second", 1, 2, profile, conflictSecondSurfaces),
            },
            context);
        Check(!boundaryConflict.Success
              && boundaryConflict.Diagnostics.Any(item =>
                  item.Code == "SD.RHINO.ADJACENCY_BOUNDARY_CONFLICT"),
            "Coincident Ground/Adiabatic geometry was paired as an inter-zone boundary.");
        checks++;

        using Brep sameNormalFace = adjacentFirstSurfaces
            .Single(surface => SurfaceNormal(surface, context) * Vector3d.XAxis > 0.999999)
            .Geometry.DuplicateBrep();
        using Brep sameNormalDuplicate = sameNormalFace.DuplicateBrep();
        var sameNormalFirst = new RhinoSurfaceSource(
            sameNormalFace,
            "Same normal first",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            construction);
        var sameNormalSecond = new RhinoSurfaceSource(
            sameNormalDuplicate,
            "Same normal second",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            construction);
        RhinoZoneExtractionResult sameNormal = RhinoZoneExtractor.Extract(
            new[]
            {
                new RhinoZoneSource("Same normal first", 1, 2, profile, new[] { sameNormalFirst }),
                new RhinoZoneSource("Same normal second", 1, 2, profile, new[] { sameNormalSecond }),
            },
            context);
        Check(!sameNormal.Success
              && sameNormal.Diagnostics.Any(item => item.Code == "SD.RHINO.ADJACENCY_NORMALS_NOT_OPPOSED"),
            "Same-normal coincident surfaces were paired.");
        checks++;

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
        using RhinoConversionPreview preview = RhinoConversionPreviewBuilder.Create(
            model,
            conversion,
            context,
            single.GeometryMap);
        Check(preview.Success, Describe(preview.Diagnostics));
        Check(preview.Surfaces.Count == 6
              && preview.Surfaces.All(item => item.Geometry.IsValid && item.ExplodedGeometry.IsValid),
            "Converted InvisibleDragon surfaces did not return as valid preview Breps.");
        Check(preview.Surfaces.All(item => AlmostEqual(item.ConvertedNetArea, item.RhinoNetArea, 1e-8)),
            "A converted preview Brep changed net area.");
        Check(preview.Surfaces.All(item =>
                item.SourceGeometry is not null
                && !string.IsNullOrWhiteSpace(item.SourceGeometryFingerprint)),
            "A converted preview surface lost its authored Surface provenance.");
        Check(preview.Surfaces.All(LabelIsOnTrimmedFace),
            "A converted surface label anchor lies outside its trimmed Rhino face.");
        checks += 6;

        Console.WriteLine(
            $"SimpleDragon Rhino smoke checks passed: {checks} checks on Rhino {RhinoApp.Version}.");
        return 0;
    }

    private static List<RhinoSurfaceSource> BoxSurfaces(
        Brep box,
        string prefix,
        SurfaceConstruction construction,
        RhinoGeometryContext context,
        OwnedBreps owned,
        Func<Vector3d, SurfaceBoundaryCondition>? boundary = null,
        Func<Vector3d, IEnumerable<RhinoFenestrationSource>?>? openings = null)
    {
        var result = new List<RhinoSurfaceSource>();
        foreach (BrepFace face in box.Faces)
        {
            Brep geometry = owned.Add(face.DuplicateFace(false));
            Vector3d normal = SurfaceNormal(geometry, context);
            SurfaceType type = TypeFromNormal(normal);
            result.Add(new RhinoSurfaceSource(
                geometry,
                prefix + " surface " + face.FaceIndex,
                type,
                boundary?.Invoke(normal) ?? DefaultBoundary(normal),
                construction,
                openings?.Invoke(normal),
                surfaceId: new EntityId(
                    "SURF-SMOKE-" + Sanitize(prefix) + "-" + face.FaceIndex)));
        }

        return result;
    }

    private static Vector3d SurfaceNormal(RhinoSurfaceSource surface, RhinoGeometryContext context) =>
        SurfaceNormal(surface.Geometry, context);

    private static Vector3d SurfaceNormal(Brep geometry, RhinoGeometryContext context)
    {
        var normal = RhinoPolygonConverter.FromBrepFace(geometry.Faces[0], context).OuterLoop.Normal;
        return new Vector3d(normal.X, normal.Y, normal.Z);
    }

    private static SurfaceType TypeFromNormal(Vector3d normal) => Math.Abs(normal.Z) <= Math.Sqrt(0.5)
        ? SurfaceType.Wall
        : normal.Z > 0
            ? SurfaceType.Ceiling
            : SurfaceType.Floor;

    private static SurfaceBoundaryCondition DefaultBoundary(Vector3d normal) =>
        TypeFromNormal(normal) == SurfaceType.Floor
            ? SurfaceBoundaryCondition.Ground
            : SurfaceBoundaryCondition.Outdoors;

    private static string Sanitize(string value) => value.Replace(' ', '-').ToUpperInvariant();

    private static Brep Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
        new Box(new BoundingBox(x0, y0, z0, x1, y1, z1)).ToBrep();

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
        Brep[] created = Brep.CreatePlanarBreps(new Curve[] { outer, inner }, 1e-6);
        if (created.Length == 1)
        {
            return created[0];
        }

        foreach (Brep item in created)
        {
            item.Dispose();
        }

        throw new InvalidOperationException("Expected one planar Brep with one hole.");
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

    private static bool AlmostEqual(double first, double second, double tolerance = 1e-10) =>
        Math.Abs(first - second) <= tolerance;

    private static string Describe(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item => item.Code + ": " + item.Message));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class OwnedBreps : IDisposable
    {
        private readonly List<Brep> _items = new();

        internal Brep Add(Brep brep)
        {
            _items.Add(brep ?? throw new ArgumentNullException(nameof(brep)));
            return brep;
        }

        public void Dispose()
        {
            foreach (Brep item in _items)
            {
                item.Dispose();
            }
        }
    }
}
