using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class ZoneCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-shape-zone-core-oracle.json";
    private const int FixtureBytes = 91_202;
    private const string FixtureSha256 =
        "sha256:63d62d596d37c2e33adcbaf025f37ccda36d8a4291d96b3201d427d8caed59b3";
    private const string FixtureSchema =
        "dragons.python-reference.dragon-shape-zone-core.v1";
    private const string CasesSha256 =
        "sha256:b42a68ae6532fa179348796c81982a98579c9789b66a453fd5c1eae22d8b964f";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_shape_zone_core_oracle.py";
    private const int GeneratorBytes = 67_980;
    private const string GeneratorSha256 =
        "sha256:ce86db526f27158c7e81b40e5e6007c090008bfca5612775a01f8df141936666";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_shape_zone_core_oracle.py";
    private const int ValidatorBytes = 22_690;
    private const string ValidatorSha256 =
        "sha256:126c3601b36b3b4f3e1e22c92402b55c510fb00f9cd5da3698285725f9491201";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/shape.py";
    private const int UpstreamBytes = 27_438;
    private const string UpstreamSourceSha256 =
        "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c";
    private const string UpstreamAstSha256 =
        "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.ZoneCoreOracleParityTests.MatchesPinnedZoneCoreThroughTypedNativeRoutes";

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Internal/DomainGuard.cs", 2_416,
            "sha256:a8d28c985fe67376ca08015ed8e6d28600c98366c33a4a41dfd4abf377f57d8c"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/PlanarPolygon.cs", 16_524,
            "sha256:73a1dd052fb12ed0802a6236d21484e2b680cbe3f0f4005ade6a61995111c653"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/SurfaceBoundary.cs", 1_909,
            "sha256:c0ba4cf5a93eb2678aee2c698320121f5bfbd68f7febb3dc901fe700da1499d9"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Surface.cs", 7_731,
            "sha256:545dc79dd89e84acf6d714e79da7b2cda059dfcaa3b4f74d291ad572ebd51264"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Zone.cs", 6_698,
            "sha256:37bd33ef649a03988255edd9f95bbb0f1ffb7c63cbf8fd1ddb784ebb071b8920"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Profile/Profile.cs", 4_079,
            "sha256:99c3e0557ba737aa74cfb0f15faf0730d9f7215a6b66f7f6b6b2044cf4013c72"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_582,
            "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SourceSystems.cs", 18_027,
            "sha256:8d302f00514af53816cec9e5ba6b80a8214921b354d86bbbc4d581ec972e026e"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SupplySystems.cs", 18_267,
            "sha256:4de030455a8a1b8db0ca4eca7745c6501930c984f9d1e156e17cb0b752d845cf"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs", 13_182,
            "sha256:50aa8a362214d34bba37dcf51ef3c0cce89d54895110a0da786c11d8fe233495"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs", 22_015,
            "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_764,
            "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905"),
    };

    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };

    private static readonly string[] ResolvedTargetsNotRetargeted =
    {
        "Zone.is_conditioned",
        "Zone.to_idf_hvac_default_object",
        "Zone.to_idf_load_object",
        "Zone.to_idf_object",
    };

    private static readonly string[] ContextSymbolsNotTargeted =
    {
        "ElectricRadiator",
        "ElectricRadiator.__init__",
        "ElectricRadiator.heatable",
        "SupplyGroup",
        "SupplyGroup.__init__",
        "SupplySystem",
        "SurfaceType.FLOOR",
    };

    private static readonly SourceReceiptPin[] ContextReceiptPins =
    {
        SourceReceipt("ElectricRadiator", 707, "class", "src/idragon/dragon/hvac.py",
            "sha256:6e4ce6d4489fd995f5cf5ebfd4ca8a96db68c7b5d0bb271fbf37a9ea01dbdf33",
            "sha256:1c9170eb76b09c7feea649df317e2a08e6baf0c314de14d3f28859af519d9b05",
            "sha256:8b8d4de15bc3ac3f97742e4883e96cfbd188a7b4195ba0bf2dd76d29fec1ec92"),
        SourceReceipt("ElectricRadiator.__init__", 708, "function", "src/idragon/dragon/hvac.py",
            "sha256:07f43ff08d4fb608d661c8399fbf11db4f1d8c7c504e81a6e8b8e9e223772ba5",
            "sha256:d23ce9ebf7e7a2bba349ed7000094734a2eecfb480fb83c721e56a5e6ef0936b",
            "sha256:f34ba716b0ec8995d0c671dd8ee8464d437871508ce667580c2f25f606b059f9"),
        SourceReceipt("ElectricRadiator.heatable", 710, "function", "src/idragon/dragon/hvac.py",
            "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db",
            "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3",
            "sha256:a200989331792d789cc947c1b615c0eb8c31e552b2dbe4f805b7ad72e3f082d4"),
        SourceReceipt("SupplyGroup", 789, "class", "src/idragon/dragon/hvac.py",
            "sha256:f22147d1bab44415fda473980799cb75dc4ce6c57693b5d9ec0a5faaf131fe69",
            "sha256:705b5c841450a5e51e48e95e3027de5a03632aa70888f7176d77d0cd48087459",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
        SourceReceipt("SupplyGroup.__init__", 790, "function", "src/idragon/dragon/hvac.py",
            "sha256:02b3c43aa048fd31a3ffc31fea96f5086a599d3245847e217dc0c99a9cf5fddd",
            "sha256:f01960cc5a0c00e094cf2eb094922d734343c92c8ec849977ea8b86337805907",
            "sha256:643ca4afc57e9a0b22eee5df0a2cd7b90d9d579cf16bb20fd6d6a9e40b5bc57c"),
        SourceReceipt("SupplySystem", 797, "class", "src/idragon/dragon/hvac.py",
            "sha256:13ed08986e2e8b8e9b6a3f9b9a1f387ad8075a99a5f79e6df18b2fd0280cfdc1",
            "sha256:e69d386ef2ddabed5236bc05985ae71c826e6a0e7cb4b9b9a35ecc71a6bfb9ef",
            "sha256:ae6bdfe5569d83c09285f8097f3d7783d8e4911c3c43d0cac4dd9eb2ea1ff51e"),
        SourceReceipt("SurfaceType.FLOOR", 1056, "constant", UpstreamPath,
            "sha256:c8c4f240e476a6db7cc85ca0bfcaea675233b72f28019edd4308f11cb689e01b",
            "sha256:909756f308b102264b0588f914f69542d69da96738233ca4fbb92a838d087bea",
            "sha256:37194ca6121ae832d5c991164c74dd662b39ba10da745ebc418aef2d1a834e5a"),
    };

    private static readonly SourceReceiptPin[] ResolvedReceiptPins =
    {
        SourceReceipt("Zone.is_conditioned", 1090, "function", UpstreamPath,
            "sha256:6fe80cb193a6716b68c1033c5c52bd29f422ffb9efbdac8475a7f4b4ddc46370",
            "sha256:2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb",
            "sha256:48a103a5bbb0b2a65f357d705eb38137269140e236bf98c2d56d7dd77474d9f3"),
        SourceReceipt("Zone.to_idf_hvac_default_object", 1092, "function", UpstreamPath,
            "sha256:ff678ec281fe0726c46fd2145ebfb7fe22b56c5772bf1423d83c4877c0287cd9",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:9a121aaad9df4bfa6222f747985a1b07749f518b3501154743ef5c32d307940b"),
        SourceReceipt("Zone.to_idf_load_object", 1093, "function", UpstreamPath,
            "sha256:d19165f0aa97a1768174def3da3a46c9c11f29567c558ae844d4cac546452f99",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:17d9c0579f4763783672c981efb7fa0d7c979af8ebfe008b70499f81273e5a78"),
        SourceReceipt("Zone.to_idf_object", 1094, "function", UpstreamPath,
            "sha256:479f4d74a625e35e97559f208b41c4bde2f00a519b8e6b840718d78fdfd2e096",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:1964153231690634955bd8ae5c39468cd1ecab4f5c2acbff9ded2cb37978369a"),
    };

    private static readonly string[] UnresolvedBoundaries =
    {
        "arbitrary-surface-iterators-that-raise-or-mutate-during-iteration",
        "foreign-area-addition-protocols-beyond-the-bounded-observed-inputs",
        "nonfinite-floor-area-values-not-observed",
        "huge-or-mixed-numeric-floor-area-overflow-and-coercion-not-observed",
        "missing-or-raising-area-attributes-not-observed",
        "zone-name-objects-with-custom-string-conversion-side-effects-or-errors",
        "virtual-or-dynamically-registered-SupplySystem-subclasses-and-descriptor-tampering",
        "concurrent-mutation-during-floor-projection-or-supply-assignment",
    };

    private static readonly CaseBinding[] Cases =
    {
        Case("Z01", "z01-representative-and-permissive-construction", "container",
            "sha256:8525a95f683d23f3c8b9fc1098f54e21545978becf0b1495934994c064e156b6",
            "sha256:48ca4f2a95644574349289883d7b2053fd89630e987c0c7f413be8ce72a35714",
            new[] { "permissive-mutable-python-zone-container", "unchecked-aliased-python-zone-construction" },
            new[] { "Zone", "Zone.__init__" }, new[] { "Zone.supply" }),
        Case("Z02", "z02-empty-floor-projection", "floor",
            "sha256:1e5bbaa6a5544d3ed2095186464a0a4558e3ac8823cb1aafb5f4f4797091408d",
            "sha256:53fae4c45819d22aa9cbed67bf233a053d90b7cb9671456bd6d8f07eb4c02151",
            new[] { "python-floor-identity-filter-and-dynamic-sum", "python-floor-identity-filter-and-fresh-list" },
            new[] { "Zone.floor_surface", "Zone.floor_area" }, new[] { "Zone", "Zone.__init__", "Zone.supply" }),
        Case("Z03", "z03-mixed-multiple-surface-floor-projection", "floor",
            "sha256:8753662d5118d6d0624e17fd8595c2658ab7dc34b13e52ff2ec63bf911eeebb6",
            "sha256:7db7329767cd20752668cf27a0eb29b54b960387c8e08d628a2165c0f4d00fed",
            new[] { "python-floor-identity-filter-and-dynamic-sum", "python-floor-identity-filter-and-fresh-list" },
            new[] { "Zone.floor_surface", "Zone.floor_area" },
            new[] { "Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR" }),
        Case("Z04", "z04-no-floor-multiple-surface-projection", "floor",
            "sha256:c0d59d4de8601928540f97f67ab54e0379a734810adbfd7407028f11ff88a35f",
            "sha256:7c9482907498643f83792188b370eaab0a1b9c09ffa65e9d204abd04a634a7be",
            new[] { "python-floor-identity-filter-and-dynamic-sum", "python-floor-identity-filter-and-fresh-list" },
            new[] { "Zone.floor_surface", "Zone.floor_area" },
            new[] { "Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR" }),
        Case("Z05", "z05-multiple-floor-dynamic-sum", "floor",
            "sha256:3e5e7c0fb8dec50819621336aac8eceb679899171a4a6cf7a51ce07c4fcfb937",
            "sha256:b8ceb11920c5e347c03bf1b3ae7abc3387503b2267802ad168b30877c31c393b",
            new[] { "python-floor-identity-filter-and-dynamic-sum", "python-floor-identity-filter-and-fresh-list" },
            new[] { "Zone.floor_surface", "Zone.floor_area" },
            new[] { "Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR" }),
        Case("Z06", "z06-surface-alias-mutation-and-reassignment", "floor",
            "sha256:8ded2c24ba9f9347474fdf01e01ee4db03de239a1597a93426c4948d2f9028d5",
            "sha256:b68647cc92f046009483d76756e1e8a14fb4db3caf8e28b6c0507694db07a039",
            new[] { "python-floor-identity-filter-and-dynamic-sum", "python-floor-identity-filter-and-fresh-list", "unchecked-aliased-python-zone-construction" },
            new[] { "Zone.__init__", "Zone.floor_surface", "Zone.floor_area" },
            new[] { "Zone", "Zone.supply", "SurfaceType.FLOOR" }),
        Case("Z07", "z07-name-formatting-and-name-mutation", "naming",
            "sha256:d946b3b64c6294fb7001400fcde629a62d78235cc616db527c2437ddf52c85d4",
            "sha256:bd383c4ed786db75b4c064874af0f079865e5a53a3114880343fa51bf0f37268",
            new[] { "mutable-unvalidated-python-zone-name-formatting" },
            new[] { "Zone.idf_airexhaustnodelistname", "Zone.idf_airinletnodelistname", "Zone.idf_equipmentlistname" },
            new[] { "Zone", "Zone.__init__", "Zone.supply" }),
        Case("Z08", "z08-supply-none-system-group-coercion", "supply",
            "sha256:9cf61887a4998f7ed1f278e59cd52b8fe6ad1786e3b94bd4b050f37ed6e792f0",
            "sha256:da56883067aefbccf8d509ed11472acb7ea85c1e04c4d38eaf2cd93aaf01c811",
            new[] { "embedded-python-zone-supply-coercion-and-mutation" },
            new[] { "Zone.supply" },
            new[] { "Zone", "Zone.__init__", "ElectricRadiator", "ElectricRadiator.__init__", "ElectricRadiator.heatable", "SupplyGroup", "SupplyGroup.__init__", "SupplySystem" }),
        Case("Z09", "z09-invalid-supply-error-and-partial-init", "supply",
            "sha256:0cbaeae8b5db19a0505a74099dd634ba7227ecdbf167df35cf074eb3dfbae85b",
            "sha256:53ed4cf50a90e1f9da5bb102def1dc020c7fa7fa50e574efd6fb12b2a151af9c",
            new[] { "embedded-python-zone-supply-coercion-and-mutation", "unchecked-aliased-python-zone-construction" },
            new[] { "Zone.__init__", "Zone.supply" },
            new[] { "Zone", "ElectricRadiator", "ElectricRadiator.__init__", "ElectricRadiator.heatable", "SupplyGroup", "SupplyGroup.__init__", "SupplySystem" }),
        Case("Z10", "z10-floor-projection-error-timing", "floor",
            "sha256:73b4f618cd44aa712ea3d1ea472ee2510d07a3614ae3f87bd98cc2598a024df6",
            "sha256:ee6b6700ce0844bca9878ee1f95a36d68e656355e9882cee89cd7718e366816a",
            new[] { "python-floor-identity-filter-and-dynamic-sum", "python-floor-identity-filter-and-fresh-list" },
            new[] { "Zone.floor_surface", "Zone.floor_area" },
            new[] { "Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR" }),
    };

    private static readonly TargetBinding[] Targets =
    {
        Target("Zone", 1083, "class",
            "sha256:4830290e50ed3c4b50717f26a9b0503763c09b5b87f041b2f03d5ab3ba035d30",
            "sha256:16fdf50a01e06bd39fd30bae2eee24f8902679a1db662214f3ba00345680a29e",
            "sha256:82f464feaab2b692325befc8de0fdf44f28698a041430c75dce9acb727a1a318",
            "dragon-shape-zone-core-1083-4830290e", "permissive-mutable-python-zone-container", new[] { 0 }),
        Target("Zone.__init__", 1084, "function",
            "sha256:fad03092d1390e4a9f0c7f4184a757c7abc55fb85b737f2d0c9be217b7682987",
            "sha256:60d0cabb1fd39adf4a0b915e1aa6dd59bd9861678ae2325ed69e38ee416a2b5e",
            "sha256:990d28f257710186eca517844cd89463291fbe5be37185a4e29df367f56be502",
            "dragon-shape-zone-core-1084-fad03092", "unchecked-aliased-python-zone-construction", new[] { 0, 5, 8 }),
        Target("Zone.floor_area", 1085, "function",
            "sha256:21fe276dd163e81d4c0de2f978cb6dca63e807a7fe798d9fdaa5f8316ec8fac2",
            "sha256:f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea",
            "sha256:c6cbd898d7acd8c43cc2661cbaef1e9e8a8988ce15aeeaef4a41f6b82c2a4213",
            "dragon-shape-zone-core-1085-21fe276d", "python-floor-identity-filter-and-dynamic-sum", new[] { 1, 2, 3, 4, 5, 9 }),
        Target("Zone.floor_surface", 1086, "function",
            "sha256:53382328123e6a81052a598a89a8e41482a1aac0a3d470e7bd66c63d6d8c22b7",
            "sha256:175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea",
            "sha256:6a515e3593890cc2dda844d3daceb975cec58874cdddaa46419fbdde77a86c48",
            "dragon-shape-zone-core-1086-53382328", "python-floor-identity-filter-and-fresh-list", new[] { 1, 2, 3, 4, 5, 9 }),
        Target("Zone.idf_airexhaustnodelistname", 1087, "function",
            "sha256:48c6fddbf04adf507eabbaa023c0a3a711bb01812e6068becd27f2abdff9c1b7",
            "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb",
            "sha256:655be145758f5c64d4cf876a2e60ef3729bfa412a6e50755f03dfec1eaa855d8",
            "dragon-shape-zone-core-1087-48c6fddb", "mutable-unvalidated-python-zone-name-formatting", new[] { 6 }),
        Target("Zone.idf_airinletnodelistname", 1088, "function",
            "sha256:97745304336763af22c9a31a48c4a590d7faa2a936ec220fa0ee144fae1b701e",
            "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb",
            "sha256:4c5c03d5d4247e862823a0d61639ea54d7a6457dc644e32a568b3a9b52c173d4",
            "dragon-shape-zone-core-1088-97745304", "mutable-unvalidated-python-zone-name-formatting", new[] { 6 }),
        Target("Zone.idf_equipmentlistname", 1089, "function",
            "sha256:ad9ccd78f5ddb00df6add098e600b6526268400988d2d2aaf2d0d3bd324b6a13",
            "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb",
            "sha256:a20734c2039bd542918bcc16011a6b1c3eb3cb79412d6d1e2fedea909d428c82",
            "dragon-shape-zone-core-1089-ad9ccd78", "mutable-unvalidated-python-zone-name-formatting", new[] { 6 }),
        Target("Zone.supply", 1091, "function",
            "sha256:1b5900c0e47502e001f7a6055ea868392c85617ee800596666325fa118979b10",
            "sha256:30c559093214655a8e9c1f7a0f57523a48b1fcdba7601332201bca6669ee0a7e",
            "sha256:33112772ec1f8e870b64bcfaf5d178b471689b1dadb017030f23d683503eac1d",
            "dragon-shape-zone-core-1091-1b5900c0", "embedded-python-zone-supply-coercion-and-mutation", new[] { 7, 8 }),
    };

    private static readonly NativePin[] ExpectedNativePins =
    {
        new(13, "sha256:13f0a751803185b1cfabde8b89efe82e05dc6836427ff4492838782de35e2e37"),
        new(8, "sha256:8474f1e2ca664f9934a2ab947d96220aa4fde1272ce2a2ca2f14805b6caade91"),
        new(9, "sha256:6fdfb429a7a734ff900147c7454a61f5d432f71c460cbb72ef106a6c055647dc"),
        new(6, "sha256:741a8f0544cb6bf0a399e9a6dd6baa4afbdedf7ab10ea816e8c5084934903f2b"),
        new(9, "sha256:c406bd5f0b22704f8692ec232d22bf9772b44756f80bdfc0a0b89f1da514acc1"),
        new(15, "sha256:deb8b70c20d4b3db64bcc46e52560dfe7af2d94550ecd985de9f2ca217e4d480"),
        new(10, "sha256:17459c9b182f000d0290a1399a5cfda4cb51835b75aa5b1be9783db4ac7c14d4"),
        new(18, "sha256:18ab46217d71856567d28206a2256f4375f351504efc65239fd3287720a5a7f5"),
        new(10, "sha256:4d3bb3168d0caa138fe030b01e2281b1d609736a6a0ff3751b04eda1f7487627"),
        new(17, "sha256:582a98ead22983ca64c77c9bc3aff51754b531c1d83d5363b20984d1d1365b58"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:f21dfce2129fa58635ab4896c2afa35000f3195f7a9719f7cee51585ffc6267d",
        "sha256:bf2ebcea57e49f729552672b68dc91fc902853f4a08c4683675f2e14ee56d89d",
        "sha256:bcc83acaa516b9f2a9b50c3aab4bb77a9b9385bd9e80903b4b8a44f711bf27ce",
        "sha256:e9c5f87781baeef940dbcf06978893b6ae84d1ed661ba4df8351b9fc807661f0",
        "sha256:cdce413a7abd467883aa51e8af549838f2e9ffba875fa6cd501ca0b7e4aa9270",
        "sha256:678949cffd20083ab71304632aae0b7e945cc9dea7862d574967b585fcc5de57",
        "sha256:ee3e42bf7b7b071d2225df831cce93f7316e7e4f576535c229aa94e9552f4468",
        "sha256:4c673a4f89ae77fea63d764db40ab8edccd65b079babe97e72a0306e2e79c2f7",
    };

    [Fact]
    public void MatchesPinnedZoneCoreThroughTypedNativeRoutes()
    {
        ValidatePinnedArtifactsAndNativeApi();
        using JsonDocument oracle = ReadPinnedOracle();
        JsonElement[] fixtureCases = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(10, observations.Length);
        Assert.Equal(Cases.Select(item => item.Scenario), observations.Select(item => item.Scenario));

        object[] receipts = Targets.Select(target => CreateReceipt(target, observations)).ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "ZONE_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    cases = observations.Select(item => new
                    {
                        item.Scenario,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipts = Targets.Select((item, index) => new
                    {
                        item.Symbol,
                        item.AssertionId,
                        item.AdaptationId,
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
        for (int index = 0; index < Targets.Length; index++)
        {
            JsonElement receipt = JsonSerializer.SerializeToElement(receipts[index]);
            ValidateReceipt(receipt, Targets[index], observations);
            TrustedEvidenceRecorder.Record(
                Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
        }

        Assert.Equal(8, Targets.Length);
        Assert.Equal(8, Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(6, Targets.Select(item => item.AdaptationId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Targets, item => Assert.Equal("exception", item.Classification));
        Assert.Equal(10, fixtureCases.Length);
    }

    private static CaseBinding Case(
        string scenario,
        string suffix,
        string subfamily,
        string caseSha256,
        string factsSha256,
        string[] adaptations,
        string[] targetSymbols,
        string[] contextSymbols) => new(
        scenario,
        "dragon-shape-zone-core." + suffix,
        subfamily,
        caseSha256,
        factsSha256,
        adaptations,
        targetSymbols,
        contextSymbols);

    private static TargetBinding Target(
        string symbol,
        int inventoryIndex,
        string kind,
        string symbolHash,
        string signatureHash,
        string bodyHash,
        string assertionId,
        string adaptationId,
        int[] caseIndices) => new(
        symbol,
        inventoryIndex,
        kind,
        symbolHash,
        signatureHash,
        bodyHash,
        assertionId,
        adaptationId,
        "exception",
        NativeTargetFor(symbol),
        caseIndices);

    private static SourceReceiptPin SourceReceipt(
        string symbol,
        int inventoryIndex,
        string kind,
        string path,
        string symbolHash,
        string signatureHash,
        string bodyHash) => new(
        symbol,
        inventoryIndex,
        kind,
        path,
        symbolHash,
        signatureHash,
        bodyHash);

    private static string NativeTargetFor(string symbol) => symbol switch
    {
        "Zone" => "Dragons.InvisibleDragon.Shape.Zone typed aggregate",
        "Zone.__init__" => "Shape.Zone constructor with validated identifiers, profile, and defensive surface-collection copy retaining immutable Surface references",
        "Zone.floor_area" => "Zone.FloorArea over immutable native Surface.GrossArea values",
        "Zone.floor_surface" => "Zone.FloorSurfaces filtered from the native read-only surface collection",
        "Zone.idf_airexhaustnodelistname" => "EnergyModelIdfAssembler zone exhaust-node naming",
        "Zone.idf_airinletnodelistname" => "EnergyModelIdfAssembler zone inlet-node naming",
        "Zone.idf_equipmentlistname" => "EnergyModelIdfAssembler zone equipment-list naming",
        "Zone.supply" => "ZoneHvacAssignment external HVAC association model",
        _ => throw new Xunit.Sdk.XunitException($"No native target for '{symbol}'."),
    };

    private static string NativeImplementationFor(string symbol) => symbol switch
    {
        "Zone" => "Dragons.InvisibleDragon.Shape.Zone",
        "Zone.__init__" => "Dragons.InvisibleDragon.Shape.Zone constructor(EntityId,string,IEnumerable<Surface>,Profile,double,double,double)",
        "Zone.floor_area" => "Dragons.InvisibleDragon.Shape.Zone.FloorArea",
        "Zone.floor_surface" => "Dragons.InvisibleDragon.Shape.Zone.FloorSurfaces",
        "Zone.idf_airexhaustnodelistname" => "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument -> EnergyModelIdfAssembler.AppendZoneEquipment/AppendNodeList",
        "Zone.idf_airinletnodelistname" => "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument -> EnergyModelIdfAssembler.AppendZoneEquipment/AppendNodeList",
        "Zone.idf_equipmentlistname" => "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument -> EnergyModelIdfAssembler.AppendZoneEquipment",
        "Zone.supply" => "Dragons.InvisibleDragon.Hvac.ZoneHvacAssignment(EntityId,SupplyGroup); no property on Shape.Zone",
        _ => throw new Xunit.Sdk.XunitException($"No native implementation for '{symbol}'."),
    };

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
        });
    }

    private static JsonElement[] ValidateOracle(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(root, "case_sha256", "cases", "cases_sha256", "consumer_contract", "context_receipts",
            "fact_sha256", "resolved_receipts", "runtime", "schema", "symbols", "target_receipts", "upstream");
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        JsonElement casesElement = root.GetProperty("cases");
        Assert.Equal(CasesSha256, CanonicalSha256(casesElement));
        JsonElement[] cases = casesElement.EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, cases.Length);
        Assert.Equal(Cases.Select(item => item.CaseId), cases.Select(item => RequiredString(item, "id")));

        JsonElement factHashes = root.GetProperty("fact_sha256");
        JsonElement caseHashes = root.GetProperty("case_sha256");
        for (int index = 0; index < cases.Length; index++)
        {
            JsonElement item = cases[index];
            CaseBinding binding = Cases[index];
            AssertKeys(item, "context_symbols", "executor", "expected_dotnet", "id", "python", "subfamily", "target_symbols");
            Assert.Equal("shape-zone-core", RequiredString(item, "executor"));
            Assert.Equal(binding.Subfamily, RequiredString(item, "subfamily"));
            AssertStringArray(item.GetProperty("target_symbols"), binding.TargetSymbols);
            AssertStringArray(item.GetProperty("context_symbols"), binding.ContextSymbols);

            JsonElement expected = item.GetProperty("expected_dotnet");
            Assert.Equal("adapted-as-pinned", RequiredString(expected, "outcome"));
            AssertStringArray(expected.GetProperty("adaptations"), binding.Adaptations);
            JsonElement classifications = expected.GetProperty("classifications");
            Assert.Equal(binding.TargetSymbols.Length, classifications.EnumerateObject().Count());
            foreach (string symbol in binding.TargetSymbols)
            {
                Assert.Equal("exception", RequiredString(classifications, symbol));
            }

            JsonElement python = item.GetProperty("python");
            Assert.Equal("observed", RequiredString(python, "outcome"));
            JsonElement facts = python.GetProperty("facts");
            Assert.Equal(binding.Scenario, RequiredString(facts, "scenario"));
            Assert.Equal(binding.FactsSha256, RequiredString(python, "facts_sha256"));
            Assert.Equal(binding.FactsSha256, RequiredString(factHashes, binding.CaseId));
            Assert.Equal(binding.FactsSha256, CanonicalSha256(facts));
            Assert.Equal(binding.CaseSha256, RequiredString(caseHashes, binding.CaseId));
            Assert.Equal(binding.CaseSha256, CanonicalSha256(item));
        }

        ValidateTargetReceipts(root);
        ValidateContract(root.GetProperty("consumer_contract"), cases);
        ValidateRuntimeAndUpstream(root);
        return cases;
    }

    private static void ValidateTargetReceipts(JsonElement root)
    {
        JsonElement[] descriptors = root.GetProperty("symbols").EnumerateArray().ToArray();
        JsonElement[] receipts = root.GetProperty("target_receipts").EnumerateArray().ToArray();
        Assert.Equal(Targets.Length, descriptors.Length);
        Assert.Equal(Targets.Length, receipts.Length);

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        Assert.Equal(InventoryBytes, inventoryBytes.Length);
        Assert.Equal(InventoryFileSha256, Sha256(inventoryBytes));
        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        JsonElement inventorySymbols = inventory.RootElement.GetProperty("symbols");
        for (int index = 0; index < Targets.Length; index++)
        {
            TargetBinding target = Targets[index];
            AssertReceiptFields(descriptors[index], target, includeIndex: false);
            AssertReceiptFields(receipts[index], target, includeIndex: true);
            JsonElement inventorySymbol = inventorySymbols[target.InventoryIndex];
            Assert.Equal(target.Symbol, RequiredString(inventorySymbol, "symbol"));
            Assert.Equal(target.Kind, RequiredString(inventorySymbol, "kind"));
            Assert.Equal(target.SymbolHash, RequiredString(inventorySymbol, "symbol_hash"));
            Assert.Equal(target.SignatureHash, RequiredString(inventorySymbol, "signature_hash"));
            Assert.Equal(target.BodyHash, RequiredString(inventorySymbol, "body_hash"));
            Assert.Equal(UpstreamPath, RequiredString(inventorySymbol, "path"));
        }

        ValidateSourceReceiptSet(root.GetProperty("context_receipts"), ContextReceiptPins);
        ValidateSourceReceiptSet(root.GetProperty("resolved_receipts"), ResolvedReceiptPins);
    }

    private static void AssertReceiptFields(JsonElement value, TargetBinding target, bool includeIndex)
    {
        Assert.Equal(target.Symbol, RequiredString(value, "symbol"));
        Assert.Equal(target.Kind, RequiredString(value, "kind"));
        Assert.Equal(target.SymbolHash, RequiredString(value, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(value, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(value, "body_hash"));
        Assert.Equal(UpstreamPath, RequiredString(value, "path"));
        if (includeIndex)
        {
            Assert.Equal(target.InventoryIndex, value.GetProperty("inventory_index").GetInt32());
        }
    }

    private static void ValidateSourceReceiptSet(JsonElement value, IReadOnlyList<SourceReceiptPin> pins)
    {
        JsonElement[] receipts = value.EnumerateArray().ToArray();
        Assert.Equal(pins.Count, receipts.Length);
        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        Assert.Equal(InventoryBytes, inventoryBytes.Length);
        Assert.Equal(InventoryFileSha256, Sha256(inventoryBytes));
        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        JsonElement inventorySymbols = inventory.RootElement.GetProperty("symbols");
        for (int index = 0; index < pins.Count; index++)
        {
            SourceReceiptPin pin = pins[index];
            JsonElement receipt = receipts[index];
            Assert.Equal(pin.InventoryIndex, receipt.GetProperty("inventory_index").GetInt32());
            Assert.Equal(pin.Symbol, RequiredString(receipt, "symbol"));
            Assert.Equal(pin.Kind, RequiredString(receipt, "kind"));
            Assert.Equal(pin.Path, RequiredString(receipt, "path"));
            Assert.Equal(pin.SymbolHash, RequiredString(receipt, "symbol_hash"));
            Assert.Equal(pin.SignatureHash, RequiredString(receipt, "signature_hash"));
            Assert.Equal(pin.BodyHash, RequiredString(receipt, "body_hash"));

            JsonElement inventoryReceipt = inventorySymbols[pin.InventoryIndex];
            Assert.Equal(pin.Symbol, RequiredString(inventoryReceipt, "symbol"));
            Assert.Equal(pin.Kind, RequiredString(inventoryReceipt, "kind"));
            Assert.Equal(pin.Path, RequiredString(inventoryReceipt, "path"));
            Assert.Equal(pin.SymbolHash, RequiredString(inventoryReceipt, "symbol_hash"));
            Assert.Equal(pin.SignatureHash, RequiredString(inventoryReceipt, "signature_hash"));
            Assert.Equal(pin.BodyHash, RequiredString(inventoryReceipt, "body_hash"));
        }
    }

    private static void ValidateContract(JsonElement contract, IReadOnlyList<JsonElement> cases)
    {
        Assert.Equal(10, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        AssertStringArray(contract.GetProperty("target_symbols"), Targets.Select(item => item.Symbol));
        Assert.Equal(8, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        Assert.Equal(0, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());

        for (int index = 0; index < Targets.Length; index++)
        {
            TargetBinding target = Targets[index];
            Assert.Equal(target.AdaptationId, RequiredString(contract.GetProperty("adaptations"), target.Symbol));
            Assert.Equal(target.AssertionId, RequiredString(contract.GetProperty("assertion_ids"), target.Symbol));
            Assert.Equal("exception", RequiredString(contract.GetProperty("classifications"), target.Symbol));
            Assert.Equal(target.NativeTarget, RequiredString(contract.GetProperty("native_targets"), target.Symbol));
            string[] actualCoverage = cases
                .Where(item => item.GetProperty("target_symbols").EnumerateArray().Any(symbol => symbol.GetString() == target.Symbol))
                .Select(item => RequiredString(item, "id"))
                .ToArray();
            Assert.Equal(target.CaseIndices.Select(caseIndex => Cases[caseIndex].CaseId), actualCoverage);
        }

        JsonElement closure = contract.GetProperty("closure");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.True(closure.GetProperty("target_coverage_complete").GetBoolean());
        Assert.Equal("exact-ten-case-eight-target-zone-core-matrix", RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("target_symbols"), Targets.Select(item => item.Symbol));
        AssertStringArray(closure.GetProperty("unresolved_boundaries"), UnresolvedBoundaries);
        ValidateSourceReceiptSet(closure.GetProperty("resolved_receipts_not_retargeted"), ResolvedReceiptPins);
        ValidateSourceReceiptSet(closure.GetProperty("context_receipts"), ContextReceiptPins);
        JsonElement[] contractTargetReceipts = contract.GetProperty("target_receipts").EnumerateArray().ToArray();
        Assert.Equal(Targets.Length, contractTargetReceipts.Length);
        for (int index = 0; index < Targets.Length; index++)
        {
            AssertReceiptFields(contractTargetReceipts[index], Targets[index], includeIndex: true);
        }

        JsonElement observedDomain = closure.GetProperty("observed_floor_sum_domain");
        AssertStringArray(observedDomain.GetProperty("edge_success_inputs"), new[] { "bool:True", "int:3", "float:2.5" });
        AssertStringArray(observedDomain.GetProperty("edge_failure_inputs"), new[] { "str:'bad'-as-first-floor-area" });
        Assert.Equal("float:12.5", RequiredString(observedDomain, "representative_finite_input"));
    }

    private static void ValidateRuntimeAndUpstream(JsonElement root)
    {
        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        JsonElement shape = upstream.GetProperty("shape_source");
        Assert.Equal(UpstreamPath, RequiredString(shape, "path"));
        Assert.Equal(UpstreamBytes, shape.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(shape, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(shape, "ast_sha256"));
        JsonElement loaded = Assert.Single(upstream.GetProperty("loaded_local_modules").EnumerateArray(),
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("idragon.dragon.shape", RequiredString(loaded, "module"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(loaded, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(loaded, "ast_sha256"));
    }

    private static void ValidatePinnedArtifactsAndNativeApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        Assert.Equal("Dragons.InvisibleDragon.Shape.Zone", typeof(Zone).FullName);
        Assert.True(typeof(Zone).IsSealed);
        ConstructorInfo constructor = Assert.Single(typeof(Zone).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(new[]
        {
            typeof(EntityId), typeof(string), typeof(IEnumerable<Surface>), typeof(ZoneProfile),
            typeof(double), typeof(double), typeof(double),
        }, constructor.GetParameters().Select(item => item.ParameterType));
        Assert.Equal(new[]
        {
            "id", "name", "surfaces", "profile", "infiltrationAirChangesPerHour",
            "lightingPowerDensityWattsPerSquareMetre", "outdoorAirFlowCubicMetresPerSecond",
        }, constructor.GetParameters().Select(item => item.Name));

        AssertGetOnlyProperty<Zone, IReadOnlyList<Surface>>(nameof(Zone.Surfaces));
        AssertGetOnlyProperty<Zone, IReadOnlyList<Surface>>(nameof(Zone.FloorSurfaces));
        AssertGetOnlyProperty<Zone, double>(nameof(Zone.FloorArea));
        AssertGetOnlyProperty<Zone, string>(nameof(Zone.Name));
        Assert.Null(typeof(Zone).GetProperty("Supply", BindingFlags.Public | BindingFlags.Instance));

        ConstructorInfo assignmentConstructor = Assert.Single(
            typeof(ZoneHvacAssignment).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(new[] { typeof(EntityId), typeof(SupplyGroup) },
            assignmentConstructor.GetParameters().Select(item => item.ParameterType));
        AssertGetOnlyProperty<ZoneHvacAssignment, EntityId>(nameof(ZoneHvacAssignment.ZoneId));
        AssertGetOnlyProperty<ZoneHvacAssignment, SupplyGroup>(nameof(ZoneHvacAssignment.Supply));

        Type assembler = typeof(EnergyModel).Assembly.GetType(
            "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler", throwOnError: true)!;
        MethodInfo appendEquipment = Assert.Single(assembler.GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            item => item.Name == "AppendZoneEquipment");
        Assert.Equal(typeof(void), appendEquipment.ReturnType);
        Assert.Equal(new[] { "document", "context", "zone", "equipment", "supply" },
            appendEquipment.GetParameters().Select(item => item.Name));
        MethodInfo appendNodeList = Assert.Single(assembler.GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            item => item.Name == "AppendNodeList");
        Assert.Equal(typeof(string), appendNodeList.ReturnType);
        Assert.Equal(new[] { "document", "context", "name", "nodes" },
            appendNodeList.GetParameters().Select(item => item.Name));
    }

    private static void AssertGetOnlyProperty<TDeclaring, TValue>(string name)
    {
        PropertyInfo property = typeof(TDeclaring).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!;
        Assert.NotNull(property);
        Assert.Equal(typeof(TValue), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    private static NativeObservation ObserveNativeCase(int index) => index switch
    {
        0 => ObserveZ01(),
        1 => ObserveZ02(),
        2 => ObserveZ03(),
        3 => ObserveZ04(),
        4 => ObserveZ05(),
        5 => ObserveZ06(),
        6 => ObserveZ07(),
        7 => ObserveZ08(),
        8 => ObserveZ09(),
        9 => ObserveZ10(),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
    };

    private static NativeObservation ObserveZ01()
    {
        var source = new List<Surface>();
        ZoneProfile profile = EmptyProfile("Z01");
        var zone = new Zone(
            Id("Z01-ZONE"),
            "Representative",
            source,
            profile,
            infiltrationAirChangesPerHour: 0.25,
            lightingPowerDensityWattsPerSquareMetre: 8,
            outdoorAirFlowCubicMetresPerSecond: 0.5);
        Assert.Equal("Representative", zone.Name);
        Assert.Same(profile, zone.Profile);
        Assert.Empty(zone.Surfaces);
        Assert.Equal(0.25, zone.InfiltrationAirChangesPerHour);
        Assert.Equal(8, zone.LightingPowerDensityWattsPerSquareMetre);
        Assert.Equal(0.5, zone.OutdoorAirFlowCubicMetresPerSecond);

        Exception blankName = Assert.Throws<ArgumentException>(() => new Zone(
            Id("Z01-BLANK"), " \t ", source, profile));
        Exception nullSurfaces = Assert.Throws<ArgumentNullException>(() => new Zone(
            Id("Z01-NULL"), "Null Surfaces", null!, profile));
        Exception nanInfiltration = Assert.Throws<ArgumentOutOfRangeException>(() => new Zone(
            Id("Z01-NAN"), "NaN", source, profile, infiltrationAirChangesPerHour: double.NaN));
        Exception hugeLighting = Assert.Throws<ArgumentOutOfRangeException>(() => new Zone(
            Id("Z01-INF"), "Infinity", source, profile,
            lightingPowerDensityWattsPerSquareMetre: double.PositiveInfinity));
        Assert.Empty(source);

        return Observation("Z01",
            "native-fqn=" + typeof(Zone).FullName,
            "representative-name=" + zone.Name,
            "representative-surface-count=" + zone.Surfaces.Count,
            "representative-profile-reference-retained=" + Boolean(ReferenceEquals(profile, zone.Profile)),
            "representative-infiltration=" + Double(zone.InfiltrationAirChangesPerHour),
            "representative-lighting-density=" + Double(zone.LightingPowerDensityWattsPerSquareMetre),
            "representative-outdoor-air-flow=" + Double(zone.OutdoorAirFlowCubicMetresPerSecond),
            "blank-name-error=" + Error(blankName),
            "null-surfaces-error=" + Error(nullSurfaces),
            "nan-infiltration-error=" + Error(nanInfiltration),
            "infinite-lighting-error=" + Error(hugeLighting),
            "Zone-constructor-parameter-types=" + Join(Assert.Single(typeof(Zone).GetConstructors())
                .GetParameters().Select(item => TypeName(item.ParameterType))),
            "source-surface-list-count-after-failed-constructions=" + source.Count);
    }

    private static NativeObservation ObserveZ02()
    {
        var authored = new List<Surface>();
        Zone zone = ZoneFor("Z02", "Empty Floor Zone", authored);
        IReadOnlyList<Surface> first = zone.FloorSurfaces;
        IReadOnlyList<Surface> second = zone.FloorSurfaces;
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.NotSame(first, second);
        Assert.NotSame(authored, zone.Surfaces);
        Assert.Equal(0, zone.FloorArea);
        Assert.Empty(zone.Surfaces);
        IList<Surface> readOnlyProjection = Assert.IsAssignableFrom<IList<Surface>>(first);
        Surface rejectedFloor = SurfaceFor("Z02-REJECTED", "Rejected Floor", SurfaceType.Floor, 1);
        Exception add = Assert.Throws<NotSupportedException>(() => readOnlyProjection.Add(rejectedFloor));
        Assert.Empty(first);
        Assert.Empty(zone.Surfaces);
        return Observation("Z02",
            "authored-and-stored-collection-reference-same=" + Boolean(ReferenceEquals(authored, zone.Surfaces)),
            "native-surface-count=" + zone.Surfaces.Count,
            "first-floor-count=" + first.Count,
            "second-floor-count=" + second.Count,
            "fresh-floor-projection=" + Boolean(!ReferenceEquals(first, second)),
            "floor-projection-add-error=" + Error(add),
            "floor-area=" + Double(zone.FloorArea),
            "source-zone-state-after-reads=" + ZoneState(zone));
    }

    private static NativeObservation ObserveZ03()
    {
        Surface wall = SurfaceFor("Z03-W", "wall-1", SurfaceType.Wall, 4);
        Surface floor = SurfaceFor("Z03-F", "floor-1", SurfaceType.Floor, 12.5);
        Surface stringFloor = SurfaceFor("Z03-S", "string-floor", SurfaceType.Wall, 6);
        Surface ceiling = SurfaceFor("Z03-C", "ceiling-1", SurfaceType.Ceiling, 7);
        Zone zone = ZoneFor("Z03", "Mixed Zone", new[] { wall, floor, stringFloor, ceiling });
        IReadOnlyList<Surface> first = zone.FloorSurfaces;
        IReadOnlyList<Surface> second = zone.FloorSurfaces;
        Assert.Same(floor, Assert.Single(first));
        Assert.Same(floor, Assert.Single(second));
        Assert.NotSame(first, second);
        Assert.Equal(12.5, zone.FloorArea);
        Assert.Equal(new[] { wall, floor, stringFloor, ceiling }, zone.Surfaces);
        PropertyInfo surfaceType = typeof(Surface).GetProperty(nameof(Surface.Type))!;
        return Observation("Z03",
            "surface-order=" + Join(zone.Surfaces.Select(item => item.Name)),
            "first-floor-order=" + Join(first.Select(item => item.Name)),
            "second-floor-order=" + Join(second.Select(item => item.Name)),
            "fresh-floor-projection=" + Boolean(!ReferenceEquals(first, second)),
            "floor-reference-retained=" + Boolean(ReferenceEquals(floor, first[0]) && ReferenceEquals(floor, second[0])),
            "floor-area=" + Double(zone.FloorArea),
            "Surface.Type-property-type=" + surfaceType.PropertyType.FullName,
            "Surface.Type-setter-present=" + Boolean(surfaceType.CanWrite),
            "source-zone-state-after-reads=" + ZoneState(zone));
    }

    private static NativeObservation ObserveZ04()
    {
        Surface wall = SurfaceFor("Z04-W", "wall-a", SurfaceType.Wall, 3);
        Surface ceiling = SurfaceFor("Z04-C", "ceiling-a", SurfaceType.Ceiling, 5);
        Surface stringFloor = SurfaceFor("Z04-S", "string-floor", SurfaceType.Wall, 7);
        Zone zone = ZoneFor("Z04", "No Floor Zone", new[] { wall, ceiling, stringFloor });
        IReadOnlyList<Surface> first = zone.FloorSurfaces;
        IReadOnlyList<Surface> second = zone.FloorSurfaces;
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.NotSame(first, second);
        Assert.Equal(0, zone.FloorArea);
        return Observation("Z04",
            "surface-order=" + Join(zone.Surfaces.Select(item => item.Name)),
            "first-floor-count=" + first.Count,
            "second-floor-count=" + second.Count,
            "fresh-floor-projection=" + Boolean(!ReferenceEquals(first, second)),
            "floor-area=" + Double(zone.FloorArea),
            "source-zone-state-after-reads=" + ZoneState(zone));
    }

    private static NativeObservation ObserveZ05()
    {
        Surface boolInput = SurfaceFor("Z05-B", "floor-bool", SurfaceType.Floor, 1);
        Surface wall = SurfaceFor("Z05-W", "wall-huge", SurfaceType.Wall, 9);
        Surface integer = SurfaceFor("Z05-I", "floor-int", SurfaceType.Floor, 3);
        Surface floating = SurfaceFor("Z05-D", "floor-float", SurfaceType.Floor, 2.5);
        Zone zone = ZoneFor("Z05", "Multiple Floor Zone", new[] { boolInput, wall, integer, floating });
        IReadOnlyList<Surface> first = zone.FloorSurfaces;
        IReadOnlyList<Surface> second = zone.FloorSurfaces;
        Assert.Equal(new[] { boolInput, integer, floating }, first);
        Assert.Equal(new[] { boolInput, integer, floating }, second);
        Assert.NotSame(first, second);
        Assert.Equal(6.5, zone.FloorArea);
        Assert.All(first, item => Assert.True(double.IsFinite(item.GrossArea)));
        PropertyInfo grossArea = typeof(Surface).GetProperty(nameof(Surface.GrossArea))!;
        return Observation("Z05",
            "native-surface-order=" + Join(zone.Surfaces.Select(item => item.Name)),
            "native-floor-areas=" + Join(first.Select(item => Double(item.GrossArea))),
            "native-first-floor-order=" + Join(first.Select(item => item.Name)),
            "native-second-floor-order=" + Join(second.Select(item => item.Name)),
            "fresh-floor-projection=" + Boolean(!ReferenceEquals(first, second)),
            "native-floor-area-sum=" + Double(zone.FloorArea),
            "authored-native-area-runtime-types=" + Join(first.Select(item => item.GrossArea.GetType().FullName!)),
            "Surface.GrossArea-property-type=" + grossArea.PropertyType.FullName,
            "source-zone-state-after-reads=" + ZoneState(zone));
    }

    private static NativeObservation ObserveZ06()
    {
        Surface floor = SurfaceFor("Z06-F", "Retained Floor", SurfaceType.Floor, 4);
        Surface wall = SurfaceFor("Z06-W", "Retained Wall", SurfaceType.Wall, 5);
        Surface lateFloor = SurfaceFor("Z06-LATE", "Late Floor", SurfaceType.Floor, 6);
        var authored = new List<Surface> { floor, wall };
        string authoredBefore = Join(authored.Select(item => item.Id.ToString()));
        Zone zone = ZoneFor("Z06", "Defensive Copy Zone", authored);
        string before = ZoneState(zone);

        authored.Add(lateFloor);
        string authoredAfterAppend = Join(authored.Select(item => item.Id.ToString()));
        authored.Reverse();
        string authoredAfterReverse = Join(authored.Select(item => item.Id.ToString()));
        authored.Clear();
        IReadOnlyList<Surface> firstFloors = zone.FloorSurfaces;
        IReadOnlyList<Surface> secondFloors = zone.FloorSurfaces;
        Assert.Equal(before, ZoneState(zone));
        Assert.Equal(new[] { floor, wall }, zone.Surfaces);
        Assert.Same(floor, zone.Surfaces[0]);
        Assert.Same(wall, zone.Surfaces[1]);
        Assert.Same(floor, Assert.Single(firstFloors));
        Assert.Same(floor, Assert.Single(secondFloors));
        Assert.NotSame(firstFloors, secondFloors);
        Assert.Equal(4, zone.FloorArea);

        IList<Surface> nativeCollection = Assert.IsAssignableFrom<IList<Surface>>(zone.Surfaces);
        Exception replacement = Assert.Throws<NotSupportedException>(() => nativeCollection[0] = lateFloor);
        Assert.Equal(before, ZoneState(zone));
        return Observation("Z06",
            "authored-list-before=" + authoredBefore,
            "authored-and-stored-collection-reference-same=" + Boolean(ReferenceEquals(authored, zone.Surfaces)),
            "native-zone-before-source-list-mutation=" + before,
            "authored-list-after-append=" + authoredAfterAppend,
            "authored-list-after-reverse=" + authoredAfterReverse,
            "authored-list-after-clear=" + Join(authored.Select(item => item.Id.ToString())),
            "native-zone-after-source-list-mutation=" + ZoneState(zone),
            "stored-surface-references-match-original-elements=" + Boolean(ReferenceEquals(floor, zone.Surfaces[0]) && ReferenceEquals(wall, zone.Surfaces[1])),
            "Surface-sealed=" + Boolean(typeof(Surface).IsSealed),
            "Surface-public-setter-count=" + typeof(Surface).GetProperties(BindingFlags.Public | BindingFlags.Instance).Count(item => item.CanWrite),
            "floor-projection-fresh=" + Boolean(!ReferenceEquals(firstFloors, secondFloors)),
            "floor-projection-retains-surface-reference=" + Boolean(ReferenceEquals(floor, firstFloors[0]) && ReferenceEquals(floor, secondFloors[0])),
            "floor-area-after-authored-list-mutation=" + Double(zone.FloorArea),
            "read-only-collection-replacement-error=" + Error(replacement),
            "native-zone-state-after-failed-replacement=" + ZoneState(zone));
    }

    private static NativeObservation ObserveZ07()
    {
        const string zoneName = "North Ω / Zone 01";
        (EnergyModel model, Zone zone, ZoneHvacAssignment assignment) = ConditionedAirModel("Z07", zoneName);
        string sourceBefore = ModelState(model);
        IdfDocument first = model.ToIdfDocument(options: LegacyOptions());
        IdfDocument second = model.ToIdfDocument(options: LegacyOptions());
        Assert.NotSame(first, second);
        Assert.Equal(IdfState(first), IdfState(second));
        Assert.Equal(sourceBefore, ModelState(model));

        string equipmentName = $"EquipmentList_for_{zoneName}";
        string inletName = $"{zoneName} Air InletNode List";
        string exhaustName = $"{zoneName} Air ExhaustNode List";
        IdfObject equipment = Assert.Single(first["ZoneHVAC:EquipmentList"]);
        Assert.Equal(equipmentName, equipment.Name);
        IdfObject[] nodeLists = first["NodeList"].ToArray();
        Assert.Equal(new[] { inletName, exhaustName }, nodeLists.Select(item => item.Name));
        IdfObject connections = Assert.Single(first["ZoneHVAC:EquipmentConnections"]);
        Assert.Equal(zoneName, connections[0]);
        Assert.Equal(equipmentName, connections[1]);
        Assert.Equal(inletName, connections[2]);
        Assert.Equal(exhaustName, connections[3]);
        Assert.Same(assignment, Assert.Single(model.HvacAssignments));
        Exception emptyName = Assert.Throws<ArgumentException>(() => new Zone(
            Id("Z07-EMPTY-NAME"), string.Empty, zone.Surfaces, zone.Profile));
        Exception nullName = Assert.Throws<ArgumentNullException>(() => new Zone(
            Id("Z07-NULL-NAME"), null!, zone.Surfaces, zone.Profile));
        Assert.Equal(sourceBefore, ModelState(model));

        return Observation("Z07",
            "native-zone-name=" + zone.Name,
            "equipment-list-name=" + equipment.Name,
            "inlet-node-list-name=" + nodeLists[0].Name,
            "exhaust-node-list-name=" + nodeLists[1].Name,
            "equipment-connections-name-fields=" + Join(connections.Fields.Take(4).Select(item => item.Value)),
            "Zone.Name-property-type=" + typeof(Zone).GetProperty(nameof(Zone.Name))!.PropertyType.FullName,
            "Zone.Name-setter-present=" + Boolean(typeof(Zone).GetProperty(nameof(Zone.Name))!.CanWrite),
            "empty-name-constructor-error=" + Error(emptyName),
            "null-name-constructor-error=" + Error(nullName),
            "native-zone-name-after-reads-and-failed-constructions=" + zone.Name);
    }

    private static NativeObservation ObserveZ08()
    {
        Zone noneZone = ZoneFor("Z08-NONE", "No Supply Zone", Array.Empty<Surface>());
        var noneModel = new EnergyModel("Z08 no assignment", new[] { noneZone });
        Assert.Empty(noneModel.HvacAssignments);
        Assert.Null(typeof(Zone).GetProperty("Supply", BindingFlags.Public | BindingFlags.Instance));

        var firstSystem = new ElectricRadiator(Id("Z08-FIRST"), "first");
        var secondSystem = new ElectricRadiator(Id("Z08-SECOND"), "second");
        Zone directZone = ZoneFor("Z08-DIRECT", "Direct Counterpart Zone", Array.Empty<Surface>());
        var explicitWrapper = new SupplyGroup(new SupplySystem[] { firstSystem });
        var directAssignment = new ZoneHvacAssignment(directZone.Id, explicitWrapper);
        Zone existingZone = ZoneFor("Z08-EXISTING", "Existing Group Zone", Array.Empty<Surface>());
        var existingGroup = new SupplyGroup(new SupplySystem[] { firstSystem, secondSystem });
        var existingAssignment = new ZoneHvacAssignment(existingZone.Id, existingGroup);
        var assignedModel = new EnergyModel(
            "Z08 external assignments",
            new[] { directZone, existingZone },
            new[] { directAssignment, existingAssignment });
        Assert.Same(explicitWrapper, directAssignment.Supply);
        Assert.Same(existingGroup, existingAssignment.Supply);
        Assert.Equal(new[] { directAssignment, existingAssignment }, assignedModel.HvacAssignments);
        Assert.Same(firstSystem, Assert.Single(explicitWrapper.Systems));
        Assert.Equal(new SupplySystem[] { firstSystem, secondSystem }, existingGroup.Systems);
        Assert.Single(explicitWrapper.Availabilities);
        Assert.Null(explicitWrapper.Availabilities[0]);
        Assert.Equal(2, existingGroup.Availabilities.Count);
        Assert.All(existingGroup.Availabilities, Assert.Null);

        ConstructorInfo constructor = Assert.Single(typeof(ZoneHvacAssignment).GetConstructors());
        Assert.Equal(typeof(SupplyGroup), constructor.GetParameters()[1].ParameterType);
        int zoneHvacParameterCount = Assert.Single(typeof(Zone).GetConstructors()).GetParameters()
            .Count(item => typeof(HvacSystem).IsAssignableFrom(item.ParameterType)
                || item.ParameterType == typeof(SupplyGroup)
                || item.ParameterType == typeof(ZoneHvacAssignment));
        return Observation("Z08",
            "unassigned-model-HvacAssignments-count=" + noneModel.HvacAssignments.Count,
            "Zone.Supply-property-found=" + Boolean(typeof(Zone).GetProperty("Supply") is not null),
            "Zone-constructor-HVAC-parameter-count=" + zoneHvacParameterCount,
            "assignment-second-parameter-type=" + constructor.GetParameters()[1].ParameterType.FullName,
            "explicit-one-system-wrapper-count=" + explicitWrapper.Systems.Count,
            "explicit-one-system-wrapper-order=" + Join(explicitWrapper.Systems.Select(item => item.Name)),
            "explicit-one-system-wrapper-availability-count=" + explicitWrapper.Availabilities.Count,
            "explicit-one-system-wrapper-first-availability-is-null=" + Boolean(explicitWrapper.Availabilities[0] is null),
            "direct-assignment-wrapper-reference-retained=" + Boolean(ReferenceEquals(explicitWrapper, directAssignment.Supply)),
            "direct-assignment-system-reference-retained=" + Boolean(ReferenceEquals(firstSystem, directAssignment.Supply.Systems[0])),
            "existing-group-system-count=" + existingGroup.Systems.Count,
            "existing-group-system-order=" + Join(existingGroup.Systems.Select(item => item.Name)),
            "existing-group-availability-count=" + existingGroup.Availabilities.Count,
            "existing-group-null-availability-flags=" + Join(existingGroup.Availabilities.Select(item => Boolean(item is null))),
            "existing-assignment-group-reference-retained=" + Boolean(ReferenceEquals(existingGroup, existingAssignment.Supply)),
            "existing-group-system-reference-flags=" + Join(new[]
            {
                Boolean(ReferenceEquals(firstSystem, existingGroup.Systems[0])),
                Boolean(ReferenceEquals(secondSystem, existingGroup.Systems[1])),
            }),
            "assigned-model-assignment-reference-flags=" + Join(new[]
            {
                Boolean(ReferenceEquals(directAssignment, assignedModel.HvacAssignments[0])),
                Boolean(ReferenceEquals(existingAssignment, assignedModel.HvacAssignments[1])),
            }),
            "ZoneHvacAssignment-public-setter-count=" + typeof(ZoneHvacAssignment)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Count(item => item.CanWrite));
    }

    private static NativeObservation ObserveZ09()
    {
        Zone zone = ZoneFor("Z09", "Invalid Supply Probe", Array.Empty<Surface>());
        var radiator = new ElectricRadiator(Id("Z09-RADIATOR"), "Radiator");
        var group = new SupplyGroup(new SupplySystem[] { radiator });
        string zoneBefore = ZoneState(zone);
        string radiatorBefore = HvacState(radiator);
        ConstructorInfo constructor = Assert.Single(typeof(ZoneHvacAssignment).GetConstructors());

        Exception nullGroup = Assert.Throws<ArgumentNullException>(() =>
            new ZoneHvacAssignment(zone.Id, null!));
        Exception nullZoneId = Assert.Throws<ArgumentNullException>(() =>
            new ZoneHvacAssignment(null!, group));
        Assert.Equal(zoneBefore, ZoneState(zone));
        Assert.Equal(radiatorBefore, HvacState(radiator));
        Assert.Null(typeof(Zone).GetProperty("Supply", BindingFlags.Public | BindingFlags.Instance));

        return Observation("Z09",
            "Zone.Supply-property-found=" + Boolean(typeof(Zone).GetProperty("Supply") is not null),
            "Zone-constructor-parameter-names=" + Join(Assert.Single(typeof(Zone).GetConstructors())
                .GetParameters().Select(item => item.Name!)),
            "ZoneHvacAssignment-constructor-parameter-types=" + Join(constructor.GetParameters()
                .Select(item => TypeName(item.ParameterType))),
            "null-supply-group-constructor-error=" + Error(nullGroup),
            "null-zone-id-constructor-error=" + Error(nullZoneId),
            "native-zone-state-before=" + zoneBefore,
            "native-zone-state-after=" + ZoneState(zone),
            "native-radiator-state-before=" + radiatorBefore,
            "native-radiator-state-after=" + HvacState(radiator),
            "ZoneHvacAssignment-public-setter-count=" + typeof(ZoneHvacAssignment)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Count(item => item.CanWrite));
    }

    private static NativeObservation ObserveZ10()
    {
        Surface floor = SurfaceFor("Z10-F", "Valid Floor", SurfaceType.Floor, 2);
        var authoredWithNull = new List<Surface> { null! };
        ZoneProfile profile = EmptyProfile("Z10");
        Exception nullSurface = Assert.Throws<ArgumentException>(() => new Zone(
            Id("Z10-NULL"), "Null Surface Zone", authoredWithNull, profile));
        Exception nonfiniteVertex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Vertex(double.NaN, 0, 0));

        ConstructorInfo constructor = Assert.Single(typeof(Zone).GetConstructors());
        Exception wrongCollection = Assert.Throws<ArgumentException>(() => constructor.Invoke(new object?[]
        {
            Id("Z10-WRONG"), "Wrong Collection", new object[] { "missing-area-token" }, profile, 0d, 0d, 0d,
        }));
        Zone zone = ZoneFor("Z10-VALID", "Valid Floor Zone", new[] { floor });
        double first = zone.FloorArea;
        double second = zone.FloorArea;
        IReadOnlyList<Surface> firstProjection = zone.FloorSurfaces;
        IReadOnlyList<Surface> secondProjection = zone.FloorSurfaces;
        Assert.Equal(2, first);
        Assert.Equal(first, second);
        Assert.Same(floor, Assert.Single(firstProjection));
        Assert.Same(floor, Assert.Single(secondProjection));
        Assert.NotSame(firstProjection, secondProjection);
        Assert.Single(authoredWithNull);
        Assert.Null(authoredWithNull[0]);

        PropertyInfo grossArea = typeof(Surface).GetProperty(nameof(Surface.GrossArea))!;
        Assert.Equal(typeof(double), grossArea.PropertyType);
        Assert.False(grossArea.CanWrite);
        Assert.True(typeof(Surface).IsSealed);
        MethodInfo grossAreaGetter = grossArea.GetMethod!;
        PropertyInfo floorArea = typeof(Zone).GetProperty(nameof(Zone.FloorArea))!;
        PropertyInfo floorSurfaces = typeof(Zone).GetProperty(nameof(Zone.FloorSurfaces))!;
        return Observation("Z10",
            "null-surface-collection-element-error=" + Error(nullSurface),
            "wrong-surface-collection-reflection-bind-error=" + Error(wrongCollection),
            "nonfinite-geometry-error-before-Surface-or-Zone-construction=" + Error(nonfiniteVertex),
            "authored-null-list-count-after-error=" + authoredWithNull.Count,
            "authored-null-list-first-still-null=" + Boolean(authoredWithNull[0] is null),
            "Surface-sealed=" + Boolean(typeof(Surface).IsSealed),
            "Surface.GrossArea-type=" + grossArea.PropertyType.FullName,
            "Surface.GrossArea-setter-present=" + Boolean(grossArea.CanWrite),
            "Surface.GrossArea-getter-virtual=" + Boolean(grossAreaGetter.IsVirtual),
            "Zone.FloorArea-getter-virtual=" + Boolean(floorArea.GetMethod!.IsVirtual),
            "Zone.FloorSurfaces-getter-virtual=" + Boolean(floorSurfaces.GetMethod!.IsVirtual),
            "first-floor-projection-count=" + firstProjection.Count,
            "second-floor-projection-count=" + secondProjection.Count,
            "floor-projections-fresh=" + Boolean(!ReferenceEquals(firstProjection, secondProjection)),
            "floor-projections-retain-valid-surface-reference=" + Boolean(ReferenceEquals(floor, firstProjection[0]) && ReferenceEquals(floor, secondProjection[0])),
            "valid-floor-area-first=" + Double(first),
            "valid-floor-area-second=" + Double(second));
    }

    private static NativeObservation Observation(string scenario, params string[] facts)
    {
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, item => Assert.False(string.IsNullOrWhiteSpace(item)));
        string hash = CanonicalSha256(JsonSerializer.SerializeToElement(facts));
        return new NativeObservation(scenario, facts, hash);
    }

    private static object CreateReceipt(TargetBinding target, IReadOnlyList<NativeObservation> observations) => new
    {
        assertion_id = target.AssertionId,
        adaptation_id = target.AdaptationId,
        classification = target.Classification,
        target_symbol = target.Symbol,
        native_target = target.NativeTarget,
        native_implementation = NativeImplementationFor(target.Symbol),
        native_type_fqn = typeof(Zone).FullName,
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
            native_sources = NativeArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
        },
        case_coverage = target.CaseIndices.Select(index => Cases[index].CaseId).ToArray(),
        observations = target.CaseIndices.Select(index => new
        {
            case_id = Cases[index].CaseId,
            python_facts_sha256 = Cases[index].FactsSha256,
            native_fact_count = observations[index].Facts.Length,
            native_facts_sha256 = observations[index].FactsSha256,
            native_facts = observations[index].Facts,
        }).ToArray(),
        scope = new
        {
            exact_target_count = 8,
            equivalent_target_count = 0,
            exception_target_count = 8,
            exact_case_count = 10,
            surface_collection_claim = "defensive collection copy retaining the same immutable Surface references; no deep-copy claim",
            floor_sum_claim = "only finite native double areas plus bounded upstream True/3/2.5 success, 12.5 representative, and first-string failure evidence",
            name_claim = "only assembler-emitted names from the immutable validated native Zone.Name",
            supply_claim = "external ZoneHvacAssignment with explicit SupplyGroup; no Zone-owned supply or implicit direct-system coercion",
            python_supply_error_timing_exclusions = PythonSupplyTimingExclusionsFor(target.Symbol),
            resolved_targets_not_retargeted = ResolvedTargetsNotRetargeted,
            context_symbols_not_targeted = ContextSymbolsNotTargeted,
            unresolved_boundaries = UnresolvedBoundaries,
            unobserved_floor_area_domains_explicitly_excluded = new[]
            {
                "nonfinite-floor-area-values",
                "huge-or-mixed-numeric-overflow-and-coercion",
                "missing-or-raising-area-attributes",
            },
        },
        upstream = new
        {
            ast_sha256 = UpstreamAstSha256,
            commit = UpstreamCommit,
            inventory_sha256 = InventoryContentSha256,
            source_bytes = UpstreamBytes,
            source_sha256 = UpstreamSourceSha256,
        },
    };

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal("exception", RequiredString(receipt, "classification"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));
        Assert.Equal(target.NativeTarget, RequiredString(receipt, "native_target"));
        Assert.Equal(NativeImplementationFor(target.Symbol), RequiredString(receipt, "native_implementation"));
        Assert.Equal("Dragons.InvisibleDragon.Shape.Zone", RequiredString(receipt, "native_type_fqn"));
        AssertReceiptFields(receipt.GetProperty("source_receipt"), target, includeIndex: true);
        AssertStringArray(receipt.GetProperty("case_coverage"), target.CaseIndices.Select(index => Cases[index].CaseId));

        JsonElement[] actual = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(target.CaseIndices.Length, actual.Length);
        for (int index = 0; index < target.CaseIndices.Length; index++)
        {
            int caseIndex = target.CaseIndices[index];
            Assert.Equal(Cases[caseIndex].CaseId, RequiredString(actual[index], "case_id"));
            Assert.Equal(Cases[caseIndex].FactsSha256, RequiredString(actual[index], "python_facts_sha256"));
            Assert.Equal(observations[caseIndex].FactsSha256, RequiredString(actual[index], "native_facts_sha256"));
            Assert.Equal(observations[caseIndex].Facts.Length, actual[index].GetProperty("native_fact_count").GetInt32());
            AssertStringArray(actual[index].GetProperty("native_facts"), observations[caseIndex].Facts);
        }

        JsonElement scope = receipt.GetProperty("scope");
        Assert.Equal(8, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(0, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(8, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(10, scope.GetProperty("exact_case_count").GetInt32());
        AssertStringArray(scope.GetProperty("resolved_targets_not_retargeted"), ResolvedTargetsNotRetargeted);
        AssertStringArray(scope.GetProperty("context_symbols_not_targeted"), ContextSymbolsNotTargeted);
        AssertStringArray(scope.GetProperty("unresolved_boundaries"), UnresolvedBoundaries);
        AssertStringArray(scope.GetProperty("python_supply_error_timing_exclusions"),
            PythonSupplyTimingExclusionsFor(target.Symbol));
        AssertStringArray(scope.GetProperty("unobserved_floor_area_domains_explicitly_excluded"), new[]
        {
            "nonfinite-floor-area-values",
            "huge-or-mixed-numeric-overflow-and-coercion",
            "missing-or-raising-area-attributes",
        });
        Assert.DoesNotContain(target.Symbol, ResolvedTargetsNotRetargeted);
        Assert.DoesNotContain(target.Symbol, ContextSymbolsNotTargeted);
    }

    private static string[] PythonSupplyTimingExclusionsFor(string symbol) => symbol switch
    {
        "Zone.__init__" or "Zone.supply" => new[]
        {
            "Python-Zone.supply-setter-TypeError-and-state-preservation-for-integer-bool-and-token-have-no-native-Zone-setter-route",
            "Python-partially-initialized-Zone-state-after-invalid-constructor-supply-has-no-native-counterpart-because-native-Zone-has-no-supply-parameter",
        },
        _ => Array.Empty<string>(),
    };

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        byte_length = bytes,
        path,
        sha256,
    };

    private static Zone ZoneFor(string suffix, string name, IEnumerable<Surface> surfaces) => new(
        Id(suffix + "-ZONE"), name, surfaces, EmptyProfile(suffix));

    private static ZoneProfile EmptyProfile(string suffix) => new(
        Id(suffix + "-PROFILE"), suffix + " Profile");

    private static Surface SurfaceFor(string suffix, string name, SurfaceType type, double area)
    {
        double z = type == SurfaceType.Ceiling ? 3 : 0;
        var polygon = new PlanarPolygon(new[]
        {
            new Vertex(0, 0, z),
            new Vertex(area, 0, z),
            new Vertex(area, 1, z),
            new Vertex(0, 1, z),
        });
        SurfaceBoundary boundary = type == SurfaceType.Floor
            ? SurfaceBoundary.Ground
            : SurfaceBoundary.Outdoors;
        return new Surface(
            Id(suffix),
            name,
            type,
            TestDomainFactory.WallConstruction("Construction " + suffix),
            boundary,
            polygon);
    }

    private static (EnergyModel Model, Zone Zone, ZoneHvacAssignment Assignment) ConditionedAirModel(
        string suffix,
        string zoneName)
    {
        Schedule heating = Schedule.Constant(suffix + " Heating", 20, ScheduleType.Temperature);
        Schedule cooling = Schedule.Constant(suffix + " Cooling", 26, ScheduleType.Temperature);
        Schedule availability = Schedule.Constant(suffix + " Availability", 1, ScheduleType.OnOff);
        var profile = new ZoneProfile(
            Id(suffix + "-PROFILE"), suffix + " Profile", heating, cooling, availability);
        Surface floor = SurfaceFor(suffix + "-FLOOR", suffix + " Floor", SurfaceType.Floor, 4);
        var zone = new Zone(Id(suffix + "-ZONE"), zoneName, new[] { floor }, profile);
        var source = new HeatPump(Id(suffix + "-SOURCE"), suffix + " Heat Pump", Fuel.Electricity, 3, 3);
        var terminal = new AirHandlingUnit(Id(suffix + "-TERMINAL"), suffix + " Terminal", source);
        var group = new SupplyGroup(new SupplySystem[] { terminal });
        var assignment = new ZoneHvacAssignment(zone.Id, group);
        var model = new EnergyModel(suffix + " Model", new[] { zone }, new[] { assignment });
        return (model, zone, assignment);
    }

    private static EnergyModelIdfOptions LegacyOptions() => new()
    {
        ThrowOnValidationErrors = false,
        AddIdealLoadsForUnassignedZones = false,
        UseLegacySimpleDragonDefaultObjectFields = true,
        UseLegacySimpleDragonScheduleMetadata = true,
        UseLegacySimpleDragonUsedProfileScheduleSelection = true,
        UseLegacySimpleDragonHvacTopology = true,
        UseLegacySimpleDragonVentilation = true,
    };

    private static EntityId Id(string value) => new(value);

    private static string ZoneState(Zone zone) => Join(new[]
    {
        "id=" + zone.Id,
        "name=" + zone.Name,
        "profile=" + zone.Profile.Id,
        "surfaces=" + Join(zone.Surfaces.Select(item => item.Id.ToString())),
        "floors=" + Join(zone.FloorSurfaces.Select(item => item.Id.ToString())),
        "floor-area=" + Double(zone.FloorArea),
        "infiltration=" + Double(zone.InfiltrationAirChangesPerHour),
        "lighting=" + Double(zone.LightingPowerDensityWattsPerSquareMetre),
        "outdoor-air=" + Double(zone.OutdoorAirFlowCubicMetresPerSecond),
    });

    private static string HvacState(HvacSystem value) => Join(new[]
    {
        "type=" + value.GetType().FullName,
        "id=" + value.Id,
        "name=" + value.Name,
    });

    private static string ModelState(EnergyModel model) => Join(new[]
    {
        "name=" + model.Name,
        "zones=" + Join(model.Zones.Select(item => item.Id.ToString())),
        "zone-states=" + Join(model.Zones.Select(ZoneState)),
        "assignments=" + Join(model.HvacAssignments.Select(item =>
            item.ZoneId + ":" + Join(item.Supply.Systems.Select(system => system.Id.ToString())))),
    });

    private static string IdfState(IdfDocument document) => Join(document.Select(item =>
        item.ObjectType + "{" + Join(item.Fields.Select(field => field.Value)) + "}"));

    private static string Error(Exception exception)
    {
        Exception actual = exception is TargetInvocationException { InnerException: not null } wrapper
            ? wrapper.InnerException!
            : exception;
        string? parameter = (actual as ArgumentException)?.ParamName;
        return actual.GetType().Name + (parameter is null ? string.Empty : "(param=" + parameter + ")");
    }

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string TypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName!;
        }

        string genericName = type.GetGenericTypeDefinition().FullName!;
        genericName = genericName.Substring(0, genericName.IndexOf('`'));
        return genericName + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
    }

    private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Join(IEnumerable<string> values) => "[" + string.Join("|", values) + "]";

    private static void AssertPinnedArtifact(string path, int bytes, string sha256)
    {
        string fullPath = FindRepositoryFile(path);
        byte[] content = File.ReadAllBytes(fullPath);
        Assert.Equal(bytes, content.Length);
        Assert.Equal(sha256, Sha256(content));
    }

    private static string FindRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private static string Sha256(byte[] value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

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
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
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

    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));

    private static void AssertStringArray(JsonElement value, IEnumerable<string> expected) =>
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));

    private static string RequiredString(JsonElement value, string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        return property.GetString()!;
    }

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(new Regex(@"0x[0-9a-fA-F]{8,}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
        Assert.DoesNotMatch(new Regex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
        Assert.DoesNotMatch(new Regex(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        string text = value.GetRawText();
        Assert.DoesNotMatch(new Regex(@"[A-Za-z]:\\", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), text);
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
        string Scenario,
        string CaseId,
        string Subfamily,
        string CaseSha256,
        string FactsSha256,
        string[] Adaptations,
        string[] TargetSymbols,
        string[] ContextSymbols);

    private sealed record TargetBinding(
        string Symbol,
        int InventoryIndex,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string AdaptationId,
        string Classification,
        string NativeTarget,
        int[] CaseIndices);

    private sealed record SourceReceiptPin(
        string Symbol,
        int InventoryIndex,
        string Kind,
        string Path,
        string SymbolHash,
        string SignatureHash,
        string BodyHash);

    private sealed record NativeObservation(string Scenario, string[] Facts, string FactsSha256);
}
