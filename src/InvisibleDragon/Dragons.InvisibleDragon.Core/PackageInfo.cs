using Dragons.BuildingEnergy.Contracts;

namespace Dragons.InvisibleDragon;

/// <summary>
/// Public version and compatibility information for InvisibleDragon.Core.
/// </summary>
public static class PackageInfo
{
    public const string Name = "InvisibleDragon";

    public const string Version = "0.1.1";

    public static CompatibilityIdentity Compatibility => CompatibilityIdentity.Current;
}
