using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class EnergyModelConditioningOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-model-conditioning-oracle.json";
    private const string OracleSha256 =
        "sha256:7cbdcad0691b3e56010981217f11e515c6cb7f417b6a22643925876b33e6de81";
    private const string CasesSha256 =
        "sha256:96d15556dcde29a91582c66bc7c056c374619d8a50c7c17785ef0eeb241bdfca";
    private const int OracleByteLength = 19_851;
    private const int ExpectedCaseCount = 9;
    private const string OracleSchema =
        "dragons.python-reference.dragon-model-conditioning.v1";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.EnergyModelConditioningOracleParityTests.MatchesPinnedPythonConditioning";
    private const string EnergyModelTypeName =
        "Dragons.InvisibleDragon.Model.EnergyModel";

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/dragon/model.py", "EnergyModel.conditioned_zones", "sha256:90ceddf7de437a59950e7081185fefbf1f56354a49662431452f11ac24bc6f24", "dragon-model-conditioning-conditioned-zones-90ceddf7"),
        new("src/idragon/dragon/model.py", "EnergyModel.unconditioned_zones", "sha256:24b8c9a917df6c286d13dfb75c3ca04403b74cf0a70e6056cc933c9ed2822e08", "dragon-model-conditioning-unconditioned-zones-24b8c9a9"),
        new("src/idragon/dragon/shape.py", "Zone.is_conditioned", "sha256:6fe80cb193a6716b68c1033c5c52bd29f422ffb9efbdac8475a7f4b4ddc46370", "dragon-model-conditioning-zone-is-conditioned-6fe80cb1"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("EnergyModel.conditioned_zones", "function", "sha256:e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e", "sha256:ae71f1c62c76cfdf6890e18c83f3dd2709b9fb72627f690db7dc52b7db719348", "equivalent", null, EnergyModelTypeName + ".ConditionedZones"),
        new("EnergyModel.unconditioned_zones", "function", "sha256:e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e", "sha256:e65c4689f16398a99be21f56cf6c046ee411718b151d637a75abc7e8076249c8", "equivalent", null, EnergyModelTypeName + ".UnconditionedZones"),
        new("Zone.is_conditioned", "function", "sha256:2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb", "sha256:48a103a5bbb0b2a65f357d705eb38137269140e236bf98c2d56d7dd77474d9f3", "exception", "model-context-zone-conditioning-predicate", EnergyModelTypeName + ".ConditionedZones"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-model-conditioning.conditioned-zones.empty-selection", "energy-model-conditioned-zones", "EnergyModel.conditioned_zones"),
        new("dragon-model-conditioning.conditioned-zones.falsey-availability-order", "energy-model-conditioned-zones", "EnergyModel.conditioned_zones"),
        new("dragon-model-conditioning.conditioned-zones.mixed-order-identity", "energy-model-conditioned-zones", "EnergyModel.conditioned_zones"),
        new("dragon-model-conditioning.unconditioned-zones.empty-selection", "energy-model-unconditioned-zones", "EnergyModel.unconditioned_zones"),
        new("dragon-model-conditioning.unconditioned-zones.mixed-complement", "energy-model-unconditioned-zones", "EnergyModel.unconditioned_zones"),
        new("dragon-model-conditioning.unconditioned-zones.profile-and-custom-only", "energy-model-unconditioned-zones", "EnergyModel.unconditioned_zones"),
        new("dragon-model-conditioning.zone-is-conditioned.falsey-availability", "zone-is-conditioned", "Zone.is_conditioned"),
        new("dragon-model-conditioning.zone-is-conditioned.no-supply", "zone-is-conditioned", "Zone.is_conditioned"),
        new("dragon-model-conditioning.zone-is-conditioned.profile-availability-required", "zone-is-conditioned", "Zone.is_conditioned"),
    };

    [Fact]
    public void MatchesPinnedPythonConditioning()
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
            JsonElement pythonFacts = cases[index]
                .GetProperty("python")
                .GetProperty("facts");
            string[] nativeFacts = ExecuteCase(binding, pythonFacts);
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
            "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0",
            RequiredString(upstream, "inventory_sha256"));
        JsonElement[] sources = upstream.GetProperty("sources")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(2, sources.Length);
        AssertSource(
            sources[0],
            "src/idragon/dragon/model.py",
            "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
            "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59");
        AssertSource(
            sources[1],
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
        Assert.Equal(ExpectedSymbols.Length, ExpectedEvidence.Length);
        Assert.Equal(
            ExpectedSymbols.Select(item => item.Symbol),
            ExpectedEvidence.Select(item => item.Symbol));
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => item.AssertionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => (item.Path, item.Symbol))
                .Distinct()
                .Count());
        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            Assert.Matches("^sha256:[0-9a-f]{64}$", evidence.SymbolHash);
            Assert.Matches(
                "^dragon-model-conditioning-[a-z-]+-[0-9a-f]{8}$",
                evidence.AssertionId);
            Assert.Equal(
                evidence.SymbolHash,
                Assert.Single(
                    ExpectedSymbols,
                    item => item.Symbol == evidence.Symbol).SymbolHash);
        }
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            JsonElement actual = symbols[index];
            SymbolContract expected = ExpectedSymbols[index];
            EvidenceBinding evidence = ExpectedEvidence[index];
            AssertKeys(
                actual,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
            Assert.Equal(evidence.Path, RequiredString(actual, "path"));
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(expected.Kind, RequiredString(actual, "kind"));
            Assert.Equal(expected.SignatureHash, RequiredString(actual, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(actual, "body_hash"));
            Assert.Equal(expected.SymbolHash, RequiredString(actual, "symbol_hash"));
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
            "runtime_names",
            "state_encoding",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(
            contract.GetProperty("case_ids"),
            ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(
            contract.GetProperty("target_symbols"),
            ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal(
            "logical-label-index-and-boolean-only-no-id-or-address",
            RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "pinned-python-only-no-native-type-name-claims",
            RequiredString(contract, "runtime_names"));
        Assert.Equal(
            "logical-presence-tags-no-raw-objects",
            RequiredString(contract, "state_encoding"));

        JsonElement classifications = contract.GetProperty("classifications");
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.Classification, RequiredString(classifications, symbol.Symbol));
        }

        Assert.Equal(2, ExpectedSymbols.Count(item => item.Classification == "equivalent"));
        Assert.Single(ExpectedSymbols, item => item.Classification == "exception");
        JsonElement adaptations = contract.GetProperty("adaptations");
        AssertKeys(adaptations, "Zone.is_conditioned");
        Assert.Equal(
            "model-context-zone-conditioning-predicate",
            RequiredString(adaptations, "Zone.is_conditioned"));
        Assert.Single(ExpectedSymbols, item => item.AdaptationId is not null);

        JsonElement assertionIds = contract.GetProperty("assertion_ids");
        AssertKeys(assertionIds, ExpectedEvidence.Select(item => item.Symbol).ToArray());
        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            Assert.Equal(evidence.AssertionId, RequiredString(assertionIds, evidence.Symbol));
        }
    }

    private static void ValidateNativeBindings()
    {
        Type modelType = typeof(EnergyModel);
        Assert.Equal(EnergyModelTypeName, modelType.FullName);
        Assert.True(modelType.IsPublic);
        Assert.True(modelType.IsSealed);

        PropertyInfo conditioned = modelType.GetProperty(
            nameof(EnergyModel.ConditionedZones),
            BindingFlags.Public | BindingFlags.Instance)!;
        PropertyInfo unconditioned = modelType.GetProperty(
            nameof(EnergyModel.UnconditionedZones),
            BindingFlags.Public | BindingFlags.Instance)!;
        AssertReadOnlyZoneListProperty(conditioned);
        AssertReadOnlyZoneListProperty(unconditioned);
        Assert.Equal(
            EnergyModelTypeName + ".ConditionedZones",
            ExpectedSymbols[0].NativeTarget);
        Assert.Equal(
            EnergyModelTypeName + ".UnconditionedZones",
            ExpectedSymbols[1].NativeTarget);
        Assert.Equal(
            EnergyModelTypeName + ".ConditionedZones",
            ExpectedSymbols[2].NativeTarget);

        Assert.Null(typeof(Zone).GetProperty(
            "IsConditioned",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(Zone).GetMethod(
            "IsConditioned",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(modelType.GetMethod(
            nameof(EnergyModel.ToIdfDocument),
            BindingFlags.Public | BindingFlags.Instance));
    }

    private static void AssertReadOnlyZoneListProperty(PropertyInfo property)
    {
        Assert.Equal(typeof(IReadOnlyList<Zone>), property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.Null(property.SetMethod);
    }

    private static void ValidateCase(JsonElement value, CaseBinding expected)
    {
        bool hasAdaptation = expected.Symbol == "Zone.is_conditioned";
        AssertKeys(
            value,
            hasAdaptation
                ? new[] { "executor", "expected_dotnet", "id", "python", "symbol" }
                : new[] { "executor", "id", "python", "symbol" });
        Assert.Equal(expected.CaseId, RequiredString(value, "id"));
        Assert.Equal(expected.Executor, RequiredString(value, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(value, "symbol"));

        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        if (hasAdaptation)
        {
            JsonElement expectedDotnet = value.GetProperty("expected_dotnet");
            AssertKeys(expectedDotnet, "adaptation", "outcome");
            Assert.Equal(
                "model-context-zone-conditioning-predicate",
                RequiredString(expectedDotnet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));
        }
        else
        {
            Assert.False(value.TryGetProperty("expected_dotnet", out _));
        }
    }

    private static string[] ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Executor switch
        {
            "energy-model-conditioned-zones" => ExecuteListCase(
                pythonFacts,
                selectConditioned: true),
            "energy-model-unconditioned-zones" => ExecuteListCase(
                pythonFacts,
                selectConditioned: false),
            "zone-is-conditioned" => ExecuteZonePredicateCase(pythonFacts),
            _ => throw new Xunit.Sdk.XunitException(
                "Unknown conditioning executor '" + binding.Executor + "'."),
        };
    }

    private static string[] ExecuteListCase(
        JsonElement pythonFacts,
        bool selectConditioned)
    {
        ListFacts facts = ParseListFacts(pythonFacts, selectConditioned);
        NativeScenario scenario = CreateScenario(facts.States);
        IReadOnlyList<Zone> first = selectConditioned
            ? scenario.Model.ConditionedZones
            : scenario.Model.UnconditionedZones;
        IReadOnlyList<Zone> second = selectConditioned
            ? scenario.Model.ConditionedZones
            : scenario.Model.UnconditionedZones;
        Assert.NotSame(first, second);
        Assert.Equal(first.Count, second.Count);
        ICollection<Zone> mutableView = Assert.IsAssignableFrom<ICollection<Zone>>(first);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => mutableView.Add(scenario.Zones[0].Zone));
        for (int index = 0; index < first.Count; index++)
        {
            Assert.Same(first[index], second[index]);
        }

        int[] actualIndices = first
            .Select(zone => Array.FindIndex(
                scenario.Zones,
                candidate => ReferenceEquals(candidate.Zone, zone)))
            .ToArray();
        Assert.DoesNotContain(actualIndices, index => index < 0);
        string[] actualLabels = actualIndices
            .Select(index => scenario.Zones[index].State.Label)
            .ToArray();
        Assert.Equal(facts.SelectedIndices, actualIndices);
        Assert.Equal(facts.SelectedLabels, actualLabels);
        Assert.Equal(
            scenario.Zones.Select(item => item.Zone),
            scenario.Model.Zones);
        Assert.All(
            scenario.Zones.Select((item, index) => (item, index)),
            pair => Assert.Same(pair.item.Zone, scenario.Model.Zones[pair.index]));

        IdfDocument document = scenario.Model.ToIdfDocument();
        int conditionedCount = facts.States.Count(item => item.PythonConditioned);
        int unconditionedCount = facts.States.Length - conditionedCount;
        Assert.Equal(
            conditionedCount,
            document["ZoneHVAC:Baseboard:RadiantConvective:Electric"].Count);
        Assert.Equal(conditionedCount, document["Sizing:Zone"].Count);
        Assert.Equal(conditionedCount, document["ZoneControl:Thermostat"].Count);
        Assert.Equal(
            unconditionedCount,
            document["HVACTemplate:Zone:IdealLoadsAirSystem"].Count);
        AssertCustomScheduleGating(document, scenario.Zones);

        string selectedLabels = actualLabels.Length == 0
            ? "<none>"
            : string.Join("|", actualLabels);
        string selectedIndices = actualIndices.Length == 0
            ? "<none>"
            : string.Join("|", actualIndices);
        return new[]
        {
            "native-selected-labels=" + selectedLabels,
            "native-selected-indices=" + selectedIndices,
            "native-order-identity=true;fresh-readonly-projection=true;idf-explicit="
                + conditionedCount
                + ";idf-ideal="
                + unconditionedCount
                + ";custom-schedules-gated=true",
        };
    }

    private static string[] ExecuteZonePredicateCase(JsonElement pythonFacts)
    {
        AssertKeys(pythonFacts, "observations");
        StateBinding[] states = pythonFacts.GetProperty("observations")
            .EnumerateArray()
            .Select(ParseState)
            .ToArray();
        Assert.Equal(3, states.Length);
        NativeScenario scenario = CreateScenario(states);
        IReadOnlyList<Zone> conditionedZones = scenario.Model.ConditionedZones;
        IReadOnlyList<Zone> unconditionedZones = scenario.Model.UnconditionedZones;
        Assert.Equal(states.Length, conditionedZones.Count + unconditionedZones.Count);
        IdfDocument document = scenario.Model.ToIdfDocument();
        AssertCustomScheduleGating(document, scenario.Zones);

        var nativeFacts = new List<string>(states.Length);
        for (int index = 0; index < states.Length; index++)
        {
            StateBinding state = states[index];
            StateZone native = scenario.Zones[index];
            bool isConditioned = conditionedZones.Any(
                zone => ReferenceEquals(zone, native.Zone));
            bool isUnconditioned = unconditionedZones.Any(
                zone => ReferenceEquals(zone, native.Zone));
            Assert.Equal(state.PythonConditioned, isConditioned);
            Assert.Equal(!state.PythonConditioned, isUnconditioned);

            bool hasExplicitIdf = document["Sizing:Zone"].Any(
                item => item.Name == native.Zone.Name);
            bool hasIdealIdf = document["HVACTemplate:Zone:IdealLoadsAirSystem"].Any(
                item => item.Name == native.Zone.Name);
            Assert.Equal(state.PythonConditioned, hasExplicitIdf);
            Assert.Equal(!state.PythonConditioned, hasIdealIdf);
            nativeFacts.Add(
                "native-context-label="
                + state.Label
                + ";profile-schedule-present="
                + Lower(state.ProfilePresent)
                + ";profile-schedule-value="
                + (state.ProfilePresent
                    ? state.NativeScheduleValue.ToString("R", CultureInfo.InvariantCulture)
                    : "none")
                + ";assignment-present="
                + Lower(state.SupplyPresent)
                + ";custom-schedule-present="
                + Lower(state.CustomSupplyAvailabilityPresent)
                + ";conditioned="
                + Lower(isConditioned)
                + ";idf-mode="
                + (hasExplicitIdf ? "explicit" : "ideal"));
        }

        return nativeFacts.ToArray();
    }

    private static ListFacts ParseListFacts(
        JsonElement facts,
        bool selectConditioned)
    {
        AssertKeys(
            facts,
            "fresh_list_each_access",
            "input_labels",
            "input_states",
            "result_type",
            "selected_indices",
            "selected_labels",
            "selected_objects_are_input_objects",
            "source_list_unchanged");
        Assert.True(facts.GetProperty("fresh_list_each_access").GetBoolean());
        Assert.Equal("list", RequiredString(facts, "result_type"));
        Assert.True(facts.GetProperty("selected_objects_are_input_objects").GetBoolean());
        Assert.True(facts.GetProperty("source_list_unchanged").GetBoolean());

        string[] inputLabels = ReadStringArray(facts.GetProperty("input_labels"));
        StateBinding[] states = facts.GetProperty("input_states")
            .EnumerateArray()
            .Select(ParseState)
            .ToArray();
        Assert.Equal(inputLabels, states.Select(item => item.Label));
        int[] expectedIndices = states
            .Select((state, index) => (state, index))
            .Where(item => item.state.PythonConditioned == selectConditioned)
            .Select(item => item.index)
            .ToArray();
        string[] expectedLabels = expectedIndices
            .Select(index => states[index].Label)
            .ToArray();
        int[] selectedIndices = ReadIntArray(facts.GetProperty("selected_indices"));
        string[] selectedLabels = ReadStringArray(facts.GetProperty("selected_labels"));
        Assert.Equal(expectedIndices, selectedIndices);
        Assert.Equal(expectedLabels, selectedLabels);
        return new ListFacts(states, selectedIndices, selectedLabels);
    }

    private static StateBinding ParseState(JsonElement state)
    {
        AssertKeys(
            state,
            "custom_supply_availability_present",
            "label",
            "profile_availability",
            "supply_present",
            "zone_is_conditioned");
        string label = RequiredString(state, "label");
        Assert.Matches("^[a-z][a-z-]*$", label);
        bool customPresent = state
            .GetProperty("custom_supply_availability_present")
            .GetBoolean();
        bool supplyPresent = state.GetProperty("supply_present").GetBoolean();
        bool pythonConditioned = state
            .GetProperty("zone_is_conditioned")
            .GetBoolean();
        if (customPresent)
        {
            Assert.True(supplyPresent);
        }

        JsonElement availability = state.GetProperty("profile_availability");
        string kind = RequiredString(availability, "kind");
        bool profilePresent;
        double nativeScheduleValue;
        switch (kind)
        {
            case "none":
                AssertKeys(availability, "kind");
                profilePresent = false;
                nativeScheduleValue = 0;
                break;
            case "token":
                AssertKeys(availability, "kind", "value");
                Assert.Equal("ALLON", RequiredString(availability, "value"));
                profilePresent = true;
                nativeScheduleValue = 1;
                break;
            case "int":
                AssertKeys(availability, "decimal", "kind");
                Assert.Equal("0", RequiredString(availability, "decimal"));
                profilePresent = true;
                nativeScheduleValue = 0;
                break;
            case "bool":
                AssertKeys(availability, "kind", "value");
                Assert.False(availability.GetProperty("value").GetBoolean());
                profilePresent = true;
                nativeScheduleValue = 0;
                break;
            case "string":
                AssertKeys(availability, "kind", "value");
                Assert.Equal(string.Empty, RequiredString(availability, "value"));
                profilePresent = true;
                nativeScheduleValue = 0;
                break;
            default:
                throw new Xunit.Sdk.XunitException(
                    "Unsupported pinned profile availability kind '" + kind + "'.");
        }

        Assert.Equal(supplyPresent && profilePresent, pythonConditioned);
        return new StateBinding(
            label,
            profilePresent,
            nativeScheduleValue,
            supplyPresent,
            customPresent,
            pythonConditioned);
    }

    private static NativeScenario CreateScenario(IReadOnlyList<StateBinding> states)
    {
        var zones = new List<StateZone>(states.Count);
        var assignments = new List<ZoneHvacAssignment>(states.Count);
        for (int index = 0; index < states.Count; index++)
        {
            StateBinding state = states[index];
            Schedule heating = Schedule.Constant(
                $"Oracle heating {index}",
                20,
                ScheduleType.Temperature);
            Schedule cooling = Schedule.Constant(
                $"Oracle cooling {index}",
                26,
                ScheduleType.Temperature);
            Schedule? profileAvailability = state.ProfilePresent
                ? Schedule.Constant(
                    $"Oracle profile availability {index}",
                    state.NativeScheduleValue,
                    ScheduleType.OnOff)
                : null;
            var profile = new ZoneProfile(
                new EntityId($"ORACLE-PROFILE-{index}"),
                $"Oracle profile {index}",
                heating,
                cooling,
                profileAvailability);
            Surface floor = TestDomainFactory.Surface(
                $"ORACLE-SURFACE-{index}",
                $"Oracle floor {index}",
                TestDomainFactory.Square(x: index * 2d),
                SurfaceType.Floor,
                SurfaceBoundary.Ground);
            var zone = new Zone(
                new EntityId($"ORACLE-ZONE-{index}"),
                $"Oracle zone {index} {state.Label}",
                new[] { floor },
                profile);

            string? customScheduleName = null;
            if (state.SupplyPresent)
            {
                var radiator = new ElectricRadiator(
                    new EntityId($"ORACLE-SUPPLY-{index}"),
                    $"Oracle radiator {index}");
                Schedule? customAvailability = state.CustomSupplyAvailabilityPresent
                    ? Schedule.Constant(
                        $"Oracle custom availability {index}",
                        1,
                        ScheduleType.OnOff)
                    : null;
                customScheduleName = customAvailability?.Name;
                assignments.Add(new ZoneHvacAssignment(
                    zone.Id,
                    new SupplyGroup(
                        new[] { radiator },
                        new Schedule?[] { customAvailability })));
            }

            zones.Add(new StateZone(
                state,
                zone,
                profileAvailability?.Name,
                customScheduleName));
        }

        var model = new EnergyModel(
            "Pinned conditioning native scenario",
            zones.Select(item => item.Zone),
            assignments);
        Assert.True(model.Validate().IsValid);
        return new NativeScenario(model, zones.ToArray());
    }

    private static void AssertCustomScheduleGating(
        IdfDocument document,
        IReadOnlyList<StateZone> states)
    {
        foreach (StateZone state in states)
        {
            if (state.ProfileScheduleName is not null)
            {
                Assert.Contains(
                    document["Schedule:Compact"],
                    item => item.Name == state.ProfileScheduleName);
            }

            if (state.CustomScheduleName is null)
            {
                continue;
            }

            bool emitted = document["Schedule:Compact"].Any(
                item => item.Name == state.CustomScheduleName);
            Assert.Equal(state.State.PythonConditioned, emitted);
        }
    }

    private static int[] ReadIntArray(JsonElement value)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
    }

    private static string[] ReadStringArray(JsonElement value)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static string Lower(bool value) => value ? "true" : "false";

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
                Assert.Equal(
                    JsonValueKind.Null,
                    observation.GetProperty("adaptation_id").ValueKind);
            }
            else
            {
                Assert.Equal(
                    expected.AdaptationId,
                    RequiredString(observation, "adaptation_id"));
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

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private static string CanonicalSha256(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false,
            }))
        {
            WriteCanonicalJson(writer, value);
        }

        return Sha256(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
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
                throw new Xunit.Sdk.XunitException(
                    "Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            string[] names = value.EnumerateObject()
                .Select(property => property.Name)
                .ToArray();
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
                    "classification" or
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
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            Assert.False(Regex.IsMatch(
                text,
                @"^(?:[A-Za-z]:[\\/]|[\\/]{2}|/)",
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
        string NativeTarget)
    {
        public string SymbolHash => Assert.Single(
            ExpectedEvidence,
            evidence => evidence.Symbol == Symbol).SymbolHash;
    }

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string? AdaptationId,
        IReadOnlyList<string> NativeFacts);

    private sealed record StateBinding(
        string Label,
        bool ProfilePresent,
        double NativeScheduleValue,
        bool SupplyPresent,
        bool CustomSupplyAvailabilityPresent,
        bool PythonConditioned);

    private sealed record ListFacts(
        StateBinding[] States,
        int[] SelectedIndices,
        string[] SelectedLabels);

    private sealed record StateZone(
        StateBinding State,
        Zone Zone,
        string? ProfileScheduleName,
        string? CustomScheduleName);

    private sealed record NativeScenario(EnergyModel Model, StateZone[] Zones);
}
