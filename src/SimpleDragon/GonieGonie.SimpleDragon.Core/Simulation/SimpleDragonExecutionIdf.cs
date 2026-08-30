using GonieGonie.InvisibleDragon.Model;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Owns the one EnergyPlus execution profile used by both single and batch
/// SimpleDragon simulations. Compatibility-export defaults remain independent.
/// </summary>
internal static class SimpleDragonExecutionIdf
{
    internal const string ProfileIdentity = "energyplus-24.2-simpledragon-execution-v1";

    internal static EnergyModelIdfOptions CreateOptions() =>
        new()
        {
            ThrowOnValidationErrors = false,
            UseLegacyRectangularFenestration = true,
            UseLegacySimpleDragonScheduleMetadata = true,
            UseLegacySimpleDragonDefaultObjectFields = true,
            UseLegacySimpleDragonUsedProfileScheduleSelection = true,
            UseLegacySimpleDragonHvacTopology = false,
            UseLegacySimpleDragonVentilation = true,
        };
}
