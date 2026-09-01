#pragma warning disable CA1861 // Closed oracle expectations are intentionally auditable in place.

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.InvisibleDragon.Idd;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Idd;

public sealed class ImugiIddSchemaStaticCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/imugi-idd-schema-static-core-oracle.json";
    private const int FixtureBytes = 124_609;
    private const string FixtureSha256 =
        "sha256:93a074d69a9cc386a5898a3af5ed5580b05d523300073fe0fb6c0d93cd29a4ac";
    private const string FixtureSchema =
        "dragons.python-reference.imugi-idd-schema-static-core.v1";
    private const string FixtureRepositoryCommit = "2fa8cf5";
    private const string CasesSha256 =
        "sha256:bb7a6f135116803da606049843a114d3ba3647ce4d0c6a63f144ab559bd821af";

    private const string GeneratorPath =
        "tools/python-reference/generate_imugi_idd_schema_static_core_oracle.py";
    private const int GeneratorBytes = 50_536;
    private const string GeneratorSha256 =
        "sha256:9ad86909322e70b861f49640174b1f98fe9e0642433ea4bfe9b5ec0f33ffdd3e";
    private const string ValidatorPath =
        "tests/PythonReference/test_imugi_idd_schema_static_core_oracle.py";
    private const int ValidatorBytes = 22_677;
    private const string ValidatorSha256 =
        "sha256:0bfd957baff75de2fa70302f3c0577a09e74633fa076d2b467d18c398551c23b";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";

    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/imugi.py";
    private const int UpstreamBytes = 91_815;
    private const string UpstreamSourceSha256 =
        "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613";
    private const string UpstreamAstSha256 =
        "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90";
    private const string TargetReceiptsSha256 =
        "sha256:8ba1afe1d26824fe0def879330816229feb65f9bf158e2fbc24072ae61ad6727";
    private const string Batch1ReceiptsSha256 =
        "sha256:cea1bdce699efee3b7f152d932f8dd1b52affe0ad139b642e3be2371446e5223";
    private const string DeferredReceiptsSha256 =
        "sha256:e0f9739effa5d9ffafa3d1bec19fa57c338d8c76a2d730ba5833edb6401c7e1c";
    private const string OutOfScopeReceiptsSha256 =
        "sha256:3ad4f99816b0591241fe459bd60a0af70f9a40e497be34bab7b132ced2fe42da";
    private const string DependenciesSha256 =
        "sha256:f69d29212b5ce6432b0c02f356d036275ea01463a8e1974ac6f89b78854fefba";
    private const string RuntimeSignaturesSha256 =
        "sha256:6e6524357de9edd851713567c1d62da167fa0b666187e73ba731ead98342e091";
    private const string LoadedModulesSha256 =
        "sha256:b38033bf44c4359f5ee8cf44f8a12b2b267a2f4ddf83a25f0a13b5628b20f692";
    private const string RelocatedObservationsSha256 =
        "sha256:89b8c44c53fb90ecf4ae781d3cae69a37a3301277f933c0a65d3525130540166";
    private const string NativeClassificationSha256 =
        "sha256:a3868741b9dee71148cf9b6671485025834679190ee1c81fc100386429fb1598";
    private const string NativeRoutesSha256 =
        "sha256:121de72c35f8f5ab70923cea76d05e18b58c56688381aed11f0b33c0b013f724";
    private const string NativeSourceReceiptsSha256 =
        "sha256:737126276dad845aaeedcc82275c4655c6aeda07a06153880ab7b96a258edf54";
    private const string FullIddIdentitySha256 =
        "sha256:8225b83bdf960137d81363da69b81acd639b309eb394e845648dd041c3cff8f0";

    private const string BaseGeneratorPath =
        "tools/python-reference/generate_imugi_idd_definitions_core_oracle.py";
    private const int BaseGeneratorBytes = 70_938;
    private const string BaseGeneratorSha256 =
        "sha256:6b69716bca218db814bc1eb2411e19f1d9614cb5857f70e93e461e5c95fb1c0e";
    private const string FullIddGeneratorPath =
        "tools/python-reference/generate_idd_schema_oracle.py";
    private const int FullIddGeneratorBytes = 38_631;
    private const string FullIddGeneratorSha256 =
        "sha256:29287f01c865d01c67bb25f1cb3e6d3f1466bed7859379342d7276124cf4cfc7";
    private const string FullIddFixturePath =
        "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz";
    private const int FullIddFixtureBytes = 585_481;
    private const string FullIddFixtureSha256 =
        "sha256:75f9d6c2efa32349704489aae4622b8647ac07f542e61cf3130624786436fa26";
    private const string EnergyPlusIddSha256 =
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2";
    private const string NativeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Idd.ImugiIddSchemaStaticCoreOracleParityTests.MatchesPinnedImugiIddSchemaStaticSemanticsThroughPublicProductionApis";

    private static readonly ArtifactPin[] ReviewedNativeArtifacts =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddDefinitions.cs", 12_999,
            "sha256:b6be5a2ac41a05f519d8103a816d90a0153fe21d64916671ff430c964c516f66"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddParser.cs", 19_954,
            "sha256:555b79f49740c1da4149002b9cb8e4507ea806eac10866098057f040e4fc55b3"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddSchemaCache.cs", 11_242,
            "sha256:55ddd0da5501f24296b36c2ae6c31fc52e8a50832e3ffc8f783849e51b6af3c7"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Common/EnergyPlusVersion.cs", 4_951,
            "sha256:e28760c5903fa7c4e842620a7ba91c15947eb3378812a72262041af4397bd5a1"),
        new("tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Idd/IddParserTests.cs", 8_330,
            "sha256:1d58b2af26801c1359f022f9498c2e9109f7e0917eb74001f3a2a6c4ac0d1fad"),
        new("tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Idd/IddSchemaOracleTests.cs", 16_839,
            "sha256:9111d5732c096495edbbae830df3bb04d0d2373a54e36270ae402658f47f63c7"),
    };

    private static readonly ArtifactPin[] ProductionSources = ReviewedNativeArtifacts.Take(4).ToArray();

    private static readonly ArtifactPin[] FullIddSupportArtifacts =
    {
        new(BaseGeneratorPath, BaseGeneratorBytes, BaseGeneratorSha256),
        new(FullIddGeneratorPath, FullIddGeneratorBytes, FullIddGeneratorSha256),
        new(FullIddFixturePath, FullIddFixtureBytes, FullIddFixtureSha256),
    };

    private static readonly CaseBinding[] Cases =
    {
        new("A01", "imugi-idd-schema-static-core.a-exception-types", "exception-types",
            "sha256:fc066cfba1bdee780c09f706abcf65aaa144091d37f9d06cf4a3f52fd5dd2829",
            "sha256:c63c81fd17ded5a68ac3854944aa1350dbbfc72da3f1d2dc15e8da87c4e2ae0b",
            new[] { "InvalidFieldValue", "InvalidParentManagement", "VersionIdentificationError" }),
        new("B01", "imugi-idd-schema-static-core.b-static-construction", "static-construction",
            "sha256:76b6813bb3043fd424f339d53a8cc282beb0822ca19d2b263c5c76cb14f5330b",
            "sha256:2f6eacc4845b2167b483323ac3b79fbb700470e1076bce6412d17f34f5dc6c91",
            new[] { "StaticIndexedDict", "StaticIndexedDict.__init__", "StaticIndexedDict.allowed_keys" }),
        new("C01", "imugi-idd-schema-static-core.c-static-index-read", "static-index-read",
            "sha256:a38a3f22e3e6eaef4ee29b6e36c465fa1e56f5f75a82c762d128c998104ec2c7",
            "sha256:0f36f0ddda42f4f16f42c3e66dfee804c43dace739350252f7b8d908661bff03",
            new[] { "StaticIndexedDict.__getitem__" }),
        new("D01", "imugi-idd-schema-static-core.d-static-index-write", "static-index-write",
            "sha256:6d6bf9f6a30d1b49305d4f1803db809c35ee3977d9492df658388b17f95450ea",
            "sha256:56a01263bb07d2cdf36f448f1d7f06c57cf3309614248cc09e7ca888751c1280",
            new[] { "StaticIndexedDict.__setitem__" }),
        new("E01", "imugi-idd-schema-static-core.e-static-views", "static-views",
            "sha256:a18061c3296ee6b2b1766cb01e9e6fa9f9be3ff8ea2c6be8cd9849bdbe1496f4",
            "sha256:608cade9ddc207cc3dd6e3beb201e548b36fc1b6c82b33bdf84a49072ce4db0d",
            new[] { "StaticIndexedDict.items", "StaticIndexedDict.keys", "StaticIndexedDict.values" }),
        new("F01", "imugi-idd-schema-static-core.f-idd-construction-and-maps", "idd-construction-and-maps",
            "sha256:b01247d21c80cf48840e0d3e5056f1de445d3ed237e7579de1fca90d1f34498c",
            "sha256:d0470082dd01ad14251cbb80d511398ecb2893df1863077d75c96252e54b7e7c",
            new[] { "IDD", "IDD.__init__", "IDD.reference_map_cls", "IDD.reference_map_obj", "IDD.referenced_map_obj", "IDD.required_objects", "IDD.version" }),
        new("G01", "imugi-idd-schema-static-core.g-idd-read", "idd-read",
            "sha256:cfc1c957ba542371308e820d7e678410c8ddc91c7c5919086abbcd69f4cb3752",
            "sha256:e6bd97600be399f9a5730f2a47ce1e12a683153e7f580cece54df79d087ea63a",
            new[] { "IDD.read_idd" }),
        new("H01", "imugi-idd-schema-static-core.h-idd-cache-roundtrip", "idd-cache",
            "sha256:7a1bd5bd7109155fa45b37e3e8cdd23cc21f0ede4c162245b159aa625707c391",
            "sha256:80e7e21ee555890e28600b9ce811ddca07d3e3dddd5763549d6643b1a8871a22",
            new[] { "IDD.load", "IDD.to_pickle" }),
    };

    private static readonly ExpectedTarget[] ExpectedTargets =
    {
        new(1095, "IDD", "class", "sha256:394fdc55f48dd088d8aca9e08ad1551904556297b21f3df006fd2ff60380300b", "sha256:e3a7ddb9d9d51d260a53f73b3da77a58feb27d162ca1575851aa25958c784946", "sha256:a331715b6d5f64193dad7fd49acbab373f1d1310d4cbe9d9e30759bdab31efd3", "imugi-idd-schema-static-core-1095-394fdc55", "exception", "typed-immutable-idd-schema-instead-of-mutable-user-dictionary", "Dragons.InvisibleDragon.Idd.IddSchema", "imugi-idd-schema-static-core.f-idd-construction-and-maps"),
        new(1097, "IDD.__init__", "function", "sha256:369f30e0e83a3ce8b91e87e703ef1c78ec5eb0c05dd96b6a27e3a5db851f6095", "sha256:cbb2586573fd54b50d5a2684c80f1099742ecbd1a021db3a03a849351dca3c67", "sha256:6f11b7dc550e0383b1114046951a734d8b69669d8f83e14da8c0727184b79203", "imugi-idd-schema-static-core-1097-369f30e0", "exception", "validated-immutable-schema-construction-with-explicit-source-identity", "Dragons.InvisibleDragon.Idd.IddSchema(...) constructor", "imugi-idd-schema-static-core.f-idd-construction-and-maps"),
        new(1100, "IDD.load", "function", "sha256:3b6538ba505d18de08c5ff0ee5b2c49557f19e065cdea220fba5ed5643183144", "sha256:86a2873354f7cdb7fb7f6753ce26828fe749852ccebc56402590ae7c97506396", "sha256:69174cbf0340293fba21917e167e235a6840d620b71c88459a38439fc261e4cd", "imugi-idd-schema-static-core-1100-3b6538ba", "exception", "source-hash-bound-json-gzip-cache-instead-of-global-pickle-cache", "Dragons.InvisibleDragon.Idd.IddSchemaCache.Read/TryRead", "imugi-idd-schema-static-core.h-idd-cache-roundtrip"),
        new(1101, "IDD.read_idd", "function", "sha256:0b48b62a5aea8fb7598aa5724c8cf90046677d7e016952c9aafccda740f8024f", "sha256:8ed5c1b4e5e30bb2cc3668cb9411035f7b1775d5ae53fd2525e6e02f6b514837", "sha256:eb4917abc62d60472f3949d18b32cd9daef3dbe0c81c7962cc52b6bc9a5f93fd", "imugi-idd-schema-static-core-1101-0b48b62a", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddParser.ParseFile", "imugi-idd-schema-static-core.g-idd-read"),
        new(1102, "IDD.reference_map_cls", "function", "sha256:1772eedce3236a508206b04d6cacb474b09446e346510798350b636738777b50", "sha256:1dccc6a04e28476cc77c415315d361e8f0b8a70ed13f673ccaeca2bf23654b3c", "sha256:7a29cb6f28c133581ad9682dc2767cb4bb82cb79565c652c0a0fc17ecfbf8090", "imugi-idd-schema-static-core-1102-1772eedc", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Objects/Fields/ReferenceClassNames projection", "imugi-idd-schema-static-core.f-idd-construction-and-maps"),
        new(1103, "IDD.reference_map_obj", "function", "sha256:9cf45886e630dd02f7a96ca3db1bf5eb1ea411487771944396cf6df09ed1cba1", "sha256:f4544b82376ce5a9e9e03a53ffa4dcf14ac6234608672b2d6e3b37cfeb23fb88", "sha256:9069a0b6720c699f76cd94ac246c18a1b5d33e61e0ff9d6d5ee94b42d0f6bb72", "imugi-idd-schema-static-core-1103-9cf45886", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Objects/Fields/References projection", "imugi-idd-schema-static-core.f-idd-construction-and-maps"),
        new(1104, "IDD.referenced_map_obj", "function", "sha256:ae43213f9ec6c540cdcaac73160055cff676abcc550bac3ba98edf453d327f88", "sha256:f4544b82376ce5a9e9e03a53ffa4dcf14ac6234608672b2d6e3b37cfeb23fb88", "sha256:85a840eac573caf4674c2e69a26332209b7e5ca3b0b97f4ca41619febfb32b80", "imugi-idd-schema-static-core-1104-ae43213f", "exception", "explicit-public-schema-projection-instead-of-absent-legacy-private-state", "Dragons.InvisibleDragon.Idd.IddSchema.Objects/Fields/ObjectLists projection", "imugi-idd-schema-static-core.f-idd-construction-and-maps"),
        new(1105, "IDD.required_objects", "function", "sha256:3bdf9b9f1261b152ae1ba91e42e96844a2e5768922e4978f7048296247fcef2a", "sha256:3600cccc11bc6800f262c4e5f0aacb4e7f2bf7ca486cbc455c0376a25e228afd", "sha256:44a2634873cfa4d4cd63e1432ebfab176e6a6ec4775b8d356992eeff225877ec", "imugi-idd-schema-static-core-1105-3bdf9b9f", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Objects projection over IsRequired", "imugi-idd-schema-static-core.f-idd-construction-and-maps"),
        new(1106, "IDD.to_pickle", "function", "sha256:78d65e39461eccc962e7389e5e0dccfa98f75268c225e280ecf3d02dc4e9581b", "sha256:9ec615141a0af53bb8a3d5abd9d7f5e4bcc5db7a96f678df2eb0546ed4fa1138", "sha256:3cbf66831935570d30aa016c7807b261008cdff6f01f6fa1fdbc3756cfd6cae5", "imugi-idd-schema-static-core-1106-78d65e39", "exception", "portable-json-gzip-cache-instead-of-arbitrary-python-pickle", "Dragons.InvisibleDragon.Idd.IddSchemaCache.Write", "imugi-idd-schema-static-core.h-idd-cache-roundtrip"),
        new(1107, "IDD.version", "function", "sha256:648c3654122ab1f1e2bb8a23bcf79105c8067f09b687259cdec9bf9319f06dcc", "sha256:f744c3c6a5f0aa81439964037cebfd8aee40690f9359b705f202bc864e925682", "sha256:35096658d33a57b9b7d4ecd346d99d4aec2d4646673a1f73ec476d97c1c42e72", "imugi-idd-schema-static-core-1107-648c3654", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Version", "imugi-idd-schema-static-core.f-idd-construction-and-maps"),
        new(1217, "InvalidFieldValue", "class", "sha256:73314d435f9b36d7d5b9e9904f7127db38fe6354c42217038b5242590d3749a9", "sha256:e633c5403d4cdb79e61dce1629bcde18dfa53570d02f9b7ad45c31c2114718e7", "sha256:921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a", "imugi-idd-schema-static-core-1217-73314d43", "exception", "standard-public-argument-and-format-exceptions-instead-of-legacy-marker-type", "Dragons.InvisibleDragon.Idd.IddFieldDefinition/IddObjectDefinition public validation", "imugi-idd-schema-static-core.a-exception-types"),
        new(1218, "InvalidParentManagement", "class", "sha256:a3c45a4a197c1131f740fe8786f9c5196d33c0c7c340fb4c241b610d0280899d", "sha256:781b5de3e6da60e85defcef95f184775186e1e27b85b6b393926023e185f48d4", "sha256:921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a", "imugi-idd-schema-static-core-1218-a3c45a4a", "exception", "immutable-definition-ownership-instead-of-parent-mutation-exception", "Dragons.InvisibleDragon.Idd.IddObjectDefinition immutable public ownership", "imugi-idd-schema-static-core.a-exception-types"),
        new(1219, "StaticIndexedDict", "class", "sha256:04e0277ab2b2bf6821c79f1e32476574f2d69abb44eb963b333c3b8e15008b4f", "sha256:732b7f7b2d88fdc3c1c6318e3941448a0a9bbdd325e13fb079f66a19174d74ac", "sha256:7d60a85cc8e7c7a13212e6c06d6f45056a84410c52b50b70749684ce367d948c", "imugi-idd-schema-static-core-1219-04e0277a", "exception", "typed-immutable-schema-collections-instead-of-generic-mutable-user-dictionary", "Dragons.InvisibleDragon.Idd.IddSchema and IddObjectDefinition typed collections", "imugi-idd-schema-static-core.b-static-construction"),
        new(1220, "StaticIndexedDict.__getitem__", "function", "sha256:c84cef47bed071fab6ae9ce83de9226fc41c7cab2d9a10e48ae0bfa7afdacbea", "sha256:da13304658e257f8023fafa320a918db94505e8d5d429b7a75bd112f51b404c4", "sha256:07bc294eb49fc53e43c63deea7ec9da591c3987510100dbf6ba56e75990399b5", "imugi-idd-schema-static-core-1220-c84cef47", "exception", "typed-case-insensitive-indexers-with-conventional-boundary-semantics", "Dragons.InvisibleDragon.Idd.IddSchema.this[int|string]", "imugi-idd-schema-static-core.c-static-index-read"),
        new(1221, "StaticIndexedDict.__init__", "function", "sha256:303db42cbe721b5b6a7bee1cd59b9961901974c1606f0e93b3e0332b98606ed1", "sha256:7290ada1ad647abedbbab7401ebdbd2b5a0c73bf060820ccc2a66ba6543274c5", "sha256:84a75c344a4df7cbb2e51f06915b90f67cf1d5c6673d7b8096dab3b44eff3af4", "imugi-idd-schema-static-core-1221-303db42c", "exception", "typed-schema-constructors-instead-of-allowed-key-user-dictionary", "Dragons.InvisibleDragon.Idd.IddSchema(...) constructor", "imugi-idd-schema-static-core.b-static-construction"),
        new(1222, "StaticIndexedDict.__setitem__", "function", "sha256:6a048e255b2328e1a87b74cd4fa82691cdd1da1eb99794b914f08dbdb45d5e85", "sha256:e64f59838ea0f778cf272e5689976303984a205719ff42e0c42e10947bd476f0", "sha256:4e3c04eda95d774217a9674ea96eb6b33e734212b032c2ecca1a6b3f08e0a95a", "imugi-idd-schema-static-core-1222-6a048e25", "exception", "immutable-read-only-production-collections", "Dragons.InvisibleDragon.Idd.IddSchema.Objects read-only collection", "imugi-idd-schema-static-core.d-static-index-write"),
        new(1223, "StaticIndexedDict.allowed_keys", "function", "sha256:45e68e36a94154a7f798f2d291da1a73cf207234157d29af32684db1ab69c66a", "sha256:4e8513bcc96fbcc31dc0bc70e972edbc5e1fae6934b5c09da7efbe3cafa00567", "sha256:d28c1220e491af51bd7dc55ce16b3f3d1c20b4dd04b4446bb8a1669b78c16c47", "imugi-idd-schema-static-core-1223-45e68e36", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Objects projection over Name", "imugi-idd-schema-static-core.b-static-construction"),
        new(1224, "StaticIndexedDict.items", "function", "sha256:ce753c48af155d1faca3ce572419ee7ddd19f4b2639c5de5a8310ce285af3e09", "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b", "sha256:fbae7ca8a806047a8b75effbefb969a95dfa8f03af53ee90efd7cad68dbd3d72", "imugi-idd-schema-static-core-1224-ce753c48", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Objects key/value projection", "imugi-idd-schema-static-core.e-static-views"),
        new(1225, "StaticIndexedDict.keys", "function", "sha256:880aaf035f5baee6b6722fd235bd20e6c2d2e495ee8ed996b847df10971601fd", "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b", "sha256:44d9ffeb433ce74606587e37dbc241a048004b20c9f7fc29d0a5d85240f483ab", "imugi-idd-schema-static-core-1225-880aaf03", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Objects projection over Name", "imugi-idd-schema-static-core.e-static-views"),
        new(1226, "StaticIndexedDict.values", "function", "sha256:7487511d15e9cbeaf8b55041b02c9ff3412cadc5046ddff1bd3045e0baf42bdf", "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b", "sha256:5c17845beaf0249dbc49b1679503d5afd442451e61dd46940901fa76ed653c76", "imugi-idd-schema-static-core-1226-7487511d", "equivalent", "not_applicable", "Dragons.InvisibleDragon.Idd.IddSchema.Objects", "imugi-idd-schema-static-core.e-static-views"),
        new(1227, "VersionIdentificationError", "class", "sha256:47e8a463cedd83dabab7fd8f7b8a62fc0316a44ad539be2473a60630c76bf5df", "sha256:7b8f02b355969944367c547803b3baa75f93047d8f12572f4bd833cd43431700", "sha256:921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a", "imugi-idd-schema-static-core-1227-47e8a463", "exception", "empty-version-parser-result-instead-of-legacy-dedicated-exception", "Dragons.InvisibleDragon.Idd.IddParser.Parse/ParseFile version contract", "imugi-idd-schema-static-core.a-exception-types"),
    };

    // Set true only for one local discovery run, then freeze every emitted hash.
    private static bool DiscoverPins => false;

    private static readonly NativePin[] ExpectedNativePins =
    [
        new("A01", 12, "sha256:c8352103c45046368d834bab3bb9f66958eca807dc7404b8b5035638cdfe9326"),
        new("B01", 10, "sha256:9d917a8bc83e78f936af8819a1e77106bcd3d9faf3a4da4db2a8ff9968918e77"),
        new("C01", 11, "sha256:27ef18984372678ab3850ad3942b71ec0c7db29dda07e146811460d5e12d832e"),
        new("D01", 7, "sha256:4707186e46757ee1026ea52dd9a1fc0f6880bf8f85d7a911eb95f61831b9395d"),
        new("E01", 9, "sha256:7182d2477e6b6dc12fa1b1c67e11b9492b2eaf73be09f2019b502dc10b56c71e"),
        new("F01", 16, "sha256:6ecfc5e02fa1cf1f0e2042eca98e5c90ae03c9124f91d9dfb9b77faaa0bcabc6"),
        new("G01", 13, "sha256:fb9d3a3880086878cfdf30f6e5d62ecd749e9ed20096b4c58f82fad280f453c1"),
        new("H01", 15, "sha256:eb441eba19c45776cd22ca9b6163d2035234b7d1883ee3b83b85c8beb7ddced8"),
    ];

    private static readonly string[] ExpectedReceiptHashes =
    [
        "sha256:df82b90635de74acc4269e534ce32dc638621d46afa1760e79e10a9a23590ddf",
        "sha256:24bde64c7bba4882cb1b2d362c9e2c936827ae11272210bd68df5cae5603710f",
        "sha256:114fc39fe5d520b56664fdb5c1a27ad2cb5b9c87fcaf306d7cebadc5fb9cb79e",
        "sha256:d80c0327781eab0536564772bbf9af0b08d11359a4b7e3b72abd2946f95cc093",
        "sha256:7b14f685da2a27bdeb1b6aef9d910114a68762b9e8dd4a0a21b8e1f854e731a5",
        "sha256:ac90dbc2827147d5d4af16429d478910a01d464ab8d72ebc873c32e02fadf15a",
        "sha256:db21bbcca7e70a23a6b84410c4d95d3905a1cf28a8adece6d4e5094e48691d84",
        "sha256:5d1ddcffe32b031d3eda85d0dcc1867c3568f857449498a8c433cab180e41da1",
        "sha256:6ac785a2feb8a7bdc7689ecc5cb9aadf50c91d7178b44dedb58c8d1205ef1752",
        "sha256:90104c36a3a2437f58fd18acf50e63a1a897e8dc345c2d56e109f9cca8d50f9d",
        "sha256:e30624208c7ff8e6259de248a5cb3899f83549d5cbbce267c6f5ff5a87936426",
        "sha256:4260bbff9b614df5e64725bb060236bdc14a156a3f9515ef89bb22ee765d5979",
        "sha256:37133b69b1a2b606053c42094daf5327fe5212d643b802b35aa5173dcf8588d1",
        "sha256:6962cd3b1628d83b2260f6f6c2eb52da5f37d6988e3f5a6b0ddfb150e463f924",
        "sha256:dd665750a307b9e29dc9b3713d64d8aa9018ec33de8bc9a8dc4ca46ee5f2807c",
        "sha256:f221e33c98315596310711fc09e7bdde85cf473ecf52792f62122ebb4b3a382a",
        "sha256:a7a7c8994ae8af6a99781bb0685ebffc6cee05e1e4d37aebb6b2f90413543bc3",
        "sha256:b16827e1383d9407697753bc7745c7bf9fbd6814c93408615eb204e972d51584",
        "sha256:c1cede573fb9fb1093dc6eb491d04b44aa796165196d96da5f6a76714f088150",
        "sha256:5580df26fba7e5add927e1850fb7836a9d41e1c4260be481ce7465e79cb2092c",
        "sha256:4aa7742c56c77598d8fd0feb0b24a96c5bcfd01397808d055b54ca9eb286c061",
    ];


    private static readonly string[] ExpectedCollectorOutputHashes =
    [
        "sha256:c39618225f79bc592c9f10e259391fa89664651f9c19a9a9980c7500aea30983", // imugi-idd-schema-static-core-1095-394fdc55
        "sha256:0792a0330d308eed7d0c6834cf2d154e2933b40f564717e05a4886b8438e31fa", // imugi-idd-schema-static-core-1097-369f30e0
        "sha256:d4aad7ec2562ad8ab94a17d5b3d5bf428a773ecea91996fb6671180f61ce6367", // imugi-idd-schema-static-core-1100-3b6538ba
        "sha256:90fe90bf536555a675cf60f9f4959f37157198d64a8978e27e7f76965dfca8ca", // imugi-idd-schema-static-core-1101-0b48b62a
        "sha256:c35bc4b6731e9a09598556e0e8ffa120b2e6024b9f995d1593e9b8c48f8b8dc8", // imugi-idd-schema-static-core-1102-1772eedc
        "sha256:00ba9f2c51ad22ea8d50e32f2fed2afb1013dea3aaf8abbd10d4bc30f24e94ee", // imugi-idd-schema-static-core-1103-9cf45886
        "sha256:3b6c3b97eff113a6947467c3f82490d4cab7b7b42075259ec0589a2c776438d8", // imugi-idd-schema-static-core-1104-ae43213f
        "sha256:1885490d949aff01f17f636c25dba791b8658a03d316d6c90a9d001ad8974277", // imugi-idd-schema-static-core-1105-3bdf9b9f
        "sha256:6675cdcc0e2bf10506f855bebec5cf317de54c4cd23ff9a908458c6a02d9683c", // imugi-idd-schema-static-core-1106-78d65e39
        "sha256:b560f1c2d5d5e417c7b14362f003dafac4fbf2097a98565f574ed95b553a6fd5", // imugi-idd-schema-static-core-1107-648c3654
        "sha256:381348e0165abfccd8b56816f1723ffb0645cdcc8d163e25803fe75baa122c5a", // imugi-idd-schema-static-core-1217-73314d43
        "sha256:df8464c4a7e4e329b97c35be20492173838a24ab9a759015b19563b08bb7cc27", // imugi-idd-schema-static-core-1218-a3c45a4a
        "sha256:32028daf5d980f85466e3745b570c342692c1adea390004fc8e6b2091378c16c", // imugi-idd-schema-static-core-1219-04e0277a
        "sha256:4410662c5d7635ff2d8416f96229129f7bb3254bc164a2dd94f90dbbd168951f", // imugi-idd-schema-static-core-1220-c84cef47
        "sha256:9bf1387a507daec657df43bf60e77e42a3a89d314ca2577b90f711ffb1cf9704", // imugi-idd-schema-static-core-1221-303db42c
        "sha256:b651911e29ecce1cf64ae11d93d8bd2fcfd4ef8e88729520781e0b6fb353d59c", // imugi-idd-schema-static-core-1222-6a048e25
        "sha256:2b50f49cbb82b76831e220faac8caafae69f56b866c72cc72a331b02ca68fa6a", // imugi-idd-schema-static-core-1223-45e68e36
        "sha256:a8b7ef33de0dd81b061a8eae88b5d9904e74b332b1cbeaccfc1a194bce709213", // imugi-idd-schema-static-core-1224-ce753c48
        "sha256:bea4be8172742b7e7a223e99f0016b7e291c8b944934fc93492382cb2d397653", // imugi-idd-schema-static-core-1225-880aaf03
        "sha256:793273bea74e7360ef2589549a1213e30f6db77024d4989481742ee5a6b170c1", // imugi-idd-schema-static-core-1226-7487511d
        "sha256:e7398b8db3c4a963432ef9f4a6c19d58bf04600ab3aedd486a83da095e044e65", // imugi-idd-schema-static-core-1227-47e8a463
    ];

    [Fact]
    public void MatchesPinnedImugiIddSchemaStaticSemanticsThroughPublicProductionApis()
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
                "IMUGI_IDD_SCHEMA_STATIC_NATIVE_PINS" + Environment.NewLine +
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

        Assert.Equal(21, recordCount);
        Assert.Equal(21, corpus.Targets.Length);
        Assert.Equal(21, corpus.Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(9, corpus.Targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(12, corpus.Targets.Count(item => item.Classification == "exception"));
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

        ConstructorInfo schemaConstructor = Assert.Single(
            typeof(IddSchema).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        MethodInfo parseFile = Assert.IsAssignableFrom<MethodInfo>(typeof(IddParser).GetMethod(
            nameof(IddParser.ParseFile), BindingFlags.Public | BindingFlags.Static));
        Assert.True(schemaConstructor.IsPublic);
        Assert.True(parseFile.IsPublic);
        Assert.Equal(typeof(IddSchema), parseFile.ReturnType);
        Assert.True(typeof(IddSchema).IsPublic);
        Assert.True(typeof(IddParser).IsPublic);
        Assert.True(typeof(IddSchemaCache).IsPublic);
        Assert.Contains(typeof(IddSchemaCache).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == nameof(IddSchemaCache.Read));
        Assert.Contains(typeof(IddSchemaCache).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == nameof(IddSchemaCache.Write));
        Assert.NotNull(typeof(IddSchema).GetProperty("Item", new[] { typeof(int) }));
        Assert.NotNull(typeof(IddSchema).GetProperty("Item", new[] { typeof(string) }));
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
        Assert.Equal("dragons.energyplus-idd-schema.v1", RequiredString(root, "oracle_schema"));
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
            "batch1_resolved_receipts", "case_sha256", "cases", "cases_sha256",
            "consumer_contract", "deferred_receipts", "fact_sha256", "native_review",
            "out_of_scope_receipts", "runtime", "schema", "support", "symbols",
            "target_receipts", "upstream");
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
        Assert.Equal(Batch1ReceiptsSha256, RequiredString(partition, "batch1_resolved"));
        Assert.Equal(DeferredReceiptsSha256, RequiredString(partition, "deferred"));
        Assert.Equal(OutOfScopeReceiptsSha256, RequiredString(partition, "out_of_scope"));
        Assert.Equal(TargetReceiptsSha256, CanonicalSha256(root.GetProperty("target_receipts")));
        Assert.Equal(Batch1ReceiptsSha256, CanonicalSha256(root.GetProperty("batch1_resolved_receipts")));
        Assert.Equal(DeferredReceiptsSha256, CanonicalSha256(root.GetProperty("deferred_receipts")));
        Assert.Equal(OutOfScopeReceiptsSha256, CanonicalSha256(root.GetProperty("out_of_scope_receipts")));
        Assert.Equal(21, root.GetProperty("target_receipts").GetArrayLength());
        Assert.Equal(40, root.GetProperty("batch1_resolved_receipts").GetArrayLength());
        Assert.Equal(44, root.GetProperty("deferred_receipts").GetArrayLength());
        Assert.Equal(28, root.GetProperty("out_of_scope_receipts").GetArrayLength());
        ValidateFullPartition(root);
        JsonElement isolated = upstream.GetProperty("isolated_import");
        Assert.Equal(2, isolated.GetProperty("source_location_count").GetInt32());
        Assert.Equal("two-byte-identical-repository-temp-copies", RequiredString(isolated, "relocated_source_copy"));
        Assert.Equal(LoadedModulesSha256, RequiredString(isolated, "loaded_local_modules_sha256"));
        Assert.Equal(LoadedModulesSha256, CanonicalSha256(isolated.GetProperty("loaded_local_modules")));
        Assert.Equal(RelocatedObservationsSha256, RequiredString(isolated, "relocated_observations_sha256"));

        ValidateRuntimeReviewAndSupport(root);
        JsonElement contract = root.GetProperty("consumer_contract");
        ValidateContractClosure(contract);
        JsonElement[] actualTargets = root.GetProperty("target_receipts").EnumerateArray().ToArray();
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
            Assert.StartsWith("Dragons.InvisibleDragon.Idd.", expected.NativeRoute, StringComparison.Ordinal);
            Assert.DoesNotContain(".Internal", expected.NativeRoute, StringComparison.Ordinal);
            targets[index] = new TargetBinding(
                expected.InventoryIndex, expected.Symbol, expected.Kind, expected.SymbolHash,
                expected.SignatureHash, expected.BodyHash, expected.AssertionId,
                expected.Classification, expected.AdaptationId, expected.NativeRoute, expected.CaseId);
        }

        Assert.Equal(9, targets.Count(item => item.Classification == "equivalent"));
        Assert.Equal(12, targets.Count(item => item.Classification == "exception"));
        Assert.Equal(21, targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        return new OracleCorpus(fixtureCases, targets);
    }

    private static void ValidateFullPartition(JsonElement root)
    {
        string[] keys =
        {
            "target_receipts", "batch1_resolved_receipts",
            "deferred_receipts", "out_of_scope_receipts",
        };
        int[] indices = keys
            .SelectMany(key => root.GetProperty(key).EnumerateArray())
            .Select(item => item.GetProperty("inventory_index").GetInt32())
            .ToArray();
        Assert.Equal(133, indices.Length);
        Assert.Equal(133, indices.Distinct().Count());
        Assert.Equal(Enumerable.Range(1095, 133), indices.OrderBy(item => item));
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
        Assert.False(review.GetProperty("python_api_compatibility_claimed").GetBoolean());
        Assert.False(review.GetProperty("python_source_compatibility_claimed").GetBoolean());
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
        ValidateArtifact(support.GetProperty("base_generator"), FullIddSupportArtifacts[0]);
        JsonElement energyPlus = support.GetProperty("energyplus_idd");
        ValidateArtifact(energyPlus.GetProperty("generator"), FullIddSupportArtifacts[1]);
        ValidateArtifact(energyPlus.GetProperty("fixture"), FullIddSupportArtifacts[2]);
        Assert.Equal(FullIddIdentitySha256, RequiredString(energyPlus, "full_schema_identity_sha256"));
        Assert.Equal(FullIddIdentitySha256, CanonicalSha256(energyPlus.GetProperty("full_schema_identity")));
        JsonElement identity = energyPlus.GetProperty("full_schema_identity");
        Assert.Equal("dragons.energyplus-idd-schema.v1", RequiredString(identity, "oracle_schema"));
        Assert.Equal("24.2.0", RequiredString(identity, "energyplus_version"));
        Assert.Equal("94a887817b", RequiredString(identity, "energyplus_build"));
        Assert.Equal(848, identity.GetProperty("object_count").GetInt32());
        Assert.Equal(13_702, identity.GetProperty("field_count").GetInt32());
        Assert.Equal(EnergyPlusIddSha256, RequiredString(identity, "source_sha256"));
    }

    private static void ValidateContractClosure(JsonElement contract)
    {
        JsonElement counts = contract.GetProperty("classification_counts");
        Assert.Equal(9, counts.GetProperty("equivalent").GetInt32());
        Assert.Equal(12, counts.GetProperty("exception").GetInt32());
        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("exact_one_case_target_partition").GetBoolean());
        Assert.True(closure.GetProperty("full_imugi_source_partition").GetBoolean());
        Assert.True(closure.GetProperty("matrix_batch1_promotion_deferred").GetBoolean());
        Assert.Equal(133, closure.GetProperty("source_declaration_count").GetInt32());
        Assert.Equal(21, closure.GetProperty("target_count").GetInt32());
        Assert.Equal(40, closure.GetProperty("batch1_resolved_count").GetInt32());
        Assert.Equal(44, closure.GetProperty("deferred_count").GetInt32());
        Assert.Equal(28, closure.GetProperty("out_of_scope_count").GetInt32());
        JsonElement evidence = contract.GetProperty("evidence_contract");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.False(evidence.GetProperty("native_runtime_executed_by_python_oracle").GetBoolean());
        Assert.False(evidence.GetProperty("structural_only").GetBoolean());
        Assert.True(evidence.GetProperty("exact_cpython_behavior_oracle").GetBoolean());
        Assert.True(evidence.GetProperty("full_energyplus_idd_support_hash_pinned").GetBoolean());
        Assert.True(evidence.GetProperty("path_independent_relocated_import").GetBoolean());
        Assert.True(evidence.GetProperty("target_coverage_complete").GetBoolean());
        Assert.Equal(21, evidence.GetProperty("expected_receipt_count").GetInt32());
    }

    private static NativeObservation ObserveNativeCase(CaseBinding item)
    {
        string[] facts = item.Code switch
        {
            "A01" => ObserveExceptionAlternatives(),
            "B01" => ObserveTypedConstruction(),
            "C01" => ObserveTypedIndexRead(),
            "D01" => ObserveReadOnlyMutationContract(),
            "E01" => ObserveTypedViews(),
            "F01" => ObserveSchemaConstructionAndMaps(),
            "G01" => ObserveIddRead(),
            "H01" => ObserveSchemaCacheRoundtrip(),
            _ => throw new InvalidOperationException("Unknown native Imugi batch-2 case: " + item.Code),
        };
        Assert.NotEmpty(facts);
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        return new NativeObservation(
            item.Code, item.CaseId, facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveExceptionAlternatives()
    {
        string emptyField = Assert.Throws<ArgumentException>(() =>
            new IddFieldDefinition(" ", 0, IddFieldKind.Alpha, "Name")).GetType().Name;
        string negativePosition = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IddFieldDefinition("A1", -1, IddFieldKind.Alpha, "Name")).GetType().Name;
        string emptyObject = Assert.Throws<ArgumentException>(() =>
            new IddObjectDefinition(" ", "Group", Array.Empty<IddFieldDefinition>())).GetType().Name;
        string missingFields = Assert.Throws<ArgumentNullException>(() =>
            new IddObjectDefinition("Thing", "Group", null!)).GetType().Name;
        string fieldBeforeObject = Assert.Throws<FormatException>(() =>
            IddParser.Parse("A1; \\field Name", NativeHash)).GetType().Name;
        IddSchema noVersion = IddParser.Parse("\\group G\nThing,\n A1; \\field Name\n", NativeHash);
        Assert.Equal(string.Empty, noVersion.Version);
        return new[]
        {
            "field.empty_token=" + emptyField,
            "field.negative_position=" + negativePosition,
            "object.empty_name=" + emptyObject,
            "object.null_fields=" + missingFields,
            "parser.field_before_object=" + fieldBeforeObject,
            "parser.missing_version=<empty>",
            "public.field_exception=" + typeof(ArgumentException).FullName,
            "public.object_ownership=immutable",
            "public.parse_exception=" + typeof(FormatException).FullName,
            "legacy.invalid_field_marker_claimed=false",
            "legacy.invalid_parent_marker_claimed=false",
            "legacy.version_marker_exception_claimed=false",
        };
    }

    private static string[] ObserveTypedConstruction()
    {
        IddObjectDefinition first = Object("First:Object", "Group A", required: true);
        IddObjectDefinition second = Object("Second:Object", "Group B");
        var source = new List<IddObjectDefinition> { first, second };
        var schema = new IddSchema("24.2.0", "build", NativeHash, source);
        source.Clear();
        Assert.Equal(2, schema.Objects.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<IddObjectDefinition>)schema.Objects).Add(first));
        string duplicate = Assert.Throws<ArgumentException>(() =>
            new IddSchema("24.2.0", "build", NativeHash, new[] { first, first })).GetType().Name;
        Assert.True(schema.TryGetObject("FIRST:OBJECT", out IddObjectDefinition? found));
        Assert.Same(first, found);
        return new[]
        {
            "schema.public_type=" + typeof(IddSchema).FullName,
            "object.public_type=" + typeof(IddObjectDefinition).FullName,
            "schema.constructor_count=" + typeof(IddSchema).GetConstructors().Length,
            "object.constructor_count=" + typeof(IddObjectDefinition).GetConstructors().Length,
            "source.defensive_copy=true",
            "objects.read_only=true",
            "allowed_names=" + Join(schema.Objects.Select(item => item.Name)),
            "groups=" + Join(schema.Groups),
            "lookup.case_insensitive=true",
            "duplicate_name=" + duplicate,
        };
    }

    private static string[] ObserveTypedIndexRead()
    {
        IddObjectDefinition first = Object("First:Object", "Group A", required: true);
        IddObjectDefinition second = Object("Second:Object", "Group B");
        var schema = new IddSchema("24.2.0", "build", NativeHash, new[] { first, second });
        string negative = Assert.Throws<ArgumentOutOfRangeException>(() => _ = schema[-1]).GetType().Name;
        string atCount = Assert.Throws<ArgumentOutOfRangeException>(() => _ = schema[2]).GetType().Name;
        string missing = Assert.Throws<KeyNotFoundException>(() => _ = schema["Missing"]).GetType().Name;
        string missingField = Assert.Throws<KeyNotFoundException>(() => _ = first["Missing"]).GetType().Name;
        Assert.True(schema.TryGetObject("second:object", out IddObjectDefinition? found));
        Assert.False(schema.TryGetObject("missing", out IddObjectDefinition? absent));
        Assert.Null(absent);
        return new[]
        {
            "schema.index0=" + schema[0].Name,
            "schema.index1=" + schema[1].Name,
            "schema.string_case=" + schema["FIRST:OBJECT"].Name,
            "schema.try_hit=" + found!.Name,
            "schema.try_miss=true",
            "object.field_index=" + first[0].Name,
            "object.field_name=" + first["name"].Token,
            "schema.negative=" + negative,
            "schema.at_count=" + atCount,
            "schema.missing=" + missing,
            "object.missing_field=" + missingField,
        };
    }

    private static string[] ObserveReadOnlyMutationContract()
    {
        IddObjectDefinition original = Object("Original:Object", "Group");
        IddObjectDefinition replacement = Object("Replacement:Object", "Group");
        var source = new List<IddObjectDefinition> { original };
        var schema = new IddSchema("24.2.0", "build", NativeHash, source);
        source[0] = replacement;
        string objectSet = Assert.Throws<NotSupportedException>(() =>
            ((IList<IddObjectDefinition>)schema.Objects)[0] = replacement).GetType().Name;
        string fieldSet = Assert.Throws<NotSupportedException>(() =>
            ((IList<IddFieldDefinition>)original.Fields)[0] = Field("A2", 0, "Other")).GetType().Name;
        PropertyInfo objects = Assert.IsAssignableFrom<PropertyInfo>(typeof(IddSchema).GetProperty(nameof(IddSchema.Objects)));
        PropertyInfo fields = Assert.IsAssignableFrom<PropertyInfo>(typeof(IddObjectDefinition).GetProperty(nameof(IddObjectDefinition.Fields)));
        return new[]
        {
            "source_mutation_visible=false",
            "stored_object=" + schema[0].Name,
            "objects.set_item=" + objectSet,
            "fields.set_item=" + fieldSet,
            "schema.objects.setter=" + (objects.SetMethod is null ? "absent" : "present"),
            "object.fields.setter=" + (fields.SetMethod is null ? "absent" : "present"),
            "mutation.route=read_only_collections",
        };
    }

    private static string[] ObserveTypedViews()
    {
        IddObjectDefinition first = Object("First:Object", "Group A", required: true);
        IddObjectDefinition second = Object("Second:Object", "Group B");
        var schema = new IddSchema("24.2.0", "build", NativeHash, new[] { first, second });
        string keys = Join(schema.Objects.Select(item => item.Name));
        string values = Join(schema.Objects.Select(item => item.Name + ":" + item.Group));
        string items = Join(schema.Objects.Select(item => item.Name + "=" + item.Fields.Count));
        Assert.Equal(new[] { first }, schema.InGroup("group a"));
        return new[]
        {
            "keys=" + keys,
            "values=" + values,
            "items=" + items,
            "groups=" + Join(schema.Groups),
            "group.case_insensitive=" + Join(schema.InGroup("GROUP B").Select(item => item.Name)),
            "objects.count=" + schema.Objects.Count,
            "objects.type=" + schema.Objects.GetType().Name,
            "groups.type=" + schema.Groups.GetType().Name,
            "ordering=source_order",
        };
    }

    private static string[] ObserveSchemaConstructionAndMaps()
    {
        var sourceName = new IddFieldDefinition(
            "A1", 0, IddFieldKind.Alpha, "Name", isRequired: true,
            defaultValue: "Unnamed", references: new[] { "NameReferences" },
            referenceClassNames: new[] { "SourceClasses" });
        var sourceObject = new IddObjectDefinition(
            "Source:Object", "Source Group", new[] { sourceName }, isRequired: true);
        var targetName = new IddFieldDefinition(
            "A1", 0, IddFieldKind.Alpha, "Source Name",
            objectLists: new[] { "NameReferences" });
        var targetObject = new IddObjectDefinition(
            "Target:Object", "Target Group", new[] { targetName });
        var schema = new IddSchema("24.2.0", "94a887817b", NativeHash.ToUpperInvariant(),
            new[] { sourceObject, targetObject });

        string referenceMap = Join(schema.Objects.SelectMany(obj => obj.Fields.SelectMany(field =>
            field.References.Select(reference => reference + "=" + obj.Name + "." + field.Name))));
        string referenceClassMap = Join(schema.Objects.SelectMany(obj => obj.Fields.SelectMany(field =>
            field.ReferenceClassNames.Select(reference => reference + "=" + obj.Name))));
        string referencedMap = Join(schema.Objects.SelectMany(obj => obj.Fields.SelectMany(field =>
            field.ObjectLists.Select(reference => reference + "=" + obj.Name + "." + field.Name))));
        Assert.True(schema.TryGetObject("source:object", out IddObjectDefinition? found));
        return new[]
        {
            "schema.version=" + schema.Version,
            "schema.build=" + schema.Build,
            "schema.source_sha256=" + schema.SourceSha256,
            "schema.objects=" + Join(schema.Objects.Select(item => item.Name)),
            "schema.groups=" + Join(schema.Groups),
            "required_objects=" + Join(schema.Objects.Where(item => item.IsRequired).Select(item => item.Name)),
            "reference_map_obj=" + referenceMap,
            "reference_map_cls=" + referenceClassMap,
            "referenced_map_obj=" + referencedMap,
            "reference.source_field=" + Join(sourceName.References),
            "reference_class.source_field=" + Join(sourceName.ReferenceClassNames),
            "object_list.target_field=" + Join(targetName.ObjectLists),
            "lookup.case_insensitive=" + found!.Name,
            "source.required=" + sourceObject.IsRequired,
            "target.required=" + targetObject.IsRequired,
            "source.default=" + sourceName.DefaultValue,
        };
    }

    private static string[] ObserveIddRead()
    {
        const string text = """
            !IDD_Version 24.2.0
            !IDD_BUILD abc123
            \group Test Group
            Version,
              \required-object
              A1; \field Version Identifier
                  \required-field
                  \default 24.2
            """;
        string path = Path.Combine(Path.GetTempPath(), "imugi-native-idd-" + Guid.NewGuid().ToString("N") + ".idd");
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
            IddSchema schema = IddParser.ParseFile(path);
            IddObjectDefinition item = Assert.Single(schema.Objects);
            IddFieldDefinition field = Assert.Single(item.Fields);
            IddSchema missingVersion = IddParser.Parse("\\group G\nThing,\n A1; \\field Name\n", NativeHash);
            string invalid = Assert.Throws<FormatException>(() =>
                IddParser.Parse("A1; \\field Name", NativeHash)).GetType().Name;
            Assert.Equal(IddParser.ComputeFileSha256(path), schema.SourceSha256);
            return new[]
            {
                "schema.version=" + schema.Version,
                "schema.build=" + schema.Build,
                "schema.object_count=" + schema.Objects.Count,
                "object.name=" + item.Name,
                "object.group=" + item.Group,
                "object.required=" + item.IsRequired,
                "field.name=" + field.Name,
                "field.required=" + field.IsRequired,
                "field.default=" + field.DefaultValue,
                "source_hash.matches=true",
                "missing_version=<empty>",
                "invalid_field_before_object=" + invalid,
                "parse_file.public=true",
            };
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string[] ObserveSchemaCacheRoundtrip()
    {
        IddObjectDefinition item = Object("Cache:Object", "Cache Group", required: true);
        var schema = new IddSchema("24.2.0", "94a887817b", NativeHash, new[] { item });
        using var stream = new MemoryStream();
        IddSchemaCache.Write(stream, schema, leaveOpen: true);
        byte[] cacheBytes = stream.ToArray();
        Assert.True(cacheBytes.Length > 2);
        Assert.Equal(0x1f, cacheBytes[0]);
        Assert.Equal(0x8b, cacheBytes[1]);
        stream.Position = 0;
        IddSchema restored = IddSchemaCache.Read(stream, NativeHash, leaveOpen: true);
        stream.Position = 0;
        string mismatch = Assert.Throws<InvalidDataException>(() =>
            IddSchemaCache.Read(stream, new string('b', 64), leaveOpen: true)).GetType().Name;

        string path = Path.Combine(Path.GetTempPath(), "imugi-native-cache-" + Guid.NewGuid().ToString("N") + ".json.gz");
        bool tryHit;
        bool tryMiss;
        try
        {
            IddSchemaCache.Write(path, schema);
            tryHit = IddSchemaCache.TryRead(path, NativeHash, out IddSchema? fromPath);
            tryMiss = IddSchemaCache.TryRead(path, new string('c', 64), out IddSchema? absent);
            Assert.NotNull(fromPath);
            Assert.Null(absent);
        }
        finally
        {
            File.Delete(path);
        }

        return new[]
        {
            "cache.schema=" + IddSchemaCache.CacheSchema,
            "cache.gzip_magic=1f8b",
            "roundtrip.version=" + restored.Version,
            "roundtrip.build=" + restored.Build,
            "roundtrip.source_sha256=" + restored.SourceSha256,
            "roundtrip.objects=" + Join(restored.Objects.Select(value => value.Name)),
            "roundtrip.groups=" + Join(restored.Groups),
            "roundtrip.required=" + restored[0].IsRequired,
            "roundtrip.field=" + restored[0][0].Name,
            "roundtrip.default=" + restored[0][0].DefaultValue,
            "source_mismatch=" + mismatch,
            "try_read.hit=" + tryHit,
            "try_read.mismatch=" + tryMiss,
            "cache.public_write=true",
            "cache.public_read=true",
        };
    }

    private static IddFieldDefinition Field(string token, int position, string name) =>
        new(token, position, token.StartsWith('N') ? IddFieldKind.Numeric : IddFieldKind.Alpha,
            name, defaultValue: "Default");

    private static IddObjectDefinition Object(string name, string group, bool required = false) =>
        new(name, group, new[] { Field("A1", 0, "Name") }, isRequired: required);

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
                batch1_resolved_count = 40,
                deferred_target_count = 44,
                equivalent_target_count = 9,
                exact_case_count = 8,
                exact_source_declaration_count = 133,
                exact_target_count = 21,
                exception_target_count = 12,
                fixture_repository_commit = FixtureRepositoryCommit,
                full_energyplus_idd_support_hash_pinned = true,
                internal_native_route_claimed = false,
                out_of_scope_count = 28,
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
        Assert.Equal(21, scope.GetProperty("exact_target_count").GetInt32());
        Assert.Equal(9, scope.GetProperty("equivalent_target_count").GetInt32());
        Assert.Equal(12, scope.GetProperty("exception_target_count").GetInt32());
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
