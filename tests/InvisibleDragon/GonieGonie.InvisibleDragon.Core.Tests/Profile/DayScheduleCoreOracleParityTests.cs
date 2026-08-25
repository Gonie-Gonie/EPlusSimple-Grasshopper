using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Profile;

public sealed class DayScheduleCoreOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/day-schedule-core-oracle.json";
    private const string OracleSha256 =
        "sha256:202c889b0c38d5571fd6c55aa1eac4cc94344f177df09d5654086860bc714239";
    private const string CasesSha256 =
        "sha256:928a70bc70c83fc9fd7969253c2c5cc5ab5ffb8fca63726f197c007405d95c45";
    private const int OracleByteLength = 166_372;
    private const int ExpectedCaseCount = 42;
    private const string OracleSchema =
        "goniegonie.invisibledragon.day-schedule-core-oracle.v1";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Profile.DayScheduleCoreOracleParityTests.MatchesPinnedPythonDayScheduleCore";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";

    // Kept as an exact three-literal binding so the compatibility manifest
    // collector can bind every receipt without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("DaySchedule", "sha256:3d09af6328fa8beb98a435f86468dbe5db1f906ae8eaef5db6f60b2e75d3ebad", "profile-dayschedule-core-value-object-3d09af63"),
        new("DaySchedule.__deepcopy__", "sha256:94716a2a9f9896956ef0fe11d0a43630ef22e3490260cdffd5b5eb34aed20061", "profile-dayschedule-core-deepcopy-94716a2a"),
        new("DaySchedule.__init__", "sha256:64dc644b6b17c50070088875126038fbd0f7fa37c6b102efc1a9fdce7c238b29", "profile-dayschedule-core-init-64dc644b"),
        new("DaySchedule.__setitem__", "sha256:f7d024f8afb2246d678ae93f48ec2dd247cee4a69f050dab9824e41d1043a703", "profile-dayschedule-core-setitem-f7d024f8"),
        new("DaySchedule.astype", "sha256:b9602775c81765b2c8833aa2e420e788fc0e15e8ecee1cc26cea6959c1896791", "profile-dayschedule-core-astype-b9602775"),
        new("DaySchedule.clip", "sha256:d8d8325402e25fc7490c3ab97a5e5406a6aa81fd4529c7e17e181a5fc79eb5e7", "profile-dayschedule-core-clip-d8d83254"),
        new("DaySchedule.compactize", "sha256:b8cb0746fc938250dd097a746f74f769d1e34cf83dbcdfdf3f83eef958581542", "profile-dayschedule-core-compactize-b8cb0746"),
        new("DaySchedule.from_compact", "sha256:7584e03e29fb0ebfc974fd95edd605fc2fc5ce7d1266b6c936553fd9131d2fe9", "profile-dayschedule-core-from-compact-7584e03e"),
        new("DaySchedule.from_constant", "sha256:71ce65d43f4c5ccf2fe5be57f6f7bd011138f11243e348f9b356bed85dfd1848", "profile-dayschedule-core-from-constant-71ce65d4"),
        new("DaySchedule.from_windows", "sha256:5a0b430f3f9b0ba4df876567989aff0675970b3993848114e381b0c69cd6b28f", "profile-dayschedule-core-from-windows-5a0b430f"),
        new("DaySchedule.summary", "sha256:0dc726d3cf145593aa0305902687e751b9ac6571450ca1d28acff2bf97aa5d85", "profile-dayschedule-core-summary-0dc726d3"),
        new("DaySchedule.time_tuple", "sha256:a7a04f776f37d8676cd20b07bc190cc28185f207663c2a183671f9bc016d6bbd", "profile-dayschedule-core-time-tuple-a7a04f77"),
        new("DaySchedule.to_idf_compactexpr", "sha256:e33e015cdd6a0057839061ecfdc1103b6c88abda0e0bc896c5c813c98113dbed", "profile-dayschedule-core-idf-compactexpr-e33e015c"),
        new("DaySchedule.type", "sha256:6c3809ae6a4918dfe994dbec71bfe025272a6ffdd18300b3146888cade19ced3", "profile-dayschedule-core-type-6c3809ae"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("DaySchedule", "class", "sha256:d6cf14abf38a8ebd5ea19cebe3e341da462019d6586a564157ec8d03a785103a", "sha256:31b0c5a91bb166328c75453a1fafba578bac89b29d9f6a39cde7930e29ac0d67", "exception", "immutable-day-schedule-value-object"),
        new("DaySchedule.__deepcopy__", "function", "sha256:520ee536d924ac7323d561d9d85957e67316c14aea8bd80a5664a796409a796f", "sha256:a921ffa52d0bb16ae64dab3b269d78b01d1e8ae605edcfe953ebf1944e03f2be", "exception", "native-day-schedule-deepcopy-memo"),
        new("DaySchedule.__init__", "function", "sha256:792feef4767b51cf22c073f4f9c482d3a7b44ebf852cd1969f696d35523144f7", "sha256:6b3800355e995b6f19cd43c8640e50f4b7ff8a91107f0b10bcf802ff5d9b3465", "exception", "immutable-deterministic-day-schedule-construction"),
        new("DaySchedule.__setitem__", "function", "sha256:c683d89aecb53af53bb23c6719e3ada99d5224b3e92694a66e6416c310dc679e", "sha256:65d793b466ed70ebe848ddb0c684e008c7ee1a985d85834255b21ea8d59126cd", "exception", "immutable-day-schedule-item-update"),
        new("DaySchedule.astype", "function", "sha256:a14d975027c0b9836d9155272e817515f8f8508481a2756286850554ff87fa08", "sha256:be2d761d2ccf19329fed5acdd335b1551c36b782d88300f6910b98888e2d3943", "exception", "immutable-day-schedule-astype"),
        new("DaySchedule.clip", "function", "sha256:3b5d09dbe8f1e838b60a4b2bb4841d43af33e72368014e36c75638e0220ff3ac", "sha256:4e51b37afdf9bfa9733425a72d587a294ef8afea623a0ff639fb8f2c6cd98df6", "exception", "immutable-day-schedule-clip"),
        new("DaySchedule.compactize", "function", "sha256:ba6052b432aaac041119c4882326d35be34f398de4c71532f310e32a58fabd8a", "sha256:6b89b9584885221eb437f7210bd216dd28c9d9244e510d7f6d236cb00cfbd248", "equivalent", null),
        new("DaySchedule.from_compact", "function", "sha256:26770cfe77ae6d18ebe45f2a4125a5456ea818d30ceebddeb89781318b230b1f", "sha256:9d8896249a21c2babaa6fbe82bc943701a89387a3b3e92d4964f34447b5b753d", "exception", "validated-deterministic-day-schedule-from-compact"),
        new("DaySchedule.from_constant", "function", "sha256:c8e44d72e356a6d7dfa40f8feb731743d4400d69fc5e9bc9e2e64517e5ccb2b8", "sha256:99133543b3c1493261df5ef066ea5884830e2fdbe94f8e575731eec0fde46fea", "exception", "deterministic-finite-day-schedule-from-constant"),
        new("DaySchedule.from_windows", "function", "sha256:d34d2b5cf299c9f0cf4a5d99215a0b0356a4e95a25b7de38f79c6229ffa05b7e", "sha256:dc83d45aeb2173cfb54288493ecfb2f4d2584529d4899d9fc4c7d2431fd08e39", "exception", "validated-deterministic-day-schedule-from-windows"),
        new("DaySchedule.summary", "function", "sha256:e2ba3e6fd17aef4152bcf0792ba941a91b32572327b6f74511fb0350a2206f5b", "sha256:7b6247de77b34b713afdd8a50cdb37a1b2187341732805d5e299c92e66d452f0", "equivalent", null),
        new("DaySchedule.time_tuple", "function", "sha256:112810143170ab80331a6cd5c2b63ea1605c00874963434d2ecf47e86faf93aa", "sha256:b844b6f9d49a5c83ed97ea0f829f7dfd20f4c1ee51605ddb1bde9b0b8c94ce0a", "equivalent", null),
        new("DaySchedule.to_idf_compactexpr", "function", "sha256:636cf3ce72c5b8fab425c494f6300a642ea6dcc1bbec8f0653a7746285088cb8", "sha256:fe44a1d2cdd71efad93b73d0b815d9ca70cc5d59e16e6d228e30f504d2a93c7c", "equivalent", null),
        new("DaySchedule.type", "function", "sha256:906bbe1fecebe27cb285db65c19d7f68b6771efdfa9aebb2662dd0e2e959d5b5", "sha256:588af62bcaf3f3c7a14cff2698009eea67025ef8f0c0a098d9015d7a9258fec9", "exception", "immutable-validated-day-schedule-type"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("astype.inplace", "astype", "DaySchedule.astype", "returned", null),
        new("astype.invalid-atomic", "astype", "DaySchedule.astype", "raised", "domain"),
        new("astype.outplace-string", "astype", "DaySchedule.astype", "returned", null),
        new("class.mutable-data", "class", "DaySchedule", "returned", null),
        new("class.sequence", "class", "DaySchedule", "returned", null),
        new("class.source-isolation", "class", "DaySchedule", "returned", null),
        new("clip.bounds-empty-name", "clip", "DaySchedule.clip", "returned", null),
        new("clip.reversed", "clip", "DaySchedule.clip", "raised", "domain"),
        new("clip.signed-zero", "clip", "DaySchedule.clip", "returned", null),
        new("compactize.alternating", "compactize", "DaySchedule.compactize", "returned", null),
        new("compactize.constant", "compactize", "DaySchedule.compactize", "returned", null),
        new("compactize.signed-zero", "compactize", "DaySchedule.compactize", "returned", null),
        new("deepcopy.memo-hit", "deepcopy", "DaySchedule.__deepcopy__", "returned", null),
        new("deepcopy.normal", "deepcopy", "DaySchedule.__deepcopy__", "returned", null),
        new("deepcopy.repeated", "deepcopy", "DaySchedule.__deepcopy__", "returned", null),
        new("from-compact.invalid-end", "from-compact", "DaySchedule.from_compact", "raised", "domain"),
        new("from-compact.off-grid", "from-compact", "DaySchedule.from_compact", "raised", "domain"),
        new("from-compact.valid", "from-compact", "DaySchedule.from_compact", "returned", null),
        new("from-constant.anonymous-real", "from-constant", "DaySchedule.from_constant", "returned", null),
        new("from-constant.bool-onoff", "from-constant", "DaySchedule.from_constant", "returned", null),
        new("from-constant.nonfinite", "from-constant", "DaySchedule.from_constant", "raised", "domain"),
        new("from-windows.first-overlap", "from-windows", "DaySchedule.from_windows", "returned", null),
        new("from-windows.out-of-day", "from-windows", "DaySchedule.from_windows", "raised", "domain"),
        new("from-windows.reversed", "from-windows", "DaySchedule.from_windows", "raised", "domain"),
        new("init.default", "init", "DaySchedule.__init__", "returned", null),
        new("init.nonfinite-real", "init", "DaySchedule.__init__", "raised", "domain"),
        new("init.text-preservation", "init", "DaySchedule.__init__", "returned", null),
        new("setitem.invalid-atomic", "setitem", "DaySchedule.__setitem__", "raised", "domain"),
        new("setitem.negative", "setitem", "DaySchedule.__setitem__", "raised", "range"),
        new("setitem.positive", "setitem", "DaySchedule.__setitem__", "returned", null),
        new("summary.negative-limit", "summary", "DaySchedule.summary", "returned", null),
        new("summary.repr-name", "summary", "DaySchedule.summary", "returned", null),
        new("summary.rich", "summary", "DaySchedule.summary", "returned", null),
        new("time-tuple.fresh", "time-tuple", "DaySchedule.time_tuple", "returned", null),
        new("time-tuple.grid", "time-tuple", "DaySchedule.time_tuple", "returned", null),
        new("time-tuple.rollover", "time-tuple", "DaySchedule.time_tuple", "returned", null),
        new("to-idf.onoff", "to-idf", "DaySchedule.to_idf_compactexpr", "returned", null),
        new("to-idf.real", "to-idf", "DaySchedule.to_idf_compactexpr", "returned", null),
        new("to-idf.signed-zero", "to-idf", "DaySchedule.to_idf_compactexpr", "returned", null),
        new("type.getters", "type", "DaySchedule.type", "returned", null),
        new("type.invalid-token", "type", "DaySchedule.type", "raised", "type"),
        new("type.stale-string-setter", "type", "DaySchedule.type", "raised", "domain"),
    };

    [Fact]
    public void MatchesPinnedPythonDayScheduleCore()
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
                @"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])",
                RegexOptions.CultureInvariant));

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
            ExpectedCaseCount,
            cases.Select(item => RequiredString(item, "id"))
                .Distinct(StringComparer.Ordinal)
                .Count());

        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
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

        Assert.Equal(4, ExpectedSymbols.Count(item => item.Classification == "equivalent"));
        Assert.Equal(10, adaptedSymbols.Length);
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
                Assert.Equal(
                    binding.NativeErrorCategory,
                    RequiredString(expected, "error_category"));
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
        string pythonOutcome = RequiredString(python, "outcome");
        if (pythonOutcome == "returned")
        {
            AssertKeys(python, "facts", "outcome");
        }
        else
        {
            Assert.Equal("raised", pythonOutcome);
            AssertKeys(
                python,
                "error_category",
                "exception_type",
                "facts",
                "message",
                "outcome");
            Assert.Contains(
                RequiredString(python, "error_category"),
                new[] { "domain", "range", "type" });
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(python, "exception_type")));
            _ = RequiredString(python, "message");
        }

        JsonElement facts = python.GetProperty("facts");
        Assert.Equal(JsonValueKind.Object, facts.ValueKind);
        ValidateFactNode(facts);
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
        if (value.TryGetProperty("kind", out JsonElement kindElement))
        {
            string kind = kindElement.GetString()!;
            if (kind == "binary64")
            {
                AssertKeys(value, "hex_without_prefix", "kind");
                Assert.Matches(
                    @"^-?(?:nan|inf|0\.0p\+0|0\.[0-9a-f]{13}p-1022|1\.[0-9a-f]{13}p[+-][0-9]+)$",
                    RequiredString(value, "hex_without_prefix"));
            }
            else if (kind == "schedule")
            {
                AssertKeys(value, "kind", "name", "schedule_type", "unit", "values");
                Assert.Contains(
                    RequiredString(value, "schedule_type"),
                    new[] { "fraction", "onoff", "real", "temperature" });
                Assert.Contains(
                    value.GetProperty("unit").ValueKind,
                    new[] { JsonValueKind.Null, JsonValueKind.String });
                ValidateNameDescriptor(value.GetProperty("name"));
                ValidateValuesDescriptor(value.GetProperty("values"));
            }
            else
            {
                throw new Xunit.Sdk.XunitException(
                    $"Unknown DaySchedule core fact kind '{kind}'.");
            }
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            ValidateFactNode(property.Value);
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
        Assert.False(
            Regex.IsMatch(
                RequiredString(value, "value"),
                @"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])",
                RegexOptions.CultureInvariant));
    }

    private static void ValidateValuesDescriptor(JsonElement value)
    {
        string encoding = RequiredString(value, "encoding");
        Assert.Equal(DaySchedule.FixedLength, value.GetProperty("length").GetInt32());
        if (encoding == "repeat")
        {
            AssertKeys(value, "encoding", "length", "pattern");
            JsonElement pattern = value.GetProperty("pattern");
            Assert.InRange(pattern.GetArrayLength(), 1, DaySchedule.FixedLength);
            Assert.Equal(0, DaySchedule.FixedLength % pattern.GetArrayLength());
            return;
        }

        Assert.Equal("full", encoding);
        AssertKeys(value, "encoding", "items", "length");
        Assert.Equal(DaySchedule.FixedLength, value.GetProperty("items").GetArrayLength());
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

    private static NativeCall ExecuteCase(
        CaseBinding binding,
        JsonElement pythonFacts)
    {
        NativeCall call = binding.Executor switch
        {
            "astype" => ExecuteAsType(binding.CaseId),
            "class" => ExecuteClass(binding.CaseId),
            "clip" => ExecuteClip(binding.CaseId),
            "compactize" => ExecuteCompactize(binding.CaseId, pythonFacts),
            "deepcopy" => ExecuteDeepCopy(binding.CaseId),
            "from-compact" => ExecuteFromCompact(binding.CaseId),
            "from-constant" => ExecuteFromConstant(binding.CaseId),
            "from-windows" => ExecuteFromWindows(binding.CaseId),
            "init" => ExecuteInit(binding.CaseId),
            "setitem" => ExecuteSetItem(binding.CaseId),
            "summary" => ExecuteSummary(binding.CaseId, pythonFacts),
            "time-tuple" => ExecuteTimeTuple(binding.CaseId, pythonFacts),
            "to-idf" => ExecuteToIdf(binding.CaseId, pythonFacts),
            "type" => ExecuteType(binding.CaseId),
            _ => throw new Xunit.Sdk.XunitException(
                $"No native DaySchedule core executor exists for '{binding.CaseId}'."),
        };

        Assert.Equal(binding.NativeOutcome, call.Outcome);
        Assert.Equal(binding.NativeErrorCategory, call.ErrorCategory);
        Assert.NotEmpty(call.Facts);
        Assert.All(call.Facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        Assert.Equal(call.Facts.Length, call.Facts.Distinct(StringComparer.Ordinal).Count());
        return call;
    }

    private static NativeCall ExecuteClass(string caseId)
    {
        if (caseId == "class.mutable-data")
        {
            DaySchedule schedule = new("mutable", Enumerable.Repeat(0d, DaySchedule.FixedLength));
            IList<double> values = Assert.IsAssignableFrom<IList<double>>(schedule.Values);
            Assert.True(values.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => values[0] = 9d);
            Assert.Equal(0d, schedule[0]);
            return Returned("native values are read-only and rejected indexed replacement", "failed replacement retained first=0");
        }

        if (caseId == "class.sequence")
        {
            double[] values = Enumerable.Range(0, DaySchedule.FixedLength)
                .Select(value => (double)value)
                .ToArray();
            DaySchedule schedule = new("sequence", values);
            Assert.IsAssignableFrom<IReadOnlyList<double>>(schedule);
            Assert.Equal(144, schedule.Count);
            Assert.Equal(0d, schedule[0]);
            Assert.Equal(143d, schedule[143]);
            Assert.Equal(10_296d, schedule.Sum());
            return Returned("native sequence count=144 first=0 last=143 sum=10296");
        }

        Assert.Equal("class.source-isolation", caseId);
        double[] source = Enumerable.Repeat(0.25d, DaySchedule.FixedLength).ToArray();
        DaySchedule isolated = new("isolated", source, ScheduleType.Fraction);
        source[0] = 1d;
        Assert.Equal(0.25d, isolated[0]);
        Assert.Equal(1d, source[0]);
        return Returned("constructor copied the caller sequence", "source first=1 while native first=0.25");
    }

    private static NativeCall ExecuteDeepCopy(string caseId)
    {
        if (caseId == "deepcopy.memo-hit")
        {
            MethodInfo method = Assert.Single(
                typeof(DaySchedule).GetMethods(BindingFlags.Instance | BindingFlags.Public),
                candidate => candidate.Name == nameof(DaySchedule.DeepCopy));
            Assert.Empty(method.GetParameters());
            DaySchedule source = DaySchedule.Constant("memo", 1d);
            DaySchedule result = source.DeepCopy();
            Assert.NotSame(source, result);
            Assert.Equal("memo:COPY", result.Name);
            return Returned("native DeepCopy has no caller memo parameter", "native DeepCopy returned a fresh copy");
        }

        if (caseId == "deepcopy.normal")
        {
            DaySchedule source = new(
                "source",
                RepeatPattern(0.2d, 0.8d),
                ScheduleType.Fraction,
                "ratio");
            DaySchedule result = source.DeepCopy();
            Assert.NotSame(source, result);
            Assert.NotSame(source.Values, result.Values);
            Assert.Equal("source:COPY", result.Name);
            Assert.Equal(source.Type, result.Type);
            Assert.Equal(source.Unit, result.Unit);
            Assert.Equal(source.Values, result.Values);
            return Returned("ordinary native copy retained values type and unit", "ordinary native copy name=source:COPY");
        }

        Assert.Equal("deepcopy.repeated", caseId);
        DaySchedule repeatedSource = DaySchedule.Constant("source", 2d);
        DaySchedule left = repeatedSource.DeepCopy();
        DaySchedule right = repeatedSource.DeepCopy();
        Assert.NotSame(left, right);
        Assert.NotSame(left, repeatedSource);
        Assert.NotSame(right, repeatedSource);
        Assert.Equal(left.Values, right.Values);
        return Returned("repeated native copies are distinct from source and each other", "repeated native copies retained equal values");
    }

    private static NativeCall ExecuteInit(string caseId)
    {
        if (caseId == "init.default")
        {
            DaySchedule result = new();
            Assert.Equal("anonymous", result.Name);
            Assert.Equal(ScheduleType.Real, result.Type);
            Assert.Null(result.Unit);
            Assert.Equal(144, result.Count);
            Assert.All(result, value => Assert.Equal(0d, value));
            return Returned("default native construction produced deterministic anonymous name", "default native construction produced 144 Real zeros");
        }

        if (caseId == "init.nonfinite-real")
        {
            double[] values = new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
                .Concat(Enumerable.Repeat(0d, 141))
                .ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DaySchedule("nonfinite", values, ScheduleType.Real));
            return RaisedDomain("native construction rejected non-finite Real values");
        }

        Assert.Equal("init.text-preservation", caseId);
        DaySchedule text = new(
            "  padded  ",
            Enumerable.Repeat(1d, DaySchedule.FixedLength),
            ScheduleType.Real,
            "  W  ");
        Assert.Equal("padded", text.Name);
        Assert.Equal("W", text.Unit);
        return Returned("native construction trimmed name to padded", "native construction trimmed unit to W");
    }

    private static NativeCall ExecuteSetItem(string caseId)
    {
        DaySchedule source = DaySchedule.Constant("items", 0.25d, ScheduleType.Fraction, "ratio");
        if (caseId == "setitem.positive")
        {
            DaySchedule result = source.WithValue(5, 0.75d);
            Assert.NotSame(source, result);
            Assert.Equal(0.25d, source[5]);
            Assert.Equal(0.75d, result[5]);
            Assert.Equal(source.Name, result.Name);
            Assert.Equal(source.Type, result.Type);
            Assert.Equal(source.Unit, result.Unit);
            return Returned("WithValue returned a fresh native schedule with index 5 set to 0.75", "source index 5 remained 0.25");
        }

        if (caseId == "setitem.negative")
        {
            Assert.Throws<IndexOutOfRangeException>(() => source.WithValue(-1, 1d));
            Assert.All(source, value => Assert.Equal(0.25d, value));
            return RaisedRange("native index contract rejected -1", "failed negative update left source unchanged");
        }

        Assert.Equal("setitem.invalid-atomic", caseId);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithValue(3, 2d));
        Assert.All(source, value => Assert.Equal(0.25d, value));
        return RaisedDomain("native Fraction validation rejected value 2", "failed invalid update left source unchanged");
    }

    private static NativeCall ExecuteAsType(string caseId)
    {
        if (caseId == "astype.outplace-string")
        {
            DaySchedule source = new("typed", RepeatPattern(0d, 1d), ScheduleType.OnOff, "flag");
            DaySchedule result = source.AsType(ScheduleType.Real);
            Assert.NotSame(source, result);
            Assert.Equal(ScheduleType.OnOff, source.Type);
            Assert.Equal(ScheduleType.Real, result.Type);
            Assert.Equal(source.Name, result.Name);
            Assert.Equal(source.Unit, result.Unit);
            Assert.Equal(source.Values, result.Values);
            Assert.DoesNotContain(
                typeof(DaySchedule).GetMethods(BindingFlags.Instance | BindingFlags.Public),
                method => method.Name == nameof(DaySchedule.AsType)
                    && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)));
            return Returned("native AsType enum conversion returned a fresh Real schedule", "native surface excludes Python string type tokens");
        }

        if (caseId == "astype.inplace")
        {
            DaySchedule source = new("typed", RepeatPattern(0.25d, 0.75d), ScheduleType.Fraction, "ratio");
            DaySchedule result = source.AsType(ScheduleType.Real);
            Assert.Equal(ScheduleType.Fraction, source.Type);
            Assert.Equal(ScheduleType.Real, result.Type);
            Assert.Equal(source.Values, result.Values);
            Assert.NotSame(source, result);
            return Returned("native immutable conversion returned a replacement Real schedule", "native immutable conversion retained Fraction source");
        }

        Assert.Equal("astype.invalid-atomic", caseId);
        DaySchedule invalidSource = DaySchedule.Constant("typed", 2d, ScheduleType.Real);
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidSource.AsType(ScheduleType.Fraction));
        Assert.Equal(ScheduleType.Real, invalidSource.Type);
        Assert.All(invalidSource, value => Assert.Equal(2d, value));
        return RaisedDomain("native conversion rejected Real value 2 as Fraction", "failed native conversion left source type and values unchanged");
    }

    private static NativeCall ExecuteClip(string caseId)
    {
        if (caseId == "clip.bounds-empty-name")
        {
            DaySchedule source = new("source", RepeatPattern(-2d, 2d), unit: "kW");
            DaySchedule result = source.Clip(-1d, 1d, string.Empty);
            Assert.Equal("source:CLIP", result.Name);
            Assert.Equal("kW", result.Unit);
            Assert.Equal(-1d, result[0]);
            Assert.Equal(1d, result[1]);
            Assert.Equal(-2d, source[0]);
            Assert.Equal(2d, source[1]);
            return Returned("native empty-name clip used source:CLIP fallback", "native clip bounded values and retained source");
        }

        if (caseId == "clip.reversed")
        {
            DaySchedule source = new("source", RepeatPattern(-2d, 2d));
            Assert.Throws<ArgumentException>(() => source.Clip(3d, 1d));
            Assert.Equal(-2d, source[0]);
            Assert.Equal(2d, source[1]);
            return RaisedDomain("native clip rejected minimum 3 above maximum 1", "failed native clip retained source");
        }

        Assert.Equal("clip.signed-zero", caseId);
        DaySchedule lower = DaySchedule.Constant("lower", 0d).Clip(minimum: -0d);
        DaySchedule upper = DaySchedule.Constant("upper", -0d).Clip(maximum: 0d);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(lower[0]));
        Assert.Equal(BitConverter.DoubleToInt64Bits(0d), BitConverter.DoubleToInt64Bits(upper[0]));
        return Returned("native lower-bound tie selected negative zero", "native upper-bound tie selected positive zero");
    }

    private static NativeCall ExecuteFromCompact(string caseId)
    {
        if (caseId == "from-compact.valid")
        {
            DaySchedule result = DaySchedule.FromCompact(
                "office",
                new[]
                {
                    new DayScheduleSegment(TimeSpan.FromHours(9), 0d),
                    new DayScheduleSegment(TimeSpan.FromHours(18), 1d),
                    new DayScheduleSegment(TimeSpan.FromHours(24), 0d),
                },
                ScheduleType.OnOff);
            Assert.Equal("office", result.Name);
            Assert.Equal(ScheduleType.OnOff, result.Type);
            Assert.All(result.Take(54), value => Assert.Equal(0d, value));
            Assert.All(result.Skip(54).Take(54), value => Assert.Equal(1d, value));
            Assert.All(result.Skip(108), value => Assert.Equal(0d, value));
            return Returned("native compact factory preserved 09:00 and 18:00 transitions", "native compact factory produced 144 OnOff values");
        }

        if (caseId == "from-compact.off-grid")
        {
            DayScheduleSegment[] segments =
            {
                new(TimeSpan.FromMinutes(5), 1d),
                new(TimeSpan.FromHours(24), 0d),
            };
            Assert.Throws<ArgumentException>(() =>
                DaySchedule.FromCompact("offgrid", segments, ScheduleType.OnOff));
            return RaisedDomain("native compact factory rejected a 00:05 off-grid endpoint");
        }

        Assert.Equal("from-compact.invalid-end", caseId);
        DayScheduleSegment[] invalid =
        {
            new(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(50), 1d),
        };
        Assert.Throws<ArgumentException>(() =>
            DaySchedule.FromCompact("bad", invalid, ScheduleType.OnOff));
        return RaisedDomain("native compact factory required final endpoint 24:00");
    }

    private static NativeCall ExecuteFromConstant(string caseId)
    {
        if (caseId == "from-constant.bool-onoff")
        {
            DaySchedule result = DaySchedule.FromConstant("on", true, ScheduleType.OnOff);
            Assert.Equal("on", result.Name);
            Assert.Equal(ScheduleType.OnOff, result.Type);
            Assert.All(result, value => Assert.Equal(1d, value));
            return Returned("native Boolean constant produced 144 OnOff ones");
        }

        if (caseId == "from-constant.anonymous-real")
        {
            DaySchedule result = DaySchedule.FromConstant(null, 4.7d, ScheduleType.Real);
            Assert.Equal("anonymous", result.Name);
            Assert.Equal(ScheduleType.Real, result.Type);
            Assert.All(result, value => Assert.Equal(4.7d, value));
            return Returned("native null-name constant used anonymous", "native Real constant produced 144 values of 4.7");
        }

        Assert.Equal("from-constant.nonfinite", caseId);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DaySchedule.FromConstant("nan", double.NaN, ScheduleType.Real));
        return RaisedDomain("native constant factory rejected NaN");
    }

    private static NativeCall ExecuteFromWindows(string caseId)
    {
        if (caseId == "from-windows.first-overlap")
        {
            DaySchedule result = DaySchedule.FromWindows(
                "overlap",
                0d,
                new[]
                {
                    new DayScheduleWindow(TimeSpan.FromHours(8), TimeSpan.FromHours(12), 1d),
                    new DayScheduleWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(11), 2d),
                });
            Assert.All(result.Take(48), value => Assert.Equal(0d, value));
            Assert.All(result.Skip(48).Take(24), value => Assert.Equal(1d, value));
            Assert.All(result.Skip(72), value => Assert.Equal(0d, value));
            Assert.DoesNotContain(2d, result);
            return Returned("native window factory preserved first-match overlap precedence", "native 08:00-12:00 window selected value 1");
        }

        if (caseId == "from-windows.reversed")
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DayScheduleWindow(TimeSpan.FromHours(18), TimeSpan.FromHours(9), 1d));
            return RaisedDomain("native window value rejected reversed 18:00-09:00 range");
        }

        Assert.Equal("from-windows.out-of-day", caseId);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DayScheduleWindow(TimeSpan.FromHours(-1), TimeSpan.FromHours(1), 1d));
        return RaisedDomain("native window value rejected -01:00 start");
    }

    private static NativeCall ExecuteType(string caseId)
    {
        if (caseId == "type.getters")
        {
            ScheduleType[] types =
            {
                ScheduleType.OnOff,
                ScheduleType.Fraction,
                ScheduleType.Real,
                ScheduleType.Temperature,
            };
            foreach (ScheduleType type in types)
            {
                DaySchedule schedule = DaySchedule.Constant(type.ToString(), 0d, type);
                Assert.Equal(type, schedule.Type);
            }

            Assert.False(typeof(DaySchedule).GetProperty(nameof(DaySchedule.Type))!.CanWrite);
            return Returned("native getter returned OnOff Fraction Real and Temperature enums", "native Type property is read-only");
        }

        if (caseId == "type.invalid-token")
        {
            DaySchedule schedule = DaySchedule.Constant("typed", 0d);
            PropertyInfo property = typeof(DaySchedule).GetProperty(nameof(DaySchedule.Type))!;
            Assert.False(property.CanWrite);
            Exception? error = Record.Exception(() => property.SetValue(schedule, "invalid"));
            Assert.NotNull(error);
            Assert.IsAssignableFrom<ArgumentException>(error);
            Assert.Equal(ScheduleType.Real, schedule.Type);
            return RaisedType("native read-only enum property rejected invalid string token", "failed reflective setter retained Real type");
        }

        Assert.Equal("type.stale-string-setter", caseId);
        DaySchedule stale = DaySchedule.Constant("stale", 2d, ScheduleType.Real);
        Assert.Throws<ArgumentOutOfRangeException>(() => stale.AsType(ScheduleType.Fraction));
        Assert.Equal(ScheduleType.Real, stale.Type);
        Assert.All(stale, value => Assert.Equal(2d, value));
        return RaisedDomain("native validated conversion rejected stale Fraction type", "failed type conversion retained Real source");
    }

    private static NativeCall ExecuteCompactize(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "compactize.constant")
        {
            IReadOnlyList<DayScheduleSegment> compact = DaySchedule.Constant("constant", 2d)
                .Compactize();
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new
                {
                    compact = compact.Select(EncodeSegment).ToArray(),
                }));
            Assert.Single(compact);
            Assert.Equal(TimeSpan.FromHours(24), compact[0].Until);
            Assert.Equal(2d, compact[0].Value);
            return Returned("native compactization matched the pinned constant tuple");
        }

        if (caseId == "compactize.alternating")
        {
            IReadOnlyList<DayScheduleSegment> compact = new DaySchedule(
                "alternating",
                RepeatPattern(0d, 1d)).Compactize();
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new
                {
                    count = compact.Count,
                    first = EncodeSegment(compact[0]),
                    last = EncodeSegment(compact[compact.Count - 1]),
                }));
            Assert.Equal(144, compact.Count);
            return Returned("native alternating compactization matched 144 pinned segments", "native alternating compactization matched first and last tuples");
        }

        Assert.Equal("compactize.signed-zero", caseId);
        double[] values = Enumerable.Repeat(0d, DaySchedule.FixedLength).ToArray();
        values[values.Length - 1] = -0d;
        IReadOnlyList<DayScheduleSegment> signed = new DaySchedule("zero", values).Compactize();
        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new
            {
                compact = signed.Select(EncodeSegment).ToArray(),
            }));
        DayScheduleSegment only = Assert.Single(signed);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(only.Value));
        return Returned("native signed-zero compactization matched one pinned negative-zero segment");
    }

    private static NativeCall ExecuteSummary(string caseId, JsonElement pythonFacts)
    {
        string summary;
        if (caseId == "summary.rich")
        {
            summary = RichDay().Summary();
        }
        else if (caseId == "summary.negative-limit")
        {
            summary = RichDay().Summary(-1);
        }
        else
        {
            Assert.Equal("summary.repr-name", caseId);
            summary = DaySchedule.Constant("a'b", 1d, ScheduleType.Real, "W")
                .Summary(0);
        }

        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new { summary }));
        Assert.False(string.IsNullOrWhiteSpace(summary));
        return Returned($"native {caseId} text matched the pinned Python summary exactly");
    }

    private static NativeCall ExecuteTimeTuple(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "time-tuple.grid")
        {
            IReadOnlyList<TimeSpan> values = DaySchedule.TimeTuple();
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new
                {
                    count = values.Count,
                    items = values.Select(EncodeTime).ToArray(),
                }));
            Assert.Equal(144, values.Count);
            return Returned("native time tuple matched every pinned ten-minute grid endpoint");
        }

        if (caseId == "time-tuple.rollover")
        {
            IReadOnlyList<TimeSpan> values = DaySchedule.TimeTuple();
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new
                {
                    hour_end = EncodeTime(values[5]),
                    last = EncodeTime(values[values.Count - 1]),
                    midnight_first = EncodeTime(values[0]),
                }));
            Assert.Equal(TimeSpan.FromMinutes(10), values[0]);
            Assert.Equal(TimeSpan.FromHours(1), values[5]);
            Assert.Equal(TimeSpan.FromHours(24), values[143]);
            return Returned("native time tuple matched pinned first hour rollover and 24:00 endpoint");
        }

        Assert.Equal("time-tuple.fresh", caseId);
        IReadOnlyList<TimeSpan> left = DaySchedule.TimeTuple();
        IReadOnlyList<TimeSpan> right = DaySchedule.TimeTuple();
        Assert.NotSame(left, right);
        Assert.Equal(left, right);
        Assert.Equal(144, left.Count);
        Assert.Equal(144, right.Count);
        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new
            {
                distinct = true,
                left_count = left.Count,
                right_count = right.Count,
                same_values = left.SequenceEqual(right),
            }));
        return Returned("native time tuple calls returned distinct instances", "native time tuple instances shared the same 144 grid values");
    }

    private static NativeCall ExecuteToIdf(string caseId, JsonElement pythonFacts)
    {
        IReadOnlyList<string> fields = caseId switch
        {
            "to-idf.onoff" => DaySchedule.FromConstant("on", 1, ScheduleType.OnOff)
                .ToIdfCompactExpression(),
            "to-idf.real" => RichDay().ToIdfCompactExpression(),
            "to-idf.signed-zero" => DaySchedule.FromConstant(
                    "negative-zero",
                    -0d,
                    ScheduleType.Real)
                .ToIdfCompactExpression(),
            _ => throw new Xunit.Sdk.XunitException($"Unknown IDF case '{caseId}'."),
        };
        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new { fields = fields.ToArray() }));
        Assert.NotEmpty(fields);
        Assert.Equal(0, fields.Count % 2);
        return Returned($"native {caseId} fields matched the pinned Python compact expression exactly");
    }

    private static DaySchedule RichDay()
    {
        double[] values = Enumerable.Repeat(0d, 36)
            .Concat(Enumerable.Repeat(1.23456d, 12))
            .Concat(Enumerable.Repeat(10_000d, 54))
            .Concat(Enumerable.Repeat(-0.000012345d, 42))
            .ToArray();
        Assert.Equal(DaySchedule.FixedLength, values.Length);
        return new DaySchedule("workday", values, ScheduleType.Real, "kW");
    }

    private static double[] RepeatPattern(params double[] pattern)
    {
        Assert.NotEmpty(pattern);
        return Enumerable.Range(0, DaySchedule.FixedLength)
            .Select(index => pattern[index % pattern.Length])
            .ToArray();
    }

    private static object[] EncodeSegment(DayScheduleSegment segment)
    {
        int totalHours = (int)segment.Until.TotalHours;
        return new object[]
        {
            totalHours,
            segment.Until.Minutes,
            EncodeBinary64(segment.Value),
        };
    }

    private static int[] EncodeTime(TimeSpan value) =>
        new[] { (int)value.TotalHours, value.Minutes };

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
            return $"{sign}0.0p+0";
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

    private static NativeCall Returned(params string[] facts) =>
        new("returned", null, facts);

    private static NativeCall RaisedDomain(params string[] facts) =>
        new("raised", "domain", facts);

    private static NativeCall RaisedRange(params string[] facts) =>
        new("raised", "range", facts);

    private static NativeCall RaisedType(params string[] facts) =>
        new("raised", "type", facts);

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
