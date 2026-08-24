using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

public static class GrrFormat
{
    public const string Version = "0.7.0";
}

public sealed class GrrReadResult
{
    internal GrrReadResult(GreenRetrofitResult? result, IReadOnlyList<Diagnostic> diagnostics)
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
            ? "The GRR document did not produce a result."
            : Diagnostics[0].Code + ": " + Diagnostics[0].Message;
        throw new InvalidDataException(message);
    }
}

/// <summary>
/// Strict, culture-independent reader for the pinned upstream GRR 0.7 JSON tree.
/// </summary>
public static class GrrReader
{
    public static GrrReadResult Read(string json)
    {
        DomainSupport.NotNull(json, nameof(json));
        return new Parser().Read(json);
    }

    public static GrrReadResult ReadFile(string path)
    {
        string source = DomainSupport.RequiredText(path, nameof(path));
        try
        {
            return Read(File.ReadAllText(source, new UTF8Encoding(false, true)));
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is DecoderFallbackException)
        {
            return new GrrReadResult(
                null,
                new[]
                {
                    new Diagnostic(
                        "SD.GRR.FILE_READ_FAILED",
                        DiagnosticSeverity.Error,
                        "Could not read GRR file '" + source + "': " + exception.Message,
                        suggestedAction: "Verify that the file exists, is UTF-8, and is readable."),
                });
        }
    }

    private sealed class Parser
    {
        private readonly List<Diagnostic> _diagnostics = new();

        public GrrReadResult Read(string json)
        {
            GreenRetrofitResult? result = null;
            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 64,
                    });
                JsonElement root = document.RootElement;
                RequireKind(root, JsonValueKind.Object, "$", "object");
                double area = Number(Object(root, "building", "$"), "total_area", "$.building");
                ValidatePositive(area, "$.building.total_area");
                ParseAndValidateConstants(Object(root, "constants", "$"));

                var metrics = new Dictionary<GreenRetrofitMetric, EnergyUseBreakdown>();
                foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
                {
                    string name = GrrVocabulary.MetricName(metric);
                    metrics.Add(metric, ParseBreakdown(Object(root, name, "$"), "$." + name));
                }

                IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> perArea =
                    ParseSummarySet(Object(root, "summary_per_area", "$"), "$.summary_per_area", gross: false);
                IReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> gross =
                    ParseSummarySet(Object(root, "summary_gross", "$"), "$.summary_gross", gross: true);
                result = new GreenRetrofitResult(area, metrics, perArea, gross);
                AddConsistencyDiagnostics(result);
            }
            catch (JsonException exception)
            {
                _diagnostics.Add(new Diagnostic(
                    "SD.GRR.JSON_INVALID",
                    DiagnosticSeverity.Error,
                    "The GRR document is not valid JSON: " + exception.Message,
                    suggestedAction: "Correct the JSON syntax and retry."));
            }
            catch (GrrParseStopException)
            {
                // A path-specific diagnostic has already been recorded.
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is OverflowException)
            {
                _diagnostics.Add(new Diagnostic(
                    "SD.GRR.DOMAIN_INVALID",
                    DiagnosticSeverity.Error,
                    "The GRR values cannot form a result: " + exception.Message,
                    suggestedAction: "Correct the reported value or relationship."));
            }

            return new GrrReadResult(
                result,
                new ReadOnlyCollection<Diagnostic>(_diagnostics.ToArray()));
        }

        private void ParseAndValidateConstants(JsonElement constants)
        {
            ValidateFactorGroup(
                Object(constants, "site2source", "$.constants"),
                "$.constants.site2source",
                EnergyConversionFactors.SiteToSource);
            ValidateFactorGroup(
                Object(constants, "site2co2", "$.constants"),
                "$.constants.site2co2",
                EnergyConversionFactors.SiteToCarbon);
            ValidateFactorGroup(
                Object(constants, "site2cost", "$.constants"),
                "$.constants.site2cost",
                EnergyConversionFactors.SiteToCost);
        }

        private void ValidateFactorGroup(
            JsonElement group,
            string path,
            Func<EnergyCarrier, double> expected)
        {
            foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
            {
                string name = GrrVocabulary.CarrierConstantName(carrier);
                double value = Number(group, name, path);
                if (Math.Abs(value - expected(carrier)) > 1e-12)
                {
                    Fail(
                        "SD.GRR.CONSTANT_MISMATCH",
                        path + "." + name,
                        "The conversion factor does not match the pinned GRR 0.7 constant.",
                        "Restore the upstream 0.7 factor before comparing or aggregating results.");
                }
            }
        }

        private EnergyUseBreakdown ParseBreakdown(JsonElement element, string path)
        {
            var uses = new Dictionary<EnergyEndUse, IReadOnlyDictionary<EnergyCarrier, MonthlySeries>>();
            foreach (EnergyEndUse endUse in GrrVocabulary.EndUses)
            {
                string useName = GrrVocabulary.EndUseName(endUse);
                JsonElement use = Object(element, useName, path);
                var carriers = new Dictionary<EnergyCarrier, MonthlySeries>();
                foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
                {
                    string carrierName = GrrVocabulary.CarrierDataName(carrier);
                    carriers.Add(
                        carrier,
                        Monthly(use, carrierName, path + "." + useName));
                }

                uses.Add(endUse, new ReadOnlyDictionary<EnergyCarrier, MonthlySeries>(carriers));
            }

            return new EnergyUseBreakdown(uses);
        }

        private ReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary> ParseSummarySet(
            JsonElement element,
            string path,
            bool gross)
        {
            var summaries = new Dictionary<GreenRetrofitMetric, GreenRetrofitSummary>();
            foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
            {
                string metricName = GrrVocabulary.MetricName(metric);
                JsonElement summary = Object(element, metricName, path);
                string summaryPath = path + "." + metricName;
                var carriers = GrrVocabulary.Carriers.ToDictionary(
                    carrier => carrier,
                    carrier => Number(summary, GrrVocabulary.CarrierDataName(carrier), summaryPath));
                var uses = GrrVocabulary.EndUses.ToDictionary(
                    endUse => endUse,
                    endUse => Number(summary, GrrVocabulary.EndUseName(endUse), summaryPath));
                summaries.Add(metric, new GreenRetrofitSummary(
                    carriers,
                    uses,
                    Monthly(summary, "total_monthly", summaryPath),
                    Number(summary, "total_annual", summaryPath),
                    gross));
            }

            return new ReadOnlyDictionary<GreenRetrofitMetric, GreenRetrofitSummary>(summaries);
        }

        private void AddConsistencyDiagnostics(GreenRetrofitResult result)
        {
            GreenRetrofitResult derived = GreenRetrofitResult.FromSiteUses(result.TotalArea, result.SiteUses);
            foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
            {
                EnergyUseBreakdown actual = result.Metrics[metric];
                EnergyUseBreakdown expected = derived.Metrics[metric];
                foreach (EnergyEndUse endUse in GrrVocabulary.EndUses)
                {
                    foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
                    {
                        for (int month = 0; month < MonthlySeries.MonthCount; month++)
                        {
                            if (Math.Abs(actual[endUse, carrier][month] - expected[endUse, carrier][month]) > 0.011d)
                            {
                                _diagnostics.Add(new Diagnostic(
                                    "SD.GRR.DERIVED_VALUE_MISMATCH",
                                    DiagnosticSeverity.Warning,
                                    "A derived " + GrrVocabulary.MetricName(metric)
                                    + " value differs from the pinned conversion factor at "
                                    + GrrVocabulary.EndUseName(endUse) + "/"
                                    + GrrVocabulary.CarrierDataName(carrier) + "/month "
                                    + (month + 1).ToString(CultureInfo.InvariantCulture) + ".",
                                    suggestedAction: "Regenerate the GRR from site uses when strict factor parity is required."));
                                return;
                            }
                        }
                    }
                }
            }
        }

        private MonthlySeries Monthly(JsonElement parent, string name, string path)
        {
            JsonElement value = Property(parent, name, path);
            RequireKind(value, JsonValueKind.Array, path + "." + name, "array");
            if (value.GetArrayLength() != MonthlySeries.MonthCount)
            {
                Fail(
                    "SD.GRR.MONTH_COUNT_INVALID",
                    path + "." + name,
                    "A monthly series requires exactly 12 values; "
                    + value.GetArrayLength().ToString(CultureInfo.InvariantCulture) + " were supplied.",
                    "Supply one finite value for each month from January through December.");
            }

            var values = new double[MonthlySeries.MonthCount];
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                values[index] = ElementNumber(item, path + "." + name + "["
                    + index.ToString(CultureInfo.InvariantCulture) + "]");
                index++;
            }

            return new MonthlySeries(values);
        }

        private double Number(JsonElement parent, string name, string path)
        {
            return ElementNumber(Property(parent, name, path), path + "." + name);
        }

        private double ElementNumber(JsonElement value, string path)
        {
            double number = 0d;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out number)
                || double.IsNaN(number) || double.IsInfinity(number))
            {
                Fail(
                    "SD.GRR.NUMBER_INVALID",
                    path,
                    "A finite JSON number is required.",
                    "Replace the value with a finite invariant-culture number.");
            }

            return number;
        }

        private void ValidatePositive(double value, string path)
        {
            if (value <= 0d)
            {
                Fail(
                    "SD.GRR.AREA_INVALID",
                    path,
                    "The building total area must be positive.",
                    "Supply the simulated gross floor area in square metres.");
            }
        }

        private JsonElement Object(JsonElement parent, string name, string path)
        {
            JsonElement value = Property(parent, name, path);
            RequireKind(value, JsonValueKind.Object, path + "." + name, "object");
            return value;
        }

        private JsonElement Property(JsonElement parent, string name, string path)
        {
            if (!parent.TryGetProperty(name, out JsonElement value))
            {
                Fail(
                    "SD.GRR.PROPERTY_REQUIRED",
                    path + "." + name,
                    "Required property '" + name + "' is missing.",
                    "Add the property using the GRR 0.7 schema.");
            }

            return value;
        }

        private void RequireKind(JsonElement value, JsonValueKind kind, string path, string description)
        {
            if (value.ValueKind != kind)
            {
                Fail(
                    "SD.GRR.TYPE_INVALID",
                    path,
                    "Expected " + description + " but found " + value.ValueKind + ".",
                    "Correct the JSON value type.");
            }
        }

        private void Fail(string code, string path, string message, string action)
        {
            _diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                path + ": " + message,
                suggestedAction: action));
            throw new GrrParseStopException();
        }
    }

    private sealed class GrrParseStopException : Exception
    {
    }
}
