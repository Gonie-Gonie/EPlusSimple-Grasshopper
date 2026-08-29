using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Rhino;
using Rhino.Geometry;
using DragonPolygon = GonieGonie.InvisibleDragon.Shape.PlanarPolygon;
using SimpleSurface = GonieGonie.SimpleDragon.Surface;
using SimpleZone = GonieGonie.SimpleDragon.Zone;

namespace GonieGonie.SimpleDragon.Rhino;

public enum RhinoMappedGeometryKind
{
    Zone,
    Surface,
    Fenestration,
}

/// <summary>
/// Relates a SimpleDragon entity to the Rhino geometry from which it was abstracted.
/// </summary>
public sealed class RhinoDomainGeometryMapEntry
{
    public RhinoDomainGeometryMapEntry(
        EntityId entityId,
        RhinoMappedGeometryKind kind,
        int sourceIndex,
        int? faceIndex,
        int? brepLoopIndex,
        int? fenestrationSourceIndex,
        GeometryProvenance provenance)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        if (!Enum.IsDefined(typeof(RhinoMappedGeometryKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown geometry-map kind.");
        }

        RhinoZoneExtractor.RequireNonNegative(sourceIndex, nameof(sourceIndex));

        if (faceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        }

        RhinoZoneExtractor.RequireNullableNonNegative(brepLoopIndex, nameof(brepLoopIndex));
        RhinoZoneExtractor.RequireNullableNonNegative(
            fenestrationSourceIndex,
            nameof(fenestrationSourceIndex));

        bool mapsFenestration = kind == RhinoMappedGeometryKind.Fenestration;
        bool mapsZone = kind == RhinoMappedGeometryKind.Zone;
        if (mapsZone == faceIndex.HasValue)
        {
            throw new ArgumentException(
                mapsZone
                    ? "A zone geometry mapping must not identify one Brep face."
                    : "A surface or fenestration geometry mapping requires its Brep face index.",
                nameof(faceIndex));
        }

        if (!mapsFenestration
            && (brepLoopIndex.HasValue || fenestrationSourceIndex.HasValue))
        {
            throw new ArgumentException(
                "Only a fenestration geometry mapping may identify an opening source.",
                nameof(brepLoopIndex));
        }

        if (mapsFenestration
            && !brepLoopIndex.HasValue
            && !fenestrationSourceIndex.HasValue)
        {
            throw new ArgumentException(
                "A fenestration geometry mapping requires a Brep-loop or explicit-source index.",
                nameof(brepLoopIndex));
        }

        Kind = kind;
        SourceIndex = sourceIndex;
        FaceIndex = faceIndex;
        BrepLoopIndex = brepLoopIndex;
        FenestrationSourceIndex = fenestrationSourceIndex;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public EntityId EntityId { get; }

    public RhinoMappedGeometryKind Kind { get; }

    public int SourceIndex { get; }

    public int? FaceIndex { get; }

    /// <summary>
    /// Gets the original Rhino Brep-loop index when the opening geometry came from a face trim.
    /// </summary>
    public int? BrepLoopIndex { get; }

    /// <summary>
    /// Gets the index in <see cref="RhinoZoneSource.Fenestrations"/> when an explicit source
    /// supplied or annotated the opening.
    /// </summary>
    public int? FenestrationSourceIndex { get; }

    /// <summary>
    /// Gets the legacy single geometry index for display compatibility. Prefer
    /// <see cref="BrepLoopIndex"/> and <see cref="FenestrationSourceIndex"/> when tracing geometry.
    /// </summary>
    public int? LoopIndex => BrepLoopIndex ?? FenestrationSourceIndex;

    public GeometryProvenance Provenance { get; }
}

/// <summary>
/// A separate closed Rhino curve assigned to one host face as a window or door.
/// The caller retains ownership of <see cref="Boundary"/>.
/// </summary>
public sealed class RhinoFenestrationSource
{
    public RhinoFenestrationSource(
        Curve boundary,
        int hostFaceIndex,
        string name,
        FenestrationType type,
        string constructionId,
        FenestrationConstruction? construction = null,
        BlindType? blind = null,
        EntityId? id = null,
        Guid? rhinoObjectId = null,
        string? grasshopperPath = null,
        int? grasshopperIndex = null)
    {
        Boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        RhinoZoneExtractor.RequireNonNegative(hostFaceIndex, nameof(hostFaceIndex));
        if (!boundary.IsValid || !boundary.IsClosed)
        {
            throw new ArgumentException("A valid closed opening curve is required.", nameof(boundary));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A fenestration name is required.", nameof(name));
        }

        if (!Enum.IsDefined(typeof(FenestrationType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown fenestration type.");
        }

        if (string.IsNullOrWhiteSpace(constructionId))
        {
            throw new ArgumentException("A construction identifier is required.", nameof(constructionId));
        }

        if (construction is not null
            && !StringComparer.Ordinal.Equals(constructionId.Trim(), construction.Id.Value))
        {
            throw new ArgumentException("The construction ID does not match the supplied construction.", nameof(constructionId));
        }

        if (blind.HasValue && !Enum.IsDefined(typeof(BlindType), blind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(blind), blind, "Unknown blind type.");
        }

        if (rhinoObjectId == Guid.Empty)
        {
            throw new ArgumentException("A Rhino object identifier must not be empty.", nameof(rhinoObjectId));
        }

        RhinoZoneExtractor.RequireNullableNonNegative(grasshopperIndex, nameof(grasshopperIndex));
        HostFaceIndex = hostFaceIndex;
        Name = name.Trim();
        Type = type;
        ConstructionId = constructionId.Trim();
        Construction = construction;
        Blind = blind;
        Id = id;
        RhinoObjectId = rhinoObjectId;
        GrasshopperPath = string.IsNullOrWhiteSpace(grasshopperPath) ? null : grasshopperPath!.Trim();
        GrasshopperIndex = grasshopperIndex;
    }

    public Curve Boundary { get; }

    public int HostFaceIndex { get; }

    public string Name { get; }

    public FenestrationType Type { get; }

    public string ConstructionId { get; }

    public FenestrationConstruction? Construction { get; }

    public BlindType? Blind { get; }

    public EntityId? Id { get; }

    public Guid? RhinoObjectId { get; }

    public string? GrasshopperPath { get; }

    public int? GrasshopperIndex { get; }
}

/// <summary>
/// One closed Rhino Brep and the non-geometric values required to create a SimpleDragon zone.
/// The caller retains ownership of <see cref="Geometry"/>.
/// </summary>
public sealed class RhinoZoneSource
{
    public RhinoZoneSource(
        Brep geometry,
        string name,
        int floorNumber,
        string profileName,
        UsageProfile? profile = null,
        double? lightDensity = null,
        EntityId? zoneId = null,
        Guid? rhinoObjectId = null,
        string? grasshopperPath = null,
        int? grasshopperIndex = null,
        IEnumerable<RhinoFenestrationSource>? fenestrations = null,
        SurfaceConstruction? defaultSurfaceConstruction = null,
        FenestrationConstruction? defaultFenestrationConstruction = null,
        SurfaceBoundaryCondition? unmatchedFloorBoundary = null)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A zone name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("A profile name is required.", nameof(profileName));
        }

        if (profile is not null && !StringComparer.Ordinal.Equals(profileName.Trim(), profile.Name))
        {
            throw new ArgumentException("The profile name does not match the supplied profile.", nameof(profileName));
        }

        if (lightDensity.HasValue
            && (double.IsNaN(lightDensity.Value)
                || double.IsInfinity(lightDensity.Value)
                || lightDensity.Value < 0d))
        {
            throw new ArgumentOutOfRangeException(nameof(lightDensity));
        }

        if (rhinoObjectId == Guid.Empty)
        {
            throw new ArgumentException("A Rhino object identifier must not be empty.", nameof(rhinoObjectId));
        }

        RhinoZoneExtractor.RequireNullableNonNegative(grasshopperIndex, nameof(grasshopperIndex));
        if (unmatchedFloorBoundary.HasValue
            && unmatchedFloorBoundary.Value != SurfaceBoundaryCondition.Ground
            && unmatchedFloorBoundary.Value != SurfaceBoundaryCondition.Outdoors
            && unmatchedFloorBoundary.Value != SurfaceBoundaryCondition.Adiabatic)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unmatchedFloorBoundary),
                unmatchedFloorBoundary,
                "An unmatched floor boundary must be Ground, Outdoors, or Adiabatic.");
        }

        RhinoFenestrationSource[] fenestrationArray = fenestrations?.ToArray()
            ?? Array.Empty<RhinoFenestrationSource>();
        if (fenestrationArray.Any(item => item is null))
        {
            throw new ArgumentException("A Rhino fenestration source cannot be null.", nameof(fenestrations));
        }

        Name = name.Trim();
        FloorNumber = floorNumber;
        ProfileName = profileName.Trim();
        Profile = profile;
        LightDensity = lightDensity;
        ZoneId = zoneId;
        RhinoObjectId = rhinoObjectId;
        GrasshopperPath = string.IsNullOrWhiteSpace(grasshopperPath) ? null : grasshopperPath!.Trim();
        GrasshopperIndex = grasshopperIndex;
        Fenestrations = new ReadOnlyCollection<RhinoFenestrationSource>(fenestrationArray);
        DefaultSurfaceConstruction = defaultSurfaceConstruction;
        DefaultFenestrationConstruction = defaultFenestrationConstruction;
        UnmatchedFloorBoundary = unmatchedFloorBoundary;
    }

    public Brep Geometry { get; }

    public string Name { get; }

    public int FloorNumber { get; }

    public string ProfileName { get; }

    public UsageProfile? Profile { get; }

    public double? LightDensity { get; }

    public EntityId? ZoneId { get; }

    public Guid? RhinoObjectId { get; }

    public string? GrasshopperPath { get; }

    public int? GrasshopperIndex { get; }

    public IReadOnlyList<RhinoFenestrationSource> Fenestrations { get; }

    /// <summary>
    /// Gets the per-zone default surface construction, or <see langword="null"/>
    /// to use the extraction-wide option.
    /// </summary>
    public SurfaceConstruction? DefaultSurfaceConstruction { get; }

    /// <summary>
    /// Gets the per-zone default construction for Brep inner-loop openings, or
    /// <see langword="null"/> to use the extraction-wide option.
    /// </summary>
    public FenestrationConstruction? DefaultFenestrationConstruction { get; }

    /// <summary>
    /// Gets the per-zone boundary for unmatched floor faces, or
    /// <see langword="null"/> to use the extraction-wide option.
    /// </summary>
    public SurfaceBoundaryCondition? UnmatchedFloorBoundary { get; }
}

public sealed class RhinoZoneExtractionOptions
{
    public bool RequireClosedBreps { get; set; } = true;

    public SurfaceBoundaryCondition UnmatchedFloorBoundary { get; set; } = SurfaceBoundaryCondition.Ground;

    public SurfaceConstruction? DefaultSurfaceConstruction { get; set; }

    public FenestrationConstruction? DefaultFenestrationConstruction { get; set; }

    public string UnresolvedFenestrationConstructionId { get; set; } = "RHINO-UNRESOLVED-FENESTRATION";

    public FenestrationType DefaultFenestrationType { get; set; } = FenestrationType.Window;

    public BlindType? DefaultBlind { get; set; }
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
/// Extracts the area-and-direction SimpleDragon domain from closed polygonal Rhino Breps.
/// </summary>
public static class RhinoZoneExtractor
{
    public static RhinoZoneExtractionResult Extract(
        IEnumerable<RhinoZoneSource> sources,
        RhinoGeometryContext context,
        RhinoZoneExtractionOptions? options = null)
    {
        RequireNotNull(sources, nameof(sources));
        RequireNotNull(context, nameof(context));

        options ??= new RhinoZoneExtractionOptions();
        ValidateOptions(options);
        RhinoZoneSource[] sourceArray = sources.ToArray();
        if (sourceArray.Any(item => item is null))
        {
            throw new ArgumentException("A Rhino zone source cannot be null.", nameof(sources));
        }

        var diagnostics = new List<Diagnostic>();
        var work = new List<ZoneWork>();
        for (int sourceIndex = 0; sourceIndex < sourceArray.Length; sourceIndex++)
        {
            ZoneWork? prepared = PrepareZone(sourceArray[sourceIndex], sourceIndex, context, options, diagnostics);
            if (prepared is not null)
            {
                work.Add(prepared);
            }
        }

        Dictionary<string, EntityId> adjacency = FindAdjacency(work, context, diagnostics);
        var zones = new List<SimpleZone>();
        var geometryMap = new List<RhinoDomainGeometryMapEntry>();
        foreach (ZoneWork zoneWork in work)
        {
            CreateZone(zoneWork, adjacency, context, options, zones, geometryMap, diagnostics);
        }

        return new RhinoZoneExtractionResult(zones, geometryMap, diagnostics);
    }

    private static ZoneWork? PrepareZone(
        RhinoZoneSource source,
        int sourceIndex,
        RhinoGeometryContext context,
        RhinoZoneExtractionOptions options,
        List<Diagnostic> diagnostics)
    {
        if (!source.Geometry.IsValid)
        {
            diagnostics.Add(Error(
                "SD.RHINO.BREP_INVALID",
                "The Rhino zone Brep is invalid.",
                null,
                null,
                "Repair the Brep before extracting a zone."));
            return null;
        }

        if (options.RequireClosedBreps && !source.Geometry.IsSolid)
        {
            diagnostics.Add(Error(
                "SD.RHINO.BREP_NOT_CLOSED",
                "The Rhino zone Brep must be a closed solid.",
                null,
                null,
                "Join and cap the Brep into one closed volume."));
            return null;
        }

        bool reverseSolidOrientation = source.Geometry.IsSolid
            && source.Geometry.SolidOrientation == BrepSolidOrientation.Inward;

        BoundingBox bounds = source.Geometry.GetBoundingBox(true);
        double height = context.ToMetres(bounds.Max.Z - bounds.Min.Z);
        if (!bounds.IsValid || height <= context.ModelToleranceMetres)
        {
            diagnostics.Add(Error(
                "SD.RHINO.ZONE_HEIGHT_INVALID",
                "The Rhino zone does not have a positive vertical height.",
                null,
                null,
                "Supply a three-dimensional zone volume."));
            return null;
        }

        var faces = new List<FaceWork>();
        foreach (BrepFace face in source.Geometry.Faces)
        {
            try
            {
                RhinoPolygonExtraction extraction = RhinoPolygonConverter.FromBrepFace(face, context);
                if (reverseSolidOrientation)
                {
                    extraction = Reverse(extraction);
                }

                var faceWork = new FaceWork(sourceIndex, face.FaceIndex, extraction);
                int[] innerLoopIndices = face.Loops
                    .Where(loop => loop.LoopType == BrepLoopType.Inner)
                    .Select(loop => loop.LoopIndex)
                    .ToArray();
                for (int loopIndex = 0; loopIndex < extraction.InnerLoops.Count; loopIndex++)
                {
                    DragonPolygon loop = extraction.InnerLoops[loopIndex];
                    FenestrationConstruction? defaultFenestrationConstruction =
                        source.DefaultFenestrationConstruction
                        ?? options.DefaultFenestrationConstruction;
                    faceWork.Openings.Add(new OpeningWork(
                        source.Name + ":Face:" + face.FaceIndex.ToString(CultureInfo.InvariantCulture)
                            + ":Opening:" + loopIndex.ToString(CultureInfo.InvariantCulture),
                        options.DefaultFenestrationType,
                        defaultFenestrationConstruction?.Id.Value
                            ?? options.UnresolvedFenestrationConstructionId.Trim(),
                        defaultFenestrationConstruction,
                        options.DefaultBlind,
                        null,
                        loop,
                        innerLoopIndices[loopIndex],
                        null,
                        source.RhinoObjectId,
                        source.GrasshopperPath,
                        source.GrasshopperIndex));
                }

                faces.Add(faceWork);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is NotSupportedException)
            {
                var fallback = new GeometryProvenance(
                    source.RhinoObjectId,
                    face.FaceIndex,
                    "unavailable-" + sourceIndex.ToString("D4", CultureInfo.InvariantCulture)
                        + "-" + face.FaceIndex.ToString("D4", CultureInfo.InvariantCulture),
                    source.GrasshopperPath,
                    source.GrasshopperIndex);
                diagnostics.Add(Error(
                    "SD.RHINO.FACE_UNSUPPORTED",
                    "A zone face could not be reduced to a planar polygon: " + exception.Message,
                    null,
                    fallback,
                    "Use planar faces with straight boundary segments for the first release."));
            }
        }

        if (faces.Count == 0)
        {
            diagnostics.Add(Error(
                "SD.RHINO.ZONE_HAS_NO_SUPPORTED_FACES",
                "The Rhino zone contains no supported planar polygon faces.",
                null,
                null,
                "Replace or simplify the zone geometry."));
            return null;
        }

        AddExplicitFenestrations(source, sourceIndex, faces, context, diagnostics);

        string fingerprint = HashFingerprint(faces.Select(item => item.Extraction.GeometryFingerprint));
        EntityId zoneId = source.ZoneId ?? new EntityId("ZONE-RHINO-" + fingerprint.Remove(24));
        return new ZoneWork(source, sourceIndex, zoneId, height, fingerprint, faces);
    }

    private static void AddExplicitFenestrations(
        RhinoZoneSource source,
        int sourceIndex,
        IReadOnlyList<FaceWork> faces,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        Dictionary<int, FaceWork> byIndex = faces.ToDictionary(item => item.FaceIndex);
        for (int openingIndex = 0; openingIndex < source.Fenestrations.Count; openingIndex++)
        {
            RhinoFenestrationSource opening = source.Fenestrations[openingIndex];
            if (!byIndex.TryGetValue(opening.HostFaceIndex, out FaceWork? host))
            {
                diagnostics.Add(Error(
                    "SD.RHINO.OPENING_HOST_NOT_FOUND",
                    "A separate opening curve references a missing or unsupported host face.",
                    opening.Id,
                    null,
                    "Use a face index contained in the source zone Brep."));
                continue;
            }

            GeometryProvenance fallback = new(
                opening.RhinoObjectId ?? source.RhinoObjectId,
                opening.HostFaceIndex,
                "unavailable-opening-" + sourceIndex.ToString("D4", CultureInfo.InvariantCulture)
                    + "-" + openingIndex.ToString("D4", CultureInfo.InvariantCulture),
                opening.GrasshopperPath ?? source.GrasshopperPath,
                opening.GrasshopperIndex ?? source.GrasshopperIndex);
            try
            {
                BrepFace hostFace = source.Geometry.Faces[opening.HostFaceIndex];
                using Curve projected = RhinoPolygonConverter.ProjectOpeningToFacePlane(
                    opening.Boundary,
                    hostFace,
                    context);
                DragonPolygon polygon = RhinoPolygonConverter.FromClosedCurve(projected, context);
                if (!host.Extraction.OuterLoop.Contains(polygon, context.ModelToleranceMetres))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.OPENING_OUTSIDE_HOST",
                        "A separate opening curve is not completely contained by its host face.",
                        opening.Id,
                        new GeometryProvenance(
                            opening.RhinoObjectId ?? source.RhinoObjectId,
                            opening.HostFaceIndex,
                            RhinoGeometryFingerprint.ForPolygon(polygon),
                            opening.GrasshopperPath ?? source.GrasshopperPath,
                            opening.GrasshopperIndex ?? source.GrasshopperIndex),
                        "Move or resize the opening inside the host face boundary."));
                    continue;
                }

                int matchingIndex = host.Openings.FindIndex(existing =>
                    existing.Polygon.IsGeometricallyEquivalentTo(
                        polygon,
                        allowReversedWinding: true,
                        tolerance: context.ModelToleranceMetres));
                if (matchingIndex >= 0)
                {
                    OpeningWork matching = host.Openings[matchingIndex];
                    if (matching.BrepLoopIndex.HasValue
                        && !matching.FenestrationSourceIndex.HasValue)
                    {
                        host.Openings[matchingIndex] = new OpeningWork(
                            opening.Name,
                            opening.Type,
                            opening.ConstructionId,
                            opening.Construction,
                            opening.Blind,
                            opening.Id,
                            matching.Polygon,
                            matching.BrepLoopIndex,
                            openingIndex,
                            opening.RhinoObjectId ?? source.RhinoObjectId,
                            opening.GrasshopperPath ?? source.GrasshopperPath,
                            opening.GrasshopperIndex ?? source.GrasshopperIndex);
                        diagnostics.Add(new Diagnostic(
                            "SD.RHINO.OPENING_INNER_LOOP_ANNOTATED",
                            DiagnosticSeverity.Info,
                            "A separate opening source supplied metadata for a matching host-face inner loop.",
                            opening.Id,
                            new GeometryProvenance(
                                opening.RhinoObjectId ?? source.RhinoObjectId,
                                opening.HostFaceIndex,
                                RhinoGeometryFingerprint.ForPolygon(matching.Polygon),
                                opening.GrasshopperPath ?? source.GrasshopperPath,
                                opening.GrasshopperIndex ?? source.GrasshopperIndex),
                            "Retain both provenance indices when tracing the opening back to Rhino."));
                    }
                    else
                    {
                        diagnostics.Add(new Diagnostic(
                            "SD.RHINO.OPENING_DUPLICATE_IGNORED",
                            DiagnosticSeverity.Warning,
                            "A separate opening duplicates another explicit host-face opening and was ignored.",
                            opening.Id,
                            fallback,
                            "Supply each explicit opening once."));
                    }

                    continue;
                }

                if (host.Openings.Any(existing =>
                        existing.Polygon.IntersectsInterior(polygon, context.ModelToleranceMetres)))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.OPENINGS_OVERLAP",
                        "Two opening polygons overlap on the same host face.",
                        opening.Id,
                        fallback,
                        "Separate the opening boundaries."));
                    continue;
                }

                host.Openings.Add(new OpeningWork(
                    opening.Name,
                    opening.Type,
                    opening.ConstructionId,
                    opening.Construction,
                    opening.Blind,
                    opening.Id,
                    polygon,
                    null,
                    openingIndex,
                    opening.RhinoObjectId ?? source.RhinoObjectId,
                    opening.GrasshopperPath ?? source.GrasshopperPath,
                    opening.GrasshopperIndex ?? source.GrasshopperIndex));
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is NotSupportedException)
            {
                diagnostics.Add(Error(
                    "SD.RHINO.OPENING_UNSUPPORTED",
                    "A separate opening curve could not be extracted: " + exception.Message,
                    opening.Id,
                    fallback,
                    "Use a closed polygonal curve on or projectable to the host plane."));
            }
        }
    }

    private static Dictionary<string, EntityId> FindAdjacency(
        IReadOnlyList<ZoneWork> zones,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        FaceWork[] faces = zones.SelectMany(zone => zone.Faces).ToArray();
        var zoneBySource = zones.ToDictionary(zone => zone.SourceIndex);
        var adjacency = new Dictionary<string, EntityId>(StringComparer.Ordinal);
        for (int firstIndex = 0; firstIndex < faces.Length; firstIndex++)
        {
            FaceWork first = faces[firstIndex];
            for (int secondIndex = firstIndex + 1; secondIndex < faces.Length; secondIndex++)
            {
                FaceWork second = faces[secondIndex];
                if (first.SourceIndex == second.SourceIndex
                    || !first.Extraction.OuterLoop.IsGeometricallyEquivalentTo(
                        second.Extraction.OuterLoop,
                        allowReversedWinding: true,
                        tolerance: context.ModelToleranceMetres))
                {
                    continue;
                }

                ZoneWork firstZone = zoneBySource[first.SourceIndex];
                ZoneWork secondZone = zoneBySource[second.SourceIndex];
                double angleTolerance = Math.Min(context.AngleToleranceRadians, Math.PI / 2d);
                double normalDot = first.Extraction.OuterLoop.Normal.Dot(second.Extraction.OuterLoop.Normal);
                if (normalDot > -Math.Cos(angleTolerance))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.ADJACENCY_NORMALS_NOT_OPPOSED",
                        "Coincident faces in two Rhino zones do not have opposing outward normals.",
                        firstZone.ZoneId,
                        Provenance(
                            firstZone.Source,
                            first.FaceIndex,
                            first.Extraction.GeometryFingerprint),
                        "Remove overlapping duplicate volumes or correct their solid orientation."));
                    continue;
                }

                if (!OpeningTopologiesEquivalent(first, second, context.ModelToleranceMetres))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.ADJACENCY_OPENINGS_MISMATCH",
                        "Coincident zone faces do not contain the same opening topology.",
                        firstZone.ZoneId,
                        Provenance(
                            firstZone.Source,
                            first.FaceIndex,
                            first.Extraction.GeometryFingerprint),
                        "Provide matching opening boundaries on both sides of the shared face."));
                    continue;
                }

                string firstKey = FaceKey(first.SourceIndex, first.FaceIndex);
                string secondKey = FaceKey(second.SourceIndex, second.FaceIndex);
                if (adjacency.ContainsKey(firstKey) || adjacency.ContainsKey(secondKey))
                {
                    diagnostics.Add(Error(
                        "SD.RHINO.ADJACENCY_AMBIGUOUS",
                        "A Rhino face coincides with more than one face in another zone.",
                        null,
                        null,
                        "Remove duplicate volumes or split the topology into unambiguous zone pairs."));
                    continue;
                }

                adjacency.Add(firstKey, secondZone.ZoneId);
                adjacency.Add(secondKey, firstZone.ZoneId);
            }
        }

        return adjacency;
    }

    private static void CreateZone(
        ZoneWork work,
        Dictionary<string, EntityId> adjacency,
        RhinoGeometryContext context,
        RhinoZoneExtractionOptions options,
        List<SimpleZone> zones,
        List<RhinoDomainGeometryMapEntry> geometryMap,
        List<Diagnostic> diagnostics)
    {
        var surfaces = new List<SimpleSurface>();
        foreach (FaceWork face in work.Faces.OrderBy(item => item.FaceIndex))
        {
            string key = FaceKey(work.SourceIndex, face.FaceIndex);
            adjacency.TryGetValue(key, out EntityId? adjacentZoneId);
            GeometryProvenance provenance = Provenance(work.Source, face.FaceIndex, face.Extraction.GeometryFingerprint);
            SurfaceType type = Classify(face.Extraction.OuterLoop, context, provenance, diagnostics);
            SurfaceBoundaryCondition boundary = adjacentZoneId is not null
                ? SurfaceBoundaryCondition.Zone
                : type == SurfaceType.Floor
                    ? work.Source.UnmatchedFloorBoundary ?? options.UnmatchedFloorBoundary
                    : SurfaceBoundaryCondition.Outdoors;
            double? azimuth = type == SurfaceType.Wall && boundary == SurfaceBoundaryCondition.Outdoors
                ? Azimuth(face.Extraction.OuterLoop)
                : null;
            EntityId surfaceId = new(
                "SURF-" + work.ZoneId.Value + "-F" + face.FaceIndex.ToString("D4", CultureInfo.InvariantCulture));
            IReadOnlyList<Fenestration> fenestrations = CreateFenestrations(
                work,
                face,
                surfaceId,
                boundary,
                geometryMap,
                diagnostics);
            SurfaceConstruction? defaultSurfaceConstruction =
                work.Source.DefaultSurfaceConstruction
                ?? options.DefaultSurfaceConstruction;
            string? constructionId = defaultSurfaceConstruction?.Id.Value;
            try
            {
                var surface = new SimpleSurface(
                    work.Source.Name + ":Face:" + face.FaceIndex.ToString(CultureInfo.InvariantCulture),
                    type,
                    boundary,
                    face.Extraction.OuterLoop.Area,
                    azimuth,
                    constructionId,
                    defaultSurfaceConstruction,
                    fenestrations,
                    adjacentZoneId: adjacentZoneId?.Value,
                    id: surfaceId);
                surfaces.Add(surface);
                geometryMap.Add(new RhinoDomainGeometryMapEntry(
                    surface.Id,
                    RhinoMappedGeometryKind.Surface,
                    work.SourceIndex,
                    face.FaceIndex,
                    null,
                    null,
                    provenance));
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(Error(
                    "SD.RHINO.SURFACE_DOMAIN_INVALID",
                    "The extracted face could not form a SimpleDragon surface: " + exception.Message,
                    surfaceId,
                    provenance,
                    "Correct the face classification or construction assignment."));
            }
        }

        GeometryProvenance zoneProvenance = Provenance(work.Source, null, work.Fingerprint);
        if (surfaces.Count == 0)
        {
            diagnostics.Add(Error(
                "SD.RHINO.ZONE_HAS_NO_SURFACES",
                "No SimpleDragon surfaces could be created for the Rhino zone.",
                work.ZoneId,
                zoneProvenance,
                "Review the face diagnostics."));
            return;
        }

        try
        {
            var zone = new SimpleZone(
                work.Source.Name,
                work.Source.FloorNumber,
                work.HeightMetres,
                surfaces,
                work.Source.ProfileName,
                work.Source.Profile,
                work.Source.LightDensity,
                id: work.ZoneId);
            zones.Add(zone);
            geometryMap.Add(new RhinoDomainGeometryMapEntry(
                zone.Id,
                RhinoMappedGeometryKind.Zone,
                work.SourceIndex,
                null,
                null,
                null,
                zoneProvenance));
            diagnostics.Add(new Diagnostic(
                "SD.RHINO.GEOMETRY_ABSTRACTED",
                DiagnosticSeverity.Warning,
                "SimpleDragon stores face areas and directions, not the source Brep vertices. The provenance map retains the source fingerprints.",
                zone.Id,
                zoneProvenance,
                "Use the InvisibleDragon conversion preview when exact generated polygons must be inspected."));
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(Error(
                "SD.RHINO.ZONE_DOMAIN_INVALID",
                "The extracted values could not form a SimpleDragon zone: " + exception.Message,
                work.ZoneId,
                zoneProvenance,
                "Correct the reported zone input or geometry."));
        }
    }

    private static IReadOnlyList<Fenestration> CreateFenestrations(
        ZoneWork work,
        FaceWork face,
        EntityId surfaceId,
        SurfaceBoundaryCondition boundary,
        List<RhinoDomainGeometryMapEntry> geometryMap,
        List<Diagnostic> diagnostics)
    {
        if (face.Openings.Count == 0)
        {
            return Array.Empty<Fenestration>();
        }

        GeometryProvenance hostProvenance = Provenance(
            work.Source,
            face.FaceIndex,
            face.Extraction.GeometryFingerprint);
        if (boundary == SurfaceBoundaryCondition.Ground || boundary == SurfaceBoundaryCondition.Adiabatic)
        {
            diagnostics.Add(Error(
                "SD.RHINO.OPENING_BOUNDARY_INVALID",
                "A ground or adiabatic face contains one or more openings.",
                surfaceId,
                hostProvenance,
                "Remove the openings or select a boundary condition that permits fenestration."));
            return Array.Empty<Fenestration>();
        }

        if (face.Openings.Any(item => item.Construction is null))
        {
            diagnostics.Add(new Diagnostic(
                "SD.RHINO.FENESTRATION_CONSTRUCTION_UNRESOLVED",
                DiagnosticSeverity.Warning,
                "One or more Rhino openings were extracted with an unresolved fenestration construction.",
                surfaceId,
                hostProvenance,
                "Supply DefaultFenestrationConstruction before converting the model to InvisibleDragon."));
        }

        var result = new List<Fenestration>();
        for (int index = 0; index < face.Openings.Count; index++)
        {
            OpeningWork definition = face.Openings[index];
            EntityId openingId = definition.Id ?? new EntityId(
                "FNST-" + surfaceId.Value + "-L" + index.ToString("D4", CultureInfo.InvariantCulture));
            var opening = new Fenestration(
                definition.Name,
                definition.Type,
                definition.Polygon.Area,
                definition.ConstructionId,
                definition.Construction,
                definition.Blind,
                openingId);
            result.Add(opening);
            geometryMap.Add(new RhinoDomainGeometryMapEntry(
                opening.Id,
                RhinoMappedGeometryKind.Fenestration,
                work.SourceIndex,
                face.FaceIndex,
                definition.BrepLoopIndex,
                definition.FenestrationSourceIndex,
                new GeometryProvenance(
                    definition.RhinoObjectId,
                    face.FaceIndex,
                    RhinoGeometryFingerprint.ForPolygon(definition.Polygon),
                    definition.GrasshopperPath,
                    definition.GrasshopperIndex)));
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
                "SD.RHINO.SLOPED_SURFACE_ABSTRACTED",
                DiagnosticSeverity.Warning,
                "A sloped face was classified by its closest wall/floor/ceiling orientation; SimpleDragon does not preserve tilt.",
                geometry: provenance,
                suggestedAction: "Inspect the converted InvisibleDragon polygon before simulation."));
        }

        return type;
    }

    private static double Azimuth(DragonPolygon polygon)
    {
        double degrees = Math.Atan2(polygon.Normal.X, polygon.Normal.Y) * 180d / Math.PI;
        return (degrees % 360d + 360d) % 360d;
    }

    private static bool OpeningTopologiesEquivalent(
        FaceWork first,
        FaceWork second,
        double tolerance)
    {
        if (first.Openings.Count != second.Openings.Count)
        {
            return false;
        }

        var used = new bool[second.Openings.Count];
        foreach (OpeningWork opening in first.Openings)
        {
            bool matched = false;
            for (int index = 0; index < second.Openings.Count; index++)
            {
                if (!used[index]
                    && opening.Polygon.IsGeometricallyEquivalentTo(
                        second.Openings[index].Polygon,
                        allowReversedWinding: true,
                        tolerance: tolerance))
                {
                    used[index] = true;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static RhinoPolygonExtraction Reverse(RhinoPolygonExtraction extraction)
    {
        return new RhinoPolygonExtraction(
            extraction.OuterLoop.Reverse(),
            extraction.InnerLoops.Select(loop => loop.Reverse()),
            extraction.SourcePlane);
    }

    private static GeometryProvenance Provenance(
        RhinoZoneSource source,
        int? faceIndex,
        string fingerprint)
    {
        return new GeometryProvenance(
            source.RhinoObjectId,
            faceIndex,
            fingerprint,
            source.GrasshopperPath,
            source.GrasshopperIndex);
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

    private static void ValidateOptions(RhinoZoneExtractionOptions options)
    {
        if (options.UnmatchedFloorBoundary != SurfaceBoundaryCondition.Ground
            && options.UnmatchedFloorBoundary != SurfaceBoundaryCondition.Outdoors
            && options.UnmatchedFloorBoundary != SurfaceBoundaryCondition.Adiabatic)
        {
            throw new ArgumentException(
                "An unmatched floor boundary must be Ground, Outdoors, or Adiabatic.",
                nameof(options));
        }

        if (!Enum.IsDefined(typeof(FenestrationType), options.DefaultFenestrationType))
        {
            throw new ArgumentException("The default fenestration type is invalid.", nameof(options));
        }

        if (options.DefaultBlind.HasValue
            && !Enum.IsDefined(typeof(BlindType), options.DefaultBlind.Value))
        {
            throw new ArgumentException("The default blind type is invalid.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.UnresolvedFenestrationConstructionId))
        {
            throw new ArgumentException(
                "An unresolved fenestration construction identifier is required.",
                nameof(options));
        }
    }

    private static string FaceKey(int sourceIndex, int faceIndex)
    {
        return sourceIndex.ToString(CultureInfo.InvariantCulture)
            + ":"
            + faceIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static Diagnostic Error(
        string code,
        string message,
        EntityId? objectId,
        GeometryProvenance? geometry,
        string action)
    {
        return new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            message,
            objectId,
            geometry,
            action);
    }

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

    private sealed class FaceWork
    {
        internal FaceWork(int sourceIndex, int faceIndex, RhinoPolygonExtraction extraction)
        {
            SourceIndex = sourceIndex;
            FaceIndex = faceIndex;
            Extraction = extraction;
            Openings = new List<OpeningWork>();
        }

        internal int SourceIndex { get; }

        internal int FaceIndex { get; }

        internal RhinoPolygonExtraction Extraction { get; }

        internal List<OpeningWork> Openings { get; }
    }

    private sealed class OpeningWork
    {
        internal OpeningWork(
            string name,
            FenestrationType type,
            string constructionId,
            FenestrationConstruction? construction,
            BlindType? blind,
            EntityId? id,
            DragonPolygon polygon,
            int? brepLoopIndex,
            int? fenestrationSourceIndex,
            Guid? rhinoObjectId,
            string? grasshopperPath,
            int? grasshopperIndex)
        {
            Name = name;
            Type = type;
            ConstructionId = constructionId;
            Construction = construction;
            Blind = blind;
            Id = id;
            Polygon = polygon;
            BrepLoopIndex = brepLoopIndex;
            FenestrationSourceIndex = fenestrationSourceIndex;
            RhinoObjectId = rhinoObjectId;
            GrasshopperPath = grasshopperPath;
            GrasshopperIndex = grasshopperIndex;
        }

        internal string Name { get; }

        internal FenestrationType Type { get; }

        internal string ConstructionId { get; }

        internal FenestrationConstruction? Construction { get; }

        internal BlindType? Blind { get; }

        internal EntityId? Id { get; }

        internal DragonPolygon Polygon { get; }

        internal int? BrepLoopIndex { get; }

        internal int? FenestrationSourceIndex { get; }

        internal Guid? RhinoObjectId { get; }

        internal string? GrasshopperPath { get; }

        internal int? GrasshopperIndex { get; }
    }

    private sealed class ZoneWork
    {
        internal ZoneWork(
            RhinoZoneSource source,
            int sourceIndex,
            EntityId zoneId,
            double heightMetres,
            string fingerprint,
            IReadOnlyList<FaceWork> faces)
        {
            Source = source;
            SourceIndex = sourceIndex;
            ZoneId = zoneId;
            HeightMetres = heightMetres;
            Fingerprint = fingerprint;
            Faces = faces;
        }

        internal RhinoZoneSource Source { get; }

        internal int SourceIndex { get; }

        internal EntityId ZoneId { get; }

        internal double HeightMetres { get; }

        internal string Fingerprint { get; }

        internal IReadOnlyList<FaceWork> Faces { get; }
    }
}
