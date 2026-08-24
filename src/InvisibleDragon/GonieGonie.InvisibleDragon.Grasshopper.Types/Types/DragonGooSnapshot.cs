using System.Globalization;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.InvisibleDragon.Shape;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Grasshopper.Types;

internal static class DragonGooSnapshot
{
    private const string Schema = "goniegonie.invisible-dragon.grasshopper-goo.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    internal static string Serialize<T>(T value)
        where T : class
    {
        (string kind, string json) = value switch
        {
            Material material => ("material", ToJson(MaterialSnapshot.From(material))),
            ISurfaceConstruction construction => ("construction", ToJson(ConstructionSnapshot.From(construction))),
            Schedule schedule => ("schedule", ToJson(ScheduleSnapshot.From(schedule))),
            ZoneProfile profile => ("profile", ToJson(ProfileSnapshot.From(profile))),
            Surface surface => ("surface", ToJson(SurfaceSnapshot.From(surface))),
            Zone zone => ("zone", ToJson(ZoneSnapshot.From(zone))),
            SourceSystem source => ("source-system", ToJson(SourceGraphSnapshot.From(source))),
            SupplySystem supply => ("supply-system", ToJson(SupplyGraphSnapshot.From(supply))),
            EnergyRecoveryVentilator ventilator =>
                ("energy-recovery-ventilator", ToJson(EnergyRecoveryVentilatorSnapshot.From(ventilator))),
            PhotovoltaicPanel panel => ("photovoltaic-panel", ToJson(PhotovoltaicPanelSnapshot.From(panel))),
            EnergyModel model => ("energy-model", ToJson(ModelSnapshot.From(model))),
            IdfDocument idf => ("idf", IdfWriter.Write(idf, new IdfWriterOptions { IncludeSchemaFieldComments = false })),
            EnergyPlusSimulationResult result => ("energyplus-result", EnergyPlusResultJson.Serialize(result)),
            Diagnostic diagnostic => ("diagnostic", JsonSerializer.Serialize(diagnostic, BuildingEnergyJson.CreateOptions())),
            _ => throw new NotSupportedException($"Grasshopper persistence is not implemented for '{value.GetType().FullName}'."),
        };

        return JsonSerializer.Serialize(
            new Envelope { Schema = Schema, Kind = kind, Payload = json },
            JsonOptions);
    }

    internal static T Deserialize<T>(string snapshot)
        where T : class
    {
        Envelope envelope = JsonSerializer.Deserialize<Envelope>(snapshot, JsonOptions)
            ?? throw new InvalidDataException("The Grasshopper value snapshot is empty.");
        if (!string.Equals(envelope.Schema, Schema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported Grasshopper value schema '{envelope.Schema}'.");
        }

        object value = envelope.Kind switch
        {
            "material" => FromJson<MaterialSnapshot>(envelope.Payload).ToDomain(),
            "construction" => FromJson<ConstructionSnapshot>(envelope.Payload).ToDomain(),
            "schedule" => FromJson<ScheduleSnapshot>(envelope.Payload).ToDomain(),
            "profile" => FromJson<ProfileSnapshot>(envelope.Payload).ToDomain(),
            "surface" => FromJson<SurfaceSnapshot>(envelope.Payload).ToDomain(),
            "zone" => FromJson<ZoneSnapshot>(envelope.Payload).ToDomain(),
            "source-system" => FromJson<SourceGraphSnapshot>(envelope.Payload).ToDomain(),
            "supply-system" => FromJson<SupplyGraphSnapshot>(envelope.Payload).ToDomain(),
            "energy-recovery-ventilator" =>
                FromJson<EnergyRecoveryVentilatorSnapshot>(envelope.Payload).ToDomain(),
            "photovoltaic-panel" => FromJson<PhotovoltaicPanelSnapshot>(envelope.Payload).ToDomain(),
            "energy-model" => FromJson<ModelSnapshot>(envelope.Payload).ToDomain(),
            "idf" => IdfParser.Parse(envelope.Payload),
            "energyplus-result" => EnergyPlusResultJson.Deserialize(envelope.Payload),
            "diagnostic" => JsonSerializer.Deserialize<Diagnostic>(envelope.Payload, BuildingEnergyJson.CreateOptions())
                ?? throw new InvalidDataException("The diagnostic snapshot is empty."),
            _ => throw new InvalidDataException($"Unsupported Grasshopper value kind '{envelope.Kind}'."),
        };

        return value as T
            ?? throw new InvalidDataException(
                $"The snapshot contains '{value.GetType().FullName}', not '{typeof(T).FullName}'.");
    }

    private static string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static T FromJson<T>(string json)
        where T : class
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException($"The {typeof(T).Name} snapshot is empty.");
    }

    private sealed class Envelope
    {
        public string Schema { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }

    private sealed class MaterialSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public double Conductivity { get; set; }
        public double Density { get; set; }
        public double SpecificHeat { get; set; }
        public double ThermalAbsorptance { get; set; }
        public double SolarAbsorptance { get; set; }
        public double VisibleAbsorptance { get; set; }
        public MaterialRoughness Roughness { get; set; }

        public static MaterialSnapshot From(Material value) => new()
        {
            Name = value.Name,
            Conductivity = value.ConductivityWattsPerMetreKelvin,
            Density = value.DensityKilogramsPerCubicMetre,
            SpecificHeat = value.SpecificHeatJoulesPerKilogramKelvin,
            ThermalAbsorptance = value.ThermalAbsorptance,
            SolarAbsorptance = value.SolarAbsorptance,
            VisibleAbsorptance = value.VisibleAbsorptance,
            Roughness = value.Roughness,
        };

        public Material ToDomain() => new(
            Name,
            Conductivity,
            Density,
            SpecificHeat,
            ThermalAbsorptance,
            SolarAbsorptance,
            VisibleAbsorptance,
            Roughness);
    }

    private sealed class LayerSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public MaterialSnapshot Material { get; set; } = new();
        public double Thickness { get; set; }

        public static LayerSnapshot From(Layer value) => new()
        {
            Name = value.Name,
            Material = MaterialSnapshot.From(value.Material),
            Thickness = value.ThicknessMetres,
        };

        public Layer ToDomain() => new(Name, Material.ToDomain(), Thickness);
    }

    private sealed class ConstructionSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public List<LayerSnapshot> Layers { get; set; } = new();

        public static ConstructionSnapshot From(ISurfaceConstruction value)
        {
            return value switch
            {
                OpaqueConstruction construction => new ConstructionSnapshot
                {
                    Kind = "layers",
                    Name = construction.Name,
                    Layers = construction.Layers.Select(LayerSnapshot.From).ToList(),
                },
                NoMassConstruction noMass => new ConstructionSnapshot
                {
                    Kind = "no-mass",
                    Name = noMass.Name,
                    Value = noMass.UValueWattsPerSquareMetreKelvin,
                },
                AirBoundary airBoundary => new ConstructionSnapshot
                {
                    Kind = "air-boundary",
                    Name = airBoundary.Name,
                    Value = airBoundary.AirChangesPerHour,
                },
                _ => throw new NotSupportedException($"Unknown construction type '{value.GetType().FullName}'."),
            };
        }

        public ISurfaceConstruction ToDomain()
        {
            return Kind switch
            {
                "layers" => new OpaqueConstruction(Name, Layers.Select(item => item.ToDomain())),
                "no-mass" => new NoMassConstruction(Name, Value),
                "air-boundary" => new AirBoundary(Name, Value),
                _ => throw new InvalidDataException($"Unknown construction snapshot kind '{Kind}'."),
            };
        }
    }

    private sealed class DayScheduleSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public ScheduleType Type { get; set; }
        public string? Unit { get; set; }
        public List<double> Values { get; set; } = new();

        public static DayScheduleSnapshot? FromOptional(DaySchedule? value) => value is null ? null : From(value);

        public static DayScheduleSnapshot From(DaySchedule value) => new()
        {
            Name = value.Name,
            Type = value.Type,
            Unit = value.Unit,
            Values = value.Values.ToList(),
        };

        public DaySchedule ToDomain() => new(Name, Values, Type, Unit);

        public static DaySchedule? ToOptional(DayScheduleSnapshot? value) => value?.ToDomain();
    }

    private sealed class RuleSetSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public ScheduleType Type { get; set; }
        public DayScheduleSnapshot Weekdays { get; set; } = new();
        public DayScheduleSnapshot Weekends { get; set; } = new();
        public DayScheduleSnapshot? Monday { get; set; }
        public DayScheduleSnapshot? Tuesday { get; set; }
        public DayScheduleSnapshot? Wednesday { get; set; }
        public DayScheduleSnapshot? Thursday { get; set; }
        public DayScheduleSnapshot? Friday { get; set; }
        public DayScheduleSnapshot? Saturday { get; set; }
        public DayScheduleSnapshot? Sunday { get; set; }
        public DayScheduleSnapshot? Holiday { get; set; }

        public static RuleSetSnapshot From(RuleSet value) => new()
        {
            Name = value.Name,
            Type = value.Type,
            Weekdays = DayScheduleSnapshot.From(value.Weekdays),
            Weekends = DayScheduleSnapshot.From(value.Weekends),
            Monday = DayScheduleSnapshot.FromOptional(value.Monday),
            Tuesday = DayScheduleSnapshot.FromOptional(value.Tuesday),
            Wednesday = DayScheduleSnapshot.FromOptional(value.Wednesday),
            Thursday = DayScheduleSnapshot.FromOptional(value.Thursday),
            Friday = DayScheduleSnapshot.FromOptional(value.Friday),
            Saturday = DayScheduleSnapshot.FromOptional(value.Saturday),
            Sunday = DayScheduleSnapshot.FromOptional(value.Sunday),
            Holiday = DayScheduleSnapshot.FromOptional(value.Holiday),
        };

        public RuleSet ToDomain() => new(
            Name,
            Weekdays.ToDomain(),
            Weekends.ToDomain(),
            DayScheduleSnapshot.ToOptional(Monday),
            DayScheduleSnapshot.ToOptional(Tuesday),
            DayScheduleSnapshot.ToOptional(Wednesday),
            DayScheduleSnapshot.ToOptional(Thursday),
            DayScheduleSnapshot.ToOptional(Friday),
            DayScheduleSnapshot.ToOptional(Saturday),
            DayScheduleSnapshot.ToOptional(Sunday),
            DayScheduleSnapshot.ToOptional(Holiday),
            Type);
    }

    private sealed class SchedulePeriodSnapshot
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public RuleSetSnapshot RuleSet { get; set; } = new();

        public static SchedulePeriodSnapshot From(SchedulePeriod value) => new()
        {
            Start = value.Start.ToString("MM-dd", CultureInfo.InvariantCulture),
            End = value.End.ToString("MM-dd", CultureInfo.InvariantCulture),
            RuleSet = RuleSetSnapshot.From(value.RuleSet),
        };

        public SchedulePeriod ToDomain() => new(
            DateTime.ParseExact($"{Schedule.DefaultYear}-{Start}", "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime.ParseExact($"{Schedule.DefaultYear}-{End}", "yyyy-MM-dd", CultureInfo.InvariantCulture),
            RuleSet.ToDomain());
    }

    private sealed class ScheduleSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public List<SchedulePeriodSnapshot> Periods { get; set; } = new();

        public static ScheduleSnapshot? FromOptional(Schedule? value) => value is null ? null : From(value);

        public static ScheduleSnapshot From(Schedule value) => new()
        {
            Name = value.Name,
            Periods = value.Compactize().Select(SchedulePeriodSnapshot.From).ToList(),
        };

        public Schedule ToDomain() => Schedule.FromCompact(Name, Periods.Select(item => item.ToDomain()));

        public static Schedule? ToOptional(ScheduleSnapshot? value) => value?.ToDomain();
    }

    private sealed class ProfileSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ScheduleSnapshot? Heating { get; set; }
        public ScheduleSnapshot? Cooling { get; set; }
        public ScheduleSnapshot? Availability { get; set; }
        public ScheduleSnapshot? Occupant { get; set; }
        public ScheduleSnapshot? Lighting { get; set; }
        public ScheduleSnapshot? Equipment { get; set; }
        public ScheduleSnapshot? HotWater { get; set; }

        public static ProfileSnapshot From(ZoneProfile value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Heating = ScheduleSnapshot.FromOptional(value.HeatingSetpoint),
            Cooling = ScheduleSnapshot.FromOptional(value.CoolingSetpoint),
            Availability = ScheduleSnapshot.FromOptional(value.HvacAvailability),
            Occupant = ScheduleSnapshot.FromOptional(value.Occupant),
            Lighting = ScheduleSnapshot.FromOptional(value.Lighting),
            Equipment = ScheduleSnapshot.FromOptional(value.Equipment),
            HotWater = ScheduleSnapshot.FromOptional(value.HotWater),
        };

        public ZoneProfile ToDomain() => new(
            new EntityId(Id),
            Name,
            ScheduleSnapshot.ToOptional(Heating),
            ScheduleSnapshot.ToOptional(Cooling),
            ScheduleSnapshot.ToOptional(Availability),
            ScheduleSnapshot.ToOptional(Occupant),
            ScheduleSnapshot.ToOptional(Lighting),
            ScheduleSnapshot.ToOptional(Equipment),
            ScheduleSnapshot.ToOptional(HotWater));
    }

    private sealed class VertexSnapshot
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public static VertexSnapshot From(Vertex value) => new() { X = value.X, Y = value.Y, Z = value.Z };

        public Vertex ToDomain() => new(X, Y, Z);
    }

    private sealed class PolygonSnapshot
    {
        public List<VertexSnapshot> Vertices { get; set; } = new();

        public static PolygonSnapshot From(PlanarPolygon value) => new()
        {
            Vertices = value.Vertices.Select(VertexSnapshot.From).ToList(),
        };

        public PlanarPolygon ToDomain() => new(Vertices.Select(item => item.ToDomain()));
    }

    private sealed class GlazingSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public double UValue { get; set; }
        public double SolarHeatGainCoefficient { get; set; }

        public static GlazingSnapshot From(Glazing value) => new()
        {
            Name = value.Name,
            UValue = value.UValueWattsPerSquareMetreKelvin,
            SolarHeatGainCoefficient = value.SolarHeatGainCoefficient,
        };

        public Glazing ToDomain() => new(Name, UValue, SolarHeatGainCoefficient);
    }

    private sealed class ShadingSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double First { get; set; }
        public double Second { get; set; }
        public double Third { get; set; }
        public double Fourth { get; set; }
        public double Fifth { get; set; }

        public static ShadingSnapshot? FromOptional(IShadingDevice? value)
        {
            return value switch
            {
                null => null,
                Shade shade => new ShadingSnapshot
                {
                    Kind = "shade",
                    Name = shade.Name,
                    First = shade.Transmittance,
                    Second = shade.Reflectance,
                },
                Blind blind => new ShadingSnapshot
                {
                    Kind = "blind",
                    Name = blind.Name,
                    First = blind.SlatWidthMetres,
                    Second = blind.SlatSeparationMetres,
                    Third = blind.SlatAngleDegrees,
                    Fourth = blind.FrontReflectance,
                    Fifth = blind.BackReflectance,
                },
                _ => throw new NotSupportedException($"Unknown shading device '{value.GetType().FullName}'."),
            };
        }

        public IShadingDevice ToDomain()
        {
            return Kind switch
            {
                "shade" => new Shade(Name, First, Second),
                "blind" => new Blind(Name, First, Second, Third, Fourth, Fifth),
                _ => throw new InvalidDataException($"Unknown shading snapshot kind '{Kind}'."),
            };
        }
    }

    private sealed class OpeningSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public PolygonSnapshot Polygon { get; set; } = new();
        public GlazingSnapshot? Glazing { get; set; }
        public ConstructionSnapshot? Construction { get; set; }
        public ShadingSnapshot? Shading { get; set; }
        public GeometryProvenance? Provenance { get; set; }

        public static OpeningSnapshot From(IOpening value)
        {
            return value switch
            {
                Window window => new OpeningSnapshot
                {
                    Kind = "window",
                    Id = window.Id.Value,
                    Name = window.Name,
                    Polygon = PolygonSnapshot.From(window.Polygon),
                    Glazing = GlazingSnapshot.From(window.Glazing),
                    Shading = ShadingSnapshot.FromOptional(window.Shading),
                    Provenance = window.Provenance,
                },
                Door door => new OpeningSnapshot
                {
                    Kind = "door",
                    Id = door.Id.Value,
                    Name = door.Name,
                    Polygon = PolygonSnapshot.From(door.Polygon),
                    Construction = ConstructionSnapshot.From(door.Construction),
                    Provenance = door.Provenance,
                },
                _ => throw new NotSupportedException($"Unknown opening type '{value.GetType().FullName}'."),
            };
        }

        public IOpening ToDomain()
        {
            return Kind switch
            {
                "window" => new Window(
                    new EntityId(Id),
                    Name,
                    Required(Glazing, nameof(Glazing)).ToDomain(),
                    Polygon.ToDomain(),
                    Shading?.ToDomain(),
                    Provenance),
                "door" => new Door(
                    new EntityId(Id),
                    Name,
                    Required(Construction, nameof(Construction)).ToDomain(),
                    Polygon.ToDomain(),
                    Provenance),
                _ => throw new InvalidDataException($"Unknown opening snapshot kind '{Kind}'."),
            };
        }
    }

    private sealed class SurfaceSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public SurfaceType Type { get; set; }
        public ConstructionSnapshot Construction { get; set; } = new();
        public SurfaceBoundaryCondition Boundary { get; set; }
        public string? AdjacentSurfaceId { get; set; }
        public PolygonSnapshot Polygon { get; set; } = new();
        public List<OpeningSnapshot> Openings { get; set; } = new();
        public GeometryProvenance? Provenance { get; set; }

        public static SurfaceSnapshot From(Surface value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Type = value.Type,
            Construction = ConstructionSnapshot.From(value.Construction),
            Boundary = value.Boundary.Condition,
            AdjacentSurfaceId = value.Boundary.AdjacentSurfaceId?.Value,
            Polygon = PolygonSnapshot.From(value.Polygon),
            Openings = value.Openings.Select(OpeningSnapshot.From).ToList(),
            Provenance = value.Provenance,
        };

        public Surface ToDomain() => new(
            new EntityId(Id),
            Name,
            Type,
            Construction.ToDomain(),
            Boundary == SurfaceBoundaryCondition.Zone
                ? SurfaceBoundary.AdjacentTo(new EntityId(AdjacentSurfaceId ?? throw new InvalidDataException("A zone boundary has no adjacent surface identifier.")))
                : new SurfaceBoundary(Boundary),
            Polygon.ToDomain(),
            Openings.Select(item => item.ToDomain()),
            Provenance);
    }

    private sealed class ZoneSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<SurfaceSnapshot> Surfaces { get; set; } = new();
        public ProfileSnapshot Profile { get; set; } = new();
        public double Infiltration { get; set; }
        public double LightingPowerDensity { get; set; }
        public double OutdoorAirFlow { get; set; }

        public static ZoneSnapshot From(Zone value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            Surfaces = value.Surfaces.Select(SurfaceSnapshot.From).ToList(),
            Profile = ProfileSnapshot.From(value.Profile),
            Infiltration = value.InfiltrationAirChangesPerHour,
            LightingPowerDensity = value.LightingPowerDensityWattsPerSquareMetre,
            OutdoorAirFlow = value.OutdoorAirFlowCubicMetresPerSecond,
        };

        public Zone ToDomain() => new(
            new EntityId(Id),
            Name,
            Surfaces.Select(item => item.ToDomain()),
            Profile.ToDomain(),
            Infiltration,
            LightingPowerDensity,
            OutdoorAirFlow);
    }

    private sealed class CoolingTowerSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double? NominalCapacityWatts { get; set; }
        public double PumpMotorEfficiency { get; set; }

        public static CoolingTowerSnapshot From(CoolingTower value)
        {
            string kind = value switch
            {
                OpenSingleSpeedCoolingTower => "open-single-speed",
                OpenTwoSpeedCoolingTower => "open-two-speed",
                ClosedSingleSpeedCoolingTower => "closed-single-speed",
                ClosedTwoSpeedCoolingTower => "closed-two-speed",
                _ => throw new NotSupportedException(
                    $"Grasshopper persistence does not support cooling-tower type '{value.GetType().FullName}'. " +
                    "Supported types are open/closed single-speed and open/closed two-speed towers."),
            };

            return new CoolingTowerSnapshot
            {
                Kind = kind,
                Id = value.Id.Value,
                Name = value.Name,
                NominalCapacityWatts = value.NominalCapacityWatts,
                PumpMotorEfficiency = value.PumpMotorEfficiency,
            };
        }

        public CoolingTower ToDomain()
        {
            EntityId id = new(Id);
            return Kind switch
            {
                "open-single-speed" => new OpenSingleSpeedCoolingTower(
                    id,
                    Name,
                    NominalCapacityWatts,
                    PumpMotorEfficiency),
                "open-two-speed" => new OpenTwoSpeedCoolingTower(
                    id,
                    Name,
                    NominalCapacityWatts,
                    PumpMotorEfficiency),
                "closed-single-speed" => new ClosedSingleSpeedCoolingTower(
                    id,
                    Name,
                    NominalCapacityWatts,
                    PumpMotorEfficiency),
                "closed-two-speed" => new ClosedTwoSpeedCoolingTower(
                    id,
                    Name,
                    NominalCapacityWatts,
                    PumpMotorEfficiency),
                _ => throw new InvalidDataException(
                    $"Unknown cooling-tower snapshot kind '{Kind}'. " +
                    "Expected open-single-speed, open-two-speed, closed-single-speed, or closed-two-speed."),
            };
        }
    }

    private sealed class SourceSystemSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Fuel? Fuel { get; set; }
        public double? HeatingCoefficientOfPerformance { get; set; }
        public double? CoolingCoefficientOfPerformance { get; set; }
        public double? HeatingCapacityWatts { get; set; }
        public double? CoolingCapacityWatts { get; set; }
        public double? NominalThermalEfficiency { get; set; }
        public double? NominalCapacityWatts { get; set; }
        public double? PumpMotorEfficiency { get; set; }
        public double? SetpointTemperatureCelsius { get; set; }
        public double? ReferenceCoefficientOfPerformance { get; set; }
        public CompressorType? Compressor { get; set; }
        public double? ThermalCoefficientOfPerformance { get; set; }
        public CoolingTowerSnapshot? CoolingTower { get; set; }
        public string? HeatSourceId { get; set; }

        public static SourceSystemSnapshot From(SourceSystem value)
        {
            return value switch
            {
                GeothermalHeatPump heatPump => HeatPumpSnapshot("geothermal-heat-pump", heatPump),
                HeatPump heatPump when heatPump.GetType() == typeof(HeatPump) =>
                    HeatPumpSnapshot("heat-pump", heatPump),
                Boiler boiler => new SourceSystemSnapshot
                {
                    Kind = "boiler",
                    Id = boiler.Id.Value,
                    Name = boiler.Name,
                    Fuel = boiler.Fuel,
                    NominalThermalEfficiency = boiler.NominalThermalEfficiency,
                    NominalCapacityWatts = boiler.NominalCapacityWatts,
                    PumpMotorEfficiency = boiler.PumpMotorEfficiency,
                    SetpointTemperatureCelsius = boiler.SetpointTemperatureCelsius,
                },
                DistrictHeating district => new SourceSystemSnapshot
                {
                    Kind = "district-heating",
                    Id = district.Id.Value,
                    Name = district.Name,
                    NominalCapacityWatts = district.NominalCapacityWatts,
                    PumpMotorEfficiency = district.PumpMotorEfficiency,
                    SetpointTemperatureCelsius = district.SetpointTemperatureCelsius,
                },
                Chiller chiller => new SourceSystemSnapshot
                {
                    Kind = "chiller",
                    Id = chiller.Id.Value,
                    Name = chiller.Name,
                    ReferenceCoefficientOfPerformance = chiller.ReferenceCoefficientOfPerformance,
                    Compressor = chiller.Compressor,
                    CoolingTower = CoolingTowerSnapshot.From(chiller.CoolingTower),
                    NominalCapacityWatts = chiller.NominalCapacityWatts,
                    PumpMotorEfficiency = chiller.PumpMotorEfficiency,
                    SetpointTemperatureCelsius = chiller.SetpointTemperatureCelsius,
                },
                AbsorptionChiller chiller => new SourceSystemSnapshot
                {
                    Kind = "absorption-chiller",
                    Id = chiller.Id.Value,
                    Name = chiller.Name,
                    ThermalCoefficientOfPerformance = chiller.ThermalCoefficientOfPerformance,
                    HeatSourceId = chiller.HeatSource.Id.Value,
                    CoolingTower = CoolingTowerSnapshot.From(chiller.CoolingTower),
                    NominalCapacityWatts = chiller.NominalCapacityWatts,
                    PumpMotorEfficiency = chiller.PumpMotorEfficiency,
                    SetpointTemperatureCelsius = chiller.SetpointTemperatureCelsius,
                },
                _ => throw new NotSupportedException(
                    $"Grasshopper persistence does not support source-system type '{value.GetType().FullName}'. " +
                    "Supported types are HeatPump, GeothermalHeatPump, Boiler, DistrictHeating, Chiller, and AbsorptionChiller."),
            };
        }

        public SourceSystem ToDomain(SourceSystemResolver resolver)
        {
            EntityId id = new(Id);
            return Kind switch
            {
                "heat-pump" => new HeatPump(
                    id,
                    Name,
                    RequiredValue(Fuel, nameof(Fuel)),
                    RequiredValue(HeatingCoefficientOfPerformance, nameof(HeatingCoefficientOfPerformance)),
                    RequiredValue(CoolingCoefficientOfPerformance, nameof(CoolingCoefficientOfPerformance)),
                    HeatingCapacityWatts,
                    CoolingCapacityWatts),
                "geothermal-heat-pump" => new GeothermalHeatPump(
                    id,
                    Name,
                    RequiredValue(Fuel, nameof(Fuel)),
                    RequiredValue(HeatingCoefficientOfPerformance, nameof(HeatingCoefficientOfPerformance)),
                    RequiredValue(CoolingCoefficientOfPerformance, nameof(CoolingCoefficientOfPerformance)),
                    HeatingCapacityWatts,
                    CoolingCapacityWatts),
                "boiler" => new Boiler(
                    id,
                    Name,
                    RequiredValue(Fuel, nameof(Fuel)),
                    RequiredValue(NominalThermalEfficiency, nameof(NominalThermalEfficiency)),
                    NominalCapacityWatts,
                    RequiredValue(PumpMotorEfficiency, nameof(PumpMotorEfficiency)),
                    RequiredValue(SetpointTemperatureCelsius, nameof(SetpointTemperatureCelsius))),
                "district-heating" => new DistrictHeating(
                    id,
                    Name,
                    NominalCapacityWatts,
                    RequiredValue(PumpMotorEfficiency, nameof(PumpMotorEfficiency)),
                    RequiredValue(SetpointTemperatureCelsius, nameof(SetpointTemperatureCelsius))),
                "chiller" => new Chiller(
                    id,
                    Name,
                    RequiredValue(
                        ReferenceCoefficientOfPerformance,
                        nameof(ReferenceCoefficientOfPerformance)),
                    RequiredValue(Compressor, nameof(Compressor)),
                    Required(CoolingTower, nameof(CoolingTower)).ToDomain(),
                    NominalCapacityWatts,
                    RequiredValue(PumpMotorEfficiency, nameof(PumpMotorEfficiency)),
                    RequiredValue(SetpointTemperatureCelsius, nameof(SetpointTemperatureCelsius))),
                "absorption-chiller" => new AbsorptionChiller(
                    id,
                    Name,
                    RequiredValue(ThermalCoefficientOfPerformance, nameof(ThermalCoefficientOfPerformance)),
                    RequireBoiler(resolver.Resolve(RequiredText(HeatSourceId, nameof(HeatSourceId)))),
                    Required(CoolingTower, nameof(CoolingTower)).ToDomain(),
                    NominalCapacityWatts,
                    RequiredValue(PumpMotorEfficiency, nameof(PumpMotorEfficiency)),
                    RequiredValue(SetpointTemperatureCelsius, nameof(SetpointTemperatureCelsius))),
                _ => throw new InvalidDataException(
                    $"Unknown source-system snapshot kind '{Kind}'. " +
                    "Expected heat-pump, geothermal-heat-pump, boiler, district-heating, chiller, or absorption-chiller."),
            };
        }

        private static SourceSystemSnapshot HeatPumpSnapshot(string kind, HeatPump value) => new()
        {
            Kind = kind,
            Id = value.Id.Value,
            Name = value.Name,
            Fuel = value.Fuel,
            HeatingCoefficientOfPerformance = value.HeatingCoefficientOfPerformance,
            CoolingCoefficientOfPerformance = value.CoolingCoefficientOfPerformance,
            HeatingCapacityWatts = value.HeatingCapacityWatts,
            CoolingCapacityWatts = value.CoolingCapacityWatts,
        };

        private static Boiler RequireBoiler(SourceSystem source)
        {
            return source as Boiler
                ?? throw new InvalidDataException(
                    $"An absorption-chiller heatSourceId must reference a Boiler, but '{source.Id}' is a {source.GetType().Name}.");
        }
    }

    private sealed class SourceSystemGraphBuilder
    {
        private readonly Dictionary<string, SourceSystemSnapshot> _sources =
            new(StringComparer.Ordinal);

        public void Add(SourceSystem source)
        {
            SourceSystemSnapshot snapshot = SourceSystemSnapshot.From(source);
            if (_sources.TryGetValue(snapshot.Id, out SourceSystemSnapshot? existing))
            {
                if (!string.Equals(ToJson(existing), ToJson(snapshot), StringComparison.Ordinal))
                {
                    throw new NotSupportedException(
                        $"Source-system identifier '{snapshot.Id}' is associated with conflicting definitions; " +
                        "Grasshopper persistence requires one deterministic definition per source ID.");
                }

                return;
            }

            _sources.Add(snapshot.Id, snapshot);
            if (source is AbsorptionChiller absorptionChiller)
            {
                Add(absorptionChiller.HeatSource);
            }
        }

        public List<SourceSystemSnapshot> Build() => _sources.Values
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class SourceSystemResolver
    {
        private readonly Dictionary<string, SourceSystemSnapshot> _snapshots;
        private readonly Dictionary<string, SourceSystem> _resolved = new(StringComparer.Ordinal);
        private readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

        public SourceSystemResolver(IEnumerable<SourceSystemSnapshot>? snapshots)
        {
            if (snapshots is null)
            {
                throw new InvalidDataException("The source-system definitions collection is required.");
            }

            _snapshots = new Dictionary<string, SourceSystemSnapshot>(StringComparer.Ordinal);
            foreach (SourceSystemSnapshot snapshot in snapshots)
            {
                if (snapshot is null)
                {
                    throw new InvalidDataException("The source-system definitions collection contains a null entry.");
                }

                string id = RequiredText(snapshot.Id, "sourceSystem.id");
                if (_snapshots.ContainsKey(id))
                {
                    throw new InvalidDataException(
                        $"The source-system snapshot contains duplicate identifier '{id}'.");
                }

                _snapshots.Add(id, snapshot);
            }
        }

        public SourceSystem Resolve(string id)
        {
            id = RequiredText(id, "sourceSystemId");
            if (_resolved.TryGetValue(id, out SourceSystem? source))
            {
                return source;
            }

            if (!_snapshots.TryGetValue(id, out SourceSystemSnapshot? snapshot))
            {
                throw new InvalidDataException(
                    $"The snapshot references missing source-system identifier '{id}'.");
            }

            if (!_resolving.Add(id))
            {
                throw new InvalidDataException(
                    $"The source-system snapshot contains a dependency cycle involving '{id}'.");
            }

            try
            {
                source = snapshot.ToDomain(this);
                _resolved.Add(id, source);
                return source;
            }
            finally
            {
                _resolving.Remove(id);
            }
        }
    }

    private sealed class SourceGraphSnapshot
    {
        public string RootId { get; set; } = string.Empty;
        public List<SourceSystemSnapshot> Sources { get; set; } = new();

        public static SourceGraphSnapshot From(SourceSystem value)
        {
            var builder = new SourceSystemGraphBuilder();
            builder.Add(value);
            return new SourceGraphSnapshot
            {
                RootId = value.Id.Value,
                Sources = builder.Build(),
            };
        }

        public SourceSystem ToDomain() => new SourceSystemResolver(Sources).Resolve(RootId);
    }

    private sealed class SupplySystemSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? SourceId { get; set; }
        public double? FanTotalEfficiency { get; set; }
        public double? FanPressureRisePascals { get; set; }
        public double? MotorEfficiency { get; set; }
        public double? ThrottlingRangeCelsius { get; set; }
        public double? HeatingCapacityWatts { get; set; }
        public double? Efficiency { get; set; }
        public double? RadiantFraction { get; set; }

        public static SupplySystemSnapshot From(SupplySystem value)
        {
            return value switch
            {
                VariableRefrigerantFlowTerminal terminal => AirSupplySnapshot(
                    "variable-refrigerant-flow-terminal",
                    terminal),
                PackagedAirConditioner packaged => AirSupplySnapshot("packaged-air-conditioner", packaged),
                AirHandlingUnit airHandlingUnit when airHandlingUnit.GetType() == typeof(AirHandlingUnit) =>
                    AirSupplySnapshot("air-handling-unit", airHandlingUnit),
                FanCoilUnit fanCoil => new SupplySystemSnapshot
                {
                    Kind = "fan-coil-unit",
                    Id = fanCoil.Id.Value,
                    Name = fanCoil.Name,
                    SourceId = fanCoil.Source!.Id.Value,
                    FanTotalEfficiency = fanCoil.FanTotalEfficiency,
                    FanPressureRisePascals = fanCoil.FanPressureRisePascals,
                    MotorEfficiency = fanCoil.MotorEfficiency,
                },
                RadiantFloor radiantFloor => new SupplySystemSnapshot
                {
                    Kind = "radiant-floor",
                    Id = radiantFloor.Id.Value,
                    Name = radiantFloor.Name,
                    SourceId = radiantFloor.Source!.Id.Value,
                    ThrottlingRangeCelsius = radiantFloor.ThrottlingRangeCelsius,
                },
                ElectricRadiantFloor radiantFloor => new SupplySystemSnapshot
                {
                    Kind = "electric-radiant-floor",
                    Id = radiantFloor.Id.Value,
                    Name = radiantFloor.Name,
                    ThrottlingRangeCelsius = radiantFloor.ThrottlingRangeCelsius,
                },
                Radiator radiator => new SupplySystemSnapshot
                {
                    Kind = "radiator",
                    Id = radiator.Id.Value,
                    Name = radiator.Name,
                    SourceId = radiator.Source!.Id.Value,
                    HeatingCapacityWatts = radiator.HeatingCapacityWatts,
                    RadiantFraction = radiator.RadiantFraction,
                },
                ElectricRadiator radiator => new SupplySystemSnapshot
                {
                    Kind = "electric-radiator",
                    Id = radiator.Id.Value,
                    Name = radiator.Name,
                    HeatingCapacityWatts = radiator.HeatingCapacityWatts,
                    Efficiency = radiator.Efficiency,
                    RadiantFraction = radiator.RadiantFraction,
                },
                _ => throw new NotSupportedException(
                    $"Grasshopper persistence does not support supply-system type '{value.GetType().FullName}'. " +
                    "Supported types are AirHandlingUnit, VariableRefrigerantFlowTerminal, PackagedAirConditioner, " +
                    "FanCoilUnit, RadiantFloor, ElectricRadiantFloor, Radiator, and ElectricRadiator."),
            };
        }

        public SupplySystem ToDomain(SourceSystemResolver resolver)
        {
            EntityId id = new(Id);
            return Kind switch
            {
                "air-handling-unit" => new AirHandlingUnit(
                    id,
                    Name,
                    RequiredSource<HeatPump>(resolver),
                    RequiredValue(FanTotalEfficiency, nameof(FanTotalEfficiency)),
                    RequiredValue(FanPressureRisePascals, nameof(FanPressureRisePascals)),
                    RequiredValue(MotorEfficiency, nameof(MotorEfficiency))),
                "variable-refrigerant-flow-terminal" =>
                    CreateVariableRefrigerantFlowTerminal(id, resolver),
                "packaged-air-conditioner" => CreatePackagedAirConditioner(id, resolver),
                "fan-coil-unit" => new FanCoilUnit(
                    id,
                    Name,
                    RequiredSource<SourceSystem>(resolver),
                    RequiredValue(FanTotalEfficiency, nameof(FanTotalEfficiency)),
                    RequiredValue(FanPressureRisePascals, nameof(FanPressureRisePascals)),
                    RequiredValue(MotorEfficiency, nameof(MotorEfficiency))),
                "radiant-floor" => new RadiantFloor(
                    id,
                    Name,
                    RequiredSource<SourceSystem>(resolver),
                    RequiredValue(ThrottlingRangeCelsius, nameof(ThrottlingRangeCelsius))),
                "electric-radiant-floor" => CreateElectricRadiantFloor(id),
                "radiator" => new Radiator(
                    id,
                    Name,
                    RequiredSource<SourceSystem>(resolver),
                    HeatingCapacityWatts,
                    RequiredValue(RadiantFraction, nameof(RadiantFraction))),
                "electric-radiator" => CreateElectricRadiator(id),
                _ => throw new InvalidDataException(
                    $"Unknown supply-system snapshot kind '{Kind}'. " +
                    "Expected air-handling-unit, variable-refrigerant-flow-terminal, packaged-air-conditioner, " +
                    "fan-coil-unit, radiant-floor, electric-radiant-floor, radiator, or electric-radiator."),
            };
        }

        private static SupplySystemSnapshot AirSupplySnapshot(string kind, AirHandlingUnit value) => new()
        {
            Kind = kind,
            Id = value.Id.Value,
            Name = value.Name,
            SourceId = value.Source!.Id.Value,
            FanTotalEfficiency = value.FanTotalEfficiency,
            FanPressureRisePascals = value.FanPressureRisePascals,
            MotorEfficiency = value.MotorEfficiency,
        };

        private TSource RequiredSource<TSource>(SourceSystemResolver resolver)
            where TSource : SourceSystem
        {
            SourceSystem source = resolver.Resolve(RequiredText(SourceId, nameof(SourceId)));
            return source as TSource
                ?? throw new InvalidDataException(
                    $"Supply-system kind '{Kind}' requires a {typeof(TSource).Name} source, " +
                    $"but source '{source.Id}' is a {source.GetType().Name}.");
        }

        private VariableRefrigerantFlowTerminal CreateVariableRefrigerantFlowTerminal(
            EntityId id,
            SourceSystemResolver resolver)
        {
            ValidateDefaultAirProperties("variable-refrigerant-flow-terminal");
            return new VariableRefrigerantFlowTerminal(id, Name, RequiredSource<HeatPump>(resolver));
        }

        private PackagedAirConditioner CreatePackagedAirConditioner(
            EntityId id,
            SourceSystemResolver resolver)
        {
            ValidateDefaultAirProperties("packaged-air-conditioner");
            return new PackagedAirConditioner(id, Name, RequiredSource<HeatPump>(resolver));
        }

        private ElectricRadiantFloor CreateElectricRadiantFloor(EntityId id)
        {
            RequireNoSource();
            return new ElectricRadiantFloor(
                id,
                Name,
                RequiredValue(ThrottlingRangeCelsius, nameof(ThrottlingRangeCelsius)));
        }

        private ElectricRadiator CreateElectricRadiator(EntityId id)
        {
            RequireNoSource();
            return new ElectricRadiator(
                id,
                Name,
                HeatingCapacityWatts,
                RequiredValue(Efficiency, nameof(Efficiency)),
                RequiredValue(RadiantFraction, nameof(RadiantFraction)));
        }

        private void RequireNoSource()
        {
            if (!string.IsNullOrWhiteSpace(SourceId))
            {
                throw new InvalidDataException(
                    $"Supply-system kind '{Kind}' cannot reference source-system identifier '{SourceId}'.");
            }
        }

        private void ValidateDefaultAirProperties(string kind)
        {
            if (RequiredValue(FanTotalEfficiency, nameof(FanTotalEfficiency)) != 0.7 ||
                RequiredValue(FanPressureRisePascals, nameof(FanPressureRisePascals)) != 100 ||
                RequiredValue(MotorEfficiency, nameof(MotorEfficiency)) != 0.9)
            {
                throw new InvalidDataException(
                    $"The current {kind} API exposes fixed fan properties (0.7, 100 Pa, 0.9); " +
                    "the snapshot contains values that cannot be restored losslessly.");
            }
        }
    }

    private sealed class SupplyGraphSnapshot
    {
        public SupplySystemSnapshot Supply { get; set; } = new();
        public List<SourceSystemSnapshot> Sources { get; set; } = new();

        public static SupplyGraphSnapshot From(SupplySystem value)
        {
            var builder = new SourceSystemGraphBuilder();
            if (value.Source is not null)
            {
                builder.Add(value.Source);
            }

            return new SupplyGraphSnapshot
            {
                Supply = SupplySystemSnapshot.From(value),
                Sources = builder.Build(),
            };
        }

        public SupplySystem ToDomain() =>
            Required(Supply, nameof(Supply)).ToDomain(new SourceSystemResolver(Sources));
    }

    private sealed class EnergyRecoveryVentilatorSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double SensibleEffectiveness { get; set; }
        public double LatentEffectiveness { get; set; }
        public double? SupplyAirFlowCubicMetresPerSecond { get; set; }
        public double FanTotalEfficiency { get; set; }
        public double FanPressureRisePascals { get; set; }

        public static EnergyRecoveryVentilatorSnapshot From(EnergyRecoveryVentilator value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            SensibleEffectiveness = value.SensibleEffectiveness,
            LatentEffectiveness = value.LatentEffectiveness,
            SupplyAirFlowCubicMetresPerSecond = value.SupplyAirFlowCubicMetresPerSecond,
            FanTotalEfficiency = value.FanTotalEfficiency,
            FanPressureRisePascals = value.FanPressureRisePascals,
        };

        public EnergyRecoveryVentilator ToDomain() => new(
            new EntityId(Id),
            Name,
            SensibleEffectiveness,
            LatentEffectiveness,
            SupplyAirFlowCubicMetresPerSecond,
            FanTotalEfficiency,
            FanPressureRisePascals);
    }

    private sealed class EnergyRecoveryVentilatorGraphBuilder
    {
        private readonly Dictionary<string, EnergyRecoveryVentilatorSnapshot> _ventilators =
            new(StringComparer.Ordinal);

        public void Add(EnergyRecoveryVentilator ventilator)
        {
            EnergyRecoveryVentilatorSnapshot snapshot = EnergyRecoveryVentilatorSnapshot.From(ventilator);
            if (_ventilators.TryGetValue(snapshot.Id, out EnergyRecoveryVentilatorSnapshot? existing))
            {
                if (!string.Equals(ToJson(existing), ToJson(snapshot), StringComparison.Ordinal))
                {
                    throw new NotSupportedException(
                        $"Ventilator identifier '{snapshot.Id}' is associated with conflicting definitions; " +
                        "Grasshopper persistence requires one deterministic definition per ventilator ID.");
                }

                return;
            }

            _ventilators.Add(snapshot.Id, snapshot);
        }

        public List<EnergyRecoveryVentilatorSnapshot> Build() => _ventilators.Values
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class EnergyRecoveryVentilatorResolver
    {
        private readonly Dictionary<string, EnergyRecoveryVentilatorSnapshot> _snapshots;
        private readonly Dictionary<string, EnergyRecoveryVentilator> _resolved = new(StringComparer.Ordinal);

        public EnergyRecoveryVentilatorResolver(IEnumerable<EnergyRecoveryVentilatorSnapshot>? snapshots)
        {
            if (snapshots is null)
            {
                throw new InvalidDataException("The ventilator definitions collection is required.");
            }

            _snapshots = new Dictionary<string, EnergyRecoveryVentilatorSnapshot>(StringComparer.Ordinal);
            foreach (EnergyRecoveryVentilatorSnapshot snapshot in snapshots)
            {
                if (snapshot is null)
                {
                    throw new InvalidDataException("The ventilator definitions collection contains a null entry.");
                }

                string id = RequiredText(snapshot.Id, "ventilator.id");
                if (_snapshots.ContainsKey(id))
                {
                    throw new InvalidDataException(
                        $"The ventilator snapshot contains duplicate identifier '{id}'.");
                }

                _snapshots.Add(id, snapshot);
            }
        }

        public EnergyRecoveryVentilator Resolve(string id)
        {
            id = RequiredText(id, "ventilatorId");
            if (_resolved.TryGetValue(id, out EnergyRecoveryVentilator? ventilator))
            {
                return ventilator;
            }

            if (!_snapshots.TryGetValue(id, out EnergyRecoveryVentilatorSnapshot? snapshot))
            {
                throw new InvalidDataException(
                    $"The snapshot references missing ventilator identifier '{id}'.");
            }

            ventilator = snapshot.ToDomain();
            _resolved.Add(id, ventilator);
            return ventilator;
        }
    }

    private sealed class PhotovoltaicPanelSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double AreaSquareMetres { get; set; }
        public double TiltDegrees { get; set; }
        public double AzimuthDegrees { get; set; }
        public double Efficiency { get; set; }
        public double ActiveCellAreaFraction { get; set; }

        public static PhotovoltaicPanelSnapshot From(PhotovoltaicPanel value) => new()
        {
            Id = value.Id.Value,
            Name = value.Name,
            AreaSquareMetres = value.AreaSquareMetres,
            TiltDegrees = value.TiltDegrees,
            AzimuthDegrees = value.AzimuthDegrees,
            Efficiency = value.Efficiency,
            ActiveCellAreaFraction = value.ActiveCellAreaFraction,
        };

        public PhotovoltaicPanel ToDomain() => new(
            new EntityId(Id),
            Name,
            AreaSquareMetres,
            TiltDegrees,
            AzimuthDegrees,
            Efficiency,
            ActiveCellAreaFraction);
    }

    private sealed class ZoneHvacAssignmentSnapshot
    {
        public string ZoneId { get; set; } = string.Empty;
        public List<SupplySystemSnapshot> Systems { get; set; } = new();
        public List<ScheduleSnapshot?> Availabilities { get; set; } = new();

        public static ZoneHvacAssignmentSnapshot From(
            ZoneHvacAssignment value,
            SourceSystemGraphBuilder sourceBuilder)
        {
            foreach (SupplySystem system in value.Supply.Systems)
            {
                if (system.Source is not null)
                {
                    sourceBuilder.Add(system.Source);
                }
            }

            return new ZoneHvacAssignmentSnapshot
            {
                ZoneId = value.ZoneId.Value,
                Systems = value.Supply.Systems.Select(SupplySystemSnapshot.From).ToList(),
                Availabilities = value.Supply.Availabilities
                    .Select(ScheduleSnapshot.FromOptional)
                    .ToList(),
            };
        }

        public ZoneHvacAssignment ToDomain(SourceSystemResolver resolver)
        {
            List<SupplySystemSnapshot> systems = Required(Systems, nameof(Systems));
            List<ScheduleSnapshot?> availabilities = Required(Availabilities, nameof(Availabilities));
            if (systems.Count != availabilities.Count)
            {
                throw new InvalidDataException(
                    $"HVAC assignment for zone '{ZoneId}' has {systems.Count} systems but " +
                    $"{availabilities.Count} availability entries.");
            }

            return new ZoneHvacAssignment(
                new EntityId(ZoneId),
                new SupplyGroup(
                    systems.Select((item, index) =>
                        Required(item, $"{nameof(Systems)}[{index}]").ToDomain(resolver)),
                    availabilities.Select(ScheduleSnapshot.ToOptional)));
        }
    }

    private sealed class ZoneVentilationAssignmentSnapshot
    {
        public string ZoneId { get; set; } = string.Empty;
        public string VentilatorId { get; set; } = string.Empty;

        public static ZoneVentilationAssignmentSnapshot From(
            ZoneVentilationAssignment value,
            EnergyRecoveryVentilatorGraphBuilder ventilatorBuilder)
        {
            ventilatorBuilder.Add(value.Ventilator);
            return new ZoneVentilationAssignmentSnapshot
            {
                ZoneId = value.ZoneId.Value,
                VentilatorId = value.Ventilator.Id.Value,
            };
        }

        public ZoneVentilationAssignment ToDomain(EnergyRecoveryVentilatorResolver resolver) => new(
            new EntityId(ZoneId),
            resolver.Resolve(VentilatorId));
    }

    private sealed class ModelSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public List<ZoneSnapshot> Zones { get; set; } = new();
        public List<SourceSystemSnapshot> Sources { get; set; } = new();
        public List<ZoneHvacAssignmentSnapshot> HvacAssignments { get; set; } = new();
        public List<EnergyRecoveryVentilatorSnapshot> Ventilators { get; set; } = new();
        public List<ZoneVentilationAssignmentSnapshot> VentilationAssignments { get; set; } = new();
        public List<PhotovoltaicPanelSnapshot> PhotovoltaicPanels { get; set; } = new();
        public double NorthAxis { get; set; }
        public Terrain Terrain { get; set; }
        public List<string> SummaryReports { get; set; } = new();
        public bool IncludeElectricityBalanceMonthly { get; set; }

        public static ModelSnapshot From(EnergyModel value)
        {
            var sourceBuilder = new SourceSystemGraphBuilder();
            List<ZoneHvacAssignmentSnapshot> hvacAssignments = value.HvacAssignments
                .Select(item => ZoneHvacAssignmentSnapshot.From(item, sourceBuilder))
                .ToList();
            var ventilatorBuilder = new EnergyRecoveryVentilatorGraphBuilder();
            List<ZoneVentilationAssignmentSnapshot> ventilationAssignments = value.VentilationAssignments
                .Select(item => ZoneVentilationAssignmentSnapshot.From(item, ventilatorBuilder))
                .ToList();

            return new ModelSnapshot
            {
                Name = value.Name,
                Zones = value.Zones.Select(ZoneSnapshot.From).ToList(),
                Sources = sourceBuilder.Build(),
                HvacAssignments = hvacAssignments,
                Ventilators = ventilatorBuilder.Build(),
                VentilationAssignments = ventilationAssignments,
                PhotovoltaicPanels = value.PhotovoltaicPanels.Select(PhotovoltaicPanelSnapshot.From).ToList(),
                NorthAxis = value.NorthAxisDegrees,
                Terrain = value.Terrain,
                SummaryReports = value.OutputTables.SummaryReports.ToList(),
                IncludeElectricityBalanceMonthly = value.OutputTables.IncludeElectricityBalanceMonthly,
            };
        }

        public EnergyModel ToDomain()
        {
            var sourceResolver = new SourceSystemResolver(Sources);
            var ventilatorResolver = new EnergyRecoveryVentilatorResolver(Ventilators);
            List<ZoneSnapshot> zones = Required(Zones, nameof(Zones));
            List<ZoneHvacAssignmentSnapshot> hvacAssignments = Required(
                HvacAssignments,
                nameof(HvacAssignments));
            List<ZoneVentilationAssignmentSnapshot> ventilationAssignments = Required(
                VentilationAssignments,
                nameof(VentilationAssignments));
            List<PhotovoltaicPanelSnapshot> photovoltaicPanels = Required(
                PhotovoltaicPanels,
                nameof(PhotovoltaicPanels));
            List<string> summaryReports = Required(SummaryReports, nameof(SummaryReports));
            return new EnergyModel(
                Name,
                zones.Select((item, index) => Required(item, $"{nameof(Zones)}[{index}]").ToDomain()),
                hvacAssignments.Select((item, index) =>
                    Required(item, $"{nameof(HvacAssignments)}[{index}]").ToDomain(sourceResolver)),
                ventilationAssignments.Select((item, index) =>
                    Required(item, $"{nameof(VentilationAssignments)}[{index}]").ToDomain(ventilatorResolver)),
                photovoltaicPanels.Select((item, index) =>
                    Required(item, $"{nameof(PhotovoltaicPanels)}[{index}]").ToDomain()),
                NorthAxis,
                Terrain,
                new OutputTableSettings(summaryReports, IncludeElectricityBalanceMonthly));
        }
    }

    private static T Required<T>(T? value, string name)
        where T : class
    {
        return value ?? throw new InvalidDataException($"The snapshot property '{name}' is required.");
    }

    private static T RequiredValue<T>(T? value, string name)
        where T : struct
    {
        return value ?? throw new InvalidDataException($"The snapshot property '{name}' is required.");
    }

    private static string RequiredText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"The snapshot property '{name}' must contain text.");
        }

        return value!;
    }
}
