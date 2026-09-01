using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Construction;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class ShadingMaterialIdfOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-shape-shading-material-to-idf-object-oracle.json";
    private const string OracleSchema =
        "dragons.python-reference.dragon-shape-shading-material-to-idf-object.v1";
    private const string OracleSha256 =
        "sha256:e805e1d8e953879012975cd8854e8737da29ed038f66d6176010337ace6f27fe";
    private const string CasesSha256 =
        "sha256:e577eebfb5c6ad65670bc3ae9624d77eec2d2f3e21d0d518c25f78cde2459f92";
    private const int OracleByteLength = 56_704;
    private const int ExpectedCaseCount = 6;
    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamPath = "src/idragon/dragon/shape.py";
    private const string UpstreamSourceSha256 =
        "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c";
    private const string UpstreamAstSha256 =
        "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2";
    private const string OracleAdaptationId =
        "model-context-shading-material-idf-assembly";
    private const string NativeTarget = "EnergyModel.ToIdfDocument";
    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs";
    private const string ImplementationSha256 =
        "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905";
    private const string AppendWindowShadingSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendWindowShading";
    private const string ShadingMaterialSymbol =
        "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.ShadingMaterial";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.ShadingMaterialIdfOracleParityTests.MatchesPinnedPythonShadingMaterialEmissionInNativeModelContext";

    private static readonly string[] BlindFieldNames =
    {
        "Name",
        "Slat Orientation",
        "Slat Width",
        "Slat Separation",
        "Slat Thickness",
        "Slat Angle",
        "Slat Conductivity",
        "Slat Beam Solar Transmittance",
        "Front Side Slat Beam Solar Reflectance",
        "Back Side Slat Beam Solar Reflectance",
        "Slat Diffuse Solar Transmittance",
        "Front Side Slat Diffuse Solar Reflectance",
        "Back Side Slat Diffuse Solar Reflectance",
        "Slat Beam Visible Transmittance",
        "Front Side Slat Beam Visible Reflectance",
        "Back Side Slat Beam Visible Reflectance",
        "Slat Diffuse Visible Transmittance",
        "Front Side Slat Diffuse Visible Reflectance",
        "Back Side Slat Diffuse Visible Reflectance",
        "Slat Infrared Hemispherical Transmittance",
        "Front Side Slat Infrared Hemispherical Emissivity",
        "Back Side Slat Infrared Hemispherical Emissivity",
        "Blind to Glass Distance",
        "Blind Top Opening Multiplier",
        "Blind Bottom Opening Multiplier",
        "Blind Left Side Opening Multiplier",
        "Blind Right Side Opening Multiplier",
        "Minimum Slat Angle",
        "Maximum Slat Angle",
    };

    private static readonly string[] ShadeFieldNames =
    {
        "Name",
        "Solar Transmittance",
        "Solar Reflectance",
        "Visible Transmittance",
        "Visible Reflectance",
        "Infrared Hemispherical Emissivity",
        "Infrared Transmittance",
        "Thickness",
        "Conductivity",
        "Shade to Glass Distance",
        "Top Opening Multiplier",
        "Bottom Opening Multiplier",
        "Left-Side Opening Multiplier",
        "Right-Side Opening Multiplier",
        "Airflow Permeability",
    };

    private static readonly string[] ContextOnlyNotTargeted =
    {
        "Blind",
        "Blind.__init__",
        "Shade",
        "Shade.__init__",
        "Shading",
        "IdfObject",
        "IdfObject.__init__",
        "isolated-IdfObject-validation-policy",
    };

    private static readonly string[] UnresolvedBehavior =
    {
        "standalone-shading-material-converter-API-shape",
        "invalid-or-nonnumeric-state-native-emission",
        "Surface",
        "Surface.blinded_window",
        "Surface.to_idf_object",
        "Window",
        "Window.__init__",
        "WindowShadingControl-emission",
        "EnergyModel.to_idf",
    };

    private static readonly SymbolBinding[] ExpectedSymbols =
    {
        new(
            1027,
            "Blind.to_idf_object",
            "sha256:16e274127d87265296d229708222d131dbf0885a06196f088f42ade37e18b231",
            "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
            "sha256:dbdfe63eb69145e34565287fea0891f7bafaeb23b5a147b7f8d6799a8f6b652b",
            "dragon-shape-blind-to-idf-object-16e27412",
            "model-context-blind-shading-material-emission"),
        new(
            1032,
            "Shade.to_idf_object",
            "sha256:75e6c8e673fc64d8f7966286fd2094b4d958b170903af6413f91b92ce095d66c",
            "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
            "sha256:db351161de65aa88fe02fa9488fdab4ca99c8f8643ff18479fcd91e63de71ef9",
            "dragon-shape-shade-to-idf-object-75e6c8e6",
            "model-context-shade-shading-material-emission"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-shape-shading-material-to-idf-object.blind.alternate-values", "Blind.to_idf_object", "returned"),
        new("dragon-shape-shading-material-to-idf-object.blind.permissive-invalid-state", "Blind.to_idf_object", "constructor-rejected"),
        new("dragon-shape-shading-material-to-idf-object.blind.representative-fields-and-freshness", "Blind.to_idf_object", "returned"),
        new("dragon-shape-shading-material-to-idf-object.shade.alternate-values", "Shade.to_idf_object", "returned"),
        new("dragon-shape-shading-material-to-idf-object.shade.permissive-invalid-and-type-failure", "Shade.to_idf_object", "constructor-rejected"),
        new("dragon-shape-shading-material-to-idf-object.shade.representative-fields-and-freshness", "Shade.to_idf_object", "returned"),
    };

    [Fact]
    public void MatchesPinnedPythonShadingMaterialEmissionInNativeModelContext()
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
        NativeMethods methods = ValidateNativeBindings();
        IddSchema schema = CreateShadingMaterialSchema();

        NativeObservation[] observations = cases
            .Select((item, index) => ExecuteNativeCase(
                ExpectedCases[index],
                item,
                methods,
                schema))
            .ToArray();
        Assert.Equal(ExpectedCaseCount, observations.Length);

        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            NativeObservation[] symbolObservations = observations
                .Where(item => item.Symbol == symbol.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(3, symbolObservations.Length);
            var receipt = new
            {
                closure = new
                {
                    context_only_not_targeted = ContextOnlyNotTargeted,
                    full_symbol_closure = false,
                    scope = "bounded-valid-state-shading-material-emission-with-validation-context",
                    unresolved_behavior = UnresolvedBehavior,
                },
                fixture = new
                {
                    case_count = ExpectedCaseCount,
                    cases_sha256 = CasesSha256,
                    path = OracleRepositoryPath,
                    sha256,
                },
                native_binding = new
                {
                    compatibility_exception_id = symbol.CompatibilityExceptionId,
                    implementation_path = ImplementationRepositoryPath,
                    implementation_sha256 = ImplementationSha256,
                    implementation_symbols = new[]
                    {
                        AppendWindowShadingSymbol,
                        ShadingMaterialSymbol,
                    },
                    oracle_adaptation_id = OracleAdaptationId,
                    public_target = NativeTarget,
                },
                observations = symbolObservations.Select(item => new
                {
                    adaptation_id = item.AdaptationId,
                    case_id = item.CaseId,
                    compatibility_exception_id = item.CompatibilityExceptionId,
                    material_field_names = item.MaterialFieldNames,
                    material_field_values = item.MaterialFieldValues,
                    material_object_type = item.MaterialObjectType,
                    native_facts = item.NativeFacts,
                    native_outcome = item.NativeOutcome,
                }).ToArray(),
                upstream = new
                {
                    inventory_index = symbol.InventoryIndex,
                    path = UpstreamPath,
                    symbol = symbol.Symbol,
                    symbol_hash = symbol.SymbolHash,
                },
            };
            JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
            ValidateReceipt(receiptJson, symbol, symbolObservations);
            TrustedEvidenceRecorder.Record(
                symbol.AssertionId,
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
        ValidateSymbols(root.GetProperty("symbols"));
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
        Assert.Equal(12, sources.Length);
        Assert.Equal(12, modules.Length);
        Assert.Equal(
            sources.Select(item => RequiredString(item, "path")),
            modules.Select(item => RequiredString(item, "path")));
        Assert.Equal(
            sources.Length,
            sources.Select(item => RequiredString(item, "path")).Distinct(StringComparer.Ordinal).Count());

        JsonElement source = Assert.Single(
            sources,
            item => RequiredString(item, "path") == UpstreamPath);
        AssertKeys(source, "ast_sha256", "path", "source_sha256");
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement module = Assert.Single(
            modules,
            item => RequiredString(item, "path") == UpstreamPath);
        AssertKeys(module, "ast_sha256", "module", "path", "source_sha256");
        Assert.Equal("idragon.dragon.shape", RequiredString(module, "module"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(module, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(module, "ast_sha256"));
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
        Assert.Equal(10, runtime.GetProperty("dependencies").EnumerateObject().Count());
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            SymbolBinding expected = ExpectedSymbols[index];
            JsonElement symbol = symbols[index];
            AssertKeys(
                symbol,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
            Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
            Assert.Equal(expected.Symbol, RequiredString(symbol, "symbol"));
            Assert.Equal("function", RequiredString(symbol, "kind"));
            Assert.Equal(expected.SymbolHash, RequiredString(symbol, "symbol_hash"));
            Assert.Equal(expected.SignatureHash, RequiredString(symbol, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(symbol, "body_hash"));
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
            "closure",
            "identity_encoding",
            "native_targets",
            "source_import_policy",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(contract.GetProperty("target_symbols"), ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal("booleans-only-no-id-or-address", RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "external-temporary-copy-with-complete-loaded-local-module-audit",
            RequiredString(contract, "source_import_policy"));

        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement nativeTargets = contract.GetProperty("native_targets");
        AssertKeys(adaptations, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        AssertKeys(assertions, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        AssertKeys(nativeTargets, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            Assert.Equal(OracleAdaptationId, RequiredString(adaptations, symbol.Symbol));
            Assert.Equal(symbol.AssertionId, RequiredString(assertions, symbol.Symbol));
            Assert.Equal("exception", RequiredString(classifications, symbol.Symbol));
            Assert.Equal(NativeTarget, RequiredString(nativeTargets, symbol.Symbol));
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "context_only_not_targeted",
            "full_symbol_closure",
            "scope",
            "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(
            "bounded-valid-state-shading-material-emission-with-validation-context",
            RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("context_only_not_targeted"), ContextOnlyNotTargeted);
        AssertStringArray(closure.GetProperty("unresolved_behavior"), UnresolvedBehavior);
    }

    private static void ValidateCase(JsonElement value, CaseBinding expected)
    {
        AssertKeys(value, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(value, "id"));
        Assert.Equal("shading-material-to-idf-object", RequiredString(value, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(value, "symbol"));

        JsonElement expectedDotnet = value.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(OracleAdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal(expected.ExpectedDotnetOutcome, RequiredString(expectedDotnet, "outcome"));

        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        if (expected.CaseId.EndsWith("blind.permissive-invalid-state", StringComparison.Ordinal))
        {
            AssertKeys(facts, "constructor_context", "emission", "input_conditions");
            ValidateConstructorContext(facts.GetProperty("constructor_context"), expected.Symbol);
            ValidateEmission(facts.GetProperty("emission"), expected.Symbol);
            JsonElement conditions = facts.GetProperty("input_conditions");
            AssertKeys(conditions, "angle_in_native_range", "dimensions_positive", "reflectances_in_unit_interval");
            Assert.True(conditions.GetProperty("angle_in_native_range").GetBoolean());
            Assert.False(conditions.GetProperty("dimensions_positive").GetBoolean());
            Assert.False(conditions.GetProperty("reflectances_in_unit_interval").GetBoolean());
            return;
        }

        if (expected.CaseId.EndsWith("shade.permissive-invalid-and-type-failure", StringComparison.Ordinal))
        {
            AssertKeys(
                facts,
                "nonnumeric_state",
                "nonnumeric_to_idf",
                "numeric_input_conditions",
                "numeric_permissive_emission");
            JsonElement numeric = facts.GetProperty("numeric_permissive_emission");
            AssertKeys(numeric, "constructor_context", "emission");
            ValidateConstructorContext(numeric.GetProperty("constructor_context"), expected.Symbol);
            ValidateEmission(numeric.GetProperty("emission"), expected.Symbol);

            JsonElement conditions = facts.GetProperty("numeric_input_conditions");
            AssertKeys(conditions, "components_in_unit_interval", "sum_not_greater_than_one");
            Assert.True(conditions.GetProperty("components_in_unit_interval").GetBoolean());
            Assert.False(conditions.GetProperty("sum_not_greater_than_one").GetBoolean());

            JsonElement error = facts.GetProperty("nonnumeric_to_idf");
            AssertKeys(error, "args", "message", "outcome", "type");
            Assert.Equal("raised", RequiredString(error, "outcome"));
            Assert.Equal("TypeError", RequiredString(error, "type"));
            Assert.Single(error.GetProperty("args").EnumerateArray());
            Assert.Equal(
                RequiredString(error, "message"),
                error.GetProperty("args")[0].GetString());
            return;
        }

        AssertKeys(facts, "constructor_context", "emission");
        ValidateConstructorContext(facts.GetProperty("constructor_context"), expected.Symbol);
        ValidateEmission(facts.GetProperty("emission"), expected.Symbol);
    }

    private static void ValidateConstructorContext(JsonElement context, string symbol)
    {
        AssertKeys(
            context,
            "input_identity_preserved",
            "parameter_order",
            "returned",
            "state",
            "state_unchanged_after_two_emissions");
        Assert.True(context.GetProperty("input_identity_preserved").GetBoolean());
        Assert.True(context.GetProperty("returned").GetBoolean());
        Assert.True(context.GetProperty("state_unchanged_after_two_emissions").GetBoolean());
        string[] expectedNames = symbol == "Blind.to_idf_object"
            ? new[] { "name", "slat_width", "slat_separation", "slat_angle", "front_reflectance", "back_reflectance" }
            : new[] { "name", "transmittance", "reflectance" };
        AssertStringArray(context.GetProperty("parameter_order"), expectedNames);

        JsonElement[] state = context.GetProperty("state").EnumerateArray().ToArray();
        Assert.Equal(expectedNames.Length, state.Length);
        for (int index = 0; index < state.Length; index++)
        {
            AssertKeys(state[index], "name", "value");
            Assert.Equal(expectedNames[index], RequiredString(state[index], "name"));
            ValidateEncodedValue(state[index].GetProperty("value"));
        }
    }

    private static void ValidateEmission(JsonElement emission, string symbol)
    {
        AssertKeys(
            emission,
            "first_object_type",
            "fresh_idf_object",
            "fresh_result_list",
            "object_count",
            "ordered_fields",
            "result_type",
            "same_idd_definition",
            "second_fields_equal");
        string expectedObjectType = MaterialObjectType(symbol);
        string[] expectedFieldNames = MaterialFieldNames(symbol);
        Assert.Equal(expectedObjectType, RequiredString(emission, "first_object_type"));
        Assert.Equal("list", RequiredString(emission, "result_type"));
        Assert.Equal(1, emission.GetProperty("object_count").GetInt32());
        Assert.True(emission.GetProperty("fresh_idf_object").GetBoolean());
        Assert.True(emission.GetProperty("fresh_result_list").GetBoolean());
        Assert.True(emission.GetProperty("same_idd_definition").GetBoolean());
        Assert.True(emission.GetProperty("second_fields_equal").GetBoolean());

        JsonElement[] fields = emission.GetProperty("ordered_fields").EnumerateArray().ToArray();
        Assert.Equal(expectedFieldNames.Length, fields.Length);
        for (int index = 0; index < fields.Length; index++)
        {
            AssertKeys(fields[index], "name", "value");
            Assert.Equal(expectedFieldNames[index], RequiredString(fields[index], "name"));
            ValidateEncodedValue(fields[index].GetProperty("value"));
        }
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
            Assert.False(string.IsNullOrEmpty(RequiredString(value, "value")));
        }
        else
        {
            Assert.Equal("float", kind);
            AssertKeys(value, "hex", "kind", "repr");
            double number = double.Parse(
                RequiredString(value, "repr"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            Assert.True(double.IsFinite(number));
            Assert.StartsWith(
                number < 0 ? "-0x" : "0x",
                RequiredString(value, "hex"),
                StringComparison.Ordinal);
        }
    }

    private static NativeMethods ValidateNativeBindings()
    {
        Assert.Equal(
            ImplementationSha256,
            Sha256(File.ReadAllBytes(FindRepositoryFile(ImplementationRepositoryPath))));

        MethodInfo? publicTarget = typeof(EnergyModel).GetMethod(
            nameof(EnergyModel.ToIdfDocument),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(IddSchema), typeof(EnergyModelIdfOptions) },
            modifiers: null);
        Assert.NotNull(publicTarget);
        Assert.Equal(typeof(IdfDocument), publicTarget.ReturnType);
        Assert.All(publicTarget.GetParameters(), parameter => Assert.True(parameter.HasDefaultValue));

        Assert.DoesNotContain(
            typeof(Blind).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name is "ToIdfObject" or "to_idf_object");
        Assert.DoesNotContain(
            typeof(Shade).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name is "ToIdfObject" or "to_idf_object");
        Assert.Equal(
            new[] { typeof(string), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double) },
            Assert.Single(typeof(Blind).GetConstructors()).GetParameters().Select(item => item.ParameterType));
        Assert.Equal(
            new[] { typeof(string), typeof(double), typeof(double) },
            Assert.Single(typeof(Shade).GetConstructors()).GetParameters().Select(item => item.ParameterType));

        Type assembler = typeof(EnergyModel).Assembly.GetType(
            "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler",
            throwOnError: true)!;
        MethodInfo appendWindowShading = Assert.Single(
            assembler.GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            method => method.Name == "AppendWindowShading");
        Assert.True(appendWindowShading.IsPrivate);
        Assert.Equal(typeof(void), appendWindowShading.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(IdfDocument),
                typeof(IdfGenerationContext),
                typeof(Zone),
                typeof(Window),
                typeof(Dictionary<string, object>),
            },
            appendWindowShading.GetParameters().Select(item => item.ParameterType));

        MethodInfo shadingMaterial = Assert.Single(
            assembler.GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            method => method.Name == "ShadingMaterial");
        Assert.True(shadingMaterial.IsPrivate);
        Assert.Equal(typeof(IdfObject), shadingMaterial.ReturnType);
        Assert.Equal(
            new[] { typeof(IdfGenerationContext), typeof(IShadingDevice) },
            shadingMaterial.GetParameters().Select(item => item.ParameterType));
        return new NativeMethods(appendWindowShading, shadingMaterial);
    }

    private static NativeObservation ExecuteNativeCase(
        CaseBinding expected,
        JsonElement oracleCase,
        NativeMethods methods,
        IddSchema schema)
    {
        SymbolBinding symbol = Assert.Single(
            ExpectedSymbols,
            item => item.Symbol == expected.Symbol);
        if (expected.ExpectedDotnetOutcome == "constructor-rejected")
        {
            return ObserveNativeConstructorRejection(expected, oracleCase, symbol);
        }

        JsonElement facts = oracleCase.GetProperty("python").GetProperty("facts");
        JsonElement constructorContext = facts.GetProperty("constructor_context");
        JsonElement emission = facts.GetProperty("emission");
        IShadingDevice device = CreateValidDevice(expected.Symbol, constructorContext);
        NativeScenario scenario = CreateScenario(device);
        string before = ScenarioFingerprint(scenario);

        IdfObject firstPrivateMaterial = InvokeShadingMaterial(methods.ShadingMaterial, schema, device);
        IdfObject secondPrivateMaterial = InvokeShadingMaterial(methods.ShadingMaterial, schema, device);
        Assert.NotSame(firstPrivateMaterial, secondPrivateMaterial);

        IdfDocument firstFragment = InvokeAppendWindowShading(methods.AppendWindowShading, schema, scenario);
        IdfDocument secondFragment = InvokeAppendWindowShading(methods.AppendWindowShading, schema, scenario);
        Assert.NotSame(firstFragment, secondFragment);
        IdfObject firstFragmentMaterial = Assert.Single(firstFragment[MaterialObjectType(expected.Symbol)]);
        IdfObject secondFragmentMaterial = Assert.Single(secondFragment[MaterialObjectType(expected.Symbol)]);
        Assert.NotSame(firstFragmentMaterial, secondFragmentMaterial);

        var options = new EnergyModelIdfOptions
        {
            AddIdealLoadsForUnassignedZones = false,
            UseLegacyRectangularFenestration = true,
        };
        IdfDocument firstDocument = scenario.Model.ToIdfDocument(schema, options);
        IdfDocument secondDocument = scenario.Model.ToIdfDocument(schema, options);
        Assert.NotSame(firstDocument, secondDocument);
        Assert.Equal(DocumentFingerprint(firstDocument), DocumentFingerprint(secondDocument));
        IdfObject firstPublicMaterial = Assert.Single(firstDocument[MaterialObjectType(expected.Symbol)]);
        IdfObject secondPublicMaterial = Assert.Single(secondDocument[MaterialObjectType(expected.Symbol)]);
        Assert.NotSame(firstPublicMaterial, secondPublicMaterial);

        IdfObject[] emittedMaterials =
        {
            firstPrivateMaterial,
            secondPrivateMaterial,
            firstFragmentMaterial,
            secondFragmentMaterial,
            firstPublicMaterial,
            secondPublicMaterial,
        };
        foreach (IdfObject material in emittedMaterials)
        {
            AssertMaterialParity(material, emission, expected.Symbol, schema);
        }

        string[] firstValues = firstPublicMaterial.Fields.Select(field => field.Value).ToArray();
        Assert.All(
            emittedMaterials,
            material => Assert.Equal(firstValues, material.Fields.Select(field => field.Value)));
        Assert.Equal(before, ScenarioFingerprint(scenario));
        Assert.Same(device, scenario.Window.Shading);
        Assert.Same(scenario.Window, Assert.Single(scenario.Surface.Openings));

        return new NativeObservation(
            expected.CaseId,
            expected.Symbol,
            OracleAdaptationId,
            symbol.CompatibilityExceptionId,
            "returned",
            MaterialObjectType(expected.Symbol),
            MaterialFieldNames(expected.Symbol),
            firstValues,
            new[]
            {
                "native-public-target:EnergyModel.ToIdfDocument",
                "native-document:fresh-and-deterministic",
                "native-public-material:fresh",
                "native-private-shading-material:fresh",
                "native-append-window-shading:exercised",
                "native-model-state:unchanged",
                "native-full-material-field-order-and-values:exact",
            });
    }

    private static NativeObservation ObserveNativeConstructorRejection(
        CaseBinding expected,
        JsonElement oracleCase,
        SymbolBinding symbol)
    {
        JsonElement facts = oracleCase.GetProperty("python").GetProperty("facts");
        string exceptionType;
        if (expected.Symbol == "Blind.to_idf_object")
        {
            JsonElement context = facts.GetProperty("constructor_context");
            ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Blind(
                    StateString(context, "name"),
                    StateDouble(context, "slat_width"),
                    StateDouble(context, "slat_separation"),
                    StateDouble(context, "slat_angle"),
                    StateDouble(context, "front_reflectance"),
                    StateDouble(context, "back_reflectance")));
            Assert.Equal("slatWidthMetres", error.ParamName);
            exceptionType = error.GetType().Name;
        }
        else
        {
            JsonElement context = facts
                .GetProperty("numeric_permissive_emission")
                .GetProperty("constructor_context");
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                new Shade(
                    StateString(context, "name"),
                    StateDouble(context, "transmittance"),
                    StateDouble(context, "reflectance")));
            Assert.Null(error.ParamName);
            exceptionType = error.GetType().Name;
        }

        return new NativeObservation(
            expected.CaseId,
            expected.Symbol,
            OracleAdaptationId,
            symbol.CompatibilityExceptionId,
            "constructor-rejected",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                "native-adaptation:validated-constructor-rejection",
                "native-exception:" + exceptionType,
                "native-emission-attempted:false",
                "invalid-emission-parity-claimed:false",
                "constructor-symbol-closure-claimed:false",
            });
    }

    private static IShadingDevice CreateValidDevice(string symbol, JsonElement context)
    {
        if (symbol == "Blind.to_idf_object")
        {
            return new Blind(
                StateString(context, "name"),
                StateDouble(context, "slat_width"),
                StateDouble(context, "slat_separation"),
                StateDouble(context, "slat_angle"),
                StateDouble(context, "front_reflectance"),
                StateDouble(context, "back_reflectance"));
        }

        return new Shade(
            StateString(context, "name"),
            StateDouble(context, "transmittance"),
            StateDouble(context, "reflectance"));
    }

    private static string StateString(JsonElement context, string name)
    {
        JsonElement value = StateValue(context, name);
        Assert.Equal("str", RequiredString(value, "kind"));
        return RequiredString(value, "value");
    }

    private static double StateDouble(JsonElement context, string name)
    {
        JsonElement value = StateValue(context, name);
        Assert.Equal("float", RequiredString(value, "kind"));
        return double.Parse(
            RequiredString(value, "repr"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
    }

    private static JsonElement StateValue(JsonElement context, string name)
    {
        JsonElement state = Assert.Single(
            context.GetProperty("state").EnumerateArray(),
            item => RequiredString(item, "name") == name);
        return state.GetProperty("value");
    }

    private static IdfObject InvokeShadingMaterial(
        MethodInfo method,
        IddSchema schema,
        IShadingDevice device)
    {
        object? result = method.Invoke(
            null,
            new object[] { new IdfGenerationContext(schema), device });
        return Assert.IsType<IdfObject>(result);
    }

    private static IdfDocument InvokeAppendWindowShading(
        MethodInfo method,
        IddSchema schema,
        NativeScenario scenario)
    {
        var document = new IdfDocument(schema);
        var definitions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        object? result = method.Invoke(
            null,
            new object[]
            {
                document,
                new IdfGenerationContext(schema),
                scenario.Zone,
                scenario.Window,
                definitions,
            });
        Assert.Null(result);
        Assert.Single(definitions);
        Assert.Same(scenario.Window.Shading, definitions[scenario.Window.Shading!.Name]);
        return document;
    }

    private static void AssertMaterialParity(
        IdfObject material,
        JsonElement emission,
        string symbol,
        IddSchema schema)
    {
        string objectType = MaterialObjectType(symbol);
        string[] fieldNames = MaterialFieldNames(symbol);
        Assert.Equal(objectType, material.ObjectType);
        Assert.Same(schema[objectType], material.Definition);
        Assert.Equal(fieldNames.Length, material.Count);

        JsonElement[] expectedFields = emission.GetProperty("ordered_fields").EnumerateArray().ToArray();
        Assert.Equal(fieldNames.Length, expectedFields.Length);
        for (int index = 0; index < fieldNames.Length; index++)
        {
            Assert.Equal(fieldNames[index], RequiredString(expectedFields[index], "name"));
            Assert.Equal(fieldNames[index], material.Definition!.ResolveField(index)!.Name);
            JsonElement expectedValue = expectedFields[index].GetProperty("value");
            string kind = RequiredString(expectedValue, "kind");
            if (kind == "none")
            {
                Assert.Equal(string.Empty, material[index]);
            }
            else if (kind == "str")
            {
                Assert.Equal(RequiredString(expectedValue, "value"), material[index]);
            }
            else
            {
                Assert.Equal("float", kind);
                double expectedNumber = double.Parse(
                    RequiredString(expectedValue, "repr"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                double actualNumber = double.Parse(
                    material[index],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(expectedNumber),
                    BitConverter.DoubleToInt64Bits(actualNumber));
            }
        }
    }

    private static IddSchema CreateShadingMaterialSchema()
    {
        return new IddSchema(
            "24.2",
            "bounded shading oracle",
            new string('0', 64),
            new[]
            {
                CreateMaterialDefinition("WindowMaterial:Blind", BlindFieldNames),
                CreateMaterialDefinition("WindowMaterial:Shade", ShadeFieldNames),
            });
    }

    private static IddObjectDefinition CreateMaterialDefinition(
        string objectType,
        IReadOnlyList<string> fieldNames)
    {
        return new IddObjectDefinition(
            objectType,
            "Window Material",
            fieldNames.Select((name, index) => new IddFieldDefinition(
                index == 0 ? "A1" : "N" + index.ToString(CultureInfo.InvariantCulture),
                index,
                index == 0 ? IddFieldKind.Alpha : IddFieldKind.Numeric,
                name)),
            minimumFields: fieldNames.Count);
    }

    private static NativeScenario CreateScenario(IShadingDevice shading)
    {
        var window = new Window(
            new EntityId("SHADING-ORACLE-WINDOW"),
            "Shading Oracle Window",
            new Glazing("Shading Oracle Glazing", 1.5, 0.42),
            VerticalRectangle(1, 2, 0.75, 1.5),
            shading);
        var surface = new Surface(
            new EntityId("SHADING-ORACLE-SURFACE"),
            "Shading Oracle Surface",
            SurfaceType.Wall,
            new NoMassConstruction("Shading Oracle Wall", 0.5),
            SurfaceBoundary.Outdoors,
            VerticalRectangle(0, 6, 0, 3),
            new[] { window });
        var zone = new Zone(
            new EntityId("SHADING-ORACLE-ZONE"),
            "Shading Oracle Zone",
            new[] { surface },
            TestDomainFactory.EmptyProfile());
        var model = new EnergyModel("Shading material oracle", new[] { zone });
        Assert.True(model.Validate().IsValid);
        return new NativeScenario(model, zone, surface, window);
    }

    private static PlanarPolygon VerticalRectangle(
        double x,
        double width,
        double z,
        double height) => new(new[]
        {
            new Vertex(x, 0, z),
            new Vertex(x + width, 0, z),
            new Vertex(x + width, 0, z + height),
            new Vertex(x, 0, z + height),
        });

    private static string ScenarioFingerprint(NativeScenario scenario)
    {
        IShadingDevice shading = scenario.Window.Shading!;
        string device = shading switch
        {
            Blind blind => string.Join(
                ":",
                blind.Name,
                Format(blind.SlatWidthMetres),
                Format(blind.SlatSeparationMetres),
                Format(blind.SlatAngleDegrees),
                Format(blind.FrontReflectance),
                Format(blind.BackReflectance)),
            Shade shade => string.Join(
                ":",
                shade.Name,
                Format(shade.Transmittance),
                Format(shade.Reflectance),
                Format(shade.Emissivity)),
            _ => throw new Xunit.Sdk.XunitException("Unexpected shading device."),
        };
        return string.Join(
            "|",
            scenario.Model.Name,
            scenario.Zone.Id,
            scenario.Zone.Name,
            scenario.Surface.Id,
            scenario.Surface.Name,
            scenario.Window.Id,
            scenario.Window.Name,
            device);
    }

    private static string DocumentFingerprint(IdfDocument document)
    {
        return string.Join(
            "\n",
            document.Select(item =>
                item.ObjectType + "|" + string.Join("|", item.Fields.Select(field => field.Value))));
    }

    private static string Format(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string MaterialObjectType(string symbol) =>
        symbol == "Blind.to_idf_object"
            ? "WindowMaterial:Blind"
            : "WindowMaterial:Shade";

    private static string[] MaterialFieldNames(string symbol) =>
        symbol == "Blind.to_idf_object"
            ? BlindFieldNames
            : ShadeFieldNames;

    private static void ValidateReceipt(
        JsonElement receipt,
        SymbolBinding symbol,
        IReadOnlyList<NativeObservation> expectedObservations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertReceiptPayloadSafe(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(receipt, "closure", "fixture", "native_binding", "observations", "upstream");

        JsonElement closure = receipt.GetProperty("closure");
        AssertKeys(
            closure,
            "context_only_not_targeted",
            "full_symbol_closure",
            "scope",
            "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(
            "bounded-valid-state-shading-material-emission-with-validation-context",
            RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("context_only_not_targeted"), ContextOnlyNotTargeted);
        AssertStringArray(closure.GetProperty("unresolved_behavior"), UnresolvedBehavior);

        JsonElement fixture = receipt.GetProperty("fixture");
        AssertKeys(fixture, "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(ExpectedCaseCount, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));

        JsonElement binding = receipt.GetProperty("native_binding");
        AssertKeys(
            binding,
            "compatibility_exception_id",
            "implementation_path",
            "implementation_sha256",
            "implementation_symbols",
            "oracle_adaptation_id",
            "public_target");
        Assert.Equal(symbol.CompatibilityExceptionId, RequiredString(binding, "compatibility_exception_id"));
        Assert.Equal(ImplementationRepositoryPath, RequiredString(binding, "implementation_path"));
        Assert.Equal(ImplementationSha256, RequiredString(binding, "implementation_sha256"));
        AssertStringArray(binding.GetProperty("implementation_symbols"), AppendWindowShadingSymbol, ShadingMaterialSymbol);
        Assert.Equal(OracleAdaptationId, RequiredString(binding, "oracle_adaptation_id"));
        Assert.Equal(NativeTarget, RequiredString(binding, "public_target"));

        JsonElement upstream = receipt.GetProperty("upstream");
        AssertKeys(upstream, "inventory_index", "path", "symbol", "symbol_hash");
        Assert.Equal(symbol.InventoryIndex, upstream.GetProperty("inventory_index").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(symbol.Symbol, RequiredString(upstream, "symbol"));
        Assert.Equal(symbol.SymbolHash, RequiredString(upstream, "symbol_hash"));

        JsonElement[] observations = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(expectedObservations.Count, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            NativeObservation expected = expectedObservations[index];
            JsonElement observation = observations[index];
            AssertKeys(
                observation,
                "adaptation_id",
                "case_id",
                "compatibility_exception_id",
                "material_field_names",
                "material_field_values",
                "material_object_type",
                "native_facts",
                "native_outcome");
            Assert.Equal(expected.AdaptationId, RequiredString(observation, "adaptation_id"));
            Assert.Equal(expected.CaseId, RequiredString(observation, "case_id"));
            Assert.Equal(expected.CompatibilityExceptionId, RequiredString(observation, "compatibility_exception_id"));
            Assert.Equal(expected.NativeOutcome, RequiredString(observation, "native_outcome"));
            AssertStringArray(observation.GetProperty("material_field_names"), expected.MaterialFieldNames);
            AssertStringArray(observation.GetProperty("material_field_values"), expected.MaterialFieldValues);
            AssertStringArray(observation.GetProperty("native_facts"), expected.NativeFacts);
            if (expected.MaterialObjectType is null)
            {
                Assert.Equal(JsonValueKind.Null, observation.GetProperty("material_object_type").ValueKind);
            }
            else
            {
                Assert.Equal(expected.MaterialObjectType, RequiredString(observation, "material_object_type"));
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
        Assert.False(Regex.IsMatch(
            value,
            @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])",
            RegexOptions.CultureInvariant));
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            Assert.False(Regex.IsMatch(
                value.GetString()!,
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

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
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

    private sealed record CaseBinding(
        string CaseId,
        string Symbol,
        string ExpectedDotnetOutcome);

    private sealed record SymbolBinding(
        int InventoryIndex,
        string Symbol,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string CompatibilityExceptionId);

    private sealed record NativeMethods(
        MethodInfo AppendWindowShading,
        MethodInfo ShadingMaterial);

    private sealed record NativeScenario(
        EnergyModel Model,
        Zone Zone,
        Surface Surface,
        Window Window);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string AdaptationId,
        string CompatibilityExceptionId,
        string NativeOutcome,
        string? MaterialObjectType,
        string[] MaterialFieldNames,
        string[] MaterialFieldValues,
        string[] NativeFacts);
}
