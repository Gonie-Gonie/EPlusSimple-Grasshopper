using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Rhino;
using Rhino.Geometry;
using DragonPolygon = Dragons.InvisibleDragon.Shape.PlanarPolygon;
using SimpleSurface = Dragons.SimpleDragon.Surface;
using SimpleZone = Dragons.SimpleDragon.Zone;

namespace Dragons.SimpleDragon.Rhino;

public enum RhinoMappedGeometryKind
{
    Zone,
    Surface,
    Fenestration,
}

/// <summary>
/// Relates a SimpleDragon entity to the Surface -&gt; Zone authoring geometry that produced it.
/// </summary>
public sealed class RhinoDomainGeometryMapEntry
{
    public RhinoDomainGeometryMapEntry(
        EntityId entityId,
        RhinoMappedGeometryKind kind,
        int zoneIndex,
        int? surfaceIndex,
        int? openingIndex,
        int? trimLoopIndex,
        GeometryProvenance provenance)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        if (!Enum.IsDefined(typeof(RhinoMappedGeometryKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown geometry-map kind.");
        }

        RhinoZoneExtractor.RequireNonNegative(zoneIndex, nameof(zoneIndex));
        RhinoZoneExtractor.RequireNullableNonNegative(surfaceIndex, nameof(surfaceIndex));
        RhinoZoneExtractor.RequireNullableNonNegative(openingIndex, nameof(openingIndex));
        RhinoZoneExtractor.RequireNullableNonNegative(trimLoopIndex, nameof(trimLoopIndex));

        bool isZone = kind == RhinoMappedGeometryKind.Zone;
        bool isOpening = kind == RhinoMappedGeometryKind.Fenestration;
        if (isZone && (surfaceIndex.HasValue || openingIndex.HasValue || trimLoopIndex.HasValue))
        {
            throw new ArgumentException("A zone mapping cannot identify a surface, opening, or trim loop.");
        }

        if (!isZone && !surfaceIndex.HasValue)
        {
            throw new ArgumentException("A surface or fenestration mapping requires a surface index.");
        }

        if (!isOpening && (openingIndex.HasValue || trimLoopIndex.HasValue))
        {
            throw new ArgumentException("Only a fenestration mapping can identify an opening or trim loop.");
        }

        if (isOpening && !openingIndex.HasValue)
        {
            throw new ArgumentException("A fenestration mapping requires an explicit opening index.");
        }

        Kind = kind;
        ZoneIndex = zoneIndex;
        SurfaceIndex = surfaceIndex;
        OpeningIndex = openingIndex;
        TrimLoopIndex = trimLoopIndex;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public EntityId EntityId { get; }

    public RhinoMappedGeometryKind Kind { get; }

    public int ZoneIndex { get; }

    public int? SurfaceIndex { get; }

    /// <summary>Gets the index in <see cref="RhinoSurfaceSource.Fenestrations"/>.</summary>
    public int? OpeningIndex { get; }

    /// <summary>Gets the one-face Brep's original inner-loop index.</summary>
    public int? TrimLoopIndex { get; }

    public GeometryProvenance Provenance { get; }
}

/// <summary>
/// A closed polygonal opening owned by exactly one <see cref="RhinoSurfaceSource"/>.
/// The caller retains ownership of <see cref="Boundary"/>.
/// </summary>
public sealed class RhinoFenestrationSource
{
    public RhinoFenestrationSource(
        Curve boundary,
        string name,
        FenestrationType type,
        FenestrationConstruction construction,
        BlindType? blind = null,
        EntityId? id = null,
        Guid? rhinoObjectId = null,
        string? grasshopperPath = null,
        int? grasshopperIndex = null)
    {
        Boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        if (!boundary.IsValid || !boundary.IsClosed)
        {
            throw new ArgumentException("A valid closed opening curve is required.", nameof(boundary));
        }

        Name = RequiredText(name, nameof(name));
        if (!Enum.IsDefined(typeof(FenestrationType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown fenestration type.");
        }

        Construction = construction ?? throw new ArgumentNullException(
            nameof(construction),
            "An opening must own its fenestration construction.");
        if (type == FenestrationType.Door && construction.IsTransparent)
        {
            throw new ArgumentException("A door requires an opaque construction.", nameof(construction));
        }

        if (type != FenestrationType.Door && !construction.IsTransparent)
        {
            throw new ArgumentException("A window or glass door requires a transparent construction.", nameof(construction));
        }

        if (blind.HasValue && !Enum.IsDefined(typeof(BlindType), blind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(blind), blind, "Unknown blind type.");
        }

        if (blind.HasValue && type == FenestrationType.Door)
        {
            throw new ArgumentException("An opaque door cannot have a blind.", nameof(blind));
        }

        ValidateProvenance(rhinoObjectId, grasshopperIndex);
        Type = type;
        Blind = blind;
        Id = id;
        RhinoObjectId = rhinoObjectId;
        GrasshopperPath = OptionalText(grasshopperPath);
        GrasshopperIndex = grasshopperIndex;
    }

    public Curve Boundary { get; }

    public string Name { get; }

    public FenestrationType Type { get; }

    public FenestrationConstruction Construction { get; }

    public BlindType? Blind { get; }

    public EntityId? Id { get; }

    public Guid? RhinoObjectId { get; }

    public string? GrasshopperPath { get; }

    public int? GrasshopperIndex { get; }

    private static string RequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static void ValidateProvenance(Guid? rhinoObjectId, int? grasshopperIndex)
    {
        if (rhinoObjectId == Guid.Empty)
        {
            throw new ArgumentException("A Rhino object identifier must not be empty.", nameof(rhinoObjectId));
        }

        RhinoZoneExtractor.RequireNullableNonNegative(grasshopperIndex, nameof(grasshopperIndex));
    }
}

/// <summary>
/// One explicit building surface. Geometry must be a valid, planar, polygonal one-face Brep.
/// </summary>
public sealed class RhinoSurfaceSource
{
    public RhinoSurfaceSource(
        Brep geometry,
        string name,
        SurfaceType type,
        SurfaceBoundaryCondition boundaryCondition,
        SurfaceConstruction? construction = null,
        IEnumerable<RhinoFenestrationSource>? fenestrations = null,
        double? coolRoofReflectance = null,
        EntityId? surfaceId = null,
        Guid? rhinoObjectId = null,
        string? grasshopperPath = null,
        int? grasshopperIndex = null)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        Name = RequiredText(name, nameof(name));
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
                "Boundary intent must be Outdoors, Ground, or Adiabatic.");
        }

        RhinoFenestrationSource[] openingArray = fenestrations?.ToArray()
            ?? Array.Empty<RhinoFenestrationSource>();
        if (openingArray.Any(item => item is null))
        {
            throw new ArgumentException("A fenestration source cannot be null.", nameof(fenestrations));
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
            && (type != SurfaceType.Ceiling || boundaryCondition != SurfaceBoundaryCondition.Outdoors))
        {
            throw new ArgumentException(
                "Cool-roof reflectance is only valid on an outdoor ceiling.",
                nameof(coolRoofReflectance));
        }

        ValidateProvenance(rhinoObjectId, grasshopperIndex);
        Type = type;
        BoundaryCondition = boundaryCondition;
        Construction = construction;
        Fenestrations = new ReadOnlyCollection<RhinoFenestrationSource>(openingArray);
        CoolRoofReflectance = coolRoofReflectance;
        SurfaceId = surfaceId;
        RhinoObjectId = rhinoObjectId;
        GrasshopperPath = OptionalText(grasshopperPath);
        GrasshopperIndex = grasshopperIndex;
    }

    public Brep Geometry { get; }

    public string Name { get; }

    public SurfaceType Type { get; }

    public SurfaceBoundaryCondition BoundaryCondition { get; }

    public SurfaceConstruction? Construction { get; }

    public IReadOnlyList<RhinoFenestrationSource> Fenestrations { get; }

    public double? CoolRoofReflectance { get; }

    public EntityId? SurfaceId { get; }

    public Guid? RhinoObjectId { get; }

    public string? GrasshopperPath { get; }

    public int? GrasshopperIndex { get; }

    private static string RequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A surface name is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static void ValidateProvenance(Guid? rhinoObjectId, int? grasshopperIndex)
    {
        if (rhinoObjectId == Guid.Empty)
        {
            throw new ArgumentException("A Rhino object identifier must not be empty.", nameof(rhinoObjectId));
        }

        RhinoZoneExtractor.RequireNullableNonNegative(grasshopperIndex, nameof(grasshopperIndex));
    }
}

/// <summary>A zone composed from already-authored surfaces; it owns no Brep.</summary>
public sealed class RhinoZoneSource
{
    public RhinoZoneSource(
        string name,
        int floorNumber,
        double height,
        UsageProfile profile,
        IEnumerable<RhinoSurfaceSource> surfaces,
        double? lightDensity = null,
        EntityId? zoneId = null,
        Guid? rhinoObjectId = null,
        string? grasshopperPath = null,
        int? grasshopperIndex = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A zone name is required.", nameof(name));
        }

        if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Zone height must be finite and positive.");
        }

        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (lightDensity.HasValue
            && (double.IsNaN(lightDensity.Value)
                || double.IsInfinity(lightDensity.Value)
                || lightDensity.Value < 0d))
        {
            throw new ArgumentOutOfRangeException(nameof(lightDensity));
        }

        RhinoSurfaceSource[] surfaceArray = (surfaces ?? throw new ArgumentNullException(nameof(surfaces))).ToArray();
        if (surfaceArray.Length == 0 || surfaceArray.Any(item => item is null))
        {
            throw new ArgumentException("A zone requires one or more non-null surfaces.", nameof(surfaces));
        }

        if (rhinoObjectId == Guid.Empty)
        {
            throw new ArgumentException("A Rhino object identifier must not be empty.", nameof(rhinoObjectId));
        }

        RhinoZoneExtractor.RequireNullableNonNegative(grasshopperIndex, nameof(grasshopperIndex));
        Name = name.Trim();
        FloorNumber = floorNumber;
        Height = height;
        Surfaces = new ReadOnlyCollection<RhinoSurfaceSource>(surfaceArray);
        LightDensity = lightDensity;
        ZoneId = zoneId;
        RhinoObjectId = rhinoObjectId;
        GrasshopperPath = string.IsNullOrWhiteSpace(grasshopperPath) ? null : grasshopperPath!.Trim();
        GrasshopperIndex = grasshopperIndex;
    }

    public string Name { get; }

    public int FloorNumber { get; }

    /// <summary>Gets the explicit zone height in metres.</summary>
    public double Height { get; }

    public UsageProfile Profile { get; }

    public IReadOnlyList<RhinoSurfaceSource> Surfaces { get; }

    public double? LightDensity { get; }

    public EntityId? ZoneId { get; }

    public Guid? RhinoObjectId { get; }

    public string? GrasshopperPath { get; }

    public int? GrasshopperIndex { get; }
}

public sealed class RhinoZoneExtractionResult
{
    internal RhinoZoneExtractionResult(
        IEnumerable<SimpleZone> zones,
        IEnumerable<RhinoDomainGeometryMapEntry> geometryMap,
        IEnumerable<Diagnostic> diagnostics)
    {
        SimpleZone[] zoneArray = zones.ToArray();
        Zones = new ReadOnlyCollection<SimpleZone>(zoneArray);
        Floors = new ReadOnlyCollection<BuildingFloor>(zoneArray
            .GroupBy(zone => zone.FloorNumber)
            .OrderBy(group => group.Key)
            .Select(group => new BuildingFloor(group.Key, group))
            .ToArray());
        GeometryMap = new ReadOnlyCollection<RhinoDomainGeometryMapEntry>(geometryMap.ToArray());
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics.ToArray());
    }

    public IReadOnlyList<SimpleZone> Zones { get; }

    public IReadOnlyList<BuildingFloor> Floors { get; }

    public IReadOnlyList<RhinoDomainGeometryMapEntry> GeometryMap { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Zones.Count > 0 && Diagnostics.All(item => !item.IsFailure);

    public IReadOnlyList<SimpleZone> RequireZones()
    {
        if (!Success)
        {
            Diagnostic? first = Diagnostics.FirstOrDefault(item => item.IsFailure);
            throw new InvalidOperationException(first is null
                ? "No SimpleDragon zone was extracted."
                : first.Code + ": " + first.Message);
        }

        return Zones;
    }
}

/// <summary>
/// Converts an explicit Surface -&gt; Zone graph into the Rhino-free SimpleDragon domain.
/// Geometry never supplies implicit surface types, boundaries, constructions, or zone height.
/// </summary>
public static class RhinoZoneExtractor
{
    public static RhinoZoneExtractionResult Extract(
        IEnumerable<RhinoZoneSource> sources,
        RhinoGeometryContext context)
    {
        RequireNotNull(sources, nameof(sources));
        RequireNotNull(context, nameof(context));
        RhinoZoneSource[] sourceArray = sources.ToArray();
        if (sourceArray.Any(item => item is null))
        {
            throw new ArgumentException("A Rhino zone source cannot be null.", nameof(sources));
        }

        var diagnostics = new List<Diagnostic>();
        var zones = new List<ZoneWork>();
        for (int zoneIndex = 0; zoneIndex < sourceArray.Length; zoneIndex++)
        {
            ZoneWork? zone = PrepareZone(sourceArray[zoneIndex], zoneIndex, context, diagnostics);
            if (zone is not null)
            {
                zones.Add(zone);
            }
        }

        ReportDuplicateIds(zones, diagnostics);
        ResolveAdjacency(zones, context, diagnostics);
        ReportUnannotatedTrimLoops(zones, diagnostics);

        var domainZones = new List<SimpleZone>();
        var geometryMap = new List<RhinoDomainGeometryMapEntry>();
        var usedOpeningIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ZoneWork zone in zones)
        {
            CreateZone(zone, domainZones, geometryMap, usedOpeningIds, diagnostics);
        }

        return new RhinoZoneExtractionResult(domainZones, geometryMap, diagnostics);
    }

    private static ZoneWork? PrepareZone(
        RhinoZoneSource source,
        int zoneIndex,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        var surfaces = new List<SurfaceWork>();
        bool failed = false;
        for (int surfaceIndex = 0; surfaceIndex < source.Surfaces.Count; surfaceIndex++)
        {
            SurfaceWork? surface = PrepareSurface(
                source,
                source.Surfaces[surfaceIndex],
                zoneIndex,
                surfaceIndex,
                context,
                diagnostics);
            if (surface is null)
            {
                failed = true;
            }
            else
            {
                surfaces.Add(surface);
            }
        }

        if (failed || surfaces.Count == 0)
        {
            diagnostics.Add(Error(
                "SD.RHINO.ZONE_SURFACE_SET_INVALID",
                "The zone was rejected because at least one authored surface is invalid.",
                source.ZoneId,
                ZoneProvenance(source, HashFingerprint(new[] { source.Name })),
                "Correct every surface error; partial zone geometry is never accepted."));
            return null;
        }

        string fingerprint = HashFingerprint(surfaces.Select(item => item.Extraction.GeometryFingerprint));
        EntityId zoneId = source.ZoneId ?? new EntityId("ZONE-RHINO-" + fingerprint.Remove(24));
        foreach (SurfaceWork surface in surfaces)
        {
            surface.SurfaceId = surface.Source.SurfaceId ?? new EntityId(
                "SURF-" + zoneId.Value + "-S" + surface.SurfaceIndex.ToString("D4", CultureInfo.InvariantCulture));
        }

        return new ZoneWork(source, zoneIndex, zoneId, fingerprint, surfaces);
    }

    private static SurfaceWork? PrepareSurface(
        RhinoZoneSource zone,
        RhinoSurfaceSource source,
        int zoneIndex,
        int surfaceIndex,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        GeometryProvenance fallback = SurfaceProvenance(
            source,
            "unavailable-" + zoneIndex.ToString("D4", CultureInfo.InvariantCulture)
                + "-" + surfaceIndex.ToString("D4", CultureInfo.InvariantCulture));
        if (!source.Geometry.IsValid || source.Geometry.Faces.Count != 1)
        {
            diagnostics.Add(Error(
                "SD.RHINO.SURFACE_REQUIRES_ONE_FACE",
                "Each authored surface must contain exactly one valid Brep face.",
                source.SurfaceId,
                fallback,
                "Explode the building geometry and connect one planar face to each SD Surface."));
            return null;
        }

        RhinoPolygonExtraction extraction;
        try
        {
            extraction = RhinoPolygonConverter.FromBrepFace(source.Geometry.Faces[0], context);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException
            || exception is NotSupportedException)
        {
            diagnostics.Add(Error(
                "SD.RHINO.SURFACE_UNSUPPORTED",
                "The authored surface is not a planar polygonal face: " + exception.Message,
                source.SurfaceId,
                fallback,
                "Use one planar Brep face with straight outer and inner trim segments."));
            return null;
        }

        GeometryProvenance provenance = SurfaceProvenance(source, extraction.GeometryFingerprint);
        SurfaceType geometricType = Classify(extraction.OuterLoop, context, provenance, diagnostics);
        if (geometricType != source.Type)
        {
            diagnostics.Add(Error(
                "SD.RHINO.SURFACE_TYPE_CONFLICT",
                "The explicit surface type does not agree with the face orientation.",
                source.SurfaceId,
                provenance,
                "Reverse the surface normal or select the matching Wall, Floor, or Ceiling type."));
            return null;
        }

        var result = new SurfaceWork(source, zoneIndex, surfaceIndex, extraction);
        int[] innerLoopIndices = source.Geometry.Faces[0].Loops
            .Where(loop => loop.LoopType == BrepLoopType.Inner)
            .Select(loop => loop.LoopIndex)
            .ToArray();
        if (innerLoopIndices.Length != extraction.InnerLoops.Count)
        {
            diagnostics.Add(Error(
                "SD.RHINO.TRIM_TOPOLOGY_INVALID",
                "Rhino returned inconsistent inner-loop topology for the surface.",
                source.SurfaceId,
                provenance,
                "Rebuild the planar Brep face before authoring openings."));
            return null;
        }

        for (int index = 0; index < extraction.InnerLoops.Count; index++)
        {
            result.Openings.Add(OpeningWork.Placeholder(
                zone.Name + ":" + source.Name + ":Trim:" + innerLoopIndices[index].ToString(CultureInfo.InvariantCulture),
                extraction.InnerLoops[index],
                zoneIndex,
                surfaceIndex,
                innerLoopIndices[index],
                source));
        }

        AddExplicitOpenings(zone, result, context, diagnostics);
        if ((source.BoundaryCondition == SurfaceBoundaryCondition.Ground
                || source.BoundaryCondition == SurfaceBoundaryCondition.Adiabatic)
            && result.Openings.Count > 0)
        {
            diagnostics.Add(Error(
                "SD.RHINO.OPENING_BOUNDARY_INVALID",
                "A ground or adiabatic surface cannot contain openings.",
                source.SurfaceId,
                provenance,
                "Remove the openings or change the explicit boundary intent to Outdoors."));
        }

        return result;
    }

    private static void AddExplicitOpenings(
        RhinoZoneSource zone,
        SurfaceWork surface,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        BrepFace face = surface.Source.Geometry.Faces[0];
        for (int openingIndex = 0; openingIndex < surface.Source.Fenestrations.Count; openingIndex++)
        {
            RhinoFenestrationSource source = surface.Source.Fenestrations[openingIndex];
            GeometryProvenance fallback = OpeningProvenance(
                zone,
                surface.Source,
                source,
                "unavailable-opening-" + surface.ZoneIndex.ToString("D4", CultureInfo.InvariantCulture)
                    + "-" + surface.SurfaceIndex.ToString("D4", CultureInfo.InvariantCulture)
                    + "-" + openingIndex.ToString("D4", CultureInfo.InvariantCulture));
            try
            {
                if (!face.TryGetPlane(out Plane hostPlane, context.SourceAbsoluteTolerance)
                    || !source.Boundary.TryGetPlane(
                        out Plane openingPlane,
                        context.SourceAbsoluteTolerance)
                    || Math.Abs(hostPlane.DistanceTo(openingPlane.Origin))
                        > context.SourceAbsoluteTolerance
                    || Math.Abs(hostPlane.Normal * openingPlane.Normal)
                        < Math.Cos(context.AngleToleranceRadians))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.OPENING_NOT_COPLANAR",
                        "An opening curve must lie on its owning surface plane within document tolerance.",
                        source.Id,
                        fallback,
                        "Move the opening onto the surface plane; distant curves are never projected implicitly."));
                    continue;
                }

                using Curve projected = RhinoPolygonConverter.ProjectOpeningToFacePlane(
                    source.Boundary,
                    face,
                    context);
                DragonPolygon polygon = RhinoPolygonConverter.FromClosedCurve(projected, context);
                GeometryProvenance provenance = OpeningProvenance(
                    zone,
                    surface.Source,
                    source,
                    RhinoGeometryFingerprint.ForPolygon(polygon));
                if (!surface.Extraction.OuterLoop.Contains(polygon, context.ModelToleranceMetres))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.OPENING_OUTSIDE_HOST",
                        "An opening is not completely contained by its owning surface.",
                        source.Id,
                        provenance,
                        "Move or resize the opening inside the surface outer boundary."));
                    continue;
                }

                int equivalent = surface.Openings.FindIndex(item => item.Polygon.IsGeometricallyEquivalentTo(
                    polygon,
                    allowReversedWinding: true,
                    tolerance: context.ModelToleranceMetres));
                if (equivalent >= 0)
                {
                    OpeningWork existing = surface.Openings[equivalent];
                    if (!existing.IsAnnotated && existing.TrimLoopIndex.HasValue)
                    {
                        existing.Annotate(source, openingIndex, provenance);
                        diagnostics.Add(new Diagnostic(
                            "SD.RHINO.OPENING_INNER_LOOP_ANNOTATED",
                            DiagnosticSeverity.Info,
                            "An SD Window, SD Door, or SD GlassDoor supplied metadata for its matching surface trim loop.",
                            source.Id,
                            provenance,
                            "The geometry map retains both the opening and trim-loop indices."));
                    }
                    else
                    {
                        diagnostics.Add(Error(
                            "SD.RHINO.OPENING_DUPLICATE",
                            "The same opening geometry was supplied more than once on one surface.",
                            source.Id,
                            provenance,
                            "Keep exactly one completed SD opening component for each physical opening."));
                    }

                    continue;
                }

                if (surface.Openings.Any(item => PolygonsConflict(
                        item.Polygon,
                        polygon,
                        context.ModelToleranceMetres)))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.OPENINGS_OVERLAP",
                        "Opening polygons overlap or nest on the same surface.",
                        source.Id,
                        provenance,
                        "Separate the opening boundaries and do not nest one opening inside another."));
                    continue;
                }

                surface.Openings.Add(OpeningWork.Explicit(
                    source,
                    polygon,
                    surface.ZoneIndex,
                    surface.SurfaceIndex,
                    openingIndex,
                    provenance));
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is NotSupportedException)
            {
                diagnostics.Add(Error(
                    "SD.RHINO.OPENING_UNSUPPORTED",
                    "An opening could not be reduced to a polygon: " + exception.Message,
                    source.Id,
                    fallback,
                    "Use one closed polygonal curve on, or projectable to, the surface plane."));
            }
        }
    }

    private static void ResolveAdjacency(
        IReadOnlyList<ZoneWork> zones,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        SurfaceWork[] surfaces = zones.SelectMany(zone => zone.Surfaces).ToArray();
        var candidates = new List<AdjacencyCandidate>();
        for (int firstIndex = 0; firstIndex < surfaces.Length; firstIndex++)
        {
            SurfaceWork first = surfaces[firstIndex];
            for (int secondIndex = firstIndex + 1; secondIndex < surfaces.Length; secondIndex++)
            {
                SurfaceWork second = surfaces[secondIndex];
                if (!first.Extraction.OuterLoop.IsGeometricallyEquivalentTo(
                        second.Extraction.OuterLoop,
                        allowReversedWinding: true,
                        tolerance: context.ModelToleranceMetres))
                {
                    continue;
                }

                if (first.ZoneIndex == second.ZoneIndex)
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.SURFACE_DUPLICATE_WITHIN_ZONE",
                        "Two surfaces in the same zone have coincident outer boundaries.",
                        first.SurfaceId,
                        SurfaceProvenance(first.Source, first.Extraction.GeometryFingerprint),
                        "Keep one authored surface for each zone boundary."));
                    continue;
                }

                if (first.Source.BoundaryCondition != SurfaceBoundaryCondition.Outdoors
                    || second.Source.BoundaryCondition != SurfaceBoundaryCondition.Outdoors)
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.ADJACENCY_BOUNDARY_CONFLICT",
                        "Coincident surfaces can become inter-zone boundaries only when both intents are Outdoors.",
                        first.SurfaceId,
                        SurfaceProvenance(first.Source, first.Extraction.GeometryFingerprint),
                        "Do not overlap Ground or Adiabatic surfaces; set both shared surfaces to Outdoors."));
                    continue;
                }

                double angleTolerance = Math.Min(context.AngleToleranceRadians, Math.PI / 2d);
                double normalDot = first.Extraction.OuterLoop.Normal.Dot(second.Extraction.OuterLoop.Normal);
                if (normalDot > -Math.Cos(angleTolerance))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.ADJACENCY_NORMALS_NOT_OPPOSED",
                        "Coincident surfaces in different zones must have opposite outward normals.",
                        first.SurfaceId,
                        SurfaceProvenance(first.Source, first.Extraction.GeometryFingerprint),
                        "Reverse one surface normal or remove duplicate, overlapping geometry."));
                    continue;
                }

                if (!TypesCanPair(first.Source.Type, second.Source.Type))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.ADJACENCY_TYPE_CONFLICT",
                        "Coincident surfaces have incompatible explicit surface types.",
                        first.SurfaceId,
                        SurfaceProvenance(first.Source, first.Extraction.GeometryFingerprint),
                        "Pair Wall with Wall, or Floor with Ceiling."));
                    continue;
                }

                candidates.Add(new AdjacencyCandidate(first, second));
            }
        }

        Dictionary<SurfaceWork, int> candidateCounts = candidates
            .SelectMany(candidate => new[] { candidate.First, candidate.Second })
            .GroupBy(surface => surface)
            .ToDictionary(group => group.Key, group => group.Count());
        var reportedAmbiguity = new HashSet<SurfaceWork>();
        foreach (AdjacencyCandidate candidate in candidates)
        {
            if (candidateCounts[candidate.First] > 1 || candidateCounts[candidate.Second] > 1)
            {
                foreach (SurfaceWork ambiguous in new[] { candidate.First, candidate.Second })
                {
                    if (candidateCounts[ambiguous] > 1 && reportedAmbiguity.Add(ambiguous))
                    {
                        diagnostics.Add(Error(
                            "SD.RHINO.ADJACENCY_AMBIGUOUS",
                            "One surface coincides with more than one surface in other zones.",
                            ambiguous.SurfaceId,
                            SurfaceProvenance(ambiguous.Source, ambiguous.Extraction.GeometryFingerprint),
                            "Remove duplicate surfaces so every inter-zone boundary has exactly one counterpart."));
                    }
                }

                continue;
            }

            if (candidate.First.Source.CoolRoofReflectance.HasValue
                || candidate.Second.Source.CoolRoofReflectance.HasValue)
            {
                diagnostics.Add(Error(
                    "SD.RHINO.ADJACENCY_COOL_ROOF_CONFLICT",
                    "A coincident inter-zone surface cannot retain outdoor cool-roof reflectance.",
                    candidate.First.SurfaceId,
                    SurfaceProvenance(candidate.First.Source, candidate.First.Extraction.GeometryFingerprint),
                    "Remove cool-roof reflectance from both shared surfaces."));
                continue;
            }

            if (!ReconcileOpenings(candidate.First, candidate.Second, context, diagnostics))
            {
                continue;
            }

            ZoneWork firstZone = zones.Single(zone => zone.ZoneIndex == candidate.First.ZoneIndex);
            ZoneWork secondZone = zones.Single(zone => zone.ZoneIndex == candidate.Second.ZoneIndex);
            candidate.First.AdjacentZoneId = secondZone.ZoneId;
            candidate.Second.AdjacentZoneId = firstZone.ZoneId;
        }
    }

    private static bool ReconcileOpenings(
        SurfaceWork first,
        SurfaceWork second,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        if (first.Openings.Count == 0 && second.Openings.Count == 0)
        {
            return true;
        }

        if (first.Openings.Count == 0 || second.Openings.Count == 0)
        {
            SurfaceWork source = first.Openings.Count > 0 ? first : second;
            SurfaceWork target = first.Openings.Count == 0 ? first : second;
            if (source.Openings.Any(opening => !opening.IsAnnotated))
            {
                diagnostics.Add(OpeningTopologyError(source));
                return false;
            }

            foreach (OpeningWork opening in source.Openings)
            {
                target.Openings.Add(opening.MirrorTo(target));
            }

            diagnostics.Add(new Diagnostic(
                "SD.RHINO.ADJACENCY_OPENINGS_MIRRORED",
                DiagnosticSeverity.Info,
                "Openings authored on one side of an inter-zone boundary were mirrored to its counterpart.",
                source.SurfaceId,
                SurfaceProvenance(source.Source, source.Extraction.GeometryFingerprint),
                "Author the shared opening once; the reciprocal surface is generated deterministically."));
            return true;
        }

        if (first.Openings.Count != second.Openings.Count)
        {
            diagnostics.Add(OpeningTopologyError(first));
            return false;
        }

        var used = new bool[second.Openings.Count];
        for (int firstOpeningIndex = 0; firstOpeningIndex < first.Openings.Count; firstOpeningIndex++)
        {
            OpeningWork firstOpening = first.Openings[firstOpeningIndex];
            int secondOpeningIndex = -1;
            for (int candidateIndex = 0; candidateIndex < second.Openings.Count; candidateIndex++)
            {
                if (!used[candidateIndex]
                    && firstOpening.Polygon.IsGeometricallyEquivalentTo(
                        second.Openings[candidateIndex].Polygon,
                        allowReversedWinding: true,
                        tolerance: context.ModelToleranceMetres))
                {
                    secondOpeningIndex = candidateIndex;
                    break;
                }
            }

            if (secondOpeningIndex < 0)
            {
                diagnostics.Add(OpeningTopologyError(first));
                return false;
            }

            used[secondOpeningIndex] = true;
            OpeningWork secondOpening = second.Openings[secondOpeningIndex];
            if (firstOpening.IsAnnotated && secondOpening.IsAnnotated)
            {
                if (!OpeningMetadataEquivalent(firstOpening, secondOpening))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.ADJACENCY_OPENING_METADATA_CONFLICT",
                        "Matching openings on both sides of a shared surface have different metadata.",
                        firstOpening.Id,
                        firstOpening.Provenance,
                        "Use the same name, type, construction values, and blind on both sides."));
                    return false;
                }

                if (firstOpening.Id is not null
                    && secondOpening.Id is not null
                    && StringComparer.Ordinal.Equals(firstOpening.Id.Value, secondOpening.Id.Value))
                {
                    secondOpening.AssignPairedId();
                }
            }
            else if (firstOpening.IsAnnotated)
            {
                secondOpening.CopyMetadataFrom(firstOpening, paired: true);
            }
            else if (secondOpening.IsAnnotated)
            {
                firstOpening.CopyMetadataFrom(secondOpening, paired: true);
            }
        }

        return true;
    }

    private static void ReportUnannotatedTrimLoops(
        IEnumerable<ZoneWork> zones,
        List<Diagnostic> diagnostics)
    {
        foreach (SurfaceWork surface in zones.SelectMany(zone => zone.Surfaces))
        {
            foreach (OpeningWork opening in surface.Openings.Where(item => !item.IsAnnotated))
            {
                diagnostics.Add(Error(
                    "SD.RHINO.OPENING_METADATA_REQUIRED",
                    "A surface inner loop has no matching SD Window, SD Door, or SD GlassDoor metadata.",
                    surface.SurfaceId,
                    opening.Provenance,
                    "Connect the matching completed opening with its own fenestration construction to this Surface."));
            }
        }
    }

    private static void ReportDuplicateIds(
        IEnumerable<ZoneWork> zones,
        List<Diagnostic> diagnostics)
    {
        ZoneWork[] zoneArray = zones.ToArray();
        foreach (IGrouping<string, ZoneWork> duplicate in zoneArray
                     .GroupBy(item => item.ZoneId.Value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            ZoneWork item = duplicate.First();
            diagnostics.Add(Error(
                "SD.RHINO.DUPLICATE_ZONE_ID",
                "More than one authored zone resolves to ID '" + duplicate.Key + "'.",
                item.ZoneId,
                ZoneProvenance(item.Source, item.Fingerprint),
                "Give every zone a distinct explicit ID or distinct geometry."));
        }

        foreach (IGrouping<string, SurfaceWork> duplicate in zoneArray
                     .SelectMany(zone => zone.Surfaces)
                     .GroupBy(item => item.SurfaceId!.Value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            SurfaceWork item = duplicate.First();
            diagnostics.Add(Error(
                "SD.RHINO.DUPLICATE_SURFACE_ID",
                "More than one authored surface resolves to ID '" + duplicate.Key + "'.",
                item.SurfaceId,
                SurfaceProvenance(item.Source, item.Extraction.GeometryFingerprint),
                "Give every surface a distinct explicit ID."));
        }
    }

    private static void CreateZone(
        ZoneWork work,
        List<SimpleZone> zones,
        List<RhinoDomainGeometryMapEntry> geometryMap,
        HashSet<string> usedOpeningIds,
        List<Diagnostic> diagnostics)
    {
        var surfaces = new List<SimpleSurface>();
        foreach (SurfaceWork definition in work.Surfaces.OrderBy(item => item.SurfaceIndex))
        {
            SurfaceBoundaryCondition boundary = definition.AdjacentZoneId is null
                ? definition.Source.BoundaryCondition
                : SurfaceBoundaryCondition.Zone;
            double? azimuth = definition.Source.Type == SurfaceType.Wall
                && boundary == SurfaceBoundaryCondition.Outdoors
                    ? Azimuth(definition.Extraction.OuterLoop)
                    : null;
            GeometryProvenance provenance = SurfaceProvenance(
                definition.Source,
                definition.Extraction.GeometryFingerprint);
            ReadOnlyCollection<Fenestration> openings = CreateFenestrations(
                definition,
                usedOpeningIds,
                geometryMap,
                diagnostics);
            try
            {
                SurfaceConstruction? construction = definition.Source.Construction;
                var surface = new SimpleSurface(
                    definition.Source.Name,
                    definition.Source.Type,
                    boundary,
                    definition.Extraction.OuterLoop.Area,
                    azimuth,
                    construction?.Id.Value,
                    construction,
                    openings,
                    definition.Source.CoolRoofReflectance,
                    definition.AdjacentZoneId?.Value,
                    definition.SurfaceId);
                surfaces.Add(surface);
                geometryMap.Add(new RhinoDomainGeometryMapEntry(
                    surface.Id,
                    RhinoMappedGeometryKind.Surface,
                    work.ZoneIndex,
                    definition.SurfaceIndex,
                    null,
                    null,
                    provenance));
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(Error(
                    "SD.RHINO.SURFACE_DOMAIN_INVALID",
                    "The authored surface could not form a SimpleDragon surface: " + exception.Message,
                    definition.SurfaceId,
                    provenance,
                    "Correct the explicit surface metadata."));
            }
        }

        GeometryProvenance zoneProvenance = ZoneProvenance(work.Source, work.Fingerprint);
        if (surfaces.Count != work.Surfaces.Count)
        {
            diagnostics.Add(Error(
                "SD.RHINO.ZONE_HAS_INVALID_SURFACE",
                "The zone was not created because one or more of its surfaces failed domain validation.",
                work.ZoneId,
                zoneProvenance,
                "Correct every surface error; partial zones are never emitted."));
            return;
        }

        try
        {
            var zone = new SimpleZone(
                work.Source.Name,
                work.Source.FloorNumber,
                work.Source.Height,
                surfaces,
                work.Source.Profile.Name,
                work.Source.Profile,
                work.Source.LightDensity,
                id: work.ZoneId);
            zones.Add(zone);
            geometryMap.Add(new RhinoDomainGeometryMapEntry(
                zone.Id,
                RhinoMappedGeometryKind.Zone,
                work.ZoneIndex,
                null,
                null,
                null,
                zoneProvenance));
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(Error(
                "SD.RHINO.ZONE_DOMAIN_INVALID",
                "The authored values could not form a SimpleDragon zone: " + exception.Message,
                work.ZoneId,
                zoneProvenance,
                "Correct the reported zone input."));
        }
    }

    private static ReadOnlyCollection<Fenestration> CreateFenestrations(
        SurfaceWork surface,
        HashSet<string> usedIds,
        List<RhinoDomainGeometryMapEntry> geometryMap,
        List<Diagnostic> diagnostics)
    {
        var result = new List<Fenestration>();
        for (int index = 0; index < surface.Openings.Count; index++)
        {
            OpeningWork definition = surface.Openings[index];
            if (!definition.IsAnnotated)
            {
                continue;
            }

            EntityId id = definition.EffectiveId ?? new EntityId(
                "FNST-" + surface.SurfaceId!.Value + "-O" + index.ToString("D4", CultureInfo.InvariantCulture));
            if (!usedIds.Add(id.Value))
            {
                diagnostics.Add(Error(
                    "SD.RHINO.DUPLICATE_OPENING_ID",
                    "More than one opening resolves to ID '" + id.Value + "'.",
                    id,
                    definition.Provenance,
                    "Give unrelated openings distinct IDs; paired openings are suffixed automatically."));
                continue;
            }

            try
            {
                var opening = new Fenestration(
                    definition.Name,
                    definition.Type!.Value,
                    definition.Polygon.Area,
                    definition.Construction!.Id.Value,
                    definition.Construction,
                    definition.Blind,
                    id);
                result.Add(opening);
                geometryMap.Add(new RhinoDomainGeometryMapEntry(
                    opening.Id,
                    RhinoMappedGeometryKind.Fenestration,
                    definition.OriginZoneIndex,
                    definition.OriginSurfaceIndex,
                    definition.OpeningIndex,
                    definition.TrimLoopIndex,
                    definition.Provenance));
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(Error(
                    "SD.RHINO.OPENING_DOMAIN_INVALID",
                    "The authored opening could not form a SimpleDragon fenestration: " + exception.Message,
                    id,
                    definition.Provenance,
                    "Correct the opening metadata or construction."));
            }
        }

        return new ReadOnlyCollection<Fenestration>(result);
    }

    private static SurfaceType Classify(
        DragonPolygon polygon,
        RhinoGeometryContext context,
        GeometryProvenance provenance,
        List<Diagnostic> diagnostics)
    {
        double absoluteZ = Math.Abs(polygon.Normal.Z);
        SurfaceType type = absoluteZ <= Math.Sqrt(0.5d)
            ? SurfaceType.Wall
            : polygon.Normal.Z > 0d
                ? SurfaceType.Ceiling
                : SurfaceType.Floor;
        double canonicalAngle = Math.Min(context.AngleToleranceRadians, Math.PI / 4d);
        bool canonical = type == SurfaceType.Wall
            ? absoluteZ <= Math.Sin(canonicalAngle)
            : absoluteZ >= Math.Cos(canonicalAngle);
        if (!canonical)
        {
            diagnostics.Add(new Diagnostic(
                "SD.RHINO.SLOPED_SURFACE_EXPLICIT_TYPE",
                DiagnosticSeverity.Warning,
                "A sloped face uses its explicitly authored closest Wall, Floor, or Ceiling type.",
                geometry: provenance,
                suggestedAction: "Inspect the InvisibleDragon conversion preview before simulation."));
        }

        return type;
    }

    private static double Azimuth(DragonPolygon polygon)
    {
        double degrees = Math.Atan2(polygon.Normal.X, polygon.Normal.Y) * 180d / Math.PI;
        return (degrees % 360d + 360d) % 360d;
    }

    private static bool TypesCanPair(SurfaceType first, SurfaceType second) =>
        first == SurfaceType.Wall && second == SurfaceType.Wall
        || first == SurfaceType.Floor && second == SurfaceType.Ceiling
        || first == SurfaceType.Ceiling && second == SurfaceType.Floor;

    private static bool OpeningMetadataEquivalent(OpeningWork first, OpeningWork second)
    {
        FenestrationConstruction firstConstruction = first.Construction!;
        FenestrationConstruction secondConstruction = second.Construction!;
        return StringComparer.Ordinal.Equals(first.Name, second.Name)
            && first.Type == second.Type
            && first.Blind == second.Blind
            && StringComparer.Ordinal.Equals(firstConstruction.Id.Value, secondConstruction.Id.Value)
            && StringComparer.Ordinal.Equals(firstConstruction.Name, secondConstruction.Name)
            && firstConstruction.UValue.Equals(secondConstruction.UValue)
            && Nullable.Equals(
                firstConstruction.SolarHeatGainCoefficient,
                secondConstruction.SolarHeatGainCoefficient);
    }

    private static bool PolygonsConflict(DragonPolygon first, DragonPolygon second, double tolerance) =>
        first.IntersectsInterior(second, tolerance)
        || first.Contains(second, tolerance)
        || second.Contains(first, tolerance);

    private static Diagnostic OpeningTopologyError(SurfaceWork surface) => Error(
        "SD.RHINO.ADJACENCY_OPENINGS_MISMATCH",
        "Coincident inter-zone surfaces have different opening topology.",
        surface.SurfaceId,
        SurfaceProvenance(surface.Source, surface.Extraction.GeometryFingerprint),
        "Author openings on one side only, or provide geometrically and semantically identical openings on both sides.");

    private static GeometryProvenance SurfaceProvenance(
        RhinoSurfaceSource source,
        string fingerprint) => new(
        source.RhinoObjectId,
        0,
        fingerprint,
        source.GrasshopperPath,
        source.GrasshopperIndex);

    private static GeometryProvenance ZoneProvenance(
        RhinoZoneSource source,
        string fingerprint) => new(
        source.RhinoObjectId,
        null,
        fingerprint,
        source.GrasshopperPath,
        source.GrasshopperIndex);

    private static GeometryProvenance OpeningProvenance(
        RhinoZoneSource zone,
        RhinoSurfaceSource surface,
        RhinoFenestrationSource opening,
        string fingerprint)
    {
        bool identifiesIndependentCurve = opening.RhinoObjectId.HasValue;
        return new GeometryProvenance(
            opening.RhinoObjectId ?? surface.RhinoObjectId ?? zone.RhinoObjectId,
            identifiesIndependentCurve ? null : 0,
            fingerprint,
            opening.GrasshopperPath ?? surface.GrasshopperPath ?? zone.GrasshopperPath,
            opening.GrasshopperIndex ?? surface.GrasshopperIndex ?? zone.GrasshopperIndex);
    }

    private static string HashFingerprint(IEnumerable<string> values)
    {
        string canonical = string.Join("\n", values.OrderBy(item => item, StringComparer.Ordinal));
        byte[] bytes = Encoding.UTF8.GetBytes(canonical);
        byte[] hash;
#if NET6_0_OR_GREATER
        hash = SHA256.HashData(bytes);
#else
        using (SHA256 algorithm = SHA256.Create())
        {
            hash = algorithm.ComputeHash(bytes);
        }
#endif
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static Diagnostic Error(
        string code,
        string message,
        EntityId? objectId,
        GeometryProvenance? geometry,
        string action) => new(
        code,
        DiagnosticSeverity.Error,
        message,
        objectId,
        geometry,
        action);

    private static T RequireNotNull<T>(T? value, string parameterName)
        where T : class
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#endif
        return value;
    }

    internal static void RequireNonNegative(int value, string parameterName)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
#else
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
#endif
    }

    internal static void RequireNullableNonNegative(int? value, string parameterName)
    {
        if (value.HasValue)
        {
            RequireNonNegative(value.Value, parameterName);
        }
    }

    private sealed class ZoneWork
    {
        internal ZoneWork(
            RhinoZoneSource source,
            int zoneIndex,
            EntityId zoneId,
            string fingerprint,
            IReadOnlyList<SurfaceWork> surfaces)
        {
            Source = source;
            ZoneIndex = zoneIndex;
            ZoneId = zoneId;
            Fingerprint = fingerprint;
            Surfaces = surfaces;
        }

        internal RhinoZoneSource Source { get; }

        internal int ZoneIndex { get; }

        internal EntityId ZoneId { get; }

        internal string Fingerprint { get; }

        internal IReadOnlyList<SurfaceWork> Surfaces { get; }
    }

    private sealed class SurfaceWork
    {
        internal SurfaceWork(
            RhinoSurfaceSource source,
            int zoneIndex,
            int surfaceIndex,
            RhinoPolygonExtraction extraction)
        {
            Source = source;
            ZoneIndex = zoneIndex;
            SurfaceIndex = surfaceIndex;
            Extraction = extraction;
            Openings = new List<OpeningWork>();
        }

        internal RhinoSurfaceSource Source { get; }

        internal int ZoneIndex { get; }

        internal int SurfaceIndex { get; }

        internal RhinoPolygonExtraction Extraction { get; }

        internal List<OpeningWork> Openings { get; }

        internal EntityId? SurfaceId { get; set; }

        internal EntityId? AdjacentZoneId { get; set; }
    }

    private sealed class OpeningWork
    {
        private OpeningWork(
            string name,
            DragonPolygon polygon,
            int originZoneIndex,
            int originSurfaceIndex,
            int? openingIndex,
            int? trimLoopIndex,
            GeometryProvenance provenance)
        {
            Name = name;
            Polygon = polygon;
            OriginZoneIndex = originZoneIndex;
            OriginSurfaceIndex = originSurfaceIndex;
            OpeningIndex = openingIndex;
            TrimLoopIndex = trimLoopIndex;
            Provenance = provenance;
        }

        internal string Name { get; private set; }

        internal FenestrationType? Type { get; private set; }

        internal FenestrationConstruction? Construction { get; private set; }

        internal BlindType? Blind { get; private set; }

        internal EntityId? Id { get; private set; }

        internal EntityId? EffectiveId { get; private set; }

        internal DragonPolygon Polygon { get; }

        internal int OriginZoneIndex { get; private set; }

        internal int OriginSurfaceIndex { get; private set; }

        internal int? OpeningIndex { get; private set; }

        internal int? TrimLoopIndex { get; private set; }

        internal GeometryProvenance Provenance { get; private set; }

        internal bool IsAnnotated => Type.HasValue && Construction is not null;

        internal static OpeningWork Placeholder(
            string name,
            DragonPolygon polygon,
            int zoneIndex,
            int surfaceIndex,
            int trimLoopIndex,
            RhinoSurfaceSource surface) => new(
            name,
            polygon,
            zoneIndex,
            surfaceIndex,
            null,
            trimLoopIndex,
            SurfaceProvenance(surface, RhinoGeometryFingerprint.ForPolygon(polygon)));

        internal static OpeningWork Explicit(
            RhinoFenestrationSource source,
            DragonPolygon polygon,
            int zoneIndex,
            int surfaceIndex,
            int openingIndex,
            GeometryProvenance provenance)
        {
            var result = new OpeningWork(
                source.Name,
                polygon,
                zoneIndex,
                surfaceIndex,
                openingIndex,
                null,
                provenance);
            result.SetMetadata(source.Name, source.Type, source.Construction, source.Blind, source.Id);
            return result;
        }

        internal void Annotate(
            RhinoFenestrationSource source,
            int openingIndex,
            GeometryProvenance provenance)
        {
            SetMetadata(source.Name, source.Type, source.Construction, source.Blind, source.Id);
            OpeningIndex = openingIndex;
            Provenance = provenance;
        }

        internal OpeningWork MirrorTo(SurfaceWork target)
        {
            var result = new OpeningWork(
                Name,
                Polygon.Reverse(),
                OriginZoneIndex,
                OriginSurfaceIndex,
                OpeningIndex,
                TrimLoopIndex,
                Provenance);
            result.SetMetadata(Name, Type!.Value, Construction!, Blind, Id);
            result.AssignPairedId();
            return result;
        }

        internal void CopyMetadataFrom(OpeningWork source, bool paired)
        {
            SetMetadata(source.Name, source.Type!.Value, source.Construction!, source.Blind, source.Id);
            OriginZoneIndex = source.OriginZoneIndex;
            OriginSurfaceIndex = source.OriginSurfaceIndex;
            OpeningIndex = source.OpeningIndex;
            TrimLoopIndex = source.TrimLoopIndex;
            Provenance = source.Provenance;
            if (paired)
            {
                AssignPairedId();
            }
        }

        internal void AssignPairedId()
        {
            if (Id is not null)
            {
                EffectiveId = new EntityId(
                    Id.Value + "-PAIR-" + HashFingerprint(new[]
                    {
                        Polygon.Area.ToString("R", CultureInfo.InvariantCulture),
                        OriginZoneIndex.ToString(CultureInfo.InvariantCulture),
                        OriginSurfaceIndex.ToString(CultureInfo.InvariantCulture),
                    }).Remove(8));
            }
        }

        private void SetMetadata(
            string name,
            FenestrationType type,
            FenestrationConstruction construction,
            BlindType? blind,
            EntityId? id)
        {
            Name = name;
            Type = type;
            Construction = construction;
            Blind = blind;
            Id = id;
            EffectiveId = id;
        }
    }

    private sealed class AdjacencyCandidate
    {
        internal AdjacencyCandidate(SurfaceWork first, SurfaceWork second)
        {
            First = first;
            Second = second;
        }

        internal SurfaceWork First { get; }

        internal SurfaceWork Second { get; }
    }
}
