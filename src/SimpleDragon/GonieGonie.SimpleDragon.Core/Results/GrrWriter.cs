using System.Text;
using System.Text.Json;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Writes deterministic UTF-8 GRR 0.7 JSON with stable property and array order.
/// </summary>
public static class GrrWriter
{
    public static string Serialize(GreenRetrofitResult result, bool writeIndented = true)
    {
        DomainSupport.NotNull(result, nameof(result));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = writeIndented,
                SkipValidation = false,
            }))
        {
            Write(writer, result);
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    public static void WriteFile(
        string path,
        GreenRetrofitResult result,
        bool writeIndented = true)
    {
        string target = DomainSupport.RequiredText(path, nameof(path));
        File.WriteAllText(target, Serialize(result, writeIndented), new UTF8Encoding(false));
    }

    private static void Write(Utf8JsonWriter writer, GreenRetrofitResult result)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("building");
        writer.WriteStartObject();
        writer.WriteNumber("total_area", result.TotalArea);
        writer.WriteEndObject();
        WriteConstants(writer);
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            writer.WritePropertyName(GrrVocabulary.MetricName(metric));
            WriteBreakdown(writer, result.Metrics[metric]);
        }

        WriteSummarySet(writer, "summary_per_area", result.PerAreaSummaries);
        WriteSummarySet(writer, "summary_gross", result.GrossSummaries);
        writer.WriteEndObject();
    }

    private static void WriteConstants(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("constants");
        writer.WriteStartObject();
        WriteFactorGroup(writer, "site2source", EnergyConversionFactors.SiteToSource);
        WriteFactorGroup(writer, "site2co2", EnergyConversionFactors.SiteToCarbon);
        WriteFactorGroup(writer, "site2cost", EnergyConversionFactors.SiteToCost);
        writer.WriteEndObject();
    }

    private static void WriteFactorGroup(
        Utf8JsonWriter writer,
        string name,
        Func<EnergyCarrier, double> factor)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
        {
            writer.WriteNumber(GrrVocabulary.CarrierConstantName(carrier), factor(carrier));
        }

        writer.WriteEndObject();
    }

    private static void WriteBreakdown(Utf8JsonWriter writer, EnergyUseBreakdown breakdown)
    {
        writer.WriteStartObject();
        foreach (EnergyEndUse endUse in GrrVocabulary.EndUses)
        {
            writer.WritePropertyName(GrrVocabulary.EndUseName(endUse));
            writer.WriteStartObject();
            foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
            {
                writer.WritePropertyName(GrrVocabulary.CarrierDataName(carrier));
                WriteMonthly(writer, breakdown[endUse, carrier]);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteSummarySet(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> summaries)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            writer.WritePropertyName(GrrVocabulary.MetricName(metric));
            GreenRetrofitSummary summary = summaries[metric];
            writer.WriteStartObject();
            foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
            {
                writer.WriteNumber(
                    GrrVocabulary.CarrierDataName(carrier),
                    summary.CarrierTotals[carrier]);
            }

            foreach (EnergyEndUse endUse in GrrVocabulary.EndUses)
            {
                writer.WriteNumber(
                    GrrVocabulary.EndUseName(endUse),
                    summary.EndUseTotals[endUse]);
            }

            writer.WritePropertyName("total_monthly");
            WriteMonthly(writer, summary.MonthlyTotal);
            writer.WriteNumber("total_annual", summary.AnnualTotal);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteMonthly(Utf8JsonWriter writer, MonthlySeries monthly)
    {
        writer.WriteStartArray();
        foreach (double value in monthly)
        {
            writer.WriteNumberValue(value);
        }

        writer.WriteEndArray();
    }
}
