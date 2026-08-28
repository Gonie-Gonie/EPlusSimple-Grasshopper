#pragma warning disable CA1861 // Immutable inline arrays keep the bounded oracle mapping readable.

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class ModelResultOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-model-result-oracle.json";
    private const int FixtureBytes = 763_720;
    private const string FixtureSha256 =
        "sha256:55d19ad2df41112fa0bb8bb1585f9e9822b68cfa4332c52b90e2aacbfd57c520";
    private const string FixtureSchema = "goniegonie.python-reference.epsimple-model-result.v1";
    private const string CasesSha256 =
        "sha256:ac4b9647caba8c1c40edc1314936fcfaaf1cfc155e0ed51f54839094484bf3cf";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_model_result_oracle.py";
    private const int GeneratorBytes = 71_783;
    private const string GeneratorSha256 =
        "sha256:5be8e30ca52aa3d820716bfbbefcfb50cda75e5b9ce0c580d0f713c3f0d84060";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_model_result_oracle.py";
    private const int ValidatorBytes = 20_902;
    private const string ValidatorSha256 =
        "sha256:4d400ad08c858bf7049ef49cd2f9a3553f4d047651a56fcc3ec752355b5946c7";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/core/model.py";
    private const string UpstreamArtifactPath =
        "temp/reference/upstream/eplussimple/src/epsimple/core/model.py";
    private const int UpstreamBytes = 36_949;
    private const string UpstreamSourceSha256 =
        "sha256:71dc9bb8d97e829c27d9b5d19ef88709af9613f9e53f60807d54ceb2922e4532";
    private const string UpstreamAstSha256 =
        "sha256:f79918272c07515ee4ae98fa62f4ca5d5d703e5e2faa334f72d6a6966e1e2447";
    private const string DependenciesSha256 =
        "sha256:85d50612b42b3818f054fd7d9cdb26a16bbf832c3afc56762ea732f55a48cb22";
    private const string RuntimeSignaturesSha256 =
        "sha256:93d5310b577faa8c6a19a409ee5dea4e23b5ff2aa086e5e8b42746a133dbf00f";
    private const string LoadedSourcesSha256 =
        "sha256:998782cc65bc94d43ffc7538fae747639503f673586bc2815aaddac4dddc1fe1";
    private const string RelocatedObservationsSha256 =
        "sha256:681dcec3e9b192e373cd31e5accd673f97c2d7234d87e5394c27a70aa14a7ca8";
    private const string AdjacentReceiptsSha256 =
        "sha256:96babe847ec683f6d00c65cedafe8d7030673247389323fb879ef650531bfb1f";
    private const string EvidenceTestCase =
        "GonieGonie.SimpleDragon.Tests.ModelResultOracleParityTests.MatchesPinnedModelResultThroughProductionPublicRoutes";

    private static readonly ArtifactPin[] NativeSources =
    {
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GreenRetrofitCsvExporter.cs", 23_696,
            "sha256:3c8ce6ae4ad2ed1de2b24f9874a4acb95029be6aff85b8d36536fbcde1febf2e"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GreenRetrofitResultBuilder.cs", 17_506,
            "sha256:9a9f1bc3c38814776c3c0ac888423418215c42bb7c270848b72b480751438b3b"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GreenRetrofitResultModels.cs", 19_280,
            "sha256:5181cc98bb9e193cae2c6c29b33ca74d6e98bf7e44f11e0e3855d9f591f4e8f7"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GrrReader.cs", 14_845,
            "sha256:498b12addde1cfc0c4e6c3931dd5c079e185cc2f45a9fa2cb5cde700f4075130"),
        new("src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GrrWriter.cs", 5_023,
            "sha256:802f6fb7592f1d48504f6d26b50a5d29e0e5305d5379265effb9efe080d5e65a"),
    };

    private static readonly ArtifactPin[] NativeData =
    {
        new("fixtures/simple-dragon/grm/ASHRAE 140 modified.grm", 9_154,
            "sha256:8e2ff63e17af29e7429b696800dbb11a5af45817cd97724481b9152b90fc76b3"),
        new("fixtures/simple-dragon/grr/ASHRAE 140 modified.grr", 19_098,
            "sha256:56fc1a200d319ceaa9e0bb5dc1e14ecc2f19c88c288049dda993a4c1adc1c7b1"),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("R01", "epsimple-model-result.class-init-area-valid-digits", "lifecycle", "sha256:4711bebc356a1f5750e9c376006c4c8aaccaab4801800f8b101df84a5797cec3", "sha256:585099dc1fb37b8b3d04ad9fdca922098ac1ff9942f6c30ab73137e92d94c249", new[] { "GreenRetrofitResult", "GreenRetrofitResult.VALID_DIGITS", "GreenRetrofitResult.__init__", "GreenRetrofitResult.area" }, Array.Empty<string>()),
        new("D01", "epsimple-model-result.domestic-hotwater-demand-calendar", "domestic-hotwater", "sha256:3f013b028e16376338fb619bce6ac8a2c56b5baa91c091f7e88f9c44e6b1e580", "sha256:724a1a94b4764ac8c8f9752f9279d2cde897922c1a92c88f337bfa8e2c24696d", new[] { "GreenRetrofitResult.get_domestic_hotwater_energy" }, new[] { "GreenRetrofitResult.area" }),
        new("D02", "epsimple-model-result.domestic-hotwater-server-selection", "domestic-hotwater", "sha256:7153bb1e6c23caf60f71a43b2f0078cb3757272a4e2354f928076abdff428045", "sha256:fe7747b329f96eac8c0206338e21c40bd2d7467d114f677b9c1228e444010b85", new[] { "GreenRetrofitResult.get_dhw_servers" }, new[] { "GreenRetrofitResult" }),
        new("D03", "epsimple-model-result.domestic-hotwater-site-energy", "domestic-hotwater", "sha256:55d6fa67b07f33630af20d31c0c98cd5731f06852acc6bbc9e68b025e1f4998f", "sha256:0bd8c10ff6fe534ad259336a9e761dff32bd89c788d38e93f470a6a5d1b3b316", new[] { "GreenRetrofitResult.calc_domestic_hotwater_site_energy" }, new[] { "GreenRetrofitResult.get_domestic_hotwater_energy", "GreenRetrofitResult.get_dhw_servers" }),
        new("S01", "epsimple-model-result.site-use-table-pv-and-boundaries", "metric", "sha256:fb0b61d263fc7a1bb48ceee75e5bbf12b9a52ba1508c055c0066a2dd25961a1e", "sha256:5dfb907078fcde732c138a8da8dcc69d8dee9ca7e28d5e44db660c6871ece454", new[] { "GreenRetrofitResult.to_site_uses" }, new[] { "GreenRetrofitResult.calc_domestic_hotwater_site_energy" }),
        new("S02", "epsimple-model-result.source-use-factors-and-enum-alias", "metric", "sha256:896a7ec68973d272609602938ac1d0b2b6f8e19267a7f9d14b8b64f9c8c1f745", "sha256:0632f3db7d87659a2c4aec581abbfe3aec3983caaa5bd4f7b0f1f075b1d4c485", new[] { "GreenRetrofitResult.to_source_uses" }, new[] { "GreenRetrofitResult.to_site_uses" }),
        new("S03", "epsimple-model-result.carbon-factors", "metric", "sha256:fc20fea5762e3fa4332ba18f67a4887e2269039ccac22d1ab873b5c95408027c", "sha256:0de2828a139d54ef81d706a0598c12815dd28ffc6ec1c9f0df57c9067ba46781", new[] { "GreenRetrofitResult.to_co2" }, new[] { "GreenRetrofitResult.to_site_uses" }),
        new("S04", "epsimple-model-result.cost-factors", "metric", "sha256:f32e918255fd5a752c65635ce9878c7ae8fe406a2007e3d1644c37774057cf7a", "sha256:06cea7332d5ce760785d2aaf16cd64a49c64d1936033fb2207fc8ae9872870b1", new[] { "GreenRetrofitResult.to_cost" }, new[] { "GreenRetrofitResult.to_site_uses" }),
        new("S05", "epsimple-model-result.summary-per-area-gross-and-shape-boundaries", "summary", "sha256:171a430247f305b222655aba236977355ac3ba78ebf50b5021f4cdc544d5dcfa", "sha256:de1915cdea10a86c600f60ec7243528421f80d775729d1c692c876db5c33b8a7", new[] { "GreenRetrofitResult.summarize" }, new[] { "GreenRetrofitResult.area" }),
        new("J01", "epsimple-model-result.dictionary-tree-and-call-topology", "serialization", "sha256:c3cd6c1c1cc439050757731424eafcd774c9093035c1da2cdd0db1c42d4b3bf5", "sha256:c0800b402b6c1c7a0b44447934c3ecfdb67c3a6b5b3964fe783203270b0361e4", new[] { "GreenRetrofitResult.to_dict" }, new[] { "GreenRetrofitResult.to_site_uses", "GreenRetrofitResult.to_source_uses", "GreenRetrofitResult.to_co2", "GreenRetrofitResult.to_cost", "GreenRetrofitResult.summarize" }),
        new("J02", "epsimple-model-result.write-json-bytes-overwrite-and-errors", "serialization", "sha256:ff638ac05baae61326c38c68c18fdd57fd44f5b6d12104dad60de9edc2ffe9bf", "sha256:348a57163273bb5b62fc7beda1ec3b1f44f9bddcfd49f46423e38d4618766c9f", new[] { "GreenRetrofitResult.write" }, new[] { "GreenRetrofitResult.to_dict" }),
    };

    private static readonly ExpectedTargetBinding[] ExpectedTargets =
    {
        Target(373, "GreenRetrofitResult", "class", "sha256:8b4073860c0a5ec5215658188d0e02cbdd83c2e792e35fc1de93180d2b76e2e0", "sha256:ad17da15ebe3f9a8b13f618e3a7d4d8a5d867b8573aab129f9bc0758c0449792", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "epsimple-model-result-373-8b407386", "exception", "reviewed-native-adaptation-immutable-complete-result-tree-rather-than-model-result-wrapper-8b407386", "GonieGonie.SimpleDragon.GreenRetrofitResult", 0),
        Target(374, "GreenRetrofitResult.VALID_DIGITS", "constant", "sha256:ff1cddacd1d221d604e80997d48ef03662bbeb531c45337abde8fcc3f9fc30df", "sha256:aa336779f69a8902021215ad36bc8925e1d599b84b1c2149a383d3313065b1a2", "sha256:ddcc9e26678f237b5f7892c086072a5962980b4d4b13bcee47bd9c0d98a52cc6", "epsimple-model-result-374-ff1cddac", "equivalent", "direct-native-greenretrofitresult-valid-digits-ff1cddac", "GonieGonie.SimpleDragon.GreenRetrofitResult.ValidDigits", 0),
        Target(375, "GreenRetrofitResult.__init__", "function", "sha256:856dd66b378dc69ca9fdf702af477ca308850afa30e1f79ddaf07c77007d2143", "sha256:e3ea637489f15196a395d06b8784e4240a686044f045de1addec871f7ee124b0", "sha256:7d8dee39517322f67931eb9ae4eeab47423ca33acb4bd9d48732687b11009213", "epsimple-model-result-375-856dd66b", "exception", "reviewed-native-adaptation-validated-factory-and-diagnostic-build-result-boundary-856dd66b", "GreenRetrofitResult.FromSiteUses(double, EnergyUseBreakdown) and GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)", 0),
        Target(376, "GreenRetrofitResult.area", "function", "sha256:37a89b1c8b8b29e09038b198162ad3edfe11206794c9b30e104febcdce483f89", "sha256:a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "sha256:7335d117f821d4cc789535e20d1f1cb563895a2e27b6fcdbe9c5bf3a1978d037", "epsimple-model-result-376-37a89b1c", "equivalent", "direct-native-greenretrofitresult-area-37a89b1c", "GonieGonie.SimpleDragon.GreenRetrofitResult.TotalArea", 0),
        Target(377, "GreenRetrofitResult.calc_domestic_hotwater_site_energy", "function", "sha256:4e80e0ef21caa93b8a0d7450676b1173677faec1ac8f3d15ad550f290b920c4c", "sha256:01ce55e2ae511cb78ed4504c328bc6d4e06786c1bbe7157feb8bd6958d2a5ede", "sha256:3d20f42d58aa292c0cda8f36c2c29aba9fcb94cb3a65fe138eed6a7d40fcb26d", "epsimple-model-result-377-4e80e0ef", "exception", "reviewed-native-adaptation-typed-server-filtering-first-id-wins-and-structured-diagnostics-4e80e0ef", "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build domestic-hot-water projection", 3),
        Target(378, "GreenRetrofitResult.get_dhw_servers", "function", "sha256:a63f6fa21523147d50860abe9915f96111ca6ace3621e57716040c9f8cc22ff3", "sha256:d2b4c877c3074459e858c8ddab98b4b507ad32ac856cab0c0358b2ff4487fce6", "sha256:757d1859c51226b31facdfb68107b5a90ce8e7c8d260e6ccb327e31f9203183c", "epsimple-model-result-378-a63f6fa2", "exception", "reviewed-native-adaptation-typed-boiler-district-filtering-rather-than-arbitrary-hotwater-object-a63f6fa2", "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build source-system filtering and grouping", 2),
        Target(379, "GreenRetrofitResult.get_domestic_hotwater_energy", "function", "sha256:b7774317313c4c32bb28168900a4ccd0af9162b9e9149f7bb58f5605784ed592", "sha256:c2d47451050e60f15a22d16146acba292a2a641fff5670ab1cec00ba7f863d58", "sha256:d43efb9ead93c11dacb01c2a869c6801e637018483c9524b390640381d1e0eb8", "epsimple-model-result-379-b7774317", "equivalent", "direct-native-greenretrofitresult-get-domestic-hotwater-energy-b7774317", "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build usage-profile domestic-hot-water demand", 1),
        Target(380, "GreenRetrofitResult.summarize", "function", "sha256:93d2bbd846d5cf13baf88fcbacddc16e948ca205b53c7e4f25fd5887dcdc3f87", "sha256:808df99bb5631c7829bf7bce92d37533bbddbb2e35281ff3add1b89d35acbab7", "sha256:c2c71105186ffc370ee09c436ac894ee6bf797989dd622d36f302633f6009b6e", "epsimple-model-result-380-93d2bbd8", "equivalent", "direct-native-greenretrofitresult-summarize-93d2bbd8", "GonieGonie.SimpleDragon.GreenRetrofitResult.PerAreaSummaries and GrossSummaries", 8),
        Target(381, "GreenRetrofitResult.to_co2", "function", "sha256:72b97e85ef6741a8eb2dfcdb37de2a27b37772b2ec054fee14a061d3a3f2d358", "sha256:3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "sha256:73699d8b52634390a3efab78dceae92be86d304fb90fbc8acc4c6092b0a2f0e6", "epsimple-model-result-381-72b97e85", "equivalent", "direct-native-greenretrofitresult-to-co2-72b97e85", "GonieGonie.SimpleDragon.GreenRetrofitResult.Carbon", 6),
        Target(382, "GreenRetrofitResult.to_cost", "function", "sha256:7d1d1cd964d4ab0842510bf94bac7aea393ed53469ed7ecdea1d7979057bf266", "sha256:3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "sha256:3363e164857a1bc4c9f5f2e9904602b4d9912b9901888e2f5e55197c4c993f30", "epsimple-model-result-382-7d1d1cd9", "equivalent", "direct-native-greenretrofitresult-to-cost-7d1d1cd9", "GonieGonie.SimpleDragon.GreenRetrofitResult.Cost", 7),
        Target(383, "GreenRetrofitResult.to_dict", "function", "sha256:010fb59959bd7ec395c6e22acccaeb73626df3fa276c4fb7e5ed1c3172a8f8d3", "sha256:b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf", "sha256:ff7f831331299a45e9c62ac55581b0c4dc6d311580a9abc84e73b53e2763324b", "epsimple-model-result-383-010fb599", "equivalent", "direct-native-greenretrofitresult-to-dict-010fb599", "GonieGonie.SimpleDragon.GrrWriter.Serialize(GreenRetrofitResult, bool)", 9),
        Target(384, "GreenRetrofitResult.to_site_uses", "function", "sha256:48114e1462753ab48eac6ca7d648438ad7e4381d4900cdbfd7618c701562bafa", "sha256:3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "sha256:b8a49f1b2b0bcbaf6c27042f1b6926bdd6954194a3db29531bdd8668d4052b7f", "epsimple-model-result-384-48114e14", "equivalent", "direct-native-greenretrofitresult-to-site-uses-48114e14", "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build and GreenRetrofitResult.SiteUses", 4),
        Target(385, "GreenRetrofitResult.to_source_uses", "function", "sha256:842eb853a7216a84eab7ccc5a04d7454fc7f2572ea9c8e0bc32f73d6ffc84291", "sha256:3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "sha256:d9c7d1b27a50ae9b04a5278c1d1881309fc297af097af411791f2f1d77e73d5d", "epsimple-model-result-385-842eb853", "equivalent", "direct-native-greenretrofitresult-to-source-uses-842eb853", "GonieGonie.SimpleDragon.GreenRetrofitResult.SourceUses", 5),
        Target(386, "GreenRetrofitResult.write", "function", "sha256:67ef521c2bdac4646a52e20ba8da306765197f8cc27846cb9d715d605d21db2e", "sha256:5294543e03913904c918f3367755b0cffe7f63c47d17de87fcd55fa0a846c288", "sha256:be074b70585f464b6e6172733e6fa39c8f8d94e716eddc77260516689568c898", "epsimple-model-result-386-67ef521c", "exception", "reviewed-native-adaptation-deterministic-grr-writer-with-terminal-newline-67ef521c", "GonieGonie.SimpleDragon.GrrWriter.WriteFile(string, GreenRetrofitResult, bool)", 10),
    };

    private static bool DiscoverPins => false;
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new() { WriteIndented = true };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new(9, "sha256:341ca031704c52a9fdf1fd2f73234f93668bb923bb3ee164c9fff139a51b3152"),
        new(7, "sha256:070aee004461100883fbd65682492bfcfa66e3d6a0a8ea1129e22dc934305da0"),
        new(9, "sha256:3b569a0aeba19cd9a8175f14760c7f847e9aa078c2db1c45d7a105a7920090c1"),
        new(9, "sha256:85f9751e058dcf6164976d461600dc1e5bb14a9e6c00955fe3f9309d00792e1b"),
        new(10, "sha256:5455a34648d4762a3ea9cc400132d49971b3d5611eb8d9f140c9805b51a15efa"),
        new(6, "sha256:90881ec4a613df2bb9251327ecefee44481c5c7e709e8a2c337c2ca02adb3067"),
        new(3, "sha256:c802c7858aa198d7f51a9d51c84cf4137590dffd5903df727cbeca113847c139"),
        new(3, "sha256:12be300b265bfcd0faea53fdf82e5fce36be2a33583daee0a0e2ae37050703bf"),
        new(8, "sha256:0d79186a4639bdfcfa613cc0c47e2824fd9fe25308727bd856686a9d10c7627b"),
        new(9, "sha256:d84b45edb56789a9ff3d8b1ab070c5e739b11e83d29391b9d7911eb3ab8ec6b7"),
        new(14, "sha256:c1b4ee2a645458bdd90e58a81700e4f1cd6f3491ba5ed3cb12ab421360cac290"),
    };
    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:1b88b24b581c4d91a226c94b3f1a647d22c1bb4264d478e1a32ecd78b239cb58",
        "sha256:6b0ad9d05323c81d7aad66fd3de9155354421eb611bd5fa55fe1885a93e9c9cc",
        "sha256:23e9b2b56149056ce8b3d2a9bc67107f6ad661e02e0a6bfe65cd2ae1e3dd7395",
        "sha256:981cf3422b0face9d13f94f2c9e3a9690c5a44c9b79de67706e764d0d5786fc0",
        "sha256:3f43de198cd4adc462bb28aa9c2e2413292257b2a84dfdb37e7b6faa0c358393",
        "sha256:71eebef3731df315ccd8036f3e0ad2c18a0ad548a1de04461cb1035bf895af49",
        "sha256:684c489703f61fa1b0fd916bd782685704c1bc409619afca19ea065df6bc2e0b",
        "sha256:246812cf086981df1f35b6baf3c2d688c4672cdf74d75be3f36f61c6fc03f817",
        "sha256:835f9f68fa71aaf8af06f4a6883d0935c0dc2dcf2ea76712d3fcd259b713d23a",
        "sha256:c65cdbd480f32d5472570b26e8fbba32750c893c5c359b51eaf518aa36ad22ce",
        "sha256:8fc60eebda78501dc1eddaa4d935570493b35a77202d4b51c7dd870ea7055e7e",
        "sha256:18c8420fda7c29c48baad8e49c1912f9af726a6b14ce9e741c857ea4f30aaa20",
        "sha256:69e804207169505e25107fdd01d20d9f0c7e0250d4253201a07f62c90b617276",
        "sha256:3a724ffa998ec81cc0d8fe4f540ddb4104a08b09bb283cd19bb52ec3b3d64a4d",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:e911c14d3c813bc5b43b2cb90a16fdb94df818527ec3da52af4ffe946e21baf7", // epsimple-model-result-373-8b407386
        "sha256:df925407b2b85ccd5c10a3a478b3da331793346d93e987542922b3b1624a0c98", // epsimple-model-result-374-ff1cddac
        "sha256:4e675409d346846b9beac564083195fcc8a4ddedc876509750a45bbffcc93b4e", // epsimple-model-result-375-856dd66b
        "sha256:3cb36d3cb8399dae6de969611d4e07c77d6354ddc49116dd6ee57f27a7c5336e", // epsimple-model-result-376-37a89b1c
        "sha256:f7391e151dd695f6dcf38a07e91f1c36e70bf9207ebf2dad8eb6664071196b80", // epsimple-model-result-377-4e80e0ef
        "sha256:065da15639826e57cfa385c5eee3c019a426d85a8ab047909ce9230c6428b5ec", // epsimple-model-result-378-a63f6fa2
        "sha256:1b0e9c6a519d97628b881db5fd98b02180667d6f221e8602d50c1dd8793f7e5a", // epsimple-model-result-379-b7774317
        "sha256:bf414e097422b1fc25f05097c0db28d5a4635b0723008f38b5efff6315657a94", // epsimple-model-result-380-93d2bbd8
        "sha256:f0ca2f5130317e1a05158356731a2e4467cfb4c951f1b78d09e7eb9b8dbe300f", // epsimple-model-result-381-72b97e85
        "sha256:6548057f35cb8b0db13a0852918d2402c27a228f10ca4e486e98a2a8abec6a52", // epsimple-model-result-382-7d1d1cd9
        "sha256:1873d250f1bf2f4333933ff902edc5008ac37465f587b2845695c00226351578", // epsimple-model-result-383-010fb599
        "sha256:c263d5ceb77af08101cf78017e8c6861821e9aa40b41eaf93cd189ef5fd7f61b", // epsimple-model-result-384-48114e14
        "sha256:c88ca4766eb1bffb2f5674ed81e9bcc5b64405526a2323d80b9f40ee6062179d", // epsimple-model-result-385-842eb853
        "sha256:bc0a0e3d5e2b47b80ce40cb836de147e21117e3bfdcef1d53e1f502f528ce03b", // epsimple-model-result-386-67ef521c
    };

    [Fact]
    public void MatchesPinnedModelResultThroughProductionPublicRoutes()
    {
        ValidatePinnedArtifactsAndPublicApi();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateOracle(oracle.RootElement);
        NativeObservation[] observations = corpus.FixtureCases
            .Select((sourceCase, index) => ObserveNativeCase(index, sourceCase))
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
                "MODEL_RESULT_NATIVE_PINS\n" + JsonSerializer.Serialize(new
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

        Assert.Equal(14, recordCount);
        Assert.Equal(14, corpus.Targets.Length);
        Assert.Equal(14, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(9, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(5, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(0, corpus.Targets.Count(item => item.Classification is not ("equivalent" or "exception")));
        Assert.Equal(11, corpus.FixtureCases.Length);
    }

    private static ExpectedTargetBinding Target(
        int inventoryIndex,
        string symbol,
        string kind,
        string symbolHash,
        string signatureHash,
        string bodyHash,
        string assertionId,
        string classification,
        string adaptationId,
        string nativeRoute,
        int caseIndex) => new(
            inventoryIndex,
            symbol,
            kind,
            symbolHash,
            signatureHash,
            bodyHash,
            assertionId,
            classification,
            adaptationId,
            nativeRoute,
            caseIndex);

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(FixturePath, FixtureBytes, FixtureSha256);
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertPinnedArtifact(UpstreamArtifactPath, UpstreamBytes, UpstreamSourceSha256);
        foreach (ArtifactPin artifact in NativeSources.Concat(NativeData))
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        Assert.True(typeof(GreenRetrofitResult).IsSealed);
        Assert.Empty(typeof(GreenRetrofitResult).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(2, GreenRetrofitResult.ValidDigits);
        AssertReadOnlyProperty<GreenRetrofitResult>(nameof(GreenRetrofitResult.TotalArea), typeof(double));
        AssertReadOnlyProperty<GreenRetrofitResult>(nameof(GreenRetrofitResult.SiteUses), typeof(EnergyUseBreakdown));
        AssertReadOnlyProperty<GreenRetrofitResult>(nameof(GreenRetrofitResult.SourceUses), typeof(EnergyUseBreakdown));
        AssertReadOnlyProperty<GreenRetrofitResult>(nameof(GreenRetrofitResult.Carbon), typeof(EnergyUseBreakdown));
        AssertReadOnlyProperty<GreenRetrofitResult>(nameof(GreenRetrofitResult.Cost), typeof(EnergyUseBreakdown));
        AssertPublicStaticMethod(typeof(GreenRetrofitResult), nameof(GreenRetrofitResult.FromSiteUses));
        AssertPublicStaticMethod(typeof(GreenRetrofitResultBuilder), nameof(GreenRetrofitResultBuilder.Build));
        AssertPublicStaticMethod(typeof(GrrReader), nameof(GrrReader.Read));
        AssertPublicStaticMethod(typeof(GrrReader), nameof(GrrReader.ReadFile));
        AssertPublicStaticMethod(typeof(GrrWriter), nameof(GrrWriter.Serialize));
        AssertPublicStaticMethod(typeof(GrrWriter), nameof(GrrWriter.WriteFile));
        AssertPublicStaticMethod(typeof(GreenRetrofitCsvExporter), nameof(GreenRetrofitCsvExporter.SerializeMonthly));
        AssertPublicStaticMethod(typeof(GreenRetrofitCsvExporter), nameof(GreenRetrofitCsvExporter.CreatePackage));
        AssertPublicStaticMethod(typeof(GreenRetrofitCsvExporter), nameof(GreenRetrofitCsvExporter.ExportDirectory));

        string[] absentPythonRoutes =
        {
            "from_csv", "from_result", "from_sqlite", "to_json", "to_monthly_csv", "to_monthly_json",
        };
        Assert.All(absentPythonRoutes, name => Assert.Null(typeof(GreenRetrofitResult).GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)));
    }

    private static void AssertReadOnlyProperty<T>(string name, Type expectedType)
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(typeof(T).GetProperty(name));
        Assert.Equal(expectedType, property.PropertyType);
        Assert.False(property.CanWrite);
        Assert.True(property.GetMethod!.IsPublic);
    }

    private static void AssertPublicStaticMethod(Type type, string name) => Assert.NotNull(
        type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(item => item.Name == name));

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
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertUniqueObjectKeysRecursive(duplicate.RootElement));
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

        TargetBinding[] targets = ValidateTargets(root);
        ValidateConsumerContract(root.GetProperty("consumer_contract"), targets);
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
            "adjacent_receipts_sha256",
            "commit",
            "inventory",
            "isolated_import",
            "source",
            "weather_resources");
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(upstream, "adjacent_receipts_sha256"));
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        JsonElement inventory = upstream.GetProperty("inventory");
        AssertKeys(inventory, "bytes", "content_sha256", "file_sha256");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));
        JsonElement source = upstream.GetProperty("source");
        AssertKeys(source, "ast_sha256", "bytes", "path", "source_sha256");
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
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
        Assert.Equal(LoadedSourcesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(RelocatedObservationsSha256, RequiredString(isolated, "relocated_observations_sha256"));
        Assert.Equal("byte-identical-epsimple-and-idragon-trees", RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        JsonElement[] modules = isolated.GetProperty("loaded_local_modules").EnumerateArray().ToArray();
        Assert.Equal(23, modules.Length);
        Assert.Equal(23, modules.Select(item => RequiredString(item, "module")).Distinct(StringComparer.Ordinal).Count());
        Assert.All(modules, item =>
        {
            AssertKeys(item, "ast_sha256", "bytes", "module", "path", "sha256");
            Assert.True(item.GetProperty("bytes").GetInt32() > 0);
            AssertSha256(RequiredString(item, "ast_sha256"));
            AssertSha256(RequiredString(item, "sha256"));
        });
        Assert.Equal(LoadedSourcesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        JsonElement loadedModel = Assert.Single(modules, item => RequiredString(item, "path") == UpstreamPath);
        Assert.Equal("epsimple.core.model", RequiredString(loadedModel, "module"));
        Assert.Equal(UpstreamBytes, loadedModel.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(loadedModel, "sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(loadedModel, "ast_sha256"));

        JsonElement[] weather = upstream.GetProperty("weather_resources").EnumerateArray().ToArray();
        Assert.Equal(2, weather.Length);
        Assert.Equal(new[] { 16_318, 38_455 }, weather.Select(item => item.GetProperty("bytes").GetInt32()));
        Assert.Equal(
            new[]
            {
                "sha256:a6949a4b3bc967aefc419f64b1da2b7180fd33a333fed0951560951831614c06",
                "sha256:ec667eeb0ade076272d23f89956add7b0f0ec7eeac6106c02a1c9c4888aa788e",
            },
            weather.Select(item => RequiredString(item, "sha256")));
        Assert.All(weather, item =>
        {
            AssertKeys(item, "bytes", "path", "sha256");
            Assert.StartsWith("epsimple/_data/weather/", RequiredString(item, "path"), StringComparison.Ordinal);
            Assert.EndsWith(".csv", RequiredString(item, "path"), StringComparison.Ordinal);
        });
    }

    private static void ValidateNativeReview(JsonElement review)
    {
        AssertKeys(review, "route_audit", "source_receipts");
        ValidateNativeRouteAudit(review.GetProperty("route_audit"));
        JsonElement[] receipts = review.GetProperty("source_receipts").EnumerateArray().ToArray();
        Assert.Equal(NativeSources.Length, receipts.Length);
        for (int index = 0; index < NativeSources.Length; index++)
        {
            AssertArtifact(
                receipts[index],
                NativeSources[index].Path,
                NativeSources[index].Bytes,
                NativeSources[index].Sha256);
        }
    }

    private static void ValidateNativeRouteAudit(JsonElement audit)
    {
        AssertKeys(
            audit,
            "from_csv",
            "from_result",
            "from_sqlite",
            "to_dict",
            "to_json",
            "to_monthly_csv",
            "to_monthly_json",
            "write");
        AssertRoute(audit, "from_csv", false, "composed-native-route",
            "InvisibleDragon EnergyPlus result parsing then GreenRetrofitResultBuilder.Build");
        AssertRoute(audit, "from_result", false, "composed-native-route",
            "GreenRetrofitResult.FromSiteUses and GreenRetrofitResultBuilder.Build");
        AssertRoute(audit, "from_sqlite", false, "intentional-absence",
            "No SimpleDragon public SQLite-specific constructor; structured EnergyPlusSimulationResult is the boundary");
        AssertRoute(audit, "to_dict", true, "equivalent-output-route",
            "GrrWriter.Serialize emits the pinned GRR dictionary topology");
        AssertRoute(audit, "to_json", false, "renamed-native-route", "GrrWriter.Serialize");
        AssertRoute(audit, "to_monthly_csv", false, "native-extension",
            "GreenRetrofitCsvExporter.SerializeMonthly");
        AssertRoute(audit, "to_monthly_json", false, "intentional-absence",
            "No monthly-only JSON route; GrrWriter.Serialize emits the complete monthly GRR tree");
        AssertRoute(audit, "write", true, "adapted-native-route", "GrrWriter.WriteFile");
    }

    private static void AssertRoute(
        JsonElement audit,
        string name,
        bool pythonMemberExists,
        string status,
        string nativeRoute)
    {
        JsonElement route = audit.GetProperty(name);
        AssertKeys(route, "native_route", "python_member_exists", "status");
        Assert.Equal(pythonMemberExists, route.GetProperty("python_member_exists").GetBoolean());
        Assert.Equal(status, RequiredString(route, "status"));
        Assert.Equal(nativeRoute, RequiredString(route, "native_route"));
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
        Assert.Equal("goniegonie.upstream-public-symbol-inventory.v2", RequiredString(inventoryDocument.RootElement, "schema"));
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
            AssertTargetProjection(inventorySymbol, expected, includeIndex: false);
            foreach (string field in new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" })
            {
                Assert.Equal(RequiredString(inventorySymbol, field), RequiredString(receipt, field));
                Assert.Equal(RequiredString(receipt, field), RequiredString(descriptor, field));
            }

            targets[index] = new TargetBinding(
                expected.InventoryIndex,
                expected.Symbol,
                expected.Kind,
                expected.SymbolHash,
                expected.SignatureHash,
                expected.BodyHash,
                expected.AssertionId,
                expected.Classification,
                expected.AdaptationId,
                expected.NativeRoute,
                expected.CaseIndex);
        }

        int[] adjacentIndices = Enumerable.Range(337, 52)
            .Except(ExpectedTargets.Select(item => item.InventoryIndex))
            .ToArray();
        Assert.Equal(38, adjacentIndices.Length);
        Assert.Equal(
            new[]
            {
                337, 338, 339, 340, 341, 342, 343, 344, 345, 346,
                347, 348, 349, 350, 351, 352, 353, 354, 355, 356,
                357, 358, 359, 360, 361, 362, 363, 364, 365, 366,
                367, 368, 369, 370, 371, 372, 387, 388,
            },
            adjacentIndices);
        JsonElement adjacent = JsonSerializer.SerializeToElement(adjacentIndices.Select(inventoryIndex => new
        {
            body_hash = RequiredString(inventorySymbols[inventoryIndex], "body_hash"),
            inventory_index = inventoryIndex,
            kind = RequiredString(inventorySymbols[inventoryIndex], "kind"),
            path = RequiredString(inventorySymbols[inventoryIndex], "path"),
            signature_hash = RequiredString(inventorySymbols[inventoryIndex], "signature_hash"),
            symbol = RequiredString(inventorySymbols[inventoryIndex], "symbol"),
            symbol_hash = RequiredString(inventorySymbols[inventoryIndex], "symbol_hash"),
        }).ToArray());
        Assert.Equal(AdjacentReceiptsSha256, CanonicalSha256(adjacent));
        Assert.Equal(Enumerable.Range(373, 14), targets.Select(item => item.InventoryIndex));
        Assert.Equal(
            Enumerable.Range(337, 52),
            targets.Select(item => item.InventoryIndex).Concat(adjacentIndices).OrderBy(item => item));
        return targets;
    }

    private static void AssertTargetProjection(
        JsonElement item,
        ExpectedTargetBinding expected,
        bool includeIndex)
    {
        AssertKeys(
            item,
            includeIndex
                ? new[] { "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash" }
                : new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" });
        if (includeIndex)
        {
            Assert.Equal(expected.InventoryIndex, item.GetProperty("inventory_index").GetInt32());
        }

        Assert.Equal(expected.Symbol, RequiredString(item, "symbol"));
        Assert.Equal(expected.Kind, RequiredString(item, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(item, "path"));
        Assert.Equal(expected.SymbolHash, RequiredString(item, "symbol_hash"));
        Assert.Equal(expected.SignatureHash, RequiredString(item, "signature_hash"));
        Assert.Equal(expected.BodyHash, RequiredString(item, "body_hash"));
    }

    private static void ValidateConsumerContract(JsonElement contract, IReadOnlyList<TargetBinding> targets)
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
            "native_route_audit",
            "native_routes",
            "runtime_signatures");
        Assert.Equal(11, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId));
        JsonElement counts = contract.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(9, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(5, counts.GetProperty("exception").GetInt32());

        JsonElement assertions = contract.GetProperty("assertion_ids");
        JsonElement classifications = contract.GetProperty("classifications");
        JsonElement adaptations = contract.GetProperty("adaptations");
        JsonElement nativeRoutes = contract.GetProperty("native_routes");
        JsonElement expectations = contract.GetProperty("expectations");
        JsonElement coverage = contract.GetProperty("coverage_by_symbol");
        JsonElement signatures = contract.GetProperty("runtime_signatures");
        string[] symbols = ExpectedTargets.Select(item => item.Symbol).ToArray();
        AssertKeys(assertions, symbols);
        AssertKeys(classifications, symbols);
        AssertKeys(adaptations, symbols);
        AssertKeys(nativeRoutes, symbols);
        AssertKeys(expectations, symbols);
        AssertKeys(coverage, symbols);
        AssertKeys(signatures, symbols);
        Assert.Equal(RuntimeSignaturesSha256, CanonicalSha256(signatures));
        foreach (ExpectedTargetBinding expected in ExpectedTargets)
        {
            Assert.Equal(expected.AssertionId, RequiredString(assertions, expected.Symbol));
            Assert.Equal(expected.Classification, RequiredString(classifications, expected.Symbol));
            Assert.Equal(expected.AdaptationId, RequiredString(adaptations, expected.Symbol));
            Assert.Equal(expected.NativeRoute, RequiredString(nativeRoutes, expected.Symbol));
            Assert.Equal(Cases[expected.CaseIndex].CaseId, RequiredString(coverage, expected.Symbol));
            JsonElement expectation = expectations.GetProperty(expected.Symbol);
            AssertKeys(expectation, "adaptation", "assertion_id", "classification", "native_route");
            Assert.Equal(expected.AdaptationId, RequiredString(expectation, "adaptation"));
            Assert.Equal(expected.AssertionId, RequiredString(expectation, "assertion_id"));
            Assert.Equal(expected.Classification, RequiredString(expectation, "classification"));
            Assert.Equal(expected.NativeRoute, RequiredString(expectation, "native_route"));
            JsonElement signature = signatures.GetProperty(expected.Symbol);
            Assert.Equal(JsonValueKind.Object, signature.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(signature, "type")));
        }

        JsonElement closure = contract.GetProperty("closure");
        AssertKeys(
            closure,
            "adjacent_count",
            "adjacent_indices",
            "exact_one_case_target_partition",
            "full_model_source_partition",
            "source_declaration_count",
            "target_count",
            "target_indices",
            "target_symbols");
        Assert.Equal(38, closure.GetProperty("adjacent_count").GetInt32());
        Assert.Equal(Enumerable.Range(337, 52).Except(Enumerable.Range(373, 14)),
            closure.GetProperty("adjacent_indices").EnumerateArray().Select(item => item.GetInt32()));
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_model_source_partition").GetBoolean());
        Assert.Equal(52, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(14, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(Enumerable.Range(373, 14),
            closure.GetProperty("target_indices").EnumerateArray().Select(item => item.GetInt32()));
        AssertStringArray(closure.GetProperty("target_symbols"), symbols);

        JsonElement evidence = contract.GetProperty("evidence_contract");
        AssertKeys(
            evidence,
            "active_energyplus_process_claim",
            "exact_cpython_behavior_oracle",
            "expected_receipt_count",
            "native_csv_or_sqlite_execution_claim",
            "path_independent_relocated_import",
            "target_coverage_complete");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.True(evidence.GetProperty("exact_cpython_behavior_oracle").GetBoolean());
        Assert.Equal(14, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.False(evidence.GetProperty("native_csv_or_sqlite_execution_claim").GetBoolean());
        Assert.True(evidence.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
        ValidateNativeRouteAudit(contract.GetProperty("native_route_audit"));
        Assert.Equal(ExpectedTargets.Select(item => item.AssertionId), targets.Select(item => item.AssertionId));
        Assert.Equal(ExpectedTargets.Select(item => item.Classification), targets.Select(item => item.Classification));
        Assert.Equal(ExpectedTargets.Select(item => item.AdaptationId), targets.Select(item => item.AdaptationId));
        Assert.Equal(ExpectedTargets.Select(item => item.NativeRoute), targets.Select(item => item.NativeRoute));
    }

    private static NativeObservation ObserveNativeCase(int index, JsonElement fixtureCase) => index switch
    {
        0 => ObserveR01(fixtureCase),
        1 => ObserveD01(),
        2 => ObserveD02(),
        3 => ObserveD03(),
        4 => ObserveS01(),
        5 => ObserveS02(),
        6 => ObserveS03(),
        7 => ObserveS04(),
        8 => ObserveS05(),
        9 => ObserveJ01(),
        10 => ObserveJ02(),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
    };

    private static NativeObservation ObserveR01(JsonElement fixtureCase)
    {
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(48d, EnergyUseBreakdown.Empty);
        Assert.Equal(48d, result.TotalArea);
        Assert.Same(EnergyUseBreakdown.Empty, result.SiteUses);
        Assert.Throws<ArgumentOutOfRangeException>(() => GreenRetrofitResult.FromSiteUses(0d, EnergyUseBreakdown.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => GreenRetrofitResult.FromSiteUses(-1d, EnergyUseBreakdown.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => GreenRetrofitResult.FromSiteUses(double.NaN, EnergyUseBreakdown.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => GreenRetrofitResult.FromSiteUses(double.PositiveInfinity, EnergyUseBreakdown.Empty));
        Assert.Throws<ArgumentNullException>(() => GreenRetrofitResult.FromSiteUses(48d, null!));

        JsonElement presence = fixtureCase.GetProperty("python").GetProperty("facts")
            .GetProperty("requested_route_member_presence");
        AssertKeys(presence, "from_csv", "from_result", "from_sqlite", "to_json", "to_monthly_csv", "to_monthly_json");
        Assert.All(presence.EnumerateObject(), item => Assert.False(item.Value.GetBoolean()));
        return Observation("R01",
            "sealed=" + Boolean(typeof(GreenRetrofitResult).IsSealed),
            "public_constructor_count=" + typeof(GreenRetrofitResult).GetConstructors().Length.ToString(CultureInfo.InvariantCulture),
            "valid_digits=" + GreenRetrofitResult.ValidDigits.ToString(CultureInfo.InvariantCulture),
            "total_area=" + Double(result.TotalArea),
            "total_area_read_only=" + Boolean(!typeof(GreenRetrofitResult).GetProperty(nameof(GreenRetrofitResult.TotalArea))!.CanWrite),
            "site_uses_identity_retained=" + Boolean(ReferenceEquals(EnergyUseBreakdown.Empty, result.SiteUses)),
            "invalid_area_types=ArgumentOutOfRangeException|ArgumentOutOfRangeException|ArgumentOutOfRangeException|ArgumentOutOfRangeException",
            "null_site_uses_type=ArgumentNullException",
            "python_requested_member_presence=from_csv:false|from_result:false|from_sqlite:false|to_json:false|to_monthly_csv:false|to_monthly_json:false");
    }

    private static NativeObservation ObserveD01()
    {
        GreenRetrofitModel model = LoadModel();
        GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(DummyMonthlyTable()));
        Assert.True(build.Success, Describe(build.Diagnostics));
        Assert.Empty(build.Diagnostics);
        GreenRetrofitResult result = build.RequireResult();
        MonthlySeries lpg = result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.LiquefiedPetroleumGas];
        Assert.Equal(12, lpg.Count);
        Assert.Equal(.78d, lpg[0]);
        Assert.Equal(.71d, lpg[1]);
        return Observation("D01",
            "builder_success=" + Boolean(build.Success),
            "model_area=" + Double(model.Area),
            "schedule_default_year=" + GonieGonie.InvisibleDragon.Profile.Schedule.DefaultYear.ToString(CultureInfo.InvariantCulture),
            "lpg_hotwater_month_count=" + lpg.Count.ToString(CultureInfo.InvariantCulture),
            "lpg_hotwater=" + Join(lpg.Select(Double)),
            "lpg_hotwater_annual=" + Double(Math.Round(lpg.Sum, 2, MidpointRounding.ToEven)),
            "diagnostic_count=" + build.Diagnostics.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static NativeObservation ObserveD02()
    {
        GreenRetrofitModel template = LoadModel();
        SourceSystem[] sources =
        {
            Boiler("D02-LPG", FuelType.LiquefiedPetroleumGas, .8d),
            District("D02-DISTRICT"),
            Boiler("D02-INACTIVE", FuelType.Oil, .9d, hotWater: false),
            new SourceSystem(
                "D02-HEATPUMP",
                SourceSystemType.HeatPump,
                fuelType: FuelType.Electricity,
                heatingCop: 3d,
                hotWaterSupply: true,
                id: new EntityId("D02-HEATPUMP")),
        };
        GreenRetrofitResult mixed = GreenRetrofitResultBuilder.Build(
            WithSources(template, sources),
            Simulation(DummyMonthlyTable())).RequireResult();
        double lpg = mixed.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.LiquefiedPetroleumGas][0];
        double district = mixed.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.DistrictHeating][0];
        double oil = mixed.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.Oil][0];
        double electricity = mixed.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.Electricity][0];
        Assert.True(lpg > 0d);
        Assert.True(district > 0d);
        Assert.Equal(0d, oil);
        Assert.Equal(0d, electricity);

        GreenRetrofitResult fallback = GreenRetrofitResultBuilder.Build(
            WithSources(template, Array.Empty<SourceSystem>()),
            Simulation(DummyMonthlyTable())).RequireResult();
        Assert.True(fallback.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.NaturalGas][0] > 0d);
        SourceSystem duplicateA = Boiler("D02-DUP", FuelType.NaturalGas, .85d);
        SourceSystem duplicateB = new(
            "D02-DUP-OTHER",
            SourceSystemType.DistrictHeating,
            hotWaterSupply: true,
            id: new EntityId("D02-DUP"));
        Assert.Throws<ArgumentException>(() => WithSources(template, new[] { duplicateA, duplicateB }));
        return Observation("D02",
            "declared_source_types=" + Join(sources.Select(item => item.Type.ToString())),
            "selected_server_count=2",
            "lpg_january=" + Double(lpg),
            "district_january=" + Double(district),
            "inactive_oil_january=" + Double(oil),
            "heatpump_electricity_january=" + Double(electricity),
            "no_server_fallback_carrier=NaturalGas",
            "no_server_fallback_january=" + Double(fallback.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.NaturalGas][0]),
            "duplicate_id_type=ArgumentException");
    }

    private static NativeObservation ObserveD03()
    {
        GreenRetrofitModel template = LoadModel();
        GreenRetrofitModel mixedModel = WithSources(template, new[]
        {
            Boiler("D03-LPG", FuelType.LiquefiedPetroleumGas, .8d),
            District("D03-DISTRICT"),
        });
        GreenRetrofitResult mixed = GreenRetrofitResultBuilder.Build(
            mixedModel,
            Simulation(DummyMonthlyTable())).RequireResult();
        double lpg = mixed.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.LiquefiedPetroleumGas][0];
        double district = mixed.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.DistrictHeating][0];
        Assert.Equal(.41d, lpg);
        Assert.Equal(.33d, district);
        Assert.Throws<ArgumentOutOfRangeException>(() => Boiler(
            "D03-ZERO",
            FuelType.NaturalGas,
            0d));
        GreenRetrofitModel zeroArea = WithSources(
            template,
            Array.Empty<SourceSystem>(),
            Array.Empty<BuildingFloor>());
        GreenRetrofitResultBuildResult invalid = GreenRetrofitResultBuilder.Build(
            zeroArea,
            Simulation(DummyMonthlyTable()));
        Assert.False(invalid.Success);
        Assert.Equal("SD.GRR.MODEL_AREA_INVALID", Assert.Single(invalid.Diagnostics).Code);
        return Observation("D03",
            "server_count=2",
            "lpg_efficiency=" + Double(.8d),
            "district_efficiency=" + Double(1d),
            "split_lpg_january=" + Double(lpg),
            "split_district_january=" + Double(district),
            "rounding_digits=" + GreenRetrofitResult.ValidDigits.ToString(CultureInfo.InvariantCulture),
            "zero_efficiency_type=ArgumentOutOfRangeException",
            "zero_area_build_success=" + Boolean(invalid.Success),
            "zero_area_diagnostic=" + invalid.Diagnostics[0].Code);
    }

    private static NativeObservation ObserveS01()
    {
        GreenRetrofitModel model = WithSources(LoadModel(), Array.Empty<SourceSystem>());
        EnergyPlusTabularTable electricity = MonthlyRows(
            "EndUseEnergyConsumptionElectricityMonthly",
            ElectricityHeadings,
            month => new[]
            {
                48d * month,
                24d * month,
                4.8d * month,
                2.4d * month,
                9.6d * month,
                4.8d * month,
                2.4d * month,
                2.4d * month,
                999d,
            });
        EnergyPlusTabularTable balance = MonthlyRows(
            "ElectricityBalanceMonthly",
            new[] { "ElectricityProduced:Facility [kWh]", "ElectricitySurplusSold:Facility [kWh]" },
            month => new[] { 96d * month, 48d * month });
        GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(electricity, balance));
        Assert.True(build.Success, Describe(build.Diagnostics));
        GreenRetrofitResult result = build.RequireResult();
        Assert.Equal(1d, result.SiteUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]);
        Assert.Equal(.5d, result.SiteUses[EnergyEndUse.Cooling, EnergyCarrier.Electricity][0]);
        Assert.Equal(.15d, result.SiteUses[EnergyEndUse.Lighting, EnergyCarrier.Electricity][0]);
        Assert.Equal(.2d, result.SiteUses[EnergyEndUse.Equipment, EnergyCarrier.Electricity][0]);
        Assert.Equal(.2d, result.SiteUses[EnergyEndUse.Circulation, EnergyCarrier.Electricity][0]);
        Assert.Equal(1d, result.SiteUses[EnergyEndUse.Generators, EnergyCarrier.Electricity][0]);
        Assert.True(result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.NaturalGas][0] > 0d);

        GreenRetrofitResultBuildResult missing = GreenRetrofitResultBuilder.Build(model, Simulation());
        Assert.Equal("SD.GRR.MONTHLY_TABLES_MISSING", Assert.Single(missing.Diagnostics).Code);
        GreenRetrofitResultBuildResult severe = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(new[] { electricity }, severeCount: 1));
        Assert.Equal("SD.GRR.ENERGYPLUS_FAILED", Assert.Single(severe.Diagnostics).Code);
        GreenRetrofitResultBuildResult allowed = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(new[] { electricity }, severeCount: 1),
            new GreenRetrofitResultBuildOptions { AllowSevereDiagnostics = true });
        Assert.True(allowed.Success, Describe(allowed.Diagnostics));
        Assert.Equal("SD.GRR.ENERGYPLUS_SEVERE_ALLOWED", Assert.Single(allowed.Diagnostics).Code);
        return Observation("S01",
            "heating_electricity_january=" + Double(result.SiteUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]),
            "cooling_electricity_january=" + Double(result.SiteUses[EnergyEndUse.Cooling, EnergyCarrier.Electricity][0]),
            "lighting_electricity_january=" + Double(result.SiteUses[EnergyEndUse.Lighting, EnergyCarrier.Electricity][0]),
            "equipment_electricity_january=" + Double(result.SiteUses[EnergyEndUse.Equipment, EnergyCarrier.Electricity][0]),
            "circulation_electricity_january=" + Double(result.SiteUses[EnergyEndUse.Circulation, EnergyCarrier.Electricity][0]),
            "generator_electricity_january=" + Double(result.SiteUses[EnergyEndUse.Generators, EnergyCarrier.Electricity][0]),
            "fallback_hotwater_naturalgas_january=" + Double(result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.NaturalGas][0]),
            "missing_table_diagnostic=" + missing.Diagnostics[0].Code,
            "severe_diagnostic=" + severe.Diagnostics[0].Code,
            "severe_allowed_diagnostic=" + allowed.Diagnostics[0].Code);
    }

    private static NativeObservation ObserveS02()
    {
        EnergyUseBreakdown site = DeterministicMatrix();
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(48d, site);
        Assert.Same(site, result.SiteUses);
        EnergyCarrier[] carriers = Enum.GetValues<EnergyCarrier>();
        string source = Join(carriers.Select(carrier =>
            carrier + ":" + Double(result.SourceUses[EnergyEndUse.Heating, carrier][0])));
        string constants = Join(carriers.Select(carrier =>
            carrier + ":" + Double(EnergyConversionFactors.SiteToSource(carrier))));
        Assert.Equal(30.25d, result.SourceUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]);
        Assert.Equal(13.2d, result.SourceUses[EnergyEndUse.Heating, EnergyCarrier.NaturalGas][0]);
        Assert.Equal(9.46d, result.SourceUses[EnergyEndUse.Heating, EnergyCarrier.LiquefiedPetroleumGas][0]);
        Assert.Equal(14d, result.SourceUses[EnergyEndUse.Heating, EnergyCarrier.Oil][0]);
        Assert.Equal(15d, result.SourceUses[EnergyEndUse.Heating, EnergyCarrier.DistrictHeating][0]);
        return Observation("S02",
            "site_identity_retained=" + Boolean(ReferenceEquals(site, result.SiteUses)),
            "source_heating_january=" + source,
            "declared_source_constants=" + constants,
            "python_alias_lpg_factor=" + Double(.728d),
            "python_alias_oil_factor=" + Double(1d),
            "python_alias_district_factor=" + Double(1d));
    }

    private static NativeObservation ObserveS03()
    {
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(48d, DeterministicMatrix());
        EnergyCarrier[] carriers = Enum.GetValues<EnergyCarrier>();
        foreach (EnergyCarrier carrier in carriers)
        {
            double expected = Math.Round(
                result.SiteUses[EnergyEndUse.Heating, carrier][0]
                * EnergyConversionFactors.SiteToCarbon(carrier),
                GreenRetrofitResult.ValidDigits,
                MidpointRounding.ToEven);
            Assert.Equal(expected, result.Carbon[EnergyEndUse.Heating, carrier][0]);
        }

        return Observation("S03",
            "carbon_heating_january=" + Join(carriers.Select(carrier =>
                carrier + ":" + Double(result.Carbon[EnergyEndUse.Heating, carrier][0]))),
            "carbon_factors=" + Join(carriers.Select(carrier =>
                carrier + ":" + Double(EnergyConversionFactors.SiteToCarbon(carrier)))),
            "month_count=" + result.Carbon[EnergyEndUse.Heating, EnergyCarrier.Electricity].Count.ToString(CultureInfo.InvariantCulture));
    }

    private static NativeObservation ObserveS04()
    {
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(48d, DeterministicMatrix());
        EnergyCarrier[] carriers = Enum.GetValues<EnergyCarrier>();
        foreach (EnergyCarrier carrier in carriers)
        {
            Assert.True(result.Cost[EnergyEndUse.Heating, carrier][0] > 0d);
        }

        return Observation("S04",
            "cost_heating_january=" + Join(carriers.Select(carrier =>
                carrier + ":" + Double(result.Cost[EnergyEndUse.Heating, carrier][0]))),
            "cost_factors=" + Join(carriers.Select(carrier =>
                carrier + ":" + Double(EnergyConversionFactors.SiteToCost(carrier)))),
            "month_count=" + result.Cost[EnergyEndUse.Heating, EnergyCarrier.Electricity].Count.ToString(CultureInfo.InvariantCulture));
    }

    private static NativeObservation ObserveS05()
    {
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(48d, DeterministicMatrix());
        GreenRetrofitSummary perArea = result.PerAreaSummaries[GreenRetrofitMetric.SiteUses];
        GreenRetrofitSummary gross = result.GrossSummaries[GreenRetrofitMetric.SiteUses];
        Assert.Equal(5, perArea.CarrierTotals.Count);
        Assert.Equal(7, perArea.EndUseTotals.Count);
        Assert.Equal(12, perArea.MonthlyTotal.Count);
        Assert.False(perArea.Gross);
        Assert.True(gross.Gross);
        Assert.Equal(Math.Round(perArea.AnnualTotal * result.TotalArea, 2, MidpointRounding.ToEven), gross.AnnualTotal);
        double expectedElectricity = Enum.GetValues<EnergyEndUse>()
            .Where(item => item != EnergyEndUse.Generators)
            .Sum(item => result.SiteUses[item, EnergyCarrier.Electricity].Sum)
            - result.SiteUses[EnergyEndUse.Generators, EnergyCarrier.Electricity].Sum;
        Assert.Equal(expectedElectricity, perArea.CarrierTotals[EnergyCarrier.Electricity]);
        Assert.Throws<ArgumentException>(() => new MonthlySeries(new double[11]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthlySeries(
            Enumerable.Repeat(0d, 11).Append(double.NaN)));
        return Observation("S05",
            "per_area_carrier_count=" + perArea.CarrierTotals.Count.ToString(CultureInfo.InvariantCulture),
            "per_area_end_use_count=" + perArea.EndUseTotals.Count.ToString(CultureInfo.InvariantCulture),
            "per_area_month_count=" + perArea.MonthlyTotal.Count.ToString(CultureInfo.InvariantCulture),
            "per_area_annual=" + Double(perArea.AnnualTotal),
            "gross_annual=" + Double(gross.AnnualTotal),
            "generator_subtracted=true",
            "short_series_type=ArgumentException",
            "nonfinite_series_type=ArgumentOutOfRangeException");
    }

    private static NativeObservation ObserveJ01()
    {
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(48d, DeterministicMatrix());
        string json = GrrWriter.Serialize(result, writeIndented: false);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        string[] keys = document.RootElement.EnumerateObject().Select(item => item.Name).ToArray();
        string[] expectedKeys =
        {
            "building", "constants", "site_uses", "source_uses", "co2", "cost", "summary_per_area", "summary_gross",
        };
        Assert.Equal(expectedKeys, keys);
        GrrReadResult read = GrrReader.Read(json);
        Assert.True(read.Success, Describe(read.Diagnostics));
        Assert.Equal(json, GrrWriter.Serialize(read.RequireResult(), writeIndented: false));
        GrrReadResult invalid = GrrReader.Read("{not-json");
        Assert.False(invalid.Success);
        Assert.Equal("SD.GRR.JSON_INVALID", Assert.Single(invalid.Diagnostics).Code);
        return Observation("J01",
            "root_keys=" + Join(keys),
            "terminal_lf=" + Boolean(json.EndsWith('\n')),
            "serialized_bytes=" + Encoding.UTF8.GetByteCount(json).ToString(CultureInfo.InvariantCulture),
            "serialized_sha256=" + TextSha256(json),
            "reader_success=" + Boolean(read.Success),
            "reader_diagnostic_count=" + read.Diagnostics.Count.ToString(CultureInfo.InvariantCulture),
            "round_trip_identical=true",
            "invalid_json_diagnostic=" + invalid.Diagnostics[0].Code,
            "native_route_claim=GrrWriter.Serialize_then_GrrReader.Read");
    }

    private static NativeObservation ObserveJ02()
    {
        string root = Path.Combine(
            FindRepositoryRoot(),
            "temp",
            "tests",
            "model-result-oracle",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        try
        {
            GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(48d, DeterministicMatrix());
            string grrPath = Path.Combine(root, "result.grr");
            GrrWriter.WriteFile(grrPath, result, writeIndented: false);
            byte[] grrBytes = File.ReadAllBytes(grrPath);
            Assert.False(grrBytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
            Assert.Equal((byte)'\n', grrBytes[^1]);
            Assert.True(GrrReader.ReadFile(grrPath).Success);
            GrrWriter.WriteFile(grrPath, result, writeIndented: true);
            Assert.Contains("\n  \"building\"", File.ReadAllText(grrPath), StringComparison.Ordinal);
            Assert.Throws<DirectoryNotFoundException>(() => GrrWriter.WriteFile(
                Path.Combine(root, "missing", "result.grr"),
                result));
            GrrReadResult missing = GrrReader.ReadFile(Path.Combine(root, "absent.grr"));
            Assert.False(missing.Success);
            Assert.Equal("SD.GRR.FILE_READ_FAILED", Assert.Single(missing.Diagnostics).Code);

            string byFuel = GreenRetrofitCsvExporter.SerializeMonthly(
                result,
                GreenRetrofitSeriesGrouping.Fuel,
                "CASE-J02");
            string byEndUse = GreenRetrofitCsvExporter.SerializeMonthly(
                result,
                GreenRetrofitSeriesGrouping.EndUse,
                "CASE-J02");
            int fuelLines = CountLines(byFuel);
            int endUseLines = CountLines(byEndUse);
            Assert.Equal(481, fuelLines);
            Assert.Equal(673, endUseLines);
            GreenRetrofitCsvPackage package = GreenRetrofitCsvExporter.CreatePackage(
                result,
                caseId: "CASE-J02");
            Assert.Equal(8, package.Files.Count);
            string exportPath = Path.Combine(root, "csv");
            GreenRetrofitCsvExportResult preview = GreenRetrofitCsvExporter.ExportDirectory(
                exportPath,
                result,
                caseId: "CASE-J02",
                export: false);
            Assert.False(preview.ExportRequested);
            Assert.False(preview.Written);
            Assert.Equal(8, preview.FilePaths.Count);
            Assert.False(Directory.Exists(exportPath));
            GreenRetrofitCsvExportResult written = GreenRetrofitCsvExporter.ExportDirectory(
                exportPath,
                result,
                caseId: "CASE-J02",
                export: true,
                overwrite: false);
            Assert.True(written.ExportRequested);
            Assert.True(written.Written);
            Assert.Equal(8, Directory.GetFiles(exportPath).Length);
            Assert.Throws<IOException>(() => GreenRetrofitCsvExporter.ExportDirectory(
                exportPath,
                result,
                caseId: "CASE-J02",
                export: true,
                overwrite: false));
            Assert.All(
                Directory.GetFiles(exportPath, "*.csv"),
                path => Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })));
            return Observation("J02",
                "grr_terminal_lf=true",
                "grr_utf8_bom=false",
                "grr_overwrite=true",
                "grr_missing_parent_type=DirectoryNotFoundException",
                "grr_missing_file_diagnostic=" + missing.Diagnostics[0].Code,
                "monthly_fuel_line_count=" + fuelLines.ToString(CultureInfo.InvariantCulture),
                "monthly_enduse_line_count=" + endUseLines.ToString(CultureInfo.InvariantCulture),
                "csv_package_file_count=" + package.Files.Count.ToString(CultureInfo.InvariantCulture),
                "csv_package_names=" + Join(package.Files.Select(item => item.Name)),
                "csv_preview_written=" + Boolean(preview.Written),
                "csv_export_written=" + Boolean(written.Written),
                "csv_bom=true",
                "csv_overwrite_guard_type=IOException",
                "python_absence_claims=fixture_native_route_audit_only");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static NativeObservation Observation(string code, params string[] facts)
    {
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        return new NativeObservation(code, facts, StringListSha256(facts));
    }

    private static readonly string[] Months =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    private static readonly string[] ElectricityHeadings =
    {
        "HEATING [kWh]",
        "COOLING [kWh]",
        "INTERIORLIGHTS [kWh]",
        "EXTERIORLIGHTS [kWh]",
        "INTERIOREQUIPMENT [kWh]",
        "FANS [kWh]",
        "PUMPS [kWh]",
        "HEATRECOVERY [kWh]",
        "WATERSYSTEMS [kWh]",
    };

    private static GreenRetrofitModel LoadModel() => GrmReader.ReadFile(
        FindRepositoryFile(NativeData[0].Path)).RequireModel();

    private static GreenRetrofitModel WithSources(
        GreenRetrofitModel source,
        IEnumerable<SourceSystem> sources,
        IEnumerable<BuildingFloor>? floors = null) => new(
            source.Name,
            source.NorthAxis,
            source.Address,
            source.Vintage,
            source.IsMultifamilyHousing,
            floors ?? source.Floors,
            source.Materials,
            source.SurfaceConstructions,
            source.FenestrationConstructions,
            sources,
            source.SupplySystems,
            source.VentilationSystems,
            source.PhotovoltaicSystems,
            source.Weather);

    private static SourceSystem Boiler(
        string id,
        FuelType fuel,
        double efficiency,
        bool hotWater = true) => new(
            id,
            SourceSystemType.Boiler,
            fuelType: fuel,
            efficiency: efficiency,
            hotWaterSupply: hotWater,
            id: new EntityId(id));

    private static SourceSystem District(string id) => new(
        id,
        SourceSystemType.DistrictHeating,
        hotWaterSupply: true,
        id: new EntityId(id));

    private static EnergyUseBreakdown DeterministicMatrix() => EnergyUseBreakdown.Create(
        (endUse, carrier) => Enumerable.Range(0, 12).Select(month =>
            ((int)endUse + 1) * 10d + ((int)carrier + 1) + month * .25d));

    private static EnergyPlusTabularTable DummyMonthlyTable() => MonthlyRows(
        "Unused",
        new[] { "Value" },
        _ => new[] { 0d });

    private static EnergyPlusTabularTable MonthlyRows(
        string reportName,
        IReadOnlyList<string> headings,
        Func<int, IReadOnlyList<double>> values)
    {
        EnergyPlusTabularCell[] header = new[] { Text("Month") }
            .Concat(headings.Select(Text))
            .ToArray();
        EnergyPlusTabularRow[] rows = Months
            .Select((month, index) => new EnergyPlusTabularRow(
                new[] { Text(month) }
                    .Concat(values(index + 1).Select(Number))
                    .ToArray()))
            .ToArray();
        return new EnergyPlusTabularTable(
            "eplustbl.csv",
            reportName,
            "Entire Facility",
            new[] { reportName },
            new EnergyPlusTabularRow(header),
            rows,
            isMonthly: true);
    }

    private static EnergyPlusTabularCell Text(string value) => new(value, null);

    private static EnergyPlusTabularCell Number(double value) => new(
        value.ToString(CultureInfo.InvariantCulture),
        value);

    private static EnergyPlusSimulationResult Simulation(params EnergyPlusTabularTable[] tables) =>
        Simulation(tables, severeCount: 0);

    private static EnergyPlusSimulationResult Simulation(
        IReadOnlyList<EnergyPlusTabularTable> tables,
        int severeCount) => new(
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

    private static int CountLines(string value) => value
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Length;

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
                relocated_observations_sha256 = RelocatedObservationsSha256,
                adjacent_receipts_sha256 = AdjacentReceiptsSha256,
                source_location_count = 2,
            },
            artifacts = new
            {
                fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
                generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                upstream_source = Artifact(UpstreamArtifactPath, UpstreamBytes, UpstreamSourceSha256),
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
                exact_cpython_behavior_oracle = true,
                native_csv_or_sqlite_execution_claim = false,
                path_independent_relocated_import = true,
                target_coverage_complete = true,
                python_absent_member_claim_source = "pinned-native-route-audit-only",
                target_count = 14,
                case_count = 11,
                adjacent_count = 38,
                claim_policy = "only-the-pinned-python-case-and-observed-production-public-route-are-claimed",
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
            "adjacent_receipts_sha256",
            "ast_sha256",
            "bytes",
            "commit",
            "dependencies_sha256",
            "inventory_content_sha256",
            "loaded_sources_sha256",
            "path",
            "relocated_observations_sha256",
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
        Assert.Equal(RelocatedObservationsSha256, RequiredString(upstream, "relocated_observations_sha256"));
        Assert.Equal(AdjacentReceiptsSha256, RequiredString(upstream, "adjacent_receipts_sha256"));
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
        AssertKeys(
            artifacts,
            "fixture",
            "generator",
            "native_data",
            "native_sources",
            "public_inventory",
            "python_validator",
            "upstream_source");
        AssertArtifact(artifacts.GetProperty("fixture"), FixturePath, FixtureBytes, FixtureSha256);
        AssertArtifact(artifacts.GetProperty("generator"), GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertArtifact(artifacts.GetProperty("python_validator"), ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertArtifact(artifacts.GetProperty("public_inventory"), InventoryPath, InventoryBytes, InventoryFileSha256);
        AssertArtifact(artifacts.GetProperty("upstream_source"), UpstreamArtifactPath, UpstreamBytes, UpstreamSourceSha256);
        AssertArtifactArray(artifacts.GetProperty("native_sources"), NativeSources);
        AssertArtifactArray(artifacts.GetProperty("native_data"), NativeData);

        JsonElement scope = receipt.GetProperty("scope");
        AssertKeys(
            scope,
            "active_energyplus_process_claim",
            "adjacent_count",
            "case_count",
            "claim_policy",
            "exact_cpython_behavior_oracle",
            "native_csv_or_sqlite_execution_claim",
            "path_independent_relocated_import",
            "python_absent_member_claim_source",
            "target_count",
            "target_coverage_complete");
        Assert.False(scope.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.Equal(38, scope.GetProperty("adjacent_count").GetInt32());
        Assert.Equal(11, scope.GetProperty("case_count").GetInt32());
        Assert.Equal(14, scope.GetProperty("target_count").GetInt32());
        Assert.True(scope.GetProperty("exact_cpython_behavior_oracle").GetBoolean());
        Assert.False(scope.GetProperty("native_csv_or_sqlite_execution_claim").GetBoolean());
        Assert.True(scope.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(scope.GetProperty("target_coverage_complete").GetBoolean());
        Assert.Equal("pinned-native-route-audit-only", RequiredString(scope, "python_absent_member_claim_source"));
        Assert.Equal(
            "only-the-pinned-python-case-and-observed-production-public-route-are-claimed",
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
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
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
}
