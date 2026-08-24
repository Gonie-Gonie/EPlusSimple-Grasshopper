using System.Globalization;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;

namespace GonieGonie.SimpleDragon.Tests;

public sealed class GreenRetrofitCsvExporterTests
{
    private static readonly byte[] Utf8Bom = { 0xef, 0xbb, 0xbf };
    private static readonly string[] ExpectedFileNames =
    {
        "manifest.json",
        "summary.csv",
        "monthly_by_fuel.csv",
        "monthly_by_enduse.csv",
        "annual_by_fuel.csv",
        "annual_by_enduse.csv",
        "diagnostics.csv",
        "geometry_map.csv",
    };

    private static readonly string[] ExpectedFuelNames =
    {
        "electricity",
        "natural_gas",
        "lpg",
        "oil",
        "district_heating",
    };

    [Fact]
    public void PackageHasStableSchemasOrderManifestHashesAndInvariantNumbers()
    {
        GreenRetrofitResult result = LoadResult();
        GreenRetrofitModel model = LoadModel();
        Diagnostic[] diagnostics = Diagnostics();
        GreenRetrofitGeometryMapEntry[] geometryMap = GeometryMap();

        GreenRetrofitCsvPackage first = GreenRetrofitCsvExporter.CreatePackage(
            result,
            diagnostics.Reverse(),
            geometryMap.Reverse(),
            "서울,case",
            model);
        GreenRetrofitCsvPackage duplicate = GreenRetrofitCsvExporter.CreatePackage(
            result,
            diagnostics.Reverse(),
            geometryMap.Reverse(),
            "서울,case",
            model);

        Assert.Equal(ExpectedFileNames, first.Files.Select(file => file.Name));
        Assert.Equal(
            first.Files.Select(file => file.Content),
            duplicate.Files.Select(file => file.Content));

        string summary = first.GetFile(GreenRetrofitCsvExporter.SummaryFileName).Content;
        string[] summaryLines = Lines(summary);
        Assert.Equal(
            "case_id,metric,basis,total_area_m2,annual_total,value_unit",
            summaryLines[0]);
        Assert.Equal(
            "\"서울,case\",site_uses,per_area,48,79.34,kWh/m2",
            summaryLines[1]);
        Assert.Equal(9, summaryLines.Length);
        Assert.Equal(481, Lines(first.GetFile(GreenRetrofitCsvExporter.MonthlyByFuelFileName).Content).Length);
        Assert.Equal(673, Lines(first.GetFile(GreenRetrofitCsvExporter.MonthlyByEndUseFileName).Content).Length);
        Assert.Equal(41, Lines(first.GetFile(GreenRetrofitCsvExporter.AnnualByFuelFileName).Content).Length);
        Assert.Equal(57, Lines(first.GetFile(GreenRetrofitCsvExporter.AnnualByEndUseFileName).Content).Length);

        string diagnosticCsv = first.GetFile(GreenRetrofitCsvExporter.DiagnosticsFileName).Content;
        string[] diagnosticLines = Lines(diagnosticCsv);
        Assert.Equal(
            "case_id,severity,code,message,object_id,rhino_object_id,brep_face_index,geometry_fingerprint,grasshopper_path,grasshopper_index,suggested_action",
            diagnosticLines[0]);
        Assert.Contains(",info,SD.CSV.A,", diagnosticLines[1], StringComparison.Ordinal);
        Assert.Contains(",error,SD.CSV.B,", diagnosticLines[2], StringComparison.Ordinal);
        Assert.Contains("\"한글, comma\"", diagnosticLines[1], StringComparison.Ordinal);

        string geometryCsv = first.GetFile(GreenRetrofitCsvExporter.GeometryMapFileName).Content;
        string[] geometryLines = Lines(geometryCsv);
        Assert.Equal(
            "case_id,entity_id,entity_kind,source_index,face_index,brep_loop_index,fenestration_source_index,rhino_object_id,geometry_fingerprint,grasshopper_path,grasshopper_index",
            geometryLines[0]);
        Assert.Contains(",OPEN-A,fenestration,", geometryLines[1], StringComparison.Ordinal);
        Assert.Contains(",ZONE-B,zone,", geometryLines[2], StringComparison.Ordinal);

        using JsonDocument manifest = JsonDocument.Parse(
            first.GetFile(GreenRetrofitCsvExporter.ManifestFileName).Content);
        JsonElement root = manifest.RootElement;
        Assert.Equal(GreenRetrofitCsvExporter.ManifestSchemaVersion, root.GetProperty("schema_version").GetString());
        Assert.Equal("서울,case", root.GetProperty("case_id").GetString());
        Assert.Equal(model.Id.Value, root.GetProperty("model").GetProperty("id").GetString());
        Assert.Equal(64, root.GetProperty("model").GetProperty("sha256").GetString()!.Length);
        Assert.Equal(64, root.GetProperty("result").GetProperty("sha256").GetString()!.Length);
        Assert.Equal(7, root.GetProperty("files").GetArrayLength());
        Assert.False(root.TryGetProperty("created_at", out _));

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            GreenRetrofitCsvPackage french = GreenRetrofitCsvExporter.CreatePackage(
                result,
                diagnostics.Reverse(),
                geometryMap.Reverse(),
                "서울,case",
                model);
            Assert.Equal(
                first.Files.Select(file => file.Content),
                french.Files.Select(file => file.Content));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ExportRequiresTriggerAndOverwriteAndWritesOnlyInsideRequestedDirectory()
    {
        GreenRetrofitResult result = LoadResult();
        GreenRetrofitModel model = LoadModel();
        string parent = TestOutputDirectory();
        string requested = Path.Combine(parent, "requested");
        string sibling = Path.Combine(parent, "outside-sentinel.txt");
        Directory.CreateDirectory(parent);
        File.WriteAllText(sibling, "outside");

        GreenRetrofitCsvExportResult preview = GreenRetrofitCsvExporter.ExportDirectory(
            requested,
            result,
            Diagnostics(),
            GeometryMap(),
            "../must,remain,data",
            model,
            export: false,
            overwrite: false);

        Assert.False(preview.ExportRequested);
        Assert.False(preview.Written);
        Assert.False(Directory.Exists(requested));
        Assert.Equal("outside", File.ReadAllText(sibling));

        GreenRetrofitCsvExportResult written = GreenRetrofitCsvExporter.ExportDirectory(
            requested,
            result,
            Diagnostics(),
            GeometryMap(),
            "../must,remain,data",
            model,
            export: true,
            overwrite: false);

        Assert.True(written.ExportRequested);
        Assert.True(written.Written);
        Assert.Equal(8, written.FilePaths.Count);
        Assert.All(
            written.FilePaths,
            path => Assert.StartsWith(
                Path.GetFullPath(requested) + Path.DirectorySeparatorChar,
                path,
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            "outside-sentinel.txt",
            Assert.Single(Directory.GetFiles(parent).Select(Path.GetFileName)));
        Assert.All(
            written.FilePaths.Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)),
            path => Assert.Equal(Utf8Bom, File.ReadAllBytes(path).Take(Utf8Bom.Length).ToArray()));
        Assert.NotEqual(
            Utf8Bom,
            File.ReadAllBytes(Path.Combine(requested, GreenRetrofitCsvExporter.ManifestFileName))
                .Take(Utf8Bom.Length)
                .ToArray());

        string summaryPath = Path.Combine(requested, GreenRetrofitCsvExporter.SummaryFileName);
        File.WriteAllText(summaryPath, "protected");
        Assert.Throws<IOException>(() => GreenRetrofitCsvExporter.ExportDirectory(
            requested,
            result,
            Diagnostics(),
            GeometryMap(),
            "case",
            model,
            export: true,
            overwrite: false));
        Assert.Equal("protected", File.ReadAllText(summaryPath));

        GreenRetrofitCsvExportResult overwritten = GreenRetrofitCsvExporter.ExportDirectory(
            requested,
            result,
            Diagnostics(),
            GeometryMap(),
            "case",
            model,
            export: true,
            overwrite: true);
        Assert.True(overwritten.Written);
        Assert.Equal(Utf8Bom, File.ReadAllBytes(summaryPath).Take(Utf8Bom.Length).ToArray());
        Assert.Equal("outside", File.ReadAllText(sibling));
        Assert.False(File.Exists(Path.Combine(parent, GreenRetrofitCsvExporter.SummaryFileName)));
    }

    [Fact]
    public void MonthlyDataHasStableFuelAndEndUseBranches()
    {
        GreenRetrofitResult result = LoadResult();

        GreenRetrofitMonthlyData fuel = GreenRetrofitMonthlyData.Create(
            result,
            GreenRetrofitMetric.SiteUses,
            gross: false,
            GreenRetrofitSeriesGrouping.Fuel);
        GreenRetrofitMonthlyData grossFuel = GreenRetrofitMonthlyData.Create(
            result,
            GreenRetrofitMetric.SiteUses,
            gross: true,
            GreenRetrofitSeriesGrouping.Fuel);
        GreenRetrofitMonthlyData endUse = GreenRetrofitMonthlyData.Create(
            result,
            GreenRetrofitMetric.Carbon,
            gross: false,
            GreenRetrofitSeriesGrouping.EndUse);

        Assert.Equal(Enumerable.Range(1, 12), fuel.XValues);
        Assert.Equal(ExpectedFuelNames, fuel.Series.Select(series => series.Name));
        Assert.All(fuel.Series, series => Assert.Equal(12, series.Values.Count));
        Assert.Equal(7, endUse.Series.Count);
        Assert.Equal("heating", endUse.Series[0].Name);
        Assert.Equal("generators", endUse.Series[6].Name);
        Assert.Equal("kgCO2e/m2", endUse.Unit);
        Assert.Equal(
            GreenRetrofitResult.Round(fuel.Series[0].Values[0] * result.TotalArea),
            grossFuel.Series[0].Values[0]);
    }

    private static Diagnostic[] Diagnostics()
    {
        return new[]
        {
            new Diagnostic(
                "SD.CSV.A",
                DiagnosticSeverity.Info,
                "한글, comma",
                new EntityId("OBJECT-A"),
                new GeometryProvenance(
                    new Guid("3147c574-7622-4cb0-a6a8-b1b2595f42aa"),
                    2,
                    "fingerprint-a",
                    "{0;1}",
                    3),
                "Keep \"quoted\" values."),
            new Diagnostic(
                "SD.CSV.B",
                DiagnosticSeverity.Error,
                "Second message",
                suggestedAction: "Fix it."),
        };
    }

    private static GreenRetrofitGeometryMapEntry[] GeometryMap()
    {
        return new[]
        {
            new GreenRetrofitGeometryMapEntry(
                new EntityId("ZONE-B"),
                GreenRetrofitGeometryKind.Zone,
                1,
                null,
                null,
                null,
                new GeometryProvenance(null, null, "zone-fingerprint", "{1}", 0)),
            new GreenRetrofitGeometryMapEntry(
                new EntityId("OPEN-A"),
                GreenRetrofitGeometryKind.Fenestration,
                0,
                4,
                2,
                null,
                new GeometryProvenance(null, 4, "opening-fingerprint", "{0}", 1)),
        };
    }

    private static GreenRetrofitResult LoadResult()
    {
        return GrrReader.ReadFile(Fixture("grr", "ASHRAE 140 modified.grr")).RequireResult();
    }

    private static GreenRetrofitModel LoadModel()
    {
        return GrmReader.ReadFile(Fixture("grm", "ASHRAE 140 modified.grm")).RequireModel();
    }

    private static string Fixture(string kind, string name)
    {
        return Path.Combine(RepositoryRoot(), "fixtures", "simple-dragon", kind, name);
    }

    private static string TestOutputDirectory()
    {
        return Path.Combine(
            RepositoryRoot(),
            "temp",
            "tests",
            "simpledragon-csv",
            Guid.NewGuid().ToString("N"));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dragons.Grasshopper.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string[] Lines(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
