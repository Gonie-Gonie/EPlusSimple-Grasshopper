using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Profile;
using Dragons.UpstreamTracker;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class ProfileResidualOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/profile-residual-oracle.json";
    private const string OracleSha256 =
        "sha256:76471f9ba5851a0483f18c3a319c947adbbdbd499a9ceaef48b4cd2f8c1bcde4";
    private const string CasesSha256 =
        "sha256:9ccf19079e3776b299a4fd4ab6c069daefb076e28855c77ca2639b18a390ba16";
    private const int OracleByteLength = 24_061;
    private const int ExpectedCaseCount = 15;
    private const string OracleSchema =
        "dragons.invisibledragon.profile-residual-oracle.v1";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Profile.ProfileResidualOracleParityTests.MatchesPinnedPythonProfileResidual";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";

    // Kept as exact three-literal bindings for the compatibility manifest collector.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("Profile", "sha256:3cf55ef99529b6051e2e5bea5c32bbecc5850819101e522fed1008be0599d6ad", "profile-residual-profile-3cf55ef9"),
        new("Profile.__init__", "sha256:19f87b176fd6f00e83c6b55bda01ac7e9bb5d8a0829e8f869f13c20a0388aa25", "profile-residual-init-19f87b17"),
        new("Profile.to_idf_object", "sha256:0b06ee5f7b81782b986777c9f524320ff3f272722a9d0ec4942f5f53ac074893", "profile-residual-idf-0b06ee5f"),
        new("Schedule", "sha256:1a40948f1e3ccbc15dbee4033662c4e80a2a6b4ee559271dd0ca2f59f890095c", "profile-residual-schedule-1a40948f"),
        new("ScheduleOperationError", "sha256:d808ccddebceb72eed1685cd6f236255cc7cc32a21a0b4459237b35af6c7f129", "profile-residual-operation-error-d808ccdd"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("Profile", "class", "sha256:bf35db5abe6e8851938c2d634421f972436bb46ab9abab1dca41465ffcd7e9d4", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "exception", "immutable-profile-value-object"),
        new("Profile.__init__", "function", "sha256:64eb4f95ace84bc62c18887bae8642d24c5a613faea7f0a6403a4d7a4cf9ba52", "sha256:73dd1c37c7a808baa32cbd8e9c811b443e20a07b79a88e38f01ad7387631251f", "exception", "validated-immutable-profile-construction"),
        new("Profile.to_idf_object", "function", "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519", "sha256:77716652f9c58182268dd2afe13cec984edbf15c4d64fffac3a85905bc740713", "equivalent", null),
        new("Schedule", "class", "sha256:24241d2bdfbc529f097a3f866f790e3a45ad8d0ad336d65ced3c9841d8844453", "sha256:00679b8c55fe41d3ab7f7d84e2d3a1e3f0b6ed9c003c318e9ff8ed595932fd34", "exception", "immutable-schedule-value-object"),
        new("ScheduleOperationError", "class", "sha256:302b0beaf8566368e9c978cee1c9dcbdf5e3ad95728e33169278853fa1dc0cab", "sha256:921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a", "exception", "native-schedule-operation-exception-family"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("profile-idf.empty", "profile-idf", "Profile.to_idf_object", "returned", null),
        new("profile-idf.ordered-seven", "profile-idf", "Profile.to_idf_object", "returned", null),
        new("profile-idf.repeated-reference", "profile-idf", "Profile.to_idf_object", "returned", null),
        new("profile-init.defaults", "profile-init", "Profile.__init__", "returned", null),
        new("profile-init.unvalidated-inputs", "profile-init", "Profile.__init__", "raised", "type"),
        new("profile-init.valid-seven-slots", "profile-init", "Profile.__init__", "returned", null),
        new("profile.alias-topology", "profile", "Profile", "returned", null),
        new("profile.identity-equality", "profile", "Profile", "returned", null),
        new("profile.mutable-surface", "profile", "Profile", "returned", null),
        new("schedule-operation-error.args", "schedule-operation-error", "ScheduleOperationError", "returned", null),
        new("schedule-operation-error.catch-family", "schedule-operation-error", "ScheduleOperationError", "returned", null),
        new("schedule-operation-error.inheritance", "schedule-operation-error", "ScheduleOperationError", "returned", null),
        new("schedule.alias-container", "schedule", "Schedule", "returned", null),
        new("schedule.default-topology", "schedule", "Schedule", "returned", null),
        new("schedule.mutable-userlist", "schedule", "Schedule", "returned", null),
    };

    [Fact]
    public void MatchesPinnedPythonProfileResidual()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
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
                NativeCall call = ExecuteCase(
                    binding,
                    item.GetProperty("python").GetProperty("facts"));
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
                TrustedEvidenceRecorder.Record(
                    evidence.AssertionId,
                    EvidenceTestCase,
                    "not_applicable",
                    new
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
                    });
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
        Assert.False(
            Regex.IsMatch(
                root.GetRawText(),
                @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]+(?![0-9A-Za-z])",
                RegexOptions.CultureInvariant));

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal("847b01f68f438f560a986072bcaa7768fbf67897", RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02",
            RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(
            "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445",
            RequiredString(upstream, "source_sha256"));

        JsonElement runtime = root.GetProperty("runtime");
        AssertKeys(
            runtime,
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

        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCaseCount, cases.Length);
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId).ToArray(),
            cases.Select(item => RequiredString(item, "id")).ToArray());
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            ExpectedCases.Select(item => item.CaseId).ToArray());
        Assert.Equal(
            ExpectedCaseCount,
            cases.Select(item => RequiredString(item, "id"))
                .Distinct(StringComparer.Ordinal)
                .Count());

        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
        }

        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            Assert.Equal(3, ExpectedCases.Count(item => item.Symbol == evidence.Symbol));
        }

        string casesHash = CanonicalSha256(root.GetProperty("cases"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, casesHash);
        return cases;
    }

    private static void ValidateSymbols(JsonElement symbolsElement)
    {
        JsonElement[] actual = symbolsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, actual.Length);
        Assert.Equal(ExpectedEvidence.Length, actual.Length);
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => item.AssertionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            ExpectedEvidence,
            item => Assert.StartsWith("profile-residual-", item.AssertionId, StringComparison.Ordinal));

        for (int index = 0; index < actual.Length; index++)
        {
            JsonElement item = actual[index];
            SymbolContract symbol = ExpectedSymbols[index];
            EvidenceBinding evidence = ExpectedEvidence[index];
            Assert.Equal(symbol.Symbol, evidence.Symbol);
            AssertKeys(
                item,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
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
        AssertKeys(
            consumer,
            "adaptations",
            "case_count",
            "case_ids",
            "classifications",
            "float_encoding",
            "runtime_names",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, consumer.GetProperty("case_count").GetInt32());
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId).ToArray(),
            consumer.GetProperty("case_ids").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(
            ExpectedSymbols.Select(item => item.Symbol).ToArray(),
            consumer.GetProperty("target_symbols").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(
            "python-binary64-hex-without-0x-prefix",
            RequiredString(consumer, "float_encoding"));
        Assert.Equal(
            "policy-token-no-raw-address",
            RequiredString(consumer, "runtime_names"));

        JsonElement classifications = consumer.GetProperty("classifications");
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.Classification, RequiredString(classifications, symbol.Symbol));
        }

        SymbolContract[] adaptedSymbols = ExpectedSymbols
            .Where(item => item.AdaptationId is not null)
            .ToArray();
        JsonElement adaptations = consumer.GetProperty("adaptations");
        AssertKeys(adaptations, adaptedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in adaptedSymbols)
        {
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
        }

        Assert.Single(ExpectedSymbols, item => item.Classification == "equivalent");
        Assert.Equal(4, adaptedSymbols.Length);
    }

    private static void ValidateCase(JsonElement item, CaseBinding binding)
    {
        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == binding.Symbol);
        bool adapted = symbol.AdaptationId is not null;
        AssertKeys(
            item,
            adapted
                ? new[] { "executor", "expected_dotnet", "id", "python", "symbol" }
                : new[] { "executor", "id", "python", "symbol" });
        Assert.Equal(binding.CaseId, RequiredString(item, "id"));
        Assert.Equal(binding.Executor, RequiredString(item, "executor"));
        Assert.Equal(binding.Symbol, RequiredString(item, "symbol"));

        if (adapted)
        {
            JsonElement expected = item.GetProperty("expected_dotnet");
            AssertKeys(
                expected,
                binding.NativeOutcome == "raised"
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
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        Assert.Equal(JsonValueKind.Object, facts.ValueKind);
        ValidateFactNode(facts);
        ValidateEquivalentFactKeys(binding, facts);
    }

    private static void ValidateEquivalentFactKeys(CaseBinding binding, JsonElement facts)
    {
        if (binding.CaseId == "profile-idf.empty")
        {
            AssertKeys(
                facts,
                "count",
                "null_slots_omitted",
                "objects",
                "repeated_call_count",
                "results_are_fresh");
        }
        else if (binding.CaseId == "profile-idf.ordered-seven")
        {
            AssertKeys(facts, "count", "objects", "schedule_names", "type_limit_names");
        }
        else if (binding.CaseId == "profile-idf.repeated-reference")
        {
            AssertKeys(
                facts,
                "converted_objects_are_fresh",
                "converted_values_match",
                "count",
                "duplicate_positions_preserved",
                "objects");
        }
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
            Assert.Contains(
                value.ValueKind,
                new[]
                {
                    JsonValueKind.False,
                    JsonValueKind.Null,
                    JsonValueKind.Number,
                    JsonValueKind.String,
                    JsonValueKind.True,
                });
            if (value.ValueKind == JsonValueKind.Number)
            {
                _ = value.GetInt64();
            }

            return;
        }

        AssertUniqueObjectKeys(value);
        if (value.TryGetProperty("policy", out _))
        {
            ValidateNameDescriptor(value);
        }

        if (value.TryGetProperty("kind", out JsonElement kindElement))
        {
            switch (kindElement.GetString())
            {
                case "idf-object":
                    AssertKeys(value, "fields", "kind", "object_type");
                    Assert.Equal("Schedule:Compact", RequiredString(value, "object_type"));
                    Assert.All(
                        value.GetProperty("fields").EnumerateArray(),
                        item => Assert.Equal(JsonValueKind.String, item.ValueKind));
                    break;
                case "profile":
                    ValidateProfileDescriptor(value);
                    break;
                case "schedule":
                    AssertKeys(value, "kind", "length", "name", "schedule_type");
                    Assert.Equal(Schedule.FixedLength, value.GetProperty("length").GetInt32());
                    ValidateNameDescriptor(value.GetProperty("name"));
                    Assert.Contains(
                        RequiredString(value, "schedule_type"),
                        new[] { "fraction", "onoff", "real", "temperature" });
                    break;
                case "foreign":
                    AssertKeys(value, "kind", "type");
                    Assert.Equal("object", RequiredString(value, "type"));
                    break;
                default:
                    throw new Xunit.Sdk.XunitException(
                        $"Unknown Profile residual fact kind '{kindElement.GetString()}'.");
            }
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            ValidateFactNode(property.Value);
        }
    }

    private static void ValidateProfileDescriptor(JsonElement value)
    {
        AssertKeys(value, "kind", "name", "objects", "slots");
        JsonElement name = value.GetProperty("name");
        if (name.ValueKind != JsonValueKind.Null)
        {
            ValidateNameDescriptor(name);
        }

        foreach (JsonElement item in value.GetProperty("objects").EnumerateArray())
        {
            AssertKeys(item, "reference", "value");
            Assert.Matches(@"^(?:foreign|schedule)-[0-9]{2}$", RequiredString(item, "reference"));
        }

        JsonElement slots = value.GetProperty("slots");
        AssertKeys(
            slots,
            "cooling_setpoint",
            "equipment",
            "heating_setpoint",
            "hotwater",
            "hvac_availability",
            "lighting",
            "occupant");
        foreach (JsonProperty slot in slots.EnumerateObject())
        {
            Assert.Contains(slot.Value.ValueKind, new[] { JsonValueKind.Null, JsonValueKind.String });
        }
    }

    private static void ValidateNameDescriptor(JsonElement value)
    {
        string policy = RequiredString(value, "policy");
        if (policy == "runtime-identity-hex")
        {
            AssertKeys(value, "policy");
            return;
        }

        Assert.Equal("literal", policy);
        AssertKeys(value, "policy", "value");
        _ = RequiredString(value, "value");
    }

    private static NativeCall ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Executor switch
        {
            "profile-idf" => ExecuteProfileIdf(binding.CaseId, pythonFacts),
            "profile-init" => ExecuteProfileInit(binding.CaseId),
            "profile" => ExecuteProfile(binding.CaseId),
            "schedule-operation-error" => ExecuteScheduleOperationError(binding.CaseId),
            "schedule" => ExecuteSchedule(binding.CaseId),
            _ => throw new Xunit.Sdk.XunitException(
                $"Unknown Profile residual executor '{binding.Executor}'."),
        };
    }

    private static NativeCall ExecuteProfileIdf(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "profile-idf.empty")
        {
            var profile = new ZoneProfile(new EntityId("PRFL-000001"), "empty");
            IReadOnlyList<IdfObject> left = profile.ToIdfObjects();
            IReadOnlyList<IdfObject> right = profile.ToIdfObjects();
            AssertReadOnly(left);
            AssertReadOnly(right);
            JsonElement actual = JsonSerializer.SerializeToElement(new
            {
                count = left.Count,
                null_slots_omitted = ProfileSlots(profile).All(value => value is null),
                objects = left.Select(EncodeIdfObject).ToArray(),
                repeated_call_count = right.Count,
                results_are_fresh = !ReferenceEquals(left, right),
            });
            AssertJsonEquivalent(pythonFacts, actual);
            return Returned(
                caseId,
                "both native exports were empty and distinct",
                "native read-only shells omitted all null slots");
        }

        if (caseId == "profile-idf.ordered-seven")
        {
            Schedule[] schedules = CreateSevenSchedules();
            ZoneProfile profile = CreateProfile("ordered", schedules);
            IReadOnlyList<IdfObject> objects = profile.ToIdfObjects();
            AssertReadOnly(objects);
            Assert.Equal(schedules, ProfileSlots(profile));
            JsonElement actual = JsonSerializer.SerializeToElement(new
            {
                count = objects.Count,
                objects = objects.Select(EncodeIdfObject).ToArray(),
                schedule_names = objects.Select(value => value.Fields[0].Value).ToArray(),
                type_limit_names = objects.Select(value => value.Fields[1].Value).ToArray(),
            });
            AssertJsonEquivalent(pythonFacts, actual);
            return Returned(
                caseId,
                "native export matched all seven pinned IDF descriptors",
                "native export preserved the upstream slot order exactly");
        }

        Assert.Equal("profile-idf.repeated-reference", caseId);
        Schedule repeated = Schedule.Constant(
            "shared-temperature",
            21,
            ScheduleType.Temperature);
        var repeatedProfile = new ZoneProfile(
            new EntityId("PRFL-000001"),
            "repeated",
            heatingSetpoint: repeated,
            coolingSetpoint: repeated);
        IReadOnlyList<IdfObject> repeatedObjects = repeatedProfile.ToIdfObjects();
        AssertReadOnly(repeatedObjects);
        Assert.Equal(2, repeatedObjects.Count);
        JsonElement repeatedActual = JsonSerializer.SerializeToElement(new
        {
            converted_objects_are_fresh = !ReferenceEquals(repeatedObjects[0], repeatedObjects[1]),
            converted_values_match = IdfObjectsHaveEqualValues(repeatedObjects[0], repeatedObjects[1]),
            count = repeatedObjects.Count,
            duplicate_positions_preserved = ReferenceEquals(
                repeatedProfile.HeatingSetpoint,
                repeatedProfile.CoolingSetpoint),
            objects = repeatedObjects.Select(EncodeIdfObject).ToArray(),
        });
        AssertJsonEquivalent(pythonFacts, repeatedActual);
        return Returned(
            caseId,
            "native export retained both duplicate source positions",
            "native duplicate conversions produced distinct equal-valued IDF objects");
    }

    private static NativeCall ExecuteProfileInit(string caseId)
    {
        if (caseId == "profile-init.defaults")
        {
            var id = new EntityId("PRFL-000001");
            var profile = new ZoneProfile(id, "defaults");
            Assert.Same(id, profile.Id);
            Assert.Equal(id, profile.Id);
            Assert.Equal("defaults", profile.Name);
            Assert.All(ProfileSlots(profile), value => Assert.Null(value));
            Assert.Empty(profile.ToIdfObjects());
            return Returned(
                caseId,
                "native validated constructor retained the required EntityId reference and value",
                "native validated constructor retained the required nonblank name",
                "native optional schedule slots defaulted to null");
        }

        if (caseId == "profile-init.unvalidated-inputs")
        {
            var validId = new EntityId("PRFL-000001");
            ArgumentNullException idError = Assert.Throws<ArgumentNullException>(() =>
                new ZoneProfile(null!, "missing identity"));
            Assert.Equal("id", idError.ParamName);
            ArgumentException nameError = Assert.Throws<ArgumentException>(() =>
                new ZoneProfile(validId, "   "));
            Assert.Equal("name", nameError.ParamName);

            ConstructorInfo constructor = Assert.Single(typeof(ZoneProfile).GetConstructors());
            object marker = new();
            object?[] arguments =
            {
                validId,
                "invalid foreign slots",
                marker,
                marker,
                marker,
                marker,
                marker,
                marker,
                marker,
            };
            Assert.Throws<ArgumentException>(() =>
            {
                _ = constructor.Invoke(arguments);
            });

            Schedule wrongReal = Schedule.Constant("wrong real domain", 1, ScheduleType.Real);
            Schedule wrongFraction = Schedule.Constant(
                "wrong fraction domain",
                0.5,
                ScheduleType.Fraction);
            AssertProfileSlotDomainRejected(
                "heatingSetpoint",
                wrongReal,
                () => new ZoneProfile(validId, "invalid heating", heatingSetpoint: wrongReal));
            AssertProfileSlotDomainRejected(
                "coolingSetpoint",
                wrongReal,
                () => new ZoneProfile(validId, "invalid cooling", coolingSetpoint: wrongReal));
            AssertProfileSlotDomainRejected(
                "hvacAvailability",
                wrongReal,
                () => new ZoneProfile(validId, "invalid hvac", hvacAvailability: wrongReal));
            AssertProfileSlotDomainRejected(
                "occupant",
                wrongFraction,
                () => new ZoneProfile(validId, "invalid occupant", occupant: wrongFraction));
            AssertProfileSlotDomainRejected(
                "lighting",
                wrongReal,
                () => new ZoneProfile(validId, "invalid lighting", lighting: wrongReal));
            AssertProfileSlotDomainRejected(
                "equipment",
                wrongFraction,
                () => new ZoneProfile(validId, "invalid equipment", equipment: wrongFraction));
            AssertProfileSlotDomainRejected(
                "hotWater",
                wrongFraction,
                () => new ZoneProfile(validId, "invalid hot water", hotWater: wrongFraction));
            return RaisedType(
                caseId,
                "native constructor rejected a null EntityId with the id parameter name",
                "native constructor rejected a blank name with the name parameter name",
                "native CLR signature rejected foreign slot objects",
                "native constructor atomically rejected all seven wrong schedule domains with exact parameter names",
                "all rejected source schedules remained unchanged and usable");
        }

        Assert.Equal("profile-init.valid-seven-slots", caseId);
        Schedule[] schedules = CreateSevenSchedules();
        ZoneProfile valid = CreateProfile("  valid profile  ", schedules);
        Assert.Equal("valid profile", valid.Name);
        Assert.Equal(schedules, ProfileSlots(valid));
        for (int index = 0; index < schedules.Length; index++)
        {
            Assert.Same(schedules[index], ProfileSlots(valid)[index]);
        }

        Assert.Equal(7, valid.ToIdfObjects().Count);
        return Returned(
            caseId,
            "native constructor preserved all seven valid schedule references",
            "native constructor normalized required surrounding name whitespace");
    }

    private static NativeCall ExecuteProfile(string caseId)
    {
        if (caseId == "profile.alias-topology")
        {
            Schedule temperature = Schedule.Constant(
                "temperature-shared",
                21,
                ScheduleType.Temperature);
            Schedule hvac = Schedule.Constant("hvac", 1, ScheduleType.OnOff);
            Schedule real = Schedule.Constant("real-shared", 0.5, ScheduleType.Real);
            Schedule lighting = Schedule.Constant("lighting", 0.5, ScheduleType.Fraction);
            var profile = new ZoneProfile(
                new EntityId("PRFL-000001"),
                "alias",
                temperature,
                temperature,
                hvac,
                real,
                lighting,
                real,
                real);
            Assert.Same(profile.HeatingSetpoint, profile.CoolingSetpoint);
            Assert.Same(profile.Occupant, profile.Equipment);
            Assert.Same(profile.Equipment, profile.HotWater);
            Assert.Same(hvac, profile.HvacAvailability);
            Assert.Same(lighting, profile.Lighting);
            Assert.Equal(7, profile.ToIdfObjects().Count);
            return Returned(
                caseId,
                "native immutable value retained the pinned slot alias topology",
                "native IDF export preserved every aliased slot position");
        }

        if (caseId == "profile.identity-equality")
        {
            var leftId = new EntityId("PRFL-000001");
            var rightId = new EntityId("PRFL-000001");
            var left = new ZoneProfile(leftId, "value");
            var right = new ZoneProfile(rightId, "value");
            Assert.NotSame(leftId, rightId);
            Assert.Equal(leftId, rightId);
            Assert.NotSame(left, right);
            Assert.Equal(left, right);
            Assert.True(left.Equals(left));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());

            Schedule low = Schedule.Constant("heating", 20, ScheduleType.Temperature);
            Schedule high = Schedule.Constant("heating", 21, ScheduleType.Temperature);
            Assert.NotSame(low, high);
            Assert.NotEqual(low, high);
            var lowProfile = new ZoneProfile(
                new EntityId("PRFL-000002"),
                "scheduled value",
                heatingSetpoint: low);
            var highProfile = new ZoneProfile(
                new EntityId("PRFL-000002"),
                "scheduled value",
                heatingSetpoint: high);
            Assert.NotEqual(lowProfile, highProfile);
            return Returned(
                caseId,
                "distinct empty native Profiles with distinct equal EntityIds used record value equality",
                "equal native Profile values produced equal hashes",
                "native Profiles with different schedule values compared unequal");
        }

        Assert.Equal("profile.mutable-surface", caseId);
        var immutable = new ZoneProfile(new EntityId("PRFL-000001"), "immutable");
        PropertyInfo[] properties = typeof(ZoneProfile).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.All(properties, property => Assert.False(property.CanWrite));
        Assert.Empty(typeof(ZoneProfile).GetFields(BindingFlags.Instance | BindingFlags.Public));
        Assert.True(typeof(ZoneProfile).IsSealed);
        Assert.False(typeof(System.Dynamic.IDynamicMetaObjectProvider).IsAssignableFrom(typeof(ZoneProfile)));
        Assert.Equal("immutable", immutable.Name);
        return Returned(
            caseId,
            "native Profile exposed no writable public properties or fields",
            "native Profile was sealed and rejected dynamic attribute topology");
    }

    private static NativeCall ExecuteScheduleOperationError(string caseId)
    {
        if (caseId == "schedule-operation-error.args")
        {
            ConstructorInfo constructor = Assert.Single(
                typeof(ScheduleOperationException).GetConstructors());
            ParameterInfo parameter = Assert.Single(constructor.GetParameters());
            Assert.Equal(typeof(string), parameter.ParameterType);
            Assert.Equal(string.Empty, new ScheduleOperationException(string.Empty).Message);
            Assert.Equal(
                "operation failed",
                new ScheduleOperationException("operation failed").Message);
            Assert.Equal(
                "('left', 2)",
                new ScheduleOperationException("('left', 2)").Message);
            return Returned(
                caseId,
                "native exception provided one explicit message constructor",
                "native exception preserved empty single and normalized multi-argument messages");
        }

        if (caseId == "schedule-operation-error.catch-family")
        {
            DaySchedule on = DaySchedule.Constant("on", 1, ScheduleType.OnOff);
            ScheduleOperationException error = Assert.Throws<ScheduleOperationException>(() => _ = on + on);
            Assert.IsAssignableFrom<InvalidOperationException>(error);
            Assert.IsAssignableFrom<Exception>(error);
            Exception exception = error;
            Assert.False(exception is ArgumentException);
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
            Assert.Equal(
                "Addition and subtraction are not defined for OnOff schedules.",
                error.Message);
            Assert.Equal("schedule-operation", NormalizeException(error));
            return Returned(
                caseId,
                "native schedule operation raised the dedicated exception type",
                "native exception normalized to the schedule-operation category",
                "native operation propagated the exact relevant nonblank failure message");
        }

        Assert.Equal("schedule-operation-error.inheritance", caseId);
        Assert.Equal(typeof(InvalidOperationException), typeof(ScheduleOperationException).BaseType);
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(ScheduleOperationException)));
        Assert.False(typeof(ArgumentException).IsAssignableFrom(typeof(ScheduleOperationException)));
        Type[] hierarchy = InheritanceChain(typeof(ScheduleOperationException));
        Assert.Equal(
            new[]
            {
                typeof(ScheduleOperationException),
                typeof(InvalidOperationException),
                typeof(SystemException),
                typeof(Exception),
                typeof(object),
            },
            hierarchy);
        return Returned(
            caseId,
            "native exception inherited from InvalidOperationException",
            "native exception hierarchy was closed and explicitly verified");
    }

    private static NativeCall ExecuteSchedule(string caseId)
    {
        if (caseId == "schedule.alias-container")
        {
            RuleSet first = RuleSet.Constant("first-sentinel", 1, ScheduleType.Real);
            RuleSet shared = RuleSet.Constant("shared", 2, ScheduleType.Real);
            RuleSet middle = RuleSet.Constant("middle-sentinel", 3, ScheduleType.Real);
            RuleSet last = RuleSet.Constant("last-sentinel", 4, ScheduleType.Real);
            var source = Enumerable.Repeat(shared, Schedule.FixedLength).ToList();
            source[0] = first;
            source[Schedule.FixedLength / 2] = middle;
            source[Schedule.FixedLength - 1] = last;
            RuleSet[] expected = source.ToArray();
            var schedule = new Schedule("alias", source, ScheduleType.Real);
            Assert.NotSame(source, schedule.RuleSets);
            Assert.Equal(
                expected.Select(value => value.Name),
                schedule.RuleSets.Select(value => value.Name));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.Same(expected[index], schedule[index]);
            }

            Assert.Same(shared, schedule[1]);
            Assert.Same(schedule[1], schedule[2]);
            Assert.Same(first, schedule[0]);
            Assert.Same(middle, schedule[Schedule.FixedLength / 2]);
            Assert.Same(last, schedule[Schedule.FixedLength - 1]);

            RuleSet replacement = RuleSet.Constant("replacement", 2, ScheduleType.Real);
            source[0] = replacement;
            source[Schedule.FixedLength / 2] = replacement;
            source[Schedule.FixedLength - 1] = replacement;
            source.Add(replacement);
            Assert.Equal(Schedule.FixedLength + 1, source.Count);
            Assert.Equal(Schedule.FixedLength, schedule.Count);
            Assert.Same(first, schedule[0]);
            Assert.Same(middle, schedule[Schedule.FixedLength / 2]);
            Assert.Same(last, schedule[schedule.Count - 1]);
            AssertReadOnly(schedule.RuleSets);
            return Returned(
                caseId,
                "native Schedule copied all 365 source references in exact sentinel order",
                "native Schedule retained repeated RuleSet alias topology",
                "native Schedule length and ordered contents ignored later source mutations");
        }

        if (caseId == "schedule.default-topology")
        {
            var schedule = new Schedule(null);
            Assert.Equal("anonymous", schedule.Name);
            Assert.Equal(Schedule.FixedLength, schedule.Count);
            Assert.Equal(ScheduleType.Real, schedule.Type);
            Assert.Equal(
                Schedule.FixedLength,
                schedule.RuleSets.Distinct(ReferenceEqualityComparer.Instance).Count());
            DaySchedule[] days = schedule.RuleSets
                .SelectMany(value => new[] { value.Weekdays, value.Weekends })
                .ToArray();
            Assert.Equal(
                Schedule.FixedLength * 2,
                days.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.All(schedule.RuleSets, value => Assert.Equal(ScheduleType.Real, value.Type));
            Assert.All(days, value => Assert.All(value.Values, item => Assert.Equal(0, item)));
            Assert.All(schedule.RuleSets, value => Assert.NotSame(value.Weekdays, value.Weekends));
            Assert.Equal("anonymous:default:001", schedule[0].Name);
            Assert.Equal("anonymous:default:365", schedule[364].Name);
            Assert.Equal("anonymous:default:001:day", schedule[0].Weekdays.Name);
            return Returned(
                caseId,
                "native default Schedule built 365 distinct deterministic rule sets",
                "native default Schedule built 730 distinct zero-valued Real day schedules");
        }

        Assert.Equal("schedule.mutable-userlist", caseId);
        Schedule immutable = Schedule.Constant("immutable", 1, ScheduleType.Real);
        PropertyInfo indexer = Assert.Single(
            typeof(Schedule).GetProperties(),
            property => property.GetIndexParameters().Length == 1
                && property.GetIndexParameters()[0].ParameterType == typeof(int));
        Assert.False(indexer.CanWrite);
        Assert.Null(typeof(Schedule).GetProperty("Data", BindingFlags.Instance | BindingFlags.Public));
        string[] forbiddenMutationMethods =
        {
            "Append",
            "Clear",
            "Extend",
            "Insert",
            "Pop",
            "Remove",
            "RemoveAt",
            "Reverse",
            "Sort",
        };
        foreach (string methodName in forbiddenMutationMethods)
        {
            Assert.DoesNotContain(
                typeof(Schedule).GetMethods(BindingFlags.Instance | BindingFlags.Public),
                method => method.Name == methodName);
        }

        MethodInfo[] functionalAddMethods = typeof(Schedule)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == "Add")
            .ToArray();
        Assert.NotEmpty(functionalAddMethods);
        Assert.All(functionalAddMethods, method => Assert.Equal(typeof(Schedule), method.ReturnType));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = immutable[-1]);
        AssertReadOnly(immutable.RuleSets);
        Schedule added = immutable.Add(1, "functional add");
        Assert.NotSame(immutable, added);
        Assert.Equal(1, immutable[0].Weekdays[0]);
        Assert.Equal(2, added[0].Weekdays[0]);
        Assert.Equal(Schedule.FixedLength, immutable.Count);
        return Returned(
            caseId,
            "native Schedule exposed no public mutable data setter or sequence mutation method",
            "every native IList mutation route was blocked by read-only storage",
            "native Add overloads returned fresh Schedule values without changing the source",
            "native Schedule rejected negative replacement and retained fixed length");
    }

    private static Schedule[] CreateSevenSchedules()
    {
        return new[]
        {
            Schedule.Constant("heating", 20, ScheduleType.Temperature),
            Schedule.Constant("cooling", 25, ScheduleType.Temperature),
            Schedule.Constant("hvac", 1, ScheduleType.OnOff),
            Schedule.Constant("occupant", 0.1, ScheduleType.Real),
            Schedule.Constant("lighting", 0.2, ScheduleType.Fraction),
            Schedule.Constant("equipment", 3, ScheduleType.Real),
            Schedule.Constant("hotwater", 4, ScheduleType.Real),
        };
    }

    private static void AssertProfileSlotDomainRejected(
        string parameterName,
        Schedule source,
        Func<ZoneProfile> construct)
    {
        string originalName = source.Name;
        ScheduleType originalType = source.Type;
        int originalCount = source.Count;
        RuleSet originalFirst = source[0];
        ZoneProfile? result = null;
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
        {
            result = construct();
        });
        Assert.Equal(parameterName, error.ParamName);
        Assert.Null(result);
        Assert.Equal(originalName, source.Name);
        Assert.Equal(originalType, source.Type);
        Assert.Equal(originalCount, source.Count);
        Assert.Same(originalFirst, source[0]);
        Assert.Equal("Schedule:Compact", source.ToIdfObject().ObjectType);
    }

    private static ZoneProfile CreateProfile(string name, IReadOnlyList<Schedule> schedules)
    {
        Assert.Equal(7, schedules.Count);
        return new ZoneProfile(
            new EntityId("PRFL-000001"),
            name,
            schedules[0],
            schedules[1],
            schedules[2],
            schedules[3],
            schedules[4],
            schedules[5],
            schedules[6]);
    }

    private static Schedule?[] ProfileSlots(ZoneProfile profile)
    {
        return new[]
        {
            profile.HeatingSetpoint,
            profile.CoolingSetpoint,
            profile.HvacAvailability,
            profile.Occupant,
            profile.Lighting,
            profile.Equipment,
            profile.HotWater,
        };
    }

    private static object EncodeIdfObject(IdfObject value)
    {
        return new
        {
            fields = value.Fields.Select(field => field.Value).ToArray(),
            kind = "idf-object",
            object_type = value.ObjectType,
        };
    }

    private static bool IdfObjectsHaveEqualValues(IdfObject left, IdfObject right)
    {
        return left.ObjectType == right.ObjectType
            && left.Fields.Select(field => field.Value)
                .SequenceEqual(right.Fields.Select(field => field.Value), StringComparer.Ordinal);
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> values)
    {
        IList<T> mutableView = Assert.IsAssignableFrom<IList<T>>(values);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        if (values.Count > 0)
        {
            Assert.Throws<NotSupportedException>(() => mutableView[0] = values[0]);
            Assert.Throws<NotSupportedException>(() => mutableView.Add(values[0]));
            Assert.Throws<NotSupportedException>(() => mutableView.Insert(0, values[0]));
            Assert.Throws<NotSupportedException>(() => mutableView.Remove(values[0]));
            Assert.Throws<NotSupportedException>(() => mutableView.RemoveAt(0));
        }
    }

    private static string NormalizeException(Exception exception)
    {
        return exception is ScheduleOperationException
            ? "schedule-operation"
            : throw new Xunit.Sdk.XunitException(
                $"Unexpected native exception type '{exception.GetType().FullName}'.");
    }

    private static Type[] InheritanceChain(Type type)
    {
        var result = new List<Type>();
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            result.Add(current);
        }

        return result.ToArray();
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

        return $"sha256:{Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()}";
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
                    $"Unsupported canonical JSON kind '{value.ValueKind}'.");
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
        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal).ToArray(), actual);
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
            $"Could not locate repository file '{relativePath}'.");
    }

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] expectedProperties = expected.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                JsonProperty[] actualProperties = actual.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(
                    expectedProperties.Select(property => property.Name),
                    actualProperties.Select(property => property.Name));
                for (int index = 0; index < expectedProperties.Length; index++)
                {
                    AssertJsonEquivalent(expectedProperties[index].Value, actualProperties[index].Value);
                }

                break;
            case JsonValueKind.Array:
                JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
                JsonElement[] actualItems = actual.EnumerateArray().ToArray();
                Assert.Equal(expectedItems.Length, actualItems.Length);
                for (int index = 0; index < expectedItems.Length; index++)
                {
                    AssertJsonEquivalent(expectedItems[index], actualItems[index]);
                }

                break;
            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.GetString());
                break;
            case JsonValueKind.Number:
                Assert.Equal(expected.GetRawText(), actual.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                break;
            case JsonValueKind.Null:
                break;
            default:
                throw new Xunit.Sdk.XunitException(
                    $"Unsupported JSON fact kind '{expected.ValueKind}'.");
        }
    }

    private static NativeCall Returned(string caseId, params string[] facts) =>
        new("returned", null, facts.Select(value => $"{caseId}: {value}").ToArray());

    private static NativeCall RaisedType(string caseId, params string[] facts) =>
        new("raised", "type", facts.Select(value => $"{caseId}: {value}").ToArray());

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

    private sealed record NativeCall(
        string Outcome,
        string? ErrorCategory,
        string[] Facts);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string NativeOutcome,
        string? NativeErrorCategory,
        string? Adaptation,
        string[] NativeFacts);
}
