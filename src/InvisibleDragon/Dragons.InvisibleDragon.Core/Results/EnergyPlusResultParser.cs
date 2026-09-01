using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dragons.EnergyPlus.Runtime;
using Dragons.InvisibleDragon.Internal;

namespace Dragons.InvisibleDragon.Results;

/// <summary>
/// Parses EnergyPlus legacy result files and adapts runtime-collected artifacts into a stable result model.
/// </summary>
public static class EnergyPlusResultParser
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly Regex VersionPattern = new(
        @"EnergyPlus,\s+Version\s+(?<version>\d+(?:\.\d+)+)(?:-(?<build>[^,\s]+))?,\s*YMD=(?<timestamp>[^,]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex DiagnosticPattern = new(
        @"^\s*\*\*\s*(?<severity>Warning|Severe|Fatal)\s*\*\*\s*(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ContinuationPattern = new(
        @"^\s*\*\*\s*~~~\s*\*\*\s*(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SummaryCountsPattern = new(
        @"(?<warning>\d+)\s+Warnings?;\s*(?<severe>\d+)\s+Severe Errors?(?:;\s*(?<fatal>\d+)\s+Fatal Errors?)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ElapsedPattern = new(
        @"Elapsed Time\s*=\s*(?<hours>\d+)hr\s*(?<minutes>\d+)min\s*(?<seconds>[0-9]+(?:\.[0-9]+)?)sec",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static EnergyPlusSimulationResult Parse(EnergyPlusRunResult runtimeResult)
    {
        Guard.NotNull(runtimeResult, nameof(runtimeResult));
        string? workDirectory = runtimeResult.WorkDirectory;
        if (!string.IsNullOrWhiteSpace(workDirectory) && Directory.Exists(workDirectory))
        {
            string outputDirectory = Path.Combine(workDirectory!, "output");
            return ParseDirectory(
                Directory.Exists(outputDirectory) ? outputDirectory : workDirectory!,
                runtimeResult);
        }

        EnergyPlusOutputArtifact? error = runtimeResult.Outputs.Error;
        EnergyPlusOutputArtifact? audit = runtimeResult.Outputs.Audit;
        EnergyPlusOutputArtifact? boundary = runtimeResult.Outputs.Boundary;
        EnergyPlusOutputArtifact? table = runtimeResult.Outputs.TableCsv;
        var tables = new List<EnergyPlusTabularTable>();
        if (TryReadArtifact(table, out string? tableText))
        {
            tables.AddRange(ParseTabular(table!.FileName, tableText!));
        }

        return Assemble(
            MetadataFromRuntime(runtimeResult),
            TryReadArtifact(error, out string? errorText) ? ParseErrorLog(errorText!) : EmptyErrorLog(),
            TryReadArtifact(audit, out string? auditText) ? ParseAudit(auditText!) : new EnergyPlusAuditLog(null, null),
            TryReadArtifact(boundary, out string? boundaryText)
                ? ParseBoundary(boundaryText!)
                : new EnergyPlusBoundaryData(null, null),
            tables,
            runtimeResult.Outputs.Available.Select(SourceFromArtifact));
    }

    public static EnergyPlusSimulationResult ParseDirectory(
        string directory,
        EnergyPlusRunResult? runtimeResult = null)
    {
        Guard.NotNull(directory, nameof(directory));
        string fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException($"EnergyPlus result directory does not exist: {fullDirectory}");
        }

        var sources = new List<EnergyPlusResultSource>();
        string? errorText = ReadOptional(fullDirectory, "eplusout.err", EnergyPlusResultSourceKind.Error, sources);
        string? auditText = ReadOptional(fullDirectory, "eplusout.audit", EnergyPlusResultSourceKind.Audit, sources);
        string? boundaryText = ReadOptional(fullDirectory, "eplusout.bnd", EnergyPlusResultSourceKind.Boundary, sources);
        var tables = new List<EnergyPlusTabularTable>();
        foreach (string tablePath in Directory.GetFiles(fullDirectory, "*tbl.csv").OrderBy(
            path => Path.GetFileName(path),
            StringComparer.OrdinalIgnoreCase))
        {
            sources.Add(SourceFromFile(tablePath, EnergyPlusResultSourceKind.TabularCsv));
            tables.AddRange(ParseTabular(Path.GetFileName(tablePath), File.ReadAllText(tablePath, Utf8WithoutBom)));
        }

        return Assemble(
            runtimeResult is null ? EmptyMetadata(fullDirectory) : MetadataFromRuntime(runtimeResult),
            errorText is null ? EmptyErrorLog() : ParseErrorLog(errorText),
            auditText is null ? new EnergyPlusAuditLog(null, null) : ParseAudit(auditText),
            boundaryText is null ? new EnergyPlusBoundaryData(null, null) : ParseBoundary(boundaryText),
            tables,
            sources);
    }

    public static EnergyPlusErrorLog ParseErrorLog(string text)
    {
        Guard.NotNull(text, nameof(text));
        string? version = null;
        string? build = null;
        string? timestamp = null;
        var diagnostics = new List<DiagnosticBuilder>();
        DiagnosticBuilder? current = null;
        int warningCount = 0;
        int severeCount = 0;
        int fatalCount = 0;
        bool completedSuccessfully = false;
        double? elapsedSeconds = null;

        using var reader = new StringReader(text);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (version is null)
            {
                Match versionMatch = VersionPattern.Match(line);
                if (versionMatch.Success)
                {
                    version = versionMatch.Groups["version"].Value;
                    build = EmptyToNull(versionMatch.Groups["build"].Value);
                    timestamp = EmptyToNull(versionMatch.Groups["timestamp"].Value);
                }
            }

            Match diagnosticMatch = DiagnosticPattern.Match(line);
            if (diagnosticMatch.Success)
            {
                current = new DiagnosticBuilder(
                    ParseSeverity(diagnosticMatch.Groups["severity"].Value),
                    diagnosticMatch.Groups["message"].Value,
                    lineNumber);
                diagnostics.Add(current);
                continue;
            }

            Match continuationMatch = ContinuationPattern.Match(line);
            if (continuationMatch.Success && current is not null)
            {
                current.ContinuationLines.Add(continuationMatch.Groups["message"].Value.Trim());
                continue;
            }

            if (ContainsIgnoreCase(line, "EnergyPlus Completed Successfully"))
            {
                completedSuccessfully = true;
            }

            Match countsMatch = SummaryCountsPattern.Match(line);
            if (countsMatch.Success && ContainsIgnoreCase(line, "EnergyPlus"))
            {
                warningCount = ParseCount(countsMatch, "warning");
                severeCount = ParseCount(countsMatch, "severe");
                fatalCount = ParseCount(countsMatch, "fatal");
                if (!completedSuccessfully && fatalCount == 0 &&
                    ContainsIgnoreCase(line, "Fatal"))
                {
                    fatalCount = 1;
                }
            }

            Match elapsedMatch = ElapsedPattern.Match(line);
            if (elapsedMatch.Success)
            {
                elapsedSeconds = ParseElapsedSeconds(elapsedMatch);
            }
        }

        EnergyPlusDiagnostic[] parsedDiagnostics = diagnostics.Select(item => item.Build()).ToArray();
        if (warningCount == 0 && severeCount == 0 && fatalCount == 0)
        {
            warningCount = parsedDiagnostics.Count(item => item.Severity == EnergyPlusDiagnosticSeverity.Warning);
            severeCount = parsedDiagnostics.Count(item => item.Severity == EnergyPlusDiagnosticSeverity.Severe);
            fatalCount = parsedDiagnostics.Count(item => item.Severity == EnergyPlusDiagnosticSeverity.Fatal);
        }

        return new EnergyPlusErrorLog(
            version,
            build,
            timestamp,
            parsedDiagnostics,
            new EnergyPlusDiagnosticSummary(
                warningCount,
                severeCount,
                fatalCount,
                completedSuccessfully,
                elapsedSeconds));
    }

    public static EnergyPlusAuditLog ParseAudit(string text)
    {
        Guard.NotNull(text, nameof(text));
        var entries = new List<EnergyPlusAuditEntry>();
        var messages = new List<string>();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            string normalized = line.Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            int equals = normalized.IndexOf('=');
            if (equals <= 0)
            {
                messages.Add(normalized);
                continue;
            }

            string key = normalized.Substring(0, equals).Trim();
            string value = normalized.Substring(equals + 1).Trim();
            entries.Add(new EnergyPlusAuditEntry(key, value, TryParseNumber(value)));
        }

        return new EnergyPlusAuditLog(entries, messages);
    }

    public static EnergyPlusBoundaryData ParseBoundary(string text)
    {
        Guard.NotNull(text, nameof(text));
        var comments = new List<string>();
        var records = new List<EnergyPlusBoundaryRecord>();
        using var reader = new StringReader(text);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            string normalized = line.Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            if (normalized[0] == '!')
            {
                comments.Add(normalized.Substring(1).TrimStart());
                continue;
            }

            IReadOnlyList<string> fields = Csv.ParseLine(line);
            if (fields.Count == 0 || string.IsNullOrWhiteSpace(fields[0]))
            {
                continue;
            }

            records.Add(new EnergyPlusBoundaryRecord(fields[0], fields.Skip(1).ToArray(), lineNumber));
        }

        return new EnergyPlusBoundaryData(comments, records);
    }

    public static IReadOnlyList<EnergyPlusTabularTable> ParseTabular(string fileName, string text)
    {
        Guard.NotNull(fileName, nameof(fileName));
        Guard.NotNull(text, nameof(text));
        var tables = new List<EnergyPlusTabularTable>();
        var pendingTitles = new List<string>();
        var block = new List<EnergyPlusTabularRow>();
        string reportName = string.Empty;
        string scope = string.Empty;

        void FinishBlock()
        {
            if (block.Count == 0)
            {
                return;
            }

            EnergyPlusTabularRow header = block[0];
            EnergyPlusTabularRow[] rows = block.Skip(1).ToArray();
            bool monthly = IsMonthly(reportName, pendingTitles, header);
            tables.Add(new EnergyPlusTabularTable(
                fileName,
                reportName,
                scope,
                pendingTitles,
                header,
                rows,
                monthly));
            block.Clear();
            pendingTitles.Clear();
        }

        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            IReadOnlyList<string> cells = Csv.ParseLine(line);
            int nonEmptyCount = cells.Count(cell => !string.IsNullOrWhiteSpace(cell));
            if (nonEmptyCount == 0)
            {
                FinishBlock();
                continue;
            }

            string first = cells.FirstOrDefault(cell => !string.IsNullOrWhiteSpace(cell))?.Trim() ?? string.Empty;
            if (first.StartsWith("REPORT:", StringComparison.OrdinalIgnoreCase))
            {
                FinishBlock();
                pendingTitles.Clear();
                reportName = AfterMarker(cells, "REPORT:");
                continue;
            }

            if (first.StartsWith("FOR:", StringComparison.OrdinalIgnoreCase))
            {
                FinishBlock();
                pendingTitles.Clear();
                scope = AfterMarker(cells, "FOR:");
                continue;
            }

            if (IsTabularMetadata(first))
            {
                continue;
            }

            if (nonEmptyCount == 1 && cells.Count > 0 && !string.IsNullOrWhiteSpace(cells[0]))
            {
                FinishBlock();
                pendingTitles.Add(first);
                continue;
            }

            block.Add(ToTabularRow(cells));
        }

        FinishBlock();
        return tables.AsReadOnly();
    }

    private static EnergyPlusSimulationResult Assemble(
        EnergyPlusResultMetadata metadata,
        EnergyPlusErrorLog errorLog,
        EnergyPlusAuditLog audit,
        EnergyPlusBoundaryData boundary,
        IEnumerable<EnergyPlusTabularTable> tables,
        IEnumerable<EnergyPlusResultSource> sources)
    {
        return new EnergyPlusSimulationResult(
            EnergyPlusSimulationResult.CurrentSchema,
            metadata,
            errorLog,
            audit,
            boundary,
            tables.ToArray(),
            sources.ToArray());
    }

    private static EnergyPlusResultMetadata MetadataFromRuntime(EnergyPlusRunResult result)
    {
        return new EnergyPlusResultMetadata(
            result.RunId,
            result.State,
            result.IsSuccess,
            result.StartedAtUtc,
            result.FinishedAtUtc,
            result.Elapsed.TotalSeconds,
            result.EnergyPlusProcess?.Elapsed.TotalSeconds,
            result.WorkDirectory,
            result.WorkDirectoryRetained);
    }

    private static EnergyPlusResultMetadata EmptyMetadata(string workDirectory)
    {
        return new EnergyPlusResultMetadata(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            workDirectory,
            null);
    }

    private static string? ReadOptional(
        string directory,
        string fileName,
        EnergyPlusResultSourceKind kind,
        List<EnergyPlusResultSource> sources)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        sources.Add(SourceFromFile(path, kind));
        return File.ReadAllText(path, Utf8WithoutBom);
    }

    private static bool TryReadArtifact(EnergyPlusOutputArtifact? artifact, out string? text)
    {
        if (artifact is null)
        {
            text = null;
            return false;
        }

        text = artifact.TextContent;
        if (text is null && File.Exists(artifact.FullPath))
        {
            text = File.ReadAllText(artifact.FullPath, Utf8WithoutBom);
        }

        return text is not null;
    }

    private static EnergyPlusResultSource SourceFromArtifact(EnergyPlusOutputArtifact artifact)
    {
        return new EnergyPlusResultSource(
            ToSourceKind(artifact.Kind),
            artifact.FileName,
            artifact.FullPath,
            artifact.Length,
            artifact.Sha256);
    }

    private static EnergyPlusResultSource SourceFromFile(string path, EnergyPlusResultSourceKind kind)
    {
        var info = new FileInfo(path);
        return new EnergyPlusResultSource(kind, info.Name, info.FullName, info.Length, ComputeSha256(path));
    }

    private static EnergyPlusResultSourceKind ToSourceKind(EnergyPlusOutputKind kind)
    {
        return kind switch
        {
            EnergyPlusOutputKind.Error => EnergyPlusResultSourceKind.Error,
            EnergyPlusOutputKind.Audit => EnergyPlusResultSourceKind.Audit,
            EnergyPlusOutputKind.Boundary => EnergyPlusResultSourceKind.Boundary,
            EnergyPlusOutputKind.TableCsv => EnergyPlusResultSourceKind.TabularCsv,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
#if NET6_0_OR_GREATER
        byte[] hash = SHA256.HashData(stream);
#else
        using SHA256 algorithm = SHA256.Create();
        byte[] hash = algorithm.ComputeHash(stream);
#endif
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static EnergyPlusErrorLog EmptyErrorLog()
    {
        return new EnergyPlusErrorLog(
            null,
            null,
            null,
            null,
            new EnergyPlusDiagnosticSummary(0, 0, 0, false, null));
    }

    private static EnergyPlusDiagnosticSeverity ParseSeverity(string value)
    {
        if (string.Equals(value, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return EnergyPlusDiagnosticSeverity.Warning;
        }

        if (string.Equals(value, "Severe", StringComparison.OrdinalIgnoreCase))
        {
            return EnergyPlusDiagnosticSeverity.Severe;
        }

        return EnergyPlusDiagnosticSeverity.Fatal;
    }

    private static int ParseCount(Match match, string groupName)
    {
        Group group = match.Groups[groupName];
        return group.Success
            ? int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture)
            : 0;
    }

    private static double ParseElapsedSeconds(Match match)
    {
        double hours = double.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        double minutes = double.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
        double seconds = double.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture);
        return (hours * 3600) + (minutes * 60) + seconds;
    }

    private static double? TryParseNumber(string value)
    {
        if (double.TryParse(
                value.Trim(),
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out double parsed) &&
            !double.IsNaN(parsed) &&
            !double.IsInfinity(parsed))
        {
            return parsed;
        }

        return null;
    }

    private static EnergyPlusTabularRow ToTabularRow(IEnumerable<string> values)
    {
        return new EnergyPlusTabularRow(values
            .Select(value => new EnergyPlusTabularCell(value, TryParseNumber(value)))
            .ToArray());
    }

    private static bool IsMonthly(
        string reportName,
        IEnumerable<string> titles,
        EnergyPlusTabularRow header)
    {
        if (ContainsIgnoreCase(reportName, "Monthly") ||
            titles.Any(title => ContainsIgnoreCase(title, "Monthly")))
        {
            return true;
        }

        string[] months =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December",
        };
        int monthColumns = header.Cells.Count(cell => months.Contains(cell.Text, StringComparer.OrdinalIgnoreCase));
        return monthColumns >= 2;
    }

    private static bool IsTabularMetadata(string first)
    {
        return first.StartsWith("Program Version:", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("Tabular Output Report", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("Building:", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("Environment:", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("Values gathered over", StringComparison.OrdinalIgnoreCase) ||
            first.All(character => character == '-');
    }

    private static string AfterColon(string value)
    {
        int separator = value.IndexOf(':');
        return separator < 0 ? value.Trim() : value.Substring(separator + 1).Trim();
    }

    private static string AfterMarker(IReadOnlyList<string> cells, string marker)
    {
        for (int index = 0; index < cells.Count; index++)
        {
            string cell = cells[index].Trim();
            if (!cell.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string inline = AfterColon(cell);
            if (inline.Length > 0)
            {
                return inline;
            }

            for (int following = index + 1; following < cells.Count; following++)
            {
                if (!string.IsNullOrWhiteSpace(cells[following]))
                {
                    return cells[following].Trim();
                }
            }
        }

        return string.Empty;
    }

    private static string? EmptyToNull(string value)
    {
        string result = value.Trim();
        return result.Length == 0 ? null : result;
    }

    private static bool ContainsIgnoreCase(string value, string expected)
    {
#if NETFRAMEWORK
        return value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
#else
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
#endif
    }

    private sealed class DiagnosticBuilder
    {
        public DiagnosticBuilder(EnergyPlusDiagnosticSeverity severity, string message, int sourceLine)
        {
            Severity = severity;
            Message = message.Trim();
            SourceLine = sourceLine;
        }

        public EnergyPlusDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public int SourceLine { get; }

        public List<string> ContinuationLines { get; } = new();

        public EnergyPlusDiagnostic Build()
        {
            return new EnergyPlusDiagnostic(Severity, Message, ContinuationLines, SourceLine);
        }
    }

    private static class Csv
    {
        public static ReadOnlyCollection<string> ParseLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }

                    continue;
                }

                if (character == ',' && !quoted)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(character);
            }

            if (quoted)
            {
                throw new FormatException("Unterminated quoted CSV field.");
            }

            values.Add(current.ToString().Trim());
            return values.AsReadOnly();
        }
    }
}
