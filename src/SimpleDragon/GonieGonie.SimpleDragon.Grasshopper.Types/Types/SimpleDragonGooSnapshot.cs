using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Types;

internal static class SimpleDragonGooSnapshot
{
    private const string Schema = SimpleDragonTypeLibrary.SchemaVersion;
    private static readonly UsageDay[] UsageDays =
        (UsageDay[])Enum.GetValues(typeof(UsageDay));
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    internal static string Serialize<T>(T value)
        where T : class
    {
        (string kind, string payload) = value switch
        {
            Diagnostic diagnostic => ("diagnostic", ToJson(diagnostic)),
            SimpleDragonBatchCase batchCase => ("batch-case", ToJson(BatchCaseSnapshot.From(batchCase))),
            OpeningDefinition opening => ("opening-definition", ToJson(OpeningDefinitionSnapshot.From(opening))),
            SurfaceDefinition surface => ("surface-definition", ToJson(SurfaceDefinitionSnapshot.From(surface))),
            ZoneDefinition zone => ("zone-definition", ToJson(ZoneDefinitionSnapshot.From(zone))),
            Material material => ("material", ToJson(MaterialSnapshot.From(material))),
            SurfaceConstructionLayer layer => ("surface-construction-layer", ToJson(SurfaceLayerSnapshot.From(layer))),
            SurfaceConstruction construction => ("surface-construction", ToJson(SurfaceConstructionSnapshot.From(construction))),
            FenestrationConstruction construction => ("fenestration-construction", ToJson(FenestrationConstructionSnapshot.From(construction))),
            UsageProfile profile => ("usage-profile", ToJson(UsageProfileSnapshot.From(profile))),
            Surface surface => ("surface", ToJson(SurfaceSnapshot.From(surface))),
            Zone zone => ("zone", ToJson(ZoneSnapshot.From(zone))),
            SourceSystem source => ("source-system", ToJson(SourceSystemSnapshot.From(source))),
            SupplySystem supply => ("supply-system", ToJson(SupplySystemSnapshot.From(supply))),
            VentilationAssignment assignment => ("ventilation-assignment", ToJson(VentilationAssignmentSnapshot.From(assignment))),
            VentilationSystem ventilator => ("energy-recovery-ventilator", ToJson(VentilationSystemSnapshot.From(ventilator))),
            PhotovoltaicSystem panel => ("photovoltaic-panel", ToJson(PhotovoltaicSnapshot.From(panel))),
            GreenRetrofitModel model => ("green-retrofit-model", ToJson(ModelSnapshot.From(model))),
            GreenRetrofitResult result => ("green-retrofit-result", GrrWriter.Serialize(result, writeIndented: false)),
            _ => throw new NotSupportedException(
                "Grasshopper persistence is not implemented for '" + value.GetType().FullName + "'."),
        };

        return JsonSerializer.Serialize(
            new Envelope { Schema = Schema, Kind = kind, Payload = payload },
            JsonOptions);
    }

    internal static T Deserialize<T>(string snapshot)
        where T : class
    {
        Envelope envelope = JsonSerializer.Deserialize<Envelope>(snapshot, JsonOptions)
            ?? throw new InvalidDataException("The SimpleDragon Grasshopper snapshot is empty.");
        if (!string.Equals(envelope.Schema, Schema, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported SimpleDragon Grasshopper schema '" + envelope.Schema + "'.");
        }

        object value = envelope.Kind switch
        {
            "diagnostic" => FromJson<Diagnostic>(envelope.Payload),
            "batch-case" => FromJson<BatchCaseSnapshot>(envelope.Payload).ToDomain(),
            "opening-definition" => FromJson<OpeningDefinitionSnapshot>(envelope.Payload).ToDomain(),
            "surface-definition" => FromJson<SurfaceDefinitionSnapshot>(envelope.Payload).ToDomain(),
            "zone-definition" => FromJson<ZoneDefinitionSnapshot>(envelope.Payload).ToDomain(),
            "material" => FromJson<MaterialSnapshot>(envelope.Payload).ToDomain(),
            "surface-construction-layer" => FromJson<SurfaceLayerSnapshot>(envelope.Payload).ToDomain(),
            "surface-construction" => FromJson<SurfaceConstructionSnapshot>(envelope.Payload).ToDomain(),
            "fenestration-construction" => FromJson<FenestrationConstructionSnapshot>(envelope.Payload).ToDomain(),
            "usage-profile" => FromJson<UsageProfileSnapshot>(envelope.Payload).ToDomain(),
            "surface" => FromJson<SurfaceSnapshot>(envelope.Payload).ToDomain(),
            "zone" => FromJson<ZoneSnapshot>(envelope.Payload).ToDomain(),
            "source-system" => FromJson<SourceSystemSnapshot>(envelope.Payload).ToDomain(),
            "supply-system" => FromJson<SupplySystemSnapshot>(envelope.Payload).ToDomain(),
            "ventilation-assignment" => FromJson<VentilationAssignmentSnapshot>(envelope.Payload).ToDomain(),
            "energy-recovery-ventilator" => FromJson<VentilationSystemSnapshot>(envelope.Payload).ToDomain(),
            "photovoltaic-panel" => FromJson<PhotovoltaicSnapshot>(envelope.Payload).ToDomain(),
            "green-retrofit-model" => FromJson<ModelSnapshot>(envelope.Payload).ToDomain(),
            "green-retrofit-result" => GrrReader.Read(envelope.Payload).RequireResult(),
            _ => throw new InvalidDataException(
                "Unsupported SimpleDragon Grasshopper value kind '" + envelope.Kind + "'."),
        };

        return value as T
            ?? throw new InvalidDataException(
                "The snapshot contains '" + value.GetType().FullName + "', not '" + typeof(T).FullName + "'.");
    }

    private static string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static T FromJson<T>(string json)
        where T : class
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException("The " + typeof(T).Name + " snapshot is empty.");
    }

    private sealed class Envelope
    {
        public string Schema { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }

    private sealed class MaterialSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public double Conductivity { get; set; }

        public double Density { get; set; }

        public double SpecificHeat { get; set; }

        public static MaterialSnapshot From(Material value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Conductivity = value.Conductivity,
            Density = value.Density,
            SpecificHeat = value.SpecificHeat,
        };

        public Material ToDomain() => new(
            Name,
            Conductivity,
            Density,
            SpecificHeat,
            new EntityId(Id));
    }

    private sealed class SurfaceLayerSnapshot
    {
        public MaterialSnapshot Material { get; set; } = new();

        public double Thickness { get; set; }

        public static SurfaceLayerSnapshot From(SurfaceConstructionLayer value) => new()
        {
            Material = MaterialSnapshot.From(value.Material),
            Thickness = value.Thickness,
        };

        public SurfaceConstructionLayer ToDomain() => new(Material.ToDomain(), Thickness);
    }

    private sealed class SurfaceConstructionSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<SurfaceLayerSnapshot> Layers { get; set; } = new();

        public static SurfaceConstructionSnapshot From(SurfaceConstruction value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Layers = value.Layers.Select(SurfaceLayerSnapshot.From).ToList(),
        };

        public SurfaceConstruction ToDomain() => new(
            Name,
            Layers.Select(item => item.ToDomain()),
            new EntityId(Id));
    }

    private sealed class FenestrationConstructionSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public double UValue { get; set; }

        public double? SolarHeatGainCoefficient { get; set; }

        public static FenestrationConstructionSnapshot From(FenestrationConstruction value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            UValue = value.UValue,
            SolarHeatGainCoefficient = value.SolarHeatGainCoefficient,
        };

        public FenestrationConstruction ToDomain() => new(
            Name,
            UValue,
            SolarHeatGainCoefficient,
            new EntityId(Id));
    }

    private sealed class VacationSnapshot
    {
        public int StartMonth { get; set; }

        public int StartDay { get; set; }

        public int EndMonth { get; set; }

        public int EndDay { get; set; }

        public static VacationSnapshot From(VacationPeriod value) => new()
        {
            StartMonth = value.Start.Month,
            StartDay = value.Start.Day,
            EndMonth = value.End.Month,
            EndDay = value.End.Day,
        };

        public VacationPeriod ToDomain() => new(
            new MonthDay(StartMonth, StartDay),
            new MonthDay(EndMonth, EndDay));
    }

    private sealed class UsageProfileSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public UsageProfileSource Source { get; set; }

        public int OccupantStart { get; set; }

        public int OccupantEnd { get; set; }

        public int HvacStart { get; set; }

        public int HvacEnd { get; set; }

        public double Ventilation { get; set; }

        public double DomesticHotWater { get; set; }

        public double LightingHours { get; set; }

        public double Occupancy { get; set; }

        public double Equipment { get; set; }

        public double HeatingSetpoint { get; set; }

        public double CoolingSetpoint { get; set; }

        public List<bool> Operation { get; set; } = new();

        public List<VacationSnapshot> Vacations { get; set; } = new();

        public static UsageProfileSnapshot? FromOptional(UsageProfile? value) =>
            value is null ? null : From(value);

        public static UsageProfileSnapshot From(UsageProfile value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Source = value.Source,
            OccupantStart = value.OccupantStart,
            OccupantEnd = value.OccupantEnd,
            HvacStart = value.HvacStart,
            HvacEnd = value.HvacEnd,
            Ventilation = value.Ventilation,
            DomesticHotWater = value.DomesticHotWater,
            LightingHours = value.LightingHours,
            Occupancy = value.Occupancy,
            Equipment = value.Equipment,
            HeatingSetpoint = value.HeatingSetpoint,
            CoolingSetpoint = value.CoolingSetpoint,
            Operation = UsageDays.Select(value.OperatesOn).ToList(),
            Vacations = value.Vacations.Select(VacationSnapshot.From).ToList(),
        };

        public UsageProfile ToDomain()
        {
            if (Operation.Count != UsageDays.Length)
            {
                throw new InvalidDataException("A usage-profile snapshot must contain every operation day.");
            }

            return new UsageProfile(
                Name,
                OccupantStart,
                OccupantEnd,
                HvacStart,
                HvacEnd,
                Ventilation,
                DomesticHotWater,
                LightingHours,
                Occupancy,
                Equipment,
                HeatingSetpoint,
                CoolingSetpoint,
                UsageDays.Select((day, index) => new KeyValuePair<UsageDay, bool>(day, Operation[index]))
                    .ToDictionary(item => item.Key, item => item.Value),
                Vacations.Select(item => item.ToDomain()),
                Source,
                new EntityId(Id));
        }
    }

    private sealed class FenestrationSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public FenestrationType Type { get; set; }

        public double Area { get; set; }

        public string ConstructionId { get; set; } = string.Empty;

        public FenestrationConstructionSnapshot? Construction { get; set; }

        public BlindType? Blind { get; set; }

        public static FenestrationSnapshot From(Fenestration value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Type = value.Type,
            Area = value.Area,
            ConstructionId = value.ConstructionId,
            Construction = value.Construction is null
                ? null
                : FenestrationConstructionSnapshot.From(value.Construction),
            Blind = value.Blind,
        };

        public Fenestration ToDomain() => new(
            Name,
            Type,
            Area,
            ConstructionId,
            Construction?.ToDomain(),
            Blind,
            new EntityId(Id));
    }

    private sealed class SurfaceSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public SurfaceType Type { get; set; }

        public SurfaceBoundaryCondition BoundaryCondition { get; set; }

        public double Area { get; set; }

        public double? Azimuth { get; set; }

        public string? ConstructionId { get; set; }

        public SurfaceConstructionSnapshot? Construction { get; set; }

        public List<FenestrationSnapshot> Fenestrations { get; set; } = new();

        public double? CoolRoofReflectance { get; set; }

        public string? AdjacentZoneId { get; set; }

        public static SurfaceSnapshot From(Surface value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Type = value.Type,
            BoundaryCondition = value.BoundaryCondition,
            Area = value.Area,
            Azimuth = value.Azimuth,
            ConstructionId = value.ConstructionId,
            Construction = value.Construction is null
                ? null
                : SurfaceConstructionSnapshot.From(value.Construction),
            Fenestrations = value.Fenestrations.Select(FenestrationSnapshot.From).ToList(),
            CoolRoofReflectance = value.CoolRoofReflectance,
            AdjacentZoneId = value.AdjacentZoneId,
        };

        public Surface ToDomain() => new(
            Name,
            Type,
            BoundaryCondition,
            Area,
            Azimuth,
            ConstructionId,
            Construction?.ToDomain(),
            Fenestrations.Select(item => item.ToDomain()),
            CoolRoofReflectance,
            AdjacentZoneId,
            new EntityId(Id));
    }

    private sealed class SourceSystemSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public SourceSystemType Type { get; set; }

        public FuelType? FuelType { get; set; }

        public double? HeatingCop { get; set; }

        public double? CoolingCop { get; set; }

        public double? HeatingCapacity { get; set; }

        public double? CoolingCapacity { get; set; }

        public double? Efficiency { get; set; }

        public bool? HotWaterSupply { get; set; }

        public CompressorType? CompressorType { get; set; }

        public CoolingTowerType? CoolingTowerType { get; set; }

        public double? CoolingTowerCapacity { get; set; }

        public CoolingTowerControl? CoolingTowerControl { get; set; }

        public double? BoilerEfficiency { get; set; }

        public List<string>? GrmFields { get; set; }

        public static SourceSystemSnapshot From(SourceSystem value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Type = value.Type,
            FuelType = value.FuelType,
            HeatingCop = value.HeatingCop,
            CoolingCop = value.CoolingCop,
            HeatingCapacity = value.HeatingCapacity,
            CoolingCapacity = value.CoolingCapacity,
            Efficiency = value.Efficiency,
            HotWaterSupply = value.HotWaterSupply,
            CompressorType = value.CompressorType,
            CoolingTowerType = value.CoolingTowerType,
            CoolingTowerCapacity = value.CoolingTowerCapacity,
            CoolingTowerControl = value.CoolingTowerControl,
            BoilerEfficiency = value.BoilerEfficiency,
            GrmFields = value.GrmFields.OrderBy(field => field, StringComparer.Ordinal).ToList(),
        };

        public SourceSystem ToDomain() => new(
            Name,
            Type,
            FuelType,
            HeatingCop,
            CoolingCop,
            HeatingCapacity,
            CoolingCapacity,
            Efficiency,
            HotWaterSupply,
            CompressorType,
            CoolingTowerType,
            CoolingTowerCapacity,
            CoolingTowerControl,
            BoilerEfficiency,
            new EntityId(Id),
            GrmFields);
    }

    private sealed class SupplySystemSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public SupplySystemType Type { get; set; }

        public string? SourceSystemId { get; set; }

        public SourceSystemSnapshot? SourceSystem { get; set; }

        public double? CoolingCop { get; set; }

        public double? CoolingCapacity { get; set; }

        public double? HeatingCapacity { get; set; }

        public List<string>? GrmFields { get; set; }

        public static SupplySystemSnapshot From(SupplySystem value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Type = value.Type,
            SourceSystemId = value.SourceSystemId,
            SourceSystem = value.SourceSystem is null ? null : SourceSystemSnapshot.From(value.SourceSystem),
            CoolingCop = value.CoolingCop,
            CoolingCapacity = value.CoolingCapacity,
            HeatingCapacity = value.HeatingCapacity,
            GrmFields = value.GrmFields.OrderBy(field => field, StringComparer.Ordinal).ToList(),
        };

        public SupplySystem ToDomain() => new(
            Name,
            Type,
            SourceSystemId,
            SourceSystem?.ToDomain(),
            CoolingCop,
            CoolingCapacity,
            HeatingCapacity,
            new EntityId(Id),
            GrmFields);
    }

    private sealed class SupplyAssignmentSnapshot
    {
        public string SupplySystemId { get; set; } = string.Empty;

        public SupplySystemSnapshot? SupplySystem { get; set; }

        public static SupplyAssignmentSnapshot From(SupplySystemAssignment value) => new()
        {
            SupplySystemId = value.SupplySystemId,
            SupplySystem = value.SupplySystem is null ? null : SupplySystemSnapshot.From(value.SupplySystem),
        };

        public SupplySystemAssignment ToDomain() => new(SupplySystemId, SupplySystem?.ToDomain());
    }

    private sealed class VentilationSystemSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public double AirflowRate { get; set; }

        public double HeatingEfficiency { get; set; }

        public double CoolingEfficiency { get; set; }

        public static VentilationSystemSnapshot From(VentilationSystem value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            AirflowRate = value.AirflowRate,
            HeatingEfficiency = value.HeatingEfficiency,
            CoolingEfficiency = value.CoolingEfficiency,
        };

        public VentilationSystem ToDomain() => new(
            Name,
            AirflowRate,
            HeatingEfficiency,
            CoolingEfficiency,
            new EntityId(Id));
    }

    private sealed class VentilationAssignmentSnapshot
    {
        public string VentilationSystemId { get; set; } = string.Empty;

        public int Count { get; set; }

        public VentilationSystemSnapshot? VentilationSystem { get; set; }

        public static VentilationAssignmentSnapshot From(VentilationAssignment value) => new()
        {
            VentilationSystemId = value.VentilationSystemId,
            Count = value.Count,
            VentilationSystem = value.VentilationSystem is null
                ? null
                : VentilationSystemSnapshot.From(value.VentilationSystem),
        };

        public VentilationAssignment ToDomain() => new(
            VentilationSystemId,
            Count,
            VentilationSystem?.ToDomain());
    }

    private sealed class OpeningDefinitionSnapshot
    {
        public string GeometryArchive { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public FenestrationType Type { get; set; }

        public FenestrationConstructionSnapshot Construction { get; set; } = new();

        public BlindType? Blind { get; set; }

        public string? Id { get; set; }

        public static OpeningDefinitionSnapshot From(OpeningDefinition value) => new()
        {
            GeometryArchive = Convert.ToBase64String(value.GeometryArchive),
            Name = value.Name,
            Type = value.Type,
            Construction = FenestrationConstructionSnapshot.From(value.Construction),
            Blind = value.Blind,
            Id = value.Id?.Value,
        };

        public OpeningDefinition ToDomain()
        {
            using Curve geometry = RhinoGeometryArchive.Decode<Curve>(
                Convert.FromBase64String(GeometryArchive));
            return new OpeningDefinition(
                geometry,
                Name,
                Type,
                Construction.ToDomain(),
                Blind,
                Id is null ? null : new EntityId(Id));
        }
    }

    private sealed class SurfaceDefinitionSnapshot
    {
        public string GeometryArchive { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public SurfaceType Type { get; set; }

        public SurfaceBoundaryCondition BoundaryCondition { get; set; }

        public SurfaceConstructionSnapshot? Construction { get; set; }

        public List<OpeningDefinitionSnapshot> Openings { get; set; } = new();

        public double? CoolRoofReflectance { get; set; }

        public string? Id { get; set; }

        public static SurfaceDefinitionSnapshot From(SurfaceDefinition value) => new()
        {
            GeometryArchive = Convert.ToBase64String(value.GeometryArchive),
            Name = value.Name,
            Type = value.Type,
            BoundaryCondition = value.BoundaryCondition,
            Construction = value.Construction is null
                ? null
                : SurfaceConstructionSnapshot.From(value.Construction),
            Openings = value.Openings.Select(OpeningDefinitionSnapshot.From).ToList(),
            CoolRoofReflectance = value.CoolRoofReflectance,
            Id = value.Id?.Value,
        };

        public SurfaceDefinition ToDomain()
        {
            using Brep geometry = RhinoGeometryArchive.Decode<Brep>(
                Convert.FromBase64String(GeometryArchive));
            return new SurfaceDefinition(
                geometry,
                Name,
                Type,
                BoundaryCondition,
                Construction?.ToDomain(),
                Openings.Select(item => item.ToDomain()),
                CoolRoofReflectance,
                Id is null ? null : new EntityId(Id));
        }
    }

    private sealed class ZoneDefinitionSnapshot
    {
        public string Name { get; set; } = string.Empty;

        public int FloorNumber { get; set; }

        public double Height { get; set; }

        public List<SurfaceDefinitionSnapshot> Surfaces { get; set; } = new();

        public UsageProfileSnapshot Profile { get; set; } = new();

        public double? LightDensity { get; set; }

        public List<SupplySystemSnapshot> SupplySystems { get; set; } = new();

        public List<VentilationAssignmentSnapshot> VentilationAssignments { get; set; } = new();

        public string? Id { get; set; }

        public static ZoneDefinitionSnapshot From(ZoneDefinition value) => new()
        {
            Name = value.Name,
            FloorNumber = value.FloorNumber,
            Height = value.Height,
            Surfaces = value.Surfaces.Select(SurfaceDefinitionSnapshot.From).ToList(),
            Profile = UsageProfileSnapshot.From(value.Profile),
            LightDensity = value.LightDensity,
            SupplySystems = value.SupplySystems.Select(SupplySystemSnapshot.From).ToList(),
            VentilationAssignments = value.VentilationAssignments
                .Select(VentilationAssignmentSnapshot.From)
                .ToList(),
            Id = value.Id?.Value,
        };

        public ZoneDefinition ToDomain() => new(
            Name,
            FloorNumber,
            Height,
            Surfaces.Select(item => item.ToDomain()),
            Profile.ToDomain(),
            LightDensity,
            SupplySystems.Select(item => item.ToDomain()),
            VentilationAssignments.Select(item => item.ToDomain()),
            Id is null ? null : new EntityId(Id));
    }

    private sealed class ZoneSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int FloorNumber { get; set; }

        public double Height { get; set; }

        public List<SurfaceSnapshot> Surfaces { get; set; } = new();

        public string ProfileName { get; set; } = string.Empty;

        public UsageProfileSnapshot? Profile { get; set; }

        public double? LightDensity { get; set; }

        public List<SupplyAssignmentSnapshot> SupplySystems { get; set; } = new();

        public List<VentilationAssignmentSnapshot> VentilationSystems { get; set; } = new();

        public static ZoneSnapshot From(Zone value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            FloorNumber = value.FloorNumber,
            Height = value.Height,
            Surfaces = value.Surfaces.Select(SurfaceSnapshot.From).ToList(),
            ProfileName = value.ProfileName,
            Profile = UsageProfileSnapshot.FromOptional(value.Profile),
            LightDensity = value.LightDensity,
            SupplySystems = value.SupplySystemAssignments.Select(SupplyAssignmentSnapshot.From).ToList(),
            VentilationSystems = value.VentilationAssignments.Select(VentilationAssignmentSnapshot.From).ToList(),
        };

        public Zone ToDomain() => new(
            Name,
            FloorNumber,
            Height,
            Surfaces.Select(item => item.ToDomain()),
            ProfileName,
            Profile?.ToDomain(),
            LightDensity,
            SupplySystems.Select(item => item.ToDomain()),
            VentilationSystems.Select(item => item.ToDomain()),
            new EntityId(Id));
    }

    private sealed class FloorSnapshot
    {
        public int FloorNumber { get; set; }

        public List<ZoneSnapshot> Zones { get; set; } = new();

        public static FloorSnapshot From(BuildingFloor value) => new()
        {
            FloorNumber = value.FloorNumber,
            Zones = value.Zones.Select(ZoneSnapshot.From).ToList(),
        };

        public BuildingFloor ToDomain() => new(FloorNumber, Zones.Select(item => item.ToDomain()));
    }

    private sealed class PhotovoltaicSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public double Area { get; set; }

        public double Efficiency { get; set; }

        public double Azimuth { get; set; }

        public double Tilt { get; set; }

        public static PhotovoltaicSnapshot From(PhotovoltaicSystem value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Area = value.Area,
            Efficiency = value.Efficiency,
            Azimuth = value.Azimuth,
            Tilt = value.Tilt,
        };

        public PhotovoltaicSystem ToDomain() => new(
            Name,
            Area,
            Efficiency,
            Azimuth,
            Tilt,
            new EntityId(Id));
    }

    private sealed class WeatherMetadataSnapshot
    {
        public string AdministrativeArea { get; set; } = string.Empty;

        public string LegalDistrictCode { get; set; } = string.Empty;

        public string Terrain { get; set; } = string.Empty;

        public double AdministrativeLatitude { get; set; }

        public double AdministrativeLongitude { get; set; }

        public string WeatherLocation { get; set; } = string.Empty;

        public string WeatherLocationType { get; set; } = string.Empty;

        public double WeatherLatitude { get; set; }

        public double WeatherLongitude { get; set; }

        public string EpwFileName { get; set; } = string.Empty;

        public static WeatherMetadataSnapshot From(WeatherMetadata value) => new()
        {
            AdministrativeArea = value.AdministrativeArea,
            LegalDistrictCode = value.LegalDistrictCode,
            Terrain = value.Terrain,
            AdministrativeLatitude = value.AdministrativeLatitude,
            AdministrativeLongitude = value.AdministrativeLongitude,
            WeatherLocation = value.WeatherLocation,
            WeatherLocationType = value.WeatherLocationType,
            WeatherLatitude = value.WeatherLatitude,
            WeatherLongitude = value.WeatherLongitude,
            EpwFileName = value.EpwFileName,
        };

        public WeatherMetadata ToDomain() => new(
            AdministrativeArea,
            LegalDistrictCode,
            Terrain,
            AdministrativeLatitude,
            AdministrativeLongitude,
            WeatherLocation,
            WeatherLocationType,
            WeatherLatitude,
            WeatherLongitude,
            EpwFileName);
    }

    private sealed class WeatherSelectionSnapshot
    {
        public WeatherMetadataSnapshot Metadata { get; set; } = new();

        public string ClimateRegion { get; set; } = string.Empty;

        public DateTime ClimateEffectiveDate { get; set; }

        public static WeatherSelectionSnapshot From(WeatherSelection value) => new()
        {
            Metadata = WeatherMetadataSnapshot.From(value.Metadata),
            ClimateRegion = value.ClimateRegion,
            ClimateEffectiveDate = value.ClimateEffectiveDate,
        };

        public WeatherSelection ToDomain() => new(
            Metadata.ToDomain(),
            ClimateRegion,
            ClimateEffectiveDate);
    }

    private sealed class BatchCaseSnapshot
    {
        public ModelSnapshot Model { get; set; } = new();

        public string? CaseId { get; set; }

        public static BatchCaseSnapshot From(SimpleDragonBatchCase value) => new()
        {
            Model = ModelSnapshot.From(value.Model),
            CaseId = value.CaseId,
        };

        public SimpleDragonBatchCase ToDomain() => new(Model.ToDomain(), CaseId);
    }

    private sealed class ModelSnapshot
    {
        public string Name { get; set; } = string.Empty;

        public double NorthAxis { get; set; }

        public string Address { get; set; } = string.Empty;

        public DateTime Vintage { get; set; }

        public bool IsMultifamilyHousing { get; set; }

        public List<FloorSnapshot> Floors { get; set; } = new();

        public List<MaterialSnapshot> Materials { get; set; } = new();

        public List<SurfaceConstructionSnapshot> SurfaceConstructions { get; set; } = new();

        public List<FenestrationConstructionSnapshot> FenestrationConstructions { get; set; } = new();

        public List<SourceSystemSnapshot> SourceSystems { get; set; } = new();

        public List<SupplySystemSnapshot> SupplySystems { get; set; } = new();

        public List<VentilationSystemSnapshot> VentilationSystems { get; set; } = new();

        public List<PhotovoltaicSnapshot> PhotovoltaicSystems { get; set; } = new();

        public bool HadWeather { get; set; }

        public WeatherSelectionSnapshot? Weather { get; set; }

        public static ModelSnapshot From(GreenRetrofitModel value) => new()
        {
            Name = value.Name,
            NorthAxis = value.NorthAxis,
            Address = value.Address,
            Vintage = value.Vintage,
            IsMultifamilyHousing = value.IsMultifamilyHousing,
            Floors = value.Floors.Select(FloorSnapshot.From).ToList(),
            Materials = value.Materials.Select(MaterialSnapshot.From).ToList(),
            SurfaceConstructions = value.SurfaceConstructions.Select(SurfaceConstructionSnapshot.From).ToList(),
            FenestrationConstructions = value.FenestrationConstructions
                .Select(FenestrationConstructionSnapshot.From)
                .ToList(),
            SourceSystems = value.SourceSystems.Select(SourceSystemSnapshot.From).ToList(),
            SupplySystems = value.SupplySystems.Select(SupplySystemSnapshot.From).ToList(),
            VentilationSystems = value.VentilationSystems.Select(VentilationSystemSnapshot.From).ToList(),
            PhotovoltaicSystems = value.PhotovoltaicSystems.Select(PhotovoltaicSnapshot.From).ToList(),
            HadWeather = value.Weather is not null,
            Weather = value.Weather is null ? null : WeatherSelectionSnapshot.From(value.Weather),
        };

        public GreenRetrofitModel ToDomain()
        {
            WeatherSelection? weather = Weather?.ToDomain();
            if (weather is null && HadWeather)
            {
                // Backward compatibility for snapshots written before weather metadata was embedded.
                weather = SimpleDragonDatabase.Default.Weather.FindByAddress(Address, Vintage).Require();
            }

            return new GreenRetrofitModel(
                Name,
                NorthAxis,
                Address,
                Vintage,
                IsMultifamilyHousing,
                Floors.Select(item => item.ToDomain()),
                Materials.Select(item => item.ToDomain()),
                SurfaceConstructions.Select(item => item.ToDomain()),
                FenestrationConstructions.Select(item => item.ToDomain()),
                SourceSystems.Select(item => item.ToDomain()),
                SupplySystems.Select(item => item.ToDomain()),
                VentilationSystems.Select(item => item.ToDomain()),
                PhotovoltaicSystems.Select(item => item.ToDomain()),
                weather);
        }
    }
}
