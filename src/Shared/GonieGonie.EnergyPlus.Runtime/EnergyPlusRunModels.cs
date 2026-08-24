using System.Text;

namespace GonieGonie.EnergyPlus.Runtime;

public enum EnergyPlusRunState
{
    Pending,
    Validating,
    Preparing,
    ExpandingObjects,
    RunningEnergyPlus,
    CollectingResults,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}

public enum EnergyPlusCleanupPolicy
{
    KeepAlways,
    DeleteOnSuccess,
    DeleteAlways
}

public enum EnergyPlusOutputKind
{
    Error,
    Audit,
    Boundary,
    TableCsv
}

/// <summary>
/// Immutable input for one isolated EnergyPlus simulation.
/// </summary>
public sealed record EnergyPlusRunRequest(
    EnergyPlusRuntimeLayout Runtime,
    string InputIdfPath,
    string? WeatherFilePath,
    string TempRootPath)
{
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromMinutes(30);

    public EnergyPlusCleanupPolicy CleanupPolicy { get; init; } = EnergyPlusCleanupPolicy.KeepAlways;

    public long MaximumCapturedArtifactBytes { get; init; } = 64L * 1024L * 1024L;
}

/// <summary>
/// A timestamped state transition suitable for Grasshopper progress reporting.
/// </summary>
public sealed record EnergyPlusRunTransition(
    EnergyPlusRunState State,
    DateTimeOffset TimestampUtc,
    string Message);

/// <summary>
/// One collected EnergyPlus text artifact. Content is omitted when it exceeds the configured capture limit.
/// </summary>
public sealed record EnergyPlusOutputArtifact(
    EnergyPlusOutputKind Kind,
    string FileName,
    string FullPath,
    long Length,
    string Sha256,
    string? TextContent)
{
    public bool ContentCaptured => TextContent is not null;
}

/// <summary>
/// Counts diagnostics emitted in eplusout.err.
/// </summary>
public sealed record EnergyPlusErrorSummary(int WarningCount, int SevereCount, int FatalCount);

/// <summary>
/// The standard result files produced by EnergyPlus legacy output naming.
/// </summary>
public sealed record EnergyPlusOutputFiles(
    EnergyPlusOutputArtifact? Error,
    EnergyPlusOutputArtifact? Audit,
    EnergyPlusOutputArtifact? Boundary,
    EnergyPlusOutputArtifact? TableCsv,
    EnergyPlusErrorSummary ErrorSummary)
{
    public static EnergyPlusOutputFiles Empty { get; } = new(
        null,
        null,
        null,
        null,
        new EnergyPlusErrorSummary(0, 0, 0));

    public IReadOnlyList<EnergyPlusOutputArtifact> Available
    {
        get
        {
            var available = new List<EnergyPlusOutputArtifact>(4);
            AddIfPresent(available, Error);
            AddIfPresent(available, Audit);
            AddIfPresent(available, Boundary);
            AddIfPresent(available, TableCsv);
            return available;
        }
    }

    private static void AddIfPresent(
        List<EnergyPlusOutputArtifact> artifacts,
        EnergyPlusOutputArtifact? artifact)
    {
        if (artifact is not null)
        {
            artifacts.Add(artifact);
        }
    }
}

/// <summary>
/// Complete, structured outcome of a run, including partial process output on failure or cancellation.
/// </summary>
public sealed record EnergyPlusRunResult(
    string RunId,
    EnergyPlusRunState State,
    EnergyPlusFailure? Failure,
    EnergyPlusRuntimeLayout? Runtime,
    string? WorkDirectory,
    bool WorkDirectoryRetained,
    ProcessExecutionResult? ExpandObjectsProcess,
    ProcessExecutionResult? EnergyPlusProcess,
    EnergyPlusOutputFiles Outputs,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    IReadOnlyList<EnergyPlusRunTransition> History,
    string? CleanupError)
{
    public bool IsSuccess => State == EnergyPlusRunState.Succeeded && Failure is null;

    public TimeSpan Elapsed => FinishedAtUtc - StartedAtUtc;
}

internal static class EnergyPlusOutputCollector
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    internal static async Task<EnergyPlusOutputFiles> CollectAsync(
        string outputDirectory,
        long maximumCapturedBytes,
        CancellationToken cancellationToken)
    {
        var error = await TryCollectAsync(
            outputDirectory,
            "eplusout.err",
            EnergyPlusOutputKind.Error,
            maximumCapturedBytes,
            cancellationToken).ConfigureAwait(false);
        var audit = await TryCollectAsync(
            outputDirectory,
            "eplusout.audit",
            EnergyPlusOutputKind.Audit,
            maximumCapturedBytes,
            cancellationToken).ConfigureAwait(false);
        var boundary = await TryCollectAsync(
            outputDirectory,
            "eplusout.bnd",
            EnergyPlusOutputKind.Boundary,
            maximumCapturedBytes,
            cancellationToken).ConfigureAwait(false);
        var table = await TryCollectAsync(
            outputDirectory,
            "eplustbl.csv",
            EnergyPlusOutputKind.TableCsv,
            maximumCapturedBytes,
            cancellationToken).ConfigureAwait(false);

        return new EnergyPlusOutputFiles(
            error,
            audit,
            boundary,
            table,
            ParseErrorSummary(error?.TextContent));
    }

    private static async Task<EnergyPlusOutputArtifact?> TryCollectAsync(
        string outputDirectory,
        string fileName,
        EnergyPlusOutputKind kind,
        long maximumCapturedBytes,
        CancellationToken cancellationToken)
    {
        var path = RuntimeFileSystem.CombineUnder(outputDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var file = new FileInfo(path);
        var hash = await RuntimeFileSystem.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
        string? content = null;
        if (file.Length <= maximumCapturedBytes)
        {
            content = await RuntimeFileSystem.ReadAllTextAsync(path, Utf8WithoutBom, cancellationToken)
                .ConfigureAwait(false);
        }

        return new EnergyPlusOutputArtifact(kind, fileName, path, file.Length, hash, content);
    }

    private static EnergyPlusErrorSummary ParseErrorSummary(string? errorText)
    {
        if (string.IsNullOrEmpty(errorText))
        {
            return new EnergyPlusErrorSummary(0, 0, 0);
        }

        var warnings = 0;
        var severe = 0;
        var fatal = 0;
        using var reader = new StringReader(errorText);
        while (reader.ReadLine() is { } line)
        {
            if (line.Contains("** Warning **"))
            {
                warnings++;
            }

            if (line.Contains("** Severe  **")
                || line.Contains("** Severe **"))
            {
                severe++;
            }

            if (line.Contains("**  Fatal  **")
                || line.Contains("** Fatal **"))
            {
                fatal++;
            }
        }

        return new EnergyPlusErrorSummary(warnings, severe, fatal);
    }
}
