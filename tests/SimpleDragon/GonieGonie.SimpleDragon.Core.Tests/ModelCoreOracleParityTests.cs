#pragma warning disable CA1861 // Immutable inline arrays make exact oracle expectations readable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class ModelCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-model-core-oracle.json";
    private const int FixtureBytes = 102_172;
    private const string FixtureSha256 =
        "sha256:e5cfdc9ba823dc891693864051ffb8cbc06cd08137becef9d6c06fd0c2942cf6";
    private const string FixtureSchema = "goniegonie.python-reference.epsimple-model-core.v1";
    private const string CasesSha256 =
        "sha256:1f7ed658cc9dc6908c0c3bbb31fe4f61927bfbe8881e62af6d04cc66072f8fa1";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_model_core_oracle.py";
    private const int GeneratorBytes = 80_750;
    private const string GeneratorSha256 =
        "sha256:39ce166f6fcc2d51056bf1bb5a06416891c04d34375b898ac709a53fb7abd70e";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_model_core_oracle.py";
    private const int ValidatorBytes = 19_038;
    private const string ValidatorSha256 =
        "sha256:9ef9f2de712be4d577d013e76472746caa5ebc0fe64ef321d4c90005bec3f10d";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
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
        "GonieGonie.SimpleDragon.Tests.ModelCoreOracleParityTests.MatchesPinnedModelCoreThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Model/GreenRetrofitModel.cs", 7_677,
            "sha256:7bf2f7dfb922f4d85982ada0f5622bfbef59dce8cb4d7a90b2759ed6978935ea"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Weather/WeatherDatabase.cs", 9_463,
            "sha256:c7ddc71015eb375e56565a2898d7998cf865fb50d0c8626374f0f642644e9e98"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Data/SimpleDragonDatabase.cs", 2_543,
            "sha256:31bf339ab34fb3e4f65362be0d9519b1d54c44e4b0e46b63e67398873d5fb74a"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Data/SimpleDragonEmbeddedData.cs", 3_131,
            "sha256:76915a821bccc2dbc8e3f185c1faf6c3da07dfe64cd50301b336367d8c5d2d81"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmReader.cs", 48_650,
            "sha256:d91f90946ec19602751fc7818484ca43f85d1c46f9905fa805d8ee8a7281d968"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmWriter.cs", 16_652,
            "sha256:4048cc4bdfca312a7baae54c7055bb3aa7177ee6a8143ed9ef1d182353df1842"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs", 87_343,
            "sha256:0a0774b4461442b2a3cccf68d39fbc236104a2aa13611e0d27c38f27aa2fe5fd"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GreenRetrofitResultBuilder.cs", 17_506,
            "sha256:9a9f1bc3c38814776c3c0ac888423418215c42bb7c270848b72b480751438b3b"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Zone.cs", 6_664,
            "sha256:82b149ae49fdc188d7947553187e4d5cb496d67087ae2e1f7c4e878a02cdd01b"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Surface.cs", 7_039,
            "sha256:a26c799796aa042529926b0c7f4052a495a0e84f8b6a21169aa2b24318b6f809"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Fenestration.cs", 2_419,
            "sha256:6b71c32871b5468b570b64dfc7389132f4cf0413340add7d16dcf0cb44451a78"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/Material.cs", 1_997,
            "sha256:c869470be0b2a1f95ce7ad7cfa3ca32489bb99bed23e3465d0ab426175e8b1f5"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/SurfaceConstruction.cs", 7_278,
            "sha256:0fa371d0fd3c6957ad506b927122c51f3eabb0de32d20d7b1602f118302458b4"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/FenestrationConstruction.cs", 3_962,
            "sha256:15eb1452a5c89bf1e2ce41e1931500b6a329ea6467ac618e2ad6fb139369f5af"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Hvac/SourceSystem.cs", 6_894,
            "sha256:c96df1bb42da5df66b3c4cbf61b800c9bf8450b4b8e427d97929809bca4e8cad"),
        new("src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusFailure.cs", 907,
            "sha256:1301a60181bc7f3369fb972c43b61b00613a9ca3e7342f5c64c543c064b7ca9f"),
        new("src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusRunner.cs", 17_203,
            "sha256:828cbaa9c1864463d83f306a2d4cca6d9b1df0b2af4d58fc4ae2fadda22c5a29"),
        new("src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusRunModels.cs", 6_916,
            "sha256:f32767a31612ce8d620c8e666e84a351ee3331032b9b8e2249481d203c3741f7"),
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
        Target(337, "ADDR_WEATHER_TABLE", "constant", "epsimple-model-core-337-1a4029a1", "exception", "typed-packaged-weather-database-rather-than-mutable-dataframe-1a4029a1", "GonieGonie.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and GonieGonie.SimpleDragon.WeatherSelection", 0),
        Target(338, "CLIMATE_TABLE", "constant", "epsimple-model-core-338-fbfb5af8", "exception", "typed-date-indexed-weather-database-rather-than-mutable-dataframe-fbfb5af8", "GonieGonie.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and GonieGonie.SimpleDragon.WeatherSelection", 0),
        Target(339, "EnergyPlusError", "class", "epsimple-model-core-339-3ed10042", "exception", "structured-diagnostics-rather-than-throwing-table-wrapper-3ed10042", "GonieGonie.EnergyPlus.Runtime.EnergyPlusFailure and GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)", 2),
        Target(340, "EnergyPlusError.__init__", "function", "epsimple-model-core-340-328cf73b", "exception", "energyplus-failure-and-result-builder-diagnostics-328cf73b", "GonieGonie.EnergyPlus.Runtime.EnergyPlusFailure and GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)", 2),
        Target(341, "GreenRetrofitModel", "class", "epsimple-model-core-341-fb39a800", "exception", "immutable-floor-and-catalog-aggregate-rather-than-mutable-zone-list-fb39a800", "GonieGonie.SimpleDragon.GreenRetrofitModel constructor", 3),
        Target(342, "GreenRetrofitModel.__init__", "function", "epsimple-model-core-342-e8bd64b7", "exception", "immutable-defensive-copy-constructor-with-explicit-weather-e8bd64b7", "GonieGonie.SimpleDragon.GreenRetrofitModel constructor", 3),
        Target(345, "GreenRetrofitModel.address", "function", "epsimple-model-core-345-df358686", "exception", "readonly-address-with-explicit-weather-selection-df358686", "GonieGonie.SimpleDragon.GreenRetrofitModel.Address", 3),
        Target(346, "GreenRetrofitModel.area", "function", "epsimple-model-core-346-bf31ed3c", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.Area", 4),
        Target(347, "GreenRetrofitModel.averaged_exteriorfloor_Uvalue", "function", "epsimple-model-core-347-ef752eff", "exception", "nullable-construction-filter-rather-than-singleton-identity-regulation-ef752eff", "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageExteriorFloorUValue", 5),
        Target(348, "GreenRetrofitModel.averaged_exteriorroof_Uvalue", "function", "epsimple-model-core-348-871c1b93", "exception", "nullable-construction-filter-rather-than-singleton-identity-regulation-871c1b93", "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageExteriorRoofUValue", 5),
        Target(349, "GreenRetrofitModel.averaged_exteriorwall_Uvalue", "function", "epsimple-model-core-349-13f93b86", "exception", "nullable-construction-filter-rather-than-singleton-identity-regulation-13f93b86", "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageExteriorWallUValue", 5),
        Target(350, "GreenRetrofitModel.averaged_infiltration", "function", "epsimple-model-core-350-4046cce9", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageInfiltration", 5),
        Target(351, "GreenRetrofitModel.averaged_lightdensity", "function", "epsimple-model-core-351-695c215a", "exception", "nullable-light-density-excluded-from-weight-denominator-695c215a", "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageLightDensity", 5),
        Target(352, "GreenRetrofitModel.averaged_window_Uvalue", "function", "epsimple-model-core-352-235f45cc", "exception", "native-window-projection-also-includes-glass-doors-235f45cc", "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageWindowUValue", 5),
        Target(353, "GreenRetrofitModel.climate", "function", "epsimple-model-core-353-27c207a5", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.Weather.ClimateRegion", 3),
        Target(354, "GreenRetrofitModel.exteriorfloors", "function", "epsimple-model-core-354-61333306", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorFloors", 4),
        Target(355, "GreenRetrofitModel.exteriorroofs", "function", "epsimple-model-core-355-9ba0cb63", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorRoofs", 4),
        Target(356, "GreenRetrofitModel.exteriorwalls", "function", "epsimple-model-core-356-428acddc", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorWalls", 4),
        Target(357, "GreenRetrofitModel.exteriorwindows", "function", "epsimple-model-core-357-d363d717", "exception", "native-window-projection-also-includes-glass-doors-d363d717", "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorWindows", 4),
        Target(359, "GreenRetrofitModel.from_grjson", "function", "epsimple-model-core-359-696d04c3", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GrmReader.ReadFile(string, SimpleDragonDatabase?)", 8),
        Target(360, "GreenRetrofitModel.get_unique_fenestration_constructions", "function", "epsimple-model-core-360-0963ad71", "exception", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-0963ad71", "GonieGonie.SimpleDragon.GreenRetrofitModel.FenestrationConstructions", 7),
        Target(361, "GreenRetrofitModel.get_unique_materials", "function", "epsimple-model-core-361-ecb20cb3", "exception", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-ecb20cb3", "GonieGonie.SimpleDragon.GreenRetrofitModel.Materials", 7),
        Target(362, "GreenRetrofitModel.get_unique_profiles", "function", "epsimple-model-core-362-13af13a1", "exception", "database-resolved-zone-profiles-rather-than-derived-overwrite-map-13af13a1", "GonieGonie.SimpleDragon.GreenRetrofitModel.Zones with SimpleDragonDatabase.Profiles", 7),
        Target(363, "GreenRetrofitModel.get_unique_surface_constructions", "function", "epsimple-model-core-363-a05748b1", "exception", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-a05748b1", "GonieGonie.SimpleDragon.GreenRetrofitModel.SurfaceConstructions", 7),
        Target(364, "GreenRetrofitModel.north_axis", "function", "epsimple-model-core-364-fc0d665a", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.NorthAxis", 3),
        Target(365, "GreenRetrofitModel.run", "function", "epsimple-model-core-365-bf192ec8", "exception", "async-runner-and-result-builder-diagnostic-boundary-bf192ec8", "GonieGonie.EnergyPlus.Runtime.EnergyPlusRunner.RunAsync(EnergyPlusRunRequest, CancellationToken) and GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)", 10),
        Target(366, "GreenRetrofitModel.source_system", "function", "epsimple-model-core-366-b2b62b80", "exception", "immutable-explicit-catalog-rather-than-computed-plus-unvalidated-merge-b2b62b80", "GonieGonie.SimpleDragon.GreenRetrofitModel.SourceSystems and GonieGonie.SimpleDragon.GreenRetrofitModel.SupplySystems", 6),
        Target(367, "GreenRetrofitModel.terrain", "function", "epsimple-model-core-367-152775fe", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.Weather.Terrain", 3),
        Target(368, "GreenRetrofitModel.to_dragon", "function", "epsimple-model-core-368-5e2e21f3", "exception", "nonthrowing-aggregate-conversion-result-with-diagnostics-5e2e21f3", "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)", 9),
        Target(369, "GreenRetrofitModel.to_idf", "function", "epsimple-model-core-369-e8d26d72", "exception", "native-idf-document-conversion-result-with-diagnostics-e8d26d72", "GonieGonie.SimpleDragon.GreenRetrofitConverter.ToIdfDocument(GreenRetrofitModel, GreenRetrofitConversionOptions?, IddSchema?, EnergyModelIdfOptions?)", 9),
        Target(370, "GreenRetrofitModel.vintage", "function", "epsimple-model-core-370-e739b9d6", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.Vintage", 3),
        Target(371, "GreenRetrofitModel.weather", "function", "epsimple-model-core-371-acd72fe8", "equivalent", "not_applicable", "GonieGonie.SimpleDragon.GreenRetrofitModel.Weather.WeatherLocation", 3),
        Target(372, "GreenRetrofitModel.weather_filepath", "function", "epsimple-model-core-372-fa174585", "exception", "epw-filename-with-caller-owned-directory-resolution-fa174585", "GonieGonie.SimpleDragon.WeatherSelection.EpwFileName and ResolveEpwPath(string)", 3),
        Target(387, "InvalidAddressError", "class", "epsimple-model-core-387-aee12b8f", "exception", "lookup-diagnostic-rather-than-address-exception-aee12b8f", "GonieGonie.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and GonieGonie.SimpleDragon.WeatherSelection", 1),
        Target(388, "address_to_weather", "function", "epsimple-model-core-388-6e86f546", "exception", "typed-nonthrowing-weather-selection-result-6e86f546", "GonieGonie.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and GonieGonie.SimpleDragon.WeatherSelection", 1),
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
        "sha256:91966d6a5847ab43c5fe433a3544f2eeaf34ca337b79310510b565d67b670dcc",
        "sha256:7f5a6eefc766787f4f36e039170753a27500990f67f20c7e2c593b8b18db34a3",
        "sha256:a78cedcf95eadb5ebb24b23ba629e88adacb6c818f551b7f2e68817f5562ab00",
        "sha256:6ae9cc18c31ffbe75df76146503584b88ebfb802ab923a5794e7a6a0efbaf1ee",
        "sha256:27901f372153a5648de783d4c599c240d99de698daa6269a22ff63a53eb5aa4e",
        "sha256:4cbf8a46f944500ab181c899db7fc11ab7ee6c81f4dfeb1d5f775898055b17b0",
        "sha256:438598353dcc2c45bc3abac65bcf5fd7f8ac08c460371620e8a45aed011b3bb2",
        "sha256:3606e82450c3dfd3c53c8673817109bf7a43c3eec1d28d149b24cc4a228b079b",
        "sha256:1e78a389fb6ce39778ad3822300e874f192123e44b0a4f8f31ee2dfe7493778e",
        "sha256:629b9d25e55ee270e1bbbdc8ded2acdcbc01cb4a145034eeb4d347109032f4a1",
        "sha256:ef2c4649bbbfafb7fe160e0f1f42ebeae2e724eb74cfe6794575e3ec35879375",
        "sha256:3355684d51d8043debe8d8b74146ecd4e163d90d5433512ec5349ec53dde9534",
        "sha256:ed08886e9e31de92e46afa676ac83ad64a7d1ac3caea13a8d3145b0dcb047445",
        "sha256:a27457bf2156b6115f927aad6cafa5d8d83fc6cd6d3057dfe856bc7ce2a7352a",
        "sha256:b3a6f2bf0049bbb9bc3e5d2747a36e992873c5223ceacadf4ac3a17d862ffb7b",
        "sha256:b3758dff0929f737b1a9a4e59dbc7330ad33652b38da9a0ca71fb58d383791bb",
        "sha256:bd98387c9f4ab3e00d532104b2dc76b6556268d9fd71669ce1d37335e09dd901",
        "sha256:5ec0fdb79bb85676e270ea6fad5dfdf9b09f3fd1c027b448c17aceb1aeb87ecc",
        "sha256:df37e9f5ff6b37e458ae835f44ec7a65cff36a5f4cd1edd2cb31b4e41e0f39f9",
        "sha256:149bc61ad7ef2e941973e2304d3c78c487a8be68141c193e10cf199b76eee1e6",
        "sha256:e73b1aae6d92a4cb2241d0cbcc4dc8a2e2b2fc1daba47f8aeb2b22c592427835",
        "sha256:ed1d3865556a0cd0d27f28358fe1473bdd7117cb029b56bf78e569e27aa77fc6",
        "sha256:db72c809fe30726e41ffac1c5aeee600c93f1bbcbe7ff982738d0192d7970716",
        "sha256:04a0ebd4fb7b9c440b5c8c161da36ae8fc07525b102340af155d124677df4fcf",
        "sha256:6fa852bd5a62442fbb9b1ab8c89aa133f113f3e1cdf5fa3a914ebe1232b66fc6",
        "sha256:3a54becfd7fc359c779523f725da25f3f3abba0f58b8e5a300ef0059988b2c10",
        "sha256:796e1480c69e82647edc62904d00f873a4d8ce9cfadd219d2f33705a7e66cb9c",
        "sha256:8336273355dcc59374290f258044a3bc2dc26bbfbbdf4aeaad4436bf14b76a5a",
        "sha256:4741eabb3d7e200f1355e9d31cb934122cd4af6bfa70312181f3e56a47a605ab",
        "sha256:d84e27b0821db80adca8e9c138f4b6851bd91f2bf1dcb994b4058d92d0c59e8f",
        "sha256:d228b855c55307f7ec1afcd29ea0d54b78c88d62c7fb65bd2a81241b26d5d152",
        "sha256:d28676e6e956b403b48eb9f9e41da72f072c2211612cd86dda7a560a30ea24ce",
        "sha256:5efe9e9cce55c7cae37e1fda8956d0816a5433092b332ff620654a1bb64ce820",
        "sha256:a0c5f3d089872bb9c9897919262f079c3c6de36a1b63a7a13de999537b29dec3",
        "sha256:c896a2eccafc626b3496465edf2eaf2bc6a4282ba7c6b3db20b30900a2ee7331",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:e1ef66cca7150c99c1bcbb5c1c5fc20191769b8d09b5687a68ad0946715b2a35", // epsimple-model-core-337-1a4029a1
        "sha256:099b302a6dbda8f1233acb091d7b0ca575483501915324b34ca7032283d3cfc6", // epsimple-model-core-338-fbfb5af8
        "sha256:46341bc4f0b3f1a55c47e73fad340e1dcd89f42cf8cc51a68374f9a006513d8e", // epsimple-model-core-339-3ed10042
        "sha256:d3ed844e918d2e5d955fa4c3ebf9195dbfa4b40c5b3511d1c2f9163a1d60a31b", // epsimple-model-core-340-328cf73b
        "sha256:b752ed7c522abd17909db6ed5d2d34755c68cd968a8db0f6e7d62d3d620730b1", // epsimple-model-core-341-fb39a800
        "sha256:6ef4dc094bdbd960087eb03743ef33f19989b0fcebed5a7d676e2ebae5579524", // epsimple-model-core-342-e8bd64b7
        "sha256:a8f1094c373fecd055ad3a4f9640b2ccf1594841a143367282b06313bf769e94", // epsimple-model-core-345-df358686
        "sha256:45bbe1b2de793c0fe8e87c21ebeee34258a9fcf86471fb1e4c279980dc37fdbb", // epsimple-model-core-346-bf31ed3c
        "sha256:6629d805fbb99464b27b65d906dcd929353bf713ce5581bbfb7a0e83c15f8ab0", // epsimple-model-core-347-ef752eff
        "sha256:40819d73efb81511b51f88440484a7bd672a746b7a6b166ba4750c50ddb190ac", // epsimple-model-core-348-871c1b93
        "sha256:15f056c61c99e65a5a6260cf2d7acce05650eda41eb2a0ca5941b6b224cde4aa", // epsimple-model-core-349-13f93b86
        "sha256:b9c94f25d34c880e05e48a0e46a5bd6efd8c8b962a1451aeae5b02b614e1fe31", // epsimple-model-core-350-4046cce9
        "sha256:838bdcb39b21287955cd598f8d36d1058540032c6803aa11f00b8bde882c805a", // epsimple-model-core-351-695c215a
        "sha256:2b493460b475deba3c5d38d63aee83cb23bc68c7b76c8ba71e7d324eabcb396e", // epsimple-model-core-352-235f45cc
        "sha256:665dc0d05f0ad0189a982d84212ebfd49dc2e5250dd04bec8194c44e40c19869", // epsimple-model-core-353-27c207a5
        "sha256:5f01d0844d3e9aa7590e1b63301c9e7cdb21f52502fecc68e292af4a0e86e72c", // epsimple-model-core-354-61333306
        "sha256:c78a53e56311d7164479fbcf2a3c38cbc54e774f0965862ef849120bf7649bc3", // epsimple-model-core-355-9ba0cb63
        "sha256:12564ec2b4c84dcd6b60477f7b6ce5db17484fb932b373c7aa21bf5f8464fdee", // epsimple-model-core-356-428acddc
        "sha256:79eb7aef87a98e980c4bceed6af1c6d29bfa7e1e59d09264f3d47b4cf602a9e1", // epsimple-model-core-357-d363d717
        "sha256:704770692af36e8e373a752081d3b4cf7559c5354cf6f83c620f937cdb87c8ee", // epsimple-model-core-359-696d04c3
        "sha256:9101f9be325f1352bf240633d4d598bbbcc2883509392175561e347b515079f0", // epsimple-model-core-360-0963ad71
        "sha256:b83cf9bbc5429fa6b911a7ec703aeff40089bdae633fd0aaf94201109bc9189d", // epsimple-model-core-361-ecb20cb3
        "sha256:7c59aadd08910d8fe75d29b97d14b9eb3f04d4209317ce23a1932c0f2597ddc5", // epsimple-model-core-362-13af13a1
        "sha256:9734a07e27a28eb4cb63306c8fd98da0970892f920ba8a1b9a263f1c4e13af37", // epsimple-model-core-363-a05748b1
        "sha256:7e2af092dd39d79bec060082c60e3a45ae55880a3c92f65e99dbc3ea9ecdaf07", // epsimple-model-core-364-fc0d665a
        "sha256:8499ee372f3f5b31cb97124e128189eea75289485e0cfedc345ffd77ff2cc2ec", // epsimple-model-core-365-bf192ec8
        "sha256:ec993c2fc6f3f39c32e91d13516b37b482df6133ef50e416dab614cb776d4890", // epsimple-model-core-366-b2b62b80
        "sha256:b7284bb5d1951519f12dd398ab8a4a395e8c3cf4749dc13b023846ef30166ac0", // epsimple-model-core-367-152775fe
        "sha256:cf621e5ee19c4abde8d875e3696d6a8a391392ed4c9a39ab043726279a3935d7", // epsimple-model-core-368-5e2e21f3
        "sha256:b248d362df4eed9ee0bdca2776748466d10920c700e8c41385a4fc84c096cd3f", // epsimple-model-core-369-e8d26d72
        "sha256:b26477ae76dd3d2a786d572410905e97dcad428aba16706cfeda923969443c50", // epsimple-model-core-370-e739b9d6
        "sha256:d432f31cfcc8eb72fa76fe482ebb58b8df58cf376f07907e77220635fe72803b", // epsimple-model-core-371-acd72fe8
        "sha256:4b714e7d385a4bb24636fb912839446bd751c8f190205f7665d2e7b3b60fdc17", // epsimple-model-core-372-fa174585
        "sha256:f5c47ccf28abf6dfa8661e27f0d2e4abc4186a13fbeb31f3afe73aa685022072", // epsimple-model-core-387-aee12b8f
        "sha256:377629baeaf441b387ecec0988f66385bf9a1f9e3845c6f130ef6c4ede5e112e", // epsimple-model-core-388-6e86f546
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
