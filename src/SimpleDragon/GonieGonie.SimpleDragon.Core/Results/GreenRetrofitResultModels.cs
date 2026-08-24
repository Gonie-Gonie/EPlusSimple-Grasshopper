using System.Collections;
using System.Collections.ObjectModel;
using System.Numerics;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

public enum GreenRetrofitMetric
{
    SiteUses,
    SourceUses,
    Carbon,
    Cost,
}

public enum EnergyEndUse
{
    Heating,
    Cooling,
    Lighting,
    Equipment,
    Circulation,
    HotWater,
    Generators,
}

/// <summary>
/// An immutable January-to-December sequence used by GRR 0.7.
/// </summary>
public sealed class MonthlySeries : IReadOnlyList<double>, IEquatable<MonthlySeries>
{
    public const int MonthCount = 12;
    private readonly ReadOnlyCollection<double> _values;

    public MonthlySeries(IEnumerable<double> values)
    {
        DomainSupport.NotNull(values, nameof(values));
        double[] copy = values.ToArray();
        if (copy.Length != MonthCount)
        {
            throw new ArgumentException("A monthly series requires exactly 12 values.", nameof(values));
        }

        for (int index = 0; index < copy.Length; index++)
        {
            if (double.IsNaN(copy[index]) || double.IsInfinity(copy[index]))
            {
                throw new ArgumentOutOfRangeException(nameof(values), "Monthly values must be finite.");
            }
        }

        _values = Array.AsReadOnly(copy);
    }

    public int Count => MonthCount;

    public double this[int index] => _values[index];

    public double Sum => _values.Sum();

    public static MonthlySeries Zero { get; } = new(new double[MonthCount]);

    public MonthlySeries Scale(double factor, int digits = GreenRetrofitResult.ValidDigits)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor))
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        return new MonthlySeries(_values.Select(value => GreenRetrofitResult.Round(value * factor, digits)));
    }

    public bool Equals(MonthlySeries? other)
    {
        return other is not null && _values.SequenceEqual(other._values);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as MonthlySeries);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (double value in _values)
            {
                hash = (hash * 397) ^ value.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<double> GetEnumerator()
    {
        return _values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Complete five-carrier by seven-end-use monthly matrix.
/// </summary>
public sealed class EnergyUseBreakdown
{
    private readonly ReadOnlyDictionary<EnergyEndUse, IReadOnlyDictionary<EnergyCarrier, MonthlySeries>> _values;

    public EnergyUseBreakdown(
        IReadOnlyDictionary<EnergyEndUse, IReadOnlyDictionary<EnergyCarrier, MonthlySeries>> values)
    {
        DomainSupport.NotNull(values, nameof(values));
        var copy = new Dictionary<EnergyEndUse, IReadOnlyDictionary<EnergyCarrier, MonthlySeries>>();
        foreach (EnergyEndUse endUse in GrrVocabulary.EndUses)
        {
            if (!values.TryGetValue(endUse, out IReadOnlyDictionary<EnergyCarrier, MonthlySeries>? carriers))
            {
                throw new ArgumentException("Missing GRR end use " + endUse + ".", nameof(values));
            }

            var carrierCopy = new Dictionary<EnergyCarrier, MonthlySeries>();
            foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
            {
                if (!carriers.TryGetValue(carrier, out MonthlySeries? monthly) || monthly is null)
                {
                    throw new ArgumentException(
                        "Missing GRR carrier " + carrier + " for " + endUse + ".",
                        nameof(values));
                }

                carrierCopy.Add(carrier, monthly);
            }

            copy.Add(endUse, new ReadOnlyDictionary<EnergyCarrier, MonthlySeries>(carrierCopy));
        }

        _values = new ReadOnlyDictionary<EnergyEndUse, IReadOnlyDictionary<EnergyCarrier, MonthlySeries>>(copy);
    }

    public IReadOnlyDictionary<EnergyEndUse, IReadOnlyDictionary<EnergyCarrier, MonthlySeries>> Values => _values;

    public MonthlySeries this[EnergyEndUse endUse, EnergyCarrier carrier] => _values[endUse][carrier];

    public static EnergyUseBreakdown Create(
        Func<EnergyEndUse, EnergyCarrier, IEnumerable<double>> valueFactory)
    {
        DomainSupport.NotNull(valueFactory, nameof(valueFactory));
        return new EnergyUseBreakdown(GrrVocabulary.EndUses.ToDictionary(
            endUse => endUse,
            endUse => (IReadOnlyDictionary<EnergyCarrier, MonthlySeries>)
                new ReadOnlyDictionary<EnergyCarrier, MonthlySeries>(
                    GrrVocabulary.Carriers.ToDictionary(
                        carrier => carrier,
                        carrier => new MonthlySeries(valueFactory(endUse, carrier))))));
    }

    public static EnergyUseBreakdown Empty { get; } = Create((_, _) => new double[MonthlySeries.MonthCount]);

    internal EnergyUseBreakdown Scale(Func<EnergyCarrier, double> factor)
    {
        return Create((endUse, carrier) => this[endUse, carrier]
            .Select(value => GreenRetrofitResult.Round(value * factor(carrier))));
    }
}

/// <summary>
/// Carrier, end-use, monthly, and annual totals for one GRR metric.
/// </summary>
public sealed class GreenRetrofitSummary
{
    public GreenRetrofitSummary(
        IReadOnlyDictionary<EnergyCarrier, double> carrierTotals,
        IReadOnlyDictionary<EnergyEndUse, double> endUseTotals,
        MonthlySeries monthlyTotal,
        double annualTotal,
        bool gross)
    {
        CarrierTotals = CopyComplete(carrierTotals, GrrVocabulary.Carriers, nameof(carrierTotals));
        EndUseTotals = CopyComplete(endUseTotals, GrrVocabulary.EndUses, nameof(endUseTotals));
        MonthlyTotal = DomainSupport.NotNull(monthlyTotal, nameof(monthlyTotal));
        if (double.IsNaN(annualTotal) || double.IsInfinity(annualTotal))
        {
            throw new ArgumentOutOfRangeException(nameof(annualTotal));
        }

        AnnualTotal = annualTotal;
        Gross = gross;
    }

    public IReadOnlyDictionary<EnergyCarrier, double> CarrierTotals { get; }

    public IReadOnlyDictionary<EnergyEndUse, double> EndUseTotals { get; }

    public MonthlySeries MonthlyTotal { get; }

    public double AnnualTotal { get; }

    public bool Gross { get; }

    private static ReadOnlyDictionary<T, double> CopyComplete<T>(
        IReadOnlyDictionary<T, double> source,
        IEnumerable<T> keys,
        string parameterName)
        where T : struct
    {
        DomainSupport.NotNull(source, parameterName);
        var result = new Dictionary<T, double>();
        foreach (T key in keys)
        {
            if (!source.TryGetValue(key, out double value))
            {
                throw new ArgumentException("A summary value is missing for " + key + ".", parameterName);
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Summary values must be finite.");
            }

            result.Add(key, value);
        }

        return new ReadOnlyDictionary<T, double>(result);
    }
}

/// <summary>
/// Immutable GRR 0.7 result tree, including deterministic derived metrics and summaries.
/// </summary>
public sealed class GreenRetrofitResult
{
    public const int ValidDigits = 2;

    internal GreenRetrofitResult(
        double totalArea,
        IReadOnlyDictionary<GreenRetrofitMetric, EnergyUseBreakdown> metrics,
        IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> perAreaSummaries,
        IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> grossSummaries)
    {
        TotalArea = DomainSupport.FinitePositive(totalArea, nameof(totalArea));
        Metrics = CopyMetrics(metrics, nameof(metrics));
        PerAreaSummaries = CopySummaries(perAreaSummaries, gross: false, nameof(perAreaSummaries));
        GrossSummaries = CopySummaries(grossSummaries, gross: true, nameof(grossSummaries));
    }

    public double TotalArea { get; }

    public IReadOnlyDictionary<GreenRetrofitMetric, EnergyUseBreakdown> Metrics { get; }

    public EnergyUseBreakdown SiteUses => Metrics[GreenRetrofitMetric.SiteUses];

    public EnergyUseBreakdown SourceUses => Metrics[GreenRetrofitMetric.SourceUses];

    public EnergyUseBreakdown Carbon => Metrics[GreenRetrofitMetric.Carbon];

    public EnergyUseBreakdown Cost => Metrics[GreenRetrofitMetric.Cost];

    public IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> PerAreaSummaries { get; }

    public IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> GrossSummaries { get; }

    public static GreenRetrofitResult FromSiteUses(double totalArea, EnergyUseBreakdown siteUses)
    {
        DomainSupport.FinitePositive(totalArea, nameof(totalArea));
        DomainSupport.NotNull(siteUses, nameof(siteUses));
        var metrics = new Dictionary<GreenRetrofitMetric, EnergyUseBreakdown>
        {
            [GreenRetrofitMetric.SiteUses] = siteUses,
            [GreenRetrofitMetric.SourceUses] = siteUses.Scale(EnergyConversionFactors.SiteToSource),
            [GreenRetrofitMetric.Carbon] = siteUses.Scale(EnergyConversionFactors.SiteToCarbon),
            [GreenRetrofitMetric.Cost] = siteUses.Scale(EnergyConversionFactors.SiteToCost),
        };
        Dictionary<GreenRetrofitMetric, GreenRetrofitSummary> perArea = GrrVocabulary.Metrics.ToDictionary(
            metric => metric,
            metric => Summarize(metrics[metric], totalArea, gross: false));
        Dictionary<GreenRetrofitMetric, GreenRetrofitSummary> gross = GrrVocabulary.Metrics.ToDictionary(
            metric => metric,
            metric => Summarize(metrics[metric], totalArea, gross: true));
        return new GreenRetrofitResult(totalArea, metrics, perArea, gross);
    }

    internal static double Round(double value, int digits = ValidDigits)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (digits < 0 || digits > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(digits));
        }

        if (value == 0d)
        {
            return value;
        }

        long bits = BitConverter.DoubleToInt64Bits(value);
        bool negative = bits < 0;
        long absoluteBits = bits & 0x7fffffffffffffffL;
        int exponentBits = (int)((absoluteBits >> 52) & 0x7ffL);
        long fraction = absoluteBits & 0x000fffffffffffffL;
        BigInteger mantissa;
        int binaryExponent;
        if (exponentBits == 0)
        {
            mantissa = fraction;
            binaryExponent = -1074;
        }
        else
        {
            mantissa = (1L << 52) | fraction;
            binaryExponent = exponentBits - 1023 - 52;
        }

        BigInteger scaled = mantissa * BigInteger.Pow(5, digits);
        int scaledBinaryExponent = binaryExponent + digits;
        BigInteger rounded;
        if (scaledBinaryExponent >= 0)
        {
            rounded = scaled << scaledBinaryExponent;
        }
        else
        {
            BigInteger denominator = BigInteger.One << -scaledBinaryExponent;
            BigInteger remainder;
            rounded = BigInteger.DivRem(scaled, denominator, out remainder);
            int comparison = (remainder << 1).CompareTo(denominator);
            if (comparison > 0 || (comparison == 0 && !rounded.IsEven))
            {
                rounded += BigInteger.One;
            }
        }

        double result = (double)rounded / Math.Pow(10d, digits);
        return negative ? -result : result;
    }

    internal static GreenRetrofitSummary Summarize(
        EnergyUseBreakdown breakdown,
        double area,
        bool gross)
    {
        var carrierTotals = new Dictionary<EnergyCarrier, double>();
        foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
        {
            double consumption = GrrVocabulary.ConsumptionEndUses.Sum(endUse => breakdown[endUse, carrier].Sum);
            double generation = breakdown[EnergyEndUse.Generators, carrier].Sum;
            double value = consumption - generation;
            carrierTotals.Add(carrier, gross ? Round(value * area) : value);
        }

        var endUseTotals = new Dictionary<EnergyEndUse, double>();
        foreach (EnergyEndUse endUse in GrrVocabulary.EndUses)
        {
            double value = GrrVocabulary.Carriers.Sum(carrier => breakdown[endUse, carrier].Sum);
            endUseTotals.Add(endUse, gross ? Round(value * area) : value);
        }

        double[] perAreaMonthly = new double[MonthlySeries.MonthCount];
        double[] monthly = new double[MonthlySeries.MonthCount];
        for (int month = 0; month < monthly.Length; month++)
        {
            double use = Round(GrrVocabulary.ConsumptionEndUses.Sum(
                endUse => GrrVocabulary.Carriers.Sum(carrier => breakdown[endUse, carrier][month])));
            double generation = Round(GrrVocabulary.Carriers.Sum(
                carrier => breakdown[EnergyEndUse.Generators, carrier][month]));
            double value = use - generation;
            perAreaMonthly[month] = value;
            monthly[month] = gross ? Round(value * area) : value;
        }

        double annual = gross
            ? Round(perAreaMonthly.Sum() * area)
            : Round(perAreaMonthly.Sum());
        return new GreenRetrofitSummary(
            carrierTotals,
            endUseTotals,
            new MonthlySeries(monthly),
            annual,
            gross);
    }

    private static ReadOnlyDictionary<GreenRetrofitMetric, EnergyUseBreakdown> CopyMetrics(
        IReadOnlyDictionary<GreenRetrofitMetric, EnergyUseBreakdown> source,
        string parameterName)
    {
        DomainSupport.NotNull(source, parameterName);
        var copy = new Dictionary<GreenRetrofitMetric, EnergyUseBreakdown>();
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            if (!source.TryGetValue(metric, out EnergyUseBreakdown? value) || value is null)
            {
                throw new ArgumentException("A metric is missing for " + metric + ".", parameterName);
            }

            copy.Add(metric, value);
        }

        return new ReadOnlyDictionary<GreenRetrofitMetric, EnergyUseBreakdown>(copy);
    }

    private static ReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> CopySummaries(
        IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> source,
        bool gross,
        string parameterName)
    {
        DomainSupport.NotNull(source, parameterName);
        var copy = new Dictionary<GreenRetrofitMetric, GreenRetrofitSummary>();
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            if (!source.TryGetValue(metric, out GreenRetrofitSummary? value) || value is null)
            {
                throw new ArgumentException("A summary is missing for " + metric + ".", parameterName);
            }

            if (value.Gross != gross)
            {
                throw new ArgumentException("Summary gross/per-area kind does not match its collection.", parameterName);
            }

            copy.Add(metric, value);
        }

        return new ReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary>(copy);
    }
}

internal static class GrrVocabulary
{
    internal static readonly EnergyCarrier[] Carriers =
    {
        EnergyCarrier.Electricity,
        EnergyCarrier.NaturalGas,
        EnergyCarrier.LiquefiedPetroleumGas,
        EnergyCarrier.Oil,
        EnergyCarrier.DistrictHeating,
    };

    internal static readonly EnergyEndUse[] EndUses =
    {
        EnergyEndUse.Heating,
        EnergyEndUse.Cooling,
        EnergyEndUse.Lighting,
        EnergyEndUse.Equipment,
        EnergyEndUse.Circulation,
        EnergyEndUse.HotWater,
        EnergyEndUse.Generators,
    };

    internal static readonly EnergyEndUse[] ConsumptionEndUses =
    {
        EnergyEndUse.Heating,
        EnergyEndUse.Cooling,
        EnergyEndUse.Lighting,
        EnergyEndUse.Equipment,
        EnergyEndUse.Circulation,
        EnergyEndUse.HotWater,
    };

    internal static readonly GreenRetrofitMetric[] Metrics =
    {
        GreenRetrofitMetric.SiteUses,
        GreenRetrofitMetric.SourceUses,
        GreenRetrofitMetric.Carbon,
        GreenRetrofitMetric.Cost,
    };

    internal static string CarrierConstantName(EnergyCarrier carrier) => carrier switch
    {
        EnergyCarrier.Electricity => "electricity",
        EnergyCarrier.NaturalGas => "natural_gas",
        EnergyCarrier.LiquefiedPetroleumGas => "lpg",
        EnergyCarrier.Oil => "oil",
        EnergyCarrier.DistrictHeating => "district_heating",
        _ => throw new ArgumentOutOfRangeException(nameof(carrier)),
    };

    internal static string CarrierDataName(EnergyCarrier carrier) => carrier switch
    {
        EnergyCarrier.Electricity => "ELECTRICITY",
        EnergyCarrier.NaturalGas => "NATURALGAS",
        EnergyCarrier.LiquefiedPetroleumGas => "LPG",
        EnergyCarrier.Oil => "OIL",
        EnergyCarrier.DistrictHeating => "DISTRICTHEATING",
        _ => throw new ArgumentOutOfRangeException(nameof(carrier)),
    };

    internal static string EndUseName(EnergyEndUse endUse) => endUse switch
    {
        EnergyEndUse.Heating => "heating",
        EnergyEndUse.Cooling => "cooling",
        EnergyEndUse.Lighting => "lighting",
        EnergyEndUse.Equipment => "equipment",
        EnergyEndUse.Circulation => "circulation",
        EnergyEndUse.HotWater => "hotwater",
        EnergyEndUse.Generators => "generators",
        _ => throw new ArgumentOutOfRangeException(nameof(endUse)),
    };

    internal static string MetricName(GreenRetrofitMetric metric) => metric switch
    {
        GreenRetrofitMetric.SiteUses => "site_uses",
        GreenRetrofitMetric.SourceUses => "source_uses",
        GreenRetrofitMetric.Carbon => "co2",
        GreenRetrofitMetric.Cost => "cost",
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };
}
