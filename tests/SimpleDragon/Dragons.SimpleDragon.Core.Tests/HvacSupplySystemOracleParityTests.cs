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
using DragonElectricRadiantFloor = Dragons.InvisibleDragon.Hvac.ElectricRadiantFloor;
using DragonElectricRadiator = Dragons.InvisibleDragon.Hvac.ElectricRadiator;
using DragonFanCoilUnit = Dragons.InvisibleDragon.Hvac.FanCoilUnit;
using DragonPackagedAirConditioner = Dragons.InvisibleDragon.Hvac.PackagedAirConditioner;
using DragonRadiantFloor = Dragons.InvisibleDragon.Hvac.RadiantFloor;
using DragonRadiator = Dragons.InvisibleDragon.Hvac.Radiator;
using DragonSupplySystem = Dragons.InvisibleDragon.Hvac.SupplySystem;

namespace Dragons.SimpleDragon.Tests;

public sealed class HvacSupplySystemOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-hvac-supply-system-oracle.json";
    private const int FixtureBytes = 167_819;
    private const string FixtureSha256 =
        "sha256:61ae6f650e0cd05db76b18b68477fff72e1357ae1842892170fefa01cb4285c2";
    private const string FixtureSchema =
        "dragons.python-reference.epsimple-hvac-supply-system.v1";
    private const string FixtureRepositoryCommit = "6d7ff18";
    private const string CasesSha256 =
        "sha256:844e26e1e019dc9fea4d12cc594c6d83ab3c1823e58ab8253ba809a591dd10a2";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_hvac_supply_system_oracle.py";
    private const int GeneratorBytes = 75_371;
    private const string GeneratorSha256 =
        "sha256:a4bb12756e28697389d1850f81f2d231d8266ab6a72259a20085a59835b6b8d9";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_hvac_supply_system_oracle.py";
    private const int ValidatorBytes = 21_726;
    private const string ValidatorSha256 =
        "sha256:52b11bb8f4afc05feedd74fd475940c1b248371effd6dcaea59fd2d8eb5ba033";
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
        "sha256:b47c0d48a11ff74cfe713378121b99368353f1b492e8f6de9b570a949c3a7939";
    private const string NativeSourcesSha256 =
        "sha256:5e85f7f593aafe22f2688fe9aa9e2698190d4dbd54908d6a24e0d987325233c1";
    private const string NativeReviewSha256 =
        "sha256:7970cc0df3abc11f86dfef9f12b9605907818f7077dd275d20796c8e8701a60b";
    private const string NativeTemplatePath =
        "fixtures/simple-dragon/grm/ASHRAE 140 modified.grm";
    private const int NativeTemplateBytes = 9_154;
    private const string NativeTemplateSha256 =
        "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.HvacSupplySystemOracleParityTests.MatchesPinnedHvacSupplySystemsThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SupplySystem.cs", 6_456,
            "sha256:7ee0ec0b4eca1a78b4c6df5f6ba452b784bf09859ff24e6f50c681d16a63f1cb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_641,
            "sha256:a212275276ccff153d5df42a44a46ac8877afa485e315ee27d08767a909f29bb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_646,
            "sha256:40e6e7aa1eb89cb341c7e7a32471fa029024e49b261dce8a8926514109d727ba"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_154,
            "sha256:9462f609a4a7a4e062612e4058921b0c91931dc8ff7216dbe54e258cb59ec22c"),
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
        Target(147, "AirHandlingUnit", "class", "epsimple-hvac-supply-system-147-6fd0030b", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-6fd0030b", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.AirHandlingUnit and public properties", 0),
        Target(148, "AirHandlingUnit.ID", "function", "epsimple-hvac-supply-system-148-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 0),
        Target(151, "AirHandlingUnit.__init__", "function", "epsimple-hvac-supply-system-151-ea6e311c", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-ea6e311c", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.AirHandlingUnit and public properties", 0),
        Target(154, "AirHandlingUnit.from_json", "function", "epsimple-hvac-supply-system-154-148b0ee3", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-148b0ee3", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 0),
        Target(155, "AirHandlingUnit.source", "function", "epsimple-hvac-supply-system-155-ef79e1d5", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.SourceSystem", 0),
        Target(156, "AirHandlingUnit.to_dragon", "function", "epsimple-hvac-supply-system-156-11a6909a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-11a6909a", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 0),
        Target(209, "ElectricRadiantFloor", "class", "epsimple-hvac-supply-system-209-f7f03ff5", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-f7f03ff5", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiantFloor and public properties", 1),
        Target(210, "ElectricRadiantFloor.ID", "function", "epsimple-hvac-supply-system-210-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 1),
        Target(213, "ElectricRadiantFloor.__init__", "function", "epsimple-hvac-supply-system-213-f8bde28f", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-f8bde28f", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiantFloor and public properties", 1),
        Target(216, "ElectricRadiantFloor.from_json", "function", "epsimple-hvac-supply-system-216-b13a9536", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b13a9536", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 1),
        Target(217, "ElectricRadiantFloor.source", "function", "epsimple-hvac-supply-system-217-b14aeb3a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b14aeb3a", "Dragons.SimpleDragon.SourceSystem.SourceSystem", 1),
        Target(218, "ElectricRadiantFloor.to_dragon", "function", "epsimple-hvac-supply-system-218-01ae7da4", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-01ae7da4", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 1),
        Target(219, "ElectricRadiator", "class", "epsimple-hvac-supply-system-219-6354666e", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-6354666e", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiator and public properties", 2),
        Target(220, "ElectricRadiator.ID", "function", "epsimple-hvac-supply-system-220-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 2),
        Target(223, "ElectricRadiator.__init__", "function", "epsimple-hvac-supply-system-223-3a47135f", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-3a47135f", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.ElectricRadiator and public properties", 2),
        Target(226, "ElectricRadiator.capacity", "function", "epsimple-hvac-supply-system-226-09cfea01", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.HeatingCapacity", 2),
        Target(227, "ElectricRadiator.from_json", "function", "epsimple-hvac-supply-system-227-20bd3338", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-20bd3338", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 2),
        Target(228, "ElectricRadiator.source", "function", "epsimple-hvac-supply-system-228-b14aeb3a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b14aeb3a", "Dragons.SimpleDragon.SourceSystem.SourceSystem", 2),
        Target(229, "ElectricRadiator.to_dragon", "function", "epsimple-hvac-supply-system-229-4b95c9d6", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-4b95c9d6", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 2),
        Target(230, "FanCoilUnit", "class", "epsimple-hvac-supply-system-230-618e77c4", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-618e77c4", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.FanCoilUnit and public properties", 3),
        Target(231, "FanCoilUnit.ID", "function", "epsimple-hvac-supply-system-231-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 3),
        Target(234, "FanCoilUnit.__init__", "function", "epsimple-hvac-supply-system-234-ea6e311c", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-ea6e311c", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.FanCoilUnit and public properties", 3),
        Target(237, "FanCoilUnit.from_json", "function", "epsimple-hvac-supply-system-237-4e773b8a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-4e773b8a", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 3),
        Target(238, "FanCoilUnit.source", "function", "epsimple-hvac-supply-system-238-ef79e1d5", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.SourceSystem", 3),
        Target(239, "FanCoilUnit.to_dragon", "function", "epsimple-hvac-supply-system-239-09f12474", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-09f12474", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 3),
        Target(271, "PackagedAirConditioner", "class", "epsimple-hvac-supply-system-271-fcef6339", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-fcef6339", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.PackagedAirConditioner and public properties", 4),
        Target(272, "PackagedAirConditioner.ID", "function", "epsimple-hvac-supply-system-272-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 4),
        Target(275, "PackagedAirConditioner.__init__", "function", "epsimple-hvac-supply-system-275-b2021d84", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b2021d84", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.PackagedAirConditioner and public properties", 4),
        Target(278, "PackagedAirConditioner.capacity", "function", "epsimple-hvac-supply-system-278-09cfea01", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCapacity", 4),
        Target(279, "PackagedAirConditioner.cop", "function", "epsimple-hvac-supply-system-279-873a49d3", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.CoolingCop", 4),
        Target(280, "PackagedAirConditioner.from_json", "function", "epsimple-hvac-supply-system-280-d49a3e1b", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-d49a3e1b", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 4),
        Target(281, "PackagedAirConditioner.source", "function", "epsimple-hvac-supply-system-281-b14aeb3a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-b14aeb3a", "Dragons.SimpleDragon.SourceSystem.SourceSystem", 4),
        Target(282, "PackagedAirConditioner.to_dragon", "function", "epsimple-hvac-supply-system-282-0be4894a", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-0be4894a", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 4),
        Target(296, "RadiantFloor", "class", "epsimple-hvac-supply-system-296-3a70e982", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-3a70e982", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.RadiantFloor and public properties", 5),
        Target(297, "RadiantFloor.ID", "function", "epsimple-hvac-supply-system-297-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 5),
        Target(300, "RadiantFloor.__init__", "function", "epsimple-hvac-supply-system-300-ea6e311c", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-ea6e311c", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.RadiantFloor and public properties", 5),
        Target(303, "RadiantFloor.coolable", "function", "epsimple-hvac-supply-system-303-b81ea250", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Coolable", 5),
        Target(304, "RadiantFloor.from_json", "function", "epsimple-hvac-supply-system-304-a3c19218", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-a3c19218", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 5),
        Target(305, "RadiantFloor.heatable", "function", "epsimple-hvac-supply-system-305-0b60e64a", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Heatable", 5),
        Target(306, "RadiantFloor.source", "function", "epsimple-hvac-supply-system-306-ef79e1d5", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.SourceSystem", 5),
        Target(307, "RadiantFloor.to_dragon", "function", "epsimple-hvac-supply-system-307-db124859", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-db124859", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 5),
        Target(308, "Radiator", "class", "epsimple-hvac-supply-system-308-8464a277", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-8464a277", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.Radiator and public properties", 6),
        Target(309, "Radiator.ID", "function", "epsimple-hvac-supply-system-309-246156d9", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Id", 6),
        Target(312, "Radiator.__init__", "function", "epsimple-hvac-supply-system-312-35304b6f", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-35304b6f", "Dragons.SimpleDragon.SupplySystem constructor with SupplySystemType.Radiator and public properties", 6),
        Target(315, "Radiator.capacity", "function", "epsimple-hvac-supply-system-315-d699d5f1", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.HeatingCapacity", 6),
        Target(316, "Radiator.from_json", "function", "epsimple-hvac-supply-system-316-349b941b", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-349b941b", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) supply-system dispatch", 6),
        Target(317, "Radiator.source", "function", "epsimple-hvac-supply-system-317-ef79e1d5", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.SourceSystem", 6),
        Target(318, "Radiator.to_dragon", "function", "epsimple-hvac-supply-system-318-bb8edb65", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-bb8edb65", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 6),
        Target(321, "SupplySystem", "class", "epsimple-hvac-supply-system-321-d236c0a0", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-d236c0a0", "Dragons.SimpleDragon.SupplySystem constructor and public properties", 7),
        Target(322, "SupplySystem.TYPE_MAPPER", "constant", "epsimple-hvac-supply-system-322-3639f058", "exception", "reviewed-native-discriminated-supply-aggregate-and-conversion-route-3639f058", "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) with SupplySystemType dispatch", 7),
        Target(323, "SupplySystem.coolable", "function", "epsimple-hvac-supply-system-323-a658d7c4", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Coolable", 7),
        Target(324, "SupplySystem.heatable", "function", "epsimple-hvac-supply-system-324-9d89b0d8", "equivalent", "not_applicable", "Dragons.SimpleDragon.SourceSystem.Heatable", 7),
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(28, "sha256:6898e5eb4ca13a968c84145f50d2032894a3ac9124d2d4f555f21714876f549f"),
        new(24, "sha256:6646ade2857fedb82b743bfaa69fbb2efdbe7a78cc24a6dc6469fd860e30e9bc"),
        new(26, "sha256:a1f48098996a07d757942a54b57ffd0f11b1b888f748d427ae1ea35a98a7d120"),
        new(30, "sha256:e91ddeba0a641f7ecf9e1d26ebb30a2d54286b062bcd735986bf0d69ee632ba7"),
        new(29, "sha256:434df20662550e48786c6cd50830e77a46365be3a8d1e6bca2a79425efe3b03a"),
        new(27, "sha256:98786bc58ad481912d676daa9c78c8ddd3b3bbeaed83f47cc7207a076ed89074"),
        new(27, "sha256:57026cbf1de618e6fd889314bf6114da274d97d1e1cd3ea9737a56ef9bb392b4"),
        new(19, "sha256:dcceabfda17fd325a7d3cf04ff576ccf267bb42a5272c81e1c0674b85bc1f6bc"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:375b24dd67717fcb3888d7fc2912c07ec07f34651b774ba9ba80907909ec5248",
        "sha256:c19817575becc570d92b770b03ed538804fe85ee85ec4062dd9dff8a66bb9cbc",
        "sha256:c775f8e24ed71d562cabb272adb685deaff9cbdc129f87e9655a562eeebd38e1",
        "sha256:1700ace7e0e9b0b4d8de3e2344173bc051370c438627e53228eb1ef609189aa8",
        "sha256:f81a05589ec73f6a8538a963981fd61b23ea895686f036628285d67952c9d28b",
        "sha256:443df1ebac44bb0bcd096a0b2760b7749c42132a83f78cf2b3403de3d1b54fe7",
        "sha256:267a35d93fc5f8d6cf8afa16f983d0dda4b5a9458ccfba27b83fa799fc4bd19c",
        "sha256:8bc9a8dcbcd99239ed4298b2edb6ebea7699b9b2b7cf700d80f76162b9409640",
        "sha256:e5b40934436af30872ed7847d67dff25c7ba4d4ed9d14804a97793b6867da3c8",
        "sha256:9ede7e2b8af1b675a5c07020aae78ca3861763fb194c9ca00b7eb783ab08e0a3",
        "sha256:6e1d6222591633a753b46c5c527398a62ca59c6d7c7e512533b9ab93be8da6b0",
        "sha256:fbab4ecc7a0c9bb6c9020ef8c413ad20531d31342e3bf09713e12cd1ace46d3c",
        "sha256:9ea4444aa2df1dabfa543eb04526b77cee13b2e1f2f3f907f3c6dde33f562955",
        "sha256:1f0034bce6aa0b20832d211c3999be5cc8c9029a1871f2ecf2c53bd1081c722f",
        "sha256:53d1897985943fc6ded5f3b2897c7359a9db8429a32fb454b0bbcf48e939fa21",
        "sha256:29de56f0bf86c605fe46909d85d8cdb1eacd63f186b27c7d2c24348bb8397455",
        "sha256:42097397df9474540dfbd4be770601007d53d84a61e1c6ff6cc9c099ce1dd1c0",
        "sha256:a8894470afcfacdb5bb3042946546073297d008b2aedcbf3f9f850a084f577e3",
        "sha256:e7e3c60d2015f85923fcfd88a4f0155fa525aee1f237429e38ff56712f1cbe07",
        "sha256:239d1ea9ad085fcf459a62871c4f7933665c00bcd82cd138b683abd14db97e2f",
        "sha256:615597dd4d4343e874386ecd2c7f481dd70bc3d205be70c718c51afcca182f04",
        "sha256:d147f618e0bee473518dfb23f6c9d1620c16223da1f4f064054b906f111f4d93",
        "sha256:a37818fc0ce568ca3604a5700590b1a31143f5afcd2c4b617cb79f968a959204",
        "sha256:203c80c0132dae1f8c1987b5d812d97fd651377836903bb461efbc32e6eae310",
        "sha256:213e506d084e073e3158b1167dd41349e7b7823a9695b476b3806c600def5a52",
        "sha256:c55bbad6ab9c6828934344d258d2945dc91dd3d09c210502fabfc05da224ceec",
        "sha256:fb3bad5113222bcb9b9477fab268eca8ad6e65bbf65aae1228b978bcdcf2a739",
        "sha256:d95f94baa35176c4f2b43d939cfb919ef537acf90e4e874e08d4d92c7cf42524",
        "sha256:4c4574353f82ae86a1f75ef843184a1bd611e65baa567e48920779f5464590bc",
        "sha256:51a4e0e160d7010317ec87e857e28a55d1241f4cf1a179ca5dab90374849f686",
        "sha256:b6f72a9d331126a88a5c432b5382c1d1bd15f0ad134e0f34e36d59ffdc4b792f",
        "sha256:3474646c86d832af0446b144665a950d6b97a0facaf8563f1b45d031293b1a6c",
        "sha256:193cbd4530e1a625518270314d42eaf04b55101d9c52780dcb7ba0607e8caca7",
        "sha256:6f6ebe1aea7fc807b67b5810fd9fca0821d5af94fd81940a78cb17a8933daac4",
        "sha256:79eade608d415f54ad69e4c3dfc6ab968aa4c81436b38188c858fa0f70300cd2",
        "sha256:f1e1884dc04daf73a501631158da5b441e781d1e75ce9307385d8ab2126bfeab",
        "sha256:fd73eee3d73ce9dee845a549f7c55afe81312b77cd97c18b06da15b98f080510",
        "sha256:51293e101ea8089cda089326ff34800b16006081e4a8860db5dd0c9f2fdd941b",
        "sha256:9c14197ce50c732e82c950ce1668570cfc65377176d747afaa3dc506b511539e",
        "sha256:b9b08e5daefa3ac58b3728763f7aef6c7ed379fb8f138789b8b94c94ecb774b4",
        "sha256:6adb0149c749c81702ee395eac4af9abf35df68e29b495c77e98a6721990c1b5",
        "sha256:044dc7619b524d74dfdea86ca2eb8b4c40d2fd21ee02310bf21e83a3b1b933c1",
        "sha256:a730b720363f1be68f19e68400a3902f7dbe8d41264b0d4912d60483a83c8a6c",
        "sha256:f808bc3a1ea7dc1c892bfe14ffc7f7ea5ada51d0be0b303610fecaf0895fb543",
        "sha256:32fb5bf9d2e01ce468e4aab16bf798fa535c050213481a741042ec75ec91936c",
        "sha256:2566c8dcca56331651c9a0de7b83b4f87fa18fcdd403b74fb3b8b702415d2bcb",
        "sha256:376175a09207cdd270f74a67607bb2a6f815421012dac1d851b8929c894336c1",
        "sha256:92c8cbc98d4beea17689bc37f8ce775a7d1718a5094ec62c673540015a6884cd",
        "sha256:ccf38ea0418015be1dc44b7a8a1dfe36ba7d6eef464fea817eddb7790c4d91d2",
        "sha256:648becc4be7a9ad80ae9e1624284bdc17da0ca936da1ee05e56c9d33a5c3beb2",
        "sha256:443e880d4838d08aa6ab867af4054106e84a53e174c1202cc15057c28784ec3e",
        "sha256:b9c665f2d6771daed523393ba08e4bcd39414f0561e26252a1e05c0b25430ca8",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:f311bf292fd48f8cf9fa71da9e31d29d87b4b2589a4cba280aaba593b51b524d", // epsimple-hvac-supply-system-147-6fd0030b
        "sha256:8228e3248b5e404740972fe57b819b9c4d2bf070b0ad2ccf52265244234a0e81", // epsimple-hvac-supply-system-148-246156d9
        "sha256:83d9a8d3b49cdc8fadcfe20a3079bf00164fc883e83246d732ec302a828c92fb", // epsimple-hvac-supply-system-151-ea6e311c
        "sha256:22e1b90301fa1b4d6252e6518f6df3d5f5c603ad008be32364fb3b8f178610f8", // epsimple-hvac-supply-system-154-148b0ee3
        "sha256:078242b5b0c47b381298d172876ddb92360296cefb4490b4d53b4aa8f255aef2", // epsimple-hvac-supply-system-155-ef79e1d5
        "sha256:4380a2e8c45bf26daf16612c82aaa3b7eb9ca4c5b5521bef703d344ae08ef227", // epsimple-hvac-supply-system-156-11a6909a
        "sha256:01b4249cf11a2070274c020ced3814a0b7116aa20d8d3ffc3d77b54bfc0e154e", // epsimple-hvac-supply-system-209-f7f03ff5
        "sha256:69a8c5443e3185111808c16400f2745cc52327b2f6b634e16d6e1ed3db40ccf3", // epsimple-hvac-supply-system-210-246156d9
        "sha256:144af64d9e6ffb54deac39c34adfc9f0000ccad691b8362d93d4cb009c500bc1", // epsimple-hvac-supply-system-213-f8bde28f
        "sha256:889d90b39251b7d99353519771ddef501b6da4529a026624ba3e1121adcb80f0", // epsimple-hvac-supply-system-216-b13a9536
        "sha256:389bf261055bd27877f6ab3799a663853ee4ed811180744eb7b5e1cd5bd3cea0", // epsimple-hvac-supply-system-217-b14aeb3a
        "sha256:97f34d1531406634f1eaff9b9e594281757284ea85e447aeab9b404cf5a8950c", // epsimple-hvac-supply-system-218-01ae7da4
        "sha256:23db44370c5f5aa842486fdf5fd543e697d5ba451e85ffe756ce4d714b4135c3", // epsimple-hvac-supply-system-219-6354666e
        "sha256:5e4a73eed88644ac626b2c80b5f4623d5aff39118ff542a7be5a1c191e4b23d8", // epsimple-hvac-supply-system-220-246156d9
        "sha256:bd28d534e034e42417ab56581b38371ee9d89e4806ac376097412cc7f0b32495", // epsimple-hvac-supply-system-223-3a47135f
        "sha256:74d2b78b0ab6f2943f1307cee613c555bcf0a513b59556b9162785b684c7e9cf", // epsimple-hvac-supply-system-226-09cfea01
        "sha256:dec0aab4256deabd447aaddd3bf56e955af090d7563efd96665322df811d0edd", // epsimple-hvac-supply-system-227-20bd3338
        "sha256:33088ce4e62585567929f944d7b33b1c72edaa2f2062f54d18cfa2137c19b096", // epsimple-hvac-supply-system-228-b14aeb3a
        "sha256:f91896659818b907d3d3f0f45763eafc90383c061c490a1b71e6602939806de3", // epsimple-hvac-supply-system-229-4b95c9d6
        "sha256:e4576a4ecbb242bde1eae4572e98f9f9238c5ae11032a19ad16a58cd6d62e353", // epsimple-hvac-supply-system-230-618e77c4
        "sha256:9a192eb4e2acbd7939bcaa001e9488733e791d6b3a546ea9980bea9522dbde0c", // epsimple-hvac-supply-system-231-246156d9
        "sha256:efc1ee3adf4acbb26643e10480eb3a8b0ff4941d06cafe921d507fb150dc315a", // epsimple-hvac-supply-system-234-ea6e311c
        "sha256:bc7d8ad8b9a5eae972b52c92ba131741851366d125f35e511d7fea4709fc3573", // epsimple-hvac-supply-system-237-4e773b8a
        "sha256:5f416ddf2f609d65b92307a344e730e8b889e083c62a26cb1f2b4c2c456f33c8", // epsimple-hvac-supply-system-238-ef79e1d5
        "sha256:d34510d37cb54b79c3d517b88e7474955c2fa19215b91df38dcfc3890edad45b", // epsimple-hvac-supply-system-239-09f12474
        "sha256:e43fa61c1b9178c76c598850c4d7563903017fd19c777396661793df27766328", // epsimple-hvac-supply-system-271-fcef6339
        "sha256:3770fa68c89bedc072fab510c4d653e19b4fe2257e980e6fc6e07ca24e69dfe1", // epsimple-hvac-supply-system-272-246156d9
        "sha256:c86c707aa74d477d0c39b1ac40cefcd1b9089693ee88ece4e682469f6e33c3c2", // epsimple-hvac-supply-system-275-b2021d84
        "sha256:6fa7af9e9a0f38304b26d9179b4384ed6c3e0ae92df70dc2f812f9a6476111b5", // epsimple-hvac-supply-system-278-09cfea01
        "sha256:a01aad2a3309cc4d24896ebb6265a2ae535bff9c40d687294d2a343ba8fde52c", // epsimple-hvac-supply-system-279-873a49d3
        "sha256:0dc69135d2db4bd45b03eedf49095a9a15ce97bbb60d4c34450a912ebe1ecdad", // epsimple-hvac-supply-system-280-d49a3e1b
        "sha256:9f3c7c1030b775397ad17374c0afd54ca2fa7d2a101973f6ce41d5aefe76b56b", // epsimple-hvac-supply-system-281-b14aeb3a
        "sha256:8ff7180dfc629255f7c0c4a512629bf67040cca2106b66d8fdd3263533d5fd5c", // epsimple-hvac-supply-system-282-0be4894a
        "sha256:a26a107e9e66245da5341398969d9403960b7c3f359f4493a969b73530a391cb", // epsimple-hvac-supply-system-296-3a70e982
        "sha256:78baa90b3c9120dba9fda19a381db351e775e6f57521b6b269ca999c6ba6bcfb", // epsimple-hvac-supply-system-297-246156d9
        "sha256:0e232ff43403f0ee30c069a63ce3d0dc56d7d3cff27cd41a4087b9687539c6e9", // epsimple-hvac-supply-system-300-ea6e311c
        "sha256:caf41f5b9f47b7facd3c33b88777678c5aed63b3494a972bc7ae5108b0efc7fe", // epsimple-hvac-supply-system-303-b81ea250
        "sha256:2291bdfe28d9d76ecad076dc61b84da9e8e8ab93fb5c8bcc61c2182aec57666d", // epsimple-hvac-supply-system-304-a3c19218
        "sha256:5833dd52fb7983647e5df94b6f74335478681b3fe208499e89f91782a554f6d1", // epsimple-hvac-supply-system-305-0b60e64a
        "sha256:351349cccdcbef4e5a13d48e85eeb6311e9634e6930b4231c754378d50ee2a7a", // epsimple-hvac-supply-system-306-ef79e1d5
        "sha256:123a2c28b205da5c074e36d818c2e771994ec78565eb62ece92db585896f8005", // epsimple-hvac-supply-system-307-db124859
        "sha256:8e015ccf36ab5aa556f47b4e11bcb24da9e67a89f0bdb714485ae2d07b241e2b", // epsimple-hvac-supply-system-308-8464a277
        "sha256:cca87e6beb93b90f152380bb1987920efc983eab5b5189712c9e25a94f2ad47d", // epsimple-hvac-supply-system-309-246156d9
        "sha256:8db29b667feded7705d0155b2310bf4646c1f42d34b56cff39e34ddeaa062756", // epsimple-hvac-supply-system-312-35304b6f
        "sha256:f8aaf4e38563bab7d79866ef9d5b62e3c79255b9a87ce5be5ace4edf69428a77", // epsimple-hvac-supply-system-315-d699d5f1
        "sha256:f0e6e2e5dffb0ee05a5cbffcfb234d09ab81893e4c7cb54b611b3c2d46124396", // epsimple-hvac-supply-system-316-349b941b
        "sha256:325329eb0cdb14d0f27e651606be0379cd193f63ad905d7aa0ec39478fd72803", // epsimple-hvac-supply-system-317-ef79e1d5
        "sha256:0355dadf23d4c5b347102124744d3485ed53b97bc3c03cf69701011dec747b64", // epsimple-hvac-supply-system-318-bb8edb65
        "sha256:469fa8dc2a18329b8694845553bb7c0cc22d5e94c37e9a91ea559417feb549b7", // epsimple-hvac-supply-system-321-d236c0a0
        "sha256:6cd7a05a8e412c8957702192c62956d40d4af787bebb6bfe99a054aee97feb86", // epsimple-hvac-supply-system-322-3639f058
        "sha256:738c8713764daf7b41db1dddc924dca3979fa8f643432d3e9ad85a15a4329ab0", // epsimple-hvac-supply-system-323-a658d7c4
        "sha256:6fdfb3717d3b1454ffa3b0976dbd927beb8bb019900bbd6d65aa1eb70b30b196", // epsimple-hvac-supply-system-324-9d89b0d8
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
            21_108,
            "sha256:555a1df41e5369dbbc44b0729a48673610a86951a215c8e2aa00cfa4fce156f1");
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
