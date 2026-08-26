using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.UpstreamTracker;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class ConstructionFamilyIdfOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-construction-to-idf-object-oracle.json";
    private const string OracleSchema =
        "goniegonie.python-reference.dragon-construction-to-idf-object.v1";
    private const int OracleByteLength = 63_256;
    private const string OracleSha256 =
        "sha256:415cda6877c71985f8943ddd328bc04500e9905f7561c908285bc1bfe95e6b91";
    private const string CasesSha256 =
        "sha256:c99cd6cf0fabfa45e599866d7acb8be22d9c7d7d6d6ab13b8732ad811291cbf5";
    private const string GeneratorRepositoryPath =
        "tools/python-reference/generate_dragon_construction_to_idf_object_oracle.py";
    private const int GeneratorByteLength = 46_536;
    private const string GeneratorSha256 =
        "sha256:3d149181d1df5faaa38c50209cc9a5f18b9bebc15f817fed7a2f7293b83d8ca4";
    private const string PythonValidatorRepositoryPath =
        "tests/PythonReference/test_dragon_construction_to_idf_object_oracle.py";
    private const int PythonValidatorByteLength = 22_845;
    private const string PythonValidatorSha256 =
        "sha256:b56a1e16fed85f62d4fcab88857ecaf800fcabcc94e34e0b2d28309d3c439a44";

    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamPath = "src/idragon/dragon/construction.py";
    private const string UpstreamSourceSha256 =
        "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622";
    private const string UpstreamAstSha256 =
        "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a";

    private const string PublicRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs";
    private const int PublicByteLength = 22_015;
    private const string PublicSha256 =
        "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3";
    private const string PublicSymbol =
        "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument";
    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs";
    private const int ImplementationByteLength = 50_281;
    private const string ImplementationSha256 =
        "sha256:f4a5eab3c337fe8eeb12aeff0ffe0490c7d7cd5c2d89be16f88da4455167e2b3";
    private const string AppendSurfaceConstructionSymbol =
        "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendSurfaceConstruction";
    private const string AppendGlazingSymbol =
        "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendGlazing";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Model.ConstructionFamilyIdfOracleParityTests.MatchesPinnedPythonConstructionFamilyEmissionThroughFreshEnergyModels";

    private const string IddOracleRepositoryPath =
        "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz";
    private const int IddOracleByteLength = 585_482;
    private const string IddOracleSha256 =
        "sha256:f2dfc27d39f788f945ef5cc3b79ffce2a516a568075717bd67088d900a75c705";
    private const string IddOracleSchema = "goniegonie.energyplus-idd-schema.v1";
    private const string EnergyPlusVersion = "24.2.0";
    private const string EnergyPlusBuild = "94a887817b";
    private const string EnergyPlusIddSourceSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const int EnergyPlusIddSourceByteLength = 4_556_412;
    private const string OfficialEpJsonSourceSha256 =
        "aefb16d63495d170468ecab3c935f1aeb68eb07c6551403dd11cbba61cb136fa";
    private const int OfficialEpJsonSourceByteLength = 10_469_751;

    private const string ClassificationBasis =
        "native conversion is available only through private EnergyModelIdfAssembler model context, which compacts default fields and deduplicates shared definitions; standalone mutable-list parity is not claimed";
    private const string FixtureScope =
        "bounded-common-valid-state-construction-family-idf-emission-in-model-context";

    private static readonly string[] ObjectTypes =
    {
        "Construction:AirBoundary",
        "Construction",
        "WindowMaterial:SimpleGlazingSystem",
        "Material",
        "Material:NoMass",
    };

    private static readonly SelectedIddTopology[] ExpectedIddTopologies =
    {
        new("Construction:AirBoundary", 4, 4, null, 0),
        new("Construction", 11, 0, null, 0),
        new("WindowMaterial:SimpleGlazingSystem", 4, 3, null, 0),
        new("Material", 9, 6, null, 0),
        new("Material:NoMass", 6, 3, null, 0),
    };

    private static readonly SymbolBinding[] ExpectedSymbols =
    {
        new(
            592,
            "AirBoundary.to_idf_object",
            "sha256:639a205f5c73ed6febc52735b33521b20dbeb644fcc4fd6ac2e148439c4e9545",
            "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
            "sha256:ada40fa4a3bb88a012f0e91622290700bbae9e525bb8f07ac918f39290e2d325",
            "dragon-construction-air-boundary-to-idf-object-639a205f",
            "model-context-air-boundary-idf-emission",
            "EnergyModel.ToIdfDocument via private EnergyModelIdfAssembler.AppendSurfaceConstruction",
            AppendSurfaceConstructionSymbol),
        new(
            601,
            "Construction.to_idf_object",
            "sha256:71a76f27ebadf7476c2746f1634258a52b3f16bd19e01624d9ce3809afc37309",
            "sha256:b55fe94795be2a00b3d45a008615fcf1e3efee2bfa89946d24c74488c2b8fb1c",
            "sha256:a878a51cb6bbfabee7834f446fba29d5b996ba549dd02e93607132b203f47d4c",
            "dragon-construction-construction-to-idf-object-71a76f27",
            "model-context-construction-idf-emission",
            "EnergyModel.ToIdfDocument via private EnergyModelIdfAssembler.AppendSurfaceConstruction",
            AppendSurfaceConstructionSymbol),
        new(
            608,
            "Glazing.to_idf_object",
            "sha256:3350beafdd06d7e477a86dedc271f9e4e71452dafd3137dcd8e512f94f58d093",
            "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b",
            "sha256:10b7267535d8de4d92cfa27a6718948e6819ff322d369c51cf9032aae397034b",
            "dragon-construction-glazing-to-idf-object-3350beaf",
            "model-context-glazing-idf-emission",
            "EnergyModel.ToIdfDocument via private EnergyModelIdfAssembler.AppendGlazing",
            AppendGlazingSymbol),
        new(
            617,
            "Layer.to_idf_object",
            "sha256:66e6d4589806a69db0d4023bcd6160f1e9a7079ed4aac3f3cb5f0839307fc884",
            "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
            "sha256:613dae616f8daa7794182d70a909db46722aff6a3c7f9fc68382082231959429",
            "dragon-construction-layer-to-idf-object-66e6d458",
            "model-context-layer-idf-emission",
            "EnergyModel.ToIdfDocument via private EnergyModelIdfAssembler.AppendSurfaceConstruction",
            AppendSurfaceConstructionSymbol),
        new(
            640,
            "NoMassConstruction.to_idf_object",
            "sha256:2bc3fe982f11770f5e4e23b97b52c608fc40f61d86d4d1afff00b0077626b096",
            "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b",
            "sha256:ef1eb1b1d4ae714edb40b1feb6fec0c62e89a7623bc88353eb60c1093e2bfa6a",
            "dragon-construction-no-mass-construction-to-idf-object-2bc3fe98",
            "model-context-no-mass-construction-idf-emission",
            "EnergyModel.ToIdfDocument via private EnergyModelIdfAssembler.AppendSurfaceConstruction",
            AppendSurfaceConstructionSymbol),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new(
            "dragon-construction-to-idf-object.air-boundary.alternate-ach",
            "AirBoundary.to_idf_object",
            new[] { 3 },
            1),
        new(
            "dragon-construction-to-idf-object.air-boundary.representative-ach",
            "AirBoundary.to_idf_object",
            new[] { 3 },
            1),
        new(
            "dragon-construction-to-idf-object.construction.multi-layer-surface-scope",
            "Construction.to_idf_object",
            new[] { 4 },
            7),
        new(
            "dragon-construction-to-idf-object.construction.single-layer-surface-scope",
            "Construction.to_idf_object",
            new[] { 2 },
            9),
        new(
            "dragon-construction-to-idf-object.glazing.alternate-values",
            "Glazing.to_idf_object",
            new[] { 3, 2 },
            10),
        new(
            "dragon-construction-to-idf-object.glazing.representative-values",
            "Glazing.to_idf_object",
            new[] { 3, 2 },
            10),
        new(
            "dragon-construction-to-idf-object.layer.alternate-material-values",
            "Layer.to_idf_object",
            new[] { 9 },
            0),
        new(
            "dragon-construction-to-idf-object.layer.representative-material-values",
            "Layer.to_idf_object",
            new[] { 9 },
            0),
        new(
            "dragon-construction-to-idf-object.no-mass-construction.alternate-u",
            "NoMassConstruction.to_idf_object",
            new[] { 6, 2 },
            9),
        new(
            "dragon-construction-to-idf-object.no-mass-construction.representative-u",
            "NoMassConstruction.to_idf_object",
            new[] { 6, 2 },
            9),
    };

    private static readonly string[] ContextOnlyNotTargeted =
    {
        "AirBoundary",
        "AirBoundary.__init__",
        "AirBoundary.__repr__",
        "AirBoundary.__str__",
        "Construction",
        "Construction.__eq__",
        "Construction.__hash__",
        "Construction.__init__",
        "Construction.U",
        "Construction.heat_capacity",
        "Construction.reversed",
        "Construction.thickness",
        "Glazing",
        "Glazing.__init__",
        "Glazing.__repr__",
        "Glazing.__str__",
        "Glazing.G",
        "Glazing.U",
        "Layer",
        "Layer.__eq__",
        "Layer.__hash__",
        "Layer.__init__",
        "Layer.U",
        "Layer.heat_capacity",
        "Layer.material",
        "Layer.thickness",
        "Material",
        "MaterialRoughness",
        "NoMassConstruction",
        "NoMassConstruction.__init__",
        "NoMassConstruction.__repr__",
        "NoMassConstruction.__str__",
        "NoMassConstruction.U",
    };

    private static readonly string[] UnresolvedBehavior =
    {
        "all-five-class-constructor-property-equality-hash-contracts",
        "invalid-domain-and-error-semantics",
        "IdfObject",
        "IdfObject.__init__",
        "isolated-IdfObject-validation-policy",
        "Surface",
        "Surface.to_idf_object",
        "Zone",
        "Zone.to_idf_object",
        "EnergyModel.to_idf",
        "native-model-deduplication-and-conflict-semantics",
        "native-global-object-order-and-shared-material-compaction",
    };

    private static readonly SourceBinding[] ExpectedSources =
    {
        new("idragon", "src/idragon/__init__.py", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618"),
        new("idragon.common", "src/idragon/common.py", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9"),
        new("idragon.constants", "src/idragon/constants.py", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"),
        new("idragon.dragon", "src/idragon/dragon/__init__.py", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52"),
        new("idragon.dragon.construction", "src/idragon/dragon/construction.py", UpstreamSourceSha256, UpstreamAstSha256),
        new("idragon.dragon.hvac", "src/idragon/dragon/hvac.py", "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0", "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"),
        new("idragon.dragon.model", "src/idragon/dragon/model.py", "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"),
        new("idragon.dragon.profile", "src/idragon/dragon/profile.py", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef"),
        new("idragon.dragon.shape", "src/idragon/dragon/shape.py", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"),
        new("idragon.imugi", "src/idragon/imugi.py", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"),
        new("idragon.launcher", "src/idragon/launcher.py", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e"),
        new("idragon.utils", "src/idragon/utils.py", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452"),
    };

    [Fact]
    public void MatchesPinnedPythonConstructionFamilyEmissionThroughFreshEnergyModels()
    {
        OfficialIddOracle iddOracle = LoadOfficialIddOracle();
        using JsonDocument oracle = ReadPinnedOracle();
        Scenario[] scenarios = Enumerable.Range(0, ExpectedCases.Length)
            .Select(CreateScenario)
            .ToArray();
        JsonElement[] cases = ValidateCorpus(oracle.RootElement, iddOracle, scenarios);
        ValidateArtifactsAndNativeBindings();

        NativeObservation[] observations = cases
            .Select((item, index) => ExecuteNativeCase(
                ExpectedCases[index],
                item.GetProperty("python").GetProperty("facts"),
                scenarios[index],
                iddOracle))
            .ToArray();
        Assert.Equal(ExpectedCases.Length, observations.Length);
        Assert.Equal(
            "Wall Assembly:for:South Wall",
            observations[2].NativeObjectNames.Single());
        Assert.Equal(
            "Roof Assembly:for:Roof Plane",
            observations[3].NativeObjectNames.Single());
        Assert.NotEqual(
            observations[2].NativeObjectNames.Single(),
            observations[3].NativeObjectNames.Single());

        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            NativeObservation[] symbolObservations = observations
                .Where(item => item.Symbol == symbol.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(2, symbolObservations.Length);
            object receipt = CreateReceipt(symbol, symbolObservations);
            ValidateReceipt(JsonSerializer.SerializeToElement(receipt), symbol, symbolObservations);
            TrustedEvidenceRecorder.Record(
                symbol.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(OracleRepositoryPath));
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        string text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
        return JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
    }

    private static void ValidateArtifactsAndNativeBindings()
    {
        AssertPinnedArtifact(
            GeneratorRepositoryPath,
            GeneratorByteLength,
            GeneratorSha256);
        AssertPinnedArtifact(
            PythonValidatorRepositoryPath,
            PythonValidatorByteLength,
            PythonValidatorSha256);
        AssertPinnedArtifact(
            PublicRepositoryPath,
            PublicByteLength,
            PublicSha256);
        AssertPinnedArtifact(
            ImplementationRepositoryPath,
            ImplementationByteLength,
            ImplementationSha256);

        MethodInfo publicMethod = Assert.Single(
            typeof(EnergyModel).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            candidate => candidate.Name == nameof(EnergyModel.ToIdfDocument));
        Assert.Equal(PublicSymbol, MethodSymbol(publicMethod));
        Assert.Equal(typeof(IdfDocument), publicMethod.ReturnType);
        ParameterInfo[] publicParameters = publicMethod.GetParameters();
        Assert.Equal(new[] { "schema", "options" }, publicParameters.Select(item => item.Name));
        Assert.Equal(typeof(IddSchema), publicParameters[0].ParameterType);
        Assert.Equal(typeof(EnergyModelIdfOptions), publicParameters[1].ParameterType);
        Assert.True(publicParameters.All(item => item.HasDefaultValue));

        Type? assemblerCandidate = typeof(EnergyModel).Assembly.GetType(
            "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler",
            throwOnError: true);
        Assert.NotNull(assemblerCandidate);
        Type assembler = assemblerCandidate!;
        MethodInfo appendSurfaceConstruction = Assert.Single(
            assembler.GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            candidate => candidate.Name == "AppendSurfaceConstruction");
        MethodInfo appendGlazing = Assert.Single(
            assembler.GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            candidate => candidate.Name == "AppendGlazing");
        Assert.Equal(AppendSurfaceConstructionSymbol, MethodSymbol(appendSurfaceConstruction));
        Assert.Equal(AppendGlazingSymbol, MethodSymbol(appendGlazing));
        Assert.Equal(typeof(string), appendSurfaceConstruction.ReturnType);
        Assert.Equal(typeof(string), appendGlazing.ReturnType);
        Assert.Equal(
            new[]
            {
                "document",
                "context",
                "construction",
                "surfaceName",
                "materialDefinitions",
                "constructionDefinitions",
            },
            appendSurfaceConstruction.GetParameters().Select(item => item.Name));
        Assert.Equal(
            new[]
            {
                "document",
                "context",
                "glazing",
                "materialDefinitions",
                "constructionDefinitions",
            },
            appendGlazing.GetParameters().Select(item => item.Name));
        Assert.Equal(
            typeof(ISurfaceConstruction),
            appendSurfaceConstruction.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(Glazing), appendGlazing.GetParameters()[2].ParameterType);
    }

    private static JsonElement[] ValidateCorpus(
        JsonElement root,
        OfficialIddOracle iddOracle,
        IReadOnlyList<Scenario> scenarios)
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
        AssertNoUnsafeIdentity(root);
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        ValidateUpstream(root.GetProperty("upstream"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCases.Length, cases.Length);
        Assert.Equal(scenarios.Count, cases.Length);
        string[] identifiers = cases.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), identifiers);
        Assert.Equal(identifiers.OrderBy(item => item, StringComparer.Ordinal), identifiers);
        Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index], scenarios[index], iddOracle);
        }

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
            JsonElement module = modules[index];
            AssertKeys(source, "ast_sha256", "path", "source_sha256");
            Assert.Equal(expected.Path, RequiredString(source, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(source, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(source, "ast_sha256"));
            AssertKeys(module, "ast_sha256", "module", "path", "source_sha256");
            Assert.Equal(expected.Module, RequiredString(module, "module"));
            Assert.Equal(expected.Path, RequiredString(module, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(module, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(module, "ast_sha256"));
        }

        Assert.Equal(
            ExpectedSources.Length,
            sources.Select(item => RequiredString(item, "path"))
                .Distinct(StringComparer.Ordinal).Count());
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
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["colorama"] = "0.4.6",
            ["et_xmlfile"] = "2.0.0",
            ["numpy"] = "2.3.1",
            ["openpyxl"] = "3.1.5",
            ["pandas"] = "2.3.0",
            ["python-dateutil"] = "2.9.0.post0",
            ["pytz"] = "2024.2",
            ["six"] = "1.16.0",
            ["tqdm"] = "4.67.1",
            ["tzdata"] = "2024.2",
        };
        foreach (KeyValuePair<string, string> item in expected)
        {
            Assert.Equal(item.Value, RequiredString(dependencies, item.Key));
        }
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            SymbolBinding expected = ExpectedSymbols[index];
            JsonElement symbol = symbols[index];
            AssertKeys(
                symbol,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
            Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
            Assert.Equal(expected.Symbol, RequiredString(symbol, "symbol"));
            Assert.Equal("function", RequiredString(symbol, "kind"));
            Assert.Equal(expected.SymbolHash, RequiredString(symbol, "symbol_hash"));
            Assert.Equal(expected.SignatureHash, RequiredString(symbol, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(symbol, "body_hash"));
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
            "classification_basis",
            "classifications",
            "closure",
            "identity_encoding",
            "native_targets",
            "source_import_policy",
            "target_symbols");
        Assert.Equal(ExpectedCases.Length, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(
            contract.GetProperty("case_ids"),
            ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(
            contract.GetProperty("target_symbols"),
            ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal(ClassificationBasis, RequiredString(contract, "classification_basis"));
        Assert.Equal("booleans-only-no-id-or-address", RequiredString(contract, "identity_encoding"));
        Assert.Equal(
            "external-temporary-copy-with-complete-loaded-local-module-audit",
            RequiredString(contract, "source_import_policy"));

        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement nativeTargets = contract.GetProperty("native_targets");
        string[] symbolNames = ExpectedSymbols.Select(item => item.Symbol).ToArray();
        AssertKeys(adaptations, symbolNames);
        AssertKeys(assertions, symbolNames);
        AssertKeys(classifications, symbolNames);
        AssertKeys(nativeTargets, symbolNames);
        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
            Assert.Equal(symbol.AssertionId, RequiredString(assertions, symbol.Symbol));
            Assert.Equal("exception", RequiredString(classifications, symbol.Symbol));
            Assert.Equal(symbol.NativeTarget, RequiredString(nativeTargets, symbol.Symbol));
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "context_only_not_targeted",
            "full_symbol_closure",
            "scope",
            "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(FixtureScope, RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("context_only_not_targeted"), ContextOnlyNotTargeted);
        AssertStringArray(closure.GetProperty("unresolved_behavior"), UnresolvedBehavior);
    }

    private static void ValidateCase(
        JsonElement value,
        CaseBinding expected,
        Scenario scenario,
        OfficialIddOracle iddOracle)
    {
        AssertKeys(value, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(expected.CaseId, RequiredString(value, "id"));
        Assert.Equal("construction-to-idf-object", RequiredString(value, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(value, "symbol"));
        Assert.Equal(expected, scenario.Binding);

        SymbolBinding symbol = ExpectedSymbols.Single(item => item.Symbol == expected.Symbol);
        JsonElement expectedDotnet = value.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));

        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        AssertKeys(facts, "emission", "input_context");
        ValidateInputContext(facts.GetProperty("input_context"), scenario);
        ValidateEmission(facts.GetProperty("emission"), expected, iddOracle);
    }

    private static void ValidateInputContext(JsonElement context, Scenario scenario)
    {
        AssertKeys(
            context,
            "captured_state_scope",
            "source_state",
            "source_state_unchanged_after_two_emissions");
        Assert.Equal(
            "properties-read-by-target-method",
            RequiredString(context, "captured_state_scope"));
        Assert.True(context.GetProperty("source_state_unchanged_after_two_emissions").GetBoolean());

        JsonElement[] state = context.GetProperty("source_state").EnumerateArray().ToArray();
        Assert.Equal(scenario.SourceState.Count, state.Length);
        Assert.Equal(
            scenario.SourceState.Keys,
            state.Select(item => RequiredString(item, "name")));
        foreach (JsonElement item in state)
        {
            AssertKeys(item, "name", "value");
            string name = RequiredString(item, "name");
            Assert.True(scenario.SourceState.TryGetValue(name, out object? expected));
            AssertEncodedSourceValue(item.GetProperty("value"), expected!);
        }
    }

    private static void ValidateEmission(
        JsonElement emission,
        CaseBinding expected,
        OfficialIddOracle iddOracle)
    {
        AssertKeys(
            emission,
            "all_allowed_fields_covered_in_order",
            "first_object_records",
            "first_objects_pairwise_distinct",
            "fresh_idf_object_flags",
            "fresh_result_list",
            "fresh_return_value",
            "object_count",
            "object_types",
            "result_type",
            "same_idd_definition_flags",
            "second_fields_equal_flags",
            "second_objects_pairwise_distinct");
        Assert.True(emission.GetProperty("all_allowed_fields_covered_in_order").GetBoolean());
        Assert.True(emission.GetProperty("first_objects_pairwise_distinct").GetBoolean());
        Assert.True(emission.GetProperty("fresh_return_value").GetBoolean());
        Assert.True(emission.GetProperty("second_objects_pairwise_distinct").GetBoolean());

        JsonElement[] records = emission.GetProperty("first_object_records")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(expected.NativeCompactFieldCounts.Length, records.Length);
        Assert.Equal(records.Length, emission.GetProperty("object_count").GetInt32());
        string[] objectTypes = records.Select(item => RequiredString(item, "object_type")).ToArray();
        AssertStringArray(emission.GetProperty("object_types"), objectTypes);
        AssertBooleanArray(emission.GetProperty("fresh_idf_object_flags"), records.Length, true);
        AssertBooleanArray(emission.GetProperty("same_idd_definition_flags"), records.Length, true);
        AssertBooleanArray(emission.GetProperty("second_fields_equal_flags"), records.Length, true);

        bool isLayer = expected.Symbol == "Layer.to_idf_object";
        Assert.Equal(isLayer ? "IdfObject" : "list", RequiredString(emission, "result_type"));
        JsonElement freshList = emission.GetProperty("fresh_result_list");
        if (isLayer)
        {
            Assert.Equal(JsonValueKind.Null, freshList.ValueKind);
        }
        else
        {
            Assert.True(freshList.GetBoolean());
        }

        int omitted = 0;
        for (int objectIndex = 0; objectIndex < records.Length; objectIndex++)
        {
            JsonElement record = records[objectIndex];
            AssertKeys(record, "field_count", "object_type", "ordered_fields");
            string objectType = RequiredString(record, "object_type");
            OfficialIddObject official = iddOracle[objectType];
            JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
            Assert.Equal(fields.Length, record.GetProperty("field_count").GetInt32());
            Assert.Equal(official.Fields.Length, fields.Length);
            Assert.True(expected.NativeCompactFieldCounts[objectIndex] <= fields.Length);
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                JsonElement field = fields[fieldIndex];
                AssertKeys(field, "name", "value");
                Assert.Equal(official.ResolveFieldName(fieldIndex), RequiredString(field, "name"));
                ValidateEncodedValue(field.GetProperty("value"));
                if (fieldIndex >= expected.NativeCompactFieldCounts[objectIndex])
                {
                    omitted++;
                }
            }
        }

        Assert.Equal(expected.ExpectedBlankOmissionCount, omitted);
    }

    private static void ValidateEncodedValue(JsonElement value)
    {
        string kind = RequiredString(value, "kind");
        if (kind == "none")
        {
            AssertKeys(value, "kind");
            return;
        }

        if (kind == "str")
        {
            AssertKeys(value, "kind", "value");
            Assert.Equal(JsonValueKind.String, value.GetProperty("value").ValueKind);
            return;
        }

        Assert.Equal("float", kind);
        AssertKeys(value, "hex", "kind", "repr");
        string representation = RequiredString(value, "repr");
        Assert.True(double.TryParse(
            representation,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsed));
        Assert.True(double.IsFinite(parsed));
        Assert.Matches(
            @"^-?0x[0-9a-f]+(?:\.[0-9a-f]+)?p[+-][0-9]+$",
            RequiredString(value, "hex"));
    }

    private static void AssertEncodedSourceValue(JsonElement encoded, object expected)
    {
        if (expected is string[] expectedStrings)
        {
            Assert.Equal(JsonValueKind.Array, encoded.ValueKind);
            JsonElement[] values = encoded.EnumerateArray().ToArray();
            Assert.Equal(expectedStrings.Length, values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                AssertEncodedString(values[index], expectedStrings[index]);
            }

            return;
        }

        if (expected is string expectedString)
        {
            AssertEncodedString(encoded, expectedString);
            return;
        }

        Assert.IsType<double>(expected);
        AssertEncodedDouble(encoded, (double)expected);
    }

    private static void AssertEncodedString(JsonElement encoded, string expected)
    {
        ValidateEncodedValue(encoded);
        Assert.Equal("str", RequiredString(encoded, "kind"));
        Assert.Equal(expected, RequiredString(encoded, "value"));
    }

    private static void AssertEncodedDouble(JsonElement encoded, double expected)
    {
        ValidateEncodedValue(encoded);
        Assert.Equal("float", RequiredString(encoded, "kind"));
        double actual = double.Parse(
            RequiredString(encoded, "repr"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(expected),
            BitConverter.DoubleToInt64Bits(actual));
    }

    private static Scenario CreateScenario(int index)
    {
        CaseBinding binding = ExpectedCases[index];
        return index switch
        {
            0 => CreateSurfaceScenario(
                binding,
                new AirBoundary("Transfer Air Alternate", 1.25),
                "Air Boundary Surface Alternate",
                SourceState(
                    ("name", "Transfer Air Alternate"),
                    ("ACH", 1.25))),
            1 => CreateSurfaceScenario(
                binding,
                new AirBoundary("Transfer Air Representative", 0.5),
                "Air Boundary Surface Representative",
                SourceState(
                    ("name", "Transfer Air Representative"),
                    ("ACH", 0.5))),
            2 => CreateSurfaceScenario(
                binding,
                new OpaqueConstruction(
                    "Wall Assembly",
                    new[]
                    {
                        Layer("Exterior Render 20mm", "Exterior Render", 0.02, 0.5, 1_200, 900),
                        Layer("Structural Core 180mm", "Structural Core", 0.18, 1.75, 2_300, 900),
                        Layer("Interior Finish 13mm", "Interior Finish", 0.013, 0.25, 850, 1_000),
                    }),
                "South Wall",
                SourceState(
                    ("name", "Wall Assembly"),
                    ("layer_names", new[]
                    {
                        "Exterior Render 20mm",
                        "Structural Core 180mm",
                        "Interior Finish 13mm",
                    }),
                    ("surface.name", "South Wall"))),
            3 => CreateSurfaceScenario(
                binding,
                new OpaqueConstruction(
                    "Roof Assembly",
                    new[]
                    {
                        Layer("Roof Insulation 200mm", "Roof Insulation", 0.2, 0.04, 45, 1_400),
                    }),
                "Roof Plane",
                SourceState(
                    ("name", "Roof Assembly"),
                    ("layer_names", new[] { "Roof Insulation 200mm" }),
                    ("surface.name", "Roof Plane"))),
            4 => CreateGlazingScenario(
                binding,
                new Glazing("Clear Glazing", 2.75, 0.625),
                SourceState(
                    ("name", "Clear Glazing"),
                    ("U", 2.75),
                    ("G", 0.625))),
            5 => CreateGlazingScenario(
                binding,
                new Glazing("Triple Glazing", 0.8, 0.45),
                SourceState(
                    ("name", "Triple Glazing"),
                    ("U", 0.8),
                    ("G", 0.45))),
            6 => CreateLayerScenario(
                binding,
                new Layer(
                    "Wood Fibre 80mm",
                    new Material(
                        "Wood Fibre",
                        0.125,
                        160,
                        2_100,
                        0.85,
                        0.5,
                        0.45,
                        MaterialRoughness.Smooth),
                    0.08),
                SourceState(
                    ("name", "Wood Fibre 80mm"),
                    ("material.name", "Wood Fibre"),
                    ("material.roughness", "Smooth"),
                    ("thickness", 0.08),
                    ("material.conductivity", 0.125),
                    ("material.density", 160d),
                    ("material.specific_heat", 2_100d),
                    ("material.thermal_absorptance", 0.85),
                    ("material.solar_absorptance", 0.5),
                    ("material.visible_absorptance", 0.45))),
            7 => CreateLayerScenario(
                binding,
                new Layer(
                    "Dense Concrete 180mm",
                    new Material(
                        "Dense Concrete",
                        1.75,
                        2_300,
                        900,
                        0.9,
                        0.65,
                        0.55,
                        MaterialRoughness.MediumRough),
                    0.18),
                SourceState(
                    ("name", "Dense Concrete 180mm"),
                    ("material.name", "Dense Concrete"),
                    ("material.roughness", "MediumRough"),
                    ("thickness", 0.18),
                    ("material.conductivity", 1.75),
                    ("material.density", 2_300d),
                    ("material.specific_heat", 900d),
                    ("material.thermal_absorptance", 0.9),
                    ("material.solar_absorptance", 0.65),
                    ("material.visible_absorptance", 0.55))),
            8 => CreateSurfaceScenario(
                binding,
                new NoMassConstruction("Light Partition", 2),
                "Light Partition Surface",
                SourceState(
                    ("name", "Light Partition"),
                    ("U", 2d))),
            9 => CreateSurfaceScenario(
                binding,
                new NoMassConstruction("Insulated Panel", 0.25),
                "Insulated Panel Surface",
                SourceState(
                    ("name", "Insulated Panel"),
                    ("U", 0.25))),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }

    private static Scenario CreateSurfaceScenario(
        CaseBinding binding,
        ISurfaceConstruction construction,
        string surfaceName,
        IReadOnlyDictionary<string, object> sourceState)
    {
        int index = Array.IndexOf(ExpectedCases, binding);
        Surface surface = CreateSurface(index, surfaceName, construction);
        return CreateScenario(binding, surface, sourceState);
    }

    private static Scenario CreateLayerScenario(
        CaseBinding binding,
        Layer layer,
        IReadOnlyDictionary<string, object> sourceState)
    {
        int index = Array.IndexOf(ExpectedCases, binding);
        var construction = new OpaqueConstruction(
            $"Layer Oracle Assembly {index}",
            new[] { layer });
        Surface surface = CreateSurface(index, $"Layer Oracle Surface {index}", construction);
        return CreateScenario(binding, surface, sourceState);
    }

    private static Scenario CreateGlazingScenario(
        CaseBinding binding,
        Glazing glazing,
        IReadOnlyDictionary<string, object> sourceState)
    {
        int index = Array.IndexOf(ExpectedCases, binding);
        var window = new Window(
            new EntityId($"WINDOW-CIDF-{index:D2}"),
            $"Glazing Oracle Window {index}",
            glazing,
            Square(1, x: 1, y: 1));
        Surface surface = CreateSurface(
            index,
            $"Glazing Oracle Host {index}",
            new AirBoundary($"Glazing Oracle Host Air {index}"),
            new[] { window });
        return CreateScenario(binding, surface, sourceState);
    }

    private static Scenario CreateScenario(
        CaseBinding binding,
        Surface surface,
        IReadOnlyDictionary<string, object> sourceState)
    {
        int index = Array.IndexOf(ExpectedCases, binding);
        var profile = new ZoneProfile(
            new EntityId($"PROFILE-CIDF-{index:D2}"),
            $"Construction Oracle Profile {index}");
        var zone = new Zone(
            new EntityId($"ZONE-CIDF-{index:D2}"),
            $"Construction Oracle Zone {index}",
            new[] { surface },
            profile);
        var model = new EnergyModel(
            $"Construction Oracle Model {index}",
            new[] { zone });
        return new Scenario(binding, model, zone, surface, sourceState);
    }

    private static Surface CreateSurface(
        int index,
        string name,
        ISurfaceConstruction construction,
        IEnumerable<IOpening>? openings = null) =>
        new(
            new EntityId($"SURFACE-CIDF-{index:D2}"),
            name,
            SurfaceType.Wall,
            construction,
            SurfaceBoundary.Outdoors,
            Square(4),
            openings);

    private static PlanarPolygon Square(double size, double x = 0, double y = 0) =>
        new(
            new[]
            {
                new Vertex(x, y, 0),
                new Vertex(x + size, y, 0),
                new Vertex(x + size, y + size, 0),
                new Vertex(x, y + size, 0),
            });

    private static Layer Layer(
        string layerName,
        string materialName,
        double thickness,
        double conductivity,
        double density,
        double specificHeat) =>
        new(
            layerName,
            new Material(materialName, conductivity, density, specificHeat),
            thickness);

    private static Dictionary<string, object> SourceState(
        params (string Name, object Value)[] values)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((string name, object value) in values)
        {
            result.Add(name, value);
        }

        return result;
    }

    private static NativeObservation ExecuteNativeCase(
        CaseBinding binding,
        JsonElement facts,
        Scenario scenario,
        OfficialIddOracle iddOracle)
    {
        GraphSnapshot snapshot = GraphSnapshot.Capture(scenario);
        Assert.True(scenario.Model.Validate().IsValid);

        IdfDocument firstDocument = scenario.Model.ToIdfDocument();
        snapshot.AssertUnchanged(scenario);
        IdfDocument secondDocument = scenario.Model.ToIdfDocument();
        snapshot.AssertUnchanged(scenario);
        Assert.NotSame(firstDocument, secondDocument);
        Assert.Equal(firstDocument.Count, secondDocument.Count);
        Assert.Equal(
            OrderIndependentDocumentFingerprint(firstDocument),
            OrderIndependentDocumentFingerprint(secondDocument));

        JsonElement records = facts.GetProperty("emission").GetProperty("first_object_records");
        IdfObject[] first = SelectNativeTargets(firstDocument, records);
        IdfObject[] second = SelectNativeTargets(secondDocument, records);
        Assert.Equal(binding.NativeCompactFieldCounts, first.Select(item => item.Count));
        Assert.Equal(binding.NativeCompactFieldCounts, second.Select(item => item.Count));
        AssertPairwiseDistinct(first);
        AssertPairwiseDistinct(second);
        for (int index = 0; index < first.Length; index++)
        {
            Assert.NotSame(first[index], second[index]);
            Assert.Equal(ObjectFingerprint(first[index]), ObjectFingerprint(second[index]));
        }

        OmissionAnalysis firstOmissions = AssertNativeParity(first, records, iddOracle);
        OmissionAnalysis secondOmissions = AssertNativeParity(second, records, iddOracle);
        Assert.Equal(firstOmissions.BlankOrNoneCount, secondOmissions.BlankOrNoneCount);
        Assert.Equal(firstOmissions.Defaults, secondOmissions.Defaults);
        Assert.Equal(binding.ExpectedBlankOmissionCount, firstOmissions.BlankOrNoneCount);
        Assert.Empty(firstOmissions.Defaults);

        string[] linkageFacts = AssertNativeLinkage(
            binding,
            scenario,
            firstDocument,
            first);
        Assert.Equal(
            linkageFacts,
            AssertNativeLinkage(binding, scenario, secondDocument, second));
        string[] nativeFacts = new[]
        {
            "native-public-route=EnergyModel.ToIdfDocument",
            "native-private-mapper=" + ExpectedSymbols.Single(item => item.Symbol == binding.Symbol).ImplementationSymbol,
            "semantic-target-selection=object-family-and-exact-name",
            "native-compact-field-counts=" + string.Join(",", binding.NativeCompactFieldCounts),
            "python-complete-field-counts=" + string.Join(",", records.EnumerateArray().Select(item => item.GetProperty("field_count").GetInt32())),
            "omitted-tail-classification=blank-or-none:" + firstOmissions.BlankOrNoneCount + ";official-idd-default:0",
            "omission-source=EnergyPlus-24.2.0-build-94a887817b",
            "two-call-freshness=distinct-documents-and-distinct-target-objects",
            "two-call-determinism=order-independent-document-multiset-and-target-fields-identical",
            "source-model-values-and-references=immutable-across-two-emissions",
            "global-document-order=not-claimed",
            "deduplication-and-conflict-semantics=not-claimed",
        }.Concat(linkageFacts).ToArray();
        Assert.Equal(nativeFacts.Length, nativeFacts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(nativeFacts, item => Assert.False(string.IsNullOrWhiteSpace(item)));

        return new NativeObservation(
            binding.CaseId,
            binding.Symbol,
            first.Select(item => item.ObjectType).ToArray(),
            first.Select(item => item.Count).ToArray(),
            first.Select(item => item.Name ?? string.Empty).ToArray(),
            first.Select(item => item.Fields.Select(field => field.Value).ToArray()).ToArray(),
            firstOmissions.BlankOrNoneCount,
            firstOmissions.Defaults,
            nativeFacts);
    }

    private static IdfObject[] SelectNativeTargets(
        IdfDocument document,
        JsonElement encodedRecords)
    {
        return encodedRecords.EnumerateArray().Select(record =>
        {
            string objectType = RequiredString(record, "object_type");
            JsonElement firstField = record.GetProperty("ordered_fields")[0];
            string expectedName = RequiredString(firstField.GetProperty("value"), "value");
            return Assert.Single(
                document[objectType],
                candidate => string.Equals(
                    candidate.Name,
                    expectedName,
                    StringComparison.Ordinal));
        }).ToArray();
    }

    private static OmissionAnalysis AssertNativeParity(
        IReadOnlyList<IdfObject> nativeObjects,
        JsonElement encodedRecords,
        OfficialIddOracle iddOracle)
    {
        JsonElement[] records = encodedRecords.EnumerateArray().ToArray();
        Assert.Equal(records.Length, nativeObjects.Count);
        var defaults = new List<DefaultOmissionFact>();
        int blankOrNone = 0;
        int comparedPresent = 0;
        for (int objectIndex = 0; objectIndex < records.Length; objectIndex++)
        {
            IdfObject native = nativeObjects[objectIndex];
            JsonElement record = records[objectIndex];
            string objectType = RequiredString(record, "object_type");
            Assert.Equal(objectType, native.ObjectType);
            OfficialIddObject official = iddOracle[objectType];
            JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
            Assert.Equal(official.Fields.Length, fields.Length);
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                JsonElement field = fields[fieldIndex];
                string fieldName = RequiredString(field, "name");
                Assert.Equal(official.ResolveFieldName(fieldIndex), fieldName);
                JsonElement encoded = field.GetProperty("value");
                if (fieldIndex < native.Count)
                {
                    AssertEncodedValueMatchesNative(encoded, native[fieldIndex]);
                    comparedPresent++;
                    continue;
                }

                string kind = RequiredString(encoded, "kind");
                if (kind == "none")
                {
                    blankOrNone++;
                    continue;
                }

                OfficialIddField officialField = official.Fields[fieldIndex];
                Assert.False(string.IsNullOrWhiteSpace(officialField.DefaultValue));
                AssertEncodedValueMatchesDefault(encoded, officialField.DefaultValue!);
                defaults.Add(new DefaultOmissionFact(
                    objectType,
                    fieldIndex,
                    fieldName,
                    EncodedDisplay(encoded),
                    officialField.DefaultValue!));
            }
        }

        Assert.Equal(nativeObjects.Sum(item => item.Count), comparedPresent);
        Assert.Equal(
            records.Sum(item => item.GetProperty("field_count").GetInt32()) - comparedPresent,
            blankOrNone + defaults.Count);
        return new OmissionAnalysis(defaults.ToArray(), blankOrNone);
    }

    private static void AssertEncodedValueMatchesNative(JsonElement encoded, string native)
    {
        string kind = RequiredString(encoded, "kind");
        if (kind == "none")
        {
            Assert.Equal(string.Empty, native);
            return;
        }

        if (kind == "str")
        {
            Assert.Equal(RequiredString(encoded, "value"), native);
            return;
        }

        Assert.Equal("float", kind);
        double expected = double.Parse(
            RequiredString(encoded, "repr"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        double actual = double.Parse(native, NumberStyles.Float, CultureInfo.InvariantCulture);
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(expected),
            BitConverter.DoubleToInt64Bits(actual));
    }

    private static void AssertEncodedValueMatchesDefault(JsonElement encoded, string officialDefault)
    {
        string kind = RequiredString(encoded, "kind");
        Assert.NotEqual("none", kind);
        if (kind == "str")
        {
            Assert.Equal(RequiredString(encoded, "value"), officialDefault);
            return;
        }

        double expected = double.Parse(
            RequiredString(encoded, "repr"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        double actual = double.Parse(
            officialDefault,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        Assert.Equal(expected, actual, 12);
    }

    private static string EncodedDisplay(JsonElement encoded)
    {
        return RequiredString(encoded, "kind") switch
        {
            "str" => RequiredString(encoded, "value"),
            "float" => RequiredString(encoded, "repr"),
            _ => "None",
        };
    }

    private static string[] AssertNativeLinkage(
        CaseBinding binding,
        Scenario scenario,
        IdfDocument document,
        IReadOnlyList<IdfObject> targets)
    {
        IdfObject hostSurface = Assert.Single(
            document["BuildingSurface:Detailed"],
            item => string.Equals(item.Name, scenario.Surface.Name, StringComparison.Ordinal));
        if (binding.Symbol == "AirBoundary.to_idf_object")
        {
            IdfObject construction = Assert.Single(targets);
            Assert.Equal(construction[0], hostSurface[2]);
            Assert.Equal("SimpleMixing", construction[1]);
            return new[]
            {
                "surface-construction-link=" + scenario.Surface.Name + "->" + construction[0],
                "air-boundary-method=SimpleMixing",
            };
        }

        if (binding.Symbol == "Construction.to_idf_object")
        {
            IdfObject construction = Assert.Single(targets);
            OpaqueConstruction source = Assert.IsType<OpaqueConstruction>(scenario.Surface.Construction);
            string scopedName = source.Name + ":for:" + scenario.Surface.Name;
            Assert.Equal(scopedName, construction[0]);
            Assert.Equal(scopedName, hostSurface[2]);
            Assert.Equal(source.Layers.Select(item => item.Name), construction.Fields.Skip(1).Select(item => item.Value));
            return new[]
            {
                "surface-scoped-construction=" + scopedName,
                "exact-layer-linkage=" + string.Join("->", source.Layers.Select(item => item.Name)),
            };
        }

        if (binding.Symbol == "Glazing.to_idf_object")
        {
            Assert.Equal(2, targets.Count);
            IdfObject material = targets[0];
            IdfObject construction = targets[1];
            Window window = Assert.Single(scenario.Surface.Windows);
            IdfObject fenestration = Assert.Single(
                document["FenestrationSurface:Detailed"],
                item => string.Equals(item.Name, window.Name, StringComparison.Ordinal));
            Assert.Equal(material[0], construction[1]);
            Assert.Equal(construction[0], fenestration[2]);
            Assert.Equal(scenario.Surface.Name, fenestration[3]);
            return new[]
            {
                "glazing-material-construction-link=" + material[0] + "->" + construction[0],
                "fenestration-construction-link=" + window.Name + "->" + construction[0],
            };
        }

        if (binding.Symbol == "Layer.to_idf_object")
        {
            IdfObject material = Assert.Single(targets);
            OpaqueConstruction source = Assert.IsType<OpaqueConstruction>(scenario.Surface.Construction);
            string scopedName = source.Name + ":for:" + scenario.Surface.Name;
            IdfObject construction = Assert.Single(
                document["Construction"],
                item => string.Equals(item.Name, scopedName, StringComparison.Ordinal));
            Assert.Equal(material[0], construction[1]);
            Assert.Equal(scopedName, hostSurface[2]);
            return new[]
            {
                "layer-material-fields=all-nine",
                "surface-material-link=" + scopedName + "->" + material[0],
            };
        }

        Assert.Equal("NoMassConstruction.to_idf_object", binding.Symbol);
        Assert.Equal(2, targets.Count);
        IdfObject noMassMaterial = targets[0];
        IdfObject noMassConstruction = targets[1];
        Assert.Equal(noMassMaterial[0], noMassConstruction[1]);
        Assert.Equal(noMassConstruction[0], hostSurface[2]);
        Assert.Equal("Rough", noMassMaterial[1]);
        Assert.Equal("0.9", noMassMaterial[3]);
        Assert.Equal("0.7", noMassMaterial[4]);
        Assert.Equal("0.7", noMassMaterial[5]);
        return new[]
        {
            "no-mass-material-construction-link=" + noMassMaterial[0] + "->" + noMassConstruction[0],
            "no-mass-resistance=reciprocal-of-source-u;absorptances=0.9,0.7,0.7",
        };
    }

    private static void AssertPairwiseDistinct(IReadOnlyList<IdfObject> objects)
    {
        for (int first = 0; first < objects.Count; first++)
        {
            for (int second = first + 1; second < objects.Count; second++)
            {
                Assert.NotSame(objects[first], objects[second]);
            }
        }
    }

    private static string ObjectFingerprint(IdfObject value) =>
        JsonSerializer.Serialize(new
        {
            object_type = value.ObjectType,
            fields = value.Fields.Select(item => item.Value).ToArray(),
        });

    private static string OrderIndependentDocumentFingerprint(IdfDocument document)
    {
        string[] objects = document
            .Select(ObjectFingerprint)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(objects)));
    }

    private static OfficialIddOracle LoadOfficialIddOracle()
    {
        byte[] compressedBytes = File.ReadAllBytes(FindRepositoryFile(IddOracleRepositoryPath));
        Assert.Equal(IddOracleByteLength, compressedBytes.Length);
        Assert.Equal(IddOracleSha256, Sha256(compressedBytes));
        using var input = new MemoryStream(compressedBytes, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using JsonDocument document = JsonDocument.Parse(
            gzip,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        JsonElement root = document.RootElement;
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(
            root,
            "energyplus_build",
            "energyplus_version",
            "field_count",
            "groups",
            "object_count",
            "objects",
            "official_epjson_schema",
            "oracle_schema",
            "source_bytes",
            "source_sha256",
            "upstream_commit");
        Assert.Equal(IddOracleSchema, RequiredString(root, "oracle_schema"));
        Assert.Equal(UpstreamCommit, RequiredString(root, "upstream_commit"));
        Assert.Equal(EnergyPlusVersion, RequiredString(root, "energyplus_version"));
        Assert.Equal(EnergyPlusBuild, RequiredString(root, "energyplus_build"));
        Assert.Equal(EnergyPlusIddSourceSha256, RequiredString(root, "source_sha256"));
        Assert.Equal(EnergyPlusIddSourceByteLength, root.GetProperty("source_bytes").GetInt32());
        Assert.Equal(848, root.GetProperty("object_count").GetInt32());
        Assert.Equal(13_702, root.GetProperty("field_count").GetInt32());
        string[] groups = root.GetProperty("groups").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Equal(59, groups.Length);
        Assert.Equal(groups.Length, groups.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        ValidateOfficialEpJsonReceipt(root.GetProperty("official_epjson_schema"));
        JsonElement[] objects = root.GetProperty("objects").EnumerateArray().ToArray();
        Assert.Equal(848, objects.Length);
        Assert.Equal(
            Enumerable.Range(0, objects.Length),
            objects.Select(item => item.GetProperty("position").GetInt32()));
        Assert.Equal(
            objects.Length,
            objects.Select(item => RequiredString(item, "name"))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(13_702, objects.Sum(item => item.GetProperty("fields").GetArrayLength()));

        var selected = new List<OfficialIddObject>();
        foreach (string objectType in ObjectTypes)
        {
            JsonElement item = Assert.Single(
                objects,
                value => RequiredString(value, "name") == objectType);
            selected.Add(ParseOfficialIddObject(item));
        }

        for (int index = 0; index < selected.Count; index++)
        {
            OfficialIddObject actual = selected[index];
            SelectedIddTopology expected = ExpectedIddTopologies[index];
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.FieldCount, actual.Fields.Length);
            Assert.Equal(expected.MinimumFields, actual.MinimumFields);
            Assert.Equal(expected.ExtensibleStartIndex, actual.ExtensibleStartIndex);
            Assert.Equal(expected.ExtensibleGroupSize, actual.ExtensibleGroupSize);
        }

        return new OfficialIddOracle(selected);
    }

    private static void ValidateOfficialEpJsonReceipt(JsonElement value)
    {
        AssertKeys(
            value,
            "energyplus_version",
            "extensible_object_count",
            "extensible_prototype_field_count",
            "field_definition_count",
            "not_compared_metadata",
            "object_count",
            "official_enum_superset_field_count",
            "paired_energyplus_build",
            "schema_draft",
            "source_bytes",
            "source_sha256",
            "unrepresented_choice_type_count",
            "unrepresented_external_list_name_count",
            "unrepresented_field_topology_object_count",
            "unrepresented_node_type_count",
            "unrepresented_required_flag_count",
            "validated_dimensions",
            "validated_field_occurrence_count");
        Assert.Equal("24.2", RequiredString(value, "energyplus_version"));
        Assert.Equal(EnergyPlusBuild, RequiredString(value, "paired_energyplus_build"));
        Assert.Equal("https://json-schema.org/draft-07/schema#", RequiredString(value, "schema_draft"));
        Assert.Equal(OfficialEpJsonSourceSha256, RequiredString(value, "source_sha256"));
        Assert.Equal(OfficialEpJsonSourceByteLength, value.GetProperty("source_bytes").GetInt32());
        Assert.Equal(848, value.GetProperty("object_count").GetInt32());
        Assert.Equal(13_469, value.GetProperty("field_definition_count").GetInt32());
        Assert.Equal(13_469, value.GetProperty("validated_field_occurrence_count").GetInt32());
        Assert.Equal(120, value.GetProperty("extensible_object_count").GetInt32());
        Assert.Equal(256, value.GetProperty("extensible_prototype_field_count").GetInt32());
        Assert.Equal(18, value.GetProperty("official_enum_superset_field_count").GetInt32());
        Assert.Equal(1, value.GetProperty("unrepresented_choice_type_count").GetInt32());
        Assert.Equal(0, value.GetProperty("unrepresented_external_list_name_count").GetInt32());
        Assert.Equal(6, value.GetProperty("unrepresented_field_topology_object_count").GetInt32());
        Assert.Equal(797, value.GetProperty("unrepresented_node_type_count").GetInt32());
        Assert.Equal(25, value.GetProperty("unrepresented_required_flag_count").GetInt32());
        Assert.Equal(9, value.GetProperty("not_compared_metadata").GetArrayLength());
        Assert.Equal(5, value.GetProperty("validated_dimensions").GetArrayLength());
    }

    private static OfficialIddObject ParseOfficialIddObject(JsonElement item)
    {
        AssertKeys(
            item,
            "additional_directives",
            "extensible_group_size",
            "extensible_start_index",
            "fields",
            "format",
            "group",
            "is_required",
            "is_unique",
            "memo",
            "minimum_fields",
            "name",
            "obsolete_message",
            "position");
        JsonElement[] fields = item.GetProperty("fields").EnumerateArray().ToArray();
        var parsed = new OfficialIddField[fields.Length];
        for (int index = 0; index < fields.Length; index++)
        {
            JsonElement field = fields[index];
            AssertKeys(
                field,
                "additional_directives",
                "begins_extensible",
                "choices",
                "data_type",
                "default_value",
                "external_list",
                "ip_units",
                "is_autocalculatable",
                "is_autosizable",
                "is_deprecated",
                "is_required",
                "kind",
                "maximum",
                "minimum",
                "name",
                "notes",
                "object_lists",
                "position",
                "reference_class_names",
                "references",
                "retains_case",
                "token",
                "units",
                "units_based_on_field");
            Assert.Equal(index, field.GetProperty("position").GetInt32());
            string kind = RequiredString(field, "kind");
            Assert.True(kind is "alpha" or "numeric");
            JsonElement defaultValue = field.GetProperty("default_value");
            Assert.True(defaultValue.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            parsed[index] = new OfficialIddField(
                RequiredString(field, "token"),
                index,
                kind,
                RequiredString(field, "name"),
                field.GetProperty("begins_extensible").GetBoolean(),
                defaultValue.ValueKind == JsonValueKind.Null ? null : defaultValue.GetString());
        }

        JsonElement start = item.GetProperty("extensible_start_index");
        Assert.True(start.ValueKind is JsonValueKind.Null or JsonValueKind.Number);
        return new OfficialIddObject(
            RequiredString(item, "name"),
            RequiredString(item, "group"),
            item.GetProperty("minimum_fields").GetInt32(),
            start.ValueKind == JsonValueKind.Null ? null : start.GetInt32(),
            item.GetProperty("extensible_group_size").GetInt32(),
            parsed);
    }

    private static object CreateReceipt(
        SymbolBinding symbol,
        IReadOnlyList<NativeObservation> observations)
    {
        return new
        {
            artifacts = new
            {
                fixture = new
                {
                    byte_length = OracleByteLength,
                    case_count = ExpectedCases.Length,
                    cases_sha256 = CasesSha256,
                    path = OracleRepositoryPath,
                    sha256 = OracleSha256,
                },
                generator = new
                {
                    byte_length = GeneratorByteLength,
                    path = GeneratorRepositoryPath,
                    sha256 = GeneratorSha256,
                },
                idd_default_oracle = new
                {
                    compressed_byte_length = IddOracleByteLength,
                    compressed_sha256 = IddOracleSha256,
                    energyplus_build = EnergyPlusBuild,
                    energyplus_version = EnergyPlusVersion,
                    official_epjson_source_byte_length = OfficialEpJsonSourceByteLength,
                    official_epjson_source_sha256 = "sha256:" + OfficialEpJsonSourceSha256,
                    official_idd_source_byte_length = EnergyPlusIddSourceByteLength,
                    official_idd_source_sha256 = "sha256:" + EnergyPlusIddSourceSha256,
                    oracle_schema = IddOracleSchema,
                    path = IddOracleRepositoryPath,
                },
                implementation = new
                {
                    byte_length = ImplementationByteLength,
                    path = ImplementationRepositoryPath,
                    sha256 = ImplementationSha256,
                },
                public_route = new
                {
                    byte_length = PublicByteLength,
                    path = PublicRepositoryPath,
                    sha256 = PublicSha256,
                },
                python_validator = new
                {
                    byte_length = PythonValidatorByteLength,
                    path = PythonValidatorRepositoryPath,
                    sha256 = PythonValidatorSha256,
                },
            },
            native_binding = new
            {
                adaptation_id = symbol.AdaptationId,
                classification = "exception",
                implementation_symbol = symbol.ImplementationSymbol,
                native_target = symbol.NativeTarget,
                public_symbol = PublicSymbol,
            },
            observations = observations.Select(item => new
            {
                adaptation_id = symbol.AdaptationId,
                case_id = item.CaseId,
                compact_field_counts = item.CompactFieldCounts,
                native_facts = item.NativeFacts,
                native_object_field_values = item.NativeObjectFieldValues,
                native_object_names = item.NativeObjectNames,
                native_object_types = item.NativeObjectTypes,
                native_outcome = "returned",
                omitted_blank_or_none_count = item.OmittedBlankOrNoneCount,
                omitted_official_idd_defaults = item.OmittedOfficialIddDefaults.Select(value => new
                {
                    field_name = value.FieldName,
                    object_type = value.ObjectType,
                    official_idd_default = value.OfficialIddDefault,
                    python_encoded_value = value.PythonEncodedValue,
                    zero_based_position = value.ZeroBasedPosition,
                }).ToArray(),
            }).ToArray(),
            representation = new
            {
                comparison = "every-python-field-in-order-versus-native-present-fields-and-classified-omitted-tail",
                fixture_result_shape = "standalone-fresh-list-or-object",
                native_result_shape = "fresh-EnergyModel-IDF-documents-with-semantic-target-selection",
                official_idd_default_omission_count = observations.Sum(item => item.OmittedOfficialIddDefaults.Length),
                omitted_blank_or_none_count = observations.Sum(item => item.OmittedBlankOrNoneCount),
                omission_policy = "trailing-blank-or-None-or-independently-pinned-official-IDD-default",
            },
            scope = new
            {
                context_only_not_targeted = ContextOnlyNotTargeted,
                full_symbol_closure = false,
                scope = FixtureScope,
                unresolved_behavior = UnresolvedBehavior,
            },
            upstream = new
            {
                ast_sha256 = UpstreamAstSha256,
                body_hash = symbol.BodyHash,
                inventory_index = symbol.InventoryIndex,
                path = UpstreamPath,
                signature_hash = symbol.SignatureHash,
                source_sha256 = UpstreamSourceSha256,
                symbol = symbol.Symbol,
                symbol_hash = symbol.SymbolHash,
            },
        };
    }

    private static void ValidateReceipt(
        JsonElement receipt,
        SymbolBinding symbol,
        IReadOnlyList<NativeObservation> expectedObservations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertReceiptPayloadSafe(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(receipt, "artifacts", "native_binding", "observations", "representation", "scope", "upstream");

        JsonElement artifacts = receipt.GetProperty("artifacts");
        AssertKeys(
            artifacts,
            "fixture",
            "generator",
            "idd_default_oracle",
            "implementation",
            "public_route",
            "python_validator");
        AssertReceiptArtifact(
            artifacts.GetProperty("generator"),
            GeneratorRepositoryPath,
            GeneratorByteLength,
            GeneratorSha256);
        AssertReceiptArtifact(
            artifacts.GetProperty("python_validator"),
            PythonValidatorRepositoryPath,
            PythonValidatorByteLength,
            PythonValidatorSha256);
        AssertReceiptArtifact(
            artifacts.GetProperty("implementation"),
            ImplementationRepositoryPath,
            ImplementationByteLength,
            ImplementationSha256);
        AssertReceiptArtifact(
            artifacts.GetProperty("public_route"),
            PublicRepositoryPath,
            PublicByteLength,
            PublicSha256);
        JsonElement fixture = artifacts.GetProperty("fixture");
        AssertKeys(fixture, "byte_length", "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(OracleByteLength, fixture.GetProperty("byte_length").GetInt32());
        Assert.Equal(ExpectedCases.Length, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));

        JsonElement idd = artifacts.GetProperty("idd_default_oracle");
        AssertKeys(
            idd,
            "compressed_byte_length",
            "compressed_sha256",
            "energyplus_build",
            "energyplus_version",
            "official_epjson_source_byte_length",
            "official_epjson_source_sha256",
            "official_idd_source_byte_length",
            "official_idd_source_sha256",
            "oracle_schema",
            "path");
        Assert.Equal(IddOracleByteLength, idd.GetProperty("compressed_byte_length").GetInt32());
        Assert.Equal(IddOracleSha256, RequiredString(idd, "compressed_sha256"));
        Assert.Equal(EnergyPlusBuild, RequiredString(idd, "energyplus_build"));
        Assert.Equal(EnergyPlusVersion, RequiredString(idd, "energyplus_version"));
        Assert.Equal(OfficialEpJsonSourceByteLength, idd.GetProperty("official_epjson_source_byte_length").GetInt32());
        Assert.Equal("sha256:" + OfficialEpJsonSourceSha256, RequiredString(idd, "official_epjson_source_sha256"));
        Assert.Equal(EnergyPlusIddSourceByteLength, idd.GetProperty("official_idd_source_byte_length").GetInt32());
        Assert.Equal("sha256:" + EnergyPlusIddSourceSha256, RequiredString(idd, "official_idd_source_sha256"));
        Assert.Equal(IddOracleSchema, RequiredString(idd, "oracle_schema"));
        Assert.Equal(IddOracleRepositoryPath, RequiredString(idd, "path"));

        JsonElement nativeBinding = receipt.GetProperty("native_binding");
        AssertKeys(
            nativeBinding,
            "adaptation_id",
            "classification",
            "implementation_symbol",
            "native_target",
            "public_symbol");
        Assert.Equal(symbol.AdaptationId, RequiredString(nativeBinding, "adaptation_id"));
        Assert.Equal("exception", RequiredString(nativeBinding, "classification"));
        Assert.Equal(symbol.ImplementationSymbol, RequiredString(nativeBinding, "implementation_symbol"));
        Assert.Equal(symbol.NativeTarget, RequiredString(nativeBinding, "native_target"));
        Assert.Equal(PublicSymbol, RequiredString(nativeBinding, "public_symbol"));

        JsonElement[] observations = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(expectedObservations.Count, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            JsonElement observation = observations[index];
            NativeObservation expected = expectedObservations[index];
            AssertKeys(
                observation,
                "adaptation_id",
                "case_id",
                "compact_field_counts",
                "native_facts",
                "native_object_field_values",
                "native_object_names",
                "native_object_types",
                "native_outcome",
                "omitted_blank_or_none_count",
                "omitted_official_idd_defaults");
            Assert.Equal(symbol.AdaptationId, RequiredString(observation, "adaptation_id"));
            Assert.Equal(expected.CaseId, RequiredString(observation, "case_id"));
            Assert.Equal("returned", RequiredString(observation, "native_outcome"));
            AssertIntArray(observation.GetProperty("compact_field_counts"), expected.CompactFieldCounts);
            AssertStringArray(observation.GetProperty("native_object_types"), expected.NativeObjectTypes);
            AssertStringArray(observation.GetProperty("native_object_names"), expected.NativeObjectNames);
            AssertStringArray(observation.GetProperty("native_facts"), expected.NativeFacts);
            Assert.Equal(
                expected.OmittedBlankOrNoneCount,
                observation.GetProperty("omitted_blank_or_none_count").GetInt32());
            Assert.Equal(
                expected.OmittedOfficialIddDefaults.Length,
                observation.GetProperty("omitted_official_idd_defaults").GetArrayLength());
            JsonElement[] rows = observation.GetProperty("native_object_field_values").EnumerateArray().ToArray();
            Assert.Equal(expected.NativeObjectFieldValues.Length, rows.Length);
            for (int row = 0; row < rows.Length; row++)
            {
                AssertStringArray(rows[row], expected.NativeObjectFieldValues[row]);
            }
        }

        JsonElement representation = receipt.GetProperty("representation");
        AssertKeys(
            representation,
            "comparison",
            "fixture_result_shape",
            "native_result_shape",
            "official_idd_default_omission_count",
            "omitted_blank_or_none_count",
            "omission_policy");
        Assert.Equal(0, representation.GetProperty("official_idd_default_omission_count").GetInt32());
        Assert.Equal(
            expectedObservations.Sum(item => item.OmittedBlankOrNoneCount),
            representation.GetProperty("omitted_blank_or_none_count").GetInt32());

        JsonElement scope = receipt.GetProperty("scope");
        AssertKeys(scope, "context_only_not_targeted", "full_symbol_closure", "scope", "unresolved_behavior");
        Assert.False(scope.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(FixtureScope, RequiredString(scope, "scope"));
        AssertStringArray(scope.GetProperty("context_only_not_targeted"), ContextOnlyNotTargeted);
        AssertStringArray(scope.GetProperty("unresolved_behavior"), UnresolvedBehavior);

        JsonElement upstream = receipt.GetProperty("upstream");
        AssertKeys(
            upstream,
            "ast_sha256",
            "body_hash",
            "inventory_index",
            "path",
            "signature_hash",
            "source_sha256",
            "symbol",
            "symbol_hash");
        Assert.Equal(UpstreamAstSha256, RequiredString(upstream, "ast_sha256"));
        Assert.Equal(symbol.BodyHash, RequiredString(upstream, "body_hash"));
        Assert.Equal(symbol.InventoryIndex, upstream.GetProperty("inventory_index").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(symbol.SignatureHash, RequiredString(upstream, "signature_hash"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(upstream, "source_sha256"));
        Assert.Equal(symbol.Symbol, RequiredString(upstream, "symbol"));
        Assert.Equal(symbol.SymbolHash, RequiredString(upstream, "symbol_hash"));
    }

    private static void AssertReceiptArtifact(
        JsonElement value,
        string path,
        int byteLength,
        string sha256)
    {
        AssertKeys(value, "byte_length", "path", "sha256");
        Assert.Equal(byteLength, value.GetProperty("byte_length").GetInt32());
        Assert.Equal(path, RequiredString(value, "path"));
        Assert.Equal(sha256, RequiredString(value, "sha256"));
    }

    private static object[] GraphReferences(Scenario scenario)
    {
        var values = new List<object>
        {
            scenario.Model,
            scenario.Model.Zones,
            scenario.Zone,
            scenario.Zone.Profile,
            scenario.Zone.Surfaces,
            scenario.Surface,
            scenario.Surface.Construction,
            scenario.Surface.Polygon,
            scenario.Surface.Openings,
        };
        if (scenario.Surface.Construction is OpaqueConstruction opaque)
        {
            values.Add(opaque.Layers);
            foreach (Layer layer in opaque.Layers)
            {
                values.Add(layer);
                values.Add(layer.Material);
            }
        }

        foreach (IOpening opening in scenario.Surface.Openings)
        {
            values.Add(opening);
            values.Add(opening.Polygon);
            if (opening is Window window)
            {
                values.Add(window.Glazing);
            }
            else if (opening is Door door)
            {
                values.Add(door.Construction);
            }
        }

        return values.ToArray();
    }

    private static string GraphValueFingerprint(Scenario scenario)
    {
        var values = new List<string>
        {
            "model.name=" + scenario.Model.Name,
            "model.north=" + Bits(scenario.Model.NorthAxisDegrees),
            "model.terrain=" + scenario.Model.Terrain,
            "zone.id=" + scenario.Zone.Id,
            "zone.name=" + scenario.Zone.Name,
            "zone.profile.id=" + scenario.Zone.Profile.Id,
            "zone.profile.name=" + scenario.Zone.Profile.Name,
            "zone.infiltration=" + Bits(scenario.Zone.InfiltrationAirChangesPerHour),
            "zone.lighting=" + Bits(scenario.Zone.LightingPowerDensityWattsPerSquareMetre),
            "zone.outdoor-air=" + Bits(scenario.Zone.OutdoorAirFlowCubicMetresPerSecond),
            "surface.id=" + scenario.Surface.Id,
            "surface.name=" + scenario.Surface.Name,
            "surface.type=" + scenario.Surface.Type,
            "surface.boundary=" + scenario.Surface.Boundary.Condition,
        };
        foreach (Vertex vertex in scenario.Surface.Polygon.Vertices)
        {
            values.Add("surface.vertex=" + Bits(vertex.X) + "," + Bits(vertex.Y) + "," + Bits(vertex.Z));
        }

        switch (scenario.Surface.Construction)
        {
            case AirBoundary air:
                values.Add("construction.air=" + air.Name + "," + Bits(air.AirChangesPerHour));
                break;
            case NoMassConstruction noMass:
                values.Add("construction.no-mass=" + noMass.Name + "," + Bits(noMass.UValueWattsPerSquareMetreKelvin));
                break;
            case OpaqueConstruction opaque:
                values.Add("construction.opaque=" + opaque.Name);
                foreach (Layer layer in opaque.Layers)
                {
                    Material material = layer.Material;
                    values.Add(
                        "layer=" + layer.Name
                        + "," + Bits(layer.ThicknessMetres)
                        + "," + material.Name
                        + "," + material.Roughness
                        + "," + Bits(material.ConductivityWattsPerMetreKelvin)
                        + "," + Bits(material.DensityKilogramsPerCubicMetre)
                        + "," + Bits(material.SpecificHeatJoulesPerKilogramKelvin)
                        + "," + Bits(material.ThermalAbsorptance)
                        + "," + Bits(material.SolarAbsorptance)
                        + "," + Bits(material.VisibleAbsorptance));
                }

                break;
        }

        foreach (IOpening opening in scenario.Surface.Openings)
        {
            values.Add("opening=" + opening.Id + "," + opening.Name + "," + opening.Type);
            foreach (Vertex vertex in opening.Polygon.Vertices)
            {
                values.Add("opening.vertex=" + Bits(vertex.X) + "," + Bits(vertex.Y) + "," + Bits(vertex.Z));
            }

            if (opening is Window window)
            {
                values.Add(
                    "glazing=" + window.Glazing.Name
                    + "," + Bits(window.Glazing.UValueWattsPerSquareMetreKelvin)
                    + "," + Bits(window.Glazing.SolarHeatGainCoefficient));
            }
        }

        foreach (KeyValuePair<string, object> state in scenario.SourceState)
        {
            string value = state.Value switch
            {
                double number => Bits(number),
                string[] strings => string.Join("->", strings),
                _ => state.Value.ToString()!,
            };
            values.Add("source." + state.Key + "=" + value);
        }

        return Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values)));
    }

    private static string Bits(double value) =>
        BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);

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
                foreach (JsonProperty property in value.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
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
                throw new Xunit.Sdk.XunitException(
                    "Unsupported canonical JSON kind '" + value.ValueKind + "'.");
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
                    "consumer_contract" or
                    "expected_dotnet" or
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

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            Assert.False(Regex.IsMatch(
                text,
                @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])",
                RegexOptions.CultureInvariant));
            Assert.False(Regex.IsMatch(
                text,
                @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])",
                RegexOptions.CultureInvariant));
            Assert.False(Regex.IsMatch(
                text,
                @"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d",
                RegexOptions.CultureInvariant));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoUnsafeIdentity(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoUnsafeIdentity(item);
            }
        }
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
            Assert.True(double.IsFinite(number));
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

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private static void AssertIntArray(JsonElement value, params int[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetInt32()));
    }

    private static void AssertBooleanArray(JsonElement value, int count, bool expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        bool[] actual = value.EnumerateArray().Select(item => item.GetBoolean()).ToArray();
        Assert.Equal(count, actual.Length);
        Assert.All(actual, item => Assert.Equal(expected, item));
    }

    private static void AssertPinnedArtifact(
        string repositoryPath,
        int expectedByteLength,
        string expectedSha256)
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(repositoryPath));
        Assert.Equal(expectedByteLength, bytes.Length);
        Assert.Equal(expectedSha256, Sha256(bytes));
    }

    private static string MethodSymbol(MethodInfo method) =>
        method.DeclaringType!.FullName + "." + method.Name;

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

    private sealed record SymbolBinding(
        int InventoryIndex,
        string Symbol,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string AdaptationId,
        string NativeTarget,
        string ImplementationSymbol);

    private sealed record CaseBinding(
        string CaseId,
        string Symbol,
        int[] NativeCompactFieldCounts,
        int ExpectedBlankOmissionCount);

    private sealed record SourceBinding(
        string Module,
        string Path,
        string SourceSha256,
        string AstSha256);

    private sealed record Scenario(
        CaseBinding Binding,
        EnergyModel Model,
        Zone Zone,
        Surface Surface,
        IReadOnlyDictionary<string, object> SourceState);

    private sealed record DefaultOmissionFact(
        string ObjectType,
        int ZeroBasedPosition,
        string FieldName,
        string PythonEncodedValue,
        string OfficialIddDefault);

    private sealed record OmissionAnalysis(
        DefaultOmissionFact[] Defaults,
        int BlankOrNoneCount);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string[] NativeObjectTypes,
        int[] CompactFieldCounts,
        string[] NativeObjectNames,
        string[][] NativeObjectFieldValues,
        int OmittedBlankOrNoneCount,
        DefaultOmissionFact[] OmittedOfficialIddDefaults,
        string[] NativeFacts);

    private sealed record SelectedIddTopology(
        string Name,
        int FieldCount,
        int MinimumFields,
        int? ExtensibleStartIndex,
        int ExtensibleGroupSize);

    private sealed record OfficialIddField(
        string Token,
        int Position,
        string Kind,
        string Name,
        bool BeginsExtensible,
        string? DefaultValue);

    private sealed record OfficialIddObject(
        string Name,
        string Group,
        int MinimumFields,
        int? ExtensibleStartIndex,
        int ExtensibleGroupSize,
        OfficialIddField[] Fields)
    {
        public string ResolveFieldName(int index)
        {
            if (ExtensibleStartIndex is null || index < ExtensibleStartIndex.Value)
            {
                return Fields[index].Name;
            }

            int prototype = ExtensibleStartIndex.Value
                + ((index - ExtensibleStartIndex.Value) % ExtensibleGroupSize);
            int group = ((index - ExtensibleStartIndex.Value) / ExtensibleGroupSize) + 1;
            return Regex.Replace(
                Fields[prototype].Name,
                @"\b1\b",
                group.ToString(CultureInfo.InvariantCulture),
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }
    }

    private sealed class OfficialIddOracle
    {
        private readonly IReadOnlyDictionary<string, OfficialIddObject> objects;

        public OfficialIddOracle(IEnumerable<OfficialIddObject> objects)
        {
            this.objects = objects.ToDictionary(
                item => item.Name,
                StringComparer.OrdinalIgnoreCase);
        }

        public OfficialIddObject this[string objectType] => objects[objectType];
    }

    private sealed record GraphSnapshot(object[] References, string ValueFingerprint)
    {
        public static GraphSnapshot Capture(Scenario scenario) =>
            new(GraphReferences(scenario), GraphValueFingerprint(scenario));

        public void AssertUnchanged(Scenario scenario)
        {
            object[] current = GraphReferences(scenario);
            Assert.Equal(References.Length, current.Length);
            for (int index = 0; index < References.Length; index++)
            {
                Assert.Same(References[index], current[index]);
            }

            Assert.Equal(ValueFingerprint, GraphValueFingerprint(scenario));
        }
    }
}
