using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.SimpleDragon.Tests;

public sealed class ConstantsNumericOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/epsimple-constants-numeric-oracle.json";
    private const string OracleSha256 =
        "sha256:5184425ab052e66882ecd3ac7253d531603279acf1ef455dde7e489e442cdffc";
    private const string CasesSha256 =
        "sha256:e80c7d274444f640a4c3a2ddf3b8a7c03e06adfe6e0b3b844c8ed74dce501e3a";
    private const int OracleByteLength = 89_692;
    private const int ExpectedCaseCount = 87;
    private const string OracleSchema =
        "dragons.python-reference.epsimple-constants-numeric.v1";
    private const string EvidenceTestCase =
        "Dragons.SimpleDragon.Tests.ConstantsNumericOracleParityTests.MatchesPinnedPythonConstantsNumeric";
    private const string UpstreamPath = "src/epsimple/constants.py";
    private const string StaticClassBodyHash =
        "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726";

    // Exact path/symbol/hash/assertion literals are consumed by the trusted
    // compatibility evidence collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/epsimple/constants.py", "ConvectionHeatTransfer", "sha256:2d68c0fd189f85734d82a18e0312c03fda9f734cdcf7b3d72bf2d3e356c29577", "epsimple-constants-numeric-convection-heat-transfer-2d68c0fd"),
        new("src/epsimple/constants.py", "ConvectionHeatTransfer.IN", "sha256:f4d1b69119dd3619805511a2f0b25fddbe63554a38c3001f672cd1efdcea1edf", "epsimple-constants-numeric-convection-heat-transfer-in-f4d1b691"),
        new("src/epsimple/constants.py", "ConvectionHeatTransfer.OUT", "sha256:c36faf62dd987cd8561bd92a0c78e218baa23444f8d487070612ff8b5b3aa5b9", "epsimple-constants-numeric-convection-heat-transfer-out-c36faf62"),
        new("src/epsimple/constants.py", "Site2CO2", "sha256:9ff40d942ec30fa90a8b95e5d24205d33eea26afc4e143ee8457bf440b0a6270", "epsimple-constants-numeric-site2co2-9ff40d94"),
        new("src/epsimple/constants.py", "Site2CO2.DISTRICTHEATING", "sha256:1d7b874c5a80a7b28fe56c8dbc5b20c395c260feb0c339657a1ee76922bf447d", "epsimple-constants-numeric-site2co2-districtheating-1d7b874c"),
        new("src/epsimple/constants.py", "Site2CO2.ELECTRICITY", "sha256:427886a21467ffa2e70b09b222f44e9185d1bdc8cf3ff6cc3d858f370b439b5a", "epsimple-constants-numeric-site2co2-electricity-427886a2"),
        new("src/epsimple/constants.py", "Site2CO2.LPG", "sha256:68cf7791fd2569d21d8dfdc36fffb54c82dcb4493c258708b2da1bda096b62f1", "epsimple-constants-numeric-site2co2-lpg-68cf7791"),
        new("src/epsimple/constants.py", "Site2CO2.NATURALGAS", "sha256:860c2f939cbd8d3c6c89855296706c774d24d3a93a70fb1595e7f04cec6a9e90", "epsimple-constants-numeric-site2co2-naturalgas-860c2f93"),
        new("src/epsimple/constants.py", "Site2CO2.OIL", "sha256:4a1979a27d16ba6b4e0765d2d5f97142a35f5485967e05be6e52916100e07727", "epsimple-constants-numeric-site2co2-oil-4a1979a2"),
        new("src/epsimple/constants.py", "Site2Cost", "sha256:0f8750781047825eb5c4eea60e058821a61701bf39a930e8a78f9a31e2c9566e", "epsimple-constants-numeric-site2cost-0f875078"),
        new("src/epsimple/constants.py", "Site2Cost.DISTRICTHEATING", "sha256:956e2b0d76110c8aa33eb3b33fec599d6e1ea9f8c98b7cb58d535c5f16884ebb", "epsimple-constants-numeric-site2cost-districtheating-956e2b0d"),
        new("src/epsimple/constants.py", "Site2Cost.ELECTRICITY", "sha256:b9b2bc9925459d830c1de8a4e971d5f4597021ec62d980f162e8a7718ac9abff", "epsimple-constants-numeric-site2cost-electricity-b9b2bc99"),
        new("src/epsimple/constants.py", "Site2Cost.LPG", "sha256:08fe014b98f9d0492866d4b64446982476f8b07432d981ccd8ff76a96cec5ecf", "epsimple-constants-numeric-site2cost-lpg-08fe014b"),
        new("src/epsimple/constants.py", "Site2Cost.NATURALGAS", "sha256:6c00bbfc4ae58ce5287c7748b5f9dd75141457e56c37b6e9d4a9284a57064055", "epsimple-constants-numeric-site2cost-naturalgas-6c00bbfc"),
        new("src/epsimple/constants.py", "Site2Cost.OIL", "sha256:f58bfe501cf9658b50f5541c6fa314ddcb98369d59b760f626fd1318aaf607d1", "epsimple-constants-numeric-site2cost-oil-f58bfe50"),
        new("src/epsimple/constants.py", "Site2Source", "sha256:763a14c74718b1386a9ddc7a5c0f06ebb769ac08391365d917d1042965292e9d", "epsimple-constants-numeric-site2source-763a14c7"),
        new("src/epsimple/constants.py", "Site2Source.DISTRICTHEATING", "sha256:5f0ca3b7ed38e426a21befb255af26cd257c025be81734a4e7be8469a777f9f7", "epsimple-constants-numeric-site2source-districtheating-5f0ca3b7"),
        new("src/epsimple/constants.py", "Site2Source.ELECTRICITY", "sha256:9f6e831e10bc5bee518399cec50e39fc7258fa7186028636d6e7dce89cbd637c", "epsimple-constants-numeric-site2source-electricity-9f6e831e"),
        new("src/epsimple/constants.py", "Site2Source.LPG", "sha256:f891444c39ad8d08e27afa90b0bd7817d5704ee1b70b187e35c86bf1bf08582e", "epsimple-constants-numeric-site2source-lpg-f891444c"),
        new("src/epsimple/constants.py", "Site2Source.NATURALGAS", "sha256:8661aaeabf25d8c5c520b75d20acb9994e59b3153f07d21eafa57971ab6c7394", "epsimple-constants-numeric-site2source-naturalgas-8661aaea"),
        new("src/epsimple/constants.py", "Site2Source.OIL", "sha256:18468fb1b142964ae9104c7ad816347e9109c40ef4205c7872dc577758efc254", "epsimple-constants-numeric-site2source-oil-18468fb1"),
        new("src/epsimple/constants.py", "Unit", "sha256:82eeceb9e427512d5ed45c6139c5fb92859289547ded26e7e410b3be3f591b70", "epsimple-constants-numeric-unit-82eeceb9"),
        new("src/epsimple/constants.py", "Unit.ACH50_TO_ACH", "sha256:fd2a09b09735722d7642be7d9f6f477970306a19540ac5b01d0357ea47c57401", "epsimple-constants-numeric-unit-ach50-to-ach-fd2a09b0"),
        new("src/epsimple/constants.py", "Unit.FRACTION_TO_PERCENT", "sha256:55d3f412e4fc8dc309ceb1e5d298946b289c8adb66736a8de5de2533b5050880", "epsimple-constants-numeric-unit-fraction-to-percent-55d3f412"),
        new("src/epsimple/constants.py", "Unit.M3_PER_S_TO_CMH", "sha256:c67e87d901a7d2de66c51d559d4b4d6552f188503c1f9dc331d5bf698540ea73", "epsimple-constants-numeric-unit-m3-per-s-to-cmh-c67e87d9"),
        new("src/epsimple/constants.py", "Unit.MM_TO_M", "sha256:78d61c825c4faade4c8268ca8c23a95c00ad44ce68574bc3afaf7791387ba1b5", "epsimple-constants-numeric-unit-mm-to-m-78d61c82"),
        new("src/epsimple/constants.py", "Unit.M_TO_MM", "sha256:b49a8507bdd65b293983e5930a4b5710befca44ce1583d8ae3ada9d7ddd4c85b", "epsimple-constants-numeric-unit-m-to-mm-b49a8507"),
        new("src/epsimple/constants.py", "Unit.PERCENT_TO_FRACTION", "sha256:2f91a99f89863099df480e571f6e4f05249479b3adb70f77f1f141838035e240", "epsimple-constants-numeric-unit-percent-to-fraction-2f91a99f"),
        new("src/epsimple/constants.py", "Unit.W_TO_KW", "sha256:9891f5c1310487862261f06e345c18941cad2fac3c22b2210a7a5ee92e22f215", "epsimple-constants-numeric-unit-w-to-kw-9891f5c1"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("ConvectionHeatTransfer", "class", "sha256:f346b39d59e5bdb4e369113e55c6e167dd2fa73da3021d8f90e896b7a936284c", StaticClassBodyHash, "exception", "native-simpledragon-convection-constant-container", "Dragons.SimpleDragon.ConvectionHeatTransfer", null, null, null),
        new("ConvectionHeatTransfer.IN", "constant", "sha256:1b01e1353c2601d136ef91cfac7fe225d304239324671935290bad727f78c005", "sha256:b6d082785c99259867d0dc0d77d76ac63c22d1a444b8f575a2378b70ee151b01", "equivalent", null, "Dragons.SimpleDragon.ConvectionHeatTransfer.Interior", "1.22e8ba2e8ba2fp+3", "IN", "IN"),
        new("ConvectionHeatTransfer.OUT", "constant", "sha256:f0e5bfe691366195a447f74f0d6201598e095b6881803351f3030d781cb1892e", "sha256:08a45f66df3ebb992ae824045dc367e08a223ac790ab9179e048ca4a0a5dc32a", "equivalent", null, "Dragons.SimpleDragon.ConvectionHeatTransfer.Exterior", "1.7417d05f417d1p+4", "OUT", "OUT"),
        new("Site2CO2", "class", "sha256:58f61ff1835c93a6b0956d0a15d937281796e98f01297ba91c6db6e874fc63d6", StaticClassBodyHash, "exception", "native-simpledragon-site-to-carbon-dispatch", "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCarbon", null, null, null),
        new("Site2CO2.DISTRICTHEATING", "constant", "sha256:7192cff6cf324d01d37009b3eb33734d4266d04f498ecb7c04589663ff47cf36", "sha256:7032add1aef46b96675e97d3f35dc6381c8925d1a19ea5ca84df707fa762046a", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCarbon(DistrictHeating)", "1.161e4f765fd8bp-3", "DISTRICTHEATING", "DISTRICTHEATING"),
        new("Site2CO2.ELECTRICITY", "constant", "sha256:72aea6596570580ccc52c17466c4628382e08da6627bd43a5b5ed57b9d682c2f", "sha256:dc2d4918d3dc700bcca1e3c3a4791792a163ca47cc8c43c8f2bf2260ceeec73c", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCarbon(Electricity)", "1.d0ff972474539p-2", "ELECTRICITY", "ELECTRICITY"),
        new("Site2CO2.LPG", "constant", "sha256:679d716960bc375fd53e3e3de5ed9351c5386c9860c457b7f0b4d59d757955b9", "sha256:fdf8e9a54a0c32c67b3cb8278615f6e369eccd60728a4e99b7b6720b8a326103", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCarbon(LiquefiedPetroleumGas)", "1.dc5d63886594bp-3", "LPG", "LPG"),
        new("Site2CO2.NATURALGAS", "constant", "sha256:3219fb8a9ad5ecac99020288d75b2f1fc7ac14f8efc0d03092ce23f6855588f7", "sha256:195526433a8bd4b1bc2e83f85212e7d705c50ed510e557f10aa44f7fe83c4d41", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCarbon(NaturalGas)", "1.9e83e425aee63p-3", "NATURALGAS", "NATURALGAS"),
        new("Site2CO2.OIL", "constant", "sha256:8cd43de8532cfc3648f5c42bbef75ffb179a0bbc291bdc65f2e20c3cd562a90d", "sha256:ae67781472c12cb4bdb378326502ea5d61ab41c783607453740f066589b29598", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCarbon(Oil)", "1.0a8c154c985f0p-2", "OIL", "OIL"),
        new("Site2Cost", "class", "sha256:e30974309debfb1ded65597741074db22e42046c3474a3c97a4ec2fc0cec9751", StaticClassBodyHash, "exception", "native-simpledragon-site-to-cost-dispatch", "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCost", null, null, null),
        new("Site2Cost.DISTRICTHEATING", "constant", "sha256:baaeaa529b5cf356ff6f5975d9ac9cddbbfe68b10fda1d105203b306bdb06506", "sha256:fc5445f71f3d40f721d85bd50509c6b83f3863cb0704887e5231a7859fa8d2dd", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCost(DistrictHeating)", "1.7beb851eb851fp+6", "DISTRICTHEATING", "DISTRICTHEATING"),
        new("Site2Cost.ELECTRICITY", "constant", "sha256:7cd5cc01b3ea53e2080f04dca3fbbecf99ab795221a61df8a1ceff52a24970de", "sha256:cef178527e410145519cf69089e01fd78e5600280c283dc1f62279a4fd56498f", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCost(Electricity)", "1.45d70a3d70a3dp+7", "ELECTRICITY", "ELECTRICITY"),
        new("Site2Cost.LPG", "constant", "sha256:a2eb07f314178de19dc8709a8c051256a8f86203773b1d619e7aab3513147d4d", "sha256:fd6cd001ed389da65ae93f87211c80326ca289e0188269679ac4a5bcdc35dd47", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCost(LiquefiedPetroleumGas)", "1.71c7ae147ae14p+7", "LPG", "LPG"),
        new("Site2Cost.NATURALGAS", "constant", "sha256:45e72751468baaa1d08dc16bde1cb11f8c82797bdfd36abe7c3ae624639d8f16", "sha256:cc5db32e4b7f9e9d343246a3f654663763ca62f35d23177fbd39de588bb60f0f", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCost(NaturalGas)", "1.387ae147ae148p+6", "NATURALGAS", "NATURALGAS"),
        new("Site2Cost.OIL", "constant", "sha256:a00b0901c5edae87145e5d7643f853976f99686f8e1dcc73850de5760d15e7b1", "sha256:5570b297853cd48c7931910ccd9e0a5fa97b3ebaef17cea325e4b01e9ed1e57a", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToCost(Oil)", "1.1bd70a3d70a3dp+7", "OIL", "OIL"),
        new("Site2Source", "class", "sha256:a0ad366186188fe14567a9661fb67fa7a1b950aa5afad401b8ff10d22f492fb2", StaticClassBodyHash, "exception", "native-simpledragon-site-to-source-dispatch", "Dragons.SimpleDragon.EnergyConversionFactors.SiteToSource", null, null, null),
        new("Site2Source.DISTRICTHEATING", "constant", "sha256:97f37a60c1b9346e6c48a0affd0acf48bc673a3adcfc06de6f52e93c73d1c193", "sha256:6f54899d2973a52f87696315784a866783785a20b9b7222a1cd5d16216ebe662", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToSource(DistrictHeating)", "1.74bc6a7ef9db2p-1", "DISTRICTHEATING", "DISTRICTHEATING"),
        new("Site2Source.ELECTRICITY", "constant", "sha256:6da7ff4826f9f1c1012fbcd7fcf801cfaa65be65aad36842c0c51626445f2d98", "sha256:11748497aa24493f29c98a749db7261b999b4dd5e8da827cb2dae8e0b7fa8f96", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToSource(Electricity)", "1.6000000000000p+1", "ELECTRICITY", "ELECTRICITY"),
        new("Site2Source.LPG", "constant", "sha256:e071db60c656b33f740fe3923237e373cb98029cef124a74c2524c909fae40a0", "sha256:9d87ff0a62d45b977abbeee07313669faa41a26a1e371fb4de8bd4920ae5ea8c", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToSource(LiquefiedPetroleumGas)", "1.199999999999ap+0", "LPG", "NATURALGAS"),
        new("Site2Source.NATURALGAS", "constant", "sha256:3575ab52059704e7b3d721927fd400cd5a6435516af21389305b1024b5ce7b95", "sha256:9d87ff0a62d45b977abbeee07313669faa41a26a1e371fb4de8bd4920ae5ea8c", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToSource(NaturalGas)", "1.199999999999ap+0", "NATURALGAS", "NATURALGAS"),
        new("Site2Source.OIL", "constant", "sha256:58a3d244b423f6a30f472b721392e57805eac073597c427be88545ff8581398b", "sha256:9d87ff0a62d45b977abbeee07313669faa41a26a1e371fb4de8bd4920ae5ea8c", "equivalent", null, "Dragons.SimpleDragon.EnergyConversionFactors.SiteToSource(Oil)", "1.199999999999ap+0", "OIL", "NATURALGAS"),
        new("Unit", "class", "sha256:4207679fe2ede1a951b1882e62a22d8d915b1442dc5d1e1f62925d16cb6422e0", StaticClassBodyHash, "exception", "native-simpledragon-unit-conversion-constants", "Dragons.SimpleDragon.UnitConversions", null, null, null),
        new("Unit.ACH50_TO_ACH", "constant", "sha256:3afd608864e96c6cbd84dafd3cdfc94ce317a2e2784cea31c8afe3990752c554", "sha256:4ee0d3906e46532ef590b16c4f85886bb129c92b697aa36a261bdc1fd09335b5", "equivalent", null, "Dragons.SimpleDragon.UnitConversions.AirChangesAt50PaToNaturalAirChanges", "1.1eb851eb851ecp-4", "ACH50_TO_ACH", "ACH50_TO_ACH"),
        new("Unit.FRACTION_TO_PERCENT", "constant", "sha256:1cb497d3cae6e62d4e2c50b1754cf150b4fdce93c973f30509b28cf4fa1e82c4", "sha256:d3c3cec052dae85942a722526911012da69bf59aca87bc1229bfbc27211abdd1", "equivalent", null, "Dragons.SimpleDragon.UnitConversions.FractionToPercent", "1.9000000000000p+6", "FRACTION_TO_PERCENT", "FRACTION_TO_PERCENT"),
        new("Unit.M3_PER_S_TO_CMH", "constant", "sha256:574acc67cf8a454280621da85ef059251e2c222aabe5bc1fc7679a3e09d7c3eb", "sha256:aa8817177208c34e6d84856ee1bbc0360af016491b5a153690f899cb967626f2", "equivalent", null, "Dragons.SimpleDragon.UnitConversions.CubicMetresPerSecondToPerHour", "1.c200000000000p+11", "M3_PER_S_TO_CMH", "M3_PER_S_TO_CMH"),
        new("Unit.MM_TO_M", "constant", "sha256:03686fd1f94671e5a411db8c0f4d7a6bc8f62f9033a4f65fecfab0cf2f2a06f8", "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf", "equivalent", null, "Dragons.SimpleDragon.UnitConversions.MillimetresToMetres", "1.0624dd2f1a9fcp-10", "MM_TO_M", "MM_TO_M"),
        new("Unit.M_TO_MM", "constant", "sha256:ee5969c67823797b883ba77c3c09f0c078a08dde8240b68a2ca79c7a57c70e78", "sha256:d1c5df1014d99d4fa0a7e141221a6ba21ecf57cc8755703a7d6229af7a2a376d", "equivalent", null, "Dragons.SimpleDragon.UnitConversions.MetresToMillimetres", "1.f400000000000p+9", "M_TO_MM", "M_TO_MM"),
        new("Unit.PERCENT_TO_FRACTION", "constant", "sha256:13ad23718f631a0cc2b84c7b09e1287564aa549a52c30ec98f10a86b15f6a3fb", "sha256:d2dff8ba2e3305a55a5cfcb7f170272f46ce3773420fc2094c6eb318b178a722", "equivalent", null, "Dragons.SimpleDragon.UnitConversions.PercentToFraction", "1.47ae147ae147bp-7", "PERCENT_TO_FRACTION", "PERCENT_TO_FRACTION"),
        new("Unit.W_TO_KW", "constant", "sha256:3212f8fad3be6cfe8fc6dc7a7391b487924402ac39cacdafd8d9af8686a00085", "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf", "equivalent", null, "Dragons.SimpleDragon.UnitConversions.WattsToKilowatts", "1.0624dd2f1a9fcp-10", "W_TO_KW", "MM_TO_M"),
    };

    private static readonly FamilyDefinition[] Families =
    {
        new("ConvectionHeatTransfer", "convection-heat-transfer", "convection-class", "convection-constant", "static-constant-container", new[] { "IN", "OUT" }, Array.Empty<string[]>()),
        new("Site2CO2", "site2co2", "site-factor-class", "site-factor-constant", "carrier-dispatch", new[] { "ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING" }, Array.Empty<string[]>()),
        new("Site2Cost", "site2cost", "site-factor-class", "site-factor-constant", "carrier-dispatch", new[] { "ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING" }, Array.Empty<string[]>()),
        new("Site2Source", "site2source", "site-factor-class", "site-factor-constant", "carrier-dispatch-with-result-zip-adaptation", new[] { "ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING" }, new[] { new[] { "NATURALGAS", "LPG", "OIL" } }),
        new("Unit", "unit", "unit-class", "unit-constant", "static-constant-container", new[] { "MM_TO_M", "M_TO_MM", "FRACTION_TO_PERCENT", "PERCENT_TO_FRACTION", "W_TO_KW", "ACH50_TO_ACH", "M3_PER_S_TO_CMH" }, new[] { new[] { "MM_TO_M", "W_TO_KW" } }),
    };

    private static readonly CaseBinding[] ExpectedCases = BuildExpectedCases();

    [Fact]
    public void MatchesPinnedPythonConstantsNumeric()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = Sha256(bytes);
        Assert.Equal(OracleSha256, sha256);
        Assert.Equal(OracleByteLength, bytes.Length);

        using JsonDocument oracle = JsonDocument.Parse(bytes);
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        var observations = new List<NativeObservation>(ExpectedCaseCount);
        for (int index = 0; index < cases.Length; index++)
        {
            CaseBinding binding = ExpectedCases[index];
            SymbolContract symbol = SymbolFor(binding.Symbol);
            string[] nativeFacts = ExecuteCase(
                binding,
                cases[index].GetProperty("python").GetProperty("facts"));
            Assert.Equal(3, nativeFacts.Length);
            Assert.Equal(3, nativeFacts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(nativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
            observations.Add(new NativeObservation(
                binding.CaseId,
                binding.Symbol,
                symbol.AdaptationId,
                nativeFacts));
        }

        Assert.Equal(ExpectedCaseCount, observations.Count);
        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            NativeObservation[] symbolObservations = observations
                .Where(item => item.Symbol == evidence.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(3, symbolObservations.Length);
            var receipt = new
            {
                fixture = new
                {
                    case_count = ExpectedCaseCount,
                    cases_sha256 = CasesSha256,
                    path = OracleRepositoryPath,
                    sha256,
                },
                observations = symbolObservations.Select(item => new
                {
                    adaptation_id = item.AdaptationId,
                    case_id = item.CaseId,
                    native_facts = item.NativeFacts,
                    native_outcome = "returned",
                }).ToArray(),
                upstream_path = evidence.Path,
                upstream_symbol = evidence.Symbol,
            };
            JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
            ValidateReceipt(receiptJson, evidence, symbolObservations);
            TrustedEvidenceRecorder.Record(
                evidence.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }
    }

    private static JsonElement[] ValidateCorpus(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
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
        AssertNoRawAddresses(root.GetRawText());
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);
        ValidateTaggedScalarsRecursive(root);

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "observation_dependency", "path", "source_sha256");
        Assert.Equal("847b01f68f438f560a986072bcaa7768fbf67897", RequiredString(upstream, "commit"));
        Assert.Equal("sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02", RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal("sha256:d5dd5241ec90b14ba3708a525cd74279a8cdc238164a5b8544c4c82b05a29897", RequiredString(upstream, "source_sha256"));
        ValidateObservationDependency(upstream.GetProperty("observation_dependency"));

        ValidateRuntime(root.GetProperty("runtime"));
        ValidateEvidenceBindings();
        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));
        ValidateNativeBindings();

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCaseCount, cases.Length);
        string[] identifiers = cases.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), identifiers);
        Assert.Equal(identifiers.OrderBy(item => item, StringComparer.Ordinal), identifiers);
        Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
        }

        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(runtime, "dependencies", "implementation", "python_hash_algorithm", "python_hash_seed", "python_hash_width_bits", "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));

        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(dependencies, "colorama", "et_xmlfile", "numpy", "openpyxl", "pandas", "python-dateutil", "pytz", "six", "tqdm", "tzdata");
        Assert.Equal("0.4.6", RequiredString(dependencies, "colorama"));
        Assert.Equal("2.0.0", RequiredString(dependencies, "et_xmlfile"));
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("3.1.5", RequiredString(dependencies, "openpyxl"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("2.9.0.post0", RequiredString(dependencies, "python-dateutil"));
        Assert.Equal("2024.2", RequiredString(dependencies, "pytz"));
        Assert.Equal("1.16.0", RequiredString(dependencies, "six"));
        Assert.Equal("4.67.1", RequiredString(dependencies, "tqdm"));
        Assert.Equal("2024.2", RequiredString(dependencies, "tzdata"));
    }

    private static void ValidateObservationDependency(JsonElement dependency)
    {
        AssertKeys(dependency, "file", "symbols");
        JsonElement file = dependency.GetProperty("file");
        AssertKeys(file, "ast_hash", "content_hash", "path");
        Assert.Equal("src/epsimple/core/model.py", RequiredString(file, "path"));
        Assert.Equal("sha256:f79918272c07515ee4ae98fa62f4ca5d5d703e5e2faa334f72d6a6966e1e2447", RequiredString(file, "ast_hash"));
        Assert.Equal("sha256:71dc9bb8d97e829c27d9b5d19ef88709af9613f9e53f60807d54ceb2922e4532", RequiredString(file, "content_hash"));

        JsonElement[] symbols = dependency.GetProperty("symbols").EnumerateArray().ToArray();
        Assert.Equal(2, symbols.Length);
        ValidateObservationSymbol(
            symbols[0],
            "constant",
            "GreenRetrofitResult.VALID_DIGITS",
            "sha256:aa336779f69a8902021215ad36bc8925e1d599b84b1c2149a383d3313065b1a2",
            "sha256:ddcc9e26678f237b5f7892c086072a5962980b4d4b13bcee47bd9c0d98a52cc6",
            "sha256:ff1cddacd1d221d604e80997d48ef03662bbeb531c45337abde8fcc3f9fc30df");
        ValidateObservationSymbol(
            symbols[1],
            "function",
            "GreenRetrofitResult.to_source_uses",
            "sha256:3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef",
            "sha256:d9c7d1b27a50ae9b04a5278c1d1881309fc297af097af411791f2f1d77e73d5d",
            "sha256:842eb853a7216a84eab7ccc5a04d7454fc7f2572ea9c8e0bc32f73d6ffc84291");
    }

    private static void ValidateObservationSymbol(
        JsonElement value,
        string kind,
        string symbol,
        string signatureHash,
        string bodyHash,
        string symbolHash)
    {
        AssertKeys(value, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
        Assert.Equal("src/epsimple/core/model.py", RequiredString(value, "path"));
        Assert.Equal(kind, RequiredString(value, "kind"));
        Assert.Equal(symbol, RequiredString(value, "symbol"));
        Assert.Equal(signatureHash, RequiredString(value, "signature_hash"));
        Assert.Equal(bodyHash, RequiredString(value, "body_hash"));
        Assert.Equal(symbolHash, RequiredString(value, "symbol_hash"));
    }

    private static void ValidateEvidenceBindings()
    {
        Assert.Equal(ExpectedSymbols.Length, ExpectedEvidence.Length);
        Assert.Equal(ExpectedEvidence.Length, ExpectedEvidence.Select(item => item.Symbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ExpectedEvidence.Length, ExpectedEvidence.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < ExpectedEvidence.Length; index++)
        {
            EvidenceBinding evidence = ExpectedEvidence[index];
            Assert.Equal(UpstreamPath, evidence.Path);
            Assert.Equal(ExpectedSymbols[index].Symbol, evidence.Symbol);
            Assert.StartsWith("sha256:", evidence.SymbolHash, StringComparison.Ordinal);
            Assert.EndsWith(evidence.SymbolHash.Substring("sha256:".Length, 8), evidence.AssertionId, StringComparison.Ordinal);
        }
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            JsonElement item = symbols[index];
            SymbolContract symbol = ExpectedSymbols[index];
            EvidenceBinding evidence = ExpectedEvidence[index];
            AssertKeys(item, "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash");
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(symbol.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(symbol.Kind, RequiredString(item, "kind"));
            Assert.Equal(symbol.SignatureHash, RequiredString(item, "signature_hash"));
            Assert.Equal(symbol.BodyHash, RequiredString(item, "body_hash"));
            Assert.Equal(evidence.SymbolHash, RequiredString(item, "symbol_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement consumer)
    {
        AssertKeys(consumer, "adaptations", "assertion_ids", "case_count", "case_ids", "classifications", "float_encoding", "runtime_names", "target_symbols");
        Assert.Equal(ExpectedCaseCount, consumer.GetProperty("case_count").GetInt32());
        AssertStringArray(consumer.GetProperty("case_ids"), ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(consumer.GetProperty("target_symbols"), ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal("python-binary64-hex-without-0x-prefix", RequiredString(consumer, "float_encoding"));
        Assert.Equal("pinned-python-only-no-native-type-name-claims", RequiredString(consumer, "runtime_names"));

        JsonElement classifications = consumer.GetProperty("classifications");
        JsonElement adaptations = consumer.GetProperty("adaptations");
        JsonElement assertionIds = consumer.GetProperty("assertion_ids");
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        AssertKeys(adaptations, ExpectedSymbols.Where(item => item.AdaptationId is not null).Select(item => item.Symbol).ToArray());
        AssertKeys(assertionIds, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal(24, ExpectedSymbols.Count(item => item.Classification == "equivalent"));
        Assert.Equal(5, ExpectedSymbols.Count(item => item.Classification == "exception"));
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            SymbolContract symbol = ExpectedSymbols[index];
            Assert.Equal(symbol.Classification, RequiredString(classifications, symbol.Symbol));
            Assert.Equal(ExpectedEvidence[index].AssertionId, RequiredString(assertionIds, symbol.Symbol));
            if (symbol.AdaptationId is not null)
            {
                Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
            }
        }
    }

    private static void ValidateCase(JsonElement value, CaseBinding binding)
    {
        SymbolContract symbol = SymbolFor(binding.Symbol);
        if (symbol.AdaptationId is null)
        {
            AssertKeys(value, "executor", "id", "python", "symbol");
        }
        else
        {
            AssertKeys(value, "executor", "expected_dotnet", "id", "python", "symbol");
            JsonElement expectedDotnet = value.GetProperty("expected_dotnet");
            AssertKeys(expectedDotnet, "adaptation", "outcome");
            Assert.Equal(symbol.AdaptationId, RequiredString(expectedDotnet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));
        }

        Assert.Equal(binding.CaseId, RequiredString(value, "id"));
        Assert.Equal(binding.Executor, RequiredString(value, "executor"));
        Assert.Equal(binding.Symbol, RequiredString(value, "symbol"));
        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));
        Assert.Equal(JsonValueKind.Object, python.GetProperty("facts").ValueKind);
    }

    private static void ValidateNativeBindings()
    {
        AssertStaticConstantContainer(
            typeof(UnitConversions),
            "AirChangesAt50PaToNaturalAirChanges",
            "CubicMetresPerSecondToPerHour",
            "FractionToPercent",
            "MetresToMillimetres",
            "MillimetresToMetres",
            "PercentToFraction",
            "WattsToKilowatts");
        AssertStaticConstantContainer(typeof(ConvectionHeatTransfer), "Exterior", "Interior");

        Type dispatch = typeof(EnergyConversionFactors);
        Assert.True(dispatch.IsAbstract && dispatch.IsSealed);
        Assert.Equal(
            new[] { "SiteToCarbon", "SiteToCost", "SiteToSource" },
            dispatch.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(item => item.Name)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            new[] { "Electricity", "NaturalGas", "LiquefiedPetroleumGas", "Oil", "DistrictHeating" },
            Enum.GetNames<EnergyCarrier>());

        foreach (SymbolContract symbol in ExpectedSymbols.Where(item => item.Kind == "constant"))
        {
            double nativeValue = NativeValue(symbol.Symbol);
            AssertBinary64(symbol.ExpectedBinary64!, nativeValue);
        }

        Assert.Equal(UnitConversions.MillimetresToMetres, UnitConversions.WattsToKilowatts);
        Assert.Equal(
            EnergyConversionFactors.SiteToSource(EnergyCarrier.NaturalGas),
            EnergyConversionFactors.SiteToSource(EnergyCarrier.LiquefiedPetroleumGas));
        Assert.Equal(
            EnergyConversionFactors.SiteToSource(EnergyCarrier.NaturalGas),
            EnergyConversionFactors.SiteToSource(EnergyCarrier.Oil));

        double[] resultFactors = NativeResultScalingFactors();
        string[] resultHex =
        {
            "1.6000000000000p+1",
            "1.199999999999ap+0",
            "1.74bc6a7ef9db2p-1",
            "1.0000000000000p+0",
            "1.0000000000000p+0",
        };
        Assert.Equal(resultHex.Length, resultFactors.Length);
        for (int index = 0; index < resultHex.Length; index++)
        {
            AssertBinary64(resultHex[index], resultFactors[index]);
        }
    }

    private static void AssertStaticConstantContainer(Type type, params string[] expectedFields)
    {
        Assert.True(type.IsAbstract && type.IsSealed);
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(expectedFields.OrderBy(item => item, StringComparer.Ordinal), fields.Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(fields, field =>
        {
            Assert.Equal(typeof(double), field.FieldType);
            Assert.True(field.IsLiteral);
            Assert.False(field.IsInitOnly);
        });
    }

    private static string[] ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        return binding.Symbol.Contains('.', StringComparison.Ordinal)
            ? ExecuteMemberCase(binding, pythonFacts)
            : ExecuteClassCase(binding, pythonFacts);
    }

    private static string[] ExecuteClassCase(CaseBinding binding, JsonElement pythonFacts)
    {
        FamilyDefinition family = FamilyFor(binding.Symbol);
        SymbolContract symbol = SymbolFor(binding.Symbol);
        if (binding.CaseId.EndsWith(".construction", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "observations");
            JsonElement[] observations = pythonFacts.GetProperty("observations").EnumerateArray().ToArray();
            Assert.Equal(3, observations.Length);
            ValidateConstructionObservation(observations[0], "first-declared-value", MemberFor(family, family.DeclaredNames[0]));
            ValidateConstructionObservation(observations[1], "last-declared-value", MemberFor(family, family.DeclaredNames[^1]));
            AssertKeys(observations[2], "error_category", "exception_type", "input", "label", "outcome");
            Assert.Equal("domain", RequiredString(observations[2], "error_category"));
            Assert.Equal("ValueError", RequiredString(observations[2], "exception_type"));
            AssertTaggedInteger(observations[2].GetProperty("input"), "-1");
            Assert.Equal("unknown-value", RequiredString(observations[2], "label"));
            Assert.Equal("raised", RequiredString(observations[2], "outcome"));
            return new[]
            {
                "native-adaptation=" + symbol.AdaptationId,
                "native-construction-model=" + family.NativeShape,
                "native-declared-member-count=" + family.DeclaredNames.Length.ToString(CultureInfo.InvariantCulture),
            };
        }

        if (binding.CaseId.EndsWith(".member-topology", StringComparison.Ordinal))
        {
            ValidateMemberTopology(family, pythonFacts);
            return new[]
            {
                "native-declared-members=" + string.Join(",", family.DeclaredNames),
                "native-alias-value-groups=" + AliasSummary(family),
                family.ClassName == "Site2Source"
                    ? "native-result-scaling-bits=" + string.Join(",", NativeResultScalingFactors().Select(HexBits))
                        + ";native-rounded-probe-row-bits="
                        + string.Join(",", NativeDirectMethodRows().Select(row => string.Join("/", row.Select(HexBits))))
                    : "native-topology=" + family.NativeShape,
            };
        }

        Assert.EndsWith(".type-topology", binding.CaseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "base_names", "class_name", "is_enum_subclass", "is_float_subclass", "module", "signature");
        AssertStringArray(pythonFacts.GetProperty("base_names"), "float", "Enum");
        Assert.Equal(family.ClassName, RequiredString(pythonFacts, "class_name"));
        Assert.True(pythonFacts.GetProperty("is_enum_subclass").GetBoolean());
        Assert.True(pythonFacts.GetProperty("is_float_subclass").GetBoolean());
        Assert.Equal("epsimple.constants", RequiredString(pythonFacts, "module"));
        Assert.Equal("(*values)", RequiredString(pythonFacts, "signature"));
        return new[]
        {
            "native-target=" + symbol.NativeTarget,
            "native-shape=" + family.NativeShape,
            "upstream-float-enum=adapted",
        };
    }

    private static void ValidateConstructionObservation(
        JsonElement observation,
        string label,
        SymbolContract member)
    {
        AssertKeys(observation, "input", "label", "outcome", "result");
        AssertTaggedFloat(observation.GetProperty("input"), member.ExpectedBinary64!);
        Assert.Equal(label, RequiredString(observation, "label"));
        Assert.Equal("returned", RequiredString(observation, "outcome"));
        JsonElement result = observation.GetProperty("result");
        AssertKeys(result, "name", "value");
        Assert.Equal(member.CanonicalName, RequiredString(result, "name"));
        AssertTaggedFloat(result.GetProperty("value"), member.ExpectedBinary64!);
    }

    private static void ValidateMemberTopology(FamilyDefinition family, JsonElement facts)
    {
        string[] baseKeys =
        {
            "alias_groups",
            "canonical_names",
            "declared_member_names",
            "declared_values",
            "iterated_member_names",
            "iterated_values",
            "member_count",
            "unique_member_count",
        };
        AssertKeys(facts, family.ClassName == "Site2Source" ? baseKeys.Append("result_scaling").ToArray() : baseKeys);
        AssertStringArray(facts.GetProperty("declared_member_names"), family.DeclaredNames);

        JsonElement canonicalNames = facts.GetProperty("canonical_names");
        AssertKeys(canonicalNames, family.DeclaredNames);
        SymbolContract[] declared = family.DeclaredNames.Select(name => MemberFor(family, name)).ToArray();
        foreach (SymbolContract member in declared)
        {
            Assert.Equal(member.CanonicalName, RequiredString(canonicalNames, member.DeclaredName!));
        }

        JsonElement[] declaredValues = facts.GetProperty("declared_values").EnumerateArray().ToArray();
        Assert.Equal(declared.Length, declaredValues.Length);
        for (int index = 0; index < declared.Length; index++)
        {
            AssertTaggedFloat(declaredValues[index], declared[index].ExpectedBinary64!);
            AssertBinary64(declared[index].ExpectedBinary64!, NativeValue(declared[index].Symbol));
        }

        string[] iteratedNames = declared.Select(item => item.CanonicalName!).Distinct(StringComparer.Ordinal).ToArray();
        AssertStringArray(facts.GetProperty("iterated_member_names"), iteratedNames);
        JsonElement[] iteratedValues = facts.GetProperty("iterated_values").EnumerateArray().ToArray();
        Assert.Equal(iteratedNames.Length, iteratedValues.Length);
        for (int index = 0; index < iteratedNames.Length; index++)
        {
            SymbolContract canonical = Assert.Single(declared, item => item.DeclaredName == iteratedNames[index]);
            AssertTaggedFloat(iteratedValues[index], canonical.ExpectedBinary64!);
        }

        Assert.Equal(declared.Length, facts.GetProperty("member_count").GetInt32());
        Assert.Equal(iteratedNames.Length, facts.GetProperty("unique_member_count").GetInt32());
        JsonElement[] aliases = facts.GetProperty("alias_groups").EnumerateArray().ToArray();
        Assert.Equal(family.AliasGroups.Length, aliases.Length);
        for (int index = 0; index < aliases.Length; index++)
        {
            AssertStringArray(aliases[index], family.AliasGroups[index]);
            double[] nativeValues = family.AliasGroups[index]
                .Select(name => NativeValue(MemberFor(family, name).Symbol))
                .ToArray();
            Assert.All(nativeValues, value => Assert.Equal(nativeValues[0], value));
        }

        if (family.ClassName == "Site2Source")
        {
            ValidateResultScaling(facts.GetProperty("result_scaling"));
        }
    }

    private static void ValidateResultScaling(JsonElement scaling)
    {
        AssertKeys(scaling, "carrier_order", "direct_method_execution", "factor_sources", "factors");
        AssertStringArray(scaling.GetProperty("carrier_order"), "ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING");
        AssertStringArray(scaling.GetProperty("factor_sources"), "ELECTRICITY", "NATURALGAS", "DISTRICTHEATING", "UNMATCHED", "UNMATCHED");
        string[] expectedHex =
        {
            "1.6000000000000p+1",
            "1.199999999999ap+0",
            "1.74bc6a7ef9db2p-1",
            "1.0000000000000p+0",
            "1.0000000000000p+0",
        };
        JsonElement[] factors = scaling.GetProperty("factors").EnumerateArray().ToArray();
        double[] nativeFactors = NativeResultScalingFactors();
        Assert.Equal(expectedHex.Length, factors.Length);
        for (int index = 0; index < expectedHex.Length; index++)
        {
            AssertTaggedFloat(factors[index], expectedHex[index]);
            AssertBinary64(expectedHex[index], nativeFactors[index]);
        }

        string[] declaredSourceHex =
        {
            SymbolFor("Site2Source.ELECTRICITY").ExpectedBinary64!,
            SymbolFor("Site2Source.NATURALGAS").ExpectedBinary64!,
            SymbolFor("Site2Source.LPG").ExpectedBinary64!,
            SymbolFor("Site2Source.OIL").ExpectedBinary64!,
            SymbolFor("Site2Source.DISTRICTHEATING").ExpectedBinary64!,
        };
        EnergyCarrier[] carriers = Enum.GetValues<EnergyCarrier>();
        for (int index = 0; index < carriers.Length; index++)
        {
            AssertBinary64(declaredSourceHex[index], EnergyConversionFactors.SiteToSource(carriers[index]));
        }

        ValidateDirectMethodExecution(scaling.GetProperty("direct_method_execution"));
    }

    private static void ValidateDirectMethodExecution(JsonElement execution)
    {
        AssertKeys(execution, "input_rows", "method", "mode", "output_rows", "valid_digits");
        Assert.Equal("GreenRetrofitResult.to_source_uses", RequiredString(execution, "method"));
        Assert.Equal("pinned-upstream-ast-exact-method", RequiredString(execution, "mode"));
        Assert.Equal(2, execution.GetProperty("valid_digits").GetInt32());

        string[] carriers = { "ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING" };
        string[][] expectedInputs = Enumerable.Repeat(
            new[] { "1.0000000000000p+0", "1.0000000000000p+1" },
            carriers.Length).ToArray();
        string[][] expectedOutputs =
        {
            new[] { "1.6000000000000p+1", "1.6000000000000p+2" },
            new[] { "1.199999999999ap+0", "1.199999999999ap+1" },
            new[] { "1.75c28f5c28f5cp-1", "1.75c28f5c28f5cp+0" },
            new[] { "1.0000000000000p+0", "1.0000000000000p+1" },
            new[] { "1.0000000000000p+0", "1.0000000000000p+1" },
        };
        ValidateMethodRows(execution.GetProperty("input_rows"), carriers, expectedInputs, null);
        ValidateMethodRows(
            execution.GetProperty("output_rows"),
            carriers,
            expectedOutputs,
            NativeDirectMethodRows());
    }

    private static void ValidateMethodRows(
        JsonElement value,
        IReadOnlyList<string> carriers,
        IReadOnlyList<string[]> expected,
        IReadOnlyList<double[]>? native)
    {
        JsonElement[] rows = value.EnumerateArray().ToArray();
        Assert.Equal(carriers.Count, rows.Length);
        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            AssertKeys(rows[rowIndex], "carrier", "values");
            Assert.Equal(carriers[rowIndex], RequiredString(rows[rowIndex], "carrier"));
            JsonElement[] values = rows[rowIndex].GetProperty("values").EnumerateArray().ToArray();
            Assert.Equal(expected[rowIndex].Length, values.Length);
            for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                AssertTaggedFloat(values[valueIndex], expected[rowIndex][valueIndex]);
                if (native is not null)
                {
                    AssertBinary64(expected[rowIndex][valueIndex], native[rowIndex][valueIndex]);
                }
            }
        }
    }

    private static string[] ExecuteMemberCase(CaseBinding binding, JsonElement pythonFacts)
    {
        SymbolContract symbol = SymbolFor(binding.Symbol);
        double nativeValue = NativeValue(binding.Symbol);
        AssertBinary64(symbol.ExpectedBinary64!, nativeValue);
        if (binding.CaseId.EndsWith(".engineering-probe", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "input", "operation", "result");
            double input = TaggedFloat(pythonFacts.GetProperty("input"));
            Assert.Equal("multiply", RequiredString(pythonFacts, "operation"));
            double expectedResult = TaggedFloat(pythonFacts.GetProperty("result"));
            double nativeResult = input * nativeValue;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expectedResult), BitConverter.DoubleToInt64Bits(nativeResult));
            return new[]
            {
                "native-operation=multiply",
                "native-probe-input-bits=" + HexBits(input),
                "native-probe-result-bits=" + HexBits(nativeResult),
            };
        }

        if (binding.CaseId.EndsWith(".numeric-semantics", StringComparison.Ordinal))
        {
            AssertKeys(pythonFacts, "canonical_name", "declared_name", "equals_value", "float_projection", "is_float_instance", "is_same_as_canonical_member", "value_type");
            Assert.Equal(symbol.CanonicalName, RequiredString(pythonFacts, "canonical_name"));
            Assert.Equal(symbol.DeclaredName, RequiredString(pythonFacts, "declared_name"));
            Assert.True(pythonFacts.GetProperty("equals_value").GetBoolean());
            AssertTaggedFloat(pythonFacts.GetProperty("float_projection"), symbol.ExpectedBinary64!);
            Assert.True(pythonFacts.GetProperty("is_float_instance").GetBoolean());
            Assert.True(pythonFacts.GetProperty("is_same_as_canonical_member").GetBoolean());
            Assert.Equal("float", RequiredString(pythonFacts, "value_type"));
            return new[]
            {
                "native-target=" + symbol.NativeTarget,
                "native-value-type=System.Double",
                "native-numeric-value-bits=" + HexBits(nativeValue),
            };
        }

        Assert.EndsWith(".value", binding.CaseId, StringComparison.Ordinal);
        AssertKeys(pythonFacts, "canonical_name", "declared_name", "value");
        Assert.Equal(symbol.CanonicalName, RequiredString(pythonFacts, "canonical_name"));
        Assert.Equal(symbol.DeclaredName, RequiredString(pythonFacts, "declared_name"));
        AssertTaggedFloat(pythonFacts.GetProperty("value"), symbol.ExpectedBinary64!);
        return new[]
        {
            "native-target=" + symbol.NativeTarget,
            "native-value-bits=" + HexBits(nativeValue),
            "native-binary64=exact",
        };
    }

    private static double NativeValue(string symbol)
    {
        return symbol switch
        {
            "ConvectionHeatTransfer.IN" => ConvectionHeatTransfer.Interior,
            "ConvectionHeatTransfer.OUT" => ConvectionHeatTransfer.Exterior,
            "Site2CO2.DISTRICTHEATING" => EnergyConversionFactors.SiteToCarbon(EnergyCarrier.DistrictHeating),
            "Site2CO2.ELECTRICITY" => EnergyConversionFactors.SiteToCarbon(EnergyCarrier.Electricity),
            "Site2CO2.LPG" => EnergyConversionFactors.SiteToCarbon(EnergyCarrier.LiquefiedPetroleumGas),
            "Site2CO2.NATURALGAS" => EnergyConversionFactors.SiteToCarbon(EnergyCarrier.NaturalGas),
            "Site2CO2.OIL" => EnergyConversionFactors.SiteToCarbon(EnergyCarrier.Oil),
            "Site2Cost.DISTRICTHEATING" => EnergyConversionFactors.SiteToCost(EnergyCarrier.DistrictHeating),
            "Site2Cost.ELECTRICITY" => EnergyConversionFactors.SiteToCost(EnergyCarrier.Electricity),
            "Site2Cost.LPG" => EnergyConversionFactors.SiteToCost(EnergyCarrier.LiquefiedPetroleumGas),
            "Site2Cost.NATURALGAS" => EnergyConversionFactors.SiteToCost(EnergyCarrier.NaturalGas),
            "Site2Cost.OIL" => EnergyConversionFactors.SiteToCost(EnergyCarrier.Oil),
            "Site2Source.DISTRICTHEATING" => EnergyConversionFactors.SiteToSource(EnergyCarrier.DistrictHeating),
            "Site2Source.ELECTRICITY" => EnergyConversionFactors.SiteToSource(EnergyCarrier.Electricity),
            "Site2Source.LPG" => EnergyConversionFactors.SiteToSource(EnergyCarrier.LiquefiedPetroleumGas),
            "Site2Source.NATURALGAS" => EnergyConversionFactors.SiteToSource(EnergyCarrier.NaturalGas),
            "Site2Source.OIL" => EnergyConversionFactors.SiteToSource(EnergyCarrier.Oil),
            "Unit.ACH50_TO_ACH" => UnitConversions.AirChangesAt50PaToNaturalAirChanges,
            "Unit.FRACTION_TO_PERCENT" => UnitConversions.FractionToPercent,
            "Unit.M3_PER_S_TO_CMH" => UnitConversions.CubicMetresPerSecondToPerHour,
            "Unit.MM_TO_M" => UnitConversions.MillimetresToMetres,
            "Unit.M_TO_MM" => UnitConversions.MetresToMillimetres,
            "Unit.PERCENT_TO_FRACTION" => UnitConversions.PercentToFraction,
            "Unit.W_TO_KW" => UnitConversions.WattsToKilowatts,
            _ => throw new Xunit.Sdk.XunitException("Unknown numeric constant symbol '" + symbol + "'."),
        };
    }

    private static double[] NativeResultScalingFactors()
    {
        EnergyUseBreakdown siteUses = EnergyUseBreakdown.Create(
            (_, _) => Enumerable.Repeat(1000d, MonthlySeries.MonthCount));
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(100d, siteUses);
        return Enum.GetValues<EnergyCarrier>()
            .Select(carrier => result.SourceUses[EnergyEndUse.Heating, carrier][0] / 1000d)
            .ToArray();
    }

    private static double[][] NativeDirectMethodRows()
    {
        EnergyUseBreakdown siteUses = EnergyUseBreakdown.Create(
            (_, _) => Enumerable.Range(1, MonthlySeries.MonthCount).Select(value => (double)value));
        GreenRetrofitResult result = GreenRetrofitResult.FromSiteUses(100d, siteUses);
        return Enum.GetValues<EnergyCarrier>()
            .Select(carrier => result.SourceUses[EnergyEndUse.Heating, carrier].Take(2).ToArray())
            .ToArray();
    }

    private static CaseBinding[] BuildExpectedCases()
    {
        var result = new List<CaseBinding>(ExpectedCaseCount);
        foreach (FamilyDefinition family in Families)
        {
            foreach (string suffix in new[] { "construction", "member-topology", "type-topology" })
            {
                result.Add(new CaseBinding(
                    "epsimple-constants-numeric." + family.Slug + ".class." + suffix,
                    family.ClassExecutor,
                    family.ClassName));
            }

            foreach (SymbolContract member in ExpectedSymbols.Where(item => item.Symbol.StartsWith(family.ClassName + ".", StringComparison.Ordinal)))
            {
                string token = member.DeclaredName!.ToLowerInvariant().Replace('_', '-');
                foreach (string suffix in new[] { "engineering-probe", "numeric-semantics", "value" })
                {
                    result.Add(new CaseBinding(
                        "epsimple-constants-numeric." + family.Slug + "." + token + "." + suffix,
                        family.MemberExecutor,
                        member.Symbol));
                }
            }
        }

        CaseBinding[] ordered = result.OrderBy(item => item.CaseId, StringComparer.Ordinal).ToArray();
        Assert.Equal(ExpectedCaseCount, ordered.Length);
        return ordered;
    }

    private static SymbolContract SymbolFor(string symbol) =>
        Assert.Single(ExpectedSymbols, item => item.Symbol == symbol);

    private static FamilyDefinition FamilyFor(string className) =>
        Assert.Single(Families, item => item.ClassName == className);

    private static SymbolContract MemberFor(FamilyDefinition family, string declaredName) =>
        SymbolFor(family.ClassName + "." + declaredName);

    private static string AliasSummary(FamilyDefinition family) =>
        family.AliasGroups.Length == 0
            ? "none"
            : string.Join(",", family.AliasGroups.Select(group => string.Join("/", group)));

    private static void AssertBinary64(string expectedBinary64, double actual)
    {
        double expected = ParsePythonBinary64(expectedBinary64);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }

    private static double TaggedFloat(JsonElement value)
    {
        AssertKeys(value, "binary64", "kind");
        Assert.Equal("float", RequiredString(value, "kind"));
        string token = RequiredString(value, "binary64");
        Assert.Matches(@"^-?[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+$", token);
        return ParsePythonBinary64(token);
    }

    private static void AssertTaggedFloat(JsonElement value, string expectedBinary64)
    {
        double parsed = TaggedFloat(value);
        Assert.Equal(expectedBinary64, RequiredString(value, "binary64"));
        Assert.Equal(BitConverter.DoubleToInt64Bits(ParsePythonBinary64(expectedBinary64)), BitConverter.DoubleToInt64Bits(parsed));
    }

    private static double ParsePythonBinary64(string value)
    {
        bool negative = value.Length > 0 && value[0] == '-';
        string unsigned = negative ? value[1..] : value;
        int exponentMarker = unsigned.IndexOf('p');
        Assert.True(exponentMarker > 0);
        string mantissaText = unsigned[..exponentMarker];
        int point = mantissaText.IndexOf('.');
        Assert.True(point > 0);
        int fractionDigits = mantissaText.Length - point - 1;
        string digits = mantissaText.Remove(point, 1);
        BigInteger mantissa = BigInteger.Parse("0" + digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        int exponent = int.Parse(unsigned.AsSpan(exponentMarker + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        double result = Math.ScaleB((double)mantissa, exponent - (fractionDigits * 4));
        Assert.True(double.IsFinite(result));
        return negative ? -result : result;
    }

    private static string HexBits(double value) =>
        unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("x16", CultureInfo.InvariantCulture);

    private static void ValidateReceipt(
        JsonElement receipt,
        EvidenceBinding evidence,
        IReadOnlyList<NativeObservation> expectedObservations)
    {
        AssertKeys(receipt, "fixture", "observations", "upstream_path", "upstream_symbol");
        Assert.Equal(evidence.Path, RequiredString(receipt, "upstream_path"));
        Assert.Equal(evidence.Symbol, RequiredString(receipt, "upstream_symbol"));
        JsonElement fixture = receipt.GetProperty("fixture");
        AssertKeys(fixture, "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(ExpectedCaseCount, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));

        JsonElement[] observations = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(3, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            JsonElement observation = observations[index];
            NativeObservation expected = expectedObservations[index];
            AssertKeys(observation, "adaptation_id", "case_id", "native_facts", "native_outcome");
            Assert.Equal(expected.CaseId, RequiredString(observation, "case_id"));
            Assert.Equal("returned", RequiredString(observation, "native_outcome"));
            if (expected.AdaptationId is null)
            {
                Assert.Equal(JsonValueKind.Null, observation.GetProperty("adaptation_id").ValueKind);
            }
            else
            {
                Assert.Equal(expected.AdaptationId, RequiredString(observation, "adaptation_id"));
            }

            AssertStringArray(observation.GetProperty("native_facts"), expected.NativeFacts.ToArray());
        }

        AssertReceiptPayloadSafe(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
    }

    private static void AssertTaggedInteger(JsonElement value, string expectedDecimal)
    {
        AssertKeys(value, "decimal", "kind");
        Assert.Equal("int", RequiredString(value, "kind"));
        Assert.Equal(expectedDecimal, RequiredString(value, "decimal"));
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!).ToArray());
    }

    private static void ValidateTaggedScalarsRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("kind", out JsonElement kindValue) &&
            (value.TryGetProperty("decimal", out _) || value.TryGetProperty("binary64", out _)))
        {
            switch (kindValue.GetString())
            {
                case "int":
                    AssertKeys(value, "decimal", "kind");
                    Assert.Matches(@"^-?(?:0|[1-9][0-9]*)$", RequiredString(value, "decimal"));
                    break;
                case "float":
                    TaggedFloat(value);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException("Unknown tagged fixture scalar kind.");
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                ValidateTaggedScalarsRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateTaggedScalarsRecursive(item);
            }
        }
    }

    private static string CanonicalSha256(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            WriteCanonicalJson(writer, value);
        }

        return Sha256(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
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
                throw new Xunit.Sdk.XunitException("Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
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

    private static void AssertReceiptPayloadSafe(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.False(property.Name is "classification" or "expected_dotnet" or "policy" or "python" or "python_facts" or "python_outcome");
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

    private static void AssertNoRawAddresses(string value)
    {
        Assert.False(Regex.IsMatch(value, @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])", RegexOptions.CultureInvariant));
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            Assert.False(Regex.IsMatch(value.GetString()!, @"^(?:[A-Za-z]:[\\/]|[\\/]{2}|/)", RegexOptions.CultureInvariant));
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

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal), actual);
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
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

    private sealed record EvidenceBinding(string Path, string Symbol, string SymbolHash, string AssertionId);

    private sealed record SymbolContract(
        string Symbol,
        string Kind,
        string SignatureHash,
        string BodyHash,
        string Classification,
        string? AdaptationId,
        string NativeTarget,
        string? ExpectedBinary64,
        string? DeclaredName,
        string? CanonicalName);

    private sealed record FamilyDefinition(
        string ClassName,
        string Slug,
        string ClassExecutor,
        string MemberExecutor,
        string NativeShape,
        string[] DeclaredNames,
        string[][] AliasGroups);

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string? AdaptationId,
        IReadOnlyList<string> NativeFacts);
}
