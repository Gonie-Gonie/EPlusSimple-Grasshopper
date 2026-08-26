using System.Reflection;
using GonieGonie.InvisibleDragon.Idf;
using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelDefaultDocumentTests
{
    private static readonly string[] PinnedObjectTypes =
    {
        "Version",
        "SimulationControl",
        "Timestep",
        "SizingPeriod:WeatherFileDays",
        "SizingPeriod:WeatherFileDays",
        "RunPeriod",
        "ScheduleTypeLimits",
        "ScheduleTypeLimits",
        "ScheduleTypeLimits",
        "ScheduleTypeLimits",
        "Schedule:Compact",
        "Schedule:Compact",
        "Schedule:Constant",
        "GlobalGeometryRules",
        "Output:Table:SummaryReports",
        "Output:Table:Monthly",
        "OutputControl:Table:Style",
    };

    [Fact]
    public void SupportedVersionsIsFreshReadOnlyAndPinnedToEnergyPlus242()
    {
        IReadOnlyList<EnergyPlusVersion> first = EnergyModel.SupportedVersions;
        IReadOnlyList<EnergyPlusVersion> second = EnergyModel.SupportedVersions;

        Assert.NotSame(first, second);
        EnergyPlusVersion firstVersion = Assert.Single(first);
        EnergyPlusVersion secondVersion = Assert.Single(second);
        Assert.Equal(new[] { 24, 2, 0 }, firstVersion.ToArray());
        Assert.Equal(firstVersion.ToArray(), secondVersion.ToArray());
        Assert.Equal(EnergyPlusDefaults.DefaultVersion.ToArray(), firstVersion.ToArray());

        IList<EnergyPlusVersion> list = Assert.IsAssignableFrom<IList<EnergyPlusVersion>>(first);
        Assert.Throws<NotSupportedException>(() => list.Add(new EnergyPlusVersion(25, 1, 0)));
        Assert.Throws<NotSupportedException>(() => list[0] = new EnergyPlusVersion(25, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = first[1];
        });
        Assert.Equal(new[] { 24, 2, 0 }, Assert.Single(EnergyModel.SupportedVersions).ToArray());
    }

    [Fact]
    public void PublicFactoryCreatesExactPinnedSeventeenObjectGraphWithoutBuilding()
    {
        IdfDocument document = EnergyModel.CreateDefaultIdfDocument();

        Assert.Equal(17, document.Count);
        Assert.Equal(PinnedObjectTypes, document.Select(item => item.ObjectType));
        Assert.Empty(document["Building"]);
        Assert.Empty(document.PreambleComments);
        Assert.Empty(document.TrailingComments);
        Assert.Null(document.Schema);
        Assert.All(document, item => Assert.Null(item.Definition));
    }

    [Fact]
    public void PublicFactoryPreservesEveryPinnedRawDefaultField()
    {
        IdfDocument document = EnergyModel.CreateDefaultIdfDocument();

        AssertFields(Assert.Single(document["Version"]), "24.2");
        AssertFields(
            Assert.Single(document["SimulationControl"]),
            "Yes", "Yes", "Yes", "No", "Yes", "No");
        AssertFields(Assert.Single(document["Timestep"]), "6");
        Assert.Equal(2, document["SizingPeriod:WeatherFileDays"].Count);
        AssertFields(
            document["SizingPeriod:WeatherFileDays"][0],
            "DesignWinter", "1", "1", "1", "31");
        AssertFields(
            document["SizingPeriod:WeatherFileDays"][1],
            "DesignSummer", "8", "1", "8", "31");
        AssertFields(
            Assert.Single(document["RunPeriod"]),
            "Year-Round", "1", "1", "2026", "12", "31", "2026");

        IReadOnlyList<IdfObject> limits = document["ScheduleTypeLimits"];
        Assert.Equal(4, limits.Count);
        AssertFields(
            limits[0],
            "ScheduleTypeLimits:Temperature", "-50", "200", "Continuous", "Temperature");
        AssertFields(
            limits[1],
            "ScheduleTypeLimits:Onoff", "0", "1", "Discrete", "Dimensionless");
        AssertFields(
            limits[2],
            "ScheduleTypeLimits:Fraction", "0", "1", "Continuous", "Dimensionless");
        AssertFields(
            limits[3],
            "ScheduleTypeLimits:Real", string.Empty, string.Empty, "Continuous", "Dimensionless");

        IReadOnlyList<IdfObject> compact = document["Schedule:Compact"];
        Assert.Equal(2, compact.Count);
        AssertFields(
            compact[0],
            "ALLON", string.Empty, "Through: 12/31", "For: AllDays", "Until: 24:00", "1");
        AssertFields(
            compact[1],
            "ALLOFF", string.Empty, "Through: 12/31", "For: AllDays", "Until: 24:00", "0");
        AssertFields(
            Assert.Single(document["Schedule:Constant"]),
            "$DEFAULT$PEOPLEACTIVITY", "real", "107.0");
        AssertFields(
            Assert.Single(document["GlobalGeometryRules"]),
            "UpperLeftCorner", "Counterclockwise", "World", "Relative", "Relative");

        AssertFields(
            Assert.Single(document["Output:Table:SummaryReports"]),
            "EndUseEnergyConsumptionElectricityMonthly",
            "EndUseEnergyConsumptionNaturalGasMonthly",
            "EndUseEnergyConsumptionDieselMonthly",
            "EndUseEnergyConsumptionFuelOilMonthly",
            "EndUseEnergyConsumptionCoalMonthly",
            "EndUseEnergyConsumptionPropaneMonthly",
            "EndUseEnergyConsumptionGasolineMonthly",
            "EndUseEnergyConsumptionOtherFuelsMonthly");
        AssertFields(
            Assert.Single(document["Output:Table:Monthly"]),
            "ElectricityBalanceMonthly",
            "3",
            "ElectricityProduced:Facility",
            "SumOrAverage",
            "ElectricitySurplusSold:Facility",
            "SumOrAverage",
            "ElectricityPurchased:Facility",
            "SumOrAverage");
        AssertFields(
            Assert.Single(document["OutputControl:Table:Style"]),
            "Comma", "JtoKWH");
    }

    [Fact]
    public void PublicFactoryReturnsIndependentDocumentsObjectsAndFields()
    {
        IdfDocument first = EnergyModel.CreateDefaultIdfDocument();
        IdfDocument second = EnergyModel.CreateDefaultIdfDocument();

        Assert.NotSame(first, second);
        for (int index = 0; index < first.Count; index++)
        {
            Assert.NotSame(first[index], second[index]);
            Assert.Equal(first[index].Count, second[index].Count);
            for (int fieldIndex = 0; fieldIndex < first[index].Count; fieldIndex++)
            {
                Assert.NotSame(first[index].Fields[fieldIndex], second[index].Fields[fieldIndex]);
            }
        }

        first["Timestep"][0][0] = "12";
        first["GlobalGeometryRules"][0][0] = "LowerLeftCorner";
        first.Append(new IdfObject("Building", new[] { "Mutated" }));

        Assert.Equal(18, first.Count);
        Assert.Equal("6", Assert.Single(second["Timestep"])[0]);
        Assert.Equal("UpperLeftCorner", Assert.Single(second["GlobalGeometryRules"])[0]);
        Assert.Empty(second["Building"]);
        Assert.Equal(PinnedObjectTypes, second.Select(item => item.ObjectType));

        IdfDocument third = EnergyModel.CreateDefaultIdfDocument();
        Assert.Equal("6", Assert.Single(third["Timestep"])[0]);
        Assert.Equal("UpperLeftCorner", Assert.Single(third["GlobalGeometryRules"])[0]);
        Assert.Empty(third["Building"]);
    }

    [Fact]
    public void PublicFactoryHasTheExactParameterlessStaticCallShapeAndRejectsArguments()
    {
        MethodInfo method = Assert.Single(
            typeof(EnergyModel)
                .GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == nameof(EnergyModel.CreateDefaultIdfDocument));

        Assert.Empty(method.GetParameters());
        Assert.Equal(typeof(IdfDocument), method.ReturnType);
        Assert.Throws<TargetParameterCountException>(() =>
        {
            _ = method.Invoke(null, new object?[] { null });
        });
        Assert.Null(typeof(EnergyModel).GetMethod(
            nameof(EnergyModel.CreateDefaultIdfDocument),
            new[] { typeof(object) }));
    }

    private static void AssertFields(IdfObject item, params string[] expected)
    {
        Assert.Equal(expected, item.Fields.Select(field => field.Value));
    }
}
