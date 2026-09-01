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
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class GeometryCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-shape-geometry-core-oracle.json";
    private const int FixtureBytes = 244_637;
    private const string FixtureSha256 =
        "sha256:46f026a4ce39931ec1e9d3581f49600e4178f3c744d2c6e022263d0fc695d4d8";
    private const string FixtureSchema =
        "dragons.python-reference.dragon-shape-geometry-core.v1";
    private const string CasesSha256 =
        "sha256:7890ed6463624c17ee70d4f0b0b9d684797b0bb55f1d7dae9a32b16a862fd8c7";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_shape_geometry_core_oracle.py";
    private const int GeneratorBytes = 82_614;
    private const string GeneratorSha256 =
        "sha256:ac340e5ec1b8eba038a947e0425427d1f8498744c69022fb34f2cfabfbf7f252";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_shape_geometry_core_oracle.py";
    private const int ValidatorBytes = 25_888;
    private const string ValidatorSha256 =
        "sha256:15c37f8ac41f3922a852fe55844c384574fa309ca8a99c0c7a8c02738641c428";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/shape.py";
    private const int UpstreamBytes = 27_438;
    private const string UpstreamSourceSha256 =
        "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c";
    private const string UpstreamAstSha256 =
        "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.GeometryCoreOracleParityTests.MatchesPinnedGeometryCoreThroughBoundedNativeRoutes";

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Internal/DomainGuard.cs", 2_416,
            "sha256:a8d28c985fe67376ca08015ed8e6d28600c98366c33a4a41dfd4abf377f57d8c"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/GeometryTolerance.cs", 505,
            "sha256:d71c816f1eac7c0f4d4bb6d3978640e764f25bdb4007f3ceda57b44d7ca0fcc0"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Vertex.cs", 3_795,
            "sha256:f37b229b45b23c23ddc54ed85aea1b93a201a74c30c7b29793f268e364435a67"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Vector3.cs", 3_782,
            "sha256:02536827db9d1c6ff48a46678871e4d736d9536228f0de370a9fb2c5294b9ede"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/PlanarPolygon.cs", 16_524,
            "sha256:73a1dd052fb12ed0802a6236d21484e2b680cbe3f0f4005ade6a61995111c653"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Surface.cs", 7_731,
            "sha256:545dc79dd89e84acf6d714e79da7b2cda059dfcaa3b4f74d291ad572ebd51264"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/SurfaceBoundary.cs", 1_909,
            "sha256:c0ba4cf5a93eb2678aee2c698320121f5bfbd68f7febb3dc901fe700da1499d9"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/SimpleConstructions.cs", 2_025,
            "sha256:4141d1125d33c40092caaf8b7e472bb50477a8c05b56b24ddf330ca72be22292"),
    };

    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };

    private static readonly CaseBinding[] Cases =
    {
        Case("V01", "v01-vertex-domain-mutable-state", "vertex",
            "sha256:fb9fd9b854743b7eedf56c21dd178bcdfab0943cab1b4237ab9288510070acd1",
            "sha256:904509ce52c0b486f82f1f130c63cd9f13b8d29987704c3c0fecf70250fb414f",
            new[] { "permissive-mutable-python-vertex-state" },
            new[] { "Vertex", "Vertex.__init__", "Vertex.x", "Vertex.y", "Vertex.z" }),
        Case("V02", "v02-vertex-copy-iteration-zero-radd", "vertex",
            "sha256:a3562158798dfcb9b2482c11bb187086dac404bb56b791e4dd73ca72a962464d",
            "sha256:7680009ebbee5bfe61ad9bd2f497a4c0ae9b42dcb9fe422790796b0f0a98c02e",
            new[] { "python-vertex-copy-iteration-zero-addition" },
            new[] { "Vertex.__deepcopy__", "Vertex.__iter__", "Vertex.__radd__" },
            new[] { "Vertex" }),
        Case("V03", "v03-vertex-point-vector-arithmetic", "vertex",
            "sha256:d8f34a82a16391847fa791d76675cbf7aba9ebcc0446e2d682e03f88ecc5bcc6",
            "sha256:dc6c1311ec1fe99e3c2e717157233427591ed85e3368abfeabe228914caca7a2",
            new[] { "untyped-python-vertex-algebra" },
            new[] { "Vertex.__add__", "Vertex.__mul__", "Vertex.__rmul__", "Vertex.__sub__", "Vertex.__truediv__" },
            new[] { "Vertex" }),
        Case("V04", "v04-vertex-operator-error-timing", "vertex",
            "sha256:910a2f90944c03a01a33df17abed33bc2819cb721f50bb0fe5af055acd4dec47",
            "sha256:3b87113455f92d8aa78515880ae94b03e4cdcd71c9cca302797d2fee77067166",
            new[] { "python-vertex-copy-iteration-zero-addition", "untyped-python-vertex-algebra" },
            new[] { "Vertex.__add__", "Vertex.__mul__", "Vertex.__radd__", "Vertex.__truediv__" },
            new[] { "Vertex", "Vertex.__rmul__" }),
        Case("V05", "v05-vertex-vector-metrics-zero-unit", "vertex",
            "sha256:6ecdf3b360223b0d2972fbe212d96d601ee38a3039081cf819be9791016e139a",
            "sha256:3a0f873d1d743750db80dbee692ab6c1aed2d2ac09206243250e62453f77964f",
            new[] { "untyped-python-vertex-metrics", "zero-preserving-python-vertex-unit" },
            new[] { "Vertex.cross", "Vertex.distance", "Vertex.dot", "Vertex.norm", "Vertex.unit" },
            new[] { "Vertex" }),
        Case("V06", "v06-vertex-coplanarity-angular-threshold", "vertex",
            "sha256:88da80328380e29b8d2813e736c4bfd44aa6451f438ea0dfd6f3c0da36090872",
            "sha256:486dc8e1c2705160ee637f0969f2fdff6ef09f221752d0a41759c337485bd5d4",
            new[] { "legacy-first-triple-angular-coplanarity" },
            new[] { "Vertex.are_coplanar" },
            new[] { "Vertex", "Vertex.__sub__", "Vertex.cross", "Vertex.unit", "Vertex.dot" }),
        Case("V07", "v07-vertex-coplanarity-first-three-collinear-defect", "vertex",
            "sha256:eed56b138d4c5e6dc4ab8ccace69c356137ef91f440793d055a3e335291f4e50",
            "sha256:56bde4fb6e5fa9d5fedd1bc17781abf8837dc9f29c545ff6fa1606772a40cdce",
            new[] { "legacy-first-triple-angular-coplanarity" },
            new[] { "Vertex.are_coplanar" },
            new[] { "Vertex", "Vertex.__sub__", "Vertex.cross", "Vertex.unit" }),
        Case("S08", "s08-surface-rectangle-scalar-geometry", "surface",
            "sha256:11350d69b5fa99127ab43938b8edc9376cf4a7fdc1f8efccee6fcecc83bc4cc1",
            "sha256:539b0710520bf4c4a14b8e6b1dff08dc2cbabd22934f9b66083b232c7b7fcf0f",
            new[] { "aliased-mutable-python-surface-vertices", "first-triple-oriented-python-surface-area", "first-triple-python-surface-normal", "mutable-string-coerced-python-surface-type", "permissive-python-surface-polygon-model", "vertex-mean-python-surface-center", "z-span-python-surface-height" },
            new[] { "Surface", "Surface.area", "Surface.center", "Surface.height", "Surface.normal", "Surface.type", "Surface.vertex" },
            new[] { "Surface.__init__", "Vertex" }),
        Case("S09", "s09-surface-reversed-winding", "surface",
            "sha256:5dc3ef16751f34401aaa608718bce6e01ca209914749168412b98a66b76d380a",
            "sha256:68dc77a160f95b446be3c9ef4167adb4e75c96dc66a619e43a8dad27bb1841b9",
            new[] { "first-triple-oriented-python-surface-area", "first-triple-python-surface-normal", "vertex-mean-python-surface-center", "z-span-python-surface-height" },
            new[] { "Surface.area", "Surface.center", "Surface.height", "Surface.normal" },
            new[] { "Surface.__init__", "Vertex", "Vertex.dot" }),
        Case("S10", "s10-surface-concave-reflex-first-turn-negative-area", "surface",
            "sha256:54a1e4b8b9c02583ad03e1c0421cbcc94fc4d1f0a6535f7c806c2b55b926d72e",
            "sha256:807e123999c4e67b21b1d4f7f6fdd6bf709a3a98507ac7554a42302019b21b7c",
            new[] { "first-triple-oriented-python-surface-area", "first-triple-python-surface-normal" },
            new[] { "Surface.area", "Surface.normal" },
            new[] { "Surface.__init__", "Vertex", "Vertex.cross", "Vertex.__radd__", "Vertex.__add__", "Vertex.dot" }),
        Case("S11", "s11-surface-invalid-polygon-acceptance", "surface",
            "sha256:161291e9b0287d395f1d639c2e5bc03e49ea6977df36b7468e6449a336c3c5c8",
            "sha256:aa93eef166452655a444e6b1868322ff07084345c31c99f5422bfedadbc6d7a7",
            new[] { "aliased-mutable-python-surface-vertices", "first-triple-oriented-python-surface-area", "first-triple-python-surface-normal", "permissive-python-surface-polygon-model" },
            new[] { "Surface", "Surface.area", "Surface.normal", "Surface.vertex" },
            new[] { "Surface.__init__", "Vertex", "Vertex.are_coplanar" }),
        Case("S12", "s12-surface-vertex-alias-mutation-and-setter-errors", "surface",
            "sha256:912e648311f5808094fb0c0ea689be6aa03628691422e79b5d0ccce8422b745c",
            "sha256:7f2da067edcf9230cc17b6bf7e448975dfb8aea9fbc781871907ec5eca3ba66f",
            new[] { "aliased-mutable-python-surface-vertices", "first-triple-oriented-python-surface-area", "first-triple-python-surface-normal", "mutable-string-coerced-python-surface-type", "vertex-mean-python-surface-center", "z-span-python-surface-height" },
            new[] { "Surface.area", "Surface.center", "Surface.height", "Surface.normal", "Surface.type", "Surface.vertex" },
            new[] { "Surface.__init__", "Vertex" }),
        Case("T13", "t13-surface-type-enum-string-topology", "surface-type",
            "sha256:03d049d473c467138691d7c6aa3bcd70da99b1fa5178ad5598ef975b61a7c055",
            "sha256:f5251fb2e0a46f95621b7ca0f458ef61efad0aee3a4fe05dbed26f14f70c0a80",
            new[] { "direct-surface-type-member-mapping", "lowercase-python-surface-type-enum" },
            new[] { "SurfaceType", "SurfaceType.CEILING", "SurfaceType.FLOOR", "SurfaceType.WALL", "SurfaceType.__str__" }),
        Case("T14", "t14-surface-type-conversion-error-topology", "surface-type",
            "sha256:ec5dabd2f1f0e30bba726e23b11b4ed33b681b91b6bcfd6f2d8a7bfa8c8cc3bd",
            "sha256:cb4c8b03cb4a2207eb499e0d38e5c25eee2f7159dfc1be11478ad8e19f6a3a7a",
            new[] { "lowercase-python-surface-type-enum" },
            new[] { "SurfaceType", "SurfaceType.__str__" },
            new[] { "SurfaceType.CEILING", "SurfaceType.FLOOR", "SurfaceType.WALL" }),
    };

    private static readonly TargetBinding[] Targets = CreateTargets();
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(11, "sha256:5d2d919fb410af2d45f4bc35f33da80a54be53a1256d42317e42e3b5a9842a74"),
        new(8, "sha256:52f8afa86f0e6d2bd9bc5d11b61d52a5387e9be9cca773ffa0b4d8b5b33a506a"),
        new(9, "sha256:70cbcf73bdf4a8796ae3ce892198086f962e41c0f524ec7cb4edfd05b26cdb1b"),
        new(12, "sha256:7c381cf1241835e95778862b7f18708c7beaa64eb9d12e7fd4b28d9dd92ec500"),
        new(12, "sha256:950a69182167b9d54a1fff475444b6fda9cb57db49f197910d4206358fa98d6b"),
        new(11, "sha256:42b615abbdc2d758ab651f43762ec77d47200ebbd4947ae5722e70fbbec55fe0"),
        new(7, "sha256:a60fcad544630b868d7c0c2224f083376b088b49402a0513a18116e1f3d929f2"),
        new(9, "sha256:0c3a8f6f4f838c38f57aee6f806cee1f90e332acb01cbfc96e1152a2337d93b6"),
        new(9, "sha256:1203c4e8dafbcc61c6391cfcfd356a235ac3f0695746572a17fbb6382346c1fa"),
        new(10, "sha256:d96692af20f512516e51bb8fc1e9071182bab9b1fcbfeba0c9089dbddbd16ea0"),
        new(13, "sha256:29d51e34d93adb91f0bf21a37391026168a4647fd727b32f3169eb916b31cfce"),
        new(10, "sha256:634cf51f7696cdf0965d1256ffbfe87ba27efe57a1953772c8a8ed645e2d8a24"),
        new(11, "sha256:30992944279731bda58ae5dd3481e9df1fafba1093183c9129e188e0fd76087c"),
        new(10, "sha256:671135ddd7fad561fa564aed6e53cde58e1061cee6b76affb4545c50695ae08c"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:2867a6b5a26a756fa6aeaf65068a2e9fa7321b0e6923e77ae9524036b686472d",
        "sha256:6cc3a7db18daa625d5dab183570f02c32e5319f911ac9f3517bff23d8d6aa191",
        "sha256:8074898397022e774997a6a725b80781111550897ab44740e89dc3238b2ff455",
        "sha256:a528074f8ab957267bac2b8ce3557194ff474c111b17321c8b97ea797b5df5a9",
        "sha256:81ec416bc7639194804e4380e03f99006dbf6a16d09525ccadec25da28321eb8",
        "sha256:d312aa561bdbb6cab2484f8a078a702bb56d38be42f3bcaa2800f5532b518888",
        "sha256:0810dc08418bc78deac6f89786a849f5c20390ecc994c9395bcad0c715ce3cd7",
        "sha256:e28446741c641805190b1565b43de667c6062361b1409215d5f7ad5d4314c31f",
        "sha256:05316ed46e42810cf7e5b0de1ba338d5f502c25a6a4c97e208fd5cdb9c435919",
        "sha256:4bab1a992d34a81b0e0b280baba6fe6d305cf430d82c817ccfef4b853ab574cb",
        "sha256:e4f63e8852e3c5be771dc461fd372e655c9683e9b3ed30ce928643eb0409029c",
        "sha256:14ae314f685943e78011024dcc0b279c48b972f9c72053029849eef98547e65b",
        "sha256:0f2962d8fab43164ac0ad84f04fe7bbbbe53ceaa9893f7b4093b4209aacfd34f",
        "sha256:9b3e942fa977cb5374c53c5539b263d8e66bffb8a7ca3d2dba42cea26460bc22",
        "sha256:7cc15089ff3974303b8ff66c82264f1e4575280cebe7dc53b695df31229e19aa",
        "sha256:a31d5463a22afc15fbfa422477e455ddbfe768412d716d1f76c4d4f0a4afd733",
        "sha256:5c429ab04b30eb052ab4fdd421665fb13e870a75b834a1f0a85307bd464002dc",
        "sha256:608a929646e45db53d8800b571e607ff4f191b15109a359ad311f91684840e38",
        "sha256:388fb6ca4786fe66e539f699caf4937ef824dc6118441a12a19e01dbb7ab049a",
        "sha256:4e09af087fb303452aaf3761ce473e61cf23286f9600a8efde211d9460a05f16",
        "sha256:bf1d02133fd2651444492a88f3b27ed4bd9b7cc1d4ffb0351232a7ca652ccc87",
        "sha256:9fcfa8d48d9d35d62788f9b2ad7f218fa224e37cdf6ecfc32fbbe02ddc9fdf53",
        "sha256:ae0fab18d3b9c344c9ca4b66c991a16a958cf93a6323b69faf7736d3160d1f87",
        "sha256:47acbfb4038b9fcc6998c29b6c2221e72bf7613ea55689cfb3b5cabf1a926c08",
        "sha256:ba64225df24c6f348dbe1c61a31f6d7abfee9b5198e9aeb7effbacd487f45f47",
        "sha256:f84f3567eecb80518afa19d48a74cd56841ff2d559f7374c3c29fa2a80da1d8e",
        "sha256:0d5f53eedec478840c4d78919bde15e99901b3011389dd7eecd5c6c10efdd324",
        "sha256:510b8d8341d45c68b2503628e1d23a91794e4a48266264c38831046f9fb518d6",
        "sha256:1b9c809756cb95c699a83bce8a074a73178f778e48bfeb9b094b6afa2f6a85b5",
        "sha256:b203ff08791766ecee97c0d65b2e6242bef726c134d29f1e9f590422c1ad7846",
        "sha256:39d8c23b19ef20d1ca83a4785214fb9470c910d6af5f9d34fd23a6f5a97ecfac",
    };

    [Fact]
    public void MatchesPinnedGeometryCoreThroughBoundedNativeRoutes()
    {
        ValidatePinnedArtifactsAndNativeApi();
        using JsonDocument oracle = ReadPinnedOracle();
        JsonElement[] fixtureCases = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(14, observations.Length);
        Assert.Equal(Cases.Select(item => item.Scenario), observations.Select(item => item.Scenario));

        object[] receipts = Targets.Select(target => CreateReceipt(target, observations)).ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "GEOMETRY_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(31, Targets.Length);
        Assert.Equal(31, Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(28, Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(new[] { "SurfaceType.CEILING", "SurfaceType.FLOOR", "SurfaceType.WALL" },
            Targets.Where(item => item.Classification == "equivalent").Select(item => item.Symbol));
        Assert.Equal(14, fixtureCases.Length);
    }

    private static CaseBinding Case(
        string scenario,
        string suffix,
        string subfamily,
        string caseSha256,
        string factsSha256,
        string[] adaptations,
        string[] targets,
        string[]? context = null) => new(
            scenario,
            "dragon-shape-geometry-core." + suffix,
            subfamily,
            caseSha256,
            factsSha256,
            adaptations,
            targets,
            context ?? Array.Empty<string>());

    private static TargetBinding[] CreateTargets() => new[]
    {
        Target("Surface", 1034, "class",
            "sha256:cb620c55ad36aaa035597b8c9975721d7fd397a000213beae556880050f75dba",
            "sha256:570bcca6296bf984b2732617159dc1d1b13c10126d72c363fbd6058b9aa3e6bf",
            "sha256:926a3131b4eecef848f3f1fca552718277fa4340c9b34aca7db597364c57df1f",
            "dragon-shape-geometry-core-1034-cb620c55", "permissive-python-surface-polygon-model", "exception",
            new[] { 7, 10 }),
        Target("Surface.area", 1038, "function",
            "sha256:f254ab666c61170d9ea16598a4182e7f49526eb4e0eaff0af293499695cbd9fa",
            "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4",
            "sha256:a562982884a4f5e17de2537275772db7d9600b609ed527d0fb20966f4f1c0d58",
            "dragon-shape-geometry-core-1038-f254ab66", "first-triple-oriented-python-surface-area", "exception",
            new[] { 7, 8, 9, 10, 11 }),
        Target("Surface.center", 1041, "function",
            "sha256:f0c05c2bc1bd07b18d9140cafa7f970129215c9aa311f80a0073445b92526273",
            "sha256:758f0228871f1c7811c457dd084ec9436eefb5e60aac482d304c0646a5f803f0",
            "sha256:8773235ee6e9cd4ea33c6d93b8289dd7a7bc3ce44e7ccaec3ce469719395716f",
            "dragon-shape-geometry-core-1041-f0c05c2b", "vertex-mean-python-surface-center", "exception",
            new[] { 7, 8, 11 }),
        Target("Surface.height", 1043, "function",
            "sha256:d479fe2f2ded1a09be3f2686e3ee6306b96beccb8cc20f04def11be7c0712f55",
            "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4",
            "sha256:c6fcbfd9ae4872946ffeecb5b90f49babe04c57e3a3999fe4353a66666869230",
            "dragon-shape-geometry-core-1043-d479fe2f", "z-span-python-surface-height", "exception",
            new[] { 7, 8, 11 }),
        Target("Surface.normal", 1044, "function",
            "sha256:3f089c8c429d26cd3ee65ff085dea58b961a0ec0c4b9b757172f65bf42a8b7e7",
            "sha256:758f0228871f1c7811c457dd084ec9436eefb5e60aac482d304c0646a5f803f0",
            "sha256:de6322fc22827c75a81be55ee33b7d86367f4e7619e5c61ae6bbd6dd09969fe8",
            "dragon-shape-geometry-core-1044-3f089c8c", "first-triple-python-surface-normal", "exception",
            new[] { 7, 8, 9, 10, 11 }),
        Target("Surface.type", 1046, "function",
            "sha256:ae4bdcc76210c35b23978d30c1d57491785d9fa9a2a66e80cc123e7c633a2db5",
            "sha256:8044a015cf023f600bfb62367bd05f9fb767cf01534d3432f781bbf466084b16",
            "sha256:5c4cda2372327676ed37a856be4b27f0f64d0c5846b3ca2523ea9665d5651313",
            "dragon-shape-geometry-core-1046-ae4bdcc7", "mutable-string-coerced-python-surface-type", "exception",
            new[] { 7, 11 }),
        Target("Surface.vertex", 1047, "function",
            "sha256:7ed5c6b3be62b893275d7dedccacd8cc2a85e7d0862801001a67650330ac2be8",
            "sha256:7d427b018243593f11def8ee612f23ac830a5a61aea07c16449702d14d2ce9b4",
            "sha256:6d481bd915484732dfe6bedcd08e09c4b0c1f3ee6ba47001210ad0238f8ab7e3",
            "dragon-shape-geometry-core-1047-7ed5c6b3", "aliased-mutable-python-surface-vertices", "exception",
            new[] { 7, 10, 11 }),
        Target("SurfaceType", 1054, "class",
            "sha256:61a37f9dc7fea0761d67c6e8efbd3ef6ef7e6e75788e8bcec26784d2a9bbf1a3",
            "sha256:db178ad05149cc0a5f8e817db69cd413099b8604c60b139f9a8603d7522744a5",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "dragon-shape-geometry-core-1054-61a37f9d", "lowercase-python-surface-type-enum", "exception",
            new[] { 12, 13 }),
        Target("SurfaceType.CEILING", 1055, "constant",
            "sha256:9ece83237cbba05bedb4f1f349b4505dba0a06a6d8e661bbb2e51485c0a28c4c",
            "sha256:2a36b1b600c86dd06a1f51523c5562edd967304930f43e09d0d0dcd555ed23d7",
            "sha256:f90f71b95564dd2dd802153760314b74dde7e11adc8daa18c55b696c6f10e914",
            "dragon-shape-geometry-core-1055-9ece8323", "direct-surface-type-member-mapping", "equivalent",
            new[] { 12 }),
        Target("SurfaceType.FLOOR", 1056, "constant",
            "sha256:c8c4f240e476a6db7cc85ca0bfcaea675233b72f28019edd4308f11cb689e01b",
            "sha256:909756f308b102264b0588f914f69542d69da96738233ca4fbb92a838d087bea",
            "sha256:37194ca6121ae832d5c991164c74dd662b39ba10da745ebc418aef2d1a834e5a",
            "dragon-shape-geometry-core-1056-c8c4f240", "direct-surface-type-member-mapping", "equivalent",
            new[] { 12 }),
        Target("SurfaceType.WALL", 1057, "constant",
            "sha256:ca6d5593884470ef294f9e38f3e03f945136bb49d08ef1e6fa9d08d5cac35cf4",
            "sha256:df01e4736e1699406341a3ee335a4f9131b888ade81d8a1a5781ba152ae3bf65",
            "sha256:d0a6a6d9c9b4333e9f62641d948701347778a44a64f2db44b9d1f6dd8bde1aff",
            "dragon-shape-geometry-core-1057-ca6d5593", "direct-surface-type-member-mapping", "equivalent",
            new[] { 12 }),
        Target("SurfaceType.__str__", 1058, "function",
            "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e",
            "sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab",
            "sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8",
            "dragon-shape-geometry-core-1058-f40e4929", "lowercase-python-surface-type-enum", "exception",
            new[] { 12, 13 }),
        Target("Vertex", 1059, "class",
            "sha256:786502893a6774ddb9c263e2ce3de1037c9f88f8dde30c32f04ad6e1418f0b64",
            "sha256:b1fc2f021d39b52f7fdf7c9fef986a60b73e5b86dc26bbe4b00dedc0cf5c4f17",
            "sha256:5ebda0e1f32f1fd86c57fc26145879526562d3c03795f2dceb5fbfcf00544a72",
            "dragon-shape-geometry-core-1059-78650289", "permissive-mutable-python-vertex-state", "exception",
            new[] { 0 }),
        Target("Vertex.__add__", 1060, "function",
            "sha256:a5c7ecea4df4c83044d8b673c72a7352e3121627c2aafe1f6e99a3ffba35977e",
            "sha256:0b34e90dfcf0e856807608a50fd75c29f13fe6b59fb8c1770d465590c56f6ef8",
            "sha256:8c3de0950e49fa12688e9d4d9c9762768c1e5590dc4ec407d7ea446a11cf4f0f",
            "dragon-shape-geometry-core-1060-a5c7ecea", "untyped-python-vertex-algebra", "exception",
            new[] { 2, 3 }),
        Target("Vertex.__deepcopy__", 1061, "function",
            "sha256:2c79da1a720680314133fb5aebf7c420f8586bd91d402a578b6797f0833b7f85",
            "sha256:6fdfdefd8e1f58c6a42b3d6022896a8dabcd8576ae421e727a5662fd45da8c58",
            "sha256:81f41f4035c79daddde1320ce8f8285f29d7b1a6b54ab19e60f48a89c11cca22",
            "dragon-shape-geometry-core-1061-2c79da1a", "python-vertex-copy-iteration-zero-addition", "exception",
            new[] { 1 }),
        Target("Vertex.__init__", 1063, "function",
            "sha256:be3c69c5422b57d538899edd108fb477fcb0766fdea42e53f6e6ca25ae838ac3",
            "sha256:39724fa8eb687875f0df66ecc43c3a3681896413da32edd88babedfcafb38aa2",
            "sha256:acc791c29de051e80a5e0e5abe4d3b37dc788ff78231ee1f84faa7121755a4e6",
            "dragon-shape-geometry-core-1063-be3c69c5", "permissive-mutable-python-vertex-state", "exception",
            new[] { 0 }),
        Target("Vertex.__iter__", 1064, "function",
            "sha256:e95d7ce5aa55d56bc0012c191bc98fc7cd74941f724816583108f53e9bda37e7",
            "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b",
            "sha256:235dfb710a8b427a949ffdaba55989daa05a5467bbcd2bb625e6441ae6506649",
            "dragon-shape-geometry-core-1064-e95d7ce5", "python-vertex-copy-iteration-zero-addition", "exception",
            new[] { 1 }),
        Target("Vertex.__mul__", 1065, "function",
            "sha256:323878e160b4a3f298740187d6136d8d8e9c112ae6f7097dccc7fd9d4be57747",
            "sha256:eead1105b53d5053c1389ede1e8718c2eebe7c78cd9f6a3e9989b8e665b1bf41",
            "sha256:83a388a00380d4a43bab7d21857dbd57886c74559980ec71fcbb7fd86eca662e",
            "dragon-shape-geometry-core-1065-323878e1", "untyped-python-vertex-algebra", "exception",
            new[] { 2, 3 }),
        Target("Vertex.__radd__", 1066, "function",
            "sha256:a473d0f327d8b3055e2e614d0e6da54681e058469f4d3266ac69d0849849dd35",
            "sha256:0b34e90dfcf0e856807608a50fd75c29f13fe6b59fb8c1770d465590c56f6ef8",
            "sha256:27ca274fd8c8adccd65b61c31f4fb234fa0597a63e30eb23d237ba6f4857915c",
            "dragon-shape-geometry-core-1066-a473d0f3", "python-vertex-copy-iteration-zero-addition", "exception",
            new[] { 1, 3 }),
        Target("Vertex.__rmul__", 1068, "function",
            "sha256:1dbe33d37c8ebeda67422c7b71c99b4314290005faaab35c11a6d62446da88ef",
            "sha256:eead1105b53d5053c1389ede1e8718c2eebe7c78cd9f6a3e9989b8e665b1bf41",
            "sha256:deaef8f9df40bb8ed6eb2dbd9bbd9be5bfcfbb2a5f989592308bde8e8f3cfc4f",
            "dragon-shape-geometry-core-1068-1dbe33d3", "untyped-python-vertex-algebra", "exception",
            new[] { 2 }),
        Target("Vertex.__sub__", 1070, "function",
            "sha256:4ee38e65b625fbec9e82d2cf2497d08bc3569dd9f19bb9d68500823113b2a9fc",
            "sha256:adec158f0b08785de53c534711342a08a6615b6a64fa56ad349652df955f9117",
            "sha256:4e0eaec417cf72093e0a90ceae1895df9ec9faa3b7b4ff3c4dcd7c69049c8161",
            "dragon-shape-geometry-core-1070-4ee38e65", "untyped-python-vertex-algebra", "exception",
            new[] { 2 }),
        Target("Vertex.__truediv__", 1071, "function",
            "sha256:94f397b889c7022f9e61270308cb32f2994fa6336aa0034bf6af9f73fa05ee53",
            "sha256:eead1105b53d5053c1389ede1e8718c2eebe7c78cd9f6a3e9989b8e665b1bf41",
            "sha256:fd9873b2ccd6e62b270feaf054790d38d7c7b908c5eb4f33f96c13f3924aae75",
            "dragon-shape-geometry-core-1071-94f397b8", "untyped-python-vertex-algebra", "exception",
            new[] { 2, 3 }),
        Target("Vertex.are_coplanar", 1072, "function",
            "sha256:905ebbf25f731adcf96fd59e0ee78f8afda0e325ed624baa9e0124cc3a5da493",
            "sha256:7be14f957bc48e96bae40454c83374af2b60d403fe48462c29c7b230debf7e19",
            "sha256:56358e3c2ceccce0c3bea4251071e545f085ba3ac12c7e67889af47901603ef3",
            "dragon-shape-geometry-core-1072-905ebbf2", "legacy-first-triple-angular-coplanarity", "exception",
            new[] { 5, 6 }),
        Target("Vertex.cross", 1073, "function",
            "sha256:6bc5db49d054daacb8f76e26342f1a6f45ccbdffdc1119addebe8e18ccbad02a",
            "sha256:adec158f0b08785de53c534711342a08a6615b6a64fa56ad349652df955f9117",
            "sha256:2230ee104aae5a223bb3bc01226737df630ece57a4b82431d159f9b1713d6fc2",
            "dragon-shape-geometry-core-1073-6bc5db49", "untyped-python-vertex-metrics", "exception",
            new[] { 4 }),
        Target("Vertex.distance", 1074, "function",
            "sha256:88c4cb9fbd03fc69d540cf3b644516743673077ab0ec7540c84a767eaca902cc",
            "sha256:569df6a5f374ddb3ab8f3639f6b20f67c2cdeac646b5e466fa0a30abc63bf4f0",
            "sha256:bb92daef11c92597c835b9e59a5dafddba4087552e828196595bb123a588a24a",
            "dragon-shape-geometry-core-1074-88c4cb9f", "untyped-python-vertex-metrics", "exception",
            new[] { 4 }),
        Target("Vertex.dot", 1075, "function",
            "sha256:1aaf5930f9dbfec62d7999fc240ee947eaa0397482c417da92d23ea51d79cc87",
            "sha256:8b6676a26cd4d89db3c842512e2bbb89318f331b84cc563c81a6163c4de9a41c",
            "sha256:886a9cd2804a3444fd71aaf9a4813692f51559ee541da3df44733244d2f19b03",
            "dragon-shape-geometry-core-1075-1aaf5930", "untyped-python-vertex-metrics", "exception",
            new[] { 4 }),
        Target("Vertex.norm", 1076, "function",
            "sha256:e41eae31e96f574bb148c14e0e8f19d03302136144b6e43baf73a18bfa678b49",
            "sha256:2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb",
            "sha256:096687da4f4b02a9c7ec12d7156245accf2ef86da9cbf4be0b05e28a5f2ddf4e",
            "dragon-shape-geometry-core-1076-e41eae31", "untyped-python-vertex-metrics", "exception",
            new[] { 4 }),
        Target("Vertex.unit", 1077, "function",
            "sha256:4267bc06a7a7d67fece4bdcb4963be1e87dd65436d8cecacce017bd19cf8c756",
            "sha256:2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb",
            "sha256:78c3a37b25dd7def0de8575181b283a5f240201d40991df19edc538a271cacab",
            "dragon-shape-geometry-core-1077-4267bc06", "zero-preserving-python-vertex-unit", "exception",
            new[] { 4 }),
        Target("Vertex.x", 1078, "function",
            "sha256:d859bad0320353e43a2fc277a54559f90cbcf19e91d3d5b49e0ec77a98da5125",
            "sha256:46ed90dbe20788ec581fc97c8027d66792fdc63ad8cf0702b3e84a8a69db3b35",
            "sha256:ecb4351565fd2434784b488f3f7faa82b7ebdc52c0c46698a71fb80b5a0496aa",
            "dragon-shape-geometry-core-1078-d859bad0", "permissive-mutable-python-vertex-state", "exception",
            new[] { 0 }),
        Target("Vertex.y", 1079, "function",
            "sha256:ff0bcc126b70820f4cd15e2d743102715885b2f19b3df6662eaba221a54f6e4c",
            "sha256:83e3d9391df015016420796f049ffde5c068bd6aa96d53568378dc723c8378fe",
            "sha256:ef8438299afc4f99a72230048fe2ae093565a58ebf9c57237f52c91abdd0531e",
            "dragon-shape-geometry-core-1079-ff0bcc12", "permissive-mutable-python-vertex-state", "exception",
            new[] { 0 }),
        Target("Vertex.z", 1080, "function",
            "sha256:64899affcdb0d27b23069a9323ba7e71ae572ecddadb121c28051a8d279fcfc5",
            "sha256:6763f7596780d07ccc7b400fd60c35cf716e7acc81fccb83ed5f5ad9cc2e7538",
            "sha256:9afbb156e7e4dbb655341601471eca492d18f0484e157bfb73d6e7e1db309158",
            "dragon-shape-geometry-core-1080-64899aff", "permissive-mutable-python-vertex-state", "exception",
            new[] { 0 }),
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
        string classification,
        int[] caseIndices) => new(
            symbol,
            inventoryIndex,
            kind,
            symbolHash,
            signatureHash,
            bodyHash,
            assertionId,
            adaptationId,
            classification,
            NativeTargetFor(symbol),
            caseIndices);

    private static string NativeTargetFor(string symbol) => symbol switch
    {
        "Surface" => "Dragons.InvisibleDragon.Shape.Surface plus PlanarPolygon",
        "Surface.area" => "Surface.GrossArea via PlanarPolygon.Area",
        "Surface.center" => "Surface.Center via PlanarPolygon.Centroid",
        "Surface.height" => "Surface.Height via PlanarPolygon.Height",
        "Surface.normal" => "Surface.Normal via PlanarPolygon.Normal",
        "Surface.type" => "Surface.Type immutable enum property",
        "Surface.vertex" => "Surface.Polygon.Vertices immutable defensive copy",
        "SurfaceType" => "Dragons.InvisibleDragon.Shape.SurfaceType",
        "SurfaceType.CEILING" => "SurfaceType.Ceiling",
        "SurfaceType.FLOOR" => "SurfaceType.Floor",
        "SurfaceType.WALL" => "SurfaceType.Wall",
        "SurfaceType.__str__" => "explicit native enum-to-IDF mapping where required",
        "Vertex" => "Dragons.InvisibleDragon.Shape.Vertex readonly struct",
        "Vertex.__add__" => "Vertex plus Vector3 operator",
        "Vertex.__deepcopy__" => "Vertex value-copy semantics",
        "Vertex.__init__" => "Vertex constructor with finite double guards",
        "Vertex.__iter__" => "explicit X/Y/Z projection",
        "Vertex.__mul__" => "Vector3 scalar multiplication after point-to-vector adaptation",
        "Vertex.__radd__" => "explicit identity/copy adaptation",
        "Vertex.__rmul__" => "Vector3 scalar multiplication after point-to-vector adaptation",
        "Vertex.__sub__" => "Vertex minus Vertex returns Vector3",
        "Vertex.__truediv__" => "Vector3 scalar division after point-to-vector adaptation",
        "Vertex.are_coplanar" => "Vertex.AreCoplanar with geometric distance tolerance",
        "Vertex.cross" => "Vector3.Cross after point-to-vector adaptation",
        "Vertex.distance" => "Vertex.DistanceTo",
        "Vertex.dot" => "Vector3.Dot after point-to-vector adaptation",
        "Vertex.norm" => "Vector3.Length after point-to-vector adaptation",
        "Vertex.unit" => "Vector3.Normalize with zero-vector rejection",
        "Vertex.x" => "Vertex.X immutable finite double",
        "Vertex.y" => "Vertex.Y immutable finite double",
        "Vertex.z" => "Vertex.Z immutable finite double",
        _ => throw new Xunit.Sdk.XunitException($"No native target for '{symbol}'."),
    };

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
            AssertKeys(item, "context_symbols", "executor", "expected_dotnet", "id", "python", "subfamily", "target_symbols");
            Assert.Equal("shape-geometry-core", RequiredString(item, "executor"));
            Assert.Equal(binding.Subfamily, RequiredString(item, "subfamily"));
            AssertStringArray(item.GetProperty("target_symbols"), binding.TargetSymbols);
            AssertStringArray(item.GetProperty("context_symbols"), binding.ContextSymbols);

            JsonElement expected = item.GetProperty("expected_dotnet");
            Assert.Equal("adapted-or-equivalent-as-pinned", RequiredString(expected, "outcome"));
            AssertStringArray(expected.GetProperty("adaptations"), binding.Adaptations);
            JsonElement classifications = expected.GetProperty("classifications");
            Assert.Equal(binding.TargetSymbols.Length, classifications.EnumerateObject().Count());
            foreach (string symbol in binding.TargetSymbols)
            {
                TargetBinding target = Assert.Single(Targets, item => item.Symbol == symbol);
                Assert.Equal(target.Classification, RequiredString(classifications, symbol));
            }

            JsonElement python = item.GetProperty("python");
            Assert.Equal("observed", RequiredString(python, "outcome"));
            JsonElement facts = python.GetProperty("facts");
            Assert.Equal(binding.Scenario, RequiredString(facts, "scenario"));
            Assert.Equal(binding.Subfamily, RequiredString(facts, "subfamily"));
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
            AssertReceiptFields(descriptors[index], target, includeIndex: false);
            AssertReceiptFields(receipts[index], target, includeIndex: true);
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
        Assert.Equal(14, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        AssertStringArray(contract.GetProperty("target_symbols"), Targets.Select(item => item.Symbol));
        Assert.Equal(28, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        Assert.Equal(3, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());

        for (int index = 0; index < Targets.Length; index++)
        {
            TargetBinding target = Targets[index];
            Assert.Equal(target.AdaptationId, RequiredString(contract.GetProperty("adaptations"), target.Symbol));
            Assert.Equal(target.AssertionId, RequiredString(contract.GetProperty("assertion_ids"), target.Symbol));
            Assert.Equal(target.Classification, RequiredString(contract.GetProperty("classifications"), target.Symbol));
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
        Assert.Equal("exact-fourteen-case-three-subfamily-geometry-core-matrix", RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("target_symbols"), Targets.Select(item => item.Symbol));
        string[] outOfScope = closure.GetProperty("out_of_scope_symbols_not_promoted")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(new[]
        {
            "Surface.__repr__", "Surface.__str__", "Vertex.__eq__", "Vertex.__repr__", "Vertex.__str__",
        }, outOfScope);
        string[] opening = closure.GetProperty("opening_adjacency_targets_not_promoted")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Contains("Surface.__init__", opening);
        Assert.Contains("Surface.get_subsurface", opening);
        Assert.DoesNotContain(opening, item => Targets.Any(target => target.Symbol == item));
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
        JsonElement shape = upstream.GetProperty("shape_source");
        Assert.Equal(UpstreamPath, RequiredString(shape, "path"));
        Assert.Equal(UpstreamBytes, shape.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(shape, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(shape, "ast_sha256"));
        JsonElement loaded = Assert.Single(upstream.GetProperty("loaded_local_modules").EnumerateArray(),
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("idragon.dragon.shape", RequiredString(loaded, "module"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(loaded, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(loaded, "ast_sha256"));
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

        Assert.True(typeof(Vertex).IsValueType);
        Assert.True(typeof(Vertex).IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false));
        Assert.False(typeof(IEnumerable).IsAssignableFrom(typeof(Vertex)));
        Assert.Equal(new[] { "X", "Y", "Z" }, typeof(Vertex).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(item => item.Name is "X" or "Y" or "Z").Select(item => item.Name));
        Assert.All(new[] { "X", "Y", "Z" }, name =>
        {
            PropertyInfo property = typeof(Vertex).GetProperty(name)!;
            Assert.Equal(typeof(double), property.PropertyType);
            Assert.False(property.CanWrite);
        });
        ConstructorInfo vertexConstructor = Assert.Single(typeof(Vertex).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(new[] { typeof(double), typeof(double), typeof(double) },
            vertexConstructor.GetParameters().Select(item => item.ParameterType));
        Assert.Equal(typeof(double), typeof(Vertex).GetMethod(nameof(Vertex.DistanceTo))!.ReturnType);
        Assert.Equal(typeof(Vector3), typeof(Vertex).GetMethod(nameof(Vertex.ToVector))!.ReturnType);
        Assert.Equal(typeof(bool), typeof(Vertex).GetMethod(nameof(Vertex.AreCoplanar), BindingFlags.Public | BindingFlags.Static)!.ReturnType);

        Assert.True(typeof(Vector3).IsValueType);
        Assert.True(typeof(Vector3).IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false));
        Assert.Equal(typeof(double), typeof(Vector3).GetProperty(nameof(Vector3.Length))!.PropertyType);
        Assert.Equal(typeof(Vector3), typeof(Vector3).GetMethod(nameof(Vector3.Cross))!.ReturnType);
        Assert.Equal(typeof(double), typeof(Vector3).GetMethod(nameof(Vector3.Dot))!.ReturnType);
        Assert.Equal(typeof(Vector3), typeof(Vector3).GetMethod(nameof(Vector3.Normalize))!.ReturnType);

        Assert.Equal(typeof(double), typeof(PlanarPolygon).GetProperty(nameof(PlanarPolygon.Area))!.PropertyType);
        Assert.Equal(typeof(Vertex), typeof(PlanarPolygon).GetProperty(nameof(PlanarPolygon.Centroid))!.PropertyType);
        Assert.Equal(typeof(Vector3), typeof(PlanarPolygon).GetProperty(nameof(PlanarPolygon.Normal))!.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<Vertex>), typeof(PlanarPolygon).GetProperty(nameof(PlanarPolygon.Vertices))!.PropertyType);
        Assert.Equal(typeof(PlanarPolygon), typeof(Surface).GetProperty(nameof(Surface.Polygon))!.PropertyType);
        Assert.Equal(typeof(SurfaceType), typeof(Surface).GetProperty(nameof(Surface.Type))!.PropertyType);
        Assert.False(typeof(Surface).GetProperty(nameof(Surface.Type))!.CanWrite);
        Assert.False(typeof(Surface).GetProperty(nameof(Surface.Polygon))!.CanWrite);
        Assert.Equal(new[] { "Wall", "Ceiling", "Floor" }, Enum.GetNames(typeof(SurfaceType)));
    }

    private static NativeObservation ObserveNativeCase(int index) => index switch
    {
        0 => ObserveV01(),
        1 => ObserveV02(),
        2 => ObserveV03(),
        3 => ObserveV04(),
        4 => ObserveV05(),
        5 => ObserveV06(),
        6 => ObserveV07(),
        7 => ObserveS08(),
        8 => ObserveS09(),
        9 => ObserveS10(),
        10 => ObserveS11(),
        11 => ObserveS12(),
        12 => ObserveT13(),
        13 => ObserveT14(),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
    };

    private static NativeObservation ObserveV01()
    {
        var first = new Vertex(1, 2, 3);
        var second = new Vertex(1, 2, 3);
        string before = VertexState(first);
        Exception nan = Assert.Throws<ArgumentOutOfRangeException>(() => new Vertex(double.NaN, 0, 0));
        Exception positiveInfinity = Assert.Throws<ArgumentOutOfRangeException>(() => new Vertex(0, double.PositiveInfinity, 0));
        Exception negativeInfinity = Assert.Throws<ArgumentOutOfRangeException>(() => new Vertex(0, 0, double.NegativeInfinity));
        Assert.Equal(before, VertexState(first));
        Assert.Equal(first, second);
        return Observation("V01",
            "native-route=Vertex(double,double,double)-and-get-only-X/Y/Z",
            "finite-first=" + before,
            "finite-repeat=" + VertexState(second),
            "repeat-value-equal=true",
            "native-value-type-readonly=true",
            "coordinate-setters=X:false|Y:false|Z:false",
            ExceptionFact("nan-x", nan),
            ExceptionFact("positive-infinity-y", positiveInfinity),
            ExceptionFact("negative-infinity-z", negativeInfinity),
            "python-boundary=mutable/nonfinite/bool-preserving/arbitrary-precision-integer-state-not-claimed-for-native-double-API",
            "captured-source-state=finite-vertex-value-unchanged");
    }

    private static NativeObservation ObserveV02()
    {
        var original = new Vertex(1, 2, 3);
        Vertex copy = original;
        double[] projection = { original.X, original.Y, original.Z };
        Vertex identity = Vertex.Origin + original.ToVector();
        string before = VertexState(original);
        Assert.Equal(original, copy);
        Assert.Equal(original, identity);
        Assert.Equal(new[] { 1d, 2d, 3d }, projection);
        Assert.Equal(before, VertexState(original));
        return Observation("V02",
            "native-route=Vertex-value-copy-plus-explicit-X/Y/Z-projection-plus-Origin+ToVector",
            "value-copy=" + VertexState(copy),
            "coordinate-projection=1|2|3",
            "origin-identity=" + VertexState(identity),
            "native-IEnumerable=false",
            "native-copy-model=value-type-no-object-freshness-identity-claim",
            "python-boundary=generator-exhaustion/object-identity/mutable-copy-after-source-mutation-not-claimed",
            "captured-source-state=original-value-unchanged");
    }

    private static NativeObservation ObserveV03()
    {
        var left = new Vertex(1, 2, 3);
        var right = new Vertex(4, -5, 6);
        string before = VertexState(left) + "|" + VertexState(right);
        Vertex added = left + right.ToVector();
        Vector3 rightScaled = left.ToVector() * 2;
        Vector3 leftScaled = 2 * left.ToVector();
        Vector3 subtracted = left - right;
        Vector3 divided = left.ToVector() / 2;
        string first = Join(new[] { VertexState(added), VectorState(rightScaled), VectorState(leftScaled), VectorState(subtracted), VectorState(divided) });
        string repeat = Join(new[]
        {
            VertexState(left + right.ToVector()),
            VectorState(left.ToVector() * 2),
            VectorState(2 * left.ToVector()),
            VectorState(left - right),
            VectorState(left.ToVector() / 2),
        });
        Assert.Equal(first, repeat);
        Assert.Equal(before, VertexState(left) + "|" + VertexState(right));
        return Observation("V03",
            "native-route=typed-Vertex+Vector3/Vertex-Vertex/Vector3*double/double*Vector3/Vector3-double-division",
            "point-add-via-right-vector=" + VertexState(added),
            "right-scale-via-vector=" + VectorState(rightScaled),
            "left-scale-via-vector=" + VectorState(leftScaled),
            "point-subtraction-vector=" + VectorState(subtracted),
            "division-via-vector=" + VectorState(divided),
            "repeat-results-equal=true",
            "python-boundary=untyped-point-as-vector-result-types-are-adapted-not-equated",
            "captured-source-state=left-and-right-values-unchanged");
    }

    private static NativeObservation ObserveV04()
    {
        var value = new Vertex(1, 2, 3);
        string before = VertexState(value);
        Vertex zeroIdentity = Vertex.Origin + value.ToVector();
        Vector3 zeroScaled = value.ToVector() * 0;
        Vector3 oneScaled = 1 * value.ToVector();
        Exception divideZero = Assert.Throws<DivideByZeroException>(() => _ = value.ToVector() / 0);
        Exception nanScale = Assert.Throws<ArgumentOutOfRangeException>(() => _ = value.ToVector() * double.NaN);
        Exception infinityDivide = Assert.Throws<ArgumentOutOfRangeException>(() => _ = value.ToVector() / double.PositiveInfinity);
        string vertexOperators = Join(V04DeclaredOperatorSignatures(typeof(Vertex)));
        string vectorOperators = Join(V04DeclaredOperatorSignatures(typeof(Vector3)));
        Assert.Equal(
            "op_Addition(Vertex,Vector3)",
            vertexOperators);
        Assert.Equal(
            "op_Division(Vector3,Double)|op_Multiply(Double,Vector3)|op_Multiply(Vector3,Double)",
            vectorOperators);
        Assert.Equal(value, zeroIdentity);
        Assert.Equal(Vector3.Zero, zeroScaled);
        Assert.Equal(value.ToVector(), oneScaled);
        Assert.Equal(before, VertexState(value));
        return Observation("V04",
            "native-route=statically-typed-Vertex/Vector3-operators",
            "zero-identity-adaptation=" + VertexState(zeroIdentity),
            "zero-vector-scale=" + VectorState(zeroScaled),
            "one-left-vector-scale=" + VectorState(oneScaled),
            ExceptionFact("divide-zero", divideZero),
            ExceptionFact("nan-scale", nanScale),
            ExceptionFact("positive-infinity-divisor", infinityDivide),
            "native-in-scope-vertex-operators=" + vertexOperators,
            "native-in-scope-vector-operators=" + vectorOperators,
            "native-type-boundary=bool/string/nonzero-int-point-operator-protocols-are-not-callable-typed-routes",
            "python-boundary=dynamic-dispatch-error-phase-and-message-not-equated",
            "captured-source-state=value-unchanged");
    }

    private static NativeObservation ObserveV05()
    {
        var value = new Vertex(3, 4, 0);
        var other = new Vertex(0, 0, 12);
        var zero = Vertex.Origin;
        string before = Join(new[] { VertexState(value), VertexState(other), VertexState(zero) });
        Vector3 valueVector = value.ToVector();
        Vector3 otherVector = other.ToVector();
        Vector3 cross = valueVector.Cross(otherVector);
        double distance = value.DistanceTo(other);
        double dot = valueVector.Dot(otherVector);
        double norm = valueVector.Length;
        Vector3 unit = valueVector.Normalize();
        Exception zeroUnit = Assert.Throws<InvalidOperationException>(() => zero.ToVector().Normalize());
        Exception zeroUnitRepeat = Assert.Throws<InvalidOperationException>(() => zero.ToVector().Normalize());
        Assert.Equal(unit, valueVector.Normalize());
        Assert.Equal(zeroUnit.Message, zeroUnitRepeat.Message);
        Assert.Equal(before, Join(new[] { VertexState(value), VertexState(other), VertexState(zero) }));
        return Observation("V05",
            "native-route=Vertex.DistanceTo-plus-Vertex.ToVector-Cross/Dot/Length/Normalize",
            "cross=" + VectorState(cross),
            "distance=" + Double(distance),
            "dot=" + Double(dot),
            "norm=" + Double(norm),
            "unit=" + VectorState(unit),
            "zero-norm=" + Double(zero.ToVector().Length),
            ExceptionFact("zero-unit", zeroUnit),
            "zero-unit-repeat-exception-equal=true",
            "repeat-nonzero-unit-equal=true",
            "python-boundary=zero-unit-success/fresh-object-and-untyped-metric-inputs-not-claimed",
            "captured-source-state=value/other/zero-unchanged");
    }

    private static NativeObservation ObserveV06()
    {
        var p0 = new Vertex(0, 0, 0);
        var p1 = new Vertex(1, 0, 0);
        var p2 = new Vertex(0, 1, 0);
        double below = Math.BitDecrement(1e-15);
        double exact = 1e-15;
        double above = Math.BitIncrement(1e-15);
        Vertex[] probes = { new(1, 0, below), new(1, 0, exact), new(1, 0, above) };
        bool[] explicitTolerance = probes
            .Select(point => Vertex.AreCoplanar(new[] { p0, p1, p2, point }, 1e-15))
            .ToArray();
        bool[] explicitToleranceRepeat = probes
            .Select(point => Vertex.AreCoplanar(new[] { p0, p1, p2, point }, 1e-15))
            .ToArray();
        bool[] defaults = probes
            .Select(point => Vertex.AreCoplanar(new[] { p0, p1, p2, point }))
            .ToArray();
        bool empty = Vertex.AreCoplanar(Array.Empty<Vertex>());
        bool three = Vertex.AreCoplanar(new[] { p0, p1, new Vertex(0, 0, 9) });
        Exception nullInput = Assert.Throws<ArgumentNullException>(() => Vertex.AreCoplanar(null!));
        Assert.Equal(new[] { true, true, false }, explicitTolerance);
        Assert.Equal(explicitTolerance, explicitToleranceRepeat);
        Assert.Equal(new[] { true, true, true }, defaults);
        return Observation("V06",
            "native-route=Vertex.AreCoplanar(IEnumerable<Vertex>,distanceTolerance)",
            "probe-z=" + Join(new[] { Double(below), Double(exact), Double(above) }),
            "explicit-distance-tolerance-1e-15=" + Join(explicitTolerance.Select(Boolean)),
            "default-distance-tolerance-1e-7=" + Join(defaults.Select(Boolean)),
            "repeat-explicit-probes-equal=true",
            "empty=true",
            "three-point-shortcut=" + Boolean(three),
            ExceptionFact("null-enumerable", nullInput),
            "native-type-boundary=non-Vertex-elements-not-representable-in-IEnumerable<Vertex>",
            "python-boundary=first-triple-angular-threshold-and-error-timing-not-equated",
            "captured-source-state=input-vertices-are-readonly-values");
    }

    private static NativeObservation ObserveV07()
    {
        var p0 = new Vertex(0, 0, 0);
        var p1 = new Vertex(1, 0, 0);
        var p2 = new Vertex(2, 0, 0);
        var p3 = new Vertex(0, 1, 0);
        var p4 = new Vertex(0, 0, 1);
        Vertex[] collinearFirst = { p0, p1, p2, p3, p4 };
        Vertex[] noncollinearFirst = { p0, p1, p3, p2, p4 };
        string before = Join(collinearFirst.Select(VertexState));
        bool first = Vertex.AreCoplanar(collinearFirst);
        bool reordered = Vertex.AreCoplanar(noncollinearFirst);
        Assert.False(first);
        Assert.False(reordered);
        Assert.Equal(before, Join(collinearFirst.Select(VertexState)));
        return Observation("V07",
            "native-route=Vertex.AreCoplanar-robust-plane-search",
            "collinear-first-three-result=" + Boolean(first),
            "noncollinear-first-three-result=" + Boolean(reordered),
            "selected-two-orderings-results-equal=true",
            "native-plane-search=continues-past-collinear-first-three",
            "python-boundary=legacy-zero-normal-collinear-first-three-defect-not-preserved",
            "captured-source-state=five-input-vertices-unchanged");
    }

    private static NativeObservation ObserveS08()
    {
        Vertex[] source = VerticalRectangle();
        string sourceBefore = Join(source.Select(VertexState));
        Surface surface = SurfaceFor("S08", SurfaceType.Wall, source);
        string first = SurfaceScalarState(surface);
        string repeat = SurfaceScalarState(surface);
        source[0] = new Vertex(9, 9, 9);
        Assert.Equal(first, repeat);
        Assert.NotEqual(sourceBefore, Join(source.Select(VertexState)));
        Assert.Equal("0,0,0", VertexState(surface.Polygon.Vertices[0]));
        Assert.Equal(first, SurfaceScalarState(surface));
        return Observation("S08",
            "native-route=Surface+validated-PlanarPolygon-GrossArea/Center/Height/Normal/Type/Vertices",
            "scalar-state=" + first,
            "repeat-scalar-state-equal=true",
            "source-array-mutation-visible-to-surface=false",
            "polygon-vertices-interface=IReadOnlyList<Vertex>",
            "surface-type=Wall",
            "defensive-copy-evidence=source-array-element-replaced;observed-Polygon.Vertices-scalars-unchanged",
            "python-boundary=mutable-vertex-alias/object-identity-and-string-coercion-not-claimed",
            "captured-source-state=observed-GrossArea/Center/Height/Normal/Type/Polygon.Vertices-unchanged-after-source-array-element-replacement");
    }

    private static NativeObservation ObserveS09()
    {
        Vertex[] forwardInput = VerticalRectangle();
        Vertex[] reverseInput = forwardInput.Reverse().ToArray();
        Surface forward = SurfaceFor("S09F", SurfaceType.Wall, forwardInput);
        Surface reverse = SurfaceFor("S09R", SurfaceType.Wall, reverseInput);
        string before = SurfaceScalarState(forward) + "|" + SurfaceScalarState(reverse);
        double dot = forward.Normal.Dot(reverse.Normal);
        Assert.Equal(forward.GrossArea, reverse.GrossArea);
        Assert.Equal(forward.Center, reverse.Center);
        Assert.Equal(forward.Height, reverse.Height);
        Assert.Equal(-1d, dot, 12);
        Assert.Equal(before, SurfaceScalarState(forward) + "|" + SurfaceScalarState(reverse));
        return Observation("S09",
            "native-route=PlanarPolygon-full-loop-area-vector-and-reversed-winding",
            "forward=" + SurfaceScalarState(forward),
            "reverse=" + SurfaceScalarState(reverse),
            "areas-equal=true",
            "centers-equal=true",
            "heights-equal=true",
            "normal-dot=" + Double(dot),
            "python-boundary=shared-Vertex-object-identity-under-reversal-not-claimed-for-native-values",
            "captured-source-state=both-surfaces-unchanged-after-repeat-read");
    }

    private static NativeObservation ObserveS10()
    {
        Vertex[] input =
        {
            new(4, 4, 0), new(2, 2, 0), new(0, 4, 0), new(0, 0, 0), new(4, 0, 0),
        };
        string inputBefore = Join(input.Select(VertexState));
        Surface surface = SurfaceFor("S10", SurfaceType.Wall, input);
        string before = SurfaceScalarState(surface);
        Vector3 crossSum = Vector3.Zero;
        for (int index = 0; index < input.Length; index++)
        {
            crossSum += input[index].ToVector().Cross(input[(index + 1) % input.Length].ToVector());
        }

        double alignment = surface.Normal.Dot(crossSum.Normalize());
        Assert.True(surface.GrossArea > 0);
        Assert.True(alignment > 0);
        Assert.Equal(inputBefore, Join(input.Select(VertexState)));
        Assert.Equal(before, SurfaceScalarState(surface));
        return Observation("S10",
            "native-route=PlanarPolygon-full-loop-area-vector",
            "cross-sum=" + VectorState(crossSum),
            "gross-area=" + Double(surface.GrossArea),
            "normal=" + VectorState(surface.Normal),
            "normal-dot-normalized-cross-sum=" + Double(alignment),
            "native-area-is-positive=true",
            "native-normal-follows-full-loop=true",
            "repeat-scalar-state-equal=true",
            "python-boundary=first-turn-oriented-negative-area/opposed-normal-not-preserved",
            "captured-source-state=input-vertex-scalars-and-observed-GrossArea/Center/Height/Normal/Type/Polygon.Vertices-unchanged");
    }

    private static NativeObservation ObserveS11()
    {
        var invalid = new Dictionary<string, Vertex[]>(StringComparer.Ordinal)
        {
            ["collinear-triangle"] = new[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(2, 0, 0) },
            ["duplicate-closing-square"] = new[] { new Vertex(0, 0, 0), new Vertex(2, 0, 0), new Vertex(2, 2, 0), new Vertex(0, 2, 0), new Vertex(0, 0, 0) },
            ["self-intersecting-bow-tie"] = new[] { new Vertex(0, 0, 0), new Vertex(2, 2, 0), new Vertex(0, 2, 0), new Vertex(2, 0, 0) },
        };
        var facts = new List<string>
        {
            "native-route=PlanarPolygon.Validate-plus-constructor-rejection-before-Surface-construction",
        };
        foreach ((string name, Vertex[] vertices) in invalid)
        {
            string before = Join(vertices.Select(VertexState));
            ValidationResult validation = PlanarPolygon.Validate(vertices);
            Exception first = Assert.Throws<ArgumentException>(() => new PlanarPolygon(vertices));
            Exception second = Assert.Throws<ArgumentException>(() => new PlanarPolygon(vertices));
            Assert.False(validation.IsValid);
            Assert.Equal(first.Message, second.Message);
            Assert.Equal(before, Join(vertices.Select(VertexState)));
            facts.Add(name + "-diagnostics=" + Join(validation.Diagnostics.Select(item => item.Code)));
            facts.Add(ExceptionFact(name + "-first", first));
            facts.Add(name + "-repeat-exception-equal=true");
        }

        facts.Add("native-type-boundary=foreign-non-Vertex-polygon-elements-not-representable-in-IEnumerable<Vertex>");
        facts.Add("python-boundary=accepted-degenerate/duplicate-closing/self-intersecting-polygons-have-no-native-Surface-instance");
        facts.Add("captured-source-state=all-three-input-vertex-arrays-unchanged-after-validation/rejection");
        return Observation("S11", facts.ToArray());
    }

    private static NativeObservation ObserveS12()
    {
        Vertex[] source =
        {
            new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(0, 4, 0),
        };
        Surface original = SurfaceFor("S12", SurfaceType.Wall, source);
        string originalBefore = SurfaceScalarState(original);
        source[0] = new Vertex(1, 0, 0);
        Assert.Equal(originalBefore, SurfaceScalarState(original));

        var replacementPolygon = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, 0), new Vertex(3, 0, 0), new Vertex(3, 2, 0), new Vertex(0, 2, 0),
        });
        Surface replacement = original.WithPolygon(replacementPolygon);
        Surface floor = new(original.Id, original.Name, SurfaceType.Floor, original.Construction,
            original.Boundary, replacement.Polygon, original.Openings, original.Provenance);
        Exception invalidType = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Surface(original.Id, original.Name, (SurfaceType)99, original.Construction,
                original.Boundary, replacement.Polygon, original.Openings, original.Provenance));
        Assert.NotSame(original, replacement);
        Assert.Equal(originalBefore, SurfaceScalarState(original));
        Assert.Equal(SurfaceType.Floor, floor.Type);
        return Observation("S12",
            "native-route=Surface-immutable-properties-plus-WithPolygon-and-validated-reconstruction",
            "original-before=" + originalBefore,
            "original-after-source-array-replacement=" + SurfaceScalarState(original),
            "replacement=" + SurfaceScalarState(replacement),
            "floor-copy-type=Floor",
            "with-polygon-fresh-surface=true",
            ExceptionFact("undefined-enum-type", invalidType),
            "native-alias-boundary=source-array/replacement-array-elements-are-value-copied-and-properties-have-no-setters",
            "python-boundary=post-construction-vertex/type-mutation-and-alias-propagation-not-preserved",
            "captured-source-state=original-observed-GrossArea/Center/Height/Normal/Type/Polygon.Vertices-unchanged;Id/Name/Construction/Boundary/Openings/Provenance-excluded;replacement-and-floor-are-distinct-validated-instances");
    }

    private static NativeObservation ObserveT13()
    {
        SurfaceType[] members = Enum.GetValues<SurfaceType>();
        SurfaceType[] repeat = Enum.GetValues<SurfaceType>();
        string names = Join(members.Select(item => item.ToString()));
        int[] values = members.Select(item => (int)item).ToArray();
        Assert.Equal(new[] { SurfaceType.Wall, SurfaceType.Ceiling, SurfaceType.Floor }, members);
        Assert.Equal(members, repeat);
        Assert.Equal(new[] { 0, 1, 2 }, values);
        return Observation("T13",
            "native-route=SurfaceType-enum-members",
            "definition-order=" + names,
            "underlying-values=" + Join(values.Select(item => item.ToString(CultureInfo.InvariantCulture))),
            "direct-mapping-CEILING=Ceiling",
            "direct-mapping-FLOOR=Floor",
            "direct-mapping-WALL=Wall",
            "equivalent-member-count=3",
            "repeat-enumeration-equal=true",
            "native-enum-is-string-subclass=false",
            "python-boundary=lowercase-string-enum/equality-to-raw-string/constructor-round-trip-topology-not-equated",
            "captured-source-state=enum-definition-is-static");
    }

    private static NativeObservation ObserveT14()
    {
        bool lowercaseWall = Enum.TryParse("wall", ignoreCase: false, out SurfaceType lowercase);
        bool titleWall = Enum.TryParse("Wall", ignoreCase: false, out SurfaceType title);
        bool unknown = Enum.TryParse("roof", ignoreCase: false, out SurfaceType unknownValue);
        SurfaceType integerCast = (SurfaceType)1;
        Assert.False(lowercaseWall);
        Assert.True(titleWall);
        Assert.Equal(SurfaceType.Wall, title);
        Assert.False(unknown);
        Assert.Equal(SurfaceType.Ceiling, integerCast);
        return Observation("T14",
            "native-route=Enum.TryParse-case-sensitive-plus-defined-underlying-integer-cast",
            "parse-lowercase-wall-success=" + Boolean(lowercaseWall),
            "parse-title-Wall-success=" + Boolean(titleWall),
            "parse-title-Wall-member=" + title,
            "parse-unknown-roof-success=" + Boolean(unknown),
            "parse-unknown-default-value=" + unknownValue,
            "integer-one-defined=" + Boolean(Enum.IsDefined(integerCast)),
            "integer-one-member=" + integerCast,
            "python-boundary=lowercase-value-construction/title-case-rejection/integer-rejection-topology-differs",
            "captured-source-state=enum-definition-is-static");
    }

    private static NativeObservation Observation(string scenario, params string[] facts)
    {
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, item => Assert.False(string.IsNullOrWhiteSpace(item)));
        string hash = CanonicalSha256(JsonSerializer.SerializeToElement(facts));
        return new NativeObservation(scenario, facts, hash);
    }

    private static object CreateReceipt(TargetBinding target, IReadOnlyList<NativeObservation> observations) => new
    {
        assertion_id = target.AssertionId,
        adaptation_id = target.AdaptationId,
        classification = target.Classification,
        target_symbol = target.Symbol,
        native_target = target.NativeTarget,
        native_implementation = NativeImplementationFor(target.Symbol),
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
            exact_target_count = 31,
            equivalent_target_count = 3,
            exception_target_count = 28,
            native_direct_analogue = HasDirectNativeAnalogue(target.Symbol),
            source_state_policy = "only-explicit-captured-source-state-facts-are-claimed;absence-means-no-source-state-claim",
            numeric_policy = "finite-double-native-results-only;Python-nonfinite/arbitrary-precision/bool-runtime-values-not-normalized-or-claimed",
            opening_adjacency_targets_not_retargeted = new[]
            {
                "Surface.__init__", "Surface.blinded_window", "Surface.boundary", "Surface.get_subsurface",
                "SurfaceBoundaryCondition", "Window", "Door", "Blind", "Shade", "Shading",
            },
            out_of_scope_symbols_not_retargeted = new[]
            {
                "Surface.__repr__", "Surface.__str__", "Vertex.__eq__", "Vertex.__repr__", "Vertex.__str__",
            },
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

    private static string NativeImplementationFor(string symbol) => symbol switch
    {
        "Surface" => "Dragons.InvisibleDragon.Shape.Surface constructor; Dragons.InvisibleDragon.Shape.PlanarPolygon constructor",
        "Surface.area" => "Dragons.InvisibleDragon.Shape.Surface.GrossArea; Dragons.InvisibleDragon.Shape.PlanarPolygon.Area",
        "Surface.center" => "Dragons.InvisibleDragon.Shape.Surface.Center; Dragons.InvisibleDragon.Shape.PlanarPolygon.Centroid",
        "Surface.height" => "Dragons.InvisibleDragon.Shape.Surface.Height; Dragons.InvisibleDragon.Shape.PlanarPolygon.Height",
        "Surface.normal" => "Dragons.InvisibleDragon.Shape.Surface.Normal; Dragons.InvisibleDragon.Shape.PlanarPolygon.Normal",
        "Surface.type" => "Dragons.InvisibleDragon.Shape.Surface.Type",
        "Surface.vertex" => "Dragons.InvisibleDragon.Shape.Surface.Polygon; Dragons.InvisibleDragon.Shape.PlanarPolygon.Vertices",
        "SurfaceType" => "Dragons.InvisibleDragon.Shape.SurfaceType",
        "SurfaceType.CEILING" => "Dragons.InvisibleDragon.Shape.SurfaceType.Ceiling",
        "SurfaceType.FLOOR" => "Dragons.InvisibleDragon.Shape.SurfaceType.Floor",
        "SurfaceType.WALL" => "Dragons.InvisibleDragon.Shape.SurfaceType.Wall",
        "SurfaceType.__str__" => "no direct lowercase override; System.Enum.ToString is observed",
        "Vertex" => "Dragons.InvisibleDragon.Shape.Vertex readonly struct",
        "Vertex.__add__" => "Dragons.InvisibleDragon.Shape.Vertex.op_Addition(Vertex,Vector3)",
        "Vertex.__deepcopy__" => "no direct method; Dragons.InvisibleDragon.Shape.Vertex value-copy semantics",
        "Vertex.__init__" => "Dragons.InvisibleDragon.Shape.Vertex constructor(double,double,double)",
        "Vertex.__iter__" => "no direct iterator; Dragons.InvisibleDragon.Shape.Vertex.X/Y/Z explicit projection",
        "Vertex.__mul__" => "Dragons.InvisibleDragon.Shape.Vector3.op_Multiply(Vector3,double) after Vertex.ToVector",
        "Vertex.__radd__" => "no direct method; Vertex.Origin + Vertex.ToVector identity adaptation",
        "Vertex.__rmul__" => "Dragons.InvisibleDragon.Shape.Vector3.op_Multiply(double,Vector3) after Vertex.ToVector",
        "Vertex.__sub__" => "Dragons.InvisibleDragon.Shape.Vertex.op_Subtraction(Vertex,Vertex)",
        "Vertex.__truediv__" => "Dragons.InvisibleDragon.Shape.Vector3.op_Division(Vector3,double) after Vertex.ToVector",
        "Vertex.are_coplanar" => "Dragons.InvisibleDragon.Shape.Vertex.AreCoplanar(IEnumerable<Vertex>,double)",
        "Vertex.cross" => "Dragons.InvisibleDragon.Shape.Vector3.Cross after Vertex.ToVector",
        "Vertex.distance" => "Dragons.InvisibleDragon.Shape.Vertex.DistanceTo",
        "Vertex.dot" => "Dragons.InvisibleDragon.Shape.Vector3.Dot after Vertex.ToVector",
        "Vertex.norm" => "Dragons.InvisibleDragon.Shape.Vector3.Length after Vertex.ToVector",
        "Vertex.unit" => "Dragons.InvisibleDragon.Shape.Vector3.Normalize after Vertex.ToVector",
        "Vertex.x" => "Dragons.InvisibleDragon.Shape.Vertex.X",
        "Vertex.y" => "Dragons.InvisibleDragon.Shape.Vertex.Y",
        "Vertex.z" => "Dragons.InvisibleDragon.Shape.Vertex.Z",
        _ => throw new Xunit.Sdk.XunitException($"No native implementation for '{symbol}'."),
    };

    private static bool HasDirectNativeAnalogue(string symbol) => symbol is not
        ("SurfaceType.__str__" or "Vertex.__deepcopy__" or "Vertex.__iter__" or "Vertex.__radd__");

    private static string[] UnresolvedFor(TargetBinding target)
    {
        if (target.Classification == "equivalent")
        {
            return Array.Empty<string>();
        }

        var values = new List<string>
        {
            target.AdaptationId + "-Python-behavior-outside-bounded-native-counterpart",
        };
        if (target.Symbol is "Vertex" or "Vertex.__init__" or "Vertex.x" or "Vertex.y" or "Vertex.z")
        {
            values.Add("Python-mutable/nonfinite/bool-preserving/arbitrary-precision-coordinate-domain");
        }

        if (target.Symbol is "Vertex.__add__" or "Vertex.__mul__" or "Vertex.__radd__" or "Vertex.__rmul__"
            or "Vertex.__sub__" or "Vertex.__truediv__")
        {
            values.Add("Python-dynamic-foreign-operand-protocols-and-error-message/timing");
        }

        if (target.Symbol == "Vertex.unit")
        {
            values.Add("Python-zero-vector-unit-success-and-fresh-object-identity");
        }

        if (target.Symbol == "Vertex.are_coplanar")
        {
            values.Add("Python-first-triple-angular-test-and-collinear-first-three-defect");
        }

        if (target.Symbol.StartsWith("Surface", StringComparison.Ordinal) && !target.Symbol.StartsWith("SurfaceType", StringComparison.Ordinal))
        {
            values.Add("Python-mutable-aliased-Surface-state-and-post-construction-invalidity");
        }

        if (target.Symbol is "SurfaceType" or "SurfaceType.__str__")
        {
            values.Add("Python-lowercase-str-enum-construction/equality/error-topology");
        }

        return values.ToArray();
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
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal(target.Classification, RequiredString(receipt, "classification"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));
        Assert.Equal(target.NativeTarget, RequiredString(receipt, "native_target"));
        Assert.Equal(NativeImplementationFor(target.Symbol), RequiredString(receipt, "native_implementation"));
        AssertReceiptFields(receipt.GetProperty("source_receipt"), target, includeIndex: true);
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
        Assert.Equal(31, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(3, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(28, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(HasDirectNativeAnalogue(target.Symbol), scope.GetProperty("native_direct_analogue").GetBoolean());
        AssertStringArray(scope.GetProperty("unresolved_behavior"), UnresolvedFor(target));
        string[] outOfScope = scope.GetProperty("out_of_scope_symbols_not_retargeted")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.DoesNotContain(target.Symbol, outOfScope);
        string[] opening = scope.GetProperty("opening_adjacency_targets_not_retargeted")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.DoesNotContain(target.Symbol, opening);
    }

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        byte_length = bytes,
        path,
        sha256,
    };

    private static Surface SurfaceFor(string suffix, SurfaceType type, IEnumerable<Vertex> vertices) => new(
        new EntityId("GEOMETRY-CORE-" + suffix),
        "Geometry Core " + suffix,
        type,
        new NoMassConstruction("Geometry Core Construction", 2.5),
        SurfaceBoundary.Outdoors,
        new PlanarPolygon(vertices));

    private static Vertex[] VerticalRectangle() => new[]
    {
        new Vertex(0, 0, 0),
        new Vertex(4, 0, 0),
        new Vertex(4, 0, 3),
        new Vertex(0, 0, 3),
    };

    private static string SurfaceScalarState(Surface value) => Join(new[]
    {
        "area=" + Double(value.GrossArea),
        "center=" + VertexState(value.Center),
        "height=" + Double(value.Height),
        "normal=" + VectorState(value.Normal),
        "type=" + value.Type,
        "vertices=" + Join(value.Polygon.Vertices.Select(VertexState)),
    });

    private static string VertexState(Vertex value) =>
        Double(value.X) + "," + Double(value.Y) + "," + Double(value.Z);

    private static string VectorState(Vector3 value) =>
        Double(value.X) + "," + Double(value.Y) + "," + Double(value.Z);

    private static string ExceptionFact(string phase, Exception exception)
    {
        string parameter = exception is ArgumentException argument
            ? argument.ParamName ?? "none"
            : "not-applicable";
        return phase + "=" + exception.GetType().Name + "|param=" + parameter;
    }

    private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

    private static string[] V04DeclaredOperatorSignatures(Type type)
    {
        string[] declared = type == typeof(Vertex)
            ? new[] { "op_Addition(Vertex,Vector3)" }
            : type == typeof(Vector3)
                ? new[]
                {
                    "op_Division(Vector3,Double)",
                    "op_Multiply(Double,Vector3)",
                    "op_Multiply(Vector3,Double)",
                }
                : throw new Xunit.Sdk.XunitException($"V04 has no declared operator routes on '{type.FullName}'.");
        string[] actual = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(item => item.IsSpecialName && item.Name.StartsWith("op_", StringComparison.Ordinal))
            .Select(item => item.Name + "(" + string.Join(",", item.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")")
            .ToArray();
        Assert.All(declared, item => Assert.Contains(item, actual));
        return declared.OrderBy(item => item, StringComparer.Ordinal).ToArray();
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
        string Subfamily,
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
        string Classification,
        string NativeTarget,
        int[] CaseIndices);

    private sealed record NativeObservation(string Scenario, string[] Facts, string FactsSha256);
}
