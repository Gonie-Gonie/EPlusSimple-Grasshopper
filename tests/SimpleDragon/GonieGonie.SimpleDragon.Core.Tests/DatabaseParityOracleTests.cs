using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class DatabaseParityOracleTests
{
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void SurfaceDatabaseMatchesEveryPinnedPythonEntryAndRegulatedBranch()
    {
        SurfaceDatabaseOracle sourceOracle = ReadOracle<SurfaceDatabaseOracle>(
            "surface-construction-database.json");
        DatabaseQueryOracle queryOracle = ReadOracle<DatabaseQueryOracle>(
            "database-query-oracle.json");
        SurfaceConstructionDatabase database = SimpleDragonDatabase.Default.SurfaceConstructions;

        Assert.Equal("goniegonie.python-reference.surface-construction-database.v1", sourceOracle.Schema);
        Assert.Equal("goniegonie.python-reference.database-query-oracle.v1", queryOracle.Schema);
        Assert.Equal(UpstreamCommit, queryOracle.UpstreamCommit);
        Assert.Equal(1344, sourceOracle.Count);
        Assert.Equal(sourceOracle.Count, sourceOracle.Items.Count);
        Assert.Equal(sourceOracle.Count, database.Entries.Count);

        for (int index = 0; index < sourceOracle.Items.Count; index++)
        {
            SurfaceDatabaseItem expected = sourceOracle.Items[index];
            SurfaceConstructionEntry actual = database.Entries[index];

            Assert.Equal(expected.Name, actual.Key.ToString());
            AssertSurfaceDatabaseResult(expected, actual.Construction);
        }

        SurfaceQueryOracle surface = queryOracle.Surface;
        Assert.Equal(14, surface.RegulationDates.Count);
        Assert.Equal(4, surface.Climates.Count);
        Assert.Equal(2, surface.HousingValues.Count);
        Assert.Equal(3, surface.SurfaceTypes.Count);
        Assert.Equal(4, surface.BoundaryConditions.Count);
        Assert.Equal(2, surface.RadiantValues.Count);
        Assert.Equal(2688, surface.QueryCount);
        Assert.Equal(672, surface.UniqueResultCount);
        Assert.Equal(surface.QueryCount, surface.Queries.Count);
        Assert.Equal(
            surface.QueryCount,
            surface.RegulationDates.Count
            * surface.Climates.Count
            * surface.HousingValues.Count
            * surface.SurfaceTypes.Count
            * surface.BoundaryConditions.Count
            * surface.RadiantValues.Count);
        Assert.Equal(
            surface.QueryCount,
            surface.Queries
                .Select(QueryIdentity)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            surface.UniqueResultCount,
            surface.Queries
                .Select(query => query.Result.Name)
                .Distinct(StringComparer.Ordinal)
                .Count());

        foreach (SurfaceQuery query in surface.Queries)
        {
            SurfaceConstruction actual = database.FindRegulated(
                ParseDate(query.Vintage),
                Enum.Parse<SurfaceType>(query.SurfaceType, ignoreCase: true),
                Enum.Parse<SurfaceBoundaryCondition>(query.BoundaryCondition, ignoreCase: true),
                query.Climate,
                query.IsRadiantFloor,
                query.IsMultifamilyHousing).Require();

            AssertSurfaceQueryResult(query.Result, actual);
        }
    }

    [Fact]
    public void FenestrationDatabaseMatchesEveryPinnedPythonKeyAndResult()
    {
        FenestrationDatabaseOracle sourceOracle = ReadOracle<FenestrationDatabaseOracle>(
            "fenestration-construction-database.json");
        FenestrationQueryOracle queryOracle = ReadOracle<DatabaseQueryOracle>(
            "database-query-oracle.json").Fenestration;
        FenestrationConstructionDatabase database = SimpleDragonDatabase.Default.FenestrationConstructions;

        Assert.Equal("goniegonie.python-reference.fenestration-construction-database.v1", sourceOracle.Schema);
        Assert.Equal(432, sourceOracle.Count);
        Assert.Equal(sourceOracle.Count, sourceOracle.Items.Count);
        Assert.Equal(sourceOracle.Count, database.Entries.Count);
        Assert.Equal(sourceOracle.Count, queryOracle.QueryCount);
        Assert.Equal(queryOracle.QueryCount, queryOracle.Queries.Count);
        Assert.Equal(
            queryOracle.QueryCount,
            queryOracle.Queries
                .Select(query => query.Key.ToString())
                .Distinct(StringComparer.Ordinal)
                .Count());

        for (int index = 0; index < sourceOracle.Items.Count; index++)
        {
            FenestrationDatabaseItem expected = sourceOracle.Items[index];
            FenestrationConstructionEntry actual = database.Entries[index];

            Assert.Equal(expected.Name, actual.Key.ToString());
            Assert.Equal(expected.Name, actual.Construction.Name);
            Assert.Equal(expected.UValue, actual.Construction.UValue, 12);
            Assert.Equal(
                expected.SolarHeatGainCoefficient,
                actual.Construction.SolarHeatGainCoefficient!.Value,
                12);
        }

        foreach (FenestrationQuery query in queryOracle.Queries)
        {
            FenestrationKey key = query.Key;
            FenestrationConstruction actual = database.Find(new FenestrationConstructionKey(
                key.WindowCount,
                key.LowEGlass,
                key.Argon,
                key.ThermalBreak,
                key.Frame,
                key.Cavity)).Require();

            Assert.Equal(query.Result.Name, actual.Name);
            Assert.Equal(query.Result.UValue, actual.UValue, 12);
            Assert.Equal(
                query.Result.SolarHeatGainCoefficient,
                actual.SolarHeatGainCoefficient!.Value,
                12);
            Assert.Equal(query.Result.IsTransparent, actual.IsTransparent);
        }
    }

    [Fact]
    public void WeatherDatabaseMatchesEveryPinnedRowAndClimateBoundary()
    {
        WeatherQueryOracle oracle = ReadOracle<DatabaseQueryOracle>(
            "database-query-oracle.json").Weather;
        WeatherDatabase database = SimpleDragonDatabase.Default.Weather;

        Assert.Equal(252, oracle.MetadataCount);
        Assert.Equal(oracle.MetadataCount, oracle.Metadata.Count);
        Assert.Equal(oracle.MetadataCount, database.Items.Count);
        Assert.Equal(5, oracle.ClimateEffectiveDates.Count);
        Assert.Equal(14, oracle.BoundaryVintages.Count);
        Assert.Equal(3528, oracle.QueryCount);
        Assert.Equal(oracle.QueryCount, oracle.Queries.Count);
        Assert.Equal(oracle.MetadataCount * oracle.BoundaryVintages.Count, oracle.QueryCount);
        Assert.Equal(
            oracle.QueryCount,
            oracle.Queries
                .Select(query => query.AdministrativeArea + "\u001f" + query.Vintage)
                .Distinct(StringComparer.Ordinal)
                .Count());

        AssertClimateBoundaryCoverage(oracle);
        for (int index = 0; index < oracle.Metadata.Count; index++)
        {
            WeatherMetadataExpectation expected = oracle.Metadata[index];
            WeatherMetadata actual = database.Items[index];

            Assert.Equal(expected.AdministrativeArea, actual.AdministrativeArea);
            Assert.Equal(expected.LegalDistrictCode, actual.LegalDistrictCode);
            Assert.Equal(expected.Terrain, actual.Terrain);
            Assert.Equal(expected.AdministrativeLatitude, actual.AdministrativeLatitude, 10);
            Assert.Equal(expected.AdministrativeLongitude, actual.AdministrativeLongitude, 10);
            Assert.Equal(expected.WeatherLocation, actual.WeatherLocation);
            Assert.Equal(expected.WeatherLocationType, actual.WeatherLocationType);
            Assert.Equal(expected.WeatherLatitude, actual.WeatherLatitude, 10);
            Assert.Equal(expected.WeatherLongitude, actual.WeatherLongitude, 10);
            Assert.Equal(expected.EpwFileName, actual.EpwFileName);
        }

        foreach (WeatherQuery query in oracle.Queries)
        {
            WeatherSelection actual = database.FindByAddress(
                query.AdministrativeArea,
                ParseDate(query.Vintage)).Require();

            Assert.Equal(query.AdministrativeArea, actual.Metadata.AdministrativeArea);
            Assert.Equal(ParseDate(query.ClimateEffectiveDate), actual.ClimateEffectiveDate);
            Assert.Equal(query.Terrain, actual.Terrain);
            Assert.Equal(query.ClimateRegion, actual.ClimateRegion);
            Assert.Equal(query.WeatherLocation, actual.WeatherLocation);
            Assert.Equal(query.EpwFileName, actual.EpwFileName);
        }
    }

    private static void AssertClimateBoundaryCoverage(WeatherQueryOracle oracle)
    {
        DateTime earliestDate = ParseDate(oracle.ClimateEffectiveDates[0]);
        var expectedVintages = new SortedSet<DateTime>();
        foreach (string effectiveDateText in oracle.ClimateEffectiveDates)
        {
            DateTime effectiveDate = ParseDate(effectiveDateText);
            for (int offset = -1; offset <= 1; offset++)
            {
                DateTime vintage = effectiveDate.AddDays(offset);
                if (vintage >= earliestDate)
                {
                    expectedVintages.Add(vintage);
                }
            }
        }

        Assert.Equal(
            expectedVintages,
            oracle.BoundaryVintages.Select(ParseDate));
        Assert.All(
            oracle.Metadata,
            metadata => Assert.Equal(
                oracle.BoundaryVintages,
                oracle.Queries
                    .Where(query => StringComparer.Ordinal.Equals(
                        query.AdministrativeArea,
                        metadata.AdministrativeArea))
                    .Select(query => query.Vintage)));
    }

    private static void AssertSurfaceDatabaseResult(
        SurfaceDatabaseItem expected,
        SurfaceConstruction actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (int index = 0; index < expected.Layers.Count; index++)
        {
            SurfaceLayerExpectation expectedLayer = expected.Layers[index];
            SurfaceConstructionLayer actualLayer = actual.Layers[index];
            SurfaceMaterialExpectation expectedMaterial = Assert.Single(
                expected.Materials,
                material => StringComparer.Ordinal.Equals(material.Name, expectedLayer.Material));

            Assert.Equal(expectedLayer.Material, actualLayer.Material.Name);
            Assert.Equal(expectedLayer.Thickness, actualLayer.Thickness, 14);
            Assert.Equal(expectedMaterial.Conductivity, actualLayer.Material.Conductivity, 14);
            Assert.Equal(expectedMaterial.Density, actualLayer.Material.Density, 12);
            Assert.Equal(expectedMaterial.SpecificHeat, actualLayer.Material.SpecificHeat, 12);
        }
    }

    private static void AssertSurfaceQueryResult(
        SurfaceQueryResult expected,
        SurfaceConstruction actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.UValue, actual.GetUValue(), 12);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (int index = 0; index < expected.Layers.Count; index++)
        {
            Assert.Equal(expected.Layers[index].Material, actual.Layers[index].Material.Name);
            Assert.Equal(expected.Layers[index].Thickness, actual.Layers[index].Thickness, 14);
        }
    }

    private static string QueryIdentity(SurfaceQuery query)
    {
        return string.Join(
            "\u001f",
            query.Vintage,
            query.Climate,
            query.IsMultifamilyHousing,
            query.SurfaceType,
            query.BoundaryCondition,
            query.IsRadiantFloor);
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.ParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
    }

    private static T ReadOracle<T>(string fileName)
    {
        string path = Path.Combine(
            FindRepositoryRoot().FullName,
            "fixtures",
            "reference",
            "python-0.7.0",
            fileName);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidDataException("Could not deserialize database parity oracle: " + path);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "upstream", "upstream.lock.json")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the test output directory.");
    }

    private sealed class SurfaceDatabaseOracle
    {
        public string Schema { get; init; } = string.Empty;

        public int Count { get; init; }

        public List<SurfaceDatabaseItem> Items { get; init; } = new();
    }

    private sealed class SurfaceDatabaseItem
    {
        public string Name { get; init; } = string.Empty;

        public List<SurfaceLayerExpectation> Layers { get; init; } = new();

        public List<SurfaceMaterialExpectation> Materials { get; init; } = new();
    }

    private sealed class SurfaceLayerExpectation
    {
        public string Material { get; init; } = string.Empty;

        public double Thickness { get; init; }
    }

    private sealed class SurfaceMaterialExpectation
    {
        public string Name { get; init; } = string.Empty;

        public double Conductivity { get; init; }

        public double Density { get; init; }

        public double SpecificHeat { get; init; }
    }

    private sealed class FenestrationDatabaseOracle
    {
        public string Schema { get; init; } = string.Empty;

        public int Count { get; init; }

        public List<FenestrationDatabaseItem> Items { get; init; } = new();
    }

    private sealed class FenestrationDatabaseItem
    {
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("U-value")]
        public double UValue { get; init; }

        [JsonPropertyName("SHGC")]
        public double SolarHeatGainCoefficient { get; init; }
    }

    private sealed class DatabaseQueryOracle
    {
        public string Schema { get; init; } = string.Empty;

        public string UpstreamCommit { get; init; } = string.Empty;

        public SurfaceQueryOracle Surface { get; init; } = new();

        public FenestrationQueryOracle Fenestration { get; init; } = new();

        public WeatherQueryOracle Weather { get; init; } = new();
    }

    private sealed class SurfaceQueryOracle
    {
        public List<string> RegulationDates { get; init; } = new();

        public List<string> Climates { get; init; } = new();

        public List<bool> HousingValues { get; init; } = new();

        public List<string> SurfaceTypes { get; init; } = new();

        public List<string> BoundaryConditions { get; init; } = new();

        public List<bool> RadiantValues { get; init; } = new();

        public int QueryCount { get; init; }

        public int UniqueResultCount { get; init; }

        public List<SurfaceQuery> Queries { get; init; } = new();
    }

    private sealed class SurfaceQuery
    {
        public string Vintage { get; init; } = string.Empty;

        public string Climate { get; init; } = string.Empty;

        public bool IsMultifamilyHousing { get; init; }

        public string SurfaceType { get; init; } = string.Empty;

        public string BoundaryCondition { get; init; } = string.Empty;

        public bool IsRadiantFloor { get; init; }

        public SurfaceQueryResult Result { get; init; } = new();
    }

    private sealed class SurfaceQueryResult
    {
        public string Name { get; init; } = string.Empty;

        public double UValue { get; init; }

        public List<SurfaceLayerExpectation> Layers { get; init; } = new();
    }

    private sealed class FenestrationQueryOracle
    {
        public int QueryCount { get; init; }

        public List<FenestrationQuery> Queries { get; init; } = new();
    }

    private sealed class FenestrationQuery
    {
        public FenestrationKey Key { get; init; } = new();

        public FenestrationQueryResult Result { get; init; } = new();
    }

    private sealed class FenestrationKey
    {
        public string WindowCount { get; init; } = string.Empty;

        public string LowEGlass { get; init; } = string.Empty;

        public string Argon { get; init; } = string.Empty;

        public string ThermalBreak { get; init; } = string.Empty;

        public string Frame { get; init; } = string.Empty;

        public string Cavity { get; init; } = string.Empty;

        public override string ToString()
        {
            return string.Join(
                "\u001f",
                WindowCount,
                LowEGlass,
                Argon,
                ThermalBreak,
                Frame,
                Cavity);
        }
    }

    private sealed class FenestrationQueryResult
    {
        public string Name { get; init; } = string.Empty;

        public double UValue { get; init; }

        public double SolarHeatGainCoefficient { get; init; }

        public bool IsTransparent { get; init; }
    }

    private sealed class WeatherQueryOracle
    {
        public int MetadataCount { get; init; }

        public List<WeatherMetadataExpectation> Metadata { get; init; } = new();

        public List<string> ClimateEffectiveDates { get; init; } = new();

        public List<string> BoundaryVintages { get; init; } = new();

        public int QueryCount { get; init; }

        public List<WeatherQuery> Queries { get; init; } = new();
    }

    private sealed class WeatherMetadataExpectation
    {
        public string AdministrativeArea { get; init; } = string.Empty;

        public string LegalDistrictCode { get; init; } = string.Empty;

        public string Terrain { get; init; } = string.Empty;

        public double AdministrativeLatitude { get; init; }

        public double AdministrativeLongitude { get; init; }

        public string WeatherLocation { get; init; } = string.Empty;

        public string WeatherLocationType { get; init; } = string.Empty;

        public double WeatherLatitude { get; init; }

        public double WeatherLongitude { get; init; }

        public string EpwFileName { get; init; } = string.Empty;
    }

    private sealed class WeatherQuery
    {
        public string AdministrativeArea { get; init; } = string.Empty;

        public string Vintage { get; init; } = string.Empty;

        public string ClimateEffectiveDate { get; init; } = string.Empty;

        public string Terrain { get; init; } = string.Empty;

        public string ClimateRegion { get; init; } = string.Empty;

        public string WeatherLocation { get; init; } = string.Empty;

        public string EpwFileName { get; init; } = string.Empty;
    }
}
