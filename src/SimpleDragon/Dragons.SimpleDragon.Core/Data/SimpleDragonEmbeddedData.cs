using System.Collections.ObjectModel;

namespace Dragons.SimpleDragon;

/// <summary>
/// Access to the exact upstream CSV bytes embedded in SimpleDragon.Core.
/// </summary>
public static class SimpleDragonEmbeddedData
{
    public const string Material = "construction/material.csv";
    public const string SurfaceRegulations = "construction/construction_regulation_surface.csv";
    public const string FenestrationRegulations = "construction/construction_regulation_fenestration.csv";
    public const string KoreanHoliday = "profile/KoreanHoliday.csv";
    public const string KoreanUsageProfile = "profile/KoreanUsageProfile.csv";
    public const string KoreanUsageProfileExtended = "profile/KoreanUsageProfileExtended.csv";
    public const string ClimateRegions = "weather/기후지역.csv";
    public const string AddressWeather = "weather/행정구역별기상데이터.csv";

    private static readonly ReadOnlyDictionary<string, string> ResourceNames =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Material] = "Dragons.SimpleDragon.Data.construction.material.csv",
                [SurfaceRegulations] = "Dragons.SimpleDragon.Data.construction.construction_regulation_surface.csv",
                [FenestrationRegulations] = "Dragons.SimpleDragon.Data.construction.construction_regulation_fenestration.csv",
                [KoreanHoliday] = "Dragons.SimpleDragon.Data.profile.KoreanHoliday.csv",
                [KoreanUsageProfile] = "Dragons.SimpleDragon.Data.profile.KoreanUsageProfile.csv",
                [KoreanUsageProfileExtended] = "Dragons.SimpleDragon.Data.profile.KoreanUsageProfileExtended.csv",
                [ClimateRegions] = "Dragons.SimpleDragon.Data.weather.climate-regions.csv",
                [AddressWeather] = "Dragons.SimpleDragon.Data.weather.address-weather.csv",
            });

    public static IReadOnlyList<string> Files { get; } = Array.AsReadOnly(
        new[]
        {
            Material,
            SurfaceRegulations,
            FenestrationRegulations,
            KoreanHoliday,
            KoreanUsageProfile,
            KoreanUsageProfileExtended,
            ClimateRegions,
            AddressWeather,
        });

    public static Stream OpenRead(string path)
    {
        Internal.DomainSupport.NotNull(path, nameof(path));

        if (!ResourceNames.TryGetValue(path.Replace('\\', '/'), out string? resourceName))
        {
            throw new FileNotFoundException("The embedded SimpleDragon data file is not registered.", path);
        }

        Stream? stream = typeof(SimpleDragonEmbeddedData).Assembly.GetManifestResourceStream(resourceName);
        return stream ?? throw new InvalidOperationException(
            "The embedded SimpleDragon data resource is missing: " + resourceName);
    }

    public static byte[] ReadAllBytes(string path)
    {
        using Stream input = OpenRead(path);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
}
