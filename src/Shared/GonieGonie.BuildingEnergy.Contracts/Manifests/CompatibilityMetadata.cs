using System.Text.Json.Serialization;
using GonieGonie.BuildingEnergy.Contracts.Internal;

namespace GonieGonie.BuildingEnergy.Contracts;

/// <summary>
/// Describes a product release and the exact upstream and component versions it supports.
/// </summary>
public sealed record CompatibilityMetadata
{
    /// <summary>
    /// Creates release compatibility metadata.
    /// </summary>
    [JsonConstructor]
    public CompatibilityMetadata(
        string productName,
        string productVersion,
        string upstreamRepository,
        string upstreamVersion,
        string upstreamCommit,
        OrderedMap<string> requirements)
    {
        ContractGuard.NotNull(requirements, nameof(requirements));

        ProductName = ContractGuard.RequiredText(productName, nameof(productName));
        ProductVersion = ContractGuard.RequiredText(productVersion, nameof(productVersion));
        UpstreamRepository = ContractGuard.RequiredText(upstreamRepository, nameof(upstreamRepository));
        UpstreamVersion = ContractGuard.RequiredText(upstreamVersion, nameof(upstreamVersion));
        UpstreamCommit = ContractGuard.RequiredText(upstreamCommit, nameof(upstreamCommit));
        Requirements = requirements;
    }

    /// <summary>
    /// Gets the released product name.
    /// </summary>
    [JsonPropertyOrder(0)]
    public string ProductName { get; }

    /// <summary>
    /// Gets the released product version.
    /// </summary>
    [JsonPropertyOrder(1)]
    public string ProductVersion { get; }

    /// <summary>
    /// Gets the compatible upstream repository.
    /// </summary>
    [JsonPropertyOrder(2)]
    public string UpstreamRepository { get; }

    /// <summary>
    /// Gets the compatible upstream version.
    /// </summary>
    [JsonPropertyOrder(3)]
    public string UpstreamVersion { get; }

    /// <summary>
    /// Gets the exact compatible upstream commit.
    /// </summary>
    [JsonPropertyOrder(4)]
    public string UpstreamCommit { get; }

    /// <summary>
    /// Gets required component versions in stable display and serialization order.
    /// </summary>
    [JsonPropertyOrder(5)]
    public OrderedMap<string> Requirements { get; }

    /// <summary>
    /// Creates metadata from a pinned compatibility identity.
    /// </summary>
    public static CompatibilityMetadata FromIdentity(
        string productName,
        string productVersion,
        CompatibilityIdentity identity,
        string invisibleDragonCoreApiVersion)
    {
        ContractGuard.NotNull(identity, nameof(identity));

        OrderedMap<string> requirements = new OrderedMap<string>()
            .Add(
                "invisible_dragon_core_api",
                ContractGuard.RequiredText(
                    invisibleDragonCoreApiVersion,
                    nameof(invisibleDragonCoreApiVersion)))
            .Add("energyplus", identity.EnergyPlusVersion);

        return new CompatibilityMetadata(
            productName,
            productVersion,
            identity.UpstreamRepository,
            identity.UpstreamVersion,
            identity.UpstreamCommit,
            requirements);
    }
}
