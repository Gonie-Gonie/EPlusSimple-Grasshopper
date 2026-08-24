namespace GonieGonie.EnergyPlus.Runtime;

/// <summary>
/// Immutable identity of the EnergyPlus executable set supported by version 0.1.
/// </summary>
public sealed record EnergyPlusRuntimeIdentity(
    string Version,
    string Build,
    string EnergyPlusExecutableSha256,
    string IddSha256,
    string ExpandObjectsSha256)
{
    public static EnergyPlusRuntimeIdentity Supported { get; } = new(
        "24.2.0",
        "94a887817b",
        "95f6047e26b9144fcff7771a85afb1e09da1f2434b748b24c092a0be5ac94728",
        "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2",
        "a15bd8e10f6a004e270fa4761527cabc95f776c089b92c11603faba19ed541ae");
}
