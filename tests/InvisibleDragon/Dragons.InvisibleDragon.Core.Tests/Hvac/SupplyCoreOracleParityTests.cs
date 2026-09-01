#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

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
using Dragons.InvisibleDragon.Tests.Model;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class SupplyCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-hvac-supply-core-oracle.json";
    private const int FixtureBytes = 215_230;
    private const string FixtureSha256 =
        "sha256:657b53b768c90a2915ca10c781ff63ab5a21323bb09f534d4d5da3178fe99194";
    private const string FixtureSchema =
        "dragons.python-reference.dragon-hvac-supply-core.v1";
    private const string FixtureRepositoryCommit = "07bcb7e";
    private const string CasesSha256 =
        "sha256:29eacb2d29f528353302d1afd8e3ef646d7d35886237bb4a3fa494039a4ec36f";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_hvac_supply_core_oracle.py";
    private const int GeneratorBytes = 65_859;
    private const string GeneratorSha256 =
        "sha256:7ce1af80729c2f2aa333016ba95db3963b25db24e1b23d2c89f49ea2694590e2";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_hvac_supply_core_oracle.py";
    private const int ValidatorBytes = 17_313;
    private const string ValidatorSha256 =
        "sha256:863eb92bbec8fe415e3c917ddf690e106beea5611bf39dbc1850a896c8d23622";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";

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
        "Dragons.InvisibleDragon.Tests.Hvac.SupplyCoreOracleParityTests.MatchesPinnedSupplyCoreThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_561,
            "sha256:fcbe9c38cacade8002d121b0834a4441560086052571dd654f3c185a0c897249"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SupplySystems.cs", 18_249,
            "sha256:bf93e1c6889f7d371fff983caad1b3c90d4cbc6113bbb5d9a7a783740af1bb46"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HydronicSupplySystems.cs", 24_504,
            "sha256:23a9ffa8e776464c77570ab60854a4fb812de22f84a6ba1e4bf242a45f563269"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs", 21_985,
            "sha256:60357af37bea1f6e7dd0640254a30761ed4097d53751183e5902c2efa62a0f28"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_723,
            "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4"),
    };

    private static readonly ArtifactPin[] SupportFixtures =
    {
        new("fixtures/reference/python-0.7.0/dragon-hvac-supply-group-core-oracle.json", 31_160,
            "sha256:32f05de2a2ead16e0097d3402577e8bce03f40ea151162a6312000bb4f5a5886"),
        new("fixtures/reference/python-0.7.0/dragon-hvac-supply-group-to-idf-object-oracle.json", 22_605,
            "sha256:e5e47e5ffa2d725697d8741d05f54655705106e4bb75348c6d9eff46e04715bc"),
        new("fixtures/reference/python-0.7.0/dragon-model-add-supply-system-oracle.json", 15_119,
            "sha256:42ad2d75ce91edd153bd9e07382a03b5095ea0300df227f87e0d0147b377230f"),
    };

    private static readonly ArtifactPin[] SupportNativeTests =
    {
        new("tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Hvac/SupplyGroupCoreOracleParityTests.cs", 84_786,
            "sha256:c0887c43694e30250b76dcce672ad27104d8a55e7e52ffe29744e8b5170290ff"),
        new("tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Hvac/SupplyGroupToIdfObjectOracleParityTests.cs", 72_366,
            "sha256:1008884f2a370e2a73080d0110ca58a3a0cf60b2ddebc364e41ec3ace1ca94c9"),
        new("tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Model/EnergyModelAddSupplySystemOracleParityTests.cs", 50_773,
            "sha256:539db26d973139a83cb704fc52347d8ebb11f60802bcebcb0d72b80ccacf511b"),
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
        new(645, "AirHandlingUnit", "class", "sha256:5a79613c19fc0d823fbc000d2ed658cdda337413d8bb800db163941f3f4581b3", "dragon-hvac-supply-core-645-airhandlingunit", "exception", "reviewed-public-aggregate-supply-emission-0fa0b1c0", "Dragons.InvisibleDragon.Hvac.AirHandlingUnit public constructor and immutable properties", Cases[0].CaseId),
        new(647, "AirHandlingUnit.__init__", "function", "sha256:613f0db31794614f9caf37919cb05b7f89be5db2481d962f37b4066e30b4eee2", "dragon-hvac-supply-core-647-airhandlingunit-__init__", "exception", "reviewed-public-aggregate-supply-emission-56ec0ea0", "Dragons.InvisibleDragon.Hvac.AirHandlingUnit public constructor and immutable properties", Cases[0].CaseId),
        new(648, "AirHandlingUnit.coolable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-648-airhandlingunit-coolable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.AirHandlingUnit.CanCool", Cases[0].CaseId),
        new(649, "AirHandlingUnit.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-649-airhandlingunit-heatable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.AirHandlingUnit.CanHeat", Cases[0].CaseId),
        new(650, "AirHandlingUnit.idf_objtypename", "function", "sha256:0845568f05ccaf9cc73c6fa6f8dc56166527245cf42b47e10feaac9d5705ad88", "dragon-hvac-supply-core-650-airhandlingunit-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-fac0b3fb", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[0].CaseId),
        new(651, "AirHandlingUnit.to_idf_object", "function", "sha256:77bf437580b1e295246d810d77ec62300113f45a411672f5832332b843fc8a1e", "dragon-hvac-supply-core-651-airhandlingunit-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-423008b9", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[0].CaseId),
        new(700, "ElectricRadiantFloor", "class", "sha256:16e58394a533fffaa611babbcd04ca4099b58e5bfb650536ced8dd558726a1fe", "dragon-hvac-supply-core-700-electricradiantfloor", "exception", "reviewed-public-aggregate-supply-emission-96fb711a", "Dragons.InvisibleDragon.Hvac.ElectricRadiantFloor public constructor and immutable properties", Cases[1].CaseId),
        new(701, "ElectricRadiantFloor.__init__", "function", "sha256:da1996945b3335573ed6a4a903997b5e97950f59f0e19da2cb57abc57dac04f5", "dragon-hvac-supply-core-701-electricradiantfloor-__init__", "exception", "reviewed-public-aggregate-supply-emission-40523ffc", "Dragons.InvisibleDragon.Hvac.ElectricRadiantFloor public constructor and immutable properties", Cases[1].CaseId),
        new(702, "ElectricRadiantFloor.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-702-electricradiantfloor-coolable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ElectricRadiantFloor.CanCool", Cases[1].CaseId),
        new(703, "ElectricRadiantFloor.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-703-electricradiantfloor-heatable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ElectricRadiantFloor.CanHeat", Cases[1].CaseId),
        new(704, "ElectricRadiantFloor.idf_objtypename", "function", "sha256:068b2c4586afd184de3e090cb9db089a186ac1e5b05d65e70a7547383594f4c5", "dragon-hvac-supply-core-704-electricradiantfloor-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-a210f9fb", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[1].CaseId),
        new(705, "ElectricRadiantFloor.source", "function", "sha256:0b3b6343593c8c54b99ac82389147b686fbf461011b3dac7e73c97fd512bbc78", "dragon-hvac-supply-core-705-electricradiantfloor-source", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ElectricRadiantFloor.Source", Cases[1].CaseId),
        new(706, "ElectricRadiantFloor.to_idf_object", "function", "sha256:5076d17659796d0620a3f3b9de5b28fe7e9ff9772dc58edc44379767cd8a6bbb", "dragon-hvac-supply-core-706-electricradiantfloor-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-3b86b9c5", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[1].CaseId),
        new(707, "ElectricRadiator", "class", "sha256:6e4ce6d4489fd995f5cf5ebfd4ca8a96db68c7b5d0bb271fbf37a9ea01dbdf33", "dragon-hvac-supply-core-707-electricradiator", "exception", "reviewed-public-aggregate-supply-emission-1951b454", "Dragons.InvisibleDragon.Hvac.ElectricRadiator public constructor and immutable properties", Cases[2].CaseId),
        new(708, "ElectricRadiator.__init__", "function", "sha256:07f43ff08d4fb608d661c8399fbf11db4f1d8c7c504e81a6e8b8e9e223772ba5", "dragon-hvac-supply-core-708-electricradiator-__init__", "exception", "reviewed-public-aggregate-supply-emission-09edd678", "Dragons.InvisibleDragon.Hvac.ElectricRadiator public constructor and immutable properties", Cases[2].CaseId),
        new(709, "ElectricRadiator.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-709-electricradiator-coolable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ElectricRadiator.CanCool", Cases[2].CaseId),
        new(710, "ElectricRadiator.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-710-electricradiator-heatable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ElectricRadiator.CanHeat", Cases[2].CaseId),
        new(711, "ElectricRadiator.idf_objtypename", "function", "sha256:ad7100cedd4c1714558ea86974be894db62f45a56e5f567ca30542f73775ab69", "dragon-hvac-supply-core-711-electricradiator-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-50af1192", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[2].CaseId),
        new(712, "ElectricRadiator.source", "function", "sha256:0b3b6343593c8c54b99ac82389147b686fbf461011b3dac7e73c97fd512bbc78", "dragon-hvac-supply-core-712-electricradiator-source", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ElectricRadiator.Source", Cases[2].CaseId),
        new(713, "ElectricRadiator.to_idf_object", "function", "sha256:e15931923550afa35476f5cdcf01c7bbfa434329b26fed648611f1b673303be5", "dragon-hvac-supply-core-713-electricradiator-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-c25777d6", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[2].CaseId),
        new(720, "FanCoilUnit", "class", "sha256:0e1ac21dcd08a80f9680490d923aee8606a25058b69396099b1379c38440e6c8", "dragon-hvac-supply-core-720-fancoilunit", "exception", "reviewed-public-aggregate-supply-emission-d16c27dc", "Dragons.InvisibleDragon.Hvac.FanCoilUnit public constructor and immutable properties", Cases[3].CaseId),
        new(721, "FanCoilUnit.__init__", "function", "sha256:613f0db31794614f9caf37919cb05b7f89be5db2481d962f37b4066e30b4eee2", "dragon-hvac-supply-core-721-fancoilunit-__init__", "exception", "reviewed-public-aggregate-supply-emission-5a46d94c", "Dragons.InvisibleDragon.Hvac.FanCoilUnit public constructor and immutable properties", Cases[3].CaseId),
        new(722, "FanCoilUnit.coolable", "function", "sha256:0ba94395670303b3e6651ede4428d86e8f57f73e6e3639078776b07cdac6502b", "dragon-hvac-supply-core-722-fancoilunit-coolable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.FanCoilUnit.CanCool", Cases[3].CaseId),
        new(723, "FanCoilUnit.heatable", "function", "sha256:8f083ca3f324853c52c4ef7f86bc3bc02f7b49e1683a051b7db482d4bc0e0a37", "dragon-hvac-supply-core-723-fancoilunit-heatable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.FanCoilUnit.CanHeat", Cases[3].CaseId),
        new(724, "FanCoilUnit.idf_objtypename", "function", "sha256:efb4de9d2381a1d947e222042ef668067bf17a29a519c6181b9e3185378cc65b", "dragon-hvac-supply-core-724-fancoilunit-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-3db9b91c", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[3].CaseId),
        new(725, "FanCoilUnit.to_idf_object", "function", "sha256:c46a171d406e558b24be0b4eb4902878a5bdd00fbe3b1232e252d1532519c411", "dragon-hvac-supply-core-725-fancoilunit-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-de1053a9", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[3].CaseId),
        new(750, "PackagedAirConditioner", "class", "sha256:9be40f1849ec90cb957163861b180166495fb07bf3552e11254d2ae0f394c505", "dragon-hvac-supply-core-750-packagedairconditioner", "exception", "reviewed-public-aggregate-supply-emission-aeefa6ef", "Dragons.InvisibleDragon.Hvac.PackagedAirConditioner public constructor and immutable properties", Cases[4].CaseId),
        new(751, "PackagedAirConditioner.coolable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-751-packagedairconditioner-coolable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.PackagedAirConditioner.CanCool", Cases[4].CaseId),
        new(752, "PackagedAirConditioner.heatable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-752-packagedairconditioner-heatable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.PackagedAirConditioner.CanHeat", Cases[4].CaseId),
        new(762, "RadiantFloor", "class", "sha256:e5d2eab0925e5f6a43c5c926c23f441024ac7644de91815cf665a4ce8cb34aa0", "dragon-hvac-supply-core-762-radiantfloor", "exception", "reviewed-public-aggregate-supply-emission-5d93e75b", "Dragons.InvisibleDragon.Hvac.RadiantFloor public constructor and immutable properties", Cases[5].CaseId),
        new(763, "RadiantFloor.__init__", "function", "sha256:dac554316222a52df56835df5bc12e1a13c2cd5778a72f4b831fbf7bd2e5226f", "dragon-hvac-supply-core-763-radiantfloor-__init__", "exception", "reviewed-public-aggregate-supply-emission-fced6522", "Dragons.InvisibleDragon.Hvac.RadiantFloor public constructor and immutable properties", Cases[5].CaseId),
        new(764, "RadiantFloor.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-764-radiantfloor-coolable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.RadiantFloor.CanCool", Cases[5].CaseId),
        new(765, "RadiantFloor.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-765-radiantfloor-heatable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.RadiantFloor.CanHeat", Cases[5].CaseId),
        new(766, "RadiantFloor.idf_objtypename", "function", "sha256:18cc149ba20da831a7a4514281ef9a1bfd72ba3e60a65038977de69552645fc6", "dragon-hvac-supply-core-766-radiantfloor-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-b04bffae", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[5].CaseId),
        new(767, "RadiantFloor.to_idf_object", "function", "sha256:bca28a99ac0a6614af9ca2274e92c42b835bafc6b033f897d673613966b5eab6", "dragon-hvac-supply-core-767-radiantfloor-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-6615b65b", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[5].CaseId),
        new(768, "Radiator", "class", "sha256:abc2c799006b9f22100106607e3b93bc2096281112eef9b24aff294409f76685", "dragon-hvac-supply-core-768-radiator", "exception", "reviewed-public-aggregate-supply-emission-e15f88dc", "Dragons.InvisibleDragon.Hvac.Radiator public constructor and immutable properties", Cases[6].CaseId),
        new(769, "Radiator.__init__", "function", "sha256:5a989318588944c57958250dd329ce17ac2a12115e6576344222055e7a9b6c93", "dragon-hvac-supply-core-769-radiator-__init__", "exception", "reviewed-public-aggregate-supply-emission-b4e3bc86", "Dragons.InvisibleDragon.Hvac.Radiator public constructor and immutable properties", Cases[6].CaseId),
        new(770, "Radiator.coolable", "function", "sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795", "dragon-hvac-supply-core-770-radiator-coolable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Radiator.CanCool", Cases[6].CaseId),
        new(771, "Radiator.heatable", "function", "sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "dragon-hvac-supply-core-771-radiator-heatable", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Radiator.CanHeat", Cases[6].CaseId),
        new(772, "Radiator.idf_objtypename", "function", "sha256:d6ab47e791de6994da51079773e814084c8bd9fe802b69ec926efa9034c29eb6", "dragon-hvac-supply-core-772-radiator-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-3fa24b28", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[6].CaseId),
        new(773, "Radiator.to_idf_object", "function", "sha256:c5eb7d0b9e37f45896426e2ea62b8d1cd9604cb8dc31ecb33df53625f5e63dfd", "dragon-hvac-supply-core-773-radiator-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-4277755b", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[6].CaseId),
        new(789, "SupplyGroup", "class", "sha256:f22147d1bab44415fda473980799cb75dc4ce6c57693b5d9ec0a5faaf131fe69", "dragon-hvac-supply-core-789-supplygroup", "exception", "reviewed-public-aggregate-supply-emission-cbcc48d4", "Dragons.InvisibleDragon.Hvac.SupplyGroup constructor/properties; ZoneHvacAssignment; EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?)", Cases[7].CaseId),
        new(797, "SupplySystem", "class", "sha256:13ed08986e2e8b8e9b6a3f9b9a1f387ad8075a99a5f79e6df18b2fd0280cfdc1", "dragon-hvac-supply-core-797-supplysystem", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.SupplySystem", Cases[8].CaseId),
        new(798, "SupplySystem.idf_get_airinletnodename", "function", "sha256:6a1a2503acd718c95c230511ed28dfa09ed7edcf75a93c9505776e3c1a99352e", "dragon-hvac-supply-core-798-supplysystem-idf_get_airinletnodename", "exception", "reviewed-public-aggregate-supply-emission-d2523a5e", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(799, "SupplySystem.idf_get_airoutletnodename", "function", "sha256:20339b80aa3e3f04fa2d841d355aa70da912ad0c2b3a2f2ab56be0a265b65c91", "dragon-hvac-supply-core-799-supplysystem-idf_get_airoutletnodename", "exception", "reviewed-public-aggregate-supply-emission-c90c06c1", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(800, "SupplySystem.idf_get_demandbranchname", "function", "sha256:eb09ae795f4bcb7489a4afc32e7f90f065f3ea7ee452893caa484b123271988b", "dragon-hvac-supply-core-800-supplysystem-idf_get_demandbranchname", "exception", "reviewed-public-aggregate-supply-emission-e95a8f49", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(801, "SupplySystem.idf_get_objname", "function", "sha256:f99114cd508825d71dceac2c8cf415e7307215266946dcb779b74cddb9f5532f", "dragon-hvac-supply-core-801-supplysystem-idf_get_objname", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.SupplySystem.ObjectNameFor(Zone)", Cases[8].CaseId),
        new(802, "SupplySystem.idf_objtypename", "function", "sha256:658520082df92fc4c03d549af63dad643ecb1962a52d7ce52cc27db4c5486918", "dragon-hvac-supply-core-802-supplysystem-idf_objtypename", "exception", "reviewed-public-aggregate-supply-emission-cc4e1a21", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
        new(803, "SupplySystem.to_idf_object", "function", "sha256:186909537d0e1c3f8e7e6fdcdac153f3ce50c8816fbef58a88266f97f3e59f87", "dragon-hvac-supply-core-803-supplysystem-to_idf_object", "exception", "reviewed-public-aggregate-supply-emission-1e9721aa", "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) public aggregate emission", Cases[8].CaseId),
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
        new("A01", 14, "sha256:6e048ffd532ed11f6c3eb93295cca1fb4bc2105d6222248f38da5f8b815ebfde"),
        new("EF01", 14, "sha256:f35dc0148e3427a6703460497d13d012bd9c717a145ba02d47c9744a58f3e291"),
        new("E01", 14, "sha256:1ab5e2f378a5578ceb47c70d2b0ec4145af9b1d28b77ed188c070e738a07ce58"),
        new("F01", 14, "sha256:bb24f29693e3f339694d49cbfabb72e65c1c3c6a6d591ef95720a9688b291eda"),
        new("P01", 14, "sha256:c4ab5267132c15f1050984ff3bf53da06601a66fd3c63810e7fcb72a3b1e640c"),
        new("RF01", 14, "sha256:93526ce671c02a1e73349c73c695abf102167538c45a3ffc41b5bf5ef70e3c0d"),
        new("R01", 14, "sha256:24564ad89f9201896e6e8105467771bd976291d901ec24e84e973f9644dedea9"),
        new("G01", 9, "sha256:d60094777e3bbfc2caf2646c60b5b6e6bf153d662c7c289bd2841fffcc5df137"),
        new("S01", 9, "sha256:0a09ddfca8bc68ef81500d557ffd4c7de73801773b2accbeb9f7ead27e7bfe21"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:323f4ae63aa6b4521d913dbdcf1923f79dcfb72f8175a6b37e6f1e34cf098cea", // 645 AirHandlingUnit
        "sha256:e36e7aa2310acae8e03caa139c9915842f5c500176f9eed69b498285b6888d66", // 647 AirHandlingUnit.__init__
        "sha256:31961fd21630146ac7c272a727cc0088c827cf9071e3bf2267ae603f8b905c8f", // 648 AirHandlingUnit.coolable
        "sha256:f0700c70c731d0b3f83ad5cfa5607fc950de822c85514be6d8781d79a87ef279", // 649 AirHandlingUnit.heatable
        "sha256:5a52a6bb9a2470dd861f5c83fac7ff46d47343afaef0bd4766a1e0bc51c78ded", // 650 AirHandlingUnit.idf_objtypename
        "sha256:f8cfe88b529d5072d322b2dd8e3879c2ec2e54345c255d16bf4900fbf0110c68", // 651 AirHandlingUnit.to_idf_object
        "sha256:3abfb9f06e772402084a2928a80e3492f3d31a5825f4e03840a77d9569bbf72b", // 700 ElectricRadiantFloor
        "sha256:2c6a60682c941ba0a9e78138f832abf45539697eaacfa23aa1b930a3831931d7", // 701 ElectricRadiantFloor.__init__
        "sha256:3bf00242d7c716f27f8a7de58dccfe84b729d1369ecb34cb5f5e2cf29230d7b1", // 702 ElectricRadiantFloor.coolable
        "sha256:d7238ab0033a111bd2bf19a36e5ff7801f7689e2624fea812d759f94fbdb1264", // 703 ElectricRadiantFloor.heatable
        "sha256:86c29a10463be1f2a409f91017d203fcfd89c9281155ab00eeb1443536d2171d", // 704 ElectricRadiantFloor.idf_objtypename
        "sha256:1327afd1ecb8cb4a56bdfaba8c2d454f23ff9ba0ae94dc01a8be2b6195d8d926", // 705 ElectricRadiantFloor.source
        "sha256:3b4dc6b52bb80b0561a31d550e8beef57eb6d02f100ab2428e230f92b56c7833", // 706 ElectricRadiantFloor.to_idf_object
        "sha256:6965b3107a26b5e47eef048d6d83fa6d54e7a1219b82f88996ecb2d9f1dd5700", // 707 ElectricRadiator
        "sha256:07da2ef20c07b346afb9ba00102c3f9173ad7440de3e106775fd7e3df5a4ed6d", // 708 ElectricRadiator.__init__
        "sha256:e4544507c4b28add08381be7fd447b4aaf90517117ec4de0b8b6b700b664612b", // 709 ElectricRadiator.coolable
        "sha256:9e2c97d4a4398f7bec04f2fd4de9f4dab356c95ca17098ad56f294ee89973673", // 710 ElectricRadiator.heatable
        "sha256:3b2823a39740210a755a66807151095c65905b701e7822f3dc48547274244c4d", // 711 ElectricRadiator.idf_objtypename
        "sha256:47555060043a1536800d302f22bd552ffc58af0f925573658c0f3282c5992bd1", // 712 ElectricRadiator.source
        "sha256:378be7c6503e8d80986b89d83e07f5143a0a228293c1fc1476900c8189965988", // 713 ElectricRadiator.to_idf_object
        "sha256:bb280ab510cd4fec40098ae1c7d3e4415c6072d3e813f684fa651e141b2f996a", // 720 FanCoilUnit
        "sha256:be0745ca1a1b4700c2e27eb45dc9bb05793eb4ade45654b0297fe14f5ef27ea0", // 721 FanCoilUnit.__init__
        "sha256:38d08bcefee2147039263264fd0d0fbfc567cd4b79f7afcd718208ba92ee9bfb", // 722 FanCoilUnit.coolable
        "sha256:e34c694afa4cff16009deffc51d8141720354efc10496e8075781c9aedf57954", // 723 FanCoilUnit.heatable
        "sha256:e7e725eefcfc55433d9747154eb11a904fbd8b873ab3ff0ff40c14152393559f", // 724 FanCoilUnit.idf_objtypename
        "sha256:362f4ffff2d767e47d1d7192e8b27ecc45bd6ec345ea494180231791731ea989", // 725 FanCoilUnit.to_idf_object
        "sha256:4c2b33ef859daae7ec39da8979f4496f519c48c3b976d1421eab61fdf984b2de", // 750 PackagedAirConditioner
        "sha256:cd9e6b36aad7cf5ca5de9697247b49dbdd12d35bbd88c6c73c0fa3304628af1a", // 751 PackagedAirConditioner.coolable
        "sha256:7ceacc168c16c052a97afd46622941442990c5d41f487485389e2efc9653f761", // 752 PackagedAirConditioner.heatable
        "sha256:530c3f2792d5f39e2cc300f6c7ac91ceb5c1e04c6ffebe74b5ebc453c6e06cb7", // 762 RadiantFloor
        "sha256:7f1512c7f394df74e4ad23c7fc5346370f9dec5c4981a12073a309a0c9e183f5", // 763 RadiantFloor.__init__
        "sha256:740f5e8a1c558f13eca09c803630f8187aff4772f56fccc4873ec0f1c49653cf", // 764 RadiantFloor.coolable
        "sha256:73d89804f0114a1cf6a02171c132e2515666105c588131832f9eb00e4dbabbb7", // 765 RadiantFloor.heatable
        "sha256:27f13c07893603bf1a717fd5ebbaba8c5cd7fbc0f697516e31aea62ae36f3b11", // 766 RadiantFloor.idf_objtypename
        "sha256:da09ca8679903363c0ccb438f53528badef8285420cf7afeed3cb19ce7ef3775", // 767 RadiantFloor.to_idf_object
        "sha256:62585c6a35a3080722c2c7829a65875318c1a1340699503f486c8e5000b656d9", // 768 Radiator
        "sha256:192e1a643694011b815b6c6483ef4d12e80f6c7f8c8c2912bbf1b11c2d17799b", // 769 Radiator.__init__
        "sha256:a1819e8e7b83266cf08ce4e0b1ca587dae1f212c65dc3de213b50962e2d9794b", // 770 Radiator.coolable
        "sha256:032438c2afc8102795e1d1d4625ec0ba4c709a17324b1572e6957db5aa3adfbf", // 771 Radiator.heatable
        "sha256:199aecfe4d7f47923a94171ba94d946564f74487851dcfbe281d8768c77901f0", // 772 Radiator.idf_objtypename
        "sha256:118b1a00ba51ab9c8fb8e586a0c70cd74b0b62e3a0a3ce0ec235d5067611163e", // 773 Radiator.to_idf_object
        "sha256:f8992f781ca1d942ff52fbc6006260eabcf07b6b558f270766fc0007477092f4", // 789 SupplyGroup
        "sha256:243d88f239ed0527c68ee6e6ee3e9b0e8a951aa1b8079fc12c2eeb83ee03baba", // 797 SupplySystem
        "sha256:629de9e8b9bf3e8fde50eb0e54593a6487f8b5c3a892311223eddf878af27153", // 798 SupplySystem.idf_get_airinletnodename
        "sha256:cc27c11cac8dae224d698c78e4dce53f1aa7ec5eb3168e6acc34075401e02a21", // 799 SupplySystem.idf_get_airoutletnodename
        "sha256:fe188d80175a47cf8b059aa17de25520f75bc603d651d224a4cee218748d3bf8", // 800 SupplySystem.idf_get_demandbranchname
        "sha256:b35bf0029a40e5443e211584fd9f3767e7c42698318e80fb62f7807522087544", // 801 SupplySystem.idf_get_objname
        "sha256:4fde991324c3c698d51a5a870577c66faff4aadd2f71573a41ffb7fb1d7cf94e", // 802 SupplySystem.idf_objtypename
        "sha256:7f8873a5a2d518e9d8bd7c04c7feb5a5babebf1bf6bf1422fe235c778d136811", // 803 SupplySystem.to_idf_object
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:807b1d675ad5b2e748255fd5bfbb608a73862abd2a122c1a5ef455bec8dbc730", // dragon-hvac-supply-core-645-airhandlingunit
        "sha256:12807a267d354f562ee196af8271162dac2c02e6ade1e58a4ac7e873365a9bcb", // dragon-hvac-supply-core-647-airhandlingunit-__init__
        "sha256:48f9304f3935277c084f9b855031cdfbfde8948003b450ab9148c3865f89ae20", // dragon-hvac-supply-core-648-airhandlingunit-coolable
        "sha256:c5bbedf2d07bcaef9e0b6545912c3b9ea1796e2caa9658876e3ab83f48fd0255", // dragon-hvac-supply-core-649-airhandlingunit-heatable
        "sha256:f25432ed05673dea2c2290a2bff2d63e58f6bb202010b800d24b1bd64d6fe9ed", // dragon-hvac-supply-core-650-airhandlingunit-idf_objtypename
        "sha256:e2f1e4165e32b1e154e8a12b2abbb034e2a11e758e7446139379ab6ab9426bc3", // dragon-hvac-supply-core-651-airhandlingunit-to_idf_object
        "sha256:62fe6ce0a7b124b13e8edd9766aa58af13e59561c958711c2e9e4b16a28ef0d2", // dragon-hvac-supply-core-700-electricradiantfloor
        "sha256:968f9321c606981c61b14de2b60da763f898123d71c68b2f66b15512ad5fc38a", // dragon-hvac-supply-core-701-electricradiantfloor-__init__
        "sha256:b37d6a0d0af3ac72117bf133ccdd059a2127a451219d68080a68ac020a6f7651", // dragon-hvac-supply-core-702-electricradiantfloor-coolable
        "sha256:a4450a04b3f89c697e4065d4ed3be5d8a3084a67a8641c30c49d8c85687cb638", // dragon-hvac-supply-core-703-electricradiantfloor-heatable
        "sha256:8fb11f89255a1b3acf80e4728fba2bd972cbff7268715cf3999a606634f10df3", // dragon-hvac-supply-core-704-electricradiantfloor-idf_objtypename
        "sha256:65135f234d7811d718df49186571f8e4e4f1a5403f77c4973c1f418efb71f977", // dragon-hvac-supply-core-705-electricradiantfloor-source
        "sha256:c6cabadcc7fe58eae9aedcb920e43870722c24984d6246169d82749c4d7ee06e", // dragon-hvac-supply-core-706-electricradiantfloor-to_idf_object
        "sha256:d2b266332649d8da62d95c7dcb885d2f899060e17b9dc47b8a9423ac69c7babc", // dragon-hvac-supply-core-707-electricradiator
        "sha256:26a9e56eb960ecd2b99772299e23da22c7cb0b42cd3f1a14f783a616baf03d30", // dragon-hvac-supply-core-708-electricradiator-__init__
        "sha256:969dcfc3ee2379b292b6d2cd553a910d700c38caeb173e66e6899a73c6fbf5e7", // dragon-hvac-supply-core-709-electricradiator-coolable
        "sha256:e71622d5b1d8ec67dcfff640b9fc66b75ec4684dda731f91d272129df3239dc0", // dragon-hvac-supply-core-710-electricradiator-heatable
        "sha256:6420c5395220f33c75e62b4484f2ac25b6166f9ea6d1e180a0f6a4f466abf738", // dragon-hvac-supply-core-711-electricradiator-idf_objtypename
        "sha256:b627b4cc5c9a14c83fe54a144577eac8c3a02379fd2a1c4aa3010e4923b4057c", // dragon-hvac-supply-core-712-electricradiator-source
        "sha256:7a8fe1b374a86c303447e1b04ccd769074d6ce2028d5adaeb46322b571c616d9", // dragon-hvac-supply-core-713-electricradiator-to_idf_object
        "sha256:2bd8eae553a88d365b27298e387656836b83258c9330dffdcddd21fdc4ded698", // dragon-hvac-supply-core-720-fancoilunit
        "sha256:e59dd3a862b0f074bf2a1111636e194d8926a35388786007bfc4ed8cf1e20d49", // dragon-hvac-supply-core-721-fancoilunit-__init__
        "sha256:fb1818187fba008f0714c48a9e1b978d97d36bdbc3d3c58cc438f87be102797b", // dragon-hvac-supply-core-722-fancoilunit-coolable
        "sha256:ab89518d4e807a57db782445ab4e8ff8329db213c51a306f3d2b3594f88ba291", // dragon-hvac-supply-core-723-fancoilunit-heatable
        "sha256:d4317dc9bfd7d235278a3ff4a452120e8e76b0c5551f60dd573187b814dee188", // dragon-hvac-supply-core-724-fancoilunit-idf_objtypename
        "sha256:8ed2a7d44dbca32536d105aad89bbfae678096671d0fa9c733d84c82c6b9eff7", // dragon-hvac-supply-core-725-fancoilunit-to_idf_object
        "sha256:9e45db82d5e9e235ed9eb99c81e547dbcb056e4a87bc37f5955035bccc7ce3b4", // dragon-hvac-supply-core-750-packagedairconditioner
        "sha256:c3db172f9bd5e9cc2f8cfede77c8354442d323aa26e3d7331f4ad3b46014930d", // dragon-hvac-supply-core-751-packagedairconditioner-coolable
        "sha256:e8f19c2f4f9f4b8ad36d77f01c30ae3b27118dd2914e4aa2edd928af5afce8ae", // dragon-hvac-supply-core-752-packagedairconditioner-heatable
        "sha256:70b11a92e2d61bbdfd972e2f6300b26d2be2b4b6b1677b0fcd51b16a166436bb", // dragon-hvac-supply-core-762-radiantfloor
        "sha256:44343c2abd459b7c6c4b1266e25e9d7bd2ff252e25f9ba0c371013a95da7b917", // dragon-hvac-supply-core-763-radiantfloor-__init__
        "sha256:46b97accf92441277dd8e694bb53294f4791a18c47b0d464d65b0efbb3743997", // dragon-hvac-supply-core-764-radiantfloor-coolable
        "sha256:df51492148cf6492cf46a0c38cdc61b7a0f2f25653ac2f2a517640eb90c8ad46", // dragon-hvac-supply-core-765-radiantfloor-heatable
        "sha256:7da9f0c22b567616dff966cea6015d36c9f8344e787d4c0ba7d81b99a6b81b58", // dragon-hvac-supply-core-766-radiantfloor-idf_objtypename
        "sha256:1a38fffca8705696951f0a5182641771890196277a544161160cb01a4f5845dc", // dragon-hvac-supply-core-767-radiantfloor-to_idf_object
        "sha256:297d83f38cb93387ff47a2a9c6c987889d615c20a9801a10fc8da8790de8d0bc", // dragon-hvac-supply-core-768-radiator
        "sha256:28162a2a059cb6001072ae6b74ed5cf39a3701eec0a6b3103e6ec099ebc39233", // dragon-hvac-supply-core-769-radiator-__init__
        "sha256:c67224f28b31a0b3769dd277b4651c70a477a95cc1143642bd1d29d14dccd530", // dragon-hvac-supply-core-770-radiator-coolable
        "sha256:40ce6ba112d3dd539e8e267ec9d03a25dac2cbed1a59d2ff05f1313d59b320de", // dragon-hvac-supply-core-771-radiator-heatable
        "sha256:58b2ef8d8ea91d9be8795a3fb7235c08522f552ff4e6d3ada0b3f2f59bfacf6b", // dragon-hvac-supply-core-772-radiator-idf_objtypename
        "sha256:e5b5bc97f8fd7cfc84b1034d74df45a401803aa348743d71b0bb2de8b6a87449", // dragon-hvac-supply-core-773-radiator-to_idf_object
        "sha256:e8445f69b273908c2ae38265a4fbec2ca6b9c120385848937c8e19565f340970", // dragon-hvac-supply-core-789-supplygroup
        "sha256:b26e2eb6246fe2a78de42316156ad6903155652af420aa696ab6128a1c7b3f9c", // dragon-hvac-supply-core-797-supplysystem
        "sha256:08dbd12aa74a2cd93737fbbad8b5695a5552271a60d0d3783044d7691b6298df", // dragon-hvac-supply-core-798-supplysystem-idf_get_airinletnodename
        "sha256:34edd8aab23b8d5a57c49aa3b5f02dc90e8f249f4f3897529be9f6ee95a7c30b", // dragon-hvac-supply-core-799-supplysystem-idf_get_airoutletnodename
        "sha256:8ae912c0bce1c81f5ac5b03831bdcfc4cd30571ba426da81211f65461022742c", // dragon-hvac-supply-core-800-supplysystem-idf_get_demandbranchname
        "sha256:f1aeea1f2d87a34dedc006ac8ac0a08e1df4fd74ad1a6c3d0806d3656922a958", // dragon-hvac-supply-core-801-supplysystem-idf_get_objname
        "sha256:afc0de48818a7f9f9cc4d1815730839f446b117713d28628e7396f3ddb58b1db", // dragon-hvac-supply-core-802-supplysystem-idf_objtypename
        "sha256:59e45b93aa423bfc8ab127daebfcc97cfbc535c71666f4d24a18b8a677e14f1b", // dragon-hvac-supply-core-803-supplysystem-to_idf_object
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
            string casePins = string.Join(
                Environment.NewLine,
                observations.Select(item =>
                    $"        new(\"{item.Code}\", {item.Facts.Length}, \"{item.FactsSha256}\"),"));
            string receiptPins = string.Join(
                Environment.NewLine,
                corpus.Targets.Select((target, index) =>
                    $"        \"{receiptHashes[index]}\", // {target.InventoryIndex} {target.Symbol}"));
            string collectorPins = string.Join(
                Environment.NewLine,
                corpus.Targets.Select((target, index) =>
                    $"        \"{collectorOutputHashes[index]}\", // {target.AssertionId}"));
            throw new Xunit.Sdk.XunitException(
                "SUPPLY_CORE_NATIVE_PINS" + Environment.NewLine +
                "CASES" + Environment.NewLine + casePins + Environment.NewLine +
                "RECEIPTS" + Environment.NewLine + receiptPins + Environment.NewLine +
                "COLLECTOR_OUTPUTS" + Environment.NewLine + collectorPins);
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].Code, observations[index].Code);
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
                RegistryAssertionId(corpus.Targets[index].AssertionId),
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
            recordCount++;
        }

        Assert.Equal(49, recordCount);
        Assert.Equal(49, corpus.Targets.Length);
        Assert.Equal(
            49,
            corpus.Targets.Select(item => RegistryAssertionId(item.AssertionId))
                .Distinct(StringComparer.Ordinal)
                .Count());
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

    private static string RegistryAssertionId(string fixtureAssertionId)
    {
        Assert.Equal(fixtureAssertionId, fixtureAssertionId.Trim());
        Assert.Matches("^[a-z0-9_-]+$", fixtureAssertionId);
        string identifier = Regex.Replace(fixtureAssertionId, "[^a-z0-9]+", "-").Trim('-');
        Assert.Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$", identifier);
        return identifier;
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
