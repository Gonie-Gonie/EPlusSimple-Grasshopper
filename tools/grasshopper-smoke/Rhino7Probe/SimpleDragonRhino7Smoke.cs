using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.SimpleDragon;
using GonieGonie.SimpleDragon.Rhino;
using Rhino;
using Rhino.Geometry;

namespace GonieGonie.Dragons.Grasshopper.Rhino7Probe;

internal static class SimpleDragonRhino7Smoke
{
    public static void Run()
    {
        Require(RhinoApp.Version.Major == 7, "SimpleDragon native smoke requires the Rhino 7 host.");

        var context = new RhinoGeometryContext(UnitSystem.Millimeters, 0.01);
        UsageProfile profile = CreateProfile();
        var material = new Material("Rhino 7 smoke concrete", 1.4d, 2200d, 880d);
        var construction = new SurfaceConstruction(
            "Rhino 7 smoke envelope",
            new[] { new SurfaceConstructionLayer(material, 0.2d) });
        var glazing = new FenestrationConstruction("Rhino 7 smoke glazing", 1.5d, 0.5d);
        var options = new RhinoZoneExtractionOptions
        {
            DefaultSurfaceConstruction = construction,
            DefaultFenestrationConstruction = glazing,
        };

        Guid zoneRhinoId = new("66425fd0-5d82-4206-8aca-43305dd4fce4");
        Guid openingRhinoId = new("cc0648d2-3a36-4e08-a879-7f58669f62bf");
        using Brep box = CreateBox();
        int windowHost = FindWallFace(box, new Vector3d(0d, -1d, 0d));
        using var windowCurve = new PolylineCurve(Closed(
            new Point3d(1000d, 0d, 500d),
            new Point3d(2000d, 0d, 500d),
            new Point3d(2000d, 0d, 1500d),
            new Point3d(1000d, 0d, 1500d)));
        var window = new RhinoFenestrationSource(
            windowCurve,
            windowHost,
            "Rhino 7 smoke window",
            FenestrationType.Window,
            glazing.Id.Value,
            glazing,
            id: new EntityId("FNST-RHINO7-NATIVE"),
            rhinoObjectId: openingRhinoId,
            grasshopperPath: "{0}",
            grasshopperIndex: 0);
        var source = new RhinoZoneSource(
            box,
            "Rhino 7 native zone",
            1,
            profile.Name,
            profile,
            zoneId: new EntityId("ZONE-RHINO7-NATIVE"),
            rhinoObjectId: zoneRhinoId,
            grasshopperPath: "{0}",
            grasshopperIndex: 0,
            fenestrations: new[] { window });

        RhinoZoneExtractionResult extraction = RhinoZoneExtractor.Extract(
            new[] { source },
            context,
            options);
        Require(extraction.Success, Describe(extraction.Diagnostics));
        Require(extraction.Zones.Count == 1 && extraction.Zones[0].Surfaces.Count == 6,
            "Rhino 7 box extraction did not create one six-surface zone.");
        Require(AlmostEqual(extraction.Zones[0].Area, 12d)
                && AlmostEqual(extraction.Zones[0].Height, 2d),
            "Rhino 7 box extraction did not normalize millimetres to SI area and height.");
        Require(AlmostEqual(
                extraction.Zones[0].Surfaces.SelectMany(surface => surface.Fenestrations).Single().Area,
                1d),
            "Rhino 7 explicit opening area changed during extraction.");
        Require(extraction.GeometryMap.Count == 8,
            "Rhino 7 extraction did not retain zone, face, and opening provenance.");
        RhinoDomainGeometryMapEntry openingMap = extraction.GeometryMap.Single(entry =>
            entry.Kind == RhinoMappedGeometryKind.Fenestration);
        Require(openingMap.BrepLoopIndex is null
                && openingMap.FenestrationSourceIndex == 0
                && openingMap.Provenance.RhinoObjectId == openingRhinoId,
            "Rhino 7 explicit-opening provenance did not retain its source index and object ID.");

        var model = new GreenRetrofitModel(
            "Rhino 7 native model",
            0d,
            "서울특별시 종로구",
            new DateTime(2020, 1, 1),
            false,
            extraction.Floors,
            new[] { material },
            new[] { construction },
            new[] { glazing });
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
        Require(conversion.Success, Describe(conversion.Diagnostics));
        Require(conversion.RequireEnergyModel().Zones.Single().Profile.Id == profile.Id,
            "Rhino 7 conversion did not preserve the stable profile entity ID.");

        using RhinoConversionPreview preview = RhinoConversionPreviewBuilder.Create(
            model,
            conversion,
            context,
            extraction.GeometryMap,
            explodeDistanceMetres: 0.25d);
        Require(preview.Success, Describe(preview.Diagnostics));
        Require(preview.Surfaces.Count == 6,
            "Rhino 7 conversion preview did not contain six surfaces.");
        Require(preview.Surfaces.All(item => item.Geometry.IsValid && item.ExplodedGeometry.IsValid),
            "Rhino 7 conversion preview contains an invalid exact or exploded Brep.");
        Require(preview.Surfaces.All(item =>
                AlmostEqual(item.ConvertedNetArea, item.RhinoNetArea, 1e-8d)
                && AlmostEqual(item.GrossAreaDifference ?? double.NaN, 0d, 1e-8d)
                && AlmostEqual(item.OpeningAreaDifference ?? double.NaN, 0d, 1e-8d)),
            "Rhino 7 preview changed source, converted, or Rhino surface areas.");
        Require(preview.Surfaces.All(item =>
                item.SourceGeometry?.RhinoObjectId == zoneRhinoId
                && !string.IsNullOrWhiteSpace(item.SourceGeometryFingerprint)),
            "Rhino 7 preview lost source-surface provenance.");
        Require(preview.Surfaces.All(item =>
                LabelIsOnTrimmedFace(item)
                && item.ExplodedLabelPoint.DistanceTo(item.LabelPoint) > 0d),
            "Rhino 7 preview produced an exterior label anchor or an unshifted exploded label.");

        Console.WriteLine("hosted-simpledragon-native=ok (box=1; surfaces=6; openings=1; previews=6)");
    }

    private static Brep CreateBox()
    {
        return new Box(new BoundingBox(0d, 0d, 0d, 4000d, 3000d, 2000d)).ToBrep();
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
            if (normal * targetNormal > 0.999999d)
            {
                return face.FaceIndex;
            }
        }

        throw new InvalidOperationException("The Rhino 7 smoke window host face was not found.");
    }

    private static UsageProfile CreateProfile()
    {
        var operation = Enum.GetValues(typeof(UsageDay))
            .Cast<UsageDay>()
            .ToDictionary(day => day, _ => true);
        return new UsageProfile(
            "Rhino 7 smoke profile",
            8,
            18,
            7,
            19,
            4d,
            0d,
            10d,
            0.1d,
            5d,
            20d,
            26d,
            operation);
    }

    private static Polyline Closed(params Point3d[] points)
    {
        var polyline = new Polyline(points);
        polyline.Add(points[0]);
        return polyline;
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
            && face.PointAt(u, v).DistanceTo(preview.LabelPoint) <= 1e-5d;
    }

    private static bool AlmostEqual(double first, double second, double tolerance = 1e-10d)
    {
        return Math.Abs(first - second) <= tolerance;
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(item => item.Code + ": " + item.Message));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
