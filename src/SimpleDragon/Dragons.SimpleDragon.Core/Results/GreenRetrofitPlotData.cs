using System.Collections.ObjectModel;
using Dragons.SimpleDragon.Internal;

namespace Dragons.SimpleDragon;

public enum GreenRetrofitSeriesGrouping
{
    Fuel,
    EndUse,
}

/// <summary>
/// One named January-to-December series suitable for Grasshopper or plotting tools.
/// </summary>
public sealed class GreenRetrofitMonthlySeries
{
    internal GreenRetrofitMonthlySeries(string name, IEnumerable<double> values)
    {
        Name = DomainSupport.RequiredText(name, nameof(name));
        Values = new MonthlySeries(values);
    }

    public string Name { get; }

    public MonthlySeries Values { get; }
}

/// <summary>
/// Stable monthly plot/data payload independent of Rhino and Grasshopper.
/// </summary>
public sealed class GreenRetrofitMonthlyData
{
    private static readonly ReadOnlyCollection<int> MonthNumbers =
        new(Enumerable.Range(1, MonthlySeries.MonthCount).ToArray());

    private static readonly ReadOnlyCollection<string> MonthNames = new(new[]
    {
        "january",
        "february",
        "march",
        "april",
        "may",
        "june",
        "july",
        "august",
        "september",
        "october",
        "november",
        "december",
    });

    private GreenRetrofitMonthlyData(
        GreenRetrofitMetric metric,
        bool gross,
        GreenRetrofitSeriesGrouping grouping,
        IEnumerable<GreenRetrofitMonthlySeries> series)
    {
        Metric = metric;
        Gross = gross;
        Grouping = grouping;
        Unit = UnitName(metric, gross);
        XValues = MonthNumbers;
        XLabels = MonthNames;
        Series = new ReadOnlyCollection<GreenRetrofitMonthlySeries>(series.ToArray());
    }

    public GreenRetrofitMetric Metric { get; }

    public bool Gross { get; }

    public GreenRetrofitSeriesGrouping Grouping { get; }

    public string Unit { get; }

    public IReadOnlyList<int> XValues { get; }

    public IReadOnlyList<string> XLabels { get; }

    public IReadOnlyList<GreenRetrofitMonthlySeries> Series { get; }

    public static GreenRetrofitMonthlyData Create(
        GreenRetrofitResult result,
        GreenRetrofitMetric metric,
        bool gross,
        GreenRetrofitSeriesGrouping grouping)
    {
        DomainSupport.NotNull(result, nameof(result));
        if (!Enum.IsDefined(typeof(GreenRetrofitMetric), metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown GRR metric.");
        }

        if (!Enum.IsDefined(typeof(GreenRetrofitSeriesGrouping), grouping))
        {
            throw new ArgumentOutOfRangeException(nameof(grouping), grouping, "Unknown result grouping.");
        }

        EnergyUseBreakdown breakdown = result.Metrics[metric];
        IEnumerable<GreenRetrofitMonthlySeries> series = grouping switch
        {
            GreenRetrofitSeriesGrouping.Fuel => GrrVocabulary.Carriers.Select(carrier =>
                new GreenRetrofitMonthlySeries(
                    GrrVocabulary.CarrierConstantName(carrier),
                    Enumerable.Range(0, MonthlySeries.MonthCount).Select(month =>
                        Scale(FuelValue(breakdown, carrier, month), result.TotalArea, gross)))),
            GreenRetrofitSeriesGrouping.EndUse => GrrVocabulary.EndUses.Select(endUse =>
                new GreenRetrofitMonthlySeries(
                    GrrVocabulary.EndUseName(endUse),
                    Enumerable.Range(0, MonthlySeries.MonthCount).Select(month =>
                        Scale(EndUseValue(breakdown, endUse, month), result.TotalArea, gross)))),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
        };
        return new GreenRetrofitMonthlyData(metric, gross, grouping, series);
    }

    internal static string UnitName(GreenRetrofitMetric metric, bool gross)
    {
        return metric switch
        {
            GreenRetrofitMetric.SiteUses => gross ? "kWh" : "kWh/m2",
            GreenRetrofitMetric.SourceUses => gross ? "kWh" : "kWh/m2",
            GreenRetrofitMetric.Carbon => gross ? "kgCO2e" : "kgCO2e/m2",
            GreenRetrofitMetric.Cost => gross ? "KRW" : "KRW/m2",
            _ => throw new ArgumentOutOfRangeException(nameof(metric)),
        };
    }

    private static double FuelValue(
        EnergyUseBreakdown breakdown,
        EnergyCarrier carrier,
        int month)
    {
        double consumption = GrrVocabulary.ConsumptionEndUses.Sum(
            endUse => breakdown[endUse, carrier][month]);
        double generation = breakdown[EnergyEndUse.Generators, carrier][month];
        return GreenRetrofitResult.Round(consumption - generation);
    }

    private static double EndUseValue(
        EnergyUseBreakdown breakdown,
        EnergyEndUse endUse,
        int month)
    {
        return GreenRetrofitResult.Round(
            GrrVocabulary.Carriers.Sum(carrier => breakdown[endUse, carrier][month]));
    }

    private static double Scale(double value, double area, bool gross)
    {
        return gross ? GreenRetrofitResult.Round(value * area) : value;
    }
}
