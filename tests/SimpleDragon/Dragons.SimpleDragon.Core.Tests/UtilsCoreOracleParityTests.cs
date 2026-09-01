using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;
using Dragons.SimpleDragon.Internal;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.SimpleDragon.Tests;

public sealed class UtilsCoreOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/utils-core-oracle.json";
    private const string OracleSha256 =
        "sha256:b661c4b906552383f4c27d68e69564b6b81a61f7555e7cc849b6c25318ac746c";
    private const string CasesSha256 =
        "sha256:0941c448edc0ca61009841c41aa479ac13fd24e5d2c66ec5b1684157d57473b9";
    private const int OracleByteLength = 16_070;
    private const int ExpectedCaseCount = 12;
    private const string OracleSchema = "dragons.simpledragon.utils-core-oracle.v1";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.UtilsCoreOracleParityTests.MatchesPinnedPythonUtilsCore";

    private static readonly SourceContract[] ExpectedSources =
    {
        new("src/epsimple/utils.py", "sha256:4b19874951feb696f0a5f1b42d85a11c405e5f83958828997af9a977a6aa9cf8"),
        new("src/idragon/utils.py", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd"),
    };

    // Exact literals are consumed by the trusted compatibility evidence collector.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/epsimple/utils.py", "GRJSON_FORMAT", "sha256:6c3ef8ba838797c6783d1ed35b52dcd6b4eb364baa529820c6df9ed8dfb2e75e", "utils-core-grjson-format-6c3ef8ba"),
        new("src/idragon/utils.py", "validate_enum", "sha256:8b3b34b63f7091d045c421b0309c3549f935ee47aa704faf4931be786991402c", "utils-core-validate-enum-8b3b34b6"),
        new("src/idragon/utils.py", "validate_range", "sha256:a5710a725c7060dead58c254874c24d8c82b0e25d08cc88abff1e68275fcb0b1", "utils-core-validate-range-a5710a72"),
        new("src/idragon/utils.py", "validate_type", "sha256:d2d6da05e97ccf6815cd924a3c8e4502fcb9055aa771281f95f609cd11c6eb26", "utils-core-validate-type-d2d6da05"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("src/epsimple/utils.py", "GRJSON_FORMAT", "constant", "sha256:d85c1609b8dd75fa0730679f37f9ee903e8f5cb3f7aadb6d2f81b72cc03bfe8e", "sha256:bfd54ded3c829caf3ffe7c5b15a3692067451f5f758d2bc89df825fb39c4409e", "immutable-validated-grm-template"),
        new("src/idragon/utils.py", "validate_enum", "function", "sha256:a1cad1caa130af3a903461789f644227240d4623fe1f248ed8865270b8b9e1cc", "sha256:38228c97c0219e1c852349edbbfec7cdc92cf88421439e0bf3f0e99c0c8f3558", "strongly-typed-native-enum-validation"),
        new("src/idragon/utils.py", "validate_range", "function", "sha256:5326abdf0b673e41a11c76f5f481e600209f97011936c814d4e4a518b38c8f17", "sha256:c92ba78111abd3b3bbd34d23a8f932ef366f93d1d673c455497e4d663189bf7e", "finite-native-range-validation"),
        new("src/idragon/utils.py", "validate_type", "function", "sha256:aad965d407adc54c3b5324be5dd2c3d2d6ea1786fe01f5c0b32b698b353019fb", "sha256:4e168989f6958d9a6aa63f5af53727b21bcbe2a154d9e44132f9944cfd99a7bf", "strongly-typed-native-type-validation"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("grjson-format.copy-isolation", "grjson-format", "GRJSON_FORMAT", "immutable-validated-grm-template"),
        new("grjson-format.exact-defaults", "grjson-format", "GRJSON_FORMAT", "immutable-validated-grm-template"),
        new("grjson-format.shared-global-mutation", "grjson-format", "GRJSON_FORMAT", "immutable-validated-grm-template"),
        new("validate-enum.accepted-members-and-raw-values", "validate-enum", "validate_enum", "strongly-typed-native-enum-validation"),
        new("validate-enum.none-and-wraps", "validate-enum", "validate_enum", "strongly-typed-native-enum-validation"),
        new("validate-enum.rejection-surface", "validate-enum", "validate_enum", "strongly-typed-native-enum-validation"),
        new("validate-range.inclusive-boundaries", "validate-range", "validate_range", "finite-native-range-validation"),
        new("validate-range.none-and-nonfinite", "validate-range", "validate_range", "finite-native-range-validation"),
        new("validate-range.rejection-surface", "validate-range", "validate_range", "finite-native-range-validation"),
        new("validate-type.allow-none-and-wraps", "validate-type", "validate_type", "strongly-typed-native-type-validation"),
        new("validate-type.rejection-surface", "validate-type", "validate_type", "strongly-typed-native-type-validation"),
        new("validate-type.union-subclass-and-bool", "validate-type", "validate_type", "strongly-typed-native-type-validation"),
    };

    [Fact]
    public void MatchesPinnedPythonUtilsCore()
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
            string[] nativeFacts = ExecuteCase(binding.CaseId);
            Assert.NotEmpty(nativeFacts);
            Assert.Equal(nativeFacts.Length, nativeFacts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(nativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
            observations.Add(new NativeObservation(
                binding.CaseId,
                binding.Symbol,
                binding.Adaptation,
                nativeFacts));
        }

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
                    adaptation_id = item.Adaptation,
                    case_id = item.CaseId,
                    native_facts = item.NativeFacts,
                    native_outcome = "returned",
                }).ToArray(),
                upstream_path = evidence.Path,
                upstream_symbol = evidence.Symbol,
            };
            AssertReceiptPayloadSafe(JsonSerializer.SerializeToElement(receipt));
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
        AssertKeys(root, "cases", "cases_sha256", "consumer_contract", "runtime", "schema", "symbols", "upstream");
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "sources");
        Assert.Equal("847b01f68f438f560a986072bcaa7768fbf67897", RequiredString(upstream, "commit"));
        Assert.Equal("sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0", RequiredString(upstream, "inventory_sha256"));
        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(ExpectedSources.Length, sources.Length);
        for (int index = 0; index < sources.Length; index++)
        {
            AssertKeys(sources[index], "path", "source_sha256");
            Assert.Equal(ExpectedSources[index].Path, RequiredString(sources[index], "path"));
            Assert.Equal(ExpectedSources[index].SourceSha256, RequiredString(sources[index], "source_sha256"));
        }

        JsonElement runtime = root.GetProperty("runtime");
        AssertKeys(runtime, "implementation", "python_hash_algorithm", "python_hash_seed", "python_hash_width_bits", "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));

        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));
        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCaseCount, cases.Length);
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), cases.Select(item => RequiredString(item, "id")));
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId).OrderBy(item => item, StringComparer.Ordinal),
            ExpectedCases.Select(item => item.CaseId));
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
        }

        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            JsonElement item = symbols[index];
            SymbolContract expected = ExpectedSymbols[index];
            EvidenceBinding evidence = ExpectedEvidence[index];
            AssertKeys(item, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(expected.Path, RequiredString(item, "path"));
            Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(expected.Kind, RequiredString(item, "kind"));
            Assert.Equal(expected.SignatureHash, RequiredString(item, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(item, "body_hash"));
            Assert.Equal(evidence.SymbolHash, RequiredString(item, "symbol_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement value)
    {
        AssertKeys(
            value,
            "adaptations",
            "case_count",
            "case_ids",
            "classifications",
            "float_encoding",
            "runtime_names",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, value.GetProperty("case_count").GetInt32());
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), value.GetProperty("case_ids").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(ExpectedSymbols.Select(item => item.Symbol), value.GetProperty("target_symbols").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(
            "python-binary64-hex-without-0x-prefix",
            RequiredString(value, "float_encoding"));
        Assert.Equal(
            "policy-token-no-raw-address",
            RequiredString(value, "runtime_names"));

        JsonElement classifications = value.GetProperty("classifications");
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        JsonElement adaptations = value.GetProperty("adaptations");
        AssertKeys(adaptations, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in ExpectedSymbols)
        {
            Assert.Equal("exception", RequiredString(classifications, symbol.Symbol));
            Assert.Equal(symbol.Adaptation, RequiredString(adaptations, symbol.Symbol));
        }
    }

    private static void ValidateCase(JsonElement value, CaseBinding expected)
    {
        AssertKeys(value, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(value, "id"));
        Assert.Equal(expected.Executor, RequiredString(value, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(value, "symbol"));

        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        Assert.Equal(JsonValueKind.Object, python.GetProperty("facts").ValueKind);
        Assert.NotEmpty(python.GetProperty("facts").EnumerateObject());

        JsonElement dotnet = value.GetProperty("expected_dotnet");
        AssertKeys(dotnet, "adaptation", "outcome");
        Assert.Equal(expected.Adaptation, RequiredString(dotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(dotnet, "outcome"));
    }

    private static string[] ExecuteCase(string identifier)
    {
        return identifier switch
        {
            "grjson-format.copy-isolation" => AssertLegacyTemplateIsolation(),
            "grjson-format.exact-defaults" => AssertLegacyTemplateDefaults(),
            "grjson-format.shared-global-mutation" => AssertLegacyTemplateImmutability(),
            "validate-enum.accepted-members-and-raw-values" => AssertEnumVocabulary(),
            "validate-enum.none-and-wraps" => AssertNullableEnumContract(),
            "validate-enum.rejection-surface" => AssertEnumRejection(),
            "validate-range.inclusive-boundaries" => AssertRangeBoundaries(),
            "validate-range.none-and-nonfinite" => AssertNullableAndFiniteContract(),
            "validate-range.rejection-surface" => AssertRangeRejection(),
            "validate-type.allow-none-and-wraps" => AssertNullableTypeContract(),
            "validate-type.rejection-surface" => AssertRequiredTypeRejection(),
            "validate-type.union-subclass-and-bool" => AssertClrTypeSeparation(),
            _ => throw new Xunit.Sdk.XunitException("Unknown utils oracle case '" + identifier + "'."),
        };
    }

    private static string[] AssertLegacyTemplateIsolation()
    {
        OrderedMap<object> first = GrmFormat.CreateLegacyInputTemplate();
        OrderedMap<object> second = GrmFormat.CreateLegacyInputTemplate();
        OrderedMap<object> firstBuilding = Assert.IsType<OrderedMap<object>>(first["building"]);
        OrderedMap<object> secondBuilding = Assert.IsType<OrderedMap<object>>(second["building"]);
        Assert.NotSame(first, second);
        Assert.NotSame(firstBuilding, secondBuilding);
        Assert.NotSame(firstBuilding["vintage"], secondBuilding["vintage"]);
        Assert.NotSame(firstBuilding["floors"], secondBuilding["floors"]);
        return new[] { "fresh-root", "fresh-building", "fresh-vintage", "fresh-floors" };
    }

    private static string[] AssertLegacyTemplateDefaults()
    {
        OrderedMap<object> template = GrmFormat.CreateLegacyInputTemplate();
        Assert.Equal(new[] { "building", "materials", "surface_constructions", "fenestration_constructions" }, template.Keys);
        OrderedMap<object> building = Assert.IsType<OrderedMap<object>>(template["building"]);
        Assert.Equal(
            new[] { "name", "north_axis", "address", "vintage", "num_aboveground_floors", "num_underground_floors", "floors", "supply_systems", "source_systems", "ventilation_systems", "photovoltaic_systems" },
            building.Keys);
        string json = JsonSerializer.Serialize(template);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Assert.Equal(304, bytes.Length);
        Assert.Equal("sha256:abbc0cbf3cd7b5dbfae88d9315ab4ae2b08da9ee28077382d6aaba1e1f6d29f1", Sha256(bytes));
        return new[] { "exact-root-order", "exact-building-order", "exact-304-byte-json", "exact-compact-sha256" };
    }

    private static string[] AssertLegacyTemplateImmutability()
    {
        OrderedMap<object> template = GrmFormat.CreateLegacyInputTemplate();
        OrderedMap<object> building = Assert.IsType<OrderedMap<object>>(template["building"]);
        IList<int> vintage = Assert.IsAssignableFrom<IList<int>>(building["vintage"]);
        IList<object> floors = Assert.IsAssignableFrom<IList<object>>(building["floors"]);
        Assert.Throws<NotSupportedException>(() => vintage[0] = 2000);
        Assert.Throws<NotSupportedException>(() => floors.Add(new object()));
        OrderedMap<object> replacement = template.SetItem("materials", Array.AsReadOnly(new object[] { "probe" }));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<object>>(template["materials"]));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object>>(replacement["materials"]));
        return new[] { "nested-vintage-read-only", "nested-floors-read-only", "functional-root-update", "source-unchanged" };
    }

    private static string[] AssertEnumVocabulary()
    {
        Assert.Equal("shade", GrmVocabulary.ToGrm(BlindType.Shade));
        Assert.Equal("venetian", GrmVocabulary.ToGrm(BlindType.Venetian));
        Assert.Equal(BlindType.Shade, GrmVocabulary.ParseBlind("shade"));
        Assert.Equal(BlindType.Venetian, GrmVocabulary.ParseBlind("venetian"));
        Assert.Equal(CoolingTowerType.Open, GrmVocabulary.ParseCoolingTower("open"));
        Assert.Equal(CoolingTowerControl.TwoSpeed, GrmVocabulary.ParseCoolingTowerControl("two-speed"));
        return new[] { "typed-members", "explicit-token-parser", "exact-token-writer" };
    }

    private static string[] AssertNullableEnumContract()
    {
        Assert.Null(DomainSupport.DefinedEnumOrNull<BlindType>(null, "value"));
        Assert.Equal(
            BlindType.Shade,
            DomainSupport.DefinedEnumOrNull<BlindType>(BlindType.Shade, "value"));
        PropertyInfo blind = typeof(Fenestration).GetProperty(nameof(Fenestration.Blind))!;
        Assert.Equal(typeof(BlindType?), blind.PropertyType);
        return new[] { "explicit-nullable-contract", "defined-member-preserved", "no-decorator-wrapper" };
    }

    private static string[] AssertEnumRejection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DomainSupport.DefinedEnumOrNull<BlindType>((BlindType)int.MaxValue, "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrmVocabulary.ToGrm((BlindType)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrmVocabulary.ToGrm((CoolingTowerType)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrmVocabulary.ToGrm((CoolingTowerControl)int.MaxValue));
        return new[] { "undefined-member-rejected", "blind-writer-fail-closed", "tower-writer-fail-closed", "control-writer-fail-closed" };
    }

    private static string[] AssertRangeBoundaries()
    {
        Assert.Equal(0d, DomainSupport.FiniteNonNegative(0d, "value"));
        Assert.Equal(double.Epsilon, DomainSupport.FinitePositive(double.Epsilon, "value"));
        var source = new SourceSystem("boiler", SourceSystemType.Boiler, FuelType.NaturalGas, efficiency: 1d, hotWaterSupply: true);
        Assert.Equal(1d, source.Efficiency);
        return new[] { "inclusive-zero-nonnegative", "positive-lower-bound", "inclusive-fraction-maximum" };
    }

    private static string[] AssertNullableAndFiniteContract()
    {
        var source = new SourceSystem("district", SourceSystemType.DistrictHeating, hotWaterSupply: true);
        Assert.Null(source.HeatingCapacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainSupport.FinitePositive(double.NaN, "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainSupport.FinitePositive(double.PositiveInfinity, "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainSupport.FiniteNonNegative(double.NegativeInfinity, "value"));
        return new[] { "declared-null-preserved", "nan-rejected", "positive-infinity-rejected", "negative-infinity-rejected" };
    }

    private static string[] AssertRangeRejection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainSupport.FinitePositive(0d, "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainSupport.FiniteNonNegative(-double.Epsilon, "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceSystem("boiler", SourceSystemType.Boiler, FuelType.NaturalGas, efficiency: 1.01d, hotWaterSupply: true));
        return new[] { "zero-positive-rejected", "negative-nonnegative-rejected", "fraction-above-one-rejected" };
    }

    private static string[] AssertNullableTypeContract()
    {
        var marker = new object();
        Assert.Same(marker, DomainSupport.NotNull(marker, "value"));
        ParameterInfo fuel = SourceConstructorParameter("fuelType");
        Assert.Equal(typeof(FuelType?), fuel.ParameterType);
        Assert.True(fuel.HasDefaultValue);
        Assert.Null(fuel.DefaultValue);
        return new[] { "reference-preserved", "nullable-signature", "explicit-null-default", "no-callable-metadata" };
    }

    private static string[] AssertRequiredTypeRejection()
    {
        Assert.Throws<ArgumentNullException>(() => DomainSupport.NotNull<object>(null, "value"));
        ParameterInfo name = SourceConstructorParameter("name");
        Assert.Equal(typeof(string), name.ParameterType);
        Assert.False(name.HasDefaultValue);
        return new[] { "required-null-rejected", "required-clr-type", "required-no-default" };
    }

    private static string[] AssertClrTypeSeparation()
    {
        Assert.NotEqual(typeof(bool), typeof(double));
        Assert.Equal(typeof(double?), SourceConstructorParameter("heatingCop").ParameterType);
        Assert.Equal(typeof(bool?), SourceConstructorParameter("hotWaterSupply").ParameterType);
        Assert.Equal(typeof(FuelType?), SourceConstructorParameter("fuelType").ParameterType);
        return new[] { "bool-not-numeric-subtype", "numeric-nullable-is-double", "boolean-nullable-is-bool", "union-replaced-by-closed-signatures" };
    }

    private static ParameterInfo SourceConstructorParameter(string name)
    {
        ConstructorInfo constructor = Assert.Single(typeof(SourceSystem).GetConstructors());
        return Assert.Single(constructor.GetParameters(), parameter => parameter.Name == name);
    }

    private static void AssertReceiptPayloadSafe(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.DoesNotContain(
                    property.Name,
                    new[] { "classification", "expected_dotnet", "policy", "python_outcome" });
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
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
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
                throw new Xunit.Sdk.XunitException("Unsupported canonical JSON kind '" + value.ValueKind + "'.");
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

    private sealed record SourceContract(string Path, string SourceSha256);

    private sealed record EvidenceBinding(string Path, string Symbol, string SymbolHash, string AssertionId);

    private sealed record SymbolContract(
        string Path,
        string Symbol,
        string Kind,
        string SignatureHash,
        string BodyHash,
        string Adaptation);

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol, string Adaptation);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string Adaptation,
        IReadOnlyList<string> NativeFacts);
}
