using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Profile;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class SupplyGroupCoreOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-hvac-supply-group-core-oracle.json";
    private const string OracleSchema =
        "dragons.python-reference.dragon-hvac-supply-group-core.v1";
    private const string OracleSha256 =
        "sha256:32f05de2a2ead16e0097d3402577e8bce03f40ea151162a6312000bb4f5a5886";
    private const string CasesSha256 =
        "sha256:b429a0dbefc2ac0411f53bfc705fcbfb984fffcf6859a1a3be7e355bc47a9b8a";
    private const int OracleByteLength = 31_160;
    private const int ExpectedCaseCount = 18;
    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamPath = "src/idragon/dragon/hvac.py";
    private const string SupplyGroupTypeName =
        "Dragons.InvisibleDragon.Hvac.SupplyGroup";
    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs";
    private const string ImplementationSha256 =
        "sha256:fcbe9c38cacade8002d121b0834a4441560086052571dd654f3c185a0c897249";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Hvac.SupplyGroupCoreOracleParityTests.MatchesPinnedPythonSupplyGroupCore";

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/dragon/hvac.py", "SupplyGroup.__init__", "sha256:02b3c43aa048fd31a3ffc31fea96f5086a599d3245847e217dc0c99a9cf5fddd", "dragon-hvac-supply-group-core-init-02b3c43a"),
        new("src/idragon/dragon/hvac.py", "SupplyGroup.coolable", "sha256:0f6f3f1afaac0b5144d7a4f3af1857e2d5d6ca2e02baf98d0427cd1a317abd36", "dragon-hvac-supply-group-core-coolable-0f6f3f1a"),
        new("src/idragon/dragon/hvac.py", "SupplyGroup.cooling_systems", "sha256:e2ee9492964b6c3eeaa5d54700d66a010198413a3c006edd46427c126150221c", "dragon-hvac-supply-group-core-cooling-systems-e2ee9492"),
        new("src/idragon/dragon/hvac.py", "SupplyGroup.heatable", "sha256:ab11abdd7afeb3b7fde0805ce2697af2df49ad6e874691b164bb5674ae9ac655", "dragon-hvac-supply-group-core-heatable-ab11abdd"),
        new("src/idragon/dragon/hvac.py", "SupplyGroup.heating_systems", "sha256:1fdfba66763618fe1880c3d0354b764e551c8a7747eb4a1dedac24d375f87dc2", "dragon-hvac-supply-group-core-heating-systems-1fdfba66"),
        new("src/idragon/dragon/hvac.py", "SupplyGroup.sources", "sha256:482d0fa2c4cc9f732bc33911ae01ea857e3042ff4cf60e680583f2abefdab423", "dragon-hvac-supply-group-core-sources-482d0fa2"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("SupplyGroup.__init__", "function", "sha256:f01960cc5a0c00e094cf2eb094922d734343c92c8ec849977ea8b86337805907", "sha256:643ca4afc57e9a0b22eee5df0a2cd7b90d9d579cf16bb20fd6d6a9e40b5bc57c", "exception", "immutable-validated-supply-group-construction", "SupplyGroup", SupplyGroupTypeName),
        new("SupplyGroup.coolable", "function", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:73f3cb2b0806dccc2593dcdb1412835c8258e8bca42388c75c1ba2e3038afa56", "equivalent", null, "SupplyGroup.CanCool", SupplyGroupTypeName + ".CanCool"),
        new("SupplyGroup.cooling_systems", "function", "sha256:97cc1e2d625ebc73e65314802efcf1b1278d42ee34f0bba31a167bb7a7525344", "sha256:ba298377ff3ee58bec8d56a856e9fab8941fec0cfd35321d6e48c7c9b3df9c89", "equivalent", null, "SupplyGroup.CoolingSystems", SupplyGroupTypeName + ".CoolingSystems"),
        new("SupplyGroup.heatable", "function", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:ac6066fdc4f9bed2e2f6b2f7c5910634c34c5b26f84a6bb5c4e2049deb3d8096", "equivalent", null, "SupplyGroup.CanHeat", SupplyGroupTypeName + ".CanHeat"),
        new("SupplyGroup.heating_systems", "function", "sha256:97cc1e2d625ebc73e65314802efcf1b1278d42ee34f0bba31a167bb7a7525344", "sha256:f1ea945dbd140a8fed2b6adf855e9751375c5ca15aae41b6d9a591c25d3291f1", "equivalent", null, "SupplyGroup.HeatingSystems", SupplyGroupTypeName + ".HeatingSystems"),
        new("SupplyGroup.sources", "function", "sha256:74055a2ba47ab60bd034a8ca75be001a2cd1b1c1e78e201eed646b37d5b2065d", "sha256:8380d67f068d32acc9710838b7314fd04f3acf74b53e5f9484cdde3b07e3d09d", "exception", "stable-entity-id-supply-source-deduplication", "SupplyGroup.Sources", SupplyGroupTypeName + ".Sources"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-hvac-supply-group-core.coolable.cooling-only-true", "supply-group-coolable", "SupplyGroup.coolable", "sha256:46aa619e40a02b6d558f0692655ea73b9ff1b345177a31d358640fbb910183fd"),
        new("dragon-hvac-supply-group-core.coolable.heating-only-false", "supply-group-coolable", "SupplyGroup.coolable", "sha256:a269c6fb8073538b8a1d002397c91dcecce6c29b850b5688409940458b7edb18"),
        new("dragon-hvac-supply-group-core.coolable.mixed-capability-true", "supply-group-coolable", "SupplyGroup.coolable", "sha256:105f5de646af3b4b297a62a286ac6ceea0d9ddfd6609adfa02833dcdca9f97f8"),
        new("dragon-hvac-supply-group-core.cooling-systems.distinct-members-and-order", "supply-group-cooling-systems", "SupplyGroup.cooling_systems", "sha256:ddafe78e54e7b41e069358cd1bddc3088ddc511a12df03f9b3431aa8b484d3a4"),
        new("dragon-hvac-supply-group-core.cooling-systems.fresh-tuple", "supply-group-cooling-systems", "SupplyGroup.cooling_systems", "sha256:492368c6970ea91cc944985ec84042ac6334bb444da280155900396cf27e0434"),
        new("dragon-hvac-supply-group-core.cooling-systems.heating-only-empty", "supply-group-cooling-systems", "SupplyGroup.cooling_systems", "sha256:9fc41a4183f8f0f9689b6d19050db8cfa9af7b92e5bfe2cf5c25e1f91f209bc0"),
        new("dragon-hvac-supply-group-core.heatable.cooling-only-false", "supply-group-heatable", "SupplyGroup.heatable", "sha256:0a16747c3c7cb5e873047e995a6bd27d6924dccac4ab57de534ebbf1469158f5"),
        new("dragon-hvac-supply-group-core.heatable.heating-only-true", "supply-group-heatable", "SupplyGroup.heatable", "sha256:7b7e341002fdb2c97daaa8aac1c5833f221bd647d639904189061f608cd008c5"),
        new("dragon-hvac-supply-group-core.heatable.mixed-capability-true", "supply-group-heatable", "SupplyGroup.heatable", "sha256:a7eeeec91ba7c39e878bd714a403c614e99d2279361e5e1b1a14aae7615d2147"),
        new("dragon-hvac-supply-group-core.heating-systems.cooling-only-empty", "supply-group-heating-systems", "SupplyGroup.heating_systems", "sha256:98d5b201d97cb1d7779e8aa1c096a01039ffb4023d914dbe2af3bcfe358e612e"),
        new("dragon-hvac-supply-group-core.heating-systems.distinct-members-and-order", "supply-group-heating-systems", "SupplyGroup.heating_systems", "sha256:1773b9ef36fca34a9669a37cc873957354cea986979c8d5b4fc4eb3a7b81a31c"),
        new("dragon-hvac-supply-group-core.heating-systems.fresh-tuple", "supply-group-heating-systems", "SupplyGroup.heating_systems", "sha256:2fb07ae26fdc78f101efc800aab9d08fd2a7f74a4597e4f17f4755f545ffbf05"),
        new("dragon-hvac-supply-group-core.init.defaults-and-snapshot", "supply-group-init", "SupplyGroup.__init__", "sha256:fbd550f5bd19326821ea7f7d11cbd69c620600dc5da6270e9b5d6221eb087f34"),
        new("dragon-hvac-supply-group-core.init.duplicates-and-explicit-availabilities", "supply-group-init", "SupplyGroup.__init__", "sha256:dd951ddc540e2015d5a48661bd4897e4f14b5ee61b062d2ee8dd396f7f83c762"),
        new("dragon-hvac-supply-group-core.init.validation-order", "supply-group-init", "SupplyGroup.__init__", "sha256:8d17fd6abf8ff1bd2bd04632188370e5ca2809a5a5d0e4b172210134a5987262"),
        new("dragon-hvac-supply-group-core.sources.distinct-equal-sources", "supply-group-sources", "SupplyGroup.sources", "sha256:30d7db3ab87755f796cad4620abbe3c9d959389a73fb7853a8edf26f29f6d3fa"),
        new("dragon-hvac-supply-group-core.sources.distinct-identifiers-first-seen", "supply-group-sources", "SupplyGroup.sources", "sha256:627dae80e77649b68542a9d6b8e6bcd18de448c9b2cc44fab9c38fb8af8805f0"),
        new("dragon-hvac-supply-group-core.sources.identity-dedup-and-none", "supply-group-sources", "SupplyGroup.sources", "sha256:3849027497731570e1f9c8270ae7c6128248621821aad500dc2a020e2a874721"),
    };

    private static readonly SourceBinding[] ExpectedSources =
    {
        new("idragon", "src/idragon/__init__.py", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618"),
        new("idragon.common", "src/idragon/common.py", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9"),
        new("idragon.constants", "src/idragon/constants.py", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"),
        new("idragon.dragon", "src/idragon/dragon/__init__.py", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52"),
        new("idragon.dragon.construction", "src/idragon/dragon/construction.py", "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"),
        new("idragon.dragon.hvac", "src/idragon/dragon/hvac.py", "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0", "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"),
        new("idragon.dragon.model", "src/idragon/dragon/model.py", "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"),
        new("idragon.dragon.profile", "src/idragon/dragon/profile.py", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef"),
        new("idragon.dragon.shape", "src/idragon/dragon/shape.py", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"),
        new("idragon.imugi", "src/idragon/imugi.py", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"),
        new("idragon.launcher", "src/idragon/launcher.py", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e"),
        new("idragon.utils", "src/idragon/utils.py", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452"),
    };

    [Fact]
    public void MatchesPinnedPythonSupplyGroupCore()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(OracleRepositoryPath));
        string sha256 = Sha256(bytes);
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, sha256);

        using JsonDocument oracle = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        ValidateNativeBindings();

        NativeObservation[] observations = cases
            .Select((item, index) =>
            {
                CaseBinding binding = ExpectedCases[index];
                SymbolContract symbol = Assert.Single(
                    ExpectedSymbols,
                    candidate => candidate.Symbol == binding.Symbol);
                JsonElement pythonFacts = item.GetProperty("python").GetProperty("facts");
                string[] facts = ExecuteNativeCase(binding, pythonFacts);
                Assert.Equal(4, facts.Length);
                Assert.Equal(4, facts.Distinct(StringComparer.Ordinal).Count());
                Assert.All(facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
                return new NativeObservation(
                    binding.CaseId,
                    binding.Symbol,
                    symbol.AdaptationId,
                    facts);
            })
            .ToArray();
        Assert.Equal(ExpectedCaseCount, observations.Length);

        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            SymbolContract symbol = Assert.Single(
                ExpectedSymbols,
                candidate => candidate.Symbol == evidence.Symbol);
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
                native_binding = new
                {
                    adaptation_id = symbol.AdaptationId,
                    implementation_path = ImplementationRepositoryPath,
                    implementation_sha256 = ImplementationSha256,
                    implementation_symbol = symbol.ImplementationSymbol,
                    public_target = symbol.NativeTarget,
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
            ValidateReceipt(receiptJson, evidence, symbol, symbolObservations);
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
        AssertKeys(root, "cases", "cases_sha256", "consumer_contract", "runtime", "schema", "symbols", "upstream");
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));
        AssertNoRawAddresses(root.GetRawText());
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);

        ValidateUpstream(root.GetProperty("upstream"));
        ValidateRuntime(root.GetProperty("runtime"));
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

        Assert.All(
            cases.GroupBy(item => RequiredString(item, "symbol")),
            group => Assert.Equal(3, group.Count()));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(upstream, "commit", "inventory_sha256", "loaded_local_modules", "sources");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventorySha256, RequiredString(upstream, "inventory_sha256"));

        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        JsonElement[] modules = upstream.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(ExpectedSources.Length, sources.Length);
        Assert.Equal(ExpectedSources.Length, modules.Length);
        for (int index = 0; index < ExpectedSources.Length; index++)
        {
            SourceBinding expected = ExpectedSources[index];
            JsonElement source = sources[index];
            AssertKeys(source, "ast_sha256", "path", "source_sha256");
            Assert.Equal(expected.Path, RequiredString(source, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(source, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(source, "ast_sha256"));

            JsonElement module = modules[index];
            AssertKeys(module, "ast_sha256", "module", "path", "source_sha256");
            Assert.Equal(expected.Module, RequiredString(module, "module"));
            Assert.Equal(expected.Path, RequiredString(module, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(module, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(module, "ast_sha256"));
        }

        Assert.Equal(
            sources.Select(item => RequiredString(item, "path")),
            modules.Select(item => RequiredString(item, "path")));
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "implementation",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());

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
        Assert.Equal(6, ExpectedEvidence.Length);
        Assert.Equal(6, ExpectedSymbols.Length);
        Assert.Equal(
            ExpectedEvidence.Select(item => item.Symbol),
            ExpectedSymbols.Select(item => item.Symbol));
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => (item.Path, item.Symbol)).Distinct().Count());
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ExpectedEvidence, item =>
        {
            Assert.Equal(UpstreamPath, item.Path);
            Assert.Matches("^sha256:[0-9a-f]{64}$", item.SymbolHash);
            Assert.Matches("^[a-z0-9][a-z0-9-]+$", item.AssertionId);
        });
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            SymbolContract expected = ExpectedSymbols[index];
            JsonElement symbol = symbols[index];
            AssertKeys(symbol, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
            Assert.Equal(expected.Symbol, RequiredString(symbol, "symbol"));
            Assert.Equal(expected.Kind, RequiredString(symbol, "kind"));
            Assert.Equal(expected.SignatureHash, RequiredString(symbol, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(symbol, "body_hash"));
            Assert.Equal(ExpectedEvidence[index].SymbolHash, RequiredString(symbol, "symbol_hash"));
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
            "closure",
            "identity_encoding",
            "native_targets",
            "source_import_policy",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(contract.GetProperty("target_symbols"), ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal("logical-labels-only-no-id-or-address", RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "external-temporary-copy-with-complete-loaded-local-module-audit",
            RequiredString(contract, "source_import_policy"));

        JsonElement adaptations = contract.GetProperty("adaptations");
        AssertKeys(adaptations, "SupplyGroup.__init__", "SupplyGroup.sources");
        Assert.Equal(
            "immutable-validated-supply-group-construction",
            RequiredString(adaptations, "SupplyGroup.__init__"));
        Assert.Equal(
            "stable-entity-id-supply-source-deduplication",
            RequiredString(adaptations, "SupplyGroup.sources"));

        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement targets = contract.GetProperty("native_targets");
        string[] symbols = ExpectedSymbols.Select(item => item.Symbol).ToArray();
        AssertKeys(assertions, symbols);
        AssertKeys(classifications, symbols);
        AssertKeys(targets, symbols);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            SymbolContract expected = ExpectedSymbols[index];
            Assert.Equal(ExpectedEvidence[index].AssertionId, RequiredString(assertions, expected.Symbol));
            Assert.Equal(expected.Classification, RequiredString(classifications, expected.Symbol));
            Assert.Equal(expected.NativeTarget, RequiredString(targets, expected.Symbol));
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(closure, "full_symbol_closure", "scope", "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal("bounded-supply-group-container-evidence", RequiredString(closure, "scope"));
        AssertStringArray(
            closure.GetProperty("unresolved_behavior"),
            "SupplyGroup",
            "SupplyGroup.to_idf_object",
            "SupplySystem",
            "concrete-supply-systems",
            "supply-system-postprocessors",
            "EnergyModel.to_idf");
    }

    private static void ValidateCase(JsonElement item, CaseBinding expected)
    {
        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == expected.Symbol);
        if (symbol.AdaptationId is null)
        {
            AssertKeys(item, "executor", "id", "python", "symbol");
        }
        else
        {
            AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
            JsonElement expectedDotNet = item.GetProperty("expected_dotnet");
            AssertKeys(expectedDotNet, "adaptation", "outcome");
            Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotNet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotNet, "outcome"));
        }

        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Executor, RequiredString(item, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        Assert.Equal(JsonValueKind.Object, facts.ValueKind);
        Assert.NotEmpty(facts.EnumerateObject());
        Assert.Equal(expected.FactsSha256, CanonicalSha256(facts));
    }

    private static void ValidateNativeBindings()
    {
        Assert.Equal(SupplyGroupTypeName, typeof(SupplyGroup).FullName);
        string implementationPath = FindRepositoryFile(ImplementationRepositoryPath);
        Assert.Equal(ImplementationSha256, Sha256(File.ReadAllBytes(implementationPath)));

        ConstructorInfo constructor = Assert.Single(
            typeof(SupplyGroup).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        ParameterInfo[] parameters = constructor.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(IEnumerable<SupplySystem>), parameters[0].ParameterType);
        Assert.Equal("systems", parameters[0].Name);
        Assert.False(parameters[0].HasDefaultValue);
        Assert.Equal(typeof(IEnumerable<Schedule>), parameters[1].ParameterType);
        Assert.Equal("availabilities", parameters[1].Name);
        Assert.True(parameters[1].HasDefaultValue);
        Assert.Null(parameters[1].DefaultValue);

        AssertProperty<IReadOnlyList<SupplySystem>>(nameof(SupplyGroup.Systems));
        AssertProperty<IReadOnlyList<Schedule?>>(nameof(SupplyGroup.Availabilities));
        AssertProperty<bool>(nameof(SupplyGroup.CanCool));
        AssertProperty<IReadOnlyList<SupplySystem>>(nameof(SupplyGroup.CoolingSystems));
        AssertProperty<bool>(nameof(SupplyGroup.CanHeat));
        AssertProperty<IReadOnlyList<SupplySystem>>(nameof(SupplyGroup.HeatingSystems));
        AssertProperty<IReadOnlyList<SourceSystem>>(nameof(SupplyGroup.Sources));

        Assert.Equal(SupplyGroupTypeName, ExpectedSymbols[0].ImplementationSymbol);
        Assert.Equal(SupplyGroupTypeName + ".CanCool", ExpectedSymbols[1].ImplementationSymbol);
        Assert.Equal(SupplyGroupTypeName + ".CoolingSystems", ExpectedSymbols[2].ImplementationSymbol);
        Assert.Equal(SupplyGroupTypeName + ".CanHeat", ExpectedSymbols[3].ImplementationSymbol);
        Assert.Equal(SupplyGroupTypeName + ".HeatingSystems", ExpectedSymbols[4].ImplementationSymbol);
        Assert.Equal(SupplyGroupTypeName + ".Sources", ExpectedSymbols[5].ImplementationSymbol);
    }

    private static void AssertProperty<T>(string name)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(
            typeof(SupplyGroup).GetProperty(name, BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(SupplyGroup), property.DeclaringType);
        Assert.Equal(typeof(T), property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.False(property.GetMethod.IsStatic);
        Assert.Null(property.SetMethod);
        Assert.Empty(property.GetIndexParameters());
    }

    private static string[] ExecuteNativeCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Executor switch
        {
            "supply-group-coolable" => ExecuteCanCool(binding.CaseId, pythonFacts),
            "supply-group-cooling-systems" => ExecuteCoolingSystems(binding.CaseId, pythonFacts),
            "supply-group-heatable" => ExecuteCanHeat(binding.CaseId, pythonFacts),
            "supply-group-heating-systems" => ExecuteHeatingSystems(binding.CaseId, pythonFacts),
            "supply-group-init" => ExecuteInit(binding.CaseId, pythonFacts),
            "supply-group-sources" => ExecuteSources(binding.CaseId, pythonFacts),
            _ => throw new InvalidOperationException("Unknown SupplyGroup executor: " + binding.Executor),
        };
    }

    private static string[] ExecuteCanCool(string caseId, JsonElement pythonFacts)
    {
        if (caseId == ExpectedCases[0].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem cooling = Observed(
                "COOLABLE-COOL",
                "cool-only",
                canHeat: false,
                canCool: true,
                reads);
            LabeledSystem[] systems = { new("cool-only", cooling) };
            var group = GroupFrom(systems);
            reads.Clear();
            bool result = group.CanCool;
            Assert.True(result);
            AssertPythonBooleanFacts(pythonFacts, reads, result);
            Assert.False(group.CanHeat);
            return Facts(
                "native-can-cool=true",
                "native-can-heat=false",
                "native-input=cool-only",
                "native-python-facts-bound=true");
        }

        if (caseId == ExpectedCases[1].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem heating = Observed(
                "COOLABLE-HEAT",
                "heat-only",
                canHeat: true,
                canCool: false,
                reads);
            LabeledSystem[] systems = { new("heat-only", heating) };
            var group = GroupFrom(systems);
            reads.Clear();
            bool result = group.CanCool;
            Assert.False(result);
            AssertPythonBooleanFacts(pythonFacts, reads, result);
            Assert.True(group.CanHeat);
            return Facts(
                "native-can-cool=false",
                "native-can-heat=true",
                "native-input=heat-only",
                "native-python-facts-bound=true");
        }

        Assert.Equal(ExpectedCases[2].CaseId, caseId);
        var mixedReads = new List<CapabilityRead>();
        SupplySystem heatingFirst = Observed(
            "COOLABLE-MIXED-HEAT",
            "heat-only",
            canHeat: true,
            canCool: false,
            mixedReads);
        SupplySystem coolingSecond = Observed(
            "COOLABLE-MIXED-COOL",
            "cool-only",
            canHeat: false,
            canCool: true,
            mixedReads);
        LabeledSystem[] mixedSystems =
        {
            new("heat-only", heatingFirst),
            new("cool-only", coolingSecond),
        };
        var mixedGroup = GroupFrom(mixedSystems);
        mixedReads.Clear();
        bool mixedResult = mixedGroup.CanCool;
        Assert.True(mixedResult);
        AssertPythonBooleanFacts(pythonFacts, mixedReads, mixedResult);
        AssertStringArray(
            pythonFacts.GetProperty("systems"),
            mixedSystems.Select(item => item.Label).ToArray());
        return Facts(
            "native-can-cool=true",
            "native-input=heat-only,cool-only",
            "native-capability-read-order=heat-only,cool-only",
            "native-python-facts-bound=true");
    }

    private static string[] ExecuteCanHeat(string caseId, JsonElement pythonFacts)
    {
        if (caseId == ExpectedCases[6].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem cooling = Observed(
                "HEATABLE-COOL",
                "cool-only",
                canHeat: false,
                canCool: true,
                reads);
            LabeledSystem[] systems = { new("cool-only", cooling) };
            var group = GroupFrom(systems);
            reads.Clear();
            bool result = group.CanHeat;
            Assert.False(result);
            AssertPythonBooleanFacts(pythonFacts, reads, result);
            Assert.True(group.CanCool);
            return Facts(
                "native-can-heat=false",
                "native-can-cool=true",
                "native-input=cool-only",
                "native-python-facts-bound=true");
        }

        if (caseId == ExpectedCases[7].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem heating = Observed(
                "HEATABLE-HEAT",
                "heat-only",
                canHeat: true,
                canCool: false,
                reads);
            LabeledSystem[] systems = { new("heat-only", heating) };
            var group = GroupFrom(systems);
            reads.Clear();
            bool result = group.CanHeat;
            Assert.True(result);
            AssertPythonBooleanFacts(pythonFacts, reads, result);
            Assert.False(group.CanCool);
            return Facts(
                "native-can-heat=true",
                "native-can-cool=false",
                "native-input=heat-only",
                "native-python-facts-bound=true");
        }

        Assert.Equal(ExpectedCases[8].CaseId, caseId);
        var mixedReads = new List<CapabilityRead>();
        SupplySystem coolingFirst = Observed(
            "HEATABLE-MIXED-COOL",
            "cool-only",
            canHeat: false,
            canCool: true,
            mixedReads);
        SupplySystem heatingSecond = Observed(
            "HEATABLE-MIXED-HEAT",
            "heat-only",
            canHeat: true,
            canCool: false,
            mixedReads);
        LabeledSystem[] mixedSystems =
        {
            new("cool-only", coolingFirst),
            new("heat-only", heatingSecond),
        };
        var mixedGroup = GroupFrom(mixedSystems);
        mixedReads.Clear();
        bool mixedResult = mixedGroup.CanHeat;
        Assert.True(mixedResult);
        AssertPythonBooleanFacts(pythonFacts, mixedReads, mixedResult);
        AssertStringArray(
            pythonFacts.GetProperty("systems"),
            mixedSystems.Select(item => item.Label).ToArray());
        return Facts(
            "native-can-heat=true",
            "native-input=cool-only,heat-only",
            "native-capability-read-order=cool-only,heat-only",
            "native-python-facts-bound=true");
    }

    private static string[] ExecuteCoolingSystems(string caseId, JsonElement pythonFacts)
    {
        if (caseId == ExpectedCases[3].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem heat = Observed(
                "COOL-PROJECTION-HEAT",
                "heat-only",
                canHeat: true,
                canCool: false,
                reads);
            SupplySystem bothFirst = Observed(
                "COOL-PROJECTION-BOTH-A",
                "both-first",
                canHeat: true,
                canCool: true,
                reads);
            SupplySystem cool = Observed(
                "COOL-PROJECTION-COOL",
                "cool-only",
                canHeat: false,
                canCool: true,
                reads);
            SupplySystem bothSecond = Observed(
                "COOL-PROJECTION-BOTH-B",
                "both-second",
                canHeat: true,
                canCool: true,
                reads);
            LabeledSystem[] systems =
            {
                new("heat-only", heat),
                new("both-first", bothFirst),
                new("cool-only", cool),
                new("both-second", bothSecond),
            };
            LabeledSystem[] expected =
            {
                systems[1],
                systems[2],
                systems[3],
            };
            var group = GroupFrom(systems);
            reads.Clear();
            IReadOnlyList<SupplySystem> result = group.CoolingSystems;
            AssertPythonProjectionFacts(
                pythonFacts,
                "input_systems",
                systems,
                result,
                expected,
                reads);
            Assert.True(pythonFacts.GetProperty("preserved_input_identity").GetBoolean());
            return Facts(
                "native-input=heat-only,both-first,cool-only,both-second",
                "native-result=both-first,cool-only,both-second",
                "native-identity=preserved",
                "native-python-facts-bound=true");
        }

        if (caseId == ExpectedCases[4].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem both = Observed(
                "COOL-PROJECTION-FRESH",
                "both",
                canHeat: true,
                canCool: true,
                reads);
            LabeledSystem[] systems = { new("both", both) };
            var group = GroupFrom(systems);
            reads.Clear();
            IReadOnlyList<SupplySystem> first = group.CoolingSystems;
            IReadOnlyList<SupplySystem> second = group.CoolingSystems;
            AssertPythonFreshProjectionFacts(
                pythonFacts,
                systems[0],
                first,
                second,
                reads);
            return Facts(
                "native-first-result=both",
                "native-second-result=both",
                "native-system-identity=preserved",
                "native-python-facts-bound=true");
        }

        Assert.Equal(ExpectedCases[5].CaseId, caseId);
        var emptyReads = new List<CapabilityRead>();
        SupplySystem heatFirst = Observed(
            "COOL-PROJECTION-EMPTY-A",
            "heat-first",
            canHeat: true,
            canCool: false,
            emptyReads);
        SupplySystem heatSecond = Observed(
            "COOL-PROJECTION-EMPTY-B",
            "heat-second",
            canHeat: true,
            canCool: false,
            emptyReads);
        LabeledSystem[] heatingSystems =
        {
            new("heat-first", heatFirst),
            new("heat-second", heatSecond),
        };
        var emptyGroup = GroupFrom(heatingSystems);
        emptyReads.Clear();
        IReadOnlyList<SupplySystem> emptyResult = emptyGroup.CoolingSystems;
        AssertPythonProjectionFacts(
            pythonFacts,
            "systems",
            heatingSystems,
            emptyResult,
            Array.Empty<LabeledSystem>(),
            emptyReads);
        return Facts(
            "native-input=heat-first,heat-second",
            "native-result=empty",
            "native-excluded-noncooling=true",
            "native-python-facts-bound=true");
    }

    private static string[] ExecuteHeatingSystems(string caseId, JsonElement pythonFacts)
    {
        if (caseId == ExpectedCases[9].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem coolFirst = Observed(
                "HEAT-PROJECTION-EMPTY-A",
                "cool-first",
                canHeat: false,
                canCool: true,
                reads);
            SupplySystem coolSecond = Observed(
                "HEAT-PROJECTION-EMPTY-B",
                "cool-second",
                canHeat: false,
                canCool: true,
                reads);
            LabeledSystem[] coolingSystems =
            {
                new("cool-first", coolFirst),
                new("cool-second", coolSecond),
            };
            var emptyGroup = GroupFrom(coolingSystems);
            reads.Clear();
            IReadOnlyList<SupplySystem> emptyResult = emptyGroup.HeatingSystems;
            AssertPythonProjectionFacts(
                pythonFacts,
                "systems",
                coolingSystems,
                emptyResult,
                Array.Empty<LabeledSystem>(),
                reads);
            return Facts(
                "native-input=cool-first,cool-second",
                "native-result=empty",
                "native-excluded-nonheating=true",
                "native-python-facts-bound=true");
        }

        if (caseId == ExpectedCases[10].CaseId)
        {
            var reads = new List<CapabilityRead>();
            SupplySystem cool = Observed(
                "HEAT-PROJECTION-COOL",
                "cool-only",
                canHeat: false,
                canCool: true,
                reads);
            SupplySystem bothFirst = Observed(
                "HEAT-PROJECTION-BOTH-A",
                "both-first",
                canHeat: true,
                canCool: true,
                reads);
            SupplySystem heat = Observed(
                "HEAT-PROJECTION-HEAT",
                "heat-only",
                canHeat: true,
                canCool: false,
                reads);
            SupplySystem bothSecond = Observed(
                "HEAT-PROJECTION-BOTH-B",
                "both-second",
                canHeat: true,
                canCool: true,
                reads);
            LabeledSystem[] systems =
            {
                new("cool-only", cool),
                new("both-first", bothFirst),
                new("heat-only", heat),
                new("both-second", bothSecond),
            };
            LabeledSystem[] expected =
            {
                systems[1],
                systems[2],
                systems[3],
            };
            var group = GroupFrom(systems);
            reads.Clear();
            IReadOnlyList<SupplySystem> result = group.HeatingSystems;
            AssertPythonProjectionFacts(
                pythonFacts,
                "input_systems",
                systems,
                result,
                expected,
                reads);
            Assert.True(pythonFacts.GetProperty("preserved_input_identity").GetBoolean());
            return Facts(
                "native-input=cool-only,both-first,heat-only,both-second",
                "native-result=both-first,heat-only,both-second",
                "native-identity=preserved",
                "native-python-facts-bound=true");
        }

        Assert.Equal(ExpectedCases[11].CaseId, caseId);
        var freshReads = new List<CapabilityRead>();
        SupplySystem both = Observed(
            "HEAT-PROJECTION-FRESH",
            "both",
            canHeat: true,
            canCool: true,
            freshReads);
        LabeledSystem[] freshSystems = { new("both", both) };
        var freshGroup = GroupFrom(freshSystems);
        freshReads.Clear();
        IReadOnlyList<SupplySystem> first = freshGroup.HeatingSystems;
        IReadOnlyList<SupplySystem> second = freshGroup.HeatingSystems;
        AssertPythonFreshProjectionFacts(
            pythonFacts,
            freshSystems[0],
            first,
            second,
            freshReads);
        return Facts(
            "native-first-result=both",
            "native-second-result=both",
            "native-system-identity=preserved",
            "native-python-facts-bound=true");
    }

    private static string[] ExecuteInit(string caseId, JsonElement pythonFacts)
    {
        if (caseId == ExpectedCases[12].CaseId)
        {
            SupplySystem heat = HeatingOnly("INIT-DEFAULT-HEAT", "heat-only");
            SupplySystem both = Both("INIT-DEFAULT-BOTH", "both");
            SupplySystem cool = CoolingOnly("INIT-DEFAULT-COOL", "cool-only");
            var inputs = new List<SupplySystem> { heat, both, cool };
            var group = new SupplyGroup(inputs);
            inputs.Reverse();
            AssertStringArray(
                pythonFacts.GetProperty("parameter_order"),
                "systems",
                "availabilities");
            Assert.Equal(
                "KEYWORD_ONLY",
                RequiredString(pythonFacts, "availability_parameter_kind"));
            Assert.True(pythonFacts.GetProperty("availability_default_is_none").GetBoolean());
            AssertStringArray(
                pythonFacts.GetProperty("stored_systems"),
                "heat-only",
                "both",
                "cool-only");
            AssertStringArray(
                pythonFacts.GetProperty("input_systems_after_mutation"),
                "cool-only",
                "both",
                "heat-only");
            Assert.True(pythonFacts.GetProperty("stored_objects_are_inputs").GetBoolean());
            Assert.True(pythonFacts.GetProperty("snapshot_isolated").GetBoolean());
            Assert.Equal("tuple", RequiredString(pythonFacts, "stored_systems_type"));
            Assert.Equal("tuple", RequiredString(pythonFacts, "stored_availabilities_type"));
            AssertNullArray(pythonFacts.GetProperty("stored_availabilities"), 3);
            AssertIdentity(inputs, cool, both, heat);
            AssertIdentity(group.Systems, heat, both, cool);
            Assert.Equal(new Schedule?[] { null, null, null }, group.Availabilities);
            AssertReadOnly(group.Systems);
            AssertReadOnly(group.Availabilities);
            return Facts(
                "native-systems=heat-only,both,cool-only",
                "native-availabilities=null,null,null",
                "native-snapshot-isolated=true",
                "native-python-facts-bound=true");
        }

        if (caseId == ExpectedCases[13].CaseId)
        {
            SupplySystem both = Both("INIT-EXPLICIT-BOTH", "both");
            Assert.True(pythonFacts.GetProperty("duplicate_same_object_accepted").GetBoolean());
            Assert.True(pythonFacts.GetProperty("non_schedule_availabilities_accepted").GetBoolean());
            Assert.True(
                pythonFacts.GetProperty("explicit_availabilities_snapshot_isolated").GetBoolean());
            AssertStringArray(
                pythonFacts.GetProperty("stored_systems"),
                "both",
                "both",
                "heat-only");
            Assert.Equal("tuple", RequiredString(pythonFacts, "stored_systems_type"));
            Assert.Equal("tuple", RequiredString(pythonFacts, "stored_availabilities_type"));
            AssertNullableStringArray(
                pythonFacts.GetProperty("stored_availabilities"),
                "availability-a",
                null,
                "availability-b");
            Assert.Throws<ArgumentException>(
                () => new SupplyGroup(new[] { both, both }));
            Schedule temperature = Schedule.Constant(
                "Not an availability",
                20,
                ScheduleType.Temperature);
            Assert.Throws<ArgumentException>(
                () => new SupplyGroup(new[] { both }, new Schedule?[] { temperature }));

            SupplySystem bothSecond = Both("INIT-EXPLICIT-BOTH-B", "both-second");
            SupplySystem heat = HeatingOnly("INIT-EXPLICIT-HEAT", "heat-only");
            Schedule availabilityA = Schedule.Constant("availability-a", 1, ScheduleType.OnOff);
            Schedule availabilityB = Schedule.Constant("availability-b", 0, ScheduleType.OnOff);
            var systems = new List<SupplySystem> { both, bothSecond, heat };
            var availabilities = new List<Schedule?> { availabilityA, null, availabilityB };
            var group = new SupplyGroup(systems, availabilities);
            systems.Clear();
            availabilities.Clear();
            AssertIdentity(group.Systems, both, bothSecond, heat);
            Assert.Same(availabilityA, group.Availabilities[0]);
            Assert.Null(group.Availabilities[1]);
            Assert.Same(availabilityB, group.Availabilities[2]);
            return Facts(
                "native-duplicate-id=rejected",
                "native-non-onoff-availability=rejected",
                "native-explicit-availabilities=availability-a,null,availability-b",
                "native-python-exception-facts-bound=true");
        }

        Assert.Equal(ExpectedCases[14].CaseId, caseId);
        AssertStringArray(
            pythonFacts.GetProperty("validation_order"),
            "empty",
            "type-before-count",
            "count-before-capability",
            "incapable");
        AssertPythonValidationAttempt(
            pythonFacts,
            0,
            "empty",
            "ValueError",
            "SupplyGroup requires at least one system.",
            systemCount: 0,
            allSystemsAreSupplySystem: true,
            availabilityCountMatches: null,
            allSystemsCapable: null);
        AssertPythonValidationAttempt(
            pythonFacts,
            1,
            "type-before-count",
            "TypeError",
            "All systems must be SupplySystem instances.",
            systemCount: 1,
            allSystemsAreSupplySystem: false,
            availabilityCountMatches: false,
            allSystemsCapable: null);
        AssertPythonValidationAttempt(
            pythonFacts,
            2,
            "count-before-capability",
            "ValueError",
            "The number of availabilities must match the number of systems.",
            systemCount: 1,
            allSystemsAreSupplySystem: true,
            availabilityCountMatches: false,
            allSystemsCapable: false);
        AssertPythonValidationAttempt(
            pythonFacts,
            3,
            "incapable",
            "ValueError",
            "Every supply system must support heating or cooling.",
            systemCount: 1,
            allSystemsAreSupplySystem: true,
            availabilityCountMatches: true,
            allSystemsCapable: false);
        ArgumentException empty = Assert.Throws<ArgumentException>(
            () => new SupplyGroup(Array.Empty<SupplySystem>()));
        Assert.Contains("requires at least one system", empty.Message, StringComparison.Ordinal);

        SupplySystem valid = HeatingOnly("INIT-ORDER-VALID", "valid");
        ArgumentException nullItem = Assert.Throws<ArgumentException>(
            () => new SupplyGroup(new SupplySystem[] { valid, null! }, Array.Empty<Schedule?>()));
        Assert.Contains("cannot contain null", nullItem.Message, StringComparison.Ordinal);

        var incapable = Capability("INIT-ORDER-INCAPABLE", "incapable", canHeat: false, canCool: false);
        ArgumentException capability = Assert.Throws<ArgumentException>(
            () => new SupplyGroup(new SupplySystem[] { incapable }, Array.Empty<Schedule?>()));
        Assert.Contains("must support heating or cooling", capability.Message, StringComparison.Ordinal);

        ArgumentException count = Assert.Throws<ArgumentException>(
            () => new SupplyGroup(new[] { valid }, Array.Empty<Schedule?>()));
        Assert.Contains("count must match", count.Message, StringComparison.Ordinal);
        return Facts(
            "native-empty=ArgumentException",
            "native-null-item-before-count=ArgumentException",
            "native-capability-before-count=adapted-ArgumentException",
            "native-python-precedence-facts-bound=true");
    }

    private static string[] ExecuteSources(string caseId, JsonElement pythonFacts)
    {
        if (caseId == ExpectedCases[15].CaseId)
        {
            Assert.True(pythonFacts.GetProperty("distinct_source_identity").GetBoolean());
            Assert.True(pythonFacts.GetProperty("equal_by_value").GetBoolean());
            Assert.Equal("tuple", RequiredString(pythonFacts, "result_type"));
            AssertStringArray(
                pythonFacts.GetProperty("result_sources"),
                "source-a",
                "source-b");
            AssertPythonSourceReads(
                pythonFacts,
                new SourceRead("first", "source-a"),
                new SourceRead("second", "source-b"));
            var sourceA = new HeatPump(
                new EntityId("SOURCE-EQUAL"),
                "source-a",
                Fuel.Electricity,
                3,
                3);
            var sourceB = new HeatPump(
                new EntityId("SOURCE-EQUAL"),
                "source-b",
                Fuel.Electricity,
                3,
                3);
            Assert.NotSame(sourceA, sourceB);
            var first = new AirHandlingUnit(new EntityId("SOURCE-EQUAL-FIRST"), "first", sourceA);
            var second = new AirHandlingUnit(new EntityId("SOURCE-EQUAL-SECOND"), "second", sourceB);
            var group = new SupplyGroup(new SupplySystem[] { first, second });
            IReadOnlyList<SourceSystem> sources = group.Sources;
            AssertIdentity(sources, sourceA);
            return Facts(
                "native-source-input=distinct-references",
                "native-logical-entity-id=shared",
                "native-result=source-a",
                "native-python-exception-facts-bound=true");
        }

        if (caseId == ExpectedCases[16].CaseId)
        {
            Assert.True(pythonFacts.GetProperty("distinct_entity_keys").GetBoolean());
            Assert.True(pythonFacts.GetProperty("distinct_source_identity").GetBoolean());
            Assert.True(pythonFacts.GetProperty("first_seen_order_preserved").GetBoolean());
            Assert.True(pythonFacts.GetProperty("reverse_logical_label_order").GetBoolean());
            Assert.Equal("tuple", RequiredString(pythonFacts, "result_type"));
            AssertPythonSourceReads(
                pythonFacts,
                new SourceRead("first", "source-z"),
                new SourceRead("second", "source-a"),
                new SourceRead("first", "source-z"),
                new SourceRead("second", "source-a"));
            var sourceZ = Source("entity-z", "source-z");
            var sourceA = Source("entity-a", "source-a");
            Assert.NotSame(sourceZ, sourceA);
            Assert.NotEqual(sourceZ.Id, sourceA.Id);
            SupplySystem firstSystem = new AirHandlingUnit(
                new EntityId("SOURCE-FIRST-Z"),
                "first",
                sourceZ);
            SupplySystem secondSystem = new AirHandlingUnit(
                new EntityId("SOURCE-SECOND-A"),
                "second",
                sourceA);
            AssertPythonSourceInputs(
                pythonFacts,
                new NativeSourceInput(firstSystem, sourceZ),
                new NativeSourceInput(secondSystem, sourceA));
            var group = new SupplyGroup(new[] { firstSystem, secondSystem });
            AssertIdentity(group.Systems, firstSystem, secondSystem);
            IReadOnlyList<SourceSystem> first = group.Sources;
            IReadOnlyList<SourceSystem> second = group.Sources;
            Assert.NotSame(first, second);
            Assert.Equal(
                !ReferenceEquals(first, second),
                pythonFacts.GetProperty("fresh_result_tuple").GetBoolean());
            AssertIdentity(first, sourceZ, sourceA);
            AssertIdentity(second, sourceZ, sourceA);
            AssertStringArray(
                pythonFacts.GetProperty("first_result_sources"),
                first.Select(item => item.Name).ToArray());
            AssertStringArray(
                pythonFacts.GetProperty("second_result_sources"),
                second.Select(item => item.Name).ToArray());
            AssertReadOnly(first);
            AssertReadOnly(second);
            return Facts(
                "native-source-entity-ids=entity-z,entity-a",
                "native-first-result=source-z,source-a",
                "native-second-result=source-z,source-a",
                "native-python-facts-bound=true");
        }

        Assert.Equal(ExpectedCases[17].CaseId, caseId);
        Assert.True(
            pythonFacts.GetProperty("first_seen_identity_deduplication").GetBoolean());
        Assert.True(pythonFacts.GetProperty("none_skipped").GetBoolean());
        Assert.Equal("tuple", RequiredString(pythonFacts, "result_type"));
        AssertPythonSourceReads(
            pythonFacts,
            new SourceRead("first", "source-a"),
            new SourceRead("second", "source-a"),
            new SourceRead("third", null),
            new SourceRead("fourth", "source-b"),
            new SourceRead("fifth", "source-a"),
            new SourceRead("first", "source-a"),
            new SourceRead("second", "source-a"),
            new SourceRead("third", null),
            new SourceRead("fourth", "source-b"),
            new SourceRead("fifth", "source-a"));
        var shared = Source("SOURCE-IDENTITY-A", "source-a");
        var other = Source("SOURCE-IDENTITY-B", "source-b");
        var identityGroup = new SupplyGroup(new SupplySystem[]
        {
            new AirHandlingUnit(new EntityId("SOURCE-IDENTITY-FIRST"), "first", shared),
            new AirHandlingUnit(new EntityId("SOURCE-IDENTITY-SECOND"), "second", shared),
            HeatingOnly("SOURCE-IDENTITY-FREE", "third"),
            new AirHandlingUnit(new EntityId("SOURCE-IDENTITY-FOURTH"), "fourth", other),
            new AirHandlingUnit(new EntityId("SOURCE-IDENTITY-FIFTH"), "fifth", shared),
        });
        IReadOnlyList<SourceSystem> firstSources = identityGroup.Sources;
        IReadOnlyList<SourceSystem> secondSources = identityGroup.Sources;
        Assert.NotSame(firstSources, secondSources);
        Assert.Equal(
            !ReferenceEquals(firstSources, secondSources),
            pythonFacts.GetProperty("fresh_result_tuple").GetBoolean());
        AssertIdentity(firstSources, shared, other);
        AssertIdentity(secondSources, shared, other);
        AssertStringArray(
            pythonFacts.GetProperty("result_sources"),
            firstSources.Select(item => item.Name).ToArray());
        AssertReadOnly(firstSources);
        return Facts(
            "native-result=source-a,source-b",
            "native-shared-reference-dedup=true",
            "native-null-source-skipped=true",
            "native-python-exception-facts-bound=true");
    }

    private static SupplyGroup GroupFrom(IReadOnlyList<LabeledSystem> systems)
    {
        SupplySystem[] nativeSystems = systems.Select(item => item.System).ToArray();
        var group = new SupplyGroup(nativeSystems);
        AssertIdentity(group.Systems, nativeSystems);
        return group;
    }

    private static void AssertPythonBooleanFacts(
        JsonElement pythonFacts,
        IReadOnlyList<CapabilityRead> nativeReads,
        bool nativeResult)
    {
        Assert.Equal("bool", RequiredString(pythonFacts, "result_type"));
        Assert.Equal(nativeResult, pythonFacts.GetProperty("result").GetBoolean());
        AssertPythonCapabilityReads(pythonFacts, nativeReads);
    }

    private static void AssertPythonProjectionFacts(
        JsonElement pythonFacts,
        string inputProperty,
        IReadOnlyList<LabeledSystem> systems,
        IReadOnlyList<SupplySystem> nativeResult,
        IReadOnlyList<LabeledSystem> expectedResult,
        IReadOnlyList<CapabilityRead> nativeReads)
    {
        Assert.Equal("tuple", RequiredString(pythonFacts, "result_type"));
        AssertStringArray(
            pythonFacts.GetProperty(inputProperty),
            systems.Select(item => item.Label).ToArray());
        AssertPythonCapabilityReads(pythonFacts, nativeReads);
        AssertStringArray(
            pythonFacts.GetProperty("result_systems"),
            expectedResult.Select(item => item.Label).ToArray());
        AssertIdentity(
            nativeResult,
            expectedResult.Select(item => item.System).ToArray());
        AssertReadOnly(nativeResult);
    }

    private static void AssertPythonFreshProjectionFacts(
        JsonElement pythonFacts,
        LabeledSystem system,
        IReadOnlyList<SupplySystem> nativeFirst,
        IReadOnlyList<SupplySystem> nativeSecond,
        IReadOnlyList<CapabilityRead> nativeReads)
    {
        Assert.Equal("tuple", RequiredString(pythonFacts, "result_type"));
        AssertStringArray(pythonFacts.GetProperty("first_result"), system.Label);
        AssertStringArray(pythonFacts.GetProperty("second_result"), system.Label);
        AssertPythonCapabilityReads(pythonFacts, nativeReads);
        Assert.Equal(
            ReferenceEquals(nativeFirst, nativeSecond),
            pythonFacts.GetProperty("same_result_object").GetBoolean());
        bool sameSystemIdentity =
            nativeFirst.Count == 1 &&
            nativeSecond.Count == 1 &&
            ReferenceEquals(nativeFirst[0], system.System) &&
            ReferenceEquals(nativeSecond[0], system.System);
        Assert.Equal(
            sameSystemIdentity,
            pythonFacts.GetProperty("same_system_identity").GetBoolean());
        AssertIdentity(nativeFirst, system.System);
        AssertIdentity(nativeSecond, system.System);
        AssertReadOnly(nativeFirst);
        AssertReadOnly(nativeSecond);
    }

    private static void AssertPythonCapabilityReads(
        JsonElement pythonFacts,
        IReadOnlyList<CapabilityRead> nativeReads)
    {
        JsonElement[] reads = pythonFacts.GetProperty("capability_reads").EnumerateArray().ToArray();
        Assert.Equal(nativeReads.Count, reads.Length);
        for (int index = 0; index < reads.Length; index++)
        {
            JsonElement read = reads[index];
            CapabilityRead nativeRead = nativeReads[index];
            AssertKeys(read, "capability", "system", "value");
            Assert.Equal(nativeRead.Capability, RequiredString(read, "capability"));
            Assert.Equal(nativeRead.System, RequiredString(read, "system"));
            Assert.Equal(nativeRead.Value, read.GetProperty("value").GetBoolean());
        }
    }

    private static void AssertPythonValidationAttempt(
        JsonElement pythonFacts,
        int index,
        string label,
        string type,
        string message,
        int systemCount,
        bool allSystemsAreSupplySystem,
        bool? availabilityCountMatches,
        bool? allSystemsCapable)
    {
        JsonElement[] attempts = pythonFacts.GetProperty("attempts").EnumerateArray().ToArray();
        Assert.Equal(4, attempts.Length);
        JsonElement attempt = attempts[index];
        AssertKeys(
            attempt,
            "all_systems_are_supply_system",
            "all_systems_capable",
            "args",
            "availability_count_matches",
            "label",
            "message",
            "outcome",
            "system_count",
            "type");
        Assert.Equal(label, RequiredString(attempt, "label"));
        Assert.Equal(type, RequiredString(attempt, "type"));
        Assert.Equal("raised", RequiredString(attempt, "outcome"));
        Assert.Equal(message, RequiredString(attempt, "message"));
        AssertStringArray(attempt.GetProperty("args"), message);
        Assert.Equal(systemCount, attempt.GetProperty("system_count").GetInt32());
        Assert.Equal(
            allSystemsAreSupplySystem,
            attempt.GetProperty("all_systems_are_supply_system").GetBoolean());
        AssertNullableBoolean(
            attempt.GetProperty("availability_count_matches"),
            availabilityCountMatches);
        AssertNullableBoolean(
            attempt.GetProperty("all_systems_capable"),
            allSystemsCapable);
    }

    private static void AssertPythonSourceReads(
        JsonElement pythonFacts,
        params SourceRead[] expected)
    {
        JsonElement[] reads = pythonFacts.GetProperty("source_reads").EnumerateArray().ToArray();
        Assert.Equal(expected.Length, reads.Length);
        for (int index = 0; index < reads.Length; index++)
        {
            JsonElement read = reads[index];
            AssertKeys(read, "source", "system");
            Assert.Equal(expected[index].System, RequiredString(read, "system"));
            AssertNullableString(read.GetProperty("source"), expected[index].Source);
        }
    }

    private static void AssertPythonSourceInputs(
        JsonElement pythonFacts,
        params NativeSourceInput[] nativeInputs)
    {
        JsonElement[] inputs = pythonFacts.GetProperty("input_sources").EnumerateArray().ToArray();
        Assert.Equal(nativeInputs.Length, inputs.Length);
        for (int index = 0; index < inputs.Length; index++)
        {
            JsonElement input = inputs[index];
            NativeSourceInput native = nativeInputs[index];
            AssertKeys(input, "entity_key", "label", "system");
            Assert.Equal(native.System.Name, RequiredString(input, "system"));
            Assert.Equal(native.Source.Name, RequiredString(input, "label"));
            Assert.Equal(native.Source.Id.Value, RequiredString(input, "entity_key"));
        }
    }

    private static void AssertNullableBoolean(JsonElement value, bool? expected)
    {
        if (expected is null)
        {
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }
        else
        {
            Assert.Equal(expected.Value, value.GetBoolean());
        }
    }

    private static void AssertNullArray(JsonElement value, int expectedCount)
    {
        JsonElement[] items = value.EnumerateArray().ToArray();
        Assert.Equal(expectedCount, items.Length);
        Assert.All(items, item => Assert.Equal(JsonValueKind.Null, item.ValueKind));
    }

    private static void AssertNullableStringArray(
        JsonElement value,
        params string?[] expected)
    {
        JsonElement[] items = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Length, items.Length);
        for (int index = 0; index < items.Length; index++)
        {
            AssertNullableString(items[index], expected[index]);
        }
    }

    private static void AssertIdentity<T>(IReadOnlyList<T> actual, params T[] expected)
        where T : class
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Same(expected[index], actual[index]);
        }
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> value)
    {
        IList<T> list = Assert.IsAssignableFrom<IList<T>>(value);
        Assert.True(list.IsReadOnly);
        if (value.Count > 0)
        {
            Assert.Throws<NotSupportedException>(() => list.Add(value[0]));
        }
    }

    private static string[] Facts(string first, string second, string third, string fourth) =>
        new[] { first, second, third, fourth };

    private static ElectricRadiator HeatingOnly(string id, string name) =>
        new(new EntityId(id), name);

    private static PackagedAirConditioner CoolingOnly(string id, string name) =>
        new(new EntityId(id), name, Source(id + "-SOURCE", name + " source"));

    private static AirHandlingUnit Both(string id, string name) =>
        new(new EntityId(id), name, Source(id + "-SOURCE", name + " source"));

    private static ObservedAirSupply Observed(
        string id,
        string label,
        bool canHeat,
        bool canCool,
        ICollection<CapabilityRead> reads) =>
        new(
            new EntityId(id),
            label,
            Source(id + "-SOURCE", label + " source"),
            canHeat,
            canCool,
            reads);

    private static CapabilityAirSupply Capability(
        string id,
        string name,
        bool canHeat,
        bool canCool) =>
        new(
            new EntityId(id),
            name,
            Source(id + "-SOURCE", name + " source"),
            canHeat,
            canCool);

    private static HeatPump Source(string id, string name) =>
        new(new EntityId(id), name, Fuel.Electricity, 3, 3);

    private static void ValidateReceipt(
        JsonElement receipt,
        EvidenceBinding evidence,
        SymbolContract symbol,
        IReadOnlyList<NativeObservation> expectedObservations)
    {
        AssertKeys(receipt, "fixture", "native_binding", "observations", "upstream_path", "upstream_symbol");
        Assert.Equal(evidence.Path, RequiredString(receipt, "upstream_path"));
        Assert.Equal(evidence.Symbol, RequiredString(receipt, "upstream_symbol"));

        JsonElement fixture = receipt.GetProperty("fixture");
        AssertKeys(fixture, "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(ExpectedCaseCount, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));

        JsonElement binding = receipt.GetProperty("native_binding");
        AssertKeys(
            binding,
            "adaptation_id",
            "implementation_path",
            "implementation_sha256",
            "implementation_symbol",
            "public_target");
        AssertNullableString(binding.GetProperty("adaptation_id"), symbol.AdaptationId);
        Assert.Equal(ImplementationRepositoryPath, RequiredString(binding, "implementation_path"));
        Assert.Equal(ImplementationSha256, RequiredString(binding, "implementation_sha256"));
        Assert.Equal(symbol.ImplementationSymbol, RequiredString(binding, "implementation_symbol"));
        Assert.Equal(symbol.NativeTarget, RequiredString(binding, "public_target"));

        JsonElement[] observations = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(3, observations.Length);
        Assert.Equal(
            observations.Select(item => RequiredString(item, "case_id")).OrderBy(item => item, StringComparer.Ordinal),
            observations.Select(item => RequiredString(item, "case_id")));
        for (int index = 0; index < observations.Length; index++)
        {
            JsonElement observation = observations[index];
            NativeObservation expected = expectedObservations[index];
            AssertKeys(observation, "adaptation_id", "case_id", "native_facts", "native_outcome");
            AssertNullableString(observation.GetProperty("adaptation_id"), expected.AdaptationId);
            Assert.Equal(expected.CaseId, RequiredString(observation, "case_id"));
            Assert.Equal("returned", RequiredString(observation, "native_outcome"));
            AssertStringArray(
                observation.GetProperty("native_facts"),
                expected.NativeFacts.ToArray());
        }

        AssertReceiptPayloadSafe(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
    }

    private static void AssertNullableString(JsonElement value, string? expected)
    {
        if (expected is null)
        {
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }
        else
        {
            Assert.Equal(JsonValueKind.String, value.ValueKind);
            Assert.Equal(expected, value.GetString());
        }
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private static string CanonicalSha256(JsonElement value)
    {
        var builder = new StringBuilder();
        WriteCanonicalJson(builder, value);
        return Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static void WriteCanonicalJson(StringBuilder builder, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }

                    firstProperty = false;
                    AppendPythonJsonString(builder, property.Name);
                    builder.Append(':');
                    WriteCanonicalJson(builder, property.Value);
                }

                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    WriteCanonicalJson(builder, item);
                }

                builder.Append(']');
                break;
            case JsonValueKind.String:
                AppendPythonJsonString(builder, value.GetString()!);
                break;
            case JsonValueKind.Number:
                builder.Append(value.GetRawText());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new Xunit.Sdk.XunitException("Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
    }

    private static void AppendPythonJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
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
                    "active_load" or
                    "claims_active_load" or
                    "classification" or
                    "environment" or
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
        Assert.False(Regex.IsMatch(
            value,
            @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])",
            RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(
            value,
            @"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d",
            RegexOptions.CultureInvariant));
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            Assert.False(Regex.IsMatch(
                text,
                @"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))",
                RegexOptions.CultureInvariant));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoHostPaths(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoHostPaths(item);
            }
        }
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

        throw new FileNotFoundException("Could not locate repository file '" + relativePath + "'.");
    }

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class CapabilityAirSupply : AirHandlingUnit
    {
        private readonly bool _canHeat;
        private readonly bool _canCool;

        internal CapabilityAirSupply(
            EntityId id,
            string name,
            HeatPump source,
            bool canHeat,
            bool canCool)
            : base(id, name, source)
        {
            _canHeat = canHeat;
            _canCool = canCool;
        }

        public override bool CanHeat => _canHeat;

        public override bool CanCool => _canCool;

    }

    private sealed class ObservedAirSupply : AirHandlingUnit
    {
        private readonly bool _canHeat;
        private readonly bool _canCool;
        private readonly string _label;
        private readonly ICollection<CapabilityRead> _reads;

        internal ObservedAirSupply(
            EntityId id,
            string label,
            HeatPump source,
            bool canHeat,
            bool canCool,
            ICollection<CapabilityRead> reads)
            : base(id, label, source)
        {
            _label = label;
            _canHeat = canHeat;
            _canCool = canCool;
            _reads = reads;
        }

        public override bool CanHeat
        {
            get
            {
                _reads.Add(new CapabilityRead("heatable", _label, _canHeat));
                return _canHeat;
            }
        }

        public override bool CanCool
        {
            get
            {
                _reads.Add(new CapabilityRead("coolable", _label, _canCool));
                return _canCool;
            }
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
        string Classification,
        string? AdaptationId,
        string NativeTarget,
        string ImplementationSymbol);

    private sealed record CaseBinding(
        string CaseId,
        string Executor,
        string Symbol,
        string FactsSha256);

    private sealed record SourceBinding(
        string Module,
        string Path,
        string SourceSha256,
        string AstSha256);

    private sealed record LabeledSystem(string Label, SupplySystem System);

    private sealed record SourceRead(string System, string? Source);

    private sealed record NativeSourceInput(SupplySystem System, SourceSystem Source);

    private sealed record CapabilityRead(string Capability, string System, bool Value);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string? AdaptationId,
        IReadOnlyList<string> NativeFacts);
}
