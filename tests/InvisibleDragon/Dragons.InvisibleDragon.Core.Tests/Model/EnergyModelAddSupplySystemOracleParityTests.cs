using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class EnergyModelAddSupplySystemOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-model-add-supply-system-oracle.json";
    private const string OracleSchema =
        "dragons.python-reference.dragon-model-add-supply-system.v1";
    private const string OracleSha256 =
        "sha256:42ad2d75ce91edd153bd9e07382a03b5095ea0300df227f87e0d0147b377230f";
    private const string CasesSha256 =
        "sha256:ac58c4020edba588dceb8793b42552d261eb6686975bee1b553e9d8697d9cc2d";
    private const int OracleByteLength = 15_119;
    private const int ExpectedCaseCount = 3;
    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamPath = "src/idragon/dragon/model.py";
    private const string UpstreamSymbol = "EnergyModel.add_supply_system";
    private const string AdaptationId = "model-context-supply-system-assembly";
    private const string AssertionId = "dragon-model-add-supply-system-174532d0";
    private const string NativeTarget = "EnergyModel.ToIdfDocument";
    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs";
    private const string ImplementationSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendHvac";
    private const string ImplementationSha256 =
        "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.EnergyModelAddSupplySystemOracleParityTests.MatchesPinnedPythonAddSupplySystemAndNativeModelContextAssembly";

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-model-add-supply-system.add-supply-system.append-then-processor-failure", "energy-model-add-supply-system"),
        new("dragon-model-add-supply-system.add-supply-system.generation-failure-before-mutation", "energy-model-add-supply-system"),
        new("dragon-model-add-supply-system.add-supply-system.success-return-and-order", "energy-model-add-supply-system"),
    };

    private static readonly SourceBinding[] ExpectedSources =
    {
        new("idragon", "src/idragon/__init__.py", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618"),
        new("idragon.common", "src/idragon/common.py", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9"),
        new("idragon.constants", "src/idragon/constants.py", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"),
        new("idragon.dragon", "src/idragon/dragon/__init__.py", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52"),
        new("idragon.dragon.construction", "src/idragon/dragon/construction.py", "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"),
        new("idragon.dragon.hvac", "src/idragon/dragon/hvac.py", "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0", "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"),
        new("idragon.dragon.model", "src/idragon/dragon/model.py", "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"),
        new("idragon.dragon.profile", "src/idragon/dragon/profile.py", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef"),
        new("idragon.dragon.shape", "src/idragon/dragon/shape.py", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"),
        new("idragon.imugi", "src/idragon/imugi.py", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"),
        new("idragon.launcher", "src/idragon/launcher.py", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e"),
        new("idragon.utils", "src/idragon/utils.py", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452"),
    };

    [Fact]
    public void MatchesPinnedPythonAddSupplySystemAndNativeModelContextAssembly()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(OracleRepositoryPath));
        string sha256 = Sha256(bytes);
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, sha256);

        using JsonDocument oracle = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        ValidateNativeBindings();

        NativeObservation[] observations = cases
            .Select((item, index) => new NativeObservation(
                ExpectedCases[index].CaseId,
                AdaptationId,
                ExecuteNativeCase(ExpectedCases[index], item.GetProperty("python").GetProperty("facts"))))
            .ToArray();
        Assert.Equal(ExpectedCaseCount, observations.Length);
        Assert.All(observations, observation =>
        {
            Assert.Equal(AdaptationId, observation.AdaptationId);
            Assert.Equal(7, observation.NativeFacts.Length);
            Assert.Equal(7, observation.NativeFacts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(observation.NativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        });

        var receipt = new
        {
            fixture = new
            {
                case_count = ExpectedCaseCount,
                cases_sha256 = CasesSha256,
                path = OracleRepositoryPath,
                sha256,
            },
            native_binding = new
            {
                adaptation_id = AdaptationId,
                implementation_path = ImplementationRepositoryPath,
                implementation_sha256 = ImplementationSha256,
                implementation_symbol = ImplementationSymbol,
                public_target = NativeTarget,
            },
            observations = observations.Select(item => new
            {
                adaptation_id = item.AdaptationId,
                case_id = item.CaseId,
                native_facts = item.NativeFacts,
                native_outcome = "returned",
            }).ToArray(),
            upstream_path = UpstreamPath,
            upstream_symbol = UpstreamSymbol,
        };
        JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
        ValidateReceipt(receiptJson, observations);
        TrustedEvidenceRecorder.Record(AssertionId, EvidenceTestCase, "not_applicable", receipt);
    }

    private static JsonElement[] ValidateCorpus(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(root, "cases", "cases_sha256", "consumer_contract", "runtime", "schema", "symbols", "upstream");
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));
        AssertNoRawAddresses(root.GetRawText());
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);

        ValidateUpstream(root.GetProperty("upstream"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateSymbol(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

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

        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(upstream, "commit", "inventory_sha256", "loaded_local_modules", "sources");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventorySha256, RequiredString(upstream, "inventory_sha256"));

        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        JsonElement[] modules = upstream.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(ExpectedSources.Length, sources.Length);
        Assert.Equal(ExpectedSources.Length, modules.Length);
        for (int index = 0; index < ExpectedSources.Length; index++)
        {
            SourceBinding expected = ExpectedSources[index];
            JsonElement source = sources[index];
            AssertKeys(source, "ast_sha256", "path", "source_sha256");
            Assert.Equal(expected.Path, RequiredString(source, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(source, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(source, "ast_sha256"));

            JsonElement module = modules[index];
            AssertKeys(module, "ast_sha256", "module", "path", "source_sha256");
            Assert.Equal(expected.Module, RequiredString(module, "module"));
            Assert.Equal(expected.Path, RequiredString(module, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(module, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(module, "ast_sha256"));
        }

        Assert.Equal(
            sources.Select(item => RequiredString(item, "path")),
            modules.Select(item => RequiredString(item, "path")));
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

    private static void ValidateSymbol(JsonElement symbols)
    {
        JsonElement symbol = Assert.Single(symbols.EnumerateArray());
        AssertKeys(symbol, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
        Assert.Equal("sha256:6bf509a4d5050f54bd748c516ed98b6ae249edf3aaa84a75c4c7bd11b7fbef4b", RequiredString(symbol, "body_hash"));
        Assert.Equal("function", RequiredString(symbol, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
        Assert.Equal("sha256:576bb4584970582d94ae80ad061612e84dad263321a9e6288b39a92af7cd959f", RequiredString(symbol, "signature_hash"));
        Assert.Equal(UpstreamSymbol, RequiredString(symbol, "symbol"));
        Assert.Equal("sha256:174532d0aa6b76826dd78f3d7020ba49eeba26494019da3fb361396e31c15a94", RequiredString(symbol, "symbol_hash"));
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
            "closure",
            "identity_encoding",
            "native_targets",
            "source_import_policy",
            "state_encoding",
            "target_symbols");
        AssertSingleMapping(contract.GetProperty("adaptations"), UpstreamSymbol, AdaptationId);
        AssertSingleMapping(contract.GetProperty("assertion_ids"), UpstreamSymbol, AssertionId);
        AssertSingleMapping(contract.GetProperty("classifications"), UpstreamSymbol, "exception");
        AssertSingleMapping(contract.GetProperty("native_targets"), UpstreamSymbol, NativeTarget);
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(contract.GetProperty("target_symbols"), UpstreamSymbol);
        Assert.Equal("logical-labels-only-no-id-or-address", RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "external-temporary-copy-with-complete-loaded-local-module-audit",
            RequiredString(contract, "source_import_policy"));
        Assert.Equal("ordered-logical-events-and-object-names", RequiredString(contract, "state_encoding"));

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(closure, "full_symbol_closure", "scope", "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal("bounded-reviewed-adaptation-evidence", RequiredString(closure, "scope"));
        AssertStringArray(
            closure.GetProperty("unresolved_behavior"),
            "EnergyModel.to_idf",
            "SupplyGroup",
            "concrete-supply-systems",
            "supply-system-postprocessors");
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Executor, RequiredString(item, "executor"));
        Assert.Equal(UpstreamSymbol, RequiredString(item, "symbol"));

        JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(AdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        JsonElement facts = python.GetProperty("facts");
        AssertKeys(
            facts,
            "append_call_count",
            "error",
            "events",
            "mutation_state",
            "processor_labels_run",
            "return",
            "supply_generation_count",
            "unreached_processor_ran",
            "zone_names_after");
        Assert.Equal(1, facts.GetProperty("supply_generation_count").GetInt32());
        Assert.False(facts.GetProperty("unreached_processor_ran").GetBoolean());

        if (expected.CaseId.EndsWith("append-then-processor-failure", StringComparison.Ordinal))
        {
            Assert.Equal("raised", RequiredString(python, "outcome"));
            Assert.Equal(1, facts.GetProperty("append_call_count").GetInt32());
            Assert.Equal("appended-before-processor-error", RequiredString(facts, "mutation_state"));
            AssertError(facts.GetProperty("error"), "processor-failure: intentional failure after append", "processor-failure:");
            AssertReturn(facts.GetProperty("return"), "not-returned");
            AssertStringArray(facts.GetProperty("processor_labels_run"), "observer-before-failure", "failing-processor");
            AssertStringArray(facts.GetProperty("zone_names_after"), "Existing-Zone", "Failure-Appended-First", "Failure-Appended-Second");
            AssertPythonEvents(
                facts.GetProperty("events"),
                "Processor-Failure-Zone",
                "Failure-Appended-First",
                "Failure-Appended-Second",
                "observer-before-failure",
                "failing-processor");
        }
        else if (expected.CaseId.EndsWith("generation-failure-before-mutation", StringComparison.Ordinal))
        {
            Assert.Equal("raised", RequiredString(python, "outcome"));
            Assert.Equal(0, facts.GetProperty("append_call_count").GetInt32());
            Assert.Equal("unchanged-before-generation-error", RequiredString(facts, "mutation_state"));
            AssertError(facts.GetProperty("error"), "generation-failure: intentional failure before append", "generation-failure:");
            AssertReturn(facts.GetProperty("return"), "not-returned");
            Assert.Empty(facts.GetProperty("processor_labels_run").EnumerateArray());
            AssertStringArray(facts.GetProperty("zone_names_after"), "Existing-Zone");
            JsonElement generationEvent = Assert.Single(facts.GetProperty("events").EnumerateArray());
            AssertSupplyEvent(generationEvent, "Generation-Failure-Zone");
        }
        else
        {
            Assert.EndsWith("success-return-and-order", expected.CaseId, StringComparison.Ordinal);
            Assert.Equal("returned", RequiredString(python, "outcome"));
            Assert.Equal(1, facts.GetProperty("append_call_count").GetInt32());
            Assert.Equal("appended-before-ordered-processors", RequiredString(facts, "mutation_state"));
            AssertSingleMapping(facts.GetProperty("error"), "kind", "none");
            AssertReturn(facts.GetProperty("return"), "none");
            AssertStringArray(facts.GetProperty("processor_labels_run"), "first-processor", "second-processor");
            AssertStringArray(facts.GetProperty("zone_names_after"), "Existing-Zone", "Success-Appended-First", "Success-Appended-Second");
            AssertPythonEvents(
                facts.GetProperty("events"),
                "Success-Zone",
                "Success-Appended-First",
                "Success-Appended-Second",
                "first-processor",
                "second-processor");
        }
    }

    private static void AssertPythonEvents(
        JsonElement eventsValue,
        string zoneName,
        string firstObject,
        string secondObject,
        string firstProcessor,
        string secondProcessor)
    {
        JsonElement[] events = eventsValue.EnumerateArray().ToArray();
        Assert.Equal(4, events.Length);
        AssertSupplyEvent(events[0], zoneName);

        AssertKeys(events[1], "event", "objects");
        Assert.Equal("idf.append", RequiredString(events[1], "event"));
        JsonElement[] objects = events[1].GetProperty("objects").EnumerateArray().ToArray();
        Assert.Equal(2, objects.Length);
        AssertObjectLabel(objects[0], firstObject);
        AssertObjectLabel(objects[1], secondObject);

        AssertProcessorEvent(events[2], firstProcessor, firstObject, secondObject);
        AssertProcessorEvent(events[3], secondProcessor, firstObject, secondObject);
    }

    private static void AssertSupplyEvent(JsonElement value, string zoneName)
    {
        AssertKeys(value, "event", "zone_name");
        Assert.Equal("supply.to_idf_object", RequiredString(value, "event"));
        Assert.Equal(zoneName, RequiredString(value, "zone_name"));
    }

    private static void AssertObjectLabel(JsonElement value, string name)
    {
        AssertKeys(value, "name", "object_type");
        Assert.Equal(name, RequiredString(value, "name"));
        Assert.Equal("Zone", RequiredString(value, "object_type"));
    }

    private static void AssertProcessorEvent(
        JsonElement value,
        string processor,
        string firstObject,
        string secondObject)
    {
        AssertKeys(value, "event", "processor", "zone_names");
        Assert.Equal("processor.run", RequiredString(value, "event"));
        Assert.Equal(processor, RequiredString(value, "processor"));
        AssertStringArray(value.GetProperty("zone_names"), "Existing-Zone", firstObject, secondObject);
    }

    private static void AssertError(JsonElement value, string message, string prefix)
    {
        AssertKeys(value, "args", "message", "message_prefix", "message_starts_with_prefix", "type");
        AssertStringArray(value.GetProperty("args"), message);
        Assert.Equal(message, RequiredString(value, "message"));
        Assert.Equal(prefix, RequiredString(value, "message_prefix"));
        Assert.True(value.GetProperty("message_starts_with_prefix").GetBoolean());
        Assert.Equal("RuntimeError", RequiredString(value, "type"));
    }

    private static void AssertReturn(JsonElement value, string kind)
    {
        AssertSingleMapping(value, "kind", kind);
    }

    private static void ValidateNativeBindings()
    {
        Assert.Equal(ImplementationSha256, Sha256(File.ReadAllBytes(FindRepositoryFile(ImplementationRepositoryPath))));

        MethodInfo[] publicTargets = typeof(EnergyModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(EnergyModel.ToIdfDocument))
            .ToArray();
        Assert.Single(publicTargets);
        Assert.DoesNotContain(
            publicTargets.SelectMany(method => method.GetParameters()),
            parameter => parameter.ParameterType == typeof(IdfDocument));

        MethodInfo? publicTarget = typeof(EnergyModel).GetMethod(
            nameof(EnergyModel.ToIdfDocument),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(IddSchema), typeof(EnergyModelIdfOptions) },
            modifiers: null);
        Assert.NotNull(publicTarget);
        Assert.Equal(typeof(IdfDocument), publicTarget.ReturnType);
        Assert.Equal(new[] { typeof(IddSchema), typeof(EnergyModelIdfOptions) }, publicTarget.GetParameters().Select(item => item.ParameterType));
        Assert.All(publicTarget.GetParameters(), parameter => Assert.True(parameter.HasDefaultValue));
        Assert.DoesNotContain(
            typeof(EnergyModel).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name is "AddSupplySystem" or "add_supply_system");

        Type? assemblerType = typeof(EnergyModel).Assembly.GetType(
            "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler",
            throwOnError: true);
        Assert.NotNull(assemblerType);
        Type assembler = assemblerType;
        MethodInfo appendHvac = Assert.Single(
            assembler.GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            method => method.Name == "AppendHvac");
        Assert.True(appendHvac.IsPrivate);
        Assert.True(appendHvac.IsStatic);
        Assert.Equal(typeof(void), appendHvac.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(IdfDocument),
                typeof(IdfGenerationContext),
                typeof(EnergyModel),
                typeof(EnergyModelIdfOptions),
                typeof(Dictionary<EntityId, EnergyRecoveryVentilator>),
            },
            appendHvac.GetParameters().Select(item => item.ParameterType));
    }

    private static string[] ExecuteNativeCase(CaseBinding expected, JsonElement pythonFacts)
    {
        if (expected.CaseId.EndsWith("append-then-processor-failure", StringComparison.Ordinal))
        {
            Assert.Equal("appended-before-processor-error", RequiredString(pythonFacts, "mutation_state"));
            return ObserveNativeFragmentCollectionFailure();
        }

        if (expected.CaseId.EndsWith("generation-failure-before-mutation", StringComparison.Ordinal))
        {
            Assert.Equal("unchanged-before-generation-error", RequiredString(pythonFacts, "mutation_state"));
            return ObserveNativeGenerationFailure();
        }

        Assert.EndsWith("success-return-and-order", expected.CaseId, StringComparison.Ordinal);
        Assert.Equal("appended-before-ordered-processors", RequiredString(pythonFacts, "mutation_state"));
        return ObserveNativeSuccess();
    }

    private static string[] ObserveNativeFragmentCollectionFailure()
    {
        Zone firstZone = ConditionedZone("NATIVE-CONFLICT-ZONE-A", "Native Conflict Zone A", x: 0);
        Zone secondZone = ConditionedZone("NATIVE-CONFLICT-ZONE-B", "Native Conflict Zone B", x: 3);
        var firstSource = new HeatPump(
            new EntityId("NATIVE-SOURCE-SAME"),
            "Native Source First",
            Fuel.Electricity,
            3,
            3);
        var secondSource = new HeatPump(
            new EntityId("NATIVE-SOURCE-SAME"),
            "Native Source Second",
            Fuel.Electricity,
            4,
            3);
        var firstTerminal = new AirHandlingUnit(
            new EntityId("NATIVE-AHU-A"),
            "Native AHU A",
            firstSource);
        var secondTerminal = new AirHandlingUnit(
            new EntityId("NATIVE-AHU-B"),
            "Native AHU B",
            secondSource);
        var model = new EnergyModel(
            "Native fragment collection failure",
            new[] { firstZone, secondZone },
            new[]
            {
                new ZoneHvacAssignment(firstZone.Id, new SupplyGroup(new[] { firstTerminal })),
                new ZoneHvacAssignment(secondZone.Id, new SupplyGroup(new[] { secondTerminal })),
            });
        ModelSnapshot snapshot = Capture(model);
        Assert.Contains(
            model.Validate().Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
        var options = new EnergyModelIdfOptions { ThrowOnValidationErrors = false };
        const string expectedMessage =
            "HVAC identifier 'NATIVE-SOURCE-SAME' has conflicting source definitions.";

        InvalidOperationException first = Assert.Throws<InvalidOperationException>(
            () => model.ToIdfDocument(options: options));
        Assert.Equal(expectedMessage, first.Message);
        AssertModelUnchanged(model, snapshot);
        InvalidOperationException second = Assert.Throws<InvalidOperationException>(
            () => model.ToIdfDocument(options: options));
        Assert.Equal(first.GetType(), second.GetType());
        Assert.Equal(first.Message, second.Message);
        AssertModelUnchanged(model, snapshot);

        return new[]
        {
            "python_stage=appended-before-processor-error",
            "native_public_target=EnergyModel.ToIdfDocument",
            $"native_exception={nameof(InvalidOperationException)}:{expectedMessage}",
            "native_return=not-returned",
            "native_repeated_attempts=2-identical",
            "native_aggregate_membership_reference_identity=unchanged",
            "native_caller_owned_idf_input=absent",
        };
    }

    private static string[] ObserveNativeGenerationFailure()
    {
        Zone zone = ConditionedZoneWithoutFloor(
            "NATIVE-GENERATION-FAILURE-ZONE",
            "Native Generation Failure Zone");
        var firstSystem = new ElectricRadiator(
            new EntityId("NATIVE-GENERATION-FIRST"),
            "Native Generated First",
            1_000);
        var boiler = new Boiler(
            new EntityId("NATIVE-GENERATION-BOILER"),
            "Native Generation Boiler",
            Fuel.NaturalGas);
        var failingSystem = new RadiantFloor(
            new EntityId("NATIVE-GENERATION-FAIL"),
            "Native Generation Fails",
            boiler);
        var group = new SupplyGroup(new SupplySystem[] { firstSystem, failingSystem });
        var model = new EnergyModel(
            "Native generation failure",
            new[] { zone },
            new[] { new ZoneHvacAssignment(zone.Id, group) });
        ModelSnapshot snapshot = Capture(model);
        Assert.Equal(new SupplySystem[] { firstSystem, failingSystem }, group.Systems);
        Assert.Contains(
            model.Validate().Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.ZONE.NO_FLOOR");
        const string expectedMessage =
            "Zone 'Native Generation Failure Zone' has no floor for radiant equipment.";

        InvalidOperationException first = Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument());
        Assert.Equal(expectedMessage, first.Message);
        AssertModelUnchanged(model, snapshot);
        InvalidOperationException second = Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument());
        Assert.Equal(first.GetType(), second.GetType());
        Assert.Equal(first.Message, second.Message);
        AssertModelUnchanged(model, snapshot);

        return new[]
        {
            "python_stage=unchanged-before-generation-error",
            "native_public_target=EnergyModel.ToIdfDocument",
            $"native_exception={nameof(InvalidOperationException)}:{expectedMessage}",
            "native_return=not-returned",
            "native_input_system_order=ElectricRadiator->RadiantFloor",
            "native_repeated_attempts=2-identical",
            "native_aggregate_membership_reference_identity=unchanged",
        };
    }

    private static string[] ObserveNativeSuccess()
    {
        Zone zone = ConditionedZone("NATIVE-SUCCESS-ZONE", "Native Success Zone", x: 0);
        var firstSystem = new ElectricRadiator(
            new EntityId("NATIVE-SUCCESS-FIRST"),
            "Native Success First",
            1_000);
        var secondSystem = new ElectricRadiator(
            new EntityId("NATIVE-SUCCESS-SECOND"),
            "Native Success Second",
            2_000);
        var group = new SupplyGroup(new SupplySystem[] { firstSystem, secondSystem });
        var model = new EnergyModel(
            "Native assembly success",
            new[] { zone },
            new[] { new ZoneHvacAssignment(zone.Id, group) });
        ModelSnapshot snapshot = Capture(model);

        IdfDocument firstDocument = model.ToIdfDocument();
        AssertModelUnchanged(model, snapshot);
        IdfDocument secondDocument = model.ToIdfDocument();
        AssertModelUnchanged(model, snapshot);
        Assert.NotSame(firstDocument, secondDocument);
        Assert.Equal(DocumentFingerprint(firstDocument), DocumentFingerprint(secondDocument));

        string firstName = firstSystem.ObjectNameFor(zone);
        string secondName = secondSystem.ObjectNameFor(zone);
        string firstFraction = $"heating_fraction_for_{firstName}";
        string secondFraction = $"heating_fraction_for_{secondName}";
        IdfObject[] radiators = firstDocument["ZoneHVAC:Baseboard:RadiantConvective:Electric"].ToArray();
        Assert.Equal(new[] { firstName, secondName }, radiators.Select(item => item.Name));
        IdfObject[] secondRadiators = secondDocument["ZoneHVAC:Baseboard:RadiantConvective:Electric"].ToArray();
        Assert.Equal(new[] { firstName, secondName }, secondRadiators.Select(item => item.Name));
        Assert.NotSame(radiators[0], secondRadiators[0]);
        Assert.NotSame(radiators[1], secondRadiators[1]);

        IdfObject[] fractions = firstDocument["Schedule:Compact"]
            .Where(item => item.Name == firstFraction || item.Name == secondFraction)
            .ToArray();
        Assert.Equal(new[] { firstFraction, secondFraction }, fractions.Select(item => item.Name));
        IdfObject equipment = Assert.Single(firstDocument["ZoneHVAC:EquipmentList"]);
        Assert.Equal(
            new[]
            {
                $"EquipmentList_for_{zone.Name}",
                "SequentialLoad",
                "ZoneHVAC:Baseboard:RadiantConvective:Electric",
                firstName,
                "1",
                "1",
                "ALLOFF",
                firstFraction,
                "ZoneHVAC:Baseboard:RadiantConvective:Electric",
                secondName,
                "2",
                "2",
                "ALLOFF",
                secondFraction,
            },
            equipment.Fields.Select(field => field.Value));
        Assert.True(IndexOf(firstDocument, radiators[0]) < IndexOf(firstDocument, radiators[1]));
        Assert.True(IndexOf(firstDocument, radiators[1]) < IndexOf(firstDocument, fractions[0]));
        Assert.True(IndexOf(firstDocument, fractions[0]) < IndexOf(firstDocument, fractions[1]));
        Assert.True(IndexOf(firstDocument, fractions[1]) < IndexOf(firstDocument, equipment));

        return new[]
        {
            "python_stage=appended-before-ordered-processors",
            "native_return=IdfDocument",
            "native_documents=fresh-distinct-and-deterministic",
            $"native_system_order={firstName}->{secondName}",
            $"native_append_order={firstName}->{secondName}->{firstFraction}->{secondFraction}->EquipmentList_for_{zone.Name}",
            $"native_equipment_refs=ALLOFF/{firstFraction};ALLOFF/{secondFraction}",
            "native_aggregate_membership_reference_identity=unchanged",
        };
    }

    private static Zone ConditionedZone(string id, string name, double x)
    {
        Surface floor = TestDomainFactory.Surface(
            $"{id}-FLOOR",
            $"{name} Floor",
            TestDomainFactory.Square(size: 2, x: x),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        return new Zone(
            new EntityId(id),
            name,
            new[] { floor },
            ConditionedProfile(id, name));
    }

    private static Zone ConditionedZoneWithoutFloor(string id, string name)
    {
        Surface roof = TestDomainFactory.Surface(
            $"{id}-ROOF",
            $"{name} Roof",
            TestDomainFactory.Square(size: 2, z: 3),
            SurfaceType.Ceiling,
            SurfaceBoundary.Outdoors);
        return new Zone(
            new EntityId(id),
            name,
            new[] { roof },
            ConditionedProfile(id, name));
    }

    private static ZoneProfile ConditionedProfile(string id, string name)
    {
        return new ZoneProfile(
            new EntityId($"{id}-PROFILE"),
            $"{name} Profile",
            Schedule.Constant($"{name} Heating", 20, ScheduleType.Temperature),
            Schedule.Constant($"{name} Cooling", 26, ScheduleType.Temperature),
            Schedule.Constant($"{name} HVAC Availability", 1, ScheduleType.OnOff));
    }

    private static ModelSnapshot Capture(EnergyModel model)
    {
        return new ModelSnapshot(
            model.Zones.ToArray(),
            model.HvacAssignments.ToArray(),
            model.HvacAssignments.Select(item => item.Supply).ToArray(),
            model.HvacAssignments.SelectMany(item => item.Supply.Systems).ToArray(),
            ModelFingerprint(model));
    }

    private static void AssertModelUnchanged(EnergyModel model, ModelSnapshot expected)
    {
        Assert.Equal(expected.Fingerprint, ModelFingerprint(model));
        AssertReferenceSequence(expected.Zones, model.Zones);
        AssertReferenceSequence(expected.Assignments, model.HvacAssignments);
        AssertReferenceSequence(expected.Groups, model.HvacAssignments.Select(item => item.Supply));
        AssertReferenceSequence(
            expected.Systems,
            model.HvacAssignments.SelectMany(item => item.Supply.Systems));
    }

    private static void AssertReferenceSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        where T : class
    {
        T[] expectedItems = expected.ToArray();
        T[] actualItems = actual.ToArray();
        Assert.Equal(expectedItems.Length, actualItems.Length);
        for (int index = 0; index < expectedItems.Length; index++)
        {
            Assert.Same(expectedItems[index], actualItems[index]);
        }
    }

    private static string ModelFingerprint(EnergyModel model)
    {
        return string.Join(
            "|",
            new[]
            {
                model.Name,
                string.Join(",", model.Zones.Select(zone => $"{zone.Id}:{zone.Name}:{zone.Surfaces.Count}:{zone.Profile.Name}")),
                string.Join(",", model.HvacAssignments.Select(assignment => $"{assignment.ZoneId}:{assignment.Supply.Systems.Count}")),
                string.Join(",", model.HvacAssignments.SelectMany(assignment => assignment.Supply.Systems).Select(system => $"{system.GetType().Name}:{system.Id}:{system.Name}")),
            });
    }

    private static string DocumentFingerprint(IdfDocument document)
    {
        return string.Join(
            "\n",
            document.Select(item => $"{item.ObjectType}|{string.Join("|", item.Fields.Select(field => field.Value))}"));
    }

    private static int IndexOf(IdfDocument document, IdfObject target)
    {
        for (int index = 0; index < document.Count; index++)
        {
            if (ReferenceEquals(document[index], target))
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException($"Object '{target.ObjectType}:{target.Name}' was not found by identity.");
    }

    private static void ValidateReceipt(JsonElement receipt, IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(
            receipt,
            "fixture",
            "native_binding",
            "observations",
            "upstream_path",
            "upstream_symbol");
        Assert.Equal(UpstreamPath, RequiredString(receipt, "upstream_path"));
        Assert.Equal(UpstreamSymbol, RequiredString(receipt, "upstream_symbol"));

        JsonElement fixture = receipt.GetProperty("fixture");
        AssertKeys(fixture, "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(ExpectedCaseCount, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));

        JsonElement binding = receipt.GetProperty("native_binding");
        AssertKeys(
            binding,
            "adaptation_id",
            "implementation_path",
            "implementation_sha256",
            "implementation_symbol",
            "public_target");
        Assert.Equal(AdaptationId, RequiredString(binding, "adaptation_id"));
        Assert.Equal(ImplementationRepositoryPath, RequiredString(binding, "implementation_path"));
        Assert.Equal(ImplementationSha256, RequiredString(binding, "implementation_sha256"));
        Assert.Equal(ImplementationSymbol, RequiredString(binding, "implementation_symbol"));
        Assert.Equal(NativeTarget, RequiredString(binding, "public_target"));

        JsonElement[] recorded = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(observations.Count, recorded.Length);
        for (int index = 0; index < recorded.Length; index++)
        {
            AssertKeys(recorded[index], "adaptation_id", "case_id", "native_facts", "native_outcome");
            Assert.Equal(AdaptationId, RequiredString(recorded[index], "adaptation_id"));
            Assert.Equal(observations[index].CaseId, RequiredString(recorded[index], "case_id"));
            Assert.Equal("returned", RequiredString(recorded[index], "native_outcome"));
            AssertStringArray(recorded[index].GetProperty("native_facts"), observations[index].NativeFacts);
        }
    }

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

    private sealed record CaseBinding(string CaseId, string Executor);

    private sealed record SourceBinding(
        string Module,
        string Path,
        string SourceSha256,
        string AstSha256);

    private sealed record NativeObservation(
        string CaseId,
        string AdaptationId,
        string[] NativeFacts);

    private sealed record ModelSnapshot(
        Zone[] Zones,
        ZoneHvacAssignment[] Assignments,
        SupplyGroup[] Groups,
        SupplySystem[] Systems,
        string Fingerprint);
}
