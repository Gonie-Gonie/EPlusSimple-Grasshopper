using System.Text.Json.Serialization;
using Dragons.BuildingEnergy.Contracts.Internal;

namespace Dragons.BuildingEnergy.Contracts;

/// <summary>
/// Identifies the upstream and runtime baseline implemented by this source tree.
/// </summary>
public sealed record CompatibilityIdentity
{
    /// <summary>
    /// Creates an upstream and EnergyPlus compatibility identity.
    /// </summary>
    [JsonConstructor]
    public CompatibilityIdentity(
        string upstreamRepository,
        string upstreamCommit,
        string upstreamVersion,
        string energyPlusVersion,
        string energyPlusBuild)
    {
        UpstreamRepository = ContractGuard.RequiredText(upstreamRepository, nameof(upstreamRepository));
        UpstreamCommit = ContractGuard.RequiredText(upstreamCommit, nameof(upstreamCommit));
        UpstreamVersion = ContractGuard.RequiredText(upstreamVersion, nameof(upstreamVersion));
        EnergyPlusVersion = ContractGuard.RequiredText(energyPlusVersion, nameof(energyPlusVersion));
        EnergyPlusBuild = ContractGuard.RequiredText(energyPlusBuild, nameof(energyPlusBuild));
    }

    /// <summary>
    /// Gets the baseline pinned by this source tree.
    /// </summary>
    public static CompatibilityIdentity Current { get; } = new(
        "snu-bslab/EPlusSimple",
        "847b01f68f438f560a986072bcaa7768fbf67897",
        "0.7.0",
        "24.2.0",
        "94a887817b");

    /// <summary>
    /// Gets the upstream repository name.
    /// </summary>
    [JsonPropertyOrder(0)]
    public string UpstreamRepository { get; }

    /// <summary>
    /// Gets the exact upstream commit.
    /// </summary>
    [JsonPropertyOrder(1)]
    public string UpstreamCommit { get; }

    /// <summary>
    /// Gets the compatible upstream release version.
    /// </summary>
    [JsonPropertyOrder(2)]
    public string UpstreamVersion { get; }

    /// <summary>
    /// Gets the required EnergyPlus version.
    /// </summary>
    [JsonPropertyOrder(3)]
    public string EnergyPlusVersion { get; }

    /// <summary>
    /// Gets the required EnergyPlus build identifier.
    /// </summary>
    [JsonPropertyOrder(4)]
    public string EnergyPlusBuild { get; }
}
