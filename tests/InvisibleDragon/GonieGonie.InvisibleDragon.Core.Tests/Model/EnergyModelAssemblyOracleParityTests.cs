using System.Globalization;
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
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelAssemblyOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-model-assembly-oracle.json";
    private const string OracleSha256 =
        "sha256:a008740b6830908cd65d3f2636532c67dde7d7a6cadd062d34e3583775f16308";
    private const string CasesSha256 =
        "sha256:9e3d8c576e2ed17fdbe9555fbafda9dc92aca3991c835b0d83a134a8415c6833";
    private const int OracleByteLength = 77_002;
    private const int ExpectedCaseCount = 5;
    private const string OracleSchema =
        "goniegonie.python-reference.dragon-model-assembly.v1";
    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string TargetSymbol = "EnergyModel.to_idf";
    private const string EnergyPlusVersion = "24.2.0";
    private const string EnergyPlusBuild = "94a887817b";
    private const string EnergyPlusIddSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const long EnergyPlusIddBytes = 4_556_412;

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-model-assembly.to-idf.assigned-without-availability-fallback", "energy-model-to-idf", TargetSymbol),
        new("dragon-model-assembly.to-idf.case-distinct-profile-schedules", "energy-model-to-idf", TargetSymbol),
        new("dragon-model-assembly.to-idf.duplicate-profile-last-wins-dangling", "energy-model-to-idf", TargetSymbol),
        new("dragon-model-assembly.to-idf.legacy-erv-unconditioned", "energy-model-to-idf", TargetSymbol),
        new("dragon-model-assembly.to-idf.two-unconditioned-shared-fallback", "energy-model-to-idf", TargetSymbol),
    };

    private static readonly SourceBinding[] ExpectedSources =
    {
        new("src/idragon/__init__.py", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618"),
        new("src/idragon/common.py", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9"),
        new("src/idragon/constants.py", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"),
        new("src/idragon/dragon/__init__.py", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52"),
        new("src/idragon/dragon/construction.py", "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"),
        new("src/idragon/dragon/hvac.py", "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0", "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"),
        new("src/idragon/dragon/model.py", "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"),
        new("src/idragon/dragon/profile.py", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef"),
        new("src/idragon/dragon/shape.py", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"),
        new("src/idragon/imugi.py", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"),
        new("src/idragon/launcher.py", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e"),
        new("src/idragon/utils.py", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452"),
    };

    private static readonly string[] ExpectedLoadedModules =
    {
        "idragon",
        "idragon.common",
        "idragon.constants",
        "idragon.dragon",
        "idragon.dragon.construction",
        "idragon.dragon.hvac",
        "idragon.dragon.model",
        "idragon.dragon.profile",
        "idragon.dragon.shape",
        "idragon.imugi",
        "idragon.launcher",
        "idragon.utils",
    };

    [Fact]
    public void MatchesPinnedPythonBoundedAssemblyCases()
    {
        using JsonDocument oracle = ReadPinnedOracle();
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);

        AssertSafeDefaultsRemainEnabled();
        for (int index = 0; index < cases.Length; index++)
        {
            ExecuteNativeCase(ExpectedCases[index], cases[index].GetProperty("python").GetProperty("facts"));
        }
    }

    [GonieGonie.InvisibleDragon.Tests.Idd.IddSchemaOracleTests.EnergyPlusIddIntegrationFact]
    [Trait("Category", "Integration")]
    public void SchemaBoundAssemblyMatchesEveryPinnedEffectiveField()
    {
        using JsonDocument oracle = ReadPinnedOracle();
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        IddSchema schema = LoadPinnedEnergyPlusSchema();

        for (int index = 0; index < cases.Length; index++)
        {
            ExecuteSchemaBoundCase(
                ExpectedCases[index],
                cases[index].GetProperty("python").GetProperty("facts"),
                schema);
        }
    }

    private static JsonDocument ReadPinnedOracle()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, Sha256(bytes));
        return JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
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
        string[] caseIds = cases.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), caseIds);
        Assert.Equal(caseIds.OrderBy(item => item, StringComparer.Ordinal), caseIds);
        Assert.Equal(caseIds.Length, caseIds.Distinct(StringComparer.Ordinal).Count());
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
        Assert.Equal(ExpectedSources.Length, sources.Length);
        for (int index = 0; index < sources.Length; index++)
        {
            SourceBinding expected = ExpectedSources[index];
            JsonElement source = sources[index];
            AssertKeys(source, "ast_sha256", "path", "source_sha256");
            Assert.Equal(expected.Path, RequiredString(source, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(source, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(source, "ast_sha256"));
        }

        JsonElement[] loadedModules = upstream.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(ExpectedSources.Length, loadedModules.Length);
        for (int index = 0; index < loadedModules.Length; index++)
        {
            SourceBinding expected = ExpectedSources[index];
            JsonElement module = loadedModules[index];
            AssertKeys(module, "ast_sha256", "module", "path", "source_sha256");
            Assert.Equal(ExpectedLoadedModules[index], RequiredString(module, "module"));
            Assert.Equal(expected.Path, RequiredString(module, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(module, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(module, "ast_sha256"));
        }
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
        Assert.Equal(TargetSymbol, RequiredString(symbol, "symbol"));
        Assert.Equal("function", RequiredString(symbol, "kind"));
        Assert.Equal("src/idragon/dragon/model.py", RequiredString(symbol, "path"));
        Assert.Equal(
            "sha256:9389bd00d5a2180ea9f3cd1aa5695ba492e1665947515c34c31eff01f072bade",
            RequiredString(symbol, "signature_hash"));
        Assert.Equal(
            "sha256:9d1b5a610b485aa782c0c1f39ed57b65d5534e1ba3271f1a325c52a109228189",
            RequiredString(symbol, "body_hash"));
        Assert.Equal(
            "sha256:de10251f38f220956e870d8faea1c7a879da9158b369cffc244f7afc6519eb35",
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
            "raw_field_encoding",
            "source_import_policy",
            "target_symbols");
        Assert.Empty(contract.GetProperty("adaptations").EnumerateObject());
        Assert.Empty(contract.GetProperty("assertion_ids").EnumerateObject());
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());

        JsonElement classifications = contract.GetProperty("classifications");
        AssertKeys(classifications, TargetSymbol);
        Assert.Equal("needs_reverification", RequiredString(classifications, TargetSymbol));
        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(closure, "full_symbol_closure", "scope", "uncovered_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal("bounded-behavioral-evidence-only", RequiredString(closure, "scope"));
        Assert.Equal(
            "remaining-EnergyModel.to_idf-branches-require-reverification",
            RequiredString(closure, "uncovered_behavior"));
        Assert.Equal("logical-labels-only-no-id-or-address", RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "typed-kind-plus-value-or-repr-with-trailing-none-trimmed",
            RequiredString(contract, "raw_field_encoding"));
        Assert.Equal(
            "external-temporary-copy-of-pinned-source",
            RequiredString(contract, "source_import_policy"));
        AssertStringArray(contract.GetProperty("target_symbols"), TargetSymbol);
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        AssertKeys(item, "executor", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Executor, RequiredString(item, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
        Assert.False(item.TryGetProperty("expected_dotnet", out _));
        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        ValidatePythonFacts(expected.CaseId, python.GetProperty("facts"));
    }

    private static void ValidatePythonFacts(string caseId, JsonElement facts)
    {
        if (caseId == ExpectedCases[0].CaseId)
        {
            ValidateAssignedWithoutAvailabilityFacts(facts);
            return;
        }

        if (caseId == ExpectedCases[1].CaseId)
        {
            ValidateCaseDistinctFacts(facts);
            return;
        }

        if (caseId == ExpectedCases[2].CaseId)
        {
            ValidateDuplicateProfileFacts(facts);
            return;
        }

        if (caseId == ExpectedCases[3].CaseId)
        {
            ValidateLegacyErvFacts(facts);
            return;
        }

        Assert.Equal(ExpectedCases[4].CaseId, caseId);
        ValidateTwoUnconditionedFacts(facts);
    }

    private static void ValidateAssignedWithoutAvailabilityFacts(JsonElement facts)
    {
        AssertKeys(
            facts,
            "absent_object_counts",
            "assigned_supply_names",
            "conditioned_zone_names",
            "default_objects",
            "ensure_validity",
            "fallback_ideal_loads",
            "fallback_thermostats",
            "nonempty_families",
            "object_count",
            "schedule_compact",
            "unconditioned_zone_names",
            "zone_is_conditioned",
            "zone_names");
        Assert.False(facts.GetProperty("ensure_validity").GetBoolean());
        Assert.False(facts.GetProperty("zone_is_conditioned").GetBoolean());
        AssertStringArray(facts.GetProperty("assigned_supply_names"), "Assigned-Electric");
        AssertStringArray(facts.GetProperty("conditioned_zone_names"));
        AssertStringArray(facts.GetProperty("unconditioned_zone_names"), "Assigned-Zone");
        AssertStringArray(facts.GetProperty("zone_names"), "Assigned-Zone");
        JsonElement absent = facts.GetProperty("absent_object_counts");
        AssertKeys(absent, "DesignSpecification:OutdoorAir", "Sizing:Zone", "ZoneControl:Thermostat", "ZoneHVAC:Baseboard:RadiantConvective:Electric", "ZoneHVAC:EquipmentList");
        Assert.All(absent.EnumerateObject(), item => Assert.Equal(0, item.Value.GetInt32()));
        Assert.Equal(23, facts.GetProperty("object_count").GetInt32());
        AssertFamilies(
            facts.GetProperty("nonempty_families"),
            ExpectedFamilies(
                4,
                new Family("Zone", 1),
                new Family("HVACTemplate:Thermostat", 1),
                new Family("HVACTemplate:Zone:IdealLoadsAirSystem", 1)));
        AssertScheduleCompact(
            facts.GetProperty("schedule_compact"),
            DefaultCompact("ALLON", "1"),
            DefaultCompact("ALLOFF", "0"),
            ConstantCompact("Heat-Assigned", "ScheduleTypeLimits:Temperature", "20.0"),
            ConstantCompact("Cool-Assigned", "ScheduleTypeLimits:Temperature", "26.0"));
        AssertDefaultObjectFacts(facts.GetProperty("default_objects"));
        AssertFallbacks(facts, "Assigned-Zone");
    }

    private static void ValidateCaseDistinctFacts(JsonElement facts)
    {
        AssertKeys(facts, "casefold_schedule_groups", "ensure_validity", "fallback_ideal_loads", "fallback_thermostats", "lights", "nonempty_families", "object_count", "schedule_compact", "used_profiles", "zone_names");
        Assert.False(facts.GetProperty("ensure_validity").GetBoolean());
        Assert.Equal(27, facts.GetProperty("object_count").GetInt32());
        AssertStringArray(facts.GetProperty("zone_names"), "Case-Zone-1", "Case-Zone-2");
        JsonElement groups = facts.GetProperty("casefold_schedule_groups");
        AssertKeys(groups, "alloff", "allon", "caselight");
        AssertStringArray(groups.GetProperty("alloff"), "ALLOFF");
        AssertStringArray(groups.GetProperty("allon"), "ALLON");
        AssertStringArray(groups.GetProperty("caselight"), "CaseLight", "caselight");
        AssertLights(facts.GetProperty("lights"),
            new LightFact("light:Case-Zone-1", "Case-Zone-1", "CaseLight"),
            new LightFact("light:Case-Zone-2", "Case-Zone-2", "caselight"));
        AssertProfiles(facts.GetProperty("used_profiles"),
            new ProfileFact("CaseProfile", "CaseLight"),
            new ProfileFact("caseprofile", "caselight"));
        AssertFamilies(
            facts.GetProperty("nonempty_families"),
            ExpectedFamilies(4, new Family("Zone", 2), new Family("Lights", 2), new Family("HVACTemplate:Thermostat", 1), new Family("HVACTemplate:Zone:IdealLoadsAirSystem", 2)));
        AssertScheduleCompact(
            facts.GetProperty("schedule_compact"),
            DefaultCompact("ALLON", "1"),
            DefaultCompact("ALLOFF", "0"),
            ConstantCompact("CaseLight", "ScheduleTypeLimits:Onoff", "1"),
            ConstantCompact("caselight", "ScheduleTypeLimits:Onoff", "0"));
        AssertFallbacks(facts, "Case-Zone-1", "Case-Zone-2");
    }

    private static void ValidateDuplicateProfileFacts(JsonElement facts)
    {
        AssertKeys(facts, "ensure_validity", "fallback_ideal_loads", "fallback_thermostats", "lights", "missing_schedule_references", "nonempty_families", "object_count", "schedule_compact", "used_profiles", "zone_names");
        Assert.False(facts.GetProperty("ensure_validity").GetBoolean());
        Assert.Equal(26, facts.GetProperty("object_count").GetInt32());
        AssertStringArray(facts.GetProperty("zone_names"), "Exact-Zone-1", "Exact-Zone-2");
        AssertStringArray(facts.GetProperty("missing_schedule_references"), "Light-A");
        AssertLights(facts.GetProperty("lights"),
            new LightFact("light:Exact-Zone-1", "Exact-Zone-1", "Light-A"),
            new LightFact("light:Exact-Zone-2", "Exact-Zone-2", "Light-B"));
        AssertProfiles(facts.GetProperty("used_profiles"), new ProfileFact("DUPLICATE-PROFILE", "Light-B"));
        AssertFamilies(
            facts.GetProperty("nonempty_families"),
            ExpectedFamilies(3, new Family("Zone", 2), new Family("Lights", 2), new Family("HVACTemplate:Thermostat", 1), new Family("HVACTemplate:Zone:IdealLoadsAirSystem", 2)));
        AssertScheduleCompact(
            facts.GetProperty("schedule_compact"),
            DefaultCompact("ALLON", "1"),
            DefaultCompact("ALLOFF", "0"),
            ConstantCompact("Light-B", "ScheduleTypeLimits:Onoff", "0"));
        AssertFallbacks(facts, "Exact-Zone-1", "Exact-Zone-2");
    }

    private static void ValidateLegacyErvFacts(JsonElement facts)
    {
        AssertKeys(facts, "conditioned_zone_names", "ensure_validity", "fallback_ideal_loads", "fallback_thermostats", "heat_recovery_nonempty_families", "nonempty_families", "object_count", "people", "schedule_compact", "unconditioned_zone_names", "ventilation", "zone_is_conditioned", "zone_names");
        Assert.False(facts.GetProperty("ensure_validity").GetBoolean());
        Assert.False(facts.GetProperty("zone_is_conditioned").GetBoolean());
        Assert.Equal(25, facts.GetProperty("object_count").GetInt32());
        AssertStringArray(facts.GetProperty("conditioned_zone_names"));
        AssertStringArray(facts.GetProperty("unconditioned_zone_names"), "ERV-Zone");
        AssertStringArray(facts.GetProperty("zone_names"), "ERV-Zone");
        Assert.Empty(facts.GetProperty("heat_recovery_nonempty_families").EnumerateArray());
        AssertFamilies(
            facts.GetProperty("nonempty_families"),
            ExpectedFamilies(4, new Family("Zone", 1), new Family("People", 1), new Family("ZoneVentilation:DesignFlowRate", 1), new Family("HVACTemplate:Thermostat", 1), new Family("HVACTemplate:Zone:IdealLoadsAirSystem", 1)));
        AssertScheduleCompact(
            facts.GetProperty("schedule_compact"),
            DefaultCompact("ALLON", "1"),
            DefaultCompact("ALLOFF", "0"),
            ConstantCompact("Occ-ERV", "ScheduleTypeLimits:Real", "1.0"),
            ConstantCompact("Occ-ERV_normalized:for:ERV-Zone:occupant", "ScheduleTypeLimits:Real", "1.0"));
        JsonElement person = Assert.Single(facts.GetProperty("people").EnumerateArray());
        AssertKeys(person, "activity_schedule_name", "name", "occupancy_schedule_name", "stored_field_count", "zone_name");
        Assert.Equal("people:ERV-Zone", RequiredString(person, "name"));
        Assert.Equal("ERV-Zone", RequiredString(person, "zone_name"));
        Assert.Equal("Occ-ERV_normalized:for:ERV-Zone:occupant", RequiredString(person, "occupancy_schedule_name"));
        Assert.Equal("$DEFAULT$PEOPLEACTIVITY", RequiredString(person, "activity_schedule_name"));
        Assert.Equal(29, person.GetProperty("stored_field_count").GetInt32());
        JsonElement ventilation = Assert.Single(facts.GetProperty("ventilation").EnumerateArray());
        AssertRawObject(ventilation, 26, LegacyVentilation());
        AssertFallbacks(facts, "ERV-Zone");
    }

    private static void ValidateTwoUnconditionedFacts(JsonElement facts)
    {
        AssertKeys(facts, "allon_object_count", "conditioned_zone_names", "ensure_validity", "fallback_ideal_loads", "fallback_thermostats", "nonempty_families", "object_count", "schedule_compact", "unconditioned_zone_names", "zone_names");
        Assert.False(facts.GetProperty("ensure_validity").GetBoolean());
        Assert.Equal(1, facts.GetProperty("allon_object_count").GetInt32());
        Assert.Equal(23, facts.GetProperty("object_count").GetInt32());
        AssertStringArray(facts.GetProperty("conditioned_zone_names"));
        AssertStringArray(facts.GetProperty("unconditioned_zone_names"), "Unconditioned-First", "Unconditioned-Second");
        AssertStringArray(facts.GetProperty("zone_names"), "Unconditioned-First", "Unconditioned-Second");
        AssertFamilies(
            facts.GetProperty("nonempty_families"),
            ExpectedFamilies(2, new Family("Zone", 2), new Family("HVACTemplate:Thermostat", 1), new Family("HVACTemplate:Zone:IdealLoadsAirSystem", 2)));
        AssertScheduleCompact(facts.GetProperty("schedule_compact"), DefaultCompact("ALLON", "1"), DefaultCompact("ALLOFF", "0"));
        AssertFallbacks(facts, "Unconditioned-First", "Unconditioned-Second");
    }

    private static void ExecuteNativeCase(CaseBinding binding, JsonElement pythonFacts)
    {
        if (binding.CaseId == ExpectedCases[0].CaseId)
        {
            ExecuteAssignedWithoutAvailability(pythonFacts);
            return;
        }

        if (binding.CaseId == ExpectedCases[1].CaseId)
        {
            ExecuteCaseDistinctProfiles(pythonFacts);
            return;
        }

        if (binding.CaseId == ExpectedCases[2].CaseId)
        {
            ExecuteDuplicateProfiles(pythonFacts);
            return;
        }

        if (binding.CaseId == ExpectedCases[3].CaseId)
        {
            ExecuteLegacyErv(pythonFacts);
            return;
        }

        Assert.Equal(ExpectedCases[4].CaseId, binding.CaseId);
        ExecuteTwoUnconditioned(pythonFacts);
    }

    private static void ExecuteAssignedWithoutAvailability(JsonElement pythonFacts)
    {
        EnergyModel model = CreateAssignedWithoutAvailabilityModel();

        IdfDocument document = AssembleLegacyDeterministically(model);
        Assert.Equal(new[] { "Assigned-Zone" }, document["Zone"].Select(item => item.Name));
        AssertCompactScheduleNames(document, "ALLON", "ALLOFF", "Heat-Assigned", "Cool-Assigned");
        AssertFields(document["Schedule:Compact"]["Heat-Assigned"], CompactFields("Heat-Assigned", "ScheduleTypeLimits:Temperature", "20.0"));
        AssertFields(document["Schedule:Compact"]["Cool-Assigned"], CompactFields("Cool-Assigned", "ScheduleTypeLimits:Temperature", "26.0"));
        AssertNativeDefaultObjects(
            document,
            pythonFacts.GetProperty("default_objects"),
            requireDefinitions: false);
        AssertLegacyFallback(document, "Assigned-Zone");
        foreach (JsonProperty absent in pythonFacts.GetProperty("absent_object_counts").EnumerateObject())
        {
            Assert.Empty(document[absent.Name]);
        }

        Assert.Equal(new[] { "Assigned-Electric" }, model.HvacAssignments[0].Supply.Systems.Select(item => item.Name));
        IdfDocument safe = model.ToIdfDocument();
        Assert.Equal("IdealThermostat_for_Assigned-Zone", Assert.Single(safe["HVACTemplate:Thermostat"]).Name);
        Assert.Empty(safe["ZoneHVAC:Baseboard:RadiantConvective:Electric"]);
    }

    private static void ExecuteCaseDistinctProfiles(JsonElement pythonFacts)
    {
        EnergyModel model = CreateCaseDistinctProfilesModel();

        IdfDocument document = AssembleLegacyDeterministically(model);
        Assert.Equal(new[] { "Case-Zone-1", "Case-Zone-2" }, document["Zone"].Select(item => item.Name));
        AssertCompactScheduleNames(document, "ALLON", "ALLOFF", "CaseLight", "caselight");
        AssertFields(Assert.Single(document["Schedule:Compact"], item => item.Name == "CaseLight"), CompactFields("CaseLight", "ScheduleTypeLimits:Onoff", "1"));
        AssertFields(Assert.Single(document["Schedule:Compact"], item => item.Name == "caselight"), CompactFields("caselight", "ScheduleTypeLimits:Onoff", "0"));
        Assert.Equal(new[] { "CaseLight", "caselight" }, document["Lights"].Select(item => item[2]));
        Assert.Equal(new[] { "light:Case-Zone-1", "light:Case-Zone-2" }, document["Lights"].Select(item => item.Name));
        AssertLegacyFallback(document, "Case-Zone-1", "Case-Zone-2");
        Assert.Equal(
            pythonFacts.GetProperty("used_profiles").EnumerateArray().Select(item => RequiredString(item, "name")),
            model.UsedProfiles.Select(item => item.Name));
        Assert.Throws<InvalidOperationException>(() => model.ToIdfDocument());
    }

    private static void ExecuteDuplicateProfiles(JsonElement pythonFacts)
    {
        EnergyModel model = CreateDuplicateProfilesModel();

        IdfDocument document = AssembleLegacyDeterministically(model);
        AssertCompactScheduleNames(document, "ALLON", "ALLOFF", "Light-B");
        Assert.DoesNotContain(document["Schedule:Compact"], item => item.Name == "Light-A");
        Assert.Equal(new[] { "Light-A", "Light-B" }, document["Lights"].Select(item => item[2]));
        Assert.Equal(
            pythonFacts.GetProperty("missing_schedule_references").EnumerateArray().Select(item => item.GetString()),
            document["Lights"].Select(item => item[2]).Where(name => !document["Schedule:Compact"].Any(schedule => schedule.Name == name)));
        Assert.Same(model.Zones[1].Profile, Assert.Single(model.UsedProfiles));
        AssertLegacyFallback(document, "Exact-Zone-1", "Exact-Zone-2");

        IdfDocument safe = model.ToIdfDocument();
        Assert.Contains(safe["Schedule:Compact"], item => item.Name == "Light-A");
        Assert.Contains(safe["Schedule:Compact"], item => item.Name == "Light-B");
        Assert.Equal(
            new[] { "IdealThermostat_for_Exact-Zone-1", "IdealThermostat_for_Exact-Zone-2" },
            safe["HVACTemplate:Thermostat"].Select(item => item.Name));
    }

    private static void ExecuteLegacyErv(JsonElement pythonFacts)
    {
        EnergyModel model = CreateLegacyErvModel();

        IdfDocument document = AssembleLegacyDeterministically(model);
        AssertCompactScheduleNames(document, "ALLON", "ALLOFF", "Occ-ERV", "Occ-ERV_normalized:for:ERV-Zone:occupant");
        AssertFields(document["Schedule:Compact"]["Occ-ERV"], CompactFields("Occ-ERV", "ScheduleTypeLimits:Real", "1.0"));
        AssertFields(
            document["Schedule:Compact"]["Occ-ERV_normalized:for:ERV-Zone:occupant"],
            CompactFields("Occ-ERV_normalized:for:ERV-Zone:occupant", "ScheduleTypeLimits:Real", "1.0"));
        IdfObject people = Assert.Single(document["People"]);
        Assert.Equal("people:ERV-Zone", people.Name);
        Assert.Equal("ERV-Zone", people[1]);
        Assert.Equal("Occ-ERV_normalized:for:ERV-Zone:occupant", people[2]);
        Assert.Equal("$DEFAULT$PEOPLEACTIVITY", people[9]);
        IdfObject ventilation = Assert.Single(document["ZoneVentilation:DesignFlowRate"]);
        AssertFields(
            ventilation,
            "NaturalVentilation:ERV-Zone", "ERV-Zone", string.Empty, "Flow/Person", string.Empty,
            string.Empty, "0.00332", string.Empty, "Exhaust", "125", "0.85");
        Assert.Empty(document["OutdoorAir:Node"]);
        Assert.Empty(document["HeatExchanger:AirToAir:SensibleAndLatent"]);
        Assert.Empty(document["Fan:OnOff"]);
        Assert.Empty(document["ZoneHVAC:EnergyRecoveryVentilator:Controller"]);
        Assert.Empty(document["ZoneHVAC:EnergyRecoveryVentilator"]);
        Assert.Empty(pythonFacts.GetProperty("heat_recovery_nonempty_families").EnumerateArray());
        AssertLegacyFallback(document, "ERV-Zone");

        IdfDocument safe = model.ToIdfDocument();
        Assert.Single(safe["HeatExchanger:AirToAir:SensibleAndLatent"]);
        Assert.Single(safe["ZoneHVAC:EnergyRecoveryVentilator"]);
        Assert.Empty(safe["HVACTemplate:Thermostat"]);
        Assert.Empty(safe["HVACTemplate:Zone:IdealLoadsAirSystem"]);
    }

    private static void ExecuteTwoUnconditioned(JsonElement pythonFacts)
    {
        EnergyModel model = CreateTwoUnconditionedModel();

        IdfDocument document = AssembleLegacyDeterministically(model);
        Assert.Equal(1, document["Schedule:Compact"].Count(item => item.Name == "ALLON"));
        Assert.Equal(pythonFacts.GetProperty("allon_object_count").GetInt32(), document["Schedule:Compact"].Count(item => item.Name == "ALLON"));
        AssertLegacyFallback(document, "Unconditioned-First", "Unconditioned-Second");

        IdfDocument safe = model.ToIdfDocument();
        Assert.Equal(
            new[] { "IdealThermostat_for_Unconditioned-First", "IdealThermostat_for_Unconditioned-Second" },
            safe["HVACTemplate:Thermostat"].Select(item => item.Name));
        Assert.Equal(
            new[] { "Unconditioned-First", "Unconditioned-Second" },
            safe["HVACTemplate:Zone:IdealLoadsAirSystem"].Select(item => item.Name));
    }

    private static void ExecuteSchemaBoundCase(
        CaseBinding binding,
        JsonElement pythonFacts,
        IddSchema schema)
    {
        EnergyModel model = CreateNativeModel(binding.CaseId);
        IdfDocument document = AssembleLegacyDeterministically(model, schema);
        Assert.Same(schema, document.Schema);

        Assert.Equal(
            RawObjectNames(pythonFacts.GetProperty("schedule_compact")),
            document["Schedule:Compact"].Select(item => item.Name));
        AssertEffectiveObjects(
            document["HVACTemplate:Thermostat"],
            pythonFacts.GetProperty("fallback_thermostats"),
            5,
            requireDefinitions: true);
        AssertEffectiveObjects(
            document["HVACTemplate:Zone:IdealLoadsAirSystem"],
            pythonFacts.GetProperty("fallback_ideal_loads"),
            30,
            requireDefinitions: true);

        if (pythonFacts.TryGetProperty("ventilation", out JsonElement ventilation))
        {
            AssertEffectiveObjects(
                document["ZoneVentilation:DesignFlowRate"],
                ventilation,
                26,
                requireDefinitions: true);
        }

        if (pythonFacts.TryGetProperty("default_objects", out JsonElement defaultObjects))
        {
            AssertNativeDefaultObjects(document, defaultObjects, requireDefinitions: true);
        }
    }

    private static EnergyModel CreateNativeModel(string caseId)
    {
        if (caseId == ExpectedCases[0].CaseId)
        {
            return CreateAssignedWithoutAvailabilityModel();
        }

        if (caseId == ExpectedCases[1].CaseId)
        {
            return CreateCaseDistinctProfilesModel();
        }

        if (caseId == ExpectedCases[2].CaseId)
        {
            return CreateDuplicateProfilesModel();
        }

        if (caseId == ExpectedCases[3].CaseId)
        {
            return CreateLegacyErvModel();
        }

        Assert.Equal(ExpectedCases[4].CaseId, caseId);
        return CreateTwoUnconditionedModel();
    }

    private static EnergyModel CreateAssignedWithoutAvailabilityModel()
    {
        Schedule heating = Schedule.Constant("Heat-Assigned", 20, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant("Cool-Assigned", 26, ScheduleType.Temperature);
        Zone zone = CreateZone(
            "Assigned-Zone",
            new ZoneProfile(new EntityId("PROFILE-ASSIGNED"), "Assigned-Profile", heating, cooling),
            0);
        var radiator = new ElectricRadiator(new EntityId("SUPPLY-ASSIGNED"), "Assigned-Electric", 1_000);
        return new EnergyModel(
            "assigned-without-availability",
            new[] { zone },
            new[] { new ZoneHvacAssignment(zone.Id, new SupplyGroup(new SupplySystem[] { radiator })) });
    }

    private static EnergyModel CreateCaseDistinctProfilesModel()
    {
        Zone first = CreateZone(
            "Case-Zone-1",
            new ZoneProfile(new EntityId("PROFILE-CASE-1"), "CaseProfile", lighting: Schedule.Constant("CaseLight", 1, ScheduleType.OnOff)),
            0,
            lightingPowerDensity: 5);
        Zone second = CreateZone(
            "Case-Zone-2",
            new ZoneProfile(new EntityId("PROFILE-CASE-2"), "caseprofile", lighting: Schedule.Constant("caselight", 0, ScheduleType.OnOff)),
            2,
            lightingPowerDensity: 5);
        return new EnergyModel("case-distinct-profile-schedules", new[] { first, second });
    }

    private static EnergyModel CreateDuplicateProfilesModel()
    {
        Zone first = CreateZone(
            "Exact-Zone-1",
            new ZoneProfile(new EntityId("PROFILE-EXACT-1"), "DUPLICATE-PROFILE", lighting: Schedule.Constant("Light-A", 1, ScheduleType.OnOff)),
            0,
            lightingPowerDensity: 5);
        Zone second = CreateZone(
            "Exact-Zone-2",
            new ZoneProfile(new EntityId("PROFILE-EXACT-2"), "DUPLICATE-PROFILE", lighting: Schedule.Constant("Light-B", 0, ScheduleType.OnOff)),
            2,
            lightingPowerDensity: 5);
        return new EnergyModel("duplicate-profile-last-wins", new[] { first, second });
    }

    private static EnergyModel CreateLegacyErvModel()
    {
        Zone zone = CreateZone(
            "ERV-Zone",
            new ZoneProfile(
                new EntityId("PROFILE-ERV"),
                "ERV-Profile",
                occupant: Schedule.Constant("Occ-ERV", 1, ScheduleType.Real)),
            0);
        var ventilator = new EnergyRecoveryVentilator(new EntityId("ERV-LEGACY"), "Legacy-ERV", 0.7, 0.5, 0.2);
        return new EnergyModel(
            "legacy-erv-unconditioned",
            new[] { zone },
            ventilationAssignments: new[] { new ZoneVentilationAssignment(zone.Id, ventilator) });
    }

    private static EnergyModel CreateTwoUnconditionedModel()
    {
        Zone first = CreateZone("Unconditioned-First", new ZoneProfile(new EntityId("PROFILE-FIRST"), "First-Profile"), 0);
        Zone second = CreateZone("Unconditioned-Second", new ZoneProfile(new EntityId("PROFILE-SECOND"), "Second-Profile"), 2);
        return new EnergyModel("two-unconditioned", new[] { first, second });
    }

    private static IdfDocument AssembleLegacyDeterministically(
        EnergyModel model,
        IddSchema? schema = null)
    {
        EnergyModelIdfOptions options = LegacyOptions();
        IdfDocument first = model.ToIdfDocument(schema, options);
        IdfDocument second = model.ToIdfDocument(schema, LegacyOptions());
        Assert.NotSame(first, second);
        Assert.Equal(IdfWriter.Write(first), IdfWriter.Write(second));
        Assert.Single(first["Version"]);
        Assert.Single(first["SimulationControl"]);
        Assert.Single(first["Building"]);
        AssertFields(first["Schedule:Compact"]["ALLON"], "ALLON", string.Empty, "Through: 12/31", "For: AllDays", "Until: 24:00", "1");
        AssertFields(first["Schedule:Compact"]["ALLOFF"], "ALLOFF", string.Empty, "Through: 12/31", "For: AllDays", "Until: 24:00", "0");
        return first;
    }

    private static void AssertLegacyFallback(IdfDocument document, params string[] zoneNames)
    {
        AssertFields(
            Assert.Single(document["HVACTemplate:Thermostat"]),
            "UNCONDITIONED_THERMOSTAT", string.Empty, "-30", string.Empty, "50");
        Assert.Equal(zoneNames, document["HVACTemplate:Zone:IdealLoadsAirSystem"].Select(item => item.Name));
        Assert.Equal(zoneNames.Length, document["HVACTemplate:Zone:IdealLoadsAirSystem"].Count);
        for (int index = 0; index < zoneNames.Length; index++)
        {
            AssertFields(
                document["HVACTemplate:Zone:IdealLoadsAirSystem"][index],
                zoneNames[index], "UNCONDITIONED_THERMOSTAT", "ALLON");
        }
    }

    private static void AssertSafeDefaultsRemainEnabled()
    {
        var options = new EnergyModelIdfOptions();
        Assert.True(options.ThrowOnValidationErrors);
        Assert.True(options.AddIdealLoadsForUnassignedZones);
        Assert.False(options.UseLegacyRectangularFenestration);
        Assert.False(options.UseLegacySimpleDragonDefaultObjectFields);
        Assert.False(options.UseLegacySimpleDragonScheduleMetadata);
        Assert.False(options.UseLegacySimpleDragonUsedProfileScheduleSelection);
        Assert.False(options.UseLegacySimpleDragonHvacTopology);
        Assert.False(options.UseLegacySimpleDragonVentilation);
    }

    private static EnergyModelIdfOptions LegacyOptions() => new()
    {
        UseLegacyRectangularFenestration = true,
        UseLegacySimpleDragonDefaultObjectFields = true,
        UseLegacySimpleDragonScheduleMetadata = true,
        UseLegacySimpleDragonUsedProfileScheduleSelection = true,
        UseLegacySimpleDragonHvacTopology = true,
        UseLegacySimpleDragonVentilation = true,
    };

    private static Zone CreateZone(
        string name,
        ZoneProfile profile,
        double x,
        double lightingPowerDensity = 0)
    {
        Surface floor = TestDomainFactory.Surface(
            "SURFACE-" + name,
            "Floor " + name,
            TestDomainFactory.Square(x: x),
            SurfaceType.Floor,
            SurfaceBoundary.Ground);
        return new Zone(
            new EntityId("ZONE-" + name),
            name,
            new[] { floor },
            profile,
            lightingPowerDensityWattsPerSquareMetre: lightingPowerDensity);
    }

    private static void AssertFallbacks(JsonElement facts, params string[] zones)
    {
        JsonElement[] thermostats = facts.GetProperty("fallback_thermostats").EnumerateArray().ToArray();
        Assert.Single(thermostats);
        AssertRawObject(thermostats[0], 5, FallbackThermostat());
        JsonElement[] idealLoads = facts.GetProperty("fallback_ideal_loads").EnumerateArray().ToArray();
        Assert.Equal(zones.Length, idealLoads.Length);
        for (int index = 0; index < zones.Length; index++)
        {
            AssertRawObject(idealLoads[index], 30, FallbackIdeal(zones[index]));
        }
    }

    private static void AssertDefaultObjectFacts(JsonElement defaults)
    {
        AssertKeys(
            defaults,
            "global_geometry_rules",
            "people_activity_schedule_constants",
            "schedule_compact",
            "schedule_type_limits");
        AssertRawObjects(
            defaults.GetProperty("global_geometry_rules"),
            new RawExpectation(
                5,
                new[]
                {
                    S("UpperLeftCorner"),
                    S("Counterclockwise"),
                    S("World"),
                    S("Relative"),
                    S("Relative"),
                }));
        AssertRawObjects(
            defaults.GetProperty("people_activity_schedule_constants"),
            new RawExpectation(
                3,
                new[]
                {
                    S("$DEFAULT$PEOPLEACTIVITY"),
                    E("real", "ScheduleType"),
                    F("107.0"),
                }));
        AssertRawObjects(
            defaults.GetProperty("schedule_compact"),
            DefaultCompact("ALLON", "1"),
            DefaultCompact("ALLOFF", "0"));
        AssertRawObjects(
            defaults.GetProperty("schedule_type_limits"),
            new RawExpectation(
                5,
                new[]
                {
                    S("ScheduleTypeLimits:Temperature"), I("-50"), I("200"),
                    S("Continuous"), S("Temperature"),
                }),
            new RawExpectation(
                5,
                new[]
                {
                    S("ScheduleTypeLimits:Onoff"), I("0"), I("1"),
                    S("Discrete"), S("Dimensionless"),
                }),
            new RawExpectation(
                5,
                new[]
                {
                    S("ScheduleTypeLimits:Fraction"), I("0"), I("1"),
                    S("Continuous"), S("Dimensionless"),
                }),
            new RawExpectation(
                5,
                new[]
                {
                    S("ScheduleTypeLimits:Real"), N(), N(),
                    S("Continuous"), S("Dimensionless"),
                }));
    }

    private static void AssertNativeDefaultObjects(
        IdfDocument document,
        JsonElement defaults,
        bool requireDefinitions)
    {
        AssertEffectiveObjects(
            document["GlobalGeometryRules"],
            defaults.GetProperty("global_geometry_rules"),
            5,
            requireDefinitions);
        AssertEffectiveObjects(
            document["Schedule:Constant"],
            defaults.GetProperty("people_activity_schedule_constants"),
            3,
            requireDefinitions);
        AssertEffectiveObjects(
            document["Schedule:Compact"].Take(2),
            defaults.GetProperty("schedule_compact"),
            6,
            requireDefinitions);
        AssertEffectiveObjects(
            document["ScheduleTypeLimits"],
            defaults.GetProperty("schedule_type_limits"),
            5,
            requireDefinitions);
    }

    private static void AssertEffectiveObjects(
        IEnumerable<IdfObject> nativeObjects,
        JsonElement encodedObjects,
        int effectiveFieldCount,
        bool requireDefinitions)
    {
        IdfObject[] native = nativeObjects.ToArray();
        JsonElement[] encoded = encodedObjects.EnumerateArray().ToArray();
        Assert.Equal(encoded.Length, native.Length);
        for (int objectIndex = 0; objectIndex < encoded.Length; objectIndex++)
        {
            JsonElement[] tokens = encoded[objectIndex].GetProperty("values").EnumerateArray().ToArray();
            Assert.Equal(effectiveFieldCount, tokens.Length);
            if (!requireDefinitions)
            {
                Assert.Equal(effectiveFieldCount, native[objectIndex].Count);
            }

            for (int fieldIndex = 0; fieldIndex < effectiveFieldCount; fieldIndex++)
            {
                IddFieldDefinition? definition = native[objectIndex].Definition?.ResolveField(fieldIndex);
                if (requireDefinitions)
                {
                    Assert.NotNull(native[objectIndex].Definition);
                    Assert.NotNull(definition);
                }

                string actual = fieldIndex < native[objectIndex].Count
                    ? native[objectIndex][fieldIndex]
                    : string.Empty;
                if (string.IsNullOrEmpty(actual) && definition?.DefaultValue is not null)
                {
                    actual = definition.DefaultValue;
                }

                AssertEffectiveToken(tokens[fieldIndex], actual);
            }
        }
    }

    private static void AssertEffectiveToken(JsonElement expected, string actual)
    {
        string kind = RequiredString(expected, "kind");
        if (kind == "none")
        {
            Assert.Equal(string.Empty, actual);
        }
        else if (kind == "str")
        {
            Assert.Equal(RequiredString(expected, "value"), actual);
        }
        else if (kind == "enum")
        {
            Assert.Equal(RequiredString(expected, "value"), actual);
        }
        else
        {
            Assert.True(kind is "int" or "float");
            Assert.True(
                decimal.TryParse(
                    RequiredString(expected, "repr"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal expectedNumber));
            Assert.True(
                decimal.TryParse(
                    actual,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal actualNumber));
            Assert.Equal(expectedNumber, actualNumber);
        }
    }

    private static string[] RawObjectNames(JsonElement encodedObjects) =>
        encodedObjects.EnumerateArray()
            .Select(item => RequiredString(item.GetProperty("values")[0], "value"))
            .ToArray();

    private static void AssertScheduleCompact(JsonElement value, params RawExpectation[] expected)
    {
        AssertRawObjects(value, expected);
    }

    private static void AssertRawObjects(JsonElement value, params RawExpectation[] expected)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            AssertRawObject(actual[index], expected[index].StoredFieldCount, expected[index].Values);
        }
    }

    private static void AssertRawObject(JsonElement value, int storedFieldCount, params RawToken[] expected)
    {
        AssertKeys(value, "stored_field_count", "values");
        Assert.Equal(storedFieldCount, value.GetProperty("stored_field_count").GetInt32());
        JsonElement[] values = value.GetProperty("values").EnumerateArray().ToArray();
        Assert.Equal(expected.Length, values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            RawToken token = expected[index];
            JsonElement actual = values[index];
            if (token.Kind == "none")
            {
                AssertKeys(actual, "kind");
                Assert.Equal("none", RequiredString(actual, "kind"));
            }
            else if (token.Kind == "str")
            {
                AssertKeys(actual, "kind", "value");
                Assert.Equal("str", RequiredString(actual, "kind"));
                Assert.Equal(token.Text, RequiredString(actual, "value"));
            }
            else if (token.Kind == "enum")
            {
                AssertKeys(actual, "enum_type", "kind", "text", "value");
                Assert.Equal("enum", RequiredString(actual, "kind"));
                Assert.Equal(token.EnumType, RequiredString(actual, "enum_type"));
                Assert.Equal(token.Text, RequiredString(actual, "text"));
                Assert.Equal(token.Text, RequiredString(actual, "value"));
            }
            else
            {
                Assert.True(token.Kind is "int" or "float");
                AssertKeys(actual, "kind", "repr");
                Assert.Equal(token.Kind, RequiredString(actual, "kind"));
                Assert.Equal(token.Text, RequiredString(actual, "repr"));
            }
        }
    }

    private static void AssertFamilies(JsonElement value, params Family[] expected)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            AssertKeys(actual[index], "count", "object_type");
            Assert.Equal(expected[index].ObjectType, RequiredString(actual[index], "object_type"));
            Assert.Equal(expected[index].Count, actual[index].GetProperty("count").GetInt32());
        }
    }

    private static Family[] ExpectedFamilies(int scheduleCount, params Family[] middle)
    {
        Family[] prefix =
        {
            new("Version", 1),
            new("SimulationControl", 1),
            new("Building", 1),
            new("Timestep", 1),
            new("SizingPeriod:WeatherFileDays", 2),
            new("RunPeriod", 1),
            new("ScheduleTypeLimits", 4),
            new("Schedule:Compact", scheduleCount),
            new("Schedule:Constant", 1),
            new("GlobalGeometryRules", 1),
        };
        Family[] suffix =
        {
            new("Output:Table:SummaryReports", 1),
            new("Output:Table:Monthly", 1),
            new("OutputControl:Table:Style", 1),
        };
        return prefix.Concat(middle).Concat(suffix).ToArray();
    }

    private static void AssertLights(JsonElement value, params LightFact[] expected)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            AssertKeys(actual[index], "name", "schedule_name", "stored_field_count", "zone_name");
            Assert.Equal(expected[index].Name, RequiredString(actual[index], "name"));
            Assert.Equal(expected[index].ZoneName, RequiredString(actual[index], "zone_name"));
            Assert.Equal(expected[index].ScheduleName, RequiredString(actual[index], "schedule_name"));
            Assert.Equal(17, actual[index].GetProperty("stored_field_count").GetInt32());
        }
    }

    private static void AssertProfiles(JsonElement value, params ProfileFact[] expected)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            AssertKeys(actual[index], "lighting_schedule", "name");
            Assert.Equal(expected[index].Name, RequiredString(actual[index], "name"));
            Assert.Equal(expected[index].LightingSchedule, RequiredString(actual[index], "lighting_schedule"));
        }
    }

    private static RawExpectation DefaultCompact(string name, string value) => new(
        153,
        new[] { S(name), N(), S("Through: 12/31"), S("For: AllDays"), S("Until: 24:00"), I(value) });

    private static RawExpectation ConstantCompact(string name, string type, string value) => new(
        153,
        new[]
        {
            S(name), S(type), S("Through: 12/31"),
            S("For: Weekdays"), S("Until: 24:00"), S(value),
            S("For: Weekends"), S("Until: 24:00"), S(value),
            S("For: AllOtherDays"), S("Until: 24:00"), S(value),
        });

    private static RawToken[] FallbackThermostat() =>
        new[] { S("UNCONDITIONED_THERMOSTAT"), N(), I("-30"), N(), I("50") };

    private static RawToken[] FallbackIdeal(string zone) =>
        new[]
        {
            S(zone), S("UNCONDITIONED_THERMOSTAT"), S("ALLON"), F("50.0"), F("13.0"), F("0.0156"), F("0.0077"),
            S("NoLimit"), N(), N(), S("NoLimit"), N(), N(), N(), N(), S("ConstantSensibleHeatRatio"), F("0.7"),
            F("60.0"), S("None"), F("30.0"), S("None"), F("0.00944"), F("0.0"), F("0.0"), N(), S("None"),
            S("NoEconomizer"), S("None"), F("0.7"), F("0.65"),
        };

    private static RawToken[] LegacyVentilation() =>
        new[]
        {
            S("NaturalVentilation:ERV-Zone"), S("ERV-Zone"), N(), S("Flow/Person"), N(), N(), F("0.00332"), N(),
            S("Exhaust"), F("125.0"), F("0.85"), F("1.0"), F("0.0"), F("0.0"), F("0.0"), S("-100"), N(),
            F("100.0"), N(), S("-100"), N(), S("-100"), N(), F("100.0"), N(), F("40.0"),
        };

    private static string[] CompactFields(string name, string type, string value) =>
        new[]
        {
            name, type, "Through: 12/31",
            "For: Weekdays", "Until: 24:00", value,
            "For: Weekends", "Until: 24:00", value,
            "For: AllOtherDays", "Until: 24:00", value,
        };

    private static void AssertCompactScheduleNames(IdfDocument document, params string[] expected)
    {
        Assert.Equal(
            expected,
            document["Schedule:Compact"].Select(item => item.Name));
    }

    private static void AssertFields(IdfObject item, params string[] expected) =>
        Assert.Equal(expected, item.Fields.Select(field => field.Value));

    private static RawToken S(string value) => new("str", value);

    private static RawToken I(string value) => new("int", value);

    private static RawToken F(string value) => new("float", value);

    private static RawToken E(string value, string enumType) => new("enum", value, enumType);

    private static RawToken N() => new("none", null);

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
        string[] actual = value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal), actual);
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
    }

    private static IddSchema LoadPinnedEnergyPlusSchema()
    {
        string path = FindEnergyPlusIdd()
            ?? throw new FileNotFoundException(
                "GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION=1, but no installed EnergyPlus 24.2 Energy+.idd was found.");
        var source = new FileInfo(path);
        Assert.Equal(EnergyPlusIddBytes, source.Length);

        IddSchema schema = IddParser.ParseFile(path);
        Assert.Equal(EnergyPlusVersion, schema.Version);
        Assert.Equal(EnergyPlusBuild, schema.Build);
        Assert.Equal(EnergyPlusIddSha256, schema.SourceSha256);
        Assert.Equal(848, schema.Objects.Count);
        return schema;
    }

    private static string? FindEnergyPlusIdd()
    {
        string?[] roots =
        {
            Environment.GetEnvironmentVariable("GONIEGONIE_ENERGYPLUS_ROOT"),
            Environment.GetEnvironmentVariable("DRAGONS_ENERGYPLUS_HOME"),
            Environment.GetEnvironmentVariable("ENERGYPLUS_HOME"),
            Environment.GetEnvironmentVariable("ENERGYPLUS_ROOT"),
            @"C:\EnergyPlusV24-2-0",
        };
        foreach (string? root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string candidate = Path.Combine(root, "Energy+.idd");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
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

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record SourceBinding(string Path, string SourceSha256, string AstSha256);

    private sealed record Family(string ObjectType, int Count);

    private sealed record LightFact(string Name, string ZoneName, string ScheduleName);

    private sealed record ProfileFact(string Name, string LightingSchedule);

    private sealed record RawToken(string Kind, string? Text, string? EnumType = null);

    private sealed record RawExpectation(int StoredFieldCount, RawToken[] Values);
}
