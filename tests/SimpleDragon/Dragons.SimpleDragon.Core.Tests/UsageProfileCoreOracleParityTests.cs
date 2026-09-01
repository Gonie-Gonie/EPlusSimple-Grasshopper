using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idf;
using Dragons.SimpleDragon.Internal;
using Dragons.UpstreamTracker;
using DragonProfile = Dragons.InvisibleDragon.Profile.Profile;
using DragonSchedule = Dragons.InvisibleDragon.Profile.Schedule;
using DragonScheduleType = Dragons.InvisibleDragon.Profile.ScheduleType;

#pragma warning disable CA1861 // Exact small arrays make the closed oracle contracts auditable in place.

namespace Dragons.SimpleDragon.Tests;

public sealed class UsageProfileCoreOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/usage-profile-core-oracle.json";
    private const string OracleSha256 =
        "sha256:af4fe3a1c7827c7857a478f07d258afed1d00f0c6009c1c54e98f7a366e7c6ed";
    private const string CasesSha256 =
        "sha256:2ffcd616b3d27069fce79d0028a0cba289e625bde5d7445b450edce58368b903";
    private const int OracleByteLength = 551_187;
    private const int ExpectedCaseCount = 39;
    private const string OracleSchema =
        "dragons.simpledragon.usage-profile-core-oracle.v1";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.UsageProfileCoreOracleParityTests.MatchesPinnedPythonUsageProfileCore";
    private const string UpstreamPath = "src/epsimple/core/profile.py";

    private static readonly string[] DayNames =
    {
        "monday", "tuesday", "wednesday", "thursday",
        "friday", "saturday", "sunday", "holiday",
    };

    private static readonly string[] DictionaryKeys =
    {
        "name", "occupant_start", "occupant_end", "hvac_start", "hvac_end",
        "ventilation", "domestic_hotwater", "lighting_hours", "occupancy",
        "equipment", "heating_setpoint", "cooling_setpoint", "operate_weekdays",
        "vacations",
    };

    private static readonly string[] ScheduleSlots =
    {
        "heating_setpoint", "cooling_setpoint", "hvac_availability", "occupant",
        "lighting", "equipment", "hotwater",
    };

    // Exact three-literal bindings are required by the compatibility manifest collector.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("KoreanUsageProfile", "sha256:52a3656b8d8c7abbbbc0403206bede355eb776a007aee2f301c0819bf9a3044f", "usage-profile-core-value-object-52a3656b"),
        new("KoreanUsageProfile.DHW_HEAT_PER_LITER", "sha256:f43f031dc4dd8dd0426bbe82871259fbfad10d3011a78f865d753c02b5203f98", "usage-profile-core-dhw-heat-per-liter-f43f031d"),
        new("KoreanUsageProfile.ID", "sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2", "usage-profile-core-id-246156d9"),
        new("KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL", "sha256:e2da67c4aaf7e9220236aa66fead7ae05e32914c255302ea32fcee37c1679b72", "usage-profile-core-people-activity-level-e2da67c4"),
        new("KoreanUsageProfile.__init__", "sha256:f242c8e5794ae9b49de1e768956963696c8351c947440f6fa3ef1f70230d50f0", "usage-profile-core-init-f242c8e5"),
        new("KoreanUsageProfile.occupied_hours", "sha256:511dd2e08d266e099afda2d88e98ff3f7976a7fb6738bb8ff2304f19033cfc90", "usage-profile-core-occupied-hours-511dd2e0"),
        new("KoreanUsageProfile.operating_days", "sha256:1ab019f1c745c00702036a96b87c9d604cd18608b033510e3de6621e8f6a930d", "usage-profile-core-operating-days-1ab019f1"),
        new("KoreanUsageProfile.to_dict", "sha256:40c556a7cf3a93741c48f26c3eb30ba4d70f7dade0abfc3ef50ecfbf3cfded5e", "usage-profile-core-to-dict-40c556a7"),
        new("KoreanUsageProfile.to_dragon", "sha256:f3b70764f326865596e72fcfc799555b190ef18db77a54dcbfa6df012f236d3e", "usage-profile-core-to-dragon-f3b70764"),
        new("KoreanUsageProfileExtended", "sha256:5a6703884a6c29f977d9e025af134b26199f10dab6f1edb680ee161c0ece47e1", "usage-profile-core-extended-5a670388"),
        new("Profile", "sha256:3cf55ef99529b6051e2e5bea5c32bbecc5850819101e522fed1008be0599d6ad", "usage-profile-core-database-3cf55ef9"),
        new("Profile.get_DB", "sha256:a8448202da1e84bb21aa6672fee1c03fb401390f51cf1ea4b2d6810af74aeecc", "usage-profile-core-get-db-a8448202"),
        new("read_csv_without_units", "sha256:77befcdc77b99adb5b3b7311f90774dd82ff72ba4eea1c6b7058419c1aff412a", "usage-profile-core-csv-77befcdc"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("KoreanUsageProfile", "class", "sha256:52fc20db82dbf6bb9654482bbd6d5d08dd2bcfdd42db88091dc00e0dfd5d87e6", "sha256:7594749ad9c4f32ae9f1ea29805b588ae2e6493decca92fda443e9363102903a", "exception", "immutable-validated-usage-profile-value-object"),
        new("KoreanUsageProfile.DHW_HEAT_PER_LITER", "constant", "sha256:7845fdd56019103d844c7b7a865059fbfe15574031fff7e4e921fdd375c285af", "sha256:43b0a1e070650d194d682674baf38e90a76f731ec3761d157708953c6ed428bf", "equivalent", null),
        new("KoreanUsageProfile.ID", "function", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da", "exception", "deterministic-native-usage-profile-identity"),
        new("KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL", "constant", "sha256:56324a51320f6e58c78e4f87d74a7154168d47d805bd66317c622c49911294f8", "sha256:7bf2eac9a816cecbf9a285d8e32c30a318024376221831c47fc41ef1200d8665", "equivalent", null),
        new("KoreanUsageProfile.__init__", "function", "sha256:89c656bd22ded0c0657f5a722839fafdb42cccd7f610bb43b7040cba6695805d", "sha256:f206c2112434015da09a81e129633eb8f6825e79569cc81d294f97410b474fcc", "exception", "validated-immutable-usage-profile-construction"),
        new("KoreanUsageProfile.occupied_hours", "function", "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876", "sha256:16138d28c27becbb4fa4a8cd449bd3cf98db4060d4b29455953384886f37ac75", "equivalent", null),
        new("KoreanUsageProfile.operating_days", "function", "sha256:3600cccc11bc6800f262c4e5f0aacb4e7f2bf7ca486cbc455c0376a25e228afd", "sha256:d86bd36f9d41592b8774143ffc945075fd0faa0cf82fc7b1ac571423f4a1f382", "equivalent", null),
        new("KoreanUsageProfile.to_dict", "function", "sha256:b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf", "sha256:368f4628ce0a2ef5bef5d48ec4f456c8f031be49094e21df37458ffd2a8ffec4", "exception", "typed-usage-profile-serialization"),
        new("KoreanUsageProfile.to_dragon", "function", "sha256:6f7976906c2ab650b07c77535c90a8ebdf8d495a52aebe95d2201ff513d29f07", "sha256:ae4c8cbc2e4327627bf44d9a2c9d9373c86b3e5a1526bd089fadf9ca0e6e6291", "equivalent", null),
        new("KoreanUsageProfileExtended", "class", "sha256:4e620273c8656d32f9be6d99fabfa0a3cfcafdfcd2098dc30dee339c4e58bb16", "sha256:65200e57b6567a313c3d3ac518535f8188a781c942d8f67c386e20bc55dce686", "exception", "usage-profile-source-discriminator"),
        new("Profile", "class", "sha256:bf35db5abe6e8851938c2d634421f972436bb46ab9abab1dca41465ffcd7e9d4", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "exception", "immutable-usage-profile-database"),
        new("Profile.get_DB", "function", "sha256:0d34914867d00b5b2ea706bb6109049695c2f386f02f0b59a77a3d51dcfc0011", "sha256:cb03eb616b3998116052d637f18e3d9ad13e571cf74878b01281b8b11d4406f6", "exception", "diagnostic-usage-profile-lookup"),
        new("read_csv_without_units", "function", "sha256:33729a0e3540283ad3b0b84235e4b6997278a0b31619ee3f51c3f1906460101e", "sha256:da342c3cae4fbd3456d7c2f712ae3670576b88c5fe264fab2f541db0bf84383c", "exception", "strict-invariant-profile-csv-reader"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dhw-heat-per-liter.database-factors", "constant", "KoreanUsageProfile.DHW_HEAT_PER_LITER", "returned", null),
        new("dhw-heat-per-liter.numeric-kind", "constant", "KoreanUsageProfile.DHW_HEAT_PER_LITER", "returned", null),
        new("dhw-heat-per-liter.value", "constant", "KoreanUsageProfile.DHW_HEAT_PER_LITER", "returned", null),
        new("occupied-hours.daytime", "occupied-hours", "KoreanUsageProfile.occupied_hours", "returned", null),
        new("occupied-hours.equal-full-day", "occupied-hours", "KoreanUsageProfile.occupied_hours", "returned", null),
        new("occupied-hours.overnight", "occupied-hours", "KoreanUsageProfile.occupied_hours", "returned", null),
        new("operating-days.all", "operating-days", "KoreanUsageProfile.operating_days", "returned", null),
        new("operating-days.none", "operating-days", "KoreanUsageProfile.operating_days", "returned", null),
        new("operating-days.sparse-order", "operating-days", "KoreanUsageProfile.operating_days", "returned", null),
        new("people-activity-level.database-factors", "constant", "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL", "returned", null),
        new("people-activity-level.numeric-kind", "constant", "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL", "returned", null),
        new("people-activity-level.value", "constant", "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL", "returned", null),
        new("profile-csv.greedy-header-and-quotes", "profile-csv", "read_csv_without_units", "returned", null),
        new("profile-csv.packaged-sources", "profile-csv", "read_csv_without_units", "returned", null),
        new("profile-csv.strip-unit-headers", "profile-csv", "read_csv_without_units", "returned", null),
        new("usage-profile-database.alias-topology", "usage-profile-database", "Profile", "returned", null),
        new("usage-profile-database.mutable-registry", "usage-profile-database", "Profile", "returned", null),
        new("usage-profile-database.type-topology", "usage-profile-database", "Profile", "returned", null),
        new("usage-profile-dict.exact-order", "usage-profile-dict", "KoreanUsageProfile.to_dict", "returned", null),
        new("usage-profile-dict.sparse-days", "usage-profile-dict", "KoreanUsageProfile.to_dict", "returned", null),
        new("usage-profile-dict.vacations", "usage-profile-dict", "KoreanUsageProfile.to_dict", "returned", null),
        new("usage-profile-dragon.all-database-profiles", "usage-profile-dragon", "KoreanUsageProfile.to_dragon", "returned", null),
        new("usage-profile-dragon.lighting-tie", "usage-profile-dragon", "KoreanUsageProfile.to_dragon", "returned", null),
        new("usage-profile-dragon.overnight-vacation", "usage-profile-dragon", "KoreanUsageProfile.to_dragon", "returned", null),
        new("usage-profile-extended.database-membership", "usage-profile-extended", "KoreanUsageProfileExtended", "returned", null),
        new("usage-profile-extended.datapath", "usage-profile-extended", "KoreanUsageProfileExtended", "returned", null),
        new("usage-profile-extended.subclass-topology", "usage-profile-extended", "KoreanUsageProfileExtended", "returned", null),
        new("usage-profile-id.explicit", "usage-profile-id", "KoreanUsageProfile.ID", "returned", null),
        new("usage-profile-id.private-mutation", "usage-profile-id", "KoreanUsageProfile.ID", "returned", null),
        new("usage-profile-id.runtime-default", "usage-profile-id", "KoreanUsageProfile.ID", "returned", null),
        new("usage-profile-init.complete", "usage-profile-init", "KoreanUsageProfile.__init__", "returned", null),
        new("usage-profile-init.mutable-inputs", "usage-profile-init", "KoreanUsageProfile.__init__", "returned", null),
        new("usage-profile-init.unvalidated", "usage-profile-init", "KoreanUsageProfile.__init__", "raised", "type"),
        new("usage-profile-lookup.all", "usage-profile-lookup", "Profile.get_DB", "returned", null),
        new("usage-profile-lookup.found-and-path", "usage-profile-lookup", "Profile.get_DB", "returned", null),
        new("usage-profile-lookup.missing", "usage-profile-lookup", "Profile.get_DB", "returned", null),
        new("usage-profile.alias-topology", "usage-profile", "KoreanUsageProfile", "returned", null),
        new("usage-profile.identity-equality", "usage-profile", "KoreanUsageProfile", "returned", null),
        new("usage-profile.mutable-surface", "usage-profile", "KoreanUsageProfile", "returned", null),
    };

    [Fact]
    public void MatchesPinnedPythonUsageProfileCore()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(OracleSha256, sha256);
        Assert.Equal(OracleByteLength, bytes.Length);

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo pinnedCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = pinnedCulture;
            CultureInfo.CurrentUICulture = pinnedCulture;

            using JsonDocument oracle = JsonDocument.Parse(bytes);
            JsonElement[] cases = ValidateCorpus(oracle.RootElement);
            var observations = new List<NativeObservation>(ExpectedCaseCount);
            for (int index = 0; index < cases.Length; index++)
            {
                JsonElement item = cases[index];
                CaseBinding binding = ExpectedCases[index];
                NativeCall call = ExecuteCase(binding, item.GetProperty("python").GetProperty("facts"));
                SymbolContract symbol = Assert.Single(
                    ExpectedSymbols,
                    candidate => candidate.Symbol == binding.Symbol);
                Assert.Equal(binding.NativeOutcome, call.Outcome);
                Assert.Equal(binding.NativeErrorCategory, call.ErrorCategory);
                Assert.NotEmpty(call.Facts);
                Assert.All(call.Facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
                Assert.Equal(call.Facts.Length, call.Facts.Distinct(StringComparer.Ordinal).Count());
                observations.Add(new NativeObservation(
                    binding.CaseId,
                    binding.Symbol,
                    call.Outcome,
                    call.ErrorCategory,
                    symbol.AdaptationId,
                    call.Facts));
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
                        path = OracleRepositoryPath,
                        sha256,
                    },
                    observations = symbolObservations.Select(item => new
                    {
                        adaptation_id = item.Adaptation,
                        case_id = item.CaseId,
                        native_error_category = item.NativeErrorCategory,
                        native_facts = item.NativeFacts,
                        native_outcome = item.NativeOutcome,
                    }).ToArray(),
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
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static JsonElement[] ValidateCorpus(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(root, "cases", "cases_sha256", "consumer_contract", "runtime", "schema", "symbols", "upstream");
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));
        Assert.False(Regex.IsMatch(
            root.GetRawText(),
            @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])",
            RegexOptions.CultureInvariant));

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal("847b01f68f438f560a986072bcaa7768fbf67897", RequiredString(upstream, "commit"));
        Assert.Equal("sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02", RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal("sha256:e43f07d41e1e90cb9dcb7207fce67d8a6cb93acf54242b7a87c0aa30dda1309c", RequiredString(upstream, "source_sha256"));

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
        Assert.Equal(ExpectedCases.Select(item => item.CaseId).OrderBy(item => item, StringComparer.Ordinal), ExpectedCases.Select(item => item.CaseId));
        Assert.Equal(ExpectedCaseCount, cases.Select(item => RequiredString(item, "id")).Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
        }

        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            Assert.Equal(3, ExpectedCases.Count(item => item.Symbol == evidence.Symbol));
        }

        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateSymbols(JsonElement symbolsElement)
    {
        JsonElement[] actual = symbolsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, actual.Length);
        Assert.Equal(ExpectedEvidence.Length, actual.Length);
        Assert.Equal(ExpectedEvidence.Length, ExpectedEvidence.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ExpectedEvidence, item => Assert.StartsWith("usage-profile-core-", item.AssertionId, StringComparison.Ordinal));
        for (int index = 0; index < actual.Length; index++)
        {
            JsonElement item = actual[index];
            SymbolContract symbol = ExpectedSymbols[index];
            EvidenceBinding evidence = ExpectedEvidence[index];
            Assert.Equal(symbol.Symbol, evidence.Symbol);
            AssertKeys(item, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(symbol.BodyHash, RequiredString(item, "body_hash"));
            Assert.Equal(symbol.Kind, RequiredString(item, "kind"));
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(symbol.SignatureHash, RequiredString(item, "signature_hash"));
            Assert.Equal(symbol.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(evidence.SymbolHash, RequiredString(item, "symbol_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement consumer)
    {
        AssertKeys(consumer, "adaptations", "case_count", "case_ids", "classifications", "float_encoding", "runtime_names", "target_symbols");
        Assert.Equal(ExpectedCaseCount, consumer.GetProperty("case_count").GetInt32());
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), consumer.GetProperty("case_ids").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(ExpectedSymbols.Select(item => item.Symbol), consumer.GetProperty("target_symbols").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal("python-binary64-hex-without-0x-prefix", RequiredString(consumer, "float_encoding"));
        Assert.Equal("policy-token-no-raw-address", RequiredString(consumer, "runtime_names"));

        JsonElement classifications = consumer.GetProperty("classifications");
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.Classification, RequiredString(classifications, symbol.Symbol));
        }

        SymbolContract[] adapted = ExpectedSymbols.Where(item => item.AdaptationId is not null).ToArray();
        JsonElement adaptations = consumer.GetProperty("adaptations");
        AssertKeys(adaptations, adapted.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in adapted)
        {
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
        }

        Assert.Equal(5, ExpectedSymbols.Count(item => item.Classification == "equivalent"));
        Assert.Equal(8, adapted.Length);
    }

    private static void ValidateCase(JsonElement item, CaseBinding binding)
    {
        SymbolContract symbol = Assert.Single(ExpectedSymbols, candidate => candidate.Symbol == binding.Symbol);
        bool adapted = symbol.AdaptationId is not null;
        AssertKeys(item, adapted
            ? new[] { "executor", "expected_dotnet", "id", "python", "symbol" }
            : new[] { "executor", "id", "python", "symbol" });
        Assert.Equal(binding.CaseId, RequiredString(item, "id"));
        Assert.Equal(binding.Executor, RequiredString(item, "executor"));
        Assert.Equal(binding.Symbol, RequiredString(item, "symbol"));

        if (adapted)
        {
            JsonElement expected = item.GetProperty("expected_dotnet");
            AssertKeys(expected, binding.NativeOutcome == "raised"
                ? new[] { "adaptation", "error_category", "outcome" }
                : new[] { "adaptation", "outcome" });
            Assert.Equal(symbol.AdaptationId, RequiredString(expected, "adaptation"));
            Assert.Equal(binding.NativeOutcome, RequiredString(expected, "outcome"));
            if (binding.NativeOutcome == "raised")
            {
                Assert.Equal(binding.NativeErrorCategory, RequiredString(expected, "error_category"));
            }
            else
            {
                Assert.Null(binding.NativeErrorCategory);
            }
        }
        else
        {
            Assert.Equal("equivalent", symbol.Classification);
            Assert.Equal("returned", binding.NativeOutcome);
            Assert.Null(binding.NativeErrorCategory);
        }

        JsonElement python = item.GetProperty("python");
        bool pythonRaised = binding.CaseId == "usage-profile-lookup.missing";
        AssertKeys(python, pythonRaised
            ? new[] { "error_category", "exception_type", "facts", "message", "outcome" }
            : new[] { "facts", "outcome" });
        Assert.Equal(pythonRaised ? "raised" : "returned", RequiredString(python, "outcome"));
        if (pythonRaised)
        {
            Assert.Equal("range", RequiredString(python, "error_category"));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(python, "exception_type")));
            _ = RequiredString(python, "message");
        }

        JsonElement facts = python.GetProperty("facts");
        AssertExpectedFactKeys(binding.CaseId, facts);
        ValidateFactNode(facts);
    }

    private static void AssertExpectedFactKeys(string caseId, JsonElement facts)
    {
        string[] keys = caseId switch
        {
            "dhw-heat-per-liter.database-factors" or "people-activity-level.database-factors" => new[] { "factors" },
            "dhw-heat-per-liter.numeric-kind" or "people-activity-level.numeric-kind" => new[] { "arithmetic_probe", "is_boolean", "is_integral" },
            "dhw-heat-per-liter.value" or "people-activity-level.value" => new[] { "value" },
            "occupied-hours.daytime" or "occupied-hours.equal-full-day" or "occupied-hours.overnight" => new[] { "occupant_end", "occupant_start", "value" },
            "operating-days.all" or "operating-days.none" or "operating-days.sparse-order" => new[] { "flags", "value" },
            "profile-csv.greedy-header-and-quotes" or "profile-csv.strip-unit-headers" => new[] { "columns", "row_count", "row_values" },
            "profile-csv.packaged-sources" => new[] { "extended", "standard" },
            "usage-profile-database.alias-topology" => new[] { "all_values_are_registry_values", "found_is_registry_value", "registry_count" },
            "usage-profile-database.mutable-registry" => new[] { "count_after_restore", "count_during_change", "temporary_value_was_observable" },
            "usage-profile-database.type-topology" => new[] { "database_attribute_is_shared", "mro_names", "profile_instances_in_registry", "registry_count" },
            "usage-profile-dict.exact-order" => new[] { "key_order", "result" },
            "usage-profile-dict.sparse-days" => new[] { "key_order", "operate_weekdays" },
            "usage-profile-dict.vacations" => new[] { "key_order", "vacations" },
            "usage-profile-dragon.all-database-profiles" => new[] { "profile_count", "profiles", "schedule_slots" },
            "usage-profile-dragon.lighting-tie" => new[] { "fractional_lighting_value_count", "fractional_lighting_values", "profile", "schedule_slots" },
            "usage-profile-dragon.overnight-vacation" => new[] { "leap_day_failure", "overnight", "profile", "schedule_slots", "vacation_count", "wrapped_vacation_noop" },
            "usage-profile-extended.database-membership" => new[] { "extended_count", "extended_names", "total_count" },
            "usage-profile-extended.datapath" => new[] { "filename", "is_distinct_from_standard", "sha256" },
            "usage-profile-extended.subclass-topology" => new[] { "is_profile_subclass", "is_usage_profile_subclass", "mro_names" },
            "usage-profile-id.explicit" => new[] { "id", "property_is_read_only" },
            "usage-profile-id.private-mutation" => new[] { "after", "dictionary_has_mangled_key", "hash_tracks_after" },
            "usage-profile-id.runtime-default" => new[] { "identities_are_distinct", "left", "right" },
            "usage-profile-init.complete" => new[] { "snapshot" },
            "usage-profile-init.mutable-inputs" => new[] { "input_is_stored_reference", "stored_count_after_input_change", "stored_values" },
            "usage-profile-init.unvalidated" => new[] { "id", "name", "occupant_end", "occupant_start", "operate_in_monday", "vacations", "ventilation" },
            "usage-profile-lookup.all" => new[] { "dictionary_key_orders", "identities_match_registry_order", "names", "value_count" },
            "usage-profile-lookup.found-and-path" => new[] { "dictionary_key_order", "found_is_registry_value", "key", "path_count", "path_filenames" },
            "usage-profile-lookup.missing" => new[] { "database_count_unchanged", "key" },
            "usage-profile.alias-topology" => new[] { "input_is_stored_reference", "snapshot", "vacation_entry_is_shared" },
            "usage-profile.identity-equality" => new[] { "equal_hashes", "left_equals_right", "left_equals_self", "same_id" },
            "usage-profile.mutable-surface" => new[] { "cooling_attribute_deleted", "dynamic_note", "name", "ventilation" },
            _ => throw new Xunit.Sdk.XunitException("Unknown UsageProfile fact contract '" + caseId + "'."),
        };
        AssertKeys(facts, keys);
    }

    private static void ValidateFactNode(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateFactNode(item);
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            Assert.Contains(value.ValueKind, new[]
            {
                JsonValueKind.False, JsonValueKind.Null, JsonValueKind.Number,
                JsonValueKind.String, JsonValueKind.True,
            });
            if (value.ValueKind == JsonValueKind.Number)
            {
                _ = value.GetInt64();
            }

            return;
        }

        AssertUniqueObjectKeys(value);
        if (value.TryGetProperty("kind", out JsonElement kind) && kind.GetString() == "binary64")
        {
            AssertKeys(value, "hex_without_prefix", "kind");
            Assert.Matches(@"^(?:-?(?:inf|nan)|-?(?:0|1)\.[0-9a-f]{1,13}p[+-][0-9]+)$", RequiredString(value, "hex_without_prefix"));
        }

        if (value.TryGetProperty("policy", out _))
        {
            ValidateNameDescriptor(value);
        }

        if (value.TryGetProperty("values_sha256", out _))
        {
            ValidateScheduleDescriptor(value);
        }

        if (value.TryGetProperty("schedules", out _)
            && value.TryGetProperty("native_output_identity", out _))
        {
            ValidateConvertedProfile(value);
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            ValidateFactNode(property.Value);
        }
    }

    private static void ValidateNameDescriptor(JsonElement value)
    {
        string policy = RequiredString(value, "policy");
        Assert.Contains(policy, new[] { "literal", "tokenized-runtime-identities" });
        AssertKeys(value, "policy", "value");
        string name = RequiredString(value, "value");
        Assert.DoesNotMatch(@"0[xX][0-9A-Fa-f]{7,16}", name);
        if (policy == "tokenized-runtime-identities")
        {
            Assert.Contains("runtime-identity-", name, StringComparison.Ordinal);
        }
    }

    private static void ValidateScheduleDescriptor(JsonElement value)
    {
        AssertKeys(value, "idf_fields", "maximum", "minimum", "name", "schedule_type", "value_count", "values_encoding", "values_sha256");
        Assert.Equal(DragonSchedule.FixedLength * 144, value.GetProperty("value_count").GetInt32());
        Assert.Equal("binary64-hex-without-prefix-lines", RequiredString(value, "values_encoding"));
        Assert.Matches(@"^sha256:[0-9a-f]{64}$", RequiredString(value, "values_sha256"));
        Assert.Contains(RequiredString(value, "schedule_type"), new[] { "fraction", "onoff", "real", "temperature" });
        ValidateNameDescriptor(value.GetProperty("name"));
        Assert.NotEmpty(value.GetProperty("idf_fields").EnumerateArray());
        Assert.All(value.GetProperty("idf_fields").EnumerateArray(), item => Assert.Equal(JsonValueKind.String, item.ValueKind));
    }

    private static void ValidateConvertedProfile(JsonElement value)
    {
        AssertKeys(value, "domestic_hotwater", "name", "native_output_identity", "occupied_hours", "operating_days", "output_name", "schedules", "source", "source_identity", "upstream_output_name_equals_source_identity", "vacations", "ventilation");
        JsonElement schedules = value.GetProperty("schedules");
        AssertKeys(schedules, ScheduleSlots);
        Assert.True(value.GetProperty("upstream_output_name_equals_source_identity").GetBoolean());
        ValidateNameDescriptor(value.GetProperty("source_identity"));
        ValidateNameDescriptor(value.GetProperty("output_name"));
        AssertJsonEquivalent(value.GetProperty("source_identity"), value.GetProperty("output_name"));
        string source = RequiredString(value, "source");
        JsonElement identity = value.GetProperty("native_output_identity");
        if (source is "standard" or "extended")
        {
            AssertKeys(identity, "adaptation", "comparison", "python_counterpart");
            Assert.Equal("deterministic-native-usage-profile-identity", RequiredString(identity, "adaptation"));
            Assert.Equal("native-only-output-id-equals-native-source-usage-profile-id", RequiredString(identity, "comparison"));
        }
        else
        {
            Assert.Equal("custom", source);
            AssertKeys(identity, "comparison", "python_counterpart");
            Assert.Equal("native-only-output-id-equals-exact-source-usage-profile-id", RequiredString(identity, "comparison"));
        }

        Assert.Equal("absent", RequiredString(identity, "python_counterpart"));
    }

    private static NativeCall ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Executor switch
        {
            "constant" => ExecuteConstant(binding.CaseId, pythonFacts),
            "occupied-hours" => ExecuteOccupiedHours(binding.CaseId, pythonFacts),
            "operating-days" => ExecuteOperatingDays(binding.CaseId, pythonFacts),
            "profile-csv" => ExecuteProfileCsv(binding.CaseId, pythonFacts),
            "usage-profile-database" => ExecuteDatabase(binding.CaseId, pythonFacts),
            "usage-profile-dict" => ExecuteDictionary(binding.CaseId, pythonFacts),
            "usage-profile-dragon" => ExecuteDragon(binding.CaseId, pythonFacts),
            "usage-profile-extended" => ExecuteExtended(binding.CaseId, pythonFacts),
            "usage-profile-id" => ExecuteId(binding.CaseId),
            "usage-profile-init" => ExecuteInit(binding.CaseId, pythonFacts),
            "usage-profile-lookup" => ExecuteLookup(binding.CaseId, pythonFacts),
            "usage-profile" => ExecuteUsageProfile(binding.CaseId, pythonFacts),
            _ => throw new Xunit.Sdk.XunitException(
                "Unknown UsageProfile core executor '" + binding.Executor + "'."),
        };
    }

    private static NativeCall ExecuteConstant(string caseId, JsonElement pythonFacts)
    {
        bool domesticHotWater = caseId.StartsWith("dhw-heat-per-liter.", StringComparison.Ordinal);
        double value = domesticHotWater
            ? UsageProfileConstants.DomesticHotWaterHeatWattHoursPerLitre
            : UsageProfileConstants.PeopleSensibleActivityWattsPerPerson;
        object actual;
        if (caseId.EndsWith(".value", StringComparison.Ordinal))
        {
            actual = new { value = checked((int)value) };
        }
        else if (caseId.EndsWith(".numeric-kind", StringComparison.Ordinal))
        {
            actual = new
            {
                arithmetic_probe = checked((int)value + 1),
                is_boolean = false,
                is_integral = value == Math.Truncate(value),
            };
        }
        else
        {
            actual = new
            {
                factors = SimpleDragonDatabase.Default.UsageProfiles.Items.Select(profile => new
                {
                    factor = EncodeBinary64(domesticHotWater
                        ? profile.DomesticHotWater / profile.OccupiedHours / value
                        : profile.Occupancy / profile.OccupiedHours / value),
                    name = profile.Name,
                }).ToArray(),
            };
        }

        AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
        return Returned(
            caseId,
            "native constant arithmetic matched the pinned value and numeric behavior",
            domesticHotWater
                ? "all 24 domestic-hot-water database factors matched exactly"
                : "all 24 people-activity database factors matched exactly");
    }

    private static NativeCall ExecuteOccupiedHours(string caseId, JsonElement pythonFacts)
    {
        (int start, int end) = caseId switch
        {
            "occupied-hours.daytime" => (9, 18),
            "occupied-hours.equal-full-day" => (8, 8),
            "occupied-hours.overnight" => (22, 6),
            _ => throw new Xunit.Sdk.XunitException("Unknown occupied-hours case."),
        };
        UsageProfile profile = CreateOracleProfile(occupantStart: start, occupantEnd: end);
        Assert.Equal(end > start ? end - start : 24 - (start - end), profile.OccupiedHours);
        var actual = new
        {
            occupant_end = end,
            occupant_start = start,
            value = checked((int)profile.OccupiedHours),
        };
        AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
        return Returned(caseId, "native occupied-hour duration matched the daytime, full-day, or overnight contract");
    }

    private static NativeCall ExecuteOperatingDays(string caseId, JsonElement pythonFacts)
    {
        HashSet<UsageDay> selected = caseId switch
        {
            "operating-days.all" => Enum.GetValues<UsageDay>().ToHashSet(),
            "operating-days.none" => new HashSet<UsageDay>(),
            "operating-days.sparse-order" => new HashSet<UsageDay>
            {
                UsageDay.Tuesday, UsageDay.Saturday, UsageDay.Holiday,
            },
            _ => throw new Xunit.Sdk.XunitException("Unknown operating-days case."),
        };
        IReadOnlyDictionary<UsageDay, bool> operation = Operation(selected);
        UsageProfile profile = CreateOracleProfile(operation: operation);
        string[] actualDays = profile.OperatingDays.Select(DayName).ToArray();
        Assert.Equal(selected.Count, actualDays.Length);
        var flags = DayNames.ToDictionary(
            name => name,
            name => profile.OperatesOn(ParseDay(name)),
            StringComparer.Ordinal);
        var actual = new { flags, value = actualDays };
        AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
        return Returned(caseId, "native active-day filtering preserved the pinned eight-day order and flags");
    }

    private static NativeCall ExecuteProfileCsv(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "profile-csv.packaged-sources")
        {
            string extendedHash = AssertEmbeddedCsvMatchesFixture(
                pythonFacts.GetProperty("extended"),
                SimpleDragonEmbeddedData.KoreanUsageProfileExtended);
            string standardHash = AssertEmbeddedCsvMatchesFixture(
                pythonFacts.GetProperty("standard"),
                SimpleDragonEmbeddedData.KoreanUsageProfile);
            return Returned(
                caseId,
                "both embedded CSV row counts, filenames, and stripped headers matched the pinned sources",
                "native embedded standard bytes were pinned independently as " + standardHash,
                "native embedded extended bytes were pinned independently as " + extendedHash);
        }

        if (caseId == "profile-csv.strip-unit-headers")
        {
            CsvDocument document = CsvDocument.Parse(
                "Alpha [kW],Beta[unit],Gamma\n1,2,3\n",
                "headers.csv",
                stripHeaderUnits: true);
            CsvRow row = Assert.Single(document.Rows);
            object actual = new
            {
                columns = document.Headers.ToArray(),
                row_count = document.Rows.Count,
                row_values = new[] { row.Integer("Alpha"), row.Integer("Beta"), row.Integer("Gamma") },
            };
            AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
            return Returned(caseId, "native trailing unit annotations were stripped with invariant integer parsing");
        }

        CsvDocument greedy = CsvDocument.Parse(
            "\"A [one] middle [two] suffix\",\"Comma, Header [u]\",Plain\n7,\"x,y\",9\n",
            "greedy.csv",
            stripHeaderUnits: true);
        Assert.Equal(
            new[] { "A [one] middle [two] suffix", "Comma, Header", "Plain" },
            greedy.Headers);
        CsvRow greedyRow = Assert.Single(greedy.Rows);
        Assert.Equal("7", greedyRow.Required(greedy.Headers[0]));
        Assert.Equal("x,y", greedyRow.Required(greedy.Headers[1]));
        Assert.Equal("9", greedyRow.Required(greedy.Headers[2]));
        Assert.Throws<CsvDataException>(() => CsvDocument.Parse("A,B\n\"open,1\n", "bad.csv", true));
        return Returned(
            caseId,
            "native RFC-style quoted comma parsing retained exact row values",
            "native unit stripping is deliberately trailing-only rather than Python's greedy header regex",
            "malformed quoting produced a source-aware CsvDataException");
    }

    private static NativeCall ExecuteDatabase(string caseId, JsonElement pythonFacts)
    {
        UsageProfileDatabase database = SimpleDragonDatabase.Default.UsageProfiles;
        IReadOnlyList<UsageProfile> items = database.Items;
        Assert.Equal(24, items.Count);
        if (caseId == "usage-profile-database.alias-topology")
        {
            UsageProfile first = items[0];
            LookupResult<UsageProfile> found = database.Find(first.Name);
            Assert.True(found.Found);
            Assert.Same(first, found.Require());
            object actual = new
            {
                all_values_are_registry_values = items.All(item => ReferenceEquals(item, database.Find(item.Name).Require())),
                found_is_registry_value = ReferenceEquals(first, found.Require()),
                registry_count = items.Count,
            };
            AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
            return Returned(caseId, "native ordered items and successful lookups share the same immutable profile instances");
        }

        if (caseId == "usage-profile-database.mutable-registry")
        {
            IList<UsageProfile> mutable = Assert.IsAssignableFrom<IList<UsageProfile>>(items);
            Assert.True(mutable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => mutable.Add(items[0]));
            Assert.Throws<NotSupportedException>(() => mutable.RemoveAt(0));
            Assert.Equal(24, database.Items.Count);
            Assert.False(database.Find("__ORACLE_TEMPORARY_PROFILE__").Found);
            return Returned(
                caseId,
                "native database collection rejected insertion and removal",
                "failed temporary lookup left the exact 24-profile registry unchanged");
        }

        Assert.True(typeof(UsageProfileDatabase).IsSealed);
        Assert.All(items, item => Assert.IsType<UsageProfile>(item));
        Assert.Equal(UsageProfileSource.Standard, items[0].Source);
        Assert.Contains(items, item => item.Source == UsageProfileSource.Extended);
        Assert.Empty(typeof(UsageProfileDatabase).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
        return Returned(
            caseId,
            "native database is a sealed typed aggregate rather than a mutable shared class registry",
            "all 24 entries carry an explicit stable source discriminator");
    }

    private static NativeCall ExecuteDictionary(string caseId, JsonElement pythonFacts)
    {
        UsageProfile profile;
        if (caseId == "usage-profile-dict.sparse-days")
        {
            profile = CreateOracleProfile(operation: Operation(new HashSet<UsageDay>
            {
                UsageDay.Monday, UsageDay.Thursday, UsageDay.Sunday,
            }));
        }
        else if (caseId == "usage-profile-dict.vacations")
        {
            profile = CreateOracleProfile(vacations: new[]
            {
                Vacation(1, 2, 3, 4),
                Vacation(11, 9, 12, 31),
            });
        }
        else
        {
            profile = CreateOracleProfile();
        }

        OrderedMap<object> dictionary = profile.ToDictionary();
        Assert.Equal(DictionaryKeys, dictionary.Keys);
        Assert.False(dictionary.ContainsKey("id"));
        Assert.False(dictionary.ContainsKey("source"));
        Assert.DoesNotContain(typeof(IDictionary<string, object>), dictionary.GetType().GetInterfaces());
        Assert.Equal(5d, Assert.IsType<double>(dictionary["lighting_hours"]));
        object actual = caseId switch
        {
            "usage-profile-dict.exact-order" => new
            {
                key_order = dictionary.Keys.ToArray(),
                result = EncodeOrderedMap(dictionary),
            },
            "usage-profile-dict.sparse-days" => new
            {
                key_order = dictionary.Keys.ToArray(),
                operate_weekdays = ((IReadOnlyList<string>)dictionary["operate_weekdays"]).ToArray(),
            },
            "usage-profile-dict.vacations" => new
            {
                key_order = dictionary.Keys.ToArray(),
                vacations = EncodeDictionaryValue(dictionary["vacations"]),
            },
            _ => throw new Xunit.Sdk.XunitException("Unknown dictionary case."),
        };
        AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
        AssertDictionaryNestedReadOnly(dictionary);
        return Returned(
            caseId,
            "native immutable map preserved the exact 14-key upstream order and selected content",
            "nested active-day and vacation collections rejected mutation");
    }

    private static NativeCall ExecuteDragon(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "usage-profile-dragon.all-database-profiles")
        {
            UsageProfile[] sources = SimpleDragonDatabase.Default.UsageProfiles.Items.ToArray();
            Assert.Equal(24, sources.Length);
            object[] profiles = sources.Select(source => ConvertedProfileDescriptor(source, databaseIdentity: true)).ToArray();
            object actual = new
            {
                profile_count = profiles.Length,
                profiles,
                schedule_slots = ScheduleSlots,
            };
            AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
            return Returned(
                caseId,
                "all 24 database profiles matched exact seven-schedule values hashes and IDF fields",
                "native converted names matched Python output names while native IDs matched source UsageProfile IDs");
        }

        if (caseId == "usage-profile-dragon.lighting-tie")
        {
            UsageProfile source = CreateOracleProfile(
                name: "Lighting Tie",
                occupantStart: 8,
                occupantEnd: 16,
                hvacStart: 8,
                hvacEnd: 16,
                lightingHours: 0.25d,
                vacations: Array.Empty<VacationPeriod>(),
                id: "PROFILE-LIGHTING-TIE");
            DragonProfile converted = GreenRetrofitConverter.ConvertProfile(source);
            double[] fractional = converted.Lighting!.DaySchedules
                .SelectMany(day => day)
                .Where(value => value != 0d && value != 1d)
                .ToArray();
            Assert.Equal(522, fractional.Length);
            double[] distinct = fractional.Distinct().OrderBy(value => value).ToArray();
            object actual = new
            {
                fractional_lighting_value_count = fractional.Length,
                fractional_lighting_values = distinct.Select(EncodeBinary64).ToArray(),
                profile = ConvertedProfileDescriptor(source, databaseIdentity: false),
                schedule_slots = ScheduleSlots,
            };
            AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
            return Returned(
                caseId,
                "lighting tie allocation produced exactly 522 fractional slots at binary64 0.75",
                "all custom converted schedule values and IDF fields matched with a fresh graph");
        }

        UsageProfile overnight = CreateOracleProfile(
            name: "Overnight Vacation",
            occupantStart: 22,
            occupantEnd: 6,
            hvacStart: 21,
            hvacEnd: 7,
            lightingHours: 2d,
            vacations: new[] { Vacation(8, 1, 8, 15) },
            id: "PROFILE-OVERNIGHT-VACATION");
        Assert.Equal(
            overnight.OccupantEnd < overnight.OccupantStart,
            pythonFacts.GetProperty("overnight").GetBoolean());
        AssertJsonEquivalent(
            pythonFacts.GetProperty("profile"),
            JsonSerializer.SerializeToElement(
                ConvertedProfileDescriptor(overnight, databaseIdentity: false)));
        Assert.Equal(
            ScheduleSlots,
            pythonFacts.GetProperty("schedule_slots")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(overnight.Vacations.Count, pythonFacts.GetProperty("vacation_count").GetInt32());

        JsonElement wrappedFacts = pythonFacts.GetProperty("wrapped_vacation_noop");
        AssertKeys(
            wrappedFacts,
            "end",
            "schedule_slots_equal_without_vacation",
            "start",
            "vacation_mask_positive_days");
        Assert.Equal("12/29", RequiredString(wrappedFacts, "start"));
        Assert.Equal("01/03", RequiredString(wrappedFacts, "end"));
        Assert.Equal(0, wrappedFacts.GetProperty("vacation_mask_positive_days").GetInt32());
        Assert.Equal(
            ScheduleSlots,
            wrappedFacts.GetProperty("schedule_slots_equal_without_vacation")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());

        VacationPeriod reversed = Vacation(12, 29, 1, 3);
        UsageProfile wrapped = CreateOracleProfile(
            name: "Wrapped Vacation",
            occupantStart: 22,
            occupantEnd: 6,
            hvacStart: 21,
            hvacEnd: 7,
            lightingHours: 2d,
            vacations: new[] { reversed },
            id: "PROFILE-WRAPPED-VACATION");
        UsageProfile noVacation = CreateOracleProfile(
            name: "Wrapped Vacation",
            occupantStart: 22,
            occupantEnd: 6,
            hvacStart: 21,
            hvacEnd: 7,
            lightingHours: 2d,
            vacations: Array.Empty<VacationPeriod>(),
            id: "PROFILE-WRAPPED-VACATION");
        DragonSchedule[] wrappedSchedules = ProfileSchedules(
            GreenRetrofitConverter.ConvertProfile(wrapped));
        DragonSchedule[] baselineSchedules = ProfileSchedules(
            GreenRetrofitConverter.ConvertProfile(noVacation));
        for (int index = 0; index < ScheduleSlots.Length; index++)
        {
            Assert.NotSame(baselineSchedules[index], wrappedSchedules[index]);
            Assert.Equal(baselineSchedules[index], wrappedSchedules[index]);
        }

        Assert.Single(wrapped.Vacations);
        Assert.Same(reversed, wrapped.Vacations[0]);

        JsonElement leapFacts = pythonFacts.GetProperty("leap_day_failure");
        AssertKeys(leapFacts, "error_category", "exception_type", "facts", "message", "outcome");
        Assert.Equal("domain", RequiredString(leapFacts, "error_category"));
        Assert.Equal("ValueError", RequiredString(leapFacts, "exception_type"));
        Assert.Equal("day is out of range for month", RequiredString(leapFacts, "message"));
        Assert.Equal("raised", RequiredString(leapFacts, "outcome"));
        JsonElement leapBoundary = leapFacts.GetProperty("facts");
        AssertKeys(leapBoundary, "end", "start");
        Assert.Equal("02/29", RequiredString(leapBoundary, "start"));
        Assert.Equal("03/01", RequiredString(leapBoundary, "end"));

        VacationPeriod leapPeriod = Vacation(2, 29, 3, 1);
        UsageProfile leapDay = CreateOracleProfile(
            name: "Leap Day Vacation",
            vacations: new[] { leapPeriod },
            id: "PROFILE-LEAP-DAY-VACATION");
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GreenRetrofitConverter.ConvertProfile(leapDay));
        Assert.Single(leapDay.Vacations);
        Assert.Same(leapPeriod, leapDay.Vacations[0]);
        return Returned(
            caseId,
            "overnight occupancy and HVAC windows matched across the annual grid",
            "the exact August vacation mask covered all seven schedules including hot water",
            "a reversed annual vacation was a no-op across all seven fresh native schedule graphs",
            "Python ValueError/domain and native ArgumentOutOfRangeException both rejected February 29 while native source state stayed unchanged");
    }

    private static NativeCall ExecuteExtended(string caseId, JsonElement pythonFacts)
    {
        IReadOnlyList<UsageProfile> items = SimpleDragonDatabase.Default.UsageProfiles.Items;
        UsageProfile[] extended = items.Where(item => item.Source == UsageProfileSource.Extended).ToArray();
        if (caseId == "usage-profile-extended.database-membership")
        {
            object actual = new
            {
                extended_count = extended.Length,
                extended_names = extended.Select(item => item.Name).ToArray(),
                total_count = items.Count,
            };
            AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
            return Returned(caseId, "native Extended source membership matched the pinned database names and counts");
        }

        if (caseId == "usage-profile-extended.datapath")
        {
            byte[] bytes = SimpleDragonEmbeddedData.ReadAllBytes(SimpleDragonEmbeddedData.KoreanUsageProfileExtended);
            Assert.Equal(
                Path.GetFileName(SimpleDragonEmbeddedData.KoreanUsageProfileExtended),
                RequiredString(pythonFacts, "filename"));
            Assert.True(pythonFacts.GetProperty("is_distinct_from_standard").GetBoolean());
            Assert.NotEqual(
                SimpleDragonEmbeddedData.KoreanUsageProfileExtended,
                SimpleDragonEmbeddedData.KoreanUsageProfile);
            string upstreamHash = RequiredString(pythonFacts, "sha256");
            string nativeHash = Sha256(bytes);
            Assert.Equal(upstreamHash, Sha256(NormalizeCrlf(bytes)));
            return Returned(
                caseId,
                "native extended source retained the exact distinct upstream filename",
                "CRLF-to-LF normalized native content matched pinned upstream hash " + upstreamHash,
                "native embedded raw bytes remained independently pinned as " + nativeHash);
        }

        Assert.Equal(0, (int)UsageProfileSource.Standard);
        Assert.Equal(1, (int)UsageProfileSource.Extended);
        Assert.Equal(2, (int)UsageProfileSource.Custom);
        Assert.True(typeof(UsageProfile).IsSealed);
        Assert.All(extended, item => Assert.Equal(UsageProfileSource.Extended, item.Source));
        return Returned(
            caseId,
            "native source topology uses a stable enum discriminator instead of mutable Python subclasses",
            "Standard and Extended persisted ordinals remained unchanged when Custom was appended");
    }

    private static NativeCall ExecuteId(string caseId)
    {
        if (caseId == "usage-profile-id.explicit")
        {
            Assert.Throws<ArgumentException>(() => new EntityId("  explicit ID  "));
            UsageProfile profile = CreateOracleProfile(id: "PROFILE-EXPLICIT");
            Assert.Equal("PROFILE-EXPLICIT", profile.Id.Value);
            Assert.Null(typeof(UsageProfile).GetProperty(nameof(UsageProfile.Id))!.SetMethod);
            return Returned(
                caseId,
                "valid explicit native EntityId was preserved exactly and is read-only",
                "whitespace-bearing Python identity was rejected by the native EntityId invariant");
        }

        if (caseId == "usage-profile-id.runtime-default")
        {
            UsageProfile left = CreateProfileWithGeneratedId();
            UsageProfile right = CreateProfileWithGeneratedId();
            Assert.Equal(left.Id, right.Id);
            Assert.Matches(@"^PRFL-[0-9a-f]{24}$", left.Id.Value);
            return Returned(
                caseId,
                "two equal native inputs generated the same non-runtime deterministic identity",
                "generated identity contained no process address or mutable counter");
        }

        UsageProfile stable = CreateOracleProfile(id: "PROFILE-BEFORE");
        PropertyInfo property = typeof(UsageProfile).GetProperty(nameof(UsageProfile.Id))!;
        Assert.Null(property.SetMethod);
        Assert.All(
            typeof(UsageProfile).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => Assert.True(field.IsInitOnly));
        _ = stable.ToDictionary();
        Assert.Equal("PROFILE-BEFORE", stable.Id.Value);
        return Returned(
            caseId,
            "native identity has no setter or writable backing field",
            "serialization left the exact EntityId unchanged");
    }

    private static NativeCall ExecuteInit(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "usage-profile-init.complete")
        {
            UsageProfile profile = CreateOracleProfile();
            Assert.Equal(5d, Assert.IsType<double>((object)profile.LightingHours));
            AssertJsonEquivalent(
                pythonFacts.GetProperty("snapshot"),
                JsonSerializer.SerializeToElement(ProfileSnapshot(profile)));
            return Returned(
                caseId,
                "native validated construction preserved every supplied scalar, flag, vacation, and explicit ID",
                "the complete semantic snapshot matched the pinned upstream profile");
        }

        if (caseId == "usage-profile-init.mutable-inputs")
        {
            var operation = Operation(DefaultOperatingDays()).ToDictionary(item => item.Key, item => item.Value);
            var vacations = new List<VacationPeriod> { Vacation(2, 1, 2, 2) };
            UsageProfile profile = CreateOracleProfile(operation: operation, vacations: vacations);
            Assert.Equal(5d, Assert.IsType<double>((object)profile.LightingHours));
            operation[UsageDay.Monday] = false;
            vacations.Add(Vacation(3, 1, 3, 2));
            Assert.True(profile.OperatesOn(UsageDay.Monday));
            Assert.Single(profile.Vacations);
            Assert.Same(vacations[0], profile.Vacations[0]);
            AssertReadOnly(profile.Vacations);
            return Returned(
                caseId,
                "native constructor copied mutable operation and vacation shells atomically",
                "immutable VacationPeriod item references were safely preserved while later input changes were isolated");
        }

        var operationSource = Operation(DefaultOperatingDays());
        ArgumentNullException nullName = Assert.Throws<ArgumentNullException>(() =>
            new UsageProfile(
                null!, 9, 18, 7, 19, 1.25d, 40d, 5d, 30d, 42d, 20d, 26d,
                operationSource));
        Assert.Equal("name", nullName.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateOracleProfile(occupantStart: 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateOracleProfile(ventilation: double.NaN));
        Assert.Throws<ArgumentException>(() => CreateOracleProfile(
            operation: new Dictionary<UsageDay, bool> { [UsageDay.Monday] = true }));
        var invalidVacations = new VacationPeriod?[] { null };
        Assert.Throws<ArgumentException>(() => CreateOracleProfile(
            vacations: invalidVacations.Cast<VacationPeriod>()));
        Assert.Equal(8, operationSource.Count);
        return RaisedType(
            caseId,
            "null name was rejected with exact parameter diagnostics before construction escaped",
            "invalid hour, nonfinite value, incomplete day map, and null vacation were independently rejected",
            "all rejected inputs remained unchanged and no partially constructed object was observable");
    }

    private static NativeCall ExecuteLookup(string caseId, JsonElement pythonFacts)
    {
        UsageProfileDatabase database = SimpleDragonDatabase.Default.UsageProfiles;
        IReadOnlyList<UsageProfile> items = database.Items;
        if (caseId == "usage-profile-lookup.all")
        {
            object actual = new
            {
                dictionary_key_orders = items.Select(item => item.ToDictionary().Keys.ToArray()).ToArray(),
                identities_match_registry_order = items.All(item => ReferenceEquals(item, database.Find(item.Name).Require())),
                names = items.Select(item => item.Name).ToArray(),
                value_count = items.Count,
            };
            AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(actual));
            return Returned(caseId, "native ordered Items and per-name lookups matched all 24 upstream registry entries");
        }

        if (caseId == "usage-profile-lookup.found-and-path")
        {
            UsageProfile first = items[0];
            UsageProfile found = database.Find(first.Name).Require();
            Assert.Equal(
                found.ToDictionary().Keys,
                pythonFacts.GetProperty("dictionary_key_order").EnumerateArray()
                    .Select(item => item.GetString()!));
            Assert.True(pythonFacts.GetProperty("found_is_registry_value").GetBoolean());
            Assert.Equal(first.Name, RequiredString(pythonFacts, "key"));
            Assert.Equal(1, pythonFacts.GetProperty("path_count").GetInt32());
            Assert.Equal(
                new[] { Path.GetFileName(SimpleDragonEmbeddedData.KoreanUsageProfile) },
                pythonFacts.GetProperty("path_filenames").EnumerateArray()
                    .Select(item => item.GetString()!));
            Assert.Same(first, found);
            Assert.DoesNotContain(
                typeof(UsageProfileDatabase).GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                typeof(UsageProfileDatabase).GetMethods(BindingFlags.Public | BindingFlags.Instance),
                method => method.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
            return Returned(
                caseId,
                "native lookup returned the exact registry instance and ordered dictionary projection",
                "pinned Python exposes one standard CSV path while native lookup deliberately exposes no host path API");
        }

        int before = items.Count;
        LookupResult<UsageProfile> missing = database.Find("__MISSING_USAGE_PROFILE__");
        Assert.False(missing.Found);
        Diagnostic diagnostic = Assert.Single(missing.Diagnostics);
        Assert.Equal("SD.DB.PROFILE_NOT_FOUND", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("__MISSING_USAGE_PROFILE__", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(before, database.Items.Count);
        return Returned(
            caseId,
            "native missing lookup returned a typed failure instead of throwing",
            "diagnostic code, severity, requested key, and unchanged database count were verified");
    }

    private static NativeCall ExecuteUsageProfile(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "usage-profile.alias-topology")
        {
            var operation = Operation(DefaultOperatingDays()).ToDictionary(item => item.Key, item => item.Value);
            var vacations = new List<VacationPeriod> { Vacation(1, 2, 1, 3) };
            UsageProfile profile = CreateOracleProfile(operation: operation, vacations: vacations);
            Assert.Equal(5d, Assert.IsType<double>((object)profile.LightingHours));
            AssertJsonEquivalent(
                pythonFacts.GetProperty("snapshot"),
                JsonSerializer.SerializeToElement(ProfileSnapshot(profile)));
            Assert.NotSame(vacations, profile.Vacations);
            Assert.Same(vacations[0], profile.Vacations[0]);
            vacations.Clear();
            operation[UsageDay.Monday] = false;
            Assert.Single(profile.Vacations);
            Assert.True(profile.OperatesOn(UsageDay.Monday));
            return Returned(
                caseId,
                "native immutable shells isolated both supplied mutable containers",
                "the immutable vacation value reference and exact semantic snapshot were preserved");
        }

        if (caseId == "usage-profile.identity-equality")
        {
            UsageProfile left = CreateOracleProfile(id: "SAME-ID");
            UsageProfile right = CreateOracleProfile(id: "SAME-ID");
            Assert.NotSame(left, right);
            Assert.Equal(left.Id, right.Id);
            Assert.True(ReferenceEquals(left, left));
            Assert.False(left.Equals(right));
            return Returned(
                caseId,
                "native profiles preserve stable equal EntityIds without claiming Python ID-based object equality",
                "self identity and distinct immutable profile instances were explicit");
        }

        UsageProfile immutable = CreateOracleProfile();
        AssertNoPublicSetters(typeof(UsageProfile));
        Assert.DoesNotContain(
            typeof(UsageProfile).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name is "Add" or "Append" or "Clear" or "Insert" or "Remove" or "RemoveAt" or "SetItem");
        OrderedMap<object> projected = immutable.ToDictionary();
        Assert.Equal("Oracle Probe", immutable.Name);
        Assert.Equal(1.25d, immutable.Ventilation);
        Assert.Equal("Oracle Probe", projected["name"]);
        return Returned(
            caseId,
            "native profile exposes no public setter, dynamic member, deletion, or mutator route",
            "read-only projection left name, ventilation, and all source state unchanged");
    }

    private static object ConvertedProfileDescriptor(UsageProfile source, bool databaseIdentity)
    {
        EntityId sourceId = source.Id;
        string sourceName = source.Name;
        UsageDay[] sourceDays = source.OperatingDays.ToArray();
        VacationPeriod[] sourceVacations = source.Vacations.ToArray();
        DragonProfile converted = GreenRetrofitConverter.ConvertProfile(source);
        string upstreamIdentity = databaseIdentity ? "$FROM_DB$:" + source.Name : source.Id.Value;
        Assert.Equal(upstreamIdentity, converted.Name);
        Assert.Equal(source.Id, converted.Id);
        Assert.Equal(sourceId, source.Id);
        Assert.Equal(sourceName, source.Name);
        Assert.Equal(sourceDays, source.OperatingDays);
        Assert.Equal(sourceVacations, source.Vacations);

        if (!databaseIdentity)
        {
            DragonProfile repeated = GreenRetrofitConverter.ConvertProfile(source);
            Assert.NotSame(converted, repeated);
            Assert.Equal(converted, repeated);
            Assert.NotSame(converted.HeatingSetpoint, repeated.HeatingSetpoint);
            Assert.NotSame(converted.HotWater, repeated.HotWater);
        }

        DragonSchedule[] schedules = ProfileSchedules(converted);
        var scheduleDescriptors = new Dictionary<string, object>(StringComparer.Ordinal);
        for (int index = 0; index < ScheduleSlots.Length; index++)
        {
            scheduleDescriptors.Add(ScheduleSlots[index], ScheduleDescriptor(schedules[index]));
        }

        object nativeOutputIdentity = databaseIdentity
            ? new
            {
                adaptation = "deterministic-native-usage-profile-identity",
                comparison = "native-only-output-id-equals-native-source-usage-profile-id",
                python_counterpart = "absent",
            }
            : new
            {
                comparison = "native-only-output-id-equals-exact-source-usage-profile-id",
                python_counterpart = "absent",
            };
        return new
        {
            domestic_hotwater = databaseIdentity
                ? checked((int)source.DomesticHotWater)
                : EncodeBinary64(source.DomesticHotWater),
            name = source.Name,
            native_output_identity = nativeOutputIdentity,
            occupied_hours = checked((int)source.OccupiedHours),
            operating_days = source.OperatingDays.Select(DayName).ToArray(),
            output_name = EncodeName(converted.Name),
            schedules = scheduleDescriptors,
            source = source.Source.ToString().ToLowerInvariant(),
            source_identity = EncodeName(upstreamIdentity),
            upstream_output_name_equals_source_identity = converted.Name == upstreamIdentity,
            vacations = source.Vacations.Select(period => new
            {
                end = period.End.ToString(),
                start = period.Start.ToString(),
            }).ToArray(),
            ventilation = source.Source == UsageProfileSource.Extended
                ? checked((int)source.Ventilation)
                : EncodeBinary64(source.Ventilation),
        };
    }

    private static object ScheduleDescriptor(DragonSchedule schedule)
    {
        Assert.Equal(DragonSchedule.FixedLength, schedule.Count);
        Assert.Equal(DragonSchedule.FixedLength, schedule.DaySchedules.Count);
        IdfObject idf = schedule.ToIdfObject();
        Assert.Equal("Schedule:Compact", idf.ObjectType);
        string[] fields = idf.Fields
            .Select(field => NormalizeRuntimeIdentities(field.Value))
            .ToArray();
        Assert.NotEmpty(fields);
        Assert.Equal(NormalizeRuntimeIdentities(schedule.Name), fields[0]);
        object maximum = schedule.Type == DragonScheduleType.OnOff
            ? checked((int)schedule.Maximum)
            : EncodeBinary64(schedule.Maximum);
        object minimum = schedule.Type == DragonScheduleType.OnOff
            ? checked((int)schedule.Minimum)
            : EncodeBinary64(schedule.Minimum);
        return new
        {
            idf_fields = fields,
            maximum,
            minimum,
            name = EncodeScheduleName(schedule.Name),
            schedule_type = schedule.Type.ToString().ToLowerInvariant(),
            value_count = DragonSchedule.FixedLength * 144,
            values_encoding = "binary64-hex-without-prefix-lines",
            values_sha256 = ScheduleValuesSha256(schedule),
        };
    }

    private static string ScheduleValuesSha256(DragonSchedule schedule)
    {
        using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var encoded = new Dictionary<long, byte[]>();
        int count = 0;
        foreach (Dragons.InvisibleDragon.Profile.DaySchedule day in schedule.DaySchedules)
        {
            foreach (double value in day)
            {
                long bits = BitConverter.DoubleToInt64Bits(value);
                if (!encoded.TryGetValue(bits, out byte[]? line))
                {
                    line = Encoding.ASCII.GetBytes(ToPythonHexWithoutPrefix(value) + "\n");
                    encoded.Add(bits, line);
                }

                digest.AppendData(line);
                count++;
            }
        }

        Assert.Equal(DragonSchedule.FixedLength * 144, count);
        return "sha256:" + Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
    }

    private static DragonSchedule[] ProfileSchedules(DragonProfile profile)
    {
        DragonSchedule[] schedules =
        {
            Assert.IsType<DragonSchedule>(profile.HeatingSetpoint),
            Assert.IsType<DragonSchedule>(profile.CoolingSetpoint),
            Assert.IsType<DragonSchedule>(profile.HvacAvailability),
            Assert.IsType<DragonSchedule>(profile.Occupant),
            Assert.IsType<DragonSchedule>(profile.Lighting),
            Assert.IsType<DragonSchedule>(profile.Equipment),
            Assert.IsType<DragonSchedule>(profile.HotWater),
        };
        Assert.Equal(DragonScheduleType.Temperature, schedules[0].Type);
        Assert.Equal(DragonScheduleType.Temperature, schedules[1].Type);
        Assert.Equal(DragonScheduleType.OnOff, schedules[2].Type);
        Assert.Equal(DragonScheduleType.Real, schedules[3].Type);
        Assert.Equal(DragonScheduleType.Fraction, schedules[4].Type);
        Assert.Equal(DragonScheduleType.Real, schedules[5].Type);
        Assert.Equal(DragonScheduleType.Real, schedules[6].Type);
        return schedules;
    }

    private static object ProfileSnapshot(UsageProfile profile)
    {
        var flags = DayNames.ToDictionary(
            name => name,
            name => profile.OperatesOn(ParseDay(name)),
            StringComparer.Ordinal);
        return new
        {
            id = EncodeName(profile.Id.Value),
            name = profile.Name,
            occupant_start = profile.OccupantStart,
            occupant_end = profile.OccupantEnd,
            hvac_start = profile.HvacStart,
            hvac_end = profile.HvacEnd,
            ventilation = EncodeBinary64(profile.Ventilation),
            domestic_hotwater = EncodeBinary64(profile.DomesticHotWater),
            lighting_hours = checked((int)profile.LightingHours),
            occupancy = EncodeBinary64(profile.Occupancy),
            equipment = EncodeBinary64(profile.Equipment),
            heating_setpoint = EncodeBinary64(profile.HeatingSetpoint),
            cooling_setpoint = EncodeBinary64(profile.CoolingSetpoint),
            operate_flags = flags,
            vacations = profile.Vacations.Select(period => new[]
            {
                new[] { period.Start.Month, period.Start.Day },
                new[] { period.End.Month, period.End.Day },
            }).ToArray(),
        };
    }

    private static UsageProfile CreateOracleProfile(
        string name = "Oracle Probe",
        int occupantStart = 9,
        int occupantEnd = 18,
        int hvacStart = 7,
        int hvacEnd = 19,
        double ventilation = 1.25d,
        double domesticHotWater = 40d,
        double lightingHours = 5d,
        double occupancy = 30d,
        double equipment = 42d,
        double heatingSetpoint = 20d,
        double coolingSetpoint = 26d,
        IReadOnlyDictionary<UsageDay, bool>? operation = null,
        IEnumerable<VacationPeriod>? vacations = null,
        UsageProfileSource source = UsageProfileSource.Custom,
        string id = "PROFILE-ORACLE-PROBE")
    {
        return new UsageProfile(
            name,
            occupantStart,
            occupantEnd,
            hvacStart,
            hvacEnd,
            ventilation,
            domesticHotWater,
            lightingHours,
            occupancy,
            equipment,
            heatingSetpoint,
            coolingSetpoint,
            operation ?? Operation(DefaultOperatingDays()),
            vacations ?? new[] { Vacation(7, 1, 7, 7) },
            source,
            new EntityId(id));
    }

    private static UsageProfile CreateProfileWithGeneratedId()
    {
        return new UsageProfile(
            "Oracle Probe",
            9,
            18,
            7,
            19,
            1.25d,
            40d,
            5d,
            30d,
            42d,
            20d,
            26d,
            Operation(DefaultOperatingDays()),
            new[] { Vacation(7, 1, 7, 7) },
            UsageProfileSource.Custom);
    }

    private static Dictionary<UsageDay, bool> Operation(ISet<UsageDay> selected)
    {
        return Enum.GetValues<UsageDay>().ToDictionary(day => day, selected.Contains);
    }

    private static HashSet<UsageDay> DefaultOperatingDays() => new()
    {
        UsageDay.Monday,
        UsageDay.Tuesday,
        UsageDay.Wednesday,
        UsageDay.Thursday,
        UsageDay.Friday,
    };

    private static string DayName(UsageDay day) => day.ToString().ToLowerInvariant();

    private static UsageDay ParseDay(string value) => Enum.Parse<UsageDay>(value, ignoreCase: true);

    private static VacationPeriod Vacation(
        int startMonth,
        int startDay,
        int endMonth,
        int endDay) => new(
            new MonthDay(startMonth, startDay),
            new MonthDay(endMonth, endDay));

    private static Dictionary<string, object?> EncodeOrderedMap(OrderedMap<object> value)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string key, object item) in value)
        {
            result.Add(
                key,
                key == "lighting_hours" && item is double lightingHours
                    ? checked((int)lightingHours)
                    : EncodeDictionaryValue(item));
        }

        return result;
    }

    private static object? EncodeDictionaryValue(object? value)
    {
        return value switch
        {
            null => null,
            double number => EncodeBinary64(number),
            OrderedMap<object> map => EncodeOrderedMap(map),
            IEnumerable<OrderedMap<object>> maps => maps.Select(EncodeOrderedMap).ToArray(),
            IEnumerable<string> strings when value is not string => strings.ToArray(),
            _ => value,
        };
    }

    private static void AssertDictionaryNestedReadOnly(OrderedMap<object> dictionary)
    {
        IReadOnlyList<string> days = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            dictionary["operate_weekdays"]);
        AssertReadOnly(days);
        IReadOnlyList<OrderedMap<object>> vacations =
            Assert.IsAssignableFrom<IReadOnlyList<OrderedMap<object>>>(dictionary["vacations"]);
        AssertReadOnly(vacations);
        Assert.All(
            vacations,
            map => Assert.DoesNotContain(
                typeof(IDictionary<string, object>),
                map.GetType().GetInterfaces()));
    }

    private static string AssertEmbeddedCsvMatchesFixture(JsonElement expected, string path)
    {
        AssertKeys(expected, "columns", "filename", "row_count", "sha256");
        CsvDocument document = CsvDocument.ReadEmbedded(path, stripHeaderUnits: true);
        byte[] bytes = SimpleDragonEmbeddedData.ReadAllBytes(path);
        Assert.Equal(
            expected.GetProperty("columns").EnumerateArray().Select(item => item.GetString()!),
            document.Headers);
        Assert.Equal(RequiredString(expected, "filename"), Path.GetFileName(path));
        Assert.Equal(expected.GetProperty("row_count").GetInt32(), document.Rows.Count);
        string upstreamHash = RequiredString(expected, "sha256");
        Assert.Matches(@"^sha256:[0-9a-f]{64}$", upstreamHash);
        Assert.Equal(upstreamHash, Sha256(NormalizeCrlf(bytes)));
        string repositoryCopy = FindRepositoryFile("data/simple-dragon/" + path);
        Assert.Equal(File.ReadAllBytes(repositoryCopy), bytes);
        return Sha256(bytes);
    }

    private static byte[] NormalizeCrlf(byte[] value)
    {
        using var output = new MemoryStream(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == (byte)'\r'
                && index + 1 < value.Length
                && value[index + 1] == (byte)'\n')
            {
                output.WriteByte((byte)'\n');
                index++;
            }
            else
            {
                output.WriteByte(value[index]);
            }
        }

        return output.ToArray();
    }

    private static object EncodeName(string value) => new
    {
        policy = "literal",
        value,
    };

    private static object EncodeScheduleName(string value)
    {
        string normalized = NormalizeRuntimeIdentities(value);
        return new
        {
            policy = normalized == value ? "literal" : "tokenized-runtime-identities",
            value = normalized,
        };
    }

    private static string NormalizeRuntimeIdentities(string value) => value
        .Replace("0xAUTO0000", "runtime-identity-0001", StringComparison.Ordinal)
        .Replace("0xAUTO0001", "runtime-identity-0002", StringComparison.Ordinal)
        .Replace("0xAUTO0002", "runtime-identity-0003", StringComparison.Ordinal);

    private static object EncodeBinary64(double value) => new
    {
        hex_without_prefix = ToPythonHexWithoutPrefix(value),
        kind = "binary64",
    };

    private static string ToPythonHexWithoutPrefix(double value)
    {
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-inf";
        }

        long signedBits = BitConverter.DoubleToInt64Bits(value);
        bool negative = signedBits < 0;
        ulong magnitude = unchecked((ulong)signedBits) & 0x7fff_ffff_ffff_ffffUL;
        string sign = negative ? "-" : string.Empty;
        if (magnitude == 0)
        {
            return sign + "0.0p+0";
        }

        int exponentBits = (int)((magnitude >> 52) & 0x7ffUL);
        ulong fraction = magnitude & 0x000f_ffff_ffff_ffffUL;
        if (exponentBits == 0)
        {
            return $"{sign}0.{fraction:x13}p-1022";
        }

        int exponent = exponentBits - 1023;
        return $"{sign}1.{fraction:x13}p{(exponent >= 0 ? "+" : string.Empty)}{exponent}";
    }

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void AssertReadOnly<T>(IReadOnlyList<T> values)
    {
        IList<T> mutable = Assert.IsAssignableFrom<IList<T>>(values);
        Assert.True(mutable.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
        if (values.Count > 0)
        {
            Assert.Throws<NotSupportedException>(() => mutable[0] = values[0]);
            Assert.Throws<NotSupportedException>(() => mutable.Add(values[0]));
            Assert.Throws<NotSupportedException>(() => mutable.Insert(0, values[0]));
            Assert.Throws<NotSupportedException>(() => mutable.Remove(values[0]));
            Assert.Throws<NotSupportedException>(() => mutable.RemoveAt(0));
        }
    }

    private static void AssertNoPublicSetters(Type type)
    {
        Assert.All(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => Assert.Null(property.SetMethod));
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
                throw new Xunit.Sdk.XunitException(
                    "Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            AssertUniqueObjectKeys(value);
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

    private static void AssertUniqueObjectKeys(JsonElement value)
    {
        string[] names = value.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
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

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual) =>
        AssertJsonEquivalent(expected, actual, "$");

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(
            expected.ValueKind == actual.ValueKind,
            $"JSON kind mismatch at {path}: expected {expected.ValueKind}, actual {actual.ValueKind}.");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] expectedProperties = expected.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                JsonProperty[] actualProperties = actual.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                Assert.True(
                    expectedProperties.Select(property => property.Name)
                        .SequenceEqual(actualProperties.Select(property => property.Name), StringComparer.Ordinal),
                    "JSON property mismatch at " + path + ".");
                for (int index = 0; index < expectedProperties.Length; index++)
                {
                    AssertJsonEquivalent(
                        expectedProperties[index].Value,
                        actualProperties[index].Value,
                        path + "." + expectedProperties[index].Name);
                }

                break;
            case JsonValueKind.Array:
                JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
                JsonElement[] actualItems = actual.EnumerateArray().ToArray();
                Assert.True(
                    expectedItems.Length == actualItems.Length,
                    $"JSON array length mismatch at {path}: expected {expectedItems.Length}, actual {actualItems.Length}."
                    + (expectedItems.All(item => item.ValueKind == JsonValueKind.String)
                        && actualItems.All(item => item.ValueKind == JsonValueKind.String)
                        ? "\nExpected:\n" + string.Join("\n", expectedItems.Select(item => item.GetString()))
                            + "\nActual:\n" + string.Join("\n", actualItems.Select(item => item.GetString()))
                        : string.Empty));
                for (int index = 0; index < expectedItems.Length; index++)
                {
                    AssertJsonEquivalent(expectedItems[index], actualItems[index], path + "[" + index + "]");
                }

                break;
            case JsonValueKind.String:
                Assert.True(
                    StringComparer.Ordinal.Equals(expected.GetString(), actual.GetString()),
                    $"JSON string mismatch at {path}: expected '{expected.GetString()}', actual '{actual.GetString()}'.");
                break;
            case JsonValueKind.Number:
                Assert.True(
                    StringComparer.Ordinal.Equals(expected.GetRawText(), actual.GetRawText()),
                    $"JSON number mismatch at {path}: expected {expected.GetRawText()}, actual {actual.GetRawText()}.");
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.True(
                    expected.GetBoolean() == actual.GetBoolean(),
                    $"JSON boolean mismatch at {path}.");
                break;
            case JsonValueKind.Null:
                break;
            default:
                throw new Xunit.Sdk.XunitException(
                    "Unsupported JSON fact kind '" + expected.ValueKind + "'.");
        }
    }

    private static NativeCall Returned(string caseId, params string[] facts) =>
        new("returned", null, facts.Select(value => caseId + ": " + value).ToArray());

    private static NativeCall RaisedType(string caseId, params string[] facts) =>
        new("raised", "type", facts.Select(value => caseId + ": " + value).ToArray());

    private sealed record EvidenceBinding(string Symbol, string SymbolHash, string AssertionId);

    private sealed record SymbolContract(
        string Symbol,
        string Kind,
        string SignatureHash,
        string BodyHash,
        string Classification,
        string? AdaptationId);

    private sealed record CaseBinding(
        string CaseId,
        string Executor,
        string Symbol,
        string NativeOutcome,
        string? NativeErrorCategory);

    private sealed record NativeCall(string Outcome, string? ErrorCategory, string[] Facts);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string NativeOutcome,
        string? NativeErrorCategory,
        string? Adaptation,
        string[] NativeFacts);
}

#pragma warning restore CA1861
