using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

/// <summary>
/// Deterministic GRM 0.7 JSON serialization.
/// </summary>
public static class GrmWriter
{
    public static string Serialize(GreenRetrofitModel model, bool indented = true)
    {
        DomainSupport.NotNull(model, nameof(model));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = indented,
            }))
        {
            WriteModel(writer, model);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void WriteFile(string path, GreenRetrofitModel model, bool indented = true)
    {
        string target = DomainSupport.RequiredText(path, nameof(path));
        File.WriteAllText(target, Serialize(model, indented), new UTF8Encoding(false));
    }

    private static void WriteModel(Utf8JsonWriter writer, GreenRetrofitModel model)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("building");
        WriteBuilding(writer, model);
        writer.WritePropertyName("materials");
        WriteMaterials(writer, model.Materials);
        writer.WritePropertyName("surface_constructions");
        WriteSurfaceConstructions(writer, model.SurfaceConstructions);
        writer.WritePropertyName("fenestration_constructions");
        WriteFenestrationConstructions(writer, model.FenestrationConstructions);
        writer.WriteEndObject();
    }

    private static void WriteBuilding(Utf8JsonWriter writer, GreenRetrofitModel model)
    {
        writer.WriteStartObject();
        writer.WriteString("name", model.Name);
        CanonicalDouble.Write(writer, "north_axis", model.NorthAxis);
        writer.WriteString("address", model.Address);
        writer.WritePropertyName("vintage");
        writer.WriteStartArray();
        writer.WriteNumberValue(model.Vintage.Year);
        writer.WriteNumberValue(model.Vintage.Month);
        writer.WriteNumberValue(model.Vintage.Day);
        writer.WriteEndArray();
        writer.WriteBoolean("is_multifamily_housing", model.IsMultifamilyHousing);
        writer.WritePropertyName("floors");
        writer.WriteStartArray();
        foreach (BuildingFloor floor in model.Floors)
        {
            WriteFloor(writer, floor);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("supply_systems");
        WriteSupplySystems(writer, model.SupplySystems);
        writer.WritePropertyName("source_systems");
        WriteSourceSystems(writer, model.SourceSystems);
        writer.WritePropertyName("ventilation_systems");
        writer.WriteStartArray();
        foreach (VentilationSystem system in model.VentilationSystems)
        {
            writer.WriteStartObject();
            writer.WriteString("id", system.Id.Value);
            writer.WriteString("name", system.Name);
            CanonicalDouble.Write(writer, "airflow_rate", system.AirflowRate);
            CanonicalDouble.Write(writer, "efficiency_heating", system.HeatingEfficiency);
            CanonicalDouble.Write(writer, "efficiency_cooling", system.CoolingEfficiency);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("photovoltaic_systems");
        writer.WriteStartArray();
        foreach (PhotovoltaicSystem system in model.PhotovoltaicSystems)
        {
            writer.WriteStartObject();
            writer.WriteString("id", system.Id.Value);
            writer.WriteString("name", system.Name);
            CanonicalDouble.Write(writer, "area", system.Area);
            CanonicalDouble.Write(writer, "efficiency", system.Efficiency);
            CanonicalDouble.Write(writer, "azimuth", system.Azimuth);
            CanonicalDouble.Write(writer, "tilt", system.Tilt);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteFloor(Utf8JsonWriter writer, BuildingFloor floor)
    {
        writer.WriteStartObject();
        writer.WriteNumber("floor_number", floor.FloorNumber);
        writer.WritePropertyName("zones");
        writer.WriteStartArray();
        foreach (Zone zone in floor.Zones)
        {
            WriteZone(writer, zone);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteZone(Utf8JsonWriter writer, Zone zone)
    {
        writer.WriteStartObject();
        writer.WriteString("id", zone.Id.Value);
        writer.WriteString("name", zone.Name);
        CanonicalDouble.Write(writer, "height", zone.Height);
        writer.WriteString("profile", zone.ProfileName);
        WriteNullableNumber(writer, "light_density", zone.LightDensity);
        writer.WritePropertyName("supply_system_ids");
        writer.WriteStartArray();
        foreach (SupplySystemAssignment assignment in zone.SupplySystemAssignments)
        {
            writer.WriteStringValue(assignment.SupplySystemId);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("ventilation_systems");
        writer.WriteStartArray();
        foreach (VentilationAssignment assignment in zone.VentilationAssignments)
        {
            writer.WriteStartObject();
            writer.WriteString("id", assignment.VentilationSystemId);
            writer.WriteNumber("count", assignment.Count);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("surfaces");
        writer.WriteStartArray();
        foreach (Surface surface in zone.Surfaces)
        {
            WriteSurface(writer, surface);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSurface(Utf8JsonWriter writer, Surface surface)
    {
        writer.WriteStartObject();
        writer.WriteString("id", surface.Id.Value);
        writer.WriteString("name", surface.Name);
        writer.WriteString("type", GrmVocabulary.ToGrm(surface.Type));
        writer.WriteString("boundary_condition", GrmVocabulary.ToGrm(surface.BoundaryCondition));
        CanonicalDouble.Write(writer, "area", surface.Area);
        if (surface.Type == SurfaceType.Wall
            && surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors)
        {
            CanonicalDouble.Write(writer, "azimuth", surface.Azimuth!.Value);
        }

        if (surface.BoundaryCondition == SurfaceBoundaryCondition.Zone
            || surface.BoundaryCondition == SurfaceBoundaryCondition.AdjacentSpace)
        {
            writer.WriteString("adjacent_zone_id", surface.AdjacentZoneId);
        }

        if (surface.Type == SurfaceType.Ceiling
            && surface.BoundaryCondition == SurfaceBoundaryCondition.Outdoors)
        {
            WriteNullableNumber(writer, "coolroof_reflectance", surface.CoolRoofReflectance);
        }

        if (surface.ConstructionReferenceKind == SurfaceConstructionReferenceKind.Unknown)
        {
            writer.WriteNull("construction_id");
        }
        else
        {
            writer.WriteString("construction_id", surface.ConstructionId);
        }

        writer.WritePropertyName("fenestrations");
        writer.WriteStartArray();
        foreach (Fenestration opening in surface.Fenestrations)
        {
            writer.WriteStartObject();
            writer.WriteString("id", opening.Id.Value);
            writer.WriteString("name", opening.Name);
            writer.WriteString("type", GrmVocabulary.ToGrm(opening.Type));
            CanonicalDouble.Write(writer, "area", opening.Area);
            if (opening.Type != FenestrationType.Door)
            {
                if (opening.Blind.HasValue)
                {
                    writer.WriteString("blind", GrmVocabulary.ToGrm(opening.Blind.Value));
                }
                else
                {
                    writer.WriteNull("blind");
                }
            }

            writer.WriteString("construction_id", opening.ConstructionId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSupplySystems(Utf8JsonWriter writer, IReadOnlyList<SupplySystem> systems)
    {
        writer.WriteStartObject();
        foreach (SupplySystemType type in GrmVocabulary.SupplySystemOrder)
        {
            SupplySystem[] typedSystems = systems.Where(system => system.Type == type).ToArray();
            if (typedSystems.Length == 0)
            {
                continue;
            }

            writer.WritePropertyName(GrmVocabulary.ToGrm(type));
            writer.WriteStartArray();
            foreach (SupplySystem system in typedSystems)
            {
                writer.WriteStartObject();
                writer.WriteString("id", system.Id.Value);
                writer.WriteString("name", system.Name);
                if (system.HasGrmField("source_system_id"))
                {
                    WriteNullableString(writer, "source_system_id", system.SourceSystemId);
                }

                if (system.HasGrmField("capacity_cooling"))
                {
                    WriteNullableNumber(writer, "capacity_cooling", system.CoolingCapacity);
                }

                if (system.HasGrmField("capacity_heating"))
                {
                    WriteNullableNumber(writer, "capacity_heating", system.HeatingCapacity);
                }

                if (system.HasGrmField("cop_cooling"))
                {
                    WriteNullableNumber(writer, "cop_cooling", system.CoolingCop);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteSourceSystems(Utf8JsonWriter writer, IReadOnlyList<SourceSystem> systems)
    {
        writer.WriteStartObject();
        foreach (SourceSystemType type in GrmVocabulary.SourceSystemOrder)
        {
            SourceSystem[] typedSystems = systems.Where(system => system.Type == type).ToArray();
            if (typedSystems.Length == 0)
            {
                continue;
            }

            writer.WritePropertyName(GrmVocabulary.ToGrm(type));
            writer.WriteStartArray();
            foreach (SourceSystem system in typedSystems)
            {
                WriteSourceSystem(writer, system);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteSourceSystem(Utf8JsonWriter writer, SourceSystem system)
    {
        writer.WriteStartObject();
        writer.WriteString("id", system.Id.Value);
        writer.WriteString("name", system.Name);
        WriteSourceNumber(writer, system, "capacity_cooling", system.CoolingCapacity);
        WriteSourceNumber(writer, system, "capacity_heating", system.HeatingCapacity);
        WriteSourceNumber(writer, system, "cop_cooling", system.CoolingCop);
        WriteSourceNumber(writer, system, "cop_heating", system.HeatingCop);
        WriteSourceNumber(writer, system, "efficiency", system.Efficiency);
        if (system.HasGrmField("fuel_type"))
        {
            if (system.FuelType.HasValue)
            {
                writer.WriteString("fuel_type", GrmVocabulary.ToGrm(system.FuelType.Value));
            }
            else
            {
                writer.WriteNull("fuel_type");
            }
        }

        if (system.HasGrmField("hotwater_supply"))
        {
            if (system.HotWaterSupply.HasValue)
            {
                writer.WriteBoolean("hotwater_supply", system.HotWaterSupply.Value);
            }
            else
            {
                writer.WriteNull("hotwater_supply");
            }
        }

        if (system.HasGrmField("compressor_type"))
        {
            WriteEnumOrNull(
                writer,
                "compressor_type",
                system.CompressorType,
                GrmVocabulary.ToGrm);
        }

        if (system.HasGrmField("coolingtower_type"))
        {
            WriteEnumOrNull(
                writer,
                "coolingtower_type",
                system.CoolingTowerType,
                GrmVocabulary.ToGrm);
        }

        WriteSourceNumber(writer, system, "coolingtower_capacity", system.CoolingTowerCapacity);
        if (system.HasGrmField("coolingtower_control"))
        {
            WriteEnumOrNull(
                writer,
                "coolingtower_control",
                system.CoolingTowerControl,
                GrmVocabulary.ToGrm);
        }

        WriteSourceNumber(writer, system, "boiler_efficiency", system.BoilerEfficiency);
        writer.WriteEndObject();
    }

    private static void WriteMaterials(Utf8JsonWriter writer, IReadOnlyList<Material> materials)
    {
        writer.WriteStartArray();
        foreach (Material material in materials)
        {
            writer.WriteStartObject();
            writer.WriteString("id", material.Id.Value);
            writer.WriteString("name", material.Name);
            CanonicalDouble.Write(writer, "conductivity", material.Conductivity);
            CanonicalDouble.Write(writer, "density", material.Density);
            CanonicalDouble.Write(writer, "specific_heat", material.SpecificHeat);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteSurfaceConstructions(
        Utf8JsonWriter writer,
        IReadOnlyList<SurfaceConstruction> constructions)
    {
        writer.WriteStartArray();
        foreach (SurfaceConstruction construction in constructions)
        {
            writer.WriteStartObject();
            writer.WriteString("id", construction.Id.Value);
            writer.WriteString("name", construction.Name);
            writer.WritePropertyName("layers");
            writer.WriteStartArray();
            foreach (SurfaceConstructionLayer layer in construction.Layers)
            {
                writer.WriteStartObject();
                writer.WriteString("material_id", layer.Material.Id.Value);
                CanonicalDouble.Write(writer, "thickness", layer.Thickness);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteFenestrationConstructions(
        Utf8JsonWriter writer,
        IReadOnlyList<FenestrationConstruction> constructions)
    {
        writer.WriteStartArray();
        foreach (FenestrationConstruction construction in constructions)
        {
            writer.WriteStartObject();
            writer.WriteString("id", construction.Id.Value);
            writer.WriteString("name", construction.Name);
            writer.WriteBoolean("is_transparent", construction.IsTransparent);
            CanonicalDouble.Write(writer, "u", construction.UValue);
            WriteNullableNumber(writer, "g", construction.SolarHeatGainCoefficient);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteSourceNumber(
        Utf8JsonWriter writer,
        SourceSystem system,
        string name,
        double? value)
    {
        if (system.HasGrmField(name))
        {
            WriteNullableNumber(writer, name, value);
        }
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, double? value)
    {
        if (value.HasValue)
        {
            CanonicalDouble.Write(writer, name, value.Value);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteEnumOrNull<T>(
        Utf8JsonWriter writer,
        string name,
        T? value,
        Func<T, string> formatter)
        where T : struct
    {
        if (value.HasValue)
        {
            writer.WriteString(name, formatter(value.Value));
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
