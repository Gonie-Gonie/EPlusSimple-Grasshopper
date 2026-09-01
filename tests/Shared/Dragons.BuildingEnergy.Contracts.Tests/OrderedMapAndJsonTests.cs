using System.Globalization;
using System.Text.Json;

namespace Dragons.BuildingEnergy.Contracts.Tests;

public sealed class OrderedMapAndJsonTests
{
    private static readonly string[] ExpectedInitialKeys = { "z", "A", "a" };
    private static readonly int[] ExpectedInitialValues = { 1, 2, 3 };
    private static readonly string[] ExpectedOriginalKeys = { "first", "second" };
    private static readonly string[] ExpectedUpdatedKeys = { "first", "second", "third" };
    private static readonly string[] ExpectedJsonKeys = { "Z-Key", "aKey" };

    [Fact]
    public void MapPreservesInsertionOrderAndUsesOrdinalKeys()
    {
        OrderedMap<int> map = new OrderedMap<int>()
            .Add("z", 1)
            .Add("A", 2)
            .Add("a", 3);

        Assert.Equal(ExpectedInitialKeys, map.Keys);
        Assert.Equal(ExpectedInitialValues, map.Values);
        Assert.Equal(2, map["A"]);
        Assert.False(map.ContainsKey("Z"));
    }

    [Fact]
    public void SetItemKeepsExistingPositionAndAppendAddsAtTheEnd()
    {
        OrderedMap<int> original = new OrderedMap<int>()
            .Add("first", 1)
            .Add("second", 2);

        OrderedMap<int> updated = original
            .SetItem("first", 10)
            .SetItem("third", 3);

        Assert.Equal(ExpectedOriginalKeys, original.Keys);
        Assert.Equal(ExpectedUpdatedKeys, updated.Keys);
        Assert.Equal(10, updated["first"]);
    }

    [Fact]
    public void ConstructorRejectsDuplicateKeys()
    {
        KeyValuePair<string, int>[] entries =
        {
            new("same", 1),
            new("same", 2),
        };

        Assert.Throws<ArgumentException>(() => new OrderedMap<int>(entries));
    }

    [Fact]
    public void JsonRoundTripPreservesMapOrderAndSemanticKeys()
    {
        JsonSerializerOptions options = BuildingEnergyJson.CreateOptions();
        OrderedMap<int> original = new OrderedMap<int>()
            .Add("Z-Key", 1)
            .Add("aKey", 2);

        string json = JsonSerializer.Serialize(original, options);
        OrderedMap<int>? restored = JsonSerializer.Deserialize<OrderedMap<int>>(json, options);

        Assert.Equal("{\"Z-Key\":1,\"aKey\":2}", json);
        Assert.NotNull(restored);
        Assert.Equal(ExpectedJsonKeys, restored.Keys);
    }

    [Fact]
    public void JsonRejectsDuplicateMapProperties()
    {
        JsonSerializerOptions options = BuildingEnergyJson.CreateOptions();

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<OrderedMap<int>>("{\"same\":1,\"same\":2}", options));
    }

    [Theory]
    [InlineData("EnergyPlusVersion", "energy_plus_version")]
    [InlineData("XMLValue", "xml_value")]
    [InlineData("already_snake_case", "already_snake_case")]
    [InlineData("GH-Component Name", "gh_component_name")]
    public void SnakeCasePolicyIsStableAcrossAcronymsAndSeparators(string input, string expected)
    {
        Assert.Equal(expected, SnakeCaseLowerNamingPolicy.Instance.ConvertName(input));
    }

    [Fact]
    public void EnumSerializationUsesSnakeCaseAndRejectsNumbers()
    {
        JsonSerializerOptions options = BuildingEnergyJson.CreateOptions();

        Assert.Equal("\"warning\"", JsonSerializer.Serialize(DiagnosticSeverity.Warning, options));
        Assert.Equal(
            DiagnosticSeverity.Fatal,
            JsonSerializer.Deserialize<DiagnosticSeverity>("\"fatal\"", options));
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<DiagnosticSeverity>("2", options));
    }

    [Fact]
    public void SchemaEnvelopeHasExplicitStableMetadataOrder()
    {
        SchemaEnvelope<string> envelope = new(
            "simpledragon-gh-model.v1",
            "0.1.0",
            "847b01f",
            "payload-value");

        string json = JsonSerializer.Serialize(envelope, BuildingEnergyJson.CreateOptions());

        Assert.Equal(
            "{\"schema_version\":\"simpledragon-gh-model.v1\",\"core_version\":\"0.1.0\",\"upstream_commit\":\"847b01f\",\"payload\":\"payload-value\"}",
            json);
    }

    [Fact]
    public void NumericJsonRemainsInvariantUnderCommaDecimalCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            OrderedMap<double> payload = new OrderedMap<double>().Add("value", 1.25);

            string json = JsonSerializer.Serialize(payload, BuildingEnergyJson.CreateOptions());

            Assert.Equal("{\"value\":1.25}", json);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void InvariantTextIgnoresTheCurrentCultureAndRoundTripsNumbers()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            string formatted = InvariantText.FormatDouble(1234.5);
            double restored = InvariantText.ParseDouble(formatted);

            Assert.Equal("1234.5", formatted);
            Assert.Equal(1234.5, restored);
            Assert.Throws<FormatException>(() => InvariantText.ParseDouble("1234,5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void InvariantTextNormalizesTimestampsToUtcIso8601()
    {
        DateTimeOffset seoulTime = new(2026, 8, 24, 21, 30, 0, TimeSpan.FromHours(9));

        Assert.Equal("2026-08-24T12:30:00.0000000+00:00", InvariantText.FormatUtc(seoulTime));
    }

    [Fact]
    public void EachOptionsRequestReturnsAnIndependentInstance()
    {
        JsonSerializerOptions changed = BuildingEnergyJson.CreateOptions();
        JsonSerializerOptions untouched = BuildingEnergyJson.CreateOptions();

        changed.WriteIndented = true;

        Assert.True(changed.WriteIndented);
        Assert.False(untouched.WriteIndented);
    }
}
