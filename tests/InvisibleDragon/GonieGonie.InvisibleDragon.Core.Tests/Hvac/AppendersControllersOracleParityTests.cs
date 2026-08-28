#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.InvisibleDragon.Tests.Model;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class AppendersControllersOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-hvac-appenders-controllers-oracle.json";
    private const int FixtureBytes = 179_542;
    private const string FixtureSha256 =
        "sha256:2d5034714366592c720d0872b616e409f62f50362abc58c48d970b904eb4b054";
    private const string FixtureSchema =
        "goniegonie.python-reference.dragon-hvac-appenders-controllers.v1";
    private const string FixtureRepositoryCommit = "d14de9e";
    private const string CasesSha256 =
        "sha256:2282854918bee238667f1307ecbdf21fa79ff7ceb305810622e6827afec7dd3d";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_hvac_appenders_controllers_oracle.py";
    private const int GeneratorBytes = 77_285;
    private const string GeneratorSha256 =
        "sha256:00da10485dbd576286b222a016171390199d6148b99c1e45f64c1b5eaa63ad31";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_hvac_appenders_controllers_oracle.py";
    private const int ValidatorBytes = 28_120;
    private const string ValidatorSha256 =
        "sha256:253e64cd09b57af1dfcb00bf164d49586af6713119dbbd97d3e60dab95074dcf";

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
        "sha256:5228c06e02e371e4da5106bb10ba5e2159bd38b452ecdb2be459245c318f2495";
    private const string SupportReceiptsSha256 =
        "sha256:88586a379f20f459fe1500bdc3ec4843aa161e11ac2e4426eeed81754f59c052";
    private const string DeferredReceiptsSha256 =
        "sha256:2172f41f390f28cc737f78b6e476876c04fda668a64901778bcaf2199393b62e";
    private const string FullSourceReceiptsSha256 =
        "sha256:f5db7f1a79890387192db20619e055691700f48bfbe368efeffbe37b695593e7";
    private const string LoadedModulesSha256 =
        "sha256:93cfad21e009eac906a4443998ad214eec82e2136ada5b7cea7888ababf30143";
    private const string RelocatedObservationsSha256 =
        "sha256:87cf389f96bf8041e9dca0b22291a465aff5715e209d190a175b03a70cbf7d65";
    private const string RuntimeDependenciesSha256 =
        "sha256:f69d29212b5ce6432b0c02f356d036275ea01463a8e1974ac6f89b78854fefba";
    private const string RuntimeSignaturesSha256 =
        "sha256:f44e1bfd639b1c59739524d9d795d6fba96336affacb4d7fa20104b0c8a2c1d5";
    private const string NativeSourceReceiptsSha256 =
        "sha256:b82b2435acc2802eb55647a47650b135b82f1198b8d504dc1a2b710ab977cfa4";
    private const string NativeRoutesSha256 =
        "sha256:f9407672b46a4049a13ee55e7f418e5b74eb13ac5f3aea70a67b9c430406aef3";
    private const string ClassificationSha256 =
        "sha256:b2abc7395c99e45184fc69f8cccc4f8215a44dc5d683fd7297ba4da43cfcf60e";

    private const string PublicRoute =
        "GonieGonie.InvisibleDragon.Hvac.SupplyGroup -> " +
        "GonieGonie.InvisibleDragon.Hvac.ZoneHvacAssignment -> " +
        "GonieGonie.InvisibleDragon.Model.EnergyModel -> " +
        "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) -> " +
        "GonieGonie.InvisibleDragon.Idf.IdfDocument";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Hvac.AppendersControllersOracleParityTests.MatchesPinnedAppendersControllersThroughPublicAggregateRoute";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_582,
            "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/SupplySystems.cs", 18_267,
            "sha256:4de030455a8a1b8db0ca4eca7745c6501930c984f9d1e156e17cb0b752d845cf"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs", 22_015,
            "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_764,
            "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/IdfModel.cs", 13_182,
            "sha256:50aa8a362214d34bba37dcf51ef3c0cce89d54895110a0da786c11d8fe233495"),
    };

    private static readonly ArtifactPin[] SupportArtifacts =
    {
        new("tools/python-reference/generate_dragon_hvac_supply_core_oracle.py", 65_898,
            "sha256:3f1bcbf28df62c3426f8d343dab3f123b9c730bcdd234e3c570aaff21b87cd97"),
        new("fixtures/reference/python-0.7.0/dragon-hvac-supply-core-oracle.json", 215_698,
            "sha256:dcf355329a083f9fac82434e18fc3b847a44bc134eb7f593f497c0aeae4c6b9f"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "dragon-hvac-appenders-controllers.demand-branch-appender", "demand-branch",
            "sha256:ed597ff707a9e05f6e13272a2046862ddd012e9cc3e4153b88911e74145dfc03",
            "sha256:ad7d55e8192bef04c3f6509932e8760791f0bf8cc5bd67a81de7a1d80638ab53", 7),
        new("B01", "dragon-hvac-appenders-controllers.equipment-list-appender", "equipment-list",
            "sha256:be089ecb06d8b1831af4ccc2f1f47fabc14cbaec81a98418e8cd9253c20999a9",
            "sha256:6b292e7f7e56bb7a01ae1f5c43f16bad2bbd4f0c4592272546e5fa3e907e1f83", 3),
        new("C01", "dragon-hvac-appenders-controllers.sequential-load-fraction-controller", "sequential-controller",
            "sha256:6db87a69044821b11b27016abd09eb7a0dffdc2764d66e71ce8a98cef2ef2fda",
            "sha256:d72589acfc903f580d3d4fb0f32942cc3e7178b5409e79e829b65c9e72b8f16c", 3),
        new("D01", "dragon-hvac-appenders-controllers.supply-system-postprocessor", "postprocessor-base",
            "sha256:a9532aee0ed8dcee59d6c507a16bddde1e8e02a6e83e7d27a11a9e3831cf5ee8",
            "sha256:200eee5adb57d9dbaac68ccff8b4cf34319881d280bbbd5ac50baee628947a90", 4),
        new("E01", "dragon-hvac-appenders-controllers.zone-air-node-appender", "zone-air-node",
            "sha256:01734b8966710d9079ca632b73b357feafde8b4ca4a7bc41d6e4171f052dac93",
            "sha256:54ee0a2d373e153cf6a7ca02a361841b34e273999ba3cf0be2e97560b3496337", 4),
        new("F01", "dragon-hvac-appenders-controllers.zone-terminal-unit-appender", "zone-terminal-unit",
            "sha256:0ef5b24143176c6dbc201a4477fe79c0008b2ba48277c179ae6f1ed9c6450e58",
            "sha256:a77be5dbc43bf4b51ba33fd30573a8ec478281857ec5593fb78c68324436dddb", 3),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        new(686, "DemandBranchAppender", "dragon-hvac-appenders-controllers-686-cdbb9fb8", "public-aggregate-hvac-postprocessing-686", Cases[0].CaseId),
        new(687, "DemandBranchAppender.append_to_branchlist", "dragon-hvac-appenders-controllers-687-72c53b16", "public-aggregate-hvac-postprocessing-687", Cases[0].CaseId),
        new(688, "DemandBranchAppender.append_to_mixer", "dragon-hvac-appenders-controllers-688-bf8d6bd1", "public-aggregate-hvac-postprocessing-688", Cases[0].CaseId),
        new(689, "DemandBranchAppender.append_to_spliter", "dragon-hvac-appenders-controllers-689-29bdd382", "public-aggregate-hvac-postprocessing-689", Cases[0].CaseId),
        new(690, "DemandBranchAppender.count_current_branches_branchlist", "dragon-hvac-appenders-controllers-690-2fb35691", "public-aggregate-hvac-postprocessing-690", Cases[0].CaseId),
        new(691, "DemandBranchAppender.count_current_branches_connector", "dragon-hvac-appenders-controllers-691-4531204e", "public-aggregate-hvac-postprocessing-691", Cases[0].CaseId),
        new(692, "DemandBranchAppender.run", "dragon-hvac-appenders-controllers-692-3d176f2c", "public-aggregate-hvac-postprocessing-692", Cases[0].CaseId),
        new(717, "EquipmentListAppender", "dragon-hvac-appenders-controllers-717-268e7fb5", "public-aggregate-hvac-postprocessing-717", Cases[1].CaseId),
        new(718, "EquipmentListAppender.count_current_equipments", "dragon-hvac-appenders-controllers-718-ef88aa10", "public-aggregate-hvac-postprocessing-718", Cases[1].CaseId),
        new(719, "EquipmentListAppender.run", "dragon-hvac-appenders-controllers-719-9fbbb80e", "public-aggregate-hvac-postprocessing-719", Cases[1].CaseId),
        new(774, "SequentialLoadFractionController", "dragon-hvac-appenders-controllers-774-35b327ea", "public-aggregate-hvac-postprocessing-774", Cases[2].CaseId),
        new(775, "SequentialLoadFractionController.find_target_equipment_number", "dragon-hvac-appenders-controllers-775-5a959bd0", "public-aggregate-hvac-postprocessing-775", Cases[2].CaseId),
        new(776, "SequentialLoadFractionController.run", "dragon-hvac-appenders-controllers-776-efc3dc2d", "public-aggregate-hvac-postprocessing-776", Cases[2].CaseId),
        new(804, "SupplySystemToIdfPostProcessor", "dragon-hvac-appenders-controllers-804-9b4492ed", "public-aggregate-hvac-postprocessing-804", Cases[3].CaseId),
        new(805, "SupplySystemToIdfPostProcessor.__init__", "dragon-hvac-appenders-controllers-805-c63e7515", "public-aggregate-hvac-postprocessing-805", Cases[3].CaseId),
        new(806, "SupplySystemToIdfPostProcessor.run", "dragon-hvac-appenders-controllers-806-582cf3ef", "public-aggregate-hvac-postprocessing-806", Cases[3].CaseId),
        new(807, "SupplySystemToIdfPostProcessor.source", "dragon-hvac-appenders-controllers-807-ce40cc13", "public-aggregate-hvac-postprocessing-807", Cases[3].CaseId),
        new(808, "ZoneAirNodeAppender", "dragon-hvac-appenders-controllers-808-bff1883c", "public-aggregate-hvac-postprocessing-808", Cases[4].CaseId),
        new(809, "ZoneAirNodeAppender.count_current_nodes", "dragon-hvac-appenders-controllers-809-fc0fbad2", "public-aggregate-hvac-postprocessing-809", Cases[4].CaseId),
        new(810, "ZoneAirNodeAppender.ensure_nodelist_existence", "dragon-hvac-appenders-controllers-810-e3fabdb5", "public-aggregate-hvac-postprocessing-810", Cases[4].CaseId),
        new(811, "ZoneAirNodeAppender.run", "dragon-hvac-appenders-controllers-811-7cd1f8e9", "public-aggregate-hvac-postprocessing-811", Cases[4].CaseId),
        new(812, "ZoneTerminalUnitAppender", "dragon-hvac-appenders-controllers-812-4ae86427", "public-aggregate-hvac-postprocessing-812", Cases[5].CaseId),
        new(813, "ZoneTerminalUnitAppender.count_current_units", "dragon-hvac-appenders-controllers-813-fc0fbad2", "public-aggregate-hvac-postprocessing-813", Cases[5].CaseId),
        new(814, "ZoneTerminalUnitAppender.run", "dragon-hvac-appenders-controllers-814-46d42798", "public-aggregate-hvac-postprocessing-814", Cases[5].CaseId),
    };

    private static bool DiscoverPins => false;

    private static readonly NativePin[] ExpectedNativePins =
    {
        new("A01", 11, "sha256:8fae8d564dcb1c8b5d348db06be47f8c61bfbba6fa6f26347d5ef6fb8f23af95"),
        new("B01", 10, "sha256:c92bf25dd54841d552278918d03f95555f1b1a8148167fd5ea4a32aca786f2a0"),
        new("C01", 10, "sha256:a94afc3f4dd129e56bc6e87dd96e434e68c0c193ef83d9f7358fa9d20278935e"),
        new("D01", 10, "sha256:8c5e971572f48d3c8a91141342f2f43721467eaf137bf2a781fc0c774e49ea62"),
        new("E01", 10, "sha256:c8f6283b3358f7074b1aa73531e0150acf4926c269bab6dc462c5a7bcd6ecc7d"),
        new("F01", 9, "sha256:923bb43987e571135467cc544cde1b9089c01aaf87c27500066767aae02de212"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:f9558f0bd716b64491bdb4ca0849da8c8eae5a999a72f7b279b50128c9efc646", // 686 DemandBranchAppender
        "sha256:9b55b9ca6e938630161a286ab6826813822065588434b3479b0a27ee575be577", // 687 DemandBranchAppender.append_to_branchlist
        "sha256:3f92bcc54df0d1444b3d5e9113ee6eed77250dd36d41ed337e3265a5ab77965f", // 688 DemandBranchAppender.append_to_mixer
        "sha256:fd06c3eddbda57b305f1ed419c8f0f2832df9d77a70e090eeaa3774197e51625", // 689 DemandBranchAppender.append_to_spliter
        "sha256:5b03c6a5540f8ab420689585c79dfdeefd67715f4ab39789b451219f95cce024", // 690 DemandBranchAppender.count_current_branches_branchlist
        "sha256:16188c2b077773a129a8308efe20818c12fc61d6a3d95a253f54cf67dd7a26eb", // 691 DemandBranchAppender.count_current_branches_connector
        "sha256:c5ba2a105cd2cc229853028f4ba821ec8f2afa1b6db508ca37784c4caa0acb9a", // 692 DemandBranchAppender.run
        "sha256:ead9109a17bf184c92b0e91722ef2084b57306772728893671a05046df9b68bf", // 717 EquipmentListAppender
        "sha256:3225344c89e812c666dbc8180cb57576c26b5471104d769c0864589cf926e860", // 718 EquipmentListAppender.count_current_equipments
        "sha256:d347c660e8428636b4b86cce79a77758f41e5a7a3b07f8244ca4fe239cc31eaf", // 719 EquipmentListAppender.run
        "sha256:1571ab6f1a186168e0df3c4241e5c505871b75261a2fc0886706789e9a6d7b93", // 774 SequentialLoadFractionController
        "sha256:bd7c5a76b01c491c69b49e2cea58d9272dbebd1e9aded669cd6712dfcb2732cb", // 775 SequentialLoadFractionController.find_target_equipment_number
        "sha256:001ee32660f4721c5c8892a4edbcd6a41bf3f4602dea4e02ff3a4e4248e0a027", // 776 SequentialLoadFractionController.run
        "sha256:c147c87a91ba218114e1474eca6e799a40120f5b8c5862d7217656d9cd267d0b", // 804 SupplySystemToIdfPostProcessor
        "sha256:b77cd26879e8ceceee727547e6b4af33c6f47aca411e9a66c18d677e9ff30ed4", // 805 SupplySystemToIdfPostProcessor.__init__
        "sha256:1bcebc77c04fc668244f4159e8de5a603f56991cee98faedf8e5777554135aed", // 806 SupplySystemToIdfPostProcessor.run
        "sha256:60c80ba8dafbb39e98505bb68bc29b1fdb15ecb1fcf52d9021883be8f2946c44", // 807 SupplySystemToIdfPostProcessor.source
        "sha256:0ff15a0a58336ef3929425b79833d058638860cb62fed363fde9ab3ca354252e", // 808 ZoneAirNodeAppender
        "sha256:38809c1f55a404cfebf488714d28226ed4afa57f6e72d49960ac20f6efab948b", // 809 ZoneAirNodeAppender.count_current_nodes
        "sha256:3e9de1a0dca14626a77d8e927c9b56a35098859e4e22f5d25d8efe5691b658c6", // 810 ZoneAirNodeAppender.ensure_nodelist_existence
        "sha256:70b32d5d953a10fb1f28de51a958f4af5966b026b378f303f802b95b74c154c5", // 811 ZoneAirNodeAppender.run
        "sha256:da8aca7c21917e886de0b24e4a3e5e5d4f8b5ef9b693371dafb3718b67bd4825", // 812 ZoneTerminalUnitAppender
        "sha256:9b2d7f01e22bdc404dc9d85c53d9e57b26c5773c79d1ca65bfa3c04fdc72008d", // 813 ZoneTerminalUnitAppender.count_current_units
        "sha256:284d35f5c088e38bb31570cb8dc45d3cd40ee099ba0fb35dbb080989c6edcead", // 814 ZoneTerminalUnitAppender.run
    };

    [Fact]
    public void MatchesPinnedAppendersControllersThroughPublicAggregateRoute()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument fixture = ReadPinnedFixture();
        OracleCorpus corpus = ValidateFixture(fixture.RootElement);

        NativeObservation[] observations = Cases.Select(ObserveNativeCase).ToArray();
        Assert.Equal(Cases.Select(item => item.Code), observations.Select(item => item.Code));
        object[] receipts = corpus.Targets.Select(target => CreateReceipt(target, observations)).ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();

        if (DiscoverPins)
        {
            string factPins = string.Join(
                Environment.NewLine,
                observations.Select(item =>
                    $"        new(\"{item.Code}\", {item.Facts.Length}, \"{item.FactsSha256}\"),"));
            string receiptPins = string.Join(
                Environment.NewLine,
                corpus.Targets.Select((target, index) =>
                    $"        \"{receiptHashes[index]}\", // {target.InventoryIndex} {target.Symbol}"));
            throw new Xunit.Sdk.XunitException(
                "APPENDERS_CONTROLLERS_NATIVE_PINS" + Environment.NewLine +
                "CASES" + Environment.NewLine + factPins + Environment.NewLine +
                "RECEIPTS" + Environment.NewLine + receiptPins);
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].Code, observations[index].Code);
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }

        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
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

        Assert.Equal(24, recordCount);
        Assert.Equal(24, corpus.Targets.Length);
        Assert.Equal(24, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(corpus.Targets, item => Assert.Equal("exception", item.Classification));
        Assert.Equal(6, corpus.FixtureCases.Length);
    }

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin pin in NativeSources.Concat(SupportArtifacts))
        {
            AssertPinnedArtifact(pin.Path, pin.Bytes, pin.Sha256);
        }

        ConstructorInfo supplyGroupConstructor = Assert.Single(
            typeof(SupplyGroup).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.True(supplyGroupConstructor.IsPublic);
        ConstructorInfo assignmentConstructor = Assert.Single(
            typeof(ZoneHvacAssignment).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.True(assignmentConstructor.IsPublic);
        MethodInfo toIdf = Assert.IsAssignableFrom<MethodInfo>(typeof(EnergyModel).GetMethod(
            nameof(EnergyModel.ToIdfDocument),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(GonieGonie.InvisibleDragon.Idd.IddSchema), typeof(EnergyModelIdfOptions) },
            modifiers: null));
        Assert.Equal(typeof(IdfDocument), toIdf.ReturnType);
        Assert.Contains(typeof(IReadOnlyList<IdfObject>), typeof(IdfDocument).GetInterfaces());
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

        JsonElement caseHashes = root.GetProperty("case_sha256");
        JsonElement factHashes = root.GetProperty("fact_sha256");
        AssertKeys(caseHashes, Cases.Select(item => item.CaseId).ToArray());
        AssertKeys(factHashes, Cases.Select(item => item.CaseId).ToArray());
        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        for (int index = 0; index < Cases.Length; index++)
        {
            CaseBinding expected = Cases[index];
            JsonElement actual = fixtureCases[index];
            Assert.Equal(expected.Code, RequiredString(actual, "code"));
            Assert.Equal(expected.CaseId, RequiredString(actual, "id"));
            Assert.Equal(expected.Subfamily, RequiredString(actual, "subfamily"));
            Assert.Equal(expected.CaseSha256, RequiredString(caseHashes, expected.CaseId));
            Assert.Equal(expected.CaseSha256, CanonicalSha256(actual));
            JsonElement facts = actual.GetProperty("python").GetProperty("facts");
            Assert.Equal(expected.PythonFactsSha256, RequiredString(actual.GetProperty("python"), "facts_sha256"));
            Assert.Equal(expected.PythonFactsSha256, RequiredString(factHashes, expected.CaseId));
            Assert.Equal(expected.PythonFactsSha256, CanonicalSha256(facts));
            Assert.Equal(expected.TargetCount, actual.GetProperty("target_symbols").GetArrayLength());
        }

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        JsonElement inventory = upstream.GetProperty("inventory");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        JsonElement source = upstream.GetProperty("source");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));
        Assert.Equal(TargetReceiptsSha256, RequiredString(upstream, "target_receipts_sha256"));
        Assert.Equal(SupportReceiptsSha256, RequiredString(upstream, "resolved_support_receipts_sha256"));
        Assert.Equal(DeferredReceiptsSha256, RequiredString(upstream, "deferred_receipts_sha256"));
        Assert.Equal(FullSourceReceiptsSha256, RequiredString(upstream, "full_source_receipts_sha256"));
        JsonElement isolated = upstream.GetProperty("isolated_import");
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal("two-byte-identical-repository-temp-copies", RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(LoadedModulesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(LoadedModulesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        Assert.Equal(RelocatedObservationsSha256, RequiredString(isolated, "relocated_observations_sha256"));

        JsonElement targetsElement = root.GetProperty("target_receipts");
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(targetsElement));
        JsonElement[] targetReceipts = targetsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, targetReceipts.Length);
        var targets = new TargetBinding[targetReceipts.Length];
        for (int index = 0; index < targetReceipts.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement actual = targetReceipts[index];
            Assert.Equal(expected.InventoryIndex, actual.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(UpstreamPath, RequiredString(actual, "path"));
            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                RequiredString(actual, "kind"),
                RequiredString(actual, "symbol_hash"),
                RequiredString(actual, "signature_hash"),
                RequiredString(actual, "body_hash"),
                expected.AssertionId,
                "exception",
                expected.AdaptationId,
                PublicRoute,
                expected.CaseId);
        }

        JsonElement[] symbols = root.GetProperty("symbols").EnumerateArray().ToArray();
        Assert.Equal(targetReceipts.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            Assert.Equal(RequiredString(targetReceipts[index], "symbol"), RequiredString(symbols[index], "symbol"));
            Assert.Equal(RequiredString(targetReceipts[index], "symbol_hash"), RequiredString(symbols[index], "symbol_hash"));
        }

        ValidateContract(root.GetProperty("consumer_contract"), targets);
        ValidateSupport(root);
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateNativeReview(root.GetProperty("native_review"));
        return new OracleCorpus(fixtureCases, targets);
    }

    private static void ValidateContract(JsonElement contract, IReadOnlyList<TargetBinding> targets)
    {
        Assert.Equal(6, contract.GetProperty("case_count").GetInt32());
        Assert.Equal(Cases.Select(item => item.CaseId), ReadStringArray(contract.GetProperty("case_ids")));
        JsonElement counts = contract.GetProperty("classification_counts");
        Assert.Equal(0, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(24, counts.GetProperty("exception").GetInt32());
        Assert.Equal(RuntimeSignaturesSha256, CanonicalSha256(contract.GetProperty("runtime_signatures")));

        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement routes = contract.GetProperty("native_routes");
        JsonElement coverage = contract.GetProperty("coverage_by_symbol");
        JsonElement expectations = contract.GetProperty("expectations");
        AssertKeys(classifications, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(assertions, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(adaptations, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(routes, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(coverage, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(expectations, targets.Select(item => item.Symbol).ToArray());
        foreach (TargetBinding target in targets)
        {
            Assert.Equal("exception", RequiredString(classifications, target.Symbol));
            Assert.Equal(target.AssertionId, RequiredString(assertions, target.Symbol));
            Assert.Equal(target.AdaptationId, RequiredString(adaptations, target.Symbol));
            Assert.Equal(PublicRoute, RequiredString(routes, target.Symbol));
            Assert.Equal(target.CaseId, RequiredString(coverage, target.Symbol));
            JsonElement expectation = expectations.GetProperty(target.Symbol);
            Assert.Equal("exception", RequiredString(expectation, "classification"));
            Assert.Equal(target.AssertionId, RequiredString(expectation, "assertion_id"));
            Assert.Equal(target.AdaptationId, RequiredString(expectation, "adaptation"));
            Assert.Equal(PublicRoute, RequiredString(expectation, "native_route"));
        }

        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("exact_disjoint_source_partition").GetBoolean());
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_hvac_source_partition").GetBoolean());
        Assert.False(closure.GetProperty("target_support_overlap").GetBoolean());
        Assert.Equal(24, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(1, closure.GetProperty("resolved_support_count").GetInt32());
        Assert.Equal(149, closure.GetProperty("deferred_count").GetInt32());
        Assert.Equal(174, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("target_indices")));
        Assert.Equal(new[] { 796 }, ReadIntArray(closure.GetProperty("resolved_support_indices")));
        Assert.Equal(new[] { "SupplyGroup.to_idf_object" }, ReadStringArray(closure.GetProperty("resolved_support_symbols")));
        Assert.Equal(149, closure.GetProperty("deferred_indices").GetArrayLength());
        Assert.Equal(SupportReceiptsSha256, RequiredString(closure, "resolved_support_receipts_sha256"));
        Assert.Equal(DeferredReceiptsSha256, RequiredString(closure, "deferred_receipts_sha256"));
        Assert.Equal(FullSourceReceiptsSha256, RequiredString(closure, "full_source_receipts_sha256"));

        JsonElement evidence = contract.GetProperty("evidence_contract");
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
        Assert.True(evidence.GetProperty("resolved_index_796_reused_from_support").GetBoolean());
        Assert.False(evidence.GetProperty("internal_native_route_claim").GetBoolean());
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
    }

    private static void ValidateSupport(JsonElement root)
    {
        JsonElement support = root.GetProperty("support");
        Assert.Equal("immutable-index-796-supply-group-conversion-support-only", RequiredString(support, "role"));
        Assert.Equal("goniegonie.python-reference.dragon-hvac-supply-core.v1", RequiredString(support, "schema"));
        Assert.False(support.GetProperty("target_promoted").GetBoolean());
        Assert.Equal(9, support.GetProperty("case_count").GetInt32());
        Assert.Equal("sha256:29eacb2d29f528353302d1afd8e3ef646d7d35886237bb4a3fa494039a4ec36f", RequiredString(support, "cases_sha256"));
        ValidateArtifact(support.GetProperty("generator"), SupportArtifacts[0]);
        ValidateArtifact(support.GetProperty("fixture"), SupportArtifacts[1]);
        Assert.Equal(SupportReceiptsSha256, RequiredString(support, "resolved_receipts_sha256"));
        Assert.Equal(SupportReceiptsSha256, CanonicalSha256(support.GetProperty("resolved_receipts")));
        Assert.Equal(SupportReceiptsSha256, CanonicalSha256(root.GetProperty("resolved_support_receipts")));
        JsonElement receipt = Assert.Single(root.GetProperty("resolved_support_receipts").EnumerateArray());
        Assert.Equal(796, receipt.GetProperty("inventory_index").GetInt32());
        Assert.Equal("SupplyGroup.to_idf_object", RequiredString(receipt, "symbol"));
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal(RuntimeDependenciesSha256, RequiredString(runtime, "dependencies_sha256"));
        Assert.Equal(RuntimeDependenciesSha256, CanonicalSha256(runtime.GetProperty("dependencies")));
    }

    private static void ValidateNativeReview(JsonElement review)
    {
        Assert.Equal(ClassificationSha256, RequiredString(review, "classification_sha256"));
        Assert.Equal(NativeRoutesSha256, RequiredString(review, "routes_sha256"));
        Assert.Equal(PublicRoute, RequiredString(review, "public_production_route"));
        Assert.True(review.GetProperty("public_production_routes_only").GetBoolean());
        Assert.False(review.GetProperty("internal_generate_route_claimed").GetBoolean());
        Assert.False(review.GetProperty("internal_postprocessor_type_route_claimed").GetBoolean());
        Assert.False(review.GetProperty("python_executes_native_runtime").GetBoolean());
        Assert.Equal(NativeSourceReceiptsSha256, RequiredString(review, "source_receipts_sha256"));
        Assert.Equal(NativeSourceReceiptsSha256, CanonicalSha256(review.GetProperty("source_receipts")));
        JsonElement[] sources = review.GetProperty("source_receipts").EnumerateArray().ToArray();
        Assert.Equal(NativeSources.Length, sources.Length);
        for (int index = 0; index < sources.Length; index++)
        {
            ValidateArtifact(sources[index], NativeSources[index]);
        }
    }

    private static NativeObservation ObserveNativeCase(CaseBinding item)
    {
        string[] facts = item.Code switch
        {
            "A01" => ObserveDemandBranches(),
            "B01" => ObserveEquipmentList(),
            "C01" => ObserveSequentialFractions(),
            "D01" => ObservePublicPostprocessingRoute(),
            "E01" => ObserveZoneAirNodes(),
            "F01" => ObserveZoneTerminalUnits(),
            _ => throw new InvalidOperationException("Unknown appender/controller case: " + item.Code),
        };
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        return new NativeObservation(item.Code, item.CaseId, facts, CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveDemandBranches()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-DB-ZONE", "Appender Demand Zone");
        var boiler = new Boiler(Id("APP-DB-BOILER"), "Appender Demand Boiler", Fuel.NaturalGas);
        var first = new Radiator(Id("APP-DB-RAD-1"), "Demand Radiator One", boiler, 3500);
        var second = new Radiator(Id("APP-DB-RAD-2"), "Demand Radiator Two", boiler, 4200);
        RouteResult route = CreateRoute("Appender demand route", zone, new SupplySystem[] { first, second });
        RouteResult repeated = CreateRoute("Appender demand route", zone, new SupplySystem[] { first, second });
        IdfObject[] branchLists = route.Document["BranchList"].ToArray();
        IdfObject[] splitters = route.Document["Connector:Splitter"].ToArray();
        IdfObject[] mixers = route.Document["Connector:Mixer"].ToArray();
        IdfObject demandList = Assert.Single(branchLists, item => item.Name!.EndsWith(" Demand BranchList", StringComparison.Ordinal));
        IdfObject demandSplitter = Assert.Single(splitters, item => item.Name!.EndsWith(" Demand Splitter", StringComparison.Ordinal));
        IdfObject demandMixer = Assert.Single(mixers, item => item.Name!.EndsWith(" Demand Mixer", StringComparison.Ordinal));
        Assert.True(demandList.Count >= 5);
        Assert.Equal(demandSplitter.Count, demandMixer.Count);
        return new[]
        {
            "systems=" + string.Join("|", route.Group.Systems.Select(item => item.Id.Value)),
            "branch-list-count=" + branchLists.Length,
            "demand-branch-list=" + Snapshot(demandList),
            "demand-branch-order=" + string.Join("|", demandList.Fields.Skip(1).Select(item => item.Value)),
            "splitter-count=" + splitters.Length,
            "demand-splitter=" + Snapshot(demandSplitter),
            "mixer-count=" + mixers.Length,
            "demand-mixer=" + Snapshot(demandMixer),
            "branch-count=" + route.Document["Branch"].Count,
            "repeat-idf-identical=" + (DocumentHash(route.Document) == DocumentHash(repeated.Document)),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveEquipmentList()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-EQ-ZONE", "Appender Equipment Zone");
        var first = new ElectricRadiator(Id("APP-EQ-RAD-1"), "Equipment Radiator One", 3100);
        var second = new ElectricRadiator(Id("APP-EQ-RAD-2"), "Equipment Radiator Two", 3200);
        RouteResult route = CreateRoute("Appender equipment route", zone, new SupplySystem[] { first, second });
        IdfObject list = Assert.Single(route.Document["ZoneHVAC:EquipmentList"]);
        IdfObject connection = Assert.Single(route.Document["ZoneHVAC:EquipmentConnections"]);
        int equipmentCount = (list.Count - 2) / 6;
        Assert.Equal(2, equipmentCount);
        Assert.Equal(first.ObjectNameFor(zone), list[3]);
        Assert.Equal(second.ObjectNameFor(zone), list[9]);
        return new[]
        {
            "equipment-list-name=" + list.Name,
            "load-distribution=" + list[1],
            "equipment-count=" + equipmentCount,
            "equipment-list-fields=" + Fields(list),
            "equipment-name-order=" + list[3] + "|" + list[9],
            "equipment-type-order=" + list[2] + "|" + list[8],
            "equipment-connection=" + Snapshot(connection),
            "public-equipment-object-count=" + route.Document["ZoneHVAC:Baseboard:RadiantConvective:Electric"].Count,
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveSequentialFractions()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-SEQ-ZONE", "Appender Sequential Zone");
        var first = new ElectricRadiator(Id("APP-SEQ-HEAT-1"), "Sequential Heat One", 3300);
        var second = new ElectricRadiator(Id("APP-SEQ-HEAT-2"), "Sequential Heat Two", 3400);
        var source = new HeatPump(Id("APP-SEQ-SOURCE"), "Sequential Cooling Source", Fuel.Electricity, 3.1, 2.9);
        var cooling = new PackagedAirConditioner(Id("APP-SEQ-COOL"), "Sequential Cooling", source);
        Schedule firstAvailability = Schedule.Constant("Sequential Availability One", 0, ScheduleType.OnOff);
        Schedule secondAvailability = Schedule.Constant("Sequential Availability Two", 1, ScheduleType.OnOff);
        Schedule coolingAvailability = Schedule.Constant("Sequential Cooling Availability", 1, ScheduleType.OnOff);
        SupplySystem[] systems = { first, second, cooling };
        Schedule?[] availabilities = { firstAvailability, secondAvailability, coolingAvailability };
        RouteResult route = CreateRoute("Appender sequential route", zone, systems, availabilities);
        RouteResult repeated = CreateRoute("Appender sequential route", zone, systems, availabilities);
        IdfObject list = Assert.Single(route.Document["ZoneHVAC:EquipmentList"]);
        Assert.Equal(3, (list.Count - 2) / 6);
        string[] fractionReferences = Enumerable.Range(0, 3)
            .Select(index => list[2 + (index * 6) + 4] + "|" + list[2 + (index * 6) + 5])
            .ToArray();
        IdfObject[] fractions = route.Document
            .Where(item => item.Name?.Contains("_fraction_for_", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.NotEmpty(fractions);
        return new[]
        {
            "system-capabilities=" + string.Join("|", systems.Select(item => item.CanHeat + ":" + item.CanCool)),
            "availability-order=" + string.Join("|", route.Group.Availabilities.Select(item => item?.Name ?? "<default>")),
            "equipment-count=" + ((list.Count - 2) / 6),
            "fraction-references=" + string.Join(";", fractionReferences),
            "alloff-reference-count=" + list.Fields.Count(item => item.Value == "ALLOFF"),
            "fraction-object-count=" + fractions.Length,
            "fraction-objects=" + string.Join(";", fractions.Select(Snapshot)),
            "repeat-idf-identical=" + (DocumentHash(route.Document) == DocumentHash(repeated.Document)),
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObservePublicPostprocessingRoute()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-POST-ZONE", "Appender Postprocess Zone");
        var system = new ElectricRadiator(Id("APP-POST-RAD"), "Postprocess Radiator", 3600);
        RouteResult route = CreateRoute("Appender postprocess route", zone, new SupplySystem[] { system });
        Assert.Same(route.Group, route.Assignment.Supply);
        Assert.Equal(zone.Id, route.Assignment.ZoneId);
        return new[]
        {
            "route=" + PublicRoute,
            "supply-group-type=" + route.Group.GetType().FullName,
            "assignment-type=" + route.Assignment.GetType().FullName,
            "model-type=" + route.Model.GetType().FullName,
            "document-type=" + route.Document.GetType().FullName,
            "assignment=" + route.Assignment.ZoneId.Value + "|" + ReferenceEquals(route.Group, route.Assignment.Supply),
            "document-is-readonly-list=" + (route.Document is IReadOnlyList<IdfObject>),
            "public-idf-object-count=" + route.Document.Count,
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveZoneAirNodes()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-NODE-ZONE", "Appender Air Node Zone");
        var source = new HeatPump(Id("APP-NODE-SOURCE"), "Appender Air Node Source", Fuel.Electricity, 3.2, 3.0);
        var system = new AirHandlingUnit(Id("APP-NODE-AHU"), "Air Node AHU", source);
        RouteResult route = CreateRoute("Appender air node route", zone, new SupplySystem[] { system });
        RouteResult repeated = CreateRoute("Appender air node route", zone, new SupplySystem[] { system });
        IdfObject connection = Assert.Single(route.Document["ZoneHVAC:EquipmentConnections"]);
        IdfObject[] nodeLists = route.Document["NodeList"].ToArray();
        string[] references = { connection[2], connection[3] };
        int resolved = references.Count(reference =>
            reference.Length > 0 && nodeLists.Any(item => string.Equals(item.Name, reference, StringComparison.Ordinal)));
        Assert.Equal(references.Count(item => item.Length > 0), resolved);
        return new[]
        {
            "node-list-count=" + nodeLists.Length,
            "node-list-names=" + string.Join("|", nodeLists.Select(item => item.Name)),
            "node-lists=" + string.Join(";", nodeLists.Select(Snapshot)),
            "equipment-connections=" + Snapshot(connection),
            "inlet-reference=" + connection[2],
            "exhaust-reference=" + connection[3],
            "resolved-node-list-references=" + resolved,
            "zone-air-node=" + connection[4],
            "repeat-idf-identical=" + (DocumentHash(route.Document) == DocumentHash(repeated.Document)),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveZoneTerminalUnits()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-TU-ZONE", "Appender Terminal Zone");
        var source = new HeatPump(Id("APP-TU-SOURCE"), "Appender Terminal Source", Fuel.Electricity, 3.2, 3.0);
        var first = new AirHandlingUnit(Id("APP-TU-AHU-1"), "Terminal AHU One", source);
        var second = new AirHandlingUnit(Id("APP-TU-AHU-2"), "Terminal AHU Two", source);
        RouteResult route = CreateRoute("Appender terminal route", zone, new SupplySystem[] { first, second });
        IdfObject terminalList = Assert.Single(route.Document["ZoneTerminalUnitList"]);
        IdfObject[] terminalUnits = route.Document["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"].ToArray();
        Assert.Equal(2, terminalUnits.Length);
        Assert.Equal(3, terminalList.Count);
        Assert.Equal(first.ObjectNameFor(zone), terminalList[1]);
        Assert.Equal(second.ObjectNameFor(zone), terminalList[2]);
        return new[]
        {
            "shared-source=" + string.Join("|", route.Group.Sources.Select(item => item.Id.Value)),
            "terminal-list-count=" + route.Document["ZoneTerminalUnitList"].Count,
            "terminal-list=" + Snapshot(terminalList),
            "terminal-unit-count=" + terminalUnits.Length,
            "terminal-unit-order=" + string.Join("|", terminalUnits.Select(item => item.Name)),
            "system-order=" + string.Join("|", route.Group.Systems.Select(item => item.Id.Value)),
            "equipment-list=" + Snapshot(Assert.Single(route.Document["ZoneHVAC:EquipmentList"])),
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static RouteResult CreateRoute(
        string modelName,
        Zone zone,
        IReadOnlyList<SupplySystem> systems,
        IReadOnlyList<Schedule?>? availabilities = null)
    {
        var group = new SupplyGroup(systems, availabilities);
        var assignment = new ZoneHvacAssignment(zone.Id, group);
        var model = new EnergyModel(modelName, new[] { zone }, new[] { assignment });
        IdfDocument document = model.ToIdfDocument();
        return new RouteResult(zone, group, assignment, model, document);
    }

    private static string Snapshot(IdfObject item) => item.ObjectType + "|" + Fields(item);

    private static string Fields(IdfObject item) => string.Join("|", item.Fields.Select(field => field.Value));

    private static string DocumentTypeCounts(IdfDocument document) => string.Join(
        ";",
        document.GroupBy(item => item.ObjectType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + "=" + group.Count()));

    private static string DocumentHash(IdfDocument document) =>
        Sha256(Encoding.UTF8.GetBytes(IdfWriter.Write(document)));

    private static object CreateReceipt(TargetBinding target, IReadOnlyList<NativeObservation> observations)
    {
        NativeObservation observation = Assert.Single(observations, item => item.CaseId == target.CaseId);
        CaseBinding fixtureCase = Assert.Single(Cases, item => item.CaseId == target.CaseId);
        return new
        {
            adaptation_id = target.AdaptationId,
            artifacts = new
            {
                fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
                generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
                native_sources = NativeSources.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                support = SupportArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
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
                    native_outcome = "public-aggregate-exception-as-pinned",
                    python_case_sha256 = fixtureCase.CaseSha256,
                    python_facts_sha256 = fixtureCase.PythonFactsSha256,
                },
            },
            scope = new
            {
                active_energyplus_process_claim = false,
                deferred_target_count = 149,
                equivalent_target_count = 0,
                exact_case_count = 6,
                exact_source_declaration_count = 174,
                exact_target_count = 24,
                exception_target_count = 24,
                fixture_repository_commit = FixtureRepositoryCommit,
                internal_native_route_claimed = false,
                public_aggregate_route_only = true,
                resolved_support_count = 1,
                standalone_postprocessor_api_claimed = false,
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

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoHostPaths(receipt);
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal("exception", RequiredString(receipt, "classification"));
        Assert.Equal(PublicRoute, RequiredString(receipt, "native_route"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));
        JsonElement source = receipt.GetProperty("source_receipt");
        Assert.Equal(target.InventoryIndex, source.GetProperty("inventory_index").GetInt32());
        Assert.Equal(target.Symbol, RequiredString(source, "symbol"));
        Assert.Equal(target.SymbolHash, RequiredString(source, "symbol_hash"));
        NativeObservation expected = Assert.Single(observations, item => item.CaseId == target.CaseId);
        JsonElement observed = Assert.Single(receipt.GetProperty("observations").EnumerateArray());
        Assert.Equal(expected.Code, RequiredString(observed, "case_code"));
        Assert.Equal(expected.CaseId, RequiredString(observed, "case_id"));
        Assert.Equal(expected.Facts.Length, observed.GetProperty("native_fact_count").GetInt32());
        Assert.Equal(expected.FactsSha256, RequiredString(observed, "native_facts_sha256"));
        Assert.Equal(expected.Facts, ReadStringArray(observed.GetProperty("native_facts")));
        JsonElement scope = receipt.GetProperty("scope");
        Assert.False(scope.GetProperty("internal_native_route_claimed").GetBoolean());
        Assert.False(scope.GetProperty("standalone_postprocessor_api_claimed").GetBoolean());
        Assert.True(scope.GetProperty("public_aggregate_route_only").GetBoolean());
        Assert.False(scope.GetProperty("structural_only").GetBoolean());
        Assert.Equal(24, scope.GetProperty("exact_target_count").GetInt32());
    }

    private static void ValidateArtifact(JsonElement actual, ArtifactPin expected)
    {
        Assert.Equal(expected.Path, RequiredString(actual, "path"));
        Assert.Equal(expected.Bytes, actual.GetProperty("bytes").GetInt32());
        Assert.Equal(expected.Sha256, RequiredString(actual, "sha256"));
    }

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

    private static EntityId Id(string value) => new(value);

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

    private static string[] ReadStringArray(JsonElement value) =>
        value.EnumerateArray().Select(item => Assert.IsType<string>(item.GetString())).ToArray();

    private static int[] ReadIntArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
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

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string CaseSha256,
        string PythonFactsSha256,
        int TargetCount);

    private sealed record ExpectedTarget(
        int InventoryIndex,
        string Symbol,
        string AssertionId,
        string AdaptationId,
        string CaseId);

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
        string CaseId);

    private sealed record NativeObservation(string Code, string CaseId, string[] Facts, string FactsSha256);

    private sealed record NativePin(string Code, int FactCount, string FactsSha256);

    private sealed record OracleCorpus(JsonElement[] FixtureCases, TargetBinding[] Targets);

    private sealed record RouteResult(
        Zone Zone,
        SupplyGroup Group,
        ZoneHvacAssignment Assignment,
        EnergyModel Model,
        IdfDocument Document);
}
