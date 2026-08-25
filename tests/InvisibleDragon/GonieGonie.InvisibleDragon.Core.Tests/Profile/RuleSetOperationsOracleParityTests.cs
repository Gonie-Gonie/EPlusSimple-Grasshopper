using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Profile;

public sealed class RuleSetOperationsOracleParityTests
{
    private const string OracleSchema =
        "goniegonie.invisibledragon.rule-set-operations-oracle.v1";
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/rule-set-operations-oracle.json";
    private const string OracleSha256 =
        "sha256:5575c9d2946b99afd474220429eef5e5531931a36e26eb55a1e3d4e93548fdc6";
    private const int ExpectedCaseCount = 334;
    private const int ExpectedAdaptationCaseCount = 61;
    private const int ExpectedRepairReferenceCount = 24;
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamSourceSha256 =
        "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Profile.RuleSetOperationsOracleParityTests.MatchesPinnedPythonOperations";

    private static readonly string[] SlotKeys =
    {
        "weekdays",
        "weekends",
        "monday",
        "tuesday",
        "wednesday",
        "thursday",
        "friday",
        "saturday",
        "sunday",
        "holiday",
    };

    private static readonly string[] OverrideKeys = SlotKeys.Skip(2).ToArray();

    private static readonly EvidenceBinding[] ExpectedSymbols =
    {
        new("RuleSet.__add__", "sha256:d658d7f91f8ee7dafbca0504b70bde094910f90830fc85a732e21dcab8ff2405", "profile-ruleset-add-d658d7f9"),
        new("RuleSet.__and__", "sha256:68f36cc14f5d257034871f8f96c9ae1e8b225489ee47fd5ce398ade357148315", "profile-ruleset-and-68f36cc1"),
        new("RuleSet.__ge__", "sha256:66ecfa68f9710c8f9914577b4617b989292394b534ca98b83e213e7fe735d2b7", "profile-ruleset-ge-66ecfa68"),
        new("RuleSet.__gt__", "sha256:c73275fa255d1916ee360c0d6a50ea20828cb75191f9733fa193e1ee4a4f0005", "profile-ruleset-gt-c73275fa"),
        new("RuleSet.__invert__", "sha256:4c2c592271f4031026fa49d9f7b90e2e9d7edf0ce708cef18108e1509768780e", "profile-ruleset-invert-4c2c5922"),
        new("RuleSet.__le__", "sha256:c28491e978f051599d30f0582d7d3e6b92ed39207719422007d5f99e44aec32b", "profile-ruleset-le-c28491e9"),
        new("RuleSet.__lt__", "sha256:cb4515a256ae510fed02b2d73955fd02ec0316d68fc480772662ed3faab38a48", "profile-ruleset-lt-cb4515a2"),
        new("RuleSet.__mul__", "sha256:dfe4535f2bfc5d3e8823015e09f766c4bffb1eaa34a5d34641f5d7b86db22094", "profile-ruleset-mul-dfe4535f"),
        new("RuleSet.__or__", "sha256:db95291ff1d42fb08f26255bca01aae3ca1bcb0ac48a860f335092d2782a83c5", "profile-ruleset-or-db95291f"),
        new("RuleSet.__radd__", "sha256:7d78c731949b203b143a486363e54f8572d57c8f12f0a598d7d0470ac776729e", "profile-ruleset-radd-7d78c731"),
        new("RuleSet.__rmul__", "sha256:7359aee63c4e4e2dc1fd2c80435b39ac3e7989f60e89e4f9951784b13c003a99", "profile-ruleset-rmul-7359aee6"),
        new("RuleSet.__rsub__", "sha256:0ee38c580eba67e6c30a824516a9bf4cf97a4965f28ce732a7271ad8b705d0d6", "profile-ruleset-rsub-0ee38c58"),
        new("RuleSet.__rtruediv__", "sha256:b665fd3ac19d91fed1717628316d613dd189aa9199ad0f9a11bf44deabbbd9a0", "profile-ruleset-rtruediv-b665fd3a"),
        new("RuleSet.__sub__", "sha256:d13292383b4ac45ca61e2e5a3af7f47116cecd9427402a0f82f2facc9a748e8f", "profile-ruleset-sub-d1329238"),
        new("RuleSet.__truediv__", "sha256:5ce5d9fa78fe66d885f07337ea82654b3976d0a954f4dbd9870d2abb08eb272e", "profile-ruleset-truediv-5ce5d9fa"),
        new("RuleSet.element_eq", "sha256:2d76198253866a17cebeac482806cb4c7172bdd1eac0247412d9e53f96a07f6e", "profile-ruleset-element-eq-2d761982"),
        new("RuleSet.element_max", "sha256:bfffae347ffeac971d2328d400ae28e5986cc2f3c60b707e0bb55431989edd39", "profile-ruleset-element-max-bfffae34"),
        new("RuleSet.element_min", "sha256:33739f88089372dfdb936c28027f53c69ca8ec4ec1f47ad24ff6d8b7fc427d64", "profile-ruleset-element-min-33739f88"),
        new("RuleSet.element_ne", "sha256:acaa0bfa9274b747da2f9096ecb5598d67e8bb6515846462ae53512c65fe6f60", "profile-ruleset-element-ne-acaa0bfa"),
        new("RuleSet.is_between", "sha256:1ada7d9d920d4732ef0bc1602db75a12d2cb97853e6cbbb5d0d7d32d15ec63e7", "profile-ruleset-is-between-1ada7d9d"),
        new("RuleSet.is_negative", "sha256:344049ce22623af29c4956fe51fd008ae546b6bbaedcaec1946037b00ef9d67e", "profile-ruleset-is-negative-344049ce"),
        new("RuleSet.is_nonzero", "sha256:12a2434cf468d99a4e259487daa1141861bf48c4b4973115adb76e2c3a24333f", "profile-ruleset-is-nonzero-12a2434c"),
        new("RuleSet.is_off", "sha256:8f8e714ff0d9a931906eee296428f6565c13fa22152d731f7c0e22e31e0c1f52", "profile-ruleset-is-off-8f8e714f"),
        new("RuleSet.is_on", "sha256:5c914c14bc867f961622cad9d503ee0103ac1f6e5bbffd860be6739fcc093592", "profile-ruleset-is-on-5c914c14"),
        new("RuleSet.is_positive", "sha256:7a7f9ce61c60171028a80e0e81f072ea7f86e59a5053360cec60843db6714247", "profile-ruleset-is-positive-7a7f9ce6"),
        new("RuleSet.is_zero", "sha256:8f8e714ff0d9a931906eee296428f6565c13fa22152d731f7c0e22e31e0c1f52", "profile-ruleset-is-zero-8f8e714f"),
        new("RuleSet.normalize_by_max", "sha256:92c2f28741585003d7e2bab24c4bff10cd1fa42133eb1cf6870409b37ec6ba55", "profile-ruleset-normalize-by-max-92c2f287"),
        new("RuleSet.where", "sha256:b245f2e84cd0e4b15b7f03d663409c07792e05f5143a8aae6a1e567769fa726a", "profile-ruleset-where-b245f2e8"),
    };

    private static readonly Dictionary<string, string> ExpectedAdaptations =
        new(StringComparer.Ordinal)
        {
            ["deterministic-ruleset-where-day-names"] = "RuleSet.where",
            ["nonfinite-result-ruleset-add"] = "RuleSet.__add__",
            ["nonfinite-result-ruleset-mul"] = "RuleSet.__mul__",
            ["nonfinite-result-ruleset-normalize-by-max"] = "RuleSet.normalize_by_max",
            ["nonfinite-result-ruleset-radd"] = "RuleSet.__radd__",
            ["nonfinite-result-ruleset-rmul"] = "RuleSet.__rmul__",
            ["nonfinite-result-ruleset-rsub"] = "RuleSet.__rsub__",
            ["nonfinite-result-ruleset-rtruediv"] = "RuleSet.__rtruediv__",
            ["nonfinite-result-ruleset-sub"] = "RuleSet.__sub__",
            ["nonfinite-result-ruleset-truediv"] = "RuleSet.__truediv__",
            ["ruleset-scalar-maximum-upstream-attribute-error"] = "RuleSet.element_max",
            ["ruleset-scalar-minimum-upstream-attribute-error"] = "RuleSet.element_min",
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
        AssertFixtureCardinality(root.GetProperty("summary"), cases);
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
                            repair_reference = item.RepairReference,
                            registered_adaptation = item.RegisteredAdaptation,
                        }).ToArray(),
                    },
                    upstream_symbol = binding.Symbol,
                });
        }
    }

    private static void AssertFixtureCardinality(JsonElement summary, JsonElement[] cases)
    {
        Assert.Equal(ExpectedCaseCount, cases.Length);
        Assert.Equal(ExpectedCaseCount, summary.GetProperty("case_count").GetInt32());
        Assert.Equal(ExpectedAdaptationCaseCount, summary.GetProperty("adaptation_case_count").GetInt32());
        Assert.Equal(ExpectedRepairReferenceCount, summary.GetProperty("repair_reference_count").GetInt32());

        JsonElement observed = summary.GetProperty("observed_outcomes");
        Assert.Equal(101, observed.GetProperty("raised").GetInt32());
        Assert.Equal(233, observed.GetProperty("returned").GetInt32());
        JsonElement expectedOutcomes = summary.GetProperty("expected_dotnet_outcomes");
        Assert.Equal(27, expectedOutcomes.GetProperty("raised").GetInt32());
        Assert.Equal(34, expectedOutcomes.GetProperty("returned").GetInt32());
        Assert.Equal(
            ExpectedAdaptations.Keys.OrderBy(item => item, StringComparer.Ordinal),
            summary.GetProperty("adaptation_ids").EnumerateArray().Select(item => item.GetString()!)
                .OrderBy(item => item, StringComparer.Ordinal));

        JsonElement[] adapted = cases.Where(item => item.TryGetProperty("expected_dotnet", out _)).ToArray();
        Assert.Equal(ExpectedAdaptationCaseCount, adapted.Length);
        Assert.Equal(ExpectedRepairReferenceCount, cases.Count(item => item.TryGetProperty("repair_reference", out _)));
        Assert.Equal(18, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "reject-nonfinite-result"));
        Assert.Equal(14, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "deterministic-slot-names"));
        Assert.Equal(22, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "match-repair-reference"));
        Assert.Equal(3, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "reject-invalid-name"));
        Assert.Equal(2, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "reject-nonfinite-repair-result"));
        Assert.Equal(1, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "trim-name-and-deterministic-slot-names"));
        Assert.Equal(1, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "trim-result-name"));

        foreach (JsonElement operationCase in adapted)
        {
            JsonElement expected = operationCase.GetProperty("expected_dotnet");
            string adaptation = RequiredString(expected, "adaptation");
            Assert.True(ExpectedAdaptations.ContainsKey(adaptation));
            Assert.Equal(ExpectedAdaptations[adaptation], RequiredString(operationCase, "symbol"));
            string outcome = RequiredString(expected, "outcome");
            Assert.Contains(outcome, new[] { "raised", "returned" });
            string policy = RequiredString(expected, "policy");
            Assert.Contains(policy, new[]
            {
                "deterministic-slot-names",
                "match-repair-reference",
                "reject-invalid-name",
                "reject-nonfinite-repair-result",
                "reject-nonfinite-result",
                "trim-name-and-deterministic-slot-names",
                "trim-result-name",
            });
            if (policy.Contains("repair", StringComparison.Ordinal))
            {
                Assert.Equal("repair_reference", RequiredString(expected, "reference"));
                Assert.True(operationCase.TryGetProperty("repair_reference", out _));
            }
        }
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
        NormalizedOutcome? repair = null;
        if (operationCase.TryGetProperty("repair_reference", out JsonElement repairReference))
        {
            Assert.Equal("scalar-other-name-read-only", RequiredString(repairReference, "bypass"));
            JsonElement repairObservation = repairReference.GetProperty("observation");
            AssertPythonInputPostconditions(repairObservation, context);
            repair = NormalizePythonOutcome(repairObservation);
        }

        ExpectedDotnetOutcome? expectedDotnet = ReadExpectedDotnetOutcome(operationCase);
        if (expectedDotnet is null)
        {
            Assert.Null(repair);
            Assert.Equal(Serialize(python), Serialize(dotnet));
        }
        else
        {
            AssertExpectedDotnetOutcome(expectedDotnet, python, repair, dotnet);
        }

        return new CaseEvidence(
            caseId,
            symbol,
            JsonSerializer.SerializeToElement(python, EvidenceJsonOptions),
            JsonSerializer.SerializeToElement(dotnet, EvidenceJsonOptions),
            repair is null ? null : JsonSerializer.SerializeToElement(repair, EvidenceJsonOptions),
            expectedDotnet?.Adaptation);
    }

    private static ExpectedDotnetOutcome? ReadExpectedDotnetOutcome(JsonElement operationCase)
    {
        if (!operationCase.TryGetProperty("expected_dotnet", out JsonElement expected))
        {
            return null;
        }

        string adaptation = RequiredString(expected, "adaptation");
        Assert.True(ExpectedAdaptations.ContainsKey(adaptation));
        Assert.Equal(ExpectedAdaptations[adaptation], RequiredString(operationCase, "symbol"));
        return new ExpectedDotnetOutcome(
            adaptation,
            RequiredString(expected, "outcome"),
            RequiredString(expected, "policy"),
            expected.TryGetProperty("error_category", out JsonElement category)
                ? RequiredStringValue(category)
                : null,
            expected.TryGetProperty("reference", out JsonElement reference)
                ? RequiredStringValue(reference)
                : null,
            expected.TryGetProperty("result_name", out JsonElement resultName)
                ? RequiredStringValue(resultName)
                : null);
    }

    private static void AssertExpectedDotnetOutcome(
        ExpectedDotnetOutcome expected,
        NormalizedOutcome python,
        NormalizedOutcome? repair,
        NormalizedOutcome dotnet)
    {
        switch (expected.Policy)
        {
            case "deterministic-slot-names":
                Assert.Null(repair);
                Assert.Equal("returned", expected.Outcome);
                Assert.Null(expected.ErrorCategory);
                Assert.Null(expected.ResultName);
                AssertDeterministicSlotNames(python, dotnet, null);
                return;
            case "match-repair-reference":
                Assert.Equal("repair_reference", expected.Reference);
                Assert.NotNull(repair);
                Assert.Equal(expected.Outcome, repair!.Outcome);
                Assert.Equal(expected.ErrorCategory, repair.ErrorCategory);
                Assert.Equal(Serialize(repair), Serialize(dotnet));
                return;
            case "reject-nonfinite-repair-result":
                Assert.Equal("repair_reference", expected.Reference);
                Assert.NotNull(repair);
                Assert.Equal("returned", repair!.Outcome);
                AssertRejectedOutcome(expected, repair, dotnet);
                return;
            case "reject-nonfinite-result":
                Assert.Null(repair);
                AssertRejectedOutcome(expected, python, dotnet);
                return;
            case "reject-invalid-name":
                Assert.Null(repair);
                AssertRejectedOutcome(expected, python, dotnet);
                return;
            case "trim-name-and-deterministic-slot-names":
                Assert.Null(repair);
                Assert.Equal("returned", expected.Outcome);
                Assert.Null(expected.ErrorCategory);
                Assert.False(string.IsNullOrWhiteSpace(expected.ResultName));
                AssertDeterministicSlotNames(python, dotnet, expected.ResultName);
                return;
            case "trim-result-name":
                Assert.Null(repair);
                Assert.Equal("returned", expected.Outcome);
                Assert.Null(expected.ErrorCategory);
                Assert.False(string.IsNullOrWhiteSpace(expected.ResultName));
                AssertTrimmedResultName(python, dotnet, expected.ResultName!);
                return;
            default:
                throw new InvalidDataException($"Unknown expected .NET policy '{expected.Policy}'.");
        }
    }

    private static void AssertRejectedOutcome(
        ExpectedDotnetOutcome expected,
        NormalizedOutcome reference,
        NormalizedOutcome dotnet)
    {
        Assert.Equal("raised", expected.Outcome);
        Assert.Equal("raised", dotnet.Outcome);
        Assert.Equal(expected.ErrorCategory, dotnet.ErrorCategory);
        Assert.Equal("none", dotnet.ResultKind);
        Assert.Equal("none", dotnet.ResultIdentity);
        Assert.Null(dotnet.Result);
        Assert.Equal(Serialize(reference.InputPostconditions), Serialize(dotnet.InputPostconditions));
    }

    private static void AssertDeterministicSlotNames(
        NormalizedOutcome python,
        NormalizedOutcome dotnet,
        string? expectedDotnetName)
    {
        Assert.Equal("returned", python.Outcome);
        Assert.Equal("returned", dotnet.Outcome);
        Assert.Equal("ruleset", python.ResultKind);
        Assert.Equal("ruleset", dotnet.ResultKind);
        Assert.Equal("new", python.ResultIdentity);
        Assert.Equal("new", dotnet.ResultIdentity);
        Assert.NotNull(python.Result);
        Assert.NotNull(dotnet.Result);
        if (expectedDotnetName is null)
        {
            Assert.Equal(python.Result!.Name, dotnet.Result!.Name);
        }
        else
        {
            Assert.NotEqual(python.Result!.Name, expectedDotnetName);
            Assert.Equal(expectedDotnetName, dotnet.Result!.Name);
        }

        AssertRuleSetExceptOuterAndChildNames(python.Result!, dotnet.Result!);
        Assert.Equal(
            Serialize(python.InputPostconditions),
            Serialize(dotnet.InputPostconditions));

        foreach (string slot in SlotKeys)
        {
            ScheduleSnapshot? pythonDay = python.Result.Slots[slot];
            ScheduleSnapshot? dotnetDay = dotnet.Result.Slots[slot];
            if (pythonDay is null)
            {
                Assert.Null(dotnetDay);
                continue;
            }

            Assert.NotNull(dotnetDay);
            Assert.Equal("runtime-identity-hex", pythonDay.NamePolicy);
            Assert.Null(pythonDay.Name);
            Assert.Equal("literal", dotnetDay!.NamePolicy);
            Assert.Equal($"{dotnet.Result.Name}:{slot}", dotnetDay.Name);
        }
    }

    private static void AssertTrimmedResultName(
        NormalizedOutcome python,
        NormalizedOutcome dotnet,
        string expectedDotnetName)
    {
        Assert.Equal("returned", python.Outcome);
        Assert.Equal("returned", dotnet.Outcome);
        Assert.Equal("ruleset", python.ResultKind);
        Assert.Equal("ruleset", dotnet.ResultKind);
        Assert.Equal("new", python.ResultIdentity);
        Assert.Equal("new", dotnet.ResultIdentity);
        Assert.NotNull(python.Result);
        Assert.NotNull(dotnet.Result);
        Assert.NotEqual(python.Result!.Name, expectedDotnetName);
        Assert.Equal(expectedDotnetName, dotnet.Result!.Name);
        AssertRuleSetExceptOuterName(python.Result, dotnet.Result);
        Assert.Equal(
            Serialize(python.InputPostconditions),
            Serialize(dotnet.InputPostconditions));
    }

    private static RuleSet Dispatch(
        string symbol,
        JsonElement inputs,
        OperationContext context)
    {
        RuleSet Receiver() => context.RuleSet("receiver");
        return symbol switch
        {
            "RuleSet.__add__" => Add(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.__and__" => Receiver() & context.RuleSet("other"),
            "RuleSet.__ge__" => Compare(Receiver(), inputs.GetProperty("other"), context, "ge"),
            "RuleSet.__gt__" => Compare(Receiver(), inputs.GetProperty("other"), context, "gt"),
            "RuleSet.__invert__" => !Receiver(),
            "RuleSet.__le__" => Compare(Receiver(), inputs.GetProperty("other"), context, "le"),
            "RuleSet.__lt__" => Compare(Receiver(), inputs.GetProperty("other"), context, "lt"),
            "RuleSet.__mul__" => Multiply(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.__or__" => Receiver() | context.RuleSet("other"),
            "RuleSet.__radd__" => ReverseAdd(Receiver(), ReadScalar(inputs, "other")),
            "RuleSet.__rmul__" => ReverseMultiply(Receiver(), ReadScalar(inputs, "other")),
            "RuleSet.__rsub__" => ReverseSubtract(Receiver(), ReadScalar(inputs, "other")),
            "RuleSet.__rtruediv__" => ReverseDivide(Receiver(), ReadScalar(inputs, "other")),
            "RuleSet.__sub__" => Subtract(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.__truediv__" => Divide(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.element_eq" => ElementEqual(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.element_max" => ElementMaximum(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.element_min" => ElementMinimum(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.element_ne" => ElementNotEqual(Receiver(), inputs.GetProperty("other"), context),
            "RuleSet.is_between" => IsBetween(Receiver(), inputs),
            "RuleSet.is_negative" => Receiver().IsNegative(),
            "RuleSet.is_nonzero" => Receiver().IsNonzero(),
            "RuleSet.is_off" => Receiver().IsOff(),
            "RuleSet.is_on" => Receiver().IsOn(),
            "RuleSet.is_positive" => Receiver().IsPositive(),
            "RuleSet.is_zero" => Receiver().IsZero(),
            "RuleSet.normalize_by_max" => Receiver().NormalizeByMaximum(OptionalText(inputs, "new_name")),
            "RuleSet.where" => Where(inputs, context),
            _ => throw new InvalidDataException($"Unknown RuleSet oracle symbol '{symbol}'."),
        };
    }

    private static RuleSet Add(RuleSet receiver, JsonElement other, OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver + context.RuleSet("other")
            : AddScalar(receiver, ReadScalar(other));
    }

    private static RuleSet Subtract(RuleSet receiver, JsonElement other, OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver - context.RuleSet("other")
            : SubtractScalar(receiver, ReadScalar(other));
    }

    private static RuleSet Multiply(RuleSet receiver, JsonElement other, OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver * context.RuleSet("other")
            : MultiplyScalar(receiver, ReadScalar(other));
    }

    private static RuleSet Divide(RuleSet receiver, JsonElement other, OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver / context.RuleSet("other")
            : DivideScalar(receiver, ReadScalar(other));
    }

    private static RuleSet AddScalar(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value);
    }

    private static RuleSet SubtractScalar(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value);
    }

    private static RuleSet MultiplyScalar(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value);
    }

    private static RuleSet DivideScalar(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value);
    }

    private static RuleSet ReverseAdd(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver);
    }

    private static RuleSet ReverseMultiply(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver);
    }

    private static RuleSet ReverseSubtract(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver);
    }

    private static RuleSet ReverseDivide(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver);
    }

    private static RuleSet Compare(
        RuleSet receiver,
        JsonElement other,
        OperationContext context,
        string operation)
    {
        if (IsRuleSet(other))
        {
            RuleSet value = context.RuleSet("other");
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

    private static RuleSet GreaterThanOrEqual(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.GreaterThanOrEqual,
            receiver.GreaterThanOrEqual,
            receiver.GreaterThanOrEqual,
            receiver.GreaterThanOrEqual,
            receiver.GreaterThanOrEqual);
    }

    private static RuleSet GreaterThan(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.GreaterThan,
            receiver.GreaterThan,
            receiver.GreaterThan,
            receiver.GreaterThan,
            receiver.GreaterThan);
    }

    private static RuleSet LessThanOrEqual(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.LessThanOrEqual,
            receiver.LessThanOrEqual,
            receiver.LessThanOrEqual,
            receiver.LessThanOrEqual,
            receiver.LessThanOrEqual);
    }

    private static RuleSet LessThan(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.LessThan,
            receiver.LessThan,
            receiver.LessThan,
            receiver.LessThan,
            receiver.LessThan);
    }

    private static RuleSet ElementEqual(
        RuleSet receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver.ElementEqual(context.RuleSet("other"))
            : ElementEqual(receiver, ReadScalar(other));
    }

    private static RuleSet ElementEqual(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementEqual,
            receiver.ElementEqual,
            receiver.ElementEqual,
            receiver.ElementEqual,
            receiver.ElementEqual);
    }

    private static RuleSet ElementNotEqual(
        RuleSet receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver.ElementNotEqual(context.RuleSet("other"))
            : ElementNotEqual(receiver, ReadScalar(other));
    }

    private static RuleSet ElementNotEqual(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementNotEqual,
            receiver.ElementNotEqual,
            receiver.ElementNotEqual,
            receiver.ElementNotEqual,
            receiver.ElementNotEqual);
    }

    private static RuleSet ElementMinimum(
        RuleSet receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver.ElementMinimum(context.RuleSet("other"))
            : ElementMinimum(receiver, ReadScalar(other));
    }

    private static RuleSet ElementMinimum(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementMinimum,
            receiver.ElementMinimum,
            receiver.ElementMinimum,
            receiver.ElementMinimum,
            receiver.ElementMinimum);
    }

    private static RuleSet ElementMaximum(
        RuleSet receiver,
        JsonElement other,
        OperationContext context)
    {
        return IsRuleSet(other)
            ? receiver.ElementMaximum(context.RuleSet("other"))
            : ElementMaximum(receiver, ReadScalar(other));
    }

    private static RuleSet ElementMaximum(RuleSet receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            receiver.ElementMaximum,
            receiver.ElementMaximum,
            receiver.ElementMaximum,
            receiver.ElementMaximum,
            receiver.ElementMaximum);
    }

    private static RuleSet IsBetween(RuleSet receiver, JsonElement inputs)
    {
        OracleScalar minimum = ReadScalar(inputs, "min_value");
        OracleScalar maximum = ReadScalar(inputs, "max_value");
        return receiver.IsBetween(
            minimum.ClrValue,
            maximum.ClrValue,
            Boolean(inputs, "include_min"),
            Boolean(inputs, "include_max"));
    }

    private static RuleSet DispatchScalar(
        OracleScalar scalar,
        Func<int, RuleSet> integerOperation,
        Func<long, RuleSet> longIntegerOperation,
        Func<BigInteger, RuleSet> unboundedIntegerOperation,
        Func<double, RuleSet> floatOperation,
        Func<bool, RuleSet> booleanOperation)
    {
        return scalar.PythonType switch
        {
            "int" when scalar.IntegerValue >= int.MinValue
                && scalar.IntegerValue <= int.MaxValue =>
                integerOperation((int)scalar.IntegerValue),
            "int" when scalar.IntegerValue >= long.MinValue
                && scalar.IntegerValue <= long.MaxValue =>
                longIntegerOperation((long)scalar.IntegerValue),
            "int" => unboundedIntegerOperation(scalar.IntegerValue),
            "float" => floatOperation(scalar.FloatValue),
            "bool" => booleanOperation(scalar.BooleanValue),
            _ => throw new InvalidDataException($"Unknown scalar type '{scalar.PythonType}'."),
        };
    }

    private static RuleSet Where(JsonElement inputs, OperationContext context)
    {
        return RuleSet.Where(
            context.RuleSet("condition"),
            ReadWhereOperand(inputs, "if_true", context),
            ReadWhereOperand(inputs, "if_false", context),
            OptionalText(inputs, "name"),
            OptionalScheduleType(inputs, "type"));
    }

    private static object ReadWhereOperand(
        JsonElement inputs,
        string name,
        OperationContext context)
    {
        JsonElement descriptor = inputs.GetProperty(name);
        return RequiredString(descriptor, "kind") switch
        {
            "ruleset" => context.RuleSet(name),
            "day-schedule" => context.DaySchedule(name),
            "text" => RequiredString(descriptor, "value"),
            _ => ReadScalar(descriptor).ClrValue,
        };
    }

    private static CapturedExecution Capture(Func<RuleSet> action)
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
        InputPostconditions inputPostconditions = ReadInputPostconditions(observation);
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
                inputPostconditions);
        }

        Assert.Equal("returned", outcome);
        string identity = RequiredString(observation, "result_identity");
        JsonElement result = observation.GetProperty("result");
        Assert.Equal("ruleset", RequiredString(result, "kind"));
        return new NormalizedOutcome(
            outcome,
            "ruleset",
            identity,
            ReadRuleSetSnapshot(result),
            null,
            inputPostconditions);
    }

    private static NormalizedOutcome NormalizeDotnetOutcome(
        CapturedExecution execution,
        OperationContext context)
    {
        InputPostconditions inputPostconditions = context.CurrentPostconditions();
        if (execution.Exception is not null)
        {
            return new NormalizedOutcome(
                "raised",
                "none",
                "none",
                null,
                DotnetErrorCategory(execution.Exception),
                inputPostconditions);
        }

        RuleSet result = Assert.IsType<RuleSet>(execution.Result);
        string identity = context.RuleSets.TryGetValue("receiver", out RuleSet? receiver)
            && ReferenceEquals(receiver, result)
            ? "receiver"
            : "new";
        return new NormalizedOutcome(
            "returned",
            "ruleset",
            identity,
            RuleSetSnapshot.From(result),
            null,
            inputPostconditions);
    }

    private static void AssertPythonInputPostconditions(
        JsonElement observation,
        OperationContext context)
    {
        InputPostconditions postconditions = ReadInputPostconditions(observation);
        Assert.Equal(
            context.RuleSets.Keys.OrderBy(item => item, StringComparer.Ordinal),
            postconditions.RuleSets.Keys);
        Assert.Equal(
            context.DaySchedules.Keys.OrderBy(item => item, StringComparer.Ordinal),
            postconditions.DaySchedules.Keys);

        foreach ((string name, RuleSetInputState state) in postconditions.RuleSets)
        {
            Assert.Equal("preserved", state.Identity);
            Assert.Equal("unchanged", state.Status);
            RuleSetInputBefore before = context.RuleSetBefore[name];
            foreach (string slot in SlotKeys)
            {
                SlotInputState slotState = state.Slots[slot];
                Assert.Equal("unchanged", slotState.Status);
                Assert.Equal(before.SlotReferences[slot] is null ? "none" : "preserved", slotState.Identity);
            }
        }

        foreach (InputObjectState state in postconditions.DaySchedules.Values)
        {
            Assert.Equal("preserved", state.Identity);
            Assert.Equal("unchanged", state.Status);
        }
    }

    private static InputPostconditions ReadInputPostconditions(JsonElement observation)
    {
        SortedDictionary<string, RuleSetInputState> rulesets = new(StringComparer.Ordinal);
        foreach (JsonProperty property in observation.GetProperty("ruleset_inputs_after").EnumerateObject())
        {
            JsonElement value = property.Value;
            SortedDictionary<string, SlotInputState> slots = new(StringComparer.Ordinal);
            JsonElement slotElement = value.GetProperty("slots");
            AssertExactKeys(slotElement, SlotKeys);
            foreach (string slot in SlotKeys)
            {
                JsonElement slotState = slotElement.GetProperty(slot);
                slots.Add(
                    slot,
                    new SlotInputState(
                        RequiredString(slotState, "identity"),
                        RequiredString(slotState, "status")));
            }

            string status = RequiredString(value, "status");
            if (status == "unchanged")
            {
                Assert.False(value.TryGetProperty("value", out _));
            }
            else
            {
                Assert.Equal("changed", status);
                _ = ReadRuleSetSnapshot(value.GetProperty("value"));
            }

            rulesets.Add(
                property.Name,
                new RuleSetInputState(RequiredString(value, "identity"), status, slots));
        }

        SortedDictionary<string, InputObjectState> days = new(StringComparer.Ordinal);
        foreach (JsonProperty property in observation.GetProperty("day_schedule_inputs_after").EnumerateObject())
        {
            JsonElement value = property.Value;
            string status = RequiredString(value, "status");
            if (status == "unchanged")
            {
                Assert.False(value.TryGetProperty("value", out _));
            }
            else
            {
                Assert.Equal("changed", status);
                _ = ReadScheduleSnapshot(value.GetProperty("value"));
            }

            days.Add(
                property.Name,
                new InputObjectState(RequiredString(value, "identity"), status));
        }

        return new InputPostconditions(rulesets, days);
    }

    private static RuleSetSnapshot ReadRuleSetSnapshot(JsonElement descriptor)
    {
        Assert.Equal("ruleset", RequiredString(descriptor, "kind"));
        NameDescriptor name = ReadNameDescriptor(descriptor.GetProperty("name"));
        Assert.Equal("literal", name.Policy);
        Assert.NotNull(name.Value);

        JsonElement slotsElement = descriptor.GetProperty("slots");
        AssertExactKeys(slotsElement, SlotKeys);
        SortedDictionary<string, ScheduleSnapshot?> slots = new(StringComparer.Ordinal);
        foreach (string slot in SlotKeys)
        {
            JsonElement schedule = slotsElement.GetProperty(slot);
            slots.Add(
                slot,
                schedule.ValueKind == JsonValueKind.Null ? null : ReadScheduleSnapshot(schedule));
        }

        SortedDictionary<string, string> sources = ReadEffectiveSources(
            descriptor.GetProperty("effective_slot_sources"));
        AssertEffectiveSources(slots, sources);
        return new RuleSetSnapshot(
            name.Value!,
            RequiredString(descriptor, "schedule_type"),
            slots,
            sources);
    }

    private static ScheduleSnapshot ReadScheduleSnapshot(JsonElement descriptor)
    {
        Assert.Equal("day-schedule", RequiredString(descriptor, "kind"));
        NameDescriptor name = ReadNameDescriptor(descriptor.GetProperty("name"));
        return new ScheduleSnapshot(
            name.Policy,
            name.Value,
            RequiredString(descriptor, "schedule_type"),
            NullableString(descriptor.GetProperty("unit")),
            DecodeValues(descriptor.GetProperty("values")));
    }

    private static NameDescriptor ReadNameDescriptor(JsonElement descriptor)
    {
        Assert.Equal(JsonValueKind.Object, descriptor.ValueKind);
        string policy = RequiredString(descriptor, "policy");
        Assert.Contains(policy, new[] { "literal", "runtime-identity-hex" });
        if (policy == "runtime-identity-hex")
        {
            Assert.False(descriptor.TryGetProperty("value", out _));
            return new NameDescriptor(policy, null);
        }

        return new NameDescriptor(policy, RequiredString(descriptor, "value"));
    }

    private static SortedDictionary<string, string> ReadEffectiveSources(JsonElement descriptor)
    {
        AssertExactKeys(descriptor, OverrideKeys);
        SortedDictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string slot in OverrideKeys)
        {
            string source = RequiredString(descriptor, slot);
            Assert.Contains(source, new[] { slot, IsWeekdaySlot(slot) ? "weekdays" : "weekends" });
            result.Add(slot, source);
        }

        return result;
    }

    private static void AssertEffectiveSources(
        SortedDictionary<string, ScheduleSnapshot?> slots,
        SortedDictionary<string, string> sources)
    {
        foreach (string slot in OverrideKeys)
        {
            string expected = slots[slot] is not null
                ? slot
                : IsWeekdaySlot(slot) ? "weekdays" : "weekends";
            Assert.Equal(expected, sources[slot]);
        }
    }

    private static bool IsWeekdaySlot(string slot)
    {
        return slot is "monday" or "tuesday" or "wednesday" or "thursday" or "friday";
    }

    private static void AssertRuleSetExceptOuterAndChildNames(
        RuleSetSnapshot expected,
        RuleSetSnapshot actual)
    {
        Assert.Equal(expected.ScheduleType, actual.ScheduleType);
        Assert.Equal(Serialize(expected.EffectiveSlotSources), Serialize(actual.EffectiveSlotSources));
        Assert.Equal(expected.Slots.Keys, actual.Slots.Keys);
        foreach (string slot in SlotKeys)
        {
            ScheduleSnapshot? expectedDay = expected.Slots[slot];
            ScheduleSnapshot? actualDay = actual.Slots[slot];
            if (expectedDay is null)
            {
                Assert.Null(actualDay);
            }
            else
            {
                Assert.NotNull(actualDay);
                AssertScheduleExceptName(expectedDay, actualDay!);
            }
        }
    }

    private static void AssertRuleSetExceptOuterName(
        RuleSetSnapshot expected,
        RuleSetSnapshot actual)
    {
        Assert.Equal(expected.ScheduleType, actual.ScheduleType);
        Assert.Equal(Serialize(expected.EffectiveSlotSources), Serialize(actual.EffectiveSlotSources));
        Assert.Equal(expected.Slots.Keys, actual.Slots.Keys);
        foreach (string slot in SlotKeys)
        {
            ScheduleSnapshot? expectedDay = expected.Slots[slot];
            ScheduleSnapshot? actualDay = actual.Slots[slot];
            if (expectedDay is null)
            {
                Assert.Null(actualDay);
            }
            else
            {
                Assert.NotNull(actualDay);
                Assert.True(expectedDay.SameAs(actualDay!));
            }
        }
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
            decoded = Enumerable.Range(0, length)
                .Select(index => pattern[index % pattern.Length])
                .ToArray();
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
        OracleScalar scalar = ReadScalar(inputs, name);
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

    private static bool IsRuleSet(JsonElement descriptor)
    {
        return RequiredString(descriptor, "kind") == "ruleset";
    }

    private static string PythonErrorCategory(string type)
    {
        return type switch
        {
            "ScheduleOperationError" => "schedule-operation",
            "ZeroDivisionError" => "divide-by-zero",
            "ValueError" => "domain",
            "OverflowError" => "domain",
            "AttributeError" => "type",
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

    private static void AssertExactKeys(JsonElement value, IEnumerable<string> expected)
    {
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name)
                .OrderBy(item => item, StringComparer.Ordinal));
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, EvidenceJsonOptions);
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        return RequiredStringValue(parent.GetProperty(name));
    }

    private static string RequiredStringValue(JsonElement value)
    {
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
            Dictionary<string, RuleSet> ruleSets,
            Dictionary<string, DaySchedule> daySchedules,
            Dictionary<string, RuleSetInputBefore> ruleSetBefore,
            Dictionary<string, ScheduleSnapshot> dayScheduleBefore)
        {
            RuleSets = ruleSets;
            DaySchedules = daySchedules;
            RuleSetBefore = ruleSetBefore;
            DayScheduleBefore = dayScheduleBefore;
        }

        public Dictionary<string, RuleSet> RuleSets { get; }

        public Dictionary<string, DaySchedule> DaySchedules { get; }

        public Dictionary<string, RuleSetInputBefore> RuleSetBefore { get; }

        public Dictionary<string, ScheduleSnapshot> DayScheduleBefore { get; }

        public RuleSet RuleSet(string name)
        {
            return RuleSets[name];
        }

        public DaySchedule DaySchedule(string name)
        {
            return DaySchedules[name];
        }

        public static OperationContext Create(JsonElement inputs)
        {
            Dictionary<string, RuleSet> ruleSets = new(StringComparer.Ordinal);
            Dictionary<string, DaySchedule> days = new(StringComparer.Ordinal);
            Dictionary<string, RuleSetInputBefore> ruleSetBefore = new(StringComparer.Ordinal);
            Dictionary<string, ScheduleSnapshot> dayBefore = new(StringComparer.Ordinal);
            foreach (JsonProperty property in inputs.EnumerateObject())
            {
                string kind = RequiredString(property.Value, "kind");
                if (kind == "ruleset")
                {
                    RuleSetSnapshot expected = ReadRuleSetSnapshot(property.Value);
                    RuleSet ruleset = CreateRuleSet(expected);
                    RuleSetSnapshot actual = RuleSetSnapshot.From(ruleset);
                    Assert.True(expected.SameAs(actual));
                    ruleSets.Add(property.Name, ruleset);
                    ruleSetBefore.Add(
                        property.Name,
                        new RuleSetInputBefore(actual, RuleSetSlotReferences(ruleset)));
                }
                else if (kind == "day-schedule")
                {
                    ScheduleSnapshot expected = ReadScheduleSnapshot(property.Value);
                    DaySchedule day = CreateDaySchedule(expected);
                    Assert.True(expected.SameAs(ScheduleSnapshot.From(day)));
                    days.Add(property.Name, day);
                    dayBefore.Add(property.Name, ScheduleSnapshot.From(day));
                }
            }

            return new OperationContext(ruleSets, days, ruleSetBefore, dayBefore);
        }

        public InputPostconditions CurrentPostconditions()
        {
            SortedDictionary<string, RuleSetInputState> ruleStates = new(StringComparer.Ordinal);
            foreach ((string name, RuleSet ruleset) in RuleSets.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                RuleSetInputBefore before = RuleSetBefore[name];
                SortedDictionary<string, DaySchedule?> currentReferences = RuleSetSlotReferences(ruleset);
                SortedDictionary<string, SlotInputState> slots = new(StringComparer.Ordinal);
                foreach (string slot in SlotKeys)
                {
                    DaySchedule? oldValue = before.SlotReferences[slot];
                    DaySchedule? newValue = currentReferences[slot];
                    string identity = newValue is null
                        ? "none"
                        : ReferenceEquals(oldValue, newValue) ? "preserved" : "replaced";
                    string status = ScheduleValuesSame(oldValue, newValue) ? "unchanged" : "changed";
                    slots.Add(slot, new SlotInputState(identity, status));
                }

                ruleStates.Add(
                    name,
                    new RuleSetInputState(
                        "preserved",
                        before.Snapshot.SameAs(RuleSetSnapshot.From(ruleset)) ? "unchanged" : "changed",
                        slots));
            }

            SortedDictionary<string, InputObjectState> dayStates = new(StringComparer.Ordinal);
            foreach ((string name, DaySchedule day) in DaySchedules.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                dayStates.Add(
                    name,
                    new InputObjectState(
                        "preserved",
                        DayScheduleBefore[name].SameAs(ScheduleSnapshot.From(day))
                            ? "unchanged"
                            : "changed"));
            }

            return new InputPostconditions(ruleStates, dayStates);
        }
    }

    private static RuleSet CreateRuleSet(RuleSetSnapshot snapshot)
    {
        SortedDictionary<string, DaySchedule?> days = new(StringComparer.Ordinal);
        foreach (string slot in SlotKeys)
        {
            days.Add(
                slot,
                snapshot.Slots[slot] is null ? null : CreateDaySchedule(snapshot.Slots[slot]!));
        }

        return new RuleSet(
            snapshot.Name,
            days["weekdays"],
            days["weekends"],
            days["monday"],
            days["tuesday"],
            days["wednesday"],
            days["thursday"],
            days["friday"],
            days["saturday"],
            days["sunday"],
            days["holiday"],
            ParseScheduleType(snapshot.ScheduleType));
    }

    private static DaySchedule CreateDaySchedule(ScheduleSnapshot snapshot)
    {
        Assert.Equal("literal", snapshot.NamePolicy);
        Assert.NotNull(snapshot.Name);
        return new DaySchedule(
            snapshot.Name!,
            snapshot.Values,
            ParseScheduleType(snapshot.ScheduleType),
            snapshot.Unit);
    }

    private static SortedDictionary<string, DaySchedule?> RuleSetSlotReferences(RuleSet ruleset)
    {
        return new SortedDictionary<string, DaySchedule?>(StringComparer.Ordinal)
        {
            ["weekdays"] = ruleset.Weekdays,
            ["weekends"] = ruleset.Weekends,
            ["monday"] = ruleset.Monday,
            ["tuesday"] = ruleset.Tuesday,
            ["wednesday"] = ruleset.Wednesday,
            ["thursday"] = ruleset.Thursday,
            ["friday"] = ruleset.Friday,
            ["saturday"] = ruleset.Saturday,
            ["sunday"] = ruleset.Sunday,
            ["holiday"] = ruleset.Holiday,
        };
    }

    private static bool ScheduleValuesSame(DaySchedule? left, DaySchedule? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return ScheduleSnapshot.From(left).SameAs(ScheduleSnapshot.From(right));
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
            return new OracleScalar("float", BigInteger.Zero, value, value != 0d);
        }

        public static OracleScalar FromBoolean(bool value)
        {
            return new OracleScalar("bool", value ? BigInteger.One : BigInteger.Zero, value ? 1d : 0d, value);
        }
    }

    private sealed record CapturedExecution(RuleSet? Result, Exception? Exception);

    private sealed record ExpectedDotnetOutcome(
        string Adaptation,
        string Outcome,
        string Policy,
        string? ErrorCategory,
        string? Reference,
        string? ResultName);

    private sealed record CaseEvidence(
        string CaseId,
        string Symbol,
        JsonElement Python,
        JsonElement Dotnet,
        JsonElement? RepairReference,
        string? RegisteredAdaptation);

    private sealed record NormalizedOutcome(
        string Outcome,
        string ResultKind,
        string ResultIdentity,
        RuleSetSnapshot? Result,
        string? ErrorCategory,
        InputPostconditions InputPostconditions);

    private sealed record InputPostconditions(
        SortedDictionary<string, RuleSetInputState> RuleSets,
        SortedDictionary<string, InputObjectState> DaySchedules);

    private sealed record RuleSetInputState(
        string Identity,
        string Status,
        SortedDictionary<string, SlotInputState> Slots);

    private sealed record SlotInputState(string Identity, string Status);

    private sealed record InputObjectState(string Identity, string Status);

    private sealed record RuleSetInputBefore(
        RuleSetSnapshot Snapshot,
        SortedDictionary<string, DaySchedule?> SlotReferences);

    private sealed record NameDescriptor(string Policy, string? Value);

    private sealed record RuleSetSnapshot(
        string Name,
        string ScheduleType,
        SortedDictionary<string, ScheduleSnapshot?> Slots,
        SortedDictionary<string, string> EffectiveSlotSources)
    {
        public static RuleSetSnapshot From(RuleSet ruleset)
        {
            SortedDictionary<string, DaySchedule?> references = RuleSetSlotReferences(ruleset);
            SortedDictionary<string, ScheduleSnapshot?> slots = new(StringComparer.Ordinal);
            foreach (string slot in SlotKeys)
            {
                DaySchedule? day = references[slot];
                slots.Add(slot, day is null ? null : ScheduleSnapshot.From(day));
            }

            SortedDictionary<string, string> sources = new(StringComparer.Ordinal);
            foreach (string slot in OverrideKeys)
            {
                sources.Add(
                    slot,
                    references[slot] is not null
                        ? slot
                        : IsWeekdaySlot(slot) ? "weekdays" : "weekends");
            }

            return new RuleSetSnapshot(
                ruleset.Name,
                ruleset.Type.CanonicalName(),
                slots,
                sources);
        }

        public bool SameAs(RuleSetSnapshot other)
        {
            if (Name != other.Name
                || ScheduleType != other.ScheduleType
                || !EffectiveSlotSources.SequenceEqual(other.EffectiveSlotSources))
            {
                return false;
            }

            foreach (string slot in SlotKeys)
            {
                ScheduleSnapshot? left = Slots[slot];
                ScheduleSnapshot? right = other.Slots[slot];
                if (left is null || right is null)
                {
                    if (left is not null || right is not null)
                    {
                        return false;
                    }
                }
                else if (!left.SameAs(right))
                {
                    return false;
                }
            }

            return true;
        }
    }

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
