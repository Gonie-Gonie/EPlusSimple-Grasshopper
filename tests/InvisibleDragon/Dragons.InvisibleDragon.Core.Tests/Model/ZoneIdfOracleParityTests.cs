using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;
using OpaqueConstruction = Dragons.InvisibleDragon.Construction.Construction;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class ZoneIdfOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-shape-zone-to-idf-object-oracle.json";
    private const int OracleByteLength = 219_575;
    private const string OracleSha256 =
        "sha256:7c0c3f10d8e3a83b52a6ddfde0512e4913e2fc950a0224ba9256f0e94ac19a67";
    private const string OracleSchema =
        "dragons.python-reference.dragon-shape-zone-to-idf-object.v1";
    private const string CasesSha256 =
        "sha256:21f896de5f0685d45bd7c0f29a777488dce65e05a79d90736ed193f1a8db493a";

    private const string GeneratorRepositoryPath =
        "tools/python-reference/generate_dragon_shape_zone_to_idf_object_oracle.py";
    private const int GeneratorByteLength = 67_640;
    private const string GeneratorSha256 =
        "sha256:41d0de6eee371576d19ed5744b7316ee1dfcf89410c050c1205a2f0c4f9a13fb";
    private const string PythonValidatorRepositoryPath =
        "tests/PythonReference/test_dragon_shape_zone_to_idf_object_oracle.py";
    private const int PythonValidatorByteLength = 20_769;
    private const string PythonValidatorSha256 =
        "sha256:a3d8c40bf50bf0e85f9b7f14beb6c39e6df09299a80201cd5ab414b1574e093f";

    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamPath = "src/idragon/dragon/shape.py";
    private const string UpstreamSourceSha256 =
        "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c";
    private const string UpstreamAstSha256 =
        "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2";

    private const string PublicRepositoryPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs";
    private const int PublicByteLength = 21_985;
    private const string PublicSha256 =
        "sha256:60357af37bea1f6e7dd0640254a30761ed4097d53751183e5902c2efa62a0f28";
    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs";
    private const int ImplementationByteLength = 50_723;
    private const string ImplementationSha256 =
        "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4";
    private const string ZoneRepositoryPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Zone.cs";
    private const int ZoneByteLength = 6_686;
    private const string ZoneSha256 =
        "sha256:17423d03e67e5d19ee681f138291bb011a81b84e42b4a188825d570854235ffa";

    private const string PublicSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument";
    private const string AppendGeometrySymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendConstructionsAndGeometry";
    private const string AppendLoadsSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneLoads";
    private const string AppendHvacSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendHvac";
    private const string AppendSizingSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendSizing";
    private const string AppendThermostatSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendThermostat";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.ZoneIdfOracleParityTests.MatchesPinnedPythonZoneEmissionThroughFreshEnergyModels";

    private const string IddOracleRepositoryPath =
        "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz";
    private const int IddOracleByteLength = 585_481;
    private const string IddOracleSha256 =
        "sha256:75f9d6c2efa32349704489aae4622b8647ac07f542e61cf3130624786436fa26";
    private const string IddOracleSchema = "dragons.energyplus-idd-schema.v1";
    private const string EnergyPlusVersion = "24.2.0";
    private const string EnergyPlusBuild = "94a887817b";
    private const string EnergyPlusIddSourceSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const int EnergyPlusIddSourceByteLength = 4_556_412;

    private const string FixtureScope =
        "bounded-common-valid-state-zone-emission-and-parent-orchestration";
    private const string ClassificationBasis =
        "native conversion is available only inside EnergyModel parent assembly; compact fields, actual equipment population, parent stage ordering, and real geometry replace the standalone Python mutable-list and trace-double representation";

    private static readonly string[] SelectedObjectTypes =
    {
        "DesignSpecification:OutdoorAir",
        "DesignSpecification:ZoneAirDistribution",
        "Sizing:Zone",
        "ZoneHVAC:EquipmentList",
        "ZoneHVAC:EquipmentConnections",
        "Schedule:Constant",
        "ThermostatSetpoint:DualSetpoint",
        "ZoneControl:Thermostat",
        "Schedule:Compact",
        "People",
        "ZoneVentilation:DesignFlowRate",
        "Lights",
        "ElectricEquipment",
        "ZoneInfiltration:DesignFlowRate",
        "Zone",
    };

    private static readonly SymbolBinding[] ExpectedSymbols =
    {
        new(
            1092,
            "Zone.to_idf_hvac_default_object",
            "sha256:ff678ec281fe0726c46fd2145ebfb7fe22b56c5772bf1423d83c4877c0287cd9",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:9a121aaad9df4bfa6222f747985a1b07749f518b3501154743ef5c32d307940b",
            "dragon-shape-zone-to-idf-hvac-default-object-ff678ec2",
            "model-context-zone-hvac-default-idf-emission",
            new[] { AppendHvacSymbol, AppendThermostatSymbol, AppendSizingSymbol }),
        new(
            1093,
            "Zone.to_idf_load_object",
            "sha256:d19165f0aa97a1768174def3da3a46c9c11f29567c558ae844d4cac546452f99",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:17d9c0579f4763783672c981efb7fa0d7c979af8ebfe008b70499f81273e5a78",
            "dragon-shape-zone-to-idf-load-object-d19165f0",
            "model-context-zone-load-idf-emission",
            new[] { AppendLoadsSymbol }),
        new(
            1094,
            "Zone.to_idf_object",
            "sha256:479f4d74a625e35e97559f208b41c4bde2f00a519b8e6b840718d78fdfd2e096",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:1964153231690634955bd8ae5c39468cd1ecab4f5c2acbff9ded2cb37978369a",
            "dragon-shape-zone-to-idf-object-479f4d74",
            "model-context-zone-idf-emission",
            new[] { AppendGeometrySymbol, AppendLoadsSymbol, AppendHvacSymbol }),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new(
            "dragon-shape-zone-to-idf-object.hvac-default.conditioned",
            "Zone.to_idf_hvac_default_object",
            "native-parent-orchestration-order-differs",
            new[] { 7, 1, 37, 8, 6, 3, 3, 5 },
            24,
            120,
            6,
            1),
        new(
            "dragon-shape-zone-to-idf-object.hvac-default.unconditioned-no-availability",
            "Zone.to_idf_hvac_default_object",
            "both-empty-target-slice",
            Array.Empty<int>(),
            0,
            0,
            0,
            0),
        new(
            "dragon-shape-zone-to-idf-object.hvac-default.unconditioned-no-supply",
            "Zone.to_idf_hvac_default_object",
            "both-empty-target-slice",
            Array.Empty<int>(),
            0,
            0,
            0,
            0),
        new(
            "dragon-shape-zone-to-idf-object.load.empty",
            "Zone.to_idf_load_object",
            "both-empty-target-slice",
            Array.Empty<int>(),
            0,
            0,
            0,
            0),
        new(
            "dragon-shape-zone-to-idf-object.load.erv-occupant",
            "Zone.to_idf_load_object",
            "same-relative-order",
            new[] { 12, 10, 11 },
            18,
            165,
            0,
            0),
        new(
            "dragon-shape-zone-to-idf-object.load.full-natural",
            "Zone.to_idf_load_object",
            "same-relative-order",
            new[] { 6, 12, 6, 12, 10, 8, 7 },
            37,
            315,
            0,
            0),
        new(
            "dragon-shape-zone-to-idf-object.parent.empty",
            "Zone.to_idf_object",
            "python-trace-double-boundary-native-real-geometry",
            new[] { 12 },
            9,
            0,
            0,
            0),
        new(
            "dragon-shape-zone-to-idf-object.parent.multiple-surfaces",
            "Zone.to_idf_object",
            "python-trace-double-boundary-native-real-geometry",
            new[] { 12 },
            9,
            0,
            0,
            0),
        new(
            "dragon-shape-zone-to-idf-object.parent.output-and-call-order",
            "Zone.to_idf_object",
            "python-trace-double-boundary-native-real-geometry",
            new[] { 12 },
            9,
            0,
            0,
            0),
    };

    private static readonly string[] ContextOnlyNotTargeted =
    {
        "Zone",
        "Zone.__init__",
        "Zone.supply",
        "Zone.is_conditioned",
        "Zone.floor_surface",
        "Zone.floor_area",
        "Zone.idf_equipmentlistname",
        "Zone.idf_airinletnodelistname",
        "Zone.idf_airexhaustnodelistname",
        "Profile",
        "Profile.__init__",
        "Schedule",
        "Schedule.normalize_by_max",
        "Schedule.to_idf_object",
        "Surface",
        "Surface.to_idf_object",
        "Window",
        "Door",
        "Shading",
        "Blind",
        "Shade",
        "IdfObject",
        "IdfObject.__init__",
    };

    private static readonly string[] UnresolvedBehavior =
    {
        "Zone-class-constructor-and-properties",
        "Surface-class-and-Surface.to_idf_object",
        "Window-door-and-shading-emission",
        "Profile-and-Schedule-child-converter-closure",
        "invalid-duck-types-and-exact-error-behavior",
        "IdfObject-class-constructor-validation-and-mutation",
        "native-global-order-deduplication-and-conflict-policy",
        "EnergyModel-parent-assembly",
    };

    [Fact]
    public void MatchesPinnedPythonZoneEmissionThroughFreshEnergyModels()
    {
        OfficialIddOracle iddOracle = LoadOfficialIddOracle();
        using JsonDocument oracle = ReadPinnedOracle();
        Scenario[] scenarios = Enumerable.Range(0, ExpectedCases.Length)
            .Select(CreateScenario)
            .ToArray();

        JsonElement[] cases = ValidateCorpus(oracle.RootElement, scenarios);
        ValidateArtifactsAndNativeBindings();
        AssertIndependentScenarioGraphs(scenarios);

        NativeObservation[] observations = cases
            .Select((item, index) => ExecuteNativeCase(
                ExpectedCases[index],
                item.GetProperty("python").GetProperty("facts"),
                scenarios[index],
                iddOracle))
            .ToArray();

        Assert.Equal(ExpectedCases.Length, observations.Length);
        Assert.Equal(6, observations[0].ContextEnrichments.Length);
        Assert.Single(observations[0].FieldRelocations);
        Assert.All(observations.Skip(1), item => Assert.Empty(item.ContextEnrichments));
        Assert.All(observations.Skip(1), item => Assert.Empty(item.FieldRelocations));
        Assert.All(
            observations.Where(item => item.PythonObjectCount > 0),
            item => Assert.True(
                item.OmittedBlankOrNoneCount + item.OmittedOfficialIddDefaults.Length > 0));

        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            NativeObservation[] symbolObservations = observations
                .Where(item => item.Symbol == symbol.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(3, symbolObservations.Length);
            object receipt = CreateReceipt(symbol, symbolObservations);
            ValidateReceipt(receipt, symbol, symbolObservations);
            TrustedEvidenceRecorder.Record(
                symbol.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(OracleRepositoryPath));
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        string text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
        return JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
    }

    private static JsonElement[] ValidateCorpus(
        JsonElement root,
        IReadOnlyList<Scenario> scenarios)
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
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));

        JsonElement casesElement = root.GetProperty("cases");
        Assert.Equal(CasesSha256, CanonicalSha256(casesElement));
        JsonElement[] cases = casesElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedCases.Length, cases.Length);
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId),
            cases.Select(item => RequiredString(item, "id")));

        ValidateConsumerContract(root.GetProperty("consumer_contract"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateUpstream(root.GetProperty("upstream"), root.GetProperty("symbols"));

        for (int index = 0; index < cases.Length; index++)
        {
            JsonElement item = cases[index];
            CaseBinding expected = ExpectedCases[index];
            AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
            Assert.Equal("zone-to-idf-object", RequiredString(item, "executor"));
            Assert.Equal(expected.CaseId, RequiredString(item, "id"));
            Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));

            SymbolBinding symbol = ExpectedSymbols.Single(value => value.Symbol == expected.Symbol);
            JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
            AssertKeys(expectedDotnet, "adaptation", "outcome");
            Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotnet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));

            JsonElement python = item.GetProperty("python");
            AssertKeys(python, "facts", "outcome");
            Assert.Equal("returned", RequiredString(python, "outcome"));
            ValidatePythonFacts(expected, python.GetProperty("facts"), scenarios[index]);
        }

        return cases;
    }

    private static void ValidateConsumerContract(JsonElement value)
    {
        AssertKeys(
            value,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classifications",
            "closure",
            "identity_encoding",
            "native_targets",
            "runtime_signatures",
            "source_import_policy",
            "target_symbols");
        Assert.Equal(ExpectedCases.Length, value.GetProperty("case_count").GetInt32());
        AssertStringArray(value.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId));
        AssertStringArray(value.GetProperty("target_symbols"), ExpectedSymbols.Select(item => item.Symbol));
        Assert.Equal("booleans-only-no-id-or-address", RequiredString(value, "identity_encoding"));
        Assert.Equal(
            "external-temporary-copy-with-complete-loaded-local-module-audit",
            RequiredString(value, "source_import_policy"));

        JsonElement adaptations = value.GetProperty("adaptations");
        JsonElement assertions = value.GetProperty("assertion_ids");
        JsonElement classifications = value.GetProperty("classifications");
        JsonElement nativeTargets = value.GetProperty("native_targets");
        JsonElement signatures = value.GetProperty("runtime_signatures");
        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
            Assert.Equal(symbol.AssertionId, RequiredString(assertions, symbol.Symbol));
            Assert.Equal("exception", RequiredString(classifications, symbol.Symbol));
            Assert.Equal("EnergyModel.ToIdfDocument", RequiredString(nativeTargets, symbol.Symbol));
            Assert.Equal("(self) -> 'list[IdfObject]'", RequiredString(signatures, symbol.Symbol));
        }

        JsonElement closure = value.GetProperty("closure");
        AssertKeys(
            closure,
            "context_only_not_targeted",
            "dependency_only_not_closed",
            "full_symbol_closure",
            "scope",
            "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(FixtureScope, RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("context_only_not_targeted"), ContextOnlyNotTargeted);
        AssertStringArray(closure.GetProperty("unresolved_behavior"), UnresolvedBehavior);

        JsonElement dependencies = closure.GetProperty("dependency_only_not_closed");
        AssertStringArray(
            dependencies.GetProperty("Zone.to_idf_hvac_default_object"),
            new[]
            {
                "Profile-setpoint-and-availability-members",
                "IdfObject-default-field-expansion",
                "Zone.is_conditioned",
            });
        AssertStringArray(
            dependencies.GetProperty("Zone.to_idf_load_object"),
            new[]
            {
                "Profile-load-schedule-members",
                "Schedule.normalize_by_max",
                "Schedule.to_idf_object",
                "IdfObject-default-field-expansion",
            });
        AssertStringArray(
            dependencies.GetProperty("Zone.to_idf_object"),
            new[]
            {
                "Zone.floor_area",
                "Surface.to_idf_object-trace-double-only",
                "Zone.to_idf_hvac_default_object-trace-double-only",
                "Zone.to_idf_load_object-trace-double-only",
            });
    }

    private static void ValidateRuntime(JsonElement value)
    {
        Assert.Equal("cpython", RequiredString(value, "implementation"));
        Assert.Equal("3.12.7", RequiredString(value, "python_version"));
        Assert.True(value.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal(0, value.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal("siphash13", RequiredString(value, "python_hash_algorithm"));
        Assert.Equal(64, value.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal(10, value.GetProperty("dependencies").EnumerateObject().Count());
    }

    private static void ValidateUpstream(JsonElement upstream, JsonElement symbols)
    {
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventorySha256, RequiredString(upstream, "inventory_sha256"));
        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        JsonElement[] loaded = upstream.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(12, sources.Length);
        Assert.Equal(12, loaded.Length);
        Assert.Equal(
            sources.Select(item => RequiredString(item, "path")),
            loaded.Select(item => RequiredString(item, "path")));

        JsonElement shapeSource = Assert.Single(
            sources,
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal(UpstreamSourceSha256, RequiredString(shapeSource, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(shapeSource, "ast_sha256"));
        JsonElement shapeModule = Assert.Single(
            loaded,
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("idragon.dragon.shape", RequiredString(shapeModule, "module"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(shapeModule, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(shapeModule, "ast_sha256"));

        JsonElement[] actualSymbols = symbols.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, actualSymbols.Length);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            SymbolBinding expected = ExpectedSymbols[index];
            JsonElement actual = actualSymbols[index];
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(UpstreamPath, RequiredString(actual, "path"));
            Assert.Equal("function", RequiredString(actual, "kind"));
            Assert.Equal(expected.SymbolHash, RequiredString(actual, "symbol_hash"));
            Assert.Equal(expected.SignatureHash, RequiredString(actual, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(actual, "body_hash"));
        }
    }

    private static void ValidatePythonFacts(
        CaseBinding binding,
        JsonElement facts,
        Scenario scenario)
    {
        bool parent = binding.Symbol == "Zone.to_idf_object";
        string[] expectedKeys = parent
            ? new[]
            {
                "child_call_trace_first",
                "child_call_trace_second",
                "dependency_isolation",
                "emission",
                "input_context",
                "input_integrity",
                "invocation",
            }
            : new[]
            {
                "dependency_mode",
                "emission",
                "input_context",
                "input_integrity",
                "invocation",
            };
        AssertKeys(facts, expectedKeys);
        if (!parent)
        {
            Assert.Equal(
                "real-pinned-profile-schedule-and-idfobject",
                RequiredString(facts, "dependency_mode"));
        }

        JsonElement invocation = facts.GetProperty("invocation");
        Assert.Empty(invocation.GetProperty("args").EnumerateArray());
        Assert.Empty(invocation.GetProperty("kwargs").EnumerateObject());

        JsonElement integrity = facts.GetProperty("input_integrity");
        Assert.Equal(7, integrity.EnumerateObject().Count());
        Assert.All(integrity.EnumerateObject(), item => Assert.True(item.Value.GetBoolean()));

        JsonElement emission = facts.GetProperty("emission");
        JsonElement first = emission.GetProperty("first_output");
        JsonElement second = emission.GetProperty("second_output_equal");
        Assert.Equal("list", RequiredString(emission, "result_type"));
        Assert.True(emission.GetProperty("fresh_result_list").GetBoolean());
        Assert.True(emission.GetProperty("all_output_items_fresh").GetBoolean());
        Assert.True(second.GetBoolean());
        Assert.True(emission.GetProperty("same_idd_definition_for_idf_objects").GetBoolean());
        Assert.Equal(first.GetArrayLength(), emission.GetProperty("object_count").GetInt32());
        AssertFixtureFamilyOrder(emission.GetProperty("object_family_order"), first);

        ValidateFixtureInput(facts.GetProperty("input_context"), scenario);
        if (parent)
        {
            JsonElement isolation = facts.GetProperty("dependency_isolation");
            Assert.Equal(
                "instrumented-instance-method-double",
                RequiredString(isolation, "hvac_default_converter"));
            Assert.Equal(
                "instrumented-instance-method-double",
                RequiredString(isolation, "load_converter"));
            Assert.Equal(
                "instrumented-surface-trace-double",
                RequiredString(isolation, "surface_converter"));
            Assert.Equal(
                facts.GetProperty("child_call_trace_first").GetRawText(),
                facts.GetProperty("child_call_trace_second").GetRawText());
        }
    }

    private static void AssertFixtureFamilyOrder(JsonElement order, JsonElement output)
    {
        string[] expected = output.EnumerateArray().Select(item =>
        {
            string kind = RequiredString(item, "kind");
            return kind == "idf-object"
                ? RequiredString(item, "object_type")
                : "trace:" + RequiredString(item, "label");
        }).ToArray();
        if (expected.Length == 0)
        {
            Assert.Equal(JsonValueKind.Array, order.ValueKind);
            Assert.Empty(order.EnumerateArray());
        }
        else if (expected.Length == 1)
        {
            if (order.ValueKind == JsonValueKind.Array)
            {
                AssertStringArray(order, expected);
            }
            else
            {
                Assert.Equal(expected[0], order.GetString());
            }
        }
        else
        {
            AssertStringArray(order, expected);
        }
    }

    private static void ValidateFixtureInput(JsonElement input, Scenario scenario)
    {
        Zone zone = scenario.Zone;
        Assert.Equal(zone.Name, RequiredString(input, "name"));
        AssertEncodedDouble(input.GetProperty("infiltration"), zone.InfiltrationAirChangesPerHour);
        AssertEncodedDouble(
            input.GetProperty("light_density"),
            zone.LightingPowerDensityWattsPerSquareMetre);

        JsonElement[] surfaces = input.GetProperty("surfaces").EnumerateArray().ToArray();
        Assert.Equal(zone.Surfaces.Count, surfaces.Length);
        for (int index = 0; index < surfaces.Length; index++)
        {
            JsonElement expected = surfaces[index];
            Surface actual = zone.Surfaces[index];
            Assert.Equal("instrumented-surface-dependency", RequiredString(expected, "kind"));
            Assert.Equal(actual.Name, RequiredString(expected, "label"));
            Assert.Equal(actual.Type.ToString().ToLowerInvariant(), RequiredString(expected, "surface_type"));
            AssertEncodedDouble(expected.GetProperty("area"), actual.GrossArea);
        }

        JsonElement profile = input.GetProperty("profile");
        Assert.Equal(zone.Profile.Name, RequiredString(profile, "name"));
        JsonElement schedules = profile.GetProperty("schedules");
        ValidateFixtureSchedule(schedules.GetProperty("heating_setpoint"), zone.Profile.HeatingSetpoint);
        ValidateFixtureSchedule(schedules.GetProperty("cooling_setpoint"), zone.Profile.CoolingSetpoint);
        ValidateFixtureSchedule(schedules.GetProperty("hvac_availability"), zone.Profile.HvacAvailability);
        ValidateFixtureSchedule(schedules.GetProperty("occupant"), zone.Profile.Occupant);
        ValidateFixtureSchedule(schedules.GetProperty("lighting"), zone.Profile.Lighting);
        ValidateFixtureSchedule(schedules.GetProperty("equipment"), zone.Profile.Equipment);
        ValidateFixtureSchedule(schedules.GetProperty("hotwater"), zone.Profile.HotWater);

        JsonElement supply = input.GetProperty("supply");
        if (scenario.HvacAssignment is null)
        {
            Assert.Equal("none", RequiredString(supply, "kind"));
        }
        else
        {
            SupplyGroup group = scenario.HvacAssignment.Supply;
            Assert.Equal("pinned-supply-group", RequiredString(supply, "kind"));
            JsonElement[] systems = supply.GetProperty("systems").EnumerateArray().ToArray();
            Assert.Equal(group.Systems.Count, systems.Length);
            for (int index = 0; index < systems.Length; index++)
            {
                Assert.Equal(group.Systems[index].Name, RequiredString(systems[index], "name"));
                Assert.Equal(group.Systems[index].GetType().Name, RequiredString(systems[index], "type"));
            }

            JsonElement[] availabilities = supply.GetProperty("availabilities").EnumerateArray().ToArray();
            Assert.Equal(group.Availabilities.Count, availabilities.Length);
            for (int index = 0; index < availabilities.Length; index++)
            {
                ValidateFixtureSchedule(availabilities[index], group.Availabilities[index]);
            }
        }

        JsonElement ventilation = input.GetProperty("ventilation");
        if (scenario.VentilationAssignment is null)
        {
            Assert.Equal("none", RequiredString(ventilation, "kind"));
        }
        else
        {
            EnergyRecoveryVentilator actual = scenario.VentilationAssignment.Ventilator;
            Assert.Equal(
                "pinned-energy-recovery-ventilator",
                RequiredString(ventilation, "kind"));
            Assert.Equal(actual.Name, RequiredString(ventilation, "name"));
            AssertEncodedDouble(
                ventilation.GetProperty("cooling_efficiency"),
                actual.SensibleEffectiveness);
            AssertEncodedDouble(
                ventilation.GetProperty("heating_efficiency"),
                actual.LatentEffectiveness);
        }
    }

    private static void ValidateFixtureSchedule(JsonElement expected, Schedule? actual)
    {
        string kind = RequiredString(expected, "kind");
        if (actual is null)
        {
            Assert.Equal("none", kind);
            return;
        }

        Assert.Equal("pinned-schedule", kind);
        Assert.Equal(actual.Name, RequiredString(expected, "name"));
        Assert.Equal(365, expected.GetProperty("day_count").GetInt32());
        Assert.Equal(
            actual.Type.ToString().ToLowerInvariant(),
            RequiredString(expected, "schedule_type"));
        AssertEncodedDouble(expected.GetProperty("minimum"), actual.Minimum);
        AssertEncodedDouble(expected.GetProperty("maximum"), actual.Maximum);
    }

    private static void ValidateArtifactsAndNativeBindings()
    {
        AssertPinnedArtifact(GeneratorRepositoryPath, GeneratorByteLength, GeneratorSha256);
        AssertPinnedArtifact(
            PythonValidatorRepositoryPath,
            PythonValidatorByteLength,
            PythonValidatorSha256);
        AssertPinnedArtifact(PublicRepositoryPath, PublicByteLength, PublicSha256);
        AssertPinnedArtifact(
            ImplementationRepositoryPath,
            ImplementationByteLength,
            ImplementationSha256);
        AssertPinnedArtifact(ZoneRepositoryPath, ZoneByteLength, ZoneSha256);

        Type zoneType = typeof(Zone);
        Assert.True(zoneType.IsPublic);
        Assert.True(zoneType.IsSealed);
        Assert.Equal("Dragons.InvisibleDragon.Shape.Zone", zoneType.FullName);
        Assert.Equal(
            new[]
            {
                "FloorArea",
                "FloorSurfaces",
                "Id",
                "InfiltrationAirChangesPerHour",
                "LightingPowerDensityWattsPerSquareMetre",
                "Name",
                "OutdoorAirFlowCubicMetresPerSecond",
                "Profile",
                "Surfaces",
            },
            zoneType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(item => item.Name)
                .OrderBy(item => item, StringComparer.Ordinal));

        MethodInfo publicMethod = Assert.Single(
            typeof(EnergyModel).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            candidate => candidate.Name == nameof(EnergyModel.ToIdfDocument));
        Assert.Equal(PublicSymbol, MethodSymbol(publicMethod));
        Assert.Equal(typeof(IdfDocument), publicMethod.ReturnType);
        Assert.Equal(
            new[] { "schema", "options" },
            publicMethod.GetParameters().Select(item => item.Name));
        Assert.All(publicMethod.GetParameters(), item => Assert.True(item.HasDefaultValue));

        Type assembler = typeof(EnergyModel).Assembly.GetType(
            "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler",
            throwOnError: true)!;
        AssertPrivateVoidMethod(
            assembler,
            "AppendConstructionsAndGeometry",
            AppendGeometrySymbol,
            "document",
            "context",
            "model",
            "options");
        AssertPrivateVoidMethod(
            assembler,
            "AppendZoneLoads",
            AppendLoadsSymbol,
            "document",
            "context",
            "zone",
            "schedules",
            "legacyVentilator",
            "legacySimpleDragonSchedules");
        AssertPrivateVoidMethod(
            assembler,
            "AppendHvac",
            AppendHvacSymbol,
            "document",
            "context",
            "model",
            "options",
            "legacyVentilators");
        AssertPrivateVoidMethod(
            assembler,
            "AppendSizing",
            AppendSizingSymbol,
            "document",
            "context",
            "zone");
        AssertPrivateVoidMethod(
            assembler,
            "AppendThermostat",
            AppendThermostatSymbol,
            "document",
            "context",
            "zone",
            "supply",
            "options");
    }

    private static void AssertPrivateVoidMethod(
        Type declaringType,
        string name,
        string expectedSymbol,
        params string[] parameterNames)
    {
        MethodInfo method = Assert.Single(
            declaringType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            candidate => candidate.Name == name);
        Assert.Equal(expectedSymbol, MethodSymbol(method));
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Equal(parameterNames, method.GetParameters().Select(item => item.Name));
    }

    private static Scenario CreateScenario(int index)
    {
        return index switch
        {
            0 => CreateHvacScenario(
                index,
                "Conditioned Zone",
                includeAvailability: true,
                includeSupply: true),
            1 => CreateHvacScenario(
                index,
                "No Availability Zone",
                includeAvailability: false,
                includeSupply: true),
            2 => CreateHvacScenario(
                index,
                "No Supply Zone",
                includeAvailability: true,
                includeSupply: false),
            3 => CreateEmptyLoadScenario(index),
            4 => CreateErvLoadScenario(index),
            5 => CreateFullLoadScenario(index),
            6 => CreateParentScenario(index, "Empty Parent Zone", Array.Empty<Surface>()),
            7 => CreateParentScenario(
                index,
                "Multiple Surface Zone",
                new[]
                {
                    CreateSurface(index, 0, "Floor-A", SurfaceType.Floor, 25),
                    CreateSurface(index, 1, "Wall-B", SurfaceType.Wall, 30),
                }),
            8 => CreateParentScenario(
                index,
                "Ordered Parent Zone",
                new[]
                {
                    CreateSurface(index, 0, "Floor-First", SurfaceType.Floor, 12.5),
                    CreateSurface(index, 1, "Floor-Empty", SurfaceType.Floor, 7.5),
                    CreateSurface(index, 2, "Ceiling-Last", SurfaceType.Ceiling, 90),
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }

    private static Scenario CreateHvacScenario(
        int index,
        string name,
        bool includeAvailability,
        bool includeSupply)
    {
        Schedule heating = Schedule.Constant("Heat Schedule", 20, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant("Cool Schedule", 26, ScheduleType.Temperature);
        Schedule? availability = includeAvailability
            ? Schedule.Constant("Availability", 1, ScheduleType.OnOff)
            : null;
        var profile = new ZoneProfile(
            Entity($"PROFILE-{index}"),
            "HVAC Profile",
            heating,
            cooling,
            availability);
        var zone = new Zone(
            Entity($"ZONE-{index}"),
            name,
            Array.Empty<Surface>(),
            profile,
            lightingPowerDensityWattsPerSquareMetre: 7.5);

        ZoneHvacAssignment? assignment = null;
        if (includeSupply)
        {
            var radiator = new ElectricRadiator(Entity($"RADIATOR-{index}"), "Panel");
            assignment = new ZoneHvacAssignment(
                zone.Id,
                new SupplyGroup(new SupplySystem[] { radiator }));
        }

        var model = new EnergyModel(
            $"Zone oracle model {index}",
            new[] { zone },
            assignment is null ? null : new[] { assignment });
        return new Scenario(
            ExpectedCases[index],
            model,
            zone,
            assignment,
            null);
    }

    private static Scenario CreateEmptyLoadScenario(int index)
    {
        var profile = new ZoneProfile(Entity($"PROFILE-{index}"), "Empty Profile");
        var zone = new Zone(
            Entity($"ZONE-{index}"),
            "Empty Load Zone",
            Array.Empty<Surface>(),
            profile);
        return new Scenario(
            ExpectedCases[index],
            new EnergyModel($"Zone oracle model {index}", new[] { zone }),
            zone,
            null,
            null);
    }

    private static Scenario CreateErvLoadScenario(int index)
    {
        Schedule occupant = Schedule.Constant("Dense Occupants", 0.2, ScheduleType.Real);
        var profile = new ZoneProfile(
            Entity($"PROFILE-{index}"),
            "ERV Profile",
            occupant: occupant);
        var zone = new Zone(
            Entity($"ZONE-{index}"),
            "ERV Zone",
            Array.Empty<Surface>(),
            profile);
        var ventilator = new EnergyRecoveryVentilator(
            Entity($"ERV-{index}"),
            "Balanced ERV",
            sensibleEffectiveness: 0.8,
            latentEffectiveness: 0.6);
        var assignment = new ZoneVentilationAssignment(zone.Id, ventilator);
        var model = new EnergyModel(
            $"Zone oracle model {index}",
            new[] { zone },
            ventilationAssignments: new[] { assignment });
        return new Scenario(
            ExpectedCases[index],
            model,
            zone,
            null,
            assignment);
    }

    private static Scenario CreateFullLoadScenario(int index)
    {
        Schedule occupant = Schedule.Constant("Occupant Schedule", 0.125, ScheduleType.Real);
        Schedule lighting = Schedule.Constant("Lighting Schedule", 0.75, ScheduleType.Fraction);
        Schedule equipment = Schedule.Constant("Equipment Schedule", 12.5, ScheduleType.Real);
        var profile = new ZoneProfile(
            Entity($"PROFILE-{index}"),
            "Full Load Profile",
            occupant: occupant,
            lighting: lighting,
            equipment: equipment);
        var zone = new Zone(
            Entity($"ZONE-{index}"),
            "Full Load Zone",
            Array.Empty<Surface>(),
            profile,
            infiltrationAirChangesPerHour: 0.35,
            lightingPowerDensityWattsPerSquareMetre: 8.75);
        return new Scenario(
            ExpectedCases[index],
            new EnergyModel($"Zone oracle model {index}", new[] { zone }),
            zone,
            null,
            null);
    }

    private static Scenario CreateParentScenario(
        int index,
        string name,
        IReadOnlyList<Surface> surfaces)
    {
        var profile = new ZoneProfile(
            Entity($"PROFILE-{index}"),
            $"{name} Profile");
        var zone = new Zone(Entity($"ZONE-{index}"), name, surfaces, profile);
        return new Scenario(
            ExpectedCases[index],
            new EnergyModel($"Zone oracle model {index}", new[] { zone }),
            zone,
            null,
            null);
    }

    private static Surface CreateSurface(
        int caseIndex,
        int surfaceIndex,
        string name,
        SurfaceType type,
        double area)
    {
        var material = new Material($"Material {caseIndex}-{surfaceIndex}", 1.4, 2_200, 880);
        var construction = new OpaqueConstruction(
            $"Construction {caseIndex}-{surfaceIndex}",
            new[]
            {
                new Layer($"Layer {caseIndex}-{surfaceIndex}", material, 0.2),
            });
        double z = type == SurfaceType.Ceiling ? 3 : 0;
        var polygon = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, z),
            new Vertex(area, 0, z),
            new Vertex(area, 1, z),
            new Vertex(0, 1, z),
        });
        SurfaceBoundary boundary = type == SurfaceType.Floor
            ? SurfaceBoundary.Ground
            : SurfaceBoundary.Outdoors;
        return new Surface(
            Entity($"SURFACE-{caseIndex}-{surfaceIndex}"),
            name,
            type,
            construction,
            boundary,
            polygon);
    }

    private static EntityId Entity(string value) => new(value);

    private static EnergyModelIdfOptions CreateOptions() => new()
    {
        ThrowOnValidationErrors = false,
        AddIdealLoadsForUnassignedZones = false,
        UseLegacySimpleDragonDefaultObjectFields = true,
        UseLegacySimpleDragonScheduleMetadata = true,
        UseLegacySimpleDragonUsedProfileScheduleSelection = true,
        UseLegacySimpleDragonHvacTopology = true,
        UseLegacySimpleDragonVentilation = true,
    };

    private static NativeObservation ExecuteNativeCase(
        CaseBinding binding,
        JsonElement pythonFacts,
        Scenario scenario,
        OfficialIddOracle iddOracle)
    {
        GraphSnapshot before = GraphSnapshot.Capture(scenario);
        IdfDocument first = scenario.Model.ToIdfDocument(options: CreateOptions());
        before.AssertUnchanged(scenario);
        IdfDocument second = scenario.Model.ToIdfDocument(options: CreateOptions());
        before.AssertUnchanged(scenario);

        Assert.NotSame(first, second);
        Assert.Equal(DocumentFingerprint(first), DocumentFingerprint(second));
        Assert.Equal(first.Count, second.Count);
        for (int index = 0; index < first.Count; index++)
        {
            Assert.NotSame(first[index], second[index]);
            Assert.Equal(ObjectFingerprint(first[index]), ObjectFingerprint(second[index]));
            Assert.Equal(first[index].Fields.Count, second[index].Fields.Count);
            for (int field = 0; field < first[index].Fields.Count; field++)
            {
                Assert.NotSame(first[index].Fields[field], second[index].Fields[field]);
            }
        }

        Assert.Same(scenario.Zone, Assert.Single(scenario.Model.Zones));
        IdfObject nativeZone = Assert.Single(
            first["Zone"],
            item => item.Name == scenario.Zone.Name);
        Assert.Equal(scenario.Zone.Name, nativeZone[0]);

        JsonElement[] pythonOutput = pythonFacts.GetProperty("emission")
            .GetProperty("first_output")
            .EnumerateArray()
            .ToArray();
        JsonElement[] pythonObjects = pythonOutput
            .Where(item => RequiredString(item, "kind") == "idf-object")
            .ToArray();
        string[] traceLabels = pythonOutput
            .Where(item => RequiredString(item, "kind") == "trace-token")
            .Select(item => RequiredString(item, "label"))
            .ToArray();

        var matched = new List<IdfObject>();
        var defaultOmissions = new List<DefaultOmissionFact>();
        var enrichments = new List<ContextEnrichmentFact>();
        var relocations = new List<FieldRelocationFact>();
        int blankOmissions = 0;
        foreach (JsonElement expectedObject in pythonObjects)
        {
            IdfObject actual = FindNativeTargetObject(first, expectedObject);
            matched.Add(actual);
            ComparisonAnalysis analysis = CompareObject(
                binding,
                expectedObject,
                actual,
                iddOracle);
            defaultOmissions.AddRange(analysis.DefaultOmissions);
            enrichments.AddRange(analysis.ContextEnrichments);
            relocations.AddRange(analysis.FieldRelocations);
            blankOmissions += analysis.BlankOrNoneOmissions;
        }

        if (pythonObjects.Length == 0)
        {
            AssertAbsentTargetSlice(first, binding.Symbol, scenario.Zone.Name);
        }

        int[] nativePositions = matched.Select(item => IndexOf(first, item)).ToArray();
        string orderClassification;
        if (binding.Symbol == "Zone.to_idf_object")
        {
            orderClassification = "python-trace-double-boundary-native-real-geometry";
        }
        else if (matched.Count == 0)
        {
            orderClassification = "both-empty-target-slice";
        }
        else if (nativePositions.SequenceEqual(nativePositions.OrderBy(item => item)))
        {
            orderClassification = "same-relative-order";
        }
        else
        {
            orderClassification = "native-parent-orchestration-order-differs";
        }

        Assert.Equal(binding.ExpectedOrderClassification, orderClassification);
        string[] nativeMatchedOrder = matched
            .OrderBy(item => IndexOf(first, item))
            .Select(ObjectIdentity)
            .ToArray();
        string[] pythonTargetOrder = pythonObjects.Select(PythonObjectIdentity).ToArray();

        string[] nativeGeometryNames = first["BuildingSurface:Detailed"]
            .Where(item => item.Count > 3 && item[3] == scenario.Zone.Name)
            .Select(item => item.Name!)
            .ToArray();
        Assert.Equal(scenario.Zone.Surfaces.Select(item => item.Name), nativeGeometryNames);
        ValidateTraceBoundary(binding, pythonFacts, scenario, traceLabels, nativeGeometryNames);

        string[] nativeContextOrder = first
            .Where(item => IsZoneLinked(item, scenario.Zone.Name))
            .Select(ObjectIdentity)
            .ToArray();
        Assert.Contains(ObjectIdentity(nativeZone), nativeContextOrder);

        int[] compactFieldCounts = matched.Select(item => item.Count).ToArray();
        Assert.Equal(binding.ExpectedCompactFieldCounts, compactFieldCounts);
        Assert.Equal(binding.ExpectedDefaultOmissionCount, defaultOmissions.Count);
        Assert.Equal(binding.ExpectedBlankOmissionCount, blankOmissions);
        Assert.Equal(binding.ExpectedContextEnrichmentCount, enrichments.Count);
        Assert.Equal(binding.ExpectedFieldRelocationCount, relocations.Count);

        return new NativeObservation(
            binding.CaseId,
            binding.Symbol,
            pythonObjects.Length,
            pythonTargetOrder,
            matched.Select(item => item.ObjectType).ToArray(),
            compactFieldCounts,
            matched.Select(item => item.Name ?? string.Empty).ToArray(),
            matched.Select(item => item.Fields.Select(field => field.Value).ToArray()).ToArray(),
            nativeMatchedOrder,
            nativeContextOrder,
            nativeGeometryNames,
            traceLabels,
            ParentChildCallTrace(pythonFacts),
            orderClassification,
            blankOmissions,
            defaultOmissions.ToArray(),
            enrichments.ToArray(),
            relocations.ToArray(),
            new[]
            {
                "public-route=EnergyModel.ToIdfDocument",
                "repeated-call=document-and-every-object-and-field-fresh",
                "input-graph=reference-and-value-immutable",
                "target-linkage=exact-zone-name-and-model-zone-reference",
                "native-options=legacy-topology-schedules-ventilation-with-validation-and-ideal-loads-disabled",
            });
    }

    private static ComparisonAnalysis CompareObject(
        CaseBinding binding,
        JsonElement expectedObject,
        IdfObject actual,
        OfficialIddOracle iddOracle)
    {
        string objectType = RequiredString(expectedObject, "object_type");
        Assert.Equal(objectType, actual.ObjectType);
        OfficialIddObject idd = iddOracle[objectType];
        JsonElement[] fields = expectedObject.GetProperty("ordered_fields")
            .EnumerateArray()
            .ToArray();
        var defaults = new List<DefaultOmissionFact>();
        var enrichments = new List<ContextEnrichmentFact>();
        var relocations = new List<FieldRelocationFact>();
        int blanks = 0;

        for (int position = 0; position < fields.Length; position++)
        {
            JsonElement expectedField = fields[position];
            string expectedFieldName = RequiredString(expectedField, "name");
            OfficialIddField officialField = idd.ResolveField(position);
            if (expectedFieldName.Length > 0)
            {
                Assert.Equal(expectedFieldName, officialField.Name);
            }

            JsonElement expectedValue = expectedField.GetProperty("value");
            string nativeValue = position < actual.Count ? actual[position] : string.Empty;
            bool expectedNone = RequiredString(expectedValue, "kind") == "none";
            if (TryRelocatedField(
                binding,
                objectType,
                position,
                out int pythonPosition))
            {
                Assert.True(nativeValue.Length > 0);
                JsonElement relocatedExpected = fields[pythonPosition].GetProperty("value");
                AssertScalarMatches(
                    relocatedExpected,
                    nativeValue,
                    objectType,
                    pythonPosition);
                Assert.NotNull(officialField.DefaultValue);
                AssertScalarMatches(
                    expectedValue,
                    officialField.DefaultValue!,
                    objectType,
                    position);
                defaults.Add(new DefaultOmissionFact(
                    objectType,
                    position,
                    officialField.Name,
                    EncodePythonScalar(expectedValue),
                    officialField.DefaultValue!));
                relocations.Add(new FieldRelocationFact(
                    objectType,
                    position,
                    pythonPosition,
                    fields[pythonPosition].GetProperty("name").GetString()!,
                    nativeValue,
                    "native-compact-fallback-position-13-retains-the-declared-zone-air-distribution-field-that-official-24.2-IDD-and-Python-place-at-position-22"));
                continue;
            }

            if (IsRelocatedFieldTarget(binding, objectType, position))
            {
                Assert.True(nativeValue.Length == 0);
                continue;
            }

            if (nativeValue.Length > 0)
            {
                if (expectedNone)
                {
                    string expectedEnrichment = ExpectedContextEnrichment(
                        binding,
                        objectType,
                        position);
                    Assert.Equal(expectedEnrichment, nativeValue);
                    enrichments.Add(new ContextEnrichmentFact(
                        objectType,
                        position,
                        officialField.Name,
                        nativeValue,
                        "native-parent-populates-real-zone-equipment-where-standalone-python-zone-emitter-retains-placeholder"));
                }
                else
                {
                    AssertScalarMatches(expectedValue, nativeValue, objectType, position);
                }

                continue;
            }

            if (expectedNone)
            {
                blanks++;
                continue;
            }

            Assert.NotNull(officialField.DefaultValue);
            AssertScalarMatches(
                expectedValue,
                officialField.DefaultValue!,
                objectType,
                position);
            defaults.Add(new DefaultOmissionFact(
                objectType,
                position,
                officialField.Name,
                EncodePythonScalar(expectedValue),
                officialField.DefaultValue!));
        }

        for (int position = fields.Length; position < actual.Count; position++)
        {
            Assert.True(
                string.IsNullOrEmpty(actual[position]),
                $"Unexpected native field {position} after the pinned Python field range of {objectType}.");
        }

        return new ComparisonAnalysis(
            defaults.ToArray(),
            enrichments.ToArray(),
            relocations.ToArray(),
            blanks);
    }

    private static bool TryRelocatedField(
        CaseBinding binding,
        string objectType,
        int nativePosition,
        out int pythonPosition)
    {
        if (binding.CaseId == ExpectedCases[0].CaseId
            && objectType == "Sizing:Zone"
            && nativePosition == 13)
        {
            pythonPosition = 22;
            return true;
        }

        pythonPosition = -1;
        return false;
    }

    private static bool IsRelocatedFieldTarget(
        CaseBinding binding,
        string objectType,
        int pythonPosition) =>
        binding.CaseId == ExpectedCases[0].CaseId
        && objectType == "Sizing:Zone"
        && pythonPosition == 22;

    private static string ExpectedContextEnrichment(
        CaseBinding binding,
        string objectType,
        int position)
    {
        if (binding.CaseId != ExpectedCases[0].CaseId
            || objectType != "ZoneHVAC:EquipmentList")
        {
            return "__no-context-enrichment-is-allowed__";
        }

        return position switch
        {
            2 => "ZoneHVAC:Baseboard:RadiantConvective:Electric",
            3 => "ElectricRadiator_named_Panel_for_Conditioned Zone",
            4 => "1",
            5 => "1",
            6 => "ALLOFF",
            7 => "heating_fraction_for_ElectricRadiator_named_Panel_for_Conditioned Zone",
            _ => "__no-context-enrichment-is-allowed__",
        };
    }

    private static IdfObject FindNativeTargetObject(
        IdfDocument document,
        JsonElement expectedObject)
    {
        string objectType = RequiredString(expectedObject, "object_type");
        JsonElement firstField = expectedObject.GetProperty("ordered_fields")[0];
        string expectedName = DecodePythonScalar(firstField.GetProperty("value"));
        return Assert.Single(
            document[objectType],
            item => string.Equals(item.Name, expectedName, StringComparison.Ordinal));
    }

    private static void AssertAbsentTargetSlice(
        IdfDocument document,
        string symbol,
        string zoneName)
    {
        string[] objectTypes = symbol switch
        {
            "Zone.to_idf_hvac_default_object" => new[]
            {
                "DesignSpecification:OutdoorAir",
                "DesignSpecification:ZoneAirDistribution",
                "Sizing:Zone",
                "ZoneHVAC:EquipmentList",
                "ZoneHVAC:EquipmentConnections",
                "ThermostatSetpoint:DualSetpoint",
                "ZoneControl:Thermostat",
            },
            "Zone.to_idf_load_object" => new[]
            {
                "Lights",
                "ElectricEquipment",
                "People",
                "ZoneInfiltration:DesignFlowRate",
                "ZoneVentilation:DesignFlowRate",
            },
            _ => Array.Empty<string>(),
        };
        foreach (string objectType in objectTypes)
        {
            Assert.DoesNotContain(document[objectType], item => IsZoneLinked(item, zoneName));
        }

        if (symbol == "Zone.to_idf_hvac_default_object")
        {
            Assert.DoesNotContain(
                document["Schedule:Constant"],
                item => string.Equals(
                    item.Name,
                    $"ScheduleTypeForThermostat_for_{zoneName}",
                    StringComparison.Ordinal));
        }
    }

    private static void ValidateTraceBoundary(
        CaseBinding binding,
        JsonElement pythonFacts,
        Scenario scenario,
        IReadOnlyList<string> traceLabels,
        IReadOnlyList<string> nativeGeometryNames)
    {
        if (binding.Symbol != "Zone.to_idf_object")
        {
            Assert.Empty(traceLabels);
            return;
        }

        string[] surfaceTraceNames = traceLabels
            .Where(item => item.StartsWith("surface:", StringComparison.Ordinal))
            .Select(item => item.Split(':')[1])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] expectedEmittingSurfaces = pythonFacts.GetProperty("input_context")
            .GetProperty("surfaces")
            .EnumerateArray()
            .Where(item => item.GetProperty("emissions").GetArrayLength() > 0)
            .Select(item => RequiredString(item, "label"))
            .ToArray();
        Assert.Equal(expectedEmittingSurfaces, surfaceTraceNames);
        Assert.Equal(scenario.Zone.Surfaces.Select(item => item.Name), nativeGeometryNames);

        string[] calls = ParentChildCallTrace(pythonFacts);
        string[] expectedCalls = scenario.Zone.Surfaces
            .Select(item => $"surface:{item.Name}:zone:{scenario.Zone.Name}")
            .Concat(new[] { "load", "hvac-default" })
            .ToArray();
        Assert.Equal(expectedCalls, calls);

        string[] childOutputKinds = traceLabels
            .Where(item => item.StartsWith("hvac:", StringComparison.Ordinal)
                || item.StartsWith("load:", StringComparison.Ordinal))
            .Select(item => item.Split(':')[0])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (traceLabels.Count > 0)
        {
            Assert.Equal(new[] { "hvac", "load" }, childOutputKinds);
        }
    }

    private static string[] ParentChildCallTrace(JsonElement pythonFacts)
    {
        return pythonFacts.TryGetProperty("child_call_trace_first", out JsonElement trace)
            ? trace.EnumerateArray().Select(item => item.GetString()!).ToArray()
            : Array.Empty<string>();
    }

    private static bool IsZoneLinked(IdfObject item, string zoneName)
    {
        return item.Fields.Any(field =>
            string.Equals(field.Value, zoneName, StringComparison.Ordinal)
            || field.Value.Contains($"_for_{zoneName}", StringComparison.Ordinal)
            || field.Value.Contains($":{zoneName}", StringComparison.Ordinal));
    }

    private static int IndexOf(IdfDocument document, IdfObject value)
    {
        for (int index = 0; index < document.Count; index++)
        {
            if (ReferenceEquals(document[index], value))
            {
                return index;
            }
        }

        throw new InvalidOperationException("The selected native object is not in its source document.");
    }

    private static string ObjectIdentity(IdfObject value) =>
        value.ObjectType + ":" + (value.Name ?? string.Empty);

    private static string PythonObjectIdentity(JsonElement value)
    {
        string objectType = RequiredString(value, "object_type");
        string name = DecodePythonScalar(
            value.GetProperty("ordered_fields")[0].GetProperty("value"));
        return objectType + ":" + name;
    }

    private static void AssertScalarMatches(
        JsonElement expected,
        string actual,
        string objectType,
        int position)
    {
        string kind = RequiredString(expected, "kind");
        if (kind is "float" or "int")
        {
            string encoded = kind == "float"
                ? RequiredString(expected, "repr")
                : RequiredString(expected, "value");
            Assert.True(
                double.TryParse(encoded, NumberStyles.Float, CultureInfo.InvariantCulture, out double expectedNumber),
                $"Pinned Python numeric value is invalid at {objectType}[{position}].");
            Assert.True(
                double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double actualNumber),
                $"Native or IDD numeric value '{actual}' is invalid at {objectType}[{position}].");
            double tolerance = Math.Max(1e-12, Math.Abs(expectedNumber) * 1e-12);
            Assert.True(
                Math.Abs(expectedNumber - actualNumber) <= tolerance,
                $"Numeric mismatch at {objectType}[{position}]: Python={encoded}, native={actual}.");
            return;
        }

        if (kind == "str")
        {
            Assert.Equal(RequiredString(expected, "value"), actual);
            return;
        }

        if (kind == "bool")
        {
            Assert.Equal(
                expected.GetProperty("value").GetBoolean() ? "Yes" : "No",
                actual);
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported non-empty Python scalar kind '{kind}' at {objectType}[{position}].");
    }

    private static string DecodePythonScalar(JsonElement value)
    {
        string kind = RequiredString(value, "kind");
        return kind switch
        {
            "none" => string.Empty,
            "str" => RequiredString(value, "value"),
            "int" => RequiredString(value, "value"),
            "float" => RequiredString(value, "repr"),
            "bool" => value.GetProperty("value").GetBoolean() ? "Yes" : "No",
            _ => throw new InvalidOperationException($"Unsupported Python scalar kind '{kind}'."),
        };
    }

    private static string EncodePythonScalar(JsonElement value) =>
        RequiredString(value, "kind") + ":" + DecodePythonScalar(value);

    private static void AssertEncodedDouble(JsonElement value, double expected)
    {
        string kind = RequiredString(value, "kind");
        Assert.True(kind is "float" or "int");
        string representation = kind == "float"
            ? RequiredString(value, "repr")
            : RequiredString(value, "value");
        Assert.True(double.TryParse(
            representation,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double actual));
        Assert.Equal(expected, actual);
    }

    private static void AssertIndependentScenarioGraphs(IReadOnlyList<Scenario> scenarios)
    {
        Assert.Equal(
            scenarios.Count,
            scenarios.Select(item => item.Model).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(
            scenarios.Count,
            scenarios.Select(item => item.Zone).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(
            scenarios.Count,
            scenarios.Select(item => item.Zone.Profile).Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    private static string ObjectFingerprint(IdfObject value) =>
        JsonSerializer.Serialize(new
        {
            object_type = value.ObjectType,
            fields = value.Fields.Select(item => item.Value).ToArray(),
        });

    private static string DocumentFingerprint(IdfDocument document) =>
        Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            document.Select(ObjectFingerprint).ToArray())));

    private static OfficialIddOracle LoadOfficialIddOracle()
    {
        byte[] compressed = File.ReadAllBytes(FindRepositoryFile(IddOracleRepositoryPath));
        Assert.Equal(IddOracleByteLength, compressed.Length);
        Assert.Equal(IddOracleSha256, Sha256(compressed));
        using var input = new MemoryStream(compressed, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using JsonDocument document = JsonDocument.Parse(
            gzip,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        JsonElement root = document.RootElement;
        AssertUniqueObjectKeysRecursive(root);
        Assert.Equal(IddOracleSchema, RequiredString(root, "oracle_schema"));
        Assert.Equal(UpstreamCommit, RequiredString(root, "upstream_commit"));
        Assert.Equal(EnergyPlusVersion, RequiredString(root, "energyplus_version"));
        Assert.Equal(EnergyPlusBuild, RequiredString(root, "energyplus_build"));
        Assert.Equal(EnergyPlusIddSourceSha256, RequiredString(root, "source_sha256"));
        Assert.Equal(EnergyPlusIddSourceByteLength, root.GetProperty("source_bytes").GetInt32());
        Assert.Equal(848, root.GetProperty("object_count").GetInt32());
        Assert.Equal(13_702, root.GetProperty("field_count").GetInt32());

        JsonElement[] objects = root.GetProperty("objects").EnumerateArray().ToArray();
        Assert.Equal(848, objects.Length);
        var selected = new List<OfficialIddObject>();
        foreach (string objectType in SelectedObjectTypes)
        {
            JsonElement item = Assert.Single(
                objects,
                candidate => RequiredString(candidate, "name") == objectType);
            selected.Add(ParseOfficialIddObject(item));
        }

        Assert.Equal(
            SelectedObjectTypes,
            selected.Select(item => item.Name));
        return new OfficialIddOracle(selected);
    }

    private static OfficialIddObject ParseOfficialIddObject(JsonElement item)
    {
        JsonElement[] fields = item.GetProperty("fields").EnumerateArray().ToArray();
        var parsed = new OfficialIddField[fields.Length];
        for (int index = 0; index < fields.Length; index++)
        {
            JsonElement field = fields[index];
            Assert.Equal(index, field.GetProperty("position").GetInt32());
            JsonElement defaultValue = field.GetProperty("default_value");
            Assert.True(defaultValue.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            parsed[index] = new OfficialIddField(
                index,
                RequiredString(field, "name"),
                field.GetProperty("begins_extensible").GetBoolean(),
                defaultValue.ValueKind == JsonValueKind.Null ? null : defaultValue.GetString());
        }

        JsonElement start = item.GetProperty("extensible_start_index");
        return new OfficialIddObject(
            RequiredString(item, "name"),
            item.GetProperty("minimum_fields").GetInt32(),
            start.ValueKind == JsonValueKind.Null ? null : start.GetInt32(),
            item.GetProperty("extensible_group_size").GetInt32(),
            parsed);
    }

    private static object CreateReceipt(
        SymbolBinding symbol,
        IReadOnlyList<NativeObservation> observations)
    {
        return new
        {
            artifacts = new
            {
                fixture = new
                {
                    byte_length = OracleByteLength,
                    case_count = ExpectedCases.Length,
                    cases_sha256 = CasesSha256,
                    path = OracleRepositoryPath,
                    sha256 = OracleSha256,
                },
                generator = Artifact(GeneratorRepositoryPath, GeneratorByteLength, GeneratorSha256),
                idd_default_oracle = new
                {
                    compressed_byte_length = IddOracleByteLength,
                    compressed_sha256 = IddOracleSha256,
                    energyplus_build = EnergyPlusBuild,
                    energyplus_version = EnergyPlusVersion,
                    official_idd_source_byte_length = EnergyPlusIddSourceByteLength,
                    official_idd_source_sha256 = "sha256:" + EnergyPlusIddSourceSha256,
                    oracle_schema = IddOracleSchema,
                    path = IddOracleRepositoryPath,
                },
                implementation = Artifact(
                    ImplementationRepositoryPath,
                    ImplementationByteLength,
                    ImplementationSha256),
                native_zone = Artifact(ZoneRepositoryPath, ZoneByteLength, ZoneSha256),
                public_route = Artifact(PublicRepositoryPath, PublicByteLength, PublicSha256),
                python_validator = Artifact(
                    PythonValidatorRepositoryPath,
                    PythonValidatorByteLength,
                    PythonValidatorSha256),
            },
            native_binding = new
            {
                adaptation_id = symbol.AdaptationId,
                classification = "exception",
                classification_basis = ClassificationBasis,
                implementation_symbols = symbol.ImplementationSymbols,
                native_target = "EnergyModel.ToIdfDocument",
                public_symbol = PublicSymbol,
            },
            observations = observations.Select(item => new
            {
                adaptation_id = symbol.AdaptationId,
                case_id = item.CaseId,
                compact_field_counts = item.CompactFieldCounts,
                context_enrichments = item.ContextEnrichments.Select(value => new
                {
                    field_name = value.FieldName,
                    native_value = value.NativeValue,
                    object_type = value.ObjectType,
                    reason = value.Reason,
                    zero_based_position = value.ZeroBasedPosition,
                }).ToArray(),
                field_relocations = item.FieldRelocations.Select(value => new
                {
                    native_compact_position = value.NativeCompactPosition,
                    native_value = value.NativeValue,
                    object_type = value.ObjectType,
                    official_field_name = value.OfficialFieldName,
                    python_official_position = value.PythonOfficialPosition,
                    reason = value.Reason,
                }).ToArray(),
                native_context_order = item.NativeContextOrder,
                native_facts = item.NativeFacts,
                native_geometry_order = item.NativeGeometryNames,
                native_matched_order = item.NativeMatchedOrder,
                native_object_field_values = item.NativeObjectFieldValues,
                native_object_names = item.NativeObjectNames,
                native_object_types_in_python_order = item.NativeObjectTypes,
                native_outcome = "returned",
                omitted_blank_or_none_count = item.OmittedBlankOrNoneCount,
                omitted_official_idd_defaults = item.OmittedOfficialIddDefaults.Select(value => new
                {
                    field_name = value.FieldName,
                    object_type = value.ObjectType,
                    official_idd_default = value.OfficialIddDefault,
                    python_encoded_value = value.PythonEncodedValue,
                    zero_based_position = value.ZeroBasedPosition,
                }).ToArray(),
                order_classification = item.OrderClassification,
                python_child_call_order = item.PythonChildCallOrder,
                python_object_count = item.PythonObjectCount,
                python_target_order = item.PythonTargetOrder,
                python_trace_tokens = item.PythonTraceLabels,
                trace_double_native_analogue = item.Symbol == "Zone.to_idf_object"
                    ? "not-replayed-native-public-route-emits-real-geometry-and-parent-stages"
                    : "not-applicable",
            }).ToArray(),
            representation = new
            {
                comparison = "every-shared-python-IDF-field-by-position-versus-native-compact-field-or-pinned-official-IDD-default",
                context_enrichment_count = observations.Sum(item => item.ContextEnrichments.Length),
                field_relocation_count = observations.Sum(item => item.FieldRelocations.Length),
                fixture_result_shape = "standalone-fresh-list-with-parent-trace-doubles-for-Zone.to_idf_object",
                native_result_shape = "fresh-EnergyModel-IDF-documents-with-semantic-target-selection-and-real-geometry",
                official_idd_default_omission_count = observations.Sum(item => item.OmittedOfficialIddDefaults.Length),
                omitted_blank_or_none_count = observations.Sum(item => item.OmittedBlankOrNoneCount),
                order_policy = "exact-relative-order-when-shared;-differences-explicitly-classified-for-parent-orchestration-and-trace-doubles",
            },
            scope = new
            {
                context_only_not_targeted = ContextOnlyNotTargeted,
                full_symbol_closure = false,
                scope = FixtureScope,
                unresolved_behavior = UnresolvedBehavior,
            },
            upstream = new
            {
                ast_sha256 = UpstreamAstSha256,
                body_hash = symbol.BodyHash,
                inventory_index = symbol.InventoryIndex,
                path = UpstreamPath,
                signature_hash = symbol.SignatureHash,
                source_sha256 = UpstreamSourceSha256,
                symbol = symbol.Symbol,
                symbol_hash = symbol.SymbolHash,
            },
        };
    }

    private static object Artifact(string path, int byteLength, string sha256) => new
    {
        byte_length = byteLength,
        path,
        sha256,
    };

    private static void ValidateReceipt(
        object receipt,
        SymbolBinding symbol,
        IReadOnlyList<NativeObservation> observations)
    {
        JsonElement value = JsonSerializer.SerializeToElement(receipt);
        AssertUniqueObjectKeysRecursive(value);
        AssertNoUnsafeIdentity(value);
        AssertNoHostPaths(value);
        AssertNoNonFiniteJsonNumbers(value);
        AssertKeys(
            value,
            "artifacts",
            "native_binding",
            "observations",
            "representation",
            "scope",
            "upstream");
        JsonElement nativeBinding = value.GetProperty("native_binding");
        Assert.Equal(symbol.AdaptationId, RequiredString(nativeBinding, "adaptation_id"));
        Assert.Equal("exception", RequiredString(nativeBinding, "classification"));
        Assert.Equal(PublicSymbol, RequiredString(nativeBinding, "public_symbol"));
        AssertStringArray(
            nativeBinding.GetProperty("implementation_symbols"),
            symbol.ImplementationSymbols);

        JsonElement[] actualObservations = value.GetProperty("observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(observations.Count, actualObservations.Length);
        Assert.Equal(
            observations.Select(item => item.CaseId),
            actualObservations.Select(item => RequiredString(item, "case_id")));
        Assert.All(
            actualObservations,
            item => Assert.Equal("returned", RequiredString(item, "native_outcome")));

        JsonElement upstream = value.GetProperty("upstream");
        Assert.Equal(symbol.Symbol, RequiredString(upstream, "symbol"));
        Assert.Equal(symbol.SymbolHash, RequiredString(upstream, "symbol_hash"));
        Assert.Equal(symbol.SignatureHash, RequiredString(upstream, "signature_hash"));
        Assert.Equal(symbol.BodyHash, RequiredString(upstream, "body_hash"));
        Assert.Equal(symbol.InventoryIndex, upstream.GetProperty("inventory_index").GetInt32());
    }

    private static void AssertPinnedArtifact(
        string repositoryPath,
        int expectedByteLength,
        string expectedSha256)
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(repositoryPath));
        Assert.Equal(expectedByteLength, bytes.Length);
        Assert.Equal(expectedSha256, Sha256(bytes));
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

    private static string MethodSymbol(MethodInfo method) =>
        method.DeclaringType!.FullName + "." + method.Name;

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

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
                throw new InvalidOperationException(
                    $"Unsupported JSON kind '{value.ValueKind}' in canonical payload.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            JsonProperty[] properties = value.EnumerateObject().ToArray();
            Assert.Equal(
                properties.Length,
                properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
            foreach (JsonProperty property in properties)
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

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            Assert.False(Regex.IsMatch(
                text,
                @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])",
                RegexOptions.CultureInvariant));
            Assert.False(Regex.IsMatch(
                text,
                @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])",
                RegexOptions.CultureInvariant));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoUnsafeIdentity(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoUnsafeIdentity(item);
            }
        }
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            Assert.False(Regex.IsMatch(
                value.GetString()!,
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
            Assert.True(double.IsFinite(number));
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
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject()
                .Select(item => item.Name)
                .OrderBy(item => item, StringComparer.Ordinal));
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
    }

    private static void AssertStringArray(
        JsonElement value,
        IEnumerable<string> expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private sealed record SymbolBinding(
        int InventoryIndex,
        string Symbol,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string AdaptationId,
        string[] ImplementationSymbols);

    private sealed record CaseBinding(
        string CaseId,
        string Symbol,
        string ExpectedOrderClassification,
        int[] ExpectedCompactFieldCounts,
        int ExpectedDefaultOmissionCount,
        int ExpectedBlankOmissionCount,
        int ExpectedContextEnrichmentCount,
        int ExpectedFieldRelocationCount);

    private sealed record Scenario(
        CaseBinding Binding,
        EnergyModel Model,
        Zone Zone,
        ZoneHvacAssignment? HvacAssignment,
        ZoneVentilationAssignment? VentilationAssignment);

    private sealed record DefaultOmissionFact(
        string ObjectType,
        int ZeroBasedPosition,
        string FieldName,
        string PythonEncodedValue,
        string OfficialIddDefault);

    private sealed record ContextEnrichmentFact(
        string ObjectType,
        int ZeroBasedPosition,
        string FieldName,
        string NativeValue,
        string Reason);

    private sealed record ComparisonAnalysis(
        DefaultOmissionFact[] DefaultOmissions,
        ContextEnrichmentFact[] ContextEnrichments,
        FieldRelocationFact[] FieldRelocations,
        int BlankOrNoneOmissions);

    private sealed record FieldRelocationFact(
        string ObjectType,
        int NativeCompactPosition,
        int PythonOfficialPosition,
        string OfficialFieldName,
        string NativeValue,
        string Reason);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        int PythonObjectCount,
        string[] PythonTargetOrder,
        string[] NativeObjectTypes,
        int[] CompactFieldCounts,
        string[] NativeObjectNames,
        string[][] NativeObjectFieldValues,
        string[] NativeMatchedOrder,
        string[] NativeContextOrder,
        string[] NativeGeometryNames,
        string[] PythonTraceLabels,
        string[] PythonChildCallOrder,
        string OrderClassification,
        int OmittedBlankOrNoneCount,
        DefaultOmissionFact[] OmittedOfficialIddDefaults,
        ContextEnrichmentFact[] ContextEnrichments,
        FieldRelocationFact[] FieldRelocations,
        string[] NativeFacts);

    private sealed record OfficialIddField(
        int Position,
        string Name,
        bool BeginsExtensible,
        string? DefaultValue);

    private sealed record OfficialIddObject(
        string Name,
        int MinimumFields,
        int? ExtensibleStartIndex,
        int ExtensibleGroupSize,
        OfficialIddField[] Fields)
    {
        public OfficialIddField ResolveField(int index)
        {
            if (index < Fields.Length)
            {
                return Fields[index];
            }

            Assert.NotNull(ExtensibleStartIndex);
            Assert.True(ExtensibleGroupSize > 0);
            int start = ExtensibleStartIndex!.Value;
            Assert.True(index >= start);
            int prototypePosition = start + ((index - start) % ExtensibleGroupSize);
            Assert.InRange(prototypePosition, 0, Fields.Length - 1);
            OfficialIddField prototype = Fields[prototypePosition];
            int group = ((index - start) / ExtensibleGroupSize) + 1;
            string name = Regex.Replace(
                prototype.Name,
                @"\b1\b",
                group.ToString(CultureInfo.InvariantCulture),
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            return prototype with { Position = index, Name = name };
        }
    }

    private sealed class OfficialIddOracle
    {
        private readonly IReadOnlyDictionary<string, OfficialIddObject> objects;

        public OfficialIddOracle(IEnumerable<OfficialIddObject> values)
        {
            objects = values.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        public OfficialIddObject this[string objectType] => objects[objectType];
    }

    private sealed record GraphSnapshot(object[] References, string ValueFingerprint)
    {
        public static GraphSnapshot Capture(Scenario scenario) =>
            new(GraphReferences(scenario), GraphValueFingerprint(scenario));

        public void AssertUnchanged(Scenario scenario)
        {
            object[] current = GraphReferences(scenario);
            Assert.Equal(References.Length, current.Length);
            for (int index = 0; index < References.Length; index++)
            {
                Assert.Same(References[index], current[index]);
            }

            Assert.Equal(ValueFingerprint, GraphValueFingerprint(scenario));
        }

        private static object[] GraphReferences(Scenario scenario)
        {
            var values = new List<object>
            {
                scenario.Model,
                scenario.Model.Zones,
                scenario.Model.HvacAssignments,
                scenario.Model.VentilationAssignments,
                scenario.Zone,
                scenario.Zone.Surfaces,
                scenario.Zone.Profile,
            };
            values.AddRange(new Schedule?[]
            {
                scenario.Zone.Profile.HeatingSetpoint,
                scenario.Zone.Profile.CoolingSetpoint,
                scenario.Zone.Profile.HvacAvailability,
                scenario.Zone.Profile.Occupant,
                scenario.Zone.Profile.Lighting,
                scenario.Zone.Profile.Equipment,
                scenario.Zone.Profile.HotWater,
            }.Where(item => item is not null).Cast<object>());
            foreach (Surface surface in scenario.Zone.Surfaces)
            {
                values.Add(surface);
                values.Add(surface.Polygon);
                values.Add(surface.Construction);
                if (surface.Construction is OpaqueConstruction construction)
                {
                    values.Add(construction.Layers);
                    foreach (Layer layer in construction.Layers)
                    {
                        values.Add(layer);
                        values.Add(layer.Material);
                    }
                }
            }

            if (scenario.HvacAssignment is not null)
            {
                values.Add(scenario.HvacAssignment);
                values.Add(scenario.HvacAssignment.Supply);
                values.Add(scenario.HvacAssignment.Supply.Systems);
                values.AddRange(scenario.HvacAssignment.Supply.Systems.Cast<object>());
            }

            if (scenario.VentilationAssignment is not null)
            {
                values.Add(scenario.VentilationAssignment);
                values.Add(scenario.VentilationAssignment.Ventilator);
            }

            return values.ToArray();
        }

        private static string GraphValueFingerprint(Scenario scenario)
        {
            Zone zone = scenario.Zone;
            return JsonSerializer.Serialize(new
            {
                model = scenario.Model.Name,
                zone = new
                {
                    zone.Name,
                    infiltration = zone.InfiltrationAirChangesPerHour,
                    lighting = zone.LightingPowerDensityWattsPerSquareMetre,
                    floor_area = zone.FloorArea,
                    surfaces = zone.Surfaces.Select(item => new
                    {
                        item.Name,
                        type = item.Type.ToString(),
                        area = item.GrossArea,
                        construction = item.Construction.Name,
                    }).ToArray(),
                    profile = new
                    {
                        zone.Profile.Name,
                        schedules = new[]
                        {
                            zone.Profile.HeatingSetpoint,
                            zone.Profile.CoolingSetpoint,
                            zone.Profile.HvacAvailability,
                            zone.Profile.Occupant,
                            zone.Profile.Lighting,
                            zone.Profile.Equipment,
                            zone.Profile.HotWater,
                        }.Where(item => item is not null).Select(item => new
                        {
                            item!.Name,
                            type = item.Type.ToString(),
                            item.Minimum,
                            item.Maximum,
                        }).ToArray(),
                    },
                },
                hvac = scenario.HvacAssignment?.Supply.Systems.Select(item => new
                {
                    type = item.GetType().Name,
                    item.Name,
                }).ToArray(),
                ventilation = scenario.VentilationAssignment is null
                    ? null
                    : new
                    {
                        scenario.VentilationAssignment.Ventilator.Name,
                        scenario.VentilationAssignment.Ventilator.SensibleEffectiveness,
                        scenario.VentilationAssignment.Ventilator.LatentEffectiveness,
                    },
            });
        }
    }
}
