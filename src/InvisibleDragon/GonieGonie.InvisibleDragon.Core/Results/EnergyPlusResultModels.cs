using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.EnergyPlus.Runtime;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Results;

public enum EnergyPlusDiagnosticSeverity
{
    Warning,
    Severe,
    Fatal,
}

public enum EnergyPlusResultSourceKind
{
    Error,
    Audit,
    Boundary,
    TabularCsv,
}

public sealed class EnergyPlusDiagnostic
{
    [JsonConstructor]
    public EnergyPlusDiagnostic(
        EnergyPlusDiagnosticSeverity severity,
        string message,
        IReadOnlyList<string>? continuationLines,
        int sourceLine)
    {
        Severity = severity;
        Message = Required(message, nameof(message));
        ContinuationLines = Copy(continuationLines);
        EnergyPlusResultModelGuard.AtLeastOne(sourceLine, nameof(sourceLine));
        SourceLine = sourceLine;
    }

    [JsonPropertyOrder(0)]
    public EnergyPlusDiagnosticSeverity Severity { get; }

    [JsonPropertyOrder(1)]
    public string Message { get; }

    [JsonPropertyOrder(2)]
    public IReadOnlyList<string> ContinuationLines { get; }

    [JsonPropertyOrder(3)]
    public int SourceLine { get; }

    [JsonIgnore]
    public Diagnostic ContractDiagnostic => new(
        "ENERGYPLUS_" + Severity.ToString().ToUpperInvariant(),
        Severity switch
        {
            EnergyPlusDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            EnergyPlusDiagnosticSeverity.Severe => DiagnosticSeverity.Error,
            EnergyPlusDiagnosticSeverity.Fatal => DiagnosticSeverity.Fatal,
            _ => throw new InvalidOperationException("Unknown EnergyPlus diagnostic severity."),
        },
        Message,
        suggestedAction: ContinuationLines.Count == 0
            ? null
            : string.Join(Environment.NewLine, ContinuationLines));

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static ReadOnlyCollection<string> Copy(IReadOnlyList<string>? values)
    {
        return new ReadOnlyCollection<string>((values ?? Array.Empty<string>()).ToArray());
    }
}

public sealed class EnergyPlusDiagnosticSummary
{
    [JsonConstructor]
    public EnergyPlusDiagnosticSummary(
        int warningCount,
        int severeCount,
        int fatalCount,
        bool completedSuccessfully,
        double? reportedElapsedSeconds)
    {
        Guard.NonNegative(warningCount, nameof(warningCount));
        Guard.NonNegative(severeCount, nameof(severeCount));
        Guard.NonNegative(fatalCount, nameof(fatalCount));
        if (reportedElapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reportedElapsedSeconds));
        }

        WarningCount = warningCount;
        SevereCount = severeCount;
        FatalCount = fatalCount;
        CompletedSuccessfully = completedSuccessfully;
        ReportedElapsedSeconds = reportedElapsedSeconds;
    }

    [JsonPropertyOrder(0)]
    public int WarningCount { get; }

    [JsonPropertyOrder(1)]
    public int SevereCount { get; }

    [JsonPropertyOrder(2)]
    public int FatalCount { get; }

    [JsonPropertyOrder(3)]
    public bool CompletedSuccessfully { get; }

    [JsonPropertyOrder(4)]
    public double? ReportedElapsedSeconds { get; }

    [JsonIgnore]
    public TimeSpan? ReportedElapsed => ReportedElapsedSeconds.HasValue
        ? TimeSpan.FromSeconds(ReportedElapsedSeconds.Value)
        : null;
}

public sealed class EnergyPlusErrorLog
{
    [JsonConstructor]
    public EnergyPlusErrorLog(
        string? energyPlusVersion,
        string? energyPlusBuild,
        string? runTimestampText,
        IReadOnlyList<EnergyPlusDiagnostic>? diagnostics,
        EnergyPlusDiagnosticSummary summary)
    {
        EnergyPlusVersion = Optional(energyPlusVersion);
        EnergyPlusBuild = Optional(energyPlusBuild);
        RunTimestampText = Optional(runTimestampText);
        Diagnostics = new ReadOnlyCollection<EnergyPlusDiagnostic>(
            (diagnostics ?? Array.Empty<EnergyPlusDiagnostic>()).ToArray());
        Summary = Guard.NotNull(summary, nameof(summary));
    }

    [JsonPropertyOrder(0)]
    public string? EnergyPlusVersion { get; }

    [JsonPropertyOrder(1)]
    public string? EnergyPlusBuild { get; }

    [JsonPropertyOrder(2)]
    public string? RunTimestampText { get; }

    [JsonPropertyOrder(3)]
    public IReadOnlyList<EnergyPlusDiagnostic> Diagnostics { get; }

    [JsonPropertyOrder(4)]
    public EnergyPlusDiagnosticSummary Summary { get; }

    private static string? Optional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}

public sealed class EnergyPlusAuditEntry
{
    [JsonConstructor]
    public EnergyPlusAuditEntry(string key, string rawValue, double? numericValue)
    {
        Key = key?.Trim() ?? string.Empty;
        RawValue = rawValue?.Trim() ?? string.Empty;
        NumericValue = numericValue;
    }

    [JsonPropertyOrder(0)]
    public string Key { get; }

    [JsonPropertyOrder(1)]
    public string RawValue { get; }

    [JsonPropertyOrder(2)]
    public double? NumericValue { get; }
}

public sealed class EnergyPlusAuditLog
{
    [JsonConstructor]
    public EnergyPlusAuditLog(
        IReadOnlyList<EnergyPlusAuditEntry>? entries,
        IReadOnlyList<string>? messages)
    {
        Entries = new ReadOnlyCollection<EnergyPlusAuditEntry>(
            (entries ?? Array.Empty<EnergyPlusAuditEntry>()).ToArray());
        Messages = new ReadOnlyCollection<string>((messages ?? Array.Empty<string>()).ToArray());
    }

    [JsonPropertyOrder(0)]
    public IReadOnlyList<EnergyPlusAuditEntry> Entries { get; }

    [JsonPropertyOrder(1)]
    public IReadOnlyList<string> Messages { get; }

    public IEnumerable<EnergyPlusAuditEntry> Find(string key)
    {
        return Entries.Where(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class EnergyPlusBoundaryRecord
{
    [JsonConstructor]
    public EnergyPlusBoundaryRecord(string recordType, IReadOnlyList<string>? fields, int sourceLine)
    {
        RecordType = recordType?.Trim() ?? string.Empty;
        Fields = new ReadOnlyCollection<string>((fields ?? Array.Empty<string>()).ToArray());
        EnergyPlusResultModelGuard.AtLeastOne(sourceLine, nameof(sourceLine));
        SourceLine = sourceLine;
    }

    [JsonPropertyOrder(0)]
    public string RecordType { get; }

    [JsonPropertyOrder(1)]
    public IReadOnlyList<string> Fields { get; }

    [JsonPropertyOrder(2)]
    public int SourceLine { get; }
}

public sealed class EnergyPlusBoundaryData
{
    [JsonConstructor]
    public EnergyPlusBoundaryData(
        IReadOnlyList<string>? comments,
        IReadOnlyList<EnergyPlusBoundaryRecord>? records)
    {
        Comments = new ReadOnlyCollection<string>((comments ?? Array.Empty<string>()).ToArray());
        Records = new ReadOnlyCollection<EnergyPlusBoundaryRecord>(
            (records ?? Array.Empty<EnergyPlusBoundaryRecord>()).ToArray());
    }

    [JsonPropertyOrder(0)]
    public IReadOnlyList<string> Comments { get; }

    [JsonPropertyOrder(1)]
    public IReadOnlyList<EnergyPlusBoundaryRecord> Records { get; }

    public IEnumerable<EnergyPlusBoundaryRecord> OfType(string recordType)
    {
        return Records.Where(record => string.Equals(
            record.RecordType,
            recordType,
            StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class EnergyPlusTabularCell
{
    [JsonConstructor]
    public EnergyPlusTabularCell(string text, double? numericValue)
    {
        Text = text?.Trim() ?? string.Empty;
        NumericValue = numericValue;
    }

    [JsonPropertyOrder(0)]
    public string Text { get; }

    [JsonPropertyOrder(1)]
    public double? NumericValue { get; }
}

public sealed class EnergyPlusTabularRow
{
    [JsonConstructor]
    public EnergyPlusTabularRow(IReadOnlyList<EnergyPlusTabularCell>? cells)
    {
        Cells = new ReadOnlyCollection<EnergyPlusTabularCell>(
            (cells ?? Array.Empty<EnergyPlusTabularCell>()).ToArray());
    }

    [JsonPropertyOrder(0)]
    public IReadOnlyList<EnergyPlusTabularCell> Cells { get; }

    public EnergyPlusTabularCell this[int index] => Cells[index];
}

public sealed class EnergyPlusTabularTable
{
    [JsonConstructor]
    public EnergyPlusTabularTable(
        string sourceFileName,
        string reportName,
        string scope,
        IReadOnlyList<string>? titlePath,
        EnergyPlusTabularRow header,
        IReadOnlyList<EnergyPlusTabularRow>? rows,
        bool isMonthly)
    {
        SourceFileName = sourceFileName?.Trim() ?? string.Empty;
        ReportName = reportName?.Trim() ?? string.Empty;
        Scope = scope?.Trim() ?? string.Empty;
        TitlePath = new ReadOnlyCollection<string>((titlePath ?? Array.Empty<string>()).ToArray());
        Header = Guard.NotNull(header, nameof(header));
        Rows = new ReadOnlyCollection<EnergyPlusTabularRow>(
            (rows ?? Array.Empty<EnergyPlusTabularRow>()).ToArray());
        IsMonthly = isMonthly;
    }

    [JsonPropertyOrder(0)]
    public string SourceFileName { get; }

    [JsonPropertyOrder(1)]
    public string ReportName { get; }

    [JsonPropertyOrder(2)]
    public string Scope { get; }

    [JsonPropertyOrder(3)]
    public IReadOnlyList<string> TitlePath { get; }

    [JsonPropertyOrder(4)]
    public EnergyPlusTabularRow Header { get; }

    [JsonPropertyOrder(5)]
    public IReadOnlyList<EnergyPlusTabularRow> Rows { get; }

    [JsonPropertyOrder(6)]
    public bool IsMonthly { get; }

    [JsonIgnore]
    public string Title => TitlePath.Count == 0 ? string.Empty : TitlePath[TitlePath.Count - 1];

    public IEnumerable<EnergyPlusTabularRow> FindRows(string rowLabel)
    {
        return Rows.Where(row => row.Cells.Any(cell => string.Equals(
            cell.Text,
            rowLabel,
            StringComparison.OrdinalIgnoreCase)));
    }

    public bool TryGetCell(
        string rowLabel,
        string columnHeading,
        out EnergyPlusTabularCell? cell)
    {
        int columnIndex = -1;
        for (int index = 0; index < Header.Cells.Count; index++)
        {
            if (string.Equals(
                    Header.Cells[index].Text,
                    columnHeading,
                    StringComparison.OrdinalIgnoreCase))
            {
                columnIndex = index;
                break;
            }
        }

        if (columnIndex >= 0)
        {
            foreach (EnergyPlusTabularRow row in FindRows(rowLabel))
            {
                if (columnIndex < row.Cells.Count)
                {
                    cell = row.Cells[columnIndex];
                    return true;
                }
            }
        }

        cell = null;
        return false;
    }
}

public sealed class EnergyPlusResultMetadata
{
    [JsonConstructor]
    public EnergyPlusResultMetadata(
        string? runId,
        EnergyPlusRunState? runtimeState,
        bool? runtimeSucceeded,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? finishedAtUtc,
        double? runtimeElapsedSeconds,
        double? energyPlusProcessElapsedSeconds,
        string? workDirectory,
        bool? workDirectoryRetained)
    {
        RunId = runId;
        RuntimeState = runtimeState;
        RuntimeSucceeded = runtimeSucceeded;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = finishedAtUtc;
        RuntimeElapsedSeconds = NonNegative(runtimeElapsedSeconds, nameof(runtimeElapsedSeconds));
        EnergyPlusProcessElapsedSeconds = NonNegative(
            energyPlusProcessElapsedSeconds,
            nameof(energyPlusProcessElapsedSeconds));
        WorkDirectory = workDirectory;
        WorkDirectoryRetained = workDirectoryRetained;
    }

    [JsonPropertyOrder(0)]
    public string? RunId { get; }

    [JsonPropertyOrder(1)]
    public EnergyPlusRunState? RuntimeState { get; }

    [JsonPropertyOrder(2)]
    public bool? RuntimeSucceeded { get; }

    [JsonPropertyOrder(3)]
    public DateTimeOffset? StartedAtUtc { get; }

    [JsonPropertyOrder(4)]
    public DateTimeOffset? FinishedAtUtc { get; }

    [JsonPropertyOrder(5)]
    public double? RuntimeElapsedSeconds { get; }

    [JsonPropertyOrder(6)]
    public double? EnergyPlusProcessElapsedSeconds { get; }

    [JsonPropertyOrder(7)]
    public string? WorkDirectory { get; }

    [JsonPropertyOrder(8)]
    public bool? WorkDirectoryRetained { get; }

    private static double? NonNegative(double? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }
}

public sealed class EnergyPlusResultSource
{
    [JsonConstructor]
    public EnergyPlusResultSource(
        EnergyPlusResultSourceKind kind,
        string fileName,
        string? fullPath,
        long length,
        string sha256)
    {
        Kind = kind;
        FileName = fileName?.Trim() ?? string.Empty;
        FullPath = fullPath;
        EnergyPlusResultModelGuard.NonNegative(length, nameof(length));
        Length = length;
        Sha256 = GonieGonie.InvisibleDragon.Idd.IddSchema.NormalizeSha256(sha256);
    }

    [JsonPropertyOrder(0)]
    public EnergyPlusResultSourceKind Kind { get; }

    [JsonPropertyOrder(1)]
    public string FileName { get; }

    [JsonPropertyOrder(2)]
    public string? FullPath { get; }

    [JsonPropertyOrder(3)]
    public long Length { get; }

    [JsonPropertyOrder(4)]
    public string Sha256 { get; }
}

public sealed class EnergyPlusSimulationResult
{
    public const string CurrentSchema = "goniegonie.invisible-dragon.energyplus-result.v1";

    [JsonConstructor]
    public EnergyPlusSimulationResult(
        string schema,
        EnergyPlusResultMetadata metadata,
        EnergyPlusErrorLog errorLog,
        EnergyPlusAuditLog audit,
        EnergyPlusBoundaryData boundary,
        IReadOnlyList<EnergyPlusTabularTable>? tables,
        IReadOnlyList<EnergyPlusResultSource>? sources)
    {
        if (!string.Equals(schema, CurrentSchema, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported EnergyPlus result schema '{schema}'.", nameof(schema));
        }

        Schema = schema;
        Metadata = Guard.NotNull(metadata, nameof(metadata));
        ErrorLog = Guard.NotNull(errorLog, nameof(errorLog));
        Audit = Guard.NotNull(audit, nameof(audit));
        Boundary = Guard.NotNull(boundary, nameof(boundary));
        Tables = new ReadOnlyCollection<EnergyPlusTabularTable>(
            (tables ?? Array.Empty<EnergyPlusTabularTable>()).ToArray());
        Sources = new ReadOnlyCollection<EnergyPlusResultSource>(
            (sources ?? Array.Empty<EnergyPlusResultSource>()).ToArray());
    }

    [JsonPropertyOrder(0)]
    public string Schema { get; }

    [JsonPropertyOrder(1)]
    public EnergyPlusResultMetadata Metadata { get; }

    [JsonPropertyOrder(2)]
    public EnergyPlusErrorLog ErrorLog { get; }

    [JsonPropertyOrder(3)]
    public EnergyPlusAuditLog Audit { get; }

    [JsonPropertyOrder(4)]
    public EnergyPlusBoundaryData Boundary { get; }

    [JsonPropertyOrder(5)]
    public IReadOnlyList<EnergyPlusTabularTable> Tables { get; }

    [JsonPropertyOrder(6)]
    public IReadOnlyList<EnergyPlusResultSource> Sources { get; }

    [JsonIgnore]
    public IReadOnlyList<EnergyPlusTabularTable> MonthlyTables => new ReadOnlyCollection<EnergyPlusTabularTable>(
        Tables.Where(table => table.IsMonthly).ToArray());

    [JsonIgnore]
    public ValidationResult Diagnostics => ValidationResult.From(
        ErrorLog.Diagnostics.Select(diagnostic => diagnostic.ContractDiagnostic));
}

internal static class EnergyPlusResultModelGuard
{
    internal static void AtLeastOne(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    internal static void NonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
