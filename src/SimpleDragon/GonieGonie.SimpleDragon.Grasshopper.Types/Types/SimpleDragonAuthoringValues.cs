using System.Text;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Runtime;

namespace GonieGonie.SimpleDragon.Grasshopper.Types;

/// <summary>
/// Immutable, geometry-backed opening input used while authoring a SimpleDragon zone.
/// </summary>
public sealed class OpeningDefinition
{
    private readonly byte[] _geometryArchive;

    public OpeningDefinition(
        Curve geometry,
        string name,
        FenestrationType type,
        FenestrationConstruction construction,
        BlindType? blind = null,
        EntityId? id = null)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(geometry);
#else
        if (geometry is null)
        {
            throw new ArgumentNullException(nameof(geometry));
        }
#endif

        using Curve geometryCopy = geometry.DuplicateCurve()
            ?? throw new ArgumentException("The opening curve could not be duplicated.", nameof(geometry));
        if (!geometryCopy.IsValid || !geometryCopy.IsClosed)
        {
            throw new ArgumentException("An opening requires a valid closed curve.", nameof(geometry));
        }

        if (!geometryCopy.IsPlanar())
        {
            throw new ArgumentException("An opening curve must be planar.", nameof(geometry));
        }

        Name = AuthoringValueSupport.RequiredText(name, nameof(name));
        if (!Enum.IsDefined(typeof(FenestrationType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown fenestration type.");
        }

        if (blind.HasValue && !Enum.IsDefined(typeof(BlindType), blind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(blind), blind, "Unknown blind type.");
        }

        if (construction is null)
        {
            throw new ArgumentNullException(
                nameof(construction),
                "An opening must own its fenestration construction.");
        }

        AuthoringValueSupport.ValidateFenestrationConstruction(type, construction, blind);
        _geometryArchive = RhinoGeometryArchive.Encode(geometryCopy);
        Type = type;
        Construction = AuthoringValueSupport.Copy(construction);
        Blind = blind;
        Id = id;
    }

    public Curve Geometry => RhinoGeometryArchive.Decode<Curve>(_geometryArchive);

    public string Name { get; }

    public FenestrationType Type { get; }

    public FenestrationConstruction Construction { get; }

    public BlindType? Blind { get; }

    public EntityId? Id { get; }

    internal byte[] GeometryArchive => (byte[])_geometryArchive.Clone();

    internal OpeningDefinition Duplicate()
    {
        using Curve geometry = Geometry;
        return new OpeningDefinition(geometry, Name, Type, Construction, Blind, Id);
    }
}

/// <summary>
/// Immutable, geometry-backed zone input. A collection-level resolver converts these
/// definitions to final zones so that inter-zone adjacency can be determined together.
/// </summary>
public sealed class ZoneDefinition
{
    private readonly byte[] _geometryArchive;

    public ZoneDefinition(
        Brep geometry,
        string name,
        int floorNumber,
        UsageProfile profile,
        SurfaceConstruction? surfaceConstruction = null,
        SurfaceBoundaryCondition unmatchedFloorBoundary = SurfaceBoundaryCondition.Ground,
        double? lightDensity = 10d,
        IEnumerable<OpeningDefinition>? openings = null,
        IEnumerable<SupplySystem>? supplySystems = null,
        IEnumerable<VentilationAssignment>? ventilationAssignments = null)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(geometry);
#else
        if (geometry is null)
        {
            throw new ArgumentNullException(nameof(geometry));
        }
#endif

        using Brep geometryCopy = geometry.DuplicateBrep()
            ?? throw new ArgumentException("The zone Brep could not be duplicated.", nameof(geometry));
        if (!geometryCopy.IsValid || !geometryCopy.IsSolid)
        {
            throw new ArgumentException("A zone requires a valid closed Brep.", nameof(geometry));
        }

        Name = AuthoringValueSupport.RequiredText(name, nameof(name));
        Profile = AuthoringValueSupport.Copy(profile ?? throw new ArgumentNullException(nameof(profile)));
        if (unmatchedFloorBoundary != SurfaceBoundaryCondition.Ground
            && unmatchedFloorBoundary != SurfaceBoundaryCondition.Outdoors
            && unmatchedFloorBoundary != SurfaceBoundaryCondition.Adiabatic)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unmatchedFloorBoundary),
                unmatchedFloorBoundary,
                "An unmatched floor boundary must be Ground, Outdoors, or Adiabatic.");
        }

        if (lightDensity.HasValue
            && (double.IsNaN(lightDensity.Value)
                || double.IsInfinity(lightDensity.Value)
                || lightDensity.Value < 0d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lightDensity),
                lightDensity,
                "Lighting power density must be finite and non-negative.");
        }

        SurfaceConstruction = AuthoringValueSupport.CopyOptional(surfaceConstruction);
        OpeningDefinition[] openingArray = AuthoringValueSupport.CopyOpenings(openings);
        SupplySystem[] supplyArray = AuthoringValueSupport.CopyCoreValues(supplySystems, nameof(supplySystems));
        VentilationAssignment[] ventilationArray = AuthoringValueSupport.CopyCoreValues(
            ventilationAssignments,
            nameof(ventilationAssignments));
        AuthoringValueSupport.EnsureUnique(
            openingArray.Where(item => item.Id is not null).Select(item => item.Id!.Value),
            "opening",
            nameof(openings));
        AuthoringValueSupport.EnsureUnique(
            supplyArray.Select(item => item.Id.Value),
            "supply-system",
            nameof(supplySystems));
        AuthoringValueSupport.EnsureUnique(
            ventilationArray.Select(item => item.VentilationSystemId),
            "ventilation-system",
            nameof(ventilationAssignments));
        int radiantCount = supplyArray.Count(item => item.Type == SupplySystemType.RadiantFloor
            || item.Type == SupplySystemType.ElectricRadiantFloor);
        if (radiantCount > 1)
        {
            throw new ArgumentException(
                "A zone cannot use more than one radiant-floor system.",
                nameof(supplySystems));
        }

        foreach (OpeningDefinition opening in openingArray)
        {
            AuthoringValueSupport.ValidateFenestrationConstruction(
                opening.Type,
                opening.Construction,
                opening.Blind);
        }

        _geometryArchive = RhinoGeometryArchive.Encode(geometryCopy);
        FloorNumber = floorNumber;
        UnmatchedFloorBoundary = unmatchedFloorBoundary;
        LightDensity = lightDensity;
        Openings = Array.AsReadOnly(openingArray);
        SupplySystems = Array.AsReadOnly(supplyArray);
        VentilationAssignments = Array.AsReadOnly(ventilationArray);
    }

    public Brep Geometry => RhinoGeometryArchive.Decode<Brep>(_geometryArchive);

    public string Name { get; }

    public int FloorNumber { get; }

    public UsageProfile Profile { get; }

    public SurfaceConstruction? SurfaceConstruction { get; }

    public SurfaceBoundaryCondition UnmatchedFloorBoundary { get; }

    public double? LightDensity { get; }

    public IReadOnlyList<OpeningDefinition> Openings { get; }

    public IReadOnlyList<SupplySystem> SupplySystems { get; }

    public IReadOnlyList<VentilationAssignment> VentilationAssignments { get; }

    internal byte[] GeometryArchive => (byte[])_geometryArchive.Clone();

    internal ZoneDefinition Duplicate()
    {
        using Brep geometry = Geometry;
        return new ZoneDefinition(
            geometry,
            Name,
            FloorNumber,
            Profile,
            SurfaceConstruction,
            UnmatchedFloorBoundary,
            LightDensity,
            Openings,
            SupplySystems,
            VentilationAssignments);
    }
}

internal static class RhinoGeometryArchive
{
    internal static byte[] Encode(CommonObject geometry)
    {
        string archive = geometry.ToJSON(new SerializationOptions
        {
            RhinoVersion = 7,
            WriteUserData = true,
        });
        if (string.IsNullOrWhiteSpace(archive))
        {
            throw new InvalidOperationException(
                "Rhino could not serialize the geometry to an OpenNURBS archive.");
        }

        return Encoding.UTF8.GetBytes(archive);
    }

    internal static T Decode<T>(byte[] archive)
        where T : CommonObject
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(archive);
#else
        if (archive is null)
        {
            throw new ArgumentNullException(nameof(archive));
        }
#endif

        if (archive.Length == 0)
        {
            throw new ArgumentException("A Rhino geometry archive cannot be empty.", nameof(archive));
        }

        using JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetString(archive));
        JsonElement root = document.RootElement;
        int archive3dm = root.GetProperty("archive3dm").GetInt32();
        int openNurbs = root.GetProperty("opennurbs").GetInt32();
        string data = root.GetProperty("data").GetString()
            ?? throw new InvalidDataException("The Rhino geometry archive contains no data.");
        CommonObject? geometry = CommonObject.FromBase64String(archive3dm, openNurbs, data);
        if (geometry is T typed)
        {
            return typed;
        }

        geometry?.Dispose();
        throw new InvalidDataException(
            "Rhino could not deserialize the expected " + typeof(T).Name + " geometry.");
    }
}

internal static class AuthoringValueSupport
{
    internal static string RequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    internal static T Copy<T>(T value)
        where T : class
    {
        return SimpleDragonGooSnapshot.Deserialize<T>(SimpleDragonGooSnapshot.Serialize(value));
    }

    internal static T? CopyOptional<T>(T? value)
        where T : class
    {
        return value is null ? null : Copy(value);
    }

    internal static OpeningDefinition[] CopyOpenings(IEnumerable<OpeningDefinition>? values)
    {
        if (values is null)
        {
            return Array.Empty<OpeningDefinition>();
        }

        OpeningDefinition[] array = values.ToArray();
        if (array.Any(item => item is null))
        {
            throw new ArgumentException("An opening definition cannot be null.", nameof(values));
        }

        return array.Select(item => item.Duplicate()).ToArray();
    }

    internal static T[] CopyCoreValues<T>(IEnumerable<T>? values, string parameterName)
        where T : class
    {
        if (values is null)
        {
            return Array.Empty<T>();
        }

        T[] array = values.ToArray();
        if (array.Any(item => item is null))
        {
            throw new ArgumentException("An item cannot be null.", parameterName);
        }

        return array.Select(Copy).ToArray();
    }

    internal static void EnsureUnique(
        IEnumerable<string> identifiers,
        string description,
        string parameterName)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (string identifier in identifiers)
        {
            if (!known.Add(identifier))
            {
                throw new ArgumentException(
                    "Duplicate " + description + " ID '" + identifier + "'.",
                    parameterName);
            }
        }
    }

    internal static void ValidateFenestrationConstruction(
        FenestrationType type,
        FenestrationConstruction? construction,
        BlindType? blind)
    {
        if (construction is not null
            && type == FenestrationType.Door
            && construction.IsTransparent)
        {
            throw new ArgumentException("A door requires an opaque fenestration construction.", nameof(construction));
        }

        if (construction is not null
            && type != FenestrationType.Door
            && !construction.IsTransparent)
        {
            throw new ArgumentException(
                "A window or glass door requires a transparent construction.",
                nameof(construction));
        }

        if (blind.HasValue && type == FenestrationType.Door)
        {
            throw new ArgumentException("An opaque door cannot have a window blind.", nameof(blind));
        }
    }
}
