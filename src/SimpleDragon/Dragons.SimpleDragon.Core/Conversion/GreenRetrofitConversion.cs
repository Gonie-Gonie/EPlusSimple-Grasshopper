using System.Collections.ObjectModel;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.SimpleDragon.Internal;
using DaySchedule = Dragons.InvisibleDragon.Profile.DaySchedule;
using DayScheduleWindow = Dragons.InvisibleDragon.Profile.DayScheduleWindow;
using DragonAbsorptionChiller = Dragons.InvisibleDragon.Hvac.AbsorptionChiller;
using DragonAirBoundary = Dragons.InvisibleDragon.Construction.AirBoundary;
using DragonBlind = Dragons.InvisibleDragon.Shape.Blind;
using DragonBoiler = Dragons.InvisibleDragon.Hvac.Boiler;
using DragonChiller = Dragons.InvisibleDragon.Hvac.Chiller;
using DragonClosedSingleSpeedCoolingTower = Dragons.InvisibleDragon.Hvac.ClosedSingleSpeedCoolingTower;
using DragonClosedTwoSpeedCoolingTower = Dragons.InvisibleDragon.Hvac.ClosedTwoSpeedCoolingTower;
using DragonCompressorType = Dragons.InvisibleDragon.Hvac.CompressorType;
using DragonConstruction = Dragons.InvisibleDragon.Construction.Construction;
using DragonCoolingTower = Dragons.InvisibleDragon.Hvac.CoolingTower;
using DragonDistrictHeating = Dragons.InvisibleDragon.Hvac.DistrictHeating;
using DragonDoor = Dragons.InvisibleDragon.Shape.Door;
using DragonElectricRadiantFloor = Dragons.InvisibleDragon.Hvac.ElectricRadiantFloor;
using DragonElectricRadiator = Dragons.InvisibleDragon.Hvac.ElectricRadiator;
using DragonFanCoilUnit = Dragons.InvisibleDragon.Hvac.FanCoilUnit;
using DragonFuel = Dragons.InvisibleDragon.Hvac.Fuel;
using DragonGeothermalHeatPump = Dragons.InvisibleDragon.Hvac.GeothermalHeatPump;
using DragonGlazing = Dragons.InvisibleDragon.Construction.Glazing;
using DragonHeatPump = Dragons.InvisibleDragon.Hvac.HeatPump;
using DragonHvacSource = Dragons.InvisibleDragon.Hvac.SourceSystem;
using DragonHvacSupply = Dragons.InvisibleDragon.Hvac.SupplySystem;
using DragonLayer = Dragons.InvisibleDragon.Construction.Layer;
using DragonMaterial = Dragons.InvisibleDragon.Construction.Material;
using DragonNoMassConstruction = Dragons.InvisibleDragon.Construction.NoMassConstruction;
using DragonOpening = Dragons.InvisibleDragon.Shape.IOpening;
using DragonOpenSingleSpeedCoolingTower = Dragons.InvisibleDragon.Hvac.OpenSingleSpeedCoolingTower;
using DragonOpenTwoSpeedCoolingTower = Dragons.InvisibleDragon.Hvac.OpenTwoSpeedCoolingTower;
using DragonPhotovoltaicPanel = Dragons.InvisibleDragon.Hvac.PhotovoltaicPanel;
using DragonPlanarPolygon = Dragons.InvisibleDragon.Shape.PlanarPolygon;
using DragonProfile = Dragons.InvisibleDragon.Profile.Profile;
using DragonRadiantFloor = Dragons.InvisibleDragon.Hvac.RadiantFloor;
using DragonRadiator = Dragons.InvisibleDragon.Hvac.Radiator;
using DragonSchedule = Dragons.InvisibleDragon.Profile.Schedule;
using DragonScheduleType = Dragons.InvisibleDragon.Profile.ScheduleType;
using DragonShade = Dragons.InvisibleDragon.Shape.Shade;
using DragonSupplyGroup = Dragons.InvisibleDragon.Hvac.SupplyGroup;
using DragonSurface = Dragons.InvisibleDragon.Shape.Surface;
using DragonSurfaceBoundary = Dragons.InvisibleDragon.Shape.SurfaceBoundary;
using DragonSurfaceConstruction = Dragons.InvisibleDragon.Construction.ISurfaceConstruction;
using DragonSurfaceType = Dragons.InvisibleDragon.Shape.SurfaceType;
using DragonTerrain = Dragons.InvisibleDragon.Model.Terrain;
using DragonVertex = Dragons.InvisibleDragon.Shape.Vertex;
using DragonWindow = Dragons.InvisibleDragon.Shape.Window;
using DragonZone = Dragons.InvisibleDragon.Shape.Zone;
using DragonZoneHvacAssignment = Dragons.InvisibleDragon.Hvac.ZoneHvacAssignment;
using DragonZoneVentilationAssignment = Dragons.InvisibleDragon.Hvac.ZoneVentilationAssignment;
using EnergyRecoveryVentilator = Dragons.InvisibleDragon.Hvac.EnergyRecoveryVentilator;
using RuleSet = Dragons.InvisibleDragon.Profile.RuleSet;

namespace Dragons.SimpleDragon;

/// <summary>
/// Controls deterministic conversion from the area-based GRM domain to InvisibleDragon geometry.
/// </summary>
public sealed class GreenRetrofitConversionOptions
{
    public SimpleDragonDatabase Database { get; set; } = SimpleDragonDatabase.Default;

    public bool ResolveUnknownConstructions { get; set; } = true;

    public bool IncludeModelValidationDiagnostics { get; set; } = true;
}

/// <summary>
/// Relates one source GRM surface to a surface emitted in the InvisibleDragon model.
/// </summary>
public sealed class GreenRetrofitSurfaceConversion
{
    internal GreenRetrofitSurfaceConversion(
        EntityId sourceZoneId,
        EntityId sourceSurfaceId,
        EntityId convertedZoneId,
        EntityId convertedSurfaceId,
        bool isSynthesizedCounterpart)
    {
        SourceZoneId = sourceZoneId;
        SourceSurfaceId = sourceSurfaceId;
        ConvertedZoneId = convertedZoneId;
        ConvertedSurfaceId = convertedSurfaceId;
        IsSynthesizedCounterpart = isSynthesizedCounterpart;
    }

    public EntityId SourceZoneId { get; }

    public EntityId SourceSurfaceId { get; }

    public EntityId ConvertedZoneId { get; }

    public EntityId ConvertedSurfaceId { get; }

    /// <summary>
    /// Gets whether this output is the missing reciprocal face synthesized from the source surface.
    /// </summary>
    public bool IsSynthesizedCounterpart { get; }
}

/// <summary>
/// A non-throwing conversion result that retains actionable compatibility diagnostics.
/// </summary>
public sealed class GreenRetrofitConversionResult
{
    internal GreenRetrofitConversionResult(
        EnergyModel? energyModel,
        WeatherSelection? weather,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<GreenRetrofitSurfaceConversion> surfaceConversions)
    {
        EnergyModel = energyModel;
        Weather = weather;
        Diagnostics = diagnostics;
        SurfaceConversions = surfaceConversions;
    }

    public EnergyModel? EnergyModel { get; }

    public WeatherSelection? Weather { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets deterministic source-to-output surface relationships, including synthesized counterparts.
    /// </summary>
    public IReadOnlyList<GreenRetrofitSurfaceConversion> SurfaceConversions { get; }

    public bool Success => EnergyModel is not null && Diagnostics.All(item => !item.IsFailure);

    public EnergyModel RequireEnergyModel()
    {
        if (Success)
        {
            return EnergyModel!;
        }

        string detail = Diagnostics.Count == 0
            ? "The GRM conversion did not produce an energy model."
            : Diagnostics[0].Code + ": " + Diagnostics[0].Message;
        throw new InvalidOperationException(detail);
    }

    public IdfDocument ToIdfDocument(
        IddSchema? schema = null,
        EnergyModelIdfOptions? options = null)
    {
        options ??= new EnergyModelIdfOptions
        {
            UseLegacyRectangularFenestration = true,
            UseLegacySimpleDragonScheduleMetadata = true,
            UseLegacySimpleDragonDefaultObjectFields = true,
            UseLegacySimpleDragonUsedProfileScheduleSelection = true,
            UseLegacySimpleDragonHvacTopology = true,
            UseLegacySimpleDragonVentilation = true,
        };
        return RequireEnergyModel().ToIdfDocument(schema, options);
    }
}

/// <summary>
/// Converts the pinned GRM 0.7 aggregate into the Rhino-independent InvisibleDragon model.
/// </summary>
public static class GreenRetrofitConverter
{
    public static GreenRetrofitConversionResult Convert(
        GreenRetrofitModel model,
        GreenRetrofitConversionOptions? options = null)
    {
        DomainSupport.NotNull(model, nameof(model));
        options ??= new GreenRetrofitConversionOptions();
        DomainSupport.NotNull(options.Database, nameof(options.Database));
        return new Converter(model, options).Convert();
    }

    /// <summary>
    /// Converts one usage profile without requiring model geometry.
    /// Each call returns a fresh immutable InvisibleDragon profile graph.
    /// </summary>
    public static DragonProfile ConvertProfile(UsageProfile profile)
    {
        DomainSupport.NotNull(profile, nameof(profile));
        return Converter.CreateProfile(profile);
    }

    public static IdfDocument ToIdfDocument(
        GreenRetrofitModel model,
        GreenRetrofitConversionOptions? conversionOptions = null,
        IddSchema? schema = null,
        EnergyModelIdfOptions? idfOptions = null)
    {
        return Convert(model, conversionOptions).ToIdfDocument(schema, idfOptions);
    }

    private sealed class Converter
    {
        private const string ClonePrefix = "$CLONE_OF$:";
        private const string ReversedPrefix = "$REVERSED$:";

        private const string DefaultInnerWallName = "SimpleDragon:DefaultInnerWallConstruction";
        private readonly GreenRetrofitModel _model;
        private readonly GreenRetrofitConversionOptions _options;
        private readonly List<Diagnostic> _diagnostics = new();
        private readonly Dictionary<string, DragonMaterial> _materials = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonConstruction> _constructions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonGlazing> _glazings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonNoMassConstruction> _doorConstructions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonProfile> _profiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonHvacSource> _sources = new(StringComparer.Ordinal);
        private readonly List<GreenRetrofitSurfaceConversion> _surfaceConversions = new();

        public Converter(GreenRetrofitModel model, GreenRetrofitConversionOptions options)
        {
            _model = model;
            _options = options;
        }

        public GreenRetrofitConversionResult Convert()
        {
            try
            {
                WeatherSelection? weather = ResolveWeather();
                Dictionary<string, List<DragonSurface>> surfacesByZone = _model.Zones.ToDictionary(
                    zone => zone.Id.Value,
                    _ => new List<DragonSurface>(),
                    StringComparer.Ordinal);

                AddSurfaces(weather, surfacesByZone);

                if (_diagnostics.Any(item => item.IsFailure))
                {
                    return Result(null, weather);
                }

                DragonZone[] zones = _model.Zones
                    .Select(zone => ConvertZone(zone, surfacesByZone[zone.Id.Value]))
                    .ToArray();
                IReadOnlyList<DragonZoneHvacAssignment> hvac = ConvertHvacAssignments();
                IReadOnlyList<DragonZoneVentilationAssignment> ventilation = ConvertVentilationAssignments();
                IReadOnlyList<DragonPhotovoltaicPanel> photovoltaic = _model.PhotovoltaicSystems
                    .Select(ConvertPhotovoltaic)
                    .ToArray();
                var energyModel = new EnergyModel(
                    _model.Name,
                    zones,
                    hvac,
                    ventilation,
                    photovoltaic,
                    _model.NorthAxis,
                    ConvertTerrain(weather?.Terrain));

                if (_options.IncludeModelValidationDiagnostics)
                {
                    _diagnostics.AddRange(energyModel.Validate().Diagnostics);
                }

                return Result(energyModel, weather);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is KeyNotFoundException)
            {
                Error(
                    "SD.CONVERSION.DOMAIN_INVALID",
                    "The GRM values could not form an InvisibleDragon model: " + exception.Message,
                    "Correct the reported domain value or relationship and retry.");
                return Result(null, _model.Weather);
            }
        }

        private GreenRetrofitConversionResult Result(EnergyModel? model, WeatherSelection? weather)
        {
            GreenRetrofitSurfaceConversion[] surfaceConversions = model is null
                ? Array.Empty<GreenRetrofitSurfaceConversion>()
                : _surfaceConversions
                    .OrderBy(item => item.ConvertedZoneId.Value, StringComparer.Ordinal)
                    .ThenBy(item => item.ConvertedSurfaceId.Value, StringComparer.Ordinal)
                    .ToArray();
            return new GreenRetrofitConversionResult(
                model,
                weather,
                new ReadOnlyCollection<Diagnostic>(_diagnostics.ToArray()),
                new ReadOnlyCollection<GreenRetrofitSurfaceConversion>(surfaceConversions));
        }

        private WeatherSelection? ResolveWeather()
        {
            if (_model.Weather is not null)
            {
                return _model.Weather;
            }

            LookupResult<WeatherSelection> lookup = _options.Database.Weather.FindByAddress(
                _model.Address,
                _model.Vintage);
            _diagnostics.AddRange(lookup.Diagnostics);
            return lookup.Value;
        }

        private void AddSurfaces(
            WeatherSelection? weather,
            Dictionary<string, List<DragonSurface>> surfacesByZone)
        {
            var zonesById = _model.Zones.ToDictionary(
                zone => zone.Id.Value,
                StringComparer.Ordinal);
            var adjacencyEntries = new List<SurfaceEntry>();
            foreach (Zone zone in _model.Zones)
            {
                for (int index = 0; index < zone.Surfaces.Count; index++)
                {
                    Surface surface = zone.Surfaces[index];
                    if (surface.BoundaryCondition != SurfaceBoundaryCondition.Zone)
                    {
                        AddNonAdjacentSurface(zone, surface, weather, surfacesByZone);
                        continue;
                    }

                    string? adjacentZoneId = surface.AdjacentZoneId;
                    if (string.IsNullOrWhiteSpace(adjacentZoneId)
                        || !zonesById.TryGetValue(adjacentZoneId!, out Zone? adjacentZone))
                    {
                        Error(
                            "SD.CONVERSION.ADJACENT_ZONE_NOT_FOUND",
                            "Surface '" + surface.Id.Value + "' references an unavailable adjacent zone.",
                            "Use the ID of a zone contained in the GRM building.",
                            surface.Id);
                        continue;
                    }

                    if (StringComparer.Ordinal.Equals(zone.Id.Value, adjacentZone.Id.Value))
                    {
                        Error(
                            "SD.CONVERSION.ADJACENCY_SELF_REFERENCE",
                            "Surface '" + surface.Id.Value + "' references its own zone as adjacent.",
                            "Reference a distinct zone or use an adiabatic boundary.",
                            surface.Id);
                        continue;
                    }

                    adjacencyEntries.Add(new SurfaceEntry(zone, surface, adjacentZone, index));
                }
            }

            foreach (IGrouping<ZonePairKey, SurfaceEntry> group in adjacencyEntries
                .GroupBy(entry => ZonePairKey.Create(entry.Zone.Id, entry.AdjacentZone.Id))
                .OrderBy(item => item.Key.FirstZoneId, StringComparer.Ordinal)
                .ThenBy(item => item.Key.SecondZoneId, StringComparer.Ordinal))
            {
                NormalizeAdjacencyGroup(group.Key, group.ToArray(), weather, surfacesByZone);
            }
        }

        private void AddNonAdjacentSurface(
            Zone zone,
            Surface surface,
            WeatherSelection? weather,
            Dictionary<string, List<DragonSurface>> surfacesByZone)
        {
            int diagnosticStart = _diagnostics.Count;
            DragonPlanarPolygon polygon = CreatePolygon(surface, zone.Height);
            DragonSurfaceConstruction? construction = ResolveConstruction(zone, surface, weather);
            if (construction is null)
            {
                return;
            }

            IReadOnlyList<DragonOpening> openings = ConvertOpenings(surface, polygon);
            if (_diagnostics.Skip(diagnosticStart).Any(item => item.IsFailure))
            {
                return;
            }

            surfacesByZone[zone.Id.Value].Add(new DragonSurface(
                surface.Id,
                surface.Id.Value,
                ConvertSurfaceType(surface.Type),
                construction,
                ConvertBoundary(surface.BoundaryCondition),
                polygon,
                openings));
            AddSurfaceConversion(zone.Id, surface.Id, zone.Id, surface.Id, false);
        }

        private void NormalizeAdjacencyGroup(
            ZonePairKey key,
            IReadOnlyList<SurfaceEntry> entries,
            WeatherSelection? weather,
            Dictionary<string, List<DragonSurface>> surfacesByZone)
        {
            var first = entries
                .Where(entry => StringComparer.Ordinal.Equals(entry.Zone.Id.Value, key.FirstZoneId))
                .OrderBy(entry => entry.Surface.Id.Value, StringComparer.Ordinal)
                .ThenBy(entry => entry.SurfaceIndex)
                .ToList();
            var second = entries
                .Where(entry => StringComparer.Ordinal.Equals(entry.Zone.Id.Value, key.SecondZoneId))
                .OrderBy(entry => entry.Surface.Id.Value, StringComparer.Ordinal)
                .ThenBy(entry => entry.SurfaceIndex)
                .ToList();

            if (first.Count == 0 || second.Count == 0)
            {
                foreach (SurfaceEntry entry in first.Concat(second))
                {
                    AddOneSidedAdjacencySurface(entry, weather, surfacesByZone);
                }

                return;
            }

            var remainingFirst = new List<SurfaceEntry>(first);
            var remainingSecond = new List<SurfaceEntry>(second);
            while (true)
            {
                var pairs = remainingFirst
                    .Select(left => new
                    {
                        Left = left,
                        Candidates = remainingSecond
                            .Where(right => AreReciprocalGeometriesCompatible(left, right))
                            .ToArray(),
                    })
                    .Where(item => item.Candidates.Length == 1)
                    .Select(item => new { item.Left, Right = item.Candidates[0] })
                    .Where(pair => remainingFirst.Count(left =>
                        AreReciprocalGeometriesCompatible(left, pair.Right)) == 1)
                    .ToArray();
                if (pairs.Length == 0)
                {
                    break;
                }

                foreach (var pair in pairs)
                {
                    AddReciprocalAdjacencyPair(pair.Left, pair.Right, weather, surfacesByZone);
                    remainingFirst.Remove(pair.Left);
                    remainingSecond.Remove(pair.Right);
                }
            }

            if (remainingFirst.Count == 0 || remainingSecond.Count == 0)
            {
                foreach (SurfaceEntry entry in remainingFirst.Concat(remainingSecond))
                {
                    AddOneSidedAdjacencySurface(entry, weather, surfacesByZone);
                }

                return;
            }

            bool ambiguous = remainingFirst.Any(left => remainingSecond.Any(right =>
                AreReciprocalGeometriesCompatible(left, right)));
            string firstIds = string.Join(", ", remainingFirst.Select(item => item.Surface.Id.Value));
            string secondIds = string.Join(", ", remainingSecond.Select(item => item.Surface.Id.Value));
            if (ambiguous)
            {
                Error(
                    "SD.CONVERSION.ADJACENCY_AMBIGUOUS",
                    "Zone pair '" + key.FirstZoneId + "' / '" + key.SecondZoneId
                    + "' has multiple indistinguishable reciprocal surface candidates ("
                    + firstIds + " / " + secondIds + ").",
                    "Make each reciprocal pair unique by surface type, area, or opening layout.",
                    remainingFirst[0].Surface.Id);
            }
            else
            {
                Error(
                    "SD.CONVERSION.ADJACENCY_MISMATCH",
                    "Zone pair '" + key.FirstZoneId + "' / '" + key.SecondZoneId
                    + "' has reciprocal surface declarations whose types, areas, or opening layouts do not match ("
                    + firstIds + " / " + secondIds + ").",
                    "Make the two declarations geometrically equivalent, or remove the incorrect reciprocal declaration.",
                    remainingFirst[0].Surface.Id);
            }
        }

        private void AddOneSidedAdjacencySurface(
            SurfaceEntry entry,
            WeatherSelection? weather,
            Dictionary<string, List<DragonSurface>> surfacesByZone)
        {
            int diagnosticStart = _diagnostics.Count;
            Surface surface = entry.Surface;
            DragonPlanarPolygon polygon = CreatePolygon(surface, entry.Zone.Height);
            DragonSurfaceConstruction? construction = ResolveConstruction(entry.Zone, surface, weather);
            if (construction is null)
            {
                return;
            }

            IReadOnlyList<DragonOpening> openings = ConvertOpenings(surface, polygon);
            if (_diagnostics.Skip(diagnosticStart).Any(item => item.IsFailure))
            {
                return;
            }

            EntityId cloneId = new(ClonePrefix + surface.Id.Value);
            bool conflictsWithSource = _model.Zones
                .SelectMany(zone => zone.Surfaces)
                .Any(item => item.Id.Equals(cloneId));
            if (conflictsWithSource)
            {
                Error(
                    "SD.CONVERSION.ADJACENCY_SYNTHETIC_ID_CONFLICT",
                    "The synthesized reciprocal ID '" + cloneId.Value + "' conflicts with a source surface ID.",
                    "Rename the conflicting source surface so the reciprocal ID is unique.",
                    surface.Id);
                return;
            }

            DragonSurface original = new(
                surface.Id,
                surface.Id.Value,
                ConvertSurfaceType(surface.Type),
                construction,
                DragonSurfaceBoundary.AdjacentTo(cloneId),
                polygon,
                openings);
            DragonSurface clone = new(
                cloneId,
                cloneId.Value,
                FlipSurfaceType(surface.Type),
                ReverseConstruction(construction),
                DragonSurfaceBoundary.AdjacentTo(surface.Id),
                polygon.Reverse(),
                CloneOpenings(openings));
            surfacesByZone[entry.Zone.Id.Value].Add(original);
            surfacesByZone[entry.AdjacentZone.Id.Value].Add(clone);
            AddSurfaceConversion(entry.Zone.Id, surface.Id, entry.Zone.Id, surface.Id, false);
            AddSurfaceConversion(entry.Zone.Id, surface.Id, entry.AdjacentZone.Id, cloneId, true);
        }

        private void AddReciprocalAdjacencyPair(
            SurfaceEntry first,
            SurfaceEntry second,
            WeatherSelection? weather,
            Dictionary<string, List<DragonSurface>> surfacesByZone)
        {
            SurfaceEntry canonical = Compare(first, second) <= 0 ? first : second;
            SurfaceEntry counterpart = ReferenceEquals(canonical, first) ? second : first;
            if (canonical.Surface.Id.Equals(counterpart.Surface.Id))
            {
                Error(
                    "SD.CONVERSION.ADJACENCY_SURFACE_ID_CONFLICT",
                    "Reciprocal surfaces in distinct zones share ID '" + canonical.Surface.Id.Value + "'.",
                    "Assign a distinct stable ID to each side of the boundary.",
                    canonical.Surface.Id);
                return;
            }

            int diagnosticStart = _diagnostics.Count;
            DragonSurfaceConstruction? construction = ResolveConstruction(
                canonical.Zone,
                canonical.Surface,
                weather);
            DragonSurfaceConstruction? counterpartConstruction = ResolveConstruction(
                counterpart.Zone,
                counterpart.Surface,
                weather);
            if (construction is null || counterpartConstruction is null)
            {
                return;
            }

            if (!AreConvertedConstructionsCompatible(construction, counterpartConstruction))
            {
                Error(
                    "SD.CONVERSION.ADJACENCY_CONSTRUCTION_MISMATCH",
                    "Reciprocal surfaces '" + canonical.Surface.Id.Value + "' and '"
                    + counterpart.Surface.Id.Value + "' have incompatible constructions.",
                    "Use the same physical layer assembly on both sides of the boundary.",
                    canonical.Surface.Id);
                return;
            }

            DragonPlanarPolygon polygon = CreatePolygon(canonical.Surface, canonical.Zone.Height);
            IReadOnlyList<DragonOpening> openings = ConvertOpenings(canonical.Surface, polygon);
            IReadOnlyList<DragonOpening> counterpartOpenings = ConvertMirroredOpenings(
                canonical.Surface,
                counterpart.Surface,
                openings);
            if (_diagnostics.Skip(diagnosticStart).Any(item => item.IsFailure))
            {
                return;
            }

            DragonSurface convertedCanonical = new(
                canonical.Surface.Id,
                canonical.Surface.Id.Value,
                ConvertSurfaceType(canonical.Surface.Type),
                construction,
                DragonSurfaceBoundary.AdjacentTo(counterpart.Surface.Id),
                polygon,
                openings);
            DragonSurface convertedCounterpart = new(
                counterpart.Surface.Id,
                counterpart.Surface.Id.Value,
                FlipSurfaceType(canonical.Surface.Type),
                ReverseConstruction(construction),
                DragonSurfaceBoundary.AdjacentTo(canonical.Surface.Id),
                polygon.Reverse(),
                counterpartOpenings);
            surfacesByZone[canonical.Zone.Id.Value].Add(convertedCanonical);
            surfacesByZone[counterpart.Zone.Id.Value].Add(convertedCounterpart);
            AddSurfaceConversion(
                canonical.Zone.Id,
                canonical.Surface.Id,
                canonical.Zone.Id,
                canonical.Surface.Id,
                false);
            AddSurfaceConversion(
                counterpart.Zone.Id,
                counterpart.Surface.Id,
                counterpart.Zone.Id,
                counterpart.Surface.Id,
                false);
        }

        private DragonSurfaceConstruction? ResolveConstruction(
            Zone zone,
            Surface surface,
            WeatherSelection? weather)
        {
            switch (surface.ConstructionReferenceKind)
            {
                case SurfaceConstructionReferenceKind.Defined:
                    return ApplyCoolRoof(ConvertConstruction(surface.Construction!), surface);
                case SurfaceConstructionReferenceKind.Open:
                    return new DragonAirBoundary("DefaultAirBoundary");
                case SurfaceConstructionReferenceKind.Unresolved:
                    Error(
                        "SD.CONVERSION.CONSTRUCTION_REFERENCE_NOT_FOUND",
                        "Surface '" + surface.Id.Value + "' references missing construction '"
                        + surface.ConstructionId + "'.",
                        "Define the referenced construction before conversion.",
                        surface.Id);
                    return null;
                case SurfaceConstructionReferenceKind.Unknown:
                    return ResolveUnknownConstruction(zone, surface, weather);
                default:
                    throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private DragonSurfaceConstruction? ResolveUnknownConstruction(
            Zone zone,
            Surface surface,
            WeatherSelection? weather)
        {
            if (!_options.ResolveUnknownConstructions)
            {
                Error(
                    "SD.CONVERSION.UNKNOWN_CONSTRUCTION",
                    "Surface '" + surface.Id.Value + "' has no construction.",
                    "Enable regulated construction resolution or assign a construction.",
                    surface.Id);
                return null;
            }

            if (surface.Type == SurfaceType.Wall
                && (surface.BoundaryCondition == SurfaceBoundaryCondition.Zone
                    || surface.BoundaryCondition == SurfaceBoundaryCondition.AdjacentSpace))
            {
                return DefaultInnerWall();
            }

            if (weather is null)
            {
                Error(
                    "SD.CONVERSION.WEATHER_REQUIRED_FOR_CONSTRUCTION",
                    "Surface '" + surface.Id.Value + "' requires a regulated construction, but weather metadata is unavailable.",
                    "Resolve the Korean address and climate region or assign a construction.",
                    surface.Id);
                return null;
            }

            bool radiant = zone.SupplySystems.Any(system =>
                system.Type == SupplySystemType.RadiantFloor
                || system.Type == SupplySystemType.ElectricRadiantFloor);
            LookupResult<SurfaceConstruction> lookup = _options.Database.SurfaceConstructions.FindRegulated(
                _model.Vintage,
                surface.Type,
                surface.BoundaryCondition,
                weather.ClimateRegion,
                radiant,
                _model.IsMultifamilyHousing);
            if (!lookup.Found)
            {
                foreach (Diagnostic diagnostic in lookup.Diagnostics)
                {
                    _diagnostics.Add(new Diagnostic(
                        diagnostic.Code,
                        diagnostic.Severity,
                        diagnostic.Message,
                        surface.Id,
                        suggestedAction: diagnostic.SuggestedAction));
                }

                return null;
            }

            return ApplyCoolRoof(ConvertConstruction(lookup.Require()), surface);
        }

        private DragonConstruction DefaultInnerWall()
        {
            if (_constructions.TryGetValue(DefaultInnerWallName, out DragonConstruction? existing))
            {
                return existing;
            }

            Material gypsum = _options.Database.Materials.Find("gypsumboard").Require();
            Material glassWool = _options.Database.Materials.Find("glasswool").Require();
            var construction = new DragonConstruction(
                DefaultInnerWallName,
                new[]
                {
                    ConvertLayer(gypsum, 0.0125d, "DefaultInnerWall:Gypsum:Outside"),
                    ConvertLayer(glassWool, 0.05d, "DefaultInnerWall:GlassWool"),
                    ConvertLayer(gypsum, 0.0125d, "DefaultInnerWall:Gypsum:Inside"),
                });
            _constructions.Add(DefaultInnerWallName, construction);
            return construction;
        }

        private DragonConstruction ConvertConstruction(SurfaceConstruction construction)
        {
            if (_constructions.TryGetValue(construction.Id.Value, out DragonConstruction? existing))
            {
                return existing;
            }

            var converted = new DragonConstruction(
                construction.Id.Value,
                construction.Layers.Select(layer => ConvertLayer(
                    layer.Material,
                    layer.Thickness,
                    layer.Material.Id.Value + "_"
                    + FormatPythonFloat(layer.Thickness * UnitConversions.MetresToMillimetres)
                    + "mm")));
            _constructions.Add(construction.Id.Value, converted);
            return converted;
        }

        private static string FormatPythonFloat(double value)
        {
            return CanonicalDouble.FormatPythonFloat(value);
        }

        private DragonLayer ConvertLayer(Material material, double thickness, string layerName)
        {
            if (!_materials.TryGetValue(material.Id.Value, out DragonMaterial? converted))
            {
                converted = new DragonMaterial(
                    material.Id.Value,
                    material.Conductivity,
                    material.Density,
                    material.SpecificHeat);
                _materials.Add(material.Id.Value, converted);
            }

            return new DragonLayer(layerName, converted, thickness);
        }

        private static DragonSurfaceConstruction ApplyCoolRoof(
            DragonSurfaceConstruction construction,
            Surface surface)
        {
            if (!surface.CoolRoofReflectance.HasValue || construction is not DragonConstruction opaque)
            {
                return construction;
            }

            DragonLayer outside = opaque.Layers[0];
            double absorptance = 1d - surface.CoolRoofReflectance.Value;
            var material = new DragonMaterial(
                "$FOR_COOLROOF$:" + outside.Material.Name,
                outside.Material.ConductivityWattsPerMetreKelvin,
                outside.Material.DensityKilogramsPerCubicMetre,
                outside.Material.SpecificHeatJoulesPerKilogramKelvin,
                absorptance,
                absorptance,
                outside.Material.VisibleAbsorptance,
                outside.Material.Roughness);
            var layer = new DragonLayer(
                "$FOR_COOLROOF$:" + outside.Name,
                material,
                outside.ThicknessMetres);
            return new DragonConstruction(
                "$FOR_COOLROOF$:" + opaque.Name,
                new[] { layer }.Concat(opaque.Layers.Skip(1)));
        }

        private DragonGlazing ConvertGlazing(FenestrationConstruction construction)
        {
            if (!_glazings.TryGetValue(construction.Id.Value, out DragonGlazing? glazing))
            {
                glazing = new DragonGlazing(
                    construction.Id.Value,
                    construction.UValue,
                    construction.SolarHeatGainCoefficient!.Value);
                _glazings.Add(construction.Id.Value, glazing);
            }

            return glazing;
        }

        private DragonNoMassConstruction ConvertDoorConstruction(FenestrationConstruction construction)
        {
            if (!_doorConstructions.TryGetValue(
                    construction.Id.Value,
                    out DragonNoMassConstruction? converted))
            {
                converted = new DragonNoMassConstruction(construction.Id.Value, construction.UValue);
                _doorConstructions.Add(construction.Id.Value, converted);
            }

            return converted;
        }

        private IReadOnlyList<DragonOpening> ConvertOpenings(
            Surface surface,
            DragonPlanarPolygon host)
        {
            if (surface.Fenestrations.Count == 0)
            {
                return Array.Empty<DragonOpening>();
            }

            if (surface.Fenestrations.Sum(item => item.Area) >= surface.Area)
            {
                Error(
                    "SD.CONVERSION.OPENING_AREA_EXCEEDS_SURFACE",
                    "The openings on surface '" + surface.Id.Value + "' consume all or more of the host area.",
                    "Reduce the total opening area below the surface area.",
                    surface.Id);
                return Array.Empty<DragonOpening>();
            }

            IReadOnlyList<DragonPlanarPolygon> polygons = CreateOpeningPolygons(
                host,
                surface.Fenestrations.Select(item => item.Area).ToArray());
            var openings = new List<DragonOpening>(surface.Fenestrations.Count);
            for (int index = 0; index < surface.Fenestrations.Count; index++)
            {
                Fenestration opening = surface.Fenestrations[index];
                if (opening.Construction is null)
                {
                    Error(
                        "SD.CONVERSION.FENESTRATION_CONSTRUCTION_NOT_FOUND",
                        "Opening '" + opening.Id.Value + "' references missing construction '"
                        + opening.ConstructionId + "'.",
                        "Define the referenced fenestration construction before conversion.",
                        opening.Id);
                    continue;
                }

                if (opening.Type == FenestrationType.Door)
                {
                    openings.Add(new DragonDoor(
                        opening.Id,
                        opening.Id.Value,
                        ConvertDoorConstruction(opening.Construction),
                        polygons[index]));
                }
                else
                {
                    openings.Add(new DragonWindow(
                        opening.Id,
                        opening.Id.Value,
                        ConvertGlazing(opening.Construction),
                        polygons[index],
                        ConvertShading(opening.Blind)));
                }
            }

            return openings.AsReadOnly();
        }

        private IReadOnlyList<DragonOpening> ConvertMirroredOpenings(
            Surface source,
            Surface counterpart,
            IReadOnlyList<DragonOpening> convertedSource)
        {
            if (convertedSource.Count != source.Fenestrations.Count)
            {
                return Array.Empty<DragonOpening>();
            }

            var remaining = counterpart.Fenestrations
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                .ToList();
            var converted = new List<DragonOpening>(convertedSource.Count);
            for (int index = 0; index < source.Fenestrations.Count; index++)
            {
                Fenestration sourceOpening = source.Fenestrations[index];
                Fenestration[] geometryCandidates = remaining
                    .Where(item => AreOpeningGeometriesCompatible(sourceOpening, item))
                    .ToArray();
                Fenestration? counterpartOpening = geometryCandidates
                    .FirstOrDefault(item => AreOpeningDefinitionsCompatible(sourceOpening, item))
                    ?? geometryCandidates.FirstOrDefault();
                if (counterpartOpening is null)
                {
                    Error(
                        "SD.CONVERSION.ADJACENCY_OPENING_MISMATCH",
                        "Opening '" + sourceOpening.Id.Value + "' has no matching opening on reciprocal surface '"
                        + counterpart.Id.Value + "'.",
                        "Mirror the opening type and area on both sides of the boundary.",
                        sourceOpening.Id);
                    continue;
                }

                remaining.Remove(counterpartOpening);
                if (!AreOpeningDefinitionsCompatible(sourceOpening, counterpartOpening))
                {
                    Error(
                        "SD.CONVERSION.ADJACENCY_OPENING_DEFINITION_MISMATCH",
                        "Reciprocal openings '" + sourceOpening.Id.Value + "' and '"
                        + counterpartOpening.Id.Value + "' have incompatible constructions or shading.",
                        "Use equivalent opening construction and shading definitions on both sides.",
                        sourceOpening.Id);
                    continue;
                }

                if (sourceOpening.Id.Equals(counterpartOpening.Id))
                {
                    Error(
                        "SD.CONVERSION.ADJACENCY_OPENING_ID_CONFLICT",
                        "Reciprocal openings share ID '" + sourceOpening.Id.Value + "'.",
                        "Assign a distinct stable ID to each opening side.",
                        sourceOpening.Id);
                    continue;
                }

                if (counterpartOpening.Construction is null)
                {
                    Error(
                        "SD.CONVERSION.FENESTRATION_CONSTRUCTION_NOT_FOUND",
                        "Opening '" + counterpartOpening.Id.Value + "' references missing construction '"
                        + counterpartOpening.ConstructionId + "'.",
                        "Define the referenced fenestration construction before conversion.",
                        counterpartOpening.Id);
                    continue;
                }

                DragonPlanarPolygon polygon = convertedSource[index].Polygon.Reverse();
                if (counterpartOpening.Type == FenestrationType.Door)
                {
                    converted.Add(new DragonDoor(
                        counterpartOpening.Id,
                        counterpartOpening.Id.Value,
                        ConvertDoorConstruction(counterpartOpening.Construction),
                        polygon));
                }
                else
                {
                    converted.Add(new DragonWindow(
                        counterpartOpening.Id,
                        counterpartOpening.Id.Value,
                        ConvertGlazing(counterpartOpening.Construction),
                        polygon,
                        ConvertShading(counterpartOpening.Blind)));
                }
            }

            return converted.AsReadOnly();
        }

        private static Dragons.InvisibleDragon.Shape.IShadingDevice? ConvertShading(BlindType? blind)
        {
            return blind switch
            {
                BlindType.Shade => new DragonShade("default_shade", 0.5d, 0.4d),
                BlindType.Venetian => new DragonBlind("default_blind", 0.05d, 0.05d, 90d, 0.5d, 0.5d),
                null => null,
                _ => throw new ArgumentOutOfRangeException(nameof(blind)),
            };
        }

        private static DragonOpening[] CloneOpenings(IEnumerable<DragonOpening> openings)
        {
            return openings.Select(opening =>
            {
                EntityId id = new(ClonePrefix + opening.Id.Value);
                return opening switch
                {
                    DragonWindow window => (DragonOpening)new DragonWindow(
                        id,
                        id.Value,
                        window.Glazing,
                        window.Polygon.Reverse(),
                        window.Shading),
                    DragonDoor door => new DragonDoor(
                        id,
                        id.Value,
                        door.Construction,
                        door.Polygon.Reverse()),
                    _ => throw new ArgumentException("Unsupported opening type.", nameof(openings)),
                };
            }).ToArray();
        }

        private DragonZone ConvertZone(Zone zone, IEnumerable<DragonSurface> surfaces)
        {
            UsageProfile? profile = zone.Profile;
            if (profile is null)
            {
                Error(
                    "SD.CONVERSION.PROFILE_NOT_FOUND",
                    "Zone '" + zone.Id.Value + "' references unavailable profile '" + zone.ProfileName + "'.",
                    "Use a profile present in the packaged database.",
                    zone.Id);
                profile = _options.Database.UsageProfiles.Find(zone.ProfileName).Value;
            }

            if (profile is null)
            {
                throw new InvalidOperationException("A zone profile could not be resolved.");
            }

            double outdoorAir = profile.Ventilation * zone.Area
                / UnitConversions.CubicMetresPerSecondToPerHour;
            return new DragonZone(
                zone.Id,
                zone.Id.Value,
                surfaces,
                ConvertProfileCached(profile),
                zone.Infiltration * UnitConversions.AirChangesAt50PaToNaturalAirChanges,
                zone.LightDensity ?? 0d,
                outdoorAir);
        }

        private DragonProfile ConvertProfileCached(UsageProfile profile)
        {
            if (_profiles.TryGetValue(profile.Id.Value, out DragonProfile? existing))
            {
                return existing;
            }

            DragonProfile converted = CreateProfile(profile);
            _profiles.Add(profile.Id.Value, converted);
            return converted;
        }

        internal static DragonProfile CreateProfile(UsageProfile profile)
        {
            string prefix = profile.Source switch
            {
                UsageProfileSource.Standard => "$FROM_DB$:" + profile.Name,
                UsageProfileSource.Extended => "$FROM_DB$:" + profile.Name,
                UsageProfileSource.Custom => profile.Id.Value,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(profile),
                    profile.Source,
                    "Unknown usage-profile source."),
            };
            const string hvacVacationMask = "0xAUTO0000:INVERTED";
            const string occupiedVacationMask = "0xAUTO0001:INVERTED";
            const string lightingVacationMask = "0xAUTO0002:INVERTED";
            string occupiedMaskName = prefix + "-Occupied:AND:" + occupiedVacationMask;
            double occupantFactor = profile.Occupancy
                / profile.OccupiedHours
                / UsageProfileConstants.PeopleSensibleActivityWattsPerPerson;
            double equipmentFactor = profile.Equipment / profile.OccupiedHours;
            double hotWaterFactor = profile.DomesticHotWater
                / profile.OccupiedHours
                / UsageProfileConstants.DomesticHotWaterHeatWattHoursPerLitre;
            DragonSchedule occupied = WeeklyWindowSchedule(
                occupiedMaskName + ":MUL:" + FormatPythonFloat(occupantFactor),
                profile,
                profile.OccupantStart,
                profile.OccupantEnd,
                occupantFactor,
                DragonScheduleType.Real);
            DragonSchedule hvac = WeeklyWindowSchedule(
                prefix + "-HVACOperating:AND:" + hvacVacationMask,
                profile,
                profile.HvacStart,
                profile.HvacEnd,
                1d,
                DragonScheduleType.OnOff);
            DragonSchedule equipment = WeeklyWindowSchedule(
                occupiedMaskName + ":MUL:" + FormatPythonFloat(equipmentFactor),
                profile,
                profile.OccupantStart,
                profile.OccupantEnd,
                equipmentFactor,
                DragonScheduleType.Real);
            DragonSchedule hotWater = WeeklyWindowSchedule(
                occupiedMaskName + ":MUL:" + FormatPythonFloat(hotWaterFactor),
                profile,
                profile.OccupantStart,
                profile.OccupantEnd,
                hotWaterFactor,
                DragonScheduleType.Real);
            DragonSchedule lighting = LightingSchedule(
                prefix + "-Lighted:MUL:" + lightingVacationMask,
                profile);
            var converted = new DragonProfile(
                profile.Id,
                prefix,
                DragonSchedule.Constant(
                    prefix + "-HeatingSetpoint",
                    profile.HeatingSetpoint,
                    DragonScheduleType.Temperature),
                DragonSchedule.Constant(
                    prefix + "-CoolingSetpoint",
                    profile.CoolingSetpoint,
                    DragonScheduleType.Temperature),
                hvac,
                occupied,
                lighting,
                equipment,
                hotWater);
            return converted;
        }

        private static DragonSchedule WeeklyWindowSchedule(
            string name,
            UsageProfile profile,
            int startHour,
            int endHour,
            double activeValue,
            DragonScheduleType type)
        {
            DaySchedule active = WindowDay(name + ":active", startHour, endHour, activeValue, type);
            DaySchedule off = DaySchedule.Constant(name + ":off", 0d, type);
            RuleSet weekly = WeeklyRuleSet(name + ":weekly", profile, active, off, type);
            RuleSet vacation = VacationRuleSet(name + ":vacation", weekly, off, type);
            return ApplyVacations(new DragonSchedule(name, Enumerable.Repeat(weekly, DragonSchedule.FixedLength), type), profile, vacation);
        }

        private static DragonSchedule LightingSchedule(string name, UsageProfile profile)
        {
            if (profile.LightingHours > profile.OccupiedHours)
            {
                throw new InvalidOperationException(
                    "Profile '" + profile.Name + "' cannot allocate lighting hours outside occupied time.");
            }

            DaySchedule active = LightingDay(name + ":active", profile);
            DaySchedule off = DaySchedule.Constant(name + ":off", 0d, DragonScheduleType.Fraction);
            RuleSet weekly = WeeklyRuleSet(name + ":weekly", profile, active, off, DragonScheduleType.Fraction);
            RuleSet vacation = VacationRuleSet(
                name + ":vacation",
                weekly,
                off,
                DragonScheduleType.Fraction);
            return ApplyVacations(
                new DragonSchedule(name, Enumerable.Repeat(weekly, DragonSchedule.FixedLength), DragonScheduleType.Fraction),
                profile,
                vacation);
        }

        private static RuleSet VacationRuleSet(
            string name,
            RuleSet weekly,
            DaySchedule off,
            DragonScheduleType type)
        {
            DaySchedule overrideOff = DaySchedule.Constant(name + ":override", 0d, type);
            DaySchedule? Preserve(DaySchedule? candidate, DaySchedule fallback)
            {
                return candidate is not null && !candidate.Equals(fallback)
                    ? overrideOff
                    : null;
            }

            return new RuleSet(
                name,
                off,
                off,
                monday: Preserve(weekly.Monday, weekly.Weekdays),
                tuesday: Preserve(weekly.Tuesday, weekly.Weekdays),
                wednesday: Preserve(weekly.Wednesday, weekly.Weekdays),
                thursday: Preserve(weekly.Thursday, weekly.Weekdays),
                friday: Preserve(weekly.Friday, weekly.Weekdays),
                saturday: Preserve(weekly.Saturday, weekly.Weekends),
                sunday: Preserve(weekly.Sunday, weekly.Weekends),
                holiday: Preserve(weekly.Holiday, weekly.Weekends),
                type: type);
        }

        private static DragonSchedule ApplyVacations(
            DragonSchedule schedule,
            UsageProfile profile,
            RuleSet vacation)
        {
            DragonSchedule result = schedule;
            foreach (VacationPeriod period in profile.Vacations)
            {
                DateTime start = ToScheduleDate(period.Start);
                DateTime end = ToScheduleDate(period.End);
                if (end < start)
                {
                    // Preserve Python 0.7.0 Schedule.from_windows: a reversed
                    // annual window assigns no slots rather than wrapping.
                    continue;
                }

                result = result.Apply(vacation, start, end, schedule.Name);
            }

            return result;
        }

        private static DateTime ToScheduleDate(MonthDay value)
        {
            return new DateTime(DragonSchedule.DefaultYear, value.Month, value.Day);
        }

        private static RuleSet WeeklyRuleSet(
            string name,
            UsageProfile profile,
            DaySchedule active,
            DaySchedule off,
            DragonScheduleType type)
        {
            DaySchedule? OverrideFor(UsageDay day) => profile.OperatesOn(day) ? active : null;
            return new RuleSet(
                name,
                off,
                off,
                monday: OverrideFor(UsageDay.Monday),
                tuesday: OverrideFor(UsageDay.Tuesday),
                wednesday: OverrideFor(UsageDay.Wednesday),
                thursday: OverrideFor(UsageDay.Thursday),
                friday: OverrideFor(UsageDay.Friday),
                saturday: OverrideFor(UsageDay.Saturday),
                sunday: OverrideFor(UsageDay.Sunday),
                holiday: OverrideFor(UsageDay.Holiday),
                type: type);
        }

        private static DaySchedule WindowDay(
            string name,
            int startHour,
            int endHour,
            double activeValue,
            DragonScheduleType type)
        {
            if (startHour == endHour || (startHour == 0 && endHour == 24))
            {
                return DaySchedule.Constant(name, activeValue, type);
            }

            var windows = new List<DayScheduleWindow>();
            if (endHour > startHour)
            {
                windows.Add(new DayScheduleWindow(
                    TimeSpan.FromHours(startHour),
                    TimeSpan.FromHours(endHour),
                    activeValue));
            }
            else
            {
                if (endHour > 0)
                {
                    windows.Add(new DayScheduleWindow(
                        TimeSpan.Zero,
                        TimeSpan.FromHours(endHour),
                        activeValue));
                }

                if (startHour < 24)
                {
                    windows.Add(new DayScheduleWindow(
                        TimeSpan.FromHours(startHour),
                        TimeSpan.FromHours(24),
                        activeValue));
                }
            }

            return DaySchedule.FromWindows(name, 0d, windows, type);
        }

        private static DaySchedule LightingDay(string name, UsageProfile profile)
        {
            bool IsOccupied(int interval)
            {
                double hour = interval / (double)DaySchedule.IntervalsPerHour;
                if (profile.OccupantStart == profile.OccupantEnd
                    || (profile.OccupantStart == 0 && profile.OccupantEnd == 24))
                {
                    return true;
                }

                return profile.OccupantEnd > profile.OccupantStart
                    ? hour >= profile.OccupantStart && hour < profile.OccupantEnd
                    : hour >= profile.OccupantStart || hour < profile.OccupantEnd;
            }

            int[] occupied = Enumerable.Range(0, DaySchedule.FixedLength)
                .Where(IsOccupied)
                .ToArray();
            double remaining = profile.LightingHours * DaySchedule.IntervalsPerHour;
            if (remaining > occupied.Length)
            {
                throw new InvalidOperationException(
                    "Profile '" + profile.Name + "' has more lighting intervals than occupied intervals.");
            }

            double[] values = new double[DaySchedule.FixedLength];
            IEnumerable<IGrouping<double, int>> normalGroups = occupied
                .Where(index => index >= 6 * DaySchedule.IntervalsPerHour)
                .GroupBy(CircularDistanceToNoon)
                .OrderByDescending(group => group.Key);
            foreach (IGrouping<double, int> group in normalGroups)
            {
                if (remaining <= 0d)
                {
                    break;
                }

                int[] tied = group.ToArray();
                if (remaining >= tied.Length)
                {
                    foreach (int index in tied)
                    {
                        values[index] = 1d;
                    }

                    remaining -= tied.Length;
                }
                else
                {
                    double value = remaining / tied.Length;
                    foreach (int index in tied)
                    {
                        values[index] = value;
                    }

                    remaining = 0d;
                }
            }

            foreach (int index in occupied.Where(
                index => index < 6 * DaySchedule.IntervalsPerHour))
            {
                if (remaining <= 0d)
                {
                    break;
                }

                double value = Math.Min(1d, remaining);
                values[index] = value;
                remaining -= value;
            }

            if (remaining > 1.0e-9d)
            {
                throw new InvalidOperationException(
                    "Profile '" + profile.Name + "' could not allocate all lighting intervals.");
            }

            return new DaySchedule(name, values, DragonScheduleType.Fraction);
        }

        private static double CircularDistanceToNoon(int interval)
        {
            double midpointMinutes = ((interval + 0.5d) * 60d / DaySchedule.IntervalsPerHour) % 1440d;
            double delta = Math.Abs(midpointMinutes - 720d);
            return Math.Min(delta, 1440d - delta);
        }

        private ReadOnlyCollection<DragonZoneHvacAssignment> ConvertHvacAssignments()
        {
            var assignments = new List<DragonZoneHvacAssignment>();
            foreach (Zone zone in _model.Zones)
            {
                var systems = new List<DragonHvacSupply>();
                foreach (SupplySystem supply in zone.SupplySystems)
                {
                    DragonHvacSupply? converted = ConvertSupply(supply);
                    if (converted is not null)
                    {
                        systems.Add(converted);
                    }
                }

                if (systems.Count > 0)
                {
                    assignments.Add(new DragonZoneHvacAssignment(
                        zone.Id,
                        new DragonSupplyGroup(systems)));
                }
            }

            return assignments.AsReadOnly();
        }

        private DragonHvacSupply? ConvertSupply(SupplySystem supply)
        {
            switch (supply.Type)
            {
                case SupplySystemType.PackagedAirConditioner:
                    double cop = supply.CoolingCop ?? 3d;
                    Warning(
                        "SD.CONVERSION.PACKAGED_AC_HEATING_COP_APPROXIMATED",
                        "Packaged air conditioner '" + supply.Id.Value
                        + "' uses a neutral heating COP of 1.0 on its cooling-only InvisibleDragon source."
                        + " The pinned upstream model leaves this value unset, but the C# heat-pump domain requires"
                        + " a positive value.",
                        "Treat the value as an inert compatibility placeholder; the converted terminal cannot heat.",
                        supply.Id);
                    var dedicated = new DragonHeatPump(
                        new EntityId("DedicatedHeatPump_for_" + supply.Id.Value),
                        "DedicatedHeatPump0xAUTO0000_for_" + supply.Id.Value,
                        DragonFuel.Electricity,
                        1d,
                        cop,
                        0.001d,
                        supply.CoolingCapacity);
                    return new Dragons.InvisibleDragon.Hvac.PackagedAirConditioner(
                        supply.Id,
                        supply.Id.Value,
                        dedicated);
                case SupplySystemType.AirHandlingUnit:
                    if (ConvertSource(supply.SourceSystem) is DragonHeatPump heatPump)
                    {
                        return new Dragons.InvisibleDragon.Hvac.AirHandlingUnit(
                            supply.Id,
                            supply.Id.Value,
                            heatPump);
                    }

                    return null;
                case SupplySystemType.RadiantFloor:
                    DragonHvacSource? source = ConvertSource(supply.SourceSystem);
                    return source is null ? null : new DragonRadiantFloor(supply.Id, supply.Id.Value, source);
                case SupplySystemType.ElectricRadiantFloor:
                    return new DragonElectricRadiantFloor(supply.Id, supply.Id.Value);
                case SupplySystemType.ElectricRadiator:
                    return new DragonElectricRadiator(
                        supply.Id,
                        supply.Id.Value,
                        supply.HeatingCapacity);
                case SupplySystemType.FanCoilUnit:
                    DragonHvacSource? fanCoilSource = ConvertSource(supply.SourceSystem);
                    return fanCoilSource is null
                        ? null
                        : new DragonFanCoilUnit(supply.Id, supply.Id.Value, fanCoilSource);
                case SupplySystemType.Radiator:
                    DragonHvacSource? radiatorSource = ConvertSource(supply.SourceSystem);
                    return radiatorSource is null
                        ? null
                        : new DragonRadiator(
                            supply.Id,
                            supply.Id.Value,
                            radiatorSource,
                            supply.HeatingCapacity);
                default:
                    throw new ArgumentOutOfRangeException(nameof(supply));
            }
        }

        private DragonHvacSource? ConvertSource(SourceSystem? source)
        {
            if (source is null)
            {
                Error(
                    "SD.CONVERSION.SOURCE_NOT_RESOLVED",
                    "An assigned supply system has no resolved source system.",
                    "Define and reference the source system before conversion.");
                return null;
            }

            if (_sources.TryGetValue(source.Id.Value, out DragonHvacSource? existing))
            {
                return existing;
            }

            DragonHvacSource? converted;
            switch (source.Type)
            {
                case SourceSystemType.HeatPump:
                    converted = new DragonHeatPump(
                        source.Id,
                        source.Id.Value,
                        ConvertFuel(source.FuelType),
                        source.HeatingCop ?? 3d,
                        source.CoolingCop ?? 3d,
                        source.HeatingCapacity,
                        source.CoolingCapacity);
                    break;
                case SourceSystemType.GeothermalHeatPump:
                    converted = new DragonGeothermalHeatPump(
                        source.Id,
                        source.Id.Value,
                        ConvertFuel(source.FuelType),
                        source.HeatingCop ?? 3d,
                        source.CoolingCop ?? 3d,
                        source.HeatingCapacity,
                        source.CoolingCapacity);
                    break;
                case SourceSystemType.Boiler:
                    converted = new DragonBoiler(
                        source.Id,
                        source.Id.Value,
                        ConvertFuel(source.FuelType),
                        source.Efficiency ?? 0.85d,
                        source.HeatingCapacity);
                    break;
                case SourceSystemType.DistrictHeating:
                    // Do not route district heat through ConvertFuel: it is an external
                    // thermal service, represented explicitly rather than as a local boiler fuel.
                    converted = new DragonDistrictHeating(
                        source.Id,
                        source.Id.Value,
                        source.HeatingCapacity);
                    break;
                case SourceSystemType.Chiller:
                    converted = new DragonChiller(
                        source.Id,
                        source.Id.Value,
                        source.CoolingCop ?? 3d,
                        ConvertCompressor(source.CompressorType),
                        ConvertCoolingTower(source),
                        source.CoolingCapacity);
                    break;
                case SourceSystemType.AbsorptionChiller:
                    string boilerName = "Boiler_for_" + source.Id.Value;
                    string towerName = "CoolingTower_for_" + source.Id.Value;
                    var generator = new DragonBoiler(
                        new EntityId(boilerName),
                        boilerName,
                        ConvertFuel(source.FuelType),
                        source.BoilerEfficiency ?? 0.85d);
                    var absorptionTower = new DragonOpenSingleSpeedCoolingTower(
                        new EntityId(towerName),
                        towerName,
                        source.CoolingCapacity);
                    converted = new DragonAbsorptionChiller(
                        source.Id,
                        source.Id.Value,
                        source.CoolingCop ?? 0.9d,
                        generator,
                        absorptionTower,
                        source.CoolingCapacity);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }

            _sources.Add(source.Id.Value, converted);
            return converted;
        }

        private static DragonCompressorType ConvertCompressor(CompressorType? compressor)
        {
            return compressor switch
            {
                CompressorType.Turbo => DragonCompressorType.Turbo,
                CompressorType.Screw => DragonCompressorType.Screw,
                CompressorType.Reciprocating => DragonCompressorType.Reciprocating,
                null => throw new ArgumentException("A chiller compressor type is required.", nameof(compressor)),
                _ => throw new ArgumentOutOfRangeException(nameof(compressor)),
            };
        }

        private static DragonCoolingTower ConvertCoolingTower(SourceSystem source)
        {
            string towerName = "CoolingTower_for_" + source.Id.Value;
            var towerId = new EntityId(towerName);
            return (source.CoolingTowerType, source.CoolingTowerControl) switch
            {
                (CoolingTowerType.Open, CoolingTowerControl.SingleSpeed) =>
                    new DragonOpenSingleSpeedCoolingTower(
                        towerId,
                        towerName,
                        source.CoolingTowerCapacity),
                (CoolingTowerType.Open, CoolingTowerControl.TwoSpeed) =>
                    new DragonOpenTwoSpeedCoolingTower(
                        towerId,
                        towerName,
                        source.CoolingTowerCapacity),
                (CoolingTowerType.Closed, CoolingTowerControl.SingleSpeed) =>
                    new DragonClosedSingleSpeedCoolingTower(
                        towerId,
                        towerName,
                        source.CoolingTowerCapacity),
                (CoolingTowerType.Closed, CoolingTowerControl.TwoSpeed) =>
                    new DragonClosedTwoSpeedCoolingTower(
                        towerId,
                        towerName,
                        source.CoolingTowerCapacity),
                _ => throw new ArgumentException(
                    "A chiller cooling-tower type and control are required.",
                    nameof(source)),
            };
        }

        private ReadOnlyCollection<DragonZoneVentilationAssignment> ConvertVentilationAssignments()
        {
            var assignments = new List<DragonZoneVentilationAssignment>();
            foreach (Zone zone in _model.Zones.Where(item => item.VentilationAssignments.Count > 0))
            {
                var resolved = zone.VentilationAssignments
                    .Where(item => item.VentilationSystem is not null)
                    .ToArray();
                if (resolved.Length != zone.VentilationAssignments.Count)
                {
                    Error(
                        "SD.CONVERSION.VENTILATION_NOT_RESOLVED",
                        "Zone '" + zone.Id.Value + "' contains an unresolved ventilation assignment.",
                        "Define every referenced ventilation system.",
                        zone.Id);
                    continue;
                }

                double totalFlow = resolved.Sum(item => item.VentilationSystem!.AirflowRate * item.Count);
                double heating = resolved.Sum(item =>
                    item.VentilationSystem!.HeatingEfficiency
                    * item.VentilationSystem.AirflowRate
                    * item.Count) / totalFlow;
                double cooling = resolved.Sum(item =>
                    item.VentilationSystem!.CoolingEfficiency
                    * item.VentilationSystem.AirflowRate
                    * item.Count) / totalFlow;
                var ventilator = new EnergyRecoveryVentilator(
                    new EntityId("ERV_for_" + zone.Id.Value),
                    "ERV_for_" + zone.Id.Value,
                    heating,
                    cooling,
                    totalFlow);
                assignments.Add(new DragonZoneVentilationAssignment(zone.Id, ventilator));
            }

            return assignments.AsReadOnly();
        }

        private static DragonPhotovoltaicPanel ConvertPhotovoltaic(PhotovoltaicSystem panel)
        {
            return new DragonPhotovoltaicPanel(
                panel.Id,
                panel.Id.Value,
                panel.Area,
                panel.Tilt,
                panel.Azimuth,
                panel.Efficiency);
        }

        private static DragonFuel ConvertFuel(FuelType? fuel)
        {
            return fuel switch
            {
                FuelType.Electricity => DragonFuel.Electricity,
                FuelType.NaturalGas => DragonFuel.NaturalGas,
                FuelType.LiquefiedPetroleumGas => DragonFuel.Propane,
                FuelType.Oil => DragonFuel.Diesel,
                FuelType.DistrictHeating => DragonFuel.OtherFuel1,
                null => throw new ArgumentException("A source fuel is required.", nameof(fuel)),
                _ => throw new ArgumentOutOfRangeException(nameof(fuel)),
            };
        }

        private static DragonTerrain ConvertTerrain(string? value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            foreach (DragonTerrain terrain in Enum.GetValues(typeof(DragonTerrain)))
            {
                if (string.Equals(terrain.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return terrain;
                }
            }

            return DragonTerrain.Suburbs;
        }

        private static DragonPlanarPolygon CreatePolygon(Surface surface, double zoneHeight)
        {
            if (surface.Type == SurfaceType.Floor)
            {
                double side = Math.Sqrt(surface.Area);
                return new DragonPlanarPolygon(new[]
                {
                    new DragonVertex(side / 2d, -side / 2d, 0d),
                    new DragonVertex(-side / 2d, -side / 2d, 0d),
                    new DragonVertex(-side / 2d, side / 2d, 0d),
                    new DragonVertex(side / 2d, side / 2d, 0d),
                });
            }

            if (surface.Type == SurfaceType.Ceiling)
            {
                double side = Math.Sqrt(surface.Area);
                return new DragonPlanarPolygon(new[]
                {
                    new DragonVertex(-side / 2d, side / 2d, zoneHeight),
                    new DragonVertex(-side / 2d, -side / 2d, zoneHeight),
                    new DragonVertex(side / 2d, -side / 2d, zoneHeight),
                    new DragonVertex(side / 2d, side / 2d, zoneHeight),
                });
            }

            double width = surface.Area / zoneHeight;
            double radians = surface.Azimuth.HasValue
                ? surface.Azimuth.Value * Math.PI / 180d
                : PythonReferenceAzimuthRadians(surface.Id.Value);
            double x = Math.Cos(radians - (1.5d * Math.PI)) * width / 2d;
            double y = Math.Sin(radians - (1.5d * Math.PI)) * width / 2d;
            return new DragonPlanarPolygon(new[]
            {
                new DragonVertex(x, -y, zoneHeight),
                new DragonVertex(x, -y, 0d),
                new DragonVertex(-x, y, 0d),
                new DragonVertex(-x, y, zoneHeight),
            });
        }

        private static ReadOnlyCollection<DragonPlanarPolygon> CreateOpeningPolygons(
            DragonPlanarPolygon host,
            IReadOnlyList<double> areas)
        {
            DragonVertex bottomLeft = host.Vertices[1];
            Dragons.InvisibleDragon.Shape.Vector3 verticalVector = host.Vertices[0] - bottomLeft;
            Dragons.InvisibleDragon.Shape.Vector3 horizontalVector = host.Vertices[2] - bottomLeft;
            double height = verticalVector.Length;
            double width = horizontalVector.Length;
            Dragons.InvisibleDragon.Shape.Vector3 vertical = verticalVector / height;
            Dragons.InvisibleDragon.Shape.Vector3 horizontal = horizontalVector / width;
            double openingHeight = height * 0.9d;
            double totalArea = areas.Sum();
            if ((totalArea / openingHeight) > width * 0.9d)
            {
                openingHeight = Math.Min(height * 0.999d, totalArea / (width * 0.9d));
            }

            double[] openingWidths = areas.Select(area => area / openingHeight).ToArray();
            double freeWidth = width - openingWidths.Sum();
            if (freeWidth <= 0d)
            {
                throw new ArgumentException("Opening geometry cannot fit inside its host surface.", nameof(areas));
            }

            double gap = freeWidth / (areas.Count + 1d);
            double verticalOffset = (height - openingHeight) / 2d;
            double offset = gap;
            var polygons = new List<DragonPlanarPolygon>(areas.Count);
            foreach (double openingWidth in openingWidths)
            {
                DragonVertex lowerLeft = bottomLeft
                    + (horizontal * offset)
                    + (vertical * verticalOffset);
                DragonVertex lowerRight = lowerLeft + (horizontal * openingWidth);
                DragonVertex upperLeft = lowerLeft + (vertical * openingHeight);
                DragonVertex upperRight = lowerRight + (vertical * openingHeight);
                polygons.Add(new DragonPlanarPolygon(new[]
                {
                    upperLeft,
                    lowerLeft,
                    lowerRight,
                    upperRight,
                }));
                offset += openingWidth + gap;
            }

            return polygons.AsReadOnly();
        }

        private static double PythonReferenceAzimuthRadians(string id)
        {
            bool isClone = id.StartsWith(ClonePrefix, StringComparison.Ordinal);
            string hashInput = isClone ? id.Replace(ClonePrefix, string.Empty) : id;
            long hash = PythonSeedZeroStringHash.Compute(hashInput);
            double radians = Math.Log10(Math.Abs((double)hash));
            return isClone
                ? (radians + Math.PI) % (2d * Math.PI)
                : radians % (2d * Math.PI);
        }

        private static bool AreReciprocalGeometriesCompatible(
            SurfaceEntry firstEntry,
            SurfaceEntry secondEntry)
        {
            Surface first = firstEntry.Surface;
            Surface second = secondEntry.Surface;
            if (!AreReciprocalTypes(first.Type, second.Type)
                || !NearlyEqual(first.Area, second.Area)
                || first.Type == SurfaceType.Wall && !NearlyEqual(firstEntry.Zone.Height, secondEntry.Zone.Height)
                || first.Fenestrations.Count != second.Fenestrations.Count)
            {
                return false;
            }

            var remaining = second.Fenestrations.ToList();
            foreach (Fenestration opening in first.Fenestrations)
            {
                Fenestration? match = remaining.FirstOrDefault(
                    candidate => AreOpeningGeometriesCompatible(opening, candidate));
                if (match is null)
                {
                    return false;
                }

                remaining.Remove(match);
            }

            return true;
        }

        private static bool AreReciprocalTypes(SurfaceType first, SurfaceType second)
        {
            return first == SurfaceType.Wall && second == SurfaceType.Wall
                || first == SurfaceType.Floor && second == SurfaceType.Ceiling
                || first == SurfaceType.Ceiling && second == SurfaceType.Floor;
        }

        private static bool AreOpeningGeometriesCompatible(Fenestration first, Fenestration second)
        {
            bool firstIsDoor = first.Type == FenestrationType.Door;
            bool secondIsDoor = second.Type == FenestrationType.Door;
            return firstIsDoor == secondIsDoor && NearlyEqual(first.Area, second.Area);
        }

        private static bool AreOpeningDefinitionsCompatible(Fenestration first, Fenestration second)
        {
            if (!AreOpeningGeometriesCompatible(first, second)
                || first.Blind != second.Blind
                || first.Construction is null
                || second.Construction is null)
            {
                return false;
            }

            return NearlyEqual(first.Construction.UValue, second.Construction.UValue)
                && NullableNearlyEqual(
                    first.Construction.SolarHeatGainCoefficient,
                    second.Construction.SolarHeatGainCoefficient);
        }

        private static bool AreConvertedConstructionsCompatible(
            DragonSurfaceConstruction first,
            DragonSurfaceConstruction second)
        {
            if (first is DragonAirBoundary && second is DragonAirBoundary)
            {
                return true;
            }

            if (first is not DragonConstruction firstOpaque
                || second is not DragonConstruction secondOpaque
                || firstOpaque.Layers.Count != secondOpaque.Layers.Count)
            {
                return first.GetType() == second.GetType()
                    && StringComparer.Ordinal.Equals(first.Name, second.Name);
            }

            return LayersMatch(firstOpaque.Layers, secondOpaque.Layers)
                || LayersMatch(firstOpaque.Layers, secondOpaque.Layers.Reverse().ToArray());
        }

        private static bool LayersMatch(
            IReadOnlyList<DragonLayer> first,
            IReadOnlyList<DragonLayer> second)
        {
            for (int index = 0; index < first.Count; index++)
            {
                DragonLayer left = first[index];
                DragonLayer right = second[index];
                if (!NearlyEqual(left.ThicknessMetres, right.ThicknessMetres)
                    || !NearlyEqual(
                        left.Material.ConductivityWattsPerMetreKelvin,
                        right.Material.ConductivityWattsPerMetreKelvin)
                    || !NearlyEqual(
                        left.Material.DensityKilogramsPerCubicMetre,
                        right.Material.DensityKilogramsPerCubicMetre)
                    || !NearlyEqual(
                        left.Material.SpecificHeatJoulesPerKilogramKelvin,
                        right.Material.SpecificHeatJoulesPerKilogramKelvin)
                    || !NearlyEqual(left.Material.ThermalAbsorptance, right.Material.ThermalAbsorptance)
                    || !NearlyEqual(left.Material.SolarAbsorptance, right.Material.SolarAbsorptance)
                    || !NearlyEqual(left.Material.VisibleAbsorptance, right.Material.VisibleAbsorptance)
                    || left.Material.Roughness != right.Material.Roughness)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NearlyEqual(double first, double second)
        {
            double scale = Math.Max(Math.Abs(first), Math.Abs(second));
            return Math.Abs(first - second) <= Math.Max(1e-6d, scale * 1e-9d);
        }

        private static bool NullableNearlyEqual(double? first, double? second)
        {
            return first.HasValue == second.HasValue
                && (!first.HasValue || NearlyEqual(first.Value, second!.Value));
        }

        private void AddSurfaceConversion(
            EntityId sourceZoneId,
            EntityId sourceSurfaceId,
            EntityId convertedZoneId,
            EntityId convertedSurfaceId,
            bool isSynthesizedCounterpart)
        {
            _surfaceConversions.Add(new GreenRetrofitSurfaceConversion(
                sourceZoneId,
                sourceSurfaceId,
                convertedZoneId,
                convertedSurfaceId,
                isSynthesizedCounterpart));
        }

        private static int Compare(SurfaceEntry first, SurfaceEntry second)
        {
            int surface = StringComparer.Ordinal.Compare(
                first.Surface.Id.Value,
                second.Surface.Id.Value);
            return surface != 0
                ? surface
                : StringComparer.Ordinal.Compare(first.Zone.Id.Value, second.Zone.Id.Value);
        }

        private static DragonSurfaceType ConvertSurfaceType(SurfaceType type)
        {
            return type switch
            {
                SurfaceType.Wall => DragonSurfaceType.Wall,
                SurfaceType.Ceiling => DragonSurfaceType.Ceiling,
                SurfaceType.Floor => DragonSurfaceType.Floor,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }

        private static DragonSurfaceType FlipSurfaceType(SurfaceType type)
        {
            return type switch
            {
                SurfaceType.Floor => DragonSurfaceType.Ceiling,
                SurfaceType.Ceiling => DragonSurfaceType.Floor,
                _ => DragonSurfaceType.Wall,
            };
        }

        private static DragonSurfaceBoundary ConvertBoundary(SurfaceBoundaryCondition condition)
        {
            return condition switch
            {
                SurfaceBoundaryCondition.Outdoors => DragonSurfaceBoundary.Outdoors,
                SurfaceBoundaryCondition.Ground => DragonSurfaceBoundary.Ground,
                SurfaceBoundaryCondition.Adiabatic => DragonSurfaceBoundary.Adiabatic,
                SurfaceBoundaryCondition.AdjacentSpace => DragonSurfaceBoundary.Adiabatic,
                _ => throw new ArgumentOutOfRangeException(nameof(condition)),
            };
        }

        private static DragonSurfaceConstruction ReverseConstruction(DragonSurfaceConstruction construction)
        {
            return construction is DragonConstruction opaque
                ? opaque.Reverse(ReversedPrefix + opaque.Name)
                : construction;
        }

        private sealed class SurfaceEntry
        {
            public SurfaceEntry(Zone zone, Surface surface, Zone adjacentZone, int surfaceIndex)
            {
                Zone = zone;
                Surface = surface;
                AdjacentZone = adjacentZone;
                SurfaceIndex = surfaceIndex;
            }

            public Zone Zone { get; }

            public Surface Surface { get; }

            public Zone AdjacentZone { get; }

            public int SurfaceIndex { get; }
        }

        private sealed class ZonePairKey : IEquatable<ZonePairKey>
        {
            private ZonePairKey(string firstZoneId, string secondZoneId)
            {
                FirstZoneId = firstZoneId;
                SecondZoneId = secondZoneId;
            }

            public string FirstZoneId { get; }

            public string SecondZoneId { get; }

            public static ZonePairKey Create(EntityId first, EntityId second)
            {
                return StringComparer.Ordinal.Compare(first.Value, second.Value) <= 0
                    ? new ZonePairKey(first.Value, second.Value)
                    : new ZonePairKey(second.Value, first.Value);
            }

            public bool Equals(ZonePairKey? other)
            {
                return other is not null
                    && StringComparer.Ordinal.Equals(FirstZoneId, other.FirstZoneId)
                    && StringComparer.Ordinal.Equals(SecondZoneId, other.SecondZoneId);
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as ZonePairKey);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(FirstZoneId) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(SecondZoneId);
                }
            }
        }

        private void Unsupported(
            string code,
            string message,
            EntityId id,
            string action)
        {
            Error(code, message, action, id);
        }

        private void Error(
            string code,
            string message,
            string action,
            EntityId? id = null)
        {
            _diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                message,
                id,
                suggestedAction: action));
        }

        private void Warning(
            string code,
            string message,
            string action,
            EntityId? id = null)
        {
            _diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Warning,
                message,
                id,
                suggestedAction: action));
        }
    }
}
