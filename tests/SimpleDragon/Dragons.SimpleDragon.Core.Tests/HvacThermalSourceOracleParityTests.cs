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
    private const int FixtureBytes = 135_657;
    private const string FixtureSha256 =
        "sha256:e78e8bcbe42cd236775db63d50088bad82a9e9c5328e5fa5de6873d069984391";
    private const string FixtureSchema =
        "dragons.python-reference.epsimple-hvac-thermal-source.v1";
    private const string FixtureRepositoryCommit = "5a1e2bb";
    private const string CasesSha256 =
        "sha256:1648981844e29967326b4caeb0b466238e12c07e43fb25469d7325b73ac3feb2";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_hvac_thermal_source_oracle.py";
    private const int GeneratorBytes = 63_818;
    private const string GeneratorSha256 =
        "sha256:e930c9242c76b48500010e76f625e41baa07de96e4629b447df61db6c571e51c";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_hvac_thermal_source_oracle.py";
    private const int ValidatorBytes = 18_337;
    private const string ValidatorSha256 =
        "sha256:ca7fb52d4a68ada17437d9e4590b129cf22cce842b37147aacf76d4f17c92265";
    private const string SupportPath =
        "tools/python-reference/generate_epsimple_hvac_enums_base_oracle.py";
    private const int SupportBytes = 61_458;
    private const string SupportSha256 =
        "sha256:eaa5691d29c341844097c8690f0e12970824494f1e00e8287811b7876ba3df0d";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
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
        "sha256:862de5307de7fcebe494fb4c6691e3ac3ea7fb25abe4a8743f922e553fa9f215";
    private const string NativeSourcesSha256 =
        "sha256:25f9f683ca28ab928c3cad112c2a3f06cbab26da5c6c3d2867fd343d32c80d6f";
    private const string NativeReviewSha256 =
        "sha256:b2c6200243aaab4deb60dcf11ef8216a4215043b53d9f54f759f09193f8db415";
    private const string NativeTemplatePath =
        "fixtures/simple-dragon/grm/ASHRAE 140 modified.grm";
    private const int NativeTemplateBytes = 9_154;
    private const string NativeTemplateSha256 =
        "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.HvacThermalSourceOracleParityTests.MatchesPinnedHvacThermalSourcesThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SourceSystem.cs", 6_894,
            "sha256:c96df1bb42da5df66b3c4cbf61b800c9bf8450b4b8e427d97929809bca4e8cad"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_650,
            "sha256:d91f90946ec19602751fc7818484ca43f85d1c46f9905fa805d8ee8a7281d968"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_652,
            "sha256:4048cc4bdfca312a7baae54c7055bb3aa7177ee6a8143ed9ef1d182353df1842"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_343,
            "sha256:0a0774b4461442b2a3cccf68d39fbc236104a2aa13611e0d27c38f27aa2fe5fd"),
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
        new(32, "sha256:a86686bb3df409087d84e8045652b590ae544e815bb624110887de8c0f82d61d"),
        new(32, "sha256:2fea00bd3688a7fefcc2bed02c3709023cfda6c42de6f1f430656851f0d53109"),
        new(39, "sha256:c90ae9c009419150a28f980b16ded1c0c544ca02c0eaadfab8bad751451a0859"),
        new(21, "sha256:cae9dd047fcb2f0c21fffef9571916cf8681db00429141a73056d0b231228c52"),
        new(26, "sha256:76f5661c09f9fe656fcaab83e9be2f2d88d1dc571078c127f38bf33cb01f2f7b"),
        new(36, "sha256:7c97dc43e9cc85c923281e34a80077c17c5c5ff2e4415b0d3aee9bdbffcbb8cf"),
    ];

    private static readonly string[] ExpectedReceiptHashes =
    [
        "sha256:b01cac1e77058e16a7983fc450a2a506ff8320694ef32a8386261d19bd562228",
        "sha256:e2807b07877fbc4a57a5c41412d0a6d00f1eaf518be3e5ab6c3f676c0c8f7105",
        "sha256:b8c28c9700ef78f23fb08bf066f492382ea267dc47d0ea0bdf44805eaa446cb6",
        "sha256:443ff1edfd5f2c66875c89dd83da495f0c1d67ff15aac25a8be411cb9111ddae",
        "sha256:eb188a1c55dd80c62e60cf46ad73b0212ad25c663988e2fca512a0cac7269d73",
        "sha256:7b9e444c554b277bc6c25e50c5f2fb593fcf46c6a20a1ad504e7cf018c3162c5",
        "sha256:e1e8a7945b3d217578963124d93113f58706b971392ed7a7f2767fff53334cf1",
        "sha256:35dcdbb2459fe5bc79159139a44505edf40b0142758dbdfce1fa51558e45e2d2",
        "sha256:e8d34b317151abcd01fd4f1f5d32d5c203a8cd4d5e7ff5c5d5d1b115aa306676",
        "sha256:73c8ae28eaebd3a5a0af93d5c93102160a472d1c951086a9a78284b8eb1e0665",
        "sha256:597d4bd26dc0d254e48847ee6a92e65d2ba8529d65b549ff7ac93345bd4ccade",
        "sha256:bc7d81bca94157cbed1f0ba500a69311eed31e5752ab97498d7dcf4b47266ea0",
        "sha256:fd09865e22ed85ed11ed4ed6b4f0e6111bb5af81daa1415e8525792e6e6839c4",
        "sha256:efe5613d1debda4395cedecea0ca9dc86bad0de877971102d1a4f8da8f5728e8",
        "sha256:8f568b733f99135309eef567ce0a70a11da4ff10413f8ad1695db3e8dbefe94f",
        "sha256:e191ff932bde186162fe0cd69b2d285c7ae28da8ffa48d7fb9e4ba5c63df730a",
        "sha256:f444c7a0e2eda4e7670786d5891548c75407b6ae554eb61c32f77098de8ae6e3",
        "sha256:ca303cbed2055515509c57875e937cb5c3effc877931cfc9aed1a985f5c6ab76",
        "sha256:63dc65460b601f46c2148889cdc5e01c780bc404483b7de17325ecf543fce51b",
        "sha256:d41ac73cca069e0409a64018d391d89e38c998b3481542f89b14d9bb3bd6114e",
        "sha256:b20b573770676374be84a4f432cdfadffd33c71bc574e69a4ced07addf923719",
        "sha256:f162fbfdca858a730516fea9e3f5d1cec8ff8b61c931addd3d34457f9e8a3da5",
        "sha256:5865691c7c4a37cfeae5335fa32f0e7e70bc56107bd4b2b54bb815484d19d21f",
        "sha256:f250e5f747fdc9742f4e762cd8ecde6000aaa95b1c0c7ac17b1dcd81754d054b",
        "sha256:52938dac7a9eae1f56b7033a77c16ba17cb5ad3afe451412ecf049d9c93a55a4",
        "sha256:7fb5cae698d0a81ea8e87f9539fc617f3b5ac6bf9d7c7de34d4da93bcc7e3602",
        "sha256:937f94dcbcb847c917e5b8889321efd1351f78a5721056badc1f7c0f5a0004e5",
        "sha256:2baa4e28157b627186ecc53b329a4496d72a12e44aba1dcf6c9bb5a66352128f",
        "sha256:637a0279f72769662574e3270633d717861542642d470fd42c28f6128c81687f",
        "sha256:e75de4d130c41dec81f2c7274a259b93ada70a2b4a7288c1e59cfdf00fb690a4",
        "sha256:48ce5c4eedeedbde292aeb1b006b463cd839e78d2e8802fc5ac109bc5581413d",
        "sha256:55ba954a6faf8ab7075c472a5da79ef53e0e8a765a904a6ebb9f37ec4cb1b3d8",
        "sha256:b28ff444366cfc2c6b9669fe340299a62cc0b15268a59bdba1b9cb2fbd47538f",
        "sha256:33711b79744ce3d4d235e37a1168bd0d17c6b0280782ce0bd591e860dd7cb318",
        "sha256:63d381b7f18a487e9847e8cf17119436ec1474a2b4e3e9e9a9e05ffa04e2c22e",
        "sha256:53bf4542b479ce9fcdaf2d8fb0a527d0b2b717dc63fa77a16db9bbc31c88eaf5",
        "sha256:711a610a7897021d5d633c7dff4d593cca95ac56178f20ef5ba3ea5da1a8c270",
        "sha256:1b0e3bd88c76060e1f2a360d059e4d9970e99b5a76b474d5dc80e547c12349d3",
        "sha256:8bd0239baea3e3a9de2829b1c6182d8d714d9a07bb00f196e9c89812ca7451b3",
        "sha256:77a3fe40c8082977dcd69a12f2c4911ebeada2ebab8812e66828cd945c6fe592",
        "sha256:e9b9b76defee4b9a8a31a170186c2ccef8538f4575ad261f166b6b17056cbb60",
        "sha256:701e7853d409f7c47b1c77201165246eff30c84787d194fc9a595a624f920df1",
        "sha256:d1742977d40381a6a2858af4f66a2341db67040c9c5b7ca2b2c68bc9b9b54a1f",
        "sha256:f8d1d0734360982358cdbfeffd18f7292993fe58584920002cc91028666a7367",
        "sha256:498330b1e98a32216a8fd3753fce253e6459eb2f817a71a1c4f097e0f189a362",
        "sha256:51705a2ee7763d79d00225590f46ff7e88ff62e0aaade38c9466df60241d988d",
        "sha256:12349574668d8886aacf0b0fd22c56fa19449cebc2b05cc34e76dc5318346135",
    ];


    private static readonly string[] ExpectedCollectorOutputHashes =
    [
        "sha256:b716febd96c3b2b7b8179d0c092443be3b2568973acd4d333f141afcb4a79f73", // epsimple-hvac-thermal-source-135-c44e12f9
        "sha256:4f3ca9cece28d4392f8184bd8eb6efa013a4dea2d52f776ee3cf5f34f004438c", // epsimple-hvac-thermal-source-136-246156d9
        "sha256:3634f3aed984b29f2d6ab8b5fc2af86157bc9830ad73e05120bc3fdbde4e8785", // epsimple-hvac-thermal-source-139-4aae19c6
        "sha256:852fd186de4bf39f2c39da5b676de8c88866fbfc0d8f2e40e06d3ac714634872", // epsimple-hvac-thermal-source-142-be052579
        "sha256:b201e7fe0af6577a12e170600fbd5153f147303c711e660032dcdf90fd181a43", // epsimple-hvac-thermal-source-143-d699d5f1
        "sha256:6a6c2852004ce82f007b81124f645391da1bfc383626ba2114fe2d5af9e14d3a", // epsimple-hvac-thermal-source-144-253d21d2
        "sha256:ba927766ab1f387b163bdc35c9f949323ece65062eeaf25d7f1bac0de8e64c92", // epsimple-hvac-thermal-source-145-f305d756
        "sha256:b0670a6a45214ee1c789b7e6da8c12a1b18497f7d39ee2be99b2649229b67433", // epsimple-hvac-thermal-source-146-7a12c015
        "sha256:e5bebd5b0a67e654fcd026c62af2ed148cc2ae7fd6ed612c56e4952824801ac5", // epsimple-hvac-thermal-source-157-8d52ff9e
        "sha256:7e3264fd1a4b2333ce91ab4d1e8ed4e6ffc2e5446afeb0a3e6d4dcfb8e605b64", // epsimple-hvac-thermal-source-158-246156d9
        "sha256:af1991ecf3a5b89169a37a44bf2be5160166cfd55e2ccade4b0633ad2c60a429", // epsimple-hvac-thermal-source-161-f45db90e
        "sha256:e64c317a3ec71fc1e9c47bb92f2daa89a65410f4fd0c583342cad308f5660aac", // epsimple-hvac-thermal-source-164-d699d5f1
        "sha256:cab7a61a9531e151bf49e31b7480fa159dbeb8003c5d256637d1fb4841d8302e", // epsimple-hvac-thermal-source-165-80144f2f
        "sha256:2e4a0f94410af992c78f7197a507c330f2bbdc127dc84f539ba1a022b756edc5", // epsimple-hvac-thermal-source-166-bd3f1e5a
        "sha256:a8c933e84abb05bf99b44f3ccda3321c6eb68d518c19e13ae84ea8b08b0e9364", // epsimple-hvac-thermal-source-167-64d0443e
        "sha256:4fe87b5b667657385bcd50410616672b367ae0f852ece6298704ad9ac78274b5", // epsimple-hvac-thermal-source-168-f9effaf3
        "sha256:9d88904657f7c5bb19ee024790d7ffd998296e4f678b149ad6aef5f59f5165c1", // epsimple-hvac-thermal-source-169-86b77a93
        "sha256:d37a13f01e7a27468325c3dc0b015814d2a6b81f3ff91647b4bbf1efe5b3ede4", // epsimple-hvac-thermal-source-170-8baa00de
        "sha256:475bcda1b622cead02bc5c9385128e7f5a23c9a63cd6878eaba6252eae3de545", // epsimple-hvac-thermal-source-171-246156d9
        "sha256:288cdb3354822d77d51764b89dc54d61cc07065db67d1e93a0c3d7e36f00df85", // epsimple-hvac-thermal-source-174-9c5215c4
        "sha256:98a4fe46bbdaaac41d196048670807fb76e4c78a8a3594db665a14475a914c0c", // epsimple-hvac-thermal-source-177-d699d5f1
        "sha256:2fe3451618f68e088360fcb36739a90402d3b7679782cafc251c272b538c55d0", // epsimple-hvac-thermal-source-178-000c99e3
        "sha256:1eba63512e312c2cfc4be2a2b50069104442b1fae22d167c9af526ba7be09a86", // epsimple-hvac-thermal-source-179-e56b52fb
        "sha256:642f0633dc7956582014cdd34d7867e346f692d4ba4c989717d8083379c6bdf0", // epsimple-hvac-thermal-source-180-473c615a
        "sha256:9e06fc69950f2bbb034e06df3b9b5f6cf1c0458bb44f5aa8f9658c76866ca02a", // epsimple-hvac-thermal-source-181-75acdde9
        "sha256:32ab862e5152b822ed5341df81e945a0411289d440f5c5cf411a4eba15f7c946", // epsimple-hvac-thermal-source-182-253d21d2
        "sha256:7ef5b5319454472a76dd7f041f12cc162395defd8bbf74907eaee917fb7469ac", // epsimple-hvac-thermal-source-183-ca5a6445
        "sha256:95bf0d07d127e0c57a6eb8b06991c2cd8c70530daf839d1d4aba78b10bdbc245", // epsimple-hvac-thermal-source-184-b3b58ae8
        "sha256:473e79b7bc45e8d18a755891c835abc3b2508c3c7b422ca939f0d236c8bd945a", // epsimple-hvac-thermal-source-199-a1c6d574
        "sha256:adf04da0ce5f264d9af58f215c696699bac6f924d432c97038d17c45a11cb09d", // epsimple-hvac-thermal-source-200-246156d9
        "sha256:ce3644625cd3d2c278ced579e32206de7313cb5adbcf128a3f3c1855a25610dc", // epsimple-hvac-thermal-source-203-f477c20b
        "sha256:914d350fdd81e3aa942b3915779d0d945c9b045c9b39058df870e9a0be428206", // epsimple-hvac-thermal-source-206-c53a5bbb
        "sha256:320f3d7b05fe1dcda83f66e5c8f5f140d761c49fdde0f24acd033671f92ade6f", // epsimple-hvac-thermal-source-207-f9effaf3
        "sha256:afe92a278708a00e24facedf6f9d9b2193e27b9454c2fcee6b4cf1537452d5ac", // epsimple-hvac-thermal-source-208-bf1c4c8b
        "sha256:1353b60f38d8696e33d01e7680579b699f6023b75031567d2aa3fd9cf98d93de", // epsimple-hvac-thermal-source-248-a87f33ee
        "sha256:908ca1834e524eb0aa934863077a4d9c985d0bb752b39f5683ee266e6de55c68", // epsimple-hvac-thermal-source-251-81ac3508
        "sha256:dec5c204d9abbf79eadaaaf673d79914ca7a7327944b6fe3d249b29ccfdd61b4", // epsimple-hvac-thermal-source-252-069a6710
        "sha256:af8aeb5902e4bde1a784e36c10de9b03a31f8b7d98697e1788f573bfe28ae376", // epsimple-hvac-thermal-source-253-3872db31
        "sha256:b377167f5fc91c900e99a19d36f32439fd4bd13947c49eb8a30fbf5d6935436e", // epsimple-hvac-thermal-source-254-246156d9
        "sha256:81707fa359bb1a84f57b9aa444b17bbf83900c711388b6eaea232fd7084860b2", // epsimple-hvac-thermal-source-257-7e88c6cd
        "sha256:01cd52d09f33230ac5563557b13674e3e5df9c375dc2b8aff3ed1b726d6c0387", // epsimple-hvac-thermal-source-260-2c365992
        "sha256:7d4632b913d3eb000b8e70f3c6a75a7cc49cdfac04a0ce9bf2a69c03cc399039", // epsimple-hvac-thermal-source-261-59bd7983
        "sha256:e60b614ca5768313011c8d180407ff10e2fb94a05e55a3652ca351f40d1accfa", // epsimple-hvac-thermal-source-262-20b220f0
        "sha256:64acfc83b9cbc71d1ccf1819b49195cf3b97ba49a94cde36655616a3095c7240", // epsimple-hvac-thermal-source-263-37420422
        "sha256:7610b721ad8a32e35ee1bc690a51b37db593863e85b73ddab84a800173bc1352", // epsimple-hvac-thermal-source-264-b48949da
        "sha256:b24fb0df19a14844039bb768d6e32c701ddf94839e1de93d45a1a67e837448a6", // epsimple-hvac-thermal-source-265-55ddf021
        "sha256:8c1ea607dde3384c3f49283173ed23038f47a638007aafba744698a2546f7d03", // epsimple-hvac-thermal-source-266-0feeee0b
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
            21_114,
            "sha256:4d2dd8d0c487af7a24f93f1e79b9b27ed19676cf7909a8039d90248fd7d6e1bc");
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
