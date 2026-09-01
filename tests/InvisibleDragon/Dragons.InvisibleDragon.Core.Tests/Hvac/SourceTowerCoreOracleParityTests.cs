#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class SourceTowerCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-hvac-source-tower-core-oracle.json";
    private const int FixtureBytes = 172_950;
    private const string FixtureSha256 =
        "sha256:60e0a2353620437049bba8420a0154e638fe86e5c915b4231793e397bb5c4fc5";
    private const string FixtureSchema =
        "dragons.python-reference.dragon-hvac-source-tower-core.v1";
    private const string FixtureRepositoryCommit = "884c2ff";
    private const string CasesSha256 =
        "sha256:3e5d0d06f45e91fbbda88b34e9c44944516a7107cf123b9052e373a347944459";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_hvac_source_tower_core_oracle.py";
    private const int GeneratorBytes = 68_752;
    private const string GeneratorSha256 =
        "sha256:e9c78f72ae62dc65f229c9766322fb53062b0f8e037bd1b62b5ac5050d8ce2d5";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_hvac_source_tower_core_oracle.py";
    private const int ValidatorBytes = 24_482;
    private const string ValidatorSha256 =
        "sha256:75762179ea1614ca74fd275accd132c1f0169f7d836b2e46e87a1a23e740f058";

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
        "sha256:894e31bb538cf8be2269a5b35b04e429ceb28b7fd881a7f6deff9d5166f360c1";
    private const string AdjacentReceiptsSha256 =
        "sha256:6e3440ca7a866008249ce603d92cb4da33cd9baf5f1b50be29e9f24e3207d769";
    private const string DependenciesSha256 =
        "sha256:f69d29212b5ce6432b0c02f356d036275ea01463a8e1974ac6f89b78854fefba";
    private const string LoadedModulesSha256 =
        "sha256:93cfad21e009eac906a4443998ad214eec82e2136ada5b7cea7888ababf30143";
    private const string RelocatedObservationsSha256 =
        "sha256:2eadd58ac936f71225de5f4181712dd6c8cebafefd12471258f719d02f193a44";
    private const string NativeRoutesSha256 =
        "sha256:6cbebb6f136b1e86a1edc9b8cab00eca578aa1bc40cc8834ce23f172d69fa3f2";
    private const string NativeClassificationSha256 =
        "sha256:25004c97fa276f70c732c54520212ca1f3979f11f84bc8bbe75f5f3e9a03291f";
    private const string NativeSourceReceiptsSha256 =
        "sha256:2b00b4902605bb3821275f5436e125bbee3fb2b5107a04680f2aef8e51ed6476";

    private const string SupportFixturePath =
        "fixtures/reference/python-0.7.0/dragon-hvac-source-system-to-idf-object-oracle.json";
    private const int SupportFixtureBytes = 3_927_710;
    private const string SupportFixtureSha256 =
        "sha256:2fbc3ad2d810dee6b3e88f8b6e8c119e8ce709abf0c534233343e486f7bf9c7f";
    private const string SupportGeneratorPath =
        "tools/python-reference/generate_dragon_hvac_source_system_to_idf_object_oracle.py";
    private const int SupportGeneratorBytes = 66_475;
    private const string SupportGeneratorSha256 =
        "sha256:f8c3a031304554ecd43381867188c29bf38c2ce0ebf4bf284c394792f7817159";
    private const string SupportParityPath =
        "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Hvac/SourceSystemToIdfObjectOracleParityTests.cs";
    private const int SupportParityBytes = 126_972;
    private const string SupportParitySha256 =
        "sha256:c26edf68f0bafc211641d19bb0dfa7f758c85a158220079cb5454a7832c18fb5";

    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Hvac.SourceTowerCoreOracleParityTests.MatchesPinnedSourceTowerCoreThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_582,
            "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SourceSystems.cs", 18_027,
            "sha256:8d302f00514af53816cec9e5ba6b80a8214921b354d86bbbc4d581ec972e026e"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/GeothermalHeatPump.cs", 1_076,
            "sha256:40fcb9c008b953cf54dfa4581c95af4073e0040fc9efcd62598e056c5b2ca80a"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/Chillers.cs", 23_777,
            "sha256:7616675c6750b32ded6edd796576b347703a88103a91dff846ca5a08c65b72be"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/CoolingTowers.cs", 19_554,
            "sha256:007145933076386fcbc44daba8a28c63d3c5467bbd687c9da87f769c969e9d07"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/IdfGenerationContext.cs", 3_801,
            "sha256:f7b6867f411575c6ce5e068df9568f76791ad7a715d41a5b4937528105f78574"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs", 22_015,
            "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/PlantLoopAssembler.cs", 10_538,
            "sha256:6a612a61c056583471cec4782ca4b64e6a94be6a177fec1ef0ee869ff3da25ee"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/CoolingPlantLoopAssembler.cs", 19_561,
            "sha256:0d571a9ad78caf2aa55913c19a86df041f12c8506b4999e7a03209d626aee594"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "dragon-hvac-source-tower-core.absorption-chiller-core", "absorption", "sha256:39d2f88b81636ed2e2195c0ec4d725ffc0a2ab198e4efbbf02de1a61c0e8d8c9", "sha256:a18e2e05f3a99a45a2c4a97c4f4ae652e1b542196cdec8a98fcc1d19c14a0505", new[] { "AbsorptionChiller", "AbsorptionChiller.__init__", "AbsorptionChiller.idf_objtypename" }),
        new("B01", "dragon-hvac-source-tower-core.boiler-core", "boiler", "sha256:47d927710da70f1cc91fd75b15aa87204bf4119ab61c4bfe83bbd1af5f1b2c29", "sha256:d557036c43e08f47d456b0dfb67967b43b03ac8748f0990e1da19a5ec4e39585", new[] { "Boiler", "Boiler.__init__", "Boiler.idf_objtypename" }),
        new("C01", "dragon-hvac-source-tower-core.chiller-core", "chiller", "sha256:cc492c1c64cbcf9c73af038611414fbbf785717f0270ad1d18efe587e309db24", "sha256:85902c31cbc27b28bb4f24ac83d351b2a39b9b19a4b832f6c08c497243c29327", new[] { "Chiller", "Chiller.__init__", "Chiller.idf_objtypename" }),
        new("D01", "dragon-hvac-source-tower-core.compressor-enum", "compressor", "sha256:b715cb79fab670b3a1f2afff587f66a0bee8962642010181a1af2eb88fc2498b", "sha256:406beba2fdc13b10784996cb350608b17d46d1ba426b303e83e4d5054a0458a3", new[] { "CompressorType", "CompressorType.RECIPROCATING", "CompressorType.SCREW", "CompressorType.TURBO" }),
        new("E01", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity", "tower-concrete", "sha256:9f084b458a40c99123b9040fcfd8319620a7b9ec2a692824d63f9385bd359c2f", "sha256:093154fe7b6adc95212d3f9c139e3148d7166d98348fef17c42497d9b072af56", new[] { "ClosedSingleSpeedCoolingTower", "ClosedSingleSpeedCoolingTower.idf_objtypename", "ClosedTwoSpeedCoolingTower", "ClosedTwoSpeedCoolingTower.idf_objtypename", "OpenSingleSpeedCoolingTower", "OpenSingleSpeedCoolingTower.idf_objtypename", "OpenTwoSpeedCoolingTower", "OpenTwoSpeedCoolingTower.idf_objtypename" }),
        new("F01", "dragon-hvac-source-tower-core.cooling-tower-core-names", "tower-core", "sha256:318e32d8e53dddced2e62bc709960d6e876aad4968da77ea5e7b8cf32b45bd54", "sha256:c647e3382c9a37af793d4c80d537c2b1188ad34a222945fd1a76e3763c7bf67a", new[] { "CoolingTower", "CoolingTower.__init__", "CoolingTower.idf_get_demandbranchlistname", "CoolingTower.idf_get_demandmixername", "CoolingTower.idf_get_demandsplittername", "CoolingTower.idf_get_loopname", "CoolingTower.idf_get_objname", "CoolingTower.idf_get_supplybranchlistname", "CoolingTower.idf_get_supplymixername", "CoolingTower.idf_get_supplysplittername", "CoolingTower.idf_objtypename" }),
        new("G01", "dragon-hvac-source-tower-core.fuel-enum", "fuel", "sha256:22859d5ee16ed6cc66a72a9b54ca6e4ad2b32a379f629e3617013179d913d98d", "sha256:8b6044fbd9f678e0a7aa00f19a7d90155dc69bf864a6bb5b52105b94a9be96d3", new[] { "Fuel", "Fuel.COAL", "Fuel.DIESEL", "Fuel.ELECTRICITY", "Fuel.FUELOILNO1", "Fuel.FUELOILNO2", "Fuel.GASOLINE", "Fuel.NATURALGAS", "Fuel.OTHERFUEL1", "Fuel.OTHERFUEL2", "Fuel.PROPANE" }),
        new("H01", "dragon-hvac-source-tower-core.geothermal-heat-pump-core", "geothermal", "sha256:0398886648e9e9c6941c727979d98ed5c761cb0d63a0ba85bccbda622a71fbaf", "sha256:e203fcb870566e744808738dda87b854705db541f585f2b2f42215122e18f630", new[] { "GeothermalHeatPump", "GeothermalHeatPump.idf_objtypename" }),
        new("I01", "dragon-hvac-source-tower-core.heat-pump-core", "heatpump", "sha256:f6ed5624e7964a76caa56a6d3f67a600a85303f7e63069df5db38ff270534602", "sha256:29ec3f4fa65c31d6f8be8b73917d519d9e66e5855e05d2e4efbb6b88b0a7a8f3", new[] { "HeatPump", "HeatPump.__init__", "HeatPump.idf_objtypename" }),
        new("J01", "dragon-hvac-source-tower-core.source-system-core-names", "source", "sha256:41a331927b73f7f359d41b101df0db22c96788b3754a1accd9a7dd273cac9eec", "sha256:ca494b84caa01187faa7a674e0217b8980cf3e6e1798f87cda981892fc3d0a17", new[] { "SourceSystem", "SourceSystem.idf_demandbranchlistname", "SourceSystem.idf_demandmixername", "SourceSystem.idf_demandsplittername", "SourceSystem.idf_loopname", "SourceSystem.idf_objname", "SourceSystem.idf_objtypename", "SourceSystem.idf_supplybranchlistname", "SourceSystem.idf_supplymixername", "SourceSystem.idf_supplysplittername", "SourceSystem.idf_terminalunitlistname" }),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        Target(641, "AbsorptionChiller", "class", "sha256:3a1dd02625e360d3868e7227c571e1209c39fde117de35d52e50f05b271bacfb", "dragon-hvac-source-tower-core-641-3a1dd026", "exception", "validated-immutable-entity-id-construction-641", "Dragons.InvisibleDragon.Hvac.AbsorptionChiller", "dragon-hvac-source-tower-core.absorption-chiller-core"),
        Target(642, "AbsorptionChiller.__init__", "function", "sha256:14ca97c14bb6467aac9221c08f2c3f7caa2072d19e2c846200bce0a2ef48efcf", "dragon-hvac-source-tower-core-642-14ca97c1", "exception", "validated-immutable-entity-id-construction-642", "Dragons.InvisibleDragon.Hvac.AbsorptionChiller(...)", "dragon-hvac-source-tower-core.absorption-chiller-core"),
        Target(643, "AbsorptionChiller.idf_objtypename", "function", "sha256:3e4cd4b3f17d040ec081cd1d6ebf3e59b539c8188a8446d4269f11366296cd29", "dragon-hvac-source-tower-core-643-3e4cd4b3", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.AbsorptionChiller.IdfObjectType", "dragon-hvac-source-tower-core.absorption-chiller-core"),
        Target(652, "Boiler", "class", "sha256:fef90e4c92d2eb17dd0a218392ca4fb689b0bade715a616f739cd10e02305d27", "dragon-hvac-source-tower-core-652-fef90e4c", "exception", "validated-immutable-entity-id-construction-652", "Dragons.InvisibleDragon.Hvac.Boiler", "dragon-hvac-source-tower-core.boiler-core"),
        Target(653, "Boiler.__init__", "function", "sha256:735f922d328beb72ea28e73c79e7d7a4fafebd548e4d0b435a35a33707e3f3dc", "dragon-hvac-source-tower-core-653-735f922d", "exception", "validated-immutable-entity-id-construction-653", "Dragons.InvisibleDragon.Hvac.Boiler(...)", "dragon-hvac-source-tower-core.boiler-core"),
        Target(654, "Boiler.idf_objtypename", "function", "sha256:b97f159095ba80d380fd70a1d735db08fdb83da939fb4273c7cda45d4c001df6", "dragon-hvac-source-tower-core-654-b97f1590", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Boiler.IdfObjectType", "dragon-hvac-source-tower-core.boiler-core"),
        Target(657, "Chiller", "class", "sha256:610bd83a3cb893234b6919415b1237cd17e7e8f5a0c9793295e174ef5b056fbd", "dragon-hvac-source-tower-core-657-610bd83a", "exception", "validated-immutable-entity-id-construction-657", "Dragons.InvisibleDragon.Hvac.Chiller", "dragon-hvac-source-tower-core.chiller-core"),
        Target(658, "Chiller.__init__", "function", "sha256:65f8043a581199d99d55e0a33ce8ace15eb535fe5dce9b14ac1c34f2b3ae710a", "dragon-hvac-source-tower-core-658-65f8043a", "exception", "validated-immutable-entity-id-construction-658", "Dragons.InvisibleDragon.Hvac.Chiller(...)", "dragon-hvac-source-tower-core.chiller-core"),
        Target(659, "Chiller.idf_objtypename", "function", "sha256:4ab109ff13801e565f0e5285a21f50ebce51520e6da0ab5ebe99007c10f0c30f", "dragon-hvac-source-tower-core-659-4ab109ff", "exception", "safe-screw-reformulated-eir-type-659", "Dragons.InvisibleDragon.Hvac.Chiller.IdfObjectType", "dragon-hvac-source-tower-core.chiller-core"),
        Target(661, "ClosedSingleSpeedCoolingTower", "class", "sha256:a4d512bcbdd707a16b374ecb473b4fd0e373d25b1f9f292f95632331a3fbe8e4", "dragon-hvac-source-tower-core-661-a4d512bc", "exception", "validated-immutable-entity-id-construction-661", "Dragons.InvisibleDragon.Hvac.ClosedSingleSpeedCoolingTower", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(662, "ClosedSingleSpeedCoolingTower.idf_objtypename", "function", "sha256:4b2767cd5505cc19de3e55c5e0c69384085c1e4335d59a9229483c28ebc6f8c9", "dragon-hvac-source-tower-core-662-4b2767cd", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ClosedSingleSpeedCoolingTower.IdfObjectType", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(664, "ClosedTwoSpeedCoolingTower", "class", "sha256:a365cc0a965a09a8443aa81dd6a6939c484604591d11c60ceb1cb22d47437e1f", "dragon-hvac-source-tower-core-664-a365cc0a", "exception", "validated-immutable-entity-id-construction-664", "Dragons.InvisibleDragon.Hvac.ClosedTwoSpeedCoolingTower", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(665, "ClosedTwoSpeedCoolingTower.idf_objtypename", "function", "sha256:648bc45c813a9dd7a30b8b208a28280ce1622b4e6bfff2d4ce18015478a194e8", "dragon-hvac-source-tower-core-665-648bc45c", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.ClosedTwoSpeedCoolingTower.IdfObjectType", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(667, "CompressorType", "class", "sha256:8785ee6da143dbc022e1a9cdb6096fa870f2d9d99804c2ab5ba18641319dfd74", "dragon-hvac-source-tower-core-667-8785ee6d", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.CompressorType", "dragon-hvac-source-tower-core.compressor-enum"),
        Target(668, "CompressorType.RECIPROCATING", "constant", "sha256:dfd51671c84116479c9ee96bf61343e6c32edc7a675ef8eb6127cb9b579c42a4", "dragon-hvac-source-tower-core-668-dfd51671", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.CompressorType.Reciprocating", "dragon-hvac-source-tower-core.compressor-enum"),
        Target(669, "CompressorType.SCREW", "constant", "sha256:2947a21386fbbd0393dfc0670795aba5ddb05be02e511da37cd0118a5d70573c", "dragon-hvac-source-tower-core-669-2947a213", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.CompressorType.Screw", "dragon-hvac-source-tower-core.compressor-enum"),
        Target(670, "CompressorType.TURBO", "constant", "sha256:5074351dd266b5054fd70ac52d608a348ca3d3bd121be79c7aeb6655f9ad1449", "dragon-hvac-source-tower-core-670-5074351d", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.CompressorType.Turbo", "dragon-hvac-source-tower-core.compressor-enum"),
        Target(673, "CoolingTower", "class", "sha256:4b07da759a3f1c822091d85129bfc1ae89eafe125e27297e5c154c04e6abd5ce", "dragon-hvac-source-tower-core-673-4b07da75", "exception", "validated-immutable-entity-id-construction-673", "Dragons.InvisibleDragon.Hvac.CoolingTower", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(674, "CoolingTower.__init__", "function", "sha256:d388c8f3e707db93101d6a5d0383a925d983e74027b50e7559c8642cbfb59002", "dragon-hvac-source-tower-core-674-d388c8f3", "exception", "validated-immutable-entity-id-construction-674", "Dragons.InvisibleDragon.Hvac.OpenSingleSpeedCoolingTower(...), OpenTwoSpeedCoolingTower(...), ClosedSingleSpeedCoolingTower(...), ClosedTwoSpeedCoolingTower(...)", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(675, "CoolingTower.idf_get_demandbranchlistname", "function", "sha256:b754f9c4ddad8ea422f8dfbac050ce72067b4dc8e161e481514609b4d4bf4ca9", "dragon-hvac-source-tower-core-675-b754f9c4", "exception", "public-context-emission-derived-name-675", "Dragons.InvisibleDragon.Hvac.CoolingTower.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(676, "CoolingTower.idf_get_demandmixername", "function", "sha256:482bcce0f0d8304f5018a64754f118fd03336f0b91cb398131759f65403f7acb", "dragon-hvac-source-tower-core-676-482bcce0", "exception", "public-context-emission-derived-name-676", "Dragons.InvisibleDragon.Hvac.CoolingTower.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(677, "CoolingTower.idf_get_demandsplittername", "function", "sha256:bc43234be143f4d9b9a13d297cf9c5413eff9793a1fb172e8c7ab727016d4cad", "dragon-hvac-source-tower-core-677-bc43234b", "exception", "public-context-emission-derived-name-677", "Dragons.InvisibleDragon.Hvac.CoolingTower.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(678, "CoolingTower.idf_get_loopname", "function", "sha256:7da2b9077a15684e7573478a6451fecffc745fae53b91ed7bbf6a6adb93027cc", "dragon-hvac-source-tower-core-678-7da2b907", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.CoolingTower.LoopNameFor(SourceSystem)", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(679, "CoolingTower.idf_get_objname", "function", "sha256:53dba42f6de8f01d61a04e5f2804912ff1f5c13e1c32b26a5e5c947e168534d9", "dragon-hvac-source-tower-core-679-53dba42f", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.CoolingTower.ObjectNameFor(SourceSystem)", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(680, "CoolingTower.idf_get_supplybranchlistname", "function", "sha256:87efb2b8ab4fa2d7d10fce0880d7a9f2d35bd25da86f2f0923ce61da30c48cd1", "dragon-hvac-source-tower-core-680-87efb2b8", "exception", "public-context-emission-derived-name-680", "Dragons.InvisibleDragon.Hvac.CoolingTower.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(681, "CoolingTower.idf_get_supplymixername", "function", "sha256:8200f079bcae2560862cb1bd0756c46c834b73bf3a170027d9522a4c6c9ec783", "dragon-hvac-source-tower-core-681-8200f079", "exception", "public-context-emission-derived-name-681", "Dragons.InvisibleDragon.Hvac.CoolingTower.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(682, "CoolingTower.idf_get_supplysplittername", "function", "sha256:c1e5599b4e23f4f4618b33da0dd19926268f0a4935d21171ea85c555e4b5a5de", "dragon-hvac-source-tower-core-682-c1e5599b", "exception", "public-context-emission-derived-name-682", "Dragons.InvisibleDragon.Hvac.CoolingTower.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(683, "CoolingTower.idf_objtypename", "function", "sha256:658520082df92fc4c03d549af63dad643ecb1962a52d7ce52cc27db4c5486918", "dragon-hvac-source-tower-core-683-65852008", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.CoolingTower.IdfObjectType", "dragon-hvac-source-tower-core.cooling-tower-core-names"),
        Target(726, "Fuel", "class", "sha256:66a9b58b66331699893ea17fec4d94a5b9cd95e109774f0d31464255e1e445f9", "dragon-hvac-source-tower-core-726-66a9b58b", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(727, "Fuel.COAL", "constant", "sha256:4d234c1e90b9d77f72b2ee53651b2ecfe7a79cc897e62c8319c1ad509a30651f", "dragon-hvac-source-tower-core-727-4d234c1e", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.Coal", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(728, "Fuel.DIESEL", "constant", "sha256:a3ee9ef5126f8a62a22e0e47b2c83bf720b19f04a523424dfb9dcebb806a515d", "dragon-hvac-source-tower-core-728-a3ee9ef5", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.Diesel", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(729, "Fuel.ELECTRICITY", "constant", "sha256:8d1b877f8cdd948498d498dea25445e3d0a335e5a3b261fcf32ef7df73b0c0de", "dragon-hvac-source-tower-core-729-8d1b877f", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.Electricity", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(730, "Fuel.FUELOILNO1", "constant", "sha256:b26b58083d2317b2160b27f4d1defa93ed10bdb68e3c51c95ee2811e7352391d", "dragon-hvac-source-tower-core-730-b26b5808", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.FuelOilNo1", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(731, "Fuel.FUELOILNO2", "constant", "sha256:1f61e381090e3233feac9d6b6b17d2982faabc0dab1594dc8b9fda344afe6a37", "dragon-hvac-source-tower-core-731-1f61e381", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.FuelOilNo2", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(732, "Fuel.GASOLINE", "constant", "sha256:7fc1afe43aa4a0e78138bb637d15285c6b96b0ca70e7b3dd49cefbed7472b8ce", "dragon-hvac-source-tower-core-732-7fc1afe4", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.Gasoline", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(733, "Fuel.NATURALGAS", "constant", "sha256:5afbce03942e7a8a61570fbc3c7f29fd97952918e48bf4eb55df3436a9d21da6", "dragon-hvac-source-tower-core-733-5afbce03", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.NaturalGas", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(734, "Fuel.OTHERFUEL1", "constant", "sha256:42a148cb292041f0a0644d3c93f87ec78a4f066afbc95014fcfbc60a43d04d5c", "dragon-hvac-source-tower-core-734-42a148cb", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.OtherFuel1", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(735, "Fuel.OTHERFUEL2", "constant", "sha256:914d30a548e2e4bcc1eec25b7223ba33fd3d836a2943a309aa0efcf71f4b1e8f", "dragon-hvac-source-tower-core-735-914d30a5", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.OtherFuel2", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(736, "Fuel.PROPANE", "constant", "sha256:dea3dce676a70c85c8b6e9b7d0383fb749a90762e6d4df1e2186f65be5f1ad80", "dragon-hvac-source-tower-core-736-dea3dce6", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.Fuel.Propane", "dragon-hvac-source-tower-core.fuel-enum"),
        Target(738, "GeothermalHeatPump", "class", "sha256:5016ceb513a88e66610818a6a65dc50fc6ecf76004648c0733d5d611c127eeaa", "dragon-hvac-source-tower-core-738-5016ceb5", "exception", "functional-native-heatpump-route-for-incomplete-abstract-upstream-738", "Dragons.InvisibleDragon.Hvac.GeothermalHeatPump", "dragon-hvac-source-tower-core.geothermal-heat-pump-core"),
        Target(739, "GeothermalHeatPump.idf_objtypename", "function", "sha256:0189ef903f1a286e2166ef62c1c4085ad4661d524fc4c309a3af4a509e3f3712", "dragon-hvac-source-tower-core-739-0189ef90", "exception", "functional-native-heatpump-route-for-incomplete-abstract-upstream-739", "Dragons.InvisibleDragon.Hvac.GeothermalHeatPump.IdfObjectType", "dragon-hvac-source-tower-core.geothermal-heat-pump-core"),
        Target(740, "HeatPump", "class", "sha256:f862c5a4167d1b8b65f47ec5b6b886f2619884c2b64b7502f6edc43b77a85012", "dragon-hvac-source-tower-core-740-f862c5a4", "exception", "validated-immutable-entity-id-construction-740", "Dragons.InvisibleDragon.Hvac.HeatPump", "dragon-hvac-source-tower-core.heat-pump-core"),
        Target(741, "HeatPump.__init__", "function", "sha256:498d6fecabd2d185628379e06c836ed6328eb0784102b1f716eecc60025ddd4d", "dragon-hvac-source-tower-core-741-498d6fec", "exception", "validated-immutable-entity-id-construction-741", "Dragons.InvisibleDragon.Hvac.HeatPump(...)", "dragon-hvac-source-tower-core.heat-pump-core"),
        Target(742, "HeatPump.idf_objtypename", "function", "sha256:66eef7768b8efdb0ddbcdca9ccca873d1391a838e43d0977df4f52fd122bc470", "dragon-hvac-source-tower-core-742-66eef776", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.HeatPump.IdfObjectType", "dragon-hvac-source-tower-core.heat-pump-core"),
        Target(744, "OpenSingleSpeedCoolingTower", "class", "sha256:219b5b22a3340c6e97289c67c0023c817823f91659f93dc6fb6dfa8f0e3c2b7d", "dragon-hvac-source-tower-core-744-219b5b22", "exception", "validated-immutable-entity-id-construction-744", "Dragons.InvisibleDragon.Hvac.OpenSingleSpeedCoolingTower", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(745, "OpenSingleSpeedCoolingTower.idf_objtypename", "function", "sha256:7ba0b88b8fd9a61d633fff97af4d3da82634c0cd451305dd8c3c62a3fd36c5d2", "dragon-hvac-source-tower-core-745-7ba0b88b", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.OpenSingleSpeedCoolingTower.IdfObjectType", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(747, "OpenTwoSpeedCoolingTower", "class", "sha256:3946064cbcde8477bd5da01eb9820d633652638a34675a9f526b439667cc7038", "dragon-hvac-source-tower-core-747-3946064c", "exception", "validated-immutable-entity-id-construction-747", "Dragons.InvisibleDragon.Hvac.OpenTwoSpeedCoolingTower", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(748, "OpenTwoSpeedCoolingTower.idf_objtypename", "function", "sha256:3692dbe4cc80479cda75dd46bb5edff91601c3c917439896c4797a27839e8098", "dragon-hvac-source-tower-core-748-3692dbe4", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.OpenTwoSpeedCoolingTower.IdfObjectType", "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity"),
        Target(777, "SourceSystem", "class", "sha256:d8fcbe86f017b859e628f37bcd2bd8af335fde9d52330c0344293184db847d6c", "dragon-hvac-source-tower-core-777-d8fcbe86", "exception", "validated-immutable-entity-id-construction-777", "Dragons.InvisibleDragon.Hvac.SourceSystem", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(778, "SourceSystem.idf_demandbranchlistname", "function", "sha256:491d3dccb08bac8d9f22634d7fee5fac5e68f913dcc8ab5a0048467407757be3", "dragon-hvac-source-tower-core-778-491d3dcc", "exception", "public-context-emission-derived-name-778", "Dragons.InvisibleDragon.Hvac.SourceSystem.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(779, "SourceSystem.idf_demandmixername", "function", "sha256:dd1f2652fc2496130f64e5281f339a30ca9427039482c20368bbc3e7e0021ead", "dragon-hvac-source-tower-core-779-dd1f2652", "exception", "public-context-emission-derived-name-779", "Dragons.InvisibleDragon.Hvac.SourceSystem.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(780, "SourceSystem.idf_demandsplittername", "function", "sha256:9fcd3f6db502c2fff61e9d3b8306f3bd71aa4e85bfd0d700c1f81568c060b510", "dragon-hvac-source-tower-core-780-9fcd3f6d", "exception", "public-context-emission-derived-name-780", "Dragons.InvisibleDragon.Hvac.SourceSystem.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(781, "SourceSystem.idf_loopname", "function", "sha256:ee8dc7f9161e86ea57abf1d785b3312eba0d18317270dbb826925fcb15e06b7a", "dragon-hvac-source-tower-core-781-ee8dc7f9", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.SourceSystem.LoopName", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(782, "SourceSystem.idf_objname", "function", "sha256:5b92cad77b3a11eb139bb45e16c9a8f379f69ab6b6e15ebf7afa3ca4c8a07818", "dragon-hvac-source-tower-core-782-5b92cad7", "exception", "concrete-native-idf-object-name-overrides-782", "Dragons.InvisibleDragon.Hvac.SourceSystem.IdfObjectName", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(783, "SourceSystem.idf_objtypename", "function", "sha256:658520082df92fc4c03d549af63dad643ecb1962a52d7ce52cc27db4c5486918", "dragon-hvac-source-tower-core-783-65852008", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Hvac.SourceSystem.IdfObjectType", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(784, "SourceSystem.idf_supplybranchlistname", "function", "sha256:6cd0d3362215336fee43d17a3c45ed28f758c423d13bb374c078f2e81a644aa6", "dragon-hvac-source-tower-core-784-6cd0d336", "exception", "public-context-emission-derived-name-784", "Dragons.InvisibleDragon.Hvac.SourceSystem.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(785, "SourceSystem.idf_supplymixername", "function", "sha256:b62cdf0b36dd2e9492495b30d20614c190bf107b3e4acd729bbcf817f0cb82fd", "dragon-hvac-source-tower-core-785-b62cdf0b", "exception", "public-context-emission-derived-name-785", "Dragons.InvisibleDragon.Hvac.SourceSystem.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(786, "SourceSystem.idf_supplysplittername", "function", "sha256:8ad08f5751bf023685adda28575f2a3f773ddc4da379348836e9159b516482de", "dragon-hvac-source-tower-core-786-8ad08f57", "exception", "public-context-emission-derived-name-786", "Dragons.InvisibleDragon.Hvac.SourceSystem.ToIdfObjects(...) -> public IdfObject fields", "dragon-hvac-source-tower-core.source-system-core-names"),
        Target(787, "SourceSystem.idf_terminalunitlistname", "function", "sha256:14bb746e8cd2994b3d52d6d3aacf9d39347352b6e62da56a0653f6415dca6e64", "dragon-hvac-source-tower-core-787-14bb746e", "exception", "public-context-emission-derived-name-787", "Dragons.InvisibleDragon.Hvac.HeatPump.TerminalUnitListName", "dragon-hvac-source-tower-core.source-system-core-names"),
    };

    private static readonly AdjacentBinding[] ExpectedAdjacent =
    {
        new(644, "AbsorptionChiller.to_idf_object", "function", "sha256:17d5fb8afe2207a9772bc47b4f5424d740b3df76301f04c9155c0fbd725af969", "exception"),
        new(655, "Boiler.to_idf_object", "function", "sha256:b63a454be07eaaee80563cbac25cd78a3fb632e462e2ea37aed7906c2967a7ae", "exception"),
        new(656, "Boiler.to_idf_object_as_generator", "function", "sha256:d239b10e14f899ec4f7d9d914e7322fd684d3cfe5096609119f32eef9dc79aa0", "exception"),
        new(660, "Chiller.to_idf_object", "function", "sha256:fc75129f85debd982652240620407bcb408a73fcf5fef197871599da771e34d3", "exception"),
        new(663, "ClosedSingleSpeedCoolingTower.to_idf_main_object", "function", "sha256:0e14065ae1ca788b3219a54f5d1ae41d7783e0dd6497667cf583e7387e0396d8", "exception"),
        new(666, "ClosedTwoSpeedCoolingTower.to_idf_main_object", "function", "sha256:30402683c6a9db760ad1727995d72c8357b93cf5704625779e5ce43b907739ae", "exception"),
        new(671, "CompressorType.__str__", "function", "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e", "out_of_scope"),
        new(672, "CompressorType.to_idf_curve_object", "function", "sha256:8ca6c2d070a534718d90fe79dff5d8a1e015593a0551a5a53ec3bf1c3e932d81", "exception"),
        new(684, "CoolingTower.to_idf_main_object", "function", "sha256:4615e08c6ec284f9bac80d2a5f25beca2b9706f4c706e0b47cf27ab35c2c5915", "exception"),
        new(685, "CoolingTower.to_idf_object", "function", "sha256:74287ab5af4712528e239034183e43122280dcf9760ebece16161e93c629c762", "exception"),
        new(737, "Fuel.__str__", "function", "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e", "out_of_scope"),
        new(743, "HeatPump.to_idf_object", "function", "sha256:b8cb28ab0ec6d2775a69548b0b5d7983afa38e0f980ec1e1835d40ccd1edacb1", "exception"),
        new(746, "OpenSingleSpeedCoolingTower.to_idf_main_object", "function", "sha256:102bccd9091484e0f915dc24010d22c22a91c69b95a17e10f44ab7d6b189e61f", "exception"),
        new(749, "OpenTwoSpeedCoolingTower.to_idf_main_object", "function", "sha256:7fd75338aa5a98323eb0d3cfeac729d921c00f95e91f7e03cfddf4b2b885e736", "exception"),
        new(788, "SourceSystem.to_idf_object", "function", "sha256:63aa5eab420418dc4467359ae79d5b1b0b59f1a0501e6e5953039b3a3adfb57b", "exception"),
    };

    // Native observations may be deliberately re-pinned only after an explicit source review.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(16, "sha256:41a023d133b465eb56d96971d43adc0d2161e3d99f85fd24f92dbb90529f6bf3"),
        new(15, "sha256:9732e608bebc852247ff3e7699d5e6c38272b7b5864f43820e64d53762cebbc7"),
        new(16, "sha256:3fb9adc51917cc8dfc848dde367278ee18fde93ecda806f5fec3983d7f3e8f98"),
        new(9, "sha256:c3ed4ff9db850b904c878515abdcfc326969ad7d20cb4a85bdbf5199b8428250"),
        new(40, "sha256:b411b92088959c2c94c31644f2246936d188233b21151baa02578e25dbc7dd9d"),
        new(17, "sha256:ef207db0152957fe02e610ef8e78ab0bf0f6fdf0b7d5945109a65f56027ab40e"),
        new(24, "sha256:9e3639040fb992b9b9b0e74c7337fe4ecadd3fd616ba604066dcf0f67b0de1b5"),
        new(13, "sha256:86a8a199d6dc23dd6dad52e6c5aa5c2788b908b1007a68b9ec11912e5f35822c"),
        new(17, "sha256:2a5b80c1060307468ef70c98b604763cdcdb9d2a5842519beb7584fc91bb8a92"),
        new(17, "sha256:b294be8308790aa313f7e8508f24d7e41085f34f09e256923698649e13230e5c"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:bc1a28f636fcaf3b10171bc76537b14b651108ac4b2064a26632908d20a4c381",
        "sha256:aeb211a1b94ef18566c45702355c1fccdfb71c3ee5d2f769f4e901939372d1fe",
        "sha256:6701b6d84e136f96768379cfa9cf2ed54172b23ab66cc20e6f092b09e6389116",
        "sha256:40c9f3a7284afe7ee2539874f1cedc713bce7b263855cd828ebbb2c5875d8881",
        "sha256:d600a36f515159e4f9a7c235e6f6beb6c786489d6d5a4bc8fcdbbdea9f550fd1",
        "sha256:87a355fe652e5746db288ec7b59ac87e9c9856ac0287e6778bbbcdac9e438b84",
        "sha256:1d2dc9a30e14dbf625851da7fc68472b444f7fcbe46c81d9f3290d291ba25503",
        "sha256:7dc0422f6167dd0d812e344abc8113b90e0cf031e97a7f8cfdf35ebfe6f7e1e4",
        "sha256:8328fcb095f2d6322b2cee28dded80423ef9d66e13a3e25e8f5d851e8dd8d645",
        "sha256:2dc061b4551171409c8d5e77bacc98f35f33c2b4bf90b091ceaf6ba83fa0ace6",
        "sha256:15574b25447ef5f455226613946d11ef5710e468a45441066e618c14f83efbfa",
        "sha256:10138bd7c1582ab59dbd57a7eedef048927bc6d8c8e0cabfc2945f8634c22b2a",
        "sha256:8a5d7f075f19e634c03565d53a34305981ca512ef9882f671241d6b39bba3918",
        "sha256:bdd0f8c25abfac28f074e6de6c7b087b33a9a3fa8821a0950b896bf78f67fb97",
        "sha256:4835afd85c7b862895d2825f20d788be8d152c7a0f700212777f5d896c7036c5",
        "sha256:56477ec89f388d4ca36d31124ef89eef2f8d404b9b6b5f0d1f0ba96750a75824",
        "sha256:21c045692817e154d0757521742ceb11ac8c13cb0832dc0a0953c99ae6999b3d",
        "sha256:0c558f1353f58b851e25bf91203622ec99428c1156326531b0b90ff3b91e1b1c",
        "sha256:9c495f90be94fe28c5ab393ab6c14b6e940284cad6cff9aa6da19dbb28bd0854",
        "sha256:62e0c7cea00e4ccb8f34e20e947ea30b6e938695651c94e1d0112fd75741fe26",
        "sha256:841286fc1045e46f606b24ec73f29ca909ff69b347e5ae98651c7c21fb625d78",
        "sha256:7c094eaf2667b09063f464e89f1601b1cf2ea657090c63a02d88bbc644294ecd",
        "sha256:03078e831f4fbec2793c859543ce0b49d8e81616e07c131285d458afd702751e",
        "sha256:9ff1b97b3efd7be5e4359d78030377acc106fe50d270890c9df0661f08977bed",
        "sha256:ce3074f2c876d468cc11c1b99cc45d96be5c3a2b416586e6728461ba8ee4ad96",
        "sha256:033734835dc1d121ecb4e7e8f1a90b0714a8c37fdf8e011ec4c37b9f2e788be9",
        "sha256:7a8fdbd84686e23ceee91486b7e63e60e4390757c9268c2c258efc6c307f907e",
        "sha256:a5dbd9b657185df3f23447e6629b7ba2663cfa3160371922735151da278c4441",
        "sha256:094a33cf546f4204ecc3669ae95d45d475efe20c970f093bfaf43831760dd834",
        "sha256:aee19b05578ce4bcccd8a6e0c79b3058cca11a87d374d9f62fa5412cfe131d69",
        "sha256:81ea36680b46976ba70f30e0110e8ff8c868a6464c3fc3f632d427d7a5b56ec4",
        "sha256:6655c225bb5b527bdf9b3323c243d843b81ad92f31f81ed4ff0c30c4f4a88160",
        "sha256:38ca9f00447153d0ea614ab4a6e4eb1909aab9e10cd35e246b57c3f2711ed691",
        "sha256:02ef27074ea6471261b16e2109d10af6c897913719eceb02b1f0d930cf75f47a",
        "sha256:5f9ae6cb39487213c351869c537a2c459b899e1d0dff428cd36b5afb971e5062",
        "sha256:6ca47cf873a95d0cbc4763d340ed40cae3384fb32d828d96fbadb882a118bf67",
        "sha256:0958d8b26fc0cb9c878de48e93cd7c3f02e6543949ed899066a3572c4945952c",
        "sha256:dc550253fb8e475b93a63bad6283f2039bf631869eb42e0c77a61fb96c826c85",
        "sha256:427aa142b3ab92939f66418c978c115bd7aee0471b358258ad257d7d6fc5b8f6",
        "sha256:cbfd7d70e3e657b538b8f9220e8b2725ab423e9621d09bcae043ee3d88bd5628",
        "sha256:3094b7d489b0b3191c5cd52d2951a5486135522c954353789017705a5af9d650",
        "sha256:64730ec0ae1de813f22d3dd75fced6b58d191869ee2f768f1e5ec1daa36adc27",
        "sha256:1fe7c3b8b9eea03f8a330ef796d156cdeac881b614a7bd2edbf4608638bbf89d",
        "sha256:90ed5c72378b5508ce56121bdc73f9f32d99c46fbb4627731ed778c94bf858f0",
        "sha256:845e17b14a41f826705416c0a4ecfc993511a704b911ceb117cadefae69c69f7",
        "sha256:1dfe8fc8a09d85f2679534d3e6bb8d69179e94a3239b586f122ee52e9ff194f6",
        "sha256:d97cb13010e4df1f7f23395dbc703763b66561e403e096afec65ce330c91c464",
        "sha256:604c14e7103c3bfbae162dcf7480662c43a1815ba06931710e1e5eccdb569947",
        "sha256:d7ca39afc098eaf98e89c946acfd95ccf4e961fbd17c71377004381d0dca921c",
        "sha256:1b813e0640336dc2dfa8f519ab141bb55e9e02d1087e22e924288a13b451e3f2",
        "sha256:1b94cd0da6569ded3e8ca3fb7fc564ed62bf95703bee621618fba0c9bbf2da9f",
        "sha256:06c21470a97110c43477e825462d7cb82548daa25bece4183e13c102c577e1e1",
        "sha256:8efb6738afac9ca75f9da37881e4cd44b95e12164b26adffc34f66dd36d42745",
        "sha256:7a0c7be6c444b39ad0693797b0b13a74c15a45e9340b3a1d66eb9b01bd316b7d",
        "sha256:b78978e5dac52d4869bc1fa012e3468d059c4db8d4baa3e0877af72117c3f5a4",
        "sha256:61a3032a682ed25f5e2226eb3550570f27e1fbf1d71f772b31d03a29fd8d6c83",
        "sha256:29bb671190876fbce6f1e81bc1eca314fdfb01136ee16dc4ddb18bc09ae47f2f",
        "sha256:f53a270a5d6a08e664067d9fbe78315239b09affdf24f46f0d815fd421801ff2",
        "sha256:c57ee020b08fa26a62fed57d7910fd3bf577041d3e052880283d8ec4b8e5a525",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:c101fe4c61f1b5fc2e5e918911b2873d08d58e1cad781ce087c9eef8086c2687", // dragon-hvac-source-tower-core-641-3a1dd026
        "sha256:bef09d3986d78f26f6577bd95dc05818ae743ff953be48043c2b1c7bcfc3b3e5", // dragon-hvac-source-tower-core-642-14ca97c1
        "sha256:9c86f627a34a0776f57d021dba83625063fc6fac1a82c6e2e4b413364bf40b6b", // dragon-hvac-source-tower-core-643-3e4cd4b3
        "sha256:ac8f16c0cf36d1452ec00298d04c249d806be88a70e74155fcc31cf0367996a4", // dragon-hvac-source-tower-core-652-fef90e4c
        "sha256:ce6fff64233e9cce7e857379122086d62ab48702c3e9563297aea29a40c96af4", // dragon-hvac-source-tower-core-653-735f922d
        "sha256:614280337651e9f3079d60f31383cca483a66e8f62f9f0cd2eac20b430e63a88", // dragon-hvac-source-tower-core-654-b97f1590
        "sha256:3d77ff757a936b4fd9ed405ee2bbf082f2b54afc525900a5c3eebf187e4fc084", // dragon-hvac-source-tower-core-657-610bd83a
        "sha256:308393e8b2311865e76018f3ee8af5b2c080ec0d4610b3884e5ac3308a97f470", // dragon-hvac-source-tower-core-658-65f8043a
        "sha256:20d114844d67123689165d2859fa3b160e7408d3af01bf16b77d746fea21a145", // dragon-hvac-source-tower-core-659-4ab109ff
        "sha256:b98ef9ebeeb47f129f55000e7043c0a093cd813ff8810f9817404165738a0d8d", // dragon-hvac-source-tower-core-661-a4d512bc
        "sha256:2a353eefc2626fe3d95bd65abafa7142ddfa0db34bc57c2a06ab957d3df51d7f", // dragon-hvac-source-tower-core-662-4b2767cd
        "sha256:69f7f2410c6b130601c15d70d73889d629b1103f3d4588894e0f5b5758c8666f", // dragon-hvac-source-tower-core-664-a365cc0a
        "sha256:f309ce93a52bb035228e6bfa38ded50d2f5b8a041c8140fbecf9b5037a7a737d", // dragon-hvac-source-tower-core-665-648bc45c
        "sha256:b3b4e3da4013e8bbe570cc7c328f9edb622bfcd2422cfe38723c83ef1467d3d6", // dragon-hvac-source-tower-core-667-8785ee6d
        "sha256:e5db69b1b6a4e6940c3c6e55493ac1184231f74aa51a2fc999039e29b16d3de9", // dragon-hvac-source-tower-core-668-dfd51671
        "sha256:7af6d2a76a0ed248eafd93af9e0cb74e6f79b52ee0b864daab2572f34d30a2bb", // dragon-hvac-source-tower-core-669-2947a213
        "sha256:081a757d3a7681fc97e3ddb89e3e1aaf7f1b8b0c784cc77c3f755192f8e79adb", // dragon-hvac-source-tower-core-670-5074351d
        "sha256:2aa2d677868645e1c1d14e45369397b4548ba6d016a8f18729b89bf70d418257", // dragon-hvac-source-tower-core-673-4b07da75
        "sha256:17c9be1c2259014f70a304181e65cf9c148e1a90f85c42ace7a74118bcfa0c8c", // dragon-hvac-source-tower-core-674-d388c8f3
        "sha256:31f448f399a5ab1dfa54d377a194e4a87515b8026882e7b0bba414bea968967c", // dragon-hvac-source-tower-core-675-b754f9c4
        "sha256:4ec2543f19799d1000717b139107c1719817cb77bd0c7dd6980d88c45ef28bd2", // dragon-hvac-source-tower-core-676-482bcce0
        "sha256:d362eb3c9037cdbb1474edeb0ca3f8a9b118107b3fd12b728bcde566c21b2d48", // dragon-hvac-source-tower-core-677-bc43234b
        "sha256:491b998b5bd80a3ba791a190098b7960980544ea82f677e8a1fcb5dc96bbf820", // dragon-hvac-source-tower-core-678-7da2b907
        "sha256:e3ccff7f7a9015e4cc41eb686e0f1a052e8300a5119bc7f5db751305ed692dab", // dragon-hvac-source-tower-core-679-53dba42f
        "sha256:59c412dcf586e4232beeaa10a77eee5a53efabff9e634e456b220881f7742c99", // dragon-hvac-source-tower-core-680-87efb2b8
        "sha256:1e06c5ccbf831992b1e63c7fb3a6e6838f394ad87f58988b3709e79f7079db70", // dragon-hvac-source-tower-core-681-8200f079
        "sha256:125ad05c035cc0dd62ee60bc36900e27a55ae92b2288445c7cfe35bbab685119", // dragon-hvac-source-tower-core-682-c1e5599b
        "sha256:50f5d1b3dc33d78a9eb900b3dad5685f99209949013bb664e2794fbd2e301717", // dragon-hvac-source-tower-core-683-65852008
        "sha256:1c3144ea560fc01409775751faf956b908010c609a2d74238c06319b0848992d", // dragon-hvac-source-tower-core-726-66a9b58b
        "sha256:ba068ff29a902ae73c1db2c47402b97fdd7e1c82f72169648754debc19dc7139", // dragon-hvac-source-tower-core-727-4d234c1e
        "sha256:9dc856adde46d71f57dc806c925743c0a9d5170a4e1b690331b1aee432f601a5", // dragon-hvac-source-tower-core-728-a3ee9ef5
        "sha256:76a3eb3adcd9ab3898d2f65cbd77177f0b659decd9fa226743981075c23f597a", // dragon-hvac-source-tower-core-729-8d1b877f
        "sha256:b06f4e35a404ddabf3c5c646d76e2ce78f1f4bc90df07cf3dff248fecfb93a69", // dragon-hvac-source-tower-core-730-b26b5808
        "sha256:fa6921038dbc67aae8195f9c7f02e1442f8f3b46325efe1ef2417a9a61d7ddd8", // dragon-hvac-source-tower-core-731-1f61e381
        "sha256:05dc23910403d315b927f15267b3edef7d60db0507594712a8b270cb6a2e3b97", // dragon-hvac-source-tower-core-732-7fc1afe4
        "sha256:f5ca6ee4a89d0a48f77a17c670455d601a6ca57a93f925e147229b046d31217c", // dragon-hvac-source-tower-core-733-5afbce03
        "sha256:cbb3728a6fe5bb70e85e80fdd0262dda2b58631461c8bc0663551ac00b7e03dc", // dragon-hvac-source-tower-core-734-42a148cb
        "sha256:327ee053a9f6d64d93e9009b72f2d16bfe4c38427e16c3830a2e3e6f4703a6a9", // dragon-hvac-source-tower-core-735-914d30a5
        "sha256:4a852faa475f1b432f2a88e1d2b52658132f0b9d5c3ca92bff03e8454f3ee276", // dragon-hvac-source-tower-core-736-dea3dce6
        "sha256:34ad9e6cd3cacdf31459c8a8f77674f3d79b7d73d6d8496c87ea45892ba4d0d8", // dragon-hvac-source-tower-core-738-5016ceb5
        "sha256:c3d6aab1451ab799787fa0bfb643427a03382ea289ab6b3e072bc31ad8cd85d0", // dragon-hvac-source-tower-core-739-0189ef90
        "sha256:2f0d791d315fed8afdc251e54edf5fcd3375f5956250bff680b9f24c27ab8b30", // dragon-hvac-source-tower-core-740-f862c5a4
        "sha256:28b50e6d72eec22392323bee01cb9e322aec9caf2a001f25ff3ba89d9ca866b7", // dragon-hvac-source-tower-core-741-498d6fec
        "sha256:476b1045fdcf0f0cc01d60a6fd0a2fe461928b0a4fc5477bc2dde1852440e99d", // dragon-hvac-source-tower-core-742-66eef776
        "sha256:1a5cd82b508c0262f1784941fd4fc1b3f7ab7e648f14b621f0ac1e14a6aa215f", // dragon-hvac-source-tower-core-744-219b5b22
        "sha256:79a9fca1396901720e60efbb873edb2e07325613e5eb1c1298e2c46bf72c49bd", // dragon-hvac-source-tower-core-745-7ba0b88b
        "sha256:669dc022c2b134fcebf5c7128b3ab392707a4b1eac8caef6c26e05c62f892079", // dragon-hvac-source-tower-core-747-3946064c
        "sha256:c1d4d83048036ce6dcead9056d651a5fe72a9dfe9f77560da537cadc3bebdafa", // dragon-hvac-source-tower-core-748-3692dbe4
        "sha256:ac25bd74ecdb59d5fcf4328cd454ab45b5901165ceb666272663495f206fde58", // dragon-hvac-source-tower-core-777-d8fcbe86
        "sha256:5df3ba5c358df0ac9c9f9071b354c8b8b606d7d666e1e208d08e0ff6fb0e388d", // dragon-hvac-source-tower-core-778-491d3dcc
        "sha256:312ad86a42c42bca15d0ba9ab41f326392049eafa2ebaf470d5d61c06a7066cf", // dragon-hvac-source-tower-core-779-dd1f2652
        "sha256:dfb6569edcb4023f31935eaa5996e127a134bde6ddedb21d072f5aad61ce72d4", // dragon-hvac-source-tower-core-780-9fcd3f6d
        "sha256:6fb1cb624b56498078e98997c3d5c36799c83dca907672359c672859f8d59f55", // dragon-hvac-source-tower-core-781-ee8dc7f9
        "sha256:35967b26b3a3769975d8ac2664cf6105bf3c5e10a8729f412fd8ae378ab7a1f8", // dragon-hvac-source-tower-core-782-5b92cad7
        "sha256:0aa7a4a3be09d1e342685579e6c204f2d75951c02150e71304d86be3ef7537a2", // dragon-hvac-source-tower-core-783-65852008
        "sha256:79c236afbb681f5712c486b2b889dd7ba75338c46ad2884dc13e5cb27d853566", // dragon-hvac-source-tower-core-784-6cd0d336
        "sha256:d8e65d523ab681d037d51fd0e0e47bf685a774f9de05894aa114207f4a64cc43", // dragon-hvac-source-tower-core-785-b62cdf0b
        "sha256:f18da24f17541001ff7da79159ff2b5bfe2a5f833f870151c29a19660ff6d585", // dragon-hvac-source-tower-core-786-8ad08f57
        "sha256:64cedafef6171d8ab44ba19f8f7089eb93cbfcd4c20b52c017b71464b17fda74", // dragon-hvac-source-tower-core-787-14bb746e
    };

    [Fact]
    public void MatchesPinnedSourceTowerCoreThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
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
            throw new Xunit.Sdk.XunitException(
                "SOURCE_TOWER_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(59, recordCount);
        Assert.Equal(59, corpus.Targets.Length);
        Assert.Equal(59, corpus.Targets.Select(item => item.AssertionId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(27, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(32, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.Equal(10, corpus.FixtureCases.Length);
        Assert.Equal(15, corpus.Adjacent.Length);
    }

    private static ExpectedTarget Target(
        int inventoryIndex,
        string symbol,
        string kind,
        string symbolHash,
        string assertionId,
        string classification,
        string adaptationId,
        string nativeRoute,
        string caseId) => new(
            inventoryIndex,
            symbol,
            kind,
            symbolHash,
            assertionId,
            classification,
            adaptationId,
            nativeRoute,
            caseId);

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertPinnedArtifact(SupportFixturePath, SupportFixtureBytes, SupportFixtureSha256);
        AssertPinnedArtifact(SupportGeneratorPath, SupportGeneratorBytes, SupportGeneratorSha256);
        AssertPinnedArtifact(SupportParityPath, SupportParityBytes, SupportParitySha256);
        foreach (ArtifactPin source in NativeSources)
        {
            AssertPinnedArtifact(source.Path, source.Bytes, source.Sha256);
        }

        Assert.True(typeof(SourceSystem).IsAbstract);
        Assert.True(typeof(CoolingTower).IsAbstract);
        Assert.False(typeof(HeatPump).IsAbstract);
        Assert.True(typeof(GeothermalHeatPump).IsSealed);
        Assert.True(typeof(Chiller).IsSealed);
        Assert.True(typeof(AbsorptionChiller).IsSealed);
        Assert.True(typeof(Boiler).IsSealed);
        Assert.True(typeof(OpenSingleSpeedCoolingTower).IsSealed);
        Assert.True(typeof(OpenTwoSpeedCoolingTower).IsSealed);
        Assert.True(typeof(ClosedSingleSpeedCoolingTower).IsSealed);
        Assert.True(typeof(ClosedTwoSpeedCoolingTower).IsSealed);

        Assert.Equal(
            new[]
            {
                "Electricity", "NaturalGas", "Propane", "FuelOilNo1", "FuelOilNo2",
                "Coal", "Diesel", "Gasoline", "OtherFuel1", "OtherFuel2",
            },
            Enum.GetNames<Fuel>());
        Assert.Equal(
            new[] { "Turbo", "Screw", "Reciprocating" },
            Enum.GetNames<CompressorType>());

        AssertPublicProperty<SourceSystem>(nameof(SourceSystem.IdfObjectType), typeof(string));
        AssertPublicProperty<SourceSystem>(nameof(SourceSystem.IdfObjectName), typeof(string));
        AssertPublicProperty<SourceSystem>(nameof(SourceSystem.LoopName), typeof(string));
        Assert.NotNull(typeof(SourceSystem).GetMethod(
            nameof(SourceSystem.ToIdfObjects),
            BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(typeof(CoolingTower).GetMethod(
            nameof(CoolingTower.ToIdfObjects),
            BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(typeof(CoolingTower).GetMethod(
            nameof(CoolingTower.ObjectNameFor),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(CoolingTower).GetMethod(
            nameof(CoolingTower.LoopNameFor),
            BindingFlags.Public | BindingFlags.Static));
        AssertPublicProperty<CoolingTower>(nameof(CoolingTower.IdfObjectType), typeof(string));
        AssertPublicProperty<HeatPump>(nameof(HeatPump.TerminalUnitListName), typeof(string));
        Assert.Single(typeof(IdfGenerationContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        AssertPublicProperty<IdfGenerationContext>(nameof(IdfGenerationContext.Options), typeof(EnergyModelIdfOptions));
    }

    private static void AssertPublicProperty<T>(string name, Type propertyType)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(typeof(T).GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(propertyType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
    }

    private static JsonDocument ReadPinnedOracle()
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

    private static OracleCorpus ValidateOracle(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(
            root,
            "adjacent_receipts",
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "fact_sha256",
            "native_review",
            "runtime",
            "schema",
            "support",
            "symbols",
            "target_receipts",
            "upstream");
        Assert.Equal(FixtureSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));

        ValidateRuntime(root.GetProperty("runtime"));
        ValidateSupport(root.GetProperty("support"));
        ValidateUpstream(root.GetProperty("upstream"));
        ValidateNativeReview(root.GetProperty("native_review"));
        JsonElement[] fixtureCases = ValidateCases(root);
        (TargetBinding[] targets, AdjacentBinding[] adjacent) = ValidateTargets(root);
        ValidateConsumerContract(root.GetProperty("consumer_contract"), targets, adjacent);
        return new OracleCorpus(fixtureCases, targets, adjacent);
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "dependencies_sha256",
            "implementation",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal(DependenciesSha256, RequiredString(runtime, "dependencies_sha256"));
        Assert.Equal(DependenciesSha256, CanonicalSha256(runtime.GetProperty("dependencies")));
        AssertKeys(
            runtime.GetProperty("dependencies"),
            "colorama",
            "et_xmlfile",
            "numpy",
            "openpyxl",
            "pandas",
            "python-dateutil",
            "pytz",
            "six",
            "tqdm",
            "tzdata");
    }

    private static void ValidateSupport(JsonElement support)
    {
        AssertKeys(
            support,
            "case_count",
            "cases_sha256",
            "fixture",
            "generator",
            "resolved_adjacent_symbols",
            "schema");
        Assert.Equal(20, support.GetProperty("case_count").GetInt32());
        Assert.Equal(
            "sha256:755e2115db65a100fe1b4249c4b4507719e5083aa2ea22939955a7aae53c5c07",
            RequiredString(support, "cases_sha256"));
        Assert.Equal(
            "dragons.python-reference.dragon-hvac-source-system-to-idf-object.v1",
            RequiredString(support, "schema"));
        AssertArtifact(
            support.GetProperty("fixture"),
            SupportFixturePath,
            SupportFixtureBytes,
            SupportFixtureSha256);
        AssertArtifact(
            support.GetProperty("generator"),
            SupportGeneratorPath,
            SupportGeneratorBytes,
            SupportGeneratorSha256);
        AssertStringArray(
            support.GetProperty("resolved_adjacent_symbols"),
            ExpectedAdjacent
                .Where(item => item.Classification == "exception")
                .Select(item => item.Symbol));
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
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement isolated = upstream.GetProperty("isolated_import");
        AssertKeys(
            isolated,
            "loaded_local_modules",
            "loaded_local_modules_sha256",
            "relocated_observations_sha256",
            "relocated_source_copy",
            "source_location_count");
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal(
            "two-byte-identical-repository-temp-copies",
            RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(LoadedModulesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(LoadedModulesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        Assert.Equal(
            RelocatedObservationsSha256,
            RequiredString(isolated, "relocated_observations_sha256"));
        Assert.Equal(12, isolated.GetProperty("loaded_local_modules").GetArrayLength());
    }

    private static void ValidateNativeReview(JsonElement review)
    {
        AssertKeys(
            review,
            "classification_sha256",
            "public_production_routes_only",
            "python_executes_native_runtime",
            "routes_sha256",
            "source_receipts",
            "source_receipts_sha256");
        Assert.True(review.GetProperty("public_production_routes_only").GetBoolean());
        Assert.False(review.GetProperty("python_executes_native_runtime").GetBoolean());
        Assert.Equal(NativeRoutesSha256, RequiredString(review, "routes_sha256"));
        Assert.Equal(NativeClassificationSha256, RequiredString(review, "classification_sha256"));
        Assert.Equal(NativeSourceReceiptsSha256, RequiredString(review, "source_receipts_sha256"));
        Assert.Equal(
            NativeSourceReceiptsSha256,
            CanonicalSha256(review.GetProperty("source_receipts")));
        AssertArtifactArray(review.GetProperty("source_receipts"), NativeSources.Take(5).ToArray());
    }

    private static JsonElement[] ValidateCases(JsonElement root)
    {
        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        AssertKeys(root.GetProperty("case_sha256"), Cases.Select(item => item.CaseId).ToArray());
        AssertKeys(root.GetProperty("fact_sha256"), Cases.Select(item => item.CaseId).ToArray());
        for (int index = 0; index < fixtureCases.Length; index++)
        {
            JsonElement item = fixtureCases[index];
            CaseBinding expected = Cases[index];
            AssertKeys(item, "code", "id", "python", "subfamily", "target_symbols");
            Assert.Equal(expected.Code, RequiredString(item, "code"));
            Assert.Equal(expected.CaseId, RequiredString(item, "id"));
            Assert.Equal(expected.Subfamily, RequiredString(item, "subfamily"));
            AssertStringArray(item.GetProperty("target_symbols"), expected.TargetSymbols);
            JsonElement python = item.GetProperty("python");
            AssertKeys(python, "facts", "facts_sha256", "outcome");
            Assert.Equal("observed", RequiredString(python, "outcome"));
            Assert.Equal(expected.FactsSha256, RequiredString(python, "facts_sha256"));
            Assert.Equal(expected.FactsSha256, CanonicalSha256(python.GetProperty("facts")));
            Assert.Equal(
                expected.FactsSha256,
                RequiredString(root.GetProperty("fact_sha256"), expected.CaseId));
            Assert.Equal(
                expected.CaseSha256,
                RequiredString(root.GetProperty("case_sha256"), expected.CaseId));
            Assert.Equal(expected.CaseSha256, CanonicalSha256(item));
        }

        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            fixtureCases.SelectMany(item => ReadStringArray(item.GetProperty("target_symbols")))
                .OrderBy(item => item, StringComparer.Ordinal));
        return fixtureCases;
    }

    private static (TargetBinding[] Targets, AdjacentBinding[] Adjacent) ValidateTargets(JsonElement root)
    {
        JsonElement[] descriptors = root.GetProperty("symbols").EnumerateArray().ToArray();
        JsonElement[] receipts = root.GetProperty("target_receipts").EnumerateArray().ToArray();
        JsonElement[] adjacentReceipts = root.GetProperty("adjacent_receipts").EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, descriptors.Length);
        Assert.Equal(ExpectedTargets.Length, receipts.Length);
        Assert.Equal(ExpectedAdjacent.Length, adjacentReceipts.Length);
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(root.GetProperty("target_receipts")));
        Assert.Equal(AdjacentReceiptsSha256, CanonicalSha256(root.GetProperty("adjacent_receipts")));

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        Assert.Equal(InventoryBytes, inventoryBytes.Length);
        Assert.Equal(InventoryFileSha256, Sha256(inventoryBytes));
        using JsonDocument inventoryDocument = JsonDocument.Parse(inventoryBytes);
        AssertUniqueObjectKeysRecursive(inventoryDocument.RootElement);
        Assert.Equal(
            InventoryContentSha256,
            RequiredString(inventoryDocument.RootElement, "content_sha256"));
        Assert.Equal(
            UpstreamCommit,
            RequiredString(inventoryDocument.RootElement, "upstream_commit"));
        JsonElement inventorySymbols = inventoryDocument.RootElement.GetProperty("symbols");

        var targets = new TargetBinding[ExpectedTargets.Length];
        for (int index = 0; index < ExpectedTargets.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement descriptor = descriptors[index];
            JsonElement receipt = receipts[index];
            JsonElement inventorySymbol = inventorySymbols[expected.InventoryIndex];
            AssertProjection(descriptor, expected, includeIndex: false);
            AssertProjection(receipt, expected, includeIndex: true);
            AssertProjection(inventorySymbol, expected, includeIndex: false, exactKeys: false);
            foreach (string hashName in new[] { "symbol_hash", "signature_hash", "body_hash" })
            {
                Assert.Equal(RequiredString(inventorySymbol, hashName), RequiredString(receipt, hashName));
                Assert.Equal(RequiredString(receipt, hashName), RequiredString(descriptor, hashName));
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

        var adjacent = new AdjacentBinding[ExpectedAdjacent.Length];
        for (int index = 0; index < ExpectedAdjacent.Length; index++)
        {
            AdjacentBinding expected = ExpectedAdjacent[index];
            JsonElement receipt = adjacentReceipts[index];
            JsonElement inventorySymbol = inventorySymbols[expected.InventoryIndex];
            AssertInventoryReceipt(receipt, expected, includeIndex: true);
            AssertInventoryReceipt(inventorySymbol, expected, includeIndex: false, exactKeys: false);
            foreach (string hashName in new[] { "symbol_hash", "signature_hash", "body_hash" })
            {
                Assert.Equal(RequiredString(inventorySymbol, hashName), RequiredString(receipt, hashName));
            }

            adjacent[index] = expected;
        }

        return (targets, adjacent);
    }

    private static void AssertProjection(
        JsonElement item,
        ExpectedTarget expected,
        bool includeIndex,
        bool exactKeys = true)
    {
        if (exactKeys)
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
        Assert.Equal(expected.SymbolHash, RequiredString(item, "symbol_hash"));
        Assert.Equal(UpstreamPath, RequiredString(item, "path"));
        AssertSha256(RequiredString(item, "signature_hash"));
        AssertSha256(RequiredString(item, "body_hash"));
    }

    private static void AssertInventoryReceipt(
        JsonElement item,
        AdjacentBinding expected,
        bool includeIndex,
        bool exactKeys = true)
    {
        if (exactKeys)
        {
            AssertKeys(
                item,
                "body_hash",
                "inventory_index",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
        }

        if (includeIndex)
        {
            Assert.Equal(expected.InventoryIndex, item.GetProperty("inventory_index").GetInt32());
        }

        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
        Assert.Equal(expected.Kind, RequiredString(item, "kind"));
        Assert.Equal(expected.SymbolHash, RequiredString(item, "symbol_hash"));
        Assert.Equal(UpstreamPath, RequiredString(item, "path"));
    }

    private static void ValidateConsumerContract(
        JsonElement contract,
        IReadOnlyList<TargetBinding> targets,
        IReadOnlyList<AdjacentBinding> adjacent)
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
        Assert.Equal(10, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(27, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(32, counts.GetProperty("exception").GetInt32());

        string[] targetSymbols = ExpectedTargets.Select(item => item.Symbol).ToArray();
        JsonElement assertionIds = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement routes = contract.GetProperty("native_routes");
        JsonElement expectations = contract.GetProperty("expectations");
        JsonElement signatures = contract.GetProperty("runtime_signatures");
        JsonElement coverage = contract.GetProperty("coverage_by_symbol");
        AssertKeys(assertionIds, targetSymbols);
        AssertKeys(classifications, targetSymbols);
        AssertKeys(routes, targetSymbols);
        AssertKeys(expectations, targetSymbols);
        AssertKeys(signatures, targetSymbols);
        AssertKeys(coverage, targetSymbols);
        AssertKeys(
            adaptations,
            ExpectedTargets
                .Where(item => item.Classification == "exception")
                .Select(item => item.Symbol)
                .ToArray());
        Assert.Equal(NativeRoutesSha256, CanonicalSha256(routes));
        Assert.Equal(NativeClassificationSha256, CanonicalSha256(classifications));

        foreach (TargetBinding target in targets)
        {
            Assert.Equal(target.AssertionId, RequiredString(assertionIds, target.Symbol));
            Assert.Equal(target.Classification, RequiredString(classifications, target.Symbol));
            Assert.Equal(target.NativeRoute, RequiredString(routes, target.Symbol));
            Assert.Equal(target.CaseId, RequiredString(coverage, target.Symbol));
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
            else
            {
                Assert.Equal("not_applicable", target.AdaptationId);
            }

            Assert.StartsWith("Dragons.InvisibleDragon", target.NativeRoute, StringComparison.Ordinal);
            Assert.DoesNotContain(".Internal", target.NativeRoute, StringComparison.Ordinal);
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "adjacent_classifications",
            "adjacent_count",
            "adjacent_indices",
            "deferred_count",
            "deferred_indices",
            "exact_one_case_target_partition",
            "full_hvac_source_partition",
            "full_source_tower_family_closure",
            "source_declaration_count",
            "source_tower_family_count",
            "target_count",
            "target_indices",
            "target_symbols");
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_hvac_source_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_source_tower_family_closure").GetBoolean());
        Assert.Equal(174, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(74, closure.GetProperty("source_tower_family_count").GetInt32());
        Assert.Equal(59, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(15, closure.GetProperty("adjacent_count").GetInt32());
        Assert.Equal(100, closure.GetProperty("deferred_count").GetInt32());
        Assert.Equal(targets.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("target_indices")));
        Assert.Equal(adjacent.Select(item => item.InventoryIndex), ReadIntArray(closure.GetProperty("adjacent_indices")));
        AssertStringArray(closure.GetProperty("target_symbols"), targetSymbols);
        JsonElement adjacentClassifications = closure.GetProperty("adjacent_classifications");
        AssertKeys(adjacentClassifications, adjacent.Select(item => item.Symbol).ToArray());
        foreach (AdjacentBinding item in adjacent)
        {
            Assert.Equal(item.Classification, RequiredString(adjacentClassifications, item.Symbol));
        }

        int[] deferred = ReadIntArray(closure.GetProperty("deferred_indices"));
        Assert.Equal(100, deferred.Length);
        Assert.Equal(
            Enumerable.Range(641, 174),
            targets.Select(item => item.InventoryIndex)
                .Concat(adjacent.Select(item => item.InventoryIndex))
                .Concat(deferred)
                .OrderBy(item => item));

        JsonElement evidence = contract.GetProperty("evidence_contract");
        AssertKeys(
            evidence,
            "active_energyplus_process_claim",
            "exact_cpython_behavior_oracle",
            "expected_receipt_count",
            "native_runtime_executed_by_python_oracle",
            "path_independent_relocated_import",
            "resolved_idf_behavior_reused_from_support",
            "target_coverage_complete");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.True(evidence.GetProperty("exact_cpython_behavior_oracle").GetBoolean());
        Assert.Equal(59, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(evidence.GetProperty("resolved_idf_behavior_reused_from_support").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
    }

    private static NativeObservation ObserveNativeCase(CaseBinding fixtureCase)
    {
        IEnumerable<string> rawFacts = fixtureCase.Code switch
        {
            "A01" => ObserveAbsorptionChiller(),
            "B01" => ObserveBoiler(),
            "C01" => ObserveChiller(),
            "D01" => ObserveCompressorEnum(),
            "E01" => ObserveConcreteCoolingTowers(),
            "F01" => ObserveCoolingTowerNames(),
            "G01" => ObserveFuelEnum(),
            "H01" => ObserveGeothermalHeatPump(),
            "I01" => ObserveHeatPump(),
            "J01" => ObserveSourceSystemNames(),
            _ => throw new InvalidOperationException($"Unknown case code '{fixtureCase.Code}'."),
        };
        string[] facts = rawFacts.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        return new NativeObservation(
            fixtureCase.Code,
            fixtureCase.CaseId,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static IEnumerable<string> ObserveAbsorptionChiller()
    {
        var boiler = new Boiler(Id("A-BOILER"), "Generator Boiler", Fuel.NaturalGas, 0.88);
        var tower = new OpenSingleSpeedCoolingTower(Id("A-TOWER"), "Absorber Tower");
        var source = new AbsorptionChiller(
            Id("A-ABSORBER"),
            "Absorber",
            0.72,
            boiler,
            tower);
        Assert.Same(boiler, source.HeatSource);
        Assert.Same(tower, source.CoolingTower);
        Assert.Equal("Chiller:Absorption", source.IdfObjectType);
        Assert.Equal("AbsorptionChiller_named_Absorber", source.IdfObjectName);
        Assert.Null(source.NominalCapacityWatts);
        Assert.Equal(0.72, source.ThermalCoefficientOfPerformance);
        Assert.Equal(0.9, source.PumpMotorEfficiency);
        Assert.Equal(6, source.SetpointTemperatureCelsius);
        Assert.Equal(Fuel.NaturalGas, source.GeneratorFuel);

        IReadOnlyList<IdfObject> modern = source.ToIdfObjects(Context(legacy: false));
        IReadOnlyList<IdfObject> legacy = source.ToIdfObjects(Context(legacy: true));
        Assert.Contains(modern, item => item.ObjectType == source.IdfObjectType);
        Assert.Contains(legacy, item => item.ObjectType == source.IdfObjectType);
        Assert.Contains(modern, item => item.ObjectType == tower.IdfObjectType);
        Assert.Contains(legacy, item => item.ObjectType == tower.IdfObjectType);

        yield return $"type={source.GetType().FullName}";
        yield return $"name={source.Name}";
        yield return $"idf-object-type={source.IdfObjectType}";
        yield return $"idf-object-name={source.IdfObjectName}";
        yield return $"loop-name={source.LoopName}";
        yield return $"cop={Format(source.ThermalCoefficientOfPerformance)}";
        yield return $"capacity={Optional(source.NominalCapacityWatts)}";
        yield return $"pump-efficiency={Format(source.PumpMotorEfficiency)}";
        yield return $"setpoint={Format(source.SetpointTemperatureCelsius)}";
        yield return $"generator-fuel={source.GeneratorFuel}";
        yield return $"heat-source-identity={ReferenceEquals(boiler, source.HeatSource)}";
        yield return $"tower-identity={ReferenceEquals(tower, source.CoolingTower)}";
        yield return $"modern={ObjectSetFact(modern)}";
        yield return $"legacy={ObjectSetFact(legacy)}";
        yield return "invalid-cop=" + CaptureArgument(() => _ = new AbsorptionChiller(
            Id("A-BAD-COP"), "Bad", 0, boiler,
            new OpenSingleSpeedCoolingTower(Id("A-BAD-TOWER"), "Bad tower")));
        yield return "shared-id=" + CaptureArgument(() => _ = new AbsorptionChiller(
            Id("A-BOILER"), "Bad", 0.7, boiler,
            new OpenSingleSpeedCoolingTower(Id("A-OTHER-TOWER"), "Other tower")));
    }

    private static IEnumerable<string> ObserveBoiler()
    {
        var source = new Boiler(
            Id("B-BOILER"),
            "Boiler",
            Fuel.Propane,
            nominalThermalEfficiency: 0.88);
        Assert.Equal(Fuel.Propane, source.Fuel);
        Assert.Equal("Boiler:HotWater", source.IdfObjectType);
        Assert.Equal("Boiler_named_Boiler", source.IdfObjectName);
        Assert.Null(source.NominalCapacityWatts);
        Assert.Equal(0.88, source.NominalThermalEfficiency);
        Assert.Equal(0.9, source.PumpMotorEfficiency);
        Assert.Equal(60, source.SetpointTemperatureCelsius);

        IReadOnlyList<IdfObject> modern = source.ToIdfObjects(Context(legacy: false));
        IReadOnlyList<IdfObject> legacy = source.ToIdfObjects(Context(legacy: true));
        Assert.Contains(modern, item => item.ObjectType == source.IdfObjectType);
        Assert.Contains(legacy, item => item.ObjectType == source.IdfObjectType);

        yield return $"type={source.GetType().FullName}";
        yield return $"name={source.Name}";
        yield return $"fuel={source.Fuel}";
        yield return $"efficiency={Format(source.NominalThermalEfficiency)}";
        yield return $"capacity={Optional(source.NominalCapacityWatts)}";
        yield return $"pump-efficiency={Format(source.PumpMotorEfficiency)}";
        yield return $"setpoint={Format(source.SetpointTemperatureCelsius)}";
        yield return $"idf-object-type={source.IdfObjectType}";
        yield return $"idf-object-name={source.IdfObjectName}";
        yield return $"loop-name={source.LoopName}";
        yield return $"modern={ObjectSetFact(modern)}";
        yield return $"legacy={ObjectSetFact(legacy)}";
        yield return "invalid-efficiency=" + CaptureArgument(() => _ = new Boiler(
            Id("B-BAD-EFF"), "Bad", Fuel.Propane, 0));
        yield return "invalid-fuel=" + CaptureArgument(() => _ = new Boiler(
            Id("B-BAD-FUEL"), "Bad", (Fuel)999));
        yield return "invalid-name=" + CaptureArgument(() => _ = new Boiler(
            Id("B-BAD-NAME"), " ", Fuel.Propane));
    }

    private static IEnumerable<string> ObserveChiller()
    {
        var tower = new ClosedTwoSpeedCoolingTower(Id("C-TOWER"), "Chiller Tower");
        var turbo = new Chiller(
            Id("C-TURBO"),
            "Chiller",
            9,
            CompressorType.Turbo,
            tower);
        var screw = new Chiller(
            Id("C-SCREW"),
            "Screw",
            5.5,
            CompressorType.Screw,
            new ClosedSingleSpeedCoolingTower(Id("C-SCREW-TOWER"), "Screw Tower"),
            nominalCapacityWatts: 222_000);
        Assert.Same(tower, turbo.CoolingTower);
        Assert.Equal("Chiller:Electric:EIR", turbo.IdfObjectType);
        Assert.Equal("Chiller:Electric:ReformulatedEIR", screw.IdfObjectType);
        Assert.Null(turbo.NominalCapacityWatts);
        Assert.Equal(9, turbo.ReferenceCoefficientOfPerformance);
        Assert.Equal(CompressorType.Turbo, turbo.Compressor);

        IReadOnlyList<IdfObject> modern = turbo.ToIdfObjects(Context(legacy: false));
        IReadOnlyList<IdfObject> legacy = turbo.ToIdfObjects(Context(legacy: true));
        IReadOnlyList<IdfObject> screwModern = screw.ToIdfObjects(Context(legacy: false));
        Assert.Contains(modern, item => item.ObjectType == turbo.IdfObjectType);
        Assert.Contains(legacy, item => item.ObjectType == turbo.IdfObjectType);
        Assert.Contains(screwModern, item => item.ObjectType == screw.IdfObjectType);

        yield return $"type={turbo.GetType().FullName}";
        yield return $"name={turbo.Name}";
        yield return $"compressor={turbo.Compressor}";
        yield return $"cop={Format(turbo.ReferenceCoefficientOfPerformance)}";
        yield return $"capacity={Optional(turbo.NominalCapacityWatts)}";
        yield return $"pump-efficiency={Format(turbo.PumpMotorEfficiency)}";
        yield return $"setpoint={Format(turbo.SetpointTemperatureCelsius)}";
        yield return $"tower-identity={ReferenceEquals(tower, turbo.CoolingTower)}";
        yield return $"turbo-type={turbo.IdfObjectType}";
        yield return $"screw-type={screw.IdfObjectType}";
        yield return $"modern={ObjectSetFact(modern)}";
        yield return $"legacy={ObjectSetFact(legacy)}";
        yield return $"screw-modern={ObjectSetFact(screwModern)}";
        yield return "invalid-compressor=" + CaptureArgument(() => _ = new Chiller(
            Id("C-BAD-COMP"), "Bad", 3, (CompressorType)999,
            new ClosedSingleSpeedCoolingTower(Id("C-BAD-COMP-TOWER"), "Bad tower")));
        yield return "invalid-cop=" + CaptureArgument(() => _ = new Chiller(
            Id("C-BAD-COP"), "Bad", -1, CompressorType.Turbo,
            new ClosedSingleSpeedCoolingTower(Id("C-BAD-COP-TOWER"), "Bad tower")));
        yield return "shared-id=" + CaptureArgument(() => _ = new Chiller(
            Id("C-SHARED"), "Bad", 3, CompressorType.Turbo,
            new ClosedSingleSpeedCoolingTower(Id("C-SHARED"), "Shared")));
    }

    private static IEnumerable<string> ObserveCompressorEnum()
    {
        CompressorType[] values = Enum.GetValues<CompressorType>();
        Assert.Equal(3, values.Length);
        Assert.Equal(3, values.Distinct().Count());
        Assert.True(Enum.TryParse("Turbo", out CompressorType turbo));
        Assert.Equal(CompressorType.Turbo, turbo);
        Assert.False(Enum.TryParse("not-declared", out CompressorType _));
        foreach (CompressorType value in values)
        {
            yield return $"member={value}:{Convert.ToInt32(value, CultureInfo.InvariantCulture)}";
            yield return $"defined={value}:{Enum.IsDefined(value)}";
        }

        yield return $"count={values.Length}";
        yield return $"unique={values.Distinct().Count()}";
        yield return "invalid-defined=" + Enum.IsDefined((CompressorType)999).ToString(CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> ObserveConcreteCoolingTowers()
    {
        var families = new TowerFamily[]
        {
            new("ClosedSingle", "FluidCooler:SingleSpeed", 5,
                (id, capacity) => new ClosedSingleSpeedCoolingTower(Id(id), "Tower", capacity)),
            new("ClosedTwo", "FluidCooler:TwoSpeed", 7,
                (id, capacity) => new ClosedTwoSpeedCoolingTower(Id(id), "Tower", capacity)),
            new("OpenSingle", "CoolingTower:SingleSpeed", 13,
                (id, capacity) => new OpenSingleSpeedCoolingTower(Id(id), "Tower", capacity)),
            new("OpenTwo", "CoolingTower:TwoSpeed", 19,
                (id, capacity) => new OpenTwoSpeedCoolingTower(Id(id), "Tower", capacity)),
        };
        foreach (TowerFamily family in families)
        {
            foreach ((string branch, double? towerCapacity, double? sourceCapacity, double expected) in
                     new[]
                     {
                         ("tower-capacity", (double?)111_000, (double?)222_000, 111_000d),
                         ("source-capacity", null, (double?)222_000, 222_000d),
                         ("fallback-capacity", null, null, 1_000_000d),
                     })
            {
                string key = $"E-{family.Code}-{branch}";
                CoolingTower tower = family.Factory(key + "-T", towerCapacity);
                var source = new Chiller(
                    Id(key + "-C"),
                    "Capacity Source",
                    4.2,
                    CompressorType.Turbo,
                    tower,
                    sourceCapacity);
                IReadOnlyList<IdfObject> first = tower.ToIdfObjects(Context(legacy: false), source);
                IReadOnlyList<IdfObject> second = tower.ToIdfObjects(Context(legacy: false), source);
                IdfObject main = Assert.Single(first, item => item.ObjectType == family.ObjectType);
                Assert.NotSame(first, second);
                Assert.NotSame(first[0], second[0]);
                Assert.Equal(family.ObjectType, tower.IdfObjectType);
                Assert.True(main.Fields.Count > family.CapacityIndex);
                Assert.Equal(IdfGenerationContext.Format(expected), main.Fields[family.CapacityIndex].Value);
                Assert.Equal(towerCapacity, tower.NominalCapacityWatts);
                Assert.Equal(sourceCapacity, source.NominalCapacityWatts);
                yield return $"{family.Code}:{branch}:type={tower.IdfObjectType}";
                yield return $"{family.Code}:{branch}:capacity={main.Fields[family.CapacityIndex].Value}";
                yield return $"{family.Code}:{branch}:objects={ObjectSetFact(first)}";
            }

            CoolingTower legacyTower = family.Factory($"E-{family.Code}-LEGACY-T", 111_000);
            var legacySource = new Chiller(
                Id($"E-{family.Code}-LEGACY-C"),
                "Legacy Source",
                4.2,
                CompressorType.Turbo,
                legacyTower,
                222_000);
            IReadOnlyList<IdfObject> legacy = legacyTower.ToIdfObjects(Context(legacy: true), legacySource);
            Assert.Contains(legacy, item => item.ObjectType == family.ObjectType);
            yield return $"{family.Code}:legacy={ObjectSetFact(legacy)}";
        }
    }

    private static IEnumerable<string> ObserveCoolingTowerNames()
    {
        var tower = new OpenSingleSpeedCoolingTower(Id("F-TOWER"), "Unused Tower Name");
        var source = new Chiller(
            Id("F-CHILLER"),
            "Name Context",
            4.1,
            CompressorType.Turbo,
            tower);
        string objectName = CoolingTower.ObjectNameFor(source);
        string loopName = CoolingTower.LoopNameFor(source);
        Assert.Equal("CT_for_Chiller_named_Name Context", objectName);
        Assert.Equal("Loop_for_CT_for_Chiller_named_Name Context", loopName);
        Assert.DoesNotContain(tower.Name, objectName, StringComparison.Ordinal);
        IReadOnlyList<IdfObject> modern = tower.ToIdfObjects(Context(legacy: false), source);
        IReadOnlyList<IdfObject> legacy = tower.ToIdfObjects(Context(legacy: true), source);
        string[] values = modern.SelectMany(item => item.Fields).Select(item => item.Value).ToArray();
        foreach (string expected in new[]
                 {
                     objectName,
                     loopName,
                     $"{loopName} Demand BranchList",
                     $"{loopName} Demand Mixer",
                     $"{loopName} Demand Splitter",
                     $"{loopName} Supply BranchList",
                     $"{loopName} Supply Mixer",
                     $"{loopName} Supply Splitter",
                 })
        {
            Assert.Contains(expected, values);
            yield return "emitted-name=" + expected;
        }

        yield return "abstract=" + typeof(CoolingTower).IsAbstract.ToString(CultureInfo.InvariantCulture);
        yield return "object-type=" + tower.IdfObjectType;
        yield return "object-name=" + objectName;
        yield return "loop-name=" + loopName;
        yield return "tower-name-not-context=" + (!objectName.Contains(tower.Name, StringComparison.Ordinal));
        yield return "modern=" + ObjectSetFact(modern);
        yield return "legacy=" + ObjectSetFact(legacy);
        yield return "null-source=" + CaptureArgument(() => CoolingTower.ObjectNameFor(null!));
        yield return "wrong-source=" + CaptureArgument(() => tower.ToIdfObjects(
            Context(legacy: false),
            new Boiler(Id("F-WRONG"), "Wrong", Fuel.Electricity)));
    }

    private static IEnumerable<string> ObserveFuelEnum()
    {
        Fuel[] values = Enum.GetValues<Fuel>();
        string[] expected =
        {
            "Electricity", "NaturalGas", "Propane", "FuelOilNo1", "FuelOilNo2",
            "Coal", "Diesel", "Gasoline", "OtherFuel1", "OtherFuel2",
        };
        Assert.Equal(expected, values.Select(item => item.ToString()));
        Assert.Equal(10, values.Distinct().Count());
        foreach (Fuel value in values)
        {
            Assert.True(Enum.TryParse(value.ToString(), out Fuel parsed));
            Assert.Equal(value, parsed);
            yield return $"member={value}:{Convert.ToInt32(value, CultureInfo.InvariantCulture)}";
            yield return $"roundtrip={value}:{parsed == value}";
        }

        yield return $"count={values.Length}";
        yield return $"unique={values.Distinct().Count()}";
        yield return "invalid-defined=" + Enum.IsDefined((Fuel)999).ToString(CultureInfo.InvariantCulture);
        yield return "invalid-parse=" + Enum.TryParse("not-declared", out Fuel _).ToString(CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> ObserveGeothermalHeatPump()
    {
        var source = new GeothermalHeatPump(
            Id("H-GEO"),
            "Ground Source",
            Fuel.Electricity,
            4.4,
            4.1,
            44_000,
            41_000);
        Assert.IsAssignableFrom<HeatPump>(source);
        Assert.Equal("AirConditioner:VariableRefrigerantFlow", source.IdfObjectType);
        Assert.Equal("HeatPump_named_Ground Source", source.IdfObjectName);
        IReadOnlyList<IdfObject> modern = source.ToIdfObjects(
            Context(legacy: false),
            terminalUnitNames: new[] { "Terminal_Geo" });
        IReadOnlyList<IdfObject> legacy = source.ToIdfObjects(
            Context(legacy: true),
            terminalUnitNames: new[] { "PackagedAirConditioner_named_Geo" });
        Assert.Contains(modern, item => item.ObjectType == source.IdfObjectType);
        Assert.Contains(legacy, item => item.ObjectType == source.IdfObjectType);

        yield return $"type={source.GetType().FullName}";
        yield return $"base-type={source.GetType().BaseType?.FullName}";
        yield return $"sealed={typeof(GeothermalHeatPump).IsSealed}";
        yield return $"fuel={source.Fuel}";
        yield return $"heating-cop={Format(source.HeatingCoefficientOfPerformance)}";
        yield return $"cooling-cop={Format(source.CoolingCoefficientOfPerformance)}";
        yield return $"heating-capacity={Optional(source.HeatingCapacityWatts)}";
        yield return $"cooling-capacity={Optional(source.CoolingCapacityWatts)}";
        yield return $"idf-object-type={source.IdfObjectType}";
        yield return $"idf-object-name={source.IdfObjectName}";
        yield return $"terminal-list={source.TerminalUnitListName}";
        yield return $"modern={ObjectSetFact(modern)}";
        yield return $"legacy={ObjectSetFact(legacy)}";
    }

    private static IEnumerable<string> ObserveHeatPump()
    {
        var source = new HeatPump(
            Id("I-HP"),
            "Heat Pump",
            Fuel.Electricity,
            3.4,
            2.9);
        Assert.Equal(Fuel.Electricity, source.Fuel);
        Assert.Equal(3.4, source.HeatingCoefficientOfPerformance);
        Assert.Equal(2.9, source.CoolingCoefficientOfPerformance);
        Assert.Null(source.HeatingCapacityWatts);
        Assert.Null(source.CoolingCapacityWatts);
        Assert.Equal("AirConditioner:VariableRefrigerantFlow", source.IdfObjectType);
        Assert.Equal("HeatPump_named_Heat Pump", source.IdfObjectName);
        Assert.Equal("Terminal_Units_for_HeatPump_named_Heat Pump", source.TerminalUnitListName);

        IReadOnlyList<IdfObject> modern = source.ToIdfObjects(
            Context(legacy: false),
            terminalUnitNames: new[] { "Terminal_A", "Terminal_B" });
        IReadOnlyList<IdfObject> legacy = source.ToIdfObjects(
            Context(legacy: true),
            terminalUnitNames: new[] { "PackagedAirConditioner_named_Only" });
        Assert.Contains(modern, item => item.ObjectType == source.IdfObjectType);
        Assert.Contains(legacy, item => item.ObjectType == source.IdfObjectType);

        yield return $"type={source.GetType().FullName}";
        yield return $"name={source.Name}";
        yield return $"fuel={source.Fuel}";
        yield return $"heating-cop={Format(source.HeatingCoefficientOfPerformance)}";
        yield return $"cooling-cop={Format(source.CoolingCoefficientOfPerformance)}";
        yield return $"heating-capacity={Optional(source.HeatingCapacityWatts)}";
        yield return $"cooling-capacity={Optional(source.CoolingCapacityWatts)}";
        yield return $"idf-object-type={source.IdfObjectType}";
        yield return $"idf-object-name={source.IdfObjectName}";
        yield return $"loop-name={source.LoopName}";
        yield return $"terminal-list={source.TerminalUnitListName}";
        yield return $"modern={ObjectSetFact(modern)}";
        yield return $"legacy={ObjectSetFact(legacy)}";
        yield return "invalid-fuel=" + CaptureArgument(() => _ = new HeatPump(
            Id("I-BAD-FUEL"), "Bad", (Fuel)999, 3, 3));
        yield return "invalid-heating-cop=" + CaptureArgument(() => _ = new HeatPump(
            Id("I-BAD-HCOP"), "Bad", Fuel.Electricity, 0, 3));
        yield return "invalid-cooling-cop=" + CaptureArgument(() => _ = new HeatPump(
            Id("I-BAD-CCOP"), "Bad", Fuel.Electricity, 3, double.NaN));
        yield return "invalid-capacity=" + CaptureArgument(() => _ = new HeatPump(
            Id("I-BAD-CAP"), "Bad", Fuel.Electricity, 3, 3, -1));
    }

    private static IEnumerable<string> ObserveSourceSystemNames()
    {
        var heatPump = new HeatPump(Id("J-HP"), "Source Name", Fuel.Electricity, 3.2, 3.0);
        var boiler = new Boiler(Id("J-BOILER"), "Boiler Name", Fuel.NaturalGas);
        var tower = new OpenTwoSpeedCoolingTower(Id("J-TOWER"), "Tower Name");
        var chiller = new Chiller(
            Id("J-CHILLER"), "Chiller Name", 4.5, CompressorType.Turbo, tower);
        SourceSystem[] sources = { heatPump, boiler, chiller };
        Assert.True(typeof(SourceSystem).IsAbstract);
        foreach (SourceSystem source in sources)
        {
            Assert.Equal($"Loop_for_{source.Name}", source.LoopName);
            yield return $"{source.GetType().Name}:name={source.Name}";
            yield return $"{source.GetType().Name}:object={source.IdfObjectName}";
            yield return $"{source.GetType().Name}:type={source.IdfObjectType}";
            yield return $"{source.GetType().Name}:loop={source.LoopName}";
        }

        Assert.Equal(
            "Terminal_Units_for_HeatPump_named_Source Name",
            heatPump.TerminalUnitListName);
        IReadOnlyList<IdfObject> heatPumpObjects = heatPump.ToIdfObjects(
            Context(legacy: false),
            terminalUnitNames: new[] { "Terminal_Source" });
        IReadOnlyList<IdfObject> boilerObjects = boiler.ToIdfObjects(Context(legacy: false));
        IReadOnlyList<IdfObject> chillerLegacy = chiller.ToIdfObjects(Context(legacy: true));
        Assert.Contains(heatPumpObjects, item => item.ObjectType == heatPump.IdfObjectType);
        Assert.Contains(boilerObjects, item => item.ObjectType == boiler.IdfObjectType);
        Assert.Contains(chillerLegacy, item => item.ObjectType == chiller.IdfObjectType);
        yield return "abstract=True";
        yield return "terminal-list=" + heatPump.TerminalUnitListName;
        yield return "heatpump-modern=" + ObjectSetFact(heatPumpObjects);
        yield return "boiler-modern=" + ObjectSetFact(boilerObjects);
        yield return "chiller-legacy=" + ObjectSetFact(chillerLegacy);
    }

    private static IdfGenerationContext Context(bool legacy) => new(
        schema: null,
        options: new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonHvacTopology = legacy,
        });

    private static string ObjectSetFact(IEnumerable<IdfObject> objects)
    {
        object[] projection = objects.Select(item => new
        {
            object_type = item.ObjectType,
            fields = item.Fields.Select(field => field.Value).ToArray(),
        }).ToArray<object>();
        return $"{projection.Length}:{CanonicalSha256(JsonSerializer.SerializeToElement(projection))}";
    }

    private static string CaptureArgument(Action action)
    {
        try
        {
            action();
            return "returned";
        }
        catch (ArgumentException error)
        {
            return $"{error.GetType().Name}:{error.ParamName ?? "none"}";
        }
    }

    private static string Optional(double? value) => value is null ? "null" : Format(value.Value);

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static object CreateReceipt(
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        NativeObservation observation = Assert.Single(
            observations,
            item => item.CaseId == target.CaseId);
        CaseBinding fixtureCase = Assert.Single(Cases, item => item.CaseId == target.CaseId);
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
                support_fixture = Artifact(
                    SupportFixturePath,
                    SupportFixtureBytes,
                    SupportFixtureSha256),
                support_generator = Artifact(
                    SupportGeneratorPath,
                    SupportGeneratorBytes,
                    SupportGeneratorSha256),
                support_native_parity = Artifact(
                    SupportParityPath,
                    SupportParityBytes,
                    SupportParitySha256),
                native_sources = NativeSources
                    .Select(item => Artifact(item.Path, item.Bytes, item.Sha256))
                    .ToArray(),
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
                    native_outcome = target.Classification == "equivalent"
                        ? "equivalent-as-pinned"
                        : "adapted-as-pinned",
                },
            },
            scope = new
            {
                exact_target_count = 59,
                equivalent_target_count = 27,
                exception_target_count = 32,
                exact_case_count = 10,
                adjacent_count_not_recorded = 15,
                adjacent_receipts_sha256 = AdjacentReceiptsSha256,
                fixture_repository_commit = FixtureRepositoryCommit,
                resolved_idf_support_reused = true,
                claim_policy = "only-the-pinned-case-and-declared-production-public-route-are-claimed",
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
        AssertKeys(
            source,
            "body_hash",
            "inventory_index",
            "kind",
            "path",
            "signature_hash",
            "symbol",
            "symbol_hash");
        Assert.Equal(target.InventoryIndex, source.GetProperty("inventory_index").GetInt32());
        Assert.Equal(target.Symbol, RequiredString(source, "symbol"));
        Assert.Equal(target.Kind, RequiredString(source, "kind"));
        Assert.Equal(target.SymbolHash, RequiredString(source, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(source, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(source, "body_hash"));
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));

        JsonElement observed = Assert.Single(receipt.GetProperty("observations").EnumerateArray());
        NativeObservation expectedObservation = Assert.Single(
            observations,
            item => item.CaseId == target.CaseId);
        CaseBinding expectedCase = Assert.Single(Cases, item => item.CaseId == target.CaseId);
        AssertKeys(
            observed,
            "case_code",
            "case_id",
            "native_fact_count",
            "native_facts",
            "native_facts_sha256",
            "native_outcome",
            "python_case_sha256",
            "python_facts_sha256");
        Assert.Equal(expectedCase.CaseId, RequiredString(observed, "case_id"));
        Assert.Equal(expectedCase.Code, RequiredString(observed, "case_code"));
        Assert.Equal(expectedCase.CaseSha256, RequiredString(observed, "python_case_sha256"));
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
            "native_sources",
            "public_inventory",
            "python_validator",
            "support_fixture",
            "support_generator",
            "support_native_parity");
        AssertArtifact(artifacts.GetProperty("fixture"), FixturePath, FixtureBytes, FixtureSha256);
        AssertArtifact(artifacts.GetProperty("generator"), GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertArtifact(artifacts.GetProperty("python_validator"), ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertArtifact(artifacts.GetProperty("public_inventory"), InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertArtifact(artifacts.GetProperty("support_fixture"), SupportFixturePath, SupportFixtureBytes, SupportFixtureSha256);
        AssertArtifact(artifacts.GetProperty("support_generator"), SupportGeneratorPath, SupportGeneratorBytes, SupportGeneratorSha256);
        AssertArtifact(artifacts.GetProperty("support_native_parity"), SupportParityPath, SupportParityBytes, SupportParitySha256);
        AssertArtifactArray(artifacts.GetProperty("native_sources"), NativeSources);

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
            "fixture_repository_commit",
            "resolved_idf_support_reused");
        Assert.Equal(59, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(27, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(32, scope.GetProperty("exception_target_count").GetInt32());
        Assert.Equal(10, scope.GetProperty("exact_case_count").GetInt32());
        Assert.Equal(15, scope.GetProperty("adjacent_count_not_recorded").GetInt32());
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(scope, "adjacent_receipts_sha256"));
        Assert.Equal(FixtureRepositoryCommit, RequiredString(scope, "fixture_repository_commit"));
        Assert.True(scope.GetProperty("resolved_idf_support_reused").GetBoolean());

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
                throw new InvalidOperationException($"Unsupported JSON value kind {value.ValueKind}.");
        }
    }

    private static string RequiredString(JsonElement item, string propertyName)
    {
        JsonElement value = item.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return Assert.IsType<string>(value.GetString());
    }

    private static string[] ReadStringArray(JsonElement value)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray().Select(item => Assert.IsType<string>(item.GetString())).ToArray();
    }

    private static int[] ReadIntArray(JsonElement value)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
    }

    private static void AssertStringArray(JsonElement value, IEnumerable<string> expected)
    {
        Assert.Equal(expected, ReadStringArray(value));
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name)
                .OrderBy(item => item, StringComparer.Ordinal));
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    Assert.True(keys.Add(property.Name), $"Duplicate JSON key '{property.Name}'.");
                    AssertUniqueObjectKeysRecursive(property.Value);
                }

                break;
            }
            case JsonValueKind.Array:
                foreach (JsonElement item in value.EnumerateArray())
                {
                    AssertUniqueObjectKeysRecursive(item);
                }

                break;
        }
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        foreach (string text in EnumerateStrings(value))
        {
            Assert.DoesNotContain("C:\\", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:/", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\\Users\\", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AppData", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                yield return value.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Object:
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    foreach (string text in EnumerateStrings(property.Value))
                    {
                        yield return text;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in value.EnumerateArray())
                {
                    foreach (string text in EnumerateStrings(item))
                    {
                        yield return text;
                    }
                }

                break;
        }
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                string raw = value.GetRawText();
                Assert.DoesNotContain("NaN", raw, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Infinity", raw, StringComparison.OrdinalIgnoreCase);
                break;
            case JsonValueKind.Object:
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    AssertNoNonFiniteJsonNumbers(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in value.EnumerateArray())
                {
                    AssertNoNonFiniteJsonNumbers(item);
                }

                break;
        }
    }

    private static void AssertSha256(string value)
    {
        Assert.Equal(71, value.Length);
        Assert.StartsWith("sha256:", value, StringComparison.Ordinal);
        Assert.All(value[7..], character => Assert.True(
            character is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string CaseSha256,
        string FactsSha256,
        string[] TargetSymbols);

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

    private sealed record AdjacentBinding(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string Classification);

    private sealed record NativeObservation(
        string Code,
        string CaseId,
        string[] Facts,
        string FactsSha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record TowerFamily(
        string Code,
        string ObjectType,
        int CapacityIndex,
        Func<string, double?, CoolingTower> Factory);

    private sealed record OracleCorpus(
        JsonElement[] FixtureCases,
        TargetBinding[] Targets,
        AdjacentBinding[] Adjacent);
}
