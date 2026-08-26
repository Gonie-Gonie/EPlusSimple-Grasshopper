using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.UpstreamTracker;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class SupplyGroupToIdfObjectOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-hvac-supply-group-to-idf-object-oracle.json";
    private const string OracleSchema =
        "goniegonie.python-reference.dragon-hvac-supply-group-to-idf-object.v1";
    private const string OracleSha256 =
        "sha256:f1c3454cdf34eed1a47180b13bacab2dadf04a06883a34c214738ed6ef50a608";
    private const string CasesSha256 =
        "sha256:8937d915b40bde81aff7b1481bf0d747a878dbefe464c28d091b4bb7d4ba8f0e";
    private const int OracleByteLength = 22_608;
    private const int ExpectedCaseCount = 3;
    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamPath = "src/idragon/dragon/hvac.py";
    private const string UpstreamSymbol = "SupplyGroup.to_idf_object";
    private const string AdaptationId = "model-context-supply-group-idf-assembly";
    private const string AssertionId = "dragon-hvac-supply-group-to-idf-object-3f9c508c";
    private const string NativeTarget = "EnergyModel.ToIdfDocument";
    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs";
    private const string ImplementationSymbol =
        "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendHvac";
    private const string ImplementationSha256 =
        "sha256:f4a5eab3c337fe8eeb12aeff0ffe0490c7d7cd5c2d89be16f88da4455167e2b3";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Hvac.SupplyGroupToIdfObjectOracleParityTests.MatchesPinnedPythonSupplyGroupToIdfObjectThroughNativeModelContext";

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-hvac-supply-group-to-idf-object.availability-failure.immediate-after-system"),
        new("dragon-hvac-supply-group-to-idf-object.success.flatten-order-controller-last-and-fresh-lists"),
        new("dragon-hvac-supply-group-to-idf-object.system-failure.prefix-before-failure"),
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
    public void MatchesPinnedPythonSupplyGroupToIdfObjectThroughNativeModelContext()
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
                ExecuteNativeCase(
                    ExpectedCases[index],
                    item.GetProperty("python").GetProperty("facts"))))
            .ToArray();
        Assert.Equal(ExpectedCaseCount, observations.Length);
        Assert.All(observations, observation =>
        {
            Assert.Equal(AdaptationId, observation.AdaptationId);
            Assert.True(observation.NativeFacts.Length >= 8);
            Assert.Equal(
                observation.NativeFacts.Length,
                observation.NativeFacts.Distinct(StringComparer.Ordinal).Count());
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
        Assert.Equal(
            "sha256:8660a470290bde21a0cc246e107e2362b5698153e7585ea05a1a69367b1342fa",
            RequiredString(symbol, "body_hash"));
        Assert.Equal("function", RequiredString(symbol, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
        Assert.Equal(
            "sha256:1dd75b2e8cc87cb78c35a6df6c2423c532b8ea9e29f24b53d113cdffdd42d2ec",
            RequiredString(symbol, "signature_hash"));
        Assert.Equal(UpstreamSymbol, RequiredString(symbol, "symbol"));
        Assert.Equal(
            "sha256:3f9c508c5b0d784d27bc327dfe65c84bd7d17ffc144615b852c37b59cbe51a41",
            RequiredString(symbol, "symbol_hash"));
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

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(closure, "full_symbol_closure", "scope", "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(
            "bounded-model-context-supply-group-idf-assembly-adaptation",
            RequiredString(closure, "scope"));
        AssertStringArray(
            closure.GetProperty("unresolved_behavior"),
            "SupplyGroup",
            "standalone-SupplyGroup-converter-API-shape",
            "SupplySystem.to_idf_object",
            "SourceSystem.to_idf_object",
            "SequentialLoadFractionController",
            "SequentialLoadFractionController.run",
            "concrete-supply-system-converters",
            "supply-system-postprocessor-run-behavior",
            "arbitrary-probe-systems-and-schedules",
            "EnergyModel.to_idf");
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal("supply-group-to-idf-object", RequiredString(item, "executor"));
        Assert.Equal(UpstreamSymbol, RequiredString(item, "symbol"));

        JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(AdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        JsonElement facts = python.GetProperty("facts");
        if (expected.CaseId.EndsWith("availability-failure.immediate-after-system", StringComparison.Ordinal))
        {
            Assert.Equal("raised", RequiredString(python, "outcome"));
            AssertAvailabilityFailureFacts(facts);
        }
        else if (expected.CaseId.EndsWith("success.flatten-order-controller-last-and-fresh-lists", StringComparison.Ordinal))
        {
            Assert.Equal("returned", RequiredString(python, "outcome"));
            AssertSuccessFacts(facts);
        }
        else
        {
            Assert.EndsWith("system-failure.prefix-before-failure", expected.CaseId, StringComparison.Ordinal);
            Assert.Equal("raised", RequiredString(python, "outcome"));
            AssertSystemFailureFacts(facts);
        }
    }

    private static void AssertAvailabilityFailureFacts(JsonElement facts)
    {
        AssertKeys(
            facts,
            "created_object_labels_before_failure",
            "created_processor_labels_before_failure",
            "error",
            "events",
            "failing_availability_call_count",
            "first_system_call_count",
            "returned_lists_observed",
            "second_availability_call_count",
            "second_system_call_count",
            "sequential_controller_returned");
        AssertStringArray(facts.GetProperty("created_object_labels_before_failure"), "first-object");
        AssertStringArray(facts.GetProperty("created_processor_labels_before_failure"), "first-processor");
        AssertPythonError(facts.GetProperty("error"), "availability-failure:first");
        JsonElement[] events = facts.GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(4, events.Length);
        AssertCapabilityEvent(events[0], 1, "first", "heatable", true);
        AssertCapabilityEvent(events[1], 1, "first", "coolable", true);
        AssertSystemEvent(events[2], 1, "first", true, true, "availability-first");
        AssertAvailabilityEvent(events[3], 1, "availability-first");
        Assert.Equal(1, facts.GetProperty("failing_availability_call_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("first_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("returned_lists_observed").GetBoolean());
        Assert.Equal(0, facts.GetProperty("second_availability_call_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("second_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("sequential_controller_returned").GetBoolean());
    }

    private static void AssertSuccessFacts(JsonElement facts)
    {
        AssertKeys(
            facts,
            "all_availability_identities_aligned",
            "all_zone_identities_aligned",
            "availability_objects_immediately_follow_owner",
            "capability_read_order",
            "child_objects_fresh",
            "child_processors_fresh",
            "events",
            "first_object_labels",
            "first_processor_labels",
            "fresh_object_list",
            "fresh_processor_list",
            "fresh_sequential_controller",
            "object_result_type",
            "processor_result_type",
            "second_object_labels",
            "second_processor_labels",
            "sequential_controller_group_identity",
            "sequential_controller_last",
            "sequential_controller_zone_identity");
        Assert.True(facts.GetProperty("all_availability_identities_aligned").GetBoolean());
        Assert.True(facts.GetProperty("all_zone_identities_aligned").GetBoolean());
        Assert.True(facts.GetProperty("availability_objects_immediately_follow_owner").GetBoolean());
        AssertStringArray(
            facts.GetProperty("capability_read_order"),
            "heatable", "coolable", "heatable", "coolable", "heatable", "coolable",
            "heatable", "coolable", "heatable", "coolable", "heatable", "coolable");
        Assert.True(facts.GetProperty("child_objects_fresh").GetBoolean());
        Assert.True(facts.GetProperty("child_processors_fresh").GetBoolean());
        AssertSuccessEvents(facts.GetProperty("events"));

        string[] objects =
        {
            "heat-object-first",
            "heat-object-second",
            "availability-heat-object",
            "both-object",
            "cool-object",
            "availability-cool-object",
        };
        string[] processors =
        {
            "heat-processor",
            "both-processor-first",
            "both-processor-second",
            "cool-processor",
            "SequentialLoadFractionController",
        };
        AssertStringArray(facts.GetProperty("first_object_labels"), objects);
        AssertStringArray(facts.GetProperty("first_processor_labels"), processors);
        Assert.True(facts.GetProperty("fresh_object_list").GetBoolean());
        Assert.True(facts.GetProperty("fresh_processor_list").GetBoolean());
        Assert.True(facts.GetProperty("fresh_sequential_controller").GetBoolean());
        Assert.Equal("list", RequiredString(facts, "object_result_type"));
        Assert.Equal("list", RequiredString(facts, "processor_result_type"));
        AssertStringArray(facts.GetProperty("second_object_labels"), objects);
        AssertStringArray(facts.GetProperty("second_processor_labels"), processors);
        Assert.True(facts.GetProperty("sequential_controller_group_identity").GetBoolean());
        Assert.True(facts.GetProperty("sequential_controller_last").GetBoolean());
        Assert.True(facts.GetProperty("sequential_controller_zone_identity").GetBoolean());
    }

    private static void AssertSuccessEvents(JsonElement value)
    {
        JsonElement[] events = value.EnumerateArray().ToArray();
        Assert.Equal(22, events.Length);
        for (int call = 1; call <= 2; call++)
        {
            int offset = (call - 1) * 11;
            AssertCapabilityEvent(events[offset], call, "heat-only", "heatable", true);
            AssertCapabilityEvent(events[offset + 1], call, "heat-only", "coolable", false);
            AssertSystemEvent(events[offset + 2], call, "heat-only", true, false, "availability-heat");
            AssertAvailabilityEvent(events[offset + 3], call, "availability-heat");
            AssertCapabilityEvent(events[offset + 4], call, "both", "heatable", true);
            AssertCapabilityEvent(events[offset + 5], call, "both", "coolable", true);
            AssertSystemEvent(events[offset + 6], call, "both", true, true, null);
            AssertCapabilityEvent(events[offset + 7], call, "cool-only", "heatable", false);
            AssertCapabilityEvent(events[offset + 8], call, "cool-only", "coolable", true);
            AssertSystemEvent(events[offset + 9], call, "cool-only", false, true, "availability-cool");
            AssertAvailabilityEvent(events[offset + 10], call, "availability-cool");
        }
    }

    private static void AssertSystemFailureFacts(JsonElement facts)
    {
        AssertKeys(
            facts,
            "created_object_labels_before_failure",
            "created_processor_labels_before_failure",
            "error",
            "events",
            "first_availability_call_count",
            "first_system_call_count",
            "returned_lists_observed",
            "second_availability_call_count",
            "second_system_call_count",
            "sequential_controller_returned",
            "third_availability_call_count",
            "third_system_call_count");
        AssertStringArray(
            facts.GetProperty("created_object_labels_before_failure"),
            "first-object-first",
            "first-object-second",
            "availability-first-object");
        AssertStringArray(facts.GetProperty("created_processor_labels_before_failure"), "first-processor");
        AssertPythonError(facts.GetProperty("error"), "system-failure:second");
        JsonElement[] events = facts.GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(7, events.Length);
        AssertCapabilityEvent(events[0], 1, "first", "heatable", true);
        AssertCapabilityEvent(events[1], 1, "first", "coolable", false);
        AssertSystemEvent(events[2], 1, "first", true, false, "availability-first");
        AssertAvailabilityEvent(events[3], 1, "availability-first");
        AssertCapabilityEvent(events[4], 1, "second", "heatable", true);
        AssertCapabilityEvent(events[5], 1, "second", "coolable", true);
        AssertSystemEvent(events[6], 1, "second", true, true, "availability-second");
        Assert.Equal(1, facts.GetProperty("first_availability_call_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("first_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("returned_lists_observed").GetBoolean());
        Assert.Equal(0, facts.GetProperty("second_availability_call_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("second_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("sequential_controller_returned").GetBoolean());
        Assert.Equal(0, facts.GetProperty("third_availability_call_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("third_system_call_count").GetInt32());
    }

    private static void AssertCapabilityEvent(
        JsonElement value,
        int groupCall,
        string system,
        string property,
        bool expectedValue)
    {
        AssertKeys(value, "event", "group_call", "property", "system", "value");
        Assert.Equal("capability.read", RequiredString(value, "event"));
        Assert.Equal(groupCall, value.GetProperty("group_call").GetInt32());
        Assert.Equal(property, RequiredString(value, "property"));
        Assert.Equal(system, RequiredString(value, "system"));
        Assert.Equal(expectedValue, value.GetProperty("value").GetBoolean());
    }

    private static void AssertSystemEvent(
        JsonElement value,
        int groupCall,
        string system,
        bool heatable,
        bool coolable,
        string? availability)
    {
        AssertKeys(
            value,
            "availability",
            "availability_identity_aligned",
            "event",
            "for_cooling",
            "for_heating",
            "group_call",
            "system",
            "zone",
            "zone_identity_aligned");
        Assert.Equal("system.to_idf_object", RequiredString(value, "event"));
        Assert.Equal(groupCall, value.GetProperty("group_call").GetInt32());
        Assert.Equal(system, RequiredString(value, "system"));
        Assert.Equal("zone-main", RequiredString(value, "zone"));
        Assert.True(value.GetProperty("zone_identity_aligned").GetBoolean());
        Assert.True(value.GetProperty("availability_identity_aligned").GetBoolean());
        Assert.Equal(heatable, value.GetProperty("for_heating").GetBoolean());
        Assert.Equal(coolable, value.GetProperty("for_cooling").GetBoolean());
        if (availability is null)
        {
            Assert.Equal(JsonValueKind.Null, value.GetProperty("availability").ValueKind);
        }
        else
        {
            Assert.Equal(availability, RequiredString(value, "availability"));
        }
    }

    private static void AssertAvailabilityEvent(JsonElement value, int groupCall, string availability)
    {
        AssertKeys(value, "availability", "event", "group_call");
        Assert.Equal("availability.to_idf_object", RequiredString(value, "event"));
        Assert.Equal(groupCall, value.GetProperty("group_call").GetInt32());
        Assert.Equal(availability, RequiredString(value, "availability"));
    }

    private static void AssertPythonError(JsonElement value, string message)
    {
        AssertKeys(value, "args", "message", "outcome", "type");
        AssertStringArray(value.GetProperty("args"), message);
        Assert.Equal(message, RequiredString(value, "message"));
        Assert.Equal("raised", RequiredString(value, "outcome"));
        Assert.Equal("RuntimeError", RequiredString(value, "type"));
    }

    private static void ValidateNativeBindings()
    {
        Assert.Equal(
            ImplementationSha256,
            Sha256(File.ReadAllBytes(FindRepositoryFile(ImplementationRepositoryPath))));

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
        Assert.Equal(
            new[] { typeof(IddSchema), typeof(EnergyModelIdfOptions) },
            publicTarget.GetParameters().Select(item => item.ParameterType));
        Assert.All(publicTarget.GetParameters(), parameter => Assert.True(parameter.HasDefaultValue));

        Assert.DoesNotContain(
            typeof(SupplyGroup).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name is "ToIdfObject" or "to_idf_object");

        Type assembler = typeof(EnergyModel).Assembly.GetType(
            "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler",
            throwOnError: true)!;
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
        if (expected.CaseId.EndsWith("availability-failure.immediate-after-system", StringComparison.Ordinal))
        {
            AssertAvailabilityFailureFacts(pythonFacts);
            return ObserveNativeAvailabilityFailure(pythonFacts);
        }

        if (expected.CaseId.EndsWith("success.flatten-order-controller-last-and-fresh-lists", StringComparison.Ordinal))
        {
            AssertSuccessFacts(pythonFacts);
            return ObserveNativeSuccess(pythonFacts);
        }

        Assert.EndsWith("system-failure.prefix-before-failure", expected.CaseId, StringComparison.Ordinal);
        AssertSystemFailureFacts(pythonFacts);
        return ObserveNativeSystemFailure(pythonFacts);
    }

    private static string[] ObserveNativeAvailabilityFailure(JsonElement facts)
    {
        string[] prefixObjects = StringArray(facts.GetProperty("created_object_labels_before_failure"));
        string[] prefixProcessors = StringArray(facts.GetProperty("created_processor_labels_before_failure"));
        string pythonError = RequiredString(facts.GetProperty("error"), "message");
        JsonElement systemEvent = facts.GetProperty("events").EnumerateArray()
            .Single(item => RequiredString(item, "event") == "system.to_idf_object");
        string firstName = RequiredString(systemEvent, "system");
        string availabilityName = RequiredString(systemEvent, "availability");
        Assert.Equal(1, facts.GetProperty("failing_availability_call_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("first_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("returned_lists_observed").GetBoolean());
        Assert.Equal(0, facts.GetProperty("second_availability_call_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("second_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("sequential_controller_returned").GetBoolean());

        Zone zone = ConditionedZone(
            "NATIVE-AVAILABILITY-FAILURE-ZONE",
            "Native Availability Failure Zone",
            Schedule.Constant("native-profile-availability", 1, ScheduleType.OnOff));
        var source = new HeatPump(
            new EntityId("NATIVE-AVAILABILITY-FAILURE-SOURCE"),
            "native-availability-failure-source",
            Fuel.Electricity,
            3,
            3);
        var first = new AirHandlingUnit(
            new EntityId("NATIVE-AVAILABILITY-FIRST"),
            firstName,
            source);
        var second = new PackagedAirConditioner(
            new EntityId("NATIVE-AVAILABILITY-SECOND"),
            "second",
            source);
        Schedule firstAvailability = Schedule.Constant(availabilityName, 1, ScheduleType.OnOff);
        Schedule conflictingAvailability = Schedule.Constant(availabilityName, 0, ScheduleType.OnOff);
        var group = new SupplyGroup(
            new SupplySystem[] { first, second },
            new Schedule?[] { firstAvailability, conflictingAvailability });
        Assert.Equal(systemEvent.GetProperty("for_heating").GetBoolean(), first.CanHeat);
        Assert.Equal(systemEvent.GetProperty("for_cooling").GetBoolean(), first.CanCool);
        Assert.Same(firstAvailability, group.Availabilities[0]);
        Assert.Same(conflictingAvailability, group.Availabilities[1]);

        var assignment = new ZoneHvacAssignment(zone.Id, group);
        var model = new EnergyModel(
            "Native availability adaptation failure",
            new[] { zone },
            new[] { assignment });
        ModelSnapshot snapshot = Capture(model);
        string expectedMessage = $"Schedule name '{availabilityName}' has conflicting definitions.";

        InvalidOperationException firstError = Assert.Throws<InvalidOperationException>(
            () => model.ToIdfDocument());
        Assert.Equal(expectedMessage, firstError.Message);
        AssertModelUnchanged(model, snapshot);
        InvalidOperationException secondError = Assert.Throws<InvalidOperationException>(
            () => model.ToIdfDocument());
        Assert.Equal(firstError.GetType(), secondError.GetType());
        Assert.Equal(firstError.Message, secondError.Message);
        AssertModelUnchanged(model, snapshot);

        return new[]
        {
            $"python_failure={pythonError}",
            $"python_local_prefix={string.Join("+", prefixObjects)}|{string.Join("+", prefixProcessors)}",
            "native_public_target=EnergyModel.ToIdfDocument",
            "native_failure_stage=global-schedule-conflict-preflight",
            $"native_exception={nameof(InvalidOperationException)}:{expectedMessage}",
            "native_return=not-returned",
            "native_repeated_attempts=2-identical",
            "native_model_and_group_reference_identity=unchanged",
            "native_standalone_mutable-lists-and-controller=not-exposed",
        };
    }

    private static string[] ObserveNativeSystemFailure(JsonElement facts)
    {
        string[] prefixObjects = StringArray(facts.GetProperty("created_object_labels_before_failure"));
        string[] prefixProcessors = StringArray(facts.GetProperty("created_processor_labels_before_failure"));
        string pythonError = RequiredString(facts.GetProperty("error"), "message");
        JsonElement[] events = facts.GetProperty("events").EnumerateArray().ToArray();
        JsonElement[] systemEvents = events
            .Where(item => RequiredString(item, "event") == "system.to_idf_object")
            .ToArray();
        Assert.Equal(2, systemEvents.Length);
        Assert.Equal(1, facts.GetProperty("first_availability_call_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("first_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("returned_lists_observed").GetBoolean());
        Assert.Equal(0, facts.GetProperty("second_availability_call_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("second_system_call_count").GetInt32());
        Assert.False(facts.GetProperty("sequential_controller_returned").GetBoolean());
        Assert.Equal(0, facts.GetProperty("third_availability_call_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("third_system_call_count").GetInt32());

        Schedule profileAvailability = Schedule.Constant("native-profile-availability", 1, ScheduleType.OnOff);
        Zone zone = ConditionedZone(
            "NATIVE-SYSTEM-FAILURE-ZONE",
            "Native System Failure Zone",
            profileAvailability);
        var conflictingId = new EntityId("NATIVE-CONFLICTING-SOURCE");
        var firstSource = new Boiler(
            conflictingId,
            "native-first-source",
            Fuel.NaturalGas);
        var secondSource = new HeatPump(
            conflictingId,
            "native-second-source",
            Fuel.Electricity,
            3,
            3);
        var thirdSource = new HeatPump(
            new EntityId("NATIVE-UNREACHED-SOURCE"),
            "native-unreached-source",
            Fuel.Electricity,
            3,
            3);
        var first = new RadiantFloor(
            new EntityId("NATIVE-SYSTEM-FIRST"),
            RequiredString(systemEvents[0], "system"),
            firstSource);
        var second = new AirHandlingUnit(
            new EntityId("NATIVE-SYSTEM-SECOND"),
            RequiredString(systemEvents[1], "system"),
            secondSource);
        var third = new PackagedAirConditioner(
            new EntityId("NATIVE-SYSTEM-THIRD"),
            "third",
            thirdSource);
        Schedule firstAvailability = Schedule.Constant(
            RequiredString(systemEvents[0], "availability"),
            1,
            ScheduleType.OnOff);
        Schedule secondAvailability = Schedule.Constant(
            RequiredString(systemEvents[1], "availability"),
            1,
            ScheduleType.OnOff);
        Schedule thirdAvailability = Schedule.Constant("availability-third", 1, ScheduleType.OnOff);
        var group = new SupplyGroup(
            new SupplySystem[] { first, second, third },
            new Schedule?[] { firstAvailability, secondAvailability, thirdAvailability });
        Assert.Equal(
            systemEvents.Select(item => item.GetProperty("for_heating").GetBoolean()),
            group.Systems.Take(2).Select(system => system.CanHeat));
        Assert.Equal(
            systemEvents.Select(item => item.GetProperty("for_cooling").GetBoolean()),
            group.Systems.Take(2).Select(system => system.CanCool));
        AssertReferenceSequence(new SupplySystem[] { first, second, third }, group.Systems);

        var model = new EnergyModel(
            "Native ordered source conflict",
            new[] { zone },
            new[] { new ZoneHvacAssignment(zone.Id, group) });
        Assert.Contains(
            model.Validate().Diagnostics,
            item => item.Code == "INVISIBLEDRAGON.MODEL.CONFLICTING_HVAC_ID");
        ModelSnapshot snapshot = Capture(model);
        var options = new EnergyModelIdfOptions { ThrowOnValidationErrors = false };
        const string expectedMessage =
            "HVAC identifier 'NATIVE-CONFLICTING-SOURCE' has conflicting source definitions.";

        InvalidOperationException firstError = Assert.Throws<InvalidOperationException>(
            () => model.ToIdfDocument(options: options));
        Assert.Equal(expectedMessage, firstError.Message);
        AssertModelUnchanged(model, snapshot);
        InvalidOperationException secondError = Assert.Throws<InvalidOperationException>(
            () => model.ToIdfDocument(options: options));
        Assert.Equal(firstError.GetType(), secondError.GetType());
        Assert.Equal(firstError.Message, secondError.Message);
        AssertModelUnchanged(model, snapshot);

        return new[]
        {
            $"python_failure={pythonError}",
            $"python_local_prefix={string.Join("+", prefixObjects)}|{string.Join("+", prefixProcessors)}",
            "native_public_target=EnergyModel.ToIdfDocument",
            "native_failure_stage=ordered-source-definition-conflict-at-second-system",
            $"native_exception={nameof(InvalidOperationException)}:{expectedMessage}",
            "native_return=not-returned",
            "native_input_order=heat-only->both->cool-only-unreached",
            "native_repeated_attempts=2-identical",
            "native_model_and-group_reference_identity=unchanged",
            "native_standalone-prefix-and-controller=not-exposed",
        };
    }

    private static string[] ObserveNativeSuccess(JsonElement facts)
    {
        bool availabilityIdentities = facts.GetProperty("all_availability_identities_aligned").GetBoolean();
        bool zoneIdentities = facts.GetProperty("all_zone_identities_aligned").GetBoolean();
        bool immediateAvailability = facts.GetProperty("availability_objects_immediately_follow_owner").GetBoolean();
        string[] capabilityReadOrder = StringArray(facts.GetProperty("capability_read_order"));
        bool childObjectsFresh = facts.GetProperty("child_objects_fresh").GetBoolean();
        bool childProcessorsFresh = facts.GetProperty("child_processors_fresh").GetBoolean();
        JsonElement[] events = facts.GetProperty("events").EnumerateArray().ToArray();
        string[] firstObjectLabels = StringArray(facts.GetProperty("first_object_labels"));
        string[] firstProcessorLabels = StringArray(facts.GetProperty("first_processor_labels"));
        bool freshObjectList = facts.GetProperty("fresh_object_list").GetBoolean();
        bool freshProcessorList = facts.GetProperty("fresh_processor_list").GetBoolean();
        bool freshController = facts.GetProperty("fresh_sequential_controller").GetBoolean();
        string objectResultType = RequiredString(facts, "object_result_type");
        string processorResultType = RequiredString(facts, "processor_result_type");
        string[] secondObjectLabels = StringArray(facts.GetProperty("second_object_labels"));
        string[] secondProcessorLabels = StringArray(facts.GetProperty("second_processor_labels"));
        bool controllerGroupIdentity = facts.GetProperty("sequential_controller_group_identity").GetBoolean();
        bool controllerLast = facts.GetProperty("sequential_controller_last").GetBoolean();
        bool controllerZoneIdentity = facts.GetProperty("sequential_controller_zone_identity").GetBoolean();

        Assert.True(availabilityIdentities);
        Assert.True(zoneIdentities);
        Assert.True(immediateAvailability);
        Assert.Equal(12, capabilityReadOrder.Length);
        Assert.True(childObjectsFresh);
        Assert.True(childProcessorsFresh);
        Assert.Equal(firstObjectLabels, secondObjectLabels);
        Assert.Equal(firstProcessorLabels, secondProcessorLabels);
        Assert.True(freshObjectList);
        Assert.True(freshProcessorList);
        Assert.True(freshController);
        Assert.Equal("list", objectResultType);
        Assert.Equal("list", processorResultType);
        Assert.True(controllerGroupIdentity);
        Assert.True(controllerLast);
        Assert.True(controllerZoneIdentity);
        Assert.Equal("SequentialLoadFractionController", firstProcessorLabels[^1]);

        JsonElement[] firstCallSystems = events
            .Where(item => RequiredString(item, "event") == "system.to_idf_object")
            .Where(item => item.GetProperty("group_call").GetInt32() == 1)
            .ToArray();
        JsonElement[] secondCallSystems = events
            .Where(item => RequiredString(item, "event") == "system.to_idf_object")
            .Where(item => item.GetProperty("group_call").GetInt32() == 2)
            .ToArray();
        Assert.Equal(3, firstCallSystems.Length);
        Assert.Equal(
            firstCallSystems.Select(item => RequiredString(item, "system")),
            secondCallSystems.Select(item => RequiredString(item, "system")));

        string heatAvailabilityName = RequiredString(firstCallSystems[0], "availability");
        Assert.Equal(JsonValueKind.Null, firstCallSystems[1].GetProperty("availability").ValueKind);
        string coolAvailabilityName = RequiredString(firstCallSystems[2], "availability");
        Schedule heatAvailability = Schedule.Constant(heatAvailabilityName, 1, ScheduleType.OnOff);
        Schedule coolAvailability = Schedule.Constant(coolAvailabilityName, 1, ScheduleType.OnOff);
        Zone zone = ConditionedZone(
            "NATIVE-SUCCESS-ZONE",
            "zone-main",
            heatAvailability);
        var source = new HeatPump(
            new EntityId("NATIVE-SUCCESS-SOURCE"),
            "native-success-source",
            Fuel.Electricity,
            3,
            3);
        var heat = new ElectricRadiator(
            new EntityId("NATIVE-SUCCESS-HEAT"),
            RequiredString(firstCallSystems[0], "system"),
            1_000);
        var both = new AirHandlingUnit(
            new EntityId("NATIVE-SUCCESS-BOTH"),
            RequiredString(firstCallSystems[1], "system"),
            source);
        var cool = new PackagedAirConditioner(
            new EntityId("NATIVE-SUCCESS-COOL"),
            RequiredString(firstCallSystems[2], "system"),
            source);
        var group = new SupplyGroup(
            new SupplySystem[] { heat, both, cool },
            new Schedule?[] { heatAvailability, null, coolAvailability });
        Assert.Equal(
            firstCallSystems.Select(item => item.GetProperty("for_heating").GetBoolean()),
            group.Systems.Select(system => system.CanHeat));
        Assert.Equal(
            firstCallSystems.Select(item => item.GetProperty("for_cooling").GetBoolean()),
            group.Systems.Select(system => system.CanCool));
        Assert.Same(heatAvailability, group.Availabilities[0]);
        Assert.Null(group.Availabilities[1]);
        Assert.Same(coolAvailability, group.Availabilities[2]);

        var assignment = new ZoneHvacAssignment(zone.Id, group);
        var model = new EnergyModel(
            "Native SupplyGroup model-context success",
            new[] { zone },
            new[] { assignment });
        Assert.Same(group, assignment.Supply);
        Assert.Same(zone, Assert.Single(model.Zones));
        ModelSnapshot snapshot = Capture(model);

        IdfDocument firstDocument = model.ToIdfDocument();
        AssertModelUnchanged(model, snapshot);
        IdfDocument secondDocument = model.ToIdfDocument();
        AssertModelUnchanged(model, snapshot);
        Assert.Equal(freshObjectList, !ReferenceEquals(firstDocument, secondDocument));
        Assert.Equal(DocumentFingerprint(firstDocument), DocumentFingerprint(secondDocument));
        Assert.Equal(firstDocument.Count, secondDocument.Count);
        bool allDocumentChildrenFresh = firstDocument
            .Zip(secondDocument, (first, second) => !ReferenceEquals(first, second))
            .All(value => value);
        Assert.Equal(childObjectsFresh, allDocumentChildrenFresh);

        string heatName = heat.ObjectNameFor(zone);
        string bothName = both.ObjectNameFor(zone);
        string coolName = cool.ObjectNameFor(zone);
        IdfObject radiator = Assert.Single(firstDocument["ZoneHVAC:Baseboard:RadiantConvective:Electric"]);
        Assert.Equal(heatName, radiator.Name);
        Assert.Equal(heatAvailabilityName, radiator[1]);
        IdfObject[] terminals = firstDocument["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"].ToArray();
        Assert.Equal(new[] { bothName, coolName }, terminals.Select(item => item.Name));
        Assert.Equal(heatAvailabilityName, terminals[0][1]);
        Assert.Equal(coolAvailabilityName, terminals[1][1]);

        IdfObject sourceObject = Assert.Single(firstDocument[source.IdfObjectType]);
        Assert.Equal(source.IdfObjectName, sourceObject.Name);
        IdfObject terminalList = Assert.Single(firstDocument["ZoneTerminalUnitList"]);
        Assert.Equal(new[] { source.TerminalUnitListName, bothName, coolName }, terminalList.Fields.Select(item => item.Value));

        IdfObject heatSchedule = Assert.Single(firstDocument, item =>
            item.ObjectType.StartsWith("Schedule:", StringComparison.OrdinalIgnoreCase)
            && item.Name == heatAvailabilityName);
        IdfObject coolSchedule = Assert.Single(firstDocument, item =>
            item.ObjectType.StartsWith("Schedule:", StringComparison.OrdinalIgnoreCase)
            && item.Name == coolAvailabilityName);
        Assert.True(IndexOf(firstDocument, heatSchedule) < IndexOf(firstDocument, sourceObject));
        Assert.True(IndexOf(firstDocument, coolSchedule) < IndexOf(firstDocument, sourceObject));
        Assert.True(IndexOf(firstDocument, sourceObject) < IndexOf(firstDocument, radiator));
        Assert.True(IndexOf(firstDocument, radiator) < IndexOf(firstDocument, terminals[0]));
        Assert.True(IndexOf(firstDocument, terminals[0]) < IndexOf(firstDocument, terminals[1]));

        string heatFraction = $"heating_fraction_for_{heatName}";
        string bothHeatingFraction = $"heating_fraction_for_{bothName}";
        string bothCoolingFraction = $"cooling_fraction_for_{bothName}";
        string coolFraction = $"cooling_fraction_for_{coolName}";
        string[] fractionNames =
        {
            heatFraction,
            bothHeatingFraction,
            bothCoolingFraction,
            coolFraction,
        };
        IdfObject[] fractions = firstDocument["Schedule:Compact"]
            .Where(item => fractionNames.Contains(item.Name, StringComparer.Ordinal))
            .ToArray();
        Assert.Equal(fractionNames, fractions.Select(item => item.Name));
        double firstSequentialFraction = 1d / (2d + 1.0e-10d);
        double lastSequentialFraction = 1d / (1d + 1.0e-10d);
        AssertScheduleValues(fractions[0], firstSequentialFraction);
        AssertScheduleValues(fractions[1], lastSequentialFraction);
        AssertScheduleValues(fractions[2], firstSequentialFraction);
        AssertScheduleValues(fractions[3], lastSequentialFraction);
        IdfObject equipment = Assert.Single(firstDocument["ZoneHVAC:EquipmentList"]);
        Assert.Equal(
            new[]
            {
                $"EquipmentList_for_{zone.Name}",
                "SequentialLoad",
                "ZoneHVAC:Baseboard:RadiantConvective:Electric",
                heatName,
                "1",
                "1",
                "ALLOFF",
                heatFraction,
                "ZoneHVAC:TerminalUnit:VariableRefrigerantFlow",
                bothName,
                "2",
                "2",
                bothCoolingFraction,
                bothHeatingFraction,
                "ZoneHVAC:TerminalUnit:VariableRefrigerantFlow",
                coolName,
                "3",
                "3",
                coolFraction,
                "ALLOFF",
            },
            equipment.Fields.Select(item => item.Value));
        bool controllerStageIsLast = IndexOf(firstDocument, terminals[1]) < IndexOf(firstDocument, fractions[0])
            && IndexOf(firstDocument, fractions[0]) < IndexOf(firstDocument, fractions[1])
            && IndexOf(firstDocument, fractions[1]) < IndexOf(firstDocument, fractions[2])
            && IndexOf(firstDocument, fractions[2]) < IndexOf(firstDocument, fractions[3])
            && IndexOf(firstDocument, fractions[3]) < IndexOf(firstDocument, equipment);
        Assert.Equal(controllerLast, controllerStageIsLast);
        Assert.Equal(
            childProcessorsFresh && freshProcessorList && freshController,
            fractions.Zip(
                secondDocument["Schedule:Compact"].Where(item => fractionNames.Contains(item.Name, StringComparer.Ordinal)),
                (first, second) => !ReferenceEquals(first, second)).All(value => value));
        Assert.Equal(controllerGroupIdentity, ReferenceEquals(group, assignment.Supply));
        Assert.Equal(controllerZoneIdentity, ReferenceEquals(zone, model.Zones[0]));

        Assert.Contains("availability-heat-object", firstObjectLabels);
        Assert.Contains("availability-cool-object", firstObjectLabels);
        Assert.Equal(
            immediateAvailability,
            Array.IndexOf(firstObjectLabels, "availability-heat-object")
                == Array.IndexOf(firstObjectLabels, "heat-object-second") + 1);

        return new[]
        {
            "python_input=heat-only/custom->both/null->cool-only/custom",
            "native_public_target=EnergyModel.ToIdfDocument",
            $"native_return={nameof(IdfDocument)}-fresh-distinct-deterministic",
            "native_probe-list-shape=adapted-to-concrete-system-fragments",
            "native_custom-availability=globally-collected-once-each-before-hvac-fragments",
            $"native_source-before-system={source.IdfObjectName}->{heatName}->{bothName}->{coolName}",
            $"native_fraction-order={string.Join("->", fractionNames)}",
            "native_fraction-values=sequential-availability-over-remaining-plus-1e-10",
            $"native_equipment-references=ALLOFF/{heatFraction};{bothCoolingFraction}/{bothHeatingFraction};{coolFraction}/ALLOFF",
            "native_controller-adaptation=fractions-after-all-system-fragments-before-equipment-list",
            "native_model-group-zone-and-availability-reference-identity=unchanged",
            "native_standalone-processor-list=not-exposed",
        };
    }

    private static Zone ConditionedZone(string id, string name, Schedule hvacAvailability)
    {
        Surface floor = TestDomainFactory.Surface(
            $"{id}-FLOOR",
            $"{name} Floor",
            TestDomainFactory.Square(size: 2),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        var profile = new ZoneProfile(
            new EntityId($"{id}-PROFILE"),
            $"{name} Profile",
            Schedule.Constant($"{name} Heating", 20, ScheduleType.Temperature),
            Schedule.Constant($"{name} Cooling", 26, ScheduleType.Temperature),
            hvacAvailability);
        return new Zone(new EntityId(id), name, new[] { floor }, profile);
    }

    private static ModelSnapshot Capture(EnergyModel model)
    {
        return new ModelSnapshot(
            model.Zones.ToArray(),
            model.HvacAssignments.ToArray(),
            model.HvacAssignments.Select(item => item.Supply).ToArray(),
            model.HvacAssignments.SelectMany(item => item.Supply.Systems).ToArray(),
            model.HvacAssignments.SelectMany(item => item.Supply.Availabilities).ToArray(),
            ModelFingerprint(model));
    }

    private static void AssertModelUnchanged(EnergyModel model, ModelSnapshot expected)
    {
        Assert.Equal(expected.Fingerprint, ModelFingerprint(model));
        AssertReferenceSequence(expected.Zones, model.Zones);
        AssertReferenceSequence(expected.Assignments, model.HvacAssignments);
        AssertReferenceSequence(expected.Groups, model.HvacAssignments.Select(item => item.Supply));
        AssertReferenceSequence(expected.Systems, model.HvacAssignments.SelectMany(item => item.Supply.Systems));
        AssertNullableReferenceSequence(
            expected.Availabilities,
            model.HvacAssignments.SelectMany(item => item.Supply.Availabilities));
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

    private static void AssertNullableReferenceSequence<T>(IEnumerable<T?> expected, IEnumerable<T?> actual)
        where T : class
    {
        T?[] expectedItems = expected.ToArray();
        T?[] actualItems = actual.ToArray();
        Assert.Equal(expectedItems.Length, actualItems.Length);
        for (int index = 0; index < expectedItems.Length; index++)
        {
            Assert.True(ReferenceEquals(expectedItems[index], actualItems[index]));
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
                string.Join(",", model.HvacAssignments.SelectMany(assignment => assignment.Supply.Availabilities).Select(schedule => schedule is null ? "<null>" : $"{schedule.Name}:{schedule.Type}:{schedule.Minimum}:{schedule.Maximum}")),
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

    private static void AssertScheduleValues(IdfObject schedule, double expected)
    {
        var values = new List<double>();
        for (int index = 1; index < schedule.Fields.Count; index++)
        {
            if (!schedule.Fields[index - 1].Value.StartsWith("Until:", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(double.TryParse(
                schedule.Fields[index].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value));
            values.Add(value);
        }

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.InRange(Math.Abs(value - expected), 0d, 1.0e-12d));
    }

    private static void ValidateReceipt(JsonElement receipt, IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(receipt, "fixture", "native_binding", "observations", "upstream_path", "upstream_symbol");
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

    private static string[] StringArray(JsonElement value)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
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

    private sealed record CaseBinding(string CaseId);

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
        Schedule?[] Availabilities,
        string Fingerprint);
}
