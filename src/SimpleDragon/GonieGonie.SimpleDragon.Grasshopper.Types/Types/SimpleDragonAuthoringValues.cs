using System.Text;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Runtime;

namespace GonieGonie.SimpleDragon.Grasshopper.Types;

/// <summary>
/// Immutable, geometry-backed opening input used while authoring a SimpleDragon surface.
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
/// Immutable, geometry-backed surface input. A surface owns its construction,
/// boundary intent, and openings before it is connected to exactly one zone.
/// </summary>
public sealed class SurfaceDefinition
{
    private readonly byte[] _geometryArchive;

    public SurfaceDefinition(
        Brep geometry,
        string name,
        SurfaceType type,
        SurfaceBoundaryCondition boundaryCondition,
        SurfaceConstruction? construction = null,
        IEnumerable<OpeningDefinition>? openings = null,
        double? coolRoofReflectance = null,
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

        using Brep geometryCopy = geometry.DuplicateBrep()
            ?? throw new ArgumentException("The surface Brep could not be duplicated.", nameof(geometry));
        if (!geometryCopy.IsValid || geometryCopy.Faces.Count != 1)
        {
            throw new ArgumentException("A surface requires one valid Brep face.", nameof(geometry));
        }

        if (!geometryCopy.Faces[0].TryGetPlane(out _))
        {
            throw new ArgumentException("A surface Brep face must be planar.", nameof(geometry));
        }

        Name = AuthoringValueSupport.RequiredText(name, nameof(name));
        if (!Enum.IsDefined(typeof(SurfaceType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown surface type.");
        }

        if (boundaryCondition != SurfaceBoundaryCondition.Outdoors
            && boundaryCondition != SurfaceBoundaryCondition.Ground
            && boundaryCondition != SurfaceBoundaryCondition.Adiabatic)
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundaryCondition),
                boundaryCondition,
                "A surface boundary intent must be Outdoors, Ground, or Adiabatic.");
        }

        OpeningDefinition[] openingArray = AuthoringValueSupport.CopyOpenings(openings);
        AuthoringValueSupport.EnsureUnique(
            openingArray.Where(item => item.Id is not null).Select(item => item.Id!.Value),
            "opening",
            nameof(openings));
        if ((boundaryCondition == SurfaceBoundaryCondition.Ground
                || boundaryCondition == SurfaceBoundaryCondition.Adiabatic)
            && openingArray.Length > 0)
        {
            throw new ArgumentException(
                "Ground and adiabatic surfaces cannot own openings.",
                nameof(openings));
        }

        if (coolRoofReflectance.HasValue
            && (double.IsNaN(coolRoofReflectance.Value)
                || double.IsInfinity(coolRoofReflectance.Value)
                || coolRoofReflectance.Value <= 0d
                || coolRoofReflectance.Value > 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(coolRoofReflectance),
                coolRoofReflectance,
                "Cool-roof reflectance must be finite and in (0, 1].");
        }

        if (coolRoofReflectance.HasValue
            && (type != SurfaceType.Ceiling
                || boundaryCondition != SurfaceBoundaryCondition.Outdoors))
        {
            throw new ArgumentException(
                "Cool-roof reflectance is only valid on an outdoor ceiling.",
                nameof(coolRoofReflectance));
        }

        _geometryArchive = RhinoGeometryArchive.Encode(geometryCopy);
        Type = type;
        BoundaryCondition = boundaryCondition;
        Construction = AuthoringValueSupport.CopyOptional(construction);
        Openings = Array.AsReadOnly(openingArray);
        CoolRoofReflectance = coolRoofReflectance;
        Id = id;
    }

    public Brep Geometry => RhinoGeometryArchive.Decode<Brep>(_geometryArchive);

    public string Name { get; }

    public SurfaceType Type { get; }

    public SurfaceBoundaryCondition BoundaryCondition { get; }

    public SurfaceConstruction? Construction { get; }

    public IReadOnlyList<OpeningDefinition> Openings { get; }

    public double? CoolRoofReflectance { get; }

    public EntityId? Id { get; }

    internal byte[] GeometryArchive => (byte[])_geometryArchive.Clone();

    internal SurfaceDefinition Duplicate()
    {
        using Brep geometry = Geometry;
        return new SurfaceDefinition(
            geometry,
            Name,
            Type,
            BoundaryCondition,
            Construction,
            Openings,
            CoolRoofReflectance,
            Id);
    }
}

/// <summary>
/// Immutable zone input composed from explicitly authored surfaces. A collection-level
/// resolver converts all zone definitions together so inter-zone adjacency can be determined.
/// </summary>
public sealed class ZoneDefinition
{
    public ZoneDefinition(
        string name,
        int floorNumber,
        double height,
        IEnumerable<SurfaceDefinition> surfaces,
        UsageProfile profile,
        double? lightDensity = 10d,
        IEnumerable<SupplySystem>? supplySystems = null,
        IEnumerable<VentilationAssignment>? ventilationAssignments = null,
        EntityId? id = null)
    {
        Name = AuthoringValueSupport.RequiredText(name, nameof(name));
        if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Zone height must be finite and positive.");
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

        SurfaceDefinition[] surfaceArray = AuthoringValueSupport.CopySurfaces(surfaces);
        if (surfaceArray.Length == 0)
        {
            throw new ArgumentException("A zone requires at least one surface.", nameof(surfaces));
        }

        SupplySystem[] supplyArray = AuthoringValueSupport.CopyCoreValues(supplySystems, nameof(supplySystems));
        VentilationAssignment[] ventilationArray = AuthoringValueSupport.CopyCoreValues(
            ventilationAssignments,
            nameof(ventilationAssignments));
        AuthoringValueSupport.EnsureUnique(
            surfaceArray.Where(item => item.Id is not null).Select(item => item.Id!.Value),
            "surface",
            nameof(surfaces));
        AuthoringValueSupport.EnsureUnique(
            surfaceArray.SelectMany(item => item.Openings)
                .Where(item => item.Id is not null)
                .Select(item => item.Id!.Value),
            "opening",
            nameof(surfaces));
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

        FloorNumber = floorNumber;
        Height = height;
        Surfaces = Array.AsReadOnly(surfaceArray);
        Profile = AuthoringValueSupport.Copy(profile ?? throw new ArgumentNullException(nameof(profile)));
        LightDensity = lightDensity;
        SupplySystems = Array.AsReadOnly(supplyArray);
        VentilationAssignments = Array.AsReadOnly(ventilationArray);
        Id = id;
    }

    public string Name { get; }

    public int FloorNumber { get; }

    public double Height { get; }

    public IReadOnlyList<SurfaceDefinition> Surfaces { get; }

    public UsageProfile Profile { get; }

    public double? LightDensity { get; }

    public IReadOnlyList<SupplySystem> SupplySystems { get; }

    public IReadOnlyList<VentilationAssignment> VentilationAssignments { get; }

    public EntityId? Id { get; }

    internal ZoneDefinition Duplicate()
    {
        return new ZoneDefinition(
            Name,
            FloorNumber,
            Height,
            Surfaces,
            Profile,
            LightDensity,
            SupplySystems,
            VentilationAssignments,
            Id);
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

    internal static SurfaceDefinition[] CopySurfaces(IEnumerable<SurfaceDefinition> values)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
#else
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }
#endif

        SurfaceDefinition[] array = values.ToArray();
        if (array.Any(item => item is null))
        {
            throw new ArgumentException("A surface definition cannot be null.", nameof(values));
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
