using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class EnergyModelProjectionOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-model-projections-oracle.json";
    private const string OracleSha256 =
        "sha256:2c1ff9a21e0d8bdb68dce2686620a3ef8812d1ee9e26e6dc75a1e241b464710f";
    private const string CasesSha256 =
        "sha256:b8ec10dcd0e44e8c46584cd241489b58fb4562d99bb790aff5202a6350b0a784";
    private const int OracleByteLength = 22_153;
    private const int ExpectedCaseCount = 12;
    private const string OracleSchema =
        "dragons.python-reference.dragon-model-projections.v1";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.EnergyModelProjectionOracleParityTests.MatchesPinnedPythonProjections";
    private const string EnergyModelTypeName =
        "Dragons.InvisibleDragon.Model.EnergyModel";

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/dragon/model.py", "EnergyModel.surfaces", "sha256:9bd40b3fbdc974f1f3a7550b2df6ec8f4c41ce9cb55ecbc07b3f2fce264834c0", "dragon-model-projections-surfaces-9bd40b3f"),
        new("src/idragon/dragon/model.py", "EnergyModel.used_constructions", "sha256:b34dd26fdb9af00f053278e77ac3cc85394a646405e8e5e0b5c077342fd1bebd", "dragon-model-projections-used-constructions-b34dd26f"),
        new("src/idragon/dragon/model.py", "EnergyModel.used_layers", "sha256:e15c8d38a7b918895bf399bc319bbb2caf2810d416cb4c8792fedb5cec3358f0", "dragon-model-projections-used-layers-e15c8d38"),
        new("src/idragon/dragon/model.py", "EnergyModel.used_profiles", "sha256:b8a8a5f692a0cbeeec4215cbab71e89291a3f96e68d7702853631dc454a695ab", "dragon-model-projections-used-profiles-b8a8a5f6"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("EnergyModel.surfaces", "function", "sha256:175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea", "sha256:9ac965df879ac38614b80c38800b8b7e28f3a584d20be71afac9301eea223c06", "equivalent", null, EnergyModelTypeName + ".Surfaces"),
        new("EnergyModel.used_constructions", "function", "sha256:47d2fe431ebc01347b7bef0a612859f9d45131c67b7ee67971757a0694919023", "sha256:56cc7c61d049242fa77c1c2457d6d9f5678ca41a41af86ddd2ff93be20ed78b3", "exception", "deterministic-used-construction-projection", EnergyModelTypeName + ".UsedConstructions"),
        new("EnergyModel.used_layers", "function", "sha256:d5bc4e72ec91b9ecdbdd46cd7a50e3da18408ff227d9549c7ae42bf488381844", "sha256:bde4ae4c3efe1129e1c3ee19dc273a7e251f770f7173fbc1a3d2b67ec80d0733", "exception", "deterministic-used-layer-projection", EnergyModelTypeName + ".UsedLayers"),
        new("EnergyModel.used_profiles", "function", "sha256:2417ee894af42b33af27bb335ee1a91c7205d1a2093879c28e6e4178554e4a60", "sha256:5e04e97f3e1161b94743a1377272a037c646df7e0aa07b6a3ce51c3d4b61ae9a", "equivalent", null, EnergyModelTypeName + ".UsedProfiles"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-model-projections.surfaces.empty-fresh", "energy-model-surfaces", "EnergyModel.surfaces"),
        new("dragon-model-projections.surfaces.flatten-order-identity", "energy-model-surfaces", "EnergyModel.surfaces"),
        new("dragon-model-projections.surfaces.result-mutation-isolated", "energy-model-surfaces", "EnergyModel.surfaces"),
        new("dragon-model-projections.used-constructions.collision-dedup", "energy-model-used-constructions", "EnergyModel.used_constructions"),
        new("dragon-model-projections.used-constructions.empty-filtered", "energy-model-used-constructions", "EnergyModel.used_constructions"),
        new("dragon-model-projections.used-constructions.hash-order-resize", "energy-model-used-constructions", "EnergyModel.used_constructions"),
        new("dragon-model-projections.used-layers.empty-fresh", "energy-model-used-layers", "EnergyModel.used_layers"),
        new("dragon-model-projections.used-layers.hash-equality-mismatch", "energy-model-used-layers", "EnergyModel.used_layers"),
        new("dragon-model-projections.used-layers.hash-order-resize", "energy-model-used-layers", "EnergyModel.used_layers"),
        new("dragon-model-projections.used-profiles.case-sensitive-unicode-replacement", "energy-model-used-profiles", "EnergyModel.used_profiles"),
        new("dragon-model-projections.used-profiles.duplicate-name-last-wins", "energy-model-used-profiles", "EnergyModel.used_profiles"),
        new("dragon-model-projections.used-profiles.empty-fresh", "energy-model-used-profiles", "EnergyModel.used_profiles"),
    };

    [Fact]
    public void MatchesPinnedPythonProjections()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = Sha256(bytes);
        Assert.Equal(OracleSha256, sha256);
        Assert.Equal(OracleByteLength, bytes.Length);

        using JsonDocument oracle = JsonDocument.Parse(bytes);
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        var observations = new List<NativeObservation>(ExpectedCaseCount);
        for (int index = 0; index < cases.Length; index++)
        {
            CaseBinding binding = ExpectedCases[index];
            SymbolContract symbol = Assert.Single(
                ExpectedSymbols,
                candidate => candidate.Symbol == binding.Symbol);
            string[] nativeFacts = ExecuteCase(
                binding,
                cases[index].GetProperty("python").GetProperty("facts"));
            Assert.Equal(3, nativeFacts.Length);
            Assert.Equal(3, nativeFacts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(nativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));

            JsonElement nativeFactsJson = JsonSerializer.SerializeToElement(nativeFacts);
            AssertNoRawAddresses(nativeFactsJson.GetRawText());
            AssertNoHostPaths(nativeFactsJson);
            AssertNoNonFiniteJsonNumbers(nativeFactsJson);
            observations.Add(new NativeObservation(
                binding.CaseId,
                binding.Symbol,
                symbol.AdaptationId,
                nativeFacts));
        }

        Assert.Equal(ExpectedCaseCount, observations.Count);
        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            NativeObservation[] symbolObservations = observations
                .Where(item => item.Symbol == evidence.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(3, symbolObservations.Length);
            var receipt = new
            {
                fixture = new
                {
                    case_count = ExpectedCaseCount,
                    cases_sha256 = CasesSha256,
                    path = OracleRepositoryPath,
                    sha256,
                },
                observations = symbolObservations.Select(item => new
                {
                    adaptation_id = item.AdaptationId,
                    case_id = item.CaseId,
                    native_facts = item.NativeFacts,
                    native_outcome = "returned",
                }).ToArray(),
                upstream_path = evidence.Path,
                upstream_symbol = evidence.Symbol,
            };
            JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
            ValidateReceipt(receiptJson, evidence, symbolObservations);
            TrustedEvidenceRecorder.Record(
                evidence.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }
    }

    private static JsonElement[] ValidateCorpus(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(
            root,
            "cases",
            "cases_sha256",
            "consumer_contract",
            "runtime",
            "schema",
            "symbols",
            "upstream");
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));
        AssertNoRawAddresses(root.GetRawText());
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);

        ValidateUpstream(root.GetProperty("upstream"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateEvidenceBindings();
        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));
        ValidateNativeBindings();

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCaseCount, cases.Length);
        string[] identifiers = cases.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), identifiers);
        Assert.Equal(identifiers.OrderBy(item => item, StringComparer.Ordinal), identifiers);
        Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
        }

        Assert.All(
            cases.GroupBy(item => RequiredString(item, "symbol")),
            group => Assert.Equal(3, group.Count()));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(upstream, "commit", "inventory_sha256", "sources");
        Assert.Equal(
            "847b01f68f438f560a986072bcaa7768fbf67897",
            RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02",
            RequiredString(upstream, "inventory_sha256"));

        JsonElement[] sources = upstream.GetProperty("sources")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(4, sources.Length);
        AssertSource(
            sources[0],
            "src/idragon/dragon/construction.py",
            "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622",
            "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a");
        AssertSource(
            sources[1],
            "src/idragon/dragon/model.py",
            "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
            "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59");
        AssertSource(
            sources[2],
            "src/idragon/dragon/profile.py",
            "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445",
            "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef");
        AssertSource(
            sources[3],
            "src/idragon/dragon/shape.py",
            "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c",
            "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2");
    }

    private static void AssertSource(
        JsonElement source,
        string path,
        string sourceSha256,
        string astSha256)
    {
        AssertKeys(source, "ast_sha256", "path", "source_sha256");
        Assert.Equal(path, RequiredString(source, "path"));
        Assert.Equal(sourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(astSha256, RequiredString(source, "ast_sha256"));
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "implementation",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());

        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(
            dependencies,
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
        Assert.Equal("0.4.6", RequiredString(dependencies, "colorama"));
        Assert.Equal("2.0.0", RequiredString(dependencies, "et_xmlfile"));
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("3.1.5", RequiredString(dependencies, "openpyxl"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("2.9.0.post0", RequiredString(dependencies, "python-dateutil"));
        Assert.Equal("2024.2", RequiredString(dependencies, "pytz"));
        Assert.Equal("1.16.0", RequiredString(dependencies, "six"));
        Assert.Equal("4.67.1", RequiredString(dependencies, "tqdm"));
        Assert.Equal("2024.2", RequiredString(dependencies, "tzdata"));
    }

    private static void ValidateEvidenceBindings()
    {
        Assert.Equal(4, ExpectedEvidence.Length);
        Assert.Equal(4, ExpectedSymbols.Length);
        Assert.Equal(
            ExpectedEvidence.Select(item => item.Symbol),
            ExpectedSymbols.Select(item => item.Symbol));
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => (item.Path, item.Symbol)).Distinct().Count());
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ExpectedEvidence, item =>
        {
            Assert.Equal("src/idragon/dragon/model.py", item.Path);
            Assert.Matches("^sha256:[0-9a-f]{64}$", item.SymbolHash);
            Assert.Matches("^[a-z0-9][a-z0-9-]+$", item.AssertionId);
        });
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            SymbolContract expected = ExpectedSymbols[index];
            JsonElement symbol = symbols[index];
            AssertKeys(
                symbol,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
            Assert.Equal("src/idragon/dragon/model.py", RequiredString(symbol, "path"));
            Assert.Equal(expected.Symbol, RequiredString(symbol, "symbol"));
            Assert.Equal(expected.Kind, RequiredString(symbol, "kind"));
            Assert.Equal(expected.SignatureHash, RequiredString(symbol, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(symbol, "body_hash"));
            Assert.Equal(ExpectedEvidence[index].SymbolHash, RequiredString(symbol, "symbol_hash"));
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
            "classifications",
            "hash_encoding",
            "identity_encoding",
            "native_order",
            "runtime_names",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(
            contract.GetProperty("target_symbols"),
            ExpectedSymbols.Select(item => item.Symbol).ToArray());
        AssertStringArray(
            contract.GetProperty("case_ids"),
            ExpectedCases.Select(item => item.CaseId).ToArray());
        Assert.Equal(
            "signed-int64-decimal-string",
            RequiredString(contract, "hash_encoding"));
        Assert.Equal(
            "logical-label-and-registry-index-only-no-id-or-address",
            RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "stable-first-use-order-for-declared-set-order-adaptations",
            RequiredString(contract, "native_order"));
        Assert.Equal(
            "pinned-python-only-no-native-type-name-claims",
            RequiredString(contract, "runtime_names"));

        JsonElement adaptations = contract.GetProperty("adaptations");
        AssertKeys(
            adaptations,
            "EnergyModel.used_constructions",
            "EnergyModel.used_layers");
        Assert.Equal(
            "deterministic-used-construction-projection",
            RequiredString(adaptations, "EnergyModel.used_constructions"));
        Assert.Equal(
            "deterministic-used-layer-projection",
            RequiredString(adaptations, "EnergyModel.used_layers"));

        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        AssertKeys(assertions, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            Assert.Equal(
                ExpectedEvidence[index].AssertionId,
                RequiredString(assertions, ExpectedSymbols[index].Symbol));
            Assert.Equal(
                ExpectedSymbols[index].Classification,
                RequiredString(classifications, ExpectedSymbols[index].Symbol));
        }
    }

    private static void ValidateNativeBindings()
    {
        Assert.Equal(EnergyModelTypeName, typeof(EnergyModel).FullName);
        AssertProjectionProperty<Surface>("Surfaces");
        AssertProjectionProperty<OpaqueConstruction>("UsedConstructions");
        AssertProjectionProperty<Layer>("UsedLayers");
        AssertProjectionProperty<ZoneProfile>("UsedProfiles");

        Assert.Equal(
            EnergyModelTypeName + ".Surfaces",
            ExpectedSymbols[0].ImplementationSymbol);
        Assert.Equal(
            EnergyModelTypeName + ".UsedConstructions",
            ExpectedSymbols[1].ImplementationSymbol);
        Assert.Equal(
            EnergyModelTypeName + ".UsedLayers",
            ExpectedSymbols[2].ImplementationSymbol);
        Assert.Equal(
            EnergyModelTypeName + ".UsedProfiles",
            ExpectedSymbols[3].ImplementationSymbol);
    }

    private static void AssertProjectionProperty<T>(string name)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(
            typeof(EnergyModel).GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(EnergyModel), property.DeclaringType);
        Assert.Equal(typeof(IReadOnlyList<T>), property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.False(property.GetMethod.IsStatic);
        Assert.Null(property.SetMethod);
        Assert.Empty(property.GetIndexParameters());
    }

    private static void ValidateCase(JsonElement value, CaseBinding expected)
    {
        SymbolContract contract = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == expected.Symbol);
        if (contract.AdaptationId is null)
        {
            AssertKeys(value, "executor", "id", "python", "symbol");
        }
        else
        {
            AssertKeys(value, "executor", "expected_dotnet", "id", "python", "symbol");
            JsonElement expectedDotNet = value.GetProperty("expected_dotnet");
            AssertKeys(expectedDotNet, "adaptation", "outcome");
            Assert.Equal(contract.AdaptationId, RequiredString(expectedDotNet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotNet, "outcome"));
        }

        Assert.Equal(expected.CaseId, RequiredString(value, "id"));
        Assert.Equal(expected.Executor, RequiredString(value, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(value, "symbol"));
        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        Assert.Equal(JsonValueKind.Object, facts.ValueKind);
        Assert.NotEmpty(facts.EnumerateObject());
    }

    private static string[] ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        ValidatePythonFacts(binding.CaseId, pythonFacts);
        return binding.Executor switch
        {
            "energy-model-surfaces" => ExecuteSurfacesCase(binding.CaseId),
            "energy-model-used-constructions" => ExecuteConstructionsCase(binding.CaseId),
            "energy-model-used-layers" => ExecuteLayersCase(binding.CaseId),
            "energy-model-used-profiles" => ExecuteProfilesCase(binding.CaseId),
            _ => throw new InvalidOperationException(
                "Unknown native projection executor: " + binding.Executor),
        };
    }

    private static void ValidatePythonFacts(string caseId, JsonElement facts)
    {
        string[] orderNames =
        {
            "Zulu", "Alpha", "Dragon", "Brick", "Omega", "한글", "🐉", "Glass", "Roof",
        };
        string[] orderHashes =
        {
            "-6911130904927632849", "-5489660660273336509", "8186683321401986332",
            "-2926469781815734489", "7551058807157025315", "3011526259676503552",
            "8889289909682346436", "5629188463992988249", "2500655868670704794",
        };

        if (caseId == ExpectedCases[0].CaseId)
        {
            AssertCommonFacts(
                facts,
                "input_zone_surface_indices",
                "output_surface_indices",
                "registry_labels");
            Assert.Empty(ReadJaggedIntArray(facts.GetProperty("input_zone_surface_indices")));
            AssertIntArray(facts.GetProperty("output_surface_indices"));
            AssertStringArray(facts.GetProperty("registry_labels"));
            return;
        }

        if (caseId == ExpectedCases[1].CaseId)
        {
            AssertCommonFacts(
                facts,
                "input_zone_surface_indices",
                "output_surface_indices",
                "registry_labels");
            Assert.Equal(
                new[] { new[] { 0, 1 }, Array.Empty<int>(), new[] { 1, 2, 0 } },
                ReadJaggedIntArray(facts.GetProperty("input_zone_surface_indices")));
            AssertIntArray(facts.GetProperty("output_surface_indices"), 0, 1, 1, 2, 0);
            AssertStringArray(facts.GetProperty("registry_labels"), "A", "B", "C");
            return;
        }

        if (caseId == ExpectedCases[2].CaseId)
        {
            AssertCommonFacts(
                facts,
                "first_result_indices_after_mutation",
                "first_result_indices_before_mutation",
                "input_zone_surface_indices",
                "registry_labels",
                "returned_list_mutation_supported",
                "second_result_indices_after_mutation");
            AssertIntArray(facts.GetProperty("first_result_indices_after_mutation"), 2, 1, 0, 3);
            AssertIntArray(facts.GetProperty("first_result_indices_before_mutation"), 0, 1, 2);
            Assert.Equal(
                new[] { new[] { 0, 1 }, new[] { 2 } },
                ReadJaggedIntArray(facts.GetProperty("input_zone_surface_indices")));
            AssertStringArray(facts.GetProperty("registry_labels"), "A", "B", "C", "RESULT-ONLY");
            Assert.True(facts.GetProperty("returned_list_mutation_supported").GetBoolean());
            AssertIntArray(facts.GetProperty("second_result_indices_after_mutation"), 0, 1, 2);
            return;
        }

        if (caseId == ExpectedCases[3].CaseId)
        {
            AssertCommonFacts(
                facts,
                "construction_registry",
                "equality",
                "input_registry_indices",
                "output_labels",
                "output_registry_indices");
            AssertHashRegistry(
                facts.GetProperty("construction_registry"),
                new[] { "first-equal", "later-equal", "same-name-unequal", "other" },
                new[] { "Shared", "Shared", "Shared", "Other" },
                new[]
                {
                    "-3612718561660722853", "-3612718561660722853",
                    "-3612718561660722853", "-8767484776472450951",
                });
            JsonElement equality = facts.GetProperty("equality");
            AssertKeys(equality, "first_equals_later", "first_equals_same_name_unequal");
            Assert.True(equality.GetProperty("first_equals_later").GetBoolean());
            Assert.False(equality.GetProperty("first_equals_same_name_unequal").GetBoolean());
            AssertIntArray(facts.GetProperty("input_registry_indices"), 0, 1, 2, 3, 0);
            AssertStringArray(facts.GetProperty("output_labels"), "other", "same-name-unequal", "first-equal");
            AssertIntArray(facts.GetProperty("output_registry_indices"), 3, 2, 0);
            return;
        }

        if (caseId == ExpectedCases[4].CaseId)
        {
            AssertCommonFacts(
                facts,
                "construction_registry",
                "input_filtered_labels",
                "input_kinds",
                "output_labels",
                "output_registry_indices");
            Assert.Empty(facts.GetProperty("construction_registry").EnumerateArray());
            AssertStringArray(facts.GetProperty("input_filtered_labels"), "air-a", "no-mass", "air-b", "air-a");
            AssertStringArray(
                facts.GetProperty("input_kinds"),
                "air-boundary",
                "no-mass",
                "air-boundary",
                "air-boundary");
            AssertStringArray(facts.GetProperty("output_labels"));
            AssertIntArray(facts.GetProperty("output_registry_indices"));
            return;
        }

        if (caseId == ExpectedCases[5].CaseId)
        {
            AssertCommonFacts(
                facts,
                "construction_registry",
                "input_registry_indices",
                "output_labels",
                "output_registry_indices");
            AssertHashRegistry(
                facts.GetProperty("construction_registry"),
                Enumerable.Range(0, 9).Select(index => "c" + index.ToString(CultureInfo.InvariantCulture)).ToArray(),
                orderNames,
                orderHashes);
            AssertIntArray(facts.GetProperty("input_registry_indices"), Enumerable.Range(0, 9).ToArray());
            AssertStringArray(facts.GetProperty("output_labels"), "c5", "c4", "c1", "c6", "c3", "c0", "c7", "c8", "c2");
            AssertIntArray(facts.GetProperty("output_registry_indices"), 5, 4, 1, 6, 3, 0, 7, 8, 2);
            return;
        }

        if (caseId == ExpectedCases[6].CaseId)
        {
            AssertCommonFacts(
                facts,
                "construction_input_kinds",
                "construction_input_labels",
                "layer_registry",
                "output_labels",
                "output_layer_indices");
            AssertStringArray(
                facts.GetProperty("construction_input_kinds"),
                "air-boundary",
                "no-mass",
                "air-boundary");
            AssertStringArray(
                facts.GetProperty("construction_input_labels"),
                "air-a",
                "no-mass",
                "air-b");
            Assert.Empty(facts.GetProperty("layer_registry").EnumerateArray());
            AssertStringArray(facts.GetProperty("output_labels"));
            AssertIntArray(facts.GetProperty("output_layer_indices"));
            return;
        }

        if (caseId == ExpectedCases[7].CaseId)
        {
            AssertCommonFacts(
                facts,
                "equality",
                "layer_registry",
                "output_labels",
                "output_layer_indices",
                "python_flattened_layer_indices",
                "python_used_construction_indices");
            JsonElement equality = facts.GetProperty("equality");
            AssertKeys(
                equality,
                "base_equals_equal_different_name",
                "base_equals_exact_duplicate",
                "base_equals_same_name_different_thickness");
            Assert.True(equality.GetProperty("base_equals_equal_different_name").GetBoolean());
            Assert.True(equality.GetProperty("base_equals_exact_duplicate").GetBoolean());
            Assert.False(equality.GetProperty("base_equals_same_name_different_thickness").GetBoolean());
            AssertHashRegistry(
                facts.GetProperty("layer_registry"),
                new[] { "base", "equal-different-name", "same-name-different-thickness", "exact-duplicate" },
                new[] { "Core-A", "Core-B", "Core-A", "Core-A" },
                new[] { "-276500280528783050", "-8620372976408521596", "-276500280528783050", "-276500280528783050" });
            AssertStringArray(facts.GetProperty("output_labels"), "same-name-different-thickness", "equal-different-name", "base");
            AssertIntArray(facts.GetProperty("output_layer_indices"), 2, 1, 0);
            AssertIntArray(facts.GetProperty("python_flattened_layer_indices"), 0, 1, 2, 3);
            AssertIntArray(facts.GetProperty("python_used_construction_indices"), 0);
            return;
        }

        if (caseId == ExpectedCases[8].CaseId)
        {
            AssertCommonFacts(
                facts,
                "construction_input_indices",
                "construction_registry",
                "layer_registry",
                "output_labels",
                "output_layer_indices",
                "python_flattened_layer_indices",
                "python_used_construction_indices");
            AssertIntArray(facts.GetProperty("construction_input_indices"), 0, 1, 2);
            AssertHashRegistry(
                facts.GetProperty("construction_registry"),
                new[] { "construction-zulu", "construction-alpha", "construction-dragon" },
                new[] { "Zulu", "Alpha", "Dragon" },
                orderHashes.Take(3).ToArray());
            AssertHashRegistry(
                facts.GetProperty("layer_registry"),
                Enumerable.Range(0, 9).Select(index => "l" + index.ToString(CultureInfo.InvariantCulture)).ToArray(),
                orderNames,
                orderHashes);
            AssertStringArray(facts.GetProperty("output_labels"), "l5", "l4", "l6", "l1", "l3", "l0", "l7", "l8", "l2");
            AssertIntArray(facts.GetProperty("output_layer_indices"), 5, 4, 6, 1, 3, 0, 7, 8, 2);
            AssertIntArray(facts.GetProperty("python_flattened_layer_indices"), 3, 4, 5, 6, 7, 8, 0, 1, 2);
            AssertIntArray(facts.GetProperty("python_used_construction_indices"), 1, 2, 0);
            return;
        }

        if (caseId == ExpectedCases[9].CaseId)
        {
            AssertProfileFacts(
                facts,
                new[] { "Alpha", "alpha", "한글", "🐉" },
                new[] { "Alpha", "alpha", "한글", "🐉", "Alpha" },
                new[] { "alpha-first", "lower-alpha", "korean", "dragon", "alpha-last" },
                new[] { "alpha-last", "lower-alpha", "korean", "dragon" },
                new[] { 4, 1, 2, 3 });
            return;
        }

        if (caseId == ExpectedCases[10].CaseId)
        {
            AssertProfileFacts(
                facts,
                new[] { "Team", "Aux", "Core" },
                new[] { "Team", "Aux", "Team", "Core", "Aux" },
                new[] { "team-first", "aux-first", "team-last", "core-only", "aux-last" },
                new[] { "team-last", "aux-last", "core-only" },
                new[] { 2, 4, 3 });
            return;
        }

        Assert.Equal(ExpectedCases[11].CaseId, caseId);
        AssertProfileFacts(
            facts,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>());
    }

    private static void AssertCommonFacts(JsonElement facts, params string[] caseKeys)
    {
        AssertKeys(
            facts,
            caseKeys.Concat(new[]
            {
                "fresh_list_each_access",
                "result_type",
                "selected_objects_are_registry_objects",
                "source_lists_unchanged",
            }).ToArray());
        Assert.True(facts.GetProperty("fresh_list_each_access").GetBoolean());
        Assert.Equal("list", RequiredString(facts, "result_type"));
        Assert.True(facts.GetProperty("selected_objects_are_registry_objects").GetBoolean());
        Assert.True(facts.GetProperty("source_lists_unchanged").GetBoolean());
    }

    private static void AssertProfileFacts(
        JsonElement facts,
        string[] firstSeen,
        string[] inputNames,
        string[] registryLabels,
        string[] outputLabels,
        int[] outputIndices)
    {
        AssertCommonFacts(
            facts,
            "first_seen_name_order",
            "input_names",
            "output_labels",
            "output_profile_indices",
            "profile_registry_labels");
        AssertStringArray(facts.GetProperty("first_seen_name_order"), firstSeen);
        AssertStringArray(facts.GetProperty("input_names"), inputNames);
        AssertStringArray(facts.GetProperty("profile_registry_labels"), registryLabels);
        AssertStringArray(facts.GetProperty("output_labels"), outputLabels);
        AssertIntArray(facts.GetProperty("output_profile_indices"), outputIndices);
    }

    private static void AssertHashRegistry(
        JsonElement value,
        string[] labels,
        string[] names,
        string[] hashes)
    {
        JsonElement[] entries = value.EnumerateArray().ToArray();
        Assert.Equal(labels.Length, entries.Length);
        Assert.Equal(labels.Length, names.Length);
        Assert.Equal(labels.Length, hashes.Length);
        for (int index = 0; index < entries.Length; index++)
        {
            AssertKeys(entries[index], "hash_decimal", "label", "name");
            Assert.Equal(hashes[index], RequiredString(entries[index], "hash_decimal"));
            Assert.Equal(labels[index], RequiredString(entries[index], "label"));
            Assert.Equal(names[index], RequiredString(entries[index], "name"));
        }
    }

    private static string[] ExecuteSurfacesCase(string caseId)
    {
        OpaqueConstruction construction = Construction("Surface construction", "Surface layer");
        if (caseId.EndsWith("empty-fresh", StringComparison.Ordinal))
        {
            var empty = new EnergyModel("Empty surface projection", Array.Empty<Zone>());
            AssertProjectionView(() => empty.Surfaces, Array.Empty<Surface>());
            return ProjectionFacts(Array.Empty<int>());
        }

        Surface[] registry =
        {
            Surface("SURFACE-A", "A", construction),
            Surface("SURFACE-B", "B", construction),
            Surface("SURFACE-C", "C", construction),
            Surface("SURFACE-RESULT-ONLY", "RESULT-ONLY", construction),
        };
        ZoneProfile profile = Profile("PROFILE-SURFACES", "Surfaces profile");
        if (caseId.EndsWith("flatten-order-identity", StringComparison.Ordinal))
        {
            var model = new EnergyModel(
                "Flattened surface projection",
                new[]
                {
                    Zone("ZONE-SURFACES-A", "Zone A", profile, registry[0], registry[1]),
                    Zone("ZONE-SURFACES-EMPTY", "Zone empty", profile),
                    Zone("ZONE-SURFACES-C", "Zone C", profile, registry[1], registry[2], registry[0]),
                });
            int[] expected = { 0, 1, 1, 2, 0 };
            AssertProjectionView(() => model.Surfaces, expected.Select(index => registry[index]).ToArray());
            Assert.Equal(expected, IdentityIndices(registry, model.Surfaces));
            return ProjectionFacts(expected);
        }

        Assert.EndsWith("result-mutation-isolated", caseId, StringComparison.Ordinal);
        var mutationModel = new EnergyModel(
            "Mutation-isolated surface projection",
            new[]
            {
                Zone("ZONE-SURFACES-MUTATE-A", "Zone A", profile, registry[0], registry[1]),
                Zone("ZONE-SURFACES-MUTATE-B", "Zone B", profile, registry[2]),
            });
        int[] mutationExpected = { 0, 1, 2 };
        IReadOnlyList<Surface> first = mutationModel.Surfaces;
        IList<Surface> mutableView = Assert.IsAssignableFrom<IList<Surface>>(first);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(registry[3]));
        Assert.Throws<NotSupportedException>(() => mutableView.RemoveAt(0));
        Assert.Equal(mutationExpected, IdentityIndices(registry, mutationModel.Surfaces));
        AssertProjectionView(
            () => mutationModel.Surfaces,
            mutationExpected.Select(index => registry[index]).ToArray());
        return new[]
        {
            "native-count=3;native-order=0,1,2",
            "native-identity=source-registry;native-source-unchanged=true",
            "native-fresh=true;native-read-only=true;returned-mutation=blocked",
        };
    }

    private static string[] ExecuteConstructionsCase(string caseId)
    {
        if (caseId.EndsWith("empty-filtered", StringComparison.Ordinal))
        {
            ZoneProfile profile = Profile("PROFILE-CONSTRUCTIONS-EMPTY", "Construction empty profile");
            var model = new EnergyModel(
                "Filtered construction projection",
                new[]
                {
                    Zone(
                        "ZONE-CONSTRUCTIONS-EMPTY",
                        "Construction empty zone",
                        profile,
                        Surface("SURFACE-AIR-A", "air-a", new AirBoundary("Air A")),
                        Surface(
                            "SURFACE-NOMASS",
                            "no-mass",
                            new NoMassConstruction("No mass", 2.5)),
                        Surface("SURFACE-AIR-B", "air-b", new AirBoundary("Air B")),
                        Surface("SURFACE-AIR-A2", "air-a-repeat", new AirBoundary("Air A"))),
                });
            AssertProjectionView(() => model.UsedConstructions, Array.Empty<OpaqueConstruction>());
            return FilteredProjectionFacts();
        }

        if (caseId.EndsWith("collision-dedup", StringComparison.Ordinal))
        {
            Material baseMaterial = Material("Shared material", conductivity: 0.72);
            Material secondaryVariant = new(
                "Shared material",
                0.72,
                1920,
                840,
                thermalAbsorptance: 0.2,
                solarAbsorptance: 0.3,
                visibleAbsorptance: 0.4,
                roughness: MaterialRoughness.VeryRough);
            OpaqueConstruction[] registry =
            {
                new("Shared", new[] { new Layer("first", baseMaterial, 0.1) }),
                new("Shared", new[] { new Layer("later", secondaryVariant, 0.1) }),
                new("Shared", new[] { new Layer("unequal", baseMaterial, 0.2) }),
                new("Other", new[] { new Layer("other", baseMaterial, 0.1) }),
            };
            Assert.True(registry[0].Equals(registry[1]));
            Assert.False(registry[0].Equals(registry[2]));
            var input = new[] { registry[0], registry[1], registry[2], registry[3], registry[0] };
            EnergyModel model = ModelWithHostConstructions("Collision constructions", input);
            int[] expected = { 0, 2, 3 };
            AssertProjectionView(
                () => model.UsedConstructions,
                expected.Select(index => registry[index]).ToArray());
            Assert.Equal(expected, IdentityIndices(registry, model.UsedConstructions));
            return ProjectionFacts(expected);
        }

        Assert.EndsWith("hash-order-resize", caseId, StringComparison.Ordinal);
        string[] names =
        {
            "Zulu", "Alpha", "Dragon", "Brick", "Omega", "한글", "🐉", "Glass", "Roof",
        };
        OpaqueConstruction[] resizeRegistry = names
            .Select((name, index) => Construction(name, "layer-" + index.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        EnergyModel resizeModel = ModelWithHostConstructions("Resize constructions", resizeRegistry);
        int[] resizeExpected = Enumerable.Range(0, 9).ToArray();
        AssertProjectionView(() => resizeModel.UsedConstructions, resizeRegistry);
        Assert.Equal(resizeExpected, IdentityIndices(resizeRegistry, resizeModel.UsedConstructions));
        return ProjectionFacts(resizeExpected);
    }

    private static string[] ExecuteLayersCase(string caseId)
    {
        if (caseId.EndsWith("empty-fresh", StringComparison.Ordinal))
        {
            ZoneProfile profile = Profile("PROFILE-LAYERS-EMPTY", "Layer empty profile");
            var empty = new EnergyModel(
                "Empty layer projection",
                new[]
                {
                    Zone(
                        "ZONE-LAYERS-EMPTY",
                        "Layer empty zone",
                        profile,
                        Surface("SURFACE-LAYER-AIR-A", "air-a", new AirBoundary("Air A")),
                        Surface(
                            "SURFACE-LAYER-NOMASS",
                            "no-mass",
                            new NoMassConstruction("No mass", 2.5)),
                        Surface("SURFACE-LAYER-AIR-B", "air-b", new AirBoundary("Air B"))),
                });
            AssertProjectionView(() => empty.UsedLayers, Array.Empty<Layer>());
            return FilteredProjectionFacts();
        }

        Material baseMaterial = Material("Brick", conductivity: 0.72);
        if (caseId.EndsWith("hash-equality-mismatch", StringComparison.Ordinal))
        {
            Layer[] registry =
            {
                new("Core-A", baseMaterial, 0.1),
                new("Core-B", baseMaterial, 0.1),
                new("Core-A", baseMaterial, 0.2),
                new("Core-A", baseMaterial, 0.1),
            };
            Assert.True(registry[0].Equals(registry[1]));
            Assert.False(registry[0].Equals(registry[2]));
            Assert.True(registry[0].Equals(registry[3]));
            var construction = new OpaqueConstruction("Layer mismatch", registry);
            EnergyModel model = ModelWithHostConstructions("Layer mismatch projection", construction);
            int[] expected = { 0, 1, 2 };
            AssertProjectionView(() => model.UsedLayers, expected.Select(index => registry[index]).ToArray());
            Assert.Equal(expected, IdentityIndices(registry, model.UsedLayers));
            return ProjectionFacts(expected);
        }

        Assert.EndsWith("hash-order-resize", caseId, StringComparison.Ordinal);
        string[] names =
        {
            "Zulu", "Alpha", "Dragon", "Brick", "Omega", "한글", "🐉", "Glass", "Roof",
        };
        Layer[] resizeRegistry = names
            .Select((name, index) => new Layer(name, baseMaterial, 0.05 + (index * 0.01)))
            .ToArray();
        OpaqueConstruction[] constructions =
        {
            new("Zulu", resizeRegistry[0..3]),
            new("Alpha", resizeRegistry[3..6]),
            new("Dragon", resizeRegistry[6..9]),
        };
        EnergyModel resizeModel = ModelWithHostConstructions("Resize layer projection", constructions);
        int[] resizeExpected = Enumerable.Range(0, 9).ToArray();
        AssertProjectionView(() => resizeModel.UsedLayers, resizeRegistry);
        Assert.Equal(resizeExpected, IdentityIndices(resizeRegistry, resizeModel.UsedLayers));
        return ProjectionFacts(resizeExpected);
    }

    private static string[] ExecuteProfilesCase(string caseId)
    {
        if (caseId.EndsWith("empty-fresh", StringComparison.Ordinal))
        {
            var empty = new EnergyModel("Empty profile projection", Array.Empty<Zone>());
            AssertProjectionView(() => empty.UsedProfiles, Array.Empty<ZoneProfile>());
            return ProjectionFacts(Array.Empty<int>());
        }

        string[] names;
        string[] labels;
        int[] expected;
        if (caseId.EndsWith("duplicate-name-last-wins", StringComparison.Ordinal))
        {
            names = new[] { "Team", "Aux", "Team", "Core", "Aux" };
            labels = new[] { "team-first", "aux-first", "team-last", "core-only", "aux-last" };
            expected = new[] { 2, 4, 3 };
        }
        else
        {
            Assert.EndsWith("case-sensitive-unicode-replacement", caseId, StringComparison.Ordinal);
            names = new[] { "Alpha", "alpha", "한글", "🐉", "Alpha" };
            labels = new[] { "alpha-first", "lower-alpha", "korean", "dragon", "alpha-last" };
            expected = new[] { 4, 1, 2, 3 };
        }

        ZoneProfile[] registry = names
            .Select((name, index) => Profile("PROFILE-PROJECTION-" + index.ToString(CultureInfo.InvariantCulture), name))
            .ToArray();
        Zone[] zones = registry
            .Select((profile, index) => Zone(
                "ZONE-PROJECTION-" + index.ToString(CultureInfo.InvariantCulture),
                labels[index],
                profile))
            .ToArray();
        var model = new EnergyModel("Profile projection", zones);
        AssertProjectionView(
            () => model.UsedProfiles,
            expected.Select(index => registry[index]).ToArray());
        Assert.Equal(expected, IdentityIndices(registry, model.UsedProfiles));
        Assert.Equal(names, model.Zones.Select(zone => zone.Profile.Name));
        return ProjectionFacts(expected);
    }

    private static string[] ProjectionFacts(IReadOnlyList<int> order)
    {
        string joined = string.Join(",", order.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        return new[]
        {
            "native-count=" + order.Count.ToString(CultureInfo.InvariantCulture) + ";native-order=" + joined,
            "native-identity=source-registry;native-source-unchanged=true",
            "native-fresh=true;native-read-only=true",
        };
    }

    private static string[] FilteredProjectionFacts() =>
        new[]
        {
            "native-count=0;native-order=",
            "native-filtered=air-boundary|no-mass;native-source-unchanged=true",
            "native-fresh=true;native-read-only=true",
        };

    private static void AssertProjectionView<T>(Func<IReadOnlyList<T>> read, IReadOnlyList<T> expected)
        where T : class
    {
        IReadOnlyList<T> first = read();
        IReadOnlyList<T> second = read();
        Assert.NotSame(first, second);
        Assert.Equal(expected.Count, first.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            Assert.Same(expected[index], first[index]);
            Assert.Same(expected[index], second[index]);
        }

        IList<T> mutableView = Assert.IsAssignableFrom<IList<T>>(first);
        Assert.True(mutableView.IsReadOnly);
        T? candidate = expected.Count == 0 ? null : expected[0];
        if (candidate is not null)
        {
            Assert.Throws<NotSupportedException>(() => mutableView.Add(candidate));
        }
    }

    private static int[] IdentityIndices<T>(IReadOnlyList<T> registry, IReadOnlyList<T> selected)
        where T : class
    {
        return selected.Select(item =>
        {
            int[] matches = registry
                .Select((candidate, index) => new { candidate, index })
                .Where(pair => ReferenceEquals(pair.candidate, item))
                .Select(pair => pair.index)
                .ToArray();
            Assert.Single(matches);
            return matches[0];
        }).ToArray();
    }

    private static EnergyModel ModelWithHostConstructions(
        string name,
        params OpaqueConstruction[] constructions)
    {
        ZoneProfile profile = Profile("PROFILE-" + Slug(name), name + " profile");
        Surface[] surfaces = constructions
            .Select((construction, index) => Surface(
                "SURFACE-" + Slug(name) + "-" + index.ToString(CultureInfo.InvariantCulture),
                "host-" + index.ToString(CultureInfo.InvariantCulture),
                construction))
            .ToArray();
        return new EnergyModel(
            name,
            new[] { Zone("ZONE-" + Slug(name), name + " zone", profile, surfaces) });
    }

    private static ZoneProfile Profile(string id, string name) =>
        new(new EntityId(id), name);

    private static Zone Zone(
        string id,
        string name,
        ZoneProfile profile,
        params Surface[] surfaces) =>
        new(new EntityId(id), name, surfaces, profile);

    private static Surface Surface(
        string id,
        string name,
        ISurfaceConstruction construction) =>
        new(
            new EntityId(id),
            name,
            SurfaceType.Wall,
            construction,
            SurfaceBoundary.Outdoors,
            TestDomainFactory.Square(2));

    private static Material Material(string name, double conductivity) =>
        new(name, conductivity, 1920, 840);

    private static OpaqueConstruction Construction(string name, string layerName) =>
        new(
            name,
            new[]
            {
                new Layer(layerName, Material(layerName + " material", 0.72), 0.1),
            });

    private static string Slug(string value) => Regex.Replace(value.ToUpperInvariant(), "[^A-Z0-9]+", "-").Trim('-');

    private static void ValidateReceipt(
        JsonElement receipt,
        EvidenceBinding evidence,
        IReadOnlyList<NativeObservation> expectedObservations)
    {
        AssertKeys(
            receipt,
            "fixture",
            "observations",
            "upstream_path",
            "upstream_symbol");
        Assert.Equal(evidence.Path, RequiredString(receipt, "upstream_path"));
        Assert.Equal(evidence.Symbol, RequiredString(receipt, "upstream_symbol"));

        JsonElement fixture = receipt.GetProperty("fixture");
        AssertKeys(fixture, "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(ExpectedCaseCount, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));

        JsonElement[] observations = receipt.GetProperty("observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(3, observations.Length);
        Assert.Equal(
            observations.Select(item => RequiredString(item, "case_id"))
                .OrderBy(item => item, StringComparer.Ordinal),
            observations.Select(item => RequiredString(item, "case_id")));
        for (int index = 0; index < observations.Length; index++)
        {
            JsonElement observation = observations[index];
            NativeObservation expected = expectedObservations[index];
            AssertKeys(
                observation,
                "adaptation_id",
                "case_id",
                "native_facts",
                "native_outcome");
            Assert.Equal(expected.CaseId, RequiredString(observation, "case_id"));
            Assert.Equal("returned", RequiredString(observation, "native_outcome"));
            if (expected.AdaptationId is null)
            {
                Assert.Equal(JsonValueKind.Null, observation.GetProperty("adaptation_id").ValueKind);
            }
            else
            {
                Assert.Equal(expected.AdaptationId, RequiredString(observation, "adaptation_id"));
            }

            Assert.Equal(
                expected.NativeFacts,
                observation.GetProperty("native_facts")
                    .EnumerateArray()
                    .Select(item => item.GetString()!)
                    .ToArray());
        }

        AssertReceiptPayloadSafe(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
    }

    private static void AssertIntArray(JsonElement value, params int[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetInt32()));
    }

    private static int[][] ReadJaggedIntArray(JsonElement value)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray()
            .Select(item =>
            {
                Assert.Equal(JsonValueKind.Array, item.ValueKind);
                return item.EnumerateArray().Select(child => child.GetInt32()).ToArray();
            })
            .ToArray();
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
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
                foreach (JsonProperty property in value.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
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
                throw new Xunit.Sdk.XunitException(
                    "Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
    }

    private static void AppendPythonJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
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

    private static void AssertReceiptPayloadSafe(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.False(property.Name is
                    "active_load" or
                    "claims_active_load" or
                    "classification" or
                    "environment" or
                    "expected_dotnet" or
                    "policy" or
                    "python" or
                    "python_facts" or
                    "python_outcome");
                AssertReceiptPayloadSafe(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertReceiptPayloadSafe(item);
            }
        }
    }

    private static void AssertNoRawAddresses(string value)
    {
        Assert.False(Regex.IsMatch(
            value,
            @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])",
            RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(
            value,
            @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])",
            RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(
            value,
            @"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d",
            RegexOptions.CultureInvariant));
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            Assert.False(Regex.IsMatch(
                text,
                @"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))",
                RegexOptions.CultureInvariant));
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

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject()
            .Select(item => item.Name)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal), actual);
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
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

        throw new FileNotFoundException(
            "Could not locate repository file '" + relativePath + "'.");
    }

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record EvidenceBinding(
        string Path,
        string Symbol,
        string SymbolHash,
        string AssertionId);

    private sealed record SymbolContract(
        string Symbol,
        string Kind,
        string SignatureHash,
        string BodyHash,
        string Classification,
        string? AdaptationId,
        string ImplementationSymbol);

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string? AdaptationId,
        IReadOnlyList<string> NativeFacts);
}
