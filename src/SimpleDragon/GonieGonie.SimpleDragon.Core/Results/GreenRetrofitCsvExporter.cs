using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.SimpleDragon.Internal;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Creates stable, analysis-friendly CSV views of a SimpleDragon result and writes them with a UTF-8 BOM.
/// </summary>
public static class GreenRetrofitCsvExporter
{
    public const string ManifestFileName = "manifest.json";
    public const string ManifestSchemaVersion = "goniegonie-simpledragon-csv-export.v2";
    public const string SummaryFileName = "summary.csv";
    public const string MonthlyByFuelFileName = "monthly_by_fuel.csv";
    public const string MonthlyByEndUseFileName = "monthly_by_enduse.csv";
    public const string AnnualByFuelFileName = "annual_by_fuel.csv";
    public const string AnnualByEndUseFileName = "annual_by_enduse.csv";
    public const string DiagnosticsFileName = "diagnostics.csv";
    public const string GeometryMapFileName = "geometry_map.csv";

    private static readonly UTF8Encoding Utf8WithBom = new(
        encoderShouldEmitUTF8Identifier: true,
        throwOnInvalidBytes: true);

    public static GreenRetrofitCsvPackage CreatePackage(
        GreenRetrofitResult result,
        IEnumerable<Diagnostic>? diagnostics = null,
        IEnumerable<GreenRetrofitGeometryMapEntry>? geometryMap = null,
        string? caseId = null,
        GreenRetrofitModel? model = null)
    {
        DomainSupport.NotNull(result, nameof(result));
        Diagnostic[] diagnosticValues = diagnostics?.ToArray() ?? Array.Empty<Diagnostic>();
        GreenRetrofitGeometryMapEntry[] geometryValues = geometryMap?.ToArray()
            ?? Array.Empty<GreenRetrofitGeometryMapEntry>();
        if (diagnosticValues.Any(item => item is null))
        {
            throw new ArgumentException("A diagnostic cannot be null.", nameof(diagnostics));
        }

        if (geometryValues.Any(item => item is null))
        {
            throw new ArgumentException("A geometry-map entry cannot be null.", nameof(geometryMap));
        }

        string normalizedCaseId = caseId?.Trim() ?? string.Empty;
        GreenRetrofitCsvFile[] csvFiles =
        {
            new GreenRetrofitCsvFile(SummaryFileName, SerializeSummary(result, normalizedCaseId)),
            new GreenRetrofitCsvFile(
                MonthlyByFuelFileName,
                SerializeMonthly(result, GreenRetrofitSeriesGrouping.Fuel, normalizedCaseId)),
            new GreenRetrofitCsvFile(
                MonthlyByEndUseFileName,
                SerializeMonthly(result, GreenRetrofitSeriesGrouping.EndUse, normalizedCaseId)),
            new GreenRetrofitCsvFile(AnnualByFuelFileName, SerializeAnnualByFuel(result, normalizedCaseId)),
            new GreenRetrofitCsvFile(AnnualByEndUseFileName, SerializeAnnualByEndUse(result, normalizedCaseId)),
            new GreenRetrofitCsvFile(DiagnosticsFileName, SerializeDiagnostics(diagnosticValues, normalizedCaseId)),
            new GreenRetrofitCsvFile(GeometryMapFileName, SerializeGeometryMap(geometryValues, normalizedCaseId)),
        };
        string manifest = SerializeManifest(result, model, normalizedCaseId, csvFiles);
        return new GreenRetrofitCsvPackage(
            new[] { new GreenRetrofitCsvFile(ManifestFileName, manifest) }.Concat(csvFiles));
    }

    /// <summary>
    /// Writes the complete package only when <paramref name="export"/> is true.
    /// Existing files require <paramref name="overwrite"/> to be true.
    /// </summary>
    public static GreenRetrofitCsvExportResult ExportDirectory(
        string directoryPath,
        GreenRetrofitResult result,
        IEnumerable<Diagnostic>? diagnostics = null,
        IEnumerable<GreenRetrofitGeometryMapEntry>? geometryMap = null,
        string? caseId = null,
        GreenRetrofitModel? model = null,
        bool export = false,
        bool overwrite = false)
    {
        string requestedDirectory = DomainSupport.RequiredText(directoryPath, nameof(directoryPath));
        string fullDirectory = Path.GetFullPath(requestedDirectory);
        GreenRetrofitCsvPackage package = CreatePackage(result, diagnostics, geometryMap, caseId, model);
        string[] paths = package.Files
            .Select(file => SafeOutputPath(fullDirectory, file.Name))
            .ToArray();
        if (!export)
        {
            return new GreenRetrofitCsvExportResult(
                fullDirectory,
                exportRequested: false,
                written: false,
                paths);
        }

        if (!overwrite)
        {
            string? existing = paths.FirstOrDefault(path => File.Exists(path) || Directory.Exists(path));
            if (existing is not null)
            {
                throw new IOException(
                    "CSV export would overwrite an existing path. Enable Overwrite explicitly: " + existing);
            }
        }

        Directory.CreateDirectory(fullDirectory);
        for (int index = 0; index < package.Files.Count; index++)
        {
            WriteFile(
                paths[index],
                package.Files[index].Content,
                emitBom: paths[index].EndsWith(".csv", StringComparison.OrdinalIgnoreCase),
                overwrite);
        }

        return new GreenRetrofitCsvExportResult(
            fullDirectory,
            exportRequested: true,
            written: true,
            paths);
    }

    public static string SerializeSummary(GreenRetrofitResult result, string? caseId = null)
    {
        DomainSupport.NotNull(result, nameof(result));
        var csv = new StableCsvWriter(
            "case_id",
            "metric",
            "basis",
            "total_area_m2",
            "annual_total",
            "value_unit");
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            WriteSummary(csv, result, metric, gross: false, caseId);
            WriteSummary(csv, result, metric, gross: true, caseId);
        }

        return csv.ToString();
    }

    public static string SerializeMonthly(
        GreenRetrofitResult result,
        GreenRetrofitSeriesGrouping grouping,
        string? caseId = null)
    {
        DomainSupport.NotNull(result, nameof(result));
        if (!Enum.IsDefined(typeof(GreenRetrofitSeriesGrouping), grouping))
        {
            throw new ArgumentOutOfRangeException(nameof(grouping));
        }

        string seriesColumn = grouping == GreenRetrofitSeriesGrouping.Fuel ? "fuel" : "end_use";
        var csv = new StableCsvWriter(
            "case_id",
            "metric",
            "basis",
            "month_index",
            "month",
            seriesColumn,
            "value",
            "value_unit");
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            WriteMonthly(csv, result, metric, gross: false, grouping, caseId);
            WriteMonthly(csv, result, metric, gross: true, grouping, caseId);
        }

        return csv.ToString();
    }

    public static string SerializeAnnualByFuel(
        GreenRetrofitResult result,
        string? caseId = null)
    {
        DomainSupport.NotNull(result, nameof(result));
        var csv = new StableCsvWriter(
            "case_id",
            "metric",
            "basis",
            "fuel",
            "value",
            "value_unit");
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            WriteAnnualByFuel(csv, result, metric, gross: false, caseId);
            WriteAnnualByFuel(csv, result, metric, gross: true, caseId);
        }

        return csv.ToString();
    }

    public static string SerializeAnnualByEndUse(
        GreenRetrofitResult result,
        string? caseId = null)
    {
        DomainSupport.NotNull(result, nameof(result));
        var csv = new StableCsvWriter(
            "case_id",
            "metric",
            "basis",
            "end_use",
            "value",
            "value_unit");
        foreach (GreenRetrofitMetric metric in GrrVocabulary.Metrics)
        {
            WriteAnnualByEndUse(csv, result, metric, gross: false, caseId);
            WriteAnnualByEndUse(csv, result, metric, gross: true, caseId);
        }

        return csv.ToString();
    }

    public static string SerializeDiagnostics(
        IEnumerable<Diagnostic> diagnostics,
        string? caseId = null)
    {
        Diagnostic[] values = DomainSupport.NotNull(diagnostics, nameof(diagnostics)).ToArray();
        if (values.Any(item => item is null))
        {
            throw new ArgumentException("A diagnostic cannot be null.", nameof(diagnostics));
        }

        var csv = new StableCsvWriter(
            "case_id",
            "severity",
            "code",
            "message",
            "object_id",
            "rhino_object_id",
            "brep_face_index",
            "geometry_fingerprint",
            "grasshopper_path",
            "grasshopper_index",
            "suggested_action");
        foreach (Diagnostic diagnostic in values
                     .OrderBy(item => item.Severity)
                     .ThenBy(item => item.Code, StringComparer.Ordinal)
                     .ThenBy(item => item.ObjectId?.Value ?? string.Empty, StringComparer.Ordinal)
                     .ThenBy(item => item.Message, StringComparer.Ordinal))
        {
            GeometryProvenance? geometry = diagnostic.Geometry;
            csv.Write(
                caseId,
                SeverityName(diagnostic.Severity),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.ObjectId?.Value,
                geometry?.RhinoObjectId?.ToString("D"),
                Integer(geometry?.BrepFaceIndex),
                geometry?.GeometryFingerprint,
                geometry?.GrasshopperPath,
                Integer(geometry?.GrasshopperIndex),
                diagnostic.SuggestedAction);
        }

        return csv.ToString();
    }

    public static string SerializeGeometryMap(
        IEnumerable<GreenRetrofitGeometryMapEntry> geometryMap,
        string? caseId = null)
    {
        GreenRetrofitGeometryMapEntry[] values = DomainSupport.NotNull(
            geometryMap,
            nameof(geometryMap)).ToArray();
        if (values.Any(item => item is null))
        {
            throw new ArgumentException("A geometry-map entry cannot be null.", nameof(geometryMap));
        }

        var csv = new StableCsvWriter(
            "case_id",
            "entity_id",
            "entity_kind",
            "zone_index",
            "surface_index",
            "opening_index",
            "trim_loop_index",
            "rhino_object_id",
            "geometry_fingerprint",
            "grasshopper_path",
            "grasshopper_index");
        foreach (GreenRetrofitGeometryMapEntry entry in values
                     .OrderBy(item => item.EntityId.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.Kind)
                     .ThenBy(item => item.ZoneIndex)
                     .ThenBy(item => item.SurfaceIndex ?? -1)
                     .ThenBy(item => item.OpeningIndex ?? -1)
                     .ThenBy(item => item.TrimLoopIndex ?? -1))
        {
            GeometryProvenance provenance = entry.Provenance;
            csv.Write(
                caseId,
                entry.EntityId.Value,
                GeometryKindName(entry.Kind),
                Integer(entry.ZoneIndex),
                Integer(entry.SurfaceIndex),
                Integer(entry.OpeningIndex),
                Integer(entry.TrimLoopIndex),
                provenance.RhinoObjectId?.ToString("D"),
                provenance.GeometryFingerprint,
                provenance.GrasshopperPath,
                Integer(provenance.GrasshopperIndex));
        }

        return csv.ToString();
    }

    public static string SerializeManifest(
        GreenRetrofitResult result,
        GreenRetrofitModel? model,
        string? caseId,
        IEnumerable<GreenRetrofitCsvFile> csvFiles)
    {
        DomainSupport.NotNull(result, nameof(result));
        GreenRetrofitCsvFile[] files = DomainSupport.NotNull(csvFiles, nameof(csvFiles)).ToArray();
        if (files.Any(file => file is null))
        {
            throw new ArgumentException("A CSV package file cannot be null.", nameof(csvFiles));
        }

        CompatibilityIdentity compatibility = PackageInfo.Compatibility;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
            }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", ManifestSchemaVersion);
            writer.WriteString("case_id", caseId?.Trim() ?? string.Empty);
            writer.WritePropertyName("product");
            writer.WriteStartObject();
            writer.WriteString("name", PackageInfo.Name);
            writer.WriteString("version", PackageInfo.Version);
            writer.WriteEndObject();
            writer.WritePropertyName("formats");
            writer.WriteStartObject();
            writer.WriteString("grm_version", GrmFormat.Version);
            writer.WriteString("grr_version", GrrFormat.Version);
            writer.WriteString("csv_schema", "2");
            writer.WriteEndObject();
            writer.WritePropertyName("compatibility");
            writer.WriteStartObject();
            writer.WriteString("upstream_version", compatibility.UpstreamVersion);
            writer.WriteString("upstream_commit", compatibility.UpstreamCommit);
            writer.WriteString("energyplus_version", compatibility.EnergyPlusVersion);
            writer.WriteString("energyplus_build", compatibility.EnergyPlusBuild);
            writer.WriteEndObject();
            writer.WritePropertyName("export_options");
            writer.WriteStartObject();
            writer.WriteString("csv_encoding", "utf-8-bom");
            writer.WriteString("manifest_encoding", "utf-8");
            writer.WriteString("line_endings", "lf");
            writer.WriteString("numeric_format", "canonical-ieee754-shortest");
            writer.WriteString("enum_format", "stable-snake-case");
            writer.WriteBoolean("explicit_trigger_required", true);
            writer.WriteBoolean("explicit_overwrite_required", true);
            writer.WriteEndObject();
            writer.WritePropertyName("model");
            if (model is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("id", model.Id.Value);
                writer.WriteString("name", model.Name);
                CanonicalDouble.Write(writer, "area_m2", model.Area);
                CanonicalDouble.Write(writer, "north_axis_degrees", model.NorthAxis);
                writer.WriteString(
                    "sha256",
                    Sha256(Encoding.UTF8.GetBytes(GrmWriter.Serialize(model, indented: false))));
                writer.WriteEndObject();
            }

            writer.WritePropertyName("result");
            writer.WriteStartObject();
            CanonicalDouble.Write(writer, "total_area_m2", result.TotalArea);
            writer.WriteString(
                "sha256",
                Sha256(Encoding.UTF8.GetBytes(GrrWriter.Serialize(result, writeIndented: false))));
            writer.WriteEndObject();
            writer.WritePropertyName("files");
            writer.WriteStartArray();
            foreach (GreenRetrofitCsvFile file in files)
            {
                writer.WriteStartObject();
                writer.WriteString("name", file.Name);
                writer.WriteString("sha256_utf8_bom", Sha256(CsvBytes(file.Content)));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteSummary(
        StableCsvWriter csv,
        GreenRetrofitResult result,
        GreenRetrofitMetric metric,
        bool gross,
        string? caseId)
    {
        GreenRetrofitSummary summary = gross
            ? result.GrossSummaries[metric]
            : result.PerAreaSummaries[metric];
        csv.Write(
            caseId,
            GrrVocabulary.MetricName(metric),
            BasisName(gross),
            Number(result.TotalArea),
            Number(summary.AnnualTotal),
            GreenRetrofitMonthlyData.UnitName(metric, gross));
    }

    private static void WriteMonthly(
        StableCsvWriter csv,
        GreenRetrofitResult result,
        GreenRetrofitMetric metric,
        bool gross,
        GreenRetrofitSeriesGrouping grouping,
        string? caseId)
    {
        GreenRetrofitMonthlyData data = GreenRetrofitMonthlyData.Create(result, metric, gross, grouping);
        for (int month = 0; month < MonthlySeries.MonthCount; month++)
        {
            foreach (GreenRetrofitMonthlySeries series in data.Series)
            {
                csv.Write(
                    caseId,
                    GrrVocabulary.MetricName(metric),
                    BasisName(gross),
                    Integer(data.XValues[month]),
                    data.XLabels[month],
                    series.Name,
                    Number(series.Values[month]),
                    data.Unit);
            }
        }
    }

    private static void WriteAnnualByFuel(
        StableCsvWriter csv,
        GreenRetrofitResult result,
        GreenRetrofitMetric metric,
        bool gross,
        string? caseId)
    {
        GreenRetrofitSummary summary = gross
            ? result.GrossSummaries[metric]
            : result.PerAreaSummaries[metric];
        foreach (EnergyCarrier carrier in GrrVocabulary.Carriers)
        {
            csv.Write(
                caseId,
                GrrVocabulary.MetricName(metric),
                BasisName(gross),
                GrrVocabulary.CarrierConstantName(carrier),
                Number(summary.CarrierTotals[carrier]),
                GreenRetrofitMonthlyData.UnitName(metric, gross));
        }
    }

    private static void WriteAnnualByEndUse(
        StableCsvWriter csv,
        GreenRetrofitResult result,
        GreenRetrofitMetric metric,
        bool gross,
        string? caseId)
    {
        GreenRetrofitSummary summary = gross
            ? result.GrossSummaries[metric]
            : result.PerAreaSummaries[metric];
        foreach (EnergyEndUse endUse in GrrVocabulary.EndUses)
        {
            csv.Write(
                caseId,
                GrrVocabulary.MetricName(metric),
                BasisName(gross),
                GrrVocabulary.EndUseName(endUse),
                Number(summary.EndUseTotals[endUse]),
                GreenRetrofitMonthlyData.UnitName(metric, gross));
        }
    }

    private static string SafeOutputPath(string fullDirectory, string fileName)
    {
        string prefix = fullDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(fullDirectory, fileName));
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A CSV output path escaped the requested directory.");
        }

        return candidate;
    }

    private static void WriteFile(string path, string content, bool emitBom, bool overwrite)
    {
        FileMode mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        using var stream = new FileStream(path, mode, FileAccess.Write, FileShare.None);
        if (emitBom)
        {
            byte[] preamble = Utf8WithBom.GetPreamble();
            stream.Write(preamble, 0, preamble.Length);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] CsvBytes(string content)
    {
        byte[] preamble = Utf8WithBom.GetPreamble();
        byte[] body = Encoding.UTF8.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    private static string Sha256(byte[] bytes)
    {
        byte[] hash;
#if NET7_0_OR_GREATER
        hash = SHA256.HashData(bytes);
#else
        using (SHA256 algorithm = SHA256.Create())
        {
            hash = algorithm.ComputeHash(bytes);
        }
#endif

        var text = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash)
        {
            text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static string BasisName(bool gross) => gross ? "gross" : "per_area";

    private static string Number(double value) => CanonicalDouble.Format(value);

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Integer(int? value) => value.HasValue ? Integer(value.Value) : string.Empty;

    private static string SeverityName(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => "info",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Fatal => "fatal",
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    private static string GeometryKindName(GreenRetrofitGeometryKind kind) => kind switch
    {
        GreenRetrofitGeometryKind.Zone => "zone",
        GreenRetrofitGeometryKind.Surface => "surface",
        GreenRetrofitGeometryKind.Fenestration => "fenestration",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private sealed class StableCsvWriter
    {
        private readonly StringBuilder builder = new();

        public StableCsvWriter(params string[] headers)
        {
            Write(headers);
        }

        public void Write(params string?[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendField(values[index] ?? string.Empty);
            }

            builder.Append('\n');
        }

        public override string ToString()
        {
            return builder.ToString();
        }

        private void AppendField(string value)
        {
            bool escape = RequiresEscaping(value);
            if (!escape)
            {
                builder.Append(value);
                return;
            }

            builder.Append('"');
            builder.Append(value.Replace("\"", "\"\""));
            builder.Append('"');
        }

        private static bool RequiresEscaping(string value)
        {
            foreach (char character in value)
            {
                if (character == ',' || character == '"' || character == '\r' || character == '\n')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
