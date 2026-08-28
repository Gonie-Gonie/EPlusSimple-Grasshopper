#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Idf;

public sealed class ImugiIdfObjectCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/imugi-idf-object-core-oracle.json";
    private const int FixtureBytes = 119_205;
    private const string FixtureSha256 =
        "sha256:7237e974d6d938c6f8f7215661f54db4f26a2a7afc664765b895656a7720babd";
    private const string FixtureSchema =
        "goniegonie.python-reference.imugi-idf-object-core.v1";
    private const string FixtureRepositoryCommit = "aa53eda";
    private const string CasesSha256 =
        "sha256:b756d2c05de8a6c61319b0e7dcaa44e13a4a4dcc01919480418b6555e7d12cc5";
    private const string TargetReceiptsSha256 =
        "sha256:b7cf5615507de3309fc1d8429390216b1920764ef910200f2559c8e187ea3b94";

    private const string GeneratorPath =
        "tools/python-reference/generate_imugi_idf_object_core_oracle.py";
    private const int GeneratorBytes = 30_116;
    private const string GeneratorSha256 =
        "sha256:8589497feab58cc9d9c05479c50264a091182c2d68531398d1decddd24f7cc43";
    private const string ValidatorPath =
        "tests/PythonReference/test_imugi_idf_object_core_oracle.py";
    private const int ValidatorBytes = 11_115;
    private const string ValidatorSha256 =
        "sha256:5c296ed4b6129dfbb40523136f91877169191a4ff42b5be63411d46bc91e5c73";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/imugi.py";
    private const int UpstreamBytes = 91_815;
    private const string UpstreamSourceSha256 =
        "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613";
    private const string UpstreamAstSha256 =
        "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Idf.ImugiIdfObjectCoreOracleParityTests.MatchesPinnedImugiIdfObjectThroughPublicProductionApis";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/IdfModel.cs", 13_182,
            "sha256:50aa8a362214d34bba37dcf51ef3c0cce89d54895110a0da786c11d8fe233495"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/IdfParser.cs", 6_040,
            "sha256:98a33eaed892707acb1d05c9e9ef74a9ebb9ec3d258e370e89ff706e267806be"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/IdfWriter.cs", 4_289,
            "sha256:cc7cc49afcd98a4d4067371686feb49d120a4dd5f7bf30611599a6512c062892"),
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
            "sha256:0dbac25c23c3a1bb3c397707d461b29db5bde22e363f9b7b60fd78ad5de33130",
            "sha256:c8e92c934fce4ae101ec6ebef6a74fc03e0cd28884192bdf4a8a9cd32e9e773d"),
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
        new("A01", 10, "sha256:09476ad2ccaaa2f8a326abba2208942dcbdb5d0f9073e65ee5e97f390843418a"),
        new("A02", 8, "sha256:7e9ace8e21b77843c503d8106c08fa1f1154410ac7ed70933e32b96927ed8d8f"),
        new("A03", 7, "sha256:46e9d744e4c707da1d16c4770f65b5f5fafa20d85399a9b83224fce96d390ee6"),
        new("A04", 6, "sha256:3f770308dd7b81e810e973f1f6c13d94049caaa170058aceae5f4416e84dd7c1"),
        new("B01", 9, "sha256:289e9bf2407b6ecab6cf200681a2d2cb752088c23dec575009bbe928ee774cce"),
        new("B02", 8, "sha256:c999cbd00cfd9ddb216621ca43b05badbe9333e9f85870a6d10678ca66aaace1"),
        new("B03", 8, "sha256:02af904fe9d238c6035aab237aad7d699d5b80567de122e196cccce0860a79bd"),
    };
    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:a05bbb17f4c250cd194b1829f397cfaecbcd240c99263c611ab90e6ecf6b39a6", // 1108 IDF
        "sha256:c45854df4b1d13cfc47cccd8ae53054fd3aae9cdd6691b0c8dc0c6cbddacf56c", // 1109 IDF.__init__
        "sha256:cfcd0109a44a7fcf733ddd6dbaf518e0135bf7ccb8aeadc6acb3f874c95bb8dd", // 1112 IDF.__str__
        "sha256:21adac19b473a851174d062a5f8a1adea60d165a0a7671b258c079d0a306419d", // 1113 IDF.append
        "sha256:2e6432a3d94d59db7939b25c0a1072b473062c2506d31d703f63424439e765ef", // 1114 IDF.check_validity
        "sha256:db8d5131b2f1e7b10ef536bb3630e1c9c4ff9d0d5c3d6642892c84eba375a9ac", // 1115 IDF.default_filename
        "sha256:a3efdf4bb2e5f70fe139d2868b5fbfb22bfa444f203ba9440b877c9fc4f1a2fe", // 1116 IDF.idd
        "sha256:6b3ecd2654f7e4e9dda2e124b2cb6cac5f8faef3f289f8c181c129c0c326b4e2", // 1118 IDF.read_idf
        "sha256:cdbcd6fb9d4a8c5e4bc0bb1014e854e16f22aaaba70973fdef2cc86be0ee3673", // 1119 IDF.run
        "sha256:5f9de5369df05fbdf6ea53a0435f54c03b4621999342d8564daa01fcdad2eb74", // 1121 IDF.version
        "sha256:fdd7c219069a9a620426c57f9a40e99ec92496aa6d4f38b7de4aaa2e868e4634", // 1122 IDF.write
        "sha256:662e7f0065144534f76e69e0f08eb3b17aaffe80eec15d052e678ff875ca03ed", // 1167 IdfObject
        "sha256:7860bed4a9596511bc39950e55309389a0591e1ee265ba8f92a9c6a44de2c3f3", // 1170 IdfObject.__getitem__
        "sha256:25d369432fe6c2598c65641a480400078fe6a1c99d1d4baec0e69e177435e5a2", // 1171 IdfObject.__init__
        "sha256:40a23d37d881a3efc31d57168378b4fac944151dfee03dfc86a713a9cc051fec", // 1173 IdfObject.__setitem__
        "sha256:eab49008e78c20c27965a7226dda77aed435fe9af9c425697bdb9109731a63fb", // 1174 IdfObject.__str__
        "sha256:f553121dcd6c3c04675c7889a8314ace7f1609f8b170859ee1426b3a0a15b276", // 1175 IdfObject.check_field_validity
        "sha256:8c4e8af0524447234cf01048de15f0468bef81226ad64156b603e953724a6820", // 1176 IdfObject.check_validity
        "sha256:1102aaf62cd29381614f4f3ddb36498938c23dd171ec1444b34d29d9148126db", // 1177 IdfObject.choices
        "sha256:827cdd2342c324ddf40c0e0b8c4efbd4b8426698cb0f5ff1d2f3ba1214788e0f", // 1178 IdfObject.ensure_validity
        "sha256:6d97fba361dbc092b0981f8934d3345f47edd1b769372a28a22bc1dc2794478d", // 1179 IdfObject.grandparent
        "sha256:bf2088d8c22197b0e2901144332f0439ac8016c98d2597e10bb571bd78f03e28", // 1180 IdfObject.has_parent
        "sha256:612a207dc9a26af1921de81b527cf5e1d40076ffd3297b374b228ab124581509", // 1181 IdfObject.idd
        "sha256:2ee4e0808cfe6697dbf03d8b685ff6a1059e5a0992c716d36ccf98c1bca940f0", // 1182 IdfObject.parent
        "sha256:f45e9352886198f542f717d8263b2314a9ea7c85b6c6b2d8dbf89a86531a55c3", // 1183 IdfObject.rename
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:933db336c8bb06d9544093ec379953084c5e44b2715cf6166a2aae827869d12e", // imugi-idf-object-core-1108-9fa0a86a
        "sha256:72b5a8356f771935bc967cf23c38af94a3a52c4661d450a6274d0e98c9e18c02", // imugi-idf-object-core-1109-a0cef595
        "sha256:d7003432ddd2ae1158bb6bc2b45ee08d4a4a94c3919523977859490aa9b896e5", // imugi-idf-object-core-1112-641b4167
        "sha256:0f9a71b3502321a72386ca3cef9509a9e571ccfacff0a3a53841cea1a53efd8e", // imugi-idf-object-core-1113-1114979d
        "sha256:54449c47f09a3b9efe144646196b1db6325b6a9d31d6fed56c05db089849c7bb", // imugi-idf-object-core-1114-ca419512
        "sha256:bd338a13734d488305b660314526617efd68ecb951154b1719567e3f0ad2ef17", // imugi-idf-object-core-1115-019279b8
        "sha256:c54cb5566d0471e3135d13cfc39f5a1d2d940c9a25d0ab38aa2c7b381cee45bc", // imugi-idf-object-core-1116-d7e9a2d3
        "sha256:0d725b19abf1486ff6bc960007d4857fef30bc26156b2ac0cd662e25ffb30dfa", // imugi-idf-object-core-1118-78c83655
        "sha256:f901914a30d80a85c7f946c3a9ba754a1a2eba50e691f104cc36f0c4966f0d60", // imugi-idf-object-core-1119-bcd90001
        "sha256:292481b8c36fcf414d5fb6c6f9e0be47a7421fdd42e806d5db15aee2b13f9686", // imugi-idf-object-core-1121-013ee380
        "sha256:9806cb73f874ff0d1832b5cf8eb8c9c9110c1aefff395739505b3cc3f023c68d", // imugi-idf-object-core-1122-d79138b0
        "sha256:b225a692ea0b8e5c6c3e4d805350216b4cd4a76a5919970e9b19473b0184d5d2", // imugi-idf-object-core-1167-57c5d21a
        "sha256:bfc9d08076fe21c258beb2990fba7a652e941a33e058916978e72f5931f581eb", // imugi-idf-object-core-1170-7799d464
        "sha256:2e0797e5fa1cda289061dc19a21f6c32902e411a8f72de2b28800e60163c3bda", // imugi-idf-object-core-1171-1f86dc75
        "sha256:ea5e90536aefe5562ab5e946b49f2f74de0b2ac9c57898f6e227fb77929dce65", // imugi-idf-object-core-1173-d94a9b97
        "sha256:69e52c5c297a9566ed6f99cdf7a05f72f4af2b2db107ac5424f23e29011598fc", // imugi-idf-object-core-1174-f978bc66
        "sha256:db7aebf9d409d991d0df3da94590ad94daa7036aa98c9cbc9093398176244f9e", // imugi-idf-object-core-1175-8eee76b2
        "sha256:453bbb52ac75364900560b4d04e5dc1a8f21affc7490892deffc176d45c95202", // imugi-idf-object-core-1176-2046c730
        "sha256:0cba8d9ed8de24827caaaa0638a4db599bb0d231efe0c7187bf6f8fae750898d", // imugi-idf-object-core-1177-823cd22d
        "sha256:c8772909dc5834b601d6cf213885fb0ad004f8cd73a6599385a4baf9e3a93fb9", // imugi-idf-object-core-1178-a24d2160
        "sha256:a5f899a0e47d4b66cf2bf82042091afb4288aa72b4c698fed5a68cea4981f45e", // imugi-idf-object-core-1179-9ee8bea6
        "sha256:81872dfce091202d1a65a8ca41ef829cda0ceca2f3bec7fac67491378d25b7ba", // imugi-idf-object-core-1180-efacd493
        "sha256:75653d5ab08393c004ba4de7efbc86cd133763172517fabf3cc4dbfa95086aa4", // imugi-idf-object-core-1181-37692960
        "sha256:82576636de2739cd77229b7bbfc4bea8b6af7b50335f164a77a63ea5d6f17c9a", // imugi-idf-object-core-1182-8f974416
        "sha256:8579d271d3c0e23cda51d1c4613fbd8523b6b959129e96ef8a2ad6e293feccb7", // imugi-idf-object-core-1183-8dfd0cfa
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
        Assert.Equal("GonieGonie.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)", Route("IDF.append"));

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
        "IDF.__str__" => "GonieGonie.InvisibleDragon.Idf.IdfWriter.Write(IdfDocument, IdfWriterOptions?)",
        "IDF.append" => "GonieGonie.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)",
        "IDF.read_idf" => "GonieGonie.InvisibleDragon.Idf.IdfParser.ParseFile(string, IddSchema?, Encoding?)",
        "IdfObject.__getitem__" => "GonieGonie.InvisibleDragon.Idf.IdfObject.this[int|string]",
        "IdfObject.__str__" => "GonieGonie.InvisibleDragon.Idf.IdfWriter.Write(IdfDocument, IdfWriterOptions?)",
        "IdfObject.idd" => "GonieGonie.InvisibleDragon.Idf.IdfObject.Definition",
        _ when symbol.StartsWith("IDF", StringComparison.Ordinal) =>
            "GonieGonie.InvisibleDragon.Idf.IdfDocument public production API (intentional adaptation; no Python source/API compatibility claim)",
        _ => "GonieGonie.InvisibleDragon.Idf.IdfObject public production API (intentional adaptation; no Python source/API compatibility claim)",
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
            "append-route=GonieGonie.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)",
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
        string directory = Path.Combine(Path.GetTempPath(), "goniegonie-imugi-idf-parity", Guid.NewGuid().ToString("N"));
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
                "parse-file-route=GonieGonie.InvisibleDragon.Idf.IdfParser.ParseFile(string, IddSchema?, Encoding?)",
                "write-file-route=GonieGonie.InvisibleDragon.Idf.IdfWriter.WriteFile(string, IdfDocument, IdfWriterOptions?, Encoding?)",
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
            "public-definition-route=GonieGonie.InvisibleDragon.Idf.IdfObject.Definition",
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
