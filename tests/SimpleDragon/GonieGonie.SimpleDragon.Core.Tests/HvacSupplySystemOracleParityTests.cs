#pragma warning disable CA1861 // Inline exact oracle expectations are intentionally immutable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.UpstreamTracker;
using DragonAirHandlingUnit = GonieGonie.InvisibleDragon.Hvac.AirHandlingUnit;
using DragonElectricRadiantFloor = GonieGonie.InvisibleDragon.Hvac.ElectricRadiantFloor;
using DragonElectricRadiator = GonieGonie.InvisibleDragon.Hvac.ElectricRadiator;
using DragonFanCoilUnit = GonieGonie.InvisibleDragon.Hvac.FanCoilUnit;
using DragonPackagedAirConditioner = GonieGonie.InvisibleDragon.Hvac.PackagedAirConditioner;
using DragonRadiantFloor = GonieGonie.InvisibleDragon.Hvac.RadiantFloor;
using DragonRadiator = GonieGonie.InvisibleDragon.Hvac.Radiator;
using DragonSupplySystem = GonieGonie.InvisibleDragon.Hvac.SupplySystem;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class HvacSupplySystemOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-hvac-supply-system-oracle.json";
    private const int FixtureBytes = 168_146;
    private const string FixtureSha256 =
        "sha256:b9a98ea739bf4181a4f93c8bed161f559c03bb93a4926ee56dccc100ddd49d65";
    private const string FixtureSchema =
        "goniegonie.python-reference.epsimple-hvac-supply-system.v1";
    private const string FixtureRepositoryCommit = "6d7ff18";
    private const string CasesSha256 =
        "sha256:844e26e1e019dc9fea4d12cc594c6d83ab3c1823e58ab8253ba809a591dd10a2";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_hvac_supply_system_oracle.py";
    private const int GeneratorBytes = 75_411;
    private const string GeneratorSha256 =
        "sha256:e7874d74d2338c4fa71ab7ddf3cf33b17ce713dcefa0a3d6519cd5a5dd28780d";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_hvac_supply_system_oracle.py";
    private const int ValidatorBytes = 21_729;
    private const string ValidatorSha256 =
        "sha256:91d1c96ea25e25804b747999e80b78993ff1b58fe8563dc32e0ba8f1a73d9534";
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
        "sha256:12d359d81856556caa506bf380f60baddfd1ab46af8042090a77a831c3a467b4";
    private const string LoadedSourcesSha256 =
        "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8";
    private const string RelocatedObservationsSha256 =
        "sha256:9aa09c93f083fd82df4a25c756fbebf5c8138a44db926f316ad42bb298e2fc64";
    private const string TargetReceiptsSha256 =
        "sha256:5753763192194cfdcef58cb9baf438770dd1bd07bb2a4b846c3e8168f032f839";
    private const string AdjacentReceiptsSha256 =
        "sha256:8516665711bdf76cc747fe3843b097c8ee038dde68a4449278c365a0315542d4";
    private const string NativeRoutesSha256 =
        "sha256:578ab9f3b1df7e5a9aa3ca30fe53edcbe8bc2620b41e6cb228267450af455145";
    private const string NativeSourcesSha256 =
        "sha256:26021fb1a71c8c47b7e4d45e31d5703bc2694c996e85d5da670954fab24b2ca0";
    private const string NativeReviewSha256 =
        "sha256:4f5dfc68347827185ddbabfe9734c052342583fe11860eafd207622f5a92cebe";
    private const string NativeTemplatePath =
        "fixtures/simple-dragon/grm/ASHRAE 140 modified.grm";
    private const int NativeTemplateBytes = 9_154;
    private const string NativeTemplateSha256 =
        "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3";
    private const string EvidenceTestCase =
        "GonieGonie.SimpleDragon.Tests.HvacSupplySystemOracleParityTests.MatchesPinnedHvacSupplySystemsThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Hvac/SupplySystem.cs", 6_465,
            "sha256:1858281dcb5ea2df12a09c0c19caba77cf785a10458fb8d265e882f5695a11c5"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmReader.cs", 48_650,
            "sha256:d91f90946ec19602751fc7818484ca43f85d1c46f9905fa805d8ee8a7281d968"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_652,
            "sha256:4048cc4bdfca312a7baae54c7055bb3aa7177ee6a8143ed9ef1d182353df1842"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_343,
            "sha256:0a0774b4461442b2a3cccf68d39fbc236104a2aa13611e0d27c38f27aa2fe5fd"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "epsimple-hvac-supply-system.air-handling-unit-construction-json-source-capabilities-dragon", "air-handling-unit", "sha256:647e2b09be67fe1e7d2af204ed2cad94bf2cf729d6b73976177cf330bd8a7fcf", "sha256:a698272c6fb8e6e337ce12d6f91ee271625452506115f29b4bf385febb5c9462", new[] { "AirHandlingUnit", "AirHandlingUnit.ID", "AirHandlingUnit.__init__", "AirHandlingUnit.from_json", "AirHandlingUnit.source", "AirHandlingUnit.to_dragon" }),
        new("E01", "epsimple-hvac-supply-system.electric-radiant-floor-construction-json-null-source-dragon", "electric-radiant-floor", "sha256:3c35d57c9de0453c01b1baa5267c49f6da094fd78d8a85ecd88bdc1b02b1b6a7", "sha256:40591ce52129ae4400fe15825d3ebe78c8399cbff3b33ab1a32b8b3cfa15475c", new[] { "ElectricRadiantFloor", "ElectricRadiantFloor.ID", "ElectricRadiantFloor.__init__", "ElectricRadiantFloor.from_json", "ElectricRadiantFloor.source", "ElectricRadiantFloor.to_dragon" }),
        new("E02", "epsimple-hvac-supply-system.electric-radiator-capacity-validation-json-null-source-dragon", "electric-radiator", "sha256:5fd43fa8e2f25548aba0db4c4a14480faa98e1a52148962ea2422ac0603a91f3", "sha256:9c2e15187f22a4605cb2fd09eb17d56d4940a3f2f10ae796005cb0272ab904a6", new[] { "ElectricRadiator", "ElectricRadiator.ID", "ElectricRadiator.__init__", "ElectricRadiator.capacity", "ElectricRadiator.from_json", "ElectricRadiator.source", "ElectricRadiator.to_dragon" }),
        new("F01", "epsimple-hvac-supply-system.fan-coil-unit-source-branches-json-dragon", "fan-coil-unit", "sha256:50a57e6921b7bcce8d810224e31861c4222455777e1e9628799b36294667c830", "sha256:347f716087187af48ef8a99e73bf07ea900f9ad6e9ec90e969e4aba72387dfba", new[] { "FanCoilUnit", "FanCoilUnit.ID", "FanCoilUnit.__init__", "FanCoilUnit.from_json", "FanCoilUnit.source", "FanCoilUnit.to_dragon" }),
        new("P01", "epsimple-hvac-supply-system.packaged-air-conditioner-defaults-validation-json-dedicated-dragon", "packaged-air-conditioner", "sha256:3ae220873d535c22a827c1a62e515c7659237a0d9794d3a9907418523a3103e9", "sha256:827b661ffea091fded609e07e1c9ccec144327e54e15230fdfe4bd8b71f89642", new[] { "PackagedAirConditioner", "PackagedAirConditioner.ID", "PackagedAirConditioner.__init__", "PackagedAirConditioner.capacity", "PackagedAirConditioner.cop", "PackagedAirConditioner.from_json", "PackagedAirConditioner.source", "PackagedAirConditioner.to_dragon" }),
        new("R01", "epsimple-hvac-supply-system.radiant-floor-source-capabilities-json-dragon", "radiant-floor", "sha256:24078204d3dce794bcc70ccdd50097fbe04e835b5fc23fd66f2abaa76d24199d", "sha256:e6bfc2d25e7cbc00aea6f5a2a52c7df23f7076f9a5014b4c92e92f3de660c9f4", new[] { "RadiantFloor", "RadiantFloor.ID", "RadiantFloor.__init__", "RadiantFloor.coolable", "RadiantFloor.from_json", "RadiantFloor.heatable", "RadiantFloor.source", "RadiantFloor.to_dragon" }),
        new("R02", "epsimple-hvac-supply-system.radiator-capacity-validation-json-dragon", "radiator", "sha256:ca419685a68ce1fc2810be16b609633882fc0a1d11dd7f7d73eea18e445c9d2e", "sha256:57b8d53ca5fc12088454084dcc0a581c14a46c79bef29d7470f3a2968105815a", new[] { "Radiator", "Radiator.ID", "Radiator.__init__", "Radiator.capacity", "Radiator.from_json", "Radiator.source", "Radiator.to_dragon" }),
        new("S01", "epsimple-hvac-supply-system.supply-system-base-mapper-capability-topology", "supply-system", "sha256:2f715484e1a9a16a8ae0ce1f4dbdda60d9fefe9e5ddfa2f4d522d072353698a1", "sha256:c7ddd975217eab969087a73702d712dcaaf57c58514a79fd93ca22ea5ceb9dc0", new[] { "SupplySystem", "SupplySystem.TYPE_MAPPER", "SupplySystem.coolable", "SupplySystem.heatable" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        Target(147, "AirHandlingUnit", "class", "epsimple-hvac-supply-system-147-6fd0030b", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-6fd0030b", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.AirHandlingUnit and public properties", 0),
        Target(148, "AirHandlingUnit.ID", "function", "epsimple-hvac-supply-system-148-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Id", 0),
        Target(151, "AirHandlingUnit.__init__", "function", "epsimple-hvac-supply-system-151-ea6e311c", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-ea6e311c", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.AirHandlingUnit and public properties", 0),
        Target(154, "AirHandlingUnit.from_json", "function", "epsimple-hvac-supply-system-154-148b0ee3", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-148b0ee3", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 0),
        Target(155, "AirHandlingUnit.source", "function", "epsimple-hvac-supply-system-155-ef79e1d5", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.SourceSystem", 0),
        Target(156, "AirHandlingUnit.to_dragon", "function", "epsimple-hvac-supply-system-156-11a6909a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-11a6909a", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 0),
        Target(209, "ElectricRadiantFloor", "class", "epsimple-hvac-supply-system-209-f7f03ff5", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-f7f03ff5", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiantFloor and public properties", 1),
        Target(210, "ElectricRadiantFloor.ID", "function", "epsimple-hvac-supply-system-210-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Id", 1),
        Target(213, "ElectricRadiantFloor.__init__", "function", "epsimple-hvac-supply-system-213-f8bde28f", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-f8bde28f", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiantFloor and public properties", 1),
        Target(216, "ElectricRadiantFloor.from_json", "function", "epsimple-hvac-supply-system-216-b13a9536", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b13a9536", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 1),
        Target(217, "ElectricRadiantFloor.source", "function", "epsimple-hvac-supply-system-217-b14aeb3a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b14aeb3a", "GonieGonie.SimpleDragon.SourceSystem.SourceSystem", 1),
        Target(218, "ElectricRadiantFloor.to_dragon", "function", "epsimple-hvac-supply-system-218-01ae7da4", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-01ae7da4", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 1),
        Target(219, "ElectricRadiator", "class", "epsimple-hvac-supply-system-219-6354666e", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-6354666e", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiator and public properties", 2),
        Target(220, "ElectricRadiator.ID", "function", "epsimple-hvac-supply-system-220-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Id", 2),
        Target(223, "ElectricRadiator.__init__", "function", "epsimple-hvac-supply-system-223-3a47135f", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-3a47135f", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiator and public properties", 2),
        Target(226, "ElectricRadiator.capacity", "function", "epsimple-hvac-supply-system-226-09cfea01", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.HeatingCapacity", 2),
        Target(227, "ElectricRadiator.from_json", "function", "epsimple-hvac-supply-system-227-20bd3338", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-20bd3338", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 2),
        Target(228, "ElectricRadiator.source", "function", "epsimple-hvac-supply-system-228-b14aeb3a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b14aeb3a", "GonieGonie.SimpleDragon.SourceSystem.SourceSystem", 2),
        Target(229, "ElectricRadiator.to_dragon", "function", "epsimple-hvac-supply-system-229-4b95c9d6", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-4b95c9d6", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 2),
        Target(230, "FanCoilUnit", "class", "epsimple-hvac-supply-system-230-618e77c4", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-618e77c4", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.FanCoilUnit and public properties", 3),
        Target(231, "FanCoilUnit.ID", "function", "epsimple-hvac-supply-system-231-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Id", 3),
        Target(234, "FanCoilUnit.__init__", "function", "epsimple-hvac-supply-system-234-ea6e311c", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-ea6e311c", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.FanCoilUnit and public properties", 3),
        Target(237, "FanCoilUnit.from_json", "function", "epsimple-hvac-supply-system-237-4e773b8a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-4e773b8a", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 3),
        Target(238, "FanCoilUnit.source", "function", "epsimple-hvac-supply-system-238-ef79e1d5", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.SourceSystem", 3),
        Target(239, "FanCoilUnit.to_dragon", "function", "epsimple-hvac-supply-system-239-09f12474", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-09f12474", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 3),
        Target(271, "PackagedAirConditioner", "class", "epsimple-hvac-supply-system-271-fcef6339", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-fcef6339", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.PackagedAirConditioner and public properties", 4),
        Target(272, "PackagedAirConditioner.ID", "function", "epsimple-hvac-supply-system-272-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Id", 4),
        Target(275, "PackagedAirConditioner.__init__", "function", "epsimple-hvac-supply-system-275-b2021d84", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b2021d84", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.PackagedAirConditioner and public properties", 4),
        Target(278, "PackagedAirConditioner.capacity", "function", "epsimple-hvac-supply-system-278-09cfea01", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.CoolingCapacity", 4),
        Target(279, "PackagedAirConditioner.cop", "function", "epsimple-hvac-supply-system-279-873a49d3", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.CoolingCop", 4),
        Target(280, "PackagedAirConditioner.from_json", "function", "epsimple-hvac-supply-system-280-d49a3e1b", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-d49a3e1b", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 4),
        Target(281, "PackagedAirConditioner.source", "function", "epsimple-hvac-supply-system-281-b14aeb3a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b14aeb3a", "GonieGonie.SimpleDragon.SourceSystem.SourceSystem", 4),
        Target(282, "PackagedAirConditioner.to_dragon", "function", "epsimple-hvac-supply-system-282-0be4894a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-0be4894a", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 4),
        Target(296, "RadiantFloor", "class", "epsimple-hvac-supply-system-296-3a70e982", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-3a70e982", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.RadiantFloor and public properties", 5),
        Target(297, "RadiantFloor.ID", "function", "epsimple-hvac-supply-system-297-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Id", 5),
        Target(300, "RadiantFloor.__init__", "function", "epsimple-hvac-supply-system-300-ea6e311c", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-ea6e311c", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.RadiantFloor and public properties", 5),
        Target(303, "RadiantFloor.coolable", "function", "epsimple-hvac-supply-system-303-b81ea250", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Coolable", 5),
        Target(304, "RadiantFloor.from_json", "function", "epsimple-hvac-supply-system-304-a3c19218", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-a3c19218", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 5),
        Target(305, "RadiantFloor.heatable", "function", "epsimple-hvac-supply-system-305-0b60e64a", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Heatable", 5),
        Target(306, "RadiantFloor.source", "function", "epsimple-hvac-supply-system-306-ef79e1d5", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.SourceSystem", 5),
        Target(307, "RadiantFloor.to_dragon", "function", "epsimple-hvac-supply-system-307-db124859", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-db124859", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 5),
        Target(308, "Radiator", "class", "epsimple-hvac-supply-system-308-8464a277", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-8464a277", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.Radiator and public properties", 6),
        Target(309, "Radiator.ID", "function", "epsimple-hvac-supply-system-309-246156d9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Id", 6),
        Target(312, "Radiator.__init__", "function", "epsimple-hvac-supply-system-312-35304b6f", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-35304b6f", "GonieGonie.SimpleDragon.SupplySystem constructor with SupplySystemType.Radiator and public properties", 6),
        Target(315, "Radiator.capacity", "function", "epsimple-hvac-supply-system-315-d699d5f1", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.HeatingCapacity", 6),
        Target(316, "Radiator.from_json", "function", "epsimple-hvac-supply-system-316-349b941b", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-349b941b", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 6),
        Target(317, "Radiator.source", "function", "epsimple-hvac-supply-system-317-ef79e1d5", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.SourceSystem", 6),
        Target(318, "Radiator.to_dragon", "function", "epsimple-hvac-supply-system-318-bb8edb65", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-bb8edb65", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 6),
        Target(321, "SupplySystem", "class", "epsimple-hvac-supply-system-321-d236c0a0", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-d236c0a0", "GonieGonie.SimpleDragon.SupplySystem constructor and public properties", 7),
        Target(322, "SupplySystem.TYPE_MAPPER", "constant", "epsimple-hvac-supply-system-322-3639f058", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-3639f058", "GonieGonie.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) with SupplySystemType dispatch", 7),
        Target(323, "SupplySystem.coolable", "function", "epsimple-hvac-supply-system-323-a658d7c4", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Coolable", 7),
        Target(324, "SupplySystem.heatable", "function", "epsimple-hvac-supply-system-324-9d89b0d8", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.SourceSystem.Heatable", 7),
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(28, "sha256:d08a165490541d5d7157ea8f6344dd4b613d5b28ec556ab9032de892c280c282"),
        new(24, "sha256:d60b7f77292770b88ede38c280a62ac5011bc217a13fee35d9dc7fcc3ccc937e"),
        new(26, "sha256:fbeb2a8bef53ca88ff75ba171eaaae8a9de6adaf923c400f2d76d584b3a33499"),
        new(30, "sha256:62d3380f2474e5a8dabc6d3cd0f81b25b159b5ca69718224b91392cbb1792549"),
        new(29, "sha256:a251f327d0863a957f60b89edbefd92728cfda7761252bfb6e8b607210a0cf19"),
        new(27, "sha256:c97ef8ddb25ff3b7c220205a804b2a8487f1d95ee5f84af6b2c56a0183add6ca"),
        new(27, "sha256:8f0c74a390c4ddfdf099fd2155c534fad454c45e04d62329b12c24640d5ba06d"),
        new(19, "sha256:3cb604eb94857f3de0f848e35e8471c07b52538c8217e11e0ec91dac6cfb437d"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:9eccb6ed2a89794fe837d46d80ec2e638991548cea116357e10ef7b71bdd5ecb",
        "sha256:dbe56e7a15905e7974e055cd8772a13273bb124ccbf04f6c9090c3ef8ecef99e",
        "sha256:c41cfcd064b7d66f01904a6f5942374909e39382f2c5ffbf7e8efd52b6770bc1",
        "sha256:17f9adb0f515fbe247974225b5feed3b63f23a42413713225f35451ee7dbd091",
        "sha256:9c29ca9d3f78a2064c008756eda6cfa3534d454d7fbab8a19d13710ba267567c",
        "sha256:b1236af48e4ef06d0b4495c926af9442a3edf4798354b4b3d3787fc9a15c9c11",
        "sha256:15fb3a20f3b5ba9ae32ddd8a2d91de0930d4af542ee3a96d82bbfbb5e323202d",
        "sha256:06a7ace20ad6d7389ca35acf199ea082919dcc1740b7a6e518a0a336535ecf2d",
        "sha256:0b0aa80be58abe7377a4f2dd925ff2275e47ddaefd626244a4e2b309e6a4055d",
        "sha256:63d45766c52bed5d5751e4968a44c2c223b058764d522d5f91fde9969636199e",
        "sha256:1380ec8cdb0a9c8b3ac7386af1bceb45eb58ca8620180c1ec46ded0c8a5d513d",
        "sha256:57f2659bdeae215e21dbe885137624b9262c500a3a85272797c3f29daa0b5bbd",
        "sha256:d7ec653a21409518e915fac88e7c84a54eba00bccac86514943fe623d9308610",
        "sha256:b55ec235c3585427941cb5fd65ae4617a0fea3188c233e71561522531d440d30",
        "sha256:e50c6cd46a5ea5185dd5ffb8a46689f350b531af3e37758ed531e380d782b79b",
        "sha256:c69ab41be874b62e30dff6fc9009ef125a44faa8c86dafda9e3b1aa0f7238b62",
        "sha256:43a5cba3ae9366ed1bbae1de714baa1c0be2bdd5164f8d6c66ec5523ed638720",
        "sha256:d67ede00a5d0f42cd1f0a3f6b58b1e493c51e634b42ca6523f72098d4521210f",
        "sha256:952224797bd4fa0528ed16be4703aac6537a357c64cb700c775b1cf9ff12a9cb",
        "sha256:0642c15662cf93a29e8e4e1c646c525cbccbc556c00df5d82581290245cb4ae9",
        "sha256:52e7c9df193110f670cc3ed95a6117ee993da8be814245175302c1d6be062a9e",
        "sha256:bb22787bb889c36adb5267f70aa9c677eb2327bfc98a7bfff3816265510e3cfa",
        "sha256:c100c3988debcd3d631a706774f48f07a890b699b133cdd7c9fa69b3ebd53200",
        "sha256:bbba75a823b57dbd68013fb35ccb7f213eec5e3290be9bbdc373d31a7b794ece",
        "sha256:e2ef44127953951d190d9e72b0a01ba3643bb662494f5e6dcbb692576c8aba16",
        "sha256:b83b1a7b23c920a87a813382ca6a2e687b17b1f92dc8538c645941395f41a467",
        "sha256:35e0e9f56646323f6e2eb8f8dc56f6bd46336dc57e986d660f7d830bcd0521fc",
        "sha256:1980296d8055cd52565e8e52d5d5bf563198d460b5c97a73d63932ec6c776fc0",
        "sha256:b6e24777fb06314dcc46253eb3421408a16fe956af6886c00637549b9bad09f9",
        "sha256:2fa1ead16a18db04ef93997ab147aafb6bbe78f0383f4f24fe522c87e3f9bc38",
        "sha256:d75bed9a27552291a23beb66af4c95c47fc0944219c373f3a5cc7187189688ca",
        "sha256:c069dafec23ecc3f980686441a5efb647c4b708c58191b0353eb99db4cf9fffe",
        "sha256:3dabbdef75e99d190eed58b6edf58c576cd0a2b5fbb10e73aa127bd6f7a209e2",
        "sha256:f31e68027619b310e834f736dd620571a7ad9d3b3f318d2a360a8237fab60ba6",
        "sha256:e4a40f32bdaed2f219a376fbca7c9c076af40fb71f85c086ef80522df3b91c16",
        "sha256:7b38dbbfe149f3f648328f2c4e4979fef8fddb4d3d33b4be9aa69ff01f83348c",
        "sha256:46093d02f6b50e4656a95f97d136f88bf3b5c04491e8369cda1117964699abbe",
        "sha256:3c371346edf0fda324d9d8c8b0d7848d13fb02eb67d1304c2dcb36d1e79f1063",
        "sha256:18e3e213c434c10c283f10cc0fab0b1f3ba70897735bbe8a1cbdec3def63f2df",
        "sha256:df7133aa837865ecf57cf3d423bcc3b7da3fc439fab0ff226a99d3d0a4dbed46",
        "sha256:a480b871f3529cc55383c049634679b83d7b89d35209de8ea26cf61ce00344c4",
        "sha256:ddbed5bede15b012aadcdba7415c73045d7106f57298f7e75a5080bfbfabc54c",
        "sha256:550b35955bc078a863807b88d129367eeb710a22b31858bebc0a16390b9fe532",
        "sha256:00079706835b42e35da03f6be5cd61e818a0ac7f88537133a462288cc04cef47",
        "sha256:b55e0f91b31c268e8405711ce9cc56da5ea0916b165a1c934b57a1073f635c68",
        "sha256:9a5d6a90fe322001c5bab3dcb14f9936ec49ca9ed7f73e35f6dffc4780271235",
        "sha256:b16001903137160f96a2504df415f886051540ceeaebafc9acd5595be776750c",
        "sha256:cc12ea13171cccbf9ef4dbd572c62ba6a1ef24c5a8672c9392af51754964ffb7",
        "sha256:5122ae92433fb2ddecef7a3cdaa8cf47c3d869b75f2698ec42fdad1d67870619",
        "sha256:afb425df3640f5f47310423a848642740ce96a940f69d0821338410964db9bd9",
        "sha256:abdd6cbff42f6540eadd450d49bd3f0e9fc029e0747eec111e3b23f06141f8fa",
        "sha256:7c7b2c092fc6116c7b64eb087a8431057a5b9512496f7abfa7490b9474d6fca1",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:0335dace4b26d2ac8b0415c0dfa3631454a8598ba21b7c2517f72ddc90bab12f", // epsimple-hvac-supply-system-147-6fd0030b
        "sha256:deba663b2203b55b9ba369fa89f0b49637c91ac0ba9374c87199c7cbd58a9d0c", // epsimple-hvac-supply-system-148-246156d9
        "sha256:acd496316ff873d18692282756e51d08a6348530012fde28fbfc581bf4b8c129", // epsimple-hvac-supply-system-151-ea6e311c
        "sha256:e4fdb6d182021c1daea7c16b151469b1a60f7a48a771886dccc78a79d523253f", // epsimple-hvac-supply-system-154-148b0ee3
        "sha256:7ef39f1f6a971dcb26cba42a485f1ef7f5fb2f5d33974b3a228572aab5e73005", // epsimple-hvac-supply-system-155-ef79e1d5
        "sha256:83cbd21a0df12f4e994e96e9a0324ae369a56783c0ec8879ea3d784b59e85b50", // epsimple-hvac-supply-system-156-11a6909a
        "sha256:13d3b880df4d2b0ee88a22aceb2093531e095c39c7796e5409d1e5f95bb29ec0", // epsimple-hvac-supply-system-209-f7f03ff5
        "sha256:eb8ebd3b67974e9544a2cd5007818f87f4e06eadcb52b63caf62f35b21e24cb0", // epsimple-hvac-supply-system-210-246156d9
        "sha256:9b7504274a5ca982039ea5e0ffc865d8131f53bf2d586d5ba12f1c6791999def", // epsimple-hvac-supply-system-213-f8bde28f
        "sha256:9e2ae7435035e981f54a11d719e600f360205cbce472167bb9f926aa1dc3faab", // epsimple-hvac-supply-system-216-b13a9536
        "sha256:f998ff5e6c28cc34e0348b013f902154f8bc07c2c311fd7419f636321a50e0e1", // epsimple-hvac-supply-system-217-b14aeb3a
        "sha256:d25b4b88d37c433174bb2879e87d240161f727f6efe5f85cdcab859d4ac82246", // epsimple-hvac-supply-system-218-01ae7da4
        "sha256:91f7baece75fb38eea1850d680b1705e941c1775ba75654fd2b9abad2bf60e51", // epsimple-hvac-supply-system-219-6354666e
        "sha256:04004923b21c4d9a35e59ab80410ca208b3529d1b1a23410468e351db60750d1", // epsimple-hvac-supply-system-220-246156d9
        "sha256:f237b8014437590ecc40cbab0d6a139c90137667b6a54185076f3b00506bd449", // epsimple-hvac-supply-system-223-3a47135f
        "sha256:52be54f593b47ad834d985da0b26628d6c6ddbb4873bd8e29c47e0c115dba2d9", // epsimple-hvac-supply-system-226-09cfea01
        "sha256:025bcf9fc0167fa1973b1a0a60ae5130daaaa36a4439f02aafb462523bdb42d2", // epsimple-hvac-supply-system-227-20bd3338
        "sha256:069ef7a1d0b4fe9d87a26be3aafd9e7a0655c4a39c05636ac4b98e2d7bb0c8c2", // epsimple-hvac-supply-system-228-b14aeb3a
        "sha256:358faa833d45e62aafdb476c123a6cf2607f5c245675d014cdb58f3c8eabf5fd", // epsimple-hvac-supply-system-229-4b95c9d6
        "sha256:3e2191b7c8f5f6df493f8868691ce583494696f05bcdbe1b25f54761ba309ff4", // epsimple-hvac-supply-system-230-618e77c4
        "sha256:348fcacbebe145283a7c237771215ce778a659b1e9bdd025d41291b4501f8bd7", // epsimple-hvac-supply-system-231-246156d9
        "sha256:093d6909678492f5d971809dafc84245697a3a5c98c57ae933a3f36f14d8e70c", // epsimple-hvac-supply-system-234-ea6e311c
        "sha256:ab8179809ac05188c6fba2bd98d2471d5a53084bd05412e555b81b44917a08b2", // epsimple-hvac-supply-system-237-4e773b8a
        "sha256:ff22903465c8323104e91e480428eec2c62876416bfadba769d1ad15de1b6b67", // epsimple-hvac-supply-system-238-ef79e1d5
        "sha256:3a001f18f8564b3d980f84abc8f87d373360e57127d3402fac1db36763f19248", // epsimple-hvac-supply-system-239-09f12474
        "sha256:4d26b4cb4bf22e22d233b419cf725b0a21575b155d9e4a94aac4ae62c3e1e7e7", // epsimple-hvac-supply-system-271-fcef6339
        "sha256:e2bb86867952fc13c2e45af3226dc5bbb3ef1ae88eccf9c347c2772cbf04ee29", // epsimple-hvac-supply-system-272-246156d9
        "sha256:d782a325ec9b28edb9c2cee0e553be86b0c4da37f8cfafed7b19e723ff01dee4", // epsimple-hvac-supply-system-275-b2021d84
        "sha256:947596c386e496ba11d0f562db75ae829e6ecac3df44e8a3ec095374d83f0d19", // epsimple-hvac-supply-system-278-09cfea01
        "sha256:4519ab0a9947098dc02465cb5329024bac470d1fce83f7a86b0724f637749ae7", // epsimple-hvac-supply-system-279-873a49d3
        "sha256:68180e2844c04a0ca5d01d4b6cf8b2d2fd50095ee5b7b345fb93af5831fd948c", // epsimple-hvac-supply-system-280-d49a3e1b
        "sha256:034564395894a086d3ece08d29fafdce96bdb72b4d263a5466c553ab4cdf72b9", // epsimple-hvac-supply-system-281-b14aeb3a
        "sha256:56c56c39dac3e903ddd336d30bc55650a185026aafda1c07ea9a2300c161760e", // epsimple-hvac-supply-system-282-0be4894a
        "sha256:83bb0323b0aa4b3f35849b391807e97ded5ed4dbfdef97cc5834240934a7e9e8", // epsimple-hvac-supply-system-296-3a70e982
        "sha256:88b39fce1db60edb6a5f963f471346564711e03b8d976de2fc363d516c606074", // epsimple-hvac-supply-system-297-246156d9
        "sha256:32347890aa86124000b07518cc014d213bf0fb6ca109fc04137690b080a2ad19", // epsimple-hvac-supply-system-300-ea6e311c
        "sha256:4e23e97e9fa5e378bde87a32f4f22268c8eaa2d94f6a16119cb1c54a0ffd33c5", // epsimple-hvac-supply-system-303-b81ea250
        "sha256:9a772fb6a357e66499f3b1a20bf07f69b1fec08bbbc93945ec5241f6298d6fbb", // epsimple-hvac-supply-system-304-a3c19218
        "sha256:2a19fb58ffaa0ea2f855b838adb2113a07d2f79c5da9cf596f74c3eaba98f558", // epsimple-hvac-supply-system-305-0b60e64a
        "sha256:f43d524da8f303ee37b4633c702a4c9d839cb6486a6e5601abbdc5fd41c7fca0", // epsimple-hvac-supply-system-306-ef79e1d5
        "sha256:eb86d7912488c425fa132a6d0a4951b4c69753f7df9408b639d4567dd91dbe9a", // epsimple-hvac-supply-system-307-db124859
        "sha256:31429562150ec17a2b9f89bb302731786192a89045d6e72bdf920e6946905673", // epsimple-hvac-supply-system-308-8464a277
        "sha256:f567413113b58ea0c0ba21dc14369c3630449b964c78c9d798dd784ba2a5de64", // epsimple-hvac-supply-system-309-246156d9
        "sha256:32a3675b6a9e6716e51404a9ed7b3f0c14430881291ef0c4cdca14eac2106e91", // epsimple-hvac-supply-system-312-35304b6f
        "sha256:452d84f70a7d16d418bbaa2178fbcc82e4d9c213285d38d38aeff43e89e7b870", // epsimple-hvac-supply-system-315-d699d5f1
        "sha256:71a1b53e0346dbe1928cf1a5fc9a5b758547eff93f4db54276d576464619b1ec", // epsimple-hvac-supply-system-316-349b941b
        "sha256:98e482d345ce139783c48ca952f79ae152a371133e6aaae3e272c3614d9b74ff", // epsimple-hvac-supply-system-317-ef79e1d5
        "sha256:cfc972797eee80fdcf8bc0af65e66bb64077826741b7b61d00b8841cb4b2a4c3", // epsimple-hvac-supply-system-318-bb8edb65
        "sha256:3b4605238452690ef69c52f1a5ca8cf4d7bb8c3383602a51f16add462294b9b0", // epsimple-hvac-supply-system-321-d236c0a0
        "sha256:5b4226bf2e69aeb89d17850c0d94d15f9b2226eeb97ea92130968eac58af49b9", // epsimple-hvac-supply-system-322-3639f058
        "sha256:d42278e7bef972c55779cc0ed97714ec4660a3d293219fab2a94cf0d7d56a72a", // epsimple-hvac-supply-system-323-a658d7c4
        "sha256:a5f070c91b0f5278da56d571c1e0cc83b187d8ced470152c6c0c3a4b0e580a50", // epsimple-hvac-supply-system-324-9d89b0d8
    };

    [Fact]
    public void MatchesPinnedHvacSupplySystemsThroughProductionPublicRoutes()
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
                "HVAC_SUPPLY_SYSTEM_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(52, recordCount);
        Assert.Equal(52, corpus.Targets.Length);
        Assert.Equal(52, corpus.Targets.Select(item => item.AssertionId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(19, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(33, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.Equal(8, corpus.FixtureCases.Length);
        Assert.Equal(150, corpus.AdjacentIndices.Length);
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

        Assert.True(typeof(SupplySystem).IsSealed);
        Assert.Single(typeof(SupplySystem).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.Id), typeof(EntityId));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.Name), typeof(string));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.Type), typeof(SupplySystemType));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.SourceSystemId), typeof(string));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.SourceSystem), typeof(SourceSystem));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.CoolingCop), typeof(double?));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.CoolingCapacity), typeof(double?));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.HeatingCapacity), typeof(double?));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.Heatable), typeof(bool));
        AssertReadOnlyProperty<SupplySystem>(nameof(SupplySystem.Coolable), typeof(bool));
        Assert.Equal(
            new[]
            {
                SupplySystemType.PackagedAirConditioner,
                SupplySystemType.AirHandlingUnit,
                SupplySystemType.FanCoilUnit,
                SupplySystemType.Radiator,
                SupplySystemType.ElectricRadiator,
                SupplySystemType.RadiantFloor,
                SupplySystemType.ElectricRadiantFloor,
            },
            Enum.GetValues<SupplySystemType>());

        Assert.True(typeof(SourceSystem).IsSealed);
        Assert.Single(typeof(SourceSystem).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.Id), typeof(EntityId));
        AssertReadOnlyProperty<SourceSystem>(nameof(SourceSystem.Type), typeof(SourceSystemType));
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
            "supply_system_support");
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
        AssertArtifact(runtime.GetProperty("supply_system_support"), SupportPath, SupportBytes, SupportSha256);
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
        Assert.Equal(150, adjacentIndices.Length);
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
        Assert.Equal(8, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(19, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(33, counts.GetProperty("exception").GetInt32());

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

            Assert.Contains("GonieGonie.SimpleDragon", target.NativeRoute, StringComparison.Ordinal);
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
        Assert.Equal(150, closure.GetProperty("adjacent_count").GetInt32());
        Assert.Equal(adjacentIndices, ReadIntArray(closure.GetProperty("adjacent_indices")));
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_hvac_source_partition").GetBoolean());
        Assert.Equal(202, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(52, closure.GetProperty("target_count").GetInt32());
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
        Assert.Equal(52, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
    }

    private static NativeObservation ObserveNativeCase(int index)
    {
        string[] facts = index switch
        {
            0 => ObserveAirHandlingUnit(),
            1 => ObserveElectricRadiantFloor(),
            2 => ObserveElectricRadiator(),
            3 => ObserveFanCoilUnit(),
            4 => ObservePackagedAirConditioner(),
            5 => ObserveRadiantFloor(),
            6 => ObserveRadiator(),
            7 => ObserveSupplySystemBase(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return new NativeObservation(
            Cases[index].Code,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveAirHandlingUnit()
    {
        SourceSystem heatPump = HeatPump("SRC-AHU-HP");
        SupplyConversionProbe probe = RoundTripAndConvert(
            Supply("SUP-AHU-HP", SupplySystemType.AirHandlingUnit, heatPump),
            heatPump);
        Assert.IsType<DragonAirHandlingUnit>(probe.ConvertedSupply);

        SourceSystem geothermal = HeatPump("SRC-AHU-GEO", geothermal: true);
        SupplyConversionProbe geothermalProbe = RoundTripAndConvert(
            Supply("SUP-AHU-GEO", SupplySystemType.AirHandlingUnit, geothermal),
            geothermal);
        Assert.IsType<DragonAirHandlingUnit>(geothermalProbe.ConvertedSupply);

        SourceSystem boiler = Boiler("SRC-AHU-BOILER");
        string unresolved = ReplaceRequired(
            probe.Json,
            "\"source_system_id\":\"SRC-AHU-HP\"",
            "\"source_system_id\":\"SRC-AHU-MISSING\"");
        return CommonFacts(probe).Concat(new[]
        {
            "branch.geothermal.source=" + geothermalProbe.ConvertedSupply.Source!.GetType().Name,
            "branch.geothermal.heatable=" + Boolean(geothermalProbe.RereadSupply.Heatable),
            "branch.geothermal.coolable=" + Boolean(geothermalProbe.RereadSupply.Coolable),
            "invalid.missing_source=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.AirHandlingUnit)),
            "invalid.boiler_source=" + ExceptionFact(() => _ = Supply(
                "SUP-AHU-BAD", SupplySystemType.AirHandlingUnit, boiler)),
            "invalid.source_id_mismatch=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.AirHandlingUnit, "SRC-OTHER", heatPump)),
            "invalid.reader_unresolved.codes=" + DiagnosticCodes(GrmReader.Read(
                unresolved, SimpleDragonDatabase.Default)),
            "native.route=SupplySystem+SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        }).ToArray();
    }

    private static string[] ObserveElectricRadiantFloor()
    {
        SupplyConversionProbe probe = RoundTripAndConvert(Supply(
            "SUP-ELECTRIC-FLOOR",
            SupplySystemType.ElectricRadiantFloor));
        Assert.IsType<DragonElectricRadiantFloor>(probe.ConvertedSupply);
        SourceSystem heatPump = HeatPump("SRC-ELECTRIC-FLOOR-BAD");
        return CommonFacts(probe).Concat(new[]
        {
            "invalid.source=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid",
                SupplySystemType.ElectricRadiantFloor,
                heatPump.Id.Value,
                heatPump)),
            "invalid.name=" + ExceptionFact(() => _ = new SupplySystem(
                " ", SupplySystemType.ElectricRadiantFloor)),
            "invalid.enum=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", (SupplySystemType)int.MaxValue)),
            "native.route=SupplySystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        }).ToArray();
    }

    private static string[] ObserveElectricRadiator()
    {
        SupplySystem minimal = ReadSupplyRoundTrip(Supply(
            "SUP-ELECTRIC-RADIATOR-DEFAULT",
            SupplySystemType.ElectricRadiator));
        SupplyConversionProbe probe = RoundTripAndConvert(Supply(
            "SUP-ELECTRIC-RADIATOR",
            SupplySystemType.ElectricRadiator,
            heatingCapacity: 18_500d));
        DragonElectricRadiator converted =
            Assert.IsType<DragonElectricRadiator>(probe.ConvertedSupply);
        return CommonFacts(probe).Concat(new[]
        {
            "default.capacity=" + Double(minimal.HeatingCapacity),
            "conversion.capacity=" + Double(converted.HeatingCapacityWatts),
            "invalid.capacity_zero=" + ExceptionFact(() => _ = Supply(
                "SUP-ELECTRIC-RADIATOR-ZERO",
                SupplySystemType.ElectricRadiator,
                heatingCapacity: 0d)),
            "invalid.capacity_nan=" + ExceptionFact(() => _ = Supply(
                "SUP-ELECTRIC-RADIATOR-NAN",
                SupplySystemType.ElectricRadiator,
                heatingCapacity: double.NaN)),
            "invalid.source_id=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.ElectricRadiator, "SRC-FORBIDDEN")),
            "native.route=SupplySystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        }).ToArray();
    }

    private static string[] ObserveFanCoilUnit()
    {
        SourceSystem boiler = Boiler("SRC-FCU-BOILER");
        SupplyConversionProbe heating = RoundTripAndConvert(
            Supply("SUP-FCU-BOILER", SupplySystemType.FanCoilUnit, boiler),
            boiler);
        Assert.IsType<DragonFanCoilUnit>(heating.ConvertedSupply);

        SourceSystem chiller = Chiller("SRC-FCU-CHILLER");
        SupplyConversionProbe cooling = RoundTripAndConvert(
            Supply("SUP-FCU-CHILLER", SupplySystemType.FanCoilUnit, chiller),
            chiller);
        Assert.IsType<DragonFanCoilUnit>(cooling.ConvertedSupply);

        SupplySystem district = Supply(
            "SUP-FCU-DISTRICT",
            SupplySystemType.FanCoilUnit,
            District("SRC-FCU-DISTRICT"));
        SupplySystem absorption = Supply(
            "SUP-FCU-ABSORPTION",
            SupplySystemType.FanCoilUnit,
            Absorption("SRC-FCU-ABSORPTION"));
        SourceSystem heatPump = HeatPump("SRC-FCU-BAD");
        string unresolved = ReplaceRequired(
            heating.Json,
            "\"source_system_id\":\"SRC-FCU-BOILER\"",
            "\"source_system_id\":\"SRC-FCU-MISSING\"");
        return CommonFacts(heating).Concat(new[]
        {
            "branch.cooling.source=" + cooling.RereadSupply.SourceSystem!.Type,
            "branch.cooling.heatable=" + Boolean(cooling.RereadSupply.Heatable),
            "branch.cooling.coolable=" + Boolean(cooling.RereadSupply.Coolable),
            "branch.cooling.dragon_source=" + cooling.ConvertedSupply.Source!.GetType().Name,
            "branch.district.capability=" + Boolean(district.Heatable) + "/" + Boolean(district.Coolable),
            "branch.absorption.capability=" + Boolean(absorption.Heatable) + "/" + Boolean(absorption.Coolable),
            "invalid.heat_pump=" + ExceptionFact(() => _ = Supply(
                "SUP-FCU-BAD", SupplySystemType.FanCoilUnit, heatPump)),
            "invalid.missing_source=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.FanCoilUnit)),
            "invalid.reader_unresolved.codes=" + DiagnosticCodes(GrmReader.Read(
                unresolved, SimpleDragonDatabase.Default)),
            "native.route=SupplySystem+SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        }).ToArray();
    }

    private static string[] ObservePackagedAirConditioner()
    {
        SupplySystem minimal = ReadSupplyRoundTrip(Supply(
            "SUP-PAC-DEFAULT",
            SupplySystemType.PackagedAirConditioner));
        SupplyConversionProbe probe = RoundTripAndConvert(Supply(
            "SUP-PAC-EXPLICIT",
            SupplySystemType.PackagedAirConditioner,
            coolingCop: 3.6d,
            coolingCapacity: 19_000d));
        Assert.IsType<DragonPackagedAirConditioner>(probe.ConvertedSupply);
        return CommonFacts(probe).Concat(new[]
        {
            "default.cop=" + Double(minimal.CoolingCop),
            "default.capacity=" + Double(minimal.CoolingCapacity),
            "conversion.dedicated_source=" + probe.ConvertedSupply.Source!.GetType().Name,
            "conversion.diagnostics=" + Join(probe.DiagnosticCodes),
            "invalid.source_id=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.PackagedAirConditioner, "SRC-FORBIDDEN")),
            "invalid.cop_zero=" + ExceptionFact(() => _ = Supply(
                "SUP-PAC-ZERO-COP",
                SupplySystemType.PackagedAirConditioner,
                coolingCop: 0d)),
            "invalid.capacity_zero=" + ExceptionFact(() => _ = Supply(
                "SUP-PAC-ZERO-CAPACITY",
                SupplySystemType.PackagedAirConditioner,
                coolingCapacity: 0d)),
            "invalid.cop_infinite=" + ExceptionFact(() => _ = Supply(
                "SUP-PAC-INFINITE",
                SupplySystemType.PackagedAirConditioner,
                coolingCop: double.PositiveInfinity)),
            "native.route=SupplySystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        }).ToArray();
    }

    private static string[] ObserveRadiantFloor()
    {
        SourceSystem boiler = Boiler("SRC-RADIANT-BOILER");
        SupplyConversionProbe heating = RoundTripAndConvert(
            Supply("SUP-RADIANT-BOILER", SupplySystemType.RadiantFloor, boiler),
            boiler);
        Assert.IsType<DragonRadiantFloor>(heating.ConvertedSupply);

        SourceSystem district = District("SRC-RADIANT-DISTRICT");
        SupplyConversionProbe districtProbe = RoundTripAndConvert(
            Supply("SUP-RADIANT-DISTRICT", SupplySystemType.RadiantFloor, district),
            district);
        Assert.IsType<DragonRadiantFloor>(districtProbe.ConvertedSupply);
        SourceSystem chiller = Chiller("SRC-RADIANT-BAD");
        return CommonFacts(heating).Concat(new[]
        {
            "branch.district.source=" + districtProbe.RereadSupply.SourceSystem!.Type,
            "branch.district.heatable=" + Boolean(districtProbe.RereadSupply.Heatable),
            "branch.district.coolable=" + Boolean(districtProbe.RereadSupply.Coolable),
            "branch.district.dragon_source=" + districtProbe.ConvertedSupply.Source!.GetType().Name,
            "invalid.chiller=" + ExceptionFact(() => _ = Supply(
                "SUP-RADIANT-BAD", SupplySystemType.RadiantFloor, chiller)),
            "invalid.missing_source=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.RadiantFloor)),
            "native.route=SupplySystem+SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        }).ToArray();
    }

    private static string[] ObserveRadiator()
    {
        SourceSystem boiler = Boiler("SRC-RADIATOR-BOILER");
        SupplySystem minimal = ReadSupplyRoundTrip(
            Supply("SUP-RADIATOR-DEFAULT", SupplySystemType.Radiator, boiler),
            boiler);
        SupplyConversionProbe probe = RoundTripAndConvert(
            Supply(
                "SUP-RADIATOR-EXPLICIT",
                SupplySystemType.Radiator,
                boiler,
                heatingCapacity: 21_000d),
            boiler);
        DragonRadiator converted = Assert.IsType<DragonRadiator>(probe.ConvertedSupply);
        SupplySystem district = Supply(
            "SUP-RADIATOR-DISTRICT",
            SupplySystemType.Radiator,
            District("SRC-RADIATOR-DISTRICT"));
        SourceSystem chiller = Chiller("SRC-RADIATOR-BAD");
        return CommonFacts(probe).Concat(new[]
        {
            "default.capacity=" + Double(minimal.HeatingCapacity),
            "conversion.capacity=" + Double(converted.HeatingCapacityWatts),
            "branch.district.capability=" + Boolean(district.Heatable) + "/" + Boolean(district.Coolable),
            "invalid.capacity_zero=" + ExceptionFact(() => _ = Supply(
                "SUP-RADIATOR-ZERO",
                SupplySystemType.Radiator,
                boiler,
                heatingCapacity: 0d)),
            "invalid.chiller=" + ExceptionFact(() => _ = Supply(
                "SUP-RADIATOR-BAD", SupplySystemType.Radiator, chiller)),
            "invalid.missing_source=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.Radiator)),
            "native.route=SupplySystem+SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        }).ToArray();
    }

    private static string[] ObserveSupplySystemBase()
    {
        SourceSystem heatPump = HeatPump("SRC-BASE-HP");
        SourceSystem chiller = Chiller("SRC-BASE-CHILLER");
        SourceSystem boiler = Boiler("SRC-BASE-BOILER");
        SourceSystem district = District("SRC-BASE-DISTRICT");
        SupplySystem[] systems =
        {
            Supply("SUP-BASE-PAC", SupplySystemType.PackagedAirConditioner),
            Supply("SUP-BASE-AHU", SupplySystemType.AirHandlingUnit, heatPump),
            Supply("SUP-BASE-FCU", SupplySystemType.FanCoilUnit, chiller),
            Supply("SUP-BASE-RADIATOR", SupplySystemType.Radiator, boiler),
            Supply("SUP-BASE-ELECTRIC-RADIATOR", SupplySystemType.ElectricRadiator),
            Supply("SUP-BASE-RADIANT", SupplySystemType.RadiantFloor, district),
            Supply("SUP-BASE-ELECTRIC-RADIANT", SupplySystemType.ElectricRadiantFloor),
        };
        GreenRetrofitModel model = CreateModel(
            new[] { heatPump, chiller, boiler, district },
            systems,
            systems[0]);
        string json = GrmWriter.Serialize(model, indented: false);
        GrmReadResult read = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(read.Success, Describe(read.Diagnostics));
        GreenRetrofitModel reread = read.RequireModel();
        GreenRetrofitConversionResult conversion = Convert(reread);
        Assert.IsType<DragonPackagedAirConditioner>(OnlySupply(conversion));
        string[] groups = SupplyGroups(json);
        string invalidGroup = ReplaceRequired(
            json,
            "\"" + groups[0] + "\":",
            "\"unknown_supply_system\":");
        var unresolved = new SupplySystem(
            "Unresolved",
            SupplySystemType.AirHandlingUnit,
            "SRC-UNRESOLVED");
        return new[]
        {
            "native.aggregate=" + typeof(SupplySystem).FullName,
            "native.base=" + typeof(DragonSupplySystem).FullName,
            "sealed=" + Boolean(typeof(SupplySystem).IsSealed),
            "constructor.count=" + typeof(SupplySystem).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance).Length,
            "enum.order=" + Join(Enum.GetNames<SupplySystemType>()),
            "property.order=" + Join(typeof(SupplySystem)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(item => item.Name)),
            "capabilities=" + Join(systems.Select(item =>
                item.Type + ":" + Boolean(item.Heatable) + "/" + Boolean(item.Coolable)
                + ":" + (item.SourceSystem?.Type.ToString() ?? "null"))),
            "writer.groups=" + Join(groups),
            "reader.supply_count=" + reread.SupplySystems.Count,
            "reader.source_count=" + reread.SourceSystems.Count,
            "reader.repeat_equal=" + Boolean(
                json == GrmWriter.Serialize(reread, indented: false)),
            "conversion.success=" + Boolean(conversion.Success),
            "unresolved.heatable=" + Boolean(unresolved.Heatable),
            "unresolved.coolable=" + Boolean(unresolved.Coolable),
            "invalid.enum=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", (SupplySystemType)int.MaxValue)),
            "invalid.name=" + ExceptionFact(() => _ = new SupplySystem(
                "", SupplySystemType.PackagedAirConditioner)),
            "invalid.source_mismatch=" + ExceptionFact(() => _ = new SupplySystem(
                "invalid", SupplySystemType.AirHandlingUnit, "SRC-OTHER", heatPump)),
            "invalid.unknown_group.codes=" + DiagnosticCodes(GrmReader.Read(
                invalidGroup, SimpleDragonDatabase.Default)),
            "native.route=SupplySystem+SourceSystem+GrmReader+GrmWriter+GreenRetrofitConverter",
        };
    }

    private static string[] CommonFacts(SupplyConversionProbe probe)
    {
        SupplySystem supply = probe.RereadSupply;
        DragonSupplySystem converted = probe.ConvertedSupply;
        return new[]
        {
            "native.aggregate=" + typeof(SupplySystem).FullName,
            "constructor.type=" + supply.Type,
            "constructor.id=" + supply.Id.Value,
            "constructor.name=" + supply.Name,
            "constructor.source_id=" + (supply.SourceSystemId ?? "null"),
            "constructor.source=" + (supply.SourceSystem?.Type.ToString() ?? "null"),
            "constructor.cooling_cop=" + Double(supply.CoolingCop),
            "constructor.cooling_capacity=" + Double(supply.CoolingCapacity),
            "constructor.heating_capacity=" + Double(supply.HeatingCapacity),
            "constructor.heatable=" + Boolean(supply.Heatable),
            "constructor.coolable=" + Boolean(supply.Coolable),
            "writer.group=" + SupplyGroup(probe.Json),
            "writer.repeat_equal=" + Boolean(probe.WriterRepeatEqual),
            "reader.source_resolved=" + Boolean(supply.SourceSystem is not null),
            "conversion.success=" + Boolean(probe.Success),
            "conversion.supply_type=" + converted.GetType().Name,
            "conversion.source_type=" + (converted.Source?.GetType().Name ?? "null"),
            "conversion.can_heat=" + Boolean(converted.CanHeat),
            "conversion.can_cool=" + Boolean(converted.CanCool),
            "conversion.fresh=" + Boolean(probe.FreshConversion),
        };
    }

    private static SourceSystem HeatPump(string id, bool geothermal = false) => new(
        geothermal ? "Geothermal " + id : "Heat pump " + id,
        geothermal ? SourceSystemType.GeothermalHeatPump : SourceSystemType.HeatPump,
        FuelType.Electricity,
        heatingCop: geothermal ? 4.5d : 3.5d,
        coolingCop: geothermal ? 5d : 4d,
        heatingCapacity: 18_000d,
        coolingCapacity: 16_000d,
        id: Id(id));

    private static SourceSystem Boiler(string id) => new(
        "Boiler " + id,
        SourceSystemType.Boiler,
        FuelType.NaturalGas,
        heatingCapacity: 24_000d,
        efficiency: 0.9d,
        hotWaterSupply: true,
        id: Id(id));

    private static SourceSystem District(string id) => new(
        "District " + id,
        SourceSystemType.DistrictHeating,
        heatingCapacity: 28_000d,
        hotWaterSupply: true,
        id: Id(id));

    private static SourceSystem Chiller(string id) => new(
        "Chiller " + id,
        SourceSystemType.Chiller,
        coolingCop: 4.25d,
        coolingCapacity: 30_000d,
        compressorType: CompressorType.Screw,
        coolingTowerType: CoolingTowerType.Closed,
        coolingTowerCapacity: 36_000d,
        coolingTowerControl: CoolingTowerControl.TwoSpeed,
        id: Id(id));

    private static SourceSystem Absorption(string id) => new(
        "Absorption " + id,
        SourceSystemType.AbsorptionChiller,
        FuelType.NaturalGas,
        coolingCop: 1.2d,
        coolingCapacity: 22_000d,
        boilerEfficiency: 0.82d,
        id: Id(id));

    private static SupplySystem Supply(
        string id,
        SupplySystemType type,
        SourceSystem? source = null,
        double? coolingCop = null,
        double? coolingCapacity = null,
        double? heatingCapacity = null) => new(
            type + " " + id,
            type,
            source?.Id.Value,
            source,
            coolingCop,
            coolingCapacity,
            heatingCapacity,
            Id(id));

    private static SupplySystem ReadSupplyRoundTrip(
        SupplySystem supply,
        params SourceSystem[] sources)
    {
        GreenRetrofitModel model = CreateModel(sources, new[] { supply }, supply);
        string json = GrmWriter.Serialize(model, indented: false);
        GrmReadResult read = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(read.Success, Describe(read.Diagnostics));
        GreenRetrofitModel reread = read.RequireModel();
        Assert.Equal(json, GrmWriter.Serialize(reread, indented: false));
        return Assert.Single(reread.SupplySystems);
    }

    private static SupplyConversionProbe RoundTripAndConvert(
        SupplySystem supply,
        params SourceSystem[] sources)
    {
        GreenRetrofitModel model = CreateModel(sources, new[] { supply }, supply);
        string json = GrmWriter.Serialize(model, indented: false);
        GrmReadResult read = GrmReader.Read(json, SimpleDragonDatabase.Default);
        Assert.True(read.Success, Describe(read.Diagnostics));
        GreenRetrofitModel reread = read.RequireModel();
        SupplySystem rereadSupply = Assert.Single(reread.SupplySystems);
        bool writerRepeatEqual = json == GrmWriter.Serialize(reread, indented: false);
        GreenRetrofitConversionResult first = Convert(reread);
        GreenRetrofitConversionResult second = Convert(reread);
        DragonSupplySystem converted = OnlySupply(first);
        DragonSupplySystem convertedAgain = OnlySupply(second);
        return new SupplyConversionProbe(
            json,
            rereadSupply,
            converted,
            first.Success,
            writerRepeatEqual,
            !ReferenceEquals(converted, convertedAgain),
            first.Diagnostics.Select(item => item.Code)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray());
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

    private static string SupplyGroup(string json) => Assert.Single(SupplyGroups(json));

    private static string[] SupplyGroups(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("building")
            .GetProperty("supply_systems")
            .EnumerateObject()
            .Select(item => item.Name)
            .ToArray();
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
                exact_target_count = 52,
                equivalent_target_count = 19,
                exception_target_count = 33,
                exact_case_count = 8,
                adjacent_count_not_recorded = 150,
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
        Assert.Equal(52, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(19, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(33, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(8, scope.GetProperty("exact_case_count").GetInt32());
        Assert.Equal(150, scope.GetProperty("adjacent_count_not_recorded").GetInt32());
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

    private sealed record SupplyConversionProbe(
        string Json,
        SupplySystem RereadSupply,
        DragonSupplySystem ConvertedSupply,
        bool Success,
        bool WriterRepeatEqual,
        bool FreshConversion,
        string[] DiagnosticCodes);
}
