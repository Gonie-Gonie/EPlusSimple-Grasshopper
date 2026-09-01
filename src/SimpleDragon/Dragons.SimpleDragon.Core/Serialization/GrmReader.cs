using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

/// <summary>
/// Diagnostic GRM 0.7 JSON reader with reference resolution against packaged profiles and weather metadata.
/// </summary>
public static class GrmReader
{
    public static GrmReadResult Read(string json, SimpleDragonDatabase? database = null)
    {
        DomainSupport.NotNull(json, nameof(json));
        var parser = new Parser(database ?? SimpleDragonDatabase.Default);
        return parser.Read(json);
    }

    public static GrmReadResult ReadFile(string path, SimpleDragonDatabase? database = null)
    {
        string source = DomainSupport.RequiredText(path, nameof(path));
        try
        {
            string json = File.ReadAllText(source, new UTF8Encoding(false, true));
            return Read(json, database);
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is DecoderFallbackException)
        {
            return new GrmReadResult(
                null,
                new[]
                {
                    new Diagnostic(
                        "SD.GRM.FILE_READ_FAILED",
                        DiagnosticSeverity.Error,
                        "Could not read GRM file '" + source + "': " + exception.Message,
                        suggestedAction: "Verify that the file exists, is UTF-8, and is readable."),
                });
        }
    }

    private sealed class Parser
    {
        private readonly SimpleDragonDatabase _database;
        private readonly List<Diagnostic> _diagnostics = new();
        private readonly HashSet<string> _surfaceIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _fenestrationIds = new(StringComparer.Ordinal);

        public Parser(SimpleDragonDatabase database)
        {
            _database = database;
        }

        public GrmReadResult Read(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 128,
                    });
                GreenRetrofitModel model = ParseModel(document.RootElement);
                return new GrmReadResult(model, _diagnostics.AsReadOnly());
            }
            catch (JsonException exception)
            {
                _diagnostics.Add(new Diagnostic(
                    "SD.GRM.JSON_INVALID",
                    DiagnosticSeverity.Error,
                    "The GRM document is not valid JSON: " + exception.Message,
                    suggestedAction: "Correct the JSON syntax and retry."));
            }
            catch (GrmParseStopException)
            {
                // A precise diagnostic was already recorded at the failure location.
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is OverflowException)
            {
                _diagnostics.Add(new Diagnostic(
                    "SD.GRM.DOMAIN_INVALID",
                    DiagnosticSeverity.Error,
                    "The GRM values cannot form a valid SimpleDragon model: " + exception.Message,
                    suggestedAction: "Correct the reported range, enum, or relationship."));
            }

            return new GrmReadResult(null, _diagnostics.AsReadOnly());
        }

        private GreenRetrofitModel ParseModel(JsonElement root)
        {
            RequireKind(root, JsonValueKind.Object, "$", "object");
            JsonElement building = RequiredObject(root, "building", "$");
            IReadOnlyList<Material> materials = ParseMaterials(RequiredArray(root, "materials", "$"));
            var materialById = ToDictionary(materials, item => item.Id.Value, "material", "$.materials");
            IReadOnlyList<SurfaceConstruction> surfaces = ParseSurfaceConstructions(
                RequiredArray(root, "surface_constructions", "$"),
                materialById);
            var surfaceById = ToDictionary(
                surfaces,
                item => item.Id.Value,
                "surface construction",
                "$.surface_constructions");
            IReadOnlyList<FenestrationConstruction> fenestrations = ParseFenestrationConstructions(
                RequiredArray(root, "fenestration_constructions", "$"));
            var fenestrationById = ToDictionary(
                fenestrations,
                item => item.Id.Value,
                "fenestration construction",
                "$.fenestration_constructions");

            IReadOnlyList<SourceSystem> sources = ParseSourceSystems(
                RequiredProperty(building, "source_systems", "$.building"));
            var sourceById = ToDictionary(
                sources,
                item => item.Id.Value,
                "source system",
                "$.building.source_systems");
            IReadOnlyList<SupplySystem> supplies = ParseSupplySystems(
                RequiredProperty(building, "supply_systems", "$.building"),
                sourceById);
            var supplyById = ToDictionary(
                supplies,
                item => item.Id.Value,
                "supply system",
                "$.building.supply_systems");
            IReadOnlyList<VentilationSystem> ventilationSystems = ParseVentilationSystems(
                RequiredArray(building, "ventilation_systems", "$.building"));
            var ventilationById = ToDictionary(
                ventilationSystems,
                item => item.Id.Value,
                "ventilation system",
                "$.building.ventilation_systems");
            IReadOnlyList<PhotovoltaicSystem> photovoltaicSystems = ParsePhotovoltaicSystems(
                RequiredArray(building, "photovoltaic_systems", "$.building"));
            IReadOnlyList<BuildingFloor> floors = ParseFloors(
                RequiredArray(building, "floors", "$.building"),
                surfaceById,
                fenestrationById,
                supplyById,
                ventilationById);

            ValidateAdjacentZones(floors);
            string address = RequiredString(building, "address", "$.building");
            DateTime vintage = ParseVintage(RequiredArray(building, "vintage", "$.building"));
            LookupResult<WeatherSelection> weatherLookup = _database.Weather.FindByAddress(address, vintage);
            foreach (Diagnostic diagnostic in weatherLookup.Diagnostics)
            {
                _diagnostics.Add(diagnostic);
            }

            return new GreenRetrofitModel(
                RequiredString(building, "name", "$.building"),
                RequiredNumber(building, "north_axis", "$.building"),
                address,
                vintage,
                RequiredBoolean(building, "is_multifamily_housing", "$.building"),
                floors,
                materials,
                surfaces,
                fenestrations,
                sources,
                supplies,
                ventilationSystems,
                photovoltaicSystems,
                weatherLookup.Value);
        }

        private ReadOnlyCollection<Material> ParseMaterials(JsonElement array)
        {
            var items = new List<Material>(array.GetArrayLength());
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string path = "$.materials[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                RequireKind(item, JsonValueKind.Object, path, "object");
                items.Add(new Material(
                    RequiredString(item, "name", path),
                    RequiredNumber(item, "conductivity", path),
                    RequiredNumber(item, "density", path),
                    RequiredNumber(item, "specific_heat", path),
                    RequiredId(item, path)));
                index++;
            }

            return items.AsReadOnly();
        }

        private ReadOnlyCollection<SurfaceConstruction> ParseSurfaceConstructions(
            JsonElement array,
            Dictionary<string, Material> materials)
        {
            var items = new List<SurfaceConstruction>(array.GetArrayLength());
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string path = "$.surface_constructions[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                RequireKind(item, JsonValueKind.Object, path, "object");
                JsonElement layerArray = RequiredArray(item, "layers", path);
                var layers = new List<SurfaceConstructionLayer>(layerArray.GetArrayLength());
                int layerIndex = 0;
                foreach (JsonElement layer in layerArray.EnumerateArray())
                {
                    string layerPath = path + ".layers[" + layerIndex.ToString(CultureInfo.InvariantCulture) + "]";
                    RequireKind(layer, JsonValueKind.Object, layerPath, "object");
                    string materialId = RequiredString(layer, "material_id", layerPath);
                    if (!materials.TryGetValue(materialId, out Material? material))
                    {
                        Stop(
                            "SD.GRM.MATERIAL_REFERENCE_NOT_FOUND",
                            layerPath + ".material_id",
                            "Material '" + materialId + "' is not defined.",
                            "Add the material or correct material_id.");
                    }

                    layers.Add(new SurfaceConstructionLayer(
                        material!,
                        RequiredNumber(layer, "thickness", layerPath)));
                    layerIndex++;
                }

                items.Add(new SurfaceConstruction(
                    RequiredString(item, "name", path),
                    layers,
                    RequiredId(item, path)));
                index++;
            }

            return items.AsReadOnly();
        }

        private ReadOnlyCollection<FenestrationConstruction> ParseFenestrationConstructions(JsonElement array)
        {
            var items = new List<FenestrationConstruction>(array.GetArrayLength());
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string path = "$.fenestration_constructions[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                RequireKind(item, JsonValueKind.Object, path, "object");
                bool transparent = RequiredBoolean(item, "is_transparent", path);
                double? solarGain = OptionalNumber(item, "g", path);
                if (transparent != (solarGain.HasValue && solarGain.Value > 0d))
                {
                    Stop(
                        "SD.GRM.FENESTRATION_TRANSPARENCY_MISMATCH",
                        path,
                        "is_transparent does not agree with g.",
                        "Transparent constructions require a positive g; opaque constructions require null g.");
                }

                items.Add(new FenestrationConstruction(
                    RequiredString(item, "name", path),
                    RequiredNumber(item, "u", path),
                    solarGain,
                    RequiredId(item, path)));
                index++;
            }

            return items.AsReadOnly();
        }

        private ReadOnlyCollection<SourceSystem> ParseSourceSystems(JsonElement element)
        {
            var systems = new List<SourceSystem>();
            if (element.ValueKind == JsonValueKind.Array)
            {
                AddWarning(
                    "SD.GRM.LEGACY_FLAT_SYSTEM_ARRAY",
                    "$.building.source_systems",
                    "A flat source-system array was accepted; GRM 0.7 uses an object grouped by type.",
                    "Rewrite the model to canonical grouped GRM form.");
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    string path = "$.building.source_systems[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    SourceSystemType type = ParseSourceType(RequiredString(item, "type", path), path + ".type");
                    systems.Add(ParseSourceSystem(item, type, path));
                    index++;
                }

                return systems.AsReadOnly();
            }

            RequireKind(element, JsonValueKind.Object, "$.building.source_systems", "object");
            foreach (JsonProperty group in element.EnumerateObject())
            {
                string groupPath = "$.building.source_systems." + group.Name;
                SourceSystemType type = ParseSourceType(group.Name, groupPath);
                RequireKind(group.Value, JsonValueKind.Array, groupPath, "array");
                int index = 0;
                foreach (JsonElement item in group.Value.EnumerateArray())
                {
                    systems.Add(ParseSourceSystem(
                        item,
                        type,
                        groupPath + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"));
                    index++;
                }
            }

            return systems.AsReadOnly();
        }

        private SourceSystem ParseSourceSystem(JsonElement item, SourceSystemType type, string path)
        {
            RequireKind(item, JsonValueKind.Object, path, "object");
            var fields = new HashSet<string>(
                item.EnumerateObject().Select(property => property.Name),
                StringComparer.Ordinal);
            FuelType? fuel = null;
            if (fields.Contains("fuel_type"))
            {
                string? fuelText = NullableString(item, "fuel_type", path, requiredProperty: true);
                if (fuelText is not null)
                {
                    fuel = ParseFuel(fuelText, path + ".fuel_type");
                }
            }

            double? heatingCop = OptionalNumber(item, "cop_heating", path);
            double? coolingCop = OptionalNumber(item, "cop_cooling", path);
            double? heatingCapacity = OptionalNumber(item, "capacity_heating", path);
            double? coolingCapacity = OptionalNumber(item, "capacity_cooling", path);
            double? efficiency = OptionalNumber(item, "efficiency", path);
            bool? hotWater = OptionalBoolean(item, "hotwater_supply", path);
            CompressorType? compressor = null;
            if (fields.Contains("compressor_type"))
            {
                compressor = ParseCompressor(
                    RequiredString(item, "compressor_type", path),
                    path + ".compressor_type");
            }

            CoolingTowerType? tower = null;
            if (fields.Contains("coolingtower_type"))
            {
                tower = ParseCoolingTower(
                    RequiredString(item, "coolingtower_type", path),
                    path + ".coolingtower_type");
            }

            CoolingTowerControl? towerControl = null;
            if (fields.Contains("coolingtower_control"))
            {
                towerControl = ParseCoolingTowerControl(
                    RequiredString(item, "coolingtower_control", path),
                    path + ".coolingtower_control");
            }

            double? towerCapacity = OptionalNumber(item, "coolingtower_capacity", path);
            double? boilerEfficiency = OptionalNumber(item, "boiler_efficiency", path);

            switch (type)
            {
                case SourceSystemType.HeatPump:
                case SourceSystemType.GeothermalHeatPump:
                    fuel ??= ParseFuel(RequiredString(item, "fuel_type", path), path + ".fuel_type");
                    heatingCop ??= 3d;
                    coolingCop ??= 3d;
                    break;
                case SourceSystemType.Chiller:
                    coolingCop ??= 3d;
                    compressor ??= ParseCompressor(
                        RequiredString(item, "compressor_type", path),
                        path + ".compressor_type");
                    tower ??= ParseCoolingTower(
                        RequiredString(item, "coolingtower_type", path),
                        path + ".coolingtower_type");
                    towerControl ??= ParseCoolingTowerControl(
                        RequiredString(item, "coolingtower_control", path),
                        path + ".coolingtower_control");
                    break;
                case SourceSystemType.AbsorptionChiller:
                    fuel ??= ParseFuel(RequiredString(item, "fuel_type", path), path + ".fuel_type");
                    coolingCop ??= 0.9d;
                    boilerEfficiency ??= 0.85d;
                    break;
                case SourceSystemType.Boiler:
                    fuel ??= ParseFuel(RequiredString(item, "fuel_type", path), path + ".fuel_type");
                    efficiency ??= 0.85d;
                    hotWater ??= RequiredBoolean(item, "hotwater_supply", path);
                    break;
                case SourceSystemType.DistrictHeating:
                    hotWater ??= RequiredBoolean(item, "hotwater_supply", path);
                    break;
            }

            return new SourceSystem(
                RequiredString(item, "name", path),
                type,
                fuel,
                heatingCop,
                coolingCop,
                heatingCapacity,
                coolingCapacity,
                efficiency,
                hotWater,
                compressor,
                tower,
                towerCapacity,
                towerControl,
                boilerEfficiency,
                RequiredId(item, path),
                fields);
        }

        private ReadOnlyCollection<SupplySystem> ParseSupplySystems(
            JsonElement element,
            IReadOnlyDictionary<string, SourceSystem> sources)
        {
            var systems = new List<SupplySystem>();
            if (element.ValueKind == JsonValueKind.Array)
            {
                AddWarning(
                    "SD.GRM.LEGACY_FLAT_SYSTEM_ARRAY",
                    "$.building.supply_systems",
                    "A flat supply-system array was accepted; GRM 0.7 uses an object grouped by type.",
                    "Rewrite the model to canonical grouped GRM form.");
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    string path = "$.building.supply_systems[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    SupplySystemType type = ParseSupplyType(RequiredString(item, "type", path), path + ".type");
                    systems.Add(ParseSupplySystem(item, type, path, sources));
                    index++;
                }

                return systems.AsReadOnly();
            }

            RequireKind(element, JsonValueKind.Object, "$.building.supply_systems", "object");
            foreach (JsonProperty group in element.EnumerateObject())
            {
                string groupPath = "$.building.supply_systems." + group.Name;
                SupplySystemType type = ParseSupplyType(group.Name, groupPath);
                RequireKind(group.Value, JsonValueKind.Array, groupPath, "array");
                int index = 0;
                foreach (JsonElement item in group.Value.EnumerateArray())
                {
                    systems.Add(ParseSupplySystem(
                        item,
                        type,
                        groupPath + "[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        sources));
                    index++;
                }
            }

            return systems.AsReadOnly();
        }

        private SupplySystem ParseSupplySystem(
            JsonElement item,
            SupplySystemType type,
            string path,
            IReadOnlyDictionary<string, SourceSystem> sources)
        {
            RequireKind(item, JsonValueKind.Object, path, "object");
            var fields = new HashSet<string>(
                item.EnumerateObject().Select(property => property.Name),
                StringComparer.Ordinal);
            string? sourceId = fields.Contains("source_system_id")
                ? NullableString(item, "source_system_id", path, requiredProperty: true)
                : null;
            SourceSystem? source = null;
            if (sourceId is not null && !sources.TryGetValue(sourceId, out source))
            {
                AddReferenceError(
                    "SD.GRM.SOURCE_SYSTEM_REFERENCE_NOT_FOUND",
                    path + ".source_system_id",
                    "Source system '" + sourceId + "' is not defined.");
            }

            return new SupplySystem(
                RequiredString(item, "name", path),
                type,
                sourceId,
                source,
                OptionalNumber(item, "cop_cooling", path)
                    ?? (type == SupplySystemType.PackagedAirConditioner ? 3d : null),
                OptionalNumber(item, "capacity_cooling", path),
                OptionalNumber(item, "capacity_heating", path),
                RequiredId(item, path),
                fields);
        }

        private ReadOnlyCollection<VentilationSystem> ParseVentilationSystems(JsonElement array)
        {
            var systems = new List<VentilationSystem>(array.GetArrayLength());
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string path = "$.building.ventilation_systems[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                RequireKind(item, JsonValueKind.Object, path, "object");
                systems.Add(new VentilationSystem(
                    RequiredString(item, "name", path),
                    RequiredNumber(item, "airflow_rate", path),
                    OptionalNumber(item, "efficiency_heating", path) ?? 0.7d,
                    OptionalNumber(item, "efficiency_cooling", path) ?? 0.45d,
                    RequiredId(item, path)));
                index++;
            }

            return systems.AsReadOnly();
        }

        private ReadOnlyCollection<PhotovoltaicSystem> ParsePhotovoltaicSystems(JsonElement array)
        {
            var systems = new List<PhotovoltaicSystem>(array.GetArrayLength());
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string path = "$.building.photovoltaic_systems[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                RequireKind(item, JsonValueKind.Object, path, "object");
                systems.Add(new PhotovoltaicSystem(
                    RequiredString(item, "name", path),
                    RequiredNumber(item, "area", path),
                    RequiredNumber(item, "efficiency", path),
                    RequiredNumber(item, "azimuth", path),
                    RequiredNumber(item, "tilt", path),
                    RequiredId(item, path)));
                index++;
            }

            return systems.AsReadOnly();
        }

        private ReadOnlyCollection<BuildingFloor> ParseFloors(
            JsonElement array,
            IReadOnlyDictionary<string, SurfaceConstruction> surfaceConstructions,
            IReadOnlyDictionary<string, FenestrationConstruction> fenestrationConstructions,
            IReadOnlyDictionary<string, SupplySystem> supplies,
            IReadOnlyDictionary<string, VentilationSystem> ventilationSystems)
        {
            var floors = new List<BuildingFloor>(array.GetArrayLength());
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string path = "$.building.floors[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                RequireKind(item, JsonValueKind.Object, path, "object");
                int floorNumber = RequiredInteger(item, "floor_number", path);
                JsonElement zoneArray = RequiredArray(item, "zones", path);
                var zones = new List<Zone>(zoneArray.GetArrayLength());
                int zoneIndex = 0;
                foreach (JsonElement zone in zoneArray.EnumerateArray())
                {
                    zones.Add(ParseZone(
                        zone,
                        floorNumber,
                        path + ".zones[" + zoneIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        surfaceConstructions,
                        fenestrationConstructions,
                        supplies,
                        ventilationSystems));
                    zoneIndex++;
                }

                floors.Add(new BuildingFloor(floorNumber, zones));
                index++;
            }

            return floors.AsReadOnly();
        }

        private Zone ParseZone(
            JsonElement item,
            int floorNumber,
            string path,
            IReadOnlyDictionary<string, SurfaceConstruction> surfaceConstructions,
            IReadOnlyDictionary<string, FenestrationConstruction> fenestrationConstructions,
            IReadOnlyDictionary<string, SupplySystem> supplies,
            IReadOnlyDictionary<string, VentilationSystem> ventilationSystems)
        {
            RequireKind(item, JsonValueKind.Object, path, "object");
            string profileName = RequiredString(item, "profile", path);
            LookupResult<UsageProfile> profileLookup = _database.UsageProfiles.Find(profileName);
            foreach (Diagnostic diagnostic in profileLookup.Diagnostics)
            {
                _diagnostics.Add(new Diagnostic(
                    "SD.GRM.PROFILE_REFERENCE_NOT_FOUND",
                    diagnostic.Severity,
                    path + ".profile: " + diagnostic.Message,
                    suggestedAction: diagnostic.SuggestedAction));
            }

            var supplyAssignments = new List<SupplySystemAssignment>();
            JsonElement supplyArray = RequiredArray(item, "supply_system_ids", path);
            int supplyIndex = 0;
            foreach (JsonElement idElement in supplyArray.EnumerateArray())
            {
                string idPath = path + ".supply_system_ids[" + supplyIndex.ToString(CultureInfo.InvariantCulture) + "]";
                string supplyId = StringValue(idElement, idPath);
                supplies.TryGetValue(supplyId, out SupplySystem? supply);
                if (supply is null)
                {
                    AddReferenceError(
                        "SD.GRM.SUPPLY_SYSTEM_REFERENCE_NOT_FOUND",
                        idPath,
                        "Supply system '" + supplyId + "' is not defined.");
                }

                supplyAssignments.Add(new SupplySystemAssignment(supplyId, supply));
                supplyIndex++;
            }

            var ventilationAssignments = new List<VentilationAssignment>();
            JsonElement ventilationArray = RequiredArray(item, "ventilation_systems", path);
            int ventilationIndex = 0;
            foreach (JsonElement assignment in ventilationArray.EnumerateArray())
            {
                string assignmentPath = path + ".ventilation_systems["
                    + ventilationIndex.ToString(CultureInfo.InvariantCulture) + "]";
                RequireKind(assignment, JsonValueKind.Object, assignmentPath, "object");
                string ventilationId = RequiredString(assignment, "id", assignmentPath);
                ventilationSystems.TryGetValue(ventilationId, out VentilationSystem? system);
                if (system is null)
                {
                    AddReferenceError(
                        "SD.GRM.VENTILATION_REFERENCE_NOT_FOUND",
                        assignmentPath + ".id",
                        "Ventilation system '" + ventilationId + "' is not defined.");
                }

                ventilationAssignments.Add(new VentilationAssignment(
                    ventilationId,
                    RequiredInteger(assignment, "count", assignmentPath),
                    system));
                ventilationIndex++;
            }

            JsonElement surfaceArray = RequiredArray(item, "surfaces", path);
            var zoneSurfaces = new List<Surface>(surfaceArray.GetArrayLength());
            int surfaceIndex = 0;
            foreach (JsonElement surface in surfaceArray.EnumerateArray())
            {
                zoneSurfaces.Add(ParseSurface(
                    surface,
                    path + ".surfaces[" + surfaceIndex.ToString(CultureInfo.InvariantCulture) + "]",
                    surfaceConstructions,
                    fenestrationConstructions));
                surfaceIndex++;
            }

            return new Zone(
                RequiredString(item, "name", path),
                floorNumber,
                RequiredNumber(item, "height", path),
                zoneSurfaces,
                profileName,
                profileLookup.Value,
                OptionalNumber(item, "light_density", path),
                supplyAssignments,
                ventilationAssignments,
                RequiredId(item, path));
        }

        private Surface ParseSurface(
            JsonElement item,
            string path,
            IReadOnlyDictionary<string, SurfaceConstruction> surfaceConstructions,
            IReadOnlyDictionary<string, FenestrationConstruction> fenestrationConstructions)
        {
            RequireKind(item, JsonValueKind.Object, path, "object");
            EntityId id = RequiredId(item, path);
            if (!_surfaceIds.Add(id.Value))
            {
                AddReferenceError(
                    "SD.GRM.DUPLICATE_SURFACE_ID",
                    path + ".id",
                    "Surface ID '" + id.Value + "' is duplicated.");
            }

            SurfaceType type = ParseSurfaceType(RequiredString(item, "type", path), path + ".type");
            SurfaceBoundaryCondition boundary = ParseBoundary(
                RequiredString(item, "boundary_condition", path),
                path + ".boundary_condition");
            string? constructionId = NullableString(item, "construction_id", path, requiredProperty: true);
            SurfaceConstruction? construction = null;
            if (constructionId is not null
                && !StringComparer.Ordinal.Equals(constructionId, "open")
                && !surfaceConstructions.TryGetValue(constructionId, out construction))
            {
                AddReferenceError(
                    "SD.GRM.SURFACE_CONSTRUCTION_REFERENCE_NOT_FOUND",
                    path + ".construction_id",
                    "Surface construction '" + constructionId + "' is not defined.");
            }

            JsonElement openingArray = RequiredArray(item, "fenestrations", path);
            var openings = new List<Fenestration>(openingArray.GetArrayLength());
            int openingIndex = 0;
            foreach (JsonElement opening in openingArray.EnumerateArray())
            {
                openings.Add(ParseFenestration(
                    opening,
                    path + ".fenestrations[" + openingIndex.ToString(CultureInfo.InvariantCulture) + "]",
                    fenestrationConstructions));
                openingIndex++;
            }

            double area = RequiredNumber(item, "area", path);
            double openingArea = openings.Sum(opening => opening.Area);
            if (openingArea > area + 1e-9)
            {
                _diagnostics.Add(new Diagnostic(
                    "SD.GRM.OPENING_AREA_EXCEEDS_SURFACE",
                    DiagnosticSeverity.Error,
                    path + ": fenestration area "
                    + openingArea.ToString("R", CultureInfo.InvariantCulture)
                    + " exceeds surface area " + area.ToString("R", CultureInfo.InvariantCulture) + ".",
                    suggestedAction: "Reduce opening areas or increase the parent surface area."));
            }

            return new Surface(
                RequiredString(item, "name", path),
                type,
                boundary,
                area,
                OptionalNumber(item, "azimuth", path),
                constructionId,
                construction,
                openings,
                OptionalNumber(item, "coolroof_reflectance", path),
                NullableString(item, "adjacent_zone_id", path),
                id);
        }

        private Fenestration ParseFenestration(
            JsonElement item,
            string path,
            IReadOnlyDictionary<string, FenestrationConstruction> constructions)
        {
            RequireKind(item, JsonValueKind.Object, path, "object");
            EntityId id = RequiredId(item, path);
            if (!_fenestrationIds.Add(id.Value))
            {
                AddReferenceError(
                    "SD.GRM.DUPLICATE_FENESTRATION_ID",
                    path + ".id",
                    "Fenestration ID '" + id.Value + "' is duplicated.");
            }

            string constructionId = RequiredString(item, "construction_id", path);
            constructions.TryGetValue(constructionId, out FenestrationConstruction? construction);
            if (construction is null)
            {
                AddReferenceError(
                    "SD.GRM.FENESTRATION_CONSTRUCTION_REFERENCE_NOT_FOUND",
                    path + ".construction_id",
                    "Fenestration construction '" + constructionId + "' is not defined.");
            }

            FenestrationType type = ParseFenestrationType(
                RequiredString(item, "type", path),
                path + ".type");
            BlindType? blind = null;
            string? blindText = NullableString(item, "blind", path);
            if (blindText is not null)
            {
                blind = ParseBlind(blindText, path + ".blind");
            }

            return new Fenestration(
                RequiredString(item, "name", path),
                type,
                RequiredNumber(item, "area", path),
                constructionId,
                construction,
                blind,
                id);
        }

        private void ValidateAdjacentZones(IReadOnlyList<BuildingFloor> floors)
        {
            var zoneIds = new HashSet<string>(
                floors.SelectMany(floor => floor.Zones).Select(zone => zone.Id.Value),
                StringComparer.Ordinal);
            foreach (Surface surface in floors.SelectMany(floor => floor.Zones).SelectMany(zone => zone.Surfaces))
            {
                if (surface.BoundaryCondition == SurfaceBoundaryCondition.Zone
                    && !zoneIds.Contains(surface.AdjacentZoneId!))
                {
                    AddReferenceError(
                        "SD.GRM.ADJACENT_ZONE_REFERENCE_NOT_FOUND",
                        "surface " + surface.Id.Value + ".adjacent_zone_id",
                        "Adjacent zone '" + surface.AdjacentZoneId + "' is not defined.");
                }
            }
        }

        private DateTime ParseVintage(JsonElement array)
        {
            if (array.GetArrayLength() != 3)
            {
                Stop(
                    "SD.GRM.VINTAGE_INVALID",
                    "$.building.vintage",
                    "Vintage must be [year, month, day].",
                    "Provide exactly three integer date components.");
            }

            int[] values = array.EnumerateArray()
                .Select((element, index) => IntegerValue(
                    element,
                    "$.building.vintage[" + index.ToString(CultureInfo.InvariantCulture) + "]"))
                .ToArray();
            try
            {
                return new DateTime(values[0], values[1], values[2]);
            }
            catch (ArgumentOutOfRangeException)
            {
                Stop(
                    "SD.GRM.VINTAGE_INVALID",
                    "$.building.vintage",
                    "Vintage is not a valid calendar date.",
                    "Correct the year, month, and day values.");
                throw;
            }
        }

        private EntityId RequiredId(JsonElement item, string path)
        {
            string value = RequiredString(item, "id", path);
            try
            {
                return new EntityId(value);
            }
            catch (ArgumentException)
            {
                Stop(
                    "SD.GRM.ID_INVALID",
                    path + ".id",
                    "ID '" + value + "' is invalid.",
                    "Use a non-empty ID without whitespace or control characters.");
                throw;
            }
        }

        private static Dictionary<string, T> ToDictionary<T>(
            IEnumerable<T> items,
            Func<T, string> keySelector,
            string description,
            string path)
            where T : class
        {
            var dictionary = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T item in items)
            {
                string key = keySelector(item);
                if (dictionary.ContainsKey(key))
                {
                    throw new ArgumentException(
                        path + " contains duplicate " + description + " ID '" + key + "'.");
                }

                dictionary.Add(key, item);
            }

            return dictionary;
        }

        private SourceSystemType ParseSourceType(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseSourceSystemType(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.SOURCE_SYSTEM_TYPE_UNKNOWN", path, "Unknown source-system type '" + value + "'.");
                throw;
            }
        }

        private SupplySystemType ParseSupplyType(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseSupplySystemType(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.SUPPLY_SYSTEM_TYPE_UNKNOWN", path, "Unknown supply-system type '" + value + "'.");
                throw;
            }
        }

        private FuelType ParseFuel(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseFuel(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.FUEL_TYPE_UNKNOWN", path, "Unknown fuel type '" + value + "'.");
                throw;
            }
        }

        private SurfaceType ParseSurfaceType(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseSurfaceType(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.SURFACE_TYPE_UNKNOWN", path, "Unknown surface type '" + value + "'.");
                throw;
            }
        }

        private SurfaceBoundaryCondition ParseBoundary(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseBoundary(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.BOUNDARY_UNKNOWN", path, "Unknown boundary condition '" + value + "'.");
                throw;
            }
        }

        private FenestrationType ParseFenestrationType(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseFenestrationType(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.FENESTRATION_TYPE_UNKNOWN", path, "Unknown fenestration type '" + value + "'.");
                throw;
            }
        }

        private BlindType ParseBlind(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseBlind(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.BLIND_TYPE_UNKNOWN", path, "Unknown blind type '" + value + "'.");
                throw;
            }
        }

        private CompressorType ParseCompressor(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseCompressor(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.COMPRESSOR_TYPE_UNKNOWN", path, "Unknown compressor type '" + value + "'.");
                throw;
            }
        }

        private CoolingTowerType ParseCoolingTower(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseCoolingTower(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.COOLING_TOWER_TYPE_UNKNOWN", path, "Unknown cooling-tower type '" + value + "'.");
                throw;
            }
        }

        private CoolingTowerControl ParseCoolingTowerControl(string value, string path)
        {
            try
            {
                return GrmVocabulary.ParseCoolingTowerControl(value);
            }
            catch (ArgumentException)
            {
                Stop("SD.GRM.COOLING_TOWER_CONTROL_UNKNOWN", path, "Unknown cooling-tower control '" + value + "'.");
                throw;
            }
        }

        private JsonElement RequiredProperty(JsonElement parent, string name, string path)
        {
            if (!parent.TryGetProperty(name, out JsonElement value))
            {
                Stop(
                    "SD.GRM.REQUIRED_PROPERTY_MISSING",
                    path + "." + name,
                    "Required property '" + name + "' is missing.",
                    "Add the property using the GRM 0.7 schema.");
            }

            return value;
        }

        private JsonElement RequiredObject(JsonElement parent, string name, string path)
        {
            JsonElement value = RequiredProperty(parent, name, path);
            RequireKind(value, JsonValueKind.Object, path + "." + name, "object");
            return value;
        }

        private JsonElement RequiredArray(JsonElement parent, string name, string path)
        {
            JsonElement value = RequiredProperty(parent, name, path);
            RequireKind(value, JsonValueKind.Array, path + "." + name, "array");
            return value;
        }

        private string RequiredString(JsonElement parent, string name, string path)
        {
            JsonElement value = RequiredProperty(parent, name, path);
            return StringValue(value, path + "." + name);
        }

        private string StringValue(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                Stop("SD.GRM.TYPE_MISMATCH", path, "Expected a JSON string.");
            }

            string? text = value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                Stop("SD.GRM.STRING_EMPTY", path, "A non-empty string is required.");
            }

            return text!;
        }

        private string? NullableString(
            JsonElement parent,
            string name,
            string path,
            bool requiredProperty = false)
        {
            if (!parent.TryGetProperty(name, out JsonElement value))
            {
                if (requiredProperty)
                {
                    Stop(
                        "SD.GRM.REQUIRED_PROPERTY_MISSING",
                        path + "." + name,
                        "Required property '" + name + "' is missing.");
                }

                return null;
            }

            return value.ValueKind == JsonValueKind.Null
                ? null
                : StringValue(value, path + "." + name);
        }

        private double RequiredNumber(JsonElement parent, string name, string path)
        {
            JsonElement value = RequiredProperty(parent, name, path);
            return NumberValue(value, path + "." + name);
        }

        private double? OptionalNumber(JsonElement parent, string name, string path)
        {
            if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return NumberValue(value, path + "." + name);
        }

        private double NumberValue(JsonElement value, string path)
        {
            double number = 0d;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out number)
                || double.IsNaN(number) || double.IsInfinity(number))
            {
                Stop("SD.GRM.TYPE_MISMATCH", path, "Expected a finite JSON number.");
            }

            return number;
        }

        private int RequiredInteger(JsonElement parent, string name, string path)
        {
            return IntegerValue(RequiredProperty(parent, name, path), path + "." + name);
        }

        private int IntegerValue(JsonElement value, string path)
        {
            int number = 0;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out number))
            {
                Stop("SD.GRM.TYPE_MISMATCH", path, "Expected a 32-bit JSON integer.");
            }

            return number;
        }

        private bool RequiredBoolean(JsonElement parent, string name, string path)
        {
            JsonElement value = RequiredProperty(parent, name, path);
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                Stop("SD.GRM.TYPE_MISMATCH", path + "." + name, "Expected a JSON boolean.");
            }

            return value.GetBoolean();
        }

        private bool? OptionalBoolean(JsonElement parent, string name, string path)
        {
            if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                Stop("SD.GRM.TYPE_MISMATCH", path + "." + name, "Expected a JSON boolean or null.");
            }

            return value.GetBoolean();
        }

        private void RequireKind(JsonElement value, JsonValueKind kind, string path, string description)
        {
            if (value.ValueKind != kind)
            {
                Stop("SD.GRM.TYPE_MISMATCH", path, "Expected a JSON " + description + ".");
            }
        }

        private void AddReferenceError(string code, string path, string message)
        {
            _diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                path + ": " + message,
                suggestedAction: "Correct the ID or add the referenced object."));
        }

        private void AddWarning(string code, string path, string message, string action)
        {
            _diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Warning,
                path + ": " + message,
                suggestedAction: action));
        }

        private void Stop(string code, string path, string message, string? action = null)
        {
            _diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                path + ": " + message,
                suggestedAction: action));
            throw new GrmParseStopException();
        }
    }

    private sealed class GrmParseStopException : Exception
    {
    }
}
