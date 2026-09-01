#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Profile;
using Dragons.InvisibleDragon.Shape;
using Dragons.InvisibleDragon.Tests.Model;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class AppendersControllersOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-hvac-appenders-controllers-oracle.json";
    private const int FixtureBytes = 178_786;
    private const string FixtureSha256 =
        "sha256:24b6994b1a39aa363fb0127ea6bfd93bcd12c803768e04f634ed615f08f815eb";
    private const string FixtureSchema =
        "dragons.python-reference.dragon-hvac-appenders-controllers.v1";
    private const string FixtureRepositoryCommit = "d14de9e";
    private const string CasesSha256 =
        "sha256:2282854918bee238667f1307ecbdf21fa79ff7ceb305810622e6827afec7dd3d";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_hvac_appenders_controllers_oracle.py";
    private const int GeneratorBytes = 77_246;
    private const string GeneratorSha256 =
        "sha256:357763c4c73e48db275833ab884bf550ea5e143126f550520e9a748bb17154d6";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_hvac_appenders_controllers_oracle.py";
    private const int ValidatorBytes = 28_120;
    private const string ValidatorSha256 =
        "sha256:f6699787a997dc3daad0b8606b5581e93220d1a97476cad44f42052503730eb3";

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
        "sha256:5228c06e02e371e4da5106bb10ba5e2159bd38b452ecdb2be459245c318f2495";
    private const string SupportReceiptsSha256 =
        "sha256:88586a379f20f459fe1500bdc3ec4843aa161e11ac2e4426eeed81754f59c052";
    private const string DeferredReceiptsSha256 =
        "sha256:2172f41f390f28cc737f78b6e476876c04fda668a64901778bcaf2199393b62e";
    private const string FullSourceReceiptsSha256 =
        "sha256:f5db7f1a79890387192db20619e055691700f48bfbe368efeffbe37b695593e7";
    private const string LoadedModulesSha256 =
        "sha256:93cfad21e009eac906a4443998ad214eec82e2136ada5b7cea7888ababf30143";
    private const string RelocatedObservationsSha256 =
        "sha256:87cf389f96bf8041e9dca0b22291a465aff5715e209d190a175b03a70cbf7d65";
    private const string RuntimeDependenciesSha256 =
        "sha256:f69d29212b5ce6432b0c02f356d036275ea01463a8e1974ac6f89b78854fefba";
    private const string RuntimeSignaturesSha256 =
        "sha256:f44e1bfd639b1c59739524d9d795d6fba96336affacb4d7fa20104b0c8a2c1d5";
    private const string NativeSourceReceiptsSha256 =
        "sha256:94fae1cf2431e27aec7389f65a8a9acd8d91fb91b372ebf25b7ec8c03c8d9672";
    private const string NativeRoutesSha256 =
        "sha256:0a73408d863943c88142137daed57b33ce6a0a5116109f5294d98f6759ec4119";
    private const string ClassificationSha256 =
        "sha256:b2abc7395c99e45184fc69f8cccc4f8215a44dc5d683fd7297ba4da43cfcf60e";

    private const string PublicRoute =
        "Dragons.InvisibleDragon.Hvac.SupplyGroup -> " +
        "Dragons.InvisibleDragon.Hvac.ZoneHvacAssignment -> " +
        "Dragons.InvisibleDragon.Model.EnergyModel -> " +
        "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?) -> " +
        "Dragons.InvisibleDragon.Idf.IdfDocument";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Hvac.AppendersControllersOracleParityTests.MatchesPinnedAppendersControllersThroughPublicAggregateRoute";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_561,
            "sha256:fcbe9c38cacade8002d121b0834a4441560086052571dd654f3c185a0c897249"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SupplySystems.cs", 18_249,
            "sha256:bf93e1c6889f7d371fff983caad1b3c90d4cbc6113bbb5d9a7a783740af1bb46"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs", 21_985,
            "sha256:60357af37bea1f6e7dd0640254a30761ed4097d53751183e5902c2efa62a0f28"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_723,
            "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs", 13_173,
            "sha256:0d16e28d37136a3aa0015759ead7ee324cfed08cff1a3269326d4af144518048"),
    };

    private static readonly ArtifactPin[] SupportArtifacts =
    {
        new("tools/python-reference/generate_dragon_hvac_supply_core_oracle.py", 65_859,
            "sha256:7ce1af80729c2f2aa333016ba95db3963b25db24e1b23d2c89f49ea2694590e2"),
        new("fixtures/reference/python-0.7.0/dragon-hvac-supply-core-oracle.json", 215_230,
            "sha256:657b53b768c90a2915ca10c781ff63ab5a21323bb09f534d4d5da3178fe99194"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "dragon-hvac-appenders-controllers.demand-branch-appender", "demand-branch",
            "sha256:ed597ff707a9e05f6e13272a2046862ddd012e9cc3e4153b88911e74145dfc03",
            "sha256:ad7d55e8192bef04c3f6509932e8760791f0bf8cc5bd67a81de7a1d80638ab53", 7),
        new("B01", "dragon-hvac-appenders-controllers.equipment-list-appender", "equipment-list",
            "sha256:be089ecb06d8b1831af4ccc2f1f47fabc14cbaec81a98418e8cd9253c20999a9",
            "sha256:6b292e7f7e56bb7a01ae1f5c43f16bad2bbd4f0c4592272546e5fa3e907e1f83", 3),
        new("C01", "dragon-hvac-appenders-controllers.sequential-load-fraction-controller", "sequential-controller",
            "sha256:6db87a69044821b11b27016abd09eb7a0dffdc2764d66e71ce8a98cef2ef2fda",
            "sha256:d72589acfc903f580d3d4fb0f32942cc3e7178b5409e79e829b65c9e72b8f16c", 3),
        new("D01", "dragon-hvac-appenders-controllers.supply-system-postprocessor", "postprocessor-base",
            "sha256:a9532aee0ed8dcee59d6c507a16bddde1e8e02a6e83e7d27a11a9e3831cf5ee8",
            "sha256:200eee5adb57d9dbaac68ccff8b4cf34319881d280bbbd5ac50baee628947a90", 4),
        new("E01", "dragon-hvac-appenders-controllers.zone-air-node-appender", "zone-air-node",
            "sha256:01734b8966710d9079ca632b73b357feafde8b4ca4a7bc41d6e4171f052dac93",
            "sha256:54ee0a2d373e153cf6a7ca02a361841b34e273999ba3cf0be2e97560b3496337", 4),
        new("F01", "dragon-hvac-appenders-controllers.zone-terminal-unit-appender", "zone-terminal-unit",
            "sha256:0ef5b24143176c6dbc201a4477fe79c0008b2ba48277c179ae6f1ed9c6450e58",
            "sha256:a77be5dbc43bf4b51ba33fd30573a8ec478281857ec5593fb78c68324436dddb", 3),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        new(686, "DemandBranchAppender", "dragon-hvac-appenders-controllers-686-cdbb9fb8", "public-aggregate-hvac-postprocessing-686", Cases[0].CaseId),
        new(687, "DemandBranchAppender.append_to_branchlist", "dragon-hvac-appenders-controllers-687-72c53b16", "public-aggregate-hvac-postprocessing-687", Cases[0].CaseId),
        new(688, "DemandBranchAppender.append_to_mixer", "dragon-hvac-appenders-controllers-688-bf8d6bd1", "public-aggregate-hvac-postprocessing-688", Cases[0].CaseId),
        new(689, "DemandBranchAppender.append_to_spliter", "dragon-hvac-appenders-controllers-689-29bdd382", "public-aggregate-hvac-postprocessing-689", Cases[0].CaseId),
        new(690, "DemandBranchAppender.count_current_branches_branchlist", "dragon-hvac-appenders-controllers-690-2fb35691", "public-aggregate-hvac-postprocessing-690", Cases[0].CaseId),
        new(691, "DemandBranchAppender.count_current_branches_connector", "dragon-hvac-appenders-controllers-691-4531204e", "public-aggregate-hvac-postprocessing-691", Cases[0].CaseId),
        new(692, "DemandBranchAppender.run", "dragon-hvac-appenders-controllers-692-3d176f2c", "public-aggregate-hvac-postprocessing-692", Cases[0].CaseId),
        new(717, "EquipmentListAppender", "dragon-hvac-appenders-controllers-717-268e7fb5", "public-aggregate-hvac-postprocessing-717", Cases[1].CaseId),
        new(718, "EquipmentListAppender.count_current_equipments", "dragon-hvac-appenders-controllers-718-ef88aa10", "public-aggregate-hvac-postprocessing-718", Cases[1].CaseId),
        new(719, "EquipmentListAppender.run", "dragon-hvac-appenders-controllers-719-9fbbb80e", "public-aggregate-hvac-postprocessing-719", Cases[1].CaseId),
        new(774, "SequentialLoadFractionController", "dragon-hvac-appenders-controllers-774-35b327ea", "public-aggregate-hvac-postprocessing-774", Cases[2].CaseId),
        new(775, "SequentialLoadFractionController.find_target_equipment_number", "dragon-hvac-appenders-controllers-775-5a959bd0", "public-aggregate-hvac-postprocessing-775", Cases[2].CaseId),
        new(776, "SequentialLoadFractionController.run", "dragon-hvac-appenders-controllers-776-efc3dc2d", "public-aggregate-hvac-postprocessing-776", Cases[2].CaseId),
        new(804, "SupplySystemToIdfPostProcessor", "dragon-hvac-appenders-controllers-804-9b4492ed", "public-aggregate-hvac-postprocessing-804", Cases[3].CaseId),
        new(805, "SupplySystemToIdfPostProcessor.__init__", "dragon-hvac-appenders-controllers-805-c63e7515", "public-aggregate-hvac-postprocessing-805", Cases[3].CaseId),
        new(806, "SupplySystemToIdfPostProcessor.run", "dragon-hvac-appenders-controllers-806-582cf3ef", "public-aggregate-hvac-postprocessing-806", Cases[3].CaseId),
        new(807, "SupplySystemToIdfPostProcessor.source", "dragon-hvac-appenders-controllers-807-ce40cc13", "public-aggregate-hvac-postprocessing-807", Cases[3].CaseId),
        new(808, "ZoneAirNodeAppender", "dragon-hvac-appenders-controllers-808-bff1883c", "public-aggregate-hvac-postprocessing-808", Cases[4].CaseId),
        new(809, "ZoneAirNodeAppender.count_current_nodes", "dragon-hvac-appenders-controllers-809-fc0fbad2", "public-aggregate-hvac-postprocessing-809", Cases[4].CaseId),
        new(810, "ZoneAirNodeAppender.ensure_nodelist_existence", "dragon-hvac-appenders-controllers-810-e3fabdb5", "public-aggregate-hvac-postprocessing-810", Cases[4].CaseId),
        new(811, "ZoneAirNodeAppender.run", "dragon-hvac-appenders-controllers-811-7cd1f8e9", "public-aggregate-hvac-postprocessing-811", Cases[4].CaseId),
        new(812, "ZoneTerminalUnitAppender", "dragon-hvac-appenders-controllers-812-4ae86427", "public-aggregate-hvac-postprocessing-812", Cases[5].CaseId),
        new(813, "ZoneTerminalUnitAppender.count_current_units", "dragon-hvac-appenders-controllers-813-fc0fbad2", "public-aggregate-hvac-postprocessing-813", Cases[5].CaseId),
        new(814, "ZoneTerminalUnitAppender.run", "dragon-hvac-appenders-controllers-814-46d42798", "public-aggregate-hvac-postprocessing-814", Cases[5].CaseId),
    };

    private static bool DiscoverPins => false;

    private static readonly NativePin[] ExpectedNativePins =
    {
        new("A01", 11, "sha256:83035ae4c93c2cc8c7830bc4c0c75e43b3c38f648b26393be655504d3a5edae1"),
        new("B01", 10, "sha256:2909f50a2240de7183f58d2aa6cd97faac782ee3a420c1d0cf2e42ebc9c95418"),
        new("C01", 10, "sha256:0ed892f60ffbefd9e48dd6dbff1c3a31f8e3099a0ffd24dac340755e7deadbc8"),
        new("D01", 10, "sha256:32887e5ccc1693074d8bd133770ed424542be9b06a5d476e02f512426df07c64"),
        new("E01", 10, "sha256:05f36796e8f7e825b1937d2d69fcd82e04f0b87ee277b5dd3f71037d4d823f16"),
        new("F01", 9, "sha256:7f870facb1c00411823b577cb31c10f6862dfd072463ee06f07547d85e307499"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:ac3e8f85290a73566498a468c9ea2d7c5ec24989ab215c80173aabcf5f75ef2c", // 686 DemandBranchAppender
        "sha256:7029a94f8dc4c26c6baa815c692d414d97cb4ece0131d587b2e7e93260d19038", // 687 DemandBranchAppender.append_to_branchlist
        "sha256:6159b8d9ed0ec3164fe5dee19118ae12a4871f69a0e328470731580307df0c95", // 688 DemandBranchAppender.append_to_mixer
        "sha256:8d16261926e34f3a123a412b07b2185429b91436c4c9cd6fb36865d6fbf12370", // 689 DemandBranchAppender.append_to_spliter
        "sha256:b4212baf9326aad582c981bf4ef96b3ae1c6f838f473d69a348987b74718ae5d", // 690 DemandBranchAppender.count_current_branches_branchlist
        "sha256:b02f9be6d0ad9594923e78837a5f17c4b95d5c25c5637931f3bcd5992bb5a8d8", // 691 DemandBranchAppender.count_current_branches_connector
        "sha256:d9a9925886acb10ab541373d4f9b7d5d953745fe6d8330ba94d9ac48ccdf03d2", // 692 DemandBranchAppender.run
        "sha256:02147c3cda8b4687fbb220559057525e54ffe7ee43b575a17566606c7a4c6cb7", // 717 EquipmentListAppender
        "sha256:5539a5babc1d4d3467d2b56be7ecf17377234bfc315fc51ba8169ae07c3fd78d", // 718 EquipmentListAppender.count_current_equipments
        "sha256:a5e3899e61e46fe1bf2e84d3b91835fbfae7cbf36c0ea40b67972b0649323405", // 719 EquipmentListAppender.run
        "sha256:da494ab7d47a12600b8c17f56b60b3ddb808e7f1011801568b77ae6b0acbeef8", // 774 SequentialLoadFractionController
        "sha256:62de4b553f166c6370d7110e35bb2bc7deca698ca58b3554464a7560f1c61785", // 775 SequentialLoadFractionController.find_target_equipment_number
        "sha256:5f74b3442761073bfd591e8917053d3f2a25b9e5a3b0cc97b4ae21fa3df934c5", // 776 SequentialLoadFractionController.run
        "sha256:644b43b029d2756b255f3b70bb57b534d6ecbe91a5c647c8d85068b64ade9280", // 804 SupplySystemToIdfPostProcessor
        "sha256:de4b745a468d51853245adfdac250cb9b9cbb7918b40e904ec37364d6b44d9ba", // 805 SupplySystemToIdfPostProcessor.__init__
        "sha256:041258a9fb3e2ebc8e81eb9652ba5a9f271df0e5a2373d09a549cba0ca4cb5f0", // 806 SupplySystemToIdfPostProcessor.run
        "sha256:daf4ee9cbd988323d0cf87f8da107b36b4bb3c8562bda82318291f76d7e0ca4f", // 807 SupplySystemToIdfPostProcessor.source
        "sha256:87f7ad7c65e0a4c3bae4183905538186311d2f5de087fafb85dec67c29db31f9", // 808 ZoneAirNodeAppender
        "sha256:e346d56289c1025e9c2924eb3ecfd50e557c55e0c18d94c49bf66eaadc25a943", // 809 ZoneAirNodeAppender.count_current_nodes
        "sha256:1813f347860591a9abbf748ba7941c225e79053813957d1cb6fc556e56faf336", // 810 ZoneAirNodeAppender.ensure_nodelist_existence
        "sha256:af38cf80dc00e710ca71b1444dc3626492a93bfb1737da24f10190f6a9c79e1c", // 811 ZoneAirNodeAppender.run
        "sha256:569a4414c90c70460468a2a427113aaaba6a25b77267b376e19675bdce4ac8ca", // 812 ZoneTerminalUnitAppender
        "sha256:0e54820557c9ea91a0c02995380d57d8fd1c30b599fdcaf9efd8d997d7a4bec7", // 813 ZoneTerminalUnitAppender.count_current_units
        "sha256:e82051aaf440188599a59af12d16044151fc3a23d11221fb61e5f1017cec07b2", // 814 ZoneTerminalUnitAppender.run
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:a9a626d770209f9bd848047d9524a9658ee4a5d272d222edfee41591f8b3647d", // dragon-hvac-appenders-controllers-686-cdbb9fb8
        "sha256:140f87040ac939f946cc4d35c4ce32e9a679ee807e4ccbfe28b47ab303d79e50", // dragon-hvac-appenders-controllers-687-72c53b16
        "sha256:3d3ece3a59cf239bb770124e751291220ce948c20e4175d417ddb36f87b16779", // dragon-hvac-appenders-controllers-688-bf8d6bd1
        "sha256:277707ef044d1fcf42318fa7836b54fc0b6990af7988e56f571770cce01cd832", // dragon-hvac-appenders-controllers-689-29bdd382
        "sha256:30e586177ea4391276ece2cb542654993cabc293c8d0cf3ff57b9ffa79506758", // dragon-hvac-appenders-controllers-690-2fb35691
        "sha256:1c8518b5584364b7b92e4794d51f7b42bb62eb907a85e7419f0201985c05359f", // dragon-hvac-appenders-controllers-691-4531204e
        "sha256:19485f416ca70e51841bc648f4c8653e77f7a279149cb932f6c523a251bcfaaf", // dragon-hvac-appenders-controllers-692-3d176f2c
        "sha256:b423b7ad259deba23ed7ae9cdbd1791aa5dfb2328f53ac02b5e902b096ddfe21", // dragon-hvac-appenders-controllers-717-268e7fb5
        "sha256:3d234d41d2486a3262031d57623a7fecb38799e552650fd8f2e196cc3293dc24", // dragon-hvac-appenders-controllers-718-ef88aa10
        "sha256:9986c97a12829a49c5f47884fa0d7f0d14f6f6414be59f5cc62feed4a6553c4f", // dragon-hvac-appenders-controllers-719-9fbbb80e
        "sha256:d16b2f2fb7d6c105c717d193d5a2bd1afa99153b2ef9a83770437dc5a0888827", // dragon-hvac-appenders-controllers-774-35b327ea
        "sha256:317746a3881979c403dc1fc9c64b1cc8fef567c1efbc6d4fe9df0ddb849bfe42", // dragon-hvac-appenders-controllers-775-5a959bd0
        "sha256:0ddf50821e519f378da5fdb8cd94a801324c260fcce613170839cb04456b7541", // dragon-hvac-appenders-controllers-776-efc3dc2d
        "sha256:1493560fd783f86b72d6308afda618016cc64ac6c1b31661625af77016bdc4a2", // dragon-hvac-appenders-controllers-804-9b4492ed
        "sha256:873e223decf86f705e961adaf56582e544dc5000c273eececba0e994015aa21f", // dragon-hvac-appenders-controllers-805-c63e7515
        "sha256:1f42672d28cf67b54b4f1173c9993644ff7a1c05ae8de705cbd1c87c4ae3f097", // dragon-hvac-appenders-controllers-806-582cf3ef
        "sha256:1786b1581d0be9b40c11737e6d1fa12ade479883ac71de4bd73ce53a02d98272", // dragon-hvac-appenders-controllers-807-ce40cc13
        "sha256:4d86750a088a091d1a3bafb7bea49db0825d0fd2080d66f3bdb7b472b2c6fe7d", // dragon-hvac-appenders-controllers-808-bff1883c
        "sha256:bd55047a7cf33cfa6c30744d3bc246e79faf9db93806963f846c7d42bd5d302f", // dragon-hvac-appenders-controllers-809-fc0fbad2
        "sha256:0dade83502f4d24fdf6e585798f0b0fd0a3033f0acd45db1d52acec02bdce894", // dragon-hvac-appenders-controllers-810-e3fabdb5
        "sha256:31bbf27eb9a8a86763ad0b19622dbc249b856b6ff82ab914a472833df970522f", // dragon-hvac-appenders-controllers-811-7cd1f8e9
        "sha256:e36179f56bd2ca88cc71ac5aa17953322bc66e440f1f61ed4eef5a365e80de83", // dragon-hvac-appenders-controllers-812-4ae86427
        "sha256:feff175919689d3a096401c4cceb530be248cadf0ccaf55b04f7a092b0d1911e", // dragon-hvac-appenders-controllers-813-fc0fbad2
        "sha256:04cafc714fb5dd0a633d587d37f54da0a7da0c9f7595855322e0ee00f204767a", // dragon-hvac-appenders-controllers-814-46d42798
    };

    [Fact]
    public void MatchesPinnedAppendersControllersThroughPublicAggregateRoute()
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
            string factPins = string.Join(
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
                "APPENDERS_CONTROLLERS_NATIVE_PINS" + Environment.NewLine +
                "CASES" + Environment.NewLine + factPins + Environment.NewLine +
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
                corpus.Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
            recordCount++;
        }

        Assert.Equal(24, recordCount);
        Assert.Equal(24, corpus.Targets.Length);
        Assert.Equal(24, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(corpus.Targets, item => Assert.Equal("exception", item.Classification));
        Assert.Equal(6, corpus.FixtureCases.Length);
    }

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin pin in NativeSources.Concat(SupportArtifacts))
        {
            AssertPinnedArtifact(pin.Path, pin.Bytes, pin.Sha256);
        }

        ConstructorInfo supplyGroupConstructor = Assert.Single(
            typeof(SupplyGroup).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.True(supplyGroupConstructor.IsPublic);
        ConstructorInfo assignmentConstructor = Assert.Single(
            typeof(ZoneHvacAssignment).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.True(assignmentConstructor.IsPublic);
        MethodInfo toIdf = Assert.IsAssignableFrom<MethodInfo>(typeof(EnergyModel).GetMethod(
            nameof(EnergyModel.ToIdfDocument),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(Dragons.InvisibleDragon.Idd.IddSchema), typeof(EnergyModelIdfOptions) },
            modifiers: null));
        Assert.Equal(typeof(IdfDocument), toIdf.ReturnType);
        Assert.Contains(typeof(IReadOnlyList<IdfObject>), typeof(IdfDocument).GetInterfaces());
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

        JsonElement caseHashes = root.GetProperty("case_sha256");
        JsonElement factHashes = root.GetProperty("fact_sha256");
        AssertKeys(caseHashes, Cases.Select(item => item.CaseId).ToArray());
        AssertKeys(factHashes, Cases.Select(item => item.CaseId).ToArray());
        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        for (int index = 0; index < Cases.Length; index++)
        {
            CaseBinding expected = Cases[index];
            JsonElement actual = fixtureCases[index];
            Assert.Equal(expected.Code, RequiredString(actual, "code"));
            Assert.Equal(expected.CaseId, RequiredString(actual, "id"));
            Assert.Equal(expected.Subfamily, RequiredString(actual, "subfamily"));
            Assert.Equal(expected.CaseSha256, RequiredString(caseHashes, expected.CaseId));
            Assert.Equal(expected.CaseSha256, CanonicalSha256(actual));
            JsonElement facts = actual.GetProperty("python").GetProperty("facts");
            Assert.Equal(expected.PythonFactsSha256, RequiredString(actual.GetProperty("python"), "facts_sha256"));
            Assert.Equal(expected.PythonFactsSha256, RequiredString(factHashes, expected.CaseId));
            Assert.Equal(expected.PythonFactsSha256, CanonicalSha256(facts));
            Assert.Equal(expected.TargetCount, actual.GetProperty("target_symbols").GetArrayLength());
        }

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        JsonElement inventory = upstream.GetProperty("inventory");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        JsonElement source = upstream.GetProperty("source");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));
        Assert.Equal(TargetReceiptsSha256, RequiredString(upstream, "target_receipts_sha256"));
        Assert.Equal(SupportReceiptsSha256, RequiredString(upstream, "resolved_support_receipts_sha256"));
        Assert.Equal(DeferredReceiptsSha256, RequiredString(upstream, "deferred_receipts_sha256"));
        Assert.Equal(FullSourceReceiptsSha256, RequiredString(upstream, "full_source_receipts_sha256"));
        JsonElement isolated = upstream.GetProperty("isolated_import");
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal("two-byte-identical-repository-temp-copies", RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(LoadedModulesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(LoadedModulesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        Assert.Equal(RelocatedObservationsSha256, RequiredString(isolated, "relocated_observations_sha256"));

        JsonElement targetsElement = root.GetProperty("target_receipts");
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(targetsElement));
        JsonElement[] targetReceipts = targetsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, targetReceipts.Length);
        var targets = new TargetBinding[targetReceipts.Length];
        for (int index = 0; index < targetReceipts.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement actual = targetReceipts[index];
            Assert.Equal(expected.InventoryIndex, actual.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(UpstreamPath, RequiredString(actual, "path"));
            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                RequiredString(actual, "kind"),
                RequiredString(actual, "symbol_hash"),
                RequiredString(actual, "signature_hash"),
                RequiredString(actual, "body_hash"),
                expected.AssertionId,
                "exception",
                expected.AdaptationId,
                PublicRoute,
                expected.CaseId);
        }

        JsonElement[] symbols = root.GetProperty("symbols").EnumerateArray().ToArray();
        Assert.Equal(targetReceipts.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            Assert.Equal(RequiredString(targetReceipts[index], "symbol"), RequiredString(symbols[index], "symbol"));
            Assert.Equal(RequiredString(targetReceipts[index], "symbol_hash"), RequiredString(symbols[index], "symbol_hash"));
        }

        ValidateContract(root.GetProperty("consumer_contract"), targets);
        ValidateSupport(root);
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateNativeReview(root.GetProperty("native_review"));
        return new OracleCorpus(fixtureCases, targets);
    }

    private static void ValidateContract(JsonElement contract, IReadOnlyList<TargetBinding> targets)
    {
        Assert.Equal(6, contract.GetProperty("case_count").GetInt32());
        Assert.Equal(Cases.Select(item => item.CaseId), ReadStringArray(contract.GetProperty("case_ids")));
        JsonElement counts = contract.GetProperty("classification_counts");
        Assert.Equal(0, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(24, counts.GetProperty("exception").GetInt32());
        Assert.Equal(RuntimeSignaturesSha256, CanonicalSha256(contract.GetProperty("runtime_signatures")));

        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement routes = contract.GetProperty("native_routes");
        JsonElement coverage = contract.GetProperty("coverage_by_symbol");
        JsonElement expectations = contract.GetProperty("expectations");
        AssertKeys(classifications, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(assertions, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(adaptations, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(routes, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(coverage, targets.Select(item => item.Symbol).ToArray());
        AssertKeys(expectations, targets.Select(item => item.Symbol).ToArray());
        foreach (TargetBinding target in targets)
        {
            Assert.Equal("exception", RequiredString(classifications, target.Symbol));
            Assert.Equal(target.AssertionId, RequiredString(assertions, target.Symbol));
            Assert.Equal(target.AdaptationId, RequiredString(adaptations, target.Symbol));
            Assert.Equal(PublicRoute, RequiredString(routes, target.Symbol));
            Assert.Equal(target.CaseId, RequiredString(coverage, target.Symbol));
            JsonElement expectation = expectations.GetProperty(target.Symbol);
            Assert.Equal("exception", RequiredString(expectation, "classification"));
            Assert.Equal(target.AssertionId, RequiredString(expectation, "assertion_id"));
            Assert.Equal(target.AdaptationId, RequiredString(expectation, "adaptation"));
            Assert.Equal(PublicRoute, RequiredString(expectation, "native_route"));
        }

        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("exact_disjoint_source_partition").GetBoolean());
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_hvac_source_partition").GetBoolean());
        Assert.False(closure.GetProperty("target_support_overlap").GetBoolean());
        Assert.Equal(24, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(1, closure.GetProperty("resolved_support_count").GetInt32());
        Assert.Equal(149, closure.GetProperty("deferred_count").GetInt32());
        Assert.Equal(174, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("target_indices")));
        Assert.Equal(new[] { 796 }, ReadIntArray(closure.GetProperty("resolved_support_indices")));
        Assert.Equal(new[] { "SupplyGroup.to_idf_object" }, ReadStringArray(closure.GetProperty("resolved_support_symbols")));
        Assert.Equal(149, closure.GetProperty("deferred_indices").GetArrayLength());
        Assert.Equal(SupportReceiptsSha256, RequiredString(closure, "resolved_support_receipts_sha256"));
        Assert.Equal(DeferredReceiptsSha256, RequiredString(closure, "deferred_receipts_sha256"));
        Assert.Equal(FullSourceReceiptsSha256, RequiredString(closure, "full_source_receipts_sha256"));

        JsonElement evidence = contract.GetProperty("evidence_contract");
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
        Assert.True(evidence.GetProperty("resolved_index_796_reused_from_support").GetBoolean());
        Assert.False(evidence.GetProperty("internal_native_route_claim").GetBoolean());
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
    }

    private static void ValidateSupport(JsonElement root)
    {
        JsonElement support = root.GetProperty("support");
        Assert.Equal("immutable-index-796-supply-group-conversion-support-only", RequiredString(support, "role"));
        Assert.Equal("dragons.python-reference.dragon-hvac-supply-core.v1", RequiredString(support, "schema"));
        Assert.False(support.GetProperty("target_promoted").GetBoolean());
        Assert.Equal(9, support.GetProperty("case_count").GetInt32());
        Assert.Equal("sha256:29eacb2d29f528353302d1afd8e3ef646d7d35886237bb4a3fa494039a4ec36f", RequiredString(support, "cases_sha256"));
        ValidateArtifact(support.GetProperty("generator"), SupportArtifacts[0]);
        ValidateArtifact(support.GetProperty("fixture"), SupportArtifacts[1]);
        Assert.Equal(SupportReceiptsSha256, RequiredString(support, "resolved_receipts_sha256"));
        Assert.Equal(SupportReceiptsSha256, CanonicalSha256(support.GetProperty("resolved_receipts")));
        Assert.Equal(SupportReceiptsSha256, CanonicalSha256(root.GetProperty("resolved_support_receipts")));
        JsonElement receipt = Assert.Single(root.GetProperty("resolved_support_receipts").EnumerateArray());
        Assert.Equal(796, receipt.GetProperty("inventory_index").GetInt32());
        Assert.Equal("SupplyGroup.to_idf_object", RequiredString(receipt, "symbol"));
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal(RuntimeDependenciesSha256, RequiredString(runtime, "dependencies_sha256"));
        Assert.Equal(RuntimeDependenciesSha256, CanonicalSha256(runtime.GetProperty("dependencies")));
    }

    private static void ValidateNativeReview(JsonElement review)
    {
        Assert.Equal(ClassificationSha256, RequiredString(review, "classification_sha256"));
        Assert.Equal(NativeRoutesSha256, RequiredString(review, "routes_sha256"));
        Assert.Equal(PublicRoute, RequiredString(review, "public_production_route"));
        Assert.True(review.GetProperty("public_production_routes_only").GetBoolean());
        Assert.False(review.GetProperty("internal_generate_route_claimed").GetBoolean());
        Assert.False(review.GetProperty("internal_postprocessor_type_route_claimed").GetBoolean());
        Assert.False(review.GetProperty("python_executes_native_runtime").GetBoolean());
        Assert.Equal(NativeSourceReceiptsSha256, RequiredString(review, "source_receipts_sha256"));
        Assert.Equal(NativeSourceReceiptsSha256, CanonicalSha256(review.GetProperty("source_receipts")));
        JsonElement[] sources = review.GetProperty("source_receipts").EnumerateArray().ToArray();
        Assert.Equal(NativeSources.Length, sources.Length);
        for (int index = 0; index < sources.Length; index++)
        {
            ValidateArtifact(sources[index], NativeSources[index]);
        }
    }

    private static NativeObservation ObserveNativeCase(CaseBinding item)
    {
        string[] facts = item.Code switch
        {
            "A01" => ObserveDemandBranches(),
            "B01" => ObserveEquipmentList(),
            "C01" => ObserveSequentialFractions(),
            "D01" => ObservePublicPostprocessingRoute(),
            "E01" => ObserveZoneAirNodes(),
            "F01" => ObserveZoneTerminalUnits(),
            _ => throw new InvalidOperationException("Unknown appender/controller case: " + item.Code),
        };
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        return new NativeObservation(item.Code, item.CaseId, facts, CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveDemandBranches()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-DB-ZONE", "Appender Demand Zone");
        var boiler = new Boiler(Id("APP-DB-BOILER"), "Appender Demand Boiler", Fuel.NaturalGas);
        var first = new Radiator(Id("APP-DB-RAD-1"), "Demand Radiator One", boiler, 3500);
        var second = new Radiator(Id("APP-DB-RAD-2"), "Demand Radiator Two", boiler, 4200);
        RouteResult route = CreateRoute("Appender demand route", zone, new SupplySystem[] { first, second });
        RouteResult repeated = CreateRoute("Appender demand route", zone, new SupplySystem[] { first, second });
        IdfObject[] branchLists = route.Document["BranchList"].ToArray();
        IdfObject[] splitters = route.Document["Connector:Splitter"].ToArray();
        IdfObject[] mixers = route.Document["Connector:Mixer"].ToArray();
        IdfObject demandList = Assert.Single(branchLists, item => item.Name!.EndsWith(" Demand BranchList", StringComparison.Ordinal));
        IdfObject demandSplitter = Assert.Single(splitters, item => item.Name!.EndsWith(" Demand Splitter", StringComparison.Ordinal));
        IdfObject demandMixer = Assert.Single(mixers, item => item.Name!.EndsWith(" Demand Mixer", StringComparison.Ordinal));
        Assert.True(demandList.Count >= 5);
        Assert.Equal(demandSplitter.Count, demandMixer.Count);
        return new[]
        {
            "systems=" + string.Join("|", route.Group.Systems.Select(item => item.Id.Value)),
            "branch-list-count=" + branchLists.Length,
            "demand-branch-list=" + Snapshot(demandList),
            "demand-branch-order=" + string.Join("|", demandList.Fields.Skip(1).Select(item => item.Value)),
            "splitter-count=" + splitters.Length,
            "demand-splitter=" + Snapshot(demandSplitter),
            "mixer-count=" + mixers.Length,
            "demand-mixer=" + Snapshot(demandMixer),
            "branch-count=" + route.Document["Branch"].Count,
            "repeat-idf-identical=" + (DocumentHash(route.Document) == DocumentHash(repeated.Document)),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveEquipmentList()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-EQ-ZONE", "Appender Equipment Zone");
        var first = new ElectricRadiator(Id("APP-EQ-RAD-1"), "Equipment Radiator One", 3100);
        var second = new ElectricRadiator(Id("APP-EQ-RAD-2"), "Equipment Radiator Two", 3200);
        RouteResult route = CreateRoute("Appender equipment route", zone, new SupplySystem[] { first, second });
        IdfObject list = Assert.Single(route.Document["ZoneHVAC:EquipmentList"]);
        IdfObject connection = Assert.Single(route.Document["ZoneHVAC:EquipmentConnections"]);
        int equipmentCount = (list.Count - 2) / 6;
        Assert.Equal(2, equipmentCount);
        Assert.Equal(first.ObjectNameFor(zone), list[3]);
        Assert.Equal(second.ObjectNameFor(zone), list[9]);
        return new[]
        {
            "equipment-list-name=" + list.Name,
            "load-distribution=" + list[1],
            "equipment-count=" + equipmentCount,
            "equipment-list-fields=" + Fields(list),
            "equipment-name-order=" + list[3] + "|" + list[9],
            "equipment-type-order=" + list[2] + "|" + list[8],
            "equipment-connection=" + Snapshot(connection),
            "public-equipment-object-count=" + route.Document["ZoneHVAC:Baseboard:RadiantConvective:Electric"].Count,
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveSequentialFractions()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-SEQ-ZONE", "Appender Sequential Zone");
        var first = new ElectricRadiator(Id("APP-SEQ-HEAT-1"), "Sequential Heat One", 3300);
        var second = new ElectricRadiator(Id("APP-SEQ-HEAT-2"), "Sequential Heat Two", 3400);
        var source = new HeatPump(Id("APP-SEQ-SOURCE"), "Sequential Cooling Source", Fuel.Electricity, 3.1, 2.9);
        var cooling = new PackagedAirConditioner(Id("APP-SEQ-COOL"), "Sequential Cooling", source);
        Schedule firstAvailability = Schedule.Constant("Sequential Availability One", 0, ScheduleType.OnOff);
        Schedule secondAvailability = Schedule.Constant("Sequential Availability Two", 1, ScheduleType.OnOff);
        Schedule coolingAvailability = Schedule.Constant("Sequential Cooling Availability", 1, ScheduleType.OnOff);
        SupplySystem[] systems = { first, second, cooling };
        Schedule?[] availabilities = { firstAvailability, secondAvailability, coolingAvailability };
        RouteResult route = CreateRoute("Appender sequential route", zone, systems, availabilities);
        RouteResult repeated = CreateRoute("Appender sequential route", zone, systems, availabilities);
        IdfObject list = Assert.Single(route.Document["ZoneHVAC:EquipmentList"]);
        Assert.Equal(3, (list.Count - 2) / 6);
        string[] fractionReferences = Enumerable.Range(0, 3)
            .Select(index => list[2 + (index * 6) + 4] + "|" + list[2 + (index * 6) + 5])
            .ToArray();
        IdfObject[] fractions = route.Document
            .Where(item => item.Name?.Contains("_fraction_for_", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.NotEmpty(fractions);
        return new[]
        {
            "system-capabilities=" + string.Join("|", systems.Select(item => item.CanHeat + ":" + item.CanCool)),
            "availability-order=" + string.Join("|", route.Group.Availabilities.Select(item => item?.Name ?? "<default>")),
            "equipment-count=" + ((list.Count - 2) / 6),
            "fraction-references=" + string.Join(";", fractionReferences),
            "alloff-reference-count=" + list.Fields.Count(item => item.Value == "ALLOFF"),
            "fraction-object-count=" + fractions.Length,
            "fraction-objects=" + string.Join(";", fractions.Select(Snapshot)),
            "repeat-idf-identical=" + (DocumentHash(route.Document) == DocumentHash(repeated.Document)),
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObservePublicPostprocessingRoute()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-POST-ZONE", "Appender Postprocess Zone");
        var system = new ElectricRadiator(Id("APP-POST-RAD"), "Postprocess Radiator", 3600);
        RouteResult route = CreateRoute("Appender postprocess route", zone, new SupplySystem[] { system });
        Assert.Same(route.Group, route.Assignment.Supply);
        Assert.Equal(zone.Id, route.Assignment.ZoneId);
        return new[]
        {
            "route=" + PublicRoute,
            "supply-group-type=" + route.Group.GetType().FullName,
            "assignment-type=" + route.Assignment.GetType().FullName,
            "model-type=" + route.Model.GetType().FullName,
            "document-type=" + route.Document.GetType().FullName,
            "assignment=" + route.Assignment.ZoneId.Value + "|" + ReferenceEquals(route.Group, route.Assignment.Supply),
            "document-is-readonly-list=" + (route.Document is IReadOnlyList<IdfObject>),
            "public-idf-object-count=" + route.Document.Count,
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveZoneAirNodes()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-NODE-ZONE", "Appender Air Node Zone");
        var source = new HeatPump(Id("APP-NODE-SOURCE"), "Appender Air Node Source", Fuel.Electricity, 3.2, 3.0);
        var system = new AirHandlingUnit(Id("APP-NODE-AHU"), "Air Node AHU", source);
        RouteResult route = CreateRoute("Appender air node route", zone, new SupplySystem[] { system });
        RouteResult repeated = CreateRoute("Appender air node route", zone, new SupplySystem[] { system });
        IdfObject connection = Assert.Single(route.Document["ZoneHVAC:EquipmentConnections"]);
        IdfObject[] nodeLists = route.Document["NodeList"].ToArray();
        string[] references = { connection[2], connection[3] };
        int resolved = references.Count(reference =>
            reference.Length > 0 && nodeLists.Any(item => string.Equals(item.Name, reference, StringComparison.Ordinal)));
        Assert.Equal(references.Count(item => item.Length > 0), resolved);
        return new[]
        {
            "node-list-count=" + nodeLists.Length,
            "node-list-names=" + string.Join("|", nodeLists.Select(item => item.Name)),
            "node-lists=" + string.Join(";", nodeLists.Select(Snapshot)),
            "equipment-connections=" + Snapshot(connection),
            "inlet-reference=" + connection[2],
            "exhaust-reference=" + connection[3],
            "resolved-node-list-references=" + resolved,
            "zone-air-node=" + connection[4],
            "repeat-idf-identical=" + (DocumentHash(route.Document) == DocumentHash(repeated.Document)),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static string[] ObserveZoneTerminalUnits()
    {
        Zone zone = EnergyModelFixtureMatrixTests.CreateZone("APP-TU-ZONE", "Appender Terminal Zone");
        var source = new HeatPump(Id("APP-TU-SOURCE"), "Appender Terminal Source", Fuel.Electricity, 3.2, 3.0);
        var first = new AirHandlingUnit(Id("APP-TU-AHU-1"), "Terminal AHU One", source);
        var second = new AirHandlingUnit(Id("APP-TU-AHU-2"), "Terminal AHU Two", source);
        RouteResult route = CreateRoute("Appender terminal route", zone, new SupplySystem[] { first, second });
        IdfObject terminalList = Assert.Single(route.Document["ZoneTerminalUnitList"]);
        IdfObject[] terminalUnits = route.Document["ZoneHVAC:TerminalUnit:VariableRefrigerantFlow"].ToArray();
        Assert.Equal(2, terminalUnits.Length);
        Assert.Equal(3, terminalList.Count);
        Assert.Equal(first.ObjectNameFor(zone), terminalList[1]);
        Assert.Equal(second.ObjectNameFor(zone), terminalList[2]);
        return new[]
        {
            "shared-source=" + string.Join("|", route.Group.Sources.Select(item => item.Id.Value)),
            "terminal-list-count=" + route.Document["ZoneTerminalUnitList"].Count,
            "terminal-list=" + Snapshot(terminalList),
            "terminal-unit-count=" + terminalUnits.Length,
            "terminal-unit-order=" + string.Join("|", terminalUnits.Select(item => item.Name)),
            "system-order=" + string.Join("|", route.Group.Systems.Select(item => item.Id.Value)),
            "equipment-list=" + Snapshot(Assert.Single(route.Document["ZoneHVAC:EquipmentList"])),
            "public-idf-type-counts=" + DocumentTypeCounts(route.Document),
            "public-idf-sha256=" + DocumentHash(route.Document),
        };
    }

    private static RouteResult CreateRoute(
        string modelName,
        Zone zone,
        IReadOnlyList<SupplySystem> systems,
        IReadOnlyList<Schedule?>? availabilities = null)
    {
        var group = new SupplyGroup(systems, availabilities);
        var assignment = new ZoneHvacAssignment(zone.Id, group);
        var model = new EnergyModel(modelName, new[] { zone }, new[] { assignment });
        IdfDocument document = model.ToIdfDocument();
        return new RouteResult(zone, group, assignment, model, document);
    }

    private static string Snapshot(IdfObject item) => item.ObjectType + "|" + Fields(item);

    private static string Fields(IdfObject item) => string.Join("|", item.Fields.Select(field => field.Value));

    private static string DocumentTypeCounts(IdfDocument document) => string.Join(
        ";",
        document.GroupBy(item => item.ObjectType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + "=" + group.Count()));

    private static string DocumentHash(IdfDocument document) =>
        Sha256(Encoding.UTF8.GetBytes(IdfWriter.Write(document)));

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
                support = SupportArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
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
                    native_outcome = "public-aggregate-exception-as-pinned",
                    python_case_sha256 = fixtureCase.CaseSha256,
                    python_facts_sha256 = fixtureCase.PythonFactsSha256,
                },
            },
            scope = new
            {
                active_energyplus_process_claim = false,
                deferred_target_count = 149,
                equivalent_target_count = 0,
                exact_case_count = 6,
                exact_source_declaration_count = 174,
                exact_target_count = 24,
                exception_target_count = 24,
                fixture_repository_commit = FixtureRepositoryCommit,
                internal_native_route_claimed = false,
                public_aggregate_route_only = true,
                resolved_support_count = 1,
                standalone_postprocessor_api_claimed = false,
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
        Assert.Equal("exception", RequiredString(receipt, "classification"));
        Assert.Equal(PublicRoute, RequiredString(receipt, "native_route"));
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
        Assert.False(scope.GetProperty("internal_native_route_claimed").GetBoolean());
        Assert.False(scope.GetProperty("standalone_postprocessor_api_claimed").GetBoolean());
        Assert.True(scope.GetProperty("public_aggregate_route_only").GetBoolean());
        Assert.False(scope.GetProperty("structural_only").GetBoolean());
        Assert.Equal(24, scope.GetProperty("exact_target_count").GetInt32());
    }

    private static void ValidateArtifact(JsonElement actual, ArtifactPin expected)
    {
        Assert.Equal(expected.Path, RequiredString(actual, "path"));
        Assert.Equal(expected.Bytes, actual.GetProperty("bytes").GetInt32());
        Assert.Equal(expected.Sha256, RequiredString(actual, "sha256"));
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
        foreach (string text in EnumerateStrings(value))
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
        string AssertionId,
        string AdaptationId,
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

    private sealed record NativeObservation(string Code, string CaseId, string[] Facts, string FactsSha256);

    private sealed record NativePin(string Code, int FactCount, string FactsSha256);

    private sealed record OracleCorpus(JsonElement[] FixtureCases, TargetBinding[] Targets);

    private sealed record RouteResult(
        Zone Zone,
        SupplyGroup Group,
        ZoneHvacAssignment Assignment,
        EnergyModel Model,
        IdfDocument Document);
}
