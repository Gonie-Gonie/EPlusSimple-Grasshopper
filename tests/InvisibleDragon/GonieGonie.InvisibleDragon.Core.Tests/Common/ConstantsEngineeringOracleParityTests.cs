using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.UpstreamTracker;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace GonieGonie.InvisibleDragon.Tests.Common;

public sealed class ConstantsEngineeringOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/constants-engineering-oracle.json";
    private const string OracleSha256 =
        "sha256:e5261b2898a374722c24247f7d5a4fbc7df83cab1fbe8ad225827ee170d5cf54";
    private const string CasesSha256 =
        "sha256:18cc2d2295cad8a96a1a54ebd726c9d258586cd5f44a46c401fcb2f87997050e";
    private const int OracleByteLength = 20_889;
    private const int ExpectedCaseCount = 24;
    private const string OracleSchema =
        "goniegonie.python-reference.constants-engineering.v1";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Common.ConstantsEngineeringOracleParityTests.MatchesPinnedPythonConstantsEngineering";
    private const string UpstreamPath = "src/idragon/constants.py";
    private const string UnitTypeName =
        "GonieGonie.InvisibleDragon.UnitConversions";
    private const string ThermalTypeName =
        "GonieGonie.InvisibleDragon.ThermalDefaults";

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/constants.py", "THERMAL", "sha256:c55d90e3a5f7120226dc556d856b18c8070aac02531b2632e56ee15f8d8dcdcd", "constants-engineering-thermal-c55d90e3"),
        new("src/idragon/constants.py", "THERMAL.PEOPLE_ACTIVITY_LEVEL", "sha256:5a39d884ca1bdfa92fe0568bc4b11f8164ed3b50ed783378becec0c18147d946", "constants-engineering-thermal-people-activity-5a39d884"),
        new("src/idragon/constants.py", "Unit", "sha256:82eeceb9e427512d5ed45c6139c5fb92859289547ded26e7e410b3be3f591b70", "constants-engineering-unit-82eeceb9"),
        new("src/idragon/constants.py", "Unit.L2M3", "sha256:91d7c58294dae00c815dbf158fb57990500db567405a7b2c31350eef60ea7102", "constants-engineering-unit-l2m3-91d7c582"),
        new("src/idragon/constants.py", "Unit.MM2M", "sha256:4f90e5dec4746b485bf2d2b35f73b00ca8b742d8ca1babed858dc04fddc01e69", "constants-engineering-unit-mm2m-4f90e5de"),
        new("src/idragon/constants.py", "Unit.NONE2PRC", "sha256:743aa08ade92de4311700e7e29b0bcfd084735a36520906bec9e74acd373c31a", "constants-engineering-unit-none2prc-743aa08a"),
        new("src/idragon/constants.py", "Unit.PRC2NONE", "sha256:48e9d7619e573e8c55d44bbd640558260c077144a4b24fe384a91b7c433e6306", "constants-engineering-unit-prc2none-48e9d761"),
        new("src/idragon/constants.py", "Unit.W2KW", "sha256:f00a14847f11df61238d82b56c9a31ecc8453877c7bda1eb12fbe13573f0f3eb", "constants-engineering-unit-w2kw-f00a1484"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("THERMAL", "class", "sha256:1a8e65ce71d37c495d404d7e8379dc1e3007bea81f99cca0d6c39c13f281d902", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "exception", "native-thermal-default-constant-container", ThermalTypeName, null, null),
        new("THERMAL.PEOPLE_ACTIVITY_LEVEL", "constant", "sha256:6987d99c6d345cbd8d6ff4397ca43194b04fe907b89d9422c7972c5a0a501d74", "sha256:b33ef9739f6bd8533418c2d2c199e209601c5aa7111e178afb610494d4ea2696", "equivalent", null, ThermalTypeName + ".PeopleActivityLevelWattsPerPerson", 107d, 0x405A_C000_0000_0000L),
        new("Unit", "class", "sha256:4207679fe2ede1a951b1882e62a22d8d915b1442dc5d1e1f62925d16cb6422e0", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "exception", "native-named-unit-conversion-constants", UnitTypeName, null, null),
        new("Unit.L2M3", "constant", "sha256:d4f677f2c249499bd341314182b551f8f784d9d00f8df315ddb9f1d3fec321e6", "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf", "equivalent", null, UnitTypeName + ".LitresToCubicMetres", 0.001d, 0x3F50_624D_D2F1_A9FCL),
        new("Unit.MM2M", "constant", "sha256:6c5322fba5eeccac01411c863db5421b2ed98765a307fea5b69e2f6878f511ff", "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf", "equivalent", null, UnitTypeName + ".MillimetresToMetres", 0.001d, 0x3F50_624D_D2F1_A9FCL),
        new("Unit.NONE2PRC", "constant", "sha256:c28ce3b1d369b3c8be93fbedc29a75951029b8485d3b7885f43e46eb817efdb1", "sha256:d3c3cec052dae85942a722526911012da69bf59aca87bc1229bfbc27211abdd1", "equivalent", null, UnitTypeName + ".FractionToPercent", 100d, 0x4059_0000_0000_0000L),
        new("Unit.PRC2NONE", "constant", "sha256:de430edab58a6cacc63b7c0d76b68d49302e7dc3217bd6b45da3db4369a05219", "sha256:d2dff8ba2e3305a55a5cfcb7f170272f46ce3773420fc2094c6eb318b178a722", "equivalent", null, UnitTypeName + ".PercentToFraction", 0.01d, 0x3F84_7AE1_47AE_147BL),
        new("Unit.W2KW", "constant", "sha256:f9130ac841cd7644647450db8a07fc69eaa4ace7594cd4f0ebb0ed6af610dbf8", "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf", "equivalent", null, UnitTypeName + ".WattsToKilowatts", 0.001d, 0x3F50_624D_D2F1_A9FCL),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("constants-engineering.thermal.class.construction", "thermal-class", "THERMAL"),
        new("constants-engineering.thermal.class.member-topology", "thermal-class", "THERMAL"),
        new("constants-engineering.thermal.class.type-topology", "thermal-class", "THERMAL"),
        new("constants-engineering.thermal.people-activity-level.idf-default", "thermal-constant", "THERMAL.PEOPLE_ACTIVITY_LEVEL"),
        new("constants-engineering.thermal.people-activity-level.numeric-semantics", "thermal-constant", "THERMAL.PEOPLE_ACTIVITY_LEVEL"),
        new("constants-engineering.thermal.people-activity-level.value", "thermal-constant", "THERMAL.PEOPLE_ACTIVITY_LEVEL"),
        new("constants-engineering.unit.class.alias-topology", "unit-class", "Unit"),
        new("constants-engineering.unit.class.member-order", "unit-class", "Unit"),
        new("constants-engineering.unit.class.type-topology", "unit-class", "Unit"),
        new("constants-engineering.unit.l2m3.engineering-probe", "unit-constant", "Unit.L2M3"),
        new("constants-engineering.unit.l2m3.numeric-semantics", "unit-constant", "Unit.L2M3"),
        new("constants-engineering.unit.l2m3.value", "unit-constant", "Unit.L2M3"),
        new("constants-engineering.unit.mm2m.engineering-probe", "unit-constant", "Unit.MM2M"),
        new("constants-engineering.unit.mm2m.numeric-semantics", "unit-constant", "Unit.MM2M"),
        new("constants-engineering.unit.mm2m.value", "unit-constant", "Unit.MM2M"),
        new("constants-engineering.unit.none2prc.engineering-probe", "unit-constant", "Unit.NONE2PRC"),
        new("constants-engineering.unit.none2prc.numeric-semantics", "unit-constant", "Unit.NONE2PRC"),
        new("constants-engineering.unit.none2prc.value", "unit-constant", "Unit.NONE2PRC"),
        new("constants-engineering.unit.prc2none.engineering-probe", "unit-constant", "Unit.PRC2NONE"),
        new("constants-engineering.unit.prc2none.numeric-semantics", "unit-constant", "Unit.PRC2NONE"),
        new("constants-engineering.unit.prc2none.value", "unit-constant", "Unit.PRC2NONE"),
        new("constants-engineering.unit.w2kw.engineering-probe", "unit-constant", "Unit.W2KW"),
        new("constants-engineering.unit.w2kw.numeric-semantics", "unit-constant", "Unit.W2KW"),
        new("constants-engineering.unit.w2kw.value", "unit-constant", "Unit.W2KW"),
    };

    [Fact]
    public void MatchesPinnedPythonConstantsEngineering()
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

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal(
            "847b01f68f438f560a986072bcaa7768fbf67897",
            RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0",
            RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(
            "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520",
            RequiredString(upstream, "source_sha256"));

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
            ExpectedSymbols.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            cases.GroupBy(item => RequiredString(item, "symbol"))
                .Select(group => group.Key)
                .OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(
            cases.GroupBy(item => RequiredString(item, "symbol")),
            group => Assert.Equal(3, group.Count()));

        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
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
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));

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
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => item.Symbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < ExpectedEvidence.Length; index++)
        {
            EvidenceBinding evidence = ExpectedEvidence[index];
            Assert.Equal(UpstreamPath, evidence.Path);
            Assert.Equal(ExpectedSymbols[index].Symbol, evidence.Symbol);
            Assert.StartsWith("sha256:", evidence.SymbolHash, StringComparison.Ordinal);
            Assert.EndsWith(
                evidence.SymbolHash.Substring("sha256:".Length, 8),
                evidence.AssertionId,
                StringComparison.Ordinal);
        }
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            JsonElement item = symbols[index];
            SymbolContract symbol = ExpectedSymbols[index];
            EvidenceBinding evidence = ExpectedEvidence[index];
            AssertKeys(
                item,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(symbol.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(symbol.Kind, RequiredString(item, "kind"));
            Assert.Equal(symbol.SignatureHash, RequiredString(item, "signature_hash"));
            Assert.Equal(symbol.BodyHash, RequiredString(item, "body_hash"));
            Assert.Equal(evidence.SymbolHash, RequiredString(item, "symbol_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement consumer)
    {
        AssertKeys(
            consumer,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classifications",
            "float_encoding",
            "runtime_names",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, consumer.GetProperty("case_count").GetInt32());
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId),
            consumer.GetProperty("case_ids").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(
            ExpectedSymbols.Select(item => item.Symbol),
            consumer.GetProperty("target_symbols").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(
            "python-binary64-hex-without-0x-prefix",
            RequiredString(consumer, "float_encoding"));
        Assert.Equal(
            "pinned-python-only-no-native-type-name-claims",
            RequiredString(consumer, "runtime_names"));

        JsonElement classifications = consumer.GetProperty("classifications");
        JsonElement adaptations = consumer.GetProperty("adaptations");
        JsonElement assertionIds = consumer.GetProperty("assertion_ids");
        string[] symbolNames = ExpectedSymbols.Select(item => item.Symbol).ToArray();
        AssertKeys(classifications, symbolNames);
        AssertKeys(
            adaptations,
            ExpectedSymbols.Where(item => item.AdaptationId is not null)
                .Select(item => item.Symbol)
                .ToArray());
        AssertKeys(assertionIds, symbolNames);
        Assert.Equal(6, ExpectedSymbols.Count(item => item.Classification == "equivalent"));
        Assert.Equal(2, ExpectedSymbols.Count(item => item.Classification == "exception"));
        Assert.Equal(2, ExpectedSymbols.Count(item => item.AdaptationId is not null));
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            SymbolContract symbol = ExpectedSymbols[index];
            Assert.Equal(symbol.Classification, RequiredString(classifications, symbol.Symbol));
            Assert.Equal(
                ExpectedEvidence[index].AssertionId,
                RequiredString(assertionIds, symbol.Symbol));
            if (symbol.AdaptationId is not null)
            {
                Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
            }
            else
            {
                Assert.False(adaptations.TryGetProperty(symbol.Symbol, out _));
            }
        }
    }

    private static void ValidateCase(JsonElement value, CaseBinding binding)
    {
        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == binding.Symbol);
        if (symbol.AdaptationId is null)
        {
            AssertKeys(value, "executor", "id", "python", "symbol");
            Assert.False(value.TryGetProperty("expected_dotnet", out _));
        }
        else
        {
            AssertKeys(value, "executor", "expected_dotnet", "id", "python", "symbol");
            JsonElement expectedDotNet = value.GetProperty("expected_dotnet");
            AssertKeys(expectedDotNet, "adaptation", "outcome");
            Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotNet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotNet, "outcome"));
        }

        Assert.Equal(binding.CaseId, RequiredString(value, "id"));
        Assert.Equal(binding.Executor, RequiredString(value, "executor"));
        Assert.Equal(binding.Symbol, RequiredString(value, "symbol"));
        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        Assert.Equal(JsonValueKind.Object, python.GetProperty("facts").ValueKind);
        ValidateTaggedScalarsRecursive(python.GetProperty("facts"));
    }

    private static void ValidateNativeBindings()
    {
        ValidateStaticContainer(
            typeof(UnitConversions),
            UnitTypeName,
            nameof(UnitConversions.FractionToPercent),
            nameof(UnitConversions.LitresToCubicMetres),
            nameof(UnitConversions.MillimetresToMetres),
            nameof(UnitConversions.PercentToFraction),
            nameof(UnitConversions.WattsToKilowatts));
        ValidateStaticContainer(
            typeof(ThermalDefaults),
            ThermalTypeName,
            nameof(ThermalDefaults.PeopleActivityLevelWattsPerPerson));

        Assert.Equal(UnitTypeName, ExpectedSymbols[2].NativeTarget);
        Assert.Equal(ThermalTypeName, ExpectedSymbols[0].NativeTarget);
        foreach (SymbolContract symbol in ExpectedSymbols.Where(item => item.ExpectedValue is not null))
        {
            FieldInfo field = NativeField(symbol);
            Assert.Equal(symbol.NativeTarget, field.DeclaringType!.FullName + "." + field.Name);
            double value = Assert.IsType<double>(field.GetRawConstantValue());
            Assert.Equal(symbol.ExpectedValue!.Value, value);
            Assert.Equal(symbol.ExpectedBits!.Value, BitConverter.DoubleToInt64Bits(value));
        }

        Assert.Equal(UnitConversions.MillimetresToMetres, UnitConversions.WattsToKilowatts);
        Assert.Equal(UnitConversions.MillimetresToMetres, UnitConversions.LitresToCubicMetres);
    }

    private static void ValidateStaticContainer(
        Type type,
        string expectedFullName,
        params string[] expectedFields)
    {
        Assert.Equal(expectedFullName, type.FullName);
        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract);
        Assert.True(type.IsSealed);
        Assert.False(type.IsEnum);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(
            expectedFields.OrderBy(item => item, StringComparer.Ordinal),
            fields.Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(fields, field =>
        {
            Assert.True(field.IsPublic);
            Assert.True(field.IsStatic);
            Assert.True(field.IsLiteral);
            Assert.False(field.IsInitOnly);
            Assert.Equal(typeof(double), field.FieldType);
        });
    }

    private static string[] ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Executor switch
        {
            "thermal-class" => ExecuteThermalClass(binding.CaseId, pythonFacts),
            "thermal-constant" => ExecuteThermalConstant(binding.CaseId, pythonFacts),
            "unit-class" => ExecuteUnitClass(binding.CaseId, pythonFacts),
            "unit-constant" => ExecuteUnitConstant(binding, pythonFacts),
            _ => throw new Xunit.Sdk.XunitException(
                "Unknown constants engineering executor '" + binding.Executor + "'."),
        };
    }

    private static string[] ExecuteThermalClass(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("class.construction", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observations");
            JsonElement[] observations = pythonFacts.GetProperty("observations")
                .EnumerateArray()
                .ToArray();
            Assert.Equal(3, observations.Length);
            AssertKeys(observations[0], "input", "label", "outcome", "result");
            AssertTaggedInteger(observations[0].GetProperty("input"), "107");
            Assert.Equal("integer-member", RequiredString(observations[0], "label"));
            Assert.Equal("returned", RequiredString(observations[0], "outcome"));
            AssertThermalMemberResult(observations[0].GetProperty("result"));
            AssertKeys(observations[1], "input", "label", "outcome", "result");
            AssertTaggedFloat(observations[1].GetProperty("input"), "1.ac00000000000p+6");
            Assert.Equal("float-member", RequiredString(observations[1], "label"));
            Assert.Equal("returned", RequiredString(observations[1], "outcome"));
            AssertThermalMemberResult(observations[1].GetProperty("result"));
            AssertKeys(
                observations[2],
                "error_category",
                "exception_type",
                "input",
                "label",
                "outcome");
            Assert.Equal("domain", RequiredString(observations[2], "error_category"));
            Assert.Equal("ValueError", RequiredString(observations[2], "exception_type"));
            AssertTaggedInteger(observations[2].GetProperty("input"), "106");
            Assert.Equal("unknown-value", RequiredString(observations[2], "label"));
            Assert.Equal("raised", RequiredString(observations[2], "outcome"));

            Assert.Empty(typeof(ThermalDefaults).GetConstructors());
            Assert.Equal(107d, ThermalDefaults.PeopleActivityLevelWattsPerPerson);
            return new[]
            {
                "native-container=public-static",
                "native-public-constructor-count=0",
                "native-people-activity-bits=405ac00000000000",
            };
        }

        if (caseId.EndsWith("class.member-topology", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "declared_member_names",
                "iterated_member_names",
                "member_count",
                "unique_member_count");
            AssertStringArray(
                pythonFacts.GetProperty("declared_member_names"),
                "PEOPLE_ACTIVITY_LEVEL");
            AssertStringArray(
                pythonFacts.GetProperty("iterated_member_names"),
                "PEOPLE_ACTIVITY_LEVEL");
            Assert.Equal(1, pythonFacts.GetProperty("member_count").GetInt32());
            Assert.Equal(1, pythonFacts.GetProperty("unique_member_count").GetInt32());

            FieldInfo field = typeof(ThermalDefaults).GetField(
                nameof(ThermalDefaults.PeopleActivityLevelWattsPerPerson))!;
            Assert.True(field.IsLiteral);
            return new[]
            {
                "native-declared-constant-count=1",
                "native-constant=PeopleActivityLevelWattsPerPerson",
                "native-constant-kind=public-static-const-double",
            };
        }

        Assert.EndsWith("class.type-topology", caseId, StringComparison.Ordinal);
        AssertKeys(
            pythonFacts,
            "base_names",
            "class_name",
            "is_enum_subclass",
            "is_float_subclass",
            "module",
            "signature");
        AssertStringArray(pythonFacts.GetProperty("base_names"), "float", "Enum");
        Assert.Equal("THERMAL", RequiredString(pythonFacts, "class_name"));
        Assert.True(pythonFacts.GetProperty("is_enum_subclass").GetBoolean());
        Assert.True(pythonFacts.GetProperty("is_float_subclass").GetBoolean());
        Assert.Equal("idragon.constants", RequiredString(pythonFacts, "module"));
        Assert.Equal("(*values)", RequiredString(pythonFacts, "signature"));

        Assert.True(typeof(ThermalDefaults).IsAbstract && typeof(ThermalDefaults).IsSealed);
        Assert.False(typeof(ThermalDefaults).IsEnum);
        return new[]
        {
            "native-type=" + ThermalTypeName,
            "native-type-kind=static-container",
            "python-enum-runtime-names=pinned-upstream-only",
        };
    }

    private static string[] ExecuteThermalConstant(string caseId, JsonElement pythonFacts)
    {
        SymbolContract symbol = ExpectedSymbols[1];
        FieldInfo field = NativeField(symbol);
        double nativeValue = Assert.IsType<double>(field.GetRawConstantValue());
        if (caseId.EndsWith("idf-default", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "activity_value",
                "field_count",
                "name",
                "object_type",
                "schedule_type");
            AssertTaggedFloat(
                pythonFacts.GetProperty("activity_value"),
                "1.ac00000000000p+6");
            Assert.Equal(3, pythonFacts.GetProperty("field_count").GetInt32());
            Assert.Equal("$DEFAULT$PEOPLEACTIVITY", RequiredString(pythonFacts, "name"));
            Assert.Equal("Schedule:Constant", RequiredString(pythonFacts, "object_type"));
            JsonElement scheduleType = pythonFacts.GetProperty("schedule_type");
            AssertKeys(scheduleType, "name", "value");
            Assert.Equal("REAL", RequiredString(scheduleType, "name"));
            Assert.Equal("real", RequiredString(scheduleType, "value"));

            IdfDocument document = CreatePublicEngineeringProbeDocument();
            IdfObject activity = Assert.Single(
                document["Schedule:Constant"],
                item => item.Name == "$DEFAULT$PEOPLEACTIVITY");
            IdfObject ventilation = Assert.Single(document["ZoneVentilation:DesignFlowRate"]);
            Assert.Equal("107.0", activity[2]);
            Assert.Equal("Flow/Person", ventilation[3]);
            Assert.Equal("0.0083", ventilation[6]);
            return new[]
            {
                "native-idf-object=Schedule:Constant",
                "native-idf-activity=107.0",
                "native-ventilation-flow-per-person=0.0083",
            };
        }

        if (caseId.EndsWith("numeric-semantics", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "equals_107",
                "float_projection",
                "is_float_instance",
                "value_type");
            Assert.True(pythonFacts.GetProperty("equals_107").GetBoolean());
            AssertTaggedFloat(
                pythonFacts.GetProperty("float_projection"),
                "1.ac00000000000p+6");
            Assert.True(pythonFacts.GetProperty("is_float_instance").GetBoolean());
            Assert.Equal("float", RequiredString(pythonFacts, "value_type"));

            Assert.Equal(107d, nativeValue);
            Assert.Equal(typeof(double), field.FieldType);
            return new[]
            {
                "native-value=107",
                "native-field-type=System.Double",
                "python-runtime-type-name=pinned-upstream-only",
            };
        }

        Assert.EndsWith(".value", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "canonical_name", "declared_name", "value");
        Assert.Equal("PEOPLE_ACTIVITY_LEVEL", RequiredString(pythonFacts, "canonical_name"));
        Assert.Equal("PEOPLE_ACTIVITY_LEVEL", RequiredString(pythonFacts, "declared_name"));
        AssertTaggedFloat(pythonFacts.GetProperty("value"), "1.ac00000000000p+6");
        Assert.True(field.IsLiteral);
        Assert.Equal(0x405A_C000_0000_0000L, BitConverter.DoubleToInt64Bits(nativeValue));
        return new[]
        {
            "native-field=PeopleActivityLevelWattsPerPerson",
            "native-field-kind=const-double",
            "native-binary64-bits=405ac00000000000",
        };
    }

    private static string[] ExecuteUnitClass(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("class.alias-topology", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "alias_group",
                "canonical_names",
                "l2m3_is_mm2m",
                "mm2m_is_w2kw");
            AssertStringArray(pythonFacts.GetProperty("alias_group"), "MM2M", "W2KW", "L2M3");
            JsonElement canonical = pythonFacts.GetProperty("canonical_names");
            AssertKeys(canonical, "L2M3", "MM2M", "NONE2PRC", "PRC2NONE", "W2KW");
            Assert.Equal("MM2M", RequiredString(canonical, "L2M3"));
            Assert.Equal("MM2M", RequiredString(canonical, "MM2M"));
            Assert.Equal("NONE2PRC", RequiredString(canonical, "NONE2PRC"));
            Assert.Equal("PRC2NONE", RequiredString(canonical, "PRC2NONE"));
            Assert.Equal("MM2M", RequiredString(canonical, "W2KW"));
            Assert.True(pythonFacts.GetProperty("l2m3_is_mm2m").GetBoolean());
            Assert.True(pythonFacts.GetProperty("mm2m_is_w2kw").GetBoolean());

            Assert.Equal(UnitConversions.MillimetresToMetres, UnitConversions.WattsToKilowatts);
            Assert.Equal(UnitConversions.MillimetresToMetres, UnitConversions.LitresToCubicMetres);
            return new[]
            {
                "native-shared-numeric-value=0.001",
                "native-named-constant-count=5",
                "native-alias-contract=numeric-equality-only",
            };
        }

        if (caseId.EndsWith("class.member-order", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "declared_member_names",
                "iterated_member_names",
                "iterated_values",
                "member_count",
                "unique_member_count");
            AssertStringArray(
                pythonFacts.GetProperty("declared_member_names"),
                "MM2M",
                "NONE2PRC",
                "PRC2NONE",
                "W2KW",
                "L2M3");
            AssertStringArray(
                pythonFacts.GetProperty("iterated_member_names"),
                "MM2M",
                "NONE2PRC",
                "PRC2NONE");
            JsonElement[] values = pythonFacts.GetProperty("iterated_values")
                .EnumerateArray()
                .ToArray();
            Assert.Equal(3, values.Length);
            AssertTaggedFloat(values[0], "1.0624dd2f1a9fcp-10");
            AssertTaggedFloat(values[1], "1.9000000000000p+6");
            AssertTaggedFloat(values[2], "1.47ae147ae147bp-7");
            Assert.Equal(5, pythonFacts.GetProperty("member_count").GetInt32());
            Assert.Equal(3, pythonFacts.GetProperty("unique_member_count").GetInt32());

            FieldInfo[] fields = typeof(UnitConversions).GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.Equal(5, fields.Length);
            return new[]
            {
                "native-field-count=5",
                "native-field-access=explicit-names",
                "python-enum-iteration-order=pinned-upstream-only",
            };
        }

        Assert.EndsWith("class.type-topology", caseId, StringComparison.Ordinal);
        AssertKeys(
            pythonFacts,
            "base_names",
            "class_name",
            "is_enum_subclass",
            "is_float_subclass",
            "module",
            "signature");
        AssertStringArray(pythonFacts.GetProperty("base_names"), "float", "Enum");
        Assert.Equal("Unit", RequiredString(pythonFacts, "class_name"));
        Assert.True(pythonFacts.GetProperty("is_enum_subclass").GetBoolean());
        Assert.True(pythonFacts.GetProperty("is_float_subclass").GetBoolean());
        Assert.Equal("idragon.constants", RequiredString(pythonFacts, "module"));
        Assert.Equal("(*values)", RequiredString(pythonFacts, "signature"));

        Assert.True(typeof(UnitConversions).IsAbstract && typeof(UnitConversions).IsSealed);
        Assert.False(typeof(UnitConversions).IsEnum);
        return new[]
        {
            "native-type=" + UnitTypeName,
            "native-type-kind=static-container",
            "python-enum-runtime-names=pinned-upstream-only",
        };
    }

    private static string[] ExecuteUnitConstant(CaseBinding binding, JsonElement pythonFacts)
    {
        ProbeDefinition probe = ProbeFor(binding.Symbol);
        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == binding.Symbol);
        FieldInfo field = NativeField(symbol);
        double nativeValue = Assert.IsType<double>(field.GetRawConstantValue());
        if (binding.CaseId.EndsWith("engineering-probe", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "input", "operation", "result");
            AssertTaggedFloat(pythonFacts.GetProperty("input"), probe.InputBinary64);
            Assert.Equal("multiply", RequiredString(pythonFacts, "operation"));
            AssertTaggedFloat(pythonFacts.GetProperty("result"), probe.ResultBinary64);

            double nativeResult = probe.Input * nativeValue;
            Assert.Equal(probe.ExpectedResult, nativeResult);
            Assert.Equal(probe.ExpectedResultBits, BitConverter.DoubleToInt64Bits(nativeResult));
            return new[]
            {
                "native-operation=multiply",
                "native-probe-input=" + probe.Input.ToString("R", CultureInfo.InvariantCulture),
                "native-probe-result-bits=" + HexBits(nativeResult),
            };
        }

        if (binding.CaseId.EndsWith("numeric-semantics", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "canonical_name",
                "declared_name",
                "equals_value",
                "float_projection",
                "is_float_instance",
                "is_same_as_canonical_member");
            Assert.Equal(probe.CanonicalName, RequiredString(pythonFacts, "canonical_name"));
            Assert.Equal(probe.DeclaredName, RequiredString(pythonFacts, "declared_name"));
            Assert.True(pythonFacts.GetProperty("equals_value").GetBoolean());
            AssertTaggedFloat(pythonFacts.GetProperty("float_projection"), probe.ValueBinary64);
            Assert.True(pythonFacts.GetProperty("is_float_instance").GetBoolean());
            Assert.True(pythonFacts.GetProperty("is_same_as_canonical_member").GetBoolean());

            Assert.Equal(symbol.ExpectedValue!.Value, nativeValue);
            Assert.Equal(typeof(double), field.FieldType);
            return new[]
            {
                "native-declared-field=" + field.Name,
                "native-numeric-value-bits=" + HexBits(nativeValue),
                "python-enum-identity=pinned-upstream-only",
            };
        }

        Assert.EndsWith(".value", binding.CaseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "canonical_name", "declared_name", "value");
        Assert.Equal(probe.CanonicalName, RequiredString(pythonFacts, "canonical_name"));
        Assert.Equal(probe.DeclaredName, RequiredString(pythonFacts, "declared_name"));
        AssertTaggedFloat(pythonFacts.GetProperty("value"), probe.ValueBinary64);
        Assert.True(field.IsLiteral);
        Assert.Equal(symbol.ExpectedBits!.Value, BitConverter.DoubleToInt64Bits(nativeValue));
        return new[]
        {
            "native-field=" + field.Name,
            "native-field-kind=const-double",
            "native-binary64-bits=" + HexBits(nativeValue),
        };
    }

    private static ProbeDefinition ProbeFor(string symbol)
    {
        return symbol switch
        {
            "Unit.L2M3" => new(
                "L2M3",
                "MM2M",
                "1.0624dd2f1a9fcp-10",
                8.3d,
                "1.099999999999ap+3",
                0.0083d,
                "1.0ff972474538fp-7",
                0x3F80_FF97_2474_538FL),
            "Unit.MM2M" => new(
                "MM2M",
                "MM2M",
                "1.0624dd2f1a9fcp-10",
                1250d,
                "1.3880000000000p+10",
                1.25d,
                "1.4000000000000p+0",
                0x3FF4_0000_0000_0000L),
            "Unit.NONE2PRC" => new(
                "NONE2PRC",
                "NONE2PRC",
                "1.9000000000000p+6",
                0.375d,
                "1.8000000000000p-2",
                37.5d,
                "1.2c00000000000p+5",
                0x4042_C000_0000_0000L),
            "Unit.PRC2NONE" => new(
                "PRC2NONE",
                "PRC2NONE",
                "1.47ae147ae147bp-7",
                37.5d,
                "1.2c00000000000p+5",
                0.375d,
                "1.8000000000000p-2",
                0x3FD8_0000_0000_0000L),
            "Unit.W2KW" => new(
                "W2KW",
                "MM2M",
                "1.0624dd2f1a9fcp-10",
                4200d,
                "1.0680000000000p+12",
                4.2d,
                "1.0cccccccccccdp+2",
                0x4010_CCCC_CCCC_CCCDL),
            _ => throw new Xunit.Sdk.XunitException(
                "Unknown unit-constant symbol '" + symbol + "'."),
        };
    }

    private static FieldInfo NativeField(SymbolContract symbol)
    {
        Type declaringType = symbol.Symbol.StartsWith("THERMAL.", StringComparison.Ordinal)
            ? typeof(ThermalDefaults)
            : typeof(UnitConversions);
        int separator = symbol.NativeTarget.LastIndexOf('.');
        Assert.True(separator >= 0);
        string fieldName = symbol.NativeTarget.Substring(separator + 1);
        return Assert.IsAssignableFrom<FieldInfo>(declaringType.GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
    }

    private static void AssertThermalMemberResult(JsonElement value)
    {
        AssertKeys(value, "name", "value");
        Assert.Equal("PEOPLE_ACTIVITY_LEVEL", RequiredString(value, "name"));
        AssertTaggedFloat(value.GetProperty("value"), "1.ac00000000000p+6");
    }

    private static IdfDocument CreatePublicEngineeringProbeDocument()
    {
        var material = new Material("Oracle concrete", 1.4d, 2200d, 880d);
        var construction = new OpaqueConstruction(
            "Oracle floor assembly",
            new[] { new Layer("Oracle concrete layer", material, 0.2d) });
        var surface = new Surface(
            new EntityId("CONSTANTS-ORACLE-FLOOR"),
            "Oracle floor",
            SurfaceType.Floor,
            construction,
            SurfaceBoundary.Ground,
            new PlanarPolygon(new[]
            {
                new Vertex(0d, 0d, 0d),
                new Vertex(4d, 0d, 0d),
                new Vertex(4d, 4d, 0d),
                new Vertex(0d, 4d, 0d),
            }));
        var profile = new ZoneProfile(
            new EntityId("CONSTANTS-ORACLE-PROFILE"),
            "Oracle profile",
            Schedule.Constant("Oracle heating", 20d, ScheduleType.Temperature),
            Schedule.Constant("Oracle cooling", 27d, ScheduleType.Temperature),
            Schedule.Constant("Oracle availability", 1d, ScheduleType.OnOff),
            Schedule.Constant("Oracle occupancy", 0.1d));
        var zone = new Zone(
            new EntityId("CONSTANTS-ORACLE-ZONE"),
            "Oracle zone",
            new[] { surface },
            profile);
        var model = new EnergyModel("Constants engineering oracle", new[] { zone });
        return model.ToIdfDocument();
    }

    private static string HexBits(double value) =>
        BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);

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

    private static void AssertTaggedInteger(JsonElement value, string expectedDecimal)
    {
        AssertKeys(value, "decimal", "kind");
        Assert.Equal("int", RequiredString(value, "kind"));
        Assert.Equal(expectedDecimal, RequiredString(value, "decimal"));
    }

    private static void AssertTaggedFloat(JsonElement value, string expectedBinary64)
    {
        AssertKeys(value, "binary64", "kind");
        Assert.Equal("float", RequiredString(value, "kind"));
        Assert.Equal(expectedBinary64, RequiredString(value, "binary64"));
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private static void ValidateTaggedScalarsRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("kind", out JsonElement kindValue))
        {
            string kind = kindValue.GetString()!;
            switch (kind)
            {
                case "int":
                    AssertKeys(value, "decimal", "kind");
                    Assert.Matches(
                        @"^-?(?:0|[1-9][0-9]*)$",
                        RequiredString(value, "decimal"));
                    break;
                case "float":
                    AssertKeys(value, "binary64", "kind");
                    Assert.Matches(
                        @"^-?(?:[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+)$",
                        RequiredString(value, "binary64"));
                    break;
                default:
                    throw new Xunit.Sdk.XunitException(
                        "Unknown tagged fixture scalar kind '" + kind + "'.");
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                ValidateTaggedScalarsRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateTaggedScalarsRecursive(item);
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
        string NativeTarget,
        double? ExpectedValue,
        long? ExpectedBits);

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string? AdaptationId,
        IReadOnlyList<string> NativeFacts);

    private sealed record ProbeDefinition(
        string DeclaredName,
        string CanonicalName,
        string ValueBinary64,
        double Input,
        string InputBinary64,
        double ExpectedResult,
        string ResultBinary64,
        long ExpectedResultBits);
}
