using GonieGonie.BuildingEnergy.Contracts;

namespace GonieGonie.InvisibleDragon;

/// <summary>
/// Public version and compatibility information for InvisibleDragon.Core.
/// </summary>
public static class PackageInfo
{
    public const string Name = "InvisibleDragon";

    public const string Version = "0.1.0";

    public static CompatibilityIdentity Compatibility => CompatibilityIdentity.Current;
}
