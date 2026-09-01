using System.Text.Json.Nodes;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Grasshopper.Types;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;

namespace Dragons.InvisibleDragon.Grasshopper.Tests;

public sealed class DragonGooSnapshotDiagnosticsTests
{
    [Fact]
    public void FullModelSnapshotIsDeterministicAcrossRoundTrip()
    {
        EnergyModel source = HvacDragonGooTests.FullHvacModel();

        string first = DragonGooSnapshot.Serialize(source);
        EnergyModel restored = DragonGooSnapshot.Deserialize<EnergyModel>(first);
        string second = DragonGooSnapshot.Serialize(restored);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ExistingZoneOnlyV1PayloadRemainsReadableWithoutNewCollections()
    {
        string snapshot = DragonGooSnapshot.Serialize(HvacDragonGooTests.FullHvacModel());
        string legacy = MutatePayload(snapshot, payload =>
        {
            payload.Remove("sources");
            payload.Remove("hvacAssignments");
            payload.Remove("ventilators");
            payload.Remove("ventilationAssignments");
            payload.Remove("photovoltaicPanels");
        });

        EnergyModel restored = DragonGooSnapshot.Deserialize<EnergyModel>(legacy);

        Assert.Equal("Full HVAC Model", restored.Name);
        Assert.Equal(2, restored.Zones.Count);
        Assert.Empty(restored.HvacAssignments);
        Assert.Empty(restored.VentilationAssignments);
        Assert.Empty(restored.PhotovoltaicPanels);
        Assert.Equal(17.5, restored.NorthAxisDegrees);
        Assert.Equal(Terrain.City, restored.Terrain);
    }

    [Fact]
    public void UnknownSourceKindIsRejectedWithActionableDiagnostic()
    {
        SourceSystem source = new HeatPump(
            new EntityId("source-diagnostic"),
            "Diagnostic Heat Pump",
            Fuel.Electricity,
            3.2,
            4.3);
        string snapshot = DragonGooSnapshot.Serialize(source);
        string corrupted = MutatePayload(snapshot, payload =>
        {
            JsonObject sourcePayload = Assert.IsType<JsonObject>(
                Assert.Single(Assert.IsType<JsonArray>(payload["sources"])));
            sourcePayload["kind"] = "future-reactor";
        });

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => DragonGooSnapshot.Deserialize<SourceSystem>(corrupted));

        Assert.Contains("Unknown source-system snapshot kind 'future-reactor'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Expected heat-pump", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSharedSourceDefinitionNamesTheBrokenIdentifier()
    {
        HeatPump source = new(
            new EntityId("source-missing"),
            "Missing Heat Pump",
            Fuel.Electricity,
            3.4,
            4.5);
        SupplySystem supply = new AirHandlingUnit(
            new EntityId("supply-missing-source"),
            "Missing Source AHU",
            source);
        string snapshot = DragonGooSnapshot.Serialize(supply);
        string corrupted = MutatePayload(snapshot, payload => payload["sources"] = new JsonArray());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => DragonGooSnapshot.Deserialize<SupplySystem>(corrupted));

        Assert.Contains("missing source-system identifier 'source-missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedEnvelopeSchemaAndKindAreRejectedActionably()
    {
        string snapshot = DragonGooSnapshot.Serialize(
            new PhotovoltaicPanel(
                new EntityId("pv-diagnostic"),
                "Diagnostic PV",
                10,
                30,
                180,
                0.2));
        JsonObject unsupportedSchema = ParseObject(snapshot);
        unsupportedSchema["schema"] = "dragons.invisible-dragon.grasshopper-goo.v99";
        InvalidDataException schemaException = Assert.Throws<InvalidDataException>(
            () => DragonGooSnapshot.Deserialize<PhotovoltaicPanel>(unsupportedSchema.ToJsonString()));
        Assert.Contains("Unsupported Grasshopper value schema", schemaException.Message, StringComparison.Ordinal);
        Assert.Contains("v99", schemaException.Message, StringComparison.Ordinal);

        JsonObject unsupportedKind = ParseObject(snapshot);
        unsupportedKind["kind"] = "future-hvac-object";
        InvalidDataException kindException = Assert.Throws<InvalidDataException>(
            () => DragonGooSnapshot.Deserialize<PhotovoltaicPanel>(unsupportedKind.ToJsonString()));
        Assert.Contains("Unsupported Grasshopper value kind 'future-hvac-object'", kindException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownDomainSubtypeIsRejectedWithItsRuntimeTypeAndSupportedList()
    {
        var unsupported = new UnsupportedSourceSystem(
            new EntityId("source-unsupported"),
            "Unsupported Source");

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => DragonGooSnapshot.Serialize<SourceSystem>(unsupported));

        Assert.Contains(typeof(UnsupportedSourceSystem).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Supported types are HeatPump", exception.Message, StringComparison.Ordinal);
    }

    private static string MutatePayload(string snapshot, Action<JsonObject> mutation)
    {
        JsonObject envelope = ParseObject(snapshot);
        string payloadText = envelope["payload"]?.GetValue<string>()
            ?? throw new Xunit.Sdk.XunitException("The test snapshot envelope has no payload.");
        JsonObject payload = ParseObject(payloadText);
        mutation(payload);
        envelope["payload"] = payload.ToJsonString();
        return envelope.ToJsonString();
    }

    private static JsonObject ParseObject(string json)
    {
        return JsonNode.Parse(json) as JsonObject
            ?? throw new Xunit.Sdk.XunitException("The test JSON was not an object.");
    }

    private sealed class UnsupportedSourceSystem : SourceSystem
    {
        public UnsupportedSourceSystem(EntityId id, string name)
            : base(id, name)
        {
        }

        public override string IdfObjectType => "Unsupported:Source";

        public override string IdfObjectName => Name;

        public override IReadOnlyList<IdfObject> ToIdfObjects(
            IdfGenerationContext context,
            IReadOnlyList<PlantDemandConnection>? demandConnections = null,
            IReadOnlyList<string>? terminalUnitNames = null)
        {
            return Array.Empty<IdfObject>();
        }
    }
}
