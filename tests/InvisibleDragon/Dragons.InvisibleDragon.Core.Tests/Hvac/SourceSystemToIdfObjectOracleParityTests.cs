using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.InvisibleDragon.Hvac;
using Dragons.InvisibleDragon.Idd;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Hvac;

public sealed class SourceSystemToIdfObjectOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-hvac-source-system-to-idf-object-oracle.json";
    private const string OracleSchema =
        "dragons.python-reference.dragon-hvac-source-system-to-idf-object.v1";
    private const int OracleByteLength = 3_927_710;
    private const string OracleSha256 =
        "sha256:2fbc3ad2d810dee6b3e88f8b6e8c119e8ce709abf0c534233343e486f7bf9c7f";
    private const string CasesSha256 =
        "sha256:755e2115db65a100fe1b4249c4b4507719e5083aa2ea22939955a7aae53c5c07";
    private const string GeneratorRepositoryPath =
        "tools/python-reference/generate_dragon_hvac_source_system_to_idf_object_oracle.py";
    private const int GeneratorByteLength = 66_475;
    private const string GeneratorSha256 =
        "sha256:f8c3a031304554ecd43381867188c29bf38c2ce0ebf4bf284c394792f7817159";
    private const string PythonValidatorRepositoryPath =
        "tests/PythonReference/test_dragon_hvac_source_system_to_idf_object_oracle.py";
    private const int PythonValidatorByteLength = 26_934;
    private const string PythonValidatorSha256 =
        "sha256:b86d4c8a2ea60de84a9d982fbf901f23f76c44e4e2216532cb4567baae802d0e";

    private const int ExpectedCaseCount = 20;
    private const int ExpectedPythonObjectCount = 519;
    private const int ExpectedPythonFieldCount = 18_670;
    private const string UpstreamCommit =
        "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string InventorySha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamPath = "src/idragon/dragon/hvac.py";
    private const string UpstreamSourceSha256 =
        "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0";
    private const string UpstreamAstSha256 =
        "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Hvac.SourceSystemToIdfObjectOracleParityTests.MatchesPinnedPythonSourceSystemFamilyThroughNativeLegacyContext";
    private static bool DiscoverPins => string.Equals(
        Environment.GetEnvironmentVariable("DRAGONS_DISCOVER_SOURCE_SYSTEM_PINS"),
        "1",
        StringComparison.Ordinal);
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        WriteIndented = true,
    };

    private const string IddOracleRepositoryPath =
        "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz";
    private const int IddOracleByteLength = 585_482;
    private const string IddOracleSha256 =
        "sha256:f2dfc27d39f788f945ef5cc3b79ffce2a516a568075717bd67088d900a75c705";
    private const string IddOracleSchema = "dragons.energyplus-idd-schema.v1";
    private const string EnergyPlusVersion = "24.2.0";
    private const string EnergyPlusBuild = "94a887817b";
    private const string EnergyPlusIddSourceSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const int EnergyPlusIddSourceByteLength = 4_556_412;

    private static readonly SymbolBinding[] ExpectedSymbols =
    {
        new(644, "AbsorptionChiller.to_idf_object",
            "sha256:17d5fb8afe2207a9772bc47b4f5424d740b3df76301f04c9155c0fbd725af969",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:235dd3954501399871a0317fb9665091c192830665e9f95baae7eb9a3d80823b",
            "dragon-hvac-absorption-chiller-to-idf-object-17d5fb8a",
            "legacy-context-absorption-chiller-idf-emission",
            "AbsorptionChiller.ToIdfObjects legacy context",
            "Dragons.InvisibleDragon.Hvac.AbsorptionChiller.ToIdfObjects"),
        new(655, "Boiler.to_idf_object",
            "sha256:b63a454be07eaaee80563cbac25cd78a3fb632e462e2ea37aed7906c2967a7ae",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:416b84930ac077833ccee544e07d01ea1e542df536d32b8a1d7c18b4a94725ed",
            "dragon-hvac-boiler-to-idf-object-b63a454b",
            "compact-native-boiler-idf-emission",
            "Boiler.ToIdfObjects",
            "Dragons.InvisibleDragon.Hvac.Boiler.ToIdfObjects"),
        new(656, "Boiler.to_idf_object_as_generator",
            "sha256:d239b10e14f899ec4f7d9d914e7322fd684d3cfe5096609119f32eef9dc79aa0",
            "sha256:b39bcfffa903f90ee98ddd5d79d4b6827d2e526aaa6acabe5667e446c80794c3",
            "sha256:32eebbda47034344f145d801a729648469cd7b24e0af847d97c1f9b6b7294cf2",
            "dragon-hvac-boiler-to-idf-object-as-generator-d239b10e",
            "fresh-native-boiler-generator-idf-emission",
            "Boiler.ToIdfObjects with generator demand connection",
            "Dragons.InvisibleDragon.Hvac.Boiler.ToIdfObjects"),
        new(660, "Chiller.to_idf_object",
            "sha256:fc75129f85debd982652240620407bcb408a73fcf5fef197871599da771e34d3",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:72ef316133dec589de7deec328090b0d097b8370726a5c360d73953dd6dc9f25",
            "dragon-hvac-chiller-to-idf-object-fc75129f",
            "legacy-context-chiller-idf-emission",
            "Chiller.ToIdfObjects legacy context",
            "Dragons.InvisibleDragon.Hvac.Chiller.ToIdfObjects"),
        new(663, "ClosedSingleSpeedCoolingTower.to_idf_main_object",
            "sha256:0e14065ae1ca788b3219a54f5d1ae41d7783e0dd6497667cf583e7387e0396d8",
            "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
            "sha256:330c3494967559c366002476ef010eb8be6c27fde0e918832932f4c2daeb6162",
            "dragon-hvac-closed-single-speed-cooling-tower-to-idf-main-object-0e14065a",
            "cooling-tower-context-closed-single-speed-main-idf-emission",
            "ClosedSingleSpeedCoolingTower.ToIdfObjects",
            "Dragons.InvisibleDragon.Hvac.ClosedSingleSpeedCoolingTower.CreateMainObject"),
        new(666, "ClosedTwoSpeedCoolingTower.to_idf_main_object",
            "sha256:30402683c6a9db760ad1727995d72c8357b93cf5704625779e5ce43b907739ae",
            "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
            "sha256:13c013239561f33e2d0ae10cde93531feea0f97a1557337402e2afe8407e2a0d",
            "dragon-hvac-closed-two-speed-cooling-tower-to-idf-main-object-30402683",
            "cooling-tower-context-closed-two-speed-main-idf-emission",
            "ClosedTwoSpeedCoolingTower.ToIdfObjects",
            "Dragons.InvisibleDragon.Hvac.ClosedTwoSpeedCoolingTower.CreateMainObject"),
        new(672, "CompressorType.to_idf_curve_object",
            "sha256:8ca6c2d070a534718d90fe79dff5d8a1e015593a0551a5a53ec3bf1c3e932d81",
            "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
            "sha256:eba2dfb849d0251170f777cb131385bbaf7316d8231cba3d096d241ce9ddce00",
            "dragon-hvac-compressor-type-to-idf-curve-object-8ca6c2d0",
            "chiller-context-compressor-curve-idf-emission",
            "Chiller.ToIdfObjects compressor curve slice",
            "Dragons.InvisibleDragon.Hvac.Chiller.CreatePerformanceCurves"),
        new(684, "CoolingTower.to_idf_main_object",
            "sha256:4615e08c6ec284f9bac80d2a5f25beca2b9706f4c706e0b47cf27ab35c2c5915",
            "sha256:679b45a374ed222434707e448b38c110efb7b0d13bc0089cadaaf661a48c7708",
            "sha256:d534464a3d86dfeb1f92e18bf2296fb90a71ce3810ab72675ff520fac00f4ce1",
            "dragon-hvac-cooling-tower-to-idf-main-object-4615e08c",
            "contextual-native-cooling-tower-main-idf-contract",
            "CoolingTower main-object contract in chiller context",
            "Dragons.InvisibleDragon.Hvac.CoolingTower.CreateMainObject"),
        new(685, "CoolingTower.to_idf_object",
            "sha256:74287ab5af4712528e239034183e43122280dcf9760ebece16161e93c629c762",
            "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
            "sha256:77fa14ec4670bce06dd78b19a08c5be26bc01e0d947ed0ade6155954879d6b3f",
            "dragon-hvac-cooling-tower-to-idf-object-74287ab5",
            "legacy-context-cooling-tower-idf-emission",
            "CoolingTower.ToIdfObjects legacy context",
            "Dragons.InvisibleDragon.Hvac.CoolingTower.ToIdfObjects"),
        new(743, "HeatPump.to_idf_object",
            "sha256:b8cb28ab0ec6d2775a69548b0b5d7983afa38e0f980ec1e1835d40ccd1edacb1",
            "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
            "sha256:601ab95c68822d4e94a03062618d0a29ab4b7c0c8f529742d9e1bd99ed850311",
            "dragon-hvac-heat-pump-to-idf-object-b8cb28ab",
            "compact-native-heat-pump-idf-emission",
            "HeatPump.ToIdfObjects",
            "Dragons.InvisibleDragon.Hvac.HeatPump.ToIdfObjects"),
        new(746, "OpenSingleSpeedCoolingTower.to_idf_main_object",
            "sha256:102bccd9091484e0f915dc24010d22c22a91c69b95a17e10f44ab7d6b189e61f",
            "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
            "sha256:6f24857ae8cda107880f2bf123e2401b2c1ab4ffffdf3b224195350b3465bfb5",
            "dragon-hvac-open-single-speed-cooling-tower-to-idf-main-object-102bccd9",
            "cooling-tower-context-open-single-speed-main-idf-emission",
            "OpenSingleSpeedCoolingTower.ToIdfObjects",
            "Dragons.InvisibleDragon.Hvac.OpenSingleSpeedCoolingTower.CreateMainObject"),
        new(749, "OpenTwoSpeedCoolingTower.to_idf_main_object",
            "sha256:7fd75338aa5a98323eb0d3cfeac729d921c00f95e91f7e03cfddf4b2b885e736",
            "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
            "sha256:a4e24f89a146eae7181177115cce1a89842869e9bd70e9f54e2b45e8bc6ead73",
            "dragon-hvac-open-two-speed-cooling-tower-to-idf-main-object-7fd75338",
            "cooling-tower-context-open-two-speed-main-idf-emission",
            "OpenTwoSpeedCoolingTower.ToIdfObjects",
            "Dragons.InvisibleDragon.Hvac.OpenTwoSpeedCoolingTower.CreateMainObject"),
        new(788, "SourceSystem.to_idf_object",
            "sha256:63aa5eab420418dc4467359ae79d5b1b0b59f1a0501e6e5953039b3a3adfb57b",
            "sha256:d62b0f5a2745a3f0d6f1ace245fbc66899d0e8953e93173c8f4d815eec741a50",
            "sha256:d534464a3d86dfeb1f92e18bf2296fb90a71ce3810ab72675ff520fac00f4ce1",
            "dragon-hvac-source-system-to-idf-object-63aa5eab",
            "contextual-native-source-system-idf-contract",
            "SourceSystem.ToIdfObjects abstract contract",
            "Dragons.InvisibleDragon.Hvac.SourceSystem.ToIdfObjects"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-hvac-source-system-to-idf-object.absorption-chiller.alternate-setpoint", "AbsorptionChiller.to_idf_object", "sha256:5c71ceb16217b251c7282d4b4a0ca6a620e16cd2b2143013df889721c2cea768"),
        new("dragon-hvac-source-system-to-idf-object.absorption-chiller.representative", "AbsorptionChiller.to_idf_object", "sha256:24c044caaefef0b4eb7ab83a5ef24414e80b223bb4bfc63f74404be394d38fd4"),
        new("dragon-hvac-source-system-to-idf-object.boiler-generator.topology", "Boiler.to_idf_object_as_generator", "sha256:2e69baa1fe4fe84418a37edc480c413659f08a90edf1250f2c4a0fa198edb9e6"),
        new("dragon-hvac-source-system-to-idf-object.boiler.autosized-natural-gas", "Boiler.to_idf_object", "sha256:d84a073bf7a1e735bf3af15988a26ee2933b77e29b706af796a4a94060ca47b7"),
        new("dragon-hvac-source-system-to-idf-object.boiler.explicit-propane", "Boiler.to_idf_object", "sha256:774ec151da44e025ebb6d607b2569111fb93243c5fe2a1c55350c66da30654c1"),
        new("dragon-hvac-source-system-to-idf-object.chiller.alternate-setpoint", "Chiller.to_idf_object", "sha256:e35658f7a4083b465179540af9cf760ddc8b608cfaff83b70eca9a6f66318011"),
        new("dragon-hvac-source-system-to-idf-object.chiller.representative", "Chiller.to_idf_object", "sha256:047d102db517aad7aa28a9d905ed2c1fc66241a7367c64db4349f3594fec9319"),
        new("dragon-hvac-source-system-to-idf-object.compressor.reciprocating", "CompressorType.to_idf_curve_object", "sha256:48349b86be34f8e4c49219912912a6fb2f4551d22fb8695e4d37950f7845e6c1"),
        new("dragon-hvac-source-system-to-idf-object.compressor.screw", "CompressorType.to_idf_curve_object", "sha256:7bd3100ca6041d3ecb230da70caff7dce7752c551630c9200124700aae3a3a71"),
        new("dragon-hvac-source-system-to-idf-object.compressor.turbo", "CompressorType.to_idf_curve_object", "sha256:9a2e805f0761f2859c1f2b2baca5a8023586b8e4d443116d892384d2c48da2ff"),
        new("dragon-hvac-source-system-to-idf-object.cooling-tower-full.closed-two-speed", "CoolingTower.to_idf_object", "sha256:106fdf88789a60b3f4d1d8066bcc8ea56db122213809ccf574f0d57fdd04ea06"),
        new("dragon-hvac-source-system-to-idf-object.cooling-tower-full.open-single-speed", "CoolingTower.to_idf_object", "sha256:67a93158f8debcaa611c61ee000b9ec74d78328becab32c3d4393d6b71c9dc25"),
        new("dragon-hvac-source-system-to-idf-object.cooling-tower-main.abstract-contract", "CoolingTower.to_idf_main_object", "sha256:096dfdc0db0a7f55451a2a0b70abf49fad5776fb2c4d215bffcb459a3fcc18fd"),
        new("dragon-hvac-source-system-to-idf-object.cooling-tower-main.closed-single-speed", "ClosedSingleSpeedCoolingTower.to_idf_main_object", "sha256:653bf79e5b03fa8bfa38f56ce4b29ccb57cdd602407bb77c37a4f23db6874744"),
        new("dragon-hvac-source-system-to-idf-object.cooling-tower-main.closed-two-speed", "ClosedTwoSpeedCoolingTower.to_idf_main_object", "sha256:9e1aea610dc00251cd8febe12f8e1abed12abb06df8dea3fd4784b451d89a808"),
        new("dragon-hvac-source-system-to-idf-object.cooling-tower-main.open-single-speed", "OpenSingleSpeedCoolingTower.to_idf_main_object", "sha256:5a0d12780bfa8680bcc7392fbc14db40feb0f7f6a77bcee28e85c815585118bf"),
        new("dragon-hvac-source-system-to-idf-object.cooling-tower-main.open-two-speed", "OpenTwoSpeedCoolingTower.to_idf_main_object", "sha256:fa85f9673cdfc4d1dccb2374f7bac51ab52a443098c089658bafa6b191e544dd"),
        new("dragon-hvac-source-system-to-idf-object.heat-pump.explicit-capacities", "HeatPump.to_idf_object", "sha256:932ffe33dcc60f78fd4b5d9790d3cb46445b195daa1a9a72e610168719584014"),
        new("dragon-hvac-source-system-to-idf-object.heat-pump.representative-autosize", "HeatPump.to_idf_object", "sha256:cc5420bb6e32d3031f7f50c7e3f51d8ab5b0c3a73bc07c12b8d9acdb059c5c1d"),
        new("dragon-hvac-source-system-to-idf-object.source-system.abstract-contract", "SourceSystem.to_idf_object", "sha256:d3a57cd9f36787a09e87def0892c959494f4637bd3513dcf610dbf37e74caac6"),
    };

    private static readonly NativeCaseExpectation[] ExpectedNativeCases =
    {
        new(ExpectedCases[0].CaseId, 580, 2_916, 18, 2, 0, 13, "sha256:019f64e4ecba016ed4c49f61fabc1b1ac3420211e7d2731e02e47d772709951d"),
        new(ExpectedCases[1].CaseId, 598, 2_915, 16, 2, 0, 13, "sha256:f8ce4f613700f24e41ab11710072b9588cbf3ef5c25f782bf1eb1760fb4a4790"),
        new(ExpectedCases[2].CaseId, 199, 988, 2, 0, 0, 20, "sha256:c8753ca8ef5822aa9d4afc15935a7eed0700b1bb5eb52f7378c1dd9ae244595b"),
        new(ExpectedCases[3].CaseId, 190, 951, 2, 0, 0, 4, "sha256:cf4dbaa45414426a0e4541d8ea1a95607acb34c6b1633528c4c22354ff975bd6"),
        new(ExpectedCases[4].CaseId, 190, 951, 2, 0, 0, 4, "sha256:8592d05da2e2e1eabab03a58d4950b46916e3eb4d07c21d612b41c87e62c6809"),
        new(ExpectedCases[5].CaseId, 399, 1_944, 28, 2, 0, 9, "sha256:aabdcfeea723476cb8f5a6e92eaa66d17eb349134513ae833b580b0e31f9065b"),
        new(ExpectedCases[6].CaseId, 432, 1_943, 28, 2, 0, 9, "sha256:09ba977e040ee0214fdfc3611e364910dddc57b1cab65df4bcbe5f4d610429fd"),
        new(ExpectedCases[7].CaseId, 28, 6, 8, 0, 0, 0, "sha256:7829c0ef98e789b96193a6f50044b76f419c2f5acc000c855462ff8d3411fd1c"),
        new(ExpectedCases[8].CaseId, 37, 6, 9, 0, 0, 0, "sha256:f3521bed2db0db8efc445ae34dd58d21e654fd4b42649f3186445b860dabfe74"),
        new(ExpectedCases[9].CaseId, 28, 6, 8, 0, 0, 0, "sha256:d2e138b3e6acaac1358fbd6c1faf5db816bf23c61f9e707e494984aacf224d95"),
        new(ExpectedCases[10].CaseId, 189, 978, 7, 1, 0, 5, "sha256:26c89c7b4e41c9b6e91b408e43d832d8e5500f7d9f5560938b33a3f0ce351b75"),
        new(ExpectedCases[11].CaseId, 207, 977, 5, 1, 0, 5, "sha256:40633364659f01c972cb46cca5aa332d1ebc51f7357d4732decbe3702ff74151"),
        new(ExpectedCases[12].CaseId, 0, 0, 0, 0, 0, 0, "sha256:4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945"),
        new(ExpectedCases[13].CaseId, 12, 1, 0, 0, 0, 0, "sha256:3e8c203f2888c7939e43cfc8da9670f3fbc17ca1329c211cfdbc3684a5dbf826"),
        new(ExpectedCases[14].CaseId, 20, 1, 2, 0, 0, 0, "sha256:d78baabca8e649fd099c874fa8981d334bfb54d65265558d21c9cedaba899196"),
        new(ExpectedCases[15].CaseId, 38, 0, 0, 0, 0, 0, "sha256:65662ff9b2bb9674d2cbd6638b8f73a825de42e08f281352e38d4d44f77734bf"),
        new(ExpectedCases[16].CaseId, 45, 0, 0, 0, 0, 0, "sha256:abded3157940955995441f70a92f189b35b731a1243c7c20d89e00cc4841d1ea"),
        new(ExpectedCases[17].CaseId, 293, 78, 41, 0, 0, 0, "sha256:e30fe394a2e50f673c559cad3f54412ab587eeb3936a5cf8b27d95fa646ab42b"),
        new(ExpectedCases[18].CaseId, 293, 78, 41, 0, 0, 0, "sha256:c1b08fc306568c9ae9db75e9447806f807db87e2e4596f898fdc075e50986639"),
        new(ExpectedCases[19].CaseId, 0, 0, 0, 0, 0, 0, "sha256:4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945"),
    };

    private static readonly SourceBinding[] ExpectedSources =
    {
        new("idragon", "src/idragon/__init__.py", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618"),
        new("idragon.common", "src/idragon/common.py", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9"),
        new("idragon.constants", "src/idragon/constants.py", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"),
        new("idragon.dragon", "src/idragon/dragon/__init__.py", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52"),
        new("idragon.dragon.construction", "src/idragon/dragon/construction.py", "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"),
        new("idragon.dragon.hvac", UpstreamPath, UpstreamSourceSha256, UpstreamAstSha256),
        new("idragon.dragon.model", "src/idragon/dragon/model.py", "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"),
        new("idragon.dragon.profile", "src/idragon/dragon/profile.py", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef"),
        new("idragon.dragon.shape", "src/idragon/dragon/shape.py", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"),
        new("idragon.imugi", "src/idragon/imugi.py", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"),
        new("idragon.launcher", "src/idragon/launcher.py", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e"),
        new("idragon.utils", "src/idragon/utils.py", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452"),
    };

    private static readonly NativeArtifact[] NativeArtifacts =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs", 7_582, "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SourceSystems.cs", 18_027, "sha256:8d302f00514af53816cec9e5ba6b80a8214921b354d86bbbc4d581ec972e026e"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/Chillers.cs", 23_777, "sha256:7616675c6750b32ded6edd796576b347703a88103a91dff846ca5a08c65b72be"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/CoolingTowers.cs", 19_554, "sha256:007145933076386fcbc44daba8a28c63d3c5467bbd687c9da87f769c969e9d07"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/PlantLoopAssembler.cs", 10_538, "sha256:6a612a61c056583471cec4782ca4b64e6a94be6a177fec1ef0ee869ff3da25ee"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/CoolingPlantLoopAssembler.cs", 19_561, "sha256:0d571a9ad78caf2aa55913c19a86df041f12c8506b4999e7a03209d626aee594"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/IdfGenerationContext.cs", 3_801, "sha256:f7b6867f411575c6ce5e068df9568f76791ad7a715d41a5b4937528105f78574"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs", 50_764, "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905"),
    };

    private static readonly string[] ContextOnlyNotTargeted =
    {
        "AbsorptionChiller", "AbsorptionChiller.__init__", "Boiler", "Boiler.__init__",
        "Chiller", "Chiller.__init__", "ClosedSingleSpeedCoolingTower",
        "ClosedTwoSpeedCoolingTower", "CompressorType", "CoolingTower",
        "CoolingTower.__init__", "Fuel", "HeatPump", "HeatPump.__init__",
        "OpenSingleSpeedCoolingTower", "OpenTwoSpeedCoolingTower", "SourceSystem",
        "all-related-naming-properties", "all-related-enum-string-and-value-contracts",
    };

    private static readonly string[] UnresolvedBehavior =
    {
        "all-related-constructors-properties-and-enums",
        "invalid-domain-nonfinite-and-duck-typed-error-semantics",
        "GeothermalHeatPump", "native-DistrictHeating",
        "general-terminal-and-demand-connection-enrichment", "IdfObject", "IdfObject.__init__",
        "isolated-IdfObject-and-IDD-default-policy", "EnergyModel.to_idf",
        "parent-EnergyModel-global-order-deduplication-and-conflicts",
        "safe-native-screw-compressor-behavior", "active-absorption-runtime-parity",
    };

    [Fact]
    public void MatchesPinnedPythonSourceSystemFamilyThroughNativeLegacyContext()
    {
        OfficialIddOracle iddOracle = LoadOfficialIddOracle();
        using JsonDocument oracle = ReadPinnedOracle();
        Scenario[] scenarios = Enumerable.Range(0, ExpectedCases.Length)
            .Select(index => CreateScenario(index, iddOracle.NativeSchema))
            .ToArray();
        JsonElement[] cases = ValidateCorpus(oracle.RootElement, iddOracle);
        ValidateArtifactsAndNativeBindings();

        NativeObservation[] observations = cases
            .Select((item, index) => ExecuteNativeCase(
                ExpectedCases[index],
                item.GetProperty("python").GetProperty("facts"),
                scenarios[index],
                iddOracle))
            .ToArray();
        Assert.Equal(ExpectedCaseCount, observations.Length);
        Assert.Equal(ExpectedPythonObjectCount, observations.Sum(item => item.PythonObjectCount));
        Assert.Equal(ExpectedPythonFieldCount, observations.Sum(item => item.PythonFieldCount));

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "SOURCE_SYSTEM_NATIVE_PINS\n" + JsonSerializer.Serialize(
                    observations.Select(item => new
                    {
                        item.CaseId,
                        item.NativeCompactFieldCount,
                        item.BlankOrNoneOmissionCount,
                        DefaultOmissionCount = item.DefaultOmissions.Length,
                        TokenCaseNormalizationCount = item.TokenCaseNormalizations.Length,
                        NumericLexemeNormalizationCount = item.NumericLexemeNormalizations.Length,
                        ObjectRelocationCount = item.ObjectRelocations.Length,
                        item.NativeOutputSha256,
                    }),
                    DiscoveryJsonOptions));
        }

        AssertLegacyTopology(observations);
        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            NativeObservation[] selected = observations
                .Where(item => item.Symbol == symbol.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                ExpectedCases.Count(item => item.Symbol == symbol.Symbol),
                selected.Length);
            object receipt = CreateReceipt(symbol, selected);
            ValidateReceipt(JsonSerializer.SerializeToElement(receipt), symbol, selected);
            TrustedEvidenceRecorder.Record(
                symbol.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(OracleRepositoryPath));
        Assert.Equal(OracleByteLength, bytes.Length);
        Assert.Equal(OracleSha256, Sha256(bytes));
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        return JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
    }

    private static JsonElement[] ValidateCorpus(
        JsonElement root,
        OfficialIddOracle iddOracle)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(
            root,
            "cases",
            "cases_sha256",
            "consumer_contract",
            "runtime",
            "schema",
            "symbols",
            "upstream");
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));

        ValidateUpstream(root.GetProperty("upstream"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCaseCount, cases.Length);
        Assert.Equal(ExpectedCaseCount, ExpectedNativeCases.Length);
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId),
            ExpectedNativeCases.Select(item => item.CaseId));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId),
            cases.Select(item => RequiredString(item, "id")));
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId).OrderBy(item => item, StringComparer.Ordinal),
            ExpectedCases.Select(item => item.CaseId));

        int objectCount = 0;
        int fieldCount = 0;
        for (int index = 0; index < cases.Length; index++)
        {
            (int caseObjects, int caseFields) = ValidateCase(cases[index], ExpectedCases[index], iddOracle);
            objectCount += caseObjects;
            fieldCount += caseFields;
        }

        Assert.Equal(ExpectedPythonObjectCount, objectCount);
        Assert.Equal(ExpectedPythonFieldCount, fieldCount);
        return cases;
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(upstream, "commit", "inventory_sha256", "loaded_local_modules", "sources");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventorySha256, RequiredString(upstream, "inventory_sha256"));
        JsonElement[] sources = upstream.GetProperty("sources").EnumerateArray().ToArray();
        JsonElement[] modules = upstream.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(ExpectedSources.Length, sources.Length);
        Assert.Equal(ExpectedSources.Length, modules.Length);
        for (int index = 0; index < ExpectedSources.Length; index++)
        {
            SourceBinding expected = ExpectedSources[index];
            JsonElement source = sources[index];
            AssertKeys(source, "ast_sha256", "path", "source_sha256");
            Assert.Equal(expected.Path, RequiredString(source, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(source, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(source, "ast_sha256"));

            JsonElement module = modules[index];
            AssertKeys(module, "ast_sha256", "module", "path", "source_sha256");
            Assert.Equal(expected.Module, RequiredString(module, "module"));
            Assert.Equal(expected.Path, RequiredString(module, "path"));
            Assert.Equal(expected.SourceSha256, RequiredString(module, "source_sha256"));
            Assert.Equal(expected.AstSha256, RequiredString(module, "ast_sha256"));
        }
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "implementation",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        JsonElement dependencies = runtime.GetProperty("dependencies");
        Assert.Equal(10, dependencies.EnumerateObject().Count());
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("3.1.5", RequiredString(dependencies, "openpyxl"));
    }

    private static void ValidateSymbols(JsonElement symbolsElement)
    {
        JsonElement[] symbols = symbolsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            SymbolBinding expected = ExpectedSymbols[index];
            JsonElement symbol = symbols[index];
            AssertKeys(symbol, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(expected.Symbol, RequiredString(symbol, "symbol"));
            Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
            Assert.Equal("function", RequiredString(symbol, "kind"));
            Assert.Equal(expected.SymbolHash, RequiredString(symbol, "symbol_hash"));
            Assert.Equal(expected.SignatureHash, RequiredString(symbol, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(symbol, "body_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement contract)
    {
        AssertKeys(
            contract,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classification_basis",
            "classifications",
            "closure",
            "identity_encoding",
            "native_targets",
            "raw_field_encoding",
            "source_import_policy",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(contract.GetProperty("target_symbols"), ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal(
            "native source emitters return fresh result lists with pairwise-distinct fresh IDF objects and deterministic fields without captured source-state mutation; compact defaults and explicit generation context are bounded here as exception evidence",
            RequiredString(contract, "classification_basis"));
        Assert.Equal("booleans-only-no-id-or-address", RequiredString(contract, "identity_encoding"));
        Assert.Equal("complete-ordered-IDD-fields-with-typed-values", RequiredString(contract, "raw_field_encoding"));
        Assert.Equal(
            "external-temporary-copy-with-complete-twelve-module-audit",
            RequiredString(contract, "source_import_policy"));

        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement nativeTargets = contract.GetProperty("native_targets");
        string[] symbols = ExpectedSymbols.Select(item => item.Symbol).ToArray();
        AssertKeys(adaptations, symbols);
        AssertKeys(assertions, symbols);
        AssertKeys(classifications, symbols);
        AssertKeys(nativeTargets, symbols);
        foreach (SymbolBinding symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
            Assert.Equal(symbol.AssertionId, RequiredString(assertions, symbol.Symbol));
            Assert.Equal("exception", RequiredString(classifications, symbol.Symbol));
            Assert.Equal(symbol.NativeTarget, RequiredString(nativeTargets, symbol.Symbol));
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(closure, "context_only_not_targeted", "full_symbol_closure", "scope", "unresolved_behavior");
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.Equal(
            "bounded-common-valid-state-hvac-source-system-idf-emission",
            RequiredString(closure, "scope"));
        AssertStringArray(closure.GetProperty("context_only_not_targeted"), ContextOnlyNotTargeted);
        AssertStringArray(closure.GetProperty("unresolved_behavior"), UnresolvedBehavior);
    }

    private static (int ObjectCount, int FieldCount) ValidateCase(
        JsonElement item,
        CaseBinding binding,
        OfficialIddOracle iddOracle)
    {
        AssertKeys(item, "executor", "expected_dotnet", "id", "python", "symbol");
        Assert.Equal(binding.CaseId, RequiredString(item, "id"));
        Assert.Equal(binding.Symbol, RequiredString(item, "symbol"));
        Assert.Equal("hvac-source-system-to-idf-object", RequiredString(item, "executor"));
        SymbolBinding symbol = ExpectedSymbols.Single(value => value.Symbol == binding.Symbol);
        JsonElement expectedDotnet = item.GetProperty("expected_dotnet");
        AssertKeys(expectedDotnet, "adaptation", "outcome");
        Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotnet, "adaptation"));
        Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));

        JsonElement python = item.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        JsonElement facts = python.GetProperty("facts");
        AssertKeys(facts, "emission", "input_context");
        Assert.Equal(binding.FactSha256, CanonicalSha256(facts));
        ValidateInputContext(facts.GetProperty("input_context"), binding);
        return ValidateEmission(facts.GetProperty("emission"), binding, iddOracle);
    }

    private static void ValidateInputContext(JsonElement context, CaseBinding binding)
    {
        AssertKeys(
            context,
            "captured_state_scope",
            "source_state",
            "source_state_unchanged_after_two_emissions");
        bool abstractContract = IsAbstractCase(binding);
        Assert.Equal(
            abstractContract
                ? "abstract-method-descriptor-and-direct-body-return"
                : "properties-read-by-target-method-and-explicit-call-context",
            RequiredString(context, "captured_state_scope"));
        Assert.True(context.GetProperty("source_state_unchanged_after_two_emissions").GetBoolean());
        JsonElement[] state = context.GetProperty("source_state").EnumerateArray().ToArray();
        Assert.NotEmpty(state);
        foreach (JsonElement field in state)
        {
            AssertKeys(field, "name", "value");
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(field, "name")));
            ValidateEncodedValue(field.GetProperty("value"));
        }
    }

    private static (int ObjectCount, int FieldCount) ValidateEmission(
        JsonElement emission,
        CaseBinding binding,
        OfficialIddOracle iddOracle)
    {
        bool abstractContract = IsAbstractCase(binding);
        string[] commonKeys =
        {
            "all_allowed_fields_covered_in_order", "first_object_records",
            "first_objects_pairwise_distinct", "fresh_idf_object_flags", "fresh_result_list",
            "fresh_return_value", "object_count", "object_types", "result_type",
            "same_idd_definition_flags", "second_fields_equal_flags",
            "second_objects_pairwise_distinct",
        };
        AssertKeys(
            emission,
            abstractContract ? commonKeys.Concat(new[] { "first_return" }).ToArray() : commonKeys);
        int objectCount = emission.GetProperty("object_count").GetInt32();
        JsonElement[] records = emission.GetProperty("first_object_records").EnumerateArray().ToArray();
        Assert.Equal(objectCount, records.Length);
        Assert.Equal(
            records.Select(record => RequiredString(record, "object_type")),
            emission.GetProperty("object_types").EnumerateArray().Select(value => value.GetString()!));
        Assert.True(emission.GetProperty("all_allowed_fields_covered_in_order").GetBoolean());
        Assert.True(emission.GetProperty("first_objects_pairwise_distinct").GetBoolean());
        Assert.True(emission.GetProperty("second_objects_pairwise_distinct").GetBoolean());

        if (abstractContract)
        {
            Assert.Equal(0, objectCount);
            Assert.Equal("NoneType", RequiredString(emission, "result_type"));
            Assert.Equal(JsonValueKind.Null, emission.GetProperty("fresh_result_list").ValueKind);
            Assert.False(emission.GetProperty("fresh_return_value").GetBoolean());
            JsonElement firstReturn = emission.GetProperty("first_return");
            AssertKeys(firstReturn, "kind");
            Assert.Equal("none", RequiredString(firstReturn, "kind"));
        }
        else
        {
            Assert.Equal("list", RequiredString(emission, "result_type"));
            Assert.True(emission.GetProperty("fresh_result_list").GetBoolean());
            Assert.True(emission.GetProperty("fresh_return_value").GetBoolean());
            AssertBooleanArray(emission.GetProperty("fresh_idf_object_flags"), objectCount, true);
            AssertBooleanArray(emission.GetProperty("same_idd_definition_flags"), objectCount, true);
            AssertBooleanArray(emission.GetProperty("second_fields_equal_flags"), objectCount, true);
        }

        int fieldCount = 0;
        foreach (JsonElement record in records)
        {
            AssertKeys(record, "field_count", "object_type", "ordered_fields");
            string objectType = RequiredString(record, "object_type");
            OfficialIddObject official = iddOracle[objectType];
            JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
            Assert.Equal(record.GetProperty("field_count").GetInt32(), fields.Length);
            Assert.True(fields.Length >= official.Fields.Length || official.ExtensibleStartIndex is not null);
            for (int index = 0; index < fields.Length; index++)
            {
                JsonElement field = fields[index];
                AssertKeys(field, "name", "value");
                string fieldName = RequiredString(field, "name");
                if (fieldName.Length == 0)
                {
                    Assert.Equal(fields.Length - 1, index);
                    Assert.Contains(objectType, new[] { "BranchList", "Connector:Splitter", "Connector:Mixer" });
                    Assert.Equal("none", RequiredString(field.GetProperty("value"), "kind"));
                }
                else
                {
                    Assert.Equal(official.ResolveFieldName(index), fieldName);
                }
                ValidateEncodedValue(field.GetProperty("value"));
            }

            fieldCount += fields.Length;
        }

        return (objectCount, fieldCount);
    }

    private static void ValidateEncodedValue(JsonElement value)
    {
        string kind = RequiredString(value, "kind");
        switch (kind)
        {
            case "none":
                AssertKeys(value, "kind");
                break;
            case "str":
                AssertKeys(value, "kind", "value");
                _ = RequiredString(value, "value");
                break;
            case "int":
                AssertKeys(value, "kind", "value");
                Assert.True(long.TryParse(RequiredString(value, "value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
                break;
            case "bool":
                AssertKeys(value, "kind", "value");
                Assert.True(value.GetProperty("value").ValueKind is JsonValueKind.True or JsonValueKind.False);
                break;
            case "float":
                AssertKeys(value, "hex", "kind", "repr");
                double number = double.Parse(RequiredString(value, "repr"), NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.True(double.IsFinite(number));
                string hexadecimal = RequiredString(value, "hex");
                Assert.True(
                    hexadecimal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    || hexadecimal.StartsWith("-0x", StringComparison.OrdinalIgnoreCase));
                break;
            default:
                throw new Xunit.Sdk.XunitException("Unsupported encoded value kind '" + kind + "'.");
        }
    }

    private static Scenario CreateScenario(int index, IddSchema schema)
    {
        CaseBinding binding = ExpectedCases[index];
        string id = "SOURCE-ORACLE-" + index.ToString("00", CultureInfo.InvariantCulture);
        switch (index)
        {
            case 0:
            {
                var boiler = new Boiler(
                    new EntityId(id + "-BOILER"),
                    "Alternate Generator",
                    Fuel.Propane,
                    0.88,
                    91_000,
                    0.86,
                    72);
                var tower = new ClosedTwoSpeedCoolingTower(
                    new EntityId(id + "-TOWER"),
                    "Alternate Closed Tower",
                    125_000,
                    0.83);
                var source = new AbsorptionChiller(
                    new EntityId(id),
                    "Alternate Absorber",
                    0.74,
                    boiler,
                    tower,
                    110_000,
                    0.84,
                    8.5);
                return ConcreteScenario(
                    binding,
                    new object[] { source, boiler, tower },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 1:
            {
                var boiler = new Boiler(
                    new EntityId(id + "-BOILER"),
                    "Representative Generator",
                    Fuel.NaturalGas,
                    0.92,
                    null,
                    0.9,
                    60);
                var tower = new OpenSingleSpeedCoolingTower(
                    new EntityId(id + "-TOWER"),
                    "Representative Open Tower",
                    null,
                    0.9);
                var source = new AbsorptionChiller(
                    new EntityId(id),
                    "Representative Absorber",
                    0.7,
                    boiler,
                    tower,
                    150_000);
                return ConcreteScenario(
                    binding,
                    new object[] { source, boiler, tower },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 2:
            {
                var source = new Boiler(
                    new EntityId(id),
                    "Generator Boiler",
                    Fuel.NaturalGas,
                    0.91,
                    85_000,
                    0.88,
                    68);
                string target = "AbsorptionChiller_named_Generator Target";
                var connection = new PlantDemandConnection(
                    source.LoopName + " Demand MainGenerator",
                    "Chiller:Absorption",
                    target,
                    target + " Generator InletNode",
                    target + " Generator OutletNode");
                return ConcreteScenario(
                    binding,
                    new object[] { source, connection },
                    () => source.ToIdfObjects(LegacyContext(schema), new[] { connection }));
            }

            case 3:
            {
                var source = new Boiler(
                    new EntityId(id),
                    "Autosized Boiler",
                    Fuel.NaturalGas);
                return ConcreteScenario(
                    binding,
                    new object[] { source },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 4:
            {
                var source = new Boiler(
                    new EntityId(id),
                    "Propane Boiler",
                    Fuel.Propane,
                    0.86,
                    72_000,
                    0.82,
                    67.5);
                return ConcreteScenario(
                    binding,
                    new object[] { source },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 5:
            {
                var tower = new ClosedSingleSpeedCoolingTower(
                    new EntityId(id + "-TOWER"),
                    "Alternate Chiller Tower",
                    98_000,
                    0.81);
                var source = new Chiller(
                    new EntityId(id),
                    "Alternate Chiller",
                    4.75,
                    CompressorType.Reciprocating,
                    tower,
                    88_000,
                    0.83,
                    9.25);
                return ConcreteScenario(
                    binding,
                    new object[] { source, tower },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 6:
            {
                var tower = new OpenTwoSpeedCoolingTower(
                    new EntityId(id + "-TOWER"),
                    "Representative Chiller Tower");
                var source = new Chiller(
                    new EntityId(id),
                    "Representative Chiller",
                    5.5,
                    CompressorType.Turbo,
                    tower);
                return ConcreteScenario(
                    binding,
                    new object[] { source, tower },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 7:
            case 8:
            case 9:
            {
                CompressorType compressor = index switch
                {
                    7 => CompressorType.Reciprocating,
                    8 => CompressorType.Screw,
                    _ => CompressorType.Turbo,
                };
                var tower = new OpenSingleSpeedCoolingTower(
                    new EntityId(id + "-TOWER"),
                    "Unused Curve Tower");
                var source = new Chiller(
                    new EntityId(id),
                    "Curve Context",
                    1,
                    compressor,
                    tower,
                    1);
                return ConcreteScenario(
                    binding,
                    new object[] { source, tower },
                    () => InvokePerformanceCurves(source, LegacyContext(schema)));
            }

            case 10:
            {
                var tower = new ClosedTwoSpeedCoolingTower(
                    new EntityId(id + "-TOWER"),
                    "Full Closed Tower",
                    103_000,
                    0.79);
                var source = new Chiller(
                    new EntityId(id),
                    "Full Closed Context",
                    1,
                    CompressorType.Turbo,
                    tower,
                    97_000);
                return ConcreteScenario(
                    binding,
                    new object[] { source, tower },
                    () => tower.ToIdfObjects(LegacyContext(schema), source));
            }

            case 11:
            {
                var tower = new OpenSingleSpeedCoolingTower(
                    new EntityId(id + "-TOWER"),
                    "Full Open Tower",
                    null,
                    0.91);
                var source = new Chiller(
                    new EntityId(id),
                    "Full Open Context",
                    1,
                    CompressorType.Turbo,
                    tower,
                    93_000);
                return ConcreteScenario(
                    binding,
                    new object[] { source, tower },
                    () => tower.ToIdfObjects(LegacyContext(schema), source));
            }

            case 12:
                return AbstractScenario(binding, typeof(CoolingTower), "CreateMainObject");

            case 13:
                return CreateTowerMainScenario(
                    binding,
                    id,
                    schema,
                    new ClosedSingleSpeedCoolingTower(
                        new EntityId(id + "-TOWER"),
                        "Main Object Tower",
                        null,
                        0.87),
                    91_000);
            case 14:
                return CreateTowerMainScenario(
                    binding,
                    id,
                    schema,
                    new ClosedTwoSpeedCoolingTower(
                        new EntityId(id + "-TOWER"),
                        "Main Object Tower",
                        92_000,
                        0.87),
                    null);
            case 15:
                return CreateTowerMainScenario(
                    binding,
                    id,
                    schema,
                    new OpenSingleSpeedCoolingTower(
                        new EntityId(id + "-TOWER"),
                        "Main Object Tower",
                        null,
                        0.87),
                    null);
            case 16:
                return CreateTowerMainScenario(
                    binding,
                    id,
                    schema,
                    new OpenTwoSpeedCoolingTower(
                        new EntityId(id + "-TOWER"),
                        "Main Object Tower",
                        94_000,
                        0.87),
                    90_000);
            case 17:
            {
                var source = new HeatPump(
                    new EntityId(id),
                    "Explicit Heat Pump",
                    Fuel.NaturalGas,
                    4.2,
                    3.6,
                    65_000,
                    58_000);
                return ConcreteScenario(
                    binding,
                    new object[] { source },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 18:
            {
                var source = new HeatPump(
                    new EntityId(id),
                    "Representative Heat Pump",
                    Fuel.Electricity,
                    3.8,
                    3.2);
                return ConcreteScenario(
                    binding,
                    new object[] { source },
                    () => source.ToIdfObjects(LegacyContext(schema)));
            }

            case 19:
                return AbstractScenario(binding, typeof(SourceSystem), nameof(SourceSystem.ToIdfObjects));
            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private static Scenario CreateTowerMainScenario(
        CaseBinding binding,
        string id,
        IddSchema schema,
        CoolingTower tower,
        double? chillerCapacity)
    {
        var source = new Chiller(
            new EntityId(id),
            "Main Object Context",
            1,
            CompressorType.Turbo,
            tower,
            chillerCapacity);
        return ConcreteScenario(
            binding,
            new object[] { source, tower },
            () => InvokeTowerMainObject(tower, source, LegacyContext(schema)));
    }

    private static Scenario ConcreteScenario(
        CaseBinding binding,
        object[] references,
        Func<IReadOnlyList<IdfObject>> emitter) =>
        new(binding, references, emitter, null, null, StateFingerprint(references));

    private static Scenario AbstractScenario(
        CaseBinding binding,
        Type owner,
        string methodName) =>
        new(binding, Array.Empty<object>(), null, owner, methodName, StateFingerprint(Array.Empty<object>()));

    private static IdfGenerationContext LegacyContext(IddSchema? schema = null) => new(
        schema,
        options: new EnergyModelIdfOptions
        {
            UseLegacySimpleDragonHvacTopology = true,
            UseLegacySimpleDragonScheduleMetadata = true,
        });

    private static IdfObject[] InvokePerformanceCurves(
        Chiller source,
        IdfGenerationContext context)
    {
        MethodInfo method = RequiredMethod(
            typeof(Chiller),
            "CreatePerformanceCurves",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(IdfGenerationContext));
        object result = method.Invoke(source, new object[] { context })!;
        return Assert.IsAssignableFrom<IEnumerable<IdfObject>>(result).ToArray();
    }

    private static IdfObject[] InvokeTowerMainObject(
        CoolingTower tower,
        SourceSystem source,
        IdfGenerationContext context)
    {
        MethodInfo method = RequiredMethod(
            tower.GetType(),
            "CreateMainObject",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(IdfGenerationContext),
            typeof(SourceSystem));
        var result = Assert.IsType<IdfObject>(method.Invoke(tower, new object[] { context, source }));
        return new[] { result };
    }

    private static string StateFingerprint(IReadOnlyList<object> references)
    {
        var values = new List<string>();
        for (int referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
        {
            object instance = references[referenceIndex];
            values.Add(referenceIndex.ToString(CultureInfo.InvariantCulture) + ":" + instance.GetType().FullName);
            foreach (PropertyInfo property in instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(item => item.GetIndexParameters().Length == 0)
                .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                object? value = property.GetValue(instance);
                values.Add(property.Name + "=" + StableValue(value, references));
            }
        }

        return Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values)));
    }

    private static string StableValue(object? value, IReadOnlyList<object> references)
    {
        if (value is null)
        {
            return "null";
        }

        for (int index = 0; index < references.Count; index++)
        {
            if (ReferenceEquals(value, references[index]))
            {
                return "ref:" + index.ToString(CultureInfo.InvariantCulture);
            }
        }

        return value switch
        {
            double number => "double:" + BitConverter.DoubleToInt64Bits(number).ToString("x16", CultureInfo.InvariantCulture),
            float number => "float:" + BitConverter.SingleToInt32Bits(number).ToString("x8", CultureInfo.InvariantCulture),
            string text => "string:" + text,
            Enum enumeration => "enum:" + enumeration.GetType().FullName + ":" + enumeration,
            IEnumerable sequence when value is not string =>
                "sequence:[" + string.Join(",", sequence.Cast<object?>().Select(item => StableValue(item, references))) + "]",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static NativeObservation ExecuteNativeCase(
        CaseBinding binding,
        JsonElement facts,
        Scenario scenario,
        OfficialIddOracle iddOracle)
    {
        if (IsAbstractCase(binding))
        {
            return ExecuteAbstractCase(binding, facts, scenario);
        }

        Assert.NotNull(scenario.Emitter);
        JsonElement recordsElement = facts.GetProperty("emission").GetProperty("first_object_records");
        JsonElement[] records = recordsElement.EnumerateArray().ToArray();
        Assert.Equal(facts.GetProperty("emission").GetProperty("object_count").GetInt32(), records.Length);

        AssertScenarioUnchanged(scenario);
        IReadOnlyList<IdfObject> first = scenario.Emitter!();
        AssertScenarioUnchanged(scenario);
        IReadOnlyList<IdfObject> second = scenario.Emitter!();
        AssertScenarioUnchanged(scenario);
        Assert.NotSame(first, second);
        Assert.Equal(records.Length, first.Count);
        Assert.Equal(first.Count, second.Count);
        AssertPairwiseDistinct(first);
        AssertPairwiseDistinct(second);
        for (int index = 0; index < first.Count; index++)
        {
            Assert.NotSame(first[index], second[index]);
        }

        ParityAnalysis firstAnalysis = AnalyzeParity(first, records, iddOracle);
        ParityAnalysis secondAnalysis = AnalyzeParity(second, records, iddOracle);
        AssertParityEquivalent(firstAnalysis, secondAnalysis);
        string nativeOutputSha256 = OutputSha256(first);
        Assert.Equal(nativeOutputSha256, OutputSha256(second));
        Assert.Empty(firstAnalysis.ValueDifferences);
        Assert.Equal(
            firstAnalysis.PythonFieldCount,
            firstAnalysis.ComparedPresentFieldCount
                + firstAnalysis.BlankOrNoneOmissionCount
                + firstAnalysis.DefaultOmissions.Length);
        NativeCaseExpectation expectation = ExpectedNativeCases.Single(item => item.CaseId == binding.CaseId);
        if (!DiscoverPins)
        {
            Assert.Equal(expectation.NativeCompactFieldCount, first.Sum(item => item.Count));
            Assert.Equal(expectation.BlankOrNoneOmissionCount, firstAnalysis.BlankOrNoneOmissionCount);
            Assert.Equal(expectation.DefaultOmissionCount, firstAnalysis.DefaultOmissions.Length);
            Assert.Equal(expectation.TokenCaseNormalizationCount, firstAnalysis.TokenCaseNormalizations.Length);
            Assert.Equal(expectation.NumericLexemeNormalizationCount, firstAnalysis.NumericLexemeNormalizations.Length);
            Assert.Equal(expectation.ObjectRelocationCount, firstAnalysis.ObjectRelocations.Length);
            Assert.Equal(expectation.NativeOutputSha256, nativeOutputSha256);
        }
        Assert.Empty(firstAnalysis.ContextEnrichments);
        AssertHonestNormalizations(firstAnalysis);

        string[] topologyFacts = AssertCaseTopology(binding, first, records);
        Assert.Equal(topologyFacts, AssertCaseTopology(binding, second, records));
        string[] nativeFacts = new[]
        {
            "native-context=UseLegacySimpleDragonHvacTopology:true;UseLegacySimpleDragonScheduleMetadata:true",
            "python-complete-objects=" + records.Length.ToString(CultureInfo.InvariantCulture),
            "python-complete-fields=" + firstAnalysis.PythonFieldCount.ToString(CultureInfo.InvariantCulture),
            "native-compact-fields=" + first.Sum(item => item.Count).ToString(CultureInfo.InvariantCulture),
            "present-fields-compared=" + firstAnalysis.ComparedPresentFieldCount.ToString(CultureInfo.InvariantCulture),
            "omitted-blank-or-none=" + firstAnalysis.BlankOrNoneOmissionCount.ToString(CultureInfo.InvariantCulture),
            "omitted-official-idd-defaults=" + firstAnalysis.DefaultOmissions.Length.ToString(CultureInfo.InvariantCulture),
            "context-enrichments=" + firstAnalysis.ContextEnrichments.Length.ToString(CultureInfo.InvariantCulture),
            "idf-token-case-normalizations=" + firstAnalysis.TokenCaseNormalizations.Length.ToString(CultureInfo.InvariantCulture),
            "numeric-lexeme-normalizations=" + firstAnalysis.NumericLexemeNormalizations.Length.ToString(CultureInfo.InvariantCulture),
            "object-order-relocations=" + firstAnalysis.ObjectRelocations.Length.ToString(CultureInfo.InvariantCulture),
            "two-call-freshness=distinct-lists-and-pairwise-distinct-fresh-idf-objects",
            "two-call-determinism=complete-native-object-order-and-fields-identical",
            "source-state-mutation=none-across-two-emissions",
            "comparison-oracle=official-EnergyPlus-24.2.0-build-94a887817b-IDD",
        }.Concat(topologyFacts).ToArray();
        Assert.Equal(nativeFacts.Length, nativeFacts.Distinct(StringComparer.Ordinal).Count());

        return new NativeObservation(
            binding.CaseId,
            binding.Symbol,
            records.Length,
            firstAnalysis.PythonFieldCount,
            first.Sum(item => item.Count),
            nativeOutputSha256,
            first.Select(ObjectSnapshot.Create).ToArray(),
            firstAnalysis.BlankOrNoneOmissionCount,
            firstAnalysis.DefaultOmissions,
            firstAnalysis.ContextEnrichments,
            firstAnalysis.TokenCaseNormalizations,
            firstAnalysis.NumericLexemeNormalizations,
            firstAnalysis.ObjectRelocations,
            nativeFacts);
    }

    private static NativeObservation ExecuteAbstractCase(
        CaseBinding binding,
        JsonElement facts,
        Scenario scenario)
    {
        Assert.Null(scenario.Emitter);
        Assert.NotNull(scenario.AbstractOwner);
        Assert.NotNull(scenario.AbstractMethodName);
        MethodInfo method = scenario.AbstractOwner!.GetMethod(
            scenario.AbstractMethodName!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            ?? throw new Xunit.Sdk.XunitException(
                "Missing abstract native contract " + scenario.AbstractOwner.FullName + "." + scenario.AbstractMethodName + ".");
        Assert.True(method.IsAbstract);
        Assert.True(method.IsVirtual);
        Assert.Equal(scenario.AbstractOwner, method.DeclaringType);
        AssertScenarioUnchanged(scenario);
        NativeCaseExpectation expectation = ExpectedNativeCases.Single(item => item.CaseId == binding.CaseId);
        Assert.Equal(0, expectation.NativeCompactFieldCount);
        Assert.Equal(0, expectation.BlankOrNoneOmissionCount);
        Assert.Equal(0, expectation.DefaultOmissionCount);
        Assert.Equal(0, expectation.TokenCaseNormalizationCount);
        Assert.Equal(0, expectation.NumericLexemeNormalizationCount);
        Assert.Equal(0, expectation.ObjectRelocationCount);
        Assert.Equal(
            expectation.NativeOutputSha256,
            Sha256(Encoding.UTF8.GetBytes("[]")));

        JsonElement emission = facts.GetProperty("emission");
        Assert.Equal(0, emission.GetProperty("object_count").GetInt32());
        Assert.Empty(emission.GetProperty("first_object_records").EnumerateArray());
        string[] factsOut;
        if (scenario.AbstractOwner == typeof(SourceSystem))
        {
            Assert.True(method.IsPublic);
            Assert.Equal(typeof(IReadOnlyList<IdfObject>), method.ReturnType);
            Assert.Equal(
                new[]
                {
                    typeof(IdfGenerationContext),
                    typeof(IReadOnlyList<PlantDemandConnection>),
                    typeof(IReadOnlyList<string>),
                },
                method.GetParameters().Select(item => item.ParameterType));
            factsOut = new[]
            {
                "native-abstract-contract=SourceSystem.ToIdfObjects",
                "native-contract-shape=public-abstract-IReadOnlyList<IdfObject>-with-explicit-context-and-optional-linkage",
                "python-direct-abstract-body-return=None",
                "fake-native-emission=not-used",
            };
        }
        else
        {
            Assert.Equal(typeof(CoolingTower), scenario.AbstractOwner);
            Assert.True(method.IsFamily);
            Assert.Equal(typeof(IdfObject), method.ReturnType);
            Assert.Equal(
                new[] { typeof(IdfGenerationContext), typeof(SourceSystem) },
                method.GetParameters().Select(item => item.ParameterType));
            factsOut = new[]
            {
                "native-abstract-contract=CoolingTower.CreateMainObject",
                "native-contract-shape=protected-abstract-IdfObject-with-explicit-context-and-source",
                "python-direct-abstract-body-return=None",
                "fake-native-emission=not-used",
            };
        }

        return new NativeObservation(
            binding.CaseId,
            binding.Symbol,
            0,
            0,
            0,
            Sha256(Encoding.UTF8.GetBytes("[]")),
            Array.Empty<ObjectSnapshot>(),
            0,
            Array.Empty<DefaultOmissionFact>(),
            Array.Empty<ContextEnrichmentFact>(),
            Array.Empty<TokenCaseNormalizationFact>(),
            Array.Empty<NumericLexemeNormalizationFact>(),
            Array.Empty<ObjectRelocationFact>(),
            factsOut);
    }

    private static ParityAnalysis AnalyzeParity(
        IReadOnlyList<IdfObject> nativeObjects,
        IReadOnlyList<JsonElement> records,
        OfficialIddOracle iddOracle)
    {
        Assert.Equal(records.Count, nativeObjects.Count);
        var nativeByKey = nativeObjects
            .Select((item, index) => new { Item = item, Index = index, Key = ObjectKey(item) })
            .ToDictionary(item => item.Key, StringComparer.Ordinal);
        Assert.Equal(nativeObjects.Count, nativeByKey.Count);

        var defaults = new List<DefaultOmissionFact>();
        var enrichments = new List<ContextEnrichmentFact>();
        var caseNormalizations = new List<TokenCaseNormalizationFact>();
        var numericNormalizations = new List<NumericLexemeNormalizationFact>();
        var relocations = new List<ObjectRelocationFact>();
        var differences = new List<ValueDifferenceFact>();
        int blankOrNone = 0;
        int comparedPresent = 0;
        int pythonFields = 0;
        for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
        {
            JsonElement record = records[recordIndex];
            string objectType = RequiredString(record, "object_type");
            string objectName = PythonObjectName(record);
            string key = ObjectKey(objectType, objectName);
            Assert.True(nativeByKey.TryGetValue(key, out var selected), "Missing native object " + key + ".");
            IdfObject native = selected!.Item;
            if (selected.Index != recordIndex)
            {
                relocations.Add(new ObjectRelocationFact(
                    objectType,
                    objectName,
                    recordIndex,
                    selected.Index));
            }

            OfficialIddObject official = iddOracle[objectType];
            JsonElement[] fields = record.GetProperty("ordered_fields").EnumerateArray().ToArray();
            Assert.True(native.Count <= fields.Length);
            pythonFields += fields.Length;
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                JsonElement encoded = fields[fieldIndex].GetProperty("value");
                string fieldName = RequiredString(fields[fieldIndex], "name");
                OfficialIddField officialField = official.ResolveField(fieldIndex);
                if (fieldIndex < native.Count)
                {
                    string nativeValue = native[fieldIndex];
                    if (RequiredString(encoded, "kind") != "none"
                        && nativeValue.Length == 0
                        && officialField.DefaultValue is not null
                        && EncodedMatchesDefault(encoded, officialField.DefaultValue))
                    {
                        defaults.Add(new DefaultOmissionFact(
                            objectType,
                            objectName,
                            fieldIndex,
                            fieldName,
                            EncodedDisplay(encoded),
                            officialField.DefaultValue));
                        continue;
                    }

                    comparedPresent++;
                    if (RequiredString(encoded, "kind") == "none" && nativeValue.Length > 0)
                    {
                        enrichments.Add(new ContextEnrichmentFact(
                            objectType,
                            objectName,
                            fieldIndex,
                            fieldName,
                            nativeValue));
                    }
                    else if (RequiredString(encoded, "kind") == "str"
                        && officialField.Kind == "numeric"
                        && !string.Equals(
                            RequiredString(encoded, "value"),
                            nativeValue,
                            StringComparison.Ordinal)
                        && NumericEqual(RequiredString(encoded, "value"), nativeValue))
                    {
                        numericNormalizations.Add(new NumericLexemeNormalizationFact(
                            objectType,
                            objectName,
                            fieldIndex,
                            fieldName,
                            RequiredString(encoded, "value"),
                            nativeValue));
                    }
                    else if (RequiredString(encoded, "kind") == "str"
                        && !string.Equals(
                            RequiredString(encoded, "value"),
                            nativeValue,
                            StringComparison.Ordinal)
                        && string.Equals(
                            RequiredString(encoded, "value"),
                            nativeValue,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        caseNormalizations.Add(new TokenCaseNormalizationFact(
                            objectType,
                            objectName,
                            fieldIndex,
                            fieldName,
                            RequiredString(encoded, "value"),
                            nativeValue));
                    }
                    else if (!EncodedMatchesNative(encoded, nativeValue))
                    {
                        differences.Add(new ValueDifferenceFact(
                            objectType,
                            objectName,
                            fieldIndex,
                            fieldName,
                            EncodedDisplay(encoded),
                            nativeValue));
                    }

                    continue;
                }

                if (RequiredString(encoded, "kind") == "none")
                {
                    blankOrNone++;
                    continue;
                }

                if (officialField.DefaultValue is not null
                    && EncodedMatchesDefault(encoded, officialField.DefaultValue))
                {
                    defaults.Add(new DefaultOmissionFact(
                        objectType,
                        objectName,
                        fieldIndex,
                        fieldName,
                        EncodedDisplay(encoded),
                        officialField.DefaultValue));
                    continue;
                }

                differences.Add(new ValueDifferenceFact(
                    objectType,
                    objectName,
                    fieldIndex,
                    fieldName,
                    EncodedDisplay(encoded),
                    "<omitted-without-matching-IDD-default>"));
            }
        }

        return new ParityAnalysis(
            pythonFields,
            comparedPresent,
            blankOrNone,
            defaults.ToArray(),
            enrichments.ToArray(),
            caseNormalizations.ToArray(),
            numericNormalizations.ToArray(),
            relocations.ToArray(),
            differences.ToArray());
    }

    private static bool EncodedMatchesNative(JsonElement encoded, string nativeValue)
    {
        string kind = RequiredString(encoded, "kind");
        return kind switch
        {
            "none" => nativeValue.Length == 0,
            "str" => string.Equals(RequiredString(encoded, "value"), nativeValue, StringComparison.Ordinal),
            "bool" => string.Equals(
                encoded.GetProperty("value").GetBoolean() ? "Yes" : "No",
                nativeValue,
                StringComparison.Ordinal),
            "int" => NumericBitsEqual(RequiredString(encoded, "value"), nativeValue),
            "float" => NumericBitsEqual(RequiredString(encoded, "repr"), nativeValue),
            _ => false,
        };
    }

    private static bool EncodedMatchesDefault(JsonElement encoded, string officialDefault)
    {
        string kind = RequiredString(encoded, "kind");
        return kind switch
        {
            "str" => string.Equals(RequiredString(encoded, "value"), officialDefault, StringComparison.Ordinal),
            "bool" => string.Equals(
                encoded.GetProperty("value").GetBoolean() ? "Yes" : "No",
                officialDefault,
                StringComparison.OrdinalIgnoreCase),
            "int" => NumericEqual(RequiredString(encoded, "value"), officialDefault),
            "float" => NumericEqual(RequiredString(encoded, "repr"), officialDefault),
            _ => false,
        };
    }

    private static bool NumericBitsEqual(string expected, string actual)
    {
        return double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out double left)
            && double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double right)
            && BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right);
    }

    private static bool NumericEqual(string expected, string actual)
    {
        return double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out double left)
            && double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double right)
            && left.Equals(right);
    }

    private static string EncodedDisplay(JsonElement encoded)
    {
        return RequiredString(encoded, "kind") switch
        {
            "none" => "None",
            "str" or "int" => RequiredString(encoded, "value"),
            "bool" => encoded.GetProperty("value").GetBoolean() ? "true" : "false",
            "float" => RequiredString(encoded, "repr"),
            _ => throw new Xunit.Sdk.XunitException("Unsupported encoded display."),
        };
    }

    private static string PythonObjectName(JsonElement record)
    {
        JsonElement first = record.GetProperty("ordered_fields")[0];
        JsonElement encoded = first.GetProperty("value");
        Assert.Equal("str", RequiredString(encoded, "kind"));
        return RequiredString(encoded, "value");
    }

    private static string ObjectKey(IdfObject value) =>
        ObjectKey(value.ObjectType, value.Name ?? string.Empty);

    private static string ObjectKey(string objectType, string objectName) =>
        objectType + "\u001f" + objectName;

    private static string OutputSha256(IEnumerable<IdfObject> values)
    {
        JsonElement output = JsonSerializer.SerializeToElement(values.Select(item => new
        {
            object_type = item.ObjectType,
            fields = item.Fields.Select(field => field.Value).ToArray(),
        }).ToArray());
        return CanonicalSha256(output);
    }

    private static void AssertScenarioUnchanged(Scenario scenario)
    {
        Assert.Equal(scenario.InitialReferences.Length, scenario.References.Length);
        for (int index = 0; index < scenario.InitialReferences.Length; index++)
        {
            Assert.Same(scenario.InitialReferences[index], scenario.References[index]);
        }

        Assert.Equal(scenario.InitialStateFingerprint, StateFingerprint(scenario.References));
    }

    private static void AssertPairwiseDistinct(IReadOnlyList<IdfObject> values)
    {
        for (int first = 0; first < values.Count; first++)
        {
            for (int second = first + 1; second < values.Count; second++)
            {
                Assert.NotSame(values[first], values[second]);
            }
        }
    }

    private static void AssertParityEquivalent(ParityAnalysis expected, ParityAnalysis actual)
    {
        Assert.Equal(expected.PythonFieldCount, actual.PythonFieldCount);
        Assert.Equal(expected.ComparedPresentFieldCount, actual.ComparedPresentFieldCount);
        Assert.Equal(expected.BlankOrNoneOmissionCount, actual.BlankOrNoneOmissionCount);
        Assert.Equal(expected.DefaultOmissions, actual.DefaultOmissions);
        Assert.Equal(expected.ContextEnrichments, actual.ContextEnrichments);
        Assert.Equal(expected.TokenCaseNormalizations, actual.TokenCaseNormalizations);
        Assert.Equal(expected.NumericLexemeNormalizations, actual.NumericLexemeNormalizations);
        Assert.Equal(expected.ObjectRelocations, actual.ObjectRelocations);
        Assert.Equal(expected.ValueDifferences, actual.ValueDifferences);
    }

    private static void AssertHonestNormalizations(ParityAnalysis analysis)
    {
        Assert.All(analysis.TokenCaseNormalizations, item =>
        {
            Assert.Equal(9, item.ZeroBasedPosition);
            Assert.Contains(item.ObjectType, new[] { "PlantLoop", "CondenserLoop" });
            Assert.EndsWith("Loop Volume", item.FieldName, StringComparison.Ordinal);
            Assert.Equal("Autocalculate", item.PythonValue);
            Assert.Equal("autocalculate", item.NativeValue);
        });
        Assert.All(analysis.NumericLexemeNormalizations, item =>
        {
            Assert.Equal("AirConditioner:VariableRefrigerantFlow", item.ObjectType);
            Assert.Equal(4, item.ZeroBasedPosition);
            Assert.Equal("Minimum Condenser Inlet Node Temperature in Cooling Mode", item.FieldName);
            Assert.Equal("-6.0", item.PythonValue);
            Assert.Equal("-6", item.NativeValue);
        });
        Assert.All(analysis.ObjectRelocations, item =>
        {
            Assert.NotEqual(item.PythonIndex, item.NativeIndex);
            Assert.False(string.IsNullOrWhiteSpace(item.ObjectType));
            Assert.False(string.IsNullOrWhiteSpace(item.ObjectName));
        });
    }

    private static string[] AssertCaseTopology(
        CaseBinding binding,
        IReadOnlyList<IdfObject> native,
        IReadOnlyList<JsonElement> pythonRecords)
    {
        string[] pythonTypes = pythonRecords.Select(item => RequiredString(item, "object_type")).ToArray();
        string[] nativeTypes = native.Select(item => item.ObjectType).ToArray();
        Assert.Equal(
            pythonTypes.OrderBy(item => item, StringComparer.Ordinal),
            nativeTypes.OrderBy(item => item, StringComparer.Ordinal));

        if (binding.CaseId.Contains("absorption-chiller", StringComparison.Ordinal))
        {
            Assert.Equal("Chiller:Absorption", native[0].ObjectType);
            Assert.Equal("Pump:VariableSpeed", native[1].ObjectType);
            int boiler = Array.IndexOf(nativeTypes, "Boiler:HotWater");
            int tower = Array.FindIndex(nativeTypes, item => item is
                "CoolingTower:SingleSpeed" or
                "CoolingTower:TwoSpeed" or
                "FluidCooler:SingleSpeed" or
                "FluidCooler:TwoSpeed");
            Assert.True(boiler > 0 && tower > boiler);
            Assert.Equal("PlantLoop", native[^2].ObjectType);
            Assert.Equal("Sizing:Plant", native[^1].ObjectType);
            IdfObject sizing = Assert.Single(
                native,
                item => item.ObjectType == "Sizing:Plant"
                    && item.Name == (binding.CaseId.Contains("alternate", StringComparison.Ordinal)
                        ? "Loop_for_Alternate Absorber"
                        : "Loop_for_Representative Absorber"));
            Assert.Equal("6.0", sizing[2]);
            IdfObject generator = Assert.Single(
                native,
                item => item.ObjectType == "Branch"
                    && item.Name!.EndsWith(" Demand MainGenerator", StringComparison.Ordinal));
            Assert.Equal("Chiller:Absorption", generator[2]);
            int generatorIndex = Array.IndexOf(native.ToArray(), generator);
            Assert.Equal(tower - 1, generatorIndex);
            return new[]
            {
                "legacy-absorption-order=chiller-prefix;generator-boiler-loop;generator-branch;condenser-loop;main-loop-closure",
                "legacy-main-sizing-design-exit-temperature=6.0",
                "generator-branch-link=Chiller:Absorption;position=immediately-before-condenser-tower",
            };
        }

        if (binding.CaseId.Contains("boiler-generator", StringComparison.Ordinal))
        {
            const string loop = "Loop_for_Generator Boiler";
            const string branchName = loop + " Demand MainGenerator";
            IdfObject branch = ObjectNamed(native, "Branch", branchName);
            Assert.Equal("Chiller:Absorption", branch[2]);
            Assert.Equal("AbsorptionChiller_named_Generator Target", branch[3]);
            Assert.Equal("AbsorptionChiller_named_Generator Target Generator InletNode", branch[4]);
            Assert.Equal("AbsorptionChiller_named_Generator Target Generator OutletNode", branch[5]);
            IdfObject branchList = ObjectNamed(native, "BranchList", loop + " Demand BranchList");
            Assert.Equal(
                new[]
                {
                    loop + " Demand Inlet",
                    loop + " Demand Bypass",
                    branchName,
                    loop + " Demand Outlet",
                },
                branchList.Fields.Skip(1).Select(item => item.Value));
            IdfObject splitter = ObjectNamed(native, "Connector:Splitter", loop + " Demand Splitter");
            IdfObject mixer = ObjectNamed(native, "Connector:Mixer", loop + " Demand Mixer");
            Assert.Contains(branchName, splitter.Fields.Select(item => item.Value));
            Assert.Contains(branchName, mixer.Fields.Select(item => item.Value));
            int branchIndex = Array.IndexOf(native.ToArray(), branch);
            Assert.Equal(13, branchIndex);
            Assert.Equal(loop + " Demand Outlet", native[branchIndex + 1].Name);
            return new[]
            {
                "generator-demand-branch=exact-chiller-absorption-name-and-nodes",
                "generator-demand-topology=branch-list-splitter-mixer-linked",
                "native-generator-branch-order=integrated-before-demand-outlet;python-appended-order-reported-as-relocation",
            };
        }

        if (binding.CaseId.Contains("boiler.", StringComparison.Ordinal))
        {
            Assert.Equal("Boiler:HotWater", native[0].ObjectType);
            Assert.Equal("PlantLoop", native[^2].ObjectType);
            Assert.Equal("Sizing:Plant", native[^1].ObjectType);
            Assert.Equal("80", native[^1][2]);
            return new[] { "heating-loop-tail=PlantLoop;Sizing:Plant:80" };
        }

        if (binding.CaseId.Contains("chiller.", StringComparison.Ordinal))
        {
            Assert.True(nativeTypes.Take(3).All(item => item.StartsWith("Curve:", StringComparison.Ordinal)));
            Assert.Equal("PlantLoop", native[^2].ObjectType);
            Assert.Equal("Sizing:Plant", native[^1].ObjectType);
            Assert.Equal("6.0", native[^1][2]);
            return new[]
            {
                "chiller-order=three-curves;condenser-loop;chilled-water-loop",
                "legacy-main-sizing-design-exit-temperature=6.0",
            };
        }

        if (binding.CaseId.Contains("compressor.", StringComparison.Ordinal))
        {
            Assert.Equal(3, native.Count);
            Assert.All(native, item => Assert.StartsWith("Curve:", item.ObjectType, StringComparison.Ordinal));
            Assert.Equal(
                new[] { "CoolingCapaTemp", "CoolingCOPTemp", "CoolingCOPPLR" },
                native.Select(item => item.Name![(item.Name!.LastIndexOf(':') + 1)..]));
            return new[] { "compressor-curve-slice=capacity-temperature;cop-temperature;cop-part-load" };
        }

        if (binding.CaseId.Contains("cooling-tower-full", StringComparison.Ordinal))
        {
            Assert.Equal("Pump:VariableSpeed", native[1].ObjectType);
            Assert.Equal("CondenserLoop", native[^2].ObjectType);
            Assert.Equal("Sizing:Plant", native[^1].ObjectType);
            return new[] { "condenser-loop-order=main-tower;pump;topology;CondenserLoop;Sizing:Plant" };
        }

        if (binding.CaseId.Contains("cooling-tower-main", StringComparison.Ordinal))
        {
            Assert.Single(native);
            Assert.Contains(native[0].ObjectType, new[]
            {
                "FluidCooler:SingleSpeed", "FluidCooler:TwoSpeed",
                "CoolingTower:SingleSpeed", "CoolingTower:TwoSpeed",
            });
            return new[] { "protected-main-object-route=exactly-one-concrete-tower-object" };
        }

        Assert.Contains("heat-pump", binding.CaseId, StringComparison.Ordinal);
        Assert.Equal(22, native.Count);
        Assert.Equal("ZoneTerminalUnitList", native[^2].ObjectType);
        Assert.Equal("AirConditioner:VariableRefrigerantFlow", native[^1].ObjectType);
        return new[] { "heat-pump-family=twenty-performance-curves;terminal-list;vrf-outdoor-unit" };
    }

    private static IdfObject ObjectNamed(
        IEnumerable<IdfObject> objects,
        string objectType,
        string name) => Assert.Single(
        objects,
        item => item.ObjectType == objectType
            && string.Equals(item.Name, name, StringComparison.Ordinal));

    private static void AssertLegacyTopology(IReadOnlyList<NativeObservation> observations)
    {
        Assert.Equal(2, observations.Count(item => item.Symbol == "AbsorptionChiller.to_idf_object"));
        Assert.Equal(2, observations.Count(item => item.Symbol == "Chiller.to_idf_object"));
        Assert.All(
            observations.Where(item => item.Symbol is "AbsorptionChiller.to_idf_object" or "Chiller.to_idf_object"),
            item => Assert.Contains("legacy-main-sizing-design-exit-temperature=6.0", item.NativeFacts));
        Assert.All(
            observations.Where(item => !IsAbstractSymbol(item.Symbol)),
            item =>
            {
                Assert.True(item.PythonObjectCount > 0);
                Assert.True(item.PythonFieldCount >= item.NativeCompactFieldCount);
                Assert.StartsWith("sha256:", item.NativeOutputSha256, StringComparison.Ordinal);
            });
        Assert.All(
            observations.Where(item => IsAbstractSymbol(item.Symbol)),
            item =>
            {
                Assert.Equal(0, item.PythonObjectCount);
                Assert.Contains("fake-native-emission=not-used", item.NativeFacts);
            });
    }

    private static void ValidateArtifactsAndNativeBindings()
    {
        AssertPinnedArtifact(GeneratorRepositoryPath, GeneratorByteLength, GeneratorSha256);
        AssertPinnedArtifact(PythonValidatorRepositoryPath, PythonValidatorByteLength, PythonValidatorSha256);
        AssertPinnedArtifact(OracleRepositoryPath, OracleByteLength, OracleSha256);
        foreach (NativeArtifact artifact in NativeArtifacts)
        {
            AssertPinnedArtifact(artifact.Path, artifact.ByteLength, artifact.Sha256);
        }

        byte[] inventoryBytes = File.ReadAllBytes(FindRepositoryFile("upstream/public-symbol-inventory.json"));
        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        Assert.Equal(InventorySha256, RequiredString(inventory.RootElement, "content_sha256"));
        Assert.Equal(UpstreamCommit, RequiredString(inventory.RootElement, "upstream_commit"));
        JsonElement[] inventorySymbols = inventory.RootElement.GetProperty("symbols").EnumerateArray().ToArray();
        foreach (SymbolBinding expected in ExpectedSymbols)
        {
            JsonElement symbol = inventorySymbols[expected.InventoryIndex];
            Assert.Equal(expected.Symbol, RequiredString(symbol, "symbol"));
            Assert.Equal(UpstreamPath, RequiredString(symbol, "path"));
            Assert.Equal(expected.SymbolHash, RequiredString(symbol, "symbol_hash"));
            Assert.Equal(expected.SignatureHash, RequiredString(symbol, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(symbol, "body_hash"));
        }

        Type connections = typeof(IReadOnlyList<PlantDemandConnection>);
        Type terminals = typeof(IReadOnlyList<string>);
        foreach (Type concrete in new[] { typeof(AbsorptionChiller), typeof(Boiler), typeof(Chiller), typeof(HeatPump) })
        {
            MethodInfo method = RequiredMethod(
                concrete,
                nameof(SourceSystem.ToIdfObjects),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(IdfGenerationContext),
                connections,
                terminals);
            Assert.Equal(typeof(IReadOnlyList<IdfObject>), method.ReturnType);
            Assert.True(method.IsVirtual);
            Assert.False(method.IsAbstract);
        }

        MethodInfo sourceAbstract = RequiredMethod(
            typeof(SourceSystem),
            nameof(SourceSystem.ToIdfObjects),
            BindingFlags.Instance | BindingFlags.Public,
            typeof(IdfGenerationContext),
            connections,
            terminals);
        Assert.True(sourceAbstract.IsAbstract);
        Assert.Equal(typeof(IReadOnlyList<IdfObject>), sourceAbstract.ReturnType);

        MethodInfo towerFull = RequiredMethod(
            typeof(CoolingTower),
            nameof(CoolingTower.ToIdfObjects),
            BindingFlags.Instance | BindingFlags.Public,
            typeof(IdfGenerationContext),
            typeof(SourceSystem));
        Assert.False(towerFull.IsVirtual);
        Assert.Equal(typeof(IReadOnlyList<IdfObject>), towerFull.ReturnType);

        MethodInfo towerAbstract = RequiredMethod(
            typeof(CoolingTower),
            "CreateMainObject",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(IdfGenerationContext),
            typeof(SourceSystem));
        Assert.True(towerAbstract.IsAbstract);
        Assert.True(towerAbstract.IsFamily);
        Assert.Equal(typeof(IdfObject), towerAbstract.ReturnType);
        foreach (Type concrete in new[]
        {
            typeof(ClosedSingleSpeedCoolingTower), typeof(ClosedTwoSpeedCoolingTower),
            typeof(OpenSingleSpeedCoolingTower), typeof(OpenTwoSpeedCoolingTower),
        })
        {
            MethodInfo method = RequiredMethod(
                concrete,
                "CreateMainObject",
                BindingFlags.Instance | BindingFlags.NonPublic,
                typeof(IdfGenerationContext),
                typeof(SourceSystem));
            Assert.Equal(concrete, method.DeclaringType);
            Assert.True(method.IsFamily);
            Assert.False(method.IsAbstract);
            Assert.Equal(typeof(IdfObject), method.ReturnType);
        }

        MethodInfo curves = RequiredMethod(
            typeof(Chiller),
            "CreatePerformanceCurves",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(IdfGenerationContext));
        Assert.True(curves.IsPrivate);
        Assert.Equal(typeof(IEnumerable<IdfObject>), curves.ReturnType);

        var defaults = new EnergyModelIdfOptions();
        Assert.False(defaults.UseLegacySimpleDragonHvacTopology);
        Assert.False(defaults.UseLegacySimpleDragonScheduleMetadata);
        IdfGenerationContext legacy = LegacyContext();
        Assert.True(legacy.Options.UseLegacySimpleDragonHvacTopology);
        Assert.True(legacy.Options.UseLegacySimpleDragonScheduleMetadata);
    }

    private static MethodInfo RequiredMethod(
        Type owner,
        string name,
        BindingFlags flags,
        params Type[] parameterTypes) =>
        owner.GetMethod(name, flags, binder: null, parameterTypes, modifiers: null)
        ?? throw new Xunit.Sdk.XunitException(
            "Missing native method '" + owner.FullName + "." + name + "'.");

    private static OfficialIddOracle LoadOfficialIddOracle()
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(IddOracleRepositoryPath));
        Assert.Equal(IddOracleByteLength, bytes.Length);
        Assert.Equal(IddOracleSha256, Sha256(bytes));
        using var input = new MemoryStream(bytes, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using JsonDocument document = JsonDocument.Parse(
            gzip,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        JsonElement root = document.RootElement;
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(
            root,
            "energyplus_build",
            "energyplus_version",
            "field_count",
            "groups",
            "object_count",
            "objects",
            "official_epjson_schema",
            "oracle_schema",
            "source_bytes",
            "source_sha256",
            "upstream_commit");
        Assert.Equal(IddOracleSchema, RequiredString(root, "oracle_schema"));
        Assert.Equal(UpstreamCommit, RequiredString(root, "upstream_commit"));
        Assert.Equal(EnergyPlusVersion, RequiredString(root, "energyplus_version"));
        Assert.Equal(EnergyPlusBuild, RequiredString(root, "energyplus_build"));
        Assert.Equal(EnergyPlusIddSourceSha256, RequiredString(root, "source_sha256"));
        Assert.Equal(EnergyPlusIddSourceByteLength, root.GetProperty("source_bytes").GetInt32());
        Assert.Equal(848, root.GetProperty("object_count").GetInt32());
        Assert.Equal(13_702, root.GetProperty("field_count").GetInt32());
        JsonElement[] objects = root.GetProperty("objects").EnumerateArray().ToArray();
        Assert.Equal(848, objects.Length);
        Assert.Equal(
            Enumerable.Range(0, objects.Length),
            objects.Select(item => item.GetProperty("position").GetInt32()));
        Assert.Equal(13_702, objects.Sum(item => item.GetProperty("fields").GetArrayLength()));
        OfficialIddObject[] parsed = objects.Select(ParseOfficialIddObject).ToArray();
        IddObjectDefinition[] nativeDefinitions = objects.Select(ParseNativeIddObject).ToArray();
        Assert.Equal(
            parsed.Length,
            parsed.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var nativeSchema = new IddSchema(
            EnergyPlusVersion,
            EnergyPlusBuild,
            EnergyPlusIddSourceSha256,
            nativeDefinitions);
        Assert.Equal(parsed.Length, nativeSchema.Objects.Count);
        return new OfficialIddOracle(parsed, nativeSchema);
    }

    private static OfficialIddObject ParseOfficialIddObject(JsonElement item)
    {
        AssertKeys(
            item,
            "additional_directives",
            "extensible_group_size",
            "extensible_start_index",
            "fields",
            "format",
            "group",
            "is_required",
            "is_unique",
            "memo",
            "minimum_fields",
            "name",
            "obsolete_message",
            "position");
        JsonElement[] fields = item.GetProperty("fields").EnumerateArray().ToArray();
        var parsed = new OfficialIddField[fields.Length];
        for (int index = 0; index < fields.Length; index++)
        {
            JsonElement field = fields[index];
            Assert.Equal(index, field.GetProperty("position").GetInt32());
            string kind = RequiredString(field, "kind");
            Assert.True(kind is "alpha" or "numeric");
            JsonElement defaultValue = field.GetProperty("default_value");
            Assert.True(defaultValue.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            parsed[index] = new OfficialIddField(
                index,
                kind,
                RequiredString(field, "name"),
                field.GetProperty("begins_extensible").GetBoolean(),
                defaultValue.ValueKind == JsonValueKind.Null ? null : defaultValue.GetString());
        }

        JsonElement extensibleStart = item.GetProperty("extensible_start_index");
        Assert.True(extensibleStart.ValueKind is JsonValueKind.Null or JsonValueKind.Number);
        return new OfficialIddObject(
            RequiredString(item, "name"),
            item.GetProperty("minimum_fields").GetInt32(),
            extensibleStart.ValueKind == JsonValueKind.Null ? null : extensibleStart.GetInt32(),
            item.GetProperty("extensible_group_size").GetInt32(),
            parsed);
    }

    private static IddObjectDefinition ParseNativeIddObject(JsonElement item)
    {
        IddFieldDefinition[] fields = item.GetProperty("fields").EnumerateArray()
            .Select((field, index) => new IddFieldDefinition(
                RequiredString(field, "token"),
                index,
                RequiredString(field, "kind") == "alpha" ? IddFieldKind.Alpha : IddFieldKind.Numeric,
                RequiredString(field, "name"),
                beginsExtensible: field.GetProperty("begins_extensible").GetBoolean(),
                defaultValue: field.GetProperty("default_value").ValueKind == JsonValueKind.Null
                    ? null
                    : field.GetProperty("default_value").GetString()))
            .ToArray();
        return new IddObjectDefinition(
            RequiredString(item, "name"),
            RequiredString(item, "group"),
            fields,
            isUnique: item.GetProperty("is_unique").GetBoolean(),
            isRequired: item.GetProperty("is_required").GetBoolean(),
            minimumFields: item.GetProperty("minimum_fields").GetInt32(),
            extensibleGroupSize: item.GetProperty("extensible_group_size").GetInt32());
    }

    private static object CreateReceipt(
        SymbolBinding symbol,
        IReadOnlyList<NativeObservation> observations)
    {
        return new
        {
            artifacts = new
            {
                fixture = new
                {
                    byte_length = OracleByteLength,
                    case_count = ExpectedCaseCount,
                    cases_sha256 = CasesSha256,
                    object_count = ExpectedPythonObjectCount,
                    path = OracleRepositoryPath,
                    complete_field_count = ExpectedPythonFieldCount,
                    sha256 = OracleSha256,
                },
                generator = new
                {
                    byte_length = GeneratorByteLength,
                    path = GeneratorRepositoryPath,
                    sha256 = GeneratorSha256,
                },
                python_validator = new
                {
                    byte_length = PythonValidatorByteLength,
                    path = PythonValidatorRepositoryPath,
                    sha256 = PythonValidatorSha256,
                },
                official_idd = new
                {
                    compressed_byte_length = IddOracleByteLength,
                    compressed_sha256 = IddOracleSha256,
                    energyplus_build = EnergyPlusBuild,
                    energyplus_version = EnergyPlusVersion,
                    official_source_byte_length = EnergyPlusIddSourceByteLength,
                    official_source_sha256 = "sha256:" + EnergyPlusIddSourceSha256,
                    oracle_schema = IddOracleSchema,
                    path = IddOracleRepositoryPath,
                },
                native_sources = NativeArtifacts.Select(item => new
                {
                    byte_length = item.ByteLength,
                    path = item.Path,
                    sha256 = item.Sha256,
                }).ToArray(),
            },
            native_binding = new
            {
                adaptation_id = symbol.AdaptationId,
                classification = "exception",
                implementation_symbol = symbol.ImplementationSymbol,
                native_target = symbol.NativeTarget,
                compatibility_options = new
                {
                    use_legacy_simple_dragon_hvac_topology = true,
                    use_legacy_simple_dragon_schedule_metadata = true,
                },
            },
            observations = observations.Select(item => new
            {
                idf_token_case_normalizations = item.TokenCaseNormalizations.Select(value => new
                {
                    field_name = value.FieldName,
                    native_value = value.NativeValue,
                    object_name = value.ObjectName,
                    object_type = value.ObjectType,
                    python_value = value.PythonValue,
                    zero_based_position = value.ZeroBasedPosition,
                }).ToArray(),
                case_id = item.CaseId,
                context_enrichments = item.ContextEnrichments.Select(value => new
                {
                    field_name = value.FieldName,
                    native_value = value.NativeValue,
                    object_name = value.ObjectName,
                    object_type = value.ObjectType,
                    zero_based_position = value.ZeroBasedPosition,
                }).ToArray(),
                native_compact_field_count = item.NativeCompactFieldCount,
                native_facts = item.NativeFacts,
                native_object_names = item.Objects.Select(value => value.Name).ToArray(),
                native_object_types = item.Objects.Select(value => value.ObjectType).ToArray(),
                native_output_sha256 = item.NativeOutputSha256,
                numeric_lexeme_normalizations = item.NumericLexemeNormalizations.Select(value => new
                {
                    field_name = value.FieldName,
                    native_value = value.NativeValue,
                    object_name = value.ObjectName,
                    object_type = value.ObjectType,
                    python_value = value.PythonValue,
                    zero_based_position = value.ZeroBasedPosition,
                }).ToArray(),
                object_order_relocations = item.ObjectRelocations.Select(value => new
                {
                    native_index = value.NativeIndex,
                    object_name = value.ObjectName,
                    object_type = value.ObjectType,
                    python_index = value.PythonIndex,
                }).ToArray(),
                omitted_blank_or_none_count = item.BlankOrNoneOmissionCount,
                omitted_official_idd_default_count = item.DefaultOmissions.Length,
                omitted_official_idd_defaults_sha256 = CanonicalSha256(
                    JsonSerializer.SerializeToElement(item.DefaultOmissions)),
                python_complete_field_count = item.PythonFieldCount,
                python_object_count = item.PythonObjectCount,
            }).ToArray(),
            representation = new
            {
                abstract_contracts_use_reflection_not_fake_emission = true,
                compact_tail_policy = "blank-or-none-or-exact-official-idd-default",
                complete_order_and_link_topology_compared = true,
                context_enrichments_and_object_relocations_reported_separately = true,
                official_idd_version = EnergyPlusVersion,
                two_call_native_emission = new
                {
                    guarantee = IsAbstractSymbol(symbol.Symbol)
                        ? "reflection-only-contract-evidence-no-native-emission"
                        : "fresh-distinct-results;deterministic-complete-output;no-source-state-mutation",
                    status = IsAbstractSymbol(symbol.Symbol)
                        ? "not_applicable_abstract_contract"
                        : "verified",
                },
            },
            scope = new
            {
                context_only_not_targeted = ContextOnlyNotTargeted,
                full_symbol_closure = false,
                scope = "bounded-common-valid-state-hvac-source-system-idf-emission",
                unresolved_behavior = UnresolvedBehavior,
            },
            upstream = new
            {
                ast_sha256 = UpstreamAstSha256,
                body_hash = symbol.BodyHash,
                commit = UpstreamCommit,
                inventory_index = symbol.InventoryIndex,
                inventory_sha256 = InventorySha256,
                path = UpstreamPath,
                signature_hash = symbol.SignatureHash,
                source_sha256 = UpstreamSourceSha256,
                symbol = symbol.Symbol,
                symbol_hash = symbol.SymbolHash,
            },
        };
    }

    private static void ValidateReceipt(
        JsonElement receipt,
        SymbolBinding symbol,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertReceiptPayloadSafe(receipt);
        AssertKeys(receipt, "artifacts", "native_binding", "observations", "representation", "scope", "upstream");
        JsonElement binding = receipt.GetProperty("native_binding");
        Assert.Equal(symbol.AdaptationId, RequiredString(binding, "adaptation_id"));
        Assert.Equal("exception", RequiredString(binding, "classification"));
        Assert.Equal(symbol.ImplementationSymbol, RequiredString(binding, "implementation_symbol"));
        Assert.Equal(symbol.NativeTarget, RequiredString(binding, "native_target"));
        JsonElement representation = receipt.GetProperty("representation");
        AssertKeys(
            representation,
            "abstract_contracts_use_reflection_not_fake_emission",
            "compact_tail_policy",
            "complete_order_and_link_topology_compared",
            "context_enrichments_and_object_relocations_reported_separately",
            "official_idd_version",
            "two_call_native_emission");
        JsonElement twoCall = representation.GetProperty("two_call_native_emission");
        AssertKeys(twoCall, "guarantee", "status");
        Assert.Equal(
            IsAbstractSymbol(symbol.Symbol)
                ? "not_applicable_abstract_contract"
                : "verified",
            RequiredString(twoCall, "status"));
        Assert.Equal(
            IsAbstractSymbol(symbol.Symbol)
                ? "reflection-only-contract-evidence-no-native-emission"
                : "fresh-distinct-results;deterministic-complete-output;no-source-state-mutation",
            RequiredString(twoCall, "guarantee"));
        JsonElement[] encoded = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(observations.Count, encoded.Length);
        Assert.Equal(
            observations.Select(item => item.CaseId),
            encoded.Select(item => RequiredString(item, "case_id")));
        Assert.Equal(symbol.Symbol, RequiredString(receipt.GetProperty("upstream"), "symbol"));
        Assert.StartsWith("sha256:", CanonicalSha256(receipt), StringComparison.Ordinal);
    }

    private static bool IsAbstractCase(CaseBinding binding) =>
        IsAbstractSymbol(binding.Symbol);

    private static bool IsAbstractSymbol(string symbol) => symbol is
        "CoolingTower.to_idf_main_object" or
        "SourceSystem.to_idf_object";

    private static void AssertPinnedArtifact(
        string repositoryPath,
        int expectedByteLength,
        string expectedSha256)
    {
        byte[] bytes = File.ReadAllBytes(FindRepositoryFile(repositoryPath));
        Assert.Equal(expectedByteLength, bytes.Length);
        Assert.Equal(expectedSha256, Sha256(bytes));
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

        throw new FileNotFoundException("Could not locate repository file '" + relativePath + "'.");
    }

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string CanonicalSha256(JsonElement value)
    {
        var builder = new StringBuilder();
        WriteCanonicalJson(builder, value);
        return Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static void WriteCanonicalJson(StringBuilder builder, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in value.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }

                    firstProperty = false;
                    AppendPythonJsonString(builder, property.Name);
                    builder.Append(':');
                    WriteCanonicalJson(builder, property.Value);
                }

                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    WriteCanonicalJson(builder, item);
                }

                builder.Append(']');
                break;
            case JsonValueKind.String:
                AppendPythonJsonString(builder, value.GetString()!);
                break;
            case JsonValueKind.Number:
                builder.Append(value.GetRawText());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new Xunit.Sdk.XunitException("Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
    }

    private static void AppendPythonJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            string[] names = value.EnumerateObject().Select(property => property.Name).ToArray();
            Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            foreach (JsonProperty property in value.EnumerateObject())
            {
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
        if (value.ValueKind == JsonValueKind.String)
        {
            Assert.False(Regex.IsMatch(
                value.GetString()!,
                @"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))",
                RegexOptions.CultureInvariant));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoHostPaths(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoHostPaths(item);
            }
        }
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number));
            Assert.True(double.IsFinite(number));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoNonFiniteJsonNumbers(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoNonFiniteJsonNumbers(item);
            }
        }
    }

    private static void AssertReceiptPayloadSafe(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.False(property.Name is
                    "consumer_contract" or
                    "expected_dotnet" or
                    "python" or
                    "python_facts" or
                    "python_outcome");
                AssertReceiptPayloadSafe(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertReceiptPayloadSafe(item);
            }
        }
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private static void AssertBooleanArray(JsonElement value, int count, bool expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        bool[] actual = value.EnumerateArray().Select(item => item.GetBoolean()).ToArray();
        Assert.Equal(count, actual.Length);
        Assert.All(actual, item => Assert.Equal(expected, item));
    }

    private sealed record SymbolBinding(
        int InventoryIndex,
        string Symbol,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string AdaptationId,
        string NativeTarget,
        string ImplementationSymbol);

    private sealed record CaseBinding(
        string CaseId,
        string Symbol,
        string FactSha256);

    private sealed record SourceBinding(
        string Module,
        string Path,
        string SourceSha256,
        string AstSha256);

    private sealed record NativeArtifact(
        string Path,
        int ByteLength,
        string Sha256);

    private sealed record NativeCaseExpectation(
        string CaseId,
        int NativeCompactFieldCount,
        int BlankOrNoneOmissionCount,
        int DefaultOmissionCount,
        int TokenCaseNormalizationCount,
        int NumericLexemeNormalizationCount,
        int ObjectRelocationCount,
        string NativeOutputSha256);

    private sealed class Scenario
    {
        public Scenario(
            CaseBinding binding,
            object[] references,
            Func<IReadOnlyList<IdfObject>>? emitter,
            Type? abstractOwner,
            string? abstractMethodName,
            string initialStateFingerprint)
        {
            Binding = binding;
            References = references;
            InitialReferences = references.ToArray();
            Emitter = emitter;
            AbstractOwner = abstractOwner;
            AbstractMethodName = abstractMethodName;
            InitialStateFingerprint = initialStateFingerprint;
        }

        public CaseBinding Binding { get; }

        public object[] References { get; }

        public object[] InitialReferences { get; }

        public Func<IReadOnlyList<IdfObject>>? Emitter { get; }

        public Type? AbstractOwner { get; }

        public string? AbstractMethodName { get; }

        public string InitialStateFingerprint { get; }
    }

    private sealed record ObjectSnapshot(
        string ObjectType,
        string Name,
        int CompactFieldCount)
    {
        public static ObjectSnapshot Create(IdfObject value) =>
            new(value.ObjectType, value.Name ?? string.Empty, value.Count);
    }

    private sealed record DefaultOmissionFact(
        string ObjectType,
        string ObjectName,
        int ZeroBasedPosition,
        string FieldName,
        string PythonEncodedValue,
        string OfficialIddDefault);

    private sealed record ContextEnrichmentFact(
        string ObjectType,
        string ObjectName,
        int ZeroBasedPosition,
        string FieldName,
        string NativeValue);

    private sealed record ObjectRelocationFact(
        string ObjectType,
        string ObjectName,
        int PythonIndex,
        int NativeIndex);

    private sealed record TokenCaseNormalizationFact(
        string ObjectType,
        string ObjectName,
        int ZeroBasedPosition,
        string FieldName,
        string PythonValue,
        string NativeValue);

    private sealed record NumericLexemeNormalizationFact(
        string ObjectType,
        string ObjectName,
        int ZeroBasedPosition,
        string FieldName,
        string PythonValue,
        string NativeValue);

    private sealed record ValueDifferenceFact(
        string ObjectType,
        string ObjectName,
        int ZeroBasedPosition,
        string FieldName,
        string PythonValue,
        string NativeValue);

    private sealed record ParityAnalysis(
        int PythonFieldCount,
        int ComparedPresentFieldCount,
        int BlankOrNoneOmissionCount,
        DefaultOmissionFact[] DefaultOmissions,
        ContextEnrichmentFact[] ContextEnrichments,
        TokenCaseNormalizationFact[] TokenCaseNormalizations,
        NumericLexemeNormalizationFact[] NumericLexemeNormalizations,
        ObjectRelocationFact[] ObjectRelocations,
        ValueDifferenceFact[] ValueDifferences);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        int PythonObjectCount,
        int PythonFieldCount,
        int NativeCompactFieldCount,
        string NativeOutputSha256,
        ObjectSnapshot[] Objects,
        int BlankOrNoneOmissionCount,
        DefaultOmissionFact[] DefaultOmissions,
        ContextEnrichmentFact[] ContextEnrichments,
        TokenCaseNormalizationFact[] TokenCaseNormalizations,
        NumericLexemeNormalizationFact[] NumericLexemeNormalizations,
        ObjectRelocationFact[] ObjectRelocations,
        string[] NativeFacts);

    private sealed record OfficialIddField(
        int Position,
        string Kind,
        string Name,
        bool BeginsExtensible,
        string? DefaultValue);

    private sealed record OfficialIddObject(
        string Name,
        int MinimumFields,
        int? ExtensibleStartIndex,
        int ExtensibleGroupSize,
        OfficialIddField[] Fields)
    {
        public OfficialIddField ResolveField(int index)
        {
            if (index < Fields.Length)
            {
                return Fields[index];
            }

            Assert.NotNull(ExtensibleStartIndex);
            Assert.True(ExtensibleGroupSize > 0);
            int prototype = ExtensibleStartIndex!.Value
                + ((index - ExtensibleStartIndex.Value) % ExtensibleGroupSize);
            return Fields[prototype];
        }

        public string ResolveFieldName(int index)
        {
            OfficialIddField field = ResolveField(index);
            if (ExtensibleStartIndex is null || index < ExtensibleStartIndex.Value)
            {
                return field.Name;
            }

            int group = ((index - ExtensibleStartIndex.Value) / ExtensibleGroupSize) + 1;
            return Regex.Replace(
                field.Name,
                @"\b1\b",
                group.ToString(CultureInfo.InvariantCulture),
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }
    }

    private sealed class OfficialIddOracle
    {
        private readonly IReadOnlyDictionary<string, OfficialIddObject> objects;

        public OfficialIddOracle(
            IEnumerable<OfficialIddObject> values,
            IddSchema nativeSchema)
        {
            objects = values.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
            NativeSchema = nativeSchema;
        }

        public IddSchema NativeSchema { get; }

        public OfficialIddObject this[string objectType] => objects[objectType];
    }
}
