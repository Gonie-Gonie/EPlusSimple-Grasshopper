using System.Collections.ObjectModel;
using System.Text;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.InvisibleDragon.Results;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

public sealed class GreenRetrofitResultBuildResult
{
    internal GreenRetrofitResultBuildResult(
        GreenRetrofitResult? result,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Result = result;
        Diagnostics = diagnostics;
    }

    public GreenRetrofitResult? Result { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Result is not null && Diagnostics.All(item => !item.IsFailure);

    public GreenRetrofitResult RequireResult()
    {
        if (Success)
        {
            return Result!;
        }

        string message = Diagnostics.Count == 0
            ? "The EnergyPlus output did not produce a GRR result."
            : Diagnostics[0].Code + ": " + Diagnostics[0].Message;
        throw new InvalidOperationException(message);
    }
}

public sealed class GreenRetrofitResultBuildOptions
{
    /// <summary>
    /// Allows conversion of complete tabular output when EnergyPlus emitted one or more
    /// severe diagnostics. Fatal diagnostics always remain blocking.
    /// </summary>
    public bool AllowSevereDiagnostics { get; set; }
}

/// <summary>
/// Transforms InvisibleDragon monthly EnergyPlus tables into the upstream GRR 0.7 result model.
/// </summary>
public static class GreenRetrofitResultBuilder
{
    private static readonly string[] Months =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    private static readonly string[] ProducedElectricityPrefix =
    {
        "ELECTRICITYPRODUCED:FACILITY",
    };

    private static readonly string[] SurplusElectricityPrefix =
    {
        "ELECTRICITYSURPLUSSOLD:FACILITY",
    };

    private static readonly IReadOnlyDictionary<EnergyEndUse, string[]> EndUsePrefixes =
        new ReadOnlyDictionary<EnergyEndUse, string[]>(
            new Dictionary<EnergyEndUse, string[]>
            {
                [EnergyEndUse.Heating] = new[] { "HEATING" },
                [EnergyEndUse.Cooling] = new[] { "COOLING" },
                [EnergyEndUse.Lighting] = new[] { "INTERIORLIGHTS", "EXTERIORLIGHTS" },
                [EnergyEndUse.Equipment] = new[] { "INTERIOREQUIPMENT" },
                [EnergyEndUse.Circulation] = new[] { "FANS", "PUMPS", "HEATRECOVERY" },
                [EnergyEndUse.HotWater] = new[] { "WATERSYSTEMS" },
                [EnergyEndUse.Generators] = Array.Empty<string>(),
            });

    public static GreenRetrofitResultBuildResult Build(
        GreenRetrofitModel model,
        EnergyPlusSimulationResult simulation,
        GreenRetrofitResultBuildOptions? options = null)
    {
        DomainSupport.NotNull(model, nameof(model));
        DomainSupport.NotNull(simulation, nameof(simulation));
        options ??= new GreenRetrofitResultBuildOptions();
        var diagnostics = new List<Diagnostic>();
        if (model.Area <= 0d)
        {
            diagnostics.Add(Error(
                "SD.GRR.MODEL_AREA_INVALID",
                "The GRM has no positive floor area for per-area result conversion.",
                "Add at least one positive floor surface before simulation."));
            return BuildResult(null, diagnostics);
        }

        if (simulation.Tables.Count == 0)
        {
            diagnostics.Add(Error(
                "SD.GRR.MONTHLY_TABLES_MISSING",
                "The EnergyPlus result contains no tabular output.",
                "Enable the InvisibleDragon default monthly output tables and rerun EnergyPlus."));
            return BuildResult(null, diagnostics);
        }

        if (simulation.ErrorLog.Summary.FatalCount > 0
            || (simulation.ErrorLog.Summary.SevereCount > 0
                && !options.AllowSevereDiagnostics))
        {
            diagnostics.Add(Error(
                "SD.GRR.ENERGYPLUS_FAILED",
                "EnergyPlus reported severe or fatal errors; GRR values would be incomplete.",
                "Resolve the EnergyPlus diagnostics and rerun the model."));
            return BuildResult(null, diagnostics);
        }

        if (simulation.ErrorLog.Summary.SevereCount > 0)
        {
            diagnostics.Add(Warning(
                "SD.GRR.ENERGYPLUS_SEVERE_ALLOWED",
                "EnergyPlus reported severe diagnostics, but complete tabular output was retained by explicit request.",
                "Review the EnergyPlus error log before relying on the converted GRR result."));
        }

        var values = CreateMutableMatrix();
        foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
        {
            EnergyPlusTabularTable? table = FindTable(simulation.Tables, ReportName(carrier));
            if (table is null)
            {
                continue;
            }

            foreach (EnergyEndUse endUse in GrrVocabulary.ConsumptionEndUses)
            {
                double[] monthly = ExtractMonthly(table, EndUsePrefixes[endUse], diagnostics);
                for (int month = 0; month < MonthlySeries.MonthCount; month++)
                {
                    values[endUse][carrier][month] = GreenRetrofitResult.Round(
                        monthly[month] / model.Area);
                }
            }
        }

        ApplyDomesticHotWater(model, values);
        ApplyPhotovoltaic(simulation.Tables, model.Area, values, diagnostics);
        EnergyUseBreakdown siteUses = EnergyUseBreakdown.Create(
            (endUse, carrier) => values[endUse][carrier]);
        return BuildResult(
            GreenRetrofitResult.FromSiteUses(model.Area, siteUses),
            diagnostics);
    }

    private static Dictionary<EnergyEndUse, Dictionary<EnergyCarrier, double[]>> CreateMutableMatrix()
    {
        return GrrVocabulary.EndUses.ToDictionary(
            endUse => endUse,
            _ => GrrVocabulary.Carriers.ToDictionary(
                carrier => carrier,
                _ => new double[MonthlySeries.MonthCount]));
    }

    private static void ApplyDomesticHotWater(
        GreenRetrofitModel model,
        Dictionary<EnergyEndUse, Dictionary<EnergyCarrier, double[]>> values)
    {
        foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
        {
            Array.Clear(values[EnergyEndUse.HotWater][carrier], 0, MonthlySeries.MonthCount);
        }

        double[] demand = DomesticHotWaterDemand(model);
        SourceSystem[] servers = model.SourceSystems
            .Where(source => source.HotWaterSupply == true
                && (source.Type == SourceSystemType.Boiler
                    || source.Type == SourceSystemType.DistrictHeating))
            .GroupBy(source => source.Id)
            .Select(group => group.First())
            .ToArray();

        if (servers.Length == 0)
        {
            AddHotWater(
                values,
                EnergyCarrier.NaturalGas,
                demand,
                model.Area,
                serverCount: 1,
                efficiency: 0.85d);
            return;
        }

        foreach (SourceSystem server in servers)
        {
            EnergyCarrier carrier;
            double efficiency;
            if (server.Type == SourceSystemType.DistrictHeating)
            {
                carrier = EnergyCarrier.DistrictHeating;
                efficiency = 1d;
            }
            else
            {
                carrier = ConvertCarrier(server.FuelType);
                efficiency = server.Efficiency ?? 0.85d;
            }

            AddHotWater(values, carrier, demand, model.Area, servers.Length, efficiency);
        }
    }

    private static void AddHotWater(
        Dictionary<EnergyEndUse, Dictionary<EnergyCarrier, double[]>> values,
        EnergyCarrier carrier,
        IReadOnlyList<double> grossDemand,
        double area,
        int serverCount,
        double efficiency)
    {
        double[] target = values[EnergyEndUse.HotWater][carrier];
        for (int month = 0; month < MonthlySeries.MonthCount; month++)
        {
            double perServerPerArea = grossDemand[month] / area / serverCount;
            target[month] = GreenRetrofitResult.Round(
                target[month] + (perServerPerArea / efficiency));
        }
    }

    private static double[] DomesticHotWaterDemand(GreenRetrofitModel model)
    {
        var demand = new double[MonthlySeries.MonthCount];
        DateTime date = new(Schedule.DefaultYear, 1, 1);
        DateTime last = new(Schedule.DefaultYear, 12, 31);
        while (date <= last)
        {
            foreach (Zone zone in model.Zones)
            {
                if (zone.Profile is not null
                    && OperatesOn(zone.Profile, date)
                    && !IsVacation(zone.Profile, date))
                {
                    demand[date.Month - 1] += zone.Profile.DomesticHotWater * zone.Area * 1e-3d;
                }
            }

            date = date.AddDays(1);
        }

        return demand;
    }

    private static bool OperatesOn(UsageProfile profile, DateTime date)
    {
        UsageDay day = date.DayOfWeek switch
        {
            DayOfWeek.Monday => UsageDay.Monday,
            DayOfWeek.Tuesday => UsageDay.Tuesday,
            DayOfWeek.Wednesday => UsageDay.Wednesday,
            DayOfWeek.Thursday => UsageDay.Thursday,
            DayOfWeek.Friday => UsageDay.Friday,
            DayOfWeek.Saturday => UsageDay.Saturday,
            DayOfWeek.Sunday => UsageDay.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(date)),
        };
        return profile.OperatesOn(day);
    }

    private static bool IsVacation(UsageProfile profile, DateTime date)
    {
        int ordinal = (date.Month * 100) + date.Day;
        foreach (VacationPeriod period in profile.Vacations)
        {
            int start = (period.Start.Month * 100) + period.Start.Day;
            int end = (period.End.Month * 100) + period.End.Day;
            bool contains = end >= start
                ? ordinal >= start && ordinal <= end
                : ordinal >= start || ordinal <= end;
            if (contains)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyPhotovoltaic(
        IReadOnlyList<EnergyPlusTabularTable> tables,
        double area,
        Dictionary<EnergyEndUse, Dictionary<EnergyCarrier, double[]>> values,
        List<Diagnostic> diagnostics)
    {
        EnergyPlusTabularTable? table = FindTable(tables, "ElectricityBalanceMonthly");
        if (table is null)
        {
            return;
        }

        double[] produced = ExtractMonthly(
            table,
            ProducedElectricityPrefix,
            diagnostics);
        double[] surplus = ExtractMonthly(
            table,
            SurplusElectricityPrefix,
            diagnostics);
        double[] target = values[EnergyEndUse.Generators][EnergyCarrier.Electricity];
        for (int month = 0; month < MonthlySeries.MonthCount; month++)
        {
            target[month] = GreenRetrofitResult.Round(
                Math.Max(produced[month] - surplus[month], 0d) / area);
        }
    }

    private static double[] ExtractMonthly(
        EnergyPlusTabularTable table,
        IReadOnlyList<string> prefixes,
        List<Diagnostic> diagnostics)
    {
        var values = new double[MonthlySeries.MonthCount];
        if (prefixes.Count == 0)
        {
            return values;
        }

        string[] header = table.Header.Cells.Select(cell => NormalizeHeading(cell.Text)).ToArray();
        int[] monthColumns = Months.Select(month => FindHeading(header, month)).ToArray();
        bool monthsAreColumns = monthColumns.All(index => index >= 0);
        if (monthsAreColumns)
        {
            foreach (EnergyPlusTabularRow row in table.Rows)
            {
                string label = FirstLabel(row);
                if (!prefixes.Any(prefix => NormalizeHeading(label).StartsWith(
                        NormalizeHeading(prefix),
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                for (int month = 0; month < MonthlySeries.MonthCount; month++)
                {
                    values[month] += Numeric(row, monthColumns[month]);
                }
            }

            return values;
        }

        int[] valueColumns = header
            .Select((heading, index) => new { heading, index })
            .Where(item => prefixes.Any(prefix => item.heading.StartsWith(
                NormalizeHeading(prefix),
                StringComparison.Ordinal)))
            .Select(item => item.index)
            .ToArray();
        if (valueColumns.Length == 0)
        {
            diagnostics.Add(Warning(
                "SD.GRR.TABLE_END_USE_NOT_FOUND",
                "Table '" + table.ReportName + "' has no columns or rows matching "
                + string.Join(", ", prefixes) + ".",
                "Verify that EnergyPlus produced the standard monthly end-use reports."));
            return values;
        }

        foreach (EnergyPlusTabularRow row in table.Rows)
        {
            int month = FindMonth(FirstLabel(row));
            if (month < 0)
            {
                continue;
            }

            values[month] = valueColumns.Sum(column => Numeric(row, column));
        }

        return values;
    }

    private static int FindMonth(string value)
    {
        for (int index = 0; index < Months.Length; index++)
        {
            if (NormalizeHeading(value).StartsWith(
                    NormalizeHeading(Months[index]),
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindHeading(IReadOnlyList<string> headings, string expected)
    {
        string normalized = NormalizeHeading(expected);
        for (int index = 0; index < headings.Count; index++)
        {
            if (headings[index].StartsWith(normalized, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static double Numeric(EnergyPlusTabularRow row, int index)
    {
        return index >= 0 && index < row.Cells.Count
            ? row.Cells[index].NumericValue ?? 0d
            : 0d;
    }

    private static string FirstLabel(EnergyPlusTabularRow row)
    {
        return row.Cells.FirstOrDefault(cell => cell.Text.Length > 0)?.Text ?? string.Empty;
    }

    private static EnergyPlusTabularTable? FindTable(
        IEnumerable<EnergyPlusTabularTable> tables,
        string expected)
    {
        string normalized = NormalizeHeading(expected);
        return tables.FirstOrDefault(table =>
            NormalizeHeading(table.ReportName) == normalized
            || NormalizeHeading(table.Title) == normalized);
    }

    private static string NormalizeHeading(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character) || character == ':')
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string ReportName(EnergyCarrier carrier)
    {
        return carrier switch
        {
            EnergyCarrier.Electricity => "EndUseEnergyConsumptionElectricityMonthly",
            EnergyCarrier.NaturalGas => "EndUseEnergyConsumptionNaturalGasMonthly",
            EnergyCarrier.LiquefiedPetroleumGas => "EndUseEnergyConsumptionPropaneMonthly",
            EnergyCarrier.Oil => "EndUseEnergyConsumptionDieselMonthly",
            EnergyCarrier.DistrictHeating => "EndUseEnergyConsumptionOtherFuelsMonthly",
            _ => throw new ArgumentOutOfRangeException(nameof(carrier)),
        };
    }

    private static EnergyCarrier ConvertCarrier(FuelType? fuel)
    {
        return fuel switch
        {
            FuelType.Electricity => EnergyCarrier.Electricity,
            FuelType.NaturalGas => EnergyCarrier.NaturalGas,
            FuelType.LiquefiedPetroleumGas => EnergyCarrier.LiquefiedPetroleumGas,
            FuelType.Oil => EnergyCarrier.Oil,
            FuelType.DistrictHeating => EnergyCarrier.DistrictHeating,
            null => throw new InvalidOperationException("A hot-water boiler has no fuel type."),
            _ => throw new ArgumentOutOfRangeException(nameof(fuel)),
        };
    }

    private static GreenRetrofitResultBuildResult BuildResult(
        GreenRetrofitResult? result,
        IEnumerable<Diagnostic> diagnostics)
    {
        return new GreenRetrofitResultBuildResult(
            result,
            new ReadOnlyCollection<Diagnostic>(diagnostics.ToArray()));
    }

    private static Diagnostic Error(string code, string message, string action)
    {
        return new Diagnostic(code, DiagnosticSeverity.Error, message, suggestedAction: action);
    }

    private static Diagnostic Warning(string code, string message, string action)
    {
        return new Diagnostic(code, DiagnosticSeverity.Warning, message, suggestedAction: action);
    }
}
