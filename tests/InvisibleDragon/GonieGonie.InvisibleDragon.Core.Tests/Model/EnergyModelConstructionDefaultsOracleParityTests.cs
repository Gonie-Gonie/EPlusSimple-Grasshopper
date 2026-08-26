using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelConstructionDefaultsOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-model-construction-defaults-oracle.json";
    private const string OracleSha256 =
        "sha256:50a6c1d8cf1c9362b7cacf4462d468211ce3b993059e362bfe83eb3274cc1f13";
    private const string CasesSha256 =
        "sha256:7a2c84fc965b884bd93d4e4f12bfab5df03e491b50dd8ed8ecda9eb4d6b21c84";
    private const int OracleByteLength = 21_802;
    private const int ExpectedCaseCount = 9;
    private const string OracleSchema =
        "goniegonie.python-reference.dragon-model-construction-defaults.v1";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Model.EnergyModelConstructionDefaultsOracleParityTests.MatchesPinnedPythonConstructionDefaults";
    private const string EnergyModelTypeName =
        "GonieGonie.InvisibleDragon.Model.EnergyModel";
    private const string EnergyModelRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs";
    private const string EnergyModelSourceSha256 =
        "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3";
    private const string AssemblerRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs";
    private const string AssemblerSourceSha256 =
        "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905";

    private static readonly string[] DefaultObjectTypes =
    {
        "Version",
        "SimulationControl",
        "Timestep",
        "SizingPeriod:WeatherFileDays",
        "SizingPeriod:WeatherFileDays",
        "RunPeriod",
        "ScheduleTypeLimits",
        "ScheduleTypeLimits",
        "ScheduleTypeLimits",
        "ScheduleTypeLimits",
        "Schedule:Compact",
        "Schedule:Compact",
        "Schedule:Constant",
        "GlobalGeometryRules",
        "Output:Table:SummaryReports",
        "Output:Table:Monthly",
        "OutputControl:Table:Style",
    };

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/dragon/model.py", "EnergyModel.__init__", "sha256:1d1dbee8fef8b70b2919c4e46a0ea60efbd748b360d31ff353ea121c72ad97d2", "dragon-model-construction-defaults-init-1d1dbee8"),
        new("src/idragon/dragon/model.py", "EnergyModel.create_default_idf", "sha256:585b53682bd5dbd4d2081e79eddc2789fa60925baafb5eae26de0541346ac9f4", "dragon-model-construction-defaults-create-default-idf-585b5368"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("EnergyModel.__init__", "function", "sha256:9706dcab3a90048744a47f3596613b34247cb6cd1eb2903582e2fb2cb6342a2d", "sha256:e4e5ef56fd12719fe976231c03d867e932eff64870f9c0fd7a5107b7e11538f1", "exception", "immutable-validated-energy-model-construction", EnergyModelTypeName + ".EnergyModel"),
        new("EnergyModel.create_default_idf", "function", "sha256:6750822d2a0b36e44dced756c45817742cfc0940e8646be6212eedfe3698d8cf", "sha256:e505591e57b64f4f7ff0b6fb18e775ad88048d4eaddb9d8a4f9e5a0afd2c8ab7", "equivalent", null, EnergyModelTypeName + ".CreateDefaultIdfDocument"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-model-construction-defaults.create-default-idf.argument-rejection", "energy-model-create-default-idf", "EnergyModel.create_default_idf"),
        new("dragon-model-construction-defaults.create-default-idf.exact-family-order-count", "energy-model-create-default-idf", "EnergyModel.create_default_idf"),
        new("dragon-model-construction-defaults.create-default-idf.fresh-mutation-isolation", "energy-model-create-default-idf", "EnergyModel.create_default_idf"),
        new("dragon-model-construction-defaults.create-default-idf.global-schedule-raw-fields", "energy-model-create-default-idf", "EnergyModel.create_default_idf"),
        new("dragon-model-construction-defaults.create-default-idf.output-objects", "energy-model-create-default-idf", "EnergyModel.create_default_idf"),
        new("dragon-model-construction-defaults.init.call-shape-errors", "energy-model-init", "EnergyModel.__init__"),
        new("dragon-model-construction-defaults.init.explicit-aliasing", "energy-model-init", "EnergyModel.__init__"),
        new("dragon-model-construction-defaults.init.permissive-invalid-values", "energy-model-init", "EnergyModel.__init__"),
        new("dragon-model-construction-defaults.init.shared-defaults-signature", "energy-model-init", "EnergyModel.__init__"),
    };

    [Fact]
    public void MatchesPinnedPythonConstructionDefaults()
    {
        AssertPinnedRepositoryFile(EnergyModelRepositoryPath, EnergyModelSourceSha256);
        AssertPinnedRepositoryFile(AssemblerRepositoryPath, AssemblerSourceSha256);

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
            int expectedCount = evidence.Symbol == "EnergyModel.__init__" ? 4 : 5;
            Assert.Equal(expectedCount, symbolObservations.Length);
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

        Assert.Equal(
            new[] { 4, 5 },
            cases.GroupBy(item => RequiredString(item, "symbol"))
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Count()));
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
            "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0",
            RequiredString(upstream, "inventory_sha256"));

        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(5, sources.Length);
        AssertSource(sources[0], "src/idragon/common.py", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9");
        AssertSource(sources[1], "src/idragon/constants.py", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084");
        AssertSource(sources[2], "src/idragon/dragon/model.py", "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59");
        AssertSource(sources[3], "src/idragon/dragon/profile.py", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef");
        AssertSource(sources[4], "src/idragon/imugi.py", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90");
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
        Assert.Equal(2, ExpectedEvidence.Length);
        Assert.Equal(2, ExpectedSymbols.Length);
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
            "identity_encoding",
            "raw_field_encoding",
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
            "logical-label-and-boolean-only-no-id-or-address",
            RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "typed-kind-plus-value-or-repr-with-trailing-none-trimmed",
            RequiredString(contract, "raw_field_encoding"));
        Assert.Equal(
            "pinned-python-builtins-and-enums-only-no-native-type-name-claims",
            RequiredString(contract, "runtime_names"));

        JsonElement adaptations = contract.GetProperty("adaptations");
        AssertKeys(adaptations, "EnergyModel.__init__");
        Assert.Equal(
            "immutable-validated-energy-model-construction",
            RequiredString(adaptations, "EnergyModel.__init__"));

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
        ConstructorInfo constructor = ModelConstructor();
        ParameterInfo[] parameters = constructor.GetParameters();
        Assert.Equal(8, parameters.Length);
        AssertParameter(parameters[0], "name", typeof(string), optional: false);
        AssertParameter(parameters[1], "zones", typeof(IEnumerable<Zone>), optional: false);
        AssertParameter(parameters[2], "hvacAssignments", typeof(IEnumerable<ZoneHvacAssignment>), optional: true);
        AssertParameter(parameters[3], "ventilationAssignments", typeof(IEnumerable<ZoneVentilationAssignment>), optional: true);
        AssertParameter(parameters[4], "photovoltaicPanels", typeof(IEnumerable<PhotovoltaicPanel>), optional: true);
        AssertParameter(parameters[5], "northAxisDegrees", typeof(double), optional: true);
        AssertParameter(parameters[6], "terrain", typeof(Terrain), optional: true);
        AssertParameter(parameters[7], "outputTables", typeof(OutputTableSettings), optional: true);
        Assert.Null(parameters[2].DefaultValue);
        Assert.Null(parameters[3].DefaultValue);
        Assert.Null(parameters[4].DefaultValue);
        Assert.Equal(0d, parameters[5].DefaultValue);
        Assert.Equal(Terrain.Suburbs, parameters[6].DefaultValue);
        Assert.Null(parameters[7].DefaultValue);

        MethodInfo factory = DefaultFactory();
        Assert.Equal(typeof(EnergyModel), factory.DeclaringType);
        Assert.True(factory.IsPublic);
        Assert.True(factory.IsStatic);
        Assert.Equal(typeof(IdfDocument), factory.ReturnType);
        Assert.Empty(factory.GetParameters());
        Assert.Equal(EnergyModelTypeName + ".EnergyModel", ExpectedSymbols[0].ImplementationSymbol);
        Assert.Equal(
            EnergyModelTypeName + ".CreateDefaultIdfDocument",
            ExpectedSymbols[1].ImplementationSymbol);
    }

    private static void AssertParameter(
        ParameterInfo parameter,
        string name,
        Type type,
        bool optional)
    {
        Assert.Equal(name, parameter.Name);
        Assert.Equal(type, parameter.ParameterType);
        Assert.Equal(optional, parameter.IsOptional);
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
            "energy-model-create-default-idf" => ExecuteCreateDefaultCase(binding.CaseId),
            "energy-model-init" => ExecuteInitCase(binding.CaseId),
            _ => throw new InvalidOperationException(
                "Unknown construction-defaults executor: " + binding.Executor),
        };
    }

    private static void ValidatePythonFacts(string caseId, JsonElement facts)
    {
        if (caseId == ExpectedCases[0].CaseId)
        {
            AssertKeys(
                facts,
                "positional_argument_error_type",
                "signature_text",
                "staticmethod_descriptor",
                "unexpected_keyword_error_type");
            Assert.Equal("TypeError", RequiredString(facts, "positional_argument_error_type"));
            Assert.Equal("() -> 'IDF'", RequiredString(facts, "signature_text"));
            Assert.True(facts.GetProperty("staticmethod_descriptor").GetBoolean());
            Assert.Equal("TypeError", RequiredString(facts, "unexpected_keyword_error_type"));
            return;
        }

        if (caseId == ExpectedCases[1].CaseId)
        {
            AssertKeys(
                facts,
                "building_object_count",
                "ensure_validity",
                "flat_object_types",
                "nonempty_families",
                "object_count",
                "version_components",
                "version_field");
            Assert.Equal(0, facts.GetProperty("building_object_count").GetInt32());
            Assert.False(facts.GetProperty("ensure_validity").GetBoolean());
            AssertStringArray(facts.GetProperty("flat_object_types"), DefaultObjectTypes);
            Assert.Equal(17, facts.GetProperty("object_count").GetInt32());
            AssertIntArray(facts.GetProperty("version_components"), 24, 2, 0);
            AssertEncodedValues(facts.GetProperty("version_field"), singleObject: true, "f:24.2");
            AssertFamilies(facts.GetProperty("nonempty_families"));
            return;
        }

        if (caseId == ExpectedCases[2].CaseId)
        {
            AssertKeys(
                facts,
                "all_corresponding_objects_are_distinct",
                "first_allon_name_after_mutation",
                "first_building_count_after_mutation",
                "first_count_after_mutation",
                "fresh_idf_instances",
                "second_allon_name_after_first_mutation",
                "second_building_count_after_first_mutation",
                "second_count_after_first_mutation",
                "shared_immutable_idd_schema");
            Assert.True(facts.GetProperty("all_corresponding_objects_are_distinct").GetBoolean());
            Assert.Equal("MUTATED-🐉", RequiredString(facts, "first_allon_name_after_mutation"));
            Assert.Equal(1, facts.GetProperty("first_building_count_after_mutation").GetInt32());
            Assert.Equal(18, facts.GetProperty("first_count_after_mutation").GetInt32());
            Assert.True(facts.GetProperty("fresh_idf_instances").GetBoolean());
            Assert.Equal("ALLON", RequiredString(facts, "second_allon_name_after_first_mutation"));
            Assert.Equal(0, facts.GetProperty("second_building_count_after_first_mutation").GetInt32());
            Assert.Equal(17, facts.GetProperty("second_count_after_first_mutation").GetInt32());
            Assert.True(facts.GetProperty("shared_immutable_idd_schema").GetBoolean());
            return;
        }

        if (caseId == ExpectedCases[3].CaseId)
        {
            ValidatePythonRawDefaultFacts(facts);
            return;
        }

        if (caseId == ExpectedCases[4].CaseId)
        {
            ValidatePythonOutputFacts(facts);
            return;
        }

        if (caseId == ExpectedCases[5].CaseId)
        {
            AssertKeys(
                facts,
                "missing_name_error_type",
                "positional_pv_error_type",
                "unexpected_keyword_error_type");
            Assert.Equal("TypeError", RequiredString(facts, "missing_name_error_type"));
            Assert.Equal("TypeError", RequiredString(facts, "positional_pv_error_type"));
            Assert.Equal("TypeError", RequiredString(facts, "unexpected_keyword_error_type"));
            return;
        }

        if (caseId == ExpectedCases[6].CaseId)
        {
            AssertKeys(
                facts,
                "explicit_pv_is_input_list",
                "explicit_zone_is_input_list",
                "input_mutation_visible_in_model",
                "model_mutation_visible_in_input",
                "pv_labels_after_bidirectional_mutation",
                "zone_labels_after_bidirectional_mutation");
            Assert.True(facts.GetProperty("explicit_pv_is_input_list").GetBoolean());
            Assert.True(facts.GetProperty("explicit_zone_is_input_list").GetBoolean());
            Assert.True(facts.GetProperty("input_mutation_visible_in_model").GetBoolean());
            Assert.True(facts.GetProperty("model_mutation_visible_in_input").GetBoolean());
            AssertStringArray(
                facts.GetProperty("pv_labels_after_bidirectional_mutation"),
                "pv:initial-🐉",
                "pv:input-appended",
                "pv:model-appended");
            AssertStringArray(
                facts.GetProperty("zone_labels_after_bidirectional_mutation"),
                "zone:initial-용",
                "zone:input-appended",
                "zone:model-appended");
            return;
        }

        if (caseId == ExpectedCases[7].CaseId)
        {
            AssertKeys(
                facts,
                "constructed_without_error",
                "name_is_none",
                "north_axis_identity_preserved",
                "north_axis_type",
                "pv_is_none",
                "terrain_identity_preserved",
                "terrain_type",
                "zone_identity_preserved",
                "zone_type");
            Assert.True(facts.GetProperty("constructed_without_error").GetBoolean());
            Assert.True(facts.GetProperty("name_is_none").GetBoolean());
            Assert.True(facts.GetProperty("north_axis_identity_preserved").GetBoolean());
            Assert.Equal("list", RequiredString(facts, "north_axis_type"));
            Assert.True(facts.GetProperty("pv_is_none").GetBoolean());
            Assert.True(facts.GetProperty("terrain_identity_preserved").GetBoolean());
            Assert.Equal("dict", RequiredString(facts, "terrain_type"));
            Assert.True(facts.GetProperty("zone_identity_preserved").GetBoolean());
            Assert.Equal("str", RequiredString(facts, "zone_type"));
            return;
        }

        Assert.Equal(ExpectedCases[8].CaseId, caseId);
        AssertKeys(
            facts,
            "first_pv_is_second_pv",
            "first_zone_is_second_zone",
            "keyword_only_parameters",
            "positional_parameters",
            "pv_default_is_distinct_from_zone_default",
            "pv_mutation_visible_cross_instance",
            "shared_pv_default_restored",
            "shared_zone_default_restored",
            "signature_text",
            "zone_mutation_visible_cross_instance");
        Assert.True(facts.GetProperty("first_pv_is_second_pv").GetBoolean());
        Assert.True(facts.GetProperty("first_zone_is_second_zone").GetBoolean());
        AssertStringArray(facts.GetProperty("keyword_only_parameters"), "pv");
        AssertStringArray(
            facts.GetProperty("positional_parameters"),
            "self",
            "name",
            "north_axis",
            "terrain",
            "zone");
        Assert.True(facts.GetProperty("pv_default_is_distinct_from_zone_default").GetBoolean());
        Assert.True(facts.GetProperty("pv_mutation_visible_cross_instance").GetBoolean());
        Assert.True(facts.GetProperty("shared_pv_default_restored").GetBoolean());
        Assert.True(facts.GetProperty("shared_zone_default_restored").GetBoolean());
        Assert.Equal(
            "(self, name: 'str', north_axis: 'int | float' = 0, terrain: 'str' = <Terrain.SUBURBS: 'Suburbs'>, zone: 'list[Zone]' = [], *, pv: 'list[PhotoVoltaicPanel]' = [])",
            RequiredString(facts, "signature_text"));
        Assert.True(facts.GetProperty("zone_mutation_visible_cross_instance").GetBoolean());
    }

    private static void AssertFamilies(JsonElement families)
    {
        (string Type, int Count)[] expected =
        {
            ("Version", 1),
            ("SimulationControl", 1),
            ("Timestep", 1),
            ("SizingPeriod:WeatherFileDays", 2),
            ("RunPeriod", 1),
            ("ScheduleTypeLimits", 4),
            ("Schedule:Compact", 2),
            ("Schedule:Constant", 1),
            ("GlobalGeometryRules", 1),
            ("Output:Table:SummaryReports", 1),
            ("Output:Table:Monthly", 1),
            ("OutputControl:Table:Style", 1),
        };
        JsonElement[] actual = families.EnumerateArray().ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            AssertKeys(actual[index], "count", "object_type");
            Assert.Equal(expected[index].Type, RequiredString(actual[index], "object_type"));
            Assert.Equal(expected[index].Count, actual[index].GetProperty("count").GetInt32());
        }
    }

    private static void ValidatePythonRawDefaultFacts(JsonElement facts)
    {
        AssertKeys(
            facts,
            "compact_schedules",
            "global_geometry_rules",
            "people_activity",
            "run_period",
            "schedule_type_limits",
            "simulation_control",
            "sizing_periods",
            "timestep");

        JsonElement[] compact = facts.GetProperty("compact_schedules").EnumerateArray().ToArray();
        Assert.Equal(2, compact.Length);
        string[][] compactValues =
        {
            new[] { "s:ALLON", "n:", "s:Through: 12/31", "s:For: AllDays", "s:Until: 24:00", "i:1" },
            new[] { "s:ALLOFF", "n:", "s:Through: 12/31", "s:For: AllDays", "s:Until: 24:00", "i:0" },
        };
        for (int index = 0; index < compact.Length; index++)
        {
            AssertKeys(compact[index], "stored_field_count", "values");
            Assert.Equal(153, compact[index].GetProperty("stored_field_count").GetInt32());
            AssertEncodedValues(compact[index].GetProperty("values"), compactValues[index]);
        }

        AssertEncodedValues(
            facts.GetProperty("global_geometry_rules"),
            "s:UpperLeftCorner",
            "s:Counterclockwise",
            "s:World",
            "s:Relative",
            "s:Relative");

        JsonElement activity = facts.GetProperty("people_activity");
        AssertKeys(activity, "stored_field_count", "values");
        Assert.Equal(3, activity.GetProperty("stored_field_count").GetInt32());
        AssertEncodedValues(
            activity.GetProperty("values"),
            "s:$DEFAULT$PEOPLEACTIVITY",
            "e:ScheduleType|real|real",
            "f:107.0");

        AssertEncodedValues(
            facts.GetProperty("run_period"),
            "s:Year-Round",
            "i:1",
            "i:1",
            "i:2026",
            "i:12",
            "i:31",
            "i:2026");

        JsonElement[] limits = facts.GetProperty("schedule_type_limits").EnumerateArray().ToArray();
        Assert.Equal(4, limits.Length);
        string[][] expectedLimits =
        {
            new[] { "s:ScheduleTypeLimits:Temperature", "i:-50", "i:200", "s:Continuous", "s:Temperature" },
            new[] { "s:ScheduleTypeLimits:Onoff", "i:0", "i:1", "s:Discrete", "s:Dimensionless" },
            new[] { "s:ScheduleTypeLimits:Fraction", "i:0", "i:1", "s:Continuous", "s:Dimensionless" },
            new[] { "s:ScheduleTypeLimits:Real", "n:", "n:", "s:Continuous", "s:Dimensionless" },
        };
        for (int index = 0; index < limits.Length; index++)
        {
            AssertEncodedValues(limits[index], expectedLimits[index]);
        }

        AssertEncodedValues(
            facts.GetProperty("simulation_control"),
            "s:Yes", "s:Yes", "s:Yes", "s:No", "s:Yes", "s:No");
        JsonElement[] sizing = facts.GetProperty("sizing_periods").EnumerateArray().ToArray();
        Assert.Equal(2, sizing.Length);
        AssertEncodedValues(sizing[0], "s:DesignWinter", "i:1", "i:1", "i:1", "i:31");
        AssertEncodedValues(sizing[1], "s:DesignSummer", "i:8", "i:1", "i:8", "i:31");
        AssertEncodedValues(facts.GetProperty("timestep"), "i:6");
    }

    private static void ValidatePythonOutputFacts(JsonElement facts)
    {
        AssertKeys(facts, "monthly", "style", "summary");
        JsonElement monthly = facts.GetProperty("monthly");
        AssertKeys(monthly, "stored_field_count", "values");
        Assert.Equal(52, monthly.GetProperty("stored_field_count").GetInt32());
        AssertEncodedValues(
            monthly.GetProperty("values"),
            "s:ElectricityBalanceMonthly",
            "i:3",
            "s:ElectricityProduced:Facility",
            "s:SumOrAverage",
            "s:ElectricitySurplusSold:Facility",
            "s:SumOrAverage",
            "s:ElectricityPurchased:Facility",
            "s:SumOrAverage");

        JsonElement style = facts.GetProperty("style");
        AssertKeys(style, "stored_field_count", "values");
        Assert.Equal(2, style.GetProperty("stored_field_count").GetInt32());
        AssertEncodedValues(style.GetProperty("values"), "s:Comma", "s:JtoKWH");

        JsonElement summary = facts.GetProperty("summary");
        AssertKeys(summary, "stored_field_count", "values");
        Assert.Equal(25, summary.GetProperty("stored_field_count").GetInt32());
        AssertEncodedValues(
            summary.GetProperty("values"),
            "s:EndUseEnergyConsumptionElectricityMonthly",
            "s:EndUseEnergyConsumptionNaturalGasMonthly",
            "s:EndUseEnergyConsumptionDieselMonthly",
            "s:EndUseEnergyConsumptionFuelOilMonthly",
            "s:EndUseEnergyConsumptionCoalMonthly",
            "s:EndUseEnergyConsumptionPropaneMonthly",
            "s:EndUseEnergyConsumptionGasolineMonthly",
            "s:EndUseEnergyConsumptionOtherFuelsMonthly");
    }

    private static void AssertEncodedValues(
        JsonElement value,
        bool singleObject,
        params string[] expected)
    {
        Assert.True(singleObject);
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal(AssertEncodedValue(value), Assert.Single(expected));
    }

    private static void AssertEncodedValues(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(AssertEncodedValue));
    }

    private static string AssertEncodedValue(JsonElement value)
    {
        string kind = RequiredString(value, "kind");
        switch (kind)
        {
            case "none":
                AssertKeys(value, "kind");
                return "n:";
            case "str":
                AssertKeys(value, "kind", "value");
                return "s:" + RequiredString(value, "value");
            case "int":
                AssertKeys(value, "kind", "repr");
                string integer = RequiredString(value, "repr");
                Assert.True(long.TryParse(integer, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
                return "i:" + integer;
            case "float":
                AssertKeys(value, "kind", "repr");
                string number = RequiredString(value, "repr");
                Assert.True(double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed));
                Assert.True(double.IsFinite(parsed));
                return "f:" + number;
            case "enum":
                AssertKeys(value, "enum_type", "kind", "text", "value");
                string type = RequiredString(value, "enum_type");
                string text = RequiredString(value, "text");
                string enumValue = RequiredString(value, "value");
                return "e:" + type + "|" + text + "|" + enumValue;
            default:
                throw new Xunit.Sdk.XunitException("Unknown typed oracle value: " + kind);
        }
    }

    private static string[] ExecuteCreateDefaultCase(string caseId)
    {
        if (caseId == ExpectedCases[0].CaseId)
        {
            MethodInfo method = DefaultFactory();
            Assert.Empty(method.GetParameters());
            Assert.Throws<TargetParameterCountException>(() =>
            {
                _ = method.Invoke(null, new object?[] { null });
            });
            IdfDocument document = Assert.IsType<IdfDocument>(method.Invoke(null, null));
            Assert.Null(document.Schema);
            return new[]
            {
                "factory_binding=public-static-no-argument",
                "argument_invocation=target-parameter-count-rejected",
                "return_type=IdfDocument;schema=unbound",
            };
        }

        if (caseId == ExpectedCases[1].CaseId)
        {
            IdfDocument document = EnergyModel.CreateDefaultIdfDocument();
            Assert.Equal(17, document.Count);
            Assert.Equal(DefaultObjectTypes, document.Select(item => item.ObjectType));
            Assert.Empty(document["Building"]);
            Assert.Equal("24.2", Assert.Single(document["Version"])[0]);
            Assert.Null(document.Schema);
            Assert.All(document, item => Assert.Null(item.Definition));
            return new[]
            {
                "object_count=17;building_count=0",
                "family_order=" + string.Join(">", DefaultObjectTypes),
                "version=24.2;schema=unbound;definitions=unbound",
            };
        }

        if (caseId == ExpectedCases[2].CaseId)
        {
            IdfDocument first = EnergyModel.CreateDefaultIdfDocument();
            IdfDocument second = EnergyModel.CreateDefaultIdfDocument();
            Assert.NotSame(first, second);
            Assert.Equal(first.Count, second.Count);
            for (int index = 0; index < first.Count; index++)
            {
                Assert.NotSame(first[index], second[index]);
                Assert.Equal(first[index].Count, second[index].Count);
                for (int fieldIndex = 0; fieldIndex < first[index].Count; fieldIndex++)
                {
                    Assert.NotSame(first[index].Fields[fieldIndex], second[index].Fields[fieldIndex]);
                }
            }

            first["Schedule:Compact"]["ALLON"][0] = "MUTATED-🐉";
            first.Append(new IdfObject("Building", new[] { "Mutation" }));
            Assert.Equal(18, first.Count);
            Assert.Equal("MUTATED-🐉", first["Schedule:Compact"][0].Name);
            Assert.Equal(17, second.Count);
            Assert.Equal("ALLON", second["Schedule:Compact"][0].Name);
            Assert.Empty(second["Building"]);
            Assert.Null(first.Schema);
            Assert.Null(second.Schema);
            return new[]
            {
                "documents=distinct;objects=distinct;fields=distinct;native_schema=unbound;upstream_schema_identity=representation-only",
                "first_after_mutation=count:18,allon:MUTATED-dragon,building:1",
                "second_after_first_mutation=count:17,allon:ALLON,building:0",
            };
        }

        if (caseId == ExpectedCases[3].CaseId)
        {
            IdfDocument document = EnergyModel.CreateDefaultIdfDocument();
            AssertDefaultRawFields(document);
            return new[]
            {
                "native_storage=effective-fields;upstream-trailing-none-capacity=trimmed;compact_tokens=ALLON-blank-Through-For-Until-1;ALLOFF-blank-Through-For-Until-0",
                "schedule_limits=Temperature,Onoff,Fraction,Real;people_activity=real,107.0",
                "geometry=UpperLeftCorner,Counterclockwise,World,Relative,Relative;year=2026",
            };
        }

        Assert.Equal(ExpectedCases[4].CaseId, caseId);
        IdfDocument outputDocument = EnergyModel.CreateDefaultIdfDocument();
        AssertOutputFields(outputDocument);
        return new[]
        {
            "summary_effective_fields=8;fuel_families=8",
            "monthly_effective_fields=8;digits=3;variables=3",
            "table_style=Comma,JtoKWH",
        };
    }

    private static string[] ExecuteInitCase(string caseId)
    {
        if (caseId == ExpectedCases[5].CaseId)
        {
            ConstructorInfo constructor = ModelConstructor();
            ParameterInfo[] parameters = constructor.GetParameters();
            Assert.Equal(2, parameters.Count(item => !item.IsOptional));
            Assert.Equal(6, parameters.Count(item => item.IsOptional));
            Assert.Throws<ArgumentNullException>(() =>
                new EnergyModel(null!, Array.Empty<Zone>()));
            Assert.Throws<ArgumentNullException>(() =>
                new EnergyModel("Native construction", null!));
            return new[]
            {
                "constructor_binding=public;required=name,zones;optional=6",
                "missing_name_surface=compile-time;null_name=ArgumentNullException",
                "missing_zones_surface=compile-time;null_zones=ArgumentNullException",
            };
        }

        if (caseId == ExpectedCases[6].CaseId)
        {
            Zone initialZone = CreateZone("initial-dragon");
            PhotovoltaicPanel initialPv = CreatePanel("initial-dragon");
            var inputZones = new List<Zone> { initialZone };
            var inputPv = new List<PhotovoltaicPanel> { initialPv };
            var model = new EnergyModel(
                "Native alias isolation",
                inputZones,
                photovoltaicPanels: inputPv);

            inputZones.Add(CreateZone("input-appended"));
            inputPv.Add(CreatePanel("input-appended"));
            Assert.Single(model.Zones);
            Assert.Single(model.PhotovoltaicPanels);
            Assert.Same(initialZone, model.Zones[0]);
            Assert.Same(initialPv, model.PhotovoltaicPanels[0]);
            IList<Zone> modelZones = Assert.IsAssignableFrom<IList<Zone>>(model.Zones);
            IList<PhotovoltaicPanel> modelPv = Assert.IsAssignableFrom<IList<PhotovoltaicPanel>>(
                model.PhotovoltaicPanels);
            Assert.Throws<NotSupportedException>(() => modelZones.Add(CreateZone("model-appended")));
            Assert.Throws<NotSupportedException>(() => modelPv.Add(CreatePanel("model-appended")));
            Assert.Equal(2, inputZones.Count);
            Assert.Equal(2, inputPv.Count);
            return new[]
            {
                "inputs=copied;later-input-mutation=isolated",
                "model-collections=read-only;model-mutation=rejected",
                "copied-elements=identity-preserved;zone_count=1;pv_count=1",
            };
        }

        if (caseId == ExpectedCases[7].CaseId)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EnergyModel(null!, Array.Empty<Zone>()));
            Assert.Throws<ArgumentException>(() =>
                new EnergyModel(" ", Array.Empty<Zone>()));
            Assert.Throws<ArgumentException>(() =>
                new EnergyModel("Invalid zone element", new Zone[] { null! }));
            Assert.Throws<ArgumentException>(() =>
                new EnergyModel(
                    "Invalid PV element",
                    Array.Empty<Zone>(),
                    photovoltaicPanels: new PhotovoltaicPanel[] { null! }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EnergyModel("NaN north", Array.Empty<Zone>(), northAxisDegrees: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EnergyModel("Infinite north", Array.Empty<Zone>(), northAxisDegrees: double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EnergyModel("Out of range north", Array.Empty<Zone>(), northAxisDegrees: 360.0001));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EnergyModel("Invalid terrain", Array.Empty<Zone>(), terrain: (Terrain)int.MaxValue));
            var valid = new EnergyModel(
                "Null optional PV",
                Array.Empty<Zone>(),
                photovoltaicPanels: null);
            Assert.Empty(valid.PhotovoltaicPanels);
            return new[]
            {
                "name=required-nonblank;zones=required-typed-nonnull-elements",
                "north_axis=finite-inclusive-minus360-to360;terrain=defined-enum",
                "pv=null-normalized-empty;pv-elements=typed-nonnull",
            };
        }

        Assert.Equal(ExpectedCases[8].CaseId, caseId);
        var first = new EnergyModel("First defaults", Array.Empty<Zone>());
        var second = new EnergyModel("Second defaults", Array.Empty<Zone>());
        Assert.NotSame(first.Zones, second.Zones);
        Assert.NotSame(first.PhotovoltaicPanels, second.PhotovoltaicPanels);
        Assert.Empty(first.Zones);
        Assert.Empty(second.Zones);
        Assert.Empty(first.PhotovoltaicPanels);
        Assert.Empty(second.PhotovoltaicPanels);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<Zone>>(first.Zones).Add(CreateZone("default-mutation")));
        Assert.Empty(second.Zones);
        ParameterInfo[] reflected = ModelConstructor().GetParameters();
        Assert.Null(reflected[4].DefaultValue);
        return new[]
        {
            "default-zone-collections=fresh-empty-read-only",
            "default-pv-collections=fresh-empty-read-only",
            "cross-instance-mutation=impossible;optional-pv-default=null",
        };
    }

    private static void AssertDefaultRawFields(IdfDocument document)
    {
        AssertFields(Assert.Single(document["SimulationControl"]), "Yes", "Yes", "Yes", "No", "Yes", "No");
        AssertFields(Assert.Single(document["Timestep"]), "6");
        AssertFields(document["SizingPeriod:WeatherFileDays"][0], "DesignWinter", "1", "1", "1", "31");
        AssertFields(document["SizingPeriod:WeatherFileDays"][1], "DesignSummer", "8", "1", "8", "31");
        AssertFields(Assert.Single(document["RunPeriod"]), "Year-Round", "1", "1", "2026", "12", "31", "2026");

        IReadOnlyList<IdfObject> limits = document["ScheduleTypeLimits"];
        Assert.Equal(4, limits.Count);
        AssertFields(limits[0], "ScheduleTypeLimits:Temperature", "-50", "200", "Continuous", "Temperature");
        AssertFields(limits[1], "ScheduleTypeLimits:Onoff", "0", "1", "Discrete", "Dimensionless");
        AssertFields(limits[2], "ScheduleTypeLimits:Fraction", "0", "1", "Continuous", "Dimensionless");
        AssertFields(limits[3], "ScheduleTypeLimits:Real", string.Empty, string.Empty, "Continuous", "Dimensionless");

        IReadOnlyList<IdfObject> compact = document["Schedule:Compact"];
        Assert.Equal(2, compact.Count);
        AssertFields(compact[0], "ALLON", string.Empty, "Through: 12/31", "For: AllDays", "Until: 24:00", "1");
        AssertFields(compact[1], "ALLOFF", string.Empty, "Through: 12/31", "For: AllDays", "Until: 24:00", "0");
        AssertFields(Assert.Single(document["Schedule:Constant"]), "$DEFAULT$PEOPLEACTIVITY", "real", "107.0");
        AssertFields(Assert.Single(document["GlobalGeometryRules"]), "UpperLeftCorner", "Counterclockwise", "World", "Relative", "Relative");
    }

    private static void AssertOutputFields(IdfDocument document)
    {
        AssertFields(
            Assert.Single(document["Output:Table:SummaryReports"]),
            "EndUseEnergyConsumptionElectricityMonthly",
            "EndUseEnergyConsumptionNaturalGasMonthly",
            "EndUseEnergyConsumptionDieselMonthly",
            "EndUseEnergyConsumptionFuelOilMonthly",
            "EndUseEnergyConsumptionCoalMonthly",
            "EndUseEnergyConsumptionPropaneMonthly",
            "EndUseEnergyConsumptionGasolineMonthly",
            "EndUseEnergyConsumptionOtherFuelsMonthly");
        AssertFields(
            Assert.Single(document["Output:Table:Monthly"]),
            "ElectricityBalanceMonthly",
            "3",
            "ElectricityProduced:Facility",
            "SumOrAverage",
            "ElectricitySurplusSold:Facility",
            "SumOrAverage",
            "ElectricityPurchased:Facility",
            "SumOrAverage");
        AssertFields(
            Assert.Single(document["OutputControl:Table:Style"]),
            "Comma",
            "JtoKWH");
    }

    private static void AssertFields(IdfObject item, params string[] expected)
    {
        Assert.Equal(expected, item.Fields.Select(field => field.Value));
    }

    private static Zone CreateZone(string label) =>
        new(
            new EntityId("ZONE-" + label),
            "Zone " + label,
            Array.Empty<Surface>(),
            TestDomainFactory.EmptyProfile("PROFILE-" + label));

    private static PhotovoltaicPanel CreatePanel(string label) =>
        new(
            new EntityId("PV-" + label),
            "PV " + label,
            areaSquareMetres: 1,
            tiltDegrees: 30,
            azimuthDegrees: 180,
            efficiency: 0.2);

    private static ConstructorInfo ModelConstructor() => Assert.Single(
        typeof(EnergyModel).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    private static MethodInfo DefaultFactory() => Assert.Single(
        typeof(EnergyModel).GetMethods(BindingFlags.Public | BindingFlags.Static),
        candidate => candidate.Name == nameof(EnergyModel.CreateDefaultIdfDocument));

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

        JsonElement[] observations = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(expectedObservations.Count, observations.Length);
        Assert.Equal(
            observations.Select(item => RequiredString(item, "case_id")),
            observations.Select(item => RequiredString(item, "case_id"))
                .OrderBy(item => item, StringComparer.Ordinal));
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

    private static void AssertPinnedRepositoryFile(string relativePath, string expectedSha256)
    {
        string path = FindRepositoryFile(relativePath);
        Assert.Equal(expectedSha256, Sha256(File.ReadAllBytes(path)));
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
