#pragma warning disable CA1861 // Immutable inline arrays make exact oracle expectations readable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.UpstreamTracker;
using DragonDoor = Dragons.InvisibleDragon.Shape.Door;
using DragonSurface = Dragons.InvisibleDragon.Shape.Surface;
using DragonWindow = Dragons.InvisibleDragon.Shape.Window;

namespace Dragons.SimpleDragon.Tests;

public sealed class ShapeCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-shape-core-oracle.json";
    private const int FixtureBytes = 108_261;
    private const string FixtureSha256 =
        "sha256:1beff8671a20e03e968dd0570aae174282752b5b28feefcd035ca136d023f90f";
    private const string FixtureSchema = "dragons.python-reference.epsimple-shape-core.v1";
    private const string CasesSha256 =
        "sha256:1b6be41823b3a165d1e5c923f46278a44ae8ff68ccef1a0edd08d72ab637398e";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_shape_core_oracle.py";
    private const int GeneratorBytes = 73_185;
    private const string GeneratorSha256 =
        "sha256:383cde612b61410cda7e35bd309a936824a96c5e0b95d876f6d98bebb5af5c9f";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_shape_core_oracle.py";
    private const int ValidatorBytes = 16_594;
    private const string ValidatorSha256 =
        "sha256:1260a5f59db7876a2dc67725edd3be847984d3f7f56ce92c3d3095a0737b173b";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/core/shape.py";
    private const int UpstreamBytes = 22_922;
    private const string UpstreamSourceSha256 =
        "sha256:9caa67d424693afc58ee6a456c86d42d504fce4e30e56d73e8ee658dc8e515c1";
    private const string UpstreamAstSha256 =
        "sha256:63cfdec0aec079cfc2d2896091974a5c253656e198cbcb1ea328dbace92c1b7e";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.ShapeCoreOracleParityTests.MatchesPinnedShapeCoreThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeArtifacts =
    {
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
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/Material.cs", 1_988,
            "sha256:a574a5a93277be915c4a9a20e81d5e13fd7d52d0e43b7ba120078fb4eb8d672e"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/SurfaceConstruction.cs", 7_269,
            "sha256:605f54f51df2690cef21885171d6c72752022823f393f872c836160312cf03c6"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/FenestrationConstruction.cs", 3_953,
            "sha256:6e8fb7cf51f284d51fb37d5a1b88626422e7ace34a3187d7e0e73196a3a96073"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SupplySystem.cs", 6_456,
            "sha256:7ee0ec0b4eca1a78b4c6df5f6ba452b784bf09859ff24e6f50c681d16a63f1cb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/OtherSystems.cs", 3_846,
            "sha256:e1aba0e081e550031cb5dfd9f83f0bc8016c89c36cc2ab1b80c7a6af35aa7714"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Profiles/UsageProfile.cs", 8_861,
            "sha256:5478c330a208c61d797413212024933db98fb6fffe021be7f9b73c30ffd079b1"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Profiles/ProfileDatabases.cs", 6_747,
            "sha256:041b28085203376258726c21033c140a04a6fed65bcd07cd9ea429ced5d73bf1"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonDatabase.cs", 2_537,
            "sha256:af9d3176183292b19e2304e9be3e000e266a6d858d462bdfd65d042d1568147b"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Internal/DomainSupport.cs", 3_757,
            "sha256:40ea55a659ab84d3ccfff8dc2d36ad8ff9612b21bf8fdf96dedef2d76e144005"),
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
        ExpectedTarget(405, "BlindType", "class", "epsimple-shape-core-405-6008dd91", "equivalent", "not_applicable", "Dragons.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(406, "BlindType.SHADE", "constant", "epsimple-shape-core-406-bb03051d", "equivalent", "not_applicable", "Dragons.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(407, "BlindType.VENETIAN", "constant", "epsimple-shape-core-407-09c92f4a", "equivalent", "not_applicable", "Dragons.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(408, "BlindType.__str__", "function", "epsimple-shape-core-408-f40e4929", "exception", "grm-vocabulary-rather-than-native-enum-tostring-f40e4929", "Dragons.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        ExpectedTarget(409, "Door", "class", "epsimple-shape-core-409-8c468e24", "exception", "unified-immutable-fenestration-with-door-discriminator-8c468e24", "Dragons.SimpleDragon.Fenestration with Dragons.SimpleDragon.FenestrationType", 1),
        ExpectedTarget(410, "Door.construction", "function", "epsimple-shape-core-410-2ca0072c", "equivalent", "not_applicable", "Dragons.SimpleDragon.Fenestration.Construction", 1),
        ExpectedTarget(411, "Door.from_json", "function", "epsimple-shape-core-411-26b0f9bb", "equivalent", "not_applicable", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 1),
        ExpectedTarget(412, "Door.to_dragon", "function", "epsimple-shape-core-412-eb81bd06", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 1),
        ExpectedTarget(413, "Fenestration", "class", "epsimple-shape-core-413-43d44ea1", "exception", "sealed-discriminated-native-fenestration-rather-than-abc-43d44ea1", "Dragons.SimpleDragon.Fenestration with Dragons.SimpleDragon.FenestrationType", 2),
        ExpectedTarget(414, "Fenestration.ID", "function", "epsimple-shape-core-414-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.Fenestration.Id", 3),
        ExpectedTarget(415, "Fenestration.__deepcopy__", "function", "epsimple-shape-core-415-a0dbc411", "exception", "immutable-native-fenestration-explicit-reconstruction-a0dbc411", "Dragons.SimpleDragon.Fenestration constructor", 3),
        ExpectedTarget(417, "Fenestration.__init__", "function", "epsimple-shape-core-417-1b22b2f1", "exception", "deterministic-native-id-and-discriminated-constructor-1b22b2f1", "Dragons.SimpleDragon.Fenestration constructor", 2),
        ExpectedTarget(418, "Fenestration.construction", "function", "epsimple-shape-core-418-0b0cbf2f", "exception", "immutable-resolved-native-construction-reference-0b0cbf2f", "Dragons.SimpleDragon.Fenestration.Construction", 2),
        ExpectedTarget(419, "Fenestration.from_json", "function", "epsimple-shape-core-419-2e553f68", "equivalent", "not_applicable", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 3),
        ExpectedTarget(420, "Fenestration.to_dragon", "function", "epsimple-shape-core-420-ede823e2", "exception", "aggregate-native-converter-rather-than-abstract-instance-method-ede823e2", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 2),
        ExpectedTarget(421, "GlassDoor", "class", "epsimple-shape-core-421-1981a404", "exception", "unified-immutable-fenestration-with-glassdoor-discriminator-1981a404", "Dragons.SimpleDragon.Fenestration with Dragons.SimpleDragon.FenestrationType", 4),
        ExpectedTarget(422, "Surface", "class", "epsimple-shape-core-422-996a596c", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface", 5),
        ExpectedTarget(423, "Surface.ID", "function", "epsimple-shape-core-423-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.Id", 5),
        ExpectedTarget(424, "Surface.__deepcopy__", "function", "epsimple-shape-core-424-0d951ae6", "exception", "immutable-native-surface-explicit-reconstruction-0d951ae6", "Dragons.SimpleDragon.Surface constructor", 6),
        ExpectedTarget(426, "Surface.__init__", "function", "epsimple-shape-core-426-bd742aa0", "exception", "deterministic-native-id-and-immutable-constructor-bd742aa0", "Dragons.SimpleDragon.Surface constructor", 5),
        ExpectedTarget(429, "Surface.adjacent_zone", "function", "epsimple-shape-core-429-cf314ac6", "exception", "native-adjacent-zone-id-rather-than-object-reference-cf314ac6", "Dragons.SimpleDragon.Surface.AdjacentZoneId", 5),
        ExpectedTarget(430, "Surface.area", "function", "epsimple-shape-core-430-aa93b96b", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.Area", 5),
        ExpectedTarget(431, "Surface.azimuth", "function", "epsimple-shape-core-431-98e03520", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.Azimuth", 5),
        ExpectedTarget(432, "Surface.boundary", "function", "epsimple-shape-core-432-3680772f", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.BoundaryCondition", 5),
        ExpectedTarget(433, "Surface.construction", "function", "epsimple-shape-core-433-9aed8e71", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.Construction", 5),
        ExpectedTarget(434, "Surface.flip", "function", "epsimple-shape-core-434-8e01b8fa", "exception", "pure-deterministic-native-flip-without-inplace-mutation-8e01b8fa", "Dragons.SimpleDragon.Surface.Flip()", 6),
        ExpectedTarget(435, "Surface.from_json", "function", "epsimple-shape-core-435-3da5f695", "equivalent", "not_applicable", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 7),
        ExpectedTarget(436, "Surface.get_unique_fenestration_constructions", "function", "epsimple-shape-core-436-72d9807c", "exception", "model-catalog-native-aggregation-72d9807c", "Dragons.SimpleDragon.GreenRetrofitModel.FenestrationConstructions", 8),
        ExpectedTarget(437, "Surface.num_doors", "function", "epsimple-shape-core-437-42d0195c", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.DoorCount", 8),
        ExpectedTarget(438, "Surface.num_windows", "function", "epsimple-shape-core-438-4ec64b53", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.WindowCount", 8),
        ExpectedTarget(439, "Surface.reflectance", "function", "epsimple-shape-core-439-3a69bea0", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.CoolRoofReflectance", 5),
        ExpectedTarget(440, "Surface.to_dragon", "function", "epsimple-shape-core-440-26abf64e", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 9),
        ExpectedTarget(441, "Surface.type", "function", "epsimple-shape-core-441-5afcce2a", "equivalent", "not_applicable", "Dragons.SimpleDragon.Surface.Type", 5),
        ExpectedTarget(442, "Window", "class", "epsimple-shape-core-442-00f305af", "exception", "unified-immutable-fenestration-with-window-discriminator-00f305af", "Dragons.SimpleDragon.Fenestration with Dragons.SimpleDragon.FenestrationType", 10),
        ExpectedTarget(443, "Window.__init__", "function", "epsimple-shape-core-443-e8fad25a", "exception", "unified-native-fenestration-constructor-e8fad25a", "Dragons.SimpleDragon.Fenestration constructor", 10),
        ExpectedTarget(444, "Window.blind", "function", "epsimple-shape-core-444-92ce583d", "equivalent", "not_applicable", "Dragons.SimpleDragon.Fenestration.Blind", 10),
        ExpectedTarget(445, "Window.construction", "function", "epsimple-shape-core-445-4f40b518", "equivalent", "not_applicable", "Dragons.SimpleDragon.Fenestration.Construction", 10),
        ExpectedTarget(446, "Window.from_json", "function", "epsimple-shape-core-446-93259bed", "equivalent", "not_applicable", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 11),
        ExpectedTarget(447, "Window.to_dragon", "function", "epsimple-shape-core-447-f032bad2", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 11),
        ExpectedTarget(448, "Zone", "class", "epsimple-shape-core-448-dda48f66", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone", 12),
        ExpectedTarget(449, "Zone.ID", "function", "epsimple-shape-core-449-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone.Id", 12),
        ExpectedTarget(451, "Zone.__init__", "function", "epsimple-shape-core-451-a5f3cee1", "exception", "deterministic-native-id-and-immutable-zone-constructor-a5f3cee1", "Dragons.SimpleDragon.Zone constructor", 12),
        ExpectedTarget(452, "Zone.area", "function", "epsimple-shape-core-452-51ef4a1e", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone.Area", 13),
        ExpectedTarget(453, "Zone.cooling_supply_systems", "function", "epsimple-shape-core-453-e0f58a2e", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone.CoolingSupplySystems", 13),
        ExpectedTarget(454, "Zone.from_json", "function", "epsimple-shape-core-454-1254d46e", "equivalent", "not_applicable", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)", 14),
        ExpectedTarget(455, "Zone.get_unique_fenestration_constructions", "function", "epsimple-shape-core-455-d8077110", "exception", "model-level-native-fenestration-catalog-d8077110", "Dragons.SimpleDragon.GreenRetrofitModel.FenestrationConstructions", 15),
        ExpectedTarget(456, "Zone.get_unique_materials", "function", "epsimple-shape-core-456-ecb20cb3", "exception", "model-level-native-material-catalog-ecb20cb3", "Dragons.SimpleDragon.GreenRetrofitModel.Materials", 15),
        ExpectedTarget(457, "Zone.get_unique_surface_constructions", "function", "epsimple-shape-core-457-486d73d3", "exception", "model-level-native-surface-catalog-486d73d3", "Dragons.SimpleDragon.GreenRetrofitModel.SurfaceConstructions", 15),
        ExpectedTarget(458, "Zone.heating_supply_systems", "function", "epsimple-shape-core-458-c68b3d65", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone.HeatingSupplySystems", 13),
        ExpectedTarget(459, "Zone.height", "function", "epsimple-shape-core-459-349a48c8", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone.Height", 12),
        ExpectedTarget(460, "Zone.infiltration", "function", "epsimple-shape-core-460-3fffc5a8", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone.Infiltration", 13),
        ExpectedTarget(461, "Zone.supply_systems", "function", "epsimple-shape-core-461-3eaf6c25", "equivalent", "not_applicable", "Dragons.SimpleDragon.Zone.SupplySystems", 12),
        ExpectedTarget(462, "Zone.to_dragon", "function", "epsimple-shape-core-462-da336048", "exception", "native-greenretrofit-converter-implements-upstream-missing-operation-da336048", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 16),
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
        "sha256:b6626755ad5bf8192fb3c35d91df91b1b388f3543617479d983cff6c15143dfe",
        "sha256:f101c843b58ec010323963b397f7dc1333d2dec9baba0f53925b067a9d837445",
        "sha256:0f8890a98cf3a48e40e891ab542e45d001e3c106d19857cca9b80c300d3f7016",
        "sha256:ad44d1b9bdcf21ecf6ecc7acfcdde849c824a931f315324f8c53b570f7cea55d",
        "sha256:4ceb9cd7a6ebf7bfd3b53ad089ee7eb23cb97a1898f85fad36dcce3916e4d681",
        "sha256:e18670dc2737585830b0882fffd4167e2d5983a2fe1d35096b2edc1a8b6f158d",
        "sha256:b166f29245d3337b4e180cafb3a3a1bf891aba76ffa8545dd0c08c1e9f13d870",
        "sha256:457d49439f2264630e0feb59bb71688c0922208e68e9607c5e60a85e9c2e9721",
        "sha256:7ea0c23edb672cc744f9c8c4d1db2bde16da65d0fb9e1cd9237b0b1a54561705",
        "sha256:c4255c628be66fc8238ecfbeeb29f6f0d7b30d5b6f5ff871aa4e637f9c49257a",
        "sha256:02863aea33cdf7d833a1af27ebed414908b53b7e7ff78942f96d64209c71b69b",
        "sha256:184b27dc7e85bf6cbc1e0f1730841cb41afa657eedef3fc71c7bfe438844fcf0",
        "sha256:8a15f7068d35d547237ef858a8bc18b2c8101dbd79e727cd41535d3860c16591",
        "sha256:60f1b0b472f56c5a392da7a542c5cff17a74cee0f985840ccdc904d544f8ff0a",
        "sha256:4bbd87dcac0c5d5633e2a53e0e2d55709011e0f67da506617d46df2ee96bc67c",
        "sha256:c9252c8a98974f2e1b3c5a9dd597070240b9bc8fd96dd902fa966cfbf62d67f0",
        "sha256:483550a46622a28134a658616b656a1045fc197d5207a05619f6ed6c3e2f2818",
        "sha256:78b167ec615dbf1cad32ba08626e92ebbeda4f27ed8bd05c2d78943156c277ee",
        "sha256:66c1560499ccecfba30c4f5db054bb946c008cd6943a2404f93ffe18f1768522",
        "sha256:9774a9b7f4ff819f2d7275d2f1fb6f3ac559fc65f22e2e7093776ed966c0e8ae",
        "sha256:5d4e6e63da17b517cff7dbbc9a94f051220741e8ca36f66154a604e56e6cd41d",
        "sha256:d632b59b9b07794a46281056f3200dba350fa6b31d1bdb8fae015135350c9431",
        "sha256:85a1d9971ad8d0bd20c26594526d5e9d9c9d08dad7910e2bad4312e3a7ece2b7",
        "sha256:a6ca5a26b49683b62fca008b4d7775631ecc87453ffb1fbe928684b167c114d1",
        "sha256:adcb3b15f383306eb64a9cdabc46daf2604255038a201e5d5c0677ce8d966d58",
        "sha256:4071890db5b30471dfdf45cd899fda204e7e7a4640d8dfefef33f980d1c265ea",
        "sha256:2ed8b9b31a39ff08920f5cb156d9eb38f8f0e258dc59929e97a6d3e47c5a58c8",
        "sha256:7ebe33e97355ecc5d83045f4c0fa62db79e52ec07aa525cdf4e17480361c8caf",
        "sha256:02eb883013eeaf5af8718ec1b1a841d48add7eb082acf9c1bade30bd5b20a38d",
        "sha256:32d28ac5a1c4b3f4e7a8ab45e5398942dd64d29e41bf6cf4c2785869fce7ca41",
        "sha256:186c2847128a15c275ee0ac10dc4add3fab7a9611bce956a8ab649c519b8b149",
        "sha256:f268d7a02653179f69494f409d7a4287a81b50524291acf6fe07115266da782c",
        "sha256:d0f82f448fa746f40110567218a2b2efcc76bd4147d229ad15c4579dc0c73acd",
        "sha256:e403a7fd3cffc452d4e88a102c7958889ccbd1d603c5ef6e07d09ab200e8f2b8",
        "sha256:c3b0694cac1ebfb34e8d95a5c5814ae5203c7895212f46bbbb502d99c7752a05",
        "sha256:adc1b9a9752e19207265d44938937b3f32642026caa284f06dde632fc7f600f0",
        "sha256:c87a493be5c50fa22fe19f135872c4252c3d568b0a63600c1660e1158734ca1c",
        "sha256:bac12f4e47f8c2ca1414caa3ac948b46c81ab9383f3145b493fe1023e32a0609",
        "sha256:544312cceab4d97057913fa54e0e9ecf415f4852ab66b650f863fc7098c4a923",
        "sha256:641db43f9fd16f7d25dd730dbf1c2b1a51657603d8ee54bcde0968c26c559a99",
        "sha256:90b581f1807d5ea04af33bd788a9d10cc02652d38db061cef0247cf7956ad917",
        "sha256:1ebc5c8f03a758872880d51f072b7b2418e3b220148e5eeb3983c9c17a74848a",
        "sha256:90b5665d3cab08d4e54525ac5e6f8f5405e905465fb6554aad7a74d7c713aa18",
        "sha256:27ef118e25014f0d176bdaa40c80bbd030acc52a6911e9e7bf14095090b932ae",
        "sha256:9c6dfe60aac500556f8018bbadb02ef5991132cad1622f9f35c522d619659d2d",
        "sha256:754682478bf1bbbdeccd67d04ea0faa9367df1b2ad8c2915f7cf9d68820f0360",
        "sha256:b191a0f79f127b2623b2390b980b7d68aac0bd7b2bcbeb59f106846d036bb6ea",
        "sha256:ebf5ffdd06e2d5c8c6efe7c8f174e8459a1ce2022cd4eeb07e0c328a875dbe62",
        "sha256:77c3cc7296a5a4787fc9aa3d200bfdcd2e8bd6533d7a593e5a0e59bbd4701ccf",
        "sha256:2fd873b5bf56c3e3ae401c62ec3a3a4b31f067513de1c3764b8b59fe72e4d629",
        "sha256:9e3d464cd39946d5ffc78342917001efc6ba5f6b8ef6f955d5d9bdc5cd25e2a0",
        "sha256:a85d9f1573da9809952b68cc7b467578e065fe2625832be6a6731347c21056e0",
        "sha256:289be1240c196a9173d636652555a89f4bb504017573cc6ee8e06f80e5f420fc",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:4befea8f364fc7416bd9969cc1085fdb39a47dce0c90f07fd252f1fcfd836719", // epsimple-shape-core-405-6008dd91
        "sha256:38c8b674f84a7aa2bafa3b7badc2cf21b587825db215f61fda68fa3568f7264c", // epsimple-shape-core-406-bb03051d
        "sha256:0f582e7f2c91f950de120a6e36f2cb3598a6c9765a0f65cd741a03fd11357c3b", // epsimple-shape-core-407-09c92f4a
        "sha256:1e120231334390120a464f64e81cb833008c0f26946c90e282945470cdb192fa", // epsimple-shape-core-408-f40e4929
        "sha256:c7131ee574c3f05d1ba9b09501fa56e5f7ebc5e7d9ebb2f4fd6113ac35ba4f72", // epsimple-shape-core-409-8c468e24
        "sha256:79aba35b29462ea91a5780e2d4fb41c415ea4d72e6220b077593982d4bd210ae", // epsimple-shape-core-410-2ca0072c
        "sha256:6acc8bce2c539d2e39f31d85cd5229dc5c95fd7a63120cbd40662a3959ded952", // epsimple-shape-core-411-26b0f9bb
        "sha256:7308ee19143e36433992f59740876f960e139e1f88fab8dfbbc40197f28002f5", // epsimple-shape-core-412-eb81bd06
        "sha256:f43adb66a9927684bd884ff20754d25751581c587018fc2210584220d4583bf5", // epsimple-shape-core-413-43d44ea1
        "sha256:469b690e993b128c925e430db63c0279a51ae264fc4949b26d3130129bca9598", // epsimple-shape-core-414-246156d9
        "sha256:a282dbe421814014e9e7528045b7ddea981ba1fb4b2aaf6f891c8155bae0aeb5", // epsimple-shape-core-415-a0dbc411
        "sha256:f5a10cbc70b3039163376d7b30d52ee57a417bee1756269725efd66f34e0874d", // epsimple-shape-core-417-1b22b2f1
        "sha256:8b800bd8d08a9f0ac5c5fb0d82854caf18c43059d398f1b237311cd3cf0f1019", // epsimple-shape-core-418-0b0cbf2f
        "sha256:03126ca71a3dda402acaf583b824d9bcbfae69d9e7190d2c1c01af9d66c10dcb", // epsimple-shape-core-419-2e553f68
        "sha256:2fcd49c33e82f404db7fb2ab3ab2ff6ebe933996e3a1373a25e96ac023a4f24e", // epsimple-shape-core-420-ede823e2
        "sha256:0c16a1950b46346afe5ab2f14773f5f4c97ff183772c8c9bfa1e633c6e7d39ec", // epsimple-shape-core-421-1981a404
        "sha256:1d817a16b2db7ef924d1eb7db4d4be11e3eb880bb674472197399ec6c7182740", // epsimple-shape-core-422-996a596c
        "sha256:329d1069e3af96118cbdbf653a4d5c49b2a931376d18ea99086dc442ff6214ae", // epsimple-shape-core-423-246156d9
        "sha256:4dbd0e4e9acf3f36b049e1482fa0d8f8a6cdf1088297f782213a7d8473a323df", // epsimple-shape-core-424-0d951ae6
        "sha256:a35e3f9aab6aba086459a4d6aea2131ae1cdf87225b4b94eef6ae3178b8b34ac", // epsimple-shape-core-426-bd742aa0
        "sha256:d47d6b69732c4242afdb809bd2c21a7fffc6909ca5052920cb78f4550893f578", // epsimple-shape-core-429-cf314ac6
        "sha256:78346e3fcccca74880bfa020aa9a4c66e45db64d3de509a79f35a45bf289f262", // epsimple-shape-core-430-aa93b96b
        "sha256:eedd5f5ae6d84f2e3abcad998eea6c14b853641a68a9ab60fae8945275119c02", // epsimple-shape-core-431-98e03520
        "sha256:e839519d67a1a65b135a31c1dd56fe4225a4744f2b78b42c1d0d706f7f76c8d5", // epsimple-shape-core-432-3680772f
        "sha256:162bff2975dd219b7c5c405f187c7cbf40e73e07b973cdaf1d3b17dc947b9361", // epsimple-shape-core-433-9aed8e71
        "sha256:acbb00491063c56b8c327a696baa63446d8b273c56d6a2933673f105667579bc", // epsimple-shape-core-434-8e01b8fa
        "sha256:70fd12403a15f8994509bd2af4438b0c6e25aa7f9a3003cc34da914ae5c2bd78", // epsimple-shape-core-435-3da5f695
        "sha256:f7de7bae464c829663ae9b1bf3515aa6bcbe25389a3a293664a360298b065f60", // epsimple-shape-core-436-72d9807c
        "sha256:f20386bb80cc5c461ac92190d9458b34835dcfdc9395126e0cd694f81bb71dac", // epsimple-shape-core-437-42d0195c
        "sha256:05664eb961ab45b3450b86331a3eecb01cce1cc3a0c3af95c42746a83e944d59", // epsimple-shape-core-438-4ec64b53
        "sha256:57f9a9d24731b26e88742e61bce18122b01b95425d2230423685fa878660fda1", // epsimple-shape-core-439-3a69bea0
        "sha256:6a5c377ad97dd9fd4d224562816fd39effdededea46636941248bbf4ae200144", // epsimple-shape-core-440-26abf64e
        "sha256:c6483f417211612a6e932abc19013975ba59f3d73a1c04ac03850e91bfa8130b", // epsimple-shape-core-441-5afcce2a
        "sha256:b4a898dfe579ba34cfa3bd5d95d9df35763fed79a46044dfc13fa623e3835c88", // epsimple-shape-core-442-00f305af
        "sha256:caf633390df9897768d32b1ac4dd85e0a66948becbaa0ab294b8b546bf9d655a", // epsimple-shape-core-443-e8fad25a
        "sha256:ecc1f94f68cbdc5555445339688789c2ae8746f7583492fc86c39ff9392d7742", // epsimple-shape-core-444-92ce583d
        "sha256:33971f03007785675dcfcdaa7f1ce2af003fc05d55399098d087ea217b6298a7", // epsimple-shape-core-445-4f40b518
        "sha256:71ade8b8bb05d105bea7560fb5102f6eb6b2543a79cf38fc68c4d0133dee6176", // epsimple-shape-core-446-93259bed
        "sha256:fe607bbfe83806e013b2ab5642871c907049e72d16f47216b72424be704b01bc", // epsimple-shape-core-447-f032bad2
        "sha256:b8a641211088506c8d9f6e8da36d778748a5cfd970f8976290e721da030e7ff1", // epsimple-shape-core-448-dda48f66
        "sha256:a8129930fb76f09db7eb947bbbd8f722981e92fc87af3832431feeba7e303a0d", // epsimple-shape-core-449-246156d9
        "sha256:6fb07ce2ee7735d47aba1bbcc1a393d331a8ddcb2a53ab7a672b2c478974f58b", // epsimple-shape-core-451-a5f3cee1
        "sha256:05e5e4b2e9ed55feaca565026d76f55a3d4ba1721c101b6a184b12fb85dc1cd8", // epsimple-shape-core-452-51ef4a1e
        "sha256:d4791261ef6be93e607adaa8c2dec48420ebbe38bee4fad708e301802f34c82d", // epsimple-shape-core-453-e0f58a2e
        "sha256:794ed5eb73fea82ecc3ee050948258d40fe09570b708ff3393bf9b7ffd53353a", // epsimple-shape-core-454-1254d46e
        "sha256:0d82ed2ef79b6727d9c991b45cdbd10ce7916ac8c16f8daf5fd3c376e8fea70e", // epsimple-shape-core-455-d8077110
        "sha256:95238b5f1817c9480a0082c7a93e13f8b72dc85f6d8889cb01fc78e5319e8080", // epsimple-shape-core-456-ecb20cb3
        "sha256:001a9fe2928f104ed0483448ebf11785149c7532689573332a636bcbbdbf37f1", // epsimple-shape-core-457-486d73d3
        "sha256:7a5858fafd5ead1c15d54cc0c67b8805ba2ae410b6480a493813523d62dd54b1", // epsimple-shape-core-458-c68b3d65
        "sha256:af685ff0d3644aabfe392ada3e03c427c07db28575b605c9ee1da0271eb427fa", // epsimple-shape-core-459-349a48c8
        "sha256:99bbd51a0d911d56daa50d11c34ceb03de6e57d12db10a4c0e469825854e22ec", // epsimple-shape-core-460-3fffc5a8
        "sha256:b665ca10d9afa0b114bb9d4eb3091b9dbe786f3cdc13a3bb66daadea9230fc3c", // epsimple-shape-core-461-3eaf6c25
        "sha256:815d1479e918862a589636042605f0cf068d5200685bc17988b1a22652218717", // epsimple-shape-core-462-da336048
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
            21_108,
            "sha256:555a1df41e5369dbbc44b0729a48673610a86951a215c8e2aa00cfa4fce156f1");
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
