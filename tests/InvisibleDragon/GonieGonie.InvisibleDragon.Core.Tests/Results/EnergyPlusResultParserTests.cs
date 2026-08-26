using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Results;

namespace GonieGonie.InvisibleDragon.Tests.Results;

public sealed class EnergyPlusResultParserTests
{
    private const string ErrorText = """
        Program Version,EnergyPlus, Version 24.2.0-94a887817b, YMD=2026.08.24 22:23,
           ** Warning ** First warning
           **   ~~~   ** warning detail=1.25
           ** Severe  ** Broken input
           **  Fatal  ** Simulation stopped
           ************* EnergyPlus Terminated--Fatal Error Detected-- 2 Warning; 1 Severe Errors; 1 Fatal Errors; Elapsed Time=01hr 02min  3.50sec
        """;

    [Fact]
    public void ErrorParserCapturesDiagnosticsSummaryVersionAndElapsedTime()
    {
        EnergyPlusErrorLog log = EnergyPlusResultParser.ParseErrorLog(ErrorText);

        Assert.Equal("24.2.0", log.EnergyPlusVersion);
        Assert.Equal("94a887817b", log.EnergyPlusBuild);
        Assert.Equal("2026.08.24 22:23", log.RunTimestampText);
        Assert.Equal(3, log.Diagnostics.Count);
        Assert.Equal(EnergyPlusDiagnosticSeverity.Warning, log.Diagnostics[0].Severity);
        Assert.Equal("warning detail=1.25", log.Diagnostics[0].ContinuationLines[0]);
        Assert.Equal(EnergyPlusDiagnosticSeverity.Severe, log.Diagnostics[1].Severity);
        Assert.Equal(DiagnosticSeverity.Fatal, log.Diagnostics[2].ContractDiagnostic.Severity);
        Assert.Equal(2, log.Summary.WarningCount);
        Assert.Equal(1, log.Summary.SevereCount);
        Assert.Equal(1, log.Summary.FatalCount);
        Assert.False(log.Summary.CompletedSuccessfully);
        Assert.Equal(3723.5, log.Summary.ReportedElapsedSeconds);
    }

    [Fact]
    public void AuditParserRetainsDuplicateKeysAndParsesNumbersInvariantly()
    {
        const string text = """
            Processing Schedule Input -- Start
             MonthlyInputCount=           0
             ScientificValue= 1.257224E-003
             MonthlyInputCount=           2
             TextValue= SutherlandHodgman
            """;

        EnergyPlusAuditLog audit = EnergyPlusResultParser.ParseAudit(text);

        Assert.Equal(4, audit.Entries.Count);
        Assert.Equal(
            new double?[] { 0d, 2d },
            audit.Find("monthlyinputcount").Select(item => item.NumericValue).ToArray());
        Assert.Equal(0.001257224, audit.Find("ScientificValue").Single().NumericValue);
        Assert.Null(audit.Find("TextValue").Single().NumericValue);
        Assert.Single(audit.Messages);
    }

    [Fact]
    public void BoundaryParserHandlesQuotedCsvAndQueryableRecordTypes()
    {
        const string text = """
            ! <Node>,<Node Number>,<Node Name>,<Node Type>
             Node,1,"Supply, Main",Air
             Node,2,Return
             #Nodes,2
            """;

        EnergyPlusBoundaryData boundary = EnergyPlusResultParser.ParseBoundary(text);

        Assert.Single(boundary.Comments);
        Assert.Equal(3, boundary.Records.Count);
        EnergyPlusBoundaryRecord[] nodes = boundary.OfType("node").ToArray();
        Assert.Equal(2, nodes.Length);
        Assert.True(boundary.TryGetColumns("node", out IReadOnlyList<string>? columns));
        Assert.Equal(new[] { "Node Number", "Node Name", "Node Type" }, columns);
        Assert.True(boundary.TryGetField(nodes[0], "Node Name", out string? nodeName));
        Assert.Equal("Supply, Main", nodeName);
        Assert.True(boundary.TryGetField(nodes[1], "Node Type", out string? missingType));
        Assert.Null(missingType);
        Assert.Equal("2", boundary.OfType("#Nodes").Single().Fields[0]);
        Assert.False(boundary.TryGetColumns("#Nodes", out _));
        Assert.False(boundary.TryGetField(nodes[0], "Unknown Column", out _));
    }

    [Fact]
    public void BoundarySchemasUseLastDuplicateHeaderAndRemainDerivedAcrossJson()
    {
        const string text = """
            ! <Node>,<Legacy Number>
            ! <Node>,<Node Number>,<Node Name>
             Node,7,Supply
            """;
        EnergyPlusBoundaryData boundary = EnergyPlusResultParser.ParseBoundary(text);

        Assert.Single(boundary.Schemas);
        Assert.True(boundary.TryGetColumns("NODE", out IReadOnlyList<string>? columns));
        Assert.Equal(new[] { "Node Number", "Node Name" }, columns);
        IDictionary<string, IReadOnlyList<string>> mutableSchemas =
            Assert.IsAssignableFrom<IDictionary<string, IReadOnlyList<string>>>(boundary.Schemas);
        Assert.True(mutableSchemas.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => mutableSchemas.Add("Other", Array.Empty<string>()));
        IList<string> mutableColumns = Assert.IsAssignableFrom<IList<string>>(columns);
        Assert.True(mutableColumns.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableColumns.Add("Other"));
        EnergyPlusBoundaryRecord record = Assert.Single(boundary.Records);
        Assert.False(boundary.TryGetField(record, "Legacy Number", out _));
        Assert.True(boundary.TryGetField(record, "Node Name", out string? name));
        Assert.Equal("Supply", name);

        string json = JsonSerializer.Serialize(boundary, BuildingEnergyJson.CreateOptions());
        Assert.DoesNotContain("schemas", json, StringComparison.OrdinalIgnoreCase);
        EnergyPlusBoundaryData restored = JsonSerializer.Deserialize<EnergyPlusBoundaryData>(
            json,
            BuildingEnergyJson.CreateOptions())!;
        Assert.True(restored.TryGetColumns("Node", out IReadOnlyList<string>? restoredColumns));
        Assert.Equal(columns, restoredColumns);
    }

    [Fact]
    public void TabularParserBuildsMonthlyTableWithInvariantCells()
    {
        const string csv = """
            Program Version:,EnergyPlus, Version 24.2.0-build
            Tabular Output Report in Format: ,Comma

            REPORT:,Custom Monthly Report
            FOR:,Entire Facility

            Energy Consumption Monthly

            ,,January,February,Annual
            ,Heating,1.25,2.50,3.75
            ,Cooling,,4.00,4.00
            """;

        IReadOnlyList<EnergyPlusTabularTable> tables = EnergyPlusResultParser.ParseTabular("customtbl.csv", csv);

        EnergyPlusTabularTable table = Assert.Single(tables);
        Assert.Equal("Custom Monthly Report", table.ReportName);
        Assert.Equal("Entire Facility", table.Scope);
        Assert.Equal("Energy Consumption Monthly", table.Title);
        Assert.True(table.IsMonthly);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(1.25, table.Rows[0][2].NumericValue);
        Assert.Null(table.Rows[1][2].NumericValue);
        Assert.True(table.TryGetCell("Heating", "February", out EnergyPlusTabularCell? february));
        Assert.Equal(2.5, february!.NumericValue);
    }

    [Fact]
    public void TabularRowLookupMatchesOnlyTheInferredLabelColumn()
    {
        const string csv = """
            REPORT:,Collision
            FOR:,Meter
            Collision Table
            ,,January,February
            ,Heating,1.25,Target Row
            ,Target Row,9.00,10.00
            """;

        EnergyPlusTabularTable table = Assert.Single(
            EnergyPlusResultParser.ParseTabular("collisiontbl.csv", csv));

        EnergyPlusTabularRow row = Assert.Single(table.FindRows("target row"));
        Assert.Equal("Target Row", row[1].Text);
        Assert.True(table.TryGetCell("Target Row", "January", out EnergyPlusTabularCell? january));
        Assert.Equal(9d, january!.NumericValue);
    }

    [Fact]
    public void RuntimeAdapterCopiesMetadataAndCapturedArtifacts()
    {
        DateTimeOffset started = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
        var outputs = new EnergyPlusOutputFiles(
            Artifact(EnergyPlusOutputKind.Error, "eplusout.err", ErrorText),
            Artifact(EnergyPlusOutputKind.Audit, "eplusout.audit", "Count= 1.5"),
            Artifact(EnergyPlusOutputKind.Boundary, "eplusout.bnd", "#Nodes,0"),
            Artifact(EnergyPlusOutputKind.TableCsv, "eplustbl.csv", "REPORT:,R\nFOR:,F\nT\n,,Value\n,Row,2.5\n"),
            new GonieGonie.EnergyPlus.Runtime.EnergyPlusErrorSummary(2, 1, 1));
        var runtimeResult = new EnergyPlusRunResult(
            "RUN-123",
            EnergyPlusRunState.Succeeded,
            null,
            null,
            null,
            false,
            null,
            null,
            outputs,
            started,
            started.AddSeconds(4.25),
            Array.Empty<EnergyPlusRunTransition>(),
            null);

        EnergyPlusSimulationResult result = EnergyPlusResultParser.Parse(runtimeResult);

        Assert.Equal("RUN-123", result.Metadata.RunId);
        Assert.Equal(EnergyPlusRunState.Succeeded, result.Metadata.RuntimeState);
        Assert.True(result.Metadata.RuntimeSucceeded);
        Assert.Equal(4.25, result.Metadata.RuntimeElapsedSeconds);
        Assert.Equal(4, result.Sources.Count);
        Assert.Single(result.Tables);
        Assert.False(result.Diagnostics.IsValid);
    }

    [Fact]
    public void JsonIsDeterministicSnakeCaseAndRoundTrips()
    {
        EnergyPlusSimulationResult original = EnergyPlusResultParser.Parse(CreateRuntimeResult());

        string first = EnergyPlusResultJson.Serialize(original, writeIndented: true);
        string second = EnergyPlusResultJson.Serialize(original, writeIndented: true);
        EnergyPlusSimulationResult restored = EnergyPlusResultJson.Deserialize(first);

        Assert.Equal(first, second);
        Assert.Contains("\"reported_elapsed_seconds\"", first, StringComparison.Ordinal);
        Assert.Contains("\"tabular_csv\"", first, StringComparison.Ordinal);
        Assert.DoesNotContain("$type", first, StringComparison.Ordinal);
        Assert.Equal(original.ErrorLog.Summary.WarningCount, restored.ErrorLog.Summary.WarningCount);
        Assert.Equal(original.Tables[0].Rows[0][2].NumericValue, restored.Tables[0].Rows[0][2].NumericValue);
    }

    [Fact]
    public void DirectoryParserFindsAllSupportedFilesAndEveryTblCsv()
    {
        using var directory = new ResultTestDirectory();
        directory.Write("eplusout.err", ErrorText);
        directory.Write("eplusout.audit", "Count=1");
        directory.Write("eplusout.bnd", "#Nodes,0");
        directory.Write("eplustbl.csv", "REPORT:,Annual\nFOR:,Facility\nAnnual Table\n,,Value\n,Row,1\n");
        directory.Write("customtbl.csv", "REPORT:,Monthly\nFOR:,Facility\nMonthly Table\n,,January,February\n,Row,1,2\n");

        EnergyPlusSimulationResult result = EnergyPlusResultParser.ParseDirectory(directory.Path);

        Assert.Equal(5, result.Sources.Count);
        Assert.Equal(2, result.Tables.Count);
        Assert.Single(result.MonthlyTables);
        Assert.All(result.Sources, source => Assert.Equal(64, source.Sha256.Length));
        Assert.Equal(directory.Path, result.Metadata.WorkDirectory);
    }

    [Fact]
    public void RuntimeAdapterReadsRetainedFilesFromOutputSubdirectory()
    {
        using var directory = new ResultTestDirectory();
        string outputDirectory = System.IO.Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(System.IO.Path.Combine(outputDirectory, "eplusout.err"), ErrorText, new UTF8Encoding(false));
        File.WriteAllText(System.IO.Path.Combine(outputDirectory, "eplusout.audit"), "Count=2", new UTF8Encoding(false));
        File.WriteAllText(System.IO.Path.Combine(outputDirectory, "eplusout.bnd"), "#Nodes,0", new UTF8Encoding(false));
        File.WriteAllText(
            System.IO.Path.Combine(outputDirectory, "eplustbl.csv"),
            "REPORT:,Annual\nFOR:,Facility\nAnnual Table\n,,Value\n,Row,1\n",
            new UTF8Encoding(false));
        DateTimeOffset started = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
        var runtimeResult = new EnergyPlusRunResult(
            "RUN-RETAINED",
            EnergyPlusRunState.Succeeded,
            null,
            null,
            directory.Path,
            true,
            null,
            null,
            EnergyPlusOutputFiles.Empty,
            started,
            started.AddSeconds(1),
            Array.Empty<EnergyPlusRunTransition>(),
            null);

        EnergyPlusSimulationResult result = EnergyPlusResultParser.Parse(runtimeResult);

        Assert.Equal("24.2.0", result.ErrorLog.EnergyPlusVersion);
        Assert.Single(result.Audit.Entries);
        Assert.Single(result.Boundary.Records);
        Assert.Single(result.Tables);
        Assert.Equal(4, result.Sources.Count);
        Assert.All(
            result.Sources,
            source => Assert.Equal(outputDirectory, System.IO.Path.GetDirectoryName(source.FullPath)));
        Assert.Equal(directory.Path, result.Metadata.WorkDirectory);
    }

    [EnergyPlusResultsIntegrationFact]
    public async Task ActualEnergyPlusRunParsesCollectedOutputs()
    {
        string root = Environment.GetEnvironmentVariable("GONIEGONIE_ENERGYPLUS_ROOT")
            ?? @"C:\EnergyPlusV24-2-0";
        EnergyPlusRuntimeResolution resolution = await new RuntimeResolver().ResolveAsync(
            new EnergyPlusRuntimeResolveOptions
            {
                RuntimeRoot = root,
                SearchDefaultInstallLocation = false,
                SearchEnvironmentVariables = false,
            });
        Assert.True(resolution.IsSuccess, resolution.Failure?.Detail ?? resolution.Failure?.Message);

        string repositoryRoot = ResultTestDirectory.FindRepositoryRoot();
        var request = new EnergyPlusRunRequest(
            resolution.Runtime!,
            Path.Combine(root, "ExampleFiles", "1ZoneUncontrolled.idf"),
            Path.Combine(root, "WeatherData", "USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.epw"),
            Path.Combine(repositoryRoot, "temp", "integration", "invisible-dragon-results"))
        {
            Timeout = TimeSpan.FromMinutes(3),
            CleanupPolicy = EnergyPlusCleanupPolicy.DeleteAlways,
        };

        EnergyPlusRunResult runtimeResult = await new EnergyPlusRunner().RunAsync(request);
        Assert.True(runtimeResult.IsSuccess, runtimeResult.Failure?.Detail ?? runtimeResult.Failure?.Message);

        EnergyPlusSimulationResult result = EnergyPlusResultParser.Parse(runtimeResult);

        Assert.Equal("24.2.0", result.ErrorLog.EnergyPlusVersion);
        Assert.True(result.ErrorLog.Summary.CompletedSuccessfully);
        Assert.Equal(0, result.ErrorLog.Summary.SevereCount);
        Assert.True(result.ErrorLog.Summary.ReportedElapsedSeconds > 0);
        Assert.True(result.Tables.Count > 20);
        Assert.Contains(result.Tables, table => table.ReportName == "Annual Building Utility Performance Summary");
        Assert.Equal(4, result.Sources.Count);
    }

    private static EnergyPlusRunResult CreateRuntimeResult()
    {
        DateTimeOffset started = new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
        var outputs = new EnergyPlusOutputFiles(
            Artifact(EnergyPlusOutputKind.Error, "eplusout.err", ErrorText),
            null,
            null,
            Artifact(
                EnergyPlusOutputKind.TableCsv,
                "eplustbl.csv",
                "REPORT:,Monthly\nFOR:,Facility\nMonthly\n,,January,February\n,Row,1.5,2.5\n"),
            new GonieGonie.EnergyPlus.Runtime.EnergyPlusErrorSummary(2, 1, 1));
        return new EnergyPlusRunResult(
            "RUN-JSON",
            EnergyPlusRunState.Failed,
            null,
            null,
            null,
            false,
            null,
            null,
            outputs,
            started,
            started.AddSeconds(1),
            Array.Empty<EnergyPlusRunTransition>(),
            null);
    }

    private static EnergyPlusOutputArtifact Artifact(
        EnergyPlusOutputKind kind,
        string fileName,
        string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new EnergyPlusOutputArtifact(kind, fileName, fileName, bytes.LongLength, hash, text);
    }
}

public sealed class EnergyPlusResultsIntegrationFactAttribute : FactAttribute
{
    public EnergyPlusResultsIntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set GONIEGONIE_RUN_ENERGYPLUS_INTEGRATION=1 to run against EnergyPlus 24.2.0.";
        }
    }
}

internal sealed class ResultTestDirectory : IDisposable
{
    public ResultTestDirectory()
    {
        Path = System.IO.Path.Combine(
            FindRepositoryRoot(),
            "temp",
            "tests",
            "invisible-dragon-results",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Write(string fileName, string text)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, fileName), text, new UTF8Encoding(false));
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    public static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(System.IO.Path.Combine(directory.FullName, "Directory.Build.props")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
