#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Hvac;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.InvisibleDragon.Tests.Model;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Hvac;

public sealed class SupplyCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-hvac-supply-core-oracle.json";
    private const int FixtureBytes = 215_698;
    private const string FixtureSha256 =
        "sha256:dcf355329a083f9fac82434e18fc3b847a44bc134eb7f593f497c0aeae4c6b9f";
    private const string FixtureSchema =
        "goniegonie.python-reference.dragon-hvac-supply-core.v1";
    private const string FixtureRepositoryCommit = "07bcb7e";
    private const string CasesSha256 =
        "sha256:29eacb2d29f528353302d1afd8e3ef646d7d35886237bb4a3fa494039a4ec36f";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_hvac_supply_core_oracle.py";
    private const int GeneratorBytes = 65_898;
    private const string GeneratorSha256 =
        "sha256:3f1bcbf28df62c3426f8d343dab3f123b9c730bcdd234e3c570aaff21b87cd97";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_hvac_supply_core_oracle.py";
    private const int ValidatorBytes = 17_313;
    private const string ValidatorSha256 =
        "sha256:6832bde12cb4e5ab213f2f12307267ebe571de1bf2fc1a8ffa37db728014eabd";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";

    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/dragon/hvac.py";
    private const int UpstreamBytes = 137_833;
    private const string UpstreamSourceSha256 =
        "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0";
    private const string UpstreamAstSha256 =
        "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31";
    private const string TargetReceiptsSha256 =
        "sha256:3c2629b0da4e0e83c079276de2b744707227784b77f1bf78225eb194d8fb5bf2";
    private const string AdjacentReceiptsSha256 =
        "sha256:655edb7852d9b2028431fa50eaa72a753195a55c0fe2df58accda3059c82f40e";

    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Hvac.SupplyCoreOracleParityTests.MatchesPinnedSupplyCoreThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_582,
            "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/SupplySystems.cs", 18_267,
            "sha256:4de030455a8a1b8db0ca4eca7745c6501930c984f9d1e156e17cb0b752d845cf"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/HydronicSupplySystems.cs", 24_522,
            "sha256:c815c219d38294b0d3ba1a0ad2921e5ea77a90377887ed0fca8150bae46f96b2"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs", 22_015,
            "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_764,
            "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905"),
    };

    private static readonly ArtifactPin[] SupportFixtures =
    {
        new("fixtures/reference/python-0.7.0/dragon-hvac-supply-group-core-oracle.json", 31_163,
            "sha256:320ac62b8b9eccc9d4053a6b5ceb6fa3e825c329d1ac3d10f4c8c5cd89f0c092"),
        new("fixtures/reference/python-0.7.0/dragon-hvac-supply-group-to-idf-object-oracle.json", 22_608,
            "sha256:f1c3454cdf34eed1a47180b13bacab2dadf04a06883a34c214738ed6ef50a608"),
        new("fixtures/reference/python-0.7.0/dragon-model-add-supply-system-oracle.json", 15_122,
            "sha256:4896c54312c44bffc573d0dc4d0fddfff14d17b0c65a3f789b8f6a487e1f181c"),
    };

    private static readonly ArtifactPin[] SupportNativeTests =
    {
        new("tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Hvac/SupplyGroupCoreOracleParityTests.cs", 84_813,
            "sha256:52c10f25b87534b8c898002f343c04b7cafcbbe1095bbc182051e81ed55681bd"),
        new("tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Hvac/SupplyGroupToIdfObjectOracleParityTests.cs", 72_411,
            "sha256:81348fc57fee6ae6cb9773b5d22dbed68ed031310c34cef4b6db9fc191607207"),
        new("tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/EnergyModelAddSupplySystemOracleParityTests.cs", 50_818,
            "sha256:9f1156dcd9684c14ca84f29eca9266b4c1fac67973aa9f642a67215acf3de801"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "dragon-hvac-supply-core.air-handling-unit-state-capability-naming-idf", "air-handling-unit", "sha256:bd9fa9a6386ebe278115d03fe49f23c14826adcb4c0ddcb81d126db4d535b6dd", "sha256:11285c61555f77b299a5382f2f2c0b89563e3709de20b69dc936bd5469ccbf7a", 6),
        new("EF01", "dragon-hvac-supply-core.electric-radiant-floor-state-capability-source-idf", "electric-radiant-floor", "sha256:a2974b2e5a5270a529e276406a2ae5e91f246fdc3cdf6307080e86bd24edbc7f", "sha256:1489da36a6b76efe2b68b10cff98020176e7e49a9d5e6da98fc91adf756d9cfb", 7),
        new("E01", "dragon-hvac-supply-core.electric-radiator-state-capability-source-idf", "electric-radiator", "sha256:41c5b623e527eb5b64894552d2a6ef55867c14f4d627b48503894e64e54bd561", "sha256:308644678a1a520855e07c1a8eff86876aa6eb21129c838d165a9224d7394c89", 7),
        new("F01", "dragon-hvac-supply-core.fan-coil-source-combinations-capability-idf", "fan-coil-unit", "sha256:62984652cf966e2a5fac342b40917764327d3e55cf585fdeba369318192bc6d5", "sha256:0229dc750a4aa81078b0485259ed08e85c2462ef5dcd631b5e5ddfc90647f60a", 6),
        new("P01", "dragon-hvac-supply-core.packaged-air-conditioner-capability-inherited-idf", "packaged-air-conditioner", "sha256:aaa97143c3b3280c9538c97da33b169d4d5eadc263385f051e4f50909c22b669", "sha256:91ef0cfd82648b8c50ae335ff94204818f604fa6f186d5d42fabb245e1282196", 3),
        new("RF01", "dragon-hvac-supply-core.radiant-floor-state-capability-validation-idf", "radiant-floor", "sha256:d98574a76002ee9dbee3fa23f4d302cae2a2e67a1c148f87757754e17e240a76", "sha256:d7aee55947597439f7f74994f96c5280b3f8bb8618c3db7fd62f4ac821846183", 6),
        new("R01", "dragon-hvac-supply-core.radiator-state-capability-validation-idf", "radiator", "sha256:e45ae73c778fc3411a47b0739ab5dad33f57d325cd6ce7280800a1321943a4ed", "sha256:dc0ec639fed2d171f7a83920ba8bf02ccfd286175254dbe097c2b49f91bbe0d4", 6),
        new("G01", "dragon-hvac-supply-core.supply-group-availability-order-sources-idf", "supply-group", "sha256:7886f188c4f89c422a27e6b067962709fe33ca52e567fbd66d11bbdef8e03ca4", "sha256:e26219508dc3894a97d6c83c229b6c36cf256e2c04a9c34298cf72e8cda0e255", 1),
        new("S01", "dragon-hvac-supply-core.supply-system-abstract-naming-rules", "supply-system", "sha256:abd1b10feddd575bfa770667c233814cc8f5d4e93b8999a9670a5dff4a7a0b52", "sha256:aa47fc70622fd0777e10ef96e7d3b1c98f7f9309bea4f3f238bc2b5f5e7d6d4c", 7),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        new(645, "AirHandlingUnit", "class", "sha256:5a79613c19fc0d823fbc000d2ed658cdda337413d8bb800db163941f3f4581b3", "dragon-hvac-supply-core-645-airhandlingunit", "exception", "reviewed-public-aggregate-supply-emission-0fa0b1c0", "GonieGonie.InvisibleDragon.Hvac.AirHandlingUnit public constructor and immutable properties", Cases[0].CaseId),
        new(647, "AirHandlingUnit.__init__", "function", "sha256:613f0db31794614f9caf37919cb05b7f89be5db2481d962f37b4066e30b4eee2", "dragon-hvac-supply-core-647-airhandlingunit-__init__", "exception", "reviewed-public-aggregate-supply-emission-56ec0ea0", "GonieGonie.InvisibleDragon.Hvac.AirHandlingUnit public constructor and immutable properties", Cases[0].CaseId),
        new(648, "AirHandlingUnit.coolable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-648-airhandlingunit-coolable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.AirHandlingUnit.CanCool", Cases[0].CaseId),
        new(649, "AirHandlingUnit.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-649-airhandlingunit-heatable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.AirHandlingUnit.CanHeat", Cases[0].CaseId),
        new(650, "AirHandlingUnit.idf_objtypename", "function", "sha256:0845568f05ccaf9cc73c6fa6f8dc56166527245cf42b47e10feaac9d5705ad88", "dragon-hvac-supply-core-650-airhandlingunit-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-fac0b3fb", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[0].CaseId),
        new(651, "AirHandlingUnit.to_idf_object", "function", "sha256:77bf437580b1e295246d810d77ec62300113f45a411672f5832332b843fc8a1e", "dragon-hvac-supply-core-651-airhandlingunit-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-423008b9", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[0].CaseId),
        new(700, "ElectricRadiantFloor", "class", "sha256:16e58394a533fffaa611babbcd04ca4099b58e5bfb650536ced8dd558726a1fe", "dragon-hvac-supply-core-700-electricradiantfloor", "exception", "reviewed-public-aggregate-supply-emission-96fb711a", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiantFloor public constructor and immutable properties", Cases[1].CaseId),
        new(701, "ElectricRadiantFloor.__init__", "function", "sha256:da1996945b3335573ed6a4a903997b5e97950f59f0e19da2cb57abc57dac04f5", "dragon-hvac-supply-core-701-electricradiantfloor-__init__", "exception", "reviewed-public-aggregate-supply-emission-40523ffc", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiantFloor public constructor and immutable properties", Cases[1].CaseId),
        new(702, "ElectricRadiantFloor.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-702-electricradiantfloor-coolable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiantFloor.CanCool", Cases[1].CaseId),
        new(703, "ElectricRadiantFloor.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-703-electricradiantfloor-heatable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiantFloor.CanHeat", Cases[1].CaseId),
        new(704, "ElectricRadiantFloor.idf_objtypename", "function", "sha256:068b2c4586afd184de3e090cb9db089a186ac1e5b05d65e70a7547383594f4c5", "dragon-hvac-supply-core-704-electricradiantfloor-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-a210f9fb", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[1].CaseId),
        new(705, "ElectricRadiantFloor.source", "function", "sha256:0b3b6343593c8c54b99ac82389147b686fbf461011b3dac7e73c97fd512bbc78", "dragon-hvac-supply-core-705-electricradiantfloor-source", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiantFloor.Source", Cases[1].CaseId),
        new(706, "ElectricRadiantFloor.to_idf_object", "function", "sha256:5076d17659796d0620a3f3b9de5b28fe7e9ff9772dc58edc44379767cd8a6bbb", "dragon-hvac-supply-core-706-electricradiantfloor-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-3b86b9c5", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[1].CaseId),
        new(707, "ElectricRadiator", "class", "sha256:6e4ce6d4489fd995f5cf5ebfd4ca8a96db68c7b5d0bb271fbf37a9ea01dbdf33", "dragon-hvac-supply-core-707-electricradiator", "exception", "reviewed-public-aggregate-supply-emission-1951b454", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiator public constructor and immutable properties", Cases[2].CaseId),
        new(708, "ElectricRadiator.__init__", "function", "sha256:07f43ff08d4fb608d661c8399fbf11db4f1d8c7c504e81a6e8b8e9e223772ba5", "dragon-hvac-supply-core-708-electricradiator-__init__", "exception", "reviewed-public-aggregate-supply-emission-09edd678", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiator public constructor and immutable properties", Cases[2].CaseId),
        new(709, "ElectricRadiator.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-709-electricradiator-coolable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiator.CanCool", Cases[2].CaseId),
        new(710, "ElectricRadiator.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-710-electricradiator-heatable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiator.CanHeat", Cases[2].CaseId),
        new(711, "ElectricRadiator.idf_objtypename", "function", "sha256:ad7100cedd4c1714558ea86974be894db62f45a56e5f567ca30542f73775ab69", "dragon-hvac-supply-core-711-electricradiator-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-50af1192", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[2].CaseId),
        new(712, "ElectricRadiator.source", "function", "sha256:0b3b6343593c8c54b99ac82389147b686fbf461011b3dac7e73c97fd512bbc78", "dragon-hvac-supply-core-712-electricradiator-source", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.ElectricRadiator.Source", Cases[2].CaseId),
        new(713, "ElectricRadiator.to_idf_object", "function", "sha256:e15931923550afa35476f5cdcf01c7bbfa434329b26fed648611f1b673303be5", "dragon-hvac-supply-core-713-electricradiator-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-c25777d6", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[2].CaseId),
        new(720, "FanCoilUnit", "class", "sha256:0e1ac21dcd08a80f9680490d923aee8606a25058b69396099b1379c38440e6c8", "dragon-hvac-supply-core-720-fancoilunit", "exception", "reviewed-public-aggregate-supply-emission-d16c27dc", "GonieGonie.InvisibleDragon.Hvac.FanCoilUnit public constructor and immutable properties", Cases[3].CaseId),
        new(721, "FanCoilUnit.__init__", "function", "sha256:613f0db31794614f9caf37919cb05b7f89be5db2481d962f37b4066e30b4eee2", "dragon-hvac-supply-core-721-fancoilunit-__init__", "exception", "reviewed-public-aggregate-supply-emission-5a46d94c", "GonieGonie.InvisibleDragon.Hvac.FanCoilUnit public constructor and immutable properties", Cases[3].CaseId),
        new(722, "FanCoilUnit.coolable", "function", "sha256:0ba94395670303b3e6651ede4428d86e8f57f73e6e3639078776b07cdac6502b", "dragon-hvac-supply-core-722-fancoilunit-coolable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.FanCoilUnit.CanCool", Cases[3].CaseId),
        new(723, "FanCoilUnit.heatable", "function", "sha256:8f083ca3f324853c52c4ef7f86bc3bc02f7b49e1683a051b7db482d4bc0e0a37", "dragon-hvac-supply-core-723-fancoilunit-heatable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.FanCoilUnit.CanHeat", Cases[3].CaseId),
        new(724, "FanCoilUnit.idf_objtypename", "function", "sha256:efb4de9d2381a1d947e222042ef668067bf17a29a519c6181b9e3185378cc65b", "dragon-hvac-supply-core-724-fancoilunit-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-3db9b91c", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[3].CaseId),
        new(725, "FanCoilUnit.to_idf_object", "function", "sha256:c46a171d406e558b24be0b4eb4902878a5bdd00fbe3b1232e252d1532519c411", "dragon-hvac-supply-core-725-fancoilunit-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-de1053a9", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[3].CaseId),
        new(750, "PackagedAirConditioner", "class", "sha256:9be40f1849ec90cb957163861b180166495fb07bf3552e11254d2ae0f394c505", "dragon-hvac-supply-core-750-packagedairconditioner", "exception", "reviewed-public-aggregate-supply-emission-aeefa6ef", "GonieGonie.InvisibleDragon.Hvac.PackagedAirConditioner public constructor and immutable properties", Cases[4].CaseId),
        new(751, "PackagedAirConditioner.coolable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-751-packagedairconditioner-coolable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.PackagedAirConditioner.CanCool", Cases[4].CaseId),
        new(752, "PackagedAirConditioner.heatable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-752-packagedairconditioner-heatable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.PackagedAirConditioner.CanHeat", Cases[4].CaseId),
        new(762, "RadiantFloor", "class", "sha256:e5d2eab0925e5f6a43c5c926c23f441024ac7644de91815cf665a4ce8cb34aa0", "dragon-hvac-supply-core-762-radiantfloor", "exception", "reviewed-public-aggregate-supply-emission-5d93e75b", "GonieGonie.InvisibleDragon.Hvac.RadiantFloor public constructor and immutable properties", Cases[5].CaseId),
        new(763, "RadiantFloor.__init__", "function", "sha256:dac554316222a52df56835df5bc12e1a13c2cd5778a72f4b831fbf7bd2e5226f", "dragon-hvac-supply-core-763-radiantfloor-__init__", "exception", "reviewed-public-aggregate-supply-emission-fced6522", "GonieGonie.InvisibleDragon.Hvac.RadiantFloor public constructor and immutable properties", Cases[5].CaseId),
        new(764, "RadiantFloor.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-764-radiantfloor-coolable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.RadiantFloor.CanCool", Cases[5].CaseId),
        new(765, "RadiantFloor.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-765-radiantfloor-heatable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.RadiantFloor.CanHeat", Cases[5].CaseId),
        new(766, "RadiantFloor.idf_objtypename", "function", "sha256:18cc149ba20da831a7a4514281ef9a1bfd72ba3e60a65038977de69552645fc6", "dragon-hvac-supply-core-766-radiantfloor-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-b04bffae", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[5].CaseId),
        new(767, "RadiantFloor.to_idf_object", "function", "sha256:bca28a99ac0a6614af9ca2274e92c42b835bafc6b033f897d673613966b5eab6", "dragon-hvac-supply-core-767-radiantfloor-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-6615b65b", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[5].CaseId),
        new(768, "Radiator", "class", "sha256:abc2c799006b9f22100106607e3b93bc2096281112eef9b24aff294409f76685", "dragon-hvac-supply-core-768-radiator", "exception", "reviewed-public-aggregate-supply-emission-e15f88dc", "GonieGonie.InvisibleDragon.Hvac.Radiator public constructor and immutable properties", Cases[6].CaseId),
        new(769, "Radiator.__init__", "function", "sha256:5a989318588944c57958250dd329ce17ac2a12115e6576344222055e7a9b6c93", "dragon-hvac-supply-core-769-radiator-__init__", "exception", "reviewed-public-aggregate-supply-emission-b4e3bc86", "GonieGonie.InvisibleDragon.Hvac.Radiator public constructor and immutable properties", Cases[6].CaseId),
        new(770, "Radiator.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-770-radiator-coolable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.Radiator.CanCool", Cases[6].CaseId),
        new(771, "Radiator.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-771-radiator-heatable", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.Radiator.CanHeat", Cases[6].CaseId),
        new(772, "Radiator.idf_objtypename", "function", "sha256:d6ab47e791de6994da51079773e814084c8bd9fe802b69ec926efa9034c29eb6", "dragon-hvac-supply-core-772-radiator-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-3fa24b28", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[6].CaseId),
        new(773, "Radiator.to_idf_object", "function", "sha256:c5eb7d0b9e37f45896426e2ea62b8d1cd9604cb8dc31ecb33df53625f5e63dfd", "dragon-hvac-supply-core-773-radiator-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-4277755b", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[6].CaseId),
        new(789, "SupplyGroup", "class", "sha256:f22147d1bab44415fda473980799cb75dc4ce6c57693b5d9ec0a5faaf131fe69", "dragon-hvac-supply-core-789-supplygroup", "exception", "reviewed-public-aggregate-supply-emission-cbcc48d4", "GonieGonie.InvisibleDragon.Hvac.SupplyGroup constructor/properties; ZoneHvacAssignment; EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?)", Cases[7].CaseId),
        new(797, "SupplySystem", "class", "sha256:13ed08986e2e8b8e9b6a3f9b9a1f387ad8075a99a5f79e6df18b2fd0280cfdc1", "dragon-hvac-supply-core-797-supplysystem", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.SupplySystem", Cases[8].CaseId),
        new(798, "SupplySystem.idf_get_airinletnodename", "function", "sha256:6a1a2503acd718c95c230511ed28dfa09ed7edcf75a93c9505776e3c1a99352e", "dragon-hvac-supply-core-798-supplysystem-idf_get_airinletnodename", "exception", "reviewed-public-aggregate-supply-emission-d2523a5e", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(799, "SupplySystem.idf_get_airoutletnodename", "function", "sha256:20339b80aa3e3f04fa2d841d355aa70da912ad0c2b3a2f2ab56be0a265b65c91", "dragon-hvac-supply-core-799-supplysystem-idf_get_airoutletnodename", "exception", "reviewed-public-aggregate-supply-emission-c90c06c1", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(800, "SupplySystem.idf_get_demandbranchname", "function", "sha256:eb09ae795f4bcb7489a4afc32e7f90f065f3ea7ee452893caa484b123271988b", "dragon-hvac-supply-core-800-supplysystem-idf_get_demandbranchname", "exception", "reviewed-public-aggregate-supply-emission-e95a8f49", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(801, "SupplySystem.idf_get_objname", "function", "sha256:f99114cd508825d71dceac2c8cf415e7307215266946dcb779b74cddb9f5532f", "dragon-hvac-supply-core-801-supplysystem-idf_get_objname", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Hvac.SupplySystem.ObjectNameFor(Zone)", Cases[8].CaseId),
        new(802, "SupplySystem.idf_objtypename", "function", "sha256:658520082df92fc4c03d549af63dad643ecb1962a52d7ce52cc27db4c5486918", "dragon-hvac-supply-core-802-supplysystem-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-cc4e1a21", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(803, "SupplySystem.to_idf_object", "function", "sha256:186909537d0e1c3f8e7e6fdcdac153f3ce50c8816fbef58a88266f97f3e59f87", "dragon-hvac-supply-core-803-supplysystem-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-1e9721aa", "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
    };

    private static readonly AdjacentBinding[] ExpectedAdjacent =
    {
        new(646, "AirHandlingUnit.__deepcopy__", "out_of_scope"),
        new(790, "SupplyGroup.__init__", "exception"),
        new(791, "SupplyGroup.coolable", "equivalent"),
        new(792, "SupplyGroup.cooling_systems", "equivalent"),
        new(793, "SupplyGroup.heatable", "equivalent"),
        new(794, "SupplyGroup.heating_systems", "equivalent"),
        new(795, "SupplyGroup.sources", "exception"),
        new(796, "SupplyGroup.to_idf_object", "exception"),
    };

    private static bool DiscoverPins => false;
    private static readonly NativePin[] ExpectedNativePins =
    {
        new("A01", 14, "sha256:5cbbd99dce705781c49aab68dfd97257fa17b7c9e288484a6b43755b28faa49b"),
        new("EF01", 14, "sha256:2cf556c84126d2f6fe9515ec3da16c4e32f9173656571a8074c9dfafb52ee717"),
        new("E01", 14, "sha256:faa2f7f81b056a358cfed7e98b263b20b788d2c941eb83a7c87fa3f90496aed5"),
        new("F01", 14, "sha256:0515e56ecb586816381436a382fac36dfa3aec59db5d8b44bf2873421b1554e7"),
        new("P01", 14, "sha256:637e8bfb9dde7a6897572a41297b16500cd0e9128227e6a58b0f9daea75aa2f5"),
        new("RF01", 14, "sha256:43495a0317b5ad73eec446010b47020eaf008408c1c3b000091ed9e03584a153"),
        new("R01", 14, "sha256:352273df2acecca15e848efa021053b5f36de8b22ec5d3fa36ead16c3b05fd18"),
        new("G01", 9, "sha256:7c2880b1a6128540305cff2aa49876116661d6e6255292d684b3e7d97241e098"),
        new("S01", 9, "sha256:f11d1808b4bf7746d3ba871fc752c4b8e5c41eaafe045d501dfb60fb49a71899"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:b89bd3f48f9dfd25aab99c924e6016885bdfcf8596185a28cd2436fc2e8fd153", // 645 AirHandlingUnit
        "sha256:6ee7a1994be096600b930f990e43a39a28e9e4065af918146a75a615ec0a8a4c", // 647 AirHandlingUnit.__init__
        "sha256:52296ddea3bd10c6c14842a2a9ef0aaef9f748ea7a07f973064907926639da4b", // 648 AirHandlingUnit.coolable
        "sha256:2560f73b157c8f0670359c4b2d46d3c035e913dea4b8e3a8132327b0d5438ee9", // 649 AirHandlingUnit.heatable
        "sha256:e0ff9f819892c972eea9258cfb08776ed0aa405c5a3d7c09678ddbf04ba93b92", // 650 AirHandlingUnit.idf_objtypename
        "sha256:04e64f1692a2f15b30a98e9d32089166d73f69185c18267347d1d62e427aa102", // 651 AirHandlingUnit.to_idf_object
        "sha256:36abbb86fb084f296bae38aff68757874e13ba9db0c9e86a69872d00c8845192", // 700 ElectricRadiantFloor
        "sha256:10d38946923fec27467b2f001d725cf871abbf9f077f4073fe715f1820de3395", // 701 ElectricRadiantFloor.__init__
        "sha256:5642d294d07c6497aeac8b83e0f1d2dcba2a30e8efa8d4b8ce606a515b771452", // 702 ElectricRadiantFloor.coolable
        "sha256:8bc3e85d035b0ebb132f1760dfe0e4cc1370434df5396aae0292bfba3b27fa68", // 703 ElectricRadiantFloor.heatable
        "sha256:ac88ce6ace18c353e2a97a4310ab2e06d42108ea06151af2f40883b64c987f16", // 704 ElectricRadiantFloor.idf_objtypename
        "sha256:5d7e04be1fa6e867426d8e9bdf484f500a6c4493c039a2359efc596cc5ca1a12", // 705 ElectricRadiantFloor.source
        "sha256:7166eed425b9011a4946ae1e8cd3a5ec5d29ad44d05799240db1f069d4d3cafa", // 706 ElectricRadiantFloor.to_idf_object
        "sha256:9eb2401d6121f5061b56eeca889db77deee0dab52ea5d03c920fbca5fa9eb894", // 707 ElectricRadiator
        "sha256:b11e364490777804cf93a8553d4ef946eefdd7d8ea9ae7a8bee804592f8b2ae7", // 708 ElectricRadiator.__init__
        "sha256:9988aea94455379e3cfee4ec1c42a95f925ae641be5f13a8321dd21a3707e75e", // 709 ElectricRadiator.coolable
        "sha256:ff95959d6da16d76372602763d2fcf3b31bed61f357b3faefdc6a6b58067db97", // 710 ElectricRadiator.heatable
        "sha256:e638200091b050adddccf619e32fb67b97e7a19eb98727670bdee477b48b3f26", // 711 ElectricRadiator.idf_objtypename
        "sha256:235e40b915c41868432392b2b26b3d5465366c8be0354bef7517cd621a51d560", // 712 ElectricRadiator.source
        "sha256:c39e5bea0bd680f93d7bdb88ce88b93b8e9c8144162570a87e066df9a4c9a2a6", // 713 ElectricRadiator.to_idf_object
        "sha256:4087ccef3db8a36c087e1cc00fad72a4b5365331abefa2ea0e2591b4c1b989db", // 720 FanCoilUnit
        "sha256:857283e6993d184ba8319d6a53a70efd0b5eb8bd805408fa23e9ce031ffe23e6", // 721 FanCoilUnit.__init__
        "sha256:687d94a5a569be7a08cb4f53bbd7d74a383a357cd8e73bc406fda812957a5b00", // 722 FanCoilUnit.coolable
        "sha256:779eed710afb5bbea019602d00c8f9300ab94652259828c9a44a53728c4f5d44", // 723 FanCoilUnit.heatable
        "sha256:00d7009794f8826dc8092e22f9d0e2b9ecc22faa70a907606f8cf6ee6b9fb5a8", // 724 FanCoilUnit.idf_objtypename
        "sha256:8b48704d90d7091440cbcbd7a893a123abdbe32da58d500713313542b1f10518", // 725 FanCoilUnit.to_idf_object
        "sha256:9594fbdcde41aeefc35b853b4509f63968a72b985cbe463fc74c6b95b236e3b7", // 750 PackagedAirConditioner
        "sha256:445ab5586db836384806ec8b8a124d1849a730dcc101e1d13d555d18ff83b9c8", // 751 PackagedAirConditioner.coolable
        "sha256:3b7e37afcf90a60c91f8a29d84bd7b4eee4aecabfe008781b5a85c7aa0d4a33b", // 752 PackagedAirConditioner.heatable
        "sha256:c8cf8a8e200e8df5179021240fbb6c7b6ff03b1ed1d02a8b5f57240d58be550f", // 762 RadiantFloor
        "sha256:52496588e4218a287e5682e475c77b0284183e6f34e7335655fd74f0f84cf59a", // 763 RadiantFloor.__init__
        "sha256:2abd6b3bd2577e8560bde7a91b82d7914baaec8e54fc049f978301d88ac1ac91", // 764 RadiantFloor.coolable
        "sha256:09c1d60890f779afe1e078b94863402a215c7e3152145fdb63cbba06d1b32e1c", // 765 RadiantFloor.heatable
        "sha256:bd7925bcb0adee5d2b15486d0ca3ff732be1540b985d8cda799974948ac8bb9d", // 766 RadiantFloor.idf_objtypename
        "sha256:0aeb7e1d5813eee0e5de9e0322fac6a5406db0ce2cd3ac43e1f1e124afc4ff37", // 767 RadiantFloor.to_idf_object
        "sha256:a22adde19796da4d17dcf439c3d550a321e06822ef1c7ce00c8f80d4d6987a20", // 768 Radiator
        "sha256:73bc5a860706c6158a23a308a87459da328984f57895ddb2d2688c13237061b6", // 769 Radiator.__init__
        "sha256:534c06463d4fcb4e4d9fc08c413d1cd3fb2e6b1cb7d0fedf47f8cae6caf05d05", // 770 Radiator.coolable
        "sha256:6cdb674b5f334434795fb2296c10db6f0e03f935daa53e591f56405ae74ef64a", // 771 Radiator.heatable
        "sha256:d3d2fb86d1ff6490a7153b0263363cc9f01609b373a6e4970e50037f1da91d0d", // 772 Radiator.idf_objtypename
        "sha256:d53af0e50d77ccdb96d659a6b14101e2d73c81c8b538acde0d4b846471cfdb8a", // 773 Radiator.to_idf_object
        "sha256:1b23d14939a6616f77abc7c5db399534fafc6e0c4922763981fb55363571aefd", // 789 SupplyGroup
        "sha256:bed499da9ee8103c450fe8fb6f8431d9c809f32627ac00ee727b3d22625ff426", // 797 SupplySystem
        "sha256:145aa3279df07b8fa52b3d9b1c1c99c928add67bee4f70ffb0bdeacac653c7fd", // 798 SupplySystem.idf_get_airinletnodename
        "sha256:850cfcc607b30b0069e281a7ee5c37ac5938c38e00dbcbd12088d5f8b4f87a86", // 799 SupplySystem.idf_get_airoutletnodename
        "sha256:b8b5cf3487bd5dba87a0d3a2af555fbec7f96964f67a5aeb266164cfdd84d46f", // 800 SupplySystem.idf_get_demandbranchname
        "sha256:759bf933eb083cc5556cb0acf8c4caaf75d2faa80cc4325ce048ca238e74e7cb", // 801 SupplySystem.idf_get_objname
        "sha256:90da736de056dca9768817bdbbdaadc869e7afe8a330b53f460d1e98c81ccb15", // 802 SupplySystem.idf_objtypename
        "sha256:4f87151118b5f36af0e6a2af1a861caef2168797032a337ccfcea5b98ed06adf", // 803 SupplySystem.to_idf_object
    };

    [Fact]
    public void MatchesPinnedSupplyCoreThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument fixture = ReadPinnedFixture();
        OracleCorpus corpus = ValidateFixture(fixture.RootElement);

        NativeObservation[] observations = Cases.Select(ObserveNativeCase).ToArray();
        Assert.Equal(Cases.Select(item => item.Code), observations.Select(item => item.Code));
        object[] receipts = corpus.Targets.Select(target => CreateReceipt(target, observations)).ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();

        if (DiscoverPins)
        {
            string casePins = string.Join(
                Environment.NewLine,
                observations.Select(item =>
                    $"        new(\"{item.Code}\", {item.Facts.Length}, \"{item.FactsSha256}\"),"));
            string receiptPins = string.Join(
                Environment.NewLine,
                corpus.Targets.Select((target, index) =>
                    $"        \"{receiptHashes[index]}\", // {target.InventoryIndex} {target.Symbol}"));
            throw new Xunit.Sdk.XunitException(
                "SUPPLY_CORE_NATIVE_PINS" + Environment.NewLine +
                "CASES" + Environment.NewLine + casePins + Environment.NewLine +
                "RECEIPTS" + Environment.NewLine + receiptPins);
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].Code, observations[index].Code);
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }

        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
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

        Assert.Equal(49, recordCount);
        Assert.Equal(49, corpus.Targets.Length);
        Assert.Equal(49, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(18, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(31, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.Equal(9, corpus.FixtureCases.Length);
        Assert.Equal(8, corpus.Adjacent.Length);
    }

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin pin in NativeSources.Concat(SupportFixtures).Concat(SupportNativeTests))
        {
            AssertPinnedArtifact(pin.Path, pin.Bytes, pin.Sha256);
        }

        Assert.True(typeof(SupplySystem).IsAbstract);
        Assert.False(typeof(AirHandlingUnit).IsAbstract);
        Assert.True(typeof(PackagedAirConditioner).IsSealed);
        Assert.True(typeof(FanCoilUnit).IsSealed);
        Assert.True(typeof(ElectricRadiator).IsSealed);
        Assert.True(typeof(ElectricRadiantFloor).IsSealed);
        Assert.True(typeof(Radiator).IsSealed);
        Assert.True(typeof(RadiantFloor).IsSealed);
        AssertPublicGetOnlyProperty<SupplySystem>(nameof(SupplySystem.Source), typeof(SourceSystem));
        AssertPublicGetOnlyProperty<SupplySystem>(nameof(SupplySystem.CanHeat), typeof(bool));
        AssertPublicGetOnlyProperty<SupplySystem>(nameof(SupplySystem.CanCool), typeof(bool));
        MethodInfo objectNameFor = Assert.IsAssignableFrom<MethodInfo>(typeof(SupplySystem).GetMethod(
            nameof(SupplySystem.ObjectNameFor),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(Zone) },
            modifiers: null));
        Assert.Equal(typeof(string), objectNameFor.ReturnType);
        Assert.NotNull(typeof(EnergyModel).GetMethod(
            nameof(EnergyModel.ToIdfDocument),
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Single(typeof(ZoneHvacAssignment).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Single(typeof(SupplyGroup).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Contains(typeof(IReadOnlyList<IdfObject>), typeof(IdfDocument).GetInterfaces());
    }

    private static void AssertPublicGetOnlyProperty<T>(string name, Type expectedType)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(
            typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(expectedType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.Null(property.SetMethod);
    }

    private static JsonDocument ReadPinnedFixture()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(FixturePath));
        Assert.Equal(FixtureBytes, bytes.Length);
        Assert.Equal(FixtureSha256, Sha256(bytes));
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
    }

    private static OracleCorpus ValidateFixture(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoHostPaths(root);
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));

        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        for (int index = 0; index < Cases.Length; index++)
        {
            CaseBinding expected = Cases[index];
            JsonElement actual = fixtureCases[index];
            Assert.Equal(expected.Code, RequiredString(actual, "code"));
            Assert.Equal(expected.CaseId, RequiredString(actual, "id"));
            Assert.Equal(expected.Subfamily, RequiredString(actual, "subfamily"));
            Assert.Equal(expected.CaseSha256, RequiredString(actual, "case_sha256"));
            Assert.Equal(expected.CaseSha256, CanonicalSha256WithoutProperty(actual, "case_sha256"));
            Assert.Equal(expected.PythonFactsSha256, CanonicalSha256(actual.GetProperty("python").GetProperty("facts")));
            Assert.Equal(expected.TargetCount, actual.GetProperty("target_symbols").GetArrayLength());
        }

        JsonElement caseSha = root.GetProperty("case_sha256");
        JsonElement factSha = root.GetProperty("fact_sha256");
        AssertKeys(caseSha, Cases.Select(item => item.CaseId).ToArray());
        AssertKeys(factSha, Cases.Select(item => item.CaseId).ToArray());
        foreach (CaseBinding item in Cases)
        {
            Assert.Equal(item.CaseSha256, RequiredString(caseSha, item.CaseId));
            Assert.Equal(item.PythonFactsSha256, RequiredString(factSha, item.CaseId));
        }

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(TargetReceiptsSha256, RequiredString(upstream, "target_receipts_sha256"));
        JsonElement source = upstream.GetProperty("source");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));
        JsonElement inventory = upstream.GetProperty("inventory");
        Assert.Equal(InventoryPath, RequiredString(inventory, "path"));
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        using (JsonDocument inventoryDocument = JsonDocument.Parse(File.ReadAllBytes(FindRepositoryFile(InventoryPath))))
        {
            Assert.Equal(
                InventoryContentSha256,
                RequiredString(inventoryDocument.RootElement, "content_sha256"));
            Assert.Equal(
                UpstreamCommit,
                RequiredString(inventoryDocument.RootElement, "upstream_commit"));
        }

        JsonElement[] receiptElements = root.GetProperty("target_receipts").EnumerateArray().ToArray();
        JsonElement[] symbolElements = root.GetProperty("symbols").EnumerateArray().ToArray();
        Assert.Equal(49, receiptElements.Length);
        Assert.Equal(49, symbolElements.Length);
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(root.GetProperty("target_receipts")));
        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.False(contract.GetProperty("internal_generate_claimed").GetBoolean());
        JsonElement assertionIds = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement nativeRoutes = contract.GetProperty("native_routes");

        var targets = new TargetBinding[ExpectedTargets.Length];
        for (int index = 0; index < ExpectedTargets.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement receipt = receiptElements[index];
            JsonElement symbol = symbolElements[index];
            Assert.Equal(expected.InventoryIndex, receipt.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, RequiredString(receipt, "symbol"));
            Assert.Equal(expected.Kind, RequiredString(receipt, "kind"));
            Assert.Equal(expected.SymbolHash, RequiredString(receipt, "symbol_hash"));
            Assert.Equal(UpstreamPath, RequiredString(receipt, "path"));
            Assert.Equal(expected.InventoryIndex, symbol.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, RequiredString(symbol, "symbol"));
            Assert.Equal(expected.SymbolHash, RequiredString(symbol, "symbol_hash"));
            Assert.Equal(expected.AssertionId, RequiredString(symbol, "assertion_id"));
            Assert.Equal(expected.Classification, RequiredString(symbol, "classification"));
            Assert.Equal(expected.NativeRoute, RequiredString(symbol, "native_route"));
            Assert.Equal(expected.AssertionId, RequiredString(assertionIds, expected.Symbol));
            Assert.Equal(expected.Classification, RequiredString(classifications, expected.Symbol));
            Assert.Equal(expected.NativeRoute, RequiredString(nativeRoutes, expected.Symbol));
            if (expected.AdaptationId == "not_applicable")
            {
                Assert.False(adaptations.TryGetProperty(expected.Symbol, out _));
            }
            else
            {
                Assert.Equal(expected.AdaptationId, RequiredString(adaptations, expected.Symbol));
                Assert.Equal(expected.AdaptationId, RequiredString(symbol, "adaptation"));
            }

            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                expected.Kind,
                expected.SymbolHash,
                RequiredString(receipt, "signature_hash"),
                RequiredString(receipt, "body_hash"),
                expected.AssertionId,
                expected.Classification,
                expected.AdaptationId,
                expected.NativeRoute,
                expected.CaseId);
        }

        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("full_family_closure").GetBoolean());
        Assert.Equal(49, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(9, closure.GetProperty("family_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("target_indices")));
        Assert.Equal(ExpectedTargets.Select(item => item.Symbol), ReadStringArray(closure.GetProperty("target_symbols")));
        Assert.Equal(ExpectedAdjacent.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("adjacent_indices")));
        Assert.Equal(ExpectedAdjacent.Select(item => item.Symbol), ReadStringArray(closure.GetProperty("adjacent_symbols")));
        JsonElement adjacentStatuses = closure.GetProperty("adjacent_existing_status");
        foreach (AdjacentBinding adjacent in ExpectedAdjacent)
        {
            Assert.Equal(adjacent.Status, RequiredString(adjacentStatuses, adjacent.Symbol));
        }
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(contract, "unpromoted_adjacent_receipt_sha256"));
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(upstream, "adjacent_receipts_sha256"));
        Assert.Equal(AdjacentReceiptsSha256, CanonicalSha256(upstream.GetProperty("adjacent_receipts")));

        JsonElement nativeReview = root.GetProperty("native_review");
        JsonElement counts = nativeReview.GetProperty("counts");
        Assert.Equal(18, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(31, counts.GetProperty("exception").GetInt32());
        Assert.Equal(49, counts.GetProperty("total").GetInt32());
        Assert.Equal(
            "Only SupplySystem.Source/CanHeat/CanCool/ObjectNameFor, concrete public supply types, SupplyGroup, ZoneHvacAssignment, and EnergyModel.ToIdfDocument are claimed. Internal Generate members are intentionally not evidence routes.",
            RequiredString(nativeReview, "public_route_boundary"));
        ValidateNativeReviewSources(nativeReview.GetProperty("native_sources"));
        ValidateSupportFixtures(root.GetProperty("support_fixtures"));
        return new OracleCorpus(fixtureCases, targets, ExpectedAdjacent);
    }

    private static void ValidateNativeReviewSources(JsonElement value)
    {
        JsonElement[] items = value.EnumerateArray().ToArray();
        Assert.Equal(NativeSources.Length, items.Length);
        for (int index = 0; index < items.Length; index++)
        {
            ArtifactPin expected = NativeSources[index];
            Assert.Equal(expected.Path, RequiredString(items[index], "path"));
            Assert.Equal(expected.Bytes, items[index].GetProperty("bytes").GetInt32());
            Assert.Equal(expected.Sha256, RequiredString(items[index], "sha256"));
        }
    }

    private static void ValidateSupportFixtures(JsonElement value)
    {
        JsonElement[] items = value.EnumerateArray().ToArray();
        Assert.Equal(SupportFixtures.Length, items.Length);
        for (int index = 0; index < items.Length; index++)
        {
            ArtifactPin expected = SupportFixtures[index];
            Assert.Equal(expected.Path, RequiredString(items[index], "path"));
            Assert.Equal(expected.Bytes, items[index].GetProperty("bytes").GetInt32());
            Assert.Equal(expected.Sha256, RequiredString(items[index], "sha256"));
        }
    }

    private static NativeObservation ObserveNativeCase(CaseBinding item)
    {
        string[] facts = item.Code switch
        {
            "A01" => ObserveAirHandlingUnit(),
            "EF01" => ObserveElectricRadiantFloor(),
            "E01" => ObserveElectricRadiator(),
            "F01" => ObserveFanCoilUnit(),
            "P01" => ObservePackagedAirConditioner(),
            "RF01" => ObserveRadiantFloor(),
            "R01" => ObserveRadiator(),
            "G01" => ObserveSupplyGroup(),
            "S01" => ObserveSupplySystem(),
            _ => throw new InvalidOperationException("Unknown supply-core case: " + item.Code),
        };
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        return new NativeObservation(item.Code, item.CaseId, facts, CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveAirHandlingUnit()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-AHU-ZONE", "Supply AHU Zone");
        var source = new HeatPump(Id("SUPPLY-AHU-SOURCE"), "Supply AHU Source", Fuel.Electricity, 3.2, 3.0);
        var system = new AirHandlingUnit(Id("SUPPLY-AHU"), "Main AHU", source, 0.71, 123.5, 0.91);
        IdfDocument document = ModelWith(zone, new SupplyGroup(new SupplySystem[] { system })).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"]);
        Assert.Same(source, system.Source);
        Assert.True(system.CanHeat);
        Assert.True(system.CanCool);
        Assert.Equal(system.ObjectNameFor(zone), equipment.Name);
        return SystemFacts(system, zone, document, equipment, new[]
        {
            "fan-total-efficiency=" + system.FanTotalEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "fan-pressure-rise-pascals=" + system.FanPressureRisePascals.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "motor-efficiency=" + system.MotorEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    private static string[] ObserveElectricRadiantFloor()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-ERF-ZONE", "Supply Electric Floor Zone");
        var system = new ElectricRadiantFloor(Id("SUPPLY-ERF"), "Electric Floor", 2.75);
        IdfDocument document = ModelWith(zone, new SupplyGroup(new SupplySystem[] { system })).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:LowTemperatureRadiant:Electric"]);
        Assert.Null(system.Source);
        Assert.True(system.CanHeat);
        Assert.False(system.CanCool);
        return SystemFacts(system, zone, document, equipment, new[]
        {
            "throttling-range-celsius=" + system.ThrottlingRangeCelsius.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "surface-group=" + equipment[3],
            "setpoint-control=" + equipment[9],
        });
    }

    private static string[] ObserveElectricRadiator()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-ER-ZONE", "Supply Electric Radiator Zone");
        var system = new ElectricRadiator(Id("SUPPLY-ER"), "Electric Radiator", 4321, 0.93, 0.22);
        IdfDocument document = ModelWith(zone, new SupplyGroup(new SupplySystem[] { system })).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:Baseboard:RadiantConvective:Electric"]);
        Assert.Null(system.Source);
        Assert.True(system.CanHeat);
        Assert.False(system.CanCool);
        return SystemFacts(system, zone, document, equipment, new[]
        {
            "heating-capacity-watts=" + system.HeatingCapacityWatts!.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "efficiency=" + system.Efficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "radiant-fraction=" + system.RadiantFraction.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    private static string[] ObserveFanCoilUnit()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-FCU-ZONE", "Supply Fan Coil Zone");
        var source = new Boiler(Id("SUPPLY-FCU-SOURCE"), "Supply Fan Coil Boiler", Fuel.NaturalGas);
        var system = new FanCoilUnit(Id("SUPPLY-FCU"), "Fan Coil", source, 0.72, 135, 0.92);
        IdfDocument document = ModelWith(zone, new SupplyGroup(new SupplySystem[] { system })).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:FourPipeFanCoil"]);
        Assert.Same(source, system.Source);
        Assert.True(system.CanHeat);
        Assert.False(system.CanCool);
        return SystemFacts(system, zone, document, equipment, new[]
        {
            "fan-total-efficiency=" + system.FanTotalEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "fan-pressure-rise-pascals=" + system.FanPressureRisePascals.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "motor-efficiency=" + system.MotorEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    private static string[] ObservePackagedAirConditioner()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-PAC-ZONE", "Supply Packaged Zone");
        var source = new HeatPump(Id("SUPPLY-PAC-SOURCE"), "Supply Packaged Source", Fuel.Electricity, 3.1, 2.9);
        var system = new PackagedAirConditioner(Id("SUPPLY-PAC"), "Packaged Unit", source);
        IdfDocument document = ModelWith(zone, new SupplyGroup(new SupplySystem[] { system })).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"]);
        Assert.Same(source, system.Source);
        Assert.False(system.CanHeat);
        Assert.True(system.CanCool);
        return SystemFacts(system, zone, document, equipment, new[]
        {
            "concrete-type-sealed=" + system.GetType().IsSealed,
            "base-type=" + system.GetType().BaseType!.FullName,
            "heating-coil-availability=" + Assert.Single(document["Coil:Heating:DX:VariableRefrigerantFlow"])[1],
        });
    }

    private static string[] ObserveRadiantFloor()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-RF-ZONE", "Supply Radiant Floor Zone");
        var source = new Boiler(Id("SUPPLY-RF-SOURCE"), "Supply Radiant Boiler", Fuel.NaturalGas);
        var system = new RadiantFloor(Id("SUPPLY-RF"), "Radiant Floor", source, 3.25);
        IdfDocument document = ModelWith(zone, new SupplyGroup(new SupplySystem[] { system })).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:LowTemperatureRadiant:VariableFlow"]);
        Assert.Same(source, system.Source);
        Assert.True(system.CanHeat);
        Assert.False(system.CanCool);
        return SystemFacts(system, zone, document, equipment, new[]
        {
            "throttling-range-celsius=" + system.ThrottlingRangeCelsius.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "water-inlet=" + equipment[6],
            "water-outlet=" + equipment[7],
        });
    }

    private static string[] ObserveRadiator()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-R-ZONE", "Supply Radiator Zone");
        var source = new Boiler(Id("SUPPLY-R-SOURCE"), "Supply Radiator Boiler", Fuel.NaturalGas);
        var system = new Radiator(Id("SUPPLY-R"), "Hydronic Radiator", source, 5432, 0.18);
        IdfDocument document = ModelWith(zone, new SupplyGroup(new SupplySystem[] { system })).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:Baseboard:RadiantConvective:Water"]);
        Assert.Same(source, system.Source);
        Assert.True(system.CanHeat);
        Assert.False(system.CanCool);
        return SystemFacts(system, zone, document, equipment, new[]
        {
            "heating-capacity-watts=" + system.HeatingCapacityWatts!.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "radiant-fraction=" + system.RadiantFraction.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "water-nodes=" + equipment[3] + "|" + equipment[4],
        });
    }

    private static string[] ObserveSupplyGroup()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-GROUP-ZONE", "Supply Group Zone");
        var heatPump = new HeatPump(Id("SUPPLY-GROUP-SOURCE"), "Supply Group Source", Fuel.Electricity, 3.0, 2.8);
        var cooling = new PackagedAirConditioner(Id("SUPPLY-GROUP-PAC"), "Group Packaged", heatPump);
        var heating = new ElectricRadiator(Id("SUPPLY-GROUP-ER"), "Group Radiator", 4000);
        Schedule availability = Schedule.Constant("Supply Group Custom Availability", 1, ScheduleType.OnOff);
        var group = new SupplyGroup(
            new SupplySystem[] { cooling, heating },
            new Schedule?[] { availability, null });
        var assignment = new ZoneHvacAssignment(zone.Id, group);
        var model = new EnergyModel("Supply group aggregate", new[] { zone }, new[] { assignment });
        IdfDocument document = model.ToIdfDocument();
        Assert.Same(group, assignment.Supply);
        Assert.Equal(zone.Id, assignment.ZoneId);
        Assert.Equal(new SupplySystem[] { cooling, heating }, group.Systems);
        Assert.Equal(new SourceSystem[] { heatPump }, group.Sources);
        Assert.True(group.CanHeat);
        Assert.True(group.CanCool);
        return new[]
        {
            "systems=" + string.Join("|", group.Systems.Select(item => item.Id.Value)),
            "availabilities=" + string.Join("|", group.Availabilities.Select(item => item?.Name ?? "<default>")),
            "heating-systems=" + string.Join("|", group.HeatingSystems.Select(item => item.Id.Value)),
            "cooling-systems=" + string.Join("|", group.CoolingSystems.Select(item => item.Id.Value)),
            "sources=" + string.Join("|", group.Sources.Select(item => item.Id.Value)),
            "zone-assignment=" + assignment.ZoneId.Value + "|" + ReferenceEquals(group, assignment.Supply),
            "public-idf-object-count=" + document.Count,
            "public-idf-type-counts=" + DocumentTypeCounts(document),
            "public-idf-sha256=" + Sha256(Encoding.UTF8.GetBytes(IdfWriter.Write(document))),
        };
    }

    private static string[] ObserveSupplySystem()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("SUPPLY-BASE-ZONE", "Supply Base Zone");
        SupplySystem system = new ElectricRadiator(Id("SUPPLY-BASE"), "Base Route", 3210);
        var group = new SupplyGroup(new[] { system });
        var assignment = new ZoneHvacAssignment(zone.Id, group);
        IdfDocument document = new EnergyModel("Supply base route", new[] { zone }, new[] { assignment }).ToIdfDocument();
        IdfObject equipment = Assert.Single(document["ZoneHVAC:Baseboard:RadiantConvective:Electric"]);
        Assert.Equal("ElectricRadiator_named_Base Route_for_Supply Base Zone", system.ObjectNameFor(zone));
        return new[]
        {
            "supply-system-abstract=" + typeof(SupplySystem).IsAbstract,
            "source=" + (system.Source?.Id.Value ?? "<none>"),
            "can-heat=" + system.CanHeat,
            "can-cool=" + system.CanCool,
            "object-name-for-zone=" + system.ObjectNameFor(zone),
            "public-idf-equipment=" + equipment.ObjectType + "|" + equipment.Name,
            "public-idf-object-count=" + document.Count,
            "public-idf-type-counts=" + DocumentTypeCounts(document),
            "public-idf-sha256=" + Sha256(Encoding.UTF8.GetBytes(IdfWriter.Write(document))),
        };
    }

    private static string[] SystemFacts(
        SupplySystem system,
        Zone zone,
        IdfDocument document,
        IdfObject equipment,
        IEnumerable<string> additional)
    {
        string[] common =
        {
            "runtime-type=" + system.GetType().FullName,
            "id=" + system.Id.Value,
            "name=" + system.Name,
            "source=" + (system.Source?.Id.Value ?? "<none>"),
            "can-heat=" + system.CanHeat,
            "can-cool=" + system.CanCool,
            "object-name-for-zone=" + system.ObjectNameFor(zone),
            "public-idf-equipment=" + equipment.ObjectType + "|" + equipment.Name,
            "public-idf-object-count=" + document.Count,
            "public-idf-type-counts=" + DocumentTypeCounts(document),
            "public-idf-sha256=" + Sha256(Encoding.UTF8.GetBytes(IdfWriter.Write(document))),
        };
        return common.Concat(additional).ToArray();
    }

    private static string DocumentTypeCounts(IdfDocument document) => string.Join(
        ";",
        document.GroupBy(item => item.ObjectType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + "=" + group.Count()));

    private static EnergyModel ModelWith(Zone zone, SupplyGroup group) => new(
        "Supply core model for " + zone.Name,
        new[] { zone },
        new[] { new ZoneHvacAssignment(zone.Id, group) });

    private static object CreateReceipt(TargetBinding target, IReadOnlyList<NativeObservation> observations)
    {
        NativeObservation observation = Assert.Single(observations, item => item.CaseId == target.CaseId);
        CaseBinding fixtureCase = Assert.Single(Cases, item => item.CaseId == target.CaseId);
        return new
        {
            adaptation_id = target.AdaptationId,
            artifacts = new
            {
                fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
                generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
                native_sources = NativeSources.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                support_fixtures = SupportFixtures.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
                support_native_tests = SupportNativeTests.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
            },
            assertion_id = target.AssertionId,
            classification = target.Classification,
            native_route = target.NativeRoute,
            observations = new[]
            {
                new
                {
                    case_code = observation.Code,
                    case_id = observation.CaseId,
                    native_fact_count = observation.Facts.Length,
                    native_facts = observation.Facts,
                    native_facts_sha256 = observation.FactsSha256,
                    native_outcome = target.Classification == "equivalent" ? "equivalent-as-pinned" : "adapted-as-pinned",
                    python_case_sha256 = fixtureCase.CaseSha256,
                    python_facts_sha256 = fixtureCase.PythonFactsSha256,
                },
            },
            scope = new
            {
                adjacent_count_not_recorded = 8,
                adjacent_receipts_sha256 = AdjacentReceiptsSha256,
                claim_policy = "only-pinned-production-public-routes-and-public-idf-document-output",
                equivalent_target_count = 18,
                exact_case_count = 9,
                exact_target_count = 49,
                exception_target_count = 31,
                fixture_repository_commit = FixtureRepositoryCommit,
                internal_generate_claimed = false,
                structural_only = false,
            },
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
            target_symbol = target.Symbol,
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
        AssertNoHostPaths(receipt);
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal(target.Classification, RequiredString(receipt, "classification"));
        Assert.Equal(target.NativeRoute, RequiredString(receipt, "native_route"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));
        JsonElement source = receipt.GetProperty("source_receipt");
        Assert.Equal(target.InventoryIndex, source.GetProperty("inventory_index").GetInt32());
        Assert.Equal(target.Symbol, RequiredString(source, "symbol"));
        Assert.Equal(target.SymbolHash, RequiredString(source, "symbol_hash"));
        NativeObservation expected = Assert.Single(observations, item => item.CaseId == target.CaseId);
        JsonElement observed = Assert.Single(receipt.GetProperty("observations").EnumerateArray());
        Assert.Equal(expected.Code, RequiredString(observed, "case_code"));
        Assert.Equal(expected.CaseId, RequiredString(observed, "case_id"));
        Assert.Equal(expected.Facts.Length, observed.GetProperty("native_fact_count").GetInt32());
        Assert.Equal(expected.FactsSha256, RequiredString(observed, "native_facts_sha256"));
        Assert.Equal(expected.Facts, ReadStringArray(observed.GetProperty("native_facts")));
        JsonElement scope = receipt.GetProperty("scope");
        Assert.False(scope.GetProperty("internal_generate_claimed").GetBoolean());
        Assert.False(scope.GetProperty("structural_only").GetBoolean());
        Assert.Equal(49, scope.GetProperty("exact_target_count").GetInt32());
    }

    private static object Artifact(string path, int bytes, string sha256) => new { bytes, path, sha256 };

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
            string candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file '" + relativePath + "'.");
    }

    private static EntityId Id(string value) => new(value);

    private static string Sha256(byte[] value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string CanonicalSha256(JsonElement value) =>
        Sha256(Encoding.UTF8.GetBytes(CanonicalJson(value)));

    private static string CanonicalSha256WithoutProperty(JsonElement value, string omittedProperty)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in value.EnumerateObject()
                         .Where(item => item.Name != omittedProperty)
                         .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }

            writer.WriteEndObject();
        }

        return Sha256(stream.ToArray());
    }

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
                throw new InvalidOperationException("Unsupported JSON kind: " + value.ValueKind);
        }
    }

    private static string RequiredString(JsonElement item, string propertyName)
    {
        JsonElement value = item.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return Assert.IsType<string>(value.GetString());
    }

    private static string[] ReadStringArray(JsonElement value) =>
        value.EnumerateArray().Select(item => Assert.IsType<string>(item.GetString())).ToArray();

    private static int[] ReadIntArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.True(keys.Add(property.Name), "Duplicate JSON key '" + property.Name + "'.");
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

    private static void AssertNoHostPaths(JsonElement value)
    {
        IEnumerable<string> strings = value.ValueKind switch
        {
            JsonValueKind.String => new[] { value.GetString() ?? string.Empty },
            JsonValueKind.Object => value.EnumerateObject().SelectMany(item => EnumerateStrings(item.Value)),
            JsonValueKind.Array => value.EnumerateArray().SelectMany(EnumerateStrings),
            _ => Array.Empty<string>(),
        };
        foreach (string text in strings)
        {
            Assert.DoesNotContain("C:\\", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:/", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
            Assert.DoesNotContain("AppData", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            yield return value.GetString() ?? string.Empty;
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                foreach (string text in EnumerateStrings(property.Value))
                {
                    yield return text;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                foreach (string text in EnumerateStrings(item))
                {
                    yield return text;
                }
            }
        }
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string CaseSha256,
        string PythonFactsSha256,
        int TargetCount);

    private sealed record ExpectedTarget(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string AssertionId,
        string Classification,
        string AdaptationId,
        string NativeRoute,
        string CaseId);

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
        string CaseId);

    private sealed record AdjacentBinding(int InventoryIndex, string Symbol, string Status);

    private sealed record NativeObservation(string Code, string CaseId, string[] Facts, string FactsSha256);

    private sealed record NativePin(string Code, int FactCount, string FactsSha256);

    private sealed record OracleCorpus(
        JsonElement[] FixtureCases,
        TargetBinding[] Targets,
        AdjacentBinding[] Adjacent);
}
