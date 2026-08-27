using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelClassOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-model-class-oracle.json";
    private const int FixtureBytes = 34_711;
    private const string FixtureSha256 =
        "sha256:9a5e00a585e983d4a753acb94c46307848d32020e8d3960f9ad8184ccb4cfa7a";
    private const string FixtureSchema =
        "goniegonie.python-reference.dragon-model-class.v1";
    private const string CasesSha256 =
        "sha256:ab27c0de1d256d0942a8db49523fe3ba3d6701ddd469684c2261818518f95a59";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_model_class_oracle.py";
    private const int GeneratorBytes = 42_980;
    private const string GeneratorSha256 =
        "sha256:083e815084afedac2e0fca455f1bae4a108986d3f06aa1e537269091216815eb";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_model_class_oracle.py";
    private const int ValidatorBytes = 21_172;
    private const string ValidatorSha256 =
        "sha256:f25d34ae593a55000422a064a3d232abc41b05738a428956a6634736e08afcd9";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";

    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/model.py";
    private const string UpstreamSymbol = "EnergyModel";
    private const int InventoryIndex = 815;
    private const string SymbolHash =
        "sha256:a7582a410b3e8189778cacda204ee15a6fd3039d6f136f9a9303bb4437fe2170";
    private const string SignatureHash =
        "sha256:0e5d2973f067f9c718303cadabe96e5f8ab87d9d83bce8ec4d369a77266db029";
    private const string BodyHash =
        "sha256:5a7db40c87570a3ae22c820c5b758ed19b5e8120a7cbf0510d3a88eb7c7f33d9";
    private const string AdaptationId =
        "sealed-read-only-native-energy-model-class-a7582a41";
    private const string AssertionId =
        "dragon-model-energy-model-class-a7582a41";
    private const string NativeTarget =
        "GonieGonie.InvisibleDragon.Model.EnergyModel";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Model.EnergyModelClassOracleParityTests.MatchesPinnedPythonEnergyModelClassThroughTypedNativeRoutes";

    private const string ExpectedReceiptSha256 =
        "sha256:bc64f0fa26cb1a352a7a96a8333038ae7922d30cdd75c27a78a45649f9a9a96e";
    private const string ExpectedCollectorOutputSha256 =
        "sha256:c2774991d73b05365682f5b0154453cf664dd1bc5f0b2ed2293a29be60703288";

    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new(
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs",
            22_015,
            "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3"),
        new(
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Common/EnergyPlusVersion.cs",
            4_954,
            "sha256:ea908729f5517e3c9d301210f882019bc8b026da8e3055caeb187d80db86a685"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new(
            "dragon-model-energy-model-class.c01-class-supported-versions-topology",
            "class-topology",
            "sha256:62b1cb7d44213516784a823cce69a6204e8e265107202c0ab06cc0b0197827a8",
            "sha256:77e860eaed52a820286e037d3966e17135f7866dd306c3bcfc478fd44d47cb4a"),
        new(
            "dragon-model-energy-model-class.c02-shared-list-append-visibility-restoration",
            "shared-class-state",
            "sha256:69e12bb56ad9212f0380b92b1c7327f8f9875bd1a8e2e3aa42f6cc88fed04aa4",
            "sha256:6268a6f473231cc8f3d88043a51ef7721c16f1372ae2edbae0d122b7bfea2a29"),
        new(
            "dragon-model-energy-model-class.c03-instance-shadow-arbitrary-attribute-subclass-topology",
            "open-instance-type",
            "sha256:e9043277693366f6b7c69a36c46da49b7dcbfcda30a180a0888567192d76bceb",
            "sha256:9a7170a7a3678ce436b450e2527d348fbca97cf817793b2fc00e8608ec6a0810"),
    };

    private static readonly NativePin[] ExpectedNativePins =
    {
        new(10, "sha256:abb3ecddd94d79ab30911ec006d1c2318f6cff932330e9fdf2f6d11530e1d0fe"),
        new(8, "sha256:8dca8ab906bb2b60ce3caa82a532afe46fc504b1cf7dd891ed82ad3b345c8e1b"),
        new(10, "sha256:60f9b3a20202a73844013dc1f83ed368f2d3ad7d1605333d2a01fd4fe5c5d971"),
    };

    private static readonly ReceiptBinding[] TargetReceipts =
    {
        new(815, "EnergyModel", "class", UpstreamPath, SymbolHash, SignatureHash, BodyHash),
    };

    private static readonly ReceiptBinding[] ContextReceipts =
    {
        new(556, "Version", "class", "src/idragon/common.py", "sha256:1c497416f9054aec72cc23eb32f3740e6001e70183471e0453128ec74d7770c8", "sha256:127a8b300808358bf3f1a153c025fb3d53ef73e7fd1ba8cc098576acb458a6ed", "sha256:fb7b04e087cf5ee44ca605240380ca8847066ea9c7c879315419dc0b52446c3c"),
        new(558, "Version.__init__", "function", "src/idragon/common.py", "sha256:a3def1029c1ebaf97d2c94d1efdc88f0c302c44e0c93d2045c38be0b12a0e983", "sha256:03d7516c1730f6f95147d7ebd855ace566e32c4f896eab3ff830b5ba6e716413", "sha256:fca44c5193da96a1ce893264f7969f6edb34bc2f579bc0447f87386e417adbce"),
    };

    private static readonly ReceiptBinding[] ResolvedReceipts =
    {
        new(816, "EnergyModel.__init__", "function", UpstreamPath, "sha256:1d1dbee8fef8b70b2919c4e46a0ea60efbd748b360d31ff353ea121c72ad97d2", "sha256:9706dcab3a90048744a47f3596613b34247cb6cd1eb2903582e2fb2cb6342a2d", "sha256:e4e5ef56fd12719fe976231c03d867e932eff64870f9c0fd7a5107b7e11538f1"),
        new(817, "EnergyModel.add_supply_system", "function", UpstreamPath, "sha256:174532d0aa6b76826dd78f3d7020ba49eeba26494019da3fb361396e31c15a94", "sha256:576bb4584970582d94ae80ad061612e84dad263321a9e6288b39a92af7cd959f", "sha256:6bf509a4d5050f54bd748c516ed98b6ae249edf3aaa84a75c4c7bd11b7fbef4b"),
        new(818, "EnergyModel.conditioned_zones", "function", UpstreamPath, "sha256:90ceddf7de437a59950e7081185fefbf1f56354a49662431452f11ac24bc6f24", "sha256:e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e", "sha256:ae71f1c62c76cfdf6890e18c83f3dd2709b9fb72627f690db7dc52b7db719348"),
        new(819, "EnergyModel.create_default_idf", "function", UpstreamPath, "sha256:585b53682bd5dbd4d2081e79eddc2789fa60925baafb5eae26de0541346ac9f4", "sha256:6750822d2a0b36e44dced756c45817742cfc0940e8646be6212eedfe3698d8cf", "sha256:e505591e57b64f4f7ff0b6fb18e775ad88048d4eaddb9d8a4f9e5a0afd2c8ab7"),
        new(820, "EnergyModel.surfaces", "function", UpstreamPath, "sha256:9bd40b3fbdc974f1f3a7550b2df6ec8f4c41ce9cb55ecbc07b3f2fce264834c0", "sha256:175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea", "sha256:9ac965df879ac38614b80c38800b8b7e28f3a584d20be71afac9301eea223c06"),
        new(821, "EnergyModel.to_idf", "function", UpstreamPath, "sha256:de10251f38f220956e870d8faea1c7a879da9158b369cffc244f7afc6519eb35", "sha256:9389bd00d5a2180ea9f3cd1aa5695ba492e1665947515c34c31eff01f072bade", "sha256:9d1b5a610b485aa782c0c1f39ed57b65d5534e1ba3271f1a325c52a109228189"),
        new(822, "EnergyModel.unconditioned_zones", "function", UpstreamPath, "sha256:24b8c9a917df6c286d13dfb75c3ca04403b74cf0a70e6056cc933c9ed2822e08", "sha256:e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e", "sha256:e65c4689f16398a99be21f56cf6c046ee411718b151d637a75abc7e8076249c8"),
        new(823, "EnergyModel.used_constructions", "function", UpstreamPath, "sha256:b34dd26fdb9af00f053278e77ac3cc85394a646405e8e5e0b5c077342fd1bebd", "sha256:47d2fe431ebc01347b7bef0a612859f9d45131c67b7ee67971757a0694919023", "sha256:56cc7c61d049242fa77c1c2457d6d9f5678ca41a41af86ddd2ff93be20ed78b3"),
        new(824, "EnergyModel.used_layers", "function", UpstreamPath, "sha256:e15c8d38a7b918895bf399bc319bbb2caf2810d416cb4c8792fedb5cec3358f0", "sha256:d5bc4e72ec91b9ecdbdd46cd7a50e3da18408ff227d9549c7ae42bf488381844", "sha256:bde4ae4c3efe1129e1c3ee19dc273a7e251f770f7173fbc1a3d2b67ec80d0733"),
        new(825, "EnergyModel.used_profiles", "function", UpstreamPath, "sha256:b8a8a5f692a0cbeeec4215cbab71e89291a3f96e68d7702853631dc454a695ab", "sha256:2417ee894af42b33af27bb335ee1a91c7205d1a2093879c28e6e4178554e4a60", "sha256:5e04e97f3e1161b94743a1377272a037c646df7e0aa07b6a3ce51c3d4b61ae9a"),
    };

    private static readonly SourceBinding[] ExpectedSources =
    {
        new("idragon", "src/idragon/__init__.py", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618"),
        new("idragon.common", "src/idragon/common.py", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9"),
        new("idragon.constants", "src/idragon/constants.py", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"),
        new("idragon.dragon", "src/idragon/dragon/__init__.py", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52"),
        new("idragon.dragon.construction", "src/idragon/dragon/construction.py", "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"),
        new("idragon.dragon.hvac", "src/idragon/dragon/hvac.py", "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0", "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"),
        new("idragon.dragon.model", UpstreamPath, "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"),
        new("idragon.dragon.profile", "src/idragon/dragon/profile.py", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef"),
        new("idragon.dragon.shape", "src/idragon/dragon/shape.py", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"),
        new("idragon.imugi", "src/idragon/imugi.py", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"),
        new("idragon.launcher", "src/idragon/launcher.py", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e"),
        new("idragon.utils", "src/idragon/utils.py", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452"),
    };

    private static bool DiscoverPins => string.Equals(
        Environment.GetEnvironmentVariable("GONIEGONIE_DISCOVER_ENERGY_MODEL_CLASS_PINS"),
        "1",
        StringComparison.Ordinal);

    [Fact]
    public void MatchesPinnedPythonEnergyModelClassThroughTypedNativeRoutes()
    {
        ValidatePinnedArtifacts();
        using JsonDocument oracle = ReadPinnedOracle();
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        ValidateNativeBinding();

        NativeObservation[] observations = cases
            .Select((item, index) => ObserveNativeCase(ExpectedCases[index], item))
            .ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), observations.Select(item => item.CaseId));
        Assert.All(observations, observation =>
        {
            Assert.NotEmpty(observation.Facts);
            Assert.Equal(observation.Facts.Length, observation.Facts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(observation.Facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
            Assert.Equal(observation.FactsSha256, CanonicalSha256(JsonSerializer.SerializeToElement(observation.Facts)));
        });

        var receipt = new
        {
            classification = "exception",
            fixture = new
            {
                case_count = ExpectedCases.Length,
                cases_sha256 = CasesSha256,
                generator = ArtifactProjection(GeneratorPath, GeneratorBytes, GeneratorSha256),
                path = FixturePath,
                sha256 = FixtureSha256,
                validator = ArtifactProjection(ValidatorPath, ValidatorBytes, ValidatorSha256),
            },
            native_binding = new
            {
                adaptation_id = AdaptationId,
                implementation_artifacts = NativeArtifacts.Select(item => ArtifactProjection(item.Path, item.Bytes, item.Sha256)).ToArray(),
                implementation_symbol = NativeTarget,
            },
            observations = observations.Select(item => new
            {
                adaptation_id = AdaptationId,
                case_id = item.CaseId,
                native_facts = item.Facts,
                native_facts_sha256 = item.FactsSha256,
                native_outcome = "adapted-as-pinned",
                python_facts_sha256 = item.PythonFactsSha256,
            }).ToArray(),
            upstream = new
            {
                inventory_index = InventoryIndex,
                path = UpstreamPath,
                symbol = UpstreamSymbol,
                symbol_hash = SymbolHash,
            },
        };
        JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
        string receiptSha256 = CanonicalSha256(receiptJson);
        string collectorOutputSha256 = CanonicalSha256(JsonSerializer.SerializeToElement(new
        {
            cases = new[]
            {
                new
                {
                    output = receipt,
                    test_case = EvidenceTestCase,
                },
            },
        }));

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "ENERGY_MODEL_CLASS_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    artifacts = new[]
                    {
                        DiscoverArtifact(FixturePath),
                        DiscoverArtifact(GeneratorPath),
                        DiscoverArtifact(ValidatorPath),
                    },
                    cases_sha256 = CanonicalSha256(oracle.RootElement.GetProperty("cases")),
                    native_cases = observations.Select(item => new
                    {
                        item.CaseId,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipt_sha256 = receiptSha256,
                    collector_output_sha256 = collectorOutputSha256,
                }, DiscoveryJsonOptions));
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }
        Assert.Equal(ExpectedReceiptSha256, receiptSha256);
        Assert.Equal(ExpectedCollectorOutputSha256, collectorOutputSha256);
        ValidateReceipt(receiptJson, observations);

        TrustedEvidenceRecorder.Record(
            AssertionId,
            EvidenceTestCase,
            "not_applicable",
            receipt);
    }

    private static JsonDocument ReadPinnedOracle()
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

    private static void ValidatePinnedArtifacts()
    {
        ValidateArtifact(FixturePath, FixtureBytes, FixtureSha256);
        ValidateArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        ValidateArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        ValidateArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            ValidateArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }
    }

    private static JsonElement[] ValidateCorpus(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoRawAddresses(root.GetRawText());
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(
            root,
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "context_receipts",
            "fact_sha256",
            "resolved_receipts",
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
        ValidateReceiptArray(root.GetProperty("symbols"), TargetReceipts, indexed: false);
        ValidateReceiptArray(root.GetProperty("target_receipts"), TargetReceipts, indexed: true);
        ValidateReceiptArray(root.GetProperty("context_receipts"), ContextReceipts, indexed: true);
        ValidateReceiptArray(root.GetProperty("resolved_receipts"), ResolvedReceipts, indexed: true);
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCases.Length, cases.Length);
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), cases.Select(item => RequiredString(item, "id")));
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId),
            ExpectedCases.Select(item => item.CaseId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(ExpectedCases.Length, ExpectedCases.Select(item => item.CaseId).Distinct(StringComparer.Ordinal).Count());

        JsonElement factMap = root.GetProperty("fact_sha256");
        JsonElement caseMap = root.GetProperty("case_sha256");
        AssertKeys(factMap, ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertKeys(caseMap, ExpectedCases.Select(item => item.CaseId).ToArray());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
            Assert.Equal(ExpectedCases[index].FactSha256, RequiredString(factMap, ExpectedCases[index].CaseId));
            Assert.Equal(ExpectedCases[index].CaseSha256, RequiredString(caseMap, ExpectedCases[index].CaseId));
            Assert.Equal(ExpectedCases[index].CaseSha256, CanonicalSha256(cases[index]));
        }
        return cases;
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "implementation",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        AssertKeys(
            runtime.GetProperty("dependencies"),
            "colorama",
            "et_xmlfile",
            "numpy",
            "openpyxl",
            "pandas",
            "python-dateutil",
            "pytz",
            "six",
            "tqdm",
            "tzdata");
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(upstream, "commit", "inventory_sha256", "loaded_local_modules", "model_source", "sources");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));

        JsonElement modelSource = upstream.GetProperty("model_source");
        AssertKeys(modelSource, "ast_sha256", "bytes", "path", "source_sha256");
        Assert.Equal(8_247, modelSource.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(modelSource, "path"));
        Assert.Equal("sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", RequiredString(modelSource, "source_sha256"));
        Assert.Equal("sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59", RequiredString(modelSource, "ast_sha256"));

        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        JsonElement[] modules = upstream.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(ExpectedSources.Length, sources.Length);
        Assert.Equal(ExpectedSources.Length, modules.Length);
        for (int index = 0; index < ExpectedSources.Length; index++)
        {
            SourceBinding expected = ExpectedSources[index];
            AssertKeys(sources[index], "ast_sha256", "path", "source_sha256");
            Assert.Equal(expected.Path, RequiredString(sources[index], "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(sources[index], "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(sources[index], "ast_sha256"));

            AssertKeys(modules[index], "ast_sha256", "module", "path", "source_sha256");
            Assert.Equal(expected.Module, RequiredString(modules[index], "module"));
            Assert.Equal(expected.Path, RequiredString(modules[index], "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(modules[index], "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(modules[index], "ast_sha256"));
        }
    }

    private static void ValidateReceiptArray(
        JsonElement value,
        IReadOnlyList<ReceiptBinding> expected,
        bool indexed)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            ReceiptBinding receipt = expected[index];
            AssertKeys(
                actual[index],
                indexed
                    ? new[] { "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash" }
                    : new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" });
            if (indexed)
            {
                Assert.Equal(receipt.Index, actual[index].GetProperty("inventory_index").GetInt32());
            }
            Assert.Equal(receipt.Symbol, RequiredString(actual[index], "symbol"));
            Assert.Equal(receipt.Kind, RequiredString(actual[index], "kind"));
            Assert.Equal(receipt.Path, RequiredString(actual[index], "path"));
            Assert.Equal(receipt.SymbolHash, RequiredString(actual[index], "symbol_hash"));
            Assert.Equal(receipt.SignatureHash, RequiredString(actual[index], "signature_hash"));
            Assert.Equal(receipt.BodyHash, RequiredString(actual[index], "body_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement contract)
    {
        AssertKeys(
            contract,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classification_basis",
            "classification_counts",
            "classifications",
            "closure",
            "identity_encoding",
            "native_targets",
            "raw_fact_encoding",
            "source_import_policy",
            "target_receipts",
            "target_symbols");
        AssertSingleMapping(contract.GetProperty("adaptations"), UpstreamSymbol, AdaptationId);
        AssertSingleMapping(contract.GetProperty("assertion_ids"), UpstreamSymbol, AssertionId);
        AssertSingleMapping(contract.GetProperty("classifications"), UpstreamSymbol, "exception");
        AssertSingleMapping(contract.GetProperty("native_targets"), UpstreamSymbol, NativeTarget);
        Assert.Equal(3, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(contract.GetProperty("target_symbols"), UpstreamSymbol);
        Assert.Equal("external-temporary-copy-with-complete-loaded-local-module-audit", RequiredString(contract, "source_import_policy"));
        Assert.False(string.IsNullOrWhiteSpace(RequiredString(contract, "classification_basis")));
        AssertKeys(contract.GetProperty("classification_counts"), "equivalent", "exception");
        Assert.Equal(0, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());
        Assert.Equal(1, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        ValidateReceiptArray(contract.GetProperty("target_receipts"), TargetReceipts, indexed: true);

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "case_coverage_by_symbol",
            "context_receipts",
            "full_symbol_closure",
            "resolved_receipts_not_retargeted",
            "scope",
            "target_coverage_complete",
            "target_symbols",
            "unresolved_boundaries");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.True(closure.GetProperty("target_coverage_complete").GetBoolean());
        Assert.Equal("exact-three-case-energy-model-class-surface", RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("target_symbols"), UpstreamSymbol);
        AssertKeys(closure.GetProperty("case_coverage_by_symbol"), UpstreamSymbol);
        AssertStringArray(closure.GetProperty("case_coverage_by_symbol").GetProperty(UpstreamSymbol), ExpectedCases.Select(item => item.CaseId).ToArray());
        ValidateReceiptArray(closure.GetProperty("context_receipts"), ContextReceipts, indexed: true);
        ValidateReceiptArray(closure.GetProperty("resolved_receipts_not_retargeted"), ResolvedReceipts, indexed: true);
        Assert.DoesNotContain(
            closure.GetProperty("resolved_receipts_not_retargeted").EnumerateArray(),
            item => RequiredString(item, "symbol") == UpstreamSymbol);
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        AssertKeys(item, "context_symbols", "executor", "expected_dotnet", "id", "python", "subfamily", "target_symbols");
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Subfamily, RequiredString(item, "subfamily"));
        Assert.Equal("energy-model-class", RequiredString(item, "executor"));
        AssertStringArray(item.GetProperty("context_symbols"), "Version", "Version.__init__");
        AssertStringArray(item.GetProperty("target_symbols"), UpstreamSymbol);

        JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptations", "classifications", "outcome");
        AssertStringArray(expectedDotnet.GetProperty("adaptations"), AdaptationId);
        AssertSingleMapping(expectedDotnet.GetProperty("classifications"), UpstreamSymbol, "exception");
        Assert.Equal("adapted-as-pinned", RequiredString(expectedDotnet, "outcome"));

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "facts_sha256", "outcome");
        Assert.Equal("observed", RequiredString(python, "outcome"));
        Assert.Equal(expected.FactSha256, RequiredString(python, "facts_sha256"));
        Assert.Equal(expected.FactSha256, CanonicalSha256(python.GetProperty("facts")));

        if (expected.CaseId.Contains("c01-", StringComparison.Ordinal))
        {
            JsonElement facts = python.GetProperty("facts");
            AssertKeys(facts, "class_topology", "declared_public_member_topology", "supported_versions");
            JsonElement topology = facts.GetProperty("class_topology");
            Assert.Equal("EnergyModel", RequiredString(topology, "name"));
            AssertStringArray(topology.GetProperty("direct_base_names"), "object");
            JsonElement versions = facts.GetProperty("supported_versions");
            Assert.Equal("list", RequiredString(versions, "container_type"));
            AssertTaggedInt(versions.GetProperty("count"), "1");
            AssertVersionComponents(Assert.Single(versions.GetProperty("items").EnumerateArray()), "24", "2", "0");
        }
        else if (expected.CaseId.Contains("c02-", StringComparison.Ordinal))
        {
            JsonElement facts = python.GetProperty("facts");
            AssertKeys(facts, "before", "mutation", "restoration");
            AssertTaggedInt(facts.GetProperty("before").GetProperty("count"), "1");
            AssertTaggedInt(facts.GetProperty("mutation").GetProperty("class_count"), "2");
            AssertTaggedInt(facts.GetProperty("mutation").GetProperty("instance_count"), "2");
            AssertTaggedInt(facts.GetProperty("mutation").GetProperty("subclass_count"), "2");
            Assert.True(facts.GetProperty("mutation").GetProperty("appended_item_identity_preserved").GetBoolean());
            AssertVersionComponents(facts.GetProperty("mutation").GetProperty("appended_item"), "25", "1", "0");
            Assert.True(facts.GetProperty("restoration").GetProperty("contents_equal_by_identity").GetBoolean());
            AssertTaggedInt(facts.GetProperty("restoration").GetProperty("count"), "1");
        }
        else
        {
            JsonElement facts = python.GetProperty("facts");
            AssertKeys(facts, "instance_topology", "subclass_topology");
            JsonElement instance = facts.GetProperty("instance_topology");
            Assert.True(instance.GetProperty("created_without_constructor").GetBoolean());
            Assert.False(instance.GetProperty("shadow_is_class_container").GetBoolean());
            Assert.True(instance.GetProperty("shadow_is_input_container").GetBoolean());
            JsonElement subclass = facts.GetProperty("subclass_topology");
            Assert.Equal("returned", RequiredString(subclass, "subclass_definition_outcome"));
            AssertStringArray(subclass.GetProperty("direct_base_names"), "EnergyModel");
            Assert.True(subclass.GetProperty("inherited_supported_versions_is_class_container").GetBoolean());
        }
    }

    private static void ValidateNativeBinding()
    {
        Type type = typeof(EnergyModel);
        Assert.Equal(NativeTarget, type.FullName);
        Assert.True(type.IsPublic);
        Assert.True(type.IsSealed);
        Assert.Equal(typeof(object), type.BaseType);
        PropertyInfo property = Assert.Single(
            type.GetProperties(BindingFlags.Public | BindingFlags.Static),
            item => item.Name == nameof(EnergyModel.SupportedVersions));
        Assert.Equal(typeof(IReadOnlyList<EnergyPlusVersion>), property.PropertyType);
        Assert.True(property.GetMethod!.IsStatic);
        Assert.Null(property.SetMethod);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
        Assert.False(typeof(System.Dynamic.IDynamicMetaObjectProvider).IsAssignableFrom(type));
        Assert.False(typeof(IDictionary<string, object>).IsAssignableFrom(type));
    }

    private static NativeObservation ObserveNativeCase(CaseBinding binding, JsonElement item)
    {
        JsonElement pythonFacts = item.GetProperty("python").GetProperty("facts");
        string[] facts;
        if (binding.CaseId.Contains("c01-", StringComparison.Ordinal))
        {
            JsonElement pythonVersions = pythonFacts.GetProperty("supported_versions");
            Assert.Equal("list", RequiredString(pythonVersions, "container_type"));
            AssertTaggedInt(pythonVersions.GetProperty("count"), "1");
            IReadOnlyList<EnergyPlusVersion> versions = EnergyModel.SupportedVersions;
            EnergyPlusVersion version = Assert.Single(versions);
            PropertyInfo property = typeof(EnergyModel).GetProperty(nameof(EnergyModel.SupportedVersions))!;
            facts = new[]
            {
                "native.type.full_name=" + typeof(EnergyModel).FullName,
                "native.type.is_public=" + Bool(typeof(EnergyModel).IsPublic),
                "native.type.is_sealed=" + Bool(typeof(EnergyModel).IsSealed),
                "native.type.base=" + typeof(EnergyModel).BaseType!.FullName,
                "native.supported_versions.getter.static=" + Bool(property.GetMethod!.IsStatic),
                "native.supported_versions.setter.present=" + Bool(property.SetMethod is not null),
                "native.supported_versions.collection.read_only=" + Bool(((IList<EnergyPlusVersion>)versions).IsReadOnly),
                "native.supported_versions.count=" + versions.Count.ToString(CultureInfo.InvariantCulture),
                "native.supported_versions.item=" + string.Join('.', version),
                "native.supported_versions.item.type=" + version.GetType().FullName,
            };
        }
        else if (binding.CaseId.Contains("c02-", StringComparison.Ordinal))
        {
            AssertTaggedInt(pythonFacts.GetProperty("mutation").GetProperty("class_count"), "2");
            AssertTaggedInt(pythonFacts.GetProperty("restoration").GetProperty("count"), "1");
            IReadOnlyList<EnergyPlusVersion> first = EnergyModel.SupportedVersions;
            IList<EnergyPlusVersion> mutable = Assert.IsAssignableFrom<IList<EnergyPlusVersion>>(first);
            int before = first.Count;
            Exception error = Assert.Throws<NotSupportedException>(() => mutable.Add(new EnergyPlusVersion(25, 1, 0)));
            IReadOnlyList<EnergyPlusVersion> second = EnergyModel.SupportedVersions;
            facts = new[]
            {
                "native.first.count.before=" + before.ToString(CultureInfo.InvariantCulture),
                "native.append.exception=" + error.GetType().FullName,
                "native.first.count.after=" + first.Count.ToString(CultureInfo.InvariantCulture),
                "native.second.count=" + second.Count.ToString(CultureInfo.InvariantCulture),
                "native.collection.first_second.same=" + Bool(ReferenceEquals(first, second)),
                "native.version.first_second.same=" + Bool(ReferenceEquals(first[0], second[0])),
                "native.collection.is_read_only=" + Bool(mutable.IsReadOnly),
                "native.version.item=" + string.Join('.', first[0]),
            };
        }
        else
        {
            Assert.Equal("returned", RequiredString(pythonFacts.GetProperty("subclass_topology"), "subclass_definition_outcome"));
            Assert.False(pythonFacts.GetProperty("instance_topology").GetProperty("shadow_is_class_container").GetBoolean());
            Type type = typeof(EnergyModel);
            PropertyInfo property = type.GetProperty(nameof(EnergyModel.SupportedVersions))!;
            bool dynamicInterface = typeof(System.Dynamic.IDynamicMetaObjectProvider).IsAssignableFrom(type);
            bool dictionaryInterface = typeof(IDictionary<string, object>).IsAssignableFrom(type);
            facts = new[]
            {
                "native.type.is_sealed=" + Bool(type.IsSealed),
                "native.subclass.declaration.supported=" + Bool(!type.IsSealed),
                "native.supported_versions.property.static=" + Bool(property.GetMethod!.IsStatic),
                "native.supported_versions.property.instance=" + Bool(!property.GetMethod.IsStatic),
                "native.supported_versions.property.setter.present=" + Bool(property.SetMethod is not null),
                "native.dynamic_interface.supported=" + Bool(dynamicInterface),
                "native.string_object_dictionary.supported=" + Bool(dictionaryInterface),
                "native.public_instance_field.count=" + type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length.ToString(CultureInfo.InvariantCulture),
                "native.instance_shadow.supported=" + Bool(!property.GetMethod.IsStatic && property.SetMethod is not null),
                "native.arbitrary_instance_attribute.supported=" + Bool(dynamicInterface || dictionaryInterface),
            };
        }
        return new NativeObservation(
            binding.CaseId,
            binding.FactSha256,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static void ValidateReceipt(JsonElement receipt, IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(receipt, "classification", "fixture", "native_binding", "observations", "upstream");
        Assert.Equal("exception", RequiredString(receipt, "classification"));

        JsonElement fixture = receipt.GetProperty("fixture");
        AssertKeys(fixture, "case_count", "cases_sha256", "generator", "path", "sha256", "validator");
        Assert.Equal(3, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(FixturePath, RequiredString(fixture, "path"));
        Assert.Equal(FixtureSha256, RequiredString(fixture, "sha256"));
        ValidateArtifactProjection(fixture.GetProperty("generator"), GeneratorPath, GeneratorBytes, GeneratorSha256);
        ValidateArtifactProjection(fixture.GetProperty("validator"), ValidatorPath, ValidatorBytes, ValidatorSha256);

        JsonElement binding = receipt.GetProperty("native_binding");
        AssertKeys(binding, "adaptation_id", "implementation_artifacts", "implementation_symbol");
        Assert.Equal(AdaptationId, RequiredString(binding, "adaptation_id"));
        Assert.Equal(NativeTarget, RequiredString(binding, "implementation_symbol"));
        JsonElement[] artifacts = binding.GetProperty("implementation_artifacts").EnumerateArray().ToArray();
        Assert.Equal(NativeArtifacts.Length, artifacts.Length);
        for (int index = 0; index < artifacts.Length; index++)
        {
            ValidateArtifactProjection(artifacts[index], NativeArtifacts[index].Path, NativeArtifacts[index].Bytes, NativeArtifacts[index].Sha256);
        }

        JsonElement[] recorded = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(observations.Count, recorded.Length);
        for (int index = 0; index < recorded.Length; index++)
        {
            AssertKeys(recorded[index], "adaptation_id", "case_id", "native_facts", "native_facts_sha256", "native_outcome", "python_facts_sha256");
            Assert.Equal(AdaptationId, RequiredString(recorded[index], "adaptation_id"));
            Assert.Equal(observations[index].CaseId, RequiredString(recorded[index], "case_id"));
            Assert.Equal("adapted-as-pinned", RequiredString(recorded[index], "native_outcome"));
            Assert.Equal(observations[index].PythonFactsSha256, RequiredString(recorded[index], "python_facts_sha256"));
            Assert.Equal(observations[index].FactsSha256, RequiredString(recorded[index], "native_facts_sha256"));
            AssertStringArray(recorded[index].GetProperty("native_facts"), observations[index].Facts);
        }

        JsonElement upstream = receipt.GetProperty("upstream");
        AssertKeys(upstream, "inventory_index", "path", "symbol", "symbol_hash");
        Assert.Equal(InventoryIndex, upstream.GetProperty("inventory_index").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(UpstreamSymbol, RequiredString(upstream, "symbol"));
        Assert.Equal(SymbolHash, RequiredString(upstream, "symbol_hash"));
    }

    private static void AssertVersionComponents(JsonElement version, params string[] expected)
    {
        Assert.Equal(
            expected,
            version.GetProperty("components").EnumerateArray().Select(item =>
            {
                AssertTaggedInt(item, RequiredString(item, "value"));
                return RequiredString(item, "value");
            }));
    }

    private static void AssertTaggedInt(JsonElement value, string expected)
    {
        AssertKeys(value, "kind", "value");
        Assert.Equal("int", RequiredString(value, "kind"));
        Assert.Equal(expected, RequiredString(value, "value"));
    }

    private static object ArtifactProjection(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
    };

    private static void ValidateArtifactProjection(JsonElement value, string path, int bytes, string sha256)
    {
        AssertKeys(value, "bytes", "path", "sha256");
        Assert.Equal(bytes, value.GetProperty("bytes").GetInt32());
        Assert.Equal(path, RequiredString(value, "path"));
        Assert.Equal(sha256, RequiredString(value, "sha256"));
    }

    private static void ValidateArtifact(string path, int bytes, string sha256)
    {
        byte[] value = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(bytes, value.Length);
        Assert.Equal(sha256, Sha256(value));
    }

    private static object DiscoverArtifact(string path)
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(path));
        return new
        {
            bytes = bytes.Length,
            path,
            sha256 = Sha256(bytes),
        };
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static void AssertSingleMapping(JsonElement value, string key, string expected)
    {
        AssertKeys(value, key);
        Assert.Equal(expected, RequiredString(value, key));
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private static string RequiredString(JsonElement value, string property)
    {
        JsonElement item = value.GetProperty(property);
        Assert.Equal(JsonValueKind.String, item.ValueKind);
        return item.GetString()!;
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
    }

    private static string CanonicalSha256(JsonElement value)
    {
        var builder = new StringBuilder();
        WriteCanonicalJson(builder, value);
        return Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static void WriteCanonicalJson(StringBuilder builder, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }
                    firstProperty = false;
                    AppendPythonJsonString(builder, property.Name);
                    builder.Append(':');
                    WriteCanonicalJson(builder, property.Value);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }
                    firstItem = false;
                    WriteCanonicalJson(builder, item);
                }
                builder.Append(']');
                break;
            case JsonValueKind.String:
                AppendPythonJsonString(builder, value.GetString()!);
                break;
            case JsonValueKind.Number:
                builder.Append(value.GetRawText());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new Xunit.Sdk.XunitException("Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
    }

    private static void AppendPythonJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            string[] names = value.EnumerateObject().Select(property => property.Name).ToArray();
            Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            foreach (JsonProperty property in value.EnumerateObject())
            {
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

    private static void AssertNoRawAddresses(string value)
    {
        Assert.False(Regex.IsMatch(value, @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(value, @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(value, @"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d", RegexOptions.CultureInvariant));
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            Assert.False(Regex.IsMatch(value.GetString()!, @"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))", RegexOptions.CultureInvariant));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoHostPaths(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoHostPaths(item);
            }
        }
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number));
            Assert.False(double.IsNaN(number));
            Assert.False(double.IsInfinity(number));
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

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);
    private sealed record CaseBinding(string CaseId, string Subfamily, string FactSha256, string CaseSha256);
    private sealed record NativeObservation(string CaseId, string PythonFactsSha256, string[] Facts, string FactsSha256);
    private sealed record NativePin(int FactCount, string FactsSha256);
    private sealed record ReceiptBinding(
        int Index,
        string Symbol,
        string Kind,
        string Path,
        string SymbolHash,
        string SignatureHash,
        string BodyHash);
    private sealed record SourceBinding(string Module, string Path, string SourceSha256, string AstSha256);
}
