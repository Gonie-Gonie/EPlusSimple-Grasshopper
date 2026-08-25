using GonieGonie.InvisibleDragon.Results;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class GreenRetrofitResultBuilderTests
{
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

    private static readonly string[] BalanceHeadings =
    {
        "ElectricityProduced:Facility [kWh]",
        "ElectricitySurplusSold:Facility [kWh]",
    };

    private static readonly string[] UnusedHeadings = { "Value" };

    private static readonly double[] ZeroValues = { 0d };

    [Fact]
    public void MonthlyRowsProduceSiteMetricsDomesticHotWaterAndPhotovoltaics()
    {
        GreenRetrofitModel model = LoadModel();
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
            BalanceHeadings,
            month => new[] { 96d * month, 48d * month });

        GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(electricity, balance));

        Assert.True(build.Success, Describe(build));
        Assert.Empty(build.Diagnostics);
        GreenRetrofitResult result = build.RequireResult();
        Assert.Equal(1d, result.SiteUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]);
        Assert.Equal(12d, result.SiteUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][11]);
        Assert.Equal(0.5d, result.SiteUses[EnergyEndUse.Cooling, EnergyCarrier.Electricity][0]);
        Assert.Equal(0.15d, result.SiteUses[EnergyEndUse.Lighting, EnergyCarrier.Electricity][0]);
        Assert.Equal(0.2d, result.SiteUses[EnergyEndUse.Circulation, EnergyCarrier.Electricity][0]);
        Assert.Equal(1d, result.SiteUses[EnergyEndUse.Generators, EnergyCarrier.Electricity][0]);

        Assert.Equal(0d, result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.Electricity][0]);
        Assert.Equal(0d, result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.NaturalGas][0]);
        Assert.Equal(
            0.78d,
            result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.LiquefiedPetroleumGas][0]);
        Assert.Equal(
            0.71d,
            result.SiteUses[EnergyEndUse.HotWater, EnergyCarrier.LiquefiedPetroleumGas][1]);
        Assert.Equal(2.75d, result.SourceUses[EnergyEndUse.Heating, EnergyCarrier.Electricity][0]);
        Assert.Equal(
            0.57d,
            result.SourceUses[EnergyEndUse.HotWater, EnergyCarrier.LiquefiedPetroleumGas][0]);
        Assert.Equal(1.83d, result.PerAreaSummaries[GreenRetrofitMetric.SiteUses].MonthlyTotal[0]);
        Assert.Equal(87.84d, result.GrossSummaries[GreenRetrofitMetric.SiteUses].MonthlyTotal[0]);
    }

    [Fact]
    public void MonthlyColumnsAreAcceptedIndependentlyOfTableOrientation()
    {
        GreenRetrofitModel model = LoadModel();
        EnergyPlusTabularTable electricity = MonthlyColumns(
            "EndUseEnergyConsumptionElectricityMonthly",
            new Dictionary<string, Func<int, double>>(StringComparer.Ordinal)
            {
                ["Heating [kWh]"] = month => 48d * month,
                ["Cooling [kWh]"] = month => 24d * month,
                ["Interior Lights [kWh]"] = month => 4.8d * month,
                ["Exterior Lights [kWh]"] = month => 2.4d * month,
                ["Interior Equipment [kWh]"] = month => 9.6d * month,
                ["Fans [kWh]"] = month => 4.8d * month,
                ["Pumps [kWh]"] = month => 2.4d * month,
                ["Heat Recovery [kWh]"] = month => 2.4d * month,
                ["Water Systems [kWh]"] = _ => 999d,
            });

        GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(electricity));

        Assert.True(build.Success, Describe(build));
        Assert.Empty(build.Diagnostics);
        GreenRetrofitResult result = build.RequireResult();
        Assert.Equal(Enumerable.Range(1, 12).Select(value => (double)value),
            result.SiteUses[EnergyEndUse.Heating, EnergyCarrier.Electricity]);
        Assert.Equal(1.8d, result.SiteUses[EnergyEndUse.Lighting, EnergyCarrier.Electricity][11]);
        Assert.All(
            result.SiteUses[EnergyEndUse.Generators, EnergyCarrier.Electricity],
            value => Assert.Equal(0d, value));
    }

    [Fact]
    public void MissingTablesAndFailedSimulationReturnStableErrors()
    {
        GreenRetrofitModel model = LoadModel();

        GreenRetrofitResultBuildResult missing = GreenRetrofitResultBuilder.Build(
            model,
            Simulation());
        GreenRetrofitResultBuildResult failed = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(
                new[] { MonthlyRows("Unused", UnusedHeadings, _ => ZeroValues) },
                severeCount: 1));

        Assert.False(missing.Success);
        Assert.Null(missing.Result);
        Assert.Equal("SD.GRR.MONTHLY_TABLES_MISSING", Assert.Single(missing.Diagnostics).Code);
        Assert.False(failed.Success);
        Assert.Null(failed.Result);
        Assert.Equal("SD.GRR.ENERGYPLUS_FAILED", Assert.Single(failed.Diagnostics).Code);
    }

    [Fact]
    public void ExplicitCompatibilityOptionRetainsCompleteTablesWithSevereDiagnostics()
    {
        GreenRetrofitModel model = LoadModel();
        EnergyPlusTabularTable electricity = MonthlyRows(
            "EndUseEnergyConsumptionElectricityMonthly",
            ElectricityHeadings,
            _ => new double[ElectricityHeadings.Length]);

        GreenRetrofitResultBuildResult build = GreenRetrofitResultBuilder.Build(
            model,
            Simulation(new[] { electricity }, severeCount: 1),
            new GreenRetrofitResultBuildOptions
            {
                AllowSevereDiagnostics = true,
            });

        Assert.True(build.Success, Describe(build));
        Assert.NotNull(build.Result);
        Assert.Equal(
            "SD.GRR.ENERGYPLUS_SEVERE_ALLOWED",
            Assert.Single(build.Diagnostics).Code);
    }

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
        return Table(reportName, header, rows);
    }

    private static EnergyPlusTabularTable MonthlyColumns(
        string reportName,
        IReadOnlyDictionary<string, Func<int, double>> values)
    {
        EnergyPlusTabularCell[] header = new[] { Text("End Use") }
            .Concat(Months.Select(Text))
            .ToArray();
        EnergyPlusTabularRow[] rows = values
            .Select(item => new EnergyPlusTabularRow(
                new[] { Text(item.Key) }
                    .Concat(Enumerable.Range(1, 12).Select(month => Number(item.Value(month))))
                    .ToArray()))
            .ToArray();
        return Table(reportName, header, rows);
    }

    private static EnergyPlusTabularTable Table(
        string reportName,
        IReadOnlyList<EnergyPlusTabularCell> header,
        IReadOnlyList<EnergyPlusTabularRow> rows)
    {
        return new EnergyPlusTabularTable(
            "eplustbl.csv",
            reportName,
            "Entire Facility",
            new[] { reportName },
            new EnergyPlusTabularRow(header),
            rows,
            isMonthly: true);
    }

    private static EnergyPlusTabularCell Text(string value)
    {
        return new EnergyPlusTabularCell(value, null);
    }

    private static EnergyPlusTabularCell Number(double value)
    {
        return new EnergyPlusTabularCell(
            value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value);
    }

    private static EnergyPlusSimulationResult Simulation(params EnergyPlusTabularTable[] tables)
    {
        return Simulation(tables, severeCount: 0);
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

    private static GreenRetrofitModel LoadModel()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(
                current.FullName,
                "fixtures",
                "simple-dragon",
                "grm",
                "ASHRAE 140 modified.grm");
            if (File.Exists(candidate))
            {
                return GrmReader.ReadFile(candidate).RequireModel();
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GRM fixture.");
    }

    private static string Describe(GreenRetrofitResultBuildResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(item => item.Code + ": " + item.Message));
    }
}
