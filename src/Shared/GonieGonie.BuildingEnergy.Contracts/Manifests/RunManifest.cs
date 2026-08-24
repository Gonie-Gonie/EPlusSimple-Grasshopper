using System.Text.Json.Serialization;
using GonieGonie.BuildingEnergy.Contracts.Internal;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// Captures enough stable metadata to identify and reproduce a simulation run.
/// </summary>
public sealed record RunManifest
{
    /// <summary>
    /// The schema emitted by the first public manifest contract.
    /// </summary>
    public const string CurrentSchemaVersion = "goniegonie-building-energy-run-manifest.v1";

    /// <summary>
    /// Creates a run manifest.
    /// </summary>
    [JsonConstructor]
    public RunManifest(
        string schemaVersion,
        EntityId runId,
        EntityId caseId,
        string runKey,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? completedAtUtc,
        RunStatus status,
        CompatibilityMetadata compatibility,
        OrderedMap<string> contentHashes)
    {
        ContractGuard.NotNull(runId, nameof(runId));
        ContractGuard.NotNull(caseId, nameof(caseId));
        ContractGuard.NotNull(compatibility, nameof(compatibility));
        ContractGuard.NotNull(contentHashes, nameof(contentHashes));

        if (!Enum.IsDefined(typeof(RunStatus), status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown run status.");
        }

        RequireUtc(startedAtUtc, nameof(startedAtUtc));
        if (completedAtUtc.HasValue)
        {
            RequireUtc(completedAtUtc.Value, nameof(completedAtUtc));
            if (completedAtUtc.Value < startedAtUtc)
            {
                throw new ArgumentException("Completion cannot precede the run start.", nameof(completedAtUtc));
            }
        }

        bool isTerminal = status != RunStatus.Running;
        if (isTerminal != completedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Running manifests must omit completion time, and terminal manifests must include it.",
                nameof(completedAtUtc));
        }

        SchemaVersion = ContractGuard.RequiredText(schemaVersion, nameof(schemaVersion));
        RunId = runId;
        CaseId = caseId;
        RunKey = ContractGuard.RequiredText(runKey, nameof(runKey));
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Status = status;
        Compatibility = compatibility;
        ContentHashes = contentHashes;
    }

    /// <summary>
    /// Gets the manifest schema version.
    /// </summary>
    [JsonPropertyOrder(0)]
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets the stable run identifier.
    /// </summary>
    [JsonPropertyOrder(1)]
    public EntityId RunId { get; }

    /// <summary>
    /// Gets the stable research case identifier.
    /// </summary>
    [JsonPropertyOrder(2)]
    public EntityId CaseId { get; }

    /// <summary>
    /// Gets the canonical cache key for the run inputs.
    /// </summary>
    [JsonPropertyOrder(3)]
    public string RunKey { get; }

    /// <summary>
    /// Gets the UTC start timestamp.
    /// </summary>
    [JsonPropertyOrder(4)]
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>
    /// Gets the UTC terminal timestamp, when the run has finished.
    /// </summary>
    [JsonPropertyOrder(5)]
    public DateTimeOffset? CompletedAtUtc { get; }

    /// <summary>
    /// Gets the run lifecycle status.
    /// </summary>
    [JsonPropertyOrder(6)]
    public RunStatus Status { get; }

    /// <summary>
    /// Gets product, upstream, and runtime compatibility metadata.
    /// </summary>
    [JsonPropertyOrder(7)]
    public CompatibilityMetadata Compatibility { get; }

    /// <summary>
    /// Gets ordered input and runtime content hashes.
    /// </summary>
    [JsonPropertyOrder(8)]
    public OrderedMap<string> ContentHashes { get; }

    /// <summary>
    /// Creates a running manifest using the current schema.
    /// </summary>
    public static RunManifest Start(
        EntityId runId,
        EntityId caseId,
        string runKey,
        DateTimeOffset startedAtUtc,
        CompatibilityMetadata compatibility,
        OrderedMap<string>? contentHashes = null)
    {
        return new RunManifest(
            CurrentSchemaVersion,
            runId,
            caseId,
            runKey,
            startedAtUtc,
            null,
            RunStatus.Running,
            compatibility,
            contentHashes ?? new OrderedMap<string>());
    }

    /// <summary>
    /// Returns a terminal copy of a running manifest.
    /// </summary>
    public RunManifest Complete(RunStatus status, DateTimeOffset completedAtUtc)
    {
        if (Status != RunStatus.Running)
        {
            throw new InvalidOperationException("Only a running manifest can be completed.");
        }

        if (status == RunStatus.Running)
        {
            throw new ArgumentException("A completion status must be terminal.", nameof(status));
        }

        return new RunManifest(
            SchemaVersion,
            RunId,
            CaseId,
            RunKey,
            StartedAtUtc,
            completedAtUtc,
            status,
            Compatibility,
            ContentHashes);
    }

    private static void RequireUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Run manifest timestamps must use the UTC offset.", parameterName);
        }
    }
}
