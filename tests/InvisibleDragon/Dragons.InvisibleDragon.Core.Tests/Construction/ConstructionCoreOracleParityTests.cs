using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.InvisibleDragon.Construction;
using Dragons.UpstreamTracker;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;

namespace Dragons.InvisibleDragon.Tests.Construction;

public sealed class ConstructionCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-construction-core-oracle.json";
    private const int FixtureBytes = 395_318;
    private const string FixtureSha256 =
        "sha256:25f2c13f0999e50d9a5fc32a31dd7b60e3a8847c1363570cec21b7d107aa10e7";
    private const string FixtureSchema =
        "dragons.python-reference.dragon-construction-core.v1";
    private const string CasesSha256 =
        "sha256:fefa2bfd0adc759e513dd2f0a83907595ab4c0519eededa3ed24ffaeb38c3e7c";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_construction_core_oracle.py";
    private const int GeneratorBytes = 82_969;
    private const string GeneratorSha256 =
        "sha256:c24172f07f19a8f6b085aa214bb709f49a87e9b4dd3363ed4c734f262c5ebefd";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_construction_core_oracle.py";
    private const int ValidatorBytes = 20_232;
    private const string ValidatorSha256 =
        "sha256:1c8cf4739f07abfe83a6b1656acadb558f7926570f92ac114c1bc6ae41d93951";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";

    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/construction.py";
    private const int UpstreamBytes = 11_652;
    private const string UpstreamSourceSha256 =
        "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622";
    private const string UpstreamAstSha256 =
        "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a";

    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Construction.ConstructionCoreOracleParityTests.MatchesPinnedDragonConstructionCoreThroughTypedNativeRoutes";

    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/ISurfaceConstruction.cs", 216,
            "sha256:d960c1de9896e2b634df27979713fb484030abfc79f14d38e68286041df3e6a7"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/Construction.cs", 1_750,
            "sha256:85f4e6d46ef7eaed3d68ab7699508c7182aa50f16e1e5f4f5079dca56d616795"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/Layer.cs", 2_040,
            "sha256:381d4b41f654233414e59710685120d7832363552dd80f9cc43d371de2109475"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/Material.cs", 3_441,
            "sha256:dd3027aaed2917eb78120f4280b86ff1e510627632d913a289d52690470112fe"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/MaterialRoughness.cs", 244,
            "sha256:76f351811fa91d043275601a688e583d5ca9bb5c74a16a55e7c595c7710f0012"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/SimpleConstructions.cs", 2_019,
            "sha256:a72caa2d2c70ea18bf080bf623837ef3a0c7869a4991a7977255b00021d9e762"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Internal/DomainGuard.cs", 2_413,
            "sha256:5bb42189e091fb4ed17f3e242a0e22c32b47b2242c2e6b9a43da46ecaa929ac4"),
    };

    private static readonly CaseDefinition[] Cases =
    {
        Case("C01", "c01-roughness-topology-order-values", "roughness"),
        Case("C02", "c02-roughness-strings", "roughness"),
        Case("C03", "c03-roughness-construction-invalid", "roughness"),
        Case("C04", "c04-material-default-state", "material"),
        Case("C05", "c05-material-explicit-mutation", "material"),
        Case("C06", "c06-material-type-range-nonfinite", "material"),
        Case("C07", "c07-layer-state-derived", "layer"),
        Case("C08", "c08-layer-mutation", "layer"),
        Case("C09", "c09-layer-type-range-nonfinite", "layer"),
        Case("C10", "c10-construction-layer-overload-metrics", "construction"),
        Case("C11", "c11-construction-material-thickness-overload", "construction"),
        Case("C12", "c12-construction-reverse-order-alias", "construction"),
        Case("C13", "c13-construction-empty-mixed-mutation", "construction"),
        Case("C14", "c14-glazing-state", "glazing"),
        Case("C15", "c15-glazing-mutation", "glazing"),
        Case("C16", "c16-glazing-type-range-nonfinite", "glazing"),
        Case("C17", "c17-no-mass-state", "no-mass"),
        Case("C18", "c18-no-mass-mutation", "no-mass"),
        Case("C19", "c19-no-mass-type-range-nonfinite", "no-mass"),
    };

    private static readonly TargetBinding[] Targets = CreateTargets();

    private static readonly NativePin[] ExpectedNativePins =
    {
        new(7, "sha256:2c77d222952e80db0fb06e38e3f5f93371b242d055b6f869b1fa1fd5b24b8ed3"),
        new(4, "sha256:ee0b0523b25fb3f8bfd5b51445fda4f58becff271633f6fd6ca1f7d43af6d9a2"),
        new(6, "sha256:2dce95a8dad351b6a96e04047308ddc71fae4ef030bd11f0129f58e9fbc46d5b"),
        new(5, "sha256:1d3fcd26903b38f99949a7c67d289aa2863f5696fdac241d53df740a039167d7"),
        new(6, "sha256:b871aca196cba5684010607c321fd1c9f5a88d3e2007f987d61f898ec6208cd3"),
        new(15, "sha256:84808cd94e722907b6a984f7f9fc55007b773bb00dbfc174063bd37ef57d29a2"),
        new(6, "sha256:e719814edba1e3dc2dac77fc3577899110aaa4e8a16cc3f0218cfa3208d0f4e1"),
        new(6, "sha256:2755912e2e336ebc5f4b3439ef2b833854c402e43aa72ada7a94a2219fa1ea71"),
        new(8, "sha256:7cd429c64fcd4b0d704c5faa3726dab7a65723e1415aa8450934d89429c625a5"),
        new(7, "sha256:ae86069aca3f90420d4735131cbb6c3cacf7770902c5a2b8c24e550f0aebe686"),
        new(9, "sha256:f6173166ac9b6a69e7639e0ea2cc48a1babd0fa62a1d8387bcbdb6828526389e"),
        new(9, "sha256:e1cf2cfdfb1d770a0e5460b78b084e1785bf98875b40f08a118682ace68dd660"),
        new(12, "sha256:cc97b8708d738a8d627bedb459c2dd5a3d599dcebd5239d1890b95770f2db6aa"),
        new(5, "sha256:9d630b2ad64469c8e542d078f995ad5f57b5274a034fc476f37263c964ff5944"),
        new(6, "sha256:d010f8b53d67101bc792cf29a045aeac47cfff57205b3ba5a576b66fafdb83da"),
        new(12, "sha256:fe49a2121bcc06cea68cfdb0c34c151b4544abaade876c9b901cee4f7827e11d"),
        new(5, "sha256:6ca2d1202a87da627a9e377ca2c39c24c3c943251b49c3068b43bd99802b5a46"),
        new(6, "sha256:44fe00cb5d9d7a38f5ab45213a6b4b500cb4a29c9442b5ad5749006e4aa32d70"),
        new(7, "sha256:205ee220ec5a3c25b0a26af4dafb925162500e035c55c985aaaf28b437d11c7e"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:d1c71b4af4d1d24d58c0d9782203e382b6a01aea96629c93a1fede5e4eb6719a",
        "sha256:30d2a1ca7dbafd4b9534102328e7ad509f214695bedf7c7498a4ebfd07552958",
        "sha256:0c532fefbe2a7191ca7f35840eb363aa661a058f4963aa863d63b7c68df53486",
        "sha256:06f63ed28372dc6612f5dc665bad789747494e93542e7c2f9faed9d04b0e2bf8",
        "sha256:deb9e6a584852d29d435cd9b2c685b5b2c5c492d997dd85afae6f661c1d0a1da",
        "sha256:2e3933c1acf3d24acc063d55c31c3b3f295c5194dfc818b0a65f11d7a8410c9e",
        "sha256:a059543e2f18d0803434034efecf80f03d0973ddfd69afd4c36329b3fad5cca2",
        "sha256:9890ac92971dd2f30d1456df14fefdc4b66b2227bccf9740315702b8df41309d",
        "sha256:5772e250992e1ecf8ebb1a2f154fc2c3fa07a5d749c3b1e58df5b9324c9fc36e",
        "sha256:bf2befee765c4eebf41cdb70065234500c56baa84c4f0ca5f36644abb90f6ff7",
        "sha256:fc852da660877b48d732c5350941be70ff66e476d96c5668279eec0dd54868d7",
        "sha256:6b8d58573d5b95e35809ce90130bd77b7e54fdb12dc35a5cac6f621fc46f5e92",
        "sha256:ed46209a09c534dbd3594d0bb3cbbb07c0c763510c82e902ff15945ccdf77f15",
        "sha256:50723ffc9b9ae9e42c6185994377d2b801708579817fa694689f65edfbc3713d",
        "sha256:03354a48eb73c4b042ca57df3d40fbc42635c3c7135a683837e570a3fffb6140",
        "sha256:b31be2ae14a7b4047f5a5b3366bf17856ee33330b3eb387c66c482f00658d5ce",
        "sha256:05c9c51485da24c8d49f024a8d7101c7cde7f134072d0cb53587ed265d902658",
        "sha256:45c26be4ec4bcc0ffd96c5b16542db370b37e82f61cfb4527901787a3190475b",
        "sha256:c883318b15009a4a6634858593aaf93ccfa06866e2231326beff6b498d3705b9",
        "sha256:a2d6e2a01dabd6dc8d1371195d94f747b8a08c795bc64223f4003ebba8a1767b",
        "sha256:bc0ac9b63291e57adcccdd5aada2c8678a5c02ae331ea69268eefb604b076e6e",
        "sha256:f0f038de2cda3eabf4b818bfc61298268af62b71b892257420c1809d4cbf42b5",
        "sha256:1f8c96489ac8dd3d49114debfce2fb8600074bf2f2c9408bbda3e00d2295b58e",
        "sha256:5c013a0918fedf1574cdb1444a30c68c87721dd7c5cc5dbfddd5acfc61ef3c48",
        "sha256:277d020f916524ec77dc9beaf65a7d8fbeac5d7fa8ed4095097b5fa781f957df",
        "sha256:d0fd1a3a01d19a4a9c6ed24cafc00a24f591aaaa7f79029c9436bd526b034614",
        "sha256:2db9b628fe3fad0498dc86b081917ee37f1648ae2bc68cd16ec5fc73e68925d6",
        "sha256:3e28bb389620a3414cd7f478b8d617e41891cd75497db9ca8c999e9a8ab9bdb1",
        "sha256:9c0b84440cb73280b9b599a7f9699777db262e6f661df4f7e794116a166ed7b6",
        "sha256:3d63dec6c5402bb4a7affe3bff3e9d1fa36110b0a165ae7e7acde569edfdc59f",
        "sha256:e15faf5a4453e5c607d368c2fd08290ff440f84b0277de2018e1a84837d31d81",
        "sha256:690354458de70dcb27ff274bf5f671461e513f8272e526c39b7a554d9ebff632",
        "sha256:27b68e8465d5754a93951b5230419cfb7aaeff2df8d642c338726c1d222e8e42",
        "sha256:a2af0947c6483cf1bb667b5d91cecedfabc7e15333eaa338452907e203c8bae2",
        "sha256:aec861e0e8028619b4afa3b76fbc143fb2b06777d7e9afe0ea7ca58257de41f4",
    };

    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:144a4b47a0eeef8b365ae239c6d3dca9fe0b9b67414ea6d411d93bb717d37a22",
        "sha256:5dff226a2f11f9d42cb50e6d542aab8dd84a0dd262d3e8afaaff21bf0a11a113",
        "sha256:0c2bd35b2ca8c760464efb9fa4cd602f53c12ac143b9800840a8e373ea9a32a2",
        "sha256:3ffebdf65a9fbc1a1c2c15e96160e5dfa7b9454466ea9ceff63149f2ba2488a9",
        "sha256:f57421b459af2f876519f3daadf05cd598a03b12cd9f06a7b1ba19bd69455a3a",
        "sha256:346b6af22ae3e8e5e5bbb6f82570e1fa5f47b2c8bb15b26a8d81e6cb15959959",
        "sha256:abe4aa7e92361b952f4fac0fe71da7e587c016ad80895806aa8e36f37cdef312",
        "sha256:3397c6abe98d6c43e8548264e916f1c7057aa8f9523b2c0c6f1f927561220651",
        "sha256:291d0750e35026ab81a06543ebac68626222a42ef34db588b0c99239588b96c8",
        "sha256:c716e866a94ba92720ef5a8122eb66861b232a70cd7e20b40b3091e0ecc2fc18",
        "sha256:41841cccc6db9f95aea4582ff289de6cf20d16e2f0abc310f7eb33a04ea48a8a",
        "sha256:5383c7ff063ca5400b9e3e93f20348d9e4638c7bee15afd517191e84bb7be04d",
        "sha256:0116ec051657d4565a12ea0402b8b217962780e2baa03d4f2f79fe855842391a",
        "sha256:1ae196c19c27380926ecad89a06abacb2b3b16a0b80360709b1dabd4ca7797d3",
        "sha256:919e4c7a33639b00f260b4c9a1e2c7b2833dbb505940957c179438875e2cebd1",
        "sha256:bc18346c0466bfb93ba73fc08ce9e8998589754a17ab390031aeac7a14f6cc90",
        "sha256:4fe09d7f2bf13253fd6113e29a2de5ea445daa40627209ca2a5f54380e47838d",
        "sha256:e99bb4c58a2767cccd3907d01f73e772ca84559b6af1afb91aea5dfe0ae77edd",
        "sha256:644b353a8283313e9d5d883be993501702daf374eb2e8073eb047d8ede875d72",
        "sha256:7b7e75ac887a695c1a75d9967cebffff488fcedbd174274240a574c401f038ac",
        "sha256:94e143285b3cabd685afc26da1f7788b8f21c35c60d4e788e71840ad98daad1f",
        "sha256:d607aa23c03f41ad619dc6e286ef83139dde9ed8a40a83616c97c0d62a803688",
        "sha256:5eda32de92842f4592a1f1ef538fdc9f00962ca4d9f0d4236d149f377b3306ea",
        "sha256:e11238839060b4df82ba04c4af6390f577d189abea283d89d4621809350395f5",
        "sha256:ef7bba1183f055dac69cba66692c11d2919dcab4946eb81abf021f6640f71bce",
        "sha256:7747123b3a3abf7cc05ea3f3f761054141f0172c13f0b2f3fe5c18e1605ae5a0",
        "sha256:ffc67481665659b336122bffe104828f3e5b89c01f4d9e0b5dd7d10c0b7dd656",
        "sha256:3501525a78b56b500cd4f32adf4cbb876455396d4c38977fa0993526cb778e80",
        "sha256:e1a08c2066fc0036fa29f41385a7e3a651c9886e30f6ed94826cabe84ff52cbe",
        "sha256:6b675ee02cec7ed9a987327d7f66acdd374befce87c446f8d5ce1eaeeb95da04",
        "sha256:a481ce2b661e8a7e7e89250a552f672b686ec41dd1a729943919d265aee5b6cf",
        "sha256:2738b75a0485a65056c18e0b50c2361677f4fe982032ea7c58b4f779967f849b",
        "sha256:87d74b5d36f8fa443b6fce0227501ae8fb56398fd51da325bbb8cea3c76a521e",
        "sha256:d67a76ec5b524d79019737f442c99e86311f4dbe3006feed3bc5c53718a33b56",
        "sha256:99cf6c0e3daefe873f0278a1590cf641b1a9e4a87ed4d4a9b16b0126b5fc91c0",
    };

    private static bool DiscoverPins => string.Equals(
        Environment.GetEnvironmentVariable("DRAGONS_DISCOVER_CONSTRUCTION_CORE_PINS"),
        "1",
        StringComparison.Ordinal);

    [Fact]
    public void MatchesPinnedDragonConstructionCoreThroughTypedNativeRoutes()
    {
        ValidatePinnedArtifactsAndNativeApi();
        using JsonDocument oracle = ReadPinnedOracle();
        FixtureContract fixture = ValidateOracle(oracle.RootElement);

        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(Cases.Select(item => item.Scenario), observations.Select(item => item.Scenario));
        Assert.All(observations, observation =>
        {
            Assert.NotEmpty(observation.Facts);
            Assert.Equal(observation.Facts.Length, observation.Facts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(observation.Facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        });
        ValidateDirectPythonParity(fixture.Cases, observations);

        object[] receipts = Targets
            .Select(target => CreateReceipt(
                target,
                observations,
                fixture.Coverage[target.Symbol],
                fixture.FactsSha256))
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
                "CONSTRUCTION_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    artifacts = new[]
                    {
                        DiscoverArtifact(FixturePath),
                        DiscoverArtifact(GeneratorPath),
                        DiscoverArtifact(ValidatorPath),
                    },
                    cases_sha256 = CanonicalSha256(oracle.RootElement.GetProperty("cases")),
                    cases = observations.Select(item => new
                    {
                        item.Scenario,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipts = Targets.Select((target, index) => new
                    {
                        target.Symbol,
                        target.AssertionId,
                        receipt_sha256 = receiptHashes[index],
                        collector_output_sha256 = collectorOutputHashes[index],
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

        for (int index = 0; index < Targets.Length; index++)
        {
            JsonElement receipt = JsonSerializer.SerializeToElement(receipts[index]);
            ValidateReceipt(
                receipt,
                Targets[index],
                observations,
                fixture.Coverage[Targets[index].Symbol],
                fixture.FactsSha256);
            TrustedEvidenceRecorder.Record(
                Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
        }

        Assert.Equal(35, Targets.Length);
        Assert.Equal(35, Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(11, Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(24, Targets.Count(item => item.Classification == "exception"));
        Assert.All(Targets.Where(item => item.Classification == "equivalent"),
            item => Assert.Null(item.AdaptationId));
        Assert.Equal(new[]
        {
            594, 598, 600, 610, 614, 629, 630, 631, 632, 633, 634,
        }, Targets.Where(item => item.Classification == "equivalent").Select(item => item.InventoryIndex));
    }

    private static CaseDefinition Case(string scenario, string suffix, string subfamily) =>
        new(scenario, "dragon-construction-core." + suffix, subfamily);

    private static TargetBinding[] CreateTargets() => new[]
    {
        Target("Construction", 593, "class",
            "sha256:451c832ae468ffe5d8cf9a462538dbd45df5d81c0d9a789d22b8ebc9cdc662c1",
            "sha256:ba362f152f2885654833496ce8ef79e40b4f76c9554bb3989187ff8284fce155",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "immutable-validated-native-construction-451c832a"),
        Target("Construction.U", 594, "function",
            "sha256:a29f2b11c458a80277b67b155ad434d7df69ed93e32c9bdaa7595bcfa41e111a",
            "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4",
            "sha256:5f09fc8f718e7deb1038c464ee7d1e34423a33647b9de4949669ccfd75149556"),
        Target("Construction.__init__", 597, "function",
            "sha256:c99eac6b7f0a56aefc53f3d6f67771870aa324f3e3e455a884ae3d046bcacbee",
            "sha256:76989db56f17a4e7cf4e9650efa5d9dc699fdd1ff26cb47e5f7dec91875f1eeb",
            "sha256:7d59358f4dd18ddf785f769c3a8e03e7b622706aa36e532ac72652edecef029c",
            "typed-nonempty-native-construction-init-c99eac6b"),
        Target("Construction.heat_capacity", 598, "function",
            "sha256:cebc9acb26c61981719622bc8621a2e46e71a856f7d735aa0ad1bac3ba924c3d",
            "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4",
            "sha256:585bb77a5040a6115541e30c1fcb41c03555d1ae57eb35302c2260a1e2d89cd4"),
        Target("Construction.reversed", 599, "function",
            "sha256:f3f8b2b13f2d35ab827dc50d35300299ffb4fdff84c23e1b61e1f75a9bd66ae6",
            "sha256:d9c98612307a4a23f460b96edc97ecfdae896510d7e3f3802c2db48397a27f7f",
            "sha256:526ddebecadebd67c23b4b2a8a0a292019e29c6e12ddeed5a95ee48cbb09dc1a",
            "immutable-validated-native-construction-reverse-f3f8b2b1"),
        Target("Construction.thickness", 600, "function",
            "sha256:bfcb0ba0853de75fd7d905f75e1faf50c62c402bb560153f99095f2ae700b42a",
            "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4",
            "sha256:69c8c7c7538e98047d16fd22ceee2d69f8bed95d94fc4ef170b4dfdb49d5e05b"),
        Target("Glazing", 602, "class",
            "sha256:5615eebbd32c5598a861819f7d6b3a78e196195e4e75720f127efcb569e3b183",
            "sha256:84f330d84d5789f02f6715bbe7f62359d20d33eb2035f16bba511e8d33e81e4d",
            "sha256:c3822492f76a666adcf5e3d03eec2d98eb4a6512284ac5b3ce410a4cc62f977f",
            "immutable-validated-native-glazing-5615eebb"),
        Target("Glazing.G", 603, "function",
            "sha256:cb8ad4be46db878574e1b2bd7d6acb89d14d3790e40fb103a36b9d2b55c06608",
            "sha256:97886dfb340546a8c9fe4b9b8cf3189e8c60725c258ed5cc38acf05f58f2d713",
            "sha256:f3304233b10860727f115b9eca9eda8e095ccc8555a79fabd8f2313d832e6106",
            "immutable-bounded-native-glazing-g-cb8ad4be"),
        Target("Glazing.U", 604, "function",
            "sha256:98ebe259795ce2a3c2e4409a7f4b07ea156ce03ec82f75f80eaacf9443ea2c74",
            "sha256:42d4877d0fcd09ffdc6f0adcf3878d8cf5fd4c4ebb84c3608e7ace68b461ded5",
            "sha256:3977456b908976b9f55edaf502ef11be78fe953ddaf51c51a06931c2cde34355",
            "immutable-finite-native-glazing-u-98ebe259"),
        Target("Glazing.__init__", 605, "function",
            "sha256:bfe7247a3ea9282f15591f6ddd95981b7d9d17090daa27f37963202cc5ec64f6",
            "sha256:3ccc7e8fdb247dcebe0a23c3b938e5c7f50dd3477011a83848d3706ae18a6313",
            "sha256:e2c9469ffa1c8710f555422ab1321056d884717d5fd4d25f0b48996bf928a094",
            "validated-immutable-native-glazing-init-bfe7247a"),
        Target("Layer", 609, "class",
            "sha256:e6a3fe0d1609d906a38b41716b1b7c4a8023d8a8d2d994372be5220fe7ffa25b",
            "sha256:29e85eb3dad7d92146453af9d99d862c4cdd12d807f41103a0bb07d4352ea3b4",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "immutable-validated-native-layer-e6a3fe0d"),
        Target("Layer.U", 610, "function",
            "sha256:be30888f37ab4d68b8032d65be2328c94c7297647b1b3ec7f6750a4d45bb60ac",
            "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4",
            "sha256:8db6a84f5441c6db866cd4b88715713d72dbe230e1199221a541bbcfcaf90e69"),
        Target("Layer.__init__", 613, "function",
            "sha256:60e437a193c85e3989efa3af43401cb41ed1dd2e4b67e71ca84e3a9c7f1eb05d",
            "sha256:4740434b9f3416b14a21d0b6efca354d514bebcb96032dfc271db5e79e04d8b6",
            "sha256:2ef34c4ddec19e44978b0a382b7838d272519b84f9531e6caeb095545c00fa2a",
            "validated-immutable-native-layer-init-60e437a1"),
        Target("Layer.heat_capacity", 614, "function",
            "sha256:ab4d9ecc8b11fd1a97ce37861d9e672f36bf362cc6c0db3e1e0171a57483c31b",
            "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4",
            "sha256:a230a49334316e1915520b5477710079b654fb2f509587ac266ad2312c825ec6"),
        Target("Layer.material", 615, "function",
            "sha256:6454844c03c2d78d936689815e815ac76301c7f135efdc361c6700b4a0391f61",
            "sha256:66d560f35bfbc32d89fb4c0926bd7f201a3482083381e3386e142545ced868df",
            "sha256:6b29070572dbb73df177ec301f5cf08d86910d98ef3656807a3723b86fb11caa",
            "immutable-required-native-layer-material-6454844c"),
        Target("Layer.thickness", 616, "function",
            "sha256:d7d789d7eddbbdc9b7f7db4e683689ddc362be53336654fe9930319b1ea25899",
            "sha256:8f13b51352c870f880244d9dacb7b0fb97dc8e5823221a7cf6a23fcbf4186d88",
            "sha256:f52129f61c499bb427a8e956033d38541ef4b21f599c3a4e703f7b0744d496a0",
            "immutable-finite-native-layer-thickness-d7d789d7"),
        Target("Material", 618, "class",
            "sha256:15ad6614da4693f24dc519c4a8ebc1503c18e90f5e7407194e7af2ee478878c2",
            "sha256:f374687b6dfa7d96b4b87d055f10b1c4045aef186851919903647548c74ae2bd",
            "sha256:f04919503f8232602615439697060f553b3b3db5b31404e7eb9b49ee20e57d65",
            "immutable-validated-native-material-15ad6614"),
        Target("Material.__init__", 620, "function",
            "sha256:d78cab39fcf7243e0cd0c59653ff7514b95dbddf3cc9a28eb14a8834bfd9791d",
            "sha256:3ba00224b6905eea7a43603fcc36c1c6adca5b5b3e4c781df75be38d9d1ec690",
            "sha256:267759cb86c1bd9f1390036d0858ee51930be747648796d9236b5260311f6d21",
            "validated-immutable-native-material-init-d78cab39"),
        Target("Material.conductivity", 621, "function",
            "sha256:b733b56b8a0acfcefc97c11b3fef116d8a1a5a29c847ed24e600839289383471",
            "sha256:68da20c9424bcb4ac2882491f00f8c9c26c63e331453583af45c04a260c45453",
            "sha256:f512da9e579d342352c80b0e5ceb0af993e59c64ac17ebd7f46067d0df112c94",
            "immutable-finite-native-material-conductivity-b733b56b"),
        Target("Material.density", 622, "function",
            "sha256:231363247e3bc2f63cd6b88174bb6e3f732f56e00f0abab5bc9eeb69d2ef8893",
            "sha256:8d7e015ab764fc82bd4de0f7447db18903e71574e2ee810518866ea31f0700b7",
            "sha256:7a3173329ae1f0c334b6362b9c1c7cc7f1aaf20be9ddea3d34ebf29c9804cfe9",
            "immutable-finite-native-material-density-23136324"),
        Target("Material.roughness", 623, "function",
            "sha256:be23eedd7fa255d7489768c6081e40cbc6361e17736f66dbea5609b89105465b",
            "sha256:07c6d8f20d92daeb00700a584eb95634eb7c7ac7b43f41de5497eedd93da1b0e",
            "sha256:369a99e17d94426a26a3ccda42dff5a360fceb9aee71166f0528f011c30b4d84",
            "immutable-strongly-typed-native-material-roughness-be23eedd"),
        Target("Material.solar_absorptance", 624, "function",
            "sha256:ae7ce02bf1109ed4279c351fa9497272fef93c019b587fd237073bf1055d315f",
            "sha256:4629f155a28541d892173c40a499ea8bbb660522be6762d87df9f0fa254ded61",
            "sha256:35af69a9a977f53ab5fd828a823ec7a1576f7042e3ec606cecd2da3b85b933c6",
            "immutable-finite-native-material-solar-absorptance-ae7ce02b"),
        Target("Material.specific_heat", 625, "function",
            "sha256:abf4a2ea739fe17a9d04c787331534748bfd530f11baddf215ea17e5363f011b",
            "sha256:3f02e26053465c1d64093f2c803d1f146085da49b32a8561b50291b3df8fea37",
            "sha256:0580b0014f432929c0452ccb674124ad60495272b5976e1380bb90ae7aa21701",
            "immutable-finite-native-material-specific-heat-abf4a2ea"),
        Target("Material.thermal_absorptance", 626, "function",
            "sha256:f17730ed4aa6cc5d8aa673527cd0b43e3ef83ead9df7d5d5910ad26eaa87f784",
            "sha256:a74f738d6f56e6f5c6b72d89a54e8c1d98b783c82afa8928f23d97945b17be5e",
            "sha256:22377bd3ebaf63e005fda95e22d5f2583df3d7fd1361109e072ff4d41b90fdaa",
            "immutable-finite-native-material-thermal-absorptance-f17730ed"),
        Target("Material.visible_absorptance", 627, "function",
            "sha256:ecf6d77de8ef2e870df1470b8113e2beaa1154e5ae54d6581d7c62840df71c9c",
            "sha256:b0839747d975780bcb3b558e0f548e211db34a5e1d793d5ba106f7e6500bee18",
            "sha256:0b8fe267c85e1a9d3e9d5ea832e8cff40ce6e03b51246ee94a09794a2d60d7ab",
            "immutable-finite-native-material-visible-absorptance-ecf6d77d"),
        Target("MaterialRoughness", 628, "class",
            "sha256:fc281859031701e047f11e96eac77ca6cb530ce23493a7ef77c1e0000d31ff08",
            "sha256:6dc25edcfe258d38350c660f1cd3ff872cf05d4b629faa638d7454a7de510903",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "strongly-typed-native-material-roughness-enum-fc281859"),
        Target("MaterialRoughness.MEDIUMROUGH", 629, "constant",
            "sha256:eda0d7d5e27bc9a869d83138fab36b72b59c5dc4cc013d3c8de181be8a683aa1",
            "sha256:d7d80ec873529a9c0869d0bc7b0e8317ac669fd979cb825bdeb0b0bee5787bb6",
            "sha256:e45dfab6985e92e259a2d5d60f612bc723282af67f90ec2c1e22c13b51818296"),
        Target("MaterialRoughness.MEDIUMSMOOTH", 630, "constant",
            "sha256:6d574d5473f00de478de3c31bb49dd0092748208c1b62b098f054b43b4d97023",
            "sha256:506ea60b4d768c655ac5410139c43c6be4ffa35b836b2b95361a2e4258dcad52",
            "sha256:fa9d3ec25e11b97efaf1b3be70b04cf9097ee8105e7227ba7990e79ef05a2da0"),
        Target("MaterialRoughness.ROUGH", 631, "constant",
            "sha256:beaf152fac9bd6bc2352e8a1ac6295cc9ba66bcf1d941a54753822672b961918",
            "sha256:3018df3727ef92da5fb87e1920b4010e9a09bcbac5ef474924ce9639f5fffeb0",
            "sha256:ca49338828392c58c98b8ab74e4cf7148e4330939bc5ef3ef113e6043bec419d"),
        Target("MaterialRoughness.SMOOTH", 632, "constant",
            "sha256:fce6deeb54d0293397f0279fb5ad9e25ff06f0bce3a0430a18d2935ad80739ff",
            "sha256:56f2e0fce6e496a46a96e9d9ee4d5906ce3c133af204314a8d0af340d7eaca4c",
            "sha256:61392d9f0253e489e1057e14f6418c347a182c4ea465fd7ce135c400d3848f5b"),
        Target("MaterialRoughness.VERYROUGH", 633, "constant",
            "sha256:9848a0c66d3e174bd16efcbaac5d3f3a1b3f0e657f223306860a8f874ed0c7fd",
            "sha256:b25aff6d388d615132acdb141dd8f540f94a7c05359633712ad75e575845eaf3",
            "sha256:846c33a8465f5b9f11ed409dc51fa0086409cad0f5eb21d889981ca0f68f6e92"),
        Target("MaterialRoughness.__str__", 634, "function",
            "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e",
            "sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab",
            "sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8"),
        Target("NoMassConstruction", 635, "class",
            "sha256:9dff867c894980d4bda1f7c0cc731348382bef441677df6a70b79ebf876c23a2",
            "sha256:24508dea22fc922a71630543e4fb07ae0250b502a350f455ff9d2e3ece31eb95",
            "sha256:c3822492f76a666adcf5e3d03eec2d98eb4a6512284ac5b3ce410a4cc62f977f",
            "immutable-validated-native-no-mass-construction-9dff867c"),
        Target("NoMassConstruction.U", 636, "function",
            "sha256:98ebe259795ce2a3c2e4409a7f4b07ea156ce03ec82f75f80eaacf9443ea2c74",
            "sha256:42d4877d0fcd09ffdc6f0adcf3878d8cf5fd4c4ebb84c3608e7ace68b461ded5",
            "sha256:3977456b908976b9f55edaf502ef11be78fe953ddaf51c51a06931c2cde34355",
            "immutable-finite-native-no-mass-u-98ebe259"),
        Target("NoMassConstruction.__init__", 637, "function",
            "sha256:4749789207a6ac2baa1695fc65c6c280636e4dd352a9a7ae0369b7857a395338",
            "sha256:88df136720eb992f7f9723304f000865e76befe28194b7682b4efd4b4afbde01",
            "sha256:0405b61d480a332d56909d06933cfec963f67cf958fd3bf548d9a07ea2d47f63",
            "validated-immutable-native-no-mass-init-47497892"),
    };

    private static TargetBinding Target(
        string symbol,
        int inventoryIndex,
        string kind,
        string symbolHash,
        string signatureHash,
        string bodyHash,
        string? adaptationId = null) => new(
            symbol,
            inventoryIndex,
            kind,
            symbolHash,
            signatureHash,
            bodyHash,
            $"dragon-construction-core-{inventoryIndex}-{symbolHash[7..15]}",
            adaptationId,
            OracleAdaptationFor(symbol),
            adaptationId is null ? "equivalent" : "exception",
            NativeTargetFor(symbol));

    private static string OracleAdaptationFor(string symbol) => symbol switch
    {
        "Construction" => "immutable-validated-native-construction-451c832a",
        "Construction.U" => "direct-native-construction-u-value",
        "Construction.__init__" => "typed-nonempty-native-construction-init-c99eac6b",
        "Construction.heat_capacity" => "direct-native-construction-heat-capacity",
        "Construction.reversed" => "immutable-validated-native-construction-reverse-f3f8b2b1",
        "Construction.thickness" => "direct-native-construction-thickness",
        "Glazing" => "immutable-validated-native-glazing-5615eebb",
        "Glazing.G" => "immutable-bounded-native-glazing-g-cb8ad4be",
        "Glazing.U" => "immutable-finite-native-glazing-u-98ebe259",
        "Glazing.__init__" => "validated-immutable-native-glazing-init-bfe7247a",
        "Layer" => "immutable-validated-native-layer-e6a3fe0d",
        "Layer.U" => "direct-native-layer-u-value",
        "Layer.__init__" => "validated-immutable-native-layer-init-60e437a1",
        "Layer.heat_capacity" => "direct-native-layer-heat-capacity",
        "Layer.material" => "immutable-required-native-layer-material-6454844c",
        "Layer.thickness" => "immutable-finite-native-layer-thickness-d7d789d7",
        "Material" => "immutable-validated-native-material-15ad6614",
        "Material.__init__" => "validated-immutable-native-material-init-d78cab39",
        "Material.conductivity" => "immutable-finite-native-material-conductivity-b733b56b",
        "Material.density" => "immutable-finite-native-material-density-23136324",
        "Material.roughness" => "immutable-strongly-typed-native-material-roughness-be23eedd",
        "Material.solar_absorptance" => "immutable-finite-native-material-solar-absorptance-ae7ce02b",
        "Material.specific_heat" => "immutable-finite-native-material-specific-heat-abf4a2ea",
        "Material.thermal_absorptance" => "immutable-finite-native-material-thermal-absorptance-f17730ed",
        "Material.visible_absorptance" => "immutable-finite-native-material-visible-absorptance-ecf6d77d",
        "MaterialRoughness" => "strongly-typed-native-material-roughness-enum-fc281859",
        "MaterialRoughness.MEDIUMROUGH" => "direct-native-material-roughness-medium-rough",
        "MaterialRoughness.MEDIUMSMOOTH" => "direct-native-material-roughness-medium-smooth",
        "MaterialRoughness.ROUGH" => "direct-native-material-roughness-rough",
        "MaterialRoughness.SMOOTH" => "direct-native-material-roughness-smooth",
        "MaterialRoughness.VERYROUGH" => "direct-native-material-roughness-very-rough",
        "MaterialRoughness.__str__" => "direct-native-material-roughness-string",
        "NoMassConstruction" => "immutable-validated-native-no-mass-construction-9dff867c",
        "NoMassConstruction.U" => "immutable-finite-native-no-mass-u-98ebe259",
        "NoMassConstruction.__init__" => "validated-immutable-native-no-mass-init-47497892",
        _ => throw new Xunit.Sdk.XunitException($"No oracle adaptation for '{symbol}'."),
    };

    private static string NativeTargetFor(string symbol) => symbol switch
    {
        "Construction" => "Dragons.InvisibleDragon.Construction.Construction immutable class",
        "Construction.U" => "Construction.UValue",
        "Construction.__init__" => "Construction(string, IEnumerable<Layer>)",
        "Construction.heat_capacity" => "Construction.HeatCapacityJoulesPerSquareMetreKelvin",
        "Construction.reversed" => "Construction.Reverse",
        "Construction.thickness" => "Construction.ThicknessMetres",
        "Glazing" => "Dragons.InvisibleDragon.Construction.Glazing immutable record",
        "Glazing.G" => "Glazing.SolarHeatGainCoefficient",
        "Glazing.U" => "Glazing.UValueWattsPerSquareMetreKelvin",
        "Glazing.__init__" => "Glazing(string, double, double)",
        "Layer" => "Dragons.InvisibleDragon.Construction.Layer immutable record",
        "Layer.U" => "Layer.UValue",
        "Layer.__init__" => "Layer(string, Material, double)",
        "Layer.heat_capacity" => "Layer.HeatCapacityJoulesPerSquareMetreKelvin",
        "Layer.material" => "Layer.Material get-only required reference",
        "Layer.thickness" => "Layer.ThicknessMetres get-only finite double",
        "Material" => "Dragons.InvisibleDragon.Construction.Material immutable record",
        "Material.__init__" => "Material validated typed constructor",
        "Material.conductivity" => "Material.ConductivityWattsPerMetreKelvin",
        "Material.density" => "Material.DensityKilogramsPerCubicMetre",
        "Material.roughness" => "Material.Roughness",
        "Material.solar_absorptance" => "Material.SolarAbsorptance",
        "Material.specific_heat" => "Material.SpecificHeatJoulesPerKilogramKelvin",
        "Material.thermal_absorptance" => "Material.ThermalAbsorptance",
        "Material.visible_absorptance" => "Material.VisibleAbsorptance",
        "MaterialRoughness" => "Dragons.InvisibleDragon.Construction.MaterialRoughness enum",
        "MaterialRoughness.MEDIUMROUGH" => "MaterialRoughness.MediumRough",
        "MaterialRoughness.MEDIUMSMOOTH" => "MaterialRoughness.MediumSmooth",
        "MaterialRoughness.ROUGH" => "MaterialRoughness.Rough",
        "MaterialRoughness.SMOOTH" => "MaterialRoughness.Smooth",
        "MaterialRoughness.VERYROUGH" => "MaterialRoughness.VeryRough",
        "MaterialRoughness.__str__" => "MaterialRoughness.ToString",
        "NoMassConstruction" => "Dragons.InvisibleDragon.Construction.NoMassConstruction immutable record",
        "NoMassConstruction.U" => "NoMassConstruction.UValueWattsPerSquareMetreKelvin",
        "NoMassConstruction.__init__" => "NoMassConstruction(string, double)",
        _ => throw new Xunit.Sdk.XunitException($"No native target for '{symbol}'."),
    };

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(FixturePath));
        if (!DiscoverPins)
        {
            Assert.Equal(FixtureBytes, bytes.Length);
            Assert.Equal(FixtureSha256, Sha256(bytes));
        }
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain("\r\n", new UTF8Encoding(false, true).GetString(bytes), StringComparison.Ordinal);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
    }

    private static FixtureContract ValidateOracle(JsonElement root)
    {
        AssertUniqueKeysRecursive(root);
        AssertNoHostPaths(root);
        AssertNoUnsafeIdentity(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(root,
            "case_sha256", "cases", "cases_sha256", "consumer_contract", "fact_sha256",
            "runtime", "schema", "symbols", "target_receipts", "upstream");
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));

        JsonElement casesElement = root.GetProperty("cases");
        string observedCasesSha256 = CanonicalSha256(casesElement);
        Assert.Equal(observedCasesSha256, RequiredString(root, "cases_sha256"));
        if (!DiscoverPins)
        {
            Assert.Equal(CasesSha256, observedCasesSha256);
        }

        JsonElement[] fixtureCases = casesElement.EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        JsonElement factHashes = root.GetProperty("fact_sha256");
        JsonElement caseHashes = root.GetProperty("case_sha256");
        var coverage = Targets.ToDictionary(
            target => target.Symbol,
            _ => new List<int>(),
            StringComparer.Ordinal);

        for (int index = 0; index < fixtureCases.Length; index++)
        {
            JsonElement item = fixtureCases[index];
            CaseDefinition expected = Cases[index];
            AssertKeys(item,
                "context_symbols", "executor", "expected_dotnet", "id", "python",
                "scenario", "subfamily", "target_symbols");
            Assert.Equal(expected.CaseId, RequiredString(item, "id"));
            Assert.Equal(expected.Scenario, RequiredString(item, "scenario"));
            Assert.Equal(expected.Subfamily, RequiredString(item, "subfamily"));
            Assert.Equal("dragon-construction-core", RequiredString(item, "executor"));

            string[] expectedTargets = ExpectedCaseTargets(index);
            string[] expectedContext = ExpectedCaseContext(index);
            AssertStringArray(item.GetProperty("target_symbols"), expectedTargets);
            AssertStringArray(item.GetProperty("context_symbols"), expectedContext);
            foreach (string symbol in expectedTargets)
            {
                coverage[symbol].Add(index);
            }

            JsonElement expectedDotNet = item.GetProperty("expected_dotnet");
            AssertKeys(expectedDotNet, "adaptations", "classifications", "outcome");
            Assert.Equal("adapted-or-equivalent-as-pinned", RequiredString(expectedDotNet, "outcome"));
            AssertStringArray(
                expectedDotNet.GetProperty("adaptations"),
                expectedTargets.Select(symbol => TargetBySymbol(symbol).OracleAdaptationId)
                    .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
            JsonElement classifications = expectedDotNet.GetProperty("classifications");
            Assert.Equal(expectedTargets.Length, classifications.EnumerateObject().Count());
            foreach (string symbol in expectedTargets)
            {
                Assert.Equal(TargetBySymbol(symbol).Classification, RequiredString(classifications, symbol));
            }

            JsonElement python = item.GetProperty("python");
            AssertKeys(python, "facts", "facts_sha256", "outcome");
            Assert.Equal("observed", RequiredString(python, "outcome"));
            JsonElement facts = python.GetProperty("facts");
            AssertKeys(facts, "observations", "scenario", "source_state", "subfamily", "timeline");
            Assert.Equal(expected.Scenario, RequiredString(facts, "scenario"));
            Assert.Equal(expected.Subfamily, RequiredString(facts, "subfamily"));
            string factSha256 = CanonicalSha256(facts);
            Assert.Equal(factSha256, RequiredString(python, "facts_sha256"));
            Assert.Equal(factSha256, RequiredString(factHashes, expected.CaseId));
            Assert.Equal(CanonicalSha256(item), RequiredString(caseHashes, expected.CaseId));
        }

        ValidateTargetReceipts(root);
        ValidateConsumerContract(root.GetProperty("consumer_contract"), coverage);
        ValidateRuntimeAndUpstream(root);

        return new FixtureContract(
            fixtureCases.Select(item => item.Clone()).ToArray(),
            coverage.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal),
            Cases.ToDictionary(
                item => item.CaseId,
                item => RequiredString(factHashes, item.CaseId),
                StringComparer.Ordinal));
    }

    private static string[] ExpectedCaseTargets(int index) => index switch
    {
        0 => new[] { "MaterialRoughness", "MaterialRoughness.MEDIUMROUGH", "MaterialRoughness.MEDIUMSMOOTH", "MaterialRoughness.ROUGH", "MaterialRoughness.SMOOTH", "MaterialRoughness.VERYROUGH" },
        1 => new[] { "MaterialRoughness.__str__", "MaterialRoughness.MEDIUMROUGH", "MaterialRoughness.MEDIUMSMOOTH", "MaterialRoughness.ROUGH", "MaterialRoughness.SMOOTH", "MaterialRoughness.VERYROUGH" },
        2 => new[] { "MaterialRoughness" },
        3 or 4 => new[] { "Material", "Material.__init__", "Material.conductivity", "Material.density", "Material.roughness", "Material.solar_absorptance", "Material.specific_heat", "Material.thermal_absorptance", "Material.visible_absorptance" },
        5 => new[] { "Material", "Material.__init__", "Material.conductivity", "Material.density", "Material.solar_absorptance", "Material.specific_heat", "Material.thermal_absorptance", "Material.visible_absorptance" },
        6 => new[] { "Layer", "Layer.__init__", "Layer.U", "Layer.heat_capacity", "Layer.material", "Layer.thickness" },
        7 => new[] { "Layer", "Layer.U", "Layer.heat_capacity", "Layer.material", "Layer.thickness" },
        8 => new[] { "Layer", "Layer.__init__", "Layer.material", "Layer.thickness" },
        9 or 10 => new[] { "Construction", "Construction.__init__", "Construction.U", "Construction.heat_capacity", "Construction.thickness" },
        11 => new[] { "Construction.reversed" },
        12 => new[] { "Construction", "Construction.__init__", "Construction.U", "Construction.heat_capacity", "Construction.thickness" },
        13 => new[] { "Glazing", "Glazing.__init__", "Glazing.G", "Glazing.U" },
        14 => new[] { "Glazing", "Glazing.G", "Glazing.U" },
        15 => new[] { "Glazing", "Glazing.__init__", "Glazing.G", "Glazing.U" },
        16 => new[] { "NoMassConstruction", "NoMassConstruction.__init__", "NoMassConstruction.U" },
        17 => new[] { "NoMassConstruction", "NoMassConstruction.U" },
        18 => new[] { "NoMassConstruction", "NoMassConstruction.__init__", "NoMassConstruction.U" },
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    private static string[] ExpectedCaseContext(int index) => index switch
    {
        0 or 13 or 15 or 16 or 18 => Array.Empty<string>(),
        1 => new[] { "MaterialRoughness" },
        2 or 3 => new[] { "MaterialRoughness.ROUGH" },
        4 => new[] { "MaterialRoughness" },
        5 => new[] { "Material.roughness" },
        6 => new[] { "Material" },
        7 => new[] { "Layer.__init__", "Material" },
        8 => new[] { "Material" },
        9 or 12 => new[] { "Layer" },
        10 => new[] { "Material", "Layer" },
        11 => new[] { "Construction", "Layer" },
        14 => new[] { "Glazing.__init__" },
        17 => new[] { "NoMassConstruction.__init__" },
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    private static void ValidateTargetReceipts(JsonElement root)
    {
        JsonElement[] descriptors = root.GetProperty("symbols").EnumerateArray().ToArray();
        JsonElement[] receipts = root.GetProperty("target_receipts").EnumerateArray().ToArray();
        Assert.Equal(Targets.Length, descriptors.Length);
        Assert.Equal(Targets.Length, receipts.Length);

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        Assert.Equal(InventoryBytes, inventoryBytes.Length);
        Assert.Equal(InventoryFileSha256, Sha256(inventoryBytes));
        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        JsonElement inventorySymbols = inventory.RootElement.GetProperty("symbols");
        for (int index = 0; index < Targets.Length; index++)
        {
            TargetBinding target = Targets[index];
            AssertSourceReceipt(descriptors[index], target, includeIndex: false);
            AssertSourceReceipt(receipts[index], target, includeIndex: true);
            JsonElement inventoried = inventorySymbols[target.InventoryIndex];
            Assert.Equal(target.Symbol, RequiredString(inventoried, "symbol"));
            Assert.Equal(target.Kind, RequiredString(inventoried, "kind"));
            Assert.Equal(target.SymbolHash, RequiredString(inventoried, "symbol_hash"));
            Assert.Equal(target.SignatureHash, RequiredString(inventoried, "signature_hash"));
            Assert.Equal(target.BodyHash, RequiredString(inventoried, "body_hash"));
            Assert.Equal(UpstreamPath, RequiredString(inventoried, "path"));
        }
    }

    private static void ValidateConsumerContract(
        JsonElement contract,
        IReadOnlyDictionary<string, List<int>> coverage)
    {
        Assert.Equal(19, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        AssertStringArray(contract.GetProperty("target_symbols"), Targets.Select(item => item.Symbol));
        AssertStringArray(contract.GetProperty("equivalent_symbols"),
            Targets.Where(item => item.Classification == "equivalent").Select(item => item.Symbol));
        Assert.Equal(11, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());
        Assert.Equal(24, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        Assert.Equal("proposed-not-yet-cross-language-verified", RequiredString(contract, "native_binding_status"));
        Assert.Equal("exact-nineteen-case-thirty-five-target-construction-core-matrix",
            RequiredString(contract.GetProperty("closure"), "scope"));
        Assert.True(contract.GetProperty("closure").GetProperty("target_coverage_complete").GetBoolean());
        Assert.False(contract.GetProperty("closure").GetProperty("full_symbol_closure").GetBoolean());
        Assert.False(contract.GetProperty("closure").GetProperty("full_construction_family_closure").GetBoolean());
        Assert.Equal(18, contract.GetProperty("closure").GetProperty("adjacent_exclusions").GetArrayLength());
        Assert.Equal(7, contract.GetProperty("closure").GetProperty("unresolved_boundaries").GetArrayLength());
        Assert.Equal(35, contract.GetProperty("evidence_contract").GetProperty("expected_receipt_count").GetInt32());
        Assert.False(contract.GetProperty("evidence_contract").GetProperty("structural_only").GetBoolean());
        Assert.False(contract.GetProperty("evidence_contract").GetProperty("full_idf_closure").GetBoolean());

        JsonElement contractReceipts = contract.GetProperty("target_receipts");
        Assert.Equal(35, contractReceipts.GetArrayLength());
        for (int index = 0; index < Targets.Length; index++)
        {
            TargetBinding target = Targets[index];
            Assert.Equal(target.OracleAdaptationId, RequiredString(contract.GetProperty("adaptations"), target.Symbol));
            Assert.Equal(target.AssertionId, RequiredString(contract.GetProperty("assertion_ids"), target.Symbol));
            Assert.Equal(target.Classification, RequiredString(contract.GetProperty("classifications"), target.Symbol));
            Assert.Equal(target.NativeTarget, RequiredString(contract.GetProperty("native_targets"), target.Symbol));
            AssertSourceReceipt(contractReceipts[index], target, includeIndex: true);
            AssertStringArray(
                contract.GetProperty("closure").GetProperty("case_coverage_by_symbol").GetProperty(target.Symbol),
                coverage[target.Symbol].Select(caseIndex => Cases[caseIndex].CaseId));
        }
    }

    private static void ValidateRuntimeAndUpstream(JsonElement root)
    {
        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        JsonElement source = upstream.GetProperty("construction_source");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));
        JsonElement inventory = upstream.GetProperty("inventory_file");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        Assert.Equal(18, upstream.GetProperty("adjacent_exclusions").GetArrayLength());
        JsonElement loaded = Assert.Single(upstream.GetProperty("loaded_local_modules").EnumerateArray(),
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("idragon.dragon.construction", RequiredString(loaded, "module"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(loaded, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(loaded, "ast_sha256"));
        JsonElement sourceReceipt = Assert.Single(upstream.GetProperty("sources").EnumerateArray(),
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal(UpstreamSourceSha256, RequiredString(sourceReceipt, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(sourceReceipt, "ast_sha256"));
    }

    private static void ValidatePinnedArtifactsAndNativeApi()
    {
        if (!DiscoverPins)
        {
            AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
            AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        }
        else
        {
            Assert.True(File.Exists(FindRepositoryFile(GeneratorPath)));
            Assert.True(File.Exists(FindRepositoryFile(ValidatorPath)));
        }
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        AssertClass(typeof(OpaqueConstruction), sealedType: true, implementsSurfaceConstruction: true);
        ConstructorInfo constructionConstructor = Assert.Single(
            typeof(OpaqueConstruction).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertParameterTypes(constructionConstructor, typeof(string), typeof(IEnumerable<Layer>));
        ValidateProperty(typeof(OpaqueConstruction), nameof(OpaqueConstruction.Name), typeof(string));
        ValidateProperty(typeof(OpaqueConstruction), nameof(OpaqueConstruction.Layers), typeof(IReadOnlyList<Layer>));
        ValidateProperty(typeof(OpaqueConstruction), nameof(OpaqueConstruction.UValue), typeof(double));
        ValidateProperty(typeof(OpaqueConstruction), nameof(OpaqueConstruction.ThicknessMetres), typeof(double));
        ValidateProperty(typeof(OpaqueConstruction), nameof(OpaqueConstruction.HeatCapacityJoulesPerSquareMetreKelvin), typeof(double));
        MethodInfo reverse = Assert.IsAssignableFrom<MethodInfo>(typeof(OpaqueConstruction).GetMethod(
            nameof(OpaqueConstruction.Reverse), BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(OpaqueConstruction), reverse.ReturnType);
        ParameterInfo reverseName = Assert.Single(reverse.GetParameters());
        Assert.Equal("name", reverseName.Name);
        Assert.Equal(typeof(string), reverseName.ParameterType);
        Assert.True(reverseName.IsOptional);
        Assert.Null(reverseName.DefaultValue);

        AssertClass(typeof(Material), sealedType: true, implementsSurfaceConstruction: false);
        ConstructorInfo materialConstructor = Assert.Single(
            typeof(Material).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertParameterTypes(materialConstructor,
            typeof(string), typeof(double), typeof(double), typeof(double),
            typeof(double), typeof(double), typeof(double), typeof(MaterialRoughness));
        Assert.Equal(4, materialConstructor.GetParameters().Count(item => item.IsOptional));
        foreach ((string name, Type type) in new[]
        {
            (nameof(Material.Name), typeof(string)),
            (nameof(Material.ConductivityWattsPerMetreKelvin), typeof(double)),
            (nameof(Material.DensityKilogramsPerCubicMetre), typeof(double)),
            (nameof(Material.SpecificHeatJoulesPerKilogramKelvin), typeof(double)),
            (nameof(Material.ThermalAbsorptance), typeof(double)),
            (nameof(Material.SolarAbsorptance), typeof(double)),
            (nameof(Material.VisibleAbsorptance), typeof(double)),
            (nameof(Material.Roughness), typeof(MaterialRoughness)),
        })
        {
            ValidateProperty(typeof(Material), name, type);
        }

        AssertClass(typeof(Layer), sealedType: true, implementsSurfaceConstruction: false);
        ConstructorInfo layerConstructor = Assert.Single(
            typeof(Layer).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertParameterTypes(layerConstructor, typeof(string), typeof(Material), typeof(double));
        ValidateProperty(typeof(Layer), nameof(Layer.Name), typeof(string));
        ValidateProperty(typeof(Layer), nameof(Layer.Material), typeof(Material));
        ValidateProperty(typeof(Layer), nameof(Layer.ThicknessMetres), typeof(double));
        ValidateProperty(typeof(Layer), nameof(Layer.UValue), typeof(double));
        ValidateProperty(typeof(Layer), nameof(Layer.HeatCapacityJoulesPerSquareMetreKelvin), typeof(double));

        AssertClass(typeof(Glazing), sealedType: true, implementsSurfaceConstruction: false);
        ConstructorInfo glazingConstructor = Assert.Single(
            typeof(Glazing).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertParameterTypes(glazingConstructor, typeof(string), typeof(double), typeof(double));
        ValidateProperty(typeof(Glazing), nameof(Glazing.Name), typeof(string));
        ValidateProperty(typeof(Glazing), nameof(Glazing.UValueWattsPerSquareMetreKelvin), typeof(double));
        ValidateProperty(typeof(Glazing), nameof(Glazing.SolarHeatGainCoefficient), typeof(double));

        AssertClass(typeof(NoMassConstruction), sealedType: true, implementsSurfaceConstruction: true);
        ConstructorInfo noMassConstructor = Assert.Single(
            typeof(NoMassConstruction).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertParameterTypes(noMassConstructor, typeof(string), typeof(double));
        ValidateProperty(typeof(NoMassConstruction), nameof(NoMassConstruction.Name), typeof(string));
        ValidateProperty(typeof(NoMassConstruction), nameof(NoMassConstruction.UValueWattsPerSquareMetreKelvin), typeof(double));

        Assert.True(typeof(MaterialRoughness).IsEnum);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(MaterialRoughness)));
        Assert.Equal(new[] { "VeryRough", "Rough", "MediumRough", "MediumSmooth", "Smooth" },
            Enum.GetNames(typeof(MaterialRoughness)));
    }

    private static NativeObservation ObserveNativeCase(int index)
    {
        string[] facts = index switch
        {
            0 => ObserveC01(),
            1 => ObserveC02(),
            2 => ObserveC03(),
            3 => ObserveC04(),
            4 => ObserveC05(),
            5 => ObserveC06(),
            6 => ObserveC07(),
            7 => ObserveC08(),
            8 => ObserveC09(),
            9 => ObserveC10(),
            10 => ObserveC11(),
            11 => ObserveC12(),
            12 => ObserveC13(),
            13 => ObserveC14(),
            14 => ObserveC15(),
            15 => ObserveC16(),
            16 => ObserveC17(),
            17 => ObserveC18(),
            18 => ObserveC19(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return new NativeObservation(
            Cases[index].Scenario,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveC01()
    {
        MaterialRoughness[] values = Enum.GetValues<MaterialRoughness>();
        Assert.Equal(new[]
        {
            MaterialRoughness.VeryRough,
            MaterialRoughness.Rough,
            MaterialRoughness.MediumRough,
            MaterialRoughness.MediumSmooth,
            MaterialRoughness.Smooth,
        }, values);
        return new[]
        {
            "enum.is_enum=" + Lower(typeof(MaterialRoughness).IsEnum),
            "enum.is_string_assignable=" + Lower(typeof(string).IsAssignableFrom(typeof(MaterialRoughness))),
            "enum.underlying_type=" + TypeName(Enum.GetUnderlyingType(typeof(MaterialRoughness))),
            "enum.member_count=" + values.Length,
            "enum.names=" + Join(Enum.GetNames<MaterialRoughness>()),
            "enum.numeric_values=" + Join(values.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture))),
            "enum.defined_values=" + Join(values.Select(value => value + "=" + Lower(Enum.IsDefined(value)))),
        };
    }

    private static string[] ObserveC02()
    {
        MaterialRoughness[] values = Enum.GetValues<MaterialRoughness>();
        string[] strings = values.Select(value => value.ToString()).ToArray();
        Assert.Equal(new[] { "VeryRough", "Rough", "MediumRough", "MediumSmooth", "Smooth" }, strings);
        return new[]
        {
            "enum.strings=" + Join(strings),
            "enum.formatted=" + Join(values.Select(value => $"<{value}>")),
            "enum.names_equal_strings=" + Join(values.Select(value => Lower(Enum.GetName(value) == value.ToString()))),
            "enum.string_is_stable=" + Lower(strings.SequenceEqual(values.Select(value => value.ToString()))),
        };
    }

    private static string[] ObserveC03()
    {
        bool parsedRough = Enum.TryParse("Rough", ignoreCase: false, out MaterialRoughness rough);
        bool parsedUpper = Enum.TryParse("ROUGH", ignoreCase: false, out MaterialRoughness _);
        bool parsedIgnoreCase = Enum.TryParse("rough", ignoreCase: true, out MaterialRoughness roughIgnoreCase);
        Assert.True(parsedRough);
        Assert.Equal(MaterialRoughness.Rough, rough);
        Assert.False(parsedUpper);
        Assert.True(parsedIgnoreCase);
        Assert.Equal(MaterialRoughness.Rough, roughIgnoreCase);
        return new[]
        {
            "enum.parse.Rough=" + Lower(parsedRough) + "|value=" + rough,
            "enum.parse.ROUGH.case_sensitive=" + Lower(parsedUpper),
            "enum.parse.rough.ignore_case=" + Lower(parsedIgnoreCase) + "|value=" + roughIgnoreCase,
            "enum.undefined_5=" + Lower(Enum.IsDefined(typeof(MaterialRoughness), 5)),
            "enum.defined_rough=" + Lower(Enum.IsDefined(typeof(MaterialRoughness), MaterialRoughness.Rough)),
            "enum.boxed_runtime_type=" + TypeName(((object)MaterialRoughness.Rough).GetType()),
        };
    }

    private static string[] ObserveC04()
    {
        var material = new Material("Default", 0.72, 1920, 840);
        Assert.Equal(MaterialRoughness.Rough, material.Roughness);
        Assert.Equal(0.9, material.ThermalAbsorptance);
        Assert.Equal(0.7, material.SolarAbsorptance);
        Assert.Equal(0.7, material.VisibleAbsorptance);
        ConstructorInfo constructor = Assert.Single(typeof(Material).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        return new[]
        {
            "material.state=" + MaterialState(material),
            "constructor.parameter_order=" + Join(constructor.GetParameters().Select(item => item.Name!)),
            "constructor.required_count=" + constructor.GetParameters().Count(item => !item.IsOptional),
            "constructor.optional_defaults=" + Join(constructor.GetParameters().Where(item => item.IsOptional).Select(ParameterFact)),
            "properties.get_only=" + Join(MaterialPropertyNames().Select(name => name + "=" + Lower(!RequiredProperty(typeof(Material), name).CanWrite))),
        };
    }

    private static string[] ObserveC05()
    {
        var material = new Material(
            "Explicit", 1.25, 900, 1100, 0.11, 0.22, 0.33, MaterialRoughness.MediumSmooth);
        string before = MaterialState(material);
        string rejection = Capture("separate-invalid", () => new Material(
            "Rejected", 1, 1, 100, roughness: (MaterialRoughness)999));
        string after = MaterialState(material);
        Assert.Equal(before, after);
        return new[]
        {
            "material.explicit_state=" + before,
            "material.separate_invalid=" + rejection,
            "material.state_after_separate_invalid=" + after,
            "material.state_unchanged=" + Lower(before == after),
            "material.reference_type=" + Lower(!typeof(Material).IsValueType),
            "material.roughness_type=" + TypeName(material.Roughness.GetType()),
        };
    }

    private static string[] ObserveC06()
    {
        var edge = new Material("Edges", double.Epsilon, double.Epsilon, 100, 0, 1, 0, MaterialRoughness.Smooth);
        return new[]
        {
            "edge.accepted=" + MaterialState(edge),
            Capture("conductivity-zero", () => new Material("Bad", 0, 1, 100)),
            Capture("conductivity-nan", () => new Material("Bad", double.NaN, 1, 100)),
            Capture("conductivity-positive-infinity", () => new Material("Bad", double.PositiveInfinity, 1, 100)),
            Capture("density-zero", () => new Material("Bad", 1, 0, 100)),
            Capture("density-nan", () => new Material("Bad", 1, double.NaN, 100)),
            Capture("specific-heat-below-minimum", () => new Material("Bad", 1, 1, 99.999)),
            Capture("specific-heat-nan", () => new Material("Bad", 1, 1, double.NaN)),
            Capture("thermal-below-zero", () => new Material("Bad", 1, 1, 100, -0.01)),
            Capture("thermal-above-one", () => new Material("Bad", 1, 1, 100, 1.01)),
            Capture("solar-nan", () => new Material("Bad", 1, 1, 100, solarAbsorptance: double.NaN)),
            Capture("visible-positive-infinity", () => new Material("Bad", 1, 1, 100, visibleAbsorptance: double.PositiveInfinity)),
            Capture("roughness-undefined", () => new Material("Bad", 1, 1, 100, roughness: (MaterialRoughness)999)),
            Capture("name-null", () => new Material(null!, 1, 1, 100)),
            Capture("name-whitespace", () => new Material("   ", 1, 1, 100)),
        };
    }

    private static string[] ObserveC07()
    {
        Material material = ThermalMaterial();
        var layer = new Layer("Thermal_1mm", material, 0.001);
        Assert.Equal(30d, layer.UValue);
        Assert.Equal(100d, layer.HeatCapacityJoulesPerSquareMetreKelvin);
        return new[]
        {
            "layer.state=" + LayerState(layer),
            "layer.material_reference_same=" + Lower(ReferenceEquals(material, layer.Material)),
            "layer.U.exact=" + DoubleFact(layer.UValue),
            "layer.heat_capacity.exact=" + DoubleFact(layer.HeatCapacityJoulesPerSquareMetreKelvin),
            "layer.thermal_resistance.exact=" + DoubleFact(layer.ThermalResistance),
            "properties.get_only=" + Join(new[] { "Name", "Material", "ThicknessMetres", "UValue", "HeatCapacityJoulesPerSquareMetreKelvin" }.Select(name => name + "=" + Lower(!RequiredProperty(typeof(Layer), name).CanWrite))),
        };
    }

    private static string[] ObserveC08()
    {
        Material originalMaterial = ThermalMaterial();
        var original = new Layer("Original", originalMaterial, 0.001);
        string before = LayerState(original);
        var replacement = new Layer("Replacement", new Material("Other", 0.06, 500, 200), 0.002);
        string after = LayerState(original);
        Assert.Equal(before, after);
        return new[]
        {
            "original.before=" + before,
            "replacement.state=" + LayerState(replacement),
            "original.after=" + after,
            "original.unchanged=" + Lower(before == after),
            "material.reference_same=" + Lower(ReferenceEquals(originalMaterial, original.Material)),
            "instances.reference_same=" + Lower(ReferenceEquals(original, replacement)),
        };
    }

    private static string[] ObserveC09()
    {
        Material material = ThermalMaterial();
        var epsilon = new Layer("Epsilon", material, double.Epsilon);
        return new[]
        {
            "epsilon.accepted=" + LayerState(epsilon),
            Capture("material-null", () => new Layer("Bad", null!, 0.1)),
            Capture("thickness-zero", () => new Layer("Bad", material, 0)),
            Capture("thickness-negative", () => new Layer("Bad", material, -1)),
            Capture("thickness-nan", () => new Layer("Bad", material, double.NaN)),
            Capture("thickness-positive-infinity", () => new Layer("Bad", material, double.PositiveInfinity)),
            Capture("name-null", () => new Layer(null!, material, 0.1)),
            Capture("name-blank", () => new Layer(" ", material, 0.1)),
        };
    }

    private static string[] ObserveC10()
    {
        Material material = ThermalMaterial("ULP");
        var outside = new Layer("Outside", material, 0.001);
        var inside = new Layer("Inside", material, 0.01);
        var construction = new OpaqueConstruction("Layered", new[] { outside, inside });
        const long expectedUpstreamBits = 0x4005D1745D1745D2;
        const long directResistanceBits = 0x4005D1745D1745D1;
        Assert.Equal(expectedUpstreamBits, BitConverter.DoubleToInt64Bits(construction.UValue));
        Assert.Equal(directResistanceBits, BitConverter.DoubleToInt64Bits(1 / construction.ThermalResistance));
        Assert.Equal(1100d, construction.HeatCapacityJoulesPerSquareMetreKelvin);
        Assert.Equal(0.011d, construction.ThicknessMetres);
        return new[]
        {
            "construction.state=" + ConstructionState(construction),
            "construction.layer_reference_order=" + Join(new[] { Lower(ReferenceEquals(outside, construction.Layers[0])), Lower(ReferenceEquals(inside, construction.Layers[1])) }),
            "construction.U.bits=" + Bits(construction.UValue),
            "construction.U.expected_upstream_bits=" + unchecked((ulong)expectedUpstreamBits).ToString("X16", CultureInfo.InvariantCulture),
            "construction.inverse_thermal_resistance.bits=" + Bits(1 / construction.ThermalResistance),
            "construction.operation_order_is_one_ulp_witness=" + Lower(BitConverter.DoubleToInt64Bits(construction.UValue) == expectedUpstreamBits && BitConverter.DoubleToInt64Bits(1 / construction.ThermalResistance) == directResistanceBits),
            "construction.properties.get_only=" + Join(new[] { "Name", "Layers", "UValue", "ThicknessMetres", "HeatCapacityJoulesPerSquareMetreKelvin" }.Select(name => name + "=" + Lower(!RequiredProperty(typeof(OpaqueConstruction), name).CanWrite))),
        };
    }

    private static string[] ObserveC11()
    {
        Material firstMaterial = ThermalMaterial("First");
        Material secondMaterial = ThermalMaterial("Second");
        var first = new Layer("First_1mm", firstMaterial, 0.001);
        var second = new Layer("Second_10mm", secondMaterial, 0.01);
        var construction = new OpaqueConstruction("Pairs", new[] { first, second });
        ConstructorInfo constructor = Assert.Single(
            typeof(OpaqueConstruction).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(new[] { "First_1mm", "Second_10mm" }, construction.Layers.Select(item => item.Name));
        return new[]
        {
            "adapted_pairs.state=" + ConstructionState(construction),
            "adapted_pairs.generated_names=" + Join(construction.Layers.Select(item => item.Name)),
            "adapted_pairs.material_reference_order=" + Join(new[] { Lower(ReferenceEquals(firstMaterial, construction.Layers[0].Material)), Lower(ReferenceEquals(secondMaterial, construction.Layers[1].Material)) }),
            "native.constructor.parameters=" + Join(constructor.GetParameters().Select(ParameterFact)),
            "native.has_variadic_material_thickness_overload=false",
            "native.bool_thickness_representable=false",
            "adapted_pairs.U=" + DoubleFact(construction.UValue),
            "adapted_pairs.heat_capacity=" + DoubleFact(construction.HeatCapacityJoulesPerSquareMetreKelvin),
            "adapted_pairs.thickness=" + DoubleFact(construction.ThicknessMetres),
        };
    }

    private static string[] ObserveC12()
    {
        Material material = ThermalMaterial();
        var first = new Layer("First", material, 0.001);
        var second = new Layer("Second", material, 0.01);
        var original = new OpaqueConstruction("Original", new[] { first, second });
        OpaqueConstruction defaultReverse = original.Reverse();
        OpaqueConstruction customReverse = original.Reverse("Custom");
        string emptyName = Capture("reverse-empty-name", () => original.Reverse(string.Empty));
        Assert.Equal("Original_reversed", defaultReverse.Name);
        Assert.Equal("Custom", customReverse.Name);
        Assert.Equal(new[] { second, first }, defaultReverse.Layers);
        Assert.Equal(new[] { second, first }, customReverse.Layers);
        return new[]
        {
            "original.state=" + ConstructionState(original),
            "default_reverse.state=" + ConstructionState(defaultReverse),
            "custom_reverse.state=" + ConstructionState(customReverse),
            "default_reverse.layer_reference_order=" + Join(new[] { Lower(ReferenceEquals(second, defaultReverse.Layers[0])), Lower(ReferenceEquals(first, defaultReverse.Layers[1])) }),
            "custom_reverse.layer_reference_order=" + Join(new[] { Lower(ReferenceEquals(second, customReverse.Layers[0])), Lower(ReferenceEquals(first, customReverse.Layers[1])) }),
            "reverse.empty_name=" + emptyName,
            "reverse.result_reference_same=" + Lower(ReferenceEquals(original, defaultReverse)),
            "reverse.layers_collection_reference_same=" + Lower(ReferenceEquals(original.Layers, defaultReverse.Layers)),
            "reverse.method=" + MethodFact(RequiredMethod(typeof(OpaqueConstruction), nameof(OpaqueConstruction.Reverse))),
        };
    }

    private static string[] ObserveC13()
    {
        Layer sourceLayer = new("Source", ThermalMaterial(), 0.001);
        var source = new List<Layer> { sourceLayer };
        var construction = new OpaqueConstruction("Defensive", source);
        source.Clear();
        Assert.Single(construction.Layers);
        bool readOnlyList = construction.Layers is IList<Layer> list && list.IsReadOnly;
        return new[]
        {
            "defensive_copy.state_after_source_clear=" + ConstructionState(construction),
            "defensive_copy.source_count=" + source.Count,
            "defensive_copy.native_count=" + construction.Layers.Count,
            "defensive_copy.collection_reference_same=" + Lower(ReferenceEquals(source, construction.Layers)),
            "defensive_copy.list_is_read_only=" + Lower(readOnlyList),
            Capture("empty-layers", () => new OpaqueConstruction("Empty", Array.Empty<Layer>())),
            Capture("null-layers", () => new OpaqueConstruction("Null", null!)),
            Capture("null-layer-item", () => new OpaqueConstruction("NullItem", new Layer[] { null! })),
            Capture("null-name", () => new OpaqueConstruction(null!, new[] { sourceLayer })),
            Capture("blank-name", () => new OpaqueConstruction(" ", new[] { sourceLayer })),
            "native.mixed_variadic_overload=false",
            "native.odd_variadic_overload=false",
        };
    }

    private static string[] ObserveC14()
    {
        var glazing = new Glazing("Window", 1.6, 0.55);
        return new[]
        {
            "glazing.state=" + GlazingState(glazing),
            "glazing.U.exact=" + DoubleFact(glazing.UValueWattsPerSquareMetreKelvin),
            "glazing.G.exact=" + DoubleFact(glazing.SolarHeatGainCoefficient),
            "constructor=" + ConstructorFact(Assert.Single(typeof(Glazing).GetConstructors(BindingFlags.Public | BindingFlags.Instance))),
            "properties.get_only=" + Join(new[] { "Name", "UValueWattsPerSquareMetreKelvin", "SolarHeatGainCoefficient" }.Select(name => name + "=" + Lower(!RequiredProperty(typeof(Glazing), name).CanWrite))),
        };
    }

    private static string[] ObserveC15()
    {
        var original = new Glazing("Original", 2.5, 0.42);
        string before = GlazingState(original);
        var replacement = new Glazing("Replacement", 1.1, 0.6);
        string after = GlazingState(original);
        Assert.Equal(before, after);
        return new[]
        {
            "original.before=" + before,
            "replacement.state=" + GlazingState(replacement),
            "original.after=" + after,
            "original.unchanged=" + Lower(before == after),
            "instances.reference_same=" + Lower(ReferenceEquals(original, replacement)),
            Capture("separate-invalid-G", () => new Glazing("Rejected", 1, 1.1)),
        };
    }

    private static string[] ObserveC16()
    {
        var zeroG = new Glazing("ZeroG", double.Epsilon, 0);
        var upperG = new Glazing("UpperG", 1, 1);
        return new[]
        {
            "zero_G.accepted=" + GlazingState(zeroG),
            "upper_G.accepted=" + GlazingState(upperG),
            Capture("U-zero", () => new Glazing("Bad", 0, 0.5)),
            Capture("U-negative", () => new Glazing("Bad", -1, 0.5)),
            Capture("U-nan", () => new Glazing("Bad", double.NaN, 0.5)),
            Capture("U-positive-infinity", () => new Glazing("Bad", double.PositiveInfinity, 0.5)),
            Capture("G-negative", () => new Glazing("Bad", 1, -0.01)),
            Capture("G-above-one", () => new Glazing("Bad", 1, 1.01)),
            Capture("G-nan", () => new Glazing("Bad", 1, double.NaN)),
            Capture("G-positive-infinity", () => new Glazing("Bad", 1, double.PositiveInfinity)),
            Capture("name-null", () => new Glazing(null!, 1, 0.5)),
            Capture("name-blank", () => new Glazing(" ", 1, 0.5)),
        };
    }

    private static string[] ObserveC17()
    {
        var construction = new NoMassConstruction("NoMass", 2.5);
        Assert.Equal(0.4, construction.ThermalResistance);
        return new[]
        {
            "no_mass.state=" + NoMassState(construction),
            "no_mass.U.exact=" + DoubleFact(construction.UValueWattsPerSquareMetreKelvin),
            "no_mass.thermal_resistance.exact=" + DoubleFact(construction.ThermalResistance),
            "constructor=" + ConstructorFact(Assert.Single(typeof(NoMassConstruction).GetConstructors(BindingFlags.Public | BindingFlags.Instance))),
            "properties.get_only=" + Join(new[] { "Name", "UValueWattsPerSquareMetreKelvin", "ThermalResistance" }.Select(name => name + "=" + Lower(!RequiredProperty(typeof(NoMassConstruction), name).CanWrite))),
        };
    }

    private static string[] ObserveC18()
    {
        var original = new NoMassConstruction("Original", 2.5);
        string before = NoMassState(original);
        var replacement = new NoMassConstruction("Replacement", 1.25);
        string after = NoMassState(original);
        Assert.Equal(before, after);
        return new[]
        {
            "original.before=" + before,
            "replacement.state=" + NoMassState(replacement),
            "original.after=" + after,
            "original.unchanged=" + Lower(before == after),
            "instances.reference_same=" + Lower(ReferenceEquals(original, replacement)),
            Capture("separate-invalid-U", () => new NoMassConstruction("Rejected", 0)),
        };
    }

    private static string[] ObserveC19()
    {
        var epsilon = new NoMassConstruction("Epsilon", double.Epsilon);
        return new[]
        {
            "epsilon.accepted=" + NoMassState(epsilon),
            Capture("U-zero", () => new NoMassConstruction("Bad", 0)),
            Capture("U-negative", () => new NoMassConstruction("Bad", -1)),
            Capture("U-nan", () => new NoMassConstruction("Bad", double.NaN)),
            Capture("U-positive-infinity", () => new NoMassConstruction("Bad", double.PositiveInfinity)),
            Capture("name-null", () => new NoMassConstruction(null!, 1)),
            Capture("name-blank", () => new NoMassConstruction(" ", 1)),
        };
    }

    private static void ValidateDirectPythonParity(
        IReadOnlyList<JsonElement> fixtureCases,
        IReadOnlyList<NativeObservation> observations)
    {
        Assert.Equal(19, fixtureCases.Count);
        Assert.Equal(19, observations.Count);

        JsonElement c01 = Observations(fixtureCases[0]);
        string[] pythonRoughnessStrings = c01.GetProperty("members").EnumerateArray()
            .Select(item => RequiredString(item, "string")).ToArray();
        Assert.Equal(pythonRoughnessStrings, Enum.GetValues<MaterialRoughness>().Select(value => value.ToString()));
        Assert.Equal(5, c01.GetProperty("member_count").GetInt32());
        Assert.True(c01.GetProperty("class_is_str_subclass").GetBoolean());

        JsonElement c02 = Observations(fixtureCases[1]);
        AssertStringArray(c02.GetProperty("strings"),
            Enum.GetValues<MaterialRoughness>().Select(value => value.ToString()));

        JsonElement pythonMaterial = Observations(fixtureCases[3]).GetProperty("material");
        var nativeMaterial = new Material("Default", 0.72, 1920, 840);
        Assert.Equal(ReadEncodedDouble(pythonMaterial.GetProperty("conductivity")), nativeMaterial.ConductivityWattsPerMetreKelvin);
        Assert.Equal(ReadEncodedDouble(pythonMaterial.GetProperty("density")), nativeMaterial.DensityKilogramsPerCubicMetre);
        Assert.Equal(ReadEncodedDouble(pythonMaterial.GetProperty("specific_heat")), nativeMaterial.SpecificHeatJoulesPerKilogramKelvin);
        Assert.Equal(ReadEncodedDouble(pythonMaterial.GetProperty("thermal_absorptance")), nativeMaterial.ThermalAbsorptance);
        Assert.Equal(ReadEncodedDouble(pythonMaterial.GetProperty("solar_absorptance")), nativeMaterial.SolarAbsorptance);
        Assert.Equal(ReadEncodedDouble(pythonMaterial.GetProperty("visible_absorptance")), nativeMaterial.VisibleAbsorptance);
        Assert.Equal(RequiredString(pythonMaterial.GetProperty("roughness"), "string"), nativeMaterial.Roughness.ToString());

        JsonElement pythonLayer = Observations(fixtureCases[6]).GetProperty("layer");
        var nativeLayer = new Layer("Thermal_1mm", ThermalMaterial(), 0.001);
        Assert.Equal(ReadEncodedDouble(pythonLayer.GetProperty("U")), nativeLayer.UValue);
        Assert.Equal(ReadEncodedDouble(pythonLayer.GetProperty("heat_capacity")), nativeLayer.HeatCapacityJoulesPerSquareMetreKelvin);
        Assert.Equal(ReadEncodedDouble(pythonLayer.GetProperty("thickness")), nativeLayer.ThicknessMetres);

        JsonElement c10 = Observations(fixtureCases[9]);
        JsonElement pythonConstruction = c10.GetProperty("construction");
        var witnessMaterial = ThermalMaterial("ULP");
        var nativeConstruction = new OpaqueConstruction("Layered", new[]
        {
            new Layer("Outside", witnessMaterial, 0.001),
            new Layer("Inside", witnessMaterial, 0.01),
        });
        double pythonWitness = ReadEncodedDouble(c10.GetProperty("ulp_witness_U"));
        Assert.Equal("0x1.5d1745d1745d2p+1",
            RequiredString(c10.GetProperty("ulp_witness_U").GetProperty("value"), "hex"));
        Assert.Equal(0x4005D1745D1745D2, BitConverter.DoubleToInt64Bits(pythonWitness));
        Assert.Equal(BitConverter.DoubleToInt64Bits(pythonWitness), BitConverter.DoubleToInt64Bits(nativeConstruction.UValue));
        Assert.Equal(ReadEncodedDouble(pythonConstruction.GetProperty("heat_capacity")), nativeConstruction.HeatCapacityJoulesPerSquareMetreKelvin);
        Assert.Equal(ReadEncodedDouble(pythonConstruction.GetProperty("thickness")), nativeConstruction.ThicknessMetres);

        JsonElement c11 = Observations(fixtureCases[10]);
        AssertStringArray(c11.GetProperty("generated_layer_names"), "First_1mm", "Second_10mm");
        Assert.Equal(BitConverter.DoubleToInt64Bits(pythonWitness),
            BitConverter.DoubleToInt64Bits(ReadEncodedDouble(c11.GetProperty("constructed").GetProperty("U"))));
        Assert.Equal("First_1000mm",
            RequiredString(c11.GetProperty("bool_thickness").GetProperty("layer_names")[0].GetProperty("value"), "value"));

        JsonElement c12 = Observations(fixtureCases[11]);
        Assert.Equal("Original_reversed", ReadEncodedString(c12.GetProperty("default_name")));
        Assert.Equal(string.Empty, ReadEncodedString(c12.GetProperty("custom_name")));
        Assert.True(c12.GetProperty("shares_every_layer").GetBoolean());

        JsonElement pythonGlazing = Observations(fixtureCases[13]).GetProperty("glazing");
        var nativeGlazing = new Glazing("Window", 1.6, 0.55);
        Assert.Equal(ReadEncodedDouble(pythonGlazing.GetProperty("U")), nativeGlazing.UValueWattsPerSquareMetreKelvin);
        Assert.Equal(ReadEncodedDouble(pythonGlazing.GetProperty("G")), nativeGlazing.SolarHeatGainCoefficient);

        JsonElement pythonNoMass = Observations(fixtureCases[16]).GetProperty("construction");
        var nativeNoMass = new NoMassConstruction("NoMass", 2.5);
        Assert.Equal(ReadEncodedDouble(pythonNoMass.GetProperty("U")), nativeNoMass.UValueWattsPerSquareMetreKelvin);
    }

    private static object CreateReceipt(
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations,
        IReadOnlyList<int> caseIndices,
        IReadOnlyDictionary<string, string> factsSha256) => new
    {
        assertion_id = target.AssertionId,
        adaptation_id = target.AdaptationId,
        classification = target.Classification,
        target_symbol = target.Symbol,
        native_target = target.NativeTarget,
        native_implementation = NativeImplementationFor(target.Symbol),
        source_receipt = SourceReceiptObject(target),
        artifacts = new
        {
            fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
            generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
            python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
            public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
            native_sources = NativeArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
        },
        case_coverage = caseIndices.Select(index => Cases[index].CaseId).ToArray(),
        observations = caseIndices.Select(index => new
        {
            case_id = Cases[index].CaseId,
            python_facts_sha256 = factsSha256[Cases[index].CaseId],
            native_fact_count = observations[index].Facts.Length,
            native_facts_sha256 = observations[index].FactsSha256,
            native_facts = observations[index].Facts,
        }).ToArray(),
        verification = new
        {
            claims_active_load = false,
            exercised_load = "not_applicable",
            kind = "cross_language",
            native_route = "typed-public-construction-domain-routes-with-reflection-limited-to-public-abi-metadata",
            structural_only = false,
        },
        scope = new
        {
            exact_target_count = 35,
            equivalent_target_count = 11,
            exception_target_count = 24,
            target_inventory_indices = Targets.Select(item => item.InventoryIndex).ToArray(),
            full_symbol_closure = false,
            full_construction_family_closure = false,
            full_idf_closure = false,
            excluded_adjacent_symbols = new[]
            {
                "AirBoundary", "AirBoundary.__init__", "AirBoundary.__repr__", "AirBoundary.__str__", "AirBoundary.to_idf_object",
                "Construction.__eq__", "Construction.__hash__", "Construction.to_idf_object",
                "Glazing.__repr__", "Glazing.__str__", "Glazing.to_idf_object",
                "Layer.__eq__", "Layer.__hash__", "Layer.to_idf_object", "Material.__eq__",
                "NoMassConstruction.__repr__", "NoMassConstruction.__str__", "NoMassConstruction.to_idf_object",
            },
            unresolved_behavior = UnresolvedFor(target),
        },
        upstream = new
        {
            ast_sha256 = UpstreamAstSha256,
            commit = UpstreamCommit,
            inventory_file_bytes = InventoryBytes,
            inventory_file_sha256 = InventoryFileSha256,
            inventory_sha256 = InventoryContentSha256,
            source_bytes = UpstreamBytes,
            source_sha256 = UpstreamSourceSha256,
        },
    };

    private static string NativeImplementationFor(string symbol) => symbol switch
    {
        "Construction" => "Dragons.InvisibleDragon.Construction.Construction sealed class and validated defensive-copy constructor",
        "Construction.U" => "Dragons.InvisibleDragon.Construction.Construction.UValue",
        "Construction.__init__" => "Dragons.InvisibleDragon.Construction.Construction.Construction(string,IEnumerable<Layer>)",
        "Construction.heat_capacity" => "Dragons.InvisibleDragon.Construction.Construction.HeatCapacityJoulesPerSquareMetreKelvin",
        "Construction.reversed" => "Dragons.InvisibleDragon.Construction.Construction.Reverse(string?)",
        "Construction.thickness" => "Dragons.InvisibleDragon.Construction.Construction.ThicknessMetres",
        "Glazing" => "Dragons.InvisibleDragon.Construction.Glazing sealed immutable record",
        "Glazing.G" => "Dragons.InvisibleDragon.Construction.Glazing.SolarHeatGainCoefficient",
        "Glazing.U" => "Dragons.InvisibleDragon.Construction.Glazing.UValueWattsPerSquareMetreKelvin",
        "Glazing.__init__" => "Dragons.InvisibleDragon.Construction.Glazing.Glazing(string,double,double)",
        "Layer" => "Dragons.InvisibleDragon.Construction.Layer sealed immutable record",
        "Layer.U" => "Dragons.InvisibleDragon.Construction.Layer.UValue",
        "Layer.__init__" => "Dragons.InvisibleDragon.Construction.Layer.Layer(string,Material,double)",
        "Layer.heat_capacity" => "Dragons.InvisibleDragon.Construction.Layer.HeatCapacityJoulesPerSquareMetreKelvin",
        "Layer.material" => "Dragons.InvisibleDragon.Construction.Layer.Material",
        "Layer.thickness" => "Dragons.InvisibleDragon.Construction.Layer.ThicknessMetres",
        "Material" => "Dragons.InvisibleDragon.Construction.Material sealed immutable record",
        "Material.__init__" => "Dragons.InvisibleDragon.Construction.Material.Material validated typed constructor",
        "Material.conductivity" => "Dragons.InvisibleDragon.Construction.Material.ConductivityWattsPerMetreKelvin",
        "Material.density" => "Dragons.InvisibleDragon.Construction.Material.DensityKilogramsPerCubicMetre",
        "Material.roughness" => "Dragons.InvisibleDragon.Construction.Material.Roughness",
        "Material.solar_absorptance" => "Dragons.InvisibleDragon.Construction.Material.SolarAbsorptance",
        "Material.specific_heat" => "Dragons.InvisibleDragon.Construction.Material.SpecificHeatJoulesPerKilogramKelvin",
        "Material.thermal_absorptance" => "Dragons.InvisibleDragon.Construction.Material.ThermalAbsorptance",
        "Material.visible_absorptance" => "Dragons.InvisibleDragon.Construction.Material.VisibleAbsorptance",
        "MaterialRoughness" => "Dragons.InvisibleDragon.Construction.MaterialRoughness enum",
        "MaterialRoughness.MEDIUMROUGH" => "Dragons.InvisibleDragon.Construction.MaterialRoughness.MediumRough",
        "MaterialRoughness.MEDIUMSMOOTH" => "Dragons.InvisibleDragon.Construction.MaterialRoughness.MediumSmooth",
        "MaterialRoughness.ROUGH" => "Dragons.InvisibleDragon.Construction.MaterialRoughness.Rough",
        "MaterialRoughness.SMOOTH" => "Dragons.InvisibleDragon.Construction.MaterialRoughness.Smooth",
        "MaterialRoughness.VERYROUGH" => "Dragons.InvisibleDragon.Construction.MaterialRoughness.VeryRough",
        "MaterialRoughness.__str__" => "Dragons.InvisibleDragon.Construction.MaterialRoughness.ToString",
        "NoMassConstruction" => "Dragons.InvisibleDragon.Construction.NoMassConstruction sealed immutable record",
        "NoMassConstruction.U" => "Dragons.InvisibleDragon.Construction.NoMassConstruction.UValueWattsPerSquareMetreKelvin",
        "NoMassConstruction.__init__" => "Dragons.InvisibleDragon.Construction.NoMassConstruction.NoMassConstruction(string,double)",
        _ => throw new Xunit.Sdk.XunitException($"No native implementation for '{symbol}'."),
    };

    private static string[] UnresolvedFor(TargetBinding target)
    {
        if (target.Classification == "equivalent")
        {
            return Array.Empty<string>();
        }

        var values = new List<string>
        {
            target.OracleAdaptationId + "-Python-behavior-outside-bounded-native-counterpart",
        };
        if (target.Symbol.StartsWith("Material", StringComparison.Ordinal)
            && !target.Symbol.StartsWith("MaterialRoughness", StringComparison.Ordinal))
        {
            values.Add("Python-mutable-aliased-material-state-and-bool/nonfinite-validator-domain");
        }
        if (target.Symbol.StartsWith("Layer", StringComparison.Ordinal))
        {
            values.Add("Python-mutable-layer-child-and-thickness-alias-state");
        }
        if (target.Symbol.StartsWith("Construction", StringComparison.Ordinal))
        {
            values.Add("Python-variadic-empty-mutable-construction-and-shared-child-state");
        }
        if (target.Symbol.StartsWith("Glazing", StringComparison.Ordinal))
        {
            values.Add("Python-mutable-unbounded-G-and-bool/nonfinite-U/G-state");
        }
        if (target.Symbol.StartsWith("NoMassConstruction", StringComparison.Ordinal))
        {
            values.Add("Python-mutable-bool/nonfinite-no-mass-U-state");
        }
        if (target.Symbol == "MaterialRoughness")
        {
            values.Add("Python-str-Enum-construction/equality/topology-versus-native-integral-enum");
        }
        return values.ToArray();
    }

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations,
        IReadOnlyList<int> caseIndices,
        IReadOnlyDictionary<string, string> factsSha256)
    {
        AssertUniqueKeysRecursive(receipt);
        AssertNoHostPaths(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.Classification, RequiredString(receipt, "classification"));
        if (target.AdaptationId is null)
        {
            Assert.Equal(JsonValueKind.Null, receipt.GetProperty("adaptation_id").ValueKind);
        }
        else
        {
            Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        }
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));
        Assert.Equal(target.NativeTarget, RequiredString(receipt, "native_target"));
        Assert.Equal(NativeImplementationFor(target.Symbol), RequiredString(receipt, "native_implementation"));
        AssertSourceReceipt(receipt.GetProperty("source_receipt"), target, includeIndex: true);
        AssertStringArray(receipt.GetProperty("case_coverage"),
            caseIndices.Select(index => Cases[index].CaseId));

        JsonElement[] receiptObservations = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(caseIndices.Count, receiptObservations.Length);
        for (int index = 0; index < caseIndices.Count; index++)
        {
            int caseIndex = caseIndices[index];
            JsonElement actual = receiptObservations[index];
            Assert.Equal(Cases[caseIndex].CaseId, RequiredString(actual, "case_id"));
            Assert.Equal(factsSha256[Cases[caseIndex].CaseId], RequiredString(actual, "python_facts_sha256"));
            Assert.Equal(observations[caseIndex].Facts.Length, actual.GetProperty("native_fact_count").GetInt32());
            Assert.Equal(observations[caseIndex].FactsSha256, RequiredString(actual, "native_facts_sha256"));
            AssertStringArray(actual.GetProperty("native_facts"), observations[caseIndex].Facts);
        }

        JsonElement verification = receipt.GetProperty("verification");
        Assert.False(verification.GetProperty("claims_active_load").GetBoolean());
        Assert.Equal("not_applicable", RequiredString(verification, "exercised_load"));
        Assert.Equal("cross_language", RequiredString(verification, "kind"));
        Assert.False(verification.GetProperty("structural_only").GetBoolean());

        JsonElement artifacts = receipt.GetProperty("artifacts");
        AssertArtifactReceipt(artifacts.GetProperty("fixture"), FixturePath, FixtureBytes, FixtureSha256);
        AssertArtifactReceipt(artifacts.GetProperty("generator"), GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertArtifactReceipt(artifacts.GetProperty("python_validator"), ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertArtifactReceipt(artifacts.GetProperty("public_inventory"), InventoryPath, InventoryBytes, InventoryFileSha256);
        JsonElement[] nativeSources = artifacts.GetProperty("native_sources").EnumerateArray().ToArray();
        Assert.Equal(NativeArtifacts.Length, nativeSources.Length);
        for (int index = 0; index < NativeArtifacts.Length; index++)
        {
            AssertArtifactReceipt(
                nativeSources[index],
                NativeArtifacts[index].Path,
                NativeArtifacts[index].Bytes,
                NativeArtifacts[index].Sha256);
        }

        JsonElement scope = receipt.GetProperty("scope");
        Assert.Equal(35, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(11, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(24, scope.GetProperty("exception_target_count").GetInt32());
        Assert.False(scope.GetProperty("full_symbol_closure").GetBoolean());
        Assert.False(scope.GetProperty("full_construction_family_closure").GetBoolean());
        Assert.False(scope.GetProperty("full_idf_closure").GetBoolean());
        Assert.Equal(18, scope.GetProperty("excluded_adjacent_symbols").GetArrayLength());
        Assert.Equal(Targets.Select(item => item.InventoryIndex),
            scope.GetProperty("target_inventory_indices").EnumerateArray().Select(item => item.GetInt32()));
        AssertStringArray(scope.GetProperty("unresolved_behavior"), UnresolvedFor(target));

        JsonElement upstream = receipt.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(UpstreamAstSha256, RequiredString(upstream, "ast_sha256"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(upstream, "source_sha256"));
        Assert.Equal(UpstreamBytes, upstream.GetProperty("source_bytes").GetInt32());
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(InventoryFileSha256, RequiredString(upstream, "inventory_file_sha256"));
        Assert.Equal(InventoryBytes, upstream.GetProperty("inventory_file_bytes").GetInt32());
    }

    private static void AssertArtifactReceipt(
        JsonElement value,
        string expectedPath,
        int expectedBytes,
        string expectedSha256)
    {
        AssertKeys(value, "bytes", "path", "sha256");
        Assert.Equal(expectedPath, RequiredString(value, "path"));
        Assert.Equal(expectedBytes, value.GetProperty("bytes").GetInt32());
        Assert.Equal(expectedSha256, RequiredString(value, "sha256"));
    }

    private static object SourceReceiptObject(TargetBinding target) => new
    {
        body_hash = target.BodyHash,
        inventory_index = target.InventoryIndex,
        kind = target.Kind,
        path = UpstreamPath,
        signature_hash = target.SignatureHash,
        symbol = target.Symbol,
        symbol_hash = target.SymbolHash,
    };

    private static void AssertSourceReceipt(JsonElement value, TargetBinding target, bool includeIndex)
    {
        AssertKeys(value, includeIndex
            ? new[] { "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash" }
            : new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" });
        if (includeIndex)
        {
            Assert.Equal(target.InventoryIndex, value.GetProperty("inventory_index").GetInt32());
        }
        Assert.Equal(target.Kind, RequiredString(value, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(value, "path"));
        Assert.Equal(target.Symbol, RequiredString(value, "symbol"));
        Assert.Equal(target.SymbolHash, RequiredString(value, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(value, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(value, "body_hash"));
    }

    private static TargetBinding TargetBySymbol(string symbol) =>
        Assert.Single(Targets, item => item.Symbol == symbol);

    private static JsonElement Observations(JsonElement fixtureCase) =>
        fixtureCase.GetProperty("python").GetProperty("facts").GetProperty("observations");

    private static double ReadEncodedDouble(JsonElement value)
    {
        JsonElement encoded = value.TryGetProperty("kind", out _) ? value : value.GetProperty("value");
        return RequiredString(encoded, "kind") switch
        {
            "float" => double.Parse(RequiredString(encoded, "repr"), NumberStyles.Float, CultureInfo.InvariantCulture),
            "int" => double.Parse(RequiredString(encoded, "value"), NumberStyles.Integer, CultureInfo.InvariantCulture),
            "bool" => encoded.GetProperty("value").GetBoolean() ? 1d : 0d,
            string kind => throw new Xunit.Sdk.XunitException($"Encoded '{kind}' is not a finite numeric value."),
        };
    }

    private static string ReadEncodedString(JsonElement value)
    {
        JsonElement encoded = value.TryGetProperty("kind", out _) ? value : value.GetProperty("value");
        Assert.Equal("str", RequiredString(encoded, "kind"));
        return encoded.GetProperty("value").GetString()!;
    }

    private static Material ThermalMaterial(string name = "Thermal") =>
        new(name, 0.03, 1000, 100);

    private static string MaterialState(Material value) => Join(new[]
    {
        "Name=" + value.Name,
        "Conductivity=" + DoubleFact(value.ConductivityWattsPerMetreKelvin),
        "Density=" + DoubleFact(value.DensityKilogramsPerCubicMetre),
        "SpecificHeat=" + DoubleFact(value.SpecificHeatJoulesPerKilogramKelvin),
        "ThermalAbsorptance=" + DoubleFact(value.ThermalAbsorptance),
        "SolarAbsorptance=" + DoubleFact(value.SolarAbsorptance),
        "VisibleAbsorptance=" + DoubleFact(value.VisibleAbsorptance),
        "Roughness=" + value.Roughness,
    });

    private static string LayerState(Layer value) => Join(new[]
    {
        "Name=" + value.Name,
        "Material=" + value.Material.Name,
        "Thickness=" + DoubleFact(value.ThicknessMetres),
        "U=" + DoubleFact(value.UValue),
        "HeatCapacity=" + DoubleFact(value.HeatCapacityJoulesPerSquareMetreKelvin),
    });

    private static string ConstructionState(OpaqueConstruction value) => Join(new[]
    {
        "Name=" + value.Name,
        "Layers=" + Join(value.Layers.Select(LayerState)),
        "Thickness=" + DoubleFact(value.ThicknessMetres),
        "U=" + DoubleFact(value.UValue),
        "HeatCapacity=" + DoubleFact(value.HeatCapacityJoulesPerSquareMetreKelvin),
    });

    private static string GlazingState(Glazing value) => Join(new[]
    {
        "Name=" + value.Name,
        "U=" + DoubleFact(value.UValueWattsPerSquareMetreKelvin),
        "G=" + DoubleFact(value.SolarHeatGainCoefficient),
    });

    private static string NoMassState(NoMassConstruction value) => Join(new[]
    {
        "Name=" + value.Name,
        "U=" + DoubleFact(value.UValueWattsPerSquareMetreKelvin),
    });

    private static string[] MaterialPropertyNames() => new[]
    {
        "Name", "ConductivityWattsPerMetreKelvin", "DensityKilogramsPerCubicMetre",
        "SpecificHeatJoulesPerKilogramKelvin", "ThermalAbsorptance", "SolarAbsorptance",
        "VisibleAbsorptance", "Roughness",
    };

    private static string Capture(string phase, Func<object?> action)
    {
        try
        {
            object? value = action();
            return phase + "=returned|type=" + (value is null ? "<null>" : TypeName(value.GetType()));
        }
        catch (Exception exception)
        {
            string parameter = exception is ArgumentException argument
                ? argument.ParamName ?? "<none>"
                : "<not-applicable>";
            return phase + "=" + exception.GetType().Name + "|param=" + parameter;
        }
    }

    private static void AssertClass(Type type, bool sealedType, bool implementsSurfaceConstruction)
    {
        Assert.True(type.IsClass);
        Assert.Equal(sealedType, type.IsSealed);
        Assert.Equal(implementsSurfaceConstruction, typeof(ISurfaceConstruction).IsAssignableFrom(type));
    }

    private static void AssertParameterTypes(MethodBase method, params Type[] expected) =>
        Assert.Equal(expected, method.GetParameters().Select(item => item.ParameterType));

    private static PropertyInfo RequiredProperty(Type type, string name) =>
        Assert.IsAssignableFrom<PropertyInfo>(type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance));

    private static MethodInfo RequiredMethod(Type type, string name) =>
        Assert.IsAssignableFrom<MethodInfo>(type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance));

    private static void ValidateProperty(Type type, string name, Type propertyType)
    {
        PropertyInfo property = RequiredProperty(type, name);
        Assert.Equal(propertyType, property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.NotNull(property.GetMethod);
        Assert.Null(property.SetMethod);
    }

    private static string ConstructorFact(ConstructorInfo constructor) =>
        constructor.DeclaringType!.Name + "(" + Join(constructor.GetParameters().Select(ParameterFact)) + ")";

    private static string MethodFact(MethodInfo method) => Join(new[]
    {
        "name=" + method.Name,
        "return=" + TypeName(method.ReturnType),
        "parameters=" + Join(method.GetParameters().Select(ParameterFact)),
    });

    private static string ParameterFact(ParameterInfo parameter) => string.Join(
        ",",
        "name=" + parameter.Name,
        "type=" + TypeName(parameter.ParameterType),
        "optional=" + Lower(parameter.IsOptional),
        "has_default=" + Lower(parameter.HasDefaultValue),
        "default=" + (parameter.HasDefaultValue ? Scalar(parameter.DefaultValue) : "<none>"));

    private static string Scalar(object? value) => value switch
    {
        null => "<null>",
        double number => DoubleFact(number),
        Enum enumValue => enumValue.ToString(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>",
    };

    private static string DoubleFact(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture) + "|bits=" + Bits(value);

    private static string Bits(double value) =>
        unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("X16", CultureInfo.InvariantCulture);

    private static string TypeName(Type type) => type.FullName ?? type.Name;

    private static string Lower(bool value) => value ? "true" : "false";

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
    };

    private static object DiscoverArtifact(string path)
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(path));
        return new
        {
            path,
            bytes = bytes.Length,
            sha256 = Sha256(bytes),
        };
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

    private static void AssertUniqueKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.True(names.Add(property.Name), $"Duplicate JSON key '{property.Name}'.");
                AssertUniqueKeysRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertUniqueKeysRecursive(item);
            }
        }
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        string[] actual = value.EnumerateObject().Select(item => item.Name)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal), actual);
    }

    private static void AssertStringArray(JsonElement value, params string[] expected) =>
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!).ToArray());

    private static void AssertStringArray(JsonElement value, IEnumerable<string> expected) =>
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));

    private static string RequiredString(JsonElement value, string property)
    {
        JsonElement result = value.GetProperty(property);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        return result.GetString()!;
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        foreach (string candidate in EnumerateStringsAndKeys(value))
        {
            Assert.DoesNotMatch("^[A-Za-z]:[\\\\/]", candidate);
            Assert.False(candidate.StartsWith('/'), candidate);
            Assert.DoesNotContain("\\\\", candidate, StringComparison.Ordinal);
        }
    }

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        foreach (string candidate in EnumerateStringsAndKeys(value))
        {
            Assert.DoesNotMatch("(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])", candidate);
            Assert.DoesNotMatch("(?i)(?<![0-9a-f])[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?![0-9a-f])", candidate);
            Assert.DoesNotMatch("(?<!\\d)\\d{4}-\\d{2}-\\d{2}[T ][0-2]\\d:[0-5]\\d:[0-5]\\d", candidate);
        }
    }

    private static IEnumerable<string> EnumerateStringsAndKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            yield return value.GetString()!;
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                yield return property.Name;
                foreach (string child in EnumerateStringsAndKeys(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                foreach (string child in EnumerateStringsAndKeys(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number));
            Assert.True(double.IsFinite(number));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoNonFiniteJsonNumbers(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoNonFiniteJsonNumbers(item);
            }
        }
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

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record CaseDefinition(string Scenario, string CaseId, string Subfamily);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record NativeObservation(string Scenario, string[] Facts, string FactsSha256);

    private sealed record FixtureContract(
        JsonElement[] Cases,
        IReadOnlyDictionary<string, int[]> Coverage,
        IReadOnlyDictionary<string, string> FactsSha256);

    private sealed record TargetBinding(
        string Symbol,
        int InventoryIndex,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string? AdaptationId,
        string OracleAdaptationId,
        string Classification,
        string NativeTarget);
}
