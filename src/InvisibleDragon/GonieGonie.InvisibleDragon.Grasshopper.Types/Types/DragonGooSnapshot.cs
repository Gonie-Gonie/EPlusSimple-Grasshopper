using System.Globalization;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
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

    private sealed class ModelSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public List<ZoneSnapshot> Zones { get; set; } = new();
        public double NorthAxis { get; set; }
        public Terrain Terrain { get; set; }
        public List<string> SummaryReports { get; set; } = new();
        public bool IncludeElectricityBalanceMonthly { get; set; }

        public static ModelSnapshot From(EnergyModel value)
        {
            if (value.HvacAssignments.Count != 0 ||
                value.VentilationAssignments.Count != 0 ||
                value.PhotovoltaicPanels.Count != 0)
            {
                throw new NotSupportedException(
                    "The first Grasshopper snapshot schema persists zone-only models; HVAC and PV persistence will be added with their public Goo types.");
            }

            return new ModelSnapshot
            {
                Name = value.Name,
                Zones = value.Zones.Select(ZoneSnapshot.From).ToList(),
                NorthAxis = value.NorthAxisDegrees,
                Terrain = value.Terrain,
                SummaryReports = value.OutputTables.SummaryReports.ToList(),
                IncludeElectricityBalanceMonthly = value.OutputTables.IncludeElectricityBalanceMonthly,
            };
        }

        public EnergyModel ToDomain() => new(
            Name,
            Zones.Select(item => item.ToDomain()),
            northAxisDegrees: NorthAxis,
            terrain: Terrain,
            outputTables: new OutputTableSettings(SummaryReports, IncludeElectricityBalanceMonthly));
    }

    private static T Required<T>(T? value, string name)
        where T : class
    {
        return value ?? throw new InvalidDataException($"The snapshot property '{name}' is required.");
    }
}
