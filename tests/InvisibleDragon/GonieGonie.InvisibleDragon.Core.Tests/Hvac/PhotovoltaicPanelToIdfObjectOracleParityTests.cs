using System.Globalization;
using System.IO.Compression;
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
using GonieGonie.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class PhotovoltaicPanelToIdfObjectOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-hvac-photovoltaic-to-idf-object-oracle.json";
    private const string OracleSchema =
        "goniegonie.python-reference.dragon-hvac-photovoltaic-to-idf-object.v1";
    private const int OracleByteLength = 147_261;
    private const string OracleSha256 =
        "sha256:07c383c316989ccb22ac3eadcf9d8388764f76effbbf03c13b7a54f8af20f22b";
    private const string CasesSha256 =
        "sha256:767c3314ec20d07aa12fdce48b9969a98b54b835855b4be7ecfdd896816be0dd";
    private const string GeneratorRepositoryPath =
        "tools/python-reference/generate_dragon_hvac_photovoltaic_to_idf_object_oracle.py";
    private const int GeneratorByteLength = 31_850;
    private const string GeneratorSha256 =
        "sha256:31ecfd6d9c94691281f2edace585402a8612baba298714874211728bb9d9876c";
    private const string PythonValidatorRepositoryPath =
        "tests/PythonReference/test_dragon_hvac_photovoltaic_to_idf_object_oracle.py";
    private const int PythonValidatorByteLength = 21_314;
    private const string PythonValidatorSha256 =
        "sha256:0b290029117396173f3a648343d0d751a5938c628a6a7d6a03d35738a474ac4c";

    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const int UpstreamInventoryIndex = 761;
    private const string UpstreamPath = "src/idragon/dragon/hvac.py";
    private const string UpstreamSourceSha256 =
        "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0";
    private const string UpstreamAstSha256 =
        "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31";
    private const string UpstreamSymbol = "PhotoVoltaicPanel.to_idf_object";
    private const string UpstreamSymbolHash =
        "sha256:4723273d4b77d9286d4a47c4d753f71049e87d146ff912b0aa6a8ab8ed911287";
    private const string UpstreamSignatureHash =
        "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519";
    private const string UpstreamBodyHash =
        "sha256:a227ed7b60c5a482a11b9a11f36e243b56cae95e2889effe9abe7e6e70d0346b";

    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/VentilationAndPv.cs";
    private const int ImplementationByteLength = 7_074;
    private const string ImplementationSha256 =
        "sha256:eb7d871d621c8f3970099dff7bdb412dc84f33cd2ef07c0fb99c94a550d5eb82";
    private const string ImplementationSymbol =
        "GonieGonie.InvisibleDragon.Hvac.PhotovoltaicPanel.ToIdfObjects";
    private const string NativeTarget = "PhotovoltaicPanel.ToIdfObjects";
    private const string AdaptationId = "compact-native-photovoltaic-idf-emission";
    private const string AssertionId =
        "dragon-hvac-photovoltaic-to-idf-object-4723273d";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Hvac.PhotovoltaicPanelToIdfObjectOracleParityTests.MatchesPinnedPythonPhotovoltaicEmissionWithOfficialIddDefaults";

    private const string IddOracleRepositoryPath =
        "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz";
    private const int IddOracleByteLength = 585_482;
    private const string IddOracleSha256 =
        "sha256:f2dfc27d39f788f945ef5cc3b79ffce2a516a568075717bd67088d900a75c705";
    private const string IddOracleSchema = "goniegonie.energyplus-idd-schema.v1";
    private const string EnergyPlusVersion = "24.2.0";
    private const string EnergyPlusBuild = "94a887817b";
    private const string EnergyPlusIddSourceSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const int EnergyPlusIddSourceByteLength = 4_556_412;

    private static readonly string[] ObjectTypes =
    {
        "Shading:Site",
        "PhotovoltaicPerformance:Simple",
        "Generator:Photovoltaic",
        "ElectricLoadCenter:Generators",
        "ElectricLoadCenter:Inverter:Simple",
        "ElectricLoadCenter:Distribution",
    };

    private static readonly int[] PythonCompleteFieldCounts = { 8, 5, 7, 151, 5, 21 };

    private static readonly int[] NativeCompactFieldCounts = { 8, 4, 4, 4, 5, 8 };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new(
            "dragon-hvac-photovoltaic-to-idf-object.boundaries.maximum-tilt-default-ratio",
            "Default Ratio PV",
            6.25,
            90,
            225,
            0.2,
            0.7,
            true),
        new(
            "dragon-hvac-photovoltaic-to-idf-object.boundaries.minimum-angles-unit-efficiencies",
            "Unit Boundary PV",
            1,
            0,
            0,
            1,
            1,
            false),
        new(
            "dragon-hvac-photovoltaic-to-idf-object.custom-ratio-nonsquare-area-sqrt",
            "Nonsquare Area PV",
            2,
            37.5,
            123.25,
            0.1875,
            0.625,
            false),
    };

    private static readonly string[] FixtureContextOnlyNotTargeted =
    {
        "PhotoVoltaicPanel",
        "PhotoVoltaicPanel.__init__",
        "PhotoVoltaicPanel.area",
        "PhotoVoltaicPanel.azimuth",
        "PhotoVoltaicPanel.effective_area_ratio",
        "PhotoVoltaicPanel.efficiency",
        "PhotoVoltaicPanel.tilt",
        "IdfObject",
        "IdfObject.__init__",
    };

    private static readonly string[] FixtureUnresolvedBehavior =
    {
        "photovoltaic-constructor-validation-order-and-errors",
        "photovoltaic-property-setter-validation-order-and-errors",
        "invalid-or-nonfinite-domain-state",
        "semantic-populated-and-default-field-parity-requires-csharp-evidence",
        "isolated-IdfObject-validation-policy",
        "EnergyModel.to_idf",
    };

    private static readonly string[] DependencyEvidenceOnly =
    {
        "PhotoVoltaicPanel",
        "PhotoVoltaicPanel.__init__",
        "PhotoVoltaicPanel.area",
        "PhotoVoltaicPanel.azimuth",
        "PhotoVoltaicPanel.effective_area_ratio",
        "PhotoVoltaicPanel.efficiency",
        "PhotoVoltaicPanel.tilt",
        "IdfObject",
        "IdfObject.__init__",
    };

    private static readonly string[] UnresolvedBehavior =
    {
        "photovoltaic-constructor-validation-order-and-errors",
        "photovoltaic-property-setter-validation-order-and-errors",
        "invalid-or-nonfinite-domain-state",
        "isolated-IdfObject-validation-policy",
        "EnergyModel.to_idf",
        "EnergyModel-parent-photovoltaic-ordering-and-failure-transactionality",
    };

    private static readonly DefaultOmissionFact[] ExpectedDefaultOmissions =
    {
        new("Generator:Photovoltaic", 4, "Heat Transfer Integration Mode", "Decoupled", "Decoupled"),
        new("Generator:Photovoltaic", 5, "Number of Series Strings in Parallel", "1.0", "1"),
        new("Generator:Photovoltaic", 6, "Number of Modules in Series", "1.0", "1"),
        new("ElectricLoadCenter:Distribution", 10, "Storage Operation Scheme", "TrackFacilityElectricDemandStoreExcessOnSite", "TrackFacilityElectricDemandStoreExcessOnSite"),
        new("ElectricLoadCenter:Distribution", 13, "Maximum Storage State of Charge Fraction", "1.0", "1.0"),
        new("ElectricLoadCenter:Distribution", 14, "Minimum Storage State of Charge Fraction", "0.0", "0.0"),
    };

    private static readonly SelectedIddTopology[] ExpectedIddTopologies =
    {
        new("Shading:Site", 8, 0, null, 0),
        new("PhotovoltaicPerformance:Simple", 5, 0, null, 0),
        new("Generator:Photovoltaic", 7, 0, null, 0),
        new("ElectricLoadCenter:Generators", 6, 6, 1, 5),
        new("ElectricLoadCenter:Inverter:Simple", 5, 0, null, 0),
        new("ElectricLoadCenter:Distribution", 21, 0, null, 0),
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
    public void MatchesPinnedPythonPhotovoltaicEmissionWithOfficialIddDefaults()
    {
        OfficialIddOracle iddOracle = LoadOfficialIddOracle();
        using JsonDocument oracle = ReadPinnedOracle();
        JsonElement[] cases = ValidateCorpus(oracle.RootElement, iddOracle);
        MethodInfo nativeMethod = ValidateArtifactAndNativeBindings();

        CaseExecution[] executions = cases
            .Select((item, index) => ExecuteNativeCase(
                ExpectedCases[index],
                item.GetProperty("python").GetProperty("facts"),
                iddOracle,
                nativeMethod,
                index))
            .ToArray();
        Assert.Equal(ExpectedCases.Length, executions.Length);
        Assert.All(executions, execution =>
        {
            Assert.Equal(158, execution.Omissions.BlankOrNoneCount);
            Assert.Equal(145, execution.Omissions.ExtensibleGeneratorTailNoneCount);
            Assert.Equal(ExpectedDefaultOmissions, execution.Omissions.Defaults);
        });

        NativeObservation[] observations = executions.Select(item => item.Observation).ToArray();
        DefaultOmissionFact[] defaults = executions[0].Omissions.Defaults;
        var receipt = new
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
                generator = new
                {
                    byte_length = GeneratorByteLength,
                    path = GeneratorRepositoryPath,
                    sha256 = GeneratorSha256,
                },
                idd_default_oracle = new
                {
                    compressed_byte_length = IddOracleByteLength,
                    compressed_sha256 = IddOracleSha256,
                    energyplus_build = EnergyPlusBuild,
                    energyplus_version = EnergyPlusVersion,
                    official_source_byte_length = EnergyPlusIddSourceByteLength,
                    official_source_sha256 = "sha256:" + EnergyPlusIddSourceSha256,
                    oracle_schema = IddOracleSchema,
                    path = IddOracleRepositoryPath,
                },
                python_validator = new
                {
                    byte_length = PythonValidatorByteLength,
                    path = PythonValidatorRepositoryPath,
                    sha256 = PythonValidatorSha256,
                },
            },
            native_binding = new
            {
                adaptation_id = AdaptationId,
                classification = "exception",
                implementation_byte_length = ImplementationByteLength,
                implementation_path = ImplementationRepositoryPath,
                implementation_sha256 = ImplementationSha256,
                implementation_symbol = ImplementationSymbol,
                native_target = NativeTarget,
            },
            observations = observations.Select(item => new
            {
                adaptation_id = AdaptationId,
                case_id = item.CaseId,
                compact_field_counts = item.CompactFieldCounts,
                native_facts = item.NativeFacts,
                native_object_field_values = item.NativeObjectFieldValues,
                native_object_names = item.NativeObjectNames,
                native_object_types = item.NativeObjectTypes,
                native_outcome = "returned",
            }).ToArray(),
            representation = new
            {
                closed_fixture_gap =
                    "semantic-populated-and-default-field-parity-requires-csharp-evidence",
                extensible_generator_tail_none_count = 145,
                native_compact_field_counts = NativeCompactFieldCounts,
                omitted_blank_or_none_count = 158,
                omitted_default_facts = defaults.Select(item => new
                {
                    field_name = item.FieldName,
                    object_type = item.ObjectType,
                    official_idd_default = item.OfficialIddDefault,
                    python_encoded_value = item.PythonEncodedValue,
                    zero_based_position = item.ZeroBasedPosition,
                }).ToArray(),
                omitted_official_default_count = defaults.Length,
                omission_policy = "omit-trailing-blank-and-official-idd-default-fields",
                python_complete_allowed_key_field_counts = PythonCompleteFieldCounts,
            },
            scope = new
            {
                dependency_evidence_only = DependencyEvidenceOnly,
                full_symbol_closure = false,
                scope =
                    "bounded-common-valid-domain-compact-native-photovoltaic-idf-emission-adaptation",
                unresolved_behavior = UnresolvedBehavior,
            },
            upstream = new
            {
                ast_sha256 = UpstreamAstSha256,
                body_hash = UpstreamBodyHash,
                inventory_index = UpstreamInventoryIndex,
                path = UpstreamPath,
                signature_hash = UpstreamSignatureHash,
                source_sha256 = UpstreamSourceSha256,
                symbol = UpstreamSymbol,
                symbol_hash = UpstreamSymbolHash,
            },
        };
        JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
        ValidateReceipt(receiptJson, observations, defaults);
        TrustedEvidenceRecorder.Record(
            AssertionId,
            EvidenceTestCase,
            "not_applicable",
            receipt);
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(OracleRepositoryPath));
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain("\r\n", new UTF8Encoding(false, true).GetString(bytes), StringComparison.Ordinal);
        return JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
    }

    private static JsonElement[] ValidateCorpus(JsonElement root, OfficialIddOracle iddOracle)
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
        AssertNoUnsafeIdentity(root);
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        ValidateUpstream(root.GetProperty("upstream"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateSymbol(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCases.Length, cases.Length);
        string[] identifiers = cases.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), identifiers);
        Assert.Equal(identifiers.OrderBy(item => item, StringComparer.Ordinal), identifiers);
        Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index], iddOracle);
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
        AssertKeys(
            symbol,
            "body_hash",
            "kind",
            "path",
            "signature_hash",
            "symbol",
            "symbol_hash");
        Assert.Equal(UpstreamBodyHash, RequiredString(symbol, "body_hash"));
        Assert.Equal("function", RequiredString(symbol, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
        Assert.Equal(UpstreamSignatureHash, RequiredString(symbol, "signature_hash"));
        Assert.Equal(UpstreamSymbol, RequiredString(symbol, "symbol"));
        Assert.Equal(UpstreamSymbolHash, RequiredString(symbol, "symbol_hash"));
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
        Assert.Equal(ExpectedCases.Length, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(contract.GetProperty("target_symbols"), UpstreamSymbol);
        Assert.Equal("booleans-only-no-id-or-address", RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "external-temporary-copy-with-complete-loaded-local-module-audit",
            RequiredString(contract, "source_import_policy"));

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "context_only_not_targeted",
            "full_symbol_closure",
            "representation_contract",
            "scope",
            "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(
            "bounded-common-valid-domain-compact-native-photovoltaic-idf-emission-adaptation",
            RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("context_only_not_targeted"), FixtureContextOnlyNotTargeted);
        AssertStringArray(closure.GetProperty("unresolved_behavior"), FixtureUnresolvedBehavior);
        JsonElement representation = closure.GetProperty("representation_contract");
        AssertKeys(
            representation,
            "native_compact_field_counts",
            "native_policy",
            "python_complete_allowed_key_field_counts");
        AssertIntArray(representation.GetProperty("native_compact_field_counts"), NativeCompactFieldCounts);
        Assert.Equal(
            "omit-trailing-blank-and-default-fields",
            RequiredString(representation, "native_policy"));
        AssertIntArray(
            representation.GetProperty("python_complete_allowed_key_field_counts"),
            PythonCompleteFieldCounts);
    }

    private static void ValidateCase(
        JsonElement item,
        CaseBinding expected,
        OfficialIddOracle iddOracle)
    {
        AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal("photovoltaic-to-idf-object", RequiredString(item, "executor"));
        Assert.Equal(UpstreamSymbol, RequiredString(item, "symbol"));
        JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(AdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));
        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        AssertKeys(facts, "constructor_context", "emission");
        ValidateConstructorContext(facts.GetProperty("constructor_context"), expected);
        ValidateEmission(facts.GetProperty("emission"), iddOracle);
    }

    private static void ValidateConstructorContext(JsonElement context, CaseBinding expected)
    {
        AssertKeys(
            context,
            "declared_effective_area_ratio_default",
            "explicit_input_identity_preserved",
            "keyword_only_parameters",
            "parameter_order",
            "returned",
            "state",
            "state_unchanged_after_two_emissions",
            "used_default_effective_area_ratio");
        AssertEncodedDouble(context.GetProperty("declared_effective_area_ratio_default"), 0.7);
        Assert.True(context.GetProperty("explicit_input_identity_preserved").GetBoolean());
        AssertStringArray(context.GetProperty("keyword_only_parameters"), "effective_area_ratio");
        AssertStringArray(
            context.GetProperty("parameter_order"),
            "name",
            "area",
            "tilt",
            "azimuth",
            "efficiency",
            "effective_area_ratio");
        Assert.True(context.GetProperty("returned").GetBoolean());
        Assert.True(context.GetProperty("state_unchanged_after_two_emissions").GetBoolean());
        Assert.Equal(
            expected.UsedDefaultEffectiveAreaRatio,
            context.GetProperty("used_default_effective_area_ratio").GetBoolean());
        JsonElement[] state = context.GetProperty("state").EnumerateArray().ToArray();
        Assert.Equal(6, state.Length);
        string[] names = { "name", "area", "tilt", "azimuth", "efficiency", "effective_area_ratio" };
        Assert.Equal(names, state.Select(value => RequiredString(value, "name")));
        Assert.All(state, value => AssertKeys(value, "name", "value"));
        AssertEncodedString(state[0].GetProperty("value"), expected.Name);
        AssertEncodedDouble(state[1].GetProperty("value"), expected.Area);
        AssertEncodedDouble(state[2].GetProperty("value"), expected.Tilt);
        AssertEncodedDouble(state[3].GetProperty("value"), expected.Azimuth);
        AssertEncodedDouble(state[4].GetProperty("value"), expected.Efficiency);
        AssertEncodedDouble(state[5].GetProperty("value"), expected.ActiveCellAreaFraction);
    }

    private static void ValidateEmission(JsonElement emission, OfficialIddOracle iddOracle)
    {
        AssertKeys(
            emission,
            "all_allowed_fields_covered_in_order",
            "first_object_records",
            "first_objects_pairwise_distinct",
            "fresh_idf_object_flags",
            "fresh_result_list",
            "object_count",
            "object_types",
            "result_type",
            "same_idd_definition_flags",
            "second_fields_equal_flags",
            "second_objects_pairwise_distinct");
        Assert.True(emission.GetProperty("all_allowed_fields_covered_in_order").GetBoolean());
        Assert.True(emission.GetProperty("first_objects_pairwise_distinct").GetBoolean());
        Assert.True(emission.GetProperty("fresh_result_list").GetBoolean());
        Assert.Equal(ObjectTypes.Length, emission.GetProperty("object_count").GetInt32());
        AssertStringArray(emission.GetProperty("object_types"), ObjectTypes);
        Assert.Equal("list", RequiredString(emission, "result_type"));
        AssertBooleanArray(emission.GetProperty("fresh_idf_object_flags"), true, true, true, true, true, true);
        AssertBooleanArray(emission.GetProperty("same_idd_definition_flags"), true, true, true, true, true, true);
        AssertBooleanArray(emission.GetProperty("second_fields_equal_flags"), true, true, true, true, true, true);
        Assert.True(emission.GetProperty("second_objects_pairwise_distinct").GetBoolean());

        JsonElement[] records = emission.GetProperty("first_object_records").EnumerateArray().ToArray();
        Assert.Equal(ObjectTypes.Length, records.Length);
        for (int objectIndex = 0; objectIndex < records.Length; objectIndex++)
        {
            JsonElement record = records[objectIndex];
            AssertKeys(record, "field_count", "object_type", "ordered_fields");
            Assert.Equal(ObjectTypes[objectIndex], RequiredString(record, "object_type"));
            Assert.Equal(PythonCompleteFieldCounts[objectIndex], record.GetProperty("field_count").GetInt32());
            JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
            Assert.Equal(PythonCompleteFieldCounts[objectIndex], fields.Length);
            Assert.Equal(fields.Length, fields.Select(value => RequiredString(value, "name")).Distinct(StringComparer.Ordinal).Count());
            OfficialIddObject definition = iddOracle[ObjectTypes[objectIndex]];
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                JsonElement field = fields[fieldIndex];
                AssertKeys(field, "name", "value");
                Assert.Equal(definition.ResolveFieldName(fieldIndex), RequiredString(field, "name"));
                ValidateEncodedValue(field.GetProperty("value"));
            }
        }
    }

    private static MethodInfo ValidateArtifactAndNativeBindings()
    {
        AssertArtifact(GeneratorRepositoryPath, GeneratorByteLength, GeneratorSha256);
        AssertArtifact(PythonValidatorRepositoryPath, PythonValidatorByteLength, PythonValidatorSha256);
        AssertArtifact(ImplementationRepositoryPath, ImplementationByteLength, ImplementationSha256);
        MethodInfo method = Assert.Single(
            typeof(PhotovoltaicPanel).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            item => item.Name == nameof(PhotovoltaicPanel.ToIdfObjects));
        Assert.Equal(typeof(PhotovoltaicPanel), method.DeclaringType);
        Assert.False(method.IsStatic);
        Assert.False(method.IsVirtual);
        Assert.Equal(typeof(IReadOnlyList<IdfObject>), method.ReturnType);
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(IdfGenerationContext), parameter.ParameterType);
        Assert.False(parameter.HasDefaultValue);
        Assert.Equal(ImplementationSymbol, method.DeclaringType!.FullName + "." + method.Name);
        return method;
    }

    private static CaseExecution ExecuteNativeCase(
        CaseBinding binding,
        JsonElement facts,
        OfficialIddOracle iddOracle,
        MethodInfo method,
        int caseIndex)
    {
        JsonElement constructor = facts.GetProperty("constructor_context");
        ValidateConstructorContext(constructor, binding);
        var panel = new PhotovoltaicPanel(
            new EntityId("PV-ORACLE-" + (caseIndex + 1).ToString(CultureInfo.InvariantCulture)),
            binding.Name,
            binding.Area,
            binding.Tilt,
            binding.Azimuth,
            binding.Efficiency,
            binding.ActiveCellAreaFraction);
        PanelSnapshot snapshot = PanelSnapshot.Capture(panel);
        var context = new IdfGenerationContext(iddOracle.Schema);

        IReadOnlyList<IdfObject> first = Invoke(method, panel, context);
        snapshot.AssertUnchanged(panel);
        IReadOnlyList<IdfObject> second = Invoke(method, panel, context);
        snapshot.AssertUnchanged(panel);
        Assert.NotSame(first, second);
        Assert.Equal(ObjectTypes.Length, first.Count);
        Assert.Equal(ObjectTypes.Length, second.Count);
        AssertPairwiseDistinct(first);
        AssertPairwiseDistinct(second);
        for (int index = 0; index < first.Count; index++)
        {
            Assert.NotSame(first[index], second[index]);
            Assert.Same(first[index].Definition, second[index].Definition);
            Assert.Equal(ObjectFingerprint(first[index]), ObjectFingerprint(second[index]));
        }

        JsonElement records = facts.GetProperty("emission").GetProperty("first_object_records");
        OmissionAnalysis firstOmissions = AssertNativeParity(first, records, iddOracle);
        OmissionAnalysis secondOmissions = AssertNativeParity(second, records, iddOracle);
        Assert.Equal(firstOmissions.Defaults, secondOmissions.Defaults);
        Assert.Equal(firstOmissions.BlankOrNoneCount, secondOmissions.BlankOrNoneCount);
        Assert.Equal(
            firstOmissions.ExtensibleGeneratorTailNoneCount,
            secondOmissions.ExtensibleGeneratorTailNoneCount);
        Assert.Equal(ExpectedDefaultOmissions, firstOmissions.Defaults);
        Assert.Equal(158, firstOmissions.BlankOrNoneCount);
        Assert.Equal(145, firstOmissions.ExtensibleGeneratorTailNoneCount);
        AssertLinkage(first);
        AssertGeometryAndRatio(first, binding);

        string[] nativeFacts =
        {
            "native-public-target=PhotovoltaicPanel.ToIdfObjects",
            "object-order=" + string.Join("->", ObjectTypes),
            "python-complete-field-counts=" + string.Join(",", PythonCompleteFieldCounts),
            "native-compact-field-counts=" + string.Join(",", NativeCompactFieldCounts),
            "present-field-semantic-parity=33-of-33-binary-or-exact",
            "omitted-tail-fields=164:blank-or-none=158:official-idd-default=6",
            "official-idd-default-source=EnergyPlus-24.2.0-build-94a887817b",
            "generator-list-extra-extensible-tail=None-count-145",
            "linkage=shade->panel;performance->panel;panel->generator-list->distribution;inverter->distribution",
            "geometry-side=sqrt(area)=" + Format(Math.Sqrt(binding.Area)),
            "active-cell-area-fraction=" + Format(binding.ActiveCellAreaFraction),
            binding.UsedDefaultEffectiveAreaRatio
                ? "upstream-default-effective-area-ratio=0.7-explicit-native-emission-input"
                : "upstream-custom-effective-area-ratio=explicit-native-emission-input",
            "two-call-freshness=distinct-lists-and-twelve-pairwise-distinct-objects",
            "two-call-determinism=all-six-object-fields-binary-identical",
            "native-source-object=reference-and-value-immutable-across-two-emissions",
        };
        Assert.Equal(nativeFacts.Length, nativeFacts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(nativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        var observation = new NativeObservation(
            binding.CaseId,
            first.Select(item => item.ObjectType).ToArray(),
            first.Select(item => item.Count).ToArray(),
            first.Select(item => item.Name ?? string.Empty).ToArray(),
            first.Select(item => item.Fields.Select(field => field.Value).ToArray()).ToArray(),
            nativeFacts);
        return new CaseExecution(observation, firstOmissions);
    }

    private static IReadOnlyList<IdfObject> Invoke(
        MethodInfo method,
        PhotovoltaicPanel panel,
        IdfGenerationContext context)
    {
        object? result = method.Invoke(panel, new object[] { context });
        return Assert.IsAssignableFrom<IReadOnlyList<IdfObject>>(result);
    }

    private static OmissionAnalysis AssertNativeParity(
        IReadOnlyList<IdfObject> nativeObjects,
        JsonElement encodedRecords,
        OfficialIddOracle iddOracle)
    {
        JsonElement[] records = encodedRecords.EnumerateArray().ToArray();
        Assert.Equal(ObjectTypes.Length, nativeObjects.Count);
        Assert.Equal(ObjectTypes.Length, records.Length);
        var defaults = new List<DefaultOmissionFact>();
        int blankOrNone = 0;
        int extensibleGeneratorTailNone = 0;
        int comparedPresent = 0;
        for (int objectIndex = 0; objectIndex < nativeObjects.Count; objectIndex++)
        {
            IdfObject native = nativeObjects[objectIndex];
            JsonElement record = records[objectIndex];
            string objectType = ObjectTypes[objectIndex];
            Assert.Equal(objectType, native.ObjectType);
            Assert.Equal(NativeCompactFieldCounts[objectIndex], native.Count);
            Assert.Equal(PythonCompleteFieldCounts[objectIndex], record.GetProperty("field_count").GetInt32());
            Assert.Same(iddOracle.Schema[objectType], native.Definition);
            JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
            OfficialIddObject official = iddOracle[objectType];
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                JsonElement field = fields[fieldIndex];
                string fieldName = RequiredString(field, "name");
                Assert.Equal(official.ResolveFieldName(fieldIndex), fieldName);
                JsonElement encoded = field.GetProperty("value");
                if (fieldIndex < native.Count)
                {
                    IddFieldDefinition? nativeDefinition = native.Definition!.ResolveField(fieldIndex);
                    Assert.NotNull(nativeDefinition);
                    Assert.Equal(official.ResolvePrototype(fieldIndex).Name, nativeDefinition!.Name);
                    AssertEncodedValueMatchesNative(encoded, native[fieldIndex]);
                    comparedPresent++;
                    continue;
                }

                string kind = RequiredString(encoded, "kind");
                if (kind == "none")
                {
                    blankOrNone++;
                    if (objectType == "ElectricLoadCenter:Generators" && fieldIndex >= 6)
                    {
                        extensibleGeneratorTailNone++;
                    }

                    continue;
                }

                OfficialIddField officialField = official.ResolvePrototype(fieldIndex);
                Assert.False(string.IsNullOrWhiteSpace(officialField.DefaultValue));
                AssertEncodedValueMatchesDefault(encoded, officialField.DefaultValue!);
                defaults.Add(new DefaultOmissionFact(
                    objectType,
                    fieldIndex,
                    fieldName,
                    EncodedDisplay(encoded),
                    officialField.DefaultValue!));
            }
        }

        Assert.Equal(NativeCompactFieldCounts.Sum(), comparedPresent);
        Assert.Equal(PythonCompleteFieldCounts.Sum() - NativeCompactFieldCounts.Sum(), blankOrNone + defaults.Count);
        return new OmissionAnalysis(defaults.ToArray(), blankOrNone, extensibleGeneratorTailNone);
    }

    private static void AssertLinkage(IReadOnlyList<IdfObject> objects)
    {
        Assert.Equal(objects[0][0], objects[2][1]);
        Assert.Equal(objects[1][0], objects[2][3]);
        Assert.Equal("PhotovoltaicPerformance:Simple", objects[2][2]);
        Assert.Equal(objects[2][0], objects[3][1]);
        Assert.Equal("Generator:Photovoltaic", objects[3][2]);
        Assert.Equal(objects[3][0], objects[5][1]);
        Assert.Equal("Baseload", objects[5][2]);
        Assert.Equal("DirectCurrentWithInverter", objects[5][6]);
        Assert.Equal(objects[4][0], objects[5][7]);
        Assert.Equal("ALLON", objects[4][1]);
    }

    private static void AssertGeometryAndRatio(
        IReadOnlyList<IdfObject> objects,
        CaseBinding binding)
    {
        AssertNumericBitsEqual(binding.Azimuth, objects[0][1]);
        AssertNumericBitsEqual(binding.Tilt, objects[0][2]);
        AssertNumericBitsEqual(Math.Sqrt(binding.Area), objects[0][6]);
        AssertNumericBitsEqual(Math.Sqrt(binding.Area), objects[0][7]);
        AssertNumericBitsEqual(binding.ActiveCellAreaFraction, objects[1][1]);
        AssertNumericBitsEqual(binding.Efficiency, objects[1][3]);
        if (binding.UsedDefaultEffectiveAreaRatio)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(0.7), BitConverter.DoubleToInt64Bits(binding.ActiveCellAreaFraction));
        }
    }

    private static OfficialIddOracle LoadOfficialIddOracle()
    {
        byte[] compressedBytes = File.ReadAllBytes(FindRepositoryFile(IddOracleRepositoryPath));
        Assert.Equal(IddOracleByteLength, compressedBytes.Length);
        Assert.Equal(IddOracleSha256, Sha256(compressedBytes));
        using var input = new MemoryStream(compressedBytes, writable: false);
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
        AssertKeys(
            root,
            "energyplus_build",
            "energyplus_version",
            "field_count",
            "groups",
            "object_count",
            "objects",
            "official_epjson_schema",
            "oracle_schema",
            "source_bytes",
            "source_sha256",
            "upstream_commit");
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
        Assert.Equal(Enumerable.Range(0, objects.Length), objects.Select(item => item.GetProperty("position").GetInt32()));
        Assert.Equal(objects.Length, objects.Select(item => RequiredString(item, "name")).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(13_702, objects.Sum(item => item.GetProperty("fields").GetArrayLength()));

        var selected = new List<OfficialIddObject>();
        foreach (string objectType in ObjectTypes)
        {
            JsonElement item = Assert.Single(objects, value => RequiredString(value, "name") == objectType);
            selected.Add(ParseOfficialIddObject(item));
        }

        for (int index = 0; index < selected.Count; index++)
        {
            OfficialIddObject actual = selected[index];
            SelectedIddTopology expected = ExpectedIddTopologies[index];
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.FieldCount, actual.Fields.Length);
            Assert.Equal(expected.MinimumFields, actual.MinimumFields);
            Assert.Equal(expected.ExtensibleStartIndex, actual.ExtensibleStartIndex);
            Assert.Equal(expected.ExtensibleGroupSize, actual.ExtensibleGroupSize);
        }

        IddObjectDefinition[] definitions = selected.Select(item => item.ToDefinition()).ToArray();
        var schema = new IddSchema(
            EnergyPlusVersion,
            EnergyPlusBuild,
            EnergyPlusIddSourceSha256,
            definitions);
        Assert.Equal(EnergyPlusIddSourceSha256, schema.SourceSha256);
        return new OfficialIddOracle(schema, selected);
    }

    private static OfficialIddObject ParseOfficialIddObject(JsonElement item)
    {
        AssertKeys(
            item,
            "additional_directives",
            "extensible_group_size",
            "extensible_start_index",
            "fields",
            "format",
            "group",
            "is_required",
            "is_unique",
            "memo",
            "minimum_fields",
            "name",
            "obsolete_message",
            "position");
        JsonElement[] fields = item.GetProperty("fields").EnumerateArray().ToArray();
        var parsed = new OfficialIddField[fields.Length];
        for (int index = 0; index < fields.Length; index++)
        {
            JsonElement field = fields[index];
            Assert.Equal(index, field.GetProperty("position").GetInt32());
            string kind = RequiredString(field, "kind");
            Assert.True(kind is "alpha" or "numeric");
            JsonElement defaultValue = field.GetProperty("default_value");
            Assert.True(defaultValue.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            parsed[index] = new OfficialIddField(
                RequiredString(field, "token"),
                index,
                kind,
                RequiredString(field, "name"),
                field.GetProperty("begins_extensible").GetBoolean(),
                defaultValue.ValueKind == JsonValueKind.Null ? null : defaultValue.GetString());
        }

        JsonElement start = item.GetProperty("extensible_start_index");
        Assert.True(start.ValueKind is JsonValueKind.Null or JsonValueKind.Number);
        return new OfficialIddObject(
            RequiredString(item, "name"),
            RequiredString(item, "group"),
            item.GetProperty("minimum_fields").GetInt32(),
            start.ValueKind == JsonValueKind.Null ? null : start.GetInt32(),
            item.GetProperty("extensible_group_size").GetInt32(),
            parsed);
    }

    private static void ValidateReceipt(
        JsonElement receipt,
        IReadOnlyList<NativeObservation> observations,
        IReadOnlyList<DefaultOmissionFact> defaults)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertReceiptPayloadSafe(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(receipt, "artifacts", "native_binding", "observations", "representation", "scope", "upstream");

        JsonElement artifacts = receipt.GetProperty("artifacts");
        AssertKeys(artifacts, "fixture", "generator", "idd_default_oracle", "python_validator");
        JsonElement fixture = artifacts.GetProperty("fixture");
        AssertKeys(fixture, "byte_length", "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(OracleByteLength, fixture.GetProperty("byte_length").GetInt32());
        Assert.Equal(ExpectedCases.Length, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));
        AssertReceiptArtifact(artifacts.GetProperty("generator"), GeneratorRepositoryPath, GeneratorByteLength, GeneratorSha256);
        AssertReceiptArtifact(artifacts.GetProperty("python_validator"), PythonValidatorRepositoryPath, PythonValidatorByteLength, PythonValidatorSha256);
        JsonElement idd = artifacts.GetProperty("idd_default_oracle");
        AssertKeys(
            idd,
            "compressed_byte_length",
            "compressed_sha256",
            "energyplus_build",
            "energyplus_version",
            "official_source_byte_length",
            "official_source_sha256",
            "oracle_schema",
            "path");
        Assert.Equal(IddOracleByteLength, idd.GetProperty("compressed_byte_length").GetInt32());
        Assert.Equal(IddOracleSha256, RequiredString(idd, "compressed_sha256"));
        Assert.Equal(EnergyPlusBuild, RequiredString(idd, "energyplus_build"));
        Assert.Equal(EnergyPlusVersion, RequiredString(idd, "energyplus_version"));
        Assert.Equal(EnergyPlusIddSourceByteLength, idd.GetProperty("official_source_byte_length").GetInt32());
        Assert.Equal("sha256:" + EnergyPlusIddSourceSha256, RequiredString(idd, "official_source_sha256"));
        Assert.Equal(IddOracleSchema, RequiredString(idd, "oracle_schema"));
        Assert.Equal(IddOracleRepositoryPath, RequiredString(idd, "path"));

        JsonElement binding = receipt.GetProperty("native_binding");
        AssertKeys(
            binding,
            "adaptation_id",
            "classification",
            "implementation_byte_length",
            "implementation_path",
            "implementation_sha256",
            "implementation_symbol",
            "native_target");
        Assert.Equal(AdaptationId, RequiredString(binding, "adaptation_id"));
        Assert.Equal("exception", RequiredString(binding, "classification"));
        Assert.Equal(ImplementationByteLength, binding.GetProperty("implementation_byte_length").GetInt32());
        Assert.Equal(ImplementationRepositoryPath, RequiredString(binding, "implementation_path"));
        Assert.Equal(ImplementationSha256, RequiredString(binding, "implementation_sha256"));
        Assert.Equal(ImplementationSymbol, RequiredString(binding, "implementation_symbol"));
        Assert.Equal(NativeTarget, RequiredString(binding, "native_target"));

        JsonElement[] recorded = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(observations.Count, recorded.Length);
        for (int index = 0; index < recorded.Length; index++)
        {
            NativeObservation expected = observations[index];
            JsonElement item = recorded[index];
            AssertKeys(
                item,
                "adaptation_id",
                "case_id",
                "compact_field_counts",
                "native_facts",
                "native_object_field_values",
                "native_object_names",
                "native_object_types",
                "native_outcome");
            Assert.Equal(AdaptationId, RequiredString(item, "adaptation_id"));
            Assert.Equal(expected.CaseId, RequiredString(item, "case_id"));
            AssertIntArray(item.GetProperty("compact_field_counts"), expected.CompactFieldCounts);
            AssertStringArray(item.GetProperty("native_facts"), expected.NativeFacts);
            AssertStringMatrix(item.GetProperty("native_object_field_values"), expected.NativeObjectFieldValues);
            AssertStringArray(item.GetProperty("native_object_names"), expected.NativeObjectNames);
            AssertStringArray(item.GetProperty("native_object_types"), expected.NativeObjectTypes);
            Assert.Equal("returned", RequiredString(item, "native_outcome"));
        }

        JsonElement representation = receipt.GetProperty("representation");
        AssertKeys(
            representation,
            "closed_fixture_gap",
            "extensible_generator_tail_none_count",
            "native_compact_field_counts",
            "omitted_blank_or_none_count",
            "omitted_default_facts",
            "omitted_official_default_count",
            "omission_policy",
            "python_complete_allowed_key_field_counts");
        Assert.Equal(
            "semantic-populated-and-default-field-parity-requires-csharp-evidence",
            RequiredString(representation, "closed_fixture_gap"));
        Assert.Equal(145, representation.GetProperty("extensible_generator_tail_none_count").GetInt32());
        AssertIntArray(representation.GetProperty("native_compact_field_counts"), NativeCompactFieldCounts);
        Assert.Equal(158, representation.GetProperty("omitted_blank_or_none_count").GetInt32());
        JsonElement[] defaultFacts = representation.GetProperty("omitted_default_facts").EnumerateArray().ToArray();
        Assert.Equal(defaults.Count, defaultFacts.Length);
        for (int index = 0; index < defaultFacts.Length; index++)
        {
            DefaultOmissionFact expected = defaults[index];
            JsonElement fact = defaultFacts[index];
            AssertKeys(fact, "field_name", "object_type", "official_idd_default", "python_encoded_value", "zero_based_position");
            Assert.Equal(expected.FieldName, RequiredString(fact, "field_name"));
            Assert.Equal(expected.ObjectType, RequiredString(fact, "object_type"));
            Assert.Equal(expected.OfficialIddDefault, RequiredString(fact, "official_idd_default"));
            Assert.Equal(expected.PythonEncodedValue, RequiredString(fact, "python_encoded_value"));
            Assert.Equal(expected.ZeroBasedPosition, fact.GetProperty("zero_based_position").GetInt32());
        }

        Assert.Equal(defaults.Count, representation.GetProperty("omitted_official_default_count").GetInt32());
        Assert.Equal(
            "omit-trailing-blank-and-official-idd-default-fields",
            RequiredString(representation, "omission_policy"));
        AssertIntArray(
            representation.GetProperty("python_complete_allowed_key_field_counts"),
            PythonCompleteFieldCounts);

        JsonElement scope = receipt.GetProperty("scope");
        AssertKeys(scope, "dependency_evidence_only", "full_symbol_closure", "scope", "unresolved_behavior");
        AssertStringArray(scope.GetProperty("dependency_evidence_only"), DependencyEvidenceOnly);
        Assert.False(scope.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(
            "bounded-common-valid-domain-compact-native-photovoltaic-idf-emission-adaptation",
            RequiredString(scope, "scope"));
        AssertStringArray(scope.GetProperty("unresolved_behavior"), UnresolvedBehavior);

        JsonElement upstream = receipt.GetProperty("upstream");
        AssertKeys(
            upstream,
            "ast_sha256",
            "body_hash",
            "inventory_index",
            "path",
            "signature_hash",
            "source_sha256",
            "symbol",
            "symbol_hash");
        Assert.Equal(UpstreamAstSha256, RequiredString(upstream, "ast_sha256"));
        Assert.Equal(UpstreamBodyHash, RequiredString(upstream, "body_hash"));
        Assert.Equal(UpstreamInventoryIndex, upstream.GetProperty("inventory_index").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(UpstreamSignatureHash, RequiredString(upstream, "signature_hash"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(upstream, "source_sha256"));
        Assert.Equal(UpstreamSymbol, RequiredString(upstream, "symbol"));
        Assert.Equal(UpstreamSymbolHash, RequiredString(upstream, "symbol_hash"));
    }

    private static void ValidateEncodedValue(JsonElement value)
    {
        string kind = RequiredString(value, "kind");
        if (kind == "none")
        {
            AssertKeys(value, "kind");
        }
        else if (kind == "str")
        {
            AssertKeys(value, "kind", "value");
            Assert.Equal(JsonValueKind.String, value.GetProperty("value").ValueKind);
        }
        else if (kind == "int")
        {
            AssertKeys(value, "kind", "value");
            Assert.True(long.TryParse(
                RequiredString(value, "value"),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _));
        }
        else
        {
            Assert.Equal("float", kind);
            AssertKeys(value, "hex", "kind", "repr");
            double fromHex = ParsePythonHexDouble(RequiredString(value, "hex"));
            double fromRepr = ParseFiniteDouble(RequiredString(value, "repr"));
            Assert.Equal(BitConverter.DoubleToInt64Bits(fromHex), BitConverter.DoubleToInt64Bits(fromRepr));
        }
    }

    private static void AssertEncodedString(JsonElement value, string expected)
    {
        ValidateEncodedValue(value);
        Assert.Equal("str", RequiredString(value, "kind"));
        Assert.Equal(expected, RequiredString(value, "value"));
    }

    private static void AssertEncodedDouble(JsonElement value, double expected)
    {
        ValidateEncodedValue(value);
        Assert.Equal("float", RequiredString(value, "kind"));
        double actual = ParseFiniteDouble(RequiredString(value, "repr"));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }

    private static void AssertEncodedValueMatchesNative(JsonElement encoded, string actual)
    {
        ValidateEncodedValue(encoded);
        string kind = RequiredString(encoded, "kind");
        if (kind == "none")
        {
            Assert.Equal(string.Empty, actual);
        }
        else if (kind == "str")
        {
            Assert.Equal(RequiredString(encoded, "value"), actual);
        }
        else
        {
            double expected = EncodedNumber(encoded);
            AssertNumericBitsEqual(expected, actual);
            double roundTrip = ParseFiniteDouble(ParseFiniteDouble(actual).ToString("R", CultureInfo.InvariantCulture));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(roundTrip));
        }
    }

    private static void AssertEncodedValueMatchesDefault(JsonElement encoded, string officialDefault)
    {
        ValidateEncodedValue(encoded);
        string kind = RequiredString(encoded, "kind");
        if (kind == "str")
        {
            Assert.Equal(RequiredString(encoded, "value"), officialDefault);
            return;
        }

        Assert.True(kind is "int" or "float");
        double expected = EncodedNumber(encoded);
        double actual = ParseFiniteDouble(officialDefault);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }

    private static double EncodedNumber(JsonElement encoded)
    {
        string kind = RequiredString(encoded, "kind");
        return kind == "int"
            ? long.Parse(RequiredString(encoded, "value"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            : ParseFiniteDouble(RequiredString(encoded, "repr"));
    }

    private static string EncodedDisplay(JsonElement encoded)
    {
        string kind = RequiredString(encoded, "kind");
        return kind switch
        {
            "str" or "int" => RequiredString(encoded, "value"),
            "float" => RequiredString(encoded, "repr"),
            _ => throw new Xunit.Sdk.XunitException("An omitted default cannot be encoded as None."),
        };
    }

    private static double ParsePythonHexDouble(string value)
    {
        Match match = Regex.Match(
            value,
            @"^(?<sign>-?)0[xX](?<whole>[0-9A-Fa-f]+)(?:\.(?<fraction>[0-9A-Fa-f]*))?[pP](?<exponent>[+-]?\d+)$",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        string fraction = match.Groups["fraction"].Value;
        string digits = match.Groups["whole"].Value + fraction;
        ulong significand = ulong.Parse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        int exponent = int.Parse(match.Groups["exponent"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            - (4 * fraction.Length);
        double result = Math.ScaleB((double)significand, exponent);
        return match.Groups["sign"].Value.Length == 0 ? result : -result;
    }

    private static double ParseFiniteDouble(string value)
    {
        Assert.True(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result));
        Assert.True(double.IsFinite(result));
        return result;
    }

    private static void AssertNumericBitsEqual(double expected, string actual)
    {
        double parsed = ParseFiniteDouble(actual);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(parsed));
    }

    private static void AssertPairwiseDistinct(IReadOnlyList<IdfObject> objects)
    {
        for (int left = 0; left < objects.Count; left++)
        {
            for (int right = left + 1; right < objects.Count; right++)
            {
                Assert.NotSame(objects[left], objects[right]);
            }
        }
    }

    private static string ObjectFingerprint(IdfObject value) =>
        value.ObjectType + "|" + string.Join("|", value.Fields.Select(field => field.Value));

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static void AssertArtifact(string path, int byteLength, string sha256)
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(byteLength, bytes.Length);
        Assert.Equal(sha256, Sha256(bytes));
    }

    private static void AssertReceiptArtifact(
        JsonElement artifact,
        string path,
        int byteLength,
        string sha256)
    {
        AssertKeys(artifact, "byte_length", "path", "sha256");
        Assert.Equal(byteLength, artifact.GetProperty("byte_length").GetInt32());
        Assert.Equal(path, RequiredString(artifact, "path"));
        Assert.Equal(sha256, RequiredString(artifact, "sha256"));
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

    private static void AssertIntArray(JsonElement value, params int[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetInt32()));
    }

    private static void AssertBooleanArray(JsonElement value, params bool[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetBoolean()));
    }

    private static void AssertStringMatrix(JsonElement value, IReadOnlyList<string[]> expected)
    {
        JsonElement[] rows = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, rows.Length);
        for (int index = 0; index < rows.Length; index++)
        {
            AssertStringArray(rows[index], expected[index]);
        }
    }

    private static void AssertReceiptPayloadSafe(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.False(property.Name is
                    "consumer_contract" or
                    "expected_dotnet" or
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

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        string raw = value.GetRawText();
        Assert.False(Regex.IsMatch(raw, @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(raw, @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(raw, @"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d", RegexOptions.CultureInvariant));
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

        throw new FileNotFoundException("Could not locate repository file '" + relativePath + "'.");
    }

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record CaseBinding(
        string CaseId,
        string Name,
        double Area,
        double Tilt,
        double Azimuth,
        double Efficiency,
        double ActiveCellAreaFraction,
        bool UsedDefaultEffectiveAreaRatio);

    private sealed record SourceBinding(
        string Module,
        string Path,
        string SourceSha256,
        string AstSha256);

    private sealed record SelectedIddTopology(
        string Name,
        int FieldCount,
        int MinimumFields,
        int? ExtensibleStartIndex,
        int ExtensibleGroupSize);

    private sealed record DefaultOmissionFact(
        string ObjectType,
        int ZeroBasedPosition,
        string FieldName,
        string PythonEncodedValue,
        string OfficialIddDefault);

    private sealed record OmissionAnalysis(
        DefaultOmissionFact[] Defaults,
        int BlankOrNoneCount,
        int ExtensibleGeneratorTailNoneCount);

    private sealed record NativeObservation(
        string CaseId,
        string[] NativeObjectTypes,
        int[] CompactFieldCounts,
        string[] NativeObjectNames,
        string[][] NativeObjectFieldValues,
        string[] NativeFacts);

    private sealed record CaseExecution(
        NativeObservation Observation,
        OmissionAnalysis Omissions);

    private sealed record PanelSnapshot(
        EntityId Id,
        string Name,
        long AreaBits,
        long TiltBits,
        long AzimuthBits,
        long EfficiencyBits,
        long ActiveCellAreaFractionBits)
    {
        public static PanelSnapshot Capture(PhotovoltaicPanel panel) => new(
            panel.Id,
            panel.Name,
            BitConverter.DoubleToInt64Bits(panel.AreaSquareMetres),
            BitConverter.DoubleToInt64Bits(panel.TiltDegrees),
            BitConverter.DoubleToInt64Bits(panel.AzimuthDegrees),
            BitConverter.DoubleToInt64Bits(panel.Efficiency),
            BitConverter.DoubleToInt64Bits(panel.ActiveCellAreaFraction));

        public void AssertUnchanged(PhotovoltaicPanel panel)
        {
            Assert.Same(Id, panel.Id);
            Assert.Equal(Name, panel.Name);
            Assert.Equal(AreaBits, BitConverter.DoubleToInt64Bits(panel.AreaSquareMetres));
            Assert.Equal(TiltBits, BitConverter.DoubleToInt64Bits(panel.TiltDegrees));
            Assert.Equal(AzimuthBits, BitConverter.DoubleToInt64Bits(panel.AzimuthDegrees));
            Assert.Equal(EfficiencyBits, BitConverter.DoubleToInt64Bits(panel.Efficiency));
            Assert.Equal(ActiveCellAreaFractionBits, BitConverter.DoubleToInt64Bits(panel.ActiveCellAreaFraction));
        }
    }

    private sealed record OfficialIddField(
        string Token,
        int Position,
        string Kind,
        string Name,
        bool BeginsExtensible,
        string? DefaultValue)
    {
        public IddFieldDefinition ToDefinition() => new(
            Token,
            Position,
            Kind == "alpha" ? IddFieldKind.Alpha : IddFieldKind.Numeric,
            Name,
            beginsExtensible: BeginsExtensible,
            defaultValue: DefaultValue);
    }

    private sealed record OfficialIddObject(
        string Name,
        string Group,
        int MinimumFields,
        int? ExtensibleStartIndex,
        int ExtensibleGroupSize,
        OfficialIddField[] Fields)
    {
        public OfficialIddField ResolvePrototype(int index)
        {
            if (index < Fields.Length)
            {
                return Fields[index];
            }

            Assert.True(ExtensibleStartIndex is not null && ExtensibleGroupSize > 0);
            int position = ExtensibleStartIndex!.Value
                + ((index - ExtensibleStartIndex.Value) % ExtensibleGroupSize);
            return Fields[position];
        }

        public string ResolveFieldName(int index)
        {
            OfficialIddField prototype = ResolvePrototype(index);
            if (ExtensibleStartIndex is null || index < ExtensibleStartIndex.Value)
            {
                return prototype.Name;
            }

            int groupNumber = ((index - ExtensibleStartIndex.Value) / ExtensibleGroupSize) + 1;
            return Regex.Replace(
                prototype.Name,
                @"\b1\b",
                groupNumber.ToString(CultureInfo.InvariantCulture),
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }

        public IddObjectDefinition ToDefinition() => new(
            Name,
            Group,
            Fields.Select(field => field.ToDefinition()),
            minimumFields: MinimumFields,
            extensibleGroupSize: ExtensibleGroupSize);
    }

    private sealed class OfficialIddOracle
    {
        private readonly IReadOnlyDictionary<string, OfficialIddObject> objects;

        public OfficialIddOracle(IddSchema schema, IEnumerable<OfficialIddObject> objects)
        {
            Schema = schema;
            this.objects = objects.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        public IddSchema Schema { get; }

        public OfficialIddObject this[string objectType] => objects[objectType];
    }
}
