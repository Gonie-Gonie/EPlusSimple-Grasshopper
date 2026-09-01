using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dragons.InvisibleDragon.Profile;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class DayScheduleOperationsOracleParityTests
{
    private const string OracleSchema =
        "dragons.invisibledragon.day-schedule-operations-oracle.v1";
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/day-schedule-operations-oracle.json";
    private const string OracleSha256 =
        "sha256:ffa9805fd16847484195eb001cb7530c718554d13db40f811f7fa1242c020355";
    private const int ExpectedCaseCount = 321;
    private const int ExpectedDotnetCaseCount = 22;
    private const int ExpectedTaggedNonfiniteResultCount = 20;
    private const int ExpectedUnboundedIntegerCaseCount = 7;
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamSourceSha256 =
        "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Profile.DayScheduleOperationsOracleParityTests.MatchesPinnedPythonOperations";

    private static readonly EvidenceBinding[] ExpectedSymbols =
    {
        new("DaySchedule.__add__", "sha256:f2cc675e8c909fae4fa4461fb915249045e55b0e6d7b754575b00ba2cecf7610", "profile-dayschedule-add-f2cc675e"),
        new("DaySchedule.__and__", "sha256:28b1aedc4bfa287ba2a8cb24dc3146eed48c955ebfcf167f85ea1c58dddcd238", "profile-dayschedule-and-28b1aedc"),
        new("DaySchedule.__ge__", "sha256:ea94e3369cd6b4314bae0a24563fb18cf478872702352f5439ee030b2024ada0", "profile-dayschedule-ge-ea94e336"),
        new("DaySchedule.__gt__", "sha256:5b9a41353d9b00038482ace45403592e4f63c95442789d384b3f06831bebdee1", "profile-dayschedule-gt-5b9a4135"),
        new("DaySchedule.__invert__", "sha256:0920f4745c4f599b013798696350c268640ce02822d4bcc405fdd5fea20916e4", "profile-dayschedule-invert-0920f474"),
        new("DaySchedule.__le__", "sha256:5c35fbea76e3e4da3f516363b17530e1972fde58b452ee210c22cb5e8d40f68f", "profile-dayschedule-le-5c35fbea"),
        new("DaySchedule.__lt__", "sha256:495dc27481315dcb97554de321b8e45a12379a1747bbb8d63bc5cbae2af46aee", "profile-dayschedule-lt-495dc274"),
        new("DaySchedule.__mul__", "sha256:c8bbdbc48d7e465d159ab6b829609d582004ead56657b7475b7caeb552454aea", "profile-dayschedule-mul-c8bbdbc4"),
        new("DaySchedule.__or__", "sha256:1bf84ec95560db45c4e29a34678c9ff7edad4906bbb3a231bc47aff0481f6fce", "profile-dayschedule-or-1bf84ec9"),
        new("DaySchedule.__radd__", "sha256:5a5ededeac5428a72339d7725836d9062c10a107cbc821e2659160c73668831f", "profile-dayschedule-radd-5a5edede"),
        new("DaySchedule.__rmul__", "sha256:87f6bef2e0be21121fdc990138093d2d07cc225d5edaff5d2129660a902a4e7f", "profile-dayschedule-rmul-87f6bef2"),
        new("DaySchedule.__rsub__", "sha256:a1fa02e18d86596b88fdebeceedaa48459b6e1068c301c93ba26170f41c37418", "profile-dayschedule-rsub-a1fa02e1"),
        new("DaySchedule.__rtruediv__", "sha256:9bc405fae0ca82d5a0ab953af9197871e5c71248a267d07d408071d44abbb374", "profile-dayschedule-rtruediv-9bc405fa"),
        new("DaySchedule.__sub__", "sha256:55fed2bd2b5cbb9b3ed69e4e8c1da4207d382e78ede3fb674750e395f4a1c4e8", "profile-dayschedule-sub-55fed2bd"),
        new("DaySchedule.__truediv__", "sha256:d4bf77a6d67c06dfa3076336eac461f80855fdbfb2f72d46115cd8e67c10ca0b", "profile-dayschedule-truediv-d4bf77a6"),
        new("DaySchedule.element_eq", "sha256:ef89564449828b40d613fe45cb0f86fe06727df8af9b4f2fa967437a68a1e139", "profile-dayschedule-element-eq-ef895644"),
        new("DaySchedule.element_max", "sha256:6bf704e3d166ef0957b56ff7cd2b0841a32b80e8c1783e12cd698a183cb20f05", "profile-dayschedule-element-max-6bf704e3"),
        new("DaySchedule.element_min", "sha256:ac3a8af2147d4a6fb6c85812769b0eea7ddf2c96342d28a0b72c659b0ed1623c", "profile-dayschedule-element-min-ac3a8af2"),
        new("DaySchedule.element_ne", "sha256:93fa9bc6ed088f976183ab9cf80f0388eb4dedffa2d72422d1ca7fef37987493", "profile-dayschedule-element-ne-93fa9bc6"),
        new("DaySchedule.is_between", "sha256:44e0340fd4f8c80dd25355692d36795370a2957bf582a23243d01a6c38736b29", "profile-dayschedule-is-between-44e0340f"),
        new("DaySchedule.is_negative", "sha256:556646a16befc126236753ebe15e3e626de264cc292fa7aeafeccc87f1d6230a", "profile-dayschedule-is-negative-556646a1"),
        new("DaySchedule.is_nonzero", "sha256:c63f38e66d2c02edbc31f84afe616eda8196ac16dd71ee7a198c5ac12c8105a6", "profile-dayschedule-is-nonzero-c63f38e6"),
        new("DaySchedule.is_off", "sha256:c26b058f1987f339e99fb32a831d3e7861f856b5a86ad618f86b4e55335060b5", "profile-dayschedule-is-off-c26b058f"),
        new("DaySchedule.is_on", "sha256:1125889a0369f6326f366dbc743ca024aa4f59ccbf04f21448237818620958a4", "profile-dayschedule-is-on-1125889a"),
        new("DaySchedule.is_positive", "sha256:95ca3954321930aceddb80707aedfff689163c7fcc4cce416cbe4af558801f8c", "profile-dayschedule-is-positive-95ca3954"),
        new("DaySchedule.is_zero", "sha256:c26b058f1987f339e99fb32a831d3e7861f856b5a86ad618f86b4e55335060b5", "profile-dayschedule-is-zero-c26b058f"),
        new("DaySchedule.normalize_by_max", "sha256:dd857df94e8e53388add91cb81cb88d6e8de762ee49553fdd52f37979f5259c7", "profile-dayschedule-normalize-by-max-dd857df9"),
        new("DaySchedule.where", "sha256:33c2a95572c296a03947b50bb90895168c069b0f054bac875f4039bb8232595c", "profile-dayschedule-where-33c2a955"),
    };

    private static readonly Dictionary<string, string> ExpectedDotnetAdaptations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["immutable-day-schedule-normalize-by-max"] = "DaySchedule.normalize_by_max",
            ["deterministic-day-schedule-where-name"] = "DaySchedule.where",
            ["nonfinite-result-day-schedule-add"] = "DaySchedule.__add__",
            ["nonfinite-result-day-schedule-mul"] = "DaySchedule.__mul__",
            ["nonfinite-result-day-schedule-radd"] = "DaySchedule.__radd__",
            ["nonfinite-result-day-schedule-rmul"] = "DaySchedule.__rmul__",
            ["nonfinite-result-day-schedule-rsub"] = "DaySchedule.__rsub__",
            ["nonfinite-result-day-schedule-rtruediv"] = "DaySchedule.__rtruediv__",
            ["nonfinite-result-day-schedule-sub"] = "DaySchedule.__sub__",
            ["nonfinite-result-day-schedule-truediv"] = "DaySchedule.__truediv__",
            ["nonfinite-result-day-schedule-element-max"] = "DaySchedule.element_max",
            ["nonfinite-result-day-schedule-element-min"] = "DaySchedule.element_min",
        };

    private static readonly JsonSerializerOptions EvidenceJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    [Fact]
    public void MatchesPinnedPythonOperations()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo french = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = french;
            CultureInfo.CurrentUICulture = french;
            AssertMatchesPinnedPythonOperations();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static void AssertMatchesPinnedPythonOperations()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        Assert.Equal(OracleSha256, sha256);

        using JsonDocument oracle = JsonDocument.Parse(bytes);
        JsonElement root = oracle.RootElement;
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));
        AssertOracleIdentity(root);
        AssertPinnedSymbols(root.GetProperty("symbols"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        AssertFixtureCardinality(cases);
        string[] caseIds = cases.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(caseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), caseIds);
        Assert.Equal(caseIds.Length, caseIds.Distinct(StringComparer.Ordinal).Count());

        CaseEvidence[] observations = cases.Select(AssertOperationCase).ToArray();
        Assert.Equal(
            ExpectedSymbols.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            observations.Select(item => item.Symbol).Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));

        var fixture = new { path = OracleRepositoryPath, sha256 };
        foreach (EvidenceBinding binding in ExpectedSymbols)
        {
            CaseEvidence[] symbolCases = observations
                .Where(item => item.Symbol == binding.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.NotEmpty(symbolCases);
            TrustedEvidenceRecorder.Record(
                binding.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                new
                {
                    fixture,
                    observations = new
                    {
                        case_count = symbolCases.Length,
                        cases = symbolCases.Select(item => new
                        {
                            case_id = item.CaseId,
                            dotnet = item.Dotnet,
                            python = item.Python,
                            registered_adaptation = item.RegisteredAdaptation,
                        }).ToArray(),
                    },
                    upstream_symbol = binding.Symbol,
                });
        }
    }

    private static void AssertFixtureCardinality(JsonElement[] cases)
    {
        Assert.Equal(ExpectedCaseCount, cases.Length);
        JsonElement[] expectedDotnetCases = cases
            .Where(item => item.TryGetProperty("expected_dotnet", out _))
            .ToArray();
        Assert.Equal(ExpectedDotnetCaseCount, expectedDotnetCases.Length);
        Assert.Equal(
            ExpectedDotnetAdaptations.Keys.OrderBy(item => item, StringComparer.Ordinal),
            expectedDotnetCases
                .Select(item => RequiredString(item.GetProperty("expected_dotnet"), "adaptation"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));
        foreach (JsonElement operationCase in expectedDotnetCases)
        {
            JsonElement expected = operationCase.GetProperty("expected_dotnet");
            string adaptation = RequiredString(expected, "adaptation");
            Assert.Equal(ExpectedDotnetAdaptations[adaptation], RequiredString(operationCase, "symbol"));
            Assert.Equal("raised", RequiredString(expected, "outcome"));
            Assert.Contains(
                RequiredString(expected, "error_category"),
                new[] { "domain", "schedule-operation" });
        }

        Assert.Equal(
            ExpectedTaggedNonfiniteResultCount,
            cases.Count(item =>
                item.GetProperty("observation").TryGetProperty("result", out JsonElement result)
                && ContainsTaggedNonfinite(result)));
        Assert.Equal(
            ExpectedUnboundedIntegerCaseCount,
            cases.Count(item => ContainsDecimalStringInteger(item.GetProperty("inputs"))));
    }

    private static void AssertOracleIdentity(JsonElement root)
    {
        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(InventorySha256, RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(upstream, "source_sha256"));

        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
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

    private static CaseEvidence AssertOperationCase(JsonElement operationCase)
    {
        string caseId = RequiredString(operationCase, "id");
        string symbol = RequiredString(operationCase, "symbol");
        Assert.Contains(symbol, ExpectedSymbols.Select(item => item.Symbol));
        JsonElement inputs = operationCase.GetProperty("inputs");
        OperationContext context = OperationContext.Create(inputs);
        CapturedExecution execution = Capture(() => Dispatch(symbol, inputs, context));
        JsonElement pythonObservation = operationCase.GetProperty("observation");

        AssertPythonInputPostconditions(pythonObservation, context);
        NormalizedOutcome python = NormalizePythonOutcome(pythonObservation);
        NormalizedOutcome dotnet = NormalizeDotnetOutcome(execution, context);
        ExpectedDotnetOutcome? expectedDotnet = ReadExpectedDotnetOutcome(operationCase);
        string? adaptation;
        if (expectedDotnet is not null)
        {
            adaptation = expectedDotnet.Adaptation;
            AssertExpectedDotnetOutcome(expectedDotnet, python, dotnet);
        }
        else
        {
            adaptation = RegisteredAdaptation(symbol, inputs, python);
            if (adaptation is null)
            {
                Assert.Equal(Serialize(python), Serialize(dotnet));
            }
            else if (adaptation == "immutable-day-schedule-normalize-by-max")
            {
                AssertImmutableNormalizationAdaptation(pythonObservation, python, dotnet, context);
            }
            else
            {
                Assert.Equal("deterministic-day-schedule-where-name", adaptation);
                AssertDeterministicWhereNameAdaptation(python, dotnet);
            }
        }

        return new CaseEvidence(
            caseId,
            symbol,
            JsonSerializer.SerializeToElement(python, EvidenceJsonOptions),
            JsonSerializer.SerializeToElement(dotnet, EvidenceJsonOptions),
            adaptation);
    }

    private static ExpectedDotnetOutcome? ReadExpectedDotnetOutcome(JsonElement operationCase)
    {
        if (!operationCase.TryGetProperty("expected_dotnet", out JsonElement expected))
        {
            return null;
        }

        string adaptation = RequiredString(expected, "adaptation");
        Assert.True(ExpectedDotnetAdaptations.ContainsKey(adaptation));
        Assert.Equal(ExpectedDotnetAdaptations[adaptation], RequiredString(operationCase, "symbol"));
        return new ExpectedDotnetOutcome(
            adaptation,
            RequiredString(expected, "outcome"),
            RequiredString(expected, "error_category"));
    }

    private static void AssertExpectedDotnetOutcome(
        ExpectedDotnetOutcome expected,
        NormalizedOutcome python,
        NormalizedOutcome dotnet)
    {
        Assert.Equal(expected.Outcome, dotnet.Outcome);
        Assert.Equal(expected.ErrorCategory, dotnet.ErrorCategory);
        if (expected.Outcome == "raised")
        {
            Assert.Equal("none", dotnet.ResultKind);
            Assert.Equal("none", dotnet.ResultIdentity);
            Assert.Null(dotnet.Result);
        }

        Assert.Equal(Serialize(python.InputStates), Serialize(dotnet.InputStates));
    }

    private static DaySchedule Dispatch(
        string symbol,
        JsonElement inputs,
        OperationContext context)
    {
        DaySchedule Receiver() => context.Schedule("receiver");
        return symbol switch
        {
            "DaySchedule.__add__" => Add(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.__and__" => Receiver() & context.Schedule("other"),
            "DaySchedule.__ge__" => Compare(Receiver(), inputs.GetProperty("other"), context, "ge"),
            "DaySchedule.__gt__" => Compare(Receiver(), inputs.GetProperty("other"), context, "gt"),
            "DaySchedule.__invert__" => !Receiver(),
            "DaySchedule.__le__" => Compare(Receiver(), inputs.GetProperty("other"), context, "le"),
            "DaySchedule.__lt__" => Compare(Receiver(), inputs.GetProperty("other"), context, "lt"),
            "DaySchedule.__mul__" => Multiply(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.__or__" => Receiver() | context.Schedule("other"),
            "DaySchedule.__radd__" => ReverseAdd(Receiver(), ReadScalar(inputs, "other")),
            "DaySchedule.__rmul__" => ReverseMultiply(Receiver(), ReadScalar(inputs, "other")),
            "DaySchedule.__rsub__" => ReverseSubtract(Receiver(), ReadScalar(inputs, "other")),
            "DaySchedule.__rtruediv__" => ReverseDivide(Receiver(), ReadScalar(inputs, "other")),
            "DaySchedule.__sub__" => Subtract(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.__truediv__" => Divide(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.element_eq" => ElementEqual(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.element_max" => ElementMaximum(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.element_min" => ElementMinimum(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.element_ne" => ElementNotEqual(Receiver(), inputs.GetProperty("other"), context),
            "DaySchedule.is_between" => IsBetween(Receiver(), inputs),
            "DaySchedule.is_negative" => Receiver().IsNegative(),
            "DaySchedule.is_nonzero" => Receiver().IsNonzero(),
            "DaySchedule.is_off" => Receiver().IsOff(),
            "DaySchedule.is_on" => Receiver().IsOn(),
            "DaySchedule.is_positive" => Receiver().IsPositive(),
            "DaySchedule.is_zero" => Receiver().IsZero(),
            "DaySchedule.normalize_by_max" => Receiver().NormalizeByMaximum(OptionalText(inputs, "new_name")),
            "DaySchedule.where" => Where(inputs, context),
            _ => throw new InvalidDataException($"Unknown DaySchedule oracle symbol '{symbol}'."),
        };
    }

    private static DaySchedule Add(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver + context.Schedule("other")
            : AddScalar(receiver, ReadScalar(other));
    }

    private static DaySchedule Subtract(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver - context.Schedule("other")
            : SubtractScalar(receiver, ReadScalar(other));
    }

    private static DaySchedule Multiply(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver * context.Schedule("other")
            : MultiplyScalar(receiver, ReadScalar(other));
    }

    private static DaySchedule Divide(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver / context.Schedule("other")
            : DivideScalar(receiver, ReadScalar(other));
    }

    private static DaySchedule AddScalar(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value);
    }

    private static DaySchedule SubtractScalar(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value);
    }

    private static DaySchedule MultiplyScalar(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value);
    }

    private static DaySchedule DivideScalar(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value);
    }

    private static DaySchedule ReverseAdd(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver);
    }

    private static DaySchedule ReverseMultiply(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver);
    }

    private static DaySchedule ReverseSubtract(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver);
    }

    private static DaySchedule ReverseDivide(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver);
    }

    private static DaySchedule Compare(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context,
        string operation)
    {
        if (IsSchedule(other))
        {
            DaySchedule value = context.Schedule("other");
            return operation switch
            {
                "ge" => receiver.GreaterThanOrEqual(value),
                "gt" => receiver.GreaterThan(value),
                "le" => receiver.LessThanOrEqual(value),
                "lt" => receiver.LessThan(value),
                _ => throw new InvalidDataException($"Unknown comparison '{operation}'."),
            };
        }

        OracleScalar scalar = ReadScalar(other);
        return operation switch
        {
            "ge" => GreaterThanOrEqual(receiver, scalar),
            "gt" => GreaterThan(receiver, scalar),
            "le" => LessThanOrEqual(receiver, scalar),
            "lt" => LessThan(receiver, scalar),
            _ => throw new InvalidDataException($"Unknown comparison '{operation}'."),
        };
    }

    private static DaySchedule GreaterThanOrEqual(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.GreaterThanOrEqual,
            receiver.GreaterThanOrEqual,
            receiver.GreaterThanOrEqual,
            receiver.GreaterThanOrEqual);
    }

    private static DaySchedule GreaterThan(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.GreaterThan,
            receiver.GreaterThan,
            receiver.GreaterThan,
            receiver.GreaterThan);
    }

    private static DaySchedule LessThanOrEqual(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.LessThanOrEqual,
            receiver.LessThanOrEqual,
            receiver.LessThanOrEqual,
            receiver.LessThanOrEqual);
    }

    private static DaySchedule LessThan(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.LessThan,
            receiver.LessThan,
            receiver.LessThan,
            receiver.LessThan);
    }

    private static DaySchedule ElementEqual(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver.ElementEqual(context.Schedule("other"))
            : ElementEqual(receiver, ReadScalar(other));
    }

    private static DaySchedule ElementEqual(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementEqual,
            receiver.ElementEqual,
            receiver.ElementEqual,
            receiver.ElementEqual);
    }

    private static DaySchedule ElementNotEqual(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver.ElementNotEqual(context.Schedule("other"))
            : ElementNotEqual(receiver, ReadScalar(other));
    }

    private static DaySchedule ElementNotEqual(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementNotEqual,
            receiver.ElementNotEqual,
            receiver.ElementNotEqual,
            receiver.ElementNotEqual);
    }

    private static DaySchedule ElementMinimum(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver.ElementMinimum(context.Schedule("other"))
            : ElementMinimum(receiver, ReadScalar(other));
    }

    private static DaySchedule ElementMinimum(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementMinimum,
            receiver.ElementMinimum,
            receiver.ElementMinimum,
            receiver.ElementMinimum,
            receiver.ElementMinimum);
    }

    private static DaySchedule ElementMaximum(
        DaySchedule receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsSchedule(other)
            ? receiver.ElementMaximum(context.Schedule("other"))
            : ElementMaximum(receiver, ReadScalar(other));
    }

    private static DaySchedule ElementMaximum(DaySchedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementMaximum,
            receiver.ElementMaximum,
            receiver.ElementMaximum,
            receiver.ElementMaximum,
            receiver.ElementMaximum);
    }

    private static DaySchedule IsBetween(DaySchedule receiver, JsonElement inputs)
    {
        OracleScalar minimum = ReadScalar(inputs, "min_value");
        OracleScalar maximum = ReadScalar(inputs, "max_value");
        bool includeMinimum = Boolean(inputs, "include_min");
        bool includeMaximum = Boolean(inputs, "include_max");
        return receiver.IsBetween(
            minimum.ClrValue,
            maximum.ClrValue,
            includeMinimum,
            includeMaximum);
    }

    private static DaySchedule DispatchScalar(
        OracleScalar scalar,
        Func<int, DaySchedule> integerOperation,
        Func<long, DaySchedule> longIntegerOperation,
        Func<double, DaySchedule> floatOperation,
        Func<bool, DaySchedule> booleanOperation,
        Func<BigInteger, DaySchedule>? unboundedIntegerOperation = null)
    {
        return scalar.PythonType switch
        {
            "int" when scalar.IntegerValue >= int.MinValue
                && scalar.IntegerValue <= int.MaxValue =>
                integerOperation((int)scalar.IntegerValue),
            "int" when scalar.IntegerValue >= long.MinValue
                && scalar.IntegerValue <= long.MaxValue =>
                longIntegerOperation((long)scalar.IntegerValue),
            "int" when unboundedIntegerOperation is not null =>
                unboundedIntegerOperation(scalar.IntegerValue),
            "int" => throw new InvalidDataException(
                "This operation does not support an unbounded Python integer oracle operand."),
            "float" => floatOperation(scalar.FloatValue),
            "bool" => booleanOperation(scalar.BooleanValue),
            _ => throw new InvalidDataException($"Unknown scalar type '{scalar.PythonType}'."),
        };
    }

    private static DaySchedule Where(JsonElement inputs, OperationContext context)
    {
        DaySchedule condition = context.Schedule("condition");
        JsonElement ifTrue = inputs.GetProperty("if_true");
        JsonElement ifFalse = inputs.GetProperty("if_false");
        string? name = OptionalText(inputs, "name");
        ScheduleType? type = OptionalScheduleType(inputs, "type");
        bool trueSchedule = IsSchedule(ifTrue);
        bool falseSchedule = IsSchedule(ifFalse);
        if (trueSchedule && falseSchedule)
        {
            return DaySchedule.Where(
                condition,
                context.Schedule("if_true"),
                context.Schedule("if_false"),
                name,
                type);
        }

        if (trueSchedule)
        {
            return DaySchedule.Where(
                condition,
                context.Schedule("if_true"),
                ReadWhereOperand(ifFalse),
                name,
                type);
        }

        if (falseSchedule)
        {
            return DaySchedule.Where(
                condition,
                ReadWhereOperand(ifTrue),
                context.Schedule("if_false"),
                name,
                type);
        }

        return DaySchedule.Where(
            condition,
            ReadWhereOperand(ifTrue),
            ReadWhereOperand(ifFalse),
            name,
            type);
    }

    private static object ReadWhereOperand(JsonElement descriptor)
    {
        return RequiredString(descriptor, "kind") == "text"
            ? RequiredString(descriptor, "value")
            : ReadScalar(descriptor).ClrValue;
    }

    private static CapturedExecution Capture(Func<DaySchedule> action)
    {
        try
        {
            return new CapturedExecution(action(), null);
        }
        catch (Exception exception)
        {
            return new CapturedExecution(null, exception);
        }
    }

    private static NormalizedOutcome NormalizePythonOutcome(JsonElement observation)
    {
        string outcome = RequiredString(observation, "outcome");
        SortedDictionary<string, string> inputStates = InputStates(
            observation.GetProperty("schedule_inputs_after"));
        if (outcome == "raised")
        {
            JsonElement exception = observation.GetProperty("exception");
            string exceptionType = RequiredString(exception, "type");
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(exception, "message")));
            return new NormalizedOutcome(
                outcome,
                "none",
                "none",
                null,
                PythonErrorCategory(exceptionType),
                inputStates);
        }

        Assert.Equal("returned", outcome);
        string identity = RequiredString(observation, "result_identity");
        JsonElement result = observation.GetProperty("result");
        string resultKind = RequiredString(result, "kind");
        return new NormalizedOutcome(
            outcome,
            resultKind,
            identity,
            resultKind == "schedule" ? ReadScheduleSnapshot(result) : null,
            null,
            inputStates);
    }

    private static NormalizedOutcome NormalizeDotnetOutcome(
        CapturedExecution execution,
        OperationContext context)
    {
        SortedDictionary<string, string> inputStates = new(StringComparer.Ordinal);
        foreach ((string key, DaySchedule schedule) in context.Schedules.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            inputStates.Add(
                key,
                context.Before[key].SameAs(ScheduleSnapshot.From(schedule)) ? "unchanged" : "changed");
        }

        if (execution.Exception is not null)
        {
            return new NormalizedOutcome(
                "raised",
                "none",
                "none",
                null,
                DotnetErrorCategory(execution.Exception),
                inputStates);
        }

        DaySchedule result = Assert.IsType<DaySchedule>(execution.Result);
        string identity = context.Schedules.Values.Any(item => ReferenceEquals(item, result))
            ? "receiver"
            : "new";
        return new NormalizedOutcome(
            "returned",
            "schedule",
            identity,
            ScheduleSnapshot.From(result),
            null,
            inputStates);
    }

    private static void AssertPythonInputPostconditions(
        JsonElement observation,
        OperationContext context)
    {
        JsonElement postconditions = observation.GetProperty("schedule_inputs_after");
        string[] expectedKeys = context.Schedules.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        string[] actualKeys = postconditions.EnumerateObject().Select(item => item.Name)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedKeys, actualKeys);
        foreach (JsonProperty property in postconditions.EnumerateObject())
        {
            Assert.Equal("preserved", RequiredString(property.Value, "identity"));
            string status = RequiredString(property.Value, "status");
            Assert.Contains(status, new[] { "changed", "unchanged" });
            if (status == "changed")
            {
                ScheduleSnapshot changed = ReadScheduleSnapshot(property.Value.GetProperty("value"));
                Assert.False(context.Before[property.Name].SameAs(changed));
            }
            else
            {
                Assert.False(property.Value.TryGetProperty("value", out _));
            }
        }
    }

    private static string? RegisteredAdaptation(
        string symbol,
        JsonElement inputs,
        NormalizedOutcome python)
    {
        if (symbol == "DaySchedule.normalize_by_max" && Boolean(inputs, "inplace"))
        {
            return "immutable-day-schedule-normalize-by-max";
        }

        if (symbol == "DaySchedule.where"
            && python.Result?.NamePolicy == "runtime-identity-hex")
        {
            return "deterministic-day-schedule-where-name";
        }

        return null;
    }

    private static void AssertImmutableNormalizationAdaptation(
        JsonElement pythonObservation,
        NormalizedOutcome python,
        NormalizedOutcome dotnet,
        OperationContext context)
    {
        Assert.Equal("returned", python.Outcome);
        Assert.Equal("none", python.ResultKind);
        Assert.Equal("none", python.ResultIdentity);
        Assert.Equal("changed", python.InputStates["receiver"]);
        Assert.Equal("unchanged", dotnet.InputStates["receiver"]);

        JsonElement changedReceiver = pythonObservation
            .GetProperty("schedule_inputs_after")
            .GetProperty("receiver")
            .GetProperty("value");
        ScheduleSnapshot pythonChanged = ReadScheduleSnapshot(changedReceiver);
        Assert.True(context.Before["receiver"].SameAs(ScheduleSnapshot.From(context.Schedule("receiver"))));
        if (dotnet.Outcome == "returned")
        {
            Assert.Equal("schedule", dotnet.ResultKind);
            Assert.Equal("new", dotnet.ResultIdentity);
            Assert.NotNull(dotnet.Result);
            AssertValuesExact(pythonChanged.Values, dotnet.Result!.Values);
            Assert.Equal(pythonChanged.ScheduleType, dotnet.Result.ScheduleType);
            Assert.Null(dotnet.Result.Unit);
        }
        else
        {
            Assert.Equal("raised", dotnet.Outcome);
            Assert.Equal("domain", dotnet.ErrorCategory);
            Assert.Equal("temperature", pythonChanged.ScheduleType);
            Assert.Null(dotnet.Result);
        }
    }

    private static void AssertDeterministicWhereNameAdaptation(
        NormalizedOutcome python,
        NormalizedOutcome dotnet)
    {
        Assert.Equal("returned", python.Outcome);
        Assert.Equal("returned", dotnet.Outcome);
        Assert.Equal("new", python.ResultIdentity);
        Assert.Equal("new", dotnet.ResultIdentity);
        Assert.NotNull(python.Result);
        Assert.NotNull(dotnet.Result);
        Assert.Equal("runtime-identity-hex", python.Result!.NamePolicy);
        Assert.Null(python.Result.Name);
        Assert.Equal("literal", dotnet.Result!.NamePolicy);
        Assert.Equal("WHERE", dotnet.Result.Name);
        AssertScheduleExceptName(python.Result, dotnet.Result);
        Assert.Equal(
            JsonSerializer.Serialize(python.InputStates),
            JsonSerializer.Serialize(dotnet.InputStates));
    }

    private static void AssertScheduleExceptName(
        ScheduleSnapshot expected,
        ScheduleSnapshot actual)
    {
        Assert.Equal(expected.ScheduleType, actual.ScheduleType);
        Assert.Equal(expected.Unit, actual.Unit);
        AssertValuesExact(expected.Values, actual.Values);
    }

    private static void AssertValuesExact(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(expected[index]),
                BitConverter.DoubleToInt64Bits(actual[index]));
        }
    }

    private static SortedDictionary<string, string> InputStates(JsonElement value)
    {
        SortedDictionary<string, string> states = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            states.Add(property.Name, RequiredString(property.Value, "status"));
        }

        return states;
    }

    private static ScheduleSnapshot ReadScheduleSnapshot(JsonElement descriptor)
    {
        Assert.Equal("schedule", RequiredString(descriptor, "kind"));
        JsonElement name = descriptor.GetProperty("name");
        string namePolicy;
        string? nameValue;
        if (name.ValueKind == JsonValueKind.String)
        {
            namePolicy = "literal";
            nameValue = name.GetString();
        }
        else
        {
            namePolicy = RequiredString(name, "policy");
            nameValue = namePolicy == "literal" ? RequiredString(name, "value") : null;
            Assert.Contains(namePolicy, new[] { "literal", "runtime-identity-hex" });
            if (namePolicy == "runtime-identity-hex")
            {
                Assert.False(name.TryGetProperty("value", out _));
            }
        }

        return new ScheduleSnapshot(
            namePolicy,
            nameValue,
            RequiredString(descriptor, "schedule_type"),
            NullableString(descriptor.GetProperty("unit")),
            DecodeValues(descriptor.GetProperty("values")));
    }

    private static double[] DecodeValues(JsonElement values)
    {
        int length = values.GetProperty("length").GetInt32();
        Assert.Equal(DaySchedule.FixedLength, length);
        string encoding = RequiredString(values, "encoding");
        double[] decoded;
        if (encoding == "repeat")
        {
            double[] pattern = values.GetProperty("pattern").EnumerateArray()
                .Select(ReadOracleDouble).ToArray();
            Assert.NotEmpty(pattern);
            decoded = Enumerable.Range(0, length).Select(index => pattern[index % pattern.Length]).ToArray();
        }
        else
        {
            Assert.Equal("full", encoding);
            decoded = values.GetProperty("items").EnumerateArray()
                .Select(ReadOracleDouble).ToArray();
        }

        Assert.Equal(length, decoded.Length);
        return decoded;
    }

    private static OracleScalar ReadScalar(JsonElement inputs, string name)
    {
        return ReadScalar(inputs.GetProperty(name));
    }

    private static OracleScalar ReadScalar(JsonElement descriptor)
    {
        string kind = RequiredString(descriptor, "kind");
        if (kind == "nonfinite")
        {
            return OracleScalar.FromFloat(ReadTaggedNonfinite(descriptor));
        }

        Assert.Equal("scalar", kind);
        string pythonType = RequiredString(descriptor, "python_type");
        return pythonType switch
        {
            "bool" => OracleScalar.FromBoolean(descriptor.GetProperty("value").GetBoolean()),
            "int" => OracleScalar.FromInteger(ReadPythonInteger(descriptor.GetProperty("value"))),
            "float" => OracleScalar.FromFloat(descriptor.GetProperty("value").GetDouble()),
            _ => throw new InvalidDataException($"Unknown scalar type '{pythonType}'."),
        };
    }

    private static BigInteger ReadPythonInteger(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return new BigInteger(value.GetInt64());
        }

        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal("decimal-string", RequiredString(value, "kind"));
        string text = RequiredString(value, "value");
        Assert.True(BigInteger.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out BigInteger integer));
        Assert.True(integer < long.MinValue || integer > long.MaxValue);
        return integer;
    }

    private static double ReadOracleDouble(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : ReadTaggedNonfinite(value);
    }

    private static double ReadTaggedNonfinite(JsonElement descriptor)
    {
        Assert.Equal(JsonValueKind.Object, descriptor.ValueKind);
        Assert.Equal("nonfinite", RequiredString(descriptor, "kind"));
        return RequiredString(descriptor, "value") switch
        {
            "positive-infinity" => double.PositiveInfinity,
            "negative-infinity" => double.NegativeInfinity,
            "nan" => double.NaN,
            string token => throw new InvalidDataException($"Unknown nonfinite token '{token}'."),
        };
    }

    private static bool Boolean(JsonElement inputs, string name)
    {
        JsonElement descriptor = inputs.GetProperty(name);
        OracleScalar scalar = ReadScalar(descriptor);
        Assert.Equal("bool", scalar.PythonType);
        return scalar.BooleanValue;
    }

    private static string? OptionalText(JsonElement inputs, string name)
    {
        JsonElement descriptor = inputs.GetProperty(name);
        string kind = RequiredString(descriptor, "kind");
        return kind switch
        {
            "none" => null,
            "text" => RequiredString(descriptor, "value"),
            _ => throw new InvalidDataException($"Unknown optional text kind '{kind}'."),
        };
    }

    private static ScheduleType? OptionalScheduleType(JsonElement inputs, string name)
    {
        JsonElement descriptor = inputs.GetProperty(name);
        string kind = RequiredString(descriptor, "kind");
        return kind switch
        {
            "none" => null,
            "schedule-type" => ParseScheduleType(RequiredString(descriptor, "value")),
            _ => throw new InvalidDataException($"Unknown optional schedule type kind '{kind}'."),
        };
    }

    private static ScheduleType ParseScheduleType(string value)
    {
        return value switch
        {
            "temperature" => ScheduleType.Temperature,
            "onoff" => ScheduleType.OnOff,
            "fraction" => ScheduleType.Fraction,
            "real" => ScheduleType.Real,
            _ => throw new InvalidDataException($"Unknown schedule type '{value}'."),
        };
    }

    private static bool IsSchedule(JsonElement descriptor)
    {
        return RequiredString(descriptor, "kind") == "schedule";
    }

    private static bool ContainsTaggedNonfinite(JsonElement element)
    {
        return ContainsDescriptorKind(element, "nonfinite");
    }

    private static bool ContainsDecimalStringInteger(JsonElement element)
    {
        return ContainsDescriptorKind(element, "decimal-string");
    }

    private static bool ContainsDescriptorKind(JsonElement element, string expectedKind)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("kind", out JsonElement kind)
                && kind.ValueKind == JsonValueKind.String
                && kind.GetString() == expectedKind)
            {
                return true;
            }

            return element.EnumerateObject()
                .Any(property => ContainsDescriptorKind(property.Value, expectedKind));
        }

        return element.ValueKind == JsonValueKind.Array
            && element.EnumerateArray().Any(item => ContainsDescriptorKind(item, expectedKind));
    }

    private static string PythonErrorCategory(string type)
    {
        return type switch
        {
            "ScheduleOperationError" => "schedule-operation",
            "ZeroDivisionError" => "divide-by-zero",
            "ValueError" => "domain",
            "OverflowError" => "domain",
            "TypeError" => "type",
            _ => throw new InvalidDataException($"Unknown Python operation error '{type}'."),
        };
    }

    private static string DotnetErrorCategory(Exception exception)
    {
        return exception switch
        {
            ScheduleOperationException => "schedule-operation",
            DivideByZeroException => "divide-by-zero",
            ArgumentOutOfRangeException => "domain",
            OverflowException => "domain",
            ArgumentException => "type",
            _ => throw new Xunit.Sdk.XunitException(
                $"Unexpected .NET operation exception '{exception.GetType().FullName}': {exception.Message}"),
        };
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, EvidenceJsonOptions);
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
    }

    private static string? NullableString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => throw new InvalidDataException("Expected a string or null JSON value."),
        };
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

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private sealed class OperationContext
    {
        private OperationContext(
            Dictionary<string, DaySchedule> schedules,
            Dictionary<string, ScheduleSnapshot> before)
        {
            Schedules = schedules;
            Before = before;
        }

        public Dictionary<string, DaySchedule> Schedules { get; }

        public Dictionary<string, ScheduleSnapshot> Before { get; }

        public DaySchedule Schedule(string name)
        {
            return Schedules[name];
        }

        public static OperationContext Create(JsonElement inputs)
        {
            Dictionary<string, DaySchedule> schedules = new(StringComparer.Ordinal);
            Dictionary<string, ScheduleSnapshot> before = new(StringComparer.Ordinal);
            foreach (JsonProperty property in inputs.EnumerateObject())
            {
                JsonElement descriptor = property.Value;
                if (!IsSchedule(descriptor))
                {
                    continue;
                }

                string name = RequiredString(descriptor, "name");
                ScheduleType type = ParseScheduleType(RequiredString(descriptor, "schedule_type"));
                string? unit = NullableString(descriptor.GetProperty("unit"));
                DaySchedule schedule = new(
                    name,
                    DecodeValues(descriptor.GetProperty("values")),
                    type,
                    unit);
                schedules.Add(property.Name, schedule);
                before.Add(property.Name, ScheduleSnapshot.From(schedule));
            }

            return new OperationContext(schedules, before);
        }
    }

    private sealed record EvidenceBinding(string Symbol, string Hash, string AssertionId);

    private readonly record struct OracleScalar(
        string PythonType,
        BigInteger IntegerValue,
        double FloatValue,
        bool BooleanValue)
    {
        public object ClrValue => PythonType switch
        {
            "int" when IntegerValue >= int.MinValue && IntegerValue <= int.MaxValue =>
                (int)IntegerValue,
            "int" when IntegerValue >= long.MinValue && IntegerValue <= long.MaxValue =>
                (long)IntegerValue,
            "int" when IntegerValue >= BigInteger.Zero && IntegerValue <= ulong.MaxValue =>
                (ulong)IntegerValue,
            "int" => IntegerValue,
            "float" => FloatValue,
            "bool" => BooleanValue,
            _ => throw new InvalidDataException($"Unknown scalar type '{PythonType}'."),
        };

        public static OracleScalar FromInteger(BigInteger value)
        {
            return new OracleScalar("int", value, (double)value, value != BigInteger.Zero);
        }

        public static OracleScalar FromFloat(double value)
        {
            return new OracleScalar("float", 0, value, value != 0);
        }

        public static OracleScalar FromBoolean(bool value)
        {
            return new OracleScalar("bool", value ? 1 : 0, value ? 1d : 0d, value);
        }
    }

    private sealed record CapturedExecution(DaySchedule? Result, Exception? Exception);

    private sealed record ExpectedDotnetOutcome(
        string Adaptation,
        string Outcome,
        string ErrorCategory);

    private sealed record CaseEvidence(
        string CaseId,
        string Symbol,
        JsonElement Python,
        JsonElement Dotnet,
        string? RegisteredAdaptation);

    private sealed record NormalizedOutcome(
        string Outcome,
        string ResultKind,
        string ResultIdentity,
        ScheduleSnapshot? Result,
        string? ErrorCategory,
        SortedDictionary<string, string> InputStates);

    private sealed record ScheduleSnapshot(
        string NamePolicy,
        string? Name,
        string ScheduleType,
        string? Unit,
        double[] Values)
    {
        public static ScheduleSnapshot From(DaySchedule schedule)
        {
            return new ScheduleSnapshot(
                "literal",
                schedule.Name,
                schedule.Type.CanonicalName(),
                schedule.Unit,
                schedule.Values.ToArray());
        }

        public bool SameAs(ScheduleSnapshot other)
        {
            return NamePolicy == other.NamePolicy
                && Name == other.Name
                && ScheduleType == other.ScheduleType
                && Unit == other.Unit
                && Values.Length == other.Values.Length
                && Values.Zip(other.Values).All(
                    pair => BitConverter.DoubleToInt64Bits(pair.First)
                        == BitConverter.DoubleToInt64Bits(pair.Second));
        }
    }
}
