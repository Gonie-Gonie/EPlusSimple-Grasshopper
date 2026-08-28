using System.Runtime.Serialization;
using System.Security.Cryptography;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace GonieGonie.Dragons.ExampleDefinitions;

internal static class ExampleBuildingModels
{
    private const string ZoneRole = "ThermalZone";
    private const string OpeningRole = "WindowOpening";
    private const string RoleKey = "DragonRole";
    private const string ZonesLayer = "DRAGON_ZONES";
    private const string OpeningsLayer = "DRAGON_OPENINGS";
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
                new OpeningSpec("WINDOW_ZONE_01_SOUTH", 0, 1.5, 4.5, 0.9, 2.2),
                new OpeningSpec("WINDOW_ZONE_02_SOUTH", 1, 7.5, 10.5, 0.9, 2.2),
            },
            new[] { new AdjacencySpec(0, 1) }),
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
                new OpeningSpec("WINDOW_ZONE_01_SOUTH", 0, 1.2, 3.8, 0.9, 2.1),
                new OpeningSpec("WINDOW_ZONE_02_SOUTH", 1, 6.4, 9.6, 0.9, 2.1),
                new OpeningSpec("WINDOW_ZONE_03_SOUTH", 2, 1.2, 3.8, 3.9, 5.1),
            },
            new[] { new AdjacencySpec(0, 1), new AdjacencySpec(0, 2) }),
    };

    internal static IReadOnlyList<ExampleBuildingModelResult> Run(ExampleHostInputs inputs)
    {
        return Models.Select(model => inputs.Action == ExampleHostAction.Generate
            ? Generate(model, inputs)
            : Validate(model, inputs)).ToArray();
    }

    internal static Brep[] CreateZoneBreps(string fileName)
    {
        BuildingModelSpec spec = RequireSpec(fileName);
        return spec.Zones.Select(CreateBrep).ToArray();
    }

    internal static Curve[] CreateOpeningCurves(string fileName)
    {
        BuildingModelSpec spec = RequireSpec(fileName);
        return spec.Openings.Select(CreateOpeningCurve).ToArray();
    }

    internal static int[] OpeningZoneIndices(string fileName)
    {
        return RequireSpec(fileName).Openings.Select(item => item.ZoneIndex).ToArray();
    }

    internal static int[] OpeningFaceIndices(string fileName)
    {
        BuildingModelSpec spec = RequireSpec(fileName);
        Brep[] zones = spec.Zones.Select(CreateBrep).ToArray();
        return spec.Openings.Select(opening => FindSouthFaceIndex(zones[opening.ZoneIndex])).ToArray();
    }

    internal static void ValidateEmbeddedGeometry(
        string fileName,
        string examplesRoot,
        IReadOnlyList<Brep> zoneBreps,
        IReadOnlyList<Curve> openingCurves)
    {
        BuildingModelSpec spec = RequireSpec(fileName);
        Require(zoneBreps.Count == spec.Zones.Length, fileName + " embedded zone count changed.");
        Require(openingCurves.Count == spec.Openings.Length, fileName + " embedded opening count changed.");
        for (int index = 0; index < spec.Zones.Length; index++)
        {
            ValidateBrep(zoneBreps[index], spec.Zones[index], fileName + " embedded zone");
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
            Require(model.Write(candidatePath, 7), "Rhino failed to write " + candidatePath + ".");
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
            Require(model.Write(roundTripPath, 7), "Rhino failed to round-trip " + canonicalPath + ".");
        }

        ValidateFile(roundTripPath, spec);
        return Result(canonicalPath, spec, generated: false);
    }

    private static File3dm CreateModel(BuildingModelSpec spec)
    {
        var model = new File3dm();
        model.Settings.ModelUnitSystem = UnitSystem.Meters;
        model.Settings.ModelAbsoluteTolerance = 0.001;
        int zoneLayer = model.AllLayers.AddLayer(
            ZonesLayer,
            System.Drawing.Color.FromArgb(35, 115, 220));
        int openingLayer = model.AllLayers.AddLayer(
            OpeningsLayer,
            System.Drawing.Color.FromArgb(35, 210, 235));
        Require(zoneLayer >= 0 && openingLayer >= 0, "Rhino refused to create example layers.");

        foreach (ZoneBoxSpec zone in spec.Zones)
        {
            var attributes = new ObjectAttributes
            {
                LayerIndex = zoneLayer,
                Name = zone.Name,
            };
            attributes.SetUserString(RoleKey, ZoneRole);
            Require(
                model.Objects.AddBrep(CreateBrep(zone), attributes) != Guid.Empty,
                "Rhino refused to add zone " + zone.Name + ".");
        }

        foreach (OpeningSpec opening in spec.Openings)
        {
            var attributes = new ObjectAttributes
            {
                LayerIndex = openingLayer,
                Name = opening.Name,
            };
            attributes.SetUserString(RoleKey, OpeningRole);
            attributes.SetUserString("ZoneName", spec.Zones[opening.ZoneIndex].Name);
            Require(
                model.Objects.AddCurve(CreateOpeningCurve(opening), attributes) != Guid.Empty,
                "Rhino refused to add opening " + opening.Name + ".");
        }

        return model;
    }

    private static void ValidateFile(string path, BuildingModelSpec spec)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Tracked Rhino building example is absent.", path);
        }

        using File3dm model = File3dm.Read(path)
            ?? throw new InvalidOperationException("Rhino could not read " + path + ".");
        Require(model.Settings.ModelUnitSystem == UnitSystem.Meters, spec.FileName + " must use metres.");
        string[] layerNames = model.AllLayers.Select(item => item.Name).ToArray();
        Require(layerNames.Contains(ZonesLayer, StringComparer.Ordinal), spec.FileName + " lost the zones layer.");
        Require(layerNames.Contains(OpeningsLayer, StringComparer.Ordinal), spec.FileName + " lost the openings layer.");

        File3dmObject[] zoneObjects = model.Objects
            .Where(item => string.Equals(item.Attributes.GetUserString(RoleKey), ZoneRole, StringComparison.Ordinal))
            .ToArray();
        File3dmObject[] openingObjects = model.Objects
            .Where(item => string.Equals(item.Attributes.GetUserString(RoleKey), OpeningRole, StringComparison.Ordinal))
            .ToArray();
        Require(zoneObjects.Length == spec.Zones.Length, spec.FileName + " zone object count changed.");
        Require(openingObjects.Length == spec.Openings.Length, spec.FileName + " opening object count changed.");

        for (int index = 0; index < spec.Zones.Length; index++)
        {
            ZoneBoxSpec expected = spec.Zones[index];
            File3dmObject actual = zoneObjects.Single(item =>
                string.Equals(item.Attributes.Name, expected.Name, StringComparison.Ordinal));
            Require(actual.Geometry is Brep, expected.Name + " is not a Brep.");
            Require(
                string.Equals(LayerName(model, actual), ZonesLayer, StringComparison.Ordinal),
                expected.Name + " is not on " + ZonesLayer + ".");
            ValidateBrep((Brep)actual.Geometry, expected, spec.FileName + " zone");
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
                    actual.Attributes.GetUserString("ZoneName"),
                    spec.Zones[expected.ZoneIndex].Name,
                    StringComparison.Ordinal),
                expected.Name + " has the wrong ZoneName user string.");
            ValidateOpening((Curve)actual.Geometry, expected, spec.FileName + " opening");
        }

        foreach (AdjacencySpec pair in spec.Adjacencies)
        {
            BoundingBox first = ((Brep)zoneObjects.Single(item => string.Equals(
                item.Attributes.Name,
                spec.Zones[pair.FirstZone].Name,
                StringComparison.Ordinal)).Geometry).GetBoundingBox(true);
            BoundingBox second = ((Brep)zoneObjects.Single(item => string.Equals(
                item.Attributes.Name,
                spec.Zones[pair.SecondZone].Name,
                StringComparison.Ordinal)).Geometry).GetBoundingBox(true);
            Require(AreAdjacent(first, second), spec.FileName + " lost an expected zone adjacency.");
        }
    }

    private static void ValidateBrep(Brep brep, ZoneBoxSpec expected, string label)
    {
        Require(brep.IsSolid, label + " " + expected.Name + " must be a closed solid Brep.");
        BoundingBox bounds = brep.GetBoundingBox(true);
        Require(Close(bounds.Min.X, expected.X0) && Close(bounds.Max.X, expected.X1), label + " X bounds changed.");
        Require(Close(bounds.Min.Y, expected.Y0) && Close(bounds.Max.Y, expected.Y1), label + " Y bounds changed.");
        Require(Close(bounds.Min.Z, expected.Z0) && Close(bounds.Max.Z, expected.Z1), label + " Z bounds changed.");
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

    private static bool AreAdjacent(BoundingBox first, BoundingBox second)
    {
        bool x = (Close(first.Max.X, second.Min.X) || Close(second.Max.X, first.Min.X))
            && Overlap(first.Min.Y, first.Max.Y, second.Min.Y, second.Max.Y)
            && Overlap(first.Min.Z, first.Max.Z, second.Min.Z, second.Max.Z);
        bool y = (Close(first.Max.Y, second.Min.Y) || Close(second.Max.Y, first.Min.Y))
            && Overlap(first.Min.X, first.Max.X, second.Min.X, second.Max.X)
            && Overlap(first.Min.Z, first.Max.Z, second.Min.Z, second.Max.Z);
        bool z = (Close(first.Max.Z, second.Min.Z) || Close(second.Max.Z, first.Min.Z))
            && Overlap(first.Min.X, first.Max.X, second.Min.X, second.Max.X)
            && Overlap(first.Min.Y, first.Max.Y, second.Min.Y, second.Max.Y);
        return x || y || z;
    }

    private static string LayerName(File3dm model, File3dmObject value)
    {
        Layer layer = model.AllLayers.Single(item => item.Index == value.Attributes.LayerIndex);
        return layer.Name;
    }

    private static bool Overlap(double a0, double a1, double b0, double b1)
    {
        return Math.Min(a1, b1) - Math.Max(a0, b0) > Tolerance;
    }

    private static Brep CreateBrep(ZoneBoxSpec box)
    {
        return Brep.CreateFromBox(new BoundingBox(
            new Point3d(box.X0, box.Y0, box.Z0),
            new Point3d(box.X1, box.Y1, box.Z1)));
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

    private static int FindSouthFaceIndex(Brep brep)
    {
        for (int index = 0; index < brep.Faces.Count; index++)
        {
            BoundingBox bounds = brep.Faces[index].GetBoundingBox(true);
            if (Math.Abs(bounds.Min.Y) <= Tolerance && Math.Abs(bounds.Max.Y) <= Tolerance)
            {
                return index;
            }
        }

        throw new InvalidOperationException("A generated example zone has no south face.");
    }

    private static ExampleBuildingModelResult Result(string path, BuildingModelSpec spec, bool generated)
    {
        return new ExampleBuildingModelResult
        {
            FileName = spec.FileName,
            CanonicalPath = Path.GetFullPath(path),
            Sha256 = ComputeSha256(path),
            ZoneCount = spec.Zones.Length,
            OpeningCount = spec.Openings.Length,
            AdjacentPairCount = spec.Adjacencies.Length,
            LayerNames = new[] { ZonesLayer, OpeningsLayer },
            ObjectNames = spec.Zones.Select(item => item.Name).Concat(spec.Openings.Select(item => item.Name)).ToArray(),
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
        int ZoneIndex,
        double X0,
        double X1,
        double Z0,
        double Z1);

    private sealed record AdjacencySpec(int FirstZone, int SecondZone);
}

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
