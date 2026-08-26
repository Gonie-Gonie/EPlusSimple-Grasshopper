using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace GonieGonie.InvisibleDragon.Tests.Results;

public sealed class EnergyPlusResultParserOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/launcher-result-parser-oracle.json";
    private const string OracleSha256 =
        "sha256:a5e4024e46ed30d2cfed54b8f158a167de796bde898cc5cc451701ded4d3df6e";
    private const string CasesSha256 =
        "sha256:a0464a29bfd0bd1712deacbac50d3f87f6ea15e4ba9f4d19a70e88e896be38dd";
    private const int OracleByteLength = 43_608;
    private const int ExpectedCaseCount = 21;
    private const string OracleSchema =
        "goniegonie.python-reference.launcher-result-parser.v1";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Results.EnergyPlusResultParserOracleParityTests.MatchesPinnedPythonLauncherResultParser";
    private const string UpstreamPath = "src/idragon/launcher.py";
    private const string ParserTypeName =
        "GonieGonie.InvisibleDragon.Results.EnergyPlusResultParser";
    private const string ResultTypeName =
        "GonieGonie.InvisibleDragon.Results.EnergyPlusSimulationResult";

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/launcher.py", "EnergyPlusResult", "sha256:eab88d95447b32529789bf2881a5d4d3e13651c4e10d9bd732a6a647f1a1f597", "launcher-result-energyplus-result-eab88d95"),
        new("src/idragon/launcher.py", "EnergyPlusResult.__init__", "sha256:30d49efa5495acff6cb5c9c03c9aacb3bd633048bb4fec63ce2d31b36f85a31a", "launcher-result-init-30d49efa"),
        new("src/idragon/launcher.py", "EnergyPlusResult.parse_audit", "sha256:7315fbc33d50d14f5dfcab78401ef66838a9e2c3760bea72e6b3b3d3aea59fce", "launcher-result-parse-audit-7315fbc3"),
        new("src/idragon/launcher.py", "EnergyPlusResult.parse_bnd", "sha256:631c7884e2ca51b8410312a506edf7fbf79928a5c49d13c132e3dc897a325ac9", "launcher-result-parse-bnd-631c7884"),
        new("src/idragon/launcher.py", "EnergyPlusResult.parse_err", "sha256:f578930710efdaf9f65ec2ec992fc1fcadb22270c26b788a2513125e029e0561", "launcher-result-parse-err-f5789307"),
        new("src/idragon/launcher.py", "EnergyPlusResult.parse_eso", "sha256:3e849bcd62a1caba6f2d56d3f5353d485f601aa2c9375ffecfa17ab4968d645c", "launcher-result-parse-eso-3e849bcd"),
        new("src/idragon/launcher.py", "EnergyPlusResult.parse_table", "sha256:eaf18f211cbe4342c5c5be02cf6ccf1ea10f6604cbf199051bacac03bcabea3a", "launcher-result-parse-table-eaf18f21"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("EnergyPlusResult", "class", "sha256:e88e44c74b7fe4452c4b4ab02a77089cc4d00bf85c9ae6e0d66da6f9434f3058", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "immutable-structured-energyplus-result", ResultTypeName),
        new("EnergyPlusResult.__init__", "function", "sha256:7e5b8274a07d7fb62744d1264fbb0b20e61cd8c98043fadd006e927ad1f5b306", "sha256:07b3bd52cbf73e0aefff8c1ad129513d27a70b4f2eb20367823906d0eb554ded", "validated-energyplus-result-file-loading", ParserTypeName + ".ParseDirectory"),
        new("EnergyPlusResult.parse_audit", "function", "sha256:1e71b92b3165cc8d006395d5ea0af9a81aff2a071bab1786e6fa831eb183d23f", "sha256:1623e7f8578b27f3cfb1fb619812d5bd86ff454bd5013ccceae56368e95b13c0", "ordered-typed-energyplus-audit-parsing", ParserTypeName + ".ParseAudit"),
        new("EnergyPlusResult.parse_bnd", "function", "sha256:46ce611e7c31e66b237299dd1fcfd62fa99a3ace82a364e0bb5f00608b7dbcb1", "sha256:fbb91620c064f38be0b8be747a217029a890610b5e534156afc8f33e01d8b61d", "csv-aware-energyplus-boundary-parsing", ParserTypeName + ".ParseBoundary"),
        new("EnergyPlusResult.parse_err", "function", "sha256:eb51fd10a2d723663f382cb82e19b2ce1c5fad4663d610da9299279287665fa5", "sha256:8e8874e9cc6f51ad980d80a7ea7d2f31edfb2fce7cd7b6146322386a68e9d4b3", "structured-energyplus-error-log-parsing", ParserTypeName + ".ParseErrorLog"),
        new("EnergyPlusResult.parse_eso", "function", "sha256:537014c1413f0afaa5e35f17842785640f506e5da03814c6547486d26c06955c", "sha256:69ee3e6f0fb7d958909b14349d6313a2b89d827e22269768d74cb04ec38a4b37", "explicitly-unsupported-energyplus-eso", ParserTypeName),
        new("EnergyPlusResult.parse_table", "function", "sha256:8d4aab53f7cf5437b4388d7289738711f41221a5fabf818c61bfda9a5a64a8b9", "sha256:6cd0846d65f6edd670890f719cd0c2f793c2fc03a7b07d3f18a7620b38ae4559", "typed-energyplus-tabular-parsing", ParserTypeName + ".ParseTabular"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("energyplus-result.class-descriptors", "class", "EnergyPlusResult"),
        new("energyplus-result.class-dynamic-identity", "class", "EnergyPlusResult"),
        new("energyplus-result.class-static-bindings", "class", "EnergyPlusResult"),
        new("energyplus-result.init-defaults", "init", "EnergyPlusResult.__init__"),
        new("energyplus-result.init-dispatch-overwrite", "init", "EnergyPlusResult.__init__"),
        new("energyplus-result.init-failure-transactionality", "init", "EnergyPlusResult.__init__"),
        new("energyplus-result.parse-audit-duplicates-unicode", "parse-audit", "EnergyPlusResult.parse_audit"),
        new("energyplus-result.parse-audit-failure-surface", "parse-audit", "EnergyPlusResult.parse_audit"),
        new("energyplus-result.parse-audit-recognition-boundaries", "parse-audit", "EnergyPlusResult.parse_audit"),
        new("energyplus-result.parse-bnd-duplicates-padding", "parse-bnd", "EnergyPlusResult.parse_bnd"),
        new("energyplus-result.parse-bnd-failure-grammar", "parse-bnd", "EnergyPlusResult.parse_bnd"),
        new("energyplus-result.parse-bnd-records", "parse-bnd", "EnergyPlusResult.parse_bnd"),
        new("energyplus-result.parse-err-diagnostics", "parse-err", "EnergyPlusResult.parse_err"),
        new("energyplus-result.parse-err-failure-surface", "parse-err", "EnergyPlusResult.parse_err"),
        new("energyplus-result.parse-err-time-empty", "parse-err", "EnergyPlusResult.parse_err"),
        new("energyplus-result.parse-eso-arity", "parse-eso", "EnergyPlusResult.parse_eso"),
        new("energyplus-result.parse-eso-opaque", "parse-eso", "EnergyPlusResult.parse_eso"),
        new("energyplus-result.parse-eso-values", "parse-eso", "EnergyPlusResult.parse_eso"),
        new("energyplus-result.parse-table-csv-multi-report", "parse-table", "EnergyPlusResult.parse_table"),
        new("energyplus-result.parse-table-failure-surface", "parse-table", "EnergyPlusResult.parse_table"),
        new("energyplus-result.parse-table-grammar-duplicates", "parse-table", "EnergyPlusResult.parse_table"),
    };

    [Fact]
    public void MatchesPinnedPythonLauncherResultParser()
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
            JsonElement nativeFactsJson = JsonSerializer.SerializeToElement(nativeFacts);
            AssertNoRawAddresses(nativeFactsJson.GetRawText());
            AssertNoNonFiniteJsonNumbers(nativeFactsJson);
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
            AssertNoNonFiniteJsonNumbers(receiptJson);
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
        AssertNoNonFiniteJsonNumbers(root);

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
            "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f",
            RequiredString(upstream, "source_sha256"));

        ValidateRuntime(root.GetProperty("runtime"));
        ValidateEvidenceBindings();
        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));
        ValidateNativeBindings();

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

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
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

        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(
            dependencies,
            "colorama",
            "et_xmlfile",
            "numpy",
            "openpyxl",
            "pandas",
            "python-dateutil",
            "pytz",
            "six",
            "tqdm",
            "tzdata");
        Assert.Equal("0.4.6", RequiredString(dependencies, "colorama"));
        Assert.Equal("2.0.0", RequiredString(dependencies, "et_xmlfile"));
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("3.1.5", RequiredString(dependencies, "openpyxl"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("2.9.0.post0", RequiredString(dependencies, "python-dateutil"));
        Assert.Equal("2024.2", RequiredString(dependencies, "pytz"));
        Assert.Equal("1.16.0", RequiredString(dependencies, "six"));
        Assert.Equal("4.67.1", RequiredString(dependencies, "tqdm"));
        Assert.Equal("2024.2", RequiredString(dependencies, "tzdata"));
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
            "assertion_ids",
            "case_count",
            "case_ids",
            "classifications",
            "dataframe_encoding",
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
            "ordered-columns-index-dtypes-and-tagged-cells",
            RequiredString(consumer, "dataframe_encoding"));
        Assert.Equal(
            "python-binary64-hex-without-0x-prefix",
            RequiredString(consumer, "float_encoding"));
        Assert.Equal(
            "pinned-python-only-no-native-type-name-claims",
            RequiredString(consumer, "runtime_names"));

        JsonElement classifications = consumer.GetProperty("classifications");
        JsonElement adaptations = consumer.GetProperty("adaptations");
        JsonElement assertionIds = consumer.GetProperty("assertion_ids");
        string[] symbolNames = ExpectedSymbols.Select(item => item.Symbol).ToArray();
        AssertKeys(classifications, symbolNames);
        AssertKeys(adaptations, symbolNames);
        AssertKeys(assertionIds, symbolNames);
        Assert.Equal(7, ExpectedSymbols.Length);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            SymbolContract symbol = ExpectedSymbols[index];
            Assert.Equal("exception", RequiredString(classifications, symbol.Symbol));
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
            Assert.Equal(
                ExpectedEvidence[index].AssertionId,
                RequiredString(assertionIds, symbol.Symbol));
        }
    }

    private static void ValidateNativeBindings()
    {
        Assert.Equal(ResultTypeName, typeof(EnergyPlusSimulationResult).FullName);
        Assert.Equal(ParserTypeName, typeof(EnergyPlusResultParser).FullName);
        Assert.Equal(ResultTypeName, ExpectedSymbols[0].NativeTarget);
        Assert.Equal(ParserTypeName + ".ParseDirectory", ExpectedSymbols[1].NativeTarget);
        Assert.Equal(ParserTypeName + ".ParseAudit", ExpectedSymbols[2].NativeTarget);
        Assert.Equal(ParserTypeName + ".ParseBoundary", ExpectedSymbols[3].NativeTarget);
        Assert.Equal(ParserTypeName + ".ParseErrorLog", ExpectedSymbols[4].NativeTarget);
        Assert.Equal(ParserTypeName, ExpectedSymbols[5].NativeTarget);
        Assert.Equal(ParserTypeName + ".ParseTabular", ExpectedSymbols[6].NativeTarget);

        MethodInfo parseDirectory = PublicStaticParserMethod("ParseDirectory");
        MethodInfo parseAudit = PublicStaticParserMethod("ParseAudit");
        MethodInfo parseBoundary = PublicStaticParserMethod("ParseBoundary");
        MethodInfo parseErrorLog = PublicStaticParserMethod("ParseErrorLog");
        MethodInfo parseTabular = PublicStaticParserMethod("ParseTabular");
        Assert.Equal(typeof(EnergyPlusSimulationResult), parseDirectory.ReturnType);
        Assert.Equal(typeof(EnergyPlusAuditLog), parseAudit.ReturnType);
        Assert.Equal(typeof(EnergyPlusBoundaryData), parseBoundary.ReturnType);
        Assert.Equal(typeof(EnergyPlusErrorLog), parseErrorLog.ReturnType);
        Assert.Equal(typeof(IReadOnlyList<EnergyPlusTabularTable>), parseTabular.ReturnType);
        Assert.Empty(typeof(EnergyPlusResultParser).GetMember(
            "ParseEso",
            MemberTypes.Method,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance));
    }

    private static MethodInfo PublicStaticParserMethod(string name)
    {
        return Assert.Single(
            typeof(EnergyPlusResultParser).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => string.Equals(method.Name, name, StringComparison.Ordinal));
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == expected.Symbol);
        AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Executor, RequiredString(item, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        Assert.Equal(JsonValueKind.Object, python.GetProperty("facts").ValueKind);
        Assert.NotEmpty(python.GetProperty("facts").EnumerateObject());
        ValidateTaggedScalarsRecursive(python.GetProperty("facts"));
        ValidateFramesRecursive(python.GetProperty("facts"));

        JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));
    }

    private static string[] ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Executor switch
        {
            "class" => ExecuteClass(binding.CaseId, pythonFacts),
            "init" => ExecuteInit(binding.CaseId, pythonFacts),
            "parse-audit" => ExecuteAudit(binding.CaseId, pythonFacts),
            "parse-bnd" => ExecuteBoundary(binding.CaseId, pythonFacts),
            "parse-err" => ExecuteErrorLog(binding.CaseId, pythonFacts),
            "parse-eso" => ExecuteUnsupportedEso(binding.CaseId, pythonFacts),
            "parse-table" => ExecuteTabular(binding.CaseId, pythonFacts),
            _ => throw new Xunit.Sdk.XunitException(
                "Unknown launcher result-parser executor '" + binding.Executor + "'."),
        };
    }

    private static string[] ExecuteClass(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("class-descriptors", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "base_names",
                "direct_dictionary_keys",
                "method_signatures",
                "module",
                "name",
                "signature");
            Assert.Equal("idragon.launcher", RequiredString(pythonFacts, "module"));
            Assert.Equal("EnergyPlusResult", RequiredString(pythonFacts, "name"));
            Assert.Equal(
                new[] { "object" },
                pythonFacts.GetProperty("base_names").EnumerateArray().Select(item => item.GetString()!));
            AssertKeys(
                pythonFacts.GetProperty("method_signatures"),
                "parse_audit",
                "parse_bnd",
                "parse_err",
                "parse_eso",
                "parse_table");

            Type nativeType = typeof(EnergyPlusSimulationResult);
            Assert.Equal(ResultTypeName, nativeType.FullName);
            Assert.True(nativeType.IsSealed);
            string[] properties = nativeType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                new[]
                {
                    "Audit",
                    "Boundary",
                    "Diagnostics",
                    "ErrorLog",
                    "Metadata",
                    "MonthlyTables",
                    "Schema",
                    "Sources",
                    "Tables",
                },
                properties);
            Assert.All(
                nativeType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => Assert.Null(property.SetMethod));
            Assert.Equal(
                "goniegonie.invisible-dragon.energyplus-result.v1",
                EnergyPlusSimulationResult.CurrentSchema);
            return new[]
            {
                "native-type=" + ResultTypeName,
                "public-instance-properties=9-read-only",
                "schema=goniegonie.invisible-dragon.energyplus-result.v1",
            };
        }

        if (caseId.EndsWith("class-dynamic-identity", StringComparison.Ordinal))
        {
            AssertKeys(
                pythonFacts,
                "arbitrary_attribute",
                "hashable",
                "identity",
                "subclass");
            AssertTaggedInteger(pythonFacts.GetProperty("arbitrary_attribute"), "7");
            Assert.True(pythonFacts.GetProperty("hashable").GetBoolean());
            JsonElement identity = pythonFacts.GetProperty("identity");
            Assert.True(identity.GetProperty("self_equal").GetBoolean());
            Assert.False(identity.GetProperty("separate_instances_equal").GetBoolean());

            using var directory = new ResultTestDirectory();
            EnergyPlusSimulationResult first = EnergyPlusResultParser.ParseDirectory(directory.Path);
            EnergyPlusSimulationResult second = EnergyPlusResultParser.ParseDirectory(directory.Path);
            Assert.Same(first, first);
            Assert.NotSame(first, second);
            Assert.True(first.GetType().IsSealed);
            Assert.Equal(ResultTypeName, first.GetType().FullName);
            return new[]
            {
                "native-reference-self-identity=true",
                "native-separate-results-same-reference=false",
                "native-result-type=sealed",
            };
        }

        Assert.EndsWith("class-static-bindings", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "descriptor_types", "same_function_identity");
        string[] methodNames =
        {
            "parse_audit",
            "parse_bnd",
            "parse_err",
            "parse_eso",
            "parse_table",
        };
        AssertKeys(pythonFacts.GetProperty("descriptor_types"), methodNames);
        AssertKeys(pythonFacts.GetProperty("same_function_identity"), methodNames);
        Assert.All(
            methodNames,
            method => Assert.Equal(
                "staticmethod",
                RequiredString(pythonFacts.GetProperty("descriptor_types"), method)));
        Assert.All(
            methodNames,
            method => Assert.True(
                pythonFacts.GetProperty("same_function_identity").GetProperty(method).GetBoolean()));

        Type parserType = typeof(EnergyPlusResultParser);
        Assert.Equal(ParserTypeName, parserType.FullName);
        Assert.True(parserType.IsAbstract);
        Assert.True(parserType.IsSealed);
        Assert.NotNull(PublicStaticParserMethod("ParseAudit"));
        Assert.NotNull(PublicStaticParserMethod("ParseBoundary"));
        Assert.NotNull(PublicStaticParserMethod("ParseErrorLog"));
        Assert.NotNull(PublicStaticParserMethod("ParseTabular"));
        return new[]
        {
            "native-parser-type=" + ParserTypeName,
            "native-parser-binding=public-static",
            "native-parser-model=typed-methods",
        };
    }

    private static string[] ExecuteInit(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("init-defaults", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "attribute_order", "has_time", "values");
            Assert.Equal(
                new[] { "audit", "err", "bnd", "tbl", "eso" },
                pythonFacts.GetProperty("attribute_order").EnumerateArray().Select(item => item.GetString()!));
            Assert.False(pythonFacts.GetProperty("has_time").GetBoolean());
            JsonElement values = pythonFacts.GetProperty("values");
            AssertKeys(values, "audit", "bnd", "err", "eso", "tbl");
            Assert.All(values.EnumerateObject(), property => AssertTaggedNone(property.Value));

            using var directory = new ResultTestDirectory();
            EnergyPlusSimulationResult result = EnergyPlusResultParser.ParseDirectory(directory.Path);
            Assert.Empty(result.Sources);
            Assert.Empty(result.Tables);
            Assert.Empty(result.Audit.Entries);
            Assert.Empty(result.Audit.Messages);
            Assert.Empty(result.Boundary.Comments);
            Assert.Empty(result.Boundary.Records);
            Assert.Empty(result.ErrorLog.Diagnostics);
            Assert.Equal(directory.Path, result.Metadata.WorkDirectory);
            return new[]
            {
                "native-empty-directory=validated",
                "native-empty-collections=typed-read-only",
                "native-work-directory=canonicalized",
            };
        }

        if (caseId.EndsWith("init-dispatch-overwrite", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "final", "ignored_suffixes", "invalid_utf8_audit");
            AssertTaggedFloat(
                pythonFacts.GetProperty("final").GetProperty("time"),
                "1.d168000000000p+11");
            Assert.True(
                pythonFacts.GetProperty("ignored_suffixes").GetProperty("all_none").GetBoolean());

            using var directory = new ResultTestDirectory();
            directory.Write("eplusout.audit", "Alpha=1\nDup=2\nDup=3\n");
            directory.Write(
                "eplusout.err",
                "** Warning ** first\n** Severe ** second\nElapsed Time=1hr 2min 3.25sec\n");
            directory.Write(
                "eplusout.bnd",
                "! <Rec>,<A>,<B>\nRec,x\n");
            directory.Write(
                "eplustbl.csv",
                "REPORT:,R\nFOR:,Facility\nR Table\n,,V\n,A,4\n");
            directory.Write(
                "customtbl.csv",
                "REPORT:,Other\nFOR:,Facility\nOther Table\n,,V\n,B,5\n");
            directory.Write("eplusout.eso", "native ESO parsing is intentionally unsupported");

            EnergyPlusSimulationResult result = EnergyPlusResultParser.ParseDirectory(directory.Path);
            Assert.Equal(3, result.Audit.Entries.Count);
            Assert.Equal(new[] { "2", "3" }, result.Audit.Find("Dup").Select(item => item.RawValue));
            Assert.Equal(3723.25, result.ErrorLog.Summary.ReportedElapsedSeconds);
            Assert.Equal(2, result.ErrorLog.Diagnostics.Count);
            Assert.True(result.Boundary.TryGetColumns("rec", out IReadOnlyList<string>? columns));
            Assert.Equal(new[] { "A", "B" }, columns);
            Assert.Equal(2, result.Tables.Count);
            Assert.Equal(5, result.Sources.Count);
            Assert.DoesNotContain(
                result.Sources,
                source => string.Equals(source.FileName, "eplusout.eso", StringComparison.OrdinalIgnoreCase));
            return new[]
            {
                "native-directory-dispatch=err-audit-bnd-all-tbl-csv",
                "native-duplicate-audit-entries=preserved-in-order",
                "native-eso-source=not-loaded",
            };
        }

        Assert.EndsWith("init-failure-transactionality", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "observations", "partial_state");
        Assert.Equal(5, pythonFacts.GetProperty("observations").GetArrayLength());
        Assert.Equal(
            new[] { "missing-file", "path-object", "malformed-error", "html-table", "xml-table" },
            pythonFacts.GetProperty("observations").EnumerateArray()
                .Select(item => RequiredString(item, "label")));
        Assert.Equal(
            "raised",
            RequiredString(
                pythonFacts.GetProperty("partial_state").GetProperty("failure"),
                "outcome"));

        using (var directory = new ResultTestDirectory())
        {
            string missing = Path.Combine(directory.Path, "missing");
            Assert.Throws<DirectoryNotFoundException>(
                () => EnergyPlusResultParser.ParseDirectory(missing));
            Assert.Throws<ArgumentNullException>(
                () => EnergyPlusResultParser.ParseDirectory(null!));
            EnergyPlusSimulationResult afterFailures =
                EnergyPlusResultParser.ParseDirectory(directory.Path);
            Assert.Empty(afterFailures.Sources);
            Assert.Empty(afterFailures.Tables);
        }

        return new[]
        {
            "native-missing-directory=DirectoryNotFoundException",
            "native-null-directory=ArgumentNullException",
            "native-parser-state-after-failure=fresh",
        };
    }

    private static string[] ExecuteAudit(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("parse-audit-duplicates-unicode", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "entries");
            JsonElement[] pythonEntries = pythonFacts.GetProperty("entries").EnumerateArray().ToArray();
            Assert.Equal(new[] { "A", "B", "한글_١", "Huge" },
                pythonEntries.Select(item => RequiredString(item, "key")));
            AssertTaggedInteger(pythonEntries[0].GetProperty("value"), "3");
            AssertTaggedInteger(
                pythonEntries[3].GetProperty("value"),
                "99999999999999999999999999999999999999");

            const string text =
                "A=1\nB=2\nA=3\n한글_١=٤٢\nHuge=99999999999999999999999999999999999999";
            EnergyPlusAuditLog audit = EnergyPlusResultParser.ParseAudit(text);
            Assert.Equal(5, audit.Entries.Count);
            Assert.Equal(new[] { "1", "3" }, audit.Find("a").Select(item => item.RawValue));
            Assert.Equal("٤٢", Assert.Single(audit.Find("한글_١")).RawValue);
            Assert.Equal(
                "99999999999999999999999999999999999999",
                Assert.Single(audit.Find("Huge")).RawValue);
            Assert.Empty(audit.Messages);
            return new[]
            {
                "native-audit-duplicate-keys=preserved",
                "native-audit-entry-order=A-B-A-unicode-Huge",
                "native-audit-raw-values=lossless-strings",
            };
        }

        if (caseId.EndsWith("parse-audit-failure-surface", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observations");
            AssertObservationLabels(
                pythonFacts.GetProperty("observations"),
                "empty",
                "noise",
                "none",
                "bytes",
                "integer");
            EnergyPlusAuditLog empty = EnergyPlusResultParser.ParseAudit(string.Empty);
            EnergyPlusAuditLog noise = EnergyPlusResultParser.ParseAudit("noise");
            Assert.Empty(empty.Entries);
            Assert.Empty(empty.Messages);
            Assert.Empty(noise.Entries);
            Assert.Equal(new[] { "noise" }, noise.Messages);
            Assert.Throws<ArgumentNullException>(() => EnergyPlusResultParser.ParseAudit(null!));
            return new[]
            {
                "native-empty-audit=empty-typed-log",
                "native-unrecognized-audit-line=message",
                "native-null-audit=ArgumentNullException",
            };
        }

        Assert.EndsWith("parse-audit-recognition-boundaries", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "entries");
        Assert.Equal(
            new[] { "Alpha", "Decimal", "Tab", "Trailing", "y" },
            pythonFacts.GetProperty("entries").EnumerateArray()
                .Select(item => RequiredString(item, "key")));
        const string boundaryText =
            "prefix Alpha= 12\nNoSpace=3\nSpaced = 4\nNegative= -5\n" +
            "Plus= +6\nDecimal= 7.5\nTab=\t8\nTrailing= 09units\nx-y= 10";
        EnergyPlusAuditLog parsed = EnergyPlusResultParser.ParseAudit(boundaryText);
        Assert.Equal(9, parsed.Entries.Count);
        Assert.Equal("12", Assert.Single(parsed.Find("prefix Alpha")).RawValue);
        Assert.Equal(3d, Assert.Single(parsed.Find("NoSpace")).NumericValue);
        Assert.Equal(4d, Assert.Single(parsed.Find("Spaced")).NumericValue);
        Assert.Equal(-5d, Assert.Single(parsed.Find("Negative")).NumericValue);
        Assert.Equal(6d, Assert.Single(parsed.Find("Plus")).NumericValue);
        Assert.Equal(7.5d, Assert.Single(parsed.Find("Decimal")).NumericValue);
        Assert.Equal(8d, Assert.Single(parsed.Find("Tab")).NumericValue);
        Assert.Null(Assert.Single(parsed.Find("Trailing")).NumericValue);
        Assert.Equal(10d, Assert.Single(parsed.Find("x-y")).NumericValue);
        return new[]
        {
            "native-audit-recognition=trimmed-key-value-around-first-equals",
            "native-audit-numbers=invariant-double-when-complete",
            "native-audit-nonnumeric-suffix=raw-only",
        };
    }

    private static string[] ExecuteBoundary(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("parse-bnd-duplicates-padding", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "tables");
            JsonElement pythonTable = Assert.Single(
                pythonFacts.GetProperty("tables").EnumerateArray());
            Assert.Equal("Rec", RequiredString(pythonTable, "key"));
            JsonElement pythonFrame = pythonTable.GetProperty("frame");
            Assert.Equal(
                new[] { "A", "B", "C" },
                pythonFrame.GetProperty("columns").EnumerateArray().Select(item => item.GetString()!));
            JsonElement pythonRow = Assert.Single(
                pythonFrame.GetProperty("rows").EnumerateArray());
            AssertTaggedString(pythonRow[0], "one");
            AssertTaggedNone(pythonRow[1]);
            AssertTaggedNone(pythonRow[2]);

            const string text =
                "! <Rec>,<Old>\n! <Rec>,<A>,<B>,<C>\nRec,one\n";
            EnergyPlusBoundaryData boundary = EnergyPlusResultParser.ParseBoundary(text);
            EnergyPlusBoundaryRecord record = Assert.Single(boundary.OfType("rec"));
            Assert.Single(record.Fields);
            Assert.True(boundary.TryGetColumns("REC", out IReadOnlyList<string>? columns));
            Assert.Equal(new[] { "A", "B", "C" }, columns);
            Assert.True(boundary.TryGetField(record, "A", out string? first));
            Assert.Equal("one", first);
            Assert.True(boundary.TryGetField(record, "B", out string? second));
            Assert.Null(second);
            Assert.True(boundary.TryGetField(record, "C", out string? third));
            Assert.Null(third);
            Assert.False(boundary.TryGetField(record, "Old", out _));
            IDictionary<string, IReadOnlyList<string>> mutableSchemas =
                Assert.IsAssignableFrom<IDictionary<string, IReadOnlyList<string>>>(boundary.Schemas);
            Assert.True(mutableSchemas.IsReadOnly);
            IList<string> mutableColumns = Assert.IsAssignableFrom<IList<string>>(columns);
            Assert.True(mutableColumns.IsReadOnly);
            return new[]
            {
                "native-bnd-duplicate-header=last-wins",
                "native-bnd-short-row-missing-columns=successful-null",
                "native-bnd-schema-map=immutable-case-insensitive",
            };
        }

        if (caseId.EndsWith("parse-bnd-failure-grammar", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observations");
            AssertObservationLabels(
                pythonFacts.GetProperty("observations"),
                "overlong-row",
                "partial-invalid-column",
                "invalid-header",
                "none",
                "bytes");
            const string text =
                "! <Rec>,<A>,<B-C>\n" +
                "! <Bad-Key>,<A-B>\n" +
                "Rec,1,2,3\n" +
                "Unknown,\"a,b\",tail\n" +
                "Bad-Key,x\n";
            EnergyPlusBoundaryData boundary = EnergyPlusResultParser.ParseBoundary(text);
            EnergyPlusBoundaryRecord overlong = Assert.Single(boundary.OfType("Rec"));
            Assert.Equal(new[] { "1", "2", "3" }, overlong.Fields);
            Assert.True(boundary.TryGetColumns("rec", out IReadOnlyList<string>? columns));
            Assert.Equal(new[] { "A", "B-C" }, columns);
            EnergyPlusBoundaryRecord unknown = Assert.Single(boundary.OfType("unknown"));
            Assert.Equal(new[] { "a,b", "tail" }, unknown.Fields);
            Assert.False(boundary.TryGetColumns("Unknown", out _));
            Assert.True(boundary.TryGetColumns("bad-key", out IReadOnlyList<string>? hyphenColumns));
            Assert.Equal(new[] { "A-B" }, hyphenColumns);
            Assert.Throws<ArgumentNullException>(() => EnergyPlusResultParser.ParseBoundary(null!));
            return new[]
            {
                "native-bnd-overlong-row=retained-losslessly",
                "native-bnd-quoted-unknown-record=csv-aware",
                "native-bnd-null-input=ArgumentNullException",
            };
        }

        Assert.EndsWith("parse-bnd-records", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "first_line", "tables");
        Assert.Equal(2, pythonFacts.GetProperty("tables").GetArrayLength());
        Assert.Equal(
            new[] { "Zone Information", "Surface Data" },
            pythonFacts.GetProperty("tables").EnumerateArray()
                .Select(item => RequiredString(item, "key")));

        const string recordsText =
            "! <Zone Information>,<Zone Name>,<Zone #>,<Path [m2/s]>\n" +
            "! <Surface Data>,<Name>,<Type>\n" +
            "Zone Information,\"Zone A\",1\n" +
            "Unknown,a,b\n" +
            "Zone Information,Zone B,2,/tmp\n";
        EnergyPlusBoundaryData parsed = EnergyPlusResultParser.ParseBoundary(recordsText);
        Assert.Equal(2, parsed.Comments.Count);
        Assert.Equal(3, parsed.Records.Count);
        EnergyPlusBoundaryRecord[] zones = parsed.OfType("zone information").ToArray();
        Assert.Equal(2, zones.Length);
        Assert.Equal(new[] { "Zone A", "1" }, zones[0].Fields);
        Assert.Equal(new[] { "Zone B", "2", "/tmp" }, zones[1].Fields);
        Assert.Equal(3, zones[0].SourceLine);
        Assert.True(parsed.TryGetField(zones[0], "Path [m2/s]", out string? missingPath));
        Assert.Null(missingPath);
        Assert.True(parsed.TryGetField(zones[1], "Path [m2/s]", out string? pathValue));
        Assert.Equal("/tmp", pathValue);
        Assert.True(parsed.TryGetColumns("Surface Data", out IReadOnlyList<string>? surfaceColumns));
        Assert.Equal(new[] { "Name", "Type" }, surfaceColumns);
        Assert.Empty(parsed.OfType("Surface Data"));

        EnergyPlusBoundaryData firstLine = EnergyPlusResultParser.ParseBoundary(
            "Rec,first\n! <Rec>,<A>\nRec,second\n");
        EnergyPlusBoundaryRecord[] records = firstLine.OfType("Rec").ToArray();
        Assert.Equal(2, records.Length);
        Assert.True(firstLine.TryGetField(records[0], "A", out string? firstValue));
        Assert.Equal("first", firstValue);
        return new[]
        {
            "native-bnd-record-order-and-source-lines=preserved",
            "native-bnd-schema-with-zero-records=queryable",
            "native-bnd-header-after-record=derived-for-all-records",
        };
    }

    private static string[] ExecuteErrorLog(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("parse-err-diagnostics", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "elapsed_seconds", "warnings");
            AssertTaggedFloat(
                pythonFacts.GetProperty("elapsed_seconds"),
                "1.d168000000000p+11");
            JsonElement pythonWarnings = pythonFacts.GetProperty("warnings");
            Assert.Equal(
                new[] { "type", "title" },
                pythonWarnings.GetProperty("columns").EnumerateArray().Select(item => item.GetString()!));
            Assert.Equal(2, pythonWarnings.GetProperty("rows").GetArrayLength());

            const string text =
                "preamble\n" +
                "** Warning **  first title  \n" +
                "** Severe ** second title\n" +
                "** Fatal ** ignored fatal\n" +
                "** warning ** ignored lower\n" +
                "** ~~~ ** continuation retained\n" +
                "Elapsed Time=1hr  2min\t3.25sec\n" +
                "Elapsed Time=9hr 9min 9sec";
            EnergyPlusErrorLog log = EnergyPlusResultParser.ParseErrorLog(text);
            Assert.Equal(4, log.Diagnostics.Count);
            Assert.Equal(
                new[]
                {
                    EnergyPlusDiagnosticSeverity.Warning,
                    EnergyPlusDiagnosticSeverity.Severe,
                    EnergyPlusDiagnosticSeverity.Fatal,
                    EnergyPlusDiagnosticSeverity.Warning,
                },
                log.Diagnostics.Select(item => item.Severity));
            Assert.Equal("first title", log.Diagnostics[0].Message);
            Assert.Equal(
                new[] { "continuation retained" },
                log.Diagnostics[3].ContinuationLines);
            Assert.Equal(2, log.Summary.WarningCount);
            Assert.Equal(1, log.Summary.SevereCount);
            Assert.Equal(1, log.Summary.FatalCount);
            Assert.Equal(32_949d, log.Summary.ReportedElapsedSeconds);
            return new[]
            {
                "native-error-diagnostics=warning-severe-fatal-typed",
                "native-error-continuations=attached-to-prior-diagnostic",
                "native-error-repeated-elapsed-marker=last-wins",
            };
        }

        if (caseId.EndsWith("parse-err-failure-surface", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observations");
            AssertObservationLabels(
                pythonFacts.GetProperty("observations"),
                "missing",
                "spaced-equals",
                "invalid-seconds",
                "none",
                "bytes");
            EnergyPlusErrorLog missing = EnergyPlusResultParser.ParseErrorLog(string.Empty);
            EnergyPlusErrorLog spaced = EnergyPlusResultParser.ParseErrorLog(
                "Elapsed Time =0hr 0min 1sec");
            EnergyPlusErrorLog invalid = EnergyPlusResultParser.ParseErrorLog(
                "Elapsed Time=0hr 0min 1..2sec");
            Assert.Empty(missing.Diagnostics);
            Assert.Null(missing.Summary.ReportedElapsedSeconds);
            Assert.Equal(1d, spaced.Summary.ReportedElapsedSeconds);
            Assert.Null(invalid.Summary.ReportedElapsedSeconds);
            Assert.Throws<ArgumentNullException>(() => EnergyPlusResultParser.ParseErrorLog(null!));
            return new[]
            {
                "native-missing-elapsed-marker=typed-null",
                "native-spaced-elapsed-equals=accepted",
                "native-malformed-seconds=typed-null",
            };
        }

        Assert.EndsWith("parse-err-time-empty", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "elapsed_seconds", "warnings");
        AssertTaggedFloat(
            pythonFacts.GetProperty("elapsed_seconds"),
            "1.0000000000000p-1");
        Assert.Empty(pythonFacts.GetProperty("warnings").GetProperty("columns").EnumerateArray());
        EnergyPlusErrorLog leadingDecimal = EnergyPlusResultParser.ParseErrorLog(
            "Elapsed Time=0hr \n 0min .5sec");
        EnergyPlusErrorLog explicitDecimal = EnergyPlusResultParser.ParseErrorLog(
            "Elapsed Time=0hr 0min 0.5sec");
        Assert.Empty(leadingDecimal.Diagnostics);
        Assert.Null(leadingDecimal.Summary.ReportedElapsedSeconds);
        Assert.Equal(0.5d, explicitDecimal.Summary.ReportedElapsedSeconds);
        return new[]
        {
            "native-empty-diagnostic-log=typed-empty",
            "native-leading-decimal-seconds=not-recognized",
            "native-same-line-zero-leading-decimal-seconds=0.5",
        };
    }

    private static string[] ExecuteUnsupportedEso(string caseId, JsonElement pythonFacts)
    {
        Type parserType = typeof(EnergyPlusResultParser);
        Assert.Equal(ParserTypeName, parserType.FullName);
        MemberInfo[] publicParseEso = parserType.GetMember(
            "ParseEso",
            MemberTypes.Method,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        Assert.Empty(publicParseEso);

        if (caseId.EndsWith("parse-eso-arity", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observations");
            AssertObservationLabels(
                pythonFacts.GetProperty("observations"),
                "zero",
                "two",
                "keyword",
                "wrong-keyword");
            JsonElement keyword = Assert.Single(
                pythonFacts.GetProperty("observations").EnumerateArray(),
                item => RequiredString(item, "label") == "keyword");
            Assert.Equal("returned", RequiredString(keyword, "outcome"));
            AssertTaggedNone(keyword.GetProperty("result"));
            return new[]
            {
                "native-eso-binding=" + ParserTypeName,
                "native-public-ParseEso-count=0",
                "native-eso-arity-surface=not-applicable",
            };
        }

        if (caseId.EndsWith("parse-eso-opaque", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observation");
            JsonElement observation = pythonFacts.GetProperty("observation");
            Assert.Equal("opaque", RequiredString(observation, "label"));
            Assert.Equal("returned", RequiredString(observation, "outcome"));
            AssertTaggedNone(observation.GetProperty("result"));
            return new[]
            {
                "native-eso-binding=" + ParserTypeName,
                "native-public-ParseEso-count=0",
                "native-eso-opaque-input=not-inspected-because-unsupported",
            };
        }

        Assert.EndsWith("parse-eso-values", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "observations");
        AssertObservationLabels(
            pythonFacts.GetProperty("observations"),
            "empty",
            "text",
            "none",
            "bytes",
            "integer",
            "list");
        Assert.All(
            pythonFacts.GetProperty("observations").EnumerateArray(),
            observation =>
            {
                Assert.Equal("returned", RequiredString(observation, "outcome"));
                AssertTaggedNone(observation.GetProperty("result"));
            });
        return new[]
        {
            "native-eso-binding=" + ParserTypeName,
            "native-public-ParseEso-count=0",
            "native-eso-value-domain=not-supported",
        };
    }

    private static string[] ExecuteTabular(string caseId, JsonElement pythonFacts)
    {
        if (caseId.EndsWith("parse-table-csv-multi-report", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "tables");
            JsonElement[] pythonTables = pythonFacts.GetProperty("tables").EnumerateArray().ToArray();
            Assert.Equal(
                new[] { "MonthlyOne", "Second_2" },
                pythonTables.Select(item => RequiredString(item, "key")));
            JsonElement firstPythonFrame = pythonTables[0].GetProperty("frame");
            Assert.Equal(
                new[] { "Electricity [kWh]", "Label" },
                firstPythonFrame.GetProperty("columns").EnumerateArray().Select(item => item.GetString()!));
            AssertTaggedNan(firstPythonFrame.GetProperty("rows")[1][0]);

            const string text =
                "REPORT:,MonthlyOne\n" +
                "FOR:,Entire Facility\n" +
                "Monthly Table\n" +
                ",,Electricity [kWh],Label\n" +
                ",Jan,1.5,\"a,b\"\n" +
                ",Feb,,plain\n\n" +
                "REPORT:,Second_2\n" +
                "FOR:,Entire Facility\n" +
                "Second Table\n" +
                ",,Value\n" +
                ",A,2\n" +
                ",B,3\n";
            IReadOnlyList<EnergyPlusTabularTable> tables =
                EnergyPlusResultParser.ParseTabular("eplustbl.csv", text);
            Assert.Equal(2, tables.Count);
            Assert.Equal(new[] { "MonthlyOne", "Second_2" }, tables.Select(table => table.ReportName));
            Assert.Equal(new[] { "Monthly Table" }, tables[0].TitlePath);
            Assert.Equal("Entire Facility", tables[0].Scope);
            Assert.True(tables[0].TryGetCell(
                "Jan",
                "Electricity [kWh]",
                out EnergyPlusTabularCell? january));
            Assert.Equal(1.5d, january!.NumericValue);
            Assert.True(tables[0].TryGetCell("Jan", "Label", out EnergyPlusTabularCell? label));
            Assert.Equal("a,b", label!.Text);
            Assert.True(tables[0].TryGetCell(
                "Feb",
                "Electricity [kWh]",
                out EnergyPlusTabularCell? february));
            Assert.Null(february!.NumericValue);
            Assert.Equal(string.Empty, february.Text);
            Assert.True(tables[1].TryGetCell("B", "Value", out EnergyPlusTabularCell? second));
            Assert.Equal(3d, second!.NumericValue);
            return new[]
            {
                "native-tabular-multiple-reports=ordered-typed-list",
                "native-tabular-quoted-cell=csv-aware",
                "native-tabular-empty-numeric-cell=null",
            };
        }

        if (caseId.EndsWith("parse-table-failure-surface", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observations");
            AssertObservationLabels(
                pythonFacts.GetProperty("observations"),
                "extension-html",
                "extension-xml",
                "extension-csv",
                "extension-none",
                "extension-1",
                "none-text",
                "bytes-text",
                "malformed-csv");
            Assert.Throws<ArgumentNullException>(
                () => EnergyPlusResultParser.ParseTabular(null!, string.Empty));
            Assert.Throws<ArgumentNullException>(
                () => EnergyPlusResultParser.ParseTabular("eplustbl.csv", null!));
            const string valid =
                "REPORT:,R\nFOR:,Facility\nTable\n,,Value\n,Row,1\n";
            EnergyPlusTabularTable extensionIndependent = Assert.Single(
                EnergyPlusResultParser.ParseTabular("eplustbl.html", valid));
            Assert.Equal("eplustbl.html", extensionIndependent.SourceFileName);
            const string malformed =
                "REPORT:,Bad\nFOR:,Facility\nTable\n,,Value\n,Row,\"unterminated\n";
            Assert.Throws<FormatException>(
                () => EnergyPlusResultParser.ParseTabular("eplustbl.csv", malformed));
            return new[]
            {
                "native-tabular-null-inputs=ArgumentNullException",
                "native-tabular-file-extension=metadata-only",
                "native-tabular-unterminated-quote=FormatException",
            };
        }

        Assert.EndsWith("parse-table-grammar-duplicates", caseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "observations");
        AssertObservationLabels(
            pythonFacts.GetProperty("observations"),
            "leading",
            "spaced-name",
            "wrong-scope",
            "crlf",
            "unterminated",
            "duplicates-separated",
            "duplicates-adjacent");
        const string lineFeedText =
            "REPORT:,Dup\n" +
            "FOR:,Facility\n" +
            "Collision Table\n" +
            ",,January,February\n" +
            ",Heating,1.25,Target Row\n" +
            ",Target Row,9,10\n\n" +
            "REPORT:,Dup\n" +
            "FOR:,Building\n" +
            "Second Table\n" +
            ",,Value\n" +
            ",Only,2\n";
        string crlfText = lineFeedText.Replace("\n", "\r\n", StringComparison.Ordinal);
        IReadOnlyList<EnergyPlusTabularTable> duplicateTables =
            EnergyPlusResultParser.ParseTabular("duplicates-tbl.csv", crlfText);
        Assert.Equal(2, duplicateTables.Count);
        Assert.All(duplicateTables, table => Assert.Equal("Dup", table.ReportName));
        EnergyPlusTabularTable collision = duplicateTables[0];
        EnergyPlusTabularRow row = Assert.Single(collision.FindRows("target row"));
        Assert.Equal("Target Row", row[1].Text);
        Assert.True(collision.TryGetCell(
            "Target Row",
            "January",
            out EnergyPlusTabularCell? collisionJanuary));
        Assert.Equal(9d, collisionJanuary!.NumericValue);
        Assert.DoesNotContain(
            collision.FindRows("Target Row"),
            candidate => candidate[1].Text == "Heating");
        Assert.Equal("Building", duplicateTables[1].Scope);
        return new[]
        {
            "native-tabular-crlf=accepted",
            "native-tabular-duplicate-report-names=preserved",
            "native-tabular-row-lookup=label-column-only",
        };
    }

    private static void AssertObservationLabels(JsonElement observations, params string[] expected)
    {
        JsonElement[] items = observations.EnumerateArray().ToArray();
        Assert.Equal(expected, items.Select(item => RequiredString(item, "label")));
        foreach (JsonElement item in items)
        {
            string outcome = RequiredString(item, "outcome");
            if (outcome == "returned")
            {
                AssertKeys(item, "label", "outcome", "result");
            }
            else
            {
                Assert.Equal("raised", outcome);
                AssertKeys(
                    item,
                    "error_category",
                    "exception_type",
                    "label",
                    "message",
                    "outcome");
            }
        }
    }

    private static void AssertTaggedNone(JsonElement value)
    {
        AssertKeys(value, "kind");
        Assert.Equal("none", RequiredString(value, "kind"));
    }

    private static void AssertTaggedNan(JsonElement value)
    {
        AssertKeys(value, "kind");
        Assert.Equal("nan", RequiredString(value, "kind"));
    }

    private static void AssertTaggedInteger(JsonElement value, string expectedDecimal)
    {
        AssertKeys(value, "decimal", "kind");
        Assert.Equal("int", RequiredString(value, "kind"));
        Assert.Equal(expectedDecimal, RequiredString(value, "decimal"));
    }

    private static void AssertTaggedFloat(JsonElement value, string expectedBinary64)
    {
        AssertKeys(value, "binary64", "kind");
        Assert.Equal("float", RequiredString(value, "kind"));
        Assert.Equal(expectedBinary64, RequiredString(value, "binary64"));
    }

    private static void AssertTaggedString(JsonElement value, string expectedValue)
    {
        AssertKeys(value, "kind", "value");
        Assert.Equal("string", RequiredString(value, "kind"));
        Assert.Equal(expectedValue, RequiredString(value, "value"));
    }

    private static void ValidateTaggedScalarsRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("kind", out JsonElement kindValue))
        {
            string kind = kindValue.GetString()!;
            switch (kind)
            {
                case "none":
                case "nan":
                case "positive-infinity":
                case "negative-infinity":
                    AssertKeys(value, "kind");
                    break;
                case "bool":
                    AssertKeys(value, "kind", "value");
                    Assert.True(value.GetProperty("value").ValueKind is
                        JsonValueKind.True or JsonValueKind.False);
                    break;
                case "int":
                    AssertKeys(value, "decimal", "kind");
                    Assert.Matches(
                        @"^-?(?:0|[1-9][0-9]*)$",
                        RequiredString(value, "decimal"));
                    break;
                case "float":
                    AssertKeys(value, "binary64", "kind");
                    Assert.Matches(
                        @"^-?(?:[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+)$",
                        RequiredString(value, "binary64"));
                    break;
                case "string":
                    AssertKeys(value, "kind", "value");
                    Assert.Equal(JsonValueKind.String, value.GetProperty("value").ValueKind);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException(
                        "Unknown tagged fixture scalar kind '" + kind + "'.");
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                ValidateTaggedScalarsRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateTaggedScalarsRecursive(item);
            }
        }
    }

    private static void ValidateFramesRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            string[] keys = value.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] frameKeys =
            {
                "columns",
                "dtypes",
                "index",
                "index_name",
                "rows",
            };
            if (keys.SequenceEqual(frameKeys.OrderBy(name => name, StringComparer.Ordinal)))
            {
                JsonElement[] columns = value.GetProperty("columns").EnumerateArray().ToArray();
                JsonElement[] dtypes = value.GetProperty("dtypes").EnumerateArray().ToArray();
                JsonElement[] indices = value.GetProperty("index").EnumerateArray().ToArray();
                JsonElement[] rows = value.GetProperty("rows").EnumerateArray().ToArray();
                Assert.All(columns, column => Assert.Equal(JsonValueKind.String, column.ValueKind));
                Assert.All(dtypes, dtype => Assert.Equal(JsonValueKind.String, dtype.ValueKind));
                Assert.Equal(columns.Length, dtypes.Length);
                Assert.Equal(indices.Length, rows.Length);
                Assert.True(value.GetProperty("index_name").TryGetProperty("kind", out _));
                Assert.All(
                    rows,
                    row => Assert.Equal(columns.Length, row.GetArrayLength()));
            }

            foreach (JsonProperty property in value.EnumerateObject())
            {
                ValidateFramesRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateFramesRecursive(item);
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
            @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])",
            RegexOptions.CultureInvariant));
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number));
            Assert.False(double.IsNaN(number));
            Assert.False(double.IsInfinity(number));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoNonFiniteJsonNumbers(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoNonFiniteJsonNumbers(item);
            }
        }
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
        string AdaptationId,
        string NativeTarget);

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string AdaptationId,
        IReadOnlyList<string> NativeFacts);
}
