#pragma warning disable CA1861 // Inline exact oracle expectations are intentionally immutable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Model;
using Dragons.UpstreamTracker;
using DragonAirHandlingUnit = Dragons.InvisibleDragon.Hvac.AirHandlingUnit;
using DragonChiller = Dragons.InvisibleDragon.Hvac.Chiller;
using DragonHeatPump = Dragons.InvisibleDragon.Hvac.HeatPump;
using DragonPackagedAirConditioner = Dragons.InvisibleDragon.Hvac.PackagedAirConditioner;
using DragonSupplySystem = Dragons.InvisibleDragon.Hvac.SupplySystem;

namespace Dragons.SimpleDragon.Tests;

public sealed class HvacEnumsBaseOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-hvac-enums-base-oracle.json";
    private const int FixtureBytes = 159_545;
    private const string FixtureSha256 =
        "sha256:878c2970a95d4a80e87408cc07a7f5ea2c97c764385e990d8459c536a199a208";
    private const string FixtureSchema =
        "dragons.python-reference.epsimple-hvac-enums-base.v1";
    private const string CasesSha256 =
        "sha256:f90df1feee80855dfa215d58ce0ee856d0b9e128b0bf77332eabf4fba0c92d10";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_hvac_enums_base_oracle.py";
    private const int GeneratorBytes = 61_377;
    private const string GeneratorSha256 =
        "sha256:a397d3169f61a375b12a3934a2270874bfef1f3713a635cfd5e342668d12046b";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_hvac_enums_base_oracle.py";
    private const int ValidatorBytes = 22_120;
    private const string ValidatorSha256 =
        "sha256:236069bdde9be3a556559da1f12150114b75bbf08fbd951918744459f015a491";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/core/hvac.py";
    private const int UpstreamBytes = 53_850;
    private const string UpstreamSourceSha256 =
        "sha256:9f3ecb27ed612baeed530ccbfd5857f1f528de24f222e6ef5093e4a635665d9c";
    private const string UpstreamAstSha256 =
        "sha256:dbbea63f51a001fae4fd73fba96dc099eab8cd5bcec39e3d9bf768e29b463873";
    private const string DependenciesSha256 =
        "sha256:85d50612b42b3818f054fd7d9cdb26a16bbf832c3afc56762ea732f55a48cb22";
    private const string RuntimeSignaturesSha256 =
        "sha256:32a219d193c6a79c54df2c58c55afc045ead9f819f1005201070cfb8c27d8104";
    private const string LoadedSourcesSha256 =
        "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8";
    private const string RelocationSnapshotSha256 =
        "sha256:ee4d52a9bf09e386f30abb4498166fb4480d770ca2801bdbcad93910100bff7e";
    private const string EmptyResourcesSha256 =
        "sha256:4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";
    private const string NativeRoutesSha256 =
        "sha256:db277df5be4d4385893d9d1c4e6ae9a0fdec15f1f212934c064d3d9ce0ef064f";
    private const string NativeSourcesSha256 =
        "sha256:30a4045345cc9f0ade5d6fad123a9b2f17350386b365fc5c4da8e248340f8b6a";
    private const string NativeAuditSha256 =
        "sha256:7bafd0bc6391de46ce7a517ec5ea966d66e5310350c24fa47f8a30deea3799ee";
    private const string NativeTemplatePath =
        "fixtures/simple-dragon/grm/ASHRAE 140 modified.grm";
    private const int NativeTemplateBytes = 9_154;
    private const string NativeTemplateSha256 =
        "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.HvacEnumsBaseOracleParityTests.MatchesPinnedHvacEnumsBaseThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SourceSystem.cs", 6_885,
            "sha256:db5fafe1034aca7b16ef222ecad981b790952474e5311b798c9eb6a677c82af4"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SupplySystem.cs", 6_456,
            "sha256:7ee0ec0b4eca1a78b4c6df5f6ba452b784bf09859ff24e6f50c681d16a63f1cb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_641,
            "sha256:a212275276ccff153d5df42a44a46ac8877afa485e315ee27d08767a909f29bb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_646,
            "sha256:40e6e7aa1eb89cb341c7e7a32471fa029024e49b261dce8a8926514109d727ba"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_154,
            "sha256:9462f609a4a7a4e062612e4058921b0c91931dc8ff7216dbe54e258cb59ec22c"),
    };

    private static readonly ArtifactPin[] NativeData =
    {
        new(NativeTemplatePath, NativeTemplateBytes, NativeTemplateSha256),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("C01", "epsimple-hvac-enums-base.compressor-values-order-lookup-string-and-conversion", "enum", "sha256:4b84056d792dc115be0625844d1ee81b63416861b9781a2ae1ce4be85349bb44", "sha256:8b67c867364b112d383744e8357daed43f93e75a32352ae0255474d93afc7f2d", new[] { "CompressorType", "CompressorType.RECIPROCATING", "CompressorType.SCREW", "CompressorType.TURBO", "CompressorType.__str__", "CompressorType.to_dragon" }),
        new("C02", "epsimple-hvac-enums-base.cooling-tower-control-values-order-string-and-lookup", "enum", "sha256:5b69b936cd8308c6b71198ec5b6c9fa0de0f5cafd783606e93ae1671102e2e6c", "sha256:d843b804c6f08d29c482f9eac898fd8e6a169770e6f64889aa2488acb6930bfc", new[] { "CoolingTowerControl", "CoolingTowerControl.SINGLESPEED", "CoolingTowerControl.TWOSPEED", "CoolingTowerControl.__str__" }),
        new("C03", "epsimple-hvac-enums-base.cooling-tower-type-values-order-string-and-lookup", "enum", "sha256:6f94330e40c55537f06af7c8516b12409db018611561834b0d83ec0f54c0d237", "sha256:43c5920065fa1fd0aa558b6f4f96ded8f8791494ff79899935d647d666407ab6", new[] { "CoolingTowerType", "CoolingTowerType.CLOSED", "CoolingTowerType.OPEN", "CoolingTowerType.__str__" }),
        new("F01", "epsimple-hvac-enums-base.fuel-values-order-lookup-string-and-conversion", "enum", "sha256:21276d5ad5c506764f380b100bae58206233aaea51e0c4267534fd4e9fa950d8", "sha256:9429dc4285592819464151aae71b8fe663bd60227020e08a3255e764c4c87394", new[] { "Fuel", "Fuel.DISTRICTHEATING", "Fuel.ELECTRICITY", "Fuel.LPG", "Fuel.NATURALGAS", "Fuel.OIL", "Fuel.__str__", "Fuel.to_dragon" }),
        new("N01", "epsimple-hvac-enums-base.none-source-singleton-id-new-and-conversion", "sentinel", "sha256:649b4f64a1aed7a8c45e179349352986650711a63534bb8ddf869277a64eed6f", "sha256:e92e8a5aa21f23c59ebab2ce092103d26ffa07d12ab3e91ed5e5d683ceacd5cb", new[] { "NoneSource", "NoneSource.ID", "NoneSource.__new__", "NoneSource.to_dragon" }),
        new("S01", "epsimple-hvac-enums-base.source-system-base-and-type-mapper-topology", "base", "sha256:7b8a645e61bdc99e1b6e1fe1770e59157d363a6af353b76afe13fb92d201034f", "sha256:db863d8ee217410e14ea263c8936b97a5085ce69d3b9debefb86f120b15da146", new[] { "SourceSystem", "SourceSystem.TYPE_MAPPER" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        Target(185, "CompressorType", "class", "epsimple-hvac-enums-base-185-8785ee6d", "equivalent", "not_applicable", "Dragons.SimpleDragon.CompressorType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        Target(186, "CompressorType.RECIPROCATING", "constant", "epsimple-hvac-enums-base-186-dfd51671", "equivalent", "not_applicable", "Dragons.SimpleDragon.CompressorType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        Target(187, "CompressorType.SCREW", "constant", "epsimple-hvac-enums-base-187-2947a213", "equivalent", "not_applicable", "Dragons.SimpleDragon.CompressorType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        Target(188, "CompressorType.TURBO", "constant", "epsimple-hvac-enums-base-188-5074351d", "equivalent", "not_applicable", "Dragons.SimpleDragon.CompressorType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        Target(189, "CompressorType.__str__", "function", "epsimple-hvac-enums-base-189-f40e4929", "exception", "grm-reader-writer-vocabulary-rather-than-native-enum-tostring-f40e4929", "Dragons.SimpleDragon.CompressorType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 0),
        Target(190, "CompressorType.to_dragon", "function", "epsimple-hvac-enums-base-190-bff3a12f", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 0),
        Target(191, "CoolingTowerControl", "class", "epsimple-hvac-enums-base-191-31f279b7", "equivalent", "not_applicable", "Dragons.SimpleDragon.CoolingTowerControl through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 1),
        Target(192, "CoolingTowerControl.SINGLESPEED", "constant", "epsimple-hvac-enums-base-192-536f3586", "equivalent", "not_applicable", "Dragons.SimpleDragon.CoolingTowerControl through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 1),
        Target(193, "CoolingTowerControl.TWOSPEED", "constant", "epsimple-hvac-enums-base-193-bc3d3c6f", "equivalent", "not_applicable", "Dragons.SimpleDragon.CoolingTowerControl through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 1),
        Target(194, "CoolingTowerControl.__str__", "function", "epsimple-hvac-enums-base-194-f40e4929", "exception", "grm-reader-writer-vocabulary-rather-than-native-enum-tostring-f40e4929", "Dragons.SimpleDragon.CoolingTowerControl through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 1),
        Target(195, "CoolingTowerType", "class", "epsimple-hvac-enums-base-195-9dd879be", "equivalent", "not_applicable", "Dragons.SimpleDragon.CoolingTowerType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 2),
        Target(196, "CoolingTowerType.CLOSED", "constant", "epsimple-hvac-enums-base-196-ec6ad133", "equivalent", "not_applicable", "Dragons.SimpleDragon.CoolingTowerType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 2),
        Target(197, "CoolingTowerType.OPEN", "constant", "epsimple-hvac-enums-base-197-0496e7cd", "equivalent", "not_applicable", "Dragons.SimpleDragon.CoolingTowerType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 2),
        Target(198, "CoolingTowerType.__str__", "function", "epsimple-hvac-enums-base-198-f40e4929", "exception", "grm-reader-writer-vocabulary-rather-than-native-enum-tostring-f40e4929", "Dragons.SimpleDragon.CoolingTowerType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 2),
        Target(240, "Fuel", "class", "epsimple-hvac-enums-base-240-66a9b58b", "equivalent", "not_applicable", "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 3),
        Target(241, "Fuel.DISTRICTHEATING", "constant", "epsimple-hvac-enums-base-241-806c9ca0", "equivalent", "not_applicable", "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 3),
        Target(242, "Fuel.ELECTRICITY", "constant", "epsimple-hvac-enums-base-242-dece9e85", "equivalent", "not_applicable", "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 3),
        Target(243, "Fuel.LPG", "constant", "epsimple-hvac-enums-base-243-c70f84e9", "equivalent", "not_applicable", "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 3),
        Target(244, "Fuel.NATURALGAS", "constant", "epsimple-hvac-enums-base-244-50160788", "equivalent", "not_applicable", "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 3),
        Target(245, "Fuel.OIL", "constant", "epsimple-hvac-enums-base-245-24bb42a1", "equivalent", "not_applicable", "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 3),
        Target(246, "Fuel.__str__", "function", "epsimple-hvac-enums-base-246-f40e4929", "exception", "grm-reader-writer-vocabulary-rather-than-native-enum-tostring-f40e4929", "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)", 3),
        Target(247, "Fuel.to_dragon", "function", "epsimple-hvac-enums-base-247-7ce39626", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 3),
        Target(267, "NoneSource", "class", "epsimple-hvac-enums-base-267-8824a756", "exception", "nullable-resolved-source-reference-rather-than-singleton-sentinel-8824a756", "Dragons.SimpleDragon.SupplySystem.SourceSystem nullable reference with Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 4),
        Target(268, "NoneSource.ID", "constant", "epsimple-hvac-enums-base-268-dbf0ef4b", "exception", "null-source-reference-rather-than-special-string-identifier-dbf0ef4b", "Dragons.SimpleDragon.SupplySystem.SourceSystem nullable reference with Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 4),
        Target(269, "NoneSource.__new__", "function", "epsimple-hvac-enums-base-269-758d9c0b", "exception", "nullable-source-state-rather-than-process-global-singleton-758d9c0b", "Dragons.SimpleDragon.SupplySystem.SourceSystem nullable reference with Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 4),
        Target(270, "NoneSource.to_dragon", "function", "epsimple-hvac-enums-base-270-c8347dc8", "exception", "aggregate-converter-diagnostic-for-unresolved-source-rather-than-null-return-c8347dc8", "Dragons.SimpleDragon.SupplySystem.SourceSystem nullable reference with Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 4),
        Target(319, "SourceSystem", "class", "epsimple-hvac-enums-base-319-9b6905f8", "exception", "sealed-validated-domain-aggregate-rather-than-empty-python-base-9b6905f8", "Dragons.SimpleDragon.SourceSystem constructor and public properties", 5),
        Target(320, "SourceSystem.TYPE_MAPPER", "constant", "epsimple-hvac-enums-base-320-813567e3", "exception", "grm-reader-enum-dispatch-rather-than-public-mutable-class-map-813567e3", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) with Dragons.SimpleDragon.SourceSystemType", 5),
    };

    private static readonly int[] ExcludedIndices =
    {
        137, 138, 140, 141, 149, 150, 152, 153, 159, 160, 162, 163,
        172, 173, 175, 176, 201, 202, 204, 205, 211, 212, 214, 215,
        221, 222, 224, 225, 232, 233, 235, 236, 249, 250, 255, 256,
        258, 259, 273, 274, 276, 277, 285, 286, 288, 289, 298, 299,
        301, 302, 310, 311, 313, 314, 327, 328, 330, 331,
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    [
        new(13, "sha256:232f9488b166453f4477c1e7369ccbcef67e29a8fabcb4d4ef8795131310170f"),
        new(12, "sha256:aa79f09a717743d12fd05df60d867bb8e3b35b9b46a2373c4f2a684bb7e507a3"),
        new(12, "sha256:36e85ca34bb064eb7a98a0b667cbeb2715e42d03efd6398ed3b09516e3a9a32c"),
        new(13, "sha256:80e4f481b40e563cbb9e96d6d30900492045912fe46ebbf7cd26568a66e04927"),
        new(18, "sha256:8fb86034623af13485f6e3c1ceca11bd4f20e7ee9cefad4eec264dec69dde85c"),
        new(19, "sha256:497f537052d43ce449a6183af3ce192b1e0281de06864013ea9698680965f10f"),
    ];

    private static readonly string[] ExpectedReceiptHashes =
    [
        "sha256:b3304bfd51258c9830e1b07a3d866f4a2e9115bdd4fb845b7dab1e57ecc56e0d",
        "sha256:01e820f9c9cab079a01f8a63b20014f3064781ad7ec8c1c805ca0508ce278fb6",
        "sha256:861fcfd5cf685607ff5d937c9ad22b710bedcaf11b151714d5bf3dfe07d737e8",
        "sha256:9dca92f49773fda70884cabaaa654b963562d89a74e27631b915812089a011cf",
        "sha256:48db1a21bf06d06375875ede8cd989c0bbf1711e0d190a79ebb5949d88408eb1",
        "sha256:b40802687488999039418378be6dda3438254a9c0ee7887c88f7931523f46fa7",
        "sha256:aca226140f15a638640b993715b484c98b07442bef083d1307990dfd58d12ff5",
        "sha256:c48b4fb90cfe7659aa988adb1055a5f1679eb92c55ef72db5b81fafa26e7df00",
        "sha256:dc19345f092277132125b378409983518f46477824756d40d02b12b94d8b2dbc",
        "sha256:723eb8a03cf65363b07ffc969e758945cefc64b11ad04897191bc35f5960881f",
        "sha256:fbcd33682ad444a0e9e9891fb53bb80787e7da26723ac9368e4ad0830d81a941",
        "sha256:1ad67920ae0ced1096a2eaf488b255674a70130ade98f11d294185d5adabb505",
        "sha256:d881a5f57ae80ac03a085ceba156d8fba7f114ffec174a368252def3e25ff439",
        "sha256:f709453d33066fc8d11350e09aaf7ebf1e1992d41a305752d9015b3deefea40b",
        "sha256:44fc8866ee98659cce4f6e9a838f4e0e96cb44a810839f286f24734bae92771d",
        "sha256:53b380e79772e624e37dce4b38f1318005293489134de01b70b88d05edddbf42",
        "sha256:908de0effdd0bc546c2ada4cf8d003a475b00161a612a0d8651e8098ab749cdf",
        "sha256:9e4fb41f0c839ff0509e6e2fb1401d2180c3847cd6f24d9a6b41e53aea577ab9",
        "sha256:e721746ac1a031a1292309306feb8d3ddcd8aeaead598f7cac3a56cf89432092",
        "sha256:009dc2350e31ec28ac86ef9e37f5b6a328b6412a0f1240f42745f362871ac1fe",
        "sha256:765c2a96148644ff0ae9ca56f0ead7af84c0b7c9ad2df6a624382574d7a4439d",
        "sha256:27a334d5cd9f033e8a979a0b27e6528fe6d26604e4b14b1ff04a347eae9df79b",
        "sha256:64485bcc1779f90f27c8778ca88e6856c3e81b06b52e80f06eb87e769d89d03f",
        "sha256:c304f85ece0a155a6efeb88b99b670054422ce3635c76e975823250ab3af9f05",
        "sha256:a7f936a94a99f5e1563c4d1733f5a312e231ab2d08851724b6685baaa431dc8a",
        "sha256:268d54779caf59001a240777d713f71199df82a8b08d8a3fb08574344ae5ffa0",
        "sha256:3ef204c6bd6f7fda57e9853030088e3cb1c4c40000ad09c0f8634afa8ac03019",
        "sha256:4ea768e99c7be9aa25e5f654e794cf73d6324f1656e19053e51b37eded889d2e",
    ];


    private static readonly string[] ExpectedCollectorOutputHashes =
    [
        "sha256:ff42b93dc720fe56d23f373ff2338a0b096a9d0346bc41da1b2f8d6576088722", // epsimple-hvac-enums-base-185-8785ee6d
        "sha256:029002e4edbdbc5a097d061e80954de6518871671e22e58ef1a07b97152872e5", // epsimple-hvac-enums-base-186-dfd51671
        "sha256:97116b9b0daa0d90013c9f9fbf35012a1297f7b815612ddce66b9e9d5f7a8ad7", // epsimple-hvac-enums-base-187-2947a213
        "sha256:b4ba0dec2d077164093d73e07f0259b5b6005cf8368e0801d7fca66e5ea57d85", // epsimple-hvac-enums-base-188-5074351d
        "sha256:093d1fa484d1889c6de5bd5fed46cda4cf6363b0167399b8cc1780f79efeed16", // epsimple-hvac-enums-base-189-f40e4929
        "sha256:a3fbcb66d3c9bd79e59f7fd3ca638b96f0c6364a2b96d40357b9ea13d1924e98", // epsimple-hvac-enums-base-190-bff3a12f
        "sha256:b4212508c15390b1028e99cff24d6138b3858e1f6f39f3825dd98d5cc3dd044d", // epsimple-hvac-enums-base-191-31f279b7
        "sha256:2bb4371f382c09ec8dbadd9ede836dd6fca887914646d7545de2604f087acac1", // epsimple-hvac-enums-base-192-536f3586
        "sha256:fd926c5e140a77c098ee1fedf711bea9ec39475c0981ef543995175f28dd762e", // epsimple-hvac-enums-base-193-bc3d3c6f
        "sha256:87e74c36596fb00877620326ed4f92fa68a81a7b69ceeee306ba8795b69ed074", // epsimple-hvac-enums-base-194-f40e4929
        "sha256:bd7293ac24d6d2a401b29938252606cdd464f561acf9041f24fbad9c234bc5df", // epsimple-hvac-enums-base-195-9dd879be
        "sha256:97df0cfdeb29aa113b3811f25deab012aaa518e37f4211fd4a307ef29c4e23ca", // epsimple-hvac-enums-base-196-ec6ad133
        "sha256:2068944b8623df427e6142efff83c690b14241de047d11402f2dc04142f04649", // epsimple-hvac-enums-base-197-0496e7cd
        "sha256:4d41186e73c704a2d358dfd5920dbbf4de3ccaea935ebe20be40abbf68287ed9", // epsimple-hvac-enums-base-198-f40e4929
        "sha256:19ccdb2528ac7448223a292db4181520f2197d170415931c17e4fdee7b11a77d", // epsimple-hvac-enums-base-240-66a9b58b
        "sha256:4b4df78a249eb53662c9b5e3872c081ca21e7909baa7e0bb1cb1355d3bef7100", // epsimple-hvac-enums-base-241-806c9ca0
        "sha256:965fee639e9c7e4b9ef3bf5ab029984458a1a88e6a3bc3e574dcc973dd842231", // epsimple-hvac-enums-base-242-dece9e85
        "sha256:e3a0a5720836995f0b341f7d1c2728d55c02435bd249d2ea90f05eddd9b99b73", // epsimple-hvac-enums-base-243-c70f84e9
        "sha256:313ac71a9734f0e1ba9e4641b052bb8d4c55776c1fcfa38b5615beade486ee98", // epsimple-hvac-enums-base-244-50160788
        "sha256:ab2fecc484b647f3766d3e90718f5df3b5051f8872632023d146c5e6bc461c17", // epsimple-hvac-enums-base-245-24bb42a1
        "sha256:64a1d8fd9d0f2fa904e207856206c8d285fa934c74034f9383e581fbbd484249", // epsimple-hvac-enums-base-246-f40e4929
        "sha256:5fe8fae4228ec06d28d43045d9356454d5d3e77ce9c33cf84922af0d85290dff", // epsimple-hvac-enums-base-247-7ce39626
        "sha256:6eac343c924897da05b3167c717105b48285b2715a8aef65a6d77b5aa7bdd83f", // epsimple-hvac-enums-base-267-8824a756
        "sha256:3eb4879406fcec61c010a1595bb333814d600b7c36f107b83b7cc92a623f7eb8", // epsimple-hvac-enums-base-268-dbf0ef4b
        "sha256:efe87cc72435a169ab081d338403e88d901f9df8892e183edf0aa6be2cdbe58b", // epsimple-hvac-enums-base-269-758d9c0b
        "sha256:88650908529c780b45b9eef13958a4328f13be47e7861265b3e16c32bfb8be29", // epsimple-hvac-enums-base-270-c8347dc8
        "sha256:c4a74c681e2ea29f9acaca04cfe8b1cb0f42c5f666a140b45447642e3f59ee92", // epsimple-hvac-enums-base-319-9b6905f8
        "sha256:f28b42be08e088512c1a7b863b4d01b9e782d834eb84c0240c8f570c38725e23", // epsimple-hvac-enums-base-320-813567e3
    ];

    [Fact]
    public void MatchesPinnedHvacEnumsBaseThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(6, observations.Length);
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
                "HVAC_ENUMS_BASE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(28, recordCount);
        Assert.Equal(28, corpus.Targets.Length);
        Assert.Equal(28, corpus.Targets.Select(item => item.AssertionId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(18, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(10, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.Equal(6, corpus.FixtureCases.Length);
        Assert.Equal(116, corpus.DeferredCount);
        Assert.Equal(58, corpus.ExcludedCount);
    }

    private static ExpectedTargetBinding Target(
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
        foreach (ArtifactPin artifact in NativeSources.Concat(NativeData))
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        Assert.True(typeof(SourceSystem).IsSealed);
        Assert.True(typeof(SupplySystem).IsSealed);
        Assert.True(typeof(FuelType).IsEnum);
        Assert.True(typeof(CompressorType).IsEnum);
        Assert.True(typeof(CoolingTowerType).IsEnum);
        Assert.True(typeof(CoolingTowerControl).IsEnum);
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.Type), typeof(SourceSystemType));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.FuelType), typeof(FuelType?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CompressorType), typeof(CompressorType?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CoolingTowerType), typeof(CoolingTowerType?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CoolingTowerControl), typeof(CoolingTowerControl?));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.SourceSystem), typeof(SourceSystem));
        Assert.Null(typeof(SourceSystem).GetField("TYPE_MAPPER", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(SourceSystem).GetProperty("TYPE_MAPPER", BindingFlags.Public | BindingFlags.Static));
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
        string text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
        byte[] tampered = (byte[])bytes.Clone();
        tampered[64] ^= 1;
        Assert.NotEqual(FixtureSha256, Sha256(tampered));
        using JsonDocument duplicate = JsonDocument.Parse("{\"value\":1,\"value\":2}");
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertUniqueObjectKeysRecursive(duplicate.RootElement));
        Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(
            "{\"value\":1,}",
            new JsonDocumentOptions { AllowTrailingCommas = false }));
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
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "deferred_receipts",
            "excluded_receipts",
            "fact_sha256",
            "native_audit",
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
        ValidateNativeAudit(root.GetProperty("native_audit"));

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
        (int deferred, int excluded) = ValidateNonTargets(root, targets);
        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol),
            fixtureCases.SelectMany(item => ReadStringArray(item.GetProperty("target_symbols"))));
        return new OracleCorpus(fixtureCases, targets, deferred, excluded);
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "bootstrap",
            "dependencies",
            "dependencies_sha256",
            "implementation",
            "platform",
            "pointer_width_bits",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version",
            "strict_json_support");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("win32", RequiredString(runtime, "platform"));
        Assert.Equal(64, runtime.GetProperty("pointer_width_bits").GetInt32());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(DependenciesSha256, RequiredString(runtime, "dependencies_sha256"));
        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(dependencies, "eppy", "numpy", "pandas", "python-dateutil", "pytz", "six", "tzdata");
        Assert.Equal("0.5.63", RequiredString(dependencies, "eppy"));
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("2.9.0.post0", RequiredString(dependencies, "python-dateutil"));
        Assert.Equal("2024.2", RequiredString(dependencies, "pytz"));
        Assert.Equal("1.16.0", RequiredString(dependencies, "six"));
        Assert.Equal("2024.2", RequiredString(dependencies, "tzdata"));
        Assert.Equal(DependenciesSha256, CanonicalSha256(dependencies));
        ValidateArtifactProjection(
            runtime.GetProperty("bootstrap"),
            "tools/python-reference/bootstrap_reference.py",
            1_232,
            "sha256:0674dcf1fe966de2a4b873a360ef67be48d74f38ba80adba9c74405fd9be7e0f");
        ValidateArtifactProjection(
            runtime.GetProperty("strict_json_support"),
            "tools/python-reference/generate_schedule_type_oracle.py",
            21_108,
            "sha256:555a1df41e5369dbbc44b0729a48673610a86951a215c8e2aa00cfa4fce156f1");
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(
            upstream,
            "commit",
            "inventory_sha256",
            "isolated_import",
            "path",
            "resource_receipts",
            "resource_receipts_sha256",
            "source");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Empty(upstream.GetProperty("resource_receipts").EnumerateArray());
        Assert.Equal(EmptyResourcesSha256, RequiredString(upstream, "resource_receipts_sha256"));
        Assert.Equal(EmptyResourcesSha256, CanonicalSha256(upstream.GetProperty("resource_receipts")));

        JsonElement source = upstream.GetProperty("source");
        AssertKeys(source, "ast_sha256", "bytes", "source_sha256");
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement isolated = upstream.GetProperty("isolated_import");
        AssertKeys(
            isolated,
            "epsimple_core_initializer_executed",
            "epsimple_package_initializer_executed",
            "loaded_local_modules",
            "loaded_local_modules_sha256",
            "relocation_snapshot_sha256",
            "source_location_count");
        Assert.False(isolated.GetProperty("epsimple_package_initializer_executed").GetBoolean());
        Assert.False(isolated.GetProperty("epsimple_core_initializer_executed").GetBoolean());
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal(LoadedSourcesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(RelocationSnapshotSha256, RequiredString(isolated, "relocation_snapshot_sha256"));
        JsonElement[] modules = isolated.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(16, modules.Length);
        Assert.Equal(16, modules.Select(item => RequiredString(item, "module"))
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(modules, item =>
        {
            AssertKeys(item, "ast_sha256", "bytes", "module", "path", "source_sha256");
            Assert.True(item.GetProperty("bytes").GetInt32() > 0);
            AssertSha256(RequiredString(item, "ast_sha256"));
            AssertSha256(RequiredString(item, "source_sha256"));
        });
        Assert.Equal(LoadedSourcesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        JsonElement loadedHvac = Assert.Single(modules, item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("epsimple.core.hvac", RequiredString(loadedHvac, "module"));
        Assert.Equal(UpstreamBytes, loadedHvac.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(loadedHvac, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(loadedHvac, "ast_sha256"));
    }

    private static void ValidateNativeAudit(JsonElement audit)
    {
        AssertKeys(
            audit,
            "public_production_routes_only",
            "routes_sha256",
            "source_receipts",
            "source_receipts_sha256");
        Assert.True(audit.GetProperty("public_production_routes_only").GetBoolean());
        Assert.Equal(NativeRoutesSha256, RequiredString(audit, "routes_sha256"));
        Assert.Equal(NativeSourcesSha256, RequiredString(audit, "source_receipts_sha256"));
        AssertArtifactArray(audit.GetProperty("source_receipts"), NativeSources);
        Assert.Equal(NativeSourcesSha256, CanonicalSha256(audit.GetProperty("source_receipts")));
        Assert.Equal(NativeAuditSha256, CanonicalSha256(audit));
    }

    private static void ValidateCase(
        JsonElement item,
        CaseBinding expected,
        JsonElement caseHashes,
        JsonElement factHashes)
    {
        AssertKeys(
            item,
            "assertion_ids",
            "category",
            "code",
            "context_symbols",
            "id",
            "python",
            "target_symbols");
        Assert.Equal(expected.Code, RequiredString(item, "code"));
        Assert.Equal(expected.CaseId, RequiredString(item, "id"));
        Assert.Equal(expected.Category, RequiredString(item, "category"));
        AssertStringArray(item.GetProperty("target_symbols"), expected.TargetSymbols);
        Assert.Empty(item.GetProperty("context_symbols").EnumerateArray());
        JsonElement assertionIds = item.GetProperty("assertion_ids");
        AssertKeys(assertionIds, expected.TargetSymbols);
        foreach (string symbol in expected.TargetSymbols)
        {
            ExpectedTargetBinding target = Assert.Single(ExpectedTargets, item => item.Symbol == symbol);
            Assert.Equal(target.AssertionId, RequiredString(assertionIds, symbol));
        }

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
            foreach (string hashName in new[] { "symbol_hash", "signature_hash", "body_hash" })
            {
                Assert.Equal(RequiredString(inventorySymbol, hashName), RequiredString(receipt, hashName));
                Assert.Equal(RequiredString(receipt, hashName), RequiredString(descriptor, hashName));
            }

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

        Assert.Equal(
            new[]
            {
                185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196,
                197, 198, 240, 241, 242, 243, 244, 245, 246, 247, 267, 268,
                269, 270, 319, 320,
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

    private static void ValidateConsumerContract(
        JsonElement contract,
        IReadOnlyList<TargetBinding> targets)
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
            "evidence_contract",
            "expectations",
            "native_routes",
            "runtime_names",
            "runtime_signatures",
            "target_symbols");
        Assert.Equal(6, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        AssertStringArray(contract.GetProperty("target_symbols"), ExpectedTargets.Select(item => item.Symbol));
        Assert.Equal(
            "pinned-python-only-no-native-runtime-name-claims",
            RequiredString(contract, "runtime_names"));

        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(18, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(10, counts.GetProperty("exception").GetInt32());
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement nativeRoutes = contract.GetProperty("native_routes");
        JsonElement expectations = contract.GetProperty("expectations");
        JsonElement signatures = contract.GetProperty("runtime_signatures");
        AssertKeys(assertions, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(classifications, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(nativeRoutes, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(expectations, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(signatures, ExpectedTargets.Select(item => item.Symbol).ToArray());
        Assert.Equal(RuntimeSignaturesSha256, CanonicalSha256(signatures));
        string[] exceptionSymbols = ExpectedTargets
            .Where(item => item.Classification == "exception")
            .Select(item => item.Symbol)
            .ToArray();
        AssertKeys(adaptations, exceptionSymbols);
        foreach (TargetBinding target in targets)
        {
            Assert.Equal(target.AssertionId, RequiredString(assertions, target.Symbol));
            Assert.Equal(target.Classification, RequiredString(classifications, target.Symbol));
            Assert.Equal(target.NativeRoute, RequiredString(nativeRoutes, target.Symbol));
            JsonElement expectation = expectations.GetProperty(target.Symbol);
            AssertKeys(expectation, "adaptation", "assertion_id", "classification", "native_route");
            Assert.Equal(target.AdaptationId, RequiredString(expectation, "adaptation"));
            Assert.Equal(target.AssertionId, RequiredString(expectation, "assertion_id"));
            Assert.Equal(target.Classification, RequiredString(expectation, "classification"));
            Assert.Equal(target.NativeRoute, RequiredString(expectation, "native_route"));
            if (target.Classification == "exception")
            {
                Assert.Equal(target.AdaptationId, RequiredString(adaptations, target.Symbol));
            }

            Assert.Contains("Dragons.SimpleDragon", target.NativeRoute, StringComparison.Ordinal);
            Assert.DoesNotContain(".Internal", target.NativeRoute, StringComparison.Ordinal);
            Assert.DoesNotContain("GrmVocabulary", target.NativeRoute, StringComparison.Ordinal);
        }

        Assert.Equal(NativeRoutesSha256, CanonicalSha256(nativeRoutes));
        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "deferred_count",
            "exact_one_case_target_partition",
            "excluded_count",
            "full_source_partition",
            "source_declaration_count",
            "target_count",
            "target_indices");
        Assert.Equal(116, closure.GetProperty("deferred_count").GetInt32());
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.Equal(58, closure.GetProperty("excluded_count").GetInt32());
        Assert.True(closure.GetProperty("full_source_partition").GetBoolean());
        Assert.Equal(202, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(28, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex),
            closure.GetProperty("target_indices").EnumerateArray().Select(item => item.GetInt32()));
        JsonElement evidence = contract.GetProperty("evidence_contract");
        AssertKeys(
            evidence,
            "active_energyplus_process_claim",
            "expected_receipt_count",
            "full_hvac_declaration_parity_claim",
            "native_runtime_executed_by_python_oracle",
            "python_behavior_oracle_only",
            "relocatable_import_claim");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.Equal(28, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.False(evidence.GetProperty("full_hvac_declaration_parity_claim").GetBoolean());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("python_behavior_oracle_only").GetBoolean());
        Assert.True(evidence.GetProperty("relocatable_import_claim").GetBoolean());
    }

    private static (int Deferred, int Excluded) ValidateNonTargets(
        JsonElement root,
        IReadOnlyList<TargetBinding> targets)
    {
        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        using JsonDocument inventoryDocument = JsonDocument.Parse(inventoryBytes);
        JsonElement inventorySymbols = inventoryDocument.RootElement.GetProperty("symbols");
        JsonElement[] deferred = root.GetProperty("deferred_receipts").EnumerateArray().ToArray();
        JsonElement[] excluded = root.GetProperty("excluded_receipts").EnumerateArray().ToArray();
        Assert.Equal(116, deferred.Length);
        Assert.Equal(58, excluded.Length);
        Assert.Equal(ExcludedIndices, excluded.Select(item => item.GetProperty("inventory_index").GetInt32()));
        Assert.Equal(116, deferred.Select(item => item.GetProperty("inventory_index").GetInt32())
            .Distinct().Count());
        foreach (JsonElement receipt in deferred.Concat(excluded))
        {
            int index = receipt.GetProperty("inventory_index").GetInt32();
            AssertNonTargetReceipt(receipt, inventorySymbols[index], index);
        }

        int[] allIndices = targets.Select(item => item.InventoryIndex)
            .Concat(deferred.Select(item => item.GetProperty("inventory_index").GetInt32()))
            .Concat(excluded.Select(item => item.GetProperty("inventory_index").GetInt32()))
            .OrderBy(item => item)
            .ToArray();
        Assert.Equal(Enumerable.Range(135, 202), allIndices);
        int[] sourceIndices = inventorySymbols.EnumerateArray()
            .Select((item, index) => (item, index))
            .Where(pair => RequiredString(pair.item, "path") == UpstreamPath)
            .Select(pair => pair.index)
            .ToArray();
        Assert.Equal(Enumerable.Range(135, 202), sourceIndices);
        return (deferred.Length, excluded.Length);
    }

    private static void AssertNonTargetReceipt(
        JsonElement receipt,
        JsonElement inventory,
        int index)
    {
        AssertKeys(receipt, "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
        Assert.Equal(index, receipt.GetProperty("inventory_index").GetInt32());
        foreach (string name in new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" })
        {
            Assert.Equal(RequiredString(inventory, name), RequiredString(receipt, name));
        }

        Assert.Equal(UpstreamPath, RequiredString(receipt, "path"));
    }

    private static NativeObservation ObserveNativeCase(int index)
    {
        string[] facts = index switch
        {
            0 => ObserveCompressor(),
            1 => ObserveCoolingTowerControl(),
            2 => ObserveCoolingTowerType(),
            3 => ObserveFuel(),
            4 => ObserveNullableSource(),
            5 => ObserveSourceSystem(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return new NativeObservation(
            Cases[index].Code,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveCompressor()
    {
        CompressorType[] values = Enum.GetValues<CompressorType>();
        var tokens = new List<string>();
        var reread = new List<string>();
        var dragon = new List<string>();
        var towerTypes = new List<string>();
        var stableWrites = new List<string>();
        foreach (CompressorType value in values)
        {
            SourceSystem source = Chiller(
                "SOURCE-COMPRESSOR-" + value,
                value,
                CoolingTowerType.Open,
                CoolingTowerControl.SingleSpeed);
            SupplySystem supply = FanCoil("SUPPLY-COMPRESSOR-" + value, source);
            GreenRetrofitModel model = CreateModel(new[] { source }, new[] { supply }, supply);
            string json = GrmWriter.Serialize(model);
            tokens.Add(SourceToken(json, "chiller", "compressor_type"));
            reread.Add(Assert.Single(GrmReader.Read(json).RequireModel().SourceSystems).CompressorType!.Value.ToString());
            GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
            Assert.True(conversion.Success, Describe(conversion.Diagnostics));
            DragonChiller converted = Assert.IsType<DragonChiller>(OnlySupply(conversion).Source);
            dragon.Add(converted.Compressor.ToString());
            towerTypes.Add(converted.CoolingTower.GetType().Name);
            stableWrites.Add(Boolean(json == GrmWriter.Serialize(model)));
        }

        SourceSystem invalidProbe = Chiller(
            "SOURCE-COMPRESSOR-INVALID-JSON",
            CompressorType.Turbo,
            CoolingTowerType.Open,
            CoolingTowerControl.SingleSpeed);
        string invalidJson = ReplaceRequired(
            GrmWriter.Serialize(CreateModel(new[] { invalidProbe })),
            "\"compressor_type\": \"turbo\"",
            "\"compressor_type\": \"invalid\"");
        return new[]
        {
            "enum.names=" + Join(values.Select(item => item.ToString())),
            "enum.numeric=" + Join(values.Select(item => ((int)item).ToString(CultureInfo.InvariantCulture))),
            "enum.defined=" + Join(values.Select(item => Boolean(Enum.IsDefined(item)))),
            "grm.tokens=" + Join(tokens),
            "reader.values=" + Join(reread),
            "writer.repeat_equal=" + Join(stableWrites),
            "dragon.compressor=" + Join(dragon),
            "dragon.tower_types=" + Join(towerTypes),
            "invalid.enum_defined=" + Boolean(Enum.IsDefined((CompressorType)int.MaxValue)),
            "invalid.constructor=" + ExceptionFact(() => _ = Chiller(
                "SOURCE-COMPRESSOR-INVALID",
                (CompressorType)int.MaxValue,
                CoolingTowerType.Open,
                CoolingTowerControl.SingleSpeed)),
            "invalid.reader.codes=" + DiagnosticCodes(GrmReader.Read(invalidJson)),
            "native.enum_type=" + typeof(CompressorType).FullName,
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveCoolingTowerControl()
    {
        CoolingTowerControl[] values = Enum.GetValues<CoolingTowerControl>();
        var tokens = new List<string>();
        var reread = new List<string>();
        var dragon = new List<string>();
        foreach (CoolingTowerControl value in values)
        {
            SourceSystem source = Chiller(
                "SOURCE-CONTROL-" + value,
                CompressorType.Turbo,
                CoolingTowerType.Open,
                value);
            SupplySystem supply = FanCoil("SUPPLY-CONTROL-" + value, source);
            GreenRetrofitModel model = CreateModel(new[] { source }, new[] { supply }, supply);
            string json = GrmWriter.Serialize(model);
            tokens.Add(SourceToken(json, "chiller", "coolingtower_control"));
            reread.Add(Assert.Single(GrmReader.Read(json).RequireModel().SourceSystems).CoolingTowerControl!.Value.ToString());
            GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
            Assert.True(conversion.Success, Describe(conversion.Diagnostics));
            DragonChiller converted = Assert.IsType<DragonChiller>(OnlySupply(conversion).Source);
            dragon.Add(converted.CoolingTower.GetType().Name);
        }

        SourceSystem invalidProbe = Chiller(
            "SOURCE-CONTROL-INVALID-JSON",
            CompressorType.Turbo,
            CoolingTowerType.Open,
            CoolingTowerControl.SingleSpeed);
        string invalidJson = ReplaceRequired(
            GrmWriter.Serialize(CreateModel(new[] { invalidProbe })),
            "\"coolingtower_control\": \"single-speed\"",
            "\"coolingtower_control\": \"invalid\"");
        return new[]
        {
            "enum.names=" + Join(values.Select(item => item.ToString())),
            "enum.numeric=" + Join(values.Select(item => ((int)item).ToString(CultureInfo.InvariantCulture))),
            "enum.defined=" + Join(values.Select(item => Boolean(Enum.IsDefined(item)))),
            "grm.tokens=" + Join(tokens),
            "reader.values=" + Join(reread),
            "dragon.tower_types=" + Join(dragon),
            "invalid.enum_defined=" + Boolean(Enum.IsDefined((CoolingTowerControl)int.MaxValue)),
            "invalid.constructor=" + ExceptionFact(() => _ = Chiller(
                "SOURCE-CONTROL-INVALID",
                CompressorType.Turbo,
                CoolingTowerType.Open,
                (CoolingTowerControl)int.MaxValue)),
            "invalid.reader.codes=" + DiagnosticCodes(GrmReader.Read(invalidJson)),
            "native.enum_type=" + typeof(CoolingTowerControl).FullName,
            "native.tostring=" + Join(values.Select(item => item.ToString())),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveCoolingTowerType()
    {
        CoolingTowerType[] values = Enum.GetValues<CoolingTowerType>();
        var tokens = new List<string>();
        var reread = new List<string>();
        var dragon = new List<string>();
        foreach (CoolingTowerType value in values)
        {
            SourceSystem source = Chiller(
                "SOURCE-TOWER-" + value,
                CompressorType.Screw,
                value,
                CoolingTowerControl.SingleSpeed);
            SupplySystem supply = FanCoil("SUPPLY-TOWER-" + value, source);
            GreenRetrofitModel model = CreateModel(new[] { source }, new[] { supply }, supply);
            string json = GrmWriter.Serialize(model);
            tokens.Add(SourceToken(json, "chiller", "coolingtower_type"));
            reread.Add(Assert.Single(GrmReader.Read(json).RequireModel().SourceSystems).CoolingTowerType!.Value.ToString());
            GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
            Assert.True(conversion.Success, Describe(conversion.Diagnostics));
            DragonChiller converted = Assert.IsType<DragonChiller>(OnlySupply(conversion).Source);
            dragon.Add(converted.CoolingTower.GetType().Name);
        }

        SourceSystem invalidProbe = Chiller(
            "SOURCE-TOWER-INVALID-JSON",
            CompressorType.Turbo,
            CoolingTowerType.Closed,
            CoolingTowerControl.SingleSpeed);
        string invalidJson = ReplaceRequired(
            GrmWriter.Serialize(CreateModel(new[] { invalidProbe })),
            "\"coolingtower_type\": \"closed\"",
            "\"coolingtower_type\": \"invalid\"");
        return new[]
        {
            "enum.names=" + Join(values.Select(item => item.ToString())),
            "enum.numeric=" + Join(values.Select(item => ((int)item).ToString(CultureInfo.InvariantCulture))),
            "enum.defined=" + Join(values.Select(item => Boolean(Enum.IsDefined(item)))),
            "grm.tokens=" + Join(tokens),
            "reader.values=" + Join(reread),
            "dragon.tower_types=" + Join(dragon),
            "invalid.enum_defined=" + Boolean(Enum.IsDefined((CoolingTowerType)int.MaxValue)),
            "invalid.constructor=" + ExceptionFact(() => _ = Chiller(
                "SOURCE-TOWER-INVALID",
                CompressorType.Turbo,
                (CoolingTowerType)int.MaxValue,
                CoolingTowerControl.SingleSpeed)),
            "invalid.reader.codes=" + DiagnosticCodes(GrmReader.Read(invalidJson)),
            "native.enum_type=" + typeof(CoolingTowerType).FullName,
            "native.tostring=" + Join(values.Select(item => item.ToString())),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveFuel()
    {
        FuelType[] values = Enum.GetValues<FuelType>();
        var tokens = new List<string>();
        var reread = new List<string>();
        var dragon = new List<string>();
        foreach (FuelType value in values)
        {
            var source = new SourceSystem(
                "fuel " + value,
                SourceSystemType.HeatPump,
                value,
                id: new EntityId("SOURCE-FUEL-" + value));
            SupplySystem supply = AirHandler("SUPPLY-FUEL-" + value, source);
            GreenRetrofitModel model = CreateModel(new[] { source }, new[] { supply }, supply);
            string json = GrmWriter.Serialize(model);
            tokens.Add(SourceToken(json, "heatpump", "fuel_type"));
            reread.Add(Assert.Single(GrmReader.Read(json).RequireModel().SourceSystems).FuelType!.Value.ToString());
            GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
            Assert.True(conversion.Success, Describe(conversion.Diagnostics));
            DragonAirHandlingUnit convertedSupply = Assert.IsType<DragonAirHandlingUnit>(OnlySupply(conversion));
            DragonHeatPump convertedSource = Assert.IsType<DragonHeatPump>(convertedSupply.Source);
            dragon.Add(convertedSource.Fuel.ToString());
        }

        var invalidProbe = new SourceSystem(
            "fuel invalid json",
            SourceSystemType.HeatPump,
            FuelType.Electricity,
            id: new EntityId("SOURCE-FUEL-INVALID-JSON"));
        string invalidJson = ReplaceRequired(
            GrmWriter.Serialize(CreateModel(new[] { invalidProbe })),
            "\"fuel_type\": \"electricity\"",
            "\"fuel_type\": \"invalid\"");
        return new[]
        {
            "enum.names=" + Join(values.Select(item => item.ToString())),
            "enum.numeric=" + Join(values.Select(item => ((int)item).ToString(CultureInfo.InvariantCulture))),
            "enum.defined=" + Join(values.Select(item => Boolean(Enum.IsDefined(item)))),
            "grm.tokens=" + Join(tokens),
            "reader.values=" + Join(reread),
            "dragon.fuels=" + Join(dragon),
            "invalid.enum_defined=" + Boolean(Enum.IsDefined((FuelType)int.MaxValue)),
            "invalid.constructor=" + ExceptionFact(() => _ = new SourceSystem(
                "fuel invalid",
                SourceSystemType.HeatPump,
                (FuelType)int.MaxValue)),
            "invalid.reader.codes=" + DiagnosticCodes(GrmReader.Read(invalidJson)),
            "native.enum_type=" + typeof(FuelType).FullName,
            "native.tostring=" + Join(values.Select(item => item.ToString())),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
            "district_heat_dragon=OtherFuel1",
        };
    }

    private static string[] ObserveNullableSource()
    {
        var packaged = new SupplySystem(
            "packaged",
            SupplySystemType.PackagedAirConditioner,
            coolingCop: 4.75d,
            coolingCapacity: 18_000d,
            id: new EntityId("SUPPLY-NULL-PACKAGED"));
        GreenRetrofitModel model = CreateModel(Array.Empty<SourceSystem>(), new[] { packaged }, packaged);
        string json = GrmWriter.Serialize(model);
        GreenRetrofitModel reread = GrmReader.Read(json).RequireModel();
        SupplySystem rereadPackaged = Assert.Single(reread.SupplySystems);
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(model);
        Assert.True(conversion.Success, Describe(conversion.Diagnostics));
        DragonPackagedAirConditioner converted = Assert.IsType<DragonPackagedAirConditioner>(OnlySupply(conversion));
        DragonHeatPump dedicated = Assert.IsType<DragonHeatPump>(converted.Source);

        var unresolved = new SupplySystem(
            "unresolved air handler",
            SupplySystemType.AirHandlingUnit,
            "SOURCE-MISSING",
            sourceSystem: null,
            id: new EntityId("SUPPLY-UNRESOLVED"));
        GreenRetrofitConversionResult unresolvedConversion = GreenRetrofitConverter.Convert(
            CreateModel(Array.Empty<SourceSystem>(), new[] { unresolved }, unresolved));
        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        JsonElement sourceGroups = jsonDocument.RootElement
            .GetProperty("building")
            .GetProperty("source_systems");
        JsonElement packagedJson = jsonDocument.RootElement
            .GetProperty("building")
            .GetProperty("supply_systems")
            .GetProperty("packaged_air_conditioner")[0];
        return new[]
        {
            "native.source_system_sealed=" + Boolean(typeof(SourceSystem).IsSealed),
            "native.none_source_type_exists=" + Boolean(typeof(SourceSystem).Assembly.GetType("Dragons.SimpleDragon.NoneSource") is not null),
            "packaged.source_id_null=" + Boolean(packaged.SourceSystemId is null),
            "packaged.source_null=" + Boolean(packaged.SourceSystem is null),
            "writer.source_group_count=" + sourceGroups.EnumerateObject().Count().ToString(CultureInfo.InvariantCulture),
            "writer.packaged_has_source_id=" + Boolean(packagedJson.TryGetProperty("source_system_id", out _)),
            "reader.source_id_null=" + Boolean(rereadPackaged.SourceSystemId is null),
            "reader.source_null=" + Boolean(rereadPackaged.SourceSystem is null),
            "conversion.success=" + Boolean(conversion.Success),
            "conversion.warning_codes=" + DiagnosticCodes(conversion.Diagnostics),
            "conversion.supply_type=" + converted.GetType().Name,
            "conversion.dedicated_source_type=" + dedicated.GetType().Name,
            "conversion.dedicated_fuel=" + dedicated.Fuel,
            "unresolved.success=" + Boolean(unresolvedConversion.Success),
            "unresolved.codes=" + DiagnosticCodes(unresolvedConversion.Diagnostics),
            "invalid.packaged_source_id=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid packaged",
                SupplySystemType.PackagedAirConditioner,
                "SOURCE-NOT-ALLOWED")),
            "invalid.required_source_id=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid handler",
                SupplySystemType.AirHandlingUnit)),
            "native.route=nullable SupplySystem.SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveSourceSystem()
    {
        SourceSystem[] sources =
        {
            new("heatpump", SourceSystemType.HeatPump, FuelType.Electricity,
                id: new EntityId("SOURCE-HEATPUMP")),
            new("geothermal", SourceSystemType.GeothermalHeatPump, FuelType.NaturalGas,
                id: new EntityId("SOURCE-GEOTHERMAL")),
            Chiller("SOURCE-CHILLER", CompressorType.Screw,
                CoolingTowerType.Closed, CoolingTowerControl.TwoSpeed),
            new("absorption", SourceSystemType.AbsorptionChiller, FuelType.NaturalGas,
                id: new EntityId("SOURCE-ABSORPTION")),
            new("boiler", SourceSystemType.Boiler, FuelType.Oil,
                hotWaterSupply: true, id: new EntityId("SOURCE-BOILER")),
            new("district", SourceSystemType.DistrictHeating,
                hotWaterSupply: false, id: new EntityId("SOURCE-DISTRICT")),
        };
        GreenRetrofitModel model = CreateModel(sources);
        string json = GrmWriter.Serialize(model);
        GreenRetrofitModel reread = GrmReader.Read(json).RequireModel();
        using JsonDocument document = JsonDocument.Parse(json);
        string[] groups = document.RootElement.GetProperty("building")
            .GetProperty("source_systems")
            .EnumerateObject()
            .Select(item => item.Name)
            .ToArray();
        string invalidJson = ReplaceRequired(
            json,
            "\"heatpump\": [",
            "\"unknown_source_type\": [");
        PropertyInfo[] properties = typeof(SourceSystem).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return new[]
        {
            "native.class=" + typeof(SourceSystem).FullName,
            "native.sealed=" + Boolean(typeof(SourceSystem).IsSealed),
            "native.constructor_count=" + typeof(SourceSystem).GetConstructors().Length.ToString(CultureInfo.InvariantCulture),
            "native.source_type_names=" + Join(Enum.GetValues<SourceSystemType>().Select(item => item.ToString())),
            "native.source_type_numeric=" + Join(Enum.GetValues<SourceSystemType>().Select(item => ((int)item).ToString(CultureInfo.InvariantCulture))),
            "writer.groups=" + Join(groups),
            "reader.types=" + Join(reread.SourceSystems.Select(item => item.Type.ToString())),
            "reader.ids=" + Join(reread.SourceSystems.Select(item => item.Id.Value)),
            "reader.runtime_types=" + Join(reread.SourceSystems.Select(item => item.GetType().Name)),
            "properties.readonly=" + Boolean(properties.All(item => !item.CanWrite)),
            "type_mapper.public_field=" + Boolean(typeof(SourceSystem).GetField("TYPE_MAPPER", BindingFlags.Public | BindingFlags.Static) is not null),
            "type_mapper.public_property=" + Boolean(typeof(SourceSystem).GetProperty("TYPE_MAPPER", BindingFlags.Public | BindingFlags.Static) is not null),
            "invalid.source_type=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", (SourceSystemType)int.MaxValue)),
            "invalid.heatpump_fuel=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid heatpump", SourceSystemType.HeatPump)),
            "invalid.chiller_components=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid chiller", SourceSystemType.Chiller)),
            "invalid.boiler_hotwater=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid boiler", SourceSystemType.Boiler, FuelType.NaturalGas)),
            "invalid.district_hotwater=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid district", SourceSystemType.DistrictHeating)),
            "invalid.reader.codes=" + DiagnosticCodes(GrmReader.Read(invalidJson)),
            "native.route=SourceSystem+SourceSystemType+GrmReader+GrmWriter",
        };
    }

    private static SourceSystem Chiller(
        string id,
        CompressorType compressor,
        CoolingTowerType tower,
        CoolingTowerControl control) => new(
            "chiller " + id,
            SourceSystemType.Chiller,
            coolingCop: 4.2d,
            coolingCapacity: 40_000d,
            compressorType: compressor,
            coolingTowerType: tower,
            coolingTowerCapacity: 45_000d,
            coolingTowerControl: control,
            id: new EntityId(id));

    private static SupplySystem FanCoil(string id, SourceSystem source) => new(
        "fan coil " + id,
        SupplySystemType.FanCoilUnit,
        source.Id.Value,
        source,
        id: new EntityId(id));

    private static SupplySystem AirHandler(string id, SourceSystem source) => new(
        "air handler " + id,
        SupplySystemType.AirHandlingUnit,
        source.Id.Value,
        source,
        id: new EntityId(id));

    private static GreenRetrofitModel CreateModel(
        IEnumerable<SourceSystem> sources,
        IEnumerable<SupplySystem>? supplies = null,
        SupplySystem? assignedSupply = null)
    {
        GreenRetrofitModel template = GrmReader.ReadFile(FindRepositoryFile(NativeTemplatePath)).RequireModel();
        Zone original = Assert.Single(template.Zones);
        SupplySystemAssignment[] assignments = assignedSupply is null
            ? Array.Empty<SupplySystemAssignment>()
            : new[] { new SupplySystemAssignment(assignedSupply.Id.Value, assignedSupply) };
        var zone = new Zone(
            original.Name,
            original.FloorNumber,
            original.Height,
            original.Surfaces,
            original.ProfileName,
            original.Profile,
            original.LightDensity,
            assignments,
            id: original.Id);
        return new GreenRetrofitModel(
            template.Name,
            template.NorthAxis,
            template.Address,
            template.Vintage,
            template.IsMultifamilyHousing,
            new[] { new BuildingFloor(zone.FloorNumber, new[] { zone }) },
            template.Materials,
            template.SurfaceConstructions,
            template.FenestrationConstructions,
            sources,
            supplies ?? Array.Empty<SupplySystem>(),
            weather: template.Weather);
    }

    private static DragonSupplySystem OnlySupply(GreenRetrofitConversionResult result)
    {
        EnergyModel model = result.RequireEnergyModel();
        return Assert.Single(Assert.Single(model.HvacAssignments).Supply.Systems);
    }

    private static string SourceToken(string json, string group, string property)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("building")
            .GetProperty("source_systems")
            .GetProperty(group)[0]
            .GetProperty(property)
            .GetString()!;
    }

    private static string ReplaceRequired(string text, string oldValue, string newValue)
    {
        Assert.Contains(oldValue, text, StringComparison.Ordinal);
        string replaced = text.Replace(oldValue, newValue, StringComparison.Ordinal);
        Assert.NotEqual(text, replaced);
        return replaced;
    }

    private static string ExceptionFact(Action action)
    {
        try
        {
            action();
            return "returned";
        }
        catch (ArgumentException error)
        {
            return error.GetType().Name + ":" + (error.ParamName ?? "none");
        }
        catch (Exception error)
        {
            return error.GetType().Name;
        }
    }

    private static string DiagnosticCodes(GrmReadResult result) => DiagnosticCodes(result.Diagnostics);

    private static string DiagnosticCodes(IEnumerable<Diagnostic> diagnostics) => Join(
        diagnostics.Select(item => item.Code).OrderBy(item => item, StringComparer.Ordinal));

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
            upstream = new
            {
                commit = UpstreamCommit,
                path = UpstreamPath,
                bytes = UpstreamBytes,
                source_sha256 = UpstreamSourceSha256,
                ast_sha256 = UpstreamAstSha256,
                inventory_content_sha256 = InventoryContentSha256,
                dependencies_sha256 = DependenciesSha256,
                loaded_sources_sha256 = LoadedSourcesSha256,
                relocation_snapshot_sha256 = RelocationSnapshotSha256,
                source_location_count = 2,
            },
            artifacts = new
            {
                fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
                generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                native_sources = NativeSources.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
                native_data = NativeData.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
            },
            observations = new[]
            {
                new
                {
                    case_id = fixtureCase.CaseId,
                    case_code = fixtureCase.Code,
                    python_case_sha256 = fixtureCase.CaseSha256,
                    python_facts_sha256 = fixtureCase.FactsSha256,
                    native_fact_count = observation.Facts.Length,
                    native_facts_sha256 = observation.FactsSha256,
                    native_facts = observation.Facts,
                },
            },
            scope = new
            {
                active_energyplus_process_claim = false,
                full_hvac_declaration_parity_claim = false,
                native_runtime_executed = true,
                python_behavior_oracle_only = true,
                relocatable_import_claim = true,
                target_count = 28,
                case_count = 6,
                deferred_count = 116,
                excluded_count = 58,
                claim_policy = "only-the-authoritative-fixture-case-and-declared-production-public-route-are-claimed",
            },
        };
    }

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
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
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(target.SymbolHash, RequiredString(source, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(source, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(source, "body_hash"));

        JsonElement upstream = receipt.GetProperty("upstream");
        AssertKeys(
            upstream,
            "ast_sha256",
            "bytes",
            "commit",
            "dependencies_sha256",
            "inventory_content_sha256",
            "loaded_sources_sha256",
            "path",
            "relocation_snapshot_sha256",
            "source_location_count",
            "source_sha256");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(UpstreamBytes, upstream.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(upstream, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(upstream, "ast_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_content_sha256"));
        Assert.Equal(DependenciesSha256, RequiredString(upstream, "dependencies_sha256"));
        Assert.Equal(LoadedSourcesSha256, RequiredString(upstream, "loaded_sources_sha256"));
        Assert.Equal(RelocationSnapshotSha256, RequiredString(upstream, "relocation_snapshot_sha256"));
        Assert.Equal(2, upstream.GetProperty("source_location_count").GetInt32());

        NativeObservation expectedObservation = observations[target.CaseIndex];
        CaseBinding expectedCase = Cases[target.CaseIndex];
        JsonElement observed = Assert.Single(receipt.GetProperty("observations").EnumerateArray());
        AssertKeys(
            observed,
            "case_code",
            "case_id",
            "native_fact_count",
            "native_facts",
            "native_facts_sha256",
            "python_case_sha256",
            "python_facts_sha256");
        Assert.Equal(expectedCase.CaseId, RequiredString(observed, "case_id"));
        Assert.Equal(expectedCase.Code, RequiredString(observed, "case_code"));
        Assert.Equal(expectedCase.CaseSha256, RequiredString(observed, "python_case_sha256"));
        Assert.Equal(expectedCase.FactsSha256, RequiredString(observed, "python_facts_sha256"));
        Assert.Equal(expectedObservation.Facts.Length, observed.GetProperty("native_fact_count").GetInt32());
        Assert.Equal(expectedObservation.FactsSha256, RequiredString(observed, "native_facts_sha256"));
        AssertStringArray(observed.GetProperty("native_facts"), expectedObservation.Facts);
        Assert.Equal(expectedObservation.FactsSha256, CanonicalSha256(observed.GetProperty("native_facts")));

        JsonElement artifacts = receipt.GetProperty("artifacts");
        AssertKeys(artifacts, "fixture", "generator", "native_data", "native_sources", "public_inventory", "python_validator");
        AssertArtifact(artifacts.GetProperty("fixture"), FixturePath, FixtureBytes, FixtureSha256);
        AssertArtifact(artifacts.GetProperty("generator"), GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertArtifact(artifacts.GetProperty("python_validator"), ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertArtifact(artifacts.GetProperty("public_inventory"), InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertArtifactArray(artifacts.GetProperty("native_sources"), NativeSources);
        AssertArtifactArray(artifacts.GetProperty("native_data"), NativeData);

        JsonElement scope = receipt.GetProperty("scope");
        AssertKeys(
            scope,
            "active_energyplus_process_claim",
            "case_count",
            "claim_policy",
            "deferred_count",
            "excluded_count",
            "full_hvac_declaration_parity_claim",
            "native_runtime_executed",
            "python_behavior_oracle_only",
            "relocatable_import_claim",
            "target_count");
        Assert.False(scope.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.Equal(6, scope.GetProperty("case_count").GetInt32());
        Assert.Equal(28, scope.GetProperty("target_count").GetInt32());
        Assert.Equal(116, scope.GetProperty("deferred_count").GetInt32());
        Assert.Equal(58, scope.GetProperty("excluded_count").GetInt32());
        Assert.False(scope.GetProperty("full_hvac_declaration_parity_claim").GetBoolean());
        Assert.True(scope.GetProperty("native_runtime_executed").GetBoolean());
        Assert.True(scope.GetProperty("python_behavior_oracle_only").GetBoolean());
        Assert.True(scope.GetProperty("relocatable_import_claim").GetBoolean());
        Assert.Equal(
            "only-the-authoritative-fixture-case-and-declared-production-public-route-are-claimed",
            RequiredString(scope, "claim_policy"));
    }

    private static void ValidateArtifactProjection(
        JsonElement value,
        string path,
        int bytes,
        string sha256) => AssertArtifact(value, path, bytes, sha256);

    private static void AssertArtifactArray(JsonElement value, IReadOnlyList<ArtifactPin> expected)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, actual.Length);
        for (int index = 0; index < expected.Count; index++)
        {
            AssertArtifact(actual[index], expected[index].Path, expected[index].Bytes, expected[index].Sha256);
        }
    }

    private static void AssertArtifact(JsonElement value, string path, int bytes, string sha256)
    {
        AssertKeys(value, "bytes", "path", "sha256");
        Assert.Equal(path, RequiredString(value, "path"));
        Assert.Equal(bytes, value.GetProperty("bytes").GetInt32());
        Assert.Equal(sha256, RequiredString(value, "sha256"));
    }

    private static void AssertPinnedArtifact(string path, int bytes, string sha256)
    {
        byte[] content = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(bytes, content.Length);
        Assert.Equal(sha256, Sha256(content));
    }

    private static string FindRepositoryFile(string repositoryPath)
    {
        string root = FindRepositoryRoot();
        string candidate = Path.Combine(root, repositoryPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException("Could not locate repository artifact.", repositoryPath);
        }

        return candidate;
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln"))
                    && Directory.Exists(Path.Combine(current.FullName, "fixtures")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string Sha256(byte[] value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string CanonicalSha256(JsonElement value)
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

        return Sha256(stream.ToArray());
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

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

    private static string Describe(IEnumerable<Diagnostic> diagnostics) => string.Join(
        Environment.NewLine,
        diagnostics.Select(item => item.Code + ": " + item.Message));

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Category,
        string CaseSha256,
        string FactsSha256,
        string[] TargetSymbols);

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

    private sealed record OracleCorpus(
        JsonElement[] FixtureCases,
        TargetBinding[] Targets,
        int DeferredCount,
        int ExcludedCount);
}
