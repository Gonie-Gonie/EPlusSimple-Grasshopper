using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.SimpleDragon.Tests;

public sealed class IdentifierConventionsOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/epsimple-identifier-conventions-oracle.json";
    private const int FixtureBytes = 120_950;
    private const string FixtureSha256 =
        "sha256:0427210382b31555368dad0b6ca5f478d5f56b7b97949e6b850637fbda6ec6c6";
    private const string FixtureSchema =
        "dragons.python-reference.epsimple-identifier-conventions.v1";
    private const string CasesSha256 =
        "sha256:6244a03437d0d6f50bfeb135c99bfaf284804391998f168a675b30dc60ef3c10";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.IdentifierConventionsOracleParityTests.MatchesPinnedPythonIdentifierAndMetadataConventions";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/epsimple/constants.py";

    private const string GeneratorPath =
        "tools/python-reference/generate_epsimple_identifier_conventions_oracle.py";
    private const int GeneratorBytes = 65_469;
    private const string GeneratorSha256 =
        "sha256:ee0c7eb18b10d575845455b4a52658ffd93dbb82ee5a6afffd4d3e054892a89d";
    private const string ValidatorPath =
        "tests/PythonReference/test_epsimple_identifier_conventions_oracle.py";
    private const int ValidatorBytes = 21_869;
    private const string ValidatorSha256 =
        "sha256:ae437e0548c07f5ff52446569c52589d2d70152b2c2f7806a66a786ce3d6e2d5";
    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new(
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Constants/IdentifierConventions.cs",
            8_610,
            "sha256:33d0281782b82837646804bbdfaa3ffd083a08a48bad98abfb7db4352aa43a3c"),
        new(
            "src/SimpleDragon/Dragons.SimpleDragon.Core/PackageInfo.cs",
            391,
            "sha256:ef73d4b6f9c9bd8948d73c225bb88012ab1616bcf4f6fc89b8d84f46cb95efe0"),
        new(
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonEmbeddedData.cs",
            3_104,
            "sha256:ae2cb7c89e4dcef7195e528fc7831c5abdba560651a244281ffeaaa83c60fc9f"),
        new(
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Weather/WeatherDatabase.cs",
            9_454,
            "sha256:28f3885362fe08663ba6393bae545b70d17284d1751aa5a97cd0194e1b271b34"),
        new(
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Dragons.SimpleDragon.Core.csproj",
            3_165,
            "sha256:98eb7f31bc1f1f6f5caa49db6201cadb5e27a0e4c0272d2948c1843635d599bf"),
    };

    private static readonly AutoBinding[] AutoMembers =
    {
        new("MATERIAL", "MTRL", AutoIdPrefix.Material),
        new("SURFACE_CONSTRUCTION", "CTSF", AutoIdPrefix.SurfaceConstruction),
        new("FENESTRATION_CONSTRUCTION", "CTFN", AutoIdPrefix.FenestrationConstruction),
        new("SOURCE_SYSTEM", "SRCE", AutoIdPrefix.SourceSystem),
        new("SUPPLY_SYSTEM", "SUPL", AutoIdPrefix.SupplySystem),
        new("HEAT_EXCHANGER", "ERVT", AutoIdPrefix.HeatExchanger),
        new("PV_PANEL", "PVPN", AutoIdPrefix.PvPanel),
        new("SURFACE", "SURF", AutoIdPrefix.Surface),
        new("FENESTRATION", "FNST", AutoIdPrefix.Fenestration),
        new("ZONE", "ZONE", AutoIdPrefix.Zone),
        new("DAY_SCHEDULE", "DYSC", AutoIdPrefix.DaySchedule),
        new("RULESET", "RLST", AutoIdPrefix.Ruleset),
        new("SCHEDULE", "SCHE", AutoIdPrefix.Schedule),
        new("PROFILE", "PRFL", AutoIdPrefix.Profile),
    };

    private static readonly SpecialBinding[] SpecialMembers =
    {
        new("SPECIAL", "SPECIAL", SpecialTag.Special),
        new("DB", "FROM_DB", SpecialTag.Database),
        new("CLONE", "CLONE_OF", SpecialTag.Clone),
        new("FLIP", "REVERSED", SpecialTag.Flip),
        new("COOLROOF", "FOR_COOLROOF", SpecialTag.CoolRoof),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("A01", "epsimple-identifier-conventions.autoid-topology-order-values", "autoid", "sha256:b86d8a037a51a8bbcc78576f1f3a47224eec0c1de54f0695835e397ba1b77082", "sha256:f9f582260b35f90e65d14df7f968ed44d3969dea64071c5314f40bca80a286b9"),
        new("A02", "epsimple-identifier-conventions.autoid-string-value-semantics", "autoid", "sha256:abd961181c7f6abc5bd5d8e5e2b65fddd7f69a65e089c6732f2403ba134296da", "sha256:86ee3d64986853ea3388c3c5e11e7b9598a1e3c0fd309e4dee7d31410e8d25f1"),
        new("A03", "epsimple-identifier-conventions.autoid-construction-lookup-errors", "autoid", "sha256:91cdc2fb8293eca7c9d7b2d13e73cfa3b6b924a0c06049a1d2a0af77ac816c01", "sha256:d172e2d8520295227932ab161d479145e4c603d1a521a04a34d433701822db95"),
        new("A04", "epsimple-identifier-conventions.autoid-format-empty-custom", "autoid", "sha256:5d2f30c684ebbef6bd7d8f504214a03d81701d09af5a9c667a378bc9c4d345a9", "sha256:bfee63da4c39846ccf17d7fdeb0fb262f7ee3b9d4dc838bd6d94f926de357ca7"),
        new("A05", "epsimple-identifier-conventions.autoid-direct-format-type-context", "autoid", "sha256:195155edcd826e64a318ba975ed6161d64cfe02af65f8bb5ace2df07d61af007", "sha256:a52a1ad8f120251a61d24a56eff1b933f3fc0b16c2565805d75a3e0a2ab931e5"),
        new("A06", "epsimple-identifier-conventions.autoid-mutation-copy-alias-context", "autoid", "sha256:e4a73198d0fd5b15ceb948a25e2991dc3113e0c6f21641e0f670204122f1f62b", "sha256:5f7f0b11edadb9b86ecaf0a15eeaebca0d65915e91a03ec9bd5e7819b29b96d8"),
        new("D01", "epsimple-identifier-conventions.directory-import-topology-path-roles", "directory", "sha256:7cac34d818f671dfacfea500ac2cca72f508083cac28ffeafb3be75a9c635c75", "sha256:70c4bac4355394e0528f3eb2e7cc2d167f76ebc1a19106825ed8a4258ae7fd86"),
        new("D02", "epsimple-identifier-conventions.directory-two-location-relocation", "directory", "sha256:720d3c2f30f7b27ca8bc518cbb6464368beed73a571b49342f685f8ccdaf0815", "sha256:99a57fb24c50cbc2bbc2ad47bbe4ada4434eb782247386d4001438f2cdfd045a"),
        new("D03", "epsimple-identifier-conventions.directory-class-attribute-mutation", "directory", "sha256:5b0aa58b88482801cf9868da1b383bdab6b8b21f5b3898f73d5ee07e365dc368", "sha256:f87fa5a492ce71c964b1c2f499f34ae28ffee5c7e66fa305f0e9baf479f21694"),
        new("D04", "epsimple-identifier-conventions.directory-instance-shadow-construction-errors", "directory", "sha256:6cb6ab832b426a84859b69a8a9e144279decaa3d7247755ca093bc98c1753261", "sha256:1df5c68fc31d36fd8f5019f9a0435a44f73636dc69ad94fc0716228d026acb05"),
        new("P01", "epsimple-identifier-conventions.package-info-topology-values", "package", "sha256:f59eb4d5b80189cc791d946e1d662b7209a5274e9aff5be78444ac8f736b3fb4", "sha256:295492546456eefd4bd71aecddf241b66f2078ceb3f2e30a0282c3c53db118d6"),
        new("P02", "epsimple-identifier-conventions.package-info-class-attribute-mutation", "package", "sha256:316bdcc43df9ccb8ddddb6a4163c7bdda00926a6220a6a4356910bc799aec100", "sha256:9f47ac49cc441103847fc505f11787af0866e16dce0dab7ebde29a4ac4f1f4d3"),
        new("P03", "epsimple-identifier-conventions.package-info-instance-shadow-construction-errors", "package", "sha256:34e7d59eca31c2c1a8dc35fccf2a07970ec46c6889ef5eb14f158c10d1dbe380", "sha256:6c14827958990d69e12b316473e28340ef7d42580d321cfdec475cf5f2419d55"),
        new("P04", "epsimple-identifier-conventions.package-name-string-operations-errors", "package", "sha256:a6d812adb05f7d8c1d9c7618f3a893f401d8599cb4dcc7205050f409045bfc84", "sha256:43a3ca062e10f6e2e070044b8f7e269138bc01432d1037d6e0336f460d2275ff"),
        new("P05", "epsimple-identifier-conventions.package-version-tuple-operations-errors", "package", "sha256:e5fcb3126fe49753262efdb8ccba508cfa1888bddeb7a06d7560fcbb97a7962c", "sha256:b957013dc67207cfc1e7f711fa9721facd61e1db89580c9b2d09d710536065a5"),
        new("P06", "epsimple-identifier-conventions.required-python-comparison-errors", "package", "sha256:c202a35f61b40c279248de8c640957455b1f6850c4b697bbf3957a2dbdef56b6", "sha256:1e946a62576ed7ef371090823414ce1398d83c505fe956aa707b61cb03934a52"),
        new("S01", "epsimple-identifier-conventions.special-tag-topology-order-values", "special-tag", "sha256:26b4ec7fcb9add9a377ec5e9a71b09e145ba63ec7b3e1fb17238f4b4e47ec69a", "sha256:046e5d02046697287f196af925a8ee8c330d19fd894e0481a7c84924fb7fcaae"),
        new("S02", "epsimple-identifier-conventions.special-tag-string-value-semantics", "special-tag", "sha256:4685cdfdcd892090ecab457b08f5c5f90476b738df722ccea650df3dd51de594", "sha256:90b472cb993e8237e5854101d697c50b85187c7deb8af22af561f652b8e7c42c"),
        new("S03", "epsimple-identifier-conventions.special-tag-construction-lookup-errors", "special-tag", "sha256:1bd97477876e5cfc9098df97e5073c2a99ad967538012458c2805e49fdf2fdc2", "sha256:433cf040ce5c958055c731dbab8751e8f8046638ef62512c61041f91ed0964f1"),
        new("S04", "epsimple-identifier-conventions.special-tag-format-empty-custom", "special-tag", "sha256:e9474278e2a07499f9d77430c4d9e7b03349a50e1d060a9183f3ca7a1522acd1", "sha256:af690eade3cf4510005b0ebc3374dcaac2b372bf7eba0318ae61731a4beb86bc"),
        new("S05", "epsimple-identifier-conventions.special-tag-direct-format-type-context", "special-tag", "sha256:0e8726490fd3628580c631915ed55145f54338f914780728be0e265fab9866f1", "sha256:baf99a9e112a1756b6d23933facd36551db0f0fcd8addb604faef8a1cfc6ebf7"),
        new("S06", "epsimple-identifier-conventions.special-tag-mutation-copy-alias-context", "special-tag", "sha256:9be4d17fde6cf7e53ab6e05c643b87539dd7c5ef9323acfba2651879fe416fb1", "sha256:802e342589ebee34b8bdaf739cfcf7abd0bf2cbbc7cf377546d66c0e8976f22d"),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        new(10, "AUTOID_PREFIX", "sha256:9a7c270abf554af2ac0d3455101382eca02debe8c0b23e6f8c3f8a465bb32355", "sha256:22696419df2acdd2f9657b84b3107ba0fb3b72d1de6cd9f1778286a0456a524c"),
        new(11, "AUTOID_PREFIX.DAY_SCHEDULE", "sha256:7d4821ca360166e6a06218c647b7ea935dd62080d896fd2f45cdff14da52eea0", "sha256:e48c9adec39a3fa24f0b8613d1675c25a2064237664a8105f642e656faa6dce9"),
        new(12, "AUTOID_PREFIX.FENESTRATION", "sha256:d327acd7e82d257668484c17fe1ad79cca5a086b7977682c6e0a07af27987603", "sha256:c1a225945c08ead821d72ba45d489914d8a80d84f98bcfe5be7da0ad55aed742"),
        new(13, "AUTOID_PREFIX.FENESTRATION_CONSTRUCTION", "sha256:a00d7b14c20b1fbeaedf4e6b456bff8555bcb9ee539f74799d9e7e42a40fcc80", "sha256:da6bcc3e44802b13e1fde57e30d719eb05f13ce4230d0c4fe5d31dae9bb57884"),
        new(14, "AUTOID_PREFIX.HEAT_EXCHANGER", "sha256:d76b9ddc6df8f01d27ebf334bb8797bf798947ed40835eea8f0cf5fc84d94ccd", "sha256:988f5d0e56e157fbf86698520ae5f1316fa1b7f43278f0ea89550d47123da874"),
        new(15, "AUTOID_PREFIX.MATERIAL", "sha256:9b7489e4c9b530dab76d9d2dd9cc834d6f751d1d35188c70976af5ecea048275", "sha256:25e1db34e06e98f822489a2fd81ff0116e13fc5cb4c599bfc3e848340f6a4bdd"),
        new(16, "AUTOID_PREFIX.PROFILE", "sha256:f04014577c229312753d0289ef8342419fb7fed9452799dc2bce8e6d5438c32e", "sha256:579ddc67359dedbabe5938500359fc7e32e8ffaa61fa335d2636548c6b6f7e35"),
        new(17, "AUTOID_PREFIX.PV_PANEL", "sha256:46500b8a4aa511167e9fcfb13e33c74b901d1ba1a274ec71390708960fee493a", "sha256:85555a35d41989361b4a446c3fae28b3950be8f3d58639032a0a507da39f1de7"),
        new(18, "AUTOID_PREFIX.RULESET", "sha256:e5ac2688f0382545b277dae27cc3a02744e6d5f7f3de1c1111ce2b487751bc15", "sha256:eee17dde11d175df92e7be0226a04d551351cf91652de6ae0fea319303ffc68b"),
        new(19, "AUTOID_PREFIX.SCHEDULE", "sha256:c61dbb424961f11afedba70eabadd6c54ccdb52a7f7be1d56299ede42c0468c6", "sha256:43f2ebe3ecc509b36aef4f5b8f7b5066e74f6f33601ffb58439c1ec7344e0ed7"),
        new(20, "AUTOID_PREFIX.SOURCE_SYSTEM", "sha256:60d016219cabe29d48669ee37e1d223932bfc6556bcc8f0a4a5ea0af147655c3", "sha256:09e744fab78384c339d40df89257da74aa0520daeacfeefffa408aa346cd0e62"),
        new(21, "AUTOID_PREFIX.SUPPLY_SYSTEM", "sha256:c2e6d435b1a6650d0998650fdb23d4310cd07ce02806553a645755990ca3bcd4", "sha256:4a60dd64ad48ead389732bc329ece5dcac495c27a96eaba1cff1dae4064cb921"),
        new(22, "AUTOID_PREFIX.SURFACE", "sha256:7fca2d17fabcb91b32dd28349a50f44fe1ed0c0be63cf6dba21d37ffbf229472", "sha256:7b4e6369ce085157ba4c2b5921a2435357723de2ba720889c0f952090d8fec6a"),
        new(23, "AUTOID_PREFIX.SURFACE_CONSTRUCTION", "sha256:147095a3d8aeedcd5fb82264a34c353733325268d6ad25b8e85222340fff3ca5", "sha256:1e72a76dd67146ebe41b02f98959c85b09593e3063d2d8213fb756a118f951cf"),
        new(24, "AUTOID_PREFIX.ZONE", "sha256:5f36f9019cc2b5ad1e96b3338a84b04d4da0360941a4499db6846ddafa926ccf", "sha256:2720c09e009d3bdf9a85e0954fb5f8398bc8b1a0ae1aa12af6eb9e84d0b2cb3f"),
        new(25, "AUTOID_PREFIX.__format__", "sha256:d0c85092c98182b0366673cd287507b75d62850d9e272b32896597e787a58170", "sha256:a816cddeda045574ece80e4d280477f6f6d5ba719bd250848d41e9cc24df964d"),
        new(27, "AUTOID_PREFIX.__str__", "sha256:13ed292afebbf1a59717e776df9d6ba3e220d2cc248ac2cc450deab9c2261c98", "sha256:63a868daac14f7900d160c3b9b93a4e25e017e473ed17ba9fa6484c9ee8c89db"),
        new(31, "Directory", "sha256:5b876ad7fd9b11f66cc01ecb6c43d4e143b6f0258ba070c02551d968dd68aaf6", "sha256:723fb76aee65e3c0a549836c678b87ad1adcec3c3997762adac6332cd37028fc"),
        new(32, "Directory.CONSTRUCTION_DIR", "sha256:91c573a02d0e3b2d93a1271fbe1c3ddb5d4d10c04083707c799fa1503f5b3dea", "sha256:2224fc383d4d95587bada9e2a53e8d225ee4b04b14d6827239d8f4769df12f77"),
        new(33, "Directory.PROFILE_DIR", "sha256:f65d5eaefa2bc1cbb6f0c9b5904624194a1551f48e7966c7973d35526bad4fa6", "sha256:c032da9509cd77fe6ccd80716e6fd2f2cd99106314c9c69dd5942f5f7a4888f0"),
        new(34, "Directory.WEATHER_DATA_DIR", "sha256:8a5bf6543c4f0db98ee0169deb7dfddd4c126e34d52aa67b512136dc3e8bcd01", "sha256:60035d4ba11ad55d8db9be9ec210b13b0006aebff5d52925e4cc2bb7c72e9f71"),
        new(35, "Directory.WEATHER_META_DIR", "sha256:15e81d1d4205ffe651af323c3cc7352255847972301ad1753fd3d8d5098dc260", "sha256:74d71a4020250fed6f7b673755272d8daf3718a2bfd0e41df7a9e08760e3ba2c"),
        new(36, "PackageInfo", "sha256:aaf5b98d4a7dc29f83b698f1fb2881b7bb258885bd2aaf17a53b6da902d1eda1", "sha256:2b6c246086882d342beb6bb57d37a75fc2ae22c9bf79d8f9be87fdc9cb4733be"),
        new(37, "PackageInfo.NAME", "sha256:537c8c3bc3c2d48105e8e6c453208e725f985ac9d84f87e5f66c094ea5696cad", "sha256:3ad9dca79d585649de8f049226cad044f8a0059655de5e659794d405f5860137"),
        new(38, "PackageInfo.REQUIRED_PYTHON", "sha256:cf74d0eb707a3668aa515bdd31d767109337841bcf28f03b96c6e9264d9407a4", "sha256:472a512d8c6a348b78562a2ba90743ca1cd94d3b4d1d40eabdf9c65ddf7505a0"),
        new(39, "PackageInfo.VERSION", "sha256:a8260e5f38f8422e1ac38ce24fd0136b4bb3a4de24f268e9a262aa6034031ea4", "sha256:691a7ac0839d15f54120e4247d22426a1e9a73be013bdcc5d06caed6d3723d5a"),
        new(58, "SpecialTag", "sha256:a66e2175ee03b1d6d73c70998500b45ae7eac6989b60ddd2adb09882a17f2c9b", "sha256:2b09900dfa47263d6e1581ba5292bc3e3836983ee1f110fb9a0d25d970738e13"),
        new(59, "SpecialTag.CLONE", "sha256:00989ee6011feaa240308f2a1e1bb8c47def1f4be493b51b91e75c75ee7bf39f", "sha256:68caa0f055733d4fb0355492e9141f69cf3d496b22b23f65c8bc31360c0d7fe4"),
        new(60, "SpecialTag.COOLROOF", "sha256:622c00d22fff7838ef72f37deeac6461b137ff084511ea1085717955cc893f4b", "sha256:e017c5ab929de08f39938b2a8b49f3614baeeb307cdc3aac77d3b341e8acbbcf"),
        new(61, "SpecialTag.DB", "sha256:a43168ea5003995edfe35fec6f3f6b25ad26eb9111e97337db7dece5e0ede870", "sha256:16b8ccdd55935eb917e956a6d3420b2cb68f8d9aef9de50cfe9df1d349145e9e"),
        new(62, "SpecialTag.FLIP", "sha256:4a5884386e242adce385eb0991559fb93426172ac5e152276000036947d4683f", "sha256:3a57d524c6ab76a5960245ea23c1fb40871f633297a0e8ae2284c13cfe2c9066"),
        new(63, "SpecialTag.SPECIAL", "sha256:0faf9b24524c68d912d1b0d1438b85bf856778fbbdc7a11ef7c20137c8d08be6", "sha256:c7b1ce68b80c60a900cf68b1967195db31ea399bdfc85c88c445cec00d7ff92c"),
        new(64, "SpecialTag.__format__", "sha256:4ef932bb8135c4cfaf7e17e805cfb299e50d9400f4a106605bdb2fb75477d3a0", "sha256:7fab4ad475da909182182f598471198ad3e3850b8c2448086b09192a8a806c8f"),
        new(66, "SpecialTag.__str__", "sha256:13ed292afebbf1a59717e776df9d6ba3e220d2cc248ac2cc450deab9c2261c98", "sha256:80ca5168bcdac3483d97de5f3041641f0ba952bc250855f62aabb7ef329a1541"),
    };

    private static readonly Dictionary<string, string> ExceptionAdaptations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AUTOID_PREFIX"] = "immutable-native-auto-id-prefix-catalog-9a7c270a",
            ["Directory"] = "embedded-explicit-native-resource-layout-5b876ad7",
            ["Directory.CONSTRUCTION_DIR"] = "embedded-native-construction-resources-91c573a0",
            ["Directory.PROFILE_DIR"] = "embedded-native-profile-resources-f65d5eae",
            ["Directory.WEATHER_DATA_DIR"] = "caller-supplied-native-weather-data-root-8a5bf654",
            ["Directory.WEATHER_META_DIR"] = "embedded-native-weather-metadata-resources-15e81d1d",
            ["PackageInfo"] = "static-native-simpledragon-package-information-aaf5b98d",
            ["PackageInfo.NAME"] = "native-simpledragon-package-name-537c8c3b",
            ["PackageInfo.REQUIRED_PYTHON"] = "compiled-simpledragon-target-framework-contract-cf74d0eb",
            ["PackageInfo.VERSION"] = "native-simpledragon-and-upstream-version-identity-a8260e5f",
            ["SpecialTag"] = "immutable-native-special-tag-catalog-a66e2175",
        };

    [Fact]
    public void MatchesPinnedPythonIdentifierAndMetadataConventions()
    {
        ValidatePinnedArtifacts();
        using JsonDocument oracle = ReadPinnedOracle();
        OracleCorpus corpus = ValidateCorpus(oracle.RootElement);

        NativeObservation[] observations = corpus.Cases
            .Select((item, index) => ObserveNativeCase(ExpectedCases[index], item))
            .ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), observations.Select(item => item.CaseId));
        Assert.All(observations, observation =>
        {
            Assert.NotEmpty(observation.Facts);
            Assert.Equal(observation.Facts.Length, observation.Facts.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(observation.FactsSha256, CanonicalSha256(JsonSerializer.SerializeToElement(observation.Facts)));
        });

        foreach (TargetContract target in corpus.Targets)
        {
            NativeObservation[] targetObservations = observations
                .Where((_, index) => CaseTargets(corpus.Cases[index], target.Symbol))
                .ToArray();
            Assert.NotEmpty(targetObservations);

            var receipt = new
            {
                classification = target.Classification,
                fixture = new
                {
                    case_count = ExpectedCases.Length,
                    cases_sha256 = CasesSha256,
                    generator = ArtifactProjection(GeneratorPath, GeneratorBytes, GeneratorSha256),
                    path = FixturePath,
                    sha256 = FixtureSha256,
                    validator = ArtifactProjection(ValidatorPath, ValidatorBytes, ValidatorSha256),
                },
                native_binding = new
                {
                    adaptation_id = target.AdaptationId,
                    implementation_artifacts = NativeArtifacts
                        .Select(item => ArtifactProjection(item.Path, item.Bytes, item.Sha256))
                        .ToArray(),
                    implementation_symbol = target.NativeRoute,
                },
                observations = targetObservations.Select(item => new
                {
                    adaptation_id = target.AdaptationId,
                    case_id = item.CaseId,
                    native_facts = item.Facts,
                    native_facts_sha256 = item.FactsSha256,
                    native_outcome = target.Classification == "equivalent"
                        ? "equivalent-as-pinned"
                        : "adapted-as-pinned",
                    python_facts_sha256 = item.PythonFactsSha256,
                }).ToArray(),
                upstream = new
                {
                    body_hash = target.BodyHash,
                    inventory_index = target.InventoryIndex,
                    kind = target.Kind,
                    path = UpstreamPath,
                    signature_hash = target.SignatureHash,
                    symbol = target.Symbol,
                    symbol_hash = target.SymbolHash,
                },
            };
            JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
            ValidateReceipt(receiptJson, target, targetObservations);
            TrustedEvidenceRecorder.Record(
                target.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }
    }

    private static void ValidatePinnedArtifacts()
    {
        ValidateArtifact(FixturePath, FixtureBytes, FixtureSha256);
        ValidateArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        ValidateArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        ValidateArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            ValidateArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }
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

    private static OracleCorpus ValidateCorpus(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoRawAddresses(root.GetRawText());
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(
            root,
            "artifacts",
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
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

        ValidateFixtureArtifacts(root.GetProperty("artifacts"));
        ValidateRuntime(root.GetProperty("runtime"));
        ValidateUpstream(root.GetProperty("upstream"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCases.Length, cases.Length);
        AssertKeys(root.GetProperty("fact_sha256"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertKeys(root.GetProperty("case_sha256"), ExpectedCases.Select(item => item.CaseId).ToArray());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(
                cases[index],
                ExpectedCases[index],
                root.GetProperty("fact_sha256"),
                root.GetProperty("case_sha256"));
        }

        TargetContract[] targets = ValidateTargets(
            root.GetProperty("target_receipts"),
            root.GetProperty("symbols"),
            root.GetProperty("consumer_contract"));
        ValidateExcludedReceipts(root.GetProperty("excluded_receipts"));

        string[] observedTargets = cases
            .SelectMany(item => ReadStringArray(item.GetProperty("target_symbols")))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ExpectedTargets.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            observedTargets);
        return new OracleCorpus(cases, targets);
    }

    private static void ValidateFixtureArtifacts(JsonElement artifacts)
    {
        AssertKeys(artifacts, "bootstrap", "strict_json_support");
        ValidateArtifactProjection(
            artifacts.GetProperty("bootstrap"),
            "tools/python-reference/bootstrap_reference.py",
            1_232,
            "sha256:0674dcf1fe966de2a4b873a360ef67be48d74f38ba80adba9c74405fd9be7e0f");
        ValidateArtifactProjection(
            artifacts.GetProperty("strict_json_support"),
            "tools/python-reference/generate_schedule_type_oracle.py",
            21_108,
            "sha256:555a1df41e5369dbbc44b0729a48673610a86951a215c8e2aa00cfa4fce156f1");
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "byteorder",
            "implementation",
            "platform",
            "pointer_width_bits",
            "python_dont_write_bytecode",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("little", RequiredString(runtime, "byteorder"));
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("win32", RequiredString(runtime, "platform"));
        Assert.Equal(64, runtime.GetProperty("pointer_width_bits").GetInt32());
        Assert.True(runtime.GetProperty("python_dont_write_bytecode").GetBoolean());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
    }

    private static void ValidateUpstream(JsonElement upstream)
    {
        AssertKeys(upstream, "commit", "inventory", "isolated_import", "source");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));

        JsonElement inventory = upstream.GetProperty("inventory");
        AssertKeys(inventory, "bytes", "content_sha256", "file_sha256");
        Assert.Equal(InventoryBytes, inventory.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryContentSha256, RequiredString(inventory, "content_sha256"));
        Assert.Equal(InventoryFileSha256, RequiredString(inventory, "file_sha256"));

        JsonElement source = upstream.GetProperty("source");
        AssertKeys(source, "ast_sha256", "bytes", "path", "source_sha256");
        Assert.Equal(4_873, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal("sha256:d5dd5241ec90b14ba3708a525cd74279a8cdc238164a5b8544c4c82b05a29897", RequiredString(source, "source_sha256"));
        Assert.Equal("sha256:6740f081f087834aadfef0c11da6cdbe11f907dc170b48ebaa287e000eb6e27b", RequiredString(source, "ast_sha256"));

        JsonElement isolated = upstream.GetProperty("isolated_import");
        AssertKeys(isolated, "files_after_execution", "module_names", "source_copy_sha256");
        AssertStringArray(
            isolated.GetProperty("module_names"),
            "_dragons_epsimple_identifier_location_a",
            "_dragons_epsimple_identifier_location_b");
        Assert.Equal(2, isolated.GetProperty("files_after_execution").GetArrayLength());
        Assert.Equal(2, isolated.GetProperty("source_copy_sha256").EnumerateObject().Count());
        Assert.All(
            isolated.GetProperty("source_copy_sha256").EnumerateObject(),
            item => Assert.Equal(
                "sha256:d5dd5241ec90b14ba3708a525cd74279a8cdc238164a5b8544c4c82b05a29897",
                item.Value.GetString()));
    }

    private static void ValidateCase(
        JsonElement value,
        CaseBinding binding,
        JsonElement factMap,
        JsonElement caseMap)
    {
        AssertKeys(value, "code", "context_symbols", "id", "python", "subfamily", "target_symbols");
        Assert.Equal(binding.Code, RequiredString(value, "code"));
        Assert.Equal(binding.CaseId, RequiredString(value, "id"));
        Assert.Equal(binding.Subfamily, RequiredString(value, "subfamily"));
        Assert.Equal(binding.FactsSha256, RequiredString(factMap, binding.CaseId));
        Assert.Equal(binding.CaseSha256, RequiredString(caseMap, binding.CaseId));
        Assert.Equal(binding.CaseSha256, CanonicalSha256(value));

        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "facts_sha256", "outcome");
        Assert.Equal("observed", RequiredString(python, "outcome"));
        Assert.Equal(JsonValueKind.Object, python.GetProperty("facts").ValueKind);
        Assert.Equal(binding.FactsSha256, RequiredString(python, "facts_sha256"));
        Assert.Equal(binding.FactsSha256, CanonicalSha256(python.GetProperty("facts")));

        string[] targets = ReadStringArray(value.GetProperty("target_symbols"));
        string[] context = ReadStringArray(value.GetProperty("context_symbols"));
        Assert.NotEmpty(targets);
        Assert.Equal(targets.Length, targets.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(context.Length, context.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(targets.Intersect(context, StringComparer.Ordinal));
        Assert.All(targets.Concat(context), symbol =>
            Assert.Contains(ExpectedTargets, item => item.Symbol == symbol));
    }

    private static TargetContract[] ValidateTargets(
        JsonElement receiptsValue,
        JsonElement symbolsValue,
        JsonElement consumer)
    {
        JsonElement[] receipts = receiptsValue.EnumerateArray().ToArray();
        JsonElement[] symbols = symbolsValue.EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, receipts.Length);
        Assert.Equal(ExpectedTargets.Length, symbols.Length);

        AssertKeys(
            consumer,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classification_counts",
            "classifications",
            "closure",
            "native_routes",
            "target_symbols");
        Assert.Equal(ExpectedCases.Length, consumer.GetProperty("case_count").GetInt32());
        AssertStringArray(consumer.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(consumer.GetProperty("target_symbols"), ExpectedTargets.Select(item => item.Symbol).ToArray());

        JsonElement counts = consumer.GetProperty("classification_counts");
        AssertKeys(counts, "equivalent", "exception");
        Assert.Equal(23, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(11, counts.GetProperty("exception").GetInt32());

        JsonElement closure = consumer.GetProperty("closure");
        AssertKeys(closure, "excluded_repr_indices", "excluded_repr_symbols", "target_count", "target_indices");
        Assert.Equal(ExpectedTargets.Length, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(ExpectedTargets.Select(item => item.InventoryIndex), closure.GetProperty("target_indices").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(new[] { 26, 65 }, closure.GetProperty("excluded_repr_indices").EnumerateArray().Select(item => item.GetInt32()));
        AssertStringArray(closure.GetProperty("excluded_repr_symbols"), "AUTOID_PREFIX.__repr__", "SpecialTag.__repr__");

        JsonElement classifications = consumer.GetProperty("classifications");
        JsonElement adaptations = consumer.GetProperty("adaptations");
        JsonElement assertionIds = consumer.GetProperty("assertion_ids");
        JsonElement nativeRoutes = consumer.GetProperty("native_routes");
        AssertKeys(classifications, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(adaptations, ExceptionAdaptations.Keys.ToArray());
        AssertKeys(assertionIds, ExpectedTargets.Select(item => item.Symbol).ToArray());
        AssertKeys(nativeRoutes, ExpectedTargets.Select(item => item.Symbol).ToArray());

        var targets = new TargetContract[ExpectedTargets.Length];
        for (int index = 0; index < ExpectedTargets.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement receipt = receipts[index];
            JsonElement symbol = symbols[index];
            AssertKeys(receipt, "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            AssertKeys(symbol, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(expected.InventoryIndex, receipt.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, RequiredString(receipt, "symbol"));
            Assert.Equal(expected.SymbolHash, RequiredString(receipt, "symbol_hash"));
            Assert.Equal(UpstreamPath, RequiredString(receipt, "path"));
            foreach (string key in new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" })
            {
                Assert.Equal(receipt.GetProperty(key).GetRawText(), symbol.GetProperty(key).GetRawText());
            }

            string classification = ExceptionAdaptations.ContainsKey(expected.Symbol)
                ? "exception"
                : "equivalent";
            string? adaptationId = ExceptionAdaptations.TryGetValue(expected.Symbol, out string? adaptation)
                ? adaptation
                : null;
            Assert.Equal(classification, RequiredString(classifications, expected.Symbol));
            if (adaptationId is not null)
            {
                Assert.Equal(adaptationId, RequiredString(adaptations, expected.Symbol));
            }

            string assertionId = "epsimple-identifier-conventions-"
                + expected.InventoryIndex.ToString(CultureInfo.InvariantCulture)
                + "-"
                + expected.SymbolHash.AsSpan("sha256:".Length, 8).ToString();
            Assert.Equal(assertionId, RequiredString(assertionIds, expected.Symbol));
            string nativeRoute = NativeRoute(expected.Symbol);
            Assert.Equal(nativeRoute, RequiredString(nativeRoutes, expected.Symbol));

            targets[index] = new TargetContract(
                expected.InventoryIndex,
                expected.Symbol,
                RequiredString(receipt, "kind"),
                expected.SymbolHash,
                RequiredString(receipt, "signature_hash"),
                RequiredString(receipt, "body_hash"),
                classification,
                adaptationId,
                assertionId,
                nativeRoute,
                expected.ReceiptSha256);
        }

        Assert.Equal(23, targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(11, targets.Count(item => item.Classification == "exception"));
        return targets;
    }

    private static void ValidateExcludedReceipts(JsonElement value)
    {
        JsonElement[] excluded = value.EnumerateArray().ToArray();
        Assert.Equal(2, excluded.Length);
        ValidateExcludedReceipt(excluded[0], 26, "AUTOID_PREFIX.__repr__");
        ValidateExcludedReceipt(excluded[1], 65, "SpecialTag.__repr__");
    }

    private static void ValidateExcludedReceipt(JsonElement receipt, int index, string symbol)
    {
        AssertKeys(receipt, "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash");
        Assert.Equal(index, receipt.GetProperty("inventory_index").GetInt32());
        Assert.Equal("function", RequiredString(receipt, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(receipt, "path"));
        Assert.Equal(symbol, RequiredString(receipt, "symbol"));
        Assert.Equal("sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e", RequiredString(receipt, "symbol_hash"));
    }

    private static NativeObservation ObserveNativeCase(CaseBinding binding, JsonElement sourceCase)
    {
        string[] facts = binding.Code switch
        {
            "A01" => ObserveAutoTopology(sourceCase),
            "A02" => ObserveAutoStringSemantics(sourceCase),
            "A03" => ObserveAutoLookup(sourceCase),
            "A04" => ObserveAutoFormatting(sourceCase),
            "A05" => ObserveAutoTypedFormatting(sourceCase),
            "A06" => ObserveAutoImmutability(sourceCase),
            "D01" => ObserveDirectoryRoles(sourceCase),
            "D02" => ObserveDirectoryRelocation(sourceCase),
            "D03" => ObserveDirectoryImmutability(sourceCase),
            "D04" => ObserveDirectoryConstructionAdaptation(sourceCase),
            "P01" => ObservePackageTopology(sourceCase),
            "P02" => ObservePackageImmutability(sourceCase),
            "P03" => ObservePackageConstructionAdaptation(sourceCase),
            "P04" => ObservePackageName(sourceCase),
            "P05" => ObservePackageVersion(sourceCase),
            "P06" => ObserveRequiredRuntime(sourceCase),
            "S01" => ObserveSpecialTopology(sourceCase),
            "S02" => ObserveSpecialStringSemantics(sourceCase),
            "S03" => ObserveSpecialLookup(sourceCase),
            "S04" => ObserveSpecialFormatting(sourceCase),
            "S05" => ObserveSpecialTypedFormatting(sourceCase),
            "S06" => ObserveSpecialImmutability(sourceCase),
            _ => throw new ArgumentOutOfRangeException(nameof(binding), binding.Code, null),
        };
        return new NativeObservation(
            binding.CaseId,
            binding.FactsSha256,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveAutoTopology(JsonElement sourceCase)
    {
        JsonElement facts = PythonFacts(sourceCase);
        Assert.Equal(14, facts.GetProperty("member_count").GetInt32());
        Assert.Empty(facts.GetProperty("alias_groups").EnumerateArray());
        AssertConventionMembers(facts.GetProperty("declared_members"), AutoMembers.Select(item => (item.Name, item.Value)).ToArray());
        Assert.Equal(AutoMembers.Select(item => item.Native), AutoIdPrefix.Values);
        return new[]
        {
            "native-type=Dragons.SimpleDragon.AutoIdPrefix",
            "native-shape=sealed-immutable-singleton-catalog",
            "native-member-names=" + string.Join(",", AutoMembers.Select(item => item.Name)),
            "native-member-values=" + string.Join(",", AutoMembers.Select(item => item.Value)),
            "native-member-count=14;native-alias-count=0",
        };
    }

    private static string[] ObserveAutoStringSemantics(JsonElement sourceCase)
    {
        Assert.Equal(AutoMembers.Length, PythonFacts(sourceCase).GetProperty("members").GetArrayLength());
        foreach (AutoBinding item in AutoMembers)
        {
            Assert.Same(item.Native, AutoIdPrefix.FromValue(item.Value));
            Assert.Equal("prefix/" + item.Value, "prefix/" + item.Native.Value);
            Assert.Equal(item.Value + "/suffix", item.Native.Value + "/suffix");
            Assert.Contains(item.Value, item.Native.Value, StringComparison.Ordinal);
            Assert.Equal(item.Value, string.Join(string.Empty, item.Native.Value.Split('/')));
            Assert.Equal(StringComparer.Ordinal.GetHashCode(item.Value), item.Native.GetHashCode());
        }
        return new[]
        {
            "native-string-semantics=through-ordinal-Value",
            "native-value-roundtrip-count=14",
            "native-concatenation-and-split=token-preserving",
            "native-hash=ordinal-token-hash",
        };
    }

    private static string[] ObserveAutoLookup(JsonElement sourceCase)
    {
        AssertKeys(PythonFacts(sourceCase), "from_name", "from_name_as_value", "from_value", "invalid");
        foreach (AutoBinding item in AutoMembers)
        {
            Assert.True(AutoIdPrefix.TryFromValue(item.Value, out AutoIdPrefix? parsed));
            Assert.Same(item.Native, parsed);
            if (item.Name == item.Value)
            {
                Assert.Same(item.Native, AutoIdPrefix.FromValue(item.Name));
            }
            else
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => AutoIdPrefix.FromValue(item.Name));
            }
        }
        Assert.Throws<ArgumentNullException>(() => AutoIdPrefix.FromValue(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoIdPrefix.FromValue("__MISSING__"));
        Assert.False(AutoIdPrefix.TryFromValue(null, out _));
        return new[]
        {
            "native-exact-value-lookup-count=14",
            "native-name-as-value-only-when-token-equals-name=ZONE",
            "native-null-error=ArgumentNullException",
            "native-invalid-token-error=ArgumentOutOfRangeException",
        };
    }

    private static string[] ObserveAutoFormatting(JsonElement sourceCase)
    {
        Assert.Equal(AutoMembers.Length, PythonFacts(sourceCase).GetProperty("members").GetArrayLength());
        string[] specs = { string.Empty, "SURFACE", ":", " ", "표면" };
        foreach (AutoBinding item in AutoMembers)
        {
            Assert.Equal(item.Value + "-", item.Native.ToString());
            foreach (string spec in specs)
            {
                string expected = spec.Length == 0
                    ? item.Value + "-"
                    : item.Value + ":" + spec + "-";
                Assert.Equal(expected, item.Native.ToString(spec, CultureInfo.InvariantCulture));
            }
        }
        return new[]
        {
            "native-format-route=AutoIdPrefix.ToString(string?,IFormatProvider?)",
            "native-empty-format=VALUE-",
            "native-custom-format=VALUE:FORMAT-",
            "native-format-member-count=14",
        };
    }

    private static string[] ObserveAutoTypedFormatting(JsonElement sourceCase)
    {
        AssertKeys(
            PythonFacts(sourceCase),
            "direct_format_int",
            "direct_format_none",
            "direct_format_object",
            "direct_str_extra_argument",
            "format_builtin_none",
            "member_name");
        MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(typeof(AutoIdPrefix).GetMethod(
            nameof(AutoIdPrefix.ToString),
            new[] { typeof(string), typeof(IFormatProvider) }));
        Assert.Equal(typeof(string), method.GetParameters()[0].ParameterType);
        Assert.Equal("MTRL-", AutoIdPrefix.Material.ToString(null, null));
        return new[]
        {
            "native-format-argument=nullable-string",
            "native-arbitrary-object-format=excluded-by-static-type",
            "native-null-format=MTRL-",
        };
    }

    private static string[] ObserveAutoImmutability(JsonElement sourceCase)
    {
        AssertKeys(
            PythonFacts(sourceCase),
            "alias_groups",
            "class_add_extra",
            "class_delete_member",
            "class_reassign_member",
            "deepcopy_identity",
            "member_add_extra",
            "member_name",
            "member_set_name",
            "member_set_value",
            "shallow_copy_identity");
        AssertImmutableConventionType(AutoIdPrefix.Values, AutoIdPrefix.Material);
        AutoIdPrefix shallow = AutoIdPrefix.Material;
        AutoIdPrefix deep = AutoIdPrefix.FromValue(AutoIdPrefix.Material.Value);
        Assert.Same(AutoIdPrefix.Material, shallow);
        Assert.Same(AutoIdPrefix.Material, deep);
        return new[]
        {
            "native-class-mutation=impossible-through-public-api",
            "native-member-mutation=impossible-through-read-only-properties",
            "native-copy-identity=singleton-reference-preserved",
            "native-alias-groups=none",
        };
    }

    private static string[] ObserveDirectoryRoles(JsonElement sourceCase)
    {
        JsonElement facts = PythonFacts(sourceCase);
        Assert.Equal("Directory", RequiredString(facts, "class_name"));
        Assert.Equal(8, SimpleDragonEmbeddedData.Files.Count);
        Assert.Contains(SimpleDragonEmbeddedData.Material, SimpleDragonEmbeddedData.Files);
        Assert.Contains(SimpleDragonEmbeddedData.KoreanUsageProfile, SimpleDragonEmbeddedData.Files);
        Assert.Contains(SimpleDragonEmbeddedData.AddressWeather, SimpleDragonEmbeddedData.Files);
        Assert.All(SimpleDragonEmbeddedData.Files, path => Assert.NotEmpty(SimpleDragonEmbeddedData.ReadAllBytes(path)));

        WeatherMetadata metadata = SimpleDragonDatabase.Default.Weather.Items[0];
        var selection = new WeatherSelection(metadata, "native-probe", new DateTime(2020, 1, 1));
        Assert.Equal(
            Path.Combine("caller-weather", metadata.EpwFileName),
            selection.ResolveEpwPath("caller-weather"));
        return new[]
        {
            "native-layout=assembly-embedded-explicit-resource-catalog",
            "native-construction-resource-count=3",
            "native-profile-resource-count=3",
            "native-weather-metadata-resource-count=2",
            "native-weather-data-root=caller-supplied-to-WeatherSelection.ResolveEpwPath",
        };
    }

    private static string[] ObserveDirectoryRelocation(JsonElement sourceCase)
    {
        Assert.True(PythonFacts(sourceCase).GetProperty("relative_roles_equal").GetBoolean());
        byte[] first = SimpleDragonEmbeddedData.ReadAllBytes(SimpleDragonEmbeddedData.Material);
        byte[] second = SimpleDragonEmbeddedData.ReadAllBytes(SimpleDragonEmbeddedData.Material);
        Assert.Equal(first, second);

        WeatherMetadata metadata = SimpleDragonDatabase.Default.Weather.Items[0];
        var selection = new WeatherSelection(metadata, "native-probe", new DateTime(2020, 1, 1));
        string a = selection.ResolveEpwPath("location-a");
        string b = selection.ResolveEpwPath("location-b");
        Assert.NotEqual(a, b);
        Assert.Equal(Path.GetFileName(a), Path.GetFileName(b));
        return new[]
        {
            "native-embedded-resource-bytes=deployment-location-independent",
            "native-weather-root-relocation=caller-controlled",
            "native-weather-filename=stable-across-roots",
        };
    }

    private static string[] ObserveDirectoryImmutability(JsonElement sourceCase)
    {
        Assert.Equal(4, PythonFacts(sourceCase).GetProperty("attributes").GetArrayLength());
        Type type = typeof(SimpleDragonEmbeddedData);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.All(
            type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
            field => Assert.True(field.IsLiteral));
        Assert.All(
            type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
            property => Assert.Null(property.SetMethod));
        return new[]
        {
            "native-directory-contract=static-resource-api",
            "native-resource-identifiers=compile-time-constants",
            "native-resource-catalog=read-only",
        };
    }

    private static string[] ObserveDirectoryConstructionAdaptation(JsonElement sourceCase)
    {
        AssertKeys(PythonFacts(sourceCase), "construction", "keyword_argument", "positional_argument", "shadowed");
        Assert.True(typeof(SimpleDragonEmbeddedData).IsAbstract && typeof(SimpleDragonEmbeddedData).IsSealed);
        Assert.Empty(typeof(SimpleDragonEmbeddedData).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        return new[]
        {
            "native-Directory-class=no-direct-type",
            "native-construction=not-applicable-static-resource-and-weather-APIs",
            "native-instance-shadowing=excluded-by-static-contract",
        };
    }

    private static string[] ObservePackageTopology(JsonElement sourceCase)
    {
        JsonElement facts = PythonFacts(sourceCase);
        Assert.Equal("epsimple", RequiredString(facts, "name"));
        Assert.Equal(new[] { 0, 7, 0 }, facts.GetProperty("version").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(new[] { 3, 12 }, facts.GetProperty("required_python").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal("SimpleDragon", PackageInfo.Name);
        Assert.Equal("0.1.2", PackageInfo.Version);
        Assert.Equal("0.7.0", PackageInfo.Compatibility.UpstreamVersion);
        Assert.Equal(new[] { "net48", "net7.0-windows", "net8.0-windows" }, DeclaredTargetFrameworks());
        return new[]
        {
            "native-package-name=SimpleDragon",
            "native-package-version=0.1.2",
            "native-upstream-compatibility-version=0.7.0",
            "native-target-frameworks=net48,net7.0-windows,net8.0-windows",
        };
    }

    private static string[] ObservePackageImmutability(JsonElement sourceCase)
    {
        Assert.Equal(3, PythonFacts(sourceCase).GetProperty("attributes").GetArrayLength());
        Type type = typeof(PackageInfo);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.All(
            type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
            field => Assert.True(field.IsLiteral));
        Assert.All(
            type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
            property => Assert.Null(property.SetMethod));
        return new[]
        {
            "native-package-contract=static-class",
            "native-name-and-version=compile-time-constants",
            "native-compatibility=read-only-identity",
        };
    }

    private static string[] ObservePackageConstructionAdaptation(JsonElement sourceCase)
    {
        AssertKeys(PythonFacts(sourceCase), "construction", "keyword_argument", "positional_argument", "shadowed");
        Assert.Empty(typeof(PackageInfo).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        return new[]
        {
            "native-PackageInfo=static-class",
            "native-construction=not-applicable",
            "native-instance-shadowing=excluded-by-static-contract",
        };
    }

    private static string[] ObservePackageName(JsonElement sourceCase)
    {
        JsonElement facts = PythonFacts(sourceCase);
        Assert.Equal("epsimple-suffix", RequiredString(facts, "concat"));
        Assert.Equal("EPSIMPLE", RequiredString(facts, "upper"));
        Assert.Equal("SimpleDragon-suffix", PackageInfo.Name + "-suffix");
        Assert.Equal("SIMPLEDRAGON", PackageInfo.Name.ToUpperInvariant());
        return new[]
        {
            "native-name=SimpleDragon",
            "native-name-type=System.String",
            "native-name-operations=ordinary-immutable-string",
            "upstream-name=epsimple;adaptation=product-renaming",
        };
    }

    private static string[] ObservePackageVersion(JsonElement sourceCase)
    {
        Assert.Equal("0.7.0", RequiredString(PythonFacts(sourceCase), "join"));
        int[] upstream = PackageInfo.Compatibility.UpstreamVersion
            .Split('.')
            .Select(item => int.Parse(item, CultureInfo.InvariantCulture))
            .ToArray();
        int[] native = PackageInfo.Version
            .Split('.')
            .Select(item => int.Parse(item, CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(new[] { 0, 7, 0 }, upstream);
        Assert.Equal(new[] { 0, 1, 2 }, native);
        return new[]
        {
            "native-package-version=0.1.2",
            "native-compatible-upstream-version=0.7.0",
            "native-version-representation=immutable-dotted-string",
        };
    }

    private static string[] ObserveRequiredRuntime(JsonElement sourceCase)
    {
        Assert.True(PythonFacts(sourceCase).GetProperty("runtime_meets_requirement").GetBoolean());
        string[] frameworks = DeclaredTargetFrameworks();
        Assert.Equal(new[] { "net48", "net7.0-windows", "net8.0-windows" }, frameworks);
        return new[]
        {
            "native-runtime-contract=compiled-target-frameworks",
            "native-rhino7-framework=net48",
            "native-rhino8-frameworks=net7.0-windows,net8.0-windows",
            "upstream-python-runtime=not-required-by-native-module",
        };
    }

    private static string[] ObserveSpecialTopology(JsonElement sourceCase)
    {
        JsonElement facts = PythonFacts(sourceCase);
        Assert.Equal(5, facts.GetProperty("member_count").GetInt32());
        Assert.Empty(facts.GetProperty("alias_groups").EnumerateArray());
        AssertConventionMembers(facts.GetProperty("declared_members"), SpecialMembers.Select(item => (item.Name, item.Value)).ToArray());
        Assert.Equal(SpecialMembers.Select(item => item.Native), SpecialTag.Values);
        return new[]
        {
            "native-type=Dragons.SimpleDragon.SpecialTag",
            "native-shape=sealed-immutable-singleton-catalog",
            "native-member-names=" + string.Join(",", SpecialMembers.Select(item => item.Name)),
            "native-member-values=" + string.Join(",", SpecialMembers.Select(item => item.Value)),
            "native-member-count=5;native-alias-count=0",
        };
    }

    private static string[] ObserveSpecialStringSemantics(JsonElement sourceCase)
    {
        Assert.Equal(SpecialMembers.Length, PythonFacts(sourceCase).GetProperty("members").GetArrayLength());
        foreach (SpecialBinding item in SpecialMembers)
        {
            Assert.Same(item.Native, SpecialTag.FromValue(item.Value));
            Assert.Equal("prefix/" + item.Value, "prefix/" + item.Native.Value);
            Assert.Equal(item.Value + "/suffix", item.Native.Value + "/suffix");
            Assert.Contains(item.Value, item.Native.Value, StringComparison.Ordinal);
            Assert.Equal(StringComparer.Ordinal.GetHashCode(item.Value), item.Native.GetHashCode());
        }
        return new[]
        {
            "native-string-semantics=through-ordinal-Value",
            "native-value-roundtrip-count=5",
            "native-concatenation=token-preserving",
            "native-hash=ordinal-token-hash",
        };
    }

    private static string[] ObserveSpecialLookup(JsonElement sourceCase)
    {
        AssertKeys(PythonFacts(sourceCase), "from_name", "from_name_as_value", "from_value", "invalid");
        foreach (SpecialBinding item in SpecialMembers)
        {
            Assert.True(SpecialTag.TryFromValue(item.Value, out SpecialTag? parsed));
            Assert.Same(item.Native, parsed);
            if (item.Name == item.Value)
            {
                Assert.Same(item.Native, SpecialTag.FromValue(item.Name));
            }
            else
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => SpecialTag.FromValue(item.Name));
            }
        }
        Assert.Throws<ArgumentNullException>(() => SpecialTag.FromValue(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpecialTag.FromValue("__MISSING__"));
        return new[]
        {
            "native-exact-value-lookup-count=5",
            "native-name-as-value-only-when-token-equals-name=SPECIAL",
            "native-null-error=ArgumentNullException",
            "native-invalid-token-error=ArgumentOutOfRangeException",
        };
    }

    private static string[] ObserveSpecialFormatting(JsonElement sourceCase)
    {
        Assert.Equal(SpecialMembers.Length, PythonFacts(sourceCase).GetProperty("members").GetArrayLength());
        string[] specs = { string.Empty, "SURFACE", ":", " ", "표면" };
        foreach (SpecialBinding item in SpecialMembers)
        {
            Assert.Equal("$" + item.Value + "$:", item.Native.ToString());
            foreach (string spec in specs)
            {
                string expected = spec.Length == 0
                    ? "$" + item.Value + "$:"
                    : "$" + item.Value + ":" + spec + "$:";
                Assert.Equal(expected, item.Native.ToString(spec, CultureInfo.InvariantCulture));
            }
        }
        return new[]
        {
            "native-format-route=SpecialTag.ToString(string?,IFormatProvider?)",
            "native-empty-format=$VALUE$:",
            "native-custom-format=$VALUE:FORMAT$:",
            "native-format-member-count=5",
        };
    }

    private static string[] ObserveSpecialTypedFormatting(JsonElement sourceCase)
    {
        AssertKeys(
            PythonFacts(sourceCase),
            "direct_format_int",
            "direct_format_none",
            "direct_format_object",
            "direct_str_extra_argument",
            "format_builtin_none",
            "member_name");
        MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(typeof(SpecialTag).GetMethod(
            nameof(SpecialTag.ToString),
            new[] { typeof(string), typeof(IFormatProvider) }));
        Assert.Equal(typeof(string), method.GetParameters()[0].ParameterType);
        Assert.Equal("$SPECIAL$:", SpecialTag.Special.ToString(null, null));
        return new[]
        {
            "native-format-argument=nullable-string",
            "native-arbitrary-object-format=excluded-by-static-type",
            "native-null-format=$SPECIAL$:",
        };
    }

    private static string[] ObserveSpecialImmutability(JsonElement sourceCase)
    {
        AssertKeys(
            PythonFacts(sourceCase),
            "alias_groups",
            "class_add_extra",
            "class_delete_member",
            "class_reassign_member",
            "deepcopy_identity",
            "member_add_extra",
            "member_name",
            "member_set_name",
            "member_set_value",
            "shallow_copy_identity");
        AssertImmutableConventionType(SpecialTag.Values, SpecialTag.Special);
        SpecialTag shallow = SpecialTag.Special;
        SpecialTag deep = SpecialTag.FromValue(SpecialTag.Special.Value);
        Assert.Same(SpecialTag.Special, shallow);
        Assert.Same(SpecialTag.Special, deep);
        return new[]
        {
            "native-class-mutation=impossible-through-public-api",
            "native-member-mutation=impossible-through-read-only-properties",
            "native-copy-identity=singleton-reference-preserved",
            "native-alias-groups=none",
        };
    }

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetContract target,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueObjectKeysRecursive(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        AssertKeys(receipt, "classification", "fixture", "native_binding", "observations", "upstream");
        Assert.Equal(target.Classification, RequiredString(receipt, "classification"));
        Assert.Equal(observations.Count, receipt.GetProperty("observations").GetArrayLength());
        if (target.Classification == "exception")
        {
            Assert.False(string.IsNullOrWhiteSpace(target.AdaptationId));
        }
        else
        {
            Assert.Null(target.AdaptationId);
        }
        Assert.Equal(target.ReceiptSha256, CanonicalSha256(receipt));
    }

    private static string NativeRoute(string symbol)
    {
        if (symbol is "AUTOID_PREFIX" or
            "AUTOID_PREFIX.DAY_SCHEDULE" or
            "AUTOID_PREFIX.FENESTRATION" or
            "AUTOID_PREFIX.FENESTRATION_CONSTRUCTION" or
            "AUTOID_PREFIX.HEAT_EXCHANGER" or
            "AUTOID_PREFIX.MATERIAL" or
            "AUTOID_PREFIX.PROFILE" or
            "AUTOID_PREFIX.PV_PANEL" or
            "AUTOID_PREFIX.RULESET" or
            "AUTOID_PREFIX.SCHEDULE" or
            "AUTOID_PREFIX.SOURCE_SYSTEM" or
            "AUTOID_PREFIX.SUPPLY_SYSTEM" or
            "AUTOID_PREFIX.SURFACE" or
            "AUTOID_PREFIX.SURFACE_CONSTRUCTION" or
            "AUTOID_PREFIX.ZONE")
        {
            return "Dragons.SimpleDragon.AutoIdPrefix";
        }
        if (symbol == "AUTOID_PREFIX.__format__")
        {
            return "Dragons.SimpleDragon.AutoIdPrefix.ToString(string?, IFormatProvider?)";
        }
        if (symbol == "AUTOID_PREFIX.__str__")
        {
            return "Dragons.SimpleDragon.AutoIdPrefix.ToString()";
        }
        if (symbol == "Directory")
        {
            return "Dragons.SimpleDragon.SimpleDragonEmbeddedData and WeatherSelection.ResolveEpwPath";
        }
        if (symbol == "Directory.CONSTRUCTION_DIR")
        {
            return "Dragons.SimpleDragon.SimpleDragonEmbeddedData construction resources";
        }
        if (symbol == "Directory.PROFILE_DIR")
        {
            return "Dragons.SimpleDragon.SimpleDragonEmbeddedData profile resources";
        }
        if (symbol == "Directory.WEATHER_DATA_DIR")
        {
            return "Dragons.SimpleDragon.WeatherSelection.ResolveEpwPath";
        }
        if (symbol == "Directory.WEATHER_META_DIR")
        {
            return "Dragons.SimpleDragon.SimpleDragonEmbeddedData weather resources";
        }
        if (symbol == "PackageInfo")
        {
            return "Dragons.SimpleDragon.PackageInfo";
        }
        if (symbol == "PackageInfo.NAME")
        {
            return "Dragons.SimpleDragon.PackageInfo.Name";
        }
        if (symbol == "PackageInfo.REQUIRED_PYTHON")
        {
            return "net48, net7.0-windows, and net8.0-windows target frameworks";
        }
        if (symbol == "PackageInfo.VERSION")
        {
            return "Dragons.SimpleDragon.PackageInfo.Version and Compatibility.UpstreamVersion";
        }
        if (symbol is "SpecialTag" or "SpecialTag.CLONE" or "SpecialTag.COOLROOF" or
            "SpecialTag.DB" or "SpecialTag.FLIP" or "SpecialTag.SPECIAL")
        {
            return "Dragons.SimpleDragon.SpecialTag";
        }
        if (symbol == "SpecialTag.__format__")
        {
            return "Dragons.SimpleDragon.SpecialTag.ToString(string?, IFormatProvider?)";
        }
        if (symbol == "SpecialTag.__str__")
        {
            return "Dragons.SimpleDragon.SpecialTag.ToString()";
        }
        throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null);
    }

    private static void AssertConventionMembers(
        JsonElement value,
        IReadOnlyList<(string Name, string Value)> expected)
    {
        JsonElement[] members = value.EnumerateArray().ToArray();
        Assert.Equal(expected.Count, members.Length);
        for (int index = 0; index < members.Length; index++)
        {
            AssertKeys(members[index], "canonical_name", "name", "value");
            Assert.Equal(expected[index].Name, RequiredString(members[index], "canonical_name"));
            Assert.Equal(expected[index].Name, RequiredString(members[index], "name"));
            Assert.Equal(expected[index].Value, RequiredString(members[index], "value"));
        }
    }

    private static void AssertImmutableConventionType<T>(IReadOnlyList<T> values, T sample)
        where T : class
    {
        Type type = typeof(T);
        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            property => Assert.Null(property.SetMethod));
        IList<T> mutableView = Assert.IsAssignableFrom<IList<T>>(values);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(sample));
    }

    private static string[] DeclaredTargetFrameworks()
    {
        string project = File.ReadAllText(FindRepositoryFile(NativeArtifacts[^1].Path), Encoding.UTF8);
        Match match = Regex.Match(
            project,
            "<TargetFrameworks>(?<value>[^<]+)</TargetFrameworks>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        return match.Groups["value"].Value.Split(';');
    }

    private static JsonElement PythonFacts(JsonElement sourceCase) =>
        sourceCase.GetProperty("python").GetProperty("facts");

    private static bool CaseTargets(JsonElement sourceCase, string symbol) =>
        sourceCase.GetProperty("target_symbols").EnumerateArray()
            .Any(item => item.GetString() == symbol);

    private static string[] ReadStringArray(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static void AssertStringArray(JsonElement value, params string[] expected) =>
        Assert.Equal(expected, ReadStringArray(value));

    private static object ArtifactProjection(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
    };

    private static void ValidateArtifactProjection(
        JsonElement value,
        string path,
        int bytes,
        string sha256)
    {
        AssertKeys(value, "bytes", "path", "sha256");
        Assert.Equal(bytes, value.GetProperty("bytes").GetInt32());
        Assert.Equal(path, RequiredString(value, "path"));
        Assert.Equal(sha256, RequiredString(value, "sha256"));
    }

    private static void ValidateArtifact(string path, int bytes, string sha256)
    {
        byte[] content = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(bytes, content.Length);
        Assert.Equal(sha256, Sha256(content));
    }

    private static string RequiredString(JsonElement value, string property)
    {
        string? result = value.GetProperty(property).GetString();
        Assert.False(string.IsNullOrEmpty(result));
        return result!;
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.Equal(
            expected.OrderBy(item => item, StringComparer.Ordinal),
            value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
    }

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
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
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
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
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

    private static void AssertNoRawAddresses(string value)
    {
        Assert.False(Regex.IsMatch(value, @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(value, @"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(value, @"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d", RegexOptions.CultureInvariant));
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            Assert.False(Regex.IsMatch(value.GetString()!, @"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))", RegexOptions.CultureInvariant));
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
            Assert.False(double.IsNaN(number));
            Assert.False(double.IsInfinity(number));
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

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);
    private sealed record AutoBinding(string Name, string Value, AutoIdPrefix Native);
    private sealed record SpecialBinding(string Name, string Value, SpecialTag Native);
    private sealed record CaseBinding(string Code, string CaseId, string Subfamily, string FactsSha256, string CaseSha256);
    private sealed record ExpectedTarget(
        int InventoryIndex,
        string Symbol,
        string SymbolHash,
        string ReceiptSha256);
    private sealed record NativeObservation(string CaseId, string PythonFactsSha256, string[] Facts, string FactsSha256);
    private sealed record OracleCorpus(JsonElement[] Cases, TargetContract[] Targets);
    private sealed record TargetContract(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string Classification,
        string? AdaptationId,
        string AssertionId,
        string NativeRoute,
        string ReceiptSha256);
}
