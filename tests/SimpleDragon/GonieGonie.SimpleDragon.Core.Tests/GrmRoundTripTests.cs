using System.Text.Json;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class GrmRoundTripTests
{
    private static readonly string[] ExpectedSurfaceIds =
    {
        "SURF-0x000000",
        "SURF-0x000001",
        "SURF-0x000002",
        "SURF-0x000003",
        "SURF-0x000004",
        "SURF-0x000005",
    };

    [Fact]
    public void AshraeFixtureLoadsWithUpstreamCountsValuesAndReferences()
    {
        GrmReadResult result = GrmReader.ReadFile(GetFixturePath());

        Assert.True(result.Success, Describe(result));
        Assert.Empty(result.Diagnostics);
        GreenRetrofitModel model = result.RequireModel();

        Assert.Equal(GrmFormat.Version, "0.7.0");
        Assert.Equal("ASHRAE 140 modified", model.Name);
        Assert.Equal(new DateTime(2000, 1, 1), model.Vintage);
        Assert.False(model.IsMultifamilyHousing);
        Assert.Equal(0d, model.NorthAxis);
        Assert.Contains(model.Address, character => character > 127);
        Assert.NotNull(model.Weather);

        Assert.Single(model.Floors);
        Zone zone = Assert.Single(model.Zones);
        Assert.Equal(6, zone.Surfaces.Count);
        Assert.Equal(2, zone.Surfaces.Sum(surface => surface.Fenestrations.Count));
        Assert.Equal(48d, zone.Area);
        Assert.Equal(48d, model.Area);
        Assert.Equal(2.7d, zone.Height);
        Assert.Equal(10d, zone.LightDensity);
        Assert.Equal(1.5d, zone.Infiltration);
        Assert.NotNull(zone.Profile);

        Assert.Equal(6, model.Materials.Count);
        Assert.Equal(3, model.SurfaceConstructions.Count);
        Assert.Single(model.FenestrationConstructions);
        Assert.Equal(2, model.SourceSystems.Count);
        Assert.Equal(2, model.SupplySystems.Count);
        Assert.Empty(model.VentilationSystems);
        Assert.Empty(model.PhotovoltaicSystems);
        Assert.Equal(4, model.ExteriorWalls.Count);
        Assert.Equal(2, model.ExteriorWindows.Count);

        Surface southWall = Assert.Single(model.ExteriorWalls, surface => surface.Azimuth == 180d);
        Assert.Equal(12d, southWall.Fenestrations.Sum(opening => opening.Area));
        Assert.Equal(2, southWall.WindowCount);
        Assert.Equal("CTSF-0x000000", southWall.ConstructionId);
        Assert.NotNull(southWall.Construction);

        SourceSystem heatPump = Assert.Single(
            model.SourceSystems,
            system => system.Type == SourceSystemType.HeatPump);
        Assert.Equal(FuelType.Electricity, heatPump.FuelType);
        Assert.Equal(3d, heatPump.HeatingCop);
        Assert.Equal(3d, heatPump.CoolingCop);
        Assert.Null(heatPump.HeatingCapacity);
        Assert.Null(heatPump.CoolingCapacity);

        SourceSystem boiler = Assert.Single(
            model.SourceSystems,
            system => system.Type == SourceSystemType.Boiler);
        Assert.Equal(FuelType.LiquefiedPetroleumGas, boiler.FuelType);
        Assert.True(boiler.HotWaterSupply);
        Assert.Equal(0.85d, boiler.Efficiency);

        SupplySystem airHandler = Assert.Single(
            model.SupplySystems,
            system => system.Type == SupplySystemType.AirHandlingUnit);
        SupplySystem radiantFloor = Assert.Single(
            model.SupplySystems,
            system => system.Type == SupplySystemType.RadiantFloor);
        Assert.Same(heatPump, airHandler.SourceSystem);
        Assert.Same(boiler, radiantFloor.SourceSystem);
        Assert.True(airHandler.Heatable);
        Assert.True(airHandler.Coolable);
        Assert.True(radiantFloor.Heatable);
        Assert.False(radiantFloor.Coolable);
    }

    [Fact]
    public void CanonicalWriterIsDeterministicUnicodeSafeAndFixedAfterOneRoundTrip()
    {
        GreenRetrofitModel model = GrmReader.ReadFile(GetFixturePath()).RequireModel();

        string first = GrmWriter.Serialize(model);
        string duplicate = GrmWriter.Serialize(model);
        Assert.Equal(first, duplicate);
        Assert.Contains(model.Address, first, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", first, StringComparison.OrdinalIgnoreCase);

        GrmReadResult reread = GrmReader.Read(first);
        Assert.True(reread.Success, Describe(reread));
        Assert.Equal(first, GrmWriter.Serialize(reread.RequireModel()));

        using JsonDocument document = JsonDocument.Parse(first);
        JsonElement building = document.RootElement.GetProperty("building");
        JsonElement sourceSystems = building.GetProperty("source_systems");
        JsonElement heatPump = sourceSystems.GetProperty("heatpump")[0];
        JsonElement boiler = sourceSystems.GetProperty("boiler")[0];

        Assert.Equal(JsonValueKind.Null, heatPump.GetProperty("capacity_cooling").ValueKind);
        Assert.Equal(JsonValueKind.Null, heatPump.GetProperty("capacity_heating").ValueKind);
        Assert.False(boiler.TryGetProperty("efficiency", out _));
        Assert.False(boiler.TryGetProperty("capacity_heating", out _));
        Assert.Equal("SRCE-0x000001", boiler.GetProperty("id").GetString());

        string[] surfaceIds = building.GetProperty("floors")[0]
            .GetProperty("zones")[0]
            .GetProperty("surfaces")
            .EnumerateArray()
            .Select(surface => surface.GetProperty("id").GetString()!)
            .ToArray();
        Assert.Equal(ExpectedSurfaceIds, surfaceIds);
    }

    [Fact]
    public void WriterAndReaderCoverEverySystemGroupPlusVentilationAndPhotovoltaics()
    {
        GreenRetrofitModel fixture = GrmReader.ReadFile(GetFixturePath()).RequireModel();
        SourceSystem fixtureHeatPump = Assert.Single(
            fixture.SourceSystems,
            system => system.Type == SourceSystemType.HeatPump);
        SourceSystem fixtureBoiler = Assert.Single(
            fixture.SourceSystems,
            system => system.Type == SourceSystemType.Boiler);
        var geothermal = new SourceSystem(
            "geothermal",
            SourceSystemType.GeothermalHeatPump,
            FuelType.Electricity,
            heatingCop: 4d,
            coolingCop: 4.5d);
        var chiller = new SourceSystem(
            "chiller",
            SourceSystemType.Chiller,
            coolingCop: 3.2d,
            compressorType: CompressorType.Turbo,
            coolingTowerType: CoolingTowerType.Closed,
            coolingTowerCapacity: 12_000d,
            coolingTowerControl: CoolingTowerControl.SingleSpeed);
        var absorption = new SourceSystem(
            "absorption",
            SourceSystemType.AbsorptionChiller,
            FuelType.NaturalGas,
            coolingCop: 0.9d,
            boilerEfficiency: 0.85d);
        var district = new SourceSystem(
            "district",
            SourceSystemType.DistrictHeating,
            hotWaterSupply: true);
        SourceSystem[] sources =
        {
            fixtureHeatPump,
            geothermal,
            chiller,
            absorption,
            fixtureBoiler,
            district,
        };

        SupplySystem fixtureAirHandler = Assert.Single(
            fixture.SupplySystems,
            system => system.Type == SupplySystemType.AirHandlingUnit);
        SupplySystem fixtureRadiantFloor = Assert.Single(
            fixture.SupplySystems,
            system => system.Type == SupplySystemType.RadiantFloor);
        SupplySystem[] supplies =
        {
            new("packaged", SupplySystemType.PackagedAirConditioner, coolingCop: 3.1d),
            fixtureAirHandler,
            new("fan coil", SupplySystemType.FanCoilUnit, chiller.Id.Value, chiller),
            new("radiator", SupplySystemType.Radiator, fixtureBoiler.Id.Value, fixtureBoiler, heatingCapacity: 8_000d),
            new("electric radiator", SupplySystemType.ElectricRadiator, heatingCapacity: 3_000d),
            fixtureRadiantFloor,
            new("electric radiant floor", SupplySystemType.ElectricRadiantFloor),
        };
        var ventilation = new VentilationSystem("heat recovery", 0.5d, 0.75d, 0.5d);
        var photovoltaic = new PhotovoltaicSystem("roof pv", 24d, 0.2d, 180d, 30d);
        var expanded = new GreenRetrofitModel(
            fixture.Name,
            fixture.NorthAxis,
            fixture.Address,
            fixture.Vintage,
            fixture.IsMultifamilyHousing,
            fixture.Floors,
            fixture.Materials,
            fixture.SurfaceConstructions,
            fixture.FenestrationConstructions,
            sources,
            supplies,
            new[] { ventilation },
            new[] { photovoltaic },
            fixture.Weather);

        string json = GrmWriter.Serialize(expanded);
        GrmReadResult result = GrmReader.Read(json);

        Assert.True(result.Success, Describe(result));
        GreenRetrofitModel roundTripped = result.RequireModel();
        Assert.Equal(Enum.GetValues<SourceSystemType>().Length, roundTripped.SourceSystems.Count);
        Assert.Equal(Enum.GetValues<SupplySystemType>().Length, roundTripped.SupplySystems.Count);
        Assert.Single(roundTripped.VentilationSystems);
        Assert.Single(roundTripped.PhotovoltaicSystems);
        Assert.Equal(json, GrmWriter.Serialize(roundTripped));
    }

    [Fact]
    public void InvalidSyntaxAndUnknownVocabularyReturnStableDiagnostics()
    {
        GrmReadResult invalidJson = GrmReader.Read("{\"building\":");
        Assert.False(invalidJson.Success);
        Assert.Null(invalidJson.Model);
        Assert.Equal("SD.GRM.JSON_INVALID", Assert.Single(invalidJson.Diagnostics).Code);

        string fixture = File.ReadAllText(GetFixturePath());
        string invalidBoundary = ReplaceFirst(
            fixture,
            "\"boundary_condition\": \"ground\"",
            "\"boundary_condition\": \"ocean\"");
        GrmReadResult unknown = GrmReader.Read(invalidBoundary);

        Assert.False(unknown.Success);
        Assert.Null(unknown.Model);
        Assert.Contains(unknown.Diagnostics, diagnostic => diagnostic.Code == "SD.GRM.BOUNDARY_UNKNOWN");
    }

    [Fact]
    public void MissingReferencesAndImpossibleOpeningAreaRetainModelAndDiagnostics()
    {
        string fixture = File.ReadAllText(GetFixturePath());
        string missingConstruction = ReplaceFirst(
            fixture,
            "\"construction_id\": \"CTFN-0x000000\"",
            "\"construction_id\": \"CTFN-MISSING\"");
        GrmReadResult missing = GrmReader.Read(missingConstruction);

        Assert.NotNull(missing.Model);
        Assert.False(missing.Success);
        Assert.Contains(
            missing.Diagnostics,
            diagnostic => diagnostic.Code == "SD.GRM.FENESTRATION_CONSTRUCTION_REFERENCE_NOT_FOUND");

        string impossibleArea = ReplaceFirst(fixture, "\"area\": 6,", "\"area\": 30,");
        GrmReadResult oversized = GrmReader.Read(impossibleArea);

        Assert.NotNull(oversized.Model);
        Assert.False(oversized.Success);
        Assert.Contains(
            oversized.Diagnostics,
            diagnostic => diagnostic.Code == "SD.GRM.OPENING_AREA_EXCEEDS_SURFACE");
    }

    private static string ReplaceFirst(string source, string oldValue, string newValue)
    {
        int index = source.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.True(index >= 0, "The fixture token to replace was not found.");
        return source.Remove(index, oldValue.Length).Insert(index, newValue);
    }

    private static string GetFixturePath()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                "grm",
                "ASHRAE 140 modified.grm");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GRM fixture from the test output directory.");
    }

    private static string Describe(GrmReadResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message));
    }
}
