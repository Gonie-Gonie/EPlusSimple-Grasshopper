using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.UpstreamTracker;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class SurfaceToIdfObjectOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-shape-surface-to-idf-object-oracle.json";
    private const int OracleByteLength = 535_248;
    private const string OracleSha256 =
        "sha256:4e8bafd045e32e94f343b83c08fa144e925f4aa8fc2b39f981c591d46d35dc9b";
    private const string OracleSchema =
        "goniegonie.python-reference.dragon-shape-surface-to-idf-object.v1";
    private const string CasesSha256 =
        "sha256:d84505731f0e5ebe95144d93faa4bf80752287c5467895ee15f4d083aba5ce11";

    private const string GeneratorRepositoryPath =
        "tools/python-reference/generate_dragon_shape_surface_to_idf_object_oracle.py";
    private const int GeneratorByteLength = 49_149;
    private const string GeneratorSha256 =
        "sha256:bdea72c903ab7ac109a89ee252587087d02dc68633d08f825e9109f132ce320c";
    private const string PythonValidatorRepositoryPath =
        "tests/PythonReference/test_dragon_shape_surface_to_idf_object_oracle.py";
    private const int PythonValidatorByteLength = 29_000;
    private const string PythonValidatorSha256 =
        "sha256:35d881d729209612987198bc512303c436a02e92954f1b1cca985aefe9e2d4be";

    private const string InventoryRepositoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryByteLength = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const int InventoryIndex = 1045;
    private const string UpstreamPath = "src/idragon/dragon/shape.py";
    private const string UpstreamSourceSha256 =
        "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c";
    private const string UpstreamAstSha256 =
        "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2";
    private const string UpstreamSymbol = "Surface.to_idf_object";
    private const string SymbolHash =
        "sha256:a03c4d5229587498a9a3451a51c842e1f6df83e08ff5c42488a64959e384fece";
    private const string SignatureHash =
        "sha256:ee1bf869a7f2dda7ebcd3108769369b9f5b3c52d60c68d9271d39a0f20315bd9";
    private const string BodyHash =
        "sha256:ab08fb2df61d8afa3cf2ad9b423c1e045de29f50d2c1469842934814f103aa9b";

    private const string AssertionId =
        "dragon-shape-surface-to-idf-object-a03c4d52";
    private const string AdaptationId = "legacy-rectangular-surface-idf-emission";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Model.SurfaceToIdfObjectOracleParityTests.MatchesPinnedPythonSurfaceEmissionThroughLegacyEnergyModelRoute";
    private const string PublicSymbol =
        "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument";
    private const string PublicRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs";
    private const int PublicByteLength = 22_015;
    private const string PublicSha256 =
        "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3";
    private const string ImplementationRepositoryPath =
        "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs";
    private const int ImplementationByteLength = 50_764;
    private const string ImplementationSha256 =
        "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905";

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

    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        WriteIndented = true,
    };
    private const string ReceiptSha256 =
        "sha256:7976d445b4680e73f8beef0304bb16e0bb87d375ea9567b9ebf1fcad7fde7ed4";

    private static readonly string[] SelectedObjectTypes =
    {
        "BuildingSurface:Detailed",
        "Window:Interzone",
        "Door:Interzone",
        "Window",
        "Door",
        "WindowMaterial:Blind",
        "WindowShadingControl",
        "WindowMaterial:Shade",
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new(
            "dragon-shape-surface-to-idf-object.adiabatic-ceiling.custom-air-boundary",
            "sha256:6d7e9229fb591479a553f453edebaaf34132c997b4bbb31625e179ad094caefa",
            new[] { "BuildingSurface:Detailed" },
            new[] { 371 },
            new NativePin(
                21,
                new[] { 23 },
                new[] { 348, 1, 0, 1 },
                "sha256:7c66b450ad379eac6c50ce3e437c5447380a32919a1801bb584d64f7732726c4",
                "sha256:d03bf0a31c1b3af4bb428d2693ab1e4b21f62d78b3c2ff17d211bc65392b4fe7",
                "sha256:02e0cbed24a78bdd03a9d9b7176cc30454d796b47df303adc6f216f84c98bf50",
                new[] { "BuildingSurface:Detailed|Adiabatic Custom-Air Ceiling" },
                new[]
                {
                    "Zone|Adiabatic Parent Zone",
                    "Construction:AirBoundary|Custom Transfer Air Boundary",
                    "BuildingSurface:Detailed|Adiabatic Custom-Air Ceiling",
                },
                new[]
                {
                    "omit|BuildingSurface:Detailed|Adiabatic Custom-Air Ceiling|23-370|native-compact-trailing-None",
                    "value|BuildingSurface:Detailed|Adiabatic Custom-Air Ceiling|2|DefaultAirBoundary|Custom Transfer Air Boundary|native-correct-custom-AirBoundary-reference-versus-upstream-dangling-DefaultAirBoundary",
                })),
        new(
            "dragon-shape-surface-to-idf-object.ground-floor.pentagon",
            "sha256:0f259981989b7cbcbfe4033832b64ff3304e694656b577a3fedfd05ff2e31efa",
            new[] { "BuildingSurface:Detailed" },
            new[] { 371 },
            new NativePin(
                22,
                new[] { 26 },
                new[] { 345, 1, 0, 0 },
                "sha256:b5e7c793d9cc6abde8ac815214c7d863cc48fd08841270e35d435b15db432268",
                "sha256:370ca48222b24498a16e104537fec48f2e76d911712838196ec38cdf46bd2a50",
                "sha256:b8e328f27fa760d53a1e0ed6d9f88770a9eae4806907381484cbc0fc7bcc1941",
                new[] { "BuildingSurface:Detailed|Ground Pentagon Floor" },
                new[]
                {
                    "Zone|Ground Parent Zone",
                    "Material|Native layer 1",
                    "Construction|Ground Pentagon Assembly:for:Ground Pentagon Floor",
                    "BuildingSurface:Detailed|Ground Pentagon Floor",
                },
                new[]
                {
                    "omit|BuildingSurface:Detailed|Ground Pentagon Floor|26-370|native-compact-trailing-None",
                })),
        new(
            "dragon-shape-surface-to-idf-object.interzone-wall.reciprocal-two-windows-two-doors",
            "sha256:aa54a964ebadce1cdbc0717b7d43b32eb76a7c6f3f2ceffebb800496004fff1f",
            new[]
            {
                "Window:Interzone", "Window:Interzone", "Door:Interzone",
                "Door:Interzone", "BuildingSurface:Detailed", "Window:Interzone",
                "Window:Interzone", "Door:Interzone", "Door:Interzone",
                "BuildingSurface:Detailed",
            },
            new[] { 9, 9, 9, 9, 371, 9, 9, 9, 9, 371 },
            new NativePin(
                38,
                new[] { 9, 9, 9, 9, 23, 9, 9, 9, 9, 23 },
                new[] { 696, 2, 2, 0 },
                "sha256:085914ae1ea97757c203349822cd174f1901e4c3cfed40b3fbeb57ccbcd50260",
                "sha256:5e7bee1746747f54e93fb3568e61fd3c8704eb0c26d96f4499661ddcb9b5c8c9",
                "sha256:a0e3f877266a7683e597b21198b23ecf3116fd41fee6e6eff2abb8663b184ee0",
                new[]
                {
                    "BuildingSurface:Detailed|Interzone Wall A",
                    "Window:Interzone|Interzone A Window 1",
                    "Window:Interzone|Interzone A Window 2",
                    "Door:Interzone|Interzone A Door 1",
                    "Door:Interzone|Interzone A Door 2",
                    "BuildingSurface:Detailed|Interzone Wall B",
                    "Window:Interzone|Interzone B Window 1",
                    "Window:Interzone|Interzone B Window 2",
                    "Door:Interzone|Interzone B Door 1",
                    "Door:Interzone|Interzone B Door 2",
                },
                new[]
                {
                    "Zone|Interzone Parent Zone A",
                    "Material|Native layer 2",
                    "Construction|Interzone Wall Assembly A:for:Interzone Wall A",
                    "BuildingSurface:Detailed|Interzone Wall A",
                    "WindowMaterial:SimpleGlazingSystem|$GLAZING_FOR$Interzone Shared Glazing",
                    "Construction|Interzone Shared Glazing",
                    "Window:Interzone|Interzone A Window 1",
                    "Window:Interzone|Interzone A Window 2",
                    "Material:NoMass|$MaterialFor$_Interzone Door Assembly",
                    "Construction|Interzone Door Assembly",
                    "Door:Interzone|Interzone A Door 1",
                    "Door:Interzone|Interzone A Door 2",
                    "Zone|Interzone Parent Zone B",
                    "Material|Native layer 3",
                    "Construction|Interzone Wall Assembly B:for:Interzone Wall B",
                    "BuildingSurface:Detailed|Interzone Wall B",
                    "Window:Interzone|Interzone B Window 1",
                    "Window:Interzone|Interzone B Window 2",
                    "Door:Interzone|Interzone B Door 1",
                    "Door:Interzone|Interzone B Door 2",
                },
                new[]
                {
                    "omit|BuildingSurface:Detailed|Interzone Wall A|23-370|native-compact-trailing-None",
                    "omit|BuildingSurface:Detailed|Interzone Wall B|23-370|native-compact-trailing-None",
                    "lexical|Window:Interzone|Interzone A Window 2|7|0.4668222740913638|0.4668222740913636|native-opening-polygon-area-roundoff-within-1e-12",
                    "lexical|Window:Interzone|Interzone B Window 2|7|0.4668222740913638|0.4668222740913636|native-opening-polygon-area-roundoff-within-1e-12",
                })),
        new(
            "dragon-shape-surface-to-idf-object.outdoors-ceiling.roof",
            "sha256:dfee7032a57f0a7a2737fd5c934fe8ca9fc4432c93f3f8936c1b7232b61ffa11",
            new[] { "BuildingSurface:Detailed" },
            new[] { 371 },
            new NativePin(
                22,
                new[] { 23 },
                new[] { 348, 1, 0, 0 },
                "sha256:174d81bccc7c17189a76f5ac59ba6c8d44b76e8d5b9bfbfbd4702b883db1ccf2",
                "sha256:7d80d7ba2108479897e8e0e59fd22201970a1dde6066f8b252e933d64b5c27ef",
                "sha256:4470e204310b807b1977f95476a00b22f5fd7d473ee3186399a25351c30d51a8",
                new[] { "BuildingSurface:Detailed|Outdoor Ceiling Becomes Roof" },
                new[]
                {
                    "Zone|Outdoor Roof Parent Zone",
                    "Material|Native layer 4",
                    "Construction|Outdoor Roof Assembly:for:Outdoor Ceiling Becomes Roof",
                    "BuildingSurface:Detailed|Outdoor Ceiling Becomes Roof",
                },
                new[]
                {
                    "omit|BuildingSurface:Detailed|Outdoor Ceiling Becomes Roof|23-370|native-compact-trailing-None",
                })),
        new(
            "dragon-shape-surface-to-idf-object.outdoors-wall.multiple-openings-blind-shade",
            "sha256:8910fb4c4633de0cea33e4c64ce60677eeffd97acce1058ecaf8a71302d2d6c6",
            new[]
            {
                "Window", "Window", "Window", "Door", "Door",
                "WindowMaterial:Blind", "WindowShadingControl",
                "WindowMaterial:Shade", "WindowShadingControl",
                "BuildingSurface:Detailed",
            },
            new[] { 9, 9, 9, 8, 8, 29, 26, 15, 26, 371 },
            new NativePin(
                35,
                new[] { 9, 9, 9, 8, 8, 29, 17, 15, 17, 23 },
                new[] { 366, 3, 19, 0 },
                "sha256:accbdf7010c6dccee1176f57b5fd409ce7be74bc60e251389cbf302b97bd57bf",
                "sha256:30bf0dafe7daa808849c6392f66ce7293b2111e9b9898d20ff7284c90498203c",
                "sha256:6b4cf576ee3970184f0675c3c657bb2f3b157eb03d5778ad38f2674508acbcfe",
                new[]
                {
                    "BuildingSurface:Detailed|Outdoor Multi-Opening Wall",
                    "Window|Outdoor Blind Window",
                    "WindowMaterial:Blind|Strong Interior Blind",
                    "WindowShadingControl|Outdoor Blind Window:ShadingControl",
                    "Window|Outdoor Shade Window",
                    "WindowMaterial:Shade|Simple Interior Shade",
                    "WindowShadingControl|Outdoor Shade Window:ShadingControl",
                    "Window|Outdoor Clear Window",
                    "Door|Outdoor Door 1",
                    "Door|Outdoor Door 2",
                },
                new[]
                {
                    "Zone|Outdoor Openings Parent Zone",
                    "Material|Native layer 5",
                    "Construction|Outdoor Wall Assembly:for:Outdoor Multi-Opening Wall",
                    "BuildingSurface:Detailed|Outdoor Multi-Opening Wall",
                    "WindowMaterial:SimpleGlazingSystem|$GLAZING_FOR$Outdoor Multi Glazing",
                    "Construction|Outdoor Multi Glazing",
                    "Window|Outdoor Blind Window",
                    "WindowMaterial:Blind|Strong Interior Blind",
                    "WindowShadingControl|Outdoor Blind Window:ShadingControl",
                    "Window|Outdoor Shade Window",
                    "WindowMaterial:Shade|Simple Interior Shade",
                    "WindowShadingControl|Outdoor Shade Window:ShadingControl",
                    "Window|Outdoor Clear Window",
                    "Material:NoMass|$MaterialFor$_Outdoor Door Assembly",
                    "Construction|Outdoor Door Assembly",
                    "Door|Outdoor Door 1",
                    "Door|Outdoor Door 2",
                },
                new[]
                {
                    "omit|WindowShadingControl|Outdoor Blind Window:ShadingControl|17-25|native-compact-trailing-None",
                    "omit|WindowShadingControl|Outdoor Shade Window:ShadingControl|17-25|native-compact-trailing-None",
                    "omit|BuildingSurface:Detailed|Outdoor Multi-Opening Wall|23-370|native-compact-trailing-None",
                    "lexical|Window|Outdoor Blind Window|7|0.34295513003715344|0.3429551300371535|native-opening-polygon-area-roundoff-within-1e-12",
                    "lexical|Window|Outdoor Shade Window|7|0.45727350671620465|0.4572735067162047|native-opening-polygon-area-roundoff-within-1e-12",
                    "lexical|Door|Outdoor Door 1|6|0.5430122892254929|0.5430122892254928|native-opening-polygon-area-roundoff-within-1e-12",
                    "lexical|Door|Outdoor Door 2|6|0.6287510717347814|0.6287510717347817|native-opening-polygon-area-roundoff-within-1e-12",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|6|221.0|221|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|7|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|10|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|13|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|16|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|19|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|24|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|27|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Blind|Strong Interior Blind|28|180.0|180|numeric-lexical-format-only",
                    "lexical|WindowShadingControl|Outdoor Blind Window:ShadingControl|2|1.0|1|numeric-lexical-format-only",
                    "lexical|WindowShadingControl|Outdoor Blind Window:ShadingControl|7|20.0|20|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Shade|Simple Interior Shade|8|100.0|100|numeric-lexical-format-only",
                    "lexical|WindowMaterial:Shade|Simple Interior Shade|14|0.0|0|numeric-lexical-format-only",
                    "lexical|WindowShadingControl|Outdoor Shade Window:ShadingControl|2|1.0|1|numeric-lexical-format-only",
                    "lexical|WindowShadingControl|Outdoor Shade Window:ShadingControl|7|20.0|20|numeric-lexical-format-only",
                })),
    };

    [Fact]
    public void MatchesPinnedPythonSurfaceEmissionThroughLegacyEnergyModelRoute()
    {
        ValidatePinnedArtifactsAndNativeRoute();
        OfficialIddOracle idd = LoadOfficialIddOracle();
        using JsonDocument oracle = ReadPinnedOracle();
        Scenario[] scenarios = Enumerable.Range(0, ExpectedCases.Length)
            .Select(CreateScenario)
            .ToArray();
        JsonElement[] cases = ValidateCorpus(oracle.RootElement, scenarios, idd);
        AssertIndependentScenarioGraphs(scenarios);

        NativeObservation[] observations = cases
            .Select((item, index) => ExecuteNativeCase(
                ExpectedCases[index],
                item.GetProperty("python").GetProperty("facts"),
                scenarios[index],
                idd))
            .ToArray();

        Assert.Equal(23, observations.Sum(item => item.PythonObjectCount));
        Assert.Equal(2_437, observations.Sum(item => item.PythonFieldCount));
        AssertReciprocalOpeningLinks(observations[2]);
        AssertRoofGroundAndShadingLinks(observations);

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "SURFACE_NATIVE_PINS\n" + JsonSerializer.Serialize(
                    observations.Select(ToDiscoveryPin).ToArray(),
                    DiscoveryJsonOptions));
        }

        for (int index = 0; index < observations.Length; index++)
        {
            AssertNativePin(ExpectedCases[index].Native, observations[index]);
        }

        object receipt = CreateReceipt(observations);
        JsonElement receiptElement = JsonSerializer.SerializeToElement(receipt);
        ValidateReceipt(receiptElement, observations);
        string receiptSha256 = CanonicalSha256(receiptElement);
        Assert.Equal(ReceiptSha256, receiptSha256);
        TrustedEvidenceRecorder.Record(
            AssertionId,
            EvidenceTestCase,
            "not_applicable",
            receipt);
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(OracleRepositoryPath));
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        string text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
    }

    private static JsonElement[] ValidateCorpus(
        JsonElement root,
        IReadOnlyList<Scenario> scenarios,
        OfficialIddOracle idd)
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
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        JsonElement casesElement = root.GetProperty("cases");
        Assert.Equal(CasesSha256, CanonicalSha256(casesElement));
        JsonElement[] cases = casesElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), cases.Select(item => RequiredString(item, "id")));

        ValidateConsumerContract(root.GetProperty("consumer_contract"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateUpstream(root.GetProperty("upstream"), root.GetProperty("symbols"));

        int objectCount = 0;
        int fieldCount = 0;
        for (int index = 0; index < cases.Length; index++)
        {
            JsonElement item = cases[index];
            CaseBinding binding = ExpectedCases[index];
            AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
            Assert.Equal("surface-to-idf-object", RequiredString(item, "executor"));
            Assert.Equal(binding.CaseId, RequiredString(item, "id"));
            Assert.Equal(UpstreamSymbol, RequiredString(item, "symbol"));
            JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
            Assert.Equal(AdaptationId, RequiredString(expectedDotnet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));
            JsonElement python = item.GetProperty("python");
            Assert.Equal("returned", RequiredString(python, "outcome"));
            JsonElement facts = python.GetProperty("facts");
            Assert.Equal(binding.FactSha256, CanonicalSha256(facts));
            ValidatePythonFacts(binding, facts, scenarios[index], idd);
            objectCount += binding.PythonObjectTypes.Length;
            fieldCount += binding.PythonFieldCounts.Sum();
        }

        Assert.Equal(23, objectCount);
        Assert.Equal(2_437, fieldCount);
        return cases;
    }

    private static void ValidateConsumerContract(JsonElement value)
    {
        AssertKeys(
            value,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classification_basis",
            "classifications",
            "closure",
            "identity_encoding",
            "native_targets",
            "raw_field_encoding",
            "runtime_signatures",
            "source_import_policy",
            "target_symbols");
        Assert.Equal(ExpectedCases.Length, value.GetProperty("case_count").GetInt32());
        AssertStringArray(value.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId));
        AssertStringArray(value.GetProperty("target_symbols"), new[] { UpstreamSymbol });
        Assert.Equal(AdaptationId, RequiredString(value.GetProperty("adaptations"), UpstreamSymbol));
        Assert.Equal(AssertionId, RequiredString(value.GetProperty("assertion_ids"), UpstreamSymbol));
        Assert.Equal("exception", RequiredString(value.GetProperty("classifications"), UpstreamSymbol));
        Assert.Equal(
            "EnergyModel.ToIdfDocument with UseLegacyRectangularFenestration",
            RequiredString(value.GetProperty("native_targets"), UpstreamSymbol));
        Assert.Equal(
            "(self, zone: 'Zone') -> 'IdfObject'",
            RequiredString(value.GetProperty("runtime_signatures"), UpstreamSymbol));
        Assert.Equal(
            "complete-ordered-IDD-fields-with-typed-values",
            RequiredString(value, "raw_field_encoding"));
        Assert.Contains("DefaultAirBoundary dangling reference", RequiredString(value, "classification_basis"));

        JsonElement closure = value.GetProperty("closure");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        string[] contextOnly = closure.GetProperty("context_only_not_targeted")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Contains("Window.to_idf_object", contextOnly);
        Assert.Contains("Door.to_idf_object", contextOnly);
        Assert.Contains("Blind.to_idf_object", contextOnly);
        Assert.Contains("Shade.to_idf_object", contextOnly);
        Assert.DoesNotContain(UpstreamSymbol, contextOnly);
        string[] unresolved = closure.GetProperty("unresolved_behavior")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Contains("native-default-detailed-fenestration-route", unresolved);
        Assert.Contains("Window-Door-Blind-Shade-standalone-converter-closure", unresolved);
    }

    private static void ValidateRuntime(JsonElement value)
    {
        AssertKeys(
            value,
            "dependencies",
            "implementation",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(value, "implementation"));
        Assert.Equal("3.12.7", RequiredString(value, "python_version"));
        Assert.Equal("siphash13", RequiredString(value, "python_hash_algorithm"));
        Assert.Equal(0, value.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, value.GetProperty("python_hash_width_bits").GetInt32());
        Assert.True(value.GetProperty("python_dont_write_bytecode").GetBoolean());
        JsonElement dependencies = value.GetProperty("dependencies");
        Assert.Equal(10, dependencies.EnumerateObject().Count());
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
    }

    private static void ValidateUpstream(JsonElement upstream, JsonElement symbols)
    {
        AssertKeys(upstream, "commit", "inventory_sha256", "loaded_local_modules", "sources");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        JsonElement[] loaded = upstream.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(12, sources.Length);
        Assert.Equal(12, loaded.Length);
        for (int index = 0; index < sources.Length; index++)
        {
            Assert.Equal(RequiredString(sources[index], "path"), RequiredString(loaded[index], "path"));
            Assert.Equal(RequiredString(sources[index], "source_sha256"), RequiredString(loaded[index], "source_sha256"));
            Assert.Equal(RequiredString(sources[index], "ast_sha256"), RequiredString(loaded[index], "ast_sha256"));
        }

        JsonElement shape = Assert.Single(sources, item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal(UpstreamSourceSha256, RequiredString(shape, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(shape, "ast_sha256"));
        JsonElement symbol = Assert.Single(symbols.EnumerateArray());
        Assert.Equal(UpstreamSymbol, RequiredString(symbol, "symbol"));
        Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
        Assert.Equal("function", RequiredString(symbol, "kind"));
        Assert.Equal(SymbolHash, RequiredString(symbol, "symbol_hash"));
        Assert.Equal(SignatureHash, RequiredString(symbol, "signature_hash"));
        Assert.Equal(BodyHash, RequiredString(symbol, "body_hash"));

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryRepositoryPath));
        Assert.Equal(InventoryByteLength, inventoryBytes.Length);
        Assert.Equal(InventoryFileSha256, Sha256(inventoryBytes));
        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        JsonElement indexed = inventory.RootElement.GetProperty("symbols")[InventoryIndex];
        Assert.Equal(symbol.GetRawText(), indexed.GetRawText());
    }

    private static void ValidatePythonFacts(
        CaseBinding binding,
        JsonElement facts,
        Scenario scenario,
        OfficialIddOracle idd)
    {
        AssertKeys(facts, "behavior_facts", "emission", "input_context", "input_integrity", "invocation");
        JsonElement emission = facts.GetProperty("emission");
        AssertKeys(
            emission,
            "all_allowed_fields_covered_in_order",
            "first_object_records",
            "first_objects_pairwise_distinct",
            "fresh_call_result_lists",
            "fresh_idf_object_flags",
            "object_count",
            "object_types",
            "result_type",
            "same_idd_definition_flags",
            "second_fields_equal_flags",
            "second_objects_pairwise_distinct");
        Assert.Equal("list", RequiredString(emission, "result_type"));
        Assert.True(emission.GetProperty("all_allowed_fields_covered_in_order").GetBoolean());
        Assert.True(emission.GetProperty("first_objects_pairwise_distinct").GetBoolean());
        Assert.True(emission.GetProperty("second_objects_pairwise_distinct").GetBoolean());
        Assert.True(emission.GetProperty("fresh_call_result_lists").GetBoolean());
        AssertAllTrue(emission.GetProperty("fresh_idf_object_flags"));
        AssertAllTrue(emission.GetProperty("same_idd_definition_flags"));
        AssertAllTrue(emission.GetProperty("second_fields_equal_flags"));
        Assert.Equal(binding.PythonObjectTypes.Length, emission.GetProperty("object_count").GetInt32());
        AssertStringArray(emission.GetProperty("object_types"), binding.PythonObjectTypes);

        JsonElement[] records = emission.GetProperty("first_object_records").EnumerateArray().ToArray();
        Assert.Equal(binding.PythonObjectTypes.Length, records.Length);
        for (int objectIndex = 0; objectIndex < records.Length; objectIndex++)
        {
            JsonElement record = records[objectIndex];
            AssertKeys(record, "field_count", "object_type", "ordered_fields");
            string objectType = RequiredString(record, "object_type");
            Assert.Equal(binding.PythonObjectTypes[objectIndex], objectType);
            Assert.Equal(binding.PythonFieldCounts[objectIndex], record.GetProperty("field_count").GetInt32());
            JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
            Assert.Equal(binding.PythonFieldCounts[objectIndex], fields.Length);
            OfficialIddObject official = idd[objectType];
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                AssertKeys(fields[fieldIndex], "name", "value");
                Assert.Equal(official.ResolveFieldName(fieldIndex), RequiredString(fields[fieldIndex], "name"));
                ValidateEncodedScalar(fields[fieldIndex].GetProperty("value"));
            }
        }

        JsonElement behavior = facts.GetProperty("behavior_facts");
        AssertKeys(
            behavior,
            "air_boundary_reference",
            "call_spans",
            "host_surface_indices",
            "host_surface_last_in_each_call",
            "number_of_vertices_fields",
            "opening_counterpart_links",
            "parent_zone_links",
            "surface_type_mappings",
            "vertex_counts");
        Assert.True(behavior.GetProperty("host_surface_last_in_each_call").GetBoolean());
        int index = Array.IndexOf(ExpectedCases, binding);
        int[][] callSpans = { new[] { 1 }, new[] { 1 }, new[] { 5, 5 }, new[] { 1 }, new[] { 10 } };
        int[][] hostIndices = { new[] { 0 }, new[] { 0 }, new[] { 4, 9 }, new[] { 0 }, new[] { 9 } };
        int[][] vertexCounts = { new[] { 4 }, new[] { 5 }, new[] { 4, 4 }, new[] { 4 }, new[] { 4 } };
        AssertIntArray(behavior.GetProperty("call_spans"), callSpans[index]);
        AssertIntArray(behavior.GetProperty("host_surface_indices"), hostIndices[index]);
        AssertIntArray(behavior.GetProperty("vertex_counts"), vertexCounts[index]);

        JsonElement integrity = facts.GetProperty("input_integrity");
        Assert.Equal(14, integrity.EnumerateObject().Count());
        Assert.All(integrity.EnumerateObject(), property => Assert.True(property.Value.GetBoolean()));
        ValidateFixtureInput(facts.GetProperty("input_context"), scenario);
        JsonElement[] invocations = facts.GetProperty("invocation").GetProperty("calls").EnumerateArray().ToArray();
        Assert.Equal(scenario.TargetSurfaces.Length, invocations.Length);
        for (int call = 0; call < invocations.Length; call++)
        {
            Assert.Equal(scenario.TargetSurfaces[call].Name, RequiredString(invocations[call], "surface_name"));
            Assert.Equal(scenario.Zones[call].Name, RequiredString(invocations[call], "zone_name"));
        }
    }

    private static void ValidateFixtureInput(JsonElement input, Scenario scenario)
    {
        JsonElement[] calls = input.GetProperty("calls").EnumerateArray().ToArray();
        Assert.Equal(scenario.TargetSurfaces.Length, calls.Length);
        for (int index = 0; index < calls.Length; index++)
        {
            Surface surface = scenario.TargetSurfaces[index];
            JsonElement expectedSurface = calls[index].GetProperty("surface");
            Assert.Equal(surface.Name, RequiredString(expectedSurface, "name"));
            Assert.Equal(surface.Type.ToString().ToLowerInvariant(), RequiredString(expectedSurface, "surface_type"));
            Assert.Equal(surface.Construction.Name, RequiredString(expectedSurface.GetProperty("construction"), "name"));
            Assert.Equal(scenario.Zones[index].Name, RequiredString(calls[index].GetProperty("zone"), "name"));
            JsonElement[] vertices = expectedSurface.GetProperty("vertices").EnumerateArray().ToArray();
            Assert.Equal(surface.Polygon.Vertices.Count, vertices.Length);
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                AssertEncodedDouble(vertices[vertex][0], surface.Polygon.Vertices[vertex].X);
                AssertEncodedDouble(vertices[vertex][1], surface.Polygon.Vertices[vertex].Y);
                AssertEncodedDouble(vertices[vertex][2], surface.Polygon.Vertices[vertex].Z);
            }

            Window[] windows = surface.Openings.OfType<Window>().ToArray();
            Door[] doors = surface.Openings.OfType<Door>().ToArray();
            JsonElement[] expectedWindows = expectedSurface.GetProperty("windows").EnumerateArray().ToArray();
            JsonElement[] expectedDoors = expectedSurface.GetProperty("doors").EnumerateArray().ToArray();
            Assert.Equal(windows.Length, expectedWindows.Length);
            Assert.Equal(doors.Length, expectedDoors.Length);
            for (int opening = 0; opening < windows.Length; opening++)
            {
                Assert.Equal(windows[opening].Name, RequiredString(expectedWindows[opening], "name"));
                Assert.Equal(windows[opening].Glazing.Name, RequiredString(expectedWindows[opening], "glazing_name"));
                AssertEncodedDouble(expectedWindows[opening].GetProperty("area"), windows[opening].Area, 1e-12);
            }

            for (int opening = 0; opening < doors.Length; opening++)
            {
                Assert.Equal(doors[opening].Name, RequiredString(expectedDoors[opening], "name"));
                Assert.Equal(doors[opening].Construction.Name, RequiredString(expectedDoors[opening], "construction_name"));
                AssertEncodedDouble(expectedDoors[opening].GetProperty("area"), doors[opening].Area, 1e-12);
            }
        }
    }

    private static Scenario CreateScenario(int index) => index switch
    {
        0 => CreateAdiabaticScenario(),
        1 => CreateGroundScenario(),
        2 => CreateInterzoneScenario(),
        3 => CreateRoofScenario(),
        4 => CreateOutdoorOpeningsScenario(),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    private static Scenario CreateAdiabaticScenario()
    {
        Surface surface = new(
            Entity("SURFACE-ADIABATIC"),
            "Adiabatic Custom-Air Ceiling",
            SurfaceType.Ceiling,
            new AirBoundary("Custom Transfer Air Boundary", 0.73),
            SurfaceBoundary.Adiabatic,
            Polygon((0, 0, 3), (5, 0, 3), (5, 4, 3), (0, 4, 3)));
        Zone zone = CreateZone("ZONE-ADIABATIC", "Adiabatic Parent Zone", surface);
        return ScenarioFor(0, new[] { zone }, new[] { surface });
    }

    private static Scenario CreateGroundScenario()
    {
        Surface surface = new(
            Entity("SURFACE-GROUND"),
            "Ground Pentagon Floor",
            SurfaceType.Floor,
            Opaque("Ground Pentagon Assembly", 1),
            SurfaceBoundary.Ground,
            Polygon((0, 0, 0), (4, 0, 0), (5, 2, 0), (2, 4, 0), (0, 2, 0)));
        Zone zone = CreateZone("ZONE-GROUND", "Ground Parent Zone", surface);
        return ScenarioFor(1, new[] { zone }, new[] { surface });
    }

    private static Scenario CreateInterzoneScenario()
    {
        var glazing = new Glazing("Interzone Shared Glazing", 1.45, 0.41);
        var doorConstruction = new NoMassConstruction("Interzone Door Assembly", 1.8);
        PlanarPolygon window1 = VerticalRectangle(0.1, 2.2, 1.8, 1);
        PlanarPolygon window2 = VerticalRectangle(2.4, 1.4, 1.8, 1);
        PlanarPolygon door1 = VerticalRectangle(0.1, 1.8, 0.2, 1);
        PlanarPolygon door2 = VerticalRectangle(2, 2, 0.2, 1);
        IOpening[] firstOpenings =
        {
            new Window(Entity("WINDOW-A-1"), "Interzone A Window 1", glazing, window1),
            new Window(Entity("WINDOW-A-2"), "Interzone A Window 2", glazing, window2),
            new Door(Entity("DOOR-A-1"), "Interzone A Door 1", doorConstruction, door1),
            new Door(Entity("DOOR-A-2"), "Interzone A Door 2", doorConstruction, door2),
        };
        IOpening[] secondOpenings =
        {
            new Window(Entity("WINDOW-B-1"), "Interzone B Window 1", glazing, window1.Reverse()),
            new Window(Entity("WINDOW-B-2"), "Interzone B Window 2", glazing, window2.Reverse()),
            new Door(Entity("DOOR-B-1"), "Interzone B Door 1", doorConstruction, door1.Reverse()),
            new Door(Entity("DOOR-B-2"), "Interzone B Door 2", doorConstruction, door2.Reverse()),
        };
        Surface first = new(
            Entity("SURFACE-INTERZONE-A"),
            "Interzone Wall A",
            SurfaceType.Wall,
            Opaque("Interzone Wall Assembly A", 2),
            SurfaceBoundary.Outdoors,
            Polygon((0, 0, 0), (4, 0, 0), (4, 0, 3), (0, 0, 3)),
            firstOpenings);
        Surface second = new(
            Entity("SURFACE-INTERZONE-B"),
            "Interzone Wall B",
            SurfaceType.Wall,
            Opaque("Interzone Wall Assembly B", 3),
            SurfaceBoundary.Outdoors,
            Polygon((0, 0, 0), (0, 0, 3), (4, 0, 3), (4, 0, 0)),
            secondOpenings);
        SurfaceAdjacencyPair pair = SurfaceAdjacency.Match(first, second);
        Zone zoneA = CreateZone("ZONE-INTERZONE-A", "Interzone Parent Zone A", pair.First);
        Zone zoneB = CreateZone("ZONE-INTERZONE-B", "Interzone Parent Zone B", pair.Second);
        return ScenarioFor(2, new[] { zoneA, zoneB }, new[] { pair.First, pair.Second });
    }

    private static Scenario CreateRoofScenario()
    {
        Surface surface = new(
            Entity("SURFACE-ROOF"),
            "Outdoor Ceiling Becomes Roof",
            SurfaceType.Ceiling,
            Opaque("Outdoor Roof Assembly", 4),
            SurfaceBoundary.Outdoors,
            Polygon((0, 0, 3.2), (0, 4, 3.2), (6, 4, 3.2), (6, 0, 3.2)));
        Zone zone = CreateZone("ZONE-ROOF", "Outdoor Roof Parent Zone", surface);
        return ScenarioFor(3, new[] { zone }, new[] { surface });
    }

    private static Scenario CreateOutdoorOpeningsScenario()
    {
        var glazing = new Glazing("Outdoor Multi Glazing", 1.35, 0.38);
        var doorConstruction = new NoMassConstruction("Outdoor Door Assembly", 2.1);
        var blind = new Blind("Strong Interior Blind", 0.025, 0.02, 45, 0.62, 0.55);
        var shade = new Shade("Simple Interior Shade", 0.12, 0.48);
        IOpening[] openings =
        {
            new Window(Entity("OUT-WINDOW-1"), "Outdoor Blind Window", glazing, VerticalRectangle(0.02, 1.2, 0.5, 1), blind),
            new Window(Entity("OUT-WINDOW-2"), "Outdoor Shade Window", glazing, VerticalRectangle(1.26, 1.6, 0.5, 1), shade),
            new Window(Entity("OUT-WINDOW-3"), "Outdoor Clear Window", glazing, VerticalRectangle(2.9, 0.9, 0.5, 1)),
            new Door(Entity("OUT-DOOR-1"), "Outdoor Door 1", doorConstruction, VerticalRectangle(3.84, 1.9, 0.5, 1)),
            new Door(Entity("OUT-DOOR-2"), "Outdoor Door 2", doorConstruction, VerticalRectangle(5.78, 2.2, 0.5, 1)),
        };
        Surface surface = new(
            Entity("SURFACE-OUTDOOR"),
            "Outdoor Multi-Opening Wall",
            SurfaceType.Wall,
            Opaque("Outdoor Wall Assembly", 5),
            SurfaceBoundary.Outdoors,
            Polygon((0, 0, 0), (8, 0, 0), (8, 0, 3.5), (0, 0, 3.5)),
            openings);
        Zone zone = CreateZone("ZONE-OUTDOOR", "Outdoor Openings Parent Zone", surface);
        return ScenarioFor(4, new[] { zone }, new[] { surface });
    }

    private static Scenario ScenarioFor(int index, Zone[] zones, Surface[] surfaces) =>
        new(
            ExpectedCases[index],
            new EnergyModel("Surface oracle model " + index, zones),
            zones,
            surfaces);

    private static Zone CreateZone(string id, string name, params Surface[] surfaces) =>
        new(
            Entity(id),
            name,
            surfaces,
            new ZoneProfile(Entity("PROFILE-" + id), name + " Profile"));

    private static OpaqueConstruction Opaque(string name, int index)
    {
        var material = new Material("Native material " + index, 1.4, 2_200, 880);
        return new OpaqueConstruction(
            name,
            new[] { new Layer("Native layer " + index, material, 0.2) });
    }

    private static PlanarPolygon Polygon(params (double X, double Y, double Z)[] values) =>
        new(values.Select(value => new Vertex(value.X, value.Y, value.Z)));

    private static PlanarPolygon VerticalRectangle(
        double x,
        double width,
        double z,
        double height) => Polygon(
            (x, 0, z),
            (x + width, 0, z),
            (x + width, 0, z + height),
            (x, 0, z + height));

    private static EntityId Entity(string value) => new(value);

    private static EnergyModelIdfOptions CreateOptions() => new()
    {
        AddIdealLoadsForUnassignedZones = false,
        UseLegacyRectangularFenestration = true,
    };

    private static NativeObservation ExecuteNativeCase(
        CaseBinding binding,
        JsonElement pythonFacts,
        Scenario scenario,
        OfficialIddOracle idd)
    {
        Assert.True(scenario.Model.Validate().IsValid);
        GraphSnapshot before = GraphSnapshot.Capture(scenario);
        IdfDocument first = scenario.Model.ToIdfDocument(options: CreateOptions());
        before.AssertUnchanged(scenario);
        IdfDocument second = scenario.Model.ToIdfDocument(options: CreateOptions());
        before.AssertUnchanged(scenario);

        Assert.NotSame(first, second);
        Assert.Equal(IdfWriter.Write(first), IdfWriter.Write(second));
        Assert.Equal(first.Count, second.Count);
        for (int objectIndex = 0; objectIndex < first.Count; objectIndex++)
        {
            Assert.NotSame(first[objectIndex], second[objectIndex]);
            Assert.Equal(ObjectFingerprint(first[objectIndex]), ObjectFingerprint(second[objectIndex]));
            Assert.Equal(first[objectIndex].Fields.Count, second[objectIndex].Fields.Count);
            for (int fieldIndex = 0; fieldIndex < first[objectIndex].Fields.Count; fieldIndex++)
            {
                Assert.NotSame(first[objectIndex].Fields[fieldIndex], second[objectIndex].Fields[fieldIndex]);
            }
        }

        JsonElement[] records = pythonFacts.GetProperty("emission")
            .GetProperty("first_object_records")
            .EnumerateArray()
            .ToArray();
        var targets = new List<IdfObject>();
        var omissionSpans = new List<OmissionSpan>();
        var lexicalDifferences = new List<LexicalDifference>();
        var valueDifferences = new List<ValueDifference>();
        int officialFieldsCompared = 0;
        for (int objectIndex = 0; objectIndex < records.Length; objectIndex++)
        {
            JsonElement record = records[objectIndex];
            IdfObject target = FindNativeTargetObject(first, record);
            targets.Add(target);
            ComparisonAnalysis analysis = CompareObject(
                binding,
                objectIndex,
                record,
                target,
                idd);
            omissionSpans.AddRange(analysis.OmissionSpans);
            lexicalDifferences.AddRange(analysis.LexicalDifferences);
            valueDifferences.AddRange(analysis.ValueDifferences);
            officialFieldsCompared += analysis.OfficialFieldsCompared;
        }

        Assert.Equal(binding.PythonFieldCounts.Sum(), officialFieldsCompared);
        string[] pythonTargetOrder = records.Select(PythonObjectIdentity).ToArray();
        string[] nativeTargetOrder = targets
            .OrderBy(item => IndexOf(first, item))
            .Select(ObjectIdentity)
            .ToArray();
        int start = first
            .Select((item, index) => new { item, index })
            .First(value => value.item.ObjectType == "Zone"
                && value.item.Name == scenario.Zones[0].Name)
            .index;
        int end = targets.Max(item => IndexOf(first, item));
        string[] nativeSliceOrder = first
            .Skip(start)
            .Take(end - start + 1)
            .Select(ObjectIdentity)
            .ToArray();
        int omittedFieldCount = omissionSpans.Sum(item => item.EndInclusive - item.StartInclusive + 1);
        int[] differenceCounts =
        {
            omittedFieldCount,
            omissionSpans.Count,
            lexicalDifferences.Count,
            valueDifferences.Count,
        };
        int[] compactFieldCounts = targets.Select(item => item.Count).ToArray();
        string[][] targetFieldValues = targets
            .Select(item => item.Fields.Select(field => field.Value).ToArray())
            .ToArray();
        string[] targetTypes = targets.Select(item => item.ObjectType).ToArray();
        string[] targetNames = targets.Select(item => item.Name ?? string.Empty).ToArray();
        string documentSha256 = Sha256(Encoding.UTF8.GetBytes(IdfWriter.Write(first)));
        string targetSha256 = CanonicalSha256(JsonSerializer.SerializeToElement(
            targets.Select(item => new
            {
                object_type = item.ObjectType,
                name = item.Name,
                fields = item.Fields.Select(field => field.Value).ToArray(),
            }).ToArray()));
        string differenceSha256 = CanonicalSha256(JsonSerializer.SerializeToElement(new
        {
            omission_spans = omissionSpans,
            lexical_differences = lexicalDifferences,
            value_differences = valueDifferences,
            python_target_order = pythonTargetOrder,
            native_target_order = nativeTargetOrder,
            native_slice_order = nativeSliceOrder,
        }));

        return new NativeObservation(
            binding.CaseId,
            records.Length,
            officialFieldsCompared,
            first.Count,
            compactFieldCounts,
            differenceCounts,
            documentSha256,
            targetSha256,
            differenceSha256,
            pythonTargetOrder,
            nativeTargetOrder,
            nativeSliceOrder,
            targetTypes,
            targetNames,
            targetFieldValues,
            omissionSpans.ToArray(),
            lexicalDifferences.ToArray(),
            valueDifferences.ToArray(),
            new[]
            {
                "public-route=EnergyModel.ToIdfDocument",
                "legacy-rectangular-fenestration=true",
                "repeated-call=document-object-field-fresh-and-byte-deterministic",
                "source-state=captured-references-and-selected-surface-vertices/opening-areas/names/types/boundary-adjacency-state-unchanged",
                "official-IDD=EnergyPlus-24.2-all-python-fields-by-position",
                "child-converters-and-classes=context-only-not-closed",
            });
    }

    private static ComparisonAnalysis CompareObject(
        CaseBinding binding,
        int objectIndex,
        JsonElement record,
        IdfObject actual,
        OfficialIddOracle idd)
    {
        string objectType = RequiredString(record, "object_type");
        string identity = PythonObjectIdentity(record);
        Assert.Equal(objectType, actual.ObjectType);
        OfficialIddObject official = idd[objectType];
        JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
        Assert.True(actual.Count <= fields.Length);
        var omissions = new List<OmissionSpan>();
        var lexical = new List<LexicalDifference>();
        var values = new List<ValueDifference>();
        string? currentOmissionKind = null;
        int currentOmissionStart = -1;

        void FlushOmission(int endExclusive)
        {
            if (currentOmissionKind is null)
            {
                return;
            }

            omissions.Add(new OmissionSpan(
                objectIndex,
                identity,
                currentOmissionStart,
                endExclusive - 1,
                currentOmissionKind));
            currentOmissionKind = null;
            currentOmissionStart = -1;
        }

        for (int position = 0; position < fields.Length; position++)
        {
            JsonElement expectedField = fields[position];
            OfficialIddField officialField = official.ResolvePrototype(position);
            Assert.Equal(official.ResolveFieldName(position), RequiredString(expectedField, "name"));
            JsonElement expected = expectedField.GetProperty("value");
            if (position >= actual.Count)
            {
                string omissionKind;
                if (RequiredString(expected, "kind") == "none")
                {
                    omissionKind = "native-compact-trailing-None";
                }
                else
                {
                    Assert.NotNull(officialField.DefaultValue);
                    AssertScalarEquivalent(expected, officialField.DefaultValue!, 0, objectType, position);
                    omissionKind = "native-compact-trailing-official-IDD-default";
                }

                if (!StringComparer.Ordinal.Equals(currentOmissionKind, omissionKind))
                {
                    FlushOmission(position);
                    currentOmissionKind = omissionKind;
                    currentOmissionStart = position;
                }

                continue;
            }

            FlushOmission(position);
            string nativeValue = actual[position];
            string kind = RequiredString(expected, "kind");
            if (kind == "none")
            {
                Assert.Equal(string.Empty, nativeValue);
                continue;
            }

            if (binding == ExpectedCases[0]
                && objectType == "BuildingSurface:Detailed"
                && position == 2)
            {
                Assert.Equal("DefaultAirBoundary", RequiredString(expected, "value"));
                Assert.Equal("Custom Transfer Air Boundary", nativeValue);
                values.Add(new ValueDifference(
                    objectIndex,
                    identity,
                    position,
                    officialField.Name,
                    "DefaultAirBoundary",
                    nativeValue,
                    "native-correct-custom-AirBoundary-reference-versus-upstream-dangling-DefaultAirBoundary"));
                continue;
            }

            ScalarComparison comparison = AssertScalarEquivalent(
                expected,
                nativeValue,
                1e-12,
                objectType,
                position);
            if (comparison.Classification is not null)
            {
                lexical.Add(new LexicalDifference(
                    objectIndex,
                    identity,
                    position,
                    officialField.Name,
                    comparison.PythonText,
                    nativeValue,
                    comparison.Classification));
            }
        }

        FlushOmission(fields.Length);
        return new ComparisonAnalysis(
            fields.Length,
            omissions.ToArray(),
            lexical.ToArray(),
            values.ToArray());
    }

    private static ScalarComparison AssertScalarEquivalent(
        JsonElement expected,
        string nativeValue,
        double tolerance,
        string objectType,
        int position)
    {
        string kind = RequiredString(expected, "kind");
        if (kind == "str")
        {
            string text = RequiredString(expected, "value");
            Assert.True(
                StringComparer.Ordinal.Equals(text, nativeValue),
                $"String mismatch for {objectType}[{position}]: Python='{text}', native='{nativeValue}'");
            return new ScalarComparison(text, null);
        }

        if (kind == "bool")
        {
            bool value = expected.GetProperty("value").GetBoolean();
            string token = value ? "Yes" : "No";
            Assert.Equal(token, nativeValue);
            return new ScalarComparison(value ? "true" : "false", "python-bool-to-native-IDD-Yes-No-token");
        }

        string pythonText = kind == "int"
            ? RequiredString(expected, "value")
            : RequiredString(expected, "repr");
        double pythonNumber = double.Parse(pythonText, NumberStyles.Float, CultureInfo.InvariantCulture);
        Assert.True(
            double.TryParse(nativeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double nativeNumber),
            $"Native numeric field is not numeric for {objectType}[{position}]: {nativeValue}");
        double allowed = tolerance * Math.Max(1d, Math.Max(Math.Abs(pythonNumber), Math.Abs(nativeNumber)));
        Assert.True(
            Math.Abs(pythonNumber - nativeNumber) <= allowed,
            $"Numeric mismatch for {objectType}[{position}]: Python={pythonText}, native={nativeValue}");
        if (StringComparer.Ordinal.Equals(pythonText, nativeValue))
        {
            return new ScalarComparison(pythonText, null);
        }

        string classification = pythonNumber.Equals(nativeNumber)
            ? "numeric-lexical-format-only"
            : "native-opening-polygon-area-roundoff-within-1e-12";
        return new ScalarComparison(pythonText, classification);
    }

    private static IdfObject FindNativeTargetObject(IdfDocument document, JsonElement record)
    {
        string type = RequiredString(record, "object_type");
        JsonElement nameField = record.GetProperty("ordered_fields")[0];
        Assert.Equal("Name", RequiredString(nameField, "name"));
        string name = RequiredString(nameField.GetProperty("value"), "value");
        return Assert.Single(document[type], item => item.Name == name);
    }

    private static string PythonObjectIdentity(JsonElement record)
    {
        string type = RequiredString(record, "object_type");
        string name = RequiredString(
            record.GetProperty("ordered_fields")[0].GetProperty("value"),
            "value");
        return type + "|" + name;
    }

    private static string ObjectIdentity(IdfObject value) =>
        value.ObjectType + "|" + (value.Name ?? string.Empty);

    private static int IndexOf(IdfDocument document, IdfObject target)
    {
        for (int index = 0; index < document.Count; index++)
        {
            if (ReferenceEquals(document[index], target))
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException("Native target object was not found by reference.");
    }

    private static string ObjectFingerprint(IdfObject value) =>
        JsonSerializer.Serialize(new
        {
            object_type = value.ObjectType,
            fields = value.Fields.Select(field => field.Value).ToArray(),
        });

    private static void AssertReciprocalOpeningLinks(NativeObservation observation)
    {
        string[] expected =
        {
            "Interzone B Window 1", "Interzone B Window 2",
            "Interzone B Door 1", "Interzone B Door 2",
            "Interzone A Window 1", "Interzone A Window 2",
            "Interzone A Door 1", "Interzone A Door 2",
        };
        int[] indices = { 0, 1, 2, 3, 5, 6, 7, 8 };
        Assert.Equal(expected, indices.Select(index => observation.TargetFieldValues[index][3]));
        Assert.Equal("Interzone Wall B", observation.TargetFieldValues[4][6]);
        Assert.Equal("Interzone Wall A", observation.TargetFieldValues[9][6]);
    }

    private static void AssertRoofGroundAndShadingLinks(IReadOnlyList<NativeObservation> observations)
    {
        Assert.Equal("Custom Transfer Air Boundary", observations[0].TargetFieldValues[0][2]);
        Assert.Equal("Floor", observations[1].TargetFieldValues[0][1]);
        Assert.Equal(
            new[]
            {
                "0.0", "0.0", "0.0", "4.0", "0.0", "0.0", "5.0", "2.0", "0.0",
                "2.0", "4.0", "0.0", "0.0", "2.0", "0.0",
            },
            observations[1].TargetFieldValues[0].Skip(11));
        Assert.Equal("Roof", observations[3].TargetFieldValues[0][1]);
        Assert.Equal("Outdoor Blind Window", observations[4].TargetFieldValues[6][16]);
        Assert.Equal("Strong Interior Blind", observations[4].TargetFieldValues[6][10]);
        Assert.Equal("InteriorBlind", observations[4].TargetFieldValues[6][3]);
        Assert.Equal("Outdoor Shade Window", observations[4].TargetFieldValues[8][16]);
        Assert.Equal("Simple Interior Shade", observations[4].TargetFieldValues[8][10]);
        Assert.Equal("InteriorShade", observations[4].TargetFieldValues[8][3]);
    }

    private static void ValidatePinnedArtifactsAndNativeRoute()
    {
        AssertPinnedArtifact(GeneratorRepositoryPath, GeneratorByteLength, GeneratorSha256);
        AssertPinnedArtifact(PythonValidatorRepositoryPath, PythonValidatorByteLength, PythonValidatorSha256);
        AssertPinnedArtifact(PublicRepositoryPath, PublicByteLength, PublicSha256);
        AssertPinnedArtifact(ImplementationRepositoryPath, ImplementationByteLength, ImplementationSha256);
        AssertPinnedArtifact(InventoryRepositoryPath, InventoryByteLength, InventoryFileSha256);

        MethodInfo publicMethod = Assert.Single(
            typeof(EnergyModel).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            candidate => candidate.Name == nameof(EnergyModel.ToIdfDocument));
        Assert.Equal(PublicSymbol, MethodSymbol(publicMethod));
        Assert.Equal(typeof(IdfDocument), publicMethod.ReturnType);
        Assert.Equal(new[] { "schema", "options" }, publicMethod.GetParameters().Select(item => item.Name));

        Type assembler = typeof(EnergyModel).Assembly.GetType(
            "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler",
            throwOnError: true)!;
        AssertPrivateStaticMethod(
            assembler,
            "AppendConstructionsAndGeometry",
            typeof(void),
            "document", "context", "model", "options");
        AssertPrivateStaticMethod(
            assembler,
            "BuildingSurface",
            typeof(IdfObject),
            "context", "zone", "surface", "constructionName", "surfacesById");
        AssertPrivateStaticMethod(
            assembler,
            "LegacyRectangularFenestration",
            typeof(IdfObject),
            "context", "host", "opening", "constructionName", "surfacesById");
        AssertPrivateStaticMethod(
            assembler,
            "AppendWindowShading",
            typeof(void),
            "document", "context", "zone", "window", "materialDefinitions");
    }

    private static void AssertPrivateStaticMethod(
        Type type,
        string name,
        Type returnType,
        params string[] parameters)
    {
        MethodInfo method = Assert.Single(
            type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            candidate => candidate.Name == name);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(parameters, method.GetParameters().Select(item => item.Name));
    }

    private static OfficialIddOracle LoadOfficialIddOracle()
    {
        byte[] compressed = File.ReadAllBytes(FindRepositoryFile(IddOracleRepositoryPath));
        Assert.Equal(IddOracleByteLength, compressed.Length);
        Assert.Equal(IddOracleSha256, Sha256(compressed));
        using var input = new MemoryStream(compressed, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using JsonDocument document = JsonDocument.Parse(gzip);
        JsonElement root = document.RootElement;
        AssertUniqueObjectKeysRecursive(root);
        Assert.Equal(IddOracleSchema, RequiredString(root, "oracle_schema"));
        Assert.Equal(UpstreamCommit, RequiredString(root, "upstream_commit"));
        Assert.Equal(EnergyPlusVersion, RequiredString(root, "energyplus_version"));
        Assert.Equal(EnergyPlusBuild, RequiredString(root, "energyplus_build"));
        Assert.Equal(EnergyPlusIddSourceSha256, RequiredString(root, "source_sha256"));
        Assert.Equal(EnergyPlusIddSourceByteLength, root.GetProperty("source_bytes").GetInt32());
        Assert.Equal(848, root.GetProperty("object_count").GetInt32());
        Assert.Equal(13_702, root.GetProperty("field_count").GetInt32());
        JsonElement[] objects = root.GetProperty("objects").EnumerateArray().ToArray();
        var selected = new List<OfficialIddObject>();
        foreach (string objectType in SelectedObjectTypes)
        {
            JsonElement item = Assert.Single(objects, candidate => RequiredString(candidate, "name") == objectType);
            selected.Add(ParseOfficialIddObject(item));
        }

        Assert.Equal(new[] { 14, 9, 9, 9, 8, 29, 17, 15 }, selected.Select(item => item.Fields.Length));
        Assert.Equal(new int?[] { 11, null, null, null, null, null, 16, null }, selected.Select(item => item.ExtensibleStartIndex));
        Assert.Equal(new[] { 3, 0, 0, 0, 0, 0, 1, 0 }, selected.Select(item => item.ExtensibleGroupSize));
        return new OfficialIddOracle(selected);
    }

    private static OfficialIddObject ParseOfficialIddObject(JsonElement item)
    {
        JsonElement[] fields = item.GetProperty("fields").EnumerateArray().ToArray();
        OfficialIddField[] parsed = fields.Select((field, index) =>
        {
            Assert.Equal(index, field.GetProperty("position").GetInt32());
            JsonElement defaultValue = field.GetProperty("default_value");
            return new OfficialIddField(
                index,
                RequiredString(field, "name"),
                defaultValue.ValueKind == JsonValueKind.Null ? null : defaultValue.GetString());
        }).ToArray();
        JsonElement extensibleStart = item.GetProperty("extensible_start_index");
        Assert.True(extensibleStart.ValueKind is JsonValueKind.Null or JsonValueKind.Number);
        return new OfficialIddObject(
            RequiredString(item, "name"),
            extensibleStart.ValueKind == JsonValueKind.Null ? null : extensibleStart.GetInt32(),
            item.GetProperty("extensible_group_size").GetInt32(),
            parsed);
    }

    private static object ToDiscoveryPin(NativeObservation item) => new
    {
        case_id = item.CaseId,
        document_count = item.DocumentCount,
        compact_field_counts = item.CompactFieldCounts,
        difference_counts = item.DifferenceCounts,
        document_sha256 = item.DocumentSha256,
        target_sha256 = item.TargetSha256,
        difference_sha256 = item.DifferenceSha256,
        native_target_order = item.NativeTargetOrder,
        native_slice_order = item.NativeSliceOrder,
        difference_array = item.DifferenceArray,
    };

    private static void AssertNativePin(NativePin expected, NativeObservation actual)
    {
        Assert.Equal(expected.DocumentCount, actual.DocumentCount);
        Assert.Equal(expected.CompactFieldCounts, actual.CompactFieldCounts);
        Assert.Equal(expected.DifferenceCounts, actual.DifferenceCounts);
        Assert.Equal(expected.DocumentSha256, actual.DocumentSha256);
        Assert.Equal(expected.TargetSha256, actual.TargetSha256);
        Assert.Equal(expected.DifferenceSha256, actual.DifferenceSha256);
        Assert.Equal(expected.NativeTargetOrder, actual.NativeTargetOrder);
        Assert.Equal(expected.NativeSliceOrder, actual.NativeSliceOrder);
        Assert.Equal(expected.DifferenceArray, actual.DifferenceArray);
    }

    private static object CreateReceipt(IReadOnlyList<NativeObservation> observations) => new
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
            generator = Artifact(GeneratorRepositoryPath, GeneratorByteLength, GeneratorSha256),
            python_validator = Artifact(PythonValidatorRepositoryPath, PythonValidatorByteLength, PythonValidatorSha256),
            public_inventory = Artifact(InventoryRepositoryPath, InventoryByteLength, InventoryFileSha256),
            public_route = Artifact(PublicRepositoryPath, PublicByteLength, PublicSha256),
            implementation = Artifact(ImplementationRepositoryPath, ImplementationByteLength, ImplementationSha256),
            official_idd = new
            {
                compressed_byte_length = IddOracleByteLength,
                compressed_sha256 = IddOracleSha256,
                energyplus_build = EnergyPlusBuild,
                energyplus_version = EnergyPlusVersion,
                official_source_byte_length = EnergyPlusIddSourceByteLength,
                official_source_sha256 = "sha256:" + EnergyPlusIddSourceSha256,
                path = IddOracleRepositoryPath,
                schema = IddOracleSchema,
            },
        },
        native_binding = new
        {
            adaptation_id = AdaptationId,
            assertion_id = AssertionId,
            classification = "exception",
            native_target = "EnergyModel.ToIdfDocument with UseLegacyRectangularFenestration=true",
            public_symbol = PublicSymbol,
            implementation_symbols = new[]
            {
                "EnergyModelIdfAssembler.AppendConstructionsAndGeometry",
                "EnergyModelIdfAssembler.BuildingSurface",
                "EnergyModelIdfAssembler.LegacyRectangularFenestration",
                "EnergyModelIdfAssembler.AppendWindowShading",
            },
        },
        observations = observations.Select(item => new
        {
            adaptation_id = AdaptationId,
            case_id = item.CaseId,
            compact_field_counts = item.CompactFieldCounts,
            difference_array = item.DifferenceArray,
            difference_counts = item.DifferenceCounts,
            difference_sha256 = item.DifferenceSha256,
            document_count = item.DocumentCount,
            document_sha256 = item.DocumentSha256,
            lexical_differences = item.LexicalDifferences,
            native_facts = item.NativeFacts,
            native_outcome = "returned",
            native_slice_order = item.NativeSliceOrder,
            native_target_order = item.NativeTargetOrder,
            omission_spans = item.OmissionSpans,
            python_field_count = item.PythonFieldCount,
            python_object_count = item.PythonObjectCount,
            python_target_order = item.PythonTargetOrder,
            target_sha256 = item.TargetSha256,
            value_differences = item.ValueDifferences,
        }).ToArray(),
        representation = new
        {
            comparison = "all-2437-Python-fields-by-official-EnergyPlus-24.2-IDD-position",
            compact_omission_policy = "consecutive-trailing-None-or-independently-verified-official-IDD-default-spans",
            captured_source_state = "reference-identities-plus-model/zone/profile/surface/construction/opening/shading-names-surface-types-boundary-adjacency-surface-vertex-coordinates-and-opening-areas-unchanged-across-two-calls",
            definition_and_model_relocation = "native-slice-order-retains-Zone-and-definition-objects-in-parent-assembly-without-reordering-Python-records",
            numeric_policy = "exact-text-or-explicit-lexical/1e-12-roundoff-difference-entry",
            upstream_air_boundary_defect = "Python-host-references-DefaultAirBoundary-while-native-emits-and-references-custom-name",
            total_difference_counts = new[]
            {
                observations.Sum(item => item.DifferenceCounts[0]),
                observations.Sum(item => item.DifferenceCounts[1]),
                observations.Sum(item => item.DifferenceCounts[2]),
                observations.Sum(item => item.DifferenceCounts[3]),
            },
            total_official_fields_compared = observations.Sum(item => item.PythonFieldCount),
            total_python_objects = observations.Sum(item => item.PythonObjectCount),
        },
        scope = new
        {
            context_only_not_targeted = new[]
            {
                "Surface.__init__", "Vertex", "SurfaceBoundaryCondition", "SurfaceType",
                "Window.__init__", "Window.to_idf_object", "Door.__init__", "Door.to_idf_object",
                "Blind.__init__", "Blind.to_idf_object", "Shade.__init__", "Shade.to_idf_object",
                "Construction.__init__", "AirBoundary.__init__", "Glazing.__init__",
                "NoMassConstruction.__init__", "Zone.name", "IdfObject.__init__",
            },
            full_symbol_closure = false,
            source_state_not_claimed = new[]
            {
                "material-layer-thermophysical-values",
                "glazing-and-shading-numeric-properties",
                "opening-vertex-coordinate-values-beyond-the-captured-area",
                "zone-profile-settings-beyond-the-profile-name",
                "HVAC-and-ventilation-assignment-contents-or-settings",
                "EnergyModel-settings-beyond-the-model-name-and-captured-collection-references",
            },
            target_symbol = UpstreamSymbol,
            unresolved_behavior = new[]
            {
                "child-converter-and-class-closure",
                "invalid-domain-and-error-semantics",
                "native-default-detailed-fenestration-route",
                "EnergyModel-global-deduplication-and-conflict-policy-beyond-five-scenarios",
            },
        },
        upstream = new
        {
            ast_sha256 = UpstreamAstSha256,
            body_hash = BodyHash,
            inventory_index = InventoryIndex,
            inventory_sha256 = InventoryContentSha256,
            path = UpstreamPath,
            signature_hash = SignatureHash,
            source_sha256 = UpstreamSourceSha256,
            symbol = UpstreamSymbol,
            symbol_hash = SymbolHash,
        },
    };

    private static object Artifact(string path, int byteLength, string sha256) => new
    {
        byte_length = byteLength,
        path,
        sha256,
    };

    private static void ValidateReceipt(JsonElement value, IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(value);
        AssertNoUnsafeIdentity(value);
        AssertNoHostPaths(value);
        AssertNoNonFiniteJsonNumbers(value);
        AssertKeys(value, "artifacts", "native_binding", "observations", "representation", "scope", "upstream");
        JsonElement binding = value.GetProperty("native_binding");
        Assert.Equal(AdaptationId, RequiredString(binding, "adaptation_id"));
        Assert.Equal(AssertionId, RequiredString(binding, "assertion_id"));
        Assert.Equal("exception", RequiredString(binding, "classification"));
        JsonElement[] actual = value.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(observations.Select(item => item.CaseId), actual.Select(item => RequiredString(item, "case_id")));
        Assert.All(actual, item => Assert.Equal("returned", RequiredString(item, "native_outcome")));
        JsonElement representation = value.GetProperty("representation");
        Assert.Equal(23, representation.GetProperty("total_python_objects").GetInt32());
        Assert.Equal(2_437, representation.GetProperty("total_official_fields_compared").GetInt32());
        JsonElement upstream = value.GetProperty("upstream");
        Assert.Equal(InventoryIndex, upstream.GetProperty("inventory_index").GetInt32());
        Assert.Equal(UpstreamSymbol, RequiredString(upstream, "symbol"));
        Assert.Equal(SymbolHash, RequiredString(upstream, "symbol_hash"));
    }

    private static void AssertIndependentScenarioGraphs(IReadOnlyList<Scenario> scenarios)
    {
        Assert.Equal(
            scenarios.Count,
            scenarios.Select(item => item.Model).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(
            scenarios.Sum(item => item.Zones.Length),
            scenarios.SelectMany(item => item.Zones).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(
            scenarios.Sum(item => item.TargetSurfaces.Length),
            scenarios.SelectMany(item => item.TargetSurfaces).Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    private static void ValidateEncodedScalar(JsonElement value)
    {
        string kind = RequiredString(value, "kind");
        switch (kind)
        {
            case "none":
                AssertKeys(value, "kind");
                break;
            case "bool":
                AssertKeys(value, "kind", "value");
                Assert.True(value.GetProperty("value").ValueKind is JsonValueKind.True or JsonValueKind.False);
                break;
            case "int":
            case "str":
                AssertKeys(value, "kind", "value");
                Assert.Equal(JsonValueKind.String, value.GetProperty("value").ValueKind);
                break;
            case "float":
                AssertKeys(value, "hex", "kind", "repr");
                double decoded = double.Parse(RequiredString(value, "repr"), NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.True(double.IsFinite(decoded));
                Assert.StartsWith("0x", RequiredString(value, "hex"), StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new Xunit.Sdk.XunitException("Unexpected encoded scalar kind: " + kind);
        }
    }

    private static void AssertEncodedDouble(JsonElement value, double actual, double tolerance = 0)
    {
        Assert.Equal("float", RequiredString(value, "kind"));
        double expected = double.Parse(
            RequiredString(value, "repr"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        if (tolerance == 0)
        {
            Assert.Equal(expected, actual);
        }
        else
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance);
        }
    }

    private static void AssertAllTrue(JsonElement values) =>
        Assert.All(values.EnumerateArray(), item => Assert.True(item.GetBoolean()));

    private static void AssertIntArray(JsonElement values, IEnumerable<int> expected) =>
        Assert.Equal(expected, values.EnumerateArray().Select(item => item.GetInt32()));

    private static void AssertPinnedArtifact(string path, int expectedBytes, string expectedSha256)
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(expectedBytes, bytes.Length);
        Assert.Equal(expectedSha256, Sha256(bytes));
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

    private static string MethodSymbol(MethodInfo method) =>
        method.DeclaringType!.FullName + "." + method.Name;

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string CanonicalSha256(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
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
                throw new InvalidOperationException("Unsupported canonical JSON value kind.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            JsonProperty[] properties = value.EnumerateObject().ToArray();
            Assert.Equal(properties.Length, properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
            foreach (JsonProperty property in properties)
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

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            Assert.False(Regex.IsMatch(text, @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])", RegexOptions.CultureInvariant));
            Assert.False(Regex.IsMatch(text, @"(?i)(?<![0-9a-f])[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?![0-9a-f])", RegexOptions.CultureInvariant));
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
            Assert.False(Regex.IsMatch(
                value.GetString()!,
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
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
    }

    private static void AssertStringArray(JsonElement value, IEnumerable<string> expected) =>
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));

    private sealed record CaseBinding(
        string CaseId,
        string FactSha256,
        string[] PythonObjectTypes,
        int[] PythonFieldCounts,
        NativePin Native);

    private sealed record NativePin(
        int DocumentCount,
        int[] CompactFieldCounts,
        int[] DifferenceCounts,
        string DocumentSha256,
        string TargetSha256,
        string DifferenceSha256,
        string[] NativeTargetOrder,
        string[] NativeSliceOrder,
        string[] DifferenceArray)
    {
        public static NativePin Discovery { get; } = new(
            0,
            Array.Empty<int>(),
            Array.Empty<int>(),
            string.Empty,
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private sealed record Scenario(
        CaseBinding Binding,
        EnergyModel Model,
        Zone[] Zones,
        Surface[] TargetSurfaces);

    private sealed record OmissionSpan(
        int ObjectIndex,
        string ObjectIdentity,
        int StartInclusive,
        int EndInclusive,
        string Classification);

    private sealed record LexicalDifference(
        int ObjectIndex,
        string ObjectIdentity,
        int Position,
        string FieldName,
        string PythonText,
        string NativeText,
        string Classification);

    private sealed record ValueDifference(
        int ObjectIndex,
        string ObjectIdentity,
        int Position,
        string FieldName,
        string PythonValue,
        string NativeValue,
        string Classification);

    private sealed record ScalarComparison(string PythonText, string? Classification);

    private sealed record ComparisonAnalysis(
        int OfficialFieldsCompared,
        OmissionSpan[] OmissionSpans,
        LexicalDifference[] LexicalDifferences,
        ValueDifference[] ValueDifferences);

    private sealed record NativeObservation(
        string CaseId,
        int PythonObjectCount,
        int PythonFieldCount,
        int DocumentCount,
        int[] CompactFieldCounts,
        int[] DifferenceCounts,
        string DocumentSha256,
        string TargetSha256,
        string DifferenceSha256,
        string[] PythonTargetOrder,
        string[] NativeTargetOrder,
        string[] NativeSliceOrder,
        string[] TargetObjectTypes,
        string[] TargetObjectNames,
        string[][] TargetFieldValues,
        OmissionSpan[] OmissionSpans,
        LexicalDifference[] LexicalDifferences,
        ValueDifference[] ValueDifferences,
        string[] NativeFacts)
    {
        public string[] DifferenceArray => OmissionSpans
            .Select(item => $"omit|{item.ObjectIdentity}|{item.StartInclusive}-{item.EndInclusive}|{item.Classification}")
            .Concat(LexicalDifferences.Select(item =>
                $"lexical|{item.ObjectIdentity}|{item.Position}|{item.PythonText}|{item.NativeText}|{item.Classification}"))
            .Concat(ValueDifferences.Select(item =>
                $"value|{item.ObjectIdentity}|{item.Position}|{item.PythonValue}|{item.NativeValue}|{item.Classification}"))
            .ToArray();
    }

    private sealed record OfficialIddField(int Position, string Name, string? DefaultValue);

    private sealed record OfficialIddObject(
        string Name,
        int? ExtensibleStartIndex,
        int ExtensibleGroupSize,
        OfficialIddField[] Fields)
    {
        public OfficialIddField ResolvePrototype(int position)
        {
            if (position < Fields.Length)
            {
                return Fields[position];
            }

            Assert.True(ExtensibleStartIndex is not null && ExtensibleGroupSize > 0);
            int prototypePosition = ExtensibleStartIndex!.Value
                + ((position - ExtensibleStartIndex.Value) % ExtensibleGroupSize);
            return Fields[prototypePosition];
        }

        public string ResolveFieldName(int position)
        {
            OfficialIddField prototype = ResolvePrototype(position);
            if (ExtensibleStartIndex is null || position < ExtensibleStartIndex.Value)
            {
                return prototype.Name;
            }

            int groupNumber = ((position - ExtensibleStartIndex.Value) / ExtensibleGroupSize) + 1;
            return Regex.Replace(
                prototype.Name,
                @"\b1\b",
                groupNumber.ToString(CultureInfo.InvariantCulture),
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }
    }

    private sealed class OfficialIddOracle
    {
        private readonly IReadOnlyDictionary<string, OfficialIddObject> objects;

        public OfficialIddOracle(IEnumerable<OfficialIddObject> values)
        {
            objects = values.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
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

        private static object[] GraphReferences(Scenario scenario)
        {
            var values = new List<object>
            {
                scenario.Model,
                scenario.Model.Zones,
                scenario.Model.HvacAssignments,
                scenario.Model.VentilationAssignments,
            };
            foreach (Zone zone in scenario.Zones)
            {
                values.Add(zone);
                values.Add(zone.Surfaces);
                values.Add(zone.Profile);
                foreach (Surface surface in zone.Surfaces)
                {
                    values.Add(surface);
                    values.Add(surface.Boundary);
                    values.Add(surface.Polygon);
                    values.Add(surface.Polygon.Vertices);
                    values.Add(surface.Construction);
                    values.Add(surface.Openings);
                    if (surface.Construction is OpaqueConstruction opaque)
                    {
                        values.Add(opaque.Layers);
                        foreach (Layer layer in opaque.Layers)
                        {
                            values.Add(layer);
                            values.Add(layer.Material);
                        }
                    }

                    foreach (IOpening opening in surface.Openings)
                    {
                        values.Add(opening);
                        values.Add(opening.Polygon);
                        values.Add(opening.Polygon.Vertices);
                        if (opening is Window window)
                        {
                            values.Add(window.Glazing);
                            if (window.Shading is not null)
                            {
                                values.Add(window.Shading);
                            }
                        }
                        else
                        {
                            values.Add(((Door)opening).Construction);
                        }
                    }
                }
            }

            return values.ToArray();
        }

        private static string GraphValueFingerprint(Scenario scenario) =>
            JsonSerializer.Serialize(new
            {
                model = scenario.Model.Name,
                zones = scenario.Zones.Select(zone => new
                {
                    zone.Name,
                    profile = zone.Profile.Name,
                    surfaces = zone.Surfaces.Select(surface => new
                    {
                        surface.Name,
                        type = surface.Type.ToString(),
                        construction = surface.Construction.Name,
                        boundary = surface.Boundary.Condition.ToString(),
                        adjacent = surface.Boundary.AdjacentSurfaceId?.Value,
                        vertices = surface.Polygon.Vertices.Select(vertex => new[]
                        {
                            vertex.X.ToString("R", CultureInfo.InvariantCulture),
                            vertex.Y.ToString("R", CultureInfo.InvariantCulture),
                            vertex.Z.ToString("R", CultureInfo.InvariantCulture),
                        }).ToArray(),
                        openings = surface.Openings.Select(opening => new
                        {
                            opening.Name,
                            type = opening.Type.ToString(),
                            area = opening.Polygon.Area.ToString("R", CultureInfo.InvariantCulture),
                            construction = opening is Window window
                                ? window.Glazing.Name
                                : ((Door)opening).Construction.Name,
                            shading = (opening as Window)?.Shading?.Name,
                        }).ToArray(),
                    }).ToArray(),
                }).ToArray(),
            });
    }
}
