using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.SimpleDragon;
using Dragons.SimpleDragon.Batch;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.EnergyPlus.Runtime.Tests;

public sealed class LauncherRuntimeOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/launcher-runtime-oracle.json";
    private const string OracleSha256 =
        "sha256:3df3d7fb8c0c9d85ad0e9ffae9ae3055d742671b4554b2c860ff9f1877f9df33";
    private const string CasesSha256 =
        "sha256:bf5d658273fcf42e536acc102e1b117497b3f017031c0db0c2d605c87297d4bc";
    private const int OracleByteLength = 19_786;
    private const int ExpectedCaseCount = 12;
    private const string OracleSchema = "dragons.python-reference.launcher-runtime.v1";
    private const string EvidenceTestCase =
        "Dragons.EnergyPlus.Runtime.Tests.LauncherRuntimeOracleParityTests.MatchesPinnedPythonLauncherRuntime";
    private const string UpstreamPath = "src/idragon/launcher.py";
    private const string FailureTypeName = "Dragons.EnergyPlus.Runtime.EnergyPlusFailure";
    private const string ResolverTargetName =
        "Dragons.EnergyPlus.Runtime.RuntimeResolver.ResolveAsync";
    private const string BatchTargetName =
        "Dragons.SimpleDragon.Batch.BatchRunner.RunAsync";
    private const string RunnerTargetName =
        "Dragons.EnergyPlus.Runtime.EnergyPlusRunner.RunAsync";
    private const string MarkerFileName = ".dragons-energyplus-run";

    // Exact literals are consumed by the trusted compatibility evidence collector.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/launcher.py", "ExecutableEnergyPlusNotFoundError", "sha256:76d795db5a0292d2af780a00ee53760a1ab5bd07d50cdf144cf6863c3b08b3d3", "launcher-runtime-executable-not-found-76d795db"),
        new("src/idragon/launcher.py", "find_executable_dir", "sha256:6de563f4cfe228449e3c29866c9e432c7cf0f9ffc49dad4e7558d9b0addebf1b", "launcher-runtime-find-executable-dir-6de563f4"),
        new("src/idragon/launcher.py", "run", "sha256:84c6ff241eb023074ab18999d177182fcc33e90c128d479f50303041945b4281", "launcher-runtime-run-84c6ff24"),
        new("src/idragon/launcher.py", "run_single", "sha256:eda7f7577da0c1ac73498136fcfa6955ffb6605bad1dc0b9a1ac80609b094884", "launcher-runtime-run-single-eda7f757"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("ExecutableEnergyPlusNotFoundError", "class", "sha256:af4269c77686b13c3db93174c1cba5a3679b876d7dd4996633b5daeab8fff8f3", "sha256:921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a", "structured-energyplus-runtime-not-found-failure", FailureTypeName),
        new("find_executable_dir", "function", "sha256:ebb7af2fdb78b6207f5681aa3c0ccab67f8ba7bd843663597beed49ccf11b61f", "sha256:99ab4b26f1043306ed119f8df86069765fa4cde5dd32792ce335ea1800820c2d", "hash-verified-energyplus-runtime-resolution", ResolverTargetName),
        new("run", "function", "sha256:4e1ba99373cc367b28141f5395751a8eac81db60ee71bb4afe691981d4bd2bf8", "sha256:0fb4f14dabde914d8f39235d9df925f011fc66d7fc88131230fc5b213bff106a", "bounded-deterministic-energyplus-batch-execution", BatchTargetName),
        new("run_single", "function", "sha256:77a80eeb659cc9b40e69a8db74a9337246ca40bb3ea900e9b466accc92bb9c0a", "sha256:e5fb9f5b5a84a697283db6c5bb88dce0f1b696c7864a2934279ff93a1b3ba659", "isolated-cancellable-energyplus-single-run", RunnerTargetName),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("launcher-runtime.executable-error-class", "executable-error", "ExecutableEnergyPlusNotFoundError"),
        new("launcher-runtime.executable-error-instance", "executable-error", "ExecutableEnergyPlusNotFoundError"),
        new("launcher-runtime.executable-error-raise", "executable-error", "ExecutableEnergyPlusNotFoundError"),
        new("launcher-runtime.find-executable-failure", "find-executable", "find_executable_dir"),
        new("launcher-runtime.find-executable-package-precedence", "find-executable", "find_executable_dir"),
        new("launcher-runtime.find-executable-system-fallback", "find-executable", "find_executable_dir"),
        new("launcher-runtime.run-broadcast", "run", "run"),
        new("launcher-runtime.run-cardinality", "run", "run"),
        new("launcher-runtime.run-scalar", "run", "run"),
        new("launcher-runtime.run-single-explicit-retain", "run-single", "run_single"),
        new("launcher-runtime.run-single-inferred-delete", "run-single", "run_single"),
        new("launcher-runtime.run-single-transactionality", "run-single", "run_single"),
    };

    [Fact]
    public async Task MatchesPinnedPythonLauncherRuntime()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = Sha256(bytes);
        Assert.Equal(OracleSha256, sha256);
        Assert.Equal(OracleByteLength, bytes.Length);

        using JsonDocument oracle = JsonDocument.Parse(bytes);
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        using var directory = new TestDirectory();
        var context = new NativeContext(directory);
        var observations = new List<NativeObservation>(ExpectedCaseCount);
        for (int index = 0; index < cases.Length; index++)
        {
            CaseBinding binding = ExpectedCases[index];
            SymbolContract symbol = Assert.Single(
                ExpectedSymbols,
                candidate => candidate.Symbol == binding.Symbol);
            string[] nativeFacts = await ExecuteCaseAsync(binding, context);
            Assert.NotEmpty(nativeFacts);
            Assert.Equal(
                nativeFacts.Length,
                nativeFacts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(nativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
            JsonElement nativeFactsJson = JsonSerializer.SerializeToElement(nativeFacts);
            AssertNoRawAddresses(nativeFactsJson.GetRawText());
            AssertNoNonFiniteJsonNumbers(nativeFactsJson);
            AssertNoHostTokens(nativeFactsJson.GetRawText());
            observations.Add(new NativeObservation(
                binding.CaseId,
                binding.Symbol,
                symbol.AdaptationId,
                symbol.NativeTarget,
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
            AssertNoHostTokens(receiptJson.GetRawText());
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
        AssertNoHostTokens(root.GetRawText());

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal("847b01f68f438f560a986072bcaa7768fbf67897", RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02",
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
            Assert.EndsWith(
                evidence.SymbolHash.Substring("sha256:".Length, 8),
                evidence.AssertionId,
                StringComparison.Ordinal);
        }
    }

    private static void ValidateSymbols(JsonElement symbols)
    {
        JsonElement[] actual = symbols.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            SymbolContract expected = ExpectedSymbols[index];
            JsonElement item = actual[index];
            AssertKeys(item, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(expected.BodyHash, RequiredString(item, "body_hash"));
            Assert.Equal(expected.Kind, RequiredString(item, "kind"));
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(expected.SignatureHash, RequiredString(item, "signature_hash"));
            Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(ExpectedEvidence[index].SymbolHash, RequiredString(item, "symbol_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement contract)
    {
        AssertKeys(
            contract,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classifications",
            "execution_policy",
            "float_encoding",
            "path_encoding",
            "runtime_names",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId),
            contract.GetProperty("case_ids").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            "closed-fakes-no-process-or-active-load",
            RequiredString(contract, "execution_policy"));
        Assert.Equal(
            "python-binary64-hex-without-0x-prefix",
            RequiredString(contract, "float_encoding"));
        Assert.Equal("logical-tokens-only", RequiredString(contract, "path_encoding"));
        Assert.Equal(
            "pinned-python-only-no-native-type-name-claims",
            RequiredString(contract, "runtime_names"));

        string[] symbolNames = ExpectedSymbols.Select(item => item.Symbol).ToArray();
        Assert.Equal(
            symbolNames,
            contract.GetProperty("target_symbols").EnumerateArray().Select(item => item.GetString()));
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement assertionIds = contract.GetProperty("assertion_ids");
        AssertKeys(classifications, symbolNames);
        AssertKeys(adaptations, symbolNames);
        AssertKeys(assertionIds, symbolNames);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            SymbolContract expected = ExpectedSymbols[index];
            Assert.Equal("exception", RequiredString(classifications, expected.Symbol));
            Assert.Equal(expected.AdaptationId, RequiredString(adaptations, expected.Symbol));
            Assert.Equal(ExpectedEvidence[index].AssertionId, RequiredString(assertionIds, expected.Symbol));
        }
    }

    private static void ValidateNativeBindings()
    {
        Assert.Equal(FailureTypeName, typeof(EnergyPlusFailure).FullName);
        Assert.True(typeof(EnergyPlusFailure).IsSealed);

        MethodInfo[] resolverMethods = typeof(RuntimeResolver).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(item => item.Name == nameof(RuntimeResolver.ResolveAsync))
            .ToArray();
        Assert.NotEmpty(resolverMethods);
        Assert.All(
            resolverMethods,
            method => Assert.Equal(ResolverTargetName, method.DeclaringType!.FullName + "." + method.Name));

        MethodInfo batchMethod = Assert.Single(
            typeof(BatchRunner).GetMethods(BindingFlags.Static | BindingFlags.Public),
            item => item.Name == nameof(BatchRunner.RunAsync));
        Assert.Equal(BatchTargetName, batchMethod.DeclaringType!.FullName + "." + batchMethod.Name);
        Assert.Contains(
            typeof(IBatchCaseExecutor),
            batchMethod.GetParameters().Select(item => item.ParameterType));

        MethodInfo[] runnerMethods = typeof(EnergyPlusRunner).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(item => item.Name == nameof(EnergyPlusRunner.RunAsync))
            .ToArray();
        Assert.Equal(2, runnerMethods.Length);
        Assert.All(
            runnerMethods,
            method => Assert.Equal(RunnerTargetName, method.DeclaringType!.FullName + "." + method.Name));
        Assert.Equal(
            ExpectedSymbols.Length,
            ExpectedSymbols.Select(item => item.NativeTarget).Distinct(StringComparer.Ordinal).Count());
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Executor, RequiredString(item, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));

        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == expected.Symbol);
        JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        Assert.Equal(JsonValueKind.Object, facts.ValueKind);
        Assert.NotEmpty(facts.EnumerateObject());
        ValidatePythonFacts(expected.CaseId, facts);
    }

    private static void ValidatePythonFacts(string caseId, JsonElement facts)
    {
        switch (caseId)
        {
            case "launcher-runtime.executable-error-class":
                ValidatePythonErrorClass(facts);
                return;
            case "launcher-runtime.executable-error-instance":
                ValidatePythonErrorInstances(facts);
                return;
            case "launcher-runtime.executable-error-raise":
                ValidatePythonErrorRaise(facts);
                return;
            case "launcher-runtime.find-executable-failure":
                ValidatePythonFindFailure(facts);
                return;
            case "launcher-runtime.find-executable-package-precedence":
                ValidatePythonFindPrecedence(facts);
                return;
            case "launcher-runtime.find-executable-system-fallback":
                ValidatePythonFindFallback(facts);
                return;
            case "launcher-runtime.run-broadcast":
                ValidatePythonRunBroadcast(facts);
                return;
            case "launcher-runtime.run-cardinality":
                ValidatePythonRunCardinality(facts);
                return;
            case "launcher-runtime.run-scalar":
                ValidatePythonRunScalar(facts);
                return;
            case "launcher-runtime.run-single-explicit-retain":
                ValidatePythonSingleExplicit(facts);
                return;
            case "launcher-runtime.run-single-inferred-delete":
                ValidatePythonSingleInferred(facts);
                return;
            case "launcher-runtime.run-single-transactionality":
                ValidatePythonSingleTransactionality(facts);
                return;
            default:
                throw new Xunit.Sdk.XunitException("Unknown launcher runtime case '" + caseId + "'.");
        }
    }

    private static void ValidatePythonErrorClass(JsonElement facts)
    {
        AssertKeys(facts, "base_names", "direct_dictionary_keys", "inspect_signature", "module", "name");
        Assert.Equal(new[] { "Exception" }, Strings(facts.GetProperty("base_names")));
        Assert.Equal(
            new[] { "__doc__", "__module__", "__weakref__" },
            Strings(facts.GetProperty("direct_dictionary_keys")));
        Assert.Equal("idragon.launcher", RequiredString(facts, "module"));
        Assert.Equal("ExecutableEnergyPlusNotFoundError", RequiredString(facts, "name"));
        JsonElement signature = facts.GetProperty("inspect_signature");
        ValidatePythonObservation(signature);
        Assert.Equal("ValueError", RequiredString(signature, "exception_type"));
    }

    private static void ValidatePythonErrorInstances(JsonElement facts)
    {
        AssertKeys(facts, "observations");
        JsonElement[] observations = facts.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(3, observations.Length);
        Assert.Equal(new[] { "empty", "single", "multiple" }, observations.Select(item => RequiredString(item, "label")));
        Assert.Equal(new[] { "", "runtime missing", "('runtime missing', 24)" }, observations.Select(item => RequiredString(item, "str")));
        foreach (JsonElement observation in observations)
        {
            AssertKeys(observation, "args", "dictionary", "label", "repr", "str");
            Assert.Empty(observation.GetProperty("dictionary").EnumerateObject());
        }
    }

    private static void ValidatePythonErrorRaise(JsonElement facts)
    {
        AssertKeys(
            facts,
            "caught_as_exact_type",
            "caught_as_exception",
            "child_caught_as_parent",
            "dynamic_marker",
            "separate_instances_equal",
            "subclassable");
        Assert.True(facts.GetProperty("caught_as_exact_type").GetBoolean());
        Assert.True(facts.GetProperty("caught_as_exception").GetBoolean());
        Assert.True(facts.GetProperty("child_caught_as_parent").GetBoolean());
        Assert.Equal(7, facts.GetProperty("dynamic_marker").GetInt32());
        Assert.False(facts.GetProperty("separate_instances_equal").GetBoolean());
        Assert.True(facts.GetProperty("subclassable").GetBoolean());
    }

    private static void ValidatePythonFindFailure(JsonElement facts)
    {
        AssertKeys(facts, "observations");
        JsonElement[] observations = facts.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(3, observations.Length);
        Assert.All(observations, ValidatePythonObservation);
        Assert.Equal(
            new[] { "ExecutableEnergyPlusNotFoundError", "FileNotFoundError", "ValueError" },
            observations.Select(item => RequiredString(item, "exception_type")));
        Assert.Equal(
            new[] { "package-root", "system-root" },
            Strings(observations[0].GetProperty("listdir_calls")));
        Assert.Empty(observations[2].GetProperty("listdir_calls").EnumerateArray());
    }

    private static void ValidatePythonFindPrecedence(JsonElement facts)
    {
        AssertKeys(facts, "observations");
        JsonElement[] observations = facts.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(3, observations.Length);
        Assert.All(observations, ValidatePythonObservation);
        Assert.All(observations, item => Assert.Equal("returned", RequiredString(item, "outcome")));
        Assert.All(
            observations,
            item => Assert.Equal("package-root/EnergyPlusV24-2-0", RequiredString(item, "result")));
        Assert.All(
            observations,
            item => Assert.Equal(new[] { "package-root" }, Strings(item.GetProperty("listdir_calls"))));
    }

    private static void ValidatePythonFindFallback(JsonElement facts)
    {
        AssertKeys(facts, "observations", "verification");
        Assert.Equal("name-membership-only", RequiredString(facts, "verification"));
        JsonElement[] observations = facts.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(2, observations.Length);
        Assert.All(observations, ValidatePythonObservation);
        Assert.Equal(
            new[] { "system-root/EnergyPlusV9-6-0", "system-root/EnergyPlusV8-9-0" },
            observations.Select(item => RequiredString(item, "result")));
        Assert.All(
            observations,
            item => Assert.Equal(
                new[] { "package-root", "system-root" },
                Strings(item.GetProperty("listdir_calls"))));
    }

    private static void ValidatePythonRunBroadcast(JsonElement facts)
    {
        AssertKeys(facts, "caller_lists_after", "calls", "progress", "returns");
        JsonElement callerLists = facts.GetProperty("caller_lists_after");
        AssertKeys(callerLists, "idfs", "one_weather");
        Assert.Equal(new[] { "model.idf", "model.idf" }, Strings(callerLists.GetProperty("idfs")));
        Assert.Equal(new[] { "only.epw", "only.epw" }, Strings(callerLists.GetProperty("one_weather")));
        JsonElement[] calls = facts.GetProperty("calls").EnumerateArray().ToArray();
        Assert.Equal(4, calls.Length);
        Assert.All(calls, item => Assert.False(item.GetProperty("kwargs").GetProperty("verbose").GetBoolean()));
        Assert.All(calls, item => Assert.False(item.GetProperty("kwargs").TryGetProperty("output_dir", out _)));
        Assert.Equal(2, facts.GetProperty("progress").GetArrayLength());
        Assert.Equal(2, facts.GetProperty("returns").GetArrayLength());
    }

    private static void ValidatePythonRunCardinality(JsonElement facts)
    {
        AssertKeys(facts, "calls", "observations", "progress");
        JsonElement[] observations = facts.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(4, observations.Length);
        Assert.All(observations, ValidatePythonObservation);
        Assert.Equal(
            new[] { "raised", "raised", "returned", "raised" },
            observations.Select(item => RequiredString(item, "outcome")));
        Assert.Equal("ValueError", RequiredString(observations[0], "exception_type"));
        Assert.Equal("IndexError", RequiredString(observations[1], "exception_type"));
        Assert.Equal("RuntimeError", RequiredString(observations[3], "exception_type"));
        Assert.Equal(2, facts.GetProperty("calls").GetArrayLength());
    }

    private static void ValidatePythonRunScalar(JsonElement facts)
    {
        AssertKeys(facts, "calls", "progress", "returns");
        JsonElement[] calls = facts.GetProperty("calls").EnumerateArray().ToArray();
        Assert.Equal(2, calls.Length);
        Assert.Empty(facts.GetProperty("progress").EnumerateArray());
        Assert.Equal(2, facts.GetProperty("returns").GetArrayLength());
        JsonElement firstOptions = calls[0].GetProperty("kwargs");
        Assert.Equal("runtime-token", RequiredString(firstOptions, "ep_dir"));
        Assert.Equal("output-token", RequiredString(firstOptions, "output_dir"));
        Assert.False(firstOptions.GetProperty("delete").GetBoolean());
        Assert.False(firstOptions.GetProperty("verbose").GetBoolean());
    }

    private static void ValidatePythonSingleExplicit(JsonElement facts)
    {
        AssertKeys(
            facts,
            "audit",
            "copied_files",
            "elapsed_binary64",
            "launch_calls",
            "launch_status_ignored",
            "process_attempt_count",
            "resolver_calls",
            "run_dir_exists_after",
            "warnings");
        Assert.Equal(new[] { "case.audit", "case.err" }, Strings(facts.GetProperty("copied_files")));
        Assert.Equal("1.8000000000000p+0", RequiredString(facts, "elapsed_binary64"));
        Assert.Equal(73, facts.GetProperty("launch_status_ignored").GetInt32());
        Assert.Equal(0, facts.GetProperty("process_attempt_count").GetInt32());
        Assert.Empty(facts.GetProperty("resolver_calls").EnumerateArray());
        Assert.False(facts.GetProperty("run_dir_exists_after").GetBoolean());
    }

    private static void ValidatePythonSingleInferred(JsonElement facts)
    {
        AssertKeys(
            facts,
            "audit",
            "copied_audit_exists_after",
            "launch_calls",
            "launch_status_ignored",
            "process_attempt_count",
            "resolver_calls",
            "run_dir_exists_after");
        Assert.Equal(new[] { "23.2.0" }, Strings(facts.GetProperty("resolver_calls")));
        Assert.Equal(-9, facts.GetProperty("launch_status_ignored").GetInt32());
        Assert.Equal(0, facts.GetProperty("process_attempt_count").GetInt32());
        Assert.False(facts.GetProperty("copied_audit_exists_after").GetBoolean());
        Assert.False(facts.GetProperty("run_dir_exists_after").GetBoolean());
    }

    private static void ValidatePythonSingleTransactionality(JsonElement facts)
    {
        AssertKeys(facts, "observations", "process_attempt_count", "side_effects");
        JsonElement[] observations = facts.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(4, observations.Length);
        Assert.All(observations, ValidatePythonObservation);
        Assert.Equal(
            new[] { "RuntimeError", "RuntimeError", "FileNotFoundError", "AttributeError" },
            observations.Select(item => RequiredString(item, "exception_type")));
        Assert.Contains("<controlled-temp>//work-1//missing.audit", RequiredString(observations[2], "message"), StringComparison.Ordinal);
        Assert.Equal(0, facts.GetProperty("process_attempt_count").GetInt32());
        JsonElement sideEffects = facts.GetProperty("side_effects");
        AssertKeys(
            sideEffects,
            "copy_failure_run_dir_exists",
            "launch_failure_run_dir_exists",
            "missing_version_created_run_dir",
            "parse_failure_copied_output_exists",
            "parse_failure_run_dir_exists");
        Assert.True(sideEffects.GetProperty("copy_failure_run_dir_exists").GetBoolean());
        Assert.True(sideEffects.GetProperty("launch_failure_run_dir_exists").GetBoolean());
        Assert.False(sideEffects.GetProperty("missing_version_created_run_dir").GetBoolean());
        Assert.True(sideEffects.GetProperty("parse_failure_copied_output_exists").GetBoolean());
        Assert.False(sideEffects.GetProperty("parse_failure_run_dir_exists").GetBoolean());
    }

    private static void ValidatePythonObservation(JsonElement observation)
    {
        string outcome = RequiredString(observation, "outcome");
        if (outcome == "returned")
        {
            AssertKeysAllowingOptional(observation, new[] { "label", "outcome", "result" }, "listdir_calls");
        }
        else
        {
            Assert.Equal("raised", outcome);
            AssertKeysAllowingOptional(
                observation,
                new[] { "error_category", "exception_type", "label", "message", "outcome" },
                "listdir_calls");
        }
    }

    private static Task<string[]> ExecuteCaseAsync(CaseBinding binding, NativeContext context)
    {
        return binding.CaseId switch
        {
            "launcher-runtime.executable-error-class" =>
                Task.FromResult(ExecuteExecutableErrorClassCase()),
            "launcher-runtime.executable-error-instance" =>
                Task.FromResult(ExecuteExecutableErrorInstanceCase()),
            "launcher-runtime.executable-error-raise" => ExecuteExecutableErrorRaiseCaseAsync(context),
            "launcher-runtime.find-executable-failure" => ExecuteFindFailureCaseAsync(context),
            "launcher-runtime.find-executable-package-precedence" => ExecuteFindPrecedenceCaseAsync(context),
            "launcher-runtime.find-executable-system-fallback" => ExecuteFindExplicitAuthorityCaseAsync(context),
            "launcher-runtime.run-broadcast" => ExecuteBatchBoundedCaseAsync(context),
            "launcher-runtime.run-cardinality" => ExecuteBatchCardinalityCaseAsync(context),
            "launcher-runtime.run-scalar" => ExecuteBatchScalarCaseAsync(context),
            "launcher-runtime.run-single-explicit-retain" => ExecuteSingleRetainCaseAsync(context),
            "launcher-runtime.run-single-inferred-delete" => ExecuteSingleDeleteCaseAsync(context),
            "launcher-runtime.run-single-transactionality" => ExecuteSingleTransactionalityCaseAsync(context),
            _ => throw new Xunit.Sdk.XunitException("No native executor for '" + binding.CaseId + "'."),
        };
    }

    private static string[] ExecuteExecutableErrorClassCase()
    {
        Type type = typeof(EnergyPlusFailure);
        Assert.Equal(FailureTypeName, type.FullName);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        Assert.True(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length == 1);
        return new[]
        {
            "native_type=Dragons.EnergyPlus.Runtime.EnergyPlusFailure",
            "representation=sealed-immutable-record",
            "failure_transport=structured-result",
        };
    }

    private static string[] ExecuteExecutableErrorInstanceCase()
    {
        var first = new EnergyPlusFailure(
            EnergyPlusFailureCategory.RuntimeNotFound,
            "RUNTIME_NOT_FOUND",
            "A compatible runtime was not found.",
            "closed candidate set");
        var second = new EnergyPlusFailure(
            EnergyPlusFailureCategory.RuntimeNotFound,
            "RUNTIME_NOT_FOUND",
            "A compatible runtime was not found.",
            "closed candidate set");
        Assert.Equal(EnergyPlusFailureCategory.RuntimeNotFound, first.Category);
        Assert.Equal("RUNTIME_NOT_FOUND", first.Code);
        Assert.Equal("A compatible runtime was not found.", first.Message);
        Assert.Equal("closed candidate set", first.Detail);
        Assert.Equal(first, second);
        return new[]
        {
            "category=RuntimeNotFound",
            "code=RUNTIME_NOT_FOUND",
            "value_equality=true",
        };
    }

    private static async Task<string[]> ExecuteExecutableErrorRaiseCaseAsync(NativeContext context)
    {
        string missingRoot = Path.Combine(context.Directory.Path, "error-missing-runtime");
        EnergyPlusRuntimeResolution resolution = await ResolveClosedAsync(
            new RuntimeResolver(),
            new EnergyPlusRuntimeResolveOptions { RuntimeRoot = missingRoot });
        Assert.False(resolution.IsSuccess);
        Assert.Null(resolution.Runtime);
        Assert.NotNull(resolution.Failure);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeNotFound, resolution.Failure!.Category);
        Assert.Equal("RUNTIME_NOT_FOUND", resolution.Failure.Code);
        return new[]
        {
            "expected_discovery_failure=returned-not-thrown",
            "category=RuntimeNotFound",
            "code=RUNTIME_NOT_FOUND",
        };
    }

    private static async Task<string[]> ExecuteFindFailureCaseAsync(NativeContext context)
    {
        string missingRoot = Path.Combine(context.Directory.Path, "resolver-missing");
        var resolver = new RuntimeResolver();
        EnergyPlusRuntimeResolution missing = await ResolveClosedAsync(
            resolver,
            new EnergyPlusRuntimeResolveOptions { RuntimeRoot = missingRoot });
        Assert.Equal(EnergyPlusFailureCategory.RuntimeNotFound, missing.Failure?.Category);
        Assert.Equal("RUNTIME_NOT_FOUND", missing.Failure?.Code);
        Assert.Single(missing.AttemptedRoots);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        EnergyPlusRuntimeResolution cancelled = await resolver.ResolveAsync(
            ClosedResolveOptions(),
            cancellation.Token);
        Assert.Equal(EnergyPlusFailureCategory.Cancelled, cancelled.Failure?.Category);
        Assert.Equal("RUNTIME_RESOLUTION_CANCELLED", cancelled.Failure?.Code);
        Assert.Null(cancelled.Runtime);
        return new[]
        {
            "missing_runtime=structured-RUNTIME_NOT_FOUND",
            "cancelled_resolution=structured-Cancelled",
            "host_discovery=disabled",
        };
    }

    private static async Task<string[]> ExecuteFindPrecedenceCaseAsync(NativeContext context)
    {
        string firstRoot = Path.Combine(context.Directory.Path, "resolver-first");
        string secondRoot = Path.Combine(context.Directory.Path, "resolver-second");
        EnergyPlusRuntimeManifest manifest = WriteRuntimePayload(firstRoot);
        WriteRuntimePayload(secondRoot);
        var options = ClosedResolveOptions() with
        {
            AdditionalSearchRoots = new[] { firstRoot, secondRoot },
        };
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver(manifest).ResolveAsync(options);
        Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);
        Assert.Equal(Path.GetFullPath(firstRoot), resolution.Runtime!.RootPath);
        Assert.Equal(new[] { Path.GetFullPath(firstRoot), Path.GetFullPath(secondRoot) }, resolution.AttemptedRoots);
        Assert.Equal(manifest.EnergyPlusExecutableSha256, resolution.Runtime.Manifest.EnergyPlusExecutableSha256);
        Assert.True(RuntimeFileSystem.IsDescendantOf(context.Directory.Path, resolution.Runtime.RootPath));
        return new[]
        {
            "candidate_precedence=first-verified-root",
            "required_payload_hashes=verified",
            "resolved_runtime=controlled-descendant",
        };
    }

    private static async Task<string[]> ExecuteFindExplicitAuthorityCaseAsync(NativeContext context)
    {
        string explicitRoot = Path.Combine(context.Directory.Path, "resolver-explicit-tampered");
        string fallbackRoot = Path.Combine(context.Directory.Path, "resolver-fallback-valid");
        EnergyPlusRuntimeManifest manifest = WriteRuntimePayload(explicitRoot);
        WriteRuntimePayload(fallbackRoot);
        File.AppendAllText(Path.Combine(explicitRoot, "Energy+.idd"), "tampered", Encoding.UTF8);
        var options = ClosedResolveOptions() with
        {
            RuntimeRoot = explicitRoot,
            AdditionalSearchRoots = new[] { fallbackRoot },
        };
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver(manifest).ResolveAsync(options);
        Assert.False(resolution.IsSuccess);
        Assert.Null(resolution.Runtime);
        Assert.Equal(EnergyPlusFailureCategory.RuntimeIntegrity, resolution.Failure?.Category);
        Assert.Equal("RUNTIME_HASH_MISMATCH", resolution.Failure?.Code);
        Assert.Equal(new[] { Path.GetFullPath(explicitRoot) }, resolution.AttemptedRoots);
        return new[]
        {
            "explicit_root_authority=preserved",
            "tampered_idd=rejected",
            "implicit_fallback=not-used",
        };
    }

    private static async Task<string[]> ExecuteBatchBoundedCaseAsync(NativeContext context)
    {
        string outputRoot = Path.Combine(context.Directory.Path, "batch-bounded");
        BatchCaseDefinition[] cases =
        {
            ClosedBatchCase("case-a"),
            ClosedBatchCase("case-b"),
            ClosedBatchCase("case-c"),
            ClosedBatchCase("case-d"),
        };
        BatchCaseDefinition[] callerSnapshot = cases.ToArray();
        var executor = new ClosedBatchExecutor(
            (item, _) => Task.FromResult(BatchCaseExecution.Success(
                new Dictionary<string, double> { ["ordered_index"] = item.Index })),
            rendezvousCount: 2);
        BatchRunResult result = await BatchRunner.RunAsync(
            cases,
            executor,
            ClosedBatchOptions(outputRoot, parallelism: 2));

        Assert.Equal(callerSnapshot, cases);
        Assert.Equal(new[] { "case-a", "case-b", "case-c", "case-d" }, result.Cases.Select(item => item.CaseId));
        Assert.All(result.Cases, item => Assert.Equal(BatchCaseStatus.Succeeded, item.Status));
        Assert.Equal(4, executor.InvocationCount);
        Assert.Equal(2, executor.MaximumActive);
        Assert.All(
            executor.Contexts,
            item => Assert.True(RuntimeFileSystem.IsDescendantOf(outputRoot, item.WorkRootPath)));
        return new[]
        {
            "input_collection_mutated=false",
            "result_order=explicit-case-order",
            "maximum_active=2",
            "process_execution=closed-fake-only",
        };
    }

    private static async Task<string[]> ExecuteBatchCardinalityCaseAsync(NativeContext context)
    {
        string emptyRoot = Path.Combine(context.Directory.Path, "batch-empty");
        var emptyExecutor = new ClosedBatchExecutor(
            (_, _) => throw new InvalidOperationException("Empty batch invoked its executor."));
        BatchRunResult empty = await BatchRunner.RunAsync(
            Array.Empty<BatchCaseDefinition>(),
            emptyExecutor,
            ClosedBatchOptions(emptyRoot, parallelism: 1));
        Assert.Empty(empty.Cases);
        Assert.Equal(0, emptyExecutor.InvocationCount);

        string partialRoot = Path.Combine(context.Directory.Path, "batch-partial");
        var partialExecutor = new ClosedBatchExecutor((item, _) =>
        {
            if (item.CaseId == "case-b")
            {
                throw new InvalidOperationException("closed case failure");
            }

            return Task.FromResult(BatchCaseExecution.Success());
        });
        BatchRunResult partial = await BatchRunner.RunAsync(
            new[] { ClosedBatchCase("case-a"), ClosedBatchCase("case-b"), ClosedBatchCase("case-c") },
            partialExecutor,
            ClosedBatchOptions(partialRoot, parallelism: 1));
        Assert.Equal(
            new[] { BatchCaseStatus.Succeeded, BatchCaseStatus.Failed, BatchCaseStatus.Succeeded },
            partial.Cases.Select(item => item.Status));
        Assert.Equal(3, partialExecutor.InvocationCount);
        Assert.Equal(new[] { "case-a", "case-b", "case-c" }, partialExecutor.Contexts.Select(item => item.CaseId));
        Assert.Equal("SD.BATCH.CASE_FAILED", Assert.Single(partial.Cases[1].Diagnostics).Code);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelledExecutor = new ClosedBatchExecutor(
            (_, _) => throw new InvalidOperationException("Cancelled batch invoked its executor."));
        BatchRunResult cancelled = await BatchRunner.RunAsync(
            new[] { ClosedBatchCase("cancelled-case") },
            cancelledExecutor,
            ClosedBatchOptions(Path.Combine(context.Directory.Path, "batch-cancelled"), parallelism: 1),
            cancellationToken: cancellation.Token);
        Assert.Equal(BatchCaseStatus.Cancelled, Assert.Single(cancelled.Cases).Status);
        Assert.Equal(0, cancelledExecutor.InvocationCount);
        return new[]
        {
            "empty_batch=result-with-zero-cases",
            "executor_exception=isolated-failed-case",
            "later_cases=continued",
            "pre_cancelled_batch=no-executor-invocation",
            "return_shape=BatchRunResult",
        };
    }

    private static async Task<string[]> ExecuteBatchScalarCaseAsync(NativeContext context)
    {
        string outputRoot = Path.Combine(context.Directory.Path, "batch-scalar");
        BatchCaseDefinition[] cases = { ClosedBatchCase("scalar-case") };
        var firstExecutor = new ClosedBatchExecutor(
            (item, _) => Task.FromResult(BatchCaseExecution.Success(
                new Dictionary<string, double> { ["index"] = item.Index })));
        var secondExecutor = new ClosedBatchExecutor(
            (item, _) => Task.FromResult(BatchCaseExecution.Success(
                new Dictionary<string, double> { ["index"] = item.Index })));
        BatchRunOptions options = ClosedBatchOptions(outputRoot, parallelism: 1);
        BatchRunResult first = await BatchRunner.RunAsync(cases, firstExecutor, options);
        BatchRunResult second = await BatchRunner.RunAsync(cases, secondExecutor, options);
        Assert.Single(first.Cases);
        Assert.Single(second.Cases);
        Assert.Equal("scalar-case", first.Cases[0].CaseId);
        Assert.Equal(BatchCaseStatus.Succeeded, first.Cases[0].Status);
        Assert.Equal(first.RunFingerprint, second.RunFingerprint);
        Assert.Equal(first.CombinedCsv, second.CombinedCsv);
        Assert.Equal(first.ReproducibilityManifest, second.ReproducibilityManifest);
        Assert.IsType<BatchRunResult>(first);
        return new[]
        {
            "single_case=explicit-typed-definition",
            "return_shape=BatchRunResult",
            "run_fingerprint=deterministic",
            "serialized_outputs=deterministic",
        };
    }

    private static async Task<string[]> ExecuteSingleRetainCaseAsync(NativeContext context)
    {
        EnergyPlusRuntimeLayout runtime = await context.GetRuntimeAsync();
        string input = context.Directory.WriteFile("single-retain/model.idf", "Version,24.2;");
        string weather = context.Directory.WriteFile("single-retain/weather.epw", "closed weather");
        string tempRoot = Path.Combine(context.Directory.Path, "single-retain-runs");
        DelegateProcessExecutor executor = CreateSuccessfulProcessExecutor(tempRoot);
        EnergyPlusRunResult result = await new EnergyPlusRunner(executor).RunAsync(
            new EnergyPlusRunRequest(runtime, input, weather, tempRoot)
            {
                CleanupPolicy = EnergyPlusCleanupPolicy.KeepAlways,
            });
        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.True(result.WorkDirectoryRetained);
        Assert.NotNull(result.WorkDirectory);
        AssertMarkedDescendant(tempRoot, result.WorkDirectory!);
        Assert.Equal(2, executor.Requests.Count);
        Assert.Equal("closed audit", result.Outputs.Audit?.TextContent);
        ProcessExecutionRequest energy = executor.Requests.Single(item => item.Stage == EnergyPlusProcessStage.EnergyPlus);
        int iddIndex = energy.Arguments.ToList().IndexOf("-i");
        Assert.True(iddIndex >= 0);
        Assert.True(RuntimeFileSystem.IsDescendantOf(result.WorkDirectory!, energy.Arguments[iddIndex + 1]));
        return new[]
        {
            "runtime=caller-supplied-hash-verified-layout",
            "work_directory=marked-controlled-descendant",
            "cleanup=KeepAlways-retained",
            "artifacts=typed-and-captured",
        };
    }

    private static async Task<string[]> ExecuteSingleDeleteCaseAsync(NativeContext context)
    {
        EnergyPlusRuntimeLayout runtime = await context.GetRuntimeAsync();
        string input = context.Directory.WriteFile("single-delete/model.idf", "Version,123.2.0;");
        string tempRoot = Path.Combine(context.Directory.Path, "single-delete-runs");
        DelegateProcessExecutor executor = CreateSuccessfulProcessExecutor(tempRoot);
        EnergyPlusRunResult result = await new EnergyPlusRunner(executor).RunAsync(
            new EnergyPlusRunRequest(runtime, input, WeatherFilePath: null, tempRoot)
            {
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess,
            });
        Assert.True(result.IsSuccess, result.Failure?.Detail ?? result.Failure?.Message);
        Assert.False(result.WorkDirectoryRetained);
        Assert.NotNull(result.WorkDirectory);
        Assert.False(Directory.Exists(result.WorkDirectory));
        Assert.Equal("closed audit", result.Outputs.Audit?.TextContent);
        Assert.Equal(2, executor.Requests.Count);
        return new[]
        {
            "version_inference=not-used",
            "runtime=explicit-verified-layout",
            "cleanup=DeleteOnSuccess-removed-marked-run",
            "captured_artifacts=retained-in-result",
        };
    }

    private static async Task<string[]> ExecuteSingleTransactionalityCaseAsync(NativeContext context)
    {
        EnergyPlusRuntimeLayout runtime = await context.GetRuntimeAsync();
        string tempRoot = Path.Combine(context.Directory.Path, "single-transaction-runs");
        DelegateProcessExecutor untouchedExecutor = CreateSuccessfulProcessExecutor(tempRoot);
        EnergyPlusRunResult invalid = await new EnergyPlusRunner(untouchedExecutor).RunAsync(
            new EnergyPlusRunRequest(
                runtime,
                Path.Combine(context.Directory.Path, "missing-model.idf"),
                WeatherFilePath: null,
                tempRoot));
        Assert.Equal(EnergyPlusFailureCategory.UserInput, invalid.Failure?.Category);
        Assert.Equal("INPUT_IDF_NOT_FOUND", invalid.Failure?.Code);
        Assert.Null(invalid.WorkDirectory);
        Assert.Empty(untouchedExecutor.Requests);

        string input = context.Directory.WriteFile("single-transaction/model.idf", "Version,24.2;");
        DelegateProcessExecutor retainedFailureExecutor = CreateFailingProcessExecutor(tempRoot, exitCode: 9);
        EnergyPlusRunResult retainedFailure = await new EnergyPlusRunner(retainedFailureExecutor).RunAsync(
            new EnergyPlusRunRequest(runtime, input, WeatherFilePath: null, tempRoot)
            {
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteOnSuccess,
            });
        Assert.Equal(EnergyPlusRunState.Failed, retainedFailure.State);
        Assert.Equal(EnergyPlusFailureCategory.ProcessFailure, retainedFailure.Failure?.Category);
        Assert.Equal("EXPANDOBJECTS_FAILED", retainedFailure.Failure?.Code);
        Assert.True(retainedFailure.WorkDirectoryRetained);
        AssertMarkedDescendant(tempRoot, retainedFailure.WorkDirectory!);

        DelegateProcessExecutor deletedFailureExecutor = CreateFailingProcessExecutor(tempRoot, exitCode: 7);
        EnergyPlusRunResult deletedFailure = await new EnergyPlusRunner(deletedFailureExecutor).RunAsync(
            new EnergyPlusRunRequest(runtime, input, WeatherFilePath: null, tempRoot)
            {
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways,
            });
        Assert.Equal(EnergyPlusRunState.Failed, deletedFailure.State);
        Assert.False(deletedFailure.WorkDirectoryRetained);
        Assert.NotNull(deletedFailure.WorkDirectory);
        Assert.False(Directory.Exists(deletedFailure.WorkDirectory));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        DelegateProcessExecutor cancelledExecutor = CreateSuccessfulProcessExecutor(tempRoot);
        EnergyPlusRunResult cancelled = await new EnergyPlusRunner(cancelledExecutor).RunAsync(
            new EnergyPlusRunRequest(runtime, input, WeatherFilePath: null, tempRoot),
            cancellation.Token);
        Assert.Equal(EnergyPlusRunState.Cancelled, cancelled.State);
        Assert.Equal(EnergyPlusFailureCategory.Cancelled, cancelled.Failure?.Category);
        Assert.Null(cancelled.WorkDirectory);
        Assert.Empty(cancelledExecutor.Requests);

        DelegateProcessExecutor timeoutExecutor = CreateCancellableProcessExecutor(tempRoot);
        EnergyPlusRunResult timedOut = await new EnergyPlusRunner(timeoutExecutor).RunAsync(
            new EnergyPlusRunRequest(runtime, input, WeatherFilePath: null, tempRoot)
            {
                CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways,
                Timeout = TimeSpan.FromMilliseconds(20),
            });
        Assert.Equal(EnergyPlusRunState.TimedOut, timedOut.State);
        Assert.Equal(EnergyPlusFailureCategory.Timeout, timedOut.Failure?.Category);
        Assert.False(timedOut.WorkDirectoryRetained);
        Assert.NotNull(timedOut.WorkDirectory);
        Assert.False(Directory.Exists(timedOut.WorkDirectory));
        return new[]
        {
            "invalid_input=fail-before-work-or-process",
            "process_failure=structured-and-retained-for-diagnosis",
            "DeleteAlways=marked-run-removed-on-failure",
            "caller_cancellation=structured-before-process",
            "timeout=structured-and-marked-run-removed",
        };
    }

    private static Task<EnergyPlusRuntimeResolution> ResolveClosedAsync(
        RuntimeResolver resolver,
        EnergyPlusRuntimeResolveOptions options)
    {
        return resolver.ResolveAsync(options with
        {
            SearchDefaultCacheLocation = false,
            SearchDefaultInstallLocation = false,
            SearchEnvironmentVariables = false,
        });
    }

    private static EnergyPlusRuntimeResolveOptions ClosedResolveOptions()
    {
        return new EnergyPlusRuntimeResolveOptions
        {
            SearchDefaultCacheLocation = false,
            SearchDefaultInstallLocation = false,
            SearchEnvironmentVariables = false,
        };
    }

    private static EnergyPlusRuntimeManifest WriteRuntimePayload(string root)
    {
        Directory.CreateDirectory(root);
        string energyPlus = WriteUtf8File(root, "energyplus.exe", "closed-energyplus");
        string expandObjects = WriteUtf8File(root, "ExpandObjects.exe", "closed-expandobjects");
        string idd = WriteUtf8File(root, "Energy+.idd", "closed-idd");
        string schema = WriteUtf8File(root, "Energy+.schema.epJSON", "{\"closed\":true}");
        return EnergyPlusRuntimeManifest.Supported with
        {
            EnergyPlusExecutableSha256 = TestRuntimeFactory.Hash(energyPlus),
            ExpandObjectsSha256 = TestRuntimeFactory.Hash(expandObjects),
            EnergyPlusIddSha256 = TestRuntimeFactory.Hash(idd),
            EnergyPlusEpJsonSchemaSha256 = TestRuntimeFactory.Hash(schema),
        };
    }

    private static string WriteUtf8File(string root, string name, string contents)
    {
        string path = Path.Combine(root, name);
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static BatchCaseDefinition ClosedBatchCase(string caseId)
    {
        var model = new GreenRetrofitModel(
            "Closed model",
            northAxis: 0d,
            "Closed address",
            new DateTime(2020, 1, 1),
            isMultifamilyHousing: false,
            Array.Empty<BuildingFloor>(),
            Array.Empty<Material>(),
            Array.Empty<SurfaceConstruction>(),
            Array.Empty<FenestrationConstruction>());
        return new BatchCaseDefinition(model, caseId);
    }

    private static BatchRunOptions ClosedBatchOptions(string outputRoot, int parallelism)
    {
        return new BatchRunOptions
        {
            MaxDegreeOfParallelism = parallelism,
            UseCache = false,
            WriteOutputs = false,
            OutputRootPath = outputRoot,
        };
    }

    private static DelegateProcessExecutor CreateSuccessfulProcessExecutor(string tempRoot)
    {
        return new DelegateProcessExecutor((request, _) =>
        {
            AssertMarkedDescendant(tempRoot, request.WorkingDirectory);
            if (request.Stage == EnergyPlusProcessStage.ExpandObjects)
            {
                File.Copy(
                    Path.Combine(request.WorkingDirectory, "in.idf"),
                    Path.Combine(request.WorkingDirectory, "expanded.idf"));
            }
            else
            {
                int outputIndex = request.Arguments.ToList().IndexOf("-d");
                Assert.True(outputIndex >= 0);
                string outputDirectory = request.Arguments[outputIndex + 1];
                Assert.True(RuntimeFileSystem.IsDescendantOf(request.WorkingDirectory, outputDirectory));
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(
                    Path.Combine(outputDirectory, "eplusout.err"),
                    "** Warning ** closed warning",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.WriteAllText(
                    Path.Combine(outputDirectory, "eplusout.audit"),
                    "closed audit",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            return Task.FromResult(DelegateProcessExecutor.Exited(request));
        });
    }

    private static DelegateProcessExecutor CreateFailingProcessExecutor(string tempRoot, int exitCode)
    {
        return new DelegateProcessExecutor((request, _) =>
        {
            AssertMarkedDescendant(tempRoot, request.WorkingDirectory);
            return Task.FromResult(DelegateProcessExecutor.Exited(request, exitCode));
        });
    }

    private static DelegateProcessExecutor CreateCancellableProcessExecutor(string tempRoot)
    {
        return new DelegateProcessExecutor(async (request, cancellationToken) =>
        {
            AssertMarkedDescendant(tempRoot, request.WorkingDirectory);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return DelegateProcessExecutor.Cancelled(request);
            }

            throw new Xunit.Sdk.XunitException("The closed process wait unexpectedly completed.");
        });
    }

    private static void AssertMarkedDescendant(string tempRoot, string workDirectory)
    {
        Assert.True(RuntimeFileSystem.IsDescendantOf(tempRoot, workDirectory));
        Assert.True(File.Exists(Path.Combine(workDirectory, MarkerFileName)));
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

    private static void AssertNoHostTokens(string value)
    {
        Assert.False(Regex.IsMatch(
            value,
            @"(?i)(?:[A-Z]:[\\/]|\\\\[A-Z0-9_.-]+[\\/])",
            RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(
            value,
            @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])",
            RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(
            value,
            @"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d",
            RegexOptions.CultureInvariant));
        Assert.DoesNotContain("dragons-launcher-runtime-oracle-", value, StringComparison.OrdinalIgnoreCase);
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

    private static void AssertKeysAllowingOptional(
        JsonElement value,
        IReadOnlyCollection<string> required,
        params string[] optional)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject().Select(item => item.Name).ToArray();
        Assert.All(required, name => Assert.Contains(name, actual));
        var allowed = new HashSet<string>(required, StringComparer.Ordinal);
        allowed.UnionWith(optional);
        Assert.All(actual, name => Assert.Contains(name, allowed));
    }

    private static string[] Strings(JsonElement value)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
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

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class NativeContext
    {
        private Task<EnergyPlusRuntimeLayout>? runtime;

        internal NativeContext(TestDirectory directory)
        {
            Directory = directory;
        }

        internal TestDirectory Directory { get; }

        internal Task<EnergyPlusRuntimeLayout> GetRuntimeAsync()
        {
            return runtime ??= CreateRuntimeAsync();
        }

        private async Task<EnergyPlusRuntimeLayout> CreateRuntimeAsync()
        {
            string root = Path.Combine(Directory.Path, "runner-runtime");
            EnergyPlusRuntimeManifest manifest = WriteRuntimePayload(root);
            EnergyPlusRuntimeResolution resolution = await ResolveClosedAsync(
                new RuntimeResolver(manifest),
                new EnergyPlusRuntimeResolveOptions { RuntimeRoot = root });
            Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);
            Assert.True(RuntimeFileSystem.IsDescendantOf(Directory.Path, resolution.Runtime!.RootPath));
            return resolution.Runtime;
        }
    }

    private sealed class ClosedBatchExecutor : IBatchCaseExecutor
    {
        private readonly Func<BatchCaseContext, CancellationToken, Task<BatchCaseExecution>> execute;
        private readonly int rendezvousCount;
        private readonly TaskCompletionSource<bool> rendezvous = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<BatchCaseContext> contexts = new();
        private int active;
        private int invocations;
        private int maximumActive;
        private int rendezvousArrivals;

        internal ClosedBatchExecutor(
            Func<BatchCaseContext, CancellationToken, Task<BatchCaseExecution>> execute,
            int rendezvousCount = 1)
        {
            this.execute = execute;
            this.rendezvousCount = rendezvousCount;
            if (rendezvousCount <= 1)
            {
                rendezvous.TrySetResult(true);
            }
        }

        public string ExecutorIdentity => "Dragons.ClosedLauncherRuntimeBatchExecutor/v1";

        public BatchRuntimeIdentity RuntimeIdentity { get; } = new(
            "24.2.0",
            "closed-build",
            new string('1', 64),
            new string('2', 64),
            new string('3', 64));

        public string CanonicalExecutionOptions => "{}";

        public string CanonicalOutputOptions => "{}";

        internal int InvocationCount => Volatile.Read(ref invocations);

        internal int MaximumActive => Volatile.Read(ref maximumActive);

        internal IReadOnlyList<BatchCaseContext> Contexts => contexts
            .OrderBy(item => item.Index)
            .ToArray();

        public async Task<BatchCaseExecution> ExecuteAsync(
            BatchCaseContext context,
            CancellationToken cancellationToken)
        {
            contexts.Enqueue(context);
            Interlocked.Increment(ref invocations);
            int currentActive = Interlocked.Increment(ref active);
            UpdateMaximum(currentActive);
            try
            {
                if (Interlocked.Increment(ref rendezvousArrivals) >= rendezvousCount)
                {
                    rendezvous.TrySetResult(true);
                }

                await rendezvous.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                return await execute(context, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        private void UpdateMaximum(int candidate)
        {
            int current;
            do
            {
                current = Volatile.Read(ref maximumActive);
                if (candidate <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref maximumActive, candidate, current) != current);
        }
    }

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
        string NativeTarget,
        IReadOnlyList<string> NativeFacts);
}
