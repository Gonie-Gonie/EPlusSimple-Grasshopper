using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.BuildingEnergy.Contracts;
using Dragons.EnergyPlus.Runtime;
using Dragons.UpstreamTracker;
using DragonPackageInfo = Dragons.InvisibleDragon.PackageInfo;
using ZoneProfile = Dragons.InvisibleDragon.Profile.Profile;

namespace Dragons.InvisibleDragon.Tests.Common;

public sealed class ConstantsMetadataOracleParityTests
{
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        WriteIndented = true,
    };

    private const string FixturePath =
        "fixtures/reference/python-0.7.0/constants-metadata-oracle.json";
    private const int FixtureBytes = 117_140;
    private const string FixtureSha256 =
        "sha256:0bd15a140cf0fa50b40c984083e7e0ea68ddfbf20f55c6c21efc3e89c7b7cc13";
    private const string CasesSha256 =
        "sha256:e664bc6349a4965d94f50f5fcf31d544b5472163496a7117146fb3f9ce83a4e0";

    private const string GeneratorPath =
        "tools/python-reference/generate_constants_metadata_oracle.py";
    private const int GeneratorBytes = 66_735;
    private const string GeneratorSha256 =
        "sha256:2616391c849abb65e774fd6c596671cf3f0ec534e367c9821a999c129ecf66b1";

    private const string ValidatorPath =
        "tests/PythonReference/test_constants_metadata_oracle.py";
    private const int ValidatorBytes = 22_802;
    private const string ValidatorSha256 =
        "sha256:3db88718364c7b631d32874b7e02de73b6218a8a179dc3e008ca3218959e5139";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";

    private const string UpstreamPath = "src/idragon/constants.py";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const int UpstreamBytes = 2_590;
    private const string UpstreamSourceSha256 =
        "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520";
    private const string UpstreamAstSha256 =
        "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084";

    private const string RuntimeResolverPath =
        "src/Shared/Dragons.EnergyPlus.Runtime/RuntimeResolver.cs";
    private const int RuntimeResolverBytes = 14_588;
    private const string RuntimeResolverSha256 =
        "sha256:ae290360e832f99eb6190744684624fda003172428b668cb7d47ba84f28f35b2";
    private const string RuntimeLayoutPath =
        "src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusRuntimeLayout.cs";
    private const int RuntimeLayoutBytes = 2_296;
    private const string RuntimeLayoutSha256 =
        "sha256:5552379c29e2f60e0edd5d2762d3468c605fd5a8e47aec65f3eab9f6c758458b";
    private const string ProfilePath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Profile/Profile.cs";
    private const int ProfileBytes = 4_067;
    private const string ProfileSha256 =
        "sha256:670c41d252c47be93f5bc839967332a1aba33061a2eb832b532e658b1b3683fd";
    private const string PackageInfoPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/PackageInfo.cs";
    private const int PackageInfoBytes = 400;
    private const string PackageInfoSha256 =
        "sha256:933a0d70a9cfed35e91a4ea0f31452c487e56ff43984387fb8d030b2fdc28385";
    private const string CoreProjectPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Dragons.InvisibleDragon.Core.csproj";
    private const int CoreProjectBytes = 770;
    private const string CoreProjectSha256 =
        "sha256:f364545115f52ce395f541e0daf0516a2ac99c1358c3eb0aab68b7ac6700b03d";
    private const string BuildPropsPath = "Directory.Build.props";
    private const int BuildPropsBytes = 1_828;
    private const string BuildPropsSha256 =
        "sha256:d7765c3f0aba0ca5aaa30706649475481fdeac4bd128be6449014b46759734f1";

    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Common.ConstantsMetadataOracleParityTests.MatchesPinnedConstantsMetadataThroughBoundedNativeAdaptations";

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new(RuntimeResolverPath, RuntimeResolverBytes, RuntimeResolverSha256),
        new(RuntimeLayoutPath, RuntimeLayoutBytes, RuntimeLayoutSha256),
        new(ProfilePath, ProfileBytes, ProfileSha256),
        new(PackageInfoPath, PackageInfoBytes, PackageInfoSha256),
        new(CoreProjectPath, CoreProjectBytes, CoreProjectSha256),
        new(BuildPropsPath, BuildPropsBytes, BuildPropsSha256),
    };

    private static readonly CaseBinding[] Cases =
    {
        new(
            "C01",
            "constants-metadata.c01-directory-import-topology",
            "directory",
            new[] { "Directory", "Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR" },
            Array.Empty<string>(),
            "sha256:01838d77ce2aa61318bf555e87eca84a0ce331baaec9d7334d0873a7f003b93d",
            "sha256:87e43700e8ba25721a4ee21650b3624560f1d6a91fc8219884b1c97fc2f5b095"),
        new(
            "C02",
            "constants-metadata.c02-directory-two-location-relocation",
            "directory",
            new[] { "Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR" },
            new[] { "Directory" },
            "sha256:82fce5c4b885ae5d8a216d807c6e2510b156e7fdfad8e9ce90425f04592c0c5f",
            "sha256:44249e9ea23786160e587dae0f83625fbf8518eace0cefb3d2f9caa9603b2fb3"),
        new(
            "C03",
            "constants-metadata.c03-directory-class-attribute-mutation",
            "directory",
            new[] { "Directory", "Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR" },
            Array.Empty<string>(),
            "sha256:4633fc76d67fc86b86dd02cb5645cf63b0d1f1b1d0685fef32e4d9921c0a8801",
            "sha256:bf9457fdb18d10c5ba8dfd269ac6d92c72dd99d473b7827b3e010e34d0ad66e7"),
        new(
            "C04",
            "constants-metadata.c04-directory-instance-shadow-and-construction-errors",
            "directory",
            new[] { "Directory" },
            new[] { "Directory.ENERGYPLUS_DIR", "Directory.IDD_DIR", "Directory.PROFILE_DIR" },
            "sha256:310219d06e081b707886cf51be5fd0cbdffc3f71ef175cfbe93245319f5c79e1",
            "sha256:c364e07d87ce34695dbd16e7f508dded48c476f0cc2edb9ecb7aeb2f7c4e4873"),
        new(
            "C05",
            "constants-metadata.c05-package-info-topology-and-values",
            "package",
            new[] { "PackageInfo", "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION" },
            Array.Empty<string>(),
            "sha256:a12f09475a94391b4080dc1d268e3ef3f658a859c08e39e6d609a9b561ddc615",
            "sha256:299cc5a1727fb2a56e2259a819c4fd3afc27265e7d85754e6e26795d72ce54b6"),
        new(
            "C06",
            "constants-metadata.c06-package-info-class-attribute-mutation",
            "package",
            new[] { "PackageInfo", "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION" },
            Array.Empty<string>(),
            "sha256:0c68707d19661edbe03301456c008c83c2a9ca189f4e96b62f4888a9a88c5a0c",
            "sha256:b6119202c85bff43231484a0403f470cde457e759b6d8da60d1f9a5a0d2d2e4a"),
        new(
            "C07",
            "constants-metadata.c07-package-info-instance-shadow-and-construction-errors",
            "package",
            new[] { "PackageInfo" },
            new[] { "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION" },
            "sha256:5f1c49066b0e6e2834ebd814c4a3ec9478f6bef6a9529f95632f4e76fa66380f",
            "sha256:687aea6edaa94ce326633a784c3e678e92ce3fe3791da88861c25a5fcdbe847a"),
        new(
            "C08",
            "constants-metadata.c08-package-name-string-operations",
            "package",
            new[] { "PackageInfo.NAME" },
            new[] { "PackageInfo" },
            "sha256:94c63c6bb305e4f6ee772888a6fa7bff0d320204a7cff7ce9c1512c58e0184ca",
            "sha256:f3fa7c3244a09346e39ff52705dc191b57521ea8691aa83bab4e0741885ef8d7"),
        new(
            "C09",
            "constants-metadata.c09-package-version-tuple-operations",
            "package",
            new[] { "PackageInfo.VERSION" },
            new[] { "PackageInfo" },
            "sha256:faa96f7e572407ded80e4eaedf19a9b3bfdac4f595d4ff02da6cdd6871714170",
            "sha256:3f5d9a78a7a86c1b76684e025c087374a3a7452e4f700c0d6eeb4c257c1a7582"),
        new(
            "C10",
            "constants-metadata.c10-required-python-comparison-and-errors",
            "package",
            new[] { "PackageInfo.REQUIRED_PYTHON" },
            new[] { "PackageInfo" },
            "sha256:2615482afc26f694b11b990929b265d9c5bb18c1b9a376c29b3b665b22635c5b",
            "sha256:bc1b4ff8c2e4315c865b6a0985bef3bf777a5fee5beec93d0d5c73d5a14d7e81"),
    };

    private static readonly TargetBinding[] Targets =
    {
        new(568, "Directory", "class",
            "sha256:5b876ad7fd9b11f66cc01ecb6c43d4e143b6f0258ba070c02551d968dd68aaf6",
            "sha256:9b095b8323bc225f2dc984ce84b448beb1c9ca385a260e7fc7fa0e20e9518d24",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "constants-metadata-568-5b876ad7", "resolved-native-runtime-and-resource-layout",
            "Dragons.EnergyPlus.Runtime.RuntimeResolver and caller-supplied resource paths",
            new[] { 0, 2, 3 }, new[] { 1 }),
        new(569, "Directory.ENERGYPLUS_DIR", "constant",
            "sha256:7e01ceac3f311fa9fbf2fde2b25cc1c7cd16c3b3f16a3dae9f55531d25ecef5d",
            "sha256:3b7cde5117ef1f4f50cc31536156cfc47972e4891e14cef1873dbb21670bec45",
            "sha256:4c60beb875b71c3866ad7b2f6c4c2976c58edba859a3eb364608665539a37a30",
            "constants-metadata-569-7e01ceac", "explicit-validated-native-energyplus-runtime-root",
            "EnergyPlusRuntimeLayout.RootPath after manifest and payload validation",
            new[] { 0, 1, 2 }, new[] { 3 }),
        new(570, "Directory.IDD_DIR", "constant",
            "sha256:1f0c2815e4e0732316c71edc653a9a35e5081466805dfbf900c10971f1d171d5",
            "sha256:fc2b368da7a4f29b674e0243a9cc5f51932a415e16ce648aaa6f0952f2d5b803",
            "sha256:611dabcf2c487823916965244bd620d3e2e8142f13418e7037648c2412df96b4",
            "constants-metadata-570-1f0c2815", "validated-native-idd-path-resolution",
            "EnergyPlusRuntimeLayout.IddPath or an explicit Grasshopper IDD path",
            new[] { 0, 1, 2 }, new[] { 3 }),
        new(571, "Directory.PROFILE_DIR", "constant",
            "sha256:f65d5eaefa2bc1cbb6f0c9b5904624194a1551f48e7966c7973d35526bad4fa6",
            "sha256:e63d078f01a7657c55c23cdbdaa3fdc0b1bf9367a911885dd5706e22cf728d36",
            "sha256:14d09e816f44d227fd4799d8ebd5d1c6d1f0fccb28985882f54864bb86696fe8",
            "constants-metadata-571-f65d5eae", "typed-native-profile-data-without-package-profile-directory",
            "typed Dragons.InvisibleDragon.Profile values supplied by callers",
            new[] { 0, 1, 2 }, new[] { 3 }),
        new(572, "PackageInfo", "class",
            "sha256:aaf5b98d4a7dc29f83b698f1fb2881b7bb258885bd2aaf17a53b6da902d1eda1",
            "sha256:2740bb2f2c36f7a928b58073cf72c4f955c0b9fbbb13d6586049071934b22209",
            "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
            "constants-metadata-572-aaf5b98d", "static-native-package-information",
            "Dragons.InvisibleDragon.PackageInfo static class",
            new[] { 4, 5, 6 }, new[] { 7, 8, 9 }),
        new(573, "PackageInfo.NAME", "constant",
            "sha256:3942a963fcf59af7b1a181bea940b7a883dec4f7059b042451842334e47768cd",
            "sha256:8a07b85ef52202817199529eb85bd9e57dc995f6e07f09ced1aeec0baf40513e",
            "sha256:cc58b284eb83d3af52e586f2af522b33b3dcb4a63d5675f752047b256874cce3",
            "constants-metadata-573-3942a963", "native-invisibledragon-package-name",
            "Dragons.InvisibleDragon.PackageInfo.Name (InvisibleDragon)",
            new[] { 4, 5, 7 }, new[] { 6 }),
        new(574, "PackageInfo.REQUIRED_PYTHON", "constant",
            "sha256:cf74d0eb707a3668aa515bdd31d767109337841bcf28f03b96c6e9264d9407a4",
            "sha256:bda307293305fe13f76bb51ed2cdbf08110bf353393c5a3ba9b2c6e48c1825a8",
            "sha256:1f50b949f3e09514616d8d527374472d470a2693413a93ccf8df89205c4814c2",
            "constants-metadata-574-cf74d0eb", "compiled-native-target-framework-contract",
            "net48, net7.0-windows, and net8.0-windows build targets",
            new[] { 4, 5, 9 }, new[] { 6 }),
        new(575, "PackageInfo.VERSION", "constant",
            "sha256:a8260e5f38f8422e1ac38ce24fd0136b4bb3a4de24f268e9a262aa6034031ea4",
            "sha256:5c9774e81f3886d7a93f4152480e1ed58f8749a486ce411bd4e0830807b1e6e7",
            "sha256:81e16ccea394a6f22e27d6a26210439f9099e8217d9d38c3f411ae7bd3f43936",
            "constants-metadata-575-a8260e5f", "native-semantic-version-string",
            "Dragons.InvisibleDragon.PackageInfo.Version (0.1.2)",
            new[] { 4, 5, 8 }, new[] { 6 }),
    };

    private static readonly SourceReceipt[] ResolvedSpecialTagReceipts =
    {
        new(576, "SpecialTag", "class",
            "sha256:3a4b37818bef17a26ede76602478983f0d70840c5a61fce8475f47e491466e41",
            "sha256:2d310be6e0c12953280b4ae7c32d74687bf07cf40743879660dcbd25a74b4cc3",
            "sha256:0f180b3be66f76d002ae59f4b778f5dda999b86b84a380a298e4c5ee331e1fa9"),
        new(577, "SpecialTag.__format__", "function",
            "sha256:4ef932bb8135c4cfaf7e17e805cfb299e50d9400f4a106605bdb2fb75477d3a0",
            "sha256:9cdfbe97dbd56c9709c1449cead8a30f8c529922f871002291db5ef625709ba0",
            "sha256:6446560bba87c8aff916dace718057f6b9a03bb1ea1d04171ece5cb8516bc6c8"),
        new(578, "SpecialTag.__repr__", "function",
            "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e",
            "sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab",
            "sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8"),
        new(579, "SpecialTag.__str__", "function",
            "sha256:13ed292afebbf1a59717e776df9d6ba3e220d2cc248ac2cc450deab9c2261c98",
            "sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab",
            "sha256:41f39e586a619e17144cefe663b44ef26f20f8e6dcb13433bcb31ebd4c066f1f"),
    };

    private static readonly NativePin[] ExpectedNativePins =
    {
        new(6, "sha256:726e47d97f16443ee03dd9582cd0b6cb7066f60cd57daefe2d144c8763556f4d"),
        new(6, "sha256:1c3f51a841d18aadc18b851f5210664148fa8f483a9c2470a81350334382404f"),
        new(5, "sha256:a45d2d6de335b367f59eeb2e1755bd527a3b70c5615bb8e65e32deb97801319e"),
        new(5, "sha256:7b04129d43384c0f4b1362adfb4cc9f8bd4f3c6222fa2a0c2c6a848513d4eb31"),
        new(5, "sha256:17ebb1675f653fa58ced8024505c7748e42fceaa76434a015184abacbe72ebce"),
        new(5, "sha256:714d0d52983faf68e560cd80cba151a5e28af6e53f85402ff913c9792bf0c95f"),
        new(5, "sha256:5569777d65f20ccdc963adb2d871a8d6574d1b38266d73dd00528f1780adf6f3"),
        new(6, "sha256:b0f439aa593d6010d6b267059d66ba6cb83dc2825dbd6f0a64b1c5e286bbda50"),
        new(5, "sha256:a2f670d396c069a8b4b36fee22bf29f697ed5fbae6e5f5f0d695441d4f1c7798"),
        new(6, "sha256:9b027c0e89ce55c99bd036aa9e99121fedc2a6d1f5ff2d8d7248d16e5ddee1b1"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:0f2d14c42d42f298582eea56c68ccff8bcdb529b6b46f62abe32f7008089b037",
        "sha256:c04ff6693002dd618726423aef024fecf018187e4da672c671d300081e5b20cf",
        "sha256:728d979a870b16310d0be9c1b5be1f016f6732fe461cbb3eaae327ed97eab6f3",
        "sha256:ead05bd05e2dc66a464732dc248106e747ee1263474335d3c53c7f2d0872e71f",
        "sha256:ad76215fd35edababaa90be19f8dd585769ebb53f75f284fc2095d3317463de6",
        "sha256:e118b7662e7b419f407cbc830d22ebd2a9f47ffbac8de33d000e82e551d0b857",
        "sha256:9921458bc9c834d7f27a432c089cff812d473192197e4998ea959fd93b91c8f5",
        "sha256:bbfeaa6056580d29f18bf1bcd687dc180aacfddec7c43d5637045f905f458930",
    };

    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:399dc79ecd5f0c9aa520efac66b926af9694c102fd6318ba919cff6eecef3896",
        "sha256:22d61c40bebb9817197e78cbb6b3353c48c471676242689ba64234a8a3d5a85a",
        "sha256:1ed232b36511c05220553039d2d774decc637ad81be76c18dd55f1f23f68b006",
        "sha256:13506910df2c33a01fcb5376ed85aecf1240e6c509ce89d1aa9455928d4f0166",
        "sha256:75b93bdfe73fefcb963b9b2e73dd200f8abc406b0e6d61e6a6f414aef4fe3a8d",
        "sha256:a567ed0d0b1ad33d735ebc678f5938fd3ec7c1c2e8835ce07ae977ab30fe8d4b",
        "sha256:48642b169fedda75b44969a49380bc77ef6c43cbbb14af69925e5cc560ac016d",
        "sha256:bda279b108ed40b8e0c8751a2c178e11192c4f8341e199e957f8fe8c981037ec",
    };

    private static bool DiscoverPins => string.Equals(
        Environment.GetEnvironmentVariable("DRAGONS_DISCOVER_CONSTANTS_METADATA_PINS"),
        "1",
        StringComparison.Ordinal);

    [Fact]
    public async Task MatchesPinnedConstantsMetadataThroughBoundedNativeAdaptations()
    {
        Assert.Equal(Enumerable.Range(568, 8), Targets.Select(item => item.InventoryIndex));
        Assert.Equal(Targets.Length, Targets.Select(item => item.Symbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Targets.Length, Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enumerable.Range(576, 4), ResolvedSpecialTagReceipts.Select(item => item.InventoryIndex));
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        string fixtureFullPath = FindRepositoryFile(FixturePath);
        byte[] fixtureBytes = File.ReadAllBytes(fixtureFullPath);
        Assert.Equal(FixtureBytes, fixtureBytes.Length);
        Assert.Equal(FixtureSha256, Sha256(fixtureBytes));
        Assert.Equal((byte)'\n', fixtureBytes[^1]);
        Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(fixtureBytes), StringComparison.Ordinal);

        using JsonDocument oracle = JsonDocument.Parse(fixtureBytes);
        ValidateOracle(oracle.RootElement);

        using var temporary = new TemporaryRoot();
        RuntimePair runtimePair = await CreateRuntimePairAsync(temporary.Path);
        var observations = new List<NativeObservation>(Cases.Length);
        for (int index = 0; index < Cases.Length; index++)
        {
            string[] facts = await ObserveNativeCaseAsync(index, runtimePair);
            Assert.NotEmpty(facts);
            Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
            observations.Add(new NativeObservation(
                Cases[index].CaseId,
                facts,
                CanonicalSha256(JsonSerializer.SerializeToElement(facts))));
        }

        object[] receipts = Targets.Select(target => CreateReceipt(target, observations)).ToArray();
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
                "CONSTANTS_METADATA_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    cases = observations.Select(item => new
                    {
                        item.CaseId,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipts = Targets.Select((target, index) => new
                    {
                        target.Symbol,
                        target.AssertionId,
                        receipt_sha256 = receiptHashes[index],
                        collector_output_sha256 = collectorOutputHashes[index],
                    }),
                }, DiscoveryJsonOptions));
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Count);
        for (int index = 0; index < observations.Count; index++)
        {
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }
        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
        Assert.Equal(ExpectedCollectorOutputHashes, collectorOutputHashes);

        for (int index = 0; index < Targets.Length; index++)
        {
            JsonElement receipt = JsonSerializer.SerializeToElement(receipts[index]);
            AssertNoHostPaths(receipt);
            Assert.Equal(Targets[index].AssertionId, RequiredString(receipt, "assertion_id"));
            TrustedEvidenceRecorder.Record(
                Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
        }
    }

    private static void ValidateOracle(JsonElement root)
    {
        AssertUniqueKeysRecursive(root);
        AssertKeys(root,
            "case_sha256", "cases", "cases_sha256", "consumer_contract", "fact_sha256",
            "resolved_receipts", "runtime", "schema", "symbols", "target_receipts", "upstream");
        Assert.Equal("dragons.python-reference.constants-metadata.v1", RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        JsonElement source = upstream.GetProperty("source");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));
        JsonElement inventory = upstream.GetProperty("inventory");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));

        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());

        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        JsonElement factHashes = root.GetProperty("fact_sha256");
        JsonElement caseHashes = root.GetProperty("case_sha256");
        for (int index = 0; index < Cases.Length; index++)
        {
            CaseBinding binding = Cases[index];
            JsonElement item = fixtureCases[index];
            Assert.Equal(binding.CaseId, RequiredString(item, "id"));
            Assert.Equal(binding.Subfamily, RequiredString(item, "subfamily"));
            AssertStringArray(item.GetProperty("target_symbols"), binding.TargetSymbols);
            AssertStringArray(item.GetProperty("context_symbols"), binding.ContextSymbols);
            JsonElement python = item.GetProperty("python");
            Assert.Equal(binding.FactsSha256, RequiredString(python, "facts_sha256"));
            Assert.Equal(binding.FactsSha256, CanonicalSha256(python.GetProperty("facts")));
            Assert.Equal(binding.Scenario, RequiredString(python.GetProperty("facts"), "scenario"));
            Assert.Equal(binding.FactsSha256, RequiredString(factHashes, binding.CaseId));
            Assert.Equal(binding.CaseSha256, CanonicalSha256(item));
            Assert.Equal(binding.CaseSha256, RequiredString(caseHashes, binding.CaseId));
        }

        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.Equal(8, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        Assert.Equal(0, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());
        Assert.Equal(
            "proposed-not-yet-cross-language-verified",
            RequiredString(contract, "native_binding_status"));
        Assert.Equal("anchor-relative-parts-only-no-host-absolute-paths", RequiredString(contract, "path_encoding"));
        Assert.Equal(10, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(contract.GetProperty("case_ids"), Cases.Select(item => item.CaseId).ToArray());
        AssertStringArray(contract.GetProperty("target_symbols"), Targets.Select(item => item.Symbol).ToArray());
        foreach (TargetBinding target in Targets)
        {
            Assert.Equal("exception", RequiredString(contract.GetProperty("classifications"), target.Symbol));
            Assert.Equal(target.AdaptationId, RequiredString(contract.GetProperty("adaptations"), target.Symbol));
            Assert.Equal(target.AssertionId, RequiredString(contract.GetProperty("assertion_ids"), target.Symbol));
            Assert.Equal(target.NativeTarget, RequiredString(contract.GetProperty("native_adaptation_candidates"), target.Symbol));
        }

        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("target_coverage_complete").GetBoolean());
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        AssertStringArray(
            closure.GetProperty("private_context_members_observed"),
            "Directory._DATA_DIR", "Directory._MODULE_ROOT", "Directory._PACKAGE_ROOT");
        Assert.Equal(8, closure.GetProperty("unresolved_boundaries").GetArrayLength());

        ValidateSourceReceipts(root.GetProperty("target_receipts"), Targets.Select(item => item.SourceReceipt).ToArray());
        ValidateSourceReceipts(root.GetProperty("resolved_receipts"), ResolvedSpecialTagReceipts);
        ValidateSourceReceipts(
            closure.GetProperty("resolved_receipts_not_retargeted"),
            ResolvedSpecialTagReceipts);
        ValidateInventoryScope();
    }

    private static void ValidateInventoryScope()
    {
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllBytes(FindRepositoryFile(InventoryPath)));
        JsonElement root = inventory.RootElement;
        Assert.Equal(InventoryContentSha256, RequiredString(root, "content_sha256"));
        JsonElement symbols = root.GetProperty("symbols");
        foreach (TargetBinding target in Targets)
        {
            JsonElement item = symbols[target.InventoryIndex];
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(target.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(target.SymbolHash, RequiredString(item, "symbol_hash"));
        }
        foreach (SourceReceipt receipt in ResolvedSpecialTagReceipts)
        {
            JsonElement item = symbols[receipt.InventoryIndex];
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(receipt.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(receipt.SymbolHash, RequiredString(item, "symbol_hash"));
        }

        string[] ePlusSimpleSymbols =
        {
            "Directory", "Directory.CONSTRUCTION_DIR", "Directory.PROFILE_DIR",
            "Directory.WEATHER_DATA_DIR", "Directory.WEATHER_META_DIR", "PackageInfo",
            "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION",
        };
        for (int index = 0; index < ePlusSimpleSymbols.Length; index++)
        {
            JsonElement item = symbols[31 + index];
            Assert.Equal("src/epsimple/constants.py", RequiredString(item, "path"));
            Assert.Equal(ePlusSimpleSymbols[index], RequiredString(item, "symbol"));
        }
    }

    private static async Task<string[]> ObserveNativeCaseAsync(int index, RuntimePair runtimes)
    {
        return index switch
        {
            0 => ObserveDirectoryTopology(),
            1 => ObserveDirectoryRelocation(runtimes),
            2 => ObserveDirectoryImmutability(),
            3 => await ObserveDirectoryConstructionAsync(),
            4 => ObservePackageTopologyAndValues(),
            5 => ObservePackageImmutability(),
            6 => ObservePackageConstruction(),
            7 => ObservePackageName(),
            8 => ObservePackageVersion(),
            9 => ObserveRequiredPythonAdaptation(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }

    private static string[] ObserveDirectoryTopology()
    {
        ConstructorInfo[] resolverConstructors = typeof(RuntimeResolver).GetConstructors();
        PropertyInfo root = RequiredProperty(typeof(EnergyPlusRuntimeLayout), nameof(EnergyPlusRuntimeLayout.RootPath));
        PropertyInfo idd = RequiredProperty(typeof(EnergyPlusRuntimeLayout), nameof(EnergyPlusRuntimeLayout.IddPath));
        var profile = new ZoneProfile(
            new EntityId("CONSTANTS-METADATA-PROFILE"),
            "Constants Metadata Profile");
        Assert.Equal(2, resolverConstructors.Length);
        Assert.Equal(typeof(string), root.PropertyType);
        Assert.Equal(typeof(string), idd.PropertyType);
        Assert.Null(typeof(ZoneProfile).GetProperty("ProfileDirectory", BindingFlags.Public | BindingFlags.Static));
        Assert.Equal("Constants Metadata Profile", profile.Name);
        Assert.Empty(profile.ToIdfObjects());
        return new[]
        {
            "native-directory-container=absent",
            "native-runtime-route=RuntimeResolver",
            "native-runtime-resolver-public-constructor-count=2",
            "native-root-path-type=System.String",
            "native-idd-path-type=System.String",
            "native-profile-storage=typed-caller-supplied",
        };
    }

    private static string[] ObserveDirectoryRelocation(RuntimePair runtimes)
    {
        Assert.NotEqual(runtimes.First.RootPath, runtimes.Second.RootPath);
        Assert.Equal("runtime", Path.GetFileName(runtimes.First.RootPath));
        Assert.Equal("runtime", Path.GetFileName(runtimes.Second.RootPath));
        Assert.Equal("Energy+.idd", Path.GetFileName(runtimes.First.IddPath));
        Assert.Equal("Energy+.idd", Path.GetFileName(runtimes.Second.IddPath));
        Assert.True(File.Exists(runtimes.First.IddPath));
        Assert.True(File.Exists(runtimes.Second.IddPath));
        return new[]
        {
            "native-two-explicit-roots-distinct=true",
            "native-root-leaf=runtime",
            "native-idd-leaf=Energy+.idd",
            "native-root-selection=caller-supplied",
            "native-runtime-integrity=all-required-payload-sha256-verified",
            "host-absolute-paths=not-serialized",
        };
    }

    private static string[] ObserveDirectoryImmutability()
    {
        PropertyInfo root = RequiredProperty(typeof(EnergyPlusRuntimeLayout), nameof(EnergyPlusRuntimeLayout.RootPath));
        PropertyInfo idd = RequiredProperty(typeof(EnergyPlusRuntimeLayout), nameof(EnergyPlusRuntimeLayout.IddPath));
        Assert.False(root.CanWrite);
        Assert.False(idd.CanWrite);
        Assert.True(typeof(ZoneProfile).IsSealed);
        Assert.Null(typeof(ZoneProfile).GetProperty("ProfileDirectory", BindingFlags.Public | BindingFlags.Static));
        return new[]
        {
            "native-root-path-setter=absent",
            "native-idd-path-setter=absent",
            "native-profile-directory-member=absent",
            "native-profile-contract=sealed-typed-value",
            "python-class-attribute-mutation=not-preserved",
        };
    }

    private static async Task<string[]> ObserveDirectoryConstructionAsync()
    {
        var first = new RuntimeResolver();
        var second = new RuntimeResolver();
        Assert.NotSame(first, second);
        EnergyPlusRuntimeResolution failure = await first.ResolveAsync(null!);
        Assert.False(failure.IsSuccess);
        Assert.Equal(EnergyPlusFailureCategory.UserInput, failure.Failure?.Category);
        Assert.Equal("RESOLVE_OPTIONS_REQUIRED", failure.Failure?.Code);
        Assert.Empty(failure.AttemptedRoots);
        return new[]
        {
            "native-runtime-resolver-instances-distinct=true",
            "native-construction=default-or-validated-manifest",
            "native-null-options=structured-user-input-failure",
            "native-null-options-code=RESOLVE_OPTIONS_REQUIRED",
            "python-instance-shadowing=not-preserved",
        };
    }

    private static string[] ObservePackageTopologyAndValues()
    {
        Type type = typeof(DragonPackageInfo);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal("InvisibleDragon", DragonPackageInfo.Name);
        Assert.Equal("0.1.2", DragonPackageInfo.Version);
        Assert.Null(type.GetField("REQUIRED_PYTHON", BindingFlags.Public | BindingFlags.Static));
        Assert.Equal(
            new[] { "net48", "net7.0-windows", "net8.0-windows" },
            DeclaredTargetFrameworks());
        return new[]
        {
            "native-package-info-kind=static-class",
            "native-name=InvisibleDragon",
            "native-version=0.1.2",
            "native-required-python-member=absent",
            "native-target-frameworks=net48|net7.0-windows|net8.0-windows",
        };
    }

    private static string[] ObservePackageImmutability()
    {
        FieldInfo name = RequiredField(typeof(DragonPackageInfo), nameof(DragonPackageInfo.Name));
        FieldInfo version = RequiredField(typeof(DragonPackageInfo), nameof(DragonPackageInfo.Version));
        Assert.True(name.IsLiteral);
        Assert.True(version.IsLiteral);
        Assert.Null(typeof(DragonPackageInfo).GetField("REQUIRED_PYTHON", BindingFlags.Public | BindingFlags.Static));
        return new[]
        {
            "native-name-field=public-static-const-string",
            "native-version-field=public-static-const-string",
            "native-required-python=compiled-project-contract",
            "native-package-class-mutation=not-available",
            "python-delete-and-restore=not-preserved",
        };
    }

    private static string[] ObservePackageConstruction()
    {
        Type type = typeof(DragonPackageInfo);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        return new[]
        {
            "native-package-info-instantiation=unavailable",
            "native-public-instance-constructor-count=0",
            "native-nonpublic-instance-constructor-count=0",
            "native-instance-shadowing=unavailable",
            "python-empty-instance-dictionaries=not-preserved",
        };
    }

    private static string[] ObservePackageName()
    {
        string value = DragonPackageInfo.Name;
        Assert.Equal(15, value.Length);
        Assert.Equal("INVISIBLEDRAGON", value.ToUpperInvariant());
        Assert.DoesNotContain('-', value);
        return new[]
        {
            "native-name=InvisibleDragon",
            "native-name-length=15",
            "native-name-upper=INVISIBLEDRAGON",
            "native-name-contains-hyphen=false",
            "python-name=invisible-dragon",
            "python-string-item-mutation=not-preserved",
        };
    }

    private static string[] ObservePackageVersion()
    {
        string value = DragonPackageInfo.Version;
        Assert.True(Version.TryParse(value, out Version? parsed));
        Assert.NotNull(parsed);
        Assert.Equal(0, parsed!.Major);
        Assert.Equal(1, parsed.Minor);
        Assert.Equal(2, parsed.Build);
        return new[]
        {
            "native-version=0.1.2",
            "native-version-storage=const-string",
            "native-version-components=0|1|2",
            "python-version-components=0|7|0",
            "python-tuple-operations=not-preserved",
        };
    }

    private static string[] ObserveRequiredPythonAdaptation()
    {
        Assert.Null(typeof(DragonPackageInfo).GetField(
            "REQUIRED_PYTHON",
            BindingFlags.Public | BindingFlags.Static));
        string[] frameworks = DeclaredTargetFrameworks();
        TargetFrameworkAttribute? current = typeof(DragonPackageInfo).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>();
        Assert.NotNull(current);
        Assert.Equal(".NETCoreApp,Version=v8.0", current!.FrameworkName);
        return new[]
        {
            "native-required-python-member=absent",
            "native-compatibility-kind=compiled-target-frameworks",
            "native-target-frameworks=" + string.Join("|", frameworks),
            "native-focused-test-framework=.NETCoreApp,Version=v8.0",
            "python-runtime-requirement=not-a-native-product-dependency",
            "python-tuple-comparison-and-errors=not-preserved",
        };
    }

    private static object CreateReceipt(
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations) => new
    {
        assertion_id = target.AssertionId,
        adaptation_id = target.AdaptationId,
        classification = "exception",
        target_symbol = target.Symbol,
        native_target = target.NativeTarget,
        native_implementation = NativeImplementationFor(target.Symbol),
        source_receipt = SourceReceiptObject(target.SourceReceipt),
        artifacts = new
        {
            fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
            generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
            python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
            public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
            native_sources = NativeArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
        },
        case_coverage = target.CaseIndices.Select(index => Cases[index].CaseId).ToArray(),
        context_cases = target.ContextCaseIndices.Select(index => Cases[index].CaseId).ToArray(),
        observations = target.CaseIndices.Select(index => new
        {
            case_id = Cases[index].CaseId,
            python_facts_sha256 = Cases[index].FactsSha256,
            native_fact_count = observations[index].Facts.Length,
            native_facts_sha256 = observations[index].FactsSha256,
            native_facts = observations[index].Facts,
        }).ToArray(),
        scope = new
        {
            exact_target_count = 8,
            equivalent_target_count = 0,
            exception_target_count = 8,
            native_direct_analogue = HasDirectNativeAnalogue(target.Symbol),
            host_path_policy = "anchor-relative-or-leaf-only;no-host-absolute-paths-serialized",
            source_state_policy = "Python-class-mutation-and-instance-shadowing-are-explicit-exception-observations-only",
            live_energyplus_policy = "synthetic-four-file-runtime-with-test-owned-hashes;no-live-installation-or-load-claim",
            epsimple_constants_indices_not_retargeted = "31-39",
            special_tag_indices_out_of_scope = "576-579",
            private_context_members_not_promoted = new[]
            {
                "Directory._DATA_DIR", "Directory._MODULE_ROOT", "Directory._PACKAGE_ROOT",
            },
            special_tag_receipts_not_retargeted = ResolvedSpecialTagReceipts
                .Select(SourceReceiptObject)
                .ToArray(),
            unresolved_behavior = UnresolvedFor(target.Symbol),
        },
        upstream = new
        {
            ast_sha256 = UpstreamAstSha256,
            commit = UpstreamCommit,
            inventory_sha256 = InventoryContentSha256,
            source_bytes = UpstreamBytes,
            source_sha256 = UpstreamSourceSha256,
        },
    };

    private static string NativeImplementationFor(string symbol) => symbol switch
    {
        "Directory" => "Dragons.EnergyPlus.Runtime.RuntimeResolver",
        "Directory.ENERGYPLUS_DIR" => "Dragons.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.RootPath",
        "Directory.IDD_DIR" => "Dragons.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.IddPath",
        "Directory.PROFILE_DIR" => "Dragons.InvisibleDragon.Profile.Profile; no native profile-directory member",
        "PackageInfo" => "Dragons.InvisibleDragon.PackageInfo",
        "PackageInfo.NAME" => "Dragons.InvisibleDragon.PackageInfo.Name",
        "PackageInfo.REQUIRED_PYTHON" => "no native member; PackageInfo project TargetFrameworks metadata",
        "PackageInfo.VERSION" => "Dragons.InvisibleDragon.PackageInfo.Version",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null),
    };

    private static bool HasDirectNativeAnalogue(string symbol) => symbol is
        "Directory.ENERGYPLUS_DIR" or "Directory.IDD_DIR" or "PackageInfo" or
        "PackageInfo.NAME" or "PackageInfo.VERSION";

    private static string[] UnresolvedFor(string symbol) => symbol switch
    {
        "Directory" => new[]
        {
            "frozen-or-zip-imports-and-loaders-without-real-__file__",
            "symlink-junction-POSIX-UNC-alternate-drive-and-case-folding-path-semantics",
            "concurrent-class-mutation-custom-metaclasses-descriptors-and-arbitrary-replacements",
        },
        "Directory.ENERGYPLUS_DIR" or "Directory.IDD_DIR" => new[]
        {
            "unvalidated-import-time-Python-path-object-identity-or-mutability",
            "filesystem-permission-and-concurrent-replacement-races-outside-the-synthetic-runtime",
            "live-EnergyPlus-installation-availability-or-execution",
        },
        "Directory.PROFILE_DIR" => new[]
        {
            "bundled-Python-profile-directory-layout-and-file-loading",
            "filesystem-permission-and-concurrent-replacement-races",
            "arbitrary-Python-class-or-instance-mutation",
        },
        "PackageInfo" => new[]
        {
            "Python-instance-construction-shadowing-and-class-attribute-replacement",
            "custom-metaclasses-descriptors-and-concurrent-mutation",
        },
        "PackageInfo.NAME" => new[]
        {
            "Python-hyphenated-name-equality-or-exact-string-operation-results",
            "class-attribute-replacement-deletion-and-instance-shadowing",
        },
        "PackageInfo.REQUIRED_PYTHON" => new[]
        {
            "Python-runtime-gating-or-tuple-comparison-error-parity",
            "non-integer-negative-huge-mixed-or-replaced-tuple-members",
            "native-frameworks-other-than-the-three-pinned-project-targets",
        },
        "PackageInfo.VERSION" => new[]
        {
            "Python-0.7.0-tuple-equality-ordering-concatenation-or-item-error-parity",
            "class-attribute-replacement-deletion-and-instance-shadowing",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null),
    };

    private static async Task<RuntimePair> CreateRuntimePairAsync(string temporaryRoot)
    {
        EnergyPlusRuntimeLayout first = await CreateRuntimeAsync(
            Path.Combine(temporaryRoot, "location-a", "runtime"));
        EnergyPlusRuntimeLayout second = await CreateRuntimeAsync(
            Path.Combine(temporaryRoot, "location-b", "runtime"));
        return new RuntimePair(first, second);
    }

    private static async Task<EnergyPlusRuntimeLayout> CreateRuntimeAsync(string root)
    {
        Directory.CreateDirectory(root);
        string energyPlus = WriteRuntimeFile(root, "energyplus.exe", "constants-metadata-energyplus");
        string expandObjects = WriteRuntimeFile(root, "ExpandObjects.exe", "constants-metadata-expandobjects");
        string idd = WriteRuntimeFile(root, "Energy+.idd", "constants-metadata-idd");
        string schema = WriteRuntimeFile(root, "Energy+.schema.epJSON", "{\"constants-metadata\":true}");
        EnergyPlusRuntimeManifest manifest = EnergyPlusRuntimeManifest.Supported with
        {
            EnergyPlusExecutableSha256 = Sha256Hex(File.ReadAllBytes(energyPlus)),
            ExpandObjectsSha256 = Sha256Hex(File.ReadAllBytes(expandObjects)),
            EnergyPlusIddSha256 = Sha256Hex(File.ReadAllBytes(idd)),
            EnergyPlusEpJsonSchemaSha256 = Sha256Hex(File.ReadAllBytes(schema)),
        };
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver(manifest).ResolveAsync(
            new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = root,
                SearchEnvironmentVariables = false,
                SearchDefaultCacheLocation = false,
                SearchDefaultInstallLocation = false,
            });
        Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);
        return Assert.IsType<EnergyPlusRuntimeLayout>(resolution.Runtime);
    }

    private static string WriteRuntimeFile(string root, string name, string content)
    {
        string path = Path.Combine(root, name);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        return path;
    }

    private static string[] DeclaredTargetFrameworks()
    {
        string project = File.ReadAllText(FindRepositoryFile(CoreProjectPath), Encoding.UTF8);
        Match match = Regex.Match(
            project,
            "<TargetFrameworks>(?<value>[^<]+)</TargetFrameworks>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        return match.Groups["value"].Value.Split(';');
    }

    private static PropertyInfo RequiredProperty(Type type, string name) =>
        Assert.IsAssignableFrom<PropertyInfo>(type.GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));

    private static FieldInfo RequiredField(Type type, string name) =>
        Assert.IsAssignableFrom<FieldInfo>(type.GetField(
            name,
            BindingFlags.Public | BindingFlags.Static));

    private static void ValidateSourceReceipts(
        JsonElement value,
        IReadOnlyList<SourceReceipt> expected)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, actual.Length);
        for (int index = 0; index < expected.Count; index++)
        {
            SourceReceipt receipt = expected[index];
            JsonElement item = actual[index];
            AssertKeys(item,
                "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(receipt.InventoryIndex, item.GetProperty("inventory_index").GetInt32());
            Assert.Equal(receipt.Kind, RequiredString(item, "kind"));
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(receipt.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(receipt.SymbolHash, RequiredString(item, "symbol_hash"));
            Assert.Equal(receipt.SignatureHash, RequiredString(item, "signature_hash"));
            Assert.Equal(receipt.BodyHash, RequiredString(item, "body_hash"));
        }
    }

    private static object SourceReceiptObject(SourceReceipt receipt) => new
    {
        body_hash = receipt.BodyHash,
        inventory_index = receipt.InventoryIndex,
        kind = receipt.Kind,
        path = UpstreamPath,
        signature_hash = receipt.SignatureHash,
        symbol = receipt.Symbol,
        symbol_hash = receipt.SymbolHash,
    };

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
    };

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

    private static void AssertUniqueKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.True(names.Add(property.Name), $"Duplicate JSON key '{property.Name}'.");
                AssertUniqueKeysRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertUniqueKeysRecursive(item);
            }
        }
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        string[] actual = value.EnumerateObject().Select(item => item.Name)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal), actual);
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    private static string RequiredString(JsonElement value, string property)
    {
        string? result = value.GetProperty(property).GetString();
        Assert.False(string.IsNullOrEmpty(result));
        return result!;
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        foreach (string text in EnumerateStrings(value))
        {
            Assert.DoesNotMatch("^[A-Za-z]:[\\\\/]", text);
            Assert.False(text.StartsWith('/'), text);
            Assert.DoesNotContain("\\\\", text, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            yield return value.GetString()!;
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

    private static string Sha256(byte[] value) => "sha256:" + Sha256Hex(value);

    private static string Sha256Hex(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

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
        writer.Flush();
    }

    private sealed class TemporaryRoot : IDisposable
    {
        public TemporaryRoot()
        {
            string repositoryRoot = System.IO.Path.GetDirectoryName(
                FindRepositoryFile("Directory.Build.props"))!;
            Path = System.IO.Path.Combine(
                repositoryRoot,
                "temp",
                "tests",
                "constants-metadata",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);
    private sealed record NativePin(int FactCount, string FactsSha256);
    private sealed record NativeObservation(string CaseId, string[] Facts, string FactsSha256);
    private sealed record RuntimePair(EnergyPlusRuntimeLayout First, EnergyPlusRuntimeLayout Second);
    private sealed record CaseBinding(
        string Scenario,
        string CaseId,
        string Subfamily,
        string[] TargetSymbols,
        string[] ContextSymbols,
        string FactsSha256,
        string CaseSha256);
    private sealed record SourceReceipt(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash);
    private sealed record TargetBinding(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string AdaptationId,
        string NativeTarget,
        int[] CaseIndices,
        int[] ContextCaseIndices)
    {
        public SourceReceipt SourceReceipt => new(
            InventoryIndex,
            Symbol,
            Kind,
            SymbolHash,
            SignatureHash,
            BodyHash);
    }
}
