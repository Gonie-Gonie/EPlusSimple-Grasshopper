using System.Collections.ObjectModel;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Internal;
using Dragons.InvisibleDragon.Shape;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Model;

public enum Terrain
{
    Country,
    Suburbs,
    City,
    Ocean,
    Urban,
}

public sealed class EnergyModelIdfOptions
{
    public bool ThrowOnValidationErrors { get; set; } = true;

    public bool AddIdealLoadsForUnassignedZones { get; set; } = true;

    /// <summary>
    /// Emits the rectangular Window/Door object family used by the pinned Python
    /// SimpleDragon conversion instead of explicit fenestration vertices.
    /// </summary>
    public bool UseLegacyRectangularFenestration { get; set; }

    /// <summary>
    /// Retains the blank and historical schedule type-limit references emitted
    /// by the pinned Python SimpleDragon conversion.
    /// </summary>
    public bool UseLegacySimpleDragonScheduleMetadata { get; set; }

    /// <summary>
    /// Emits the exact raw default-object fields created by the pinned Python
    /// SimpleDragon implementation. This is independent from the broader native
    /// schedule-metadata compatibility switch.
    /// </summary>
    public bool UseLegacySimpleDragonDefaultObjectFields { get; set; }

    /// <summary>
    /// Selects profile schedules through the pinned Python used-profiles projection,
    /// where the first profile-name position is retained and the last profile with
    /// that case-sensitive name supplies the schedules. This can intentionally leave
    /// an earlier duplicate-name zone referencing a schedule that is not emitted.
    /// </summary>
    public bool UseLegacySimpleDragonUsedProfileScheduleSelection { get; set; }

    /// <summary>
    /// Retains the HVAC topology emitted by the pinned Python SimpleDragon
    /// conversion when it differs from the native InvisibleDragon topology.
    /// </summary>
    public bool UseLegacySimpleDragonHvacTopology { get; set; }

    /// <summary>
    /// Models SimpleDragon energy-recovery ventilation as the reduced
    /// ZoneVentilation load emitted by the pinned Python conversion instead
    /// of native explicit heat-recovery equipment.
    /// </summary>
    public bool UseLegacySimpleDragonVentilation { get; set; }
}

/// <summary>
/// A complete, Rhino-independent building energy model that assembles deterministic EnergyPlus objects.
/// </summary>
public sealed class EnergyModel
{
    /// <summary>
    /// EnergyPlus versions supported by the pinned InvisibleDragon model contract.
    /// A fresh read-only collection is returned for every access.
    /// </summary>
    public static IReadOnlyList<EnergyPlusVersion> SupportedVersions =>
        new ReadOnlyCollection<EnergyPlusVersion>(new[] { EnergyPlusDefaults.DefaultVersion });

    /// <summary>
    /// Creates the exact standalone default IDF graph used by pinned InvisibleDragon 0.7.0.
    /// The returned document is fresh and does not contain a Building object.
    /// </summary>
    public static IdfDocument CreateDefaultIdfDocument()
    {
        return EnergyModelIdfAssembler.CreateDefaultDocument();
    }

    public EnergyModel(
        string name,
        IEnumerable<Zone> zones,
        IEnumerable<ZoneHvacAssignment>? hvacAssignments = null,
        IEnumerable<ZoneVentilationAssignment>? ventilationAssignments = null,
        IEnumerable<PhotovoltaicPanel>? photovoltaicPanels = null,
        double northAxisDegrees = 0,
        Terrain terrain = Terrain.Suburbs,
        OutputTableSettings? outputTables = null)
    {
        Name = DomainGuard.RequiredText(name, nameof(name));
        Zone[] zoneCopy = DomainGuard.CopyRequired(zones, nameof(zones));
        ZoneHvacAssignment[] hvacCopy = hvacAssignments is null
            ? Array.Empty<ZoneHvacAssignment>()
            : DomainGuard.CopyRequired(hvacAssignments, nameof(hvacAssignments));
        ZoneVentilationAssignment[] ventilationCopy = ventilationAssignments is null
            ? Array.Empty<ZoneVentilationAssignment>()
            : DomainGuard.CopyRequired(ventilationAssignments, nameof(ventilationAssignments));
        PhotovoltaicPanel[] photovoltaicCopy = photovoltaicPanels is null
            ? Array.Empty<PhotovoltaicPanel>()
            : DomainGuard.CopyRequired(photovoltaicPanels, nameof(photovoltaicPanels));
        NorthAxisDegrees = DomainGuard.InRange(northAxisDegrees, -360, 360, nameof(northAxisDegrees));
        if (!Enum.IsDefined(typeof(Terrain), terrain))
        {
            throw new ArgumentOutOfRangeException(nameof(terrain));
        }

        Terrain = terrain;
        Zones = new ReadOnlyCollection<Zone>(zoneCopy);
        HvacAssignments = new ReadOnlyCollection<ZoneHvacAssignment>(hvacCopy);
        VentilationAssignments = new ReadOnlyCollection<ZoneVentilationAssignment>(ventilationCopy);
        PhotovoltaicPanels = new ReadOnlyCollection<PhotovoltaicPanel>(photovoltaicCopy);
        OutputTables = outputTables ?? OutputTableSettings.Default;
    }

    public string Name { get; }

    public double NorthAxisDegrees { get; }

    public Terrain Terrain { get; }

    public IReadOnlyList<Zone> Zones { get; }

    public IReadOnlyList<ZoneHvacAssignment> HvacAssignments { get; }

    public IReadOnlyList<ZoneVentilationAssignment> VentilationAssignments { get; }

    public IReadOnlyList<PhotovoltaicPanel> PhotovoltaicPanels { get; }

    public OutputTableSettings OutputTables { get; }

    public IReadOnlyList<Surface> Surfaces => new ReadOnlyCollection<Surface>(Zones.SelectMany(zone => zone.Surfaces).ToArray());

    /// <summary>
    /// Opaque constructions assigned directly to host surfaces.
    /// Equivalent definitions retain their first object and surface encounter order.
    /// </summary>
    public IReadOnlyList<OpaqueConstruction> UsedConstructions
    {
        get
        {
            var result = new List<OpaqueConstruction>();
            foreach (Surface surface in Surfaces)
            {
                if (surface.Construction is OpaqueConstruction construction
                    && !result.Any(existing => existing.Equals(construction)))
                {
                    result.Add(construction);
                }
            }

            return new ReadOnlyCollection<OpaqueConstruction>(result);
        }
    }

    /// <summary>
    /// Layers referenced by <see cref="UsedConstructions"/> in deterministic first-use order.
    /// </summary>
    /// <remarks>
    /// InvisibleDragon 0.7.0 hashes layers by name but compares their material and thickness.
    /// Requiring both the same ordinal name and the pinned layer equality reproduces that set
    /// membership without exposing Python hash-table ordering or runtime hash collisions.
    /// </remarks>
    public IReadOnlyList<Layer> UsedLayers
    {
        get
        {
            var result = new List<Layer>();
            foreach (OpaqueConstruction construction in UsedConstructions)
            {
                foreach (Layer layer in construction.Layers)
                {
                    if (!result.Any(existing => UsedLayerEquals(existing, layer)))
                    {
                        result.Add(layer);
                    }
                }
            }

            return new ReadOnlyCollection<Layer>(result);
        }
    }

    /// <summary>
    /// Zone profiles keyed by their case-sensitive names.
    /// The first occurrence fixes the result position and the last occurrence supplies the object.
    /// </summary>
    public IReadOnlyList<ZoneProfile> UsedProfiles
    {
        get
        {
            var orderedNames = new List<string>();
            var profilesByName = new Dictionary<string, ZoneProfile>(StringComparer.Ordinal);
            foreach (Zone zone in Zones)
            {
                string name = zone.Profile.Name;
                if (!profilesByName.ContainsKey(name))
                {
                    orderedNames.Add(name);
                }

                profilesByName[name] = zone.Profile;
            }

            return new ReadOnlyCollection<ZoneProfile>(
                orderedNames.Select(name => profilesByName[name]).ToArray());
        }
    }

    /// <summary>
    /// Zones that have both an HVAC assignment and a profile-level HVAC availability schedule.
    /// Input zone order and zone object identity are preserved.
    /// </summary>
    public IReadOnlyList<Zone> ConditionedZones => new ReadOnlyCollection<Zone>(
        Zones.Where(IsConditionedZone).ToArray());

    /// <summary>
    /// Zones that do not have both an HVAC assignment and a profile-level HVAC availability schedule.
    /// Input zone order and zone object identity are preserved.
    /// </summary>
    public IReadOnlyList<Zone> UnconditionedZones => new ReadOnlyCollection<Zone>(
        Zones.Where(zone => !IsConditionedZone(zone)).ToArray());

    public ValidationResult Validate()
    {
        List<Diagnostic> diagnostics = Zones.SelectMany(zone => zone.Validate().Diagnostics).ToList();
        AddDuplicateDiagnostics(Zones, zone => zone.Id, zone => zone.Name, "ZONE", diagnostics);
        AddDuplicateDiagnostics(Surfaces, surface => surface.Id, surface => surface.Name, "SURFACE", diagnostics);
        AddDuplicateDiagnostics(Surfaces.SelectMany(surface => surface.Openings), opening => opening.Id, opening => opening.Name, "OPENING", diagnostics);
        AddDuplicateDiagnostics(PhotovoltaicPanels, panel => panel.Id, panel => panel.Name, "PV", diagnostics);

        Dictionary<EntityId, Zone> zonesById = Zones
            .GroupBy(zone => zone.Id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (IGrouping<EntityId, ZoneHvacAssignment> duplicate in HvacAssignments.GroupBy(item => item.ZoneId).Where(group => group.Count() > 1))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.MODEL.DUPLICATE_HVAC_ASSIGNMENT",
                $"Zone '{duplicate.Key}' has more than one HVAC assignment.",
                duplicate.Key,
                "Combine the equipment in one SupplyGroup."));
        }

        foreach (ZoneHvacAssignment assignment in HvacAssignments)
        {
            if (!zonesById.TryGetValue(assignment.ZoneId, out Zone? zone))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.MODEL.UNKNOWN_HVAC_ZONE",
                    $"HVAC assignment references unknown zone '{assignment.ZoneId}'.",
                    assignment.ZoneId,
                    "Use a zone identifier contained in this model."));
                continue;
            }

            if (!IsConditionedZone(zone))
            {
                continue;
            }

            if (assignment.Supply.HeatingSystems.Count > 0 && zone.Profile.HeatingSetpoint is null)
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.MODEL.MISSING_HEATING_SETPOINT",
                    $"Conditioned zone '{zone.Name}' has heating equipment but no heating setpoint schedule.",
                    zone.Id,
                    "Add a temperature heating setpoint schedule."));
            }

            if (assignment.Supply.CoolingSystems.Count > 0 && zone.Profile.CoolingSetpoint is null)
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.MODEL.MISSING_COOLING_SETPOINT",
                    $"Conditioned zone '{zone.Name}' has cooling equipment but no cooling setpoint schedule.",
                    zone.Id,
                    "Add a temperature cooling setpoint schedule."));
            }
        }

        foreach (ZoneVentilationAssignment assignment in VentilationAssignments)
        {
            if (!zonesById.ContainsKey(assignment.ZoneId))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.MODEL.UNKNOWN_VENTILATION_ZONE",
                    $"Ventilation assignment references unknown zone '{assignment.ZoneId}'.",
                    assignment.ZoneId,
                    "Use a zone identifier contained in this model."));
            }
        }

        AddConstructionIdentityDiagnostics(diagnostics);
        AddHvacIdentityDiagnostics(diagnostics);
        AddAdjacencyDiagnostics(diagnostics);
        return diagnostics.Count == 0 ? ValidationResult.Success : ValidationResult.From(diagnostics);
    }

    public IdfDocument ToIdfDocument(IddSchema? schema = null, EnergyModelIdfOptions? options = null)
    {
        options ??= new EnergyModelIdfOptions();
        ValidationResult validation = Validate();
        if (options.ThrowOnValidationErrors && !validation.IsValid)
        {
            string codes = string.Join(", ", validation.Diagnostics.Where(item => item.IsFailure).Select(item => item.Code));
            throw new InvalidOperationException($"The model cannot be assembled because validation failed: {codes}.");
        }

        return EnergyModelIdfAssembler.Assemble(this, schema, options);
    }

    internal bool IsConditionedZone(Zone zone)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(zone);
#else
        if (zone is null)
        {
            throw new ArgumentNullException(nameof(zone));
        }
#endif

        return zone.Profile.HvacAvailability is not null
            && HvacAssignments.Any(assignment => assignment.ZoneId.Equals(zone.Id));
    }

    private static bool UsedLayerEquals(Layer first, Layer second)
    {
        return StringComparer.Ordinal.Equals(first.Name, second.Name)
            && first.Equals(second);
    }

    private void AddHvacIdentityDiagnostics(List<Diagnostic> diagnostics)
    {
        IEnumerable<SupplySystem> supplySystems = HvacAssignments
            .SelectMany(assignment => assignment.Supply.Systems);
        IEnumerable<SourceSystem> sourceSystems = supplySystems
            .Select(system => system.Source)
            .Where(source => source is not null)
            .Cast<SourceSystem>();
        HvacSystem[] systems = supplySystems
            .Cast<HvacSystem>()
            .Concat(sourceSystems)
            .Concat(VentilationAssignments.Select(assignment => (HvacSystem)assignment.Ventilator))
            .Concat(PhotovoltaicPanels)
            .ToArray();
        foreach (IGrouping<EntityId, HvacSystem> duplicate in systems
            .GroupBy(system => system.Id)
            .Where(group =>
            {
                HvacSystem first = group.First();
                return group.Skip(1).Any(item => !ModelDefinitionComparer.HvacSystemEquals(first, item));
            }))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID",
                $"HVAC identifier '{duplicate.Key}' is used by conflicting system definitions.",
                duplicate.Key,
                "Assign a unique identifier or reuse the same source definition."));
        }

        foreach (IGrouping<string, HvacSystem> duplicate in systems.GroupBy(system => system.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Select(item => item.Id).Distinct().Count() > 1))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.MODEL.DUPLICATE_HVAC_NAME",
                $"HVAC name '{duplicate.Key}' is used by multiple identifiers.",
                duplicate.First().Id,
                "Use unique system names to keep IDF references unambiguous."));
        }
    }

    private void AddConstructionIdentityDiagnostics(List<Diagnostic> diagnostics)
    {
        var constructionUsages = Surfaces
            .Select(surface => (OwnerId: surface.Id, Definition: surface.Construction))
            .Concat(Surfaces.SelectMany(surface => surface.Doors.Select(
                door => (OwnerId: door.Id, Definition: door.Construction))))
            .ToArray();

        foreach (var duplicate in constructionUsages
            .GroupBy(usage => usage.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group =>
            {
                ISurfaceConstruction first = group.First().Definition;
                return group.Skip(1).Any(
                    usage => !ModelDefinitionComparer.SurfaceConstructionEquals(first, usage.Definition));
            }))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.MODEL.CONFLICTING_CONSTRUCTION_NAME",
                $"Construction name '{duplicate.Key}' is used by conflicting definitions.",
                duplicate.First().OwnerId,
                "Use a unique construction name or reuse an identical definition."));
        }

        var layersByName = new Dictionary<string, (EntityId OwnerId, Layer Definition)>(
            StringComparer.OrdinalIgnoreCase);
        var reportedLayerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var usage in constructionUsages)
        {
            if (usage.Definition is not OpaqueConstruction opaque)
            {
                continue;
            }

            foreach (Layer layer in opaque.Layers)
            {
                if (!layersByName.TryGetValue(layer.Name, out var previous))
                {
                    layersByName.Add(layer.Name, (usage.OwnerId, layer));
                }
                else if (!ModelDefinitionComparer.LayerEquals(previous.Definition, layer)
                    && reportedLayerNames.Add(layer.Name))
                {
                    diagnostics.Add(Error(
                        "INVISIBLEDRAGON.MODEL.CONFLICTING_MATERIAL_NAME",
                        $"Material name '{layer.Name}' is used by conflicting layer definitions.",
                        usage.OwnerId,
                        "Use a unique material name or reuse identical thickness and material properties."));
                }
            }
        }

        var glazingUsages = Surfaces
            .SelectMany(surface => surface.Windows.Select(
                window => (OwnerId: window.Id, Definition: window.Glazing)));
        foreach (var duplicate in glazingUsages
            .GroupBy(usage => usage.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group =>
            {
                Glazing first = group.First().Definition;
                return group.Skip(1).Any(
                    usage => !ModelDefinitionComparer.GlazingEquals(first, usage.Definition));
            }))
        {
            diagnostics.Add(Error(
                "INVISIBLEDRAGON.MODEL.CONFLICTING_GLAZING_NAME",
                $"Glazing name '{duplicate.Key}' is used by conflicting definitions.",
                duplicate.First().OwnerId,
                "Use a unique glazing name or reuse identical thermal properties."));
        }
    }

    private void AddAdjacencyDiagnostics(List<Diagnostic> diagnostics)
    {
        Dictionary<EntityId, Surface> byId = Surfaces
            .GroupBy(surface => surface.Id)
            .ToDictionary(group => group.Key, group => group.First());
        HashSet<EntityId> validatedSurfaceIds = new();
        foreach (Surface surface in Surfaces.Where(item => item.Boundary.Condition == SurfaceBoundaryCondition.Zone))
        {
            EntityId adjacentId = surface.Boundary.AdjacentSurfaceId!;
            if (!byId.TryGetValue(adjacentId, out Surface? adjacent))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.MODEL.ADJACENT_SURFACE_MISSING",
                    $"Surface '{surface.Name}' references missing adjacent surface '{adjacentId}'.",
                    surface.Id,
                    "Include both zones and both matched surfaces in the model."));
            }
            else if (adjacent.Boundary.AdjacentSurfaceId is null || !adjacent.Boundary.AdjacentSurfaceId.Equals(surface.Id))
            {
                diagnostics.Add(Error(
                    "INVISIBLEDRAGON.MODEL.ADJACENCY_NOT_RECIPROCAL",
                    $"Surface '{surface.Name}' does not have a reciprocal reference from '{adjacent.Name}'.",
                    surface.Id,
                    "Match both surfaces with SurfaceAdjacency.Match."));
            }
            else if (validatedSurfaceIds.Add(surface.Id))
            {
                validatedSurfaceIds.Add(adjacent.Id);
                diagnostics.AddRange(SurfaceAdjacency.ValidateMatch(surface, adjacent).Diagnostics);
            }
        }
    }

    private static void AddDuplicateDiagnostics<T>(
        IEnumerable<T> items,
        Func<T, EntityId> id,
        Func<T, string> name,
        string kind,
        List<Diagnostic> diagnostics)
    {
        foreach (IGrouping<EntityId, T> duplicate in items.GroupBy(id).Where(group => group.Count() > 1))
        {
            diagnostics.Add(Error(
                $"INVISIBLEDRAGON.MODEL.DUPLICATE_{kind}_ID",
                $"{kind} identifier '{duplicate.Key}' occurs more than once.",
                duplicate.Key,
                "Assign stable unique identifiers."));
        }

        foreach (IGrouping<string, T> duplicate in items.GroupBy(name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            diagnostics.Add(Error(
                $"INVISIBLEDRAGON.MODEL.DUPLICATE_{kind}_NAME",
                $"{kind} name '{duplicate.Key}' occurs more than once.",
                id(duplicate.First()),
                "Use unique names because EnergyPlus resolves references by name."));
        }
    }

    private static Diagnostic Error(string code, string message, EntityId id, string action) =>
        new(code, DiagnosticSeverity.Error, message, id, suggestedAction: action);
}
