using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.UpstreamTracker;
using DragonAirBoundary = Dragons.InvisibleDragon.Construction.AirBoundary;
using DragonConstruction = Dragons.InvisibleDragon.Construction.Construction;
using DragonGlazing = Dragons.InvisibleDragon.Construction.Glazing;
using DragonNoMassConstruction = Dragons.InvisibleDragon.Construction.NoMassConstruction;
using DragonSurface = Dragons.InvisibleDragon.Shape.Surface;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.SimpleDragon.Tests;

public sealed class ConstructionCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-construction-core-oracle.json";
    private const int FixtureBytes = 349_214;
    private const string FixtureSha256 =
        "sha256:d4e9421c40c39dfaef948054798b03fb046fa31d1a5742cb8a53484c87d819f9";
    private const string FixtureSchema = "dragons.python-reference.epsimple-construction-core.v1";
    private const string CasesSha256 =
        "sha256:9046cfba389607b07ceb9308c6962cba74c8550fd1e2557fe453f8144d1b0f92";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_construction_core_oracle.py";
    private const int GeneratorBytes = 107_989;
    private const string GeneratorSha256 =
        "sha256:1b48f4ed06dfbae36685f517563a5991438511208481f4f9db653db7228acdf9";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_construction_core_oracle.py";
    private const int ValidatorBytes = 22_545;
    private const string ValidatorSha256 =
        "sha256:39f940059fd79e5070159b1abc1cd06dbb857ee6d572d86e19c9e33c719d37a4";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/core/construction.py";
    private const int UpstreamBytes = 25_902;
    private const string UpstreamSourceSha256 =
        "sha256:50b784d9c7ebd0df34fb6e524585482f04eb90ef915d5afd125fe779c0620816";
    private const string UpstreamAstSha256 =
        "sha256:fe40c8c89f2c3341ce4972976eabf96edd85ccba55a3a7619ca17e0a7603c0ab";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.ConstructionCoreOracleParityTests.MatchesPinnedConstructionCoreThroughProductionPublicRoutes";

    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/Material.cs", 1_997,
            "sha256:c869470be0b2a1f95ce7ad7cfa3ca32489bb99bed23e3465d0ab426175e8b1f5"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/FenestrationConstruction.cs", 3_962,
            "sha256:15eb1452a5c89bf1e2ce41e1931500b6a329ea6467ac618e2ad6fb139369f5af"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/SurfaceConstruction.cs", 7_278,
            "sha256:0fa371d0fd3c6957ad506b927122c51f3eabb0de32d20d7b1602f118302458b4"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/ConstructionDatabases.cs", 11_476,
            "sha256:8e45c4ba676f60b5b687bf333b7f0c0134577fdb4c2e00f8e9142defc2551f6d"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonDatabase.cs", 2_543,
            "sha256:31bf339ab34fb3e4f65362be0d9519b1d54c44e4b0e46b63e67398873d5fb74a"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonEmbeddedData.cs", 3_131,
            "sha256:76915a821bccc2dbc8e3f185c1faf6c3da07dfe64cd50301b336367d8c5d2d81"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Fenestration.cs", 2_419,
            "sha256:6b71c32871b5468b570b64dfc7389132f4cf0413340add7d16dcf0cb44451a78"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Surface.cs", 7_039,
            "sha256:a26c799796aa042529926b0c7f4052a495a0e84f8b6a21169aa2b24318b6f809"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Zone.cs", 6_664,
            "sha256:82b149ae49fdc188d7947553187e4d5cb496d67087ae2e1f7c4e878a02cdd01b"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_650,
            "sha256:d91f90946ec19602751fc7818484ca43f85d1c46f9905fa805d8ee8a7281d968"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_652,
            "sha256:4048cc4bdfca312a7baae54c7055bb3aa7177ee6a8143ed9ef1d182353df1842"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_343,
            "sha256:0a0774b4461442b2a3cccf68d39fbc236104a2aa13611e0d27c38f27aa2fe5fd"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Model/GreenRetrofitModel.cs", 7_677,
            "sha256:7bf2f7dfb922f4d85982ada0f5622bfbef59dce8cb4d7a90b2759ed6978935ea"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Profiles/ProfileDatabases.cs", 6_756,
            "sha256:73564c26c8ba3ec98e0758fa8528a6a0771d72c268af7a4beb23e5cc7dc6625c"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Weather/WeatherDatabase.cs", 9_463,
            "sha256:c7ddc71015eb375e56565a2898d7998cf865fb50d0c8626374f0f642644e9e98"),
        new("data/simple-dragon/construction/material.csv", 146,
            "sha256:2a2b62b1c13e65d99098acac23f1ffcc4aa9ce08d162aa8491898b3f0c7bd395"),
        new("data/simple-dragon/construction/construction_regulation_surface.csv", 106_539,
            "sha256:292d2acc786bbfae0a83a9365e85b697f5bb97b25f25d4f3de21aae25310d48a"),
        new("data/simple-dragon/construction/construction_regulation_fenestration.csv", 27_623,
            "sha256:4e3813baf863dcce1bdb30382d9b33f3a481d5d7b927279c2a76f10aa7cc8562"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("M01", "epsimple-construction-core.material-construction-id-state", "material", "sha256:a92d5d96221defd842f23f6583a068644b3021fda80ceaf7dad07fee75d3c030", "sha256:4751e51ddb0f3b9e49ca82041138c209b07f87bfe1938c9fb7055881ece7eaef", new[] { "Material", "Material.ID", "Material.__init__" }, new[] { "Material.conductivity", "Material.density", "Material.specific_heat" }),
        new("M02", "epsimple-construction-core.material-property-validation-mutation", "material", "sha256:b925283d864b137f1d8df9d404c7d7eeb31e9decb1e2cd77cf157a2382e2b544", "sha256:edb9b59868285928aa302702bb75e5c8b17a1ea8ef9468ef99b61438ffa33632", new[] { "Material.conductivity", "Material.density", "Material.specific_heat" }, new[] { "Material", "Material.__init__" }),
        new("M03", "epsimple-construction-core.material-json-dict-dragon", "material", "sha256:98e70de2024fff1826dd7079ca1e5832a78840d89e7100b4989b9e2a5f74cc04", "sha256:7a2ef5f2f7a4cf3495ebb1cb1e04495c8bb3d9980f6591588b8555445ba9d356", new[] { "Material.from_json", "Material.to_dict", "Material.to_dragon" }, new[] { "Material", "Material.ID" }),
        new("M04", "epsimple-construction-core.material-database-load-get", "material", "sha256:e3ac196fd183cad8fad4d321879f99a220e0722cba0900d20aaf58afbb6412e8", "sha256:d81e8b09fceca3c4cee939812df383500e9fafa05e275081c4d1c2eae6800929", new[] { "Material.get_DB", "Material.load_DB" }, new[] { "Material", "Material.to_dict" }),
        new("F01", "epsimple-construction-core.fenestration-construction-id-state", "fenestration", "sha256:cdc71569325f286ea212ba5e4924b935fbd9c18d6700bb7de2880354aa3e01e1", "sha256:0d2d5fb3e613b6a437afb4621da0c81a8db18ca817ade0eb477c356adfd57102", new[] { "FenestrationConstruction", "FenestrationConstruction.ID", "FenestrationConstruction.__init__" }, new[] { "FenestrationConstruction.u", "FenestrationConstruction.g", "FenestrationConstruction.is_transparent" }),
        new("F02", "epsimple-construction-core.fenestration-property-validation-transparency", "fenestration", "sha256:f31f333465a424b9db81f08fd89cd40d6f9c68813b0fbdb773ffed01a6647e3d", "sha256:551df1ba806799b0fb9d557be0b8cca1d305ef0a1f574cd45ef9054d9ee40208", new[] { "FenestrationConstruction.u", "FenestrationConstruction.g", "FenestrationConstruction.is_transparent" }, new[] { "FenestrationConstruction", "FenestrationConstruction.__init__" }),
        new("F03", "epsimple-construction-core.fenestration-json-dict-dragon", "fenestration", "sha256:75277a02c3018735535b23b5e8570427eca4bd4b25764058c34e6ca3f4094789", "sha256:1ae7f8ba0bfd36052d9c71788d0b1745a329941d02f650ba848bf6b7aac5f0ad", new[] { "FenestrationConstruction.from_json", "FenestrationConstruction.to_dict", "FenestrationConstruction.to_dragon" }, new[] { "FenestrationConstruction", "FenestrationConstruction.ID" }),
        new("F04", "epsimple-construction-core.fenestration-database-load-get", "fenestration", "sha256:a762d9c0960840ecd04b1101e0a641e84a4e9e08dd4d557fea1af14812775485", "sha256:34347c009760b31d79be70cd9cc82f25eb6c6692b32afcdbe68bc7a8d9dc11a1", new[] { "FenestrationConstruction.get_DB", "FenestrationConstruction.load_DB" }, new[] { "FenestrationConstruction", "FenestrationConstruction.to_dict" }),
        new("S01", "epsimple-construction-core.surface-construction-id-layer-filtering", "surface", "sha256:30a911d59265890c603e627e82644f41baeb6e675717430678518678435110d4", "sha256:72ba71d45668765872ac8a83e09e9256780fa25d99c5b31f8ce8213bc2e9a444", new[] { "SurfaceConstruction", "SurfaceConstruction.ID", "SurfaceConstruction.__init__" }, new[] { "Material" }),
        new("S02", "epsimple-construction-core.surface-derived-state-and-validation", "surface", "sha256:a64179c7d1f1e6dbed4194e2f64ecbbf06473376ddeeeb856b32b523ea3ed2f6", "sha256:cd046046d0502f2f4f2a7e46ac29168829368ebe926efcb7278aef2abd5ebc17", new[] { "SurfaceConstruction.U_internal", "SurfaceConstruction.depth", "SurfaceConstruction.get_U", "SurfaceConstruction.get_unique_materials", "SurfaceConstruction.heat_capacity" }, new[] { "SurfaceConstruction", "SurfaceConstruction.__init__", "Material" }),
        new("S03", "epsimple-construction-core.surface-create-simple-branches", "surface", "sha256:6db25a9f88e0f782a019af35800d4c6f83e21e5d18de42bb208157dd318ebef3", "sha256:c4be9d0ebf60c8b3a413b954429c0d9bbc7d0e34f6e02ff883b281cc467c5640", new[] { "SurfaceConstruction.create_simply" }, new[] { "SurfaceConstruction", "SurfaceConstruction.get_U", "Material.get_DB" }),
        new("S04", "epsimple-construction-core.surface-reverse-and-dict", "surface", "sha256:e0742c6fa8c672ab34f828292c6602eb5187a9b9fb7cf520d58a009ff405def2", "sha256:ad4ac7bce44d0c78d242acf0e9e522736ca97efa8ea7a37d472ca78607eafc7a", new[] { "SurfaceConstruction.reversed", "SurfaceConstruction.to_dict" }, new[] { "SurfaceConstruction", "SurfaceConstruction.ID", "SurfaceConstruction.get_unique_materials" }),
        new("S05", "epsimple-construction-core.surface-json-and-dragon", "surface", "sha256:4214c3837d5fcad3dbba9357369710ab221c4ebaf0bec4510a63a2934c5eb549", "sha256:2002c34f535cf15f01c1b5495f614c75658c7b8acdc02b19e0e50a93ef26ae78", new[] { "SurfaceConstruction.from_json", "SurfaceConstruction.to_dragon" }, new[] { "SurfaceConstruction", "Material.to_dragon" }),
        new("S06", "epsimple-construction-core.surface-database-load-get", "surface", "sha256:2ed68fd6da2bd79b8c832990ff1d1ae7b7be03c720d1226d1b74b23681ed7635", "sha256:3532d39d80fd6547a90017341ae1226fd4e6cd6360d04e7aa7227801b82b4463", new[] { "SurfaceConstruction.get_DB", "SurfaceConstruction.load_DB" }, new[] { "SurfaceConstruction", "SurfaceConstruction.to_dict" }),
        new("S07", "epsimple-construction-core.surface-regulation-selection", "surface", "sha256:6e9a7e0f77d9db9f8a506a9c4dcc878b09955ead2f6a710eaa9d2ef57c2225d0", "sha256:7c7d8fc7d6df51ca699873555837a21431e1c21174fc2b7913f3cb4262612d19", new[] { "SurfaceConstruction.get_regulated_construction" }, new[] { "SurfaceConstruction.get_DB" }),
        new("X01", "epsimple-construction-core.special-singleton-empty-reverse", "special", "sha256:6bd4a806154977176eec59049331235c94f086fb337038ceb7e01bfdb11cc906", "sha256:f810dcc9194081639c84d1fe09108102acb77e16c308ed5fc094b115412d7487", new[] { "SpecialConstruction", "SpecialConstruction.__new__", "SpecialConstruction.get_unique_materials", "SpecialConstruction.reversed" }, new[] { "OpenConstruction", "UnknownConstruction" }),
        new("X02", "epsimple-construction-core.open-singleton-id-dragon", "special", "sha256:a6c1792d8aa673eb6768ab947dda77db08e78c3f7d821bcd7e5a24e7e56429c6", "sha256:45fd2438e983f4d5b79cea2a850b86a95923cb10b044ef1ca63d36e194abc961", new[] { "OpenConstruction", "OpenConstruction.ID", "OpenConstruction.to_dragon" }, new[] { "SpecialConstruction", "SpecialConstruction.__new__" }),
        new("X03", "epsimple-construction-core.unknown-singleton-id-dragon", "special", "sha256:59e23d864b5bbbd3b900d5a1a62853182080f9b935620f82fb147ac95a08d3fd", "sha256:75e592cd05e8ab2786f261abdc4a346a75c97b91bd846e63950aa442f8cbe1fb", new[] { "UnknownConstruction", "UnknownConstruction.ID", "UnknownConstruction.to_dragon" }, new[] { "SpecialConstruction", "SpecialConstruction.__new__" }),
        new("R01", "epsimple-construction-core.byte-identical-relocated-import", "relocation", "sha256:17d201c32fdce2398bd45ab41de992740c7f60b11fc1d4a36eea21bbf3f34229", "sha256:2307fc5a22839f2c103bcbc5511b49d2fd4d05c933e605530070bfbaf32a582c", Array.Empty<string>(), new[] { "Material.load_DB", "SurfaceConstruction.load_DB", "FenestrationConstruction.load_DB" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        ExpectedTarget(75, "FenestrationConstruction", "class", "epsimple-construction-core-75-f86ec154", "exception", "reviewed-native-adaptation-fenestrationconstruction-f86ec154", "Dragons.SimpleDragon.FenestrationConstruction", 4),
        ExpectedTarget(76, "FenestrationConstruction.ID", "function", "epsimple-construction-core-76-246156d9", "exception", "reviewed-native-adaptation-fenestrationconstruction-id-246156d9", "FenestrationConstruction.Id", 4),
        ExpectedTarget(79, "FenestrationConstruction.__init__", "function", "epsimple-construction-core-79-92969825", "exception", "reviewed-native-adaptation-fenestrationconstruction-init-92969825", "FenestrationConstruction(string, double, double?, EntityId?)", 4),
        ExpectedTarget(82, "FenestrationConstruction.from_json", "function", "epsimple-construction-core-82-e3c4284e", "exception", "reviewed-native-adaptation-fenestrationconstruction-from-json-e3c4284e", "Dragons.SimpleDragon.GrmReader fenestration construction path", 6),
        ExpectedTarget(83, "FenestrationConstruction.g", "function", "epsimple-construction-core-83-5025a060", "exception", "reviewed-native-adaptation-fenestrationconstruction-g-5025a060", "FenestrationConstruction.SolarHeatGainCoefficient", 5),
        ExpectedTarget(84, "FenestrationConstruction.get_DB", "function", "epsimple-construction-core-84-87537fa6", "exception", "reviewed-native-adaptation-fenestrationconstruction-get-db-87537fa6", "FenestrationConstructionDatabase.Find and Entries", 7),
        ExpectedTarget(85, "FenestrationConstruction.is_transparent", "function", "epsimple-construction-core-85-c288c4c2", "equivalent", "direct-native-fenestrationconstruction-is-transparent-c288c4c2", "FenestrationConstruction.IsTransparent", 5),
        ExpectedTarget(86, "FenestrationConstruction.load_DB", "function", "epsimple-construction-core-86-538b0465", "exception", "reviewed-native-adaptation-fenestrationconstruction-load-db-538b0465", "SimpleDragonEmbeddedData.FenestrationConstructions", 7),
        ExpectedTarget(87, "FenestrationConstruction.to_dict", "function", "epsimple-construction-core-87-8aaf803c", "exception", "reviewed-native-adaptation-fenestrationconstruction-to-dict-8aaf803c", "Dragons.SimpleDragon.GrmWriter fenestration construction path", 6),
        ExpectedTarget(88, "FenestrationConstruction.to_dragon", "function", "epsimple-construction-core-88-f430c29b", "exception", "reviewed-native-adaptation-fenestrationconstruction-to-dragon-f430c29b", "GreenRetrofitConversion fenestration construction conversion", 6),
        ExpectedTarget(89, "FenestrationConstruction.u", "function", "epsimple-construction-core-89-72e986b6", "exception", "reviewed-native-adaptation-fenestrationconstruction-u-72e986b6", "FenestrationConstruction.UValue", 5),
        ExpectedTarget(90, "Material", "class", "epsimple-construction-core-90-590c4070", "exception", "reviewed-native-adaptation-material-590c4070", "Dragons.SimpleDragon.Material", 0),
        ExpectedTarget(91, "Material.ID", "function", "epsimple-construction-core-91-246156d9", "exception", "reviewed-native-adaptation-material-id-246156d9", "Material.Id", 0),
        ExpectedTarget(94, "Material.__init__", "function", "epsimple-construction-core-94-d909f493", "exception", "reviewed-native-adaptation-material-init-d909f493", "Material(string, double, double, double, EntityId?)", 0),
        ExpectedTarget(97, "Material.conductivity", "function", "epsimple-construction-core-97-b733b56b", "exception", "reviewed-native-adaptation-material-conductivity-b733b56b", "Material.Conductivity", 1),
        ExpectedTarget(98, "Material.density", "function", "epsimple-construction-core-98-23136324", "exception", "reviewed-native-adaptation-material-density-23136324", "Material.Density", 1),
        ExpectedTarget(99, "Material.from_json", "function", "epsimple-construction-core-99-f2772e15", "exception", "reviewed-native-adaptation-material-from-json-f2772e15", "Dragons.SimpleDragon.GrmReader material path", 2),
        ExpectedTarget(100, "Material.get_DB", "function", "epsimple-construction-core-100-c3fc9501", "exception", "reviewed-native-adaptation-material-get-db-c3fc9501", "MaterialDatabase.Find and Items", 3),
        ExpectedTarget(101, "Material.load_DB", "function", "epsimple-construction-core-101-f6b33018", "exception", "reviewed-native-adaptation-material-load-db-f6b33018", "SimpleDragonEmbeddedData.Materials", 3),
        ExpectedTarget(102, "Material.specific_heat", "function", "epsimple-construction-core-102-abf4a2ea", "exception", "reviewed-native-adaptation-material-specific-heat-abf4a2ea", "Material.SpecificHeat", 1),
        ExpectedTarget(103, "Material.to_dict", "function", "epsimple-construction-core-103-7326bc5b", "exception", "reviewed-native-adaptation-material-to-dict-7326bc5b", "Dragons.SimpleDragon.GrmWriter material path", 2),
        ExpectedTarget(104, "Material.to_dragon", "function", "epsimple-construction-core-104-352f66b1", "exception", "reviewed-native-adaptation-material-to-dragon-352f66b1", "GreenRetrofitConversion material conversion", 2),
        ExpectedTarget(105, "OpenConstruction", "class", "epsimple-construction-core-105-3257fd04", "exception", "reviewed-native-adaptation-openconstruction-3257fd04", "SurfaceConstructionReferenceKind.Open", 16),
        ExpectedTarget(106, "OpenConstruction.ID", "constant", "epsimple-construction-core-106-45236b5b", "exception", "reviewed-native-adaptation-openconstruction-id-45236b5b", "Surface.ConstructionId value open and SurfaceConstructionReferenceKind.Open", 16),
        ExpectedTarget(107, "OpenConstruction.to_dragon", "function", "epsimple-construction-core-107-3f5ae9f0", "equivalent", "direct-native-openconstruction-to-dragon-3f5ae9f0", "GreenRetrofitConversion returns DragonAirBoundary", 16),
        ExpectedTarget(108, "SpecialConstruction", "class", "epsimple-construction-core-108-9f449287", "exception", "reviewed-native-adaptation-specialconstruction-9f449287", "SurfaceConstructionReferenceKind special cases", 15),
        ExpectedTarget(109, "SpecialConstruction.__new__", "function", "epsimple-construction-core-109-758d9c0b", "exception", "reviewed-native-adaptation-specialconstruction-new-758d9c0b", "SimpleSurface construction reference kind value semantics", 15),
        ExpectedTarget(110, "SpecialConstruction.get_unique_materials", "function", "epsimple-construction-core-110-4f9ce2c0", "exception", "reviewed-native-adaptation-specialconstruction-get-unique-materials-4f9ce2c0", "GreenRetrofitConversion special construction material bypass", 15),
        ExpectedTarget(111, "SpecialConstruction.reversed", "function", "epsimple-construction-core-111-119ed204", "exception", "reviewed-native-adaptation-specialconstruction-reversed-119ed204", "GreenRetrofitConversion special construction orientation bypass", 15),
        ExpectedTarget(112, "SurfaceConstruction", "class", "epsimple-construction-core-112-f3d6bd23", "exception", "reviewed-native-adaptation-surfaceconstruction-f3d6bd23", "Dragons.SimpleDragon.SurfaceConstruction", 8),
        ExpectedTarget(113, "SurfaceConstruction.ID", "function", "epsimple-construction-core-113-246156d9", "exception", "reviewed-native-adaptation-surfaceconstruction-id-246156d9", "SurfaceConstruction.Id", 8),
        ExpectedTarget(114, "SurfaceConstruction.U_internal", "function", "epsimple-construction-core-114-c6b969b4", "equivalent", "direct-native-surfaceconstruction-u-internal-c6b969b4", "SurfaceConstruction.InternalUValue", 9),
        ExpectedTarget(117, "SurfaceConstruction.__init__", "function", "epsimple-construction-core-117-6e437543", "exception", "reviewed-native-adaptation-surfaceconstruction-init-6e437543", "SurfaceConstruction(string, IEnumerable<SurfaceConstructionLayer>, EntityId?)", 8),
        ExpectedTarget(120, "SurfaceConstruction.create_simply", "function", "epsimple-construction-core-120-23907b76", "exception", "reviewed-native-adaptation-surfaceconstruction-create-simply-23907b76", "SurfaceConstruction.CreateSimple", 10),
        ExpectedTarget(121, "SurfaceConstruction.depth", "function", "epsimple-construction-core-121-60a500a8", "equivalent", "direct-native-surfaceconstruction-depth-60a500a8", "SurfaceConstruction.Depth", 9),
        ExpectedTarget(122, "SurfaceConstruction.from_json", "function", "epsimple-construction-core-122-b1bb16e6", "exception", "reviewed-native-adaptation-surfaceconstruction-from-json-b1bb16e6", "Dragons.SimpleDragon.GrmReader surface construction path", 12),
        ExpectedTarget(123, "SurfaceConstruction.get_DB", "function", "epsimple-construction-core-123-d21ed4db", "exception", "reviewed-native-adaptation-surfaceconstruction-get-db-d21ed4db", "SurfaceConstructionDatabase.Find and Entries", 13),
        ExpectedTarget(124, "SurfaceConstruction.get_U", "function", "epsimple-construction-core-124-8a480443", "equivalent", "direct-native-surfaceconstruction-get-u-8a480443", "SurfaceConstruction.GetUValue", 9),
        ExpectedTarget(125, "SurfaceConstruction.get_regulated_construction", "function", "epsimple-construction-core-125-a806c4c3", "exception", "reviewed-native-adaptation-surfaceconstruction-get-regulated-construction-a806c4c3", "SurfaceConstructionDatabase.FindRegulated", 14),
        ExpectedTarget(126, "SurfaceConstruction.get_unique_materials", "function", "epsimple-construction-core-126-71552576", "equivalent", "direct-native-surfaceconstruction-get-unique-materials-71552576", "SurfaceConstruction.Layers material projection", 9),
        ExpectedTarget(127, "SurfaceConstruction.heat_capacity", "function", "epsimple-construction-core-127-dc8c7ebc", "equivalent", "direct-native-surfaceconstruction-heat-capacity-dc8c7ebc", "SurfaceConstruction.HeatCapacity", 9),
        ExpectedTarget(128, "SurfaceConstruction.load_DB", "function", "epsimple-construction-core-128-fec259a4", "exception", "reviewed-native-adaptation-surfaceconstruction-load-db-fec259a4", "SimpleDragonEmbeddedData.SurfaceConstructions", 13),
        ExpectedTarget(129, "SurfaceConstruction.reversed", "function", "epsimple-construction-core-129-d72c2143", "exception", "reviewed-native-adaptation-surfaceconstruction-reversed-d72c2143", "SurfaceConstruction.Reverse", 11),
        ExpectedTarget(130, "SurfaceConstruction.to_dict", "function", "epsimple-construction-core-130-59426aa2", "exception", "reviewed-native-adaptation-surfaceconstruction-to-dict-59426aa2", "Dragons.SimpleDragon.GrmWriter surface construction path", 11),
        ExpectedTarget(131, "SurfaceConstruction.to_dragon", "function", "epsimple-construction-core-131-a204e680", "exception", "reviewed-native-adaptation-surfaceconstruction-to-dragon-a204e680", "GreenRetrofitConversion surface construction conversion", 12),
        ExpectedTarget(132, "UnknownConstruction", "class", "epsimple-construction-core-132-d803cd9d", "exception", "reviewed-native-adaptation-unknownconstruction-d803cd9d", "SurfaceConstructionReferenceKind.Unknown", 17),
        ExpectedTarget(133, "UnknownConstruction.ID", "constant", "epsimple-construction-core-133-d6777d2d", "exception", "reviewed-native-adaptation-unknownconstruction-id-d6777d2d", "Surface.ConstructionId null or empty and SurfaceConstructionReferenceKind.Unknown", 17),
        ExpectedTarget(134, "UnknownConstruction.to_dragon", "function", "epsimple-construction-core-134-558da4a7", "exception", "reviewed-native-adaptation-unknownconstruction-to-dragon-558da4a7", "GreenRetrofitConversion.ResolveUnknownConstruction", 17),
    };

    private static readonly int[] ExcludedIndices =
    {
        77, 78, 80, 81, 92, 93, 95, 96, 115, 116, 118, 119,
    };

    private static readonly string[] ExcludedSymbols =
    {
        "FenestrationConstruction.__eq__",
        "FenestrationConstruction.__hash__",
        "FenestrationConstruction.__repr__",
        "FenestrationConstruction.__str__",
        "Material.__eq__",
        "Material.__hash__",
        "Material.__repr__",
        "Material.__str__",
        "SurfaceConstruction.__eq__",
        "SurfaceConstruction.__hash__",
        "SurfaceConstruction.__repr__",
        "SurfaceConstruction.__str__",
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(8, "sha256:dede5553e44d1e168169130cf5baa5914b1928f90e82659420834d609e60c52d"),
        new(8, "sha256:06e9d34f983212e370a0cad0f3baa5344235fcc3c260b21081793dce72383a6a"),
        new(8, "sha256:0cf5859c9add6e6af68971f48d2bf684f840ba5c7439dbbcace680d2e10ecf2d"),
        new(8, "sha256:92707509d4775f6db3de71efb659b1a2743be4b71e53e936b26ff38c20a3b69f"),
        new(7, "sha256:90d52ed2fa5a31bbbaaf189c84f0689712fc13c06a73efd4152f436d5a32ef84"),
        new(8, "sha256:6c443b31da4115ec784576fc3b3b4d0f1a3e0576f15da8baa055715c0daf5557"),
        new(8, "sha256:d2478fbcd9fc3be986bd550d27dfbd7f19a9fca24402dfad7daa6d5f726e859e"),
        new(8, "sha256:247da9bbae6429bb2af43fe3207ac34ea886fc2e353895ab017130c250d7a34b"),
        new(10, "sha256:b94dd3cd66345cc50070f9b02fe4c1097e45b0c74a47ff75bcb0dbb441f78ace"),
        new(8, "sha256:45d7385a08c695f3f5a1ab1e4d3ab26459dc0ad0536061cd3e8b107dff43df57"),
        new(9, "sha256:14a97bb905be961b525075030a92d1e295b41d2a8e6ae5a102afe1659e7d43a1"),
        new(8, "sha256:88c41b1da313a9ebb20db64b3dfc3ce658f8607ca078ca9a533479517e6fced3"),
        new(8, "sha256:eaa1b59062fcad0e20c89645fdfdbb0bfc1d2e096a9312bb6cf919f0dbf43183"),
        new(8, "sha256:8658499a9b8b2bade41923a70d6011457ac6fc8ec89c7559a6380693b7de9d9a"),
        new(7, "sha256:c756ed62169a833a3590f1164da81dbbb7eb7408ed441ad3d81dbbbdccc2fce8"),
        new(9, "sha256:bb8a6a82670c54f6f0e143b68e75e028955b308a16be9b111df3639592ba0089"),
        new(7, "sha256:0477649ec13f822b702398db55ec67c09381588cc00b57a39384eb0ed608be5d"),
        new(8, "sha256:6f4cd965ecf199581c6f55743cd0618f82dcd59e81bface802f08b7dd8fb9b2d"),
        new(9, "sha256:cec6a56a14ad7453f6e2491b0f3bb9516f7513818fdba9b4d9b302de143e3901"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:33adc66a0ef21ca9bd88d16f2686fc643243cd6e0567e4171664d447ba5aa34d",
        "sha256:a8993a436c3f17d29116fe64796c479fc6463ee009958c1b0d1e595a841f1889",
        "sha256:7c16d3cb4b39902879051b6c1b7d13225c5052ce0db637d434f559b652891a8d",
        "sha256:373ba473c59cadfaab2a94098fe87c68847ac74e359e003ed3597077d4eea10f",
        "sha256:3deb7c1efa556db36cc539e9d0da25c24ae0d524a7e7f12085af4216026684b4",
        "sha256:a8b36dcf00afcebcaa32267e9dff06aabce3cde3bba52b18054e0415c1cbd930",
        "sha256:6fd92faf17ebb7849714bebe16016b205ee82385c4d47e7a7a5cf22ec6df30b7",
        "sha256:a3a95b319924c5ed0a514097f51b58304116aec27fb7bf8b54eacadf99c7f95b",
        "sha256:472dac42f26c79aa05f21c73e85981c6a692261cfae129d91e767091b0650524",
        "sha256:71e03b3a90ec9ab06adc8271985446c6787fb7ee2663e969f5a6038446c013e2",
        "sha256:ecd7eb7df54b0e36c322997b9c1790864f71bf271b107099a68b824aea53ac53",
        "sha256:37c0f50de0f9fc0373f16d653fa400a8a77c0fdafde215cec128e6eb40c66886",
        "sha256:9fba48bf7142405e85e2f3fca6f287a756409402a73c3034a851045bd0bc87b5",
        "sha256:8582d6ced4e2c733030301abaecbbb116dbd768a811a5e84977ce2dc483a7394",
        "sha256:a6cb502c0f778234b5aeb500146b099e7ac64dc0f21b924616f48b85de4f8fc3",
        "sha256:5ae597abd6bf069dc89495ce57a8c0487803c2ff5f0c40821c2980f19d4e01d5",
        "sha256:759b1acc304e6e3a995222bf11b61285a987931511ab2e11b718358d0161b1ec",
        "sha256:96e08fc53a6c187aff0015e123fa7f008bbbc4d0f800915991cf8897353e727a",
        "sha256:5838327af4df9cea4b0ccbf49b49cb29385e85c1c87184108c7fbbd01a886de1",
        "sha256:87f21532fddaed447db63826daa6be6a4710a907f32d1978e283d7211958fcf1",
        "sha256:8bfe7263935d3e23c9158b6d7acdbca48991020bb46f171c39d0ed5dbfdaf751",
        "sha256:c8099628f7632ca17affa436d7115ac1f38eab0dcee8b136738a8e4cf2b4f1c0",
        "sha256:d0ef39d5e144715db48a8d76f57317fc336c239aeed262466c3a8e94f00e14b7",
        "sha256:57613c9754f3d009895d894eb4d412370e92754392f60767d20e64861c3613d5",
        "sha256:931e2342879b4854d9f9c7995f54bfbece73ef2940a224ad447ab63e0d10ff52",
        "sha256:fac111b8c4fb37549ea9c4511633085c3e7ca8f097d583d9c4264f8c103706e4",
        "sha256:8442b781e89a64e3ae2092f5c14af60bf6c344acd5c1e1683f2317493396054c",
        "sha256:22356dc92d50dcc551026e74296eb42d1c79d7c6faa5a5efd9809373e8967921",
        "sha256:7c127d16f151e1f8ddd0c3b7b48611323f0b8c68e5e645cf8cd7ac37706f8f6e",
        "sha256:dfc85167a7954b7bfcf07ba377f70a7e98635cc23a29596739ea7bc20282df63",
        "sha256:772344f282161e027020a3760c9830633a36cf9441c06fac3026186699fee4ad",
        "sha256:130e37512e58515af5848a9c63741ba4726daf3f68d90c10c333d7a3f5db8023",
        "sha256:9d94e64e795518e649c4fbf98de2369205ffae43e7c8152728988cd4d949b448",
        "sha256:a4c92cd1c66ddd06827b22cf42d8ba33dba5c6ed6c173e601f6c892504ab7b62",
        "sha256:b4e1a3799d7d858e96d70ae5c2ac1e2be68fe4238cd93549aed430089d4c9a8e",
        "sha256:0c9efcf98ee6aa8fbc04b5e069a41656302ea3a08436289be535d55108632820",
        "sha256:c106f71cbb1a54d67e62f644c779bb609e28fcce4b98cd20170e9d5c621c6313",
        "sha256:2f70488dd6555d42e1dfca9fe69ace14c378093d67155d809bbc9e2ec7ced2b6",
        "sha256:f137b5347c688f0ee6ae7178e18dfa916b7e8ac9c59810602f880f30e525d717",
        "sha256:6ee6f81a98cee670cd492d1cb62218be346eed6d80203b84b820f45410f1946c",
        "sha256:6abeed335b2314384a631479a77fadbf15d3670764c7c065639d950941387b29",
        "sha256:bbecaf74c4e41fbec1d5448689e6d093430b5076c03cee9e69653642d87dc35c",
        "sha256:81ae46ce56bfb207be8d96d8675bdeb732d55d0fba7fc7d909f4575411a55c98",
        "sha256:679e90e645d89bd5d4944885c255de36c2cccdca6b4dbda78a63587266c9a58a",
        "sha256:52c8d8801881636e40a354a65f4b3298fa8b371850165ba1ff9045963557c1fd",
        "sha256:80ccec14be573d1eb38f11232801045f347032e1b13236bb290f7086cd9f31a0",
        "sha256:96391d43fba261c49204026126cd729bc38d31349fdd93d0e6348673c57ea4fc",
        "sha256:f43151a366c579e24d78d588438afeaf523227c9492c909284d99b61ba943f44",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:be15b5d17e49845f3d341b4c71a21a15389dbaf3b5e05df9615f9ada48aa365d", // epsimple-construction-core-75-f86ec154
        "sha256:20840f7f7f4fe5f1d2c7a82ed7161567eb1d601a5e0c74cf01a99e7b78824974", // epsimple-construction-core-76-246156d9
        "sha256:55660d7ebfe63caea0e15f3fbc2d73bd67e538b4daee8308da233809c9776178", // epsimple-construction-core-79-92969825
        "sha256:f6509287b9b977dbcd3f18669b0f621bdad81acb2c832e86cf57381ff0c5f3fe", // epsimple-construction-core-82-e3c4284e
        "sha256:185dba0977d6508d63193baaa7c0055e26d3572e9e0fdbc07d4fbd47e3b969c2", // epsimple-construction-core-83-5025a060
        "sha256:2160362137025871f7dd976e71c34a6012646eddc6bdbb56b58be7a88a352ac6", // epsimple-construction-core-84-87537fa6
        "sha256:e49b2bd15781c8fe3db43c595c55839daae50b27b94552125ea0b0a45659fc2c", // epsimple-construction-core-85-c288c4c2
        "sha256:99e3f3d2eb12797716219cd41719360887f243cb80fda8ccda859a3cfbe76386", // epsimple-construction-core-86-538b0465
        "sha256:552be7eb306d7e32eb5c8568f846485a3e0f2dcf4e7b4f057bfc123223d945a2", // epsimple-construction-core-87-8aaf803c
        "sha256:e3d91265e74478a42666e2a5734d6614fa589313fa23b48f6ccde44d21a6ae5e", // epsimple-construction-core-88-f430c29b
        "sha256:9ef79f9cfd4082e9716477420d25ce9de0f56ecb7eb180eebbfd9fca17d71346", // epsimple-construction-core-89-72e986b6
        "sha256:298b2d3418132658f494afe64615eea2d8c03013399083b0f85d4271c54588f2", // epsimple-construction-core-90-590c4070
        "sha256:97752afb6e1ac932083f5d6325a415f669e8746c00817f5b58f2736bfdab9577", // epsimple-construction-core-91-246156d9
        "sha256:4aea70b4b9c28c75c3b42af943ab4cd8c3de450dad3cf112ef8c5cb4954de19e", // epsimple-construction-core-94-d909f493
        "sha256:f3b853874379764ec7a27ae5c212217078c21876580957c2b410054097d21e42", // epsimple-construction-core-97-b733b56b
        "sha256:fa99bee4c9c034d70fb0e64a16824186374f961a2ba7b539aa9eb8f4e072784d", // epsimple-construction-core-98-23136324
        "sha256:4c07223c4fe705977d225e197bd4b349d234fa63ea04f997cf48b2f8d0641d05", // epsimple-construction-core-99-f2772e15
        "sha256:d702e7bee9fcc86a7ea6731f963f257df202d72aa5c1e6c50eb19155ca7089d9", // epsimple-construction-core-100-c3fc9501
        "sha256:b39ab879ece874f05063457768818ec2a9c24c56deea6c58ce775d613252e80a", // epsimple-construction-core-101-f6b33018
        "sha256:1c06e5dd5ed1c967ea3765e5f14285e6a62aef75973207492dbd1d53c6f7faf1", // epsimple-construction-core-102-abf4a2ea
        "sha256:ea4c117883bf41744435a24153d7ae93dc73575fbb8f2f57e019a1df3ec21cfa", // epsimple-construction-core-103-7326bc5b
        "sha256:3a1ab91e190772255b34e4a615fc0b3aa5dc16bb5ca4e8101e1c89af90073615", // epsimple-construction-core-104-352f66b1
        "sha256:8b31a074872bf8606d548b300a26c3b195c14e45ec0f31c5f24d8142860b4462", // epsimple-construction-core-105-3257fd04
        "sha256:77a634a2c35b40dc7a610447e4e78754f6443516aab7a17b005bd847dd2f32ea", // epsimple-construction-core-106-45236b5b
        "sha256:5cbc32e069e1f8bd99ef49ae2f6616a3c4d96de001bf9a21aa65d8c397c27bc5", // epsimple-construction-core-107-3f5ae9f0
        "sha256:e24f9402e50fa327679d7e3366eebab0177f7e6d8e57a8af4a0357750973d138", // epsimple-construction-core-108-9f449287
        "sha256:bad3cedc5b3a2b9d5dd916d400f0bcaf048f712f365152fec77e00ac977dc6d4", // epsimple-construction-core-109-758d9c0b
        "sha256:121d5fdaa1a925235c725695bbbf6f8478975c105d297617c2c7c32f0cc7cb32", // epsimple-construction-core-110-4f9ce2c0
        "sha256:6a53f7ef253728de24ae91ff52a8456cd2250e8c6cea27910203b13101d437a3", // epsimple-construction-core-111-119ed204
        "sha256:dfef9bad91ae09af49e85ec0de89630a9b9af833282dcf2b5e927289e0acd2bd", // epsimple-construction-core-112-f3d6bd23
        "sha256:0657b4cd89dc56675a49a77dd496ac68c0316cb4d25d012f85b7b12ee7e1110a", // epsimple-construction-core-113-246156d9
        "sha256:649a0c199fd3076dafa4bb6939ef3822d86fab404758e16acc29c9b152aade56", // epsimple-construction-core-114-c6b969b4
        "sha256:726c25b4649a8dd6da318b329df28903e1f9db023e7762defd2eae737bbc0287", // epsimple-construction-core-117-6e437543
        "sha256:32e2241f16ffe2c24b8817be2fb0c8acb24f8cd7b024e733677de0e6c64abc02", // epsimple-construction-core-120-23907b76
        "sha256:fdda2f1308642f24d877735705a3d0094536560cf48a8cb5785d6ef5d7749d48", // epsimple-construction-core-121-60a500a8
        "sha256:cf44cc3226af313b36d7a4bbb49712610fa640225cff9f54c08cc41d514acd38", // epsimple-construction-core-122-b1bb16e6
        "sha256:aae519998ee504d88b3645af63b0e19798b1104e174144664121014882061657", // epsimple-construction-core-123-d21ed4db
        "sha256:b55b36af1b228767852caf6a82e952ecc96afa4807824cb5ba45902bffbcde97", // epsimple-construction-core-124-8a480443
        "sha256:f0f792fb32c28afee1d8e850d16aca223a2479a1619ff4fcb87aa7c663c9a557", // epsimple-construction-core-125-a806c4c3
        "sha256:9f53176553501f2726460f965a66700ee83012958faca874913dfc808d2d584e", // epsimple-construction-core-126-71552576
        "sha256:40a6592b910a659b550f01869bc358991b389f482f428596bfdfd1682d590f59", // epsimple-construction-core-127-dc8c7ebc
        "sha256:27d6fc1b325fa5be5257466628db7827dc3d13c1085a148799d19f090f88a47d", // epsimple-construction-core-128-fec259a4
        "sha256:f3f75136694ef229d139934dc61dd51ca83269630d1455db28721ea1de5fd69e", // epsimple-construction-core-129-d72c2143
        "sha256:eb7491571cb82df7de81bdd66e4875ac17737293eea4812cca609649a651a3b3", // epsimple-construction-core-130-59426aa2
        "sha256:0fe1d465e43816e5de87c9434d90f7ace11b58b45d6fc71a3c11af9a167abde7", // epsimple-construction-core-131-a204e680
        "sha256:f975d1b4aaa2f1deac4ec39c347ec1c605bc16a94629f8f64069103875375a57", // epsimple-construction-core-132-d803cd9d
        "sha256:641c341314c719098e068bf63a11daa2a918db4ed541cd55a5bf2cb33fdd08c2", // epsimple-construction-core-133-d6777d2d
        "sha256:9aada2b7196181f42e2312c3eee48d1e20bcdcc0ba2348d99a2fe56ec202155a", // epsimple-construction-core-134-558da4a7
    };

    [Fact]
    public void MatchesPinnedConstructionCoreThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(19, observations.Length);
        Assert.Equal(Cases.Select(item => item.Code), observations.Select(item => item.Code));

        object[] receipts = corpus.Targets
            .Select(target => CreateReceipt(target, observations))
            .ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();
        string[] collectorOutputHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(new
            {
                cases = new[]
                {
                    new
                    {
                        output = receipt,
                        test_case = EvidenceTestCase,
                    },
                },
            })))
            .ToArray();

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "CONSTRUCTION_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    cases = observations.Select(item => new
                    {
                        item.Code,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipts = corpus.Targets.Select((item, index) => new
                    {
                        item.Symbol,
                        item.AssertionId,
                        receipt_sha256 = receiptHashes[index],
                    }),
                }, DiscoveryJsonOptions));
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }

        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
        Assert.Equal(ExpectedCollectorOutputHashes, collectorOutputHashes);
        int recordCount = 0;
        for (int index = 0; index < corpus.Targets.Length; index++)
        {
            JsonElement receipt = JsonSerializer.SerializeToElement(receipts[index]);
            ValidateReceipt(receipt, corpus.Targets[index], observations);
            TrustedEvidenceRecorder.Record(
                corpus.Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
            recordCount++;
        }

        Assert.Equal(48, recordCount);
        Assert.Equal(48, corpus.Targets.Length);
        Assert.Equal(48, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(7, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(41, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.DoesNotContain(corpus.Targets, item => ExcludedIndices.Contains(item.InventoryIndex));
        Assert.DoesNotContain(corpus.Targets, item => ExcludedSymbols.Contains(item.Symbol, StringComparer.Ordinal));
        Assert.Equal(19, corpus.FixtureCases.Length);
    }

    private static ExpectedTargetBinding ExpectedTarget(
        int inventoryIndex,
        string symbol,
        string kind,
        string assertionId,
        string classification,
        string adaptationId,
        string nativeRoute,
        int caseIndex) => new(
            inventoryIndex,
            symbol,
            kind,
            assertionId,
            classification,
            adaptationId,
            nativeRoute,
            caseIndex);

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        Assert.True(typeof(Material).IsSealed);
        Assert.True(typeof(FenestrationConstruction).IsSealed);
        Assert.True(typeof(SurfaceConstruction).IsSealed);
        AssertReadOnlyProperty<Material>(nameof(Material.Id), typeof(EntityId));
        AssertReadOnlyProperty<Material>(nameof(Material.Conductivity), typeof(double));
        AssertReadOnlyProperty<Material>(nameof(Material.Density), typeof(double));
        AssertReadOnlyProperty<Material>(nameof(Material.SpecificHeat), typeof(double));
        AssertReadOnlyProperty<FenestrationConstruction>(nameof(FenestrationConstruction.Id), typeof(EntityId));
        AssertReadOnlyProperty<FenestrationConstruction>(nameof(FenestrationConstruction.UValue), typeof(double));
        AssertReadOnlyProperty<FenestrationConstruction>(
            nameof(FenestrationConstruction.SolarHeatGainCoefficient),
            typeof(double?));
        AssertReadOnlyProperty<FenestrationConstruction>(
            nameof(FenestrationConstruction.IsTransparent),
            typeof(bool));
        AssertReadOnlyProperty<SurfaceConstruction>(nameof(SurfaceConstruction.Id), typeof(EntityId));
        AssertReadOnlyProperty<SurfaceConstruction>(
            nameof(SurfaceConstruction.Layers),
            typeof(IReadOnlyList<SurfaceConstructionLayer>));
        AssertReadOnlyProperty<SurfaceConstruction>(nameof(SurfaceConstruction.InternalUValue), typeof(double));
        AssertReadOnlyProperty<SurfaceConstruction>(nameof(SurfaceConstruction.Depth), typeof(double));
        AssertReadOnlyProperty<SurfaceConstruction>(nameof(SurfaceConstruction.HeatCapacity), typeof(double));
        Assert.Equal(typeof(SurfaceConstruction), typeof(SurfaceConstruction).GetMethod(
            nameof(SurfaceConstruction.Reverse),
            Type.EmptyTypes)!.ReturnType);
        Assert.NotNull(typeof(SurfaceConstruction).GetMethod(
            nameof(SurfaceConstruction.CreateSimple),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(GrmReader).GetMethod(
            nameof(GrmReader.Read),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(SimpleDragonDatabase) },
            modifiers: null));
        Assert.NotNull(typeof(GrmWriter).GetMethod(
            nameof(GrmWriter.Serialize),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(GreenRetrofitModel), typeof(bool) },
            modifiers: null));
        Assert.NotNull(typeof(GreenRetrofitConverter).GetMethod(
            nameof(GreenRetrofitConverter.Convert),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(GreenRetrofitModel), typeof(GreenRetrofitConversionOptions) },
            modifiers: null));
        Assert.Equal(
            new[] { "Defined", "Unknown", "Open", "Unresolved" },
            Enum.GetNames<SurfaceConstructionReferenceKind>());
    }

    private static void AssertReadOnlyProperty<T>(string name, Type expectedType)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(typeof(T).GetProperty(name));
        Assert.Equal(expectedType, property.PropertyType);
        Assert.False(property.CanWrite);
        Assert.True(property.GetMethod!.IsPublic);
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(FixturePath));
        Assert.Equal(FixtureBytes, bytes.Length);
        Assert.Equal(FixtureSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain("\r\n", new UTF8Encoding(false, true).GetString(bytes), StringComparison.Ordinal);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 256,
        });
    }

    private static OracleCorpus ValidateOracle(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoUnsafeIdentity(root);
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(
            root,
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "excluded_receipts",
            "fact_sha256",
            "runtime",
            "schema",
            "symbols",
            "target_receipts",
            "upstream");
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));

        ValidateRuntime(root.GetProperty("runtime"));
        ValidateUpstream(root.GetProperty("upstream"));

        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        AssertKeys(root.GetProperty("case_sha256"), Cases.Select(item => item.CaseId).ToArray());
        AssertKeys(root.GetProperty("fact_sha256"), Cases.Select(item => item.CaseId).ToArray());
        for (int index = 0; index < fixtureCases.Length; index++)
        {
            ValidateCase(
                fixtureCases[index],
                Cases[index],
                root.GetProperty("case_sha256"),
                root.GetProperty("fact_sha256"));
        }

        TargetBinding[] targets = ValidateTargets(root);
        ValidateConsumerContract(root.GetProperty("consumer_contract"), targets);
        ValidateExcludedReceipts(
            root.GetProperty("excluded_receipts"),
            root.GetProperty("upstream").GetProperty("adjacent_exclusions"),
            targets);
        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            fixtureCases.SelectMany(item => ReadStringArray(item.GetProperty("target_symbols")))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));
        return new OracleCorpus(fixtureCases, targets);
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "implementation",
            "platform",
            "pointer_width_bits",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("win32", RequiredString(runtime, "platform"));
        Assert.Equal(64, runtime.GetProperty("pointer_width_bits").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(dependencies, "eppy", "numpy", "pandas", "shapely");
        Assert.Equal("0.5.63", RequiredString(dependencies, "eppy"));
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("2.0.6", RequiredString(dependencies, "shapely"));
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(
            upstream,
            "adjacent_exclusions",
            "artifacts",
            "commit",
            "database_resources",
            "inventory",
            "isolated_import",
            "source");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));

        JsonElement inventory = upstream.GetProperty("inventory");
        AssertKeys(inventory, "bytes", "content_sha256", "file_sha256");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));

        JsonElement source = upstream.GetProperty("source");
        AssertKeys(source, "ast_sha256", "bytes", "path", "source_sha256");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement artifacts = upstream.GetProperty("artifacts");
        AssertKeys(artifacts, "bootstrap", "strict_json_support");
        ValidateArtifactProjection(
            artifacts.GetProperty("bootstrap"),
            "tools/python-reference/bootstrap_reference.py",
            1_232,
            "sha256:0674dcf1fe966de2a4b873a360ef67be48d74f38ba80adba9c74405fd9be7e0f");
        ValidateArtifactProjection(
            artifacts.GetProperty("strict_json_support"),
            "tools/python-reference/generate_schedule_type_oracle.py",
            21_114,
            "sha256:4d2dd8d0c487af7a24f93f1e79b9b27ed19676cf7909a8039d90248fd7d6e1bc");

        JsonElement[] resources = upstream.GetProperty("database_resources").EnumerateArray().ToArray();
        Assert.Equal(3, resources.Length);
        ValidateArtifactProjection(resources[0], "epsimple/_data/construction/material.csv", 141,
            "sha256:e7186c4a29ddf1b91195ba86829e4ca49af1f4ee07c59377f6df3b83676614c8");
        ValidateArtifactProjection(resources[1],
            "epsimple/_data/construction/construction_regulation_surface.csv", 105_194,
            "sha256:db07a96bd3920ffeb1a2244f2d6bc9e42ea2c8c264143a393c22649c72d12cd7");
        ValidateArtifactProjection(resources[2],
            "epsimple/_data/construction/construction_regulation_fenestration.csv", 27_190,
            "sha256:5b452e853be1c2743f187d151fa424af049584a0968a60839d657e10e391b0c7");

        JsonElement isolated = upstream.GetProperty("isolated_import");
        AssertKeys(
            isolated,
            "epsimple_core_initializer_executed",
            "epsimple_package_initializer_executed",
            "loaded_local_modules",
            "relocated_source_copy",
            "source_location_count");
        Assert.False(isolated.GetProperty("epsimple_core_initializer_executed").GetBoolean());
        Assert.False(isolated.GetProperty("epsimple_package_initializer_executed").GetBoolean());
        Assert.Equal("byte-identical-epsimple-and-idragon-trees", RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        JsonElement[] loaded = isolated.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(16, loaded.Length);
        Assert.Equal(16, loaded.Select(item => RequiredString(item, "module"))
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(loaded, item => AssertKeys(item, "ast_sha256", "module", "path", "source_sha256"));
        JsonElement construction = Assert.Single(
            loaded,
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("epsimple.core.construction", RequiredString(construction, "module"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(construction, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(construction, "ast_sha256"));
    }

    private static void ValidateCase(
        JsonElement item,
        CaseBinding expected,
        JsonElement caseHashes,
        JsonElement factHashes)
    {
        AssertKeys(item, "code", "context_symbols", "id", "python", "subfamily", "target_symbols");
        Assert.Equal(expected.Code, RequiredString(item, "code"));
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Subfamily, RequiredString(item, "subfamily"));
        AssertStringArray(item.GetProperty("target_symbols"), expected.TargetSymbols);
        AssertStringArray(item.GetProperty("context_symbols"), expected.ContextSymbols);

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "facts_sha256", "outcome");
        Assert.Equal("observed", RequiredString(python, "outcome"));
        Assert.Equal(expected.FactsSha256, RequiredString(python, "facts_sha256"));
        Assert.Equal(expected.FactsSha256, RequiredString(factHashes, expected.CaseId));
        Assert.Equal(expected.FactsSha256, CanonicalSha256(python.GetProperty("facts")));
        Assert.Equal(expected.CaseSha256, RequiredString(caseHashes, expected.CaseId));
        Assert.Equal(expected.CaseSha256, CanonicalSha256(item));
        Assert.Empty(expected.TargetSymbols.Intersect(expected.ContextSymbols, StringComparer.Ordinal));
    }

    private static TargetBinding[] ValidateTargets(JsonElement root)
    {
        JsonElement[] descriptors = root.GetProperty("symbols").EnumerateArray().ToArray();
        JsonElement[] receipts = root.GetProperty("target_receipts").EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, descriptors.Length);
        Assert.Equal(ExpectedTargets.Length, receipts.Length);

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        Assert.Equal(InventoryBytes, inventoryBytes.Length);
        Assert.Equal(InventoryFileSha256, Sha256(inventoryBytes));
        using JsonDocument inventoryDocument = JsonDocument.Parse(inventoryBytes);
        AssertUniqueObjectKeysRecursive(inventoryDocument.RootElement);
        Assert.Equal(InventoryContentSha256, RequiredString(inventoryDocument.RootElement, "content_sha256"));
        Assert.Equal(UpstreamCommit, RequiredString(inventoryDocument.RootElement, "upstream_commit"));
        JsonElement inventorySymbols = inventoryDocument.RootElement.GetProperty("symbols");

        var targets = new TargetBinding[ExpectedTargets.Length];
        for (int index = 0; index < ExpectedTargets.Length; index++)
        {
            ExpectedTargetBinding expected = ExpectedTargets[index];
            JsonElement descriptor = descriptors[index];
            JsonElement receipt = receipts[index];
            JsonElement inventorySymbol = inventorySymbols[expected.InventoryIndex];
            AssertTargetProjection(descriptor, expected, includeIndex: false);
            AssertTargetProjection(receipt, expected, includeIndex: true);
            AssertTargetProjection(inventorySymbol, expected, includeIndex: false, requireExactKeys: false);
            foreach (string key in new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" })
            {
                Assert.Equal(receipt.GetProperty(key).GetRawText(), descriptor.GetProperty(key).GetRawText());
                Assert.Equal(receipt.GetProperty(key).GetRawText(), inventorySymbol.GetProperty(key).GetRawText());
            }

            string symbolHash = RequiredString(receipt, "symbol_hash");
            Assert.Equal(
                "epsimple-construction-core-"
                + expected.InventoryIndex.ToString(CultureInfo.InvariantCulture)
                + "-"
                + symbolHash.AsSpan("sha256:".Length, 8).ToString(),
                expected.AssertionId);
            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                expected.Kind,
                symbolHash,
                RequiredString(receipt, "signature_hash"),
                RequiredString(receipt, "body_hash"),
                expected.AssertionId,
                expected.Classification,
                expected.AdaptationId,
                expected.NativeRoute,
                expected.CaseIndex);
        }

        Assert.Equal(48, targets.Length);
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex), targets.Select(item => item.InventoryIndex));
        return targets;
    }

    private static void AssertTargetProjection(
        JsonElement item,
        ExpectedTargetBinding expected,
        bool includeIndex,
        bool requireExactKeys = true)
    {
        if (requireExactKeys)
        {
            AssertKeys(
                item,
                includeIndex
                    ? new[] { "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash" }
                    : new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" });
        }

        if (includeIndex)
        {
            Assert.Equal(expected.InventoryIndex, item.GetProperty("inventory_index").GetInt32());
        }

        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
        Assert.Equal(expected.Kind, RequiredString(item, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(item, "path"));
        AssertSha256(RequiredString(item, "symbol_hash"));
        AssertSha256(RequiredString(item, "signature_hash"));
        AssertSha256(RequiredString(item, "body_hash"));
    }

    private static void ValidateConsumerContract(
        JsonElement contract,
        IReadOnlyList<TargetBinding> targets)
    {
        AssertKeys(
            contract,
            "adaptations",
            "adjacent_policy",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classification_counts",
            "classifications",
            "closure",
            "coverage_by_subfamily",
            "coverage_by_symbol",
            "evidence_contract",
            "native_routes",
            "runtime_signatures");
        Assert.Equal(19, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));

        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(7, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(41, counts.GetProperty("exception").GetInt32());

        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement nativeRoutes = contract.GetProperty("native_routes");
        JsonElement signatures = contract.GetProperty("runtime_signatures");
        AssertKeys(assertions, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(classifications, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(nativeRoutes, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(signatures, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(adaptations, ExpectedTargets.Select(item => item.Symbol).ToArray());
        foreach (ExpectedTargetBinding expected in ExpectedTargets)
        {
            Assert.Equal(expected.AssertionId, RequiredString(assertions, expected.Symbol));
            Assert.Equal(expected.Classification, RequiredString(classifications, expected.Symbol));
            Assert.Equal(expected.NativeRoute, RequiredString(nativeRoutes, expected.Symbol));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(signatures, expected.Symbol)));
            Assert.Equal(expected.AdaptationId, RequiredString(adaptations, expected.Symbol));
            if (expected.Classification == "equivalent")
            {
                Assert.StartsWith("direct-native-", expected.AdaptationId, StringComparison.Ordinal);
            }
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "exact_one_case_target_partition",
            "excluded_indices",
            "excluded_symbols",
            "full_source_classification_partition",
            "target_count",
            "target_indices",
            "target_symbols");
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_source_classification_partition").GetBoolean());
        Assert.Equal(48, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("target_indices")));
        AssertStringArray(closure.GetProperty("target_symbols"), ExpectedTargets.Select(item => item.Symbol));
        Assert.Equal(ExcludedIndices, ReadIntArray(closure.GetProperty("excluded_indices")));
        AssertStringArray(closure.GetProperty("excluded_symbols"), ExcludedSymbols);

        JsonElement coverage = contract.GetProperty("coverage_by_symbol");
        AssertKeys(coverage, ExpectedTargets.Select(item => item.Symbol).ToArray());
        foreach (ExpectedTargetBinding expected in ExpectedTargets)
        {
            AssertStringArray(
                coverage.GetProperty(expected.Symbol),
                Cases[expected.CaseIndex].CaseId);
        }

        JsonElement subfamilies = contract.GetProperty("coverage_by_subfamily");
        AssertKeys(subfamilies, "fenestration", "material", "relocation", "special", "surface");
        foreach (IGrouping<string, CaseBinding> group in Cases.GroupBy(item => item.Subfamily))
        {
            AssertStringArray(subfamilies.GetProperty(group.Key), group.Select(item => item.CaseId));
        }

        AssertStringArray(
            contract.GetProperty("adjacent_policy"),
            "existing equality hash representation and string scope decisions remain unchanged",
            "no excluded symbol appears in target or context coverage",
            "object identity is observed only as boolean alias topology and never promoted",
            "raw memory addresses are normalized and rejected from persisted facts");
        JsonElement evidence = contract.GetProperty("evidence_contract");
        AssertKeys(
            evidence,
            "expected_receipt_count",
            "full_idf_emission_closure",
            "target_coverage_complete");
        Assert.Equal(48, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.False(evidence.GetProperty("full_idf_emission_closure").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());

        Assert.Equal(ExpectedTargets.Select(item => item.AssertionId), targets.Select(item => item.AssertionId));
        Assert.Equal(ExpectedTargets.Select(item => item.Classification), targets.Select(item => item.Classification));
        Assert.Equal(ExpectedTargets.Select(item => item.AdaptationId), targets.Select(item => item.AdaptationId));
        Assert.Equal(ExpectedTargets.Select(item => item.NativeRoute), targets.Select(item => item.NativeRoute));
        Assert.Equal(48, Cases.Sum(item => item.TargetSymbols.Length));
        Assert.All(ExpectedTargets, expected => Assert.Contains(
            expected.Symbol,
            Cases[expected.CaseIndex].TargetSymbols));
    }

    private static void ValidateExcludedReceipts(
        JsonElement excludedReceipts,
        JsonElement adjacentExclusions,
        IReadOnlyList<TargetBinding> targets)
    {
        JsonElement[] excluded = excludedReceipts.EnumerateArray().ToArray();
        JsonElement[] adjacent = adjacentExclusions.EnumerateArray().ToArray();
        Assert.Equal(12, excluded.Length);
        Assert.Equal(12, adjacent.Length);

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        JsonElement inventorySymbols = inventory.RootElement.GetProperty("symbols");
        for (int index = 0; index < excluded.Length; index++)
        {
            JsonElement item = excluded[index];
            AssertKeys(item, "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(ExcludedIndices[index], item.GetProperty("inventory_index").GetInt32());
            Assert.Equal(ExcludedSymbols[index], RequiredString(item, "symbol"));
            Assert.Equal("function", RequiredString(item, "kind"));
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(CanonicalJson(item), CanonicalJson(adjacent[index]));

            JsonElement inventoryItem = inventorySymbols[ExcludedIndices[index]];
            foreach (string field in new[] { "symbol", "kind", "path", "symbol_hash", "signature_hash", "body_hash" })
            {
                Assert.Equal(RequiredString(inventoryItem, field), RequiredString(item, field));
            }

            Assert.DoesNotContain(targets, target => target.InventoryIndex == ExcludedIndices[index]);
            Assert.DoesNotContain(targets, target => target.Symbol == ExcludedSymbols[index]);
            Assert.DoesNotContain(Cases.SelectMany(value => value.TargetSymbols), symbol => symbol == ExcludedSymbols[index]);
            Assert.DoesNotContain(Cases.SelectMany(value => value.ContextSymbols), symbol => symbol == ExcludedSymbols[index]);
        }
    }

    private static void ValidateArtifactProjection(
        JsonElement artifact,
        string path,
        int bytes,
        string sha256)
    {
        AssertKeys(artifact, "bytes", "path", "sha256");
        Assert.Equal(path, RequiredString(artifact, "path"));
        Assert.Equal(bytes, artifact.GetProperty("bytes").GetInt32());
        Assert.Equal(sha256, RequiredString(artifact, "sha256"));
    }

    private static NativeObservation ObserveNativeCase(int index) => index switch
    {
        0 => ObserveM01(),
        1 => ObserveM02(),
        2 => ObserveM03(),
        3 => ObserveM04(),
        4 => ObserveF01(),
        5 => ObserveF02(),
        6 => ObserveF03(),
        7 => ObserveF04(),
        8 => ObserveS01(),
        9 => ObserveS02(),
        10 => ObserveS03(),
        11 => ObserveS04(),
        12 => ObserveS05(),
        13 => ObserveS06(),
        14 => ObserveS07(),
        15 => ObserveX01(),
        16 => ObserveX02(),
        17 => ObserveX03(),
        18 => ObserveR01(),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
    };

    private static NativeObservation ObserveM01()
    {
        var explicitMaterial = new Material(
            "Explicit material",
            0.4d,
            800d,
            900d,
            Id("MAT-EXPLICIT"));
        var automatic = new Material("Automatic material", 0.25d, 40d, 1_400d);
        var repeated = new Material("Automatic material", 0.25d, 40d, 1_400d);
        Exception blankName = Assert.Throws<ArgumentException>(
            () => new Material(" ", 0.4d, 800d, 900d));

        Assert.Equal("MAT-EXPLICIT", explicitMaterial.Id.Value);
        Assert.Equal("Explicit material", explicitMaterial.Name);
        Assert.Equal(0.4d, explicitMaterial.Conductivity);
        Assert.Equal(800d, explicitMaterial.Density);
        Assert.Equal(900d, explicitMaterial.SpecificHeat);
        Assert.StartsWith("MTRL-", automatic.Id.Value, StringComparison.Ordinal);
        Assert.Equal(automatic.Id, repeated.Id);
        return Observation(
            "M01",
            "native-route=Material-constructor-plus-EntityId",
            "explicit-id=MAT-EXPLICIT",
            "explicit-state=conductivity:0.4|density:800|specific-heat:900",
            "automatic-id-prefix=MTRL-",
            "automatic-id-repeat-stable=true",
            "native-constructor-properties=get-only",
            ExceptionFact("blank-name", blankName),
            "adaptation=native-name-is-required-and-id-is-deterministic");
    }

    private static NativeObservation ObserveM02()
    {
        var material = new Material("Validated material", 0.4d, 800d, 900d, Id("MAT-VALID"));
        Exception zeroConductivity = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Material("zero conductivity", 0d, 800d, 900d));
        Exception nanDensity = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Material("nan density", 0.4d, double.NaN, 900d));
        Exception infiniteDensity = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Material("infinite density", 0.4d, double.PositiveInfinity, 900d));
        Exception lowSpecificHeat = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Material("low heat", 0.4d, 800d, 99d));

        Assert.Equal(0.4d, material.Conductivity);
        Assert.Equal(800d, material.Density);
        Assert.Equal(900d, material.SpecificHeat);
        Assert.All(
            new[]
            {
                nameof(Material.Conductivity),
                nameof(Material.Density),
                nameof(Material.SpecificHeat),
            },
            property => Assert.False(typeof(Material).GetProperty(property)!.CanWrite));
        return Observation(
            "M02",
            "native-route=Material-validation-plus-get-only-properties",
            "valid-state=0.4|800|900",
            "mutation-surface=none",
            ExceptionFact("zero-conductivity", zeroConductivity),
            ExceptionFact("nan-density", nanDensity),
            ExceptionFact("infinite-density", infiniteDensity),
            ExceptionFact("low-specific-heat", lowSpecificHeat),
            "adaptation=native-rejects-nonfinite-values-and-is-immutable");
    }

    private static NativeObservation ObserveM03()
    {
        NativeGraph graph = CreateNativeGraph();
        string json = GrmWriter.Serialize(graph.Model, indented: false);
        GreenRetrofitModel reread = ReadRoundTrip(json);
        Material parsed = Assert.Single(reread.Materials, item => item.Id.Equals(graph.MaterialA.Id));
        GreenRetrofitConversionResult conversion = Convert(graph.Model);
        var convertedLayer = conversion.RequireEnergyModel().UsedLayers
            .Single(item => item.Material.Name == graph.MaterialA.Id.Value);

        Assert.Contains("\"materials\"", json, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"MAT-A\"", json, StringComparison.Ordinal);
        Assert.Equal(graph.MaterialA.Conductivity, parsed.Conductivity);
        Assert.Equal(graph.MaterialA.Density, parsed.Density);
        Assert.Equal(graph.MaterialA.SpecificHeat, parsed.SpecificHeat);
        Assert.Equal(graph.MaterialA.Conductivity, convertedLayer.Material.ConductivityWattsPerMetreKelvin);
        Assert.Equal(graph.MaterialA.Density, convertedLayer.Material.DensityKilogramsPerCubicMetre);
        Assert.Equal(graph.MaterialA.SpecificHeat, convertedLayer.Material.SpecificHeatJoulesPerKilogramKelvin);
        return Observation(
            "M03",
            "native-route=GrmWriter.Serialize-plus-GrmReader.Read-plus-GreenRetrofitConverter.Convert",
            "wire-material-id=MAT-A",
            "wire-material-state=conductivity:0.5|density:800|specific-heat:1000",
            "reader-material-reference-resolved=true",
            "converter-material-name=MAT-A",
            "converter-material-properties-preserved=true",
            "roundtrip-canonical=true",
            "adaptation=aggregate-native-serialization-and-conversion-routes");
    }

    private static NativeObservation ObserveM04()
    {
        SimpleDragonDatabase database = SimpleDragonDatabase.LoadEmbedded();
        Material first = Assert.IsType<Material>(database.Materials.Items[0]);
        LookupResult<Material> found = database.Materials.Find(first.Name);
        LookupResult<Material> trimmed = database.Materials.Find("  " + first.Name + "  ");
        LookupResult<Material> missing = database.Materials.Find("missing-material");
        LookupResult<Material> empty = database.Materials.Find(null);

        Assert.NotEmpty(database.Materials.Items);
        Assert.Same(first, found.Require());
        Assert.Same(first, trimmed.Require());
        Assert.False(missing.Found);
        Assert.Equal("SD.DB.MATERIAL_NOT_FOUND", Assert.Single(missing.Diagnostics).Code);
        Assert.False(empty.Found);
        Assert.Equal("SD.DB.MATERIAL_NOT_FOUND", Assert.Single(empty.Diagnostics).Code);
        return Observation(
            "M04",
            "native-route=SimpleDragonDatabase.LoadEmbedded-plus-MaterialDatabase.Items-and-Find",
            "embedded-material-count-positive=true",
            "find-first-found=true",
            "find-first-reference-identical=true",
            "trimmed-key-found=true",
            "missing-code=SD.DB.MATERIAL_NOT_FOUND",
            "empty-code=SD.DB.MATERIAL_NOT_FOUND",
            "adaptation=typed-lookup-result-rather-than-polymorphic-as-dict-return");
    }

    private static NativeObservation ObserveF01()
    {
        var explicitConstruction = new FenestrationConstruction(
            "Explicit glazing",
            1.4d,
            0.5d,
            Id("FC-EXPLICIT"));
        var automatic = new FenestrationConstruction("Automatic glazing", 1.6d, 0.45d);
        var repeated = new FenestrationConstruction("Automatic glazing", 1.6d, 0.45d);
        Exception blankName = Assert.Throws<ArgumentException>(
            () => new FenestrationConstruction(" ", 1.4d, 0.5d));

        Assert.Equal("FC-EXPLICIT", explicitConstruction.Id.Value);
        Assert.Equal("Explicit glazing", explicitConstruction.Name);
        Assert.Equal(1.4d, explicitConstruction.UValue);
        Assert.Equal(0.5d, explicitConstruction.SolarHeatGainCoefficient);
        Assert.StartsWith("CTFN-", automatic.Id.Value, StringComparison.Ordinal);
        Assert.Equal(automatic.Id, repeated.Id);
        return Observation(
            "F01",
            "native-route=FenestrationConstruction-constructor-plus-EntityId",
            "explicit-id=FC-EXPLICIT",
            "explicit-state=u:1.4|shgc:0.5|transparent:true",
            "automatic-id-prefix=CTFN-",
            "automatic-id-repeat-stable=true",
            ExceptionFact("blank-name", blankName),
            "adaptation=native-name-is-required-and-state-is-immutable");
    }

    private static NativeObservation ObserveF02()
    {
        var transparent = new FenestrationConstruction("Transparent", 1.4d, 0.5d, Id("FC-T"));
        var opaque = new FenestrationConstruction("Opaque", 2.2d, id: Id("FC-O"));
        Exception zeroU = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FenestrationConstruction("zero u", 0d, 0.5d));
        Exception nanU = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FenestrationConstruction("nan u", double.NaN, 0.5d));
        Exception zeroShgc = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FenestrationConstruction("zero shgc", 1.4d, 0d));
        Exception oneShgc = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FenestrationConstruction("one shgc", 1.4d, 1d));

        Assert.True(transparent.IsTransparent);
        Assert.False(opaque.IsTransparent);
        Assert.Equal(1.4d, transparent.UValue);
        Assert.Equal(0.5d, transparent.SolarHeatGainCoefficient);
        Assert.Null(opaque.SolarHeatGainCoefficient);
        return Observation(
            "F02",
            "native-route=FenestrationConstruction-properties-and-validation",
            "transparent-state=u:1.4|shgc:0.5|transparent:true",
            "opaque-state=u:2.2|shgc:null|transparent:false",
            ExceptionFact("zero-u", zeroU),
            ExceptionFact("nan-u", nanU),
            ExceptionFact("zero-shgc", zeroShgc),
            ExceptionFact("one-shgc", oneShgc),
            "adaptation=native-shgc-is-nullable-and-strictly-bounded");
    }

    private static NativeObservation ObserveF03()
    {
        NativeGraph graph = CreateNativeGraph();
        string json = GrmWriter.Serialize(graph.Model, indented: false);
        GreenRetrofitModel reread = ReadRoundTrip(json);
        FenestrationConstruction glazing = Assert.Single(
            reread.FenestrationConstructions,
            item => item.Id.Equals(graph.TransparentFenestrationConstruction.Id));
        FenestrationConstruction door = Assert.Single(
            reread.FenestrationConstructions,
            item => item.Id.Equals(graph.OpaqueFenestrationConstruction.Id));
        DragonSurface convertedWall = ConvertedSurface(Convert(graph.Model), graph.DefinedWall.Id);
        DragonGlazing convertedGlazing = Assert.IsType<DragonGlazing>(
            Assert.Single(convertedWall.Windows).Glazing);
        DragonNoMassConstruction convertedDoor = Assert.IsType<DragonNoMassConstruction>(
            Assert.Single(convertedWall.Doors).Construction);

        Assert.Contains("\"fenestration_constructions\"", json, StringComparison.Ordinal);
        Assert.True(glazing.IsTransparent);
        Assert.False(door.IsTransparent);
        Assert.Equal(graph.TransparentFenestrationConstruction.UValue, convertedGlazing.UValueWattsPerSquareMetreKelvin);
        Assert.Equal(graph.TransparentFenestrationConstruction.SolarHeatGainCoefficient, convertedGlazing.SolarHeatGainCoefficient);
        Assert.Equal(graph.OpaqueFenestrationConstruction.UValue, convertedDoor.UValueWattsPerSquareMetreKelvin);
        return Observation(
            "F03",
            "native-route=GrmWriter.Serialize-plus-GrmReader.Read-plus-GreenRetrofitConverter.Convert",
            "wire-fenestration-construction-count=2",
            "reader-transparent-id=FC-G",
            "reader-opaque-id=FC-D",
            "converter-window-construction=Glazing",
            "converter-door-construction=NoMassConstruction",
            "converter-thermal-values-preserved=true",
            "adaptation=aggregate-native-serialization-and-conversion-routes");
    }

    private static NativeObservation ObserveF04()
    {
        SimpleDragonDatabase database = SimpleDragonDatabase.LoadEmbedded();
        FenestrationConstructionEntry first = database.FenestrationConstructions.Entries[0];
        LookupResult<FenestrationConstruction> found = database.FenestrationConstructions.Find(first.Key);
        LookupResult<FenestrationConstruction> missing = database.FenestrationConstructions.Find(null);

        Assert.NotEmpty(database.FenestrationConstructions.Entries);
        Assert.Same(first.Construction, found.Require());
        Assert.False(missing.Found);
        Assert.Equal("SD.DB.FENESTRATION_CONSTRUCTION_NOT_FOUND", Assert.Single(missing.Diagnostics).Code);
        Assert.True(first.Construction.UValue > 0d);
        Assert.True(first.Construction.SolarHeatGainCoefficient is > 0d and < 1d);
        return Observation(
            "F04",
            "native-route=SimpleDragonDatabase.LoadEmbedded-plus-FenestrationConstructionDatabase.Entries-and-Find",
            "embedded-fenestration-count-positive=true",
            "find-first-found=true",
            "find-first-reference-identical=true",
            "first-u-positive=true",
            "first-shgc-strictly-bounded=true",
            "missing-code=SD.DB.FENESTRATION_CONSTRUCTION_NOT_FOUND",
            "adaptation=typed-key-and-lookup-result-rather-than-polymorphic-string-return");
    }

    private static NativeObservation ObserveS01()
    {
        var materialA = new Material("Layer A", 0.5d, 800d, 1_000d, Id("MAT-LAYER-A"));
        var materialB = new Material("Layer B", 0.25d, 40d, 1_400d, Id("MAT-LAYER-B"));
        var layers = new[]
        {
            new SurfaceConstructionLayer(materialA, 0.2d),
            new SurfaceConstructionLayer(materialB, 0.08d),
        };
        var construction = new SurfaceConstruction("Explicit assembly", layers, Id("SC-EXPLICIT"));
        var automatic = new SurfaceConstruction("Automatic assembly", layers);
        var repeated = new SurfaceConstruction("Automatic assembly", layers);
        Exception empty = Assert.Throws<ArgumentException>(
            () => new SurfaceConstruction("empty", Array.Empty<SurfaceConstructionLayer>()));
        Exception nullLayer = Assert.Throws<ArgumentException>(
            () => new SurfaceConstruction("null layer", new SurfaceConstructionLayer[] { null! }));
        Exception zeroThickness = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SurfaceConstructionLayer(materialA, 0d));

        Assert.Equal("SC-EXPLICIT", construction.Id.Value);
        Assert.Equal(new[] { materialA, materialB }, construction.Layers.Select(item => item.Material));
        Assert.Equal(new[] { 0.2d, 0.08d }, construction.Layers.Select(item => item.Thickness));
        Assert.Equal(automatic.Id, repeated.Id);
        IList<SurfaceConstructionLayer> mutableView = Assert.IsAssignableFrom<IList<SurfaceConstructionLayer>>(
            construction.Layers);
        Assert.True(mutableView.IsReadOnly);
        return Observation(
            "S01",
            "native-route=SurfaceConstructionLayer-plus-SurfaceConstruction-constructor",
            "explicit-id=SC-EXPLICIT",
            "layer-order=MAT-LAYER-A|MAT-LAYER-B",
            "layer-thicknesses=0.2|0.08",
            "layers-read-only=true",
            "automatic-id-repeat-stable=true",
            ExceptionFact("empty-layers", empty),
            ExceptionFact("null-layer", nullLayer),
            ExceptionFact("zero-thickness", zeroThickness),
            "adaptation=native-constructor-rejects-rather-than-filters-invalid-layers");
    }

    private static NativeObservation ObserveS02()
    {
        var materialA = new Material("A", 0.5d, 800d, 1_000d, Id("MAT-A-DERIVED"));
        var materialB = new Material("B", 0.25d, 40d, 1_400d, Id("MAT-B-DERIVED"));
        var construction = new SurfaceConstruction(
            "Derived",
            new[]
            {
                new SurfaceConstructionLayer(materialA, 0.2d),
                new SurfaceConstructionLayer(materialB, 0.08d),
                new SurfaceConstructionLayer(materialA, 0.05d),
            },
            Id("SC-DERIVED"));
        double expectedInternal = 1d / ((0.2d / 0.5d) + (0.08d / 0.25d) + (0.05d / 0.5d));
        double expectedHeatCapacity = (800d * 1_000d * 0.2d)
            + (40d * 1_400d * 0.08d)
            + (800d * 1_000d * 0.05d);
        double expectedU = 1d / ((1d / 7.7d) + (1d / 25d) + (1d / expectedInternal));
        Exception zeroInterior = Assert.Throws<ArgumentOutOfRangeException>(
            () => construction.GetUValue(0d, 25d));
        Exception nanExterior = Assert.Throws<ArgumentOutOfRangeException>(
            () => construction.GetUValue(7.7d, double.NaN));

        Assert.Equal(expectedInternal, construction.InternalUValue, 12);
        Assert.Equal(0.33d, construction.Depth, 12);
        Assert.Equal(expectedHeatCapacity, construction.HeatCapacity, 8);
        Assert.Equal(expectedU, construction.GetUValue(7.7d, 25d), 12);
        Assert.Equal(2, construction.Layers.Select(item => item.Material)
            .Distinct().Count());
        return Observation(
            "S02",
            "native-route=SurfaceConstruction-derived-properties-and-GetUValue",
            "internal-u=" + Double(construction.InternalUValue),
            "depth=0.33",
            "heat-capacity=" + Double(construction.HeatCapacity),
            "u-with-films=" + Double(construction.GetUValue(7.7d, 25d)),
            "unique-material-count=2",
            ExceptionFact("zero-interior-convection", zeroInterior),
            ExceptionFact("nan-exterior-convection", nanExterior));
    }

    private static NativeObservation ObserveS03()
    {
        var insulation = new Material("Insulation", 0.04d, 30d, 1_400d, Id("MAT-INS"));
        var concrete = new Material("Concrete", 1.4d, 2_300d, 880d, Id("MAT-CON"));
        SurfaceConstruction insulated = SurfaceConstruction.CreateSimple(
            "Insulated",
            0.3d,
            insulation,
            concrete,
            interiorConvection: 7.7d,
            exteriorConvection: 25d,
            id: Id("SC-SIMPLE-I"));
        SurfaceConstruction concreteOnly = SurfaceConstruction.CreateSimple(
            "Concrete only",
            4d,
            insulation,
            concrete,
            interiorConvection: 7.7d,
            exteriorConvection: 25d,
            id: Id("SC-SIMPLE-C"));
        Exception zeroU = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurfaceConstruction.CreateSimple("zero", 0d, insulation, concrete));
        Exception overLimit = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurfaceConstruction.CreateSimple(
                "over limit",
                6d,
                insulation,
                concrete,
                interiorConvection: 7.7d,
                exteriorConvection: 25d));

        Assert.Equal(2, insulated.Layers.Count);
        Assert.Same(insulation, insulated.Layers[0].Material);
        Assert.Same(concrete, insulated.Layers[1].Material);
        Assert.Single(concreteOnly.Layers);
        Assert.Same(concrete, concreteOnly.Layers[0].Material);
        Assert.Equal(0.3d, insulated.GetUValue(7.7d, 25d), 12);
        Assert.Equal(4d, concreteOnly.GetUValue(7.7d, 25d), 12);
        return Observation(
            "S03",
            "native-route=SurfaceConstruction.CreateSimple",
            "insulated-branch-layer-count=2",
            "insulated-branch-order=MAT-INS|MAT-CON",
            "insulated-branch-u=0.3",
            "concrete-only-branch-layer-count=1",
            "concrete-only-branch-u=4",
            ExceptionFact("zero-u", zeroU),
            ExceptionFact("zero-thickness-limit", overLimit),
            "adaptation=materials-are-explicit-native-arguments-rather-than-hidden-db-lookups");
    }

    private static NativeObservation ObserveS04()
    {
        NativeGraph graph = CreateNativeGraph();
        SurfaceConstruction reversed = graph.SurfaceConstruction.Reverse();
        SurfaceConstruction repeated = graph.SurfaceConstruction.Reverse();
        string json = GrmWriter.Serialize(graph.Model, indented: false);

        Assert.Equal("Construction A_reversed", reversed.Name);
        Assert.Equal(graph.SurfaceConstruction.Layers.Reverse().Select(item => item.Material.Id),
            reversed.Layers.Select(item => item.Material.Id));
        Assert.Equal(graph.SurfaceConstruction.Layers.Reverse().Select(item => item.Thickness),
            reversed.Layers.Select(item => item.Thickness));
        Assert.Equal(reversed.Id, repeated.Id);
        Assert.Contains("\"surface_constructions\"", json, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"SC-A\"", json, StringComparison.Ordinal);
        Assert.Contains("\"material_id\":\"MAT-A\"", json, StringComparison.Ordinal);
        return Observation(
            "S04",
            "native-route=SurfaceConstruction.Reverse-plus-GrmWriter.Serialize",
            "reversed-name=Construction A_reversed",
            "reversed-layer-order=MAT-B|MAT-A",
            "reversed-thickness-order=0.08|0.2",
            "reversed-id-repeat-stable=true",
            "wire-surface-construction-id=SC-A",
            "wire-layer-material-order=MAT-A|MAT-B",
            "adaptation=reverse-is-pure-and-deterministically-reidentified");
    }

    private static NativeObservation ObserveS05()
    {
        NativeGraph graph = CreateNativeGraph();
        string json = GrmWriter.Serialize(graph.Model, indented: false);
        GreenRetrofitModel reread = ReadRoundTrip(json);
        SurfaceConstruction parsed = Assert.Single(
            reread.SurfaceConstructions,
            item => item.Id.Equals(graph.SurfaceConstruction.Id));
        DragonSurface convertedWall = ConvertedSurface(Convert(reread), graph.DefinedWall.Id);
        DragonConstruction converted = Assert.IsType<DragonConstruction>(convertedWall.Construction);

        Assert.Equal(graph.SurfaceConstruction.Layers.Count, parsed.Layers.Count);
        Assert.Equal(graph.SurfaceConstruction.Layers.Select(item => item.Material.Id),
            parsed.Layers.Select(item => item.Material.Id));
        Assert.Equal(graph.SurfaceConstruction.Layers.Select(item => item.Thickness),
            parsed.Layers.Select(item => item.Thickness));
        Assert.Equal(graph.SurfaceConstruction.Id.Value, converted.Name);
        Assert.Equal(graph.SurfaceConstruction.Layers.Count, converted.Layers.Count);
        Assert.Equal(graph.SurfaceConstruction.InternalUValue, converted.UValue, 12);
        return Observation(
            "S05",
            "native-route=GrmWriter.Serialize-plus-GrmReader.Read-plus-GreenRetrofitConverter.Convert",
            "reader-construction-id=SC-A",
            "reader-layer-order=MAT-A|MAT-B",
            "reader-thickness-order=0.2|0.08",
            "converter-construction-name=SC-A",
            "converter-layer-count=2",
            "converter-u-preserved=true",
            "adaptation=aggregate-native-reader-and-converter-routes");
    }

    private static NativeObservation ObserveS06()
    {
        SimpleDragonDatabase database = SimpleDragonDatabase.LoadEmbedded();
        SurfaceConstructionEntry first = database.SurfaceConstructions.Entries[0];
        LookupResult<SurfaceConstruction> found = database.SurfaceConstructions.Find(first.Key);
        LookupResult<SurfaceConstruction> missing = database.SurfaceConstructions.Find(null);

        Assert.NotEmpty(database.SurfaceConstructions.Entries);
        Assert.NotEmpty(database.SurfaceConstructions.RegulationDates);
        Assert.Same(first.Construction, found.Require());
        Assert.False(missing.Found);
        Assert.Equal("SD.DB.SURFACE_CONSTRUCTION_NOT_FOUND", Assert.Single(missing.Diagnostics).Code);
        Assert.Equal(first.RegulatedUValue, first.Construction.GetUValue(), 10);
        return Observation(
            "S06",
            "native-route=SimpleDragonDatabase.LoadEmbedded-plus-SurfaceConstructionDatabase.Entries-and-Find",
            "embedded-surface-count-positive=true",
            "regulation-date-count-positive=true",
            "find-first-found=true",
            "find-first-reference-identical=true",
            "regulated-u-preserved=true",
            "missing-code=SD.DB.SURFACE_CONSTRUCTION_NOT_FOUND",
            "adaptation=typed-regulation-key-and-lookup-result");
    }

    private static NativeObservation ObserveS07()
    {
        SurfaceConstructionDatabase database = SimpleDragonDatabase.LoadEmbedded().SurfaceConstructions;
        string climate = database.Entries[0].Key.ClimateRegion;
        LookupResult<SurfaceConstruction>? successful = null;
        foreach (DateTime vintage in database.RegulationDates.Reverse())
        {
            foreach (SurfaceType type in Enum.GetValues<SurfaceType>())
            {
                foreach (SurfaceBoundaryCondition boundary in Enum.GetValues<SurfaceBoundaryCondition>())
                {
                    foreach (bool radiant in new[] { false, true })
                    {
                        foreach (bool multifamily in new[] { false, true })
                        {
                            LookupResult<SurfaceConstruction> candidate = database.FindRegulated(
                                vintage,
                                type,
                                boundary,
                                climate,
                                radiant,
                                multifamily);
                            if (candidate.Found)
                            {
                                successful = candidate;
                                break;
                            }
                        }

                        if (successful is not null)
                        {
                            break;
                        }
                    }

                    if (successful is not null)
                    {
                        break;
                    }
                }

                if (successful is not null)
                {
                    break;
                }
            }

            if (successful is not null)
            {
                break;
            }
        }

        Assert.NotNull(successful);
        Assert.True(successful.Found);
        Assert.True(successful.Require().GetUValue() > 0d);
        LookupResult<SurfaceConstruction> early = database.FindRegulated(
            DateTime.MinValue,
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            climate);
        LookupResult<SurfaceConstruction> missingClimate = database.FindRegulated(
            database.RegulationDates[^1],
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            null);
        Assert.False(early.Found);
        Assert.Equal("SD.DB.SURFACE_VINTAGE_NOT_COVERED", Assert.Single(early.Diagnostics).Code);
        Assert.False(missingClimate.Found);
        Assert.Equal("SD.DB.CLIMATE_REGION_REQUIRED", Assert.Single(missingClimate.Diagnostics).Code);
        return Observation(
            "S07",
            "native-route=SurfaceConstructionDatabase.FindRegulated",
            "packaged-climate-selection-found=true",
            "selected-construction-u-positive=true",
            "selection-search-space=dates|surface-types|boundaries|radiant|housing",
            "early-vintage-code=SD.DB.SURFACE_VINTAGE_NOT_COVERED",
            "missing-climate-code=SD.DB.CLIMATE_REGION_REQUIRED",
            "adaptation=typed-selection-result-with-stable-diagnostics");
    }

    private static NativeObservation ObserveX01()
    {
        NativeGraph graph = CreateNativeGraph();
        Surface open = graph.OpenSurface;
        Surface unknown = graph.UnknownSurface;
        Surface unresolved = new(
            "Unresolved",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Adiabatic,
            4d,
            null,
            "SC-MISSING",
            null,
            id: Id("SURF-UNRESOLVED"));
        Surface flippedOpen = open.Flip();
        Surface flippedUnknown = unknown.Flip();

        Assert.Equal(SurfaceConstructionReferenceKind.Open, open.ConstructionReferenceKind);
        Assert.Equal(SurfaceConstructionReferenceKind.Unknown, unknown.ConstructionReferenceKind);
        Assert.Equal(SurfaceConstructionReferenceKind.Unresolved, unresolved.ConstructionReferenceKind);
        Assert.Null(open.Construction);
        Assert.Null(unknown.Construction);
        Assert.Null(unresolved.Construction);
        Assert.Equal(SurfaceConstructionReferenceKind.Open, flippedOpen.ConstructionReferenceKind);
        Assert.Equal(SurfaceConstructionReferenceKind.Unknown, flippedUnknown.ConstructionReferenceKind);
        Assert.Equal(new[] { "Defined", "Unknown", "Open", "Unresolved" },
            Enum.GetNames<SurfaceConstructionReferenceKind>());
        return Observation(
            "X01",
            "native-route=SurfaceConstructionReferenceKind-plus-Surface.Flip",
            "reference-kinds=Defined|Unknown|Open|Unresolved",
            "special-construction-references-null=true",
            "open-kind=Open",
            "unknown-kind=Unknown",
            "unresolved-kind=Unresolved",
            "flipped-open-kind=Open",
            "flipped-unknown-kind=Unknown",
            "adaptation=special-values-are-surface-reference-kinds-not-construction-singletons");
    }

    private static NativeObservation ObserveX02()
    {
        NativeGraph graph = CreateNativeGraph();
        GreenRetrofitConversionResult conversion = Convert(graph.Model);
        DragonSurface converted = ConvertedSurface(conversion, graph.OpenSurface.Id);
        DragonAirBoundary airBoundary = Assert.IsType<DragonAirBoundary>(converted.Construction);

        Assert.Equal("open", graph.OpenSurface.ConstructionId);
        Assert.Equal(SurfaceConstructionReferenceKind.Open, graph.OpenSurface.ConstructionReferenceKind);
        Assert.Equal("DefaultAirBoundary", airBoundary.Name);
        return Observation(
            "X02",
            "native-route=SurfaceConstructionReferenceKind.Open-plus-GreenRetrofitConverter.Convert",
            "source-construction-id=open",
            "source-reference-kind=Open",
            "source-construction-null=true",
            "converted-construction-type=AirBoundary",
            "converted-construction-name=DefaultAirBoundary",
            "equivalent=open-construction-converts-to-air-boundary");
    }

    private static NativeObservation ObserveX03()
    {
        NativeGraph graph = CreateNativeGraph();
        GreenRetrofitConversionResult conversion = Convert(graph.SpecialModel);
        DragonSurface converted = ConvertedSurface(conversion, graph.UnknownSurface.Id);
        DragonConstruction resolved = Assert.IsType<DragonConstruction>(converted.Construction);

        Assert.Null(graph.UnknownSurface.ConstructionId);
        Assert.Null(graph.UnknownSurface.Construction);
        Assert.Equal(SurfaceConstructionReferenceKind.Unknown, graph.UnknownSurface.ConstructionReferenceKind);
        Assert.Equal("SimpleDragon:DefaultInnerWallConstruction", resolved.Name);
        Assert.Equal(3, resolved.Layers.Count);
        return Observation(
            "X03",
            "native-route=SurfaceConstructionReferenceKind.Unknown-plus-GreenRetrofitConverter.ResolveUnknownConstruction",
            "source-construction-id=null",
            "source-reference-kind=Unknown",
            "source-construction-null=true",
            "resolved-construction-type=Construction",
            "resolved-construction-name=SimpleDragon:DefaultInnerWallConstruction",
            "resolved-layer-count=3",
            "adaptation=native-resolves-unknown-interior-wall-through-reviewed-policy");
    }

    private static NativeObservation ObserveR01()
    {
        SimpleDragonDatabase first = SimpleDragonDatabase.LoadEmbedded();
        SimpleDragonDatabase second = SimpleDragonDatabase.LoadEmbedded();
        Assert.NotSame(first, second);
        Assert.Equal(first.Materials.Items.Select(item => item.Id),
            second.Materials.Items.Select(item => item.Id));
        Assert.Equal(first.SurfaceConstructions.Entries.Select(item => item.Construction.Id),
            second.SurfaceConstructions.Entries.Select(item => item.Construction.Id));
        Assert.Equal(first.FenestrationConstructions.Entries.Select(item => item.Construction.Id),
            second.FenestrationConstructions.Entries.Select(item => item.Construction.Id));
        Assert.Equal(first.Materials.Items.Count, second.Materials.Items.Count);
        Assert.Equal(first.SurfaceConstructions.Entries.Count, second.SurfaceConstructions.Entries.Count);
        Assert.Equal(first.FenestrationConstructions.Entries.Count, second.FenestrationConstructions.Entries.Count);
        return Observation(
            "R01",
            "native-route=two-independent-SimpleDragonDatabase.LoadEmbedded-calls",
            "database-aggregate-reference-distinct=true",
            "material-id-sequence-identical=true",
            "surface-construction-id-sequence-identical=true",
            "fenestration-construction-id-sequence-identical=true",
            "material-count-identical=true",
            "surface-count-identical=true",
            "fenestration-count-identical=true",
            "adaptation=relocation-invariance-is-native-embedded-load-determinism");
    }

    private static NativeObservation Observation(string code, params string[] facts)
    {
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, item => Assert.False(string.IsNullOrWhiteSpace(item)));
        string hash = CanonicalSha256(JsonSerializer.SerializeToElement(facts));
        return new NativeObservation(code, facts, hash);
    }

    private static NativeGraph CreateNativeGraph()
    {
        var materialA = new Material("Material A", 0.5d, 800d, 1_000d, Id("MAT-A"));
        var materialB = new Material("Material B", 0.25d, 40d, 1_400d, Id("MAT-B"));
        var surfaceConstruction = new SurfaceConstruction(
            "Construction A",
            new[]
            {
                new SurfaceConstructionLayer(materialA, 0.2d),
                new SurfaceConstructionLayer(materialB, 0.08d),
            },
            Id("SC-A"));
        var transparent = new FenestrationConstruction("Glazing", 1.4d, 0.5d, Id("FC-G"));
        var opaque = new FenestrationConstruction("Door construction", 2.2d, id: Id("FC-D"));
        var window = new Fenestration(
            "Window",
            FenestrationType.Window,
            3d,
            transparent.Id.Value,
            transparent,
            BlindType.Shade,
            Id("FN-WINDOW"));
        var door = new Fenestration(
            "Door",
            FenestrationType.Door,
            2d,
            opaque.Id.Value,
            opaque,
            id: Id("FN-DOOR"));
        var definedWall = new Surface(
            "Defined wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            40d,
            180d,
            surfaceConstruction.Id.Value,
            surfaceConstruction,
            new[] { window, door },
            id: Id("SURF-DEFINED"));
        var floor = new Surface(
            "Ground floor",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            40d,
            null,
            surfaceConstruction.Id.Value,
            surfaceConstruction,
            id: Id("SURF-FLOOR"));
        var roof = new Surface(
            "Roof",
            SurfaceType.Ceiling,
            SurfaceBoundaryCondition.Outdoors,
            40d,
            null,
            surfaceConstruction.Id.Value,
            surfaceConstruction,
            id: Id("SURF-ROOF"));
        var open = new Surface(
            "Open special surface",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Adiabatic,
            4d,
            null,
            "open",
            null,
            id: Id("SURF-OPEN"));
        var unknown = new Surface(
            "Unknown interior wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.AdjacentSpace,
            8d,
            null,
            null,
            null,
            id: Id("SURF-UNKNOWN"));

        SimpleDragonDatabase database = SimpleDragonDatabase.Default;
        UsageProfile profile = database.UsageProfiles.Items[0];
        DateTime vintage = new(2020, 1, 1);
        WeatherMetadata metadata = database.Weather.Items[0];
        WeatherSelection weather = database.Weather.FindByAddress(
            metadata.AdministrativeArea,
            vintage).Require();
        var zone = new Zone(
            "Construction zone",
            1,
            3d,
            new[] { definedWall, floor, roof, open },
            profile.Name,
            profile,
            8d,
            id: Id("ZONE-CONSTRUCTION"));
        var model = new GreenRetrofitModel(
            "Construction Core Native Model",
            0d,
            metadata.AdministrativeArea,
            vintage,
            false,
            new[] { new BuildingFloor(1, new[] { zone }) },
            new[] { materialA, materialB },
            new[] { surfaceConstruction },
            new[] { transparent, opaque },
            weather: weather);
        var specialZone = new Zone(
            "Construction special zone",
            1,
            3d,
            new[] { definedWall, floor, roof, open, unknown },
            profile.Name,
            profile,
            8d,
            id: Id("ZONE-CONSTRUCTION-SPECIAL"));
        var specialModel = new GreenRetrofitModel(
            "Construction Core Special Model",
            0d,
            metadata.AdministrativeArea,
            vintage,
            false,
            new[] { new BuildingFloor(1, new[] { specialZone }) },
            new[] { materialA, materialB },
            new[] { surfaceConstruction },
            new[] { transparent, opaque },
            weather: weather);
        return new NativeGraph(
            materialA,
            materialB,
            surfaceConstruction,
            transparent,
            opaque,
            definedWall,
            open,
            unknown,
            model,
            specialModel);
    }

    private static GreenRetrofitModel ReadRoundTrip(string json)
    {
        GrmReadResult result = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(result.Success, Describe(result.Diagnostics));
        GreenRetrofitModel model = result.RequireModel();
        Assert.Equal(json, GrmWriter.Serialize(model, indented: false));
        return model;
    }

    private static GreenRetrofitConversionResult Convert(GreenRetrofitModel model)
    {
        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(
            model,
            new GreenRetrofitConversionOptions
            {
                Database = SimpleDragonDatabase.Default,
                ResolveUnknownConstructions = true,
                IncludeModelValidationDiagnostics = false,
            });
        Assert.True(result.Success, Describe(result.Diagnostics));
        Assert.NotNull(result.RequireEnergyModel());
        return result;
    }

    private static DragonSurface ConvertedSurface(
        GreenRetrofitConversionResult conversion,
        EntityId sourceId) => Assert.Single(
            conversion.RequireEnergyModel().Surfaces,
            item => item.Id.Equals(sourceId));

    private static object CreateReceipt(
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        NativeObservation observation = observations[target.CaseIndex];
        CaseBinding fixtureCase = Cases[target.CaseIndex];
        return new
        {
            assertion_id = target.AssertionId,
            adaptation_id = target.AdaptationId,
            classification = target.Classification,
            target_symbol = target.Symbol,
            native_route = target.NativeRoute,
            source_receipt = new
            {
                body_hash = target.BodyHash,
                inventory_index = target.InventoryIndex,
                kind = target.Kind,
                path = UpstreamPath,
                signature_hash = target.SignatureHash,
                symbol = target.Symbol,
                symbol_hash = target.SymbolHash,
            },
            artifacts = new
            {
                fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
                generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                native_sources = NativeArtifacts
                    .Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
            },
            observations = new[]
            {
                new
                {
                    case_id = fixtureCase.CaseId,
                    case_code = fixtureCase.Code,
                    python_facts_sha256 = fixtureCase.FactsSha256,
                    native_fact_count = observation.Facts.Length,
                    native_facts_sha256 = observation.FactsSha256,
                    native_facts = observation.Facts,
                    native_outcome = target.Classification == "equivalent"
                        ? "equivalent-as-pinned"
                        : "adapted-as-pinned",
                },
            },
            scope = new
            {
                exact_target_count = 48,
                equivalent_target_count = 7,
                exception_target_count = 41,
                exact_case_count = 19,
                excluded_indices_not_recorded = ExcludedIndices,
                excluded_symbols_not_recorded = ExcludedSymbols,
                claim_policy = "only-the-authoritative-fixture-case-and-declared-production-public-route-are-claimed",
            },
            upstream = new
            {
                ast_sha256 = UpstreamAstSha256,
                commit = UpstreamCommit,
                inventory_content_sha256 = InventoryContentSha256,
                source_bytes = UpstreamBytes,
                source_path = UpstreamPath,
                source_sha256 = UpstreamSourceSha256,
            },
        };
    }

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(
            receipt,
            "adaptation_id",
            "artifacts",
            "assertion_id",
            "classification",
            "native_route",
            "observations",
            "scope",
            "source_receipt",
            "target_symbol",
            "upstream");
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal(target.Classification, RequiredString(receipt, "classification"));
        Assert.Equal(target.NativeRoute, RequiredString(receipt, "native_route"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));

        JsonElement source = receipt.GetProperty("source_receipt");
        AssertKeys(source, "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
        Assert.Equal(target.InventoryIndex, source.GetProperty("inventory_index").GetInt32());
        Assert.Equal(target.Symbol, RequiredString(source, "symbol"));
        Assert.Equal(target.Kind, RequiredString(source, "kind"));
        Assert.Equal(target.SymbolHash, RequiredString(source, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(source, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(source, "body_hash"));
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));

        JsonElement observed = Assert.Single(receipt.GetProperty("observations").EnumerateArray());
        NativeObservation expectedObservation = observations[target.CaseIndex];
        CaseBinding expectedCase = Cases[target.CaseIndex];
        Assert.Equal(expectedCase.CaseId, RequiredString(observed, "case_id"));
        Assert.Equal(expectedCase.Code, RequiredString(observed, "case_code"));
        Assert.Equal(expectedCase.FactsSha256, RequiredString(observed, "python_facts_sha256"));
        Assert.Equal(expectedObservation.Facts.Length, observed.GetProperty("native_fact_count").GetInt32());
        Assert.Equal(expectedObservation.FactsSha256, RequiredString(observed, "native_facts_sha256"));
        AssertStringArray(observed.GetProperty("native_facts"), expectedObservation.Facts);
        Assert.Equal(
            target.Classification == "equivalent" ? "equivalent-as-pinned" : "adapted-as-pinned",
            RequiredString(observed, "native_outcome"));

        JsonElement artifacts = receipt.GetProperty("artifacts");
        AssertKeys(artifacts, "fixture", "generator", "native_sources", "public_inventory", "python_validator");
        Assert.Equal(NativeArtifacts.Length, artifacts.GetProperty("native_sources").GetArrayLength());
        JsonElement scope = receipt.GetProperty("scope");
        Assert.Equal(48, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(7, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(41, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(19, scope.GetProperty("exact_case_count").GetInt32());
        Assert.Equal(ExcludedIndices, ReadIntArray(scope.GetProperty("excluded_indices_not_recorded")));
        AssertStringArray(scope.GetProperty("excluded_symbols_not_recorded"), ExcludedSymbols);
        Assert.DoesNotContain(target.InventoryIndex, ExcludedIndices);
        Assert.DoesNotContain(target.Symbol, ExcludedSymbols);
    }

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        byte_length = bytes,
        path,
        sha256,
    };

    private static EntityId Id(string value) => new(value);

    private static string ExceptionFact(string phase, Exception exception)
    {
        string parameter = exception is ArgumentException argument
            ? argument.ParamName ?? "none"
            : "not-applicable";
        return phase + "=" + exception.GetType().Name + "|param=" + parameter;
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics) => string.Join(
        " | ",
        diagnostics.Select(item => item.Code + ":" + item.Severity + ":" + item.Message));

    private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static void AssertPinnedArtifact(string path, int bytes, string sha256)
    {
        byte[] content = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(bytes, content.Length);
        Assert.Equal(sha256, Sha256(content));
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

    private static string Sha256(byte[] value) =>
        "sha256:" + System.Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string CanonicalSha256(JsonElement value) =>
        Sha256(Encoding.UTF8.GetBytes(CanonicalJson(value)));

    private static string CanonicalJson(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            WriteCanonical(writer, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonical(writer, item);
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
                throw new Xunit.Sdk.XunitException($"Unsupported JSON kind {value.ValueKind}.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.True(names.Add(property.Name), $"Duplicate JSON key '{property.Name}'.");
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

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
    }

    private static void AssertStringArray(JsonElement value, params string[] expected) =>
        Assert.Equal(expected, ReadStringArray(value));

    private static void AssertStringArray(JsonElement value, IEnumerable<string> expected) =>
        Assert.Equal(expected, ReadStringArray(value));

    private static string[] ReadStringArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static int[] ReadIntArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    private static string RequiredString(JsonElement value, string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        string? result = property.GetString();
        Assert.False(string.IsNullOrWhiteSpace(result));
        return result!;
    }

    private static void AssertSha256(string value) => Assert.Matches(
        new Regex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
        value);

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(
            new Regex(@"0x[0-9a-fA-F]{8,}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
        Assert.DoesNotMatch(
            new Regex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
        Assert.DoesNotMatch(
            new Regex(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(
            new Regex(@"[A-Za-z]:\\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
            text);
        Assert.DoesNotContain("/home/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number) && double.IsFinite(number));
        }
        else if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            IEnumerable<JsonElement> children = value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                : value.EnumerateObject().Select(item => item.Value);
            foreach (JsonElement child in children)
            {
                AssertNoNonFiniteJsonNumbers(child);
            }
        }
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string FactsSha256,
        string CaseSha256,
        string[] TargetSymbols,
        string[] ContextSymbols);

    private sealed record ExpectedTargetBinding(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string AssertionId,
        string Classification,
        string AdaptationId,
        string NativeRoute,
        int CaseIndex);

    private sealed record TargetBinding(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string Classification,
        string AdaptationId,
        string NativeRoute,
        int CaseIndex);

    private sealed record NativeObservation(string Code, string[] Facts, string FactsSha256);

    private sealed record OracleCorpus(JsonElement[] FixtureCases, TargetBinding[] Targets);

    private sealed record NativeGraph(
        Material MaterialA,
        Material MaterialB,
        SurfaceConstruction SurfaceConstruction,
        FenestrationConstruction TransparentFenestrationConstruction,
        FenestrationConstruction OpaqueFenestrationConstruction,
        Surface DefinedWall,
        Surface OpenSurface,
        Surface UnknownSurface,
        GreenRetrofitModel Model,
        GreenRetrofitModel SpecialModel);
}
