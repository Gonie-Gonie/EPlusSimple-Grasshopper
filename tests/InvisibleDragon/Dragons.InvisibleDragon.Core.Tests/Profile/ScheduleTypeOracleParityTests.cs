using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests;

public sealed class ScheduleTypeOracleParityTests
{
    private const string OracleSchema = "dragons.invisibledragon.schedule-type-oracle.v1";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamSourceSha256 = "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445";
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/schedule-type-oracle.json";
    private const string OracleSha256 =
        "sha256:68e858a2b243df127663b801f80396671e069645b11659e55ad48eb81973e705";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.ScheduleTypeOracleParityTests.MatchesPinnedPython";
    private static readonly (string Symbol, string Hash)[] ExpectedSymbols =
    {
        ("ScheduleType", "sha256:f873f5e850d3f042a188507bae21c0e74e115483b80a46f72872438e8eeaa38a"),
        ("ScheduleType.FRACTION", "sha256:00d89a2b31e5155ae7bfb099c21c20736c7feb93222e1c11aa002d683c094528"),
        ("ScheduleType.ONOFF", "sha256:767a33fed3b7eec45baa4463546cc530953e0e3fce66d9140b9f84dd0a6e90c3"),
        ("ScheduleType.REAL", "sha256:daaa37fac4fc602f11bc3fba7684dbd4a2c4613929219c46a94d3f70997fbb0e"),
        ("ScheduleType.TEMPERATURE", "sha256:a85b41c57a152b9e1164b77ca6289d10e5d640d50e2f3b7d82f64e2172b2166a"),
        ("ScheduleType.idf_objname", "sha256:6922ec3fabc53f7c283f0626837f483147071358be52745fd4542adce1cfff70"),
        ("ScheduleType.lower_limit", "sha256:e4bfd0fa9092e8a15c109936aca87b8563ad28e785e0d4d3bf31f9271b8dacf2"),
        ("ScheduleType.numeric_type", "sha256:723a16400cd165414a5b9f146557550742fbca24da2d3b341633ff7374c81389"),
        ("ScheduleType.to_idf_object", "sha256:7f67c4b1b5f76c37aa6fb6355d194b08fad513d38bae331c65f645677fa3e1a5"),
        ("ScheduleType.unit_type", "sha256:66ea929d97c87c709bfffcf03a76c7c8ad86b75c844cbff09074e99f8ce339f0"),
        ("ScheduleType.upper_limit", "sha256:e921c8faee5d3b8fa3190333c18831f18e1ff4afc0e2b5ea332156933159c48b"),
        ("ScheduleType.validate", "sha256:b09903103bf95c771eb228f80666fb264e176204c332873795c2d96f86056bcb"),
    };

    [Fact]
    public void MatchesPinnedPython()
    {
        byte[] oracleBytes = File.ReadAllBytes(FindOraclePath());
        string oracleSha256 =
            $"sha256:{Convert.ToHexString(SHA256.HashData(oracleBytes)).ToLowerInvariant()}";
        Assert.Equal(OracleSha256, oracleSha256);
        using JsonDocument oracle = JsonDocument.Parse(oracleBytes);
        JsonElement root = oracle.RootElement;
        Assert.Equal(OracleSchema, root.GetProperty("schema").GetString());

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, upstream.GetProperty("commit").GetString());
        Assert.Equal(UpstreamPath, upstream.GetProperty("path").GetString());
        Assert.Equal(InventorySha256, upstream.GetProperty("inventory_sha256").GetString());
        Assert.Equal(UpstreamSourceSha256, upstream.GetProperty("source_sha256").GetString());
        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        AssertPinnedSymbols(root.GetProperty("symbols"));

        JsonElement.ArrayEnumerator typeRows = root.GetProperty("types").EnumerateArray();
        JsonElement[] rows = typeRows.ToArray();
        Assert.Equal(
            new[]
            {
                ScheduleType.Temperature,
                ScheduleType.OnOff,
                ScheduleType.Fraction,
                ScheduleType.Real,
            },
            Enum.GetValues(typeof(ScheduleType)).Cast<ScheduleType>().ToArray());
        Assert.Equal(
            new[] { "temperature", "onoff", "fraction", "real" },
            rows.Select(row => RequiredString(row, "type")).ToArray());

        IdfObject[] legacyObjects = CreateTypeLimitObjects(legacySimpleDragon: true);
        Assert.Equal(rows.Length, legacyObjects.Length);

        List<ValidationObservation> validationObservations = new();
        for (int index = 0; index < rows.Length; index++)
        {
            JsonElement row = rows[index];
            ScheduleType type = ParseType(RequiredString(row, "type"));
            Assert.Equal(ExpectedEnumName(type), RequiredString(row, "enum_name"));
            Assert.Equal(type.CanonicalName(), RequiredString(row, "type"));
            Assert.Equal(type.IdfObjectName(), RequiredString(row, "idf_objname"));
            AssertNullableNumber(type.LowerLimit(), row.GetProperty("lower_limit"));
            AssertNullableNumber(type.UpperLimit(), row.GetProperty("upper_limit"));
            Assert.Equal(type.NumericType(), RequiredString(row, "numeric_type"));
            Assert.Equal(type.UnitType(), RequiredString(row, "unit_type"));

            JsonElement expectedIdf = row.GetProperty("idf_object");
            AssertIdfObject(type.ToIdfObject(), expectedIdf);
            AssertIdfObject(legacyObjects[index], expectedIdf);

            JsonElement[] validationCases = row.GetProperty("validation_cases").EnumerateArray().ToArray();
            Assert.Equal(ExpectedValidationCaseCount(type), validationCases.Length);
            string[] caseIds = validationCases.Select(item => RequiredString(item, "id")).ToArray();
            Assert.Equal(caseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), caseIds);
            Assert.Equal(caseIds.Length, caseIds.Distinct(StringComparer.Ordinal).Count());

            foreach (JsonElement validationCase in validationCases)
            {
                validationObservations.Add(AssertValidationCase(type, validationCase));
            }
        }

        Assert.Equal(44, validationObservations.Count);
        Assert.Equal(
            3,
            validationObservations.Count(item => item.RegisteredSafetyDivergence));

        IdfObject[] nativeObjects = CreateTypeLimitObjects(legacySimpleDragon: false);
        Assert.Equal("ScheduleTypeLimits:OnOff", nativeObjects[(int)ScheduleType.OnOff].Fields[0].Value);
        Assert.NotEqual(
            ScheduleType.OnOff.IdfObjectName(),
            nativeObjects[(int)ScheduleType.OnOff].Fields[0].Value);

        ScheduleType[] orderedTypes = Enum.GetValues<ScheduleType>();
        var fixture = new
        {
            path = OracleRepositoryPath,
            sha256 = oracleSha256,
        };
        Dictionary<ScheduleType, string> oracleEnumNames = rows.ToDictionary(
            row => ParseType(RequiredString(row, "type")),
            row => RequiredString(row, "enum_name"));

        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-f873f5e8",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    count = orderedTypes.Length,
                    ordered_types = orderedTypes.Select((type, ordinal) => new
                    {
                        canonical_name = type.CanonicalName(),
                        dotnet_name = type.ToString(),
                        ordinal,
                    }).ToArray(),
                },
                upstream_symbol = "ScheduleType",
            });
        RecordScheduleTypeConstant(
            "profile-scheduletype-fraction-00d89a2b",
            "ScheduleType.FRACTION",
            ScheduleType.Fraction,
            oracleEnumNames[ScheduleType.Fraction],
            fixture);
        RecordScheduleTypeConstant(
            "profile-scheduletype-onoff-767a33fe",
            "ScheduleType.ONOFF",
            ScheduleType.OnOff,
            oracleEnumNames[ScheduleType.OnOff],
            fixture);
        RecordScheduleTypeConstant(
            "profile-scheduletype-real-daaa37fa",
            "ScheduleType.REAL",
            ScheduleType.Real,
            oracleEnumNames[ScheduleType.Real],
            fixture);
        RecordScheduleTypeConstant(
            "profile-scheduletype-temperature-a85b41c5",
            "ScheduleType.TEMPERATURE",
            ScheduleType.Temperature,
            oracleEnumNames[ScheduleType.Temperature],
            fixture);
        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-idf-objname-6922ec3f",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    values = orderedTypes.Select(type => new
                    {
                        type = type.CanonicalName(),
                        value = type.IdfObjectName(),
                    }).ToArray(),
                },
                upstream_symbol = "ScheduleType.idf_objname",
            });
        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-lower-limit-e4bfd0fa",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    values = orderedTypes.Select(type => new
                    {
                        type = type.CanonicalName(),
                        value = type.LowerLimit(),
                    }).ToArray(),
                },
                upstream_symbol = "ScheduleType.lower_limit",
            });
        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-numeric-type-723a1640",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    values = orderedTypes.Select(type => new
                    {
                        type = type.CanonicalName(),
                        value = type.NumericType(),
                    }).ToArray(),
                },
                upstream_symbol = "ScheduleType.numeric_type",
            });
        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-to-idf-object-7f67c4b1",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    objects = orderedTypes.Select(type =>
                    {
                        IdfObject value = type.ToIdfObject();
                        return new
                        {
                            fields = value.Fields.Select(field => field.Value).ToArray(),
                            object_type = value.ObjectType,
                            type = type.CanonicalName(),
                        };
                    }).ToArray(),
                },
                upstream_symbol = "ScheduleType.to_idf_object",
            });
        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-unit-type-66ea929d",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    values = orderedTypes.Select(type => new
                    {
                        type = type.CanonicalName(),
                        value = type.UnitType(),
                    }).ToArray(),
                },
                upstream_symbol = "ScheduleType.unit_type",
            });
        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-upper-limit-e921c8fa",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    values = orderedTypes.Select(type => new
                    {
                        type = type.CanonicalName(),
                        value = type.UpperLimit(),
                    }).ToArray(),
                },
                upstream_symbol = "ScheduleType.upper_limit",
            });
        TrustedEvidenceRecorder.Record(
            "profile-scheduletype-validate-b0990310",
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    case_count = validationObservations.Count,
                    cases = validationObservations.Select(item => new
                    {
                        case_id = item.CaseId,
                        dotnet = new
                        {
                            error_category = item.DotnetErrorCategory,
                            finite_value = item.DotnetFiniteValue,
                            numeric_kind = item.DotnetNumericKind,
                            status = item.DotnetStatus,
                            value_kind = item.DotnetValueKind,
                            value_token = item.DotnetValueToken,
                        },
                        input_kind = item.InputKind,
                        native_numeric_kind_adaptation = item.NativeNumericKindAdaptation,
                        python = new
                        {
                            error_category = item.PythonErrorCategory,
                            finite_value = item.PythonFiniteValue,
                            numeric_kind = item.PythonNumericKind,
                            status = item.PythonStatus,
                            value_kind = item.PythonValueKind,
                            value_token = item.PythonValueToken,
                        },
                        registered_safety_divergence = item.RegisteredSafetyDivergence,
                        type = item.Type,
                    }).ToArray(),
                    native_numeric_kind_adaptation_count = validationObservations.Count(
                        item => item.NativeNumericKindAdaptation),
                    real_nonfinite_safety_divergence_count = validationObservations.Count(
                        item => item.RegisteredSafetyDivergence),
                },
                upstream_symbol = "ScheduleType.validate",
            });
    }

    private static void AssertPinnedSymbols(JsonElement symbolsElement)
    {
        JsonElement[] symbols = symbolsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            Assert.Equal(UpstreamPath, RequiredString(symbols[index], "path"));
            Assert.Equal(ExpectedSymbols[index].Symbol, RequiredString(symbols[index], "symbol"));
            Assert.Equal(ExpectedSymbols[index].Hash, RequiredString(symbols[index], "symbol_hash"));
        }
    }

    private static ValidationObservation AssertValidationCase(
        ScheduleType type,
        JsonElement validationCase)
    {
        JsonElement inputElement = validationCase.GetProperty("input");
        JsonElement outcome = validationCase.GetProperty("outcome");
        object input = DecodeInput(inputElement);
        string caseId = RequiredString(validationCase, "id");
        string inputKind = RequiredString(inputElement, "kind");
        string status = RequiredString(outcome, "status");
        bool isRealNonFinite = type == ScheduleType.Real
            && inputKind == "nonfinite";

        if (isRealNonFinite)
        {
            Assert.Equal("value", status);
            Assert.Equal("float", RequiredString(outcome, "numeric_kind"));
            JsonElement expectedValue = outcome.GetProperty("value");
            Assert.Equal("nonfinite", RequiredString(expectedValue, "kind"));
            Assert.Equal(
                RequiredString(inputElement, "value"),
                RequiredString(expectedValue, "value"));
            Exception? error = Record.Exception(
                () => type.ValidateValue(input, "fixtureValue"));
            string dotnetCategory = RequiredDotnetErrorCategory(error);
            Assert.Equal("domain", dotnetCategory);
            return new ValidationObservation(
                type.CanonicalName(),
                caseId,
                inputKind,
                status,
                "error",
                null,
                dotnetCategory,
                RequiredString(outcome, "numeric_kind"),
                null,
                RequiredString(expectedValue, "kind"),
                null,
                null,
                null,
                RequiredString(expectedValue, "value"),
                null,
                false,
                true);
        }

        if (status == "error")
        {
            Exception? error = Record.Exception(() => type.ValidateValue(input, "fixtureValue"));
            string category = RequiredString(outcome, "error_category");
            string dotnetCategory = RequiredDotnetErrorCategory(error);
            Assert.Equal(category, dotnetCategory);
            return new ValidationObservation(
                type.CanonicalName(),
                caseId,
                inputKind,
                status,
                "error",
                category,
                dotnetCategory,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false);
        }

        Assert.Equal("value", status);
        JsonElement expected = outcome.GetProperty("value");
        Assert.Equal("finite", RequiredString(expected, "kind"));
        double actual = type.ValidateValue(input, "fixtureValue");
        Assert.Equal(expected.GetProperty("value").GetDouble(), actual);

        string expectedNumericKind = type == ScheduleType.OnOff ? "int" : "float";
        Assert.Equal(expectedNumericKind, RequiredString(outcome, "numeric_kind"));
        return new ValidationObservation(
            type.CanonicalName(),
            caseId,
            inputKind,
            status,
            "value",
            null,
            null,
            expectedNumericKind,
            "double",
            "finite",
            "finite",
            expected.GetProperty("value").GetDouble(),
            actual,
            null,
            null,
            expectedNumericKind == "int",
            false);
    }

    private static string RequiredDotnetErrorCategory(Exception? error)
    {
        return error switch
        {
            ArgumentOutOfRangeException => "domain",
            ArgumentException => "type",
            null => throw new Xunit.Sdk.XunitException(
                "Expected schedule validation to throw an argument exception."),
            _ => throw new Xunit.Sdk.XunitException(
                $"Unexpected schedule validation exception '{error.GetType().FullName}'."),
        };
    }

    private static void RecordScheduleTypeConstant<TFixture>(
        string assertionId,
        string upstreamSymbol,
        ScheduleType type,
        string oracleEnumName,
        TFixture fixture)
    {
        TrustedEvidenceRecorder.Record(
            assertionId,
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    canonical_name = type.CanonicalName(),
                    dotnet_name = type.ToString(),
                    oracle_enum_name = oracleEnumName,
                },
                upstream_symbol = upstreamSymbol,
            });
    }

    private static object DecodeInput(JsonElement input)
    {
        string kind = RequiredString(input, "kind");
        return kind switch
        {
            "number" => RequiredString(input, "numeric_kind") == "int"
                ? input.GetProperty("value").GetInt64()
                : input.GetProperty("value").GetDouble(),
            "boolean" => input.GetProperty("value").GetBoolean(),
            "string" => RequiredString(input, "value"),
            "nonfinite" => DecodeNonFinite(RequiredString(input, "value")),
            _ => throw new InvalidDataException($"Unknown schedule oracle input kind '{kind}'."),
        };
    }

    private static double DecodeNonFinite(string token)
    {
        return token switch
        {
            "nan" => double.NaN,
            "positive-infinity" => double.PositiveInfinity,
            "negative-infinity" => double.NegativeInfinity,
            _ => throw new InvalidDataException($"Unknown non-finite token '{token}'."),
        };
    }

    private static void AssertIdfObject(IdfObject actual, JsonElement expected)
    {
        Assert.Equal(RequiredString(expected, "object_type"), actual.ObjectType);
        JsonElement[] fields = expected.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Equal(5, fields.Length);
        Assert.Equal(fields.Length, actual.Fields.Count);
        for (int index = 0; index < fields.Length; index++)
        {
            if (fields[index].ValueKind == JsonValueKind.Number)
            {
                Assert.True(double.TryParse(
                    actual.Fields[index].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double nativeNumber));
                Assert.Equal(fields[index].GetDouble(), nativeNumber);
            }
            else
            {
                Assert.Equal(NormalizeIdfField(fields[index]), actual.Fields[index].Value);
            }
        }
    }

    private static string NormalizeIdfField(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => string.Empty,
            JsonValueKind.String => value.GetString()!,
            _ => throw new InvalidDataException(
                $"Unsupported IDF field JSON kind '{value.ValueKind}'."),
        };
    }

    private static void AssertNullableNumber(double? actual, JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(actual);
        }
        else
        {
            Assert.Equal(JsonValueKind.Number, expected.ValueKind);
            Assert.Equal(expected.GetDouble(), actual);
        }
    }

    private static IdfObject[] CreateTypeLimitObjects(bool legacySimpleDragon)
    {
        IdfDocument document = new EnergyModel(
            "Schedule type oracle",
            Array.Empty<Zone>()).ToIdfDocument(
                options: new EnergyModelIdfOptions
                {
                    UseLegacySimpleDragonScheduleMetadata = legacySimpleDragon,
                });
        return document["ScheduleTypeLimits"].ToArray();
    }

    private static ScheduleType ParseType(string name)
    {
        return name switch
        {
            "temperature" => ScheduleType.Temperature,
            "onoff" => ScheduleType.OnOff,
            "fraction" => ScheduleType.Fraction,
            "real" => ScheduleType.Real,
            _ => throw new InvalidDataException($"Unknown schedule oracle type '{name}'."),
        };
    }

    private static string ExpectedEnumName(ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Temperature => "TEMPERATURE",
            ScheduleType.OnOff => "ONOFF",
            ScheduleType.Fraction => "FRACTION",
            ScheduleType.Real => "REAL",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }

    private static int ExpectedValidationCaseCount(ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Temperature => 13,
            ScheduleType.OnOff => 11,
            ScheduleType.Fraction => 11,
            ScheduleType.Real => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown schedule type."),
        };
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        string? value = element.GetProperty(propertyName).GetString();
        return value ?? throw new InvalidDataException(
            $"Schedule oracle property '{propertyName}' must be a string.");
    }

    private static string FindOraclePath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "fixtures",
                "reference",
                "python-0.7.0",
                "schedule-type-oracle.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate fixtures/reference/python-0.7.0/schedule-type-oracle.json.");
    }

    private sealed record ValidationObservation(
        string Type,
        string CaseId,
        string InputKind,
        string PythonStatus,
        string DotnetStatus,
        string? PythonErrorCategory,
        string? DotnetErrorCategory,
        string? PythonNumericKind,
        string? DotnetNumericKind,
        string? PythonValueKind,
        string? DotnetValueKind,
        double? PythonFiniteValue,
        double? DotnetFiniteValue,
        string? PythonValueToken,
        string? DotnetValueToken,
        bool NativeNumericKindAdaptation,
        bool RegisteredSafetyDivergence);
}
