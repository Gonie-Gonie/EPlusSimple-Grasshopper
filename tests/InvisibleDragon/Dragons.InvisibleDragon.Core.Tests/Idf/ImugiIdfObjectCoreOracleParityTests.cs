#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.InvisibleDragon.Idf;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Idf;

public sealed class ImugiIdfObjectCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/imugi-idf-object-core-oracle.json";
    private const int FixtureBytes = 119_037;
    private const string FixtureSha256 =
        "sha256:61c137044af671cd9a1a935fea516b3d72eaa74f3d3c5122b3a61acef981cc93";
    private const string FixtureSchema =
        "dragons.python-reference.imugi-idf-object-core.v1";
    private const string FixtureRepositoryCommit = "aa53eda";
    private const string CasesSha256 =
        "sha256:703852d0899ab2e6baef64a49a13551420804ae7bbad92728a74f326a1e544d1";
    private const string TargetReceiptsSha256 =
        "sha256:b7cf5615507de3309fc1d8429390216b1920764ef910200f2559c8e187ea3b94";

    private const string GeneratorPath =
        "tools/python-reference/generate_imugi_idf_object_core_oracle.py";
    private const int GeneratorBytes = 30_077;
    private const string GeneratorSha256 =
        "sha256:3e87aaf0501d1176ab1ffb2be07710d1c8e6c58ef061101b4a70b14eb6f8b7f7";
    private const string ValidatorPath =
        "tests/PythonReference/test_imugi_idf_object_core_oracle.py";
    private const int ValidatorBytes = 11_109;
    private const string ValidatorSha256 =
        "sha256:054a927afa780027119b67634e6b84196404160ac23dd9b10c99049444b16a25";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/imugi.py";
    private const int UpstreamBytes = 91_815;
    private const string UpstreamSourceSha256 =
        "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613";
    private const string UpstreamAstSha256 =
        "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Idf.ImugiIdfObjectCoreOracleParityTests.MatchesPinnedImugiIdfObjectThroughPublicProductionApis";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs", 13_173,
            "sha256:0d16e28d37136a3aa0015759ead7ee324cfed08cff1a3269326d4af144518048"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfParser.cs", 6_031,
            "sha256:30d9ad1e84f55ff3a62180c4a3be1d60d37f09ee24f03e37d9c8dd8fd7003b1c"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfWriter.cs", 4_280,
            "sha256:c7b98b6eed298687fca229ae7262ffdf2494953b3cc6576835cacbcc47cf998a"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "imugi-idf-object-core.idf-construction-and-properties", 5,
            "sha256:f002a89574b46c2a90f44a59eddea0b930a2bcb8613111d31ec09068f5a4a414",
            "sha256:1e6778325aa3e425f7c8a4d9bc6e21b9a9d9db3c8eb21eaad1ffeef35066c4d8"),
        new("A02", "imugi-idf-object-core.idf-text-append-and-validity", 3,
            "sha256:fbf30d45ff890ac30b4d11c2891d46fa38b9b2826816255bd084e57b067fac90",
            "sha256:223305616ffe25b10756ef7a5de922e771282114b3757f558ac73276683f0e7f"),
        new("A03", "imugi-idf-object-core.idf-read-and-write", 2,
            "sha256:b6cb09fcf092b7e107b93a7f4710cade1b542636ec282a2466b36e133f0c5872",
            "sha256:8ad8bde738a9b855393561c66150f855cb015c9f0ea42a144a13e0bf00cfaac1"),
        new("A04", "imugi-idf-object-core.idf-run-signature", 1,
            "sha256:0a43be3847e54fcfd9985f150b0af5541c7078ee96c2690214363f7dcaed33a8",
            "sha256:096017556ee6c744bdb66767bb3f57d1c7c52ee7af6df6df190cb66c768113a8"),
        new("B01", "imugi-idf-object-core.idf-object-construction-indexing-and-text", 5,
            "sha256:f5f3aa52012a91758af292099ad8c8d137752b3484f80cba1663f3dc6720a802",
            "sha256:a7286a586fc9405231883776e22fa7f32e6c2e18f78ef4409cd769e438403b85"),
        new("B02", "imugi-idf-object-core.idf-object-validation-and-choices", 4,
            "sha256:319a7e8ad0efc26303e657747157a3512f5a84cb8ca4a0af06b5ca8966ce5ec7",
            "sha256:a6162af6ebf81b3b9d2d015090810f37098340fccf836e44823541c42a0566a1"),
        new("B03", "imugi-idf-object-core.idf-object-relationships-and-rename", 5,
            "sha256:fe98fe37c75c289d5c6e6cb28c35206027474a4d9bef965d1c2ff0428e9218d6",
            "sha256:f7fedb8dab3e50ccaf7fac294548f0643b66c53f813ac9b84812d39a2a1290c8"),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        T(1108, "IDF", 0), T(1109, "IDF.__init__", 0),
        T(1112, "IDF.__str__", 1), T(1113, "IDF.append", 1), T(1114, "IDF.check_validity", 1),
        T(1115, "IDF.default_filename", 0), T(1116, "IDF.idd", 0),
        T(1118, "IDF.read_idf", 2), T(1119, "IDF.run", 3), T(1121, "IDF.version", 0), T(1122, "IDF.write", 2),
        T(1167, "IdfObject", 4), T(1170, "IdfObject.__getitem__", 4), T(1171, "IdfObject.__init__", 4),
        T(1173, "IdfObject.__setitem__", 4), T(1174, "IdfObject.__str__", 4),
        T(1175, "IdfObject.check_field_validity", 5), T(1176, "IdfObject.check_validity", 5),
        T(1177, "IdfObject.choices", 5), T(1178, "IdfObject.ensure_validity", 5),
        T(1179, "IdfObject.grandparent", 6), T(1180, "IdfObject.has_parent", 6),
        T(1181, "IdfObject.idd", 6), T(1182, "IdfObject.parent", 6), T(1183, "IdfObject.rename", 6),
    };

    private static readonly HashSet<int> EquivalentIndices = new() { 1112, 1113, 1118, 1170, 1174, 1181 };
    private static bool DiscoverPins => false;
    private static readonly NativePin[] ExpectedNativePins =
    {
        new("A01", 10, "sha256:8822610926b562b1f4f54677bd3a524145fb309cf84be12d3fb088dde5c3af9c"),
        new("A02", 8, "sha256:30ae57f51d25303649a863d336d6b4cd85eb16242b1995bbfde2044302e3a2b1"),
        new("A03", 7, "sha256:853c71f96692bd43dfe66c6d2ebad0e83e6b14f205409d26b77c9388ebedc5ef"),
        new("A04", 6, "sha256:3f770308dd7b81e810e973f1f6c13d94049caaa170058aceae5f4416e84dd7c1"),
        new("B01", 9, "sha256:84842eb083b5008b0046fb87f98684d7d7e7f5e5ce1991459bbc2696c0a4b94f"),
        new("B02", 8, "sha256:c999cbd00cfd9ddb216621ca43b05badbe9333e9f85870a6d10678ca66aaace1"),
        new("B03", 8, "sha256:729e03dbdd13f59171ec3c9ad2190cff457350440ba9cb26b60b1f7a952f9849"),
    };
    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:ae6fcd8b57bb8f5927012d931e87c0bca2bc1a639d3c00057cc1e28b07e75f13", // 1108 IDF
        "sha256:7c862fae6eacb6a649b46691b703ff63afc8c1e598fed0b93e6d6cbd38344a5a", // 1109 IDF.__init__
        "sha256:21eacb997947460f8b8699131e7aac9ba1f078dc28399c79e10588160ef13fa9", // 1112 IDF.__str__
        "sha256:4d089ea32d64d49ad080da577a7ffd293e4dff907d381156c19acda3df469076", // 1113 IDF.append
        "sha256:68761ea531e92eb4ee37aa62ec27319c8a5f4413ad318f05a8437448424039c7", // 1114 IDF.check_validity
        "sha256:9bdc5faae90ac8455267ed1573b5a4e03a95706d4b68365916b45f498ba755bf", // 1115 IDF.default_filename
        "sha256:71590d33595e816e4bb3dd63e981b46fca3ab8014bcda0dbd217f6102706ff0c", // 1116 IDF.idd
        "sha256:c9d823e4828a7d810442abfce9783e879d8a2ede6d4b2f1abe1a36e28940edc0", // 1118 IDF.read_idf
        "sha256:b27d664a0c487814e80bbc802d2f8a1a1853707157618a07111984efb48f1f18", // 1119 IDF.run
        "sha256:b072c416d19e96fe28011650f33fa37ccdc666288ae143d5f0b060ebe7e14cbc", // 1121 IDF.version
        "sha256:0b630631659915e3bc1e7dfee75d728d011ed51e7a5da31817fafecce58c2a1f", // 1122 IDF.write
        "sha256:022a270cf8ecc007eed21bd7c021a2722f25123f75bb1ab0e8ad5f86f4d29aa6", // 1167 IdfObject
        "sha256:13b8b15724fd7021048db448e0e10ee78873322dfa756d9348d7adc85d68f86e", // 1170 IdfObject.__getitem__
        "sha256:a9d763d1c076398346dc5e56c77d9b8d788c2a8821f66341257f0b201992c1a3", // 1171 IdfObject.__init__
        "sha256:984e1def1faf1ce77f4bd1ac14641948f26ba92468433c159c3379445c7e5f41", // 1173 IdfObject.__setitem__
        "sha256:8586f7746c94e0dc8571d862fde29d2cc567fc85d12a3eeb354910e5c37dd5e3", // 1174 IdfObject.__str__
        "sha256:6d7e3f11039c6cf01d476913680c70531ee4dddbc675ab277c5a03fea0fd7a4c", // 1175 IdfObject.check_field_validity
        "sha256:0d60b930a75a1448111cd1c6edab4f0ff629afacc376ac54519fd885f8ab6a4d", // 1176 IdfObject.check_validity
        "sha256:92b108ec6835c2d3fbc17aa5bd9a385db3cf2e38cfc6c3780ac2ad60936273e7", // 1177 IdfObject.choices
        "sha256:9c00ef30e358a2bdf47c0ef98653e1a6c993c0086a594451194e3034d7eaeae2", // 1178 IdfObject.ensure_validity
        "sha256:6039ac24d4451aaa0f44cb75001b9ecb31815a7599cf45c08f5fa98786bcaddb", // 1179 IdfObject.grandparent
        "sha256:d3596afd037368b61b421552739440c0c2a67f3b6127dd6f522cda875af28c04", // 1180 IdfObject.has_parent
        "sha256:401c2a6a5aaad25bf65bfb898066176eda044c2b0ad8e1b60c1242856dce593b", // 1181 IdfObject.idd
        "sha256:8d61e784bb170896b8b4586234af4fdb23e90c2b3b95b757a7a7c92d447c8a39", // 1182 IdfObject.parent
        "sha256:4433f025d198852b728ea636c83ad42677ef1fc83d360e4cfce1fa93f4b0a80c", // 1183 IdfObject.rename
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:37f73b67f880feb5c425b4325e4681feab095870a4390b550dad855f47beca84", // imugi-idf-object-core-1108-9fa0a86a
        "sha256:0c421f4b1e2908209cee595e2a4ae08b12510dcd7c583959082ef89d6e145990", // imugi-idf-object-core-1109-a0cef595
        "sha256:ef912d1949838f2a9a37f52554c462d19a9af34a4b7f9d9e0cf7c4552b95b915", // imugi-idf-object-core-1112-641b4167
        "sha256:c19c51083eb58cae86939278eadf85054229ed1dcf5c5732721e587d01d9dec2", // imugi-idf-object-core-1113-1114979d
        "sha256:2f278d911e13f721aac8c36e6cf24bfffa55c296ffb76a3f81eb6197e5ffa083", // imugi-idf-object-core-1114-ca419512
        "sha256:cc0580315c5d0202797c9282474d1a2d0e516e143cca2b2d204bf2d4819f184e", // imugi-idf-object-core-1115-019279b8
        "sha256:f911d17d074019444fcafc95d40fb7dcabd6ebe6b79f657ece5f073de4654213", // imugi-idf-object-core-1116-d7e9a2d3
        "sha256:bb860b2e0c837ab247c2f50d2b3efbefecdcd92c2ceb08c8405eafe3a79ee29a", // imugi-idf-object-core-1118-78c83655
        "sha256:7119c61e5f99bc0e63b63d439a747af54edae1e1cb18bfb74c0638386bd6ea4a", // imugi-idf-object-core-1119-bcd90001
        "sha256:83ca580ff88744b0321393dcb261ff0baab25569f4e89a8eeaf6e250151856ea", // imugi-idf-object-core-1121-013ee380
        "sha256:bf5cce41c607694905191171f9e37424ca967ce5c19f16204f80118b0748c72e", // imugi-idf-object-core-1122-d79138b0
        "sha256:56fac9a1d21d8ea656fc74c3ed986d9278323226388bbf1edc07e3617bd50daa", // imugi-idf-object-core-1167-57c5d21a
        "sha256:d060a506f7b4336e0625162c2bef1f9ac7eabad327c47306674180841b215271", // imugi-idf-object-core-1170-7799d464
        "sha256:6b58709d339ce2541749a0c0f31e484eea0c9c9954902ab11fda99345db6c516", // imugi-idf-object-core-1171-1f86dc75
        "sha256:69ada4d9f3ffc8478f379b2ddb602cfbd20cf46dcb7ff81a877bbc8dcaa94c10", // imugi-idf-object-core-1173-d94a9b97
        "sha256:41f425aca1176b9b15b2d77825111c2e715e3efb435fb5355d835c760887c1e2", // imugi-idf-object-core-1174-f978bc66
        "sha256:9e73bd52570357feafc2fe6365d5473243d82bcb9c147f723fee58d09ab2e988", // imugi-idf-object-core-1175-8eee76b2
        "sha256:64fc2a11af4106d7f70865b6729df5ee645a1e210db3d6dc0f1d864426f6e6bf", // imugi-idf-object-core-1176-2046c730
        "sha256:e004298c8a95a53c940395cdbb6adf4624397e3f1c734ab3b1427f0d19501583", // imugi-idf-object-core-1177-823cd22d
        "sha256:4089160bd730d7cae01f3860015c44457c625f3a9e03f3fa2eb802c770de514d", // imugi-idf-object-core-1178-a24d2160
        "sha256:63060e8dcbdff398f969f9e5424392f7b2105d41d4dbd9a49797476f04371eb1", // imugi-idf-object-core-1179-9ee8bea6
        "sha256:bb264824a244bd7434c9927f8f5b3573f9f7f7b1e7ae60d8414a8b6225bc0db9", // imugi-idf-object-core-1180-efacd493
        "sha256:7bb39b67d67d9f5519a4384c9ce9bd9511baafe8db7ecfe730735cbe009770b4", // imugi-idf-object-core-1181-37692960
        "sha256:d8f083e0f1ec1ac91308c5b71ba843978f8cb0cdfc686a23f201046e16089d5d", // imugi-idf-object-core-1182-8f974416
        "sha256:7214a919f63cff267c660459ed7bbc192304eccd9c93609795d9529af3c53df8", // imugi-idf-object-core-1183-8dfd0cfa
    };

    [Fact]
    public void MatchesPinnedImugiIdfObjectThroughPublicProductionApis()
    {
        ValidateArtifactsAndPublicApis();
        using JsonDocument fixture = ReadFixture();
        OracleCorpus corpus = ValidateFixture(fixture.RootElement);
        NativeObservation[] observations = Cases.Select(Observe).ToArray();
        object[] receipts = corpus.Targets.Select(target => Receipt(target, observations)).ToArray();
        string[] hashes = receipts.Select(item => CanonicalSha256(JsonSerializer.SerializeToElement(item))).ToArray();
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
            string native = string.Join(Environment.NewLine, observations.Select(item =>
                $"        new(\"{item.Code}\", {item.Facts.Length}, \"{item.FactsSha256}\"),"));
            string receipt = string.Join(Environment.NewLine, corpus.Targets.Select((item, index) =>
                $"        \"{hashes[index]}\", // {item.InventoryIndex} {item.Symbol}"));
            throw new Xunit.Sdk.XunitException("IMUGI_IDF_OBJECT_NATIVE_PINS\n" + native + "\n" + receipt);
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].Code, observations[index].Code);
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }
        Assert.Equal(ExpectedReceiptHashes, hashes);
        Assert.Equal(ExpectedCollectorOutputHashes, collectorOutputHashes);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach ((TargetBinding target, object receipt) in corpus.Targets.Zip(receipts))
        {
            Assert.True(ids.Add(target.AssertionId));
            TrustedEvidenceRecorder.Record(target.AssertionId, EvidenceTestCase, "not_applicable", receipt);
        }
        Assert.Equal(25, ids.Count);
        Assert.Equal(6, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(19, corpus.Targets.Count(item => item.Classification == "exception"));
    }

    private static ExpectedTarget T(int index, string symbol, int caseIndex) => new(index, symbol, Cases[caseIndex].CaseId);

    private static void ValidateArtifactsAndPublicApis()
    {
        AssertArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin pin in NativeSources) AssertArtifact(pin.Path, pin.Bytes, pin.Sha256);

        Assert.True(typeof(IdfDocument).IsSealed);
        Assert.True(typeof(IdfObject).IsSealed);
        Assert.NotEmpty(typeof(IdfDocument).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(2, typeof(IdfObject).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
        AssertPublicMethod(typeof(IdfDocument), nameof(IdfDocument.Append));
        AssertPublicMethod(typeof(IdfParser), nameof(IdfParser.ParseFile));
        AssertPublicMethod(typeof(IdfWriter), nameof(IdfWriter.Write));
        AssertPublicMethod(typeof(IdfWriter), nameof(IdfWriter.WriteFile));
        Assert.DoesNotContain(typeof(IdfDocument).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "Add");
        Assert.DoesNotContain(typeof(IdfDocument).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "Run");
    }

    private static JsonDocument ReadFixture()
    {
        byte[] bytes = File.ReadAllBytes(Find(FixturePath));
        Assert.Equal(FixtureBytes, bytes.Length);
        Assert.Equal(FixtureSha256, Sha256(bytes));
        return JsonDocument.Parse(bytes);
    }

    private static OracleCorpus ValidateFixture(JsonElement root)
    {
        AssertNoHostPaths(root);
        Assert.Equal(FixtureSchema, S(root, "schema"));
        Assert.Equal(CasesSha256, S(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        JsonElement[] actualCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, actualCases.Length);
        for (int index = 0; index < Cases.Length; index++)
        {
            CaseBinding expected = Cases[index];
            JsonElement actual = actualCases[index];
            Assert.Equal(expected.Code, S(actual, "code"));
            Assert.Equal(expected.CaseId, S(actual, "id"));
            Assert.Equal(expected.TargetCount, actual.GetProperty("target_symbols").GetArrayLength());
            Assert.Equal(expected.CaseSha256, S(root.GetProperty("case_sha256"), expected.CaseId));
            Assert.Equal(expected.PythonFactsSha256, S(actual.GetProperty("python"), "facts_sha256"));
        }

        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.Equal(6, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());
        Assert.Equal(19, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        JsonElement evidence = contract.GetProperty("evidence_contract");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.False(evidence.GetProperty("python_api_or_source_compatibility_claim").GetBoolean());
        Assert.False(evidence.GetProperty("structural_only").GetBoolean());
        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("exact_disjoint_source_partition").GetBoolean());
        Assert.Equal(133, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(25, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(TargetReceiptsSha256, S(closure.GetProperty("partition_receipts_sha256"), "target"));

        JsonElement targetsElement = root.GetProperty("target_receipts");
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(targetsElement));
        JsonElement[] actualTargets = targetsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, actualTargets.Length);
        var targets = new TargetBinding[actualTargets.Length];
        for (int index = 0; index < actualTargets.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement actual = actualTargets[index];
            Assert.Equal(expected.InventoryIndex, actual.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, S(actual, "symbol"));
            string classification = EquivalentIndices.Contains(expected.InventoryIndex) ? "equivalent" : "exception";
            string route = Route(expected.Symbol);
            Assert.Equal(classification, S(contract.GetProperty("classifications"), expected.Symbol));
            Assert.Equal(route, S(contract.GetProperty("native_routes"), expected.Symbol));
            targets[index] = new TargetBinding(
                expected.InventoryIndex, expected.Symbol, S(actual, "kind"), S(actual, "symbol_hash"),
                S(actual, "signature_hash"), S(actual, "body_hash"), classification,
                S(contract.GetProperty("assertion_ids"), expected.Symbol),
                S(contract.GetProperty("adaptations"), expected.Symbol), route, expected.CaseId);
        }
        Assert.Equal("Dragons.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)", Route("IDF.append"));

        JsonElement review = root.GetProperty("native_review");
        Assert.False(review.GetProperty("python_executes_native_runtime").GetBoolean());
        Assert.True(review.GetProperty("no_python_api_or_source_compatibility_claim").GetBoolean());
        Assert.True(review.GetProperty("public_production_routes_only").GetBoolean());
        JsonElement[] sources = review.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(4, sources.Length);
        for (int index = 0; index < NativeSources.Length; index++)
        {
            Assert.Equal(NativeSources[index].Path, S(sources[index], "path"));
            Assert.Equal(NativeSources[index].Bytes, sources[index].GetProperty("bytes").GetInt32());
            Assert.Equal(NativeSources[index].Sha256, S(sources[index], "sha256"));
        }
        return new OracleCorpus(targets);
    }

    private static string Route(string symbol) => symbol switch
    {
        "IDF.__str__" => "Dragons.InvisibleDragon.Idf.IdfWriter.Write(IdfDocument, IdfWriterOptions?)",
        "IDF.append" => "Dragons.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)",
        "IDF.read_idf" => "Dragons.InvisibleDragon.Idf.IdfParser.ParseFile(string, IddSchema?, Encoding?)",
        "IdfObject.__getitem__" => "Dragons.InvisibleDragon.Idf.IdfObject.this[int|string]",
        "IdfObject.__str__" => "Dragons.InvisibleDragon.Idf.IdfWriter.Write(IdfDocument, IdfWriterOptions?)",
        "IdfObject.idd" => "Dragons.InvisibleDragon.Idf.IdfObject.Definition",
        _ when symbol.StartsWith("IDF", StringComparison.Ordinal) =>
            "Dragons.InvisibleDragon.Idf.IdfDocument public production API (intentional adaptation; no Python source/API compatibility claim)",
        _ => "Dragons.InvisibleDragon.Idf.IdfObject public production API (intentional adaptation; no Python source/API compatibility claim)",
    };

    private static NativeObservation Observe(CaseBinding item)
    {
        string[] facts = item.Code switch
        {
            "A01" => ObserveDocumentConstruction(),
            "A02" => ObserveAppendAndWrite(),
            "A03" => ObserveParseAndWriteFile(),
            "A04" => ObserveRunAdaptation(),
            "B01" => ObserveObjectCore(),
            "B02" => ObserveObjectValidationAdaptation(),
            "B03" => ObserveObjectRelationships(),
            _ => throw new InvalidOperationException("Unknown case " + item.Code),
        };
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        return new NativeObservation(item.Code, item.CaseId, facts, CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveDocumentConstruction()
    {
        var document = new IdfDocument(
            preambleComments: new[] { "Oracle preamble" },
            trailingComments: new[] { "Oracle trailing" });
        return new[]
        {
            "public-type=" + document.GetType().FullName,
            "sealed=" + document.GetType().IsSealed,
            "implements-readonly-list=" + (document is IReadOnlyList<IdfObject>),
            "count=" + document.Count,
            "schema-null=" + (document.Schema is null),
            "energyplus-version-null=" + (document.EnergyPlusVersion is null),
            "preamble=" + string.Join("|", document.PreambleComments),
            "trailing=" + string.Join("|", document.TrailingComments),
            "default-filename-api=false",
            "python-api-source-compatibility-claim=false",
        };
    }

    private static string[] ObserveAppendAndWrite()
    {
        var document = new IdfDocument();
        var version = new IdfObject("Version", new[] { "24.2" });
        var building = new IdfObject("Building", new[] { "Oracle Building", "0", "Suburbs" });
        document.Append(version);
        document.Append(building);
        string first = IdfWriter.Write(document);
        string second = IdfWriter.Write(document);
        return new[]
        {
            "append-route=Dragons.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)",
            "count=" + document.Count,
            "type-order=" + string.Join("|", document.Select(value => value.ObjectType)),
            "version=" + document.EnergyPlusVersion,
            "typed-version-count=" + document["Version"].Count,
            "writer-deterministic=" + (first == second),
            "writer-sha256=" + Sha256(Encoding.UTF8.GetBytes(first)),
            "internal-route-claim=false",
        };
    }

    private static string[] ObserveParseAndWriteFile()
    {
        string text = "Version,24.2;\nBuilding,Oracle Building,0,Suburbs;\n";
        string directory = Path.Combine(Path.GetTempPath(), "dragons-imugi-idf-parity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "input.idf");
        string output = Path.Combine(directory, "output.idf");
        try
        {
            File.WriteAllText(input, text, new UTF8Encoding(false));
            IdfDocument document = IdfParser.ParseFile(input, encoding: new UTF8Encoding(false));
            IdfWriter.WriteFile(output, document, new IdfWriterOptions { NewLine = "\n" }, new UTF8Encoding(false));
            string written = File.ReadAllText(output, Encoding.UTF8);
            return new[]
            {
                "parse-file-route=Dragons.InvisibleDragon.Idf.IdfParser.ParseFile(string, IddSchema?, Encoding?)",
                "write-file-route=Dragons.InvisibleDragon.Idf.IdfWriter.WriteFile(string, IdfDocument, IdfWriterOptions?, Encoding?)",
                "parsed-count=" + document.Count,
                "parsed-type-order=" + string.Join("|", document.Select(value => value.ObjectType)),
                "written-sha256=" + Sha256(Encoding.UTF8.GetBytes(written)),
                "roundtrip-semantic-count=" + IdfParser.Parse(written).Count,
                "host-path-recorded=false",
            };
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static string[] ObserveRunAdaptation()
    {
        return new[]
        {
            "idf-document-run-method=false",
            "idf-parser-run-method=false",
            "idf-writer-run-method=false",
            "active-energyplus-process-claim=false",
            "python-api-source-compatibility-claim=false",
            "classification=exception",
        };
    }

    private static string[] ObserveObjectCore()
    {
        var item = new IdfObject("Oracle:Object", new[] { "Oracle Name", "On", "3.0" });
        string before = IdfWriter.Write(new IdfDocument(objects: new[] { item }));
        item[1] = "Off";
        item.Add("tail");
        string after = IdfWriter.Write(new IdfDocument(objects: new[] { item }));
        return new[]
        {
            "public-type=" + item.GetType().FullName,
            "sealed=" + item.GetType().IsSealed,
            "object-type=" + item.ObjectType,
            "name=" + item.Name,
            "getitem-index-zero=" + item[0],
            "setitem-index-one=" + item[1],
            "count-after-add=" + item.Count,
            "render-before-sha256=" + Sha256(Encoding.UTF8.GetBytes(before)),
            "render-after-sha256=" + Sha256(Encoding.UTF8.GetBytes(after)),
        };
    }

    private static string[] ObserveObjectValidationAdaptation()
    {
        var item = new IdfObject("Oracle:Object", new[] { "Name", "On" });
        Exception named = Assert.Throws<InvalidOperationException>(() => _ = item["Mode"]);
        item[4] = "expanded";
        return new[]
        {
            "definition-null=" + (item.Definition is null),
            "named-indexer-without-definition=" + named.GetType().Name,
            "numeric-indexer-expands-count=" + item.Count,
            "choice-api=false",
            "ensure-validity-property=false",
            "instance-check-validity-api=false",
            "field-validity-api=false",
            "typed-native-adaptation=true",
        };
    }

    private static string[] ObserveObjectRelationships()
    {
        var item = new IdfObject("Oracle:Object", new[] { "Name" });
        var document = new IdfDocument(objects: new[] { item });
        return new[]
        {
            "definition-null=" + (item.Definition is null),
            "document-contains-same-reference=" + ReferenceEquals(item, document[0]),
            "parent-property=false",
            "grandparent-property=false",
            "has-parent-property=false",
            "rename-method=false",
            "public-definition-route=Dragons.InvisibleDragon.Idf.IdfObject.Definition",
            "internal-route-claim=false",
        };
    }

    private static object Receipt(TargetBinding target, IReadOnlyList<NativeObservation> observations)
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
                equivalent_target_count = 6,
                exact_case_count = 7,
                exact_target_count = 25,
                exception_target_count = 19,
                fixture_repository_commit = FixtureRepositoryCommit,
                internal_native_route_claimed = false,
                public_production_routes_only = true,
                python_api_or_source_compatibility_claim = false,
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

    private static MethodInfo AssertPublicMethod(Type type, string name) => Assert.Single(
        type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
        method => method.Name == name);

    private static object Artifact(string path, int bytes, string sha256) => new { bytes, path, sha256 };

    private static void AssertArtifact(string path, int bytes, string sha256)
    {
        byte[] content = File.ReadAllBytes(Find(path));
        Assert.Equal(bytes, content.Length);
        Assert.Equal(sha256, Sha256(content));
    }

    private static string Find(string relative)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(relative);
    }

    private static string S(JsonElement item, string property)
    {
        JsonElement value = item.GetProperty(property);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return Assert.IsType<string>(value.GetString());
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
        })) WriteCanonical(writer, value);
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
                foreach (JsonElement item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText(), false); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidOperationException(value.ValueKind.ToString());
        }
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        foreach (string text in Strings(value))
        {
            Assert.DoesNotContain("C:\\", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:/", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AppData", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> Strings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) yield return value.GetString() ?? string.Empty;
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) foreach (string text in Strings(item)) yield return text;
        else if (value.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty property in value.EnumerateObject()) foreach (string text in Strings(property.Value)) yield return text;
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);
    private sealed record CaseBinding(string Code, string CaseId, int TargetCount, string CaseSha256, string PythonFactsSha256);
    private sealed record ExpectedTarget(int InventoryIndex, string Symbol, string CaseId);
    private sealed record TargetBinding(int InventoryIndex, string Symbol, string Kind, string SymbolHash, string SignatureHash, string BodyHash, string Classification, string AssertionId, string Adaptation, string NativeRoute, string CaseId);
    private sealed record NativeObservation(string Code, string CaseId, string[] Facts, string FactsSha256);
    private sealed record NativePin(string Code, int FactCount, string FactsSha256);
    private sealed record OracleCorpus(TargetBinding[] Targets);
}
