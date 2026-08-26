using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class UtilsProductionSurfaceTests
{
    private const string ExpectedCompactTemplate =
        "{\"building\":{\"name\":\"\",\"north_axis\":0,\"address\":\"\",\"vintage\":[1900,1,1]," +
        "\"num_aboveground_floors\":0,\"num_underground_floors\":0,\"floors\":[]," +
        "\"supply_systems\":[],\"source_systems\":[],\"ventilation_systems\":[]," +
        "\"photovoltaic_systems\":[]},\"materials\":[],\"surface_constructions\":[]," +
        "\"fenestration_constructions\":[]}";

    private static readonly string[] BuildingKeys =
    {
        "name",
        "north_axis",
        "address",
        "vintage",
        "num_aboveground_floors",
        "num_underground_floors",
        "floors",
        "supply_systems",
        "source_systems",
        "ventilation_systems",
        "photovoltaic_systems",
    };

    private static readonly string[] RootKeys =
    {
        "building",
        "materials",
        "surface_constructions",
        "fenestration_constructions",
    };

    private static readonly int[] Vintage = { 1900, 1, 1 };

    private static readonly string[] EmptyBuildingListKeys =
    {
        "floors",
        "supply_systems",
        "source_systems",
        "ventilation_systems",
        "photovoltaic_systems",
    };

    private static readonly string[] EmptyRootListKeys =
    {
        "materials",
        "surface_constructions",
        "fenestration_constructions",
    };

    [Fact]
    public void LegacyInputTemplateMatchesPinnedUpstreamOrderValuesAndCompactHash()
    {
        OrderedMap<object> template = GrmFormat.CreateLegacyInputTemplate();

        Assert.Equal(RootKeys, template.Keys);
        OrderedMap<object> building = Assert.IsType<OrderedMap<object>>(template["building"]);
        Assert.Equal(BuildingKeys, building.Keys);
        Assert.Equal(string.Empty, Assert.IsType<string>(building["name"]));
        Assert.Equal(0, Assert.IsType<int>(building["north_axis"]));
        Assert.Equal(string.Empty, Assert.IsType<string>(building["address"]));
        Assert.Equal(
            Vintage,
            Assert.IsAssignableFrom<IReadOnlyList<int>>(building["vintage"]));
        Assert.Equal(0, Assert.IsType<int>(building["num_aboveground_floors"]));
        Assert.Equal(0, Assert.IsType<int>(building["num_underground_floors"]));
        Assert.All(EmptyBuildingListKeys, key => Assert.Empty(EmptyList(building[key])));
        Assert.All(EmptyRootListKeys, key => Assert.Empty(EmptyList(template[key])));

        string compact = JsonSerializer.Serialize(template);
        byte[] bytes = Encoding.UTF8.GetBytes(compact);
        string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        Assert.Equal(ExpectedCompactTemplate, compact);
        Assert.Equal(304, bytes.Length);
        Assert.Equal("abbc0cbf3cd7b5dbfae88d9315ab4ae2b08da9ee28077382d6aaba1e1f6d29f1", sha256);
    }

    [Fact]
    public void LegacyInputTemplateCallsReturnFreshDeeplyReadOnlyTrees()
    {
        OrderedMap<object> first = GrmFormat.CreateLegacyInputTemplate();
        OrderedMap<object> second = GrmFormat.CreateLegacyInputTemplate();
        OrderedMap<object> firstBuilding = Assert.IsType<OrderedMap<object>>(first["building"]);
        OrderedMap<object> secondBuilding = Assert.IsType<OrderedMap<object>>(second["building"]);

        Assert.NotSame(first, second);
        Assert.NotSame(firstBuilding, secondBuilding);
        Assert.DoesNotContain(typeof(IDictionary<string, object>), first.GetType().GetInterfaces());
        Assert.DoesNotContain(
            typeof(IDictionary<string, object>),
            firstBuilding.GetType().GetInterfaces());

        IList<int> firstVintage = Assert.IsAssignableFrom<IList<int>>(firstBuilding["vintage"]);
        IList<int> secondVintage = Assert.IsAssignableFrom<IList<int>>(secondBuilding["vintage"]);
        Assert.NotSame(firstVintage, secondVintage);
        Assert.True(firstVintage.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => firstVintage[0] = 2000);

        foreach (string key in EmptyBuildingListKeys)
        {
            AssertFreshReadOnlyEmptyList(firstBuilding[key], secondBuilding[key]);
        }

        foreach (string key in EmptyRootListKeys)
        {
            AssertFreshReadOnlyEmptyList(first[key], second[key]);
        }

        OrderedMap<object> changed = first.SetItem("materials", Array.AsReadOnly(new object[] { "probe" }));
        Assert.Empty(EmptyList(first["materials"]));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object>>(changed["materials"]));
        Assert.Equal(ExpectedCompactTemplate, JsonSerializer.Serialize(second));
    }

    [Fact]
    public void DefinedNullableEnumGuardPreservesNullAndDefinedBoundaryMembers()
    {
        Assert.Null(DomainSupport.DefinedEnumOrNull<BlindType>(null, "value"));
        Assert.Equal(
            FuelType.Electricity,
            DomainSupport.DefinedEnumOrNull<FuelType>(FuelType.Electricity, "value"));
        Assert.Equal(
            FuelType.DistrictHeating,
            DomainSupport.DefinedEnumOrNull<FuelType>(FuelType.DistrictHeating, "value"));
        Assert.Equal(
            CompressorType.Turbo,
            DomainSupport.DefinedEnumOrNull<CompressorType>(CompressorType.Turbo, "value"));
        Assert.Equal(
            CompressorType.Reciprocating,
            DomainSupport.DefinedEnumOrNull<CompressorType>(CompressorType.Reciprocating, "value"));
        Assert.Equal(
            CoolingTowerType.Closed,
            DomainSupport.DefinedEnumOrNull<CoolingTowerType>(CoolingTowerType.Closed, "value"));
        Assert.Equal(
            CoolingTowerType.Open,
            DomainSupport.DefinedEnumOrNull<CoolingTowerType>(CoolingTowerType.Open, "value"));
        Assert.Equal(
            CoolingTowerControl.SingleSpeed,
            DomainSupport.DefinedEnumOrNull<CoolingTowerControl>(CoolingTowerControl.SingleSpeed, "value"));
        Assert.Equal(
            CoolingTowerControl.TwoSpeed,
            DomainSupport.DefinedEnumOrNull<CoolingTowerControl>(CoolingTowerControl.TwoSpeed, "value"));
        Assert.Equal(
            BlindType.Shade,
            DomainSupport.DefinedEnumOrNull<BlindType>(BlindType.Shade, "value"));
        Assert.Equal(
            BlindType.Venetian,
            DomainSupport.DefinedEnumOrNull<BlindType>(BlindType.Venetian, "value"));

        var source = new SourceSystem(
            "district source",
            SourceSystemType.DistrictHeating,
            hotWaterSupply: true);
        var opening = new Fenestration(
            "unshaded window",
            FenestrationType.Window,
            1d,
            "CTFN-TEST");
        Assert.Null(source.FuelType);
        Assert.Null(source.CompressorType);
        Assert.Null(source.CoolingTowerType);
        Assert.Null(source.CoolingTowerControl);
        Assert.Null(opening.Blind);
    }

    [Fact]
    public void SourceSystemAndFenestrationRejectUndefinedNullableEnumsAtConstruction()
    {
        AssertOutOfRange(
            "fuelType",
            () => _ = new SourceSystem(
                "invalid fuel",
                SourceSystemType.HeatPump,
                (FuelType)int.MaxValue));
        AssertOutOfRange(
            "compressorType",
            () => _ = new SourceSystem(
                "invalid compressor",
                SourceSystemType.Chiller,
                compressorType: (CompressorType)int.MaxValue,
                coolingTowerType: CoolingTowerType.Open,
                coolingTowerControl: CoolingTowerControl.SingleSpeed));
        AssertOutOfRange(
            "coolingTowerType",
            () => _ = new SourceSystem(
                "invalid tower",
                SourceSystemType.Chiller,
                compressorType: CompressorType.Turbo,
                coolingTowerType: (CoolingTowerType)int.MaxValue,
                coolingTowerControl: CoolingTowerControl.SingleSpeed));
        AssertOutOfRange(
            "coolingTowerControl",
            () => _ = new SourceSystem(
                "invalid control",
                SourceSystemType.Chiller,
                compressorType: CompressorType.Turbo,
                coolingTowerType: CoolingTowerType.Open,
                coolingTowerControl: (CoolingTowerControl)int.MaxValue));
        AssertOutOfRange(
            "blind",
            () => _ = new Fenestration(
                "invalid blind",
                FenestrationType.Window,
                1d,
                "CTFN-TEST",
                blind: (BlindType)int.MaxValue));
    }

    [Fact]
    public void GrmVocabularyMapsEveryBinaryEnumAndFailsClosedForUndefinedValues()
    {
        Assert.Equal("shade", GrmVocabulary.ToGrm(BlindType.Shade));
        Assert.Equal("venetian", GrmVocabulary.ToGrm(BlindType.Venetian));
        Assert.Equal("closed", GrmVocabulary.ToGrm(CoolingTowerType.Closed));
        Assert.Equal("open", GrmVocabulary.ToGrm(CoolingTowerType.Open));
        Assert.Equal("single-speed", GrmVocabulary.ToGrm(CoolingTowerControl.SingleSpeed));
        Assert.Equal("two-speed", GrmVocabulary.ToGrm(CoolingTowerControl.TwoSpeed));

        AssertOutOfRange("value", () => GrmVocabulary.ToGrm((BlindType)int.MaxValue));
        AssertOutOfRange("value", () => GrmVocabulary.ToGrm((CoolingTowerType)int.MaxValue));
        AssertOutOfRange("value", () => GrmVocabulary.ToGrm((CoolingTowerControl)int.MaxValue));
    }

    private static IReadOnlyList<object> EmptyList(object value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<object>>(value);
    }

    private static void AssertFreshReadOnlyEmptyList(object first, object second)
    {
        IList<object> firstList = Assert.IsAssignableFrom<IList<object>>(first);
        IList<object> secondList = Assert.IsAssignableFrom<IList<object>>(second);
        Assert.NotSame(firstList, secondList);
        Assert.True(firstList.IsReadOnly);
        Assert.Empty(firstList);
        Assert.Throws<NotSupportedException>(() => firstList.Add("probe"));
    }

    private static void AssertOutOfRange(string parameterName, Action action)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal(parameterName, exception.ParamName);
    }
}
