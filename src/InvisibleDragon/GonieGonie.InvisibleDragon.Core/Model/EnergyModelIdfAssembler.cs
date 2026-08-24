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
    // Below 0.278 Wh/(m2 K), retain the layer's R-value without asking EnergyPlus
    // to solve physically insignificant thermal storage. Thick layers remain massive.
    private const double MaximumNoMassArealHeatCapacity = 1000d;

    private static readonly string[] GeneratedPreamble =
    {
        "Generated deterministically by GonieGonie InvisibleDragon.",
    };

    internal static IdfDocument Assemble(EnergyModel model, IddSchema? schema, EnergyModelIdfOptions options)
    {
        IdfGenerationContext context = new(schema);
        IdfDocument document = new(schema, preambleComments: GeneratedPreamble);
        Append(document, CreateDefaults(context));
        document.Append(context.Create(
            "Building",
            IdfGenerationContext.Field(0, "Name", model.Name),
            IdfGenerationContext.Field(1, "North Axis", model.NorthAxisDegrees),
            IdfGenerationContext.Field(2, "Terrain", model.Terrain),
            IdfGenerationContext.Field(5, "Solar Distribution", "MinimalShadowing")));
        Append(document, model.OutputTables.ToIdfObjects(context));

        List<Schedule> schedules = CollectSchedules(model).ToList();
        Dictionary<string, Schedule> uniqueSchedules = new(StringComparer.OrdinalIgnoreCase);
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
            document.Append(ScheduleIdfExporter.Create(context, schedule));
        }

        AppendConstructionsAndGeometry(document, context, model);
        foreach (Zone zone in model.Zones)
        {
            AppendZoneLoads(document, context, zone, uniqueSchedules);
        }

        AppendHvac(document, context, model, options);
        foreach (PhotovoltaicPanel panel in model.PhotovoltaicPanels)
        {
            Append(document, panel.ToIdfObjects(context));
        }

        document.ApplyDefaults();
        return document;
    }

    private static IEnumerable<IdfObject> CreateDefaults(IdfGenerationContext context)
    {
        yield return context.CreateRaw("Version", "24.2");
        yield return context.CreateRaw("SimulationControl", "Yes", "Yes", "Yes", "No", "Yes", "No");
        yield return context.CreateRaw("SizingPeriod:WeatherFileDays", "DesignWinter", 1, 1, 1, 31);
        yield return context.CreateRaw("SizingPeriod:WeatherFileDays", "DesignSummer", 8, 1, 8, 31);
        yield return context.CreateRaw("Timestep", 6);
        yield return context.CreateRaw("RunPeriod", "Year-Round", 1, 1, 2026, 12, 31, 2026);
        yield return context.CreateRaw("GlobalGeometryRules", "UpperLeftCorner", "CounterClockwise", "World");
        foreach (IdfObject typeLimit in ScheduleIdfExporter.CreateTypeLimits(context))
        {
            yield return typeLimit;
        }

        yield return context.CreateRaw("Schedule:Compact", "ALLON", ScheduleIdfExporter.TypeLimitName(ScheduleType.OnOff), "Through: 12/31", "For: AllDays", "Until: 24:00", 1);
        yield return context.CreateRaw("Schedule:Compact", "ALLOFF", ScheduleIdfExporter.TypeLimitName(ScheduleType.OnOff), "Through: 12/31", "For: AllDays", "Until: 24:00", 0);
        yield return context.CreateRaw("Schedule:Constant", "$DEFAULT$PEOPLEACTIVITY", ScheduleIdfExporter.TypeLimitName(ScheduleType.Real), 120);
    }

    private static IEnumerable<Schedule> CollectSchedules(EnergyModel model)
    {
        foreach (ZoneProfile profile in model.Zones.Select(zone => zone.Profile))
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

        foreach (Schedule schedule in model.HvacAssignments.SelectMany(assignment => assignment.Supply.CustomAvailabilitySchedules))
        {
            yield return schedule;
        }
    }

    private static void AppendConstructionsAndGeometry(IdfDocument document, IdfGenerationContext context, EnergyModel model)
    {
        Dictionary<string, object> materialDefinitions = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object> constructionDefinitions = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<EntityId, Surface> surfacesById = model.Surfaces.ToDictionary(surface => surface.Id);
        foreach (Zone zone in model.Zones)
        {
            document.Append(context.CreateRaw("Zone", zone.Name));
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

                    document.Append(FenestrationSurface(context, surface, opening, openingConstruction));
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
                    if (IsEffectivelyNoMass(layer))
                    {
                        document.Append(context.CreateRaw(
                            "Material:NoMass",
                            layer.Name,
                            layer.Material.Roughness,
                            layer.ThermalResistance,
                            layer.Material.ThermalAbsorptance,
                            layer.Material.SolarAbsorptance,
                            layer.Material.VisibleAbsorptance));
                    }
                    else
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
            string material = $"MaterialFor_{noMass.Name}";
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

    private static bool IsEffectivelyNoMass(Layer layer)
    {
        return layer.HeatCapacityJoulesPerSquareMetreKelvin < MaximumNoMassArealHeatCapacity;
    }

    private static string AppendGlazing(
        IdfDocument document,
        IdfGenerationContext context,
        Glazing glazing,
        Dictionary<string, object> materialDefinitions,
        Dictionary<string, object> constructionDefinitions)
    {
        string material = $"GlazingMaterialFor_{glazing.Name}";
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
            IdfGenerationContext.Field(10, "Number of Vertices", surface.Polygon.Vertices.Count));
        AddVertices(result, surface.Polygon.Vertices);
        return result;
    }

    private static IdfObject FenestrationSurface(
        IdfGenerationContext context,
        Surface host,
        IOpening opening,
        string constructionName)
    {
        IdfObject result = context.Create(
            "FenestrationSurface:Detailed",
            IdfGenerationContext.Field(0, "Name", opening.Name),
            IdfGenerationContext.Field(1, "Surface Type", opening.Type),
            IdfGenerationContext.Field(2, "Construction Name", constructionName),
            IdfGenerationContext.Field(3, "Building Surface Name", host.Name),
            IdfGenerationContext.Field(4, "Outside Boundary Condition Object", null),
            IdfGenerationContext.Field(5, "View Factor to Ground", "autocalculate"),
            IdfGenerationContext.Field(6, "Frame and Divider Name", null),
            IdfGenerationContext.Field(7, "Multiplier", 1),
            IdfGenerationContext.Field(8, "Number of Vertices", opening.Polygon.Vertices.Count));
        AddVertices(result, opening.Polygon.Vertices);
        return result;
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
        IDictionary<string, Schedule> schedules)
    {
        ZoneProfile profile = zone.Profile;
        if (profile.Lighting is not null && zone.LightingPowerDensityWattsPerSquareMetre > 0)
        {
            document.Append(context.CreateRaw("Lights", $"light:{zone.Name}", zone.Name, profile.Lighting.Name, "Watts/Area", null, zone.LightingPowerDensityWattsPerSquareMetre));
        }

        if (profile.Equipment is not null && profile.Equipment.Maximum > 0)
        {
            string schedule = AppendNormalizedSchedule(document, context, profile.Equipment, zone.Name, "equipment", schedules);
            document.Append(context.CreateRaw("ElectricEquipment", $"electric_equipment:{zone.Name}", zone.Name, schedule, "Watts/Area", null, profile.Equipment.Maximum));
        }

        if (profile.Occupant is not null && profile.Occupant.Maximum > 0)
        {
            string schedule = AppendNormalizedSchedule(document, context, profile.Occupant, zone.Name, "occupant", schedules);
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

        if (zone.OutdoorAirFlowCubicMetresPerSecond > 0)
        {
            document.Append(context.CreateRaw("ZoneVentilation:DesignFlowRate", $"NaturalVentilation:{zone.Name}", zone.Name, "ALLON", "Flow/Zone", zone.OutdoorAirFlowCubicMetresPerSecond));
        }
    }

    private static string AppendNormalizedSchedule(
        IdfDocument document,
        IdfGenerationContext context,
        Schedule schedule,
        string zoneName,
        string purpose,
        IDictionary<string, Schedule> schedules)
    {
        string name = $"{schedule.Name}_normalized:for:{zoneName}:{purpose}";
        if (!schedules.ContainsKey(name))
        {
            Schedule normalized = schedule.Scale(1 / schedule.Maximum, name);
            schedules.Add(name, normalized);
            document.Append(ScheduleIdfExporter.Create(context, normalized));
        }

        return name;
    }

    private static void AppendHvac(IdfDocument document, IdfGenerationContext context, EnergyModel model, EnergyModelIdfOptions options)
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

            for (int index = 0; index < assignment.Supply.Systems.Count; index++)
            {
                SupplySystem system = assignment.Supply.Systems[index];
                string availability = assignment.Supply.Availabilities[index]?.Name
                    ?? zone.Profile.HvacAvailability?.Name
                    ?? "ALLON";
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
                        accumulator.DemandConnections.Add(fragment.PlantConnection);
                    }

                    if (fragment.TerminalUnitName is not null)
                    {
                        accumulator.TerminalUnitNames.Add(fragment.TerminalUnitName);
                    }
                }
            }
        }

        foreach (ZoneVentilationAssignment assignment in model.VentilationAssignments)
        {
            if (zones.TryGetValue(assignment.ZoneId, out Zone? zone))
            {
                fragmentsByZone[zone.Id].Add(assignment.Ventilator.Generate(context, zone, zone.Profile.HvacAvailability?.Name ?? "ALLON"));
            }
        }

        foreach (SourceAccumulator source in sources.Values.OrderBy(item => item.Source.Id))
        {
            Append(document, source.Source.ToIdfObjects(context, source.DemandConnections, source.TerminalUnitNames));
        }

        foreach (Zone zone in model.Zones)
        {
            List<SupplyIdfFragment> fragments = fragmentsByZone[zone.Id];
            foreach (SupplyIdfFragment fragment in fragments)
            {
                Append(document, fragment.Objects);
            }

            if (fragments.Count > 0)
            {
                AppendZoneEquipment(document, context, zone, fragments.Select(fragment => fragment.Equipment).ToArray());
            }

            ZoneHvacAssignment? assignment = model.HvacAssignments.FirstOrDefault(item => item.ZoneId.Equals(zone.Id));
            if (assignment is not null)
            {
                AppendThermostat(document, context, zone, assignment.Supply);
                AppendSizing(document, context, zone);
            }
            else if (fragments.Count == 0 && options.AddIdealLoadsForUnassignedZones)
            {
                document.Append(context.CreateRaw("HVACTemplate:Thermostat", $"IdealThermostat_for_{zone.Name}", null, -30, null, 50));
                document.Append(context.CreateRaw("HVACTemplate:Zone:IdealLoadsAirSystem", zone.Name, $"IdealThermostat_for_{zone.Name}"));
            }
        }
    }

    private static void AppendZoneEquipment(
        IdfDocument document,
        IdfGenerationContext context,
        Zone zone,
        IReadOnlyList<ZoneEquipmentDescriptor> equipment)
    {
        List<object?> equipmentFields = new() { $"EquipmentList_for_{zone.Name}", "SequentialLoad" };
        for (int index = 0; index < equipment.Count; index++)
        {
            ZoneEquipmentDescriptor item = equipment[index];
            int sequence = index + 1;
            equipmentFields.Add(item.ObjectType);
            equipmentFields.Add(item.Name);
            equipmentFields.Add(sequence);
            equipmentFields.Add(sequence);
            equipmentFields.Add(null);
            equipmentFields.Add(null);
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
            $"{zone.Name} Return Air Node"));
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

    private static void AppendThermostat(IdfDocument document, IdfGenerationContext context, Zone zone, SupplyGroup supply)
    {
        string controlSchedule = $"ScheduleTypeForThermostat_for_{zone.Name}";
        string thermostat = $"Thermostat_for_{zone.Name}";
        if (supply.HeatingSystems.Count > 0 && supply.CoolingSystems.Count > 0)
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
        document.Append(context.CreateRaw("DesignSpecification:OutdoorAir", outdoorAir, "Flow/Zone", zone.OutdoorAirFlowCubicMetresPerSecond, null, null, null, "ALLON"));
        document.Append(context.CreateRaw("DesignSpecification:ZoneAirDistribution", distribution));
        document.Append(context.Create(
            "Sizing:Zone",
            IdfGenerationContext.Field(0, "Zone or ZoneList Name", zone.Name),
            IdfGenerationContext.Field(1, "Zone Cooling Design Supply Air Temperature Input Method", "SupplyAirTemperature"),
            IdfGenerationContext.Field(2, "Zone Cooling Design Supply Air Temperature", 14),
            IdfGenerationContext.Field(4, "Zone Heating Design Supply Air Temperature Input Method", "SupplyAirTemperature"),
            IdfGenerationContext.Field(5, "Zone Heating Design Supply Air Temperature", 50),
            IdfGenerationContext.Field(7, "Zone Cooling Design Supply Air Humidity Ratio", 0.009),
            IdfGenerationContext.Field(8, "Zone Heating Design Supply Air Humidity Ratio", 0.004),
            IdfGenerationContext.Field(9, "Design Specification Outdoor Air Object Name", outdoorAir),
            IdfGenerationContext.Field(10, "Zone Heating Sizing Factor", 1.25),
            IdfGenerationContext.Field(11, "Zone Cooling Sizing Factor", 1.15),
            IdfGenerationContext.Field(13, "Design Specification Zone Air Distribution Object Name", distribution)));
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
    }
}
