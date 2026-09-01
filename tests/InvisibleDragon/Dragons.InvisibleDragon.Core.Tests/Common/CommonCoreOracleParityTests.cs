using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Common;

public sealed class CommonCoreOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/common-core-oracle.json";
    private const string OracleSha256 =
        "sha256:3510b6b3c561019457501391d2847c5e45ed2dc6dd4479842df9bf7db8446f7e";
    private const string CasesSha256 =
        "sha256:143964427b8165d29a99192ad21414eacc1f9cdc21520f3f3681447ad28e4ea4";
    private const int OracleByteLength = 34_828;
    private const int ExpectedCaseCount = 39;
    private const string OracleSchema =
        "dragons.invisibledragon.common-core-oracle.v1";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Common.CommonCoreOracleParityTests.MatchesPinnedPythonCommonCore";
    private const string UpstreamPath = "src/idragon/common.py";

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/common.py", "Setting", "sha256:6e21a2020f51e224497609cab212d06906e185320247c6604a763f2498b8a965", "common-core-setting-6e21a202"),
        new("src/idragon/common.py", "Setting.DEFAULT_EP_VERSION", "sha256:f61d5ffdf018890e5d6e521ee25af49c93de60ce1d8a10c00e41b8d484d64ba6", "common-core-setting-default-ep-version-f61d5ffd"),
        new("src/idragon/common.py", "Setting.DEFAULT_YEAR", "sha256:06415c37d66501858c44650d009662b50212cac62baf713ca9e75276e737eb14", "common-core-setting-default-year-06415c37"),
        new("src/idragon/common.py", "Version", "sha256:1c497416f9054aec72cc23eb32f3740e6001e70183471e0453128ec74d7770c8", "common-core-version-1c497416"),
        new("src/idragon/common.py", "Version.__format__", "sha256:da210c4fe8b52304df65a5ebcd0ac74511eed62730dd724b3c6f8ce3fbabc528", "common-core-version-format-da210c4f"),
        new("src/idragon/common.py", "Version.__init__", "sha256:a3def1029c1ebaf97d2c94d1efdc88f0c302c44e0c93d2045c38be0b12a0e983", "common-core-version-init-a3def102"),
        new("src/idragon/common.py", "Version.__iter__", "sha256:6d3a4baddd16fa313692dee29016da7b507b724a99f3e96f90b3def0b20c84e0", "common-core-version-iter-6d3a4bad"),
        new("src/idragon/common.py", "Version.ep_dirname", "sha256:4b01fd15706bc10675d11074bffb225f0ff0cf52d42c9367e9f815e420c43f38", "common-core-version-ep-dirname-4b01fd15"),
        new("src/idragon/common.py", "Version.iddname", "sha256:35a0ff29689c5bec73734a0541aed807b56f3f7d452f9803d3dc48cdfa2987cf", "common-core-version-iddname-35a0ff29"),
        new("src/idragon/common.py", "Version.major", "sha256:eb78e2b16110644dbb1186f0957d03be39f3b277422c81019a9da6b15d4e8723", "common-core-version-major-eb78e2b1"),
        new("src/idragon/common.py", "Version.minor", "sha256:2574c06325619eff67f689a237849ff548f990cc4f121556aa1b8a563d9828c0", "common-core-version-minor-2574c063"),
        new("src/idragon/common.py", "Version.patch", "sha256:e799dbd50398b1bce90539df69a7c61165ec72ea1933bba3cf17bbdea580b8de", "common-core-version-patch-e799dbd5"),
        new("src/idragon/common.py", "Version.to_version_anyway", "sha256:d59930546366ae649f0b4c1b7f0c3e38b46194099dc987873b519e47883fcc61", "common-core-version-to-version-anyway-d5993054"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("Setting", "class", "sha256:ee5384599d7bf86f25c4c9be3c78b9ca50772d5770a0cdd4e4ba6df05ca13228", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "equivalent", null),
        new("Setting.DEFAULT_EP_VERSION", "constant", "sha256:23bdff34828c36f054d2cbb1d25fba1a6c760b3caafa55eaf7e79275d9bdc112", "sha256:7ae62845ed4693ec74ed0a0816732e1e6b73208c56e2a77a7618fc521026fde2", "equivalent", null),
        new("Setting.DEFAULT_YEAR", "constant", "sha256:1b8a61e6ffe40d8e48d9355f31cc15c065d4667bab652fc07b81b08e40d7e92c", "sha256:0d1272fa2e01e32086f61a1b3ba1e1f0fa830eac73d098d45c29c22d4c5e6b36", "equivalent", null),
        new("Version", "class", "sha256:127a8b300808358bf3f1a153c025fb3d53ef73e7fd1ba8cc098576acb458a6ed", "sha256:fb7b04e087cf5ee44ca605240380ca8847066ea9c7c879315419dc0b52446c3c", "exception", "native-energyplus-version-descriptor"),
        new("Version.__format__", "function", "sha256:898cc4fc44ddc0f34fa112615fb7b40d48b275e241775ced67b86d2912549d7e", "sha256:c839272a4d8790a62fefc1020c9eb590c9f978c6fe48f967062d5b936c3771b9", "equivalent", null),
        new("Version.__init__", "function", "sha256:03d7516c1730f6f95147d7ebd855ace566e32c4f896eab3ff830b5ba6e716413", "sha256:fca44c5193da96a1ce893264f7969f6edb34bc2f579bc0447f87386e417adbce", "exception", "validated-energyplus-version-construction"),
        new("Version.__iter__", "function", "sha256:d9c32b0d50573f40cb5a4661cd4e1a7d0fed48c9126e12d1232aa81d4986ab85", "sha256:08cbcae78468818d6528e4acca4edbebc02f602d9f5c88ce4a48a0708b48dc9c", "equivalent", null),
        new("Version.ep_dirname", "function", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:29ae518a6fbdb45dc66b0b7da90a3440e5b467f8a9d548e50c16841dbc0d2d1b", "equivalent", null),
        new("Version.iddname", "function", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:ca48532a9eba41657918bcc72e8f326620e023a4b16df154772eee584ef1c280", "equivalent", null),
        new("Version.major", "function", "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876", "sha256:25aebf43a7db451d8989bb906db40d99b7f30f80903bd26fad7fcd9ca367012c", "equivalent", null),
        new("Version.minor", "function", "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876", "sha256:dcc7fe6ca11597a4e305f98448113b96de26e37cc25544a5092372ed8932ef3c", "equivalent", null),
        new("Version.patch", "function", "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876", "sha256:52d92682a931ea9189cec4de0714158104ba614d5ae6fed24a1e0a47779fb9be", "equivalent", null),
        new("Version.to_version_anyway", "function", "sha256:692fddbade2d31fda71ec2d931a2797265fd87743d9f23211f29d3d7851c9dc1", "sha256:126857416d367ce852290756b157a9a33e5a191247d1ec56e9752711b7bbaec5", "exception", "strongly-typed-energyplus-version-coercion"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("setting-default-ep-version.components", "setting-default-ep-version", "Setting.DEFAULT_EP_VERSION"),
        new("setting-default-ep-version.formatted-identities", "setting-default-ep-version", "Setting.DEFAULT_EP_VERSION"),
        new("setting-default-ep-version.semantic-shape", "setting-default-ep-version", "Setting.DEFAULT_EP_VERSION"),
        new("setting-default-year.calendar", "setting-default-year", "Setting.DEFAULT_YEAR"),
        new("setting-default-year.run-period", "setting-default-year", "Setting.DEFAULT_YEAR"),
        new("setting-default-year.scalar", "setting-default-year", "Setting.DEFAULT_YEAR"),
        new("setting.baseline-values", "setting", "Setting"),
        new("setting.default-version-roundtrip", "setting", "Setting"),
        new("setting.engineering-shape", "setting", "Setting"),
        new("version-class.descriptor", "version-class", "Version"),
        new("version-class.identity-equality", "version-class", "Version"),
        new("version-class.readonly-properties", "version-class", "Version"),
        new("version-coerce.existing-identity", "version-coerce", "Version.to_version_anyway"),
        new("version-coerce.failure-surface", "version-coerce", "Version.to_version_anyway"),
        new("version-coerce.strings-and-sequences", "version-coerce", "Version.to_version_anyway"),
        new("version-ep-dirname.default", "version-ep-dirname", "Version.ep_dirname"),
        new("version-ep-dirname.legacy", "version-ep-dirname", "Version.ep_dirname"),
        new("version-ep-dirname.zero-and-large", "version-ep-dirname", "Version.ep_dirname"),
        new("version-format.default-direct", "version-format", "Version.__format__"),
        new("version-format.delimiters", "version-format", "Version.__format__"),
        new("version-format.empty-spec", "version-format", "Version.__format__"),
        new("version-iddname.default", "version-iddname", "Version.iddname"),
        new("version-iddname.legacy", "version-iddname", "Version.iddname"),
        new("version-iddname.zero-and-large", "version-iddname", "Version.iddname"),
        new("version-init.failure-surface", "version-init", "Version.__init__"),
        new("version-init.integer-overloads", "version-init", "Version.__init__"),
        new("version-init.string-tokenization", "version-init", "Version.__init__"),
        new("version-iter.conversions", "version-iter", "Version.__iter__"),
        new("version-iter.fresh-generators", "version-iter", "Version.__iter__"),
        new("version-iter.ordered-exhaustion", "version-iter", "Version.__iter__"),
        new("version-major.default-baseline", "version-major", "Version.major"),
        new("version-major.explicit-three", "version-major", "Version.major"),
        new("version-major.two-component-default", "version-major", "Version.major"),
        new("version-minor.default-baseline", "version-minor", "Version.minor"),
        new("version-minor.explicit-three", "version-minor", "Version.minor"),
        new("version-minor.two-component-default", "version-minor", "Version.minor"),
        new("version-patch.default-baseline", "version-patch", "Version.patch"),
        new("version-patch.explicit-three", "version-patch", "Version.patch"),
        new("version-patch.two-component-default", "version-patch", "Version.patch"),
    };

    [Fact]
    public void MatchesPinnedPythonCommonCore()
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
            Assert.NotEmpty(nativeFacts);
            Assert.Equal(
                nativeFacts.Length,
                nativeFacts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(nativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
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
            AssertReceiptPayloadSafe(receiptJson);
            AssertNoRawAddresses(receiptJson.GetRawText());
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

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal(
            "847b01f68f438f560a986072bcaa7768fbf67897",
            RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02",
            RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(
            "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d",
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

        ValidateEvidenceBindings();
        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

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
            "policy-token-no-raw-address",
            RequiredString(consumer, "runtime_names"));

        JsonElement classifications = consumer.GetProperty("classifications");
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.Classification, RequiredString(classifications, symbol.Symbol));
        }

        Assert.Equal(
            10,
            ExpectedSymbols.Count(item => item.Classification == "equivalent"));
        Assert.Equal(
            3,
            ExpectedSymbols.Count(item => item.Classification == "exception"));
        JsonElement adaptations = consumer.GetProperty("adaptations");
        SymbolContract[] adapted = ExpectedSymbols
            .Where(item => item.AdaptationId is not null)
            .ToArray();
        AssertKeys(adaptations, adapted.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in adapted)
        {
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
        }
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == expected.Symbol);
        AssertKeys(
            item,
            symbol.AdaptationId is null
                ? new[] { "executor", "id", "python", "symbol" }
                : new[] { "executor", "expected_dotnet", "id", "python", "symbol" });
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Executor, RequiredString(item, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        Assert.Equal(JsonValueKind.Object, python.GetProperty("facts").ValueKind);
        Assert.NotEmpty(python.GetProperty("facts").EnumerateObject());

        if (symbol.AdaptationId is not null)
        {
            JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
            AssertKeys(expectedDotnet, "adaptation", "outcome");
            Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotnet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));
        }
    }

    private static string[] ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Executor switch
        {
            "setting" => ExecuteSetting(binding.CaseId, pythonFacts),
            "setting-default-ep-version" => ExecuteDefaultVersion(binding.CaseId, pythonFacts),
            "setting-default-year" => ExecuteDefaultYear(binding.CaseId, pythonFacts),
            "version-class" => ExecuteVersionClass(binding.CaseId, pythonFacts),
            "version-coerce" => ExecuteVersionCoerce(binding.CaseId, pythonFacts),
            "version-ep-dirname" => ExecuteDirectoryName(binding.CaseId, pythonFacts),
            "version-format" => ExecuteFormat(binding.CaseId, pythonFacts),
            "version-iddname" => ExecuteIddName(binding.CaseId, pythonFacts),
            "version-init" => ExecuteConstructor(binding.CaseId, pythonFacts),
            "version-iter" => ExecuteIteration(binding.CaseId, pythonFacts),
            "version-major" => ExecuteProperty(binding.CaseId, pythonFacts, nameof(EnergyPlusVersion.Major)),
            "version-minor" => ExecuteProperty(binding.CaseId, pythonFacts, nameof(EnergyPlusVersion.Minor)),
            "version-patch" => ExecuteProperty(binding.CaseId, pythonFacts, nameof(EnergyPlusVersion.Patch)),
            _ => throw new Xunit.Sdk.XunitException(
                "Unknown common-core executor '" + binding.Executor + "'."),
        };
    }

    private static string[] ExecuteSetting(string caseId, JsonElement pythonFacts)
    {
        EnergyPlusVersion version = EnergyPlusDefaults.DefaultVersion;
        int year = EnergyPlusDefaults.DefaultYear;
        if (caseId == "setting.baseline-values")
        {
            return Equivalent(
                pythonFacts,
                new { default_ep_version = version.ToArray(), default_year = year },
                "native defaults retained version 24-2-0 and year 2026");
        }

        if (caseId == "setting.default-version-roundtrip")
        {
            EnergyPlusVersion roundTrip = EnergyPlusVersion.From(version);
            Assert.Same(version, roundTrip);
            AssertKeys(pythonFacts, "version");
            AssertVersionSnapshot(pythonFacts.GetProperty("version"), roundTrip);
            return new[]
            {
                "native default version round-tripped by identity",
                "native default version engineering projections matched Python",
                "pinned Python component labels remained int while native components remained Int32",
            };
        }

        Assert.Equal("setting.engineering-shape", caseId);
        int days = DateTime.IsLeapYear(year) ? 366 : 365;
        return Equivalent(
            pythonFacts,
            new
            {
                component_count = version.Count,
                patch_default_is_zero = version.Patch == 0,
                year_day_count = days,
                year_is_non_leap = !DateTime.IsLeapYear(year),
            },
            "native defaults retained three version components",
            "native default year retained the non-leap 365-day calendar");
    }

    private static string[] ExecuteDefaultVersion(string caseId, JsonElement pythonFacts)
    {
        EnergyPlusVersion version = EnergyPlusDefaults.DefaultVersion;
        if (caseId == "setting-default-ep-version.components")
        {
            return Equivalent(
                pythonFacts,
                new { component_count = version.Count, components = version.ToArray() },
                "native default version retained ordered components 24 2 0");
        }

        if (caseId == "setting-default-ep-version.formatted-identities")
        {
            return Equivalent(
                pythonFacts,
                new
                {
                    dotted = version.Format("."),
                    ep_dirname = version.EnergyPlusDirectoryName,
                    hyphenated = version.Format(),
                    iddname = version.LegacyIddFileName,
                },
                "native default version retained dotted and hyphenated forms",
                "native default version retained legacy file and directory identities");
        }

        Assert.Equal("setting-default-ep-version.semantic-shape", caseId);
        return Equivalent(
            pythonFacts,
            new
            {
                all_nonnegative = version.All(item => item >= 0),
                component_count = version.Count,
                patch_is_zero = version.Patch == 0,
            },
            "native default version retained nonnegative three-part shape");
    }

    private static string[] ExecuteDefaultYear(string caseId, JsonElement pythonFacts)
    {
        int year = EnergyPlusDefaults.DefaultYear;
        if (caseId == "setting-default-year.calendar")
        {
            return Equivalent(
                pythonFacts,
                new
                {
                    day_count = DateTime.IsLeapYear(year) ? 366 : 365,
                    is_leap = DateTime.IsLeapYear(year),
                    year,
                },
                "native default year retained the 2026 non-leap calendar");
        }

        if (caseId == "setting-default-year.run-period")
        {
            IdfDocument document = DefaultIdfDocument();
            IdfObject runPeriod = Assert.Single(document["RunPeriod"]);
            int startYear = int.Parse(runPeriod[3]!, CultureInfo.InvariantCulture);
            int endYear = int.Parse(runPeriod[6]!, CultureInfo.InvariantCulture);
            Assert.Equal(year, startYear);
            Assert.Equal(year, endYear);
            return Equivalent(
                pythonFacts,
                new
                {
                    end = new[] { endYear, 12, 31 },
                    start = new[] { startYear, 1, 1 },
                },
                "native IDF RunPeriod retained 2026 start and end years");
        }

        Assert.Equal("setting-default-year.scalar", caseId);
        return Equivalent(
            pythonFacts,
            new
            {
                next_year = year + 1,
                previous_year = year - 1,
                text = year.ToString(CultureInfo.InvariantCulture),
                value = year,
            },
            "native default year retained scalar arithmetic and invariant text");
    }

    private static string[] ExecuteVersionClass(string caseId, JsonElement pythonFacts)
    {
        Type type = typeof(EnergyPlusVersion);
        if (caseId == "version-class.descriptor")
        {
            Assert.False(pythonFacts.GetProperty("defines_equality").GetBoolean());
            Assert.True(pythonFacts.GetProperty("has_instance_dictionary").GetBoolean());
            Assert.Equal("Version", RequiredString(pythonFacts, "type_name"));
            Assert.Equal(
                new[]
                {
                    "ep_dirname",
                    "iddname",
                    "major",
                    "minor",
                    "patch",
                    "to_version_anyway",
                },
                pythonFacts.GetProperty("public_descriptors")
                    .EnumerateArray()
                    .Select(item => item.GetString()!));
            Assert.True(type.IsSealed);
            Assert.True(typeof(IReadOnlyList<int>).IsAssignableFrom(type));
            Assert.Equal(
                typeof(object),
                type.GetMethod(nameof(object.Equals), new[] { typeof(object) })!.DeclaringType);
            Assert.Equal(3, type.GetConstructors().Length);
            return new[]
            {
                "native EnergyPlusVersion is sealed and Rhino-independent",
                "native descriptor uses inherited identity equality",
                "native descriptor exposes IReadOnlyList Int32 components",
            };
        }

        if (caseId == "version-class.identity-equality")
        {
            Assert.True(pythonFacts.GetProperty("components_equal").GetBoolean());
            Assert.False(pythonFacts.GetProperty("separate_instances_equal").GetBoolean());
            Assert.True(pythonFacts.GetProperty("self_equal").GetBoolean());
            var left = new EnergyPlusVersion(24, 2, 0);
            var right = new EnergyPlusVersion(24, 2, 0);
            Assert.True(left.SequenceEqual(right));
            Assert.False(left.Equals(right));
            Assert.True(left.Equals(left));
            return new[]
            {
                "native equal component sequences retained distinct identities",
                "native self equality retained reference identity",
            };
        }

        Assert.Equal("version-class.readonly-properties", caseId);
        JsonElement[] pythonObservations = pythonFacts.GetProperty("observations")
            .EnumerateArray()
            .ToArray();
        string[] propertyNames =
        {
            "major",
            "minor",
            "patch",
        };
        Assert.Equal(propertyNames.Length, pythonObservations.Length);
        for (int index = 0; index < propertyNames.Length; index++)
        {
            string pythonPropertyName = propertyNames[index];
            AssertPythonRaisedObservation(
                pythonObservations[index],
                "type",
                "AttributeError",
                $"property '{pythonPropertyName}' of 'Version' object has no setter");
            PropertyInfo property = type.GetProperty(
                char.ToUpperInvariant(pythonPropertyName[0]) + pythonPropertyName.Substring(1))!;
            Assert.False(property.CanWrite);
            Exception? error = Record.Exception(() => property.SetValue(
                new EnergyPlusVersion(24, 2, 0),
                1));
            Assert.NotNull(error);
        }

        return new[]
        {
            "native Major Minor and Patch properties are read-only",
            "reflective writes cannot mutate native version components",
        };
    }

    private static string[] ExecuteVersionCoerce(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "version-coerce.existing-identity")
        {
            var existing = new EnergyPlusVersion(24, 2, 0);
            EnergyPlusVersion result = EnergyPlusVersion.From(existing);
            Assert.Same(existing, result);
            Assert.True(pythonFacts.GetProperty("same_identity").GetBoolean());
            AssertVersionSnapshot(pythonFacts.GetProperty("version"), result);
            return new[]
            {
                "native From existing retained the exact instance",
                "native retained all default version projections",
            };
        }

        if (caseId == "version-coerce.strings-and-sequences")
        {
            EnergyPlusVersion[] values =
            {
                EnergyPlusVersion.From("V24-2-0"),
                EnergyPlusVersion.From((IReadOnlyList<int>)new[] { 24, 2, 0 }),
                EnergyPlusVersion.From((IReadOnlyList<int>)new[] { 24, 2 }),
            };
            AssertPythonObservationResults(pythonFacts, values);
            Assert.All(values, value => Assert.Equal(new[] { 24, 2, 0 }, value.ToArray()));
            return new[]
            {
                "native From string retained version 24-2-0",
                "native From three-component list retained version 24-2-0",
                "native From two-component list defaulted patch to zero",
            };
        }

        Assert.Equal("version-coerce.failure-surface", caseId);
        JsonElement[] failureObservations = pythonFacts.GetProperty("observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(4, failureObservations.Length);
        Assert.All(failureObservations, observation => AssertPythonRaisedObservation(
            observation,
            "type",
            "TypeError",
            "sequence item 0: expected str instance, type found"));
        Assert.Throws<ArgumentException>(() =>
            EnergyPlusVersion.From((IReadOnlyList<int>)new[] { 24 }));
        Assert.Throws<ArgumentNullException>(() =>
            EnergyPlusVersion.From((EnergyPlusVersion)null!));
        Assert.Throws<ArgumentNullException>(() =>
            EnergyPlusVersion.From((string)null!));
        Assert.Throws<ArgumentNullException>(() =>
            EnergyPlusVersion.From((IReadOnlyList<int>)null!));
        Type[] parameterTypes = typeof(EnergyPlusVersion)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(EnergyPlusVersion.From))
            .Select(method => Assert.Single(method.GetParameters()).ParameterType)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                typeof(EnergyPlusVersion),
                typeof(IReadOnlyList<int>),
                typeof(string),
            }.OrderBy(type => type.FullName, StringComparer.Ordinal),
            parameterTypes);
        return new[]
        {
            "native From rejected invalid component count and null inputs",
            "native From exposes only existing string and IReadOnlyList Int32 overloads",
            "mixed and unrelated runtime objects are excluded by the typed surface",
        };
    }

    private static string[] ExecuteDirectoryName(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "version-ep-dirname.default")
        {
            EnergyPlusVersion value = new(24, 2, 0);
            return Equivalent(
                pythonFacts,
                new { components = value.ToArray(), value = value.EnergyPlusDirectoryName },
                "native default directory name matched EnergyPlusV24-2-0");
        }

        if (caseId == "version-ep-dirname.legacy")
        {
            var value = new EnergyPlusVersion("V9.6");
            return Equivalent(
                pythonFacts,
                new { components = value.ToArray(), value = value.EnergyPlusDirectoryName },
                "native legacy directory name matched EnergyPlusV9-6-0");
        }

        Assert.Equal("version-ep-dirname.zero-and-large", caseId);
        return Equivalent(
            pythonFacts,
            new
            {
                observations = new[]
                {
                    new { outcome = "returned", result = new EnergyPlusVersion(0, 0, 0).EnergyPlusDirectoryName },
                    new { outcome = "returned", result = new EnergyPlusVersion(123, 45, 6).EnergyPlusDirectoryName },
                },
            },
            "native directory names retained zero and large supported versions");
    }

    private static string[] ExecuteFormat(string caseId, JsonElement pythonFacts)
    {
        var value = new EnergyPlusVersion(24, 2, 0);
        if (caseId == "version-format.default-direct")
        {
            return Equivalent(
                pythonFacts,
                new { direct_default = value.Format(), explicit_dash = value.Format("-") },
                "native default and explicit dash formatting matched 24-2-0");
        }

        if (caseId == "version-format.delimiters")
        {
            return Equivalent(
                pythonFacts,
                new
                {
                    dash = value.Format("-"),
                    dot = value.Format("."),
                    double_colon = value.Format("::"),
                    slash = value.Format("/"),
                },
                "native formatting retained dash dot double-colon and slash separators");
        }

        Assert.Equal("version-format.empty-spec", caseId);
        string empty = value.Format(string.Empty);
        return Equivalent(
            pythonFacts,
            new { builtin_format = empty, direct_empty = empty, fstring_empty = empty },
            "native empty separator formatting matched Python empty format-spec semantics");
    }

    private static string[] ExecuteIddName(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "version-iddname.default")
        {
            EnergyPlusVersion value = new(24, 2, 0);
            return Equivalent(
                pythonFacts,
                new { components = value.ToArray(), value = value.LegacyIddFileName },
                "native default IDD name matched V24-2-0-Energy+.idd");
        }

        if (caseId == "version-iddname.legacy")
        {
            var value = new EnergyPlusVersion("V9.6");
            return Equivalent(
                pythonFacts,
                new { components = value.ToArray(), value = value.LegacyIddFileName },
                "native legacy IDD name matched V9-6-0-Energy+.idd");
        }

        Assert.Equal("version-iddname.zero-and-large", caseId);
        return Equivalent(
            pythonFacts,
            new
            {
                observations = new[]
                {
                    new { outcome = "returned", result = new EnergyPlusVersion(0, 0, 0).LegacyIddFileName },
                    new { outcome = "returned", result = new EnergyPlusVersion(123, 45, 6).LegacyIddFileName },
                },
            },
            "native IDD names retained zero and large supported versions");
    }

    private static string[] ExecuteConstructor(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "version-init.string-tokenization")
        {
            EnergyPlusVersion[] values =
            {
                new("V9-6-0"),
                new("9.6"),
                new("prefix24__2++0suffix"),
                new("-1.-2"),
                new("24..2"),
                new("V\u0661\u0662-\u0662-\u0660"),
            };
            AssertPythonObservationResults(pythonFacts, values);
            Assert.Equal(new[] { 12, 2, 0 }, values[5].ToArray());
            return new[]
            {
                "native constructor retained prefix suffix and arbitrary delimiter tokenization",
                "native constructor treated signs as delimiters and defaulted two-token patch to zero",
                "native constructor retained Unicode decimal digit tokenization",
            };
        }

        if (caseId == "version-init.integer-overloads")
        {
            JsonElement[] observations = pythonFacts.GetProperty("observations")
                .EnumerateArray()
                .ToArray();
            Assert.Equal(4, observations.Length);
            EnergyPlusVersion[] supported =
            {
                new(9, 6),
                new(9, 6, 0),
            };
            for (int index = 0; index < supported.Length; index++)
            {
                Assert.Equal("returned", RequiredString(observations[index], "outcome"));
                AssertVersionSnapshot(
                    observations[index].GetProperty("result"),
                    supported[index]);
            }

            Assert.Equal(-1, observations[2].GetProperty("result").GetProperty("major").GetInt32());
            Assert.True(observations[3].GetProperty("result").GetProperty("major").GetBoolean());
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyPlusVersion(-1, 2, 3));
            Assert.DoesNotContain(
                typeof(EnergyPlusVersion).GetConstructors().SelectMany(item => item.GetParameters()),
                parameter => parameter.ParameterType == typeof(bool));
            return new[]
            {
                "native two and three Int32 overloads matched supported Python components",
                "native constructor rejected negative components",
                "native typed constructor excludes Python bool-as-int inputs",
            };
        }

        Assert.Equal("version-init.failure-surface", caseId);
        JsonElement[] failureObservations = pythonFacts.GetProperty("observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(6, failureObservations.Length);
        (string Category, string Type, string Message)[] expectedFailures =
        {
            ("domain", "ValueError", "Expected three integers, but got 0 in "),
            ("domain", "ValueError", "Expected three integers, but got 1 in 9"),
            ("domain", "ValueError", "Expected three integers, but got 4 in 1.2.3.4"),
            ("type", "TypeError", "sequence item 0: expected str instance, type found"),
            ("type", "TypeError", "sequence item 0: expected str instance, type found"),
            ("domain", "ValueError", "Expected one string or two/three integers, but got ."),
        };
        for (int index = 0; index < expectedFailures.Length; index++)
        {
            AssertPythonRaisedObservation(
                failureObservations[index],
                expectedFailures[index].Category,
                expectedFailures[index].Type,
                expectedFailures[index].Message);
        }
        Assert.Throws<ArgumentException>(() => new EnergyPlusVersion(string.Empty));
        Assert.Throws<ArgumentException>(() => new EnergyPlusVersion("9"));
        Assert.Throws<ArgumentException>(() => new EnergyPlusVersion("1.2.3.4"));
        Assert.Throws<ArgumentNullException>(() => new EnergyPlusVersion(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyPlusVersion(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EnergyPlusVersion("2147483648.2.0"));
        ConstructorInfo[] constructors = typeof(EnergyPlusVersion).GetConstructors();
        Assert.Contains(constructors, constructor => ConstructorTypes(constructor, typeof(string)));
        Assert.Contains(constructors, constructor => ConstructorTypes(constructor, typeof(int), typeof(int)));
        Assert.Contains(constructors, constructor => ConstructorTypes(constructor, typeof(int), typeof(int), typeof(int)));
        return new[]
        {
            "native constructor rejected zero one and four numeric-token strings",
            "native constructor rejected null negative and Int32-overflow inputs",
            "native constructor exposes only string two-Int32 and three-Int32 overloads",
        };
    }

    private static string[] ExecuteIteration(string caseId, JsonElement pythonFacts)
    {
        var value = new EnergyPlusVersion(24, 2, 0);
        if (caseId == "version-iter.conversions")
        {
            int[] list = value.ToList().ToArray();
            int[] tuple = value.ToArray();
            return Equivalent(
                pythonFacts,
                new { list, tuple },
                "native IReadOnlyList and enumeration conversions retained 24 2 0 order");
        }

        if (caseId == "version-iter.fresh-generators")
        {
            IEnumerator<int> first = value.GetEnumerator();
            IEnumerator<int> second = value.GetEnumerator();
            Assert.NotSame(first, second);
            int[] firstValues = Drain(first);
            int[] secondValues = Drain(second);
            AssertKeys(
                pythonFacts,
                "first_is_second",
                "first_type",
                "first_values",
                "second_values");
            Assert.False(pythonFacts.GetProperty("first_is_second").GetBoolean());
            Assert.Equal("generator", RequiredString(pythonFacts, "first_type"));
            AssertJsonEquivalent(
                pythonFacts.GetProperty("first_values"),
                JsonSerializer.SerializeToElement(firstValues));
            AssertJsonEquivalent(
                pythonFacts.GetProperty("second_values"),
                JsonSerializer.SerializeToElement(secondValues));
            Assert.IsAssignableFrom<IEnumerator<int>>(first);
            Assert.IsAssignableFrom<IEnumerator<int>>(second);
            return new[]
            {
                "native repeated enumeration returned distinct iterator instances",
                "native IEnumerator Int32 instances retained the pinned ordered components",
                "pinned Python iterator label remained generator without being copied into native facts",
            };
        }

        Assert.Equal("version-iter.ordered-exhaustion", caseId);
        IEnumerator<int> iterator = value.GetEnumerator();
        var values = new List<int>();
        while (iterator.MoveNext())
        {
            values.Add(iterator.Current);
        }

        bool exhausted = !iterator.MoveNext();
        return Equivalent(
            pythonFacts,
            new { exhausted, values = values.ToArray() },
            "native iterator yielded Major Minor Patch then remained exhausted");
    }

    private static string[] ExecuteProperty(
        string caseId,
        JsonElement pythonFacts,
        string propertyName)
    {
        EnergyPlusVersion value;
        if (caseId.EndsWith(".default-baseline", StringComparison.Ordinal))
        {
            value = new EnergyPlusVersion(24, 2, 0);
        }
        else if (caseId.EndsWith(".explicit-three", StringComparison.Ordinal))
        {
            value = new EnergyPlusVersion(9, 6, 7);
        }
        else
        {
            Assert.EndsWith(".two-component-default", caseId, StringComparison.Ordinal);
            value = new EnergyPlusVersion(9, 6);
        }

        int propertyValue = propertyName switch
        {
            nameof(EnergyPlusVersion.Major) => value.Major,
            nameof(EnergyPlusVersion.Minor) => value.Minor,
            nameof(EnergyPlusVersion.Patch) => value.Patch,
            _ => throw new Xunit.Sdk.XunitException(
                "Unknown EnergyPlusVersion property '" + propertyName + "'."),
        };
        Assert.False(typeof(EnergyPlusVersion).GetProperty(propertyName)!.CanWrite);
        return Equivalent(
            pythonFacts,
            new { components = value.ToArray(), value = propertyValue },
            $"native read-only {propertyName} retained the pinned component value for {caseId}");
    }

    private static IdfDocument DefaultIdfDocument()
    {
        IdfDocument document = new EnergyModel(
            "common defaults",
            Array.Empty<Zone>()).ToIdfDocument();
        IdfObject version = Assert.Single(document["Version"]);
        Assert.Equal("24.2", version[0]);
        Assert.Equal(Schedule.DefaultYear, EnergyPlusDefaults.DefaultYear);
        return document;
    }

    private static void AssertVersionSnapshot(
        JsonElement pythonSnapshot,
        EnergyPlusVersion native)
    {
        AssertKeys(
            pythonSnapshot,
            "component_types",
            "components",
            "ep_dirname",
            "format_dash",
            "format_dot",
            "iddname",
            "major",
            "minor",
            "patch");
        Assert.Equal(
            new[] { "int", "int", "int" },
            pythonSnapshot.GetProperty("component_types")
                .EnumerateArray()
                .Select(item => item.GetString()!));
        Assert.All(native, item => Assert.IsType<int>(item));
        Assert.Equal(
            native.ToArray(),
            pythonSnapshot.GetProperty("components")
                .EnumerateArray()
                .Select(item => item.GetInt32()));
        Assert.Equal(native.EnergyPlusDirectoryName, RequiredString(pythonSnapshot, "ep_dirname"));
        Assert.Equal(native.Format(), RequiredString(pythonSnapshot, "format_dash"));
        Assert.Equal(native.Format("."), RequiredString(pythonSnapshot, "format_dot"));
        Assert.Equal(native.LegacyIddFileName, RequiredString(pythonSnapshot, "iddname"));
        Assert.Equal(native.Major, pythonSnapshot.GetProperty("major").GetInt32());
        Assert.Equal(native.Minor, pythonSnapshot.GetProperty("minor").GetInt32());
        Assert.Equal(native.Patch, pythonSnapshot.GetProperty("patch").GetInt32());
    }

    private static void AssertPythonObservationResults(
        JsonElement pythonFacts,
        IReadOnlyList<EnergyPlusVersion> nativeValues)
    {
        JsonElement[] observations = pythonFacts.GetProperty("observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(nativeValues.Count, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            AssertKeys(observations[index], "outcome", "result");
            Assert.Equal("returned", RequiredString(observations[index], "outcome"));
            AssertVersionSnapshot(
                observations[index].GetProperty("result"),
                nativeValues[index]);
        }
    }

    private static void AssertPythonRaisedObservation(
        JsonElement observation,
        string errorCategory,
        string exceptionType,
        string message)
    {
        AssertKeys(
            observation,
            "error_category",
            "exception_type",
            "message",
            "outcome");
        Assert.Equal("raised", RequiredString(observation, "outcome"));
        Assert.Equal(errorCategory, RequiredString(observation, "error_category"));
        Assert.Equal(exceptionType, RequiredString(observation, "exception_type"));
        Assert.Equal(message, RequiredString(observation, "message"));
    }

    private static bool ConstructorTypes(ConstructorInfo constructor, params Type[] types) =>
        constructor.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(types);

    private static int[] Drain(IEnumerator<int> iterator)
    {
        var result = new List<int>();
        while (iterator.MoveNext())
        {
            result.Add(iterator.Current);
        }

        return result.ToArray();
    }

    private static string[] Equivalent(
        JsonElement pythonFacts,
        object nativeFacts,
        params string[] receiptFacts)
    {
        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(nativeFacts));
        return receiptFacts;
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
                    "Unsupported JSON fact kind '" + expected.ValueKind + "'.");
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
            @"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase));
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
        string? AdaptationId);

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string? AdaptationId,
        IReadOnlyList<string> NativeFacts);
}
