using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace Dragons.ExampleDefinitions;

internal static class ExampleBuildingModels
{
    private const string SurfaceRole = "ThermalSurface";
    private const string OpeningRole = "WindowOpening";
    private const string RoleKey = "DragonRole";
    private const string SurfacesLayer = "DRAGON_SURFACES";
    private const string OpeningsLayer = "DRAGON_OPENINGS";
    private const string ModelApplicationName = "Dragons Grasshopper";
    private const string ModelApplicationUrl = "https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper";
    private const string ModelApplicationDetails =
        "Neutral Rhino 7+ geometry for Dragon zone, surface, opening, and adjacency workflows.";
    private const double Tolerance = 1e-7;

    private static readonly BuildingModelSpec[] Models =
    {
        new(
            "30-two-zone-office.3dm",
            new[]
            {
                new ZoneBoxSpec("ZONE_01_WEST", 0, 0, 0, 6, 8, 3.2),
                new ZoneBoxSpec("ZONE_02_EAST", 6, 0, 0, 12, 8, 3.2),
            },
            new[]
            {
                new OpeningSpec("WINDOW_ZONE_01_SOUTH", "ZONE_01_WEST_SOUTH", 1.5, 4.5, 0.9, 2.2),
                new OpeningSpec("WINDOW_ZONE_02_SOUTH", "ZONE_02_EAST_SOUTH", 7.5, 10.5, 0.9, 2.2),
            },
            new[] { new AdjacencySpec("ZONE_01_WEST", "ZONE_02_EAST") }),
        new(
            "31-three-zone-stepped-office.3dm",
            new[]
            {
                new ZoneBoxSpec("ZONE_01_WEST_GROUND", 0, 0, 0, 5, 7, 3),
                new ZoneBoxSpec("ZONE_02_EAST_GROUND", 5, 0, 0, 11, 7, 3),
                new ZoneBoxSpec("ZONE_03_WEST_UPPER", 0, 0, 3, 5, 7, 6),
            },
            new[]
            {
                new OpeningSpec("WINDOW_ZONE_01_SOUTH", "ZONE_01_WEST_GROUND_SOUTH", 1.2, 3.8, 0.9, 2.1),
                new OpeningSpec("WINDOW_ZONE_02_SOUTH", "ZONE_02_EAST_GROUND_SOUTH", 6.4, 9.6, 0.9, 2.1),
                new OpeningSpec("WINDOW_ZONE_03_SOUTH", "ZONE_03_WEST_UPPER_SOUTH", 1.2, 3.8, 3.9, 5.1),
            },
            new[]
            {
                new AdjacencySpec("ZONE_01_WEST_GROUND", "ZONE_02_EAST_GROUND"),
                new AdjacencySpec("ZONE_01_WEST_GROUND", "ZONE_03_WEST_UPPER"),
            }),
    };

    internal static IReadOnlyList<ExampleBuildingModelResult> Run(ExampleHostInputs inputs)
    {
        return Models.Select(model => inputs.Action == ExampleHostAction.Generate
            ? Generate(model, inputs)
            : Validate(model, inputs)).ToArray();
    }

    internal static ExampleSurfaceGeometry[] CreateSurfaceBreps(string fileName)
    {
        BuildingModelSpec spec = RequireSpec(fileName);
        return SurfaceSpecs(spec)
            .Select(surface => new ExampleSurfaceGeometry(
                surface.ZoneName,
                surface.Name,
                surface.Type,
                surface.BoundaryIntent,
                CreateSurfaceBrep(surface)))
            .ToArray();
    }

    internal static Curve[] CreateOpeningCurves(string fileName)
    {
        BuildingModelSpec spec = RequireSpec(fileName);
        return spec.Openings.Select(CreateOpeningCurve).ToArray();
    }

    internal static void ValidateEmbeddedGeometry(
        string fileName,
        string examplesRoot,
        IReadOnlyList<Brep> surfaceBreps,
        IReadOnlyList<Curve> openingCurves)
    {
        BuildingModelSpec spec = RequireSpec(fileName);
        SurfaceSpec[] surfaces = SurfaceSpecs(spec);
        Require(surfaceBreps.Count == surfaces.Length, fileName + " embedded surface count changed.");
        Require(openingCurves.Count == spec.Openings.Length, fileName + " embedded opening count changed.");

        var unmatched = surfaceBreps.ToList();
        foreach (SurfaceSpec expected in surfaces)
        {
            int match = unmatched.FindIndex(candidate => SurfaceMatches(candidate, expected));
            Require(match >= 0, fileName + " embedded surface " + expected.Name + " changed.");
            ValidateSurfaceBrep(unmatched[match], expected, fileName + " embedded surface");
            unmatched.RemoveAt(match);
        }

        for (int index = 0; index < spec.Openings.Length; index++)
        {
            ValidateOpening(openingCurves[index], spec.Openings[index], fileName + " embedded opening");
        }

        string modelPath = Path.Combine(examplesRoot, fileName);
        ValidateFile(modelPath, spec);
    }

    private static ExampleBuildingModelResult Generate(BuildingModelSpec spec, ExampleHostInputs inputs)
    {
        string candidateDirectory = Path.Combine(inputs.OutputDirectory, "generated");
        Directory.CreateDirectory(candidateDirectory);
        string candidatePath = Path.Combine(candidateDirectory, spec.FileName);
        using (File3dm model = CreateModel(spec))
        {
            WriteNeutralModel(model, candidatePath);
        }

        ValidateFile(candidatePath, spec);
        string canonicalPath = Path.Combine(inputs.ExamplesRoot, spec.FileName);
        Directory.CreateDirectory(inputs.ExamplesRoot);
        CanonicalExamplePublisher.Publish(
            candidatePath,
            canonicalPath,
            inputs.OutputDirectory,
            path => ValidateFile(path, spec));
        ValidateFile(canonicalPath, spec);
        return Result(canonicalPath, spec, generated: true);
    }

    private static ExampleBuildingModelResult Validate(BuildingModelSpec spec, ExampleHostInputs inputs)
    {
        string canonicalPath = Path.Combine(inputs.ExamplesRoot, spec.FileName);
        ValidateFile(canonicalPath, spec);
        string roundTripDirectory = Path.Combine(inputs.OutputDirectory, "roundtrip");
        Directory.CreateDirectory(roundTripDirectory);
        string roundTripPath = Path.Combine(roundTripDirectory, spec.FileName);
        using (File3dm model = File3dm.Read(canonicalPath)
            ?? throw new InvalidOperationException("Rhino could not read " + canonicalPath + "."))
        {
            WriteNeutralModel(model, roundTripPath);
        }

        ValidateFile(roundTripPath, spec);
        return Result(canonicalPath, spec, generated: false);
    }

    private static File3dm CreateModel(BuildingModelSpec spec)
    {
        var model = new File3dm();
        model.Settings.ModelUnitSystem = UnitSystem.Meters;
        model.Settings.ModelAbsoluteTolerance = 0.001;
        int surfaceLayer = model.AllLayers.AddLayer(
            SurfacesLayer,
            System.Drawing.Color.FromArgb(35, 115, 220));
        int openingLayer = model.AllLayers.AddLayer(
            OpeningsLayer,
            System.Drawing.Color.FromArgb(35, 210, 235));
        Require(surfaceLayer >= 0 && openingLayer >= 0, "Rhino refused to create example layers.");

        foreach (SurfaceSpec surface in SurfaceSpecs(spec))
        {
            var attributes = new ObjectAttributes
            {
                LayerIndex = surfaceLayer,
                Name = surface.Name,
            };
            attributes.SetUserString(RoleKey, SurfaceRole);
            attributes.SetUserString("ZoneName", surface.ZoneName);
            attributes.SetUserString("SurfaceType", surface.Type.ToString());
            attributes.SetUserString("BoundaryIntent", surface.BoundaryIntent);
            Require(
                model.Objects.AddBrep(CreateSurfaceBrep(surface), attributes) != Guid.Empty,
                "Rhino refused to add surface " + surface.Name + ".");
        }

        foreach (OpeningSpec opening in spec.Openings)
        {
            var attributes = new ObjectAttributes
            {
                LayerIndex = openingLayer,
                Name = opening.Name,
            };
            attributes.SetUserString(RoleKey, OpeningRole);
            attributes.SetUserString("SurfaceName", opening.SurfaceName);
            Require(
                model.Objects.AddCurve(CreateOpeningCurve(opening), attributes) != Guid.Empty,
                "Rhino refused to add opening " + opening.Name + ".");
        }

        ApplyNeutralModelIdentity(model);
        return model;
    }

    private static void WriteNeutralModel(File3dm model, string path)
    {
        ApplyNeutralModelIdentity(model);
        var options = new File3dmWriteOptions
        {
            Version = 7,
            SaveUserData = true,
        };
        byte[] bytes = model.ToByteArray(options);
        Require(bytes.Length > 0, "Rhino produced an empty model archive for " + path + ".");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void ApplyNeutralModelIdentity(File3dm model)
    {
        model.StartSectionComments = string.Empty;
        model.ApplicationName = ModelApplicationName;
        model.ApplicationUrl = ModelApplicationUrl;
        model.ApplicationDetails = ModelApplicationDetails;
    }

    private static void ValidateNoLocalBinaryIdentity(string path, string fileName)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string ascii = Encoding.ASCII.GetString(bytes);
        string utf16 = Encoding.Unicode.GetString(bytes);
        bool exposesUser = System.Text.RegularExpressions.Regex.IsMatch(
                ascii,
                "GonieGonie",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            || System.Text.RegularExpressions.Regex.IsMatch(
                utf16,
                "GonieGonie",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        bool exposesWindowsProfile = System.Text.RegularExpressions.Regex.IsMatch(
                ascii,
                @"[A-Za-z]:[\\/]Users[\\/]",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            || System.Text.RegularExpressions.Regex.IsMatch(
                utf16,
                @"[A-Za-z]:[\\/]Users[\\/]",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Require(!exposesUser && !exposesWindowsProfile,
            fileName + " exposes a local user or absolute Windows profile path in its binary metadata.");
    }

    private static void ValidateFile(string path, BuildingModelSpec spec)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Tracked Rhino building example is absent.", path);
        }

        using File3dm model = File3dm.Read(path)
            ?? throw new InvalidOperationException("Rhino could not read " + path + ".");
        Require(
            string.Equals(model.ApplicationName, ModelApplicationName, StringComparison.Ordinal)
                && string.Equals(model.ApplicationUrl, ModelApplicationUrl, StringComparison.Ordinal)
                && string.Equals(model.ApplicationDetails, ModelApplicationDetails, StringComparison.Ordinal),
            spec.FileName + " application metadata changed.");
        ValidateNoLocalBinaryIdentity(path, spec.FileName);
        Require(model.Settings.ModelUnitSystem == UnitSystem.Meters, spec.FileName + " must use metres.");
        string[] layerNames = model.AllLayers.Select(item => item.Name).ToArray();
        Require(layerNames.Contains(SurfacesLayer, StringComparer.Ordinal), spec.FileName + " lost the surfaces layer.");
        Require(layerNames.Contains(OpeningsLayer, StringComparer.Ordinal), spec.FileName + " lost the openings layer.");

        SurfaceSpec[] surfaces = SurfaceSpecs(spec);
        File3dmObject[] surfaceObjects = model.Objects
            .Where(item => string.Equals(item.Attributes.GetUserString(RoleKey), SurfaceRole, StringComparison.Ordinal))
            .ToArray();
        File3dmObject[] openingObjects = model.Objects
            .Where(item => string.Equals(item.Attributes.GetUserString(RoleKey), OpeningRole, StringComparison.Ordinal))
            .ToArray();
        Require(surfaceObjects.Length == surfaces.Length, spec.FileName + " surface object count changed.");
        Require(openingObjects.Length == spec.Openings.Length, spec.FileName + " opening object count changed.");

        foreach (SurfaceSpec expected in surfaces)
        {
            File3dmObject actual = surfaceObjects.Single(item =>
                string.Equals(item.Attributes.Name, expected.Name, StringComparison.Ordinal));
            Require(actual.Geometry is Brep, expected.Name + " is not a Brep.");
            Require(
                string.Equals(LayerName(model, actual), SurfacesLayer, StringComparison.Ordinal),
                expected.Name + " is not on " + SurfacesLayer + ".");
            Require(
                string.Equals(actual.Attributes.GetUserString("ZoneName"), expected.ZoneName, StringComparison.Ordinal),
                expected.Name + " has the wrong ZoneName user string.");
            Require(
                string.Equals(actual.Attributes.GetUserString("SurfaceType"), expected.Type.ToString(), StringComparison.Ordinal),
                expected.Name + " has the wrong SurfaceType user string.");
            Require(
                string.Equals(actual.Attributes.GetUserString("BoundaryIntent"), expected.BoundaryIntent, StringComparison.Ordinal),
                expected.Name + " has the wrong BoundaryIntent user string.");
            ValidateSurfaceBrep((Brep)actual.Geometry, expected, spec.FileName + " surface");
        }

        for (int index = 0; index < spec.Openings.Length; index++)
        {
            OpeningSpec expected = spec.Openings[index];
            File3dmObject actual = openingObjects.Single(item =>
                string.Equals(item.Attributes.Name, expected.Name, StringComparison.Ordinal));
            Require(actual.Geometry is Curve, expected.Name + " is not a curve.");
            Require(
                string.Equals(LayerName(model, actual), OpeningsLayer, StringComparison.Ordinal),
                expected.Name + " is not on " + OpeningsLayer + ".");
            Require(
                string.Equals(
                    actual.Attributes.GetUserString("SurfaceName"),
                    expected.SurfaceName,
                    StringComparison.Ordinal),
                expected.Name + " has the wrong SurfaceName user string.");
            Require(
                surfaceObjects.Any(item => string.Equals(
                    item.Attributes.Name,
                    expected.SurfaceName,
                    StringComparison.Ordinal)),
                expected.Name + " references a missing owning Surface.");
            ValidateOpening((Curve)actual.Geometry, expected, spec.FileName + " opening");
        }

        foreach (AdjacencySpec pair in spec.Adjacencies)
        {
            File3dmObject[] first = surfaceObjects.Where(item => string.Equals(
                item.Attributes.GetUserString("ZoneName"),
                pair.FirstZoneName,
                StringComparison.Ordinal)).ToArray();
            File3dmObject[] second = surfaceObjects.Where(item => string.Equals(
                item.Attributes.GetUserString("ZoneName"),
                pair.SecondZoneName,
                StringComparison.Ordinal)).ToArray();
            Require(
                first.Any(a => second.Any(b => CoincidentOppositeFaces((Brep)a.Geometry, (Brep)b.Geometry))),
                spec.FileName + " lost an expected zone adjacency.");
        }
    }

    private static void ValidateSurfaceBrep(Brep brep, SurfaceSpec expected, string label)
    {
        Require(brep.IsValid, label + " " + expected.Name + " must be valid.");
        Require(!brep.IsSolid, label + " " + expected.Name + " must be a single face, not a Zone solid.");
        Require(brep.Faces.Count == 1, label + " " + expected.Name + " must contain exactly one face.");
        Require(brep.Faces[0].IsPlanar(0.001), label + " " + expected.Name + " must be planar.");
        BoundingBox bounds = brep.GetBoundingBox(true);
        BoundingBox expectedBounds = new(expected.Points);
        Require(Close(bounds.Min.X, expectedBounds.Min.X) && Close(bounds.Max.X, expectedBounds.Max.X), label + " X bounds changed.");
        Require(Close(bounds.Min.Y, expectedBounds.Min.Y) && Close(bounds.Max.Y, expectedBounds.Max.Y), label + " Y bounds changed.");
        Require(Close(bounds.Min.Z, expectedBounds.Min.Z) && Close(bounds.Max.Z, expectedBounds.Max.Z), label + " Z bounds changed.");
        Vector3d actualNormal = FaceNormal(brep);
        Require(actualNormal * expected.OutwardNormal > 0.999, label + " outward normal changed.");
    }

    private static void ValidateOpening(Curve curve, OpeningSpec expected, string label)
    {
        Require(curve.IsClosed, label + " " + expected.Name + " must be closed.");
        Require(curve.TryGetPlane(out Plane plane, 0.001), label + " " + expected.Name + " must be planar.");
        Require(Math.Abs(plane.OriginY) <= 0.001, label + " " + expected.Name + " must lie on the south facade.");
        BoundingBox bounds = curve.GetBoundingBox(true);
        Require(Close(bounds.Min.X, expected.X0) && Close(bounds.Max.X, expected.X1), label + " X bounds changed.");
        Require(Close(bounds.Min.Z, expected.Z0) && Close(bounds.Max.Z, expected.Z1), label + " Z bounds changed.");
    }

    private static SurfaceSpec[] SurfaceSpecs(BuildingModelSpec spec)
    {
        return spec.Zones.SelectMany(SurfacesForZone).ToArray();
    }

    private static IEnumerable<SurfaceSpec> SurfacesForZone(ZoneBoxSpec zone)
    {
        yield return new SurfaceSpec(
            zone.Name,
            zone.Name + "_FLOOR",
            ExampleSurfaceType.Floor,
            Math.Abs(zone.Z0) <= Tolerance ? "Ground" : "Outdoors",
            new[]
            {
                new Point3d(zone.X0, zone.Y0, zone.Z0),
                new Point3d(zone.X0, zone.Y1, zone.Z0),
                new Point3d(zone.X1, zone.Y1, zone.Z0),
                new Point3d(zone.X1, zone.Y0, zone.Z0),
            },
            -Vector3d.ZAxis);
        yield return new SurfaceSpec(
            zone.Name,
            zone.Name + "_CEILING",
            ExampleSurfaceType.Ceiling,
            "Outdoors",
            new[]
            {
                new Point3d(zone.X0, zone.Y0, zone.Z1),
                new Point3d(zone.X1, zone.Y0, zone.Z1),
                new Point3d(zone.X1, zone.Y1, zone.Z1),
                new Point3d(zone.X0, zone.Y1, zone.Z1),
            },
            Vector3d.ZAxis);
        yield return new SurfaceSpec(
            zone.Name,
            SouthSurfaceName(zone.Name),
            ExampleSurfaceType.Wall,
            "Outdoors",
            new[]
            {
                new Point3d(zone.X0, zone.Y0, zone.Z0),
                new Point3d(zone.X1, zone.Y0, zone.Z0),
                new Point3d(zone.X1, zone.Y0, zone.Z1),
                new Point3d(zone.X0, zone.Y0, zone.Z1),
            },
            -Vector3d.YAxis);
        yield return new SurfaceSpec(
            zone.Name,
            zone.Name + "_NORTH",
            ExampleSurfaceType.Wall,
            "Outdoors",
            new[]
            {
                new Point3d(zone.X1, zone.Y1, zone.Z0),
                new Point3d(zone.X0, zone.Y1, zone.Z0),
                new Point3d(zone.X0, zone.Y1, zone.Z1),
                new Point3d(zone.X1, zone.Y1, zone.Z1),
            },
            Vector3d.YAxis);
        yield return new SurfaceSpec(
            zone.Name,
            zone.Name + "_WEST",
            ExampleSurfaceType.Wall,
            "Outdoors",
            new[]
            {
                new Point3d(zone.X0, zone.Y1, zone.Z0),
                new Point3d(zone.X0, zone.Y0, zone.Z0),
                new Point3d(zone.X0, zone.Y0, zone.Z1),
                new Point3d(zone.X0, zone.Y1, zone.Z1),
            },
            -Vector3d.XAxis);
        yield return new SurfaceSpec(
            zone.Name,
            zone.Name + "_EAST",
            ExampleSurfaceType.Wall,
            "Outdoors",
            new[]
            {
                new Point3d(zone.X1, zone.Y0, zone.Z0),
                new Point3d(zone.X1, zone.Y1, zone.Z0),
                new Point3d(zone.X1, zone.Y1, zone.Z1),
                new Point3d(zone.X1, zone.Y0, zone.Z1),
            },
            Vector3d.XAxis);
    }

    private static Brep CreateSurfaceBrep(SurfaceSpec surface)
    {
        Brep? result = Brep.CreateFromCornerPoints(
            surface.Points[0],
            surface.Points[1],
            surface.Points[2],
            surface.Points[3],
            Tolerance);
        Require(result is not null, "Rhino refused to create surface " + surface.Name + ".");
        Vector3d normal = FaceNormal(result!);
        if (normal * surface.OutwardNormal < 0d)
        {
            result!.Flip();
        }

        return result!;
    }

    private static bool SurfaceMatches(Brep candidate, SurfaceSpec expected)
    {
        if (!candidate.IsValid || candidate.Faces.Count != 1)
        {
            return false;
        }

        BoundingBox expectedBounds = new(expected.Points);
        return BoundsEqual(candidate.GetBoundingBox(true), expectedBounds)
            && FaceNormal(candidate) * expected.OutwardNormal > 0.999;
    }

    private static bool CoincidentOppositeFaces(Brep first, Brep second)
    {
        return first.IsValid
            && second.IsValid
            && first.Faces.Count == 1
            && second.Faces.Count == 1
            && BoundsEqual(first.GetBoundingBox(true), second.GetBoundingBox(true))
            && FaceNormal(first) * FaceNormal(second) < -0.999;
    }

    private static Vector3d FaceNormal(Brep brep)
    {
        BrepFace face = brep.Faces[0];
        Interval u = face.Domain(0);
        Interval v = face.Domain(1);
        Vector3d normal = face.NormalAt(u.Mid, v.Mid);
        if (face.OrientationIsReversed)
        {
            normal.Reverse();
        }

        Require(normal.Unitize(), "A surface face has no usable normal.");
        return normal;
    }

    private static bool BoundsEqual(BoundingBox first, BoundingBox second)
    {
        return Close(first.Min.X, second.Min.X)
            && Close(first.Max.X, second.Max.X)
            && Close(first.Min.Y, second.Min.Y)
            && Close(first.Max.Y, second.Max.Y)
            && Close(first.Min.Z, second.Min.Z)
            && Close(first.Max.Z, second.Max.Z);
    }

    private static string SouthSurfaceName(string zoneName)
    {
        return zoneName + "_SOUTH";
    }

    private static string LayerName(File3dm model, File3dmObject value)
    {
        Layer layer = model.AllLayers.Single(item => item.Index == value.Attributes.LayerIndex);
        return layer.Name;
    }

    private static Curve CreateOpeningCurve(OpeningSpec opening)
    {
        return new PolylineCurve(new[]
        {
            new Point3d(opening.X0, 0, opening.Z0),
            new Point3d(opening.X1, 0, opening.Z0),
            new Point3d(opening.X1, 0, opening.Z1),
            new Point3d(opening.X0, 0, opening.Z1),
            new Point3d(opening.X0, 0, opening.Z0),
        });
    }

    private static ExampleBuildingModelResult Result(string path, BuildingModelSpec spec, bool generated)
    {
        return new ExampleBuildingModelResult
        {
            FileName = spec.FileName,
            CanonicalPath = Path.GetFullPath(path),
            Sha256 = ComputeSha256(path),
            ZoneCount = spec.Zones.Length,
            SurfaceCount = SurfaceSpecs(spec).Length,
            OpeningCount = spec.Openings.Length,
            AdjacentPairCount = spec.Adjacencies.Length,
            LayerNames = new[] { SurfacesLayer, OpeningsLayer },
            ObjectNames = SurfaceSpecs(spec).Select(item => item.Name).Concat(spec.Openings.Select(item => item.Name)).ToArray(),
            Generated = generated,
        };
    }

    private static BuildingModelSpec RequireSpec(string fileName)
    {
        return Models.SingleOrDefault(item => string.Equals(item.FileName, fileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Unknown building example " + fileName + ".");
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return string.Concat(sha256.ComputeHash(stream).Select(value =>
            value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static bool Close(double left, double right)
    {
        return Math.Abs(left - right) <= Tolerance;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record BuildingModelSpec(
        string FileName,
        ZoneBoxSpec[] Zones,
        OpeningSpec[] Openings,
        AdjacencySpec[] Adjacencies);

    private sealed record ZoneBoxSpec(
        string Name,
        double X0,
        double Y0,
        double Z0,
        double X1,
        double Y1,
        double Z1);

    private sealed record OpeningSpec(
        string Name,
        string SurfaceName,
        double X0,
        double X1,
        double Z0,
        double Z1);

    private sealed record SurfaceSpec(
        string ZoneName,
        string Name,
        ExampleSurfaceType Type,
        string BoundaryIntent,
        Point3d[] Points,
        Vector3d OutwardNormal);

    private sealed record AdjacencySpec(string FirstZoneName, string SecondZoneName);
}

internal enum ExampleSurfaceType
{
    Wall = 0,
    Ceiling = 1,
    Floor = 2,
}

internal sealed record ExampleSurfaceGeometry(
    string ZoneName,
    string Name,
    ExampleSurfaceType Type,
    string BoundaryIntent,
    Brep Geometry);

[DataContract]
internal sealed class ExampleBuildingModelResult
{
    [DataMember(Name = "fileName", Order = 1)]
    public string FileName { get; set; } = string.Empty;

    [DataMember(Name = "canonicalPath", Order = 2)]
    public string CanonicalPath { get; set; } = string.Empty;

    [DataMember(Name = "sha256", Order = 3)]
    public string Sha256 { get; set; } = string.Empty;

    [DataMember(Name = "zoneCount", Order = 4)]
    public int ZoneCount { get; set; }

    [DataMember(Name = "surfaceCount", Order = 10)]
    public int SurfaceCount { get; set; }

    [DataMember(Name = "openingCount", Order = 5)]
    public int OpeningCount { get; set; }

    [DataMember(Name = "adjacentPairCount", Order = 6)]
    public int AdjacentPairCount { get; set; }

    [DataMember(Name = "layerNames", Order = 7)]
    public string[] LayerNames { get; set; } = Array.Empty<string>();

    [DataMember(Name = "objectNames", Order = 8)]
    public string[] ObjectNames { get; set; } = Array.Empty<string>();

    [DataMember(Name = "generated", Order = 9)]
    public bool Generated { get; set; }
}
