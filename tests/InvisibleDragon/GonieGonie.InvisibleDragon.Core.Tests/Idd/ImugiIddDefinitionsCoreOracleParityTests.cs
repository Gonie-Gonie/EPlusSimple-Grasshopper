#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Idd;

public sealed class ImugiIddDefinitionsCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/imugi-idd-definitions-core-oracle.json";
    private const int FixtureBytes = 165_323;
    private const string FixtureSha256 =
        "sha256:3e56e7fe6026fef3146a62aadf3248940c65aa9a2b5c624b519fbc0e3d99dd69";
    private const string FixtureSchema =
        "goniegonie.python-reference.imugi-idd-definitions-core.v1";
    private const string FixtureRepositoryCommit = "f208041";
    private const string CasesSha256 =
        "sha256:002239e3f457bc553c44b4144c0e45e1b470ba7ababe0e2a4aa33c0038abc6ce";

    private const string GeneratorPath =
        "tools/python-reference/generate_imugi_idd_definitions_core_oracle.py";
    private const int GeneratorBytes = 70_965;
    private const string GeneratorSha256 =
        "sha256:fa70dfc565a30542f58697cee512701356cf2200b3f07332de4e345f0b7b1398";
    private const string ValidatorPath =
        "tests/PythonReference/test_imugi_idd_definitions_core_oracle.py";
    private const int ValidatorBytes = 23_636;
    private const string ValidatorSha256 =
        "sha256:b797ab5cb57509672d644bdc733ff2b8bd8534c4d697972f7722b944a7ff66f9";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_070;
    private const string InventoryFileSha256 =
        "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3";
    private const string InventoryContentSha256 =
        "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0";

    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/imugi.py";
    private const int UpstreamBytes = 91_815;
    private const string UpstreamSourceSha256 =
        "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613";
    private const string UpstreamAstSha256 =
        "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90";
    private const string TargetReceiptsSha256 =
        "sha256:cea1bdce699efee3b7f152d932f8dd1b52affe0ad139b642e3be2371446e5223";
    private const string DeferredReceiptsSha256 =
        "sha256:61f4342d8b5391b714de9ae1a37d505ed58d169d13fb1c739bac607c54056c96";
    private const string OutOfScopeReceiptsSha256 =
        "sha256:3ad4f99816b0591241fe459bd60a0af70f9a40e497be34bab7b132ced2fe42da";
    private const string DependenciesSha256 =
        "sha256:f69d29212b5ce6432b0c02f356d036275ea01463a8e1974ac6f89b78854fefba";
    private const string RuntimeSignaturesSha256 =
        "sha256:2e63f560d0e9a805d6357f763eb75512ccd0cb1f288c1ccea294928b52e6302a";
    private const string LoadedModulesSha256 =
        "sha256:b38033bf44c4359f5ee8cf44f8a12b2b267a2f4ddf83a25f0a13b5628b20f692";
    private const string RelocatedObservationsSha256 =
        "sha256:757fa1f6f1a78f595eb2894b11427cb2ee7ec9ceb61fe98df86d9d1eb3e939d4";
    private const string NativeClassificationSha256 =
        "sha256:18ec23c265e1ec4e1a03900cabb555878be94260721f3e3e0917494b636aa8ae";
    private const string NativeRoutesSha256 =
        "sha256:5d667ff19b24ac69c71cb9cc6b28421ac7a94b74a1607cc49e2efd1044a42418";
    private const string NativeSourceReceiptsSha256 =
        "sha256:79a9fdb0bbb7ab52d9203cda6225fa12bef234175f618c31f449334ae0e3fd02";
    private const string FullIddIdentitySha256 =
        "sha256:7e37ecb64566277e54a8c406dffd8df81517df6babfecba1a5a6feb6a9ba15af";

    private const string FullIddGeneratorPath =
        "tools/python-reference/generate_idd_schema_oracle.py";
    private const int FullIddGeneratorBytes = 38_634;
    private const string FullIddGeneratorSha256 =
        "sha256:64986549c0e3a3aadfef16606396006257d1be4e3b301058098ce364db8391f0";
    private const string FullIddFixturePath =
        "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz";
    private const int FullIddFixtureBytes = 585_482;
    private const string FullIddFixtureSha256 =
        "sha256:f2dfc27d39f788f945ef5cc3b79ffce2a516a568075717bd67088d900a75c705";
    private const string EnergyPlusIddSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const string NativeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Idd.ImugiIddDefinitionsCoreOracleParityTests.MatchesPinnedImugiIddDefinitionsThroughPublicProductionApis";

    private static readonly ArtifactPin[] ReviewedNativeArtifacts =
    {
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddDefinitions.cs", 13_005,
            "sha256:5e716db28821b68ae147ab0700380fdc6d406bb2666367903f3c12c2b54427ed"),
        new("src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddParser.cs", 19_960,
            "sha256:0f932fe250ca0e63b8734032abc34adf98c31ade16405caa547f5ac67c76823f"),
        new("tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idd/IddParserTests.cs", 8_339,
            "sha256:783ff125aa66cd72afe67ef5c45b69bc208a7c7f9a9d04fe99a930d9ec7a1eaa"),
        new("tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idd/IddSchemaOracleTests.cs", 16_860,
            "sha256:04d3a61e8c5d2a6bf7addc6900f5a8e0c2736005f90955f97641457cb27ea31f"),
    };

    private static readonly ArtifactPin[] ProductionSources = ReviewedNativeArtifacts.Take(2).ToArray();

    private static readonly ArtifactPin[] FullIddSupportArtifacts =
    {
        new(FullIddGeneratorPath, FullIddGeneratorBytes, FullIddGeneratorSha256),
        new(FullIddFixturePath, FullIddFixtureBytes, FullIddFixtureSha256),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "imugi-idd-definitions-core.field-class-and-construction", "field-construction",
            "sha256:7db966a34658ab56826ff09313db42fdc890b3395e90ff91d2fa9ae2af8c951a",
            "sha256:d9af6e406b1a21cedc127a4168d1c65fe9bf67d919c76e774187b9749ab84748",
            new[] { "IddField", "IddField.__init__" }),
        new("B01", "imugi-idd-definitions-core.field-equality", "field-equality",
            "sha256:202813e7ba7c1b561a2bc375bdcae66e698ecb2cee300dc130e7be479bc4884c",
            "sha256:5686c751946a0d9980d837d7f13b9bfd3b46347d0bf55ffd5344c784f47fba71",
            new[] { "IddField.__eq__" }),
        new("C01", "imugi-idd-definitions-core.field-fragment-parsing", "field-parser",
            "sha256:8916ca69542e0f3b232febad12877b394d9070594dfe7ea4b2d6c3bca9bc7815",
            "sha256:138e8e32a2a070e3ef2e9f1452b748d0851e52b0370ff9f5ebeb6279f5b92961",
            new[] { "IddField.from_text" }),
        new("D01", "imugi-idd-definitions-core.field-properties", "field-properties",
            "sha256:fc1a4435bcd89569e412aee78d85a14fcd8f48cde9ef0c800e2174f1e13223c2",
            "sha256:8350e46049c9d06038368828ea991d6d87e5cce74895f916151128dd0d1c7d20",
            new[] { "IddField.default", "IddField.external_list", "IddField.is_autocalculatable", "IddField.is_autosizable", "IddField.is_deprecated", "IddField.is_extensible", "IddField.is_required", "IddField.is_retaincase", "IddField.key", "IddField.maximum", "IddField.memo", "IddField.minimum", "IddField.name", "IddField.object_list", "IddField.reference", "IddField.reference_cls", "IddField.referenceable", "IddField.type", "IddField.unit" }),
        new("E01", "imugi-idd-definitions-core.object-class-and-construction", "object-construction",
            "sha256:485dae80cd1fce309c039161fa42f291bc775f2e7a103983da5acf61ba4ed21e",
            "sha256:67e38ac935576b850b0675f82947a500e59de1e91dab203d9b598e4c49016872",
            new[] { "IddObject", "IddObject.__init__" }),
        new("F01", "imugi-idd-definitions-core.object-equality", "object-equality",
            "sha256:6074b9ff57024b8f660643a3088ea9c791f174c8627de316563d3760648a1c40",
            "sha256:6738956989a52db24baebc8806c95a9f50e4e6fc718435ca99ddd423f3dfcfa2",
            new[] { "IddObject.__eq__" }),
        new("G01", "imugi-idd-definitions-core.object-fragment-parsing", "object-parser",
            "sha256:a9ad860cacce1bd0ef4b92c3662d7f58ab5f1d801c6c2c5e7fb03f2b717fd86b",
            "sha256:2fd8b5d6fb596da60a3ec3c79ca175f79fdc5ef7f93b5a1f85f48bb1be684d2a",
            new[] { "IddObject.from_text" }),
        new("H01", "imugi-idd-definitions-core.object-properties", "object-properties",
            "sha256:6d103260cb4ef1a89d451028abda8844cacfe5ce93628ad69721330a64bf87b3",
            "sha256:e92e05c38c65d60b046bfb9944b5c9dc65ea3f79897ea35beb9e29ea9627a522",
            new[] { "IddObject.begin_extensible", "IddObject.default", "IddObject.extensible", "IddObject.format", "IddObject.idd_index", "IddObject.is_obsolete", "IddObject.is_required", "IddObject.is_unique", "IddObject.memo", "IddObject.min_fields", "IddObject.name", "IddObject.reference", "IddObject.required_fields" }),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        new(1123, "IddField", "class", "sha256:209ee0732aa01e04fe36fb9d8b343da1db06ad0b61e4dd5ac2ca4bb51e54828c", "sha256:1c885862922126a62004a703eb2e58f7746430f37550ea0ff1c8b83b50b4ac84", "sha256:c411743b7a22c9604d52320facc868d537a2649d5fc3be0958759382336f323d", "imugi-idd-definitions-core-1123-209ee073", "exception", "typed-immutable-field-definition-1123", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition", "imugi-idd-definitions-core.field-class-and-construction"),
        new(1124, "IddField.__eq__", "function", "sha256:cc926b4c004774e340ca94860214ada51f03ae52472a3815ab62cdf4045dc3d0", "sha256:01931580940e7e1190016598c7845a350e95c883fb6b7cdf8461c14d89f63d9a", "sha256:0d9d38bfa4874ff358083631fa963b0b8140dfa804353702828f6a6ffb0bb95b", "imugi-idd-definitions-core-1124-cc926b4c", "exception", "field-by-field-structural-parity-without-value-equality-override-1124", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition public properties (structural comparison)", "imugi-idd-definitions-core.field-equality"),
        new(1125, "IddField.__init__", "function", "sha256:e4303f761ffd753b0f13f0e7787f0a5678244d6ba52c262982825f2e8bd0fe00", "sha256:b0c03bd4852c4c4f6258c20cc177fa993c9ba89301ae60de0fe2c21c9e0a485b", "sha256:eefa6e31944b26a986c14c495a9da517f4d417219f0abf5d2fa24cc4922b49ee", "imugi-idd-definitions-core-1125-e4303f76", "exception", "token-position-kind-explicit-validated-construction-1125", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition(...) constructor", "imugi-idd-definitions-core.field-class-and-construction"),
        new(1128, "IddField.default", "function", "sha256:4748d03a1c5f03ba7311bb44fc86dd2cdd539472398068e1561be04f64a6025c", "sha256:ef0f73d9d09ec8d966ab342dddfdead484c034726a4694edd956d78db79518df", "sha256:b94ef9e8731c3b4dae3bd5403df70ed7c58d0ee2e51ec91e09f9233dd989c331", "imugi-idd-definitions-core-1128-4748d03a", "exception", "lossless-string-default-instead-of-legacy-numeric-coercion-1128", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.DefaultValue", "imugi-idd-definitions-core.field-properties"),
        new(1129, "IddField.external_list", "function", "sha256:d3f9ed1ff2902468ffde530ec83ca689d33630c538ed2a1b92bbdb6172ae9411", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:955132a072a9ac106a91c0ec537d8dc157e82b2d6b5c2953abc26b7e38587f90", "imugi-idd-definitions-core-1129-d3f9ed1f", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.ExternalList", "imugi-idd-definitions-core.field-properties"),
        new(1130, "IddField.from_text", "function", "sha256:2b6d880add7b01117f24cb3903f951d22f50757f33326a3767b3e2da7b1b0ec3", "sha256:db59632e63d9a82f74786d094210a7fae20312dcec48d102a74cf79cd7b3b769", "sha256:bddbf4a84a1ad379f5650ba76d58f1f0e1cc1f291289c2a7129a2501ce5fd60f", "imugi-idd-definitions-core-1130-2b6d880a", "exception", "full-schema-parser-route-instead-of-field-fragment-parser-1130", "GonieGonie.InvisibleDragon.Idd.IddParser.Parse(...).Objects[0].Fields[0]", "imugi-idd-definitions-core.field-fragment-parsing"),
        new(1131, "IddField.is_autocalculatable", "function", "sha256:d0349be4c39e53ad17cf0e0c3e7559ad1d4fe4888d634dfec12d58cacb27953f", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:3bb8eeab2371b0c998b8cd395bc243479cd8a5f9a0a67e6cc4d56716fb642a48", "imugi-idd-definitions-core-1131-d0349be4", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.IsAutocalculatable", "imugi-idd-definitions-core.field-properties"),
        new(1132, "IddField.is_autosizable", "function", "sha256:a5aa4b09ed294aada05b999e8ee091143080fd3abb7a87eb1522bed2e9343ee9", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:26f296f9b2eb575039897e05b8ba882e0fa377f222e06489d06a678d69d88233", "imugi-idd-definitions-core-1132-a5aa4b09", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.IsAutosizable", "imugi-idd-definitions-core.field-properties"),
        new(1133, "IddField.is_deprecated", "function", "sha256:83922a08bed877914ce9235575d50cc12ec06c6c0c0c3c44df0a2bfbbbb376db", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:3751219997bc27d2a69ea56930c26b61a6fa8ce47a403be626cfe854bb1c5cbd", "imugi-idd-definitions-core-1133-83922a08", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.IsDeprecated", "imugi-idd-definitions-core.field-properties"),
        new(1134, "IddField.is_extensible", "function", "sha256:6f8cd3f124bb3e157a750e7c5364a01df927fb4249d81cb817d04e5a0aaf56a4", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:668787687b5d268c4393172820636896c207ac61ad502b73dc90dc7644796f03", "imugi-idd-definitions-core-1134-6f8cd3f1", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.BeginsExtensible", "imugi-idd-definitions-core.field-properties"),
        new(1135, "IddField.is_required", "function", "sha256:b64e21456b9791c019fdef355056d7e5c6940768ff8f5d535f9fa8ec4d7754db", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:b67b1493b339394473353521d4b3909ebba5ace0f0a6a4c1c138c4f746504cd4", "imugi-idd-definitions-core-1135-b64e2145", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.IsRequired", "imugi-idd-definitions-core.field-properties"),
        new(1136, "IddField.is_retaincase", "function", "sha256:46f4293276fe07cd6c66d7b9475d0c0cafc8eb8d44a4509894bde75a7266d769", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:e47d5d11c9335377e62eeed6c67a9b8039c448cb040b7e04764008b0af339b65", "imugi-idd-definitions-core-1136-46f42932", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.RetainsCase", "imugi-idd-definitions-core.field-properties"),
        new(1137, "IddField.key", "function", "sha256:b2a62552400e9c38d1834cbc8d93986e38e9a4ec9f09ef94f47bc5600d505f62", "sha256:8accb5cc40af7afc8a7aeaad91e5f497ec1134fb27b5489d3b6afda6afc4536d", "sha256:44aafb44ffc02bbfdb01d8c47afa35ce67eac3ab80fae3fb96cd4464832acefd", "imugi-idd-definitions-core-1137-b2a62552", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.Choices", "imugi-idd-definitions-core.field-properties"),
        new(1138, "IddField.maximum", "function", "sha256:0fc0df498a3c76c45082dd2adbf853ae6ac36c3d1b68ae51fa6a479ce14ffb70", "sha256:f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "sha256:78a67e9a7f45675654d7ab751d0bf8ba64ad65ee0f95c7a76351e9fe69bc4105", "imugi-idd-definitions-core-1138-0fc0df49", "exception", "explicit-inclusive-bound-instead-of-nextafter-sentinel-1138", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.Maximum", "imugi-idd-definitions-core.field-properties"),
        new(1139, "IddField.memo", "function", "sha256:203215fc3599a4174a2f54eabfff299cac752874f66079a230df9a436268ed6c", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:0f60520a06514ed2219adfdc2c05a666ec0ec1e647063b33ca1bdf1ed18ac854", "imugi-idd-definitions-core-1139-203215fc", "exception", "ordered-note-list-instead-of-formatted-sentence-string-1139", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.Notes", "imugi-idd-definitions-core.field-properties"),
        new(1140, "IddField.minimum", "function", "sha256:04d44d1d59dc3d1afe44806ee1aef4bd7f36883b6a085d961deaf0632484b29a", "sha256:f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "sha256:c042a6dad92c014aa65c63df3fbeb4b707e0a07429d4dbe7a500fc5ce925c00e", "imugi-idd-definitions-core-1140-04d44d1d", "exception", "explicit-inclusive-bound-instead-of-nextafter-sentinel-1140", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.Minimum", "imugi-idd-definitions-core.field-properties"),
        new(1141, "IddField.name", "function", "sha256:dc05a34b412c0963f73610621ae4c7befa1efdce2bf905c59c9edf231cb64f77", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:454cbcaacaa600fe9a3898044a921b8987b3ee094de83a56d3c7eacae4c80ad4", "imugi-idd-definitions-core-1141-dc05a34b", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.Name", "imugi-idd-definitions-core.field-properties"),
        new(1142, "IddField.object_list", "function", "sha256:f35f045ca2aa3b5cceb7649fdae15f615601e2f3c7790b4fb64d783cf07b67d8", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:e7b922bc23aca79e39e053e009239fcc1f13311d9a9cc977e8bb5204afce471b", "imugi-idd-definitions-core-1142-f35f045c", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.ObjectLists", "imugi-idd-definitions-core.field-properties"),
        new(1143, "IddField.reference", "function", "sha256:ecceb577b64a032652be4208494314bd23b7eff64d0861182f88461fa4fe8f80", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:0fa7a97176b55863e8175e6166fca9a7dc8c3a8b3326494198d8f1d9d62bba82", "imugi-idd-definitions-core-1143-ecceb577", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.References", "imugi-idd-definitions-core.field-properties"),
        new(1144, "IddField.reference_cls", "function", "sha256:704213becfa64b3f0e063a379b41995c8416067f02098d2f3fc61ae13a73c3a2", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:c04a813c21bffdba54edba140544f1c3967d3c5cd08a6c2a7cde5111ea33b139", "imugi-idd-definitions-core-1144-704213be", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.ReferenceClassNames", "imugi-idd-definitions-core.field-properties"),
        new(1145, "IddField.referenceable", "function", "sha256:4695751c1aa9e251af37469066247f690589c2330beb72fa89438ad04a4c9a8d", "sha256:0b201bb4b7a375a13ffd76fdd099aae0ac6cadf8c924c67c744b34c939881709", "sha256:25afc19ef62997fd26572ac68df9bc574ecb3f53d6b3947b944e21e9e8275291", "imugi-idd-definitions-core-1145-4695751c", "exception", "schema-projection-instead-of-mutable-backreference-list-1145", "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects projection over IddFieldDefinition.References", "imugi-idd-definitions-core.field-properties"),
        new(1146, "IddField.type", "function", "sha256:5b7e3d73506d79c8de9c7a2bd15951fe3351c543969a4ccc6df83308d370387f", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:c127d9c3b77bd9baf591dbecb6bac00e4249af93aa7b3ef5a503333c07abc581", "imugi-idd-definitions-core-1146-5b7e3d73", "exception", "closed-idd-data-type-enum-with-kind-derived-default-1146", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.DataType", "imugi-idd-definitions-core.field-properties"),
        new(1147, "IddField.unit", "function", "sha256:59edda63773ac040eeef9bc941906168d56459deaa79b7efbb93dac8a7bfd011", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:cee23df43be6648df234ab6b726d42e1bc1915f239ecdba6aa2f3e7c45e4e252", "imugi-idd-definitions-core-1147-59edda63", "exception", "separate-units-ip-units-and-units-based-on-field-metadata-1147", "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition.Units and UnitsBasedOnField", "imugi-idd-definitions-core.field-properties"),
        new(1148, "IddObject", "class", "sha256:8a75cdbcff665d5d249df5fd70d6be7b3c4ff1cf62d4924796b5a8309b9c26af", "sha256:b87b9b0fb86e5b2072d79b70fd35000bd2709bc965955befe2dd8718a52916f9", "sha256:ffd88d5b3e5b7deae3444431882436bb9c7f11743e842f0e7a30e7ca5e2a851b", "imugi-idd-definitions-core-1148-8a75cdbc", "exception", "typed-immutable-object-definition-1148", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition", "imugi-idd-definitions-core.object-class-and-construction"),
        new(1149, "IddObject.__eq__", "function", "sha256:355edc7a0ebf1dcdc410c30ce703816b335848009bf0a47d8ca6affcb57bfa23", "sha256:30a1df688e2680b0dd26954028e2863af01b7f2367308dc27b2e0a7ebe1a1281", "sha256:1ae730d07ef0a1ce8704eceb3efea74aff6e7416c126df0e7f31e20082c98474", "imugi-idd-definitions-core-1149-355edc7a", "exception", "field-by-field-structural-parity-without-value-equality-override-1149", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition public properties (structural comparison)", "imugi-idd-definitions-core.object-equality"),
        new(1150, "IddObject.__init__", "function", "sha256:a85878fd438a00a8c9391dc3c81dad3e34dfebc8a79d89172868753401542d19", "sha256:69ec82eae1b61306eb958f7931cb71af18f5d4eb7704c012e5613cddcebdad39", "sha256:408916150a60f5b94142b6a6ac75dee7806fbd6708be49aec353271e496818c0", "imugi-idd-definitions-core-1150-a85878fd", "exception", "ordered-consecutive-field-definition-construction-1150", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition(...) constructor", "imugi-idd-definitions-core.object-class-and-construction"),
        new(1153, "IddObject.begin_extensible", "function", "sha256:cc944d8ebdc2d6ab8c5515a8a41de1920f9ca2a3459622eab9c2e81b5336e7ba", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:8213adbfc184659dc1bb2b0d98757b4d787348cd3328c8f9f5f98d68ef48c398", "imugi-idd-definitions-core-1153-cc944d8e", "exception", "resolved-zero-based-extensible-start-index-1153", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.ExtensibleStartIndex", "imugi-idd-definitions-core.object-properties"),
        new(1154, "IddObject.default", "function", "sha256:ceed741c69fb49b6bc12f8455f3b875b8e146b0b2fef5b6f8cd9f8d127d8a4db", "sha256:8accb5cc40af7afc8a7aeaad91e5f497ec1134fb27b5489d3b6afda6afc4536d", "sha256:b94ef9e8731c3b4dae3bd5403df70ed7c58d0ee2e51ec91e09f9233dd989c331", "imugi-idd-definitions-core-1154-ceed741c", "exception", "field-default-projection-instead-of-cached-list-1154", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.Fields projection over DefaultValue", "imugi-idd-definitions-core.object-properties"),
        new(1155, "IddObject.extensible", "function", "sha256:656dae5addd602463386d773d8094419e466d8320eced25422ed22f8306f3293", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:e63cebea81ddd7edc880f6a459a3c5513aacb563122afef5bb32e6edf6579f93", "imugi-idd-definitions-core-1155-656dae5a", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.ExtensibleGroupSize", "imugi-idd-definitions-core.object-properties"),
        new(1156, "IddObject.format", "function", "sha256:54aa570dce77d57fbdc4fd6aea8abf9699bc533d8f765fd3d99747adc1b4cf44", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:c0b79b3a76ee300fa3952e1d0058f971722faed3a3665582cf1a8ee0a0403cbc", "imugi-idd-definitions-core-1156-54aa570d", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.Format", "imugi-idd-definitions-core.object-properties"),
        new(1157, "IddObject.from_text", "function", "sha256:e55a8f894fc1486c07c5853a8f3d41ea444dd58c29d8357588f47a3dea32e695", "sha256:c3856c65eff6bf2f2cd857ae90b984ed80879c9a0aef8ba17ac5664078a2e6b8", "sha256:f9c7c7d2da860cb2bca7720b9262effb0013e0dae13af39db7b2ff48f4134ef2", "imugi-idd-definitions-core-1157-e55a8f89", "exception", "full-schema-parser-route-instead-of-object-fragment-parser-1157", "GonieGonie.InvisibleDragon.Idd.IddParser.Parse(...).Objects[0]", "imugi-idd-definitions-core.object-fragment-parsing"),
        new(1158, "IddObject.idd_index", "function", "sha256:b7a1f5de05d189e74c63972c5105da91b69700743a056bb9373e9c3c81e64941", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:96a8f1c0e01af084a07686c6e47631baf4076033cbf6a60c82429b9b7b4ad404", "imugi-idd-definitions-core-1158-b7a1f5de", "exception", "ordered-field-token-projection-1158", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.Fields projection over Token", "imugi-idd-definitions-core.object-properties"),
        new(1159, "IddObject.is_obsolete", "function", "sha256:b654e57780f9263390dc8d45d58cd57ff675fa9feaa7c55ea0066e26aee173b0", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:d91e886640b3216db585e929ddf6784a755d2ea363a0535c1bf6ca0132ab5b6e", "imugi-idd-definitions-core-1159-b654e577", "exception", "obsolete-message-preservation-instead-of-boolean-only-1159", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.ObsoleteMessage", "imugi-idd-definitions-core.object-properties"),
        new(1160, "IddObject.is_required", "function", "sha256:b64e21456b9791c019fdef355056d7e5c6940768ff8f5d535f9fa8ec4d7754db", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:b67b1493b339394473353521d4b3909ebba5ace0f0a6a4c1c138c4f746504cd4", "imugi-idd-definitions-core-1160-b64e2145", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.IsRequired", "imugi-idd-definitions-core.object-properties"),
        new(1161, "IddObject.is_unique", "function", "sha256:9f99bd10fd99f8d79cca0d4f43d2af065f9947b8b725f402d78aa7065794e861", "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "sha256:1d3e393a196fecc2383b6426dc75f707b896318300e02cb16791d4c34a503027", "imugi-idd-definitions-core-1161-9f99bd10", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.IsUnique", "imugi-idd-definitions-core.object-properties"),
        new(1162, "IddObject.memo", "function", "sha256:203215fc3599a4174a2f54eabfff299cac752874f66079a230df9a436268ed6c", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:0f60520a06514ed2219adfdc2c05a666ec0ec1e647063b33ca1bdf1ed18ac854", "imugi-idd-definitions-core-1162-203215fc", "exception", "ordered-memo-list-instead-of-formatted-sentence-string-1162", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.Memo", "imugi-idd-definitions-core.object-properties"),
        new(1163, "IddObject.min_fields", "function", "sha256:1821fd046dacffa6cb854bb3fffac25f28f40c626e2425a90979a5539630c04a", "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876", "sha256:f8853b19a115d3e241ae5d8e2c2a162e4172ce532e6070f4c599b71b4ad4252e", "imugi-idd-definitions-core-1163-1821fd04", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.MinimumFields", "imugi-idd-definitions-core.object-properties"),
        new(1164, "IddObject.name", "function", "sha256:dc05a34b412c0963f73610621ae4c7befa1efdce2bf905c59c9edf231cb64f77", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:454cbcaacaa600fe9a3898044a921b8987b3ee094de83a56d3c7eacae4c80ad4", "imugi-idd-definitions-core-1164-dc05a34b", "equivalent", "not_applicable", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.Name", "imugi-idd-definitions-core.object-properties"),
        new(1165, "IddObject.reference", "function", "sha256:ecceb577b64a032652be4208494314bd23b7eff64d0861182f88461fa4fe8f80", "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "sha256:0fa7a97176b55863e8175e6166fca9a7dc8c3a8b3326494198d8f1d9d62bba82", "imugi-idd-definitions-core-1165-ecceb577", "exception", "additional-directive-preservation-for-reference-class-name-1165", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.AdditionalDirectives", "imugi-idd-definitions-core.object-properties"),
        new(1166, "IddObject.required_fields", "function", "sha256:b01c46a03850c3e5209c78a96141d1a125c934404d2f2024c845b8a21f6b8c39", "sha256:3600cccc11bc6800f262c4e5f0aacb4e7f2bf7ca486cbc455c0376a25e228afd", "sha256:2fd852b6f259cc890c7de93d0b9eea5f31a2a08fb109838b6bcc0c4c34e3aaf2", "imugi-idd-definitions-core-1166-b01c46a0", "exception", "required-field-definition-projection-instead-of-cached-name-list-1166", "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition.Fields projection over IsRequired", "imugi-idd-definitions-core.object-properties"),
    };

    // Set true only for a local discovery run, then freeze all emitted hashes below.
    private static bool DiscoverPins => false;

    private static readonly NativePin[] ExpectedNativePins =
    {
        new("A01", 25, "sha256:676a736a9dc1f029b5428ffca232ed1f07bdde700a4790a8876c3caa43453dd3"),
        new("B01", 7, "sha256:a9eb9f4173806f044daeddb92aacd83d5d057bdc2a44f61050cdc508a21dfcab"),
        new("C01", 25, "sha256:d180336669074f59b00c77cc06570a5461c51f752aab6470c680b211494bad26"),
        new("D01", 27, "sha256:5ce6a9199f53bd476df8809a17087594d1426751697222310dd2de101eef7483"),
        new("E01", 19, "sha256:cc6af54a8b10e195091e93f9133c6e7207e6cd4c618f3c39924f5a8dde7ad591"),
        new("F01", 7, "sha256:4cad3f75e0740ac201116f929d4a242ecc152741b2e2aa3efc821099944e7751"),
        new("G01", 18, "sha256:1b4f12941b6db9114607240b36de663c2ae5a7083ef57c7d46b5470c240bd47e"),
        new("H01", 23, "sha256:c7e32caccfd6cbe5a834e41af782e5e9a63a989c2ed215f869fc3ec6897c6658"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:11465c65883f63aeea67c0fa88d37042589bba176d42f18c6c2bf92dfc9ca5e8", // 1123 IddField
        "sha256:2db9a361deca0360c042dec43ed3bd9a6e26263af0e48c75367d91d3da6c4205", // 1124 IddField.__eq__
        "sha256:78726b4e017dda28550728ccf0d53379b1aebf1e883c0173675b37bc09129208", // 1125 IddField.__init__
        "sha256:6aff1a32f27c68366a1301520afc7363d7624f2a2b785a70b6107846c26ae615", // 1128 IddField.default
        "sha256:1eb3aafd85943de37f9649bc97c836969c2ce98b71e8781cf987127f1b5c063c", // 1129 IddField.external_list
        "sha256:8b9912a4c6f36eda42d920326e3beae7b432317946247a999f5d33a8870ec7ca", // 1130 IddField.from_text
        "sha256:1fb85af57dd88d7c3424836671a208f84c55e31186980e6cbfdb708d5cb14ef7", // 1131 IddField.is_autocalculatable
        "sha256:e00d5522733d414efa241ce95b13336aa77ca058f3d4aff95cf2996e3f4c8397", // 1132 IddField.is_autosizable
        "sha256:b7fa299d8ce591b78b469433d9dc584d52c867807c1d8fb919b9f5304c32f7d7", // 1133 IddField.is_deprecated
        "sha256:8cb19253e511bf5cc94d10c85dbbb380a1aedfe7dfca6294e16a38ff0dca2ff8", // 1134 IddField.is_extensible
        "sha256:32afa60db379cf00b9f202e8fd6dccf0e3c809f3df635f542501260dec41d71d", // 1135 IddField.is_required
        "sha256:692fc8b89aa5b3bd1e40b096461612892448a8f8dfbe7d9d59582e6b87ab29b5", // 1136 IddField.is_retaincase
        "sha256:55a59dd865004479377820811afef5c29567bf9a3427562b65de29606f0acf5d", // 1137 IddField.key
        "sha256:6dc457f34627a82d395df085158a62c0a4ff9df02b01326b50aced690aa0649f", // 1138 IddField.maximum
        "sha256:b201f99a7fda44c68872c258036a15469c8bb2bc09dd2e2f1792b555762d1553", // 1139 IddField.memo
        "sha256:1e6ea2b2b7f235f6b7aa99937fc4c4732e0558c71b54a3fc705103e6470bd8d5", // 1140 IddField.minimum
        "sha256:8af36bcf459e9fbe86b41190774544e0b2f3c6290861525c46bdb6dc0b229a9e", // 1141 IddField.name
        "sha256:2ea49ee72526df51d8ec95ccc1f8c2a3789a9c0d874781c4c24cfaf589ecb65a", // 1142 IddField.object_list
        "sha256:b7c2e12e20964cdf1b72ce81b6ad1990b59ae809a6c38b2968092a9edf3fb822", // 1143 IddField.reference
        "sha256:20fd112a5f75a9659379f6595eceebaa8b29227cda84b44ff2bc30819ec26fdf", // 1144 IddField.reference_cls
        "sha256:c68f481024a739af7c6ff41b7ebb081bb23f903c696a28dfcbf65b684c6c3028", // 1145 IddField.referenceable
        "sha256:45caf1f5369057da52e86294e596ffd62720b7bbc0485cea03a8b3b091f19971", // 1146 IddField.type
        "sha256:4ae32fe67c1a15bdc4d10f5245edbe7bb0f7bb9857402a287fbcdb3974ca0810", // 1147 IddField.unit
        "sha256:ab78b423c973cbb2d17cf8787c275e6d78fcfb66ecec3273f1de8ea128fda1ee", // 1148 IddObject
        "sha256:1ec0e0d0b7f42b1dbe0ab380857c9be77e5e38fa02e2a11e3892321fcb61ea0a", // 1149 IddObject.__eq__
        "sha256:42f8842eca2aebcb52f962a913bf99c01d15ef7cf16e6a61855064a40585c534", // 1150 IddObject.__init__
        "sha256:32607d13d85d8790224f27a2306e5e87a1e1ef12b92b3c79265dc7c39e560eff", // 1153 IddObject.begin_extensible
        "sha256:fac5807754f6c98414fc0bced33ad3b967984ef047fc9edfaa41ed3efe9479fd", // 1154 IddObject.default
        "sha256:e342cc7f4ce81549017f3ea89b14e3de33b3a21c0b020d5962a20391fc6df0b4", // 1155 IddObject.extensible
        "sha256:c797e5dfdbcad668fc3e87653c48064074e0ac845d8e80f3cc6bffbe1224cd11", // 1156 IddObject.format
        "sha256:a23e3efc1dd4330c9fe48f512e61698293368f9620c44f6582555b2aeed03cc6", // 1157 IddObject.from_text
        "sha256:6b15ed10c4b470e5531a5c3ab17fef05c380e7774562a0280a14d53efa6cb0a7", // 1158 IddObject.idd_index
        "sha256:4422852be2c5e5744639ae95f83f84d101868b12146ee6237b43b94a55fb0702", // 1159 IddObject.is_obsolete
        "sha256:f6fce9ff22090489eb653682b8c94442f5a60310b7d0cec241859aa2dd616e60", // 1160 IddObject.is_required
        "sha256:720a16ce2e26b5bde087d5a6fabdcdfc98c6d6af679c1ecd1b25917ee12f5be7", // 1161 IddObject.is_unique
        "sha256:046929c27229e3cf2a249ed64dd4759f4db0339cbdcc6bd5502e32c3c737cf80", // 1162 IddObject.memo
        "sha256:1b9db4c65e380598a1955b6e56de2554e99adb0ca531379e94fb96fb6dc7e964", // 1163 IddObject.min_fields
        "sha256:0abcf434f973f5da1fc287a4c575e8ccdecfd5c0608fc5ca169679d11b9f965f", // 1164 IddObject.name
        "sha256:b4c4bef580d189f6d81f63ecb833b34266b82c52c5408922f4598e3c3fcc6aeb", // 1165 IddObject.reference
        "sha256:c530c822b4dbb341fe0690d38dc4a0a0e85136a481845204f981c06f3295596e", // 1166 IddObject.required_fields
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:9d137d5283eeff82b72ddd1fa2e47e626a4a9446dbbcac83242caa9083dd2b4d", // imugi-idd-definitions-core-1123-209ee073
        "sha256:f4c227c209456056a8b725767d9c62baf6cfb77183e27057a5801c382942b118", // imugi-idd-definitions-core-1124-cc926b4c
        "sha256:3cb6067f47094592c4937818512b9b8fa46ee3a06674896b0fbf0dbeb5009131", // imugi-idd-definitions-core-1125-e4303f76
        "sha256:15ff9327b246e8b08123764a74e157887e7304006687189ed7d805335fe0a054", // imugi-idd-definitions-core-1128-4748d03a
        "sha256:2bfe79d483eb810c2deb3397e55f33dd2c56a82aef070dd9c21df85a7b2f0cf8", // imugi-idd-definitions-core-1129-d3f9ed1f
        "sha256:341cb5e4afaa61813b6266831688bdf8c86b653cdd5d5ee4bb22ac34727ddab8", // imugi-idd-definitions-core-1130-2b6d880a
        "sha256:be4180f9a9424a8a0a4901a0c616ae1332b5ddc7283bb3256091c1b34d450d2e", // imugi-idd-definitions-core-1131-d0349be4
        "sha256:173fc29335d8dde40595636790c2414e0a2da379a45608c7cbebb97bbb55dd10", // imugi-idd-definitions-core-1132-a5aa4b09
        "sha256:f32dd7ffdf61c8cc0190df4518fd039403d08a670ce47ca39b61eea09ff7e690", // imugi-idd-definitions-core-1133-83922a08
        "sha256:d62c9814d8215d835516a2322a904cab7a74963c970807e396182b529e94129a", // imugi-idd-definitions-core-1134-6f8cd3f1
        "sha256:415c13a372bbc7c54d388675b8f50b17ebf4429ff279553683ba5704c8563302", // imugi-idd-definitions-core-1135-b64e2145
        "sha256:37689102cc70615d32125dea8d6e90849123b0571fd70e667efde1ef1250a8ee", // imugi-idd-definitions-core-1136-46f42932
        "sha256:fa551b81235d4ba36d4809ebb893a73b51aa80f5a8d732fd1a815c2bc74eb4fb", // imugi-idd-definitions-core-1137-b2a62552
        "sha256:4703770c22ea3018b781bb07d1f3e8f761a90a95adb36187250b98aaa153c43c", // imugi-idd-definitions-core-1138-0fc0df49
        "sha256:87662be1d512ed73fff82aa5023c42c1677d35f93a39faa5d88fde1ef9c0a6fe", // imugi-idd-definitions-core-1139-203215fc
        "sha256:4aee0fa6e55413e119d35e70d225e549644e1b06101739c4f2cf642fb5a3167d", // imugi-idd-definitions-core-1140-04d44d1d
        "sha256:176ad20d5e2698c95b7c571aaf8c846b2fd8b26b361026721c1effc7ecb248e7", // imugi-idd-definitions-core-1141-dc05a34b
        "sha256:58b4526e67e448fe36e43eff876e311865459ff2479adda684f77d8bb2a0f05f", // imugi-idd-definitions-core-1142-f35f045c
        "sha256:893510f276fa098d06b719bf6de9169672ae985e705a29a7c31cb6b27c056881", // imugi-idd-definitions-core-1143-ecceb577
        "sha256:588690135b7f5f7b99c464b5adae6b28ffb8e6c8ea9063b1d5c57f8caf434209", // imugi-idd-definitions-core-1144-704213be
        "sha256:393aebbd921b380982b50cd96868d0908e50457cd4c4d51fe9128493c0cc653e", // imugi-idd-definitions-core-1145-4695751c
        "sha256:f1c935a3cce0915630ad58e2861694edffad299eb7d398152f8d0188937dfd33", // imugi-idd-definitions-core-1146-5b7e3d73
        "sha256:924605805d6ad918e4b52c5fb7d82ae13ef96984359cf52709cdf8271aaf8b8e", // imugi-idd-definitions-core-1147-59edda63
        "sha256:c3ee9252bc55c14689fa778d202b826eabb6db0d498b95635a867ff19c09074e", // imugi-idd-definitions-core-1148-8a75cdbc
        "sha256:b2931121bc1f80a86839aeefb28ab315649c5ce872a1faa5106e2d931cb5d8e8", // imugi-idd-definitions-core-1149-355edc7a
        "sha256:a5d9b8a39a73a532245f137eef0feb02f7b8e2b445adf469ae3ced73606ec236", // imugi-idd-definitions-core-1150-a85878fd
        "sha256:ab82d0d0a8e1d70bb9e08eef686b8979c0ab528b704fe8e4cac24e38db9c1c1c", // imugi-idd-definitions-core-1153-cc944d8e
        "sha256:3a3616ae83e2a3027cd7781b0e02e1f7ff9ee5ac76018e655af8e14e916c8902", // imugi-idd-definitions-core-1154-ceed741c
        "sha256:f114d072e4a8f16f06a43bea6f929f8b052aa270a386a1f48d1c5d1b1bad05f9", // imugi-idd-definitions-core-1155-656dae5a
        "sha256:f966d8d93570263c81ee8990b826ffdad14a6c86a72cd4857d010833a98e6b8f", // imugi-idd-definitions-core-1156-54aa570d
        "sha256:999060046e6470b63e782d66eaec66bfea422a9b82b1176859c512111cc503e8", // imugi-idd-definitions-core-1157-e55a8f89
        "sha256:c04a03e27c8862a5366a98c0a7428052d7a667834d1e7a8b15133a461f345451", // imugi-idd-definitions-core-1158-b7a1f5de
        "sha256:4f99e9b3f31f9f8723e6d3d21c023cee2ac15dde5c17fbf7281775dbe8fabd74", // imugi-idd-definitions-core-1159-b654e577
        "sha256:61b392a42c719febd001d5bf07bb90a8d5587ec5166dda3a513458dbe31b13c7", // imugi-idd-definitions-core-1160-b64e2145
        "sha256:6ee5b51dab7226e62d61ae91a9a88793bb71ce9fac07edca5c77e40cb1855d8d", // imugi-idd-definitions-core-1161-9f99bd10
        "sha256:fd240675909c8ba3848269184a2302f95676e4eddf7dd7a49424f56e29f2b8b8", // imugi-idd-definitions-core-1162-203215fc
        "sha256:874a67f14644a76eaf0110d679a402ab8796cf804b0d43467234eb9b14e77cfd", // imugi-idd-definitions-core-1163-1821fd04
        "sha256:c093f5045e5f3c7568b750eeed7547c0c3382888f98d718a0ec39e4b4f7f6459", // imugi-idd-definitions-core-1164-dc05a34b
        "sha256:39b5eb618540d1d550d82c4b3b61a71b4a487425798647655374d5dea4730cb7", // imugi-idd-definitions-core-1165-ecceb577
        "sha256:87ef21bf335b669ed0d47c30fa3e6f218cd93389d919a7acb49891042cc75033", // imugi-idd-definitions-core-1166-b01c46a0
    };

    [Fact]
    public void MatchesPinnedImugiIddDefinitionsThroughPublicProductionApis()
    {
        ValidatePinnedArtifactsAndPublicApi();
        ValidateFullIddSupportArtifact();
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
            throw new Xunit.Sdk.XunitException(
                "IMUGI_IDD_DEFINITIONS_NATIVE_PINS" + Environment.NewLine +
                "CASES" + Environment.NewLine + factPins + Environment.NewLine +
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

        Assert.Equal(40, recordCount);
        Assert.Equal(40, corpus.Targets.Length);
        Assert.Equal(40, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(18, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(22, corpus.Targets.Count(item => item.Classification == "exception"));
        Assert.Equal(8, corpus.FixtureCases.Length);
    }

    private static void ValidatePinnedArtifactsAndPublicApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin pin in ReviewedNativeArtifacts.Concat(FullIddSupportArtifacts))
        {
            AssertPinnedArtifact(pin.Path, pin.Bytes, pin.Sha256);
        }

        ConstructorInfo fieldConstructor = Assert.Single(
            typeof(IddFieldDefinition).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        ConstructorInfo objectConstructor = Assert.Single(
            typeof(IddObjectDefinition).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        MethodInfo parse = Assert.IsAssignableFrom<MethodInfo>(typeof(IddParser).GetMethod(
            nameof(IddParser.Parse),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null));
        Assert.True(fieldConstructor.IsPublic);
        Assert.True(objectConstructor.IsPublic);
        Assert.True(parse.IsPublic);
        Assert.Equal(typeof(IddSchema), parse.ReturnType);
        Assert.True(typeof(IddFieldDefinition).IsPublic);
        Assert.True(typeof(IddObjectDefinition).IsPublic);
        Assert.True(typeof(IddSchema).IsPublic);
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

    private static void ValidateFullIddSupportArtifact()
    {
        using FileStream file = File.OpenRead(FindRepositoryFile(FullIddFixturePath));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using JsonDocument support = JsonDocument.Parse(gzip, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
        JsonElement root = support.RootElement;
        AssertUniqueObjectKeysRecursive(root);
        Assert.Equal("goniegonie.energyplus-idd-schema.v1", RequiredString(root, "oracle_schema"));
        Assert.Equal("24.2.0", RequiredString(root, "energyplus_version"));
        Assert.Equal("94a887817b", RequiredString(root, "energyplus_build"));
        Assert.Equal(848, root.GetProperty("object_count").GetInt32());
        Assert.Equal(13_702, root.GetProperty("field_count").GetInt32());
        Assert.Equal(EnergyPlusIddSha256, RequiredString(root, "source_sha256"));
        Assert.Equal(UpstreamCommit, RequiredString(root, "upstream_commit"));
        JsonElement official = root.GetProperty("official_epjson_schema");
        Assert.Equal(848, official.GetProperty("object_count").GetInt32());
        Assert.Equal(13_469, official.GetProperty("validated_field_occurrence_count").GetInt32());
    }

    private static OracleCorpus ValidateFixture(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertNoHostPaths(root);
        AssertKeys(root,
            "case_sha256", "cases", "cases_sha256", "consumer_contract",
            "deferred_receipts", "fact_sha256", "native_review", "out_of_scope_receipts",
            "runtime", "schema", "support", "symbols", "target_receipts", "upstream");
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
            JsonElement python = actual.GetProperty("python");
            Assert.Equal("observed", RequiredString(python, "outcome"));
            Assert.Equal(expected.PythonFactsSha256, RequiredString(python, "facts_sha256"));
            Assert.Equal(expected.PythonFactsSha256, RequiredString(factHashes, expected.CaseId));
            Assert.Equal(expected.PythonFactsSha256, CanonicalSha256(python.GetProperty("facts")));
            Assert.Equal(expected.TargetSymbols, ReadStringArray(actual.GetProperty("target_symbols")));
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
        JsonElement partition = upstream.GetProperty("partition_receipts_sha256");
        Assert.Equal(TargetReceiptsSha256, RequiredString(partition, "target"));
        Assert.Equal(DeferredReceiptsSha256, RequiredString(partition, "deferred"));
        Assert.Equal(OutOfScopeReceiptsSha256, RequiredString(partition, "out_of_scope"));
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(root.GetProperty("target_receipts")));
        Assert.Equal(DeferredReceiptsSha256, CanonicalSha256(root.GetProperty("deferred_receipts")));
        Assert.Equal(OutOfScopeReceiptsSha256, CanonicalSha256(root.GetProperty("out_of_scope_receipts")));
        Assert.Equal(40, root.GetProperty("target_receipts").GetArrayLength());
        Assert.Equal(65, root.GetProperty("deferred_receipts").GetArrayLength());
        Assert.Equal(28, root.GetProperty("out_of_scope_receipts").GetArrayLength());
        JsonElement isolated = upstream.GetProperty("isolated_import");
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal("two-byte-identical-repository-temp-copies", RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(LoadedModulesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(LoadedModulesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        Assert.Equal(RelocatedObservationsSha256, RequiredString(isolated, "relocated_observations_sha256"));

        ValidateRuntimeReviewAndSupport(root);
        JsonElement contract = root.GetProperty("consumer_contract");
        ValidateContractClosure(contract);
        JsonElement targetReceipts = root.GetProperty("target_receipts");
        JsonElement[] actualTargets = targetReceipts.EnumerateArray().ToArray();
        JsonElement[] symbols = root.GetProperty("symbols").EnumerateArray().ToArray();
        Assert.Equal(ExpectedTargets.Length, actualTargets.Length);
        Assert.Equal(ExpectedTargets.Length, symbols.Length);
        var targets = new TargetBinding[ExpectedTargets.Length];
        for (int index = 0; index < ExpectedTargets.Length; index++)
        {
            ExpectedTarget expected = ExpectedTargets[index];
            JsonElement actual = actualTargets[index];
            Assert.Equal(expected.InventoryIndex, actual.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(expected.Kind, RequiredString(actual, "kind"));
            Assert.Equal(expected.SymbolHash, RequiredString(actual, "symbol_hash"));
            Assert.Equal(expected.SignatureHash, RequiredString(actual, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(actual, "body_hash"));
            Assert.Equal(UpstreamPath, RequiredString(actual, "path"));
            JsonElement descriptor = symbols[index];
            Assert.Equal(expected.Symbol, RequiredString(descriptor, "symbol"));
            Assert.Equal(expected.SymbolHash, RequiredString(descriptor, "symbol_hash"));
            Assert.Equal(expected.SignatureHash, RequiredString(descriptor, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(descriptor, "body_hash"));

            JsonElement expectation = contract.GetProperty("expectations").GetProperty(expected.Symbol);
            Assert.Equal(expected.AssertionId, RequiredString(expectation, "assertion_id"));
            Assert.Equal(expected.Classification, RequiredString(expectation, "classification"));
            Assert.Equal(expected.AdaptationId, RequiredString(expectation, "adaptation"));
            Assert.Equal(expected.NativeRoute, RequiredString(expectation, "native_route"));
            Assert.Equal(expected.Classification, RequiredString(contract.GetProperty("classifications"), expected.Symbol));
            Assert.Equal(expected.NativeRoute, RequiredString(contract.GetProperty("native_routes"), expected.Symbol));
            Assert.Equal(expected.AssertionId, RequiredString(contract.GetProperty("assertion_ids"), expected.Symbol));
            Assert.Equal(expected.CaseId, RequiredString(contract.GetProperty("coverage_by_symbol"), expected.Symbol));
            Assert.StartsWith("GonieGonie.InvisibleDragon.Idd.", expected.NativeRoute, StringComparison.Ordinal);
            Assert.DoesNotContain(".Internal", expected.NativeRoute, StringComparison.Ordinal);
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
                expected.CaseId);
        }

        Assert.Equal(18, targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(22, targets.Count(item => item.Classification == "exception"));
        Assert.Equal(40, targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        return new OracleCorpus(fixtureCases, targets);
    }

    private static void ValidateRuntimeReviewAndSupport(JsonElement root)
    {
        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(DependenciesSha256, RequiredString(runtime, "dependencies_sha256"));
        Assert.Equal(DependenciesSha256, CanonicalSha256(runtime.GetProperty("dependencies")));

        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.Equal(RuntimeSignaturesSha256, CanonicalSha256(contract.GetProperty("runtime_signatures")));
        JsonElement review = root.GetProperty("native_review");
        Assert.False(review.GetProperty("python_executes_native_runtime").GetBoolean());
        Assert.True(review.GetProperty("public_production_routes_only").GetBoolean());
        Assert.Equal(NativeClassificationSha256, RequiredString(review, "classification_sha256"));
        Assert.Equal(NativeRoutesSha256, RequiredString(review, "routes_sha256"));
        Assert.Equal(NativeSourceReceiptsSha256, RequiredString(review, "source_receipts_sha256"));
        Assert.Equal(NativeSourceReceiptsSha256, CanonicalSha256(review.GetProperty("source_receipts")));
        JsonElement[] reviewed = review.GetProperty("source_receipts").EnumerateArray().ToArray();
        Assert.Equal(ReviewedNativeArtifacts.Length, reviewed.Length);
        for (int index = 0; index < reviewed.Length; index++)
        {
            ValidateArtifact(reviewed[index], ReviewedNativeArtifacts[index]);
        }

        JsonElement support = root.GetProperty("support");
        ValidateArtifact(support.GetProperty("generator"), FullIddSupportArtifacts[0]);
        ValidateArtifact(support.GetProperty("fixture"), FullIddSupportArtifacts[1]);
        Assert.Equal(FullIddIdentitySha256, RequiredString(support, "full_schema_identity_sha256"));
        Assert.Equal(FullIddIdentitySha256, CanonicalSha256(support.GetProperty("full_schema_identity")));
        JsonElement identity = support.GetProperty("full_schema_identity");
        Assert.Equal("goniegonie.energyplus-idd-schema.v1", RequiredString(identity, "oracle_schema"));
        Assert.Equal("24.2.0", RequiredString(identity, "energyplus_version"));
        Assert.Equal(848, identity.GetProperty("object_count").GetInt32());
        Assert.Equal(13_702, identity.GetProperty("field_count").GetInt32());
        Assert.Equal(EnergyPlusIddSha256, RequiredString(identity, "source_sha256"));
    }

    private static void ValidateContractClosure(JsonElement contract)
    {
        JsonElement counts = contract.GetProperty("classification_counts");
        Assert.Equal(18, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(22, counts.GetProperty("exception").GetInt32());
        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_imugi_source_partition").GetBoolean());
        Assert.Equal(133, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(40, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(65, closure.GetProperty("deferred_count").GetInt32());
        Assert.Equal(28, closure.GetProperty("out_of_scope_count").GetInt32());
        JsonElement evidence = contract.GetProperty("evidence_contract");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("exact_cpython_behavior_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("full_energyplus_idd_support_hash_pinned").GetBoolean());
        Assert.True(evidence.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
        Assert.Equal(40, evidence.GetProperty("expected_receipt_count").GetInt32());
    }

    private static NativeObservation ObserveNativeCase(CaseBinding item)
    {
        string[] facts = item.Code switch
        {
            "A01" => ObserveFieldConstruction(),
            "B01" => ObserveFieldEquality(),
            "C01" => ObserveFieldParser(),
            "D01" => ObserveFieldProperties(),
            "E01" => ObserveObjectConstruction(),
            "F01" => ObserveObjectEquality(),
            "G01" => ObserveObjectParser(),
            "H01" => ObserveObjectProperties(),
            _ => throw new InvalidOperationException("Unknown native Imugi case: " + item.Code),
        };
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        return new NativeObservation(
            item.Code,
            item.CaseId,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveFieldConstruction()
    {
        var notes = new List<string> { " First note ", "Second note" };
        var choices = new List<string> { " On ", "Off" };
        var objectLists = new List<string> { " ObjectNames " };
        var references = new List<string> { "ReferenceNames" };
        var referenceClasses = new List<string> { "ReferenceClasses" };
        var additional = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["custom-directive"] = new[] { " custom value " },
        };
        var field = new IddFieldDefinition(
            " A1 ", 0, IddFieldKind.Alpha, " Mode ", notes, " m ", " ft ", " A2 ",
            isRequired: true, beginsExtensible: true, isDeprecated: true,
            isAutosizable: true, isAutocalculatable: true, retainsCase: true,
            defaultValue: " 1.25 ", dataType: IddDataType.Choice, choices: choices,
            objectLists: objectLists, externalList: " ExternalNames ", references: references,
            referenceClassNames: referenceClasses,
            minimum: new IddNumericBound(0, false), maximum: new IddNumericBound(10, true),
            additionalDirectives: additional);
        notes.Clear();
        choices.Clear();
        objectLists.Clear();
        references.Clear();
        referenceClasses.Clear();
        additional.Clear();

        Assert.Equal("A1", field.Token);
        Assert.Equal("Mode", field.Name);
        Assert.Equal(new[] { "First note", "Second note" }, field.Notes);
        Assert.Equal(new[] { "On", "Off" }, field.Choices);
        Assert.Equal(new[] { "ObjectNames" }, field.ObjectLists);
        Assert.Equal(new[] { "ReferenceNames" }, field.References);
        Assert.Equal(new[] { "ReferenceClasses" }, field.ReferenceClassNames);
        Assert.Single(field.AdditionalDirectives);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)field.Choices).Add("Drift"));
        string emptyToken = Assert.Throws<ArgumentException>(() =>
            new IddFieldDefinition(" ", 0, IddFieldKind.Alpha, "Name")).GetType().Name;
        string negativePosition = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IddFieldDefinition("A1", -1, IddFieldKind.Alpha, "Name")).GetType().Name;

        return new[]
        {
            "public.type=" + typeof(IddFieldDefinition).FullName,
            "public.constructor.count=" + typeof(IddFieldDefinition).GetConstructors().Length,
            "token=" + field.Token,
            "position=" + field.Position,
            "kind=" + field.Kind,
            "name=" + field.Name,
            "notes=" + Join(field.Notes),
            "units=" + field.Units,
            "ip_units=" + field.IpUnits,
            "units_based_on=" + field.UnitsBasedOnField,
            "flags=" + Bool(field.IsRequired, field.BeginsExtensible, field.IsDeprecated, field.IsAutosizable, field.IsAutocalculatable, field.RetainsCase),
            "default=" + field.DefaultValue,
            "data_type=" + field.DataType,
            "choices=" + Join(field.Choices),
            "object_lists=" + Join(field.ObjectLists),
            "external_list=" + field.ExternalList,
            "references=" + Join(field.References),
            "reference_classes=" + Join(field.ReferenceClassNames),
            "minimum=0|inclusive=" + field.Minimum!.IsInclusive,
            "maximum=10|inclusive=" + field.Maximum!.IsInclusive,
            "additional=" + Directives(field.AdditionalDirectives),
            "collections.defensive=true",
            "collections.read_only=true",
            "empty_token=" + emptyToken,
            "negative_position=" + negativePosition,
        };
    }

    private static string[] ObserveFieldEquality()
    {
        IddFieldDefinition left = StandardField("A1", 0, "Name", "X");
        IddFieldDefinition equalShape = StandardField("A1", 0, "Name", "X");
        IddFieldDefinition different = StandardField("A1", 0, "Name", "Y");
        string leftSnapshot = FieldSnapshot(left);
        string equalSnapshot = FieldSnapshot(equalShape);
        Assert.Equal(leftSnapshot, equalSnapshot);
        Assert.NotEqual(leftSnapshot, FieldSnapshot(different));
        Assert.False(left.Equals(equalShape));
        Assert.True(ReferenceEquals(left, left));
        MethodInfo equals = Assert.IsAssignableFrom<MethodInfo>(
            typeof(IddFieldDefinition).GetMethod(nameof(object.Equals), new[] { typeof(object) }));
        Assert.Equal(typeof(object), equals.DeclaringType);
        return new[]
        {
            "reference_identity.self=true",
            "reference_identity.equal_shape=false",
            "equals.declaring_type=" + equals.DeclaringType!.FullName,
            "structural.equal=true",
            "structural.different_default=true",
            "left=" + leftSnapshot,
            "different=" + FieldSnapshot(different),
        };
    }

    private static string[] ObserveFieldParser()
    {
        const string text = """
            !IDD_Version 24.2.0
            !IDD_BUILD abc123
            \group Test Group
            Test:Object,
              A1, \field Mode
                  \note first note
                  \required-field
                  \type choice
                  \retaincase
                  \key On
                  \key Off
                  \object-list ObjectNames
                  \external-list ExternalNames
                  \reference ReferenceNames
                  \reference-class-name ReferenceClasses
                  \default 1.25
                  \mystery retained
              N1, \field Size
                  \units m
                  \ip-units ft
                  \unitsBasedOnField A2
                  \minimum> 0
                  \maximum 10
                  \autosizable
              A2; \field Tail
                  \begin-extensible
                  \autocalculatable
                  \deprecated
            """;
        IddSchema schema = IddParser.Parse(text, NativeHash);
        IddObjectDefinition item = Assert.Single(schema.Objects);
        IddFieldDefinition mode = item["mode"];
        IddFieldDefinition size = item["SIZE"];
        IddFieldDefinition tail = item["Tail"];
        Assert.Equal("24.2.0", schema.Version);
        Assert.Equal("abc123", schema.Build);
        Assert.Equal(IddDataType.Choice, mode.DataType);
        Assert.Equal(new[] { "On", "Off" }, mode.Choices);
        Assert.Equal("1.25", mode.DefaultValue);
        Assert.False(size.Minimum!.IsInclusive);
        Assert.True(size.Maximum!.IsInclusive);
        Assert.Equal("retained", Assert.Single(mode.AdditionalDirectives["mystery"]));
        Assert.True(tail.BeginsExtensible);
        return new[]
        {
            "schema.version=" + schema.Version,
            "schema.build=" + schema.Build,
            "schema.source=" + schema.SourceSha256,
            "object.count=" + schema.Objects.Count,
            "field.count=" + item.Fields.Count,
            "mode.name=" + mode.Name,
            "mode.notes=" + Join(mode.Notes),
            "mode.required=" + mode.IsRequired,
            "mode.retain_case=" + mode.RetainsCase,
            "mode.type=" + mode.DataType,
            "mode.choices=" + Join(mode.Choices),
            "mode.object_lists=" + Join(mode.ObjectLists),
            "mode.external=" + mode.ExternalList,
            "mode.references=" + Join(mode.References),
            "mode.reference_classes=" + Join(mode.ReferenceClassNames),
            "mode.default_string=" + mode.DefaultValue,
            "mode.additional=" + Directives(mode.AdditionalDirectives),
            "size.units=" + size.Units,
            "size.ip_units=" + size.IpUnits,
            "size.units_based_on=" + size.UnitsBasedOnField,
            "size.minimum=0|inclusive=" + size.Minimum.IsInclusive,
            "size.maximum=10|inclusive=" + size.Maximum.IsInclusive,
            "size.autosizable=" + size.IsAutosizable,
            "tail.flags=" + Bool(tail.BeginsExtensible, tail.IsAutocalculatable, tail.IsDeprecated),
            "lookup.case_insensitive=true",
        };
    }

    private static string[] ObserveFieldProperties()
    {
        IddFieldDefinition field = StandardField("N1", 0, "Flow", "Autosize");
        var objectDefinition = new IddObjectDefinition("Thing", "Group", new[] { field });
        var schema = new IddSchema("24.2.0", "build", NativeHash, new[] { objectDefinition });
        int reverseReferenceCount = schema.Objects
            .SelectMany(item => item.Fields)
            .Count(item => item.References.Contains("ReferenceNames", StringComparer.Ordinal));
        Assert.Equal(1, reverseReferenceCount);
        Assert.Equal(IddFieldKind.Numeric, field.Kind);
        Assert.Equal(IddDataType.Real, field.DataType);
        Assert.False(field.Minimum!.IsInclusive);
        Assert.True(field.Maximum!.IsInclusive);
        return new[]
        {
            "token=" + field.Token,
            "position=" + field.Position,
            "kind=" + field.Kind,
            "name=" + field.Name,
            "notes=" + Join(field.Notes),
            "units=" + field.Units,
            "ip_units=" + field.IpUnits,
            "units_based_on=" + N(field.UnitsBasedOnField),
            "required=" + field.IsRequired,
            "begins_extensible=" + field.BeginsExtensible,
            "deprecated=" + field.IsDeprecated,
            "autosizable=" + field.IsAutosizable,
            "autocalculatable=" + field.IsAutocalculatable,
            "retains_case=" + field.RetainsCase,
            "default_string=" + field.DefaultValue,
            "data_type=" + field.DataType,
            "choices=" + Join(field.Choices),
            "object_lists=" + Join(field.ObjectLists),
            "external_list=" + field.ExternalList,
            "references=" + Join(field.References),
            "reference_classes=" + Join(field.ReferenceClassNames),
            "minimum=" + field.Minimum.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "minimum.inclusive=" + field.Minimum.IsInclusive,
            "maximum=" + field.Maximum.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "maximum.inclusive=" + field.Maximum.IsInclusive,
            "additional=" + Directives(field.AdditionalDirectives),
            "reverse_reference_projection.count=" + reverseReferenceCount,
        };
    }

    private static string[] ObserveObjectConstruction()
    {
        IddFieldDefinition name = StandardField("A1", 0, "Name", "Unnamed");
        IddFieldDefinition size = StandardField("N1", 1, "Size", "1.5");
        var sourceFields = new List<IddFieldDefinition> { name, size };
        var definition = new IddObjectDefinition(
            " Test:Object ", " Test Group ", sourceFields, new[] { " Memo one ", "Memo two" },
            isUnique: true, isRequired: true, minimumFields: 2, extensibleGroupSize: 1,
            format: " vertices ", obsoleteMessage: " Superseded ",
            additionalDirectives: new Dictionary<string, IReadOnlyList<string>>
            {
                ["reference-class-name"] = new[] { "TestClasses" },
            });
        sourceFields.Clear();
        Assert.Equal(2, definition.Fields.Count);
        Assert.Equal("Test:Object", definition.Name);
        Assert.Equal("Test Group", definition.Group);
        Assert.Equal(1, definition.ExtensibleStartIndex);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<IddFieldDefinition>)definition.Fields).Add(name));

        IddFieldDefinition duplicateFirst = StandardField("A1", 0, "Duplicate", "first");
        IddFieldDefinition duplicateSecond = StandardField("A2", 1, "Duplicate", "second");
        var duplicate = new IddObjectDefinition(
            "Duplicate:Object", "Group", new[] { duplicateFirst, duplicateSecond });
        Assert.Equal(2, duplicate.Fields.Count);
        Assert.Same(duplicateFirst, duplicate["duplicate"]);
        string nonConsecutive = Assert.Throws<ArgumentException>(() =>
            new IddObjectDefinition("Broken", "Group", new[]
            {
                new IddFieldDefinition("A1", 1, IddFieldKind.Alpha, "Name"),
            })).GetType().Name;

        return new[]
        {
            "public.type=" + typeof(IddObjectDefinition).FullName,
            "public.constructor.count=" + typeof(IddObjectDefinition).GetConstructors().Length,
            "name=" + definition.Name,
            "group=" + definition.Group,
            "fields=" + string.Join(",", definition.Fields.Select(item => item.Token + ":" + item.Name)),
            "memo=" + Join(definition.Memo),
            "unique=" + definition.IsUnique,
            "required=" + definition.IsRequired,
            "minimum_fields=" + definition.MinimumFields,
            "extensible_group_size=" + definition.ExtensibleGroupSize,
            "extensible_start_index=" + definition.ExtensibleStartIndex,
            "format=" + definition.Format,
            "obsolete_message=" + definition.ObsoleteMessage,
            "additional=" + Directives(definition.AdditionalDirectives),
            "fields.defensive=true",
            "fields.read_only=true",
            "duplicate.fields=2",
            "duplicate.lookup=first",
            "non_consecutive=" + nonConsecutive,
        };
    }

    private static string[] ObserveObjectEquality()
    {
        IddObjectDefinition left = StandardObject("Thing", "X");
        IddObjectDefinition equalShape = StandardObject("Thing", "X");
        IddObjectDefinition different = StandardObject("Thing", "Y");
        string leftSnapshot = ObjectSnapshot(left);
        Assert.Equal(leftSnapshot, ObjectSnapshot(equalShape));
        Assert.NotEqual(leftSnapshot, ObjectSnapshot(different));
        Assert.False(left.Equals(equalShape));
        MethodInfo equals = Assert.IsAssignableFrom<MethodInfo>(
            typeof(IddObjectDefinition).GetMethod(nameof(object.Equals), new[] { typeof(object) }));
        Assert.Equal(typeof(object), equals.DeclaringType);
        return new[]
        {
            "reference_identity.self=true",
            "reference_identity.equal_shape=false",
            "equals.declaring_type=" + equals.DeclaringType!.FullName,
            "structural.equal=true",
            "structural.different_default=true",
            "left=" + leftSnapshot,
            "different=" + ObjectSnapshot(different),
        };
    }

    private static string[] ObserveObjectParser()
    {
        const string text = """
            !IDD_Version 24.2.0
            !IDD_BUILD abc123
            \group Test Group
            Test:Object,
              \memo First memo
              \memo Second memo
              \unique-object
              \required-object
              \min-fields 2
              \extensible:2
              \format vertices
              \obsolete Superseded by New:Object
              \reference-class-name TestClasses
              A1, \field Name
                  \required-field
                  \default Unnamed
              A2, \field Extensible Name
                  \begin-extensible
              N1; \field Extensible Value
                  \default 1.5
            """;
        IddSchema schema = IddParser.Parse(text, NativeHash);
        IddObjectDefinition item = Assert.Single(schema.Objects);
        Assert.Equal("Test:Object", item.Name);
        Assert.Equal("Test Group", item.Group);
        Assert.Equal(new[] { "First memo", "Second memo" }, item.Memo);
        Assert.True(item.IsUnique);
        Assert.True(item.IsRequired);
        Assert.Equal(2, item.MinimumFields);
        Assert.Equal(2, item.ExtensibleGroupSize);
        Assert.Equal(1, item.ExtensibleStartIndex);
        Assert.Equal("Superseded by New:Object", item.ObsoleteMessage);
        Assert.Equal("TestClasses", Assert.Single(item.AdditionalDirectives["reference-class-name"]));
        Assert.Equal(new[] { "A1", "A2", "N1" }, item.Fields.Select(field => field.Token));
        return new[]
        {
            "schema.version=" + schema.Version,
            "schema.build=" + schema.Build,
            "schema.groups=" + Join(schema.Groups),
            "object.name=" + item.Name,
            "object.group=" + item.Group,
            "object.memo=" + Join(item.Memo),
            "object.unique=" + item.IsUnique,
            "object.required=" + item.IsRequired,
            "object.minimum_fields=" + item.MinimumFields,
            "object.extensible_group_size=" + item.ExtensibleGroupSize,
            "object.extensible_start_index=" + item.ExtensibleStartIndex,
            "object.format=" + item.Format,
            "object.obsolete_message=" + item.ObsoleteMessage,
            "object.additional=" + Directives(item.AdditionalDirectives),
            "object.tokens=" + string.Join(",", item.Fields.Select(field => field.Token)),
            "object.defaults=" + string.Join(",", item.Fields.Select(field => N(field.DefaultValue))),
            "object.required_fields=" + string.Join(",", item.Fields.Where(field => field.IsRequired).Select(field => field.Name)),
            "lookup.case_insensitive=" + ReferenceEquals(item, schema["test:object"]),
        };
    }

    private static string[] ObserveObjectProperties()
    {
        var fields = new[]
        {
            new IddFieldDefinition("A1", 0, IddFieldKind.Alpha, "Name", isRequired: true, defaultValue: "Unnamed"),
            new IddFieldDefinition("A2", 1, IddFieldKind.Alpha, "Vertex Name", beginsExtensible: true),
            new IddFieldDefinition("N1", 2, IddFieldKind.Numeric, "Vertex Value", defaultValue: "1.5", dataType: IddDataType.Real),
        };
        var item = new IddObjectDefinition(
            "Property:Object", "Geometry", fields, new[] { "Memo" },
            isUnique: true, isRequired: true, minimumFields: 1,
            extensibleGroupSize: 2, format: "vertices", obsoleteMessage: "Legacy",
            additionalDirectives: new Dictionary<string, IReadOnlyList<string>>
            {
                ["reference-class-name"] = new[] { "PropertyClasses" },
            });
        Assert.Equal(1, item.ExtensibleStartIndex);
        Assert.Same(fields[1], item.ResolveField(3));
        Assert.Same(fields[2], item.ResolveField(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => item.ResolveField(-1));
        var nonExtensible = new IddObjectDefinition("Plain", "Group", new[] { fields[0] });
        Assert.Null(nonExtensible.ResolveField(2));
        return new[]
        {
            "name=" + item.Name,
            "group=" + item.Group,
            "field_count=" + item.Fields.Count,
            "field_tokens=" + string.Join(",", item.Fields.Select(field => field.Token)),
            "field_defaults=" + string.Join(",", item.Fields.Select(field => N(field.DefaultValue))),
            "required_fields=" + string.Join(",", item.Fields.Where(field => field.IsRequired).Select(field => field.Name)),
            "memo=" + Join(item.Memo),
            "unique=" + item.IsUnique,
            "required=" + item.IsRequired,
            "minimum_fields=" + item.MinimumFields,
            "extensible_group_size=" + item.ExtensibleGroupSize,
            "extensible_start_index=" + item.ExtensibleStartIndex,
            "format=" + item.Format,
            "obsolete_message=" + item.ObsoleteMessage,
            "additional=" + Directives(item.AdditionalDirectives),
            "index.int=" + item[0].Name,
            "index.name=" + item["vertex name"].Token,
            "try_get.hit=" + item.TryGetField("VERTEX VALUE", out IddFieldDefinition? found),
            "try_get.value=" + found!.Token,
            "resolve.3=" + item.ResolveField(3)!.Token,
            "resolve.4=" + item.ResolveField(4)!.Token,
            "resolve.non_extensible=null",
            "resolve.negative=ArgumentOutOfRangeException",
        };
    }

    private static IddFieldDefinition StandardField(
        string token,
        int position,
        string name,
        string defaultValue) => new(
            token, position, token.StartsWith('N') ? IddFieldKind.Numeric : IddFieldKind.Alpha,
            name, new[] { "Note" }, "m", "ft", null,
            isRequired: true, beginsExtensible: false, isDeprecated: true,
            isAutosizable: true, isAutocalculatable: true, retainsCase: true,
            defaultValue: defaultValue,
            dataType: token.StartsWith('N') ? IddDataType.Real : IddDataType.Choice,
            choices: new[] { "On", "Off" }, objectLists: new[] { "ObjectNames" },
            externalList: "ExternalNames", references: new[] { "ReferenceNames" },
            referenceClassNames: new[] { "ReferenceClasses" },
            minimum: new IddNumericBound(0, false), maximum: new IddNumericBound(10, true),
            additionalDirectives: new Dictionary<string, IReadOnlyList<string>>
            {
                ["custom"] = new[] { "value" },
            });

    private static IddObjectDefinition StandardObject(string name, string defaultValue) => new(
        name,
        "Group",
        new[] { StandardField("A1", 0, "Name", defaultValue) },
        new[] { "Memo" },
        isUnique: true,
        isRequired: true,
        minimumFields: 1,
        extensibleGroupSize: 0,
        format: "singleLine",
        obsoleteMessage: "Legacy",
        additionalDirectives: new Dictionary<string, IReadOnlyList<string>>
        {
            ["custom"] = new[] { "value" },
        });

    private static string FieldSnapshot(IddFieldDefinition value) => string.Join("|", new[]
    {
        value.Token,
        value.Position.ToString(System.Globalization.CultureInfo.InvariantCulture),
        value.Kind.ToString(),
        value.Name,
        Join(value.Notes),
        N(value.Units),
        N(value.IpUnits),
        N(value.UnitsBasedOnField),
        Bool(value.IsRequired, value.BeginsExtensible, value.IsDeprecated, value.IsAutosizable, value.IsAutocalculatable, value.RetainsCase),
        N(value.DefaultValue),
        value.DataType.ToString(),
        Join(value.Choices),
        Join(value.ObjectLists),
        N(value.ExternalList),
        Join(value.References),
        Join(value.ReferenceClassNames),
        value.Minimum is null ? "null" : value.Minimum.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + value.Minimum.IsInclusive,
        value.Maximum is null ? "null" : value.Maximum.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + value.Maximum.IsInclusive,
        Directives(value.AdditionalDirectives),
    });

    private static string ObjectSnapshot(IddObjectDefinition value) => string.Join("|", new[]
    {
        value.Name,
        value.Group,
        string.Join(";", value.Fields.Select(FieldSnapshot)),
        Join(value.Memo),
        Bool(value.IsUnique, value.IsRequired),
        value.MinimumFields.ToString(System.Globalization.CultureInfo.InvariantCulture),
        value.ExtensibleGroupSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
        value.ExtensibleStartIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null",
        N(value.Format),
        N(value.ObsoleteMessage),
        Directives(value.AdditionalDirectives),
    });

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
                native_sources = ProductionSources.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
                public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
                python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
                reviewed_native_artifacts = ReviewedNativeArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
                support = FullIddSupportArtifacts.Select(item => Artifact(item.Path, item.Bytes, item.Sha256)).ToArray(),
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
                    native_outcome = target.Classification == "equivalent"
                        ? "public-native-equivalent-as-pinned"
                        : "public-native-adaptation-as-pinned",
                    python_case_sha256 = fixtureCase.CaseSha256,
                    python_facts_sha256 = fixtureCase.PythonFactsSha256,
                },
            },
            scope = new
            {
                active_energyplus_process_claim = false,
                deferred_target_count = 65,
                equivalent_target_count = 18,
                exact_case_count = 8,
                exact_source_declaration_count = 133,
                exact_target_count = 40,
                exception_target_count = 22,
                fixture_repository_commit = FixtureRepositoryCommit,
                full_energyplus_idd_support_hash_pinned = true,
                internal_native_route_claimed = false,
                public_production_api_only = true,
                python_api_compatibility_claimed = false,
                python_source_compatibility_claimed = false,
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
        Assert.False(scope.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.False(scope.GetProperty("internal_native_route_claimed").GetBoolean());
        Assert.True(scope.GetProperty("public_production_api_only").GetBoolean());
        Assert.False(scope.GetProperty("python_api_compatibility_claimed").GetBoolean());
        Assert.False(scope.GetProperty("python_source_compatibility_claimed").GetBoolean());
        Assert.False(scope.GetProperty("structural_only").GetBoolean());
        Assert.Equal(40, scope.GetProperty("exact_target_count").GetInt32());
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

    private static string Join(IEnumerable<string> values) => string.Join(",", values);

    private static string Bool(params bool[] values) => string.Join(",", values.Select(value => value.ToString()));

    private static string N(string? value) => value ?? "<null>";

    private static string Directives(IReadOnlyDictionary<string, IReadOnlyList<string>> value) => string.Join(
        ";",
        value.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Key + "=" + Join(item.Value)));

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
            string[] names = value.EnumerateObject().Select(item => item.Name).ToArray();
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
        string raw = value.GetRawText();
        Assert.DoesNotContain(@"C:\Users\", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Program Files\", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/home/", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("/tmp/", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x1234abcd", raw, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record CaseBinding(
        string Code,
        string CaseId,
        string Subfamily,
        string CaseSha256,
        string PythonFactsSha256,
        string[] TargetSymbols);

    private sealed record ExpectedTarget(
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

    private sealed record OracleCorpus(JsonElement[] FixtureCases, TargetBinding[] Targets);

    private sealed record NativeObservation(
        string Code,
        string CaseId,
        string[] Facts,
        string FactsSha256);

    private sealed record NativePin(string Code, int FactCount, string FactsSha256);
}
