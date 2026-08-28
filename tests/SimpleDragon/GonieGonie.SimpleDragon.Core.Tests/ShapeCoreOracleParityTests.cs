#pragma warning disable CA1861 // Immutable inline arrays make exact oracle expectations readable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.UpstreamTracker;
using DragonDoor = GonieGonie.InvisibleDragon.Shape.Door;
using DragonSurface = GonieGonie.InvisibleDragon.Shape.Surface;
using DragonWindow = GonieGonie.InvisibleDragon.Shape.Window;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class ShapeCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-shape-core-oracle.json";
    private const int FixtureBytes = 108_435;
    private const string FixtureSha256 =
        "sha256:802bcf3d1bc05828329a659ec9013c498325ea5be8f647975dcbb4cb3eee2ba5";
    private const string FixtureSchema = "goniegonie.python-reference.epsimple-shape-core.v1";
    private const string CasesSha256 =
        "sha256:1b6be41823b3a165d1e5c923f46278a44ae8ff68ccef1a0edd08d72ab637398e";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_shape_core_oracle.py";
    private const int GeneratorBytes = 73_269;
    private const string GeneratorSha256 =
        "sha256:40431189b32b4592b949d48a04092634618d84d1a2bfaa3db11b00a346b501a2";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_shape_core_oracle.py";
    private const int ValidatorBytes = 16_597;
    private const string ValidatorSha256 =
        "sha256:db4eb54a35bd7293600904229b3ce6172e1b811df73544661540e3be133e91fa";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/core/shape.py";
    private const int UpstreamBytes = 22_922;
    private const string UpstreamSourceSha256 =
        "sha256:9caa67d424693afc58ee6a456c86d42d504fce4e30e56d73e8ee658dc8e515c1";
    private const string UpstreamAstSha256 =
        "sha256:63cfdec0aec079cfc2d2896091974a5c253656e198cbcb1ea328dbace92c1b7e";
    private const string EvidenceTestCase =
        "GonieGonie.SimpleDragon.Tests.ShapeCoreOracleParityTests.MatchesPinnedShapeCoreThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Fenestration.cs", 2_419,
            "sha256:6b71c32871b5468b570b64dfc7389132f4cf0413340add7d16dcf0cb44451a78"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Surface.cs", 7_039,
            "sha256:a26c799796aa042529926b0c7f4052a495a0e84f8b6a21169aa2b24318b6f809"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Zone.cs", 6_664,
            "sha256:82b149ae49fdc188d7947553187e4d5cb496d67087ae2e1f7c4e878a02cdd01b"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmReader.cs", 48_650,
            "sha256:d91f90946ec19602751fc7818484ca43f85d1c46f9905fa805d8ee8a7281d968"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_652,
            "sha256:4048cc4bdfca312a7baae54c7055bb3aa7177ee6a8143ed9ef1d182353df1842"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_343,
            "sha256:0a0774b4461442b2a3cccf68d39fbc236104a2aa13611e0d27c38f27aa2fe5fd"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Model/GreenRetrofitModel.cs", 7_677,
            "sha256:7bf2f7dfb922f4d85982ada0f5622bfbef59dce8cb4d7a90b2759ed6978935ea"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/Material.cs", 1_997,
            "sha256:c869470be0b2a1f95ce7ad7cfa3ca32489bb99bed23e3465d0ab426175e8b1f5"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/SurfaceConstruction.cs", 7_278,
            "sha256:0fa371d0fd3c6957ad506b927122c51f3eabb0de32d20d7b1602f118302458b4"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/FenestrationConstruction.cs", 3_962,
            "sha256:15eb1452a5c89bf1e2ce41e1931500b6a329ea6467ac618e2ad6fb139369f5af"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Hvac/SupplySystem.cs", 6_465,
            "sha256:1858281dcb5ea2df12a09c0c19caba77cf785a10458fb8d265e882f5695a11c5"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Hvac/OtherSystems.cs", 3_855,
            "sha256:72280cf991d2b48cca0e4be0c0da9402e63348666c4ae5f91ab41d7d1b5938b5"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Profiles/UsageProfile.cs", 8_870,
            "sha256:94ef1a3d94da3d5e108e47da9f0158a4c51e340f4646d5bc52d885da63852eb9"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Profiles/ProfileDatabases.cs", 6_756,
            "sha256:73564c26c8ba3ec98e0758fa8528a6a0771d72c268af7a4beb23e5cc7dc6625c"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Data/SimpleDragonDatabase.cs", 2_543,
            "sha256:31bf339ab34fb3e4f65362be0d9519b1d54c44e4b0e46b63e67398873d5fb74a"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Internal/DomainSupport.cs", 3_763,
            "sha256:8e08f4b14fe302d5920970a505940db34a5e863a57670ddba241ef2288e703ab"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("B01", "epsimple-shape-core.blind-type-values-and-string-semantics", "blind", "sha256:6a8ac8400beb6c4122c6fa6a1fc9908674c499f5adb571201a19d5417cc806e6", "sha256:15b8de85cd4d9332d48c04334d3636b4c865f2b5cb2d64a6ffc2257a00bb398c", new[] { "BlindType", "BlindType.SHADE", "BlindType.VENETIAN", "BlindType.__str__" }, Array.Empty<string>()),
        new("D01", "epsimple-shape-core.door-validation-json-and-dragon-conversion", "fenestration", "sha256:6b0994673eeaac9a47bdbfde027edebb65c7b4909385bb71b4e9520f621eaa94", "sha256:31718b07756d58c19a76a2ddd2142050bc5929d2071a31aa52270c5293a2037a", new[] { "Door", "Door.construction", "Door.from_json", "Door.to_dragon" }, new[] { "Fenestration" }),
        new("F01", "epsimple-shape-core.fenestration-abstract-contract", "fenestration", "sha256:f467490337b86f2d9a5c655a16969520e69f39c02a2b851336c05ed447140126", "sha256:71fdfc17d5d49293c0e9e9ca8f67e93a17bdd1ece347a7843df6070f47aff15c", new[] { "Fenestration", "Fenestration.__init__", "Fenestration.construction", "Fenestration.to_dragon" }, Array.Empty<string>()),
        new("F02", "epsimple-shape-core.fenestration-id-deepcopy-and-factory-dispatch", "fenestration", "sha256:20f684ccb9cd4ab8ea7479779199f89be239308e8176f3809bd82e80e1cf3140", "sha256:a369c9b79535f2f0e1b244ba2ae07b1531d043634bd02f03a16477de64f315bc", new[] { "Fenestration.ID", "Fenestration.__deepcopy__", "Fenestration.from_json" }, new[] { "Door", "Window", "GlassDoor" }),
        new("G01", "epsimple-shape-core.glass-door-window-subtype-and-conversion", "fenestration", "sha256:861a3fd0e8cd792dc7e833a96af580b29bf8cb0fe7d23320f34f67fe4a856269", "sha256:32873aa8ca4eadbae3fb8270ea83b8ab5962bd84b2adb8d17e4ffca8033da1f8", new[] { "GlassDoor" }, new[] { "Window", "Fenestration" }),
        new("S01", "epsimple-shape-core.surface-constructor-properties-and-boundary-coupling", "surface", "sha256:ceb53ccba0964b9b75a9128fe407c44cf4be7b18cf582d42293a656f5e1c99fe", "sha256:5d8187491d8aeb46ed3bc6aaa94f0fa0d7e4f150b2675970dc94efc174feec6f", new[] { "Surface", "Surface.ID", "Surface.__init__", "Surface.adjacent_zone", "Surface.area", "Surface.azimuth", "Surface.boundary", "Surface.construction", "Surface.reflectance", "Surface.type" }, new[] { "Zone" }),
        new("S02", "epsimple-shape-core.surface-deepcopy-and-flip-semantics", "surface", "sha256:4f9dadc6bf62c5c2bfc210a20e5aa9ec5cd18dcc7319baf7dad98bd5ccdd7ea4", "sha256:144124e7d1b568ae7feb1a2b0440f7be41276ba2b1d588195646744684f1c0b3", new[] { "Surface.__deepcopy__", "Surface.flip" }, new[] { "Surface.type", "Surface.azimuth" }),
        new("S03", "epsimple-shape-core.surface-json-defined-open-unknown-constructions", "surface", "sha256:4035c1295cc3847d12ed743a40907129032105320affbf26121d3bda8150707b", "sha256:00984c4cbe9bf848f7ef23176d6b33fd566caf3894e96a1ac11aa2e2caf2ecba", new[] { "Surface.from_json" }, new[] { "Surface.construction", "Fenestration.from_json" }),
        new("S04", "epsimple-shape-core.surface-opening-counts-and-unique-constructions", "surface", "sha256:4bde8e1a5cf1ee711bf2de689fb8ff41b394ea44f556a788d4fc1950c55fbec2", "sha256:54585f1a9f1dce957c3029eaeb64c1a63f070a7a2262a3611b2235272369af78", new[] { "Surface.get_unique_fenestration_constructions", "Surface.num_doors", "Surface.num_windows" }, new[] { "Door", "Window", "GlassDoor" }),
        new("S05", "epsimple-shape-core.surface-dragon-geometry-and-opening-partition", "surface", "sha256:4cc1736d5fdfebdbaef75032688585c656c8745feddc42bc15c9c232fe2fb337", "sha256:2ec9a3d6fa5dab7e066fd91144afecd64d319543382b84d3c99b658202333415", new[] { "Surface.to_dragon" }, new[] { "Window.to_dragon", "Door.to_dragon" }),
        new("W01", "epsimple-shape-core.window-constructor-blind-and-construction-validation", "fenestration", "sha256:1eb22483d6cf26f7e591a0a04261894d4276d98c499df475d5ea23322b22f8ca", "sha256:e8d9eae97ecb3028407c3e44f341db74fb1f6aafef4d3ae7e6ee4fb87436c620", new[] { "Window", "Window.__init__", "Window.blind", "Window.construction" }, new[] { "BlindType" }),
        new("W02", "epsimple-shape-core.window-json-and-dragon-blind-mapping", "fenestration", "sha256:6c9d395fd92f8c1308f7c3790e4eebae0b5a662c49f9ab4c318ebb4d56867d1e", "sha256:35de73b349ee6459875511cc87b8bda256ec43faaec8e75565be856efa27704a", new[] { "Window.from_json", "Window.to_dragon" }, new[] { "BlindType.SHADE", "BlindType.VENETIAN" }),
        new("Z01", "epsimple-shape-core.zone-constructor-id-height-and-supply-validation", "zone", "sha256:1b779e39a56312ee23d131296b4e73cc0f62512ba062d7287ad179fc8f2a4df9", "sha256:56416046eb966ea8ef36472c1afd7a8e91ca13dbfe3802937b067e6542f1e8d5", new[] { "Zone", "Zone.ID", "Zone.__init__", "Zone.height", "Zone.supply_systems" }, Array.Empty<string>()),
        new("Z02", "epsimple-shape-core.zone-area-infiltration-and-supply-filtering", "zone", "sha256:5328b9ee32e0a71252006f2c2285800f8c1d2677dd80c899b3baa92f7b7d66ee", "sha256:b6792937ff3ff7fe7267f5fa5718f486870213b03363346246a5059a6d36d8b4", new[] { "Zone.area", "Zone.cooling_supply_systems", "Zone.heating_supply_systems", "Zone.infiltration" }, new[] { "Surface.num_windows" }),
        new("Z03", "epsimple-shape-core.zone-json-surface-profile-system-and-ventilation-counts", "zone", "sha256:1c6cfbb7efaf0115b9e781d13bbbbc5d9525256045616474972df37ae1763665", "sha256:6e157cb3d0f65744730c4b504762681877f2b3e415fee1efb40e0f9aa3102f23", new[] { "Zone.from_json" }, new[] { "Surface.from_json" }),
        new("Z04", "epsimple-shape-core.zone-unique-construction-and-material-aggregation", "zone", "sha256:e1fe208bc5a494d8d91418e6943a80aaae62c6b19e4c106d0beb3b6e42a1f219", "sha256:5d888077c59733555f0eca8413bd45a7732f608f233b543791b762c0b957b731", new[] { "Zone.get_unique_fenestration_constructions", "Zone.get_unique_materials", "Zone.get_unique_surface_constructions" }, new[] { "Surface.get_unique_fenestration_constructions" }),
        new("Z05", "epsimple-shape-core.zone-to-dragon-upstream-failure", "zone", "sha256:817afda425049cf2992edaef2c9a9fff00352e80a619c8ee9aed2a96b76e28a1", "sha256:493d789c6bc273ada4b17f0a776084c450311cf7a9ef5fd0bc54eb3d5cb1a778", new[] { "Zone.to_dragon" }, new[] { "Zone" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        ExpectedTarget(405, "BlindType", "class", "epsimple-shape-core-405-6008dd91", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(406, "BlindType.SHADE", "constant", "epsimple-shape-core-406-bb03051d", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(407, "BlindType.VENETIAN", "constant", "epsimple-shape-core-407-09c92f4a", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(408, "BlindType.__str__", "function", "epsimple-shape-core-408-f40e4929", "exception", "grm-vocabulary-rather-than-native-enum-tostring-f40e4929", "GonieGonie.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(409, "Door", "class", "epsimple-shape-core-409-8c468e24", "exception", "unified-immutable-fenestration-with-door-discriminator-8c468e24", "GonieGonie.SimpleDragon.Fenestration with GonieGonie.SimpleDragon.FenestrationType", 1),
        ExpectedTarget(410, "Door.construction", "function", "epsimple-shape-core-410-2ca0072c", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Fenestration.Construction", 1),
        ExpectedTarget(411, "Door.from_json", "function", "epsimple-shape-core-411-26b0f9bb", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 1),
        ExpectedTarget(412, "Door.to_dragon", "function", "epsimple-shape-core-412-eb81bd06", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 1),
        ExpectedTarget(413, "Fenestration", "class", "epsimple-shape-core-413-43d44ea1", "exception", "sealed-discriminated-native-fenestration-rather-than-abc-43d44ea1", "GonieGonie.SimpleDragon.Fenestration with GonieGonie.SimpleDragon.FenestrationType", 2),
        ExpectedTarget(414, "Fenestration.ID", "function", "epsimple-shape-core-414-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Fenestration.Id", 3),
        ExpectedTarget(415, "Fenestration.__deepcopy__", "function", "epsimple-shape-core-415-a0dbc411", "exception", "immutable-native-fenestration-explicit-reconstruction-a0dbc411", "GonieGonie.SimpleDragon.Fenestration constructor", 3),
        ExpectedTarget(417, "Fenestration.__init__", "function", "epsimple-shape-core-417-1b22b2f1", "exception", "deterministic-native-id-and-discriminated-constructor-1b22b2f1", "GonieGonie.SimpleDragon.Fenestration constructor", 2),
        ExpectedTarget(418, "Fenestration.construction", "function", "epsimple-shape-core-418-0b0cbf2f", "exception", "immutable-resolved-native-construction-reference-0b0cbf2f", "GonieGonie.SimpleDragon.Fenestration.Construction", 2),
        ExpectedTarget(419, "Fenestration.from_json", "function", "epsimple-shape-core-419-2e553f68", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 3),
        ExpectedTarget(420, "Fenestration.to_dragon", "function", "epsimple-shape-core-420-ede823e2", "exception", "aggregate-native-converter-rather-than-abstract-instance-method-ede823e2", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 2),
        ExpectedTarget(421, "GlassDoor", "class", "epsimple-shape-core-421-1981a404", "exception", "unified-immutable-fenestration-with-glassdoor-discriminator-1981a404", "GonieGonie.SimpleDragon.Fenestration with GonieGonie.SimpleDragon.FenestrationType", 4),
        ExpectedTarget(422, "Surface", "class", "epsimple-shape-core-422-996a596c", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface", 5),
        ExpectedTarget(423, "Surface.ID", "function", "epsimple-shape-core-423-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.Id", 5),
        ExpectedTarget(424, "Surface.__deepcopy__", "function", "epsimple-shape-core-424-0d951ae6", "exception", "immutable-native-surface-explicit-reconstruction-0d951ae6", "GonieGonie.SimpleDragon.Surface constructor", 6),
        ExpectedTarget(426, "Surface.__init__", "function", "epsimple-shape-core-426-bd742aa0", "exception", "deterministic-native-id-and-immutable-constructor-bd742aa0", "GonieGonie.SimpleDragon.Surface constructor", 5),
        ExpectedTarget(429, "Surface.adjacent_zone", "function", "epsimple-shape-core-429-cf314ac6", "exception", "native-adjacent-zone-id-rather-than-object-reference-cf314ac6", "GonieGonie.SimpleDragon.Surface.AdjacentZoneId", 5),
        ExpectedTarget(430, "Surface.area", "function", "epsimple-shape-core-430-aa93b96b", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.Area", 5),
        ExpectedTarget(431, "Surface.azimuth", "function", "epsimple-shape-core-431-98e03520", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.Azimuth", 5),
        ExpectedTarget(432, "Surface.boundary", "function", "epsimple-shape-core-432-3680772f", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.BoundaryCondition", 5),
        ExpectedTarget(433, "Surface.construction", "function", "epsimple-shape-core-433-9aed8e71", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.Construction", 5),
        ExpectedTarget(434, "Surface.flip", "function", "epsimple-shape-core-434-8e01b8fa", "exception", "pure-deterministic-native-flip-without-inplace-mutation-8e01b8fa", "GonieGonie.SimpleDragon.Surface.Flip()", 6),
        ExpectedTarget(435, "Surface.from_json", "function", "epsimple-shape-core-435-3da5f695", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 7),
        ExpectedTarget(436, "Surface.get_unique_fenestration_constructions", "function", "epsimple-shape-core-436-72d9807c", "exception", "model-catalog-native-aggregation-72d9807c", "GonieGonie.SimpleDragon.GreenRetrofitModel.FenestrationConstructions", 8),
        ExpectedTarget(437, "Surface.num_doors", "function", "epsimple-shape-core-437-42d0195c", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.DoorCount", 8),
        ExpectedTarget(438, "Surface.num_windows", "function", "epsimple-shape-core-438-4ec64b53", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.WindowCount", 8),
        ExpectedTarget(439, "Surface.reflectance", "function", "epsimple-shape-core-439-3a69bea0", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.CoolRoofReflectance", 5),
        ExpectedTarget(440, "Surface.to_dragon", "function", "epsimple-shape-core-440-26abf64e", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 9),
        ExpectedTarget(441, "Surface.type", "function", "epsimple-shape-core-441-5afcce2a", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Surface.Type", 5),
        ExpectedTarget(442, "Window", "class", "epsimple-shape-core-442-00f305af", "exception", "unified-immutable-fenestration-with-window-discriminator-00f305af", "GonieGonie.SimpleDragon.Fenestration with GonieGonie.SimpleDragon.FenestrationType", 10),
        ExpectedTarget(443, "Window.__init__", "function", "epsimple-shape-core-443-e8fad25a", "exception", "unified-native-fenestration-constructor-e8fad25a", "GonieGonie.SimpleDragon.Fenestration constructor", 10),
        ExpectedTarget(444, "Window.blind", "function", "epsimple-shape-core-444-92ce583d", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Fenestration.Blind", 10),
        ExpectedTarget(445, "Window.construction", "function", "epsimple-shape-core-445-4f40b518", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Fenestration.Construction", 10),
        ExpectedTarget(446, "Window.from_json", "function", "epsimple-shape-core-446-93259bed", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 11),
        ExpectedTarget(447, "Window.to_dragon", "function", "epsimple-shape-core-447-f032bad2", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 11),
        ExpectedTarget(448, "Zone", "class", "epsimple-shape-core-448-dda48f66", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone", 12),
        ExpectedTarget(449, "Zone.ID", "function", "epsimple-shape-core-449-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone.Id", 12),
        ExpectedTarget(451, "Zone.__init__", "function", "epsimple-shape-core-451-a5f3cee1", "exception", "deterministic-native-id-and-immutable-zone-constructor-a5f3cee1", "GonieGonie.SimpleDragon.Zone constructor", 12),
        ExpectedTarget(452, "Zone.area", "function", "epsimple-shape-core-452-51ef4a1e", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone.Area", 13),
        ExpectedTarget(453, "Zone.cooling_supply_systems", "function", "epsimple-shape-core-453-e0f58a2e", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone.CoolingSupplySystems", 13),
        ExpectedTarget(454, "Zone.from_json", "function", "epsimple-shape-core-454-1254d46e", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 14),
        ExpectedTarget(455, "Zone.get_unique_fenestration_constructions", "function", "epsimple-shape-core-455-d8077110", "exception", "model-level-native-fenestration-catalog-d8077110", "GonieGonie.SimpleDragon.GreenRetrofitModel.FenestrationConstructions", 15),
        ExpectedTarget(456, "Zone.get_unique_materials", "function", "epsimple-shape-core-456-ecb20cb3", "exception", "model-level-native-material-catalog-ecb20cb3", "GonieGonie.SimpleDragon.GreenRetrofitModel.Materials", 15),
        ExpectedTarget(457, "Zone.get_unique_surface_constructions", "function", "epsimple-shape-core-457-486d73d3", "exception", "model-level-native-surface-catalog-486d73d3", "GonieGonie.SimpleDragon.GreenRetrofitModel.SurfaceConstructions", 15),
        ExpectedTarget(458, "Zone.heating_supply_systems", "function", "epsimple-shape-core-458-c68b3d65", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone.HeatingSupplySystems", 13),
        ExpectedTarget(459, "Zone.height", "function", "epsimple-shape-core-459-349a48c8", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone.Height", 12),
        ExpectedTarget(460, "Zone.infiltration", "function", "epsimple-shape-core-460-3fffc5a8", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone.Infiltration", 13),
        ExpectedTarget(461, "Zone.supply_systems", "function", "epsimple-shape-core-461-3eaf6c25", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.Zone.SupplySystems", 12),
        ExpectedTarget(462, "Zone.to_dragon", "function", "epsimple-shape-core-462-da336048", "exception", "native-greenretrofit-converter-implements-upstream-missing-operation-da336048", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 16),
    };

    private static readonly int[] ExcludedIndices = { 416, 425, 427, 428, 450 };
    private static readonly string[] ExcludedSymbols =
    {
        "Fenestration.__hash__",
        "Surface.__hash__",
        "Surface.__repr__",
        "Surface.__str__",
        "Zone.__hash__",
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(8, "sha256:ac91bac2a26fc5dfa8844ff458d271c417e94bf7e5b9443589758e626d4dc671"),
        new(8, "sha256:9c0d86fc6de06df87e3a23b0d3fbd6bde205a86ef2c676f33372d44a464e1a75"),
        new(8, "sha256:e81f5ff11fc2d4bdd1933552a87449441578f4eb904d8dea5fbbcc76284876d0"),
        new(8, "sha256:7c7e4b3f695bc953eb8308853f4cf75f070950e33c5b91d43a7542b7d8c5125a"),
        new(8, "sha256:c6aeab8af3ecd28b48df75ae97a47ad9aed05795a25b8218ce54da0082b1af66"),
        new(11, "sha256:b2281301be1c73c510c4767d1dc2b3ab31e5833f7f773f7e5a78831807da86e2"),
        new(12, "sha256:28cb3716fa5ae4bd07b1c28a53273bdafea5bf31ccdb9d32447d828dc9b4e22e"),
        new(9, "sha256:83dd9c204ce675268f5f626c645b75a6e180f62d24a6f325a57be29415e5f3cf"),
        new(7, "sha256:914f29edd9a50df255e3d7a20874c4e984cf5d533524e341b21d97f8f5ddd262"),
        new(9, "sha256:ddcc5e2f7308ac7dd2ebdb41aaaae0226493706210521648e5b092747f1b84f5"),
        new(9, "sha256:5dcf2d43e7f6464fece25eb57cf8e0c8f8fdf2b96c79324bb160f777726411eb"),
        new(8, "sha256:3f0a0237177284b0212e2bf878ecd4147ab9242c830203a584784bfb12ee0793"),
        new(8, "sha256:3885a77c65f922e629ccbdbbaae87f71503329a9ba7dfeffaf9ac587de294334"),
        new(9, "sha256:e795c4acf2edf320a105853acc6a7323951ad2843eacbb5b1bdac9e919b0088b"),
        new(9, "sha256:7989bf7b58f95dc48f604045e982bce35e8f38d853da25303a37420737dfa523"),
        new(8, "sha256:9485164c586f35df6d945b913c56e70d824c63a9dcc7f743035a8fc22c73534f"),
        new(9, "sha256:fcda231eb5d4255e27bad6389d603e29a61da7a928d371612e73f730b85b5a6d"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:c91053c8621ff85b6ef3b5433806c7ea3dcc1b21de57542c6dcdabf92e6b1644",
        "sha256:249ea441d842f497603575c9c9c2079682d6411f97572d2dfbef7101f9b0bf27",
        "sha256:bb6518827f5e01a8fa3fa384ba1ecea3d829377f23505b21f36959e10d4c3c64",
        "sha256:f123f0bf380b29d580d37479e11f1b0d60a08ec8dc78c88dbba8fe29565cb8e1",
        "sha256:df9411469e9e6b56ae5a148b9d1256a227ef26472ab3885e7bd0164992aa120b",
        "sha256:5851a4df77ce53345039feca424379f156ef80579feb691c6795555ecec7dcb8",
        "sha256:e439e96ed77f6fdf559d27fd574abb7c166933ec6b5711c86a4299788bc202ed",
        "sha256:6064b935a453421cb7e46c0d874e0030a67bf804008f10e9230ea4ae2de2e75c",
        "sha256:828cfc9976cee6dbcf58d317c4a9f0d765ced88af5ace65642e0eca1f9fcbc5b",
        "sha256:0b77f43701b02d723429062bb23532932e3a40f4d69f587aab6090f8c47b6146",
        "sha256:6d26c2cbedd21261294cf94e0367b8c84d1d8a0057244f49f8f0343dfbf9492d",
        "sha256:ccd1d3a93ec95a03b69d5492ccf2e38acf96eb403f9222e3e2ca4a073dd23e75",
        "sha256:1b9c78a65fe702383218a10772158326aa2186e4b0f8c38c35de2d8df8f81c90",
        "sha256:49fef807a087dcb3479d5dbf11cc02ff5c2a8184e5e624a80921be52046e32b8",
        "sha256:402cf192b70650333d40da12120c816ccb0d6bb7e7ab21af0e59acb669cdd90b",
        "sha256:3b410c64208d8ea43ba8cc5b581de11db2a5a3e11f724eb0c18f776176cf684e",
        "sha256:eade22e11fa771331b1a7e9fe3816022e171264b9ded1830129b45fbade5f0f4",
        "sha256:ed73b6cc24784ca991b35b26d8d88e22f8a1ae6f8ad910859d133bb3b31a25af",
        "sha256:c10249072d2f7797a84d85751c61d59d7b1936ca570ac11de3e0bdcc72f0282b",
        "sha256:edb876a1a25cfb54e29b790dae13014a290d778330b3351e0a4a8872071bb6e9",
        "sha256:19b6cd5c44f0aca9a1b8355cb642a3eb07b48d4742db6b5e225772d4e310dd4e",
        "sha256:5d58b415ec0ea02090111fc41f98a0061f3b4834e2c334a27f32521004be7cd9",
        "sha256:54732d735006a832ceacbcff38e9827b9ccffae59a4e7fccc31320d9ddb7dd98",
        "sha256:3a11f5c4df71dd7f76f79c47b08472c07f3118c307576b21c7b8fff8cb66f658",
        "sha256:b61279b3e6837db174a553c50c5e0a33600d5871111619488ad30458ba2b1baa",
        "sha256:1202598d119100e3c185ccfebf049dd25cdae86cbe15a57ee3741d5ce11ad8e0",
        "sha256:a5813c4c053c38e8e9706abb01cb870b09a999d8d5b87a79b421d9529dbea46b",
        "sha256:722157ee9277cbf0a2d47ddd594d5451a3b2d009aaf61d3f1083e826e396ddb5",
        "sha256:73a9290c396fdff94a91676ec23af3059d0728102229959624be9da6ff4e0ab2",
        "sha256:ea816387e7a8e05055a45bc8478409283f51179c4c4853c7837e548e0dcf9d14",
        "sha256:41fd0b9676a71b406b12694d8e41016f2ce0d29114ea673e37ba0ef2ef0122b3",
        "sha256:bd48c53490e6e19be65c670333dec94f8aa68c91690371044147ad97f35ac63a",
        "sha256:f01a8a90a5b787ad691b237f84d598b64035013740c5885fb4cedfc8efed54e3",
        "sha256:9d147afd0c4b343d8e60a2cabf491696086cba7c98967e4cd89b4a4f863afdf8",
        "sha256:d279db700d7515474d83de0f05732aee2dea8b88a0d08e0cf99ad2d042244dae",
        "sha256:aef0b974bce1c63ba0e3790ac9512f271e988f26e5c989df243c2e8318018ffa",
        "sha256:6472d9b36a01318d98733c558ae25e8faa0e54f8f8f2ddab6c58dec8820cf33b",
        "sha256:3939e13476fcd3bd195292296fd531ee16ecfb4cc33fcf174cc3effd12dcda93",
        "sha256:6cc26afe2a83e6b03a96dd44e7dea4c002fdeda2df20608edb7a2c5233d50291",
        "sha256:c84337b58e5d0a6e3188658f921f65d1f712702e8bd6918d3f20420738afb0df",
        "sha256:257c4e040154a6c72edca73e90bd2c23e50e097afa2b40bce4def1323145ae0b",
        "sha256:e2fd819467791dd5c9e61146e4c1eb487f3e578e78d9ff4b6c5e86a3885ff3dc",
        "sha256:86857191473c42c13e020e1a7c001ed5d886f6246a1be302da8302911d6378a7",
        "sha256:3e68d8d5364c3a0a28a75503ae1df4831b7cccb1b381468eb440ccf4afda3fde",
        "sha256:5551f27d85a6d565484cf751665a4459dafca98be89d66c1a914b00add5c0cba",
        "sha256:542b33db5555279cb4014249430989f2a2eb6551db490a3adb532ba761624156",
        "sha256:934baa12adf58a63de7820ccf998656407e9d2e6394272d157651751e98f0295",
        "sha256:c25bca8ce860c72a8a3924b8df6a7e75394e4993840e8908cf525847c1063fb6",
        "sha256:fdcdbf61ce1f9635aef0f6f24e03294ea9fa280f56a5d787a9064c5d4050d26a",
        "sha256:d04acf486afff0b9dc467048c34e510af82abfd3f0685be1744964f6edb963b9",
        "sha256:23684e21723560507e0f489599970b44426991cf93a69d8a4b721089fb1c6be6",
        "sha256:257250202b6d31736de8502a19ef5c6f4d3bf1b09cc5ec431e76844535d7a6f5",
        "sha256:8ab3a77bb4628a15d2a242119db9c6c25813631a7ad088af7b2d3e6d1b1ecdca",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:6a912e79b185d0ab6ab40efc600f617c5cc04071d337152030a21d4c6084a4a9", // epsimple-shape-core-405-6008dd91
        "sha256:aa9241036c2b91520bc849d217d493465a929fe170c50ab8e14ae39a20148e10", // epsimple-shape-core-406-bb03051d
        "sha256:3bb19bfb68417af9869a334c92aeefd82f6ab299755f395356a1cc0266b54c2e", // epsimple-shape-core-407-09c92f4a
        "sha256:fdb6b82bc61548b7ee1cead61b1971dbdb4f36b6615412528de194f8b28ae785", // epsimple-shape-core-408-f40e4929
        "sha256:1b2321c58e32104ee487d7bf9659e62768111be5e81f8dfd977e1c8bebc9b932", // epsimple-shape-core-409-8c468e24
        "sha256:482b5abf11a1011f23c2e43ba73789abdff54b84674c51637dd9635fcb78aea7", // epsimple-shape-core-410-2ca0072c
        "sha256:0b205f347ed2bdda90ef6584894b51f906e5e1bb183103759b46b05b45f24494", // epsimple-shape-core-411-26b0f9bb
        "sha256:78ee715368380f4b16e7b8d0687754049906e2a3a2782c92f1e40e1d76b20f22", // epsimple-shape-core-412-eb81bd06
        "sha256:e5b1dea92acd091b68cffac57f8e4e57307614f8a7fc38ee0fea372f5ed1c9d6", // epsimple-shape-core-413-43d44ea1
        "sha256:d1becc96b87aff61fac206fc83d6cb18434a5b6566b843b9f0e716f26d436e68", // epsimple-shape-core-414-246156d9
        "sha256:ba61fa25c83851b987f60f58d9b4d1cd042217525b5ee66e36d1c9693ce49c01", // epsimple-shape-core-415-a0dbc411
        "sha256:c2aabad9e104c189cb71d8f8ac939279de462488459100112ad56c6a3f48237f", // epsimple-shape-core-417-1b22b2f1
        "sha256:23fb3694a208957a05c939dadf5e51c7656b3706dae67acef5aeece505a8725d", // epsimple-shape-core-418-0b0cbf2f
        "sha256:61662c8eb3f522c9329fdfb63c014ad98c7af96c064479db31e8c4149751cdd7", // epsimple-shape-core-419-2e553f68
        "sha256:0cb38e1141a685dba73d726e6190b6a5b5f5a501ead91406c9542c723f259344", // epsimple-shape-core-420-ede823e2
        "sha256:c7502e59d16247399599b881e5d0bc6c5c38f47ea15d16d07ccf31cb14ea4f68", // epsimple-shape-core-421-1981a404
        "sha256:4f7e321cdebc0679d61ebbc251f5a3d637bc9c570a18877bad92250faffe2e1a", // epsimple-shape-core-422-996a596c
        "sha256:30090495fc11fe53be68f80df9b0e8998e939bffa541c970e020bebd352f8767", // epsimple-shape-core-423-246156d9
        "sha256:675c74ddc7cb05a6199d1ddd7bfe9bf4ed7bbe5fa0e417adf2e72999d4da172b", // epsimple-shape-core-424-0d951ae6
        "sha256:031e3b76154daa19a270633573b38f0bcbf9ff0bace1a67128f4cf88ff8802b8", // epsimple-shape-core-426-bd742aa0
        "sha256:f845805a5ea886d2ad87c24f8d9823134743ad36905010456808d5ed79208b5b", // epsimple-shape-core-429-cf314ac6
        "sha256:8e60064a82ff649be1fbe8faba1df8816367f059b1b6c2aa17a36b6eb70cc5be", // epsimple-shape-core-430-aa93b96b
        "sha256:7a79bc4d5b05900b5cf13b40b529d2d0090c1ed54f6bd23e01a237bb6b2ab9b9", // epsimple-shape-core-431-98e03520
        "sha256:d53595bf046190ad04a260afd792d28e81e2f11674ff589824ec391dc0ebcf17", // epsimple-shape-core-432-3680772f
        "sha256:512e21a3f819b9657bc8b939066007ca9884f8f66fa1ce67c9b9bfe4e96f5e35", // epsimple-shape-core-433-9aed8e71
        "sha256:df481fb0f8d349eac64f48d61261f98ff89c30fe5bd5fc171401359a0a523b76", // epsimple-shape-core-434-8e01b8fa
        "sha256:88dae5e7b59f91bea94ecaf92e724157b96bc985cfbfb3de7b72e5dc8a975f9c", // epsimple-shape-core-435-3da5f695
        "sha256:f15758d4d1371d94a1d9a8032471cbe5331a45b26b1b703da9b20ef79f2bd676", // epsimple-shape-core-436-72d9807c
        "sha256:c4aad8ff5b12ae4f16f302f6cc211748fae0b17a700808b788f3748dcd346a01", // epsimple-shape-core-437-42d0195c
        "sha256:e8f614b494c0ef57d69cf26489abca4412df09391eebb2cc6a2531f169823ed8", // epsimple-shape-core-438-4ec64b53
        "sha256:668f71c9d6f65ec762f9f62289ea6ffa0ed9cf495ffd26c6b0e3974a845b6025", // epsimple-shape-core-439-3a69bea0
        "sha256:2cbf5715a41c952aaffc378a9f823b71101fda921401cbc622994bd2ea21ed01", // epsimple-shape-core-440-26abf64e
        "sha256:8877d85d7c229c1bbc1834a45c665e4bf241e41f35a29abbe578af642b7fbda4", // epsimple-shape-core-441-5afcce2a
        "sha256:3546205a2db5a52a82b9c92fcca76d9d953f1abcf176b69ef6491366deaeb4fc", // epsimple-shape-core-442-00f305af
        "sha256:98ae9bbe2eb72a1650e05a5a243ac114eca31becbf3091deca4e5912ebd6e19e", // epsimple-shape-core-443-e8fad25a
        "sha256:64611b1b71fe0082f2af3d5ba41c0bbc68be86f34b5b8909f18c06de19839e2d", // epsimple-shape-core-444-92ce583d
        "sha256:9db1582df64c53f07b3b63b27cf3137a1120b0b47e6bd8760fd9a0730e512b19", // epsimple-shape-core-445-4f40b518
        "sha256:3da04156ef0482111842f42ce3b4b79cc46ebcee641948104fb9ecb57a88c2ff", // epsimple-shape-core-446-93259bed
        "sha256:44b036eb8fa06100a9a87757373ce6f55249d96f6da6b57f12ba6c8e61e13172", // epsimple-shape-core-447-f032bad2
        "sha256:b6c8caa6832e12340305245ab16768197ab673323b6ece1c2f0d3922b9f37732", // epsimple-shape-core-448-dda48f66
        "sha256:79cf6808b695ab24dc08ea0f1241519935f43b8b1830517c3d4fc5d82ec3e6bd", // epsimple-shape-core-449-246156d9
        "sha256:fce3629fb5295527dcecb07d81fb23e1ed9decf1fea3ed108e9e749d64a6401b", // epsimple-shape-core-451-a5f3cee1
        "sha256:fe1942a46e4fb8955e4e9321160787fd6754a4a4aec3c58bd54fb3ee1d937cd2", // epsimple-shape-core-452-51ef4a1e
        "sha256:2be40379493229bc6223ee9c0ecf1f90dd2f1aaa3d542f38768874a939a453e6", // epsimple-shape-core-453-e0f58a2e
        "sha256:b53e471e9eb85a71cc248dd61e509e412d13ed2af5f09563a037b5e181a7fb45", // epsimple-shape-core-454-1254d46e
        "sha256:9955f6fa79a55b12401615893ada0c9b5a3429e11769f2421e835212818669b1", // epsimple-shape-core-455-d8077110
        "sha256:5fed2c7ddb8819de98cff249bdfc61fc38f210102940d9fa1245512021c31148", // epsimple-shape-core-456-ecb20cb3
        "sha256:14cdd478ba8a71a9eb1d2ec05cdc986d99c0542e4b02240f8eab2c040ba68e18", // epsimple-shape-core-457-486d73d3
        "sha256:f1978d4547e9db7f7fb119cae53efc3e9da4cc44dbe6d2b92ae2cf4e4590f862", // epsimple-shape-core-458-c68b3d65
        "sha256:f172a13b6c83729a8c69cf8e7e872e54b6da87a344a0d99e9d963d8964789fbf", // epsimple-shape-core-459-349a48c8
        "sha256:b5b837cc1a8a790d5dd64f45253bd2f8be1097b3b1dc63b12f6fe50080740185", // epsimple-shape-core-460-3fffc5a8
        "sha256:32be0733ed8961692a2fd088a96eea7d8771efb2bcf29be226718ea2391c3cc4", // epsimple-shape-core-461-3eaf6c25
        "sha256:18362fe3a5cbeb306493abeea18ffad2d8f8ff97dc371fac4dee8808d9166d49", // epsimple-shape-core-462-da336048
    };

    [Fact]
    public void MatchesPinnedShapeCoreThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(17, observations.Length);
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
                "SHAPE_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(53, recordCount);
        Assert.Equal(53, corpus.Targets.Length);
        Assert.Equal(53, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(33, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(20, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.DoesNotContain(corpus.Targets, item => ExcludedIndices.Contains(item.InventoryIndex));
        Assert.DoesNotContain(corpus.Targets, item => ExcludedSymbols.Contains(item.Symbol, StringComparer.Ordinal));
        Assert.Equal(17, corpus.FixtureCases.Length);
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

        Assert.True(typeof(Fenestration).IsSealed);
        Assert.False(typeof(Fenestration).IsAbstract);
        Assert.Equal(new[] { "Window", "Door", "GlassDoor" }, Enum.GetNames<FenestrationType>());
        Assert.Equal(new[] { "Shade", "Venetian" }, Enum.GetNames<BlindType>());
        AssertReadOnlyProperty<Fenestration>(nameof(Fenestration.Id), typeof(EntityId));
        AssertReadOnlyProperty<Fenestration>(nameof(Fenestration.Construction), typeof(FenestrationConstruction));
        AssertReadOnlyProperty<Fenestration>(nameof(Fenestration.Blind), typeof(BlindType?));
        AssertReadOnlyProperty<Surface>(nameof(Surface.AdjacentZoneId), typeof(string));
        AssertReadOnlyProperty<Surface>(nameof(Surface.Area), typeof(double));
        AssertReadOnlyProperty<Surface>(nameof(Surface.WindowCount), typeof(int));
        AssertReadOnlyProperty<Zone>(nameof(Zone.Area), typeof(double));
        AssertReadOnlyProperty<Zone>(nameof(Zone.Infiltration), typeof(double));
        Assert.Equal(typeof(Surface), typeof(Surface).GetMethod(nameof(Surface.Flip))!.ReturnType);
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
            MaxDepth = 128,
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
            "artifacts",
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

        ValidateFixtureArtifacts(root.GetProperty("artifacts"));
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
        ValidateExcludedReceipts(root.GetProperty("excluded_receipts"), targets);
        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            fixtureCases.SelectMany(item => ReadStringArray(item.GetProperty("target_symbols")))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));
        return new OracleCorpus(fixtureCases, targets);
    }

    private static void ValidateFixtureArtifacts(JsonElement artifacts)
    {
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
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "byteorder",
            "dependencies",
            "implementation",
            "platform",
            "pointer_width_bits",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("little", RequiredString(runtime, "byteorder"));
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("win32", RequiredString(runtime, "platform"));
        Assert.Equal(64, runtime.GetProperty("pointer_width_bits").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));

        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(dependencies, "eppy", "numpy", "pandas", "python-dateutil", "pytz", "six", "tzdata");
        Assert.Equal("0.5.63", RequiredString(dependencies, "eppy"));
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("2.9.0.post0", RequiredString(dependencies, "python-dateutil"));
        Assert.Equal("2024.2", RequiredString(dependencies, "pytz"));
        Assert.Equal("1.16.0", RequiredString(dependencies, "six"));
        Assert.Equal("2024.2", RequiredString(dependencies, "tzdata"));
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(upstream, "commit", "inventory", "loaded_sources", "source");
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

        JsonElement[] loadedSources = upstream.GetProperty("loaded_sources").EnumerateArray().ToArray();
        Assert.Equal(23, loadedSources.Length);
        Assert.Equal(23, loadedSources.Select(item => RequiredString(item, "module"))
            .Distinct(StringComparer.Ordinal).Count());
        JsonElement loadedShape = Assert.Single(
            loadedSources,
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("epsimple.core.shape", RequiredString(loadedShape, "module"));
        Assert.Equal(UpstreamBytes, loadedShape.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(loadedShape, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(loadedShape, "ast_sha256"));
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
        Assert.Equal(
            InventoryContentSha256,
            RequiredString(inventoryDocument.RootElement, "content_sha256"));
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
            Assert.Equal(UpstreamPath, RequiredString(inventorySymbol, "path"));
            Assert.Equal(RequiredString(inventorySymbol, "symbol_hash"), RequiredString(receipt, "symbol_hash"));
            Assert.Equal(RequiredString(inventorySymbol, "signature_hash"), RequiredString(receipt, "signature_hash"));
            Assert.Equal(RequiredString(inventorySymbol, "body_hash"), RequiredString(receipt, "body_hash"));
            Assert.Equal(RequiredString(receipt, "symbol_hash"), RequiredString(descriptor, "symbol_hash"));
            Assert.Equal(RequiredString(receipt, "signature_hash"), RequiredString(descriptor, "signature_hash"));
            Assert.Equal(RequiredString(receipt, "body_hash"), RequiredString(descriptor, "body_hash"));
            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                expected.Kind,
                RequiredString(receipt, "symbol_hash"),
                RequiredString(receipt, "signature_hash"),
                RequiredString(receipt, "body_hash"),
                expected.AssertionId,
                expected.Classification,
                expected.AdaptationId,
                expected.NativeRoute,
                expected.CaseIndex);
        }

        Assert.Equal(53, targets.Length);
        Assert.Equal(
            new[]
            {
                405, 406, 407, 408, 409, 410, 411, 412, 413, 414, 415,
                417, 418, 419, 420, 421, 422, 423, 424, 426,
                429, 430, 431, 432, 433, 434, 435, 436, 437, 438, 439, 440, 441,
                442, 443, 444, 445, 446, 447, 448, 449,
                451, 452, 453, 454, 455, 456, 457, 458, 459, 460, 461, 462,
            },
            targets.Select(item => item.InventoryIndex));
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

    private static void ValidateConsumerContract(JsonElement contract, IReadOnlyList<TargetBinding> targets)
    {
        AssertKeys(
            contract,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classification_counts",
            "classifications",
            "closure",
            "native_routes",
            "target_symbols");
        Assert.Equal(17, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        AssertStringArray(contract.GetProperty("target_symbols"), ExpectedTargets.Select(item => item.Symbol));

        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(33, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(20, counts.GetProperty("exception").GetInt32());

        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement nativeRoutes = contract.GetProperty("native_routes");
        AssertKeys(assertions, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(classifications, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(nativeRoutes, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(
            adaptations,
            ExpectedTargets.Where(item => item.Classification == "exception")
                .Select(item => item.Symbol).ToArray());
        foreach (ExpectedTargetBinding expected in ExpectedTargets)
        {
            Assert.Equal(expected.AssertionId, RequiredString(assertions, expected.Symbol));
            Assert.Equal(expected.Classification, RequiredString(classifications, expected.Symbol));
            Assert.Equal(expected.NativeRoute, RequiredString(nativeRoutes, expected.Symbol));
            if (expected.Classification == "exception")
            {
                Assert.Equal(expected.AdaptationId, RequiredString(adaptations, expected.Symbol));
            }
            else
            {
                Assert.False(adaptations.TryGetProperty(expected.Symbol, out _));
                Assert.Equal("not_applicable", expected.AdaptationId);
            }
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(closure, "excluded_indices", "excluded_symbols", "target_count", "target_indices");
        Assert.Equal(53, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("target_indices")));
        Assert.Equal(ExcludedIndices, ReadIntArray(closure.GetProperty("excluded_indices")));
        AssertStringArray(closure.GetProperty("excluded_symbols"), ExcludedSymbols);
        Assert.Equal(ExpectedTargets.Select(item => item.AssertionId), targets.Select(item => item.AssertionId));
        Assert.Equal(ExpectedTargets.Select(item => item.Classification), targets.Select(item => item.Classification));
        Assert.Equal(ExpectedTargets.Select(item => item.AdaptationId), targets.Select(item => item.AdaptationId));
        Assert.Equal(ExpectedTargets.Select(item => item.NativeRoute), targets.Select(item => item.NativeRoute));
    }

    private static void ValidateExcludedReceipts(
        JsonElement excludedReceipts,
        IReadOnlyList<TargetBinding> targets)
    {
        JsonElement[] excluded = excludedReceipts.EnumerateArray().ToArray();
        Assert.Equal(5, excluded.Length);

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

            JsonElement inventoryItem = inventorySymbols[ExcludedIndices[index]];
            foreach (string field in new[] { "symbol", "kind", "path", "symbol_hash", "signature_hash", "body_hash" })
            {
                Assert.Equal(RequiredString(inventoryItem, field), RequiredString(item, field));
            }

            Assert.DoesNotContain(targets, target => target.InventoryIndex == ExcludedIndices[index]);
            Assert.DoesNotContain(targets, target => target.Symbol == ExcludedSymbols[index]);
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
        0 => ObserveB01(),
        1 => ObserveD01(),
        2 => ObserveF01(),
        3 => ObserveF02(),
        4 => ObserveG01(),
        5 => ObserveS01(),
        6 => ObserveS02(),
        7 => ObserveS03(),
        8 => ObserveS04(),
        9 => ObserveS05(),
        10 => ObserveW01(),
        11 => ObserveW02(),
        12 => ObserveZ01(),
        13 => ObserveZ02(),
        14 => ObserveZ03(),
        15 => ObserveZ04(),
        16 => ObserveZ05(),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
    };

    private static NativeObservation ObserveB01()
    {
        NativeGraph graph = CreateNativeGraph();
        string json = GrmWriter.Serialize(graph.Model, indented: false);
        GreenRetrofitModel reread = ReadRoundTrip(json);
        Surface wall = Assert.Single(reread.Zones).Surfaces.Single(item => item.Id.Value == "SURF-WALL");
        BlindType?[] parsed = wall.Fenestrations
            .Where(item => item.Type == FenestrationType.Window)
            .Select(item => item.Blind)
            .ToArray();
        Assert.Equal(new BlindType?[] { BlindType.Shade, BlindType.Venetian }, parsed);
        Assert.Contains("\"blind\":\"shade\"", json, StringComparison.Ordinal);
        Assert.Contains("\"blind\":\"venetian\"", json, StringComparison.Ordinal);
        Assert.False(Enum.IsDefined(typeof(BlindType), 99));
        return Observation(
            "B01",
            "native-route=BlindType-plus-GrmWriter.Serialize-plus-GrmReader.Read",
            "enum-members=" + Join(Enum.GetNames<BlindType>()),
            "shade-member=" + BlindType.Shade,
            "venetian-member=" + BlindType.Venetian,
            "serialized-vocabulary=shade|venetian",
            "reread-blinds=" + Join(parsed.Select(item => item!.Value.ToString())),
            "invalid-integer-defined=false",
            "adaptation=wire-vocabulary-is-lowercase-while-native-enum-ToString-is-title-case");
    }

    private static NativeObservation ObserveD01()
    {
        NativeGraph graph = CreateNativeGraph();
        Fenestration door = graph.Door;
        Assert.Equal(FenestrationType.Door, door.Type);
        Assert.Same(graph.OpaqueFenestrationConstruction, door.Construction);
        Exception transparent = Assert.Throws<ArgumentException>(() => new Fenestration(
            "invalid transparent door",
            FenestrationType.Door,
            1d,
            graph.TransparentFenestrationConstruction.Id.Value,
            graph.TransparentFenestrationConstruction,
            id: Id("FN-D-BAD-TRANSPARENT")));
        Exception blind = Assert.Throws<ArgumentException>(() => new Fenestration(
            "invalid shaded door",
            FenestrationType.Door,
            1d,
            graph.OpaqueFenestrationConstruction.Id.Value,
            graph.OpaqueFenestrationConstruction,
            BlindType.Shade,
            Id("FN-D-BAD-BLIND")));

        GreenRetrofitModel reread = ReadRoundTrip(GrmWriter.Serialize(graph.Model, indented: false));
        Fenestration rereadDoor = reread.Zones.SelectMany(item => item.Surfaces)
            .SelectMany(item => item.Fenestrations)
            .Single(item => item.Id.Equals(door.Id));
        Assert.Equal(FenestrationType.Door, rereadDoor.Type);
        Assert.NotNull(rereadDoor.Construction);

        GreenRetrofitConversionResult conversion = Convert(graph.Model);
        DragonSurface convertedWall = ConvertedSurface(conversion, graph.Wall.Id);
        DragonDoor convertedDoor = Assert.Single(convertedWall.Doors);
        Assert.Equal(door.Id, convertedDoor.Id);
        Assert.Equal(door.Area, convertedDoor.Area, 8);
        return Observation(
            "D01",
            "native-route=Fenestration(Door)-plus-GrmReader.Read-plus-GreenRetrofitConverter.Convert",
            "door-state=" + FenestrationState(door),
            "resolved-opaque-construction=" + rereadDoor.Construction!.Id.Value,
            "reader-retained-discriminator=" + rereadDoor.Type,
            "converter-opening-type=" + convertedDoor.GetType().Name,
            "converter-opening-area=" + Double(convertedDoor.Area),
            ExceptionFact("transparent-door", transparent),
            ExceptionFact("shaded-door", blind));
    }

    private static NativeObservation ObserveF01()
    {
        NativeGraph graph = CreateNativeGraph();
        Fenestration opening = new(
            "contract window",
            FenestrationType.Window,
            2.5d,
            graph.TransparentFenestrationConstruction.Id.Value,
            graph.TransparentFenestrationConstruction,
            BlindType.Shade,
            Id("FN-CONTRACT"));
        Assert.True(typeof(Fenestration).IsSealed);
        Assert.False(typeof(Fenestration).IsAbstract);
        Assert.Same(graph.TransparentFenestrationConstruction, opening.Construction);
        GreenRetrofitConversionResult conversion = Convert(graph.Model);
        Assert.NotNull(conversion.RequireEnergyModel());
        return Observation(
            "F01",
            "native-route=sealed-Fenestration-discriminator-plus-aggregate-GreenRetrofitConverter",
            "sealed=" + Boolean(typeof(Fenestration).IsSealed),
            "abstract=" + Boolean(typeof(Fenestration).IsAbstract),
            "constructed-discriminator=" + opening.Type,
            "construction-reference=" + opening.Construction!.Id.Value,
            "construction-reference-same=" + Boolean(ReferenceEquals(graph.TransparentFenestrationConstruction, opening.Construction)),
            "aggregate-conversion-success=" + Boolean(conversion.Success),
            "adaptation=no-abstract-instance-to-dragon-method");
    }

    private static NativeObservation ObserveF02()
    {
        NativeGraph graph = CreateNativeGraph();
        Fenestration source = graph.WindowShade;
        var reconstructed = new Fenestration(
            source.Name,
            source.Type,
            source.Area,
            source.ConstructionId,
            source.Construction,
            source.Blind,
            source.Id);
        Assert.NotSame(source, reconstructed);
        Assert.Equal(source.Id, reconstructed.Id);
        Assert.Equal(source.Blind, reconstructed.Blind);
        Assert.Same(source.Construction, reconstructed.Construction);

        GreenRetrofitModel reread = ReadRoundTrip(GrmWriter.Serialize(graph.Model, indented: false));
        Fenestration[] dispatch = reread.Zones.SelectMany(item => item.Surfaces)
            .SelectMany(item => item.Fenestrations)
            .ToArray();
        Assert.Contains(dispatch, item => item.Type == FenestrationType.Window);
        Assert.Contains(dispatch, item => item.Type == FenestrationType.GlassDoor);
        Assert.Contains(dispatch, item => item.Type == FenestrationType.Door);
        return Observation(
            "F02",
            "native-route=explicit-EntityId-plus-immutable-constructor-reconstruction-plus-GrmReader.Read",
            "source-id=" + source.Id.Value,
            "reconstructed-id=" + reconstructed.Id.Value,
            "distinct-reference=" + Boolean(!ReferenceEquals(source, reconstructed)),
            "blind-retained=" + reconstructed.Blind,
            "construction-reference-retained=" + Boolean(ReferenceEquals(source.Construction, reconstructed.Construction)),
            "reader-discriminators=" + Join(dispatch.Select(item => item.Type.ToString())),
            "adaptation=explicit-reconstruction-is-the-reviewed-deepcopy-route");
    }

    private static NativeObservation ObserveG01()
    {
        NativeGraph graph = CreateNativeGraph();
        Fenestration glassDoor = graph.GlassDoor;
        Assert.Equal(FenestrationType.GlassDoor, glassDoor.Type);
        Assert.True(glassDoor.Construction!.IsTransparent);
        Exception opaque = Assert.Throws<ArgumentException>(() => new Fenestration(
            "invalid opaque glass door",
            FenestrationType.GlassDoor,
            2d,
            graph.OpaqueFenestrationConstruction.Id.Value,
            graph.OpaqueFenestrationConstruction,
            id: Id("FN-GD-BAD")));

        GreenRetrofitModel reread = ReadRoundTrip(GrmWriter.Serialize(graph.Model, indented: false));
        Fenestration rereadGlassDoor = reread.Zones.SelectMany(item => item.Surfaces)
            .SelectMany(item => item.Fenestrations)
            .Single(item => item.Id.Equals(glassDoor.Id));
        Assert.Equal(FenestrationType.GlassDoor, rereadGlassDoor.Type);
        DragonWindow converted = Assert.Single(
            ConvertedSurface(Convert(graph.Model), graph.Wall.Id).Windows,
            item => item.Id.Equals(glassDoor.Id));
        return Observation(
            "G01",
            "native-route=Fenestration(GlassDoor)-discriminator-plus-reader-plus-converter",
            "source-discriminator=" + glassDoor.Type,
            "reader-discriminator=" + rereadGlassDoor.Type,
            "transparent-construction=" + Boolean(rereadGlassDoor.Construction!.IsTransparent),
            "converted-opening-type=" + converted.GetType().Name,
            "converted-opening-area=" + Double(converted.Area),
            ExceptionFact("opaque-glassdoor", opaque),
            "adaptation=glassdoor-remains-source-discriminator-and-converts-as-transparent-window");
    }

    private static NativeObservation ObserveS01()
    {
        NativeGraph graph = CreateNativeGraph();
        Surface wall = graph.Wall;
        var adjacent = new Surface(
            "adjacent wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Zone,
            12d,
            null,
            graph.SurfaceConstructionA.Id.Value,
            graph.SurfaceConstructionA,
            adjacentZoneId: "ZONE-NEIGHBOR",
            id: Id("SURF-ADJACENT"));
        Exception missingAzimuth = Assert.Throws<ArgumentException>(() => new Surface(
            "missing azimuth", SurfaceType.Wall, SurfaceBoundaryCondition.Outdoors, 10d,
            null, graph.SurfaceConstructionA.Id.Value, graph.SurfaceConstructionA));
        Exception extraAzimuth = Assert.Throws<ArgumentException>(() => new Surface(
            "extra azimuth", SurfaceType.Wall, SurfaceBoundaryCondition.Ground, 10d,
            20d, graph.SurfaceConstructionA.Id.Value, graph.SurfaceConstructionA));
        Exception mismatch = Assert.Throws<ArgumentException>(() => new Surface(
            "mismatch", SurfaceType.Floor, SurfaceBoundaryCondition.Ground, 10d,
            null, "SC-MISSING", graph.SurfaceConstructionA));
        Exception groundOpening = Assert.Throws<ArgumentException>(() => new Surface(
            "ground opening", SurfaceType.Floor, SurfaceBoundaryCondition.Ground, 10d,
            null, graph.SurfaceConstructionA.Id.Value, graph.SurfaceConstructionA,
            new[] { graph.WindowShade }));
        Exception missingAdjacent = Assert.Throws<ArgumentNullException>(() => new Surface(
            "missing adjacent", SurfaceType.Wall, SurfaceBoundaryCondition.Zone, 10d,
            null, graph.SurfaceConstructionA.Id.Value, graph.SurfaceConstructionA));
        Exception wrongReflectance = Assert.Throws<ArgumentException>(() => new Surface(
            "wall reflectance", SurfaceType.Wall, SurfaceBoundaryCondition.Outdoors, 10d,
            180d, graph.SurfaceConstructionA.Id.Value, graph.SurfaceConstructionA,
            coolRoofReflectance: 0.7d));
        Assert.Same(graph.SurfaceConstructionA, wall.Construction);
        Assert.Equal("ZONE-NEIGHBOR", adjacent.AdjacentZoneId);
        return Observation(
            "S01",
            "native-route=Surface-constructor-and-get-only-properties",
            "wall-state=" + SurfaceState(wall),
            "roof-reflectance=" + Double(graph.Roof.CoolRoofReflectance!.Value),
            "adjacent-zone-id=" + adjacent.AdjacentZoneId,
            "construction-reference-same=" + Boolean(ReferenceEquals(graph.SurfaceConstructionA, wall.Construction)),
            ExceptionFact("outdoor-wall-missing-azimuth", missingAzimuth),
            ExceptionFact("non-outdoor-wall-extra-azimuth", extraAzimuth),
            ExceptionFact("construction-id-mismatch", mismatch),
            ExceptionFact("ground-opening", groundOpening),
            ExceptionFact("zone-boundary-missing-id", missingAdjacent),
            ExceptionFact("wall-coolroof", wrongReflectance));
    }

    private static NativeObservation ObserveS02()
    {
        NativeGraph graph = CreateNativeGraph();
        Surface source = graph.Wall;
        var reconstructed = new Surface(
            source.Name,
            source.Type,
            source.BoundaryCondition,
            source.Area,
            source.Azimuth,
            source.ConstructionId,
            source.Construction,
            source.Fenestrations,
            source.CoolRoofReflectance,
            source.AdjacentZoneId,
            source.Id);
        Surface flipped = source.Flip();
        Surface repeated = source.Flip();
        Surface flippedFloor = graph.Floor.Flip();
        Surface flippedRoof = graph.Roof.Flip();
        Assert.NotSame(source, reconstructed);
        Assert.Equal(source.Id, reconstructed.Id);
        Assert.Equal(180d, source.Azimuth);
        Assert.Equal(0d, flipped.Azimuth);
        Assert.Equal(flipped.Id, repeated.Id);
        Assert.Equal(SurfaceType.Ceiling, flippedFloor.Type);
        Assert.Equal(SurfaceType.Floor, flippedRoof.Type);
        Assert.Null(flippedRoof.CoolRoofReflectance);
        return Observation(
            "S02",
            "native-route=Surface-constructor-reconstruction-plus-pure-Surface.Flip",
            "reconstruction-distinct=" + Boolean(!ReferenceEquals(source, reconstructed)),
            "reconstruction-id-retained=" + reconstructed.Id.Value,
            "reconstruction-opening-count=" + reconstructed.Fenestrations.Count,
            "original-azimuth-after-flip=" + Double(source.Azimuth!.Value),
            "flipped-azimuth=" + Double(flipped.Azimuth!.Value),
            "flipped-name=" + flipped.Name,
            "flipped-id-repeat-stable=" + Boolean(flipped.Id.Equals(repeated.Id)),
            "floor-flipped-type=" + flippedFloor.Type,
            "roof-flipped-type=" + flippedRoof.Type,
            "roof-reflectance-dropped=" + Boolean(!flippedRoof.CoolRoofReflectance.HasValue),
            "adaptation=no-inplace-mutation");
    }

    private static NativeObservation ObserveS03()
    {
        NativeGraph graph = CreateNativeGraph();
        var defined = new Surface(
            "defined", SurfaceType.Wall, SurfaceBoundaryCondition.Outdoors, 20d, 90d,
            graph.SurfaceConstructionA.Id.Value, graph.SurfaceConstructionA,
            new[] { graph.WindowShade }, id: Id("SURF-DEFINED"));
        var open = new Surface(
            "open", SurfaceType.Floor, SurfaceBoundaryCondition.Adiabatic, 12d, null,
            "open", null, id: Id("SURF-OPEN"));
        var unknown = new Surface(
            "unknown", SurfaceType.Ceiling, SurfaceBoundaryCondition.Adiabatic, 12d, null,
            null, null, id: Id("SURF-UNKNOWN"));
        GreenRetrofitModel source = ModelFor(
            graph,
            new[] { defined, open, unknown },
            supplyAssignments: Array.Empty<SupplySystemAssignment>(),
            ventilationAssignments: Array.Empty<VentilationAssignment>(),
            supplySystems: Array.Empty<SupplySystem>(),
            ventilationSystems: Array.Empty<VentilationSystem>(),
            zoneId: "ZONE-JSON-SURFACE");
        GreenRetrofitModel reread = ReadRoundTrip(GrmWriter.Serialize(source, indented: false));
        Surface[] surfaces = Assert.Single(reread.Zones).Surfaces.ToArray();
        Surface rereadDefined = surfaces.Single(item => item.Id.Value == "SURF-DEFINED");
        Surface rereadOpen = surfaces.Single(item => item.Id.Value == "SURF-OPEN");
        Surface rereadUnknown = surfaces.Single(item => item.Id.Value == "SURF-UNKNOWN");
        Assert.Equal(SurfaceConstructionReferenceKind.Defined, rereadDefined.ConstructionReferenceKind);
        Assert.Equal(SurfaceConstructionReferenceKind.Open, rereadOpen.ConstructionReferenceKind);
        Assert.Equal(SurfaceConstructionReferenceKind.Unknown, rereadUnknown.ConstructionReferenceKind);
        Assert.NotNull(rereadDefined.Construction);
        Assert.Null(rereadOpen.Construction);
        Assert.Null(rereadUnknown.Construction);
        Assert.Equal(FenestrationType.Window, Assert.Single(rereadDefined.Fenestrations).Type);
        return Observation(
            "S03",
            "native-route=GrmWriter.Serialize-plus-GrmReader.Read-surface-reference-resolution",
            "defined-kind=" + rereadDefined.ConstructionReferenceKind,
            "defined-construction=" + rereadDefined.Construction!.Id.Value,
            "defined-opening-type=" + Assert.Single(rereadDefined.Fenestrations).Type,
            "open-kind=" + rereadOpen.ConstructionReferenceKind,
            "open-construction-null=" + Boolean(rereadOpen.Construction is null),
            "unknown-kind=" + rereadUnknown.ConstructionReferenceKind,
            "unknown-construction-id-null=" + Boolean(rereadUnknown.ConstructionId is null),
            "surface-count=" + surfaces.Length);
    }

    private static NativeObservation ObserveS04()
    {
        NativeGraph graph = CreateNativeGraph();
        Assert.Equal(3, graph.Wall.WindowCount);
        Assert.Equal(1, graph.Wall.DoorCount);
        Assert.Equal(2, graph.Model.FenestrationConstructions.Count);
        Assert.Equal(
            new[] { "FC-G", "FC-D" },
            graph.Model.FenestrationConstructions.Select(item => item.Id.Value));
        Assert.Equal(
            new[] { "FC-G", "FC-D" },
            graph.Wall.Fenestrations.Select(item => item.ConstructionId)
                .Distinct(StringComparer.Ordinal));
        return Observation(
            "S04",
            "native-route=Surface.WindowCount-plus-DoorCount-plus-GreenRetrofitModel.FenestrationConstructions",
            "opening-discriminators=" + Join(graph.Wall.Fenestrations.Select(item => item.Type.ToString())),
            "window-count-includes-glassdoor=" + graph.Wall.WindowCount,
            "door-count=" + graph.Wall.DoorCount,
            "model-fenestration-catalog=" + Join(graph.Model.FenestrationConstructions.Select(item => item.Id.Value)),
            "used-construction-ids=" + Join(graph.Wall.Fenestrations.Select(item => item.ConstructionId).Distinct(StringComparer.Ordinal)),
            "adaptation=unique-construction-aggregation-is-owned-by-model-catalog");
    }

    private static NativeObservation ObserveS05()
    {
        NativeGraph graph = CreateNativeGraph();
        GreenRetrofitConversionResult conversion = Convert(graph.Model);
        DragonSurface wall = ConvertedSurface(conversion, graph.Wall.Id);
        Assert.Equal(graph.Wall.Area, wall.GrossArea, 8);
        Assert.Equal(graph.Wall.Fenestrations.Sum(item => item.Area), wall.OpeningArea, 8);
        Assert.Equal(3, wall.Windows.Count);
        Assert.Single(wall.Doors);
        Assert.True(wall.Validate().IsValid, Describe(wall.Validate().Diagnostics));
        return Observation(
            "S05",
            "native-route=GreenRetrofitConverter.Convert-surface-geometry-and-openings",
            "conversion-success=" + Boolean(conversion.Success),
            "converted-gross-area=" + Double(wall.GrossArea),
            "converted-opening-area=" + Double(wall.OpeningArea),
            "converted-net-area=" + Double(wall.NetArea),
            "converted-window-count=" + wall.Windows.Count,
            "converted-door-count=" + wall.Doors.Count,
            "converted-opening-types=" + Join(wall.Openings.Select(item => item.GetType().Name)),
            "converted-surface-valid=" + Boolean(wall.Validate().IsValid));
    }

    private static NativeObservation ObserveW01()
    {
        NativeGraph graph = CreateNativeGraph();
        Fenestration window = graph.WindowShade;
        Assert.Equal(FenestrationType.Window, window.Type);
        Assert.Equal(BlindType.Shade, window.Blind);
        Assert.Same(graph.TransparentFenestrationConstruction, window.Construction);
        Exception opaque = Assert.Throws<ArgumentException>(() => new Fenestration(
            "opaque window", FenestrationType.Window, 1d,
            graph.OpaqueFenestrationConstruction.Id.Value,
            graph.OpaqueFenestrationConstruction,
            id: Id("FN-W-BAD-OPAQUE")));
        Exception invalidBlind = Assert.Throws<ArgumentOutOfRangeException>(() => new Fenestration(
            "invalid blind", FenestrationType.Window, 1d,
            graph.TransparentFenestrationConstruction.Id.Value,
            graph.TransparentFenestrationConstruction,
            (BlindType)99,
            Id("FN-W-BAD-BLIND")));
        Exception zeroArea = Assert.Throws<ArgumentOutOfRangeException>(() => new Fenestration(
            "zero area", FenestrationType.Window, 0d,
            graph.TransparentFenestrationConstruction.Id.Value,
            graph.TransparentFenestrationConstruction,
            id: Id("FN-W-BAD-AREA")));
        return Observation(
            "W01",
            "native-route=Fenestration(Window)-constructor-validation-and-properties",
            "window-state=" + FenestrationState(window),
            "blind=" + window.Blind,
            "transparent-construction=" + Boolean(window.Construction!.IsTransparent),
            "construction-reference-same=" + Boolean(ReferenceEquals(graph.TransparentFenestrationConstruction, window.Construction)),
            ExceptionFact("opaque-window", opaque),
            ExceptionFact("invalid-blind", invalidBlind),
            ExceptionFact("zero-area", zeroArea),
            "adaptation=window-is-a-discriminator-not-a-subclass");
    }

    private static NativeObservation ObserveW02()
    {
        NativeGraph graph = CreateNativeGraph();
        string json = GrmWriter.Serialize(graph.Model, indented: false);
        GreenRetrofitModel reread = ReadRoundTrip(json);
        Surface rereadWall = Assert.Single(reread.Zones).Surfaces.Single(item => item.Id.Equals(graph.Wall.Id));
        Fenestration shade = rereadWall.Fenestrations.Single(item => item.Id.Equals(graph.WindowShade.Id));
        Fenestration venetian = rereadWall.Fenestrations.Single(item => item.Id.Equals(graph.WindowVenetian.Id));
        Assert.Equal(BlindType.Shade, shade.Blind);
        Assert.Equal(BlindType.Venetian, venetian.Blind);
        DragonSurface convertedWall = ConvertedSurface(Convert(reread), graph.Wall.Id);
        DragonWindow shadeWindow = convertedWall.Windows.Single(item => item.Id.Equals(graph.WindowShade.Id));
        DragonWindow venetianWindow = convertedWall.Windows.Single(item => item.Id.Equals(graph.WindowVenetian.Id));
        Assert.Equal("Shade", shadeWindow.Shading!.GetType().Name);
        Assert.Equal("Blind", venetianWindow.Shading!.GetType().Name);
        return Observation(
            "W02",
            "native-route=GrmReader.Read-window-blinds-plus-GreenRetrofitConverter.Convert",
            "reader-shade=" + shade.Blind,
            "reader-venetian=" + venetian.Blind,
            "writer-has-shade-token=" + Boolean(json.Contains("\"blind\":\"shade\"", StringComparison.Ordinal)),
            "writer-has-venetian-token=" + Boolean(json.Contains("\"blind\":\"venetian\"", StringComparison.Ordinal)),
            "converted-shade-type=" + shadeWindow.Shading.GetType().Name,
            "converted-venetian-type=" + venetianWindow.Shading.GetType().Name,
            "converted-window-count=" + convertedWall.Windows.Count);
    }

    private static NativeObservation ObserveZ01()
    {
        NativeGraph graph = CreateNativeGraph();
        Zone zone = graph.Zone;
        Exception duplicateSupply = Assert.Throws<ArgumentException>(() => new Zone(
            "duplicate supply", 1, 3d, graph.Zone.Surfaces,
            graph.Profile.Name, graph.Profile, 8d,
            new[]
            {
                new SupplySystemAssignment(graph.CoolingSupply.Id.Value, graph.CoolingSupply),
                new SupplySystemAssignment(graph.CoolingSupply.Id.Value, graph.CoolingSupply),
            },
            id: Id("ZONE-DUP-SUPPLY")));
        var radiantA = new SupplySystem(
            "radiant A", SupplySystemType.ElectricRadiantFloor, id: Id("SUP-RADIANT-A"));
        var radiantB = new SupplySystem(
            "radiant B", SupplySystemType.ElectricRadiantFloor, id: Id("SUP-RADIANT-B"));
        Exception duplicateRadiant = Assert.Throws<ArgumentException>(() => new Zone(
            "duplicate radiant", 1, 3d, graph.Zone.Surfaces,
            graph.Profile.Name, graph.Profile, 8d,
            new[]
            {
                new SupplySystemAssignment(radiantA.Id.Value, radiantA),
                new SupplySystemAssignment(radiantB.Id.Value, radiantB),
            },
            id: Id("ZONE-DUP-RADIANT")));
        Exception invalidHeight = Assert.Throws<ArgumentOutOfRangeException>(() => new Zone(
            "invalid height", 1, 0d, graph.Zone.Surfaces,
            graph.Profile.Name, graph.Profile, 8d,
            id: Id("ZONE-BAD-HEIGHT")));
        Assert.Equal("ZONE-A", zone.Id.Value);
        Assert.Equal(3d, zone.Height);
        Assert.Equal(2, zone.SupplySystems.Count);
        return Observation(
            "Z01",
            "native-route=Zone-constructor-Id-Height-SupplySystems-validation",
            "zone-id=" + zone.Id.Value,
            "zone-height=" + Double(zone.Height),
            "resolved-supply-ids=" + Join(zone.SupplySystems.Select(item => item.Id.Value)),
            ExceptionFact("duplicate-supply-id", duplicateSupply),
            ExceptionFact("multiple-radiant", duplicateRadiant),
            ExceptionFact("nonpositive-height", invalidHeight),
            "adaptation=explicit-id-and-immutable-input-copies");
    }

    private static NativeObservation ObserveZ02()
    {
        NativeGraph graph = CreateNativeGraph();
        var secondFloor = new Surface(
            "second floor", SurfaceType.Floor, SurfaceBoundaryCondition.Ground, 12d, null,
            graph.SurfaceConstructionB.Id.Value, graph.SurfaceConstructionB,
            id: Id("SURF-FLOOR-SECOND"));
        var filtered = new Zone(
            "filtered", 1, 3d,
            graph.Zone.Surfaces.Concat(new[] { secondFloor }),
            graph.Profile.Name,
            graph.Profile,
            8d,
            new[]
            {
                new SupplySystemAssignment(graph.CoolingSupply.Id.Value, graph.CoolingSupply),
                new SupplySystemAssignment(graph.HeatingSupply.Id.Value, graph.HeatingSupply),
                new SupplySystemAssignment("SUP-UNRESOLVED"),
            },
            id: Id("ZONE-FILTERED"));
        var doorWall = new Surface(
            "door wall", SurfaceType.Wall, SurfaceBoundaryCondition.Outdoors, 20d, 0d,
            graph.SurfaceConstructionA.Id.Value, graph.SurfaceConstructionA,
            new[] { graph.Door }, id: Id("SURF-DOOR-ONLY"));
        var doorOnly = new Zone(
            "door only", 1, 3d, new[] { graph.Floor, doorWall },
            graph.Profile.Name, graph.Profile, 8d, id: Id("ZONE-DOOR-ONLY"));
        Assert.Equal(60d, filtered.Area);
        Assert.Equal(1.5d, filtered.Infiltration);
        Assert.Equal(0d, doorOnly.Infiltration);
        Assert.Equal(new[] { "SUP-COOL", "SUP-HEAT" }, filtered.SupplySystems.Select(item => item.Id.Value));
        Assert.Equal("SUP-COOL", Assert.Single(filtered.CoolingSupplySystems).Id.Value);
        Assert.Equal("SUP-HEAT", Assert.Single(filtered.HeatingSupplySystems).Id.Value);
        return Observation(
            "Z02",
            "native-route=Zone.Area-Infiltration-SupplySystems-heating/cooling-filters",
            "floor-area-sum=" + Double(filtered.Area),
            "windowed-outdoor-infiltration=" + Double(filtered.Infiltration),
            "door-only-infiltration=" + Double(doorOnly.Infiltration),
            "assignment-count=" + filtered.SupplySystemAssignments.Count,
            "resolved-supply-count=" + filtered.SupplySystems.Count,
            "resolved-supply-ids=" + Join(filtered.SupplySystems.Select(item => item.Id.Value)),
            "cooling-supply-ids=" + Join(filtered.CoolingSupplySystems.Select(item => item.Id.Value)),
            "heating-supply-ids=" + Join(filtered.HeatingSupplySystems.Select(item => item.Id.Value)));
    }

    private static NativeObservation ObserveZ03()
    {
        NativeGraph graph = CreateNativeGraph();
        string json = GrmWriter.Serialize(graph.Model, indented: false);
        GreenRetrofitModel reread = ReadRoundTrip(json);
        Zone zone = Assert.Single(reread.Zones);
        Assert.Equal(3, zone.Surfaces.Count);
        Assert.Equal(4, zone.Surfaces.Sum(item => item.Fenestrations.Count));
        Assert.NotNull(zone.Profile);
        Assert.Equal(2, zone.SupplySystemAssignments.Count);
        Assert.Equal(2, zone.SupplySystems.Count);
        Assert.Single(zone.VentilationAssignments);
        Assert.NotNull(Assert.Single(zone.VentilationAssignments).VentilationSystem);
        return Observation(
            "Z03",
            "native-route=GrmWriter.Serialize-plus-GrmReader.Read-zone-graph",
            "zone-id=" + zone.Id.Value,
            "surface-count=" + zone.Surfaces.Count,
            "fenestration-count=" + zone.Surfaces.Sum(item => item.Fenestrations.Count),
            "profile-resolved=" + Boolean(zone.Profile is not null),
            "supply-assignment-count=" + zone.SupplySystemAssignments.Count,
            "resolved-supply-count=" + zone.SupplySystems.Count,
            "ventilation-assignment-count=" + zone.VentilationAssignments.Count,
            "ventilation-resolved=" + Boolean(zone.VentilationAssignments.All(item => item.VentilationSystem is not null)));
    }

    private static NativeObservation ObserveZ04()
    {
        NativeGraph graph = CreateNativeGraph();
        string[] materialCatalog = graph.Model.Materials.Select(item => item.Id.Value).ToArray();
        string[] surfaceCatalog = graph.Model.SurfaceConstructions.Select(item => item.Id.Value).ToArray();
        string[] fenestrationCatalog = graph.Model.FenestrationConstructions.Select(item => item.Id.Value).ToArray();
        string[] usedMaterials = graph.Model.SurfaceConstructions.SelectMany(item => item.Layers)
            .Select(item => item.Material.Id.Value).Distinct(StringComparer.Ordinal).ToArray();
        string[] usedSurfaces = graph.Model.Zones.SelectMany(item => item.Surfaces)
            .Select(item => item.ConstructionId!).Distinct(StringComparer.Ordinal).ToArray();
        string[] usedFenestrations = graph.Model.Zones.SelectMany(item => item.Surfaces)
            .SelectMany(item => item.Fenestrations).Select(item => item.ConstructionId)
            .Distinct(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "MAT-A", "MAT-B" }, materialCatalog);
        Assert.Equal(new[] { "SC-A", "SC-B" }, surfaceCatalog);
        Assert.Equal(new[] { "FC-G", "FC-D" }, fenestrationCatalog);
        Assert.Equal(materialCatalog, usedMaterials);
        Assert.Equal(surfaceCatalog, usedSurfaces);
        Assert.Equal(fenestrationCatalog, usedFenestrations);
        return Observation(
            "Z04",
            "native-route=GreenRetrofitModel-material/surface/fenestration-catalogs",
            "material-catalog=" + Join(materialCatalog),
            "surface-construction-catalog=" + Join(surfaceCatalog),
            "fenestration-construction-catalog=" + Join(fenestrationCatalog),
            "used-material-ids=" + Join(usedMaterials),
            "used-surface-construction-ids=" + Join(usedSurfaces),
            "used-fenestration-construction-ids=" + Join(usedFenestrations),
            "adaptation=aggregation-is-explicit-model-level-catalog-state");
    }

    private static NativeObservation ObserveZ05()
    {
        NativeGraph graph = CreateNativeGraph();
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(
            graph.Model,
            new GreenRetrofitConversionOptions
            {
                Database = SimpleDragonDatabase.Default,
                ResolveUnknownConstructions = true,
                IncludeModelValidationDiagnostics = true,
            });
        Assert.True(conversion.Success, Describe(conversion.Diagnostics));
        var energyModel = conversion.RequireEnergyModel();
        Assert.Single(energyModel.Zones);
        Assert.Equal(3, energyModel.Surfaces.Count);
        Assert.Equal(4, energyModel.Surfaces.Sum(item => item.Openings.Count));
        Assert.Equal(3, conversion.SurfaceConversions.Count);
        Assert.All(conversion.SurfaceConversions, item => Assert.False(item.IsSynthesizedCounterpart));
        Assert.True(energyModel.Validate().IsValid, Describe(energyModel.Validate().Diagnostics));
        return Observation(
            "Z05",
            "native-route=GreenRetrofitConverter.Convert-implements-upstream-Zone.to_dragon-gap",
            "conversion-success=" + Boolean(conversion.Success),
            "energy-model-zone-count=" + energyModel.Zones.Count,
            "energy-model-surface-count=" + energyModel.Surfaces.Count,
            "energy-model-opening-count=" + energyModel.Surfaces.Sum(item => item.Openings.Count),
            "surface-conversion-count=" + conversion.SurfaceConversions.Count,
            "synthesized-counterpart-count=" + conversion.SurfaceConversions.Count(item => item.IsSynthesizedCounterpart),
            "energy-model-valid=" + Boolean(energyModel.Validate().IsValid),
            "adaptation=native-converter-succeeds-where-upstream-method-raises-NotImplementedError");
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
        var surfaceConstructionA = new SurfaceConstruction(
            "Construction A",
            new[]
            {
                new SurfaceConstructionLayer(materialA, 0.2d),
                new SurfaceConstructionLayer(materialB, 0.08d),
            },
            Id("SC-A"));
        var surfaceConstructionB = new SurfaceConstruction(
            "Construction B",
            new[] { new SurfaceConstructionLayer(materialB, 0.15d) },
            Id("SC-B"));
        var transparent = new FenestrationConstruction("Glazing", 1.4d, 0.5d, Id("FC-G"));
        var opaque = new FenestrationConstruction("Door construction", 2.2d, id: Id("FC-D"));
        var shade = new Fenestration(
            "Shade window", FenestrationType.Window, 3d,
            transparent.Id.Value, transparent, BlindType.Shade, Id("FN-W-SHADE"));
        var venetian = new Fenestration(
            "Venetian window", FenestrationType.Window, 2d,
            transparent.Id.Value, transparent, BlindType.Venetian, Id("FN-W-VENETIAN"));
        var glassDoor = new Fenestration(
            "Glass door", FenestrationType.GlassDoor, 4d,
            transparent.Id.Value, transparent, id: Id("FN-GLASSDOOR"));
        var door = new Fenestration(
            "Door", FenestrationType.Door, 2d,
            opaque.Id.Value, opaque, id: Id("FN-DOOR"));
        var wall = new Surface(
            "South wall", SurfaceType.Wall, SurfaceBoundaryCondition.Outdoors, 48d, 180d,
            surfaceConstructionA.Id.Value, surfaceConstructionA,
            new[] { shade, venetian, glassDoor, door }, id: Id("SURF-WALL"));
        var floor = new Surface(
            "Floor", SurfaceType.Floor, SurfaceBoundaryCondition.Ground, 48d, null,
            surfaceConstructionB.Id.Value, surfaceConstructionB, id: Id("SURF-FLOOR"));
        var roof = new Surface(
            "Roof", SurfaceType.Ceiling, SurfaceBoundaryCondition.Outdoors, 48d, null,
            surfaceConstructionA.Id.Value, surfaceConstructionA,
            coolRoofReflectance: 0.7d, id: Id("SURF-ROOF"));

        UsageProfile profile = SimpleDragonDatabase.Default.UsageProfiles.Find("소규모사무실").Require();
        var cooling = new SupplySystem(
            "Cooling", SupplySystemType.PackagedAirConditioner,
            coolingCop: 3.2d, coolingCapacity: 8_000d, id: Id("SUP-COOL"));
        var heating = new SupplySystem(
            "Heating", SupplySystemType.ElectricRadiator,
            heatingCapacity: 7_000d, id: Id("SUP-HEAT"));
        var ventilation = new VentilationSystem(
            "Ventilation", 0.2d, 0.75d, 0.5d, Id("VENT-A"));
        var zone = new Zone(
            "Zone A",
            1,
            3d,
            new[] { wall, floor, roof },
            profile.Name,
            profile,
            8d,
            new[]
            {
                new SupplySystemAssignment(cooling.Id.Value, cooling),
                new SupplySystemAssignment(heating.Id.Value, heating),
            },
            new[] { new VentilationAssignment(ventilation.Id.Value, 1, ventilation) },
            Id("ZONE-A"));
        var model = new GreenRetrofitModel(
            "Shape Core Native Model",
            0d,
            "서울특별시 관악구",
            new DateTime(2020, 1, 1),
            false,
            new[] { new BuildingFloor(1, new[] { zone }) },
            new[] { materialA, materialB },
            new[] { surfaceConstructionA, surfaceConstructionB },
            new[] { transparent, opaque },
            supplySystems: new[] { cooling, heating },
            ventilationSystems: new[] { ventilation });
        return new NativeGraph(
            materialA,
            materialB,
            surfaceConstructionA,
            surfaceConstructionB,
            transparent,
            opaque,
            shade,
            venetian,
            glassDoor,
            door,
            wall,
            floor,
            roof,
            profile,
            cooling,
            heating,
            ventilation,
            zone,
            model);
    }

    private static GreenRetrofitModel ModelFor(
        NativeGraph graph,
        IEnumerable<Surface> surfaces,
        IEnumerable<SupplySystemAssignment> supplyAssignments,
        IEnumerable<VentilationAssignment> ventilationAssignments,
        IEnumerable<SupplySystem> supplySystems,
        IEnumerable<VentilationSystem> ventilationSystems,
        string zoneId)
    {
        var zone = new Zone(
            "Model zone",
            1,
            3d,
            surfaces,
            graph.Profile.Name,
            graph.Profile,
            8d,
            supplyAssignments,
            ventilationAssignments,
            Id(zoneId));
        return new GreenRetrofitModel(
            "Shape reference model",
            0d,
            "서울특별시 관악구",
            new DateTime(2020, 1, 1),
            false,
            new[] { new BuildingFloor(1, new[] { zone }) },
            new[] { graph.MaterialA, graph.MaterialB },
            new[] { graph.SurfaceConstructionA, graph.SurfaceConstructionB },
            new[] { graph.TransparentFenestrationConstruction, graph.OpaqueFenestrationConstruction },
            supplySystems: supplySystems,
            ventilationSystems: ventilationSystems);
    }

    private static GreenRetrofitModel ReadRoundTrip(string json)
    {
        GrmReadResult result = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(result.Success, Describe(result.Diagnostics));
        return result.RequireModel();
    }

    private static GreenRetrofitConversionResult Convert(GreenRetrofitModel model)
    {
        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(
            model,
            new GreenRetrofitConversionOptions
            {
                Database = SimpleDragonDatabase.Default,
                ResolveUnknownConstructions = true,
                IncludeModelValidationDiagnostics = true,
            });
        Assert.True(result.Success, Describe(result.Diagnostics));
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
                exact_target_count = 53,
                equivalent_target_count = 33,
                exception_target_count = 20,
                exact_case_count = 17,
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

        JsonElement scope = receipt.GetProperty("scope");
        Assert.Equal(53, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(33, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(20, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(17, scope.GetProperty("exact_case_count").GetInt32());
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

    private static string FenestrationState(Fenestration value) => Join(new[]
    {
        "id=" + value.Id.Value,
        "type=" + value.Type,
        "area=" + Double(value.Area),
        "construction=" + value.ConstructionId,
        "blind=" + (value.Blind?.ToString() ?? "null"),
    });

    private static string SurfaceState(Surface value) => Join(new[]
    {
        "id=" + value.Id.Value,
        "type=" + value.Type,
        "boundary=" + value.BoundaryCondition,
        "area=" + Double(value.Area),
        "azimuth=" + (value.Azimuth.HasValue ? Double(value.Azimuth.Value) : "null"),
        "construction-kind=" + value.ConstructionReferenceKind,
        "window-count=" + value.WindowCount.ToString(CultureInfo.InvariantCulture),
        "door-count=" + value.DoorCount.ToString(CultureInfo.InvariantCulture),
    });

    private static string ExceptionFact(string phase, Exception exception)
    {
        string parameter = exception is ArgumentException argument
            ? argument.ParamName ?? "none"
            : "not-applicable";
        return phase + "=" + exception.GetType().Name + "|param=" + parameter;
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics) => string.Join(
        " | ",
        diagnostics.Select(item => item.Code + ":" + item.Severity));

    private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

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

    private static void AssertKeys(JsonElement value, params string[] expected) => Assert.Equal(
        expected.OrderBy(item => item, StringComparer.Ordinal),
        value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));

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
        return property.GetString()!;
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
        string CaseSha256,
        string FactsSha256,
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
        SurfaceConstruction SurfaceConstructionA,
        SurfaceConstruction SurfaceConstructionB,
        FenestrationConstruction TransparentFenestrationConstruction,
        FenestrationConstruction OpaqueFenestrationConstruction,
        Fenestration WindowShade,
        Fenestration WindowVenetian,
        Fenestration GlassDoor,
        Fenestration Door,
        Surface Wall,
        Surface Floor,
        Surface Roof,
        UsageProfile Profile,
        SupplySystem CoolingSupply,
        SupplySystem HeatingSupply,
        VentilationSystem Ventilation,
        Zone Zone,
        GreenRetrofitModel Model);
}
