#pragma warning disable CA1861 // Immutable inline arrays make exact oracle expectations readable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Results;
using Dragons.UpstreamTracker;

namespace Dragons.SimpleDragon.Tests;

public sealed class ModelCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-model-core-oracle.json";
    private const int FixtureBytes = 101_911;
    private const string FixtureSha256 =
        "sha256:85c6f251087083b59c889725b19cbc5f9fb2c9c28b29135c38ce38fe7f65f61d";
    private const string FixtureSchema = "dragons.python-reference.epsimple-model-core.v1";
    private const string CasesSha256 =
        "sha256:1f7ed658cc9dc6908c0c3bbb31fe4f61927bfbe8881e62af6d04cc66072f8fa1";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_model_core_oracle.py";
    private const int GeneratorBytes = 80_699;
    private const string GeneratorSha256 =
        "sha256:513e0052b41727212ae72cd64fa609104d35f26ed4d2378809ce156942655dd5";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_model_core_oracle.py";
    private const int ValidatorBytes = 19_035;
    private const string ValidatorSha256 =
        "sha256:b1ea5798c8c5315e40bc829f01eef4ee2af7686156454441697fc432317fb234";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/core/model.py";
    private const int UpstreamBytes = 36_949;
    private const string UpstreamSourceSha256 =
        "sha256:71dc9bb8d97e829c27d9b5d19ef88709af9613f9e53f60807d54ceb2922e4532";
    private const string UpstreamAstSha256 =
        "sha256:f79918272c07515ee4ae98fa62f4ca5d5d703e5e2faa334f72d6a6966e1e2447";
    private const string DependenciesSha256 =
        "sha256:85d50612b42b3818f054fd7d9cdb26a16bbf832c3afc56762ea732f55a48cb22";
    private const string LoadedSourcesSha256 =
        "sha256:998782cc65bc94d43ffc7538fae747639503f673586bc2815aaddac4dddc1fe1";
    private const string RelocationSnapshotSha256 =
        "sha256:311a666c7b67b8cd0fdd272362a33538c4a6dad6c35e7164ccf8b2f5c51204ab";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.ModelCoreOracleParityTests.MatchesPinnedModelCoreThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Model/GreenRetrofitModel.cs", 7_668,
            "sha256:927ac0cd6982f48f1112a690e1a656dd16716dd96d5a145beb303e2154bbcc33"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Weather/WeatherDatabase.cs", 9_454,
            "sha256:28f3885362fe08663ba6393bae545b70d17284d1751aa5a97cd0194e1b271b34"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonDatabase.cs", 2_537,
            "sha256:af9d3176183292b19e2304e9be3e000e266a6d858d462bdfd65d042d1568147b"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonEmbeddedData.cs", 3_104,
            "sha256:ae2cb7c89e4dcef7195e528fc7831c5abdba560651a244281ffeaaa83c60fc9f"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs", 48_641,
            "sha256:a212275276ccff153d5df42a44a46ac8877afa485e315ee27d08767a909f29bb"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_646,
            "sha256:40e6e7aa1eb89cb341c7e7a32471fa029024e49b261dce8a8926514109d727ba"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_154,
            "sha256:9462f609a4a7a4e062612e4058921b0c91931dc8ff7216dbe54e258cb59ec22c"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Results/GreenRetrofitResultBuilder.cs", 17_491,
            "sha256:4ddd61c1826875419c820647bc1b1088170eaad9c93be59009d8bb00442ee4c9"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Zone.cs", 6_655,
            "sha256:e5a1c9672c7ff9a9d2cf660c96f303f0f162cdd888f681e0f7b24ef98d197a29"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Surface.cs", 7_030,
            "sha256:fc64bbc6f9914393f1f3ec1fea7a101ba30e0c7640ed12280a8d1614dfc78dee"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Fenestration.cs", 2_410,
            "sha256:254b305f2ea49d8c39b25a228a0e734e730fd9168ba04c599c3344b6e92ac9f8"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/Material.cs", 1_988,
            "sha256:a574a5a93277be915c4a9a20e81d5e13fd7d52d0e43b7ba120078fb4eb8d672e"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/SurfaceConstruction.cs", 7_269,
            "sha256:605f54f51df2690cef21885171d6c72752022823f393f872c836160312cf03c6"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/FenestrationConstruction.cs", 3_953,
            "sha256:6e8fb7cf51f284d51fb37d5a1b88626422e7ace34a3187d7e0e73196a3a96073"),
        new("src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SourceSystem.cs", 6_885,
            "sha256:db5fafe1034aca7b16ef222ecad981b790952474e5311b798c9eb6a677c82af4"),
        new("src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusFailure.cs", 904,
            "sha256:818b5d5e901397b9b4aee1aaf25d120bef716173994df19bb4b4b70046ffdc2e"),
        new("src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusRunner.cs", 17_200,
            "sha256:64129e1f61fe153c5cf3e64536664b64e4fbb3bac345339cb0732120eacae464"),
        new("src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusRunModels.cs", 6_913,
            "sha256:624eef93656fce4a7c15ca993fdfd71e9d925f32d184d0d9e5e34cb762c18b37"),
    };

    private static readonly ArtifactPin[] NativeData =
    {
        new("fixtures/simple-dragon/grm/ASHRAE 140 modified.grm", 9_154,
            "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3"),
        new("data/simple-dragon/construction/material.csv", 146,
            "sha256:2a2b62b1c13e65d99098acac23f1ffcc4aa9ce08d162aa8491898b3f0c7bd395"),
        new("data/simple-dragon/construction/construction_regulation_surface.csv", 106_539,
            "sha256:292d2acc786bbfae0a83a9365e85b697f5bb97b25f25d4f3de21aae25310d48a"),
        new("data/simple-dragon/construction/construction_regulation_fenestration.csv", 27_623,
            "sha256:4e3813baf863dcce1bdb30382d9b33f3a481d5d7b927279c2a76f10aa7cc8562"),
        new("data/simple-dragon/profile/KoreanHoliday.csv", 555,
            "sha256:82975066695d335065b4fe905b000aa4a0ae1aa8893b4deb1ee6668c3344dfce"),
        new("data/simple-dragon/profile/KoreanUsageProfile.csv", 2_014,
            "sha256:f3c56d80121bc62c47113dafca27e903aa76ff487f18723ce5fe3dfcff8a65bb"),
        new("data/simple-dragon/profile/KoreanUsageProfileExtended.csv", 409,
            "sha256:a24d0ba02559394becb9abc488c544c5bfb1fd424f1c8cd19ef61b6c2cf90ed0"),
        new("data/simple-dragon/weather/기후지역.csv", 16_571,
            "sha256:565027a25cc89e9d45924c9b4a957251aafc84022fd61f5dcbf3deb0467fb405"),
        new("data/simple-dragon/weather/행정구역별기상데이터.csv", 38_708,
            "sha256:3422b036820740587173dece19453c6b0e7fba5e7e1aa15f27b830948b6787bc"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("T01", "epsimple-model-core.weather-table-topology", "weather", "sha256:8c4c57200340370423cfba8ffa71ae82c0426242f035c7c2c2ef226cef1e0ad2", "sha256:7c707fa287c1ed8e0acf67f7f6344a34358ec504945dc873c314f74adda922eb", new[] { "ADDR_WEATHER_TABLE", "CLIMATE_TABLE" }, Array.Empty<string>()),
        new("A01", "epsimple-model-core.address-weather-resolution-and-failure", "weather", "sha256:ccd017b52eb63068b3c42277715c00604ece54b41435586350b754db6cb8ceed", "sha256:c4a51fa76ebb12f444c38b9226963bd896e061b20ffa6283f4df92afe18e490a", new[] { "InvalidAddressError", "address_to_weather" }, new[] { "ADDR_WEATHER_TABLE", "CLIMATE_TABLE" }),
        new("E01", "epsimple-model-core.energyplus-error-formatting", "error", "sha256:940b5460270b4b6c5bba6f31f2c6b7ce2371e1efe75268660bd53ed6dd7bc106", "sha256:fb6b2c39f7c7a69ece80c784ff87504299727a6f9e8ac29befb0651c415f3d4d", new[] { "EnergyPlusError", "EnergyPlusError.__init__" }, Array.Empty<string>()),
        new("M01", "epsimple-model-core.model-constructor-fundamental-properties", "model", "sha256:12e64a711ecfa0734eb3d47e890a696344745fc62e8b7a7bbe951af228d1195f", "sha256:d14a7bbabd527f0acd5f1898a17fb0e3c419cbd0cc185006525bddffcb2844d6", new[] { "GreenRetrofitModel", "GreenRetrofitModel.__init__", "GreenRetrofitModel.address", "GreenRetrofitModel.climate", "GreenRetrofitModel.north_axis", "GreenRetrofitModel.terrain", "GreenRetrofitModel.vintage", "GreenRetrofitModel.weather", "GreenRetrofitModel.weather_filepath" }, new[] { "address_to_weather" }),
        new("P01", "epsimple-model-core.area-and-exterior-projections", "projection", "sha256:57ddb3d713362d7ff126361acacd97e33bf1742460bb03ff7a86cfaeb480c81f", "sha256:ff270cce43b17e56870f654764fa61cfb069867547d0caafcd1013346da10f19", new[] { "GreenRetrofitModel.area", "GreenRetrofitModel.exteriorfloors", "GreenRetrofitModel.exteriorroofs", "GreenRetrofitModel.exteriorwalls", "GreenRetrofitModel.exteriorwindows" }, Array.Empty<string>()),
        new("W01", "epsimple-model-core.weighted-averages-and-zero-cases", "projection", "sha256:6713975ddac523afde1ddc797fc53a08cfbeede94413b70f77f68641a474c331", "sha256:1f4d9e053180a350462b0d2e8b46ba46b2beb0ad75fdd206219f117ac9ba5db8", new[] { "GreenRetrofitModel.averaged_exteriorfloor_Uvalue", "GreenRetrofitModel.averaged_exteriorroof_Uvalue", "GreenRetrofitModel.averaged_exteriorwall_Uvalue", "GreenRetrofitModel.averaged_infiltration", "GreenRetrofitModel.averaged_lightdensity", "GreenRetrofitModel.averaged_window_Uvalue" }, Array.Empty<string>()),
        new("S01", "epsimple-model-core.source-system-dedup-and-explicit-merge", "model", "sha256:23a3ab2d2234b2199f89ddb9c1d4d8338d6228fb56f23e28828bced7c80e0a38", "sha256:7f03eb6c6ea5fd9f5e093cea126f9983d5bf929f9e3ffc6d30e6c96b9222afae", new[] { "GreenRetrofitModel.source_system" }, Array.Empty<string>()),
        new("U01", "epsimple-model-core.unique-catalog-projections", "projection", "sha256:1f971ab2878da05a73e6e551d1a02cd2f1d48dc3dfe83d1c61a0c92c9deee066", "sha256:61a5d1b8914d16b779f3e56c34666624d370d308e66e3df17c5b15dc63e3ef50", new[] { "GreenRetrofitModel.get_unique_fenestration_constructions", "GreenRetrofitModel.get_unique_materials", "GreenRetrofitModel.get_unique_profiles", "GreenRetrofitModel.get_unique_surface_constructions" }, Array.Empty<string>()),
        new("J01", "epsimple-model-core.grjson-full-graph-and-adjacency-allocation", "serialization", "sha256:209359d9ed17d9da99e5788b05c91dd29260ad1c8c0ed11d972051f6f4b9eb0a", "sha256:646a852bcceca7aad45be6df9be0b77bbe531b4630c29e37dae4f2f8a90579cd", new[] { "GreenRetrofitModel.from_grjson" }, Array.Empty<string>()),
        new("C01", "epsimple-model-core.dragon-and-idf-conversion", "conversion", "sha256:8556889dff89e16cab492bbf820498bc13857e9e7e293ed70a5c85cef77d7969", "sha256:07dded0daf3cf65f6d36031921359823ea75e209e6924df6b0460feea5fe29f4", new[] { "GreenRetrofitModel.to_dragon", "GreenRetrofitModel.to_idf" }, Array.Empty<string>()),
        new("R01", "epsimple-model-core.instrumented-run-success-and-failure", "runtime", "sha256:178e2c718fe5cbe8951147d8fc16795fed65d4b3a5719ff4975f5e35e9e51c49", "sha256:404d9f442aa7385107127a5309d372a57c52d6a8d7fb0ee39ee51c1b7f7606e1", new[] { "GreenRetrofitModel.run" }, new[] { "EnergyPlusError" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        Target(337, "ADDR_WEATHER_TABLE", "constant", "epsimple-model-core-337-1a4029a1", "exception", "typed-packaged-weather-database-rather-than-mutable-dataframe-1a4029a1", "Dragons.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and Dragons.SimpleDragon.WeatherSelection", 0),
        Target(338, "CLIMATE_TABLE", "constant", "epsimple-model-core-338-fbfb5af8", "exception", "typed-date-indexed-weather-database-rather-than-mutable-dataframe-fbfb5af8", "Dragons.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and Dragons.SimpleDragon.WeatherSelection", 0),
        Target(339, "EnergyPlusError", "class", "epsimple-model-core-339-3ed10042", "exception", "structured-diagnostics-rather-than-throwing-table-wrapper-3ed10042", "Dragons.EnergyPlus.Runtime.EnergyPlusFailure and Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)", 2),
        Target(340, "EnergyPlusError.__init__", "function", "epsimple-model-core-340-328cf73b", "exception", "energyplus-failure-and-result-builder-diagnostics-328cf73b", "Dragons.EnergyPlus.Runtime.EnergyPlusFailure and Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)", 2),
        Target(341, "GreenRetrofitModel", "class", "epsimple-model-core-341-fb39a800", "exception", "immutable-floor-and-catalog-aggregate-rather-than-mutable-zone-list-fb39a800", "Dragons.SimpleDragon.GreenRetrofitModel constructor", 3),
        Target(342, "GreenRetrofitModel.__init__", "function", "epsimple-model-core-342-e8bd64b7", "exception", "immutable-defensive-copy-constructor-with-explicit-weather-e8bd64b7", "Dragons.SimpleDragon.GreenRetrofitModel constructor", 3),
        Target(345, "GreenRetrofitModel.address", "function", "epsimple-model-core-345-df358686", "exception", "readonly-address-with-explicit-weather-selection-df358686", "Dragons.SimpleDragon.GreenRetrofitModel.Address", 3),
        Target(346, "GreenRetrofitModel.area", "function", "epsimple-model-core-346-bf31ed3c", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.Area", 4),
        Target(347, "GreenRetrofitModel.averaged_exteriorfloor_Uvalue", "function", "epsimple-model-core-347-ef752eff", "exception", "nullable-construction-filter-rather-than-singleton-identity-regulation-ef752eff", "Dragons.SimpleDragon.GreenRetrofitModel.AverageExteriorFloorUValue", 5),
        Target(348, "GreenRetrofitModel.averaged_exteriorroof_Uvalue", "function", "epsimple-model-core-348-871c1b93", "exception", "nullable-construction-filter-rather-than-singleton-identity-regulation-871c1b93", "Dragons.SimpleDragon.GreenRetrofitModel.AverageExteriorRoofUValue", 5),
        Target(349, "GreenRetrofitModel.averaged_exteriorwall_Uvalue", "function", "epsimple-model-core-349-13f93b86", "exception", "nullable-construction-filter-rather-than-singleton-identity-regulation-13f93b86", "Dragons.SimpleDragon.GreenRetrofitModel.AverageExteriorWallUValue", 5),
        Target(350, "GreenRetrofitModel.averaged_infiltration", "function", "epsimple-model-core-350-4046cce9", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.AverageInfiltration", 5),
        Target(351, "GreenRetrofitModel.averaged_lightdensity", "function", "epsimple-model-core-351-695c215a", "exception", "nullable-light-density-excluded-from-weight-denominator-695c215a", "Dragons.SimpleDragon.GreenRetrofitModel.AverageLightDensity", 5),
        Target(352, "GreenRetrofitModel.averaged_window_Uvalue", "function", "epsimple-model-core-352-235f45cc", "exception", "native-window-projection-also-includes-glass-doors-235f45cc", "Dragons.SimpleDragon.GreenRetrofitModel.AverageWindowUValue", 5),
        Target(353, "GreenRetrofitModel.climate", "function", "epsimple-model-core-353-27c207a5", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.Weather.ClimateRegion", 3),
        Target(354, "GreenRetrofitModel.exteriorfloors", "function", "epsimple-model-core-354-61333306", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorFloors", 4),
        Target(355, "GreenRetrofitModel.exteriorroofs", "function", "epsimple-model-core-355-9ba0cb63", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorRoofs", 4),
        Target(356, "GreenRetrofitModel.exteriorwalls", "function", "epsimple-model-core-356-428acddc", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorWalls", 4),
        Target(357, "GreenRetrofitModel.exteriorwindows", "function", "epsimple-model-core-357-d363d717", "exception", "native-window-projection-also-includes-glass-doors-d363d717", "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorWindows", 4),
        Target(359, "GreenRetrofitModel.from_grjson", "function", "epsimple-model-core-359-696d04c3", "equivalent", "not_applicable", "Dragons.SimpleDragon.GrmReader.ReadFile(string, SimpleDragonDatabase?)", 8),
        Target(360, "GreenRetrofitModel.get_unique_fenestration_constructions", "function", "epsimple-model-core-360-0963ad71", "exception", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-0963ad71", "Dragons.SimpleDragon.GreenRetrofitModel.FenestrationConstructions", 7),
        Target(361, "GreenRetrofitModel.get_unique_materials", "function", "epsimple-model-core-361-ecb20cb3", "exception", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-ecb20cb3", "Dragons.SimpleDragon.GreenRetrofitModel.Materials", 7),
        Target(362, "GreenRetrofitModel.get_unique_profiles", "function", "epsimple-model-core-362-13af13a1", "exception", "database-resolved-zone-profiles-rather-than-derived-overwrite-map-13af13a1", "Dragons.SimpleDragon.GreenRetrofitModel.Zones with SimpleDragonDatabase.Profiles", 7),
        Target(363, "GreenRetrofitModel.get_unique_surface_constructions", "function", "epsimple-model-core-363-a05748b1", "exception", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-a05748b1", "Dragons.SimpleDragon.GreenRetrofitModel.SurfaceConstructions", 7),
        Target(364, "GreenRetrofitModel.north_axis", "function", "epsimple-model-core-364-fc0d665a", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.NorthAxis", 3),
        Target(365, "GreenRetrofitModel.run", "function", "epsimple-model-core-365-bf192ec8", "exception", "async-runner-and-result-builder-diagnostic-boundary-bf192ec8", "Dragons.EnergyPlus.Runtime.EnergyPlusRunner.RunAsync(EnergyPlusRunRequest, CancellationToken) and Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)", 10),
        Target(366, "GreenRetrofitModel.source_system", "function", "epsimple-model-core-366-b2b62b80", "exception", "immutable-explicit-catalog-rather-than-computed-plus-unvalidated-merge-b2b62b80", "Dragons.SimpleDragon.GreenRetrofitModel.SourceSystems and Dragons.SimpleDragon.GreenRetrofitModel.SupplySystems", 6),
        Target(367, "GreenRetrofitModel.terrain", "function", "epsimple-model-core-367-152775fe", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.Weather.Terrain", 3),
        Target(368, "GreenRetrofitModel.to_dragon", "function", "epsimple-model-core-368-5e2e21f3", "exception", "nonthrowing-aggregate-conversion-result-with-diagnostics-5e2e21f3", "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 9),
        Target(369, "GreenRetrofitModel.to_idf", "function", "epsimple-model-core-369-e8d26d72", "exception", "native-idf-document-conversion-result-with-diagnostics-e8d26d72", "Dragons.SimpleDragon.GreenRetrofitConverter.ToIdfDocument(GreenRetrofitModel, GreenRetrofitConversionOptions?, IddSchema?, EnergyModelIdfOptions?)", 9),
        Target(370, "GreenRetrofitModel.vintage", "function", "epsimple-model-core-370-e739b9d6", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.Vintage", 3),
        Target(371, "GreenRetrofitModel.weather", "function", "epsimple-model-core-371-acd72fe8", "equivalent", "not_applicable", "Dragons.SimpleDragon.GreenRetrofitModel.Weather.WeatherLocation", 3),
        Target(372, "GreenRetrofitModel.weather_filepath", "function", "epsimple-model-core-372-fa174585", "exception", "epw-filename-with-caller-owned-directory-resolution-fa174585", "Dragons.SimpleDragon.WeatherSelection.EpwFileName and ResolveEpwPath(string)", 3),
        Target(387, "InvalidAddressError", "class", "epsimple-model-core-387-aee12b8f", "exception", "lookup-diagnostic-rather-than-address-exception-aee12b8f", "Dragons.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and Dragons.SimpleDragon.WeatherSelection", 1),
        Target(388, "address_to_weather", "function", "epsimple-model-core-388-6e86f546", "exception", "typed-nonthrowing-weather-selection-result-6e86f546", "Dragons.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and Dragons.SimpleDragon.WeatherSelection", 1),
    };

    private static readonly int[] ExcludedIndices = { 343, 344, 358 };
    private static readonly string[] ExcludedSymbols =
    {
        "GreenRetrofitModel.__repr__",
        "GreenRetrofitModel.__str__",
        "GreenRetrofitModel.from_excel",
    };

    private static readonly string[] DeferredSymbols =
    {
        "GreenRetrofitResult",
        "GreenRetrofitResult.VALID_DIGITS",
        "GreenRetrofitResult.__init__",
        "GreenRetrofitResult.area",
        "GreenRetrofitResult.calc_domestic_hotwater_site_energy",
        "GreenRetrofitResult.get_dhw_servers",
        "GreenRetrofitResult.get_domestic_hotwater_energy",
        "GreenRetrofitResult.summarize",
        "GreenRetrofitResult.to_co2",
        "GreenRetrofitResult.to_cost",
        "GreenRetrofitResult.to_dict",
        "GreenRetrofitResult.to_site_uses",
        "GreenRetrofitResult.to_source_uses",
        "GreenRetrofitResult.write",
    };

    // Set only while intentionally discovering a changed, reviewed native observation surface.
    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(11, "sha256:1bd2fc9cd0a98b816afdf751a8599537e9d704d60dbb1c623d224752a3b025f8"),
        new(13, "sha256:35d14c66e69a319c3903964418a962bcd54584f48ae28ee20617c1fdd8e5c169"),
        new(12, "sha256:5499dc83b73583ef3fecaa48120e621c3c6266f98db89ed9b5ca8b45437b070f"),
        new(18, "sha256:e6355c693e8ce6eb1190d489df2145a200ab5b333d8a7a9308984b50d6122c4d"),
        new(14, "sha256:629716e01cfc4cb8063a0521db31849c8d0feaf416a439f264a6966c4b4796b5"),
        new(16, "sha256:e04a9bb27012cd9904c411d8c64f8f9f79bc72991f0a42de4722787596c36896"),
        new(11, "sha256:a234297a813116d5e920d697b64ed8c8d90b256490358420e142ee2ffa6ce1f5"),
        new(11, "sha256:1fa3d7268bd711bcaca7c40f329995a138eb58014a829fb38cdb9dca836f38ea"),
        new(14, "sha256:1ac46b73505543e2600d8a3950f4b3350251eac13b19f82147a936358388d414"),
        new(15, "sha256:371523fdd514fe5162bdef0ed22ab55235b8b6e0f78146636280854f43227eff"),
        new(15, "sha256:40e0e551984826c448a7731958bcbe4e34161b34415b05117590592496a617e9"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:c8260275d40fec867c12e23c52a9e9f07e3050476f4f1d2f563a93116ca0e7fe",
        "sha256:6ab6e5c7685dbb2ace6578429c008a69d7ed76249e6244d4ae04a2f095c0a3c0",
        "sha256:74454cc2128511c5dbb7a1160e5a4e6dd73778f2e760db639ee086f869b503b4",
        "sha256:d33e92fc4d594812dbfe5bde7b86f9cc01393c5a897a151a14be3bfbe08b6dda",
        "sha256:13d56374a3788f92c16775d85333a62f177e64ed3b244d608db84356c71977be",
        "sha256:a39d5e98d491526ec1838aed8244bd06f1501b57ecdad47264ed0530048a42dd",
        "sha256:6bdac11699c9b5c2c51afc42b4f2b34e72d473ff77bef860346afffba422e3e6",
        "sha256:d094abd785eb82231d9ad67d8a50240f29d5570b68f877c53fe0696f00d50951",
        "sha256:53308a134af65eb2a7df05ad936e2f0c7ee877b3be052c29ba1ddc7090464f6d",
        "sha256:e0afa170e93917e45b10dd42cdd009bdd824062412dceb049ddf4b8166b5d70e",
        "sha256:850de3ec9e9057e2cd834707397adabf0ae946fca46d301ee7503ce4ae9bf885",
        "sha256:29b3fbb53642b8065cdd1a712341949d68597793b233f6ae93b7217b2db6a34f",
        "sha256:7fe7434c833925fa138416a55c22ea075ed22cc708ad3eef4b38cdc5fc9202dd",
        "sha256:de6a5b025e88f49b3f688ddd11faef1aa15f7ad69b5b239660e1b3abfdd3308e",
        "sha256:a8cc55c691b3f9ef14a508a2fe1725ccc4472f85ab30e8bcb8e0ce609f762b0a",
        "sha256:08e66e5a6ebe269e197335693b4b4931d9448d7fb9ec9e12277a0cf8890983da",
        "sha256:bc2699534c08f79d42d8ff82003a23e21ea7bb30c777c55c03656158464dffaa",
        "sha256:e155bf95ca02f00f99bad4d90cc71e2e105a2b6665bf703bfc37a7de594647da",
        "sha256:0bd20efc6a2278b10901ccb95bf4fa748ff615959743f8c555beacc7e4217889",
        "sha256:24e43537306f3423d9b581103b00a9bd4dd559d18b0aadd75a3a2f3af0d7221f",
        "sha256:cc2c22a05b8bbf3eb54782584fb19842bc8c67b6620a370e53d97a29b435005a",
        "sha256:dbb4ae595fc275d0df0c2bee264a6aa6ea355816b7934f800e72435e297ff6da",
        "sha256:5dc3b701675a9ce8616073c2c15c5f9e66a44cabb70f91329916ff0cd116d622",
        "sha256:a4dd3351bbe3b61b3fa0cab7e6c8632c98706e94b8c65e808e42e0b5a27e4ba5",
        "sha256:a90770c1a7eb1905c894458ed021b68ac5e37ebad4c771479bdddb75df5f0fd3",
        "sha256:04e521253b56b8b7eb002fad98c1a4901d08a4b247546406f5136c81b12f3996",
        "sha256:ca8e570eeba4a5009a428676acdcd259590c9bab2c9f91a6c6427997f11be4dc",
        "sha256:86285e8ddaac587ce365b27303938144023731f3011592b7a89e911b68c96622",
        "sha256:438ed813b039805a551fe4c791414b2514ff2bda603306857b8209c00a8878a4",
        "sha256:586c30bdeb0916001e28ec0a7bc821756056f88642ce0b99b7034e4fc3ab43e4",
        "sha256:969e70689512de59562a6299b28cd5510efef76b95fdf6ed934dc16246f9253f",
        "sha256:95a7489d3781d005349ba499dd3966113646e3129e68661aae052943e57b38df",
        "sha256:19df310c32033282aa3d72be5dae656b3629a41ecbe1de564ca5c88c03e1f407",
        "sha256:06fc24b5c0c4dbe71e9f24feab9dc2ecc21acf561a22372f6d21f2a3b908a77b",
        "sha256:7c67ebddae5b1a8fba10113dc55a3d3c18d0a326b3ff5e82a26813fc13bf4c68",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:378ba006e7359f3d42bbcd064290d9dab5faf9502f2910f24bb7c44d40777b7e", // epsimple-model-core-337-1a4029a1
        "sha256:7437ef18a25a28162feb6a8e68ae2f8d8415822550dcae802f13757dbd1dae74", // epsimple-model-core-338-fbfb5af8
        "sha256:0f246ba3f9eeb58dfd12a3a9e6006ea2aaf451adbc984c38be7e85f34aed31ff", // epsimple-model-core-339-3ed10042
        "sha256:24df4a0f64d02e729fb7922bc1a6b62495e32a2a5b759a05480a02159b2b574a", // epsimple-model-core-340-328cf73b
        "sha256:3ce2ed326d8b7050cd25e8a243db7f314c3409affe7e2d989bd78108f260fe92", // epsimple-model-core-341-fb39a800
        "sha256:fc50da43ab46717d11682f5f3aa3a12395de0b81a5811eedd9a875b19f0e95ea", // epsimple-model-core-342-e8bd64b7
        "sha256:44b73fada4b7ad0073e8b6b15e7a193d86dea3af48ad6ca9d4308bd6e8379fe4", // epsimple-model-core-345-df358686
        "sha256:ce74b6355e549b218d09293835b6430a1f9761106e3f8a5e5e5e80de3ef6fde3", // epsimple-model-core-346-bf31ed3c
        "sha256:9cc48887c8248d6377236ac1a379b063ff34177b90a36e2703a19244802567f9", // epsimple-model-core-347-ef752eff
        "sha256:63b3c43b5099b9b4aa56a36eea0c05f3c07bf7527f4b68670a88c02a1a78569d", // epsimple-model-core-348-871c1b93
        "sha256:9f2c8240ed8c7a566cac7de882142c913b74d035d5cfacefd21eb4e381b564a8", // epsimple-model-core-349-13f93b86
        "sha256:b25ed61879d61e3e929844b62c7cdb43e447e1204d77d2888398fecfb7b03e00", // epsimple-model-core-350-4046cce9
        "sha256:9691a0c7162a40714c336227a453968ee266113bd51568b7cce845606833d3f2", // epsimple-model-core-351-695c215a
        "sha256:e3813649891605ac6685a874f64c44f828f5c724dabea46d88e308040710a6e9", // epsimple-model-core-352-235f45cc
        "sha256:0395eb6b480d504d5405661e4a31b27be42d794ae90096f4f4491abcbfa39deb", // epsimple-model-core-353-27c207a5
        "sha256:284af60b06d4fe2fbf60fd8695922d2796c9cb1c7bea7123673c23080938e1e8", // epsimple-model-core-354-61333306
        "sha256:a513ff4824f747b6d60f97ebde0cdb3769fa356c5947bf212bf287dfee69f1b0", // epsimple-model-core-355-9ba0cb63
        "sha256:9c903a32c19a41d3b4d98fac299b9325f6b3f4fc76117af29048a6f9cc5041a9", // epsimple-model-core-356-428acddc
        "sha256:de6c572787f2d151a3e1d638310cf070a79ec194e9022fff23dcc94cfce4de49", // epsimple-model-core-357-d363d717
        "sha256:75e94727892f024f8615462fa7fc123618c4691c827928534075fcf689271802", // epsimple-model-core-359-696d04c3
        "sha256:9200be6c24cb4ad128b90f53a09824722bfa5d462402deb9e8acf829f16b9699", // epsimple-model-core-360-0963ad71
        "sha256:bd4bfdf53130bab836a4776c7908f2868c304b00d517f2b31035a0a8f41e005f", // epsimple-model-core-361-ecb20cb3
        "sha256:1f394158a0b98233eab17d7619adb971d5628b8211bffe72e87310fe9d4f2e22", // epsimple-model-core-362-13af13a1
        "sha256:546c344d4d58ccd1856046a181bb6e51850a80aae9a7323c7d322811374844ac", // epsimple-model-core-363-a05748b1
        "sha256:8b2a05a6decf126bf82238f7b8c8f3801cef71451f18e97258199d2e34f3099b", // epsimple-model-core-364-fc0d665a
        "sha256:6812ecf45f6394d7f650c2113c9a32cc638463050c1833bb27fc76ffa2b1ad1d", // epsimple-model-core-365-bf192ec8
        "sha256:47616f5ce88d82f7dace8e06e211121ec4368855793999d553ef5d7fb7a60ae6", // epsimple-model-core-366-b2b62b80
        "sha256:f333cbcd971bebac60462d98b3edb2192da2e94fdd2d87eb663b1af9af44fd44", // epsimple-model-core-367-152775fe
        "sha256:ae1fdc108ed19c09a222d11a2da1fc05c1beb3ff1b4996dcc11f58c158e81f1a", // epsimple-model-core-368-5e2e21f3
        "sha256:513f8edd05b742882b9641b1d0fd5d0378b9c767bcbc907feac5621f62c3d7d6", // epsimple-model-core-369-e8d26d72
        "sha256:1e413eb03a2324a3eba9b489909a4bf9b833bf48c38abbfd429504ff6c42f087", // epsimple-model-core-370-e739b9d6
        "sha256:88bb2ff45fade998112904b7c7ccd9efde8ae6b0e45d29cedb8ad0248a300a7f", // epsimple-model-core-371-acd72fe8
        "sha256:16e257626cbd55dfa933b13aeb11701d76bfdcbce4993fa2e2458408cc86435c", // epsimple-model-core-372-fa174585
        "sha256:726aeac0811460acface0f3b661fb83f60e4e84fd8c2f9010cc85d57d453438b", // epsimple-model-core-387-aee12b8f
        "sha256:d440091068a8cedf3a4023f99ee5ecdcd1c9dcd84facb25492d213efceed8de2", // epsimple-model-core-388-6e86f546
    };

    [Fact]
    public void MatchesPinnedModelCoreThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(11, observations.Length);
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
                "MODEL_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(35, recordCount);
        Assert.Equal(35, corpus.Targets.Length);
        Assert.Equal(35, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(11, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(24, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.DoesNotContain(corpus.Targets, item => ExcludedIndices.Contains(item.InventoryIndex));
        Assert.DoesNotContain(corpus.Targets, item => ExcludedSymbols.Contains(item.Symbol, StringComparer.Ordinal));
        Assert.Equal(11, corpus.FixtureCases.Length);
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

        Assert.True(typeof(GreenRetrofitModel).IsSealed);
        AssertReadOnlyProperty<GreenRetrofitModel>(nameof(GreenRetrofitModel.Address), typeof(string));
        AssertReadOnlyProperty<GreenRetrofitModel>(nameof(GreenRetrofitModel.NorthAxis), typeof(double));
        AssertReadOnlyProperty<GreenRetrofitModel>(nameof(GreenRetrofitModel.Vintage), typeof(DateTime));
        AssertReadOnlyProperty<GreenRetrofitModel>(nameof(GreenRetrofitModel.Weather), typeof(WeatherSelection));
        AssertReadOnlyProperty<GreenRetrofitModel>(nameof(GreenRetrofitModel.Area), typeof(double));
        AssertReadOnlyProperty<GreenRetrofitModel>(nameof(GreenRetrofitModel.SourceSystems), typeof(IReadOnlyList<SourceSystem>));
        Assert.NotNull(typeof(WeatherDatabase).GetMethod(
            nameof(WeatherDatabase.FindByAddress),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(string), typeof(DateTime) },
            modifiers: null));
        Assert.NotNull(typeof(GrmReader).GetMethod(
            nameof(GrmReader.ReadFile),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(SimpleDragonDatabase) },
            modifiers: null));
        Assert.NotNull(typeof(GreenRetrofitConverter).GetMethod(
            nameof(GreenRetrofitConverter.Convert),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(GreenRetrofitModel), typeof(GreenRetrofitConversionOptions) },
            modifiers: null));
        Assert.NotNull(typeof(GreenRetrofitConverter).GetMethod(
            nameof(GreenRetrofitConverter.ToIdfDocument),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(EnergyPlusRunner).GetMethod(
            nameof(EnergyPlusRunner.RunAsync),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(EnergyPlusRunRequest), typeof(CancellationToken) },
            modifiers: null));
        Assert.NotNull(typeof(GreenRetrofitResultBuilder).GetMethod(
            nameof(GreenRetrofitResultBuilder.Build),
            BindingFlags.Public | BindingFlags.Static));
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
        AssertFailClosedTamperProbes(bytes);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 128,
        });
    }

    private static void AssertFailClosedTamperProbes(byte[] bytes)
    {
        byte[] tampered = (byte[])bytes.Clone();
        tampered[64] ^= 1;
        Assert.NotEqual(FixtureSha256, Sha256(tampered));

        using JsonDocument duplicate = JsonDocument.Parse("{\"value\":1,\"value\":2}");
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertUniqueObjectKeysRecursive(duplicate.RootElement));
        Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(
            "{\"value\":1,}",
            new JsonDocumentOptions { AllowTrailingCommas = false }));
    }

    private static OracleCorpus ValidateOracle(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
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
        ValidateExcludedAndDeferred(root, targets);
        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            fixtureCases.SelectMany(item => ReadStringArray(item.GetProperty("target_symbols")))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));
        return new OracleCorpus(fixtureCases, targets);
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "dependencies_sha256",
            "implementation",
            "platform",
            "pointer_width_bits",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
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
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(
            upstream,
            "commit",
            "inventory_sha256",
            "isolated_import",
            "model_resource",
            "path",
            "source",
            "weather_resources");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));

        JsonElement source = upstream.GetProperty("source");
        AssertKeys(source, "ast_sha256", "bytes", "source_sha256");
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));

        JsonElement isolated = upstream.GetProperty("isolated_import");
        AssertKeys(
            isolated,
            "loaded_local_modules",
            "loaded_local_modules_sha256",
            "relocation_snapshot_sha256",
            "source_location_count");
        Assert.Equal(LoadedSourcesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(RelocationSnapshotSha256, RequiredString(isolated, "relocation_snapshot_sha256"));
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        JsonElement[] modules = isolated.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(23, modules.Length);
        Assert.Equal(23, modules.Select(item => RequiredString(item, "module"))
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(modules, item =>
        {
            AssertKeys(item, "ast_sha256", "bytes", "module", "path", "sha256");
            Assert.True(item.GetProperty("bytes").GetInt32() > 0);
            AssertSha256(RequiredString(item, "ast_sha256"));
            AssertSha256(RequiredString(item, "sha256"));
        });
        Assert.Equal(LoadedSourcesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        JsonElement loadedModel = Assert.Single(
            modules,
            item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("epsimple.core.model", RequiredString(loadedModel, "module"));
        Assert.Equal(UpstreamBytes, loadedModel.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(loadedModel, "sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(loadedModel, "ast_sha256"));

        JsonElement modelResource = upstream.GetProperty("model_resource");
        ValidateArtifactProjection(
            modelResource,
            "examples/grm/ASHRAE 140 modified.grm",
            8_900,
            "sha256:4dd307475207fd57599b43b99be22ab1c1d740c3e5a8a9d39e8ee0e30476257a");
        JsonElement[] weatherResources = upstream.GetProperty("weather_resources").EnumerateArray().ToArray();
        Assert.Equal(2, weatherResources.Length);
        Assert.Equal(new[] { 16_318, 38_455 }, weatherResources.Select(item => item.GetProperty("bytes").GetInt32()));
        Assert.Equal(
            new[]
            {
                "sha256:a6949a4b3bc967aefc419f64b1da2b7180fd33a333fed0951560951831614c06",
                "sha256:ec667eeb0ade076272d23f89956add7b0f0ec7eeac6106c02a1c9c4888aa788e",
            },
            weatherResources.Select(item => RequiredString(item, "sha256")));
        Assert.All(weatherResources, item =>
        {
            AssertKeys(item, "bytes", "path", "sha256");
            Assert.StartsWith("epsimple/_data/weather/", RequiredString(item, "path"), StringComparison.Ordinal);
            Assert.EndsWith(".csv", RequiredString(item, "path"), StringComparison.Ordinal);
        });
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
        AssertStringArray(item.GetProperty("context_symbols"), expected.ContextSymbols);

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
            Assert.Equal(RequiredString(inventorySymbol, "symbol_hash"), RequiredString(receipt, "symbol_hash"));
            Assert.Equal(RequiredString(inventorySymbol, "signature_hash"), RequiredString(receipt, "signature_hash"));
            Assert.Equal(RequiredString(inventorySymbol, "body_hash"), RequiredString(receipt, "body_hash"));
            Assert.Equal(RequiredString(receipt, "symbol_hash"), RequiredString(descriptor, "symbol_hash"));
            Assert.Equal(RequiredString(receipt, "signature_hash"), RequiredString(descriptor, "signature_hash"));
            Assert.Equal(RequiredString(receipt, "body_hash"), RequiredString(descriptor, "body_hash"));
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
                337, 338, 339, 340, 341, 342,
                345, 346, 347, 348, 349, 350, 351, 352, 353, 354, 355, 356, 357,
                359, 360, 361, 362, 363, 364, 365, 366, 367, 368, 369, 370, 371, 372,
                387, 388,
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
            "classifications",
            "closure",
            "evidence_contract",
            "expectations",
            "native_routes",
            "runtime_names",
            "runtime_signatures",
            "target_symbols");
        Assert.Equal(11, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        AssertStringArray(contract.GetProperty("target_symbols"), ExpectedTargets.Select(item => item.Symbol));
        Assert.Equal(
            "pinned-python-only-no-native-type-name-claims",
            RequiredString(contract, "runtime_names"));

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
        AssertKeys(
            adaptations,
            ExpectedTargets.Where(item => item.Classification == "exception")
                .Select(item => item.Symbol).ToArray());
        foreach (ExpectedTargetBinding expected in ExpectedTargets)
        {
            Assert.Equal(expected.AssertionId, RequiredString(assertions, expected.Symbol));
            Assert.Equal(expected.Classification, RequiredString(classifications, expected.Symbol));
            Assert.Equal(expected.NativeRoute, RequiredString(nativeRoutes, expected.Symbol));
            JsonElement expectation = expectations.GetProperty(expected.Symbol);
            AssertKeys(expectation, "adaptation", "assertion_id", "classification", "native_route");
            Assert.Equal(expected.AdaptationId, RequiredString(expectation, "adaptation"));
            Assert.Equal(expected.AssertionId, RequiredString(expectation, "assertion_id"));
            Assert.Equal(expected.Classification, RequiredString(expectation, "classification"));
            Assert.Equal(expected.NativeRoute, RequiredString(expectation, "native_route"));
            Assert.Equal(JsonValueKind.Object, signatures.GetProperty(expected.Symbol).ValueKind);
            Assert.True(signatures.GetProperty(expected.Symbol).TryGetProperty("type", out JsonElement type));
            Assert.False(string.IsNullOrWhiteSpace(type.GetString()));
            if (expected.Classification == "exception")
            {
                Assert.Equal(expected.AdaptationId, RequiredString(adaptations, expected.Symbol));
            }
            else
            {
                Assert.False(adaptations.TryGetProperty(expected.Symbol, out _));
                Assert.Equal("not_applicable", expected.AdaptationId);
            }
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "deferred_greenretrofitresult_count",
            "exact_one_case_target_partition",
            "full_source_partition",
            "out_of_scope_exclusion_count",
            "source_declaration_count",
            "target_count");
        Assert.Equal(14, closure.GetProperty("deferred_greenretrofitresult_count").GetInt32());
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_source_partition").GetBoolean());
        Assert.Equal(3, closure.GetProperty("out_of_scope_exclusion_count").GetInt32());
        Assert.Equal(52, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(35, closure.GetProperty("target_count").GetInt32());

        JsonElement evidence = contract.GetProperty("evidence_contract");
        AssertKeys(
            evidence,
            "active_energyplus_process_claim",
            "expected_receipt_count",
            "full_grm_graph_claim",
            "full_idf_semantic_parity_claim",
            "python_behavior_oracle_only",
            "run_boundary_instrumented");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.Equal(35, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.True(evidence.GetProperty("full_grm_graph_claim").GetBoolean());
        Assert.False(evidence.GetProperty("full_idf_semantic_parity_claim").GetBoolean());
        Assert.True(evidence.GetProperty("python_behavior_oracle_only").GetBoolean());
        Assert.True(evidence.GetProperty("run_boundary_instrumented").GetBoolean());
        Assert.Equal(ExpectedTargets.Select(item => item.AssertionId), targets.Select(item => item.AssertionId));
        Assert.Equal(ExpectedTargets.Select(item => item.Classification), targets.Select(item => item.Classification));
        Assert.Equal(ExpectedTargets.Select(item => item.AdaptationId), targets.Select(item => item.AdaptationId));
        Assert.Equal(ExpectedTargets.Select(item => item.NativeRoute), targets.Select(item => item.NativeRoute));
    }

    private static void ValidateExcludedAndDeferred(
        JsonElement root,
        IReadOnlyList<TargetBinding> targets)
    {
        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile(InventoryPath));
        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        JsonElement inventorySymbols = inventory.RootElement.GetProperty("symbols");

        JsonElement[] excluded = root.GetProperty("excluded_receipts").EnumerateArray().ToArray();
        Assert.Equal(3, excluded.Length);
        for (int index = 0; index < excluded.Length; index++)
        {
            ValidateNonTargetReceipt(
                excluded[index],
                inventorySymbols,
                ExcludedIndices[index],
                ExcludedSymbols[index]);
            Assert.DoesNotContain(targets, target => target.InventoryIndex == ExcludedIndices[index]);
        }

        JsonElement[] deferred = root.GetProperty("deferred_receipts").EnumerateArray().ToArray();
        Assert.Equal(14, deferred.Length);
        for (int index = 0; index < deferred.Length; index++)
        {
            ValidateNonTargetReceipt(
                deferred[index],
                inventorySymbols,
                373 + index,
                DeferredSymbols[index]);
            Assert.DoesNotContain(targets, target => target.InventoryIndex == 373 + index);
        }

        int[] partition = targets.Select(item => item.InventoryIndex)
            .Concat(ExcludedIndices)
            .Concat(Enumerable.Range(373, 14))
            .OrderBy(item => item)
            .ToArray();
        Assert.Equal(Enumerable.Range(337, 52), partition);
        Assert.Equal(52, partition.Distinct().Count());
    }

    private static void ValidateNonTargetReceipt(
        JsonElement receipt,
        JsonElement inventorySymbols,
        int expectedIndex,
        string expectedSymbol)
    {
        AssertKeys(receipt, "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
        Assert.Equal(expectedIndex, receipt.GetProperty("inventory_index").GetInt32());
        Assert.Equal(expectedSymbol, RequiredString(receipt, "symbol"));
        Assert.Equal(UpstreamPath, RequiredString(receipt, "path"));
        JsonElement inventoryItem = inventorySymbols[expectedIndex];
        foreach (string field in new[] { "symbol", "kind", "path", "symbol_hash", "signature_hash", "body_hash" })
        {
            Assert.Equal(RequiredString(inventoryItem, field), RequiredString(receipt, field));
        }
    }

    private static void ValidateArtifactProjection(
        JsonElement artifact,
        string path,
        int bytes,
        string sha256)
    {
        AssertKeys(artifact, "bytes", "path", "sha256");
        Assert.Equal(path, RequiredString(artifact, "path"));
        Assert.Equal(bytes, artifact.GetProperty("bytes").GetInt32());
        Assert.Equal(sha256, RequiredString(artifact, "sha256"));
    }

    private static NativeObservation ObserveNativeCase(int index) => index switch
    {
        0 => ObserveT01(),
        1 => ObserveA01(),
        2 => ObserveE01(),
        3 => ObserveM01(),
        4 => ObserveP01(),
        5 => ObserveW01(),
        6 => ObserveS01(),
        7 => ObserveU01(),
        8 => ObserveJ01(),
        9 => ObserveC01(),
        10 => ObserveR01(),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
    };

    private static NativeObservation ObserveT01()
    {
        SimpleDragonDatabase database = SimpleDragonDatabase.LoadEmbedded();
        WeatherDatabase weather = database.Weather;
        Assert.Equal(252, weather.Items.Count);
        Assert.Equal(252, weather.Items.Select(item => item.AdministrativeArea)
            .Distinct(StringComparer.Ordinal).Count());
        WeatherMetadata first = weather.Items[0];
        WeatherSelection selection = weather.FindByAddress(
            first.AdministrativeArea,
            new DateTime(2020, 1, 1)).Require();
        Assert.Same(first, selection.Metadata);
        byte[] addressData = SimpleDragonEmbeddedData.ReadAllBytes(SimpleDragonEmbeddedData.AddressWeather);
        byte[] climateData = SimpleDragonEmbeddedData.ReadAllBytes(SimpleDragonEmbeddedData.ClimateRegions);
        Assert.Equal(38_708, addressData.Length);
        Assert.Equal(16_571, climateData.Length);
        Assert.Equal(252, weather.Items.Select(item => item.LegalDistrictCode)
            .Distinct(StringComparer.Ordinal).Count());
        return Observation(
            "T01",
            "native-route=SimpleDragonDatabase.LoadEmbedded-plus-WeatherDatabase.FindByAddress-plus-WeatherSelection",
            "weather-metadata-count=" + weather.Items.Count,
            "administrative-area-unique-count=" + weather.Items.Select(item => item.AdministrativeArea).Distinct(StringComparer.Ordinal).Count(),
            "legal-district-code-unique-count=" + weather.Items.Select(item => item.LegalDistrictCode).Distinct(StringComparer.Ordinal).Count(),
            "terrain-distinct-count=" + weather.Items.Select(item => item.Terrain).Distinct(StringComparer.Ordinal).Count(),
            "first-address-sha256=" + TextSha256(first.AdministrativeArea),
            "selected-weather-location-sha256=" + TextSha256(selection.WeatherLocation),
            "selected-climate-sha256=" + TextSha256(selection.ClimateRegion),
            "address-weather-resource-bytes=" + addressData.Length,
            "climate-resource-bytes=" + climateData.Length,
            "adaptation=typed-immutable-weather-database-not-mutable-dataframe");
    }

    private static NativeObservation ObserveA01()
    {
        WeatherDatabase weather = SimpleDragonDatabase.LoadEmbedded().Weather;
        WeatherMetadata first = weather.Items[0];
        LookupResult<WeatherSelection> oldLookup = weather.FindByAddress(
            first.AdministrativeArea + " probe",
            new DateTime(2000, 1, 1));
        LookupResult<WeatherSelection> currentLookup = weather.FindByAddress(
            first.AdministrativeArea,
            new DateTime(2020, 1, 1));
        WeatherSelection oldSelection = oldLookup.Require();
        WeatherSelection currentSelection = currentLookup.Require();
        LookupResult<WeatherSelection> missing = weather.FindByAddress(null, new DateTime(2020, 1, 1));
        LookupResult<WeatherSelection> unknown = weather.FindByAddress(
            "unsupported-address-probe",
            new DateTime(2020, 1, 1));
        Assert.False(missing.Found);
        Assert.False(unknown.Found);
        Assert.Equal("SD.WEATHER.ADDRESS_REQUIRED", Assert.Single(missing.Diagnostics).Code);
        Assert.Equal("SD.WEATHER.ADDRESS_NOT_FOUND", Assert.Single(unknown.Diagnostics).Code);
        Assert.Equal(oldSelection.EpwFileName, currentSelection.EpwFileName);
        Assert.NotEqual(oldSelection.ClimateRegion, currentSelection.ClimateRegion);
        string resolved = currentSelection.ResolveEpwPath("weather-root");
        Assert.Equal("weather-root", Path.GetDirectoryName(resolved));
        Assert.Equal(currentSelection.EpwFileName, Path.GetFileName(resolved));
        return Observation(
            "A01",
            "native-route=WeatherDatabase.FindByAddress-plus-LookupResult-plus-WeatherSelection.ResolveEpwPath",
            "address-sha256=" + TextSha256(first.AdministrativeArea),
            "old-found=" + Boolean(oldLookup.Found),
            "current-found=" + Boolean(currentLookup.Found),
            "old-climate-sha256=" + TextSha256(oldSelection.ClimateRegion),
            "current-climate-sha256=" + TextSha256(currentSelection.ClimateRegion),
            "weather-file=" + currentSelection.EpwFileName,
            "weather-location-sha256=" + TextSha256(currentSelection.WeatherLocation),
            "resolved-parent-token=weather-root",
            "missing-code=" + Assert.Single(missing.Diagnostics).Code,
            "unknown-code=" + Assert.Single(unknown.Diagnostics).Code,
            "unknown-message-sha256=" + TextSha256(Assert.Single(unknown.Diagnostics).Message),
            "adaptation=nonthrowing-typed-lookup-result-with-stable-diagnostic");
    }

    private static NativeObservation ObserveE01()
    {
        GreenRetrofitModel model = LoadFixtureModel();
        var failure = new EnergyPlusFailure(
            EnergyPlusFailureCategory.ProcessFailure,
            "PROCESS_FAILED",
            "Safe process failure.",
            "Safe detail.");
        GreenRetrofitResultBuildResult missing = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(Array.Empty<EnergyPlusTabularTable>(), severeCount: 0));
        GreenRetrofitResultBuildResult severe = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(new[] { DummyTable() }, severeCount: 1));
        Assert.False(missing.Success);
        Assert.False(severe.Success);
        Assert.Equal("SD.GRR.MONTHLY_TABLES_MISSING", Assert.Single(missing.Diagnostics).Code);
        Assert.Equal("SD.GRR.ENERGYPLUS_FAILED", Assert.Single(severe.Diagnostics).Code);
        Exception require = Assert.Throws<InvalidOperationException>(() => missing.RequireResult());
        Assert.Equal(EnergyPlusFailureCategory.ProcessFailure, failure.Category);
        return Observation(
            "E01",
            "native-route=EnergyPlusFailure-plus-GreenRetrofitResultBuilder.Build",
            "failure-category=" + failure.Category,
            "failure-code=" + failure.Code,
            "failure-message-sha256=" + TextSha256(failure.Message),
            "failure-detail-sha256=" + TextSha256(failure.Detail!),
            "missing-success=" + Boolean(missing.Success),
            "missing-diagnostic=" + Assert.Single(missing.Diagnostics).Code,
            "severe-success=" + Boolean(severe.Success),
            "severe-diagnostic=" + Assert.Single(severe.Diagnostics).Code,
            "require-result-exception=" + require.GetType().Name,
            "require-result-message-sha256=" + TextSha256(require.Message),
            "adaptation=structured-failure-and-result-diagnostics-not-dataframe-exception-formatting");
    }

    private static NativeObservation ObserveM01()
    {
        NativeGraph graph = CreateNativeGraph();
        var floors = graph.Model.Floors.ToList();
        var materials = graph.Model.Materials.ToList();
        var surfaces = graph.Model.SurfaceConstructions.ToList();
        var fenestrations = graph.Model.FenestrationConstructions.ToList();
        var copied = new GreenRetrofitModel(
            "Model constructor probe",
            15d,
            graph.Model.Address,
            new DateTime(2001, 2, 3, 12, 34, 56),
            false,
            floors,
            materials,
            surfaces,
            fenestrations,
            weather: graph.Model.Weather);
        floors.Clear();
        materials.Clear();
        surfaces.Clear();
        fenestrations.Clear();
        Assert.Equal(2, copied.Floors.Count);
        Assert.Equal(2, copied.Materials.Count);
        Assert.Equal(6, copied.SurfaceConstructions.Count);
        Assert.Equal(3, copied.FenestrationConstructions.Count);
        Assert.Equal(new DateTime(2001, 2, 3), copied.Vintage);
        Assert.Same(graph.Model.Weather, copied.Weather);
        Exception high = Assert.Throws<ArgumentOutOfRangeException>(() => EmptyModel(360d, graph.Model.Weather));
        Exception negative = Assert.Throws<ArgumentOutOfRangeException>(() => EmptyModel(-1d, graph.Model.Weather));
        Exception nan = Assert.Throws<ArgumentOutOfRangeException>(() => EmptyModel(double.NaN, graph.Model.Weather));
        Assert.Equal(359.999d, EmptyModel(359.999d, graph.Model.Weather).NorthAxis);
        string weatherPath = copied.Weather!.ResolveEpwPath("weather-root");
        return Observation(
            "M01",
            "native-route=GreenRetrofitModel-constructor-and-readonly-properties-plus-WeatherSelection",
            "model-sealed=" + Boolean(typeof(GreenRetrofitModel).IsSealed),
            "name=" + copied.Name,
            "address-sha256=" + TextSha256(copied.Address),
            "vintage=" + copied.Vintage.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "north-axis=" + Double(copied.NorthAxis),
            "floors-after-input-clear=" + copied.Floors.Count,
            "materials-after-input-clear=" + copied.Materials.Count,
            "surface-catalog-after-input-clear=" + copied.SurfaceConstructions.Count,
            "fenestration-catalog-after-input-clear=" + copied.FenestrationConstructions.Count,
            "weather-location-sha256=" + TextSha256(copied.Weather.WeatherLocation),
            "climate-sha256=" + TextSha256(copied.Weather.ClimateRegion),
            "terrain=" + copied.Weather.Terrain,
            "weather-file=" + Path.GetFileName(weatherPath),
            "north-axis-high-exception=" + high.GetType().Name,
            "north-axis-negative-exception=" + negative.GetType().Name,
            "north-axis-nan-exception=" + nan.GetType().Name,
            "adaptation=immutable-defensive-copy-with-explicit-weather-selection");
    }

    private static NativeObservation ObserveP01()
    {
        NativeGraph graph = CreateNativeGraph();
        GreenRetrofitModel model = graph.Model;
        Assert.Equal(60d, model.Area, 8);
        Assert.Equal(2, model.ExteriorWalls.Count);
        Assert.Equal(2, model.ExteriorRoofs.Count);
        Assert.Equal(2, model.ExteriorFloors.Count);
        Assert.Equal(2, model.ExteriorWindows.Count);
        Assert.Contains(model.ExteriorWindows, item => item.Type == FenestrationType.Window);
        Assert.Contains(model.ExteriorWindows, item => item.Type == FenestrationType.GlassDoor);
        Assert.DoesNotContain(model.ExteriorWindows, item => item.Type == FenestrationType.Door);
        return Observation(
            "P01",
            "native-route=GreenRetrofitModel.Area-and-exterior-projection-properties",
            "area=" + Double(model.Area),
            "exterior-wall-count=" + model.ExteriorWalls.Count,
            "exterior-roof-count=" + model.ExteriorRoofs.Count,
            "exterior-floor-count=" + model.ExteriorFloors.Count,
            "exterior-window-count=" + model.ExteriorWindows.Count,
            "exterior-wall-name-set-sha256=" + StringSetSha256(model.ExteriorWalls.Select(item => item.Name)),
            "exterior-roof-name-set-sha256=" + StringSetSha256(model.ExteriorRoofs.Select(item => item.Name)),
            "exterior-floor-name-set-sha256=" + StringSetSha256(model.ExteriorFloors.Select(item => item.Name)),
            "exterior-window-name-set-sha256=" + StringSetSha256(model.ExteriorWindows.Select(item => item.Name)),
            "window-type-count=" + model.ExteriorWindows.Count(item => item.Type == FenestrationType.Window),
            "glass-door-type-count=" + model.ExteriorWindows.Count(item => item.Type == FenestrationType.GlassDoor),
            "opaque-door-projected=false",
            "adaptation=glass-doors-are-included-in-native-window-projection");
    }

    private static NativeObservation ObserveW01()
    {
        NativeGraph graph = CreateNativeGraph();
        GreenRetrofitModel model = graph.Model;
        GreenRetrofitModel zero = EmptyModel(0d, model.Weather);
        Assert.Equal(0.45d, model.AverageExteriorWallUValue, 8);
        Assert.Equal(0.4d, model.AverageExteriorRoofUValue, 8);
        Assert.Equal(5d / 12d, model.AverageExteriorFloorUValue, 8);
        Assert.Equal(1.68d, model.AverageWindowUValue, 8);
        Assert.Equal(10d, model.AverageLightDensity, 8);
        Assert.Equal(0.9d, model.AverageInfiltration, 8);
        Assert.Equal(0d, zero.AverageExteriorWallUValue);
        Assert.Equal(0d, zero.AverageExteriorRoofUValue);
        Assert.Equal(0d, zero.AverageExteriorFloorUValue);
        Assert.Equal(0d, zero.AverageWindowUValue);
        Assert.Equal(0d, zero.AverageLightDensity);
        Assert.Equal(0d, zero.AverageInfiltration);
        return Observation(
            "W01",
            "native-route=GreenRetrofitModel-six-weighted-average-properties-and-zero-model",
            "weighted-wall-u=" + Double(model.AverageExteriorWallUValue),
            "weighted-roof-u=" + Double(model.AverageExteriorRoofUValue),
            "weighted-floor-u=" + Double(model.AverageExteriorFloorUValue),
            "weighted-window-u=" + Double(model.AverageWindowUValue),
            "weighted-light-density=" + Double(model.AverageLightDensity),
            "weighted-infiltration=" + Double(model.AverageInfiltration),
            "zero-wall-u=" + Double(zero.AverageExteriorWallUValue),
            "zero-roof-u=" + Double(zero.AverageExteriorRoofUValue),
            "zero-floor-u=" + Double(zero.AverageExteriorFloorUValue),
            "zero-window-u=" + Double(zero.AverageWindowUValue),
            "zero-light-density=" + Double(zero.AverageLightDensity),
            "zero-infiltration=" + Double(zero.AverageInfiltration),
            "unknown-construction-policy=nullable-reference-filter-not-singleton-identity",
            "light-density-policy=null-zone-excluded-from-denominator",
            "window-policy=window-plus-glass-door");
    }

    private static NativeObservation ObserveS01()
    {
        NativeGraph graph = CreateNativeGraph();
        var sourceA = new SourceSystem(
            "District source",
            SourceSystemType.DistrictHeating,
            hotWaterSupply: true,
            id: Id("SRC-A"));
        var sourceB = new SourceSystem(
            "Boiler source",
            SourceSystemType.Boiler,
            FuelType.NaturalGas,
            efficiency: 0.85d,
            hotWaterSupply: true,
            id: Id("SRC-B"));
        var supply = new SupplySystem(
            "Radiator supply",
            SupplySystemType.Radiator,
            sourceA.Id.Value,
            sourceA,
            id: Id("SUP-A"));
        var sources = new List<SourceSystem> { sourceA, sourceB };
        var supplies = new List<SupplySystem> { supply };
        GreenRetrofitModel model = Rebuild(graph, sources, supplies);
        sources.Clear();
        supplies.Clear();
        Assert.Equal(2, model.SourceSystems.Count);
        Assert.Single(model.SupplySystems);
        Assert.Same(sourceA, Assert.Single(model.SupplySystems).SourceSystem);
        Exception duplicate = Assert.Throws<ArgumentException>(() =>
            Rebuild(graph, new[] { sourceA, sourceA }, new[] { supply }));
        Exception nullItem = Assert.Throws<ArgumentException>(() =>
            Rebuild(graph, new SourceSystem[] { sourceA, null! }, new[] { supply }));
        return Observation(
            "S01",
            "native-route=GreenRetrofitModel.SourceSystems-and-SupplySystems-with-constructor-validation",
            "source-count-after-input-clear=" + model.SourceSystems.Count,
            "supply-count-after-input-clear=" + model.SupplySystems.Count,
            "source-id-set-sha256=" + StringSetSha256(model.SourceSystems.Select(item => item.Id.Value)),
            "supply-id-set-sha256=" + StringSetSha256(model.SupplySystems.Select(item => item.Id.Value)),
            "supply-source-reference-retained=" + Boolean(ReferenceEquals(sourceA, Assert.Single(model.SupplySystems).SourceSystem)),
            "duplicate-id-exception=" + duplicate.GetType().Name,
            "duplicate-id-message-sha256=" + TextSha256(duplicate.Message),
            "null-item-exception=" + nullItem.GetType().Name,
            "null-item-message-sha256=" + TextSha256(nullItem.Message),
            "adaptation=explicit-immutable-catalog-rejects-duplicates-and-invalid-items");
    }

    private static NativeObservation ObserveU01()
    {
        NativeGraph graph = CreateNativeGraph();
        GreenRetrofitModel model = graph.Model;
        string[] profileIds = model.Zones
            .Where(item => item.Profile is not null)
            .Select(item => item.Profile!.Id.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, model.Materials.Count);
        Assert.Equal(6, model.SurfaceConstructions.Count);
        Assert.Equal(3, model.FenestrationConstructions.Count);
        Assert.Equal(2, profileIds.Length);
        Assert.All(model.Zones, item => Assert.NotNull(item.Profile));
        return Observation(
            "U01",
            "native-route=GreenRetrofitModel-explicit-catalogs-plus-Zones-resolved-Profiles",
            "material-count=" + model.Materials.Count,
            "material-id-set-sha256=" + StringSetSha256(model.Materials.Select(item => item.Id.Value)),
            "surface-construction-count=" + model.SurfaceConstructions.Count,
            "surface-construction-id-set-sha256=" + StringSetSha256(model.SurfaceConstructions.Select(item => item.Id.Value)),
            "fenestration-construction-count=" + model.FenestrationConstructions.Count,
            "fenestration-construction-id-set-sha256=" + StringSetSha256(model.FenestrationConstructions.Select(item => item.Id.Value)),
            "resolved-profile-count=" + profileIds.Length,
            "resolved-profile-id-set-sha256=" + StringSetSha256(profileIds),
            "all-zone-profiles-resolved=true",
            "adaptation=validated-explicit-catalogs-and-database-resolved-zone-profiles");
    }

    private static NativeObservation ObserveJ01()
    {
        NativeGraph graph = CreateNativeGraph();
        string json = GrmWriter.Serialize(graph.Model, indented: false);
        string tempDirectory = Path.Combine(FindRepositoryRoot(), "temp", "oracle-tests");
        Directory.CreateDirectory(tempDirectory);
        string tempPath = Path.Combine(tempDirectory, "model-core-" + Guid.NewGuid().ToString("N") + ".grm");
        GreenRetrofitModel model;
        try
        {
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            GrmReadResult read = GrmReader.ReadFile(tempPath, SimpleDragonDatabase.Default);
            Assert.True(read.Success, Describe(read.Diagnostics));
            model = read.RequireModel();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (Directory.Exists(tempDirectory)
                && !Directory.EnumerateFileSystemEntries(tempDirectory).Any())
            {
                Directory.Delete(tempDirectory);
            }
        }

        Surface[] adjacent = model.Zones.SelectMany(item => item.Surfaces)
            .Where(item => item.BoundaryCondition == SurfaceBoundaryCondition.Zone)
            .ToArray();
        Assert.Equal(2, adjacent.Length);
        Assert.All(adjacent, item => Assert.Contains(
            model.Zones,
            zone => StringComparer.Ordinal.Equals(zone.Id.Value, item.AdjacentZoneId)));
        Assert.Equal(2, model.Zones.Count);
        Assert.Equal(8, model.Zones.Sum(item => item.Surfaces.Count));
        Assert.Equal(3, model.Zones.Sum(item => item.Surfaces.Sum(surface => surface.Fenestrations.Count)));
        return Observation(
            "J01",
            "native-route=GrmWriter.Serialize-setup-plus-GrmReader.ReadFile-public-production-route",
            "read-success=true",
            "zone-count=" + model.Zones.Count,
            "surface-count=" + model.Zones.Sum(item => item.Surfaces.Count),
            "fenestration-count=" + model.Zones.Sum(item => item.Surfaces.Sum(surface => surface.Fenestrations.Count)),
            "material-count=" + model.Materials.Count,
            "surface-construction-count=" + model.SurfaceConstructions.Count,
            "fenestration-construction-count=" + model.FenestrationConstructions.Count,
            "adjacent-surface-count=" + adjacent.Length,
            "adjacent-targets-resolved=true",
            "zone-id-set-sha256=" + StringSetSha256(model.Zones.Select(item => item.Id.Value)),
            "surface-id-set-sha256=" + StringSetSha256(model.Zones.SelectMany(item => item.Surfaces).Select(item => item.Id.Value)),
            "full-graph-claim=true",
            "temp-scope=repository-temp-oracle-tests-cleaned");
    }

    private static NativeObservation ObserveC01()
    {
        GreenRetrofitModel model = LoadFixtureModel();
        GreenRetrofitConversionResult conversion = GreenRetrofitConverter.Convert(
            model,
            new GreenRetrofitConversionOptions
            {
                Database = SimpleDragonDatabase.Default,
                ResolveUnknownConstructions = true,
                IncludeModelValidationDiagnostics = true,
            });
        Assert.True(conversion.Success, Describe(conversion.Diagnostics));
        var energy = conversion.RequireEnergyModel();
        var validation = energy.Validate();
        Assert.True(validation.IsValid, Describe(validation.Diagnostics));
        IdfDocument idf = GreenRetrofitConverter.ToIdfDocument(
            model,
            new GreenRetrofitConversionOptions
            {
                Database = SimpleDragonDatabase.Default,
                ResolveUnknownConstructions = true,
                IncludeModelValidationDiagnostics = true,
            });
        Assert.Equal("24.2", idf.EnergyPlusVersion);
        Assert.NotEmpty(idf);
        Assert.Single(idf["Building"]);
        Assert.Single(idf["Zone"]);
        string[] firstTypes = idf.Take(8).Select(item => item.ObjectType).ToArray();
        return Observation(
            "C01",
            "native-route=GreenRetrofitConverter.Convert-plus-static-ToIdfDocument",
            "conversion-success=" + Boolean(conversion.Success),
            "energy-model-zone-count=" + energy.Zones.Count,
            "energy-model-conditioned-zone-count=" + energy.ConditionedZones.Count,
            "energy-model-surface-count=" + energy.Surfaces.Count,
            "energy-model-opening-count=" + energy.Surfaces.Sum(item => item.Openings.Count),
            "energy-model-valid=" + Boolean(validation.IsValid),
            "surface-conversion-count=" + conversion.SurfaceConversions.Count,
            "idf-version=" + idf.EnergyPlusVersion,
            "idf-object-count=" + idf.Count,
            "idf-object-type-count=" + idf.Select(item => item.ObjectType).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "idf-first-eight-types-sha256=" + StringListSha256(firstTypes),
            "idf-building-count=" + idf["Building"].Count,
            "idf-zone-count=" + idf["Zone"].Count,
            "full-idf-semantic-parity-claim=false");
    }

    private static NativeObservation ObserveR01()
    {
        var runner = new EnergyPlusRunner();
        EnergyPlusRunResult run = runner.RunAsync(null!, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.False(run.IsSuccess);
        Assert.Equal(EnergyPlusRunState.Failed, run.State);
        Assert.NotNull(run.Failure);
        Assert.Equal("RUN_REQUEST_REQUIRED", run.Failure!.Code);
        Assert.Null(run.ExpandObjectsProcess);
        Assert.Null(run.EnergyPlusProcess);

        GreenRetrofitModel model = LoadFixtureModel();
        GreenRetrofitResultBuildResult missing = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(Array.Empty<EnergyPlusTabularTable>(), severeCount: 0));
        GreenRetrofitResultBuildResult success = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(new[] { DummyTable() }, severeCount: 0));
        Assert.False(missing.Success);
        Assert.True(success.Success, Describe(success.Diagnostics));
        GreenRetrofitResult result = success.RequireResult();
        return Observation(
            "R01",
            "native-route=EnergyPlusRunner.RunAsync-validation-boundary-plus-GreenRetrofitResultBuilder.Build",
            "energyplus-process-started=false",
            "runner-state=" + run.State,
            "runner-failure-category=" + run.Failure.Category,
            "runner-failure-code=" + run.Failure.Code,
            "runner-history-states=" + Join(run.History.Select(item => item.State.ToString())),
            "expandobjects-process-null=" + Boolean(run.ExpandObjectsProcess is null),
            "energyplus-process-null=" + Boolean(run.EnergyPlusProcess is null),
            "builder-failure-success=" + Boolean(missing.Success),
            "builder-failure-code=" + Assert.Single(missing.Diagnostics).Code,
            "builder-success=" + Boolean(success.Success),
            "builder-result-type=" + result.GetType().Name,
            "builder-result-area=" + Double(result.TotalArea),
            "active-energyplus-process-claim=false",
            "adaptation=async-runner-validation-and-structured-result-builder-boundary");
    }

    private static NativeObservation Observation(string code, params string[] facts)
    {
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, item => Assert.False(string.IsNullOrWhiteSpace(item)));
        string hash = CanonicalSha256(JsonSerializer.SerializeToElement(facts));
        return new NativeObservation(code, facts, hash);
    }

    private static NativeGraph CreateNativeGraph()
    {
        SimpleDragonDatabase database = SimpleDragonDatabase.Default;
        UsageProfile profileA = database.UsageProfiles.Items[0];
        UsageProfile profileB = database.UsageProfiles.Items[1];
        WeatherMetadata metadata = database.Weather.Items[0];
        WeatherSelection weather = database.Weather.FindByAddress(
            metadata.AdministrativeArea,
            new DateTime(2020, 1, 1)).Require();

        var insulation = new Material("Insulation", 0.04d, 30d, 1_000d, Id("MAT-A"));
        var concrete = new Material("Concrete", 1.4d, 2_300d, 900d, Id("MAT-B"));
        SurfaceConstruction wallA = SurfaceConstruction.CreateSimple(
            "Wall A", 0.3d, insulation, concrete, id: Id("SC-WALL-A"));
        SurfaceConstruction wallB = SurfaceConstruction.CreateSimple(
            "Wall B", 0.9d, insulation, concrete, id: Id("SC-WALL-B"));
        SurfaceConstruction roofA = SurfaceConstruction.CreateSimple(
            "Roof A", 0.2d, insulation, concrete, id: Id("SC-ROOF-A"));
        SurfaceConstruction roofB = SurfaceConstruction.CreateSimple(
            "Roof B", 0.8d, insulation, concrete, id: Id("SC-ROOF-B"));
        SurfaceConstruction floorA = SurfaceConstruction.CreateSimple(
            "Floor A", 0.25d, insulation, concrete, id: Id("SC-FLOOR-A"));
        SurfaceConstruction floorB = SurfaceConstruction.CreateSimple(
            "Floor B", 0.75d, insulation, concrete, id: Id("SC-FLOOR-B"));
        var windowA = new FenestrationConstruction("Window A", 1.2d, 0.5d, Id("FC-WIN-A"));
        var windowB = new FenestrationConstruction("Window B", 2.4d, 0.6d, Id("FC-WIN-B"));
        var doorConstruction = new FenestrationConstruction("Door", 2d, id: Id("FC-DOOR"));
        var window = new Fenestration(
            "Window A",
            FenestrationType.Window,
            3d,
            windowA.Id.Value,
            windowA,
            id: Id("FN-WINDOW"));
        var glassDoor = new Fenestration(
            "Glass door",
            FenestrationType.GlassDoor,
            2d,
            windowB.Id.Value,
            windowB,
            id: Id("FN-GLASS-DOOR"));
        var door = new Fenestration(
            "Opaque door",
            FenestrationType.Door,
            1d,
            doorConstruction.Id.Value,
            doorConstruction,
            id: Id("FN-DOOR"));

        var zoneAWall = new Surface(
            "Zone A exterior wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            30d,
            0d,
            wallA.Id.Value,
            wallA,
            new[] { window, glassDoor, door },
            id: Id("SURF-A-WALL"));
        var zoneARoof = new Surface(
            "Zone A roof",
            SurfaceType.Ceiling,
            SurfaceBoundaryCondition.Outdoors,
            40d,
            null,
            roofA.Id.Value,
            roofA,
            id: Id("SURF-A-ROOF"));
        var zoneAFloor = new Surface(
            "Zone A floor",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            40d,
            null,
            floorA.Id.Value,
            floorA,
            id: Id("SURF-A-FLOOR"));
        var zoneAAdjacent = new Surface(
            "Zone A adjacent wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Zone,
            10d,
            null,
            wallA.Id.Value,
            wallA,
            adjacentZoneId: "ZONE-B",
            id: Id("SURF-A-ADJ"));
        var zoneBWall = new Surface(
            "Zone B exterior wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Outdoors,
            10d,
            180d,
            wallB.Id.Value,
            wallB,
            id: Id("SURF-B-WALL"));
        var zoneBRoof = new Surface(
            "Zone B roof",
            SurfaceType.Ceiling,
            SurfaceBoundaryCondition.Outdoors,
            20d,
            null,
            roofB.Id.Value,
            roofB,
            id: Id("SURF-B-ROOF"));
        var zoneBFloor = new Surface(
            "Zone B floor",
            SurfaceType.Floor,
            SurfaceBoundaryCondition.Ground,
            20d,
            null,
            floorB.Id.Value,
            floorB,
            id: Id("SURF-B-FLOOR"));
        var zoneBAdjacent = new Surface(
            "Zone B adjacent wall",
            SurfaceType.Wall,
            SurfaceBoundaryCondition.Zone,
            10d,
            null,
            wallB.Id.Value,
            wallB,
            adjacentZoneId: "ZONE-A",
            id: Id("SURF-B-ADJ"));
        var zoneA = new Zone(
            "Zone A",
            1,
            3d,
            new[] { zoneAWall, zoneARoof, zoneAFloor, zoneAAdjacent },
            profileA.Name,
            profileA,
            10d,
            id: Id("ZONE-A"));
        var zoneB = new Zone(
            "Zone B",
            2,
            4d,
            new[] { zoneBWall, zoneBRoof, zoneBFloor, zoneBAdjacent },
            profileB.Name,
            profileB,
            null,
            id: Id("ZONE-B"));
        var model = new GreenRetrofitModel(
            "Model Core Native Graph",
            15d,
            metadata.AdministrativeArea,
            new DateTime(2020, 1, 1),
            false,
            new[]
            {
                new BuildingFloor(1, new[] { zoneA }),
                new BuildingFloor(2, new[] { zoneB }),
            },
            new[] { insulation, concrete },
            new[] { wallA, wallB, roofA, roofB, floorA, floorB },
            new[] { windowA, windowB, doorConstruction },
            weather: weather);
        return new NativeGraph(model, weather);
    }

    private static GreenRetrofitModel Rebuild(
        NativeGraph graph,
        IEnumerable<SourceSystem> sources,
        IEnumerable<SupplySystem> supplies) => new(
            graph.Model.Name + " systems",
            graph.Model.NorthAxis,
            graph.Model.Address,
            graph.Model.Vintage,
            graph.Model.IsMultifamilyHousing,
            graph.Model.Floors,
            graph.Model.Materials,
            graph.Model.SurfaceConstructions,
            graph.Model.FenestrationConstructions,
            sources,
            supplies,
            weather: graph.Weather);

    private static GreenRetrofitModel EmptyModel(double northAxis, WeatherSelection? weather) => new(
        "Empty model",
        northAxis,
        weather?.Metadata.AdministrativeArea ?? "address-probe",
        new DateTime(2020, 1, 1),
        false,
        Array.Empty<BuildingFloor>(),
        Array.Empty<Material>(),
        Array.Empty<SurfaceConstruction>(),
        Array.Empty<FenestrationConstruction>(),
        weather: weather);

    private static GreenRetrofitModel LoadFixtureModel()
    {
        GrmReadResult read = GrmReader.ReadFile(
            FindRepositoryFile("fixtures/simple-dragon/grm/ASHRAE 140 modified.grm"),
            SimpleDragonDatabase.Default);
        Assert.True(read.Success, Describe(read.Diagnostics));
        return read.RequireModel();
    }

    private static EnergyPlusTabularTable DummyTable()
    {
        var header = new EnergyPlusTabularRow(new[]
        {
            new EnergyPlusTabularCell("Month", null),
            new EnergyPlusTabularCell("Value", null),
        });
        var row = new EnergyPlusTabularRow(new[]
        {
            new EnergyPlusTabularCell("January", null),
            new EnergyPlusTabularCell("0", 0d),
        });
        return new EnergyPlusTabularTable(
            "eplustbl.csv",
            "Unused",
            "Entire Facility",
            new[] { "Unused" },
            header,
            new[] { row },
            isMonthly: true);
    }

    private static EnergyPlusSimulationResult Simulation(
        IReadOnlyList<EnergyPlusTabularTable> tables,
        int severeCount)
    {
        return new EnergyPlusSimulationResult(
            EnergyPlusSimulationResult.CurrentSchema,
            new EnergyPlusResultMetadata(
                null,
                null,
                runtimeSucceeded: severeCount == 0,
                null,
                null,
                null,
                null,
                null,
                null),
            new EnergyPlusErrorLog(
                null,
                null,
                null,
                Array.Empty<EnergyPlusDiagnostic>(),
                new EnergyPlusDiagnosticSummary(
                    warningCount: 0,
                    severeCount,
                    fatalCount: 0,
                    completedSuccessfully: severeCount == 0,
                    reportedElapsedSeconds: null)),
            new EnergyPlusAuditLog(null, null),
            new EnergyPlusBoundaryData(null, null),
            tables,
            Array.Empty<EnergyPlusResultSource>());
    }

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
                full_grm_graph_claim = true,
                full_idf_semantic_parity_claim = false,
                python_behavior_oracle_only = true,
                run_boundary_instrumented = true,
                target_count = 35,
                case_count = 11,
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
            "full_grm_graph_claim",
            "full_idf_semantic_parity_claim",
            "python_behavior_oracle_only",
            "run_boundary_instrumented",
            "target_count");
        Assert.False(scope.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.Equal(11, scope.GetProperty("case_count").GetInt32());
        Assert.Equal(35, scope.GetProperty("target_count").GetInt32());
        Assert.True(scope.GetProperty("full_grm_graph_claim").GetBoolean());
        Assert.False(scope.GetProperty("full_idf_semantic_parity_claim").GetBoolean());
        Assert.True(scope.GetProperty("python_behavior_oracle_only").GetBoolean());
        Assert.True(scope.GetProperty("run_boundary_instrumented").GetBoolean());
        Assert.Equal(
            "only-the-authoritative-fixture-case-and-declared-production-public-route-are-claimed",
            RequiredString(scope, "claim_policy"));
    }

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

    private static string TextSha256(string value) => Sha256(Encoding.UTF8.GetBytes(value));

    private static string StringSetSha256(IEnumerable<string> values) => CanonicalSha256(
        JsonSerializer.SerializeToElement(values.OrderBy(item => item, StringComparer.Ordinal).ToArray()));

    private static string StringListSha256(IEnumerable<string> values) => CanonicalSha256(
        JsonSerializer.SerializeToElement(values.ToArray()));

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

    private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Join(IEnumerable<string> values) => string.Join("|", values);

    private static EntityId Id(string value) => new(value);

    private static string Describe(IEnumerable<Diagnostic> diagnostics) => string.Join(
        Environment.NewLine,
        diagnostics.Select(item => item.Code + ": " + item.Message));

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string CaseSha256,
        string FactsSha256,
        string[] TargetSymbols,
        string[] ContextSymbols);

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

    private sealed record OracleCorpus(JsonElement[] FixtureCases, TargetBinding[] Targets);

    private sealed record NativeGraph(GreenRetrofitModel Model, WeatherSelection Weather);
}
