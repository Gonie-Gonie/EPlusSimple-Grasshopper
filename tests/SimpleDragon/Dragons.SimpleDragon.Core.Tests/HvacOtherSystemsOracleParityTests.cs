#pragma warning disable CA1861 // Inline exact oracle expectations are intentionally immutable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Model;
using Dragons.UpstreamTracker;
using DragonPhotovoltaicPanel = Dragons.InvisibleDragon.Hvac.PhotovoltaicPanel;
using DragonVentilationAssignment = Dragons.InvisibleDragon.Hvac.ZoneVentilationAssignment;

namespace Dragons.SimpleDragon.Tests;

public sealed class HvacOtherSystemsOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-hvac-other-systems-oracle.json";
    private const int FixtureBytes = 72_674;
    private const string FixtureSha256 =
        "sha256:e93876b839672d4de1f5b0c205c87f1b03a894c08e391cef2170b090f2645dc4";
    private const string FixtureSchema =
        "dragons.python-reference.epsimple-hvac-other-systems.v1";
    private const string FixtureRepositoryCommit = "8e2949d";
    private const string CasesSha256 =
        "sha256:3d2d33dc4d341965a36f1af6e8b36ef072af9f9d91bb044596826099efdb2c6a";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_hvac_other_systems_oracle.py";
    private const int GeneratorBytes = 53_586;
    private const string GeneratorSha256 =
        "sha256:f749032884f2336a2d672a2a59af432859fe9d40498cf4399cb969f0cec9f277";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_hvac_other_systems_oracle.py";
    private const int ValidatorBytes = 20_003;
    private const string ValidatorSha256 =
        "sha256:5f394ab6811e6d174443278f93ec3956a07ad41eba186073b2c339baa2373db7";
    private const string SupportPath =
        "tools/python-reference/generate_epsimple_hvac_enums_base_oracle.py";
    private const int SupportBytes = 61_377;
    private const string SupportSha256 =
        "sha256:a397d3169f61a375b12a3934a2270874bfef1f3713a635cfd5e342668d12046b";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/core/hvac.py";
    private const int UpstreamBytes = 53_850;
    private const string UpstreamSourceSha256 =
        "sha256:9f3ecb27ed612baeed530ccbfd5857f1f528de24f222e6ef5093e4a635665d9c";
    private const string UpstreamAstSha256 =
        "sha256:dbbea63f51a001fae4fd73fba96dc099eab8cd5bcec39e3d9bf768e29b463873";
    private const string DependenciesSha256 =
        "sha256:85d50612b42b3818f054fd7d9cdb26a16bbf832c3afc56762ea732f55a48cb22";
    private const string RuntimeSignaturesSha256 =
        "sha256:08032fd90460d741d4b7f4b6bf5fab329f8ea195a6a03ef81b2aad976ebad6b2";
    private const string LoadedSourcesSha256 =
        "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8";
    private const string RelocatedObservationsSha256 =
        "sha256:ce5d3cd59eb175aa4fadbe2cb4cb4945a5c653f571f845c32d4ac0e0a6099f23";
    private const string TargetReceiptsSha256 =
        "sha256:c75dc2dc10c45ca2cc59300b130cc06399ec1ac07d6a138f69bebc43af70fe0f";
    private const string AdjacentReceiptsSha256 =
        "sha256:9496c1be4d58eee9816df92993a953e6c0c946a7254226cf7c52f2c80515b1a2";
    private const string NativeRoutesSha256 =
        "sha256:64ccbd587c31a0f0da4e6654d25c716be6032e5fff7ab65207c286c2de4b2a8a";
    private const string NativeSourcesSha256 =
        "sha256:c4349eab7c7f52daf11749b6e81f168ab7e2b31dc2807e8bc174c50cd0450f15";
    private const string NativeReviewSha256 =
        "sha256:0e2b93750fe52bdc2719d1d1d2dbd9d042ab503572647be88116251a9537b58d";
    private const string NativeTemplatePath =
        "fixtures/simple-dragon/grm/ASHRAE 140 modified.grm";
    private const int NativeTemplateBytes = 9_154;
    private const string NativeTemplateSha256 =
        "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.HvacOtherSystemsOracleParityTests.MatchesPinnedHvacOtherSystemsThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/OtherSystems.cs", 3_846,
            "sha256:e1aba0e081e550031cb5dfd9f83f0bc8016c89c36cc2ab1b80c7a6af35aa7714"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_641,
            "sha256:a212275276ccff153d5df42a44a46ac8877afa485e315ee27d08767a909f29bb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_646,
            "sha256:40e6e7aa1eb89cb341c7e7a32471fa029024e49b261dce8a8926514109d727ba"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_154,
            "sha256:9462f609a4a7a4e062612e4058921b0c91931dc8ff7216dbe54e258cb59ec22c"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new(
            "P01",
            "epsimple-hvac-other-systems.photovoltaic-state-validation-json-dragon",
            "photovoltaic",
            "sha256:333b0d584dd37182a2a8a1cfb273680a1bafa4d08f9f0623492f79adf15a2cad",
            "sha256:cf332ade7ae06a2da518e2904aaee751e5304bb3ec6971ffca6ee191025b1026",
            new[] { "PhotoVoltaicSystem", "PhotoVoltaicSystem.ID", "PhotoVoltaicSystem.__init__", "PhotoVoltaicSystem.area", "PhotoVoltaicSystem.azimuth", "PhotoVoltaicSystem.efficiency", "PhotoVoltaicSystem.from_json", "PhotoVoltaicSystem.tilt", "PhotoVoltaicSystem.to_dragon" }),
        new(
            "V01",
            "epsimple-hvac-other-systems.ventilation-defaults-state-validation-json-dragon",
            "ventilation",
            "sha256:ec05927e73b3fad6290ad3b35c00825f282692e97f8c0ab5b75878185dbec920",
            "sha256:bb870af9eadf5e3e1c462b471b8f00e8cf02b3d8cffc8dec233f1a24f54a92eb",
            new[] { "VentilationSystem", "VentilationSystem.ID", "VentilationSystem.__init__", "VentilationSystem.airflow_rate", "VentilationSystem.cooling_efficiency", "VentilationSystem.from_json", "VentilationSystem.heating_efficiency", "VentilationSystem.to_dragon" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        Target(283, "PhotoVoltaicSystem", "class", "epsimple-hvac-other-systems-283-5a79715b", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-5a79715b", "Dragons.SimpleDragon.PhotovoltaicSystem constructor, public immutable properties, and GrmWriter.Write(GreenRetrofitModel, bool)", 0),
        Target(284, "PhotoVoltaicSystem.ID", "function", "epsimple-hvac-other-systems-284-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.PhotovoltaicSystem.Id", 0),
        Target(287, "PhotoVoltaicSystem.__init__", "function", "epsimple-hvac-other-systems-287-b0187462", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-b0187462", "Dragons.SimpleDragon.PhotovoltaicSystem constructor, public immutable properties, and GrmWriter.Write(GreenRetrofitModel, bool)", 0),
        Target(290, "PhotoVoltaicSystem.area", "function", "epsimple-hvac-other-systems-290-aa93b96b", "equivalent", "not_applicable", "Dragons.SimpleDragon.PhotovoltaicSystem.Area", 0),
        Target(291, "PhotoVoltaicSystem.azimuth", "function", "epsimple-hvac-other-systems-291-3b2cfc1a", "equivalent", "not_applicable", "Dragons.SimpleDragon.PhotovoltaicSystem.Azimuth", 0),
        Target(292, "PhotoVoltaicSystem.efficiency", "function", "epsimple-hvac-other-systems-292-80144f2f", "equivalent", "not_applicable", "Dragons.SimpleDragon.PhotovoltaicSystem.Efficiency", 0),
        Target(293, "PhotoVoltaicSystem.from_json", "function", "epsimple-hvac-other-systems-293-1571f37e", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-1571f37e", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) photovoltaic-system dispatch", 0),
        Target(294, "PhotoVoltaicSystem.tilt", "function", "epsimple-hvac-other-systems-294-abeb16e6", "equivalent", "not_applicable", "Dragons.SimpleDragon.PhotovoltaicSystem.Tilt", 0),
        Target(295, "PhotoVoltaicSystem.to_dragon", "function", "epsimple-hvac-other-systems-295-6f67da14", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-6f67da14", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 0),
        Target(325, "VentilationSystem", "class", "epsimple-hvac-other-systems-325-b4f22735", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-b4f22735", "Dragons.SimpleDragon.VentilationSystem constructor, public immutable properties, and GrmWriter.Write(GreenRetrofitModel, bool)", 1),
        Target(326, "VentilationSystem.ID", "function", "epsimple-hvac-other-systems-326-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.VentilationSystem.Id", 1),
        Target(329, "VentilationSystem.__init__", "function", "epsimple-hvac-other-systems-329-7d9d5173", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-7d9d5173", "Dragons.SimpleDragon.VentilationSystem constructor, public immutable properties, and GrmWriter.Write(GreenRetrofitModel, bool)", 1),
        Target(332, "VentilationSystem.airflow_rate", "function", "epsimple-hvac-other-systems-332-b19eca15", "equivalent", "not_applicable", "Dragons.SimpleDragon.VentilationSystem.AirflowRate", 1),
        Target(333, "VentilationSystem.cooling_efficiency", "function", "epsimple-hvac-other-systems-333-83943137", "equivalent", "not_applicable", "Dragons.SimpleDragon.VentilationSystem.CoolingEfficiency", 1),
        Target(334, "VentilationSystem.from_json", "function", "epsimple-hvac-other-systems-334-acaa4faa", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-acaa4faa", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) ventilation-system dispatch", 1),
        Target(335, "VentilationSystem.heating_efficiency", "function", "epsimple-hvac-other-systems-335-76edd9cd", "equivalent", "not_applicable", "Dragons.SimpleDragon.VentilationSystem.HeatingEfficiency", 1),
        Target(336, "VentilationSystem.to_dragon", "function", "epsimple-hvac-other-systems-336-fdc1293c", "exception", "reviewed-native-immutable-other-system-and-aggregate-route-fdc1293c", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 1),
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(44, "sha256:5aff29784a71a5dbada9287bab529fb391885dc70cb4595f1866cbb56b95dba3"),
        new(45, "sha256:0762960e072b8e18676da65009cbb65a84789e9fc26de98fd0ad3cbd66ea4c6b"),
    };
    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:b97c5c8df8b532fba1383ad254fcf294717e8e826f8b06a6f1d9a9d9527d371f",
        "sha256:98d81a6d74c34ba73b13dd937e6b57b14a620469aaa851339ed6a4014b5e46d6",
        "sha256:3587aecf6791a2b80d78e6209a9820509bb75e0380f22207b1c28517c715dfbe",
        "sha256:4908d89ea8b380091b2b481b4b5371b41a71cc0e4eda18ae9e1ac7f96a7e65ac",
        "sha256:40a23c675e85572c0f5ea264470da1e3f2cdba5e472d28b3893d899180274ae6",
        "sha256:de322ec9a28cb585e852f8c3d5cd8461e860d866c4df97e5e46c5fb2394ef7c7",
        "sha256:8a682307e0f48950f3ee9e638a951d9f5fb3b9710f9a2e2268121de677f2c09a",
        "sha256:b6eb714eb3c1773b26bccb7c7ddf5b6bf1f38750ea46f109e17706d46ece2ca9",
        "sha256:c0dc2270d60f224ab154c28d6ec89cb79afabfa9d33f526571ecd9713242fde9",
        "sha256:427aa34491dedb8d4486f2664e0fe8e8ae4d4c97ee1ec64ed0d92f78b976025e",
        "sha256:9fb162109081e980a584d862d038d02c83a3616e52ba84704ac6cd987d47c570",
        "sha256:46c744c129f09f2d48375f5b129a598058fcec911fde87330cd1142bd44b6239",
        "sha256:fab7c56d23b4fd75b6d401ed3686feafb58900cacabcdf672f95c8d1cbe3bb48",
        "sha256:3fc2db90a9d1fec231862b31ea1e0214d49b6fae047b9cc4a6ab780dbd34c33e",
        "sha256:b1009fb32719ef4068198c4ae742555483e27de840b7d388f8304981afe6f02c",
        "sha256:09f3c411e20b80d381c4f08360de4f283c8c50cdf888680370ada200a4dc49f1",
        "sha256:2d69156f20b26e852ed0e2d8a29f79fe78f996ff62cabefa69ce1fa6282c471e",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:2fa8216fb3e3b3dae598a719719b875fa60b978dd865e0c9def1e6326f6163fe", // epsimple-hvac-other-systems-283-5a79715b
        "sha256:8f07a6883b0146007480621d8e2dd3c4c43483f4995094307ec915c26563068b", // epsimple-hvac-other-systems-284-246156d9
        "sha256:967fb9fca82a4d012b6d119bb371937d640dd2e12a0e9361a7fb8025a19b9a92", // epsimple-hvac-other-systems-287-b0187462
        "sha256:7e99e73ac3e413c49fdb2f5342965dd032c59adbb00785d74b949f0e6f2d2360", // epsimple-hvac-other-systems-290-aa93b96b
        "sha256:62f9118105bbfa9cf9efabd78e725c48185b20017e154f6ef2253f87b8dd02d2", // epsimple-hvac-other-systems-291-3b2cfc1a
        "sha256:6377e88bbd083da5cdcc15b7278acb6f6ec9c774e6f14bd388986f04f4a4b5ad", // epsimple-hvac-other-systems-292-80144f2f
        "sha256:0097950aaf3d7c099a626ccafb944a8b8b47325f753a900f2dcaa461f6eabf09", // epsimple-hvac-other-systems-293-1571f37e
        "sha256:dabcb99f2a2b0cf6921bb5fabcddbd1ea6cb2776f0fd76f370af972741738e1a", // epsimple-hvac-other-systems-294-abeb16e6
        "sha256:1796db3f186f4f384170805e64c6851c6ca24279140a9a7e1beb99f42a2ae9c1", // epsimple-hvac-other-systems-295-6f67da14
        "sha256:bf2a0499870e378f48de6a8e5c7632ae69a535dea4c29d13c2197659ffa0a562", // epsimple-hvac-other-systems-325-b4f22735
        "sha256:1326ffb6111fc727b595016e5cc62d647795b53ec364d2c425a889fa211611ce", // epsimple-hvac-other-systems-326-246156d9
        "sha256:a56ac8f9563781429b719f1cb95250e6024070c41b522d9eb48f428196d4f3b8", // epsimple-hvac-other-systems-329-7d9d5173
        "sha256:77d0c9e47a25b2edff8e131eff19385256c79c704c7be48baf24df72b847ea1c", // epsimple-hvac-other-systems-332-b19eca15
        "sha256:8fd810c9ffc411e7e9c179113728a7e6aa85651fa0d4f63635bc91f4f652cc7a", // epsimple-hvac-other-systems-333-83943137
        "sha256:dd90d191cb351fe7964935950f28ac6e9cd9d52ab429630176eb70b46964787b", // epsimple-hvac-other-systems-334-acaa4faa
        "sha256:ac4a22c788929ce36cd91c2a2a813044b5fb537406c065848675018189e7281b", // epsimple-hvac-other-systems-335-76edd9cd
        "sha256:7764b2cdfc3bdaa41a338b32b20d30a80847a80d886ca21becdda3311e6f43a0", // epsimple-hvac-other-systems-336-fdc1293c
    };

    [Fact]
    public void MatchesPinnedHvacOtherSystemsThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(Cases.Select(item => item.Code), observations.Select(item => item.Code));

        object[] receipts = corpus.Targets
            .Select(target => CreateReceipt(target, observations))
            .ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();
        string[] collectorOutputHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(new
            {
                cases = new[]
                {
                    new
                    {
                        output = receipt,
                        test_case = EvidenceTestCase,
                    },
                },
            })))
            .ToArray();

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "HVAC_OTHER_SYSTEMS_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    cases = observations.Select(item => new
                    {
                        item.Code,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipts = corpus.Targets.Select((item, index) => new
                    {
                        item.Symbol,
                        item.AssertionId,
                        receipt_sha256 = receiptHashes[index],
                    }),
                }, DiscoveryJsonOptions));
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }

        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
        Assert.Equal(ExpectedCollectorOutputHashes, collectorOutputHashes);
        int recordCount = 0;
        for (int index = 0; index < corpus.Targets.Length; index++)
        {
            JsonElement receipt = JsonSerializer.SerializeToElement(receipts[index]);
            ValidateReceipt(receipt, corpus.Targets[index], observations);
            TrustedEvidenceRecorder.Record(
                corpus.Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
            recordCount++;
        }

        Assert.Equal(17, recordCount);
        Assert.Equal(17, corpus.Targets.Length);
        Assert.Equal(17, corpus.Targets.Select(item => item.AssertionId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(9, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(8, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.Equal(2, corpus.FixtureCases.Length);
        Assert.Equal(185, corpus.AdjacentIndices.Length);
    }

    private static ExpectedTargetBinding Target(
        int inventoryIndex,
        string symbol,
        string kind,
        string assertionId,
        string classification,
        string adaptationId,
        string nativeRoute,
        int caseIndex) => new(
            inventoryIndex,
            symbol,
            kind,
            assertionId,
            classification,
            adaptationId,
            nativeRoute,
            caseIndex);

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(SupportPath, SupportBytes, SupportSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertPinnedArtifact(NativeTemplatePath, NativeTemplateBytes, NativeTemplateSha256);
        foreach (ArtifactPin source in NativeSources)
        {
            AssertPinnedArtifact(source.Path, source.Bytes, source.Sha256);
        }

        Assert.True(typeof(PhotovoltaicSystem).IsSealed);
        Assert.Single(typeof(PhotovoltaicSystem).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertReadOnlyProperty<PhotovoltaicSystem>(nameof(PhotovoltaicSystem.Id), typeof(EntityId));
        AssertReadOnlyProperty<PhotovoltaicSystem>(nameof(PhotovoltaicSystem.Name), typeof(string));
        AssertReadOnlyProperty<PhotovoltaicSystem>(nameof(PhotovoltaicSystem.Area), typeof(double));
        AssertReadOnlyProperty<PhotovoltaicSystem>(nameof(PhotovoltaicSystem.Efficiency), typeof(double));
        AssertReadOnlyProperty<PhotovoltaicSystem>(nameof(PhotovoltaicSystem.Azimuth), typeof(double));
        AssertReadOnlyProperty<PhotovoltaicSystem>(nameof(PhotovoltaicSystem.Tilt), typeof(double));

        Assert.True(typeof(VentilationSystem).IsSealed);
        Assert.Single(typeof(VentilationSystem).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertReadOnlyProperty<VentilationSystem>(nameof(VentilationSystem.Id), typeof(EntityId));
        AssertReadOnlyProperty<VentilationSystem>(nameof(VentilationSystem.Name), typeof(string));
        AssertReadOnlyProperty<VentilationSystem>(nameof(VentilationSystem.AirflowRate), typeof(double));
        AssertReadOnlyProperty<VentilationSystem>(nameof(VentilationSystem.HeatingEfficiency), typeof(double));
        AssertReadOnlyProperty<VentilationSystem>(nameof(VentilationSystem.CoolingEfficiency), typeof(double));

        Assert.NotNull(typeof(GrmReader).GetMethod(
            nameof(GrmReader.Read),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(SimpleDragonDatabase) },
            modifiers: null));
        Assert.NotNull(typeof(GrmWriter).GetMethod(
            nameof(GrmWriter.Serialize),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(GreenRetrofitModel), typeof(bool) },
            modifiers: null));
        Assert.NotNull(typeof(GreenRetrofitConverter).GetMethod(
            nameof(GreenRetrofitConverter.Convert),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(GreenRetrofitModel), typeof(GreenRetrofitConversionOptions) },
            modifiers: null));
    }

    private static void AssertReadOnlyProperty<T>(string name, Type expectedType)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(typeof(T).GetProperty(name));
        Assert.Equal(expectedType, property.PropertyType);
        Assert.False(property.CanWrite);
        Assert.True(property.GetMethod!.IsPublic);
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(FixturePath));
        Assert.Equal(FixtureBytes, bytes.Length);
        Assert.Equal(FixtureSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        string text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
        byte[] tampered = (byte[])bytes.Clone();
        tampered[64] ^= 1;
        Assert.NotEqual(FixtureSha256, Sha256(tampered));
        using JsonDocument duplicate = JsonDocument.Parse("{\"value\":1,\"value\":2}");
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => AssertUniqueObjectKeysRecursive(duplicate.RootElement));
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 128,
        });
    }

    private static OracleCorpus ValidateOracle(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoUnsafeIdentity(root);
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(
            root,
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "fact_sha256",
            "native_review",
            "runtime",
            "schema",
            "symbols",
            "target_receipts",
            "upstream");
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateUpstream(root.GetProperty("upstream"));
        ValidateNativeReview(root.GetProperty("native_review"));

        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        AssertKeys(root.GetProperty("case_sha256"), Cases.Select(item => item.CaseId).ToArray());
        AssertKeys(root.GetProperty("fact_sha256"), Cases.Select(item => item.CaseId).ToArray());
        for (int index = 0; index < fixtureCases.Length; index++)
        {
            ValidateCase(
                fixtureCases[index],
                Cases[index],
                root.GetProperty("case_sha256"),
                root.GetProperty("fact_sha256"));
        }

        (TargetBinding[] targets, int[] adjacentIndices) = ValidateTargets(root);
        ValidateConsumerContract(root.GetProperty("consumer_contract"), targets, adjacentIndices);
        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol),
            fixtureCases.SelectMany(item => ReadStringArray(item.GetProperty("target_symbols"))));
        return new OracleCorpus(fixtureCases, targets, adjacentIndices);
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "bootstrap",
            "dependencies",
            "dependencies_sha256",
            "implementation",
            "other_systems_support",
            "platform",
            "pointer_width_bits",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version",
            "strict_json_support");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("win32", RequiredString(runtime, "platform"));
        Assert.Equal(64, runtime.GetProperty("pointer_width_bits").GetInt32());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(DependenciesSha256, RequiredString(runtime, "dependencies_sha256"));
        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(dependencies, "eppy", "numpy", "pandas", "python-dateutil", "pytz", "six", "tzdata");
        Assert.Equal(DependenciesSha256, CanonicalSha256(dependencies));
        AssertArtifact(runtime.GetProperty("bootstrap"),
            "tools/python-reference/bootstrap_reference.py", 1_232,
            "sha256:0674dcf1fe966de2a4b873a360ef67be48d74f38ba80adba9c74405fd9be7e0f");
        AssertArtifact(runtime.GetProperty("strict_json_support"),
            "tools/python-reference/generate_schedule_type_oracle.py", 21_108,
            "sha256:555a1df41e5369dbbc44b0729a48673610a86951a215c8e2aa00cfa4fce156f1");
        AssertArtifact(runtime.GetProperty("other_systems_support"),
            SupportPath, SupportBytes, SupportSha256);
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(
            upstream,
            "adjacent_receipts_sha256",
            "commit",
            "inventory",
            "isolated_import",
            "source",
            "target_receipts_sha256");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(TargetReceiptsSha256, RequiredString(upstream, "target_receipts_sha256"));
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(upstream, "adjacent_receipts_sha256"));

        JsonElement inventory = upstream.GetProperty("inventory");
        AssertKeys(inventory, "bytes", "content_sha256", "file_sha256");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));

        JsonElement source = upstream.GetProperty("source");
        AssertKeys(source, "ast_sha256", "bytes", "path", "source_sha256");
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement isolated = upstream.GetProperty("isolated_import");
        AssertKeys(
            isolated,
            "epsimple_core_initializer_executed",
            "epsimple_package_initializer_executed",
            "loaded_local_modules",
            "loaded_local_modules_sha256",
            "relocated_observations_sha256",
            "relocated_source_copy",
            "source_location_count");
        Assert.False(isolated.GetProperty("epsimple_package_initializer_executed").GetBoolean());
        Assert.False(isolated.GetProperty("epsimple_core_initializer_executed").GetBoolean());
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal("byte-identical-epsimple-and-idragon-trees",
            RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(LoadedSourcesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(RelocatedObservationsSha256,
            RequiredString(isolated, "relocated_observations_sha256"));
        JsonElement modules = isolated.GetProperty("loaded_local_modules");
        Assert.Equal(16, modules.GetArrayLength());
        Assert.Equal(LoadedSourcesSha256, CanonicalSha256(modules));
    }

    private static void ValidateNativeReview(JsonElement review)
    {
        AssertKeys(
            review,
            "public_production_routes_only",
            "python_executes_native_runtime",
            "reviewed_semantics",
            "routes_sha256",
            "source_receipts",
            "source_receipts_sha256");
        Assert.True(review.GetProperty("public_production_routes_only").GetBoolean());
        Assert.False(review.GetProperty("python_executes_native_runtime").GetBoolean());
        Assert.Equal(NativeRoutesSha256, RequiredString(review, "routes_sha256"));
        Assert.Equal(NativeSourcesSha256, RequiredString(review, "source_receipts_sha256"));
        AssertArtifactArray(review.GetProperty("source_receipts"), NativeSources);
        Assert.Equal(NativeSourcesSha256, CanonicalSha256(review.GetProperty("source_receipts")));
        JsonElement semantics = review.GetProperty("reviewed_semantics");
        AssertKeys(
            semantics,
            "native_auto_ids_are_deterministic",
            "native_models_are_immutable",
            "native_rejects_blank_names_and_nonfinite_numbers",
            "native_ventilation_conversion_preserves_airflow_and_assignment_count",
            "python_auto_ids_use_process_identity",
            "python_models_are_mutable",
            "python_nonfinite_range_behavior_is_observed_not_normalized",
            "python_ventilation_to_dragon_omits_airflow");
        Assert.All(semantics.EnumerateObject(), item => Assert.True(item.Value.GetBoolean()));
        Assert.Equal(NativeReviewSha256, CanonicalSha256(review));
    }

    private static void ValidateCase(
        JsonElement item,
        CaseBinding expected,
        JsonElement caseHashes,
        JsonElement factHashes)
    {
        AssertKeys(item, "code", "context_symbols", "id", "python", "subfamily", "target_symbols");
        Assert.Equal(expected.Code, RequiredString(item, "code"));
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Subfamily, RequiredString(item, "subfamily"));
        AssertStringArray(item.GetProperty("target_symbols"), expected.TargetSymbols);
        Assert.Empty(item.GetProperty("context_symbols").EnumerateArray());
        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "facts_sha256", "outcome");
        Assert.Equal("observed", RequiredString(python, "outcome"));
        Assert.Equal(expected.FactsSha256, RequiredString(python, "facts_sha256"));
        Assert.Equal(expected.FactsSha256, RequiredString(factHashes, expected.CaseId));
        Assert.Equal(expected.FactsSha256, CanonicalSha256(python.GetProperty("facts")));
        Assert.Equal(expected.CaseSha256, RequiredString(caseHashes, expected.CaseId));
        Assert.Equal(expected.CaseSha256, CanonicalSha256(item));
    }

    private static (TargetBinding[] Targets, int[] AdjacentIndices) ValidateTargets(JsonElement root)
    {
        JsonElement[] descriptors = root.GetProperty("symbols").EnumerateArray().ToArray();
        JsonElement[] receipts = root.GetProperty("target_receipts").EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, descriptors.Length);
        Assert.Equal(ExpectedTargets.Length, receipts.Length);
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(root.GetProperty("target_receipts")));

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        Assert.Equal(InventoryBytes, inventoryBytes.Length);
        Assert.Equal(InventoryFileSha256, Sha256(inventoryBytes));
        using JsonDocument inventoryDocument = JsonDocument.Parse(inventoryBytes);
        AssertUniqueObjectKeysRecursive(inventoryDocument.RootElement);
        Assert.Equal(InventoryContentSha256,
            RequiredString(inventoryDocument.RootElement, "content_sha256"));
        Assert.Equal(UpstreamCommit,
            RequiredString(inventoryDocument.RootElement, "upstream_commit"));
        JsonElement inventorySymbols = inventoryDocument.RootElement.GetProperty("symbols");

        var targets = new TargetBinding[ExpectedTargets.Length];
        for (int index = 0; index < ExpectedTargets.Length; index++)
        {
            ExpectedTargetBinding expected = ExpectedTargets[index];
            JsonElement descriptor = descriptors[index];
            JsonElement receipt = receipts[index];
            JsonElement inventorySymbol = inventorySymbols[expected.InventoryIndex];
            AssertTargetProjection(descriptor, expected, includeIndex: false);
            AssertTargetProjection(receipt, expected, includeIndex: true);
            AssertTargetProjection(inventorySymbol, expected, includeIndex: false, requireExactKeys: false);
            foreach (string hashName in new[] { "symbol_hash", "signature_hash", "body_hash" })
            {
                Assert.Equal(RequiredString(inventorySymbol, hashName), RequiredString(receipt, hashName));
                Assert.Equal(RequiredString(receipt, hashName), RequiredString(descriptor, hashName));
            }

            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                expected.Kind,
                RequiredString(receipt, "symbol_hash"),
                RequiredString(receipt, "signature_hash"),
                RequiredString(receipt, "body_hash"),
                expected.AssertionId,
                expected.Classification,
                expected.AdaptationId,
                expected.NativeRoute,
                expected.CaseIndex);
        }

        int[] sourceIndices = inventorySymbols.EnumerateArray()
            .Select((item, index) => (item, index))
            .Where(pair => RequiredString(pair.item, "path") == UpstreamPath)
            .Select(pair => pair.index)
            .ToArray();
        Assert.Equal(Enumerable.Range(135, 202), sourceIndices);
        int[] targetIndices = targets.Select(item => item.InventoryIndex).ToArray();
        int[] adjacentIndices = sourceIndices.Except(targetIndices).ToArray();
        Assert.Equal(185, adjacentIndices.Length);
        object[] adjacentReceipts = adjacentIndices
            .Select(index => InventoryReceipt(inventorySymbols[index], index))
            .ToArray();
        Assert.Equal(
            AdjacentReceiptsSha256,
            CanonicalSha256(JsonSerializer.SerializeToElement(adjacentReceipts)));
        return (targets, adjacentIndices);
    }

    private static object InventoryReceipt(JsonElement symbol, int index) => new
    {
        body_hash = RequiredString(symbol, "body_hash"),
        inventory_index = index,
        kind = RequiredString(symbol, "kind"),
        path = RequiredString(symbol, "path"),
        signature_hash = RequiredString(symbol, "signature_hash"),
        symbol = RequiredString(symbol, "symbol"),
        symbol_hash = RequiredString(symbol, "symbol_hash"),
    };

    private static void AssertTargetProjection(
        JsonElement item,
        ExpectedTargetBinding expected,
        bool includeIndex,
        bool requireExactKeys = true)
    {
        if (requireExactKeys)
        {
            AssertKeys(
                item,
                includeIndex
                    ? new[] { "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash" }
                    : new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" });
        }

        if (includeIndex)
        {
            Assert.Equal(expected.InventoryIndex, item.GetProperty("inventory_index").GetInt32());
        }

        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
        Assert.Equal(expected.Kind, RequiredString(item, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(item, "path"));
        AssertSha256(RequiredString(item, "symbol_hash"));
        AssertSha256(RequiredString(item, "signature_hash"));
        AssertSha256(RequiredString(item, "body_hash"));
    }

    private static void ValidateConsumerContract(
        JsonElement contract,
        IReadOnlyList<TargetBinding> targets,
        IReadOnlyList<int> adjacentIndices)
    {
        AssertKeys(
            contract,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classification_counts",
            "classifications",
            "closure",
            "coverage_by_symbol",
            "evidence_contract",
            "expectations",
            "native_routes",
            "runtime_signatures");
        Assert.Equal(2, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(9, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(8, counts.GetProperty("exception").GetInt32());

        string[] targetSymbols = ExpectedTargets.Select(item => item.Symbol).ToArray();
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement routes = contract.GetProperty("native_routes");
        JsonElement expectations = contract.GetProperty("expectations");
        JsonElement signatures = contract.GetProperty("runtime_signatures");
        JsonElement coverage = contract.GetProperty("coverage_by_symbol");
        AssertKeys(assertions, targetSymbols);
        AssertKeys(classifications, targetSymbols);
        AssertKeys(routes, targetSymbols);
        AssertKeys(expectations, targetSymbols);
        AssertKeys(signatures, targetSymbols);
        AssertKeys(coverage, targetSymbols);
        Assert.Equal(RuntimeSignaturesSha256, CanonicalSha256(signatures));
        Assert.Equal(NativeRoutesSha256, CanonicalSha256(routes));
        string[] exceptionSymbols = ExpectedTargets
            .Where(item => item.Classification == "exception")
            .Select(item => item.Symbol)
            .ToArray();
        AssertKeys(adaptations, exceptionSymbols);
        foreach (TargetBinding target in targets)
        {
            Assert.Equal(target.AssertionId, RequiredString(assertions, target.Symbol));
            Assert.Equal(target.Classification, RequiredString(classifications, target.Symbol));
            Assert.Equal(target.NativeRoute, RequiredString(routes, target.Symbol));
            Assert.Equal(Cases[target.CaseIndex].CaseId, RequiredString(coverage, target.Symbol));
            JsonElement expectation = expectations.GetProperty(target.Symbol);
            AssertKeys(expectation, "adaptation", "assertion_id", "classification", "native_route");
            Assert.Equal(target.AdaptationId, RequiredString(expectation, "adaptation"));
            Assert.Equal(target.AssertionId, RequiredString(expectation, "assertion_id"));
            Assert.Equal(target.Classification, RequiredString(expectation, "classification"));
            Assert.Equal(target.NativeRoute, RequiredString(expectation, "native_route"));
            if (target.Classification == "exception")
            {
                Assert.Equal(target.AdaptationId, RequiredString(adaptations, target.Symbol));
            }

            Assert.Contains("Dragons.SimpleDragon", target.NativeRoute, StringComparison.Ordinal);
            Assert.DoesNotContain(".Internal", target.NativeRoute, StringComparison.Ordinal);
            Assert.DoesNotContain("GrmVocabulary", target.NativeRoute, StringComparison.Ordinal);
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "adjacent_count",
            "adjacent_indices",
            "exact_one_case_target_partition",
            "full_hvac_source_partition",
            "source_declaration_count",
            "target_count",
            "target_indices",
            "target_symbols");
        Assert.Equal(185, closure.GetProperty("adjacent_count").GetInt32());
        Assert.Equal(adjacentIndices, ReadIntArray(closure.GetProperty("adjacent_indices")));
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_hvac_source_partition").GetBoolean());
        Assert.Equal(202, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(17, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex),
            ReadIntArray(closure.GetProperty("target_indices")));
        AssertStringArray(closure.GetProperty("target_symbols"), targetSymbols);
        Assert.Equal(
            Enumerable.Range(135, 202),
            ExpectedTargets.Select(item => item.InventoryIndex)
                .Concat(adjacentIndices)
                .OrderBy(item => item));

        JsonElement evidence = contract.GetProperty("evidence_contract");
        AssertKeys(
            evidence,
            "active_energyplus_process_claim",
            "adjacent_behavior_promoted",
            "exact_cpython_behavior_oracle",
            "expected_receipt_count",
            "native_runtime_executed_by_python_oracle",
            "path_independent_relocated_import",
            "target_coverage_complete");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.False(evidence.GetProperty("adjacent_behavior_promoted").GetBoolean());
        Assert.True(evidence.GetProperty("exact_cpython_behavior_oracle").GetBoolean());
        Assert.Equal(17, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
    }

    private static NativeObservation ObserveNativeCase(int index)
    {
        string[] facts = index switch
        {
            0 => ObservePhotovoltaic(),
            1 => ObserveVentilation(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return new NativeObservation(
            Cases[index].Code,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObservePhotovoltaic()
    {
        var panel = new PhotovoltaicSystem(
            "Roof PV", 24d, 0.2d, 180d, 30d, Id("PV-EXPLICIT"));
        var automaticFirst = new PhotovoltaicSystem("Auto PV", 12d, 0.18d, 90d, 20d);
        var automaticSecond = new PhotovoltaicSystem("Auto PV", 12d, 0.18d, 90d, 20d);
        var automaticDifferent = new PhotovoltaicSystem("Auto PV", 13d, 0.18d, 90d, 20d);
        var minimumArea = new PhotovoltaicSystem(
            "Minimum PV", double.Epsilon, 1d, 0d, 90d, Id("PV-BOUNDARY"));
        OtherSystemsProbe probe = RoundTripAndConvert(panel, ventilation: null, ventilationCount: 0);
        PhotovoltaicSystem reread = Assert.Single(probe.RereadModel.PhotovoltaicSystems);
        DragonPhotovoltaicPanel converted = Assert.Single(probe.FirstModel.PhotovoltaicPanels);
        DragonPhotovoltaicPanel convertedAgain = Assert.Single(probe.SecondModel.PhotovoltaicPanels);

        string withoutArea = ReplaceRequired(probe.Json, "\"area\":24,", string.Empty);
        GrmReadResult missingArea = GrmReader.Read(withoutArea, SimpleDragonDatabase.Default);

        return new[]
        {
            "native-type=PhotovoltaicSystem",
            "native-sealed=" + Boolean(typeof(PhotovoltaicSystem).IsSealed),
            "native-public-constructor-count=" + typeof(PhotovoltaicSystem).GetConstructors().Length.ToString(CultureInfo.InvariantCulture),
            "native-writable-property-count=" + WritablePropertyCount<PhotovoltaicSystem>().ToString(CultureInfo.InvariantCulture),
            "id=" + panel.Id.Value,
            "name=" + panel.Name,
            "area=" + Double(panel.Area),
            "efficiency=" + Double(panel.Efficiency),
            "azimuth=" + Double(panel.Azimuth),
            "tilt=" + Double(panel.Tilt),
            "auto-id-prefix=" + Boolean(automaticFirst.Id.Value.StartsWith("PVPN-", StringComparison.Ordinal)),
            "auto-id-repeat=" + Boolean(automaticFirst.Id == automaticSecond.Id),
            "auto-id-input-sensitive=" + Boolean(automaticFirst.Id != automaticDifferent.Id),
            "boundary-area=" + Double(minimumArea.Area),
            "boundary-efficiency-one=" + Double(minimumArea.Efficiency),
            "boundary-azimuth-zero=" + Double(minimumArea.Azimuth),
            "boundary-tilt-ninety=" + Double(minimumArea.Tilt),
            "blank-name=" + ExceptionFact(() => _ = new PhotovoltaicSystem(" ", 1d, 0.2d, 0d, 0d)),
            "area-zero=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 0d, 0.2d, 0d, 0d)),
            "area-nan=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", double.NaN, 0.2d, 0d, 0d)),
            "area-infinity=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", double.PositiveInfinity, 0.2d, 0d, 0d)),
            "efficiency-zero=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, 0d, 0d, 0d)),
            "efficiency-over-one=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, Math.BitIncrement(1d), 0d, 0d)),
            "efficiency-nan=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, double.NaN, 0d, 0d)),
            "azimuth-negative=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, 0.2d, -double.Epsilon, 0d)),
            "azimuth-360=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, 0.2d, 360d, 0d)),
            "azimuth-infinity=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, 0.2d, double.PositiveInfinity, 0d)),
            "tilt-negative=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, 0.2d, 0d, -double.Epsilon)),
            "tilt-over-ninety=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, 0.2d, 0d, Math.BitIncrement(90d))),
            "tilt-nan=" + ExceptionFact(() => _ = new PhotovoltaicSystem("PV", 1d, 0.2d, 0d, double.NaN)),
            "grm-success=" + Boolean(probe.ReadSuccess),
            "grm-repeat=" + Boolean(probe.WriterRepeatEqual),
            "grm-reread-id=" + reread.Id.Value,
            "grm-reread-state=" + Join(new[] { Double(reread.Area), Double(reread.Efficiency), Double(reread.Azimuth), Double(reread.Tilt) }),
            "grm-missing-area-success=" + Boolean(missingArea.Success),
            "grm-missing-area-diagnostics=" + DiagnosticCodes(missingArea),
            "conversion-success=" + Boolean(probe.FirstSuccess),
            "conversion-id=" + converted.Id.Value,
            "conversion-name=" + converted.Name,
            "conversion-state=" + Join(new[] { Double(converted.AreaSquareMetres), Double(converted.Efficiency), Double(converted.AzimuthDegrees), Double(converted.TiltDegrees) }),
            "conversion-active-cell-fraction=" + Double(converted.ActiveCellAreaFraction),
            "conversion-fresh=" + Boolean(!ReferenceEquals(converted, convertedAgain)),
            "conversion-pv-count=" + probe.FirstModel.PhotovoltaicPanels.Count.ToString(CultureInfo.InvariantCulture),
            "conversion-ventilation-count=" + probe.FirstModel.VentilationAssignments.Count.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static string[] ObserveVentilation()
    {
        var ventilation = new VentilationSystem(
            "Explicit ERV", 0.5d, 0.8d, 0.55d, Id("ERV-EXPLICIT"));
        var defaults = new VentilationSystem(
            "Default ERV", 0.4d, id: Id("ERV-DEFAULT"));
        var automaticFirst = new VentilationSystem("Auto ERV", 0.3d);
        var automaticSecond = new VentilationSystem("Auto ERV", 0.3d);
        var automaticDifferent = new VentilationSystem("Auto ERV", 0.31d);
        var boundaries = new VentilationSystem(
            "Boundary ERV",
            double.Epsilon,
            double.Epsilon,
            Math.BitDecrement(1d),
            Id("ERV-BOUNDARY"));
        OtherSystemsProbe probe = RoundTripAndConvert(
            photovoltaic: null, ventilation, ventilationCount: 2);
        VentilationSystem reread = Assert.Single(probe.RereadModel.VentilationSystems);
        DragonVentilationAssignment converted = Assert.Single(probe.FirstModel.VentilationAssignments);
        DragonVentilationAssignment convertedAgain = Assert.Single(probe.SecondModel.VentilationAssignments);

        OtherSystemsProbe defaultProbe = RoundTripAndConvert(
            photovoltaic: null, defaults, ventilationCount: 1);
        string defaultsOmitted = ReplaceRequired(
            defaultProbe.Json,
            ",\"efficiency_heating\":0.7,\"efficiency_cooling\":0.45",
            string.Empty);
        GrmReadResult defaultRead = GrmReader.Read(defaultsOmitted, SimpleDragonDatabase.Default);
        Assert.True(defaultRead.Success, Describe(defaultRead.Diagnostics));
        VentilationSystem defaultReread = Assert.Single(defaultRead.RequireModel().VentilationSystems);

        return new[]
        {
            "native-type=VentilationSystem",
            "native-sealed=" + Boolean(typeof(VentilationSystem).IsSealed),
            "native-public-constructor-count=" + typeof(VentilationSystem).GetConstructors().Length.ToString(CultureInfo.InvariantCulture),
            "native-writable-property-count=" + WritablePropertyCount<VentilationSystem>().ToString(CultureInfo.InvariantCulture),
            "id=" + ventilation.Id.Value,
            "name=" + ventilation.Name,
            "airflow=" + Double(ventilation.AirflowRate),
            "heating-efficiency=" + Double(ventilation.HeatingEfficiency),
            "cooling-efficiency=" + Double(ventilation.CoolingEfficiency),
            "default-heating-efficiency=" + Double(defaults.HeatingEfficiency),
            "default-cooling-efficiency=" + Double(defaults.CoolingEfficiency),
            "auto-id-prefix=" + Boolean(automaticFirst.Id.Value.StartsWith("ERVT-", StringComparison.Ordinal)),
            "auto-id-repeat=" + Boolean(automaticFirst.Id == automaticSecond.Id),
            "auto-id-input-sensitive=" + Boolean(automaticFirst.Id != automaticDifferent.Id),
            "boundary-airflow=" + Double(boundaries.AirflowRate),
            "boundary-heating=" + Double(boundaries.HeatingEfficiency),
            "boundary-cooling=" + Double(boundaries.CoolingEfficiency),
            "blank-name=" + ExceptionFact(() => _ = new VentilationSystem("", 0.5d)),
            "airflow-zero=" + ExceptionFact(() => _ = new VentilationSystem("ERV", 0d)),
            "airflow-nan=" + ExceptionFact(() => _ = new VentilationSystem("ERV", double.NaN)),
            "airflow-infinity=" + ExceptionFact(() => _ = new VentilationSystem("ERV", double.PositiveInfinity)),
            "heating-zero=" + ExceptionFact(() => _ = new VentilationSystem("ERV", 0.5d, 0d)),
            "heating-one=" + ExceptionFact(() => _ = new VentilationSystem("ERV", 0.5d, 1d)),
            "heating-nan=" + ExceptionFact(() => _ = new VentilationSystem("ERV", 0.5d, double.NaN)),
            "cooling-zero=" + ExceptionFact(() => _ = new VentilationSystem("ERV", 0.5d, 0.7d, 0d)),
            "cooling-one=" + ExceptionFact(() => _ = new VentilationSystem("ERV", 0.5d, 0.7d, 1d)),
            "cooling-infinity=" + ExceptionFact(() => _ = new VentilationSystem("ERV", 0.5d, 0.7d, double.PositiveInfinity)),
            "assignment-zero=" + ExceptionFact(() => _ = new VentilationAssignment(ventilation.Id.Value, 0, ventilation)),
            "assignment-mismatch=" + ExceptionFact(() => _ = new VentilationAssignment("ERV-OTHER", 1, ventilation)),
            "grm-success=" + Boolean(probe.ReadSuccess),
            "grm-repeat=" + Boolean(probe.WriterRepeatEqual),
            "grm-reread-id=" + reread.Id.Value,
            "grm-reread-state=" + Join(new[] { Double(reread.AirflowRate), Double(reread.HeatingEfficiency), Double(reread.CoolingEfficiency) }),
            "grm-defaults-omitted-success=" + Boolean(defaultRead.Success),
            "grm-defaults-omitted-state=" + Join(new[] { Double(defaultReread.HeatingEfficiency), Double(defaultReread.CoolingEfficiency) }),
            "conversion-success=" + Boolean(probe.FirstSuccess),
            "conversion-zone-matches=" + Boolean(converted.ZoneId == probe.FirstModel.Zones[0].Id),
            "conversion-id-prefix=" + Boolean(converted.Ventilator.Id.Value.StartsWith("ERV_for_", StringComparison.Ordinal)),
            "conversion-name-prefix=" + Boolean(converted.Ventilator.Name.StartsWith("ERV_for_", StringComparison.Ordinal)),
            "conversion-airflow=" + Double(converted.Ventilator.SupplyAirFlowCubicMetresPerSecond),
            "conversion-heating=" + Double(converted.Ventilator.SensibleEffectiveness),
            "conversion-cooling=" + Double(converted.Ventilator.LatentEffectiveness),
            "conversion-fresh=" + Boolean(!ReferenceEquals(converted.Ventilator, convertedAgain.Ventilator)),
            "conversion-pv-count=" + probe.FirstModel.PhotovoltaicPanels.Count.ToString(CultureInfo.InvariantCulture),
            "conversion-ventilation-count=" + probe.FirstModel.VentilationAssignments.Count.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static OtherSystemsProbe RoundTripAndConvert(
        PhotovoltaicSystem? photovoltaic,
        VentilationSystem? ventilation,
        int ventilationCount)
    {
        GreenRetrofitModel model = CreateModel(photovoltaic, ventilation, ventilationCount);
        string json = GrmWriter.Serialize(model, indented: false);
        GrmReadResult read = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(read.Success, Describe(read.Diagnostics));
        GreenRetrofitModel reread = read.RequireModel();
        bool writerRepeatEqual = json == GrmWriter.Serialize(reread, indented: false);
        Assert.True(writerRepeatEqual);
        GreenRetrofitConversionResult first = Convert(reread);
        GreenRetrofitConversionResult second = Convert(reread);
        return new OtherSystemsProbe(
            json,
            reread,
            first.RequireEnergyModel(),
            second.RequireEnergyModel(),
            read.Success,
            first.Success,
            writerRepeatEqual);
    }

    private static GreenRetrofitModel CreateModel(
        PhotovoltaicSystem? photovoltaic,
        VentilationSystem? ventilation,
        int ventilationCount)
    {
        GreenRetrofitModel template = GrmReader.ReadFile(
            FindRepositoryFile(NativeTemplatePath),
            SimpleDragonDatabase.Default).RequireModel();
        Zone original = Assert.Single(template.Zones);
        VentilationAssignment[] ventilationAssignments = ventilation is null
            ? Array.Empty<VentilationAssignment>()
            : new[] { new VentilationAssignment(ventilation.Id.Value, ventilationCount, ventilation) };
        var zone = new Zone(
            original.Name,
            original.FloorNumber,
            original.Height,
            original.Surfaces,
            original.ProfileName,
            original.Profile,
            original.LightDensity,
            original.SupplySystemAssignments,
            ventilationAssignments,
            original.Id);
        return new GreenRetrofitModel(
            template.Name,
            template.NorthAxis,
            template.Address,
            template.Vintage,
            template.IsMultifamilyHousing,
            new[] { new BuildingFloor(zone.FloorNumber, new[] { zone }) },
            template.Materials,
            template.SurfaceConstructions,
            template.FenestrationConstructions,
            template.SourceSystems,
            template.SupplySystems,
            ventilation is null ? Array.Empty<VentilationSystem>() : new[] { ventilation },
            photovoltaic is null ? Array.Empty<PhotovoltaicSystem>() : new[] { photovoltaic },
            template.Weather);
    }

    private static GreenRetrofitConversionResult Convert(GreenRetrofitModel model)
    {
        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(
            model,
            new GreenRetrofitConversionOptions
            {
                Database = SimpleDragonDatabase.Default,
                IncludeModelValidationDiagnostics = false,
            });
        Assert.True(result.Success, Describe(result.Diagnostics));
        Assert.NotNull(result.RequireEnergyModel());
        return result;
    }

    private static int WritablePropertyCount<T>() => typeof(T)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Count(item => item.SetMethod?.IsPublic == true);

    private static string ReplaceRequired(string text, string oldValue, string newValue)
    {
        Assert.Contains(oldValue, text, StringComparison.Ordinal);
        string replaced = text.Replace(oldValue, newValue, StringComparison.Ordinal);
        Assert.NotEqual(text, replaced);
        return replaced;
    }

    private static string ExceptionFact(Action action)
    {
        try
        {
            action();
            return "returned";
        }
        catch (ArgumentException error)
        {
            return error.GetType().Name + ":" + (error.ParamName ?? "none");
        }
        catch (Exception error)
        {
            return error.GetType().Name;
        }
    }

    private static string DiagnosticCodes(GrmReadResult result) => Join(
        result.Diagnostics.Select(item => item.Code).OrderBy(item => item, StringComparer.Ordinal));

    private static object CreateReceipt(
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        NativeObservation observation = observations[target.CaseIndex];
        CaseBinding fixtureCase = Cases[target.CaseIndex];
        return new
        {
            assertion_id = target.AssertionId,
            adaptation_id = target.AdaptationId,
            classification = target.Classification,
            target_symbol = target.Symbol,
            native_route = target.NativeRoute,
            source_receipt = new
            {
                body_hash = target.BodyHash,
                inventory_index = target.InventoryIndex,
                kind = target.Kind,
                path = UpstreamPath,
                signature_hash = target.SignatureHash,
                symbol = target.Symbol,
                symbol_hash = target.SymbolHash,
            },
            artifacts = new
            {
                fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
                generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
                python_support = Artifact(SupportPath, SupportBytes, SupportSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                native_data = Artifact(NativeTemplatePath, NativeTemplateBytes, NativeTemplateSha256),
                native_sources = NativeSources
                    .Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
            },
            observations = new[]
            {
                new
                {
                    case_id = fixtureCase.CaseId,
                    case_code = fixtureCase.Code,
                    python_facts_sha256 = fixtureCase.FactsSha256,
                    native_fact_count = observation.Facts.Length,
                    native_facts_sha256 = observation.FactsSha256,
                    native_facts = observation.Facts,
                    native_outcome = target.Classification == "equivalent"
                        ? "equivalent-as-pinned"
                        : "adapted-as-pinned",
                },
            },
            scope = new
            {
                exact_target_count = 17,
                equivalent_target_count = 9,
                exception_target_count = 8,
                exact_case_count = 2,
                adjacent_count_not_recorded = 185,
                adjacent_receipts_sha256 = AdjacentReceiptsSha256,
                fixture_repository_commit = FixtureRepositoryCommit,
                claim_policy = "only-the-authoritative-fixture-case-and-declared-production-public-route-are-claimed",
            },
            upstream = new
            {
                ast_sha256 = UpstreamAstSha256,
                commit = UpstreamCommit,
                inventory_content_sha256 = InventoryContentSha256,
                source_bytes = UpstreamBytes,
                source_path = UpstreamPath,
                source_sha256 = UpstreamSourceSha256,
                target_receipts_sha256 = TargetReceiptsSha256,
            },
        };
    }

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(
            receipt,
            "adaptation_id",
            "artifacts",
            "assertion_id",
            "classification",
            "native_route",
            "observations",
            "scope",
            "source_receipt",
            "target_symbol",
            "upstream");
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal(target.Classification, RequiredString(receipt, "classification"));
        Assert.Equal(target.NativeRoute, RequiredString(receipt, "native_route"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));

        JsonElement source = receipt.GetProperty("source_receipt");
        AssertKeys(source, "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
        Assert.Equal(target.InventoryIndex, source.GetProperty("inventory_index").GetInt32());
        Assert.Equal(target.Symbol, RequiredString(source, "symbol"));
        Assert.Equal(target.Kind, RequiredString(source, "kind"));
        Assert.Equal(target.SymbolHash, RequiredString(source, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(source, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(source, "body_hash"));
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));

        JsonElement observed = Assert.Single(receipt.GetProperty("observations").EnumerateArray());
        NativeObservation expectedObservation = observations[target.CaseIndex];
        CaseBinding expectedCase = Cases[target.CaseIndex];
        AssertKeys(
            observed,
            "case_code",
            "case_id",
            "native_fact_count",
            "native_facts",
            "native_facts_sha256",
            "native_outcome",
            "python_facts_sha256");
        Assert.Equal(expectedCase.CaseId, RequiredString(observed, "case_id"));
        Assert.Equal(expectedCase.Code, RequiredString(observed, "case_code"));
        Assert.Equal(expectedCase.FactsSha256, RequiredString(observed, "python_facts_sha256"));
        Assert.Equal(expectedObservation.Facts.Length,
            observed.GetProperty("native_fact_count").GetInt32());
        Assert.Equal(expectedObservation.FactsSha256,
            RequiredString(observed, "native_facts_sha256"));
        AssertStringArray(observed.GetProperty("native_facts"), expectedObservation.Facts);
        Assert.Equal(
            target.Classification == "equivalent" ? "equivalent-as-pinned" : "adapted-as-pinned",
            RequiredString(observed, "native_outcome"));

        JsonElement artifacts = receipt.GetProperty("artifacts");
        AssertKeys(
            artifacts,
            "fixture",
            "generator",
            "native_data",
            "native_sources",
            "public_inventory",
            "python_support",
            "python_validator");
        AssertArtifact(artifacts.GetProperty("fixture"), FixturePath, FixtureBytes, FixtureSha256);
        AssertArtifact(artifacts.GetProperty("generator"), GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertArtifact(artifacts.GetProperty("python_support"), SupportPath, SupportBytes, SupportSha256);
        AssertArtifact(artifacts.GetProperty("python_validator"), ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertArtifact(artifacts.GetProperty("public_inventory"), InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertArtifact(artifacts.GetProperty("native_data"), NativeTemplatePath, NativeTemplateBytes, NativeTemplateSha256);
        AssertArtifactArray(artifacts.GetProperty("native_sources"), NativeSources);

        JsonElement scope = receipt.GetProperty("scope");
        AssertKeys(
            scope,
            "adjacent_count_not_recorded",
            "adjacent_receipts_sha256",
            "claim_policy",
            "equivalent_target_count",
            "exact_case_count",
            "exact_target_count",
            "exception_target_count",
            "fixture_repository_commit");
        Assert.Equal(17, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(9, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(8, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(2, scope.GetProperty("exact_case_count").GetInt32());
        Assert.Equal(185, scope.GetProperty("adjacent_count_not_recorded").GetInt32());
        Assert.Equal(AdjacentReceiptsSha256,
            RequiredString(scope, "adjacent_receipts_sha256"));
        Assert.Equal(FixtureRepositoryCommit,
            RequiredString(scope, "fixture_repository_commit"));
        Assert.Equal(
            "only-the-authoritative-fixture-case-and-declared-production-public-route-are-claimed",
            RequiredString(scope, "claim_policy"));

        JsonElement upstream = receipt.GetProperty("upstream");
        AssertKeys(
            upstream,
            "ast_sha256",
            "commit",
            "inventory_content_sha256",
            "source_bytes",
            "source_path",
            "source_sha256",
            "target_receipts_sha256");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "source_path"));
        Assert.Equal(UpstreamBytes, upstream.GetProperty("source_bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(upstream, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(upstream, "ast_sha256"));
        Assert.Equal(InventoryContentSha256,
            RequiredString(upstream, "inventory_content_sha256"));
        Assert.Equal(TargetReceiptsSha256,
            RequiredString(upstream, "target_receipts_sha256"));
    }

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
    };

    private static void AssertArtifactArray(JsonElement value, IReadOnlyList<ArtifactPin> expected)
    {
        JsonElement[] items = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, items.Length);
        for (int index = 0; index < items.Length; index++)
        {
            AssertArtifact(items[index], expected[index].Path, expected[index].Bytes, expected[index].Sha256);
        }
    }

    private static void AssertArtifact(JsonElement value, string path, int bytes, string sha256)
    {
        AssertKeys(value, "bytes", "path", "sha256");
        Assert.Equal(path, RequiredString(value, "path"));
        Assert.Equal(bytes, value.GetProperty("bytes").GetInt32());
        Assert.Equal(sha256, RequiredString(value, "sha256"));
    }

    private static void AssertPinnedArtifact(string path, int bytes, string sha256)
    {
        byte[] content = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(bytes, content.Length);
        Assert.Equal(sha256, Sha256(content));
    }

    private static string FindRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private static EntityId Id(string value) => new(value);

    private static string Sha256(byte[] value) =>
        "sha256:" + System.Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string CanonicalSha256(JsonElement value) =>
        Sha256(Encoding.UTF8.GetBytes(CanonicalJson(value)));

    private static string CanonicalJson(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            WriteCanonical(writer, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new Xunit.Sdk.XunitException($"Unsupported JSON kind {value.ValueKind}.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.True(names.Add(property.Name), $"Duplicate JSON key '{property.Name}'.");
                AssertUniqueObjectKeysRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertUniqueObjectKeysRecursive(item);
            }
        }
    }

    private static void AssertKeys(JsonElement value, params string[] expected) => Assert.Equal(
        expected.OrderBy(item => item, StringComparer.Ordinal),
        value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));

    private static void AssertStringArray(JsonElement value, IEnumerable<string> expected) =>
        Assert.Equal(expected, ReadStringArray(value));

    private static string[] ReadStringArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static int[] ReadIntArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    private static string RequiredString(JsonElement value, string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        string? result = property.GetString();
        Assert.False(string.IsNullOrWhiteSpace(result));
        return result!;
    }

    private static void AssertSha256(string value) => Assert.Matches(
        new Regex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
        value);

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(
            new Regex(@"0x[0-9a-fA-F]{8,}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
        Assert.DoesNotMatch(
            new Regex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
        Assert.DoesNotMatch(
            new Regex(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(
            new Regex(@"[A-Za-z]:\\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
        Assert.DoesNotContain("/home/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number) && double.IsFinite(number));
        }
        else if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            IEnumerable<JsonElement> children = value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                : value.EnumerateObject().Select(item => item.Value);
            foreach (JsonElement child in children)
            {
                AssertNoNonFiniteJsonNumbers(child);
            }
        }
    }

    private static string Double(double? value) => value.HasValue
        ? value.Value.ToString("R", CultureInfo.InvariantCulture)
        : "null";

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

    private static string Describe(IEnumerable<Diagnostic> diagnostics) => string.Join(
        " | ",
        diagnostics.Select(item => item.Code + ":" + item.Severity + ":" + item.Message));

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string FactsSha256,
        string CaseSha256,
        string[] TargetSymbols);

    private sealed record ExpectedTargetBinding(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string AssertionId,
        string Classification,
        string AdaptationId,
        string NativeRoute,
        int CaseIndex);

    private sealed record TargetBinding(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string Classification,
        string AdaptationId,
        string NativeRoute,
        int CaseIndex);

    private sealed record NativeObservation(string Code, string[] Facts, string FactsSha256);

    private sealed record OracleCorpus(
        JsonElement[] FixtureCases,
        TargetBinding[] Targets,
        int[] AdjacentIndices);

    private sealed record OtherSystemsProbe(
        string Json,
        GreenRetrofitModel RereadModel,
        EnergyModel FirstModel,
        EnergyModel SecondModel,
        bool ReadSuccess,
        bool FirstSuccess,
        bool WriterRepeatEqual);
}
