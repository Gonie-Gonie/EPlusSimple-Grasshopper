using GonieGonie.BuildingEnergy.Contracts;

namespace GonieGonie.SimpleDragon;

/// <summary>
/// Public version and compatibility information for SimpleDragon.Core.
/// </summary>
public static class PackageInfo
{
    public const string Name = "SimpleDragon";

    public const string Version = "0.1.0";

    public static CompatibilityIdentity Compatibility => CompatibilityIdentity.Current;
}
