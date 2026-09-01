using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Profile;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class ScheduleCoreOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/schedule-core-oracle.json";
    private const string OracleSha256 =
        "sha256:cbb999f40dc0633acc1c1e58ed3681fe6ec76f9875c61c0aa9d22258a235b922";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Profile.ScheduleCoreOracleParityTests.MatchesPinnedPythonScheduleCore";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";

    private static readonly SymbolBinding[] ExpectedSymbols =
    {
        new("Schedule.FIXED_LENGTH", "constant", "sha256:60d994b214a3939f0fb0a15f398a1198ef3ef96416327199e5b8b8be5ba9f598", "sha256:9cdae9ed9b67d131225e71a359cc943afe964e49396a4cb4632616e960eb6892", "sha256:298d347ed1b3135773bbdfab53a8b58385e7938b13515fcb4f6e0b5b602114e8", 1, "equivalent", null, "profile-schedule-core-fixed-length-60d994b2"),
        new("Schedule.TIME_TUPLE", "constant", "sha256:e175d235cac1a4c1ad2f2f06b27f6df1ee8dfcafa93bcba1482d4c9fc3a823a3", "sha256:daf9c4eb96da759243b4648d6f79d7b00557a559a6590bad8e9c897152c9aefc", "sha256:95ec4a88da53823cc2ff388d2abf00988e08e74d841ae71523fe2d028ebd58fd", 1, "exception", "immutable-schedule-time-tuple", "profile-schedule-core-time-tuple-e175d235"),
        new("Schedule.__deepcopy__", "function", "sha256:be9a64938799225409f7b10083e2fcae187eb2bae01151a68f21305ffd240a7d", "sha256:520ee536d924ac7323d561d9d85957e67316c14aea8bd80a5664a796409a796f", "sha256:8a99c79db32660efce771100f29be4b7945134495bc3da13eed9ef68e769d57a", 3, "exception", "native-schedule-deepcopy-memo", "profile-schedule-core-deepcopy-be9a6493"),
        new("Schedule.__init__", "function", "sha256:72d34a65bd7c9b82f9962da98b6ec5e1496459918de625fb8e2126c9832ddf06", "sha256:915f78a8269d49c58d743378b7da96b3ff1df7163da08b063f2f6291272a3bb9", "sha256:e16fada203281a0116ba8511a9195082dae89888324e60b008abb6e4932563af", 11, "exception", "immutable-deterministic-schedule-construction", "profile-schedule-core-init-72d34a65"),
        new("Schedule.apply", "function", "sha256:cac23120005e2cac2c4729c70471a7796840160338fff91e1c49e9670f763ba9", "sha256:11be20cfabb1a9a724105598d037333aee0a027a5bd203ba5248f38dcaf2b36d", "sha256:f89ab40213ab94d716fdcc898b02f8a2a5f78a1c74af0f27fddaa866fee14aee", 9, "exception", "immutable-schedule-apply", "profile-schedule-core-apply-cac23120"),
        new("Schedule.astype", "function", "sha256:3c3e1ad91d7a933d4d60c38cab8d9b0ef5ed28f1b036eb23f3c9961115df2c07", "sha256:515cbd9ddcac180a896bc3f651cc7d38ba557d75e998517d9c9ee766d5e9928f", "sha256:2837c0d2bdea42beed61b1fe91bbbac544277ea9d37b6da2dd0831d78e29fcf4", 4, "exception", "immutable-schedule-astype", "profile-schedule-core-astype-3c3e1ad9"),
        new("Schedule.average", "function", "sha256:e5a1cd49cc7fd4ceff37a6ec7f39d72c7269e769e351d6c3b4e46a2fbd3fa9e0", "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "sha256:e29689f42cdca557f1c708f7ef0d4f993e54cb3a999d6b1ec998988a222a1443", 3, "equivalent", null, "profile-schedule-core-average-e5a1cd49"),
        new("Schedule.clip", "function", "sha256:a5c9474c7c676e8512720ed02a0f4f112e0e7380c5e2d3cc19e22d3aee38dec2", "sha256:b0d71606cea2f1cc014ca55449c3b02c261fa7e04ca108076c09bd966b870d04", "sha256:38390367e0a31d04201ff51fcbccb8f2e504b40d47b21ea3b87fbc004ec1e0be", 8, "exception", "immutable-schedule-clip", "profile-schedule-core-clip-a5c9474c"),
        new("Schedule.compactize", "function", "sha256:47d2d3d2edf795f4d2d532bd242e1f6497df6085dec0bc7ec0a6deeff74ae470", "sha256:6a340566f9769890b33c86757198014bbcaad1b857e6ce8cc69a7d357f381f99", "sha256:d3ec40973a9d283ffa9f8c4b629ba3ebda042a7a01eb03fa95097a15e72b73ce", 4, "equivalent", null, "profile-schedule-core-compactize-47d2d3d2"),
        new("Schedule.dayschedules", "function", "sha256:61806264198f15d60f0113d4c0aa9bc2dd6ffa4d7d9d719399297c90d4efe1e5", "sha256:3e09706dc5ff2656367a7ecd488a913671c872025d6e8dccab5213082073a876", "sha256:db20ff838295c8036cec06a81b03c817ee2744b90122924dcd820a9233f042d4", 2, "equivalent", null, "profile-schedule-core-dayschedules-61806264"),
        new("Schedule.from_compact", "function", "sha256:ce943fca5d32b9a2c538b68eca8edd3e9fd16f63f9fc7dc7847ff7695719622b", "sha256:1a0668a6302027c9a780f3f9f644c94886cae9a1abfde890960b76cbce42e3d9", "sha256:d116e3a00349284826b541bb48d3cbd62f7c82cdd1fd0a3b985e9726737a93bd", 9, "exception", "validated-deterministic-schedule-from-compact", "profile-schedule-core-from-compact-ce943fca"),
        new("Schedule.from_constant", "function", "sha256:921474e6c535cf86b9c5452f828a1ee00c0a9aafbafc8b64ea9d3a924f3242fd", "sha256:f96ffc362d2cec11d62eb810ed61967a2c8c324edb151220ab1429dc3e120280", "sha256:9570b19d99792bc153a0b1612d5ccfbe36be98b24fb6265214795ec218358617", 8, "exception", "deterministic-schedule-from-constant-child-names", "profile-schedule-core-from-constant-921474e6"),
        new("Schedule.from_windows", "function", "sha256:95346844e1f1e0554287cd904a0b24ef17522ccb1099007fbb3b72cfd2703a2d", "sha256:1071a09f948c41086f61c3210be1c69e6d82a27fc80e25eb1eceacd7c44d7713", "sha256:b250c49f69656c3fce4948aaedf874ae9d290f12879195935d3646d4e3a782cf", 11, "exception", "validated-deterministic-schedule-from-windows", "profile-schedule-core-from-windows-95346844"),
        new("Schedule.integral", "function", "sha256:ef9cd611a4831abe92322e26ea5be6be6c185f9afc07efc8560ca4818e20c254", "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "sha256:d53cdc426a1670ce21522bc8aeab29bdff5112e18c702a249720a730f1a73fb9", 3, "equivalent", null, "profile-schedule-core-integral-ef9cd611"),
        new("Schedule.max", "function", "sha256:5b932882346fc3af953b9bf3695807e819437b1938e15ea9dd62d89548c5a66b", "sha256:f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "sha256:33e1df330dcca418e12f40f8f6a4789f4361ba152380eb3b3892de44c566460e", 2, "equivalent", null, "profile-schedule-core-max-5b932882"),
        new("Schedule.min", "function", "sha256:788223628b6747bd445c617bbc83e40062f57ac8168a06908c67dff054d25771", "sha256:f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "sha256:2be9195cbd5a944c9cefed57d405bba6e08678fb3105cb84999514a5428ef7ea", 2, "equivalent", null, "profile-schedule-core-min-78822362"),
        new("Schedule.positive_average", "function", "sha256:8c464f8c2937679875bcb851700d359c029c7a9d6480c138a15cbb82ea3cfc2a", "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "sha256:59525f389d783209593724d13b1ac128e5cd8054c68e41b184291cb3c294a137", 3, "equivalent", null, "profile-schedule-core-positive-average-8c464f8c"),
        new("Schedule.summary", "function", "sha256:6ccaf08dce837f4e10048cdd029bde79236ef48b0731d455abd213844e9b7118", "sha256:09c155d4dbac079ab0e2a8703f3c48d4c1f38e9947ac9733b653e0cc15d66ea5", "sha256:63c0ceb166516f60bbd298b3a3b358d7498545497d020b258b5501adc1ee72f9", 4, "equivalent", null, "profile-schedule-core-summary-6ccaf08d"),
        new("Schedule.to_idf_object", "function", "sha256:afa76bbb026a6b79b79602918b37f93ad3f936239cbd7b57e3a037d59e8b30fc", "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008", "sha256:3ed49d8ba7ff73f008ef438dff59db25e52d286bccbf0b2ba476c3922645490e", 4, "equivalent", null, "profile-schedule-core-to-idf-object-afa76bbb"),
        new("Schedule.type", "function", "sha256:2819b394ba739818561d80cd1f770484a9e012fb106d62d66ed4cb53c35d1c7b", "sha256:21dd5d9cec4df73e1829358d690a1f3c0e75892344709e48f90464e0b1c3dd00", "sha256:c127d9c3b77bd9baf591dbecb6bac00e4249af93aa7b3ef5a503333c07abc581", 2, "equivalent", null, "profile-schedule-core-type-2819b394"),
        new("Schedule.unify_compactized_schedules", "function", "sha256:6f7741b799e71d8d5f0f180a0d9ade68fe4d3a86bf0fde346f34a57b287c8231", "sha256:23e8f30b79164d3bb101d8ba53ae33e0308f40c0f743d2e26977b71a1b492468", "sha256:af7687ab1d8efa024be3423ab042f4f0c89f7dbf75b89eb76b0d7f349f4234b0", 5, "exception", "validated-schedule-unify-coverage", "profile-schedule-core-unify-pair-6f7741b7"),
        new("Schedule.unify_compactized_schedules_many", "function", "sha256:51d9dbc95d51184a2b61120e8501128d2c9dd5cac33bee6020b0695b0c657788", "sha256:5fd67edc18132c398911f846e6255b4ddc9d6c1eacf4d9ce829a15f99b68f9ba", "sha256:530ccb7960abc4c9fe6169f01426e726877b251439ab2531002ac53e547ff002", 5, "equivalent", null, "profile-schedule-core-unify-many-51d9dbc9"),
    };

    private static readonly string[] ExpectedMutationQuirks =
    {
        "apply.inplace-inclusive-mmdd:schedule",
        "apply.parse-digit-pair:schedule",
        "apply.parse-yyyymmdd:schedule",
        "apply.type-unchecked:schedule",
        "astype.inplace-partial:good",
        "astype.inplace-partial:schedule",
        "astype.inplace-stale:left",
        "astype.inplace-stale:middle",
        "astype.inplace-stale:schedule",
        "clip.inplace-distinct:schedule",
        "clip.inplace-partial:schedule",
        "init.supplied-list-alias:rulesets",
        "init.supplied-list-alias:schedule",
    };

    private static readonly CaseBinding[] ExpectedCases = BuildExpectedCases();
    private static readonly Dictionary<string, NativeExpectation> ExpectedAdaptations =
        BuildExpectedAdaptations();

    private static CaseBinding[] BuildExpectedCases()
    {
        var result = new List<CaseBinding>();
        void Add(string symbol, string executor, params string[] caseIds)
        {
            result.AddRange(caseIds.Select(caseId => new CaseBinding(caseId, symbol, executor)));
        }

        Add("Schedule.apply", "apply",
            "apply.foreign-year-noop", "apply.inplace-inclusive-mmdd", "apply.invalid-date",
            "apply.noninplace-deepcopy", "apply.outside-year-noop", "apply.parse-digit-pair",
            "apply.parse-yyyymmdd", "apply.reversed-noop", "apply.type-unchecked");
        Add("Schedule.astype", "astype",
            "astype.inplace-partial", "astype.inplace-stale", "astype.outplace",
            "astype.outplace-failure-atomic");
        Add("Schedule.average", "metric",
            "average.catastrophic", "average.minimum-subnormal", "average.negative-zero");
        Add("Schedule.clip", "clip",
            "clip.empty-name-default", "clip.inplace-distinct", "clip.inplace-partial",
            "clip.min-greater-than-max", "clip.outplace-bounds", "clip.outplace-lower-only",
            "clip.outplace-no-bounds-copy", "clip.outplace-upper-only");
        Add("Schedule.compactize", "compactize",
            "compactize.default-distinct", "compactize.equal-distinct",
            "compactize.full-run", "compactize.identity-runs");
        Add("Schedule.FIXED_LENGTH", "constant", "constant.fixed-length");
        Add("Schedule.TIME_TUPLE", "constant", "constant.time-tuple");
        Add("Schedule.dayschedules", "dayschedules",
            "dayschedules.fresh-list", "dayschedules.weekday-overrides");
        Add("Schedule.__deepcopy__", "deepcopy",
            "deepcopy.memo-hit", "deepcopy.noncontiguous-alias-split", "deepcopy.shared-period");
        Add("Schedule.from_compact", "from-compact",
            "from-compact.distinct-equal-adjacent", "from-compact.empty",
            "from-compact.leap-day", "from-compact.mixed-type", "from-compact.outside-noop",
            "from-compact.overlap-later-wins", "from-compact.reversed-noop",
            "from-compact.same-ref-adjacent", "from-compact.single-gap");
        Add("Schedule.from_constant", "from-constant",
            "from-constant.anonymous", "from-constant.bool",
            "from-constant.day-explicit-type-ignored", "from-constant.real-nan",
            "from-constant.ruleset-explicit-type-ignored", "from-constant.scalar",
            "from-constant.surrounding-space-name", "from-constant.unsupported-object");
        Add("Schedule.from_windows", "from-windows",
            "from-windows.day-alias", "from-windows.empty", "from-windows.leap-day",
            "from-windows.repeated-day-wrappers", "from-windows.repeated-scalar-wrappers",
            "from-windows.reversed-noop", "from-windows.ruleset-alias",
            "from-windows.scalar-overlap", "from-windows.scalar-positive-infinity",
            "from-windows.type-mismatch", "from-windows.unsupported-object");
        Add("Schedule.to_idf_object", "idf",
            "idf.constant-real", "idf.default-expanded-fields",
            "idf.multiple-periods", "idf.rich-overrides");
        Add("Schedule.__init__", "init",
            "init.anonymous", "init.default-fraction", "init.default-real", "init.empty-name",
            "init.explicit-type-mismatch", "init.invalid-item", "init.invalid-length",
            "init.mixed-types", "init.supplied-list-alias", "init.surrounding-space-name",
            "init.whitespace-name");
        Add("Schedule.integral", "metric",
            "integral.catastrophic", "integral.minimum-subnormal", "integral.overflow");
        Add("Schedule.max", "extrema", "max.negative-zero", "max.unused-holiday");
        Add("Schedule.min", "extrema", "min.negative-zero", "min.unused-holiday");
        Add("Schedule.positive_average", "metric",
            "positive-average.catastrophic", "positive-average.minimum-subnormal",
            "positive-average.none");
        Add("Schedule.summary", "summary",
            "summary.exact-rich", "summary.invalid-period-limit",
            "summary.negative-period-limit", "summary.zero-period-limit");
        Add("Schedule.type", "type", "type.explicit-fraction", "type.normal");
        Add("Schedule.unify_compactized_schedules_many", "unify",
            "unify-many.asymmetric-three", "unify-many.first-overlap-wins",
            "unify-many.missing-coverage", "unify-many.one-empty", "unify-many.zero");
        Add("Schedule.unify_compactized_schedules", "unify",
            "unify-pair.asymmetric", "unify-pair.empty", "unify-pair.first-overlap-wins",
            "unify-pair.interior-gap", "unify-pair.missing-coverage");
        return result.OrderBy(item => item.CaseId, StringComparer.Ordinal).ToArray();
    }

    private static Dictionary<string, NativeExpectation> BuildExpectedAdaptations()
    {
        var result = new Dictionary<string, NativeExpectation>(StringComparer.Ordinal);
        void Add(
            string adaptation,
            string outcome,
            string? errorCategory,
            params string[] caseIds)
        {
            foreach (string caseId in caseIds)
            {
                Assert.True(result.TryAdd(
                    caseId,
                    new NativeExpectation(adaptation, outcome, errorCategory)));
            }
        }

        Add("immutable-schedule-apply", "returned", null,
            "apply.foreign-year-noop", "apply.inplace-inclusive-mmdd",
            "apply.noninplace-deepcopy", "apply.outside-year-noop",
            "apply.parse-digit-pair", "apply.parse-yyyymmdd");
        Add("immutable-schedule-apply", "raised", "domain",
            "apply.invalid-date", "apply.reversed-noop", "apply.type-unchecked");
        Add("immutable-schedule-astype", "returned", null, "astype.inplace-stale");
        Add("immutable-schedule-astype", "raised", "domain", "astype.inplace-partial");
        Add("immutable-schedule-clip", "returned", null, "clip.inplace-distinct");
        Add("immutable-schedule-clip", "raised", "domain",
            "clip.inplace-partial", "clip.min-greater-than-max");
        Add("immutable-schedule-time-tuple", "returned", null, "constant.time-tuple");
        Add("native-schedule-deepcopy-memo", "returned", null, "deepcopy.memo-hit");
        Add("validated-deterministic-schedule-from-compact", "returned", null,
            "from-compact.distinct-equal-adjacent", "from-compact.outside-noop",
            "from-compact.overlap-later-wins", "from-compact.same-ref-adjacent",
            "from-compact.single-gap");
        Add("validated-deterministic-schedule-from-compact", "raised", "domain",
            "from-compact.leap-day", "from-compact.reversed-noop");
        Add("deterministic-schedule-from-constant-child-names", "returned", null,
            "from-constant.anonymous", "from-constant.bool",
            "from-constant.day-explicit-type-ignored", "from-constant.scalar",
            "from-constant.surrounding-space-name");
        Add("deterministic-schedule-from-constant-child-names", "raised", "domain",
            "from-constant.real-nan");
        Add("deterministic-schedule-from-constant-child-names", "raised", "type",
            "from-constant.unsupported-object");
        Add("validated-deterministic-schedule-from-windows", "returned", null,
            "from-windows.day-alias", "from-windows.empty",
            "from-windows.repeated-day-wrappers", "from-windows.repeated-scalar-wrappers",
            "from-windows.scalar-overlap");
        Add("validated-deterministic-schedule-from-windows", "raised", "domain",
            "from-windows.leap-day", "from-windows.reversed-noop",
            "from-windows.scalar-positive-infinity");
        Add("validated-deterministic-schedule-from-windows", "raised", "type",
            "from-windows.unsupported-object");
        Add("immutable-deterministic-schedule-construction", "returned", null,
            "init.anonymous", "init.default-fraction", "init.default-real",
            "init.supplied-list-alias", "init.surrounding-space-name");
        Add("immutable-deterministic-schedule-construction", "raised", "domain",
            "init.empty-name", "init.whitespace-name");
        Add("validated-schedule-unify-coverage", "raised", "domain",
            "unify-pair.interior-gap", "unify-pair.missing-coverage");

        Assert.Equal(48, result.Count);
        return result;
    }

    [Fact]
    public void MatchesPinnedPythonScheduleCore()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        Assert.Equal(OracleSha256, sha256);
        Assert.Equal(15_661_615, bytes.Length);

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo pinnedCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = pinnedCulture;
            CultureInfo.CurrentUICulture = pinnedCulture;

            using JsonDocument oracle = JsonDocument.Parse(bytes);
            JsonElement root = oracle.RootElement;
            Dictionary<string, JsonElement> cases = ValidateCorpus(root);
            List<NativeObservation> observations = cases.Values
                .Select(ExecuteCase)
                .ToList();
            Assert.Equal(104, observations.Count);

            foreach (SymbolBinding binding in ExpectedSymbols)
            {
                NativeObservation[] symbolObservations = observations
                    .Where(item => item.Symbol == binding.Symbol)
                    .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(binding.CaseCount, symbolObservations.Length);
                TrustedEvidenceRecorder.Record(
                    binding.AssertionId,
                    EvidenceTestCase,
                    "not_applicable",
                    new
                    {
                        fixture = new
                        {
                            case_count = 104,
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
                        upstream_symbol = binding.Symbol,
                    });
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static Dictionary<string, JsonElement> ValidateCorpus(JsonElement root)
    {
        AssertKeys(root, "cases", "consumer_contract", "runtime", "schema", "summary", "symbols", "upstream");
        Assert.Equal(
            "dragons.invisibledragon.schedule-core-oracle.v1",
            RequiredString(root, "schema"));

        JsonElement consumer = root.GetProperty("consumer_contract");
        AssertKeys(consumer, "annual_length", "culture", "date_grid", "float_encoding", "identity_encoding", "idf_observation_scope", "native_container_mappings", "period_endpoints", "runtime_names");
        Assert.Equal(365, consumer.GetProperty("annual_length").GetInt32());
        Assert.Equal("fr-FR", RequiredString(consumer, "culture"));
        Assert.Equal("2026-non-leap-inclusive", RequiredString(consumer, "date_grid"));
        Assert.Equal("json-number-plus-python-binary64-hex-without-0x-prefix", RequiredString(consumer, "float_encoding"));
        Assert.Equal("case-local-type-scoped-first-observation-groups", RequiredString(consumer, "identity_encoding"));
        Assert.Equal("inclusive-iso-date", RequiredString(consumer, "period_endpoints"));
        Assert.Equal("normalized-identity-linked-segments", RequiredString(consumer, "runtime_names"));
        JsonElement idfScope = consumer.GetProperty("idf_observation_scope");
        AssertKeys(idfScope, "excluded", "included");
        Assert.Equal(
            "raw-logical-Schedule:Compact-object-type-field-order-field-values-and-extended-input",
            RequiredString(idfScope, "included"));
        Assert.Equal(
            "rendered-IdfObject-text-escaping-and-sanitization; covered-by-the-separate-IdfObject-serializer-contract",
            RequiredString(idfScope, "excluded"));
        JsonElement mappings = consumer.GetProperty("native_container_mappings");
        AssertKeys(mappings, "Schedule.dayschedules", "Schedule.to_idf_object");
        JsonElement daysMapping = mappings.GetProperty("Schedule.dayschedules");
        AssertKeys(daysMapping, "dotnet", "preserved", "python");
        Assert.Equal("fresh-read-only-collection-on-every-property-access", RequiredString(daysMapping, "dotnet"));
        Assert.Equal("length-order-and-DaySchedule-reference-identity", RequiredString(daysMapping, "preserved"));
        Assert.Equal("fresh-mutable-list-on-every-property-access", RequiredString(daysMapping, "python"));
        JsonElement idfMapping = mappings.GetProperty("Schedule.to_idf_object");
        AssertKeys(
            idfMapping,
            "dotnet",
            "fixture_validation_metadata",
            "normalization",
            "preserved",
            "python");
        Assert.Equal(
            "one-contiguous-IdfObject.Fields-logical-value-sequence",
            RequiredString(idfMapping, "dotnet"));
        Assert.Equal(
            "python-field-names-and-primary-extension-boundary-only; native-IdfObject-has-no-separate-extended-collection",
            RequiredString(idfMapping, "fixture_validation_metadata"));
        Assert.Equal(
            "only-trailing-null-primary-slots-may-be-omitted",
            RequiredString(idfMapping, "normalization"));
        Assert.Equal(
            "object-type-exact-non-null-primary-prefix-in-field-position-order-and-exact-extension-continuation",
            RequiredString(idfMapping, "preserved"));
        Assert.Equal(
            "ordered-fixed-153-primary-data-entries-plus-ordered-extended_input",
            RequiredString(idfMapping, "python"));

        JsonElement runtime = root.GetProperty("runtime");
        AssertKeys(runtime, "implementation", "python_hash_algorithm", "python_hash_seed", "python_hash_width_bits", "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal("847b01f68f438f560a986072bcaa7768fbf67897", RequiredString(upstream, "commit"));
        Assert.Equal("sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02", RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal("sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", RequiredString(upstream, "source_sha256"));

        ValidateSymbols(root.GetProperty("symbols"));
        ValidateSummary(root.GetProperty("summary"));

        JsonElement[] caseElements = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(104, caseElements.Length);
        string[] caseIds = caseElements.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(caseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), caseIds);
        Assert.Equal(caseIds.Length, caseIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ExpectedCases.Select(item => item.CaseId).ToArray(), caseIds);

        var mutationQuirks = new List<string>();
        int adaptationCount = 0;
        int pythonRaised = 0;
        int dotnetRaised = 0;
        int dotnetReturned = 0;
        for (int caseIndex = 0; caseIndex < caseElements.Length; caseIndex++)
        {
            JsonElement item = caseElements[caseIndex];
            CaseBinding caseBinding = ExpectedCases[caseIndex];
            bool adapted = item.TryGetProperty("expected_dotnet", out JsonElement expectedDotnet);
            bool pinnedAdapted = ExpectedAdaptations.TryGetValue(
                caseBinding.CaseId,
                out NativeExpectation? pinnedExpectation);
            Assert.Equal(pinnedAdapted, adapted);
            AssertKeys(item, adapted
                ? new[] { "expected_dotnet", "id", "observation", "symbol" }
                : new[] { "id", "observation", "symbol" });
            string symbol = RequiredString(item, "symbol");
            Assert.Equal(caseBinding.CaseId, RequiredString(item, "id"));
            Assert.Equal(caseBinding.Symbol, symbol);
            SymbolBinding binding = Assert.Single(ExpectedSymbols, candidate => candidate.Symbol == symbol);

            if (adapted)
            {
                adaptationCount++;
                ValidateExpectedDotnet(expectedDotnet, binding, pinnedExpectation!);
                if (RequiredString(expectedDotnet, "outcome") == "raised")
                {
                    dotnetRaised++;
                }
                else
                {
                    dotnetReturned++;
                }
            }

            JsonElement observation = item.GetProperty("observation");
            string pythonOutcome = RequiredString(observation, "outcome");
            if (pythonOutcome == "raised")
            {
                pythonRaised++;
                AssertKeys(observation, "error_category", "exception", "input_postconditions", "outcome");
                Assert.Contains(RequiredString(observation, "error_category"), new[] { "domain", "type" });
                JsonElement exception = observation.GetProperty("exception");
                AssertKeys(exception, "message", "type");
                Assert.Contains(RequiredString(exception, "type"), new[] { "TypeError", "ValueError" });
                _ = RequiredString(exception, "message");
            }
            else
            {
                Assert.Equal("returned", pythonOutcome);
                AssertKeys(observation, "input_postconditions", "outcome", "result");
            }

            JsonElement postconditions = observation.GetProperty("input_postconditions");
            Assert.Equal(JsonValueKind.Object, postconditions.ValueKind);
            AssertUniqueObjectKeys(postconditions);
            foreach (JsonProperty property in postconditions.EnumerateObject())
            {
                JsonElement postcondition = property.Value;
                AssertKeys(postcondition, "after", "before", "preserved");
                if (!postcondition.GetProperty("preserved").GetBoolean())
                {
                    mutationQuirks.Add($"{RequiredString(item, "id")}:{property.Name}");
                }
            }

            ValidateJsonNode(observation);
        }

        Assert.Equal(48, adaptationCount);
        Assert.Equal(17, pythonRaised);
        Assert.Equal(18, dotnetRaised);
        Assert.Equal(30, dotnetReturned);
        Assert.Equal(ExpectedMutationQuirks, mutationQuirks.OrderBy(item => item, StringComparer.Ordinal).ToArray());
        return caseElements.ToDictionary(item => RequiredString(item, "id"), StringComparer.Ordinal);
    }

    private static void ValidateSymbols(JsonElement symbolsElement)
    {
        JsonElement[] symbols = symbolsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            JsonElement actual = symbols[index];
            SymbolBinding expected = ExpectedSymbols[index];
            AssertKeys(actual, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(expected.BodyHash, RequiredString(actual, "body_hash"));
            Assert.Equal(expected.Kind, RequiredString(actual, "kind"));
            Assert.Equal(UpstreamPath, RequiredString(actual, "path"));
            Assert.Equal(expected.SignatureHash, RequiredString(actual, "signature_hash"));
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(expected.SymbolHash, RequiredString(actual, "symbol_hash"));
        }
    }

    private static void ValidateSummary(JsonElement summary)
    {
        AssertKeys(summary, "adaptation_case_count", "adaptation_ids", "case_count", "classification_counts", "equivalent_symbols", "exception_symbols", "expected_dotnet_outcomes", "observed_outcomes", "symbol_case_counts");
        Assert.Equal(48, summary.GetProperty("adaptation_case_count").GetInt32());
        Assert.Equal(104, summary.GetProperty("case_count").GetInt32());
        Assert.Equal(
            ExpectedSymbols.Where(item => item.Classification == "equivalent").Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            summary.GetProperty("equivalent_symbols").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ExpectedSymbols.Where(item => item.Classification == "exception").Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            summary.GetProperty("exception_symbols").EnumerateArray().Select(item => item.GetString()!).ToArray());

        string[] adaptationIds = ExpectedSymbols
            .Select(item => item.AdaptationId)
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(adaptationIds, summary.GetProperty("adaptation_ids").EnumerateArray().Select(item => item.GetString()!).ToArray());
        AssertCountObject(summary.GetProperty("classification_counts"), ("equivalent", 12), ("exception", 10));
        AssertCountObject(summary.GetProperty("expected_dotnet_outcomes"), ("raised", 18), ("returned", 30));
        AssertCountObject(summary.GetProperty("observed_outcomes"), ("raised", 17), ("returned", 87));

        JsonElement symbolCounts = summary.GetProperty("symbol_case_counts");
        AssertKeys(symbolCounts, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolBinding binding in ExpectedSymbols)
        {
            Assert.Equal(binding.CaseCount, symbolCounts.GetProperty(binding.Symbol).GetInt32());
        }
    }

    private static void ValidateExpectedDotnet(
        JsonElement expectation,
        SymbolBinding binding,
        NativeExpectation pinned)
    {
        string outcome = RequiredString(expectation, "outcome");
        AssertKeys(expectation, outcome == "raised"
            ? new[] { "adaptation", "error_category", "outcome", "policy" }
            : new[] { "adaptation", "outcome", "policy" });
        Assert.Equal(binding.AdaptationId, RequiredString(expectation, "adaptation"));
        Assert.Equal(pinned.Adaptation, RequiredString(expectation, "adaptation"));
        Assert.Contains(outcome, new[] { "raised", "returned" });
        Assert.Equal(pinned.Outcome, outcome);
        Assert.False(string.IsNullOrWhiteSpace(RequiredString(expectation, "policy")));
        if (outcome == "raised")
        {
            Assert.Contains(RequiredString(expectation, "error_category"), new[] { "domain", "type" });
            Assert.Equal(pinned.ErrorCategory, RequiredString(expectation, "error_category"));
        }
        else
        {
            Assert.Null(pinned.ErrorCategory);
        }
    }

    private static void ValidateJsonNode(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateJsonNode(item);
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        AssertUniqueObjectKeys(value);
        if (value.TryGetProperty("kind", out JsonElement kindElement))
        {
            string kind = kindElement.GetString()!;
            switch (kind)
            {
                case "binary64":
                    AssertKeys(value, "hex_without_prefix", "kind", "value");
                    Assert.Equal(
                        ToPythonHexWithoutPrefix(value.GetProperty("value").GetDouble()),
                        RequiredString(value, "hex_without_prefix"));
                    break;
                case "bool":
                    AssertKeys(value, "kind", "value");
                    Assert.Contains(
                        value.GetProperty("value").ValueKind,
                        new[] { JsonValueKind.False, JsonValueKind.True });
                    break;
                case "compact-periods":
                    AssertKeys(value, "kind", "object_graph", "periods");
                    break;
                case "date":
                    AssertKeys(value, "kind", "value");
                    Assert.True(DateTime.TryParseExact(RequiredString(value, "value"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
                    break;
                case "date-sequence":
                    AssertKeys(value, "container", "dates", "kind", "length");
                    Assert.Equal(value.GetProperty("length").GetInt32(), value.GetProperty("dates").GetArrayLength());
                    break;
                case "day-schedule":
                    AssertKeys(value, "identity_group", "kind", "name", "schedule_type", "unit", "values");
                    break;
                case "day-schedule-sequence":
                    AssertKeys(value, "container", "day_schedules", "kind", "length", "references");
                    break;
                case "idf-object":
                    AssertKeys(value, "data_entries", "extended_input", "kind", "object_type");
                    ValidateLogicalIdf(value);
                    break;
                case "int":
                    AssertKeys(value, "kind", "value");
                    _ = value.GetProperty("value").GetInt64();
                    break;
                case "literal":
                case "runtime-identity":
                    AssertKeys(value, "kind", "value");
                    _ = RequiredString(value, "value");
                    break;
                case "none":
                case "same-as-before":
                    AssertKeys(value, "kind");
                    break;
                case "nonfinite":
                    AssertKeys(value, "kind", "value");
                    Assert.Contains(RequiredString(value, "value"), new[] { "nan", "negative-infinity", "positive-infinity" });
                    break;
                case "object":
                    AssertKeys(value, "kind", "python_type");
                    _ = RequiredString(value, "python_type");
                    break;
                case "ruleset":
                    AssertKeys(value, "identity_group", "kind", "object_graph");
                    break;
                case "ruleset-sequence":
                    AssertKeys(value, "container", "kind", "length", "object_graph", "references");
                    break;
                case "schedule":
                    AssertKeys(value, "identity_group", "kind", "length", "name", "object_graph", "rule_references", "schedule_type");
                    break;
                case "schedule-type":
                    AssertKeys(value, "idf_object_name", "kind", "value");
                    break;
                case "sequence":
                    AssertKeys(value, "container", "items", "kind");
                    break;
                case "text":
                    AssertKeys(value, "kind", "value");
                    _ = RequiredString(value, "value");
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"Unknown Schedule core descriptor kind '{kind}'.");
            }
        }

        if (value.TryGetProperty("policy", out JsonElement policyElement))
        {
            string policy = policyElement.GetString()!;
            if (policy == "runtime-identity-hex")
            {
                AssertKeys(value, "identity_group", "policy");
            }
            else if (policy == "literal-with-normalized-runtime-identities")
            {
                AssertKeys(value, "policy", "segments");
            }
        }

        if (value.TryGetProperty("encoding", out JsonElement encodingElement))
        {
            string encoding = encodingElement.GetString()!;
            AssertKeys(value, encoding == "empty"
                ? new[] { "encoding", "length" }
                : new[] { "encoding", "length", "pattern" });
            Assert.Contains(encoding, new[] { "empty", "repeat" });
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            ValidateJsonNode(property.Value);
        }
    }

    private static void ValidateLogicalIdf(JsonElement descriptor)
    {
        JsonElement[] entries = descriptor.GetProperty("data_entries").EnumerateArray().ToArray();
        Assert.Equal(153, entries.Length);
        bool sawNull = false;
        for (int index = 0; index < entries.Length; index++)
        {
            JsonElement entry = entries[index];
            AssertKeys(entry, "field", "value");
            Assert.Equal(
                index switch
                {
                    0 => "Name",
                    1 => "Schedule Type Limits Name",
                    _ => $"Field {index - 1}",
                },
                RequiredString(entry, "field"));
            Assert.Contains(entry.GetProperty("value").ValueKind, new[] { JsonValueKind.Null, JsonValueKind.String });
            if (entry.GetProperty("value").ValueKind == JsonValueKind.Null)
            {
                sawNull = true;
            }
            else
            {
                Assert.False(sawNull, $"Primary IDF position {index} is non-null after a trailing null slot.");
            }
        }

        JsonElement[] extended = descriptor.GetProperty("extended_input").EnumerateArray().ToArray();
        Assert.All(extended, value => Assert.Equal(JsonValueKind.String, value.ValueKind));
        if (extended.Length > 0)
        {
            Assert.Equal(JsonValueKind.String, entries[152].GetProperty("value").ValueKind);
        }
    }

    private static NativeObservation ExecuteCase(JsonElement item)
    {
        string caseId = RequiredString(item, "id");
        string symbol = RequiredString(item, "symbol");
        CaseBinding binding = Assert.Single(ExpectedCases, candidate => candidate.CaseId == caseId);
        Assert.Equal(binding.Symbol, symbol);
        AssertExecutorPrefix(binding);
        JsonElement observation = item.GetProperty("observation");
        string pythonOutcome = RequiredString(observation, "outcome");
        JsonElement? expectedResult = pythonOutcome == "returned"
            ? observation.GetProperty("result")
            : null;
        string? adaptation = null;
        string expectedDotnetOutcome = pythonOutcome;
        string? expectedNativeErrorCategory = pythonOutcome == "raised"
            ? RequiredString(observation, "error_category")
            : null;
        if (ExpectedAdaptations.TryGetValue(caseId, out NativeExpectation? nativeExpectation))
        {
            adaptation = nativeExpectation.Adaptation;
            expectedDotnetOutcome = nativeExpectation.Outcome;
            expectedNativeErrorCategory = nativeExpectation.ErrorCategory;
        }

        var context = new NativeCaseContext(item);
        NativeCall call = binding.Executor switch
        {
            "apply" => ExecuteApply(caseId, context),
            "astype" => ExecuteAsType(caseId, expectedResult, context),
            "metric" => ExecuteMetric(caseId, expectedResult!.Value, context),
            "clip" => ExecuteClip(caseId, expectedResult, context),
            "compactize" => ExecuteCompactize(caseId, expectedResult!.Value, context),
            "constant" => ExecuteConstant(caseId, expectedResult!.Value, context),
            "dayschedules" => ExecuteDaySchedules(caseId, expectedResult!.Value, context),
            "deepcopy" => ExecuteDeepCopy(caseId, expectedResult, context),
            "from-compact" => ExecuteFromCompact(caseId, expectedResult, context),
            "from-constant" => ExecuteFromConstant(caseId, expectedResult, context),
            "from-windows" => ExecuteFromWindows(caseId, expectedResult, context),
            "idf" => ExecuteIdf(caseId, expectedResult!.Value, context),
            "init" => ExecuteInit(caseId, expectedResult, context),
            "extrema" => ExecuteExtrema(caseId, expectedResult!.Value, context),
            "summary" => ExecuteSummary(caseId, expectedResult, context),
            "type" => ExecuteType(caseId, expectedResult!.Value, context),
            "unify" => ExecuteUnification(caseId, expectedResult, context),
            _ => throw new Xunit.Sdk.XunitException($"No native Schedule core case executor exists for '{caseId}'."),
        };

        context.AssertFinalInputPostconditions();
        Assert.Equal(expectedDotnetOutcome, call.Outcome);
        Assert.Equal(expectedNativeErrorCategory, call.ErrorCategory);
        Assert.NotEmpty(call.Facts);
        Assert.Equal(call.Facts.Length, call.Facts.Distinct(StringComparer.Ordinal).Count());
        return new NativeObservation(
            caseId,
            symbol,
            call.Outcome,
            call.ErrorCategory,
            adaptation,
            call.Facts);
    }

    private static void AssertExecutorPrefix(CaseBinding binding)
    {
        string[] prefixes = binding.Executor switch
        {
            "apply" => new[] { "apply." },
            "astype" => new[] { "astype." },
            "clip" => new[] { "clip." },
            "compactize" => new[] { "compactize." },
            "constant" => new[] { "constant." },
            "dayschedules" => new[] { "dayschedules." },
            "deepcopy" => new[] { "deepcopy." },
            "extrema" => new[] { "max.", "min." },
            "from-compact" => new[] { "from-compact." },
            "from-constant" => new[] { "from-constant." },
            "from-windows" => new[] { "from-windows." },
            "idf" => new[] { "idf." },
            "init" => new[] { "init." },
            "metric" => new[] { "average.", "integral.", "positive-average." },
            "summary" => new[] { "summary." },
            "type" => new[] { "type." },
            "unify" => new[] { "unify-many.", "unify-pair." },
            _ => throw new Xunit.Sdk.XunitException(
                $"Unknown native executor key '{binding.Executor}'."),
        };
        Assert.Contains(
            prefixes,
            prefix => binding.CaseId.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static NativeCall ExecuteConstant(string caseId, JsonElement expected, NativeCaseContext context)
    {
        if (caseId == "constant.fixed-length")
        {
            Assert.Equal("int", RequiredString(expected, "kind"));
            Assert.Equal(Schedule.FixedLength, expected.GetProperty("value").GetInt32());
            return Returned("365-day native constant");
        }

        Assert.Equal("constant.time-tuple", caseId);
        Assert.True(DescriptorBoolean(expected.GetProperty("mutation_succeeded")));
        Assert.True(DescriptorBoolean(expected.GetProperty("is_class_value")));
        Assert.Equal(new DateTime(2026, 1, 2), ReadDate(expected.GetProperty("first_after_assignment")));
        IReadOnlyList<DateTime> first = Schedule.TimeTuple;
        IReadOnlyList<DateTime> second = Schedule.TimeTuple;
        context.BindDateSequenceInput("time_tuple", first);
        Assert.Same(first, second);
        Assert.Equal(365, first.Count);
        DateTime firstDay = new(2026, 1, 1);
        for (int index = 0; index < first.Count; index++)
        {
            Assert.Equal(firstDay.AddDays(index), first[index]);
        }

        IList<DateTime> mutableView = Assert.IsAssignableFrom<IList<DateTime>>(first);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = firstDay.AddDays(1));
        Assert.Throws<NotSupportedException>(() => mutableView.Add(firstDay));
        Assert.Throws<NotSupportedException>(() => mutableView.Remove(firstDay));
        Assert.Throws<NotSupportedException>(() => mutableView.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        Assert.Same(first, Schedule.TimeTuple);
        Assert.Equal(firstDay, first[0]);
        return Returned(
            "native TimeTuple is one stable collection instance",
            "native TimeTuple contains every consecutive 2026 date exactly once",
            "native TimeTuple rejects set/add/remove/remove-at/clear mutations");
    }

    private static NativeCall ExecuteInit(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        switch (caseId)
        {
            case "init.default-real":
                {
                    context.BindScalarInput("name", "DefaultReal");
                    Schedule schedule = new("DefaultReal");
                    AssertScheduleDescriptor(schedule, expected!.Value, context);
                    AssertDefaultGraphNames(schedule, "DefaultReal");
                    AssertSameNameTopology(schedule, new Schedule("DefaultReal"));
                    return Returned(
                        "native default constructor creates 365 distinct RuleSets",
                        "repeated native default construction has exact deterministic ordinal child names");
                }
            case "init.default-fraction":
                {
                    context.BindScalarInput("name", "DefaultFraction");
                    context.BindScalarInput("type", ScheduleType.Fraction);
                    Schedule schedule = new("DefaultFraction", type: ScheduleType.Fraction);
                    AssertScheduleDescriptor(schedule, expected!.Value, context);
                    AssertDefaultGraphNames(schedule, "DefaultFraction");
                    AssertSameNameTopology(
                        schedule,
                        new Schedule("DefaultFraction", type: ScheduleType.Fraction));
                    return Returned(
                        "native typed default graph has 365 distinct Fraction RuleSets",
                        "repeated native typed default construction has exact deterministic ordinal child names");
                }
            case "init.anonymous":
                {
                    context.BindScalarInput("name", null);
                    Schedule first = new(null);
                    Schedule second = new(null);
                    AssertScheduleDescriptor(first, expected!.Value, context);
                    Assert.Equal("anonymous", first.Name);
                    Assert.Equal(first.Name, second.Name);
                    AssertDefaultGraphNames(first, "anonymous");
                    AssertDefaultGraphNames(second, "anonymous");
                    for (int index = 0; index < Schedule.FixedLength; index++)
                    {
                        Assert.Equal(first[index].Name, second[index].Name);
                        Assert.Equal(first[index].Weekdays.Name, second[index].Weekdays.Name);
                        Assert.Equal(first[index].Weekends.Name, second[index].Weekends.Name);
                    }

                    return Returned(
                        "native null names normalize exactly to anonymous",
                        "repeated anonymous construction produces identical deterministic child names");
                }
            case "init.empty-name":
                context.BindScalarInput("name", string.Empty);
                Assert.Throws<ArgumentException>(() => new Schedule(string.Empty));
                return RaisedDomain("empty product name rejected");
            case "init.whitespace-name":
                context.BindScalarInput("name", "  ");
                Assert.Throws<ArgumentException>(() => new Schedule("  "));
                return RaisedDomain("whitespace-only product name rejected");
            case "init.surrounding-space-name":
                {
                    context.BindScalarInput("name", "  Named  ");
                    Schedule schedule = new("  Named  ");
                    Assert.Equal("Named", schedule.Name);
                    AssertDefaultGraphNames(schedule, "Named");
                    AssertSameNameTopology(schedule, new Schedule("  Named  "));
                    return Returned(
                        "native constructor trims surrounding whitespace to exact name Named",
                        "repeated trimmed-name construction has exact Named:default:NNN child names");
                }
            case "init.invalid-length":
                {
                    RuleSet rule = MakeRule("R", 0);
                    RuleSet[] rules = Enumerable.Repeat(rule, 364).ToArray();
                    context.BindInput("rulesets", rules);
                    Assert.Throws<ArgumentException>(() => new Schedule("BadLength", rules));
                    return RaisedDomain("invalid annual length rejected");
                }
            case "init.invalid-item":
                {
                    RuleSet rule = MakeRule("R", 0);
                    RuleSet[] items = Enumerable.Repeat(rule, 365).ToArray();
                    items[364] = null!;
                    context.BindInvalidRuleSetInput("rulesets", items);
                    Assert.Throws<ArgumentException>(() => new Schedule("BadItem", items));
                    return RaisedType("null/non-RuleSet item excluded by typed native boundary");
                }
            case "init.mixed-types":
                {
                    RuleSet[] items = Enumerable.Repeat(MakeRule("Real", 0), 364)
                        .Append(MakeRule("Fraction", 0.5, ScheduleType.Fraction))
                        .ToArray();
                    context.BindInput("rulesets", items);
                    Assert.Throws<ArgumentException>(() => new Schedule("Mixed", items));
                    return RaisedDomain("mixed RuleSet types rejected");
                }
            case "init.explicit-type-mismatch":
                {
                    RuleSet rule = MakeRule("Real", 0);
                    RuleSet[] rules = Enumerable.Repeat(rule, 365).ToArray();
                    context.BindInput("rulesets", rules);
                    context.BindScalarInput("type", ScheduleType.Fraction);
                    Assert.Throws<ArgumentException>(() =>
                        new Schedule("Mismatch", rules, ScheduleType.Fraction));
                    return RaisedDomain("explicit container type mismatch rejected");
                }
            case "init.supplied-list-alias":
                {
                    RuleSet original = MakeRule("Original", 0);
                    RuleSet replacement = MakeRule("Replacement", 1);
                    var items = Enumerable.Repeat(original, 365).ToList();
                    Schedule schedule = new("Aliased", items);
                    context.BindInput("original", original);
                    context.BindInput("replacement", replacement);
                    context.BindInputUsingFixtureAfter("rulesets", items);
                    context.BindInput("schedule", schedule);
                    ScheduleReferenceSnapshot before = CaptureSchedule(schedule);
                    items[0] = replacement;
                    Assert.Same(original, schedule[0]);
                    Assert.NotSame(replacement, schedule[0]);
                    AssertScheduleUnchanged(schedule, before);
                    Assert.True(DescriptorBoolean(expected!.Value.GetProperty("data_is_supplied_list")));
                    Assert.True(DescriptorBoolean(expected.Value.GetProperty("schedule_first_is_replacement")));
                    return Returned(
                        "native constructor copies the supplied 365-item container",
                        "caller list replacement leaves every native schedule reference unchanged");
                }
            default:
                throw Unknown(caseId);
        }
    }

    private static NativeCall ExecuteApply(string caseId, NativeCaseContext context)
    {
        RuleSet source = MakeRule("Source", 0);
        RuleSet overrideRule = caseId == "apply.type-unchecked"
            ? MakeRule("OnOff", 1, ScheduleType.OnOff)
            : MakeRule("Override", 1);
        Schedule schedule = Schedule.FromConstant("Apply", source);
        context.BindInput("source", source);
        context.BindInput("override", overrideRule);
        context.BindInput("schedule", schedule);
        ScheduleReferenceSnapshot before = CaptureSchedule(schedule);

        if (caseId == "apply.invalid-date")
        {
            Assert.ThrowsAny<ArgumentException>(() => schedule.Apply(overrideRule, "not-a-date", "0102"));
            AssertRuleRuns(schedule, (365, source));
            AssertScheduleUnchanged(schedule, before);
            return RaisedDomain(
                "native parser rejects not-a-date",
                "invalid date failure preserves all 365 source RuleSet references");
        }

        if (caseId == "apply.reversed-noop")
        {
            Assert.Throws<ArgumentException>(() => schedule.Apply(overrideRule, "0103", "0102"));
            AssertRuleRuns(schedule, (365, source));
            AssertScheduleUnchanged(schedule, before);
            return RaisedDomain(
                "native Apply rejects a 01/03..01/02 reversed range",
                "reversed-range failure preserves all 365 source RuleSet references");
        }

        if (caseId == "apply.type-unchecked")
        {
            Assert.Throws<ArgumentException>(() => schedule.Apply(overrideRule, "0102", "0102"));
            AssertRuleRuns(schedule, (365, source));
            AssertScheduleUnchanged(schedule, before);
            return RaisedDomain(
                "native Apply rejects an OnOff RuleSet for a Real Schedule",
                "type-mismatch failure preserves all 365 source RuleSet references");
        }

        Schedule result = caseId switch
        {
            "apply.inplace-inclusive-mmdd" => schedule.Apply(overrideRule, "0102", "0103"),
            "apply.noninplace-deepcopy" => schedule.Apply(overrideRule, "0102", "0103"),
            "apply.parse-yyyymmdd" => schedule.Apply(overrideRule, "20260102", "20260103"),
            "apply.parse-digit-pair" => schedule.Apply(overrideRule, "1/2", "1-3"),
            "apply.foreign-year-noop" => schedule.Apply(
                overrideRule,
                new DateTime(2025, 1, 2),
                new DateTime(2025, 1, 3)),
            "apply.outside-year-noop" => schedule.Apply(
                overrideRule,
                new DateTime(2027, 1, 1),
                new DateTime(2027, 12, 31)),
            _ => throw Unknown(caseId),
        };

        Assert.NotSame(schedule, result);
        AssertScheduleUnchanged(schedule, before);
        AssertRuleRuns(schedule, (365, source));
        Assert.Equal("Apply", result.Name);
        if (caseId == "apply.outside-year-noop")
        {
            AssertRuleRuns(result, (365, overrideRule));
        }
        else
        {
            AssertRuleRuns(result, (1, source), (2, overrideRule), (362, source));
        }

        return caseId switch
        {
            "apply.inplace-inclusive-mmdd" => Returned(
                "native Apply returns a distinct Schedule",
                "MMDD range produces exact RuleSet runs 1/2/362",
                "native source graph remains reference-identical"),
            "apply.noninplace-deepcopy" => Returned(
                "native Apply result reuses exact caller source and override RuleSets",
                "native source graph remains reference-identical"),
            "apply.parse-yyyymmdd" => Returned(
                "native parser normalizes 20260102..20260103 to exact RuleSet runs 1/2/362",
                "native source graph remains reference-identical"),
            "apply.parse-digit-pair" => Returned(
                "native parser accepts 1/2 and 1-3 as exact RuleSet runs 1/2/362",
                "native source graph remains reference-identical"),
            "apply.foreign-year-noop" => Returned(
                "native DateTime input normalizes foreign-year 01/02..01/03 to exact RuleSet runs 1/2/362",
                "native source graph remains reference-identical"),
            "apply.outside-year-noop" => Returned(
                "native DateTime input normalizes foreign-year 01/01..12/31 to 365 override references",
                "native source graph remains reference-identical"),
            _ => throw Unknown(caseId),
        };
    }

    private static NativeCall ExecuteDeepCopy(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        MethodInfo[] deepCopyMethods = typeof(Schedule)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(Schedule.DeepCopy))
            .ToArray();
        MethodInfo deepCopyMethod = Assert.Single(deepCopyMethods);
        Assert.Empty(deepCopyMethod.GetParameters());
        Assert.DoesNotContain(
            typeof(Schedule).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name == "__deepcopy__" ||
                (method.Name == nameof(Schedule.DeepCopy) && method.GetParameters().Length != 0));

        if (caseId == "deepcopy.memo-hit")
        {
            (Schedule source, RuleSet left, RuleSet middle) = ThreePeriodSchedule();
            context.BindInput("left", left);
            context.BindInput("middle", middle);
            context.BindInput("schedule", source);
            ScheduleReferenceSnapshot before = CaptureSchedule(source);
            Schedule copy = source.DeepCopy();
            Assert.NotSame(source, copy);
            Assert.Equal("S:COPY", copy.Name);
            Assert.Same(source[0], source[364]);
            Assert.NotSame(copy[0], copy[364]);
            AssertScheduleUnchanged(source, before);
            Assert.Equal("memo-sentinel", RequiredString(expected!.Value, "value"));
            return Returned(
                "native Schedule exposes exactly one parameterless DeepCopy method",
                "native Schedule exposes no memo or __deepcopy__ API",
                "native DeepCopy splits noncontiguous source aliases by compact period");
        }

        Schedule sourceSchedule;
        ScheduleReferenceSnapshot sourceBefore;
        Schedule result;
        if (caseId == "deepcopy.shared-period")
        {
            DaySchedule day = MakeDay("SharedDay", 0);
            RuleSet rule = new("SharedRule", day, day, monday: day, holiday: day);
            sourceSchedule = Schedule.FromConstant("Shared", rule);
            context.BindInput("day", day);
            context.BindInput("rule", rule);
            context.BindInput("schedule", sourceSchedule);
            sourceBefore = CaptureSchedule(sourceSchedule);
            result = sourceSchedule.DeepCopy();
        }
        else if (caseId == "deepcopy.noncontiguous-alias-split")
        {
            (sourceSchedule, RuleSet left, RuleSet middle) = ThreePeriodSchedule();
            context.BindInput("left", left);
            context.BindInput("middle", middle);
            context.BindInput("schedule", sourceSchedule);
            sourceBefore = CaptureSchedule(sourceSchedule);
            result = sourceSchedule.DeepCopy();
        }
        else
        {
            throw Unknown(caseId);
        }

        AssertScheduleDescriptor(result, expected!.Value, context);
        AssertScheduleUnchanged(sourceSchedule, sourceBefore);
        return Returned(
            "native DeepCopy result has the asserted case-scoped clone identity partition",
            "native DeepCopy leaves every source RuleSet and DaySchedule reference unchanged while the result uses isolated clones",
            "native Schedule exposes no memo overload");
    }

    private static NativeCall ExecuteAsType(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        context.BindScalarInput("type", ScheduleType.OnOff);
        if (caseId is "astype.outplace" or "astype.inplace-stale")
        {
            (Schedule source, RuleSet left, RuleSet middle) = ThreePeriodSchedule();
            context.BindInput("left", left);
            context.BindInput("middle", middle);
            context.BindInput("schedule", source);
            ScheduleReferenceSnapshot before = CaptureSchedule(source);
            Schedule result = source.AsType(ScheduleType.OnOff);
            AssertScheduleUnchanged(source, before);
            AssertRuleRuns(source, (2, left), (2, middle), (361, left));
            Assert.Equal(ScheduleType.OnOff, result.Type);
            Assert.All(result.RuleSets, rule => Assert.Equal(ScheduleType.OnOff, rule.Type));
            if (caseId == "astype.outplace")
            {
                AssertScheduleDescriptor(result, expected!.Value, context);
            }
            else
            {
                Assert.Equal("S", result.Name);
                List<ReferenceRun<RuleSet>> runs = ReferenceRunLengthEncode(result.RuleSets);
                Assert.Equal(new[] { 2, 2, 361 }, runs.Select(run => run.Count).ToArray());
                Assert.NotSame(runs[0].Value, runs[2].Value);
                Assert.Equal(new[] { "A:COPY", "B:COPY", "A:COPY" },
                    runs.Select(run => run.Value.Name).ToArray());
                Assert.All(runs, run =>
                {
                    Assert.Equal(ScheduleType.OnOff, run.Value.Type);
                    double expectedValue = run.Value.Name == "B:COPY" ? 1d : 0d;
                    Assert.Equal(expectedValue, run.Value.Minimum);
                    Assert.Equal(expectedValue, run.Value.Maximum);
                    Assert.NotSame(run.Value.Weekdays, run.Value.Weekends);
                    string sourceRuleName = run.Value.Name == "B:COPY" ? "B" : "A";
                    Assert.Equal($"{sourceRuleName}:weekdays:COPY", run.Value.Weekdays.Name);
                    Assert.Equal($"{sourceRuleName}:weekends:COPY", run.Value.Weekends.Name);
                    Assert.Equal(144, run.Value.Weekdays.Count);
                    Assert.Equal(144, run.Value.Weekends.Count);
                    Assert.All(run.Value.Weekdays, value => Assert.Equal(expectedValue, value));
                    Assert.All(run.Value.Weekends, value => Assert.Equal(expectedValue, value));
                    Assert.All(
                        RuleSlots(run.Value).Skip(2),
                        slot => Assert.Null(slot.Value));
                });
            }

            return Returned(caseId == "astype.inplace-stale"
                ? new[]
                {
                    "native AsType returns a new OnOff Schedule while the Real source graph stays reference-identical",
                    "native stale-source scenario yields exact converted runs 2/2/361 with three distinct period clones",
                }
                : new[]
                {
                    "native out-of-place conversion matches the asserted clone identity topology",
                    "native Real source graph stays reference-identical after conversion",
                });
        }

        RuleSet good = MakeRule("GOOD", 0);
        RuleSet bad = MakeRule("BAD", 2);
        Schedule partial = new("Partial", new[] { good }.Concat(Enumerable.Repeat(bad, 364)));
        context.BindInput("good", good);
        context.BindInput("bad", bad);
        context.BindInput("schedule", partial);
        ScheduleReferenceSnapshot partialBefore = CaptureSchedule(partial);
        Assert.ThrowsAny<ArgumentException>(() => partial.AsType(ScheduleType.OnOff));
        AssertScheduleUnchanged(partial, partialBefore);
        AssertRuleRuns(partial, (1, good), (364, bad));
        return RaisedDomain(caseId switch
        {
            "astype.inplace-partial" => "native conversion rejects the value 2 for OnOff after a valid first period; all 365 source references remain unchanged",
            "astype.outplace-failure-atomic" => "native out-of-place conversion failure leaves the exact 1/364 source identity runs unchanged",
            _ => throw Unknown(caseId),
        });
    }

    private static NativeCall ExecuteMetric(string caseId, JsonElement expected, NativeCaseContext context)
    {
        double[] values = caseId switch
        {
            "average.catastrophic" or "integral.catastrophic" or "positive-average.catastrophic" =>
                new[] { 1e16, 1d, -1e16 }.Concat(Enumerable.Repeat(0d, 141)).ToArray(),
            "average.minimum-subnormal" or "integral.minimum-subnormal" or "positive-average.minimum-subnormal" =>
                Enumerable.Repeat(BitConverter.Int64BitsToDouble(1), 144).ToArray(),
            "average.negative-zero" => Enumerable.Repeat(-0d, 144).ToArray(),
            "integral.overflow" => Enumerable.Repeat(double.MaxValue, 144).ToArray(),
            "positive-average.none" => Enumerable.Range(0, 144).Select(index => index % 2 == 0 ? -1d : 0d).ToArray(),
            _ => throw Unknown(caseId),
        };
        DaySchedule day = new("MetricDay", values, ScheduleType.Real);
        RuleSet rule = new("MetricRule", day, day);
        Schedule schedule = Schedule.FromConstant("Metric", rule);
        context.BindInput("day", day);
        context.BindInput("rule", rule);
        context.BindInput("schedule", schedule);
        double actual = caseId.StartsWith("average.", StringComparison.Ordinal)
            ? schedule.Average
            : caseId.StartsWith("integral.", StringComparison.Ordinal)
                ? schedule.Integral
                : schedule.PositiveAverage;
        AssertScalarDescriptor(actual, expected);
        return Returned("binary64/nonfinite result matches CPython 3.12 compensated sum");
    }

    private static NativeCall ExecuteExtrema(string caseId, JsonElement expected, NativeCaseContext context)
    {
        bool minimum = caseId.StartsWith("min.", StringComparison.Ordinal);
        RuleSet rule;
        if (caseId.EndsWith("negative-zero", StringComparison.Ordinal))
        {
            rule = MakeRule("NegativeZero", -0d);
        }
        else if (caseId.EndsWith("unused-holiday", StringComparison.Ordinal))
        {
            DaySchedule day = MakeDay("Base", 1);
            DaySchedule holiday = MakeDay("UnusedHoliday", minimum ? -999 : 999);
            rule = new RuleSet("HolidayExtrema", day, day, holiday: holiday);
        }
        else
        {
            throw Unknown(caseId);
        }

        Schedule schedule = Schedule.FromConstant("Extrema", rule);
        if (caseId.EndsWith("unused-holiday", StringComparison.Ordinal))
        {
            context.BindInput("base", rule.Weekdays);
            context.BindInput("holiday", rule.Holiday!);
        }

        context.BindInput("rule", rule);
        context.BindInput("schedule", schedule);
        AssertScalarDescriptor(minimum ? schedule.Minimum : schedule.Maximum, expected);
        return Returned("extremum value and signed-zero bits match Python");
    }

    private static NativeCall ExecuteClip(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        RuleSet sourceRule = MakeRule("ClipRule", 0.5);
        Schedule source = Schedule.FromConstant("ClipSchedule", sourceRule);
        context.BindInput("source", sourceRule);
        ScheduleReferenceSnapshot sourceBefore = CaptureSchedule(source);
        if (caseId == "clip.min-greater-than-max")
        {
            context.BindInput("schedule", source);
            Assert.Throws<ArgumentException>(() => source.Clip(0.8, 0.2));
            AssertScheduleUnchanged(source, sourceBefore);
            AssertRuleRuns(source, (365, sourceRule));
            return RaisedDomain(
                "native Clip rejects minimum 0.8 above maximum 0.2",
                "inverted-bound failure preserves the complete homogeneous source graph");
        }

        if (caseId == "clip.inplace-partial")
        {
            RuleSet onOff = MakeRule("OnOff", 1, ScheduleType.OnOff);
            context.BindInput("bad", onOff);
            DaySchedule sourceWeekdays = sourceRule.Weekdays;
            DaySchedule sourceWeekends = sourceRule.Weekends;
            DaySchedule badWeekdays = onOff.Weekdays;
            DaySchedule badWeekends = onOff.Weekends;
            RuleSet[] heterogeneous = new[] { sourceRule, onOff }
                .Concat(Enumerable.Repeat(sourceRule, 363))
                .ToArray();
            Assert.Throws<ArgumentException>(() => new Schedule("ClipSchedule", heterogeneous));
            Schedule uncheckedHeterogeneous = Schedule.FromConstant("ClipSchedule", sourceRule);
            SetRuleSetsForTest(uncheckedHeterogeneous, heterogeneous);
            context.BindInput("schedule", uncheckedHeterogeneous);
            ScheduleReferenceSnapshot heterogeneousBefore = CaptureSchedule(uncheckedHeterogeneous);

            Assert.ThrowsAny<ArgumentException>(() => uncheckedHeterogeneous.Clip(0.2, 0.8));
            AssertScheduleUnchanged(uncheckedHeterogeneous, heterogeneousBefore);
            AssertRuleRuns(uncheckedHeterogeneous, (1, sourceRule), (1, onOff), (363, sourceRule));
            Assert.Same(sourceRule, heterogeneous[0]);
            Assert.Same(onOff, heterogeneous[1]);
            Assert.Same(sourceRule, heterogeneous[364]);
            Assert.Same(sourceWeekdays, sourceRule.Weekdays);
            Assert.Same(sourceWeekends, sourceRule.Weekends);
            Assert.Same(badWeekdays, onOff.Weekdays);
            Assert.Same(badWeekends, onOff.Weekends);
            Assert.All(sourceRule.Weekdays, value => Assert.Equal(0.5, value));
            Assert.All(onOff.Weekdays, value => Assert.Equal(1d, value));
            return RaisedDomain(
                "native public Schedule construction rejects the heterogeneous Real/OnOff fixture graph",
                "test-only exact fixture topology reaches native Clip with valid 0.2..0.8 bounds",
                "native Clip failure preserves the exact 1/1/363 RuleSet runs and all caller child references");
        }

        context.BindInput("schedule", source);

        Schedule result = caseId switch
        {
            "clip.outplace-bounds" => source.Clip(0.6, 0.8, "Clipped"),
            "clip.outplace-lower-only" => source.Clip(minimum: 0.6),
            "clip.outplace-no-bounds-copy" => source.Clip(),
            "clip.outplace-upper-only" => source.Clip(maximum: 0.4),
            "clip.empty-name-default" => source.Clip(0, 1, string.Empty),
            "clip.inplace-distinct" => source.Clip(0.2, 0.8),
            _ => throw Unknown(caseId),
        };
        Assert.NotSame(source, result);
        AssertScheduleUnchanged(source, sourceBefore);
        AssertRuleRuns(source, (365, sourceRule));
        Assert.NotSame(sourceRule, result[0]);
        Assert.Equal(0.5, source[0].Weekdays[0]);
        if (caseId != "clip.inplace-distinct")
        {
            AssertScheduleDescriptor(result, expected!.Value, context);
        }
        else
        {
            RuleSet clipped = result[0];
            AssertRuleRuns(result, (365, clipped));
            Assert.Equal("ClipSchedule:CLIP", result.Name);
            Assert.Equal("ClipRule:CLIP", clipped.Name);
            Assert.NotSame(sourceRule.Weekdays, clipped.Weekdays);
            Assert.NotSame(sourceRule.Weekends, clipped.Weekends);
            Assert.NotSame(clipped.Weekdays, clipped.Weekends);
            Assert.Equal("ClipRule:weekdays:CLIP", clipped.Weekdays.Name);
            Assert.Equal("ClipRule:weekends:CLIP", clipped.Weekends.Name);
            Assert.Equal(144, clipped.Weekdays.Count);
            Assert.Equal(144, clipped.Weekends.Count);
            Assert.All(clipped.Weekdays, value => Assert.Equal(0.5, value));
            Assert.All(clipped.Weekends, value => Assert.Equal(0.5, value));
            Assert.All(RuleSlots(clipped).Skip(2), slot => Assert.Null(slot.Value));
        }

        return Returned(caseId == "clip.inplace-distinct"
            ? new[]
            {
                "native Clip returns one distinct cloned RuleSet reused for all 365 result dates",
                "native clipped children are distinct 144-value clones with exact deterministic :CLIP names",
                "native source graph remains reference-identical",
            }
            : new[]
            {
                "native clipped output equals the asserted full graph and logical values",
                "native source graph remains reference-identical",
            });
    }

    private static void SetRuleSetsForTest(Schedule schedule, IReadOnlyList<RuleSet> ruleSets)
    {
        Assert.Equal(Schedule.FixedLength, ruleSets.Count);
        FieldInfo? field = typeof(Schedule).GetField(
            "<RuleSets>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.True(field.IsInitOnly);
        Assert.Equal(typeof(IReadOnlyList<RuleSet>), field.FieldType);
        field.SetValue(schedule, Array.AsReadOnly(ruleSets.ToArray()));
        Assert.True(schedule.RuleSets.Zip(ruleSets, ReferenceEquals).All(item => item));
    }

    private static NativeCall ExecuteCompactize(string caseId, JsonElement expected, NativeCaseContext context)
    {
        Schedule schedule;
        RuleSet? left = null;
        RuleSet? right = null;
        if (caseId == "compactize.default-distinct")
        {
            schedule = new Schedule("CompactDefault");
        }
        else
        {
            left = MakeRule("A", 0);
            right = MakeRule("B", 0);
            IEnumerable<RuleSet> values = caseId switch
            {
                "compactize.identity-runs" =>
                    Enumerable.Repeat(left, 2)
                        .Concat(Enumerable.Repeat(right, 2))
                        .Concat(Enumerable.Repeat(left, 361)),
                "compactize.equal-distinct" =>
                    new[] { left }.Concat(Enumerable.Repeat(right, 364)),
                "compactize.full-run" => Enumerable.Repeat(left, 365),
                _ => throw Unknown(caseId),
            };
            schedule = new Schedule("Compact", values);
            context.BindInput("left", left);
            context.BindInput("right", right);
        }

        context.BindInput("schedule", schedule);
        ScheduleReferenceSnapshot before = CaptureSchedule(schedule);
        IReadOnlyList<SchedulePeriod> compact = schedule.Compactize();
        AssertCompactDescriptor(compact, expected, context);
        AssertScheduleUnchanged(schedule, before);
        if (left is not null)
        {
            Assert.Contains(compact, period => ReferenceEquals(period.RuleSet, left));
            if (caseId is "compactize.identity-runs" or "compactize.equal-distinct")
            {
                Assert.Contains(compact, period => ReferenceEquals(period.RuleSet, right));
            }
        }

        return Returned(caseId switch
        {
            "compactize.default-distinct" => "native compactization retains 365 identity periods from 365 distinct default RuleSets",
            "compactize.equal-distinct" => "native compactization keeps value-equal but reference-distinct adjacent RuleSets as two periods",
            "compactize.full-run" => "native compactization returns one 01/01..12/31 period with the exact caller RuleSet",
            "compactize.identity-runs" => "native compactization returns exact 2/2/361 reference runs including the reused caller RuleSet",
            _ => throw Unknown(caseId),
        }, "native compactization leaves every source reference unchanged");
    }

    private static NativeCall ExecuteDaySchedules(string caseId, JsonElement expected, NativeCaseContext context)
    {
        DaySchedule weekdays = MakeDay("Weekdays", 1);
        DaySchedule weekends = MakeDay("Weekends", 2);
        DaySchedule monday = MakeDay("Monday", 3);
        DaySchedule sunday = MakeDay("Sunday", 4);
        DaySchedule holiday = MakeDay("Holiday", 999);
        RuleSet rule = new(
            "CalendarRule",
            weekdays,
            weekends,
            monday: monday,
            sunday: sunday,
            holiday: holiday);
        Schedule schedule = Schedule.FromConstant("Calendar", rule);
        context.BindInput("holiday", holiday);
        context.BindInput("monday", monday);
        context.BindInput("rule", rule);
        context.BindInput("schedule", schedule);
        context.BindInput("sunday", sunday);
        context.BindInput("weekdays", weekdays);
        context.BindInput("weekends", weekends);
        ScheduleReferenceSnapshot before = CaptureSchedule(schedule);

        static DaySchedule ExpectedDay(
            DateTime date,
            DaySchedule weekdayValue,
            DaySchedule weekendValue,
            DaySchedule mondayValue,
            DaySchedule sundayValue) => date.DayOfWeek switch
            {
                DayOfWeek.Monday => mondayValue,
                DayOfWeek.Saturday => weekendValue,
                DayOfWeek.Sunday => sundayValue,
                _ => weekdayValue,
            };

        if (caseId == "dayschedules.fresh-list")
        {
            IReadOnlyList<DaySchedule> first = schedule.DaySchedules;
            IReadOnlyList<DaySchedule> second = schedule.DaySchedules;
            Assert.NotSame(first, second);
            Assert.True(first.Zip(second, ReferenceEquals).All(item => item));
            Assert.True(DescriptorBoolean(expected.GetProperty("lists_are_distinct")));
            Assert.True(DescriptorBoolean(expected.GetProperty("same_day_references")));
            AssertDaySequenceDescriptor(first, expected.GetProperty("first"), context);
            AssertDaySequenceDescriptor(second, expected.GetProperty("second"), context);
            for (int index = 0; index < Schedule.FixedLength; index++)
            {
                DaySchedule expectedDay = ExpectedDay(
                    Schedule.TimeTuple[index], weekdays, weekends, monday, sunday);
                Assert.Same(expectedDay, first[index]);
                Assert.Same(expectedDay, second[index]);
            }

            IList<DaySchedule> mutableView = Assert.IsAssignableFrom<IList<DaySchedule>>(first);
            Assert.True(mutableView.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => mutableView[0] = holiday);
            Assert.Throws<NotSupportedException>(() => mutableView.Add(holiday));
            Assert.Throws<NotSupportedException>(() => mutableView.Remove(weekdays));
            Assert.Throws<NotSupportedException>(() => mutableView.RemoveAt(0));
            Assert.Throws<NotSupportedException>(() => mutableView.Clear());
            AssertScheduleUnchanged(schedule, before);
            Assert.Same(weekdays, rule.Weekdays);
            Assert.Same(weekends, rule.Weekends);
            Assert.Same(monday, rule.Monday);
            Assert.Same(sunday, rule.Sunday);
            Assert.Same(holiday, rule.Holiday);
            return Returned(
                "each native DaySchedules access returns a fresh read-only 365-item collection",
                "all 365 entries retain exact caller DaySchedule references by 2026 weekday",
                "set/add/remove/remove-at/clear mutations are rejected and source child references remain unchanged");
        }

        Assert.Equal("dayschedules.weekday-overrides", caseId);
        IReadOnlyList<DaySchedule> actual = schedule.DaySchedules;
        AssertDaySequenceDescriptor(actual, expected, context);
        for (int index = 0; index < Schedule.FixedLength; index++)
        {
            Assert.Same(
                ExpectedDay(Schedule.TimeTuple[index], weekdays, weekends, monday, sunday),
                actual[index]);
        }

        Assert.DoesNotContain(holiday, actual);
        AssertScheduleUnchanged(schedule, before);
        return Returned(
            "native 2026 calendar resolves every Monday/Sunday override and weekday/weekend fallback by exact reference",
            "unused Holiday is absent from all 365 returned entries",
            "source RuleSet and all supplied DaySchedule references remain unchanged");
    }

    private static NativeCall ExecuteFromCompact(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        RuleSet left = MakeRule("A", 0);
        RuleSet right = MakeRule("B", 1);
        context.BindInput("left", left);
        context.BindInput("right", right);
        RuleSetReferenceSnapshot leftBefore = CaptureRule(left);
        RuleSetReferenceSnapshot rightBefore = CaptureRule(right);
        if (caseId == "from-compact.empty")
        {
            SchedulePeriod[] compact = Array.Empty<SchedulePeriod>();
            context.BindInput("compact", compact);
            Assert.Throws<ArgumentException>(() => Schedule.FromCompact("FromCompact", compact));
            AssertRuleUnchanged(leftBefore);
            AssertRuleUnchanged(rightBefore);
            return RaisedDomain(
                "native FromCompact rejects an empty period list",
                "empty-input failure preserves both caller RuleSet graphs");
        }

        if (caseId == "from-compact.mixed-type")
        {
            RuleSet fraction = MakeRule("Fraction", 0.5, ScheduleType.Fraction);
            context.BindInput("fraction", fraction);
            SchedulePeriod[] compact =
            {
                new("0101", "0101", left),
                new("0102", "0102", fraction),
            };
            context.BindInput("compact", compact);
            Assert.Throws<ArgumentException>(() => Schedule.FromCompact(
                "FromCompact",
                compact));
            AssertRuleUnchanged(leftBefore);
            AssertRuleUnchanged(rightBefore);
            return RaisedDomain(
                "native FromCompact rejects Real/Fraction period mixing",
                "mixed-type failure preserves every caller period and child reference");
        }

        if (caseId == "from-compact.leap-day")
        {
            context.AdaptUnrepresentablePeriodInput(
                "compact",
                "0229",
                "0301",
                left,
                "02/29 cannot be represented by the fixed-2026 native SchedulePeriod value object.");
            Assert.ThrowsAny<ArgumentException>(() => new SchedulePeriod("0229", "0301", left));
            AssertRuleUnchanged(leftBefore);
            AssertRuleUnchanged(rightBefore);
            return RaisedDomain(
                "native SchedulePeriod rejects 02/29 on the fixed 2026 calendar",
                "leap-day rejection preserves both caller RuleSet graphs");
        }

        if (caseId == "from-compact.reversed-noop")
        {
            context.AdaptUnrepresentablePeriodInput(
                "compact",
                "0103",
                "0102",
                left,
                "A reversed interval cannot be represented by the validated native SchedulePeriod value object.");
            Assert.Throws<ArgumentException>(() => new SchedulePeriod("0103", "0102", left));
            AssertRuleUnchanged(leftBefore);
            AssertRuleUnchanged(rightBefore);
            return RaisedDomain(
                "native SchedulePeriod rejects 01/03..01/02",
                "reversed-period rejection preserves both caller RuleSet graphs");
        }

        RuleSet? equal = null;
        RuleSetReferenceSnapshot? equalBefore = null;
        SchedulePeriod[] compactInput;
        Schedule result;
        switch (caseId)
        {
            case "from-compact.single-gap":
                compactInput = new[] { new SchedulePeriod("0102", "0103", left) };
                break;
            case "from-compact.same-ref-adjacent":
                compactInput = new[]
                {
                        new SchedulePeriod("0101", "0102", left),
                        new SchedulePeriod("0103", "0104", left),
                };
                break;
            case "from-compact.distinct-equal-adjacent":
                equal = MakeRule("A", 0);
                equalBefore = CaptureRule(equal);
                context.BindInput("equal", equal);
                compactInput = new[]
                {
                        new SchedulePeriod("0101", "0102", left),
                        new SchedulePeriod("0103", "0104", equal),
                };
                break;
            case "from-compact.overlap-later-wins":
                compactInput = new[]
                {
                        new SchedulePeriod("0101", "0104", left),
                        new SchedulePeriod("0103", "0105", right),
                };
                break;
            case "from-compact.outside-noop":
                compactInput = new[]
                {
                        new SchedulePeriod(
                            new DateTime(2027, 1, 1),
                            new DateTime(2027, 12, 31),
                            left),
                };
                break;
            default:
                throw Unknown(caseId);
        }

        context.BindInput("compact", compactInput);
        result = Schedule.FromCompact("FromCompact", compactInput);
        if (caseId != "from-compact.outside-noop")
        {
            AssertScheduleDescriptor(result, expected!.Value, context);
        }
        if (caseId == "from-compact.single-gap")
        {
            Assert.Equal(364, UniqueReferenceCount(result.RuleSets));
            Assert.Equal(363, UniqueReferenceCount(result.RuleSets.Where(item => !ReferenceEquals(item, left))));
        }
        if (caseId == "from-compact.same-ref-adjacent")
        {
            Assert.Same(left, result[January(1)]);
            Assert.Same(left, result[January(4)]);
        }
        if (caseId == "from-compact.distinct-equal-adjacent")
        {
            Assert.Same(left, result[January(1)]);
            Assert.Same(equal, result[January(3)]);
            Assert.NotSame(left, equal);
        }
        if (caseId == "from-compact.overlap-later-wins")
        {
            Assert.Same(left, result[January(2)]);
            Assert.Same(right, result[January(3)]);
            Assert.Same(right, result[January(5)]);
        }
        if (caseId == "from-compact.outside-noop")
        {
            AssertRuleRuns(result, (365, left));
            Assert.Single(result.Compactize());
        }

        Schedule repeated = Schedule.FromCompact("FromCompact", compactInput);
        AssertSameNameTopology(result, repeated);
        var callerRules = new List<RuleSet> { left, right };
        if (equal is not null)
        {
            callerRules.Add(equal);
        }
        for (int index = 0; index < Schedule.FixedLength; index++)
        {
            RuleSet rule = result[index];
            if (callerRules.Any(caller => ReferenceEquals(caller, rule)))
            {
                continue;
            }

            string expectedRuleName = $"FromCompact:default:{index + 1:D3}";
            Assert.Equal(expectedRuleName, rule.Name);
            Assert.Equal($"{expectedRuleName}:day", rule.Weekdays.Name);
            Assert.Equal($"{expectedRuleName}:day", rule.Weekends.Name);
            Assert.NotSame(rule.Weekdays, rule.Weekends);
        }

        AssertRuleUnchanged(leftBefore);
        AssertRuleUnchanged(rightBefore);
        if (equalBefore is not null)
        {
            AssertRuleUnchanged(equalBefore);
        }

        return Returned(caseId switch
        {
            "from-compact.single-gap" => "native uncovered dates create 363 distinct default RuleSets while preserving the exact caller for 01/02..01/03",
            "from-compact.same-ref-adjacent" => "native adjacent periods retain the exact same caller RuleSet reference across 01/01..01/04",
            "from-compact.distinct-equal-adjacent" => "native value-equal adjacent inputs retain two exact distinct caller RuleSet references",
            "from-compact.overlap-later-wins" => "native overlapping input applies the exact later caller RuleSet on 01/03..01/05",
            "from-compact.outside-noop" => "native foreign-year full window normalizes by month/day to one 365-day caller-reference run",
            _ => throw Unknown(caseId),
        },
        "repeated native FromCompact construction has identical exact deterministic gap names",
        "native FromCompact leaves all caller RuleSet child references unchanged");
    }

    private static NativeCall ExecuteFromConstant(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        if (caseId == "from-constant.real-nan")
        {
            context.BindScalarInput("type", ScheduleType.Real);
            context.BindScalarInput("value", double.NaN);
            Assert.ThrowsAny<ArgumentException>(() =>
                Schedule.FromConstant("NaN", double.NaN, ScheduleType.Real));
            return RaisedDomain(
                "native FromConstant rejects NaN for a Real DaySchedule",
                "nonfinite rejection occurs before any Schedule is returned");
        }

        if (caseId == "from-constant.unsupported-object")
        {
            object unsupportedValue = new();
            context.BindScalarInput("type", ScheduleType.Real);
            context.BindScalarInput("value", unsupportedValue);
            Assert.ThrowsAny<ArgumentException>(() =>
                Schedule.FromConstant("Unsupported", unsupportedValue, ScheduleType.Real));
            return RaisedType(
                "native generic FromConstant rejects System.Object as a schedule operand",
                "unsupported operand rejection occurs before any Schedule is returned");
        }

        DaySchedule? callerDay = null;
        RuleSet? callerRule = null;
        Func<Schedule> construct;
        switch (caseId)
        {
            case "from-constant.scalar":
                context.BindScalarInput("type", ScheduleType.Fraction);
                context.BindScalarInput("value", 0.25d);
                construct = () => Schedule.FromConstant("Scalar", 0.25, ScheduleType.Fraction);
                break;
            case "from-constant.bool":
                context.BindScalarInput("value", true);
                construct = () => Schedule.FromConstant("Bool", true);
                break;
            case "from-constant.anonymous":
                context.BindScalarInput("name", null);
                context.BindScalarInput("value", 0.25d);
                construct = () => Schedule.FromConstant(null, 0.25);
                break;
            case "from-constant.surrounding-space-name":
                context.BindScalarInput("name", "  Scalar  ");
                context.BindScalarInput("type", ScheduleType.Fraction);
                context.BindScalarInput("value", 0.25d);
                construct = () => Schedule.FromConstant("  Scalar  ", 0.25, ScheduleType.Fraction);
                break;
            case "from-constant.day-explicit-type-ignored":
                callerDay = MakeDay("FractionDay", 0.25, ScheduleType.Fraction);
                context.BindInput("day", callerDay);
                context.BindScalarInput("explicit_type", ScheduleType.Real);
                construct = () => Schedule.FromConstant("Day", callerDay, ScheduleType.Real);
                break;
            case "from-constant.ruleset-explicit-type-ignored":
                callerDay = MakeDay("FractionDay", 0.25, ScheduleType.Fraction);
                callerRule = new RuleSet("FractionRule", callerDay, callerDay);
                context.BindInput("rule", callerRule);
                context.BindScalarInput("explicit_type", ScheduleType.Real);
                construct = () => Schedule.FromConstant("Rule", callerRule, ScheduleType.Real);
                break;
            default:
                throw Unknown(caseId);
        }

        Schedule result = construct();
        Schedule repeated = construct();
        if (caseId != "from-constant.surrounding-space-name")
        {
            AssertScheduleDescriptor(result, expected!.Value, context);
        }
        Assert.Equal(result.Name, repeated.Name);
        Assert.Equal(result[0].Name, repeated[0].Name);
        Assert.Equal(result[0].Weekdays.Name, repeated[0].Weekdays.Name);
        Assert.Equal(result[0].Weekends.Name, repeated[0].Weekends.Name);
        AssertRuleRuns(result, (365, result[0]));
        AssertRuleRuns(repeated, (365, repeated[0]));

        if (caseId is "from-constant.scalar" or "from-constant.surrounding-space-name")
        {
            Assert.Equal("Scalar", result.Name);
            Assert.Equal("Scalar:ruleset", result[0].Name);
            Assert.Equal("Scalar:ruleset:day", result[0].Weekdays.Name);
            Assert.Equal("Scalar:ruleset:day", result[0].Weekends.Name);
            Assert.NotSame(result[0].Weekdays, result[0].Weekends);
        }
        if (caseId == "from-constant.bool")
        {
            Assert.Equal("Bool", result.Name);
            Assert.Equal("Bool:ruleset", result[0].Name);
            Assert.Equal("Bool:ruleset:day", result[0].Weekdays.Name);
            Assert.Equal("Bool:ruleset:day", result[0].Weekends.Name);
            Assert.NotSame(result[0].Weekdays, result[0].Weekends);
        }
        if (caseId == "from-constant.anonymous")
        {
            Assert.Equal("anonymous", result.Name);
            Assert.Equal("anonymous:ruleset", result[0].Name);
            Assert.Equal("anonymous:ruleset:day", result[0].Weekdays.Name);
            Assert.Equal("anonymous:ruleset:day", result[0].Weekends.Name);
            Assert.NotSame(result[0].Weekdays, result[0].Weekends);
        }
        if (caseId == "from-constant.day-explicit-type-ignored")
        {
            Assert.Equal("Day", result.Name);
            Assert.Equal("Day:ruleset", result[0].Name);
            Assert.Same(callerDay, result[0].Weekdays);
            Assert.Same(callerDay, result[0].Weekends);
            Assert.Equal(ScheduleType.Fraction, result.Type);
        }
        if (caseId == "from-constant.ruleset-explicit-type-ignored")
        {
            Assert.Equal("Rule", result.Name);
            Assert.Same(callerRule, result[0]);
            Assert.Same(callerDay, result[0].Weekdays);
            Assert.Same(callerDay, result[0].Weekends);
            Assert.Equal(ScheduleType.Fraction, result.Type);
        }

        return Returned(caseId switch
        {
            "from-constant.scalar" => "native scalar factory uses exact Scalar / Scalar:ruleset / Scalar:ruleset:day names and two distinct day objects",
            "from-constant.bool" => "native bool factory uses exact Bool / Bool:ruleset / Bool:ruleset:day names and two distinct day objects",
            "from-constant.anonymous" => "native null-name factory uses exact anonymous / anonymous:ruleset / anonymous:ruleset:day names",
            "from-constant.surrounding-space-name" => "native factory trims the name before deriving exact Scalar child names",
            "from-constant.day-explicit-type-ignored" => "native DaySchedule overload creates exact Day:ruleset wrapper and aliases the caller in both slots",
            "from-constant.ruleset-explicit-type-ignored" => "native RuleSet overload reuses the exact caller RuleSet and ignores the explicit Real type",
            _ => throw Unknown(caseId),
        }, "repeated native construction produces the same exact deterministic name topology");
    }

    private static NativeCall ExecuteFromWindows(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        if (caseId == "from-windows.leap-day")
        {
            context.BindScalarInput("default", 0.1d);
            context.BindScalarInput("type", ScheduleType.Fraction);
            context.AdaptUnrepresentableWindowInput(
                "windows",
                "0229",
                "0301",
                0.5d,
                "02/29 cannot be represented by the fixed-2026 native ScheduleValueWindow value object.");
            Assert.ThrowsAny<ArgumentException>(() => new ScheduleValueWindow("0229", "0301", 0.5));
            return RaisedDomain(
                "native ScheduleValueWindow rejects 02/29 on the fixed 2026 calendar",
                "leap-day rejection occurs before FromWindows creates a Schedule");
        }
        if (caseId == "from-windows.reversed-noop")
        {
            context.BindScalarInput("default", 0.1d);
            context.BindScalarInput("type", ScheduleType.Fraction);
            context.AdaptUnrepresentableWindowInput(
                "windows",
                "0103",
                "0102",
                0.5d,
                "A reversed interval cannot be represented by the validated native ScheduleValueWindow value object.");
            Assert.Throws<ArgumentException>(() => new ScheduleValueWindow("0103", "0102", 0.5));
            return RaisedDomain(
                "native ScheduleValueWindow rejects 01/03..01/02",
                "reversed-window rejection occurs before FromWindows creates a Schedule");
        }
        if (caseId == "from-windows.scalar-positive-infinity")
        {
            ScheduleValueWindow[] windows =
            {
                new("0102", "0103", double.PositiveInfinity),
            };
            context.BindScalarInput("default", 0d);
            context.BindScalarInput("type", ScheduleType.Real);
            context.BindWindowsInput("windows", windows);
            Assert.ThrowsAny<ArgumentException>(() => Schedule.FromWindows(
                "InfiniteWindow",
                0d,
                windows,
                ScheduleType.Real));
            return RaisedDomain(
                "native FromWindows rejects positive infinity in a Real scalar window",
                "nonfinite-window rejection returns no Schedule");
        }
        if (caseId == "from-windows.unsupported-object")
        {
            object unsupported = new();
            ScheduleValueWindow[] windows =
            {
                new("0102", "0103", unsupported),
            };
            context.BindScalarInput("default", 0.1d);
            context.BindScalarInput("override", unsupported);
            context.BindWindowsInput("windows", windows);
            Assert.ThrowsAny<ArgumentException>(() => Schedule.FromWindows(
                "Unsupported",
                0.1,
                windows));
            return RaisedType(
                "native FromWindows rejects System.Object as a window operand",
                "unsupported-window rejection returns no Schedule");
        }
        if (caseId == "from-windows.type-mismatch")
        {
            RuleSet mismatch = MakeRule("Mismatch", 1, ScheduleType.OnOff);
            ScheduleValueWindow[] windows =
            {
                new("0102", "0103", mismatch),
            };
            context.BindScalarInput("default", 0.1d);
            context.BindInput("override", mismatch);
            context.BindScalarInput("type", ScheduleType.Fraction);
            context.BindWindowsInput("windows", windows);
            RuleSetReferenceSnapshot mismatchBefore = CaptureRule(mismatch);
            Assert.ThrowsAny<ArgumentException>(() => Schedule.FromWindows(
                "Mismatch",
                0.1,
                windows,
                ScheduleType.Fraction));
            AssertRuleUnchanged(mismatchBefore);
            return RaisedDomain(
                "native FromWindows rejects an OnOff override against a Fraction default",
                "type-mismatch failure preserves the caller override RuleSet graph");
        }

        Schedule result;
        Func<Schedule> reconstruct;
        switch (caseId)
        {
            case "from-windows.empty":
                {
                    ScheduleValueWindow[] windows = Array.Empty<ScheduleValueWindow>();
                    context.BindScalarInput("default", 0.1d);
                    context.BindScalarInput("type", ScheduleType.Fraction);
                    context.BindWindowsInput("windows", windows);
                    reconstruct = () => Schedule.FromWindows(
                        "EmptyWindows", 0.1, windows, ScheduleType.Fraction);
                    result = reconstruct();
                    AssertScalarWrapper(
                        result[January(1)],
                        "EmptyWindows:ruleset",
                        0.1,
                        ScheduleType.Fraction);
                    AssertRuleRuns(result, (365, result[January(1)]));
                    break;
                }
            case "from-windows.repeated-day-wrappers":
                {
                    DaySchedule day = MakeDay("RepeatedDay", 0.5, ScheduleType.Fraction);
                    ScheduleValueWindow[] windows =
                    {
                        new("0102", "0102", day),
                        new("0104", "0104", day),
                    };
                    context.BindScalarInput("default", 0.1d);
                    context.BindInput("override", day);
                    context.BindScalarInput("type", ScheduleType.Fraction);
                    context.BindWindowsInput("windows", windows);
                    reconstruct = () => Schedule.FromWindows(
                        "RepeatedDayWindows", 0.1, windows,
                        ScheduleType.Fraction);
                    result = reconstruct();
                    AssertScalarWrapper(
                        result[January(1)],
                        "RepeatedDayWindows:ruleset",
                        0.1,
                        ScheduleType.Fraction);
                    Assert.NotSame(result[January(2)], result[January(4)]);
                    AssertDayWrapper(
                        result[January(2)],
                        "RepeatedDayWindows:window:001",
                        day);
                    AssertDayWrapper(
                        result[January(4)],
                        "RepeatedDayWindows:window:002",
                        day);
                    break;
                }
            case "from-windows.repeated-scalar-wrappers":
                {
                    ScheduleValueWindow[] windows =
                    {
                        new("0102", "0102", 0.5),
                        new("0104", "0104", 0.5),
                    };
                    context.BindScalarInput("default", 0.1d);
                    context.BindScalarInput("type", ScheduleType.Fraction);
                    context.BindWindowsInput("windows", windows);
                    reconstruct = () => Schedule.FromWindows(
                        "RepeatedScalarWindows", 0.1, windows, ScheduleType.Fraction);
                    result = reconstruct();
                    AssertScalarWrapper(
                        result[January(1)],
                        "RepeatedScalarWindows:ruleset",
                        0.1,
                        ScheduleType.Fraction);
                    Assert.NotSame(result[January(2)], result[January(4)]);
                    Assert.NotSame(result[January(2)].Weekdays, result[January(4)].Weekdays);
                    AssertScalarWrapper(
                        result[January(2)],
                        "RepeatedScalarWindows:window:001",
                        0.5,
                        ScheduleType.Fraction);
                    AssertScalarWrapper(
                        result[January(4)],
                        "RepeatedScalarWindows:window:002",
                        0.5,
                        ScheduleType.Fraction);
                    break;
                }
            case "from-windows.scalar-overlap":
                {
                    ScheduleValueWindow[] windows =
                    {
                        new("0102", "0103", 0.2),
                        new("0103", "0104", 0.3),
                    };
                    context.BindScalarInput("default", 0.1d);
                    context.BindScalarInput("type", ScheduleType.Fraction);
                    context.BindWindowsInput("windows", windows);
                    reconstruct = () => Schedule.FromWindows(
                        "ScalarWindows", 0.1, windows, ScheduleType.Fraction);
                    result = reconstruct();
                    AssertScalarWrapper(
                        result[January(1)],
                        "ScalarWindows:ruleset",
                        0.1,
                        ScheduleType.Fraction);
                    AssertScalarWrapper(
                        result[January(2)],
                        "ScalarWindows:window:001",
                        0.2,
                        ScheduleType.Fraction);
                    AssertScalarWrapper(
                        result[January(3)],
                        "ScalarWindows:window:002",
                        0.3,
                        ScheduleType.Fraction);
                    Assert.Same(result[January(3)], result[January(4)]);
                    break;
                }
            case "from-windows.day-alias":
                {
                    DaySchedule defaultDay = MakeDay("DefaultDay", 0.1, ScheduleType.Fraction);
                    DaySchedule overrideDay = MakeDay("OverrideDay", 0.5, ScheduleType.Fraction);
                    ScheduleValueWindow[] windows =
                    {
                        new("0102", "0103", overrideDay),
                    };
                    context.BindInput("default", defaultDay);
                    context.BindInput("override", overrideDay);
                    context.BindWindowsInput("windows", windows);
                    reconstruct = () => Schedule.FromWindows(
                        "DayWindows", defaultDay,
                        windows, ScheduleType.Real);
                    result = reconstruct();
                    AssertDayWrapper(result[January(1)], "DayWindows:ruleset", defaultDay);
                    AssertDayWrapper(result[January(2)], "DayWindows:window:001", overrideDay);
                    break;
                }
            case "from-windows.ruleset-alias":
                {
                    RuleSet defaultRule = MakeRule("DefaultRule", 0);
                    RuleSet overrideRule = MakeRule("OverrideRule", 1);
                    ScheduleValueWindow[] windows =
                    {
                        new("0102", "0103", overrideRule),
                    };
                    context.BindInput("default", defaultRule);
                    context.BindInput("override", overrideRule);
                    context.BindWindowsInput("windows", windows);
                    reconstruct = () => Schedule.FromWindows(
                        "RuleWindows", defaultRule,
                        windows, ScheduleType.Fraction);
                    result = reconstruct();
                    Assert.Same(defaultRule, result[January(1)]);
                    Assert.Same(overrideRule, result[January(2)]);
                    Assert.Equal("DefaultRule", result[January(1)].Name);
                    Assert.Equal("DefaultRule:weekdays", result[January(1)].Weekdays.Name);
                    Assert.Equal("DefaultRule:weekends", result[January(1)].Weekends.Name);
                    Assert.Equal("OverrideRule", result[January(2)].Name);
                    Assert.Equal("OverrideRule:weekdays", result[January(2)].Weekdays.Name);
                    Assert.Equal("OverrideRule:weekends", result[January(2)].Weekends.Name);
                    break;
                }
            default:
                throw Unknown(caseId);
        }

        AssertScheduleDescriptor(result, expected!.Value, context);
        Schedule repeated = reconstruct();
        AssertSameNameTopology(result, repeated);

        Schedule foreignYear = Schedule.FromWindows(
            "ForeignYear",
            0.1,
            new[]
            {
                new ScheduleValueWindow(
                    new DateTime(2040, 1, 2),
                    new DateTime(2040, 1, 3),
                    0.9),
            },
            ScheduleType.Fraction);
        Assert.Equal(0.1, foreignYear[January(1)].Weekdays[0]);
        Assert.Equal(0.9, foreignYear[January(2)].Weekdays[0]);
        Assert.Equal(0.9, foreignYear[January(3)].Weekdays[0]);
        Assert.Equal(0.1, foreignYear[January(4)].Weekdays[0]);
        AssertScalarWrapper(
            foreignYear[January(1)],
            "ForeignYear:ruleset",
            0.1,
            ScheduleType.Fraction);
        AssertScalarWrapper(
            foreignYear[January(2)],
            "ForeignYear:window:001",
            0.9,
            ScheduleType.Fraction);
        Assert.Same(foreignYear[January(2)], foreignYear[January(3)]);
        AssertRuleRuns(
            foreignYear,
            (1, foreignYear[January(1)]),
            (2, foreignYear[January(2)]),
            (362, foreignYear[January(1)]));

        return Returned(caseId switch
        {
            "from-windows.empty" => "native empty-window factory returns one exact deterministic default wrapper",
            "from-windows.repeated-day-wrappers" => "native repeated DaySchedule windows create exact :001/:002 wrappers while aliasing the same caller day",
            "from-windows.repeated-scalar-wrappers" => "native repeated scalar windows create distinct exact :001/:002 RuleSet and :day wrappers",
            "from-windows.scalar-overlap" => "native later scalar window wins on 01/03 with the exact :002 wrapper",
            "from-windows.day-alias" => "native default and override DaySchedule callers are reused in both slots of exact deterministic wrappers",
            "from-windows.ruleset-alias" => "native default and override RuleSet callers are reused directly despite the explicit Fraction type",
            _ => throw Unknown(caseId),
        },
        "repeated native FromWindows construction has identical exact name topology",
        "native DateTime windows normalize foreign years by month/day to 01/02..01/03");
    }

    private static void AssertScalarWrapper(
        RuleSet rule,
        string expectedName,
        double expectedValue,
        ScheduleType expectedType)
    {
        Assert.Equal(expectedName, rule.Name);
        Assert.Equal(expectedType, rule.Type);
        Assert.Equal($"{expectedName}:day", rule.Weekdays.Name);
        Assert.Equal($"{expectedName}:day", rule.Weekends.Name);
        Assert.NotSame(rule.Weekdays, rule.Weekends);
        Assert.Equal(expectedType, rule.Weekdays.Type);
        Assert.Equal(expectedType, rule.Weekends.Type);
        Assert.All(rule.Weekdays, value => Assert.Equal(expectedValue, value));
        Assert.All(rule.Weekends, value => Assert.Equal(expectedValue, value));
        Assert.All(RuleSlots(rule).Skip(2), slot => Assert.Null(slot.Value));
    }

    private static void AssertDayWrapper(
        RuleSet rule,
        string expectedName,
        DaySchedule expectedDay)
    {
        Assert.Equal(expectedName, rule.Name);
        Assert.Equal(expectedDay.Type, rule.Type);
        Assert.Same(expectedDay, rule.Weekdays);
        Assert.Same(expectedDay, rule.Weekends);
        Assert.All(RuleSlots(rule).Skip(2), slot => Assert.Null(slot.Value));
    }

    private static NativeCall ExecuteIdf(string caseId, JsonElement expected, NativeCaseContext context)
    {
        Schedule schedule;
        RuleSet? firstRule = null;
        RuleSet? secondRule = null;
        switch (caseId)
        {
            case "idf.constant-real":
                schedule = Schedule.FromConstant("Annual", 1d);
                break;
            case "idf.default-expanded-fields":
                schedule = new Schedule("Default");
                break;
            case "idf.multiple-periods":
                firstRule = MakeRule("First", 1);
                secondRule = MakeRule("Second", 2);
                schedule = Schedule.FromCompact(
                    "Multiple",
                    new[]
                    {
                        new SchedulePeriod("0101", "0630", firstRule),
                        new SchedulePeriod("0701", "1231", secondRule),
                    });
                context.BindInput("first", firstRule);
                context.BindInput("second", secondRule);
                break;
            case "idf.rich-overrides":
                {
                    double minimumSubnormal = BitConverter.Int64BitsToDouble(1);
                    DaySchedule weekdays = MakeDay("Weekdays", 0.5);
                    DaySchedule weekends = MakeDay("Weekends", -0d);
                    DaySchedule monday = new(
                        "Monday",
                        Enumerable.Repeat(10_000d, 36)
                            .Concat(Enumerable.Repeat(minimumSubnormal, 108)),
                        ScheduleType.Real);
                    DaySchedule saturday = MakeDay("Saturday", 1.23456789);
                    DaySchedule holiday = MakeDay("Holiday", 2);
                    RuleSet rule = new(
                        "RichRule",
                        weekdays,
                        weekends,
                        monday: monday,
                        saturday: saturday,
                        holiday: holiday);
                    schedule = Schedule.FromConstant("A,B;!", rule);
                    context.BindInput("holiday", holiday);
                    context.BindInput("monday", monday);
                    context.BindInput("rule", rule);
                    context.BindInput("saturday", saturday);
                    context.BindInput("weekdays", weekdays);
                    context.BindInput("weekends", weekends);
                    break;
                }
            default:
                throw Unknown(caseId);
        }

        context.BindInput("schedule", schedule);
        ScheduleReferenceSnapshot before = CaptureSchedule(schedule);
        (int primaryPrefixLength, int extensionLength) = caseId switch
        {
            "idf.constant-real" => (12, 0),
            "idf.default-expanded-fields" => (153, 3499),
            "idf.multiple-periods" => (22, 0),
            "idf.rich-overrides" => (32, 0),
            _ => throw Unknown(caseId),
        };
        Assert.Equal(
            primaryPrefixLength,
            expected.GetProperty("data_entries")
                .EnumerateArray()
                .TakeWhile(entry => entry.GetProperty("value").ValueKind == JsonValueKind.String)
                .Count());
        Assert.Equal(extensionLength, expected.GetProperty("extended_input").GetArrayLength());
        AssertIdfDescriptor(schedule.ToIdfObject(), expected);
        AssertScheduleUnchanged(schedule, before);
        return Returned(
            "native Schedule:Compact object type and contiguous fields equal the asserted primary-prefix-plus-extension mapping",
            "native IDF conversion leaves the complete source Schedule graph reference-identical");
    }

    private static NativeCall ExecuteSummary(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        Schedule schedule = SummarySchedule();
        context.BindInput("left", schedule[0]);
        context.BindInput("right", schedule[2]);
        context.BindInput("final", schedule[4]);
        context.BindInput("schedule", schedule);
        ScheduleReferenceSnapshot before = CaptureSchedule(schedule);
        if (caseId == "summary.invalid-period-limit")
        {
            MethodInfo method = Assert.Single(
                typeof(Schedule).GetMethods(BindingFlags.Instance | BindingFlags.Public),
                candidate => candidate.Name == nameof(Schedule.Summary));
            ParameterInfo parameter = Assert.Single(method.GetParameters());
            Assert.Equal(typeof(int), parameter.ParameterType);
            Assert.ThrowsAny<ArgumentException>(() => method.Invoke(schedule, new object[] { 1.5 }));
            AssertScheduleUnchanged(schedule, before);
            return RaisedType(
                "native Summary exposes one Int32 maxPeriods parameter",
                "reflection invocation with Double 1.5 is rejected without source mutation");
        }

        string actual = caseId switch
        {
            "summary.exact-rich" => schedule.Summary(),
            "summary.zero-period-limit" => schedule.Summary(0),
            "summary.negative-period-limit" => schedule.Summary(-1),
            _ => throw Unknown(caseId),
        };
        Assert.Equal("text", RequiredString(expected!.Value, "kind"));
        Assert.Equal(RequiredString(expected.Value, "value"), actual);
        AssertScheduleUnchanged(schedule, before);
        return Returned(
            "native Summary output equals the exact ordinal fixture text",
            "native Summary leaves the complete source graph reference-identical");
    }

    private static NativeCall ExecuteType(string caseId, JsonElement expected, NativeCaseContext context)
    {
        Schedule schedule = caseId switch
        {
            "type.normal" => Schedule.FromConstant("Type", 0d, ScheduleType.Real),
            "type.explicit-fraction" => Schedule.FromConstant("Type", 0d, ScheduleType.Fraction),
            _ => throw Unknown(caseId),
        };
        context.BindInput("schedule", schedule);
        ScheduleType type = schedule.Type;
        Assert.Equal("schedule-type", RequiredString(expected, "kind"));
        Assert.Equal(RequiredString(expected, "value"), type.CanonicalName());
        Assert.Equal(RequiredString(expected, "idf_object_name"), type.IdfObjectName());
        return Returned("native schedule type and IDF object name match Python");
    }

    private static NativeCall ExecuteUnification(string caseId, JsonElement? expected, NativeCaseContext context)
    {
        RuleSet a = MakeRule("A", 0);
        RuleSet b = MakeRule("B", 1);
        RuleSet c = MakeRule("C", 2);
        RuleSet d = MakeRule("D", 3);
        RuleSet e = MakeRule("E", 4);
        IReadOnlyList<SchedulePeriod> fullA = new[]
        {
            Period(1, 1, 4, 10, a),
            Period(4, 11, 12, 31, b),
        };
        IReadOnlyList<SchedulePeriod> fullB = new[]
        {
            Period(1, 1, 2, 19, c),
            Period(2, 20, 12, 31, d),
        };
        IReadOnlyList<SchedulePeriod> fullC = new[]
        {
            Period(1, 1, 6, 30, e),
            Period(7, 1, 12, 31, a),
        };
        RuleSet[] callerRules = { a, b, c, d, e };
        context.BindInput("rules", callerRules);

        if (caseId == "unify-many.missing-coverage")
        {
            IReadOnlyList<SchedulePeriod> shortA = new[] { Period(1, 1, 1, 1, a) };
            IReadOnlyList<SchedulePeriod> shortB = new[] { Period(1, 1, 1, 2, b) };
            IReadOnlyList<IReadOnlyList<SchedulePeriod>> inputs = new[] { shortA, shortB };
            context.BindInput("compactized_schedules", inputs);
            Assert.Throws<ArgumentException>(() =>
                Schedule.UnifyCompactizedSchedulesMany(shortA, shortB));
            context.BindInput("compactized_schedules", inputs);
            return RaisedDomain(
                "native many-unify rejects the uncovered 01/02 segment in the first input",
                "missing-coverage failure leaves both compact inputs and caller RuleSets reference-identical");
        }

        if (caseId is "unify-pair.missing-coverage" or "unify-pair.interior-gap")
        {
            IReadOnlyList<SchedulePeriod> left = caseId.EndsWith("interior-gap", StringComparison.Ordinal)
                ? new[] { Period(1, 1, 1, 1, a), Period(1, 3, 1, 3, a) }
                : new[] { Period(1, 1, 1, 1, a) };
            IReadOnlyList<SchedulePeriod> right = caseId.EndsWith("interior-gap", StringComparison.Ordinal)
                ? new[] { Period(1, 1, 1, 3, b) }
                : new[] { Period(1, 1, 1, 2, b) };
            IReadOnlyList<IReadOnlyList<SchedulePeriod>> inputs = new[] { left, right };
            context.BindInput("compactized_schedules", inputs);
            Assert.Throws<ArgumentException>(() =>
                Schedule.UnifyCompactizedSchedules(left, right));
            if (caseId == "unify-pair.missing-coverage")
            {
                Assert.Throws<ArgumentException>(() =>
                    Schedule.UnifyCompactizedSchedulesMany(left, right));
            }

            context.BindInput("compactized_schedules", inputs);
            return caseId.EndsWith("interior-gap", StringComparison.Ordinal)
                ? RaisedDomain(
                    "native pair-unify rejects the uncovered interior 01/02 segment and preserves both inputs")
                : RaisedDomain(
                    "native pair-unify rejects the uncovered 01/02 segment in the first input and preserves both inputs",
                    "the separate native many-input operation also rejects the same missing coverage");
        }

        IReadOnlyList<IReadOnlyList<SchedulePeriod>> actual;
        IReadOnlyList<IReadOnlyList<SchedulePeriod>> nativeInputs;
        switch (caseId)
        {
            case "unify-pair.asymmetric":
                {
                    nativeInputs = new[] { fullA, fullB };
                    context.BindInput("compactized_schedules", nativeInputs);
                    (IReadOnlyList<SchedulePeriod> left, IReadOnlyList<SchedulePeriod> right) =
                        Schedule.UnifyCompactizedSchedules(fullA, fullB);
                    actual = new[] { left, right };
                    break;
                }
            case "unify-many.asymmetric-three":
                nativeInputs = new[] { fullA, fullB, fullC };
                context.BindInput("compactized_schedules", nativeInputs);
                actual = Schedule.UnifyCompactizedSchedulesMany(fullA, fullB, fullC);
                break;
            case "unify-pair.first-overlap-wins":
            case "unify-many.first-overlap-wins":
                {
                    IReadOnlyList<SchedulePeriod> overlapping = new[]
                    {
                    Period(1, 1, 12, 31, a),
                        Period(1, 10, 1, 20, b),
                    };
                    nativeInputs = new[] { overlapping, fullB };
                    context.BindInput("compactized_schedules", nativeInputs);
                    if (caseId.StartsWith("unify-pair", StringComparison.Ordinal))
                    {
                        (IReadOnlyList<SchedulePeriod> left, IReadOnlyList<SchedulePeriod> right) =
                            Schedule.UnifyCompactizedSchedules(overlapping, fullB);
                        actual = new[] { left, right };
                    }
                    else
                    {
                        actual = Schedule.UnifyCompactizedSchedulesMany(overlapping, fullB);
                    }

                    break;
                }
            case "unify-pair.empty":
                {
                    IReadOnlyList<SchedulePeriod> emptyLeft = Array.Empty<SchedulePeriod>();
                    IReadOnlyList<SchedulePeriod> emptyRight = Array.Empty<SchedulePeriod>();
                    nativeInputs = new[] { emptyLeft, emptyRight };
                    context.BindInput("compactized_schedules", nativeInputs);
                    (IReadOnlyList<SchedulePeriod> left, IReadOnlyList<SchedulePeriod> right) =
                        Schedule.UnifyCompactizedSchedules(emptyLeft, emptyRight);
                    actual = new[] { left, right };
                    break;
                }
            case "unify-many.one-empty":
                nativeInputs = new[] { (IReadOnlyList<SchedulePeriod>)Array.Empty<SchedulePeriod>() };
                context.BindInput("compactized_schedules", nativeInputs);
                actual = Schedule.UnifyCompactizedSchedulesMany(nativeInputs[0]);
                break;
            case "unify-many.zero":
                nativeInputs = Array.Empty<IReadOnlyList<SchedulePeriod>>();
                context.BindInput("compactized_schedules", nativeInputs);
                actual = Schedule.UnifyCompactizedSchedulesMany();
                break;
            default:
                throw Unknown(caseId);
        }

        AssertCompactSequenceDescriptor(actual, expected!.Value, context);
        context.BindInput("compactized_schedules", nativeInputs);
        context.BindInput("rules", callerRules);
        if (caseId == "unify-many.asymmetric-three")
        {
            Assert.Equal(3, actual.Count);
            AssertPeriodRules(actual[0], a, a, b, b);
            AssertPeriodRules(actual[1], c, d, d, d);
            AssertPeriodRules(actual[2], e, e, e, a);
            Assert.Same(actual[0][0].RuleSet, actual[2][3].RuleSet);
        }

        return Returned(caseId switch
        {
            "unify-many.asymmetric-three" => "native three-way unification returns exact caller patterns [a,a,b,b] / [c,d,d,d] / [e,e,e,a] with cross-output a sharing",
            "unify-many.first-overlap-wins" => "native many-unify retains the first overlapping caller RuleSet at every unified boundary",
            "unify-many.one-empty" => "native many-unify returns one empty read-only output for one empty input",
            "unify-many.zero" => "native many-unify returns zero outputs for zero inputs",
            "unify-pair.asymmetric" => "native pair-unify returns exact asymmetric unified boundaries with original caller RuleSets",
            "unify-pair.first-overlap-wins" => "native pair-unify retains the first overlapping caller RuleSet at every unified boundary",
            "unify-pair.empty" => "native pair-unify returns two empty read-only outputs for two empty inputs",
            _ => throw Unknown(caseId),
        }, "native unification leaves every compact input and caller RuleSet reference unchanged");
    }

    private static void AssertScheduleDescriptor(
        Schedule actual,
        JsonElement expected,
        NativeCaseContext context)
    {
        Assert.Equal("schedule", RequiredString(expected, "kind"));
        context.MapSchedule(RequiredString(expected, "identity_group"), actual);
        Assert.Equal(expected.GetProperty("length").GetInt32(), actual.Count);
        Assert.Equal(RequiredString(expected, "schedule_type"), actual.Type.CanonicalName());
        AssertNameDescriptor(actual.Name, expected.GetProperty("name"), context, actual);

        JsonElement[] expectedRuns = expected.GetProperty("rule_references").EnumerateArray().ToArray();
        List<ReferenceRun<RuleSet>> actualRuns = ReferenceRunLengthEncode(actual.RuleSets);
        Assert.Equal(expectedRuns.Length, actualRuns.Count);
        var localRules = new Dictionary<string, RuleSet>(StringComparer.Ordinal);
        for (int index = 0; index < expectedRuns.Length; index++)
        {
            AssertKeys(expectedRuns[index], "count", "value");
            Assert.Equal(expectedRuns[index].GetProperty("count").GetInt32(), actualRuns[index].Count);
            string identity = RequiredString(expectedRuns[index], "value");
            MapIdentity(localRules, identity, actualRuns[index].Value);
            context.MapRule(identity, actualRuns[index].Value);
        }

        AssertRuleGraph(expected.GetProperty("object_graph"), context, localRules);
    }

    private static void AssertCompactDescriptor(
        IReadOnlyList<SchedulePeriod> actual,
        JsonElement expected,
        NativeCaseContext context)
    {
        Assert.Equal("compact-periods", RequiredString(expected, "kind"));
        JsonElement[] periods = expected.GetProperty("periods").EnumerateArray().ToArray();
        Assert.Equal(periods.Length, actual.Count);
        var localRules = new Dictionary<string, RuleSet>(StringComparer.Ordinal);
        for (int index = 0; index < periods.Length; index++)
        {
            JsonElement period = periods[index];
            AssertKeys(period, "end", "ruleset_identity_group", "start");
            Assert.Equal(ReadDateLike(period.GetProperty("start")), actual[index].Start);
            Assert.Equal(ReadDateLike(period.GetProperty("end")), actual[index].End);
            JsonElement identity = period.GetProperty("ruleset_identity_group");
            Assert.Equal(JsonValueKind.String, identity.ValueKind);
            MapIdentity(localRules, identity.GetString()!, actual[index].RuleSet);
            context.MapRule(identity.GetString()!, actual[index].RuleSet);
        }

        AssertRuleGraph(expected.GetProperty("object_graph"), context, localRules);
    }

    private static void AssertCompactSequenceDescriptor(
        IReadOnlyList<IReadOnlyList<SchedulePeriod>> actual,
        JsonElement expected,
        NativeCaseContext context)
    {
        Assert.Equal("sequence", RequiredString(expected, "kind"));
        JsonElement[] items = expected.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(items.Length, actual.Count);
        for (int index = 0; index < items.Length; index++)
        {
            if (RequiredString(items[index], "kind") == "sequence")
            {
                Assert.Empty(items[index].GetProperty("items").EnumerateArray());
                Assert.Empty(actual[index]);
            }
            else
            {
                AssertCompactDescriptor(actual[index], items[index], context);
            }
        }
    }

    private static void AssertDaySequenceDescriptor(
        IReadOnlyList<DaySchedule> actual,
        JsonElement expected,
        NativeCaseContext context)
    {
        Assert.Equal("day-schedule-sequence", RequiredString(expected, "kind"));
        Assert.Equal(expected.GetProperty("length").GetInt32(), actual.Count);
        JsonElement[] expectedRuns = expected.GetProperty("references").EnumerateArray().ToArray();
        List<ReferenceRun<DaySchedule>> actualRuns = ReferenceRunLengthEncode(actual);
        Assert.Equal(expectedRuns.Length, actualRuns.Count);
        var localDays = new Dictionary<string, DaySchedule>(StringComparer.Ordinal);
        for (int index = 0; index < expectedRuns.Length; index++)
        {
            AssertKeys(expectedRuns[index], "count", "value");
            Assert.Equal(expectedRuns[index].GetProperty("count").GetInt32(), actualRuns[index].Count);
            string identity = RequiredString(expectedRuns[index], "value");
            MapIdentity(localDays, identity, actualRuns[index].Value);
            context.MapDay(identity, actualRuns[index].Value);
        }

        JsonElement[] days = expected.GetProperty("day_schedules").EnumerateArray().ToArray();
        Assert.Equal(localDays.Count, days.Length);
        foreach (JsonElement day in days)
        {
            string identity = RequiredString(day, "identity_group");
            Assert.True(localDays.TryGetValue(identity, out DaySchedule? value), $"Missing native day identity '{identity}'.");
            AssertDayDescriptor(value!, day, context);
        }
    }

    private static void AssertRuleGraph(
        JsonElement graph,
        NativeCaseContext context,
        Dictionary<string, RuleSet>? expectedLocalRules = null)
    {
        AssertKeys(graph, "day_schedules", "rulesets");
        JsonElement[] rules = graph.GetProperty("rulesets").EnumerateArray().ToArray();
        if (expectedLocalRules is not null)
        {
            Assert.Equal(expectedLocalRules.Count, rules.Length);
        }

        var localDays = new Dictionary<string, DaySchedule>(StringComparer.Ordinal);
        foreach (JsonElement ruleDescriptor in rules)
        {
            AssertKeys(ruleDescriptor, "identity_group", "name", "schedule_type", "slots");
            string identity = RequiredString(ruleDescriptor, "identity_group");
            RuleSet rule = context.RequiredRule(identity);
            if (expectedLocalRules is not null)
            {
                Assert.True(expectedLocalRules.TryGetValue(identity, out RuleSet? localRule));
                Assert.Same(localRule, rule);
            }

            Assert.Equal(RequiredString(ruleDescriptor, "schedule_type"), rule.Type.CanonicalName());
            AssertNameDescriptor(rule.Name, ruleDescriptor.GetProperty("name"), context, rule);

            JsonElement slots = ruleDescriptor.GetProperty("slots");
            AssertKeys(slots, "friday", "holiday", "monday", "saturday", "sunday", "thursday", "tuesday", "wednesday", "weekdays", "weekends");
            foreach ((string name, DaySchedule? day) in RuleSlots(rule))
            {
                JsonElement expectedSlot = slots.GetProperty(name);
                if (expectedSlot.ValueKind == JsonValueKind.Null)
                {
                    Assert.Null(day);
                }
                else
                {
                    Assert.NotNull(day);
                    string dayIdentity = expectedSlot.GetString()!;
                    MapIdentity(localDays, dayIdentity, day!);
                    context.MapDay(dayIdentity, day!);
                }
            }
        }

        JsonElement[] days = graph.GetProperty("day_schedules").EnumerateArray().ToArray();
        Assert.Equal(localDays.Count, days.Length);
        foreach (JsonElement dayDescriptor in days)
        {
            string identity = RequiredString(dayDescriptor, "identity_group");
            Assert.True(localDays.TryGetValue(identity, out DaySchedule? day), $"Missing native DaySchedule identity '{identity}'.");
            Assert.Same(context.RequiredDay(identity), day);
            AssertDayDescriptor(day!, dayDescriptor, context);
        }
    }

    private static void AssertDayDescriptor(
        DaySchedule actual,
        JsonElement expected,
        NativeCaseContext context)
    {
        Assert.Equal(RequiredString(expected, "schedule_type"), actual.Type.CanonicalName());
        JsonElement unit = expected.GetProperty("unit");
        if (unit.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(actual.Unit);
        }
        else
        {
            Assert.Equal(unit.GetString(), actual.Unit);
        }

        context.MapDay(RequiredString(expected, "identity_group"), actual);
        AssertNameDescriptor(actual.Name, expected.GetProperty("name"), context, actual);
        JsonElement values = expected.GetProperty("values");
        string encoding = RequiredString(values, "encoding");
        Assert.Equal(values.GetProperty("length").GetInt32(), actual.Count);
        if (encoding == "empty")
        {
            Assert.Empty(actual);
            return;
        }

        Assert.Equal("repeat", encoding);
        JsonElement[] pattern = values.GetProperty("pattern").EnumerateArray().ToArray();
        Assert.NotEmpty(pattern);
        for (int index = 0; index < actual.Count; index++)
        {
            AssertScalarDescriptor(actual[index], pattern[index % pattern.Length]);
        }
    }

    private static void AssertNameDescriptor(
        string actual,
        JsonElement expected,
        NativeCaseContext context,
        object owner)
    {
        string policy = RequiredString(expected, "policy");
        Assert.False(string.IsNullOrWhiteSpace(actual));
        Assert.False(actual.StartsWith("0x", StringComparison.Ordinal));
        if (policy == "runtime-identity-hex")
        {
            Assert.Equal(
                context.RequiredNativeName(RequiredString(expected, "identity_group"), owner),
                actual);
            return;
        }

        Assert.Equal("literal-with-normalized-runtime-identities", policy);
        JsonElement[] segments = expected.GetProperty("segments").EnumerateArray().ToArray();
        string expectedNativeName = string.Concat(segments.Select(segment =>
            RequiredString(segment, "kind") switch
            {
                "literal" => RequiredString(segment, "value"),
                "runtime-identity" => context.RequiredNativeName(
                    RequiredString(segment, "value"),
                    owner: null),
                string kind => throw new Xunit.Sdk.XunitException(
                    $"Unknown normalized name segment kind '{kind}'."),
            }));
        Assert.Equal(expectedNativeName, actual);
    }

    private static void AssertScalarDescriptor(double actual, JsonElement expected)
    {
        string kind = RequiredString(expected, "kind");
        switch (kind)
        {
            case "binary64":
                Assert.Equal(RequiredString(expected, "hex_without_prefix"), ToPythonHexWithoutPrefix(actual));
                break;
            case "nonfinite":
                {
                    string value = RequiredString(expected, "value");
                    Assert.True(value switch
                    {
                        "nan" => double.IsNaN(actual),
                        "positive-infinity" => double.IsPositiveInfinity(actual),
                        "negative-infinity" => double.IsNegativeInfinity(actual),
                        _ => false,
                    });
                    break;
                }
            case "int":
                Assert.Equal(expected.GetProperty("value").GetInt64(), actual);
                break;
            case "bool":
                Assert.Equal(expected.GetProperty("value").GetBoolean() ? 1d : 0d, actual);
                break;
            default:
                throw new Xunit.Sdk.XunitException($"Expected numeric descriptor, got '{kind}'.");
        }
    }

    private static void AssertIdfDescriptor(IdfObject actual, JsonElement expected)
    {
        Assert.Equal("idf-object", RequiredString(expected, "kind"));
        Assert.Equal(RequiredString(expected, "object_type"), actual.ObjectType);
        JsonElement[] primary = expected.GetProperty("data_entries").EnumerateArray().ToArray();
        Assert.Equal(153, primary.Length);
        int primaryPrefixLength = Array.FindIndex(
            primary,
            entry => entry.GetProperty("value").ValueKind == JsonValueKind.Null);
        if (primaryPrefixLength < 0)
        {
            primaryPrefixLength = primary.Length;
        }

        Assert.All(
            primary.Skip(primaryPrefixLength),
            entry => Assert.Equal(JsonValueKind.Null, entry.GetProperty("value").ValueKind));
        string[] primaryPrefix = primary
            .Take(primaryPrefixLength)
            .Select(entry => entry.GetProperty("value").GetString()!)
            .ToArray();
        string[] extension = expected.GetProperty("extended_input")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        string[] expectedNativeFields = primaryPrefix.Concat(extension).ToArray();
        string[] actualFields = actual.Fields.Select(field => field.Value).ToArray();
        Assert.Equal(expectedNativeFields, actualFields);
        Assert.Equal(primaryPrefix, actualFields.Take(primaryPrefixLength).ToArray());
        Assert.Equal(extension, actualFields.Skip(primaryPrefixLength).ToArray());
        if (extension.Length > 0)
        {
            Assert.Equal(153, primaryPrefixLength);
            Assert.Equal(extension[0], actualFields[153]);
        }
    }

    private static void MapIdentity<T>(Dictionary<string, T> map, string identity, T value)
        where T : class
    {
        if (map.TryGetValue(identity, out T? existing))
        {
            Assert.Same(existing, value);
            return;
        }

        Assert.False(
            map.Values.Any(candidate => ReferenceEquals(candidate, value)),
            $"Distinct fixture identities unexpectedly map to the same native {typeof(T).Name}.");
        map.Add(identity, value);
    }

    private static List<ReferenceRun<T>> ReferenceRunLengthEncode<T>(IEnumerable<T> values)
        where T : class
    {
        var result = new List<ReferenceRun<T>>();
        foreach (T value in values)
        {
            if (result.Count > 0 && ReferenceEquals(result[result.Count - 1].Value, value))
            {
                ReferenceRun<T> previous = result[result.Count - 1];
                result[result.Count - 1] = previous with { Count = previous.Count + 1 };
            }
            else
            {
                result.Add(new ReferenceRun<T>(1, value));
            }
        }

        return result;
    }

    private static IEnumerable<(string Name, DaySchedule? Value)> RuleSlots(RuleSet rule)
    {
        yield return ("weekdays", rule.Weekdays);
        yield return ("weekends", rule.Weekends);
        yield return ("monday", rule.Monday);
        yield return ("tuesday", rule.Tuesday);
        yield return ("wednesday", rule.Wednesday);
        yield return ("thursday", rule.Thursday);
        yield return ("friday", rule.Friday);
        yield return ("saturday", rule.Saturday);
        yield return ("sunday", rule.Sunday);
        yield return ("holiday", rule.Holiday);
    }

    private static DaySchedule MakeDay(
        string name,
        double value,
        ScheduleType type = ScheduleType.Real)
    {
        return DaySchedule.Constant(name, value, type);
    }

    private static RuleSet MakeRule(
        string name,
        double value,
        ScheduleType type = ScheduleType.Real)
    {
        return new RuleSet(
            name,
            MakeDay($"{name}:weekdays", value, type),
            MakeDay($"{name}:weekends", value, type),
            type: type);
    }

    private static (Schedule Schedule, RuleSet Left, RuleSet Middle) ThreePeriodSchedule()
    {
        RuleSet left = MakeRule("A", 0);
        RuleSet middle = MakeRule("B", 1);
        Schedule schedule = new(
            "S",
            Enumerable.Repeat(left, 2)
                .Concat(Enumerable.Repeat(middle, 2))
                .Concat(Enumerable.Repeat(left, 361)));
        return (schedule, left, middle);
    }

    private static void AssertDefaultGraphNames(Schedule schedule, string scheduleName)
    {
        Assert.Equal(scheduleName, schedule.Name);
        Assert.Equal(Schedule.FixedLength, UniqueReferenceCount(schedule.RuleSets));
        for (int index = 0; index < Schedule.FixedLength; index++)
        {
            RuleSet rule = schedule[index];
            string expectedRuleName = $"{scheduleName}:default:{index + 1:D3}";
            Assert.Equal(expectedRuleName, rule.Name);
            Assert.Equal($"{expectedRuleName}:day", rule.Weekdays.Name);
            Assert.Equal($"{expectedRuleName}:day", rule.Weekends.Name);
            Assert.NotSame(rule.Weekdays, rule.Weekends);
        }
    }

    private static void AssertSameNameTopology(Schedule first, Schedule second)
    {
        Assert.Equal(first.Name, second.Name);
        List<ReferenceRun<RuleSet>> firstRuns = ReferenceRunLengthEncode(first.RuleSets);
        List<ReferenceRun<RuleSet>> secondRuns = ReferenceRunLengthEncode(second.RuleSets);
        Assert.Equal(firstRuns.Select(run => run.Count), secondRuns.Select(run => run.Count));
        Assert.Equal(firstRuns.Count, secondRuns.Count);
        for (int index = 0; index < firstRuns.Count; index++)
        {
            RuleSet left = firstRuns[index].Value;
            RuleSet right = secondRuns[index].Value;
            Assert.Equal(left.Name, right.Name);
            Assert.Equal(left.Type, right.Type);
            Assert.Equal(
                RuleSlots(left).Select(slot => slot.Value?.Name),
                RuleSlots(right).Select(slot => slot.Value?.Name));
        }
    }

    private static Schedule SummarySchedule()
    {
        DaySchedule leftDay = MakeDay("LeftDay", -0d);
        RuleSet left = new("dup", leftDay, leftDay);
        RuleSet right = MakeRule("peak", 12_345);
        DaySchedule finalDay = MakeDay("FinalDay", -0d);
        RuleSet final = new("dup", finalDay, finalDay);
        return new Schedule(
            "S'Q",
            Enumerable.Repeat(left, 2)
                .Concat(Enumerable.Repeat(right, 2))
                .Concat(Enumerable.Repeat(final, 361)));
    }

    private static SchedulePeriod Period(
        int startMonth,
        int startDay,
        int endMonth,
        int endDay,
        RuleSet rule)
    {
        return new SchedulePeriod(
            new DateTime(2026, startMonth, startDay),
            new DateTime(2026, endMonth, endDay),
            rule);
    }

    private static DateTime January(int day) => new(2026, 1, day);

    private static DateTime ReadDate(JsonElement descriptor)
    {
        Assert.Equal("date", RequiredString(descriptor, "kind"));
        return DateTime.ParseExact(
            RequiredString(descriptor, "value"),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
    }

    private static DateTime ReadDateLike(JsonElement descriptor)
    {
        string kind = RequiredString(descriptor, "kind");
        if (kind == "date")
        {
            return ReadDate(descriptor);
        }

        Assert.Equal("text", kind);
        string value = RequiredString(descriptor, "value");
        string digits = new(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 8)
        {
            digits = digits.Substring(4);
        }

        if (digits.Length == 4
            && int.TryParse(digits.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int month)
            && int.TryParse(digits.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int day))
        {
            return new DateTime(Schedule.DefaultYear, month, day);
        }

        string[] parts = value.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, parts.Length);
        return new DateTime(
            Schedule.DefaultYear,
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private static bool DescriptorBoolean(JsonElement descriptor)
    {
        Assert.Equal("bool", RequiredString(descriptor, "kind"));
        return descriptor.GetProperty("value").GetBoolean();
    }

    private static int UniqueReferenceCount(IEnumerable<RuleSet> values)
    {
        var unique = new List<RuleSet>();
        foreach (RuleSet value in values)
        {
            if (!unique.Any(candidate => ReferenceEquals(candidate, value)))
            {
                unique.Add(value);
            }
        }

        return unique.Count;
    }

    private static ScheduleReferenceSnapshot CaptureSchedule(Schedule schedule)
    {
        RuleSet[] rules = schedule.RuleSets.ToArray();
        var uniqueRuleObjects = new List<RuleSet>();
        foreach (RuleSet rule in rules)
        {
            if (!uniqueRuleObjects.Any(candidate => ReferenceEquals(candidate, rule)))
            {
                uniqueRuleObjects.Add(rule);
            }
        }

        RuleSetReferenceSnapshot[] uniqueRules = uniqueRuleObjects
            .Select(rule => new RuleSetReferenceSnapshot(
                rule,
                rule.Name,
                rule.Type,
                RuleSlots(rule).Select(slot => slot.Value).ToArray()))
            .ToArray();
        return new ScheduleReferenceSnapshot(
            schedule.Name,
            schedule.Type,
            rules,
            uniqueRules);
    }

    private static RuleSetReferenceSnapshot CaptureRule(RuleSet rule) => new(
        rule,
        rule.Name,
        rule.Type,
        RuleSlots(rule).Select(slot => slot.Value).ToArray());

    private static void AssertRuleUnchanged(RuleSetReferenceSnapshot expected)
    {
        Assert.Equal(expected.Name, expected.Rule.Name);
        Assert.Equal(expected.Type, expected.Rule.Type);
        DaySchedule?[] slots = RuleSlots(expected.Rule).Select(slot => slot.Value).ToArray();
        Assert.Equal(expected.Slots.Length, slots.Length);
        for (int index = 0; index < slots.Length; index++)
        {
            Assert.Same(expected.Slots[index], slots[index]);
        }
    }

    private static void AssertScheduleUnchanged(
        Schedule actual,
        ScheduleReferenceSnapshot expected)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Rules.Length, actual.RuleSets.Count);
        for (int index = 0; index < expected.Rules.Length; index++)
        {
            Assert.Same(expected.Rules[index], actual.RuleSets[index]);
        }

        foreach (RuleSetReferenceSnapshot ruleSnapshot in expected.UniqueRules)
        {
            Assert.Equal(ruleSnapshot.Name, ruleSnapshot.Rule.Name);
            Assert.Equal(ruleSnapshot.Type, ruleSnapshot.Rule.Type);
            DaySchedule?[] slots = RuleSlots(ruleSnapshot.Rule)
                .Select(slot => slot.Value)
                .ToArray();
            Assert.Equal(ruleSnapshot.Slots.Length, slots.Length);
            for (int index = 0; index < slots.Length; index++)
            {
                Assert.Same(ruleSnapshot.Slots[index], slots[index]);
            }
        }
    }

    private static void AssertRuleRuns(
        Schedule schedule,
        params (int Count, RuleSet Rule)[] expected)
    {
        List<ReferenceRun<RuleSet>> runs = ReferenceRunLengthEncode(schedule.RuleSets);
        Assert.Equal(expected.Length, runs.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Count, runs[index].Count);
            Assert.Same(expected[index].Rule, runs[index].Value);
        }
    }

    private static void AssertPeriodRules(
        IReadOnlyList<SchedulePeriod> periods,
        params RuleSet[] expected)
    {
        Assert.Equal(expected.Length, periods.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Same(expected[index], periods[index].RuleSet);
        }
    }

    private static string ToPythonHexWithoutPrefix(double value)
    {
        long signedBits = BitConverter.DoubleToInt64Bits(value);
        bool negative = signedBits < 0;
        ulong bits = unchecked((ulong)signedBits);
        ulong magnitude = bits & 0x7fff_ffff_ffff_ffffUL;
        string sign = negative ? "-" : string.Empty;
        if (magnitude == 0)
        {
            return $"{sign}0.0p+0";
        }

        int exponentBits = (int)((magnitude >> 52) & 0x7ffUL);
        ulong fraction = magnitude & 0x000f_ffff_ffff_ffffUL;
        if (exponentBits == 0x7ff)
        {
            throw new ArgumentException("Python hexadecimal descriptors only encode finite values.", nameof(value));
        }

        if (exponentBits == 0)
        {
            return $"{sign}0.{fraction:x13}p-1022";
        }

        int exponent = exponentBits - 1023;
        return $"{sign}1.{fraction:x13}p{(exponent >= 0 ? "+" : string.Empty)}{exponent}";
    }

    private static void AssertCountObject(JsonElement actual, params (string Name, int Count)[] expected)
    {
        AssertKeys(actual, expected.Select(item => item.Name).ToArray());
        foreach ((string name, int count) in expected)
        {
            Assert.Equal(count, actual.GetProperty(name).GetInt32());
        }
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(actual.Length, actual.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            actual.OrderBy(item => item, StringComparer.Ordinal).ToArray());
    }

    private static void AssertUniqueObjectKeys(JsonElement value)
    {
        string[] keys = value.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
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

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private static NativeCall Returned(params string[] facts) => new("returned", null, facts);

    private static NativeCall RaisedDomain(params string[] facts) => new("raised", "domain", facts);

    private static NativeCall RaisedType(params string[] facts) => new("raised", "type", facts);

    private static Xunit.Sdk.XunitException Unknown(string caseId) =>
        new($"Unknown Schedule core case '{caseId}'.");

    private sealed record SymbolBinding(
        string Symbol,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        int CaseCount,
        string Classification,
        string? AdaptationId,
        string AssertionId);

    private sealed record CaseBinding(string CaseId, string Symbol, string Executor);

    private sealed record NativeExpectation(
        string Adaptation,
        string Outcome,
        string? ErrorCategory);

    private sealed class NativeCaseContext
    {
        private static readonly Dictionary<string, string[]> ExpectedInputMappings =
            new(StringComparer.Ordinal)
            {
                ["from-compact.leap-day"] = new[] { "compact" },
                ["from-compact.reversed-noop"] = new[] { "compact" },
                ["from-windows.leap-day"] = new[] { "windows" },
                ["from-windows.reversed-noop"] = new[] { "windows" },
                ["init.invalid-item"] = new[] { "rulesets" },
            };

        private readonly string caseId;
        private readonly JsonElement inputPostconditions;
        private readonly HashSet<string> boundInputs = new(StringComparer.Ordinal);
        private readonly HashSet<string> adaptedInputs = new(StringComparer.Ordinal);
        private readonly List<Action> finalAssertions = new();
        private readonly Dictionary<string, Schedule> schedules = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RuleSet> rules = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DaySchedule> days = new(StringComparer.Ordinal);

        public NativeCaseContext(JsonElement item)
        {
            caseId = RequiredString(item, "id");
            inputPostconditions = item
                .GetProperty("observation")
                .GetProperty("input_postconditions");
        }

        public void AssertFinalInputPostconditions()
        {
            foreach (Action assertion in finalAssertions)
            {
                assertion();
            }

            string[] expectedNames = inputPostconditions
                .EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                expectedNames,
                boundInputs.OrderBy(name => name, StringComparer.Ordinal).ToArray());

            string[] expectedMappings = ExpectedInputMappings.TryGetValue(caseId, out string[]? mappings)
                ? mappings
                : Array.Empty<string>();
            Assert.Equal(
                expectedMappings.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                adaptedInputs.OrderBy(name => name, StringComparer.Ordinal).ToArray());

            foreach (JsonProperty property in inputPostconditions.EnumerateObject())
            {
                JsonElement postcondition = property.Value;
                AssertKeys(postcondition, "after", "before", "preserved");
                JsonElement after = postcondition.GetProperty("after");
                if (postcondition.GetProperty("preserved").GetBoolean())
                {
                    AssertKeys(after, "kind");
                    Assert.Equal("same-as-before", RequiredString(after, "kind"));
                }
                else
                {
                    Assert.True(
                        ExpectedAdaptations.ContainsKey(caseId),
                        $"Fixture mutation for '{caseId}:{property.Name}' lacks a pinned .NET adaptation.");
                    Assert.NotEqual("same-as-before", RequiredString(after, "kind"));
                }
            }
        }

        public void BindInput(string name, Schedule value)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertScheduleDescriptor(value, descriptor, this);
            finalAssertions.Add(() => AssertScheduleDescriptor(value, descriptor, this));
        }

        public void BindInput(string name, RuleSet value)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertRuleDescriptor(value, descriptor);
            finalAssertions.Add(() => AssertRuleDescriptor(value, descriptor));
        }

        public void BindInput(string name, DaySchedule value)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertDayInputDescriptor(value, descriptor);
            finalAssertions.Add(() => AssertDayInputDescriptor(value, descriptor));
        }

        public void BindInput(string name, IReadOnlyList<RuleSet> values)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertRuleSetSequenceDescriptor(values, descriptor);
            finalAssertions.Add(() => AssertRuleSetSequenceDescriptor(values, descriptor));
        }

        public void BindInputUsingFixtureAfter(string name, IReadOnlyList<RuleSet> values)
        {
            JsonElement postcondition = BindPostcondition(name);
            Assert.False(postcondition.GetProperty("preserved").GetBoolean());
            Assert.True(ExpectedAdaptations.ContainsKey(caseId));
            AssertRuleSetSequenceDescriptor(values, postcondition.GetProperty("before"));
            JsonElement after = postcondition.GetProperty("after");
            finalAssertions.Add(() => AssertRuleSetSequenceDescriptor(values, after));
        }

        public void BindInvalidRuleSetInput(string name, IReadOnlyList<RuleSet> values)
        {
            Assert.Equal("init.invalid-item", caseId);
            JsonElement descriptor = AdaptedDescriptor(
                name,
                "Python's final object() sentinel maps to null at the typed IEnumerable<RuleSet> boundary.");
            AssertInvalidRuleSetSequenceDescriptor(values, descriptor);
            finalAssertions.Add(() => AssertInvalidRuleSetSequenceDescriptor(values, descriptor));
        }

        public void BindInput(string name, IReadOnlyList<SchedulePeriod> values)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertCompactOrEmptyDescriptor(values, descriptor);
            finalAssertions.Add(() => AssertCompactOrEmptyDescriptor(values, descriptor));
        }

        public void BindInput(
            string name,
            IReadOnlyList<IReadOnlyList<SchedulePeriod>> values)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertCompactSequenceDescriptor(values, descriptor, this);
            finalAssertions.Add(() => AssertCompactSequenceDescriptor(values, descriptor, this));
        }

        public void BindScalarInput(string name, object? value)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertScalarInputDescriptor(value, descriptor);
            finalAssertions.Add(() => AssertScalarInputDescriptor(value, descriptor));
        }

        public void BindDateSequenceInput(string name, IReadOnlyList<DateTime> values)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertDateSequenceDescriptor(values, descriptor);
            finalAssertions.Add(() => AssertDateSequenceDescriptor(values, descriptor));
        }

        public void BindWindowsInput(string name, IReadOnlyList<ScheduleValueWindow> values)
        {
            JsonElement descriptor = BindDescriptor(name);
            AssertWindowDescriptor(values, descriptor);
            finalAssertions.Add(() => AssertWindowDescriptor(values, descriptor));
        }

        public void AdaptUnrepresentablePeriodInput(
            string name,
            string start,
            string end,
            RuleSet rule,
            string reason)
        {
            JsonElement descriptor = AdaptedDescriptor(name, reason);
            Assert.Equal("compact-periods", RequiredString(descriptor, "kind"));
            JsonElement period = Assert.Single(descriptor.GetProperty("periods").EnumerateArray());
            Assert.Equal("text", RequiredString(period.GetProperty("start"), "kind"));
            Assert.Equal(start, RequiredString(period.GetProperty("start"), "value"));
            Assert.Equal("text", RequiredString(period.GetProperty("end"), "kind"));
            Assert.Equal(end, RequiredString(period.GetProperty("end"), "value"));
            string identity = RequiredString(period, "ruleset_identity_group");
            MapRule(identity, rule);
            AssertRuleGraph(
                descriptor.GetProperty("object_graph"),
                this,
                new Dictionary<string, RuleSet>(StringComparer.Ordinal) { [identity] = rule });
            RuleSetReferenceSnapshot before = CaptureRule(rule);
            finalAssertions.Add(() => AssertRuleUnchanged(before));
        }

        public void AdaptUnrepresentableWindowInput(
            string name,
            string start,
            string end,
            object value,
            string reason)
        {
            JsonElement descriptor = AdaptedDescriptor(name, reason);
            Assert.Equal("sequence", RequiredString(descriptor, "kind"));
            Assert.Equal("list", RequiredString(descriptor, "container"));
            JsonElement window = Assert.Single(descriptor.GetProperty("items").EnumerateArray());
            Assert.Equal("sequence", RequiredString(window, "kind"));
            Assert.Equal("tuple", RequiredString(window, "container"));
            JsonElement[] items = window.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(3, items.Length);
            Assert.Equal("text", RequiredString(items[0], "kind"));
            Assert.Equal(start, RequiredString(items[0], "value"));
            Assert.Equal("text", RequiredString(items[1], "kind"));
            Assert.Equal(end, RequiredString(items[1], "value"));
            AssertScalarInputDescriptor(value, items[2]);
        }

        public void MapSchedule(string identity, Schedule value) =>
            MapIdentity(schedules, identity, value);

        public void MapRule(string identity, RuleSet value) =>
            MapIdentity(rules, identity, value);

        public void MapDay(string identity, DaySchedule value) =>
            MapIdentity(days, identity, value);

        public RuleSet RequiredRule(string identity)
        {
            Assert.True(rules.TryGetValue(identity, out RuleSet? value),
                $"Missing case-scoped native RuleSet identity '{identity}'.");
            return value!;
        }

        public DaySchedule RequiredDay(string identity)
        {
            Assert.True(days.TryGetValue(identity, out DaySchedule? value),
                $"Missing case-scoped native DaySchedule identity '{identity}'.");
            return value!;
        }

        public string RequiredNativeName(string identity, object? owner)
        {
            object value;
            if (identity.StartsWith("schedule:", StringComparison.Ordinal))
            {
                Assert.True(schedules.TryGetValue(identity, out Schedule? schedule),
                    $"Missing case-scoped native Schedule identity '{identity}'.");
                value = schedule!;
            }
            else if (identity.StartsWith("ruleset:", StringComparison.Ordinal))
            {
                value = RequiredRule(identity);
            }
            else if (identity.StartsWith("day-schedule:", StringComparison.Ordinal))
            {
                value = RequiredDay(identity);
            }
            else
            {
                throw new Xunit.Sdk.XunitException($"Unknown identity group '{identity}'.");
            }

            if (owner is not null)
            {
                Assert.Same(owner, value);
            }

            return value switch
            {
                Schedule schedule => schedule.Name,
                RuleSet rule => rule.Name,
                DaySchedule day => day.Name,
                _ => throw new Xunit.Sdk.XunitException($"Identity '{identity}' has no native name."),
            };
        }

        private JsonElement BindDescriptor(string name) =>
            BindPostcondition(name).GetProperty("before");

        private JsonElement AdaptedDescriptor(string name, string reason)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason));
            JsonElement postcondition = BindPostcondition(name);
            Assert.True(
                adaptedInputs.Add(name),
                $"Input mapping '{caseId}:{name}' was declared more than once.");
            return postcondition.GetProperty("before");
        }

        private JsonElement BindPostcondition(string name)
        {
            Assert.True(
                inputPostconditions.TryGetProperty(name, out JsonElement postcondition),
                $"Fixture input '{caseId}:{name}' is missing.");
            AssertKeys(postcondition, "after", "before", "preserved");
            boundInputs.Add(name);
            return postcondition;
        }

        private void AssertRuleDescriptor(RuleSet value, JsonElement descriptor)
        {
            Assert.Equal("ruleset", RequiredString(descriptor, "kind"));
            string identity = RequiredString(descriptor, "identity_group");
            MapRule(identity, value);
            AssertRuleGraph(
                descriptor.GetProperty("object_graph"),
                this,
                new Dictionary<string, RuleSet>(StringComparer.Ordinal) { [identity] = value });
        }

        private void AssertDayInputDescriptor(DaySchedule value, JsonElement descriptor)
        {
            Assert.Equal("day-schedule", RequiredString(descriptor, "kind"));
            MapDay(RequiredString(descriptor, "identity_group"), value);
            AssertDayDescriptor(value, descriptor, this);
        }

        private void AssertRuleSetSequenceDescriptor(
            IReadOnlyList<RuleSet> values,
            JsonElement descriptor)
        {
            Assert.Equal("ruleset-sequence", RequiredString(descriptor, "kind"));
            Assert.Equal(descriptor.GetProperty("length").GetInt32(), values.Count);
            JsonElement[] expectedRuns = descriptor.GetProperty("references").EnumerateArray().ToArray();
            List<ReferenceRun<RuleSet>> actualRuns = ReferenceRunLengthEncode(values);
            Assert.Equal(expectedRuns.Length, actualRuns.Count);
            var localRules = new Dictionary<string, RuleSet>(StringComparer.Ordinal);
            for (int index = 0; index < expectedRuns.Length; index++)
            {
                AssertKeys(expectedRuns[index], "count", "value");
                Assert.Equal(expectedRuns[index].GetProperty("count").GetInt32(), actualRuns[index].Count);
                string identity = RequiredString(expectedRuns[index], "value");
                MapIdentity(localRules, identity, actualRuns[index].Value);
                MapRule(identity, actualRuns[index].Value);
            }

            AssertRuleGraph(descriptor.GetProperty("object_graph"), this, localRules);
        }

        private void AssertInvalidRuleSetSequenceDescriptor(
            IReadOnlyList<RuleSet> values,
            JsonElement descriptor)
        {
            Assert.Equal("sequence", RequiredString(descriptor, "kind"));
            Assert.Equal("list", RequiredString(descriptor, "container"));
            JsonElement[] items = descriptor.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(items.Length, values.Count);
            Assert.Equal(Schedule.FixedLength, values.Count);
            RuleSet first = Assert.IsType<RuleSet>(values[0]);
            for (int index = 0; index < values.Count - 1; index++)
            {
                Assert.Same(first, values[index]);
                Assert.Equal("ruleset", RequiredString(items[index], "kind"));
                Assert.Equal(
                    RequiredString(items[0], "identity_group"),
                    RequiredString(items[index], "identity_group"));
            }

            Assert.Null(values[values.Count - 1]);
            Assert.Equal("object", RequiredString(items[items.Length - 1], "kind"));
            Assert.Equal("object", RequiredString(items[items.Length - 1], "python_type"));
            AssertRuleDescriptor(first, items[0]);
        }

        private void AssertCompactOrEmptyDescriptor(
            IReadOnlyList<SchedulePeriod> values,
            JsonElement descriptor)
        {
            if (RequiredString(descriptor, "kind") == "sequence")
            {
                Assert.Equal("list", RequiredString(descriptor, "container"));
                Assert.Empty(descriptor.GetProperty("items").EnumerateArray());
                Assert.Empty(values);
                return;
            }

            AssertCompactDescriptor(values, descriptor, this);
        }

        private void AssertScalarInputDescriptor(object? value, JsonElement descriptor)
        {
            string kind = RequiredString(descriptor, "kind");
            switch (kind)
            {
                case "none":
                    Assert.Null(value);
                    break;
                case "text":
                    Assert.Equal(RequiredString(descriptor, "value"), Assert.IsType<string>(value));
                    break;
                case "bool":
                    Assert.Equal(descriptor.GetProperty("value").GetBoolean(), Assert.IsType<bool>(value));
                    break;
                case "binary64":
                case "nonfinite":
                    AssertScalarDescriptor(Assert.IsType<double>(value), descriptor);
                    break;
                case "schedule-type":
                    ScheduleType type = Assert.IsType<ScheduleType>(value);
                    Assert.Equal(RequiredString(descriptor, "value"), type.CanonicalName());
                    Assert.Equal(RequiredString(descriptor, "idf_object_name"), type.IdfObjectName());
                    break;
                case "object":
                    Assert.NotNull(value);
                    Assert.Equal(typeof(object), value!.GetType());
                    Assert.Equal("object", RequiredString(descriptor, "python_type"));
                    break;
                default:
                    throw new Xunit.Sdk.XunitException(
                        $"Unsupported scalar input descriptor kind '{kind}' in '{caseId}'.");
            }
        }

        private static void AssertDateSequenceDescriptor(
            IReadOnlyList<DateTime> values,
            JsonElement descriptor)
        {
            Assert.Equal("date-sequence", RequiredString(descriptor, "kind"));
            Assert.Equal("list", RequiredString(descriptor, "container"));
            Assert.Equal(descriptor.GetProperty("length").GetInt32(), values.Count);
            DateTime[] dates = descriptor.GetProperty("dates")
                .EnumerateArray()
                .Select(item => DateTime.ParseExact(
                    item.GetString()!,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None))
                .ToArray();
            Assert.Equal(dates, values.ToArray());
        }

        private void AssertWindowDescriptor(
            IReadOnlyList<ScheduleValueWindow> values,
            JsonElement descriptor)
        {
            if (RequiredString(descriptor, "kind") == "compact-periods")
            {
                JsonElement[] periods = descriptor.GetProperty("periods").EnumerateArray().ToArray();
                Assert.Equal(periods.Length, values.Count);
                var localRules = new Dictionary<string, RuleSet>(StringComparer.Ordinal);
                for (int index = 0; index < periods.Length; index++)
                {
                    Assert.Equal(ReadDateLike(periods[index].GetProperty("start")), values[index].Start);
                    Assert.Equal(ReadDateLike(periods[index].GetProperty("end")), values[index].End);
                    RuleSet rule = Assert.IsType<RuleSet>(values[index].Value);
                    string identity = RequiredString(periods[index], "ruleset_identity_group");
                    MapIdentity(localRules, identity, rule);
                    MapRule(identity, rule);
                }

                AssertRuleGraph(descriptor.GetProperty("object_graph"), this, localRules);
                return;
            }

            Assert.Equal("sequence", RequiredString(descriptor, "kind"));
            Assert.Equal("list", RequiredString(descriptor, "container"));
            JsonElement[] expectedWindows = descriptor.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(expectedWindows.Length, values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                JsonElement expectedWindow = expectedWindows[index];
                Assert.Equal("sequence", RequiredString(expectedWindow, "kind"));
                Assert.Equal("tuple", RequiredString(expectedWindow, "container"));
                JsonElement[] items = expectedWindow.GetProperty("items").EnumerateArray().ToArray();
                Assert.Equal(3, items.Length);
                Assert.Equal(ReadDateLike(items[0]), values[index].Start);
                Assert.Equal(ReadDateLike(items[1]), values[index].End);
                if (RequiredString(items[2], "kind") == "day-schedule")
                {
                    AssertDayInputDescriptor(Assert.IsType<DaySchedule>(values[index].Value), items[2]);
                }
                else
                {
                    AssertScalarInputDescriptor(values[index].Value, items[2]);
                }
            }
        }

    }

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

    private sealed record ReferenceRun<T>(int Count, T Value)
        where T : class;

    private sealed record ScheduleReferenceSnapshot(
        string Name,
        ScheduleType Type,
        RuleSet[] Rules,
        RuleSetReferenceSnapshot[] UniqueRules);

    private sealed record RuleSetReferenceSnapshot(
        RuleSet Rule,
        string Name,
        ScheduleType Type,
        DaySchedule?[] Slots);
}
