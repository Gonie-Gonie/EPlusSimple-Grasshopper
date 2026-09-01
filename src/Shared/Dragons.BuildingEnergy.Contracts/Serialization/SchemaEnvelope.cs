using System.Text.Json.Serialization;
using Dragons.BuildingEnergy.Contracts.Internal;

namespace Dragons.BuildingEnergy.Contracts;

/// <summary>
/// Adds explicit schema and compatibility metadata to a serialized payload.
/// </summary>
/// <typeparam name="TPayload">The payload type.</typeparam>
public sealed record SchemaEnvelope<TPayload>
{
    /// <summary>
    /// Creates a versioned payload envelope.
    /// </summary>
    [JsonConstructor]
    public SchemaEnvelope(
        string schemaVersion,
        string coreVersion,
        string upstreamCommit,
        TPayload payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        SchemaVersion = ContractGuard.RequiredText(schemaVersion, nameof(schemaVersion));
        CoreVersion = ContractGuard.RequiredText(coreVersion, nameof(coreVersion));
        UpstreamCommit = ContractGuard.RequiredText(upstreamCommit, nameof(upstreamCommit));
        Payload = payload;
    }

    /// <summary>
    /// Gets the payload schema identifier.
    /// </summary>
    [JsonPropertyOrder(0)]
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets the producing core version.
    /// </summary>
    [JsonPropertyOrder(1)]
    public string CoreVersion { get; }

    /// <summary>
    /// Gets the exact compatible upstream commit.
    /// </summary>
    [JsonPropertyOrder(2)]
    public string UpstreamCommit { get; }

    /// <summary>
    /// Gets the versioned payload.
    /// </summary>
    [JsonPropertyOrder(3)]
    public TPayload Payload { get; }
}
