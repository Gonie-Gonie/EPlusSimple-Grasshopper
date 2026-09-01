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
    private const int FixtureBytes = 349_184;
    private const string FixtureSha256 =
        "sha256:8fad664f712facf9eef8627d80e9bafcf468e4b0c63d4cf09d9632db814246b4";
    private const string FixtureSchema = "dragons.python-reference.epsimple-construction-core.v1";
    private const string CasesSha256 =
        "sha256:9046cfba389607b07ceb9308c6962cba74c8550fd1e2557fe453f8144d1b0f92";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_construction_core_oracle.py";
    private const int GeneratorBytes = 107_953;
    private const string GeneratorSha256 =
        "sha256:3a46720e1cdf8ffd301a3af62fabe5c9a710d5fa9ba4c0130916bf9944f8f36f";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_construction_core_oracle.py";
    private const int ValidatorBytes = 22_545;
    private const string ValidatorSha256 =
        "sha256:f17f039a00dda4e9ff12b59705f94eaad94aec3d7138345dd603a015d36fd299";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
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
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/Material.cs", 1_988,
            "sha256:a574a5a93277be915c4a9a20e81d5e13fd7d52d0e43b7ba120078fb4eb8d672e"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/FenestrationConstruction.cs", 3_953,
            "sha256:6e8fb7cf51f284d51fb37d5a1b88626422e7ace34a3187d7e0e73196a3a96073"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/SurfaceConstruction.cs", 7_269,
            "sha256:605f54f51df2690cef21885171d6c72752022823f393f872c836160312cf03c6"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/ConstructionDatabases.cs", 11_467,
            "sha256:d32ccc161eb531aa2686891243948871e8af4e5b169012dac0084314df430e44"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonDatabase.cs", 2_537,
            "sha256:af9d3176183292b19e2304e9be3e000e266a6d858d462bdfd65d042d1568147b"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonEmbeddedData.cs", 3_104,
            "sha256:ae2cb7c89e4dcef7195e528fc7831c5abdba560651a244281ffeaaa83c60fc9f"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Fenestration.cs", 2_410,
            "sha256:254b305f2ea49d8c39b25a228a0e734e730fd9168ba04c599c3344b6e92ac9f8"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Surface.cs", 7_030,
            "sha256:fc64bbc6f9914393f1f3ec1fea7a101ba30e0c7640ed12280a8d1614dfc78dee"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Zone.cs", 6_655,
            "sha256:e5a1c9672c7ff9a9d2cf660c96f303f0f162cdd888f681e0f7b24ef98d197a29"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_641,
            "sha256:a212275276ccff153d5df42a44a46ac8877afa485e315ee27d08767a909f29bb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_646,
            "sha256:40e6e7aa1eb89cb341c7e7a32471fa029024e49b261dce8a8926514109d727ba"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_154,
            "sha256:9462f609a4a7a4e062612e4058921b0c91931dc8ff7216dbe54e258cb59ec22c"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Model/GreenRetrofitModel.cs", 7_668,
            "sha256:927ac0cd6982f48f1112a690e1a656dd16716dd96d5a145beb303e2154bbcc33"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Profiles/ProfileDatabases.cs", 6_747,
            "sha256:041b28085203376258726c21033c140a04a6fed65bcd07cd9ea429ced5d73bf1"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Weather/WeatherDatabase.cs", 9_454,
            "sha256:28f3885362fe08663ba6393bae545b70d17284d1751aa5a97cd0194e1b271b34"),
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
        "sha256:7c9c3f29a24027d2c031e0c265a3e48f2cc511cbfcc2190e2641e2f1c0d9372b",
        "sha256:29dfc56a9a7d9833cef1ae61b6026565841d0ccf56101b09baaab44f88372bea",
        "sha256:87c1419bcce53c9fe5127a5292fcf010ca78b7670a566849f8a2a5638fe0c89f",
        "sha256:d963ddeb43b2ac4aef00e793161ae7e42140180d58f1f2124f80771d7df7bdd6",
        "sha256:be519198243c652e2d3237ffa50435e4c80c5ae93f59e398acabb40b6b9ee6b5",
        "sha256:ccfb42d65f925d715a806bc9d483622028a9ec9c0f284557f3c30af479d6d967",
        "sha256:fc4831148f8fff3d3fbda332969fe57ec16c88d8c514500765073223987ddd24",
        "sha256:74c5d656a83d8aefa26a446037f1b4046d15c5251f33ebcb7d96ae6f45b494ad",
        "sha256:934934476640e60c0082b408dade4a06ed6525484a14b74878b2472c3a26ab60",
        "sha256:7ec4282f7d53b9536c87f4525d3396cdff2ce65caf892c852afd672fc1b3a91b",
        "sha256:4cfe4214bb92dd57afb3db5a9fd5ce89d79cc42d037f7b1c156d4ea4dfcad678",
        "sha256:ad3be8202c90190868e7177d305f48f5ae3b5aec771e580b12709cabfb084552",
        "sha256:d5265ec6ca65557506a0a413b3a75a57833fbe46df35bc84afa9634973beedaa",
        "sha256:bda498157faf9838434c0e778fcaff72eb2c313a54503bfb0d593af78a3da774",
        "sha256:f70f6562e2f4f6adccd95ecc850a2643fea076582c40d0f3a6165ceba4532c96",
        "sha256:221e57ab8c294cb0967110885f44938fac1bfbb41f9aef28492018a89016e298",
        "sha256:5a49ca0cc65073b7a6e63133d05ef978512c77c9a584a02e31d11eba82fd66a8",
        "sha256:aa6ad3ecbd3a2a8fe7d7d87132d8eb30d01b8531e05be0b87cdc9d8cf77f1052",
        "sha256:9e7a064718279a58c559cc3591cd6257918ce519f755ea94300283dd880f9d21",
        "sha256:0a66b52df39919b86290884fa06b533edb8d57dcf52b556e05e508b4d874ca8f",
        "sha256:c8d6d935a71ace933707e2e7617f786336be3c40f8e12c5f0a15219616a64834",
        "sha256:33a2296e0669a3a9dee4e7baf9b18c87d1cc1b3454dfefc38903a92304d03dff",
        "sha256:d7604877f84a0c8a19f7b01a01c4f25b01d7d65bd227a0ad3e59ba90ef955c45",
        "sha256:577edb2a67885673b3c87f052a95af2bc47bc97042b66f4ff5e78b24dd31a156",
        "sha256:5bc7709bada75ffd5ac7cea7a151f1a9f4ea027faee0ef226331d78052d36558",
        "sha256:a145533119c93dc8dcdb45d306d3cf7912ad73c75fe7a0d9b0a48c304ca086ad",
        "sha256:d9e519b36886535977a63516973d87171c19a57aa9d9061c9b4a35145e603f0f",
        "sha256:79deb3571525eb0eb0cd52078094ab826555e61c4b639de40e6bd159574a76b1",
        "sha256:c4a41989c92fe31e7f005deff1531b5d09a62275eb394cb1c4091ea70af24622",
        "sha256:754f8f695090ca6bc1ec4ed002c0de372e1fa4e1854cb131d55af87669765e9a",
        "sha256:d6a8b2e6fd1e4c7acae27fe9b907b50d878b1a58319a80e131ae1e29925be516",
        "sha256:f74e66e0435d9393d0bfbb216ae25ee6935b198718b37bcf37e2a148c304ef4f",
        "sha256:d251d7634fcc2bea2d7ea79c35beaa5f2c72b89713ad6801c6b217e71e401866",
        "sha256:6d9501a50dda1f69fa0eccd16a307cc2a91ab05e9d82a3fe0aeccb81cd4617b3",
        "sha256:e26ae4902b4f7f75ec6f43039baab10800a98d41fa42912b14ef21ca361cb8c2",
        "sha256:8606f2dedda84328f0835d130de755bd77a931afb25c5d25c4e5d47a7263001e",
        "sha256:443964325376b5ee17fa90905dac8a8abb6eeb2ff825a0d28a24cebe273cd2d6",
        "sha256:cd3ea2dbb97b5bbf6917f6294b24bfe09ee8c4f8efbb2e83c43006852d927097",
        "sha256:c19dabb8802b676b3c0738ff8f82dbfe6ab40dc7470838163c15810a6117d6c5",
        "sha256:c1358b5e916a16797acb115300ef9c082373bf8144a3a764bcce0eb0974921b9",
        "sha256:e85f2018f6dd14c1d0325b87ac97d6da1b297c07ef96d1a52587c57f889b3c42",
        "sha256:cc351045f5ff8a4a5aef8aadeb49fc1f3e19c2732e4b8317e70cd5ccf98a7a82",
        "sha256:a0adea62f24f327b4178b29f51e268818d73e639bed0d3eb71b1cb31a57e7fbf",
        "sha256:c790824127415f709f85a6423f6e6e89bb2d2cad142d2d4654116e96bddd4a52",
        "sha256:07082e3b8585867f9277986b58917c8de5acb05d04c7a55fc5f7ff4d96b49d84",
        "sha256:3366598aba7971ca284404cae7d0fb0eab964cc8013ec182aa728eb893821278",
        "sha256:b0dbe0e66122ddce977abc8b5255844c258766c8e35235e1393046317708d9d9",
        "sha256:c65b8a207734b143d4568d4403f1426d9f0aba93e90fab4afd053f5b83ca9a80",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:51570cf0c570ddc6c541cfa7598ebadd001924557137e1c1b138cfaffb0b70da", // epsimple-construction-core-75-f86ec154
        "sha256:489d9b6c051f6316d7f430b700ff93e00d0638da3aa4b1213bc18e44faebc069", // epsimple-construction-core-76-246156d9
        "sha256:18353a27b7709b3558154caea5fac23d50b1422c7d2720ebf838b0dbe1260302", // epsimple-construction-core-79-92969825
        "sha256:28e69eef2c656c79882ecb4e2cd4b3fc26f6d8093ae315f4388c0d3c0098f4e1", // epsimple-construction-core-82-e3c4284e
        "sha256:4d84c61a1f36e29770fa31599ccf7cb2e77924a7b42ad944396d7581ffba0677", // epsimple-construction-core-83-5025a060
        "sha256:b8e6ab2e8ccce273b70b6a80de10db45bb7cbdffaf2f4797c99a7507e2a80dc7", // epsimple-construction-core-84-87537fa6
        "sha256:fec3f0e649bf507decbbce38d4789ccdeaefb9033a13f6a91e839b83a2794aa1", // epsimple-construction-core-85-c288c4c2
        "sha256:0c4224e06d2207aff47d1ab69aee1a3c2787def49780e7c38d1a684e60b79282", // epsimple-construction-core-86-538b0465
        "sha256:6e21d499ae1d9b616329319ac292e7b3b35446ecc858091cba4b975c0d6267e6", // epsimple-construction-core-87-8aaf803c
        "sha256:37cc3fa5db07c70c9f2f7903d104472dbc5146930d5885b09266135a87fca1a1", // epsimple-construction-core-88-f430c29b
        "sha256:fcededfe634a5776c30498f4c77ec54ad55964d753cf5ca7329b30233cb3b3eb", // epsimple-construction-core-89-72e986b6
        "sha256:488596a98c3a9ace8ee0f0a652c4b085ad4e3b385b1018bf4b27030171954177", // epsimple-construction-core-90-590c4070
        "sha256:4f948ad853cb8b224d77440771ef5f9634913739c632e54784f96b8b37017d9d", // epsimple-construction-core-91-246156d9
        "sha256:35bb3de9fec0c56f09f0a44a0a4bedc886ee1ff64b95f67ccb3dd23173831154", // epsimple-construction-core-94-d909f493
        "sha256:ea493f7d579339baaf9ba55612521478a8ed832aa881612cbe7b3c56ac1f6302", // epsimple-construction-core-97-b733b56b
        "sha256:982fc059243cb34ed3bd852c68bc6453f1057dd8adf9afd0d58a5c794a8f9d9b", // epsimple-construction-core-98-23136324
        "sha256:9df158ddc8a71b0c1bfcd9af2c6274070236da0b79ab9702c1e7b1996b9925d0", // epsimple-construction-core-99-f2772e15
        "sha256:f0cce3e4010f6de6ce380400149a822aafc01d5ede5f44d1f0b8033a2969bf05", // epsimple-construction-core-100-c3fc9501
        "sha256:fd7e086e44389ac75fec7a61bd7e1eb4656848ab0671d95ad7d651a7e5615b5e", // epsimple-construction-core-101-f6b33018
        "sha256:63d5ae73307ede0ee97edb5cfb8299a1726821bd3bbb8b3f3530a7b4a229d5b9", // epsimple-construction-core-102-abf4a2ea
        "sha256:8558fe5b78c5797a4050cc639a4b562e6729bff0dd24cecfe314ceef4639cfc9", // epsimple-construction-core-103-7326bc5b
        "sha256:df694e02c160aae8106766eea76a09d1c4e6d717b72c5b00a8f4ec8b62d64582", // epsimple-construction-core-104-352f66b1
        "sha256:5f23bdfd2389569d52fa0a353cbbc4d3607c13d9cd3027a241186799eb874278", // epsimple-construction-core-105-3257fd04
        "sha256:f7f6aaec62b26de128b9d61d0cc77b46afb16f7ee69adb31c7bbb2db602b7afb", // epsimple-construction-core-106-45236b5b
        "sha256:7446bb8b2e8e635f1eebd605afb2757577529f80c4b79d894b75bdb1d045b757", // epsimple-construction-core-107-3f5ae9f0
        "sha256:b925536e176cf0f5c261e8eabf95365b0f9ad9e8b4577628b63587b130f35c51", // epsimple-construction-core-108-9f449287
        "sha256:7f150327fe0a70e6b24858f6426f622039ad55f633f647e0552645c4a50dc504", // epsimple-construction-core-109-758d9c0b
        "sha256:5b099a96461716befcf2b6054c646349467360bcf0ed4a7d94ce7859bda21b0d", // epsimple-construction-core-110-4f9ce2c0
        "sha256:c942f22fdd474c1ffc519d37d732a4be8a91b12a3cfbf85801bcca905fe16ab2", // epsimple-construction-core-111-119ed204
        "sha256:3f23fd8fabecb4234c67a96673a148c2ae8e1604543ee4d83783b3e7ad5b9af7", // epsimple-construction-core-112-f3d6bd23
        "sha256:c42a38b171608ac2cc5b1bafe4dd1d246ab222b6c1d02de30902d1971082d1ea", // epsimple-construction-core-113-246156d9
        "sha256:5e84e5a9c8acd262a245f07739c76244d45104b2d27b1404a30dbfc5d019b743", // epsimple-construction-core-114-c6b969b4
        "sha256:d3c186ba38ec05150f25a54fca203a9db785c973060783e7671d96055590d57d", // epsimple-construction-core-117-6e437543
        "sha256:552a9fd7f0618d59c53e19c4b331609742a9261199ba92aa4be51f157a77c519", // epsimple-construction-core-120-23907b76
        "sha256:8c5c86e56f90a76447c59724e9e62265141a4654fd507809199c718e2c179444", // epsimple-construction-core-121-60a500a8
        "sha256:fb70f4677fd8a416112db1cba778ad9b5e8fd224f499a05a5a7acbfbea74e26b", // epsimple-construction-core-122-b1bb16e6
        "sha256:9d7c6e65b4dbc666fe7ee5b475aa273219d21157a4301d885cb9f547f325f700", // epsimple-construction-core-123-d21ed4db
        "sha256:7b17c2090d5a5d198015fdf112f5b8bb4e39b6e9c8cddcf125151dd4905fe813", // epsimple-construction-core-124-8a480443
        "sha256:2f6d57d8b99a8a420083844a03f02e6226c1aa86f1e8d50e6c7a4245eba20d8f", // epsimple-construction-core-125-a806c4c3
        "sha256:1ae7a1d535437885ccb53e2c732b29e9adfc07e7dfdc295d0532c55e19e96f37", // epsimple-construction-core-126-71552576
        "sha256:4fc1a5f4d6b00ff66097bcc5f43dd3ebb1b061a54ae606891d575735fdb317b9", // epsimple-construction-core-127-dc8c7ebc
        "sha256:28e5628be1adae52582b5441a6445e0cf279c41ccc88f4dbfbdc381e117ceec8", // epsimple-construction-core-128-fec259a4
        "sha256:bcfc3c831a9ff4e117cb755c6e213c237c4e9d85cd6bee4ad8f1b68b5bd52340", // epsimple-construction-core-129-d72c2143
        "sha256:2a03e0f01d250a09f1cfcbb731f451f8a7ed6118a0418848a61fdc1fdb2baa3a", // epsimple-construction-core-130-59426aa2
        "sha256:da334968cccba2f6c1d61762e176c7d4e46782a96be123f114890418de07a766", // epsimple-construction-core-131-a204e680
        "sha256:02dec2096d345b5a16d537d870363b46b0a1a4c8e88b57e7febf43d578d145fb", // epsimple-construction-core-132-d803cd9d
        "sha256:f9025ce080e66a234ab4f24657554b45fcb8e84acbb6e284fd1cb3fc36d235cb", // epsimple-construction-core-133-d6777d2d
        "sha256:ee6eb9f0cc6f80ec093d8d9fd20bb58588402d1f678c4f10894ba66accec1afc", // epsimple-construction-core-134-558da4a7
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
            21_108,
            "sha256:555a1df41e5369dbbc44b0729a48673610a86951a215c8e2aa00cfa4fce156f1");

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
