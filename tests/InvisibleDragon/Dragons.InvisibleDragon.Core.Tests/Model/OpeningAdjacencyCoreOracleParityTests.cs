using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class OpeningAdjacencyCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-shape-opening-adjacency-core-oracle.json";
    private const int FixtureBytes = 260_256;
    private const string FixtureSha256 =
        "sha256:1eb9d258baa9471665d1470498d6855db7e7fde6bc89ac7a259d8908b6a3fe64";
    private const string FixtureSchema =
        "dragons.python-reference.dragon-shape-opening-adjacency-core.v1";
    private const string CasesSha256 =
        "sha256:ee98651aeaf270f3d9fb07a862950ffba343dc757de6523541a60daf0b3c392a";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_shape_opening_adjacency_core_oracle.py";
    private const int GeneratorBytes = 91_181;
    private const string GeneratorSha256 =
        "sha256:004eb87cbe18ddf3ac8c6c919c708d78e52182c585ac55b8994afbc7ff1ecec2";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_shape_opening_adjacency_core_oracle.py";
    private const int ValidatorBytes = 26_850;
    private const string ValidatorSha256 =
        "sha256:cf017361f5914deaa0f777a2538ac40cea8cf173a0a7334192af3f14d793699b";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/shape.py";
    private const int UpstreamBytes = 27_438;
    private const string UpstreamSourceSha256 =
        "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c";
    private const string UpstreamAstSha256 =
        "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.OpeningAdjacencyCoreOracleParityTests.MatchesPinnedOpeningAdjacencyCoreThroughBoundedNativeRoutes";

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Shading.cs", 1_944,
            "sha256:e125e43e56a69fbb4707e1553d8a3318280b1d3356ec8c403256a6adc5001ef3"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Openings.cs", 2_396,
            "sha256:3bca5b2a25574c58318eb55fd7f9a2c121a05e5c5645224a8e91c9ba92474588"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Surface.cs", 7_719,
            "sha256:a4d2d35982c8aff254c0c8d74982e13394db2a770f38691710f9739f8b0a38e8"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/SurfaceBoundary.cs", 1_903,
            "sha256:fc745e92061a0e8b1429399836f8a268b0d551e644f75f800a9cf987712c9d7a"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/SurfaceAdjacency.cs", 4_035,
            "sha256:d78880fc40340ac3a2cfa4c63ff048f68347cabf114dc856ff18dc9666051190"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs", 21_985,
            "sha256:60357af37bea1f6e7dd0640254a30761ed4097d53751183e5902c2efa62a0f28"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_723,
            "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4"),
    };

    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };

    private static readonly CaseBinding[] Cases =
    {
        Case("A01", "a01-blind-representative",
            "sha256:b2bf732059c388f149c4a3375e55c7d0b56fc31a9d4c714101511ab7408217d0",
            "sha256:f9080ec5ff6a7bbb6d9788e458b572a2957ff2a677148a3ba513fc73a8158b3f",
            new[] { "permissive-python-blind-state" }, new[] { "Blind", "Blind.__init__" }),
        Case("A02", "a02-blind-unchecked-invalid-state",
            "sha256:45a43e2bacd6564067fb1bc9c5c2d27459d8af3dbc776235035ea25a68ae9345",
            "sha256:876b0cd631164560a6e6e5badcd9fc848f3325b049d0291f90101a82198ed7ab",
            new[] { "permissive-python-blind-state" }, new[] { "Blind.__init__" }),
        Case("A03", "a03-shade-representative",
            "sha256:5fdbd59ce5a497d699a5b9778ba0f85176e2913b55d23d435ab759695d67ff6b",
            "sha256:4535f170e18df765a101f38253a642afbff4dca14449308bb15d6558135f96f7",
            new[] { "permissive-python-shade-state" }, new[] { "Shade", "Shade.__init__" }),
        Case("A04", "a04-shade-excessive-optical-sum",
            "sha256:d4f1fa7308fe19242606b1a868e55d35bd1c39c738c14aeee4a7d8f18d5a7932",
            "sha256:6443dc89c3fdda5446a32f31250b8cf96efe117643f0fa37be9642f7d0b13792",
            new[] { "permissive-python-shade-state" }, new[] { "Shade.__init__" }),
        Case("A05", "a05-shading-direct-instantiation",
            "sha256:ef5c444a8bea4f22fb93b436c893905d040bfc9e74fb305e3e100627c70bced2",
            "sha256:ffcd94abf936bbbf60c884dcc27140b892b2a0f8cfb08e79036a7f3c96632c6b",
            new[] { "directly-instantiable-empty-python-shading" }, new[] { "Shading" }),
        Case("A06", "a06-window-shading-variants",
            "sha256:7b9d16ee52fc8aa1582b6ba8e7297aedc1bf307729e53e5d17912ea795ec29d7",
            "sha256:4dce9b5e6485f3b3be1d13335d1d423c48d32bec3410419d14781d4e76ed87ed",
            new[] { "permissive-python-window-state" }, new[] { "Window", "Window.__init__" }, new[] { "Blind", "Shade" }),
        Case("A07", "a07-window-unchecked-invalid-mutable",
            "sha256:d4c3f02f82d44a7b461d50e70f84eb284b67dfcf61ff1cead93fb89595d59c51",
            "sha256:02750bd234014e81870497cf6cd8511c524797ac85797d787f4ef31b650108c9",
            new[] { "permissive-python-window-state" }, new[] { "Window.__init__" }),
        Case("A08", "a08-door-representative",
            "sha256:bc94adb5e13a599494c8bee1a853c8e969ee2f8a6f70e250b02ff693df1d383f",
            "sha256:6c121f349750c9547ddb069adb18235000120a63842c5f58b56cbda7b27477a0",
            new[] { "permissive-python-door-state" }, new[] { "Door", "Door.__init__" }),
        Case("A09", "a09-door-unchecked-invalid-mutable",
            "sha256:24e0063bea60899ce7162878a5e69f3270b9eaa60b3a1aea076726402e8344bf",
            "sha256:93b39be1ee324732ddc2601a2c3d6d8c8aff2d91864781c03e614bb3ea7d1eb7",
            new[] { "permissive-python-door-state" }, new[] { "Door.__init__" }),
        Case("A10", "a10-surface-shared-default-opening-lists",
            "sha256:422c402fd0515082f8e7fe591aef027e56cc758a6eaee37434825388e6c49b2f",
            "sha256:ccaeeade20e5f5701b4237b4fec87d00ee9d220c56c0692de8bef43f47323ec5",
            new[] { "aliased-python-surface-opening-inputs" }, new[] { "Surface.__init__" }),
        Case("A11", "a11-surface-explicit-mixed-opening-alias-order",
            "sha256:f91da16dad8709b62464757ee82d0ab0f81f2a9399904a26a5a84428125da36b",
            "sha256:e4c3da1a51457308b722dadf09a1247aef504583481a2c2fc2bd98f43d5c25d3",
            new[] { "aliased-python-surface-opening-inputs" }, new[] { "Surface.__init__" }, new[] { "Window", "Door" }),
        Case("A12", "a12-surface-blinded-window-fresh-order",
            "sha256:ec504f49dc42679c5f7ce4132ede8fd12bef3b3fd05cc0852df0cc2b729e4ce2",
            "sha256:44f0693db9b99c8cc39e1853ae9c6ae8689b88f301d34e3e4e1103065d7c0c1d",
            new[] { "fresh-python-blinded-window-projection" }, new[] { "Surface.blinded_window" }, new[] { "Window", "Blind", "Shade" }),
        Case("A13", "a13-boundary-enum-and-unlinked-zone",
            "sha256:1086dc82bc075cd6af3da1899388bbf1b188d42c28f824ce938960a18352c1a4",
            "sha256:182810f5fe7be8cb5171347a101b5ff934772daa8251d81b55eb9081271043d1",
            new[] { "lowercase-python-surface-boundary-enum", "mutable-reciprocal-python-surface-adjacency" },
            new[] { "Surface.boundary", "SurfaceBoundaryCondition", "SurfaceBoundaryCondition.ADIABATIC", "SurfaceBoundaryCondition.GROUND", "SurfaceBoundaryCondition.OUTDOOR", "SurfaceBoundaryCondition.ZONE", "SurfaceBoundaryCondition.__str__" },
            new[] { "Surface.__init__" }),
        Case("A14", "a14-boundary-reciprocal-adjacency",
            "sha256:21f4e7f4d7f06134fd9ea09db671ef1359e49f3468003d1f64ab3d30c7760f81",
            "sha256:fab7534265a39f9ed910248300a8d084cf9504dc25ed3b3de3347f56a181b5dd",
            new[] { "mutable-reciprocal-python-surface-adjacency" }, new[] { "Surface.boundary" }, new[] { "Surface.__init__" }),
        Case("A15", "a15-boundary-stale-reassignment-and-self",
            "sha256:55cbbe6652a044e435bf39628a6c877d3542084cf1d56fa554737214dc742528",
            "sha256:516318d8c182edd72b42d63bef9749818cbe220fa2dad2206affa2e2a3f35c53",
            new[] { "mutable-reciprocal-python-surface-adjacency" }, new[] { "Surface.boundary" }, new[] { "Surface.__init__" }),
        Case("A16", "a16-adjacency-positional-zip-truncation",
            "sha256:646013d5fd36efdd5e77c041b32e8fc0a5505d8fed5f98ea6269e43bb71c469f",
            "sha256:d4ef2f888df43dd3baec2c96168b14e2233558f2b445bdf33fa4a6817eb50ca5",
            new[] { "mutable-reciprocal-python-surface-adjacency" }, new[] { "Surface.boundary" }, new[] { "Surface.to_idf_object", "Window", "Door" }),
        Case("A17", "a17-get-subsurface-linear-scale-edge-domain",
            "sha256:c2e3a6894e6511afd6d277292054e7a9c07907ee33440f6134cb8030d24a1b07",
            "sha256:7dd9dcdf4a0ec5f996c73dfe9e4bd0fec5612a53030ac31b435c63d227aa0ed9",
            new[] { "legacy-linear-scale-subsurface-projection" }, new[] { "Surface.get_subsurface" }, new[] { "Surface.__init__" }),
        Case("A18", "a18-get-subsurface-oversized-error",
            "sha256:30afd4da99d6fea8f340cd33d342e3350786057ce7cfbe74d9f6a0cf4f36927a",
            "sha256:92986ed3d126f8bb177e6694849f68feb824b9e9ea014e55a4464208cf55e025",
            new[] { "legacy-linear-scale-subsurface-projection" }, new[] { "Surface.get_subsurface" }, new[] { "Surface.__init__" }),
    };

    private static readonly NativePin[] ExpectedNativePins =
    {
        new(5, "sha256:d52f775a43aa57d519069dac4aef91c63ae0fbdaa8828e2b17071b69bf1ef802"),
        new(7, "sha256:4ac15c7b8d339adf4b906b776522eb92e710863d80e2db0aebad4c112df55564"),
        new(6, "sha256:5b6d7555f05a3cc40e7837ebe8e705b2a9141d545d370372538338f80c033b08"),
        new(5, "sha256:fbe5a9f5fb8a3713436751ad345807f01cb2170d775237de5be00d3effccf429"),
        new(5, "sha256:5edda2e00275c80c7598a639e33b0d1a9e32a543c06d1de96d39d7f056f07fec"),
        new(6, "sha256:192dd5fa15f0ce82a97583fc817df7b4eb7395beada958c8269244f49e0fa7f8"),
        new(7, "sha256:62c0d1660d77b8a53d6d2c6657847ab590ec4cef7eb81305ffcc54c3ee233129"),
        new(5, "sha256:85bf0828402e4e898b976d0d3daa18fb5e1f6547c493df536ca4c78131c98e24"),
        new(7, "sha256:ef8772264c1da4a9ada0bea4557a5565a0cb05373972eb0e5d70d8ea9014aaed"),
        new(8, "sha256:54967df58fa02459691411e464ba060e860b60d9bd84e1450d0442db9af42c11"),
        new(7, "sha256:7b321bafcf0a945c4b3811241a99fbc073e33bb649ec169ef81781ad2ae2830d"),
        new(7, "sha256:a59cb6cfa983401ccebbe6add874cdc73048b29085276a9730de2c74a546fbfb"),
        new(7, "sha256:26864b21db4e256819ecd68bd4493f8b0cbdd48f430d91726c0d817035f946f4"),
        new(6, "sha256:1cfa7e7e6196db5db775ef158391ab18d5c0f1b4b51c290b1ff1653de39eb92b"),
        new(6, "sha256:b73d1f643c95b2cdead760d228d4d8bc08d201ec7e640cbc8deb412e1464035b"),
        new(10, "sha256:bfcd5537448072544d0a6ba04619963a50e0826754d4e29611c081bcec2d4110"),
        new(13, "sha256:afae1436752290c7cef315a98d03854dc6ff16ef7999999b6e8fc976f7411cf4"),
        new(8, "sha256:730a4d632408ca29d1bfa5204554ead7d4ab41bb51133400832a07571f9565ef"),
    };

    private static readonly TargetBinding[] Targets = CreateTargets();

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:91a3e4fd999e1e1123daf461b7af133716d1a5c86fa027e7d4460e172ae4c667",
        "sha256:ab0354e29293adce96b9106de5b795be0cd01f7d3fec9dfff1d26fd6dd7a03a0",
        "sha256:8907b49adfad94d4ef1d426469b35f4199710c9f2364f1381b3280ee6fcc6c0b",
        "sha256:5e0e7a26e2d065ff213c2544cd1735aee2e5c8e14f26af3d3d40cd89706d90e9",
        "sha256:6415125a5c9362b8b2fb519652d51dac54495eb17ac13a8efc1d0c01465fd76b",
        "sha256:acf1a5b94a183cefca351af0257cd83d19743bd2e5fe4e90e580ed4039d16b6f",
        "sha256:0a701f25bf91d130c669beb329c048fd57504b3d398738383e26b1532ad4ce35",
        "sha256:8343dace323f0af0efda3cdbd541b09a1524b6bda415786c33bc7cce61ae4050",
        "sha256:953543c89f85d80e15a183043a683ccaa05f69c6c3bb3c58c4b70080fcbebfd6",
        "sha256:7dcd4c85d04003ad27bc97fabaa3852ee52699065a01395c18cb773d160c53f2",
        "sha256:d5f392b056defc290a57cb77da10475f75c66f9d16cd42737589f4d5025286c3",
        "sha256:1e7e05e399eb8f8cde731e645b31188f3607c992d538dc76eccca80c071a61df",
        "sha256:a39a9c28955850d803f240ec0e29fa114f803bcccaa4c01dbed9b890596989ec",
        "sha256:5ba61edf5694d7a6d1a57163728d07e2a39deb3e71b8df671a4ff7d7ca28c9a4",
        "sha256:cf1b32b5afd459cee99a80cae9761b8ddc499e0396253d17fdb2466a59c34e01",
        "sha256:f8daafdf176b247959630b7760e811a1ae9f449e3f1f0461fe54d8a520e9183b",
        "sha256:2287965fdbc2c9926419d913473a16022fb029fb5b1059ff6974763d3172271d",
        "sha256:e33f2b187df71f8a4e660c096e8be2efe8209d79abb190116d522fcdf5ff5a21",
        "sha256:441af14783469fbafe8f54bc01b439b6f57fee16d8993085a94431bbf3190bfd",
    };

    [Fact]
    public void MatchesPinnedOpeningAdjacencyCoreThroughBoundedNativeRoutes()
    {
        ValidatePinnedArtifactsAndNativeApi();
        using JsonDocument oracle = ReadPinnedOracle();
        JsonElement[] fixtureCases = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(18, observations.Length);

        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(Cases[index].Scenario, observations[index].Scenario);
        }

        object[] receipts = Targets
            .Select(target => CreateReceipt(target, observations))
            .ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "OPENING_ADJACENCY_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    cases = observations.Select(item => new
                    {
                        item.Scenario,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipts = Targets.Select((item, index) => new
                    {
                        item.Symbol,
                        item.AssertionId,
                        receipt_sha256 = receiptHashes[index],
                    }),
                }, DiscoveryJsonOptions));
        }

        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }

        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
        for (int index = 0; index < Targets.Length; index++)
        {
            JsonElement receipt = JsonSerializer.SerializeToElement(receipts[index]);
            ValidateReceipt(receipt, Targets[index], observations);
            TrustedEvidenceRecorder.Record(
                Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
        }

        Assert.Equal(19, Targets.Length);
        Assert.Equal(19, Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(10, Targets.Select(item => item.AdaptationId).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(Targets, item => item.Symbol == "Surface.to_idf_object");
        Assert.Single(fixtureCases, item => RequiredString(item.GetProperty("python").GetProperty("facts"), "scenario") == "A16");
    }

    private static CaseBinding Case(
        string scenario,
        string suffix,
        string caseSha256,
        string factsSha256,
        string[] adaptations,
        string[] targets,
        string[]? context = null) => new(
            scenario,
            "dragon-shape-opening-adjacency-core." + suffix,
            caseSha256,
            factsSha256,
            adaptations,
            targets,
            context ?? Array.Empty<string>());

    private static TargetBinding[] CreateTargets() => new[]
    {
        Target("Blind", 1025, "class",
            "sha256:75f7c91c526ca8c2a86f7a984fa2007d17e94a8a3e38a6a80ffa6a7af37cd36b",
            "sha256:fc7a9c184e4c3d27ade9f49aa28e0fac174fc62924cb16b31214be1c5040a0ce",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "dragon-shape-opening-adjacency-core-1025-75f7c91c", "permissive-python-blind-state",
            "Dragons.InvisibleDragon.Shape.Blind constructor", new[] { 0 }),
        Target("Blind.__init__", 1026, "function",
            "sha256:574e9b5ab31178c6d64eaeb70e19e3a434448c712cf2d8459bfdc36704047eee",
            "sha256:d42cf37a1ce3ef68b7b965525da19d840fad8f959e20cedce16272a4c2062f32",
            "sha256:c7af4f5037c03da48ea55ce1b17434d0adee92079ed159f3662f8f3529807067",
            "dragon-shape-opening-adjacency-core-1026-574e9b5a", "permissive-python-blind-state",
            "Dragons.InvisibleDragon.Shape.Blind constructor", new[] { 0, 1 }),
        Target("Door", 1028, "class",
            "sha256:717d717ab0c24c7d2900081f9853e5b1670c8f37731d3076410b3401718e59b9",
            "sha256:0e2346da9e26019c14e49847521a42359715dcbe64fa76f594be06344837ac38",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "dragon-shape-opening-adjacency-core-1028-717d717a", "permissive-python-door-state",
            "Dragons.InvisibleDragon.Shape.Door constructor", new[] { 7 }),
        Target("Door.__init__", 1029, "function",
            "sha256:efd71c8161c4540503d2a0539dd30c3bf05109fa353292091035dec6b848bbde",
            "sha256:1b879b85e5521d34e5f6d6b4b8b5de28d161b537609dcec99a6eb4443f9220c7",
            "sha256:64e9af88814e32ae336e082a145dc9bd7fcb7a35aabec066fb6441f9b6697d86",
            "dragon-shape-opening-adjacency-core-1029-efd71c81", "permissive-python-door-state",
            "Dragons.InvisibleDragon.Shape.Door constructor", new[] { 7, 8 }),
        Target("Shade", 1030, "class",
            "sha256:9404da043505f2d5bcd314f7a1ce2a994eaec9ba237a8d039f9c107bb97987a0",
            "sha256:0f80436ffb22f4436b5ba8ddc953c234450eaa30fb8d9a28a00302dc1dd524ba",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "dragon-shape-opening-adjacency-core-1030-9404da04", "permissive-python-shade-state",
            "Dragons.InvisibleDragon.Shape.Shade constructor", new[] { 2 }),
        Target("Shade.__init__", 1031, "function",
            "sha256:f76ed298cc435ea32d2c8b3631590e12fbb4b844e60af60e13aa517867b225b7",
            "sha256:0993a1b330563d2c24636c42d55e2049625588c9be7766db90230da994886dd5",
            "sha256:3dc5d4920337b46160c5da4cc1f2c4ac137e8c5fa1d58dd1ba3639fe9abe1ef0",
            "dragon-shape-opening-adjacency-core-1031-f76ed298", "permissive-python-shade-state",
            "Dragons.InvisibleDragon.Shape.Shade constructor", new[] { 2, 3 }),
        Target("Shading", 1033, "class",
            "sha256:4dba9833a4c24512afe7f0cc7566f8e89fa27a5c4b4d2be523a568dfa83d221c",
            "sha256:134552eef91182656eaed430922ad3ea45c073c187ddbc3c54d8f65ccb782416",
            "sha256:841d4cb6106fd1288f259549c1674303f32505b0270beb50c4048e496e48d5db",
            "dragon-shape-opening-adjacency-core-1033-4dba9833", "directly-instantiable-empty-python-shading",
            "Dragons.InvisibleDragon.Shape.IShadingDevice contract", new[] { 4 }),
        Target("Surface.__init__", 1035, "function",
            "sha256:ef349ef4b0a7bfcd1f47a297b0107d24018f5c4350b1765051948f2cfde5daa3",
            "sha256:91e81dfb11f60b18fb209a8ce5ab7b1c31ccf24e1fab04c3a4f79cd370173980",
            "sha256:4a5a7556a35cd8ddd65641f5ba6e98ba112631c7581f158c349fc7737e50c389",
            "dragon-shape-opening-adjacency-core-1035-ef349ef4", "aliased-python-surface-opening-inputs",
            "Dragons.InvisibleDragon.Shape.Surface constructor", new[] { 9, 10 }),
        Target("Surface.blinded_window", 1039, "function",
            "sha256:f520fbfe3104ddbfa8f056b4c28908706faac3b0b333f46b19ff4a7366d73234",
            "sha256:6ed2bf44ec68a9cda9c9305419f2564c10dcf9ffa3541254a942a64ef21bd2d4",
            "sha256:1d3cc4d0181730c8ef36c846de99dcf384cafdca1995d6e321529b42f2d5760c",
            "dragon-shape-opening-adjacency-core-1039-f520fbfe", "fresh-python-blinded-window-projection",
            "Surface.Windows filtered by Window.Shading", new[] { 11 }),
        Target("Surface.boundary", 1040, "function",
            "sha256:7753d96736d6410917d1eb131f747db5f1e5538aa51e5f00bcf68ee34c084316",
            "sha256:11060e585257ead0cc3dbce8f24b8dba7b63f4df1140d5665311b5fcf798980f",
            "sha256:f751320cef2e3413ed702ef8e23a43d9148130cd02678df8145fb094890b2276",
            "dragon-shape-opening-adjacency-core-1040-7753d967", "mutable-reciprocal-python-surface-adjacency",
            "SurfaceBoundary plus SurfaceAdjacency.Match", new[] { 12, 13, 14, 15 }),
        Target("Surface.get_subsurface", 1042, "function",
            "sha256:7e43708dfc08dc4b915a0fbb6ea3ebb1ee7b943031a60d12336e4fe3ed33e91f",
            "sha256:b4b38a26eb25cd420fa750f6e3df05aff1f17a04019274a0866a9919b192a8b6",
            "sha256:c0dff706444e067d08d2c480969520b1927bd7085f67452388642139565f6547",
            "dragon-shape-opening-adjacency-core-1042-7e43708d", "legacy-linear-scale-subsurface-projection",
            "Surface.CreateCenteredSubsurface", new[] { 16, 17 }),
        Target("SurfaceBoundaryCondition", 1048, "class",
            "sha256:73a8b86f663a2874b87c5c6f8ba801e5515095918422a1854e1acf157bb72fa7",
            "sha256:a19cb257b67cfe826191f490a77f5e4d2ec67dd04d22e67840b4e0db65a8976d",
            "sha256:fa63e0c63f78931ad2499d6cbdce49736062ce69e49ba7e475be611ff93799c4",
            "dragon-shape-opening-adjacency-core-1048-73a8b86f", "lowercase-python-surface-boundary-enum",
            "Dragons.InvisibleDragon.Shape.SurfaceBoundaryCondition", new[] { 12 }),
        Target("SurfaceBoundaryCondition.ADIABATIC", 1049, "constant",
            "sha256:1d0e3d46c8e9ae9dec15e60e913ee94e01a3261bbae746ebfe9f71913eb08051",
            "sha256:a77afcfa981dffc115a2d5b307c32e5a87017d0bb905ea40499433a79dc8988e",
            "sha256:6e122ac194244051572f5d6fad4d0d208a8ef86998cf763329afe6b5882d935a",
            "dragon-shape-opening-adjacency-core-1049-1d0e3d46", "lowercase-python-surface-boundary-enum",
            "SurfaceBoundaryCondition.Adiabatic", new[] { 12 }),
        Target("SurfaceBoundaryCondition.GROUND", 1050, "constant",
            "sha256:0992cbf625fbf401fbc1229e59696a8fa65bc36efc11177322a8b181c329e410",
            "sha256:1a16c10fc43be40d81c04f68b02c92dfeadd1fce921e156f9998399f9874df74",
            "sha256:c7f32d1a16829421283e84020abdc7359b68f59cdbc7982fbc3bd54131019c0f",
            "dragon-shape-opening-adjacency-core-1050-0992cbf6", "lowercase-python-surface-boundary-enum",
            "SurfaceBoundaryCondition.Ground", new[] { 12 }),
        Target("SurfaceBoundaryCondition.OUTDOOR", 1051, "constant",
            "sha256:8560160a8415533fb8b2572a963112b6fef686482ffaacd99c461ea99fa30306",
            "sha256:f1fb2d320126039c88d7c8b391550959a7479d2212123217df022690c957fb3a",
            "sha256:e77842f79eabf8bd08cd21c0af1d558de32c12c118304601a01f5e4d5c2b3dd9",
            "dragon-shape-opening-adjacency-core-1051-8560160a", "lowercase-python-surface-boundary-enum",
            "SurfaceBoundaryCondition.Outdoors", new[] { 12 }),
        Target("SurfaceBoundaryCondition.ZONE", 1052, "constant",
            "sha256:3ec06789fa4f783e94be2d46f5c31e90fdad2fac6641ea8097a304beba8e613e",
            "sha256:6e2c14954d19501e9e789403c235fb2d61160415b444b44c346814335989a15d",
            "sha256:5ff79e3fee75f5cebcfa0af7c998358641f1579818f3cf36df0663a984c3f44f",
            "dragon-shape-opening-adjacency-core-1052-3ec06789", "lowercase-python-surface-boundary-enum",
            "SurfaceBoundaryCondition.Zone", new[] { 12 }),
        Target("SurfaceBoundaryCondition.__str__", 1053, "function",
            "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e",
            "sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab",
            "sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8",
            "dragon-shape-opening-adjacency-core-1053-f40e4929", "lowercase-python-surface-boundary-enum",
            "EnergyModelIdfAssembler boundary mapping", new[] { 12 }),
        Target("Window", 1081, "class",
            "sha256:af640a9abfcfaae14201dbe8195aba06780027412da5ac3ffaf480d7bfe45b3b",
            "sha256:51e36b1ede4e2ba8870f6b2ab855c3d628e8e9fbb02fef5efabd828d925c9e70",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "dragon-shape-opening-adjacency-core-1081-af640a9a", "permissive-python-window-state",
            "Dragons.InvisibleDragon.Shape.Window constructor", new[] { 5 }),
        Target("Window.__init__", 1082, "function",
            "sha256:3ce851bd512903617cce711c5883a4968e1e0ab7e275c2bb10d0b046532e7380",
            "sha256:f69f7e176b5b3338f40002a66cdc91c8eaec356648e739aa66f40a2ad3c02c7b",
            "sha256:1ec931f0f7720883c9c44f4a2c10e240602039e80d5f2179a50cf0cb07212641",
            "dragon-shape-opening-adjacency-core-1082-3ce851bd", "permissive-python-window-state",
            "Dragons.InvisibleDragon.Shape.Window constructor", new[] { 5, 6 }),
    };

    private static TargetBinding Target(
        string symbol,
        int inventoryIndex,
        string kind,
        string symbolHash,
        string signatureHash,
        string bodyHash,
        string assertionId,
        string adaptationId,
        string nativeTarget,
        int[] caseIndices) => new(
            symbol,
            inventoryIndex,
            kind,
            symbolHash,
            signatureHash,
            bodyHash,
            assertionId,
            adaptationId,
            nativeTarget,
            caseIndices);

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(FixturePath));
        Assert.Equal(FixtureBytes, bytes.Length);
        Assert.Equal(FixtureSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain("\r\n", new UTF8Encoding(false, true).GetString(bytes), StringComparison.Ordinal);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
    }

    private static JsonElement[] ValidateOracle(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(root, "case_sha256", "cases", "cases_sha256", "consumer_contract", "fact_sha256", "runtime", "schema", "symbols", "target_receipts", "upstream");
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        JsonElement casesElement = root.GetProperty("cases");
        Assert.Equal(CasesSha256, CanonicalSha256(casesElement));
        JsonElement[] cases = casesElement.EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, cases.Length);
        Assert.Equal(Cases.Select(item => item.CaseId), cases.Select(item => RequiredString(item, "id")));

        JsonElement factHashes = root.GetProperty("fact_sha256");
        JsonElement caseHashes = root.GetProperty("case_sha256");
        for (int index = 0; index < cases.Length; index++)
        {
            JsonElement item = cases[index];
            CaseBinding binding = Cases[index];
            AssertKeys(item, "context_symbols", "executor", "expected_dotnet", "id", "python", "target_symbols");
            Assert.Equal("shape-opening-adjacency-core", RequiredString(item, "executor"));
            AssertStringArray(item.GetProperty("target_symbols"), binding.TargetSymbols);
            AssertStringArray(item.GetProperty("context_symbols"), binding.ContextSymbols);
            JsonElement expected = item.GetProperty("expected_dotnet");
            Assert.Equal("exception", RequiredString(expected, "classification"));
            Assert.Equal("adapted-or-rejected-as-pinned", RequiredString(expected, "outcome"));
            AssertStringArray(expected.GetProperty("adaptations"), binding.Adaptations);
            JsonElement python = item.GetProperty("python");
            Assert.Equal("observed", RequiredString(python, "outcome"));
            JsonElement facts = python.GetProperty("facts");
            Assert.Equal(binding.Scenario, RequiredString(facts, "scenario"));
            Assert.Equal(binding.FactsSha256, RequiredString(python, "facts_sha256"));
            Assert.Equal(binding.FactsSha256, RequiredString(factHashes, binding.CaseId));
            Assert.Equal(binding.FactsSha256, CanonicalSha256(facts));
            Assert.Equal(binding.CaseSha256, RequiredString(caseHashes, binding.CaseId));
            Assert.Equal(binding.CaseSha256, CanonicalSha256(item));
        }

        ValidateTargetReceipts(root);
        ValidateContract(root.GetProperty("consumer_contract"), cases);
        ValidateRuntimeAndUpstream(root);
        return cases;
    }

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
            JsonElement descriptor = descriptors[index];
            JsonElement receipt = receipts[index];
            AssertReceiptFields(descriptor, target, includeIndex: false);
            AssertReceiptFields(receipt, target, includeIndex: true);
            JsonElement inventorySymbol = inventorySymbols[target.InventoryIndex];
            Assert.Equal(target.Symbol, RequiredString(inventorySymbol, "symbol"));
            Assert.Equal(target.Kind, RequiredString(inventorySymbol, "kind"));
            Assert.Equal(target.SymbolHash, RequiredString(inventorySymbol, "symbol_hash"));
            Assert.Equal(target.SignatureHash, RequiredString(inventorySymbol, "signature_hash"));
            Assert.Equal(target.BodyHash, RequiredString(inventorySymbol, "body_hash"));
            Assert.Equal(UpstreamPath, RequiredString(inventorySymbol, "path"));
        }
    }

    private static void AssertReceiptFields(JsonElement value, TargetBinding target, bool includeIndex)
    {
        Assert.Equal(target.Symbol, RequiredString(value, "symbol"));
        Assert.Equal(target.Kind, RequiredString(value, "kind"));
        Assert.Equal(target.SymbolHash, RequiredString(value, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(value, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(value, "body_hash"));
        Assert.Equal(UpstreamPath, RequiredString(value, "path"));
        if (includeIndex)
        {
            Assert.Equal(target.InventoryIndex, value.GetProperty("inventory_index").GetInt32());
        }
    }

    private static void ValidateContract(JsonElement contract, IReadOnlyList<JsonElement> cases)
    {
        Assert.Equal(18, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        AssertStringArray(contract.GetProperty("target_symbols"), Targets.Select(item => item.Symbol));
        Assert.Equal(19, contract.GetProperty("target_receipts").GetArrayLength());
        for (int index = 0; index < Targets.Length; index++)
        {
            TargetBinding target = Targets[index];
            Assert.Equal(target.AdaptationId, RequiredString(contract.GetProperty("adaptations"), target.Symbol));
            Assert.Equal(target.AssertionId, RequiredString(contract.GetProperty("assertion_ids"), target.Symbol));
            Assert.Equal("exception", RequiredString(contract.GetProperty("classifications"), target.Symbol));
            Assert.Equal(target.NativeTarget, RequiredString(contract.GetProperty("native_targets"), target.Symbol));
            string[] actualCoverage = cases
                .Where(item => item.GetProperty("target_symbols").EnumerateArray().Any(symbol => symbol.GetString() == target.Symbol))
                .Select(item => RequiredString(item, "id"))
                .ToArray();
            Assert.Equal(target.CaseIndices.Select(caseIndex => Cases[caseIndex].CaseId), actualCoverage);
        }

        JsonElement closure = contract.GetProperty("closure");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.True(closure.GetProperty("target_coverage_complete").GetBoolean());
        Assert.True(closure.GetProperty("parent_emission_is_context_only").GetBoolean());
        AssertStringArray(closure.GetProperty("parent_emission_context_case_ids"), new[] { Cases[15].CaseId });
        AssertStringArray(closure.GetProperty("unresolved_target_behavior"), new[]
        {
            "Surface.get_subsurface-nan-positive-infinity-and-negative-infinity-inputs",
            "Surface.get_subsurface-nonnumeric-inputs-and-arithmetic-error-timing",
        });
        Assert.Contains("Surface.to_idf_object", closure.GetProperty("context_only_not_targeted").EnumerateArray().Select(item => item.GetString()!));
        Assert.DoesNotContain("Surface.to_idf_object", Targets.Select(item => item.Symbol));
        Assert.Contains("positional zip truncation", RequiredString(contract, "classification_basis"));
    }

    private static void ValidateRuntimeAndUpstream(JsonElement root)
    {
        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        JsonElement shape = upstream.GetProperty("shape_source");
        Assert.Equal(UpstreamPath, RequiredString(shape, "path"));
        Assert.Equal(UpstreamBytes, shape.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(shape, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(shape, "ast_sha256"));
        Assert.Equal(12, upstream.GetProperty("sources").GetArrayLength());
        Assert.Equal(12, upstream.GetProperty("loaded_local_modules").GetArrayLength());
    }

    private static void ValidatePinnedArtifactsAndNativeApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        AssertConstructor(typeof(Blind), "name", "slatWidthMetres", "slatSeparationMetres", "slatAngleDegrees", "frontReflectance", "backReflectance");
        AssertConstructor(typeof(Shade), "name", "transmittance", "reflectance");
        AssertConstructor(typeof(Window), "id", "name", "glazing", "polygon", "shading", "provenance");
        AssertConstructor(typeof(Door), "id", "name", "construction", "polygon", "provenance");
        AssertConstructor(typeof(Surface), "id", "name", "type", "construction", "boundary", "polygon", "openings", "provenance");
        Assert.True(typeof(IShadingDevice).IsInterface);
        Assert.Equal(typeof(IReadOnlyList<Window>), typeof(Surface).GetProperty(nameof(Surface.Windows))!.PropertyType);
        Assert.Equal(typeof(PlanarPolygon), typeof(Surface).GetMethod(nameof(Surface.CreateCenteredSubsurface))!.ReturnType);
        Assert.NotNull(typeof(SurfaceAdjacency).GetMethod(nameof(SurfaceAdjacency.Match), BindingFlags.Public | BindingFlags.Static));
    }

    private static void AssertConstructor(Type type, params string[] parameterNames)
    {
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(parameterNames, constructor.GetParameters().Select(item => item.Name));
    }

    private static NativeObservation ObserveNativeCase(int index) => index switch
    {
        0 => ObserveA01(),
        1 => ObserveA02(),
        2 => ObserveA03(),
        3 => ObserveA04(),
        4 => ObserveA05(),
        5 => ObserveA06(),
        6 => ObserveA07(),
        7 => ObserveA08(),
        8 => ObserveA09(),
        9 => ObserveA10(),
        10 => ObserveA11(),
        11 => ObserveA12(),
        12 => ObserveA13(),
        13 => ObserveA14(),
        14 => ObserveA15(),
        15 => ObserveA16(),
        16 => ObserveA17(),
        17 => ObserveA18(),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    private static NativeObservation ObserveA01()
    {
        Blind first = new("Representative Blind", 0.025, 0.02, 45, 0.6, 0.4);
        Blind second = new("Representative Blind", 0.025, 0.02, 45, 0.6, 0.4);
        IShadingDevice contract = first;
        Assert.NotSame(first, second);
        Assert.Equal(BlindState(first), BlindState(second));
        Assert.Same(first, contract);
        return Observation("A01",
            "native-route=Blind.constructor",
            "first=" + BlindState(first),
            "second=" + BlindState(second),
            "instance-identity=fresh",
            "interface-contract=IShadingDevice");
    }

    private static NativeObservation ObserveA02()
    {
        ArgumentOutOfRangeException width = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Blind("Unchecked Blind", -1, 0.02, 45, 0.6, 0.4));
        ArgumentOutOfRangeException separation = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Blind("Unchecked Blind", 0.025, 0, 45, 0.6, 0.4));
        ArgumentOutOfRangeException angle = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Blind("Unchecked Blind", 0.025, 0.02, 999, 0.6, 0.4));
        ArgumentOutOfRangeException front = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Blind("Unchecked Blind", 0.025, 0.02, 45, -0.2, 0.4));
        ArgumentOutOfRangeException back = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Blind("Unchecked Blind", 0.025, 0.02, 45, 0.6, 1.5));
        Assert.Equal("slatWidthMetres", width.ParamName);
        Assert.Equal("slatSeparationMetres", separation.ParamName);
        Assert.Equal("slatAngleDegrees", angle.ParamName);
        Assert.Equal("frontReflectance", front.ParamName);
        Assert.Equal("backReflectance", back.ParamName);
        Assert.All(typeof(Blind).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.False(property.CanWrite));
        return Observation("A02",
            "python-boundary=unchecked-invalid-and-mutable-state-not-reproduced",
            ExceptionFact("negative-width", width),
            ExceptionFact("zero-separation", separation),
            ExceptionFact("angle-999", angle),
            ExceptionFact("front-minus-0.2", front),
            ExceptionFact("back-1.5", back),
            "native-state=constructor-validated-get-only-properties");
    }

    private static NativeObservation ObserveA03()
    {
        Shade first = new("Representative Shade", 0.3, 0.2);
        Shade second = new("Representative Shade", 0.3, 0.2);
        IShadingDevice contract = first;
        Assert.NotSame(first, second);
        Assert.Equal(ShadeState(first), ShadeState(second));
        Assert.Equal(0.5, first.Emissivity, 12);
        Assert.Same(first, contract);
        return Observation("A03",
            "native-route=Shade.constructor",
            "first=" + ShadeState(first),
            "second=" + ShadeState(second),
            "emissivity=" + Double(first.Emissivity),
            "instance-identity=fresh",
            "interface-contract=IShadingDevice");
    }

    private static NativeObservation ObserveA04()
    {
        ArgumentException sum = Assert.Throws<ArgumentException>(
            () => new Shade("Unchecked Shade", 0.8, 0.7));
        ArgumentOutOfRangeException transmittance = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Shade("Unchecked Shade", -0.1, 0.2));
        ArgumentOutOfRangeException reflectance = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Shade("Unchecked Shade", 0.2, 1.1));
        Assert.Null(sum.ParamName);
        Assert.Equal("transmittance", transmittance.ParamName);
        Assert.Equal("reflectance", reflectance.ParamName);
        Assert.All(typeof(Shade).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.False(property.CanWrite));
        return Observation("A04",
            "python-boundary=excessive-optical-sum-and-mutation-not-reproduced",
            ExceptionFact("optical-sum-1.5", sum),
            ExceptionFact("negative-transmittance", transmittance),
            ExceptionFact("reflectance-1.1", reflectance),
            "native-state=constructor-validated-get-only-properties");
    }

    private static NativeObservation ObserveA05()
    {
        Type contract = typeof(IShadingDevice);
        Assert.True(contract.IsInterface);
        Assert.False(contract.IsClass);
        Assert.Equal(new[] { "Name" }, contract.GetProperties().Select(item => item.Name));
        MissingMethodException exception = Assert.Throws<MissingMethodException>(() => Activator.CreateInstance(contract));
        return Observation("A05",
            "python-boundary=directly-instantiable-empty-Shading-not-reproduced",
            "native-route=IShadingDevice.interface",
            "native-contract-members=Name",
            "direct-instantiation=" + exception.GetType().Name,
            "concrete-implementations=Blind|Shade");
    }

    private static NativeObservation ObserveA06()
    {
        var glazing = new Glazing("Window Variants Glazing", 1.4, 0.4);
        var blind = new Blind("Window Variant Blind", 0.02, 0.018, 35, 0.55, 0.45);
        var shade = new Shade("Window Variant Shade", 0.25, 0.35);
        Window[] windows =
        {
            new(Entity("A06-W1"), "Unshaded Window", glazing, RectangleWithArea(2)),
            new(Entity("A06-W2"), "Blind Window", glazing, RectangleWithArea(1.5), blind),
            new(Entity("A06-W3"), "Shade Window", glazing, RectangleWithArea(1), shade),
        };
        Assert.All(windows, item => Assert.Same(glazing, item.Glazing));
        Assert.Null(windows[0].Shading);
        Assert.Same(blind, windows[1].Shading);
        Assert.Same(shade, windows[2].Shading);
        Assert.Equal(2d, windows[0].Area, 12);
        Assert.Equal(1.5d, windows[1].Area, 12);
        Assert.Equal(1d, windows[2].Area, 12);
        return Observation("A06",
            "native-route=Window.constructor",
            "names=" + Join(windows.Select(item => item.Name)),
            "areas=" + Join(windows.Select(item => Double(item.Area))),
            "shading=none|Blind|Shade",
            "glazing-reference=shared-exact",
            "shading-references=exact");
    }

    private static NativeObservation ObserveA07()
    {
        ConstructorInfo constructor = Assert.Single(typeof(Window).GetConstructors());
        Assert.Equal(typeof(PlanarPolygon), constructor.GetParameters()[3].ParameterType);
        var glazing = new Glazing("Typed Glazing", 1.4, 0.4);
        PlanarPolygon polygon = RectangleWithArea(1);
        ArgumentNullException id = Assert.Throws<ArgumentNullException>(
            () => new Window(null!, "Invalid Window", glazing, polygon));
        ArgumentNullException glazingError = Assert.Throws<ArgumentNullException>(
            () => new Window(Entity("A07-W"), "Invalid Window", null!, polygon));
        ArgumentNullException polygonError = Assert.Throws<ArgumentNullException>(
            () => new Window(Entity("A07-W"), "Invalid Window", glazing, null!));
        Assert.Equal("id", id.ParamName);
        Assert.Equal("glazing", glazingError.ParamName);
        Assert.Equal("polygon", polygonError.ParamName);
        Assert.All(typeof(Window).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.False(property.CanWrite));
        return Observation("A07",
            "python-boundary=negative/nonfinite-scalar-area-foreign-reference-mutation-not-reproduced",
            "native-signature=EntityId|string|Glazing|PlanarPolygon|IShadingDevice?|GeometryProvenance?",
            "native-area=derived-from-validated-polygon",
            ExceptionFact("null-id", id),
            ExceptionFact("null-glazing", glazingError),
            ExceptionFact("null-polygon", polygonError),
            "native-state=get-only-typed-references");
    }

    private static NativeObservation ObserveA08()
    {
        var construction = new NoMassConstruction("Representative Door Assembly", 2.1);
        PlanarPolygon polygon = RectangleWithArea(2.1);
        Door first = new(Entity("A08-D1"), "Representative Door", construction, polygon);
        Door second = new(Entity("A08-D2"), "Representative Door", construction, polygon);
        Assert.NotSame(first, second);
        Assert.Same(construction, first.Construction);
        Assert.Same(construction, second.Construction);
        Assert.Equal(2.1, first.Area, 12);
        return Observation("A08",
            "native-route=Door.constructor",
            "first=" + DoorState(first),
            "second=" + DoorState(second),
            "construction-reference=shared-exact",
            "instance-identity=fresh");
    }

    private static NativeObservation ObserveA09()
    {
        ConstructorInfo constructor = Assert.Single(typeof(Door).GetConstructors());
        Assert.Equal(typeof(PlanarPolygon), constructor.GetParameters()[3].ParameterType);
        var construction = new NoMassConstruction("Typed Door Assembly", 2.1);
        PlanarPolygon polygon = RectangleWithArea(1);
        ArgumentNullException id = Assert.Throws<ArgumentNullException>(
            () => new Door(null!, "Invalid Door", construction, polygon));
        ArgumentNullException constructionError = Assert.Throws<ArgumentNullException>(
            () => new Door(Entity("A09-D"), "Invalid Door", null!, polygon));
        ArgumentNullException polygonError = Assert.Throws<ArgumentNullException>(
            () => new Door(Entity("A09-D"), "Invalid Door", construction, null!));
        Assert.Equal("id", id.ParamName);
        Assert.Equal("construction", constructionError.ParamName);
        Assert.Equal("polygon", polygonError.ParamName);
        Assert.All(typeof(Door).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.False(property.CanWrite));
        return Observation("A09",
            "python-boundary=negative/nonfinite-scalar-area-foreign-reference-mutation-not-reproduced",
            "native-signature=EntityId|string|ISurfaceConstruction|PlanarPolygon|GeometryProvenance?",
            "native-area=derived-from-validated-polygon",
            ExceptionFact("null-id", id),
            ExceptionFact("null-construction", constructionError),
            ExceptionFact("null-polygon", polygonError),
            "native-state=get-only-typed-references");
    }

    private static NativeObservation ObserveA10()
    {
        Surface first = BasicSurface("A10-S1", "First Default Surface");
        Surface second = BasicSurface("A10-S2", "Second Default Surface");
        Assert.Empty(first.Openings);
        Assert.Empty(second.Openings);
        Assert.NotSame(first.Openings, second.Openings);
        var addition = new Window(Entity("A10-W"), "Default Mutation Probe", Glazing(), RectangleWithArea(1));
        IList firstList = Assert.IsAssignableFrom<IList>(first.Openings);
        IList secondList = Assert.IsAssignableFrom<IList>(second.Openings);
        Assert.True(firstList.IsReadOnly);
        Assert.True(secondList.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => firstList.Add(addition));
        Assert.Empty(first.Openings);
        Assert.Empty(second.Openings);
        return Observation("A10",
            "python-boundary=shared-mutable-default-window/door-lists-not-reproduced",
            "native-route=Surface.constructor",
            "first-opening-count=0",
            "second-opening-count=0",
            "opening-collection-identity=distinct",
            "opening-collections=read-only",
            "mutation-attempt=NotSupportedException",
            "captured-state=both-opening-counts-remain-zero");
    }

    private static NativeObservation ObserveA11()
    {
        Window w1 = new(Entity("A11-W1"), "Explicit Window 1", Glazing(), RectangleWithArea(1));
        Window w2 = new(Entity("A11-W2"), "Explicit Window 2", Glazing(), RectangleWithArea(1));
        Door d1 = new(Entity("A11-D1"), "Explicit Door 1", DoorConstruction(), RectangleWithArea(1));
        Door d2 = new(Entity("A11-D2"), "Explicit Door 2", DoorConstruction(), RectangleWithArea(1));
        var authored = new List<IOpening> { w1, d1, w2, d2 };
        Surface surface = BasicSurface("A11-S", "Explicit Mixed Surface", authored);
        Assert.NotSame(authored, surface.Openings);
        Assert.Equal(new[] { "Explicit Window 1", "Explicit Door 1", "Explicit Window 2", "Explicit Door 2" }, surface.Openings.Select(item => item.Name));
        authored.Add(new Window(Entity("A11-W3"), "Explicit Window 3", Glazing(), RectangleWithArea(1)));
        authored.Add(new Door(Entity("A11-D3"), "Explicit Door 3", DoorConstruction(), RectangleWithArea(1)));
        Assert.Equal(6, authored.Count);
        Assert.Equal(4, surface.Openings.Count);
        IList surfaceList = Assert.IsAssignableFrom<IList>(surface.Openings);
        Assert.Throws<NotSupportedException>(() => surfaceList.RemoveAt(0));
        return Observation("A11",
            "python-boundary=separate-window/door-list-aliasing-not-reproduced",
            "native-route=Surface.constructor-defensive-copy",
            "authored-initial-order=Explicit Window 1|Explicit Door 1|Explicit Window 2|Explicit Door 2",
            "surface-order-after-source-mutation=Explicit Window 1|Explicit Door 1|Explicit Window 2|Explicit Door 2",
            "source-count-after-mutation=6",
            "surface-count-after-mutation=4",
            "surface-mutation-attempt=NotSupportedException");
    }

    private static NativeObservation ObserveA12()
    {
        var blind = new Blind("Projection Blind", 0.02, 0.018, 35, 0.55, 0.45);
        var shade = new Shade("Projection Shade", 0.25, 0.35);
        IOpening[] openings =
        {
            new Window(Entity("A12-W1"), "Plain 1", Glazing(), RectangleWithArea(1)),
            new Window(Entity("A12-W2"), "Blind 1", Glazing(), RectangleWithArea(1), blind),
            new Window(Entity("A12-W3"), "Shade", Glazing(), RectangleWithArea(1), shade),
            new Window(Entity("A12-W4"), "Blind 2", Glazing(), RectangleWithArea(1), blind),
            new Window(Entity("A12-W5"), "Plain 2", Glazing(), RectangleWithArea(1)),
        };
        Surface surface = BasicSurface("A12-S", "Projection Surface", openings);
        Assert.Equal(openings.Length, surface.Openings.Count);
        for (int index = 0; index < openings.Length; index++)
        {
            Assert.Same(openings[index], surface.Openings[index]);
        }

        IReadOnlyList<Window> firstWindows = surface.Windows;
        IReadOnlyList<Window> secondWindows = surface.Windows;
        Assert.NotSame(firstWindows, secondWindows);
        var firstProjection = firstWindows.Where(item => item.Shading is not null).ToList();
        string[] before = firstProjection.Select(item => item.Name).ToArray();
        firstProjection.RemoveAt(0);
        var secondProjection = secondWindows.Where(item => item.Shading is not null).ToList();
        Assert.Equal(new[] { "Blind 1", "Shade", "Blind 2" }, before);
        Assert.Equal(new[] { "Blind 1", "Shade", "Blind 2" }, secondProjection.Select(item => item.Name));
        Assert.Equal(new[] { "Plain 1", "Blind 1", "Shade", "Blind 2", "Plain 2" }, surface.Windows.Select(item => item.Name));
        Assert.All(secondProjection, item => Assert.Contains(item, surface.Openings));
        for (int index = 0; index < openings.Length; index++)
        {
            Assert.Same(openings[index], surface.Openings[index]);
        }

        return Observation("A12",
            "native-route=Surface.Windows-then-filter-Window.Shading",
            "native-adaptation=explicit-filter-projection",
            "first-projection-before-local-mutation=Blind 1|Shade|Blind 2",
            "first-projection-after-local-mutation=Shade|Blind 2",
            "second-projection=Blind 1|Shade|Blind 2",
            "source-window-order=Plain 1|Blind 1|Shade|Blind 2|Plain 2",
            "captured-state=source-opening-references-and-order-unchanged");
    }

    private static NativeObservation ObserveA13()
    {
        SurfaceBoundaryCondition[] values = Enum.GetValues<SurfaceBoundaryCondition>();
        Assert.Equal(
            new[]
            {
                SurfaceBoundaryCondition.Outdoors,
                SurfaceBoundaryCondition.Ground,
                SurfaceBoundaryCondition.Adiabatic,
                SurfaceBoundaryCondition.Zone,
            },
            values);
        Assert.Equal(new[] { "Outdoors", "Ground", "Adiabatic", "Zone" }, values.Select(item => item.ToString()));
        Assert.Same(SurfaceBoundary.Outdoors, SurfaceBoundary.Outdoors);
        Assert.Same(SurfaceBoundary.Ground, SurfaceBoundary.Ground);
        Assert.Same(SurfaceBoundary.Adiabatic, SurfaceBoundary.Adiabatic);
        ArgumentException unlinked = Assert.Throws<ArgumentException>(
            () => new SurfaceBoundary(SurfaceBoundaryCondition.Zone));
        ArgumentOutOfRangeException invalid = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SurfaceBoundary((SurfaceBoundaryCondition)999));
        Assert.Equal("adjacentSurfaceId", unlinked.ParamName);
        Assert.Equal("condition", invalid.ParamName);

        Surface outdoors = BasicSurface("A13-OUT", "A13 Outdoors", boundary: SurfaceBoundary.Outdoors);
        Surface ground = BasicSurface("A13-GROUND", "A13 Ground", boundary: SurfaceBoundary.Ground);
        Surface adiabatic = BasicSurface("A13-ADIABATIC", "A13 Adiabatic", boundary: SurfaceBoundary.Adiabatic);
        Surface pairFirst = BasicSurface("A13-ZONE-A", "A13 Zone A");
        Surface pairSecond = BasicSurface("A13-ZONE-B", "A13 Zone B", polygon: pairFirst.Polygon.Reverse());
        SurfaceAdjacencyPair pair = SurfaceAdjacency.Match(pairFirst, pairSecond);
        var model = new EnergyModel("A13 boundary mapping", new[]
        {
            ZoneFor("A13-Z1", outdoors),
            ZoneFor("A13-Z2", ground),
            ZoneFor("A13-Z3", adiabatic),
            ZoneFor("A13-Z4", pair.First),
            ZoneFor("A13-Z5", pair.Second),
        });
        IdfDocument document = model.ToIdfDocument(options: LegacyOptions());
        string[] mapped = new[] { outdoors.Name, ground.Name, adiabatic.Name, pair.First.Name, pair.Second.Name }
            .Select(name => Assert.Single(document["BuildingSurface:Detailed"], item => item.Name == name)[5])
            .ToArray();
        Assert.Equal(new[] { "Outdoors", "Ground", "Adiabatic", "Surface", "Surface" }, mapped);
        return Observation("A13",
            "native-route=SurfaceBoundaryCondition|SurfaceBoundary|EnergyModelIdfAssembler-boundary-mapping",
            "enum-definition-order=Outdoors|Ground|Adiabatic|Zone",
            "enum-ToString=Outdoors|Ground|Adiabatic|Zone",
            "idf-boundary-tokens=Outdoors|Ground|Adiabatic|Surface|Surface",
            ExceptionFact("unlinked-zone-boundary", unlinked),
            ExceptionFact("undefined-enum-999", invalid),
            "python-boundary=lowercase-str-enum-and-unlinked-zone-state-not-reproduced");
    }

    private static NativeObservation ObserveA14()
    {
        Surface first = BasicSurface("A14-FIRST", "A14 First");
        Surface second = BasicSurface("A14-SECOND", "A14 Second", polygon: first.Polygon.Reverse());
        string before = SurfaceBoundaryState(first) + "|" + SurfaceBoundaryState(second);
        SurfaceAdjacencyPair pair = SurfaceAdjacency.Match(first, second);
        Assert.NotSame(first, pair.First);
        Assert.NotSame(second, pair.Second);
        Assert.Equal(SurfaceBoundaryCondition.Zone, pair.First.Boundary.Condition);
        Assert.Equal(SurfaceBoundaryCondition.Zone, pair.Second.Boundary.Condition);
        Assert.Equal(second.Id, pair.First.Boundary.AdjacentSurfaceId);
        Assert.Equal(first.Id, pair.Second.Boundary.AdjacentSurfaceId);
        Assert.Same(SurfaceBoundary.Outdoors, first.Boundary);
        Assert.Same(SurfaceBoundary.Outdoors, second.Boundary);
        Assert.Equal(before, SurfaceBoundaryState(first) + "|" + SurfaceBoundaryState(second));
        return Observation("A14",
            "native-route=SurfaceAdjacency.Match",
            "matched-first=" + SurfaceBoundaryState(pair.First),
            "matched-second=" + SurfaceBoundaryState(pair.Second),
            "matched-surface-identity=fresh",
            "captured-source-boundaries=original-Outdoors-unchanged",
            "python-boundary=in-place-reciprocal-mutation-not-reproduced");
    }

    private static NativeObservation ObserveA15()
    {
        Surface first = BasicSurface("A15-FIRST", "A15 First");
        Surface old = BasicSurface("A15-OLD", "A15 Old", polygon: first.Polygon.Reverse());
        Surface replacement = BasicSurface("A15-REPLACEMENT", "A15 Replacement", polygon: first.Polygon.Reverse());
        SurfaceAdjacencyPair oldPair = SurfaceAdjacency.Match(first, old);
        SurfaceAdjacencyPair replacementPair = SurfaceAdjacency.Match(first, replacement);
        ArgumentException self = Assert.Throws<ArgumentException>(() => SurfaceAdjacency.Match(first, first));
        Assert.Equal(old.Id, oldPair.First.Boundary.AdjacentSurfaceId);
        Assert.Equal(first.Id, oldPair.Second.Boundary.AdjacentSurfaceId);
        Assert.Equal(replacement.Id, replacementPair.First.Boundary.AdjacentSurfaceId);
        Assert.Equal(first.Id, replacementPair.Second.Boundary.AdjacentSurfaceId);
        Assert.Same(SurfaceBoundary.Outdoors, first.Boundary);
        Assert.Same(SurfaceBoundary.Outdoors, old.Boundary);
        Assert.Same(SurfaceBoundary.Outdoors, replacement.Boundary);
        Assert.Contains("cannot be adjacent to itself", self.Message, StringComparison.OrdinalIgnoreCase);
        return Observation("A15",
            "native-route=SurfaceAdjacency.Match-immutable-pairs",
            "old-pair=" + SurfaceBoundaryState(oldPair.First) + "|" + SurfaceBoundaryState(oldPair.Second),
            "replacement-pair=" + SurfaceBoundaryState(replacementPair.First) + "|" + SurfaceBoundaryState(replacementPair.Second),
            ExceptionFact("self-adjacency", self),
            "captured-source-boundaries=first/old/replacement-Outdoors-unchanged",
            "python-boundary=stale-reassignment-and-self-adjacency-not-reproduced");
    }

    private static NativeObservation ObserveA16()
    {
        PlanarPolygon host = HostPolygon();
        Glazing glazing = Glazing("A16 Glazing");
        NoMassConstruction doorConstruction = DoorConstruction("A16 Door Assembly");
        PlanarPolygon w1 = OpeningRectangle(1, 1, 1, 1);
        PlanarPolygon w2 = OpeningRectangle(3, 1, 1, 1);
        PlanarPolygon w3 = OpeningRectangle(5, 1, 1, 1);
        PlanarPolygon d1 = OpeningRectangle(1, 4, 1, 2);
        PlanarPolygon d2 = OpeningRectangle(4, 4, 1, 2);
        IOpening[] tooMany =
        {
            new Window(Entity("A16-M-AW1"), "A Window 1", glazing, w1),
            new Window(Entity("A16-M-AW2"), "A Window 2", glazing, w2),
            new Window(Entity("A16-M-AW3"), "A Window 3 Truncated", glazing, w3),
            new Door(Entity("A16-M-AD1"), "A Door 1", doorConstruction, d1),
            new Door(Entity("A16-M-AD2"), "A Door 2 Truncated", doorConstruction, d2),
        };
        IOpening[] tooFew =
        {
            new Window(Entity("A16-M-BW2"), "B Window 2 First", glazing, w2.Reverse()),
            new Window(Entity("A16-M-BW1"), "B Window 1 Second", glazing, w1.Reverse()),
            new Door(Entity("A16-M-BD1"), "B Door 1", doorConstruction, d1.Reverse()),
        };
        Surface mismatchedA = BasicSurface("A16-M-A", "Zip Surface A", tooMany, polygon: host);
        Surface mismatchedB = BasicSurface("A16-M-B", "Zip Surface B", tooFew, polygon: host.Reverse());
        ValidationResult mismatch = SurfaceAdjacency.ValidateMatch(mismatchedA, mismatchedB);
        Assert.False(mismatch.IsValid);
        Assert.Contains(mismatch.Diagnostics, item => item.Code == "INVISIBLEDRAGON.ADJACENCY.OPENING_COUNT_MISMATCH");
        ArgumentException mismatchError = Assert.Throws<ArgumentException>(() => SurfaceAdjacency.Match(mismatchedA, mismatchedB));

        IOpening[] validA =
        {
            new Window(Entity("A16-AW1"), "A Window 1", glazing, w1),
            new Window(Entity("A16-AW2"), "A Window 2", glazing, w2),
            new Window(Entity("A16-AW3"), "A Window 3", glazing, w3),
            new Door(Entity("A16-AD1"), "A Door 1", doorConstruction, d1),
            new Door(Entity("A16-AD2"), "A Door 2", doorConstruction, d2),
        };
        IOpening[] validB =
        {
            new Window(Entity("A16-BW2"), "B Window 2", glazing, w2.Reverse()),
            new Door(Entity("A16-BD2"), "B Door 2", doorConstruction, d2.Reverse()),
            new Window(Entity("A16-BW1"), "B Window 1", glazing, w1.Reverse()),
            new Door(Entity("A16-BD1"), "B Door 1", doorConstruction, d1.Reverse()),
            new Window(Entity("A16-BW3"), "B Window 3", glazing, w3.Reverse()),
        };
        Surface validSurfaceA = BasicSurface("A16-A", "Native Surface A", validA, polygon: host);
        Surface validSurfaceB = BasicSurface("A16-B", "Native Surface B", validB, polygon: host.Reverse());
        SurfaceAdjacencyPair pair = SurfaceAdjacency.Match(validSurfaceA, validSurfaceB);
        Surface[] capturedSurfaces = { pair.First, pair.Second };
        SurfaceBoundary[] capturedBoundaries = capturedSurfaces.Select(item => item.Boundary).ToArray();
        IReadOnlyList<IOpening>[] capturedOpeningCollections = capturedSurfaces
            .Select(item => item.Openings)
            .ToArray();
        IOpening[][] capturedOpenings = capturedSurfaces
            .Select(item => item.Openings.ToArray())
            .ToArray();
        string[] capturedStates = capturedSurfaces
            .Select(item => SurfaceBoundaryState(item) + ":" + Join(item.Openings.Select(opening => opening.Name)))
            .ToArray();
        var model = new EnergyModel("A16 legacy context", new[]
        {
            ZoneFor("A16-ZA", pair.First),
            ZoneFor("A16-ZB", pair.Second),
        });
        Assert.True(model.Validate().IsValid);
        IdfDocument firstDocument = model.ToIdfDocument(options: LegacyOptions());
        IdfDocument secondDocument = model.ToIdfDocument(options: LegacyOptions());
        string firstText = IdfWriter.Write(firstDocument);
        Assert.Equal(firstText, IdfWriter.Write(secondDocument));
        Assert.NotSame(firstDocument, secondDocument);
        Assert.Equal(firstDocument.Count, secondDocument.Count);
        for (int objectIndex = 0; objectIndex < firstDocument.Count; objectIndex++)
        {
            Assert.NotSame(firstDocument[objectIndex], secondDocument[objectIndex]);
        }

        for (int surfaceIndex = 0; surfaceIndex < capturedSurfaces.Length; surfaceIndex++)
        {
            Surface capturedSurface = capturedSurfaces[surfaceIndex];
            Assert.Same(capturedBoundaries[surfaceIndex], capturedSurface.Boundary);
            Assert.Same(capturedOpeningCollections[surfaceIndex], capturedSurface.Openings);
            Assert.Equal(capturedOpenings[surfaceIndex].Length, capturedSurface.Openings.Count);
            for (int openingIndex = 0; openingIndex < capturedOpenings[surfaceIndex].Length; openingIndex++)
            {
                Assert.Same(capturedOpenings[surfaceIndex][openingIndex], capturedSurface.Openings[openingIndex]);
            }

            Assert.Equal(
                capturedStates[surfaceIndex],
                SurfaceBoundaryState(capturedSurface) + ":" + Join(capturedSurface.Openings.Select(opening => opening.Name)));
        }

        string[] links = new[]
        {
            Link(firstDocument, "Window:Interzone", "A Window 1"),
            Link(firstDocument, "Window:Interzone", "A Window 2"),
            Link(firstDocument, "Window:Interzone", "A Window 3"),
            Link(firstDocument, "Door:Interzone", "A Door 1"),
            Link(firstDocument, "Door:Interzone", "A Door 2"),
            Link(firstDocument, "Window:Interzone", "B Window 1"),
            Link(firstDocument, "Window:Interzone", "B Window 2"),
            Link(firstDocument, "Window:Interzone", "B Window 3"),
            Link(firstDocument, "Door:Interzone", "B Door 1"),
            Link(firstDocument, "Door:Interzone", "B Door 2"),
        };
        Assert.Equal(new[]
        {
            "A Window 1->B Window 1", "A Window 2->B Window 2", "A Window 3->B Window 3",
            "A Door 1->B Door 1", "A Door 2->B Door 2",
            "B Window 1->A Window 1", "B Window 2->A Window 2", "B Window 3->A Window 3",
            "B Door 1->A Door 1", "B Door 2->A Door 2",
        }, links);
        return Observation("A16",
            "target-route=SurfaceAdjacency.ValidateMatch|SurfaceAdjacency.Match",
            "mismatched-opening-count-diagnostic=INVISIBLEDRAGON.ADJACENCY.OPENING_COUNT_MISMATCH",
            ExceptionFact("mismatched-opening-count-match", mismatchError),
            "python-boundary=positional-zip-truncation-not-reproduced",
            "legacy-parent-emission=context-only-not-targeted",
            "Surface.to_idf_object=excluded-from-target-and-receipt",
            "bounded-valid-counterpart-links=" + Join(links),
            "bounded-valid-counterpart-document-sha256=" + Sha256(Encoding.UTF8.GetBytes(firstText)),
            "bounded-valid-counterpart-two-call=document-and-objects-fresh-byte-deterministic",
            "captured-source-state=valid-surface-boundaries/opening-order-and-references-unchanged");
    }

    private static NativeObservation ObserveA17()
    {
        Surface surface = BasicSurface("A17-S", "A17 Subsurface", polygon: Square(4));
        PlanarPolygon sourcePolygon = surface.Polygon;
        string before = PolygonState(surface.Polygon);
        PlanarPolygon first = surface.CreateCenteredSubsurface(4);
        PlanarPolygon second = surface.CreateCenteredSubsurface(4);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Vertices, second.Vertices);
        Assert.Equal(4, first.Area, 12);
        Assert.Equal(4, second.Area, 12);
        Assert.Equal(new[] { "1,1,0", "3,1,0", "3,3,0", "1,3,0" }, first.Vertices.Select(VertexState));
        Assert.Equal(first.Vertices, second.Vertices);
        ArgumentOutOfRangeException equal = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(16));
        ArgumentOutOfRangeException zero = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(0));
        ArgumentOutOfRangeException negative = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(-4));
        ArgumentOutOfRangeException nan = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(double.NaN));
        ArgumentOutOfRangeException positiveInfinity = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(double.PositiveInfinity));
        ArgumentOutOfRangeException negativeInfinity = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(double.NegativeInfinity));
        Assert.All(new[] { equal, zero, negative, nan, positiveInfinity, negativeInfinity }, item => Assert.Equal("targetArea", item.ParamName));
        Assert.Same(sourcePolygon, surface.Polygon);
        Assert.Equal(before, PolygonState(surface.Polygon));
        return Observation("A17",
            "native-route=Surface.CreateCenteredSubsurface",
            "target-area=4",
            "native-scale=sqrt(4/16)=0.5",
            "native-result-area=4",
            "native-result-vertices=1,1,0|3,1,0|3,3,0|1,3,0",
            "two-call=result-polygons/vertex-collections-fresh-value-vertices-equal",
            ExceptionFact("equal-host-area-16", equal),
            ExceptionFact("zero", zero),
            ExceptionFact("negative-4", negative),
            "nonfinite-native-context=NaN/positive-infinity/negative-infinity-rejected-not-Python-equivalence",
            "nonnumeric-input=unresolved-static-double-API-boundary",
            "captured-source-state=host-polygon-reference-and-coordinates-unchanged",
            "python-boundary=linear-scale/equal/zero/negative-defect-not-reproduced");
    }

    private static NativeObservation ObserveA18()
    {
        Surface surface = BasicSurface("A18-S", "A18 Oversized", polygon: Square(4));
        PlanarPolygon sourcePolygon = surface.Polygon;
        string before = PolygonState(surface.Polygon);
        ArgumentOutOfRangeException first = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(20));
        ArgumentOutOfRangeException second = Assert.Throws<ArgumentOutOfRangeException>(() => surface.CreateCenteredSubsurface(20));
        Assert.Equal("targetArea", first.ParamName);
        Assert.Equal("targetArea", second.ParamName);
        Assert.Equal(20d, first.ActualValue);
        Assert.Equal(20d, second.ActualValue);
        Assert.Equal(first.GetType(), second.GetType());
        Assert.Same(sourcePolygon, surface.Polygon);
        Assert.Equal(before, PolygonState(surface.Polygon));
        return Observation("A18",
            "native-route=Surface.CreateCenteredSubsurface",
            "host-area=16",
            "target-area=20",
            ExceptionFact("oversized-first-call", first),
            ExceptionFact("oversized-second-call", second),
            "two-call=exception-type/parameter/actual-value-equal",
            "captured-source-state=host-polygon-reference-and-coordinates-unchanged",
            "python-boundary=ValueError-message-not-normalized-to-native-ArgumentOutOfRangeException");
    }

    private static NativeObservation Observation(string scenario, params string[] facts)
    {
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, item => Assert.False(string.IsNullOrWhiteSpace(item)));
        string hash = CanonicalSha256(JsonSerializer.SerializeToElement(facts));
        return new NativeObservation(scenario, facts, hash);
    }

    private static string BlindState(Blind value) => Join(new[]
    {
        value.Name,
        Double(value.SlatWidthMetres),
        Double(value.SlatSeparationMetres),
        Double(value.SlatAngleDegrees),
        Double(value.FrontReflectance),
        Double(value.BackReflectance),
    });

    private static string ShadeState(Shade value) => Join(new[]
    {
        value.Name,
        Double(value.Transmittance),
        Double(value.Reflectance),
        Double(value.Emissivity),
    });

    private static string DoorState(Door value) => Join(new[]
    {
        value.Name,
        value.Construction.Name,
        Double(value.Area),
    });

    private static string SurfaceBoundaryState(Surface value) =>
        value.Name + "|" + value.Boundary.Condition + "|" + (value.Boundary.AdjacentSurfaceId?.Value ?? "none");

    private static string PolygonState(PlanarPolygon value) =>
        Join(value.Vertices.Select(VertexState));

    private static string VertexState(Vertex value) =>
        Double(value.X) + "," + Double(value.Y) + "," + Double(value.Z);

    private static string ExceptionFact(string phase, Exception exception)
    {
        string parameter = exception is ArgumentException argument
            ? argument.ParamName ?? "none"
            : "not-applicable";
        return phase + "=" + exception.GetType().Name + "|param=" + parameter;
    }

    private static string Link(IdfDocument document, string objectType, string name)
    {
        IdfObject item = Assert.Single(document[objectType], value => value.Name == name);
        return name + "->" + item[3];
    }

    private static Surface BasicSurface(
        string id,
        string name,
        IEnumerable<IOpening>? openings = null,
        SurfaceBoundary? boundary = null,
        PlanarPolygon? polygon = null) => new(
            Entity(id),
            name,
            SurfaceType.Wall,
            DoorConstruction("Native Surface Construction"),
            boundary ?? SurfaceBoundary.Outdoors,
            polygon ?? HostPolygon(),
            openings);

    private static Zone ZoneFor(string id, Surface surface) => new(
        Entity(id),
        "Zone " + id,
        new[] { surface },
        new ZoneProfile(Entity("PROFILE-" + id), "Profile " + id));

    private static EnergyModelIdfOptions LegacyOptions() => new()
    {
        AddIdealLoadsForUnassignedZones = false,
        UseLegacyRectangularFenestration = true,
    };

    private static Glazing Glazing(string name = "Native Glazing") => new(name, 1.4, 0.4);

    private static NoMassConstruction DoorConstruction(string name = "Native Door Assembly") => new(name, 2.1);

    private static EntityId Entity(string value) => new(value);

    private static PlanarPolygon RectangleWithArea(double area)
    {
        double side = Math.Sqrt(area);
        return new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0),
            new Vertex(side, 0, 0),
            new Vertex(side, side, 0),
            new Vertex(0, side, 0),
        });
    }

    private static PlanarPolygon Square(double side) => new(new[]
    {
        new Vertex(0, 0, 0),
        new Vertex(side, 0, 0),
        new Vertex(side, side, 0),
        new Vertex(0, side, 0),
    });

    private static PlanarPolygon HostPolygon() => new(new[]
    {
        new Vertex(0, 0, 0),
        new Vertex(10, 0, 0),
        new Vertex(10, 0, 10),
        new Vertex(0, 0, 10),
    });

    private static PlanarPolygon OpeningRectangle(double x, double z, double width, double height) => new(new[]
    {
        new Vertex(x, 0, z),
        new Vertex(x + width, 0, z),
        new Vertex(x + width, 0, z + height),
        new Vertex(x, 0, z + height),
    });

    private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

    private static object CreateReceipt(TargetBinding target, IReadOnlyList<NativeObservation> observations) => new
    {
        assertion_id = target.AssertionId,
        adaptation_id = target.AdaptationId,
        classification = "exception",
        target_symbol = target.Symbol,
        native_target = target.NativeTarget,
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
            python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
            public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
            native_sources = NativeArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
        },
        case_coverage = target.CaseIndices.Select(index => Cases[index].CaseId).ToArray(),
        observations = target.CaseIndices.Select(index => new
        {
            case_id = Cases[index].CaseId,
            python_facts_sha256 = Cases[index].FactsSha256,
            native_fact_count = observations[index].Facts.Length,
            native_facts_sha256 = observations[index].FactsSha256,
            native_facts = observations[index].Facts,
        }).ToArray(),
        scope = new
        {
            already_covered_emitters_not_retargeted = new[]
            {
                "Blind.to_idf_object", "Door.to_idf_object", "Shade.to_idf_object",
                "Surface.to_idf_object", "Window.to_idf_object",
            },
            context_only_parent_emission_case_ids = target.Symbol == "Surface.boundary"
                ? new[] { Cases[15].CaseId }
                : Array.Empty<string>(),
            parent_emission_is_targeted = false,
            source_state_policy = "only-explicit-captured-state-facts-are-claimed;absence-means-no-source-state-claim",
            unresolved_behavior = UnresolvedFor(target),
        },
        upstream = new
        {
            ast_sha256 = UpstreamAstSha256,
            commit = UpstreamCommit,
            inventory_sha256 = InventoryContentSha256,
            source_bytes = UpstreamBytes,
            source_sha256 = UpstreamSourceSha256,
        },
    };

    private static string[] UnresolvedFor(TargetBinding target)
    {
        var values = new List<string>
        {
            target.AdaptationId + "-Python-state-outside-bounded-native-counterpart",
        };
        if (target.Symbol == "Surface.get_subsurface")
        {
            values.Add("Surface.get_subsurface-nan-positive-infinity-and-negative-infinity-Python-inputs");
            values.Add("Surface.get_subsurface-nonnumeric-Python-inputs-and-arithmetic-error-timing");
        }

        if (target.Symbol == "Surface.boundary")
        {
            values.Add("A16-Surface.to_idf_object-parent-emission-context-only-not-targeted");
        }

        if (target.Symbol is "Blind" or "Blind.__init__" or "Door" or "Door.__init__"
            or "Shade" or "Shade.__init__" or "Window" or "Window.__init__")
        {
            values.Add("standalone-child-to_idf_object-converter-closure-not-claimed");
        }

        return values.ToArray();
    }

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        byte_length = bytes,
        path,
        sha256,
    };

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal("exception", RequiredString(receipt, "classification"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));
        Assert.Equal(target.NativeTarget, RequiredString(receipt, "native_target"));
        JsonElement source = receipt.GetProperty("source_receipt");
        AssertReceiptFields(source, target, includeIndex: true);
        AssertStringArray(receipt.GetProperty("case_coverage"), target.CaseIndices.Select(index => Cases[index].CaseId));
        JsonElement[] actual = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(target.CaseIndices.Length, actual.Length);
        for (int index = 0; index < target.CaseIndices.Length; index++)
        {
            int caseIndex = target.CaseIndices[index];
            Assert.Equal(Cases[caseIndex].CaseId, RequiredString(actual[index], "case_id"));
            Assert.Equal(Cases[caseIndex].FactsSha256, RequiredString(actual[index], "python_facts_sha256"));
            Assert.Equal(observations[caseIndex].FactsSha256, RequiredString(actual[index], "native_facts_sha256"));
            Assert.Equal(observations[caseIndex].Facts.Length, actual[index].GetProperty("native_fact_count").GetInt32());
            AssertStringArray(actual[index].GetProperty("native_facts"), observations[caseIndex].Facts);
        }

        JsonElement scope = receipt.GetProperty("scope");
        Assert.False(scope.GetProperty("parent_emission_is_targeted").GetBoolean());
        string[] contextCases = scope.GetProperty("context_only_parent_emission_case_ids")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (target.Symbol == "Surface.boundary")
        {
            Assert.Equal(new[] { Cases[15].CaseId }, contextCases);
            Assert.Contains("A16-Surface.to_idf_object-parent-emission-context-only-not-targeted",
                scope.GetProperty("unresolved_behavior").EnumerateArray().Select(item => item.GetString()!));
        }
        else
        {
            Assert.Empty(contextCases);
        }
    }

    private static void AssertPinnedArtifact(string path, int bytes, string sha256)
    {
        string fullPath = FindRepositoryFile(path);
        byte[] content = File.ReadAllBytes(fullPath);
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

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
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

    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));

    private static void AssertStringArray(JsonElement value, IEnumerable<string> expected) =>
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));

    private static string RequiredString(JsonElement value, string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        return property.GetString()!;
    }

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(new Regex(@"0x[0-9a-fA-F]{8,}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
        Assert.DoesNotMatch(new Regex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
        Assert.DoesNotMatch(new Regex(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(new Regex(@"[A-Za-z]:\\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
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

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record CaseBinding(
        string Scenario,
        string CaseId,
        string CaseSha256,
        string FactsSha256,
        string[] Adaptations,
        string[] TargetSymbols,
        string[] ContextSymbols);

    private sealed record TargetBinding(
        string Symbol,
        int InventoryIndex,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string AdaptationId,
        string NativeTarget,
        int[] CaseIndices);

    private sealed record NativeObservation(string Scenario, string[] Facts, string FactsSha256);
}
