using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dragons.InvisibleDragon.Profile;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class ScheduleOperationsOracleParityTests
{
    private const string OracleSchema =
        "dragons.invisibledragon.schedule-operations-oracle.v1";
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/schedule-operations-oracle.json";
    private const string OracleSha256 =
        "sha256:0036b17a367317e3898a16b57d39727035f9df30c7fb8a5c6a9cfaa49263c9a0";
    private const int ExpectedCaseCount = 329;
    private const int ExpectedAdaptationCaseCount = 56;
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamSourceSha256 =
        "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Profile.ScheduleOperationsOracleParityTests.MatchesPinnedPythonOperations";

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
        new("Schedule.__add__", "sha256:b1f53fbc391503bdfb118688bb177fa95bcc24c9dfa53fed27cf00442c23eb72", "profile-schedule-add-b1f53fbc"),
        new("Schedule.__and__", "sha256:7f01a01b4cac360d47a636894a07fd56068d60de8e9f38f0f2093136b7d5b604", "profile-schedule-and-7f01a01b"),
        new("Schedule.__ge__", "sha256:11523775a19222ca1a489b107918fcfc6b8f82c3c0a546e353e56db07531549d", "profile-schedule-ge-11523775"),
        new("Schedule.__gt__", "sha256:e70545b0c4551837664dfd3c684e8c835b11039a47898efd2c95df938cbcc6dc", "profile-schedule-gt-e70545b0"),
        new("Schedule.__invert__", "sha256:474278997d954d91123564a0aa856a2ec834728fb3561ed94084cce5b7893b5e", "profile-schedule-invert-47427899"),
        new("Schedule.__le__", "sha256:2c2318841748622514438612475423857eae8f569efe30da720d0f20fca8a21d", "profile-schedule-le-2c231884"),
        new("Schedule.__lt__", "sha256:78d60d6a572ac4b18c51274a13bd5f089f183508f1315e82c4a00767febd87b2", "profile-schedule-lt-78d60d6a"),
        new("Schedule.__mul__", "sha256:341d9b28a235a5361ed9d16141e7ffdeac6f8933c1e420208fefb4714298029a", "profile-schedule-mul-341d9b28"),
        new("Schedule.__or__", "sha256:cad1d342fc3e187970f1e3c996ddcd1d8ab53e184ca980cd948c2f9641ead350", "profile-schedule-or-cad1d342"),
        new("Schedule.__radd__", "sha256:ebaafbe81f9daa483e5f13afbd779c49f7308ef8b9551e10dac213d64a37c045", "profile-schedule-radd-ebaafbe8"),
        new("Schedule.__rmul__", "sha256:279533a07d8189cc0a3f7fa57174faab7ea8500caf007ed8fc08ddb067353be2", "profile-schedule-rmul-279533a0"),
        new("Schedule.__rsub__", "sha256:e84f78d3b0d4f00c202d644a04d90915a5302c87e28b1b37394c9504a4400047", "profile-schedule-rsub-e84f78d3"),
        new("Schedule.__rtruediv__", "sha256:32d900f7d3189a35816962c0dae8c984f77bf619f9d75e74464a20803b090209", "profile-schedule-rtruediv-32d900f7"),
        new("Schedule.__sub__", "sha256:c963a4baf0da27e3807668ca2a93d212c429e167beebb78cdacce21be1c935dc", "profile-schedule-sub-c963a4ba"),
        new("Schedule.__truediv__", "sha256:cb9dd7d8cd8f71bb8ddff07c59959540a9a259b1caf6268128a68e17e375652c", "profile-schedule-truediv-cb9dd7d8"),
        new("Schedule.element_eq", "sha256:e9c68d0b1d5292abffaf63d02594825784cb1f07bdff0151f3ba0fefbcd1dae4", "profile-schedule-element-eq-e9c68d0b"),
        new("Schedule.element_max", "sha256:6287b64a5cf6b3db41eeae0fdeea354e3803debaac7591381e47939c312a087c", "profile-schedule-element-max-6287b64a"),
        new("Schedule.element_min", "sha256:56fdf733359e9c5e0fd96ec1d1288795816cc4eb3f34541a5b37917ee9297b36", "profile-schedule-element-min-56fdf733"),
        new("Schedule.element_ne", "sha256:32a6c5639c7affbca0e62ccf8ec70bb00ced57fbdf6e318cf1196eb7cd3f3e49", "profile-schedule-element-ne-32a6c563"),
        new("Schedule.is_between", "sha256:d359b7f1264f8fedf1c8c448b7efcc6ac8179ec977bb1d3dd6f2e6f2ace4eb5f", "profile-schedule-is-between-d359b7f1"),
        new("Schedule.is_negative", "sha256:49c07d553db98c166cf8cf61ea861b974fe140062ea9f2152a5f830ef6ca94c6", "profile-schedule-is-negative-49c07d55"),
        new("Schedule.is_nonzero", "sha256:c4f3aa30304e19e7b367eb2bb0e49b29c63d7b2b42e7b956334b56ef61aa4b01", "profile-schedule-is-nonzero-c4f3aa30"),
        new("Schedule.is_off", "sha256:b57679c27fb4fd20277b0bfb3942f0227ac4a7a69f779b66a4e5d495f19755fa", "profile-schedule-is-off-b57679c2"),
        new("Schedule.is_on", "sha256:5b1abd1e95bc9b66d360bfec68721a2c989e2b98f37c063935239deffe2a1423", "profile-schedule-is-on-5b1abd1e"),
        new("Schedule.is_positive", "sha256:54b471f257f020203e667a9496c22e38b0e021762b2b63dc793341013372a25c", "profile-schedule-is-positive-54b471f2"),
        new("Schedule.is_zero", "sha256:b57679c27fb4fd20277b0bfb3942f0227ac4a7a69f779b66a4e5d495f19755fa", "profile-schedule-is-zero-b57679c2"),
        new("Schedule.normalize_by_max", "sha256:b12e2905f36794820228b307d1ee4dacf368b1c00e19a26b7423acae87bab5d3", "profile-schedule-normalize-by-max-b12e2905"),
        new("Schedule.where", "sha256:d673aaaebf6468cbce8fe25610252702146eda0a155bef637940e26305108315", "profile-schedule-where-d673aaae"),
    };

    private static readonly Dictionary<string, string> ExpectedAdaptations =
        new(StringComparer.Ordinal)
        {
            ["deterministic-schedule-where-child-names"] = "Schedule.where",
            ["nonfinite-result-schedule-add"] = "Schedule.__add__",
            ["nonfinite-result-schedule-mul"] = "Schedule.__mul__",
            ["nonfinite-result-schedule-normalize-by-max"] = "Schedule.normalize_by_max",
            ["nonfinite-result-schedule-radd"] = "Schedule.__radd__",
            ["nonfinite-result-schedule-rmul"] = "Schedule.__rmul__",
            ["nonfinite-result-schedule-rsub"] = "Schedule.__rsub__",
            ["nonfinite-result-schedule-rtruediv"] = "Schedule.__rtruediv__",
            ["nonfinite-result-schedule-sub"] = "Schedule.__sub__",
            ["nonfinite-result-schedule-truediv"] = "Schedule.__truediv__",
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
            if (binding.Symbol == "Schedule.where")
            {
                Assert.Equal(50, symbolCases.Length);
            }

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

    private static void AssertFixtureCardinality(JsonElement summary, JsonElement[] cases)
    {
        Assert.Equal(28, ExpectedSymbols.Length);
        Assert.Equal(10, ExpectedAdaptations.Count);
        Assert.Equal(ExpectedCaseCount, cases.Length);
        Assert.Equal(ExpectedCaseCount, summary.GetProperty("case_count").GetInt32());
        Assert.Equal(ExpectedAdaptationCaseCount, summary.GetProperty("adaptation_case_count").GetInt32());
        Assert.Equal(0, summary.GetProperty("repair_reference_count").GetInt32());
        Assert.Equal(77, summary.GetProperty("observed_outcomes").GetProperty("raised").GetInt32());
        Assert.Equal(252, summary.GetProperty("observed_outcomes").GetProperty("returned").GetInt32());
        Assert.Equal(26, summary.GetProperty("expected_dotnet_outcomes").GetProperty("raised").GetInt32());
        Assert.Equal(30, summary.GetProperty("expected_dotnet_outcomes").GetProperty("returned").GetInt32());
        Assert.Equal(
            ExpectedAdaptations.Keys.OrderBy(item => item, StringComparer.Ordinal),
            summary.GetProperty("adaptation_ids").EnumerateArray().Select(item => item.GetString()!)
                .OrderBy(item => item, StringComparer.Ordinal));

        JsonElement[] adapted = cases.Where(item => item.TryGetProperty("expected_dotnet", out _)).ToArray();
        Assert.Equal(ExpectedAdaptationCaseCount, adapted.Length);
        Assert.DoesNotContain(cases, item => item.TryGetProperty("repair_reference", out _));
        Assert.Equal(23, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "reject-nonfinite-result"));
        Assert.Equal(22, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "deterministic-slot-names"));
        Assert.Equal(6, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "deterministic-period-child-names"));
        Assert.Equal(3, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "reject-invalid-name"));
        Assert.Equal(1, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "trim-name-and-deterministic-slot-names"));
        Assert.Equal(1, adapted.Count(item => RequiredString(item.GetProperty("expected_dotnet"), "policy") == "trim-result-name"));

        AssertHardenedWhereCoverage(cases);

        foreach (JsonElement operationCase in adapted)
        {
            JsonElement expected = operationCase.GetProperty("expected_dotnet");
            string adaptation = RequiredString(expected, "adaptation");
            Assert.True(ExpectedAdaptations.ContainsKey(adaptation));
            Assert.Equal(ExpectedAdaptations[adaptation], RequiredString(operationCase, "symbol"));
            Assert.Contains(RequiredString(expected, "outcome"), new[] { "raised", "returned" });
            Assert.Contains(
                RequiredString(expected, "policy"),
                new[]
                {
                    "deterministic-period-child-names",
                    "deterministic-slot-names",
                    "reject-invalid-name",
                    "reject-nonfinite-result",
                    "trim-name-and-deterministic-slot-names",
                    "trim-result-name",
                });
        }
    }

    private static void AssertHardenedWhereCoverage(JsonElement[] cases)
    {
        JsonElement[] whereCases = cases
            .Where(item => RequiredString(item, "symbol") == "Schedule.where")
            .ToArray();
        Assert.Equal(50, whereCases.Length);

        AssertNonfiniteWhereMatrix(whereCases);
        AssertBooleanWhereCases(whereCases);
        AssertScheduleRuleSetWherePairs(whereCases);
    }

    private static void AssertNonfiniteWhereMatrix(JsonElement[] whereCases)
    {
        string[] tokens = { "nan", "negative-infinity", "positive-infinity" };
        (string Prefix, string ConditionName, string NonfiniteOperand, string FiniteOperand,
            string Policy, string DotnetOutcome)[] scenarios =
        {
            (
                "nonfinite.where.selected-false",
                "condition-all-false:Annual",
                "if_false",
                "if_true",
                "reject-nonfinite-result",
                "raised"),
            (
                "nonfinite.where.selected-true",
                "condition-all-true:Annual",
                "if_true",
                "if_false",
                "reject-nonfinite-result",
                "raised"),
            (
                "nonfinite.where.unselected-false",
                "condition-all-true:Annual",
                "if_false",
                "if_true",
                "deterministic-slot-names",
                "returned"),
            (
                "nonfinite.where.unselected-true",
                "condition-all-false:Annual",
                "if_true",
                "if_false",
                "deterministic-slot-names",
                "returned"),
        };

        string[] expectedIds = scenarios
            .SelectMany(scenario => tokens.Select(token => $"{scenario.Prefix}.{token}"))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        JsonElement[] matrixCases = whereCases
            .Where(item => RequiredString(item, "id").StartsWith(
                "nonfinite.where.",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(12, matrixCases.Length);
        Assert.Equal(
            expectedIds,
            matrixCases.Select(item => RequiredString(item, "id"))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray());

        foreach (var scenario in scenarios)
        {
            foreach (string token in tokens)
            {
                JsonElement operationCase = RequiredCase(
                    matrixCases,
                    $"{scenario.Prefix}.{token}");
                JsonElement inputs = operationCase.GetProperty("inputs");
                Assert.Equal(
                    scenario.ConditionName,
                    RequiredLiteralObjectName(inputs.GetProperty("condition"), "schedule"));

                JsonElement nonfinite = inputs.GetProperty(scenario.NonfiniteOperand);
                Assert.Equal("nonfinite", RequiredString(nonfinite, "kind"));
                Assert.Equal(token, RequiredString(nonfinite, "value"));
                Assert.Equal(
                    "scalar",
                    RequiredString(inputs.GetProperty(scenario.FiniteOperand), "kind"));

                Assert.Equal(
                    "returned",
                    RequiredString(operationCase.GetProperty("observation"), "outcome"));
                JsonElement expected = operationCase.GetProperty("expected_dotnet");
                Assert.Equal(
                    "deterministic-schedule-where-child-names",
                    RequiredString(expected, "adaptation"));
                Assert.Equal(scenario.Policy, RequiredString(expected, "policy"));
                Assert.Equal(scenario.DotnetOutcome, RequiredString(expected, "outcome"));
                if (scenario.DotnetOutcome == "raised")
                {
                    Assert.Equal("domain", RequiredString(expected, "error_category"));
                }
                else
                {
                    Assert.False(expected.TryGetProperty("error_category", out _));
                }
            }
        }
    }

    private static void AssertBooleanWhereCases(JsonElement[] whereCases)
    {
        (string Id, bool SelectedValue, long UnselectedValue)[] expectations =
        {
            ("where.bool.selected-false-value", false, 1),
            ("where.bool.selected-true-value", true, 0),
        };

        foreach (var expectation in expectations)
        {
            JsonElement operationCase = RequiredCase(whereCases, expectation.Id);
            JsonElement inputs = operationCase.GetProperty("inputs");
            Assert.Equal(
                "condition-all-true:Annual",
                RequiredLiteralObjectName(inputs.GetProperty("condition"), "schedule"));

            JsonElement selected = inputs.GetProperty("if_true");
            Assert.Equal("scalar", RequiredString(selected, "kind"));
            Assert.Equal("bool", RequiredString(selected, "python_type"));
            Assert.Equal(expectation.SelectedValue, selected.GetProperty("value").GetBoolean());

            JsonElement unselected = inputs.GetProperty("if_false");
            Assert.Equal("scalar", RequiredString(unselected, "kind"));
            Assert.Equal("int", RequiredString(unselected, "python_type"));
            Assert.Equal(expectation.UnselectedValue, unselected.GetProperty("value").GetInt64());

            AssertReturnedWhereAdaptation(operationCase, "deterministic-slot-names");
        }
    }

    private static void AssertScheduleRuleSetWherePairs(JsonElement[] whereCases)
    {
        (string Id, string TrueKind, string TrueName, string FalseKind, string FalseName)[]
            expectations =
            {
                (
                    "where.branch.ruleset-schedule.inferred",
                    "ruleset",
                    "WhereTrueRules",
                    "schedule",
                    "where-false:Annual"),
                (
                    "where.branch.schedule-ruleset.inferred",
                    "schedule",
                    "where-true:Annual",
                    "ruleset",
                    "WhereFalseRules"),
            };

        foreach (var expectation in expectations)
        {
            JsonElement operationCase = RequiredCase(whereCases, expectation.Id);
            JsonElement inputs = operationCase.GetProperty("inputs");
            Assert.Equal(
                "condition:Annual",
                RequiredLiteralObjectName(inputs.GetProperty("condition"), "schedule"));
            Assert.Equal(
                expectation.TrueName,
                RequiredLiteralObjectName(
                    inputs.GetProperty("if_true"),
                    expectation.TrueKind));
            Assert.Equal(
                expectation.FalseName,
                RequiredLiteralObjectName(
                    inputs.GetProperty("if_false"),
                    expectation.FalseKind));
            Assert.Equal("none", RequiredString(inputs.GetProperty("type"), "kind"));

            AssertReturnedWhereAdaptation(
                operationCase,
                "deterministic-period-child-names");
        }
    }

    private static void AssertReturnedWhereAdaptation(
        JsonElement operationCase,
        string policy)
    {
        Assert.Equal(
            "returned",
            RequiredString(operationCase.GetProperty("observation"), "outcome"));
        JsonElement expected = operationCase.GetProperty("expected_dotnet");
        Assert.Equal(
            "deterministic-schedule-where-child-names",
            RequiredString(expected, "adaptation"));
        Assert.Equal(policy, RequiredString(expected, "policy"));
        Assert.Equal("returned", RequiredString(expected, "outcome"));
        Assert.False(expected.TryGetProperty("error_category", out _));
    }

    private static JsonElement RequiredCase(JsonElement[] cases, string id)
    {
        JsonElement[] matches = cases
            .Where(item => RequiredString(item, "id") == id)
            .ToArray();
        Assert.Single(matches);
        return matches[0];
    }

    private static string RequiredLiteralObjectName(JsonElement descriptor, string kind)
    {
        Assert.Equal(kind, RequiredString(descriptor, "kind"));
        JsonElement name = descriptor.GetProperty("name");
        Assert.Equal("literal", RequiredString(name, "policy"));
        return RequiredString(name, "value");
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

        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.Equal(Schedule.FixedLength, contract.GetProperty("annual_length").GetInt32());
        Assert.Equal("fr-FR", RequiredString(contract, "culture"));
        Assert.Equal("inclusive-iso-date", RequiredString(contract, "period_endpoints"));
        Assert.Equal("python-str-culture-invariant", RequiredString(contract, "scalar_names"));
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
        if (expectedDotnet is null)
        {
            Assert.Equal(Serialize(python), Serialize(dotnet));
        }
        else
        {
            AssertExpectedDotnetOutcome(expectedDotnet, python, dotnet);
        }

        return new CaseEvidence(
            caseId,
            symbol,
            JsonSerializer.SerializeToElement(python, EvidenceJsonOptions),
            JsonSerializer.SerializeToElement(dotnet, EvidenceJsonOptions),
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
            expected.TryGetProperty("result_name", out JsonElement resultName)
                ? RequiredStringValue(resultName)
                : null);
    }

    private static void AssertExpectedDotnetOutcome(
        ExpectedDotnetOutcome expected,
        NormalizedOutcome python,
        NormalizedOutcome dotnet)
    {
        switch (expected.Policy)
        {
            case "deterministic-period-child-names":
            case "deterministic-slot-names":
                Assert.Equal("returned", expected.Outcome);
                Assert.Null(expected.ErrorCategory);
                Assert.Null(expected.ResultName);
                AssertDeterministicChildNames(python, dotnet, null);
                return;
            case "reject-nonfinite-result":
            case "reject-invalid-name":
                AssertRejectedOutcome(expected, python, dotnet);
                return;
            case "trim-name-and-deterministic-slot-names":
                Assert.Equal("returned", expected.Outcome);
                Assert.Null(expected.ErrorCategory);
                Assert.False(string.IsNullOrWhiteSpace(expected.ResultName));
                AssertDeterministicChildNames(python, dotnet, expected.ResultName);
                return;
            case "trim-result-name":
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
        Assert.Equal("returned", reference.Outcome);
        Assert.Equal("raised", expected.Outcome);
        Assert.Equal("raised", dotnet.Outcome);
        Assert.Equal(expected.ErrorCategory, dotnet.ErrorCategory);
        Assert.Equal("none", dotnet.ResultKind);
        Assert.Equal("none", dotnet.ResultIdentity);
        Assert.Null(dotnet.Result);
        Assert.Equal(Serialize(reference.InputPostconditions), Serialize(dotnet.InputPostconditions));
    }

    private static void AssertDeterministicChildNames(
        NormalizedOutcome python,
        NormalizedOutcome dotnet,
        string? expectedDotnetName)
    {
        AssertReturnedSchedules(python, dotnet);
        AnnualSnapshot expected = python.Result!;
        AnnualSnapshot actual = dotnet.Result!;
        if (expectedDotnetName is null)
        {
            Assert.Equal(expected.Name, actual.Name);
        }
        else
        {
            Assert.NotEqual(expected.Name, expectedDotnetName);
            Assert.Equal(expectedDotnetName, actual.Name);
        }

        AssertAnnualExceptOuterAndDayNames(expected, actual);
        Assert.Equal(Serialize(python.InputPostconditions), Serialize(dotnet.InputPostconditions));
        foreach ((AnnualPeriodSnapshot pythonPeriod, AnnualPeriodSnapshot dotnetPeriod) in
            expected.Periods.Zip(actual.Periods))
        {
            foreach (string slot in SlotKeys)
            {
                DaySnapshot? pythonDay = pythonPeriod.RuleSet.Slots[slot];
                DaySnapshot? dotnetDay = dotnetPeriod.RuleSet.Slots[slot];
                if (pythonDay is null)
                {
                    Assert.Null(dotnetDay);
                    continue;
                }

                Assert.NotNull(dotnetDay);
                Assert.Equal("runtime-identity-hex", pythonDay.NamePolicy);
                Assert.Null(pythonDay.Name);
                Assert.Equal("literal", dotnetDay!.NamePolicy);
                Assert.Equal($"{dotnetPeriod.RuleSet.Name}:{slot}", dotnetDay.Name);
            }
        }
    }

    private static void AssertTrimmedResultName(
        NormalizedOutcome python,
        NormalizedOutcome dotnet,
        string expectedDotnetName)
    {
        AssertReturnedSchedules(python, dotnet);
        Assert.NotEqual(python.Result!.Name, expectedDotnetName);
        Assert.Equal(expectedDotnetName, dotnet.Result!.Name);
        AssertAnnualExceptOuterName(python.Result, dotnet.Result);
        Assert.Equal(Serialize(python.InputPostconditions), Serialize(dotnet.InputPostconditions));
    }

    private static void AssertReturnedSchedules(NormalizedOutcome python, NormalizedOutcome dotnet)
    {
        Assert.Equal("returned", python.Outcome);
        Assert.Equal("returned", dotnet.Outcome);
        Assert.Equal("schedule", python.ResultKind);
        Assert.Equal("schedule", dotnet.ResultKind);
        Assert.Equal("new", python.ResultIdentity);
        Assert.Equal("new", dotnet.ResultIdentity);
        Assert.NotNull(python.Result);
        Assert.NotNull(dotnet.Result);
    }

    private static Schedule Dispatch(
        string symbol,
        JsonElement inputs,
        OperationContext context)
    {
        Schedule Receiver() => context.Schedule("receiver");
        return symbol switch
        {
            "Schedule.__add__" => Add(Receiver(), inputs.GetProperty("other"), context),
            "Schedule.__and__" => Receiver() & context.Schedule("other"),
            "Schedule.__ge__" => Compare(Receiver(), inputs.GetProperty("other"), context, "ge"),
            "Schedule.__gt__" => Compare(Receiver(), inputs.GetProperty("other"), context, "gt"),
            "Schedule.__invert__" => !Receiver(),
            "Schedule.__le__" => Compare(Receiver(), inputs.GetProperty("other"), context, "le"),
            "Schedule.__lt__" => Compare(Receiver(), inputs.GetProperty("other"), context, "lt"),
            "Schedule.__mul__" => Multiply(Receiver(), inputs.GetProperty("other"), context),
            "Schedule.__or__" => Receiver() | context.Schedule("other"),
            "Schedule.__radd__" => ReverseAdd(Receiver(), ReadScalar(inputs, "other")),
            "Schedule.__rmul__" => ReverseMultiply(Receiver(), ReadScalar(inputs, "other")),
            "Schedule.__rsub__" => ReverseSubtract(Receiver(), ReadScalar(inputs, "other")),
            "Schedule.__rtruediv__" => ReverseDivide(Receiver(), ReadScalar(inputs, "other")),
            "Schedule.__sub__" => Subtract(Receiver(), inputs.GetProperty("other"), context),
            "Schedule.__truediv__" => Divide(Receiver(), inputs.GetProperty("other"), context),
            "Schedule.element_eq" => ElementEqual(Receiver(), inputs.GetProperty("other"), context),
            "Schedule.element_max" => Receiver().ElementMaximum(context.Schedule("other")),
            "Schedule.element_min" => Receiver().ElementMinimum(context.Schedule("other")),
            "Schedule.element_ne" => ElementNotEqual(Receiver(), inputs.GetProperty("other"), context),
            "Schedule.is_between" => IsBetween(Receiver(), inputs),
            "Schedule.is_negative" => Receiver().IsNegative(),
            "Schedule.is_nonzero" => Receiver().IsNonzero(),
            "Schedule.is_off" => Receiver().IsOff(),
            "Schedule.is_on" => Receiver().IsOn(),
            "Schedule.is_positive" => Receiver().IsPositive(),
            "Schedule.is_zero" => Receiver().IsZero(),
            "Schedule.normalize_by_max" => Receiver().NormalizeByMaximum(OptionalText(inputs, "new_name")),
            "Schedule.where" => Where(inputs, context),
            _ => throw new InvalidDataException($"Unknown Schedule oracle symbol '{symbol}'."),
        };
    }

    private static Schedule Add(Schedule receiver, JsonElement other, OperationContext context)
    {
        return IsSchedule(other)
            ? receiver + context.Schedule("other")
            : AddScalar(receiver, ReadScalar(other));
    }

    private static Schedule Subtract(Schedule receiver, JsonElement other, OperationContext context)
    {
        return IsSchedule(other)
            ? receiver - context.Schedule("other")
            : SubtractScalar(receiver, ReadScalar(other));
    }

    private static Schedule Multiply(Schedule receiver, JsonElement other, OperationContext context)
    {
        return IsSchedule(other)
            ? receiver * context.Schedule("other")
            : MultiplyScalar(receiver, ReadScalar(other));
    }

    private static Schedule Divide(Schedule receiver, JsonElement other, OperationContext context)
    {
        return IsSchedule(other)
            ? receiver / context.Schedule("other")
            : DivideScalar(receiver, ReadScalar(other));
    }

    private static Schedule AddScalar(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value,
            value => receiver + value);
    }

    private static Schedule SubtractScalar(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value,
            value => receiver - value);
    }

    private static Schedule MultiplyScalar(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value,
            value => receiver * value);
    }

    private static Schedule DivideScalar(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value,
            value => receiver / value);
    }

    private static Schedule ReverseAdd(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver,
            value => value + receiver);
    }

    private static Schedule ReverseMultiply(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver,
            value => value * receiver);
    }

    private static Schedule ReverseSubtract(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver,
            value => value - receiver);
    }

    private static Schedule ReverseDivide(Schedule receiver, OracleScalar scalar)
    {
        return DispatchScalar(
            scalar,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver,
            value => value / receiver);
    }

    private static Schedule Compare(
        Schedule receiver,
        JsonElement other,
        OperationContext context,
        string operation)
    {
        if (IsSchedule(other))
        {
            Schedule value = context.Schedule("other");
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
            "ge" => DispatchScalar(
                scalar,
                value => receiver.GreaterThanOrEqual(value),
                value => receiver.GreaterThanOrEqual(value),
                value => receiver.GreaterThanOrEqual(value),
                value => receiver.GreaterThanOrEqual(value),
                value => receiver.GreaterThanOrEqual(value)),
            "gt" => DispatchScalar(
                scalar,
                value => receiver.GreaterThan(value),
                value => receiver.GreaterThan(value),
                value => receiver.GreaterThan(value),
                value => receiver.GreaterThan(value),
                value => receiver.GreaterThan(value)),
            "le" => DispatchScalar(
                scalar,
                value => receiver.LessThanOrEqual(value),
                value => receiver.LessThanOrEqual(value),
                value => receiver.LessThanOrEqual(value),
                value => receiver.LessThanOrEqual(value),
                value => receiver.LessThanOrEqual(value)),
            "lt" => DispatchScalar(
                scalar,
                value => receiver.LessThan(value),
                value => receiver.LessThan(value),
                value => receiver.LessThan(value),
                value => receiver.LessThan(value),
                value => receiver.LessThan(value)),
            _ => throw new InvalidDataException($"Unknown comparison '{operation}'."),
        };
    }

    private static Schedule ElementEqual(
        Schedule receiver,
        JsonElement other,
        OperationContext context)
    {
        if (IsSchedule(other))
        {
            return receiver.ElementEqual(context.Schedule("other"));
        }

        OracleScalar scalar = ReadScalar(other);
        return DispatchScalar(
            scalar,
            value => receiver.ElementEqual(value),
            value => receiver.ElementEqual(value),
            value => receiver.ElementEqual(value),
            value => receiver.ElementEqual(value),
            value => receiver.ElementEqual(value));
    }

    private static Schedule ElementNotEqual(
        Schedule receiver,
        JsonElement other,
        OperationContext context)
    {
        if (IsSchedule(other))
        {
            return receiver.ElementNotEqual(context.Schedule("other"));
        }

        OracleScalar scalar = ReadScalar(other);
        return DispatchScalar(
            scalar,
            value => receiver.ElementNotEqual(value),
            value => receiver.ElementNotEqual(value),
            value => receiver.ElementNotEqual(value),
            value => receiver.ElementNotEqual(value),
            value => receiver.ElementNotEqual(value));
    }

    private static Schedule IsBetween(Schedule receiver, JsonElement inputs)
    {
        OracleScalar minimum = ReadScalar(inputs, "min_value");
        OracleScalar maximum = ReadScalar(inputs, "max_value");
        return receiver.IsBetween(
            minimum.ClrValue,
            maximum.ClrValue,
            Boolean(inputs, "include_min"),
            Boolean(inputs, "include_max"));
    }

    private static Schedule DispatchScalar(
        OracleScalar scalar,
        Func<int, Schedule> integerOperation,
        Func<long, Schedule> longIntegerOperation,
        Func<BigInteger, Schedule> unboundedIntegerOperation,
        Func<double, Schedule> floatOperation,
        Func<bool, Schedule> booleanOperation)
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

    private static Schedule Where(JsonElement inputs, OperationContext context)
    {
        return Schedule.Where(
            context.Schedule("condition"),
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
            "schedule" => context.Schedule(name),
            "ruleset" => context.RuleSet(name),
            "day-schedule" => context.DaySchedule(name),
            "text" => RequiredString(descriptor, "value"),
            _ => ReadScalar(descriptor).ClrValue,
        };
    }

    private static CapturedExecution Capture(Func<Schedule> action)
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
        Assert.Equal("schedule", RequiredString(result, "kind"));
        return new NormalizedOutcome(
            outcome,
            "schedule",
            identity,
            ReadAnnualSnapshot(result),
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

        Schedule result = Assert.IsType<Schedule>(execution.Result);
        string identity = context.Schedules.TryGetValue("receiver", out Schedule? receiver)
            && ReferenceEquals(receiver, result)
            ? "receiver"
            : "new";
        return new NormalizedOutcome(
            "returned",
            "schedule",
            identity,
            AnnualSnapshot.From(result),
            null,
            inputPostconditions);
    }

    private static void AssertPythonInputPostconditions(
        JsonElement observation,
        OperationContext context)
    {
        InputPostconditions postconditions = ReadInputPostconditions(observation);
        Assert.Equal(
            context.Schedules.Keys.OrderBy(item => item, StringComparer.Ordinal),
            postconditions.Schedules.Keys);
        Assert.Equal(
            context.RuleSets.Keys.OrderBy(item => item, StringComparer.Ordinal),
            postconditions.RuleSets.Keys);
        Assert.Equal(
            context.DaySchedules.Keys.OrderBy(item => item, StringComparer.Ordinal),
            postconditions.DaySchedules.Keys);

        foreach ((string name, ScheduleInputState state) in postconditions.Schedules)
        {
            Assert.Equal("preserved", state.Identity);
            Assert.Equal("preserved", state.AnnualRuleSetIdentities);
            Assert.Equal("unchanged", state.Status);
            Assert.Equal(Schedule.FixedLength, context.ScheduleBefore[name].RuleSetReferences.Length);
        }

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
        SortedDictionary<string, ScheduleInputState> schedules = new(StringComparer.Ordinal);
        foreach (JsonProperty property in observation.GetProperty("schedule_inputs_after").EnumerateObject())
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
                _ = ReadAnnualSnapshot(value.GetProperty("value"));
            }

            schedules.Add(
                property.Name,
                new ScheduleInputState(
                    RequiredString(value, "identity"),
                    RequiredString(value, "annual_rule_set_identities"),
                    status));
        }

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
                _ = ReadDaySnapshot(value.GetProperty("value"));
            }

            days.Add(
                property.Name,
                new InputObjectState(RequiredString(value, "identity"), status));
        }

        return new InputPostconditions(schedules, rulesets, days);
    }

    private static AnnualSnapshot ReadAnnualSnapshot(JsonElement descriptor)
    {
        Assert.Equal("schedule", RequiredString(descriptor, "kind"));
        NameDescriptor name = ReadNameDescriptor(descriptor.GetProperty("name"));
        JsonElement[] periods = descriptor.GetProperty("periods").EnumerateArray().ToArray();
        Assert.Equal(periods.Length, descriptor.GetProperty("compact_period_count").GetInt32());
        Assert.NotEmpty(periods);

        var parsedPeriods = new List<AnnualPeriodSnapshot>(periods.Length);
        int nextIndex = 0;
        foreach (JsonElement period in periods)
        {
            int startIndex = period.GetProperty("start_index").GetInt32();
            int endIndex = period.GetProperty("end_index").GetInt32();
            Assert.Equal(nextIndex, startIndex);
            Assert.InRange(endIndex, startIndex, Schedule.FixedLength - 1);
            DateTime start = ParseAnnualDate(RequiredString(period, "start"));
            DateTime end = ParseAnnualDate(RequiredString(period, "end"));
            Assert.Equal(startIndex, DayIndex(start));
            Assert.Equal(endIndex, DayIndex(end));
            parsedPeriods.Add(
                new AnnualPeriodSnapshot(
                    startIndex,
                    endIndex,
                    ReadRuleSetSnapshot(period.GetProperty("rule_set"))));
            nextIndex = endIndex + 1;
        }

        Assert.Equal(Schedule.FixedLength, nextIndex);
        JsonElement sequence = descriptor.GetProperty("annual_rule_set_sequence");
        Assert.Equal("period-index-ranges", RequiredString(sequence, "encoding"));
        Assert.Equal(Schedule.FixedLength, sequence.GetProperty("length").GetInt32());
        JsonElement[] ranges = sequence.GetProperty("ranges").EnumerateArray().ToArray();
        Assert.Equal(parsedPeriods.Count, ranges.Length);
        for (int index = 0; index < ranges.Length; index++)
        {
            Assert.Equal(index, ranges[index].GetProperty("period_index").GetInt32());
            Assert.Equal(parsedPeriods[index].StartIndex, ranges[index].GetProperty("start_index").GetInt32());
            Assert.Equal(parsedPeriods[index].EndIndex, ranges[index].GetProperty("end_index").GetInt32());
        }

        return new AnnualSnapshot(
            name.Policy,
            name.Value,
            RequiredString(descriptor, "schedule_type"),
            parsedPeriods.ToArray());
    }

    private static RuleSetSnapshot ReadRuleSetSnapshot(JsonElement descriptor)
    {
        Assert.Equal("ruleset", RequiredString(descriptor, "kind"));
        NameDescriptor name = ReadNameDescriptor(descriptor.GetProperty("name"));
        JsonElement slotsElement = descriptor.GetProperty("slots");
        AssertExactKeys(slotsElement, SlotKeys);
        SortedDictionary<string, DaySnapshot?> slots = new(StringComparer.Ordinal);
        foreach (string slot in SlotKeys)
        {
            JsonElement schedule = slotsElement.GetProperty(slot);
            slots.Add(slot, schedule.ValueKind == JsonValueKind.Null ? null : ReadDaySnapshot(schedule));
        }

        SortedDictionary<string, string> sources = ReadEffectiveSources(
            descriptor.GetProperty("effective_slot_sources"));
        AssertEffectiveSources(slots, sources);
        return new RuleSetSnapshot(
            name.Policy,
            name.Value,
            RequiredString(descriptor, "schedule_type"),
            slots,
            sources);
    }

    private static DaySnapshot ReadDaySnapshot(JsonElement descriptor)
    {
        Assert.Equal("day-schedule", RequiredString(descriptor, "kind"));
        NameDescriptor name = ReadNameDescriptor(descriptor.GetProperty("name"));
        return new DaySnapshot(
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
        SortedDictionary<string, DaySnapshot?> slots,
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

    private static void AssertAnnualExceptOuterAndDayNames(
        AnnualSnapshot expected,
        AnnualSnapshot actual)
    {
        Assert.Equal(expected.ScheduleType, actual.ScheduleType);
        Assert.Equal(expected.Periods.Length, actual.Periods.Length);
        foreach ((AnnualPeriodSnapshot expectedPeriod, AnnualPeriodSnapshot actualPeriod) in
            expected.Periods.Zip(actual.Periods))
        {
            Assert.Equal(expectedPeriod.StartIndex, actualPeriod.StartIndex);
            Assert.Equal(expectedPeriod.EndIndex, actualPeriod.EndIndex);
            AssertRuleSetExceptDayNames(expectedPeriod.RuleSet, actualPeriod.RuleSet);
        }
    }

    private static void AssertAnnualExceptOuterName(
        AnnualSnapshot expected,
        AnnualSnapshot actual)
    {
        Assert.Equal(expected.ScheduleType, actual.ScheduleType);
        Assert.Equal(expected.Periods.Length, actual.Periods.Length);
        foreach ((AnnualPeriodSnapshot expectedPeriod, AnnualPeriodSnapshot actualPeriod) in
            expected.Periods.Zip(actual.Periods))
        {
            Assert.Equal(expectedPeriod.StartIndex, actualPeriod.StartIndex);
            Assert.Equal(expectedPeriod.EndIndex, actualPeriod.EndIndex);
            Assert.True(expectedPeriod.RuleSet.SameAs(actualPeriod.RuleSet));
        }
    }

    private static void AssertRuleSetExceptDayNames(
        RuleSetSnapshot expected,
        RuleSetSnapshot actual)
    {
        Assert.Equal(expected.NamePolicy, actual.NamePolicy);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.ScheduleType, actual.ScheduleType);
        Assert.Equal(Serialize(expected.EffectiveSlotSources), Serialize(actual.EffectiveSlotSources));
        Assert.Equal(expected.Slots.Keys, actual.Slots.Keys);
        foreach (string slot in SlotKeys)
        {
            DaySnapshot? expectedDay = expected.Slots[slot];
            DaySnapshot? actualDay = actual.Slots[slot];
            if (expectedDay is null)
            {
                Assert.Null(actualDay);
            }
            else
            {
                Assert.NotNull(actualDay);
                AssertDayExceptName(expectedDay, actualDay!);
            }
        }
    }

    private static void AssertDayExceptName(DaySnapshot expected, DaySnapshot actual)
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

    private static bool IsSchedule(JsonElement descriptor)
    {
        return RequiredString(descriptor, "kind") == "schedule";
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

    private static DateTime ParseAnnualDate(string value)
    {
        Assert.True(DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime result));
        Assert.Equal(Schedule.DefaultYear, result.Year);
        return result;
    }

    private static int DayIndex(DateTime value)
    {
        return (value - new DateTime(Schedule.DefaultYear, 1, 1)).Days;
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
            Dictionary<string, Schedule> schedules,
            Dictionary<string, RuleSet> ruleSets,
            Dictionary<string, DaySchedule> daySchedules,
            Dictionary<string, ScheduleInputBefore> scheduleBefore,
            Dictionary<string, RuleSetInputBefore> ruleSetBefore,
            Dictionary<string, DaySnapshot> dayScheduleBefore)
        {
            Schedules = schedules;
            RuleSets = ruleSets;
            DaySchedules = daySchedules;
            ScheduleBefore = scheduleBefore;
            RuleSetBefore = ruleSetBefore;
            DayScheduleBefore = dayScheduleBefore;
        }

        public Dictionary<string, Schedule> Schedules { get; }

        public Dictionary<string, RuleSet> RuleSets { get; }

        public Dictionary<string, DaySchedule> DaySchedules { get; }

        public Dictionary<string, ScheduleInputBefore> ScheduleBefore { get; }

        public Dictionary<string, RuleSetInputBefore> RuleSetBefore { get; }

        public Dictionary<string, DaySnapshot> DayScheduleBefore { get; }

        public Schedule Schedule(string name)
        {
            return Schedules[name];
        }

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
            Dictionary<string, Schedule> schedules = new(StringComparer.Ordinal);
            Dictionary<string, RuleSet> ruleSets = new(StringComparer.Ordinal);
            Dictionary<string, DaySchedule> days = new(StringComparer.Ordinal);
            Dictionary<string, ScheduleInputBefore> scheduleBefore = new(StringComparer.Ordinal);
            Dictionary<string, RuleSetInputBefore> ruleSetBefore = new(StringComparer.Ordinal);
            Dictionary<string, DaySnapshot> dayBefore = new(StringComparer.Ordinal);
            foreach (JsonProperty property in inputs.EnumerateObject())
            {
                string kind = RequiredString(property.Value, "kind");
                if (kind == "schedule")
                {
                    AnnualSnapshot expected = ReadAnnualSnapshot(property.Value);
                    Schedule schedule = CreateSchedule(expected);
                    AnnualSnapshot actual = AnnualSnapshot.From(schedule);
                    Assert.True(expected.SameAs(actual));
                    schedules.Add(property.Name, schedule);
                    scheduleBefore.Add(
                        property.Name,
                        new ScheduleInputBefore(actual, schedule.RuleSets.ToArray()));
                }
                else if (kind == "ruleset")
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
                    DaySnapshot expected = ReadDaySnapshot(property.Value);
                    DaySchedule day = CreateDaySchedule(expected);
                    Assert.True(expected.SameAs(DaySnapshot.From(day)));
                    days.Add(property.Name, day);
                    dayBefore.Add(property.Name, DaySnapshot.From(day));
                }
            }

            return new OperationContext(
                schedules,
                ruleSets,
                days,
                scheduleBefore,
                ruleSetBefore,
                dayBefore);
        }

        public InputPostconditions CurrentPostconditions()
        {
            SortedDictionary<string, ScheduleInputState> scheduleStates = new(StringComparer.Ordinal);
            foreach ((string name, Schedule schedule) in Schedules.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                ScheduleInputBefore before = ScheduleBefore[name];
                bool referencesPreserved = before.RuleSetReferences.Length == schedule.RuleSets.Count
                    && before.RuleSetReferences.Zip(schedule.RuleSets)
                        .All(pair => ReferenceEquals(pair.First, pair.Second));
                bool unchanged = referencesPreserved
                    && before.Snapshot.SameAs(AnnualSnapshot.From(schedule));
                scheduleStates.Add(
                    name,
                    new ScheduleInputState(
                        "preserved",
                        referencesPreserved ? "preserved" : "changed",
                        unchanged ? "unchanged" : "changed"));
            }

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
                    string status = DayValuesSame(oldValue, newValue) ? "unchanged" : "changed";
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
                        DayScheduleBefore[name].SameAs(DaySnapshot.From(day))
                            ? "unchanged"
                            : "changed"));
            }

            return new InputPostconditions(scheduleStates, ruleStates, dayStates);
        }
    }

    private static Schedule CreateSchedule(AnnualSnapshot snapshot)
    {
        Assert.Equal("literal", snapshot.NamePolicy);
        Assert.NotNull(snapshot.Name);
        RuleSet[] ruleSets = new RuleSet[Schedule.FixedLength];
        foreach (AnnualPeriodSnapshot period in snapshot.Periods)
        {
            RuleSet ruleSet = CreateRuleSet(period.RuleSet);
            for (int index = period.StartIndex; index <= period.EndIndex; index++)
            {
                ruleSets[index] = ruleSet;
            }
        }

        Assert.DoesNotContain(ruleSets, item => item is null);
        return new Schedule(
            snapshot.Name!,
            ruleSets,
            ParseScheduleType(snapshot.ScheduleType));
    }

    private static RuleSet CreateRuleSet(RuleSetSnapshot snapshot)
    {
        Assert.Equal("literal", snapshot.NamePolicy);
        Assert.NotNull(snapshot.Name);
        SortedDictionary<string, DaySchedule?> days = new(StringComparer.Ordinal);
        foreach (string slot in SlotKeys)
        {
            days.Add(
                slot,
                snapshot.Slots[slot] is null ? null : CreateDaySchedule(snapshot.Slots[slot]!));
        }

        return new RuleSet(
            snapshot.Name!,
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

    private static DaySchedule CreateDaySchedule(DaySnapshot snapshot)
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

    private static bool DayValuesSame(DaySchedule? left, DaySchedule? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return DaySnapshot.From(left).SameAs(DaySnapshot.From(right));
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
            return new OracleScalar(
                "bool",
                value ? BigInteger.One : BigInteger.Zero,
                value ? 1d : 0d,
                value);
        }
    }

    private sealed record CapturedExecution(Schedule? Result, Exception? Exception);

    private sealed record ExpectedDotnetOutcome(
        string Adaptation,
        string Outcome,
        string Policy,
        string? ErrorCategory,
        string? ResultName);

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
        AnnualSnapshot? Result,
        string? ErrorCategory,
        InputPostconditions InputPostconditions);

    private sealed record InputPostconditions(
        SortedDictionary<string, ScheduleInputState> Schedules,
        SortedDictionary<string, RuleSetInputState> RuleSets,
        SortedDictionary<string, InputObjectState> DaySchedules);

    private sealed record ScheduleInputState(
        string Identity,
        string AnnualRuleSetIdentities,
        string Status);

    private sealed record RuleSetInputState(
        string Identity,
        string Status,
        SortedDictionary<string, SlotInputState> Slots);

    private sealed record SlotInputState(string Identity, string Status);

    private sealed record InputObjectState(string Identity, string Status);

    private sealed record ScheduleInputBefore(
        AnnualSnapshot Snapshot,
        RuleSet[] RuleSetReferences);

    private sealed record RuleSetInputBefore(
        RuleSetSnapshot Snapshot,
        SortedDictionary<string, DaySchedule?> SlotReferences);

    private sealed record NameDescriptor(string Policy, string? Value);

    private sealed record AnnualSnapshot(
        string NamePolicy,
        string? Name,
        string ScheduleType,
        AnnualPeriodSnapshot[] Periods)
    {
        public static AnnualSnapshot From(Schedule schedule)
        {
            AnnualPeriodSnapshot[] periods = schedule.Compactize()
                .Select(period => new AnnualPeriodSnapshot(
                    DayIndex(period.Start),
                    DayIndex(period.End),
                    RuleSetSnapshot.From(period.RuleSet)))
                .ToArray();
            Assert.NotEmpty(periods);
            Assert.Equal(0, periods[0].StartIndex);
            Assert.Equal(Schedule.FixedLength - 1, periods[^1].EndIndex);
            for (int index = 1; index < periods.Length; index++)
            {
                Assert.Equal(periods[index - 1].EndIndex + 1, periods[index].StartIndex);
            }

            return new AnnualSnapshot(
                "literal",
                schedule.Name,
                schedule.Type.CanonicalName(),
                periods);
        }

        public bool SameAs(AnnualSnapshot other)
        {
            if (NamePolicy != other.NamePolicy
                || Name != other.Name
                || ScheduleType != other.ScheduleType
                || Periods.Length != other.Periods.Length)
            {
                return false;
            }

            return Periods.Zip(other.Periods).All(pair => pair.First.SameAs(pair.Second));
        }
    }

    private sealed record AnnualPeriodSnapshot(
        int StartIndex,
        int EndIndex,
        RuleSetSnapshot RuleSet)
    {
        public bool SameAs(AnnualPeriodSnapshot other)
        {
            return StartIndex == other.StartIndex
                && EndIndex == other.EndIndex
                && RuleSet.SameAs(other.RuleSet);
        }
    }

    private sealed record RuleSetSnapshot(
        string NamePolicy,
        string? Name,
        string ScheduleType,
        SortedDictionary<string, DaySnapshot?> Slots,
        SortedDictionary<string, string> EffectiveSlotSources)
    {
        public static RuleSetSnapshot From(RuleSet ruleset)
        {
            SortedDictionary<string, DaySchedule?> references = RuleSetSlotReferences(ruleset);
            SortedDictionary<string, DaySnapshot?> slots = new(StringComparer.Ordinal);
            foreach (string slot in SlotKeys)
            {
                DaySchedule? day = references[slot];
                slots.Add(slot, day is null ? null : DaySnapshot.From(day));
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
                "literal",
                ruleset.Name,
                ruleset.Type.CanonicalName(),
                slots,
                sources);
        }

        public bool SameAs(RuleSetSnapshot other)
        {
            if (NamePolicy != other.NamePolicy
                || Name != other.Name
                || ScheduleType != other.ScheduleType
                || !EffectiveSlotSources.SequenceEqual(other.EffectiveSlotSources))
            {
                return false;
            }

            foreach (string slot in SlotKeys)
            {
                DaySnapshot? left = Slots[slot];
                DaySnapshot? right = other.Slots[slot];
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

    private sealed record DaySnapshot(
        string NamePolicy,
        string? Name,
        string ScheduleType,
        string? Unit,
        double[] Values)
    {
        public static DaySnapshot From(DaySchedule schedule)
        {
            return new DaySnapshot(
                "literal",
                schedule.Name,
                schedule.Type.CanonicalName(),
                schedule.Unit,
                schedule.Values.ToArray());
        }

        public bool SameAs(DaySnapshot other)
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
