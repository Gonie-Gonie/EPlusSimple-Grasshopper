using System.Globalization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Model;

internal static class EnergyModelIdfAssembler
{
    private const string LegacyUnconditionedThermostatName = "UNCONDITIONED_THERMOSTAT";
    private const int PinnedDefaultObjectCount = 17;

    private static readonly string[] GeneratedPreamble =
    {
        "Generated deterministically by GonieGonie InvisibleDragon.",
    };

    private static readonly string[] PinnedDefaultFamilyOrder =
    {
        "Version",
        "SimulationControl",
        "Timestep",
        "SizingPeriod:WeatherFileDays",
        "RunPeriod",
        "ScheduleTypeLimits",
        "Schedule:Compact",
        "Schedule:Constant",
        "GlobalGeometryRules",
        "Output:Table:SummaryReports",
        "Output:Table:Monthly",
        "OutputControl:Table:Style",
    };

    internal static IdfDocument CreateDefaultDocument()
    {
        var options = new EnergyModelIdfOptions();
        IdfGenerationContext context = new(options: options);
        IdfObject[] objects = CreateDefaults(
                context,
                options,
                exactPinnedFields: true)
            .Concat(OutputTableSettings.Default.ToIdfObjects(context))
            .ToArray();
        if (objects.Length != PinnedDefaultObjectCount
            || objects.Any(item => string.Equals(
                item.ObjectType,
                "Building",
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"The pinned default IDF graph must contain exactly {PinnedDefaultObjectCount} non-Building objects.");
        }

        IdfDocument document = new();
        foreach (string objectType in PinnedDefaultFamilyOrder)
        {
            Append(
                document,
                objects.Where(item => string.Equals(
                    item.ObjectType,
                    objectType,
                    StringComparison.OrdinalIgnoreCase)));
        }

        if (document.Count != objects.Length)
        {
            throw new InvalidOperationException(
                "The pinned default IDF family order does not cover every default object.");
        }

        return document;
    }

    internal static IdfDocument Assemble(EnergyModel model, IddSchema? schema, EnergyModelIdfOptions options)
    {
        IdfGenerationContext context = new(schema, options);
        IdfDocument document = new(schema, preambleComments: GeneratedPreamble);
        Append(document, CreateDefaults(
            context,
            options,
            exactPinnedFields: options.UseLegacySimpleDragonDefaultObjectFields));
        document.Append(context.Create(
            "Building",
            IdfGenerationContext.Field(0, "Name", model.Name),
            IdfGenerationContext.Field(1, "North Axis", model.NorthAxisDegrees),
            IdfGenerationContext.Field(2, "Terrain", model.Terrain),
            IdfGenerationContext.Field(5, "Solar Distribution", "MinimalShadowing")));
        Append(document, model.OutputTables.ToIdfObjects(context));

        List<Schedule> schedules = CollectSchedules(model, options).ToList();
        IEqualityComparer<string> scheduleNameComparer =
            options.UseLegacySimpleDragonUsedProfileScheduleSelection
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;
        Dictionary<string, Schedule> uniqueSchedules = new(scheduleNameComparer);
        foreach (Schedule schedule in schedules)
        {
            if (uniqueSchedules.TryGetValue(schedule.Name, out Schedule? previous))
            {
                if (!previous.Equals(schedule))
                {
                    throw new InvalidOperationException($"Schedule name '{schedule.Name}' has conflicting definitions.");
                }

                continue;
            }

            uniqueSchedules.Add(schedule.Name, schedule);
            document.Append(ScheduleIdfExporter.Create(
                context,
                schedule,
                options.UseLegacySimpleDragonScheduleMetadata));
        }

        Dictionary<EntityId, EnergyRecoveryVentilator> legacyVentilators =
            ResolveLegacyVentilators(model, options);
        AppendConstructionsAndGeometry(document, context, model, options);
        foreach (Zone zone in model.Zones)
        {
            legacyVentilators.TryGetValue(zone.Id, out EnergyRecoveryVentilator? legacyVentilator);
            AppendZoneLoads(
                document,
                context,
                zone,
                uniqueSchedules,
                legacyVentilator,
                options.UseLegacySimpleDragonScheduleMetadata);
        }

        AppendHvac(document, context, model, options, legacyVentilators);
        foreach (PhotovoltaicPanel panel in model.PhotovoltaicPanels)
        {
            Append(document, panel.ToIdfObjects(context));
        }

        document.ApplyDefaults();
        return document;
    }

    private static IEnumerable<IdfObject> CreateDefaults(
        IdfGenerationContext context,
        EnergyModelIdfOptions options,
        bool exactPinnedFields = false)
    {
        EnergyPlusVersion version = EnergyPlusDefaults.DefaultVersion;
        yield return context.CreateRaw(
            "Version",
            string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor));
        yield return context.CreateRaw("SimulationControl", "Yes", "Yes", "Yes", "No", "Yes", "No");
        yield return context.CreateRaw("SizingPeriod:WeatherFileDays", "DesignWinter", 1, 1, 1, 31);
        yield return context.CreateRaw("SizingPeriod:WeatherFileDays", "DesignSummer", 8, 1, 8, 31);
        yield return context.CreateRaw("Timestep", 6);
        yield return context.CreateRaw(
            "RunPeriod",
            "Year-Round",
            1,
            1,
            EnergyPlusDefaults.DefaultYear,
            12,
            31,
            EnergyPlusDefaults.DefaultYear);
        if (exactPinnedFields)
        {
            yield return context.CreateRaw(
                "GlobalGeometryRules",
                "UpperLeftCorner",
                "Counterclockwise",
                "World",
                "Relative",
                "Relative");
        }
        else
        {
            yield return context.CreateRaw("GlobalGeometryRules", "UpperLeftCorner", "CounterClockwise", "World");
        }

        bool legacyScheduleMetadata = exactPinnedFields
            || options.UseLegacySimpleDragonScheduleMetadata;
        foreach (IdfObject typeLimit in ScheduleIdfExporter.CreateTypeLimits(
            context,
            legacyScheduleMetadata))
        {
            yield return typeLimit;
        }

        string? onOffType = legacyScheduleMetadata
            ? null
            : ScheduleIdfExporter.TypeLimitName(ScheduleType.OnOff);
        string realType;
        if (exactPinnedFields)
        {
            realType = "real";
        }
        else if (options.UseLegacySimpleDragonScheduleMetadata)
        {
            realType = "Real";
        }
        else
        {
            realType = ScheduleIdfExporter.TypeLimitName(ScheduleType.Real);
        }

        object peopleActivity = exactPinnedFields
            ? "107.0"
            : ThermalDefaults.PeopleActivityLevelWattsPerPerson;
        yield return context.CreateRaw("Schedule:Compact", "ALLON", onOffType, "Through: 12/31", "For: AllDays", "Until: 24:00", 1);
        yield return context.CreateRaw("Schedule:Compact", "ALLOFF", onOffType, "Through: 12/31", "For: AllDays", "Until: 24:00", 0);
        yield return context.CreateRaw(
            "Schedule:Constant",
            "$DEFAULT$PEOPLEACTIVITY",
            realType,
            peopleActivity);
    }

    private static IEnumerable<Schedule> CollectSchedules(
        EnergyModel model,
        EnergyModelIdfOptions options)
    {
        IEnumerable<ZoneProfile> profiles = options.UseLegacySimpleDragonUsedProfileScheduleSelection
            ? model.UsedProfiles
            : model.Zones.Select(zone => zone.Profile);
        foreach (ZoneProfile profile in profiles)
        {
            foreach (Schedule? schedule in new[]
            {
                profile.HeatingSetpoint,
                profile.CoolingSetpoint,
                profile.HvacAvailability,
                profile.Occupant,
                profile.Lighting,
                profile.Equipment,
                profile.HotWater,
            })
            {
                if (schedule is not null)
                {
                    yield return schedule;
                }
            }
        }

        HashSet<EntityId> conditionedZoneIds = new(
            model.ConditionedZones.Select(zone => zone.Id));
        foreach (Schedule schedule in model.HvacAssignments
            .Where(assignment => conditionedZoneIds.Contains(assignment.ZoneId))
            .SelectMany(assignment => assignment.Supply.CustomAvailabilitySchedules))
        {
            yield return schedule;
        }
    }

    private static void AppendConstructionsAndGeometry(
        IdfDocument document,
        IdfGenerationContext context,
        EnergyModel model,
        EnergyModelIdfOptions options)
    {
        Dictionary<string, object> materialDefinitions = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object> constructionDefinitions = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<EntityId, Surface> surfacesById = model.Surfaces.ToDictionary(surface => surface.Id);
        foreach (Zone zone in model.Zones)
        {
            document.Append(context.Create(
                "Zone",
                IdfGenerationContext.Field(0, "Name", zone.Name),
                IdfGenerationContext.Field(9, "Floor Area", zone.FloorArea),
                IdfGenerationContext.Field(10, "Zone Inside Convection Algorithm", "TARP"),
                IdfGenerationContext.Field(11, "Zone Outside Convection Algorithm", "TARP")));
            foreach (Surface surface in zone.Surfaces)
            {
                string constructionName = AppendSurfaceConstruction(
                    document,
                    context,
                    surface.Construction,
                    surface.Name,
                    materialDefinitions,
                    constructionDefinitions);
                document.Append(BuildingSurface(context, zone, surface, constructionName, surfacesById));
                foreach (IOpening opening in surface.Openings)
                {
                    string openingConstruction;
                    if (opening is Window window)
                    {
                        openingConstruction = AppendGlazing(
                            document,
                            context,
                            window.Glazing,
                            materialDefinitions,
                            constructionDefinitions);
                    }
                    else
                    {
                        Door door = (Door)opening;
                        openingConstruction = AppendSurfaceConstruction(
                            document,
                            context,
                            door.Construction,
                            null,
                            materialDefinitions,
                            constructionDefinitions);
                    }

                    document.Append(FenestrationSurface(
                        context,
                        surface,
                        opening,
                        openingConstruction,
                        surfacesById,
                        options));
                    if (opening is Window shadedWindow && shadedWindow.Shading is not null)
                    {
                        AppendWindowShading(
                            document,
                            context,
                            zone,
                            shadedWindow,
                            materialDefinitions);
                    }
                }
            }
        }
    }

    private static string AppendSurfaceConstruction(
        IdfDocument document,
        IdfGenerationContext context,
        ISurfaceConstruction construction,
        string? surfaceName,
        Dictionary<string, object> materialDefinitions,
        Dictionary<string, object> constructionDefinitions)
    {
        if (construction is OpaqueConstruction opaque)
        {
            foreach (Layer layer in opaque.Layers)
            {
                if (RegisterDefinition(
                    materialDefinitions,
                    layer.Name,
                    layer,
                    ModelDefinitionComparer.EmittedMaterialEquals,
                    "Material"))
                {
                    document.Append(context.CreateRaw(
                        "Material",
                        layer.Name,
                        layer.Material.Roughness,
                        layer.ThicknessMetres,
                        layer.Material.ConductivityWattsPerMetreKelvin,
                        layer.Material.DensityKilogramsPerCubicMetre,
                        layer.Material.SpecificHeatJoulesPerKilogramKelvin,
                        layer.Material.ThermalAbsorptance,
                        layer.Material.SolarAbsorptance,
                        layer.Material.VisibleAbsorptance));
                }
            }

            string name = surfaceName is null ? opaque.Name : $"{opaque.Name}:for:{surfaceName}";
            if (RegisterDefinition(
                constructionDefinitions,
                name,
                opaque,
                ModelDefinitionComparer.EmittedConstructionEquals,
                "Construction"))
            {
                document.Append(context.CreateRaw("Construction", new object?[] { name }.Concat(opaque.Layers.Select(layer => (object?)layer.Name)).ToArray()));
            }

            return name;
        }

        if (construction is NoMassConstruction noMass)
        {
            string material = $"$MaterialFor$_{noMass.Name}";
            if (RegisterDefinition(
                materialDefinitions,
                material,
                noMass,
                ModelDefinitionComparer.EmittedMaterialEquals,
                "Material"))
            {
                document.Append(context.CreateRaw("Material:NoMass", material, "Rough", noMass.ThermalResistance, 0.9, 0.7, 0.7));
            }

            if (RegisterDefinition(
                constructionDefinitions,
                noMass.Name,
                noMass,
                ModelDefinitionComparer.EmittedConstructionEquals,
                "Construction"))
            {
                document.Append(context.CreateRaw("Construction", noMass.Name, material));
            }

            return noMass.Name;
        }

        AirBoundary airBoundary = (AirBoundary)construction;
        if (RegisterDefinition(
            constructionDefinitions,
            airBoundary.Name,
            airBoundary,
            ModelDefinitionComparer.EmittedConstructionEquals,
            "Construction"))
        {
            document.Append(context.CreateRaw("Construction:AirBoundary", airBoundary.Name, "SimpleMixing", airBoundary.AirChangesPerHour));
        }

        return airBoundary.Name;
    }

    private static string AppendGlazing(
        IdfDocument document,
        IdfGenerationContext context,
        Glazing glazing,
        Dictionary<string, object> materialDefinitions,
        Dictionary<string, object> constructionDefinitions)
    {
        string material = $"$GLAZING_FOR${glazing.Name}";
        if (RegisterDefinition(
            materialDefinitions,
            material,
            glazing,
            ModelDefinitionComparer.EmittedMaterialEquals,
            "Material"))
        {
            document.Append(context.CreateRaw(
                "WindowMaterial:SimpleGlazingSystem",
                material,
                glazing.UValueWattsPerSquareMetreKelvin,
                glazing.SolarHeatGainCoefficient));
        }

        if (RegisterDefinition(
            constructionDefinitions,
            glazing.Name,
            glazing,
            ModelDefinitionComparer.EmittedConstructionEquals,
            "Construction"))
        {
            document.Append(context.CreateRaw("Construction", glazing.Name, material));
        }

        return glazing.Name;
    }

    private static void AppendWindowShading(
        IdfDocument document,
        IdfGenerationContext context,
        Zone zone,
        Window window,
        Dictionary<string, object> materialDefinitions)
    {
        IShadingDevice shading = window.Shading!;
        if (RegisterDefinition(
            materialDefinitions,
            shading.Name,
            shading,
            ModelDefinitionComparer.EmittedMaterialEquals,
            "Material"))
        {
            document.Append(ShadingMaterial(context, shading));
        }

        string shadingType = shading is Blind ? "InteriorBlind" : "InteriorShade";
        document.Append(context.CreateRaw(
            "WindowShadingControl",
            $"{window.Name}:ShadingControl",
            zone.Name,
            1,
            shadingType,
            null,
            "OffNightAndOnDayIfCoolingAndHighSolarOnWindow",
            null,
            20,
            false,
            false,
            shading.Name,
            "FixedSlatAngle",
            null,
            null,
            null,
            "Sequential",
            window.Name));
    }

    private static IdfObject ShadingMaterial(
        IdfGenerationContext context,
        IShadingDevice shading)
    {
        return shading switch
        {
            Blind blind => context.CreateRaw(
                "WindowMaterial:Blind",
                blind.Name,
                "Horizontal",
                blind.SlatWidthMetres,
                blind.SlatSeparationMetres,
                0.00025,
                blind.SlatAngleDegrees,
                221,
                0,
                blind.FrontReflectance,
                blind.BackReflectance,
                0,
                blind.FrontReflectance,
                blind.BackReflectance,
                0,
                null,
                null,
                0,
                null,
                null,
                0,
                0.9,
                0.9,
                0.05,
                0.5,
                0,
                0.5,
                0.5,
                0,
                180),
            Shade shade => context.CreateRaw(
                "WindowMaterial:Shade",
                shade.Name,
                shade.Transmittance,
                shade.Reflectance,
                shade.Transmittance,
                shade.Reflectance,
                shade.Emissivity,
                shade.Transmittance,
                0.01,
                100,
                0.05,
                0.5,
                0.5,
                0.5,
                0.5,
                0),
            _ => throw new ArgumentOutOfRangeException(nameof(shading)),
        };
    }

    private static bool RegisterDefinition(
        Dictionary<string, object> definitions,
        string name,
        object definition,
        Func<object, object, bool> definitionsEqual,
        string kind)
    {
        if (!definitions.TryGetValue(name, out object? previous))
        {
            definitions.Add(name, definition);
            return true;
        }

        if (!definitionsEqual(previous, definition))
        {
            throw new InvalidOperationException($"{kind} name '{name}' has conflicting definitions.");
        }

        return false;
    }

    private static IdfObject BuildingSurface(
        IdfGenerationContext context,
        Zone zone,
        Surface surface,
        string constructionName,
        IReadOnlyDictionary<EntityId, Surface> surfacesById)
    {
        string boundaryObject = surface.Boundary.Condition == SurfaceBoundaryCondition.Zone
            ? surfacesById[surface.Boundary.AdjacentSurfaceId!].Name
            : string.Empty;
        string boundary = surface.Boundary.Condition switch
        {
            SurfaceBoundaryCondition.Outdoors => "Outdoors",
            SurfaceBoundaryCondition.Ground => "Ground",
            SurfaceBoundaryCondition.Adiabatic => "Adiabatic",
            SurfaceBoundaryCondition.Zone => "Surface",
            _ => throw new InvalidOperationException(),
        };
        string surfaceType = surface.Type == SurfaceType.Ceiling && boundary == "Outdoors" ? "Roof" : surface.Type.ToString();
        IdfObject result = context.Create(
            "BuildingSurface:Detailed",
            IdfGenerationContext.Field(0, "Name", surface.Name),
            IdfGenerationContext.Field(1, "Surface Type", surfaceType),
            IdfGenerationContext.Field(2, "Construction Name", constructionName),
            IdfGenerationContext.Field(3, "Zone Name", zone.Name),
            IdfGenerationContext.Field(4, "Space Name", null),
            IdfGenerationContext.Field(5, "Outside Boundary Condition", boundary),
            IdfGenerationContext.Field(6, "Outside Boundary Condition Object", boundaryObject),
            IdfGenerationContext.Field(7, "Sun Exposure", boundary == "Outdoors" ? "SunExposed" : "NoSun"),
            IdfGenerationContext.Field(8, "Wind Exposure", boundary == "Outdoors" ? "WindExposed" : "NoWind"),
            IdfGenerationContext.Field(9, "View Factor to Ground", "autocalculate"),
            IdfGenerationContext.Field(10, "Number of Vertices", "autocalculate"));
        AddVertices(result, surface.Polygon.Vertices);
        return result;
    }

    private static IdfObject FenestrationSurface(
        IdfGenerationContext context,
        Surface host,
        IOpening opening,
        string constructionName,
        IReadOnlyDictionary<EntityId, Surface> surfacesById,
        EnergyModelIdfOptions options)
    {
        if (options.UseLegacyRectangularFenestration)
        {
            return LegacyRectangularFenestration(
                context,
                host,
                opening,
                constructionName,
                surfacesById);
        }

        string boundaryObject = string.Empty;
        if (host.Boundary.Condition == SurfaceBoundaryCondition.Zone)
        {
            Surface adjacent = surfacesById[host.Boundary.AdjacentSurfaceId!];
            IOpening counterpart = adjacent.Openings.Single(candidate =>
                candidate.Type == opening.Type
                && opening.Polygon.IsGeometricallyEquivalentTo(candidate.Polygon, true));
            boundaryObject = counterpart.Name;
        }

        IdfObject result = context.Create(
            "FenestrationSurface:Detailed",
            IdfGenerationContext.Field(0, "Name", opening.Name),
            IdfGenerationContext.Field(1, "Surface Type", opening.Type),
            IdfGenerationContext.Field(2, "Construction Name", constructionName),
            IdfGenerationContext.Field(3, "Building Surface Name", host.Name),
            IdfGenerationContext.Field(4, "Outside Boundary Condition Object", boundaryObject),
            IdfGenerationContext.Field(5, "View Factor to Ground", "autocalculate"),
            IdfGenerationContext.Field(6, "Frame and Divider Name", null),
            IdfGenerationContext.Field(7, "Multiplier", 1),
            IdfGenerationContext.Field(8, "Number of Vertices", "autocalculate"));
        AddVertices(result, opening.Polygon.Vertices);
        return result;
    }

    private static IdfObject LegacyRectangularFenestration(
        IdfGenerationContext context,
        Surface host,
        IOpening opening,
        string constructionName,
        IReadOnlyDictionary<EntityId, Surface> surfacesById)
    {
        const double safetyFactor = 0.999d;
        const double safetyMargin = 0.001d;
        double height = (host.Height - safetyMargin) * safetyFactor;
        if (height <= 0d)
        {
            throw new InvalidOperationException(
                $"Surface '{host.Name}' has no positive height for legacy rectangular fenestration.");
        }

        double width = (opening.Polygon.Area / height) * safetyFactor;
        bool interzone = host.Boundary.Condition == SurfaceBoundaryCondition.Zone;
        string objectType = opening.Type switch
        {
            OpeningType.Window when interzone => "Window:Interzone",
            OpeningType.Window => "Window",
            OpeningType.Door when interzone => "Door:Interzone",
            OpeningType.Door => "Door",
            _ => throw new ArgumentOutOfRangeException(nameof(opening)),
        };

        if (!interzone)
        {
            return opening.Type == OpeningType.Window
                ? context.CreateRaw(
                    objectType,
                    opening.Name,
                    constructionName,
                    host.Name,
                    null,
                    1,
                    safetyMargin,
                    safetyMargin,
                    width,
                    height)
                : context.CreateRaw(
                    objectType,
                    opening.Name,
                    constructionName,
                    host.Name,
                    1,
                    safetyMargin,
                    safetyMargin,
                    width,
                    height);
        }

        Surface adjacent = surfacesById[host.Boundary.AdjacentSurfaceId!];
        IOpening counterpart = adjacent.Openings.Single(candidate =>
            candidate.Type == opening.Type
            && opening.Polygon.IsGeometricallyEquivalentTo(candidate.Polygon, true));
        return context.CreateRaw(
            objectType,
            opening.Name,
            constructionName,
            host.Name,
            counterpart.Name,
            1,
            safetyMargin,
            safetyMargin,
            width,
            height);
    }

    private static void AddVertices(IdfObject target, IEnumerable<Vertex> vertices)
    {
        foreach (Vertex vertex in vertices)
        {
            target.Add(IdfGenerationContext.Format(vertex.X));
            target.Add(IdfGenerationContext.Format(vertex.Y));
            target.Add(IdfGenerationContext.Format(vertex.Z));
        }
    }

    private static void AppendZoneLoads(
        IdfDocument document,
        IdfGenerationContext context,
        Zone zone,
        IDictionary<string, Schedule> schedules,
        EnergyRecoveryVentilator? legacyVentilator,
        bool legacySimpleDragonSchedules)
    {
        ZoneProfile profile = zone.Profile;
        if (profile.Lighting is not null
            && (zone.LightingPowerDensityWattsPerSquareMetre > 0 || legacySimpleDragonSchedules))
        {
            document.Append(context.CreateRaw("Lights", $"light:{zone.Name}", zone.Name, profile.Lighting.Name, "Watts/Area", null, zone.LightingPowerDensityWattsPerSquareMetre));
        }

        if (profile.Equipment is not null
            && (profile.Equipment.Maximum > 0 || legacySimpleDragonSchedules))
        {
            string schedule = AppendNormalizedSchedule(
                document,
                context,
                profile.Equipment,
                zone.Name,
                "equipment",
                schedules,
                legacySimpleDragonSchedules);
            document.Append(context.CreateRaw("ElectricEquipment", $"electric_equipment:{zone.Name}", zone.Name, schedule, "Watts/Area", null, profile.Equipment.Maximum));
        }

        if (profile.Occupant is not null
            && (profile.Occupant.Maximum > 0 || legacySimpleDragonSchedules))
        {
            string schedule = AppendNormalizedSchedule(
                document,
                context,
                profile.Occupant,
                zone.Name,
                "occupant",
                schedules,
                legacySimpleDragonSchedules);
            document.Append(context.Create(
                "People",
                IdfGenerationContext.Field(0, "Name", $"people:{zone.Name}"),
                IdfGenerationContext.Field(1, "Zone or ZoneList or Space or SpaceList Name", zone.Name),
                IdfGenerationContext.Field(2, "Number of People Schedule Name", schedule),
                IdfGenerationContext.Field(3, "Number of People Calculation Method", "People/Area"),
                IdfGenerationContext.Field(4, "Number of People", null),
                IdfGenerationContext.Field(5, "People per Floor Area", profile.Occupant.Maximum),
                IdfGenerationContext.Field(6, "Floor Area per Person", null),
                IdfGenerationContext.Field(7, "Fraction Radiant", null),
                IdfGenerationContext.Field(8, "Sensible Heat Fraction", null),
                IdfGenerationContext.Field(9, "Activity Level Schedule Name", "$DEFAULT$PEOPLEACTIVITY")));
        }

        if (zone.InfiltrationAirChangesPerHour > 0)
        {
            document.Append(context.CreateRaw("ZoneInfiltration:DesignFlowRate", $"{zone.Name}:infiltration", zone.Name, "ALLON", "AirChanges/Hour", null, null, null, zone.InfiltrationAirChangesPerHour));
        }

        if (profile.Occupant is not null)
        {
            if (legacyVentilator is null)
            {
                document.Append(context.Create(
                    "ZoneVentilation:DesignFlowRate",
                    IdfGenerationContext.Field(0, "Name", $"NaturalVentilation:{zone.Name}"),
                    IdfGenerationContext.Field(1, "Zone or ZoneList or Space or SpaceList Name", zone.Name),
                    IdfGenerationContext.Field(3, "Design Flow Rate Calculation Method", "Flow/Person"),
                    IdfGenerationContext.Field(
                        6,
                        "Flow Rate per Person",
                        8.3d * UnitConversions.LitresToCubicMetres)));
            }
            else
            {
                double overallEffectiveness =
                    (legacyVentilator.SensibleEffectiveness + legacyVentilator.LatentEffectiveness) / 2d;
                double unrecoveredFraction = 1d - overallEffectiveness;
                if (unrecoveredFraction <= 0d)
                {
                    throw new InvalidOperationException(
                        "Legacy SimpleDragon ventilation requires average heat-recovery effectiveness below 1.");
                }

                document.Append(context.Create(
                    "ZoneVentilation:DesignFlowRate",
                    IdfGenerationContext.Field(0, "Name", $"NaturalVentilation:{zone.Name}"),
                    IdfGenerationContext.Field(1, "Zone or ZoneList or Space or SpaceList Name", zone.Name),
                    IdfGenerationContext.Field(3, "Design Flow Rate Calculation Method", "Flow/Person"),
                    IdfGenerationContext.Field(
                        6,
                        "Flow Rate per Person",
                        8.3d * UnitConversions.LitresToCubicMetres * unrecoveredFraction),
                    IdfGenerationContext.Field(8, "Ventilation Type", "Exhaust"),
                    IdfGenerationContext.Field(9, "Fan Pressure Rise", 50d / unrecoveredFraction),
                    IdfGenerationContext.Field(10, "Fan Total Efficiency", 0.85d)));
            }
        }
    }

    private static string AppendNormalizedSchedule(
        IdfDocument document,
        IdfGenerationContext context,
        Schedule schedule,
        string zoneName,
        string purpose,
        IDictionary<string, Schedule> schedules,
        bool legacySimpleDragon)
    {
        string name = $"{schedule.Name}_normalized:for:{zoneName}:{purpose}";
        if (!schedules.ContainsKey(name))
        {
            Schedule normalized = schedule.NormalizeByMaximum(name);
            schedules.Add(name, normalized);
            document.Append(ScheduleIdfExporter.Create(context, normalized, legacySimpleDragon));
        }

        return name;
    }

    private static void AppendHvac(
        IdfDocument document,
        IdfGenerationContext context,
        EnergyModel model,
        EnergyModelIdfOptions options,
        Dictionary<EntityId, EnergyRecoveryVentilator> legacyVentilators)
    {
        Dictionary<EntityId, Zone> zones = model.Zones.ToDictionary(zone => zone.Id);
        Dictionary<EntityId, List<SupplyIdfFragment>> fragmentsByZone = model.Zones.ToDictionary(zone => zone.Id, _ => new List<SupplyIdfFragment>());
        Dictionary<EntityId, SourceAccumulator> sources = new();
        foreach (ZoneHvacAssignment assignment in model.HvacAssignments.GroupBy(item => item.ZoneId).Select(group => group.First()))
        {
            if (!zones.TryGetValue(assignment.ZoneId, out Zone? zone))
            {
                continue;
            }

            if (!model.IsConditionedZone(zone))
            {
                continue;
            }

            for (int index = 0; index < assignment.Supply.Systems.Count; index++)
            {
                SupplySystem system = assignment.Supply.Systems[index];
                string availability = assignment.Supply.Availabilities[index]?.Name
                    ?? zone.Profile.HvacAvailability!.Name;
                SupplyIdfFragment fragment = system.Generate(context, zone, availability);
                fragmentsByZone[zone.Id].Add(fragment);
                if (system.Source is not null)
                {
                    if (!sources.TryGetValue(system.Source.Id, out SourceAccumulator? accumulator))
                    {
                        accumulator = new SourceAccumulator(system.Source);
                        sources.Add(system.Source.Id, accumulator);
                    }
                    else if (!ModelDefinitionComparer.HvacSystemEquals(accumulator.Source, system.Source))
                    {
                        throw new InvalidOperationException(
                            $"HVAC identifier '{system.Source.Id}' has conflicting source definitions.");
                    }

                    if (fragment.PlantConnection is not null)
                    {
                        accumulator.AddDemandConnection(fragment.PlantConnection);
                    }

                    if (fragment.TerminalUnitName is not null)
                    {
                        accumulator.TerminalUnitNames.Add(fragment.TerminalUnitName);
                    }
                }
            }
        }

        if (!options.UseLegacySimpleDragonVentilation)
        {
            foreach (ZoneVentilationAssignment assignment in model.VentilationAssignments)
            {
                if (zones.TryGetValue(assignment.ZoneId, out Zone? zone))
                {
                    fragmentsByZone[zone.Id].Add(assignment.Ventilator.Generate(
                        context,
                        zone,
                        zone.Profile.HvacAvailability?.Name ?? "ALLON"));
                }
            }
        }

        foreach (SourceAccumulator source in sources.Values.OrderBy(item => item.Source.Id))
        {
            Append(document, source.Source.ToIdfObjects(context, source.DemandConnections, source.TerminalUnitNames));
        }

        bool sharedLegacyThermostatAppended = false;
        foreach (Zone zone in model.Zones)
        {
            List<SupplyIdfFragment> fragments = fragmentsByZone[zone.Id];
            bool isConditioned = model.IsConditionedZone(zone);
            ZoneHvacAssignment? assignment = isConditioned
                ? model.HvacAssignments.FirstOrDefault(item => item.ZoneId.Equals(zone.Id))
                : null;
            foreach (SupplyIdfFragment fragment in fragments)
            {
                Append(document, fragment.Objects);
            }

            if (fragments.Count > 0)
            {
                AppendZoneEquipment(
                    document,
                    context,
                    zone,
                    fragments.Select(fragment => fragment.Equipment).ToArray(),
                    assignment?.Supply);
            }

            if (isConditioned && assignment is not null)
            {
                AppendThermostat(document, context, zone, assignment.Supply, options);
                AppendSizing(document, context, zone);
            }
            else if (!isConditioned
                && fragments.Count == 0
                && options.AddIdealLoadsForUnassignedZones
                && (!legacyVentilators.ContainsKey(zone.Id)
                    || options.UseLegacySimpleDragonHvacTopology))
            {
                if (options.UseLegacySimpleDragonHvacTopology)
                {
                    if (!sharedLegacyThermostatAppended)
                    {
                        document.Append(context.CreateRaw(
                            "HVACTemplate:Thermostat",
                            LegacyUnconditionedThermostatName,
                            null,
                            -30,
                            null,
                            50));
                        sharedLegacyThermostatAppended = true;
                    }

                    document.Append(context.CreateRaw(
                        "HVACTemplate:Zone:IdealLoadsAirSystem",
                        zone.Name,
                        LegacyUnconditionedThermostatName,
                        "ALLON"));
                }
                else
                {
                    document.Append(context.CreateRaw("HVACTemplate:Thermostat", $"IdealThermostat_for_{zone.Name}", null, -30, null, 50));
                    document.Append(context.CreateRaw("HVACTemplate:Zone:IdealLoadsAirSystem", zone.Name, $"IdealThermostat_for_{zone.Name}"));
                }
            }
        }
    }

    private static Dictionary<EntityId, EnergyRecoveryVentilator> ResolveLegacyVentilators(
        EnergyModel model,
        EnergyModelIdfOptions options)
    {
        var result = new Dictionary<EntityId, EnergyRecoveryVentilator>();
        if (!options.UseLegacySimpleDragonVentilation)
        {
            return result;
        }

        foreach (ZoneVentilationAssignment assignment in model.VentilationAssignments)
        {
            if (result.ContainsKey(assignment.ZoneId))
            {
                throw new InvalidOperationException(
                    $"Legacy SimpleDragon ventilation requires one aggregated assignment for zone '{assignment.ZoneId}'.");
            }

            result.Add(assignment.ZoneId, assignment.Ventilator);
        }

        return result;
    }

    private static void AppendZoneEquipment(
        IdfDocument document,
        IdfGenerationContext context,
        Zone zone,
        IReadOnlyList<ZoneEquipmentDescriptor> equipment,
        SupplyGroup? supply)
    {
        (string? Heating, string? Cooling)[] fractions = AppendSequentialLoadFractions(
            document,
            context,
            zone,
            supply,
            equipment);
        List<object?> equipmentFields = new() { $"EquipmentList_for_{zone.Name}", "SequentialLoad" };
        for (int index = 0; index < equipment.Count; index++)
        {
            ZoneEquipmentDescriptor item = equipment[index];
            int sequence = index + 1;
            equipmentFields.Add(item.ObjectType);
            equipmentFields.Add(item.Name);
            equipmentFields.Add(sequence);
            equipmentFields.Add(sequence);
            equipmentFields.Add(fractions[index].Cooling);
            equipmentFields.Add(fractions[index].Heating);
        }

        document.Append(context.CreateRaw("ZoneHVAC:EquipmentList", equipmentFields.ToArray()));
        string[] inletNodes = equipment.Select(item => item.InletNodeName).Where(item => item is not null).Cast<string>().ToArray();
        string[] exhaustNodes = equipment.Select(item => item.ExhaustNodeName).Where(item => item is not null).Cast<string>().ToArray();
        string inletReference = AppendNodeList(document, context, $"{zone.Name} Air InletNode List", inletNodes);
        string exhaustReference = AppendNodeList(document, context, $"{zone.Name} Air ExhaustNode List", exhaustNodes);
        document.Append(context.CreateRaw(
            "ZoneHVAC:EquipmentConnections",
            zone.Name,
            $"EquipmentList_for_{zone.Name}",
            inletReference,
            exhaustReference,
            $"{zone.Name} Zone Air Node",
            null));
    }

    private static (string? Heating, string? Cooling)[] AppendSequentialLoadFractions(
        IdfDocument document,
        IdfGenerationContext context,
        Zone zone,
        SupplyGroup? supply,
        IReadOnlyList<ZoneEquipmentDescriptor> equipment)
    {
        var result = new (string? Heating, string? Cooling)[equipment.Count];
        if (supply is null)
        {
            return result;
        }

        string?[] heating = AppendModeFractions(
            document,
            context,
            zone,
            supply,
            equipment,
            "heating",
            system => system.CanHeat);
        string?[] cooling = AppendModeFractions(
            document,
            context,
            zone,
            supply,
            equipment,
            "cooling",
            system => system.CanCool);
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = (heating[index], cooling[index]);
        }

        return result;
    }

    private static string?[] AppendModeFractions(
        IdfDocument document,
        IdfGenerationContext context,
        Zone zone,
        SupplyGroup supply,
        IReadOnlyList<ZoneEquipmentDescriptor> equipment,
        string mode,
        Func<SupplySystem, bool> supportsMode)
    {
        var references = new string?[equipment.Count];
        int pairedCount = Math.Min(supply.Systems.Count, equipment.Count);
        for (int index = 0; index < pairedCount; index++)
        {
            references[index] = "ALLOFF";
        }

        int[] active = Enumerable.Range(0, pairedCount)
            .Where(index => supportsMode(supply.Systems[index]))
            .ToArray();
        if (active.Length == 0)
        {
            return references;
        }

        if (active.Length == 1)
        {
            int index = active[0];
            string name = $"{mode}_fraction_for_{equipment[index].Name}";
            Schedule fraction = Schedule.Constant(name, 1d, ScheduleType.Real);
            document.Append(ScheduleIdfExporter.Create(context, fraction));
            references[index] = name;
            return references;
        }

        Schedule[] availability = new Schedule[pairedCount];
        foreach (int index in active)
        {
            availability[index] = supply.Availabilities[index]?.AsType(ScheduleType.Real)
                ?? Schedule.Constant(
                    $"{mode}_availability_for_{equipment[index].Name}",
                    1d,
                    ScheduleType.Real);
        }

        Schedule remaining = Schedule.Constant(
            $"{mode}_remaining_for_{zone.Name}",
            0d,
            ScheduleType.Real);
        foreach (int index in active)
        {
            remaining = remaining.Add(availability[index]);
        }

        Schedule epsilon = Schedule.Constant(
            $"{mode}_fraction_epsilon_for_{zone.Name}",
            1.0e-10d,
            ScheduleType.Real);
        foreach (int index in active)
        {
            string name = $"{mode}_fraction_for_{equipment[index].Name}";
            Schedule fraction = availability[index].Divide(
                remaining.Add(epsilon),
                name);
            document.Append(ScheduleIdfExporter.Create(context, fraction));
            references[index] = name;
            remaining = remaining.Subtract(availability[index]);
        }

        return references;
    }

    private static string AppendNodeList(IdfDocument document, IdfGenerationContext context, string name, string[] nodes)
    {
        if (nodes.Length == 0)
        {
            return string.Empty;
        }

        document.Append(context.CreateRaw("NodeList", new object?[] { name }.Concat(nodes.Cast<object?>()).ToArray()));
        return name;
    }

    private static void AppendThermostat(
        IdfDocument document,
        IdfGenerationContext context,
        Zone zone,
        SupplyGroup supply,
        EnergyModelIdfOptions options)
    {
        string controlSchedule = $"ScheduleTypeForThermostat_for_{zone.Name}";
        string thermostat = $"Thermostat_for_{zone.Name}";
        if (options.UseLegacySimpleDragonHvacTopology
            || (supply.HeatingSystems.Count > 0 && supply.CoolingSystems.Count > 0))
        {
            document.Append(context.CreateRaw("Schedule:Constant", controlSchedule, null, 4));
            document.Append(context.CreateRaw("ThermostatSetpoint:DualSetpoint", $"DualSetPoint_for_{zone.Name}", zone.Profile.HeatingSetpoint!.Name, zone.Profile.CoolingSetpoint!.Name));
            document.Append(context.CreateRaw("ZoneControl:Thermostat", thermostat, zone.Name, controlSchedule, "ThermostatSetpoint:DualSetpoint", $"DualSetPoint_for_{zone.Name}"));
        }
        else if (supply.HeatingSystems.Count > 0)
        {
            document.Append(context.CreateRaw("Schedule:Constant", controlSchedule, null, 1));
            document.Append(context.CreateRaw("ThermostatSetpoint:SingleHeating", $"HeatingSetPoint_for_{zone.Name}", zone.Profile.HeatingSetpoint!.Name));
            document.Append(context.CreateRaw("ZoneControl:Thermostat", thermostat, zone.Name, controlSchedule, "ThermostatSetpoint:SingleHeating", $"HeatingSetPoint_for_{zone.Name}"));
        }
        else
        {
            document.Append(context.CreateRaw("Schedule:Constant", controlSchedule, null, 2));
            document.Append(context.CreateRaw("ThermostatSetpoint:SingleCooling", $"CoolingSetPoint_for_{zone.Name}", zone.Profile.CoolingSetpoint!.Name));
            document.Append(context.CreateRaw("ZoneControl:Thermostat", thermostat, zone.Name, controlSchedule, "ThermostatSetpoint:SingleCooling", $"CoolingSetPoint_for_{zone.Name}"));
        }
    }

    private static void AppendSizing(IdfDocument document, IdfGenerationContext context, Zone zone)
    {
        string outdoorAir = $"DesignSpecificationOutdoorAir_for_{zone.Name}";
        string distribution = $"DesignSpecificationZoneAirDistribution_for_{zone.Name}";
        document.Append(context.CreateRaw(
            "DesignSpecification:OutdoorAir",
            outdoorAir,
            "Flow/Person",
            0.00944d,
            0,
            0,
            0,
            "ALLON"));
        document.Append(context.CreateRaw("DesignSpecification:ZoneAirDistribution", distribution));
        document.Append(context.Create(
            "Sizing:Zone",
            IdfGenerationContext.Field(0, "Zone or ZoneList Name", zone.Name),
            IdfGenerationContext.Field(1, "Zone Cooling Design Supply Air Temperature Input Method", "SupplyAirTemperature"),
            IdfGenerationContext.Field(2, "Zone Cooling Design Supply Air Temperature", 14),
            IdfGenerationContext.Field(3, "Zone Cooling Design Supply Air Temperature Difference", 10),
            IdfGenerationContext.Field(4, "Zone Heating Design Supply Air Temperature Input Method", "SupplyAirTemperature"),
            IdfGenerationContext.Field(5, "Zone Heating Design Supply Air Temperature", 50),
            IdfGenerationContext.Field(6, "Zone Heating Design Supply Air Temperature Difference", 10),
            IdfGenerationContext.Field(7, "Zone Cooling Design Supply Air Humidity Ratio", 0.009),
            IdfGenerationContext.Field(8, "Zone Heating Design Supply Air Humidity Ratio", 0.004),
            IdfGenerationContext.Field(9, "Design Specification Outdoor Air Object Name", outdoorAir),
            IdfGenerationContext.Field(10, "Zone Heating Sizing Factor", 1.25),
            IdfGenerationContext.Field(11, "Zone Cooling Sizing Factor", 1.15),
            IdfGenerationContext.Field(13, "Design Specification Zone Air Distribution Object Name", distribution),
            IdfGenerationContext.Field(36, "Type of Space Sum to Use", "Coincident")));
    }

    private static void Append(IdfDocument document, IEnumerable<IdfObject> objects)
    {
        foreach (IdfObject item in objects)
        {
            document.Append(item);
        }
    }

    private sealed class SourceAccumulator
    {
        internal SourceAccumulator(SourceSystem source)
        {
            Source = source;
        }

        internal SourceSystem Source { get; }

        internal List<PlantDemandConnection> DemandConnections { get; } = new();

        internal List<string> TerminalUnitNames { get; } = new();

        internal void AddDemandConnection(PlantDemandConnection connection)
        {
            if (!DemandConnections.Any(item => string.Equals(
                item.BranchName,
                connection.BranchName,
                StringComparison.OrdinalIgnoreCase)))
            {
                DemandConnections.Add(connection);
                return;
            }

            string disambiguatedBaseName = $"{connection.BranchName}_for_{connection.ComponentName}";
            string disambiguatedName = disambiguatedBaseName;
            int suffix = 2;
            while (DemandConnections.Any(item => string.Equals(
                item.BranchName,
                disambiguatedName,
                StringComparison.OrdinalIgnoreCase)))
            {
                disambiguatedName = $"{disambiguatedBaseName}_{suffix}";
                suffix++;
            }

            DemandConnections.Add(new PlantDemandConnection(
                disambiguatedName,
                connection.ComponentObjectType,
                connection.ComponentName,
                connection.InletNodeName,
                connection.OutletNodeName));
        }
    }
}
