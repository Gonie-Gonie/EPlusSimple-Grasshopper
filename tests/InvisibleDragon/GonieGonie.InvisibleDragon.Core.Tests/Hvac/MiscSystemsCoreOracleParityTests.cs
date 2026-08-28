#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.InvisibleDragon.Tests.Model;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class MiscSystemsCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-hvac-misc-systems-core-oracle.json";
    private const int FixtureBytes = 290_479;
    private const string FixtureSha256 =
        "sha256:2b2e5d3a5a6fc76247e6faec469dc23039ad53ae0c64a36553974633f2da9f89";
    private const string FixtureSchema =
        "goniegonie.python-reference.dragon-hvac-misc-systems-core.v1";
    private const string FixtureRepositoryCommit = "c99f216";
    private const string CasesSha256 =
        "sha256:4f52a6e71dd8f2136d7ba9cfe61e904e2038d831291f3cb8c50c0f18aa5e7ca3";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_hvac_misc_systems_core_oracle.py";
    private const int GeneratorBytes = 53_961;
    private const string GeneratorSha256 =
        "sha256:4d32b8eb44c810ee1210448be2e1fc8c94dee90a18159099304a2e74743dc421";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_hvac_misc_systems_core_oracle.py";
    private const int ValidatorBytes = 22_698;
    private const string ValidatorSha256 =
        "sha256:ef66a678175883a24ca4eedd29f0f16570d321a8379f3eceba1e8e123b0a2117";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/hvac.py";
    private const int UpstreamBytes = 137_833;
    private const string UpstreamSourceSha256 =
        "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0";
    private const string UpstreamAstSha256 =
        "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31";
    private const string TargetReceiptsSha256 =
        "sha256:92bd193686bf7ff9da3219571d197c70f55d16b33996268741e56af7083cff1b";
    private const string FullSourceReceiptsSha256 =
        "sha256:f5db7f1a79890387192db20619e055691700f48bfbe368efeffbe37b695593e7";

    private const string SupportPath =
        "fixtures/reference/python-0.7.0/dragon-hvac-photovoltaic-to-idf-object-oracle.json";
    private const int SupportBytes = 147_261;
    private const string SupportSha256 =
        "sha256:07c383c316989ccb22ac3eadcf9d8388764f76effbbf03c13b7a54f8af20f22b";

    private const string ErvRoute =
        "GonieGonie.InvisibleDragon.Hvac.ZoneVentilationAssignment -> " +
        "GonieGonie.InvisibleDragon.Model.EnergyModel -> " +
        "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?)";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Hvac.MiscSystemsCoreOracleParityTests.MatchesPinnedMiscSystemsThroughPublicProductionApis";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_582,
            "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/DomesticHotWater.cs", 1_941,
            "sha256:586f020b82c50c70ad20d8a667fa338ce3372d39bb1bd48291ea42c97b8d4e2d"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/VentilationAndPv.cs", 7_074,
            "sha256:eb7d871d621c8f3970099dff7bdb412dc84f33cd2ef07c0fb99c94a550d5eb82"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs", 22_015,
            "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_764,
            "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/IdfModel.cs", 13_182,
            "sha256:50aa8a362214d34bba37dcf51ef3c0cce89d54895110a0da786c11d8fe233495"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "dragon-hvac-misc-systems-core.domestic-hot-water-constructor-fuel",
            "sha256:61beb677ffd37c28b5e0342599c9bed0db0d03bc93aaf45dabd350dc96f2554c",
            "sha256:8492b46ae506b16cd682e22a4b5b104ea8151eb866c5f308730a29e684d9e13b", 3),
        new("A02", "dragon-hvac-misc-systems-core.domestic-hot-water-efficiency-emission",
            "sha256:c04616618b419cc84decdc047747a07aa87d85bde2f9d4a5af3ccf099089937b",
            "sha256:bdab6a146714be73054d9500686cb1d631b7bc6f76a55c9bdbe30042f6c9fc41", 2),
        new("B01", "dragon-hvac-misc-systems-core.energy-recovery-ventilator-permissive-empty",
            "sha256:7003b96958be66c6f33756480249a91ff5d879ee912ff34b9edb14e98eeffff8",
            "sha256:07678b1e3fccc095815b9ec30e57786a015c071544e2f3816d29b662f8107259", 3),
        new("C01", "dragon-hvac-misc-systems-core.photovoltaic-constructor-shape",
            "sha256:9873174fd27c61b03c65aa9499642195804c89e9af6c559b55adb6ef485c1073",
            "sha256:9d42863c0cb8de852b4cc45e19518a652b6bd18ad4fee191d2a46872e237149d", 2),
        new("C02", "dragon-hvac-misc-systems-core.photovoltaic-geometry-properties",
            "sha256:bd3cb31de0085b52e6ca0e4665e06cb161884797a2655e6377f025950dd28556",
            "sha256:83470f1177e4f17e1fbfeacd6239cc826d5e95845c3acafc196366a3fe0c9625", 3),
        new("C03", "dragon-hvac-misc-systems-core.photovoltaic-efficiency-properties",
            "sha256:2f4c5a8b04b5582804edbad5e6c4abfbdbdbb3c4331fff49738b677f80bd8ad8",
            "sha256:a0b39cb7c87d292f5ee53eeffe80c33e0de73f23d1cf6565fc8e38fa7b563453", 2),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        new(693, "DomesticHotWater", "exception", "dragon-hvac-misc-systems-core-693-domestichotwater", "immutable-native-domain-model", "GonieGonie.InvisibleDragon.Hvac.DomesticHotWater", Cases[0].CaseId),
        new(694, "DomesticHotWater.__init__", "exception", "dragon-hvac-misc-systems-core-694-domestichotwater-__init__", "immutable-native-domain-model", "GonieGonie.InvisibleDragon.Hvac.DomesticHotWater.DomesticHotWater(EntityId, string, Fuel, double)", Cases[0].CaseId),
        new(697, "DomesticHotWater.efficiency", "equivalent", "dragon-hvac-misc-systems-core-697-domestichotwater-efficiency", "immutable-native-property", "GonieGonie.InvisibleDragon.Hvac.DomesticHotWater.Efficiency", Cases[0].CaseId),
        new(698, "DomesticHotWater.fuel", "exception", "dragon-hvac-misc-systems-core-698-domestichotwater-fuel", "immutable-native-domain-model", "GonieGonie.InvisibleDragon.Hvac.DomesticHotWater.Fuel", Cases[1].CaseId),
        new(699, "DomesticHotWater.to_idf_object", "equivalent", "dragon-hvac-misc-systems-core-699-domestichotwater-to_idf_object", "direct-public-domestic-hot-water-empty-emission", "GonieGonie.InvisibleDragon.Hvac.DomesticHotWater.ToIdfObjects(IdfGenerationContext)", Cases[1].CaseId),
        new(714, "EnergyRecoveryVentilator", "exception", "dragon-hvac-misc-systems-core-714-energyrecoveryventilator", "immutable-native-domain-model", "GonieGonie.InvisibleDragon.Hvac.EnergyRecoveryVentilator", Cases[2].CaseId),
        new(715, "EnergyRecoveryVentilator.__init__", "exception", "dragon-hvac-misc-systems-core-715-energyrecoveryventilator-__init__", "immutable-native-domain-model", "GonieGonie.InvisibleDragon.Hvac.EnergyRecoveryVentilator.EnergyRecoveryVentilator(EntityId, string, double, double, double?, double, double)", Cases[2].CaseId),
        new(716, "EnergyRecoveryVentilator.to_idf_object", "exception", "dragon-hvac-misc-systems-core-716-energyrecoveryventilator-to_idf_object", "aggregate-public-energy-model-ventilation-emission", ErvRoute, Cases[2].CaseId),
        new(753, "PhotoVoltaicPanel", "exception", "dragon-hvac-misc-systems-core-753-photovoltaicpanel", "immutable-native-domain-model", "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel", Cases[3].CaseId),
        new(754, "PhotoVoltaicPanel.__init__", "exception", "dragon-hvac-misc-systems-core-754-photovoltaicpanel-__init__", "immutable-native-domain-model", "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel.PhotovoltaicPanel(EntityId, string, double, double, double, double, double)", Cases[3].CaseId),
        new(756, "PhotoVoltaicPanel.area", "equivalent", "dragon-hvac-misc-systems-core-756-photovoltaicpanel-area", "immutable-native-property", "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel.AreaSquareMetres", Cases[4].CaseId),
        new(757, "PhotoVoltaicPanel.azimuth", "equivalent", "dragon-hvac-misc-systems-core-757-photovoltaicpanel-azimuth", "immutable-native-property", "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel.AzimuthDegrees", Cases[4].CaseId),
        new(758, "PhotoVoltaicPanel.effective_area_ratio", "equivalent", "dragon-hvac-misc-systems-core-758-photovoltaicpanel-effective_area_ratio", "immutable-native-property", "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel.ActiveCellAreaFraction", Cases[4].CaseId),
        new(759, "PhotoVoltaicPanel.efficiency", "equivalent", "dragon-hvac-misc-systems-core-759-photovoltaicpanel-efficiency", "immutable-native-property", "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel.Efficiency", Cases[5].CaseId),
        new(760, "PhotoVoltaicPanel.tilt", "equivalent", "dragon-hvac-misc-systems-core-760-photovoltaicpanel-tilt", "immutable-native-property", "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel.TiltDegrees", Cases[5].CaseId),
    };

    private static bool DiscoverPins => false;

    private static readonly NativePin[] ExpectedNativePins =
    {
        new("A01", 7, "sha256:c0d45e20c7b0a60054cd7a0cfa110bf8985437ff0239ee691dae570044868f60"),
        new("A02", 7, "sha256:fca24e1d2ca2f86b679e441b2a29adfe8f0e34882b307ad4fb6ba1f0e695cf44"),
        new("B01", 13, "sha256:fc96123b456b1fad9981be1d36974df5fccb32da6b2a7332a47604f9d5dcccdd"),
        new("C01", 6, "sha256:c30afcb4e4b30db2a83e0686f45f89bd0dac9fc05409afd0652ceca72c2778cf"),
        new("C02", 6, "sha256:9c9dd7c4c87629fe127ad7c12b9fe9ec37158f3017f45062b90a1110877ef8f7"),
        new("C03", 6, "sha256:7a9faeec325fcde4fd9a20f1eea37d80f67be9bb84dfbbcf5d5e41707be727fa"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:fb8acacf8a2859a292d2b0220936f09897d5fac6b2a5c311d821034cf5b48f9c", // 693 DomesticHotWater
        "sha256:e69532849718f4503971fd563f6fb9f438c32b108c0e6bdeb8bcae3368b68a38", // 694 DomesticHotWater.__init__
        "sha256:d1ce8137b8e834ec8ed2603fa90c6564f9185fb927e97fa79179bfbd471431ed", // 697 DomesticHotWater.efficiency
        "sha256:30890a1468c2395a92c0d03a51b7517fc07b8ad5818b9d37f040a70cf30a0bcf", // 698 DomesticHotWater.fuel
        "sha256:ff7e019736f12dc8a0016f23d6ba0fd78b5dfa2b5a335423f13a0a74be281881", // 699 DomesticHotWater.to_idf_object
        "sha256:60eec34b8b5129939037dd962d8a129f826696c47f934edacee6245e7b156b8e", // 714 EnergyRecoveryVentilator
        "sha256:5c39462f5320d0cf19919a7f97d449b31241cf4908c8a639f43f8ef5d5b7dfba", // 715 EnergyRecoveryVentilator.__init__
        "sha256:f6a8bbdbd9ae2e6911472b3bdf405d004ffaec5dea891d2d2580366bc39f3385", // 716 EnergyRecoveryVentilator.to_idf_object
        "sha256:1203db3639d74a5452b21d8b869ec17ca0d92d35ce2262771213a12853e6a7aa", // 753 PhotoVoltaicPanel
        "sha256:c2d8c6166e529f15eddc9622681a37c3843d0da4b86a1aa2a7d214931b116590", // 754 PhotoVoltaicPanel.__init__
        "sha256:1d138cdbbd31817127ee54a8e132dc68863a08d6c0a59963b2f3e1dcde5589f4", // 756 PhotoVoltaicPanel.area
        "sha256:a6b5cbf8f9656851e10d2f3702214889ac2db22107551a72189240c8872e5c1e", // 757 PhotoVoltaicPanel.azimuth
        "sha256:6e712f4c7437ae00d3c2211f40802cef614471c203d9dbae8e1b79da1bb200a5", // 758 PhotoVoltaicPanel.effective_area_ratio
        "sha256:b0e62e5f756f9509366948eb15cc763f47e8064b4cfb604b228846d8e8af9ee3", // 759 PhotoVoltaicPanel.efficiency
        "sha256:5dbb12930a21167f4985d51e3107c54dbc7258faa8fe9741ab21866bff47cae6", // 760 PhotoVoltaicPanel.tilt
    };

    [Fact]
    public void MatchesPinnedMiscSystemsThroughPublicProductionApis()
    {
        ValidatePinnedArtifactsAndPublicApis();
        using JsonDocument fixture = ReadPinnedFixture();
        OracleCorpus corpus = ValidateFixture(fixture.RootElement);
        NativeObservation[] observations = Cases.Select(ObserveNativeCase).ToArray();
        object[] receipts = corpus.Targets.Select(target => CreateReceipt(target, observations)).ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();

        if (DiscoverPins)
        {
            string facts = string.Join(Environment.NewLine, observations.Select(item =>
                $"        new(\"{item.Code}\", {item.Facts.Length}, \"{item.FactsSha256}\"),"));
            string hashes = string.Join(Environment.NewLine, corpus.Targets.Select((target, index) =>
                $"        \"{receiptHashes[index]}\", // {target.InventoryIndex} {target.Symbol}"));
            throw new Xunit.Sdk.XunitException(
                "MISC_SYSTEMS_NATIVE_PINS" + Environment.NewLine + facts + Environment.NewLine + hashes);
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].Code, observations[index].Code);
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }

        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
        var recordedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach ((TargetBinding target, object receipt) in corpus.Targets.Zip(receipts))
        {
            string registryAssertionId = RegistryAssertionId(target.AssertionId);
            Assert.True(recordedIds.Add(registryAssertionId));
            TrustedEvidenceRecorder.Record(
                registryAssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }

        Assert.Equal(15, recordedIds.Count);
        Assert.Equal(15, corpus.Targets.Length);
        Assert.Equal(7, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(8, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(6, observations.Length);
    }

    private static void ValidatePinnedArtifactsAndPublicApis()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertPinnedArtifact(SupportPath, SupportBytes, SupportSha256);
        foreach (ArtifactPin pin in NativeSources)
        {
            AssertPinnedArtifact(pin.Path, pin.Bytes, pin.Sha256);
        }

        Assert.True(typeof(DomesticHotWater).IsSealed);
        AssertConstructor(typeof(DomesticHotWater), typeof(EntityId), typeof(string), typeof(Fuel), typeof(double));
        AssertPublicReadOnlyProperty(typeof(DomesticHotWater), nameof(DomesticHotWater.Fuel), typeof(Fuel));
        AssertPublicReadOnlyProperty(typeof(DomesticHotWater), nameof(DomesticHotWater.Efficiency), typeof(double));
        MethodInfo dhwEmission = AssertPublicMethod(typeof(DomesticHotWater), nameof(DomesticHotWater.ToIdfObjects));
        Assert.Equal(typeof(IReadOnlyList<IdfObject>), dhwEmission.ReturnType);
        Assert.Equal(typeof(IdfGenerationContext), Assert.Single(dhwEmission.GetParameters()).ParameterType);

        Assert.True(typeof(EnergyRecoveryVentilator).IsSealed);
        AssertConstructor(typeof(EnergyRecoveryVentilator), typeof(EntityId), typeof(string), typeof(double), typeof(double), typeof(double?), typeof(double), typeof(double));
        Assert.DoesNotContain(typeof(EnergyRecoveryVentilator).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Idf", StringComparison.Ordinal));
        MethodInfo aggregate = AssertPublicMethod(typeof(EnergyModel), nameof(EnergyModel.ToIdfDocument));
        Assert.Equal(typeof(IdfDocument), aggregate.ReturnType);

        Assert.True(typeof(PhotovoltaicPanel).IsSealed);
        AssertConstructor(typeof(PhotovoltaicPanel), typeof(EntityId), typeof(string), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double));
        AssertPublicReadOnlyProperty(typeof(PhotovoltaicPanel), nameof(PhotovoltaicPanel.AreaSquareMetres), typeof(double));
        AssertPublicReadOnlyProperty(typeof(PhotovoltaicPanel), nameof(PhotovoltaicPanel.TiltDegrees), typeof(double));
        AssertPublicReadOnlyProperty(typeof(PhotovoltaicPanel), nameof(PhotovoltaicPanel.AzimuthDegrees), typeof(double));
        AssertPublicReadOnlyProperty(typeof(PhotovoltaicPanel), nameof(PhotovoltaicPanel.Efficiency), typeof(double));
        AssertPublicReadOnlyProperty(typeof(PhotovoltaicPanel), nameof(PhotovoltaicPanel.ActiveCellAreaFraction), typeof(double));
    }

    private static JsonDocument ReadPinnedFixture()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(FixturePath));
        Assert.Equal(FixtureBytes, bytes.Length);
        Assert.Equal(FixtureSha256, Sha256(bytes));
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
    }

    private static OracleCorpus ValidateFixture(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoHostPaths(root);
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        for (int index = 0; index < Cases.Length; index++)
        {
            CaseBinding expected = Cases[index];
            JsonElement actual = fixtureCases[index];
            Assert.Equal(expected.Code, RequiredString(actual, "code"));
            Assert.Equal(expected.CaseId, RequiredString(actual, "id"));
            Assert.Equal(expected.CaseSha256, RequiredString(actual, "case_sha256"));
            Assert.Equal(expected.PythonFactsSha256, RequiredString(actual.GetProperty("python"), "facts_sha256"));
            Assert.Equal(expected.TargetCount, actual.GetProperty("target_symbols").GetArrayLength());
        }

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(TargetReceiptsSha256, RequiredString(upstream, "target_receipts_sha256"));
        Assert.Equal(FullSourceReceiptsSha256, RequiredString(upstream, "full_source_receipts_sha256"));
        JsonElement inventory = upstream.GetProperty("inventory");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        JsonElement source = upstream.GetProperty("source");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement targetReceipts = root.GetProperty("target_receipts");
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(targetReceipts));
        JsonElement[] actualTargets = targetReceipts.EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, actualTargets.Length);
        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.Equal(7, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());
        Assert.Equal(8, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        var targets = new TargetBinding[actualTargets.Length];
        for (int index = 0; index < actualTargets.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement actual = actualTargets[index];
            Assert.Equal(expected.InventoryIndex, actual.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(expected.Classification, RequiredString(contract.GetProperty("classifications"), expected.Symbol));
            Assert.Equal(expected.AssertionId, RequiredString(contract.GetProperty("assertion_ids"), expected.Symbol));
            Assert.Equal(expected.Adaptation, RequiredString(contract.GetProperty("adaptations"), expected.Symbol));
            Assert.Equal(expected.NativeRoute, RequiredString(contract.GetProperty("native_routes"), expected.Symbol));
            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                RequiredString(actual, "kind"),
                RequiredString(actual, "symbol_hash"),
                RequiredString(actual, "signature_hash"),
                RequiredString(actual, "body_hash"),
                expected.Classification,
                expected.AssertionId,
                expected.Adaptation,
                expected.NativeRoute,
                expected.CaseId);
        }

        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("exact_disjoint_source_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_hvac_source_partition").GetBoolean());
        Assert.Equal(15, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(174, closure.GetProperty("source_declaration_count").GetInt32());
        JsonElement evidence = contract.GetProperty("evidence_contract");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("photovoltaic_index_761_support_reused").GetBoolean());
        Assert.False(evidence.GetProperty("photovoltaic_index_761_emission_executed").GetBoolean());

        JsonElement support = root.GetProperty("support");
        Assert.Equal(SupportPath, RequiredString(support, "path"));
        Assert.Equal(SupportBytes, support.GetProperty("bytes").GetInt32());
        Assert.Equal(SupportSha256, RequiredString(support, "sha256"));
        Assert.False(support.GetProperty("target_promoted").GetBoolean());
        JsonElement supportReceipt = Assert.Single(support.GetProperty("resolved_receipts").EnumerateArray());
        Assert.Equal(761, supportReceipt.GetProperty("inventory_index").GetInt32());
        Assert.Equal("PhotoVoltaicPanel.to_idf_object", RequiredString(supportReceipt, "symbol"));

        JsonElement review = root.GetProperty("native_review");
        Assert.Equal(7, review.GetProperty("counts").GetProperty("equivalent").GetInt32());
        Assert.Equal(8, review.GetProperty("counts").GetProperty("exception").GetInt32());
        Assert.True(review.GetProperty("domestic_hot_water_direct_public_api_only").GetBoolean());
        Assert.True(review.GetProperty("energy_recovery_ventilator_public_aggregate_route").GetBoolean());
        Assert.True(review.GetProperty("photovoltaic_public_api_only").GetBoolean());
        Assert.False(review.GetProperty("internal_generate_route_claimed").GetBoolean());
        JsonElement[] nativeSources = review.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(NativeSources.Length, nativeSources.Length);
        for (int index = 0; index < nativeSources.Length; index++)
        {
            Assert.Equal(NativeSources[index].Path, RequiredString(nativeSources[index], "path"));
            Assert.Equal(NativeSources[index].Bytes, nativeSources[index].GetProperty("bytes").GetInt32());
            Assert.Equal(NativeSources[index].Sha256, RequiredString(nativeSources[index], "sha256"));
        }

        return new OracleCorpus(targets);
    }

    private static NativeObservation ObserveNativeCase(CaseBinding item)
    {
        string[] facts = item.Code switch
        {
            "A01" => ObserveDomesticConstructor(),
            "A02" => ObserveDomesticEmission(),
            "B01" => ObserveVentilatorAggregate(),
            "C01" => ObservePhotovoltaicConstructor(),
            "C02" => ObservePhotovoltaicGeometry(),
            "C03" => ObservePhotovoltaicEfficiency(),
            _ => throw new InvalidOperationException("Unknown misc-system case: " + item.Code),
        };
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        return new NativeObservation(item.Code, item.CaseId, facts, CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveDomesticConstructor()
    {
        var system = new DomesticHotWater(new EntityId("MISC-DHW-A"), "  Oracle Domestic Water  ", Fuel.NaturalGas, 0.825);
        return new[]
        {
            "public-type=" + system.GetType().FullName,
            "sealed=" + system.GetType().IsSealed,
            "id=" + system.Id.Value,
            "normalized-name=" + system.Name,
            "fuel-enum=" + system.Fuel,
            "efficiency=" + Format(system.Efficiency),
            "immutable-properties=" + ReadOnlyPropertyNames(typeof(DomesticHotWater)),
        };
    }

    private static string[] ObserveDomesticEmission()
    {
        var system = new DomesticHotWater(new EntityId("MISC-DHW-B"), "Oracle Empty Water", Fuel.Propane, 1);
        var context = new IdfGenerationContext();
        IReadOnlyList<IdfObject> first = system.ToIdfObjects(context);
        IReadOnlyList<IdfObject> second = system.ToIdfObjects(context);
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.NotSame(first, second);
        return new[]
        {
            "public-route=GonieGonie.InvisibleDragon.Hvac.DomesticHotWater.ToIdfObjects(IdfGenerationContext)",
            "fuel-enum=" + system.Fuel,
            "efficiency=" + Format(system.Efficiency),
            "first-count=" + first.Count,
            "second-count=" + second.Count,
            "fresh-list=" + !ReferenceEquals(first, second),
            "active-energyplus-process=false",
        };
    }

    private static string[] ObserveVentilatorAggregate()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("MISC-ERV-ZONE", "Misc ERV Zone");
        var ventilator = new EnergyRecoveryVentilator(
            new EntityId("MISC-ERV"), "Misc Oracle ERV", 0.78, 0.62, 0.45, 0.71, 140);
        var assignment = new ZoneVentilationAssignment(zone.Id, ventilator);
        var model = new EnergyModel("Misc ERV aggregate", new[] { zone }, ventilationAssignments: new[] { assignment });
        IdfDocument first = model.ToIdfDocument();
        IdfDocument second = model.ToIdfDocument();
        string[] ervTypes =
        {
            "OutdoorAir:Node",
            "HeatExchanger:AirToAir:SensibleAndLatent",
            "Fan:OnOff",
            "ZoneHVAC:EnergyRecoveryVentilator:Controller",
            "ZoneHVAC:EnergyRecoveryVentilator",
        };
        Assert.All(ervTypes, type => Assert.NotEmpty(first[type]));
        return new[]
        {
            "public-route=" + ErvRoute,
            "ventilator=" + ventilator.Id.Value + "|" + ventilator.Name,
            "effectiveness=" + Format(ventilator.SensibleEffectiveness) + "|" + Format(ventilator.LatentEffectiveness),
            "flow=" + Format(ventilator.SupplyAirFlowCubicMetresPerSecond!.Value),
            "fan=" + Format(ventilator.FanTotalEfficiency) + "|" + Format(ventilator.FanPressureRisePascals),
            "assignment-zone=" + assignment.ZoneId.Value,
            "aggregate-object-count=" + first.Count,
            "erv-type-counts=" + string.Join(";", ervTypes.Select(type => type + "=" + first[type].Count)),
            "repeat-idf-identical=" + (DocumentHash(first) == DocumentHash(second)),
            "aggregate-idf-sha256=" + DocumentHash(first),
            "upstream-empty-vs-native-aggregate-emission=exception",
            "internal-generate-route-claimed=false",
            "active-energyplus-process=false",
        };
    }

    private static string[] ObservePhotovoltaicConstructor()
    {
        PhotovoltaicPanel panel = CreatePanel("MISC-PV-A", "Misc PV Constructor");
        return new[]
        {
            "public-type=" + panel.GetType().FullName,
            "sealed=" + panel.GetType().IsSealed,
            "id=" + panel.Id.Value,
            "name=" + panel.Name,
            "constructor-parameter-count=" + Assert.Single(typeof(PhotovoltaicPanel).GetConstructors()).GetParameters().Length,
            "index-761-emission-executed=false",
        };
    }

    private static string[] ObservePhotovoltaicGeometry()
    {
        PhotovoltaicPanel panel = CreatePanel("MISC-PV-B", "Misc PV Geometry");
        return new[]
        {
            "public-area-square-metres=" + Format(panel.AreaSquareMetres),
            "public-azimuth-degrees=" + Format(panel.AzimuthDegrees),
            "public-tilt-degrees=" + Format(panel.TiltDegrees),
            "public-active-cell-area-fraction=" + Format(panel.ActiveCellAreaFraction),
            "immutable-properties=" + ReadOnlyPropertyNames(typeof(PhotovoltaicPanel)),
            "index-761-support-only=true",
        };
    }

    private static string[] ObservePhotovoltaicEfficiency()
    {
        PhotovoltaicPanel panel = CreatePanel("MISC-PV-C", "Misc PV Efficiency");
        return new[]
        {
            "public-efficiency=" + Format(panel.Efficiency),
            "public-tilt-degrees=" + Format(panel.TiltDegrees),
            "public-active-cell-area-fraction=" + Format(panel.ActiveCellAreaFraction),
            "direct-public-properties-only=true",
            "to-idf-object-index-761-promoted=false",
            "active-energyplus-process=false",
        };
    }

    private static PhotovoltaicPanel CreatePanel(string id, string name) =>
        new(new EntityId(id), name, 42.25, 27.5, 182.75, 0.213, 0.68);

    private static object CreateReceipt(TargetBinding target, IReadOnlyList<NativeObservation> observations)
    {
        NativeObservation observation = Assert.Single(observations, item => item.CaseId == target.CaseId);
        CaseBinding fixtureCase = Assert.Single(Cases, item => item.CaseId == target.CaseId);
        return new
        {
            adaptation_id = target.Adaptation,
            artifacts = new
            {
                fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
                generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
                native_sources = NativeSources.Select(pin => Artifact(pin.Path, pin.Bytes, pin.Sha256)).ToArray(),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                photovoltaic_emission_support = Artifact(SupportPath, SupportBytes, SupportSha256),
            },
            assertion_id = target.AssertionId,
            classification = target.Classification,
            native_route = target.NativeRoute,
            observations = new[]
            {
                new
                {
                    case_code = observation.Code,
                    case_id = observation.CaseId,
                    native_fact_count = observation.Facts.Length,
                    native_facts = observation.Facts,
                    native_facts_sha256 = observation.FactsSha256,
                    python_case_sha256 = fixtureCase.CaseSha256,
                    python_facts_sha256 = fixtureCase.PythonFactsSha256,
                },
            },
            scope = new
            {
                active_energyplus_process_claim = false,
                equivalent_target_count = 7,
                exact_case_count = 6,
                exact_target_count = 15,
                exception_target_count = 8,
                fixture_repository_commit = FixtureRepositoryCommit,
                internal_generate_route_claimed = false,
                photovoltaic_index_761_emission_executed = false,
                photovoltaic_index_761_support_only = true,
                public_production_routes_only = true,
                structural_only = false,
            },
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
            target_symbol = target.Symbol,
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

    private static void AssertConstructor(Type type, params Type[] parameters)
    {
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(parameters, constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static PropertyInfo AssertPublicReadOnlyProperty(Type type, string name, Type propertyType)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(propertyType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.Null(property.SetMethod);
        return property;
    }

    private static MethodInfo AssertPublicMethod(Type type, string name) => Assert.Single(
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance), method => method.Name == name);

    private static string ReadOnlyPropertyNames(Type type) => string.Join(
        "|",
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is not null && property.SetMethod is null)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal));

    private static string DocumentHash(IdfDocument document) =>
        Sha256(Encoding.UTF8.GetBytes(IdfWriter.Write(document)));

    private static string Format(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static object Artifact(string path, int bytes, string sha256) => new { bytes, path, sha256 };

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
            string candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file '" + relativePath + "'.");
    }

    private static string Sha256(byte[] value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

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
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
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
                throw new InvalidOperationException("Unsupported JSON kind: " + value.ValueKind);
        }
    }

    private static string RequiredString(JsonElement item, string propertyName)
    {
        JsonElement value = item.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return Assert.IsType<string>(value.GetString());
    }

    private static string RegistryAssertionId(string fixtureAssertionId)
    {
        Assert.Equal(fixtureAssertionId, fixtureAssertionId.Trim());
        Assert.Matches("^[a-z0-9_-]+$", fixtureAssertionId);
        string identifier = Regex.Replace(fixtureAssertionId, "[^a-z0-9]+", "-").Trim('-');
        Assert.Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$", identifier);
        return identifier;
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.True(keys.Add(property.Name), "Duplicate JSON key '" + property.Name + "'.");
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

    private static void AssertNoHostPaths(JsonElement value)
    {
        foreach (string text in EnumerateStrings(value))
        {
            Assert.DoesNotContain("C:\\", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:/", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
            Assert.DoesNotContain("AppData", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            yield return value.GetString() ?? string.Empty;
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                foreach (string text in EnumerateStrings(property.Value))
                {
                    yield return text;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                foreach (string text in EnumerateStrings(item))
                {
                    yield return text;
                }
            }
        }
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);
    private sealed record CaseBinding(string Code, string CaseId, string CaseSha256, string PythonFactsSha256, int TargetCount);
    private sealed record ExpectedTarget(int InventoryIndex, string Symbol, string Classification, string AssertionId, string Adaptation, string NativeRoute, string CaseId);
    private sealed record TargetBinding(int InventoryIndex, string Symbol, string Kind, string SymbolHash, string SignatureHash, string BodyHash, string Classification, string AssertionId, string Adaptation, string NativeRoute, string CaseId);
    private sealed record NativeObservation(string Code, string CaseId, string[] Facts, string FactsSha256);
    private sealed record NativePin(string Code, int FactCount, string FactsSha256);
    private sealed record OracleCorpus(TargetBinding[] Targets);
}
