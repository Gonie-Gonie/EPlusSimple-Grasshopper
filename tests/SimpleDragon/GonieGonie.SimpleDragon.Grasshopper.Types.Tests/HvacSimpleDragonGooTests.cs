using System.Text.Json.Nodes;
using GH_IO.Serialization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Grasshopper.Parameters;
using GonieGonie.SimpleDragon.Grasshopper.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GonieGonie.SimpleDragon.Grasshopper.Tests;

public sealed class HvacSimpleDragonGooTests
{
    [Fact]
    public void EverySourceFamilyDuplicatesAndArchiveRoundTripsLosslessly()
    {
        foreach (SourceSystem source in Sources())
        {
            var goo = new SimpleDragonSourceSystemGoo(source);
            var duplicate = Assert.IsType<SimpleDragonSourceSystemGoo>(goo.Duplicate());
            SimpleDragonSourceSystemGoo archived = ArchiveRoundTrip(
                goo,
                new SimpleDragonSourceSystemGoo());

            Assert.NotSame(source, duplicate.Value);
            AssertSourceEquivalent(source, duplicate.Value);
            AssertSourceEquivalent(source, archived.Value);
            Assert.Contains(source.Name, goo.ToString(), StringComparison.Ordinal);

            var directCast = new SimpleDragonSourceSystemGoo();
            Assert.True(directCast.CastFrom(source));
            SourceSystem? restored = null;
            Assert.True(directCast.CastTo(ref restored));
            Assert.Same(source, restored);

            var wrapperCast = new SimpleDragonSourceSystemGoo();
            Assert.True(wrapperCast.CastFrom(new GH_ObjectWrapper(source)));
            Assert.Same(source, wrapperCast.Value);
        }
    }

    [Fact]
    public void EverySupplyFamilyAndNestedSourceDuplicatesAndArchiveRoundTripsLosslessly()
    {
        foreach (SupplySystem supply in Supplies())
        {
            var goo = new SimpleDragonSupplySystemGoo(supply);
            var duplicate = Assert.IsType<SimpleDragonSupplySystemGoo>(goo.Duplicate());
            SimpleDragonSupplySystemGoo archived = ArchiveRoundTrip(
                goo,
                new SimpleDragonSupplySystemGoo());

            Assert.NotSame(supply, duplicate.Value);
            AssertSupplyEquivalent(supply, duplicate.Value);
            AssertSupplyEquivalent(supply, archived.Value);
            Assert.Contains(supply.Name, goo.ToString(), StringComparison.Ordinal);

            var castTarget = new SimpleDragonSupplySystemGoo();
            Assert.True(castTarget.CastFrom(supply));
            SupplySystem? restored = null;
            Assert.True(castTarget.CastTo(ref restored));
            Assert.Same(supply, restored);
        }
    }

    [Fact]
    public void EnergyRecoveryVentilatorAndPhotovoltaicPanelGoosPreserveEveryField()
    {
        var ventilator = new VentilationSystem(
            "High Performance ERV",
            0.432d,
            0.83d,
            0.61d,
            new EntityId("ERV-GOO"));
        var panel = new PhotovoltaicSystem(
            "Roof PV",
            42.5d,
            0.217d,
            187.4d,
            31.2d,
            new EntityId("PV-GOO"));

        var ventilatorGoo = new SimpleDragonEnergyRecoveryVentilatorGoo(ventilator);
        var ventilatorDuplicate = Assert.IsType<SimpleDragonEnergyRecoveryVentilatorGoo>(
            ventilatorGoo.Duplicate());
        SimpleDragonEnergyRecoveryVentilatorGoo ventilatorArchived = ArchiveRoundTrip(
            ventilatorGoo,
            new SimpleDragonEnergyRecoveryVentilatorGoo());
        AssertVentilatorEquivalent(ventilator, ventilatorDuplicate.Value);
        AssertVentilatorEquivalent(ventilator, ventilatorArchived.Value);

        var panelGoo = new SimpleDragonPhotovoltaicPanelGoo(panel);
        var panelDuplicate = Assert.IsType<SimpleDragonPhotovoltaicPanelGoo>(panelGoo.Duplicate());
        SimpleDragonPhotovoltaicPanelGoo panelArchived = ArchiveRoundTrip(
            panelGoo,
            new SimpleDragonPhotovoltaicPanelGoo());
        AssertPanelEquivalent(panel, panelDuplicate.Value);
        AssertPanelEquivalent(panel, panelArchived.Value);

        var ventilatorCast = new SimpleDragonEnergyRecoveryVentilatorGoo();
        Assert.True(ventilatorCast.CastFrom(ventilator));
        VentilationSystem? castVentilator = null;
        Assert.True(ventilatorCast.CastTo(ref castVentilator));
        Assert.Same(ventilator, castVentilator);

        var panelCast = new SimpleDragonPhotovoltaicPanelGoo();
        Assert.True(panelCast.CastFrom(panel));
        PhotovoltaicSystem? castPanel = null;
        Assert.True(panelCast.CastTo(ref castPanel));
        Assert.Same(panel, castPanel);
    }

    [Fact]
    public void NewGoosHandleNullAndInvalidInputsConsistently()
    {
        IGH_Goo[] emptyGoos =
        {
            new SimpleDragonSourceSystemGoo(),
            new SimpleDragonSupplySystemGoo(),
            new SimpleDragonEnergyRecoveryVentilatorGoo(),
            new SimpleDragonPhotovoltaicPanelGoo(),
        };

        foreach (IGH_Goo goo in emptyGoos)
        {
            Assert.False(goo.IsValid);
            Assert.Contains("contains no value", goo.IsValidWhyNot, StringComparison.Ordinal);
            Assert.Null(goo.ScriptVariable());
            Assert.StartsWith("Null SimpleDragon", goo.ToString(), StringComparison.Ordinal);
            Assert.False(goo.CastFrom(new object()));
            Assert.False(goo.Duplicate().IsValid);
        }

        SimpleDragonSourceSystemGoo archivedSource = ArchiveRoundTrip(
            new SimpleDragonSourceSystemGoo(),
            new SimpleDragonSourceSystemGoo());
        SimpleDragonSupplySystemGoo archivedSupply = ArchiveRoundTrip(
            new SimpleDragonSupplySystemGoo(),
            new SimpleDragonSupplySystemGoo());
        SimpleDragonEnergyRecoveryVentilatorGoo archivedVentilator = ArchiveRoundTrip(
            new SimpleDragonEnergyRecoveryVentilatorGoo(),
            new SimpleDragonEnergyRecoveryVentilatorGoo());
        SimpleDragonPhotovoltaicPanelGoo archivedPanel = ArchiveRoundTrip(
            new SimpleDragonPhotovoltaicPanelGoo(),
            new SimpleDragonPhotovoltaicPanelGoo());
        Assert.Null(archivedSource.Value);
        Assert.Null(archivedSupply.Value);
        Assert.Null(archivedVentilator.Value);
        Assert.Null(archivedPanel.Value);
    }

    [Fact]
    public void UnsupportedSchemaKindAndRequestedTypeAreRejectedActionably()
    {
        string snapshot = SimpleDragonGooSnapshot.Serialize(Sources()[0]);
        JsonObject wrongSchema = ParseObject(snapshot);
        wrongSchema["schema"] = "goniegonie.simple-dragon.grasshopper-goo.v99";
        InvalidDataException schemaException = Assert.Throws<InvalidDataException>(
            () => SimpleDragonGooSnapshot.Deserialize<SourceSystem>(wrongSchema.ToJsonString()));
        Assert.Contains("Unsupported SimpleDragon Grasshopper schema", schemaException.Message, StringComparison.Ordinal);

        JsonObject wrongKind = ParseObject(snapshot);
        wrongKind["kind"] = "future-source-system";
        InvalidDataException kindException = Assert.Throws<InvalidDataException>(
            () => SimpleDragonGooSnapshot.Deserialize<SourceSystem>(wrongKind.ToJsonString()));
        Assert.Contains("Unsupported SimpleDragon Grasshopper value kind", kindException.Message, StringComparison.Ordinal);

        InvalidDataException typeException = Assert.Throws<InvalidDataException>(
            () => SimpleDragonGooSnapshot.Deserialize<SupplySystem>(snapshot));
        Assert.Contains(typeof(SourceSystem).FullName!, typeException.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(SupplySystem).FullName!, typeException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingFullGreenRetrofitModelV1SnapshotRemainsReadable()
    {
        GreenRetrofitModel original = GrmReader.ReadFile(Fixture()).RequireModel();
        string snapshot = SimpleDragonGooSnapshot.Serialize(original);
        JsonObject envelope = ParseObject(snapshot);
        Assert.Equal(SimpleDragonTypeLibrary.SchemaVersion, envelope["schema"]?.GetValue<string>());
        Assert.Equal("green-retrofit-model", envelope["kind"]?.GetValue<string>());
        JsonObject payload = ParseObject(
            envelope["payload"]?.GetValue<string>()
            ?? throw new Xunit.Sdk.XunitException("The model envelope has no payload."));

        // V1 model snapshots written before canonical GRM field-presence tracking did not
        // contain these optional arrays. Missing properties must continue to deserialize.
        RemovePropertyRecursively(payload, "grmFields");
        envelope["payload"] = payload.ToJsonString();

        GreenRetrofitModel restored = SimpleDragonGooSnapshot.Deserialize<GreenRetrofitModel>(
            envelope.ToJsonString());

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Zones.Count, restored.Zones.Count);
        Assert.Equal(original.SourceSystems.Select(item => (item.Id, item.Type)),
            restored.SourceSystems.Select(item => (item.Id, item.Type)));
        Assert.Equal(original.SupplySystems.Select(item => (item.Id, item.Type, item.SourceSystemId)),
            restored.SupplySystems.Select(item => (item.Id, item.Type, item.SourceSystemId)));
        Assert.Equal(original.VentilationSystems.Select(item => item.Id),
            restored.VentilationSystems.Select(item => item.Id));
        Assert.Equal(original.PhotovoltaicSystems.Select(item => item.Id),
            restored.PhotovoltaicSystems.Select(item => item.Id));
    }

    [Fact]
    public void NewGooAndPersistentParameterTypesArePubliclyDiscoverable()
    {
        Type[] expectedGoos =
        {
            typeof(SimpleDragonSourceSystemGoo),
            typeof(SimpleDragonSupplySystemGoo),
            typeof(SimpleDragonEnergyRecoveryVentilatorGoo),
            typeof(SimpleDragonPhotovoltaicPanelGoo),
        };
        Type[] expectedParameters =
        {
            typeof(SimpleDragonSourceSystemParam),
            typeof(SimpleDragonSupplySystemParam),
            typeof(SimpleDragonEnergyRecoveryVentilatorParam),
            typeof(SimpleDragonPhotovoltaicPanelParam),
        };
        Type[] exported = typeof(SimpleDragonSourceSystemGoo).Assembly.GetExportedTypes();

        Assert.All(expectedGoos, type =>
        {
            Assert.True(type.IsPublic);
            Assert.Contains(type, exported);
        });
        Assert.All(expectedParameters, type =>
        {
            Assert.True(type.IsPublic);
            Assert.Contains(type, exported);
            Assert.IsAssignableFrom<IGH_Param>(Activator.CreateInstance(type));
        });
    }

    private static SourceSystem[] Sources()
    {
        return new[]
        {
            new SourceSystem(
                "Heat Pump",
                SourceSystemType.HeatPump,
                FuelType.Electricity,
                3.31d,
                4.42d,
                31_100d,
                42_200d,
                id: new EntityId("SOURCE-HEAT-PUMP")),
            new SourceSystem(
                "Geothermal",
                SourceSystemType.GeothermalHeatPump,
                FuelType.LiquefiedPetroleumGas,
                4.13d,
                5.24d,
                51_300d,
                62_400d,
                id: new EntityId("SOURCE-GEOTHERMAL")),
            new SourceSystem(
                "Chiller",
                SourceSystemType.Chiller,
                coolingCop: 5.15d,
                coolingCapacity: 72_001d,
                compressorType: CompressorType.Reciprocating,
                coolingTowerType: CoolingTowerType.Closed,
                coolingTowerCapacity: 91_001d,
                coolingTowerControl: CoolingTowerControl.TwoSpeed,
                id: new EntityId("SOURCE-CHILLER")),
            new SourceSystem(
                "Absorption",
                SourceSystemType.AbsorptionChiller,
                FuelType.NaturalGas,
                coolingCop: 0.87d,
                coolingCapacity: 73_005d,
                boilerEfficiency: 0.91d,
                id: new EntityId("SOURCE-ABSORPTION")),
            new SourceSystem(
                "Boiler",
                SourceSystemType.Boiler,
                FuelType.Oil,
                heatingCapacity: 81_500d,
                efficiency: 0.92d,
                hotWaterSupply: true,
                id: new EntityId("SOURCE-BOILER")),
            new SourceSystem(
                "District",
                SourceSystemType.DistrictHeating,
                heatingCapacity: 54_321d,
                hotWaterSupply: false,
                id: new EntityId("SOURCE-DISTRICT")),
        };
    }

    private static SupplySystem[] Supplies()
    {
        SourceSystem[] sources = Sources();
        SourceSystem heatPump = Assert.Single(sources, item => item.Type == SourceSystemType.HeatPump);
        SourceSystem chiller = Assert.Single(sources, item => item.Type == SourceSystemType.Chiller);
        SourceSystem boiler = Assert.Single(sources, item => item.Type == SourceSystemType.Boiler);
        SourceSystem district = Assert.Single(sources, item => item.Type == SourceSystemType.DistrictHeating);
        return new[]
        {
            new SupplySystem(
                "Packaged",
                SupplySystemType.PackagedAirConditioner,
                coolingCop: 4.73d,
                coolingCapacity: 18_100d,
                id: new EntityId("SUPPLY-PACKAGED")),
            new SupplySystem(
                "Air Handler",
                SupplySystemType.AirHandlingUnit,
                heatPump.Id.Value,
                heatPump,
                id: new EntityId("SUPPLY-AHU")),
            new SupplySystem(
                "Fan Coil",
                SupplySystemType.FanCoilUnit,
                chiller.Id.Value,
                chiller,
                id: new EntityId("SUPPLY-FAN-COIL")),
            new SupplySystem(
                "Radiator",
                SupplySystemType.Radiator,
                boiler.Id.Value,
                boiler,
                heatingCapacity: 7_400d,
                id: new EntityId("SUPPLY-RADIATOR")),
            new SupplySystem(
                "Electric Radiator",
                SupplySystemType.ElectricRadiator,
                heatingCapacity: 7_500d,
                id: new EntityId("SUPPLY-ELECTRIC-RADIATOR")),
            new SupplySystem(
                "Radiant Floor",
                SupplySystemType.RadiantFloor,
                district.Id.Value,
                district,
                id: new EntityId("SUPPLY-RADIANT-FLOOR")),
            new SupplySystem(
                "Electric Radiant Floor",
                SupplySystemType.ElectricRadiantFloor,
                id: new EntityId("SUPPLY-ELECTRIC-RADIANT-FLOOR")),
        };
    }

    private static void AssertSourceEquivalent(SourceSystem expected, SourceSystem actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.FuelType, actual.FuelType);
        Assert.Equal(expected.HeatingCop, actual.HeatingCop);
        Assert.Equal(expected.CoolingCop, actual.CoolingCop);
        Assert.Equal(expected.HeatingCapacity, actual.HeatingCapacity);
        Assert.Equal(expected.CoolingCapacity, actual.CoolingCapacity);
        Assert.Equal(expected.Efficiency, actual.Efficiency);
        Assert.Equal(expected.HotWaterSupply, actual.HotWaterSupply);
        Assert.Equal(expected.CompressorType, actual.CompressorType);
        Assert.Equal(expected.CoolingTowerType, actual.CoolingTowerType);
        Assert.Equal(expected.CoolingTowerCapacity, actual.CoolingTowerCapacity);
        Assert.Equal(expected.CoolingTowerControl, actual.CoolingTowerControl);
        Assert.Equal(expected.BoilerEfficiency, actual.BoilerEfficiency);
    }

    private static void AssertSupplyEquivalent(SupplySystem expected, SupplySystem actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.SourceSystemId, actual.SourceSystemId);
        Assert.Equal(expected.CoolingCop, actual.CoolingCop);
        Assert.Equal(expected.CoolingCapacity, actual.CoolingCapacity);
        Assert.Equal(expected.HeatingCapacity, actual.HeatingCapacity);
        Assert.Equal(expected.Heatable, actual.Heatable);
        Assert.Equal(expected.Coolable, actual.Coolable);
        if (expected.SourceSystem is null)
        {
            Assert.Null(actual.SourceSystem);
        }
        else
        {
            Assert.NotNull(actual.SourceSystem);
            Assert.NotSame(expected.SourceSystem, actual.SourceSystem);
            AssertSourceEquivalent(expected.SourceSystem, actual.SourceSystem!);
        }
    }

    private static void AssertVentilatorEquivalent(VentilationSystem expected, VentilationSystem actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.AirflowRate, actual.AirflowRate);
        Assert.Equal(expected.HeatingEfficiency, actual.HeatingEfficiency);
        Assert.Equal(expected.CoolingEfficiency, actual.CoolingEfficiency);
    }

    private static void AssertPanelEquivalent(PhotovoltaicSystem expected, PhotovoltaicSystem actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Area, actual.Area);
        Assert.Equal(expected.Efficiency, actual.Efficiency);
        Assert.Equal(expected.Azimuth, actual.Azimuth);
        Assert.Equal(expected.Tilt, actual.Tilt);
    }

    private static TGoo ArchiveRoundTrip<TGoo>(TGoo source, TGoo target)
        where TGoo : GH_IO.GH_ISerializable
    {
        var writeArchive = new GH_Archive();
        Assert.True(writeArchive.AppendObject(source, "Value"));
        byte[] bytes = writeArchive.Serialize_Binary();
        var readArchive = new GH_Archive();
        Assert.True(readArchive.Deserialize_Binary(bytes));
        Assert.True(readArchive.ExtractObject(target, "Value"));
        return target;
    }

    private static JsonObject ParseObject(string json)
    {
        return JsonNode.Parse(json) as JsonObject
            ?? throw new Xunit.Sdk.XunitException("The snapshot JSON was not an object.");
    }

    private static void RemovePropertyRecursively(JsonNode? node, string propertyName)
    {
        switch (node)
        {
            case JsonObject value:
                value.Remove(propertyName);
                foreach (KeyValuePair<string, JsonNode?> child in value.ToArray())
                {
                    RemovePropertyRecursively(child.Value, propertyName);
                }

                break;
            case JsonArray value:
                foreach (JsonNode? child in value)
                {
                    RemovePropertyRecursively(child, propertyName);
                }

                break;
        }
    }

    private static string Fixture()
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

        throw new DirectoryNotFoundException("Could not locate the SimpleDragon GRM fixture.");
    }
}
