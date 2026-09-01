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
using DragonAbsorptionChiller = Dragons.InvisibleDragon.Hvac.AbsorptionChiller;
using DragonBoiler = Dragons.InvisibleDragon.Hvac.Boiler;
using DragonChiller = Dragons.InvisibleDragon.Hvac.Chiller;
using DragonDistrictHeating = Dragons.InvisibleDragon.Hvac.DistrictHeating;
using DragonGeothermalHeatPump = Dragons.InvisibleDragon.Hvac.GeothermalHeatPump;
using DragonHeatPump = Dragons.InvisibleDragon.Hvac.HeatPump;
using DragonSourceSystem = Dragons.InvisibleDragon.Hvac.SourceSystem;
using DragonSupplySystem = Dragons.InvisibleDragon.Hvac.SupplySystem;

namespace Dragons.SimpleDragon.Tests;

public sealed class HvacThermalSourceOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-hvac-thermal-source-oracle.json";
    private const int FixtureBytes = 135_360;
    private const string FixtureSha256 =
        "sha256:a82d1b26673cada47b45b8cbd61f03beeb6ce39495090e6b731bc1b4114bcdf2";
    private const string FixtureSchema =
        "dragons.python-reference.epsimple-hvac-thermal-source.v1";
    private const string FixtureRepositoryCommit = "5a1e2bb";
    private const string CasesSha256 =
        "sha256:1648981844e29967326b4caeb0b466238e12c07e43fb25469d7325b73ac3feb2";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_hvac_thermal_source_oracle.py";
    private const int GeneratorBytes = 63_785;
    private const string GeneratorSha256 =
        "sha256:7a3ad0eb70b31542a04b6927389aad67fdcac37a0426632a00a55bdbc40f182d";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_hvac_thermal_source_oracle.py";
    private const int ValidatorBytes = 18_334;
    private const string ValidatorSha256 =
        "sha256:8d3026ebea8b4484fae93331b62ac010ba8b9bc1a536f36c4c3b12104c348dfc";
    private const string SupportPath =
        "tools/python-reference/generate_epsimple_hvac_enums_base_oracle.py";
    private const int SupportBytes = 61_377;
    private const string SupportSha256 =
        "sha256:a397d3169f61a375b12a3934a2270874bfef1f3713a635cfd5e342668d12046b";
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
        "sha256:9f07d8a23754df14f9fff1e7f2cda0b334e630ad19363173859d5c14bfdc7031";
    private const string LoadedSourcesSha256 =
        "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8";
    private const string RelocatedObservationsSha256 =
        "sha256:bda0a2f6607b8ad2d72183e64c989e76ddb91b65890398aa7191cd2c636c6f03";
    private const string TargetReceiptsSha256 =
        "sha256:0374c74cedba9ecd7ce3e744f1b33cf490531c03cdd93f96bf3510c7f2d2caf1";
    private const string AdjacentReceiptsSha256 =
        "sha256:ef4f76630b955cdfdb33b822b2fa3d59ef89c4d2b02d5435567e3f1684cfb15f";
    private const string NativeRoutesSha256 =
        "sha256:3d6a875956692d15c0512fda9a319c9f46bd39dc3b760cc48c337b7080e72886";
    private const string NativeSourcesSha256 =
        "sha256:37cec365c3d6305962db2773749fd9ad22ce148241dbc6720d3208b03f6d66b4";
    private const string NativeReviewSha256 =
        "sha256:aa0af5125100e524c774ba92d7993d00fadff21347e49303c32850e0868e11a8";
    private const string NativeTemplatePath =
        "fixtures/simple-dragon/grm/ASHRAE 140 modified.grm";
    private const int NativeTemplateBytes = 9_154;
    private const string NativeTemplateSha256 =
        "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.HvacThermalSourceOracleParityTests.MatchesPinnedHvacThermalSourcesThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SourceSystem.cs", 6_885,
            "sha256:db5fafe1034aca7b16ef222ecad981b790952474e5311b798c9eb6a677c82af4"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_641,
            "sha256:a212275276ccff153d5df42a44a46ac8877afa485e315ee27d08767a909f29bb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_646,
            "sha256:40e6e7aa1eb89cb341c7e7a32471fa029024e49b261dce8a8926514109d727ba"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_154,
            "sha256:9462f609a4a7a4e062612e4058921b0c91931dc8ff7216dbe54e258cb59ec22c"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "epsimple-hvac-thermal-source.absorption-chiller-state-validation-json-dragon", "absorption", "sha256:b14eee5f3b17ee6554f380af12eef43e8b5af07bcfc0aaa55e506cb2d26142eb", "sha256:07e2bba13da938db6c7bbd64d047bda378f2e40ca541615ee898fbb025da6919", new[] { "AbsorptionChiller", "AbsorptionChiller.ID", "AbsorptionChiller.__init__", "AbsorptionChiller.boiler_efficiency", "AbsorptionChiller.capacity", "AbsorptionChiller.cop", "AbsorptionChiller.from_json", "AbsorptionChiller.to_dragon" }),
        new("B01", "epsimple-hvac-thermal-source.boiler-state-validation-json-dragon", "boiler", "sha256:f82cb04c34c84047f95c9352634a6b01ba922c70a0b7b674e4a68d30d2985e54", "sha256:eaaaacf0bd3ef08a080aca2bc2ab8f61fffe6a07dccaaaa664fc5bf798e7ddc2", new[] { "Boiler", "Boiler.ID", "Boiler.__init__", "Boiler.capacity", "Boiler.efficiency", "Boiler.from_json", "Boiler.fuel", "Boiler.hotwater_supply", "Boiler.to_dragon" }),
        new("C01", "epsimple-hvac-thermal-source.chiller-state-tower-branches-json-dragon", "chiller", "sha256:108cee171ab3816a0817b6bd78412c74056e953eeb924e1a6909c8828e471de1", "sha256:2cb0247acde415dd8cc99296635057972415979af3754edb55930c2ea89055e3", new[] { "Chiller", "Chiller.ID", "Chiller.__init__", "Chiller.capacity", "Chiller.compressor_type", "Chiller.coolingtower_capacity", "Chiller.coolingtower_control", "Chiller.coolingtower_type", "Chiller.cop", "Chiller.from_json", "Chiller.to_dragon" }),
        new("D01", "epsimple-hvac-thermal-source.district-heating-state-validation-json-dragon", "district", "sha256:2862c963664ca87858dbb43d3f98e432cd963edb196528912163488b6a940c50", "sha256:4d2e2f6b199cdc3bc0eb2a569243fc4bd85b10c8b84be9f36e07d7eef3d94695", new[] { "DistrictHeating", "DistrictHeating.ID", "DistrictHeating.__init__", "DistrictHeating.from_json", "DistrictHeating.hotwater_supply", "DistrictHeating.to_dragon" }),
        new("G01", "epsimple-hvac-thermal-source.geothermal-heatpump-json-dragon", "geothermal", "sha256:abd7c2a03dd1f1c9be3dd4d3009546fd2fddab3b9e0d5e7f9a63222b966d7b81", "sha256:86697920fc6275852d414b00544844417927fb1e57f95d5a93718f7d262dffaf", new[] { "GeothermalHeatPump", "GeothermalHeatPump.from_json", "GeothermalHeatPump.to_dragon" }),
        new("H01", "epsimple-hvac-thermal-source.heatpump-state-validation-json-dragon", "heatpump", "sha256:ab6a248ed48834b7d4e84467aafa68962f306940fd704f66b52e3f59cfe9069e", "sha256:8ac4a3fe84318f9f640b9f2387f5aa8185f97c35a423ba588c27462711050599", new[] { "HeatPump", "HeatPump.ID", "HeatPump.__init__", "HeatPump.cooling_capacity", "HeatPump.cooling_cop", "HeatPump.from_json", "HeatPump.fuel", "HeatPump.heating_capacity", "HeatPump.heating_cop", "HeatPump.to_dragon" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        Target(135, "AbsorptionChiller", "class", "epsimple-hvac-thermal-source-135-c44e12f9", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-c44e12f9", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.AbsorptionChiller and public properties", 0),
        Target(136, "AbsorptionChiller.ID", "function", "epsimple-hvac-thermal-source-136-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 0),
        Target(139, "AbsorptionChiller.__init__", "function", "epsimple-hvac-thermal-source-139-4aae19c6", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-4aae19c6", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.AbsorptionChiller and public properties", 0),
        Target(142, "AbsorptionChiller.boiler_efficiency", "function", "epsimple-hvac-thermal-source-142-be052579", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.BoilerEfficiency", 0),
        Target(143, "AbsorptionChiller.capacity", "function", "epsimple-hvac-thermal-source-143-d699d5f1", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCapacity", 0),
        Target(144, "AbsorptionChiller.cop", "function", "epsimple-hvac-thermal-source-144-253d21d2", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCop", 0),
        Target(145, "AbsorptionChiller.from_json", "function", "epsimple-hvac-thermal-source-145-f305d756", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-f305d756", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) source-system dispatch", 0),
        Target(146, "AbsorptionChiller.to_dragon", "function", "epsimple-hvac-thermal-source-146-7a12c015", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-7a12c015", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 0),
        Target(157, "Boiler", "class", "epsimple-hvac-thermal-source-157-8d52ff9e", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-8d52ff9e", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.Boiler and public properties", 1),
        Target(158, "Boiler.ID", "function", "epsimple-hvac-thermal-source-158-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 1),
        Target(161, "Boiler.__init__", "function", "epsimple-hvac-thermal-source-161-f45db90e", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-f45db90e", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.Boiler and public properties", 1),
        Target(164, "Boiler.capacity", "function", "epsimple-hvac-thermal-source-164-d699d5f1", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.HeatingCapacity", 1),
        Target(165, "Boiler.efficiency", "function", "epsimple-hvac-thermal-source-165-80144f2f", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Efficiency", 1),
        Target(166, "Boiler.from_json", "function", "epsimple-hvac-thermal-source-166-bd3f1e5a", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-bd3f1e5a", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) source-system dispatch", 1),
        Target(167, "Boiler.fuel", "function", "epsimple-hvac-thermal-source-167-64d0443e", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.FuelType", 1),
        Target(168, "Boiler.hotwater_supply", "function", "epsimple-hvac-thermal-source-168-f9effaf3", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.HotWaterSupply", 1),
        Target(169, "Boiler.to_dragon", "function", "epsimple-hvac-thermal-source-169-86b77a93", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-86b77a93", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 1),
        Target(170, "Chiller", "class", "epsimple-hvac-thermal-source-170-8baa00de", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-8baa00de", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.Chiller and public properties", 2),
        Target(171, "Chiller.ID", "function", "epsimple-hvac-thermal-source-171-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 2),
        Target(174, "Chiller.__init__", "function", "epsimple-hvac-thermal-source-174-9c5215c4", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-9c5215c4", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.Chiller and public properties", 2),
        Target(177, "Chiller.capacity", "function", "epsimple-hvac-thermal-source-177-d699d5f1", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCapacity", 2),
        Target(178, "Chiller.compressor_type", "function", "epsimple-hvac-thermal-source-178-000c99e3", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CompressorType", 2),
        Target(179, "Chiller.coolingtower_capacity", "function", "epsimple-hvac-thermal-source-179-e56b52fb", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingTowerCapacity", 2),
        Target(180, "Chiller.coolingtower_control", "function", "epsimple-hvac-thermal-source-180-473c615a", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingTowerControl", 2),
        Target(181, "Chiller.coolingtower_type", "function", "epsimple-hvac-thermal-source-181-75acdde9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingTowerType", 2),
        Target(182, "Chiller.cop", "function", "epsimple-hvac-thermal-source-182-253d21d2", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCop", 2),
        Target(183, "Chiller.from_json", "function", "epsimple-hvac-thermal-source-183-ca5a6445", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-ca5a6445", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) source-system dispatch", 2),
        Target(184, "Chiller.to_dragon", "function", "epsimple-hvac-thermal-source-184-b3b58ae8", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-b3b58ae8", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 2),
        Target(199, "DistrictHeating", "class", "epsimple-hvac-thermal-source-199-a1c6d574", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-a1c6d574", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.DistrictHeating and public properties", 3),
        Target(200, "DistrictHeating.ID", "function", "epsimple-hvac-thermal-source-200-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 3),
        Target(203, "DistrictHeating.__init__", "function", "epsimple-hvac-thermal-source-203-f477c20b", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-f477c20b", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.DistrictHeating and public properties", 3),
        Target(206, "DistrictHeating.from_json", "function", "epsimple-hvac-thermal-source-206-c53a5bbb", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-c53a5bbb", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) source-system dispatch", 3),
        Target(207, "DistrictHeating.hotwater_supply", "function", "epsimple-hvac-thermal-source-207-f9effaf3", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.HotWaterSupply", 3),
        Target(208, "DistrictHeating.to_dragon", "function", "epsimple-hvac-thermal-source-208-bf1c4c8b", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-bf1c4c8b", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 3),
        Target(248, "GeothermalHeatPump", "class", "epsimple-hvac-thermal-source-248-a87f33ee", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-a87f33ee", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.GeothermalHeatPump and public properties", 4),
        Target(251, "GeothermalHeatPump.from_json", "function", "epsimple-hvac-thermal-source-251-81ac3508", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-81ac3508", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) source-system dispatch", 4),
        Target(252, "GeothermalHeatPump.to_dragon", "function", "epsimple-hvac-thermal-source-252-069a6710", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-069a6710", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 4),
        Target(253, "HeatPump", "class", "epsimple-hvac-thermal-source-253-3872db31", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-3872db31", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.HeatPump and public properties", 5),
        Target(254, "HeatPump.ID", "function", "epsimple-hvac-thermal-source-254-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 5),
        Target(257, "HeatPump.__init__", "function", "epsimple-hvac-thermal-source-257-7e88c6cd", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-7e88c6cd", "Dragons.SimpleDragon.SourceSystem constructor with SourceSystemType.HeatPump and public properties", 5),
        Target(260, "HeatPump.cooling_capacity", "function", "epsimple-hvac-thermal-source-260-2c365992", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCapacity", 5),
        Target(261, "HeatPump.cooling_cop", "function", "epsimple-hvac-thermal-source-261-59bd7983", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCop", 5),
        Target(262, "HeatPump.from_json", "function", "epsimple-hvac-thermal-source-262-20b220f0", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-20b220f0", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) source-system dispatch", 5),
        Target(263, "HeatPump.fuel", "function", "epsimple-hvac-thermal-source-263-37420422", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.FuelType", 5),
        Target(264, "HeatPump.heating_capacity", "function", "epsimple-hvac-thermal-source-264-b48949da", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.HeatingCapacity", 5),
        Target(265, "HeatPump.heating_cop", "function", "epsimple-hvac-thermal-source-265-55ddf021", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.HeatingCop", 5),
        Target(266, "HeatPump.to_dragon", "function", "epsimple-hvac-thermal-source-266-0feeee0b", "exception", "reviewed-native-discriminated-source-aggregate-and-conversion-route-0feeee0b", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 5),
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    [
        new(32, "sha256:da848eefc680aa746ecd27309b1b88ae4b75c71c4f2837fc6fa9c3aa4488d4ce"),
        new(32, "sha256:96a34260bb285e6b84e3f7364d7b83ace8b3d9e9fea0d311d288c661816dbe29"),
        new(39, "sha256:d30ed66dea2c6af5e4350a4fb87d3961d2cfee65cf85f69876dea3b2671e3077"),
        new(21, "sha256:7e6417776b82561513c836b4c84b4f17e56807b3d3b81f5edb250ded754c920c"),
        new(26, "sha256:928a7ac965fb4b77a5199d01bf927710c4e8845068e03f5dbf08a89326b9fe0f"),
        new(36, "sha256:9480da94ac8774f1430ddc1749e4db408cf43327925e47c5dbbdcb1db92cc929"),
    ];

    private static readonly string[] ExpectedReceiptHashes =
    [
        "sha256:09022e8150e5e13d1aa41bff01c32b56b651dc067bbc1e72870087934d25a73b",
        "sha256:c402b02825939ef171e4d684123e86f072c544746513ca905e48865d9461970f",
        "sha256:8d0af8876ea3a47ab5354d9c0dcb18e74dd667da4210003f5b1b231fb78711dc",
        "sha256:73cd0ed862a50f9f31d8eb2e4c9214ed483739f767be803dc175522d050da873",
        "sha256:0698abd90d45648a89cbfc8d7a977ecb28834163c83666a1d8d9d990f7797826",
        "sha256:927be08883794de83292941ebb6c637758b6d3ab2e93153cfc32c13378f7bc37",
        "sha256:235e5d92bb2b90c4f56c55d8f8fecf41e40e212ce022ed258c2d3234326deef1",
        "sha256:4da0602ceacad8e93a45581cd61661c6446c15037baaf6d1020b42f970f76f05",
        "sha256:fd6f3a9e0c8a24ee763ae930276d8c4ddad26cd22f5e4667d5e9b9a22ab050eb",
        "sha256:678dcd69cc805811b30310b16e9c33963869b4f57c2b5146a6548ff46b7c43fd",
        "sha256:a5a6ccee2b0e46ef8fed86f4fb606b9471d349bdd4b59d5dcf06a73616d624a4",
        "sha256:23e514d0ae5fa6e4c92c813818d1eff11f46178eed1150bad885566f820869e9",
        "sha256:68be179dc0c55ff8dedf24aff688f18f42e5ae5052fab78bb1493eca1f5a63ac",
        "sha256:64e95b50f4492a9c293a437e084ff07d0f756a0cba9da9f3591b2f2bc6aabcc8",
        "sha256:5eca476e3478dedfce95607ea901dc356c6d5281940078f537948f5077bcca39",
        "sha256:755dfb59d6ae14c6456fedc64ef7dce2c567083e6be8f0b8020922b645651beb",
        "sha256:7d3405dc75e4f42a1c6ae181f6929b2cb8b200dc029c92152a593ce81ead618a",
        "sha256:a17bbe8d189c39fced38daa56053b04ae734f8dc5ed7a0f371be7b1c2a5cfc9d",
        "sha256:717218bacf91fc74a611d2e2c722970bb4b284ea12b93f960e604219c859cf25",
        "sha256:1326d7df40352c2542ff87029f49776bf53e51bf3cb82a296f66058bdae19e29",
        "sha256:5e16fbc2462aa96e9e938f2f4e43ebdab055aab476419461835c88eeb7e0dc3e",
        "sha256:ec4d37a9a2e7ab4c712554d900889b3b1e187eeebd46c66cce4cc15b9eb7a0e2",
        "sha256:34ce1878713cc7d4a302009854670e2b156b29ec1957e667e1000a654e9a224d",
        "sha256:e801405b3cc71a6db2c79953de3ac80df397ee5dd48c11ca1e75ab17307f3e82",
        "sha256:fe9274e4af11908337968518fa9f44a5e3e37b11f72b4062d03ec94a5af618d9",
        "sha256:a0312aa437751272b36972ed658c653f09921612a328bb38292a8a192b9a33f3",
        "sha256:114104e25d98805eb2777945e2bbeec1e7ae8a053cc67530cdf3ffe2aea639f8",
        "sha256:686477b76a89b8c645e28b3b2e863eb921bdd3632a9f9912dd57892efac948e5",
        "sha256:584924a1d31cceeff08cdb1652b6df9ef561b452d918e5357908949f7f470f17",
        "sha256:5357d3656de8a7269e4f10f0792b130578f9bf9f2e76b817d5bd288020045aba",
        "sha256:55453c733388b73948a54eddd5c106bef7e3d23d404778ef4bd7d8aede4d09cf",
        "sha256:3e09f7fe894c9ed386d2cf7107006c3396e065707e9192a23765fcb3600b66e3",
        "sha256:440594a7a08e9275eb3eb13013b64c37d8fdff43ba0c30816c5c268ed7764701",
        "sha256:fa429f45ad3d5a5bd09639a1f4850172713e9481a749a2e538f88e833b25a433",
        "sha256:c93fb15e44169025b0f20a37a617a402d7a308c7022236e788c91c6303757375",
        "sha256:a1059181cb6ced53ee44d18349d2646a9e3af386b97ac753a025366744399053",
        "sha256:9d077b112a7c84e69fa409b5124aaaf0f66608fa667428e1684f932509527e96",
        "sha256:e40dba82ff37415c087802b95d23dbc7de39f4f57d10cec67563e8a39d513bb9",
        "sha256:607e6389746cceb569d060564cb9ee2ded9f299f37b3e9cc8e69adf9131862f4",
        "sha256:274344a9f969c9617104a5d0575881c7dec4651e7539635134b939a64cd29171",
        "sha256:91d77ba1d4d6606d086116e3e5e27ebeb64dc8d3871a16d1b2d1026f55ca75fc",
        "sha256:5fb761d4778387f6e61c0e48d339ef5cb1e6ff6c0d4921dcbbaca69c0814a3e9",
        "sha256:51449b47bdb4715a32a0fa4fbd244ee56560934ce77a4cb61092ae550bb6ca5a",
        "sha256:ba6167bd89907c0bc45f51ceaf444fe6f54c00ce00f81f265b4f7e6e440cbf42",
        "sha256:adfcf293c008bf0289923706eaccb6e89af3d2d6a1da76fadda5fa16f5e880b9",
        "sha256:154ba5cc67bd9a8a3fa7f6ffbcd6da47e3af80c8c41df22b930ed394b4308f0c",
        "sha256:4f42fec81caee4bcfe980e02888be68f13bc186dc17f043e1d6a8517a8a785f6",
    ];


    private static readonly string[] ExpectedCollectorOutputHashes =
    [
        "sha256:f48e5d49030a654320c3f9b46f1fd23402cc057681ffe1266bac5b93f77c143b", // epsimple-hvac-thermal-source-135-c44e12f9
        "sha256:d4842bf9967e3ec5f36f7daf000cbe0230474af526b6f04aabb080685d350932", // epsimple-hvac-thermal-source-136-246156d9
        "sha256:d9d3309d759989531fe5c687f4b12c4d8bff844d936543ae7de7cbc3367274c3", // epsimple-hvac-thermal-source-139-4aae19c6
        "sha256:e8762f77024b9b0f2064bf8e0361427011bda78e2fcbcb2c1ca7fc5ebec61fbf", // epsimple-hvac-thermal-source-142-be052579
        "sha256:60952b6ae6a56b5cdda9647733c2ee90801e98b37a0f1b4a4cb7036b848bb32a", // epsimple-hvac-thermal-source-143-d699d5f1
        "sha256:d11d8a4b8ac7f5016fcaa742ab222a00ac0d3179af9fefa826853ab8e0b97cce", // epsimple-hvac-thermal-source-144-253d21d2
        "sha256:4db311f4f6d33ad35ffc0ef7150781d7b26d4ae43005b8df148d02d387ae253c", // epsimple-hvac-thermal-source-145-f305d756
        "sha256:6110b8cd827a88a927bd2b5f160559fc1b9ec023fbda8436fcd02c217685bb0f", // epsimple-hvac-thermal-source-146-7a12c015
        "sha256:e017e238045dfcd5bddf753c54c360ce453f79abfdbf3d3b7d4ff39f1f2992b0", // epsimple-hvac-thermal-source-157-8d52ff9e
        "sha256:229f2e78da834265a53de115ccb9744c2dd975bd73ae712b13ef66521b364b56", // epsimple-hvac-thermal-source-158-246156d9
        "sha256:3ec4b5da6c26f18586bd2af6a750fe74f3c89b82a71c4c1d9584051350227e1b", // epsimple-hvac-thermal-source-161-f45db90e
        "sha256:40e236292e737cf1026273d88a7bffa5fd7754592978f2dd06064dabad7064be", // epsimple-hvac-thermal-source-164-d699d5f1
        "sha256:bc60352fb304ab2102d7dcb2cb75f41959101d82f1538d4ddde7690d0a8dc95b", // epsimple-hvac-thermal-source-165-80144f2f
        "sha256:58dba5ce887f49519d276a84dbe246f07a724589b6ff65ec05b104f405aebd02", // epsimple-hvac-thermal-source-166-bd3f1e5a
        "sha256:8058f582646e18348702b3d21e51276729d00b0d3813402e7f037695e5fe8238", // epsimple-hvac-thermal-source-167-64d0443e
        "sha256:3fc98c7a0024451b6a113e75067fe2aee5c36ac7f1e635db80b8a550d37b1761", // epsimple-hvac-thermal-source-168-f9effaf3
        "sha256:5bfec6588aec44a83d55c77f7b662ec5748c22d04d1d9794d751ba01db291c9e", // epsimple-hvac-thermal-source-169-86b77a93
        "sha256:d28663574aa8353bae6d1188e579d721946688cb07cdd9e8b3284a6bb0c4fdaf", // epsimple-hvac-thermal-source-170-8baa00de
        "sha256:259a14f0625be872204f5063de28700847c116f3b8dbf4c1309c9fcab4fd6810", // epsimple-hvac-thermal-source-171-246156d9
        "sha256:f7bdc1c8798f72c7d31efd920d58c8b44b45bd627bd534fd6f9c3efc24014b94", // epsimple-hvac-thermal-source-174-9c5215c4
        "sha256:d243d8ccd72c3f8f3645d32d0e7b59f5ec15f973c73c5e4c60c7d362526525a3", // epsimple-hvac-thermal-source-177-d699d5f1
        "sha256:f3094367e7f5adc73fba6963ae72782f225e0706c860af794c32b29d9d48d49a", // epsimple-hvac-thermal-source-178-000c99e3
        "sha256:441769a3c9e603eda71a8bc738226fb2929708cab39e3d4f8a6dc44f4106c655", // epsimple-hvac-thermal-source-179-e56b52fb
        "sha256:33a95fbaec5d81be8bbaa5b091ab8cfbb66bd7fc0ed6b7e775e9be480ac0c8d0", // epsimple-hvac-thermal-source-180-473c615a
        "sha256:406c1d88e37f97a54858fa9f12df15d89872da185705e3e64e9cd2caea9d18e3", // epsimple-hvac-thermal-source-181-75acdde9
        "sha256:a9bb3dada60a9e9cc9c259da2064b93467d65012e59a4627322d37d9d4ae8cd6", // epsimple-hvac-thermal-source-182-253d21d2
        "sha256:56e319617a45666e9293b317b755c7f1a9e34c42afe6e8d0eaf050dd974aa298", // epsimple-hvac-thermal-source-183-ca5a6445
        "sha256:734972d99e2edbf2d08d0f5f169d02616770ddfd4ffae2271db2c2721432cd30", // epsimple-hvac-thermal-source-184-b3b58ae8
        "sha256:e649f58495ad74e911dc273f8d61972f18bf86e334785be737ed89de639f3d1d", // epsimple-hvac-thermal-source-199-a1c6d574
        "sha256:79f8a3a448a2277b55b96b8efadc37cf6cd36520a625a8a64e51cfb7257b6545", // epsimple-hvac-thermal-source-200-246156d9
        "sha256:651b9a11675c322aff968ed1e298741f90f1efbf58acc607c1a6af9621375c88", // epsimple-hvac-thermal-source-203-f477c20b
        "sha256:9bd5939b552e2d3b2e3268964c2381d718ef1eaaf9928446d30928fbeaf59269", // epsimple-hvac-thermal-source-206-c53a5bbb
        "sha256:e5f2715f2508924d86e6ea2ce818358b24019a890716e9cd5d3abcc3cd66ecc2", // epsimple-hvac-thermal-source-207-f9effaf3
        "sha256:d37c4df94b4a58cb4faff84723e60114443a2e4e3e84914eebeb18e2c23eea23", // epsimple-hvac-thermal-source-208-bf1c4c8b
        "sha256:f306dbcf04b4e4da776db42299b2a5aa2e500eec6207611d9988232953bf892d", // epsimple-hvac-thermal-source-248-a87f33ee
        "sha256:f4775f48af555fbc17e4eb9e9bf189c406442c4a813bb05adc7d17f03c3b57c1", // epsimple-hvac-thermal-source-251-81ac3508
        "sha256:7cc1ed1b0749f17a22b19652b2a72fd941cb4a271c52271584f6000e329b53b8", // epsimple-hvac-thermal-source-252-069a6710
        "sha256:f766078b3f52722738664415791be0f0e6aab247bfba8885cd01944efcca4143", // epsimple-hvac-thermal-source-253-3872db31
        "sha256:e4a8200c63acc8cb75849a6dc2ea8c1693b53ca243b80c0aeaeb4ed47cc6d036", // epsimple-hvac-thermal-source-254-246156d9
        "sha256:4ba9570536d1b8071394161e128a8addae7d8ed87b88fb2aea474b3d4ec598d6", // epsimple-hvac-thermal-source-257-7e88c6cd
        "sha256:1b1033db6264070fb444ca82eda0776a6f592d4e4905c5f28352fb5cdb072ff5", // epsimple-hvac-thermal-source-260-2c365992
        "sha256:784d37a6d865e79b07075b8fd13cc54151b9f28d9116a19f78e4cf02b1d5acfe", // epsimple-hvac-thermal-source-261-59bd7983
        "sha256:803cc59a441cf98e8ec70e1053ab97ebf333b16a6ad3d6661397469677d4b4cd", // epsimple-hvac-thermal-source-262-20b220f0
        "sha256:c6fcab229670e39e7067e0df25e370f87d9ccdc58468951fc4e5cc0e495abe1c", // epsimple-hvac-thermal-source-263-37420422
        "sha256:a827ac48eba9ba9c1a87c8889cafd6e59e55738a01edcd7b9a27f86eb0fe3af3", // epsimple-hvac-thermal-source-264-b48949da
        "sha256:722983307641cbf07c704d65257d6422a4d28285d80685cb09308a8935aa7b03", // epsimple-hvac-thermal-source-265-55ddf021
        "sha256:efbcb11d2cb345eed15e65fc6e577f98f5f83eb63b8d6dda2915a6eda3271cf7", // epsimple-hvac-thermal-source-266-0feeee0b
    ];

    [Fact]
    public void MatchesPinnedHvacThermalSourcesThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
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
                "HVAC_THERMAL_SOURCE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(47, recordCount);
        Assert.Equal(47, corpus.Targets.Length);
        Assert.Equal(47, corpus.Targets.Select(item => item.AssertionId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(24, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(23, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.Equal(6, corpus.FixtureCases.Length);
        Assert.Equal(155, corpus.AdjacentIndices.Length);
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
        AssertPinnedArtifact(SupportPath, SupportBytes, SupportSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertPinnedArtifact(NativeTemplatePath, NativeTemplateBytes, NativeTemplateSha256);
        foreach (ArtifactPin source in NativeSources)
        {
            AssertPinnedArtifact(source.Path, source.Bytes, source.Sha256);
        }

        Assert.True(typeof(SourceSystem).IsSealed);
        Assert.Single(typeof(SourceSystem).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.Id), typeof(EntityId));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.Name), typeof(string));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.Type), typeof(SourceSystemType));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.FuelType), typeof(FuelType?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.HeatingCop), typeof(double?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CoolingCop), typeof(double?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.HeatingCapacity), typeof(double?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CoolingCapacity), typeof(double?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.Efficiency), typeof(double?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.HotWaterSupply), typeof(bool?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CompressorType), typeof(CompressorType?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CoolingTowerType), typeof(CoolingTowerType?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CoolingTowerCapacity), typeof(double?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.CoolingTowerControl), typeof(CoolingTowerControl?));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.BoilerEfficiency), typeof(double?));
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
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => AssertUniqueObjectKeysRecursive(duplicate.RootElement));
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
            "fact_sha256",
            "native_review",
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
        ValidateNativeReview(root.GetProperty("native_review"));

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

        (TargetBinding[] targets, int[] adjacentIndices) = ValidateTargets(root);
        ValidateConsumerContract(root.GetProperty("consumer_contract"), targets, adjacentIndices);
        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol),
            fixtureCases.SelectMany(item => ReadStringArray(item.GetProperty("target_symbols"))));
        return new OracleCorpus(fixtureCases, targets, adjacentIndices);
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
            "strict_json_support",
            "thermal_source_support");
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
        Assert.Equal(DependenciesSha256, CanonicalSha256(dependencies));
        AssertArtifact(
            runtime.GetProperty("bootstrap"),
            "tools/python-reference/bootstrap_reference.py",
            1_232,
            "sha256:0674dcf1fe966de2a4b873a360ef67be48d74f38ba80adba9c74405fd9be7e0f");
        AssertArtifact(
            runtime.GetProperty("strict_json_support"),
            "tools/python-reference/generate_schedule_type_oracle.py",
            21_108,
            "sha256:555a1df41e5369dbbc44b0729a48673610a86951a215c8e2aa00cfa4fce156f1");
        AssertArtifact(runtime.GetProperty("thermal_source_support"), SupportPath, SupportBytes, SupportSha256);
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(
            upstream,
            "adjacent_receipts_sha256",
            "commit",
            "inventory",
            "isolated_import",
            "source",
            "target_receipts_sha256");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(TargetReceiptsSha256, RequiredString(upstream, "target_receipts_sha256"));
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(upstream, "adjacent_receipts_sha256"));

        JsonElement inventory = upstream.GetProperty("inventory");
        AssertKeys(inventory, "bytes", "content_sha256", "file_sha256");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));

        JsonElement source = upstream.GetProperty("source");
        AssertKeys(source, "ast_sha256", "bytes", "path", "source_sha256");
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement isolated = upstream.GetProperty("isolated_import");
        AssertKeys(
            isolated,
            "epsimple_core_initializer_executed",
            "epsimple_package_initializer_executed",
            "loaded_local_modules",
            "loaded_local_modules_sha256",
            "relocated_observations_sha256",
            "relocated_source_copy",
            "source_location_count");
        Assert.False(isolated.GetProperty("epsimple_package_initializer_executed").GetBoolean());
        Assert.False(isolated.GetProperty("epsimple_core_initializer_executed").GetBoolean());
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal(
            "byte-identical-epsimple-and-idragon-trees",
            RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(LoadedSourcesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(RelocatedObservationsSha256, RequiredString(isolated, "relocated_observations_sha256"));
        JsonElement modules = isolated.GetProperty("loaded_local_modules");
        Assert.Equal(16, modules.GetArrayLength());
        Assert.Equal(LoadedSourcesSha256, CanonicalSha256(modules));
    }

    private static void ValidateNativeReview(JsonElement review)
    {
        AssertKeys(
            review,
            "public_production_routes_only",
            "python_executes_native_runtime",
            "routes_sha256",
            "source_receipts",
            "source_receipts_sha256");
        Assert.True(review.GetProperty("public_production_routes_only").GetBoolean());
        Assert.False(review.GetProperty("python_executes_native_runtime").GetBoolean());
        Assert.Equal(NativeRoutesSha256, RequiredString(review, "routes_sha256"));
        Assert.Equal(NativeSourcesSha256, RequiredString(review, "source_receipts_sha256"));
        AssertArtifactArray(review.GetProperty("source_receipts"), NativeSources);
        Assert.Equal(NativeSourcesSha256, CanonicalSha256(review.GetProperty("source_receipts")));
        Assert.Equal(NativeReviewSha256, CanonicalSha256(review));
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
        Assert.Empty(item.GetProperty("context_symbols").EnumerateArray());
        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "facts_sha256", "outcome");
        Assert.Equal("observed", RequiredString(python, "outcome"));
        Assert.Equal(expected.FactsSha256, RequiredString(python, "facts_sha256"));
        Assert.Equal(expected.FactsSha256, RequiredString(factHashes, expected.CaseId));
        Assert.Equal(expected.FactsSha256, CanonicalSha256(python.GetProperty("facts")));
        Assert.Equal(expected.CaseSha256, RequiredString(caseHashes, expected.CaseId));
        Assert.Equal(expected.CaseSha256, CanonicalSha256(item));
    }

    private static (TargetBinding[] Targets, int[] AdjacentIndices) ValidateTargets(JsonElement root)
    {
        JsonElement[] descriptors = root.GetProperty("symbols").EnumerateArray().ToArray();
        JsonElement[] receipts = root.GetProperty("target_receipts").EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, descriptors.Length);
        Assert.Equal(ExpectedTargets.Length, receipts.Length);
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(root.GetProperty("target_receipts")));

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

        int[] sourceIndices = inventorySymbols.EnumerateArray()
            .Select((item, index) => (item, index))
            .Where(pair => RequiredString(pair.item, "path") == UpstreamPath)
            .Select(pair => pair.index)
            .ToArray();
        Assert.Equal(Enumerable.Range(135, 202), sourceIndices);
        int[] targetIndices = targets.Select(item => item.InventoryIndex).ToArray();
        int[] adjacentIndices = sourceIndices.Except(targetIndices).ToArray();
        Assert.Equal(155, adjacentIndices.Length);
        object[] adjacentReceipts = adjacentIndices
            .Select(index => InventoryReceipt(inventorySymbols[index], index))
            .ToArray();
        Assert.Equal(
            AdjacentReceiptsSha256,
            CanonicalSha256(JsonSerializer.SerializeToElement(adjacentReceipts)));
        return (targets, adjacentIndices);
    }

    private static object InventoryReceipt(JsonElement symbol, int index) => new
    {
        body_hash = RequiredString(symbol, "body_hash"),
        inventory_index = index,
        kind = RequiredString(symbol, "kind"),
        path = RequiredString(symbol, "path"),
        signature_hash = RequiredString(symbol, "signature_hash"),
        symbol = RequiredString(symbol, "symbol"),
        symbol_hash = RequiredString(symbol, "symbol_hash"),
    };

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
        IReadOnlyList<TargetBinding> targets,
        IReadOnlyList<int> adjacentIndices)
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
            "coverage_by_symbol",
            "evidence_contract",
            "expectations",
            "native_routes",
            "runtime_signatures");
        Assert.Equal(6, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(24, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(23, counts.GetProperty("exception").GetInt32());

        string[] targetSymbols = ExpectedTargets.Select(item => item.Symbol).ToArray();
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement routes = contract.GetProperty("native_routes");
        JsonElement expectations = contract.GetProperty("expectations");
        JsonElement signatures = contract.GetProperty("runtime_signatures");
        JsonElement coverage = contract.GetProperty("coverage_by_symbol");
        AssertKeys(assertions, targetSymbols);
        AssertKeys(classifications, targetSymbols);
        AssertKeys(routes, targetSymbols);
        AssertKeys(expectations, targetSymbols);
        AssertKeys(signatures, targetSymbols);
        AssertKeys(coverage, targetSymbols);
        Assert.Equal(RuntimeSignaturesSha256, CanonicalSha256(signatures));
        Assert.Equal(NativeRoutesSha256, CanonicalSha256(routes));
        string[] exceptionSymbols = ExpectedTargets
            .Where(item => item.Classification == "exception")
            .Select(item => item.Symbol)
            .ToArray();
        AssertKeys(adaptations, exceptionSymbols);
        foreach (TargetBinding target in targets)
        {
            Assert.Equal(target.AssertionId, RequiredString(assertions, target.Symbol));
            Assert.Equal(target.Classification, RequiredString(classifications, target.Symbol));
            Assert.Equal(target.NativeRoute, RequiredString(routes, target.Symbol));
            Assert.Equal(Cases[target.CaseIndex].CaseId, RequiredString(coverage, target.Symbol));
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

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "adjacent_count",
            "adjacent_indices",
            "exact_one_case_target_partition",
            "full_hvac_source_partition",
            "source_declaration_count",
            "target_count",
            "target_indices",
            "target_symbols");
        Assert.Equal(155, closure.GetProperty("adjacent_count").GetInt32());
        Assert.Equal(adjacentIndices, ReadIntArray(closure.GetProperty("adjacent_indices")));
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_hvac_source_partition").GetBoolean());
        Assert.Equal(202, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(47, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex),
            ReadIntArray(closure.GetProperty("target_indices")));
        AssertStringArray(closure.GetProperty("target_symbols"), targetSymbols);
        Assert.Equal(
            Enumerable.Range(135, 202),
            ExpectedTargets.Select(item => item.InventoryIndex)
                .Concat(adjacentIndices)
                .OrderBy(item => item));

        JsonElement evidence = contract.GetProperty("evidence_contract");
        AssertKeys(
            evidence,
            "active_energyplus_process_claim",
            "exact_cpython_behavior_oracle",
            "expected_receipt_count",
            "native_runtime_executed_by_python_oracle",
            "path_independent_relocated_import",
            "target_coverage_complete");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.True(evidence.GetProperty("exact_cpython_behavior_oracle").GetBoolean());
        Assert.Equal(47, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
    }

    private static NativeObservation ObserveNativeCase(int index)
    {
        string[] facts = index switch
        {
            0 => ObserveAbsorptionChiller(),
            1 => ObserveBoiler(),
            2 => ObserveChiller(),
            3 => ObserveDistrictHeating(),
            4 => ObserveGeothermalHeatPump(),
            5 => ObserveHeatPump(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return new NativeObservation(
            Cases[index].Code,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveAbsorptionChiller()
    {
        var minimal = new SourceSystem(
            "Abs Default",
            SourceSystemType.AbsorptionChiller,
            FuelType.LiquefiedPetroleumGas,
            id: Id("SRC-ABS-DEFAULT"));
        SourceSystem rereadMinimal = ReadRoundTrip(minimal);
        var explicitSource = new SourceSystem(
            "Abs Explicit",
            SourceSystemType.AbsorptionChiller,
            FuelType.LiquefiedPetroleumGas,
            coolingCop: 1.2d,
            coolingCapacity: 12_000d,
            boilerEfficiency: 0.8d,
            id: Id("SRC-ABS-EXPLICIT"));
        ConversionProbe probe = RoundTripAndConvert(explicitSource);
        DragonAbsorptionChiller converted = Assert.IsType<DragonAbsorptionChiller>(probe.ConvertedSource);
        return new[]
        {
            "native.aggregate=" + typeof(SourceSystem).FullName,
            "constructor.type=" + explicitSource.Type,
            "constructor.id=" + explicitSource.Id.Value,
            "constructor.name=" + explicitSource.Name,
            "constructor.fuel=" + explicitSource.FuelType,
            "constructor.cop=" + Double(explicitSource.CoolingCop),
            "constructor.capacity=" + Double(explicitSource.CoolingCapacity),
            "constructor.boiler_efficiency=" + Double(explicitSource.BoilerEfficiency),
            "writer.group=" + SourceGroup(probe.Json),
            "writer.repeat_equal=" + Boolean(probe.WriterRepeatEqual),
            "reader.type=" + probe.RereadSource.Type,
            "reader.id=" + probe.RereadSource.Id.Value,
            "reader.cop=" + Double(probe.RereadSource.CoolingCop),
            "reader.capacity=" + Double(probe.RereadSource.CoolingCapacity),
            "reader.boiler_efficiency=" + Double(probe.RereadSource.BoilerEfficiency),
            "reader.default_cop=" + Double(rereadMinimal.CoolingCop),
            "reader.default_boiler_efficiency=" + Double(rereadMinimal.BoilerEfficiency),
            "conversion.success=" + Boolean(probe.Success),
            "conversion.source_type=" + converted.GetType().Name,
            "conversion.id=" + converted.Id.Value,
            "conversion.cop=" + Double(converted.ThermalCoefficientOfPerformance),
            "conversion.capacity=" + Double(converted.NominalCapacityWatts),
            "conversion.generator_type=" + converted.HeatSource.GetType().Name,
            "conversion.generator_fuel=" + converted.HeatSource.Fuel,
            "conversion.generator_efficiency=" + Double(converted.HeatSource.NominalThermalEfficiency),
            "conversion.tower_type=" + converted.CoolingTower.GetType().Name,
            "conversion.fresh=" + Boolean(probe.FreshConversion),
            "invalid.cop_zero=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.AbsorptionChiller, FuelType.Electricity, coolingCop: 0d)),
            "invalid.capacity_zero=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.AbsorptionChiller, FuelType.Electricity, coolingCapacity: 0d)),
            "invalid.boiler_efficiency_above_one=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.AbsorptionChiller, FuelType.Electricity, boilerEfficiency: 1.01d)),
            "invalid.missing_fuel=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.AbsorptionChiller)),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveBoiler()
    {
        var minimal = new SourceSystem(
            "Boiler Default",
            SourceSystemType.Boiler,
            FuelType.NaturalGas,
            hotWaterSupply: false,
            id: Id("SRC-BOILER-DEFAULT"));
        SourceSystem rereadMinimal = ReadRoundTrip(minimal);
        var explicitSource = new SourceSystem(
            "Boiler Explicit",
            SourceSystemType.Boiler,
            FuelType.LiquefiedPetroleumGas,
            heatingCapacity: 15_000d,
            efficiency: 0.92d,
            hotWaterSupply: true,
            id: Id("SRC-BOILER-EXPLICIT"));
        ConversionProbe probe = RoundTripAndConvert(explicitSource);
        DragonBoiler converted = Assert.IsType<DragonBoiler>(probe.ConvertedSource);
        string invalidBooleanJson = ReplaceRequired(
            probe.Json,
            "\"hotwater_supply\":true",
            "\"hotwater_supply\":1");
        return new[]
        {
            "native.aggregate=" + typeof(SourceSystem).FullName,
            "constructor.type=" + explicitSource.Type,
            "constructor.id=" + explicitSource.Id.Value,
            "constructor.name=" + explicitSource.Name,
            "constructor.fuel=" + explicitSource.FuelType,
            "constructor.capacity=" + Double(explicitSource.HeatingCapacity),
            "constructor.efficiency=" + Double(explicitSource.Efficiency),
            "constructor.hotwater=" + Boolean(explicitSource.HotWaterSupply),
            "writer.group=" + SourceGroup(probe.Json),
            "writer.repeat_equal=" + Boolean(probe.WriterRepeatEqual),
            "reader.type=" + probe.RereadSource.Type,
            "reader.id=" + probe.RereadSource.Id.Value,
            "reader.fuel=" + probe.RereadSource.FuelType,
            "reader.capacity=" + Double(probe.RereadSource.HeatingCapacity),
            "reader.efficiency=" + Double(probe.RereadSource.Efficiency),
            "reader.hotwater=" + Boolean(probe.RereadSource.HotWaterSupply),
            "reader.default_efficiency=" + Double(rereadMinimal.Efficiency),
            "conversion.success=" + Boolean(probe.Success),
            "conversion.source_type=" + converted.GetType().Name,
            "conversion.id=" + converted.Id.Value,
            "conversion.fuel=" + converted.Fuel,
            "conversion.capacity=" + Double(converted.NominalCapacityWatts),
            "conversion.efficiency=" + Double(converted.NominalThermalEfficiency),
            "conversion.fresh=" + Boolean(probe.FreshConversion),
            "invalid.capacity_zero=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.Boiler, FuelType.NaturalGas,
                heatingCapacity: 0d, hotWaterSupply: true)),
            "invalid.efficiency_zero=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.Boiler, FuelType.NaturalGas,
                efficiency: 0d, hotWaterSupply: true)),
            "invalid.efficiency_above_one=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.Boiler, FuelType.NaturalGas,
                efficiency: 1.01d, hotWaterSupply: true)),
            "invalid.missing_fuel=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.Boiler, hotWaterSupply: true)),
            "invalid.missing_hotwater=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.Boiler, FuelType.NaturalGas)),
            "invalid.fuel_enum=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.Boiler, (FuelType)int.MaxValue,
                hotWaterSupply: true)),
            "invalid.reader_hotwater.codes=" + DiagnosticCodes(GrmReader.Read(
                invalidBooleanJson,
                SimpleDragonDatabase.Default)),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveChiller()
    {
        var minimal = new SourceSystem(
            "Chiller Default",
            SourceSystemType.Chiller,
            compressorType: CompressorType.Turbo,
            coolingTowerType: CoolingTowerType.Open,
            coolingTowerControl: CoolingTowerControl.SingleSpeed,
            id: Id("SRC-CHILLER-DEFAULT"));
        SourceSystem rereadMinimal = ReadRoundTrip(minimal);
        var explicitSource = new SourceSystem(
            "Chiller SRC-CHILLER-EXPLICIT",
            SourceSystemType.Chiller,
            coolingCop: 4.25d,
            coolingCapacity: 24_000d,
            compressorType: CompressorType.Screw,
            coolingTowerType: CoolingTowerType.Closed,
            coolingTowerCapacity: 31_000d,
            coolingTowerControl: CoolingTowerControl.TwoSpeed,
            id: Id("SRC-CHILLER-EXPLICIT"));
        ConversionProbe probe = RoundTripAndConvert(explicitSource);
        DragonChiller converted = Assert.IsType<DragonChiller>(probe.ConvertedSource);
        string[] towerBranches =
        {
            ConvertedTower(CoolingTowerType.Open, CoolingTowerControl.SingleSpeed),
            ConvertedTower(CoolingTowerType.Open, CoolingTowerControl.TwoSpeed),
            ConvertedTower(CoolingTowerType.Closed, CoolingTowerControl.SingleSpeed),
            ConvertedTower(CoolingTowerType.Closed, CoolingTowerControl.TwoSpeed),
        };
        return new[]
        {
            "native.aggregate=" + typeof(SourceSystem).FullName,
            "constructor.type=" + explicitSource.Type,
            "constructor.id=" + explicitSource.Id.Value,
            "constructor.name=" + explicitSource.Name,
            "constructor.cop=" + Double(explicitSource.CoolingCop),
            "constructor.capacity=" + Double(explicitSource.CoolingCapacity),
            "constructor.compressor=" + explicitSource.CompressorType,
            "constructor.tower_type=" + explicitSource.CoolingTowerType,
            "constructor.tower_capacity=" + Double(explicitSource.CoolingTowerCapacity),
            "constructor.tower_control=" + explicitSource.CoolingTowerControl,
            "writer.group=" + SourceGroup(probe.Json),
            "writer.repeat_equal=" + Boolean(probe.WriterRepeatEqual),
            "reader.type=" + probe.RereadSource.Type,
            "reader.id=" + probe.RereadSource.Id.Value,
            "reader.cop=" + Double(probe.RereadSource.CoolingCop),
            "reader.capacity=" + Double(probe.RereadSource.CoolingCapacity),
            "reader.compressor=" + probe.RereadSource.CompressorType,
            "reader.tower_type=" + probe.RereadSource.CoolingTowerType,
            "reader.tower_capacity=" + Double(probe.RereadSource.CoolingTowerCapacity),
            "reader.tower_control=" + probe.RereadSource.CoolingTowerControl,
            "reader.default_cop=" + Double(rereadMinimal.CoolingCop),
            "conversion.success=" + Boolean(probe.Success),
            "conversion.source_type=" + converted.GetType().Name,
            "conversion.id=" + converted.Id.Value,
            "conversion.cop=" + Double(converted.ReferenceCoefficientOfPerformance),
            "conversion.capacity=" + Double(converted.NominalCapacityWatts),
            "conversion.compressor=" + converted.Compressor,
            "conversion.tower_type=" + converted.CoolingTower.GetType().Name,
            "conversion.tower_capacity=" + Double(CoolingTowerCapacity(converted.CoolingTower)),
            "conversion.branches=" + Join(towerBranches),
            "conversion.fresh=" + Boolean(probe.FreshConversion),
            "invalid.cop_zero=" + ExceptionFact(() => _ = Chiller(coolingCop: 0d)),
            "invalid.capacity_zero=" + ExceptionFact(() => _ = Chiller(coolingCapacity: 0d)),
            "invalid.tower_capacity_zero=" + ExceptionFact(() => _ = Chiller(coolingTowerCapacity: 0d)),
            "invalid.compressor_enum=" + ExceptionFact(() => _ = Chiller(
                compressorType: (CompressorType)int.MaxValue)),
            "invalid.tower_type_enum=" + ExceptionFact(() => _ = Chiller(
                coolingTowerType: (CoolingTowerType)int.MaxValue)),
            "invalid.tower_control_enum=" + ExceptionFact(() => _ = Chiller(
                coolingTowerControl: (CoolingTowerControl)int.MaxValue)),
            "invalid.missing_components=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.Chiller)),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveDistrictHeating()
    {
        var falseSource = new SourceSystem(
            "District False",
            SourceSystemType.DistrictHeating,
            hotWaterSupply: false,
            id: Id("SRC-DISTRICT-FALSE"));
        SourceSystem rereadFalse = ReadRoundTrip(falseSource);
        var trueSource = new SourceSystem(
            "District True",
            SourceSystemType.DistrictHeating,
            hotWaterSupply: true,
            id: Id("SRC-DISTRICT-TRUE"));
        ConversionProbe probe = RoundTripAndConvert(trueSource);
        DragonDistrictHeating converted = Assert.IsType<DragonDistrictHeating>(probe.ConvertedSource);
        string invalidBooleanJson = ReplaceRequired(
            probe.Json,
            "\"hotwater_supply\":true",
            "\"hotwater_supply\":1");
        return new[]
        {
            "native.aggregate=" + typeof(SourceSystem).FullName,
            "constructor.type=" + trueSource.Type,
            "constructor.id=" + trueSource.Id.Value,
            "constructor.name=" + trueSource.Name,
            "constructor.hotwater=" + Boolean(trueSource.HotWaterSupply),
            "writer.group=" + SourceGroup(probe.Json),
            "writer.repeat_equal=" + Boolean(probe.WriterRepeatEqual),
            "reader.type=" + probe.RereadSource.Type,
            "reader.id=" + probe.RereadSource.Id.Value,
            "reader.hotwater=" + Boolean(probe.RereadSource.HotWaterSupply),
            "reader.false_hotwater=" + Boolean(rereadFalse.HotWaterSupply),
            "conversion.success=" + Boolean(probe.Success),
            "conversion.source_type=" + converted.GetType().Name,
            "conversion.id=" + converted.Id.Value,
            "conversion.capacity=" + Double(converted.NominalCapacityWatts),
            "conversion.fresh=" + Boolean(probe.FreshConversion),
            "python.dragon_type=Boiler",
            "native.dragon_type=DistrictHeating",
            "invalid.missing_hotwater=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.DistrictHeating)),
            "invalid.reader_hotwater.codes=" + DiagnosticCodes(GrmReader.Read(
                invalidBooleanJson,
                SimpleDragonDatabase.Default)),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveGeothermalHeatPump()
    {
        var minimal = new SourceSystem(
            "Geo Default",
            SourceSystemType.GeothermalHeatPump,
            FuelType.Electricity,
            id: Id("SRC-GEO-DEFAULT"));
        SourceSystem rereadMinimal = ReadRoundTrip(minimal);
        var explicitSource = new SourceSystem(
            "Geo Explicit",
            SourceSystemType.GeothermalHeatPump,
            FuelType.Electricity,
            heatingCop: 4.5d,
            coolingCop: 5d,
            heatingCapacity: 18_000d,
            coolingCapacity: 16_000d,
            id: Id("SRC-GEO-EXPLICIT"));
        ConversionProbe probe = RoundTripAndConvert(explicitSource);
        DragonGeothermalHeatPump converted = Assert.IsType<DragonGeothermalHeatPump>(probe.ConvertedSource);
        return new[]
        {
            "native.aggregate=" + typeof(SourceSystem).FullName,
            "constructor.type=" + explicitSource.Type,
            "constructor.id=" + explicitSource.Id.Value,
            "constructor.fuel=" + explicitSource.FuelType,
            "constructor.heating_cop=" + Double(explicitSource.HeatingCop),
            "constructor.cooling_cop=" + Double(explicitSource.CoolingCop),
            "constructor.heating_capacity=" + Double(explicitSource.HeatingCapacity),
            "constructor.cooling_capacity=" + Double(explicitSource.CoolingCapacity),
            "writer.group=" + SourceGroup(probe.Json),
            "writer.repeat_equal=" + Boolean(probe.WriterRepeatEqual),
            "reader.type=" + probe.RereadSource.Type,
            "reader.id=" + probe.RereadSource.Id.Value,
            "reader.default_heating_cop=" + Double(rereadMinimal.HeatingCop),
            "reader.default_cooling_cop=" + Double(rereadMinimal.CoolingCop),
            "conversion.success=" + Boolean(probe.Success),
            "conversion.source_type=" + converted.GetType().Name,
            "conversion.base_type=" + converted.GetType().BaseType!.Name,
            "conversion.id=" + converted.Id.Value,
            "conversion.fuel=" + converted.Fuel,
            "conversion.heating_cop=" + Double(converted.HeatingCoefficientOfPerformance),
            "conversion.cooling_cop=" + Double(converted.CoolingCoefficientOfPerformance),
            "conversion.heating_capacity=" + Double(converted.HeatingCapacityWatts),
            "conversion.cooling_capacity=" + Double(converted.CoolingCapacityWatts),
            "conversion.fresh=" + Boolean(probe.FreshConversion),
            "invalid.missing_fuel=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.GeothermalHeatPump)),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] ObserveHeatPump()
    {
        var minimal = new SourceSystem(
            "HeatPump Default",
            SourceSystemType.HeatPump,
            FuelType.Electricity,
            id: Id("SRC-HP-DEFAULT"));
        SourceSystem rereadMinimal = ReadRoundTrip(minimal);
        var explicitSource = new SourceSystem(
            "HeatPump Explicit",
            SourceSystemType.HeatPump,
            FuelType.NaturalGas,
            heatingCop: 3.5d,
            coolingCop: 4d,
            heatingCapacity: 14_000d,
            coolingCapacity: 12_000d,
            id: Id("SRC-HP-EXPLICIT"));
        ConversionProbe probe = RoundTripAndConvert(explicitSource);
        DragonHeatPump converted = Assert.IsType<DragonHeatPump>(probe.ConvertedSource);
        return new[]
        {
            "native.aggregate=" + typeof(SourceSystem).FullName,
            "constructor.type=" + explicitSource.Type,
            "constructor.id=" + explicitSource.Id.Value,
            "constructor.name=" + explicitSource.Name,
            "constructor.fuel=" + explicitSource.FuelType,
            "constructor.heating_cop=" + Double(explicitSource.HeatingCop),
            "constructor.cooling_cop=" + Double(explicitSource.CoolingCop),
            "constructor.heating_capacity=" + Double(explicitSource.HeatingCapacity),
            "constructor.cooling_capacity=" + Double(explicitSource.CoolingCapacity),
            "writer.group=" + SourceGroup(probe.Json),
            "writer.repeat_equal=" + Boolean(probe.WriterRepeatEqual),
            "reader.type=" + probe.RereadSource.Type,
            "reader.id=" + probe.RereadSource.Id.Value,
            "reader.fuel=" + probe.RereadSource.FuelType,
            "reader.heating_cop=" + Double(probe.RereadSource.HeatingCop),
            "reader.cooling_cop=" + Double(probe.RereadSource.CoolingCop),
            "reader.heating_capacity=" + Double(probe.RereadSource.HeatingCapacity),
            "reader.cooling_capacity=" + Double(probe.RereadSource.CoolingCapacity),
            "reader.default_heating_cop=" + Double(rereadMinimal.HeatingCop),
            "reader.default_cooling_cop=" + Double(rereadMinimal.CoolingCop),
            "conversion.success=" + Boolean(probe.Success),
            "conversion.source_type=" + converted.GetType().Name,
            "conversion.id=" + converted.Id.Value,
            "conversion.fuel=" + converted.Fuel,
            "conversion.heating_cop=" + Double(converted.HeatingCoefficientOfPerformance),
            "conversion.cooling_cop=" + Double(converted.CoolingCoefficientOfPerformance),
            "conversion.heating_capacity=" + Double(converted.HeatingCapacityWatts),
            "conversion.cooling_capacity=" + Double(converted.CoolingCapacityWatts),
            "conversion.fresh=" + Boolean(probe.FreshConversion),
            "invalid.heating_cop_zero=" + ExceptionFact(() => _ = HeatPump(heatingCop: 0d)),
            "invalid.cooling_cop_zero=" + ExceptionFact(() => _ = HeatPump(coolingCop: 0d)),
            "invalid.heating_capacity_zero=" + ExceptionFact(() => _ = HeatPump(heatingCapacity: 0d)),
            "invalid.cooling_capacity_zero=" + ExceptionFact(() => _ = HeatPump(coolingCapacity: 0d)),
            "invalid.fuel_enum=" + ExceptionFact(() => _ = HeatPump(fuelType: (FuelType)int.MaxValue)),
            "invalid.missing_fuel=" + ExceptionFact(() => _ = new SourceSystem(
                "invalid", SourceSystemType.HeatPump)),
            "native.route=SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static SourceSystem Chiller(
        double? coolingCop = 4.25d,
        double? coolingCapacity = 24_000d,
        CompressorType? compressorType = CompressorType.Screw,
        CoolingTowerType? coolingTowerType = CoolingTowerType.Closed,
        double? coolingTowerCapacity = 31_000d,
        CoolingTowerControl? coolingTowerControl = CoolingTowerControl.TwoSpeed,
        string id = "SRC-CHILLER-PROBE") => new(
            "Chiller probe",
            SourceSystemType.Chiller,
            coolingCop: coolingCop,
            coolingCapacity: coolingCapacity,
            compressorType: compressorType,
            coolingTowerType: coolingTowerType,
            coolingTowerCapacity: coolingTowerCapacity,
            coolingTowerControl: coolingTowerControl,
            id: Id(id));

    private static SourceSystem HeatPump(
        FuelType? fuelType = FuelType.Electricity,
        double? heatingCop = 3.5d,
        double? coolingCop = 4d,
        double? heatingCapacity = 14_000d,
        double? coolingCapacity = 12_000d) => new(
            "Heat pump probe",
            SourceSystemType.HeatPump,
            fuelType,
            heatingCop,
            coolingCop,
            heatingCapacity,
            coolingCapacity,
            id: Id("SRC-HP-PROBE"));

    private static string ConvertedTower(CoolingTowerType type, CoolingTowerControl control)
    {
        ConversionProbe probe = RoundTripAndConvert(Chiller(
            coolingTowerType: type,
            coolingTowerControl: control,
            id: "SRC-CHILLER-" + type + "-" + control));
        return Assert.IsType<DragonChiller>(probe.ConvertedSource).CoolingTower.GetType().Name;
    }

    private static double? CoolingTowerCapacity(Dragons.InvisibleDragon.Hvac.CoolingTower tower)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(
            tower.GetType().GetProperty("NominalCapacityWatts"));
        return (double?)property.GetValue(tower);
    }

    private static SourceSystem ReadRoundTrip(SourceSystem source)
    {
        GreenRetrofitModel model = CreateModel(new[] { source });
        string json = GrmWriter.Serialize(model, indented: false);
        GrmReadResult result = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(result.Success, Describe(result.Diagnostics));
        GreenRetrofitModel reread = result.RequireModel();
        Assert.Equal(json, GrmWriter.Serialize(reread, indented: false));
        return Assert.Single(reread.SourceSystems);
    }

    private static ConversionProbe RoundTripAndConvert(SourceSystem source)
    {
        SupplySystem supply = CreateSupply(source);
        GreenRetrofitModel model = CreateModel(new[] { source }, new[] { supply }, supply);
        string json = GrmWriter.Serialize(model, indented: false);
        GrmReadResult read = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(read.Success, Describe(read.Diagnostics));
        GreenRetrofitModel rereadModel = read.RequireModel();
        SourceSystem rereadSource = Assert.Single(rereadModel.SourceSystems);
        bool writerRepeatEqual = json == GrmWriter.Serialize(rereadModel, indented: false);
        Assert.True(writerRepeatEqual);

        GreenRetrofitConversionResult first = Convert(model);
        GreenRetrofitConversionResult second = Convert(model);
        DragonSourceSystem converted = Assert.IsAssignableFrom<DragonSourceSystem>(OnlySupply(first).Source);
        DragonSourceSystem convertedAgain = Assert.IsAssignableFrom<DragonSourceSystem>(OnlySupply(second).Source);
        return new ConversionProbe(
            json,
            rereadSource,
            converted,
            first.Success,
            writerRepeatEqual,
            !ReferenceEquals(converted, convertedAgain));
    }

    private static SupplySystem CreateSupply(SourceSystem source)
    {
        SupplySystemType type = source.Type is SourceSystemType.HeatPump
            or SourceSystemType.GeothermalHeatPump
            ? SupplySystemType.AirHandlingUnit
            : SupplySystemType.FanCoilUnit;
        return new SupplySystem(
            "Supply for " + source.Id.Value,
            type,
            source.Id.Value,
            source,
            id: Id("SUPPLY-FOR-" + source.Id.Value));
    }

    private static GreenRetrofitModel CreateModel(
        IEnumerable<SourceSystem> sources,
        IEnumerable<SupplySystem>? supplies = null,
        SupplySystem? assignedSupply = null)
    {
        GreenRetrofitModel template = GrmReader.ReadFile(
            FindRepositoryFile(NativeTemplatePath),
            SimpleDragonDatabase.Default).RequireModel();
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

    private static GreenRetrofitConversionResult Convert(GreenRetrofitModel model)
    {
        GreenRetrofitConversionResult result = GreenRetrofitConverter.Convert(
            model,
            new GreenRetrofitConversionOptions
            {
                Database = SimpleDragonDatabase.Default,
                IncludeModelValidationDiagnostics = false,
            });
        Assert.True(result.Success, Describe(result.Diagnostics));
        Assert.NotNull(result.RequireEnergyModel());
        return result;
    }

    private static DragonSupplySystem OnlySupply(GreenRetrofitConversionResult result)
    {
        EnergyModel model = result.RequireEnergyModel();
        return Assert.Single(Assert.Single(model.HvacAssignments).Supply.Systems);
    }

    private static string SourceGroup(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Assert.Single(document.RootElement
            .GetProperty("building")
            .GetProperty("source_systems")
            .EnumerateObject()).Name;
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

    private static string DiagnosticCodes(GrmReadResult result) => Join(
        result.Diagnostics.Select(item => item.Code).OrderBy(item => item, StringComparer.Ordinal));

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
                python_support = Artifact(SupportPath, SupportBytes, SupportSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                native_data = Artifact(NativeTemplatePath, NativeTemplateBytes, NativeTemplateSha256),
                native_sources = NativeSources
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
                exact_target_count = 47,
                equivalent_target_count = 24,
                exception_target_count = 23,
                exact_case_count = 6,
                adjacent_count_not_recorded = 155,
                adjacent_receipts_sha256 = AdjacentReceiptsSha256,
                fixture_repository_commit = FixtureRepositoryCommit,
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
                target_receipts_sha256 = TargetReceiptsSha256,
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
        AssertKeys(
            observed,
            "case_code",
            "case_id",
            "native_fact_count",
            "native_facts",
            "native_facts_sha256",
            "native_outcome",
            "python_facts_sha256");
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
        AssertKeys(
            artifacts,
            "fixture",
            "generator",
            "native_data",
            "native_sources",
            "public_inventory",
            "python_support",
            "python_validator");
        AssertReceiptArtifact(artifacts.GetProperty("fixture"), FixturePath, FixtureBytes, FixtureSha256);
        AssertReceiptArtifact(artifacts.GetProperty("generator"), GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertReceiptArtifact(artifacts.GetProperty("python_support"), SupportPath, SupportBytes, SupportSha256);
        AssertReceiptArtifact(artifacts.GetProperty("python_validator"), ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertReceiptArtifact(artifacts.GetProperty("public_inventory"), InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertReceiptArtifact(artifacts.GetProperty("native_data"), NativeTemplatePath, NativeTemplateBytes, NativeTemplateSha256);
        AssertReceiptArtifactArray(artifacts.GetProperty("native_sources"), NativeSources);

        JsonElement scope = receipt.GetProperty("scope");
        AssertKeys(
            scope,
            "adjacent_count_not_recorded",
            "adjacent_receipts_sha256",
            "claim_policy",
            "equivalent_target_count",
            "exact_case_count",
            "exact_target_count",
            "exception_target_count",
            "fixture_repository_commit");
        Assert.Equal(47, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(24, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(23, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(6, scope.GetProperty("exact_case_count").GetInt32());
        Assert.Equal(155, scope.GetProperty("adjacent_count_not_recorded").GetInt32());
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(scope, "adjacent_receipts_sha256"));
        Assert.Equal(FixtureRepositoryCommit, RequiredString(scope, "fixture_repository_commit"));

        JsonElement upstream = receipt.GetProperty("upstream");
        AssertKeys(
            upstream,
            "ast_sha256",
            "commit",
            "inventory_content_sha256",
            "source_bytes",
            "source_path",
            "source_sha256",
            "target_receipts_sha256");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "source_path"));
        Assert.Equal(UpstreamBytes, upstream.GetProperty("source_bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(upstream, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(upstream, "ast_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_content_sha256"));
        Assert.Equal(TargetReceiptsSha256, RequiredString(upstream, "target_receipts_sha256"));
    }

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
    };

    private static void AssertArtifactArray(JsonElement value, IReadOnlyList<ArtifactPin> expected)
    {
        JsonElement[] items = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, items.Length);
        for (int index = 0; index < items.Length; index++)
        {
            AssertArtifact(items[index], expected[index].Path, expected[index].Bytes, expected[index].Sha256);
        }
    }

    private static void AssertArtifact(JsonElement value, string path, int bytes, string sha256)
    {
        AssertKeys(value, "bytes", "path", "sha256");
        Assert.Equal(path, RequiredString(value, "path"));
        Assert.Equal(bytes, value.GetProperty("bytes").GetInt32());
        Assert.Equal(sha256, RequiredString(value, "sha256"));
    }

    private static void AssertReceiptArtifactArray(JsonElement value, IReadOnlyList<ArtifactPin> expected)
    {
        JsonElement[] items = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, items.Length);
        for (int index = 0; index < items.Length; index++)
        {
            AssertReceiptArtifact(
                items[index],
                expected[index].Path,
                expected[index].Bytes,
                expected[index].Sha256);
        }
    }

    private static void AssertReceiptArtifact(JsonElement value, string path, int bytes, string sha256)
    {
        AssertArtifact(value, path, bytes, sha256);
    }

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

    private static EntityId Id(string value) => new(value);

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

    private static string Double(double? value) => value.HasValue
        ? value.Value.ToString("R", CultureInfo.InvariantCulture)
        : "null";

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string Boolean(bool? value) => value.HasValue ? Boolean(value.Value) : "null";

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

    private static string Describe(IEnumerable<Diagnostic> diagnostics) => string.Join(
        " | ",
        diagnostics.Select(item => item.Code + ":" + item.Severity + ":" + item.Message));

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string FactsSha256,
        string CaseSha256,
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
        int[] AdjacentIndices);

    private sealed record ConversionProbe(
        string Json,
        SourceSystem RereadSource,
        DragonSourceSystem ConvertedSource,
        bool Success,
        bool WriterRepeatEqual,
        bool FreshConversion);
}
