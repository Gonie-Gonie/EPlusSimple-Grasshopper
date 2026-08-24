using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.SimpleDragon.Internal;
using DragonAirBoundary = GonieGonie.InvisibleDragon.Construction.AirBoundary;
using DragonBlind = GonieGonie.InvisibleDragon.Shape.Blind;
using DragonBoiler = GonieGonie.InvisibleDragon.Hvac.Boiler;
using DragonConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using DragonDistrictHeating = GonieGonie.InvisibleDragon.Hvac.DistrictHeating;
using DragonDoor = GonieGonie.InvisibleDragon.Shape.Door;
using DragonElectricRadiantFloor = GonieGonie.InvisibleDragon.Hvac.ElectricRadiantFloor;
using DragonElectricRadiator = GonieGonie.InvisibleDragon.Hvac.ElectricRadiator;
using DragonFuel = GonieGonie.InvisibleDragon.Hvac.Fuel;
using DragonGlazing = GonieGonie.InvisibleDragon.Construction.Glazing;
using DragonHeatPump = GonieGonie.InvisibleDragon.Hvac.HeatPump;
using DragonHvacSource = GonieGonie.InvisibleDragon.Hvac.SourceSystem;
using DragonHvacSupply = GonieGonie.InvisibleDragon.Hvac.SupplySystem;
using DragonLayer = GonieGonie.InvisibleDragon.Construction.Layer;
using DragonMaterial = GonieGonie.InvisibleDragon.Construction.Material;
using DragonNoMassConstruction = GonieGonie.InvisibleDragon.Construction.NoMassConstruction;
using DragonOpening = GonieGonie.InvisibleDragon.Shape.IOpening;
using DragonPhotovoltaicPanel = GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel;
using DragonPlanarPolygon = GonieGonie.InvisibleDragon.Shape.PlanarPolygon;
using DragonProfile = GonieGonie.InvisibleDragon.Profile.Profile;
using DragonRadiantFloor = GonieGonie.InvisibleDragon.Hvac.RadiantFloor;
using DragonSchedule = GonieGonie.InvisibleDragon.Profile.Schedule;
using DragonScheduleType = GonieGonie.InvisibleDragon.Profile.ScheduleType;
using DragonShade = GonieGonie.InvisibleDragon.Shape.Shade;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;
using DragonSurfaceBoundary = GonieGonie.InvisibleDragon.Shape.SurfaceBoundary;
using DragonSurfaceConstruction = GonieGonie.InvisibleDragon.Construction.ISurfaceConstruction;
using DragonSurfaceType = GonieGonie.InvisibleDragon.Shape.SurfaceType;
using DragonSupplyGroup = GonieGonie.InvisibleDragon.Hvac.SupplyGroup;
using DragonTerrain = GonieGonie.InvisibleDragon.Model.Terrain;
using DragonVertex = GonieGonie.InvisibleDragon.Shape.Vertex;
using DragonWindow = GonieGonie.InvisibleDragon.Shape.Window;
using DragonZone = GonieGonie.InvisibleDragon.Shape.Zone;
using DragonZoneHvacAssignment = GonieGonie.InvisibleDragon.Hvac.ZoneHvacAssignment;
using DragonZoneVentilationAssignment = GonieGonie.InvisibleDragon.Hvac.ZoneVentilationAssignment;
using EnergyRecoveryVentilator = GonieGonie.InvisibleDragon.Hvac.EnergyRecoveryVentilator;
using RuleSet = GonieGonie.InvisibleDragon.Profile.RuleSet;
using DaySchedule = GonieGonie.InvisibleDragon.Profile.DaySchedule;
using DayScheduleWindow = GonieGonie.InvisibleDragon.Profile.DayScheduleWindow;

namespace GonieGonie.SimpleDragon;

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
/// A non-throwing conversion result that retains actionable compatibility diagnostics.
/// </summary>
public sealed class GreenRetrofitConversionResult
{
    internal GreenRetrofitConversionResult(
        EnergyModel? energyModel,
        WeatherSelection? weather,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        EnergyModel = energyModel;
        Weather = weather;
        Diagnostics = diagnostics;
    }

    public EnergyModel? EnergyModel { get; }

    public WeatherSelection? Weather { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

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

                foreach (Zone zone in _model.Zones)
                {
                    foreach (Surface surface in zone.Surfaces)
                    {
                        AddSurface(zone, surface, weather, surfacesByZone);
                    }
                }

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
            return new GreenRetrofitConversionResult(
                model,
                weather,
                new ReadOnlyCollection<Diagnostic>(_diagnostics.ToArray()));
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

        private void AddSurface(
            Zone zone,
            Surface surface,
            WeatherSelection? weather,
            Dictionary<string, List<DragonSurface>> surfacesByZone)
        {
            DragonPlanarPolygon polygon = CreatePolygon(surface, zone.Height);
            DragonSurfaceConstruction? construction = ResolveConstruction(zone, surface, weather);
            if (construction is null)
            {
                return;
            }

            IReadOnlyList<DragonOpening> openings = ConvertOpenings(surface, polygon);
            if (_diagnostics.Any(item => item.IsFailure && Equals(item.ObjectId, surface.Id)))
            {
                return;
            }

            if (surface.BoundaryCondition == SurfaceBoundaryCondition.Zone
                || surface.BoundaryCondition == SurfaceBoundaryCondition.AdjacentSpace)
            {
                string? adjacentZoneId = surface.AdjacentZoneId;
                if (string.IsNullOrWhiteSpace(adjacentZoneId)
                    || !surfacesByZone.TryGetValue(adjacentZoneId!, out List<DragonSurface>? adjacentSurfaces))
                {
                    Error(
                        "SD.CONVERSION.ADJACENT_ZONE_NOT_FOUND",
                        "Surface '" + surface.Id.Value + "' references an unavailable adjacent zone.",
                        "Use the ID of a zone contained in the GRM building.",
                        surface.Id);
                    return;
                }

                EntityId cloneId = new("CLONE:" + surface.Id.Value);
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
                surfacesByZone[zone.Id.Value].Add(original);
                adjacentSurfaces.Add(clone);
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
                construction.Layers.Select((layer, index) => ConvertLayer(
                    layer.Material,
                    layer.Thickness,
                    layer.Material.Id.Value + "_"
                    + (layer.Thickness * UnitConversions.MetresToMillimetres)
                        .ToString("G17", System.Globalization.CultureInfo.InvariantCulture)
                    + "mm_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            _constructions.Add(construction.Id.Value, converted);
            return converted;
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
                "COOLROOF:" + outside.Material.Name,
                outside.Material.ConductivityWattsPerMetreKelvin,
                outside.Material.DensityKilogramsPerCubicMetre,
                outside.Material.SpecificHeatJoulesPerKilogramKelvin,
                absorptance,
                absorptance,
                outside.Material.VisibleAbsorptance,
                outside.Material.Roughness);
            var layer = new DragonLayer(
                "COOLROOF:" + outside.Name,
                material,
                outside.ThicknessMetres);
            return new DragonConstruction(
                "COOLROOF:" + opaque.Name,
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

        private static GonieGonie.InvisibleDragon.Shape.IShadingDevice? ConvertShading(BlindType? blind)
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
                EntityId id = new("CLONE:" + opening.Id.Value);
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
                ConvertProfile(profile),
                zone.Infiltration * UnitConversions.AirChangesAt50PaToNaturalAirChanges,
                zone.LightDensity ?? 0d,
                outdoorAir);
        }

        private DragonProfile ConvertProfile(UsageProfile profile)
        {
            if (_profiles.TryGetValue(profile.Id.Value, out DragonProfile? existing))
            {
                return existing;
            }

            string prefix = profile.Source == UsageProfileSource.Standard
                || profile.Source == UsageProfileSource.Extended
                ? "$FROM_DB$:" + profile.Name
                : profile.Id.Value;
            DragonSchedule occupied = WeeklyWindowSchedule(
                prefix + "-Occupied",
                profile,
                profile.OccupantStart,
                profile.OccupantEnd,
                profile.Occupancy / profile.OccupiedHours / UsageProfileConstants.PeopleSensibleActivityWattsPerPerson,
                DragonScheduleType.Real);
            DragonSchedule hvac = WeeklyWindowSchedule(
                prefix + "-HVACOperating",
                profile,
                profile.HvacStart,
                profile.HvacEnd,
                1d,
                DragonScheduleType.OnOff);
            DragonSchedule equipment = WeeklyWindowSchedule(
                prefix + "-Equipment",
                profile,
                profile.OccupantStart,
                profile.OccupantEnd,
                profile.Equipment / profile.OccupiedHours,
                DragonScheduleType.Real);
            DragonSchedule hotWater = WeeklyWindowSchedule(
                prefix + "-HotWater",
                profile,
                profile.OccupantStart,
                profile.OccupantEnd,
                profile.DomesticHotWater
                / profile.OccupiedHours
                / UsageProfileConstants.DomesticHotWaterHeatWattHoursPerLitre,
                DragonScheduleType.Real);
            DragonSchedule lighting = LightingSchedule(prefix + "-Lighted", profile);
            var converted = new DragonProfile(
                new EntityId(prefix),
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
            _profiles.Add(profile.Id.Value, converted);
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
            RuleSet vacation = RuleSet.FromDaySchedule(name + ":vacation", off);
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
            DaySchedule off = DaySchedule.Constant(name + ":off", 0d, DragonScheduleType.OnOff);
            RuleSet weekly = WeeklyRuleSet(name + ":weekly", profile, active, off, DragonScheduleType.OnOff);
            RuleSet vacation = RuleSet.FromDaySchedule(name + ":vacation", off);
            return ApplyVacations(
                new DragonSchedule(name, Enumerable.Repeat(weekly, DragonSchedule.FixedLength), DragonScheduleType.OnOff),
                profile,
                vacation);
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
                if (end >= start)
                {
                    result = result.Apply(vacation, start, end, schedule.Name);
                }
                else
                {
                    result = result.Apply(
                        vacation,
                        start,
                        new DateTime(DragonSchedule.DefaultYear, 12, 31),
                        schedule.Name);
                    result = result.Apply(
                        vacation,
                        new DateTime(DragonSchedule.DefaultYear, 1, 1),
                        end,
                        schedule.Name);
                }
            }

            return result;
        }

        private static DateTime ToScheduleDate(MonthDay value)
        {
            int day = value.Month == 2 && value.Day == 29 ? 28 : value.Day;
            return new DateTime(DragonSchedule.DefaultYear, value.Month, day);
        }

        private static RuleSet WeeklyRuleSet(
            string name,
            UsageProfile profile,
            DaySchedule active,
            DaySchedule off,
            DragonScheduleType type)
        {
            DaySchedule For(UsageDay day) => profile.OperatesOn(day) ? active : off;
            return new RuleSet(
                name,
                off,
                off,
                monday: For(UsageDay.Monday),
                tuesday: For(UsageDay.Tuesday),
                wednesday: For(UsageDay.Wednesday),
                thursday: For(UsageDay.Thursday),
                friday: For(UsageDay.Friday),
                saturday: For(UsageDay.Saturday),
                sunday: For(UsageDay.Sunday),
                holiday: For(UsageDay.Holiday),
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

            int required = checked((int)Math.Round(
                profile.LightingHours * DaySchedule.IntervalsPerHour,
                MidpointRounding.AwayFromZero));
            int[] occupied = Enumerable.Range(0, DaySchedule.FixedLength)
                .Where(IsOccupied)
                .OrderByDescending(index => CircularDistanceToNoon(index))
                .ThenBy(index => index)
                .ToArray();
            if (required > occupied.Length)
            {
                throw new InvalidOperationException(
                    "Profile '" + profile.Name + "' has more lighting intervals than occupied intervals.");
            }

            double[] values = new double[DaySchedule.FixedLength];
            foreach (int index in occupied.Take(required))
            {
                values[index] = 1d;
            }

            return new DaySchedule(name, values, DragonScheduleType.OnOff);
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
                    var dedicated = new DragonHeatPump(
                        new EntityId("DedicatedHeatPump_for_" + supply.Id.Value),
                        "DedicatedHeatPump_for_" + supply.Id.Value,
                        DragonFuel.Electricity,
                        cop,
                        cop,
                        0.001d,
                        supply.CoolingCapacity);
                    return new GonieGonie.InvisibleDragon.Hvac.PackagedAirConditioner(
                        supply.Id,
                        supply.Id.Value,
                        dedicated);
                case SupplySystemType.AirHandlingUnit:
                    if (ConvertSource(supply.SourceSystem) is DragonHeatPump heatPump)
                    {
                        return new GonieGonie.InvisibleDragon.Hvac.AirHandlingUnit(
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
                    return new DragonElectricRadiator(supply.Id, supply.Id.Value);
                case SupplySystemType.FanCoilUnit:
                case SupplySystemType.Radiator:
                    Unsupported(
                        "SD.CONVERSION.SUPPLY_TYPE_NOT_IMPLEMENTED",
                        "Assigned supply system '" + supply.Id.Value + "' uses unsupported type " + supply.Type + ".",
                        supply.Id,
                        "Use one of the currently EnergyPlus-ready supply types or add the matching InvisibleDragon adapter.");
                    return null;
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
                case SourceSystemType.GeothermalHeatPump:
                    converted = new DragonHeatPump(
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
                    converted = new DragonDistrictHeating(
                        source.Id,
                        source.Id.Value,
                        source.HeatingCapacity);
                    break;
                case SourceSystemType.Chiller:
                case SourceSystemType.AbsorptionChiller:
                    Unsupported(
                        "SD.CONVERSION.SOURCE_TYPE_NOT_IMPLEMENTED",
                        "Assigned source system '" + source.Id.Value + "' uses unsupported type " + source.Type + ".",
                        source.Id,
                        "Use a currently EnergyPlus-ready source type or add the matching InvisibleDragon plant adapter.");
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }

            _sources.Add(source.Id.Value, converted);
            return converted;
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
                : StableAzimuthRadians(surface.Id.Value);
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
            GonieGonie.InvisibleDragon.Shape.Vector3 verticalVector = host.Vertices[0] - bottomLeft;
            GonieGonie.InvisibleDragon.Shape.Vector3 horizontalVector = host.Vertices[2] - bottomLeft;
            double height = verticalVector.Length;
            double width = horizontalVector.Length;
            GonieGonie.InvisibleDragon.Shape.Vector3 vertical = verticalVector / height;
            GonieGonie.InvisibleDragon.Shape.Vector3 horizontal = horizontalVector / width;
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

        private static double StableAzimuthRadians(string id)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(id);
#if NET6_0_OR_GREATER
            byte[] hash = SHA256.HashData(bytes);
#else
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(bytes);
            }
#endif
            uint value = ((uint)hash[0] << 24)
                | ((uint)hash[1] << 16)
                | ((uint)hash[2] << 8)
                | hash[3];
            return (value / (double)uint.MaxValue) * 2d * Math.PI;
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
                ? opaque.Reverse("REVERSED:" + opaque.Name)
                : construction;
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
    }
}
